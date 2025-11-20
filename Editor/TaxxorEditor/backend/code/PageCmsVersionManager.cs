using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// Routines used in the Version Manager page
    /// </summary>
    /// 

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders the ajax version of the version manager
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GenerateVersionManagerContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var html = await GenerateVersionManagerContent();
            await response.OK(html, ReturnTypeEnum.Html, true);
        }


        /// <summary>
        /// Renders the HTML for the Version Manager Page
        /// </summary>
        /// <param name="debugRoutine"></param>
        public static async Task<string> GenerateVersionManagerContent(bool debugRoutine = false)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            if (siteType == "local" || siteType == "dev") debugRoutine = true;

            //
            // => Setup the XSLT stylesheet cache
            //
            SetupPdfStylesheetCache(reqVars);


            XmlDocument xmlVersionInformation = await DocumentStoreService.FilingVersion.Load(true);

            // Add a virtual tag that represents the current state
            /*
              <tag name="v11.13" hashData="bf960d67e3cd602e34d78ca640310075804b56b5" hashContent="ca1e311b29646793785c9d184e9514b84c2d6088">
                <message>Post external tables synchronization (workbook reference: interactive-worldmap, id: AR2018_worldmap-data) version</message>
                <date epoch="1555673846">04/19/2019 11:37:26</date>
                <author>
                <id>johan.thijs@philips.com</id>
                <name>Johan Thijs</name>
                <ip>::ffff:172.19.0.11</ip>
                </author>
              </tag> 
            */
            var nodeVirtualTag = xmlVersionInformation.CreateElement("virtual");

            // Clone the latest tag and modify it
            var nodeNewVirtualSourceTag = xmlVersionInformation.SelectSingleNode("/tags/tag");
            if (nodeNewVirtualSourceTag != null)
            {
                var nodeNewVirtualTag = nodeNewVirtualSourceTag.CloneNode(true);
                SetAttribute(nodeNewVirtualTag, "name", "current");
                SetAttribute(nodeNewVirtualTag, "hashData", "");
                SetAttribute(nodeNewVirtualTag, "hashContent", "");
                var nodeMessage = nodeNewVirtualTag.SelectSingleNode("message");
                if (nodeMessage != null) nodeMessage.InnerText = "Current content state";
                var nodeDate = nodeNewVirtualTag.SelectSingleNode("date");
                if (nodeDate != null) nodeDate.InnerText = GenerateLogTimestamp().SubstringBefore(".");

                nodeVirtualTag.AppendChild(nodeNewVirtualTag);
                xmlVersionInformation.DocumentElement.AppendChild(nodeVirtualTag);

            }
            else
            {
                return "<p>No versions available yet, please create one</p>";
            }

            // Normalize the date information to ISO
            var nodeListTags = xmlVersionInformation.SelectNodes("/tags/tag");
            foreach (XmlNode nodeTag in nodeListTags)
            {
                // Add a special attribute that we can use to sort the tags
                nodeTag.SetAttribute("namesort", (nodeTag.GetAttribute("name") ?? "0.0").Replace("v", "").Replace(".", ""));
            }

            // Dump the result
            if (debugRoutine)
            {
                var saved = SaveXmlDocument(xmlVersionInformation, logRootPathOs + "/_versionmanager-done.xml");
            }

            // Generate the HTML
            var xslPathOs = CalculateFullPathOs("cms_xsl_version-manager");

            XsltArgumentList xsltArgumentList = new XsltArgumentList();
            xsltArgumentList.AddParam("doc-configuration", "", xmlApplicationConfiguration);
            xsltArgumentList.AddParam("doc-hierarchy", "", reqVars.xmlHierarchyStripped);
            xsltArgumentList.AddParam("projectId", "", projectVars.projectId);
            xsltArgumentList.AddParam("editorId", "", projectVars.editorId);

            var html = TransformXml(xmlVersionInformation, xslPathOs, xsltArgumentList);

            return html;
        }

        /// <summary>
        /// Generate a version and automatically create a version label
        /// (Called from the Excel synchronization module)
        /// </summary>
        /// <param name="versionMessage"></param>
        /// <param name="majorVersion"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> GenerateVersion(string projectId, string versionMessage, bool majorVersion, ProjectVariables projectVars = null)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            /*
            Call the Taxxor Document Store to retrieve the version label
            */
            var dataToPost = new Dictionary<string, string>
            {
                { "pid", projectId }
            };

            // Call the service and retrieve the latest version label
            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "contentversion", dataToPost, debugRoutine, false, projectVars);

            if (XmlContainsError(xmlResponse))
            {
                return new TaxxorReturnMessage(xmlResponse);
            }
            else
            {
                // Grab the content version from the payload
                var contentVersion = xmlResponse.SelectSingleNode("/result/payload").InnerText.Trim();

                // Parse result
                if (!RegExpTest(@"^v\d+\.\d+$", contentVersion))
                {
                    return new TaxxorReturnMessage(false, "Unable to retrieve current content version", $"contentVersion: {contentVersion}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // Generate a new version label
                    var newVersionLabel = "";
                    var versionMajor = -1;
                    if (!Int32.TryParse(RegExpReplace(@"^v(\d+)\.(\d+)$", contentVersion, "$1"), out versionMajor))
                    {
                        appLogger.LogWarning($"Could not parse major version into an integer, stack-trace: {GetStackTrace()}");
                    }
                    var versionMinor = -1;
                    if (!Int32.TryParse(RegExpReplace(@"^v(\d+)\.(\d+)$", contentVersion, "$2"), out versionMinor))
                    {
                        appLogger.LogWarning($"Could not parse minor version into an integer, stack-trace: {GetStackTrace()}");
                    }
                    if (versionMajor == -1 || versionMinor == -1) return new TaxxorReturnMessage(false, "Could not render a new version label", $"versionMajor: {versionMajor.ToString()}, versionMinor: {versionMinor.ToString()}, stack-trace: {GetStackTrace()}");

                    newVersionLabel = (majorVersion) ? $"v{(versionMajor + 1).ToString()}.{versionMinor}" : $"v{versionMajor}.{(versionMinor + 1).ToString()}";

                    // Generate the version
                    var versionType = (majorVersion) ? "major" : "minor";
                    return await GenerateVersion(projectId, versionType, newVersionLabel, versionMessage, debugRoutine, false, projectVars);
                }
            }

        }

        /// <summary>
        /// Generates a new version of the filing document and the associated output channels using a version identifier and a version message as posted variables
        /// (Called from the Version Manager page)
        /// </summary>
        /// <param name="returnType"></param>
        /// <param name="debugLogic"></param>
        /// <returns></returns>
        public static async Task GenerateVersion(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugLogic = (siteType == "local" || siteType == "dev");

            // Retrieve posted data 
            var tagMessage = request.RetrievePostedValue("message", RegexEnum.TextArea, true, reqVars.returnType);
            var tagName = request.RetrievePostedValue("tagname", @"^v(\d+)\.(\d+)$", true, reqVars.returnType);
            var versionType = request.RetrievePostedValue("versiontype", @"^(major|minor)$", true, reqVars.returnType);
            try
            {
                // Make sure that the massage does not contain any XSS sensitive elements
                tagMessage = RemoveXssSensitiveElements(tagMessage);

                // Return a message to stop the XHR call
                var processStartedMessage = new TaxxorReturnMessage(true, "Generate version process started");
                await response.OK(processStartedMessage, ReturnTypeEnum.Json, true);
                await response.CompleteAsync();

                // Render the version
                TaxxorReturnMessage versionCreateResult = await GenerateVersion(projectVars.projectId, versionType, tagName, tagMessage, debugLogic);

                // Dump a failure in the logs
                if (!versionCreateResult.Success)
                {
                    appLogger.LogError(versionCreateResult.ToString());
                }

                await MessageToCurrentClient("VersionCreateDone", versionCreateResult);
            }
            catch (Exception ex)
            {
                // Log the issue
                var errorMessage = $"There was a problem generating version {tagName} on the server";
                appLogger.LogError(ex, errorMessage);

                // Render response for websocket and XHR
                var errorReturnMessage = new TaxxorReturnMessage(false, errorMessage);
                await MessageToCurrentClient("VersionCreateDone", errorReturnMessage);
                await response.Error(errorReturnMessage, reqVars.returnType, true);
            }
        }

        /// <summary>
        ///  Generates a new version of the filing document and the optionally the associated output channels
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionType"></param>
        /// <param name="tagName"></param>
        /// <param name="tagMessage"></param>
        /// <param name="debugLogic"></param>
        /// <param name="disableGenerateOutputChannels"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> GenerateVersion(string projectId, string versionType, string tagName, string tagMessage, bool debugLogic = false, bool disableGenerateOutputChannels = false, ProjectVariables projectVars = null)
        {
            var context = System.Web.Context.Current;
            if (projectVars == null) projectVars = RetrieveProjectVariables(context);


            var debugRoutine = siteType == "local" || siteType == "dev";





            var payloadBuilder = new StringBuilder();

            var errorMessage = "Unable to render version on the server";
            var generatePdfFiles = (versionType == "major") ? true : false;
            var generateHtmlFiles = (versionType == "major") ? false : false; // always disabled
            var generateWordFiles = (versionType == "major") ? true : false;
            var generateExcelFiles = (versionType == "major") ? true : false;

            var generateOutputChannels = generatePdfFiles || generateHtmlFiles;
            if (disableGenerateOutputChannels) generateOutputChannels = false;

            var step = 1;
            var stepMax = 1;

            if (generateOutputChannels) stepMax++;


            await _versionCreateMessageToClient(projectVars.currentUser.Id, "Start creating version");


            // Define if we post the files that we need for a version to the Taxxor Document Store, or if we we use a shared drive to transfer the files
            var postFiles = false;

            // Retrieve the editor ID
            var editorId = RetrieveEditorIdFromProjectId(projectId);

            // Render a PDF to store with the version snapshot
            PdfProperties pdfProperties = new PdfProperties();
            pdfProperties.Sections = "all";


            // Construct a properties file for the MS Word file that we want to generate
            MsOfficeFileProperties msWordProperties = new MsOfficeFileProperties();
            msWordProperties.Sections = "all";
            msWordProperties.RenderHiddenElements = false;

            // Construct a properties file for the MS Excel file that we want to generate
            MsOfficeFileProperties msExcelProperties = new MsOfficeFileProperties();
            msExcelProperties.Sections = "all";
            msExcelProperties.RenderHiddenElements = false;

            try
            {
                if (!string.IsNullOrEmpty(editorId))
                {
                    var errorMessages = new List<string>();
                    var errorDetails = "";

                    // Contains the information of the files we want to store on the Taxxor Document Store
                    Dictionary<string, string> files = [];

                    if (generateOutputChannels)
                    {
                        step = await _versionCreateMessageToClient(projectVars.currentUser.Id, "Start generating output channel files", step, stepMax);

                        //
                        // => CreateSetup the XSLT stylesheet cache
                        //
                        RequestVariables reqVars = RetrieveRequestVariables(context);
                        SetupPdfStylesheetCache(reqVars);


                        // Loop through all possible variants and generate the PDF so that we can store it alongside the content of the filing
                        var tempFolderPathOs = $"{dataRootPathOs}/temp/{CreateTimeBasedString("millisecond")}{RandomString(8, false)}";
                        if (!postFiles)
                        {
                            // Store the files on a shared location
                            tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/{CreateTimeBasedString("millisecond")}{RandomString(8, false)}";
                        }

                        try
                        {
                            Directory.CreateDirectory(tempFolderPathOs);
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError($"Unable to generate the version on the server. error: {ex}, stack-trace: {GetStackTrace()}");
                            return new TaxxorReturnMessage(false, "Could not create location", $"error: {ex}, stack-trace: {GetStackTrace()}");
                        }

                        var nodeListOutputChannels = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(editorId) + "]/output_channels/output_channel");



                        foreach (XmlNode nodeOutputChannel in nodeListOutputChannels)
                        {
                            var outputChannelType = GetAttribute(nodeOutputChannel, "type");
                            var nodeListOutputChannelVariants = nodeOutputChannel.SelectNodes("variants/variant");
                            foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                            {
                                var outputChannelVariantLanguage = GetAttribute(nodeVariant, "lang");
                                var outputChannelVariantId = GetAttribute(nodeVariant, "id");
                                var outputChannelVariantName = nodeVariant.SelectSingleNode("name")?.InnerText ?? "unknown";
                                //var contentType = "regular";

                                if (!string.IsNullOrEmpty(outputChannelVariantLanguage))
                                {

                                    // Construct a project variables object that we can send to the PDF Generator and the HTML generator
                                    var projectVariablesForOutputChannelGeneration = new ProjectVariables();

                                    // Fill with variables passed to this function
                                    projectVariablesForOutputChannelGeneration.projectId = projectId;
                                    projectVariablesForOutputChannelGeneration.versionId = "latest";
                                    //projectVariablesForPdfGeneration.did = "all";

                                    // Fill with variables retrieved from the context    
                                    projectVariablesForOutputChannelGeneration.editorContentType = "regular";
                                    projectVariablesForOutputChannelGeneration.reportTypeId = RetrieveReportTypeIdFromProjectId(projectId);
                                    projectVariablesForOutputChannelGeneration.outputChannelType = outputChannelType;
                                    projectVariablesForOutputChannelGeneration.editorId = editorId;
                                    projectVariablesForOutputChannelGeneration.outputChannelVariantId = outputChannelVariantId;
                                    projectVariablesForOutputChannelGeneration.outputChannelVariantLanguage = outputChannelVariantLanguage;
                                    projectVariablesForOutputChannelGeneration.currentUser.Id = projectVars.currentUser.Id;

                                    // Retrieve the key that we need to use for locating the hierarchy XML document for this output channel
                                    var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(editorId, outputChannelType, outputChannelVariantId, outputChannelVariantLanguage);


                                    if (outputChannelType == "website")
                                    {
                                        // TODO: kick off the website generator and generate the complete website so it's fixed on the server
                                        // Store the complete website as a zip file on the server???
                                    }
                                    else
                                    {

                                        //
                                        // => Generate PDF files
                                        //
                                        if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                                        {


                                            if (generatePdfFiles)
                                            {


                                                // TODO: for now we will render all output channels as a PDF and single HTML file

                                                // PDF will initially be stored on the local file system (in the Taxxor Editor)
                                                var pdfFileName = $"{outputChannelVariantId}-{outputChannelVariantLanguage}.pdf";
                                                var tempPdfFilePathOs = $"{tempFolderPathOs}/{pdfFileName}";

                                                await _versionCreateMessageToClient(projectVars.currentUser.Id, $"* Generating {pdfFileName}");

                                                Console.Write($"- tempPdfFilePathOs: {tempPdfFilePathOs}");

                                                // Retrieve output channel hierarchy to get the top node value and use that as a string to send to the PDF generator
                                                var xmlHierarchyDoc = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;
                                                var booktitle = xmlHierarchyDoc.SelectSingleNode("/items/structured/item/web_page/linkname").InnerText;

                                                if (booktitle != "")
                                                {
                                                    if (pdfProperties.PdfGeneratorStringSet.ContainsKey("booktitle"))
                                                    {
                                                        pdfProperties.PdfGeneratorStringSet["booktitle"] = booktitle;
                                                    }
                                                    else
                                                    {
                                                        pdfProperties.PdfGeneratorStringSet.Add("booktitle", booktitle);
                                                    }

                                                }

                                                //- Determine the layout in which we need to render the PDF documents (using defaults as defined in the data configuration for the client)
                                                pdfProperties.Layout = RetrieveOutputChannelDefaultLayout(projectVariablesForOutputChannelGeneration).Layout;

                                                // Render the PDF and store it in the system folder
                                                var xmlPdfRenderResult = await PdfService.RenderPdf<XmlDocument>(pdfProperties, projectVariablesForOutputChannelGeneration, tempPdfFilePathOs, debugLogic);

                                                if (XmlContainsError(xmlPdfRenderResult))
                                                {
                                                    errorMessage = $"Error rendering {outputChannelVariantName} PDF";
                                                    errorDetails = $"recieved: {xmlPdfRenderResult.OuterXml}, stack-trace: {GetStackTrace()}";

                                                    // Do not stop the version rendering, but move on with the next PDF
                                                    appLogger.LogError($"{errorMessage}: {errorDetails}");
                                                    errorMessages.Add(errorMessage);
                                                    await _versionCreateMessageToClient(projectVars.currentUser.Id, $"! There was a problem rendering {pdfFileName}, moving on with the next PDF");
                                                    continue;
                                                }

                                                if (postFiles)
                                                {
                                                    // Store a Base64 encoded version of the PDF
                                                    files.Add(pdfFileName, Base64EncodeBinaryFile(tempPdfFilePathOs));
                                                }
                                                else
                                                {
                                                    files.Add(pdfFileName, tempPdfFilePathOs);
                                                }

                                            }
                                        }
                                        else
                                        {
                                            appLogger.LogWarning($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}'] - unable to generate PDF document, stack-trace: {GetStackTrace()}");

                                            payloadBuilder.AppendLine($"Unable to render PDF document for {outputChannelHierarchyMetadataKey}");
                                        }
                                    }


                                    //
                                    // => Generate MS Word Files
                                    //
                                    if (generateWordFiles)
                                    {
                                        // MS Word will initially be stored on the local file system (in the Taxxor Editor)
                                        var mswordFileName = $"{outputChannelVariantId}-{outputChannelVariantLanguage}.docx";
                                        var tempMsWordFilePathOs = $"{tempFolderPathOs}/{mswordFileName}";

                                        await _versionCreateMessageToClient(projectVars.currentUser.Id, $"* Generating {mswordFileName}");

                                        XmlDocument msWordRenderResult = await ConversionService.RenderMsWord<XmlDocument>(msWordProperties, projectVariablesForOutputChannelGeneration, tempMsWordFilePathOs, debugRoutine);
                                        if (XmlContainsError(msWordRenderResult))
                                        {
                                            errorMessage = $"Error rendering {outputChannelVariantName} MS Word file";
                                            errorDetails = $"recieved: {msWordRenderResult.OuterXml}, stack-trace: {GetStackTrace()}";

                                            // We do not quit the bulk generation process, but we record the problem and move on
                                            appLogger.LogError($"{errorMessage}: {errorDetails}");
                                            errorMessages.Add(errorMessage);
                                            await _outputGenerationProgressMessage($"WARNING: {errorMessage}");
                                            continue;
                                        }

                                        if (postFiles)
                                        {
                                            // Store a Base64 encoded version of the PDF
                                            files.Add(mswordFileName, Base64EncodeBinaryFile(tempMsWordFilePathOs));
                                        }
                                        else
                                        {
                                            files.Add(mswordFileName, tempMsWordFilePathOs);
                                        }
                                    }

                                    //
                                    // => Generate Excel files
                                    //
                                    if (generateExcelFiles)
                                    {
                                        // MS Word will initially be stored on the local file system (in the Taxxor Editor)
                                        var msexcelFileName = $"{outputChannelVariantId}-{outputChannelVariantLanguage}.xlsx";
                                        var tempMsExcelFilePathOs = $"{tempFolderPathOs}/{msexcelFileName}";

                                        await _versionCreateMessageToClient(projectVars.currentUser.Id, $"* Generating {msexcelFileName}");

                                        var msExcelRenderResult = await ConversionService.RenderMsExcel(msExcelProperties, projectVariablesForOutputChannelGeneration, tempMsExcelFilePathOs, debugRoutine);
                                        if (!msExcelRenderResult.Success)
                                        {
                                            if (!msExcelRenderResult.Message.ToLower().StartsWith("warning:"))
                                            {
                                                errorMessage = $"Error rendering {outputChannelVariantName} MS Excel file";
                                                errorDetails = $"recieved: {msExcelRenderResult.ToString()}, stack-trace: {GetStackTrace()}";

                                                // We do not quit the bulk generation process, but we record the problem and move on
                                                appLogger.LogError($"{errorMessage}: {errorDetails}");
                                                errorMessages.Add(errorMessage);
                                                await _outputGenerationProgressMessage($"WARNING: {errorMessage}");
                                            }
                                            else
                                            {
                                                await _outputGenerationProgressMessage($"Skipping MS Excel generation ({outputChannelVariantName} does not contain any tables)");
                                            }
                                            continue;
                                        }

                                        if (postFiles)
                                        {
                                            // Store a Base64 encoded version of the PDF
                                            files.Add(msexcelFileName, Base64EncodeBinaryFile(tempMsExcelFilePathOs));
                                        }
                                        else
                                        {
                                            files.Add(msexcelFileName, tempMsExcelFilePathOs);
                                        }
                                    }

                                    //
                                    // => Generate HTML file of the output channel
                                    //
                                    if (generateHtmlFiles)
                                    {
                                        if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                                        {
                                            var htmlFileName = $"{outputChannelVariantId}-{outputChannelVariantLanguage}.html";

                                            await _versionCreateMessageToClient(projectVars.currentUser.Id, $"* Generating {htmlFileName}");

                                            // Retrieve the HTML that we used as a basis for generating the PDF file
                                            string htmlPdf = await RetrieveLatestPdfHtml(projectVariablesForOutputChannelGeneration, "all", false, false);

                                            // Add the HTML to the files that we will post to the Taxxor Document Store
                                            if (htmlPdf.ToLower().StartsWith("error:"))
                                            {
                                                appLogger.LogError($"Could retrieve PDF HTML, htmlPdf: {htmlPdf}, stack-trace: {GetStackTrace()}");

                                                await _versionCreateMessageToClient(projectVars.currentUser.Id, $"! There was a problem retrieving {htmlFileName}, moving on with the next HTML document");
                                                continue;
                                                // return new TaxxorReturnMessage(false, "Could not retrieve full HTML file", $"Message from service: {htmlPdf}, stack-trace: {GetStackTrace()}");
                                            }
                                            else
                                            {

                                                var htmlFilePathOs = $"{tempFolderPathOs}/{outputChannelVariantId}-{outputChannelVariantLanguage}.html";
                                                await TextFileCreateAsync(htmlPdf, htmlFilePathOs);
                                                if (postFiles)
                                                {
                                                    files.Add(htmlFileName, htmlPdf);
                                                }
                                                else
                                                {
                                                    files.Add(htmlFileName, htmlFilePathOs);
                                                }

                                            }
                                        }
                                        else
                                        {
                                            appLogger.LogWarning($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}'] - unable to render full HTML file of output channel, stack-trace: {GetStackTrace()}");

                                            payloadBuilder.AppendLine($"Unable to render cache document for {outputChannelHierarchyMetadataKey}");
                                        }
                                    }

                                }
                            }
                        }
                    }

                    // Create the version
                    step = await _versionCreateMessageToClient(projectVars.currentUser.Id, "Start generating version in the Taxxor Document Store", step, stepMax);
                    var xmlVersionCreateResult = await DocumentStoreService.FilingVersion.Create(tagName, tagMessage, files, debugRoutine, projectVars);

                    if (XmlContainsError(xmlVersionCreateResult))
                    {
                        appLogger.LogError($"Unable to generate the version on the server. response: {xmlVersionCreateResult.OuterXml}, stack-trace: {GetStackTrace()}");
                        return new TaxxorReturnMessage(xmlVersionCreateResult);
                    }
                    else
                    {
                        appLogger.LogInformation($"Successfully created version {tagName}");
                        await _versionCreateMessageToClient(projectVars.currentUser.Id, "Finished creating version");
                        var storedFiles = string.Format("({0})", string.Join(",", files.Keys));
                        return new TaxxorReturnMessage(true, $"Successfully generated version {tagName}", payloadBuilder.ToString(), $"storedFiles: {storedFiles}");
                    }
                }
                else
                {
                    return new TaxxorReturnMessage(false, "Could not retrieve editor and project id", $"stack-trace: {GetStackTrace()}");
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, ex.ToString());
            }
        }


        /// <summary>
        /// Utility routine to send a message to the client over a websocket
        /// </summary>
        /// <param name="message"></param>
        /// <param name="step"></param>
        /// <param name="maxStep"></param>
        /// <returns></returns>
        private static async Task<int> _versionCreateMessageToClient(string userId, string message, int step = -1, int maxStep = -1)
        {
            var messageToClient = new TaxxorReturnMessage(true, (step == -1) ? message : $"Step {step}/{maxStep}: {message}");

            await MessageToOtherClient("VersionCreateDataProgress", userId, messageToClient);

            if (step > -1) step++;

            return step;
        }

    }
}