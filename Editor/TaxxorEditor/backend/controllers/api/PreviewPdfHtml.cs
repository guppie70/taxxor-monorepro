using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    /// <summary>
    /// Used for previewing HTML that will render the PDF file
    /// </summary>

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Special route for rendering the paged-media HTML in the editor
        /// Works in conjunction with the PagedMediaContentCache to retrieve the data
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RenderPagedMediaHtml(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var previewHtml = "";


            // Retrieve posted variables
            projectVars.editorContentType = request.RetrievePostedValue("ctype", @"^(regular)$", false, reqVars.returnType, "regular");
            var previewMode = request.RetrievePostedValue("mode", @"^(diff|preview|normal|nochrome)$", false, reqVars.returnType, "normal");
            var renderScope = request.RetrievePostedValue("renderscope", "top-level", RegexEnum.Default, reqVars.returnType); // single-section | include-children | top-level
            var layout = request.RetrievePostedValue("layout", "regular", RegexEnum.Default, reqVars.returnType);
            var bypassCacheString = request.RetrievePostedValue("nocache", RegexEnum.Boolean, false, reqVars.returnType, "false");
            var bypassCache = bypassCacheString == "true" || siteType == "local";
            var useContentStatusString = request.RetrievePostedValue("usecontentstatus", "(true|false)", false, reqVars.returnType, "false");

            var hideCurrentPeriodDatapointsString = request.RetrievePostedValue("hidecurrentperioddatapoints", "(true|false)", false, reqVars.returnType, "false");
            var hideCurrentPeriodDatapoints = hideCurrentPeriodDatapointsString == "true";

            var useContentStatus = useContentStatusString == "true";

            // For debugging purposes
            // bypassCache = false;


            // Setup the PDF XSLT cache
            SetupPdfStylesheetCache(reqVars);

            // Load the hierarchy
            var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);
            var xmlOutputChannelHierarchy = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;

            // Figure out the level 1 item that we need to optionally render
            var didToUseForRendering = projectVars.did;
            if (renderScope == "top-level")
            {
                // Check if we are trying to render the full PDF
                if (xmlOutputChannelHierarchy.SelectSingleNode($"/items/structured/item[@id='{projectVars.did}']") != null)
                {
                    // Full PDF requires to use 'all'
                    didToUseForRendering = "all";
                }
                else
                {
                    var xPathTopLevel = $"/items/structured/item/sub_items/item[@id='{projectVars.did}' or sub_items/item[descendant-or-self::item/@id='{projectVars.did}']]";

                    var nodeLevel1 = xmlOutputChannelHierarchy.SelectSingleNode(xPathTopLevel);
                    if (nodeLevel1 == null)
                    {
                        var errorMessage = new TaxxorReturnMessage(false, "Unable to find paged media html to return", $"nodeLevel1 not found, xPathTopLevel: {xPathTopLevel}, stack-trace: {GetStackTrace()}");
                        HandleError(errorMessage);
                    }
                    else
                    {
                        didToUseForRendering = nodeLevel1.GetAttribute("id");
                        if (string.IsNullOrEmpty(didToUseForRendering))
                        {
                            var errorMessage = new TaxxorReturnMessage(false, "Unable to find paged media html to return", $"item id not found, xPathTopLevel: {xPathTopLevel}, stack-trace: {GetStackTrace()}");
                            HandleError(errorMessage);
                        }
                    }
                }
            }

            // Try to retrieve the value from the cache
            if (!bypassCache)
            {
                previewHtml = ContentCache.RetrieveContent(projectVars, didToUseForRendering);
            }
            else
            {
                appLogger.LogInformation($"!! Content cache is bypassed for did: {didToUseForRendering}, pid: {projectVars.projectId} !!");
            }

            // When we receive no response, then retrieve fresh content from the cache
            if (string.IsNullOrEmpty(previewHtml))
            {
                previewHtml = await RetrieveLatestPdfHtml(projectVars.projectId, projectVars.versionId, didToUseForRendering, useContentStatus, hideCurrentPeriodDatapoints, (renderScope == "top-level") ? "include-children" : renderScope);

                // Post process the PDF HTML
                var pdfProperties = new PdfProperties();
                pdfProperties.Sections = didToUseForRendering;
                pdfProperties.Mode = previewMode; // diff|preview|normal|nochrome
                pdfProperties.HideCurrentPeriodDatapoints = hideCurrentPeriodDatapoints;
                var pdfPostProcessResult = await _PostProcessPdfHtml(previewHtml, pdfProperties, reqVars, projectVars);
                if (!pdfPostProcessResult.Success) HandleError(pdfPostProcessResult);

                if (siteType == "local") await TextFileCreateAsync(pdfPostProcessResult.Payload, $"{logRootPathOs}/pagedmediacontent.html");

                // Store the content in the cache
                ContentCache.SetContent(projectVars, didToUseForRendering, pdfPostProcessResult.Payload);

                // Prepare the response to the browser
                pdfPostProcessResult.Message = "Retrieved content from Project Data Store";
                pdfPostProcessResult.DebugInfo = "newrequest";


                // DumpReturnType(reqVars.returnType);

                // Render the response for the browser
                await response.OK(pdfPostProcessResult, ReturnTypeEnum.Json, true);
            }
            else
            {
                var cacheResult = new TaxxorReturnMessage(true, "Retrieved content from cache", previewHtml, "cacheresponse");

                // DumpReturnType(reqVars.returnType);

                // Render the response for the browser
                await response.OK(cacheResult, ReturnTypeEnum.Json, true);
            }


            // void DumpReturnType(ReturnTypeEnum returnType)
            // {
            //     Console.WriteLine("########## Paged Media Response ############");
            //     Console.WriteLine(ReturnTypeEnumToString(returnType));
            //     Console.WriteLine("############################################");
            // }

        }



        /// <summary>
        /// Renders the HTML preview page which is used as a basis for the PDF generator
        /// </summary>
        public async static Task RenderPdfHtml(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var previewHtml = "";


            // Retrieve posted variables
            projectVars.editorContentType = request.RetrievePostedValue("ctype", @"^(regular)$", false, reqVars.returnType, "regular");
            var previewMode = request.RetrievePostedValue("mode", @"^(diff|preview|normal|nochrome)$", false, reqVars.returnType, "normal");
            var renderScope = request.RetrievePostedValue("renderscope", "single-section", RegexEnum.Default, reqVars.returnType); // single-section | include-children | top-level
            var layout = request.RetrievePostedValue("layout", "regular", RegexEnum.Default, reqVars.returnType);
            var useContentStatusString = request.RetrievePostedValue("usecontentstatus", "(true|false)", false, reqVars.returnType, "false");
            var hideCurrentPeriodDatapointsString = request.RetrievePostedValue("hidecurrentperioddatapoints", "(true|false)", false, reqVars.returnType, "false");
            var hideCurrentPeriodDatapoints = hideCurrentPeriodDatapointsString == "true";
            var useContentStatus = useContentStatusString == "true";

            // Setup the PDF XSLT cache
            SetupPdfStylesheetCache(reqVars);

            if (previewMode == "diff")
            {
                // Retrieve base and latest parameter
                var baseVersion = context.Request.RetrievePostedValue("base", @"^(current|v\d+\.\d+)$", true, reqVars.returnType);
                var latestVersion = context.Request.RetrievePostedValue("latest", @"^(current|v\d+\.\d+)$", true, reqVars.returnType); // this can be "current" or "v1.1"          

                var htmlBaseVersion = "";
                var htmlLatestVersion = "";


                var pdfLanguage = "";
                var variantId = "";
                var nodeListOutputChannels = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(projectVars.editorId) + "]/output_channels/output_channel[@type='pdf' or @type='xbrl']");

                foreach (XmlNode nodeOutputChannel in nodeListOutputChannels)
                {
                    var nodeListOutputChannelVariants = nodeOutputChannel.SelectNodes("variants/variant");
                    foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                    {
                        pdfLanguage = GetAttribute(nodeVariant, "lang");
                        variantId = GetAttribute(nodeVariant, "id");
                    }
                }


                // 1) Retrieve the base HTML from the cache location in the Taxxor Data Store
                var htmlFileName = $"{variantId}-{pdfLanguage}.html";
                htmlBaseVersion = await DocumentStoreService.FilingData.Load<string>($"/system/cache/{baseVersion}/{htmlFileName}", "cmscontentroot", true);

                if (htmlBaseVersion.StartsWith("ERROR:", StringComparison.CurrentCulture))
                {
                    // Write the error to the output stream, write the error in the log file and stop any further processing
                    HandleError(reqVars.returnType, "Could not retrieve base document for comparison", $"Retrieved HTML: {TruncateString(htmlBaseVersion, 50)}, stack-trace: {GetStackTrace()}");
                }


                // 2) Retrieve latest HTML
                if (latestVersion == "current")
                {
                    // Retrieve the latest content using the previewer URL from the website
                    htmlLatestVersion = await RetrieveLatestPdfHtml(projectVars.projectId, projectVars.versionId, projectVars.did, useContentStatus, hideCurrentPeriodDatapoints, renderScope);
                }
                else
                {
                    htmlLatestVersion = await DocumentStoreService.FilingData.Load<string>($"/system/cache/{latestVersion}/{htmlFileName}", "cmscontentroot", true);

                    if (htmlLatestVersion.StartsWith("ERROR:", StringComparison.CurrentCulture))
                    {
                        // Write the error to the output stream, write the error in the log file and stop any further processing
                        HandleError(reqVars.returnType, "Could not retrieve latest document for comparison", $"Retrieved HTML: {TruncateString(htmlLatestVersion, 50)}, stack-trace: {GetStackTrace()}");
                    }

                }

                // Call the PDF Service to render the Diff HTML for us
                previewHtml = await PdfService.RenderDiffHtml(htmlBaseVersion, htmlLatestVersion);

                var xmlDiffHtml = new XmlDocument();
                xmlDiffHtml.LoadHtml(previewHtml);

                //
                // => Improve the track changes marks
                //
                PostProcessTrackChangesHtml(ref xmlDiffHtml);

                previewHtml = xmlDiffHtml.OuterXml;
            }
            else
            {
                // Normal preview mode -> show latest version
                previewHtml = await RetrieveLatestPdfHtml(projectVars.projectId, projectVars.versionId, projectVars.did, useContentStatus, hideCurrentPeriodDatapoints, renderScope);
            }

            // Post process the PDF HTML
            var pdfProperties = new PdfProperties();
            pdfProperties.Sections = projectVars.did;
            pdfProperties.Mode = previewMode;
            pdfProperties.HideCurrentPeriodDatapoints = hideCurrentPeriodDatapoints;
            var pdfPostProcessResult = await _PostProcessPdfHtml(previewHtml, pdfProperties, reqVars, projectVars);
            if (!pdfPostProcessResult.Success) HandleError(pdfPostProcessResult);

            // Render the response for the browser
            await context.Response.OK(pdfPostProcessResult.Payload, ReturnTypeEnum.Html, true);
        }


        /// <summary>
        /// Central function for post processing the PDF HTML we retrieve from the Project Data Store
        /// </summary>
        /// <param name="basicPdfHtml"></param>
        /// <param name="previewMode"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        private static async Task<TaxxorReturnMessage> _PostProcessPdfHtml(string basicPdfHtml, PdfProperties pdfProperties, RequestVariables reqVars, ProjectVariables projectVars)
        {

            // RS: This should probably be a general function that can be called from multiple places.
            // Code is also in PdfGeneratorInterface.cs around line 163
            var debugRoutine = reqVars.isDebugMode == true || siteType == "local" || siteType == "dev";
            var outputChannelVariantId = projectVars.outputChannelVariantId;
            var outputChannelVariantLanguage = projectVars.outputChannelVariantLanguage;
            var booktitle = "";
            var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, "pdf", outputChannelVariantId, outputChannelVariantLanguage);
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
                        HandleError("Unable to render PDF", $"nodeRootSection: null, outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        booktitle = nodeRootSection.SelectSingleNode("web_page/linkname").InnerText;
                    }
                }
                else
                {
                    HandleError("Unable to render PDF", $"xmlHierarchyDoc: null, outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey}, stack-trace: {GetStackTrace()}");
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

            var xmlPreviewHtml = new XmlDocument();
            xmlPreviewHtml.LoadHtml(basicPdfHtml);


            //
            // => Prepare the current PDF HTML for processing
            //
            var pdfHtmlPreparationResult = await PreparePdfHtmlForRendering(xmlPreviewHtml, projectVars, pdfProperties);
            if (!pdfHtmlPreparationResult.Success)
            {
                return pdfHtmlPreparationResult;
            }

            //
            // => Render the complete HTML that we would normally send to the PDF generator
            //
            TaxxorReturnMessage pdfHtmlCompleteResult = await CreateCompletePdfHtml(pdfHtmlPreparationResult.XmlPayload.OuterXml, reqVars, projectVars, pdfProperties);
            if (!pdfHtmlCompleteResult.Success)
            {
                return pdfHtmlCompleteResult;
            }
            else
            {
                // In the Message property we have inserted the translated HTML
                basicPdfHtml = pdfHtmlCompleteResult.XmlPayload.OuterXml;
            }

            return new TaxxorReturnMessage(true, "Success", basicPdfHtml, "");
        }

    }
}