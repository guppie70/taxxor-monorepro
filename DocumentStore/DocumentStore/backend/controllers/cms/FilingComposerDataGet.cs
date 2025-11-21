using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with user preferences and data
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Resolves table cell content, footnote content, internal section and content element links for a section data file
        /// </summary>
        /// <param name="context"></param>
        /// <param name="projectId"></param>
        /// <param name="xmlSectionPathOs"></param>
        /// <returns></returns>
        public static XmlDocument LoadAndResolveInlineFilingComposerData(RequestVariables reqVars, ProjectVariables projectVars, string xmlSectionPathOs, bool stopProcessingOnError = true)
        {

            // Global variables
            string? lang = projectVars.outputChannelVariantLanguage;
            var errorMessage = "";
            var errorDetails = "";

            // Calculate the path to the folder that contains the data files
            var xmlSectionFolderPathOs = Path.GetDirectoryName(xmlSectionPathOs);

            var xmlDocument = new XmlDocument();

            // Load the XML data of a single section
            // appLogger.LogCritical(xmlSectionPathOs);
            var error = false;
            try
            {
                xmlDocument.Load(xmlSectionPathOs);
            }
            catch (Exception ex)
            {
                error = true;
                errorMessage = "Could not load source data";
                errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
            }

            // Sync the data into the page from the cached external table
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(SyncExternalCachedTableData(xmlDocument, projectVars.projectId, xmlSectionFolderPathOs, lang), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (table sync error)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Sync the data from the cached structured data files
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(SyncStructuredDataElements(xmlDocument, xmlSectionPathOs, projectVars.projectId), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (structured data sync error)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Sync the footnote content
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(SyncFootnoteContent(xmlDocument, projectVars, xmlSectionFolderPathOs), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (footnote sync error)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Check links
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(ProcessInternalDocumentLinks(reqVars, projectVars, xmlDocument, xmlSectionPathOs), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (link checking error)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Resolve links to elements and sections
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(ResolveSectionElementLinks(xmlDocument, projectVars, xmlSectionFolderPathOs), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (element links error)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Resolve links to configuration nodes
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(ResolveConfigurationLinks(xmlDocument, projectVars, xmlSectionFolderPathOs), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (configuration links error)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Resolve dynamic links to images, drawings, etc. that contain placeholder values (like {siteid}, etc.)
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(DynamicPlaceholdersResolve(xmlDocument, reqVars, projectVars, xmlSectionFolderPathOs), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (dynamic placeholder replacement)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Set elememts to be editable or not
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(ContentEditableResolve(xmlDocument, reqVars, projectVars, xmlSectionFolderPathOs), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (dynamic placeholder replacement)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Wrap image nodes in a wrapper element if a caption needs to be displayed
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(ProcessImagesOnLoad(xmlDocument, reqVars, projectVars, xmlSectionFolderPathOs), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (dynamic placeholder replacement)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Inject the latest graph SVG data
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(ProcessGraphsOnLoad(xmlDocument, projectVars), true);
                }
                catch (Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (SVG graph data injection)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            // Custom logic
            if (!error)
            {
                try
                {
                    xmlDocument.ReplaceContent(CustomFilingComposerDataGet(xmlDocument, projectVars, xmlSectionFolderPathOs), true);
                }
                catch (System.Exception ex)
                {
                    error = true;
                    errorMessage = "Could not load source data (custom logic error)";
                    errorDetails = $"xmlSectionPathOs: {xmlSectionPathOs}, error: {ex}, stack-trace: {GetStackTrace()}";
                }
            }

            if (error)
            {
                if (stopProcessingOnError)
                {
                    HandleError(ReturnTypeEnum.Xml, errorMessage, errorDetails);
                }
                else
                {
                    return GenerateErrorXml(errorMessage, errorDetails);
                }

            }

            return xmlDocument;
        }

        /// <summary>
        /// Retrieves the path for the content tools based XHTML data file
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="did"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static string RetrieveInlineFilingComposerXmlPathOs(RequestVariables reqVars, ProjectVariables projectVars, string did, bool debugRoutine)
        {
            var dataReference = did;
            if (!did.EndsWith(".xml"))
            {
                // Map did (sitestructure id) to data-ref in order to calculate the correct path for the XML file that we need to retrieve
                var xmlFilingHierarchyPathOs = CalculateHierarchyPathOs(reqVars, projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                var xmlFilingHierarchy = new XmlDocument();
                xmlFilingHierarchy.Load(xmlFilingHierarchyPathOs);

                var basicDebugInfo = $"projectId: {projectVars.projectId}, versionId: {projectVars.versionId}, editorId: {projectVars.editorId}, editorContentType: {projectVars.editorContentType}, reportTypeId: {projectVars.reportTypeId}, outputChannelType: {projectVars.outputChannelType}, outputChannelVariantId: {projectVars.outputChannelVariantId}, outputChannelVariantLanguage: {projectVars.outputChannelVariantLanguage}";

                var xPath = $"/items/structured//item[@id={GenerateEscapedXPathString(did)}]";
                var nodeItem = xmlFilingHierarchy.SelectSingleNode(xPath);
                if (nodeItem == null) HandleError("Could not locate filing hierarchy item", $"xPath: {xPath}, {basicDebugInfo}, stack-trace: {GetStackTrace()}");

                dataReference = GetAttribute(nodeItem, "data-ref");
                if (string.IsNullOrEmpty(dataReference)) HandleError("Could not locate the data reference from the filing hierarchy item", $"{basicDebugInfo}, stack-trace: {GetStackTrace()}");
            }

            // Calculate the base path to the xml file that contains the section XML file
            var dataFilePathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(reqVars, projectVars) + "/" + dataReference;

            return dataFilePathOs;
        }

        public static string? RetrieveInlineFilingComposerXmlRootFolderPathOs(RequestVariables reqVars, ProjectVariables projectVars, string contentType = "regular")
        {
            // Calculate the base path to the xml file that contains the section XML file
            var nodeSourceDataLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/content_types/content_management/type[@id=" + GenerateEscapedXPathString(contentType) + "]/xml");
            appLogger.LogInformation($"RetrieveInlineFilingComposerXmlRootFolderPathOs - nodeSourceDataLocation: '{nodeSourceDataLocation?.OuterXml ?? "NULL"}', projectId: '{projectVars.projectId}', contentType: '{contentType}'");
            var dataFilePathOs = CalculateFullPathOs(nodeSourceDataLocation, reqVars, projectVars, true);
            appLogger.LogInformation($"RetrieveInlineFilingComposerXmlRootFolderPathOs - dataFilePathOs after CalculateFullPathOs: '{dataFilePathOs}'");
            var result = Path.GetDirectoryName(dataFilePathOs);
            appLogger.LogInformation($"RetrieveInlineFilingComposerXmlRootFolderPathOs - returning: '{result}'");
            return result;
        }

        /// <summary>
        /// Retrieves the root folder path OS where the data files for the inline filing editor is stored
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public static string? RetrieveInlineFilingComposerXmlRootFolderPathOs(string projectId, string contentType = "regular")
        {
            // Calculate the base path to the xml file that contains the section XML file
            var nodeSourceDataLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]/content_types/content_management/type[@id=" + GenerateEscapedXPathString(contentType) + "]/xml");
            var dataFilePathOs = CalculateFullPathOs(nodeSourceDataLocation, null, null, true);
            return Path.GetDirectoryName(dataFilePathOs);
        }


        /// <summary>
        /// Retrieves an overview of all the filing content data elements
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RetrieveFilingDataOverview(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            if (!string.IsNullOrEmpty(projectVars.projectId))
            {
                var dataFolderPathOs = RetrieveFilingDataFolderPathOs();
                var renderFilingOverviewResult = RetrieveFilingDataOverview(dataFolderPathOs);

                if (renderFilingOverviewResult.Success)
                {
                    await response.OK(GenerateSuccessXml(renderFilingOverviewResult.Message, renderFilingOverviewResult.DebugInfo, HttpUtility.HtmlEncode(renderFilingOverviewResult.XmlPayload.OuterXml)), ReturnTypeEnum.Xml, true);
                }
                else
                {
                    HandleError(ReturnTypeEnum.Xml, "Could not compile overview", renderFilingOverviewResult.DebugInfo);
                }
            }
            else
            {
                HandleError(ReturnTypeEnum.Xml, "Could not compile overview", "Not enough information supplied - project ID is empty");
            }
        }

        /// <summary>
        /// Retrieves an overview of all the filing content data elements
        /// </summary>
        /// <param name="dataFolderPathOs"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage RetrieveFilingDataOverview(string dataFolderPathOs)
        {
            var xmlFilingDataOverview = new XmlDocument();
            xmlFilingDataOverview.LoadXml("<items/>");

            DirectoryInfo info = new DirectoryInfo(dataFolderPathOs);
            FileInfo[] files = info.GetFiles("*.xml", SearchOption.TopDirectoryOnly).OrderByDescending(p => p.CreationTime).ToArray();
            foreach (FileInfo fileInfo in files)
            {
                var xmlPathOs = fileInfo.FullName;
                try
                {
                    var xmlSection = new XmlDocument();
                    xmlSection.Load(xmlPathOs);
                    var nodeFirstTranslation = xmlSection.SelectSingleNode("/data/content[1]/*");
                    var sectionId = GetAttribute(nodeFirstTranslation, "id");
                    var hierarchyId = $"{sectionId}-{RandomString(16, false)}";
                    var dataRef = Path.GetFileName(xmlPathOs);

                    // Create base item node
                    var nodeItem = xmlFilingDataOverview.CreateElement("item");
                    SetAttribute(nodeItem, "id", hierarchyId);
                    SetAttribute(nodeItem, "data-ref", dataRef);
                    SetAttribute(nodeItem, "section-id", sectionId);

                    // Loop through all the content (language) nodes
                    foreach (XmlNode nodeContent in xmlSection.SelectNodes("/data/content"))
                    {
                        var lang = GetAttribute(nodeContent, "lang");

                        // Attempt to find the header node
                        var nodeSectionTitle = RetrieveFirstHeaderNode(nodeContent);

                        if (nodeSectionTitle != null)
                        {
                            var nodeHeaderImported = xmlFilingDataOverview.ImportNode(nodeSectionTitle, true);
                            nodeHeaderImported.SetAttribute("lang", lang);
                            nodeItem.AppendChild(nodeHeaderImported);
                        }
                        else
                        {
                            var nodeHeader = xmlFilingDataOverview.CreateElement("h1");
                            nodeHeader.SetAttribute("lang", lang);
                            nodeHeader.InnerText = "[unknown]";
                            nodeItem.AppendChild(nodeHeader);
                        }

                        xmlFilingDataOverview.DocumentElement.AppendChild(nodeItem);
                    }

                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not compile overview", $"ERROR: xmlPathOs: {xmlPathOs}, error-message: {ex.Message}, error-stack: {ex.StackTrace}");
                }
            }

            return new TaxxorReturnMessage(true, "Successfully compiled overview of XML section files", xmlFilingDataOverview, "");
        }

    }
}