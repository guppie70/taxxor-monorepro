using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Logic for dealing with Structured Data elements used in the source data of a project
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Updates the sync-status of a Structured Data Element in the cache
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task UpdateCachedStructuredDataElementStatus(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = siteType == "local" || siteType == "dev";

            // Get posted data
            var factId = request.RetrievePostedValue("factid", RegexEnum.None, true, reqVars.returnType);
            var sdeSyncStatus = context.Request.RetrievePostedValue("syncstatus", RegexEnum.None, true, reqVars.returnType);

            var baseDebugString = $"factId: {factId}, sdeSyncStatus: {sdeSyncStatus}";
            try
            {
                // Calculate the base path to the xml file that contains the section XML file
                var xmlSectionPathOs = RetrieveInlineFilingComposerXmlPathOs(reqVars, projectVars, projectVars.did, debugRoutine);

                // Calculate the path to the cache file
                var cacheStructuredDataFilePathOs = RetrieveContentDataFolderPathOs(projectVars.projectId) + "/" + _generateStructuredDataElementsCacheFilename(xmlSectionPathOs);
                baseDebugString += $", xmlSectionPathOs: {xmlSectionPathOs}, cacheStructuredDataFilePathOs: {cacheStructuredDataFilePathOs}";

                if (!File.Exists(cacheStructuredDataFilePathOs))
                {
                    HandleError(ReturnTypeEnum.Xml, "Could not load structured data cache file", baseDebugString);
                }

                // Adjust the status
                var xmlStructuredDataCache = new XmlDocument();
                xmlStructuredDataCache.Load(cacheStructuredDataFilePathOs);
                var xPath = $"/elements/element[@id={GenerateEscapedXPathString(factId)}]";
                var nodeListCacheStructuredDataElements = xmlStructuredDataCache.SelectNodes(xPath);

                if (nodeListCacheStructuredDataElements.Count == 0)
                {
                    HandleError(ReturnTypeEnum.Xml, "Could not find cached structured data element", $"xPath: {xPath}, {baseDebugString}");
                }
                else
                {
                    foreach (XmlNode nodeCachedStructuredDataElement in nodeListCacheStructuredDataElements)
                    {
                        nodeCachedStructuredDataElement.SetAttribute("status", sdeSyncStatus);
                    }
                }

                // Save the file
                xmlStructuredDataCache.Save(cacheStructuredDataFilePathOs);

                // Write a response
                await response.OK(GenerateSuccessXml("Successfully updated the sync status of the structured data element", baseDebugString), ReturnTypeEnum.Xml, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Xml, "There was an error updating the Structured Data Cache sync status", $"error: {ex}, {baseDebugString}");
            }
        }


        /// <summary>
        /// Syncs structured data elements per data file (used in API route)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task SyncStructuredDataElementsPerDataReference(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var projectId = request.RetrievePostedValue("projectid", RegexEnum.None, true, reqVars.returnType);
            var snapshotId = request.RetrievePostedValue("snapshotid", RegexEnum.None, true, reqVars.returnType);
            var dataReference = request.RetrievePostedValue("dataref", RegexEnum.None, true, reqVars.returnType);

            var syncResult = await SyncStructuredDataElementsPerDataReference(projectId, dataReference);

            if (syncResult.Success)
            {
                await response.OK(syncResult, ReturnTypeEnum.Xml);
            }
            else
            {
                await response.Error(syncResult, ReturnTypeEnum.Xml);
            }
        }

        /// <summary>
        /// Syncs structured data elements per data file
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="dataReference"></param>
        /// <returns></returns> <summary>
        public async static Task<TaxxorReturnMessage> SyncStructuredDataElementsPerDataReference(string projectId, string dataReference)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Store the posted projectId in the context, so we can retrieve it from other routines as well 
            context?.Items?.Add("projectid", projectId);

            // Total number of SDE found
            var structuredDataElementsFound = 0; // All the structured data elements found in the document

            StringBuilder sbDebug = new StringBuilder();

            try
            {

                // Calculate the path to the data file
                var xmlSectionFolderPathOs = RetrieveContentDataFolderPathOs(projectId);
                sbDebug.AppendLine($"- xmlSectionFolderPathOs: {xmlSectionFolderPathOs}");

                List<string> contentXmlPaths = new List<string>();
                contentXmlPaths.Add($"{xmlSectionFolderPathOs}/{dataReference}");

                //
                // => Loop through all the data files for this project and update all the structured data elements
                //    
                var resultRenderUniqueStructuredDataElementList = _renderUniqueStructuredDataElementList(projectId, contentXmlPaths, ref structuredDataElementsFound);
                if (!resultRenderUniqueStructuredDataElementList.Success)
                {
                    HandleError(ReturnTypeEnum.Xml, resultRenderUniqueStructuredDataElementList);
                }

                // Define the unique elements list
                var xmlStructuredDataElements = new XmlDocument();
                xmlStructuredDataElements.LoadXml(resultRenderUniqueStructuredDataElementList.XmlPayload.OuterXml);

                if (debugRoutine)
                {
                    Console.WriteLine("");
                    Console.WriteLine("--------");
                    Console.WriteLine("xmlStructuredDataElements:");
                    Console.WriteLine(PrettyPrintXml(xmlStructuredDataElements));
                    Console.WriteLine("--------");
                    Console.WriteLine("");
                }

                // HandleError("Thrown on purpose");

                //
                // => Refresh all the structured data elements by rebuilding the cache
                //
                var syncStructuredDataResult = await _syncStructuredDataElements(projectId, xmlStructuredDataElements);
                if (!syncStructuredDataResult.Success)
                {
                    return new TaxxorReturnMessage(false, syncStructuredDataResult.Message, syncStructuredDataResult.DebugInfo);
                }
                else
                {
                    // Add the total number of SDE
                    syncStructuredDataResult.StructuredDataElementsFound = structuredDataElementsFound;

                    //
                    // => Serialize the sync object into XML so we can return that to the Taxxor Editor
                    //
                    var serializer = new DataContractSerializer(typeof(SdeSyncStatictics));
                    string xmlString;
                    using (var sw = new StringWriter())
                    {
                        using var writer = new XmlTextWriter(sw);
                        writer.Formatting = Formatting.Indented; // indent the Xml so it's human readable
                        serializer.WriteObject(writer, syncStructuredDataResult);
                        writer.Flush();
                        xmlString = sw.ToString();
                    }


                    //
                    // => Render the response
                    //
                    var xmlSyncResultSerialized = new XmlDocument();
                    xmlSyncResultSerialized.LoadXml(xmlString);

                    return new TaxxorReturnMessage(true, $"Successfully synchronized SDE's for '{dataReference}'", xmlSyncResultSerialized, sbDebug.ToString());
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"There was an error syncing structured data elements for project with ID: {projectId}";
                appLogger.LogError(ex, errorMessage);

                return new TaxxorReturnMessage(false, errorMessage, $"error: {ex.ToString()}, debugInfo: {sbDebug.ToString()}");
            }


        }

        /// <summary>
        /// Removes the Structured Data Cache files for a project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task SyncStructuredDataElementsRemoveCache(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var projectId = request.RetrievePostedValue("projectid", RegexEnum.None, true, reqVars.returnType);
            var scope = request.RetrievePostedValue("scope", RegexEnum.None, true, reqVars.returnType);
            var dataReferencesString = request.RetrievePostedValue("datarefs", RegexEnum.None, false, reqVars.returnType);

            // Remove the structured data cache files
            TaxxorReturnMessage? deleteCacheResult = null;
            TaxxorReturnMessage? backupCacheResult = null;

            if (scope == "all")
            {
                backupCacheResult = _backupCachedStructuredDataFiles(projectId);

                deleteCacheResult = _removeCachedStructuredDataFiles(projectId);
            }
            else
            {
                var dataReferences = new List<string>();
                if (dataReferencesString.Contains(","))
                {
                    dataReferences.AddRange(dataReferencesString.Split(','));
                }
                else
                {
                    dataReferences.Add(dataReferencesString);
                }

                backupCacheResult = _backupCachedStructuredDataFiles(projectId, dataReferences);

                deleteCacheResult = _removeCachedStructuredDataFiles(projectId, dataReferences);
            }

            if (!backupCacheResult.Success)
            {
                HandleError(ReturnTypeEnum.Xml, backupCacheResult.Message, backupCacheResult.DebugInfo);
            }


            // Render the result
            if (deleteCacheResult.Success)
            {
                await response.OK(GenerateSuccessXml(deleteCacheResult.Message, deleteCacheResult.DebugInfo), ReturnTypeEnum.Xml, true);
            }
            else
            {
                HandleError(ReturnTypeEnum.Xml, deleteCacheResult.Message, deleteCacheResult.DebugInfo);
            }
        }


        /// <summary>
        /// Creates a SDE sync report
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task SyncStructuredDataElementsCreateLog(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var projectId = request.RetrievePostedValue("projectid", RegexEnum.None, true, reqVars.returnType);

            // Generate the report
            var renderSyncLogResult = _syncStructuredDataElementsCreateLog(projectId);

            // Render the result
            if (renderSyncLogResult.Success)
            {
                await response.OK(GenerateSuccessXml(renderSyncLogResult.Message, renderSyncLogResult.DebugInfo), ReturnTypeEnum.Xml, true);
            }
            else
            {
                HandleError(ReturnTypeEnum.Xml, renderSyncLogResult.Message, renderSyncLogResult.DebugInfo);
            }
        }



        /// <summary>
        /// Syncs structured data element value data into section XML content
        /// Executed when a section is requested
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="xmlFilePathOs"></param>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static XmlDocument SyncStructuredDataElements(XmlDocument xmlDocument, string xmlFilePathOs, string projectId)
        {
            // Global variables
            var debugRoutine = siteType == "local" || siteType == "dev";
            StringBuilder sbDebug = new StringBuilder();

            var autoCreateCacheFile = false;
            var insertEmptyFactValues = true;
            var sectionFolderPathOs = Path.GetDirectoryName(xmlFilePathOs);
            var sourceStructuredDataCachePathOs = "";
            var dataFileName = Path.GetFileName(xmlFilePathOs);


            var nodeCmsProjectStructuredDataSnapshot = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']/repositories/structured_data/snapshots/snapshot");
            if (nodeCmsProjectStructuredDataSnapshot == null) HandleError($"There was an error syncing the structured data for project with ID: {projectId}", $"error: 'Could not locate structured data snapshot', stack-trace: {GetStackTrace()}");

            // Retrieve structured data snapshot ID
            var snapshotId = GetAttribute(nodeCmsProjectStructuredDataSnapshot, "id");
            if (string.IsNullOrEmpty(snapshotId)) HandleError($"There was an error syncing the structured data for project with ID: {projectId}", $"error: 'Could not locate structured data snapshot ID', stack-trace: {GetStackTrace()}");

            // Retrieve attribute from the cache that signals to disable the sync from the cache files
            var disableSync = (nodeCmsProjectStructuredDataSnapshot.GetAttribute("disablesync") ?? "false") == "true";
            if (debugRoutine && disableSync) Console.WriteLine($"### disableSync: {disableSync}, projectId: {projectId} ###");

            // Only sync values from the cache files if the sync has not been actively disabled in the configuration
            if (!disableSync)
            {
                var totalSdes = 0;
                var targetSdesPerLanguage = new Dictionary<string, XmlNodeList>();

                var nodeListContentBlocks = xmlDocument.SelectNodes("/data/content");
                if (nodeListContentBlocks.Count > 0)
                {
                    foreach (XmlNode nodeContentBlock in nodeListContentBlocks)
                    {
                        var currentLanguage = nodeContentBlock.GetAttribute("lang");
                        if (!string.IsNullOrEmpty(currentLanguage) && !targetSdesPerLanguage.ContainsKey(currentLanguage))
                        {
                            // Retrieve the individual Structured Data elements from the section XML file
                            var nodeListTargetStructuredDataElements = _retrieveStructuredDataElements(xmlDocument, true, currentLanguage);
                            totalSdes = totalSdes + nodeListTargetStructuredDataElements.Count;
                            targetSdesPerLanguage.Add(currentLanguage, nodeListTargetStructuredDataElements);
                        }
                    }
                }
                else
                {
                    // Fallback for footnote repository
                    if (xmlDocument.SelectSingleNode("/footnotes/footnote/span") != null)
                    {
                        var editorId = RetrieveEditorIdFromProjectId(projectId);
                        var defaultLanguage = RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id='{editorId}']/output_channels[1]/output_channel[1]/variants[1]/variant[1]/@lang", xmlApplicationConfiguration);
                        var projectLangs = RetrieveProjectLanguages(editorId);
                        foreach (var lang in projectLangs)
                        {
                            var sdeXpath = (lang == defaultLanguage) ? $"/footnotes/footnote/span[not(@lang) or @lang='{lang}']//span[@data-fact-id]" : $"/footnotes/footnote/span[@lang='{lang}']//span[@data-fact-id]";

                            var nodeListTargetStructuredDataElements = xmlDocument.SelectNodes(sdeXpath);
                            totalSdes = totalSdes + nodeListTargetStructuredDataElements.Count;
                            targetSdesPerLanguage.Add(lang, nodeListTargetStructuredDataElements);
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"Unknown document type to retrieve SDE information from (dataRef: {dataFileName})");
                    }
                }



                if (totalSdes > 0)
                {
                    // => Retrieve cache (source) file path on the disk
                    var cachedStructuredDataElementsFileName = _generateStructuredDataElementsCacheFilename(xmlFilePathOs);
                    if (string.IsNullOrEmpty(cachedStructuredDataElementsFileName)) HandleError(ReturnTypeEnum.Xml, "Could not generate structured data cache filename", $"dataFileName: {dataFileName}, stack-trace: {GetStackTrace()}");
                    sourceStructuredDataCachePathOs = $"{sectionFolderPathOs}/{cachedStructuredDataElementsFileName}";
                    // if (debugRoutine) Console.WriteLine($"- xmlSourceStructuredDataCachePathOs: {sourceStructuredDataCachePathOs}");


                    if (!File.Exists(sourceStructuredDataCachePathOs))
                    {
                        if (autoCreateCacheFile)
                        {
                            //
                            // => Create a new cache file containing the structured data information from the source xml file (we do not request new data from the server)
                            //
                            if (debugRoutine) Console.WriteLine($"* Does not exist...");
                            var cacheFileCreationResult = _renderNewStructuredDataElementsCacheFileFromContent(targetSdesPerLanguage, sourceStructuredDataCachePathOs, false, false);
                            if (cacheFileCreationResult.Success)
                            {
                                if (debugRoutine) Console.WriteLine($"- cacheFileCreationResult: {cacheFileCreationResult.ToString()}");
                            }
                            else
                            {
                                appLogger.LogError(cacheFileCreationResult.ToString());
                                HandleError(ReturnTypeEnum.Xml, cacheFileCreationResult.Message, cacheFileCreationResult.DebugInfo);
                            }
                        }
                        else
                        {
                            // Log the error
                            appLogger.LogError($"Could not find SDE cache file. sourceStructuredDataCachePathOs: {sourceStructuredDataCachePathOs}, xmlFilePathOs: {xmlFilePathOs}");
                        }
                    }

                    // => Check if the cache file exists
                    if (File.Exists(sourceStructuredDataCachePathOs))
                    {
                        //
                        // => Update the elements in the target XML file
                        //
                        // if (debugRoutine) Console.WriteLine($"* Exists so start updating the SDE's in the content");
                        var xmlSourceStructuredDataCache = new XmlDocument();
                        xmlSourceStructuredDataCache.Load(sourceStructuredDataCachePathOs);


                        // - Loop through the structured data elements found in the target XML file and then update these with the values found in the cache source
                        foreach (var pair in targetSdesPerLanguage)
                        {
                            var currentLanguage = pair.Key;
                            var nodeListTargetStructuredDataElements = pair.Value;

                            foreach (XmlNode nodeTargetStructuredDataElement in nodeListTargetStructuredDataElements)
                            {
                                var syncStatus = "200-ok";
                                var factId = nodeTargetStructuredDataElement.GetAttribute("data-fact-id");
                                var factClass = nodeTargetStructuredDataElement.GetAttribute("class") ?? "";
                                var hideValue = (nodeTargetStructuredDataElement.GetAttribute("data-hidevalue") ?? "") == "true";

                                if (!factClass.Contains("xbrl-level-2"))
                                {
                                    if (hideValue)
                                    {
                                        //
                                        // => Sync in a dummy empty string to "hide" the value from the output
                                        //
                                        nodeTargetStructuredDataElement.InnerText = "";
                                    }
                                    else
                                    {
                                        //
                                        // => Sync the value from the cache 
                                        //
                                        var factTargetValue = nodeTargetStructuredDataElement.InnerText;

                                        // - Find the corresponding element in the source cache document
                                        var nodeListSourceStructuredDataElements = xmlSourceStructuredDataCache.SelectNodes($"/elements/element[@id={GenerateEscapedXPathString(factId)}]");
                                        if (nodeListSourceStructuredDataElements.Count > 0)
                                        {
                                            // - Update the value (using the first match that we have found)
                                            var factValueRaw = nodeListSourceStructuredDataElements.Item(0).SelectSingleNode($"value[@lang='{currentLanguage}']")?.InnerText ??
                                                nodeListSourceStructuredDataElements.Item(0).SelectSingleNode($"value[not(@lang)]")?.InnerText ?? // Old-style SDE cache file
                                                "";
                                            var factValue = factValueRaw.Trim();
                                            if (string.IsNullOrEmpty(factValue) && !insertEmptyFactValues)
                                            {
                                                appLogger.LogError($"Structured Data Element with factId '{factId}' does not contain a value and can therefor not be updated in the section xml document (dataFileName: {dataFileName})");
                                                syncStatus = "404-missing-cache-value";
                                            }
                                            else
                                            {
                                                syncStatus = nodeListSourceStructuredDataElements.Item(0).GetAttribute("status") ?? "404-couldnotfindstatus";
                                                if (syncStatus.StartsWith("500")) factValue = "err";

                                                if (syncStatus == "201-nodatasource")
                                                {
                                                    // Leave the value in the content as-is

                                                }
                                                else
                                                {
                                                    if (factValueRaw == " " || factValueRaw == "\u00A0")
                                                    {
                                                        nodeTargetStructuredDataElement.InnerText = factValueRaw;
                                                    }
                                                    else
                                                    {
                                                        nodeTargetStructuredDataElement.InnerText = string.IsNullOrEmpty(factValue) ? "" : factValue;
                                                    }

                                                }
                                            }

                                        }
                                        else
                                        {
                                            appLogger.LogError($"Structured Data Element with factId '{factId}' cannot be located in the cache and therfor not updated in the section xml document (dataFileName: {dataFileName})");
                                            syncStatus = "404-missing-cache-element";
                                        }
                                    }

                                    // Set the sync status in the data that we will send to the editor
                                    SetAttribute(nodeTargetStructuredDataElement, "data-syncstatus", syncStatus);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!autoCreateCacheFile)
                        {
                            // - Loop through the structured data elements found in the target XML file and then update these with the values found in the cache source
                            foreach (var pair in targetSdesPerLanguage)
                            {
                                var currentLanguage = pair.Key;
                                var nodeListTargetStructuredDataElements = pair.Value;
                                foreach (XmlNode nodeTargetStructuredDataElement in nodeListTargetStructuredDataElements)
                                {
                                    var syncStatus = "404-missing-sdecachefile";
                                    var factClass = nodeTargetStructuredDataElement.GetAttribute("class") ?? "";

                                    if (!factClass.Contains("xbrl-level-2"))
                                    {
                                        // Set the sync status in the data that we will send to the editor
                                        SetAttribute(nodeTargetStructuredDataElement, "data-syncstatus", syncStatus);
                                    }
                                }
                            }
                        }
                        else
                        {
                            appLogger.LogError($"Structured Data Elements cache file not found. sourceStructuredDataCachePathOs: {sourceStructuredDataCachePathOs}, stack-trace: {GetStackTrace()}");
                        }
                    }

                }
            }

            return xmlDocument;
        }


        /// <summary>
        /// Calculates the filename of the structured data cache file
        /// </summary>
        /// <param name="xmlFilePathOs"></param>
        /// <returns></returns>
        private static string _generateStructuredDataElementsCacheFilename(string xmlFilePathOs)
        {
            if (xmlFilePathOs.EndsWith(".xml"))
            {
                return $"__structured-data--{Path.GetFileNameWithoutExtension(xmlFilePathOs)}.xml";
            }
            else
            {
                appLogger.LogWarning($"Could not generate a Structured Data Cache file name from xmlFilePathOs: {xmlFilePathOs}, stack-trace: {GetStackTrace()}");
                return "";
            }
        }



        /// <summary>
        /// Creates a new structured data cache file based on the content of a section XML file
        /// </summary>
        /// <param name="xmlDocument">XML section document to be inspected</param>
        /// <param name="cachedStructuredDataElementsFilePathOs">Path to the SDE cache file that needs to be created</param>
        /// <param name="deleteCacheFileFirst">If set to true, then the existing SDE cache file will be removed before the routine continues</param>
        /// <param name="retrieveFreshData">If set to true, then the routine will request new data from the Taxxor Structured Data system and use these new values in the cache</param>
        /// <returns></returns>
        private static TaxxorReturnMessage _renderNewStructuredDataElementsCacheFileFromContent(XmlDocument xmlDocument, string cachedStructuredDataElementsFilePathOs, bool deleteCacheFileFirst, bool retrieveFreshData)
        {
            // => Create a new one based on the information in the XML file

            var targetSdesPerLanguage = new Dictionary<string, XmlNodeList>();

            var nodeListContentBlocks = xmlDocument.SelectNodes("//content");
            if (nodeListContentBlocks.Count > 0)
            {
                foreach (XmlNode nodeContentBlock in nodeListContentBlocks)
                {
                    var currentLanguage = nodeContentBlock.GetAttribute("lang");
                    if (!string.IsNullOrEmpty(currentLanguage) && !targetSdesPerLanguage.ContainsKey(currentLanguage))
                    {
                        // Retrieve the individual Structured Data elements from the section XML file
                        var nodeListTargetStructuredDataElements = _retrieveStructuredDataElements(xmlDocument, true, currentLanguage);
                        targetSdesPerLanguage.Add(currentLanguage, nodeListTargetStructuredDataElements);
                    }
                }
            }
            else
            {
                // Fallback for footnote repository
                if (xmlDocument.SelectSingleNode("/footnotes/footnote/span") != null)
                {
                    // Retrieve the project id that we have stored in the HTTP context
                    var context = System.Web.Context.Current;
                    object? projectIdTemp = null;

                    if (context.Items.TryGetValue("projectid", out projectIdTemp))
                    {
                        string projectId = (string)projectIdTemp;
                        var editorId = RetrieveEditorIdFromProjectId(projectId);
                        var defaultLanguage = RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id='{editorId}']/output_channels[1]/output_channel[1]/variants[1]/variant[1]/@lang", xmlApplicationConfiguration);
                        var projectLangs = RetrieveProjectLanguages(editorId);
                        foreach (var lang in projectLangs)
                        {
                            var sdeXpath = (lang == defaultLanguage) ? $"/footnotes/footnote/span[not(@lang) or @lang='{lang}']//span[@data-fact-id]" : $"/footnotes/footnote/span[@lang='{lang}']//span[@data-fact-id]";

                            var nodeListTargetStructuredDataElements = xmlDocument.SelectNodes(sdeXpath);
                            targetSdesPerLanguage.Add(lang, nodeListTargetStructuredDataElements);
                        }
                    }
                    else
                    {
                        appLogger.LogError($"Unable to retrieve the project ID from the context for footnotes");
                    }
                }
                else
                {
                    appLogger.LogWarning($"Unknown document type to retrieve SDE information from");
                }
            }


            return _renderNewStructuredDataElementsCacheFileFromContent(targetSdesPerLanguage, cachedStructuredDataElementsFilePathOs, deleteCacheFileFirst, retrieveFreshData);
        }

        /// <summary>
        /// Creates a new structured data cache file based on the content of a section XML file
        /// </summary>
        /// <param name="dictSdesPerLanguage">A nodelist of Structured Data Elements</param>
        /// <param name="cachedStructuredDataElementsFilePathOs">Path to the SDE cache file that needs to be created</param>
        /// <param name="deleteCacheFileFirst">If set to true, then the existing SDE cache file will be removed before the routine continues</param>
        /// <param name="retrieveFreshData">If set to true, then the routine will request new data from the Taxxor Structured Data system and use these new values in the cache</param>
        /// <returns></returns>
        private static TaxxorReturnMessage _renderNewStructuredDataElementsCacheFileFromContent(Dictionary<string, XmlNodeList> dictSdesPerLanguage, string cachedStructuredDataElementsFilePathOs, bool deleteCacheFileFirst, bool retrieveFreshData)
        {
            var errorMessage = "Something went wrong while generating the SDE cache from the data reference file";

            // TODO: Implement fresh data retrieval when building up a new cache file
            if (retrieveFreshData) return new TaxxorReturnMessage(false, "Retrieval of fresh data for re-building SDE cache still needs to be implemented", Path.GetFileName(cachedStructuredDataElementsFilePathOs));

            // Cache filename - can help to debug when things go wrong
            var structuredDataCacheFileName = Path.GetFileName(cachedStructuredDataElementsFilePathOs);

            try
            {
                //
                // => Remove the existing cache file for this section
                //
                if (deleteCacheFileFirst)
                {
                    try
                    {
                        if (File.Exists(cachedStructuredDataElementsFilePathOs)) File.Delete(cachedStructuredDataElementsFilePathOs);
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Structured Data cache file could not be removed. cachedStructuredDataElementsFilePathOs: {cachedStructuredDataElementsFilePathOs}");
                    }
                }

                //
                // => Count the total number of SDE's in the collection
                //
                var languages = new List<string>();
                var totalSdes = 0;
                foreach (var pair in dictSdesPerLanguage)
                {
                    languages.Add(pair.Key);
                    totalSdes = totalSdes + pair.Value.Count;
                }

                //
                // => Create a new cache file
                //
                if (totalSdes > 0)
                {
                    var sdeCacheItems = new Dictionary<string, SdeCacheItem>(); // fact id, cache item
                    foreach (var pair in dictSdesPerLanguage)
                    {
                        var currentLanguage = pair.Key;
                        var nodeListSdes = pair.Value;

                        foreach (XmlNode nodeStructuredDataElement in nodeListSdes)
                        {
                            var factId = nodeStructuredDataElement.GetAttribute("data-fact-id");
                            var factValue = nodeStructuredDataElement.InnerText;

                            SdeCacheItem sdeCacheItem = null;
                            if (!sdeCacheItems.ContainsKey(factId))
                            {
                                // Create a new one
                                sdeCacheItem = new SdeCacheItem(factId, languages);
                                sdeCacheItem.Status = "200-ok";
                            }
                            else
                            {
                                // Re-use the one we have created earlier
                                sdeCacheItem = sdeCacheItems[factId];
                            }

                            // Loop through the existing list of values to check if a value for this language already exists
                            foreach (var existingCacheValue in sdeCacheItem.Values)
                            {
                                if (existingCacheValue.Lang == currentLanguage)
                                {
                                    existingCacheValue.Value = factValue;
                                }
                            }

                            // Store the cache item in the dictionary
                            if (!sdeCacheItems.ContainsKey(factId))
                            {
                                sdeCacheItems.Add(factId, sdeCacheItem);
                            }
                            else
                            {
                                sdeCacheItems[factId] = sdeCacheItem;
                            }

                        }
                    }

                    // - Add dummy values for languages that were not explicitly used 


                    // - Create a new cache file
                    try
                    {
                        var xmlBaseCachedStructuredDataElements = new XmlDocument();
                        xmlBaseCachedStructuredDataElements.AppendChild(xmlBaseCachedStructuredDataElements.CreateElement("elements"));

                        foreach (var pair in sdeCacheItems)
                        {
                            var sdeCacheItem = pair.Value;
                            sdeCacheItem.AddToDoc(ref xmlBaseCachedStructuredDataElements);
                        }

                        if (cachedStructuredDataElementsFilePathOs == null)
                        {
                            // - Return the rendered cache file as payload in the TaxxorReturnMessage
                            var result = new TaxxorReturnMessage(true, "Sucessfully created new structured data cache file", $"Cache information added to XML Payload");
                            result.XmlPayload = new XmlDocument();
                            result.XmlPayload.ReplaceContent(xmlBaseCachedStructuredDataElements);
                            return result;
                        }
                        else
                        {
                            // - Save the cache file
                            xmlBaseCachedStructuredDataElements.Save(cachedStructuredDataElementsFilePathOs);
                        }

                    }
                    catch (Exception ex)
                    {
                        return new TaxxorReturnMessage(false, "Unable to save section data", $"Structured Data cache file could not be created. cachedStructuredDataElementsFilePathOs: {cachedStructuredDataElementsFilePathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }

                    return new TaxxorReturnMessage(true, "Sucessfully created new structured data cache file", $"cachedStructuredDataElementsFilePathOs: {cachedStructuredDataElementsFilePathOs}");
                }
                else
                {
                    return new TaxxorReturnMessage(true, "No need to create a structured data cache file");
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, errorMessage);

                return new TaxxorReturnMessage(false, errorMessage, ex.ToString());
            }


        }

        /// <summary>
        /// Creates a XML structure for a Structured Data Cache element
        /// </summary>
        /// <param name="xmlCachedStructuredDataElements"></param>
        /// <param name="factId"></param>
        /// <param name="factValue"></param>
        /// <param name="factStatus"></param>
        /// <returns></returns>
        private static XmlNode _createStructuredDataCacheElement(XmlDocument xmlCachedStructuredDataElements, string factId, string factValue, string factStatus, string language)
        {
            XmlElement? nodeCachedStructuredDataElement = null;

            var nodeElement = xmlCachedStructuredDataElements.SelectSingleNode($"/elements/element[@id='{factId}']");
            if (nodeElement == null)
            {
                // Create a new element node
                nodeCachedStructuredDataElement = xmlCachedStructuredDataElements.CreateElement("element");
                nodeCachedStructuredDataElement.SetAttribute("id", factId);
                nodeCachedStructuredDataElement.SetAttribute("status", factStatus);
            }
            else
            {
                nodeCachedStructuredDataElement = nodeElement as XmlElement;
            }

            var nodeValue = nodeCachedStructuredDataElement.SelectSingleNode($"value[@lang='{language}']");
            if (nodeValue == null)
            {
                // Create a new value node
                var nodeCachedStructuredDataElementValue = xmlCachedStructuredDataElements.CreateElement("value");
                nodeCachedStructuredDataElementValue.SetAttribute("lang", language);
                nodeCachedStructuredDataElementValue.InnerText = factValue;

                nodeCachedStructuredDataElement.AppendChild(nodeCachedStructuredDataElementValue);
            }
            else
            {
                // Update the value node
                nodeValue.InnerText = factValue;
            }

            return nodeCachedStructuredDataElement;
        }


        /// <summary>
        /// Renders an XmlDocument containing a unique list of structured data elements
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="contentXmlPaths"></param>
        /// <param name="structuredDataElementsFound"></param>
        /// <returns></returns>
        private static TaxxorReturnMessage _renderUniqueStructuredDataElementList(string projectId, List<string> contentXmlPaths, ref int structuredDataElementsFound)
        {
            try
            {
                // Loop through all the datafiles in the project and collect all the structured data elements and their cache files in one XML Document
                var xmlStructuredDataElements = new XmlDocument();
                xmlStructuredDataElements.AppendChild(xmlStructuredDataElements.CreateElement("data"));
                xmlStructuredDataElements.DocumentElement.AppendChild(xmlStructuredDataElements.CreateElement("elements"));
                xmlStructuredDataElements.DocumentElement.AppendChild(xmlStructuredDataElements.CreateElement("cache"));

                var nodeStructuredDataElementsRoot = xmlStructuredDataElements.SelectSingleNode("/data/elements");
                var nodeStructuredDataCacheRoot = xmlStructuredDataElements.SelectSingleNode("/data/cache");

                try
                {
                    if (!Context.Current?.Items?.ContainsKey("projectid") ?? false)
                    {
                        Context.Current?.Items?.Add("projectid", projectId);
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogWarning(ex, $"Unable to add projectid to the Web.Context.Current.Items dictionary.");
                }


                //
                // => Loop throught all the data files posted to this routine
                //                    

                foreach (string xmlContentFilePathOs in contentXmlPaths)
                {
                    var sectionXmlDataFileName = Path.GetFileName(xmlContentFilePathOs);

                    // Load the datafile
                    var xmlContent = new XmlDocument();
                    xmlContent.Load(xmlContentFilePathOs);

                    // Optionally test of this file is actually in use (otherwise we can skip it from the sync routine)
                    var dataFileInUseByAnyOutputChannel = IsSectionDataFileInUse(XmlCmsContentMetadata, projectId, xmlContentFilePathOs);

                    //
                    // => Generate an in-memory XML Document (xmlStructuredDataElements) that contains all the SDE's present in this section content file
                    //
                    var structuredDataElementsCacheCreationResult = _renderNewStructuredDataElementsCacheFileFromContent(xmlContent, null, false, false);
                    if (!structuredDataElementsCacheCreationResult.Success)
                    {
                        return new TaxxorReturnMessage(false, $"Could not create Structured Data Elements cache content", $"xmlContentFilePathOs: {xmlContentFilePathOs}, message: {structuredDataElementsCacheCreationResult.Message}, debuginfo: {structuredDataElementsCacheCreationResult.DebugInfo}, stack-trace: {GetStackTrace()}");
                    }


                    if (structuredDataElementsCacheCreationResult.XmlPayload != null && structuredDataElementsCacheCreationResult.XmlPayload.DocumentElement != null)
                    {
                        // Add information about the Structured Data Elements in the in-memory XML document
                        var nodeCacheImported = xmlStructuredDataElements.ImportNode(structuredDataElementsCacheCreationResult.XmlPayload.DocumentElement, true);
                        var nodeCacheEntry = xmlStructuredDataElements.CreateElement("entry");
                        SetAttribute(nodeCacheEntry, "origin", xmlContentFilePathOs);
                        nodeCacheEntry.AppendChild(nodeCacheImported);
                        nodeStructuredDataCacheRoot.AppendChild(nodeCacheEntry);

                        // Create a unique list of Stuctured Data Elements so that we can request them in an efficient way
                        var nodeListCacheElements = nodeCacheImported.SelectNodes("element");
                        foreach (XmlNode nodeCacheElement in nodeListCacheElements)
                        {
                            structuredDataElementsFound++;
                            var cachedFactId = nodeCacheElement.GetAttribute("id");
                            if (!string.IsNullOrEmpty(cachedFactId))
                            {
                                if (nodeStructuredDataElementsRoot.SelectSingleNode($"element[@id={GenerateEscapedXPathString(cachedFactId)}]") == null)
                                {
                                    // Add the element to the overall list because it does not exist yet
                                    var nodeCacheElementCloned = nodeCacheElement.CloneNode(true);
                                    nodeStructuredDataElementsRoot.AppendChild(nodeCacheElementCloned);
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"Could not find cachedFactId for xml content with path: {xmlContentFilePathOs}");
                            }

                        }
                    }
                    else
                    {
                        appLogger.LogInformation($"sectionXmlDataFileName: {sectionXmlDataFileName} does not seem to contain any structured data elements");
                    }

                }

                // Return the unique list of structured data elements
                return new TaxxorReturnMessage(true, "Successfully compiled a list of structured data elements to sync", xmlStructuredDataElements);
            }
            catch (Exception ex)
            {
                var errorMessage = $"There was an error compiling the list of structured data elements to sync";
                appLogger.LogError(ex, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }


        /// <summary>
        /// Retrieve new data from the Taxxor Structured Data Store and rebuild the cache files on the disk
        /// </summary>
        /// <param name="projectIdToSync"></param>
        /// <param name="xmlStructuredDataElements"></param>
        /// <returns></returns>
        private static async Task<SdeSyncStatictics> _syncStructuredDataElements(string projectIdToSync, XmlDocument xmlStructuredDataElements)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";


            var xmlSectionFolderPathOs = RetrieveContentDataFolderPathOs(projectIdToSync);
            // sbDebug.AppendLine($"- xmlSectionFolderPathOs: {xmlSectionFolderPathOs}");

            // Object which will collect the statistics for the SDE synchronization
            var syncStats = new SdeSyncStatictics();
            var dataReference = "";
            var successMessage = "";
            var warningMessage = "";
            var errorMessage = "There was an error syncing the structured data elements";
            // var debugFactId = "xyz";

            try
            {

                var nodeStructuredDataElementsRoot = xmlStructuredDataElements.SelectSingleNode("/data/elements");
                var nodeStructuredDataCacheRoot = xmlStructuredDataElements.SelectSingleNode("/data/cache");

                dataReference = nodeStructuredDataCacheRoot.SelectSingleNode("entry")?.GetAttribute("origin") ?? "unknown";
                if (dataReference != "unknown")
                {
                    dataReference = Path.GetFileName(dataReference);
                }

                if (debugRoutine) xmlStructuredDataElements.Save($"{logRootPathOs}/_____sde.xml");

                //
                // => Retrieve all the structured data element values in one request from the mapping service
                //
                var xmlStructuredDataElementValuesRetrieved = new XmlDocument();
                var factGuids = new List<string>();
                var nodeListStructuredDataElements = xmlStructuredDataElements.SelectNodes("/data/elements/element");
                if (nodeListStructuredDataElements.Count == 0)
                {
                    syncStats.Success = true;
                    syncStats.Message = "No structured data elements found";
                    return syncStats;
                }


                foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                {
                    var factId = nodeStructuredDataElement.GetAttribute("id");
                    if (!factGuids.Contains(factId)) factGuids.Add(factId);
                }

                // Add a dummy element so that we always receive an XML document back as payload
                if (factGuids.Count == 1)
                {
                    factGuids.Add("bogus");
                }

                var watch = System.Diagnostics.Stopwatch.StartNew();
                if (debugRoutine) Console.WriteLine($"Start bulk data value retrieve for {dataReference} with {factGuids.Count} number of SDE's");
                var bulkRetrieveResult = await RetieveStructuredDataValue(string.Join(",", [.. factGuids]), projectIdToSync);
                watch.Stop();
                if (debugRoutine) Console.WriteLine($"Bulk data value retrieve took: {watch.ElapsedMilliseconds.ToString()} ms");

                if (bulkRetrieveResult.Success)
                {
                    xmlStructuredDataElementValuesRetrieved = bulkRetrieveResult.XmlPayload;
                    if (debugRoutine) xmlStructuredDataElementValuesRetrieved.Save($"{logRootPathOs}/_____sde-retrieved.xml");
                }
                else
                {
                    syncStats.ImportTaxxorResultMessage(bulkRetrieveResult);
                    return syncStats;
                }

                //
                // => Retrieve new content for the structured data nodes that we have found
                //
                var nodeListStructuredDataElementsToRetrieve = nodeStructuredDataElementsRoot.SelectNodes("element");
                syncStats.StructuredDataElementsUnique = nodeListStructuredDataElementsToRetrieve.Count;
                foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElementsToRetrieve)
                {
                    // TODO: Add these values to the syncStats object
                    var factGuid = GetAttribute(nodeStructuredDataElement, "id");
                    var syncStatus = "200-ok";

                    // Avoid errors to appear for each language
                    var errorLogged = false;

                    // Loop through each language value
                    foreach (XmlNode nodeStructuredDataElementValue in nodeStructuredDataElement.SelectNodes("value"))
                    {
                        var currentLanguage = nodeStructuredDataElementValue.GetAttribute("lang");

                        if (string.IsNullOrEmpty(currentLanguage))
                        {
                            //
                            // => Return an error message
                            //
                            syncStats.Success = false;
                            syncStats.Message = errorMessage;
                            syncStats.DebugInfo = $"SDE list in wrong format - unable to find language information for factGuid: {factGuid}. nodeStructuredDataElement: {nodeStructuredDataElement.OuterXml}, stack-trace: {GetStackTrace()}";
                            return syncStats;
                        }

                        string? originalValue = nodeStructuredDataElementValue?.InnerText ?? null;

                        var sucessfullyRetrievedNewValue = false;
                        var newValue = "";
                        if (originalValue == null)
                        {
                            warningMessage = $"Could not find original structured data value node for factGuid: {factGuid}";
                            syncStats.LogWarning.AppendLine(warningMessage);
                            appLogger.LogWarning(warningMessage);
                            var nodeValueNew = xmlStructuredDataElements.CreateElement("value");
                            nodeValueNew.SetAttribute("lang", currentLanguage);
                            nodeStructuredDataElement.AppendChild(nodeStructuredDataElementValue);
                        }
                        else
                        {
                            // - Find the corresponding xml node in the data we have just received in bulk from the mapping service
                            var nodeRetrievedStructuredDataElement = xmlStructuredDataElementValuesRetrieved.SelectSingleNode($"/response/item[@id={GenerateEscapedXPathString(factGuid)}]");

                            if (nodeRetrievedStructuredDataElement != null)
                            {
                                // - Parse the information from the received content
                                newValue = nodeRetrievedStructuredDataElement.SelectSingleNode($"value[@lang='{currentLanguage}']")?.InnerText ?? "";
                                var factId = nodeRetrievedStructuredDataElement.GetAttribute("id") ?? "nothingthatcanevermatch";
                                var retrieveStatusResult = nodeRetrievedStructuredDataElement.GetAttribute("result") ?? "unknown";


                                syncStatus = _sdeRetrieveResultToSyncStatus(retrieveStatusResult);

                                if (syncStatus == "200-ok")
                                {
                                    // Store the retrieved value in the XML file that we are compiling
                                    nodeStructuredDataElementValue.InnerText = newValue;
                                    sucessfullyRetrievedNewValue = true;
                                }
                                else if (syncStatus == "201-nodatasource")
                                {
                                    // No need to update anything

                                }
                                else
                                {
                                    // Figure out in which sections this fact is being used and mark the error in the cache file
                                    var xPath = $"entry/elements/element[@id={GenerateEscapedXPathString(factGuid)}]";
                                    var nodeListCacheStructuredDataElements = nodeStructuredDataCacheRoot.SelectNodes(xPath);
                                    var dataFilesContainingErrorStructuredDataElement = new List<string>();
                                    foreach (XmlNode nodeCacheStructuredDataElement in nodeListCacheStructuredDataElements)
                                    {
                                        // Find origin filename
                                        var nodeEntry = nodeCacheStructuredDataElement.ParentNode.ParentNode;
                                        var originFilePath = nodeEntry.GetAttribute("origin");
                                        if (!string.IsNullOrEmpty(originFilePath))
                                        {
                                            dataFilesContainingErrorStructuredDataElement.Add(Path.GetFileName(originFilePath));
                                        }
                                        else
                                        {
                                            appLogger.LogWarning($"Could not retrieve structured data element origin path using {xPath}");
                                        }
                                    }

                                    // Make sure we capture the issue
                                    if (!errorLogged)
                                    {
                                        errorMessage = $"Error fetching data for factGuid: {factGuid} found in {string.Join(",", dataFilesContainingErrorStructuredDataElement.ToArray())}";
                                        appLogger.LogError($"{errorMessage}, status: {syncStatus}");
                                        syncStats.LogError.AppendLine(errorMessage);
                                        errorLogged = true;
                                    }

                                }

                            }
                            else
                            {
                                syncStatus = "404-notfoundinmappingservice";

                                // Make sure we capture the issue
                                errorMessage = $"Could not locate: {factGuid} in response from the mapping service bulk request";
                                appLogger.LogError($"{errorMessage}, status: {syncStatus}");
                                syncStats.LogError.AppendLine(errorMessage);
                            }
                        }

                        // Update the status element with the new status
                        nodeStructuredDataElement.SetAttribute("status", syncStatus);

                        // Update the syncStatus object
                        var sdeValueForLog = newValue;
                        if (string.IsNullOrEmpty(sdeValueForLog)) sdeValueForLog = originalValue;
                        if (string.IsNullOrEmpty(sdeValueForLog)) sdeValueForLog = "no-value";
                        syncStats.AddSdeSyncInfo(factGuid, syncStatus, sdeValueForLog);

                        if (debugRoutine)
                        {
                            Console.WriteLine("");
                            Console.WriteLine($"- factGuid: {factGuid}");
                            Console.WriteLine($"- originalValue: {originalValue}");
                            if (sucessfullyRetrievedNewValue)
                            {
                                Console.WriteLine($"- newValue: {newValue}");
                            }
                            else
                            {
                                Console.WriteLine("ERROR: Could not retrieve new structured data element value");
                            }
                            Console.WriteLine("");
                        }

                    }
                }


                //
                // => Update the cache entries in the XML Document with updated values
                //
                foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElementsToRetrieve)
                {
                    // Retrieve the values
                    var factGuid = GetAttribute(nodeStructuredDataElement, "id");
                    var structuredDataElementSyncStatus = nodeStructuredDataElement.GetAttribute("status");

                    // Loop through each language value
                    foreach (XmlNode nodeStructuredDataElementValue in nodeStructuredDataElement.SelectNodes("value"))
                    {
                        var currentLanguage = nodeStructuredDataElementValue.GetAttribute("lang");


                        if (nodeStructuredDataElementValue != null && structuredDataElementSyncStatus != null)
                        {
                            var structuredDataElementValue = nodeStructuredDataElementValue.InnerText;

                            // Set the values in the cache part of the XML document
                            var xPath = $"entry/elements/element[@id={GenerateEscapedXPathString(factGuid)}]";
                            var nodeListCacheStructuredDataElements = nodeStructuredDataCacheRoot.SelectNodes(xPath);
                            if (nodeListCacheStructuredDataElements.Count == 0)
                            {
                                warningMessage = $"Could not find original structured data cache element for factGuid: {factGuid}";
                                syncStats.LogWarning.AppendLine(warningMessage);
                                appLogger.LogWarning(warningMessage);
                            }
                            else
                            {
                                foreach (XmlNode nodeCacheStructuredDataElement in nodeListCacheStructuredDataElements)
                                {
                                    nodeCacheStructuredDataElement.SetAttribute("status", structuredDataElementSyncStatus);

                                    var nodeCacheStructuredDataElementValue = nodeCacheStructuredDataElement.SelectSingleNode($"value[@lang='{currentLanguage}']");
                                    if (nodeCacheStructuredDataElementValue != null)
                                    {
                                        if (nodeCacheStructuredDataElementValue.InnerText.Trim() != structuredDataElementValue.Trim())
                                        {
                                            // Update the value
                                            nodeCacheStructuredDataElementValue.InnerText = structuredDataElementValue;
                                            syncStats.StructuredDataElementsUpdated++;
                                        }
                                        else
                                        {
                                            syncStats.StructuredDataElementWithoutUpdate++;
                                        }

                                        // Write success in log
                                        successMessage = $"Successfully processed structured data element with ID: {factGuid}";
                                        syncStats.LogSuccess.AppendLine(successMessage);
                                    }
                                    else
                                    {
                                        warningMessage = $"Could not find original structured data cache value element for factGuid: {factGuid}";
                                        syncStats.LogWarning.AppendLine(warningMessage);
                                        appLogger.LogWarning(warningMessage);
                                    }
                                }
                            }
                        }
                        else
                        {
                            errorMessage = $"Error retrieving structured data value and status for factGuid: {factGuid}";
                            appLogger.LogError(errorMessage);
                            syncStats.LogError.AppendLine(errorMessage);
                        }
                    }
                }


                //
                // => Write the new cache files to the disk
                //
                var nodeListCacheEntries = nodeStructuredDataCacheRoot.SelectNodes("entry");
                foreach (XmlNode nodeCacheEntry in nodeListCacheEntries)
                {
                    var originalDataFilePathOs = nodeCacheEntry.GetAttribute("origin");
                    if (!string.IsNullOrEmpty(originalDataFilePathOs))
                    {
                        var cacheStructuredDataFilePathOs = xmlSectionFolderPathOs + "/" + _generateStructuredDataElementsCacheFilename(originalDataFilePathOs);
                        try
                        {

                            var xmlStructuredDataCache = new XmlDocument();
                            var nodeCache = xmlStructuredDataCache.ImportNode(nodeCacheEntry.FirstChild, true);
                            xmlStructuredDataCache.AppendChild(nodeCache);
                            xmlStructuredDataCache.Save(cacheStructuredDataFilePathOs);
                        }
                        catch (Exception ex)
                        {
                            errorMessage = $"Could not store new structured data cache file '{Path.GetFileName(cacheStructuredDataFilePathOs)}'";
                            appLogger.LogError(ex, $"{errorMessage}, error: {ex}");
                            syncStats.LogError.AppendLine(errorMessage);
                        }
                    }
                    else
                    {
                        errorMessage = $"Could not retrieve original file path for cache file creation";
                        appLogger.LogError(errorMessage);
                        syncStats.LogError.AppendLine(errorMessage);
                    }
                }

                //
                // => Return a response
                //
                syncStats.Success = true;
                syncStats.Message = "Successfully synced the structured data elements";
                syncStats.DebugInfo = $"projectId: {projectIdToSync}, dataReference: {dataReference}";
                return syncStats;


            }
            catch (Exception ex)
            {
                //
                // => Return an error message
                //
                syncStats.Success = false;
                syncStats.Message = errorMessage;
                syncStats.DebugInfo = $"error: {ex}, stack-trace: {GetStackTrace()}";
                return syncStats;
            }

        }

        /// <summary>
        /// Remove all the Structured Data cache for a project
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        private static TaxxorReturnMessage _removeCachedStructuredDataFiles(string projectId)
        {
            var xmlSectionFolderPathOs = RetrieveContentDataFolderPathOs(projectId);
            try
            {
                Directory.EnumerateFiles(xmlSectionFolderPathOs)
                    .Where(fn => Path.GetExtension(fn) == ".xml" && Path.GetFileName(fn).StartsWith("__structured-data"))
                    .ToList()
                    .ForEach(f => File.Delete(f));
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, $"Could not delete all structured data cache files", $"error: {ex}, projectId: {projectId}, xmlSectionFolderPathOs: {xmlSectionFolderPathOs}");
            }
            return new TaxxorReturnMessage(true, "Successfully removed all cached structured data files", $"projectId: {projectId}, xmlSectionFolderPathOs: {xmlSectionFolderPathOs}");
        }

        /// <summary>
        /// Removes a part of the Structured Data Cache files for a project
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="dataReferences"></param>
        /// <returns></returns>
        private static TaxxorReturnMessage _removeCachedStructuredDataFiles(string projectId, List<string> dataReferences)
        {
            var xmlSectionFolderPathOs = RetrieveContentDataFolderPathOs(projectId);
            try
            {
                foreach (var pathOs in Directory.GetFiles(xmlSectionFolderPathOs, "*.xml"))
                {
                    if (pathOs.Contains("__structured-data"))
                    {
                        var cacheFileName = Path.GetFileName(pathOs);
                        var associatedDataReferenceFileName = cacheFileName.Replace("__structured-data--", "");
                        if (dataReferences.Contains(associatedDataReferenceFileName)) File.Delete(pathOs);
                    }
                }
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, $"Could not delete structured data cache files", $"error: {ex}, projectId: {projectId}, xmlSectionFolderPathOs: {xmlSectionFolderPathOs}, dataReferences: {string.Join(',', dataReferences.ToArray())}");
            }
            return new TaxxorReturnMessage(true, "Successfully removed cached structured data files", $"projectId: {projectId}, xmlSectionFolderPathOs: {xmlSectionFolderPathOs}");
        }

        /// <summary>
        /// Stores the structured data cache files in a temporary directory
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="dataReferences"></param>
        /// <returns></returns>
        private static TaxxorReturnMessage _backupCachedStructuredDataFiles(string projectId, List<string> dataReferences = null)
        {
            var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/structureddatacache-{projectId}";
            var xmlSectionFolderPathOs = RetrieveContentDataFolderPathOs(projectId);
            try
            {
                if (!Directory.Exists(tempFolderPathOs))
                {
                    Directory.CreateDirectory(tempFolderPathOs);
                }
                else
                {
                    DelTree(tempFolderPathOs, false);
                }

                foreach (var pathOs in Directory.GetFiles(xmlSectionFolderPathOs, "*.xml"))
                {
                    if (pathOs.Contains("__structured-data"))
                    {
                        var cacheFileName = Path.GetFileName(pathOs);

                        if (dataReferences == null)
                        {
                            File.Move(pathOs, $"{tempFolderPathOs}/{Path.GetFileName(pathOs)}");
                        }
                        else
                        {
                            var associatedDataReferenceFileName = cacheFileName.Replace("__structured-data--", "");
                            if (dataReferences.Contains(associatedDataReferenceFileName))
                            {
                                File.Move(pathOs, $"{tempFolderPathOs}/{Path.GetFileName(pathOs)}");
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Could not backup structured data cache files");
                return new TaxxorReturnMessage(false, $"Could not backup structured data cache files", $"error: {ex}, projectId: {projectId}, xmlSectionFolderPathOs: {xmlSectionFolderPathOs}, dataReferences: {string.Join(',', dataReferences.ToArray())}");
            }
            return new TaxxorReturnMessage(true, "Successfully backed up cached structured data files", $"projectId: {projectId}, xmlSectionFolderPathOs: {xmlSectionFolderPathOs}");
        }


        /// <summary>
        /// Generates an SDE sync report by comparing the SDE cache files before and after synchronization
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        private static TaxxorReturnMessage _syncStructuredDataElementsCreateLog(string projectId)
        {
            var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/structureddatacache-{projectId}";
            var xmlSectionFolderPathOs = RetrieveContentDataFolderPathOs(projectId);
            var xmlLogReport = new XmlDocument();
            xmlLogReport.LoadXml("<syncreport/>");
            xmlLogReport.DocumentElement.SetAttribute("projectid", projectId);
            var errorMessage = "";

            try
            {

                if (!Directory.Exists(tempFolderPathOs))
                {
                    errorMessage = $"Unable to locate backup folder at {tempFolderPathOs}";
                    appLogger.LogError(errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"projectId: {projectId}, xmlSectionFolderPathOs: {xmlSectionFolderPathOs}, tempFolderPathOs: {tempFolderPathOs}");
                }



                foreach (var cacheBackupPathOs in Directory.GetFiles(tempFolderPathOs, "*.xml"))
                {
                    var xmlCacheBackup = new XmlDocument();
                    var xmlCacheCurrent = new XmlDocument();

                    xmlCacheBackup.Load(cacheBackupPathOs);

                    var cacheCurrentPathOs = $"{xmlSectionFolderPathOs}/{Path.GetFileName(cacheBackupPathOs)}";
                    if (File.Exists(cacheCurrentPathOs)) xmlCacheCurrent.Load(cacheCurrentPathOs);

                    var nodeReference = xmlLogReport.CreateElement("datareference");
                    nodeReference.SetAttribute("dataref", Path.GetFileName(cacheBackupPathOs).Replace("__structured-data--", ""));

                    var nodeListBackupCacheSdes = xmlCacheBackup.SelectNodes("/elements/element");
                    foreach (XmlNode nodeBackupCacheSde in nodeListBackupCacheSdes)
                    {
                        var factId = nodeBackupCacheSde.GetAttribute("id");
                        var backupSyncStatus = nodeBackupCacheSde.GetAttribute("status");
                        var backupSdeValue = nodeBackupCacheSde.SelectSingleNode("value")?.InnerText ?? "";

                        var nodeCurrentCacheSde = xmlCacheCurrent.SelectSingleNode($"/elements/element[@id={GenerateEscapedXPathString(factId)}]");
                        string? currentSyncStatus = null;
                        string? currentSdeValue = null;
                        if (nodeCurrentCacheSde != null)
                        {
                            currentSyncStatus = nodeCurrentCacheSde.GetAttribute("status");
                            currentSdeValue = nodeCurrentCacheSde.SelectSingleNode("value")?.InnerText ?? null;
                        }

                        var nodeSde = xmlLogReport.CreateElement("sde");
                        nodeSde.SetAttribute("id", factId);
                        var nodeBackupValue = xmlLogReport.CreateElementWithText("value-old", backupSdeValue);
                        nodeBackupValue.SetAttribute("status", backupSyncStatus);
                        nodeSde.AppendChild(nodeBackupValue);

                        if (currentSdeValue != null)
                        {
                            var nodeCurrentValue = xmlLogReport.CreateElementWithText("value-new", currentSdeValue);
                            nodeCurrentValue.SetAttribute("status", (currentSyncStatus == null) ? "unknown" : currentSyncStatus);
                            nodeSde.AppendChild(nodeCurrentValue);
                        }

                        nodeReference.AppendChild(nodeSde);
                    }

                    xmlLogReport.DocumentElement.AppendChild(nodeReference);
                }

                //
                // => Store the log in the data directory
                //
                var syncLogFilePathOs = $"{xmlSectionFolderPathOs}/logs/sde-synclog.xml";
                var syncLogFolderPathOs = Path.GetDirectoryName(syncLogFilePathOs);
                if (!Directory.Exists(syncLogFilePathOs))
                {
                    Directory.CreateDirectory(syncLogFolderPathOs);
                }
                xmlLogReport.Save(syncLogFilePathOs);


                return new TaxxorReturnMessage(true, "Successfully created sync log", xmlLogReport, $"projectId: {projectId}, xmlSectionFolderPathOs: {xmlSectionFolderPathOs}, tempFolderPathOs: {tempFolderPathOs}");
            }
            catch (Exception ex)
            {
                errorMessage = "Unable to render sync report";
                appLogger.LogError(ex, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}, projectId: {projectId}, xmlSectionFolderPathOs: {xmlSectionFolderPathOs}, tempFolderPathOs: {tempFolderPathOs}");
            }

        }


        /// <summary>
        /// Updates the SDE cache file with the values located in the XML document passed to this routine
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="xmlFilePathOs"></param>
        /// <param name="projectId"></param>
        /// <param name="langSavedContent"></param>
        /// <param name="rebuildStructuredDataCacheOnSave"></param>
        /// <param name="removeUnusedCacheEntries"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage UpdateStructuredDataElementsCacheFile(XmlDocument xmlDocument, string xmlFilePathOs, string projectId, string langSavedContent, bool rebuildStructuredDataCacheOnSave, bool removeUnusedCacheEntries, bool debugRoutine)
        {
            // Helps setting a breakpoint in the routine
            var factIdDebug = "xyz";


            var nodeListStructuredDataSnapshots = xmlApplicationConfiguration.SelectNodes($"/configuration/cms_projects/cms_project[@id='{projectId}']/repositories/structured_data/snapshots/snapshot");
            if (nodeListStructuredDataSnapshots.Count > 0)
            {
                try
                {
                    // - Construct a filename and path for the cache file
                    var cachedStructuredDataElementsFileName = _generateStructuredDataElementsCacheFilename(xmlFilePathOs);
                    if (string.IsNullOrEmpty(cachedStructuredDataElementsFileName)) HandleError(ReturnTypeEnum.Xml, "Could not generate structured data cache filename", $"stack-trace: {GetStackTrace()}");
                    var cachedStructuredDataElementsFilePathOs = $"{Path.GetDirectoryName(xmlFilePathOs)}/{cachedStructuredDataElementsFileName}";
                    var cachedStructuredDataElementsFileExists = File.Exists(cachedStructuredDataElementsFilePathOs);
                    if (debugRoutine) Console.WriteLine($"- cachedStructuredDataElementsFileName: {cachedStructuredDataElementsFileName}");

                    if (!cachedStructuredDataElementsFileExists || rebuildStructuredDataCacheOnSave)
                    {
                        // - Create a new cache file
                        var deleteCacheFileFirst = false;
                        if (rebuildStructuredDataCacheOnSave && cachedStructuredDataElementsFileExists) deleteCacheFileFirst = true;

                        // - Create the cache file if required
                        var cacheFileCreationResult = _renderNewStructuredDataElementsCacheFileFromContent(xmlDocument, cachedStructuredDataElementsFilePathOs, deleteCacheFileFirst, false);
                        if (cacheFileCreationResult.Success)
                        {
                            if (debugRoutine) Console.WriteLine($"- cacheFileCreationResult: {cacheFileCreationResult.ToString()}");
                        }
                        else
                        {
                            HandleError(ReturnTypeEnum.Xml, cacheFileCreationResult.Message, cacheFileCreationResult.DebugInfo);
                        }
                    }
                    else
                    {
                        // - Update the existing cache file with new values
                        var targetStructuredDataCacheChanged = false;
                        var xmlTargetStructuredDataCache = new XmlDocument();
                        xmlTargetStructuredDataCache.Load(cachedStructuredDataElementsFilePathOs);

                        // - Find the source and target structured data items
                        var nodeListTargetStructuredDataElements = xmlTargetStructuredDataCache.SelectNodes("/elements/element");

                        XmlNodeList? nodeListSourceStructuredDataElements = null;
                        var nodeListContentBlocks = xmlDocument.SelectNodes("/data/content");
                        if (nodeListContentBlocks.Count > 0)
                        {
                            nodeListSourceStructuredDataElements = _retrieveStructuredDataElements(xmlDocument, true, langSavedContent);
                        }
                        else
                        {
                            // Fallback for footnote repository
                            if (xmlDocument.SelectSingleNode("/footnotes/footnote/span") != null)
                            {
                                nodeListSourceStructuredDataElements = xmlDocument.SelectNodes($"/footnotes/footnote/span//span[@data-fact-id]");
                            }
                            else
                            {
                                appLogger.LogWarning($"Unknown document type to retrieve SDE information from");
                            }
                        }


                        // - Update the cache file with the delta
                        if (removeUnusedCacheEntries)
                        {
                            foreach (XmlNode nodeTargetStructuredDataElement in nodeListTargetStructuredDataElements)
                            {
                                SetAttribute(nodeTargetStructuredDataElement, "remove", "true");
                            }
                        }


                        // 1) Find new items in the source
                        foreach (XmlNode nodeSourceStructedDataElement in nodeListSourceStructuredDataElements)
                        {
                            var factId = nodeSourceStructedDataElement.GetAttribute("data-fact-id");
                            if (string.IsNullOrEmpty(factId))
                            {
                                appLogger.LogWarning($"Found Structured Data Element in the content XML without an empty factId");
                            }
                            else
                            {
                                var noCacheUpdate = nodeSourceStructedDataElement.GetAttribute("data-nocacheupdate") ?? "false";
                                if (factId == factIdDebug)
                                {
                                    appLogger.LogDebug("Debug fact id");
                                }

                                if (noCacheUpdate == "false")
                                {
                                    var factValue = nodeSourceStructedDataElement.InnerText;

                                    // Can we find this in the cache?
                                    var structuredDataElementExistsInTarget = false;
                                    var firstElement = true;

                                    var nodeListTarget = xmlTargetStructuredDataCache.SelectNodes($"/elements/element[@id={GenerateEscapedXPathString(factId)}]");
                                    if (nodeListTarget.Count > 1) appLogger.LogWarning($"The structured data elements cache contains {nodeListTarget.Count.ToString()} elements with the same factId ('{factId}'). stack-trace: {GetStackTrace()}");
                                    foreach (XmlNode nodeCacheTarget in nodeListTarget)
                                    {
                                        structuredDataElementExistsInTarget = true;

                                        // Remove the "remove" attribute
                                        RemoveAttribute(nodeCacheTarget, "remove");

                                        // Remove double cache items if these exist
                                        if (!firstElement)
                                        {
                                            RemoveXmlNode(nodeCacheTarget);
                                            targetStructuredDataCacheChanged = true;
                                        }
                                        else
                                        {
                                            // Test if we need to update the value in the cache, because the user made a new selection in an existing Structured Data Element
                                            var nodeCacheTargetValue = nodeCacheTarget.SelectSingleNode($"value[@lang='{langSavedContent}']");
                                            if (nodeCacheTargetValue != null)
                                            {
                                                if (nodeCacheTargetValue.InnerText != factValue)
                                                {
                                                    nodeCacheTargetValue.InnerText = factValue;
                                                    targetStructuredDataCacheChanged = true;
                                                }
                                            }
                                            else
                                            {
                                                appLogger.LogWarning($"Structured data cache element (filename: {cachedStructuredDataElementsFileName}) with ID {factId} has no value node");
                                            }
                                        }

                                        firstElement = false;
                                    }

                                    // If the node does not exist in the target cache document, then create it

                                    if (!structuredDataElementExistsInTarget)
                                    {
                                        xmlTargetStructuredDataCache.DocumentElement.AppendChild(_createStructuredDataCacheElement(xmlTargetStructuredDataCache, factId, factValue, "new", langSavedContent));
                                        targetStructuredDataCacheChanged = true;
                                    }
                                }
                                else
                                {
                                    appLogger.LogInformation($"No need to update factId: {factId} in the cache, bacause @data-nocacheupdate was set to 'true'.");
                                }
                            }
                        }

                        // 2) Delete the items from the target cache that are not in use anymore
                        if (removeUnusedCacheEntries)
                        {
                            var nodeListTargetStructuredDataElementsToRemove = xmlTargetStructuredDataCache.SelectNodes("/elements/element[@remove='true']");
                            if (nodeListTargetStructuredDataElementsToRemove.Count > 0)
                            {
                                RemoveXmlNodes(nodeListTargetStructuredDataElementsToRemove);
                                targetStructuredDataCacheChanged = true;
                            }
                        }



                        // 3) Save the updated cache file on the disk
                        if (targetStructuredDataCacheChanged)
                        {
                            if (debugRoutine) Console.WriteLine("** Saving updated structured data cache file **");

                            // Assure that the SDE cache file is stored in the correct format
                            var editorId = RetrieveEditorIdFromProjectId(projectId);
                            var projectLanguages = RetrieveProjectLanguages(editorId);
                            var xsltArgumentList = new XsltArgumentList();
                            for (int i = 0; i < projectLanguages.Length; i++)
                            {
                                var language = projectLanguages[i];
                                var parameterName = $"language{(i + 1).ToString()}";
                                xsltArgumentList.AddParam(parameterName, "", language);
                            }
                            xmlTargetStructuredDataCache = TransformXmlToDocument(xmlTargetStructuredDataCache, "xsl_sdecache-convert", xsltArgumentList);

                            xmlTargetStructuredDataCache.Save(cachedStructuredDataElementsFilePathOs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "There was an error updating the structured data cache", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }

            return new TaxxorReturnMessage(true, "Successfully updated SDE cache entries");
        }

        /// <summary>
        /// Represents the Structured Data Elements cache which is associated with a content data file that contains SDE's
        /// </summary>
        public class SdeCache
        {
            public List<SdeCacheItem> Items { get; set; } = new List<SdeCacheItem>();

            public SdeCache() { }

            public SdeSearchResult Find(string factId, string lang)
            {
                var searchResult = new SdeSearchResult();

                foreach (var sdeCacheItem in this.Items)
                {
                    if (sdeCacheItem.Id == factId)
                    {
                        searchResult.Status = sdeCacheItem.Status;

                        foreach (var sdeValue in sdeCacheItem.Values)
                        {
                            if (sdeValue.Lang == lang)
                            {
                                searchResult.Found = true;
                                searchResult.Value = sdeValue.Value;
                                break;
                            }
                        }
                        break;
                    }
                }

                return searchResult;
            }
        }

        /// <summary>
        /// Represents an SDE cache item
        /// </summary>
        public class SdeCacheItem
        {
            public string Id { get; set; } = "";
            public string Status { get; set; } = "";

            public List<SdeCacheValue> Values { get; set; } = new List<SdeCacheValue>();
            public SdeCacheItem() { }

            public SdeCacheItem(string id)
            {
                this.Id = id;
            }

            public SdeCacheItem(string id, List<string> languages)
            {
                this.Id = id;

                foreach (string lang in languages)
                {
                    this.Values.Add(new SdeCacheValue(lang, null));
                }
            }

            public void AddToDoc(ref XmlDocument xmlDoc)
            {
                var nodeElement = xmlDoc.CreateElement("element");
                nodeElement.SetAttribute("id", this.Id);
                nodeElement.SetAttribute("status", this.Status);
                foreach (var cacheValue in this.Values)
                {
                    var nodeValue = xmlDoc.CreateElement("value");
                    nodeValue.SetAttribute("lang", cacheValue.Lang);
                    nodeValue.InnerText = cacheValue.Value ?? "";
                    nodeElement.AppendChild(nodeValue);
                }

                xmlDoc.DocumentElement.AppendChild(nodeElement);
            }

        }

        /// <summary>
        /// Represents an SDE value for a specific language
        /// </summary>
        public class SdeCacheValue
        {
            public string Lang { get; set; }
            public string Value { get; set; }

            public SdeCacheValue(string lang, string value)
            {
                this.Lang = lang;
                this.Value = value;
            }
        }

        /// <summary>
        /// Utility class containing the information of a SDE value search
        /// </summary>
        public class SdeSearchResult
        {
            public bool Found { get; set; } = false;
            public string Id { get; set; } = "";
            public string Status { get; set; } = "";
            public string Value { get; set; } = "";

            public SdeSearchResult()
            {

            }
        }


    }
}