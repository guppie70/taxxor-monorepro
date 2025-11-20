using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;
using static Taxxor.ConnectedServices.DocumentStoreService;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Section locks using websocket connection
        /// </summary>
        public partial class WebSocketsHub : Hub
        {

            /// <summary>
            /// Retrieves the hierarchy that a user can use to create a custom report in PDF or Word
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/customreporthierarchy")]
            public async Task<TaxxorReturnMessage> RetrieveCustomReportHierarchy(string jsonData)
            {
                var errorMessage = "There was an error rendering the UI for the custom report generator";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";
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
                        // if (debugRoutine) Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                        // Fill the request and project variables in the context with the posted values
                        var renderProjectVarsResult = await ProjectVariablesFromXml(xmlDataPosted, true);
                        if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");
                        // if (debugRoutine) Console.WriteLine(GenerateDebugObjectString(projectVars, ReturnTypeEnum.Txt));


                        // Message to return
                        var methodResult = new TaxxorReturnMessage();



                        //
                        // => Retrieve output channel hierarchy
                        //
                        XmlDocument? xmlHierarchyDoc = null;
                        var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, "pdf", projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                        if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                        {
                            xmlHierarchyDoc = projectVars.rbacCache.GetHierarchy(outputChannelHierarchyMetadataKey, "full");

                            // If the complete hierarchy cannot be loacted in the rbacCache, then load it fresh from the Project Data Store
                            if (xmlHierarchyDoc == null)
                            {
                                appLogger.LogWarning($"Unable to find the outputchannel hierarchy in projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}']");
                                xmlHierarchyDoc = await FilingData.LoadHierarchy(projectVars, debugRoutine);
                                if (XmlContainsError(xmlHierarchyDoc))
                                {
                                    xmlHierarchyDoc = null;
                                }
                            }

                            //
                            // => Create the HTML to show in the UI
                            //
                            var html = TransformXml(xmlHierarchyDoc, "cms_xsl_hierarchy-to-input-list");


                            //
                            // => Fill the response message
                            //
                            methodResult.Success = true;
                            methodResult.Message = "Successfully rendered custom report selector UI";
                            methodResult.Payload = html;
                            // methodResult.DebugInfo = projectVars.cmsMetaData[RetrieveOutputChannelHierarchyMetadataId(projectVars)].Xml.OuterXml;

                        }
                        else
                        {
                            methodResult.Success = false;
                            methodResult.Message = "Unable to find the output channel hierarchy";
                            methodResult.DebugInfo = $"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}'], stack-trace: {GetStackTrace()}";
                            appLogger.LogError(methodResult.DebugInfo);
                        }

                        // methodResult.Success = false;
                        // methodResult.Message = "Thrown on purpose";
                        // methodResult.DebugInfo = $"stack-trace: {GetStackTrace()}";

                        //
                        // => Send the result back to the client
                        // 
                        if (!methodResult.Success)
                        {
                            appLogger.LogError($"{errorMessage} - {methodResult.ToString()}");
                        }

                        return methodResult;
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }



            /// <summary>
            /// Generates a PDF file on the server and returns the location where it can be picked up
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/pdfgenerator")]
            public async Task<TaxxorReturnMessage> GeneratePdf(string jsonData)
            {
                var errorMessage = "";
                var errorDetails = "";
                var errorMessages = new List<string>();
                var bulkGenerate = false;
                var successfullyRenderedOutputChannels = new List<string>();

                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

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
                        var tokenPosted = _extractPostedXmlValue(xmlDataPosted, "token");
                        var projectIdPosted = _extractPostedXmlValue(xmlDataPosted, "pid");
                        var sectionIdPosted = _extractPostedXmlValue(xmlDataPosted, "did");
                        var serveAsDownloadString = _extractPostedXmlValue(xmlDataPosted, "serveasdownload");
                        var serveAsBinaryString = _extractPostedXmlValue(xmlDataPosted, "serveasbinary");
                        var renderSignatureMarksString = _extractPostedXmlValue(xmlDataPosted, "serveasbinary");
                        var modePosted = _extractPostedXmlValue(xmlDataPosted, "mode");
                        var basePosted = _extractPostedXmlValue(xmlDataPosted, "base");
                        var latestPosted = _extractPostedXmlValue(xmlDataPosted, "latest");
                        var htmlPosted = _extractPostedXmlValue(xmlDataPosted, "html");
                        var securePdfString = _extractPostedXmlValue(xmlDataPosted, "secured");
                        var printReadyString = _extractPostedXmlValue(xmlDataPosted, "printready");
                        var landscapeString = _extractPostedXmlValue(xmlDataPosted, "landscape");
                        var postProcessPosted = _extractPostedXmlValue(xmlDataPosted, "postprocess");
                        var layoutPosted = _extractPostedXmlValue(xmlDataPosted, "layout");
                        var renderScopePosted = _extractPostedXmlValue(xmlDataPosted, "renderscope");
                        var tablesOnlyString = _extractPostedXmlValue(xmlDataPosted, "tablesonly");
                        var useLoremIpsumString = _extractPostedXmlValue(xmlDataPosted, "useloremipsum");
                        var useContentStatusString = _extractPostedXmlValue(xmlDataPosted, "usecontentstatus");
                        var hideCurrentPeriodDatapointsString = _extractPostedXmlValue(xmlDataPosted, "hidecurrentperioddatapoints");
                        var hideErrorsString = _extractPostedXmlValue(xmlDataPosted, "hideerrors");
                        // - For bulk generate
                        var outputChannelVariantIds = _extractPostedXmlValue(xmlDataPosted, "ocvariantids");
                        // - Output format
                        var outputFormat = _extractPostedXmlValue(xmlDataPosted, "outputformat");


                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add(tokenPosted, @"^[a-zA-Z0-9]{10,40}$", true);
                        inputValidationCollection.Add(projectIdPosted, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        inputValidationCollection.Add(sectionIdPosted, RegexEnum.DefaultLong, true);
                        inputValidationCollection.Add(serveAsDownloadString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(serveAsBinaryString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(renderSignatureMarksString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(modePosted, RegexEnum.Default, "normal");
                        inputValidationCollection.Add(basePosted, RegexEnum.Default, "");
                        inputValidationCollection.Add(latestPosted, RegexEnum.Default, "");
                        inputValidationCollection.Add(htmlPosted, RegexEnum.None, "");
                        inputValidationCollection.Add(securePdfString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(printReadyString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(landscapeString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(postProcessPosted, RegexEnum.Default, "none");
                        inputValidationCollection.Add(layoutPosted, RegexEnum.Default, "unknown");
                        inputValidationCollection.Add(renderScopePosted, RegexEnum.Default, "single-section");
                        inputValidationCollection.Add(tablesOnlyString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(useLoremIpsumString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(useContentStatusString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(hideCurrentPeriodDatapointsString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(hideErrorsString, RegexEnum.Boolean, "false");
                        inputValidationCollection.Add(outputFormat, @"^(pdf|raw)$", true);
                        // - For bulk generate
                        inputValidationCollection.Add(outputChannelVariantIds, RegexEnum.None, "");




                        var validationResult = inputValidationCollection.Validate();
                        if (!validationResult.Success)
                        {
                            appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                            return validationResult;
                        }
                        else
                        {
                            // Fill the request and project variables in the context with the posted values
                            var renderProjectVarsResult = await ProjectVariablesFromXml(xmlDataPosted, true);
                            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                            projectVars.did = "pdfgenerator";

                            var extension = (outputFormat.Value == "pdf") ? "pdf" : "html";

                            //
                            // => Post process the variables that we have received
                            //
                            var sectionId = sectionIdPosted.Value;
                            var mode = modePosted.Value;
                            var _base = basePosted.Value;
                            var latest = latestPosted.Value;
                            var html = htmlPosted.Value;
                            var renderScope = renderScopePosted.Value;
                            var serveAsDownload = serveAsDownloadString.Value == "true";
                            var serveAsBinary = serveAsBinaryString.Value == "true";
                            var renderSignatureMarks = renderSignatureMarksString.Value == "true";
                            var securePdf = securePdfString.Value == "true";
                            var printReady = printReadyString.Value == "true";
                            var landscape = landscapeString.Value == "true";
                            var postProcess = postProcessPosted.Value;
                            var tablesOnly = tablesOnlyString.Value == "true";
                            var useLoremIpsum = useLoremIpsumString.Value == "true";
                            var useContentStatus = useContentStatusString.Value == "true";
                            var hideCurrentPeriodDatapoints = hideCurrentPeriodDatapointsString.Value == "true";
                            var hideErrors = hideErrorsString.Value == "true";


                            //
                            // => Test if we have received all the required parameters for a track changes PDF
                            //
                            if (mode == "diff")
                            {
                                if (string.IsNullOrEmpty(_base) || string.IsNullOrEmpty(latest))
                                {
                                    throw new Exception($"Not enough valid information supplied, mode: {mode}, _base: {_base}, latest: {latest}");
                                }
                                else
                                {
                                    if (!RegExpTest(@"^(current|v\d+\.\d+)$", _base))
                                    {
                                        throw new Exception($"Not enough valid information supplied: _base parameter did not pass the validation pattern, mode: {mode}, _base: {_base}, latest: {latest}");
                                    }
                                    else if (!RegExpTest(@"^(current|v\d+\.\d+)$", latest))
                                    {
                                        throw new Exception($"Not enough valid information supplied: latest parameter did not pass the validation pattern, mode: {mode}, _base: {_base}, latest: {latest}");
                                    }
                                }
                            }

                            //
                            // => Create a list of output channel variant IDs to be used in this rendering
                            //
                            var ouputVariantIds = new List<string>();
                            if (string.IsNullOrEmpty(outputChannelVariantIds.Value))
                            {
                                ouputVariantIds.Add(projectVars.outputChannelVariantId);
                            }
                            else if (outputChannelVariantIds.Value.Contains(","))
                            {
                                ouputVariantIds = outputChannelVariantIds.Value.Split(",").ToList();
                            }
                            else
                            {
                                ouputVariantIds.Add(outputChannelVariantIds.Value);
                            }
                            bulkGenerate = ouputVariantIds.Count > 1;


                            //
                            // => Stylesheet cache
                            //
                            SetupPdfStylesheetCache(reqVars);


                            //
                            // => Generate the PDF(s)
                            //
                            var debugInfo = "";
                            var debugInfoToReturn = "";
                            var pdfsFilePathOs = new List<string>();
                            var pdfsFileName = new List<string>();
                            if (bulkGenerate) await RenderWebSocketProgressMessage($"* Start bulk PDF generation process *");
                            foreach (var currentOutputChannelVariantId in ouputVariantIds)
                            {
                                projectVars.outputChannelVariantId = currentOutputChannelVariantId;
                                projectVars.outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);
                                SetProjectVariables(context, projectVars);

                                string outputChannelName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(projectVars.outputChannelVariantId)}]/name")?.InnerText ?? "unknown";
                                await RenderWebSocketProgressMessage($"Start generating {outputChannelName} PDF");

                                // Console.WriteLine($"ProjectVariables:");
                                // Console.WriteLine(projectVars.DumpToString());



                                var layout = layoutPosted.Value;
                                if (layout == "unknown")
                                {
                                    layout = "regular";
                                    if (!string.IsNullOrEmpty(projectVars.outputChannelVariantId))
                                    {
                                        if (string.IsNullOrEmpty(projectVars.editorId)) projectVars.editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
                                        layout = RetrieveOutputChannelDefaultLayout(projectVars).Layout;
                                    }
                                }

                                debugInfo += $"outputChannelVariantId: {projectVars.outputChannelVariantId} => (sectionId: {sectionId}, projectId: {projectVars.projectId}, versionId: {projectVars.versionId})";


                                // If we generate a print ready PDF, then we do not want to render sync error markers
                                if (printReady) hideErrors = true;

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
                                    UseLoremIpsum = useLoremIpsum,
                                    UseContentStatus = useContentStatus,
                                    HideCurrentPeriodDatapoints = hideCurrentPeriodDatapoints,
                                    HideErrors = hideErrors
                                };

                                // Retrieve output channel hierarchy to get the top node value and use that as a string to send to the PDF generator
                                var booktitle = "";
                                var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, "pdf", projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                                if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                                {
                                    XmlDocument? xmlHierarchyDoc = projectVars.rbacCache.GetHierarchy(outputChannelHierarchyMetadataKey, "full");

                                    // If the complete hierarchy cannot be loacted in the rbacCache, then load it fresh from the Project Data Store
                                    if (xmlHierarchyDoc == null)
                                    {
                                        xmlHierarchyDoc = await FilingData.LoadHierarchy(projectVars, debugRoutine);
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
                                                await RenderWebSocketProgressMessage($"WARNING: {errorMessage}");
                                                continue;
                                            }
                                            throw new Exception($"{errorMessage}: {errorDetails}");
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
                                            await RenderWebSocketProgressMessage($"WARNING: {errorMessage}");
                                            continue;
                                        }
                                        throw new Exception($"{errorMessage}: {errorDetails}");
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



                                //
                                // => Calculate PDF filename to use when we will be serving the PDF as a download or when we want to store it on the disk somewhere
                                //

                                var pdfDownloadFileName = $"document.{extension}";
                                try
                                {
                                    pdfDownloadFileName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/name").InnerText.ToLower().Replace(" ", "-");
                                    pdfDownloadFileName += $"_{pdfProperties.Sections}_{pdfProperties.Mode}_{projectVars.outputChannelVariantLanguage}.{extension}";
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogWarning($"Could not compose a nice filename for the {extension.ToUpper()} download. error: {ex}, stack-trace: {GetStackTrace()}");
                                }




                                //
                                // => Render the output document
                                //
                                XmlDocument? xmlRenderResult = new XmlDocument();
                                switch (outputFormat.Value)
                                {
                                    case "pdf":
                                        //
                                        // => Start rendering the PDF document
                                        //
                                        xmlRenderResult = await PdfService.RenderPdf<XmlDocument>(pdfProperties, projectVars, null, debugRoutine);
                                        break;

                                    case "raw":
                                        //
                                        // => Start the GIT Diff document
                                        //
                                        xmlRenderResult = await PdfService.RenderRawFullDiffHtml<XmlDocument>(pdfProperties, projectVars, null, true);
                                        break;

                                    default:
                                        xmlRenderResult = GenerateErrorXml($"Unable to render {outputFormat.Value} output format");
                                        break;
                                }



                                if (XmlContainsError(xmlRenderResult))
                                {
                                    errorMessage = $"Error rendering {outputChannelName}  {extension.ToUpper()}";
                                    errorDetails = $"recieved: {xmlRenderResult.OuterXml}";
                                    if (bulkGenerate)
                                    {
                                        // We do not quit the bulk generation process, but we record the problem and move on
                                        appLogger.LogError($"{errorMessage}: {errorDetails}");
                                        errorMessages.Add(errorMessage);
                                        await RenderWebSocketProgressMessage($"WARNING: {errorMessage}");
                                        continue;
                                    }
                                    throw new Exception($"{errorMessage}: {errorDetails}");
                                }

                                await RenderWebSocketProgressMessage($"Successfully rendered  {extension.ToUpper()} file");

                                successfullyRenderedOutputChannels.Add(currentOutputChannelVariantId);
                                var pdfFilePathOs = xmlRenderResult.SelectSingleNode("/result/pdfpath").InnerText;
                                pdfsFilePathOs.Add(pdfFilePathOs);
                                var pdfFileName = Path.GetFileName(pdfFilePathOs);
                                pdfsFileName.Add(pdfFileName);

                                var debuggerInfo = xmlRenderResult.SelectSingleNode("/result/debuginfo")?.InnerText ?? "";
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
                            // => Compile the message to return
                            //
                            TaxxorReturnMessage pdfGenerationResult = new();
                            if (!bulkGenerate)
                            {
                                // The result is a single file
                                var pdfFileName = pdfsFileName[0];
                                pdfGenerationResult = new TaxxorReturnMessage(true, "Successfully rendered the PDF document", pdfFileName, debugInfo);
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
                                using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
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

                                var successMessage = "Finalized bulk PDF generation process";
                                if (errorMessages.Count > 0)
                                {
                                    // We return a warning message as some of the PDF's in the original request could not be generated
                                    var warningMessage = $"(failed to generate {errorMessages.Count} PDF file{((errorMessages.Count > 1) ? "s" : "")})";
                                    pdfGenerationResult = new TaxxorReturnMessage(true, $"{successMessage} {warningMessage}", downloadFileName, $"Errors: {string.Join(",", errorMessages)} -- {debugInfo}");
                                    await RenderWebSocketProgressMessage($"* {successMessage} {warningMessage} *");
                                }
                                else
                                {
                                    pdfGenerationResult = new TaxxorReturnMessage(true, successMessage, downloadFileName, debugInfo);
                                    await RenderWebSocketProgressMessage($"* {successMessage} *");
                                }

                            }

                            return pdfGenerationResult;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"There was an error generating your PDF document{(bulkGenerate ? "s" : "")}";
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }

                /// <summary>
                /// Helper method to communicate the rendering progress to the client
                /// </summary>
                /// <param name="message"></param>
                /// <param name="debugInfo"></param>
                /// <returns></returns>
                async Task RenderWebSocketProgressMessage(string message, string debugInfo = null)
                {
                    await RenderWebsocketMessage("OutputGenerationProgress", true, message, debugInfo);
                }

                /// <summary>
                /// Generic helper method to execute the SignalR communication to the client
                /// </summary>
                /// <param name="functionName"></param>
                /// <param name="success"></param>
                /// <param name="message"></param>
                /// <param name="debugInfo"></param>
                /// <returns></returns>
                async Task RenderWebsocketMessage(string functionName, bool success, string message, string debugInfo = null)
                {
                    try
                    {
                        var returnMessage = new TaxxorReturnMessage(success, message, debugInfo);
                        await Clients.Caller.SendAsync(functionName, returnMessage.ToJson());
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"There was an error generating your PDF document");
                    }
                }



            }

            /// <summary>
            /// Retrieves the contents of the Generated Reports Repository from the Document Store
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="outputChannelVariantId"></param>
            /// <param name="scheme"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveGeneratedReportRepositoryContents(string projectId, string outputChannelVariantId, string scheme, string format = "html")
            {
                var errorMessage = "There was an error retrieving the generated report repository contents.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("outputChannelVariantId", outputChannelVariantId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("scheme", scheme, @"^[a-zA-Z0-9\-_]{2,128}$", true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Prepare the project variables object so that we can use it in the authorization process
                    //
                    projectVars.projectId = projectId;
                    projectVars.outputChannelVariantId = outputChannelVariantId;

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Retrieve the repository contents
                    //
                    var retrieveResult = await GeneratedReportsRepository.RetrieveContent(scheme, null, null, debugRoutine);
                    if (!retrieveResult.Success)
                    {
                        appLogger.LogError(retrieveResult.ToString());
                        throw new Exception(retrieveResult.Message);
                    }

                    //
                    // => Render information needed for building the links to the validation information
                    //
                    var uriArelleService = GetServiceUrl(ConnectedServiceEnum.EdgarArelleService);
                    var baseDomainName = "";
                    var folderPrefix = "";
                    var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{siteType}']");
                    var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
                    var protocol = (nodeCurrentDomain.GetAttribute("ssl") == "true") ? "https" : "http";

                    switch (siteType)
                    {
                        case "local":
                            baseDomainName = $"{protocol}://{currentDomainName}:4812";
                            break;
                        default:
                            folderPrefix = "/validationservice";
                            // baseDomainName = $"{protocol}://{currentDomainName}";
                            break;
                    }

                    //
                    // => Pre process the result before rendering the UI
                    //
                    var nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']");
                    var nodeListReports = retrieveResult.XmlPayload.SelectNodes("/reports/report");
                    foreach (XmlNode node in nodeListReports)
                    {
                        var reportingRequirementId = node.GetAttribute("scheme");
                        var reportingRequirementName = nodeProject.SelectSingleNode($"reporting_requirements/reporting_requirement[@ref-mappingservice='{reportingRequirementId}']/name")?.InnerText ?? "unknown";
                        node.SetAttribute("scheme-name", reportingRequirementName.Replace(" [placeholder]", ""));

                        var userId = node.GetAttribute("user");
                        var user = new Framework.AppUser();
                        user.UserNameFromUserId(userId);
                        var userFullName = (!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName)) ? $"{user.LastName}, {user.FirstName}" : userId;
                        node.SetAttribute("user-fullname", userFullName);

                        var nodeListValidationInformation = node.SelectNodes($"validationinformation/item");
                        var nodeValidationLinksRoot = retrieveResult.XmlPayload.CreateElement("validationlinks");

                        // var nodeInteractiveReportInformation = retrieveResult.XmlPayload.SelectSingleNode("validationinformation/item[@id='ixbrl']");
                        // if (nodeInteractiveReportInformation != null)
                        // {
                        //     var linkViewer = retrieveResult.XmlPayload.CreateElement("a");
                        //     var linkHref = RenderValidatorUri(nodeInteractiveReportInformation.InnerText);

                        //     linkViewer.SetAttribute("href", linkHref);
                        //     linkViewer.SetAttribute("target", "_blank");
                        //     linkViewer.InnerText = "Interactive XBRL viewer";

                        //     nodeValidationLinksRoot.AppendChild(linkViewer);
                        // }
                        // else
                        // {
                        //     var message = "Inline XBRL viewer report was not generated.";
                        //     appLogger.LogWarning(message);
                        // }

                        foreach (XmlNode nodeItem in nodeListValidationInformation)
                        {
                            var itemId = nodeItem.GetAttribute("id");
                            var itemValue = nodeItem.InnerText;
                            var nodeLink = retrieveResult.XmlPayload.CreateElement("a");

                            var addElement = true;
                            switch (itemId)
                            {
                                case "log":
                                    nodeLink.SetAttribute("href", RenderValidatorUri(itemValue));
                                    nodeLink.SetAttribute("target", "_blank");
                                    nodeLink.InnerText = "Validation details";
                                    break;

                                case "report":
                                    nodeLink.SetAttribute("href", RenderValidatorUri(itemValue));
                                    nodeLink.SetAttribute("target", "_blank");
                                    nodeLink.InnerText = "Validation report";
                                    break;

                                case "ixbrl":
                                    nodeLink.SetAttribute("href", RenderValidatorUri(itemValue));
                                    nodeLink.SetAttribute("target", "_blank");
                                    nodeLink.InnerText = "iXBRL viewer";
                                    break;

                                case "excel":
                                    nodeLink.SetAttribute("href", RenderValidatorUri(itemValue));
                                    nodeLink.SetAttribute("target", "_blank");
                                    nodeLink.InnerText = "Excel download";
                                    break;

                                default:
                                    addElement = false;
                                    break;
                            }


                            if (addElement) nodeValidationLinksRoot.AppendChild(nodeLink);
                        }
                        node.AppendChild(nodeValidationLinksRoot);

                    }

                    if (siteType == "local" || siteType == "dev") retrieveResult.XmlPayload.Save($"{logRootPathOs}/generatedreports.xml");

                    switch (format)
                    {
                        case "json":
                            //
                            // => Convert the XML to JSON
                            //
                            var nodeListReport = retrieveResult.XmlPayload.SelectNodes("/reports/report");
                            foreach (XmlNode nodeOutputChannel in nodeListReport)
                            {
                                var attrForceArray = retrieveResult.XmlPayload.CreateAttribute("json", "Array", "http://james.newtonking.com/projects/json");
                                attrForceArray.InnerText = "true";
                                nodeOutputChannel.Attributes.Append(attrForceArray);
                            }


                            retrieveResult.Payload = ConvertToJson(retrieveResult.XmlPayload);
                            break;

                        case "html":
                            //
                            // => Generate the contents for the UI
                            //
                            XsltArgumentList xsltArgs = new XsltArgumentList();
                            xsltArgs.AddParam("project-id", "", projectId);
                            xsltArgs.AddParam("token", "", projectVars.token);
                            retrieveResult.Payload = TransformXml(retrieveResult.XmlPayload, "generated-reports", xsltArgs);

                            break;

                        default:
                            return new TaxxorReturnMessage(false, "Unsupported format", $"format: {format}");
                    }



                    return retrieveResult;

                    //
                    // => Helper methods
                    //
                    string RenderValidatorUri(string dictKey)
                    {
                        return $"{((siteType == "local") ? uriArelleService : baseDomainName + folderPrefix)}{dictKey}";
                    }

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }



            }



            /// <summary>
            /// Retrieves the state of explicit the access control settings 
            /// </summary>
            /// <param name="userId"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> StoreGeneratedReportRepositoryComment(string projectId, string repositoryItemId, string comment)
            {
                var errorMessage = "There was an error storing the repository comment";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("repositoryItemId", repositoryItemId, RegexEnum.Guid, true);
                    inputValidationCollection.Add("comment", comment, RegexEnum.TextArea, true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Prepare the project variables object so that we can use it in the authorization process
                    //
                    projectVars.projectId = projectId;

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Store the comment in the repository
                    //
                    // The filing source data
                    var dataToPost = new Dictionary<string, string>();

                    dataToPost.Add("pid", projectVars.projectId);
                    dataToPost.Add("guid", repositoryItemId);
                    dataToPost.Add("comment", comment);

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "generatedreportsrepositorycomment", dataToPost, debugRoutine);

                    if (debugRoutine)
                    {
                        appLogger.LogInformation($"StoreGeneratedReportRepositoryComment() - {PrettyPrintXml(responseXml)}");
                    }

                    return new TaxxorReturnMessage(responseXml);


                    // // Render the result as a TaxxorReturnMessage
                    // return new TaxxorReturnMessage(true, "Successfully stored the repository comment", "", $"projectId: {projectId}");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveWebsiteUrls(string projectId)
            {
                var errorMessage = "There was an error retrieving the website URLs";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Prepare the project variables object so that we can use it in the authorization process
                    //
                    projectVars.projectId = projectId;

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Retrieve the URLs of the website preview sites
                    //
                    var editorId = RetrieveEditorIdFromProjectId(projectId);
                    dynamic jsonData = new ExpandoObject();
                    jsonData.sites = new Dictionary<string, ExpandoObject>();


                    var languages = RetrieveProjectLanguages(editorId);
                    foreach (var lang in languages)
                    {
                        var previewUrl = $"/htmlsite/{projectVars.guidLegalEntity}/{projectId}/{lang}/";
                        jsonData.sites.Add(lang, new ExpandoObject());
                        ((dynamic)jsonData.sites[lang]).url = previewUrl;

                        var testUrl = $"http://htmlsiteserver:8082/{siteType}/{projectVars.guidLegalEntity}/{projectId}/{lang}/";

                        // Check if the site exists by making a HEAD request
                        bool siteExists = false;
                        try 
                        {
                            using var httpClient = new HttpClient();
                            httpClient.Timeout = TimeSpan.FromSeconds(1); // 1 second timeout
                            var response = await httpClient.GetAsync(testUrl);
                            siteExists = response.IsSuccessStatusCode;
                        }
                        catch (Exception)
                        {
                            // If request fails, site doesn't exist
                            siteExists = false;
                            appLogger.LogWarning($"HTML website could not be reached does not exist: {testUrl}");
                        }

                        ((dynamic)jsonData.sites[lang]).exists = siteExists;
                    }



                    var json = (string)ConvertToJson(jsonData);
                    if (debugRoutine)
                    {
                        appLogger.LogInformation($"RetrieveWebsiteUrls() - {json}");
                    }

                    return new TaxxorReturnMessage(true, "Successfully retrieved the website URLs", json, $"projectId: {projectId}");
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