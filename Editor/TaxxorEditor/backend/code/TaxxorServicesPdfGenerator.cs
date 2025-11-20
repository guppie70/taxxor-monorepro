using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.Extensions.Logging;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace Taxxor
{

    public partial class ConnectedServices
    {

        /// <summary>
        /// Utility class to interact with the PDF Generator Service
        /// </summary>
        public static class PdfService
        {



            /// <summary>
            /// Renders a "GIT" style HTML file containing the differences between two full reports
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="pdfProperties"></param>
            /// <param name="projectVariablesForPdfGeneration"></param>
            /// <param name="pdfPath"></param>
            /// <param name="debugLogic"></param>
            /// <returns></returns>
            public static async Task<T> RenderRawFullDiffHtml<T>(PdfProperties pdfProperties, ProjectVariables projectVariablesForPdfGeneration, string pdfPath = null, bool debugLogic = false)
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                var debugRoutine = debugLogic || reqVars.isDebugMode == true || siteType == "local" || siteType == "dev";

                // Extend the project variables object
                projectVariablesForPdfGeneration.projectRootPath = projectVars.projectRootPath;
                projectVariablesForPdfGeneration.guidClient = projectVars.guidClient;
                projectVariablesForPdfGeneration.guidEntityGroup = projectVars.guidEntityGroup;
                projectVariablesForPdfGeneration.guidLegalEntity = projectVars.guidLegalEntity;

                // Retrieve the type of data that we need to return
                var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                // Global variables
                var routineDebugInfo = "";
                var errorMessage = "An error occurred while generating your raw HTML";
                var debugInfo = "";
                var diffHtml = "";
                try
                {
                    XmlDocument? pdfResult = RetrieveTemplate("pdfgenerator_result");

                    if (siteType == "local")
                    {
                        Console.WriteLine($"PDF Properties:");
                        Console.WriteLine(pdfProperties.DumpToString());
                    }

                    /*
                    Render or retrieve the HTML that we need for the PDF
                     */
                    if (pdfProperties.Mode == "diff")
                    {
                        // - in diff mode, we always render the document using the default layout
                        pdfProperties.Layout = RetrieveOutputChannelDefaultLayout(projectVariablesForPdfGeneration).Layout;

                        // a) Retrieve the documents for the diff html
                        var htmlFileName = $"{projectVariablesForPdfGeneration.outputChannelVariantId}-{projectVariablesForPdfGeneration.outputChannelVariantLanguage}.html";
                        var htmlBaseVersion = "";
                        var htmlLatestVersion = "";


                        // 1) Retrieve the base HTML from the cache location in the Taxxor Data Store
                        htmlBaseVersion = await RetrieveHistoricalPdfHtml(projectVariablesForPdfGeneration, pdfProperties.Base, pdfProperties.Sections, pdfProperties.UseContentStatus, pdfProperties.RenderScope);

                        // Pretty pring the output so that it compares nicely
                        var xmlBaseVersion = new XmlDocument();
                        xmlBaseVersion.LoadHtml(htmlBaseVersion);
                        htmlBaseVersion = PrettyPrintXml(xmlBaseVersion);

                        // Message to the client
                        await _outputGenerationProgressMessage("Successfully retrieved base content for PDF diff generation");

                        if (htmlBaseVersion.StartsWith("ERROR:", StringComparison.CurrentCulture))
                        {
                            // Write the error to the output stream, write the error in the log file and stop any further processing
                            return (T)RenderErrorResponse("Could not retrieve base document for comparison", $"Retrieved HTML: {TruncateString(htmlBaseVersion, 50)}, stack-trace: {GetStackTrace()}");
                        }


                        // 2) Retrieve latest HTML
                        if (pdfProperties.Latest == "current")
                        {
                            // Retrieve the latest content using the previewer URL from the website
                            htmlLatestVersion = await RetrieveLatestPdfHtml(projectVariablesForPdfGeneration, pdfProperties.Sections, pdfProperties.UseContentStatus, false, pdfProperties.RenderScope);

                            // Message to the client
                            await _outputGenerationProgressMessage("Successfully retrieved latest content for PDF generation");
                        }
                        else
                        {
                            htmlLatestVersion = await RetrieveHistoricalPdfHtml(projectVariablesForPdfGeneration, pdfProperties.Latest, pdfProperties.Sections, pdfProperties.UseContentStatus, pdfProperties.RenderScope);

                            // Message to the client
                            await _outputGenerationProgressMessage("Successfully retrieved latest content for PDF generation");

                            if (htmlLatestVersion.StartsWith("ERROR:", StringComparison.CurrentCulture))
                            {
                                // Write the error to the output stream, write the error in the log file and stop any further processing
                                return (T)RenderErrorResponse("Could not retrieve latest document for comparison", $"Retrieved HTML: {TruncateString(htmlLatestVersion, 50)}, stack-trace: {GetStackTrace()}");
                            }
                        }

                        // Pretty pring the output so that it compares nicely
                        var xmlLatest = new XmlDocument();
                        xmlLatest.LoadHtml(htmlLatestVersion);
                        htmlLatestVersion = PrettyPrintXml(xmlLatest);

                        if (debugRoutine)
                        {
                            await TextFileCreateAsync(htmlBaseVersion, $"{logRootPathOs}/_diff-pdf-base.html");
                            await TextFileCreateAsync(htmlLatestVersion, $"{logRootPathOs}/_diff-pdf-latest.html");
                        }

                        //
                        // => Store the files on the disk so we can compare them
                        //
                        var fileName = RandomString(12, false);
                        var baseHtmlPathOs = $"{dataRootPathOs}/temp/{fileName}-base.html";
                        var latestHtmlPathOs = $"{dataRootPathOs}/temp/{fileName}-latest.html";
                        await TextFileCreateAsync(htmlBaseVersion, baseHtmlPathOs);
                        await TextFileCreateAsync(htmlLatestVersion, latestHtmlPathOs);

                        //
                        // => Use GIT to generate the Diff HTML
                        //
                        List<string> gitCommandList = [$"git diff --no-index {baseHtmlPathOs} {latestHtmlPathOs}"];
                        var gitResult = GitCommand(gitCommandList, applicationRootPathOs, reqVars.returnType, false);
                        if (gitResult == null)
                        {
                            throw new Exception("Unable to render diff HTML");
                        }

                        var diffHtmlContents = gitResult.Trim();


                        //
                        // => Generate the report
                        //
                        diffHtml = $@"
                    <html>
                        <head>
                            <title>Diff Report</title>
                            <meta charset='UTF-8'>
                            <link rel='stylesheet' type='text/css' href='{CalculateFullPathOs("html-diff-css")}'>
                            <script type='text/javascript' src='{CalculateFullPathOs("html-diff-js")}'></script>
                            <style type='text/css'>
                            	body {{
                            		font-family: 'Roboto', Helvetica, Arial, sans-serif;
                            		font-size: 14px;
									line-height: 1.42857;
									color: #333333;
                            	}}
                            </style>
                        </head>
                        <body>
                            <h1>Diff Report</h1>
                            <p>Base commit reference: {pdfProperties.Base}, latest commit reference: {pdfProperties.Latest}</p>
                            <p>Difference details:</p>
                            <div id='diffresult' style='display: none'>
                            {(string.IsNullOrEmpty(diffHtmlContents) ? "No differences found" : HtmlEncode(diffHtmlContents))}
                            </div>
                            <div id='formatted'></div>
                            <script type='text/javascript'>
                                // Plain JS function to decode an HTML encoded string
                                var decodeHTML = function (html) {{
                                    var txt = document.createElement('textarea');
                                    txt.innerHTML = html;
                                    return txt.value;
                                }};
                                // Turn the diff output into a nicely formatted HTML view
                                var elDiffResult=document.getElementById('diffresult');
                                var elFormattedDiff=document.getElementById('formatted');
                                var diffHtml = Diff2Html.getPrettyHtml(
                                    decodeHTML(elDiffResult.innerHTML),
                                    {{
                                        inputFormat: 'diff', 
                                        showFiles: true, 
                                        matching: 'lines', 
                                        outputFormat: 'line-by-line'
                                    }}
                                );
                                elFormattedDiff.innerHTML = diffHtml;
                            </script>
                        </body>
                    </html>";

                        if (debugRoutine)
                        {
                            await TextFileCreateAsync(diffHtml, $"{logRootPathOs}/_diff-pdf.html");
                        }
                    }
                    else
                    {

                    }

                    //
                    // => Return the content of the file that we have requested
                    //
                    switch (returnType)
                    {
                        case "xml":
                            // Store the generated file on the disk
                            var generatedRawHtmlFilePathOs = "";
                            if (pdfPath == null)
                            {
                                var pdfFileName = RandomString(12, false) + ".html";

                                // Store the PDF in a temporary location on this server
                                generatedRawHtmlFilePathOs = dataRootPathOs + "/temp/" + pdfFileName;
                            }
                            else
                            {
                                // Use the file path that was passed to the routine
                                generatedRawHtmlFilePathOs = pdfPath;
                            }

                            // Test is the directory to store the PDF file in exists
                            if (!Directory.Exists(Path.GetDirectoryName(generatedRawHtmlFilePathOs)))
                            {
                                errorMessage = "Unable to locate folder to store raw HTML in";
                                debugInfo = $"Folder: {Path.GetDirectoryName(generatedRawHtmlFilePathOs)} does not exist, stack-trace: {GetStackTrace()}";

                                appLogger.LogError($"{errorMessage} - {debugInfo}");

                                // Return an error XML Document
                                return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));
                            }

                            try
                            {
                                await TextFileCreateAsync(diffHtml, generatedRawHtmlFilePathOs);
                            }
                            catch (Exception ex)
                            {
                                errorMessage = "Unable to convert and store PDF content";
                                debugInfo = $"error: {ex}, stack-trace: {GetStackTrace()}";

                                appLogger.LogError($"{errorMessage} - {debugInfo}");

                                // Return an error XML Document
                                return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));
                            }

                            // Complete the XML we are going to return
                            pdfResult.SelectSingleNode("/result/pdfpath").InnerText = generatedRawHtmlFilePathOs;
                            pdfResult.SelectSingleNode("/result/debuginfo").InnerText = routineDebugInfo;

                            // Return the XmlDocument
                            return (T)Convert.ChangeType(pdfResult, typeof(T));

                        default:
                            // Return the Base64 encoded string
                            return (T)Convert.ChangeType(diffHtml, typeof(T));
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = "Unable to render the raw HTML document";
                    debugInfo = $"pdfProperties: {pdfProperties.DumpToString()}, stack-trace: {GetStackTrace()}";
                    appLogger.LogError(ex, $"{errorMessage}. pdfProperties: {pdfProperties.DumpToString()}");
                    return (T)RenderErrorResponse(errorMessage, debugInfo);
                }


                T RenderErrorResponse(string errorMessage, string debugInfo)
                {
                    return returnType switch
                    {
                        "xml" => (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T)),// Return an error XML Document
                        _ => (T)Convert.ChangeType($"ERROR: {errorMessage}, debug-info: {debugInfo}", typeof(T)),// Return the error as a string
                    };
                }
            }



            /// <summary>
            /// Renders a PDF using the PDF Service and returns the result as
            /// - XmlDocument
            /// - Base64 encoded string
            /// - Byte[] array
            /// </summary>
            /// <param name="pdfProperties"></param>
            /// <param name="projectVariablesForPdfGeneration"></param>
            /// <param name="pdfPath"></param>
            /// <param name="debugRoutine"></param>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public static async Task<T> RenderPdf<T>(PdfProperties pdfProperties, ProjectVariables projectVariablesForPdfGeneration, string? pdfPath = null, bool debugLogic = false)
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                var debugRoutine = debugLogic || reqVars.isDebugMode == true || siteType == "local" || siteType == "dev";

                // Extend the project variables object
                projectVariablesForPdfGeneration.projectRootPath = projectVars.projectRootPath;
                projectVariablesForPdfGeneration.guidClient = projectVars.guidClient;
                projectVariablesForPdfGeneration.guidEntityGroup = projectVars.guidEntityGroup;
                projectVariablesForPdfGeneration.guidLegalEntity = projectVars.guidLegalEntity;

                // Retrieve the type of data that we need to return
                var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                if (pdfProperties.Sections == "foobar")
                {
                    appLogger.LogWarning($"Problem PDF render request detected with 'foobar'. stack-trace:\n{GetStackTrace()}");
                }

                // Global variables
                var routineDebugInfo = "";
                var errorMessage = "An error occurred while generating your PDF";
                var debugInfo = "";
                try
                {
                    XmlDocument? pdfResult = RetrieveTemplate("pdfgenerator_result");

                    if (siteType == "local")
                    {
                        Console.WriteLine($"PDF Properties:");
                        Console.WriteLine(pdfProperties.DumpToString());
                    }


                    /*
                    Render the PDF and grab the result in an XML envelope
                     */

                    // Generate the object that contains the variables that we want to post
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "serveasdownload", "false" },
                        { "type", "json" },
                        { "securepdf", pdfProperties.Secure ? "true" : "false" },
                        { "postprocess", pdfProperties.PostProcess }
                    };

                    /*
                    Render or retrieve the HTML that we need for the PDF
                     */
                    if (pdfProperties.Mode == "diff")
                    {
                        // - in diff mode, we always render the document using the default layout
                        pdfProperties.Layout = RetrieveOutputChannelDefaultLayout(projectVariablesForPdfGeneration).Layout;

                        // a) Retrieve the documents for the diff html
                        var htmlFileName = $"{projectVariablesForPdfGeneration.outputChannelVariantId}-{projectVariablesForPdfGeneration.outputChannelVariantLanguage}.html";
                        var htmlBaseVersion = "";
                        var htmlLatestVersion = "";



                        // 1) Retrieve the base HTML from the cache location in the Taxxor Data Store
                        htmlBaseVersion = await RetrieveHistoricalPdfHtml(projectVariablesForPdfGeneration, pdfProperties.Base, pdfProperties.Sections, pdfProperties.UseContentStatus, pdfProperties.RenderScope);

                        // Message to the client
                        await _outputGenerationProgressMessage("Successfully retrieved base content for PDF diff generation");

                        if (htmlBaseVersion.StartsWith("ERROR:", StringComparison.CurrentCulture))
                        {
                            // Write the error to the output stream, write the error in the log file and stop any further processing
                            return (T)RenderErrorResponse("Could not retrieve base document for comparison", $"Retrieved HTML: {TruncateString(htmlBaseVersion, 50)}, stack-trace: {GetStackTrace()}");
                        }

                        var xmlBaseVersion = new XmlDocument();
                        xmlBaseVersion.LoadXml(htmlBaseVersion);
                        if (debugRoutine) await xmlBaseVersion.SaveAsync($"{logRootPathOs}/_diff-pdf-base.xml", false, true);

                        // Check if we need to add data-hierarchical-level attributes
                        var nodeListArticlesToAdjust = xmlBaseVersion.SelectNodes("/data/content/*[not(@data-hierarchical-level)]");
                        if (nodeListArticlesToAdjust.Count > 0)
                        {
                            try
                            {
                                // Load the hierarchy
                                var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);
                                var outputChannelHierarchy = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;

                                // Find the article ID in the output channel hierarchy and set the hierarchical level based on that node
                                foreach (XmlNode nodeArticleToAdjust in nodeListArticlesToAdjust)
                                {
                                    var itemId = GetAttribute(nodeArticleToAdjust, "id");
                                    if (!string.IsNullOrEmpty(itemId))
                                    {
                                        int hierarchicalLevel = outputChannelHierarchy.SelectNodes($"ancestor-or-self::item[@id='{itemId}']").Count;
                                        SetAttribute(nodeArticleToAdjust, "data-hierarchical-level", hierarchicalLevel.ToString());
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogWarning($"Could not load output channel hierarchy for correcting the hierarchical level attributes. error: {ex}");
                            }
                        }


                        //
                        // => Prepare the base PDF HTML for processing
                        //
                        var baseHtmlPreperationResult = await PreparePdfHtmlForRendering(xmlBaseVersion, projectVariablesForPdfGeneration, pdfProperties);
                        if (!baseHtmlPreperationResult.Success)
                        {
                            return (T)RenderErrorResponse(baseHtmlPreperationResult.Message, baseHtmlPreperationResult.DebugInfo);
                        }
                        else
                        {
                            // Stream errors and warnings to the websocket
                            await _outputGenerationLogErrorAndWarnings(baseHtmlPreperationResult);
                        }
                        xmlBaseVersion.LoadXml(baseHtmlPreperationResult.XmlPayload.OuterXml);
                        if (debugRoutine) await xmlBaseVersion.SaveAsync($"{logRootPathOs}/_diff-pdf-base-1.xml", false, true);

                        //
                        // => Prepare the base XHTML for using in diff comparison
                        //
                        var xhtmBaseDiffComparisonPreparationResult = PrepareSectionXhtmlForDiffPdfGeneration(xmlBaseVersion);
                        if (!xhtmBaseDiffComparisonPreparationResult.Success)
                        {
                            return (T)RenderErrorResponse(xhtmBaseDiffComparisonPreparationResult.Message, xhtmBaseDiffComparisonPreparationResult.DebugInfo);
                        }
                        htmlBaseVersion = xhtmBaseDiffComparisonPreparationResult.Payload;
                        if (debugRoutine) await TextFileCreateAsync(htmlBaseVersion, $"{logRootPathOs}/_diff-pdf-base-2.xml");


                        // 2) Retrieve latest HTML
                        if (pdfProperties.Latest == "current")
                        {
                            // Retrieve the latest content using the previewer URL from the website
                            htmlLatestVersion = await RetrieveLatestPdfHtml(projectVariablesForPdfGeneration, pdfProperties.Sections, pdfProperties.UseContentStatus, false, pdfProperties.RenderScope);

                            // Message to the client
                            await _outputGenerationProgressMessage("Successfully retrieved latest content for PDF generation");
                        }
                        else
                        {
                            htmlLatestVersion = await RetrieveHistoricalPdfHtml(projectVariablesForPdfGeneration, pdfProperties.Latest, pdfProperties.Sections, pdfProperties.UseContentStatus, pdfProperties.RenderScope);

                            // Message to the client
                            await _outputGenerationProgressMessage("Successfully retrieved latest content for PDF generation");

                            if (htmlLatestVersion.StartsWith("ERROR:", StringComparison.CurrentCulture))
                            {
                                // Write the error to the output stream, write the error in the log file and stop any further processing
                                return (T)RenderErrorResponse("Could not retrieve latest document for comparison", $"Retrieved HTML: {TruncateString(htmlLatestVersion, 50)}, stack-trace: {GetStackTrace()}");
                            }
                        }

                        var xmlLatestVersion = new XmlDocument();
                        xmlLatestVersion.LoadXml(htmlLatestVersion);
                        if (debugRoutine) await xmlBaseVersion.SaveAsync($"{logRootPathOs}/_diff-pdf-latest.xml", false, true);


                        //
                        // => Prepare the latest PDF HTML for processing
                        //
                        var latestHtmlPreparationResult = await PreparePdfHtmlForRendering(xmlLatestVersion, projectVariablesForPdfGeneration, pdfProperties);
                        if (!latestHtmlPreparationResult.Success)
                        {
                            return (T)RenderErrorResponse(latestHtmlPreparationResult.Message, latestHtmlPreparationResult.DebugInfo);
                        }
                        else
                        {
                            // Stream errors and warnings to the websocket
                            await _outputGenerationLogErrorAndWarnings(latestHtmlPreparationResult);
                        }
                        htmlLatestVersion = latestHtmlPreparationResult.XmlPayload.OuterXml;
                        if (debugRoutine) await latestHtmlPreparationResult.XmlPayload.SaveAsync($"{logRootPathOs}/_diff-pdf-latest-1.xml", false, true);


                        // 3) Cleanup the XHTML and Add HTML chrome to the web page (head, title, body, etc) to the HTML fragments so that we send a complete version to the PDF Generator
                        var xhtmlLatestDiffComparisonPreparationResult = await PrepareSectionXhtmlForDiffPdfGeneration(htmlLatestVersion, reqVars, projectVariablesForPdfGeneration, pdfProperties);
                        if (!xhtmlLatestDiffComparisonPreparationResult.Success)
                        {
                            return (T)RenderErrorResponse(xhtmlLatestDiffComparisonPreparationResult.Message, xhtmlLatestDiffComparisonPreparationResult.DebugInfo);
                        }
                        htmlLatestVersion = xhtmlLatestDiffComparisonPreparationResult.Payload;
                        if (debugRoutine) await TextFileCreateAsync(htmlLatestVersion, $"{logRootPathOs}/_diff-pdf-latest-2.xml");


                        if (debugRoutine)
                        {
                            await TextFileCreateAsync(htmlBaseVersion, $"{logRootPathOs}/_diff-pdf-base.html");
                            await TextFileCreateAsync(htmlLatestVersion, $"{logRootPathOs}/_diff-pdf-latest.html");
                        }

                        //
                        // => Call the PDF Service to render the Diff HTML
                        //
                        var diffHtml = await PdfService.RenderDiffHtml(htmlBaseVersion, htmlLatestVersion);

                        if (debugRoutine)
                        {
                            await TextFileCreateAsync(diffHtml, $"{logRootPathOs}/_diff-pdf.html");
                        }

                        //
                        // => Post process the diff HTML a bit so that we can send the HTML to the PDF generator for rendering the binary file
                        //
                        var xmlDiffHtml = new XmlDocument();
                        xmlDiffHtml.LoadHtml(diffHtml);

                        //
                        // => Improve the track changes marks
                        //
                        PostProcessTrackChangesHtml(ref xmlDiffHtml);
                        if (debugRoutine)
                        {
                            await TextFileCreateAsync(diffHtml, $"{logRootPathOs}/_diff-pdf.postprocessed.html");
                        }

                        //
                        // => Replace the dynamic placeholders in the path
                        //
                        xmlDiffHtml.ReplaceContent(DynamicPlaceholdersResolve(xmlDiffHtml, reqVars, projectVars, null), true);


                        var xmlDiffHtmlReworked = new XmlDocument();
                        xmlDiffHtmlReworked.LoadXml("<content><metadata><hierarchy/></metadata></content>");

                        // Select the articles and stick in basic XML structure
                        var nodeListDiffArticles = xmlDiffHtml.SelectNodes("//article");
                        foreach (XmlNode nodeDiffArticle in nodeListDiffArticles)
                        {
                            xmlDiffHtmlReworked.DocumentElement.AppendChild(xmlDiffHtmlReworked.ImportNode(nodeDiffArticle, true));
                        }
                        // Retrieve removed section method
                        xmlDiffHtmlReworked.DocumentElement.SetAttribute("removedsectionmethod", xmlDiffHtml.DocumentElement.GetAttribute("removedsectionmethod") ?? "unknown");
                        if (debugRoutine) await xmlDiffHtmlReworked.SaveAsync($"{logRootPathOs}/_diff-pdf.reworked.xml", false, true);

                        // 
                        // => Settings which are render engine specific
                        // 
                        if (PdfRenderEngine == "pagedjs")
                        {
                            pdfProperties.Mode = "nochrome";
                        }

                        //
                        // => Render a complete HTML document for Prince to base the PDF binary on
                        //
                        TaxxorReturnMessage result = await CreateCompletePdfHtml(xmlDiffHtmlReworked.OuterXml, reqVars, projectVariablesForPdfGeneration, pdfProperties);
                        if (!result.Success)
                        {
                            return (T)RenderErrorResponse(result.Message, result.DebugInfo);
                        }
                        else
                        {
                            RemoveDummyEmptyElementData(result.XmlPayload);

                            // In the Message property we have inserted the translated HTML
                            diffHtml = result.XmlPayload.OuterXml;
                        }


                        // Add the HTML to the data to post
                        dataToPost.Add("url", diffHtml);
                    }
                    else
                    {
                        var xmlPdfHtml = new XmlDocument();
                        var pdfHtml = "";
                        if (pdfProperties.Mode == "preview")
                        {
                            pdfHtml = pdfProperties.Html;

                            // => Load the XHTML in an XmlDocument object
                            xmlPdfHtml.LoadHtml("<div class=\"body-wrapper\"><metadata><hierarchy></hierarchy></metadata>" + pdfHtml + "</div>");
                        }
                        else
                        {


                            pdfHtml = await RetrieveLatestPdfHtml(projectVariablesForPdfGeneration, pdfProperties.Sections, pdfProperties.UseContentStatus, pdfProperties.HideCurrentPeriodDatapoints, pdfProperties.RenderScope);
                            // await TextFileCreateAsync(pdfHtml, $"{logRootPathOs}/_pdf-generator.fromdatastore.html");

                            // Message to the client
                            await _outputGenerationProgressMessage("Successfully retrieved content for PDF generation");

                            // Optionally convert the HTML content for the PDF to Lorem Ipsum
                            if (pdfProperties.UseLoremIpsum)
                            {
                                xmlPdfHtml.LoadXml(pdfHtml);

                                var xmlPdfHtmlLoremIpsum = await ConvertToLoremIpsum(xmlPdfHtml, projectVariablesForPdfGeneration.outputChannelVariantLanguage);

                                // Message to the client
                                await _outputGenerationProgressMessage("Converted content to fake (lorem ipsum) text");

                                // Remove the XML declaration
                                if (xmlPdfHtmlLoremIpsum.FirstChild.NodeType == XmlNodeType.XmlDeclaration) xmlPdfHtmlLoremIpsum.RemoveChild(xmlPdfHtmlLoremIpsum.FirstChild);

                                pdfHtml = xmlPdfHtmlLoremIpsum.OuterXml;
                            }

                            xmlPdfHtml.LoadXml(pdfHtml);
                        }



                        // => Convert to tables-only version
                        if (pdfProperties.TablesOnly)
                        {
                            OutputTablesOnly(ref xmlPdfHtml);
                        }



                        //
                        // => Prepare the current PDF HTML for processing
                        //
                        var latestHtmlPrepResult = await PreparePdfHtmlForRendering(xmlPdfHtml, projectVariablesForPdfGeneration, pdfProperties);
                        if (!latestHtmlPrepResult.Success)
                        {
                            return (T)RenderErrorResponse(latestHtmlPrepResult.Message, latestHtmlPrepResult.DebugInfo);
                        }
                        else
                        {
                            // Stream errors and warnings to the websocket
                            await _outputGenerationLogErrorAndWarnings(latestHtmlPrepResult);
                        }
                        pdfHtml = latestHtmlPrepResult.XmlPayload.OuterXml;

                        if (debugRoutine) await TextFileCreateAsync(pdfHtml, $"{logRootPathOs}/_pdf-generator.tofinaltransform.html");

                        // 
                        // => Settings which are render engine specific (1.2)
                        // 
                        if (PdfRenderEngine == "pagedjs")
                        {
                            pdfProperties.Mode = "nochrome";
                        }

                        //
                        // => Render a complete HTML document for Prince to base the PDF binary on
                        //                 
                        TaxxorReturnMessage result = await CreateCompletePdfHtml(pdfHtml, reqVars, projectVariablesForPdfGeneration, pdfProperties);
                        if (!result.Success)//(1.3)
                        {
                            return (T)RenderErrorResponse(result.Message, result.DebugInfo);
                        }
                        else
                        {
                            // In the Message property we have inserted the translated HTML
                            pdfHtml = result.XmlPayload.OuterXml;
                        }



                        // Cleanup the result before sending it to the PDF Generator
                        // var xmlPdfHtmlToClean = new XmlDocument();
                        // xmlPdfHtmlToClean.LoadHtml(pdfHtml);
                        // RemoveRenderedGraphs(ref xmlPdfHtmlToClean);
                        // RemoveEmptyElements(ref xmlPdfHtmlToClean);
                        // AddCommentInEmptyElements(ref xmlPdfHtmlToClean);

                        // Add the HTMl to the data we will post to the PDF Generator Service
                        dataToPost.Add("url", pdfHtml);
                    }

                    // 
                    // => Post process the content we will send to the PDF Generator based on the PDF render engine used
                    // 
                    switch (PdfRenderEngine)
                    {
                        case "pagedjs":

                            // - Inject the PDF HTML into the viewer file wich is part of the Report Design Package
                            var pagedMediaEnvelopeResult = await InjectInPagedMediaEnvelope(projectVariablesForPdfGeneration, dataToPost["url"]);
                            if (!pagedMediaEnvelopeResult.Success) return (T)RenderErrorResponseTaxxorReturnMessage(pagedMediaEnvelopeResult);

                            // - Replace the existing HTML data with the one we received back from the routine
                            dataToPost.Remove("url");
                            dataToPost.Add("url", pagedMediaEnvelopeResult.Payload);

                            break;

                            // Other render engines to be placed here

                    }




                    // 
                    // => Potentially send custom HTML to the PDF Generator (for testing purposes)
                    //
                    if (siteType == "local")
                    {

                        // Console.WriteLine("---- PDF HTML ----");
                        // Console.Write(TruncateString(dataToPost["url"], 2000));
                        // Console.WriteLine("------------------");

                        // var pdfGeneratorTestHtmlFilePathOs = $"{logRootPathOs}/pdfgenerator/test.html";
                        // if(File.Exists(pdfGeneratorTestHtmlFilePathOs)){
                        //     var pdfTestFileContent = await RetrieveTextFile(pdfGeneratorTestHtmlFilePathOs);
                        //     if(pdfTestFileContent.Length>100){
                        //         dataToPost.Remove("url");
                        //         dataToPost.Add("url", "https://editor:4812/outputchannels/tiekinetix/pdf/templates/pagedjsviewer/index.html?1626269371224&dataurl=https:%2F%2Feditor:4812%2Fapi%2Fpagedmedia%2Fsource%3Fpid%3Dar21%26did%3Dreport-root%26ocvariantid%3Darpdfen%26layout%3Dregular%26mode%3Dnochrome%26renderscope%3Dtop-level#the-supervisory-board");
                        //     }
                        // }
                    }


                    // Store the file on the disk so that we can inspect it
                    await GeneratePdfGeneratorDebugFile(dataToPost["url"]);

                    /*
                    Add parameters for the PDF generation engine
                    */
                    ExtendPdfGeneratorPostParameters(ref dataToPost, pdfProperties, projectVariablesForPdfGeneration.editorId, projectVariablesForPdfGeneration.outputChannelType);

                    // Method (route) ID from the PDF Service XML description file depends on the mode with which we want to generate the PDF
                    //string serviceMethodId = (pdfProperties.Mode == "diff") ? "rendertrackchangespdf" : "renderpdf";
                    string serviceMethodId = "renderpdf";

                    // Message to the client
                    await _outputGenerationProgressMessage("Start Taxxor PDF Generator");


                    //
                    // => Render the PDF and grab the result as binary data
                    //
                    dataToPost.Remove("type");
                    dataToPost.Add("type", "binary");
                    dataToPost.Remove("serveasdownload");
                    dataToPost.Add("serveasdownload", "false");
                    var apiUrl = GetServiceUrlByMethodId(ConnectedServiceEnum.PdfService, serviceMethodId);
                    byte[]? pdfBytes = null;

                    try
                    {
                        pdfBytes = await RetrieveBinaryWithPost(apiUrl, dataToPost);
                    }
                    catch (Exception ex)
                    {
                        errorMessage = "Failed to retrieve PDF from generator service";
                        debugInfo = $"Error calling PDF service: {ex.Message}. Stack trace: {ex.StackTrace}";

                        // Add other possible debuginformation that we have gathered
                        if (routineDebugInfo != "")
                        {
                            debugInfo += Environment.NewLine + routineDebugInfo;
                        }

                        appLogger.LogError(ex, $"{errorMessage} - {debugInfo}");

                        switch (returnType)
                        {
                            case "xml":
                                return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));
                            default:
                                return (T)Convert.ChangeType($"ERROR: {errorMessage}, debug-info: {debugInfo}", typeof(T));
                        }
                    }



                    // // Capture the base64 encoded result
                    // var nodePdfContent = xmlRestResult.SelectSingleNode("/root/content");
                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        errorMessage = "Unable to locate PDF content";
                        debugInfo = $"Response from the PDF Generator did not contain a base64 encoded string representing the PDF binary file. stack-trace: {GetStackTrace()}";

                        appLogger.LogError($"{errorMessage} - {debugInfo}");

                        switch (returnType)
                        {
                            case "xml":
                                // Return an error XML Document
                                return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));
                            default:
                                // Return the error as a string
                                return (T)Convert.ChangeType($"ERROR: {errorMessage}, debug-info: {debugInfo}", typeof(T));
                        }

                    }
                    else
                    {

                        // Return the content of the file that we have requested
                        switch (returnType)
                        {
                            case "xml":
                                //
                                // Store the generated file on the disk
                                //
                                var generatedPdfFilePathOs = "";
                                if (pdfPath == null) // 7.9
                                {
                                    // Store the PDF in a temporary location on this server
                                    generatedPdfFilePathOs = dataRootPathOs + "/temp/" + RandomString(12, false) + ".pdf";
                                }
                                else
                                {
                                    // Use the file path that was passed to the routine
                                    generatedPdfFilePathOs = pdfPath;
                                }

                                // Test is the directory to store the PDF file in exists
                                if (!Directory.Exists(Path.GetDirectoryName(generatedPdfFilePathOs)))
                                {
                                    errorMessage = "Unable to locate folder to store PDF in";
                                    debugInfo = $"Folder: {Path.GetDirectoryName(generatedPdfFilePathOs)} does not exist, stack-trace: {GetStackTrace()}";

                                    appLogger.LogError($"{errorMessage} - {debugInfo}");

                                    // Return an error XML Document
                                    return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));
                                }

                                try
                                {

                                    // Convert directly to file stream to avoid keeping large byte arrays in memory
                                    using (var fileStream = new FileStream(generatedPdfFilePathOs, FileMode.Create, FileAccess.Write))
                                    {
                                        // Write the bytes directly to the file
                                        await fileStream.WriteAsync(pdfBytes);
                                    }

                                    // Explicitly trigger GC only for large PDFs
                                    if (pdfBytes.Length > 52428800) // 50MB in bytes
                                    {
                                        GC.Collect();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    errorMessage = "Unable to convert and store PDF content";
                                    debugInfo = $"error: {ex}, stack-trace: {GetStackTrace()}";

                                    appLogger.LogError($"{errorMessage} - {debugInfo}");

                                    // Return an error XML Document
                                    return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));
                                }

                                //
                                // Complete the XML we are going to return
                                //
                                pdfResult.SelectSingleNode("/result/pdfpath").InnerText = generatedPdfFilePathOs; // 3.3
                                pdfResult.SelectSingleNode("/result/debuginfo").InnerText = routineDebugInfo;

                                // Return the XmlDocument
                                return (T)Convert.ChangeType(pdfResult, typeof(T)); // 5.2

                            case "bytearray":
                                // Return the PDF file as binary content that we can directly stream to the client
                                return (T)Convert.ChangeType(pdfBytes, typeof(T));
                            default:
                                // Return the Base64 encoded string
                                return (T)Convert.ChangeType(Convert.ToBase64String(pdfBytes), typeof(T));
                        }

                    }


                }
                catch (Exception ex)
                {
                    errorMessage = "Unable to render the PDF document";
                    debugInfo = $"pdfProperties: {pdfProperties.DumpToString()}, stack-trace: {GetStackTrace()}";
                    appLogger.LogError(ex, $"{errorMessage}. pdfProperties: {pdfProperties.DumpToString()}");
                    return (T)RenderErrorResponse(errorMessage, debugInfo);
                }


                T RenderErrorResponse(string errorMessage, string debugInfo)
                {
                    switch (returnType)
                    {
                        case "xml":
                            // Return an error XML Document
                            return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));
                        default:
                            // Return the error as a string
                            return (T)Convert.ChangeType($"ERROR: {errorMessage}, debug-info: {debugInfo}", typeof(T));
                    }
                }

                T RenderErrorResponseTaxxorReturnMessage(TaxxorReturnMessage taxxorReturnMessage)
                {
                    switch (returnType)
                    {
                        case "xml":
                            // Return an error XML Document
                            return (T)Convert.ChangeType(taxxorReturnMessage.ToXml(), typeof(T));
                        default:
                            // Return the error as a string
                            return (T)Convert.ChangeType($"ERROR: {taxxorReturnMessage.Message}, debug-info: {taxxorReturnMessage.DebugInfo}", typeof(T));
                    }
                }


                /// <summary>
                /// Helper routine to dump the PDF Generator content on the disk so that we can use it for debugging purposes
                /// </summary>
                /// <param name="pdfContent"></param>
                /// <returns></returns>
                async Task GeneratePdfGeneratorDebugFile(string pdfContent)
                {
                    if (debugRoutine || projectVars.currentUser.Permissions.All)
                    {
                        try
                        {
                            var pdfGeneratorDumpFile = CalculateFullPathOs("pdfgenerator-dumpfile");
                            await TextFileCreateAsync(pdfContent, pdfGeneratorDumpFile);
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Could not write the HTML to log the data that we send to the PDF Generator");
                        }
                    }
                }



            }





            /// <summary>
            /// Renders paged media inline HTML that you can load into a browser without any processing required
            /// </summary>
            /// <param name="xmlHtmlIn"></param>
            /// <param name="returnType"></param>
            /// <returns></returns>
            public static async Task<TaxxorReturnMessage> RenderInlinePagedMediaHtml(XmlDocument xmlHtmlIn, ReturnTypeEnum returnType = ReturnTypeEnum.Xml, string logFolderPathOs = null)
            {

                var dataToPost = new Dictionary<string, string>();
                dataToPost.Add("html", xmlHtmlIn.OuterXml);

                // Send request to the PDF Generator
                var inlineHtml = await CallTaxxorConnectedService<string>(ConnectedServiceEnum.PdfService, RequestMethodEnum.Post, "renderinlinehtml", dataToPost, true);

                // Log the response from the PDF Generator containing the processed HTML
                await TextFileCreateAsync(inlineHtml, $"{((logFolderPathOs == null) ? logRootPathOs : logFolderPathOs)}/ixbrl-from-htmlinliner.html");

                // Detect error or not
                if (inlineHtml.ToLower().StartsWith("error"))
                {
                    return new TaxxorReturnMessage(false, "Error retrieving inline paged media HTML", inlineHtml);
                }

                // 
                // => Return the content
                // 
                switch (returnType)
                {
                    case ReturnTypeEnum.Xml:
                        try
                        {
                            var xmlHtmlContent = new XmlDocument();
                            xmlHtmlContent.LoadHtml(inlineHtml, true);
                            await xmlHtmlContent.SaveAsync($"{((logFolderPathOs == null) ? logRootPathOs : logFolderPathOs)}/ixbrl-from-htmlinliner.as.xml.html");

                            //
                            // => Replace the CSS style with the unescaped one
                            //
                            var nodeStyle = xmlHtmlContent.SelectSingleNode("//style[@id='documentstylesincharacterdata']");
                            if (nodeStyle != null)
                            {
                                // Unescape the CSS rules
                                var unEscapedCss = nodeStyle.InnerText.Replace("&gt;", ">");

                                // Remove the existing CDATA wrapper as it will be re-created by the logic below
                                unEscapedCss = unEscapedCss.Replace("<![CDATA[", "").Replace("]]>", "");

                                // Re-insert the CSS rules inside the style node wrapped in a CDATA section
                                nodeStyle.InnerText = "";
                                XmlCDataSection CData = xmlHtmlContent.CreateCDataSection(unEscapedCss);
                                nodeStyle.AppendChild(CData);

                                nodeStyle.RemoveAttribute("id");
                            }
                            await xmlHtmlContent.SaveAsync($"{((logFolderPathOs == null) ? logRootPathOs : logFolderPathOs)}/ixbrl-from-htmlinliner.as.xml.reworked.html");

                            return new TaxxorReturnMessage(true, "Successfully retrieved inline paged media HTML", xmlHtmlContent);
                        }
                        catch (Exception ex)
                        {
                            return new TaxxorReturnMessage(false, "There was an error parsing the inline paged media HYML", inlineHtml, ex.ToString());
                        }

                    case ReturnTypeEnum.Html:
                    case ReturnTypeEnum.Txt:
                    case ReturnTypeEnum.Xhtml:
                        return new TaxxorReturnMessage(true, "Successfully retrieved inline paged media HTML", inlineHtml);

                    default:

                        return new TaxxorReturnMessage(false, "Unsupported type to return", $"returnType: {ReturnTypeEnumToString(returnType)}");
                }

            }


            /// <summary>
            /// Calls the PDF Service to render a difference ("track changes") html by comparing the base HTML file against the latest HTML file
            /// </summary>
            /// <param name="_base">Base HTML document used for comparison</param>
            /// <param name="latest">Latest HTML document used for comparison</param>
            /// <param name="debugLogic"></param>
            /// <returns></returns>
            public static async Task<string> RenderDiffHtml(string _base, string latest, bool debugLogic = false)
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                var debuggerInfo = "";
                var errorMessage = "";
                var debugInfo = "";

                /*
                 * Send the two versions to the diff service
                 */
                var dataToPost = new Dictionary<string, string>();
                dataToPost.Add("pid", projectVars.projectId);
                dataToPost.Add("base", _base);
                dataToPost.Add("latest", latest);

                // Use the "diff" routine in the PDF Service to render the "track changes HTML"
                XmlDocument xmlRestResult = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.PdfService, RequestMethodEnum.Post, "generatetrackchangeshtml", dataToPost, true);

                // await xmlRestResult.SaveAsync($"{logRootPathOs}/_diff-pdf.raw.xml", true);

                if (debugLogic)
                {
                    debuggerInfo += "Raw REST result: " + Environment.NewLine + xmlRestResult.OuterXml + Environment.NewLine;
                }

                if (XmlContainsError(xmlRestResult))
                {
                    // An error has occurred - now constuct a solid error message
                    errorMessage = "An error occurred while generating the content for a track changes PDF";
                    debugInfo = xmlRestResult.SelectSingleNode("/error/debuginfo").InnerText;

                    // Check if we can capture additional details from the PDF Generator
                    XmlNode? nodeErrorInfoPdfGenerator = xmlRestResult.SelectSingleNode("/error/httpresponse/root/error");
                    if (nodeErrorInfoPdfGenerator != null)
                    {
                        errorMessage = nodeErrorInfoPdfGenerator.SelectSingleNode("message").InnerText;
                        debugInfo = nodeErrorInfoPdfGenerator.SelectSingleNode("debuginfo").InnerText;
                    }
                    else
                    {
                        nodeErrorInfoPdfGenerator = xmlRestResult.SelectSingleNode("/error/httpresponse");
                        if (nodeErrorInfoPdfGenerator != null)
                        {
                            debugInfo = nodeErrorInfoPdfGenerator.InnerText;
                        }
                    }

                    // Add the status code to the debug information if available
                    var nodeStatusCode = xmlRestResult.SelectSingleNode("/error/httpstatuscode");
                    if (nodeStatusCode != null)
                    {
                        debugInfo = "Received HTTP Status Code " + nodeStatusCode.InnerText + ". Additional information: " + debugInfo;
                    }

                    // Add other possible debuginformation that we have gathered
                    if (debuggerInfo != "")
                    {
                        debugInfo += Environment.NewLine + debuggerInfo;
                    }

                    HandleError(reqVars.returnType, GenerateErrorXml(errorMessage, debugInfo + $", stack-trace: {GetStackTrace()}"));
                }

                var diffContent = "";
                var nodeContent = xmlRestResult.SelectSingleNode("/root/content");
                if (nodeContent == null)
                {
                    debugInfo = $"response xml: {TruncateString(xmlRestResult.OuterXml, 1000)}, _base: {TruncateString(_base, 1000)}, latest: {TruncateString(latest, 1000)}";
                    HandleError(reqVars.returnType, GenerateErrorXml("There was an error rendering the diff content", debugInfo + $", stack-trace: {GetStackTrace()}"));
                }
                else
                {
                    diffContent = HttpUtility.UrlDecode(nodeContent.InnerText);
                }


                return diffContent;
            }


            /// <summary>
            /// Injects the Paged Media HTML into the paged media viewer file (part of the Report Design Package) so that it can be used by the new PDF Generator logic
            /// </summary>
            /// <param name="baseXhtml"></param>
            /// <returns></returns>
            public static async Task<TaxxorReturnMessage> InjectInPagedMediaEnvelope(ProjectVariables projectVars, string baseXhtml)
            {

                var xmlPdfContent = new XmlDocument();
                try
                {
                    xmlPdfContent.LoadXml(baseXhtml);

                    return await InjectInPagedMediaEnvelope(projectVars, xmlPdfContent, false);
                }
                catch (Exception ex)
                {
                    var errorMessage = "There was an error constructing the Paged Media HTML content";
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, ex.ToString());
                }
            }

            /// <summary>
            /// Injects the Paged Media XML into the paged media viewer file (part of the Report Design Package) so that it can be used by the new PDF Generator logic
            /// </summary>
            /// <param name="xmlPdfContent"></param>
            /// <returns></returns>
            public static async Task<TaxxorReturnMessage> InjectInPagedMediaEnvelope(ProjectVariables projectVars, XmlDocument xmlPdfContent, bool returnXml = true)
            {

                var context = System.Web.Context.Current;
                try
                {
                    // var viewerTemplatePathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/pdf/{LegalEntity}/templates/pagedjsviewer/index.html";
                    var viewerTemplatePathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/pdf/templates/pagedjsviewer/index.html";
                    if (!File.Exists(viewerTemplatePathOs))
                    {
                        // For an environment that contains multiple dev packages for different legal entities, we need to look for the correct one
                        viewerTemplatePathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/pdf/{projectVars.guidLegalEntity}/templates/pagedjsviewer/index.html";
                        if (!File.Exists(viewerTemplatePathOs))
                        {
                            return new TaxxorReturnMessage(false, "There was an error constructing the Paged Media HTML content", $"Unable to locate viewer file viewerTemplatePathOs. viewerTemplatePathOs: {viewerTemplatePathOs}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    var baseHrefUri = Path.GetDirectoryName(viewerTemplatePathOs).Replace(websiteRootPathOs, "https://editor:4812") + "/";

                    var htmlViewer = await RetrieveTextFile(viewerTemplatePathOs);
                    if (htmlViewer.Length < 100) return new TaxxorReturnMessage(false, "There was an error constructing the Paged Media HTML content", $"Viewer file does not seem to have enough content. htmlViewer: {htmlViewer}, stack-trace: {GetStackTrace()}");



                    var nodeContent = xmlPdfContent.SelectSingleNode("//content") ?? xmlPdfContent.SelectSingleNode("//div[@class='body-wrapper']");

                    // In some cases, we need to construct a content node our selves
                    // if (nodeContent == null)
                    // {
                    //     var nodelistArticles = xmlPdfContent.SelectNodes("//article");
                    //     if (nodelistArticles.Count > 0)
                    //     {
                    //         var xmlTemp = new XmlDocument();
                    //         nodeContent = xmlTemp.AppendChild(xmlTemp.CreateElement("content"));
                    //         foreach (XmlNode nodeArticle in nodelistArticles){

                    //             var nodeArticleImported = xmlTemp.ImportNode(nodeArticle, true);
                    //             xmlTemp.DocumentElement.AppendChild(nodeArticleImported);
                    //         }
                    //     }
                    //     else
                    //     {
                    //         return new TaxxorReturnMessage(false, "No articles found in the Paged Media HTML content");
                    //     }
                    // }

                    if (nodeContent != null)
                    {
                        // - Generate an access token to make sure we can access all the content
                        var tokenExpire = (siteType == "local" || siteType == "dev") ? 3600 : 3600;
                        var accessToken = AccessToken.GenerateToken(tokenExpire);

                        // - Append the access token to the image and svg assets that the content may contain
                        var nodeListVisuals = nodeContent.SelectNodes("//img[@src]");
                        foreach (XmlNode nodeVisual in nodeListVisuals)
                        {
                            nodeVisual.SetAttribute("src", $"{nodeVisual.GetAttribute("src")}?token={accessToken}");
                        }

                        // - Transport all the attributes that we have set on the body node to the body node of the viewer file so that we can still use it for CSS selectors
                        var nodeListBodyAttributes = xmlPdfContent.DocumentElement.SelectNodes("@*");
                        var bodyAttributes = new StringBuilder();
                        foreach (XmlAttribute node in nodeListBodyAttributes)
                        {
                            if (node.Name != "class") bodyAttributes.Append($" {node.Name}=\"{node.Value}\"");
                        }

                        // - Create the content by looping through the articles in the content node
                        var sbXhtmlContent = new StringBuilder();
                        var nodeListArticles = nodeContent.SelectNodes("article");
                        foreach (XmlNode nodeArticle in nodeListArticles)
                        {
                            sbXhtmlContent.AppendLine(nodeArticle.OuterXml);
                        }

                        // 
                        // => Replace the placeholders in the viewer file with the dynamically rendered content
                        // 
                        htmlViewer = htmlViewer.Replace("?token=none", $"?token={accessToken}");
                        htmlViewer = htmlViewer.Replace("<body", $"<body{bodyAttributes.ToString()}");
                        htmlViewer = htmlViewer.Replace("<!-- contentplaceholder -->", sbXhtmlContent.ToString());
                        htmlViewer = htmlViewer.Replace("<!-- headcontentplaceholderstart -->", $"<base href=\"{baseHrefUri}\" />");
                        var inlineCss = await RetrieveTextFile($"{GetServiceUrl(ConnectedServiceEnum.StaticAssets)}/static/stylesheets/statusindicators.css");
                        htmlViewer = htmlViewer.Replace("<!-- headcontentplaceholderend -->", $"<style id=\"stylesstatusindicators\" type=\"text/css\" nonce=\"{context.Items["nonce"]?.ToString() ?? ""}\">{inlineCss}</style>");

                        //
                        // => Convert the Paged Media Previewer into an XmlDocument
                        //
                        // var xmlPagedMediaViewer = new XmlDocument();
                        // xmlPagedMediaViewer.LoadXml(htmlViewer);
                        // Console.WriteLine(PrettyPrintXml(xmlPagedMediaViewer));

                        // 
                        // => Return the result
                        // 
                        if (returnXml)
                        {
                            // TODO: needs to be significantly improved!
                            var xmlViewer = new XmlDocument();
                            xmlViewer.LoadXml(htmlViewer);
                            return new TaxxorReturnMessage(true, "Successfully injected the XML into a paged media envelope", xmlViewer, "");
                        }
                        else
                        {
                            return new TaxxorReturnMessage(true, "Successfully injected the HTML into a paged media envelope", htmlViewer, "");
                        }

                    }
                    else
                    {
                        return new TaxxorReturnMessage(false, "There was an error constructing the Paged Media HTML content", $"Unable to locate content node. xmlPdfContent: {TruncateString(xmlPdfContent.OuterXml, 512)}, stack-trace: {GetStackTrace()}");
                    }

                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "There was an error constructing the Paged Media HTML content", ex.ToString());
                }

            }

        }

    }
}


/// <summary>
/// Class to be used for defining the properties of a PDF file that we need to generate with the Taxxor PDF Service
/// </summary>
public class PdfProperties
{

    /*
	HTTP POST Parameters supported

	NICE TO SUPPLY:
	sections -> {'all' or pipe delimited string of section id's} - tells the PDF Generator which sections to include [defaults to 'all']
	generatefile -> {true|false} - tell the PDF generator to only check the cache or to generate the PDF as well
	filename -> {string} - relative or absolute path for the PDF file, also single or double relative path '../' or '../../' are supported -> if not supplied then the NodeJS logic will auto-generate a filename
	ecomode -> {regular|compact|mixed} - optional settings to save paper and ink [defaults to 'regular']
	environment -> {tangelo|printmanager|printpage|contentsonly} - sets for example if the printer is launched [defaults to 'printmanager']
	pageurl -> {string} - show the webpage URL in footer when 'environment=printpage' [defaults to '']
	printmode -> {true|false} - adds printer popup to generated PDF file [defaults to false] 
  

	OPTIONAL:
	xmlsource -> {uri} fully qualified path (disk or URL) to source XML file [defaults to full XML file for current language]
	xslpdfprepare -> {uri} fully qualified path (disk or URL) to the XSLT file that prepares the source XML file before it's being processed [defaults to app config setting]
	xslpdfrender -> {uri} fully qualified path (disk or URL) to XSL:FO stylesheet [defaults to app config setting]
	translationfile -> {uri} fully qualified path (disk or URL) to L10n translation fragments file [defaults to app config setting]
	assetdir -> {uri} fully qualified path (disk or URL) to the PDF Generator assets folder (files like PDF Cover, etc.) [defaults to app config setting]
	imagedir -> {uri} fully qualified path (disk or URL) to the images folder [defaults to app config setting]

	grid -> {true|false} - shows debugging grid in PDF [defaults to false] 
	trackchanges -> {true|false} - shows change tracking in the PDF [defaults to false] 
	texthighlight -> {true|false} - shows highlights for text in the PDF [defaults to false] 
	watermark -> {true|false} - displays the "secret" watermark [defaults to false] 
	visualassistance -> {true|false} - adds additional debugging lines in PDF [defaults to false] 
	hidegraphs -> {true|false} - hides the graphs in the PDF [defaults to false] 
	pagemode -> {double|single} - suited for single or double sided printing [defaults to 'double']
	targetaudience -> {full|customized|employee|sustainable|executive|analyst} - used for the special PDF files [defaults to 'full']
	reporttype -> {AR|20-F} - determines which sections may be included in the PDF [defaults to 'AR'] 
  
	cache -> {true|false} - use the cache system [defaults to true]
	async -> {true|false} - wait for the PDF Generator to finish or not [defaults to false] 
	deliverymethod -> {attachment|base64|filesystem} - attachment returns the PDF file to the server, filesystem stores the PDF on the server where it's generated [defaults to 'attachment'] 
	sign -> {string} - signing profile used to sign (secure) the PDF file [defaults to app config setting]
  
	soappath -> {uri} OS path to save the SOAP messages to (useful for debugging) [defaults to null] 
  
	DEVELOPMENT ONLY
	pdfrenderserver -> {uri} Overwrite the standard defined PDF Generator URI with a specific one [defaults to uri defined in the configuration] 

	DEPRECATED:
	pdf_hash -> {string} - hard code the hash to use in the cache system [defaults to null]
	email -> {string} - email address to send the PDF file to when it's generated [defaults to null] 
	*/

    public string Sections = null;
    public string FileName = null;
    public bool TrackChanges = false;
    public bool Comments = false;
    //public bool KeepAlive = true;
    public bool Watermark = false;
    public bool EcoVersion = false;

    /// <summary>
    /// Set to true if the PDF needs to be secured using a random password
    /// </summary>
    public bool Secure = false;

    /// <summary>
    /// Set to true if you want to render a PDF ready for printing (include crop marks, etc)
    /// </summary>
    public bool PrintReady = false;

    // /// <summary>
    // /// Set to true if you want to render a PDF in landscape format
    // /// </summary>
    // public bool Landscape = false;

    /// <summary>
    /// Defines possible layout variations of the PDF
    /// 'regular' and 'landscape' are supported
    /// </summary>
    public string Layout = "regular";

    /// <summary>
    /// Defines post-processing methods that need to be applied to the PDF after it has been rendered
    /// </summary>
    public string PostProcess = "none";

    // Extra properties used for diff mode

    /// <summary>
    /// Supported values: "normal", "diff", "preview", "nochrome"
    /// </summary>
    public string Mode = "normal";
    public string Base = null;
    public string Latest = null;

    // Extra property to send HTML string
    public string Html = null;

    /// <summary>
    /// Defines how many sections need to be included in the PDF. Possible values
    /// - single-section -> PDF should only contain the sections as defined in the Sections field
    /// - include-children -> PDF should contain the sections as defined in the Sections field and all child sections
    /// </summary>
    public string RenderScope = "single-section";

    /// <summary>
    /// Defines if we need to render fake ("Lorem Ipsum") content for the PDF
    /// </summary>
    public bool UseLoremIpsum = false;

    /// <summary>
    /// Show signature marks or not
    /// </summary>
    public bool SignatureMarks = true;

    /// <summary>
    /// Set to true if the PDF should only include tables
    /// </summary>
    public bool TablesOnly = false;

    /// <summary>
    /// Set to true if you need to hide sync errors in the PDF output
    /// </summary>
    public bool HideErrors = false;

    /// <summary>
    /// Process content status in the PDF
    /// </summary>
    public bool UseContentStatus = false;

    /// <summary>
    /// Indicates if datapoints in the current reporting period should be hidden
    /// </summary>
    public bool HideCurrentPeriodDatapoints = false;

    /// <summary>
    /// Render links pointing to external sites or not
    /// </summary>
    public bool DisableExternalLinks = false;


    /// <summary>
    /// Set of key-value pairs that we can send to the PDF Generator Service
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <typeparam name="string"></typeparam>
    /// <returns></returns>
    public Dictionary<string, string> PdfGeneratorStringSet = new Dictionary<string, string>();

    public PdfProperties()
    {
        //
        // TODO: Add constructor logic here
        //
    }

    /// <summary>
    /// Dumps the PdfProperties contents to a string for debugging purposes
    /// </summary>
    /// <returns></returns>
    public string DumpToString()
    {
        var debugContent = new List<string>
        {
            $"Sections: {this.Sections}",
            $"FileName: {this.FileName}",
            $"TrackChanges: {this.TrackChanges}",
            $"Comments: {this.Comments}",
            $"Watermark: {this.Watermark}",
            $"EcoVersion: {this.EcoVersion}",
            $"Secure: {this.Secure}",
            $"PrintReady: {this.PrintReady}",
            $"Layout: {this.Layout}",
            $"PostProcess: {this.PostProcess}",
            $"Mode: {this.Mode}",
            $"Latest: {TruncateString(this.Latest, 100)}",
            $"Base: {TruncateString(this.Base, 100)}",
            $"Html: {TruncateString(this.Html, 100)}",
            $"RenderScope: {this.RenderScope}",
            $"UseLoremIpsum: {this.UseLoremIpsum}",
            $"SignatureMarks: {this.SignatureMarks}",
            $"TablesOnly: {this.TablesOnly}",
            $"DisableExternalLinks: {this.DisableExternalLinks}",
            $"HideErrors: {this.HideErrors}",
            $"UseContentStatus: {this.UseContentStatus}",
            $"HideCurrentPeriodDatapoints: {this.HideCurrentPeriodDatapoints}"
        };
        return string.Join(", ", debugContent.ToArray());
    }

}