using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
        /// Loads the base document to use for the XBRL conversion and adds it to the XbrlConversionProperties object
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> LoadFullTaxxorBaseDocument(XbrlConversionProperties xbrlConversionProperties, bool debugRoutine = false)
        {
            // Retrieve the XHTML that we need as the basis for the XBRL filing
            var xbrlHtml = await RetrieveLatestPdfHtml(xbrlConversionProperties.XbrlVars, xbrlConversionProperties.Sections, false, false);

            try
            {
                xbrlConversionProperties.XmlTaxxor.LoadXml(xbrlHtml);
                if (XmlContainsError(xbrlConversionProperties.XmlTaxxor))
                {
                    return new TaxxorReturnMessage(xbrlConversionProperties.XmlTaxxor);
                }
                else
                {
                    // Retrieve the report caption/title from the hierarchy which is included in the XML that was returned
                    xbrlConversionProperties.ReportCaption = xbrlConversionProperties.XmlTaxxor.SelectSingleNode("//metadata//structured/item/web_page/linkname")?.InnerText ?? "";

                    // Post process the base XHTML document
                    var postProcessingResult = Extensions.XhtmlFilingPostProcessing(xbrlConversionProperties.XmlTaxxor, xbrlConversionProperties.XbrlVars.projectId, xbrlConversionProperties.Channel);
                    if (postProcessingResult.Success)
                    {
                        xbrlConversionProperties.XmlTaxxor.ReplaceContent(postProcessingResult.XmlPayload);
                    }
                    else
                    {
                        // Return the result as an error
                        return postProcessingResult;
                    }

                    return new TaxxorReturnMessage(true, $"Successfully loaded full Taxxor {xbrlConversionProperties.Channel} document");
                }
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "Unable to load the full document", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }



        /// <summary>
        /// Prepares the XHTML document so that:
        /// - SVG is injected instead of referenced
        /// - New filenames are calculated for the images and the visuals
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> PrepareVisualsInTaxxorBaseDocument(XbrlConversionProperties xbrlConversionProperties, bool debugRoutine = false)
        {
            var contentLang = xbrlConversionProperties.XbrlVars.outputChannelVariantLanguage;
            var editorId = RetrieveEditorIdFromProjectId(xbrlConversionProperties.XbrlVars.projectId);

            // Retrieve the xPath pointing to the filestructure definition
            var resultFileStructureDefinitionXpath = RetrieveFileStructureXpath(editorId);
            if (!resultFileStructureDefinitionXpath.Success)
            {
                appLogger.LogWarning($"{resultFileStructureDefinitionXpath.Message}, debuginfo: {resultFileStructureDefinitionXpath.DebugInfo}, stack-trace: {GetStackTrace()}");
                return resultFileStructureDefinitionXpath;
            }

            var imagesWebsiteAssetsFolderPath = RetrieveAttributeValueIfExists($"{resultFileStructureDefinitionXpath.Payload}[@content='images']/@name", xmlApplicationConfiguration);
            var publicationImagesRootPathOs = $"{xbrlConversionProperties.InputFolderPathOs}{imagesWebsiteAssetsFolderPath}";
            var logMessage = "";

            var xmlXbrl = new XmlDocument();
            xmlXbrl.LoadXml(xbrlConversionProperties.XmlXbrl.OuterXml);

            var processSvgs = xbrlConversionProperties.RegulatorId == "sec" && PdfRenderEngine != "pagedjs";
            if (xbrlConversionProperties.Channel == "WEB") processSvgs = true;

            //
            // => Images
            //
            var xPathImages = "/div/article//section//img";
            var nodeListImages = xmlXbrl.SelectNodes(xPathImages);
            if (nodeListImages.Count == 0) appLogger.LogInformation($"Could not find any images in the content");
            var imgCounter = 0;
            var imagesCopied = new Dictionary<string, string>();
            foreach (XmlNode nodeImage in nodeListImages)
            {
                // Test if this image is an illustration (SVG)
                var nodeWrapper = nodeImage.ParentNode;
                var nodeWrapperClass = nodeWrapper.GetAttribute("class") ?? "";
                var originalImageUri = GetAttribute(nodeImage, "src") ?? "";

                // Skip copying SVG files in case we render an SEC filing and we do not use paged.js
                if (processSvgs)
                {
                    if ((nodeWrapper.LocalName == "div" && nodeWrapperClass.Contains("illustration")) || originalImageUri.Contains(".svg")) continue;
                }

                imgCounter++;


                if (string.IsNullOrEmpty(originalImageUri))
                {
                    appLogger.LogError($"Found an image without a source path. html: {nodeImage.OuterXml}");
                }
                else
                {
                    // Find the image on the disk, move it to the new location and then update the reference in the original image node
                    var originalImagePathOs = "";
                    var imageSubPath = "";
                    if (originalImageUri.StartsWith("/dataserviceassets"))
                    {
                        // Check if the SVG exists on the shared folder
                        imageSubPath = originalImageUri.SubstringAfter("/images");
                        originalImagePathOs = $"{xbrlConversionProperties.InputFolderPathOs}/{imagesWebsiteAssetsFolderPath}{imageSubPath}";
                    }
                    else if (originalImageUri.Contains("/dataserviceassets/"))
                    {
                        // Check if the SVG exists on the shared folder
                        imageSubPath = originalImageUri.SubstringAfter("/images");
                        originalImagePathOs = $"{xbrlConversionProperties.InputFolderPathOs}/{imagesWebsiteAssetsFolderPath}{imageSubPath}";
                    }
                    else
                    {
                        if (originalImageUri.StartsWith("/custom") || originalImageUri.StartsWith("/outputchannels"))
                        {
                            originalImagePathOs = websiteRootPathOs + originalImageUri;
                        }
                        else
                        {
                            return new TaxxorReturnMessage(false, "Unable to parse image uri", $"No conversion method for uri path {originalImageUri}");
                        }
                    }


                    if (!File.Exists(originalImagePathOs))
                    {
                        logMessage = $"Could not find image file on {originalImagePathOs} - this image is broken";
                        appLogger.LogError(logMessage);
                        await _outputGenerationProgressMessage($"ERROR: {logMessage}");
                        SetAttribute(nodeImage, "data-error", "not-found");
                    }
                    else
                    {
                        var copyError = false;
                        var imageAssetFileName = "";
                        if (!imagesCopied.ContainsKey(originalImagePathOs))
                        {
                            // Copy the image as it's not there yet
                            imageAssetFileName = _normalizedAssetName("image", originalImageUri, imgCounter);


                            // Potentially correct the paths
                            var targetImagePathOs = $"{xbrlConversionProperties.WorkingVisualsFolderPathOs}/{imageAssetFileName}";
                            if (siteType != "local")
                            {
                                // We have used full paths to the disk but the conversion service runs in a docker, so we need to corret the path
                                if (!originalImagePathOs.Contains("/custom") && !originalImagePathOs.Contains("/outputchannels")) originalImagePathOs = ConvertToDockerPath(originalImagePathOs);
                                targetImagePathOs = ConvertToDockerPath(targetImagePathOs);
                            }


                            try
                            {
                                File.Copy(originalImagePathOs, targetImagePathOs);
                                imagesCopied.Add(originalImagePathOs, targetImagePathOs);
                            }
                            catch (Exception e)
                            {
                                logMessage = $"Could not copy image file. originalImagePathOs: {originalImagePathOs}, targetImagePathOs: {targetImagePathOs}";
                                appLogger.LogError(e, logMessage);
                                await _outputGenerationProgressMessage($"ERROR: {logMessage}");
                                copyError = true;
                            }
                        }
                        else
                        {
                            // Re-use the normalized image name that we already have copied on the disk
                            imageAssetFileName = Path.GetFileName(imagesCopied[originalImagePathOs]);
                        }


                        // Use the normalized asset name in the content
                        if (copyError)
                        {
                            SetAttribute(nodeImage, "data-error", "could-not-copy");
                        }
                        else
                        {
                            if (xbrlConversionProperties.RegulatorId == "sec")
                            {
                                // The SEC only accepts JPG images
                                SetAttribute(nodeImage, "src", imageAssetFileName.Replace(".png", ".jpg"));
                            }
                            else
                            {
                                SetAttribute(nodeImage, "src", imageAssetFileName);
                            }
                        }

                    }
                }


            }

            // If we are generating a report for the SEC then convert PNG to JPG
            if (xbrlConversionProperties.RegulatorId == "sec")
            {
                // The image library contains image renditions for the PNG images - copy that rendered image
                var imageRenditionsError = 0;
                foreach (var imagePair in imagesCopied)
                {
                    var originalImagePathOs = imagePair.Key;
                    var targetImagePathOs = imagePair.Value;
                    if (Path.GetExtension(originalImagePathOs).ToLower() == ".png" || Path.GetExtension(originalImagePathOs).ToLower() == ".gif")
                    {
                        appLogger.LogInformation($"- originalImagePathOs: {originalImagePathOs}");
                        if (originalImagePathOs.Contains(xbrlConversionProperties.InputFolderPathOs))
                        {
                            // "/Users/jthijs/Documents/my_projects/taxxor/tdm/data/_shared/temp/xbrl_ar20-phg2019-en/input/publication/imgs"
                            var originalImagePath = originalImagePathOs.Replace($"{xbrlConversionProperties.InputFolderPathOs}/{imagesWebsiteAssetsFolderPath}", "");
                            appLogger.LogInformation($"- originalImagePath: {originalImagePath}");

                            var renditionImagePathOs = $"{xbrlConversionProperties.InputFolderPathOs}/{imagesWebsiteAssetsFolderPath}/{ImageRenditionsFolderName}/images/{originalImagePath.Replace(Path.GetExtension(targetImagePathOs), $".{ThumbnailFileExtension}")}".Replace("//", "/");
                            appLogger.LogInformation($"- renditionImagePathOs: {renditionImagePathOs}");
                            if (File.Exists(renditionImagePathOs))
                            {
                                // Copy the pre-rendered file
                                try
                                {
                                    File.Copy(renditionImagePathOs, targetImagePathOs.Replace(Path.GetExtension(targetImagePathOs), $".{ThumbnailFileExtension}"));
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Could not copy image renditions file");
                                }

                                // Remove the original png or gif file
                                try
                                {
                                    File.Delete(targetImagePathOs);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Could not remove original png or gif file");
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to find image rendition at '{renditionImagePathOs}'");
                                imageRenditionsError++;
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Orginal image '{originalImagePathOs}' is not located in the image library of this project '{xbrlConversionProperties.XbrlVars.projectId}'");
                            imageRenditionsError++;
                        }
                    }
                }

                // This should normally never occur, but as a fallback - convert the PNG images to JPG using imagemagick
                if (imageRenditionsError > 0)
                {
                    await _outputGenerationProgressMessage($"WARNING: {imageRenditionsError} images could not be found in the library, converting png images on-the-fly");
                    string[] pngFiles = Directory.GetFiles(xbrlConversionProperties.WorkingVisualsFolderPathOs, "*.png");
                    foreach (string pngFilePathOs in pngFiles)
                    {
                        appLogger.LogWarning($"WARNING: converting {pngFilePathOs.Replace(xbrlConversionProperties.WorkingVisualsFolderPathOs, "")} on-the-fly");
                        var imageConvertCommand = $"convert {pngFilePathOs} -background white -alpha remove -quality 85 {pngFilePathOs.Replace(".png", ".jpg")}";

                        var imageConversionResult = await ExecuteCommandLineAsync(imageConvertCommand, applicationRootPathOs);
                        if (imageConversionResult.Success)
                        {
                            // Remove the PNG file
                            try
                            {
                                File.Delete(pngFilePathOs);
                            }
                            catch (Exception ex)
                            {
                                logMessage = $"There was an error removing the PNG file pngFilePathOs: {pngFilePathOs}";
                                appLogger.LogError(ex, logMessage);
                                await _outputGenerationProgressMessage($"ERROR: {logMessage}");
                            }
                        }
                        else
                        {
                            logMessage = $"There was an error converting the image. pngFilePathOs: {Path.GetFileName(pngFilePathOs)}";
                            appLogger.LogError($"{logMessage}, pngFilePathOs: {pngFilePathOs}, imageConversionResult: {imageConversionResult.Message} - debuginfo: {imageConversionResult.DebugInfo}");
                            await _outputGenerationProgressMessage($"ERROR: {logMessage}");
                        }
                    }
                }
            }


            //
            // => Determine the rendition file format we want to use for including the drawings and graphs
            //
            var renditionExtension = ".jpg";
            if (xbrlConversionProperties.Channel == "WEB" || xbrlConversionProperties.RegulatorId == "afm") renditionExtension = ".png";

            //
            // => Graphs
            //
            var warningMessage = "";
            var chartCounter = 0;
            // - Find the articles that contain graphs
            var xPathArticlesWithGraphs = $"//article[descendant::{RetrieveGraphElementsBaseXpath(false, true).Substring(2)}]";
            var nodeListArticlesWithGraphs = xmlXbrl.SelectNodes(xPathArticlesWithGraphs);
            appLogger.LogInformation($"Number of articles with graphs: {nodeListArticlesWithGraphs.Count} found with xpath: {xPathArticlesWithGraphs}");
            if (nodeListArticlesWithGraphs.Count > 0)
            {
                var graphWrapperIds = new List<string>();

                var articleCounter = 0;
                foreach (XmlNode nodeArticleWithGraph in nodeListArticlesWithGraphs)
                {
                    var currentContentLang = nodeArticleWithGraph.GetAttribute("lang") ?? "undefined";
                    var dataReference = nodeArticleWithGraph.GetAttribute("data-ref") ?? "undefined";

                    // - Find the wrapper div elements that contain the graphs
                    var graphWrapperCounter = 0;
                    var nodeListGraphWrappers = nodeArticleWithGraph.SelectNodes($"*{RetrieveGraphElementsBaseXpath(false, true)}");
                    foreach (XmlNode nodeWrapper in nodeListGraphWrappers)
                    {

                        var uniqueId = string.Format("{0:X}", $"art{articleCounter}grph{graphWrapperCounter}".GetHashCode());

                        var nodeTable = nodeWrapper.SelectSingleNode("table");
                        var wrapperId = nodeWrapper.GetAttribute("id");

                        // - Make sure that no double wrapper ID's are present
                        if (graphWrapperIds.Contains($"{wrapperId}-{currentContentLang}"))
                        {
                            warningMessage = $"Found graph in {dataReference} with an ID {wrapperId} that is not unique.";
                            await _outputGenerationProgressMessage($"WARNING: {warningMessage}");
                            appLogger.LogWarning(warningMessage);
                        }
                        else
                        {
                            graphWrapperIds.Add($"{wrapperId}-{currentContentLang}");
                        }

                        // - Make sure an ID is present on the wrapper div
                        if (string.IsNullOrEmpty(nodeWrapper.GetAttribute("id")))
                        {
                            warningMessage = $"Found a graph data table in {dataReference} without a wrapper element id.";
                            await _outputGenerationProgressMessage($"WARNING: {warningMessage}");
                            appLogger.LogWarning(warningMessage);
                            nodeWrapper.SetAttribute("id", uniqueId);
                            wrapperId = uniqueId;
                        }
                        else
                        {
                            if (wrapperId.Contains("_")) wrapperId = wrapperId.SubstringAfter("_");
                        }

                        // - Calculate the paths of the files involved
                        var graphRenditionFileNameWithoutExtension = $"{Path.GetFileNameWithoutExtension(dataReference)}---{wrapperId}---{contentLang}";
                        var graphRenditionFileName = $"{graphRenditionFileNameWithoutExtension}{renditionExtension}";

                        var originalGraphRenditionPathOs = $"{xbrlConversionProperties.InputFolderPathOs}/{imagesWebsiteAssetsFolderPath}/{ImageRenditionsFolderName}/graphs/{graphRenditionFileName}";


                        var graphAssetFileName = _normalizedAssetName("chart", null, chartCounter);
                        var targetGraphRenditionPathOs = $"{xbrlConversionProperties.WorkingVisualsFolderPathOs}/{graphAssetFileName}".Replace(".png", renditionExtension);

                        // Potentially correct the paths
                        if (siteType != "local")
                        {
                            // We have used full paths to the disk but the conversion service runs in a docker, so we need to corret the path
                            originalGraphRenditionPathOs = ConvertToDockerPath(originalGraphRenditionPathOs);
                            targetGraphRenditionPathOs = ConvertToDockerPath(targetGraphRenditionPathOs);
                        }

                        // Copy the graph rendition from the source to the target location
                        var copyError = false;
                        try
                        {
                            File.Copy(originalGraphRenditionPathOs, targetGraphRenditionPathOs);
                        }
                        catch (Exception e)
                        {
                            logMessage = $"Could not copy graph rendition file. originalImagePathOs: {originalGraphRenditionPathOs.Replace(xbrlConversionProperties.InputFolderPathOs, "")}, targetImagePathOs: {targetGraphRenditionPathOs.Replace(xbrlConversionProperties.WorkingVisualsFolderPathOs, "")}";
                            appLogger.LogError(e, $"{logMessage}, dataReference: {dataReference}");
                            await _outputGenerationProgressMessage($"ERROR: {logMessage}");
                            copyError = true;
                        }

                        // Insert the graph image into the XHTML
                        var nodeImage = xmlXbrl.CreateElement("img");
                        SetAttribute(nodeImage, "class", "chart-placeholder");
                        if (!copyError)
                        {
                            // Set the src to the location of the copied image
                            SetAttribute(nodeImage, "src", Path.GetFileName(targetGraphRenditionPathOs));
                            SetAttribute(nodeImage, "alt", "Chart visual");
                        }
                        else
                        {
                            // Inject the broken image base 64 content as a source
                            SetAttribute(nodeImage, "src", BrokenImageBase64);
                            SetAttribute(nodeImage, "alt", "Broken chart image");
                            SetAttribute(nodeImage, "data-error", "could-not-copy");
                        }

                        // Inject the image node where you would normally render the graph SVG
                        var nodeChartContent = nodeWrapper.SelectSingleNode("div[@class='chart-content' or @class='tx-renderedchart']");
                        if (nodeChartContent != null)
                        {
                            var nodeImageBasedChartContentWrapper = xmlXbrl.CreateElement("div");
                            nodeImageBasedChartContentWrapper.SetAttribute("class", "chart-content");
                            var chartContentId = nodeChartContent.GetAttribute("id");
                            if (string.IsNullOrEmpty(chartContentId)) chartContentId = Path.GetFileNameWithoutExtension(originalGraphRenditionPathOs);
                            nodeImageBasedChartContentWrapper.SetAttribute("id", chartContentId);
                            nodeImageBasedChartContentWrapper.AppendChild(nodeImage);

                            ReplaceXmlNode(nodeChartContent, nodeImageBasedChartContentWrapper);
                        }

                        // Remove the table node
                        RemoveXmlNode(nodeWrapper.SelectSingleNode("table"));

                        chartCounter++;
                        graphWrapperCounter++;
                    }

                    articleCounter++;

                }

            }


            // Some additional logging
            if (siteType == "local") await xmlXbrl.SaveAsync(logRootPathOs + "/--priortodrawingsfinding.xml", false, true);


            //
            // => SVG sources
            //
            var svgCounter = 0;
            if (processSvgs)
            {
                var xPathSvgSources = $"//article//{RetrieveSvgElementsBaseXpath()}";
                var nodeListSvgSources = xmlXbrl.SelectNodes(xPathSvgSources);
                if (nodeListSvgSources.Count == 0) appLogger.LogInformation($"Could not find any SVG sources in the content");

                var drawingsCopied = new Dictionary<string, string>();
                foreach (XmlNode nodeSvgSource in nodeListSvgSources)
                {
                    var copyError = false;
                    svgCounter++;
                    var sourceAttributeName = (nodeSvgSource.LocalName == "img") ? "src" : "data";
                    var originalSvgUri = GetAttribute(nodeSvgSource, sourceAttributeName);

                    // var dataReference = nodeSvgSource.SelectSingleNode("ancestor::article")?.GetAttribute("data-ref") ?? "unknown";
                    // Console.WriteLine($"* Processing {originalSvgUri} from {dataReference}");

                    if (string.IsNullOrEmpty(originalSvgUri))
                    {
                        logMessage = $"Found SVG without a source path";
                        appLogger.LogError(logMessage);
                        await _outputGenerationProgressMessage($"ERROR: {logMessage}");
                        continue;
                    }

                    var originalDrawingPathOs = "";
                    var svgSubPath = "";
                    if (originalSvgUri.StartsWith("/dataserviceassets"))
                    {
                        // Check if the SVG exists on the shared folder
                        svgSubPath = originalSvgUri.SubstringAfter("/images");
                        originalDrawingPathOs = $"{xbrlConversionProperties.InputFolderPathOs}/{imagesWebsiteAssetsFolderPath}/{ImageRenditionsFolderName}/drawings{svgSubPath}".Replace(".svg", renditionExtension);
                    }
                    else
                    {
                        if (originalSvgUri.StartsWith("/custom") || originalSvgUri.StartsWith("/outputchannels"))
                        {
                            originalDrawingPathOs = websiteRootPathOs + originalSvgUri;
                        }
                        else
                        {
                            return new TaxxorReturnMessage(false, "Unable to parse svg uri", $"No conversion method for uri path {originalSvgUri}");
                        }
                    }

                    var svgFileExists = true;
                    if (!File.Exists(originalDrawingPathOs))
                    {
                        logMessage = $"Could not find svg file on {originalDrawingPathOs} - this drawing is broken";
                        appLogger.LogError(logMessage);
                        await _outputGenerationProgressMessage($"ERROR: {logMessage}");
                        svgFileExists = false;
                    }
                    else
                    {
                        var svgAssetFileName = _normalizedAssetName("svg", originalSvgUri, svgCounter);

                        // Potentially correct the paths
                        var targetDrawingPathOs = $"{xbrlConversionProperties.WorkingVisualsFolderPathOs}/{(Path.GetFileNameWithoutExtension(svgAssetFileName) + renditionExtension)}";
                        if (siteType != "local")
                        {
                            // We have used full paths to the disk but the conversion service runs in a docker, so we need to corret the path
                            if (!originalDrawingPathOs.Contains("/custom") && !originalDrawingPathOs.Contains("/outputchannels")) originalDrawingPathOs = ConvertToDockerPath(originalDrawingPathOs);
                            targetDrawingPathOs = ConvertToDockerPath(targetDrawingPathOs);
                        }

                        try
                        {
                            File.Copy(originalDrawingPathOs, targetDrawingPathOs);
                            drawingsCopied.Add(targetDrawingPathOs, originalDrawingPathOs);
                        }
                        catch (Exception e)
                        {
                            logMessage = $"Could not copy image file. originalImagePathOs: {originalDrawingPathOs}, targetImagePathOs: {targetDrawingPathOs}";
                            appLogger.LogError(e, logMessage);
                            await _outputGenerationProgressMessage($"ERROR: {logMessage}");
                            copyError = true;
                        }

                        // Inject a classic <img/> node that already contains the correct path to the converted image
                        var nodeImage = xmlXbrl.CreateElement("img");


                        SetAttribute(nodeImage, "class", "drawing-placeholder");
                        if (!copyError)
                        {
                            // Set the src to the location of the copied image
                            SetAttribute(nodeImage, "src", (Path.GetFileNameWithoutExtension(svgAssetFileName) + renditionExtension));
                            SetAttribute(nodeImage, "alt", "Drawing or illustration");
                        }
                        else
                        {
                            // Inject the broken image base 64 content as a source
                            SetAttribute(nodeImage, "src", BrokenImageBase64);
                            SetAttribute(nodeImage, "alt", "Broken drawing or illustration");
                            if (!svgFileExists) SetAttribute(nodeImage, "data-error", "source-not-found");
                            if (copyError) SetAttribute(nodeImage, "data-error", "could-not-copy");
                        }

                        nodeSvgSource.ParentNode.InsertAfter(nodeImage, nodeSvgSource);
                    }
                }

                // Remove the SVG object nodes from the XML
                foreach (XmlNode nodeSvgSource in nodeListSvgSources)
                {
                    RemoveXmlNode(nodeSvgSource);
                }

            }

            //
            // => Hard coded drawings
            //
            var renderHardCodedDrawings = Extensions.RenderHardCodedDrawings(xmlXbrl, xbrlConversionProperties.XbrlVars.projectId, xbrlConversionProperties.Channel, $"{xbrlConversionProperties.InputFolderPathOs}/{imagesWebsiteAssetsFolderPath}", $"{xbrlConversionProperties.WorkingVisualsFolderPathOs}");
            if (!renderHardCodedDrawings.Success)
            {
                appLogger.LogError(renderHardCodedDrawings.Message + ((!string.IsNullOrEmpty(renderHardCodedDrawings.DebugInfo) ? $", debugInfo: {renderHardCodedDrawings.DebugInfo}" : "")));
                await _outputGenerationProgressMessage($"ERROR: {renderHardCodedDrawings.Message}");
            }
            else
            {
                // Update the local version of the document with the one that we have received from the extension
                xmlXbrl.LoadXml(renderHardCodedDrawings.XmlPayload.OuterXml);
            }


            try
            {
                xbrlConversionProperties.XmlXbrl.LoadXml(xmlXbrl.OuterXml);
            }
            catch (Exception ex)
            {
                // Store the content on the disk
                await TextFileCreateAsync(xmlXbrl.OuterXml, $"{logRootPathOs}/error.xml");

                // Return an error message
                return new TaxxorReturnMessage(false, "There was an error loading the processed XML document back into the object", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

            return new TaxxorReturnMessage(true, $"Successfully prepared visuals ({imgCounter} images, {svgCounter} drawings, {chartCounter} charts)");
        }

        /// <summary>
        /// Converts the base Taxxor HTML source data to a format that we can use for converting to XBRL
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> BaseHtmlToInlineXbrlXhtml(XbrlConversionProperties xbrlConversionProperties, bool debugRoutine = false)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var errors = new List<string>();
            var warnings = new List<string>();
            var logMessage = "";

            var xsltArgumentList = new XsltArgumentList();
            XmlNodeList? nodeListCssAssets = null;
            var pageTitle = "";
            var currentYear = "";
            var inlineCss = "";


            // Global variables
            // // TODO: This should probably move to client specific code
            // var outputChannelVariant = "20F";
            // if (
            //     xbrlConversionProperties.Channel == "WEB" ||
            //     xbrlConversionProperties.ReportRequirementScheme == "ESMA" ||
            //     xbrlConversionProperties.ReportRequirementScheme == "SASB"
            // ) outputChannelVariant = "AR";

            // Transfer the XML document that we want to work with into a local variable
            var xmlXbrl = new XmlDocument();
            xmlXbrl.LoadXml(xbrlConversionProperties.XmlXbrl.OuterXml);


            //
            // => Post process it to make it ready for the Taxxor XBRL Service
            //
            try
            {
                //
                // => Strip all rows and cells that are hidden in the table
                //
                var nodeListHiddenTableWrappers = xmlXbrl.SelectNodes("//div[contains(@class, 'hide') and contains(@class, 'c-table')]");
                foreach (XmlNode nodeHiddenTableWrapper in nodeListHiddenTableWrappers)
                {
                    RemoveXmlNode(nodeHiddenTableWrapper);
                }

                var nodeListHiddenRows = xmlXbrl.SelectNodes("//table/*/tr[@data-hiddenrow='true' or contains(@class, 'hide')]");
                foreach (XmlNode nodeHiddenRow in nodeListHiddenRows)
                {
                    RemoveXmlNode(nodeHiddenRow);
                }

                var nodeListHiddenCells = xmlXbrl.SelectNodes("//table//*[@data-hiddencell='true' or contains(@class, 'hide')]");
                foreach (XmlNode nodeHiddenCell in nodeListHiddenCells)
                {
                    RemoveXmlNode(nodeHiddenCell);
                }

                //
                // => Remove the root article node that we do not need
                //
                RemoveXmlNode(xmlXbrl.SelectSingleNode("//article[@data-hierarchical-level='0']"));

                //
                // => Prepare the XHTML to (same as PDF):
                // - Create footnote summary at the end of a section
                // - remove 20-F / AR blocks etc
                // - Insert a TOC
                // - Check internal links
                // - Render dynamic header nodes (h1, h2, h3..) based on hierarchical position
                //
                var htmlPreperationResult = await PreparePagedMediaHtmlForRendering(xmlXbrl, xbrlConversionProperties);
                if (!htmlPreperationResult.Success)
                {
                    appLogger.LogError($"There was an error preparing the XBRL content. message: {htmlPreperationResult.Message}, debuginfo: {htmlPreperationResult.DebugInfo}");
                    return htmlPreperationResult;
                }
                else
                {
                    // Update the XBRL document with the new DOM that was generated
                    xmlXbrl.LoadXml(htmlPreperationResult.XmlPayload.OuterXml);

                    // Stream errors and warnings to the websocket
                    await _outputGenerationLogErrorAndWarnings(htmlPreperationResult);
                }
                xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "prepared");


                //
                // => Assure that footnote references in the tables use a unique ID
                //
                CreateUniqueFootnoteReferencesInTables(ref xmlXbrl);
                xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "footnotes-prepare");


                //
                // => Normalize ID's so that these are conforming to the XHTML specifications
                //
                xmlXbrl.ReplaceContent(CreateXhtmlCompliantIds(xmlXbrl));
                xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "normalized-ids");


                //
                // => Inline the css rules so that they become @styles on the HTML nodes
                //

                switch (xbrlConversionProperties.Channel)
                {
                    case "SECHTML":
                    case "XBRL":
                    case "IXBRL":

                        //
                        // * prepare the xml file by adding the HTML chrome, page title and css to it
                        //
                        switch (xbrlConversionProperties.RegulatorId)
                        {
                            case "sec":
                                // CSS which is used for this page
                                nodeListCssAssets = xbrlConversionProperties.XmlClientAssets.SelectNodes("//css/file");
                                foreach (XmlNode nodeCssAsset in nodeListCssAssets)
                                {
                                    var uriCss = GetAttribute(nodeCssAsset, "uri");
                                    if (uriCss.Contains("?"))
                                    {
                                        uriCss = uriCss.SubstringBefore("?");
                                    }
                                    // appLogger.LogInformation($"uriCss: {uriCss}");
                                    try
                                    {
                                        inlineCss += await RetrieveTextFile($"{websiteRootPathOs}{uriCss}", true);
                                    }
                                    catch (Exception ex)
                                    {
                                        appLogger.LogWarning($"Unable to retrieve stylesheet '{websiteRootPathOs}{uriCss}'. error: {ex}");
                                        await _outputGenerationProgressMessage($"WARNING: unable to retrieve stylesheet {uriCss}");
                                    }
                                }

                                // Add custom css for testing purposes
                                if (projectVars.projectId == "6k_2020-10-09")
                                {
                                    inlineCss += await RetrieveTextFile($"{websiteRootPathOs}/sandbox/multicolumn.css");
                                }
                                // Calculate page title
                                switch (xbrlConversionProperties.ReportRequirementScheme)
                                {
                                    case "SEC6K":
                                        pageTitle = $"{xbrlConversionProperties.ClientName} - 6-K";
                                        break;

                                    case "PHG2017":
                                    case "PHG2019":
                                        pageTitle = $"{xbrlConversionProperties.ClientName} - 20-F {xbrlConversionProperties.ReportingYear}";
                                        break;

                                    default:
                                        logMessage = $"No specific page title is being used for xbrlConversionProperties.ReportingRequirementScheme: {xbrlConversionProperties.ReportRequirementScheme}";
                                        appLogger.LogWarning(logMessage);
                                        await _outputGenerationProgressMessage($"WARNING: {logMessage}");
                                        pageTitle = $"{xbrlConversionProperties.ClientName}";
                                        break;
                                }

                                break;

                            case "esef":
                            case "afm":
                            case "sasb":
                                // CSS which is used for this page
                                switch (PdfRenderEngine)
                                {
                                    case "pagedjs":

                                        // - Inject the XBRL content into the viewer file wich is part of the Report Design Package
                                        var pagedMediaEnvelopeResult = await PdfService.InjectInPagedMediaEnvelope(projectVars, xmlXbrl);
                                        if (!pagedMediaEnvelopeResult.Success)
                                        {
                                            return pagedMediaEnvelopeResult;
                                        }

                                        xmlXbrl.ReplaceContent(pagedMediaEnvelopeResult.XmlPayload);
                                        break;

                                    case "prince":
                                        // Stylesheet to use

                                        nodeListCssAssets = xbrlConversionProperties.XmlClientAssets.SelectNodes("//css/file");
                                        foreach (XmlNode nodeCssAsset in nodeListCssAssets)
                                        {
                                            var uriCss = GetAttribute(nodeCssAsset, "uri");
                                            if (uriCss.Contains("?"))
                                            {
                                                uriCss = uriCss.SubstringBefore("?");
                                            }
                                            // appLogger.LogInformation($"uriCss: {uriCss}");
                                            inlineCss += await RetrieveTextFile($"{websiteRootPathOs}{uriCss}");
                                        }

                                        break;

                                    // Other render engines to be placed here


                                    default:
                                        return new TaxxorReturnMessage(false, "There was a problem preparing the XBRL content", $"unsupported PDF render engine: {PdfRenderEngine}");
                                }

                                // Page title
                                switch (xbrlConversionProperties.RegulatorId)
                                {
                                    case "esef":
                                    case "afm":
                                        pageTitle = $"{xbrlConversionProperties.ClientName} - ESMA/ESEF filing {xbrlConversionProperties.ReportingYear}";
                                        break;

                                    case "sasb":
                                        pageTitle = $"{xbrlConversionProperties.ClientName} - SASB filing {xbrlConversionProperties.ReportingYear}";
                                        break;
                                }

                                xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "pagedmedia-envelope");

                                break;
                        }

                        xsltArgumentList.AddParam("inline-css", "", inlineCss);
                        xsltArgumentList.AddParam("page-title", "", pageTitle);
                        xsltArgumentList.AddParam("renderengine", "", PdfRenderEngine);
                        xsltArgumentList.AddParam("regulatorid", "", xbrlConversionProperties.RegulatorId);
                        xsltArgumentList.AddParam("commentcontents", "", "");
                        xsltArgumentList.AddParam("signature-marks", "", ((xbrlConversionProperties.SignatureMarks) ? "yes" : "no"));


                        xmlXbrl = TransformXmlToDocument(xmlXbrl, "inlinecssprepare", xsltArgumentList);
                        xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "inlinecss-prepare");


                        //
                        // * Render a complete HTML document to base the XBRL filing on
                        //                 
                        TaxxorReturnMessage result = await CreateCompletePdfHtml(xmlXbrl, reqVars, xbrlConversionProperties);
                        if (!result.Success)
                        {
                            appLogger.LogError($"There was an error creating the complete the XBRL content. message: {result.Message}, debuginfo: {result.DebugInfo}");
                            return result;
                        }
                        else
                        {
                            // In the Message property we have inserted the translated HTML
                            xmlXbrl.LoadXml(result.XmlPayload.OuterXml);
                        }
                        xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "complete");


                        // return new TaxxorReturnMessage(false, "Thrown on purpose");

                        //
                        // * Post processing for images
                        //
                        if (xbrlConversionProperties.Channel != "XBRL")
                        {
                            switch (xbrlConversionProperties.RegulatorId)
                            {
                                case "esef":
                                case "afm":
                                case "sasb":
                                    try
                                    {
                                        // Embed the images as base64 in the content
                                        var imageXpath = "/html/body//img";
                                        var imagesToRemove = new List<string>();
                                        var nodeListImages = xmlXbrl.SelectNodesAgnostic(imageXpath);
                                        foreach (XmlNode nodeImage in nodeListImages)
                                        {
                                            var imagePathOrig = nodeImage.GetAttribute("src");
                                            if (imagePathOrig.Contains("?"))
                                            {
                                                imagePathOrig = imagePathOrig.SubstringBefore("?");
                                            }

                                            if (imagePathOrig.StartsWith("data:"))
                                            {
                                                appLogger.LogInformation($"Image with src {imagePathOrig.Substring(0, 20)} is already embedded as base 64.");
                                                continue;
                                            }

                                            // Console.WriteLine($"- imagePathOrig: {imagePathOrig}");

                                            var imagePathOs = $"{xbrlConversionProperties.WorkingVisualsFolderPathOs}/{imagePathOrig}";
                                            if (File.Exists(imagePathOs))
                                            {
                                                var imageExtension = Path.GetExtension(imagePathOs);


                                                // Read the image from file using optimized approach
                                                byte[]? originalImageData;
                                                var fileInfo = new FileInfo(imagePathOs);

                                                // Use FileStream with buffer pooling for more efficient reading
                                                using (var fileStream = new FileStream(
                                                    imagePathOs,
                                                    FileMode.Open,
                                                    FileAccess.Read,
                                                    FileShare.Read,
                                                    bufferSize: 64 * 1024, // 64KB buffer
                                                    options: FileOptions.SequentialScan)) // Optimize for sequential reads
                                                {
                                                    // For typical image files, we can allocate once
                                                    originalImageData = new byte[fileInfo.Length];
                                                    int bytesRead = 0;
                                                    int totalBytesRead = 0;

                                                    // Handle inexact reads by reading until we get the full content or reach EOF
                                                    while (totalBytesRead < fileInfo.Length &&
                                                          (bytesRead = await fileStream.ReadAsync(originalImageData, totalBytesRead, (int)fileInfo.Length - totalBytesRead)) > 0)
                                                    {
                                                        totalBytesRead += bytesRead;
                                                    }

                                                    // Verify if we got all the expected bytes
                                                    if (totalBytesRead != fileInfo.Length)
                                                    {
                                                        appLogger.LogWarning($"Only read {totalBytesRead} of {fileInfo.Length} bytes from file {imagePathOs}");
                                                    }
                                                }

                                                // Potentially, an SVG may be stored with the extension .png - in that case we need to inspect the binary to figure out the real file and mime type
                                                var imageType = (imageExtension == ".png") ? FileInspector.TryGetExtension(originalImageData) ?? "" : "jpg";
                                                var mimeType = (imageType == "svg") ? "image/svg+xml" : GetContentType(imagePathOrig);

                                                // Downsample the image to save space
                                                string base64Content;
                                                if (xbrlConversionProperties.ImageResizeFactor < 1 && (imageExtension == ".jpg" || imageExtension == ".jpeg" || imageExtension == ".png"))
                                                {
                                                    // Calculate original size
                                                    double originalSizeMB = originalImageData.Length / (1024.0 * 1024.0);

                                                    // Process image in place when possible
                                                    byte[] downsampledImageData = DownSampleImage(originalImageData, xbrlConversionProperties.ImageResizeFactor, imageExtension);

                                                    // Log combined size information
                                                    double downsampledSizeMB = downsampledImageData.Length / (1024.0 * 1024.0);
                                                    double reductionPercent = (1 - (downsampledImageData.Length / (double)originalImageData.Length)) * 100;
                                                    appLogger.LogInformation($"Image '{imagePathOrig}' downsampled: {originalSizeMB:F2} MB → {downsampledSizeMB:F2} MB (reduction: {reductionPercent:F1}%, factor: {xbrlConversionProperties.ImageResizeFactor})");

                                                    base64Content = Convert.ToBase64String(downsampledImageData);
                                                    // Allow the original data to be garbage collected
                                                    originalImageData = null;
                                                }
                                                else
                                                {
                                                    base64Content = Convert.ToBase64String(originalImageData);
                                                    // Allow the original data to be garbage collected immediately
                                                    originalImageData = null;
                                                }

                                                // Inject the binary image data as base64 content into the image source
                                                nodeImage.SetAttribute("src", $"data:{mimeType};base64,{base64Content}");

                                                // Add the path to the images that we need to remove
                                                if (!imagesToRemove.Contains(imagePathOs)) imagesToRemove.Add(imagePathOs);
                                            }
                                            else
                                            {
                                                await _outputGenerationProgressMessage($"WARNING: Unable to embed image '{imagePathOrig}'");
                                                appLogger.LogError($"Could not locate image with path {imagePathOs}");
                                            }
                                        }

                                        // Remove the images
                                        foreach (var imageToDeletePathOs in imagesToRemove)
                                        {
                                            try
                                            {
                                                File.Delete(imageToDeletePathOs);
                                            }
                                            catch (Exception ex)
                                            {
                                                appLogger.LogWarning($"Unable to delete image ${imageToDeletePathOs} - error: {ex}");
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        return new TaxxorReturnMessage(false, "There was an issue embedding the images in the instance document", $"error: {ex}");
                                    }

                                    xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "inlinecss-prepare-embedded-imgs");

                                    break;

                                default:
                                    appLogger.LogInformation($"No image post processing required");
                                    break;
                            }
                        }
                        else
                        {
                            await _outputGenerationProgressMessage($"{xbrlConversionProperties.Channel} output channel does not require image processing");
                            appLogger.LogInformation($"{xbrlConversionProperties.Channel} output channel does not require image processing");
                        }



                        // Optionally store the output for the HTML styler
                        if (xbrlConversionProperties.GenerateHtmlStylerOutput)
                        {
                            var htmlStylerSourceFilePathOs = $"{dataRootPathOs}/temp/htmlstyler-{xbrlConversionProperties.ReportRequirementScheme}.html";
                            var htmlStylerFolderPathOs = RenderHtmlStylerOutputPathOs(projectVars.projectId, $"ixbrl-{xbrlConversionProperties.ReportRequirementScheme}");

                            await xmlXbrl.SaveAsync(htmlStylerSourceFilePathOs, false, true);

                            File.Copy(htmlStylerSourceFilePathOs, $"{htmlStylerFolderPathOs}/index.html", true);
                        }

                        //
                        // * execute CSS inliner and/or the paged media generator
                        //
                        if (xbrlConversionProperties.Channel != "XBRL")
                        {
                            switch (xbrlConversionProperties.RegulatorId)
                            {
                                case "sec":
                                    // Inline the CSS
                                    xmlXbrl = await ConversionService.ConvertToInlineCss(xmlXbrl);
                                    if (XmlContainsError(xmlXbrl)) return new TaxxorReturnMessage(xmlXbrl);

                                    await _outputGenerationProgressMessage("Successfully generated inline css version");
                                    break;

                                case "esef":
                                case "afm":
                                case "sasb":

                                    switch (PdfRenderEngine)
                                    {
                                        case "pagedjs":
                                            await _outputGenerationProgressMessage("Start generating paged media layout");
                                            //
                                            // => Post process the XHTML content so that the PagedJS logic is processed
                                            //
                                            xmlXbrl = ValidHtmlVoidTags(xmlXbrl);

                                            // To assist debugging the paged media inliner
                                            await xmlXbrl.SaveAsync($"{xbrlConversionProperties.WorkingFolderPathOs}/ixbrl-to-htmlinliner.html");


                                            var inlineConversionResult = await Taxxor.ConnectedServices.PdfService.RenderInlinePagedMediaHtml(xmlXbrl, ReturnTypeEnum.Xml, xbrlConversionProperties.WorkingFolderPathOs);
                                            if (!inlineConversionResult.Success) return inlineConversionResult;

                                            xmlXbrl.ReplaceContent(inlineConversionResult.XmlPayload);

                                            //
                                            // => Make XHTML compliant
                                            //

                                            // - Style on the HTML node needs to move to the body node
                                            var nodeHtml = xmlXbrl.SelectSingleNode("/html[@style]");
                                            var nodeBody = xmlXbrl.SelectSingleNode("/html/body");
                                            if (nodeHtml != null && nodeBody != null)
                                            {
                                                var bodyCss = nodeBody.GetAttribute("style") ?? "";
                                                nodeBody.SetAttribute("style", $"{nodeHtml.GetAttribute("style")} {bodyCss}");
                                                nodeHtml.RemoveAttribute("style");
                                            }

                                            // - Type needs to be defined on style nodes
                                            var nodeListStyles = xmlXbrl.SelectNodes("/html//style[not(@type)]");
                                            foreach (XmlNode nodeStyle in nodeListStyles)
                                            {
                                                nodeStyle.SetAttribute("type", "text/css");
                                            }

                                            // - Remove non-compliant attributes
                                            var nodeWidthHeightNodes = xmlXbrl.SelectNodes("/html//*[@width or @height or @start]");
                                            foreach (XmlNode nodeWidthHeight in nodeWidthHeightNodes)
                                            {
                                                nodeWidthHeight.RemoveAttribute("width");
                                                nodeWidthHeight.RemoveAttribute("height");
                                                nodeWidthHeight.RemoveAttribute("start");
                                            }

                                            // TODO: this is specific for the TIE Kinetic case... And actually really an expensive hack
                                            // - Rework duplicate ID's
                                            //h1[@id = following::h1/@id or @id = preceding::h1/@id]
                                            var nodeListHeaderOneWithDuplicateId = xmlXbrl.SelectNodes("//h1[@id = preceding::h1/@id]");
                                            var counter = 0;
                                            foreach (XmlNode nodeHeaderOne in nodeListHeaderOneWithDuplicateId)
                                            {
                                                counter++;
                                                nodeHeaderOne.SetAttribute("id", $"{nodeHeaderOne.GetAttribute("id")}-{counter.ToString()}");
                                            }

                                            xmlXbrl = ValidHtmlVoidTags(xmlXbrl);


                                            await _outputGenerationProgressMessage("Successfully generated paged media layout");
                                            break;

                                        default:
                                            // Inline the CSS using the normal CSS inliner in the pandoc service
                                            xmlXbrl = await ConversionService.ConvertToInlineCss(xmlXbrl);
                                            if (XmlContainsError(xmlXbrl)) return new TaxxorReturnMessage(xmlXbrl);

                                            await _outputGenerationProgressMessage("Successfully generated inline css version");
                                            break;

                                    }

                                    break;

                                default:
                                    return new TaxxorReturnMessage(false, "Unsupported regulator", $"xbrlConversionProperties.Channel: {xbrlConversionProperties.Channel}, xbrlConversionProperties.RegulatorId: {xbrlConversionProperties.RegulatorId}");


                            }
                        }
                        else
                        {
                            await _outputGenerationProgressMessage($"{xbrlConversionProperties.Channel} output channel does not require css processing");
                            appLogger.LogInformation($"{xbrlConversionProperties.Channel} output channel does not require css processing");
                        }


                        break;
                }
                xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "inlinecss");


                //
                // => Process exhibit links for SEC filings
                //
                if (xbrlConversionProperties.RegulatorId == "sec")
                {
                    // - Locate links to exhibits inside this document           
                    var nodeListInternalLinks = xmlXbrl.SelectNodes($"//article//{RenderInternalLinkXpathSelector()}");
                    foreach (XmlNode nodeInternalLink in nodeListInternalLinks)
                    {
                        // Console.WriteLine(nodeInternalLink.OuterXml);
                        var logLinkProcessingDetails = false;
                        var logCurrentLink = true;

                        var sbLogMessage = new StringBuilder();

                        if (logLinkProcessingDetails) sbLogMessage.AppendLine($"[linksystem] => Link: {nodeInternalLink.OuterXml} <=");

                        var targetArticleId = GetAttribute(nodeInternalLink, "href");
                        if (!string.IsNullOrEmpty(targetArticleId))
                        {
                            // Test if the target exists
                            targetArticleId = targetArticleId.SubstringAfter("#");
                            var nodeTargetArticle = xmlXbrl.SelectSingleNode($"//article[@id='{targetArticleId}' or @data-guid='{targetArticleId}' or @data-fact-id='{targetArticleId}']");
                            if (nodeTargetArticle != null)
                            {
                                var targetHierarchicalLevel = nodeTargetArticle.GetAttribute("data-hierarchical-level");
                                var targetArticleType = GetAttribute(nodeTargetArticle, "data-articletype");
                                if (!string.IsNullOrEmpty(targetArticleType))
                                {
                                    if (targetArticleType.Contains("exhibit"))
                                    {
                                        switch (nodeInternalLink.InnerText.ToLower())
                                        {
                                            case string a when a.Contains("index of exhibits"):
                                                // case string b when b.Contains(","):
                                                if (!targetArticleId.ToLower().Contains("exhibit")) logCurrentLink = false;
                                                if (logLinkProcessingDetails) sbLogMessage.AppendLine($"Target article type: {targetArticleType} is not an exhibit");
                                                break;

                                            default:
                                                // Decorate the link to an exhibit in a special way
                                                nodeInternalLink.SetAttribute("data-link-type", "exhibit");
                                                var exhibitLinkStyle = $"-sec-extract:exhibit; {nodeInternalLink.GetAttribute("style") ?? ""}";
                                                nodeInternalLink.SetAttribute("style", exhibitLinkStyle);

                                                if (logLinkProcessingDetails) sbLogMessage.AppendLine($"YES - found exhibit link - YES");
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        if (!targetArticleId.ToLower().Contains("exhibit")) logCurrentLink = false;
                                        if (logLinkProcessingDetails) sbLogMessage.AppendLine($"Target article type: {targetArticleType} is not an exhibit");
                                    }
                                }
                                else
                                {
                                    if (logLinkProcessingDetails) sbLogMessage.AppendLine($"No data-articletype found");
                                }
                            }
                            else
                            {
                                if (logLinkProcessingDetails) sbLogMessage.AppendLine($"Could not find target article with id: {targetArticleId}");
                            }
                        }
                        else
                        {
                            if (logLinkProcessingDetails) sbLogMessage.AppendLine($"Empty href (link target)");
                        }

                        if (logLinkProcessingDetails && logCurrentLink) appLogger.LogInformation(sbLogMessage.ToString());
                    }

                    // - Locate links to external exhibit references: external links inside the sec.gov domain are (most likely) links to articles filed in previous filings. These links need to be marked in a special way
                    var nodeListEdgarLinks = xmlXbrl.SelectNodes("//article//a[contains(@href, 'www.sec.gov/Archives/edgar')]");
                    foreach (XmlNode nodeEdgarLink in nodeListEdgarLinks)
                    {
                        // Decorate the link to an exhibit in a special way
                        nodeEdgarLink.SetAttribute("data-link-type", "exhibit");
                        var exhibitLinkStyle = $"-sec-extract:exhibit; {nodeEdgarLink.GetAttribute("style") ?? ""}";
                        nodeEdgarLink.SetAttribute("style", exhibitLinkStyle);

                        var secUri = GetAttribute(nodeEdgarLink, "href");
                        if (secUri.StartsWith("https://"))
                        {
                            SetAttribute(nodeEdgarLink, "href", secUri.Replace("https://", "http://"));
                        }
                    }

                    var nodeListSeperateExhibits = xmlXbrl.SelectNodes("//article//a[@data-secexhibitmark='true']");
                    foreach (XmlNode nodeLink in nodeListSeperateExhibits)
                    {
                        // Decorate the link to an exhibit in a special way
                        nodeLink.SetAttribute("data-link-type", "exhibit");
                        var exhibitLinkStyle = $"-sec-extract:exhibit; {nodeLink.GetAttribute("style") ?? ""}";
                        nodeLink.SetAttribute("style", exhibitLinkStyle);
                    }

                    xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "exhibits-processed");
                }


                //
                // => Generate the regulator specific output
                //
                switch (xbrlConversionProperties.Channel)
                {
                    case "SECHTML":
                    case "XBRL":
                    case "IXBRL":
                        // Rendering output for a regulator
                        switch (xbrlConversionProperties.RegulatorId)
                        {
                            case "sec":
                                // 
                                // => Strip the exhibits from the main document
                                //
                                var nodeListExhibitArticles = xmlXbrl.SelectNodes("//article[(contains(@data-articletype, 'exhibit') or contains(@id, 'exhibit')) and (not(contains)) and number(@data-hierarchical-level)>1]");
                                var exhibitCounter = 0;
                                foreach (XmlNode nodeExhibitArticle in nodeListExhibitArticles)
                                {
                                    exhibitCounter++;
                                    var exhibitArticleId = GetAttribute(nodeExhibitArticle, "id");
                                    if (string.IsNullOrEmpty(exhibitArticleId))
                                    {
                                        logMessage = "Could not find the exhibit article ID";
                                        appLogger.LogError(logMessage);
                                        await _outputGenerationProgressMessage($"WARNING: {logMessage}");
                                    }
                                    else
                                    {
                                        var nodeMainTitle = nodeExhibitArticle.SelectSingleNode("*//*[local-name()='h1' or local-name()='h2' or local-name()='h3']");
                                        var exhibitTitle = "";

                                        if (nodeMainTitle == null)
                                        {
                                            // Attempt to locate the title of the exhibit from the hierarchy

                                            appLogger.LogInformation("Attempt to find the name of the article in the hierarchy");

                                            var exhibitDataRef = GetAttribute(nodeExhibitArticle, "data-ref");
                                            if (string.IsNullOrEmpty(exhibitDataRef))
                                            {
                                                logMessage = "Could not locate the exhibit in the hierarchy, because data-ref is missing or empty";
                                                appLogger.LogWarning(logMessage);
                                                await _outputGenerationProgressMessage($"WARNING: {logMessage}");
                                            }
                                            else
                                            {
                                                var nodeHierarchyLinkname = xbrlConversionProperties.XmlHierarchy.SelectSingleNode($"//item[@data-ref='{exhibitDataRef}']/web_page/linkname");
                                                if (nodeHierarchyLinkname == null)
                                                {
                                                    logMessage = $"Could not locate exhibit item with data-ref: {exhibitDataRef} in the hierarchy";
                                                    appLogger.LogWarning(logMessage);
                                                    await _outputGenerationProgressMessage($"WARNING: {logMessage}");
                                                    exhibitTitle = $"unknown-{exhibitCounter.ToString()}";
                                                }
                                                else
                                                {
                                                    exhibitTitle = nodeHierarchyLinkname.InnerText;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            exhibitTitle = nodeMainTitle.InnerText;
                                            if (nodeMainTitle.SelectSingleNode("span[@class='nr']") != null)
                                            {
                                                var nodeListTextFragments = nodeMainTitle.SelectNodes("text()");
                                                exhibitTitle = nodeListTextFragments.Item(0).InnerText;
                                            }
                                        }

                                        //Console.WriteLine($"- exhibitTitle: {exhibitTitle}");

                                        // Only treat the content as an exhibit if it contains a number
                                        if (RegExpTest(@"\d", exhibitTitle))
                                        {
                                            // Convert the exhibit title to a nice filename
                                            var exhibitFileName = xbrlConversionProperties.Prefix + "-" + RegExpReplace(@"([^a-z\d\s])", exhibitTitle.ToLower(), "").Replace(" ", "") + ".htm";

                                            var exhibit = new XbrlExhibit(exhibitFileName);
                                            exhibit.Title = exhibitTitle;
                                            exhibit.Xml.LoadXml($"<content>{nodeExhibitArticle.OuterXml}</content>");

                                            xbrlConversionProperties.Exhibits.Add(exhibitArticleId, exhibit);

                                            // Strip this node from the xbrlXml
                                        }
                                        else
                                        {
                                            appLogger.LogInformation($"Processing of exhibit with title: '{exhibitTitle}' skipped");
                                        }
                                    }
                                }

                                // Remove the exhibit nodes from the main document
                                appLogger.LogInformation($"Found {xbrlConversionProperties.Exhibits.Count} exhibits that will be removed from the main XBRL document");
                                foreach (var pair in xbrlConversionProperties.Exhibits)
                                {
                                    var exhibitArticleId = pair.Key;
                                    var exhibit = pair.Value;
                                    var exhibitFileName = exhibit.FileName;
                                    // Console.WriteLine($"- {exhibitArticleId} has filename {exhibit.FileName}");

                                    var nodeExhibitArticleToRemove = xmlXbrl.SelectSingleNode($"//article[@id='{exhibitArticleId}']");
                                    if (nodeExhibitArticleToRemove == null)
                                    {
                                        logMessage = $"Could not find exhibit article with ID {exhibitArticleId} to remove from the main document.";
                                        appLogger.LogError(logMessage + $" stack-trace: {GetStackTrace()}");
                                        await _outputGenerationProgressMessage($"ERROR: {logMessage}");
                                    }
                                    else
                                    {
                                        RemoveXmlNode(nodeExhibitArticleToRemove);
                                    }

                                    // Correct the links pointing to the exhibit documents
                                    var nodeListExhibitLinks = xmlXbrl.SelectNodes($"//article//a[@href = '#{exhibitArticleId}']");
                                    foreach (XmlNode nodeExhibitLink in nodeListExhibitLinks)
                                    {
                                        SetAttribute(nodeExhibitLink, "href", exhibitFileName);
                                    }

                                }
                                xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "exhibits-removed");



                                //
                                // => Basic SEC iXBRL output (make sure that we only use nodes which are allowed by the SEC)
                                //
                                currentYear = DateTime.Now.ToString("yyyy");
                                xsltArgumentList = new XsltArgumentList();
                                xsltArgumentList.AddParam("report-type-id", "", (xbrlConversionProperties.XbrlVars.reportTypeId));
                                xsltArgumentList.AddParam("report-requirement-scheme", "", (xbrlConversionProperties.ReportRequirementScheme));

                                xmlXbrl = TransformXmlToDocument(xmlXbrl, "sec_ixbrl", xsltArgumentList);

                                // Save this file in the log directory for inspection, but also so that we can pick it up for the HTML styler
                                if (debugRoutine) await xmlXbrl.SaveAsync($"{logRootPathOs}/inline-xbrl-base.html", false);

                                // if (siteType == "local") xmlXbrl.Load($"{logRootPathOs}/test-q121.xml");

                                // Store the document that we send to the service on the disk so that we can inspect it
                                xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "sec-ixbrl");

                                //
                                // => Basic SEC iXBRL output for exhibits
                                //
                                foreach (var pair in xbrlConversionProperties.Exhibits)
                                {
                                    var exhibit = pair.Value;
                                    var xmlExhibit = new XmlDocument();
                                    xmlExhibit.LoadXml(exhibit.Xml.OuterXml);

                                    // Generate the HTML chrome with the title and css 
                                    xsltArgumentList = new XsltArgumentList();
                                    xsltArgumentList.AddParam("inline-css", "", inlineCss);
                                    xsltArgumentList.AddParam("page-title", "", pageTitle);
                                    xsltArgumentList.AddParam("renderengine", "", PdfRenderEngine);
                                    xsltArgumentList.AddParam("regulatorid", "", xbrlConversionProperties.RegulatorId);
                                    xsltArgumentList.AddParam("commentcontents", "", xbrlConversionProperties.CommentContents ?? "");
                                    xsltArgumentList.AddParam("signature-marks", "", ((xbrlConversionProperties.SignatureMarks) ? "yes" : "no"));

                                    xmlExhibit = TransformXmlToDocument(xmlExhibit, "inlinecssprepare", xsltArgumentList);

                                    // Inline the CSS
                                    xmlExhibit = await ConversionService.ConvertToInlineCss(xmlExhibit);
                                    if (XmlContainsError(xmlExhibit)) return new TaxxorReturnMessage(xmlExhibit);

                                    // Process the html further                                                      
                                    xsltArgumentList = new XsltArgumentList();
                                    xsltArgumentList.AddParam("report-type-id", "", (xbrlConversionProperties.XbrlVars.reportTypeId));
                                    xsltArgumentList.AddParam("report-requirement-scheme", "", (xbrlConversionProperties.ReportRequirementScheme));
                                    xmlExhibit = TransformXmlToDocument(xmlExhibit, "sec_ixbrl", xsltArgumentList);

                                    // Treat the Exhibit as regular EDGAR HTML
                                    xmlExhibit = TransformXmlToDocument(xmlExhibit, "sec_edgar");

                                    // Remove steering attributes
                                    var cleanAttributesResultExhibits = RemoveSteeringAttributes(xmlExhibit, xbrlConversionProperties, debugRoutine);
                                    await _handleProcessStepResult(xbrlConversionProperties, cleanAttributesResultExhibits);
                                    xmlExhibit.LoadXml(cleanAttributesResultExhibits.XmlPayload.OuterXml);

                                    exhibit.Xml.LoadXml(xmlExhibit.OuterXml);
                                }
                                await xbrlConversionProperties.SaveExhibits(xbrlConversionProperties.OutputIxbrlFolderPathOs, true);

                                break;

                            case "esef":
                            case "afm":
                            case "sasb":

                                if (debugRoutine) await xmlXbrl.SaveAsync($"{logRootPathOs}/inline-xbrl-base.before.html", false);

                                // Render the basic page that will be used as a basis going forward
                                currentYear = DateTime.Now.ToString("yyyy");
                                xsltArgumentList = new XsltArgumentList();
                                xsltArgumentList.AddParam("lang", "", "en"); // TODO: this needs to become dynamic
                                xsltArgumentList.AddParam("report-type-id", "", (xbrlConversionProperties.XbrlVars.reportTypeId));
                                xsltArgumentList.AddParam("report-requirement-scheme", "", (xbrlConversionProperties.ReportRequirementScheme));

                                xmlXbrl = TransformXmlToDocument(xmlXbrl, "esma_ixbrl", xsltArgumentList);

                                xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "esma-ixbrl");

                                if (debugRoutine) await xmlXbrl.SaveAsync($"{logRootPathOs}/inline-xbrl-base.html", false);

                                break;

                            default:

                                return new TaxxorReturnMessage(false, $"No processing rules defined for regulator with id: {xbrlConversionProperties.RegulatorId}");
                        }
                        break;

                    case "WEB":

                        // For the full-html version
                        var inlineFullWebCss = "";
                        var nodeListCssAssetsWeb = xbrlConversionProperties.XmlClientAssets.SelectNodes("//css/file");
                        foreach (XmlNode nodeCssAsset in nodeListCssAssetsWeb)
                        {
                            var uriCss = GetAttribute(nodeCssAsset, "uri");
                            if (uriCss.Contains("?"))
                            {
                                uriCss = uriCss.SubstringBefore("?");
                            }
                            // appLogger.LogInformation($"uriCss: {uriCss}");
                            inlineFullWebCss += await RetrieveTextFile($"{websiteRootPathOs}{uriCss}");
                        }

                        //var overrideCss = await RetrieveTextFile($"{applicationRootPathOs}/frontend/public/outputchannels/{TaxxorClientId}/website/css/full-html.css");
                        var xsltFilePathOs = $"{applicationRootPathOs}/backend/code/custom/full-html.xsl";
                        xsltArgumentList = new XsltArgumentList();
                        xsltArgumentList.AddParam("inline-css", "", inlineFullWebCss);
                        //xsltArgumentList.AddParam("inline-css-override", "", overrideCss);
                        xsltArgumentList.AddParam("page-title", "", $"{xbrlConversionProperties.ClientName} - Full {xbrlConversionProperties.ReportingYear} Annual Report");
                        xsltArgumentList.AddParam("current-year", "", DateTime.Now.ToString("yyyy"));
                        xmlXbrl.LoadXml(TransformXml(xmlXbrl, xsltFilePathOs, xsltArgumentList));

                        break;

                    default:
                        // Unknown output channel
                        return new TaxxorReturnMessage(false, $"No processing rules defined channel: {xbrlConversionProperties.Channel}");
                }

            }
            catch (Exception ex)
            {
                var errorMessage = "There was an error post-processing the XHTML document";
                appLogger.LogError(ex, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

            // Store the document that we send to the service on the disk so that we can inspect it
            xbrlConversionProperties.LogXmlXbrl(xmlXbrl, "channel-processed");

            //
            // => Store the processed XML file back into the properties object that we are using
            //
            try
            {
                xbrlConversionProperties.XmlXbrl.LoadXml(xmlXbrl.OuterXml);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error loading the post processed XML document back into the object", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

            // Return success
            return new TaxxorReturnMessage(true, "Successfully created XBRL assets", "");
        }

        /// <summary>
        /// Creates the XBRL package needed for the filing
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CompileXbrlPackage(XbrlConversionProperties xbrlConversionProperties, bool debugRoutine = false)
        {
            try
            {
                var outputFolderPathOs = (xbrlConversionProperties.Channel == "IXBRL") ? xbrlConversionProperties.OutputIxbrlFolderPathOs : xbrlConversionProperties.OutputXbrlFolderPathOs;

                // Copy all the visuals
                try
                {
                    CopyDirectoryRecursive(xbrlConversionProperties.WorkingVisualsFolderPathOs, outputFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, $"Could not compile inline XBRL package", $"error: {ex}");
                }


                // Grab the Inline XBRL document that we have been preparing      
                var xmlXbrl = new XmlDocument();
                xmlXbrl.LoadXml(xbrlConversionProperties.XmlXbrl.OuterXml);

                // Last minute fix to avoid sending empty classes to the generator
                var nodeListElementsWithEmptyClass = xmlXbrl.SelectNodes("//*[normalize-space(@class)='']");
                if (nodeListElementsWithEmptyClass.Count > 0)
                {
                    // Remove the class attribute from each selected element
                    foreach (XmlNode node in nodeListElementsWithEmptyClass)
                    {
                        if (node.Attributes != null)
                        {
                            node.Attributes.RemoveNamedItem("class");
                        }
                    }
                }

                // Cleanup the document to avoid self closing tags
                // RemoveRenderedGraphs(ref xmlXbrl);
                // RemoveEmptyElements(ref xmlXbrl);
                // AddCommentInEmptyElements(ref xmlXbrl);

                // Last minute rework before sending to generator
                switch (xbrlConversionProperties.RegulatorId)
                {
                    case "sec":

                        // Temporary fix
                        // var nodeListArticlesWithReportingRequirement = xmlXbrl.SelectNodes("//div[@data-originalnodename='article' and contains(@data-reportingrequirements, 'PHG')]");
                        // foreach (XmlNode nodeArticleWithReportingRequirement in nodeListArticlesWithReportingRequirement)
                        // {
                        //     nodeArticleWithReportingRequirement.SetAttribute("data-reportingrequirements", "PHG2017");
                        // }

                        break;
                }

                //
                // => Store the document that we send to the generator
                //
                await xmlXbrl.SaveAsync($"{xbrlConversionProperties.WorkingFolderPathOs}/ixbrl-to-generator.xml");

                // Report the file size
                var xbrlFilePath = $"{xbrlConversionProperties.WorkingFolderPathOs}/ixbrl-to-generator.xml";
                if (File.Exists(xbrlFilePath))
                {
                    var fileInfo = new FileInfo(xbrlFilePath);
                    var fileSizeInMB = fileInfo.Length / (1024.0 * 1024.0);
                    appLogger.LogInformation($"XHTML size: {fileSizeInMB:F2} MB ({fileInfo.Length:N0} bytes)");
                    await _outputGenerationProgressMessage($"XHTML size: {fileSizeInMB:F2} MB");
                }


                //
                // => Generate an Inline XBRL package and store the result in the output directory
                //
                var xbrlGenerationResult = new TaxxorReturnMessage();
                switch (xbrlConversionProperties.RegulatorId)
                {
                    case "sec":
                        xbrlGenerationResult = await XbrlService.RenderSecXbrl(xmlXbrl, xbrlConversionProperties.XbrlVars.projectId, outputFolderPathOs, xbrlConversionProperties.WorkingFolderPathOs, xbrlConversionProperties.CommentContents);
                        break;

                    case "esef":
                    case "afm":
                    case "sasb":
                        switch (xbrlConversionProperties.ReportRequirementScheme)
                        {
                            case "dVI" when xbrlConversionProperties.Channel == "XBRL":
                            case "CVO" when xbrlConversionProperties.Channel == "XBRL":
                            case "OCW-XBRL" when xbrlConversionProperties.Channel == "XBRL":
                                xbrlGenerationResult = await XbrlService.RenderPlainXbrl(xmlXbrl, xbrlConversionProperties.XbrlVars.projectId, outputFolderPathOs, xbrlConversionProperties.WorkingFolderPathOs, xbrlConversionProperties.CommentContents, xbrlConversionProperties.ReportRequirementScheme);
                                break;

                            default:
                                xbrlGenerationResult = await XbrlService.RenderEsmaXbrl(xmlXbrl, xbrlConversionProperties.XbrlVars.projectId, outputFolderPathOs, xbrlConversionProperties.WorkingFolderPathOs, xbrlConversionProperties.CommentContents, xbrlConversionProperties.ReportRequirementScheme);
                                break;
                        }
                        break;

                    default:
                        return new TaxxorReturnMessage(false, $"Unable to generate XBRL package as {xbrlConversionProperties.RegulatorId} is not supported");
                }
                if (!xbrlGenerationResult.Success) return xbrlGenerationResult;

                //
                // => Store the path of the XBRL package ZIP file
                //
                xbrlConversionProperties.XbrlPackagePathOs = xbrlGenerationResult.Payload;

                //
                // => Dynamically find the iXBRL instance document filename
                //
                var inlineXbrlFilePathOs = RetrieveInstanceDocumentPath(xbrlConversionProperties.WorkingFolderPathOs);
                if (string.IsNullOrEmpty(inlineXbrlFilePathOs))
                {
                    return new TaxxorReturnMessage(false, $"Unable to retrieve instance document path.", $"stack-trace: {GetStackTrace()}");
                }

                //
                // => Load the generated instance document
                //
                xmlXbrl.Load(inlineXbrlFilePathOs);


                //
                // => Copy the original instance document from the XBRL generator to the log folder so that we can use it for inspection
                //
                try
                {
                    File.Copy(inlineXbrlFilePathOs, $"{xbrlConversionProperties.LogFolderPathOs}/{Path.GetFileName(inlineXbrlFilePathOs)}");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Unable to copy instance document into log folder for inspection. error: {ex}");
                }



                //
                // => Log the result
                //
                await _handleProcessStepResult(xbrlConversionProperties, xbrlGenerationResult, "generator-result");
                File.Copy(inlineXbrlFilePathOs, $"{xbrlConversionProperties.WorkingFolderPathOs}/ixbrl-from-generator.xml");

                // Load the instance document again for post-processing
                try
                {
                    // TODO: this is a temporary fix
                    if (xbrlConversionProperties.RegulatorId != "sec")
                    {
                        // Load the instance document into the XbrlConversionProperties object to post-process if needed
                        xbrlConversionProperties.XmlXbrl.LoadXml(xmlXbrl.OuterXml);
                    }
                    else
                    {
                        // This is wrong: it does not add the instance document to the XbrlConversionProperties object
                        xmlXbrl.Load(inlineXbrlFilePathOs);
                    }
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "There was an issue loading the XBRL instance document after generation", $"error: {ex}");
                }

                // Return success
                return new TaxxorReturnMessage(true, "Successfully compiled the iXBRL package", "");
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "Something went wrong while compiling the XBRL package", $"error: {ex}");
            }
        }


        /// <summary>
        /// Renders an (i)XBRL validation package
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CreateValidationPackage(XbrlConversionProperties xbrlConversionProperties, bool debugRoutine = false)
        {
            if (xbrlConversionProperties.ReportRequirementScheme == "SASB")
            {
                xbrlConversionProperties.ValidationPackageUri = "";
                return new TaxxorReturnMessage(true, $"XBRL validation process disabled for {xbrlConversionProperties.ReportRequirementScheme}");
            }

            // Creation of the validation package can take a very long time, so we want to inform the user that the process has started
            await _outputGenerationProgressMessage($"Start creation of the iXBRL validation package");

            try
            {
                //
                // => Generic variables
                //
                var xbrlPackageType = "";
                var xbrlFileToValidate = xbrlConversionProperties.XbrlPackagePathOs;
                MediaTypeHeaderValue contentType = new MediaTypeHeaderValue(System.Net.Mime.MediaTypeNames.Application.Octet);
                switch (xbrlConversionProperties.RegulatorId)
                {
                    case "afm" when xbrlConversionProperties.ReportRequirementScheme == "dVI":
                    case "afm" when xbrlConversionProperties.ReportRequirementScheme == "CVO":
                        xbrlPackageType = "ixbrl";
                        xbrlFileToValidate = $"{xbrlConversionProperties.WorkingFolderPathOs}/ixbrl-from-generator.xml";
                        break;

                    case "esef":
                    case "afm":
                        xbrlPackageType = "esef";
                        break;

                    case "sec":
                        xbrlPackageType = "sec";
                        break;
                }



                if (!File.Exists(xbrlFileToValidate)) return new TaxxorReturnMessage(false, "Unable to generate the XBRL valication package", $"XBRL zip package not fount at {xbrlFileToValidate}");

                // - URL to use for the request
                var uriArelleService = GetServiceUrl(ConnectedServiceEnum.EdgarArelleService);
                string url = $"{uriArelleService}/api/arelle/validate?type={xbrlPackageType}&name={Path.GetFileName(xbrlFileToValidate)}";


                //
                // => Validate the XBRL package
                //
                var dictResponse = new Dictionary<string, string>();

                try
                {
                    using (HttpClient _httpClient = new HttpClient())
                    {
                        _httpClient.Timeout = TimeSpan.FromMinutes(5);

                        using (FileStream filestream = File.OpenRead(xbrlFileToValidate))
                        {
                            using (StreamContent content = new StreamContent(filestream))
                            {
                                content.Headers.ContentType = contentType;

                                using (HttpResponseMessage result = await _httpClient.PostAsync(url, content))
                                {
                                    if (result.IsSuccessStatusCode)
                                    {
                                        // {"log":"/arelle/esef-2f7700a2-0e9e01aa/log","ixbrl":"/arelle/esef-2f7700a2-0e9e01aa/ixbrl","id":"esef-2f7700a2-0e9e01aa"}

                                        // Retrieve the JSON response string
                                        var jsonResonse = await result.Content.ReadAsStringAsync();

                                        // Convert to a dictionary so we can use it's values as a key/value pair
                                        var objectValues = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResonse);
                                        var stringValues = objectValues.Select(o => new KeyValuePair<string, string>(o.Key, o.Value?.ToString()));
                                        dictResponse = stringValues.ToDictionary(pair => pair.Key, pair => pair.Value);
                                    }
                                    else
                                    {
                                        var errorContent = $"{result.ReasonPhrase}: ";
                                        errorContent += await result.Content.ReadAsStringAsync();
                                        var errorMessage = "There was an error validating the XBRL package";
                                        var errorDebugInfo = $"url: {url}, HTTP Status Code: {result.StatusCode}, client-response: {errorContent}, incoming-request: {RenderHttpRequestDebugInformation()}";
                                        appLogger.LogError($"{errorMessage}, {errorDebugInfo}");
                                        return new TaxxorReturnMessage(false, "There was an error validating the XBRL package", errorDebugInfo);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Unable to generate Arelle validation package at {url}");
                    await _outputGenerationProgressMessage($"WARNING: iXBRL validation package generation failed, please notify the system administrator");
                }



                //
                // => Render the SEC EIS file
                //
                if (xbrlConversionProperties.RegulatorId == "sec")
                {
                    // - EIS file location (including a timestamp)
                    var eisFilePathOs = xbrlConversionProperties.XbrlPackagePathOs.Replace(".zip", ".eis");
                    eisFilePathOs = $"{Path.GetDirectoryName(xbrlConversionProperties.XbrlPackagePathOs)}/{createIsoFilenameTimestamp(eisFilePathOs, false, true, "_", xbrlConversionProperties.ClientServerOffsetMinutes)}";


                    // - URL to use for the request
                    var uriXbrlService = GetServiceUrl(ConnectedServiceEnum.XbrlService);
                    var queryStringDictionary = new Dictionary<string, string>
                    {
                        { "test", "true" },
                        { "period", xbrlConversionProperties.IsoTimestampPeriodEndDate }
                    };
                    if (!string.IsNullOrEmpty(xbrlConversionProperties.SubmissionType))
                    {
                        queryStringDictionary.Add("submissionType", xbrlConversionProperties.SubmissionType);
                    }
                    var queryBuilder = new QueryBuilder(queryStringDictionary);
                    string urlXbrlServiceApi = $"{uriXbrlService}/api/xbrl/sec/eis{queryBuilder.ToQueryString()}";
                    appLogger.LogInformation($"Starting EIS file generation with {urlXbrlServiceApi}");

                    try
                    {
                        using (HttpClient _httpClient = new HttpClient())
                        {
                            _httpClient.Timeout = TimeSpan.FromMinutes(5);

                            using (FileStream filestream = File.OpenRead(xbrlConversionProperties.XbrlPackagePathOs))
                            {
                                using (StreamContent content = new StreamContent(filestream))
                                {
                                    content.Headers.ContentType = new MediaTypeHeaderValue(System.Net.Mime.MediaTypeNames.Application.Zip);

                                    using (HttpResponseMessage result = await _httpClient.PostAsync(urlXbrlServiceApi, content))
                                    {
                                        if (result.IsSuccessStatusCode)
                                        {
                                            // Retrieve the JSON response string
                                            var jsonResonse = await result.Content.ReadAsStringAsync();

                                            // Convert to dynamic object
                                            dynamic? json = JsonConvert.DeserializeObject<ExpandoObject>(jsonResonse, new ExpandoObjectConverter());

                                            // Convert the base64 representation of the EIS file contents into a binary and save it
                                            try
                                            {
                                                if (File.Exists(eisFilePathOs)) File.Delete(eisFilePathOs);
                                                await File.WriteAllBytesAsync(eisFilePathOs, Base64DecodeToBytes(json.payload.content));
                                            }
                                            catch (Exception ex)
                                            {
                                                appLogger.LogError(ex, $"Payload not available in XBRL service response");
                                            }
                                        }
                                        else
                                        {
                                            var errorContent = $"{result.ReasonPhrase}: ";
                                            errorContent += await result.Content.ReadAsStringAsync();
                                            var errorMessage = "There was an error generating the EIS file";
                                            var errorDebugInfo = $"url: {urlXbrlServiceApi}, HTTP Status Code: {result.StatusCode}, client-response: {errorContent}, incoming-request: {RenderHttpRequestDebugInformation()}";
                                            appLogger.LogError($"{errorMessage}, {errorDebugInfo}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Unable to generate EIS file at {urlXbrlServiceApi}");
                        await _outputGenerationProgressMessage($"WARNING: EIS file generation failed, please notify the system administrator");
                    }


                }

                //
                // => Store the key value pairs with URL's returned by the service
                //
                xbrlConversionProperties.XbrlValidationLinks = dictResponse;

                //
                // => Generate an HTML information file
                //
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
                        baseDomainName = $"{protocol}://{currentDomainName}";
                        break;
                }

                // - retrieve the base HTML for the base package HTML
                var xmlPackageInformation = RetrieveTemplate("xbrlinfo_html");

                // - package content
                var listRoot = xmlPackageInformation.SelectSingleNode("/html/body//ul[@class='package-list']");
                if (listRoot != null)
                {
                    AppendListItem(listRoot, $"{Path.GetFileName(xbrlConversionProperties.XbrlPackagePathOs)}<br/>iXBRL package containing all the XBRL document that need to be submitted to the regulator.");
                    if (xbrlConversionProperties.RegulatorId == "sec")
                    {
                        AppendListItem(listRoot, $"{Path.GetFileNameWithoutExtension(xbrlConversionProperties.XbrlPackagePathOs)}.eis<br/>EDGAR package file which can be used to upload into the SEC EDGAR website and pre-fill the submission form with the filing contents.");
                    }
                    if (xbrlConversionProperties.IncludeXbrlGeneratorLogs) AppendListItem(listRoot, $"/.system<br/>Folder containing log information that was captured during the XBRL generation process in Taxxor DM. To be used for debugging purposes only.");
                }
                else
                {
                    appLogger.LogWarning("Contents list root cannot be found in the template");
                }

                // - link to viewer
                var linkViewer = xmlPackageInformation.SelectSingleNode("/html/body//a[@class='xbrlviewer-link']");
                if (linkViewer != null)
                {
                    var linkHref = RenderValidatorUri("ixbrl");
                    if (linkHref != null)
                    {
                        linkViewer.SetAttribute("href", linkHref);
                    }
                    else
                    {
                        var message = "Inline XBRL viewer report was not generated.";
                        appLogger.LogWarning(message);
                        linkViewer.ParentNode.InnerText = message;
                    }
                }
                else
                {
                    appLogger.LogWarning("Inline XBRL viewer link cannot be found in the template");
                }

                // - links to validation results
                listRoot = xmlPackageInformation.SelectSingleNode("/html/body//ul[@class='validation-list']");
                foreach (var item in dictResponse)
                {
                    switch (item.Key)
                    {
                        case "log":
                            AppendListItem(listRoot, $"<a href='{RenderValidatorUri(item.Key)}' target='_blank'>Validation details</a><br/>XML file containing details about the XBRL verification steps and potential errors and warnings.");
                            break;
                        case "report":
                            AppendListItem(listRoot, $"<a href='{RenderValidatorUri(item.Key)}' target='_blank'>Interactive XBRL view</a><br/>Interactive XBRL report containing details about the XBRL informaton which can be extracted from the inline XBRL report.");
                            break;
                        case "excel":
                            AppendListItem(listRoot, $"<a href='{RenderValidatorUri(item.Key)}' target='_blank'>Excel download</a><br/>All XBRL information extracted from the inline XBRL report.");
                            break;
                    }
                }

                // - Other informaton
                var nodeSha1Hash = xmlPackageInformation.SelectSingleNode("/html/body//span[@class='package-hash']");
                if (nodeSha1Hash != null)
                {
                    var packageChecksum = BytesToString(GetHashSha256(xbrlConversionProperties.XbrlPackagePathOs));
                    nodeSha1Hash.InnerText = packageChecksum;
                }
                else
                {
                    appLogger.LogWarning("SHA hash node cannot be found in the template");
                }

                var nodeValidationId = xmlPackageInformation.SelectSingleNode("/html/body//span[@class='validation-id']");
                if (nodeSha1Hash != null)
                {
                    nodeValidationId.InnerText = (dictResponse.ContainsKey("id")) ? dictResponse["id"] : "";
                }
                else
                {
                    appLogger.LogWarning("Validation ID node cannot be found in the template");
                }

                // - Store the file
                await xmlPackageInformation.SaveAsync($"{Path.GetDirectoryName(xbrlConversionProperties.XbrlPackagePathOs)}/README.html");


                //
                // => Helper methods
                //
                string? RenderValidatorUri(string dictKey)
                {
                    string? uri = null;
                    if (dictResponse.ContainsKey(dictKey))
                    {
                        uri = $"{((siteType == "local") ? uriArelleService : baseDomainName + folderPrefix)}{dictResponse[dictKey]}";
                    }

                    return uri;
                }

                void AppendListItem(XmlNode nodeListRoot, string listItemContent)
                {
                    var nodeLi = xmlPackageInformation.CreateElement("li");
                    nodeLi.InnerXml = listItemContent;
                    nodeListRoot.AppendChild(nodeLi);
                }

                byte[] GetHashSha256(string filename)
                {
                    // The cryptographic service provider.
                    SHA256 Sha256 = SHA256.Create();
                    using (FileStream stream = File.OpenRead(filename))
                    {
                        return Sha256.ComputeHash(stream);
                    }
                }

                string BytesToString(byte[] bytes)
                {
                    string result = "";
                    foreach (byte b in bytes) result += b.ToString("x2");
                    return result;
                }



            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error generating the XBRL validation package", ex);
            }

            // Return success
            return new TaxxorReturnMessage(true, $"Successfully created the validation package", "");
        }


        /// <summary>
        /// Sends an IXBRL zip file to the Arelle validation service and stores the response as a ZIP file on the disk
        /// </summary>
        /// <param name="pathOsXbrlPackageToValidate">Location of ZIP file containing the XBRL files to validate</param>
        /// <param name="pathOsEdgarValidationPackage">Location where the result of the Arelle validation needs to be stored as a ZIP file</param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RenderArelleValidationPackage(string pathOsXbrlPackageToValidate, string pathOsEdgarValidationPackage)
        {

            // - URL to use for the request
            var uriArelleService = GetServiceUrl(ConnectedServiceEnum.EdgarArelleService);
            string url = $"{uriArelleService}/rest/xbrl/validation?disclosure-system=efm-extended-pragmatic-all-years&media=zip&plugins=EdgarRenderer&logFile=log.xml";
            var basicErrorMessage = "Something went wrong retrieving the Arelle validation package";

            try
            {
                using (HttpClient _httpClient = new HttpClient())
                {
                    _httpClient.Timeout = TimeSpan.FromMinutes(2);

                    using (FileStream filestream = File.OpenRead(pathOsXbrlPackageToValidate))
                    {
                        using (StreamContent content = new StreamContent(filestream))
                        {
                            content.Headers.ContentType = new MediaTypeHeaderValue(System.Net.Mime.MediaTypeNames.Application.Zip);

                            using (HttpResponseMessage result = await _httpClient.PostAsync(url, content))
                            {
                                if (result.IsSuccessStatusCode)
                                {
                                    using (Stream output = File.OpenWrite(pathOsEdgarValidationPackage))
                                    using (Stream input = await result.Content.ReadAsStreamAsync())
                                    {
                                        input.CopyTo(output);
                                    }
                                }
                                else
                                {
                                    var errorContent = $"{result.ReasonPhrase}: ";
                                    errorContent += await result.Content.ReadAsStringAsync();
                                    return new TaxxorReturnMessage(false, basicErrorMessage, $"url: {url}, HTTP Status Code: {result.StatusCode}, client-response: {errorContent}, incoming-request: {RenderHttpRequestDebugInformation()}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, basicErrorMessage, $"error: {ex}, url: {url}");
            }



            return new TaxxorReturnMessage(true, "Successfully rendered EDGAR package");
        }


        /// <summary>
        /// Generates EDGAR HTML from the Inline XBRL information
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> InlineXbrlToEdgar(XbrlConversionProperties xbrlConversionProperties, bool debugRoutine = false)
        {
            // Please the compiler
            await DummyAwaiter();

            // Transfer the XML document that we want to work with into a local variable
            var xmlXbrl = new XmlDocument();
            xmlXbrl.LoadXml(xbrlConversionProperties.XmlXbrl.OuterXml);

            // Run the XmlDocument through the stylesheet
            xmlXbrl = TransformXmlToDocument(xmlXbrl, "sec_edgar");

            // Run the exhibits through the same stylesheet
            foreach (var pair in xbrlConversionProperties.Exhibits)
            {
                var exhibit = pair.Value;
                var xmlExhibit = new XmlDocument();
                xmlExhibit.LoadXml(exhibit.Xml.OuterXml);



                exhibit.Xml.LoadXml(TransformXml(xmlExhibit, "sec_edgar"));
            }

            //
            // => Store the processed XML file back into the properties object that we are using
            //
            try
            {
                xbrlConversionProperties.XmlXbrl.LoadXml(xmlXbrl.OuterXml);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error loading the post processed XML document back into the object", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }


            // Return success
            return new TaxxorReturnMessage(true, $"Successfully created {xbrlConversionProperties.ReportRequirementScheme} assets", "");
        }

        /// <summary>
        /// Generates the EDGAR package
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CompileEdgarPackage(XbrlConversionProperties xbrlConversionProperties, bool debugRoutine = false)
        {
            var generateEisFile = false;

            // Copy all the visuals
            try
            {
                CopyDirectoryRecursive(xbrlConversionProperties.WorkingVisualsFolderPathOs, xbrlConversionProperties.OutputEdgarFolderPathOs);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, $"Could not compile EDGAR package", $"error: {ex}");
            }


            // Clean the exhibits
            foreach (var pair in xbrlConversionProperties.Exhibits)
            {
                var exhibit = pair.Value;
                var xmlExhibit = new XmlDocument();
                xmlExhibit.LoadXml(exhibit.Xml.OuterXml);

                var cleanAttributesResultExhibits = RemoveSteeringAttributes(xmlExhibit, xbrlConversionProperties, debugRoutine);
                await _handleProcessStepResult(xbrlConversionProperties, cleanAttributesResultExhibits);
                xmlExhibit.LoadXml(cleanAttributesResultExhibits.XmlPayload.OuterXml);

                exhibit.Xml.LoadXml(xmlExhibit.OuterXml);
            }

            // Store the exhibits for the EDGAR filing
            await xbrlConversionProperties.SaveExhibits(xbrlConversionProperties.OutputEdgarFolderPathOs, true);

            // Store the exhibits for the iXBRL filing
            await xbrlConversionProperties.SaveExhibits(xbrlConversionProperties.OutputIxbrlFolderPathOs, true);

            // Grab the Inline XBRL document that we have been preparing      
            var xmlXbrl = new XmlDocument();
            xmlXbrl.LoadXml(xbrlConversionProperties.XmlXbrl.OuterXml);

            // Clean the attributes
            var cleanAttributesResult = RemoveSteeringAttributes(xmlXbrl, xbrlConversionProperties, debugRoutine);
            await _handleProcessStepResult(xbrlConversionProperties, cleanAttributesResult);
            xmlXbrl.LoadXml(cleanAttributesResult.XmlPayload.OuterXml);

            // Construct a filename for the document
            var mainDocumentFileName = $"{xbrlConversionProperties.Prefix}-{xbrlConversionProperties.ReportingYear}1231.htm";
            switch (xbrlConversionProperties.ReportRequirementScheme)
            {
                case "SEC6K":
                    // Use the publication date if available
                    mainDocumentFileName = $"{xbrlConversionProperties.Prefix}-{xbrlConversionProperties.IsoTimestampPeriodEndDate.Replace("-", "")}.htm";
                    break;
            }

            // Save the document (and make sure that we render numeric entities)
            var edgarHtmlFilePathOs = $"{xbrlConversionProperties.OutputEdgarFolderPathOs}/{mainDocumentFileName}";

            using (var stream = File.Create(edgarHtmlFilePathOs))
            {
                using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings()
                {
                    OmitXmlDeclaration = true,
                    Encoding = Encoding.ASCII,
                    Indent = true,
                    IndentChars = (" ")
                }))
                {
                    xmlXbrl.Save(writer);
                }
            }

            // Make sure that the main document is HTML compliant
            await _convertXmlToHtml(edgarHtmlFilePathOs);

            // Generate an EIS file
            if (generateEisFile)
            {
                // - EIS file location (including a timestamp)
                var eisFilePathOs = xbrlConversionProperties.OutputEdgarFolderPathOs;
                eisFilePathOs = $"{eisFilePathOs}/{createIsoFilenameTimestamp("edgar.eis", false, true, "_", xbrlConversionProperties.ClientServerOffsetMinutes)}";

                // - ZIP file that contains all the contents of the EIS file
                var zipPathOs = $"{xbrlConversionProperties.OutputEdgarFolderPathOs}/{createIsoFilenameTimestamp("edgar.zip", false, true, "_", xbrlConversionProperties.ClientServerOffsetMinutes)}";
                DirectoryInfo di = new DirectoryInfo(xbrlConversionProperties.OutputEdgarFolderPathOs);
                var files = di.GetFiles();
                using (var stream = File.OpenWrite(zipPathOs))
                using (ZipArchive archive = new ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
                {
                    foreach (var item in files)
                    {
                        archive.CreateEntryFromFile(item.FullName, item.Name, CompressionLevel.Optimal);
                    }
                }

                // - URL to use for the request
                var uriXbrlService = GetServiceUrl(ConnectedServiceEnum.XbrlService);
                var queryStringDictionary = new Dictionary<string, string>
                    {
                        { "test", "true" },
                        { "period", xbrlConversionProperties.IsoTimestampPeriodEndDate }
                    };
                if (!string.IsNullOrEmpty(xbrlConversionProperties.SubmissionType))
                {
                    queryStringDictionary.Add("submissionType", xbrlConversionProperties.SubmissionType);
                }
                var queryBuilder = new QueryBuilder(queryStringDictionary);
                string urlXbrlServiceApi = $"{uriXbrlService}/api/xbrl/sec/eis{queryBuilder.ToQueryString()}";
                appLogger.LogInformation($"Starting EIS file generation with {urlXbrlServiceApi}");

                try
                {
                    using (HttpClient _httpClient = new HttpClient())
                    {
                        _httpClient.Timeout = TimeSpan.FromMinutes(5);

                        using (FileStream filestream = File.OpenRead(zipPathOs))
                        {
                            using (StreamContent content = new StreamContent(filestream))
                            {
                                content.Headers.ContentType = new MediaTypeHeaderValue(System.Net.Mime.MediaTypeNames.Application.Zip);

                                using (HttpResponseMessage result = await _httpClient.PostAsync(urlXbrlServiceApi, content))
                                {
                                    if (result.IsSuccessStatusCode)
                                    {
                                        // Retrieve the JSON response string
                                        var jsonResonse = await result.Content.ReadAsStringAsync();

                                        // Convert to dynamic object
                                        dynamic? json = JsonConvert.DeserializeObject<ExpandoObject>(jsonResonse, new ExpandoObjectConverter());

                                        // Convert the base64 representation of the EIS file contents into a binary and save it
                                        try
                                        {
                                            if (File.Exists(eisFilePathOs)) File.Delete(eisFilePathOs);
                                            await File.WriteAllBytesAsync(eisFilePathOs, Base64DecodeToBytes(json.payload.content));
                                        }
                                        catch (Exception ex)
                                        {
                                            appLogger.LogError(ex, $"Payload not available in XBRL service response");
                                        }
                                    }
                                    else
                                    {
                                        var errorContent = $"{result.ReasonPhrase}: ";
                                        errorContent += await result.Content.ReadAsStringAsync();
                                        var errorMessage = "There was an error generating the EIS file";
                                        var errorDebugInfo = $"url: {urlXbrlServiceApi}, HTTP Status Code: {result.StatusCode}, client-response: {errorContent}, incoming-request: {RenderHttpRequestDebugInformation()}";
                                        appLogger.LogError($"{errorMessage}, {errorDebugInfo}");
                                    }
                                }
                            }
                        }
                    }

                    // Delete the ZIP file we used to generate the EIS file with
                    File.Delete(zipPathOs);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Unable to generate EIS file at {urlXbrlServiceApi}");
                    await _outputGenerationProgressMessage($"WARNING: EIS file generation failed, please notify the system administrator");
                }
            }


            // Return success
            return new TaxxorReturnMessage(true, $"Successfully compiled the {xbrlConversionProperties.ReportRequirementScheme} package", "");
        }


        /// <summary>
        /// Converts a full OS path to a path that we can use within a docker
        /// </summary>
        /// <param name="pathOs"></param>
        /// <returns></returns>
        public static string ConvertToDockerPath(string pathOs)
        {
            return RetrieveSharedFolderPathOs(true) + pathOs.Replace(RetrieveSharedFolderPathOs(), "");
        }

        /// <summary>
        /// Runs through the XmlDocument and makes sure that the ID's used do not start with a number because that is not conform the XHTML spec
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        public static XmlDocument CreateXhtmlCompliantIds(XmlDocument xmlDoc)
        {
            var nodeListElementsWithIds = xmlDoc.SelectNodes("//*[@id]");

            foreach (XmlNode nodeWithId in nodeListElementsWithIds)
            {
                var existingId = GetAttribute(nodeWithId, "id");
                if (string.IsNullOrEmpty(existingId))
                {
                    RemoveAttribute(nodeWithId, "id");
                }
                else
                {
                    bool firstCharIsDigit = char.IsDigit(existingId[0]);
                    if (firstCharIsDigit)
                    {
                        var newId = $"tx{existingId}";

                        // Find links to elements with this ID
                        var nodeListLinks = xmlDoc.SelectNodes($"//a[@href = '#{existingId}']");
                        foreach (XmlNode nodeLink in nodeListLinks)
                        {
                            SetAttribute(nodeLink, "href", $"#{newId}");
                        }

                        SetAttribute(nodeWithId, "id", newId);
                    }
                }
            }

            return xmlDoc;
        }

        /// <summary>
        /// Converts an XHTML file to plain HTML
        /// </summary>
        /// <param name="pathOs"></param>
        private static async Task _convertXmlToHtml(string pathOs)
        {
            // This is vey dirty....
            var xhtml = await RetrieveTextFile(pathOs);
            var html = xhtml.Replace(" />", ">").Replace("/>", ">");
            await TextFileCreateAsync(html, pathOs);
        }



        /// <summary>
        /// Handles the response of a (conversion) step
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="xmlStepResult"></param>
        /// <param name="logfileName"></param>
        private static async Task _handleProcessStepResult(XbrlConversionProperties xbrlConversionProperties, XmlDocument xmlStepResult, string logfileName = null)
        {
            await _handleProcessStepResult(xbrlConversionProperties, new TaxxorReturnMessage(xmlStepResult), logfileName);
        }

        /// <summary>
        /// Handles the response of a (conversion) step
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="processStepResult"></param>
        /// <param name="logfileName"></param>
        private static async Task _handleProcessStepResult(XbrlConversionProperties xbrlConversionProperties, TaxxorReturnMessage processStepResult, string logfileName = null)
        {
            // Send a websocket message
            try
            {
                await _outputGenerationProgressMessage(processStepResult.Message);
            }
            catch (Exception ex)
            {
                appLogger.LogWarning($"Could not send websocket message. error: {ex}");
            }

            if (!processStepResult.Success)
            {
                appLogger.LogError($"{processStepResult.Message}, debuginfo: {processStepResult.DebugInfo}");
                HandleError(processStepResult.Message, processStepResult.DebugInfo);
            }
            else
            {
                appLogger.LogInformation(processStepResult.Message);
            }

            // Generate an entry in the log
            if (logfileName != null)
            {
                xbrlConversionProperties.LogXmlXbrl(logfileName);
            }
        }

        /// <summary>
        /// Sends a websocket message about the progress of the process
        /// </summary>
        /// <param name="message"></param>
        /// <param name="step"></param>
        /// <param name="maxStep"></param>
        /// <returns></returns>
        public static async Task<int> _outputGenerationProgressMessage(string message, int step = -1, int maxStep = -1)
        {
            if (step == -1)
            {
                await MessageToCurrentClient("OutputGenerationProgress", message);
            }
            else
            {
                await MessageToCurrentClient("OutputGenerationProgress", $"Step {step}/{maxStep}: {message}");
                step++;
            }

            return step;
        }

        /// <summary>
        /// Extracts errors and warnings from the TaxxorReturnMessage and streams those to the websocket connection
        /// </summary>
        /// <param name="taxxorMessage"></param>
        /// <returns></returns>
        public static async Task _outputGenerationLogErrorAndWarnings(TaxxorReturnMessage taxxorMessage)
        {
            if (taxxorMessage.DebugInfo.Contains("<") && taxxorMessage.DebugInfo.Contains(">"))
            {
                try
                {
                    var xmlDebugInfo = new XmlDocument();
                    xmlDebugInfo.LoadXml(taxxorMessage.DebugInfo);
                    if (xmlDebugInfo.DocumentElement.LocalName == "debuginfo")
                    {
                        await _outputGenerationLogErrorAndWarnings(xmlDebugInfo);
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Unable to parse {taxxorMessage.DebugInfo} into an XmlDocument");
                }
            }
            else
            {
                if (taxxorMessage.XmlPayload != null)
                {
                    if (taxxorMessage.XmlPayload.DocumentElement.LocalName == "debuginfo")
                    {
                        await _outputGenerationLogErrorAndWarnings(taxxorMessage.XmlPayload);
                    }
                }
            }
        }

        /// <summary>
        /// Streams errors and warnings to the websocket connection
        /// </summary>
        /// <param name="xmlDebugInfo"></param>
        /// <returns></returns>
        public static async Task _outputGenerationLogErrorAndWarnings(XmlDocument xmlDebugInfo)
        {
            var nodeListWarnings = xmlDebugInfo.SelectNodes("/debuginfo/warnings/warning");
            foreach (XmlNode nodeWarning in nodeListWarnings)
            {
                await _outputGenerationProgressMessage($"WARNING: {nodeWarning.InnerText}");
            }

            var nodeListErrors = xmlDebugInfo.SelectNodes("/debuginfo/errors/error");
            foreach (XmlNode nodeError in nodeListErrors)
            {
                await _outputGenerationProgressMessage($"ERROR: {nodeError.InnerText}");
            }
        }

        /// <summary>
        /// Finds the path of an iXBRL instance document by looping through the files in a taxonomy package folder
        /// </summary>
        /// <param name="folderPathOs"></param>
        /// <returns></returns>
        public static string RetrieveInstanceDocumentPath(string folderPathOs)
        {
            string? inlineXbrlFilePathOs = null;
            if (Directory.Exists(folderPathOs))
            {
                // - Retrieve all the files from this XBRL taxonomy package
                var taxonomyPackageFiles = Directory.GetFiles(folderPathOs, "*.*", SearchOption.AllDirectories);

                // - Retrieve all the potential XBRL instance document files
                var instanceDocumentFound = false;
                var instanceDocumentCandidates = new List<string>();
                foreach (string pathOs in taxonomyPackageFiles)
                {
                    var extension = Path.GetExtension(pathOs);
                    if (extension == ".htm" || extension == ".html" || extension == ".xhtml" || extension == ".xbrl")
                    {
                        instanceDocumentCandidates.Add(pathOs);
                        if (extension == ".xbrl")
                        {
                            instanceDocumentFound = true;
                            inlineXbrlFilePathOs = pathOs;
                            break;
                        }
                    }
                }

                if (taxonomyPackageFiles.Length <= 2)
                {
                    instanceDocumentFound = taxonomyPackageFiles.Any(file => Path.GetExtension(file) == ".xbrl" || Path.GetExtension(file) == ".xhtml");
                    if (instanceDocumentFound)
                    {
                        inlineXbrlFilePathOs = taxonomyPackageFiles.FirstOrDefault(file => Path.GetExtension(file) == ".xbrl" || Path.GetExtension(file) == ".xhtml");
                    }
                }

                // - The correct instance document has an XSD file (the XBRL extension taxonomy) which has the same base file name as the instance document
                if (!instanceDocumentFound)
                {
                    foreach (string instanceDocumentCandidatePathOs in instanceDocumentCandidates)
                    {
                        var instanceDocumentFileName = Path.GetFileName(instanceDocumentCandidatePathOs);
                        var extensionTaxonomyFileName = Path.GetFileNameWithoutExtension(instanceDocumentCandidatePathOs) + ".xsd";
                        foreach (string pathOs in taxonomyPackageFiles)
                        {
                            var fileName = Path.GetFileName(pathOs);
                            if (fileName == extensionTaxonomyFileName)
                            {
                                inlineXbrlFilePathOs = instanceDocumentCandidatePathOs;
                                instanceDocumentFound = true;
                                break;
                            }
                        }

                        if (instanceDocumentFound) break;
                    }
                }


                // - In some rare cases, the XBRL engine can return an instance document without an associated XBRL extension taxonomy file
                if (!instanceDocumentFound)
                {
                    appLogger.LogWarning($"Unable to find an instance document with the same name as the extension taxonomy file!");
                    foreach (string instanceDocumentCandidatePathOs in instanceDocumentCandidates)
                    {
                        inlineXbrlFilePathOs = instanceDocumentCandidatePathOs;
                        instanceDocumentFound = true;
                        break;
                    }
                }
            }
            return inlineXbrlFilePathOs;
        }

        /// <summary>
        /// Generates a normalized asset name for the (XBRL) package
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        public static string _normalizedAssetName(string type, string assetPath, int counter = -1)
        {
            var prefix = "visual";
            switch (type)
            {
                case "image":

                    break;
                case "svg":
                    prefix = $"{prefix}drawing";
                    break;
                case "chart":
                case "graph":
                    prefix = $"{prefix}graph";
                    break;
                default:
                    appLogger.LogWarning($"Could not match asset type {type}");
                    break;
            }
            var suffix = "";
            if (counter == -1)
            {
                // Calculate a short hash
                suffix = String.Format("{0:X}", assetPath.GetHashCode()).ToLower();
            }
            else
            {
                if (counter < 10)
                {
                    suffix = $"000{counter.ToString()}";
                }
                else if (counter < 100)
                {
                    suffix = $"00{counter.ToString()}";
                }
                else if (counter < 1000)
                {
                    suffix = $"0{counter.ToString()}";
                }
                else if (counter < 10000)
                {
                    suffix = $"{counter.ToString()}";
                }
                else
                {
                    suffix = counter.ToString();
                }
            }
            var extension = Path.GetExtension(assetPath);
            if (extension == ".svg" || type == "chart" || type == "graph")
            {
                extension = ".png";
            }

            // Return the asset name
            return $"{prefix}{suffix}{extension}";
        }

        /// <summary>
        /// Creates a timestamp for the end date of a publication
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <returns></returns>
        public static string CreateIsoTimestampPublicationDate(XbrlConversionProperties xbrlConversionProperties)
        {

            var dateForFilename = createIsoTimestamp(true);
            // Use the publication date if available
            var nodeProjectConfig = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{xbrlConversionProperties.XbrlVars.projectId}']");
            if (nodeProjectConfig == null)
            {
                appLogger.LogWarning($"Could not locate project configuration section. stack-trace: {GetStackTrace()}");
            }
            else
            {
                // Retrieve the dates that we can work with
                var publicationDate = nodeProjectConfig.GetAttribute("date-publication");
                var reportingPeriod = nodeProjectConfig.SelectSingleNode("reporting_period")?.InnerText ?? "unknown";
                var quarter = RegExpReplace(@"^(..)..$", reportingPeriod, "$1");
                if (!string.IsNullOrEmpty(publicationDate) && quarter != "ar" && !quarter.StartsWith("q"))
                {
                    dateForFilename = createIsoTimestamp(publicationDate, true);
                }
                else
                {
                    if (!string.IsNullOrEmpty(reportingPeriod))
                    {
                        var dateEnd = $"{xbrlConversionProperties.ReportingYear}-";


                        switch (quarter)
                        {
                            case "q1":
                                dateEnd += "03-31";
                                break;

                            case "q2":
                                dateEnd += "06-30";
                                break;

                            case "q3":
                                dateEnd += "09-30";
                                break;

                            case "q4":
                            case "ar":
                                dateEnd += "12-31";
                                break;

                            default:
                                appLogger.LogError($"Could not convert quarter: '{quarter}' to start and end dates for the SEC file name");
                                return dateForFilename;
                        }

                        dateForFilename = dateEnd;
                    }
                }
            }

            return dateForFilename;
        }



    }
}