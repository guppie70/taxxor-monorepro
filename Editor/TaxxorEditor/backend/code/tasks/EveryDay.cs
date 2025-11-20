using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DocumentStore.Protos;
using Microsoft.Extensions.Logging;
// To make sure we can easily refer to the static methods defined in the framework
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace ScheduledTasks
{
    public class ScheduledTaskEveryDay(FilingComposerDataService.FilingComposerDataServiceClient filingComposerDataClient) : IScheduledTask
    {
        public string Schedule => "0 1 * * *"; // runs every day at 01:00
        private readonly FilingComposerDataService.FilingComposerDataServiceClient _filingComposerDataClient = filingComposerDataClient;

        /// <summary>
        /// Runs a background job every day
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Wait for the application to finish it's initiation process
            while (!AppInitiated)
            {
                // Console.WriteLine("Waiting for the application to finish the initiation process");
                await PauseExecution(100);
            }

            // Pull the custom frontend repository
            var customFrontendGitPullResult = GitPull($"{applicationRootPathOs}/frontend/public/custom");
            if (!customFrontendGitPullResult.Success)
            {
                Console.WriteLine($"ERROR: Unable to pull the customfrontend repository: {customFrontendGitPullResult.Message}, {customFrontendGitPullResult.DebugInfo}");
            }




            if (siteType != "local")
            {

                //
                // => Update the drawing renditions for the open projects
                //
                try
                {
                    var processOnlyOpenProjects = true;

                    Console.WriteLine("** Start updating drawing rendition libraries for open projects **");
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var xPathProjects = (processOnlyOpenProjects) ? "/configuration/cms_projects/cms_project[versions/version/status/text() = 'open']" : "/configuration/cms_projects/cms_project";
                    var nodeListProjects = xmlApplicationConfiguration.SelectNodes(xPathProjects);
                    foreach (XmlNode nodeCurrentProject in nodeListProjects)
                    {
                        var currentProjectId = GetAttribute(nodeCurrentProject, "id") ?? "";
                        // appLogger.LogInformation($"currentProjectId: {currentProjectId}");
                        if (currentProjectId != "") await Taxxor.Project.ProjectLogic.UpdateDrawingLibraryRenditions(currentProjectId);
                    }
                    watch.Stop();
                    Console.WriteLine($"** Finished updating drawing rendition libraries for open projects in {watch.ElapsedMilliseconds.ToString()} ms **");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Unable to update the drawing renditions for all open projects. error: {ex}");
                }

                //
                // => Update the graph renditions for the open projects
                //
                try
                {
                    var processOnlyOpenProjects = true;

                    Console.WriteLine("** Start updating graph rendition libraries for open projects **");
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var xPathProjects = (processOnlyOpenProjects) ? "/configuration/cms_projects/cms_project[versions/version/status/text() = 'open']" : "/configuration/cms_projects/cms_project";
                    var nodeListProjects = xmlApplicationConfiguration.SelectNodes(xPathProjects);
                    foreach (XmlNode nodeCurrentProject in nodeListProjects)
                    {
                        var currentProjectId = GetAttribute(nodeCurrentProject, "id") ?? "";
                        appLogger.LogInformation($"currentProjectId: {currentProjectId}");
                        if (currentProjectId != "")
                        {
                            var graphRenditionsResult = await Taxxor.Project.ProjectLogic.GenerateGraphRenditions(currentProjectId);
                            if (!graphRenditionsResult.Success)
                            {
                                Console.WriteLine($"Failed rendering graph renditions for {currentProjectId}");
                                Console.WriteLine(graphRenditionsResult.LogToString());
                            }
                        }
                    }
                    watch.Stop();
                    Console.WriteLine($"** Finished updating graph rendition libraries for open projects in {watch.ElapsedMilliseconds.ToString()} ms **");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Unable to update the graph renditions for all open projects. error: {ex}");
                }
            }

            //
            // => Find active section xml files and check if they are conforming to XHTML srict
            //
            try
            {
                var sharedFoldePath = RetrieveSharedFolderPathOs();
                var sharedCacheFolderPathOs = $"{sharedFoldePath}/cache";
                if (!Directory.Exists(sharedCacheFolderPathOs)) Directory.CreateDirectory(sharedCacheFolderPathOs);
                var lastXhtmlRunFilePathOs = $"{sharedCacheFolderPathOs}/_last-xhtml-run.txt";
                var runXhtmlCheck = false;
                if (File.Exists(lastXhtmlRunFilePathOs))
                {
                    // Read the last run date from the file and if it's been more than 24 hours since the last run then run the check again
                    var lastRunDate = DateTime.Parse(File.ReadAllText(lastXhtmlRunFilePathOs));
                    if (DateTime.Now - lastRunDate > TimeSpan.FromHours(24)) runXhtmlCheck = true;
                }
                else
                {
                    runXhtmlCheck = true;
                }

                if (runXhtmlCheck)
                {
                    Console.WriteLine("** Start checking XHTML compliance **");

                    // Execute the XHTML validation check
                    await CheckAllXhtmlSections(_filingComposerDataClient);

                    // Analyze the validation results and check if the documentstore was ready when we ran the routine
                    var documentstoreReady = WasDocumentStoreReadyDuringXhtmlValidation(validationErrorDetailsList);

                    if (documentstoreReady)
                    {

                        if (!validationErrorDetailsList.IsEmpty)
                        {
                            // Retrieve the addresses to send the report to
                            var nodeListSubscribers = xmlApplicationConfiguration.SelectNodes("/configuration/general/settings/setting[@id='mail-subscribers']/xhtml-validation/subscriber");

                            // Render the validation report
                            var validationReport = RenderXhtmlValidationReport(validationErrorDetailsList, true, ReturnTypeEnum.Html);

                            if (nodeListSubscribers != null && siteType == "prod")
                            {
                                var mailErrorLog = new List<string>();
                                foreach (XmlNode nodeCurrentSubscriber in nodeListSubscribers)
                                {
                                    var email = nodeCurrentSubscriber.GetAttribute("email");
                                    var name = nodeCurrentSubscriber.InnerText;
                                    if (email != null && name != null)
                                    {
                                        var emailResult = await EmailService.SendGraphEmailAsync($"{UpperFirst(TaxxorClientId)}: XHTML validation report", validationReport, email);
                                        if (!emailResult.Success)
                                        {
                                            var errorMessage = $"ERROR: Could not send email with Microsoft Sendgrid API. error: {emailResult.Message}";
                                            if (!mailErrorLog.Contains(errorMessage)) mailErrorLog.Add($"ERROR: Could not send email with Microsoft Sendgrid API. error: {emailResult.Message}");
                                        }
                                    }
                                }

                                if (mailErrorLog.Count > 0)
                                {
                                    Console.WriteLine(string.Join("\n", mailErrorLog));
                                }
                            }

                            await TextFileCreateAsync(validationReport, CalculateFullPathOs("xhtml-validation-report"));

                            // Cleanup validation report issues
                            validationErrorDetailsList.Clear();
                        }

                        // Store the current timestamp in the cache file to control when the next run is needed
                        File.WriteAllText(lastXhtmlRunFilePathOs, DateTime.Now.ToString());

                        Console.WriteLine("** Finished checking XHTML compliance **");
                    }
                    else
                    {
                        Console.WriteLine("Documentstore not ready, skipping report of validation errors");
                    }

                }
                else
                {
                    Console.WriteLine("** XHTML compliance check skipped as it has been done within the last 24 hours **");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Problems occurred while checking the XHTML compliance. error: {ex}");
                Console.WriteLine("** Finished checking XHTML compliance **");
            }

            //
            // => Once a day we clear all the cache
            //
            try
            {
                RbacCacheData.Clear();

                UserSectionInformationCacheData.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Unable to clear the RBAC and user section information cache. error: {ex}");
            }




            try
            {
                xmlApplicationConfiguration = CompileApplicationConfiguration(xmlApplicationConfiguration);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to build a new application configuration. error: {ex}");
            }



            //
            // => Remove diff files from the root of the shared folder
            //
            var maxLongAgeInHours = 24;
            try
            {
                bool CheckIsDiffFile(FileInfo f)
                {
                    return f.Name.StartsWith("diff-");
                }
                Directory.GetFiles(Taxxor.Project.ProjectLogic.RetrieveSharedFolderPathOs())
                                .Select(f => new FileInfo(f))
                                .Where(f => f.LastAccessTime < DateTime.Now.AddHours(-maxLongAgeInHours))
                                .Where(f => CheckIsDiffFile(f))
                                .ToList()
                                .ForEach(f => f.Delete());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not remove diff files from '{Taxxor.Project.ProjectLogic.RetrieveSharedFolderPathOs()}', error: {ex}, stack-trace: {GetStackTrace()}");
            }

            //
            // => Remove generated xbrl package folders from the root of the shared folder
            //
            var xbrlDirectoryPathOs = "";
            try
            {
                bool CheckIfXbrlDirectoryShouldBeRemoved(DirectoryInfo d)
                {
                    return d.Name.StartsWith("xbrlpackage");
                }

                string? sharedFolderPathOs = Taxxor.Project.ProjectLogic.RetrieveSharedFolderPathOs();
                if (sharedFolderPathOs != null)
                {
                    Directory.GetDirectories(sharedFolderPathOs)
                        .Select(d => new DirectoryInfo(d))
                        .Where(d => d.CreationTime < DateTime.Now.AddHours(-maxLongAgeInHours))
                        .Where(d => CheckIfXbrlDirectoryShouldBeRemoved(d))
                        .ToList()
                        .ForEach(async (d) =>
                        {
                            // For logging
                            xbrlDirectoryPathOs = d.FullName;

                            bool success = await DelTreeAsync(d.FullName);
                            if (!success)
                            {
                                Console.WriteLine($"ERROR: Could not async remove directories from '{sharedFolderPathOs}', currentDirectory: {xbrlDirectoryPathOs}");
                            }
                        });
                }
                else
                {
                    Console.WriteLine("INFO: Could not retrieve the shared folder path");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not remove directories from '{Taxxor.Project.ProjectLogic.RetrieveSharedFolderPathOs()}', currentDirectory: {xbrlDirectoryPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
            }

        }
    }
}