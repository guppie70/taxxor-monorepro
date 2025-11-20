using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

// To make sure we can easily refer to the static methods defined in the framework
using static Framework;

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
            // Wait for the application to finish it's initiation process
            while (!AppInitiated)
            {
                // Console.WriteLine("Waiting for the application to finish the initiation process");
                await PauseExecution(100);
            }

            //
            // => Retrieve the system state
            //
            try
            {
                var xPath = $"/configuration/taxxor/components/service//*[(local-name()='service' or local-name()='web-application') and @id='taxxoreditor']//routes//route[@id='systemstate']";
                if (xmlApplicationConfiguration.SelectSingleNode(xPath) != null)
                {
                    XmlDocument responseXml = await Taxxor.Project.ProjectLogic.CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.Editor, RequestMethodEnum.Get, "systemstate");
                    if (!XmlContainsError(responseXml))
                    {
                        Taxxor.Project.ProjectLogic.SystemState.FromXml(responseXml);
                    }
                }
                else
                {
                    if (siteType == "local" || siteType == "dev") Console.WriteLine("WARNING: Could not find the systemstate API definition");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Unable to retrieve the editor system state. error: {ex}");
            }


            //
            // => Sync EFR data which is captured during a sync process
            //
            try
            {
                var renderScheduledSdsSyncLogs = siteType == "local" || siteType == "dev";
                renderScheduledSdsSyncLogs = true;
                var isSyncRunning = Taxxor.Project.ProjectLogic.SystemState.ErpImport.IsRunning || Taxxor.Project.ProjectLogic.SystemState.SdsSync.IsRunning;
                // var isSyncRunning = Taxxor.Project.ProjectLogic.SystemState.SdsSync.IsRunning;
                

                if (!Taxxor.Project.ProjectLogic.SyncSections.IsEmpty && !isSyncRunning)
                {
                    if (renderScheduledSdsSyncLogs) Console.WriteLine("* Starting SDE sync as a task *");


                    var watch = System.Diagnostics.Stopwatch.StartNew();

                    var dictKeys = Taxxor.Project.ProjectLogic.SyncSections.Keys;
                    var failedSyncItems = new Dictionary<string, string[]>();
                    var sycSectionsContent = Taxxor.Project.ProjectLogic.SyncSections.ToArray();

                    foreach (var key in dictKeys)
                    {
                        if (Taxxor.Project.ProjectLogic.SyncSections.TryRemove(key, out string[] sectionInformation))
                        {
                            if (renderScheduledSdsSyncLogs) Console.WriteLine($"SDE sync for {string.Join(',', sectionInformation)}");
                            var projectIdToSync = sectionInformation[0];
                            var dataReferenceIdToSync = sectionInformation[1];
                            var attempts = Convert.ToInt32(sectionInformation[2]);

                            try
                            {
                                var cacheUpdateResult = await Taxxor.Project.ProjectLogic.SyncStructuredDataElementsPerDataReference(sectionInformation[0], sectionInformation[1]);
                                if (!cacheUpdateResult.Success)
                                {
                                    Console.WriteLine($"ERROR: Unable to update SDE cache file for dataReference: {dataReferenceIdToSync} in project: {projectIdToSync}. {cacheUpdateResult.ToString()}");
                                    attempts++;
                                    sectionInformation[2] = attempts.ToString();
                                    failedSyncItems.Add(key, sectionInformation);

                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"ERROR: Unable to update SDE cache file for dataReference: {dataReferenceIdToSync} in project: {projectIdToSync}. Error: {ex.ToString()}");
                                attempts++;
                                sectionInformation[2] = attempts.ToString();
                                failedSyncItems.Add(key, sectionInformation);
                            }
                        }
                        else
                        {
                            Console.WriteLine("ERROR: Unable to take sync information from the queue");
                        }
                    }

                    // Add the sync information back to the queue when it has failed so we can try it again later
                    if (failedSyncItems.Count > 0)
                    {
                        Console.WriteLine($"INFO: Adding {failedSyncItems.Count} failed sync items back to the queue");
                        foreach (var syncItem in failedSyncItems)
                        {
                            var key = syncItem.Key;
                            var sectionInformation = syncItem.Value;

                            var attempts = Convert.ToInt32(sectionInformation[2]);
                            if (attempts <= 3)
                            {
                                if (!Taxxor.Project.ProjectLogic.SyncSections.TryAdd(key, sectionInformation))
                                {
                                    Console.WriteLine($"ERROR: Unable to add {key} back to the SDE sync queue because it already exists");
                                };
                            }
                            else
                            {
                                Console.WriteLine($"ERROR: Stopped trying to sync dataReference: {sectionInformation[1]} in project: {sectionInformation[0]} after {attempts - 1} attempts");
                            }
                        }
                    }
                    watch.Stop();
                    if (renderScheduledSdsSyncLogs) Console.WriteLine($"* Ending SDE sync as a task after {watch.ElapsedMilliseconds} ms *");


                }
                else
                {
                    // if (renderScheduledSdsSyncLogs) Console.WriteLine("###> Nothing to sync from the clue <###>");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Something went while executing a scheduled task (every minute). error: {ex}");
            }
        }
    }
}