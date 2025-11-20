using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace Taxxor.Project
{



    public class SdsSyncRunDetails : BackgroundTaskRunDetails
    {

    }


    /// <summary>
    /// Settings to use for the SDE sync
    /// </summary>
    public class SdsSyncSettings(RequestVariables reqVars, ProjectVariables projectVars)
    {
        public ProjectVariables ProjectVars { get; set; } = projectVars;
        public RequestVariables ReqVars { get; set; } = reqVars;

        public bool CombinedWithErpImport { get; set; } = false;

        public string RunId { get; set; } = Guid.NewGuid().ToString();
    }

    public class SdsSyncService(ILogger<SdsSyncService> logger) : ISdsSyncService
    {

        private readonly ILogger<SdsSyncService> _logger = logger;


        public async Task<bool> Synchronize(SdsSyncSettings settings)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";
            var testForLocks = false;
            var generateVersions = siteType != "local";
            generateVersions = true;

            try
            {
                if (settings.ProjectVars.projectId == null)
                {
                    throw new Exception("Project ID send to SDE sync background process is null");
                }

                // Retrieve the import details object
                var runId = settings?.RunId ?? "willnevermatch";
                var erpImportDetails = SdsSynchronizationStatus[runId];
                erpImportDetails.RunId = runId;

                // Update the system state
                SystemState.SdsSync.IsRunning = true;
                SystemState.SdsSync.ProjectId = settings.ProjectVars.projectId;

                Console.WriteLine("!!!!!!!!!! STARTING SDS SYNCHRONIZATION AS A BACKGROUND PROCESS !!!!!!!!!!");

                var projectVars = settings.ProjectVars;
                var reqVars = settings.ReqVars;


                //
                // => Update the system state
                //
                SystemState.SdsSync.IsRunning = true;
                SystemState.SdsSync.ProjectId = projectVars.projectId;

                //
                // => Store the status in the document store
                //
                await SystemState.UpdateRemote();

                try
                {

                    //
                    // => Start syncing the structured data elements
                    //
                    var syncStructuredDataElementsResult = await SyncStructuredDataElementsPerDataReference(projectVars.projectId, "1", true, generateVersions, testForLocks, true, null, reqVars, projectVars, settings);

                    //
                    // => Update the CMS Metadata object in the Project Data Store and then use that information to update the local XML object as well
                    //
                    await UpdateCmsMetadataRemoteAndLocal(projectVars.projectId);

                    //
                    // => Clear the paged media cache
                    // 
                    ContentCache.Clear(projectVars.projectId);


                    //
                    // => Render the end result of the process as a websocket message
                    //
                    if (!syncStructuredDataElementsResult.Success)
                    {
                        await SystemState.SdsSync.Reset(true);

                        appLogger.LogError($"{syncStructuredDataElementsResult.ToString()}");
                        await MessageToCurrentClient("SyncStructuredDataDone", syncStructuredDataElementsResult);
                    }
                    else
                    {
                        await SystemState.SdsSync.Reset(true);

                        // Construct a response message for the client
                        var responseMessage = $"Successfully synchronized structured data elements. Please check the Auditor View for details";
                        if (!string.IsNullOrEmpty(syncStructuredDataElementsResult.Payload))
                        {
                            responseMessage = $"<pre>{syncStructuredDataElementsResult.Payload}</pre>";
                        }

                        string? debugInformation = null;
                        if (siteType != "prod")
                        {
                            debugInformation = syncStructuredDataElementsResult.DebugInfo;
                        }

                        // Clear the datastore cache
                        var clearCacheResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.ClearCache(projectVars, debugRoutine);
                        if (!clearCacheResult.Success)
                        {
                            appLogger.LogWarning($"Failed to clear the document store cache. details: {clearCacheResult.ToString()}");
                        }


                        // Send the result of the synchronization process back to the client via a websocket call
                        await MessageToOtherClient("SyncStructuredDataDone", projectVars.currentUser.Id, responseMessage, debugInformation);
                    }
                }
                catch (Exception ex)
                {
                    await SystemState.SdsSync.Reset(true);
                    var errorMessage = "There was an error in the SDE synchronization process";
                    appLogger.LogError(ex, errorMessage);

                    await MessageToOtherClient("SyncStructuredDataDone", projectVars.currentUser.Id, $"ERROR: {errorMessage}");
                }



                SystemState.SdsSync.IsRunning = false;
                SystemState.SdsSync.ProjectId = null;

                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                return true;
            }
            catch (Exception ex)
            {
                SystemState.SdsSync.IsRunning = false;
                SystemState.SdsSync.ProjectId = null;
                var errorMessage = $"Error occured during SDS sync";
                _logger.LogError(ex, errorMessage);

                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                return false;
            }

        }

    }

    public interface ISdsSyncService
    {
        Task<bool> Synchronize(SdsSyncSettings settings);
    }

}