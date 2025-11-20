using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
// To make sure we can easily refer to the static methods defined in the framework
using static Framework;

namespace ScheduledTasks
{
    public class ScheduledTaskEveryFiveMinutes : IScheduledTask
    {
        public string Schedule => "*/5 * * * *"; // runs every five minutes


        /// <summary>
        /// Executed every two minutes
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

            try
            {
                // Update the information about the GIT repositories used in this application so that we can use that when rendering the service description
                var success = Taxxor.Project.ProjectLogic.RetrieveTaxxorGitRepositoriesVersionInfo(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Something went while updating the git repositories version information. error: {ex}");
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

                    if (needsToBeSaved) await xmlLog.SaveAsync(uriLogPathOs, true, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Could not create the URI logging document. error: {ex}");
                }
            }

            //
            // => Remove old broken link reports
            //
            try
            {
                if (Directory.Exists($"{logRootPathOs}/links"))
                {
                    var brokenLinkReportSubDirectories = Directory.GetDirectories($"{logRootPathOs}/links");
                    foreach (var brokenLinkDirectoryPathOs in brokenLinkReportSubDirectories)
                    {
                        // Cleanup previously generated reports so that we know what to look for if we want to inspect the broken links
                        RemoveFilesOfSpecificAgeFromFolder(brokenLinkDirectoryPathOs, (60 * 60));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: There was an error remove broken link reports. error: {ex}");
            }

            //
            // => Remove potential locks in version control repositories if older than 5 minutes
            //
            try
            {
                var cmd = $"find {dataRootPathOs}/ -type f -cmin +5 -name \"index.lock\" -delete";
                var result = await ExecuteCommandLineAsync(cmd, dataRootPathOs);
                if(!result.Success){
                    Console.WriteLine($"Unable to execute git lock removal routine. result: {result.ToString()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: There was an error removing the git lock files. error: {ex}");
            }
        }
    }
}