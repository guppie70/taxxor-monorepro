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
    public class ScheduledTaskEveryTwoMinutes : IScheduledTask
    {
        public string Schedule => "*/2 * * * *"; // runs every two minutes


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
                // Update the information about the connected Taxxor webservices
                await Taxxor.Project.ProjectLogic.CompileConnectedServicesInformation(siteType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Something went while updating the services information. error: {ex}");
            }


            try
            {
                // Render the CMS Content metadata
                await Taxxor.Project.ProjectLogic.UpdateCmsMetadata();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Something went while updating the metadata structures. error: {ex}");
            }
        }
    }
}