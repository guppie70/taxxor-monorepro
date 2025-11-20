using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities for working with Structured Data Elements
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves a single structured data value
        /// </summary>
        /// <param name="factGuid">One Structured Data Element ID or a comma seperated list of Structured Data Element ID's</param>
        /// <param name="projectId">Taxxor project ID</param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RetieveStructuredDataValue(string factGuid, string projectId, string? valueLang = null, bool debugRoutine = false)
        {
            if (!debugRoutine)
            {
                if (siteType == "local" || siteType == "dev") debugRoutine = true;

                // Disabled standard debugging
                debugRoutine = false;
            }

            var languages = RetrieveProjectLanguages(RetrieveEditorIdFromProjectId(projectId));
            // string[] languages = { "en" };
            var baseLogFilePathOs = $"{logRootPathOs}/_structureddatavalues";

            var retrieveMultiple = false;

            try
            {
                var xmlStructuredDataQuery = new XmlDocument();
                xmlStructuredDataQuery.AppendChild(xmlStructuredDataQuery.CreateElement("request"));

                if (factGuid.Contains(','))
                {
                    retrieveMultiple = true;
                    var factGuids = factGuid.Split(',').ToList();
                    foreach (var factId in factGuids)
                    {
                        xmlStructuredDataQuery.DocumentElement.AppendChild(xmlStructuredDataQuery.CreateElementWithText("item", factId));
                    }
                }
                else
                {
                    xmlStructuredDataQuery.DocumentElement.AppendChild(xmlStructuredDataQuery.CreateElementWithText("item", factGuid));
                }




                if (debugRoutine)
                {
                    Console.WriteLine("**** BULK RETRIEVE POST DATA ****");
                    Console.WriteLine(PrettyPrintXml(xmlStructuredDataQuery));
                    Console.WriteLine("*********************************");
                }


                // - Retrieve the URL of the mapping service (for some reason, the "proxy" route is not part of the service description)
                var queryString = $"?locale={string.Join("&locale=", languages)}";
                var forceErpSourceUpdate = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='always-refresh-erp-data']")?.InnerText ?? "false";
                if (forceErpSourceUpdate == "true")
                {
                    queryString += "&refresh=true";
                }

                var mappingServiceApiUri = $"{GetServiceUrl(ConnectedServiceEnum.MappingService)}/api/v2/projects/{projectId}/datastore/values{queryString}";

                // - Call the mapping service to retrieve the Structured Data Element value
                CustomHttpHeaders customHttpHeaders = new CustomHttpHeaders();
                customHttpHeaders.RequestType = ReturnTypeEnum.Xml;
                customHttpHeaders.Accept = "text/xml";

                if (!string.IsNullOrEmpty(SystemUser))
                {
                    customHttpHeaders.AddTaxxorUserInformation(SystemUser);
                }


                if (UriLogEnabled)
                {
                    if (!UriLogBackend.Contains(mappingServiceApiUri)) UriLogBackend.Add(mappingServiceApiUri);
                }

                var xmlMappingServiceResult = await RestRequest<XmlDocument>(RequestMethodEnum.Post, mappingServiceApiUri, xmlStructuredDataQuery.OuterXml.ToString(), customHttpHeaders, 1800000, true);


                if (debugRoutine)
                {
                    Console.WriteLine("**** BULK RETRIEVE SDE VALUES ****");
                    Console.WriteLine(PrettyPrintXml(xmlMappingServiceResult));
                    Console.WriteLine("*********************************");
                }

                // - Return an error if the bulk request was unsuccessful
                if (XmlContainsError(xmlMappingServiceResult))
                {
                    return new TaxxorReturnMessage(false, "500-requesterror", $"xmlMappingServiceResult: {xmlMappingServiceResult.OuterXml}");
                }

                //
                // => Convert the response so that we only use the localisedvalue going forward
                // 

                // - Remove the standard value nodes
                var nodeListValuesToRemove = xmlMappingServiceResult.SelectNodes("/response/item/value");
                RemoveXmlNodes(nodeListValuesToRemove);

                // - Transform localisedvalue into value nodes and @locale into @lang
                var nodeListToRename = xmlMappingServiceResult.SelectNodes("/response/item/localisedvalue");
                foreach (XmlNode nodeLocalisedValue in nodeListToRename)
                {
                    var nodeValue = xmlMappingServiceResult.CreateElementWithText("value", nodeLocalisedValue.InnerText);
                    nodeValue.SetAttribute("lang", nodeLocalisedValue.GetAttribute("locale"));
                    nodeLocalisedValue.ParentNode.AppendChild(nodeValue);
                }
                RemoveXmlNodes(nodeListToRename);


                // - Return the result
                if (retrieveMultiple)
                {
                    return new TaxxorReturnMessage(true, "Successfully retrieved multiple structured data element values", xmlMappingServiceResult);
                }
                else
                {
                    var nodeItem = xmlMappingServiceResult.SelectSingleNode($"/response/item[@id={GenerateEscapedXPathString(factGuid)}]");
                    var itemRetrieveResult = (nodeItem.GetAttribute("result") ?? "unknown").ToLower();
                    var syncResult = _sdeRetrieveResultToSyncStatus(itemRetrieveResult);
                    if (syncResult == "200-ok" || syncResult == "201-nodatasource")
                    {
                        // Response is depending on the language that we are requesting
                        if (valueLang == null)
                        {
                            // Return the values in a JSON format
                            dynamic jsonData = new ExpandoObject();
                            jsonData.values = new List<ExpandoObject>();
                            foreach (XmlNode nodeValue in nodeItem.SelectNodes("value"))
                            {
                                dynamic valueResult = new ExpandoObject();
                                valueResult.lang = nodeValue.GetAttribute("lang");
                                valueResult.value = nodeValue?.InnerText ?? "";
                                jsonData.values.Add(valueResult);
                            }

                            var json = (string)ConvertToJson(jsonData);

                            return new TaxxorReturnMessage(true, "Successfully retrieved structured data element value", json, "");
                        }
                        else
                        {
                            // Return the value directly as a string
                            var itemValue = nodeItem.SelectSingleNode($"value[@lang='{valueLang}']")?.InnerText ?? "";
                            return new TaxxorReturnMessage(true, "Successfully retrieved structured data element value", itemValue, "");
                        }
                    }
                    else
                    {
                        var debugInformation = "";
                        debugInformation = xmlMappingServiceResult.SelectSingleNode($"/response/item[@id={GenerateEscapedXPathString(factGuid)}]/message")?.InnerText ?? "";
                        return new TaxxorReturnMessage(false, syncResult, debugInformation);
                    }
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was an error retrieving the SDE value");
                return new TaxxorReturnMessage(false, "500-overallerror", $"error: {ex}");
            }

        }

        /// <summary>
        /// Generates a formatted string displaying the results of the structured data sync in the UI
        /// </summary>
        /// <param name="snapshotId"></param>
        /// <param name="syncStats"></param>
        /// <param name="xmlMetadataCache"></param>
        /// <returns></returns>
        public static string _renderStructuredDataSyncResponseMessage(string projectId, string snapshotId, SdeSyncStatictics syncStats, XmlDocument xmlMetadataCache)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";
            var writeSuccessLog = false;


            if (debugRoutine)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(syncStats, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText($"{logRootPathOs}/syncstats.json", json);
            }

            var responseMessage = $"Structured data elements sync report:\n\n\n";
            // responseMessage += $"Snapshot ID: {snapshotId}\n";
            responseMessage += $"Total structured data elements found in document: {syncStats.StructuredDataElementsFound.ToString()} (unique: {syncStats.StructuredDataElementsUnique.ToString()})\n";
            // responseMessage += $"Structured data elements updated with a new value: {syncStats.StructuredDataElementsUpdated.ToString()}\n";

            // TODO: temporary fix -> we need to check how the numbers are calculated
            if ((syncStats.StructuredDataElementsFound - syncStats.StructuredDataElementWithoutUpdate) > 0) responseMessage += $"Structured data elements updated with a new value: {(syncStats.StructuredDataElementsFound - syncStats.StructuredDataElementWithoutUpdate).ToString()}\n"; // TODO: Quick fix -> somehow the StructuredDataElementsUpdated does not seem to be correctly updated
            if (syncStats.StructuredDataElementWithoutUpdate <= syncStats.StructuredDataElementsFound) responseMessage += $"Structured data elements without need to update: {syncStats.StructuredDataElementWithoutUpdate.ToString()}\n";
            responseMessage += "\n";

            responseMessage += $"Sync summary:\n";
            responseMessage += $"- Success count: {syncStats.SyncOk.Count}\n";
            responseMessage += $"- Warning count: {syncStats.SyncWarning.Count}\n";
            responseMessage += $"- Error count: {syncStats.SyncError.Count}\n\n";

            // Add the logs
            var logContent = "";
            responseMessage += $"Sync details:\n\n";
            if (syncStats.SyncWarning.Count == 0 && syncStats.SyncError.Count == 0)
            {
                if (syncStats.StructuredDataElementsUpdated > 0)
                {
                    if (writeSuccessLog)
                    {
                        logContent = _generateStructuredDataSyncTypeMessage(syncStats.SyncOkConsolidated, xmlMetadataCache);
                        responseMessage += $"* Success log:\n{logContent}\n### ";
                    }

                    // We successfully synced structured data elements
                    responseMessage += $"Synchonization was successful - use a track changes PDF to view the updates";
                }
                else
                {
                    // There was nothing to sync
                    responseMessage += $"Nothing to report - no structured data elements were updated";
                }
            }
            else
            {
                if (syncStats.SyncOk.Count > 0 && writeSuccessLog)
                {
                    logContent = _generateStructuredDataSyncTypeMessage(syncStats.SyncOkConsolidated, xmlMetadataCache);
                    responseMessage += $"* Success log:\n{logContent}";
                }
                if (syncStats.SyncWarning.Count > 0)
                {
                    logContent = _generateStructuredDataSyncTypeMessage(syncStats.SyncWarningConsolidated, xmlMetadataCache);
                    responseMessage += $"* Warning log:\n{logContent}";
                }
                if (syncStats.SyncOk.Count > 0)
                {
                    logContent = _generateStructuredDataSyncTypeMessage(syncStats.SyncErrorConsolidated, xmlMetadataCache);
                    responseMessage += $"* Error log:\n{logContent}";
                }

            }

            return responseMessage;

            /// <summary>
            /// Generates a message to use in the web client
            /// </summary>
            /// <param name="consolidatedSyncResults"></param>
            /// <param name="xmlMetadataCache"></param>
            /// <returns></returns>
            string _generateStructuredDataSyncTypeMessage(Dictionary<string, List<SdeItem>> consolidatedSyncResults, XmlDocument xmlMetadataCache)
            {

                var message = new StringBuilder();
                foreach (var datareferenceSyncResult in consolidatedSyncResults)
                {
                    var dataReference = datareferenceSyncResult.Key;
                    var sdeItems = datareferenceSyncResult.Value;
                    if (sdeItems.Count > 0)
                    {
                        message.Append($"<div class=\"tx-sdesyncdetails\" data-ref=\"{dataReference}\">");
                        var sectionTitle = RetrieveSectionTitleFromMetadataCache(xmlMetadataCache, dataReference, projectId, "")?.Trim();

                        var sectionDisplayName = sectionTitle;
                        if (string.IsNullOrEmpty(sectionDisplayName))
                        {
                            sectionDisplayName = dataReference;
                        }
                        else
                        {
                            if (sectionDisplayName.Contains("\n") || sectionDisplayName.Contains("{"))
                            {
                                sectionDisplayName = dataReference;
                            }
                        }

                        message.AppendLine($"<a class=\"title\" href=\"#\">{sectionDisplayName}</a>:");

                        for (int i = 0; i < sdeItems.Count; i++)
                        {
                            var sdeItem = sdeItems[i];
                            message.Append($"<a data-factid=\"{sdeItem.Id}\" target=\"_blank\" href=\"#\">{sdeItem.Value.Trim()}</a> - ({sdeItem.SyncStatus})");

                            if (i < sdeItems.Count - 1) message.Append(", ");
                        }
                        message.Append("</div>");
                        message.Append("\n");
                    }

                }

                return message.ToString();
            }
        }



        /// <summary>
        /// Attempts to find the internal entry in a mapping cluster
        /// </summary>
        /// <param name="nodeFact"></param>
        /// <returns></returns>
        public static XmlNode? _getInternalEntry(XmlNode nodeFact, string? factId = null)
        {
            XmlNodeList? nodeListInternalEntries = null;
            XmlNodeList? nodeListExcelEntries = null;

            var factIdSelector = (factId == null) ? "" : $" and mapping/text()={GenerateEscapedXPathString(factId)}";

            // First attempt to locate an internal entry
            nodeListInternalEntries = nodeFact.SelectNodes($"entry[@scheme='Internal'{factIdSelector}]");
            if (nodeListInternalEntries.Count == 0)
            {
                // As a fallback, try to locate an Excel entry
                // appLogger.LogInformation($"Unable to locate internal entry in mapping cluster");
                nodeListExcelEntries = nodeFact.SelectNodes($"entry[@scheme='Excel_Named_Range'{factIdSelector}]");
                if (nodeListExcelEntries.Count > 0)
                {
                    return nodeListExcelEntries.Item(0);
                }
                else
                {
                    var factIdToReport = _getFactIdFromCluster(nodeFact, factId);
                    var articleId = GetArticleId(nodeFact);
                    var logMessage = (articleId == "unknown") ? $"" : $"articleId: '{articleId}'";
                    var tableId = GetTableId(nodeFact);
                    logMessage += (tableId == "unknown") ? $"" : $", tableId: '{tableId}'";
                    logMessage += string.IsNullOrEmpty(factIdToReport) ? "" : (string.IsNullOrEmpty(logMessage)) ? ("factId: " + factIdToReport) : (", factId: " + factIdToReport);

                    appLogger.LogError($"Could not find an internal or excel entry in the mapping cluster. ({logMessage})");
                    return null;
                }
            }
            else
            {
                if (nodeListInternalEntries.Count > 1)
                {
                    var factIdToReport = _getFactIdFromCluster(nodeFact, factId);
                    var articleId = GetArticleId(nodeFact);
                    var logMessage = (articleId == "unknown") ? $"" : $"articleId: '{articleId}'";
                    var tableId = GetTableId(nodeFact);
                    logMessage += (tableId == "unknown") ? $"" : $", tableId: '{tableId}'";
                    logMessage += string.IsNullOrEmpty(factIdToReport) ? "" : (string.IsNullOrEmpty(logMessage)) ? ("factId: " + factIdToReport) : (", factId: " + factIdToReport);

                    appLogger.LogWarning($"Multiple internal entries found. ({logMessage})");
                }
                return nodeListInternalEntries.Item(0);
            }

            string GetArticleId(XmlNode nodeFact)
            {
                return nodeFact?.SelectSingleNode("ancestor::article")?.GetAttribute("id") ?? "unknown";
            }

            string GetTableId(XmlNode nodeFact)
            {
                return nodeFact?.SelectSingleNode("ancestor::table")?.GetAttribute("id") ?? "unknown";
            }
        }

        /// <summary>
        /// Helper function used in error reporting to provide a pointer to the factId that is causing issues
        /// </summary>
        /// <param name="nodeFact"></param>
        /// <param name="factId"></param>
        /// <returns></returns>
        private static string? _getFactIdFromCluster(XmlNode nodeFact, string? factId = null)
        {
            var factIdToReport = factId;
            if (string.IsNullOrEmpty(factIdToReport))
            {
                factIdToReport = nodeFact.GetAttribute("requestId");
                if (string.IsNullOrEmpty(factIdToReport))
                {
                    factIdToReport = nodeFact.GetAttribute("id");
                    if (string.IsNullOrEmpty(factIdToReport))
                    {
                        factIdToReport = nodeFact.ParentNode.GetAttribute("id");
                    }
                }
            }
            return factIdToReport;
        }

        /// <summary>
        /// Attempts to find the source entry in a mapping cluster
        /// </summary>
        /// <param name="nodeFact"></param>
        /// <param name="structuredDataScheme">The external mapping scheme that we need to find</param>
        /// <returns></returns>
        public static XmlNode? _getSourceEntry(XmlNode nodeFact, string structuredDataScheme = "EFR")
        {
            return nodeFact.SelectSingleNode($"entry[@scheme='{structuredDataScheme}']");
        }

        /// <summary>
        /// Attempts to retrieve the target entry in a mapping cluster
        /// </summary>
        /// <param name="nodeFact"></param>
        /// <param name="xbrlScheme"></param>
        /// <returns></returns>
        public static XmlNode _getTargetEntry(XmlNode nodeFact, string xbrlScheme = "PHG2019")
        {
            return nodeFact.SelectSingleNode($"entry[@scheme='{xbrlScheme}']");
        }

        /// <summary>
        /// Tests if an internal mapping entry has been explicitly set
        /// </summary>
        /// <param name="nodeFact"></param>
        /// <returns></returns>
        private static bool _hasInternalTagging(XmlNode nodeFact)
        {
            var nodeInternalEntry = _getInternalEntry(nodeFact);

            if (AttributeExists(nodeInternalEntry, "displayOption") && AttributeExists(nodeInternalEntry, "datatype"))
            {
                if (nodeInternalEntry.GetAttribute("displayOption") == "default" && nodeInternalEntry.GetAttribute("datatype") == "pure")
                {
                    return false;
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Tests if a source mapping has been explicitly set
        /// </summary>
        /// <param name="nodeFact"></param>
        /// <returns></returns>
        private static bool _hasSourceTagging(XmlNode nodeFact)
        {
            var nodeSourceEntry = _getSourceEntry(nodeFact);
            var nodeMapping = nodeSourceEntry.SelectSingleNode("mapping");
            if (nodeMapping != null)
            {
                if (nodeMapping.InnerText.Contains("dataset"))
                {
                    if (!nodeMapping.InnerText.Contains("\"dataset\":null") && !nodeMapping.InnerText.Contains("\"dataset\" : null") && !nodeMapping.InnerText.Contains("\"dataset\": null"))
                    {
                        return true;
                    }
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Retrieves the structured data elements from a section XML file
        /// </summary>
        /// <param name="xmlNode"></param>
        /// <param name="documentIsDataFile"></param>
        /// <param name="lang"></param>
        /// <returns></returns>
        private static XmlNodeList _retrieveStructuredDataElements(XmlNode xmlNode, bool documentIsDataFile = true, string lang = "all")
        {
            var langFilter = (lang.ToLower() == "all") ? "" : $"[@lang='{lang}']";
            var xPath = $".{_retrieveStructuredDataElementsBaseXpath()}";
            // appLogger.LogDebug($"- xPath: {xPath}");
            return xmlNode.SelectNodes(xPath);
        }

        /// <summary>
        /// Retrieves the structured data elements from a section XML file
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="documentIsDataFile"></param>
        /// <returns></returns>
        private static XmlNodeList? _retrieveStructuredDataElements(XmlDocument xmlDocument, bool documentIsDataFile = true, string lang = "all")
        {
            var langFilter = (lang.ToLower() == "all") ? "" : $"[@lang='{lang}']";
            var xPath = $"{((documentIsDataFile) ? ("/data/content" + langFilter + "/*") : "//article")}{_retrieveStructuredDataElementsBaseXpath()}";
            // appLogger.LogDebug($"- xPath: {xPath}");

            // TODO: Add the level 2 block tags here!

            return xmlDocument.SelectNodes(xPath);
        }

        /// <summary>
        /// Renders the base xpath to select Structured Data Elements from the data files
        /// </summary>
        /// <returns></returns>
        private static string _retrieveStructuredDataElementsBaseXpath()
        {
            return "//*[@data-fact-id and not(ancestor::table[@data-workbookreference and not(@data-instanceid)]) and not(contains(@class, 'xbrl-level-2'))]";
        }

        /// <summary>
        /// Normalizes the result of the Structured Data Value retrieval routine from the Taxxor Mapping Service into a normalized string that we can use in the content
        /// </summary>
        /// <param name="retrieveResult"></param>
        /// <returns></returns>
        private static string _sdeRetrieveResultToSyncStatus(string retrieveResult)
        {
            switch (retrieveResult.ToLower())
            {
                case "ok":
                    return "200-ok";

                case "nodatasource":
                    return "201-nodatasource";

                case "notfound":
                case "mappingnotfound":
                    return $"404-{retrieveResult.ToLower()}";

                default:
                    return $"500-{retrieveResult.ToLower().Replace(" ", "")}";
            }
        }


    }
}