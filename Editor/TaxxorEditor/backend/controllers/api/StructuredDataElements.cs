using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Syncs and rebuilds the structured data cache for a single data reference (content data file)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task SyncStructuredDataElementsPerDataReference(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            string? dataRef = request.RetrievePostedValue("datareference", RegexEnum.FileName, false, reqVars.returnType);
            string? snapshotId = request.RetrievePostedValue("snapshotid", RegexEnum.Default, false, reqVars.returnType, "1");
            string? sectionId = request.RetrievePostedValue("did", RegexEnum.Loose, false, reqVars.returnType);

            //
            // => Stylesheet cache
            //
            SetupPdfStylesheetCache(reqVars);

            if (string.IsNullOrEmpty(dataRef) && string.IsNullOrEmpty(sectionId))
            {
                HandleError("Not enough information supplied", $"dataRef: {dataRef}, sectionId: {sectionId}, stack-trace: {GetStackTrace()}");
            }
            else if (string.IsNullOrEmpty(dataRef))
            {
                // We need to figure out the data reference from the hierarchy before we can sync the content
                var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);

                if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                {
                    var xmlHierarchyDoc = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;

                    // Try to find the data reference by using the hierarchy id
                    var itemXpath = $"//item[@id={GenerateEscapedXPathString(sectionId)}]";
                    var nodeItem = xmlHierarchyDoc.SelectSingleNode(itemXpath);
                    if (nodeItem == null)
                    {
                        HandleError("Could not find item", $"itemXpath: {itemXpath}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        dataRef = nodeItem.GetAttribute("data-ref");
                        if (string.IsNullOrEmpty(dataRef)) HandleError("Could not locate data reference", $"stack-trace: {GetStackTrace()}");
                    }
                }
                else
                {
                    appLogger.LogError($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}']");
                }
            }

            // - Call the service and sync the structured data elements
            var dataToPost = new Dictionary<string, string>
            {
                { "projectid", projectVars.projectId },
                { "snapshotid", snapshotId },
                { "dataref", dataRef }
            };

            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorsyncstructureddatasnapshotperdataref", dataToPost, debugRoutine);
            if (XmlContainsError(xmlResponse))
            {
                HandleError(xmlResponse);
            }
            else
            {
                //
                // => Clear the paged media cache
                // 
                ContentCache.Clear(projectVars.projectId);


                //
                // => Commit the new and updated structured data cache files in the version control system
                //
                dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectVars.projectId },
                    { "vid", "latest" },
                    { "type", "text" },
                    { "linknames", "none" },
                    { "sectionids", sectionId },
                    { "crud", "structureddatasync" }
                };

                XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "contentversion", dataToPost, debugRoutine, false, projectVars);

                if (responseXml.OuterXml.Contains("nothing to commit"))
                {
                    appLogger.LogWarning("No changes were detected in the cache system by the version control application");
                }
                else
                {
                    // Handle error
                    if (XmlContainsError(responseXml))
                    {
                        appLogger.LogError($"There was an error committing the structured data elements cache to the version control system. stack-trace: {GetStackTrace()}");
                    }
                }

                //
                // => Generate graph renditions
                //
                var dataRefs = new List<string>
                {
                    dataRef
                };
                var graphRenditionsResult = await GenerateGraphRenditions(reqVars, projectVars, null, dataRefs);
                if (!graphRenditionsResult.Success)
                {
                    appLogger.LogError($"Failed rendering graph renditions. project id: {projectVars.projectId}, {graphRenditionsResult.LogToString()}");
                }
                else
                {
                    appLogger.LogInformation($"Successfully rendered graph renditions for {dataRef}");
                }

                //
                // => Clear the paged media cache
                //
                if (PdfRenderEngine == "pagedjs")
                {
                    // - Retrieve output channel hierarchy
                    var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);

                    if (!projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                    {
                        appLogger.LogInformation("Retrieving metadata from the server");
                        var hierarchyRetrieveResult = await RetrieveOutputChannelHierarchiesMetaData(projectVars, reqVars, true);
                        if (!hierarchyRetrieveResult)
                        {
                            appLogger.LogWarning($"Unable to clear the paged media cache as we were unable to retrieve the output channel hierarchy from the server (projectId: {projectVars.projectId}, projectVars.currentUser.Id: {projectVars.currentUser.Id}, outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey})");
                        }
                    }

                    if (projectVars.cmsMetaData.TryGetValue(outputChannelHierarchyMetadataKey, out MetaData? metaDataItem))
                    {
                        // - Retrieve the output channel hierarchy
                        XmlDocument xmlHierarchyDoc = metaDataItem.Xml;

                        ContentCache.Clear(projectVars.projectId, projectVars.outputChannelVariantId, sectionId);
                        // - execute for parents as well
                        var nodeItem = xmlHierarchyDoc.SelectSingleNode($"/items/structured//item[@id={GenerateEscapedXPathString(sectionId)}]");
                        var nodeListParentItems = nodeItem.SelectNodes("ancestor::item[parent::sub_items]");
                        foreach (XmlNode nodeParentItem in nodeListParentItems)
                        {
                            ContentCache.Clear(projectVars.projectId, projectVars.outputChannelVariantId, nodeParentItem.GetAttribute("id"));
                        }

                    }
                    else
                    {
                        appLogger.LogWarning($"Unable to clear the paged media cache because projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}']=null (projectId: {projectVars.projectId}, projectVars.currentUser.Id: {projectVars.currentUser.Id}, outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey})");
                    }
                }


                //
                // => Update the CMS Metadata object in the Project Data Store and then use that information to update the local XML object as well
                //
                await UpdateCmsMetadataRemoteAndLocal(projectVars.projectId);




                // Render the success response
                await response.OK(xmlResponse, reqVars.returnType);
            }
        }

        /// <summary>
        /// Kicks off the Structured Data Elements sync per data reference using an ajax call and then updates the client on the progress using websockets
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task SyncStructuredDataElementsStart(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var addPause = debugRoutine;
            addPause = false;

            var testForLocks = false;
            var generateVersions = (siteType != "local");
            generateVersions = true;

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var snapshotId = request.RetrievePostedValue("snapshotid", RegexEnum.Default, true, reqVars.returnType);

            if (SystemState.ErpImport.IsRunning || (SystemState.SdsSync.IsRunning && SystemState.SdsSync.ProjectId == projectVars.projectId))
            {
                var typeOfAction = SystemState.ErpImport.IsRunning ? "n ERP Import" : " SDS data sync";
                var returnMessage = new TaxxorReturnMessage(false, $"There is already a{typeOfAction} in progress. Please wait until these have been completed and try again later", SystemState.ToString());
                await response.Error(returnMessage, ReturnTypeEnum.Json, true);
                await response.CompleteAsync();
            }
            else
            {
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
                    // => Stylesheet cache
                    //
                    SetupPdfStylesheetCache(reqVars);

                    //
                    // => Return data to the web client to close the XHR call
                    //
                    await response.OK(GenerateSuccessXml("Started structured data sync", $"projectId: {projectVars.projectId}, snapshotId: {snapshotId}"), ReturnTypeEnum.Json, true, true);
                    await response.CompleteAsync();

                    //
                    // => Start syncing the structured data elements
                    //
                    var syncStructuredDataElementsResult = await SyncStructuredDataElementsPerDataReference(projectVars.projectId, snapshotId, true, generateVersions, testForLocks, true, null, reqVars, projectVars);

                    //
                    // => Update the CMS Metadata object in the Project Data Store and then use that information to update the local XML object as well
                    //
                    await UpdateCmsMetadataRemoteAndLocal(projectVars.projectId);

                    //
                    // => Clear the paged media cache
                    // 
                    ContentCache.Clear(projectVars.projectId);

                    //
                    // => Optionally pause the process for debugging purposes
                    //
                    if (addPause)
                    {
                        await MessageToCurrentClient("SyncStructuredDataProgress", "!!!!!! STARTING PAUSE !!!!!!");
                        await PauseExecution(50000);
                        await MessageToCurrentClient("SyncStructuredDataProgress", "!!!!!! ENDING PAUSE !!!!!!");
                    }

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
                        var clearCacheResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.ClearCache(debugRoutine);
                        if (!clearCacheResult.Success)
                        {
                            appLogger.LogWarning($"Failed to clear the document store cache. details: {clearCacheResult.ToString()}");
                        }


                        // Send the result of the synchronization process back to the client via a websocket call
                        await MessageToCurrentClient("SyncStructuredDataDone", responseMessage, debugInformation);
                    }
                }
                catch (Exception ex)
                {
                    await SystemState.SdsSync.Reset(true);
                    var errorMessage = "There was an error in the SDE synchronization process";
                    appLogger.LogError(ex, errorMessage);
                    await MessageToCurrentClient("SyncStructuredDataDone", $"ERROR: {errorMessage}");
                }
            }
        }

        /// <summary>
        /// Syncs structured data elements from the Taxxor Structured Data Store into this project datareference-by-datareference
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="snapshotId"></param>
        /// <param name="onlySyncSdeForSectionsInUse"></param>
        /// <param name="generateVersions"></param>
        /// <param name="testForLocks"></param>
        /// <param name="renderWebsocketMessages"></param>
        /// <returns></returns>
        public async static Task<TaxxorReturnMessage> SyncStructuredDataElementsPerDataReference(string projectId, string snapshotId, bool onlySyncSdeForSectionsInUse, bool generateVersions, bool testForLocks, bool renderWebsocketMessages, List<string> dataReferencesFixed, RequestVariables reqVars, ProjectVariables projectVars, SdsSyncSettings settings = null)
        {
            try
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                // Statistics that we gather of the synchronization process
                StringBuilder logSuccess = new StringBuilder();
                StringBuilder logWarning = new StringBuilder();
                StringBuilder logError = new StringBuilder();
                StringBuilder sbDebug = new StringBuilder();


                var step = 1;
                var stepMax = 4;
                if (projectVars != null && reqVars != null) stepMax = stepMax + 1;
                if (generateVersions) stepMax = stepMax + 2;

                //
                // => Check if there are any sections locked - if so, then back out 
                //
                if (testForLocks)
                {
                    var numberOfLocks = FilingLockStore.RetrieveNumberOfLocks(projectId);
                    if (numberOfLocks > 0)
                    {
                        return new TaxxorReturnMessage(false, $"Could not proceed because there {((numberOfLocks == 1) ? "is 1 section" : "are " + numberOfLocks + " sections")} locked in this project. Structured Data sync is only available when there are no sections locked in a project.", $"stack-trace: {GetStackTrace()}");
                    }
                }

                //
                // => Create a minor version before we sync
                //
                if (generateVersions)
                {
                    step = await SendMessageToClient(projectVars.currentUser.Id, "Create the pre-sync version of the content", step, stepMax);
                    var preSyncVerionResult = await GenerateVersion(projectId, $"Pre structured data synchronization (snapshot-id: {snapshotId}) version", false, projectVars);
                    if (!preSyncVerionResult.Success) return preSyncVerionResult;
                }

                //
                // => Retrieve all the metadata of the data used in this project
                //
                step = await SendMessageToClient(projectVars.currentUser.Id, "Retrieving content references to sync", step, stepMax);
                var metadataRetrieveResult = await RetrieveCmsMetadata(projectId);
                XmlDocument xmlCmsContentMetadata = new XmlDocument();
                if (!metadataRetrieveResult.Success)
                {
                    appLogger.LogError($"ERROR: unable to retrieve metadata. {metadataRetrieveResult.ToString()}");
                    return metadataRetrieveResult;
                }
                xmlCmsContentMetadata.ReplaceContent(metadataRetrieveResult.XmlPayload);


                //
                // => Calculate a list of data references to run the data sync for
                //
                var dataReferences = new List<string>();
                if (dataReferencesFixed == null)
                {
                    var xPathForDataReferences = "/projects/cms_project/data/content[@datatype='sectiondata']";
                    if (onlySyncSdeForSectionsInUse) xPathForDataReferences = "/projects/cms_project/data/content[@datatype='sectiondata' and metadata/entry[@key='inuse']/text()='true']";
                    var nodeListContentDataFiles = xmlCmsContentMetadata.SelectNodes(xPathForDataReferences);
                    foreach (XmlNode nodeContentDataFile in nodeListContentDataFiles)
                    {
                        var dataRef = nodeContentDataFile.GetAttribute("ref");
                        if (!string.IsNullOrEmpty(dataRef))
                        {
                            if (!dataReferences.Contains(dataRef)) dataReferences.Add(dataRef);
                        }
                        else
                        {
                            appLogger.LogWarning("Data reference was empty");
                        }
                    }

                    // Potentially expand the list of datareferences in a custom script
                    var customDataReferencesList = Extensions.ExtendSdeSycDataReferences(projectId);
                    if (customDataReferencesList.Count > 0)
                    {
                        foreach (var customDataReference in customDataReferencesList)
                        {
                            if (!dataReferences.Contains(customDataReference))
                            {
                                appLogger.LogInformation($"Adding {customDataReference} to the list of SDE sync references");
                                dataReferences.Add(customDataReference);
                            }
                        }
                    }

                }
                else
                {
                    dataReferences = dataReferencesFixed.ToList();
                }

                // Add the footnote XML to the list
                var nodeFootnoteEntry = xmlCmsContentMetadata.SelectSingleNode("/projects/cms_project/data/content[@datatype='footnote']");
                if (nodeFootnoteEntry != null)
                {
                    var footnoteDataReference = nodeFootnoteEntry.GetAttribute("ref");
                    if (!string.IsNullOrEmpty(footnoteDataReference))
                    {
                        dataReferences.Add(footnoteDataReference);
                    }
                }
                else
                {
                    appLogger.LogInformation($"This project with ID: {projectId} does not contain a footnote references datastore");
                }


                // Sort the datareference list so that the order is not influenced by the OS or the way that the cache is set-up
                dataReferences.Sort();

                //
                // => Remove the existing structured data cache from the server
                //
                step = await SendMessageToClient(projectVars.currentUser.Id, "Removing structured data cache from server", step, stepMax);
                var dataToPost = new Dictionary<string, string>
                {
                    { "projectid", projectId }
                };

                if (dataReferencesFixed == null)
                {
                    // Remove the complete cache
                    dataToPost.Add("scope", "all");
                }
                else
                {
                    // Only remove the cache files for the elements that we want to sync
                    dataToPost.Add("scope", "partial");
                    dataToPost.Add("datarefs", string.Join(',', dataReferences.ToArray()));
                }

                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Delete, "taxxoreditorsyncstructureddatasnapshot", dataToPost, debugRoutine, false, projectVars);
                if (XmlContainsError(xmlResponse)) return new TaxxorReturnMessage(xmlResponse);

                //
                // => Loop through these file references and sync the content per file
                //
                var consolidatedSyncStats = new SdeSyncStatictics();
                step = await SendMessageToClient(projectVars.currentUser.Id, "Start syncing structured data elements", step, stepMax);
                var countDataReferenceMax = dataReferences.Count;
                var countDataReferenceCurrent = 1;
                foreach (string dataReference in dataReferences)
                {

                    // Retrieve the section title of the section to generate a readable output for the end user
                    var outputChannelLang = (projectVars != null) ? ((string.IsNullOrEmpty(projectVars.outputChannelVariantLanguage)) ? projectVars.outputChannelDefaultLanguage : projectVars.outputChannelVariantLanguage) : "";
                    var sectionTitle = (!dataReference.StartsWith("_")) ? RetrieveSectionTitleFromMetadataCache(xmlCmsContentMetadata, dataReference, projectId, outputChannelLang)?.Trim() : "";

                    var sectionDisplayName = sectionTitle;
                    if (string.IsNullOrEmpty(sectionDisplayName))
                    {
                        sectionDisplayName = Path.GetFileNameWithoutExtension(dataReference);
                    }
                    else
                    {
                        if (sectionDisplayName.Contains('\n') || sectionDisplayName.Contains('{'))
                        {
                            sectionDisplayName = dataReference;
                        }
                    }

                    var message = $"* ({countDataReferenceCurrent}/{countDataReferenceMax}) - syncing '{sectionDisplayName}'";
                    await SendMessageToClient(projectVars.currentUser.Id, message);

                    // Calculate the progress of the EFR import process
                    if (settings != null && settings.CombinedWithErpImport)
                    {
                        double progressCorrectionFactor = 2;
                        SystemState.SdsSync.Progress = (1 / progressCorrectionFactor) + ((double)(countDataReferenceCurrent - 1) / (double)countDataReferenceMax) / progressCorrectionFactor;
                    }
                    else
                    {
                        SystemState.SdsSync.Progress = (double)(countDataReferenceCurrent - 1) / (double)countDataReferenceMax;
                    }

                    if (debugRoutine) Console.WriteLine($"-------------------\n{message}\n-------------------");

                    // - Call the service and sync the structured data elements
                    dataToPost = new Dictionary<string, string>
                    {
                        { "projectid", projectId },
                        { "snapshotid", snapshotId },
                        { "dataref", dataReference }
                    };

                    xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorsyncstructureddatasnapshotperdataref", dataToPost, debugRoutine, false, projectVars);
                    if (XmlContainsError(xmlResponse)) return new TaxxorReturnMessage(xmlResponse);

                    //
                    // => Deserialize the sync-statistics back from the XML we used for transport
                    //
                    var xml = HttpUtility.HtmlDecode(xmlResponse.SelectSingleNode("/result/payload").InnerText);
                    SdeSyncStatictics? syncStats = null;

                    try
                    {
                        using (var stringReader = new StringReader(xml))
                        {
                            var serializer = new DataContractSerializer(typeof(SdeSyncStatictics));
                            using (var xmlTextReader = new XmlTextReader(stringReader))
                            {
                                syncStats = (SdeSyncStatictics)serializer.ReadObject(xmlTextReader);
                                xmlTextReader.Close();
                            }
                            stringReader.Close();
                        }
                    }
                    catch (Exception exp)
                    {
                        appLogger.LogError(exp, "There was an error deserializing xml into a SdeSyncStatictics object");
                    }

                    //
                    // => Parse the result of this sync
                    //
                    consolidatedSyncStats.ConsolidateSdeSyncInfo(dataReference, syncStats);


                    // Update the counter
                    countDataReferenceCurrent++;
                }


                //
                // => Generate the sync log on the Document Store
                //
                step = await SendMessageToClient(projectVars.currentUser.Id, "Generating SDE sync log", step, stepMax);
                dataToPost = new Dictionary<string, string>
                {
                    { "projectid", projectId }
                };

                if (dataReferencesFixed == null)
                {
                    // Remove the complete cache
                    dataToPost.Add("scope", "all");
                }
                else
                {
                    // Only remove the cache files for the elements that we want to sync
                    dataToPost.Add("scope", "partial");
                    dataToPost.Add("datarefs", string.Join(',', dataReferences.ToArray()));
                }

                xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "taxxoreditorsyncstructureddatasnapshot", dataToPost, debugRoutine, false, projectVars);
                if (XmlContainsError(xmlResponse)) return new TaxxorReturnMessage(xmlResponse);



                //
                // => Commit the new and updated structured data cache files in the version control system
                //
                dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "vid", "latest" },
                    { "type", "text" },
                    { "linknames", "none" },
                    { "sectionids", $"{String.Join(",", dataReferences)}" },
                    { "crud", "structureddatasync" }
                };

                XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "contentversion", dataToPost, debugRoutine, false, projectVars);

                if (responseXml.OuterXml.Contains("nothing to commit"))
                {
                    appLogger.LogWarning("No changes were detected in the cache system by the version control application");
                }
                else
                {
                    // Handle error
                    if (XmlContainsError(responseXml))
                    {
                        appLogger.LogError($"There was an error committing the structured data elements cache to the version control system. stack-trace: {GetStackTrace()}");
                    }
                }


                //
                // => Generate the log message
                //
                var synchronizationResult = _renderStructuredDataSyncResponseMessage(projectId, snapshotId, consolidatedSyncStats, xmlCmsContentMetadata);


                //
                // => Create another minor version after the sync was completed
                //
                if (generateVersions)
                {
                    step = await SendMessageToClient(projectVars.currentUser.Id, "Create the post-sync version of the content", step, stepMax);
                    // Render an XML formatted message where we can fit in the log of the sync result as well
                    var xmlTagMessage = new XmlDocument();
                    xmlTagMessage.LoadXml("<data><title/><log/></data>");
                    xmlTagMessage.SelectSingleNode("/data/title").InnerText = $"Post structured data synchronization (snapshot-id: {snapshotId}) version";
                    if (!string.IsNullOrEmpty(synchronizationResult))
                    {
                        // If the sync log is too long, truncate it
                        var syncLog = TruncateString(synchronizationResult, 4500);

                        // Add the log to the XML that we dump as a message in the version
                        xmlTagMessage.SelectSingleNode("/data/log").InnerText = syncLog;
                    }

                    var postSyncVersionResult = await GenerateVersion(projectId, xmlTagMessage.OuterXml, false, projectVars);
                    if (!postSyncVersionResult.Success) return postSyncVersionResult;
                }

                //
                // => Generate graph renditions
                //
                if (projectVars != null && reqVars != null)
                {
                    step = await SendMessageToClient(projectVars.currentUser.Id, "Generating graph renditions", step, stepMax);
                    var graphRenditionsResult = await GenerateGraphRenditions(reqVars, projectVars);
                    if (!graphRenditionsResult.Success)
                    {
                        appLogger.LogError($"Failed rendering graph renditions. project id: {projectId}, {graphRenditionsResult.LogToString()}");
                        await _syncExternalTablesMessageToClient("ERROR: Failed to render graph renditions");
                    }
                }

                // Return a success message containing the sync result from the server
                return new TaxxorReturnMessage(true, "Successfully synced structured data elements", (string.IsNullOrEmpty(synchronizationResult) ? "No details" : synchronizationResult), "");
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error syncing the structured data elements in your project", $"error: {ex}");
            }

            async Task<int> SendMessageToClient(string userId, string message, int step = -1, int maxStep = -1)
            {

                var messageToClient = new TaxxorReturnMessage(true, (step == -1) ? message : $"Step {step}/{maxStep}: {message}");

                if (settings == null)
                {
                    await MessageToOtherClient("SyncStructuredDataProgress", userId, messageToClient);
                }
                else
                {
                    await MessageToAllClients("SyncStructuredDataProgress", messageToClient);
                }

                if (step > -1) step++;

                return step;
            }

        }

        /// <summary>
        /// Helper routine to append the sync log items to a stringbuilder
        /// </summary>
        /// <param name="sbLog"></param>
        /// <param name="syncLog"></param>
        private static void _convertSyncLogToStringBuilder(ref StringBuilder sbLog, string syncLog)
        {
            string[] syncLogElements = syncLog.Split("||");

            for (int i = 0; i < syncLogElements.Length; i++)
            {
                var logEntry = syncLogElements[i];
                if (!string.IsNullOrEmpty(logEntry)) sbLog.AppendLine(syncLogElements[i]);
            }
        }


        /// <summary>
        /// Updates the sync-status of a Structured Data Element in the cache
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task UpdateCachedStructuredDataElementStatus(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var factId = request.RetrievePostedValue("factid", @"^[a-zA-Z0-9_\-\d\s;]{2,512}$", true, reqVars.returnType);
            var sdeSyncStatus = request.RetrievePostedValue("syncstatus", @"^\d{3}-[a-zA-Z_\-\d]{2,30}$", true, reqVars.returnType);

            // Call the service and update the value in the cache
            var dataToPost = new Dictionary<string, string>
            {
                { "pid", projectVars.projectId },
                { "vid", projectVars.versionId },
                { "syncstatus", sdeSyncStatus },
                { "factid", factId },
                { "type", "text" },
                { "did", projectVars.did },
                { "ctype", projectVars.editorContentType },
                { "rtype", projectVars.reportTypeId },
                { "octype", projectVars.outputChannelType },
                { "ocvariantid", projectVars.outputChannelVariantId },
                { "oclang", projectVars.outputChannelVariantLanguage }
            };

            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "structureddataelementcachestate", dataToPost, debugRoutine);
            if (XmlContainsError(xmlResponse)) HandleError(reqVars.returnType, xmlResponse);

            // Write a response
            await response.OK(GenerateSuccessXml("Successfully updated sync status for structured data element", $"xmlResponse: {xmlResponse.OuterXml}", (xmlResponse.SelectSingleNode("//debuginfo")?.InnerText ?? "")), reqVars.returnType, true);
        }

        /// <summary>
        /// Retrieves the value of a single structured data element
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveStructuredDataElementValue(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            // var factId = request.RetrievePostedValue("factid", @"^[a-zA-Z0-9_\-\d\:\%\.;\s\+&#]{2,1024}$", true, reqVars.returnType);
            var factId = request.RetrievePostedValue("factid", @"^.{2,1024}$", true, reqVars.returnType);
            string? valueLang = request.RetrievePostedValue("valuelang", @"^\w{2,3}$", true, reqVars.returnType);
            if (valueLang.ToLower() == "all") valueLang = null;

            // Retrieve the structured data element value
            var structuredDataElementValueResult = await RetieveStructuredDataValue(factId, projectVars.projectId, valueLang, debugRoutine);

            // Render a JSON response
            if (structuredDataElementValueResult.Success)
            {
                await response.OK(structuredDataElementValueResult.ToJson(), ReturnTypeEnum.Json, true);
            }
            else
            {
                await response.Error(structuredDataElementValueResult.ToJson(), ReturnTypeEnum.Json, true);
            }
        }


        /// <summary>
        /// Utility routine to send a message to the client over a websocket
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="message"></param>
        /// <param name="step"></param>
        /// <param name="maxStep"></param>
        /// <returns></returns>
        private static async Task<int> _syncStructuredDataMessageToClient(string userId, string message, int step = -1, int maxStep = -1)
        {
            var messageToClient = new TaxxorReturnMessage(true, (step == -1) ? message : $"Step {step}/{maxStep}: {message}");

            await MessageToOtherClient("SyncStructuredDataProgress", userId, messageToClient);

            if (step > -1) step++;

            return step;
        }

    }

}