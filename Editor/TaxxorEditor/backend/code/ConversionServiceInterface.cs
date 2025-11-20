using System;
using System.Buffers;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    /// <summary>
    /// C# utilities to interact with the Pandoc Service
    /// </summary>

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders all the tables in a document as Excel worksheets in an Excel workbook
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RenderMsExcel(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = reqVars.isDebugMode == true || siteType == "local" || siteType == "dev";

            // Standard: {pid: '<<project_id>>', vid: '<<version_id>>', ocvariantid, '<<output channel id>>', oclang: '<<output channel language>>', did: 'all'}
            // Optional: {serveasdownload: 'true|false', serveasbinary: 'true|false', mode, 'normal|diff', base: 'baseversion-id', latest: 'current|latestversion-id'}

            //
            // => Retrieve posted values
            //
            var sectionId = request.RetrievePostedValue("did", RegexEnum.UltraLoose, true, reqVars.returnType);
            projectVars.did = "msexcelgenerator";

            // Standard data we need to know
            var projectId = request.RetrievePostedValue("pid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var versionId = request.RetrievePostedValue("vid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var outputChannelVariantId = request.RetrievePostedValue("ocvariantid", RegexEnum.Strict, true, reqVars.returnType);
            var outputChannelVariantLanguage = request.RetrievePostedValue("oclang", RegexEnum.Strict, true, reqVars.returnType);

            var serveAsDownloadString = request.RetrievePostedValue("serveasdownload", "(true|false)", false, reqVars.returnType, "false");
            var serveAsDownload = serveAsDownloadString == "true";

            var useContentStatusString = request.RetrievePostedValue("usecontentstatus", "(true|false)", false, reqVars.returnType, "false");
            var useContentStatus = useContentStatusString == "true";

            var hideCurrentPeriodDatapointsString = request.RetrievePostedValue("hidecurrentperioddatapoints", "(true|false)", false, reqVars.returnType, "false");
            var hideCurrentPeriodDatapoints = hideCurrentPeriodDatapointsString == "true";

            var serveAsBinaryString = request.RetrievePostedValue("serveasbinary", "(true|false)", false, reqVars.returnType, "false");
            var serveAsBinary = serveAsBinaryString == "true";

            var forceNumberConversionString = request.RetrievePostedValue("forcenumberconversion", "(true|false)", false, reqVars.returnType, "true");
            var forceNumberConversion = forceNumberConversionString == "true";

            var renderHiddenElementsString = request.RetrievePostedValue("renderhiddenelements", "(true|false)", false, reqVars.returnType, "false");
            var renderHiddenElements = renderHiddenElementsString == "true";
            // renderHiddenElements = true;

            var mode = request.RetrievePostedValue("mode", "normal");


            var renderScope = request.RetrievePostedValue("renderscope", "single-section", RegexEnum.Default, reqVars.returnType);

            var useLoremIpsumString = request.RetrievePostedValue("useloremipsum", "(true|false)", false, reqVars.returnType, "false");
            var useLoremIpsum = useLoremIpsumString == "true";

            // Send when we want to render Excel files in bulk
            var outputChannelVariantIds = request.RetrievePostedValue("ocvariantids", RegexEnum.Default, false, reqVars.returnType, "");

            // Detect if we want to stream the outcome of the PDF rendering as a web-response or as a message over the SignalR connection
            var webSocketModeString = context.Request.RetrievePostedValue("websocketmode", "(true|false)", false, reqVars.returnType, "false");
            var webSocketMode = webSocketModeString == "true";

            if (webSocketMode)
            {
                //
                // => Return data to the web client to close the XHR call
                //
                await context.Response.OK(GenerateSuccessXml("Started PDF generation process", $"projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}"), ReturnTypeEnum.Json, true, true);
                await context.Response.CompleteAsync();
            }


            // Base debug information
            var baseDebugInfo = $"projectId: {projectId}, sectionId: {sectionId}, outputChannelVariantId: {outputChannelVariantId}";

            // Location on the shared folder where we are working with the website data
            var basePath = $"/temp/excel-{projectId}";
            var sharedFoldePathForDocker = RetrieveSharedFolderPathOs(true);
            var sharedFolderWebsiteWorkingPathForDocker = $"{sharedFoldePathForDocker}{basePath}";
            var sharedFolderForApp = RetrieveSharedFolderPathOs();
            var sharedFolderWebsiteWorkingPathForApp = $"{sharedFolderForApp}{basePath}";
            var sourceFolderPathForDocker = $"{sharedFolderWebsiteWorkingPathForDocker}/data";
            var targetFolderPathForDocker = $"{sharedFolderWebsiteWorkingPathForDocker}/excel";



            var errorMessage = "";
            var errorDetails = "";
            var errorMessages = new List<string>();
            var successfullyRenderedOutputChannels = new List<string>();
            var bulkGenerate = false;
            var bulkGenerateCustomSections = false;
            var debugInfo = "";
            try
            {

                List<string> sectionIds = new List<string>();
                if (sectionId.Contains(","))
                {
                    sectionIds.AddRange(sectionId.Split(","));
                }
                else
                {
                    sectionIds.Add(sectionId);
                }

                //
                // => Stylesheet cache
                //
                SetupPdfStylesheetCache(reqVars);


                //
                // => Create a list of output channel variant IDs to be used in this rendering
                //
                var ouputVariantIds = new List<string>();
                if (string.IsNullOrEmpty(outputChannelVariantIds))
                {
                    ouputVariantIds.Add(projectVars.outputChannelVariantId);
                }
                else if (outputChannelVariantIds.Contains(","))
                {
                    ouputVariantIds = outputChannelVariantIds.Split(",").ToList();
                }
                else
                {
                    ouputVariantIds.Add(outputChannelVariantIds);
                }
                bulkGenerate = ouputVariantIds.Count > 1;

                //
                // => Bulk generation of MS Excel files with a custom selection is a special case where we need the hierarchy overview to resolve everything
                //
                var xmlHierarchyOverview = new XmlDocument();
                var level1DataReferences = new List<string>();
                if (bulkGenerate && sectionId != "all")
                {
                    bulkGenerateCustomSections = true;

                    // Retrieve hierarchy overview
                    xmlHierarchyOverview = await RenderOutputChannelHierarchyOverview(projectVars, false, true, true);

                    // Capture the level 1 datareferences
                    foreach (var itemId in sectionIds)
                    {
                        var nodeLevel1Item = xmlHierarchyOverview.SelectSingleNode($"/hierarchies/output_channel[@id='{projectVars.outputChannelVariantId}']/items/structured/item/sub_items/item[@id='{itemId}']");
                        if (nodeLevel1Item != null)
                        {
                            var dataReferenceLevel1 = nodeLevel1Item.GetAttribute("data-ref");
                            if (!string.IsNullOrEmpty(dataReferenceLevel1))
                            {
                                level1DataReferences.Add(dataReferenceLevel1);
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to retrieve a data reference from item ID {itemId}");
                            }
                        }
                    }

                    if (level1DataReferences.Count == 0)
                    {
                        errorMessage = "Cannot bulk generate custom Excel documents";
                        errorDetails = $"No level 1 items found for bulk generation";
                        HandleError(errorMessage, $"{errorDetails}, stack-trace: {GetStackTrace()}");
                    }
                }



                //
                // => Generate the MS Excel file(s)
                //
                var outputWritten = false;
                var msexcelFilesPathOs = new List<string>();
                var msexcelFilesName = new List<string>();
                if (bulkGenerate) await _outputGenerationProgressMessage($"* Start bulk MS Excel generation process *");
                foreach (var currentOutputChannelVariantId in ouputVariantIds)
                {

                    //
                    // => Adjust project variables to match the output channel we are generating the Excel files for
                    //
                    projectVars.outputChannelVariantId = currentOutputChannelVariantId;
                    projectVars.outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);
                    projectVars.editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
                    projectVars.outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);
                    projectVars.reportTypeId = RetrieveReportTypeIdFromProjectId(projectId);
                    SetProjectVariables(context, projectVars);
                    // Console.WriteLine($"ProjectVariables:");
                    // Console.WriteLine(projectVars.DumpToString());


                    //
                    // => Construct a project variables object that we can send to the Project Data Store to retrieve the output channel content with
                    //
                    var projectVariablesForExcelContent = new ProjectVariables
                    {
                        projectId = projectVars.projectId,
                        versionId = "latest",
                        editorContentType = "regular",
                        reportTypeId = projectVars.reportTypeId,
                        outputChannelType = projectVars.outputChannelType,
                        editorId = projectVars.editorId,
                        outputChannelVariantId = projectVars.outputChannelVariantId,
                        outputChannelVariantLanguage = projectVars.outputChannelVariantLanguage
                    };

                    string outputChannelName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(projectVars.outputChannelVariantId)}]/name")?.InnerText ?? "unknown";

                    //
                    // => Dynamically retrieve the section ID's in case we need to render a custom selection in bulk
                    //
                    if (bulkGenerateCustomSections)
                    {
                        var customItemIds = new List<string>();
                        foreach (var level1DataReference in level1DataReferences)
                        {
                            var nodeLevel1Item = xmlHierarchyOverview.SelectSingleNode($"/hierarchies/output_channel[@id='{projectVars.outputChannelVariantId}']/items/structured/item/sub_items/item[@data-ref='{level1DataReference}']");
                            if (nodeLevel1Item != null)
                            {
                                var itemId = nodeLevel1Item.GetAttribute("id");
                                if (!string.IsNullOrEmpty(itemId))
                                {
                                    customItemIds.Add(itemId);
                                    // Find the child nodes of this level 1 hierarchy item
                                    var nodeListSubItems = nodeLevel1Item.SelectNodes("sub_items//item");
                                    foreach (XmlNode nodeSubItem in nodeListSubItems)
                                    {
                                        itemId = nodeSubItem.GetAttribute("id");
                                        if (!string.IsNullOrEmpty(itemId))
                                        {
                                            customItemIds.Add(itemId);
                                        }
                                        else
                                        {
                                            appLogger.LogWarning($"Cannot find an ID for hierarchy item in output channel {projectVars.outputChannelVariantId}");
                                        }
                                    }
                                }
                                else
                                {
                                    appLogger.LogWarning($"Cannot find an ID for level 1 data reference {level1DataReference} in output channel {projectVars.outputChannelVariantId}");
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"Cannot resolve data reference {level1DataReference} in output channel {projectVars.outputChannelVariantId}");
                            }
                        }

                        // Skip the generation of this Excel file if we did not find any sections to generate
                        if (customItemIds.Count == 0)
                        {
                            errorMessage = $"Unable to render {outputChannelName} Excel file (section combination not found)";
                            errorDetails = $"projectVars.outputChannelVariantId: {projectVars.outputChannelVariantId}";

                            // We do not quit the bulk generation process, but we record the problem and move on
                            appLogger.LogError($"{errorMessage}: {errorDetails}");
                            errorMessages.Add(errorMessage);
                            await _outputGenerationProgressMessage($"WARNING: {errorMessage}");
                            continue;
                        }

                        // Adjust the sections that we want to render based on the item ID's that we have found
                        sectionIds.Clear();
                        sectionIds.AddRange(customItemIds);
                    }

                    // Construct a properties file for the MS Excel file that we want to generate
                    MsOfficeFileProperties msExcelProperties = new MsOfficeFileProperties
                    {
                        Sections = string.Join(",", sectionIds.ToArray()),
                        RenderScope = renderScope,
                        UseContentStatus = useContentStatus,
                        HideCurrentPeriodDatapoints = hideCurrentPeriodDatapoints,
                        RenderHiddenElements = renderHiddenElements
                    };




                    //
                    // => Paths to use
                    //
                    var excelFileName = $"{projectVars.projectId}-{projectVars.outputChannelVariantId}.xlsx";
                    var msExcelSourceFilePathOs = $"{sharedFolderWebsiteWorkingPathForApp}/excel/{excelFileName}";
                    var msExcelTargetPathOs = $"{dataRootPathOs}/temp/{Path.GetFileNameWithoutExtension(msExcelSourceFilePathOs)}-{currentOutputChannelVariantId}.xslx";

                    //
                    // => Generate the Excel file
                    //
                    await _outputGenerationProgressMessage($"Start generating {outputChannelName} MS Excel document");
                    var excelRenderResult = await ConversionService.RenderMsExcel(msExcelProperties, projectVariablesForExcelContent, msExcelTargetPathOs, debugRoutine);
                    if (!excelRenderResult.Success)
                    {
                        if (!excelRenderResult.Message.ToLower().StartsWith("warning:"))
                        {
                            await _outputGenerationProgressMessage($"ERROR: {excelRenderResult.Message}");
                        }
                        else
                        {
                            await _outputGenerationProgressMessage(excelRenderResult.Message);
                        }

                        if (bulkGenerate)
                        {
                            // We do not quit the bulk generation process, but we record the problem and move on
                            errorMessages.Add(excelRenderResult.Message);

                            continue;
                        }

                        throw new Exception(excelRenderResult.Message);
                    }
                    await _outputGenerationProgressMessage(excelRenderResult.Message);

                    //
                    // => Remember key information about the Excel file we have just rendered
                    //
                    successfullyRenderedOutputChannels.Add(currentOutputChannelVariantId);
                    msexcelFilesPathOs.Add(msExcelTargetPathOs);
                    msexcelFilesName.Add(Path.GetFileName(msExcelTargetPathOs));
                }


                if (!outputWritten)
                {
                    if (msexcelFilesName.Count == 0)
                    {
                        throw new Exception($"No tables found to base the MS Excel file{((errorMessages.Count > 1) ? "s" : "")} on");
                    }

                    if (serveAsBinary)
                    {
                        // Stream the MS Word file to the client
                        await StreamFile(context.Response, msexcelFilesName[0], msexcelFilesPathOs[0], serveAsDownload, reqVars.returnType);
                    }
                    else
                    {

                        if (!bulkGenerate)
                        {
                            // The result is a single file
                            var msWordFileName = msexcelFilesName[0];

                            // Render the response for the client
                            await RenderResponse(msWordFileName);
                        }
                        else
                        {
                            // The result is a number of files, so we need to bundle them in a zip
                            var folderPathOs = Path.GetDirectoryName(msexcelFilesPathOs[0]);

                            var downloadFileName = "bulk-msexcel-generation.zip";
                            var downloadFilePathOs = $"{folderPathOs}/{downloadFileName}";
                            try
                            {
                                if (File.Exists(downloadFilePathOs)) File.Delete(downloadFilePathOs);
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, $"Unable to remove bulk generation bundle");
                            }

                            using (var stream = File.OpenWrite(downloadFilePathOs))
                            using (ZipArchive archive = new ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
                            {
                                var fileCounter = 0;
                                foreach (var msExcelPathOs in msexcelFilesPathOs)
                                {
                                    var fileName = Path.GetFileName(msExcelPathOs);
                                    try
                                    {
                                        fileName = $"{projectVars.projectId}-{successfullyRenderedOutputChannels[fileCounter]}.xlsx";
                                    }
                                    catch (Exception ex)
                                    {
                                        appLogger.LogWarning(ex, $"Unable to render a filename using the outputchannel as a reference");
                                    }
                                    archive.CreateEntryFromFile(msExcelPathOs, fileName, CompressionLevel.Optimal);
                                    fileCounter++;
                                }
                            }

                            // Render the response for the client
                            await RenderResponse(downloadFileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "Failed to generate Excel file");
                await response.Error(new TaxxorReturnMessage(false, ex.Message, baseDebugInfo), reqVars.returnType, true);
            }



            /// <summary>
            /// Write a response to the client
            /// </summary>
            /// <param name="downloadFileName"></param>
            /// <returns></returns>
            async Task RenderResponse(string downloadFileName)
            {

                // Main message to return
                var successMessage = $"Successfully rendered the MS Excel file.";
                if (bulkGenerate)
                {
                    successMessage = "Successfully finalized bulk MS Excel file generation process";
                    if (errorMessages.Count > 0)
                    {
                        // We return a warning message as some of the Excel's in the original request could not be generated
                        successMessage += $" (failed to generate {errorMessages.Count} MS Excel file{((errorMessages.Count > 1) ? "s" : "")})";
                    }
                    await _outputGenerationProgressMessage($"* {successMessage} *");
                }

                // Stream the response to the client
                if (webSocketMode)
                {
                    dynamic jsonData = new ExpandoObject();
                    jsonData.result = new ExpandoObject();

                    jsonData.result.message = successMessage;
                    jsonData.result.filename = downloadFileName;

                    var json = (string)ConvertToJson(jsonData);


                    await MessageToCurrentClient("ExcelGenerationDone", new TaxxorReturnMessage(true, successMessage, json, ""));
                }
                else
                {
                    switch (reqVars.returnType)
                    {
                        case ReturnTypeEnum.Json:
                            dynamic jsonData = new ExpandoObject();
                            jsonData.result = new ExpandoObject();

                            jsonData.result.message = successMessage;
                            jsonData.result.filename = downloadFileName;

                            if ((siteType == "local" || siteType == "dev") && debugInfo != "")
                            {
                                //jsonData.result.debuginfo = debuggerInfo;
                            }

                            var json = (string)ConvertToJson(jsonData);
                            await context.Response.OK(json, ReturnTypeEnum.Json, true);
                            break;

                        case ReturnTypeEnum.Xml:
                            XmlDocument? xmlResponse = RetrieveTemplate("success_xml");
                            xmlResponse.SelectSingleNode("/result/message").InnerText = successMessage;

                            XmlElement nodeToBeAdded = xmlResponse.CreateElement("filename");
                            nodeToBeAdded.InnerText = downloadFileName;
                            xmlResponse.DocumentElement.AppendChild(nodeToBeAdded);

                            if ((siteType == "local" || siteType == "dev") && debugInfo != "")
                            {
                                xmlResponse.SelectSingleNode("/result/debuginfo").InnerText = debugInfo;
                            }

                            await context.Response.OK(xmlResponse, ReturnTypeEnum.Xml, false);
                            break;

                        default:
                            var message = $"{successMessage}. Filename: {downloadFileName}";

                            if ((siteType == "local" || siteType == "dev") && debugInfo != "") message += ". Debuginfo: " + debugInfo;

                            await context.Response.OK(message, ReturnTypeEnum.Txt, true);
                            break;
                    }
                }


            }
        }




        /// <summary>
        /// Renders an MS Word file file based on the posted variables and returns the result to the client
        /// </summary>
        /// <param name="returnType"></param>
        public static async Task RenderMsWord(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = reqVars.isDebugMode == true || siteType == "local" || siteType == "dev";

            // Standard: {pid: '<<project_id>>', vid: '<<version_id>>', ocvariantid, '<<output channel id>>', oclang: '<<output channel language>>', did: 'all'}
            // Optional: {serveasdownload: 'true|false', serveasbinary: 'true|false', mode, 'normal|diff', base: 'baseversion-id', latest: 'current|latestversion-id'}

            // Retrieve posted values
            var sectionId = projectVars.did;
            projectVars.did = "mswordgenerator";

            // Standard data we need to know
            var projectId = request.RetrievePostedValue("pid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var versionId = request.RetrievePostedValue("vid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var outputChannelVariantId = request.RetrievePostedValue("ocvariantid", RegexEnum.Strict, true, reqVars.returnType);
            var outputChannelVariantLanguage = request.RetrievePostedValue("oclang", RegexEnum.Strict, true, reqVars.returnType);

            var serveAsDownloadString = request.RetrievePostedValue("serveasdownload", "(true|false)", false, reqVars.returnType, "false");
            var serveAsDownload = serveAsDownloadString == "true";

            var serveAsBinaryString = request.RetrievePostedValue("serveasbinary", "(true|false)", false, reqVars.returnType, "false");
            var serveAsBinary = serveAsBinaryString == "true";

            var renderSignatureMarksString = request.RetrievePostedValue("signaturemarks", "(true|false)", false, reqVars.returnType, "true");
            var renderSignatureMarks = renderSignatureMarksString == "true";

            var mode = request.RetrievePostedValue("mode", "normal");

            var html = request.RetrievePostedValue("html", "", false, reqVars.returnType);

            var renderScope = request.RetrievePostedValue("renderscope", "single-section", RegexEnum.Default, reqVars.returnType);

            var useLoremIpsumString = request.RetrievePostedValue("useloremipsum", "(true|false)", false, reqVars.returnType, "false");
            var useLoremIpsum = useLoremIpsumString == "true";

            var useContentStatusString = request.RetrievePostedValue("usecontentstatus", "(true|false)", false, reqVars.returnType, "false");
            var useContentStatus = useContentStatusString == "true";

            var hideCurrentPeriodDatapointsString = request.RetrievePostedValue("hidecurrentperioddatapoints", "(true|false)", false, reqVars.returnType, "false");
            var hideCurrentPeriodDatapoints = hideCurrentPeriodDatapointsString == "true";

            var renderHiddenElementsString = request.RetrievePostedValue("renderhiddenelements", "(true|false)", false, reqVars.returnType, "false");
            var renderHiddenElements = renderHiddenElementsString == "true";

            // Send when we want to render MS Word files in bulk
            var outputChannelVariantIds = request.RetrievePostedValue("ocvariantids", RegexEnum.Default, false, reqVars.returnType, "");

            // Detect if we want to stream the outcome of the MS Word rendering as a web-response or as a message over the SignalR connection
            var webSocketModeString = request.RetrievePostedValue("websocketmode", "(true|false)", false, reqVars.returnType, "false");
            var webSocketMode = webSocketModeString == "true";

            if (webSocketMode)
            {
                //
                // => Return data to the web client to close the XHR call
                //
                await context.Response.OK(GenerateSuccessXml("Started MS Word generation process", $"projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}"), ReturnTypeEnum.Json, true, true);
                await context.Response.CompleteAsync();
            }

            var errorMessage = "";
            var errorDetails = "";
            var errorMessages = new List<string>();
            var successfullyRenderedOutputChannels = new List<string>();
            var bulkGenerate = false;
            var bulkGenerateCustomSections = false;
            var debugInfo = "";

            try
            {
                //
                // => Create a list of output channel variant IDs to be used in this rendering
                //
                var ouputVariantIds = new List<string>();
                if (string.IsNullOrEmpty(outputChannelVariantIds))
                {
                    ouputVariantIds.Add(projectVars.outputChannelVariantId);
                }
                else if (outputChannelVariantIds.Contains(","))
                {
                    ouputVariantIds = outputChannelVariantIds.Split(",").ToList();
                }
                else
                {
                    ouputVariantIds.Add(outputChannelVariantIds);
                }
                bulkGenerate = ouputVariantIds.Count > 1;

                //
                // => Bulk generation of MS Word files with a custom selection is a special case where we need the hierarchy overview to resolve everything
                //
                var xmlHierarchyOverview = new XmlDocument();
                var level1DataReferences = new List<string>();
                if (bulkGenerate && sectionId != "all")
                {
                    bulkGenerateCustomSections = true;

                    // Retrieve hierarchy overview
                    xmlHierarchyOverview = await RenderOutputChannelHierarchyOverview(projectVars, false, true, true);

                    // Capture the level 1 datareferences
                    var sectionIds = new List<string>();
                    if (sectionId.Contains(","))
                    {
                        sectionIds = sectionId.Split(",").ToList();
                    }
                    else
                    {
                        sectionIds.Add(sectionId);
                    }

                    foreach (var itemId in sectionIds)
                    {
                        var nodeLevel1Item = xmlHierarchyOverview.SelectSingleNode($"/hierarchies/output_channel[@id='{projectVars.outputChannelVariantId}']/items/structured/item/sub_items/item[@id='{itemId}']");
                        if (nodeLevel1Item != null)
                        {
                            var dataReferenceLevel1 = nodeLevel1Item.GetAttribute("data-ref");
                            if (!string.IsNullOrEmpty(dataReferenceLevel1))
                            {
                                level1DataReferences.Add(dataReferenceLevel1);
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to retrieve a data reference from item ID {itemId}");
                            }
                        }
                    }

                    if (level1DataReferences.Count == 0)
                    {
                        errorMessage = "Cannot bulk generate custom PDF documents";
                        errorDetails = $"No level 1 items found for bulk generation";
                        HandleError(errorMessage, $"{errorDetails}, stack-trace: {GetStackTrace()}");
                    }
                }


                //
                // => Stylesheet cache
                //
                SetupPdfStylesheetCache(reqVars);


                //
                // => Generate the MS Word file(s)
                //
                var mswordFilesPathOs = new List<string>();
                var mswordFilesName = new List<string>();
                if (bulkGenerate) await _outputGenerationProgressMessage($"* Start bulk MS Word generation process *");
                foreach (var currentOutputChannelVariantId in ouputVariantIds)
                {
                    projectVars.outputChannelVariantId = currentOutputChannelVariantId;
                    projectVars.outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);
                    SetProjectVariables(context, projectVars);
                    // Console.WriteLine($"ProjectVariables:");
                    // Console.WriteLine(projectVars.DumpToString());

                    // Message to the client
                    string outputChannelName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(projectVars.outputChannelVariantId)}]/name")?.InnerText ?? "unknown";
                    await _outputGenerationProgressMessage($"Start generating {outputChannelName} MS Word document");


                    debugInfo += $"outputChannelVariantId: {projectVars.outputChannelVariantId} => (sectionId: {sectionId}, projectId: {projectVars.projectId}, versionId: {projectVars.versionId})";


                    // Construct a project variables object that we can send to the Taxxor Pandoc Service
                    var projectVariablesForMsWordGeneration = new ProjectVariables
                    {
                        // Fill with variables passed to this function
                        projectId = projectVars.projectId,
                        versionId = projectVars.versionId,

                        // Fill with variables retrieved from the context    
                        editorContentType = projectVars.editorContentType ?? "regular",
                        reportTypeId = projectVars.reportTypeId ?? RetrieveReportTypeIdFromProjectId(projectId),
                        outputChannelType = projectVars.outputChannelType,
                        editorId = RetrieveEditorIdFromProjectId(projectVars.projectId),
                        outputChannelVariantId = projectVars.outputChannelVariantId,
                        outputChannelVariantLanguage = projectVars.outputChannelVariantLanguage ?? projectVars.outputChannelDefaultLanguage
                    };


                    // Construct a properties file for the MS Word file that we want to generate
                    MsOfficeFileProperties msWordProperties = new()
                    {
                        Sections = sectionId,
                        Mode = mode,
                        Html = html,
                        RenderScope = renderScope,
                        SignatureMarks = renderSignatureMarks,
                        UseLoremIpsum = useLoremIpsum,
                        UseContentStatus = useContentStatus,
                        HideCurrentPeriodDatapoints = hideCurrentPeriodDatapoints,
                        RenderHiddenElements = renderHiddenElements
                    };

                    //
                    // => Dynamically retrieve the section ID's in case we need to render a custom selection in bulk
                    //
                    if (bulkGenerateCustomSections)
                    {
                        var customItemIds = new List<string>();
                        foreach (var level1DataReference in level1DataReferences)
                        {
                            var nodeLevel1Item = xmlHierarchyOverview.SelectSingleNode($"/hierarchies/output_channel[@id='{projectVars.outputChannelVariantId}']/items/structured/item/sub_items/item[@data-ref='{level1DataReference}']");
                            if (nodeLevel1Item != null)
                            {
                                var itemId = nodeLevel1Item.GetAttribute("id");
                                if (!string.IsNullOrEmpty(itemId))
                                {
                                    customItemIds.Add(itemId);
                                    // Find the child nodes of this level 1 hierarchy item
                                    var nodeListSubItems = nodeLevel1Item.SelectNodes("sub_items//item");
                                    foreach (XmlNode nodeSubItem in nodeListSubItems)
                                    {
                                        itemId = nodeSubItem.GetAttribute("id");
                                        if (!string.IsNullOrEmpty(itemId))
                                        {
                                            customItemIds.Add(itemId);
                                        }
                                        else
                                        {
                                            appLogger.LogWarning($"Cannot find an ID for hierarchy item in output channel {projectVars.outputChannelVariantId}");
                                        }
                                    }
                                }
                                else
                                {
                                    appLogger.LogWarning($"Cannot find an ID for level 1 data reference {level1DataReference} in output channel {projectVars.outputChannelVariantId}");
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"Cannot resolve data reference {level1DataReference} in output channel {projectVars.outputChannelVariantId}");
                            }
                        }

                        // Skip the generation of this MS Word if we did not find any sections to generate
                        if (customItemIds.Count == 0)
                        {
                            errorMessage = $"Unable to render {outputChannelName} Word file (section combination not found)";
                            errorDetails = $"projectVars.outputChannelVariantId: {projectVars.outputChannelVariantId}";

                            // We do not quit the bulk generation process, but we record the problem and move on
                            appLogger.LogError($"{errorMessage}: {errorDetails}");
                            errorMessages.Add(errorMessage);
                            await _outputGenerationProgressMessage($"WARNING: {errorMessage}");
                            continue;
                        }

                        // Adjust the sections that we want to render based on the item ID's that we have found
                        msWordProperties.Sections = string.Join(",", customItemIds.ToArray());
                    }



                    // Generate the MS Word and return the result in the correct format
                    if (serveAsBinary)
                    {

                        // Calculate MS Word filename to use when we will be serving the MS Word as a download or when we want to store it on the disk somewhere
                        var msWordDownloadFileName = "document.docx";
                        try
                        {
                            msWordDownloadFileName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']/name").InnerText.ToLower().Replace(" ", "-");
                            msWordDownloadFileName += $"_{msWordProperties.Sections}_{msWordProperties.Mode}_{outputChannelVariantLanguage}.docx";
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogWarning($"Could not compose a nice filename for the MS Word download. error: {ex}, stack-trace: {GetStackTrace()}");
                        }

                        // Call the MS Word service, grab the result (binary or base64) and pass it directly to the client
                        string base64MsWordContent = await ConversionService.RenderMsWord<string>(msWordProperties, projectVariablesForMsWordGeneration, null, debugRoutine);

                        // Check if all went well
                        if (base64MsWordContent.StartsWith("ERROR:"))
                        {
                            HandleError(ReturnTypeEnum.Html, "Error rendering MS Word file", $"recieved: {base64MsWordContent}, stack-trace: {GetStackTrace()}");
                        }
                        else
                        {
                            await _outputGenerationProgressMessage("Successfully rendered MS Word file");
                        }

                        // Convert the Base64 blob into a bytearray and then return it in the browser
                         byte[] bytes = Base64DecodeToBytes(base64MsWordContent);

                        // Set the right headers
                        SetHeaders(context.Response, msWordDownloadFileName, GetContentType("docx"), Convert.ToInt32(bytes.Length), serveAsDownload);

                        // Dump the binary content into the body and stream it to the client
                        await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);

                        break;
                    }


                    XmlDocument msWordRenderResult = await ConversionService.RenderMsWord<XmlDocument>(msWordProperties, projectVariablesForMsWordGeneration, null, debugRoutine);
                    if (XmlContainsError(msWordRenderResult))
                    {
                        errorMessage = $"Error rendering {outputChannelName} MS Word";
                        errorDetails = $"recieved: {msWordRenderResult.OuterXml}, stack-trace: {GetStackTrace()}";
                        if (bulkGenerate)
                        {
                            // We do not quit the bulk generation process, but we record the problem and move on
                            appLogger.LogError($"{errorMessage}. {errorDetails}");
                            errorMessages.Add(errorMessage);
                            await _outputGenerationProgressMessage($"WARNING: {errorMessage}");
                            continue;
                        }

                        HandleError(reqVars.returnType, errorMessage, $"{errorDetails}, stack-trace: {GetStackTrace()}");
                    }

                    await _outputGenerationProgressMessage("Successfully rendered MS Word file");
                    successfullyRenderedOutputChannels.Add(currentOutputChannelVariantId);
                    var msWordFilePathOs = msWordRenderResult.SelectSingleNode("/result/mswordpath").InnerText;
                    mswordFilesPathOs.Add(msWordFilePathOs);
                    var msWordFileName = Path.GetFileName(msWordFilePathOs);
                    mswordFilesName.Add(msWordFileName);

                }


                //
                // => Render a response to the client
                //
                if (!serveAsBinary)
                {
                    if (!bulkGenerate)
                    {
                        // The result is a single file
                        var msWordFileName = mswordFilesName[0];

                        // Render the response for the client
                        await RenderResponse(msWordFileName);
                    }
                    else
                    {
                        // The result is a number of files, so we need to bundle them in a zip
                        var folderPathOs = Path.GetDirectoryName(mswordFilesPathOs[0]);

                        var downloadFileName = "bulk-msword-generation.zip";
                        var downloadFilePathOs = $"{folderPathOs}/{downloadFileName}";
                        try
                        {
                            if (File.Exists(downloadFilePathOs)) File.Delete(downloadFilePathOs);
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Unable to remove bulk generation bundle");
                        }

                        using (var stream = File.OpenWrite(downloadFilePathOs))
                        using (ZipArchive archive = new ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
                        {
                            var fileCounter = 0;
                            foreach (var mswordFilePathOs in mswordFilesPathOs)
                            {
                                var fileName = Path.GetFileName(mswordFilePathOs);
                                try
                                {
                                    fileName = $"{projectVars.projectId}-{successfullyRenderedOutputChannels[fileCounter]}.docx";
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogWarning(ex, $"Unable to render a filename using the outputchannel as a reference");
                                }
                                archive.CreateEntryFromFile(mswordFilePathOs, fileName, CompressionLevel.Optimal);
                                fileCounter++;
                            }
                        }

                        // Render the response for the client
                        await RenderResponse(downloadFileName);
                    }
                }

            }
            catch (Exception ex)
            {
                errorMessage = $"There was a problem generating the MS Word file(s)";
                appLogger.LogError(ex, $"{errorMessage}. {debugInfo}, stack-trace: {GetStackTrace()}");

                var errorReturnMessage = new TaxxorReturnMessage(false, errorMessage);
                if (webSocketMode)
                {
                    await MessageToCurrentClient("WordGenerationDone", errorReturnMessage);
                }
                else
                {
                    errorReturnMessage.DebugInfo = debugInfo;
                    HandleError(errorReturnMessage);
                }
            }


            /// <summary>
            /// Write a response to the client
            /// </summary>
            /// <param name="downloadFileName"></param>
            /// <returns></returns>
            async Task RenderResponse(string downloadFileName)
            {

                // Main message to return
                var successMessage = "Successfully rendered the MS Word file";
                if (bulkGenerate)
                {
                    successMessage = "Successfully finalized bulk MS Word file generation process";
                    if (errorMessages.Count > 0)
                    {
                        // We return a warning message as some of the PDF's in the original request could not be generated
                        successMessage += $" (failed to generate {errorMessages.Count} MS Word file{((errorMessages.Count > 1) ? "s" : "")})";
                    }
                    await _outputGenerationProgressMessage($"* {successMessage} *");
                }

                // Payload (json string containing information about the files generated)
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = successMessage;
                jsonData.result.filename = downloadFileName;
                var json = (string)ConvertToJson(jsonData);

                // Response to client
                if (webSocketMode)
                {
                    await MessageToCurrentClient("WordGenerationDone", new TaxxorReturnMessage(true, successMessage, json, ""));
                }
                else
                {
                    // Stream the response to the client
                    switch (reqVars.returnType)
                    {
                        case ReturnTypeEnum.Json:
                            await context.Response.OK(json, ReturnTypeEnum.Json, true);
                            break;

                        case ReturnTypeEnum.Xml:
                            XmlDocument? xmlResponse = RetrieveTemplate("success_xml");
                            xmlResponse.SelectSingleNode("/result/message").InnerText = successMessage;

                            XmlElement nodeToBeAdded = xmlResponse.CreateElement("filename");
                            nodeToBeAdded.InnerText = downloadFileName;
                            xmlResponse.DocumentElement.AppendChild(nodeToBeAdded);

                            if ((siteType == "local" || siteType == "dev") && debugInfo != "")
                            {
                                xmlResponse.SelectSingleNode("/result/debuginfo").InnerText = debugInfo;
                            }

                            await context.Response.OK(xmlResponse, ReturnTypeEnum.Xml, false);
                            break;

                        default:
                            var message = $"{successMessage}. Filename: {downloadFileName}";

                            if ((siteType == "local" || siteType == "dev") && debugInfo != "") message += ". Debuginfo: " + debugInfo;

                            await context.Response.OK(message, ReturnTypeEnum.Txt, true);
                            break;
                    }
                }



            }



        }


        /// <summary>
        /// Adds HTML web page chrome (head, title, body) to the XHTML document that the Taxxor Pandoc Service can use as a basis for a MS Word file
        /// </summary>
        /// <param name="msWordHtml"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage CreateCompleteMsWordHtml(string msWordHtml, RequestVariables reqVars, ProjectVariables projectVars, MsOfficeFileProperties msWordProperties = null)
        {
            var xslPathOs = CalculateFullPathOs("cms_xsl_pdfgenerator-htmlchrome", reqVars);
            if (File.Exists(xslPathOs))
            {
                var xmlMsWordHtml = new XmlDocument();
                try
                {
                    xmlMsWordHtml.LoadHtml(msWordHtml);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not load XHTML for MS Word file generation", $"msWordHtml: {TruncateString(msWordHtml, 500)} error: {ex}, stack-trace: {GetStackTrace()}");
                }

                // Create a temporary token
                var token = AccessToken.GenerateToken(120);

                XsltArgumentList xsltArguments = new XsltArgumentList();
                xsltArguments.AddParam("pageId", "", reqVars.pageId);
                xsltArguments.AddParam("editorId", "", projectVars.editorId);
                xsltArguments.AddParam("variantId", "", projectVars.outputChannelVariantId);
                xsltArguments.AddParam("date", "", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"));
                xsltArguments.AddParam("reportTypeId", "", projectVars.reportTypeId);
                xsltArguments.AddParam("projectRootPath", "", projectVars.projectRootPath);
                // Use the parameter below to define if you want to include the PDF css stylesheet directly in the HTML 
                xsltArguments.AddParam("includePdfCssStylesheet", "", "true");
                xsltArguments.AddParam("generatedformat", "", "msword");
                xsltArguments.AddParam("baseurl", "", LocalWebAddressDomain);
                xsltArguments.AddParam("token", "", token);
                xsltArguments.AddParam("taxxorClientId", "", TaxxorClientId);
                xsltArguments.AddParam("reportcaption", "", "");

                if (msWordProperties != null)
                {
                    xsltArguments.AddParam("renderScope", "", msWordProperties.RenderScope);
                    xsltArguments.AddParam("sections", "", msWordProperties.Sections);
                    xsltArguments.AddParam("signature-marks", "", msWordProperties.SignatureMarks ? "yes" : "no");
                }
                else
                {
                    xsltArguments.AddParam("renderScope", "", "unknown");
                    xsltArguments.AddParam("sections", "", "unknown");
                }

                // Retrieve the client-side assets (JS and CSS)
                var xmlAssets = Extensions.RenderOutputChannelCssAndJs(projectVars, "msword");
                xsltArguments.AddParam("clientassets", "", xmlAssets.DocumentElement);

                xmlMsWordHtml = TransformXmlToDocument(xmlMsWordHtml, xslPathOs, xsltArguments);

                // Convert to dynamically calculated header tags
                xmlMsWordHtml = ConvertToHierarchicalHeaders(xmlMsWordHtml);

                return new TaxxorReturnMessage(true, "Successfully created complete Word XHTML", xmlMsWordHtml);
            }
            else
            {
                return new TaxxorReturnMessage(false, "Could not load XSLT to create XHTML for MS Word file generation", $"xslPathOs: {xslPathOs}, stack-trace: {GetStackTrace()}");
            }

        }


    }
}