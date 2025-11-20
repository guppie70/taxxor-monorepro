using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
// To make sure we can easily refer to the static methods defined in the framework
using static Framework;

namespace ScheduledTasks
{
    public class ScheduledTaskEveryDay : IScheduledTask
    {
        public string Schedule => "0 5 * * *"; // runs every day at 5 am


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

            //
            // => Update the image renditions for the open projects
            //
            try
            {
                var processOnlyOpenProjects = true;

                var xPathProjects = (processOnlyOpenProjects) ? "/configuration/cms_projects/cms_project[versions/version/status/text() = 'open']" : "/configuration/cms_projects/cms_project";

                var nodeListProjects = xmlApplicationConfiguration.SelectNodes(xPathProjects);
                foreach (XmlNode nodeCurrentProject in nodeListProjects)
                {
                    var currentProjectId = GetAttribute(nodeCurrentProject, "id") ?? "";
                    if (currentProjectId != "")
                    {
                        await Taxxor.Project.ProjectLogic.UpdateImageLibraryRenditions(currentProjectId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Unable to update the image renditions for all open projects. error: {ex}");
            }
        }
    }
}