using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
// To make sure we can easily refer to the static methods defined in the framework
using static Framework;
using static Taxxor.Project.ProjectLogic;

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

            //
            // => Wait for the application to finish it's initiation process
            //
            while (!AppInitiated)
            {
                // Console.WriteLine("Waiting for the application to finish the initiation process");
                await PauseExecution(100);
            }

            // Pause this script for 5 seconds to let the other scheduled tasks finish first
            await PauseExecution(5000);

            //
            // => Attempt to reload the data configuration when the data service has been unavailable
            //
            TaxxorReturnMessage updateApplicationConfigResult = await UpdateDataConfigurationInApplicationConfiguration();
            if (updateApplicationConfigResult.Success == false)
            {
                appLogger.LogWarning($"Could not restore data configuration. message: {updateApplicationConfigResult.Message}, debuginfo: {updateApplicationConfigResult.DebugInfo}");
            }


        }
    }
}