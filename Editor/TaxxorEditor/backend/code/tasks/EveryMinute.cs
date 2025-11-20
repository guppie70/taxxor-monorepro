using System;
using System.Threading;
using System.Threading.Tasks;
// To make sure we can easily refer to the static methods defined in the framework
using static Framework;
using static Taxxor.ConnectedServices;
using static Taxxor.Project.ProjectLogic;

namespace ScheduledTasks
{
    public class ScheduledTaskEveryMinute : IScheduledTask
    {
        public string Schedule => "*/1 * * * *"; // runs every minute

        /// <summary>
        /// Runs a background job every minute
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {

            try
            {
                //
                // => Wait for the application to finish it's initiation process
                //
                while (!AppInitiated)
                {
                    // Console.WriteLine("Waiting for the application to finish the initiation process");
                    await PauseExecution(100);
                }

                //
                // => Check if RBAC cache (permissions) need to be updated
                //
                var clearPermissionsCache = false;
                string? currentRbacPermissionsHash = await AccessControlService.RetrieveHash();

                if (string.IsNullOrEmpty(currentRbacPermissionsHash))
                {
                    Console.WriteLine("ERROR: Retrieved an empty hash string from the RBAC service so we leave the cache in tact");
                }
                else
                {
                    if (currentRbacPermissionsHash != RbacPermissionsHash)
                    {
                        clearPermissionsCache = true;
                    }

                    if (clearPermissionsCache && !string.IsNullOrEmpty(currentRbacPermissionsHash))
                    {
                        Console.WriteLine("#######################");
                        Console.WriteLine("CLEAR PERMISSIONS CACHE");
                        Console.WriteLine("#######################");

                        // Clear the cache by loading a root element in the XmlDocument
                        RbacCacheData.Clear();

                        // Clear the user section information cache data
                        UserSectionInformationCacheData.Clear();

                        // Update the hash stored as a field of the Taxxor.Project.ProjectLogic class
                        RbacPermissionsHash = currentRbacPermissionsHash;
                    }
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: There was an error checking the permissions cache. error: {ex}");
            }




            //
            // => Log open file handles
            //
            // if (IsRunningInDocker())
            // {
            //     try
            //     {
            //         // In a docker container the main (dotnet) process always starts with pid 1
            //         var listOpenFileHandlesCommand = $"ls /proc/1/fd | wc -l";

            //         // Execute the command on the commandline
            //         var commandlist = new List<string>();
            //         commandlist.Add(listOpenFileHandlesCommand);
            //         var cmdResultRaw = ExecuteCommandLine<string>(commandlist, false, true, true, "cmd.exe", applicationRootPathOs);
            //         // Console.WriteLine("********");
            //         // Console.WriteLine(cmdResultRaw);
            //         // Console.WriteLine("********");

            //         // Post process the result so that we are only left with the number
            //         var openFileHandles = RemoveNewLines(cmdResultRaw.Trim());

            //         // Test if we are left with a number
            //         int openFileHandlesInteger;
            //         var isOpenFileHandlesValidInteger = int.TryParse(openFileHandles, out openFileHandlesInteger);

            //         // Log the result
            //         if (!isOpenFileHandlesValidInteger)
            //         {
            //             Console.WriteLine($"ERROR: cmdResultRaw: {cmdResultRaw}, listOpenFileHandlesCommand: {listOpenFileHandlesCommand}");
            //         }
            //         else
            //         {
            //             Console.WriteLine($"=> Open file handles result: {openFileHandles}");
            //             appLogger.LogInformation($"- openFileHandles: {openFileHandles}");
            //         }
            //     }
            //     catch (Exception ex)
            //     {
            //         Console.WriteLine($"ERROR: Could not check the open file handles. error: {ex}");
            //     }
            // }

            //
            // => Update the information about the connected Taxxor webservices
            //
            try
            {
                await UpdateTaxxorServicesInformation(siteType);
                UpdateStaticAssetsVersion();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could update the services information. error: {ex}");
            }

            //
            // => Loop through the validationErrorDetailsList, output the contents of each element and then empty the list after the loop
            //
            try
            {
                // await CheckAllXhtmlSections();

                // if (!validationErrorDetailsList.IsEmpty)
                // {
                //     Console.WriteLine("#######################");
                //     Console.WriteLine("VALIDATION ERROR DETAILS");
                //     Console.WriteLine("#######################");
                //     foreach (var validationErrorDetail in validationErrorDetailsList)
                //     {
                //         Console.WriteLine(validationErrorDetail);
                //     }
                //     validationErrorDetailsList.Clear();
                //     Console.WriteLine("#######################");
                // }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not output the validation error details. error: {ex}");
            }

        }
    }
}