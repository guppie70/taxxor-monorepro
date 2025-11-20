using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace Taxxor
{

    public partial class ConnectedServices
    {

        /// <summary>
        /// Utility class to interact with the Taxxor Pandoc Service
        /// </summary>
        public static class ConversionService
        {

            /// <summary>
            /// Renders Excel files from a directory containing JSON data files
            /// </summary>
            /// <param name="sourceSharedFolderPath"></param>
            /// <param name="targetSharedFolderPath"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RenderExcelFromDirectory(string sourceSharedFolderPath, string targetSharedFolderPath, bool forceNumberConversion = false, bool cleanOutputDir = false)
            {
                return await RenderExcelFromDirectory(sourceSharedFolderPath, targetSharedFolderPath, null, forceNumberConversion, cleanOutputDir);
            }

            /// <summary>
            /// Renders Excel files from a directory containing JSON data files
            /// </summary>
            /// <param name="sourceSharedFolderPath"></param>
            /// <param name="targetSharedFolderPath"></param>
            /// <param name="excelFileName"></param>
            /// <param name="cleanOutputDir"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RenderExcelFromDirectory(string sourceSharedFolderPath, string targetSharedFolderPath, string excelFileName, bool forceNumberConversion = false, bool cleanOutputDir = false)
            {
                appLogger.LogInformation($"- sourceSharedFolderPath: {sourceSharedFolderPath}");
                appLogger.LogInformation($"- targetSharedFolderPath: {targetSharedFolderPath}");

                // Parameters to POST
                var dataToPost = new Dictionary<string, string>
                {
                    { "sourcedir", sourceSharedFolderPath },
                    { "outputdir", targetSharedFolderPath },
                    { "cleanoutputdir", ((cleanOutputDir) ? "true" : "false") },
                    { "forcenumberconversion", ((forceNumberConversion) ? "true" : "false") }
                };

                if (!string.IsNullOrEmpty(excelFileName))
                {
                    // All excel sheets need to be combined in one file
                    dataToPost.Add("excelfilename", excelFileName);
                }

                /*
                Render the MS Excel files and grab the result in an XML envelope
                */
                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.ConversionService, RequestMethodEnum.Post, "renderexcelfromdirectory", dataToPost, true);
            }

            /// <summary>
            /// Renders binary visuals of the graphs from a utility HTML page
            /// </summary>
            /// <param name="utilityFilePathUri"></param>
            /// <param name="sharedOutputFolderPath"></param>
            /// <param name="cleanOutputDir"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RenderGraphBinariesFromUtilityPage(string utilityFilePathUri, string sharedOutputFolderPath, int graphMagnificationFactor, bool cleanOutputDir = false, bool removePngAfterConversion = true, bool renderJpegVersions = false, bool renderSvgVersions = false)
            {
                // appLogger.LogInformation($"- utilityFilePathUri: {utilityFilePathUri}");
                // appLogger.LogInformation($"- sharedOutputFolderPath: {sharedOutputFolderPath}");

                // Parameters to POST
                var dataToPost = new Dictionary<string, string>
                {
                    { "utilityuri", utilityFilePathUri },
                    { "outputdir", sharedOutputFolderPath },
                    { "graphmagnification", graphMagnificationFactor.ToString() },
                    { "cleanoutputdir", cleanOutputDir.ToString().ToLower() },
                    { "removepng", removePngAfterConversion.ToString().ToLower() },
                    { "renderjpg", renderJpegVersions.ToString().ToLower() },
                    { "rendersvg", renderSvgVersions.ToString().ToLower() }
                };

                /*
                Render the binary files files and grab the result in an XML envelope
                */
                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.ConversionService, RequestMethodEnum.Post, "generategraphbinariesfromutilitypage", dataToPost, true);
            }

            /// <summary>
            /// Genarates (image) binaries from SVG drawings using a special utility page
            /// </summary>
            /// <param name="utilityFilePathUri"></param>
            /// <param name="sharedOutputFolderPath"></param>
            /// <param name="graphMagnificationFactor"></param>
            /// <param name="cleanOutputDir"></param>
            /// <param name="removePngAfterConversion"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RenderDrawingBinariesFromUtilityPage(string utilityFilePathUri, string sharedOutputFolderPath, int graphMagnificationFactor, bool cleanOutputDir = false, bool removePngAfterConversion = true)
            {
                // Parameters to POST
                var dataToPost = new Dictionary<string, string>
                {
                    { "utilityuri", utilityFilePathUri },
                    { "outputdir", sharedOutputFolderPath },
                    { "graphmagnification", graphMagnificationFactor.ToString() },
                    { "cleanoutputdir", ((cleanOutputDir) ? "true" : "false") },
                    { "removepng", ((removePngAfterConversion) ? "true" : "false") }
                };

                /*
                Render the binary files files and grab the result in an XML envelope
                */
                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.ConversionService, RequestMethodEnum.Post, "generatedrawingbinariesfromutilitypage", dataToPost, true);
            }

            /// <summary>
            /// Converts the linked CSS styles in an XHTML document to inline CSS @style rukes
            /// </summary>
            /// <param name="xmlDoc"></param>
            /// <param name="outputFilePathOs"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> ConvertToInlineCss(XmlDocument xmlDoc, string baseUri = null, string outputFilePathOs = null)
            {

                // Parameters to POST
                var dataToPost = new Dictionary<string, string>
                {
                    { "html", xmlDoc.OuterXml }
                };
                if (outputFilePathOs != null)
                {
                    dataToPost.Add("outputfilepathos", outputFilePathOs);
                }
                if (baseUri != null)
                {
                    dataToPost.Add("baseuri", baseUri);
                }

                /*
                Convert the XML and retrieve the result in the payload node
                */
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.ConversionService, RequestMethodEnum.Post, "converttoinlinecss", dataToPost, true);

                if (XmlContainsError(xmlResponse))
                {
                    return xmlResponse;
                }
                else
                {
                    if (outputFilePathOs != null)
                    {
                        // Return the response as it does not contain the converted data
                        return xmlResponse;
                    }
                    else
                    {
                        var nodePayload = xmlResponse.SelectSingleNode("/root/payload");
                        if (nodePayload == null)
                        {
                            return GenerateErrorXml("Could not collect data", "payload node could not be found in the response");
                        }
                        else
                        {
                            var xml = nodePayload.InnerText;
                            var xmlToReturn = new XmlDocument();
                            try
                            {
                                xmlToReturn.LoadXml(xml);
                                return xmlToReturn;
                            }
                            catch (Exception ex)
                            {
                                // Store the received XML on the disk so that we can inspect it
                                await TextFileCreateAsync(xml, $"{logRootPathOs}/_inline-css.xml");

                                // Return an error xml
                                return GenerateErrorXml("Could not parse the data that was returned", $"error: {ex}");
                            }
                        }
                    }
                }
            }



            /// <summary>
            /// Renders an Excel file from the tables used in the section/document
            /// </summary>
            /// <param name="msExcelProperties"></param>
            /// <param name="projectVariablesForExcelContent"></param>
            /// <param name="msExcelTargetPathOs"></param>
            /// <param name="debugLogic"></param>
            /// <returns></returns>
            public static async Task<TaxxorReturnMessage> RenderMsExcel(MsOfficeFileProperties msExcelProperties, ProjectVariables projectVariablesForExcelContent, string msExcelTargetPathOs, bool debugLogic = false)
            {

                var errorMessage = "";
                var errorDetails = "";
                string outputChannelName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVariablesForExcelContent.editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(projectVariablesForExcelContent.outputChannelVariantId)}]/name")?.InnerText ?? "unknown";

                //
                // => Location on the shared folder where we are working with the Excel data
                //
                var basePath = $"/temp/excel-{projectVariablesForExcelContent.projectId}";
                var sharedFoldePathForDocker = RetrieveSharedFolderPathOs(true);
                var sharedFolderWebsiteWorkingPathForDocker = $"{sharedFoldePathForDocker}{basePath}";
                var sharedFolderForApp = RetrieveSharedFolderPathOs();
                var sharedFolderWebsiteWorkingPathForApp = $"{sharedFolderForApp}{basePath}";
                var sourceFolderPathForDocker = $"{sharedFolderWebsiteWorkingPathForDocker}/data";
                var targetFolderPathForDocker = $"{sharedFolderWebsiteWorkingPathForDocker}/excel";

                //
                // => Paths to use
                //
                var excelFileName = $"{projectVariablesForExcelContent.projectId}-{projectVariablesForExcelContent.outputChannelVariantId}.xlsx";
                var msExcelSourceFilePathOs = $"{sharedFolderWebsiteWorkingPathForApp}/excel/{excelFileName}";

                try
                {


                    //
                    // => Setup and cleanup directory structure
                    //
                    if (!Directory.Exists($"{sharedFolderWebsiteWorkingPathForApp}/data")) Directory.CreateDirectory($"{sharedFolderWebsiteWorkingPathForApp}/data");
                    if (!Directory.Exists($"{sharedFolderWebsiteWorkingPathForApp}/excel")) Directory.CreateDirectory($"{sharedFolderWebsiteWorkingPathForApp}/excel");
                    DelTree($"{sharedFolderWebsiteWorkingPathForApp}/data", false);
                    DelTree($"{sharedFolderWebsiteWorkingPathForApp}/excel", false);


                    //
                    // => Retrieve the source data
                    //
                    var xmlReportContent = await DocumentStoreService.PdfData.Load(projectVariablesForExcelContent, msExcelProperties.Sections, msExcelProperties.UseContentStatus, msExcelProperties.RenderScope, msExcelProperties.HideCurrentPeriodDatapoints, debugLogic);
                    if (XmlContainsError(xmlReportContent))
                    {
                        appLogger.LogError($"{xmlReportContent.SelectSingleNode("//message")?.InnerText ?? ""}, debuginfo: {xmlReportContent.SelectSingleNode("//debuginfo")?.InnerText ?? ""}");

                        errorMessage = $"Unable to retrieve source data for the {outputChannelName} Excel file";
                        errorDetails = xmlReportContent.SelectSingleNode("//debuginfo")?.InnerText ?? "";
                        return new TaxxorReturnMessage(false, errorMessage, errorDetails);
                    }
                    if (debugLogic)
                    {
                        await xmlReportContent.SaveAsync($"{logRootPathOs}/-excel.1.xhtml.xml", true, true);
                    }

                    var xmlOutputChannelContent = new XmlDocument();
                    xmlOutputChannelContent.ReplaceContent(xmlReportContent);


                    //
                    // => Find the tables in the document
                    //
                    var tablesXPath = (msExcelProperties.RenderHiddenElements) ? "//div[contains(@class, 'table-wrapper')]" : "//div[contains(@class, 'table-wrapper') and not(contains(@class, 'hide'))]";
                    var nodeListTables = xmlOutputChannelContent.SelectNodesAgnostic(tablesXPath);
                    if (nodeListTables.Count == 0)
                    {
                        //
                        // => No tables found so we return a 200 OK response, but with an error object in it
                        //
                        var warningMessage = $"WARNING: Couldn't find any tables in the {outputChannelName} content to convert to Excel format.";
                        appLogger.LogWarning($"{warningMessage}");
                        return new TaxxorReturnMessage(false, warningMessage, $"nodeListTables.Count: {nodeListTables.Count}");
                    }

                    // Loop through the tables and generate an Excel workbook containing one sheet per table
                    foreach (XmlNode nodeTable in nodeListTables)
                    {
                        var xmlHtmlTable = new XmlDocument();
                        try
                        {
                            xmlHtmlTable.LoadXml(nodeTable.OuterXml);

                            // Strip namespaces so that the result is "clean" and easy to transform/query
                            xmlHtmlTable = StripNameSpaces(xmlHtmlTable);

                            var tableIsHidden = nodeTable.GetAttribute("class")?.Contains("hide") ?? false;

                            // Generate a filename for the Excel file that we want to save
                            var tableId = RetrieveAttributeValueIfExists("/div//table/@id", xmlHtmlTable);
                            if (string.IsNullOrEmpty(tableId))
                            {
                                string articleIdentifier = nodeTable.SelectSingleNode("ancestor::article")?.GetAttribute("data-ref") ?? nodeTable.SelectSingleNode("ancestor::article")?.GetAttribute("id") ?? "unknown";
                                appLogger.LogError($"Could not find a table ID so cannot save Excel table. article-reference: {articleIdentifier}, tabledata: {TruncateString(xmlHtmlTable.OuterXml, 500)}, stack-trace: {GetStackTrace()}");
                            }
                            else
                            {
                                var baseFileName = tableId;
                                var suffix = 0;
                                while (File.Exists($"{sharedFolderWebsiteWorkingPathForApp}/data/{baseFileName}.json"))
                                {
                                    suffix++;
                                    baseFileName = $"{tableId}-{suffix}";
                                }

                                await xmlHtmlTable.SaveAsync($"{sharedFolderWebsiteWorkingPathForApp}/data/{baseFileName}-source.xml", true, true);

                                // Convert the XML to a plain table format in XHTML
                                var xsltArgumentList = new XsltArgumentList();
                                xsltArgumentList.AddParam("taxxorClientId", "", TaxxorClientId);
                                var xhtmlTable = TransformXmlToDocument(xmlHtmlTable, $"cms_export-excel-tables", xsltArgumentList);

                                // In case we should not render hidden elements, then we need to strip them from the table
                                if (!msExcelProperties.RenderHiddenElements)
                                {
                                    var nodeListHiddenCells = xhtmlTable.SelectNodes("/table/*/tr/*[(local-name()='td' or local-name()='th') and @hide='true']");
                                    if (nodeListHiddenCells.Count > 0) RemoveXmlNodes(nodeListHiddenCells);
                                    var nodeListEmptyRows = xhtmlTable.SelectNodes("/table/*/tr[count(*) = 0]");
                                    if (nodeListEmptyRows.Count > 0) RemoveXmlNodes(nodeListEmptyRows);
                                }
                                else if (tableIsHidden)
                                {
                                    // Mark all cells in the table as hidden
                                    var nodeListCells = xhtmlTable.SelectNodes("/table/*/tr/*[(local-name()='td' or local-name()='th')]");
                                    foreach (XmlNode nodeCell in nodeListCells)
                                    {
                                        nodeCell.SetAttribute("hide", "true");
                                    }
                                }

                                // Additional check: only save the table if it contains at least some content
                                var nodeListTableCells = xhtmlTable.SelectNodes("/table/*/tr/*[(local-name()='td' or local-name()='th')]");
                                if (nodeListTableCells.Count > 0)
                                {
                                    await xhtmlTable.SaveAsync($"{sharedFolderWebsiteWorkingPathForApp}/data/{baseFileName}.xml", true, true);

                                    // Store the data for rendering the Excel files on the shared folder
                                    await TextFileCreateAsync(ConvertToJson(xhtmlTable, Newtonsoft.Json.Formatting.Indented), $"{sharedFolderWebsiteWorkingPathForApp}/data/{baseFileName}.json");
                                }
                                else
                                {
                                    appLogger.LogWarning($"Table (ID: {tableId}) is empty - skipping it for export to Excel.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Could not load XHTML content of table for {outputChannelName}");
                        }
                    }

                    //
                    // => Render Excel file
                    //
                    await _outputGenerationProgressMessage("Rendering Excel document");
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var msExcelRenderResponse = await Taxxor.ConnectedServices.ConversionService.RenderExcelFromDirectory(sourceFolderPathForDocker, targetFolderPathForDocker, excelFileName, true, true);
                    watch.Stop();
                    var elapsedMs = watch.ElapsedMilliseconds;
                    if (siteType == "local") appLogger.LogInformation($"Rendering of Excel document retreival took: {elapsedMs.ToString()} ms");

                    if (XmlContainsError(msExcelRenderResponse))
                    {
                        errorMessage = $"Error rendering the {outputChannelName} Excel file";
                        errorDetails = $"recieved: {msExcelRenderResponse.OuterXml}, stack-trace: {GetStackTrace()}";
                        return new TaxxorReturnMessage(false, errorMessage, errorDetails);
                    }

                    //
                    // => Copy the generated Excel file to the data directory of the Editor and wrap things up
                    //
                    if (!File.Exists(msExcelSourceFilePathOs))
                    {
                        errorMessage = $"Could not locate the {outputChannelName} Excel file";
                        errorDetails = $"msExcelFilePathOs: {msExcelSourceFilePathOs}, stack-trace: {GetStackTrace()}";
                        return new TaxxorReturnMessage(false, errorMessage, errorDetails);
                    }
                    if (File.Exists(msExcelTargetPathOs)) File.Delete(msExcelTargetPathOs);
                    File.Copy(msExcelSourceFilePathOs, msExcelTargetPathOs);


                    // Return a success message
                    return new TaxxorReturnMessage(true, $"Successfully generated {outputChannelName} MS Excel file", msExcelTargetPathOs, "");
                }
                catch (Exception ex)
                {
                    // Return an error message
                    errorMessage = $"There was an error generating the {outputChannelName} Excel file";
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, projectVariablesForExcelContent.DumpToString());
                }

            }




            /// <summary>
            /// Renders an MS Word file
            /// </summary>
            /// <param name="msWordProperties"></param>
            /// <param name="projectVariablesForMsWordGeneration"></param>
            /// <param name="msWordPath"></param>
            /// <param name="debugLogic"></param>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public static async Task<T> RenderMsWord<T>(MsOfficeFileProperties msWordProperties, ProjectVariables projectVariablesForMsWordGeneration, string msWordPath = null, bool debugLogic = false)
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                // Retrieve the type of data that we need to return
                var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                // Global variables
                var routineDebugInfo = "";
                string errorMessage = "";
                string debugInfo = "";

                // Console.WriteLine("*****************************************");
                // Console.WriteLine(projectVariablesForMsWordGeneration.DumpToString(false));
                // Console.WriteLine("*****************************************");

                try
                {
                    /*
                    Render the MS Word content and grab the result in an XML envelope
                    */
                    XmlDocument xmlRestResult = await _renderMsWord(msWordProperties, projectVariablesForMsWordGeneration, debugLogic);


                    if (debugLogic)
                    {
                        routineDebugInfo += "Raw REST result: " + Environment.NewLine + xmlRestResult.OuterXml + Environment.NewLine;
                    }

                    if (XmlContainsError(xmlRestResult))
                    {
                        // An error has occurred - now constuct a solid error message
                        errorMessage = "An error occurred while generating your MS Word file";
                        debugInfo = xmlRestResult.SelectSingleNode("/error/debuginfo").InnerText;

                        // Check if we can capture additional details from the Pandoc Service
                        XmlNode? nodeErrorInfoPandocService = xmlRestResult.SelectSingleNode("/error/httpresponse/root/error");
                        if (nodeErrorInfoPandocService != null)
                        {
                            try
                            {
                                errorMessage = nodeErrorInfoPandocService.SelectSingleNode("message").InnerText;
                                debugInfo = nodeErrorInfoPandocService.SelectSingleNode("debuginfo").InnerText;
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, "Something went wrong trying to retrieve additional error information");
                            }

                        }

                        // Add the status code to the debug information if available
                        var nodeStatusCode = xmlRestResult.SelectSingleNode("/error/httpstatuscode");
                        if (nodeStatusCode != null)
                        {
                            debugInfo = $"Taxxor Editor received HTTP Status Code {nodeStatusCode.InnerText}. Additional information: " + debugInfo;
                        }

                        // Add other possible debuginformation that we have gathered
                        if (routineDebugInfo != "")
                        {
                            debugInfo += Environment.NewLine + routineDebugInfo;
                        }

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
                        // Capture the base64 encoded result
                        var nodeMsWordContent = xmlRestResult.SelectSingleNode("/root/content");
                        if (nodeMsWordContent == null)
                        {
                            errorMessage = "Unable to locate MS Word content";
                            debugInfo = $"Response from the Pandoc Service did not contain a base64 encoded string representing the MS Word binary file. stack-trace: {GetStackTrace()}";

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
                        // Grab the base 64 encoded blob from the XmlDocument that we have received
                        var base64MsWordContent = nodeMsWordContent.InnerText;

                        // Return the content of the file that we have requested
                        switch (returnType)
                        {
                            case "xml":
                                // Store the generated file on the disk
                                var generatedMsWordFilePathOs = "";
                                if (msWordPath == null)
                                {
                                    var msWordFileName = RandomString(12, false) + ".docx";
                                    // - Attempt to capture the filename that the Pandoc Service has used for the temporary MS Word file
                                    XmlNode nodeFileName = xmlRestResult.SelectSingleNode("/root/filename");
                                    if (nodeFileName != null) msWordFileName = nodeFileName.InnerText;

                                    // Store the MS Word in a temporary location on this server
                                    generatedMsWordFilePathOs = dataRootPathOs + "/temp/" + msWordFileName;
                                }
                                else
                                {
                                    // Use the file path that was passed to the routine
                                    generatedMsWordFilePathOs = msWordPath;
                                }

                                // Test if the directory to store the MS Word file in exists
                                if (!Directory.Exists(Path.GetDirectoryName(generatedMsWordFilePathOs)))
                                {
                                    errorMessage = "Unable to locate folder to store MS Word file in";
                                    debugInfo = $"Folder: {Path.GetDirectoryName(generatedMsWordFilePathOs)} does not exist, stack-trace: {GetStackTrace()}";

                                    appLogger.LogError($"{errorMessage} - {debugInfo}");

                                    // Return an error XML Document
                                    return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));
                                }

                                try
                                {
                                    Byte[] bytes = Base64DecodeToBytes(base64MsWordContent);
                                    File.WriteAllBytes(generatedMsWordFilePathOs, bytes);
                                }
                                catch (Exception ex)
                                {
                                    errorMessage = "Unable to convert and store MS Word content";
                                    debugInfo = $"error: {ex}, stack-trace: {GetStackTrace()}";

                                    appLogger.LogError($"{errorMessage} - {debugInfo}");

                                    // Return an error XML Document
                                    return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));
                                }

                                // Complete the XML we are going to return
                                XmlDocument msWordResult = RetrieveTemplate("mswordgenerator_result");
                                msWordResult.SelectSingleNode("/result/mswordpath").InnerText = generatedMsWordFilePathOs;
                                msWordResult.SelectSingleNode("/result/debuginfo").InnerText = routineDebugInfo;

                                // Return the XmlDocument
                                return (T)Convert.ChangeType(msWordResult, typeof(T));

                            case "bytearray":
                                // Return the MS Word file as binary content that we can directly stream to the client
                                return (T)Convert.ChangeType(Base64DecodeToBytes(base64MsWordContent), typeof(T));

                            default:
                                // Return the Base64 encoded string
                                return (T)Convert.ChangeType(base64MsWordContent, typeof(T));
                        }

                    }
                }
                catch (Exception ex)
                {
                    errorMessage = "Unable to render MS Word file";
                    debugInfo = $"error: {ex}, stack-trace: {GetStackTrace()}";

                    appLogger.LogError(ex, errorMessage);

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
            }


            /// <summary>
            /// Sends the request to the Conversion Service to render a MS Word file and returns the result
            /// </summary>
            /// <param name="msWordProperties"></param>
            /// <param name="projectVariablesForMsWordGeneration"></param>
            /// <param name="debugLogic"></param>
            /// <returns></returns>
            private static async Task<XmlDocument> _renderMsWord(MsOfficeFileProperties msWordProperties, ProjectVariables projectVariablesForMsWordGeneration, bool debugLogic = false)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                var errors = new List<string>();
                var warnings = new List<string>();

                // Extend the project variables object
                projectVariablesForMsWordGeneration.projectRootPath = projectVars.projectRootPath;

                // Generate the object that contains the variables that we want to post
                var dataToPost = new Dictionary<string, string>
                {
                    { "serveasdownload", "false" },
                    { "type", "json" }
                };

                //
                // => Retrieve the HTML that we need for the Word file
                //
                var msWordHtml = await RetrieveLatestPdfHtml(projectVariablesForMsWordGeneration, msWordProperties.Sections, msWordProperties.UseContentStatus, msWordProperties.HideCurrentPeriodDatapoints, msWordProperties.RenderScope);
                await _outputGenerationProgressMessage("Successfully retrieved content for MS Word generation");

                //
                // => Optionally convert the HTML content for the PDF to Lorem Ipsum
                //
                if (msWordProperties.UseLoremIpsum)
                {
                    var xmlWordHtml = new XmlDocument();
                    xmlWordHtml.LoadXml(msWordHtml);

                    var xmlPdfHtmlLoremIpsum = await ConvertToLoremIpsum(xmlWordHtml, projectVariablesForMsWordGeneration.outputChannelVariantLanguage);

                    // Message to the client
                    await _outputGenerationProgressMessage("Converted content to fake (lorem ipsum) text");

                    // Remove the XML declaration
                    if (xmlPdfHtmlLoremIpsum.FirstChild.NodeType == XmlNodeType.XmlDeclaration) xmlPdfHtmlLoremIpsum.RemoveChild(xmlPdfHtmlLoremIpsum.FirstChild);

                    msWordHtml = xmlPdfHtmlLoremIpsum.OuterXml;
                }

                // TODO: This should probably move to client specific code
                var outputChannelVariant = (projectVariablesForMsWordGeneration.outputChannelVariantId.Contains("20f")) ? "20F" : "AR";

                //
                // => Post process the HTML file
                //
                try
                {
                    var xmlMsWord = new XmlDocument();
                    xmlMsWord.LoadXml(msWordHtml);


                    //
                    // => Deal with hidden table elements
                    //
                    var hiddenElementColor = "#D1D1D1";
                    var tablesXPath = "//div[contains(@class, 'table-wrapper')]";
                    var nodeListTableWrappers = xmlMsWord.SelectNodesAgnostic(tablesXPath);

                    // Loop through the tables and generate an Excel workbook containing one sheet per table
                    foreach (XmlNode nodeTableWrapper in nodeListTableWrappers)
                    {
                        var tableIsHidden = nodeTableWrapper.GetAttribute("class")?.Contains("hide") ?? false;
                        if (!msWordProperties.RenderHiddenElements)
                        {
                            // Strip all the hidden table elements from the XML
                            if (tableIsHidden)
                            {
                                nodeTableWrapper.InnerXml = "";
                            }
                            else
                            {
                                var nodeListHiddenCells = nodeTableWrapper.SelectNodes("table/*/tr/*[(local-name()='td' or local-name()='th' or local-name()='tr') and contains(@class, 'hide')]");
                                if (nodeListHiddenCells.Count > 0) RemoveXmlNodes(nodeListHiddenCells);
                                var nodeListEmptyRows = nodeTableWrapper.SelectNodes("/table/*/tr[count(*) = 0]");
                                if (nodeListEmptyRows.Count > 0) RemoveXmlNodes(nodeListEmptyRows);
                            }
                        }
                        else if (tableIsHidden)
                        {
                            // Mark all cells in the table as hidden
                            var nodeListCells = nodeTableWrapper.SelectNodes("table/*/tr/*[(local-name()='td' or local-name()='th')]");
                            foreach (XmlNode nodeCell in nodeListCells)
                            {
                                nodeCell.SetAttribute("style", $"background-color: {hiddenElementColor};");
                            }
                        }
                        else
                        {
                            // Mark one or more cells are hidden
                            var nodeListHiddenCells = nodeTableWrapper.SelectNodes("table/*/tr/*[(local-name()='td' or local-name()='th') and contains(@class, 'hide')]");
                            foreach (XmlNode nodeHiddenCell in nodeListHiddenCells)
                            {
                                nodeHiddenCell.SetAttribute("style", $"background-color: {hiddenElementColor};");
                            }
                            var nodeHiddenRow = nodeTableWrapper.SelectSingleNode("table/*/tr[contains(@class, 'hide')]");
                            if (nodeHiddenRow != null)
                            {
                                nodeHiddenRow.SetAttribute("class", nodeHiddenRow.GetAttribute("class").Replace("hide", ""));
                                nodeListHiddenCells = nodeHiddenRow.SelectNodes("*[(local-name()='td' or local-name()='th')]");
                                foreach (XmlNode nodeHiddenCell in nodeListHiddenCells)
                                {
                                    nodeHiddenCell.SetAttribute("style", $"background-color: {hiddenElementColor};");
                                }
                            }
                        }
                    }

                    // Combine the in-text footnotes in a summary wrapper div at the bottom of each article
                    CreateInTextFootnoteSummary(ref xmlMsWord, ref warnings, ref errors);

                    // Log warnings and errors
                    foreach (var warning in warnings)
                    {
                        await _outputGenerationProgressMessage($"WARNING: {warning}");
                    }
                    warnings.Clear();
                    foreach (var error in errors)
                    {
                        await _outputGenerationProgressMessage($"ERROR: {error}");
                    }
                    errors.Clear();

                    // Store the document that we send to the service on the disk so that we can inspect it
                    if (debugRoutine) await xmlMsWord.SaveAsync($"{logRootPathOs}/_1-msword.xml", false);

                    // Run through simplification stylesheet
                    var inlineCssFetchResult = await FetchStaticAsset("/stylesheets/msword.css");
                    var inlineCss = "";
                    if (inlineCssFetchResult.Success)
                    {
                        inlineCss = inlineCssFetchResult.Payload;
                    }
                    else
                    {
                        appLogger.LogError($"Could not retrieve MS Word CSS file. details: {inlineCssFetchResult.ToString()}");
                    }

                    XsltArgumentList xsltArgumentList = new XsltArgumentList();
                    xsltArgumentList.AddParam("inline-css", "", inlineCss);
                    xsltArgumentList.AddParam("output-channel-variant", "", outputChannelVariant);
                    xsltArgumentList.AddParam("post-processing-format", "", "docx");
                    xmlMsWord = TransformXmlToDocument(xmlMsWord, "xsl_simplify-source-data", xsltArgumentList);


                    // Store the document that we send to the service on the disk so that we can inspect it
                    if (debugRoutine) await xmlMsWord.SaveAsync($"{logRootPathOs}/_2-msword.simplified.xml", false);

                    //
                    // => Insert the TOC and the chapter numbers in the XHTML content
                    //

                    // Create a bogus PdfProperties object so that we can generate a TOC
                    var bogusPdfProperties = new PdfProperties();
                    bogusPdfProperties.RenderScope = msWordProperties.RenderScope;
                    bogusPdfProperties.Sections = msWordProperties.Sections;

                    // Insert the TOC and section numbers
                    InsertTableOfContentsAndChapterNumbers(ref xmlMsWord, projectVariablesForMsWordGeneration, "WORD", bogusPdfProperties);

                    // Inject an additional space between the section number and the section text (for some obscure reason this cannot be set in css)
                    var sectionNumberingXpath = Extensions.GenerateArticleXpathForAddingSectionNumbering();
                    var nodeListArticles = xmlMsWord.SelectNodes(sectionNumberingXpath);
                    foreach (XmlNode nodeArticle in nodeListArticles)
                    {
                        // Find the highest header in the article and insert the section number there
                        var nodeListHeaders = nodeArticle.SelectNodes($".//*[local-name()='h1' or local-name()='h2']");
                        if (nodeListHeaders.Count > 0)
                        {
                            var nodeHeader = nodeListHeaders.Item(0);
                            var nodeSpanNumber = nodeHeader.SelectSingleNode("span");
                            if (nodeSpanNumber != null)
                            {
                                nodeSpanNumber.InnerText = $"{nodeSpanNumber.InnerText}  ";
                            }
                        }
                    }


                    // Store the document that we send to the service on the disk so that we can inspect it
                    if (debugRoutine) await xmlMsWord.SaveAsync($"{logRootPathOs}/_3-msword.tocincluded.xml", false);

                    //
                    // => Inject images for SVG's
                    //
                    var reUri = new Regex(@"^(.*dataserviceassets/.*?/images)(.*?)(\.svg)$");
                    var nodeListSvgSources = xmlMsWord.SelectNodes($"//article//{RetrieveSvgElementsBaseXpath()}");
                    foreach (XmlNode nodeSvgObject in nodeListSvgSources)
                    {
                        var svgUri = nodeSvgObject.GetAttribute("src") ?? nodeSvgObject.GetAttribute("data");
                        if (svgUri.Contains("?")) svgUri = svgUri.SubstringBefore("?");
                        // appLogger.LogInformation($"-svgUri: {svgUri}");
                        var imageRenditionUri = RegExpReplace(reUri, svgUri, $"$1/{ImageRenditionsFolderName}/drawings$2.png");
                        // appLogger.LogInformation($"-imageRenditionUri: {imageRenditionUri}");
                        var nodeImg = xmlMsWord.CreateElement("img");
                        nodeImg.SetAttribute("src", imageRenditionUri);
                        nodeSvgObject.ParentNode.AppendChild(nodeImg);
                    }
                    if (nodeListSvgSources.Count > 0) RemoveXmlNodes(nodeListSvgSources);

                    // Store the document that we send to the service on the disk so that we can inspect it
                    if (debugRoutine) await xmlMsWord.SaveAsync($"{logRootPathOs}/_4-msword.visuals.xml", false);



                    //
                    // => Generate Graphs
                    //
                    var xPathGraphData = "//article//section//div[div/@class='chart-content']/table";
                    var nodeListGraphSourceTables = xmlMsWord.SelectNodes(xPathGraphData);

                    if (nodeListGraphSourceTables.Count > 0)
                    {
                        // - Update the graph library to make sure that we include all the graphs
                        var dataReferences = new List<string>();
                        foreach (XmlNode nodeTable in nodeListGraphSourceTables)
                        {
                            var nodeArticle = nodeTable.SelectSingleNode("ancestor::article");
                            if (nodeArticle != null)
                            {
                                var dataReference = nodeArticle.GetAttribute("data-ref");
                                // Console.WriteLine($"- dataReference: {dataReference}");
                                if (!string.IsNullOrEmpty(dataReference) && !dataReferences.Contains(dataReference)) dataReferences.Add(dataReference);
                            }
                        }
                        // Console.WriteLine("+++++++++++++++");
                        // Console.WriteLine(string.Join(",", dataReferences));
                        // Console.WriteLine("+++++++++++++++");

                        var graphConversionLogResult = await GenerateGraphRenditions(reqVars, projectVariablesForMsWordGeneration, null, dataReferences);
                        if (graphConversionLogResult.Success)
                        {
                            await _outputGenerationProgressMessage(graphConversionLogResult.Message);
                        }
                        else
                        {
                            var message = "There was an error updating the graph renditions";
                            appLogger.LogError($"{message}. details: {graphConversionLogResult.LogToString()}");
                            await _outputGenerationProgressMessage($"ERROR: {message}");
                        }

                        // - Insert image nodes with a reference to the pre-rendered graph rendition
                        var chartCounter = 0;
                        var graphWrapperIds = new List<string>();
                        foreach (XmlNode nodeTable in nodeListGraphSourceTables)
                        {
                            var nodeArticle = nodeTable.SelectSingleNode("ancestor::article");
                            var contentLang = nodeArticle.GetAttribute("lang") ?? "undefined";
                            var nodeWrapper = nodeTable.ParentNode;

                            var dataReference = nodeArticle.GetAttribute("data-ref");
                            var randomId = RandomString(10);
                            var wrapperId = nodeWrapper.GetAttribute("id");

                            // - Make sure that no double wrapper ID's are present
                            if (graphWrapperIds.Contains($"{wrapperId}-{contentLang}"))
                            {
                                var warningMessage = $"Found graph in {dataReference} with a data table using ID {wrapperId} that is not unique.";
                                await _outputGenerationProgressMessage($"WARNING: {warningMessage}");
                                appLogger.LogWarning(warningMessage);
                            }
                            else
                            {
                                graphWrapperIds.Add($"{wrapperId}-{contentLang}");
                            }

                            // - Make sure an ID is present on the wrapper div
                            if (string.IsNullOrEmpty(wrapperId))
                            {
                                var warningMessage = $"Found a graph data table in {dataReference} without a data table id.";
                                await _outputGenerationProgressMessage($"WARNING: {warningMessage}");
                                appLogger.LogWarning(warningMessage);
                                nodeWrapper.SetAttribute("id", randomId);
                                wrapperId = randomId;
                            }
                            else
                            {
                                if (wrapperId.Contains("_")) wrapperId = wrapperId.SubstringAfter("_");
                            }

                            // - Calculate the paths of the files involved
                            var graphRenditionFileName = $"{Path.GetFileNameWithoutExtension(dataReference)}---{wrapperId}---{projectVariablesForMsWordGeneration.outputChannelVariantLanguage}.png";
                            var graphRenditionUri = $"/dataserviceassets/{projectVariablesForMsWordGeneration.projectId}/images/_renditions/graphs/{graphRenditionFileName}";

                            // Insert an image node which is going to contain the binary representation of the graph
                            var nodeImage = xmlMsWord.CreateElement("img");
                            SetAttribute(nodeImage, "src", graphRenditionUri);
                            SetAttribute(nodeImage, "alt", "Chart visual");
                            SetAttribute(nodeImage, "class", "chart");
                            SetAttribute(nodeImage, "data-contentencoding", "base64");

                            // Inject the image node where you would normally render the graph SVG
                            var nodeChartContent = nodeWrapper.SelectSingleNode("div[@class='chart-content']");
                            if (nodeChartContent != null)
                            {
                                var nodeImageBasedChartContentWrapper = xmlMsWord.CreateElement("div");
                                nodeImageBasedChartContentWrapper.SetAttribute("class", "chart-content");
                                var chartContentId = nodeChartContent.GetAttribute("id");
                                if (string.IsNullOrEmpty(chartContentId)) chartContentId = Path.GetFileNameWithoutExtension(graphRenditionFileName);
                                nodeImageBasedChartContentWrapper.SetAttribute("id", chartContentId);
                                nodeImageBasedChartContentWrapper.AppendChild(nodeImage);

                                ReplaceXmlNode(nodeChartContent, nodeImageBasedChartContentWrapper);
                            }

                            chartCounter++;
                        }

                        // Remove the data tables for the graphs
                        RemoveXmlNodes(nodeListGraphSourceTables);
                    }
                    else
                    {
                        appLogger.LogInformation("No need to inject graphs for this word file rendering");
                    }

                    // Store the document that we send to the service on the disk so that we can inspect it
                    if (debugRoutine) await xmlMsWord.SaveAsync($"{logRootPathOs}/_5-msword.graphs.xml", false);


                    //
                    // => Inject the images as base64 inline images
                    //
                    var nodeListImages = xmlMsWord.SelectNodes("//article//img");
                    foreach (XmlNode nodeImage in nodeListImages)
                    {
                        var imgSrc = nodeImage.GetAttribute("src");
                        if (string.IsNullOrEmpty(imgSrc))
                        {
                            appLogger.LogWarning($"Found image without a URI. html: {nodeImage.OuterXml}");
                            continue;
                        }

                        // - Store the original URL of the image in a data attribute so that we can easily locate it
                        nodeImage.SetAttribute("data-originaluri", imgSrc);

                        var currentProjectId = "";
                        var assetPathToRetrieve = "";
                        Match regexMatches = ReDataServiceAssetsPathParser.Match(imgSrc);
                        if (regexMatches.Success)
                        {
                            currentProjectId = regexMatches.Groups[1].Value;
                            assetPathToRetrieve = regexMatches.Groups[2].Value;

                            if (assetPathToRetrieve.Contains("?")) assetPathToRetrieve = assetPathToRetrieve.SubstringBefore("?");

                            var querystringData = new Dictionary<string, string>
                            {
                                { "pid", currentProjectId },
                                { "relativeto", "cmscontentroot" },
                                { "path", assetPathToRetrieve }
                            };

                            var apiUrl = QueryHelpers.AddQueryString(GetServiceUrlByMethodId(ConnectedServiceEnum.DocumentStore, "taxxoreditorfilingimage"), querystringData);

                            try
                            {
                                var byteArray = await RetrieveBinary(apiUrl);
                                if (byteArray != null && byteArray.Length > 0)
                                {
                                    // Retrieve the size of the image and dynamically set a new size which is used in the Word file
                                    using Image image = Image.Load(byteArray);
                                    int width = image.Width;
                                    int height = image.Height;

                                    double newWidth = 400;
                                    double newHeight = (double)height * (newWidth / (double)width);
                                    newHeight = Math.Round(newHeight, MidpointRounding.AwayFromZero);
                                    // Console.WriteLine($"Image {Path.GetFileName(imagePathOs)} width: {width} - height: {height}, newWidth: {newWidth} - newHeight: {newHeight}");
                                    nodeImage.SetAttribute("width", newWidth.ToString());
                                    nodeImage.SetAttribute("height", newHeight.ToString());

                                    // Convert the image to base64 and insert into the image node
                                    var mimeType = GetContentType(imgSrc);

                                    SetAttribute(nodeImage, "data-contentencoding", "base64");
                                    nodeImage.SetAttribute("src", $"data:{mimeType};base64,{Convert.ToBase64String(byteArray)}");
                                }
                                else
                                {
                                    var message = $"Unable to retrieve image from '{apiUrl}'";
                                    appLogger.LogError(message);
                                    await _outputGenerationProgressMessage($"ERROR: {message}");
                                    SetAttribute(nodeImage, "data-error", "not-found");
                                    SetAttribute(nodeImage, "data-contentencoding", "base64");
                                    SetAttribute(nodeImage, "src", BrokenImageBase64);
                                }
                            }
                            catch (Exception ex)
                            {
                                var errorMessage = $"Unable to retrieve image from '{apiUrl}'";
                                appLogger.LogError(ex, errorMessage);
                                await _outputGenerationProgressMessage($"ERROR: {errorMessage}");
                                SetAttribute(nodeImage, "data-error", "error-fetching");
                                SetAttribute(nodeImage, "data-contentencoding", "base64");
                                SetAttribute(nodeImage, "src", BrokenImageBase64);
                            }

                        }
                        else
                        {
                            if (imgSrc.StartsWith("/outputchannels"))
                            {
                                // This is an image which is managed by the partner developer packages and resides in the frontend folder
                                var imagePathOs = $"{websiteRootPathOs}{imgSrc}";
                                if (File.Exists(imagePathOs))
                                {
                                    var base64Content = Base64EncodeBinaryFile(imagePathOs);
                                    var mimeType = GetContentType(imgSrc);
                                    SetAttribute(nodeImage, "data-contentencoding", "base64");
                                    nodeImage.SetAttribute("src", $"data:{mimeType};base64,{base64Content}");

                                }
                                else
                                {
                                    var errorMessage = $"Unable to locate image {Path.GetFileName(imagePathOs)}";
                                    appLogger.LogError($"{errorMessage}. imagePathOs: {imagePathOs} not found");
                                    await _outputGenerationProgressMessage($"ERROR: {errorMessage}");
                                    SetAttribute(nodeImage, "data-error", "not-found");
                                    SetAttribute(nodeImage, "data-contentencoding", "base64");
                                    SetAttribute(nodeImage, "src", BrokenImageBase64);
                                }
                            }
                            else
                            {
                                var message = $"Unable to parse image path '{imgSrc}'";
                                appLogger.LogWarning(message);
                                await _outputGenerationProgressMessage($"ERROR: {message}");
                                SetAttribute(nodeImage, "data-error", "unknown-url");
                                SetAttribute(nodeImage, "data-contentencoding", "base64");
                                SetAttribute(nodeImage, "src", BrokenImageBase64);
                            }
                        }
                    }


                    // Store the document that we send to the service on the disk so that we can inspect it
                    if (debugRoutine) await xmlMsWord.SaveAsync($"{logRootPathOs}/_6-msword.embeddedimages.xml", false);

                    msWordHtml = xmlMsWord.OuterXml;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Could not load data for MS Word conversion");
                }


                //
                // => Generates XHTML skeleton and inserts the client-side JS and CSS that will be used by the MS Word generator for rendering the file
                //
                XmlDocument xmlMsWordComplete = new XmlDocument();
                TaxxorReturnMessage result = CreateCompleteMsWordHtml(msWordHtml, reqVars, projectVariablesForMsWordGeneration, msWordProperties);
                if (!result.Success)
                {
                    return GenerateErrorXml(result.Message, result.DebugInfo);
                }
                else
                {
                    // In the Message property we have inserted the translated HTML
                    xmlMsWordComplete.LoadXml(result.XmlPayload.OuterXml);
                }

                //
                // => Customer specific post processing
                //
                Extensions.PostProcessMsWordDoc(ref xmlMsWordComplete, projectVariablesForMsWordGeneration);

                //
                // => Remove comments in the XML as the contents might result in the numeric entities conversion routine to fail
                //
                foreach (XmlNode node in xmlMsWordComplete.SelectNodes("//article//comment()"))
                {
                    node.ParentNode.RemoveChild(node);
                }

                // Store the document that we send to the service on the disk so that we can inspect it
                if (debugRoutine) await xmlMsWordComplete.SaveAsync($"{logRootPathOs}/_7-msword.completehtml.xml", false);

                //
                // => Convert to numeric entities
                //
                using (var stream = new MemoryStream())
                {
                    using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings()
                    {
                        OmitXmlDeclaration = true,
                        Encoding = Encoding.ASCII
                    }))
                    {
                        xmlMsWordComplete.Save(writer);
                    }

                    msWordHtml = Encoding.ASCII.GetString(stream.ToArray());
                }

                // For some reason, a single quote seems to render as &apos; in the Word document so we replace it by the hex representation of the single quote
                msWordHtml = msWordHtml.Replace("'", "&#8217;");

                await TextFileCreateAsync(msWordHtml, CalculateFullPathOs("wordgenerator-dumpfile"));

                // Add the HTML to the data we will post to the Pandoc Service
                dataToPost.Add("html", msWordHtml);

                /*
                Add parameters for the Pandoc conversion tool
                */
                // (1) Base URL that the PDF Service needs to use to resolve paths to external assets (css, js, images)
                dataToPost.Add("baseurl", LocalWebAddressDomain);

                // Message to the client
                await _outputGenerationProgressMessage("Start Taxxor MS Word Generator");

                /*
                Render the MS Word file and grab the result in an XML envelope
                 */
                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.ConversionService, RequestMethodEnum.Post, "renderworddoc", dataToPost, true);
            }

        }

    }
}


/// <summary>
/// Object to be used when generating a MS Word/Excel files using the Taxxor Conversion Service
/// </summary>
public class MsOfficeFileProperties
{

    public string Sections = null;
    public string FileName = null;

    /// <summary>
    /// Suported are normal and preview
    /// If "preview" is used, then we need to send HTML directly to the Office Generator  
    /// </summary>
    public string Mode = "normal";

    /// <summary>
    /// Extra property to send HTML string
    /// </summary>
    public string Html = null;

    /// <summary>
    /// Show signature marks or not
    /// </summary>
    public bool SignatureMarks = true;

    /// <summary>
    /// Defines how many sections need to be included in the MS Word file. Possible values
    /// - single-section -> MS Word file should only contain the sections as defined in the Sections field
    /// - include-children -> MS Word file should contain the sections as defined in the Sections field and all child sections
    /// </summary>
    public string RenderScope = "single-section";

    /// <summary>
    /// Defines if we need to render fake ("Lorem Ipsum") content for the PDF
    /// </summary>
    public bool UseLoremIpsum = false;

    /// <summary>
    /// Process content status in the MS Office documents
    /// </summary>
    public bool UseContentStatus = false;

    /// <summary>
    /// Indicates if datapoints in the current reporting period should be hidden
    /// </summary>
    public bool HideCurrentPeriodDatapoints = false;

    /// <summary>
    /// Render hidden table elements
    /// </summary>
    public bool RenderHiddenElements = false;


    public MsOfficeFileProperties()
    {
        //
        // TODO: Add constructor logic here
        //
    }

}