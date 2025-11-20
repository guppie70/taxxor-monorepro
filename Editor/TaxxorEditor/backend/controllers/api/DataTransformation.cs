using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// Custom attribute that we can use to decorate data transformation methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class DataTransformationAttribute : Attribute
    {
        public string Id { get; }
        public string Name { get; set; }
        public string Description { get; set; }

        public DataTransformationAttribute(string id)
        {
            Id = id;
        }
    }


    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Returns information about the available bulk transformations by using the DataTransformationList that we have created in the ProjectLogic() constructor
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task BulkTransformListScenarios(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Sort the list of scenario's so that we can easily list these in the client (JavaScript sorting sucks)
            var dataTransformationListSorted = DataTransformationList.OrderBy(o => o.Name);

            // Construct a response message for the client
            dynamic jsonData = new ExpandoObject();
            jsonData.result = new ExpandoObject();
            jsonData.result.scenarios = dataTransformationListSorted;

            string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

            // Render an error response - the content that will be returned to the client depends on the value of reqVars.returnType -> which is dynamically determined using content negotiation
            await context.Response.OK(jsonToReturn, reqVars.returnType, true);
        }


        /// <summary>
        /// Bulk transforms all the data files of the current project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task BulkTransformFilingContentData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var transformMethodId = request.RetrievePostedValue("transformationmethodid", @"^(\w|\d|\-|_){2,100}$", true, reqVars.returnType);
            var transformationScope = request.RetrievePostedValue("scope", @"^(\w|\d){2,512}$", true, reqVars.returnType);

            TaxxorReturnMessage? bulkTransformResult = null;
            switch (transformationScope)
            {
                case "outputchannel":
                    var outputChannelId = request.RetrievePostedValue("outputchannelid", @"^(\w|\d|\-|_,){2,512}$", true, reqVars.returnType);
                    var includeVersionHtmlFiles = request.RetrievePostedValue("includehtmlversionfiles", @"^(true|false)$", true, reqVars.returnType);

                    bulkTransformResult = await BulkTransformFilingContentData(projectVars, outputChannelId, transformMethodId, (includeVersionHtmlFiles == "true"));

                    break;

                case "datareference":
                    var commitChanges = true;
                    var dataReferencesSuccess = new List<string>();
                    var dataReferencesFail = new List<string>();
                    var dataReference = request.RetrievePostedValue("dataref", RegexEnum.FileName, true, reqVars.returnType);
                    var language = request.RetrievePostedValue("lang", @"^(\w|\W){2,12}$", true, reqVars.returnType);

                    var dataReferences = new List<string>();
                    dataReferences.Add(dataReference);

                    var result = await BulkTransformFilingContentData(dataReferences, transformMethodId, projectVars.projectId, language, debugRoutine);

                    dataReferencesSuccess.AddRange(result.Success);
                    dataReferencesFail.AddRange(result.Fail);

                    var successTransformations = dataReferencesSuccess.Count;
                    var failedTransformations = dataReferencesFail.Count;

                    var debugInfo = $"dataReferencesSuccess: {String.Join(",", dataReferencesSuccess)}, dataReferencesFail: {String.Join(",", dataReferencesFail)}";

                    // Attempt to commit the transformed data file
                    if (commitChanges)
                    {
                        bulkTransformResult = await _commitBulkTransformedFiles(projectVars, dataReferences, transformMethodId, null, debugInfo);
                    }
                    else
                    {
                        bulkTransformResult = new TaxxorReturnMessage(true, $"Successfully transformed {dataReference}", debugInfo);
                    }

                    break;

                default:
                    HandleError("Transformation scope not defined", $"transformationScope: {transformationScope}");
                    break;
            }

            //
            // => Render a result
            //
            if (bulkTransformResult.Success)
            {
                //
                // => Clear the paged media cache
                // 
                ContentCache.Clear(projectVars.projectId);

                await response.OK(bulkTransformResult, reqVars.returnType, true);
            }
            else
            {
                await response.Error(bulkTransformResult, reqVars.returnType, true);
            }
        }

        /// <summary>
        /// Bulk transforms all the data files of the current project
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="outputChannelId"></param>
        /// <param name="transformMethodId"></param>
        /// <param name="includeVersionHtmlFiles"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> BulkTransformFilingContentData(ProjectVariables projectVars, string outputChannelId, string transformMethodId, bool includeVersionHtmlFiles, bool generateCommit = true)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var testForLocks = false;

            // Global variables
            var lang = "all";
            var languages = new List<string>();
            var outputChannelIds = new List<string>();
            var dataReferences = new List<string>();
            var cacheReferences = new List<string>();
            var dataReferencesSuccess = new List<string>();
            var dataReferencesFail = new List<string>();
            var cacheReferencesSuccess = new List<string>();
            var cacheReferencesFail = new List<string>();


            // Check if there are any sections locked - if so, then back out
            if (testForLocks)
            {
                var numberOfLocks = FilingLockStore.RetrieveNumberOfLocks(projectVars);
                if (numberOfLocks > 0)
                {
                    HandleError($"Could not proceed because there {((numberOfLocks == 1) ? "is 1 section" : "are " + numberOfLocks + " sections")} locked in this project. Bulk transform is only available when there are no sections locked in a project.", $"stack-trace: {GetStackTrace()}");
                }
            }


            if (!string.IsNullOrEmpty(outputChannelId))
            {
                //
                // => Build a list of content data files to transform
                //

                // - If output channel = "all" then initially try to build a list of file references using the metadata cache logic
                if (outputChannelId == "all")
                {
                    // - Use the cache of the Taxxor Document Store to loop through all the content files
                    dataReferences = await RetrieveDataReferences(projectVars.projectId);
                }

                // If output channel was specified or the result of the step above did not return any file references use the hierarchies to find them
                if (outputChannelId != "all" || (outputChannelId == "all" && dataReferences.Count == 0))
                {
                    // - Use the hierarchy of the output channel that was selected

                    // Compile one document based on all the output channels
                    var xmlOutputChannelHierarchies = await RenderOutputChannelHierarchyOverview();

                    // Convert output channels passed to this routine into a list as there may be multiple selected in the UI
                    if (outputChannelId.Contains(","))
                    {
                        outputChannelIds = outputChannelId.Split(',').ToList();
                    }
                    else
                    {
                        if (outputChannelId == "all")
                        {
                            // Add all the output channel id's
                            foreach (XmlNode nodeOutputChannel in xmlOutputChannelHierarchies.SelectNodes($"/hierarchies/output_channel"))
                            {
                                languages.Add(GetAttribute(nodeOutputChannel, "lang"));
                                outputChannelIds.Add(GetAttribute(nodeOutputChannel, "id"));
                            }
                        }
                        else
                        {
                            outputChannelIds.Add(outputChannelId);
                        }
                    }


                    // Retrieve a unique list of data references to work with
                    var counter = 1;
                    foreach (string currentOutputChannelId in outputChannelIds)
                    {
                        var nodeOutputChannel = xmlOutputChannelHierarchies.SelectSingleNode($"/hierarchies/output_channel[@id='{currentOutputChannelId}']");
                        if (nodeOutputChannel == null) HandleError("Could not locate the output channel", $"outputChannelId: {currentOutputChannelId}, stack-trace: {GetStackTrace()}");
                        languages.Add(GetAttribute(nodeOutputChannel, "lang"));

                        var nodeHierarchyRoot = nodeOutputChannel.SelectSingleNode($"items");
                        if (nodeHierarchyRoot == null) HandleError("Could not locate the hierarchy", $"outputChannelId: {currentOutputChannelId}, stack-trace: {GetStackTrace()}");

                        var nodeListItems = nodeHierarchyRoot.SelectNodes(".//item");

                        foreach (XmlNode nodeItem in nodeListItems)
                        {
                            var referenceId = GetAttribute(nodeItem, "data-ref");
                            var nodeId = GetAttribute(nodeItem, "id");
                            if (string.IsNullOrEmpty(referenceId) || string.IsNullOrEmpty(nodeId))
                            {
                                appLogger.LogWarning($"Could not find data reference or item id for hierarchy item({counter.ToString()}). currentOutputChannelId: {currentOutputChannelId}, stack-trace: {GetStackTrace()}");
                            }
                            else
                            {
                                if (!dataReferences.Contains(referenceId)) dataReferences.Add(referenceId);
                            }
                            counter++;
                        }
                    }


                    // - Determine the language that we need to apply the transformation for
                    if (outputChannelId != "all")
                    {
                        if (languages.Count == 1)
                        {
                            lang = languages[0];
                        }
                        else
                        {
                            // TODO: Should be more granular
                            lang = "all";
                        }
                    }

                }

                //
                // => Bulk transform the content data files
                //
                var result = await BulkTransformFilingContentData(dataReferences, transformMethodId, projectVars.projectId, lang, debugRoutine);
                dataReferencesSuccess.AddRange(result.Success);
                dataReferencesFail.AddRange(result.Fail);
            }
            else
            {
                if (!includeVersionHtmlFiles)
                {
                    return new TaxxorReturnMessage(false, "Nothing to do", "There has been no output channel selection nor did the transformation incude the version HTML files, so there is nothing to do");
                }
            }

            //
            // => Transform version HTML files
            //
            if (includeVersionHtmlFiles)
            {
                // Retrieve all the cache files
                var xmlCacheFiles = await DocumentStoreService.CacheData.ListFiles(true);

                // Filter out all the files that possibly can to transform
                var xpathFilterFiles = $"/files/file[contains(text(), '.html') or contains(text(), '.html')]";
                XmlNodeList? nodeListFiles = xmlCacheFiles.SelectNodes(xpathFilterFiles);

                // Generate a list of cache file paths where we potentially filter out the paths that are not part of the requested output channels (if any)
                foreach (XmlNode nodeFile in nodeListFiles)
                {
                    var filePath = nodeFile.InnerText;
                    if (outputChannelId != "all" && outputChannelIds.Count > 0)
                    {
                        foreach (string currentOutputChannelId in outputChannelIds)
                        {
                            if (filePath.Contains(currentOutputChannelId) && !cacheReferences.Contains(filePath))
                            {
                                cacheReferences.Add(nodeFile.InnerText);
                            }
                        }
                    }
                    else
                    {
                        cacheReferences.Add(nodeFile.InnerText);
                    }
                }
                if (debugRoutine) Console.WriteLine($"- cacheFilePaths: {string.Join(",", cacheReferences)}");

                // Transform the files
                foreach (string cacheFilePath in cacheReferences)
                {
                    var transformationSuccess = false;

                    var xmlFilingContentSourceData = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.Load<XmlDocument>($"/system{cacheFilePath}", "cmscontentroot", true, false);
                    if (XmlContainsError(xmlFilingContentSourceData))
                    {
                        appLogger.LogError($"Could not load cache data. dataReference: {cacheFilePath}, response: {xmlFilingContentSourceData.OuterXml}");
                        dataReferencesFail.Add(cacheFilePath);
                    }
                    else
                    {
                        // Add temporary steering information
                        xmlFilingContentSourceData.DocumentElement.SetAttribute("data-temp-path", $"/system{cacheFilePath}");

                        // Start transformation
                        if (debugRoutine) appLogger.LogInformation($"Transforming cache data with reference: {cacheFilePath}");

                        TaxxorReturnMessage? transformationResult = null;
                        if (DataTransformationMethods.TryGetValue(transformMethodId, out DataTransformationDelegate? method))
                        {
                            // Invoke the transformation method
                            transformationResult = await method(projectVars.projectId, lang, xmlFilingContentSourceData);
                            if (transformationResult.Success)
                            {
                                // Remove steering information
                                transformationResult.XmlPayload.DocumentElement.RemoveAttribute("data-temp-path");

                                // Store the transformed document on the Taxxor Document Store
                                var successSave = await DocumentStoreService.FilingData.Save<bool>(transformationResult.XmlPayload, $"/system{cacheFilePath}", "cmscontentroot", true, false);
                                transformationSuccess = successSave;
                            }
                            else
                            {
                                appLogger.LogError($"There was an error transforming cache {cacheFilePath}, message: {transformationResult.Message}, debuginfo: {transformationResult.DebugInfo}");
                            }
                        }
                        else
                        {
                            appLogger.LogError($"Could not map transformMethodId: {transformMethodId} to a method that we can invoke for transforming the cache file...");
                        }

                        // Add the reference to the list
                        if (transformationSuccess)
                        {
                            cacheReferencesSuccess.Add(cacheFilePath);
                        }
                        else
                        {
                            cacheReferencesFail.Add(cacheFilePath);
                        }
                    }

                }
            }

            //
            // => Commit all the changed content into the versioning system
            //
            var successTransformations = dataReferencesSuccess.Count + cacheReferencesSuccess.Count;
            var failedTransformations = dataReferencesFail.Count + cacheReferencesFail.Count;
            var totalTransformations = successTransformations + failedTransformations;
            var failedTransformationsMessage = (failedTransformations > 0) ? $"Failed transformation for {failedTransformations.ToString()} files" : "";
            var successMessage = $"Successfully bulk transformed {successTransformations} data files. {failedTransformationsMessage}";
            var debugInfo = $"dataReferencesSuccess: {String.Join(",", dataReferencesSuccess)}, dataReferencesFail: {String.Join(",", dataReferencesFail)}, cacheReferencesSuccess: {String.Join(",", cacheReferencesSuccess)}, cacheReferencesFail: {String.Join(",", cacheReferencesFail)}";

            if (successTransformations > 0)
            {
                if (dataReferencesSuccess.Count > 0)
                {
                    if (generateCommit)
                    {
                        var versionCreateResult = await _commitBulkTransformedFiles(projectVars, dataReferencesSuccess, transformMethodId, outputChannelId, debugInfo);

                        if (!versionCreateResult.Success) return versionCreateResult;
                    }

                    //
                    // => Update the local and remote version of the CMS Metadata XML object now that we are fully done with the transformation
                    //
                    await UpdateCmsMetadataRemoteAndLocal(projectVars.projectId);

                    // Render the response (successfull and committed changed data files into the versioning system)
                    return new TaxxorReturnMessage(true, successMessage, debugInfo);
                }
                else
                {
                    // Render the response (successful, but no need to commit changed files)
                    return new TaxxorReturnMessage(true, successMessage, debugInfo);
                }
            }
            else
            {
                // Render the response (none of the transformations succeeded)
                return new TaxxorReturnMessage(false, $"Attempt to bulk transform {totalTransformations} files, but none were successful... {failedTransformationsMessage}", debugInfo);
            }
        }

        /// <summary>
        /// Commits the transformed files in the GIT repository
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="dataReferences"></param>
        /// <param name="transformMethodId"></param>
        /// <param name="outputChannelId"></param>
        /// <param name="debugInfo"></param>
        /// <returns></returns>
        private async static Task<TaxxorReturnMessage> _commitBulkTransformedFiles(ProjectVariables projectVars, List<string> dataReferences, string transformMethodId, string outputChannelId, string debugInfo)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pid", projectVars.projectId);
            dataToPost.Add("vid", projectVars.versionId);
            dataToPost.Add("type", "text");
            dataToPost.Add("did", projectVars.did);
            dataToPost.Add("ctype", projectVars.editorContentType);
            dataToPost.Add("rtype", projectVars.reportTypeId);
            dataToPost.Add("octype", projectVars.outputChannelType);
            dataToPost.Add("ocvariantid", projectVars.outputChannelVariantId);
            dataToPost.Add("oclang", projectVars.outputChannelVariantLanguage);

            // Data for the revision system
            var message = "";
            if (string.IsNullOrEmpty(outputChannelId))
            {
                message = $"Transformed using method: {transformMethodId}";
            }
            else
            {
                message = $"Bulk transform using method: {transformMethodId} on output channels: {outputChannelId}";
            }
            dataToPost.Add("linknames", message);
            dataToPost.Add("sectionids", $"{String.Join(",", dataReferences)}");
            dataToPost.Add("crud", "bulktransform");

            XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "contentversion", dataToPost, debugRoutine);

            if (responseXml.OuterXml.Contains("nothing to commit"))
            {
                return new TaxxorReturnMessage(true, "Transform process completed successfully, but there were no changes made in the content data", debugInfo);
            }
            else
            {
                // Handle error
                if (XmlContainsError(responseXml))
                {
                    return new TaxxorReturnMessage(false, "Could not commit the transformed data files", $"{debugInfo}, responseXml: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // Render the response (successfull and committed changed data files into the versioning system)
                    return new TaxxorReturnMessage(true, "Successfully created bulk transform commit", debugInfo);
                }
            }



        }


        /// <summary>
        /// Bulk transform a list of datafile references for a project using a specific method ID
        /// </summary>
        /// <param name="dataReferences">A list of data-file references</param>
        /// <param name="transformMethodId">A transformation method ID (as defined by the custom method attribute)</param>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<(List<string> Success, List<string> Fail)> BulkTransformFilingContentData(List<string> dataReferences, string transformMethodId, string projectId, string lang, bool debugRoutine)
        {
            var dataReferencesSuccess = new List<string>();
            var dataReferencesFail = new List<string>();

            foreach (var dataReference in dataReferences)
            {
                var transformationSuccess = false;

                // - Store the reference of the current data file in a global variable (dirty, but works for now)
                DataReferenceForBulkTransform = dataReference;

                //
                // => Retrieve data from the Taxxor Document Store
                //                   
                if (debugRoutine) appLogger.LogInformation($"Loading data with reference: '{dataReference}'");
                var xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{dataReference}", "cmsdataroot", true, false);

                if (XmlContainsError(xmlFilingContentSourceData))
                {
                    appLogger.LogError($"Could not load data. dataReference: {dataReference}, response: {xmlFilingContentSourceData.OuterXml}");
                    dataReferencesFail.Add(dataReference);
                }
                else
                {
                    //
                    // => Transform data files
                    //
                    if (debugRoutine) appLogger.LogInformation($"Transforming data with reference: {dataReference}");

                    // Add temporary steering information
                    xmlFilingContentSourceData.DocumentElement.SetAttribute("data-temp-path", $"/textual/{dataReference}");


                    TaxxorReturnMessage? transformationResult = null;
                    if (DataTransformationMethods.TryGetValue(transformMethodId, out DataTransformationDelegate? method))
                    {
                        try
                        {
                            // Invoke the transformation method
                            transformationResult = await method(projectId, lang, xmlFilingContentSourceData);
                            if (transformationResult.Success)
                            {
                                transformationSuccess = true;

                                var successMessage = $"* {transformationResult.Message}{(string.IsNullOrEmpty(transformationResult.DebugInfo) ? "" : " - " + transformationResult.DebugInfo)}";
                                appLogger.LogInformation(successMessage);

                                // Remove steering information
                                transformationResult.XmlPayload.DocumentElement.RemoveAttribute("data-temp-path");

                                // Store the transformed document on the Taxxor Document Store
                                var successSave = await DocumentStoreService.FilingData.Save<bool>(transformationResult.XmlPayload, $"/textual/{dataReference}", "cmsdataroot", true, false);
                                transformationSuccess = successSave;
                            }
                            else
                            {
                                appLogger.LogError($"There was an error transforming {dataReference}, message: {transformationResult.Message}, debuginfo: {transformationResult.DebugInfo}");
                            }
                        }
                        catch (Exception ex)
                        {
                            transformationSuccess = false;
                            appLogger.LogError($"There was an uncaught error transforming {dataReference}, error: {ex}");
                        }

                    }
                    else
                    {
                        appLogger.LogError($"Could not map transformMethodId: {transformMethodId} to a method that we can invoke...");
                    }

                    // Add the reference to the list
                    if (transformationSuccess)
                    {
                        dataReferencesSuccess.Add(dataReference);
                    }
                    else
                    {
                        dataReferencesFail.Add(dataReference);
                    }
                }
            }

            // Reset the global variable
            DataReferenceForBulkTransform = "";

            return (Success: dataReferencesSuccess, Fail: dataReferencesFail);
        }



        /// <summary>
        /// Moves the dates in table cells and in table/graph headers to match the reporting period indicated in the project properties
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("movedatesintables", Name = "Dynamic dates: Shift dates in tables and table headers", Description = "Shifts the dates in table cells and in table/graph headers to match the reporting period indicated in the project properties")]
        public async static Task<TaxxorReturnMessage> MoveDatesInTables(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();


            xmlFilingContentSourceData.LoadXml(MoveDatesInTableCells(projectId, xmlFilingContentSourceData, lang).OuterXml);


            return new TaxxorReturnMessage(true, $"Inspected table and table headers to move dates", xmlFilingContentSourceData, $"");
        }

        /// <summary>
        /// Shifts the dates for Structured Data Elements used in this project so that they match with the chosen reporting period for the current project
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("shiftsdeperiods", Name = "Dynamic dates: Shift SDE periods", Description = "Shifts the periods for Structured Data Elements used in this project based on the table date-headers so that they match with the chosen reporting period for the current project")]
        public async static Task<TaxxorReturnMessage> ShiftPeriodsForStructuredDataElements(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            try
            {
                //
                // => Shift the SDE periods for this datafile using the (dynamic) dates in the table headers
                //
                appLogger.LogInformation($"Start moving SDE periods for {DataReferenceForBulkTransform}");
                var sdeDateShiftResult = await MovePeriodsForStructuredDataElementsUsingTableInformation(projectId, xmlFilingContentSourceData, lang);
                if (!sdeDateShiftResult.Success)
                {
                    return new TaxxorReturnMessage(true, $"Unable to shift the structured data elements periods", xmlFilingContentSourceData, $"");
                }

                var xmlSdePeriodShiftDetails = new XmlDocument();
                xmlSdePeriodShiftDetails.ReplaceContent(sdeDateShiftResult.XmlPayload, true);

                //
                // => Rebuild the cache for the structured data elements used in this file
                //
                appLogger.LogInformation($"Rebuild SDE cache for {DataReferenceForBulkTransform}");
                if (!string.IsNullOrEmpty(DataReferenceForBulkTransform))
                {
                    var dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("projectid", projectId);
                    dataToPost.Add("snapshotid", "1");
                    dataToPost.Add("dataref", DataReferenceForBulkTransform);

                    var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorsyncstructureddatasnapshotperdataref", dataToPost, true);
                    if (XmlContainsError(xmlResponse))
                    {
                        return new TaxxorReturnMessage(false, xmlResponse.SelectSingleNode("//message").InnerText, xmlFilingContentSourceData, xmlResponse.SelectSingleNode("//debuginfo").InnerText);
                    }
                }

                return new TaxxorReturnMessage(true, $"Successfully shifted SDE periods", xmlFilingContentSourceData, $"");
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "Unable to shift the date for the SDE elements", $"error: {ex}");
            }
        }

        /// <summary>
        /// Task that wraps date cells of tables into span nodes to indicate their relative date
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("wrapheadercelldates", Name = "Dynamic dates: Create dynamic date elements for tables", Description = "Loops through the tables and table headers and attempts to find new content to wrap date expressions into <code>&lt;span/&gt;</code> nodes to allow these to be flexible when the project is cloned and a new period is chosen")]
        public async static Task<TaxxorReturnMessage> WrapHeaderCellDates(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            var markDatesResult = MarkDatesInTableCells(projectId, xmlFilingContentSourceData, lang, false);

            // Log some content if needed
            if (markDatesResult.DebugContent.Length > 0)
            {
                char[] trimChars = { ',', ' ' };
                appLogger.LogInformation(Environment.NewLine + markDatesResult.DebugContent.ToString().TrimEnd(trimChars));
            }

            if (markDatesResult.ErrorContent.Length > 0)
            {
                appLogger.LogError("!! Errors detected during period processing !!");
                appLogger.LogError(markDatesResult.ErrorContent.ToString());
            }

            // Replace the content of the Data XML file with the content containing the date marks
            xmlFilingContentSourceData.LoadXml(markDatesResult.XmlData.OuterXml);


            return new TaxxorReturnMessage(true, $"Inspected table cells for date strings", xmlFilingContentSourceData, $"{markDatesResult.ErrorContent.ToString()}");
        }


        /// <summary>
        /// Task that renders new wrappers for period expressions in tables and table headers
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("replaceheadercelldatewrappers", Name = "Dynamic dates: Replace dynamic date elements for tables", Description = "Loops through the tables and table headers. Removes existing dynamic date wrappers and then renders new date expression <code>&lt;span/&gt;</code> wrappers for perriod references into  nodes to allow these to be flexible when the project is cloned and a new period is chosen")]
        public async static Task<TaxxorReturnMessage> ReplaceHeaderCellDateWrappers(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            var markDatesResult = MarkDatesInTableCells(projectId, xmlFilingContentSourceData, lang, true);

            // Log some content if needed
            if (markDatesResult.DebugContent.Length > 0)
            {
                char[] trimChars = { ',', ' ' };
                appLogger.LogInformation(Environment.NewLine + markDatesResult.DebugContent.ToString().TrimEnd(trimChars));
            }

            if (markDatesResult.ErrorContent.Length > 0)
            {
                appLogger.LogError("!! Errors detected during period processing !!");
                appLogger.LogError(markDatesResult.ErrorContent.ToString());
            }

            // Replace the content of the Data XML file with the content containing the date marks
            xmlFilingContentSourceData.LoadXml(markDatesResult.XmlData.OuterXml);


            return new TaxxorReturnMessage(true, $"Inspected table cells for date strings", xmlFilingContentSourceData, $"{markDatesResult.ErrorContent.ToString()}");
        }

        /// <summary>
        /// Removes the whitespace markers that have been set in the document data to optimize the whitespace in the PDF output channel
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("stripwhitespacemarkers", Name = "Cleanup: Strip whitespace markers", Description = "Removes the whitespace markers that have been set in the document data to optimize the whitespace in the PDF output channel")]
        public async static Task<TaxxorReturnMessage> StripWhitespaceMarkers(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {

            var whiteSpaceRemovalRegex = @"\s?(tx-mtop-1|tx-mtop-2|tx-mbot-1|tx-mbot-2|tx-cb-b|tx-pb-b|tx-pb-a|tx-cs-a|tx-flt-t|tx-flt-b|tx-ls-small|tx-ls-big|tx-gh-80|tx-gh-90|tx-gh-110|tx-gh-120|mtop-1|mtop-2|mbot-1|mbot-2|cb-b|pb-b|pb-a|cs-a|flt-t|flt-b)\s?";

            NameValueCollection xsltParameters = new NameValueCollection();
            xsltParameters.Add("lang", lang);
            xsltParameters.Add("regex-class-search", whiteSpaceRemovalRegex);

            // Strip the XHTML from the "whitespace" classes that could have been set by the user
            var xmlTransformedData = await Xslt3TransformXml(xmlFilingContentSourceData, "xsl_whitespace-markup-remover", xsltParameters);

            // Console.WriteLine("************");
            // Console.WriteLine(xmlTransformedData);
            // Console.WriteLine("************");

            if (xmlTransformedData.StartsWith("ERROR"))
            {
                HandleError("There was an error stripping whitespace markers from the content", $"transformation-result: {xmlTransformedData}, projectId: {projectId}, lang: {lang}");
            }

            // Load the passed filing data document with a pretty print version of the transformed data
            xmlFilingContentSourceData.LoadXml(PrettyPrintXml(xmlTransformedData));


            return new TaxxorReturnMessage(true, $"Removed whitespace markers", xmlFilingContentSourceData, $"");
        }


        /// <summary>
        /// Creates a clone of all the structured data elements found in the section(s) and inserts the new fact-id's into the data
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("cloneallsdes", Name = "Utility: Clone and replace all SDE's", Description = "Finds all the structured data elements in the section(s), clones the definition in the Taxxor Structured Data Store and updates the fact-id in the content using the new ID that the Taxxor Structured Data Store returned. This is useful when duplicating sections or if you suspect double fact-id's in the content of a report.")]
        public async static Task<TaxxorReturnMessage> CloneAllStructuredDataElements(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            // Counters and statistics
            var cloneSdeCount = 0;
            var cloneSdeProblems = 0;

            var xmlSectionDataPath = xmlFilingContentSourceData.DocumentElement.GetAttribute("data-temp-path");
            var nodeHeader = RetrieveFirstHeaderNode(xmlFilingContentSourceData);
            var pageTitle = (nodeHeader != null) ? nodeHeader.InnerText : "unknown";

            var langFilter = (lang.ToLower() == "all") ? "" : $"[@lang='{lang}']";

            try
            {
                //
                // => Find the SDE's to change
                //
                var factIds = new List<string>();
                var xPathSdes = $"/data/content{langFilter}//*[@data-fact-id]";
                var nodeListSdes = xmlFilingContentSourceData.SelectNodes(xPathSdes);
                foreach (XmlNode nodeSde in nodeListSdes)
                {
                    // Do not clone mappings if they exist in an external table
                    var nodeExternalTable = nodeSde.SelectSingleNode("ancestor::div[contains(@id, 'tablewrapper_') and contains(@class, 'external-table')]");
                    if (nodeExternalTable != null) continue;

                    var nodeName = nodeSde.LocalName;
                    if (nodeName == "article") continue;

                    var elementClass = nodeSde.GetAttribute("class") ?? "";
                    if (elementClass.Contains("table-wrapper")) continue;

                    // appLogger.LogInformation(nodeSde.OuterXml);
                    var factId = nodeSde.GetAttribute("data-fact-id");

                    // Clone the mapping cluster (this could probably be done in a single call)
                    var xmlCloneResult = await MappingService.CloneMappingCluster(projectId, factId, false);

                    // Process the result
                    if (XmlContainsError(xmlCloneResult))
                    {
                        appLogger.LogError($"Could not clone mapping cluster for factId: {factId}, message: {xmlCloneResult.SelectSingleNode("//message")?.InnerText ?? ""}, debuginfo: {xmlCloneResult.SelectSingleNode("//debuginfo")?.InnerText ?? ""}");
                        cloneSdeProblems++;
                    }
                    else
                    {
                        // Retrieve the new fact ID (Internal mapping ID) that was created
                        var newFactId = xmlCloneResult.SelectSingleNode("/response/item/factId")?.InnerText ?? "";
                        if (string.IsNullOrEmpty(newFactId))
                        {
                            return new TaxxorReturnMessage(false, $"Could not retrieve new factId", xmlFilingContentSourceData, $"xmlCloneResult: {xmlCloneResult.OuterXml}, cloneSdeCount: {cloneSdeCount}");
                        }
                        else
                        {
                            // Set the new fact ID in the content
                            nodeSde.SetAttribute("data-fact-id", newFactId);
                        }
                    }
                }



                //
                // => Save the updated XML document to the server so that we can render a new cache file from it
                //
                var successSave = await DocumentStoreService.FilingData.Save<bool>(xmlFilingContentSourceData, xmlSectionDataPath, "cmsdataroot", true, false);
                if (!successSave) return new TaxxorReturnMessage(false, $"Could not save a temporary copy of the section data file", xmlFilingContentSourceData, $"cloneSdeCount: {cloneSdeCount}, cloneSdeProblems: {cloneSdeProblems}");

                //
                // => Update the SDE cache file
                //
                appLogger.LogInformation("Starting SDE cache creation");
                var dataToPost = new Dictionary<string, string>();
                dataToPost.Add("projectid", projectId);
                dataToPost.Add("snapshotid", "1");
                dataToPost.Add("dataref", Path.GetFileName(xmlSectionDataPath));

                var xmlSyncResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorsyncstructureddatasnapshotperdataref", dataToPost, true);
                if (XmlContainsError(xmlSyncResponse))
                {
                    appLogger.LogError($"Failed to create a new SDE cache entry for {Path.GetFileName(xmlSectionDataPath)}");
                    cloneSdeProblems++;
                }
                else
                {
                    appLogger.LogInformation("SDE cache creation successfully completed");
                }

            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"SDE clone failed. xmlSectionDataPath: {xmlSectionDataPath}");
            }



            return new TaxxorReturnMessage(true, $"Cloned SDE's in page '{pageTitle}'", xmlFilingContentSourceData, $"cloneSdeCount: {cloneSdeCount}, cloneSdeProblems: {cloneSdeProblems}");
        }

        /// <summary>
        /// Converts existing image paths to use dynamic placeholders for project id
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("setdynamicimagepath", Name = "Transform: Set dynamic image paths", Description = "Converts existing image paths to use dynamic placeholders for project id <code>{projectid}</code>")]
        public async static Task<TaxxorReturnMessage> SetDynamicImagePath(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            var replacedPaths = 0;
            var basePath = "/dataserviceassets";


            var nodeListVisuals = xmlFilingContentSourceData.SelectNodes($"//img[starts-with(@src, '{basePath}')]");
            foreach (XmlNode nodeVisual in nodeListVisuals)
            {
                var originalSrc = nodeVisual.GetAttribute("src");
                if (!originalSrc.Contains("{projectid}"))
                {
                    Match regexMatches = ReDataServiceAssetsPathParser.Match(originalSrc);
                    if (regexMatches.Success)
                    {
                        var currentProjectId = regexMatches.Groups[1].Value;
                        var assetPathToRetrieve = regexMatches.Groups[2].Value;
                        nodeVisual.SetAttribute("src", $"{basePath}/{{projectid}}{assetPathToRetrieve}");
                        replacedPaths++;
                    }
                }
            }

            var nodeListDrawings = xmlFilingContentSourceData.SelectNodes($"//object[starts-with(@data, '{basePath}')]");
            foreach (XmlNode nodeDrawing in nodeListDrawings)
            {
                var originalDataSource = nodeDrawing.GetAttribute("data");
                if (!originalDataSource.Contains("{projectid}"))
                {
                    Match regexMatches = ReDataServiceAssetsPathParser.Match(originalDataSource);
                    if (regexMatches.Success)
                    {
                        var currentProjectId = regexMatches.Groups[1].Value;
                        var assetPathToRetrieve = regexMatches.Groups[2].Value;
                        nodeDrawing.SetAttribute("data", $"{basePath}/{{projectid}}{assetPathToRetrieve}");
                        replacedPaths++;
                    }
                }
            }

            var message = "No need to replace image paths";
            if (replacedPaths > 0)
            {
                message = $"Replaced {replacedPaths} image or visual paths";
            }

            return new TaxxorReturnMessage(true, message, xmlFilingContentSourceData, $"dataref: {Path.GetFileName(xmlFilingContentSourceData.DocumentElement.GetAttribute("data-temp-path"))}");
        }

        /// <summary>
        /// Removes all XBRL level 2 block tags from the data source
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("removeblocktagging", Name = "Transform: Remove XBRL block tags", Description = "Removes all XBRL level 2 block tags from the data source")]
        public async static Task<TaxxorReturnMessage> RemoveBlockLevel2Tags(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            var nodeListBlockLevelElements = xmlFilingContentSourceData.SelectNodes($"//*[@data-block-id]");
            foreach (XmlNode nodeBlockElement in nodeListBlockLevelElements)
            {
                nodeBlockElement.RemoveAttribute("data-block-id");
            }

            var message = "No need to remove XBRL block tags";
            if (nodeListBlockLevelElements.Count > 0)
            {
                message = $"Removed {nodeListBlockLevelElements.Count} XBRL level 2 block tags";
            }

            return new TaxxorReturnMessage(true, message, xmlFilingContentSourceData, $"dataref: {Path.GetFileName(xmlFilingContentSourceData.DocumentElement.GetAttribute("data-temp-path"))}");
        }

        /// <summary>
        /// Transformation routine to harmonize all link signatures to that they contain a valid data-link-type attribute
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("harmonizelinksignature", Name = "Utility: Harmonize links", Description = "Routine that will attempt to apply the correct type of link (<code>data-link-type</code>) so that links can be processed correctly. Link categories are 'section', 'external', 'footnote', 'note' and 'email'.")]
        public async static Task<TaxxorReturnMessage> HarmonizeLinkSignature(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            // Load the metadata file to find the target article type


            var xPath = "//a";
            var nodeListLinks = xmlFilingContentSourceData.SelectNodes(xPath);
            foreach (XmlNode nodeLink in nodeListLinks)
            {
                var linkTarget = GetAttribute(nodeLink, "href");
                var type = GetAttribute(nodeLink, "type");
                if (!string.IsNullOrEmpty(type))
                {
                    // This is probably a footnote link "old style"
                    switch (type)
                    {
                        case "footnote":
                            RemoveAttribute(nodeLink, "type");
                            SetAttribute(nodeLink, "data-link-type", "footnote");
                            break;

                        default:
                            appLogger.LogWarning($"Could not process a/@type='{type}'");
                            break;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(linkTarget))
                    {
                        // Inject a reference that is always broken
                        nodeLink.SetAttribute("href", "#lorem-ipsum-broken");
                        SetAttribute(nodeLink, "data-link-type", "section");
                    }
                    else if (linkTarget.StartsWith("http") || linkTarget.StartsWith("www"))
                    {
                        SetAttribute(nodeLink, "data-link-type", "external");
                    }
                    else if (linkTarget.StartsWith("mailto:"))
                    {
                        SetAttribute(nodeLink, "data-link-type", "email");
                    }
                    else if (nodeLink.HasAttribute("data-noteid"))
                    {
                        SetAttribute(nodeLink, "data-link-type", "note");
                    }
                    else if (RegExpTest(@"^#\d+\-([a-z])+([a-z0-9\-])+$", linkTarget))
                    {
                        SetAttribute(nodeLink, "data-link-type", "section");
                    }
                    else if (linkTarget.StartsWith("#footnote"))
                    {
                        SetAttribute(nodeLink, "data-link-type", "footnote");
                    }
                    else
                    {
                        // Footnote links without the proper data-link-type attribute
                        var wrapperElement = nodeLink.ParentNode;
                        var wrapperElementClass = wrapperElement.GetAttribute("class") ?? "";
                        if (wrapperElementClass == "fn" || wrapperElementClass.StartsWith("fn "))
                        {
                            SetAttribute(nodeLink, "data-link-type", "footnote");
                        }

                    }

                }

                // TODO: attempt to find and correctly mark links to notes @data-link-type="note"


            }


            return new TaxxorReturnMessage(true, $"Successfully transformed", xmlFilingContentSourceData, $"xPath: {xPath}");
        }

        /// <summary>
        /// Marks none-unique Structured Data Elements with an attribute so that we can higlight these in the UI
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("marknoneuniquesdes", Name = "Utility: Mark non-unique SDE's", Description = "Finds Structured Data Elements in the content that have a fact-id which is used elsewhere as well")]
        public async static Task<TaxxorReturnMessage> MarkNonUniqueFactIDs(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            // FactID selector retrieved from data analysis routine
            var xPathSelector = "[@data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_GrossCarryingAmountMember' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_GrossCarryingAmountMember' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_AccumulatedDepreciationAmortisationAndImpairmentMember' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_AccumulatedDepreciationAmortisationAndImpairmentMember' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_PropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_PropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_AdditionsOtherThanThroughBusinessCombinationsPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_AdditionsOtherThanThroughBusinessCombinationsPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_AssetsAvailableForUse' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_AssetsAvailableForUse' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_AcquisitionsThroughBusinessCombinationsPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_AcquisitionsThroughBusinessCombinationsPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_DepreciationPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_DepreciationPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_ImpairmentLossRecognisedInProfitOrLossPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_ImpairmentLossRecognisedInProfitOrLossPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_IncreaseDecreaseThroughNetExchangeDifferencesPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_IncreaseDecreaseThroughNetExchangeDifferencesPropertyPlantAndEquipment' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_TotalChanges' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_TotalChanges' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_GrossCarryingAmountMember__1' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_GrossCarryingAmountMember__1' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_AccumulatedDepreciationAmortisationAndImpairmentMember__1' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_AccumulatedDepreciationAmortisationAndImpairmentMember__1' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_PropertyPlantAndEquipment__1' or @data-fact-id='PPE_CY_tableid__Cell_ifrs_full_MachineryMember_ifrs_full_PropertyPlantAndEquipment__1' or @data-fact-id='Plan_assets_allocation_tableid__Cell_3_ifrs_full_EquityInstrumentsAmountContributedToFairValueOfPlanAssets' or @data-fact-id='b0eb1bd7-f92e-4dcd-a3ef-3e9b78429bfa' or @data-fact-id='cc21d99b-39c5-41ae-9ff5-cb2b776f8210' or @data-fact-id='87cd19a1-9d4b-49f7-a22e-fe1da8fde179' or @data-fact-id='09b3d267-581a-47db-97c2-263833a1bec3' or @data-fact-id='Credit_risk_tableid__Cell_3_ARatedBankCounterparties' or @data-fact-id='Credit_risk_tableid__Cell_3_ARatedBankCounterparties__1' or @data-fact-id='Credit_risk_tableid__Cell_3_ARatedBankCounterparties__2' or @data-fact-id='id__Cell_2_' or @data-fact-id='id__Cell_3_' or @data-fact-id='id__Cell_3_LongtermDebt']";
            var xPathNoneUniqueFactIds = $"//*{xPathSelector}";
            var nodeListNoneUniqueStructuredDataElements = xmlFilingContentSourceData.SelectNodes(xPathNoneUniqueFactIds);
            var noneUniqueStructuredDataElementsFound = (nodeListNoneUniqueStructuredDataElements.Count > 0);
            foreach (XmlNode nodeStructuredDataElement in nodeListNoneUniqueStructuredDataElements)
            {
                nodeStructuredDataElement.SetAttribute("data-none-unique", "true");
            }

            return new TaxxorReturnMessage(true, $"Finished finding none-unique SDE's - noneUniqueStructuredDataElementsFound: {noneUniqueStructuredDataElementsFound.ToString().ToLower()}", xmlFilingContentSourceData, $"");
        }


        /// <summary>
        /// Removes non-unique fact ID markers in the content
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("unmarknoneuniquesdes", Name = "Utility: Remove non-unique SDE markers", Description = "Removes non-unique fact ID markers in the content")]
        public async static Task<TaxxorReturnMessage> RemoveNonUniqueFactIdMarkers(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            var xPathNoneUniqueFactIds = $"//*[@data-none-unique]";
            var nodeListNoneUniqueStructuredDataElements = xmlFilingContentSourceData.SelectNodes(xPathNoneUniqueFactIds);
            var noneUniqueStructuredDataElementsFound = (nodeListNoneUniqueStructuredDataElements.Count > 0);
            foreach (XmlNode nodeStructuredDataElement in nodeListNoneUniqueStructuredDataElements)
            {
                RemoveAttribute(nodeStructuredDataElement, "data-none-unique");
            }

            return new TaxxorReturnMessage(true, $"Removing none-unique SDE markers - noneUniqueStructuredDataElementsFound: {noneUniqueStructuredDataElementsFound.ToString().ToLower()}", xmlFilingContentSourceData, $"");
        }


        /// <summary>
        /// Loops through the data files and changes the reference that maps the data file to a reporting requirement ID such as 'ESMA' or 'PHG2019'
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("updatereportingrequirementsref", Name = "Transform: Update reporting requirement reference", Description = "Loops through the data files and changes the reference that maps the data file to a reporting requirement ID such as 'ESMA' or 'PHG2019'. Now configured to search for 'PHG2017' and replace by '2019'.")]
        public async static Task<TaxxorReturnMessage> UpdateReportingRequirementsReference(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            var found = 0;

            var langFilter = (lang.ToLower() == "all") ? "" : $"[@lang='{lang}']";

            try
            {
                var nodeListContent = xmlFilingContentSourceData.SelectNodes($"/data/content{langFilter}/*");
                foreach (XmlNode nodeContentRoot in nodeListContent)
                {
                    var reportingRequirement = nodeContentRoot.GetAttribute("data-reportingrequirements") ?? "";
                    if (reportingRequirement != "")
                    {
                        var newReportingRequirementAttributeValue = reportingRequirement;
                        // if (reportingRequirement.Contains("PHG2019"))
                        // {
                        //     found++;

                        //     newReportingRequirementAttributeValue = newReportingRequirementAttributeValue.Replace("PHG2019", "SEC");
                        // }

                        if (reportingRequirement.Contains("PHG2017"))
                        {
                            found++;

                            newReportingRequirementAttributeValue = newReportingRequirementAttributeValue.Replace("PHG2017", "PHG2019");
                        }

                        nodeContentRoot.SetAttribute("data-reportingrequirements", newReportingRequirementAttributeValue);
                    }
                }
            }
            catch (Exception ex)
            {
                new TaxxorReturnMessage(true, $"There was an error replacing the reporting requirements value", xmlFilingContentSourceData, $"error: {ex}");
            }



            return new TaxxorReturnMessage(true, $"Successfully changed {found} reporting requirement attributes", xmlFilingContentSourceData, $"");
        }


        /// <summary>
        /// Loop through the filing content data files and make sure that for all the different languages there is (minimum) content available
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <returns></returns>
        [DataTransformation("addremovelanguages", Name = "Utility: Add/remove content languages", Description = "Loop through the filing content data files and make sure that for all the different languages there is (minimum) content available")]
        public async static Task<TaxxorReturnMessage> AddRemoveContentLanguages(string projectId, string lang, XmlDocument xmlFilingContentSourceData)
        {
            await DummyAwaiter();

            var editorId = RetrieveEditorIdFromProjectId(projectId);




            // Current local time with offset
            var createTimeStamp = createIsoTimestamp();

            // Retrieve the basic structure for a section in the document
            var filingDataTemplate = new XmlDocument();
            switch (editorId)
            {
                case "xyz":
                    filingDataTemplate = RetrieveTemplate("inline-editor-content");
                    break;
                default:
                    filingDataTemplate = RetrieveTemplate("inline-editor-content");
                    break;
            }

            // Fill the template with the data that we have received
            var nodeDateCreated = filingDataTemplate.SelectSingleNode("/data/system/date_created");
            var nodeDateModified = filingDataTemplate.SelectSingleNode("/data/system/date_modified");

            // Find the original content node
            var nodeContentOriginal = filingDataTemplate.SelectSingleNode("/data/content");


            //
            // => Retrieve the project languages for this project
            //
            var languages = RetrieveProjectLanguages(editorId);


            //
            // => Add potentially new languages
            //
            var languagesAdded = 0;
            foreach (var language in languages)
            {
                var nodeLangContent = xmlFilingContentSourceData.SelectSingleNode($"//content[@lang='{language}']");
                if (nodeLangContent != null)
                {
                    // Language exists, so we mark the node
                    nodeLangContent.SetAttribute("processed", "true");
                }
                else
                {
                    var nodeArticleExisting = xmlFilingContentSourceData.SelectSingleNode($"//content/article");
                    var sectionId = nodeArticleExisting?.GetAttribute("id") ?? "unknown";

                    var nodeLangContentnew = nodeContentOriginal.CloneNode(true);
                    var nodeContentArticle = nodeLangContentnew.FirstChild;


                    nodeDateCreated.InnerText = createTimeStamp;
                    nodeDateModified.InnerText = createTimeStamp;
                    SetAttribute(nodeLangContentnew, "lang", language);
                    // Temporary ID is set - in the project datastore we will test if the ID is unique and if it may need to be replaced by something else
                    SetAttribute(nodeContentArticle, "id", sectionId);
                    SetAttribute(nodeContentArticle, "data-guid", sectionId);
                    SetAttribute(nodeContentArticle, "data-last-modified", createTimeStamp);

                    // nodeContentArticle.SelectSingleNode("")?.InnerText = "bla";

                    var nodeHeader = nodeContentArticle.SelectSingleNode("*//h1");
                    if (nodeHeader != null)
                    {
                        nodeHeader.InnerText = "Dummy section title";
                    }

                    // Set the processed attribute
                    nodeContentOriginal.SetAttribute("processed", "true");

                    // Append the new language to the filing content data
                    xmlFilingContentSourceData.DocumentElement.AppendChild(xmlFilingContentSourceData.ImportNode(nodeLangContentnew, true));

                    languagesAdded++;
                }


            }

            var languagesRemoved = 0;
            //
            // => Remove languages which are not relevant anymore
            //
            var nodeListUnprocessed = xmlFilingContentSourceData.SelectNodes("//content[not(@processed)]");
            languagesRemoved = nodeListUnprocessed.Count;
            if (nodeListUnprocessed.Count > 0) RemoveXmlNodes(nodeListUnprocessed);

            //
            // => Remove the processed system node
            //
            var nodeListProcessed = xmlFilingContentSourceData.SelectNodes("//content[@processed]");
            foreach (XmlNode node in nodeListProcessed)
            {
                node.RemoveAttribute("processed");
            }


            //
            // => Construct return message
            //
            var message = "No need to add / remove lamguages";
            if (languagesAdded > 0 || languagesRemoved > 0) message = $"Added {languagesAdded} language nodes and removed {languagesRemoved} language nodes";
            return new TaxxorReturnMessage(true, message, xmlFilingContentSourceData, $"dataref: {Path.GetFileName(xmlFilingContentSourceData.DocumentElement.GetAttribute("data-temp-path"))}");
        }





    }

}