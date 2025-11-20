using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Auditor view websocket methods
        /// </summary>
        public partial class WebSocketsHub : Hub
        {

            /// <summary>
            /// Synchronizes structured data elements of a snapshot in all the project data files
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="importErpData"></param>
            /// <param name="forceFullErpImport"></param>
            /// <param name="snapshotId"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/syncstructureddatasnapshot")]
            public async Task<TaxxorReturnMessage> SyncStructuredData(string projectId, bool importErpData, bool forceFullErpImport, string snapshotId)
            {

                // Centralized error messsage
                var errorMessage = "There was an error synchronizing structured data";

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                try
                {
                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }
                    else
                    {
                        if (SystemState.ErpImport.IsRunning || SystemState.SdsSync.IsRunning)
                        {
                            var returnMessage = new TaxxorReturnMessage(false, "There is already an ERP import or SDS data sync in progress. Please wait until these have been completed and try again later", SystemState.ToString());
                            appLogger.LogError(returnMessage.ToString());
                            return returnMessage;
                        }
                        else
                        {

                            // Validate the data
                            var inputValidationCollection = new InputValidationCollection();
                            inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                            inputValidationCollection.Add("snapshotId", snapshotId, @"^[a-zA-Z0-9\-_]{1,128}$", true);
                            var validationResult = inputValidationCollection.Validate();
                            if (!validationResult.Success)
                            {
                                appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                                return validationResult;
                            }
                            else
                            {
                                // Fill the request and project variables in the context with the posted values
                                var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
                                if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                                //
                                // => Stylesheet cache
                                //
                                SetupPdfStylesheetCache(reqVars);

                                // Retrieve the project name
                                var projectName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/name")?.InnerText ?? "unknown";

                                //
                                // => Optionally start the ERP data import process
                                //

                                if (importErpData)
                                {

                                    // Create the ERP import background task
                                    var erpImportSettings = new ErpImportSettings(projectVars)
                                    {
                                        PartOfSdsSync = true,
                                        ForceFullImport = forceFullErpImport,
                                        AddCooldownPeriod = true
                                    };
                                    var erpRunDetails = new ErpImportRunDetails();
                                    ErpImportStatus.TryAdd(erpImportSettings.RunId, erpRunDetails);

                                    // Add a log line to the erp import status
                                    await ErpImportStatus[erpImportSettings.RunId].AddLog($"[<span data-epochstart=\"{erpRunDetails.EpochStartTime}\">{GenerateLogTimestamp(false)}</span>]\nPreparing import EFR data task");

                                    // Schedule the task so that is will be picked up by the scheduler
                                    var importTask = TaskSettings.Create(erpImportSettings);
                                    _tasksToRun.Enqueue(importTask);
                                }

                                //
                                // => Synchronize the SDS data in the project
                                //


                                // Create the background task and queue it
                                var sdsSyncSettings = new SdsSyncSettings(reqVars, projectVars)
                                {
                                    CombinedWithErpImport = importErpData
                                };
                                var sdsSyncRunDetails = new SdsSyncRunDetails();
                                SdsSynchronizationStatus.TryAdd(sdsSyncSettings.RunId, sdsSyncRunDetails);

                                // Add a log line to the erp import status
                                await SdsSynchronizationStatus[sdsSyncSettings.RunId].AddLog($"[<span data-epochstart=\"{sdsSyncRunDetails.EpochStartTime}\">{GenerateLogTimestamp(false)}</span>]\nPreparing import structured data sync task");
                                var task = TaskSettings.Create(sdsSyncSettings);

                                // Schedule the task so that is will be picked up by the scheduler
                                _tasksToRun.Enqueue(task);

                                // Send the result of the synchronization process back to the server
                                return new TaxxorReturnMessage(true, $"Started the structured data synchronization\n  * project name: '{projectName}'", ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"\n  * projectId: {projectId}, snapshotId: {snapshotId}" : "")); ;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await SystemState.SdsSync.Reset(true);

                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : ""));
                }
            }



            /// <summary>
            /// Synchronizes structured data elements of a snapshot in one or more project data files
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/syncstructureddatabyreference")]
            public async Task<TaxxorReturnMessage> SyncStructuredDataByProjectReference(string jsonData)
            {
                var errorMessage = "There was an error synchronizing the structured data elements in your content";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var testForLocks = false;
                    var generateVersions = (siteType != "local");
                    // generateVersions = true;

                    // Handle security
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }
                    else
                    {
                        // Retrieve the posted data by converting JSON to XML
                        var xmlDataPosted = ConvertJsonToXml(jsonData, "data");
                        // Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                        // Extract the posted data
                        var token = _extractPostedXmlValue(xmlDataPosted, "token");
                        var projectId = _extractPostedXmlValue(xmlDataPosted, "pid");
                        var snapshotId = _extractPostedXmlValue(xmlDataPosted, "snapshotid");
                        var sectionId = _extractPostedXmlValue(xmlDataPosted, "sectionid");
                        var includeChildren = _extractPostedXmlValue(xmlDataPosted, "includechildren");

                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add(token, @"^[a-zA-Z0-9]{10,40}$", true);
                        inputValidationCollection.Add(projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        inputValidationCollection.Add(snapshotId, @"^(\w|\-|\d){1,32}$", true);
                        inputValidationCollection.Add(sectionId, RegexEnum.Default, false);
                        inputValidationCollection.Add(includeChildren, @"^(true|false)$", true);

                        var validationResult = inputValidationCollection.Validate();
                        if (!validationResult.Success)
                        {
                            appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                            return validationResult;
                        }
                        else
                        {
                            var baseDebugString = $"projectId: {projectId.Value}, snapshotId: {snapshotId.Value}, sectionId: {sectionId.Value}, includeChildren: {includeChildren.Value}";

                            // Fill the request and project variables in the context with the posted values
                            var renderProjectVarsResult = await ProjectVariablesFromXml(xmlDataPosted, true);
                            if (!renderProjectVarsResult) throw new Exception($"Unable to retrieve project variables. {baseDebugString}");

                            //
                            // => Retrieve a list of data references that we need to use for the synchronization
                            //
                            var dataReferencesToSync = new List<string>();

                            // - Load the hierarchy
                            var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                            // Console.WriteLine($"outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey}");

                            XmlDocument? xmlHierarchyDoc = projectVars.rbacCache.GetHierarchy(outputChannelHierarchyMetadataKey, "full");

                            if (xmlHierarchyDoc == null)
                            {
                                xmlHierarchyDoc = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadHierarchy(projectVars);
                                if (XmlContainsError(xmlHierarchyDoc))
                                {
                                    xmlHierarchyDoc = null;
                                    throw new Exception("Could not retrieve hierarchy");
                                }
                            }
                            // Console.WriteLine("-------------------");
                            // Console.WriteLine(xmlHierarchyDoc.OuterXml);
                            // Console.WriteLine("-------------------");                            

                            // - Find the data references
                            var nodeCurrentItem = xmlHierarchyDoc.SelectSingleNode($"//item[@id='{sectionId.Value}']");
                            if (nodeCurrentItem == null)
                            {
                                throw new Exception($"Could not locate this item in the hierarchy. {baseDebugString}");
                            }

                            var dataReference = nodeCurrentItem.GetAttribute("data-ref");
                            if (string.IsNullOrEmpty(dataReference))
                            {
                                throw new Exception($"Could not find a datareference for item '{sectionId.Value}'. {baseDebugString}");
                            }
                            dataReferencesToSync.Add(dataReference);

                            if (includeChildren.Value == "true")
                            {
                                var nodeListChildItems = nodeCurrentItem.SelectNodes($"sub_items//item");
                                foreach (XmlNode nodeItem in nodeListChildItems)
                                {
                                    dataReference = nodeItem.GetAttribute("data-ref");
                                    if (!string.IsNullOrEmpty(dataReference))
                                    {
                                        dataReferencesToSync.Add(dataReference);
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Data reference could not be located in {nodeItem.OuterXml}");
                                    }
                                }


                            }

                            baseDebugString += $", dataReferencesToSync: {string.Join(",", dataReferencesToSync.ToArray())}";

                            if (includeChildren.Value == "false") generateVersions = false;

                            // var errorResult = new TaxxorReturnMessage(false, "Thrown on purpose", baseDebugString);
                            // await Clients.Caller.SendAsync("SyncStructuredDataDone", errorResult.ToJson());

                            // Start syncing the structured data elements
                            var syncStructuredDataElementsResult = await SyncStructuredDataElementsPerDataReference(projectId.Value, snapshotId.Value, true, generateVersions, testForLocks, true, dataReferencesToSync, reqVars, projectVars);

                            if (!syncStructuredDataElementsResult.Success)
                            {
                                appLogger.LogError($"{syncStructuredDataElementsResult.ToString()}");
                                return syncStructuredDataElementsResult;
                            }
                            else
                            {
                                //
                                // => Generate graph renditions
                                //
                                var graphRenditionsResult = await GenerateGraphRenditions(reqVars, projectVars, dataReferencesToSync);
                                if (!graphRenditionsResult.Success)
                                {
                                    appLogger.LogError($"Failed rendering graph renditions. project id: {projectVars.projectId}, {graphRenditionsResult.LogToString()}");
                                    await _syncStructuredDataMessageToClient(projectVars.currentUser.Id, "ERROR: Failed to render graph renditions");
                                }

                                // Construct a response message for the client
                                var responseMessage = $"Successfully synchronized structured data elements. Please check the Auditor View for details";
                                if (!string.IsNullOrEmpty(syncStructuredDataElementsResult.Payload))
                                {
                                    responseMessage = $"<pre>{syncStructuredDataElementsResult.Payload}</pre>";
                                }
                                dynamic jsonResponseData = new ExpandoObject();
                                jsonResponseData.result = new ExpandoObject();
                                jsonResponseData.result.message = responseMessage;
                                jsonResponseData.result.debuginfo = syncStructuredDataElementsResult.DebugInfo;

                                // Convert to JSON and return it to the client
                                string jsonToReturn = JsonConvert.SerializeObject(jsonResponseData, Newtonsoft.Json.Formatting.Indented);

                                var message = new TaxxorReturnMessage(true, "Successfully synchronized structured data elements", jsonToReturn,"");

                                return message;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


        }

    }

}