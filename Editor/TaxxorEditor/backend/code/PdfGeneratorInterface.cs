using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.Extensions.Logging;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    /// <summary>
    /// C# utilities to interact with the PDF Generator
    /// </summary>

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders a PDF file based on the posted variables and returns the result to the client
        /// </summary>
        /// <param name="returnType"></param>
        public static async Task RenderPdf(ReturnTypeEnum returnType = ReturnTypeEnum.Html)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Dump the total rendering time of the PDF in the log of the system so that we have an idea of it's performance
            var timePdfRendering = (siteType == "prod") ? false : true;
            Stopwatch? watch = null;

            // Start timing the generation
            if (timePdfRendering) watch = Stopwatch.StartNew();

            var debugRoutine = (reqVars.isDebugMode == true || siteType == "local" || siteType == "dev");

            // Standard: {pid: '<<project_id>>', vid: '<<version_id>>', ocvariantid, '<<output channel id>>', oclang: '<<output channel language>>', did: 'all'}
            // Optional: {serveasdownload: 'true|false', serveasbinary: 'true|false', mode, 'normal|diff', base: 'baseversion-id', latest: 'current|latestversion-id'}

            // Retrieve posted values
            var sectionId = context.Request.RetrievePostedValue("did", RegexEnum.DefaultLong, true, returnType);
            projectVars.did = "pdfgenerator";

            // Standard data we need to know
            var projectId = projectVars.projectId;
            var versionId = projectVars.versionId;
            var outputChannelVariantId = projectVars.outputChannelVariantId;
            var outputChannelVariantLanguage = projectVars.outputChannelVariantLanguage;

            var serveAsDownloadString = context.Request.RetrievePostedValue("serveasdownload", "(true|false)", false, returnType, "false");
            var serveAsDownload = serveAsDownloadString == "true";

            var serveAsBinaryString = context.Request.RetrievePostedValue("serveasbinary", "(true|false)", false, returnType, "false");
            var serveAsBinary = serveAsBinaryString == "true";

            var renderSignatureMarksString = context.Request.RetrievePostedValue("signaturemarks", "(true|false)", false, reqVars.returnType, "true");
            var renderSignatureMarks = renderSignatureMarksString == "true";


            var mode = context.Request.RetrievePostedValue("mode", "normal");
            var _base = context.Request.RetrievePostedValue("base", false, returnType);
            var latest = context.Request.RetrievePostedValue("latest", false, returnType);

            var html = context.Request.RetrievePostedValue("html", "", false, returnType);

            var securePdfString = context.Request.RetrievePostedValue("secured", "(true|false)", false, returnType, "false");
            var securePdf = securePdfString == "true";

            var printReadyString = context.Request.RetrievePostedValue("printready", "(true|false)", false, returnType, "false");
            var printReady = printReadyString == "true";

            var landscapeString = context.Request.RetrievePostedValue("landscape", "(true|false)", false, returnType, "false");
            var landscape = landscapeString == "true";

            var postProcess = context.Request.RetrievePostedValue("postprocess", RegexEnum.Default, false, returnType, "none");

            var layoutDetails = RetrieveOutputChannelDefaultLayout(projectVars);

            var layout = context.Request.RetrievePostedValue("layout", RegexEnum.Default, false, returnType, "unknown");
            if (!layoutDetails.Forced)
            {
                // If the layout is not forced into a specific format, then default to whatever has been defined for the output channel
                if (layout == "unknown")
                {
                    layout = "regular";
                    if (!string.IsNullOrEmpty(outputChannelVariantId))
                    {
                        if (string.IsNullOrEmpty(projectVars.editorId)) projectVars.editorId = RetrieveEditorIdFromProjectId(projectId);

                        layout = layoutDetails.Layout;
                    }
                }
                if (landscape) layout = "landscape";
            }
            else
            {
                layout = layoutDetails.Layout;
            }


            var renderScope = context.Request.RetrievePostedValue("renderscope", "single-section", RegexEnum.Default, returnType);

            // Special versions of the PDF
            var tablesOnlyString = context.Request.RetrievePostedValue("tablesonly", "(true|false)", false, returnType, "false");
            var tablesOnly = tablesOnlyString == "true";

            var useLoremIpsumString = context.Request.RetrievePostedValue("useloremipsum", "(true|false)", false, returnType, "false");
            var useLoremIpsum = useLoremIpsumString == "true";

            var useContentStatusString = context.Request.RetrievePostedValue("usecontentstatus", "(true|false)", false, returnType, "false");
            var useContentStatus = useContentStatusString == "true";

            var hideCurrentPeriodDatapointsString = context.Request.RetrievePostedValue("hidecurrentperioddatapoints", "(true|false)", false, reqVars.returnType, "false");
            var hideCurrentPeriodDatapoints = hideCurrentPeriodDatapointsString == "true";

            var disableExternalLinksString = context.Request.RetrievePostedValue("disableexternallinks", "(true|false)", false, returnType, "false");
            var disableExternalLinks = disableExternalLinksString == "true";

            // Hide sync errors in the generated PDF
            var hideErrorsString = context.Request.RetrievePostedValue("hideerrors", "(true|false)", false, returnType, "false");
            var hideErrors = hideErrorsString == "true";

            // Send when we want to render PDF files in bulk
            var outputChannelVariantIds = context.Request.RetrievePostedValue("ocvariantids", RegexEnum.Default, false, returnType, "");

            // Detect if we want to stream the outcome of the PDF rendering as a web-response or as a message over the SignalR connection
            var webSocketModeString = context.Request.RetrievePostedValue("websocketmode", "(true|false)", false, returnType, "false");
            var webSocketMode = webSocketModeString == "true";

            // Make sure that when a print ready flag is set, we will render all sections
            if (printReady) sectionId = "all";

            if (webSocketMode)
            {
                //
                // => Return data to the web client to close the XHR call
                //
                await context.Response.OK(GenerateSuccessXml("Started PDF generation process", $"projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}"), ReturnTypeEnum.Json, true, true);
                await context.Response.CompleteAsync();
            }


            var errorMessage = "";
            var errorDetails = "";
            var errorMessages = new List<string>();
            var bulkGenerate = false;
            var bulkGenerateCustomSections = false;
            var debugInfo = "";
            var debugInfoToReturn = "";
            var successfullyRenderedOutputChannels = new List<string>();

            try
            {
                //
                // => Test if we have received all the required parameters for a track changes PDF
                //
                if (mode == "diff")
                {
                    if (string.IsNullOrEmpty(_base) || string.IsNullOrEmpty(latest))
                    {
                        HandleError(returnType, "Not enough valid information supplied", $"mode: {mode}, _base: {_base}, latest: {latest}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        if (!RegExpTest(@"^(current|v\d+\.\d+)$", _base))
                        {
                            HandleError(returnType, "Not enough valid information supplied", $"_base parameter did not pass the validation pattern, mode: {mode}, _base: {_base}, latest: {latest}, stack-trace: {GetStackTrace()}");
                        }
                        else if (!RegExpTest(@"^(current|v\d+\.\d+)$", latest))
                        {
                            HandleError(returnType, "Not enough valid information supplied", $"latest parameter did not pass the validation pattern, mode: {mode}, _base: {_base}, latest: {latest}, stack-trace: {GetStackTrace()}");
                        }
                    }

                }

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
                // => Bulk generation of PDF's with a custom selection is a special case where we need the hierarchy overview to resolve everything
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
                // => Generate the PDF(s)
                //
                var pdfsFilePathOs = new List<string>();
                var pdfsFileName = new List<string>();
                if (bulkGenerate) await _outputGenerationProgressMessage($"* Start bulk PDF generation process *");
                foreach (var currentOutputChannelVariantId in ouputVariantIds)
                {
                    projectVars.outputChannelVariantId = currentOutputChannelVariantId;
                    projectVars.outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);
                    SetProjectVariables(context, projectVars);
                    // Console.WriteLine($"ProjectVariables:");
                    // Console.WriteLine(projectVars.DumpToString());

                    // Message to the client
                    string outputChannelName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(projectVars.outputChannelVariantId)}]/name")?.InnerText ?? "unknown";
                    await _outputGenerationProgressMessage($"Start generating {outputChannelName} PDF");


                    debugInfo += $"outputChannelVariantId: {projectVars.outputChannelVariantId} => (sectionId: {sectionId}, projectId: {projectVars.projectId}, versionId: {projectVars.versionId})";

                    // If we generate a print ready PDF, then we do not want to render sync error markers
                    if (printReady) hideErrors = true;

                    if (bulkGenerate)
                    {
                        layoutDetails = RetrieveOutputChannelDefaultLayout(projectVars);
                        if (layoutDetails.Forced)
                        {
                            layout = RetrieveOutputChannelDefaultLayout(projectVars).Layout;
                        }
                    }

                    //
                    // => Setup PDF properties object
                    //
                    PdfProperties pdfProperties = new()
                    {
                        Sections = sectionId,
                        Mode = mode,
                        Base = _base,
                        Latest = latest,
                        Html = html,
                        RenderScope = renderScope,
                        Secure = securePdf,
                        PrintReady = printReady,
                        Layout = layout,
                        PostProcess = postProcess,
                        SignatureMarks = renderSignatureMarks,
                        TablesOnly = tablesOnly,
                        DisableExternalLinks = disableExternalLinks,
                        UseLoremIpsum = useLoremIpsum,
                        UseContentStatus = useContentStatus,
                        HideCurrentPeriodDatapoints = hideCurrentPeriodDatapoints,
                        HideErrors = hideErrors
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

                        // Skip the generation of this PDF if we did not find any sections to generate
                        if (customItemIds.Count == 0)
                        {
                            errorMessage = $"Unable to render {outputChannelName} PDF (section combination not found)";
                            errorDetails = $"projectVars.outputChannelVariantId: {projectVars.outputChannelVariantId}";

                            // We do not quit the bulk generation process, but we record the problem and move on
                            appLogger.LogError($"{errorMessage}: {errorDetails}");
                            errorMessages.Add(errorMessage);
                            await _outputGenerationProgressMessage($"WARNING: {errorMessage}");
                            continue;
                        }

                        // Adjust the sections that we want to render based on the item ID's that we have found
                        pdfProperties.Sections = string.Join(",", customItemIds.ToArray());
                    }

                    // Retrieve output channel hierarchy to get the top node value and use that as a string to send to the PDF generator
                    string booktitle = "";
                    var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, "pdf", projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                    if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                    {
                        XmlDocument? xmlHierarchyDoc = projectVars.rbacCache.GetHierarchy(outputChannelHierarchyMetadataKey, "full");

                        // If the complete hierarchy cannot be loacted in the rbacCache, then load it fresh from the Project Data Store
                        if (xmlHierarchyDoc == null)
                        {
                            xmlHierarchyDoc = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadHierarchy(projectVars, debugRoutine);
                            if (XmlContainsError(xmlHierarchyDoc))
                            {
                                xmlHierarchyDoc = null;
                            }
                        }

                        if (xmlHierarchyDoc != null)
                        {
                            var nodeRootSection = xmlHierarchyDoc.SelectSingleNode("/items/structured/item[web_page/linkname]");
                            if (nodeRootSection == null)
                            {
                                errorMessage = $"Unable to render {outputChannelName} PDF";
                                errorDetails = $"nodeRootSection: null, outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey}";
                                if (bulkGenerate)
                                {
                                    // We do not quit the bulk generation process, but we record the problem and move on
                                    appLogger.LogError($"{errorMessage}: {errorDetails}");
                                    errorMessages.Add(errorMessage);
                                    await _outputGenerationProgressMessage($"WARNING: {errorMessage}");
                                    continue;
                                }
                                HandleError(errorMessage, $"{errorDetails}, stack-trace: {GetStackTrace()}");
                            }
                            else
                            {
                                booktitle = nodeRootSection.SelectSingleNode("web_page/linkname").InnerText;
                            }
                        }
                        else
                        {
                            errorMessage = $"Unable to render {outputChannelName} PDF";
                            errorDetails = $"xmlHierarchyDoc: null, outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey}";
                            if (bulkGenerate)
                            {
                                // We do not quit the bulk generation process, but we record the problem and move on
                                appLogger.LogError($"{errorMessage}: {errorDetails}");
                                errorMessages.Add(errorMessage);
                                await _outputGenerationProgressMessage($"WARNING: {errorMessage}");
                                continue;
                            }
                            HandleError(errorMessage, $"{errorDetails}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    else
                    {
                        appLogger.LogError($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}'], stack-trace: {GetStackTrace()}");
                    }
                    if (booktitle != "")
                    {
                        pdfProperties.PdfGeneratorStringSet.Add("booktitle", booktitle);
                    }



                    // Generate the PDF and return the result in the correct format
                    if (serveAsBinary)
                    {
                        // Console.WriteLine("****************************");
                        // Console.WriteLine("*** IN BINARY SERVE MODE ***");
                        // Console.WriteLine("****************************");

                        try
                        {
                            byte[]? pdfBytes = await PdfService.RenderPdf<byte[]>(pdfProperties, projectVars, null, debugRoutine);

                            await _outputGenerationProgressMessage("Successfully rendered PDF file");

                            // Calculate PDF filename to use when we will be serving the PDF as a download or when we want to store it on the disk somewhere
                            var pdfDownloadFileName = "document.pdf";
                            try
                            {
                                pdfDownloadFileName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']/name").InnerText.ToLower().Replace(" ", "-");
                                pdfDownloadFileName += $"_{pdfProperties.Sections}_{pdfProperties.Mode}_{outputChannelVariantLanguage}.pdf";
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogWarning($"Could not compose a nice filename for the PDF download. error: {ex}, stack-trace: {GetStackTrace()}");
                            }

                            // Set the right headers
                            SetHeaders(context.Response, pdfDownloadFileName, GetContentType("pdf"), Convert.ToInt32(pdfBytes.Length), serveAsDownload);

                            // Dump the binary content into the body and stream it to the client
                            await context.Response.Body.WriteAsync(pdfBytes);

                            pdfBytes = null;
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Error serving binary PDF. stack-trace: {GetStackTrace()}");
                            byte[]? errorPdf = await RetrieveErrorPdf();

                            // Set the right headers
                            SetHeaders(context.Response, "error.pdf", GetContentType("pdf"), Convert.ToInt32(errorPdf.Length), serveAsDownload);

                            await context.Response.Body.WriteAsync(errorPdf, 0, errorPdf.Length);

                            errorPdf = null;
                        }


                        //
                        // => Exit the loop: PDF streamed as a binary only used for a single PDF
                        //
                        break;
                    }

                    // Console.WriteLine("***********************************");
                    // Console.WriteLine("*** IN DOWNLOAD GENERATION MODE ***");
                    // Console.WriteLine("***********************************");

                    XmlDocument pdfRenderResult = await PdfService.RenderPdf<XmlDocument>(pdfProperties, projectVars, null, debugRoutine);

                    if (XmlContainsError(pdfRenderResult))
                    {
                        errorMessage = $"Error rendering {outputChannelName} PDF";
                        errorDetails = $"recieved: {pdfRenderResult.OuterXml}";
                        if (bulkGenerate)
                        {
                            // We do not quit the bulk generation process, but we record the problem and move on
                            appLogger.LogError($"{errorMessage}: {errorDetails}");
                            errorMessages.Add(errorMessage);
                            await _outputGenerationProgressMessage($"WARNING: {errorMessage}");
                            continue;
                        }
                        HandleError(errorMessage, $"{errorDetails}, stack-trace: {GetStackTrace()}");
                    }


                    await _outputGenerationProgressMessage("Successfully rendered PDF file");
                    successfullyRenderedOutputChannels.Add(currentOutputChannelVariantId);
                    var pdfFilePathOs = pdfRenderResult.SelectSingleNode("/result/pdfpath").InnerText;
                    pdfsFilePathOs.Add(pdfFilePathOs);
                    var pdfFileName = Path.GetFileName(pdfFilePathOs);
                    pdfsFileName.Add(pdfFileName);

                    var debuggerInfo = pdfRenderResult.SelectSingleNode("/result/debuginfo")?.InnerText ?? "";
                    if ((siteType == "local" || siteType == "dev") && debuggerInfo != "")
                    {
                        if (debuggerInfo != "")
                        {
                            debugInfoToReturn += debuggerInfo;
                        }
                        else
                        {
                            debugInfoToReturn += debugInfo;
                        }
                    }
                }

                //
                // => Render a response to the client
                //
                if (!serveAsBinary)
                {
                    if (!bulkGenerate)
                    {
                        // The result is a single file
                        var pdfFileName = pdfsFileName[0];

                        // Render the response for the client
                        await RenderResponse(pdfFileName);
                    }
                    else
                    {
                        // The result is a number of files, so we need to bundle them in a zip
                        var folderPathOs = Path.GetDirectoryName(pdfsFilePathOs[0]);

                        var downloadFileName = "bulk-pdf-generation.zip";
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
                            foreach (var pdfFilePathOs in pdfsFilePathOs)
                            {
                                var fileName = Path.GetFileName(pdfFilePathOs);
                                try
                                {
                                    fileName = $"{projectVars.projectId}-{successfullyRenderedOutputChannels[fileCounter]}.pdf";
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogWarning(ex, $"Unable to render a filename using the outputchannel as a reference");
                                }
                                archive.CreateEntryFromFile(pdfFilePathOs, fileName, CompressionLevel.Optimal);
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
                appLogger.LogError(ex, $"There was a problem generating the PDF. {debugInfo}, stack-trace: {GetStackTrace()}");

                var errorReturnMessage = new TaxxorReturnMessage(false, errorMessage);
                if (webSocketMode)
                {
                    await MessageToCurrentClient("PdfGenerationDone", errorReturnMessage);
                }
                else
                {
                    errorReturnMessage.DebugInfo = debugInfo;
                    HandleError(errorReturnMessage);
                }
            }
            finally
            {
                if (timePdfRendering)
                {
                    watch.Stop();
                    appLogger.LogInformation($"INFO: Rendering of PDF took: {watch.ElapsedMilliseconds.ToString()} ms");
                }
            }

            /// <summary>
            /// Retrieves a special PDF document indicating that something went wrong in the PDF generation process
            /// </summary>
            /// <returns></returns>
            async Task<Byte[]> RetrieveErrorPdf()
            {
                var errorPdfPathOs = $"{dataRootPathOs}/error-global.pdf";
                try
                {
                    return await File.ReadAllBytesAsync(errorPdfPathOs);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Could not load the error PDF document");
                    return null;
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
                var successMessage = "Successfully rendered the PDF file";
                if (bulkGenerate)
                {
                    successMessage = "Successfully finalized bulk PDF generation process";
                    if (errorMessages.Count > 0)
                    {
                        // We return a warning message as some of the PDF's in the original request could not be generated
                        successMessage += $" (failed to generate {errorMessages.Count} PDF file{((errorMessages.Count > 1) ? "s" : "")})";
                    }
                    await _outputGenerationProgressMessage($"* {successMessage} *");
                }

                if (webSocketMode)
                {
                    dynamic jsonData = new ExpandoObject();
                    jsonData.result = new ExpandoObject();

                    jsonData.result.message = successMessage;
                    jsonData.result.filename = downloadFileName;

                    var json = (string)ConvertToJson(jsonData);


                    await MessageToCurrentClient("PdfGenerationDone", new TaxxorReturnMessage(true, successMessage, json, ""));
                }
                else
                {
                    // Stream the response to the client
                    switch (returnType)
                    {
                        case ReturnTypeEnum.Json:
                            dynamic jsonData = new ExpandoObject();
                            jsonData.result = new ExpandoObject();

                            jsonData.result.message = successMessage;
                            jsonData.result.filename = downloadFileName;

                            if ((siteType == "local" || siteType == "dev") && debugInfoToReturn != "")
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

                            if ((siteType == "local" || siteType == "dev") && debugInfoToReturn != "")
                            {
                                xmlResponse.SelectSingleNode("/result/debuginfo").InnerText = debugInfoToReturn;
                            }

                            await context.Response.OK(xmlResponse, ReturnTypeEnum.Xml, false);
                            break;

                        default:
                            var message = $"{successMessage}. Filename: {downloadFileName}";

                            if ((siteType == "local" || siteType == "dev") && debugInfoToReturn != "") message += ". Debuginfo: " + debugInfoToReturn;

                            await context.Response.OK(message, ReturnTypeEnum.Txt, true);
                            break;
                    }
                }


            }
        }


        public static async Task<TaxxorReturnMessage> PreparePagedMediaHtmlForRendering(XmlDocument xmlData, XbrlConversionProperties xbrlConversionProperties)
        {
            // Create a PDF Properties object that we can use
            var pdfProperties = new PdfProperties();
            pdfProperties.Sections = xbrlConversionProperties.Sections;

            return await PreparePdfHtmlForRendering(xmlData, xbrlConversionProperties.XbrlVars, pdfProperties, xbrlConversionProperties);
        }

        /// <summary>
        /// Prepares PDF (XHTML) content for binary rendering
        /// - Summarizes footnotes
        /// - Strips AR / 20-F content and other elements not needed for the rendering
        /// - Insert TOC and Chapter Numbering
        /// - Mark and report on broken links
        /// </summary>
        /// <param name="xmlData"></param>
        /// <param name="projectVarsForPdfGeneration"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> PreparePdfHtmlForRendering(XmlDocument xmlData, ProjectVariables projectVarsForPdfGeneration, PdfProperties pdfProperties, XbrlConversionProperties? xbrlConversionProperties = null)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var outputChannelVariant = (projectVarsForPdfGeneration.outputChannelVariantId.Contains("20f")) ? "20F" : "AR";
            var message = "";
            var errors = new List<string>();
            var warnings = new List<string>();

            int counter = 0;
            string GenerateLogFilePathOs()
            {
                counter++;
                return $"{logRootPathOs}/__1-{counter}_pdf-prepareforrendering.xml";
            }


            try
            {
                //
                // => Summarize the in-text footnotes in the content by grouping them at the end of the chapter in a wrapper element
                //
                if (projectVarsForPdfGeneration.outputChannelVariantId != "qrpdf")
                {
                    // Check if this functionality is enabled
                    var disableInTextFootnoteSummary = (xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='disable-intext-footnote-grouping']")?.InnerText ?? "false") == "true";
                    if (!disableInTextFootnoteSummary) CreateInTextFootnoteSummary(ref xmlData, ref warnings, ref errors);
                }

                //
                // => Run through simplification stylesheet (remove 20-F / AR blocks etc)
                //
                var stylesheetId = "xsl_simplify-source-data";
                var xsltStylesheet = PdfXslCache.Get(stylesheetId);
                if (xsltStylesheet == null)
                {
                    return new TaxxorReturnMessage(false, "Could not load stylesheet", $"stylesheetId: {stylesheetId}, stack-trace: {GetStackTrace()}");
                }

                if (debugRoutine) await xmlData.SaveAsync(GenerateLogFilePathOs(), false);
                var xsltArguments = new XsltArgumentList();
                xsltArguments.AddParam("add-html-chrome", "", "false");
                xsltArguments.AddParam("output-channel-variant", "", outputChannelVariant); // 20F or AR
                xsltArguments.AddParam("post-processing-format", "", ((xbrlConversionProperties == null) ? "PDF" : xbrlConversionProperties.Channel));
                xsltArguments.AddParam("reporting-requirement-scheme", "", ((xbrlConversionProperties == null) ? "" : (xbrlConversionProperties.ReportRequirementScheme ?? "")));
                xsltArguments.AddParam("tablesonly", "", ((pdfProperties.TablesOnly) ? "yes" : "no"));
                xsltArguments.AddParam("footnote-suffix", "", (xmlApplicationConfiguration.SelectSingleNode("/configuration/output_channels/general/footnotes/suffix")?.InnerText ?? ")"));
                if (xbrlConversionProperties == null) xsltArguments.AddParam("pdfrendermode", "", pdfProperties.Mode);


                xmlData = TransformXmlToDocument(xmlData, xsltStylesheet, xsltArguments);
                if (debugRoutine) await xmlData.SaveAsync(GenerateLogFilePathOs(), false);

                //
                // => Insert the TOC and the chapter numbers in the XHTML content
                //
                InsertTableOfContentsAndChapterNumbers(ref xmlData, projectVarsForPdfGeneration, ((xbrlConversionProperties == null) ? "PDF" : xbrlConversionProperties.Channel), pdfProperties);
                if (debugRoutine) await xmlData.SaveAsync(GenerateLogFilePathOs(), false);

                //
                // => Set the language on the aricle nodes
                //
                var nodeListArticles = xmlData.SelectNodes("//article");
                foreach (XmlNode nodeArticle in nodeListArticles)
                {
                    nodeArticle.SetAttribute("lang", projectVarsForPdfGeneration.outputChannelVariantLanguage);
                }

                //
                // => Process internal document links and mark the links that are broken
                //

                // - Only log broken links if we are rendering a full PDF
                var logBrokenLinkMessages = false;
                if (pdfProperties.Sections == "all")
                {
                    logBrokenLinkMessages = true;
                }
                else
                {
                    var rootSectionId = xmlData.SelectSingleNode("//metadata/hierarchy/items/structured/item")?.GetAttribute("id") ?? "nothingthatwillevermatch";
                    if (pdfProperties.Sections == rootSectionId) logBrokenLinkMessages = true;
                }

                if (nodeListArticles.Count > 1 && pdfProperties.Mode != "diff")
                {

                    var nodeListInternalLinks = xmlData.SelectNodes($"//article//{RenderInternalLinkXpathSelector()}");
                    foreach (XmlNode nodeInternalLink in nodeListInternalLinks)
                    {
                        //Console.WriteLine(nodeInternalLink.OuterXml);

                        var targetArticleId = GetAttribute(nodeInternalLink, "href");
                        if (string.IsNullOrEmpty(targetArticleId))
                        {
                            message = $"Link without target in @href detected, {nodeInternalLink.OuterXml}";
                            warnings.Add(message);
                            appLogger.LogWarning(message);
                            SetAttribute(nodeInternalLink, "data-error", "no-target");
                        }
                        else
                        {
                            // Test if the target exists
                            targetArticleId = targetArticleId.SubstringAfter("#");
                            var nodeTargetArticle = xmlData.SelectSingleNode($"//article[@id='{targetArticleId}']");
                            if (nodeTargetArticle == null)
                            {
                                if (IsInternalLinkRelevant(nodeInternalLink, projectVarsForPdfGeneration.outputChannelVariantId))
                                {
                                    var nodeSourceArticle = nodeInternalLink.SelectSingleNode("ancestor::article");
                                    var sourceArticleId = "";
                                    if (nodeSourceArticle != null) sourceArticleId = GetAttribute(nodeSourceArticle, "id");

                                    if (logBrokenLinkMessages)
                                    {
                                        var sourceArticleTitle = nodeSourceArticle.SelectSingleNode("*//*[local-name()='h1' or local-name()='h2' or local-name()='h3']")?.InnerText ?? "";
                                        var linkName = nodeInternalLink?.InnerText ?? "";
                                        message = $"Broken link detected in section '{sourceArticleTitle}' ({sourceArticleId}) pointing to section '{linkName}' ({targetArticleId})";
                                        warnings.Add(message);
                                        appLogger.LogWarning(message);
                                    }

                                    SetAttribute(nodeInternalLink, "data-error", "broken-link");
                                }
                            }
                        }
                    }

                }
                if (debugRoutine) await xmlData.SaveAsync(GenerateLogFilePathOs(), false);


                //
                // => Convert the XML to rewrite the header (h1, h2, h3, ...) and set them based on the hierarchical level of the article
                //
                stylesheetId = "cms_xsl_dynamic-header-levels";
                xsltStylesheet = PdfXslCache.Get(stylesheetId);
                if (xsltStylesheet == null)
                {
                    return new TaxxorReturnMessage(false, "Could not load stylesheet", $"stylesheetId: {stylesheetId}, stack-trace: {GetStackTrace()}");
                }
                xmlData = ConvertToHierarchicalHeaders(xmlData, xsltStylesheet);
                if (debugRoutine) await xmlData.SaveAsync(GenerateLogFilePathOs(), false);


                //
                // => Insert dummy data in empty elements to avoid that they self-close in a later step
                //
                if (pdfProperties.Mode == "diff")
                {
                    InsertDummyEmptyElementData(xmlData);
                    if (debugRoutine) await xmlData.SaveAsync(GenerateLogFilePathOs(), false);
                }



                // TODO: find a way to return information on the broken links that we have found

                //
                // => Package the errors and warnings into an XML structure that we can parse in the caller routine to extract the details
                //
                var debugInfo = new XmlDocument();
                debugInfo.AppendChild(debugInfo.CreateElement("debuginfo"));
                if (warnings.Count > 0)
                {
                    var nodeWarnings = debugInfo.CreateElement("warnings");
                    foreach (var warning in warnings)
                    {
                        var nodeWarning = debugInfo.CreateElementWithText("warning", warning);
                        nodeWarnings.AppendChild(nodeWarning);
                    }
                    debugInfo.DocumentElement.AppendChild(nodeWarnings);
                }
                if (errors.Count > 0)
                {
                    var nodeErrors = debugInfo.CreateElement("errors");
                    foreach (var error in errors)
                    {
                        var nodeError = debugInfo.CreateElementWithText("error", error);
                        nodeErrors.AppendChild(nodeError);
                    }
                    debugInfo.DocumentElement.AppendChild(nodeErrors);
                }

                return new TaxxorReturnMessage(true, "Successfully processed PDF XHTML", xmlData, debugInfo.OuterXml);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error preparing the PDF content", ex.ToString());
            }


        }

        /// <summary>
        /// Adds HTML web page chrome (head, title, body) to the XHTML document that the PDF Service can use as a basis for a PDF file
        /// </summary>
        /// <param name="pdfHtml"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="pdfProperties"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CreateCompletePdfHtml(string pdfHtml, RequestVariables reqVars, ProjectVariables projectVars, PdfProperties pdfProperties = null)
        {
            // var xmlTemp = new XmlDocument();
            // xmlTemp.LoadXml(pdfHtml);
            // var pdfHtmlReworked = TransformXml(xmlTemp, "xsl_identity-transform");


            var xmlPdfHtml = new XmlDocument();
            try
            {
                xmlPdfHtml.LoadHtml(pdfHtml);
                // xmlPdfHtml.LoadXml(pdfHtml);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "Could not load XHTML for PDF", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

            return await CreateCompletePdfHtml(xmlPdfHtml, reqVars, projectVars, pdfProperties);

        }

        /// <summary>
        /// Adds HTML web page chrome (head, title, body) to the XHTML document that the PDF Service can use as a basis for a PDF file
        /// </summary>
        /// <param name="xmlPdfHtml"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="xbrlConversionProperties"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CreateCompletePdfHtml(XmlDocument xmlPdfHtml, RequestVariables reqVars, XbrlConversionProperties xbrlConversionProperties)
        {
            // Create a PDF Properties object that we can use
            var pdfProperties = new PdfProperties();
            pdfProperties.Sections = xbrlConversionProperties.Sections;

            // Add "booktitle" (lagacy PDF property, but required in the XBRL generation to align PDF with XBRL generation)
            pdfProperties.PdfGeneratorStringSet.Add("booktitle", xbrlConversionProperties.ReportCaption);

            return await CreateCompletePdfHtml(xmlPdfHtml, reqVars, xbrlConversionProperties.XbrlVars, pdfProperties, "xbrl");
        }


        /// <summary>
        /// Adds HTML web page chrome (head, title, body) to the XHTML document that the PDF Service can use as a basis for a PDF file
        /// </summary>
        /// <param name="xmlPdfHtml"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="pdfProperties"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CreateCompletePdfHtml(XmlDocument xmlPdfHtml, RequestVariables reqVars, ProjectVariables projectVars, PdfProperties pdfProperties, string generatedFormat = null)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";
            XsltArgumentList xsltArguments = new XsltArgumentList();
            XmlDocument xmlAssets = new XmlDocument();

            int counter = 0;
            string GenerateLogFilePathOs()
            {
                counter++;
                return $"{logRootPathOs}/__2-{counter}_pdf-createcompletepdfhtml.xml";
            }

            if (debugRoutine) await xmlPdfHtml.SaveAsync(GenerateLogFilePathOs(), false);

            try
            {
                //
                // => Generic transformation parameters
                //
                xsltArguments.AddParam("pageId", "", reqVars.pageId);
                xsltArguments.AddParam("editorId", "", projectVars.editorId);
                xsltArguments.AddParam("reportTypeId", "", projectVars.reportTypeId);
                xsltArguments.AddParam("guidLegalEntity", "", projectVars.guidLegalEntity);
                xsltArguments.AddParam("variantId", "", projectVars.outputChannelVariantId);
                xsltArguments.AddParam("date", "", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"));
                xsltArguments.AddParam("projectRootPath", "", projectVars.projectRootPath);
                xsltArguments.AddParam("appversion", "", ProjectLogic.applicationVersion.Replace(".", ""));
                xsltArguments.AddParam("taxxorClientId", "", TaxxorClientId);
                xsltArguments.AddParam("lang", "", projectVars.outputChannelVariantLanguage);

                var reportCaption = "";
                if (pdfProperties != null)
                {
                    if (!pdfProperties.PdfGeneratorStringSet.TryGetValue("booktitle", out reportCaption))
                    {
                        appLogger.LogInformation("Booktitle PDF string is not available");
                    }

                    xsltArguments.AddParam("disableexternallinks", "", pdfProperties.DisableExternalLinks ? "yes" : "no");
                }
                if (reportCaption != null)
                {
                    xsltArguments.AddParam("reportcaption", "", reportCaption);
                }

                //
                // => Output format specific transformation parameters
                //
                switch (generatedFormat)
                {
                    case null: // - PDF

                        xsltArguments.AddParam("includePdfCssStylesheet", "", "true");

                        if (pdfProperties != null)
                        {
                            xsltArguments.AddParam("renderScope", "", pdfProperties.RenderScope);
                            xsltArguments.AddParam("sections", "", pdfProperties.Sections);
                            xsltArguments.AddParam("printready", "", ((pdfProperties.PrintReady) ? "yes" : "no"));
                            xsltArguments.AddParam("landscape", "", (!string.IsNullOrEmpty(pdfProperties.Layout) && pdfProperties.Layout == "landscape") ? "yes" : "no");
                            if (!string.IsNullOrEmpty(pdfProperties.Layout)) xsltArguments.AddParam("layout", "", pdfProperties.Layout);
                            xsltArguments.AddParam("tablesonly", "", ((pdfProperties.TablesOnly) ? "yes" : "no"));
                            xsltArguments.AddParam("signature-marks", "", ((pdfProperties.SignatureMarks) ? "yes" : "no"));
                            xsltArguments.AddParam("hideerrors", "", ((pdfProperties.HideErrors) ? "yes" : "no"));
                            xsltArguments.AddParam("usecontentstatus", "", ((pdfProperties.UseContentStatus) ? "yes" : "no"));
                            xsltArguments.AddParam("mode", "", pdfProperties.Mode);
                        }
                        else
                        {
                            xsltArguments.AddParam("renderScope", "", "unknown");
                            xsltArguments.AddParam("sections", "", "unknown");
                        }

                        break;

                    case "xbrl": // - XBRL
                        xsltArguments.AddParam("includePdfCssStylesheet", "", "false");
                        xsltArguments.AddParam("generatedformat", "", "xbrl");
                        xsltArguments.AddParam("baseurl", "", "");
                        xsltArguments.AddParam("token", "", "");
                        xsltArguments.AddParam("renderScope", "", "include-children");
                        xsltArguments.AddParam("sections", "", pdfProperties.Sections);

                        break;

                    default:
                        return new TaxxorReturnMessage(false, "Error creating complete content", $"Unsupported format: {generatedFormat}");
                }

                //
                // => Retrieve the client-side assets (JS and CSS)
                //
                switch (generatedFormat)
                {
                    case null:
                        // For PDF rendering
                        var renderMode = "normal";
                        if (pdfProperties != null)
                        {
                            if (pdfProperties.PrintReady) renderMode = "print";
                        }
                        var layout = "regular";
                        if (pdfProperties != null)
                        {
                            if (!string.IsNullOrEmpty(pdfProperties.Layout)) layout = pdfProperties.Layout;
                        }
                        xmlAssets = Extensions.RenderOutputChannelCssAndJs(projectVars, renderMode, layout);

                        xsltArguments.AddParam("clientassets", "", xmlAssets.DocumentElement);
                        break;

                    case "xbrl":
                        //For XBRL rendering
                        xmlAssets = Extensions.RenderOutputChannelCssAndJs(projectVars, "xbrl");
                        // Console.WriteLine("****************** HTML CHROME **********************");
                        // Console.WriteLine(PrettyPrintXml(xmlAssets));
                        // Console.WriteLine("****************************************");

                        xsltArguments.AddParam("clientassets", "", xmlAssets.DocumentElement);
                        break;
                }


                // Retrieve the stylesheet from the cache
                var stylesheetId = "cms_xsl_pdfgenerator-htmlchrome";
                var xsltStylesheet = PdfXslCache.Get(stylesheetId);
                if (xsltStylesheet == null)
                {
                    return new TaxxorReturnMessage(false, "Could not load stylesheet", $"stylesheetId: {stylesheetId}, stack-trace: {GetStackTrace()}");
                }

                xmlPdfHtml = TransformXmlToDocument(xmlPdfHtml, xsltStylesheet, xsltArguments);
                if (debugRoutine) await xmlPdfHtml.SaveAsync(GenerateLogFilePathOs(), false);

                // Message to the client
                await _outputGenerationProgressMessage("Successfully created complete source content");

                return new TaxxorReturnMessage(true, "Success", xmlPdfHtml);
            }
            catch (Exception e)
            {
                var errorMessage = "There was an error creating complete source content";
                appLogger.LogError(e, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, $"error: {e.ToString()}");
            }
        }

        /// <summary>
        /// Renders the latest html of a filing
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="sections"></param>
        /// <param name="renderScope"></param>
        /// <returns></returns>
        public static async Task<string> RetrieveLatestPdfHtml(string projectId, string versionId, string sections, bool useContentStatus, bool hideCurrentPeriodDatapoints, string renderScope = "single-section")
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Construct a project variables object that we can send to the PDF Generator
            var projectVariablesForPdfGeneration = new ProjectVariables();

            // Fill with variables passed to this function
            projectVariablesForPdfGeneration.projectId = projectId;
            projectVariablesForPdfGeneration.versionId = versionId;

            // Fill with variables retrieved from the context    
            projectVariablesForPdfGeneration.editorContentType = projectVars.editorContentType ?? "regular";
            projectVariablesForPdfGeneration.reportTypeId = projectVars.reportTypeId ?? RetrieveReportTypeIdFromProjectId(projectId);
            projectVariablesForPdfGeneration.outputChannelType = projectVars.outputChannelType;
            projectVariablesForPdfGeneration.editorId = RetrieveEditorIdFromProjectId(projectId);
            projectVariablesForPdfGeneration.outputChannelVariantId = RetrieveFirstOutputChannelVariantIdFromEditorId(projectVars.editorId);
            if (!string.IsNullOrEmpty(projectVars.outputChannelVariantId)) projectVariablesForPdfGeneration.outputChannelVariantId = projectVars.outputChannelVariantId;
            projectVariablesForPdfGeneration.outputChannelVariantLanguage = projectVars.outputChannelVariantLanguage ?? projectVars.outputChannelDefaultLanguage;

            // Retrieve the full XHML file
            return await RetrieveLatestPdfHtml(projectVariablesForPdfGeneration, sections, useContentStatus, hideCurrentPeriodDatapoints, renderScope);
        }

        /// <summary>
        /// Renders the latest html of a filing
        /// </summary>
        /// <param name="projectVariablesForPdfGeneration"></param>
        /// <param name="sections"></param>
        /// <param name="renderScope"></param>
        /// <returns></returns>
        public static async Task<string> RetrieveLatestPdfHtml(ProjectVariables projectVariablesForPdfGeneration, string sections, bool useContentStatus, bool hideCurrentPeriodDatapoints, string renderScope = "single-section")
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Retrieve the full XHML file from the Taxxor Document Store
            var xmlDoc = await DocumentStoreService.PdfData.Load(projectVariablesForPdfGeneration, sections, useContentStatus, renderScope, hideCurrentPeriodDatapoints, true);

            if (XmlContainsError(xmlDoc)) HandleError(ReturnTypeEnum.Html, xmlDoc);

            // Render XHTML from the data we have received using an XSLT translation
            return await PdfGeneratorPreprocessXml(xmlDoc, projectVariablesForPdfGeneration.outputChannelType, sections, projectVariablesForPdfGeneration);
        }

        /// <summary>
        /// Retrieves the full HTML of the PDF output channel from the version control system (using a tag name or commit hash)
        /// </summary>
        /// <param name="projectVariablesForPdfGeneration"></param>
        /// <param name="commitIdentifier"></param>
        /// <param name="sections"></param>
        /// <param name="renderScope"></param>
        /// <returns></returns>
        public static async Task<string> RetrieveHistoricalPdfHtml(ProjectVariables projectVariablesForPdfGeneration, string commitIdentifier, string sections, bool useContentStatus, string renderScope = "single-section")
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Retrieve the full XHML file from the Taxxor Document Store
            var xmlDoc = await DocumentStoreService.PdfData.LoadHistorical(projectVariablesForPdfGeneration, commitIdentifier, sections, useContentStatus, renderScope, true);

            if (XmlContainsError(xmlDoc)) HandleError(ReturnTypeEnum.Html, xmlDoc);

            // Render XHTML from the data we have received using an XSLT translation
            return await PdfGeneratorPreprocessXml(xmlDoc, projectVariablesForPdfGeneration.outputChannelType, sections, projectVariablesForPdfGeneration);
        }

        /// <summary>
        /// Converts the XML of a section to a basic XHTML div structure before sending it to the PDF Generator process
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="outputChannelType"></param>
        /// <param name="sections"></param>
        /// <returns></returns>
        public static async Task<string> PdfGeneratorPreprocessXml(XmlDocument xmlDoc, string outputChannelType, string sections, ProjectVariables projectVars)
        {
            var stylesheetId = "cms_xsl_pdfgenerator-postload";
            var xsltStylesheet = PdfXslCache.Get(stylesheetId);
            if (xsltStylesheet == null)
            {
                HandleError("ERROR: Could not load stylesheet", $"stylesheetId: {stylesheetId}, stack-trace: {GetStackTrace()}");

                return null;
            }

            if (siteType == "local" || siteType == "development")
            {
                var saved = await SaveXmlDocument(xmlDoc, logRootPathOs + "/_pdfpreviewer-data.xml");
            }

            XsltArgumentList xsltArguments = new XsltArgumentList();
            xsltArguments.AddParam("output-channel-type", "", outputChannelType);
            xsltArguments.AddParam("sectionId", "", sections);
            xsltArguments.AddParam("renderHtml", "", "yes");

            xmlDoc = TransformXmlToDocument(xmlDoc, xsltStylesheet, xsltArguments);

            //
            // => Execute custom logic for manipulating the XML document before it is used for further processing
            //
            var customProcessingResult = Extensions.XhtmlPdfPostProcessing(xmlDoc, projectVars);
            if (!customProcessingResult.Success)
            {
                appLogger.LogError($"There was an issue post processing the PDF content received. Message: {customProcessingResult.Message}, DebugInfo: {customProcessingResult.DebugInfo}");
                return xmlDoc.OuterXml;
            }
            else
            {
                return customProcessingResult.XmlPayload.OuterXml;
            }
        }

        /// <summary>
        /// Converts a typical XML section content data file to basic XHTML input for the PDF Generator
        /// </summary>
        /// <param name="xmlSection"></param>
        /// <param name="dataReference"></param>
        /// <param name="outputChannelVariantLanguage"></param>
        /// <param name="hierarchicalLevel"></param>
        /// <returns></returns>
        public static XmlDocument ConvertXmlFilingDataToBasicPdfGeneratorDocument(XmlDocument xmlSection, string dataReference, string outputChannelVariantLanguage, int hierarchicalLevel = 1)
        {
            // The XML Document that we will be building up and return
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml("<div class=\"body-wrapper\"/>");
            var targetDocumentRootNode = xmlDocument.DocumentElement;


            // Select the node that we want to import and set the current hierarchical level in it
            var xPathArticle = $"/data/content[@lang='{outputChannelVariantLanguage}']/*";
            var nodeSourceData = xmlSection.SelectSingleNode(xPathArticle);
            if (nodeSourceData == null)
            {
                appLogger.LogError($"Could not find article node so the article is not added to the PDF. xmlSection: {TruncateString(xmlSection.OuterXml, 400)}, xPathArticle: {xPathArticle}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                SetAttribute(nodeSourceData, "data-hierarchical-level", hierarchicalLevel.ToString());
                SetAttribute(nodeSourceData, "data-ref", dataReference);
                SetAttribute(nodeSourceData, "lang", outputChannelVariantLanguage);

                // Import the fragment and sequentially add it to the root node
                var importedNode = xmlDocument.ImportNode(nodeSourceData, true);
                targetDocumentRootNode.AppendChild(importedNode);
            }

            return xmlDocument;
        }

        /// <summary>
        /// Prepares basic section XHTML for a DIFF pdf
        /// Cleanup the XHTML and add HTML chrome to the web page (head, title, body, etc) to the HTML fragments so that we send a complete version to the PDF Generator
        /// </summary>
        /// <param name="xhtmlContent"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVariablesForPdfGeneration"></param>
        /// <param name="pdfProperties"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> PrepareSectionXhtmlForDiffPdfGeneration(string xhtmlContent, RequestVariables reqVars, ProjectVariables projectVariablesForPdfGeneration, PdfProperties pdfProperties)
        {
            TaxxorReturnMessage result = await CreateCompletePdfHtml(xhtmlContent, reqVars, projectVariablesForPdfGeneration, pdfProperties);
            if (!result.Success)
            {
                return result;
            }
            else
            {
                return PrepareSectionXhtmlForDiffPdfGeneration(result.XmlPayload.OuterXml);
            }
        }

        /// <summary>
        /// Converts a diff XHTML string to an Xml Document for further processing for a DIFF pdf
        /// </summary>
        /// <param name="xhtmlContent"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage PrepareSectionXhtmlForDiffPdfGeneration(string xhtmlContent)
        {
            try
            {
                // Convert to an XML Document for further processing
                var xmlSectionContentForPdfGeneration = new XmlDocument();
                xmlSectionContentForPdfGeneration.LoadHtml(xhtmlContent);

                return PrepareSectionXhtmlForDiffPdfGeneration(xmlSectionContentForPdfGeneration);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was a problem preparing the XHTML for PDF Generation", $"Unable to parse xhtml, error: {ex}");
            }
        }

        /// <summary>
        /// Prepares basic section XHTML for a DIFF pdf
        /// </summary>
        /// <param name="xmlSectionContentForPdfGeneration"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage PrepareSectionXhtmlForDiffPdfGeneration(XmlDocument xmlSectionContentForPdfGeneration)
        {
            try
            {
                // Cleanup the SVG content of the graphs
                RemoveRenderedGraphs(ref xmlSectionContentForPdfGeneration);
                var foundXmlDeclaration = false;
                foreach (XmlNode node in xmlSectionContentForPdfGeneration)
                {
                    if (!foundXmlDeclaration && node.NodeType == XmlNodeType.XmlDeclaration)
                    {
                        xmlSectionContentForPdfGeneration.RemoveChild(node);
                        foundXmlDeclaration = true;
                    }
                }

                // Replace the SVG rendering of the graph by a still image that we have rendered in the renditions folder in the Project Data Store
                var nodeListChartWrappers = xmlSectionContentForPdfGeneration.SelectNodes("//article//section//div[div/@class='chart-content']");
                foreach (XmlNode nodeChartWrapper in nodeListChartWrappers)
                {
                    var wrapperId = nodeChartWrapper.GetAttribute("id") ?? "notfound";
                    if (wrapperId.Contains("_")) wrapperId = wrapperId.SubstringAfter("_");

                    var nodeArticle = nodeChartWrapper.SelectSingleNode("ancestor::article");
                    var contentLang = "undefined";
                    var dataReference = "undefined";
                    if (nodeArticle != null)
                    {
                        contentLang = nodeArticle.GetAttribute("lang") ?? "unknown";
                        dataReference = nodeArticle.GetAttribute("data-ref") ?? "unknown";
                    }



                    var graphRenditionFileName = $"{Path.GetFileNameWithoutExtension(dataReference)}---{wrapperId}---{contentLang}.png";
                    var nodeImage = xmlSectionContentForPdfGeneration.CreateElement("img");
                    nodeImage.SetAttribute("class", "chart-placeholder");
                    nodeImage.SetAttribute("src", "/dataserviceassets/{projectid}/images" + $"/{ImageRenditionsFolderName}/graphs/{graphRenditionFileName}");
                    var nodeChartContent = nodeChartWrapper.SelectSingleNode("div[@class='chart-content']");
                    if (nodeChartContent != null)
                    {
                        var nodeImageBasedChartContentWrapper = xmlSectionContentForPdfGeneration.CreateElement("div");
                        nodeImageBasedChartContentWrapper.SetAttribute("class", "chart-content");
                        var chartContentId = nodeChartContent.GetAttribute("id");
                        if (string.IsNullOrEmpty(chartContentId)) chartContentId = Path.GetFileNameWithoutExtension(graphRenditionFileName);
                        nodeImageBasedChartContentWrapper.SetAttribute("id", chartContentId);
                        nodeImageBasedChartContentWrapper.AppendChild(nodeImage);

                        ReplaceXmlNode(nodeChartContent, nodeImageBasedChartContentWrapper);
                    }

                    // By assigning another class to the wrapper div, the table will automatically show and we can use it in the track changes
                    nodeChartWrapper.SetAttribute("class", nodeChartWrapper.GetAttribute("class").Replace("chart-wrapper", "chart-table"));
                }

                // nodeValueCreation = xmlLatestVersion.SelectSingleNode("//div[@class='c-value__wheelwrapper']");
                // if (nodeValueCreation != null) nodeValueCreation.InnerText = ".";
                PrepareTablesForDiffPdf(ref xmlSectionContentForPdfGeneration);
                RemoveEmptyElements(ref xmlSectionContentForPdfGeneration);
                AddCommentInEmptyElements(ref xmlSectionContentForPdfGeneration);

                // Create HTML output without self closing tags
                return new TaxxorReturnMessage(true, "Successfully prepared XHTML for PDF DIFF Generation", ValidHtmlVoidTags(xmlSectionContentForPdfGeneration).OuterXml, "");
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was a problem preparing the XHTML for PDF DIFF Generation", $"error: {ex}");
            }
        }



        /// <summary>
        /// Generates part of the parameters that we need to post to the PDF Generator to generate a PDF binary
        /// </summary>
        /// <param name="Dictionary<string"></param>
        /// <param name="dataToPost"></param>
        /// <param name="pdfProperties"></param>
        /// <param name="editorId"></param>
        /// <param name="outputChannelType"></param>
        public static void ExtendPdfGeneratorPostParameters(ref Dictionary<string, string> dataToPost, PdfProperties pdfProperties, string editorId, string outputChannelType = "pdf")
        {

            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Construct the raw data to post  

            /*
            Add parameters for the PDF generation engine
            */
            // (1) Base URL that the PDF Service needs to use to resolve paths to external assets (css, js, images)
            dataToPost.Add("baseurl", LocalWebAddressDomain);

            var booktitle = "";
            // (2) CSS
            // CSS as plain CSS content to send to the service - optional
            if (pdfProperties.PdfGeneratorStringSet.Count > 0)
            {
                // Loop through the string sets defined to generate PrinceXML specific CSS code
                List<string> cssSetContent = new List<string>();
                foreach (var stringSet in pdfProperties.PdfGeneratorStringSet)
                {
                    cssSetContent.Add($"{stringSet.Key} '{stringSet.Value}'");
                    if (stringSet.Key == "booktitle")
                    {
                        booktitle = stringSet.Value;
                    }
                }
                var cssSets = String.Join(", ", cssSetContent.ToArray());
                var stringSetCss = "html { string-set: " + cssSets + "; }";

                // Add it to the posted data
                dataToPost.Add("csspage", stringSetCss);
            }

            dataToPost.Add("pdftitle", booktitle);

            if (pdfProperties.Mode == "diff")
            {
                // dataToPost.Add("pdfkeywords", $"Comparison of lastest: {dataToPost["versionlatest"]} against base: {dataToPost["versionbase"]}");
                dataToPost.Add("pdfkeywords", $"Comparison of '{pdfProperties.Latest}' against '{pdfProperties.Base}'");
            }


            // CSS media queries to apply to when rendering the PDF - optional (possible values --media=(speech|print|screen|all))
            dataToPost.Add("media", "--media=print");

            // Configure if the PDF Service should use the CSS rules as specified in the HTML source - optional (defaults to true)
            dataToPost.Add("enablesourcecss", "true");

            // Special CSS that the PDF Service should use for the transformation and fetch via HTTP from this application
            var cssLocationXpath = $"/configuration/editors/editor[@id='{editorId}']/output_channels/output_channel[@type='{outputChannelType}']//location[@id='pdf-css-stylesheet']";
            var nodeCssLocation = xmlApplicationConfiguration.SelectSingleNode(cssLocationXpath);
            if (nodeCssLocation != null)
            {
                var cssUrl = CalculateFullPathOs(nodeCssLocation, reqVars);
                // Add and external CSS file to be added to the source HTML file - optional
                dataToPost.Add("css", LocalWebAddressDomain + cssUrl);
            }

            // (3) Javascript configuration
            // Toggle javascript rendering of scripts specified on the source HTML page on - optional (defaults to true)
            dataToPost.Add("enablesourcejs", "true");

            // Specify an external javascript file to include and run on the HTML source page (http path style) - optional
            var jsLocationXpath = $"/configuration/editors/editor[@id='{editorId}']/output_channels/output_channel[@type='{outputChannelType}']//location[@id='pdf-js-script']";
            var nodeJsLocation = xmlApplicationConfiguration.SelectSingleNode(jsLocationXpath);
            if (nodeJsLocation != null)
            {
                var jsUrl = CalculateFullPathOs(nodeJsLocation, reqVars);
                // Add and external Javascript file to be added to the source HTML file - optional
                dataToPost.Add("js", LocalWebAddressDomain + jsUrl);
            }

            // The author name that we need to show in the PDF
            string? pdfAuthorName = null;
            if (!string.IsNullOrEmpty(projectVars.guidLegalEntity))
            {
                pdfAuthorName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/taxxor/clients/client/entity_groups/entity_group/entity[@guidLegalEntity='{projectVars.guidLegalEntity}']/name")?.InnerText ?? "";
            }
            if (string.IsNullOrEmpty(pdfAuthorName)) pdfAuthorName = TaxxorClientName;

            dataToPost.Add("pdfauthor", pdfAuthorName);

            // The renderengine that the PDF Generator needs to use
            dataToPost.Add("renderengine", PdfRenderEngine);
        }

        /// <summary>
        /// Routine to centrally setup the PDF stylesheet cache
        /// </summary>
        /// <param name="reqVars"></param>
        public static void SetupPdfStylesheetCache(RequestVariables reqVars)
        {
            //
            // => Stylesheet cache
            //
            try
            {
                if (PdfXslCache == null)
                {
                    PdfXslCache = new PdfXslStylesheetCache(reqVars);
                }
                else
                {
                    if (!PdfXslCache.ContainsObjects())
                    {
                        PdfXslCache = new PdfXslStylesheetCache(reqVars);
                    }
                    else
                    {
                        // appLogger.LogCritical("Using cached stylesheets!!!");
                    }
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"There was an error retrieving the PDF XSL stylesheet cache object.");
                PdfXslCache = new PdfXslStylesheetCache(reqVars);
            }
        }

        /// <summary>
        /// For caching the stylesheets used for generating a PDF
        /// </summary>
        public class PdfXslStylesheetCache : XslStylesheetCache
        {
            public PdfXslStylesheetCache(RequestVariables reqVars)
            {
                var stylesheetIds = new List<string>();

                // TODO: this needs to be made more generic so that it works with different editors
                var editorPathOs = $"{applicationRootPathOs}/frontend/public/report_editors/default";

                // Editor specific stylesheets
                var calculatedPath = "";
                calculatedPath = CalculateFullPathOs("cms_xsl_pdfgenerator-postload", reqVars);
                if (!calculatedPath.StartsWith(editorPathOs)) calculatedPath = editorPathOs + calculatedPath;
                this.Add("cms_xsl_pdfgenerator-postload", calculatedPath);

                calculatedPath = CalculateFullPathOs("cms_xsl_pdfgenerator-htmlchrome", reqVars);
                if (!calculatedPath.StartsWith(editorPathOs)) calculatedPath = editorPathOs + calculatedPath;
                this.Add("cms_xsl_pdfgenerator-htmlchrome", calculatedPath);

                // Taxxor Editor stylesheets
                stylesheetIds.Add("xsl_simplify-source-data");
                stylesheetIds.Add("cms_xsl_dynamic-header-levels");
                stylesheetIds.Add("cms_xsl_flatten-pdf-content");
                stylesheetIds.Add("xsl_outputchannel-tables-only");

                this.ReqVars = reqVars;
                this.Add(stylesheetIds);
            }

        }

        /// <summary>
        /// Improves the rendering of track changes
        /// </summary>
        /// <param name="xmlTrackChanges"></param>
        public static void PostProcessTrackChangesHtml(ref XmlDocument xmlTrackChanges)
        {
            var executePostProcessing = false;

            //
            // => Remove nodes which were used to help the diff generator to match cell contents
            //
            var nodeListDiffUtilityNodes = xmlTrackChanges.SelectNodes("//table//span[@data-utility='trackchanges']");
            if (nodeListDiffUtilityNodes.Count > 0) RemoveXmlNodes(nodeListDiffUtilityNodes);

            //
            // => If articles are deleted, then we need to inject the deleted content in a del node
            //
            var nodeListModifiedArticles = xmlTrackChanges.SelectNodes("//article[contains(@class, 'del')]");
            foreach (XmlNode nodeDeletedArticle in nodeListModifiedArticles)
            {
                // Mark all items with the deleted class
                var nodeListElements = nodeDeletedArticle.SelectNodes("section/*");
                foreach (XmlNode nodeElement in nodeListElements)
                {
                    var delNode = xmlTrackChanges.CreateElement("del");
                    nodeElement.SetAttribute("class", "del");
                    nodeElement.SetAttribute("style", "color: red; background-color: #ffcccb;");
                    delNode.AppendChild(nodeElement.CloneNode(true));
                    nodeElement.ParentNode.ReplaceChild(delNode, nodeElement);
                }
            }

            //
            // -> If an article is inserted, then we need to wrap the inserted content in an ins node
            //
            nodeListModifiedArticles = xmlTrackChanges.SelectNodes("//article[contains(@class, 'new')]");
            foreach (XmlNode nodeInsertedArticle in nodeListModifiedArticles)
            {
                // Mark all items with the deleted class
                var nodeListElements = nodeInsertedArticle.SelectNodes("section/*");
                foreach (XmlNode nodeElement in nodeListElements)
                {
                    var insNode = xmlTrackChanges.CreateElement("ins");
                    nodeElement.SetAttribute("class", "new");
                    nodeElement.SetAttribute("style", "color: green; background-color: #90EE90;");
                    insNode.AppendChild(nodeElement.CloneNode(true));
                    nodeElement.ParentNode.ReplaceChild(insNode, nodeElement);
                }
            }


            if (executePostProcessing)
            {


                // var nodeListDel = xmlTrackChanges.SelectNodes("//del[not(ancestor::table)]");
                var nodeListDel = xmlTrackChanges.SelectNodes("//del");
                foreach (XmlNode nodeDel in nodeListDel)
                {
                    var operationIndex = nodeDel.GetAttribute("data-operation-index");
                    if (!string.IsNullOrEmpty(operationIndex))
                    {
                        // Find the corresponding ins node
                        var nodeListIns = nodeDel.ParentNode.SelectNodes($"*[(local-name()='ins' and @data-operation-index='{operationIndex}') or (@data-diff-node='ins' and @data-operation-index='{operationIndex}')]");
                        if (nodeListIns.Count > 0)
                        {
                            // Wrap all of them in a span node
                            var nodeSpan = xmlTrackChanges.CreateElement("span");
                            nodeSpan.SetAttribute("data-operation-index", operationIndex);
                            nodeDel.ParentNode.InsertBefore(nodeSpan, nodeDel);
                            nodeSpan.AppendChild(nodeDel);

                            var innerXmlIns = "";
                            foreach (XmlNode nodeIns in nodeListIns)
                            {
                                if (nodeIns.Name == "ins")
                                {
                                    innerXmlIns += nodeIns.InnerXml;
                                }
                                else
                                {


                                    var nodeElement = xmlTrackChanges.CreateElement(nodeIns.Name);
                                    nodeElement.InnerXml = nodeIns.SelectSingleNode("*")?.InnerXml ?? nodeIns.InnerXml;

                                    innerXmlIns += nodeElement.OuterXml;

                                }
                                RemoveXmlNode(nodeIns);

                            }
                            var nodeInsSimplified = xmlTrackChanges.CreateElement("ins");
                            nodeInsSimplified.InnerXml = innerXmlIns;
                            nodeSpan.AppendChild(nodeInsSimplified);

                            if (operationIndex == "9")
                            {
                                appLogger.LogInformation("Debugging...");
                            }

                            var trackChangesItem = new TrackChangesItem(nodeSpan);
                            var nodeSpanSimplyfied = trackChangesItem.Simplify();
                            if (nodeSpanSimplyfied.GetAttribute("changed") == "true")
                            {
                                ReplaceXmlNode(nodeSpan, nodeSpanSimplyfied);
                            }
                            else
                            {
                                appLogger.LogInformation($"No need to change track changes item {operationIndex}");
                            }


                        }
                        else
                        {
                            appLogger.LogInformation($"Track changes item ({operationIndex}) is not a 'change' that we need to process");
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"{nodeDel.OuterXml} does not have an operation index");
                    }
                }

            }
        }

        /// <summary>
        /// Utility method that inserts some dummy data into nodes that could otherwise become self-closing
        /// </summary>
        /// <param name="xmlData"></param>
        public static void InsertDummyEmptyElementData(XmlDocument xmlData)
        {
            var nodeListEmptyDivs = xmlData.SelectNodes("//div[not(node()) and not(string())]");
            foreach (XmlNode nodeEmptyDiv in nodeListEmptyDivs)
            {
                // var nodeComment = xmlData.CreateComment(".");
                // nodeEmptyDiv.AppendChild(nodeComment);
                // nodeEmptyDiv.InnerText = "**loremipsumfoobar**";
                nodeEmptyDiv.AppendChild(xmlData.CreateElement("wbr"));
            }
        }

        /// <summary>
        /// Removes the dummy content from nodes that could otherwise become self-closing
        /// </summary>
        /// <param name="xmlData"></param>
        public static void RemoveDummyEmptyElementData(XmlDocument xmlData)
        {
            // var nodeListEmptyDivs = xmlData.SelectNodes("//div[text()='**loremipsumfoobar**' and count(*)=0]");
            var nodeListEmptyDivs = xmlData.SelectNodes("//div[wbr and count(*)=1]");
            foreach (XmlNode nodeEmptyDiv in nodeListEmptyDivs)
            {
                var nodeComment = xmlData.CreateComment(".");
                nodeEmptyDiv.AppendChild(nodeComment);
                nodeEmptyDiv.InnerText = " ";
            }
        }

        /// <summary>
        /// Contains a normalized version of a track-changes item
        /// </summary>
        public class TrackChangesItem
        {

            private XmlNode _nodeOriginal { get; set; } = null;
            private string _operationIndex { get; set; } = "";
            private List<string> _prefix { get; set; } = new List<string>();
            private List<string> _suffix { get; set; } = new List<string>();
            private XmlNode _nodeInsert { get; set; } = null;
            private XmlNode _nodeDelete { get; set; } = null;
            private XmlDocument _doc { get; set; } = null;

            public TrackChangesItem(XmlNode nodeTrackChangesItemOriginal)
            {
                this._doc = nodeTrackChangesItemOriginal.OwnerDocument;
                this._nodeOriginal = nodeTrackChangesItemOriginal;
                this._operationIndex = nodeTrackChangesItemOriginal.GetAttribute("data-operation-index");
            }

            public XmlNode Simplify()
            {
                var changed = false;

                // Compare the two strings
                var strXmlDel = this._nodeOriginal.SelectSingleNode("del").InnerXml;
                var strXmlInsert = this._nodeOriginal.SelectSingleNode("ins").InnerXml;
                string[] arrDel = strXmlDel.ToCharArray().Select(c => c.ToString()).ToArray();
                string[] arrInsert = strXmlInsert.ToCharArray().Select(c => c.ToString()).ToArray();

                // Get prefix
                var arrayLength = (arrDel.Length > arrInsert.Length) ? arrDel.Length : arrInsert.Length;
                for (int i = 0; i < arrayLength; i++)
                {
                    // Del and ins character to search for
                    string? strCharDel = null;
                    string? strCharInsert = null;
                    if (i < arrDel.Length) strCharDel = arrDel[i];
                    if (i < arrInsert.Length) strCharInsert = arrInsert[i];

                    if (arrInsert.Length >= (i + 1))
                    {
                        if (strCharDel == strCharInsert)
                        {
                            this._prefix.Add(strCharDel);
                        }
                        else
                        {
                            // Adjust the del and ins content
                            strXmlDel = strXmlDel.SubstringAfter(string.Join("", this._prefix));
                            strXmlInsert = strXmlInsert.SubstringAfter(string.Join("", this._prefix));
                            if (this._operationIndex == "9")
                            {
                                appLogger.LogInformation("debugging...");
                            }
                            break;
                        }
                    }
                }

                // Get suffix
                string[] arrDel2 = strXmlDel.ToCharArray().Select(c => c.ToString()).ToArray();
                string[] arrInsert2 = strXmlInsert.ToCharArray().Select(c => c.ToString()).ToArray();
                arrayLength = (arrDel2.Length > arrInsert2.Length) ? arrDel2.Length : arrInsert2.Length;
                for (int i = 0; i < arrayLength; i++)
                {
                    try
                    {
                        var indexDel = arrDel2.Length - 1 - i;
                        var indexInsert = arrInsert2.Length - 1 - i;

                        // Del and ins character to search for
                        string? strCharDel = null;
                        string? strCharInsert = null;
                        if (indexDel >= 0 && indexDel < arrDel2.Length) strCharDel = arrDel2[indexDel];
                        if (indexInsert >= 0 && indexInsert < arrInsert2.Length) strCharInsert = arrInsert2[indexInsert];

                        if (strCharDel == strCharInsert)
                        {
                            this._suffix.Add(strCharDel);
                        }
                        else
                        {
                            strXmlDel = strXmlDel.Remove(strXmlDel.Length - this._suffix.Count + 0);
                            strXmlInsert = strXmlInsert.Remove(strXmlInsert.Length - this._suffix.Count + 0);
                            // stop = true;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, "Error creating track changes suffix");
                        break;
                    }

                }

                var nodeTcSimplified = this._doc.CreateNode(XmlNodeType.Element, null, "span", null);
                nodeTcSimplified.SetAttribute("data-operation-index", this._operationIndex);
                if (this._prefix.Count > 0 || this._suffix.Count > 0)
                {
                    changed = true;
                    this._suffix.Reverse();

                    nodeTcSimplified.InnerXml = $"{string.Join("", this._prefix)}{((strXmlDel.Length > 0) ? "<del>" + strXmlDel + "</del>" : "")}{((strXmlInsert.Length > 0) ? "<ins>" + strXmlInsert + "</ins>" : "")}{string.Join("", this._suffix)}";
                }


                nodeTcSimplified.SetAttribute("changed", changed.ToString().ToLower());

                return nodeTcSimplified;
            }



        }

    }
}