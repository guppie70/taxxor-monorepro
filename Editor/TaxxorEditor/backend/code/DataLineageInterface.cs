using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{


    /// <summary>
    /// Utilities used for interacting with the XBRL service
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Renders an XBRL package based on the posted variables and returns the result to the client
        /// </summary>
        /// <param name="returnType"></param>
        public static async Task RenderDataLineageReport(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            RequestVariables reqVars = RetrieveRequestVariables(System.Web.Context.Current);
            ProjectVariables projectVars = RetrieveProjectVariables(System.Web.Context.Current);

            // Variables to speed up development
            var debugRoutine = (reqVars.isDebugMode == true || siteType == "local" || siteType == "dev");
            var useRandomOutputFolder = (siteType == "prev" || siteType == "prod");
            var debuggerInfo = "";

            // Standard: {pid: '<<project_id>>', vid: '<<version_id>>', ocvariantid, '<<output channel id>>', oclang: '<<output channel language>>', did: 'all'}
            // Optional: {serveasdownload: 'true|false', serveasbinary: 'true|false', mode, 'normal|diff', base: 'baseversion-id', latest: 'current|latestversion-id'}

            //
            // => Retrieve posted values
            //
            var sectionId = request.RetrievePostedValue("did", RegexEnum.UltraLoose.Value, true, reqVars.returnType, null);
            var projectId = request.RetrievePostedValue("pid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var versionId = request.RetrievePostedValue("vid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var outputChannelVariantId = request.RetrievePostedValue("ocvariantid", RegexEnum.Loose.Value, true, reqVars.returnType);
            var outputChannelVariantLanguage = request.RetrievePostedValue("oclang", RegexEnum.Loose.Value, true, reqVars.returnType);
            var reportingRequirementScheme = request.RetrievePostedValue("reportingrequirementscheme", RegexEnum.Default.Value, false, reqVars.returnType, "PHG2017");
            var serveAsDownloadString = request.RetrievePostedValue("serveasdownload", "(true|false)", false, reqVars.returnType, "false");
            var serveAsDownload = (serveAsDownloadString == "true");
            var serveAsBinaryString = request.RetrievePostedValue("serveasbinary", "(true|false)", false, reqVars.returnType, "false");
            var serveAsBinary = (serveAsBinaryString == "true");
            var context = request.RetrievePostedValue("context", "(editor|outputgenerator)", false, reqVars.returnType, "outputgenerator");
            var webSocketMode = context == "outputgenerator";
            webSocketMode = true;

            //
            // => Create the XSLT stylesheet cache
            //
            SetupPdfStylesheetCache(reqVars);


            //
            // => Return data to the web client to close the XHR call
            //
            if (reqVars.returnType == ReturnTypeEnum.Json && webSocketMode)
            {
                await response.OK(GenerateSuccessXml("Started data lineage report generation", $"projectId: {projectVars.projectId}"), ReturnTypeEnum.Json, true, true);
                await response.CompleteAsync();
            }

            //
            // => In the case of rendering the data lineage report from the editor -> then automatically include the section ID's of all chile elements
            //
            if (context == "editor")
            {
                var xmlOutputChannelHierarchy = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadHierarchy(projectId, outputChannelVariantId, debugRoutine);
                if (xmlOutputChannelHierarchy != null)
                {
                    var nodeChildSections = xmlOutputChannelHierarchy.SelectNodes($"//item[@id='{sectionId}']//item");
                    foreach (XmlNode nodeChildSection in nodeChildSections)
                    {
                        var sectionChildId = nodeChildSection.Attributes["id"].Value;
                        sectionId += $",{sectionChildId}";

                    }
                }
            }

            //
            // => Start generating the data lineage report
            //
            try
            {
                TaxxorReturnMessage? dataLineageRenderResult = null;

                // Send a message to the client that we will start the generation of a data lineage report
                await _outputGenerationProgressMessage("Start generation of data lineage report");


                // - Determine the sections for which to generate the report with
                List<string> dataReferences = new List<string>();
                List<string> sectionIds = new List<string>();

                if (sectionId.Contains(","))
                {
                    sectionIds.AddRange(sectionId.Split(","));
                }
                else
                {
                    sectionIds.Add(sectionId);
                }

                // sectionIds.Add("2041649-consolidated-statements-of-income");
                // dataReferences.Add("2041651-consolidated-statements-of-comprehensive-income.xml");

                dataLineageRenderResult = await RenderDataLineageReport(projectId, outputChannelVariantId, reportingRequirementScheme, sectionIds, dataReferences);


                if (!dataLineageRenderResult.Success)
                {
                    appLogger.LogError($"{dataLineageRenderResult.Message}, debuginfo: {dataLineageRenderResult.DebugInfo}");
                    if (webSocketMode)
                    {
                        await MessageToCurrentClient("DataLineageGenerationDone", dataLineageRenderResult);
                    }
                    else
                    {
                        await response.Error(dataLineageRenderResult, ReturnTypeEnum.Json);
                    }
                }
                else
                {
                    dynamic jsonData = new ExpandoObject();
                    jsonData.result = new ExpandoObject();

                    jsonData.result.message = $"Successfully rendered the data lineage report";
                    jsonData.result.filename = dataLineageRenderResult.Payload;


                    if ((siteType == "local" || siteType == "dev") && debuggerInfo != "")
                    {
                        //jsonData.result.debuginfo = debuggerInfo;
                    }

                    var json = (string)ConvertToJson(jsonData);

                    // Create a TaxxorReturnMessage
                    var generationResult = new TaxxorReturnMessage(true, "Successfully generated data lineage report", json, "");

                    // Send the result of the synchronization process back to the server (use the message field to transport the target project id)
                    if (webSocketMode)
                    {
                        await MessageToCurrentClient("DataLineageGenerationDone", generationResult);
                    }
                    else
                    {
                        await response.OK(generationResult, ReturnTypeEnum.Json, true);
                    }

                }
            }
            catch (Exception ex)
            {
                var errorMessage = new TaxxorReturnMessage(false, "There was an error generating data lineage report", $"error: {ex}");

                if (reqVars.returnType == ReturnTypeEnum.Json)
                {
                    appLogger.LogError(ex, errorMessage.Message);
                    if (webSocketMode)
                    {
                        await MessageToCurrentClient("DataLineageGenerationDone", errorMessage);
                    }
                    else
                    {
                        await response.Error(errorMessage, ReturnTypeEnum.Json);
                    }
                }
                else
                {
                    HandleError(errorMessage);
                }
            }
        }




        /// <summary>
        /// Renders a data lineage report using the mapping information from the Structured Data Store and the Pandoc service to render the Excel
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="outputChannelVariantId"></param>
        /// <param name="targetModel"></param>
        /// <param name="sectionIds"></param>
        /// <param name="dataReferences"></param>
        /// <returns>The filename of the Excel file which is stored in the shared temp dir</returns>
        public static async Task<TaxxorReturnMessage> RenderDataLineageReport(string projectId, string outputChannelVariantId, string targetModel, List<string> sectionIds, List<string> dataReferences = null)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var useRandomOutputFolder = (siteType == "prev" || siteType == "prod");
            useRandomOutputFolder = true;
            var includeLevel2BlockTags = false;
            var baseDebugInfo = $"projectId: {projectId}, targetModel: {targetModel}, outputChannelVariantId: {outputChannelVariantId}";
            var msExcelTargetPathOs = "";
            Stopwatch? watch = null;

            try
            {

                //
                // => Setup default paths that the Pandoc service will need to work with
                //
                var excelFileName = $"datalineage-{projectId}-{outputChannelVariantId}-{targetModel}.xlsx".ToLower();
                var excelFileNameTarget = excelFileName;
                if (useRandomOutputFolder) excelFileNameTarget = $"datalineage-{projectId}-{outputChannelVariantId}-{targetModel}-{RandomString(8, false)}.xlsx".ToLower();

                // Location on the shared folder where we are working with the data lineage source data
                var tempFolderName = (useRandomOutputFolder) ? $"datalineage_{CreateTimeBasedString("millisecond")}{RandomString(8, false)}" : $"datalineage-{projectId}-{outputChannelVariantId}-{targetModel}".ToLower();
                var basePath = $"/temp/{tempFolderName}";
                var sharedFoldePathForDocker = RetrieveSharedFolderPathOs(true);
                var sharedFolderWebsiteWorkingPathForDocker = $"{sharedFoldePathForDocker}{basePath}";
                var sharedFolderForApp = RetrieveSharedFolderPathOs();
                var sharedFolderWebsiteWorkingPathForApp = $"{sharedFolderForApp}{basePath}";


                var sourceFolderPathForDocker = $"{sharedFolderWebsiteWorkingPathForDocker}/data";
                var targetFolderPathForDocker = $"{sharedFolderWebsiteWorkingPathForDocker}/excel";

                //
                // => Setup directory structure
                //
                Directory.CreateDirectory($"{sharedFolderWebsiteWorkingPathForApp}/data");
                Directory.CreateDirectory($"{sharedFolderWebsiteWorkingPathForApp}/excel");

                DelTree($"{sharedFolderWebsiteWorkingPathForApp}/data", false);
                DelTree($"{sharedFolderWebsiteWorkingPathForApp}/excel", false);

                //
                // => Load the hierarchy to figure out the section ID's that we need to work with
                //
                if ((sectionIds == null || sectionIds.Count == 0) && (dataReferences != null && dataReferences.Count > 0))
                {
                    var xmlOutputChannelHierarchy = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadHierarchy(projectId, outputChannelVariantId, debugRoutine);

                    foreach (var dataReference in dataReferences)
                    {
                        var itemNode = xmlOutputChannelHierarchy.SelectSingleNode($"//item[@data-ref='{dataReference}']");
                        if (itemNode != null)
                        {
                            var itemId = itemNode.GetAttribute("id");
                            if (!string.IsNullOrEmpty(itemId) && !sectionIds.Contains(itemId)) sectionIds.Add(itemId);
                        }
                        else
                        {
                            appLogger.LogWarning($"Unable to find a section ID for data reference: {dataReference}");
                        }
                    }
                }
                if (sectionIds.Count == 0)
                {
                    return new TaxxorReturnMessage(false, "No section ID's found to render the data lineage report with", $"{baseDebugInfo}, dataReferences: {string.Join(", ", dataReferences.ToArray())}");
                }

                //
                // => Retrieve the data for the report as it would normally have been used in the PDF document
                //

                // - Construct a project variables object that we can send to the Project Data Store to retrieve the output channel content with
                var editorId = RetrieveEditorIdFromProjectId(projectId);
                var outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectId, outputChannelVariantId);
                var outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectId, outputChannelVariantId);

                var projectVariablesForExcelContent = new ProjectVariables();
                projectVariablesForExcelContent.projectId = projectId;
                projectVariablesForExcelContent.versionId = "latest";
                projectVariablesForExcelContent.editorContentType = "regular";
                projectVariablesForExcelContent.reportTypeId = RetrieveReportTypeIdFromProjectId(projectId);
                projectVariablesForExcelContent.outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectId, outputChannelVariantId);
                projectVariablesForExcelContent.editorId = editorId;
                projectVariablesForExcelContent.outputChannelVariantId = outputChannelVariantId;
                projectVariablesForExcelContent.outputChannelVariantLanguage = outputChannelVariantLanguage;

                // - Retrieve the source dataReference
                var xmlReportContent = await DocumentStoreService.PdfData.Load(projectVariablesForExcelContent, string.Join(",", sectionIds.ToArray()), false, "single-section", false, debugRoutine);
                if (XmlContainsError(xmlReportContent)) return new TaxxorReturnMessage(false, "Unable to retrieve source data for the data lineage report", baseDebugInfo);
                if (debugRoutine)
                {
                    await xmlReportContent.SaveAsync($"{logRootPathOs}/-datalineage.1.xhtml.xml", true, true);
                }

                // - Message to the client
                await _outputGenerationProgressMessage("Successfully retrieved content for data lineage report");


                //
                // => Find the fact ID's used in the report
                //
                var factIds = new List<string>();
                var nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlReportContent, false);
                foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                {
                    var factId = nodeStructuredDataElement.GetAttribute("data-fact-id").Replace("\n", "");
                    if (!factIds.Contains(factId)) factIds.Add(factId);
                }

                // - Add XBRL Level 2 elements to the list (old style)
                var nodeListLevelTwoElements = xmlReportContent.SelectNodes("//*[contains(@class, 'xbrl-level-2') and @data-fact-id]");
                foreach (XmlNode nodeLevelTwoElement in nodeListLevelTwoElements)
                {
                    var factId = nodeLevelTwoElement.GetAttribute("data-fact-id").Replace("\n", "");
                    if (!factIds.Contains(factId)) factIds.Add(factId);
                }

                // Add XBRL Level 2 elements to the list (new style)
                nodeListLevelTwoElements = xmlReportContent.SelectNodes("//*[@data-block-id]");
                foreach (XmlNode nodeLevelTwoElement in nodeListLevelTwoElements)
                {
                    var factIdsLevelTwoRaw = nodeLevelTwoElement.GetAttribute("data-block-id") ?? "";
                    var factIdsLevelTwo = new List<string>();
                    if (factIdsLevelTwoRaw.Contains(","))
                    {
                        factIdsLevelTwo = factIdsLevelTwoRaw.Split(',').ToList();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(factIdsLevelTwoRaw)) factIdsLevelTwo.Add(factIdsLevelTwoRaw);
                    }

                    foreach (var factIdLevelTwo in factIdsLevelTwo)
                    {
                        var factIdLevelTwoToInsert = factIdLevelTwo.Replace("\n", "");
                        if (!factIds.Contains(factIdLevelTwoToInsert)) factIds.Add(factIdLevelTwoToInsert);
                    }
                }

                if (debugRoutine) Console.WriteLine($"+++ Found {factIds.Count} structured data elements to retrieve from the Structured Data Store +++");

                //
                // => Only continue with the process if the sections contain factID's
                //
                if (factIds.Count == 0) return new TaxxorReturnMessage(false, "No structured data elements found to render the data lineage report with", $"{baseDebugInfo}");


                //
                // => Data to be used in the API request
                //

                // - URL
                var apiUrl = $"{GetServiceUrl(ConnectedServiceEnum.StructuredDataStore)}/api/datalineage";
                var querystringData = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "targetModel", targetModel },
                    { "includeUnmapped", "true" }
                };
                apiUrl = QueryHelpers.AddQueryString(apiUrl, querystringData);

                // - HTTP headers
                CustomHttpHeaders customHttpHeaders = new CustomHttpHeaders
                {
                    RequestType = ReturnTypeEnum.Xml
                };
                if (!string.IsNullOrEmpty(SystemUser))
                {
                    customHttpHeaders.AddTaxxorUserInformation(SystemUser);
                }


                //
                // => Retrieve the data lineage content from the Structured Data Store
                //
                await _outputGenerationProgressMessage("Retrieving data lineage content per section");
                var xmlOverallDataLineageResult = new XmlDocument();
                var nodeDataLineageRoot = xmlOverallDataLineageResult.CreateElement("datalineage");

                var normalizedArticleTitles = new List<string>();
                var nodeListArticles = xmlReportContent.SelectNodes("//article[@data-fact-id or *//*/@data-fact-id and not(ancestor::article)]");
                var articleCount = 1;
                foreach (XmlNode nodeArticle in nodeListArticles)
                {

                    // - Retrieve data about the article we are processing
                    var articleId = nodeArticle.GetAttribute("id");
                    var dataReference = nodeArticle.GetAttribute("data-ref");
                    var articleTitle = RetrieveFirstHeaderText(nodeArticle);
                    if (string.IsNullOrEmpty(articleTitle) || (articleTitle.Contains("{") && articleTitle.Contains("}")))
                    {
                        appLogger.LogWarning($"Unable to find article title. articleId: {articleId}, dataReference: {dataReference}");
                        articleTitle = Path.GetFileNameWithoutExtension(dataReference);
                    }
                    var articleTitleNormalized = NormalizeFileNameWithoutExtension(articleTitle);
                    if (normalizedArticleTitles.Contains(articleTitleNormalized))
                    {
                        var articleTitleNormalizedAlternative = articleTitleNormalized;
                        var suffix = 0;
                        while (normalizedArticleTitles.Contains(articleTitleNormalizedAlternative))
                        {
                            suffix++;
                            articleTitleNormalizedAlternative = $"{articleTitleNormalized}-{suffix}";
                        }
                        articleTitleNormalized = articleTitleNormalizedAlternative;
                    }
                    normalizedArticleTitles.Add(articleTitleNormalized);

                    var userMessage = $"* ({articleCount}/{nodeListArticles.Count}) - {articleTitle}";
                    Console.WriteLine($"{userMessage}. articleId: {articleId}, dataReference: {dataReference}");
                    await _outputGenerationProgressMessage(userMessage);

                    var nodeDataLineageArticle = xmlOverallDataLineageResult.CreateElement("article");
                    nodeDataLineageArticle.SetAttribute("id", articleId);
                    nodeDataLineageArticle.SetAttribute("data-ref", dataReference);
                    nodeDataLineageArticle.SetAttribute("title", articleTitleNormalized);

                    var nodeTitle = xmlOverallDataLineageResult.CreateElementWithText("title", articleTitle);
                    nodeDataLineageArticle.AppendChild(nodeTitle);

                    // - Find the fact id's in the article
                    factIds = new List<string>();
                    nodeListStructuredDataElements = _retrieveStructuredDataElements(nodeArticle, false);
                    foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                    {
                        var factId = nodeStructuredDataElement.GetAttribute("data-fact-id").Replace("\n", "");
                        if (!factIds.Contains(factId)) factIds.Add(factId);
                    }

                    //
                    // => Block tagging
                    //
                    if (includeLevel2BlockTags)
                    {
                        // - Add level 2 structured data elements to the article (old style)
                        nodeListLevelTwoElements = nodeArticle.SelectNodes("*//*[contains(@class, 'xbrl-level-2') and @data-fact-id]");
                        foreach (XmlNode nodeLevelTwoElement in nodeListLevelTwoElements)
                        {
                            var factId = nodeLevelTwoElement.GetAttribute("data-fact-id").Replace("\n", "");
                            if (!factIds.Contains(factId)) factIds.Add(factId);
                        }

                        // Add XBRL Level 2 elements to the list (new style)
                        nodeListLevelTwoElements = nodeArticle.SelectNodes("//*[@data-block-id]");
                        foreach (XmlNode nodeLevelTwoElement in nodeListLevelTwoElements)
                        {
                            var factIdsLevelTwoRaw = nodeLevelTwoElement.GetAttribute("data-block-id") ?? "";
                            var factIdsLevelTwo = new List<string>();
                            if (factIdsLevelTwoRaw.Contains(","))
                            {
                                factIdsLevelTwo = factIdsLevelTwoRaw.Split(',').ToList();
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(factIdsLevelTwoRaw)) factIdsLevelTwo.Add(factIdsLevelTwoRaw);
                            }

                            foreach (var factIdLevelTwo in factIdsLevelTwo)
                            {
                                var factIdLevelTwoToInsert = factIdLevelTwo.Replace("\n", "");
                                if (!factIds.Contains(factIdLevelTwoToInsert)) factIds.Add(factIdLevelTwoToInsert);
                            }
                        }
                    }


                    if (factIds.Count > 0)
                    {
                        // - Create the document to send
                        var xmlDataToSend = new XmlDocument();
                        var nodeRequest = xmlDataToSend.CreateElement("request");
                        foreach (var factId in factIds)
                        {
                            var nodeItem = xmlDataToSend.CreateElementWithText("item", factId);
                            nodeRequest.AppendChild(nodeItem);
                        }
                        xmlDataToSend.AppendChild(nodeRequest);

                        // - Retrieve the data lineage information from the Structured Data Store
                        appLogger.LogInformation($"Starting calling Structured Data Store for info {dataReference}");
                        watch = System.Diagnostics.Stopwatch.StartNew();
                        var xmlDataLineageSource = await RestRequestHttp1<XmlDocument>(RequestMethodEnum.Post, apiUrl, xmlDataToSend, customHttpHeaders, 60000, true);
                        watch.Stop();
                        appLogger.LogInformation($"Finished calling Structured Data Store for info {dataReference} took: {watch.ElapsedMilliseconds.ToString()} ms");

                        if (XmlContainsError(xmlDataLineageSource))
                        {
                            appLogger.LogError($"Unable to retrieve source data for the data lineage report from the Structured Data Store. response: {xmlDataLineageSource.OuterXml}, dataReference: {dataReference}, baseDebugInfo: {baseDebugInfo}");
                            await _outputGenerationProgressMessage($"ERROR: failed to retrieve data for {articleTitle}");

                            // - Create dummy error items for each of the fact ids so that the issue can also be found in the report
                            foreach (var factId in factIds)
                            {
                                var nodeItem = xmlOverallDataLineageResult.CreateElement("item");
                                nodeItem.SetAttribute("id", factId);
                                nodeItem.SetAttribute("error", "Unable to retrieve information");
                                nodeDataLineageArticle.AppendChild(nodeItem);
                            }
                        }
                        else
                        {
                            // - Add all the results that we found into the article node that we have just created
                            var nodeListItems = xmlDataLineageSource.SelectNodes("/response/item");
                            foreach (XmlNode nodeItem in nodeListItems)
                            {
                                var nodeItemImported = xmlOverallDataLineageResult.ImportNode(nodeItem, true);
                                nodeDataLineageArticle.AppendChild(nodeItemImported);
                            }
                        }
                    }
                    else
                    {
                        appLogger.LogInformation($"No need to retrieve data lineage information for {dataReference} even though we initially found structured data elements in the article");
                    }


                    nodeDataLineageRoot.AppendChild(nodeDataLineageArticle);
                    articleCount++;
                }

                xmlOverallDataLineageResult.AppendChild(nodeDataLineageRoot);
                if (debugRoutine)
                {
                    await xmlOverallDataLineageResult.SaveAsync($"{logRootPathOs}/-datalineage.2.source.xml", true, true);
                }


                //
                // => Post process the data lineage source so that it's easier to process
                //
                var nodeListDataLineageItems = xmlOverallDataLineageResult.SelectNodes("/datalineage/article/item[@periodStart or @periodEnd]");
                foreach (XmlNode nodeItem in nodeListDataLineageItems)
                {
                    var periodStart = nodeItem.GetAttribute("periodStart");
                    try
                    {
                        if (!string.IsNullOrEmpty(periodStart)) nodeItem.SetAttribute("periodStart", createIsoTimestamp(periodStart, true));
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Failed to normalize period start date '{periodStart ?? ""}'");
                    }

                    var periodEnd = nodeItem.GetAttribute("periodEnd");
                    try
                    {
                        if (!string.IsNullOrEmpty(periodEnd)) nodeItem.SetAttribute("periodEnd", createIsoTimestamp(periodEnd, true));
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Failed to normalize period end date '{periodEnd ?? ""}'");
                    }
                }
                if (debugRoutine)
                {
                    await xmlOverallDataLineageResult.SaveAsync($"{logRootPathOs}/-datalineage.3.postprocess.xml", true, true);
                }

                //
                // => Prepare so that we can send this to the Excel convertor
                //
                var xmlExcelSourceDocument = TransformXmlToDocument(xmlOverallDataLineageResult, "taxxor_xsl_datalineage-prepare");
                if (debugRoutine)
                {
                    await xmlExcelSourceDocument.SaveAsync($"{logRootPathOs}/-datalineage.4.prepared.xml", true, true);
                }
                await xmlExcelSourceDocument.SaveAsync($"{sharedFolderWebsiteWorkingPathForApp}/data/{Path.GetFileNameWithoutExtension(excelFileName)}.xml");

                // Store the data for rendering the Excel files on the shared folder
                await TextFileCreateAsync(ConvertToJson(xmlExcelSourceDocument, Newtonsoft.Json.Formatting.Indented), $"{sharedFolderWebsiteWorkingPathForApp}/data/{Path.GetFileNameWithoutExtension(excelFileName)}.json");



                //
                // => Convert to Excel
                //
                await _outputGenerationProgressMessage("Start rendering Excel file");
                watch = System.Diagnostics.Stopwatch.StartNew();
                var msExcelRenderResponse = await Taxxor.ConnectedServices.ConversionService.RenderExcelFromDirectory(sourceFolderPathForDocker, targetFolderPathForDocker, excelFileName, true, true);
                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;
                if (siteType == "local") appLogger.LogInformation($"Rendering of Excel files took: {elapsedMs.ToString()} ms");

                if (XmlContainsError(msExcelRenderResponse))
                {
                    return new TaxxorReturnMessage(false, "Error rendering Data Lineage MS Excel", $"recieved: {msExcelRenderResponse.OuterXml}, {baseDebugInfo}");
                }
                else
                {
                    await _outputGenerationProgressMessage("Successfully rendered Excel file");

                    var msExcelSourceFilePathOs = $"{sharedFolderWebsiteWorkingPathForApp}/excel/{excelFileName}";

                    msExcelTargetPathOs = dataRootPathOs + "/temp/" + excelFileNameTarget;
                    if (!File.Exists(msExcelSourceFilePathOs))
                    {
                        return new TaxxorReturnMessage(false, "Could not locate source Excel file", $"msExcelSourceFilePathOs: {msExcelSourceFilePathOs}, {baseDebugInfo}");
                    }
                    else
                    {
                        // Move the file to the data directory of the Taxxor Editor
                        if (File.Exists(msExcelTargetPathOs)) File.Delete(msExcelTargetPathOs);

                        File.Copy(msExcelSourceFilePathOs, msExcelTargetPathOs);
                    }
                }


                //
                // => Return a success message
                //
                return new TaxxorReturnMessage(true, "Successfully rendered data lineage report", Path.GetFileName(msExcelTargetPathOs), baseDebugInfo);
            }
            catch (Exception ex)
            {
                var errorMessage = "There was an error creating the data lineage report";
                appLogger.LogError(ex, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, baseDebugInfo);
            }

        }

    }
}