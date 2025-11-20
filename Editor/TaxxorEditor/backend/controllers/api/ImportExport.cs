using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Imports the SDE mappings and source XML from another project
        /// </summary>
        /// <param name="sourceProjectId"></param>
        /// <param name="targetProjectId"></param>
        /// <param name="dataReferences"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> ImportFilingDataAndMappings(string sourceProjectId, string targetProjectId, List<string> dataReferences)
        {
            try
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");
                var debugInfo = $"sourceProjectId: {sourceProjectId}, dataReferences: {string.Join(',', dataReferences.ToArray())}";


                foreach (string dataReference in dataReferences)
                {
                    //
                    // => Load the source filing content data
                    //
                    var xmlFilingContentTargetData = await DocumentStoreService.FilingData.Load<XmlDocument>(targetProjectId, $"/textual/{dataReference}", "cmsdataroot", true, false);
                    var targetFilingContentExists = true;
                    if (XmlContainsError(xmlFilingContentTargetData)) targetFilingContentExists = false;

                    // Determines the method for importing the mapping clusters
                    // 'true' forces the process to push the source mappings to the target even if they already exist
                    var overrideMappingClusters = false;
                    if (targetFilingContentExists)
                    {
                        appLogger.LogInformation($"{dataReference} exists in target project (id: {targetProjectId})");
                        overrideMappingClusters = true;
                    }

                    //
                    // => Load the source filing content data
                    //
                    var xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>(sourceProjectId, $"/textual/{dataReference}", "cmsdataroot", true, false);
                    if (XmlContainsError(xmlFilingContentSourceData)) return new TaxxorReturnMessage(xmlFilingContentSourceData);
                    // debugInfo += $", xml: {TruncateString(xmlFilingContentSourceData.OuterXml, 1000)}";


                    //
                    // => Find the mapping id's in the source data
                    //
                    var factIds = new List<string>();
                    var nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlFilingContentSourceData, false);
                    foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                    {
                        var factId = nodeStructuredDataElement.GetAttribute("data-fact-id").Replace("\n", "");
                        if (!factIds.Contains(factId)) factIds.Add(factId);
                    }
                    appLogger.LogDebug($"+++ Found {factIds.Count} structured data elements to import +++");
                    debugInfo += $", factIds.Count: {factIds.Count}";


                    if (factIds.Count == 0)
                    {
                        appLogger.LogInformation($"dataReference: {dataReference} does not contain any SDE's, so we only import the source data and leave it at that");

                        //
                        // => Save the imported source data file for this project
                        //
                        var xmlSaveResult = await DocumentStoreService.FilingData.Save<XmlDocument>(xmlFilingContentSourceData, targetProjectId, $"/textual/{dataReference}", "cmsdataroot", debugRoutine, false);
                        if (XmlContainsError(xmlSaveResult)) return new TaxxorReturnMessage(xmlSaveResult);
                    }
                    else
                    {
                        //
                        // => Remove existing mappingclusters for the fact id's
                        //

                        //
                        // => Import mappings in the mapping store
                        //
                        var importMappingClustersResult = await ImportMappingClusters(sourceProjectId, targetProjectId, factIds, overrideMappingClusters, debugRoutine);
                        if (!importMappingClustersResult.Success)
                        {
                            return importMappingClustersResult;
                        }
                        else
                        {
                            appLogger.LogInformation($"importMappingClustersResult.Message: {importMappingClustersResult.Message}");
                            appLogger.LogInformation($"importMappingClustersResult.DebugInfo: {importMappingClustersResult.DebugInfo}");
                            debugInfo += $", mapping import details: {importMappingClustersResult.DebugInfo}";
                        }

                        //
                        // => Save the imported source data file for this project
                        //
                        var xmlSaveResult = await DocumentStoreService.FilingData.Save<XmlDocument>(xmlFilingContentSourceData, targetProjectId, $"/textual/{dataReference}", "cmsdataroot", debugRoutine, false);
                        if (XmlContainsError(xmlSaveResult)) return new TaxxorReturnMessage(xmlSaveResult);

                        //
                        // => Rebuild the EFR cache file
                        //
                        var dataToPost = new Dictionary<string, string>();
                        dataToPost.Add("projectid", targetProjectId);
                        dataToPost.Add("snapshotid", "1");
                        dataToPost.Add("dataref", dataReference);

                        var xmlRebuildSdeCacheResult = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorsyncstructureddatasnapshotperdataref", dataToPost, debugRoutine);
                        if (XmlContainsError(xmlRebuildSdeCacheResult)) return new TaxxorReturnMessage(xmlRebuildSdeCacheResult);
                    }
                }

                appLogger.LogInformation("!!! Finished importing mapping and filing data !!!");

                return new TaxxorReturnMessage(true, $"Successfully imported {dataReferences.Count} datareference{((dataReferences.Count > 0) ? "s" : "")} and mappings.", debugInfo);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error importing filing and mapping data", ex.ToString());
            }

        }


        /// <summary>
        /// Exports the section xml content
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ExportSectionXml(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var useRandomOutputFolder = true;

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var includeChildSectionsString = context.Request.RetrievePostedValue("includechildren", "(true|false)", false, reqVars.returnType, "false");
            var includeChildSections = (includeChildSectionsString == "true");
            var removeBrokenLinksString = request.RetrievePostedValue("removebrokenlinks", @"^(true|false)$", false, ReturnTypeEnum.Json, "false");
            var removeBrokenLinks = (removeBrokenLinksString == "true");
            var serveAsBinaryString = context.Request.RetrievePostedValue("serveasbinary", "(true|false)", false, reqVars.returnType, "false");
            var serveAsBinary = (serveAsBinaryString == "true");

            // Calculate the folder where we need to export to
            var tempFolderName = (useRandomOutputFolder) ? $"export_{CreateTimeBasedString("millisecond")}{RandomString(8, false)}" : $"export_{projectVars.projectId}-{projectVars.outputChannelVariantLanguage}";
            var tempFolderPathOs = $"{dataRootPathOs}/temp/{tempFolderName}";


            //
            // => Setup folder structure for package
            //
            await MessageToCurrentClient("DataImportExportProgress", "Create export folder structure");
            try
            {
                if (Directory.Exists(tempFolderPathOs)) DelTree(tempFolderPathOs);

                Directory.CreateDirectory(tempFolderPathOs);
            }
            catch (Exception ex)
            {
                // Handle the error
                HandleError("Could not create directory structure", $"tempFolderPathOs: {tempFolderPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
            }

            //
            // => Build up a list of broken links if we need to remove these on-export
            //
            var brokenLinkUris = new List<string>();
            if (removeBrokenLinks)
            {
                await MessageToCurrentClient("DataImportExportProgress", "Find problamatic links");

                // - Create the XSLT stylesheet cache
                SetupPdfStylesheetCache(reqVars);

                // - Retrieve complete XHTML of the output channel so we can find all the links that are broken
                var pdfHtml = await RetrieveLatestPdfHtml(projectVars, "all", false, false);
                var xmlFullPdfContent = new XmlDocument();
                xmlFullPdfContent.LoadXml(pdfHtml);

                if (debugRoutine) await xmlFullPdfContent.SaveAsync($"{logRootPathOs}/exportlinkcheck.xml");

                // - Create a unique list of broken link URI's
                var nodeListBrokenLinks = xmlFullPdfContent.SelectNodes("//a[(@data-link-type='section' or @data-link-type='note') and @data-link-error='missing-link-target']");
                foreach (XmlNode nodeBrokenLink in nodeListBrokenLinks)
                {
                    var brokenLinkUri = nodeBrokenLink.GetAttribute("href");
                    if (!string.IsNullOrEmpty(brokenLinkUri))
                    {
                        if (!brokenLinkUris.Contains(brokenLinkUri)) brokenLinkUris.Add(brokenLinkUri);
                    }
                }

                appLogger.LogInformation($"Found {brokenLinkUris.Count} broken links");
            }

            //
            // => Load the output channel hierarchy
            //
            await MessageToCurrentClient("DataImportExportProgress", "Retrieving outputchannel hierarchy");
            var outputChannelHierarchyId = RetrieveOutputChannelHierarchyMetadataId(projectVars);
            if (!projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyId))
            {
                HandleError("Could not find the output channel hierarchy", $"outputChannelHierarchyId: {outputChannelHierarchyId}, stack-trace: {GetStackTrace()}");
            }
            var xmlHierarchyDoc = projectVars.cmsMetaData[outputChannelHierarchyId].Xml;

            //
            // => Retrieve the item in the hierarchy that we want to work with
            //
            var nodeHierarchyItem = xmlHierarchyDoc.SelectSingleNode($"/items/structured//item[@id={GenerateEscapedXPathString(projectVars.did)}]");
            if (nodeHierarchyItem == null) HandleError("Could not locate the section you want to export in hierarchy", $"projectVars.did: {projectVars.did}, stack-trace: {GetStackTrace()}");

            //
            // => Render a list of section ID's that we need to export
            //
            var exportedItemsInformation = new List<SiteStructureItem>();
            exportedItemsInformation.Add(RetrieveHierarchyItemInformation(nodeHierarchyItem, null));

            if (includeChildSections)
            {
                var nodeListSubItems = nodeHierarchyItem.SelectNodes("descendant::item");
                foreach (XmlNode nodeItem in nodeListSubItems)
                {
                    exportedItemsInformation.Add(RetrieveHierarchyItemInformation(nodeItem, null));
                }
            }

            //
            // => Export the sections and store them on the disk
            //
            await MessageToCurrentClient("DataImportExportProgress", "Exporting sections");
            var initialExportedFilePathOs = "";
            var exportCounter = 0;
            foreach (var exportedItem in exportedItemsInformation)
            {
                await MessageToCurrentClient("DataImportExportProgress", $"* ({(exportCounter + 1)}/{exportedItemsInformation.Count}) Exporting {exportedItem.Linkname ?? exportedItem.Id ?? "unknown"}");


                //
                // => Retrieve the section content from the Taxxor Document Store
                //
                XmlDocument xmlFilingContent = new XmlDocument();
                xmlFilingContent = await RetrieveFilingComposerXmlData(projectVars.projectId, projectVars.editorId, "latest", exportedItem.Id, debugRoutine);
                var articleId = GetAttribute(xmlFilingContent.SelectSingleNode("//article"), "id") ?? "unknown-articleid";
                // appLogger.LogInformation(xmlFilingContent.OuterXml);

                //
                // => Deal with the broken links
                //
                if (removeBrokenLinks && brokenLinkUris.Count > 0)
                {
                    var articleType = xmlFilingContent.SelectSingleNode("//article")?.GetAttribute("data-articletype") ?? "unknown";

                    var nodeListLinks = xmlFilingContent.SelectNodes("//a[(@data-link-type='section' or @data-link-type='note')]");
                    foreach (XmlNode nodeLink in nodeListLinks)
                    {
                        var linkType = nodeLink.GetAttribute("data-link-type") ?? "unknown";
                        var targetIsNote = (linkType == "note");
                        var currentUri = nodeLink.GetAttribute("href") ?? "---loremipsumnevermatch---";
                        if (brokenLinkUris.Contains(currentUri))
                        {
                            // Links to notes in the financial tables need to disappear
                            if (articleType == "megatable" && targetIsNote && (nodeLink.SelectSingleNode("ancestor::table") != null))
                            {
                                RemoveXmlNode(nodeLink);
                            }
                            else
                            {
                                // Normal broken links: wrap the text into a <em/> element with an attribute @data-link-error='missing-link-target'
                                var nodeLinkPlaceholder = xmlFilingContent.CreateElementWithText("em", nodeLink.InnerText ?? "");
                                nodeLinkPlaceholder.SetAttribute("data-link-error", "missing-link-target");
                                nodeLinkPlaceholder.SetAttribute("style", "color: red;");
                                ReplaceXmlNode(nodeLink, (XmlNode)nodeLinkPlaceholder);
                            }
                        }
                    }

                }

                //
                // => Convert the XHTML to a new format
                //
                try
                {
                    XsltArgumentList xsltArgumentList = new XsltArgumentList();
                    xsltArgumentList.AddParam("projectid", "", projectVars.projectId);
                    xsltArgumentList.AddParam("projectname", "", xmlApplicationConfiguration.SelectSingleNode($"//cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/name"));
                    xsltArgumentList.AddParam("outputchannel-language", "", projectVars.outputChannelVariantLanguage);
                    xsltArgumentList.AddParam("section-title", "", exportedItem.Linkname ?? exportedItem.Id ?? "unknown");
                    xsltArgumentList.AddParam("data-reference", "", exportedItem.Ref);
                    xmlFilingContent = TransformXmlToDocument(xmlFilingContent, "cms_export-section-data", xsltArgumentList);
                }
                catch (Exception ex)
                {
                    await MessageToCurrentClient("DataImportExportProgress", $"ERROR: Exporting {(exportedItem.Linkname ?? exportedItem.Id ?? "unknown")} failed");
                    HandleError($"Unable to export {exportedItem.Ref}", $"error: {ex}");
                }

                //
                // => Store the to-be-exported file in the folder we have just created
                //
                var xhtmlPathOs = $"{tempFolderPathOs}/{exportCounter.ToString("D3")}-{projectVars.projectId}-{projectVars.outputChannelVariantLanguage}_{articleId}.html";
                if (exportCounter == 0) initialExportedFilePathOs = xhtmlPathOs;
                try
                {
                    await xmlFilingContent.SaveAsync(xhtmlPathOs);
                }
                catch (Exception ex)
                {
                    // Handle the error
                    HandleError("Could not store the XHTML file", $"xhtmlPathOs: {xhtmlPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                }

                exportCounter++;
            }

            // HandleError("Thrown on purpose", "Some debugging information to test if errors are properly handled");


            //
            // => Create a zip file
            //
            await MessageToCurrentClient("DataImportExportProgress", "Creating ZIP file");
            var zipSuffix = Path.GetFileNameWithoutExtension(exportedItemsInformation[0].Ref);
            var zipPackageFilePathOs = $"{Path.GetDirectoryName(tempFolderPathOs)}/{TaxxorClientId}-{projectVars.projectId}-{projectVars.outputChannelVariantLanguage}_{zipSuffix}.zip";
            try
            {
                if (File.Exists(zipPackageFilePathOs)) File.Delete(zipPackageFilePathOs);

                ZipFile.CreateFromDirectory(tempFolderPathOs, zipPackageFilePathOs);
            }
            catch (Exception ex)
            {
                HandleError("Could not create ZIP package", $"tempFolderPathOs: {tempFolderPathOs}, xbrlZipPackageFilePathOs: {zipPackageFilePathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
            }


            //
            // => Generate the response for the client
            //
            if (serveAsBinary)
            {
                // Stream the ZIP file to the client
                await StreamFile(context.Response, Path.GetFileName(zipPackageFilePathOs), zipPackageFilePathOs, true, reqVars.returnType);
            }
            else
            {

                // Write a response to the client
                if (reqVars.returnType == ReturnTypeEnum.Json)
                {

                    dynamic jsonData = new ExpandoObject();
                    jsonData.result = new ExpandoObject();

                    jsonData.result.message = "Successfully rendered the Taxxor section export package file";
                    jsonData.result.filename = Path.GetFileName(zipPackageFilePathOs);


                    var json = (string)ConvertToJson(jsonData);
                    await context.Response.OK(json, ReturnTypeEnum.Json, true);
                }
                else if (reqVars.returnType == ReturnTypeEnum.Xml)
                {
                    XmlDocument? xmlResponse = RetrieveTemplate("success_xml");
                    xmlResponse.SelectSingleNode("/result/message").InnerText = "Successfully rendered the Taxxor section export package file";

                    XmlElement nodeToBeAdded = xmlResponse.CreateElement("filename");
                    nodeToBeAdded.InnerText = Path.GetFileName(zipPackageFilePathOs);
                    xmlResponse.DocumentElement.AppendChild(nodeToBeAdded);

                    await context.Response.OK(xmlResponse, ReturnTypeEnum.Xml, false);
                }
                else
                {
                    var message = $"Successfully rendered the Taxxor section export package file. Filename: {Path.GetFileName(zipPackageFilePathOs)}";

                    await context.Response.OK(message, ReturnTypeEnum.Txt, true);
                }
            }
        }


        /// <summary>
        /// Imports section XML content
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ImportSectionXml(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var useRandomOutputFolder = (debugRoutine) ? false : true;
            var errorMessage = "";
            var errorDebugInfo = "";
            var currentTimestamp = createIsoTimestamp();

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);


            // Calculate the folder where we need to export to
            var tempFolderName = (useRandomOutputFolder) ? $"import_{CreateTimeBasedString("millisecond")}{RandomString(8, false)}" : $"import_{projectVars.projectId}-{projectVars.outputChannelVariantLanguage}-{projectVars.did}";
            var tempFolderPathOs = $"{dataRootPathOs}/temp/{tempFolderName}";
            var debugFolderName = Path.GetDirectoryName(tempFolderPathOs);


            //
            // => Load the output channel hierarchy
            //
            await MessageToCurrentClient("DataImportExportProgress", "Retrieving outputchannel hierarchy");
            var outputChannelHierarchyId = RetrieveOutputChannelHierarchyMetadataId(projectVars);
            if (!projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyId))
            {
                HandleError("Could not find the output channel hierarchy", $"outputChannelHierarchyId: {outputChannelHierarchyId}, stack-trace: {GetStackTrace()}");
            }
            var xmlHierarchyDoc = projectVars.cmsMetaData[outputChannelHierarchyId].Xml;

            //
            // => Retrieve the item in the hierarchy that we want to work with
            //
            var nodeHierarchyItem = xmlHierarchyDoc.SelectSingleNode($"/items/structured//item[@id={GenerateEscapedXPathString(projectVars.did)}]");
            if (nodeHierarchyItem == null) HandleError("Could not locate the section you want to export in hierarchy", $"projectVars.did: {projectVars.did}, stack-trace: {GetStackTrace()}");

            //
            // => Render a list of section ID's that is in the scope of the selection that the user has made in the hierarchy manager
            //
            var selectedItemsInformation = new List<SiteStructureItem>();
            selectedItemsInformation.Add(RetrieveHierarchyItemInformation(nodeHierarchyItem, null));
            var nodeListSubItems = nodeHierarchyItem.SelectNodes("descendant::item");
            foreach (XmlNode nodeItem in nodeListSubItems)
            {
                selectedItemsInformation.Add(RetrieveHierarchyItemInformation(nodeItem, null));
            }


            //
            // => Load the footnote repository
            //
            await MessageToCurrentClient("DataImportExportProgress", "Retrieve the footnote repository");
            var nodeFootnoteRepository = xmlApplicationConfiguration.SelectSingleNode($"/configuration/locations/location[@id='footnote_repository']");
            if (nodeFootnoteRepository == null) HandleError("Could not locate footnote repository");

            var xmlFootnoteRepository = await DocumentStoreService.FilingData.Load<XmlDocument>(projectVars.projectId, nodeFootnoteRepository.InnerText, nodeFootnoteRepository.GetAttribute("path-type"), true, false);

            if (XmlContainsError(xmlFootnoteRepository))
            {
                HandleError($"Could not load footnote repository.", $"message: {xmlFootnoteRepository.SelectSingleNode("//message")?.InnerText}, debug-info: {xmlFootnoteRepository.SelectSingleNode("//debuginfo")?.InnerText}");
            }

            //
            // => Setup folder structure for package
            //
            await MessageToCurrentClient("DataImportExportProgress", "Setup folder structure for import utility");
            try
            {
                if (Directory.Exists(tempFolderPathOs)) DelTree(tempFolderPathOs);

                Directory.CreateDirectory(tempFolderPathOs);
            }
            catch (Exception ex)
            {
                // Handle the error
                HandleError("Could not create directory structure", $"tempFolderPathOs: {tempFolderPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
            }

            //
            // => Pick up the posted binary files and store them in the temporary folder
            //
            await MessageToCurrentClient("DataImportExportProgress", "Retrieving uploaded files");
            try
            {
                foreach (IFormFile postedBinaryFile in request.Form.Files)
                {
                    string filename = ContentDispositionHeaderValue.Parse(postedBinaryFile.ContentDisposition).FileName.Trim('"');

                    // Storing the file in the temporary folder
                    var targetFilePathOs = $"{tempFolderPathOs}/{filename}";
                    using (FileStream output = System.IO.File.Create(targetFilePathOs))
                    {
                        await postedBinaryFile.CopyToAsync(output);
                    }

                    switch (Path.GetExtension(targetFilePathOs))
                    {
                        case ".zip":
                            appLogger.LogInformation("ZIP file upload detected");
                            ZipFile.ExtractToDirectory(targetFilePathOs, tempFolderPathOs);

                            // Remove the ZIP file from the temporary folder
                            File.Delete(targetFilePathOs);

                            // Check if the extracted files are directly in the target or a subfolder
                            var extractedFiles = Directory.GetFiles(tempFolderPathOs, "*.*", SearchOption.AllDirectories);
                            foreach (var extractedFilePathOs in extractedFiles)
                            {
                                if (Path.GetDirectoryName(extractedFilePathOs) != Path.GetDirectoryName(targetFilePathOs))
                                {
                                    File.Move(extractedFilePathOs, $"{tempFolderPathOs}/{Path.GetFileName(extractedFilePathOs)}");
                                }
                            }
                            break;

                        case ".htm":
                        case ".html":
                        case ".xml":
                            appLogger.LogInformation("XHTML file upload detected");
                            break;

                        default:
                            HandleError("Unkown file format", $"filename: {filename}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the error
                HandleError("Unable to store and process uploaded files", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }





            //
            // => Loop through the XML or HTML files that the user has just uploaded and attempt to import the content
            //
            var countTotal = 0;
            var countSuccess = 0;
            var countFailure = 0;
            var countFootnoteUpdates = 0;

            string[] sourceFileExtensions = { "xml", "html", "htm" };
            var sourceFiles = Directory.GetFiles(tempFolderPathOs);
            foreach (var sourceFilePathOs in sourceFiles)
            {
                // appLogger.LogError($"- sourceFilePathOs: {sourceFilePathOs}");
                if (sourceFilePathOs.EndsWithAny(sourceFileExtensions))
                {
                    countTotal++;

                    var xmlSourceFilingContent = new XmlDocument();
                    try
                    {
                        var sourceFileContent = await RetrieveTextFile(sourceFilePathOs, "utf-8", reqVars.returnType);
                        xmlSourceFilingContent.LoadHtml(sourceFileContent);
                        if (debugRoutine) await xmlSourceFilingContent.SaveAsync($"{debugFolderName}/filingdata-source.xml", false);


                        //
                        // => Test if this file is within the scope that the user selected
                        //
                        var inSelectionScope = false;
                        var dataReference = xmlSourceFilingContent.SelectSingleNode("//body")?.GetAttribute("data-ref") ?? "";
                        if (dataReference == "")
                        {
                            await MessageToCurrentClient("DataImportExportProgress", $"Unable to locate data reference information in {Path.GetFileName(sourceFilePathOs)}");
                            await MessageToCurrentClient("DataImportExportProgress", $"Skipping import...");
                            countFailure++;
                            continue;
                        }
                        SiteStructureItem? currentSiteStructureItem = null;
                        foreach (var siteStructureItem in selectedItemsInformation)
                        {
                            if (siteStructureItem.Ref == dataReference)
                            {
                                inSelectionScope = true;
                                currentSiteStructureItem = siteStructureItem;
                                continue;
                            }
                        }
                        if (!inSelectionScope)
                        {
                            await MessageToCurrentClient("DataImportExportProgress", $"WARNNG: Import file {Path.GetFileName(sourceFilePathOs)} not found within the selection scope");
                            countFailure++;
                            continue;
                        }


                        //
                        // => Retrieve the target section content from the Taxxor Project Data Store
                        //
                        await MessageToCurrentClient("DataImportExportProgress", $"Retrieving '{currentSiteStructureItem.Linkname}' target section data");
                        XmlDocument xmlTargetFilingContent = new XmlDocument();

                        xmlTargetFilingContent = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadSourceData(projectVars.projectId, currentSiteStructureItem.Id, "latest", debugRoutine);
                        if (XmlContainsError(xmlTargetFilingContent))
                        {
                            errorMessage = xmlTargetFilingContent.SelectSingleNode("//message")?.InnerText ?? "unknown";
                            errorDebugInfo = xmlTargetFilingContent.SelectSingleNode("//debuginfo")?.InnerText ?? "unknown";

                            await MessageToCurrentClient("DataImportExportProgress", "ERROR: Unable to load the target content");
                            appLogger.LogError($"{errorMessage}. {errorDebugInfo}, dataReference: {currentSiteStructureItem.Ref}");
                            countFailure++;
                            continue;
                        }
                        if (debugRoutine) await xmlTargetFilingContent.SaveAsync($"{debugFolderName}/filingdata-target.xml", false);

                        // Retrieve the article ID from the content we have just received
                        var xPathTargetArticle = $"/data/content[@lang={GenerateEscapedXPathString(projectVars.outputChannelVariantLanguage)}]//article";
                        var nodeTargetArticle = xmlTargetFilingContent.SelectSingleNode(xPathTargetArticle);
                        if (nodeTargetArticle == null) HandleError("Could not find the target article node", $"xPathTargetArticle: {xPathTargetArticle}, stack-trace: {GetStackTrace()}");

                        var targetArticleId = GetAttribute(nodeTargetArticle, "id") ?? "";

                        if (string.IsNullOrEmpty(targetArticleId)) HandleError("Unable to retrieve a section ID from the filing section content");


                        //
                        // => Import the content into the target section
                        //
                        var nodeSourceArticle = xmlSourceFilingContent.SelectSingleNode("//article");
                        if (nodeSourceArticle == null)
                        {
                            errorMessage = "Unable to import content because the article node cannot be found";
                            await MessageToCurrentClient("DataImportExportProgress", errorMessage);
                            appLogger.LogError(errorMessage + $" dataReference: {currentSiteStructureItem.Ref}");
                            countFailure++;
                            continue;
                        }

                        var sourceArticleId = GetAttribute(nodeSourceArticle, "id");
                        if (string.IsNullOrEmpty(sourceArticleId))
                        {
                            errorMessage = "Imported data does not contain an article ID";
                            await MessageToCurrentClient("DataImportExportProgress", errorMessage);
                            appLogger.LogError(errorMessage + $" dataReference: {currentSiteStructureItem.Ref}");
                            countFailure++;
                            continue;
                        }

                        if (sourceArticleId != targetArticleId)
                        {
                            errorMessage = "Could not import the data because the source and target article ID's do not match";
                            await MessageToCurrentClient("DataImportExportProgress", errorMessage);
                            appLogger.LogError(errorMessage + $" dataReference: {currentSiteStructureItem.Ref}, sourceArticleId: {sourceArticleId}, targetArticleId: {targetArticleId}");
                            countFailure++;
                            continue;
                        }

                        //
                        // => Import the data in the target document
                        //
                        ReplaceXmlNode(nodeTargetArticle, nodeSourceArticle);


                        //
                        // => Update the timestamps
                        //
                        nodeTargetArticle = xmlTargetFilingContent.SelectSingleNode(xPathTargetArticle);
                        if (nodeTargetArticle == null)
                        {
                            appLogger.LogWarning($"Could not set timestamps because the target article could not be found. xPathTargetArticle: {xPathTargetArticle}");
                            continue;
                        }
                        else
                        {
                            SetAttribute(nodeTargetArticle, "data-last-modified", currentTimestamp);
                            SetAttribute(nodeTargetArticle, "modified-by", projectVars.currentUser.Id);
                        }
                        var nodeDateModified = xmlTargetFilingContent.SelectSingleNode("/data/system/date_modified");
                        if (nodeDateModified == null)
                        {
                            appLogger.LogWarning($"Could not find date modified node in the system section of the article");
                            continue;
                        }
                        else
                        {
                            nodeDateModified.InnerText = currentTimestamp;
                        }

                        //
                        // => Store the file on the Taxxor Document Store
                        //
                        await MessageToCurrentClient("DataImportExportProgress", "Store the updated section content on the server");
                        var xmlFilingContentSaveResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.SaveSourceData(xmlTargetFilingContent, currentSiteStructureItem.Id, projectVars.outputChannelVariantLanguage, debugRoutine);
                        if (XmlContainsError(xmlFilingContentSaveResult))
                        {
                            errorMessage = xmlFilingContentSaveResult.SelectSingleNode("//message")?.InnerText ?? "unknown";
                            errorDebugInfo = xmlFilingContentSaveResult.SelectSingleNode("//debuginfo")?.InnerText ?? "unknown";

                            await MessageToCurrentClient("DataImportExportProgress", "ERROR: Unable to store the updated content on the server");
                            appLogger.LogError($"{errorMessage}. {errorDebugInfo}, dataReference: {currentSiteStructureItem.Ref}");
                            countFailure++;
                            continue;
                        }

                        // - Save the imported result for debugging purposes
                        if (debugRoutine) await xmlTargetFilingContent.SaveAsync($"{debugFolderName}/filingdata-imported.xml", false);

                        countSuccess++;


                        //
                        // => Update the footnotes into the footnotes library documemt
                        //

                        // - table footnotes
                        var nodeListTableFootnotes = nodeSourceArticle.SelectNodes("*//div[@class='footnote' and @id and span/@lang]");
                        foreach (XmlNode nodeTableFootnote in nodeListTableFootnotes)
                        {
                            var sourceFootnoteId = nodeTableFootnote.GetAttribute("id");
                            var sourceFootnoteContent = nodeTableFootnote.SelectSingleNode("span[@lang]").InnerXml;

                            if (_insertFootnoteContentForLang(ref xmlFootnoteRepository, sourceFootnoteId, sourceFootnoteContent, projectVars.outputChannelVariantLanguage))
                            {
                                countFootnoteUpdates++;
                            }
                        }

                        // - footnotes in the running text
                        var nodeListContentFootnotes = nodeSourceArticle.SelectNodes("*//span[@class='footnote section' and @id and sup/@class and span/@lang]");
                        foreach (XmlNode nodeContentFootnote in nodeListContentFootnotes)
                        {
                            var sourceFootnoteId = nodeContentFootnote.GetAttribute("id");
                            var sourceFootnoteContent = nodeContentFootnote.SelectSingleNode("span[@lang]").InnerXml;

                            if (_insertFootnoteContentForLang(ref xmlFootnoteRepository, sourceFootnoteId, sourceFootnoteContent, projectVars.outputChannelVariantLanguage))
                            {
                                countFootnoteUpdates++;
                            }
                        }



                    }
                    catch (Exception ex)
                    {
                        HandleError("Could not import filing content", $"error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }
                else
                {
                    await MessageToCurrentClient("DataImportExportProgress", $"WARNING: Skipping {Path.GetFileName(sourceFilePathOs)}: unsupported file format.");
                }
            }


            //
            // => Handle basic errors
            //
            if (countTotal == 0 || countTotal == countFailure)
            {
                HandleError("Unable to import any content", $"countTotal: {countTotal}, countSuccess: {countSuccess}, countFailure: {countFailure}");
            }
            if (countSuccess > 0 && countFailure > 0)
            {
                await MessageToCurrentClient("DataImportExportProgress", "WARNING: Unable to import all content");
                appLogger.LogWarning($"Unable to import all content. countTotal: {countTotal}, countSuccess: {countSuccess}, countFailure: {countFailure}");
            }



            //
            // => Optionally store the footnote repository in the Taxxor Document Store
            //
            if (countFootnoteUpdates > 0)
            {
                await MessageToCurrentClient("DataImportExportProgress", "Storing updated footnotes on the server");

                var xmlSaveResult = await DocumentStoreService.FilingData.Save<XmlDocument>(xmlFootnoteRepository, projectVars.projectId, nodeFootnoteRepository.InnerText, nodeFootnoteRepository.GetAttribute("path-type"), true, false);
                if (XmlContainsError(xmlSaveResult)) HandleError(reqVars.returnType, xmlSaveResult);

                // => Commit the footnote changes in the version control system
                XmlDocument? xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "u"; // Update
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = "Footnotes";
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = "footnotes";
                var message = xmlCommitMessage.DocumentElement.InnerXml;

                // Execute the commit request
                var commitResult = await Taxxor.ConnectedServices.DocumentStoreService.VersionControl.GitCommit(projectVars.projectId, "reportdataroot", message, debugRoutine);
                if (!commitResult.Success)
                {
                    appLogger.LogWarning($"There was an issue commiting the content in the version control system. {commitResult.Message}, debuginfo: {commitResult.DebugInfo}");
                }
            }

            // => Render a response to the client
            await context.Response.OK(GenerateSuccessXml("Successfully imported the section data", $"countTotal: {countTotal}, countSuccess: {countSuccess}, countFailure: {countFailure}"), reqVars.returnType, true);
        }


        /// <summary>
        /// Updates or creates a footnote in the footnote repository for a specific language
        /// </summary>
        /// <param name="xmlFootnoteRepository"></param>
        /// <param name="footnoteId"></param>
        /// <param name="footnoteContent"></param>
        /// <param name="footnoteLanguage"></param>
        /// <returns></returns>
        private static bool _insertFootnoteContentForLang(ref XmlDocument xmlFootnoteRepository, string footnoteId, string footnoteContent, string footnoteLanguage)
        {
            var footnoteRepositoryUpdated = false;

            if (string.IsNullOrEmpty(footnoteId))
            {
                appLogger.LogWarning($"Unable to insert footnote content because footnoteId is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(footnoteContent))
            {
                appLogger.LogWarning($"Unable to insert footnote content because footnoteContent is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(footnoteLanguage))
            {
                appLogger.LogWarning($"Unable to insert footnote content because footnoteLanguage is null or empty");
                return false;
            }


            var nodeRepositoryFootnoteWrapper = xmlFootnoteRepository.SelectSingleNode($"/footnotes/footnote[@id='{footnoteId}']");
            if (nodeRepositoryFootnoteWrapper == null)
            {
                appLogger.LogError($"Footnotewrapper with ID {footnoteId} could not be found in the repository");
            }
            else
            {
                // Set the default language on the existing footnote content elements
                var nodeListFootnotesWithoutLang = nodeRepositoryFootnoteWrapper.SelectNodes("span[not(@lang)]");
                foreach (XmlNode nodeFootnoteWithoutLang in nodeListFootnotesWithoutLang)
                {
                    nodeFootnoteWithoutLang.SetAttribute("lang", footnoteLanguage);
                    footnoteRepositoryUpdated = true;
                }


                var nodeTargetFootnoteContent = nodeRepositoryFootnoteWrapper.SelectSingleNode($"span[@lang='{footnoteLanguage}']");
                if (nodeTargetFootnoteContent == null)
                {
                    nodeTargetFootnoteContent = xmlFootnoteRepository.CreateElement("span");
                    nodeTargetFootnoteContent.SetAttribute("lang", footnoteLanguage);
                    nodeTargetFootnoteContent.InnerXml = footnoteContent;
                    nodeRepositoryFootnoteWrapper.AppendChild(nodeTargetFootnoteContent);
                    footnoteRepositoryUpdated = true;
                }
                else
                {
                    nodeTargetFootnoteContent.InnerXml = footnoteContent;
                    footnoteRepositoryUpdated = true;
                }
            }

            return footnoteRepositoryUpdated;
        }

    }

}