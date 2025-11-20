using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities involved in saving editor data to the server
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Load XML data into the editor (text, config or other XML data)
        /// </summary>
        public static async Task SaveFilingComposerData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted variables
            var projectId = request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var versionId = request.RetrievePostedValue("vid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var dataType = request.RetrievePostedValue("type", true, ReturnTypeEnum.Xml);
            var did = request.RetrievePostedValue("did", true, ReturnTypeEnum.Xml);
            projectVars.editorContentType = request.RetrievePostedValue("ctype", true, ReturnTypeEnum.Xml);
            projectVars.reportTypeId = request.RetrievePostedValue("rtype", true, ReturnTypeEnum.Xml);
            var contentLanguage = context.Request.RetrievePostedValue("oclang", RegexEnum.None, true, ReturnTypeEnum.Json, "en");

            // Retrieve the (xml) data that we want to store
            var dataToStore = request.RetrievePostedValue("data", RegexEnum.None, true, ReturnTypeEnum.Xml);

            // Console.WriteLine("");
            // Console.WriteLine("***************");
            // Console.WriteLine($"projectVars.editorId: {projectVars.editorId}");
            // Console.WriteLine($"projectVars.reportTypeId: {projectVars.reportTypeId}");
            // Console.WriteLine("***************");
            // Console.WriteLine("");

            // if (debugRoutine) Console.WriteLine($"** Received Data: {dataToStore}");

            if (ValidateCmsPostedParameters(projectId, versionId, dataType) && did != null)
            {

                TaxxorReturnMessage? postProcessResult = null;
                string xmlFilePathOs = "";

                // Retrieve the filing hierarchy file from the metadata object that we have created in the middleware
                XmlDocument xmlFilingDocumentHierarchy = new XmlDocument();
                var hierarchyPathOs = CalculateHierarchyPathOs(reqVars, projectId, versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, projectVars.outputChannelType, projectVars.outputChannelVariantId, contentLanguage);
                try
                {
                    xmlFilingDocumentHierarchy.Load(hierarchyPathOs);
                }
                catch (Exception ex)
                {
                    HandleError(ReturnTypeEnum.Xml, GenerateErrorXml("Could not validate load output channel hierarchy", $"hierarchyPathOs: {hierarchyPathOs}, error: {ex}, projectId: {projectId}, versionId: {versionId}, dataType: {dataType}, stack-trace: {GetStackTrace()}"));
                }


                // Calculate the path of the file on the disk
                switch (dataType)
                {
                    case "text":
                        xmlFilePathOs = RetrieveInlineFilingComposerXmlPathOs(reqVars, projectVars, did, debugRoutine);
                        break;

                    default:
                        HandleError(ReturnTypeEnum.Xml, "Saving this type of data not supported", $"dataType: {dataType}, stack-trace: {GetStackTrace()}");
                        break;
                }

                // Retrieve the posted document and store it on the disk
                XmlDocument xmlDocument = new XmlDocument();
                try
                {
                    xmlDocument.LoadXml(dataToStore);
                }
                catch (Exception ex)
                {
                    HandleError(ReturnTypeEnum.Xml, "There was a problem loading the data", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

                if (debugRoutine)
                {
                    await xmlDocument.SaveAsync($"{logRootPathOs}/_editor-saved.xml", false, true);
                }

                // Process the retrieved xml file
                switch (projectVars.reportTypeId)
                {

                    default:
                        postProcessResult = await SaveXmlInlineFilingComposerData(xmlDocument, xmlFilePathOs, projectVars);
                        break;
                }

                if (!postProcessResult.Success) HandleError(ReturnTypeEnum.Xml, postProcessResult.Message, postProcessResult.DebugInfo);

                // Construct the commit message
                var linkname = RetrieveLinkName(did, xmlFilingDocumentHierarchy);
                if (string.IsNullOrEmpty(linkname))
                {
                    // Attempt to retrieve the linkname via the content that was posted
                    var nodeLinkName = xmlDocument.SelectSingleNode("//item[@id='" + did + "']/linknames/linkname[@lang='" + contentLanguage + "']");
                    if (nodeLinkName != null) linkname = nodeLinkName.InnerText;
                }
                // HandleError("Stopped on purpose");
                XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "u"; // Update
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = ((string.IsNullOrEmpty(linkname)) ? "" : linkname); // Linkname
                xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", projectVars.outputChannelVariantLanguage);
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = did; // Page ID
                var message = xmlCommitMessage.DocumentElement.InnerXml;

                // Commit the result in the GIT repository
                var successfullyCommitted = CommitFilingData(message, ReturnTypeEnum.Xml, false);
                if (!successfullyCommitted)
                {
                    appLogger.LogWarning("Unable to commit the section data to the version control system");
                }

                // Handle success
                await context.Response.OK(GenerateSuccessXml("Successfully stored the data", postProcessResult.DebugInfo), ReturnTypeEnum.Xml, true);
            }
            else
            {
                HandleError(ReturnTypeEnum.Xml, GenerateErrorXml("Could not validate posted data", $"projectId: {projectId}, versionId: {versionId}, dataType: {dataType}, stack-trace: {GetStackTrace()}"));
            }
        }

        /// <summary>
        /// Stores an XML data fragment of the inline filing composer
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="xmlFilePathOs"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> SaveXmlInlineFilingComposerData(XmlDocument xmlDocument, string xmlFilePathOs, ProjectVariables projectVars)
        {
            var rebuildStructuredDataCacheOnSave = false;
            var removeUnusedCacheEntries = false;

            var syncStructuredDataInAnAsycClue = true;
            var isSyncRunning = SystemState.ErpImport.IsRunning || SystemState.SdsSync.IsRunning;
            // var isSyncRunning = SystemState.SdsSync.IsRunning;

            var projectId = projectVars.projectId;
            var nodeCmsProjectExternalDataSets = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']/repositories/external_data/sets");
            if (nodeCmsProjectExternalDataSets == null) return new TaxxorReturnMessage(false, $"There was an error syncing the external tables for project with ID: {projectId}", $"error: 'Could not locate external data set', stack-trace: {GetStackTrace()}");
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var error = false;
            var errorMessage = "";
            var errorDetails = "";
            var langSavedContent = projectVars.outputChannelVariantLanguage;


            // HandleError(ReturnTypeEnum.Xml, "Thrown on purpose");

            //
            // => External Data Tables (make sure that the cache files are present and potentially created if the content saved contains tables that are not in the cache yet)
            //

            // Locate a default workbook ID
            var containsOldStyleTables = xmlDocument.SelectNodes("//table[@data-syncstatus]").Count > 0;
            var nodeDefaultExternalDataSet = nodeCmsProjectExternalDataSets.SelectSingleNode("set");
            if (nodeDefaultExternalDataSet == null && containsOldStyleTables)
            {
                appLogger.LogWarning($"Could not retrieve cached tables from server because no external dataset has been defined for project with ID {projectId} for SaveXmlInlineFilingComposerData() in section {Path.GetFileName(xmlFilePathOs)}");
            }
            else
            {
                // Test if we need to retrieve cached versions of external tables
                var workbookId = GetAttribute(nodeDefaultExternalDataSet, "id");
                var nodeListExternalTables = xmlDocument.SelectNodes("/data/content/*//div[contains(@class, 'external-table')]/table");
                foreach (XmlNode nodeExternalTable in nodeListExternalTables)
                {
                    var externalTableId = GetAttribute(nodeExternalTable, "id");
                    if (string.IsNullOrEmpty(externalTableId))
                    {
                        appLogger.LogWarning($"Unable to find the table ID for this external table. table html: {TruncateString(nodeExternalTable.OuterXml, 400)}");
                        appLogger.LogWarning($"Not processing this table any further");
                        continue;
                    }
                    else
                    {
                        externalTableId = CalculateBaseExternalTableId(externalTableId);
                    }

                    var cachedExternalHtmlTableFileName = CalculateCachedExternalTableFileName(externalTableId);
                    var cachedExternalHtmlTableFilePathOs = $"{Path.GetDirectoryName(xmlFilePathOs)}/{cachedExternalHtmlTableFileName}";

                    if (!File.Exists(cachedExternalHtmlTableFilePathOs))
                    {
                        // Retrieve the workbook ID from the workbook reference
                        var currentWorkbookId = workbookId;
                        var workbookReference = GetAttribute(nodeExternalTable, "data-workbookreference");
                        if (string.IsNullOrEmpty(workbookReference))
                        {
                            appLogger.LogWarning($"Could not retrieve workbook reference, so we will be using the default workbook ID");
                        }
                        else
                        {
                            currentWorkbookId = MapWorkbookReferenceToWorkbookId(projectId, workbookReference, "");
                        }

                        if (!string.IsNullOrEmpty(currentWorkbookId))
                        {
                            // Retrieve the source content from the server
                            var cachedExternalHtmlTable = Taxxor.ConnectedServices.GenericDataConnector.RetrieveExternalTable(projectId, currentWorkbookId, externalTableId, true).GetAwaiter().GetResult();
                            if (XmlContainsError(cachedExternalHtmlTable))
                            {
                                appLogger.LogError($"Could not retrieve external html table. projectId: {projectId}, datareference: {Path.GetFileName(xmlFilePathOs)}, externalTableId: {externalTableId}, response: {cachedExternalHtmlTable.OuterXml}");
                            }
                            else
                            {
                                // Save the cached file on the disk
                                cachedExternalHtmlTable.Save(cachedExternalHtmlTableFilePathOs);
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Could not retrieve a new cached version of external table with ID {externalTableId} because the workbook ID coold not be determined");
                        }

                    }
                }
            }

            //
            // => Structured Data Elements (update the cache with new entries)
            //
            var sdeCacheUpdateResult = UpdateStructuredDataElementsCacheFile(xmlDocument, xmlFilePathOs, projectId, langSavedContent, rebuildStructuredDataCacheOnSave, removeUnusedCacheEntries, debugRoutine);
            if (!sdeCacheUpdateResult.Success)
            {
                appLogger.LogWarning($"Unable to update SDE cache file for dataReference: {Path.GetFileName(xmlFilePathOs)}. {sdeCacheUpdateResult}");
            }


            //
            // => Remove elements that have been explicitly marked with @data-stripcontentonsave
            //
            xmlDocument.ReplaceContent(RemoveTemporaryElements(xmlDocument, projectVars), true);

            //
            // => Remove potentially injected linked elements before saving the file
            //
            xmlDocument.ReplaceContent(RemoveInjectedSectionElements(xmlDocument, projectVars), true);

            //
            // => Remove potentially injected configuration links before saving the file
            //
            xmlDocument.ReplaceContent(RemoveInjectedConfigurationLinkElements(xmlDocument, projectVars), true);

            //
            // => Mark dates in table cells and headers
            //
            var markDatesResult = MarkDatesInTableCells(projectId, xmlDocument, projectVars.outputChannelVariantLanguage);

            // Log some content if needed
            if (markDatesResult.DebugContent.Length > 0 && debugRoutine)
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
            if (!XmlContainsError(markDatesResult.XmlData))
            {
                xmlDocument.ReplaceContent(markDatesResult.XmlData, true);
            }
            else
            {
                appLogger.LogError($"Could not load XML with date markings as the transformation returned: {markDatesResult.XmlData.OuterXml}");
            }

            //
            // => Restore original values of dynamically replaced paths to prevent that we store the resolved ones
            //
            xmlDocument.ReplaceContent(DynamicPlaceholdersRestore(xmlDocument, projectVars), true);


            //
            // => Post process the images
            //
            xmlDocument.ReplaceContent(ProcessImagesOnSave(xmlDocument, projectVars), true);

            //
            // => Cleanup the document before storing it on the disk
            //
            xmlDocument.ReplaceContent(CleanupDataReference(xmlDocument, projectVars), true);

            //
            // => Custom logic
            //
            try
            {
                xmlDocument.ReplaceContent(CustomFilingComposerDataSave(xmlDocument, projectVars, xmlFilePathOs), true);
            }
            catch (Exception ex)
            {
                error = true;
                errorMessage = "Could not save data (custom logic error)";
                errorDetails = $"xmlSectionPathOs: {xmlFilePathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                if (error) appLogger.LogError(ex, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, errorDetails);
            }

            //
            // => Add this section to the cue so that the background scheduler can pick it up and retrieve all the Structured Data Element values from the Structured Data Store
            //
            if (syncStructuredDataInAnAsycClue && isSyncRunning)
            {
                if (!SyncSections.TryAdd(md5($"{projectId}{xmlFilePathOs}"), [projectId, Path.GetFileName(xmlFilePathOs), "0"]))
                {
                    appLogger.LogInformation($"Unable to add the section {Path.GetFileName(xmlFilePathOs)} to the sync queue for project {projectId} because it is already in the queue");
                }
            }

            //
            // => Store it on the disk and commit the contents to the repository
            //
            if (!SaveXmlDocumentSync(xmlDocument, xmlFilePathOs)) return new TaxxorReturnMessage(false, "Could not save your XML data", $"Path: {xmlFilePathOs}, stack-trace: {GetStackTrace()}");

            //
            // => Update the metadata entry in the CMS Metadata object
            //
            await UpdateCmsMetadataEntry(projectId, Path.GetFileName(xmlFilePathOs));

            return new TaxxorReturnMessage(true, "Successfully saved the data", $"xmlFilePathOs: {xmlFilePathOs}");
        }

        /// <summary>
        /// Helper function to add attributes with a namespace to an element in XmlDocument
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="xmlElement"></param>
        /// <param name="prefix"></param>
        /// <param name="uri"></param>
        /// <param name="attributeName"></param>
        /// <param name="attributeValue"></param>
        public static void AddAttributeNamespace(XmlDocument xmlDocument, XmlElement xmlElement, string prefix, string uri, string attributeName, string attributeValue)
        {
            XmlAttribute attrType = xmlDocument.CreateAttribute(prefix, attributeName, uri);
            attrType.Value = attributeValue;
            xmlElement.Attributes.Append(attrType);
        }


        /// <summary>
        /// Removes elements from the content that have been marked with @data-stripcontentonsave='true'
        /// </summary>
        /// <param name="xmlSourceDocument"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static XmlDocument RemoveTemporaryElements(XmlDocument xmlSourceDocument, ProjectVariables projectVars)
        {
            // Global variables
            string? lang = projectVars.outputChannelVariantLanguage;

            // Check if this is a regular article or a website data file
            var xpathNodeToRemove = $"/data/content[@lang='{lang}']/*//*[@data-stripcontentonsave]";

            // Remove
            var nodeListPotentialElementsToRemove = xmlSourceDocument.SelectNodes(xpathNodeToRemove);
            foreach (XmlNode nodePotentialElementToRemove in nodeListPotentialElementsToRemove)
            {
                if (nodePotentialElementToRemove.GetAttribute("data-stripcontentonsave") == "true")
                {
                    RemoveXmlNode(nodePotentialElementToRemove);
                }
                else
                {
                    // Remove the attribute only
                    RemoveAttribute(nodePotentialElementToRemove, "data-stripcontentonsave");
                }
            }

            return xmlSourceDocument;
        }

        /// <summary>
        /// Removes unwanted elements and attributes from section filing data before they are stored in the project data store
        /// </summary>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static XmlDocument CleanupDataReference(XmlDocument xmlFilingContentSourceData, ProjectVariables projectVars = null)
        {
            //
            // => Cleanup the new data from attributes which are needed for the editor 
            //

            // - Using C# because XSLT strips CDATA from the XML structure we want to save
            List<string> attributesToCleanup = new List<string>();
            attributesToCleanup.Add("data-editable");
            attributesToCleanup.Add("data-fixture");
            attributesToCleanup.Add("data-syncstatus");
            attributesToCleanup.Add("data-temp-ismissing");
            attributesToCleanup.Add("data-mce-id");
            attributesToCleanup.Add("data-link-error");
            attributesToCleanup.Add("contenteditable");
            attributesToCleanup.Add("data-tangeloid");
            attributesToCleanup.Add("data-ce-tag");
            attributesToCleanup.Add("data-friendlyid");

            foreach (var attributeName in attributesToCleanup)
            {
                var nodeListToCleanup = xmlFilingContentSourceData.SelectNodes($"//*[@{attributeName}]");
                foreach (XmlNode nodeToCleanup in nodeListToCleanup)
                {
                    var nodeName = nodeToCleanup.LocalName;
                    if (attributeName == "data-link-error" && nodeName == "em")
                    {
                        appLogger.LogInformation($"Do not cleanup em node specially marked with @data-link-error to mark removed link");
                    }
                    else
                    {
                        RemoveAttribute(nodeToCleanup, attributeName);
                    }
                }
            }


            //
            // => Cleanup using XSLT for the more complex constructions
            //
            if (xmlFilingContentSourceData.OuterXml.ToLower().Contains("[cdata["))
            {
                appLogger.LogError($"Unable to cleanup filing section data, because it contains CDATA content. projectVars: {((projectVars != null) ? projectVars.DumpToString() : "not available")}");
            }
            else
            {
                // - Exclude specific data references from the cleanup routine
                var nodeTaxesPaidCountryDetailPageWrapper = xmlFilingContentSourceData.SelectSingleNode("//div[contains(@class, 'taxes-paid-country')]");
                var nodeArticle20fCoverPage = xmlFilingContentSourceData.SelectSingleNode("//article[@data-articletype='20fcover' or @data-template='sec-coverpage' or @id='1201713-front-cover1' or @id='toc-20f' or @id='toc' or @data-description='front-cover']");

                if (nodeTaxesPaidCountryDetailPageWrapper == null && nodeArticle20fCoverPage == null)
                {
                    xmlFilingContentSourceData = TransformXmlToDocument(xmlFilingContentSourceData, "xsl_datareference-cleanup");
                }
            }

            return ValidHtmlVoidTags(xmlFilingContentSourceData);
        }


    }
}