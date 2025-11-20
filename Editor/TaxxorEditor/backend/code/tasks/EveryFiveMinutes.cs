using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
// To make sure we can easily refer to the static methods defined in the framework
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace ScheduledTasks
{
    public class ScheduledTaskEveryFiveMinutes : IScheduledTask
    {
        public string Schedule => "*/5 * * * *"; // runs every five minutes

        /// <summary>
        /// Executed every five minutes
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            //
            // => Wait for the application to finish it's initiation process
            //
            while (!AppInitiated)
            {
                // Console.WriteLine("Waiting for the application to finish the initiation process");
                await PauseExecution(100);
            }

            // Pause the execution of this logic for 10 seconds to allow the other scheduled tasks to finish first before attempting to run this code
            await PauseExecution(10000);

            //
            // => Update the information about the GIT repositories used in this application so that we can use that when rendering the service description
            //
            var successfullyUpdatedRepositoryVersionInformation = await Taxxor.Project.ProjectLogic.RetrieveTaxxorGitRepositoriesVersionInfo(false);
            if ((siteType == "local" || siteType == "dev") && !successfullyUpdatedRepositoryVersionInformation) appLogger.LogInformation($"- RetrieveTaxxorGitRepositoriesVersionInfo.success: {successfullyUpdatedRepositoryVersionInformation.ToString()}");

            // Update the version information in the ProjectLogic part
            if (successfullyUpdatedRepositoryVersionInformation)
            {
                RetrieveAppVersion();
                // Console.WriteLine($"=> applicationVersion: {applicationVersion}");
            }
            else
            {
                Console.WriteLine("Unable to retrieve repository version information (background task)");
            }

            //
            // => Delete temporary files and folders on the disk
            //
            var maxAgeInHours = (siteType == "local") ? 12 : 1;
            string[] fileExtensionsToDelete = { "pdf", "zip", "doc", "docx", "html", "htm", "svg", "xls", "xlsx" };
            var tempDataFolderPathOs = $"{dataRootPathOs}/temp";
            var directoryCreateError = false;
            try
            {
                if (!Directory.Exists(tempDataFolderPathOs))
                {
                    Directory.CreateDirectory(tempDataFolderPathOs);
                }
            }
            catch (Exception ex)
            {
                directoryCreateError = true;
                Console.WriteLine($"ERROR: Could not create local temp data folder: {tempDataFolderPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
            }

            if (!directoryCreateError)
            {


                // 1) Delete temporary files created in the local temp directory 
                foreach (var fileExtension in fileExtensionsToDelete)
                {
                    try
                    {
                        Directory.GetFiles(tempDataFolderPathOs, $"*.{fileExtension}")
                            .Select(f => new FileInfo(f))
                            .Where(f => f.LastAccessTime < DateTime.Now.AddHours(-maxAgeInHours))
                            .ToList()
                            .ForEach(f => f.Delete());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR: Could not remove {fileExtension} files from '{tempDataFolderPathOs}', error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }


                // 2) Delete temporary folders
                try
                {
                    Directory.GetDirectories(tempDataFolderPathOs)
                        .Select(d => new DirectoryInfo(d))
                        .Where(d => d.CreationTime < DateTime.Now.AddHours(-maxAgeInHours))
                        .ToList()
                        .ForEach(d => d.Delete(true));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Could not remove directories from '{tempDataFolderPathOs}', error: {ex}, stack-trace: {GetStackTrace()}");
                }

            }

            //
            // => Delete temporary files and folders in the shared temp directory
            //
            tempDataFolderPathOs = $"{Taxxor.Project.ProjectLogic.RetrieveSharedFolderPathOs()}/temp";
            directoryCreateError = false;
            try
            {
                if (!Directory.Exists(tempDataFolderPathOs))
                {
                    Directory.CreateDirectory(tempDataFolderPathOs);
                }
            }
            catch (Exception ex)
            {
                directoryCreateError = true;
                Console.WriteLine($"ERROR: Could not create shared temp data folder: {tempDataFolderPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
            }

            if (!directoryCreateError)
            {
                // 1) Delete temporary files
                foreach (var fileExtension in fileExtensionsToDelete)
                {
                    try
                    {
                        Directory.GetFiles(tempDataFolderPathOs, $"*.{fileExtension}")
                            .Select(f => new FileInfo(f))
                            .Where(f => f.LastAccessTime < DateTime.Now.AddHours(-maxAgeInHours))
                            .ToList()
                            .ForEach(f => f.Delete());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR: Could not remove {fileExtension} files from '{tempDataFolderPathOs}', error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }

                // 2) Delete temporary folders
                var currentDirectory = "";
                try
                {
                    // Leave website and xbrl directories on the local file system for inspection
                    bool CheckIfTempDirectoryShouldBeRemoved(DirectoryInfo d)
                    {
                        if (siteType == "local" || siteType == "dev")
                        {
                            return (d.Name.StartsWith("website_") == false && d.Name.StartsWith("xbrl_ar") == false);
                        }
                        return true;
                    }
                    Directory.GetDirectories(tempDataFolderPathOs)
                        .Select(d => new DirectoryInfo(d))
                        .Where(d => d.CreationTime < DateTime.Now.AddHours(-maxAgeInHours))
                        .Where(d => CheckIfTempDirectoryShouldBeRemoved(d))
                        .ToList()
                        .ForEach(async (d) =>
                        {
                            // For logging
                            currentDirectory = d.FullName;

                            var success = await DelTreeAsync(d.FullName);
                            if (!success)
                            {
                                Console.WriteLine($"ERROR: Could not async remove directories from '{tempDataFolderPathOs}', currentDirectory: {currentDirectory}");
                            }
                        });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Could not remove directories from '{tempDataFolderPathOs}', currentDirectory: {currentDirectory}, error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }

            //
            // => Refresh the local ip address and references to it
            //
            localIpAddress = GetLocalIpAddress();
            Taxxor.Project.ProjectLogic.RefreshLocalAddressAndIp = true;

            //
            // => Retrieve a fresh version of project_configuration.xml and store it locally as a failover file
            //
            var projectConfigurationLocationNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='xml_project_configuration_failover']");
            if (projectConfigurationLocationNode != null)
            {
                var projectConfigurationPathOs = CalculateFullPathOs(projectConfigurationLocationNode);
                // Console.WriteLine($"- projectConfigurationPathOs: {projectConfigurationPathOs}");
                var projectConfigurationUrl = CalculateFullPathOs("/configuration/configuration_system/config//location[@id='project_configuration']");
                // Console.WriteLine($"- projectConfigurationUrl: {projectConfigurationUrl}");

                try
                {
                    var xmlProjectConfiguration = new XmlDocument();
                    xmlProjectConfiguration.Load(projectConfigurationUrl);
                    await xmlProjectConfiguration.SaveAsync(projectConfigurationPathOs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: There was an error retrieving the project configuration file from {projectConfigurationUrl}. error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }
            else
            {
                Console.WriteLine($"ERROR: Could not retrieve project configuration failover location");
            }

            //
            // => Retrieve a fresh version of the data configuration.xml file and store it locally as a failover file
            //
            var dataConfigurationLocationNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='xml_data_configuration_failover']");
            if (dataConfigurationLocationNode != null)
            {
                var dataConfigurationPathOs = CalculateFullPathOs(dataConfigurationLocationNode);
                // Console.WriteLine($"- dataConfigurationPathOs: {dataConfigurationPathOs}");
                var dataConfigurationUrl = CalculateFullPathOs("/configuration/configuration_system/config//location[@id='data_configuration']");
                // Console.WriteLine($"- dataConfigurationUrl: {dataConfigurationUrl}");

                try
                {
                    var xmlProjectConfiguration = new XmlDocument();
                    xmlProjectConfiguration.Load(dataConfigurationUrl);
                    await xmlProjectConfiguration.SaveAsync(dataConfigurationPathOs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WARNING: There was an error retrieving the data configuration file from {dataConfigurationUrl}. error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }
            else
            {
                Console.WriteLine($"ERROR: Could not retrieve data configuration failover location");
            }

            //
            // => Store the captured URL's in a logfile on the server
            //
            if (Taxxor.Project.ProjectLogic.UriLogEnabled)
            {
                // Console.WriteLine("$$$$$$$$$ Logged URI's $$$$$$$$$");
                // Console.WriteLine(string.Join("\n", UriLogBackend.ToArray()));
                // Console.WriteLine("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");

                try
                {
                    var xmlLog = new XmlDocument();
                    var uriLogPathOs = $"{logRootPathOs}/inspector/_urilog.xml";
                    var needsToBeSaved = false;
                    if (!File.Exists(uriLogPathOs))
                    {
                        xmlLog.LoadXml("<log/>");
                    }
                    else
                    {
                        xmlLog.Load(uriLogPathOs);
                    }

                    foreach (var uriString in Taxxor.Project.ProjectLogic.UriLogBackend)
                    {
                        if (!string.IsNullOrEmpty(uriString))
                        {
                            var currentUri = new Uri(uriString);
                            var currentServiceName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/taxxor/components//*[uri and (contains(uri, '{currentUri.Port}') or contains(uri/domain, '{currentUri.Port}'))]")?.GetAttribute("id") ?? "unknown";

                            var nodeServiceRoot = xmlLog.SelectSingleNode($"/log/{currentServiceName}");
                            if (nodeServiceRoot == null)
                            {
                                var nodeNewServiceRoot = xmlLog.CreateElement(currentServiceName);
                                nodeServiceRoot = xmlLog.DocumentElement.AppendChild(nodeNewServiceRoot);
                                needsToBeSaved = true;
                            }

                            var nodeUri = nodeServiceRoot.SelectSingleNode($"uri[text()='{uriString}']");
                            if (nodeUri == null)
                            {
                                nodeUri = xmlLog.CreateElementWithText("uri", uriString);
                                nodeServiceRoot.AppendChild(nodeUri);
                                needsToBeSaved = true;
                            }
                        }
                    }

                    if (needsToBeSaved) await xmlLog.SaveAsync(uriLogPathOs, true, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Could not create the URI logging document. error: {ex}");
                }
            }

            //
            // => Store the state of the permissions cache in the logfolder
            //
            if (siteType != "local") await DumpRbacContents();

            //
            // => Update the CMS Content Metadata field
            //
            try
            {
                await UpdateCmsMetadata();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Unable to update the CMS Content Metadata. error: {ex}");
            }

            //
            // => Clear the cache of paths used for CSS/JS in pdf generation
            //
            try
            {
                ProjectExtensions.FsExistsCache.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Unable to clear the CSS/JS cache. error: {ex}");
            }

            //
            // => Retrieve static assets from the Taxxor static assets website
            //
            try
            {
                // <<path on static assets server>>, <<local path where the file should be stored>>
                var StaticAssetsToFetch = new Dictionary<string, string>
                {
                    { "/stylesheets/statusindicators.css", websiteRootPathOs + "/outputchannels/stylesheets/statusindicators.css" }
                };

                var result = await FetchStaticAssets(StaticAssetsToFetch);
                if (!result.Success)
                {
                    Console.WriteLine($"ERROR: Unable to fetch the static assets. error: {result.Message}, debuginfo: {result.DebugInfo}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Unable to retrieve local static assets. error: {ex}");
            }

        }
    }
}