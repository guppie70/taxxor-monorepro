using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Routines used for the html styler page
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Picks up the HTML of the document that we want to style and manipulates the content so that we can use livereload for changing CSS and immediately checking the result
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RenderHtmlStylerContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local");



            // Generate a complete projectvars object that we can use furtheron
            projectVars = await FillProjectVariablesFromProjectId(projectVars);

            // The MD5 encrypted string of the folder where the document is located that we need to render
            var md5PathOs = request.RetrievePostedValue("type", RegexEnum.Default, true, ReturnTypeEnum.Html);

            // Map the MD5 hash passed to a folder contained on the filesystem
            var htmlStylerDataFolderPathOs = RenderHtmlStylerOutputPathOs(projectVars.projectId, null);
            string? htmlStylerRootFolderPathOs = null;

            if (Directory.Exists(htmlStylerDataFolderPathOs))
            {
                foreach (var directoryPathOs in Directory.EnumerateDirectories(htmlStylerDataFolderPathOs))
                {
                    var directoryName = Path.GetFileName(directoryPathOs);
                    var md5 = EncryptText(directoryPathOs, EncryptionTypeEnum.MD5);
                    if (md5 == md5PathOs) htmlStylerRootFolderPathOs = directoryPathOs;
                }
            }
            else
            {
                HandleError("Unable to locate document root folder", $"stack-trace: {GetStackTrace()}", 404);
            }

            var pagedMediaContent = (PdfRenderEngine == "pagedjs" && htmlStylerRootFolderPathOs.ToLower().Contains("esma"));

            // Determine the filename of the document that we need to display
            var documentType = "html";
            var defaultDocument = "index.html";
            if (Path.GetFileName(htmlStylerRootFolderPathOs).ToLower().Contains("xbrl"))
            {
                var instanceDocumentRootPathOs = RetrieveInstanceDocumentPath(htmlStylerRootFolderPathOs);
                if (string.IsNullOrEmpty(instanceDocumentRootPathOs))
                {
                    HandleError("Unable to locate document", $"instanceDocumentRootPathOs: {instanceDocumentRootPathOs}, stack-trace: {GetStackTrace()}", 404);
                }
                else
                {
                    documentType = "ixbrl";

                    // The default document is the name of the instance document.
                    if (!pagedMediaContent && !htmlStylerRootFolderPathOs.ToLower().Contains("esma")) defaultDocument = Path.GetFileName(instanceDocumentRootPathOs);
                }
            }
            else if (Path.GetFileName(htmlStylerRootFolderPathOs).ToLower().Contains("6k"))
            {
                documentType = "ixbrl";
            }

            // Pick up the HTML file that we want to stream to the output
            var htmlFilePathOs = $"{htmlStylerRootFolderPathOs}/{defaultDocument}";
            appLogger.LogInformation($"htmlFilePathOs: {htmlFilePathOs}");
            if (File.Exists(htmlFilePathOs))
            {
                try
                {
                    var htmlToReturn = "";
                    var xhtmlDoc = new XmlDocument();
                    xhtmlDoc.Load(htmlFilePathOs);



                    if (pagedMediaContent || htmlStylerRootFolderPathOs.ToLower().Contains("esma"))
                    {
                        // Potentially process the HTML before returning it
                        htmlToReturn = xhtmlDoc.OuterXml.Replace(@"<style type=""text/css""><![CDATA[", @"<style type=""text/css"">").Replace("]]></style>", "</style>");
                    }
                    else
                    {
                        var baseHrefPath = Path.GetDirectoryName(htmlFilePathOs).Replace(websiteRootPathOs, "");

                        // Change the content
                        var xpathHead = "/html/head";
                        var nodeXhtmlHead = xhtmlDoc.SelectSingleNodeAgnostic(xpathHead);
                        if (nodeXhtmlHead == null)
                        {
                            HandleError(ReturnTypeEnum.Html, "Could not find head node", $"pdfGeneratorHtmlFile: {htmlFilePathOs}");
                        }
                        else
                        {
                            // Strip out potential metadata node
                            var nodeListMetadata = xhtmlDoc.SelectNodesAgnostic("//metadata");
                            if (nodeListMetadata.Count > 0) RemoveXmlNodes(nodeListMetadata);

                            // Strip out existing style nodes
                            var nodeListInlineCssStyles = nodeXhtmlHead.SelectNodesAgnostic("style");
                            foreach (XmlNode nodeInlineStyle in nodeListInlineCssStyles)
                            {
                                RemoveXmlNode(nodeInlineStyle);
                            }

                            // Add base href node for resolving images, etc properly
                            var nodeBaseHref = xhtmlDoc.CreateElement("base");
                            SetAttribute(nodeBaseHref, "href", $"{baseHrefPath}/");
                            nodeXhtmlHead.AppendChild(nodeBaseHref);

                            var xmlClientAssets = new XmlDocument();
                            switch (documentType)
                            {
                                case "html":
                                    xmlClientAssets = Extensions.RenderOutputChannelCssAndJs(projectVars, "fullhtml");
                                    break;

                                case "ixbrl":
                                    xmlClientAssets = Extensions.RenderOutputChannelCssAndJs(projectVars, "reportgenerator");
                                    break;

                                default:
                                    HandleError("No support for this document type", $"documentType: {documentType}");
                                    break;
                            }

                            if (debugRoutine)
                            {
                                Console.WriteLine($"- documentType: {documentType}");
                                Console.WriteLine("****************** ASSETS **********************");
                                Console.WriteLine(PrettyPrintXml(xmlClientAssets));
                                Console.WriteLine("****************************************");
                            }

                            // Add the new stylesheet in the head of the document
                            foreach (XmlNode nodeCss in xmlClientAssets.SelectNodes("/assets/css/file"))
                            {
                                var cssUri = nodeCss.GetAttribute("uri");
                                if (!string.IsNullOrEmpty(cssUri))
                                {
                                    var nodeLinkedStylesheet = xhtmlDoc.CreateElement("link");
                                    SetAttribute(nodeLinkedStylesheet, "rel", "stylesheet");
                                    SetAttribute(nodeLinkedStylesheet, "href", cssUri);
                                    nodeXhtmlHead.AppendChild(nodeLinkedStylesheet);
                                }
                                else
                                {
                                    appLogger.LogWarning("Unable to find a URI for the stylesheet");
                                }
                            }

                            // TODO - Inject Javascript?

                            // Correct javascript links
                            var nodeListLinkedJs = xhtmlDoc.SelectNodesAgnostic("/html/body/script");
                            foreach (XmlNode nodeLinkedJs in nodeListLinkedJs)
                            {
                                var uri = GetAttribute(nodeLinkedJs, "src") ?? "";
                                if (uri.Contains("full-html.js"))
                                {
                                    SetAttribute(nodeLinkedJs, "src", $"/outputchannels/{TaxxorClientId}/website/js/full-html.js");
                                }
                            }
                        }


                        htmlToReturn = xhtmlDoc.OuterXml;

                    }

                    // Stream the content to the output channels
                    await response.OK(htmlToReturn, ReturnTypeEnum.Html, true);
                }
                catch (Exception ex)
                {
                    HandleError(ReturnTypeEnum.Html, "Could not load HTML styling source data", $"htmlFilePathOs: {htmlFilePathOs}, error: {ex}");
                }
            }
            else
            {
                HandleError(ReturnTypeEnum.Html, "Could not load HTML styling source data", $"htmlFilePathOs: {htmlFilePathOs}");
            }

        }

        /// <summary>
        /// Calculates the path for the HTML styler output directory
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="documentType"></param>
        /// <returns></returns>
        public static string RenderHtmlStylerOutputPathOs(string projectId, string? documentType)
        {
            var folderPathOs = $"{websiteRootPathOs}/custom/{TaxxorClientId}/develop/{projectId}{((string.IsNullOrEmpty(documentType) ? "" : "/" + documentType))}";

            if (!Directory.Exists(folderPathOs)) Directory.CreateDirectory(folderPathOs);

            return folderPathOs;
        }

        /// <summary>
        /// Retrieves the content that was sent to the PDF Generator or the Pandoc service
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RenderGeneratorInputContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local");

            // Retrieve posted values
            var inputGeneratorType = request.RetrievePostedValue("type", RegexEnum.Default, true, ReturnTypeEnum.Html);

            // Generate a complete projectvars object that we can use furtheron
            projectVars = await FillProjectVariablesFromProjectId(projectVars);

            // Debug information
            var debugInfo = $"inputGeneratorType: {inputGeneratorType}";

            try
            {
                var htmlContent = "";
                var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp";
                var generatorInputFilePathOs = "";
                var directoryFilter = "";
                var errorMessage = "";

                // Determine the path where we can find the document that we want to steam to the client
                switch (inputGeneratorType)
                {
                    case "pdf":
                        generatorInputFilePathOs = CalculateFullPathOs("pdfgenerator-dumpfile");
                        break;

                    case "word":
                        generatorInputFilePathOs = CalculateFullPathOs("wordgenerator-dumpfile");
                        break;

                    case "drawinggenerator":
                        directoryFilter = $"drawingconversion_{projectVars.projectId}*";
                        var drawingDirs = new DirectoryInfo(tempFolderPathOs).GetDirectories(directoryFilter);
                        if (drawingDirs.Length > 0)
                        {
                            var drawingGeneratorDirInfo = drawingDirs.OrderByDescending(d => d.LastWriteTimeUtc).First();
                            generatorInputFilePathOs = $"{drawingGeneratorDirInfo.FullName}/_visuals-utility.html";
                        }
                        else
                        {
                            errorMessage = $"Unable to find drawing generator directory with filter '{directoryFilter}' in {tempFolderPathOs}";
                            appLogger.LogError(errorMessage);
                            throw new Exception(errorMessage);
                        }
                        break;

                    case "graphgenerator":
                        directoryFilter = $"graphrenderer_{projectVars.projectId}*";

                        var graphDirs = new DirectoryInfo(tempFolderPathOs).GetDirectories(directoryFilter);
                        if (graphDirs.Length > 0)
                        {
                            var drawingGeneratorDirInfo = graphDirs.OrderByDescending(d => d.LastWriteTimeUtc).First();
                            generatorInputFilePathOs = $"{drawingGeneratorDirInfo.FullName}/_graphs-utility.html";
                        }
                        else
                        {
                            errorMessage = $"Unable to find graph generator directory with filter '{directoryFilter}' in {tempFolderPathOs}";
                            appLogger.LogError(errorMessage);
                            throw new Exception(errorMessage);
                        }
                        break;

                    default:
                        throw new Exception($"Generator input type '{inputGeneratorType}' is not supported yet");
                }

                // Test if the generator file exists
                if (!File.Exists(generatorInputFilePathOs))
                {
                    appLogger.LogError($"generatorInputFilePathOs: {generatorInputFilePathOs} does not exist");
                    throw new Exception("Generator source path could not be located");
                }

                // Log a bit of context
                appLogger.LogDebug($"generatorInputFilePathOs: {generatorInputFilePathOs}");

                // Retrieve the content
                htmlContent = await RetrieveTextFile(generatorInputFilePathOs);

                // Post process the content
                switch (inputGeneratorType)
                {
                    case "pdf":
                        // - Replace the base HREF with the domain name that is currently used in the request
                        var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{siteType}']");
                        var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
                        var protocol = (nodeCurrentDomain.GetAttribute("ssl") == "true") ? "https" : "http";
                        htmlContent = htmlContent.Replace("<base href=\"https://editor:4812/", $"<base href=\"{protocol}://{currentDomainName}/");

                        // - Hack to bypass the token access control system because this is an authenticated request
                        htmlContent = htmlContent.Replace("token=", $"v=");
                        break;

                    case "graphgenerator":
                    case "drawinggenerator":
                        // - Fix domain name references
                        htmlContent = RegExpReplace(@"""http.*?//.*?(/.*?)""", htmlContent, "\"$1\"", true);
                        break;
                }

                // Stream the content to the output channels
                await response.OK(htmlContent, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                var responseToClient = $@"
                <html>
                    <head>
                        <title>{reqVars.pageTitle}</title>
                    </head>
                    <body>
                        <h1>There was an error retrieving the data</h1>
                        <h2>Error details:</h2>
                        {ex}
                        <h2>Debug info</h2>
                        <p>{debugInfo}</p>
                        <pre>
{projectVars.DumpToString()}
                        </pre>
                    </body>
                </html>";

                await response.Error(responseToClient, ReturnTypeEnum.Html, true);
            }


        }



    }
}