using System;
using System.Threading;
using System.Threading.Tasks;
// To make sure we can easily refer to the static methods defined in the framework
using static Framework;

namespace ScheduledTasks
{
    public class ScheduledTaskExample : IScheduledTask
    {
        public string Schedule => "*/1 * * * *"; // runs every minute
        // "*/5 * * * *" -> runs every 5 minutes => useful website for defining cron syntax: https://crontab.guru/


        /// <summary>
        /// Executes a scheduled background task
        /// Sample task to demonstrate how to implement these
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // await Task.Delay(5000, cancellationToken);

            // Sample code that receives some HTML from a remote URL and dumps it to the console
            var url = "https://www.nu.nl";
            try
            {
                string httpSource = await WebRequest<string>(url);

                httpSource = RemoveNewLines(httpSource);

                Console.WriteLine($"Recieved {TruncateString(httpSource, 100)} from nu.nl");
            }
            catch (Exception ex)
            {
                WriteErrorMessageToConsole("An error occured in the scheduled task", ex.ToString());
            }
        }
    }
}