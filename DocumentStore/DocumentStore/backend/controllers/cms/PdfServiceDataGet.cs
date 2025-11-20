using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
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
        /// Retrieves the source XHTML data for the PDF Service to use as the source material
        /// </summary>
        /// <returns>The user data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task RetrievePdfSourceData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted variables
            var projectId = request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var versionId = request.RetrievePostedValue("vid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var dataType = request.RetrievePostedValue("type", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var sections = request.RetrievePostedValue("sections", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var renderScope = request.RetrievePostedValue("renderscope", RegexEnum.None, true, ReturnTypeEnum.Xml);
            projectVars.editorContentType = request.RetrievePostedValue("ctype", RegexEnum.None, false, ReturnTypeEnum.Xml, "regular");
            projectVars.reportTypeId = request.RetrievePostedValue("rtype", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var hideCurrentPeriodDatapointsString = request.RetrievePostedValue("hidecurrentperioddatapoints", "(true|false)", false, reqVars.returnType, "false");
            var hideCurrentPeriodDatapoints = hideCurrentPeriodDatapointsString == "true";

            var baseDebugInfo = $"projectId: '{projectId}', versionId: '{versionId}', dataType: '{dataType}', sections: '{sections}', projectVars.editorContentType: '{projectVars.editorContentType}', projectVars.reportTypeId: '{projectVars.reportTypeId}'";

            if (projectVars.currentUser.IsAuthenticated)
            {
                if (!ValidateCmsPostedParameters(projectId, versionId, dataType)) HandleError(reqVars, "There was an error validating the posted values", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");
                // Retrieve the XML data that we need to return
                var result = RetrievePdfSourceData(projectId, versionId, sections, renderScope, debugRoutine);
                if (result.Success)
                {

                    if (hideCurrentPeriodDatapoints)
                    {
                        //
                        // => Hide the current period datapoints from the returned XML
                        //
                        var hiddenCharacter = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='hidden-character']")?.InnerText ?? "-";
                        var hiddenClass = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='hidden-class']")?.InnerText ?? "tx-notavailable";
                        var xmlToReturn = new XmlDocument();
                        xmlToReturn.ReplaceContent(result.XmlPayload, false);

                        // - Retrieve the current year of the project
                        var projectPeriodMetadata = new ProjectPeriodProperties(projectId);
                        if (!projectPeriodMetadata.Success)
                        {
                            HandleError(ReturnTypeEnum.Xml, "Unable to retrieve project metadata", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");
                        }

                        // - The current check if a SDE value is in the curent period only works for Annual Report projects
                        if (projectPeriodMetadata.ProjectType != "ar")
                        {
                            HandleError(ReturnTypeEnum.Xml, $"No support for hiding current period values for project type {projectPeriodMetadata.ProjectType}", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");
                        }

                        // - Retrieve the SDE's in the content
                        var factIds = new Dictionary<string, XmlNode>();
                        var nodeListSdes = xmlToReturn.SelectNodes(_retrieveStructuredDataElementsBaseXpath());

                        if (nodeListSdes.Count > 0)
                        {

                            foreach (XmlNode nodeSde in nodeListSdes)
                            {
                                var sdeId = nodeSde.GetAttribute("data-fact-id");
                                if (!string.IsNullOrEmpty(sdeId) && !factIds.ContainsKey(sdeId)) factIds.Add(sdeId, nodeSde);
                            }

                            // Dump the list of sdeIds to the console in one command
                            // Console.WriteLine($"Node list: {nodeListSdes.Count}\nList of SDE IDs:\n{string.Join("\n", factIds.Select(id => $"- {id}"))}\nTotal SDEs found: {factIds.Count}");


                            //
                            // => Retrieve the mapping information for the SDE's in scope
                            //
                            var xmlMappingInformation = await Taxxor.ConnectedServices.MappingService.RetrieveMappingInformation(factIds.Keys.ToList(), projectId, false);

                            if (!XmlContainsError(xmlMappingInformation))
                            {

                                //
                                // => Loop through the mapping clusters we have received
                                //
                                var nodeListMappingClusters = xmlMappingInformation.SelectNodes("//mappingCluster");
                                foreach (XmlNode nodeMappingCluster in nodeListMappingClusters)
                                {
                                    string factId;
                                    string existingPeriod;

                                    var nodeInternalEntry = _getInternalEntry(nodeMappingCluster);
                                    if (nodeInternalEntry != null)
                                    {
                                        existingPeriod = nodeInternalEntry.GetAttribute("period");
                                        factId = nodeInternalEntry.SelectSingleNode("mapping")?.InnerText;
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Could not find internal mapping entry in nodeInternalEntry: {nodeInternalEntry?.OuterXml ?? nodeMappingCluster?.OuterXml ?? "null"}.");

                                        // Skip the rest of the processing
                                        continue;
                                    }

                                    // Console.WriteLine($"- factId: {factId}, existing period: {existingPeriod}");

                                    //
                                    // => Check if the SDE is within the current period of the project and then adjust it's value and apply the hiddenClass
                                    //
                                    if (existingPeriod.Contains(projectPeriodMetadata.CurrentProjectYear.ToString()))
                                    {
                                        // Render a dash instead of the value of the SDE
                                        factIds[factId].InnerText = hiddenCharacter;

                                        // Add the hiddenClass to the element's class attribute
                                        var classAttribute = factIds[factId].Attributes["class"];
                                        if (classAttribute != null)
                                        {
                                            // If class attribute exists, append the hiddenClass
                                            var currentClasses = classAttribute.Value.Split(' ');
                                            if (!currentClasses.Contains(hiddenClass))
                                            {
                                                classAttribute.Value += $" {hiddenClass}";
                                            }
                                        }
                                        else
                                        {
                                            // If class attribute doesn't exist, create it with hiddenClass
                                            factIds[factId].SetAttribute("class", hiddenClass);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                appLogger.LogError($"There was an error retrieving the mapping information of the SDE's. stack-trace: {GetStackTrace()}");
                                HandleError(ReturnTypeEnum.Xml, "Error retrieving mapping information", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");
                            }
                        }

                        // Stick the file content in the message field of the success xml and the file path into the debuginfo node
                        await response.OK(GenerateSuccessXml(HttpUtility.HtmlEncode(xmlToReturn.OuterXml), baseDebugInfo), ReturnTypeEnum.Xml, true);
                    }
                    else
                    {

                        // Stick the file content in the message field of the success xml and the file path into the debuginfo node
                        await response.OK(GenerateSuccessXml(HttpUtility.HtmlEncode(result.XmlPayload.OuterXml), baseDebugInfo), ReturnTypeEnum.Xml, true);
                    }
                }
                else
                {
                    HandleError(ReturnTypeEnum.Xml, result);
                }
            }
            else
            {
                HandleError(ReturnTypeEnum.Xml, "Not authenticated", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}", 403);
            }
        }

        /// <summary>
        /// Retrieves the source XHTML data for the PDF Service to use as the base information for rendering a PDF
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="sections"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage RetrievePdfSourceData(string projectId, string versionId, string sections, string renderScope, bool debugRoutine)
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            RequestVariables reqVars = RetrieveRequestVariables(context);

            //
            // => Calculate the base path to the xml file that contains the section XML file
            //
            var xpath = $"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectId)}]/content_types/content_management/type[@id={GenerateEscapedXPathString(projectVars.editorContentType)}]/xml";
            var nodeSourceDataLocation = xmlApplicationConfiguration.SelectSingleNode(xpath);
            if (nodeSourceDataLocation == null)
            {
                return new TaxxorReturnMessage(false, "Could not generate PDF Source data", $"xpath: {xpath}, stack-trace: {GetStackTrace()}");
            }

            var xmlSectionPathOs = CalculateFullPathOs(nodeSourceDataLocation);

            var xmlSectionDataFolderPathOs = Path.GetDirectoryName(xmlSectionPathOs);

            //
            // => Compile a complete XML document based on the information avaibable in the hierarchy
            //

            // - Retrieve the hierarchy
            var xmlHierarchy = RenderFilingHierarchy(reqVars, projectVars, projectId, versionId, true);

            return RetrievePdfSourceData(xmlHierarchy, xmlSectionPathOs, sections, renderScope, debugRoutine);
        }

        /// <summary>
        /// Retrieves the source XHTML data for the PDF Service to use as the base information for rendering a PDF
        /// </summary>
        /// <param name="xmlHierarchy"></param>
        /// <param name="xmlSectionPathOs"></param>
        /// <param name="sections"></param>
        /// <param name="renderScope"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage RetrievePdfSourceData(XmlDocument xmlHierarchy, string xmlSectionPathOs, string sections, string renderScope, bool debugRoutine)
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Console.WriteLine("*************");
            // Console.WriteLine($"xmlSectionDataFolderPathOs: {xmlSectionDataFolderPathOs}");
            // Console.WriteLine(PrettyPrintXml(xmlHierarchy));
            // Console.WriteLine("*************");
            XmlDocument xmlPdfData = new XmlDocument();
            var targetDocumentRootNode = xmlPdfData.CreateElement("div");
            targetDocumentRootNode.SetAttribute("class", "body-wrapper");
            var nodeMetadata = xmlPdfData.CreateElement("metadata");
            nodeMetadata.AppendChild(xmlPdfData.CreateElement("hierarchy"));
            targetDocumentRootNode.AppendChild(nodeMetadata);
            xmlPdfData.AppendChild(targetDocumentRootNode);

            var fullOutputChannelRequested = sections == "all";
            if (!fullOutputChannelRequested && renderScope == "include-children" && !sections.Contains(","))
            {
                // Test if the section that is requested is the ID of the root element in the hierarchy, because then we are rendering the full output channel
                if (xmlHierarchy.SelectSingleNode($"/items/structured/item[@id='{sections}']") != null) fullOutputChannelRequested = true;
            }


            var xmlSectionDataFolderPathOs = xmlSectionPathOs.Contains(".xml") ? Path.GetDirectoryName(xmlSectionPathOs) : xmlSectionPathOs;

            // Build up an xpath containing all the sections that we need in our PDF
            var xpath = "/items/structured//item";
            if (sections != "all")
            {
                // Parse the passed section/item id's in a basic list (exploding comma-delimited as well)
                List<string> itemIds = new List<string>();
                List<string> itemIdsComplete = new List<string>();

                if (sections.Contains(','))
                {
                    string[] sectionIds = sections.Split(',');
                    itemIds.AddRange(sectionIds);
                }
                else
                {
                    itemIds.Add(sections);
                }

                // Create the final list that we will use to build up the xpath

                foreach (string itemId in itemIds)
                {
                    if (renderScope == "top-level")
                    {
                        var xPathTopLevel = $"/items/structured/item/sub_items/item[@id='{itemId}' or sub_items/item[descendant-or-self::item/@id='{itemId}']]";

                        // var xPathTopLevel = $"/items/structured/item/sub_items//item[ancestor-or-self::item/@id='{itemId}' or descendant::item/@id='{itemId}']";
                        var nodeLevel1 = xmlHierarchy.SelectSingleNode(xPathTopLevel);
                        if (nodeLevel1 == null)
                        {
                            return new TaxxorReturnMessage(false, "Unable to find section data to return", $"xPathTopLevel: {xPathTopLevel}, stack-trace: {GetStackTrace()}");
                        }
                        else
                        {
                            itemIdsComplete.Add(nodeLevel1.GetAttribute("id"));
                            var nodeListTopLevelItems = nodeLevel1.SelectNodes("sub_items//item");
                            foreach (XmlNode node in nodeListTopLevelItems)
                            {
                                itemIdsComplete.Add(node.GetAttribute("id"));
                            }
                        }

                    }
                    else
                    {
                        itemIdsComplete.Add(itemId);
                        if (renderScope == "include-children")
                        {
                            var nodeListChildItems = xmlHierarchy.SelectNodes($"//item[@id='{itemId}']/sub_items//item");
                            foreach (XmlNode nodeChildItem in nodeListChildItems)
                            {
                                var childItemId = GetAttribute(nodeChildItem, "id");
                                if (string.IsNullOrEmpty(childItemId))
                                {
                                    appLogger.LogWarning($"Could not find an ID for hierarchy item: {nodeChildItem.OuterXml}. stack-trace: {GetStackTrace()}");
                                }
                                else
                                {
                                    itemIdsComplete.Add(childItemId);
                                }
                            }
                        }
                    }


                }

                // Generate the xpath
                var sbXpath = new StringBuilder();
                sbXpath.Append("/items/structured//item[");
                for (int i = 0; i < itemIdsComplete.Count; i++)
                {
                    var itemId = itemIdsComplete[i];
                    // Console.WriteLine($"- itemId: {itemId}");

                    sbXpath.Append($"@id='{itemId}'");

                    if (i < (itemIdsComplete.Count - 1))
                    {
                        sbXpath.Append(" or ");
                    }
                }
                sbXpath.Append("]");

                xpath = sbXpath.ToString();
            }

            // if (debugRoutine) Console.WriteLine($"- xpath: {xpath}");


            // Loop through the hierarchy items to retrieve the individual XML sources
            var includeHierarchy = false;
            var nodeListItems = xmlHierarchy.SelectNodes(xpath);
            if (nodeListItems.Count == 0) return new TaxxorReturnMessage(false, "Unable to find source data because we could not locate it in the hierarchy", $"xpath: {xpath}, stack-trace: {GetStackTrace()}");

            foreach (XmlNode nodeItem in nodeListItems)
            {
                var itemId = GetAttribute(nodeItem, "id");
                var dataRef = GetAttribute(nodeItem, "data-ref");
                var tocNumber = GetAttribute(nodeItem, "data-tocnumber");
                var nodeListAncestors = nodeItem.SelectNodes($"ancestor-or-self::item");
                var hierarchicalLevel = nodeListAncestors.Count - 1;


                var sectionDataFilePathOs = $"{xmlSectionDataFolderPathOs}/{dataRef}";
                //Console.WriteLine($"- hierarchicalLevel: {hierarchicalLevel}");

                // Calculate the path to the folder that contains the data files
                var xmlSectionFolderPathOs = Path.GetDirectoryName(sectionDataFilePathOs);
                try
                {
                    var xmlSection = LoadAndResolveInlineFilingComposerData(reqVars, projectVars, sectionDataFilePathOs);

                    // Select the node that we want to import and set the current hierarchical level in it
                    var xPathArticle = $"/data/content[@lang='{projectVars.outputChannelVariantLanguage}']/*";
                    var nodeSourceData = xmlSection.SelectSingleNode(xPathArticle);
                    if (nodeSourceData == null)
                    {
                        var articleId = xmlSection.SelectSingleNode("//article")?.GetAttribute("id") ?? "unknown";
                        return new TaxxorReturnMessage(false, $"Could not find article to add to the PDF content", $"articleId: {articleId}, sectionDataFilePathOs: {sectionDataFilePathOs}, xPathArticle: {xPathArticle}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        nodeSourceData.SetAttribute("data-hierarchical-level", hierarchicalLevel.ToString());
                        nodeSourceData.SetAttribute("data-ref", dataRef);
                        nodeSourceData.SetAttribute("data-did", itemId);
                        if (!string.IsNullOrEmpty(tocNumber)) nodeSourceData.SetAttribute("data-tocnumber", tocNumber);

                        // Include the level one 
                        var levelOneADataReference = "";
                        if (hierarchicalLevel > 0)
                        {
                            levelOneADataReference = nodeListAncestors.Item(1).GetAttribute("data-ref");
                        }
                        nodeSourceData.SetAttribute("data-levelone-ref", levelOneADataReference);

                        // Test if this content contains the table of contents placeholder node
                        if (!includeHierarchy)
                        {
                            var nodeTocPlaceholder = nodeSourceData.SelectSingleNode("*//ul[@class='toc-list' or @data-tableofcontentholder]");
                            if (nodeTocPlaceholder != null)
                            {
                                includeHierarchy = true;
                            }
                            else
                            {
                                // If we use a special linkstyle then we need to include the hierarchy as well
                                if (nodeSourceData.SelectNodes("//*[@data-linkstyle='include-section-numbers']").Count > 0)
                                {
                                    includeHierarchy = true;
                                }
                            }
                        }

                        // Import the fragment and sequentially add it to the root node
                        var importedNode = xmlPdfData.ImportNode(nodeSourceData, true);
                        targetDocumentRootNode.AppendChild(importedNode);
                    }
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, $"Could not load section XML data - path: {xmlSectionDataFolderPathOs}/{itemId}.xml", ex);
                }
            }

            //
            // => Test if we have received useful content
            //
            if (xmlPdfData.SelectNodes("//article").Count == 0)
            {
                return new TaxxorReturnMessage(false, $"Unable to find any articles to return", $"stack-trace: {GetStackTrace()}");
            }

            //
            // => Remove articles from the XML we return if they have been marked as such
            //
            if (fullOutputChannelRequested)
            {
                var nodeListArticlesToDelete = xmlPdfData.SelectNodes("//article[descendant::section[@data-hidefromfullpdf]]");
                if (nodeListArticlesToDelete.Count > 0) RemoveXmlNodes(nodeListArticlesToDelete);
            }


            //
            // => Inject the hierarchy in the metadata node
            //
            if (includeHierarchy)
            {
                var nodeHierarchyImported = xmlPdfData.ImportNode(xmlHierarchy.DocumentElement, true);
                xmlPdfData.SelectSingleNode("/div/metadata/hierarchy").AppendChild(nodeHierarchyImported);
            }

            return new TaxxorReturnMessage(true, "Successfully retrieved PDF source data", xmlPdfData);
        }

        /// <summary>
        /// Compiles a PDF source data document from an earlier commit in the version control (GIT) system
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RetrievePdfHistoricalSourceData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted variables
            var projectId = request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var versionId = request.RetrievePostedValue("vid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var dataType = request.RetrievePostedValue("type", true, ReturnTypeEnum.Xml);
            var sections = request.RetrievePostedValue("sections", true, ReturnTypeEnum.Xml);
            var renderScope = request.RetrievePostedValue("renderscope", true, ReturnTypeEnum.Xml);
            projectVars.editorContentType = request.RetrievePostedValue("ctype", RegexEnum.None, false, ReturnTypeEnum.Xml, "regular");
            projectVars.reportTypeId = request.RetrievePostedValue("rtype", true, ReturnTypeEnum.Xml);
            var commitHash = request.RetrievePostedValue("commithash", RegexEnum.HashOrTag, true, reqVars.returnType);

            var baseDebugInfo = $"commitHash: {commitHash}, projectId: '{projectId}', versionId: '{versionId}', dataType: '{dataType}', sections: '{sections}', projectVars.editorContentType: '{projectVars.editorContentType}', projectVars.reportTypeId: '{projectVars.reportTypeId}'";

            if (projectVars.currentUser.IsAuthenticated)
            {
                if (!ValidateCmsPostedParameters(projectId, versionId, dataType)) HandleError(reqVars, "There was an error validating the posted values", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");

                try
                {


                    // Retrieve the XML data that we need to return
                    var historicalPdfSourceDataResult = RetrievePdfHistoricalSourceData(commitHash, sections, renderScope, debugRoutine, null, projectId);
                    if (!historicalPdfSourceDataResult.Success)
                    {
                        HandleError(historicalPdfSourceDataResult);
                    }
                    else
                    {
                        // Stick the file content in the message field of the success xml and the file path into the debuginfo node
                        await response.OK(GenerateSuccessXml(HttpUtility.HtmlEncode(historicalPdfSourceDataResult.XmlPayload.OuterXml), baseDebugInfo), ReturnTypeEnum.Xml, true);
                    }

                }
                catch (Exception ex)
                {
                    HandleError(ReturnTypeEnum.Xml, "There was an error retrieving the filing data", $"error: {ex}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");
                }

            }
            else
            {
                HandleError(ReturnTypeEnum.Xml, "Not authenticated", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}", 403);
            }
        }


        /// <summary>
        /// Compiles a PDF source data document from an earlier commit in the version control (GIT) system
        /// </summary>
        /// <param name="commitIdentifier">GIT tag or hash of commit to use for the historical version</param>
        /// <param name="sections">'all' or a comma-delimited string of sections to include in the data</param>
        /// <param name="renderScope">'single-section' or 'include-children' are supported</param>
        /// <param name="debugRoutine">Dumps debug information to the console</param>
        /// <param name="extractPathOs">Optional path where the historical data should be extracted</param>
        /// <returns></returns>
        public static TaxxorReturnMessage RetrievePdfHistoricalSourceData(string commitIdentifier, string sections, string renderScope, bool debugRoutine, string extractPathOs = null, string projectId = null)
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            RequestVariables reqVars = RetrieveRequestVariables(context);

            var deleteExtractedFiles = true;

            try
            {
                // - Path to extract directory
                if (extractPathOs == null) extractPathOs = applicationRootPathOs + "/temp/" + GenerateRandomString(12, false);

                var baseDebugInfo = $"commitIdentifier: {commitIdentifier}, extractPathOs: {extractPathOs}";

                // - Calculate paths
                var xpath = "/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/location[@id='reportdataroot']";
                var gitDirectory = RetrieveNodeValueIfExists(xpath, xmlApplicationConfiguration);
                if (string.IsNullOrEmpty(gitDirectory)) return new TaxxorReturnMessage(false, "Could not locate repository", $"xpath: {xpath}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");

                var cmsDataRootPathOs = dataRootPathOs + gitDirectory;
                if (!GitIsActiveRepository(cmsDataRootPathOs)) return new TaxxorReturnMessage(false, "Directory is not a GIT repository", $"gitDirectory: {gitDirectory}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");


                //
                // => Extract the GIT commit to a temporary location
                //
                var extractFolderPathOs = ExportGitWorkingDirectory(cmsDataRootPathOs, commitIdentifier, extractPathOs, projectVars.projectId);
                Console.WriteLine($"- extractFolderPathOs: {extractFolderPathOs}");

                //
                // => Compile hierarchy
                //
                var historicalDataRootFolderPathOs = $"{extractPathOs}/version_1/textual";
                var historicalHierarchyRootFolderPathOs = $"{extractPathOs}/version_1/metadata";
                if (!Directory.Exists(historicalDataRootFolderPathOs) || !Directory.Exists(historicalHierarchyRootFolderPathOs))
                {
                    return new TaxxorReturnMessage(false, "Could not locate historical data or hierarchy path", $"historicalDataRootFolderPathOs: {historicalDataRootFolderPathOs}, historicalHierarchyRootFolderPathOs: {historicalHierarchyRootFolderPathOs}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");
                }

                var currentHierarchyPathOs = CalculateHierarchyPathOs(reqVars, projectVars);
                var hierarchyFileName = Path.GetFileName(currentHierarchyPathOs);
                Console.WriteLine($"- hierarchyFileName: {hierarchyFileName}");
                var historicalHierarchyPathOs = $"{historicalHierarchyRootFolderPathOs}/{hierarchyFileName}";
                if (!File.Exists(historicalHierarchyPathOs)) return new TaxxorReturnMessage(false, "Could not locate historical hierarchy", $"historicalHierarchyPathOs: {historicalHierarchyPathOs}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");

                var xmlHierarchy = new XmlDocument();
                try
                {
                    xmlHierarchy.Load(historicalHierarchyPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "There was an error retrieving the loading the historical hierarchy XML data", ex.ToString());
                }

                //
                // => Add the article ID's to the hierarchy
                //
                if (projectId != null)
                {
                    ExtendHierarchyWithArticleIds(ref xmlHierarchy, projectId);
                }


                //
                // => Compile the source data
                //
                var result = RetrievePdfSourceData(xmlHierarchy, historicalDataRootFolderPathOs, sections, renderScope, debugRoutine);
                if (!result.Success) return result;


                //
                // => Cleanup directory where the content of the historical data was extracted
                //
                if (deleteExtractedFiles)
                {
                    try
                    {
                        DelTree(extractPathOs);
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogWarning(ex, $"Unable to remove the folder where we extracted the historical PDF data. error: {ex}");
                    }
                }

                // xmlPdfHistoricalData.Save($"{logRootPathOs}/______historical.xml");

                //
                // => Return the result
                //
                return new TaxxorReturnMessage(true, "Successfully generated content for a historical PDF file", result.XmlPayload, baseDebugInfo);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error retrieving the historical PDF data", ex.ToString());
            }


        }


        /// <summary>
        /// Compiles an XML Document containing the content of all the content data files of this project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RetrieveAllSourceData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted variables
            var projectId = request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var lang = request.RetrievePostedValue("lang", RegexEnum.None, false, ReturnTypeEnum.Xml, "all");
            var inUseString = request.RetrievePostedValue("inuse", RegexEnum.None, false, ReturnTypeEnum.Xml, "true");
            var inUse = (inUseString == "true");

            try
            {
                var xmlCompleteSourceData = RetrieveAllSourceData(reqVars, projectVars, lang, inUse);

                await response.OK(GenerateSuccessXml("Successfully retrieved complete source data overview", "", HttpUtility.HtmlEncode(xmlCompleteSourceData.OuterXml)), ReturnTypeEnum.Xml, true);
            }
            catch (Exception ex)
            {
                HandleError("There was an error creating a complete source data overview", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Compiles an XML Document containing the content of all the content data files of this project
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="onlyInUse"></param>
        /// <returns></returns>
        public static XmlDocument RetrieveAllSourceData(RequestVariables reqVars, ProjectVariables projectVars, string lang = "all", bool onlyInUse = true)
        {

            // Calculate the base path to the xml file that contains the section XML file
            var xpath = $"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/content_types/content_management/type[@id='regular']/xml";
            var nodeSourceDataLocation = xmlApplicationConfiguration.SelectSingleNode(xpath);
            if (nodeSourceDataLocation == null)
            {
                return GenerateErrorXml("Could not generate all source data because the project could not be located", $"xpath: {xpath}, stack-trace: {GetStackTrace()}");
            }

            var xmlSectionPathOs = CalculateFullPathOs(nodeSourceDataLocation, true);

            // The XML Document that we will be building up and return
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml("<div class=\"body-wrapper\"/>");
            var targetDocumentRootNode = xmlDocument.DocumentElement;

            var xmlSectionDataFolderPathOs = Path.GetDirectoryName(xmlSectionPathOs);

            var langFilter = "";
            if (lang != "all")
            {
                // <entry key="languages">en</entry>
                langFilter = $" and metadata/entry[@key='languages' and contains(text(), '{lang}')]";
            }

            var inUseFilter = "";
            if (onlyInUse)
            {
                inUseFilter = $" and metadata/entry[@key='inuse']='true'";
            }

            var contentLangFilter = "";
            if (lang != "all")
            {
                contentLangFilter = $"[@lang='{lang}']";
            }

            // Test if we have the content in the cache to generate the content
            var xPathContentFiles = $"/projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/data/content[@datatype='sectiondata'{langFilter}{inUseFilter}]";
            var nodeListContentItems = XmlCmsContentMetadata.SelectNodes(xPathContentFiles);
            if (nodeListContentItems.Count == 0)
            {
                var xmlContentMetadataProject = CompileCmsMetadata(projectVars.projectId, false);
                nodeListContentItems = xmlContentMetadataProject.SelectNodes(xPathContentFiles);
            }
            foreach (XmlNode nodeContentItem in nodeListContentItems)
            {
                var itemId = "";
                var nodeIds = nodeContentItem.SelectSingleNode("metadata/entry[@key='ids']");
                if (nodeIds != null)
                {
                    itemId = nodeIds.InnerText;
                    if (itemId.Contains(',')) itemId = itemId.SubstringBefore(",");
                }
                var dataRef = GetAttribute(nodeContentItem, "ref");

                // Calculate the path to the folder that contains the data files
                var sectionDataFilePathOs = $"{xmlSectionDataFolderPathOs}/{dataRef}";

                try
                {
                    var xmlSection = LoadAndResolveInlineFilingComposerData(reqVars, projectVars, sectionDataFilePathOs);

                    // Select the article content
                    var xPathArticle = $"/data/content{contentLangFilter}/*";
                    var nodeListArticles = xmlSection.SelectNodes(xPathArticle);

                    if (nodeListArticles.Count == 0)
                    {
                        appLogger.LogError($"Could not find article so the article is not added to the PDF. sectionDataFilePathOs: {sectionDataFilePathOs}, xPathArticle: {xPathArticle}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        foreach (XmlNode nodeSourceData in nodeListArticles)
                        {
                            SetAttribute(nodeSourceData, "data-hierarchical-level", "-1");
                            SetAttribute(nodeSourceData, "data-ref", dataRef);

                            // Import the fragment and sequentially add it to the root node
                            var importedNode = xmlDocument.ImportNode(nodeSourceData, true);
                            targetDocumentRootNode.AppendChild(importedNode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleError(ReturnTypeEnum.Xml, $"Could not load section XML data - path: {sectionDataFilePathOs}", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }

            // Return the document that we have compiled
            return xmlDocument;

        }

    }
}