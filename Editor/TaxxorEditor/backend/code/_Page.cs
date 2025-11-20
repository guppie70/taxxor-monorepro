using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Taxxor.Project
{

    /// <summary>
    /// Generic utilities and tools commonly used in the web pages of this site/application
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        //render page components (page head, page body start, page body end, etc. here using functions

        /// <summary>
        /// Renders HTML content to be injected in the head of a CMS page
        /// </summary>
        /// <param name="pageId">Site structure page ID</param>
        /// <param name="basePageInterfaceElement">(optional) base user interface element to use</param>
        public static async Task<string> RenderPageHead(string pageId, string basePageInterfaceElement = "page-head_default")
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Determine if this is the master or slave language
            var masterLanguage = (projectVars.outputChannelDefaultLanguage == projectVars.outputChannelVariantLanguage);

            // Used for building up the HTML content
            StringBuilder sbHtml = new StringBuilder();

            // Retrieve the page head content
            switch (pageId)
            {
                default:
                    sbHtml.AppendLine(RetrieveInterfaceElement(basePageInterfaceElement, xmlApplicationConfiguration, reqVars));
                    break;
            }

            // Add page specific content
            switch (pageId)
            {
                case "cms-overview":
                case "cms_hierarchy-manager":
                case "cms_auditor-view":
                case "cms_version-manager":
                case "cms_administration-page":
                    sbHtml.AppendLine(RetrieveInterfaceElement($"page-head_{pageId}", xmlApplicationConfiguration, reqVars));
                    break;

                //the content editor page
                case "cms_content-editor":
                    var pageHeadElementId = "page-head_cms-editor-inline";
                    sbHtml.AppendLine(RetrieveInterfaceElement(pageHeadElementId, xmlApplicationConfiguration, reqVars));

                    //inject inline javascript
                    var xmlNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='cms_xsl_hierarchy-to-js-array']");
                    var xslPathOs = CalculateFullPathOs(xmlNode);

                    var javascriptContent = TransformXml(projectVars.cmsMetaData[RetrieveOutputChannelHierarchyMetadataId(projectVars)].Xml, xslPathOs);
                    sbHtml.Replace("[inline_javascript]", javascriptContent);


                    //inject data loader url
                    var urlDataLoader = RenderDataRetrievalUrl(projectVars.projectId, projectVars.versionId, "text", "[pageid]");
                    sbHtml.Replace("[dataproxy]", urlDataLoader);

                    //inject the data saver url
                    var urlDataSaver = RetrieveUrlFromHierarchy("u-cms_data-saver", xmlHierarchy);
                    sbHtml.Replace("[saveurl]", urlDataSaver);

                    break;

            }

            // Inject inline CSS which can be dynamically generated
            var sbInlineCss = new StringBuilder();
            sbHtml.Replace("[inline_css]", string.Join(";", sbInlineCss));

            // Add the API routes and the project preferences JSON
            var pathOs = CalculateFullPathOs(xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='cms_xsl_hierarchy-api-to-js-object']"));
            var projectPreferencesJson = await RenderProjectPreferencesJson(projectVars);
            sbHtml.AppendLine($"<script type=\"text/javascript\" nonce=\"{context.Items["nonce"]?.ToString() ?? ""}\">{TransformXml(reqVars.xmlHierarchyStripped, pathOs)}{projectPreferencesJson}</script>");

            // Replace standard placeholders
            var html = _ReplaceProjectSpecificPlaceholders(sbHtml.ToString());

            // Construct a reporting requirements object
            var jsReportingRequirements = "{}";
            if (!string.IsNullOrEmpty(projectVars.projectId))
            {
                var nodeListReportingRequirements = xmlApplicationConfiguration.SelectNodes($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/reporting_requirements/reporting_requirement");
                if (nodeListReportingRequirements.Count > 0)
                {
                    // Generate unique list of output channel ID's for which we need to compile a list of reporting requirements
                    var outputChannelVariantIds = new List<string>();
                    foreach (XmlNode nodeRequirement in nodeListReportingRequirements)
                    {
                        var outputChannelVariantId = GetAttribute(nodeRequirement, "ref-outputchannelvariant");
                        if (!string.IsNullOrEmpty(outputChannelVariantId))
                        {
                            if (!outputChannelVariantIds.Contains(outputChannelVariantId)) outputChannelVariantIds.Add(outputChannelVariantId);
                        }
                    }

                    var jsReportingRequirementsBuilder = new StringBuilder();
                    jsReportingRequirementsBuilder.Append('{');
                    for (int i = 0; i < outputChannelVariantIds.Count; i++)
                    {
                        var outputChannelVariantId = outputChannelVariantIds[i];
                        var nodeListRequirements = xmlApplicationConfiguration.SelectNodes($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/reporting_requirements/reporting_requirement[@ref-outputchannelvariant='{outputChannelVariantId}']");

                        jsReportingRequirementsBuilder.Append($"'{outputChannelVariantId}':[");

                        var processedTaxonomies = new List<string>();
                        for (int j = 0; j < nodeListRequirements.Count; j++)
                        {
                            XmlNode? nodeRequirement = nodeListRequirements.Item(j);
                            var scheme = GetAttribute(nodeRequirement, "ref-mappingservice") ?? "";
                            var name = nodeRequirement.SelectSingleNode("name")?.InnerText;
                            var taxonomy = GetAttribute(nodeRequirement, "ref-taxonomy") ?? "";

                            if (!string.IsNullOrEmpty(taxonomy))
                            {
                                if (!processedTaxonomies.Contains(taxonomy))
                                {
                                    processedTaxonomies.Add(taxonomy);
                                    jsReportingRequirementsBuilder.Append($"{{scheme: '{taxonomy}', taxonomy: '{taxonomy}', renderprofile: '{scheme}', name: '{name}'}}");
                                }
                            }
                            else
                            {
                                if (!processedTaxonomies.Contains(scheme))
                                {
                                    processedTaxonomies.Add(scheme);
                                    jsReportingRequirementsBuilder.Append($"{{scheme: '{scheme}', taxonomy: '{taxonomy}', renderprofile: '{scheme}', name: '{name}'}}");
                                }
                            }

                            if (j < nodeListRequirements.Count - 1) jsReportingRequirementsBuilder.Append(',');
                        }

                        jsReportingRequirementsBuilder.Append(']');
                        if (i < outputChannelVariantIds.Count - 1) jsReportingRequirementsBuilder.Append(',');
                    }
                    jsReportingRequirementsBuilder.Append('}');
                    jsReportingRequirements = jsReportingRequirementsBuilder.ToString();
                }
            }

            // 


            // Project variables for client-side usage
            dynamic projectVarsJs = new ExpandoObject();
            projectVarsJs.pid = projectVars.projectId;
            projectVarsJs.vid = projectVars.versionId;
            projectVarsJs.did = projectVars.did;
            projectVarsJs.ctype = projectVars.editorContentType;
            projectVarsJs.rtype = projectVars.reportTypeId;
            projectVarsJs.octype = projectVars.outputChannelType;
            projectVarsJs.ocvariantid = projectVars.outputChannelVariantId;
            projectVarsJs.oclang = projectVars.outputChannelVariantLanguage;
            projectVarsJs.appversion = applicationVersion.Replace(".", "");
            projectVarsJs.guidclient = projectVars.guidClient;
            projectVarsJs.guidentitygroup = projectVars.guidEntityGroup;
            projectVarsJs.guidlegalentity = projectVars.guidLegalEntity;
            projectVarsJs.currentuser = new ExpandoObject();
            projectVarsJs.currentuser.id = projectVars.currentUser?.Id;

            // Paths for the image and download managers
            var baseImagePath = $"/dataserviceassets/{projectVars.projectId}/images";
            var baseImagePlaceholderPath = "/dataserviceassets/{siteid}/images";
            var baseDownloadsPath = $"/dataserviceassets/{projectVars.projectId}/downloads";
            var baseDownloadsPlaceholderPath = "/dataserviceassets/{siteid}/downloads";

            if (projectVars.outputChannelType == "website")
            {
                var resultFileStructureDefinitionXpath = RetrieveFileStructureXpath(projectVars.editorId);
                if (resultFileStructureDefinitionXpath.Success)
                {
                    var nodeWebImagesLocation = xmlApplicationConfiguration.SelectSingleNode($"{resultFileStructureDefinitionXpath.Payload}[@content='images']");
                    if (nodeWebImagesLocation != null)
                    {
                        var basePath = nodeWebImagesLocation.GetAttribute("name");
                        if (!string.IsNullOrEmpty(basePath))
                        {
                            baseImagePlaceholderPath = Extensions.RenderWebsiteImagePlaceholderPath(basePath);
                        }
                    }

                    var nodeWebDownloadsLocation = xmlApplicationConfiguration.SelectSingleNode($"{resultFileStructureDefinitionXpath.Payload}[@content='downloads']");
                    if (nodeWebDownloadsLocation != null)
                    {
                        var basePath = nodeWebDownloadsLocation.GetAttribute("name");
                        if (!string.IsNullOrEmpty(basePath))
                        {
                            baseDownloadsPlaceholderPath = Extensions.RenderWebsiteDownloadPlaceholderPath(basePath.Replace("[language]", projectVars.outputChannelVariantLanguage));
                        }
                    }
                }
                else
                {
                    appLogger.LogWarning($"{resultFileStructureDefinitionXpath.Message}, debuginfo: {resultFileStructureDefinitionXpath.DebugInfo}, stack-trace: {GetStackTrace()}");
                }


            }

            dynamic fileManagerData = new ExpandoObject();
            fileManagerData.images = new ExpandoObject();
            fileManagerData.images.basepath = baseImagePath;
            fileManagerData.images.placeholderpath = baseImagePlaceholderPath;
            fileManagerData.downloads = new ExpandoObject();
            fileManagerData.downloads.basepath = baseDownloadsPath;
            fileManagerData.downloads.placeholderpath = baseDownloadsPlaceholderPath;

            // Output channel information
            dynamic outputChannels = new ExpandoObject();
            outputChannels.ids = new Dictionary<string, ExpandoObject>();
            if (!string.IsNullOrEmpty(projectVars.editorId))
            {
                var nodeListOutputChannelVariants = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels/output_channel/variants/variant");
                foreach (XmlNode nodeOutputChannelVariant in nodeListOutputChannelVariants)
                {
                    var ocVariantId = nodeOutputChannelVariant.GetAttribute("id");
                    dynamic outputChannelInfo = new ExpandoObject();
                    outputChannelInfo.name = nodeOutputChannelVariant.SelectSingleNode("name")?.InnerText ?? "unknown";
                    outputChannelInfo.type = nodeOutputChannelVariant.ParentNode.ParentNode.GetAttribute("type");
                    outputChannelInfo.defaultlayout = nodeOutputChannelVariant.GetAttribute("defaultlayout");
                    outputChannelInfo.forcedlayout = nodeOutputChannelVariant.GetAttribute("forcedlayout");
                    outputChannelInfo.lang = nodeOutputChannelVariant.GetAttribute("lang");
                    outputChannels.ids.Add(ocVariantId, outputChannelInfo);
                }
            }

            // Does this installation contain a EFR import service
            var hasErpImportCapabilities = xmlApplicationConfiguration.SelectSingleNode("/configuration/taxxor/components/service/services/service[@role='structureddatasource']") != null;

            // Datamanager information
            dynamic dataManagerProperties = new ExpandoObject();
            if (!string.IsNullOrEmpty(projectVars.projectId) && pageId == "cms_content-editor")
            {
                var reportingPeriodBaseMetadata = new ProjectPeriodProperties(projectVars.projectId);
                if (!reportingPeriodBaseMetadata.Success)
                {
                    appLogger.LogError($"Unable to parse the project period metadata for {projectVars.projectId}");
                }
                else
                {
                    dataManagerProperties.periodinfo = new ExpandoObject();
                    dataManagerProperties.periodinfo.year = reportingPeriodBaseMetadata.CurrentProjectYear.ToString();
                    dataManagerProperties.periodinfo.quarter = reportingPeriodBaseMetadata.CurrentProjectQuarter.ToString();
                    dataManagerProperties.periodinfo.month = reportingPeriodBaseMetadata.CurrentProjectMonth.ToString();
                    dataManagerProperties.periodinfo.start = reportingPeriodBaseMetadata.PeriodStart.ToString("yyyy-MM-dd");
                    dataManagerProperties.periodinfo.end = reportingPeriodBaseMetadata.PeriodEnd.ToString("yyyy-MM-dd");

                    //             struct Shortcut
                    // {
                    //     public string name { get; set; }
                    //     public string start { get; set; }

                    //     public string end { get; set; }
                    //     // Add other properties as needed
                    // }



                    dataManagerProperties.shortcuts = new Dictionary<string, ExpandoObject>();
                    dynamic? dateFragments = null;
                    switch (reportingPeriodBaseMetadata.ProjectType)
                    {
                        case "ar":
                            // This year
                            dateFragments = new ExpandoObject();
                            dateFragments.start = reportingPeriodBaseMetadata.PeriodStart.ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("This year", dateFragments);

                            // Year - 1
                            dateFragments = new ExpandoObject();
                            dateFragments.start = reportingPeriodBaseMetadata.PeriodStart.AddYears(-1).ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.AddYears(-1).ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("Year - 1", dateFragments);

                            // Year - 2
                            dateFragments = new ExpandoObject();
                            dateFragments.start = reportingPeriodBaseMetadata.PeriodStart.AddYears(-2).ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.AddYears(-2).ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("Year - 2", dateFragments);

                            break;

                        case "qr":

                            // This quarter
                            dateFragments = new ExpandoObject();
                            dateFragments.start = reportingPeriodBaseMetadata.PeriodStart.ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("This quarter", dateFragments);

                            // Quarter - 1
                            dateFragments = new ExpandoObject();
                            dateFragments.start = reportingPeriodBaseMetadata.PeriodStart.AddMonths(-3).ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.AddQuarters(-1).ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("Quarter - 1", dateFragments);


                            // YTD
                            var ytdProjectPeriodProperties = new ProjectPeriodProperties("ar" + reportingPeriodBaseMetadata.CurrentProjectYear.ToString().Substring(2));
                            dateFragments = new ExpandoObject();
                            dateFragments.start = ytdProjectPeriodProperties.PeriodStart.ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("YTD", dateFragments);

                            // YTD, year -1
                            ytdProjectPeriodProperties = new ProjectPeriodProperties("ar" + (reportingPeriodBaseMetadata.CurrentProjectYear - 1).ToString().Substring(2));
                            dateFragments = new ExpandoObject();
                            dateFragments.start = ytdProjectPeriodProperties.PeriodStart.ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.AddYears(-1).ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("YTD, year - 1", dateFragments);

                            break;

                        case "mr":

                            // This month
                            dateFragments = new ExpandoObject();
                            dateFragments.start = reportingPeriodBaseMetadata.PeriodStart.ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("This month", dateFragments);

                            // Month, year - 1
                            dateFragments = new ExpandoObject();
                            dateFragments.start = reportingPeriodBaseMetadata.PeriodStart.AddYears(-1).ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.AddYears(-1).ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("Month, year - 1", dateFragments);

                            // Month - 1, year - 1
                            dateFragments = new ExpandoObject();
                            dateFragments.start = reportingPeriodBaseMetadata.PeriodStart.AddMonths(-1).AddYears(-1).ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.AddMonths(-1).AddYears(-1).ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("Month - 1, year - 1", dateFragments);

                            // QTD
                            var startQuarterDate = reportingPeriodBaseMetadata.PeriodEnd.GetQuarterStartDate();
                            dateFragments = new ExpandoObject();
                            dateFragments.start = startQuarterDate.ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("QTD", dateFragments);

                            // QTD, year - 1
                            dateFragments = new ExpandoObject();
                            dateFragments.start = startQuarterDate.AddYears(-1).ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.AddYears(-1).ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("QTD, year - 1", dateFragments);

                            // YTD
                            var ytdProjectPeriodPropertiesNested = new ProjectPeriodProperties("ar" + reportingPeriodBaseMetadata.CurrentProjectYear.ToString().Substring(2));
                            dateFragments = new ExpandoObject();
                            dateFragments.start = ytdProjectPeriodPropertiesNested.PeriodStart.ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("YTD", dateFragments);

                            // YTD, year -1
                            ytdProjectPeriodPropertiesNested = new ProjectPeriodProperties("ar" + (reportingPeriodBaseMetadata.CurrentProjectYear - 1).ToString().Substring(2));
                            dateFragments = new ExpandoObject();
                            dateFragments.start = ytdProjectPeriodPropertiesNested.PeriodStart.ToString("yyyy-MM-dd");
                            dateFragments.end = reportingPeriodBaseMetadata.PeriodEnd.AddYears(-1).ToString("yyyy-MM-dd");
                            dataManagerProperties.shortcuts.Add("YTD, year - 1", dateFragments);


                            break;

                        default:
                            appLogger.LogInformation($"Project type {reportingPeriodBaseMetadata.ProjectType} not supported to create shortcuts for");
                            break;
                    }



                }
            }

            // Add default javascript variables to the head
            var scriptBuilder = new StringBuilder();

            scriptBuilder.Append("<script type=\"text/javascript\" nonce=\"")
                         .Append(context.Items["nonce"]?.ToString() ?? "")
                         .Append("\">var strPageId='")
                         .Append(pageId)
                         .Append("', strSiteType='")
                         .Append(siteType)
                         .Append("', strProjectId='")
                         .Append(projectVars.projectId)
                         .Append("', masterLanguage=")
                         .Append(masterLanguage.ToString().ToLower())
                         .Append(", strVersionId='")
                         .Append(projectVars.versionId)
                         .Append("', strReportTypeId='")
                         .Append(projectVars.reportTypeId)
                         .Append("', strEditorContentTypeId='")
                         .Append(projectVars.editorContentType)
                         .Append("', appVersion='")
                         .Append(applicationVersion)
                         .Append("', reportingPeriod='")
                         .Append(projectVars.reportingPeriod)
                         .Append("', taxxorClientId='")
                         .Append(TaxxorClientId)
                         .Append("', userEmail='")
                         .Append(projectVars.currentUser.Email)
                         .Append("', ")
                         .Append(projectVars.currentUser.Permissions.JavaScriptPermissions)
                         .Append(", reportingRequirements=")
                         .Append(jsReportingRequirements)
                         .Append(", projectStatus='")
                         .Append(projectVars.projectStatus ?? "")
                         .Append("', projectVars=")
                         .Append(JsonConvert.SerializeObject(projectVarsJs))
                         .Append(", filemanagerData=")
                         .Append(JsonConvert.SerializeObject(fileManagerData))
                         .Append(", pdfRenderEngine='")
                         .Append(PdfRenderEngine)
                         .Append("', outputChannelInfo=")
                         .Append(JsonConvert.SerializeObject(outputChannels))
                         .Append(", dataManagerProperties=")
                         .Append(JsonConvert.SerializeObject(dataManagerProperties))
                         .Append(", hasErpImportCapabilities=")
                         .Append(hasErpImportCapabilities.ToString().ToLower())
                         .Append(", uriStaticAssets='")
                         .Append(projectVars.uriStaticAssets)
                         .Append("', bootstrapVersion=$.fn.tooltip.Constructor.VERSION;")
                         .Append("</script>");

            html += scriptBuilder.ToString();

            // Allows implementation specific HTML to be injected
            html = Extensions.RenderPageHeadCustom(context, pageId, html);

            //pretty print the output so that it looks professional...
            //html = PrettyPrintXml("<foo>" + html + "</foo>").Replace("<foo>", "").Replace("</foo>", "").Replace("<!--|", "<!--[").Replace("|>", "]>");

            return html;

        }

        /// <summary>
        /// Injects HTML content right after the body tag in the CMS page
        /// </summary>
        /// <param name="pageId">Site structure page ID</param>
        /// <param name="basePageInterfaceElement">(optional) base user interface element to use</param>
        public static string RenderPageBodyStart(string pageId, string basePageInterfaceElement = "page-body-start_default")
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Determine if this is the master or slave language
            var masterLanguage = (projectVars.outputChannelDefaultLanguage == projectVars.outputChannelVariantLanguage);

            // Use a stringbuilder for creating the HTML we want to return
            var sbHtml = new StringBuilder();

            // Add a layer containing debug information
            if (context.Request.RetrievePostedValue("debug") == "true") sbHtml.AppendLine($"<div style=\"background-color: white; font-size: 11px; border: 1px solid #999; position: absolute; top: 60px; left: 200px; z-index: 999; padding: 5px; text-align: left\" class=\"debug_content\">{reqVars.debugContent}</div>");

            switch (pageId)
            {
                case "some-exclusive-change-for-a-page":
                    sbHtml.AppendLine(RetrieveInterfaceElement("page-body-start_editor", xmlApplicationConfiguration, reqVars));
                    break;

                default:
                    if (basePageInterfaceElement == "page-body-start_default")
                    {
                        // Use XSLT translation for creating the top menu
                        XsltArgumentList xsltArgs = new XsltArgumentList();
                        xsltArgs.AddParam("pageId", "", pageId);
                        xsltArgs.AddParam("doc-configuration", "", projectVars.xmlApplicationConfiguration);
                        if (!string.IsNullOrEmpty(projectVars.projectId))
                        {
                            xsltArgs.AddParam("projectId", "", projectVars.projectId);
                        }

                        if (!string.IsNullOrEmpty(projectVars.editorId))
                        {
                            xsltArgs.AddParam("editorId", "", projectVars.editorId);
                        }
                        else
                        {
                            var editorId = RetrieveEditorIdFromReportId(projectVars.reportTypeId);
                            if (!string.IsNullOrEmpty(editorId))
                            {
                                xsltArgs.AddParam("editorId", "", editorId);
                            }
                        }
                        if (!string.IsNullOrEmpty(projectVars.idFirstEditablePage))
                        {
                            xsltArgs.AddParam("idFirstEditablePage", "", projectVars.idFirstEditablePage);
                        }

                        sbHtml.AppendLine(TransformXml(reqVars.xmlHierarchyStripped, "cms_xsl_top-navigation", xsltArgs));
                    }
                    else
                    {
                        // Use predefined HTML from the configuration document
                        sbHtml.AppendLine(RetrieveInterfaceElement(basePageInterfaceElement, xmlApplicationConfiguration, reqVars));
                    }

                    break;
            }

            var html = _ReplaceProjectSpecificPlaceholders(sbHtml.ToString());

            // Inject custom html for this specific installation of the Taxxor Editor
            html = Extensions.RenderPageBodyStartCustom(context, pageId, html);

            html = PrettyPrintXml("<foo>" + html + "</foo>").Replace("<foo>", "").Replace("</foo>", "");

            return html;
        }

        /// <summary>
        /// Injects HTML content just before the body tag closes
        /// </summary>
        /// <param name="pageId">Site structure page ID</param>
        /// <param name="basePageInterfaceElement">(optional) base user interface element to use</param>
        public static string RenderPageBodyEnd(string pageId, string basePageInterfaceElement = "page-body-end")
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Use a stringbuilder for creating the HTML we want to return
            var sbHtml = new StringBuilder();

            // On all pages we want the info and warning modal dialog boxes
            sbHtml.AppendLine(RetrieveInterfaceElement("dialog_info", xmlApplicationConfiguration, reqVars));
            sbHtml.AppendLine(RetrieveInterfaceElement("dialog_warning", xmlApplicationConfiguration, reqVars));

            switch (pageId)
            {
                case "cms_snapshot-selector":
                case "cms_content-editor":
                    sbHtml.AppendLine(RetrieveInterfaceElement("dialog_saved", xmlApplicationConfiguration, reqVars));
                    sbHtml.AppendLine(RetrieveInterfaceElement("dialog_render-pdf", xmlApplicationConfiguration, reqVars));
                    sbHtml.AppendLine(RetrieveInterfaceElement("dialog_unsaved-changes", xmlApplicationConfiguration, reqVars));
                    break;

                case "cms_hierarchy-manager":
                    sbHtml.AppendLine(RetrieveInterfaceElement("dialog_saved", xmlApplicationConfiguration, reqVars));
                    sbHtml.AppendLine(RetrieveInterfaceElement("dialog_unsaved-changes", xmlApplicationConfiguration, reqVars));
                    break;
            }

            // Add the basic page end element
            sbHtml.AppendLine(RetrieveInterfaceElement(basePageInterfaceElement, xmlApplicationConfiguration, reqVars));

            //add the hidden "system" iframe
            sbHtml.AppendLine($"<iframe id=\"iframe-system\" name=\"iframe-system\" frameborder=\"no\" width=\"0\" height=\"0\" src=\"{projectVars.uriStaticAssets}/utilities/blank.htm\"></iframe>");

            //stick in the project specific values needed
            var html = _ReplaceProjectSpecificPlaceholders(sbHtml.ToString());

            //custom html
            html = Extensions.RenderPageBodyEndCustom(context, pageId, html);

            html = PrettyPrintXml("<foo>" + html + "</foo>").Replace("<foo>", "").Replace("</foo>", "");

            return html;
        }

        /// <summary>
        /// Injects HTML at the start of the main column of the page
        /// </summary>
        /// <param name="pageId">Site structure page ID</param>
        /// <param name="basePageInterfaceElement">(optional) base user interface element to use</param>
        public static string RenderPageMainColumnStart(string pageId, string basePageInterfaceElement = "main-column_start")
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            string? html = "";
            switch (pageId)
            {
                case "ulogindotnet":

                    break;

                default:
                    html = RetrieveInterfaceElement(basePageInterfaceElement, xmlApplicationConfiguration, reqVars);

                    //generate the breadcrumb content
                    var breadcrumbContent = "";
                    XmlNodeList? nodeListHierarchy = reqVars.xmlHierarchy.SelectNodes("//item[@id='" + pageId + "']");
                    foreach (XmlNode nodeHierarchy in nodeListHierarchy)
                    {
                        if (nodeHierarchy != null)
                        {
                            var nodeListAncestors = nodeHierarchy.SelectNodes("ancestor-or-self::*[local-name(.)='item']");
                            for (int i = 0; i < nodeListAncestors.Count; i++)
                            {
                                var nodeHierarchyNested = nodeListAncestors.Item(i);
                                var pageIdNested = GetAttribute(nodeHierarchyNested, "id");
                                var linkname = nodeHierarchyNested.SelectSingleNode("web_page/linkname").InnerText;
                                var path = nodeHierarchyNested.SelectSingleNode("web_page/path").InnerText;

                                var htmlIcon = "";
                                if (i == 0)
                                {
                                    htmlIcon = "<i class=\"ace-icon fa fa-home home-icon\"></i>";
                                }

                                if (i < nodeListAncestors.Count - 1)
                                {
                                    breadcrumbContent += "<li>" + htmlIcon + "<a href=\"javascript:navigate('" + path + "', true)\"";
                                    //if (i == 0) breadcrumbContent += " target=\"_top\"";
                                    breadcrumbContent += ">" + linkname + "</a></li>";
                                }
                                else
                                {
                                    breadcrumbContent += "<li class=\"active\">" + htmlIcon + linkname + "</li>";
                                }

                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(html))
                    {
                        html = html.Replace("[div_content-wrapper_start]", "<div class=\"main-content\">");
                        html = html.Replace("[div_content_start]", "<div class=\"page-content\">");
                        html = html.Replace("[pagetitle]", _RenderPageTitle(reqVars, projectVars));
                        html = html.Replace("[breadcrumb]", breadcrumbContent);

                        /*
                        Disabled left navigation as we will probably not use it in the new design 
                        */
                        // XsltArgumentList xsltArgs = new XsltArgumentList();
                        // xsltArgs.AddParam("pageId", "", pageId);
                        // xsltArgs.AddParam("doc-configuration", "", xmlApplicationConfiguration);
                        // if (!string.IsNullOrEmpty(projectVars.projectId))
                        // {
                        //     xsltArgs.AddParam("projectId", "", projectVars.projectId);
                        // }
                        // if (!string.IsNullOrEmpty(projectVars.editorId))
                        // {
                        //     xsltArgs.AddParam("editorId", "", projectVars.editorId);
                        // }

                        // html = html.Replace("[left_navigation]", TransformXml(reqVars.xmlHierarchyStripped, CalculateFullPathOs("cms_xsl_left-navigation"), xsltArgs));
                    }

                    break;
            }

            // Test if all services are available and show a warning
            var webservicesState = CheckStatusTaxxorServices();
            if (!webservicesState.Success)
            {
                // Grab the alert HTML and add it to the output
                var alertElementId = (webservicesState.CriticalError) ? "alert-missingservices-critical" : "alert-missingservices-warning";
                html += RetrieveInterfaceElement(alertElementId, xmlApplicationConfiguration, reqVars);

                if (webservicesState.CriticalError)
                {
                    appLogger.LogError($"Critical Taxxor docker services unavailable. ids: {string.Join(",", webservicesState.ErrorServiceIds.ToArray())}");
                }
                else
                {
                    appLogger.LogError($"Taxxor docker services unavailable. ids: {string.Join(",", webservicesState.ErrorServiceIds.ToArray())}");
                }
            }

            // Test if we need to show a warning that the project is locked
            if (projectVars.projectStatus?.ToLower() == "closed")
            {
                html += RetrieveInterfaceElement("alert-closedproject-warning", xmlApplicationConfiguration, reqVars);
            }

            // Custom html
            html = Extensions.RenderPageMainColumnStartCustom(context, pageId, html);

            //html = PrettyPrintXml("<foo>" + html + "</foo>").Replace("<foo>", "").Replace("</foo>", "");

            return html;
        }

        /// <summary>
        /// Injects HTML at the end of the main column of the page
        /// </summary>
        /// <param name="pageId">Site structure page ID</param>
        /// <param name="basePageInterfaceElement">(optional) base user interface element to use</param>
        public static string RenderPageMainColumnEnd(string pageId, string basePageInterfaceElement = "main-column_end")
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);

            string? html = "";
            switch (pageId)
            {
                default:
                    html = RetrieveInterfaceElement(basePageInterfaceElement, xmlApplicationConfiguration, reqVars);
                    break;
            }

            if (html != "" || html != null)
            {
                html = html.Replace("[div_content-wrapper_end]", "</div>");
                html = html.Replace("[div_content_end]", "</div>");
                html = html.Replace("[currentyear]", DateTime.Now.Year.ToString());
            }

            //custom html
            html = Extensions.RenderPageMainColumnEndCustom(context, pageId, html);

            //html = PrettyPrintXml("<foo>" + html + "</foo>").Replace("<foo>", "").Replace("</foo>", "");

            return html;
        }

        /// <summary>
        /// Retrieves the document/project name
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static string RetrieveDocumentName(RequestVariables reqVars, ProjectVariables projectVars)
        {
            var documentName = RetrieveNodeValueIfExists($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/name", xmlApplicationConfiguration) ?? "";

            return documentName;
        }

        /// <summary>
        /// Writes the "navigation" to the output stream in the editor
        /// </summary>
        public static async Task<string> GenerateEditorNavigation(JObject projectPreferences)
        {
            var context = System.Web.Context.Current;
            var projectVars = RetrieveProjectVariables(context);
            var debugRoutine = siteType == "local" || siteType == "dev";

            var showSectionNumbersInMenu = false;
            if (projectPreferences != null)
            {
                try
                {
                    showSectionNumbersInMenu = projectPreferences.SelectToken("showsectionnumbersinmenu")?.Value<bool>() ?? false;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Error parsing the state of the option to show section numbers in left menu");
                }
            }

            // Calculate path to the XSLT stylesheet
            XmlNode? xmlNode = null;
            switch (projectVars.editorId)
            {
                case "default_editor":
                    xmlNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='cms_xsl_hierarchy-to-html-list']");
                    break;

                default:
                    xmlNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='cms_xsl_hierarchy-to-html-list']");
                    break;
            }
            var xslPathOs = CalculateFullPathOs(xmlNode);

            // Test if we have stored user preferences of the project
            var userPreferencesKey = $"projectpreferences-{projectVars.projectId}";
            var nodeProjectPreferences = projectVars.currentUser.XmlUserPreferences.SelectSingleNode($"/settings/setting[@id='{userPreferencesKey}']");
            if (nodeProjectPreferences != null)
            {
                Console.WriteLine(PrettyPrintXml(nodeProjectPreferences));
            }

            var xpath = $"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels/output_channel[@type={GenerateEscapedXPathString(projectVars.outputChannelType)}]/name";
            var outputChannelName = RetrieveNodeValueIfExists(xpath, xmlApplicationConfiguration);
            XsltArgumentList xsltArgumentList = new XsltArgumentList();
            xsltArgumentList.AddParam("editor-id", "", projectVars.editorId);
            xsltArgumentList.AddParam("context", "", "taxxoreditor");
            xsltArgumentList.AddParam("output_channel_language", "", projectVars.outputChannelVariantLanguage);
            xsltArgumentList.AddParam("output_channel_name", "", outputChannelName);
            xsltArgumentList.AddParam("output_channel_type", "", projectVars.outputChannelType);
            xsltArgumentList.AddParam("all-permissions", "", string.Join<string>(",", projectVars.currentUser.Permissions.Permissions));
            xsltArgumentList.AddParam("show-section-numbers", "", (showSectionNumbersInMenu) ? "yes" : "no");

            var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);

            var outputChannelHierarchy = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;

            if (debugRoutine)
            {
                appLogger.LogDebug($"- outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey}");
                await outputChannelHierarchy.SaveAsync($"{logRootPathOs}/editor-leftmenu.1.xml", false, true);
            }

            // if (debugRoutine) await outputChannelHierarchy.SaveAsync($"{logRootPathOs}/editor-leftmenu.1.xml", false, true);

            switch (projectVars.editorId)
            {
                default:
                    outputChannelHierarchy = await EnrichFilingHierarchyWithMetadata(outputChannelHierarchy, projectVars);
                    break;
            }

            if (debugRoutine) await outputChannelHierarchy.SaveAsync($"{logRootPathOs}/editor-leftmenu.2.xml", false, true);

            return TransformXml(outputChannelHierarchy, xslPathOs, xsltArgumentList);
        }

        /// <summary>
        /// Retrieves the metadata of an output channel and enriches the hierarchy with that information
        /// </summary>
        /// <param name="xmlHierarchy"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> EnrichFilingHierarchyWithMetadata(XmlDocument xmlHierarchy, ProjectVariables projectVars)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            //
            // => Retrieve the metadata cache of all the files used in this project
            //
            var metadataRetrieveResult = await RetrieveCmsMetadata(projectVars.projectId);
            XmlDocument xmlCmsContentMetadata = new XmlDocument();
            if (!metadataRetrieveResult.Success)
            {
                HandleError(metadataRetrieveResult);
            }
            xmlCmsContentMetadata.ReplaceContent(metadataRetrieveResult.XmlPayload);

            // if (debugRoutine) await xmlCmsContentMetadata.SaveAsync(logRootPathOs + "/_project-filing-metadata.xml");

            //
            // => Start the enrichment process
            //
            var nodeListItems = xmlHierarchy.SelectNodes("//item");

            foreach (XmlNode nodeItem in nodeListItems)
            {
                if (nodeItem != null)
                {
                    var dataRef = GetAttribute(nodeItem, "data-ref");
                    if (!string.IsNullOrEmpty(dataRef))
                    {
                        // Find the metadata node
                        var nodeItemMetadata = xmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project/data/content[@ref='{dataRef}']");
                        if (nodeItemMetadata != null)
                        {
                            var articleType = nodeItemMetadata.SelectSingleNode("metadata/entry[@key='articletype']")?.InnerText ?? "";
                            var languages = nodeItemMetadata.SelectSingleNode("metadata/entry[@key='languages']")?.InnerText ?? "";
                            var reportingRequirements = nodeItemMetadata.SelectSingleNode("metadata/entry[@key='reportingrequirements']")?.InnerText ?? "";
                            var articleIds = nodeItemMetadata.SelectSingleNode("metadata/entry[@key='ids']")?.InnerText ?? "";

                            SetAttribute(nodeItem, "data-articletype", articleType);
                            SetAttribute(nodeItem, "data-languages", languages);
                            SetAttribute(nodeItem, "data-articleids", articleIds);

                            // Add the reporting requirements
                            var nodeListItemReportingRequirements = nodeItemMetadata.SelectNodes("metadata/entry[contains(@key,'reportingrequirements-')]");
                            foreach (XmlNode nodeItemReportingRequirement in nodeListItemReportingRequirements)
                            {
                                var metadataKey = nodeItemReportingRequirement?.GetAttribute("key") ?? "";
                                SetAttribute(nodeItem, $"data-{metadataKey}", nodeItemReportingRequirement?.InnerText ?? "");
                            }

                            // Add the content status indicators
                            var nodeListItemContentStatusses = nodeItemMetadata.SelectNodes("metadata/entry[contains(@key,'contentstatus-')]");
                            foreach (XmlNode nodeItemContentStatus in nodeListItemContentStatusses)
                            {
                                var metadataKey = nodeItemContentStatus?.GetAttribute("key") ?? "";
                                SetAttribute(nodeItem, $"data-{metadataKey}", nodeItemContentStatus?.InnerText ?? "");
                            }

                            // Add the article ID
                            var articleId = RetrieveArticleIdFromMetadataCache(nodeItemMetadata, projectVars.outputChannelVariantLanguage);
                            SetAttribute(nodeItem, "data-articleid", articleId ?? "");

                            // Add the article title
                            var articleTitle = RetrieveSectionTitleFromMetadataCache(nodeItemMetadata, projectVars.outputChannelVariantLanguage);
                            SetAttribute(nodeItem, "data-articletitle", articleTitle ?? "");
                        }
                    }
                }
            }

            return xmlHierarchy;
        }


        /// <summary>
        /// Generates javascript variables dynamically that can be used in the web pages
        /// </summary>
        public static async Task GenerateJsVariables()
        {
            var context = System.Web.Context.Current;
            var projectVars = RetrieveProjectVariables(System.Web.Context.Current);

            StringBuilder js = new StringBuilder();

            js.AppendLine(@"var strToken = '" + projectVars.token + "';");
            // Disable auto-logout on development platforms or if the user has all permissions (then we assure it's an administrator)
            js.AppendLine(@"var inactivityTime = " + ((siteType == "local" || siteType == "dev" || projectVars.currentUser.Permissions.All) ? "-1" : "60") + ";");

            await context.Response.OK(js.ToString(), ReturnTypeEnum.Js, true);
        }

        /// <summary>
        /// Replace placeholder values in the html with values specific for this project
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private static string _ReplaceProjectSpecificPlaceholders(string html)
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            // Stick in the values needed
            if ((html != "" || html != null) && html.Contains("["))
            {
                html = html.Replace("[pagetitle]", _RenderPageTitle(reqVars, projectVars));
                html = html.Replace("[cmsroot]", CmsRootPath);
                html = html.Replace("[projectroot]", projectVars.projectRootPath);
                html = html.Replace("[pageid]", reqVars.pageId);

                // inject project and version id's
                html = html.Replace("[projectid]", projectVars.projectId);
                html = html.Replace("[versionid]", projectVars.versionId);

                // inject user name
                html = html.Replace("[username]", projectVars.currentUser.DisplayName);

                // inject customer id
                html = html.Replace("[taxxor-client-id]", TaxxorClientId);
                // TODO: Legacy - needs to be removed
                html = html.Replace("[taxxorclientid]", TaxxorClientId);

                // insert the querystring to bypass the cache
                html = html.Replace("[querystringversion]", $"?v={StaticAssetsVersion}");

                // inject the nonce
                html = html.Replace("[nonce]", context.Items["nonce"]?.ToString() ?? "");


                // dynamically insert minified "min" suffix
                html = html.Replace("[minifiedsuffix]", (siteType == "local" || siteType == "dev" || siteType == "prev") ? "" : ".min");
            }

            return html ?? "";
        }

        /// <summary>
        /// Renders the title to be used in the <head> section of the web pages
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        private static string _RenderPageTitle(RequestVariables reqVars, ProjectVariables projectVars)
        {
            var basePageTitle = (projectVars.currentUser.Permissions.All) ? $"{TaxxorClientName} | {reqVars.pageTitle}" : $"{reqVars.pageTitle}";

            if (siteType == "prod")
            {
                return (projectVars.currentUser.Permissions.All) ? $"{TaxxorClientName} | {reqVars.pageTitle}" : $"{reqVars.pageTitle}";
            }
            else
            {
                return (projectVars.currentUser.Permissions.All) ? $"{TaxxorClientName} ({siteType.ToUpper()}) | {reqVars.pageTitle}" : $"{reqVars.pageTitle} - ({siteType.ToUpper()})";
            }
        }

        /// <summary>
        /// Extends the xmlApplicationConfiguration XmlDocument with an attribute that indicates the first editable page that the current user is allowed to edit
        /// </summary>
        public static async Task SetFirstEditablePageIdsForAllOutputChannels()
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            await DummyAwaiter();

            var attributeName = "firstEditableId";

            if (!string.IsNullOrEmpty(projectVars.editorId))
            {
                var nodeListOutputChannels = projectVars.xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(projectVars.editorId) + "]/output_channels");

                foreach (XmlNode nodeOutputChannels in nodeListOutputChannels)
                {
                    if (nodeOutputChannels != null)
                    {
                        // Retrieve if we need to dynamically find the first editable page
                        string? dynamicFirstEditableId = GetAttribute(nodeOutputChannels, "dynamicFirstEditableId");
                        if (string.IsNullOrEmpty(dynamicFirstEditableId)) dynamicFirstEditableId = "false";

                        var nodeListOutputChannel = nodeOutputChannels.SelectNodes("output_channel");
                        foreach (XmlNode nodeOutputChannel in nodeListOutputChannel)
                        {
                            if (nodeOutputChannel != null)
                            {
                                string? currentOutputChannelType = GetAttribute(nodeOutputChannel, "type");

                                var nodeListOutputChannelVariants = nodeOutputChannel.SelectNodes("variants/variant");
                                foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                                {
                                    if (nodeVariant != null)
                                    {
                                        var currentOutputChannelVariantId = GetAttribute(nodeVariant, "id");
                                        var currentOutputChannelLanguage = GetAttribute(nodeVariant, "lang");
                                        if (!string.IsNullOrEmpty(currentOutputChannelVariantId))
                                        {
                                            string? firstEditableId = "";

                                            // For the PDF, the first editable id is the first item on level 1
                                            var firstEditableItemXpath = "//items/structured//item[permissions/permission/@id='all' or permissions/permission/@id='view' or permissions/permission/@id='editsection']";
                                            if (currentOutputChannelType == "pdf")
                                            {
                                                firstEditableItemXpath = "//items/structured/item/sub_items//item[permissions/permission/@id='all' or permissions/permission/@id='view' or permissions/permission/@id='editsection']";
                                            }

                                            // Reset existing attribute
                                            RemoveAttribute(nodeVariant, attributeName);
                                            var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, currentOutputChannelType, currentOutputChannelVariantId, currentOutputChannelLanguage);
                                            var isAvailableInProjectConfig = true;
                                            if (RetrieveOutputchannelHierarchyLocationXmlNode(projectVars.projectId, outputChannelHierarchyMetadataKey) == null) isAvailableInProjectConfig = false;

                                            // Retrieve the first editable page (fact) id from the output channel hierarchy
                                            if (isAvailableInProjectConfig)
                                            {
                                                if (dynamicFirstEditableId == "true" && currentOutputChannelVariantId != null)
                                                {
                                                    // Retrieve the hierarchy from the cache

                                                    //appLogger.LogCritical($"outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey}");

                                                    if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                                                    {
                                                        var xmlHierarchyDoc = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;

                                                        var nodeFistItem = xmlHierarchyDoc.SelectSingleNode(firstEditableItemXpath);
                                                        if (nodeFistItem != null)
                                                        {
                                                            firstEditableId = GetAttribute(nodeFistItem, "id");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        appLogger.LogError($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}']");
                                                    }

                                                }
                                                else
                                                {

                                                    if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                                                    {
                                                        firstEditableId = RetrieveIdFirstEditablePage(projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml);
                                                    }
                                                    else
                                                    {
                                                        appLogger.LogError($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}']");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (siteType == "local" || siteType == "dev") appLogger.LogInformation($"outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey} is available in the editor definition, but not in the project definition");
                                            }


                                            // Set the attribute value
                                            if (!string.IsNullOrEmpty(firstEditableId))
                                            {
                                                // Set the first editable ID in the application cconfiguration attached to the project variables
                                                SetAttribute(nodeVariant, attributeName, firstEditableId);

                                                // Set the projectvars version of the first editable page ID
                                                if (projectVars.outputChannelType == currentOutputChannelType && projectVars.outputChannelVariantId == currentOutputChannelVariantId && projectVars.outputChannelVariantLanguage == currentOutputChannelLanguage)
                                                {
                                                    projectVars.idFirstEditablePage = firstEditableId;
                                                }
                                            }
                                        }
                                    }


                                }
                            }

                        }
                    }



                }

            }
            else
            {
                appLogger.LogError("!! Could not determine first editable page ID because projectVars.editorId is empty !!");
            }

        }

        /// <summary>
        /// Retrieves the first editable id from the hierarchy
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <returns></returns>
        public static string RetrieveIdFirstEditablePage(XmlDocument xmlDocument)
        {
            var pageId = "";
            var xmlNodeList = xmlDocument.SelectNodes("//item[@editable='true']");
            foreach (XmlNode xmlNode in xmlNodeList)
            {
                if (xmlNode != null)
                {
                    pageId = xmlNode.Attributes["id"].Value;
                    break;
                }
            }
            return pageId;
        }

        /// <summary>
        /// Renders the project preferences javascript variable which is based on the user preferences
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="includeVariableDeclaration"></param>
        /// <returns></returns>
        public static async Task<string> RenderProjectPreferencesJson(ProjectVariables projectVars, bool includeVariableDeclaration = true)
        {
            if (string.IsNullOrEmpty(projectVars.projectId)) return "";

            // Get the nodelist containing all the output channel variations
            var nodeListOutputChannelVariants = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/variants/variant");

            // Test if we have stored user preferences of the project
            var userPreferencesKey = $"projectpreferences-{projectVars.projectId}";
            var projectPreferencesJson = "";
            var nodeProjectPreferences = projectVars.currentUser.XmlUserPreferences.SelectSingleNode($"/settings/setting[@id='{userPreferencesKey}']");
            if (nodeProjectPreferences != null)
            {
                // Validate if all the outputchannel variant ID's are present in the stored user preferences
                var projectPreferencesFromSession = JObject.Parse(nodeProjectPreferences.InnerText);

                var projectPreferencesValid = true;
                foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                {
                    var outputChannelVariantId = nodeVariant?.GetAttribute("id") ?? "";
                    if (outputChannelVariantId != "")
                    {
                        var outputChannelVariantLayoutSettings = projectPreferencesFromSession["outputchannels"][outputChannelVariantId];

                        // OutputchannelVariant does not exist so we need to add it
                        if (outputChannelVariantLayoutSettings == null)
                        {
                            appLogger.LogWarning($"Output channel variant {outputChannelVariantId} does not exist in the project preferences so we need to add it");
                            projectPreferencesValid = false;

                            var outputChannelObject = projectPreferencesFromSession["outputchannels"] as JObject;
                            outputChannelObject.Add(outputChannelVariantId,
                                new JObject(
                                    new JProperty("layout", "regular")));
                        }
                    }
                    else
                    {
                        appLogger.LogWarning("Unable to find an output channel variant ID");
                    }
                }

                if (!projectPreferencesValid)
                {
                    // Store the updated JSON string in the user preferences
                    // Console.WriteLine($"NEW JSON: {projectPreferencesFromSession.ToString()}");
                    await projectVars.currentUser.UpdateUserPreferenceKey(userPreferencesKey, projectPreferencesFromSession.ToString());
                }




                // foreach (JProperty x in projectPreferencesFromSession["outputchannels"])
                // { // Where 'obj' and 'obj["otherObject"]' are both JObjects
                //     string outputChannelVariantId = x.Name;
                //     JToken value = x.Value;
                //     Console.WriteLine($"outputChannelVariantId: {outputChannelVariantId} value: {value}");

                //     if (xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/variants/variant[@id='{outputChannelVariantId}']") == null)
                //     {
                //         Console.WriteLine($"** Not exists **");
                //     }

                // }

                projectPreferencesJson = projectPreferencesFromSession.ToString();
            }
            else
            {
                dynamic projectPreferences = new ExpandoObject();
                projectPreferences.leftmenufilter = false;
                projectPreferences.showsectionnumbersinmenu = false;
                projectPreferences.outputchannels = new Dictionary<string, ExpandoObject>();


                foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                {
                    var outputChannelVariantId = nodeVariant?.GetAttribute("id") ?? "";
                    dynamic outputChannel = new ExpandoObject();
                    outputChannel.layout = RetrieveOutputChannelDefaultLayout(projectVars.projectId, outputChannelVariantId).Layout;
                    try
                    {
                        projectPreferences.outputchannels.Add(nodeVariant?.GetAttribute("id") ?? "", outputChannel);
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, "Unable to add output channel to project preferences json");
                    }

                }

                projectPreferencesJson = JsonConvert.SerializeObject(projectPreferences);
            }

            if (includeVariableDeclaration)
            {
                return $"var projectPreferences={projectPreferencesJson};";
            }
            else
            {
                return projectPreferencesJson;
            }

        }

    }
}