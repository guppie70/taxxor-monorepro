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
using System.Xml.Linq;
using System.Xml.XPath;
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
        /// Custom attribute that we can use to decorate data Data Analysis methods
        /// </summary>
        [AttributeUsage(AttributeTargets.Method)]
        public class DataAnalysisAttribute : Attribute
        {
            public string Id { get; }
            public string Name { get; set; }
            public string Description { get; set; }

            public DataAnalysisAttribute(string id)
            {
                Id = id;
            }
        }

        /// <summary>
        /// Retrieves a list of all the data analysis methods available
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task DataAnalysisListScenarios(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);


            // Construct a response message for the client
            var xmlScenarios = new XmlDocument();
            xmlScenarios.AppendChild(xmlScenarios.CreateElement("scenarios"));
            var dataAnalysisListSorted = DataAnalysisList.OrderBy(o => o.Name);
            foreach (var dataAnalysisScenario in dataAnalysisListSorted)
            {
                var nodeScenario = xmlScenarios.CreateElement("scenario");

                var nodeScanarioId = xmlScenarios.CreateElement("id");
                nodeScanarioId.InnerText = dataAnalysisScenario.Id;
                nodeScenario.AppendChild(nodeScanarioId);

                var nodeScanarioName = xmlScenarios.CreateElement("name");
                nodeScanarioName.InnerText = dataAnalysisScenario.Name;
                nodeScenario.AppendChild(nodeScanarioName);

                var nodeScanarioDescription = xmlScenarios.CreateElement("description");
                nodeScanarioDescription.InnerText = dataAnalysisScenario.Description;
                nodeScenario.AppendChild(nodeScanarioDescription);

                xmlScenarios.DocumentElement.AppendChild(nodeScenario);
            }

            // Stick the file content in the message field of the success xml and the file path into the debuginfo node
            await response.OK(GenerateSuccessXml("Successfully retrieved data analysis scenario list", "", HttpUtility.HtmlEncode(xmlScenarios.OuterXml)), ReturnTypeEnum.Xml, true);
        }

        /// <summary>
        /// Retrieves the source XHTML data for the PDF Service to use as the source material
        /// </summary>
        /// <returns>The user data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task AnalyzeEditorData(HttpRequest request, HttpResponse response, RouteData routeData)
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
            projectVars.editorContentType = request.RetrievePostedValue("ctype", true, ReturnTypeEnum.Xml);
            projectVars.reportTypeId = request.RetrievePostedValue("rtype", true, ReturnTypeEnum.Xml);
            var analysisType = request.RetrievePostedValue("analysistype", RegexEnum.None, true, ReturnTypeEnum.Xml);

            var baseDebugInfo = $"analysisType: {analysisType}, projectId: '{projectId}', versionId: '{versionId}', dataType: '{dataType}', sections: '{sections}', projectVars.editorContentType: '{projectVars.editorContentType}', projectVars.reportTypeId: '{projectVars.reportTypeId}'";

            // HandleError(reqVars, "Thrown on purpose Taxxor Document Store - RetrieveFactIds()", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");

            if (projectVars.currentUser.IsAuthenticated)
            {
                if (!ValidateCmsPostedParameters(projectId, versionId, dataType)) HandleError(reqVars, "There was an error validating the posted values", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");

                try
                {
                    var xmlToAnalyze = new XmlDocument();

                    switch (projectVars.editorId)
                    {
                        default:
                            // Retrieve the XML data that we need to return
                            var result = RetrievePdfSourceData(projectId, versionId, sections, renderScope, true);
                            if (!result.Success) HandleError(ReturnTypeEnum.Xml, result);

                            xmlToAnalyze.ReplaceContent(result.XmlPayload, true);
                            break;
                    }

                    // Add context information with steering attributes
                    xmlToAnalyze.DocumentElement.SetAttribute("data-pid", projectId);
                    xmlToAnalyze.DocumentElement.SetAttribute("data-vid", versionId);
                    xmlToAnalyze.DocumentElement.SetAttribute("data-ctype", projectVars.editorContentType);
                    xmlToAnalyze.DocumentElement.SetAttribute("data-rtype", projectVars.reportTypeId);
                    xmlToAnalyze.DocumentElement.SetAttribute("data-ocvariantid", projectVars.outputChannelVariantId);
                    xmlToAnalyze.DocumentElement.SetAttribute("data-oclang", projectVars.outputChannelVariantLanguage);


                    if (debugRoutine)
                    {
                        xmlToAnalyze.Save($"{logRootPathOs}/_data-analysis-input.xml");
                    }

                    var xmlToReturn = new XmlDocument();
                    // Determine the scenario to run

                    if (DataAnalysisMethods.TryGetValue(analysisType, out DataAnalysisDelegate method))
                    {
                        // Invoke the transformation method
                        xmlToReturn = method(xmlToAnalyze);

                    }
                    else
                    {
                        appLogger.LogError($"Could not map transformMethodId: {analysisType} to a method that we can invoke...");
                        HandleError(ReturnTypeEnum.Xml, "Unknown analysis scenario", $"projectVars.editorId: {projectVars.editorId}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");
                    }

                    // GenerateSuccessXml(HttpUtility.HtmlEncode(xmlToReturn.OuterXml), baseDebugInfo)

                    // Stick the file content in the message field of the success xml and the file path into the debuginfo node
                    await response.OK(new TaxxorReturnMessage(true, "Successfully analyzed data", xmlToReturn, ""), ReturnTypeEnum.Xml, true);
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
        /// Find the fact ID's in the content of an output channel
        /// </summary>
        /// <param name="xmlToAnalyze"></param>
        /// <returns></returns>
        [DataAnalysis("analyzefactids", Name = "SDE report: XBRL usage", Description = "Reports all SDE's used in the filing by XBRL level. Reported XBRL levels: 0 = Complete report, 1 = Section level, 2 = Text blocks within sections, 3 = Table tags, 4 = Detailed fact tags")]
        public static XmlDocument AnalyzeFactIds(XmlDocument xmlToAnalyze)
        {
            return RenderStructuredDataElementOverview(xmlToAnalyze);
        }

        /// <summary>
        /// Analyze structured data elements in the content
        /// </summary>
        /// <param name="xmlToAnalyze"></param>
        /// <returns></returns>
        [DataAnalysis("analyzestructureddataelements", Name = "SDE report: fact-id's and values", Description = "Returns all SDE's used in an output channel including their values and the number of times the fact-id occurs in the report. Usually fact-id's need to be unique.")]
        public static XmlDocument AnalyzeStructuredDataElements(XmlDocument xmlToAnalyze)
        {
            // Retrieve context information from the steering attributes set on the root element
            var projectId = xmlToAnalyze.DocumentElement.GetAttribute("data-pid");
            var outputVariantId = xmlToAnalyze.DocumentElement.GetAttribute("data-ocvariantid");
            var contentType = xmlToAnalyze.DocumentElement.GetAttribute("data-ctype");
            var reportType = xmlToAnalyze.DocumentElement.GetAttribute("data-rtype");
            var outputChannelLanguage = xmlToAnalyze.DocumentElement.GetAttribute("data-oclang");

            var dataReferencesCollection = new Dictionary<string, XmlDocument>();

            // Path where we can find the raw content
            var sectionDataRootPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId);


            var xmlToReturn = new XmlDocument();
            xmlToReturn.LoadXml("<result><summary><count type=\"total-structureddata-elements\" value=\"\"/><count type=\"unique-structureddata-elements\" value=\"\"/></summary><elements/><details/></result>");

            var nodeRootStructuredDataElements = xmlToReturn.SelectSingleNode("/result/elements");
            var nodeRootDetails = xmlToReturn.SelectSingleNode("/result/details");

            var totalStructuredDataElements = 0;
            var uniqueStructuredDataElements = 0;

            var nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlToAnalyze, false);
            totalStructuredDataElements = nodeListStructuredDataElements.Count;

            foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
            {
                // Retrieve information about this structured data element
                var factGuid = nodeStructuredDataElement.GetAttribute("data-fact-id");
                var factStatus = nodeStructuredDataElement.GetAttribute("data-syncstatus") ?? "unknown";
                string factValue = nodeStructuredDataElement.InnerText;

                if (factGuid != null)
                {
                    var dataRef = "unknown";
                    var nodeArticle = nodeStructuredDataElement.SelectSingleNode("ancestor-or-self::article");
                    if (nodeArticle != null)
                    {
                        dataRef = nodeArticle.GetAttribute("data-ref") ?? dataRef;
                    }
                    else
                    {
                        appLogger.LogWarning("Could not find article");
                    }

                    // Create a node in the details list if needed
                    var nodeDataRef = nodeRootDetails.SelectSingleNode($"datafile[@src='{dataRef}']");
                    if (nodeDataRef == null)
                    {
                        nodeDataRef = xmlToReturn.CreateElement("datafile");
                        nodeDataRef.SetAttribute("src", dataRef);
                        nodeRootDetails.AppendChild(nodeDataRef);
                    }

                    var nodeSummaryStructuredDataElement = _createStructuredDataCacheElement(xmlToReturn, factGuid, factValue, factStatus, outputChannelLanguage);
                    nodeDataRef.AppendChild(nodeSummaryStructuredDataElement);

                    //
                    // => Add the original SDE value (as they are set in the unparsed document)
                    //

                    // - Expand the collection if needed
                    if (!dataReferencesCollection.ContainsKey(dataRef))
                    {
                        var xmlDataReference = new XmlDocument();
                        xmlDataReference.Load($"{sectionDataRootPathOs}/{dataRef}");
                        dataReferencesCollection.Add(dataRef, xmlDataReference);
                    }

                    // Find the value of the SDE in the raw data     
                    var nodeSdeOriginal = dataReferencesCollection[dataRef].SelectSingleNode($"/data/content[@lang='{outputChannelLanguage}']/article{_retrieveStructuredDataElementsBaseXpath().Replace("@data-fact-id", $"@data-fact-id='{factGuid}'")}");
                    if (nodeSdeOriginal != null)
                    {
                        var nodeOriginalValue = xmlToReturn.CreateElementWithText("value-orig", nodeSdeOriginal.InnerText ?? "");
                        nodeSummaryStructuredDataElement.AppendChild(nodeOriginalValue);
                    }





                    // Create a unique list of elements with a counter in the elements node
                    var nodeUniqueStructuredDataElement = nodeRootStructuredDataElements.SelectSingleNode($"element[@id={GenerateEscapedXPathString(factGuid)}]");
                    if (nodeUniqueStructuredDataElement == null)
                    {
                        nodeUniqueStructuredDataElement = _createStructuredDataCacheElement(xmlToReturn, factGuid, factValue, factStatus, outputChannelLanguage);
                        nodeUniqueStructuredDataElement.SetAttribute("count", "1");
                        nodeRootStructuredDataElements.AppendChild(nodeUniqueStructuredDataElement);
                        uniqueStructuredDataElements++;
                    }
                    else
                    {
                        // Increase the counter
                        var counterString = nodeUniqueStructuredDataElement.GetAttribute("count");

                        if (!Int32.TryParse(counterString, out int counter)) Console.WriteLine("String could not be parsed.");
                        counter = counter + 1;
                        nodeUniqueStructuredDataElement.SetAttribute("count", counter.ToString());
                    }







                }
                else
                {
                    appLogger.LogWarning($"Could not retrieve enough structured data information: factGuid: {factGuid}, factValue: {factValue}");
                }
            }

            // Order the list to put the most counted unique elements on top
            var xDocumentToReturn = XDocument.Parse(xmlToReturn.OuterXml);
            var baseElement = xDocumentToReturn.XPathSelectElement("/result/elements");
            var sortedElements = baseElement.Elements()
                .OrderByDescending(e => e.Attribute("count").Value)
                .ToList(); // this call may or may not be needed, but just in case...
            baseElement.ReplaceAll(sortedElements);

            // Convert the xDocument back to XML Document object for further processing
            using (var xmlReader = xDocumentToReturn.CreateReader())
            {
                xmlToReturn.Load(xmlReader);
            }



            // Set the statistics
            xmlToReturn.SelectSingleNode("/result/summary/count[@type='total-structureddata-elements']").SetAttribute("value", totalStructuredDataElements.ToString());
            xmlToReturn.SelectSingleNode("/result/summary/count[@type='unique-structureddata-elements']").SetAttribute("value", uniqueStructuredDataElements.ToString());

            xmlToReturn.Save(logRootPathOs + "/analyzestructureddataelements.xml");
            return xmlToReturn;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlToAnalyze"></param>
        /// <returns></returns>
        [DataAnalysis("renderstructureddataelementlist", Name = "SDE report: fact-id list", Description = "Renders a complete list containing all the fact-id's used by the Structured Data Elements in the report")]
        public static XmlDocument RenderStructuredDataElementList(XmlDocument xmlToAnalyze)
        {
            var errorMessage = "There was a problem rendering the structured data elements list";
            try
            {
                var xmlToReturn = new XmlDocument();
                xmlToReturn.AppendChild(xmlToReturn.CreateElement("facts"));
                var projectId = xmlToAnalyze.DocumentElement.GetAttribute("data-pid");
                if (!string.IsNullOrEmpty(projectId)) xmlToReturn.DocumentElement.SetAttribute("pid", projectId);
                var outputChannelVariantId = xmlToAnalyze.DocumentElement.GetAttribute("data-ocvariantid") ?? "";
                var langToCheck = "all";
                if (!string.IsNullOrEmpty(outputChannelVariantId))
                {
                    // Add the output channel variant id to the result
                    xmlToReturn.DocumentElement.SetAttribute("ocvariantid", outputChannelVariantId);

                    // Retrieve the language used in this output channel
                    langToCheck = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectId, outputChannelVariantId);
                    xmlToReturn.DocumentElement.SetAttribute("lang", langToCheck);
                }

                appLogger.LogInformation($"projectId: {projectId}, outputChannelVariantId: {outputChannelVariantId}, langToCheck: {langToCheck}");

                var nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlToAnalyze, false, langToCheck);

                var uniqueFactIdsChecker = new List<string>();
                var noneUniqueFactIds = new List<string>();


                var emptyFactIdCounter = 0;
                foreach (XmlNode nodeSde in nodeListStructuredDataElements)
                {
                    var factId = nodeSde.GetAttribute("data-fact-id");
                    if (string.IsNullOrEmpty(factId))
                    {
                        emptyFactIdCounter++;
                    }
                    else
                    {
                        if (uniqueFactIdsChecker.Contains(factId))
                        {
                            // This fact already exists in the content
                        }
                        else
                        {
                            var nodeFact = xmlToReturn.CreateElement("fact");
                            nodeFact.SetAttribute("id", factId);

                            // Find the data reference where we have found this SDE
                            var nodeArticle = nodeSde.SelectSingleNode("ancestor::article");
                            if (nodeArticle != null)
                            {
                                var dataReference = nodeArticle.GetAttribute("data-ref");
                                if (!string.IsNullOrEmpty(dataReference))
                                {
                                    nodeFact.SetAttribute("data-ref", dataReference);
                                }
                                else
                                {
                                    appLogger.LogWarning($"Unable to find datareference for SDE with ID: {factId}");
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to find article node for SDE with ID: {factId}");
                            }

                            xmlToReturn.DocumentElement.AppendChild(nodeFact);
                            uniqueFactIdsChecker.Add(factId);
                        }
                    }
                }

                xmlToReturn.DocumentElement.SetAttribute("count", nodeListStructuredDataElements.Count.ToString());

                return xmlToReturn;
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, errorMessage);
                return GenerateErrorXml(errorMessage, ex.ToString());
            }


        }



        /// <summary>
        /// Analyzes the links in the document
        /// </summary>
        /// <param name="xmlToAnalyze"></param>
        /// <returns></returns>
        [DataAnalysis("analyzelinks", Name = "Link checker", Description = "Analyzes the different types of links in the document, tries to resolve them, and potentially reports if the links are broken.")]
        public static XmlDocument AnalyzeLinks(XmlDocument xmlToAnalyze)
        {
            var outputVariantId = xmlToAnalyze.DocumentElement.GetAttribute("data-ocvariantid");



            var xmlToReturn = new XmlDocument();
            xmlToReturn.LoadXml("<links><broken/><category type=\"section\"/><category type=\"external\"/><category type=\"note\"/><category type=\"email\"/><category type=\"footnote\"/><category type=\"undefined\"/></links>");

            // Query the document and append matches to the result document
            var xPath = "//a";
            var nodeListLinks = xmlToAnalyze.SelectNodes(xPath);

            var nodeTypeSection = xmlToReturn.SelectSingleNode("/links/category[@type='section']");
            var nodeTypeExternal = xmlToReturn.SelectSingleNode("/links/category[@type='external']");
            var nodeTypeNote = xmlToReturn.SelectSingleNode("/links/category[@type='note']");
            var nodeTypeEmail = xmlToReturn.SelectSingleNode("/links/category[@type='email']");
            var nodeTypeFootnote = xmlToReturn.SelectSingleNode("/links/category[@type='footnote']");

            var nodeTypeUndefined = xmlToReturn.SelectSingleNode("/links/category[@type='undefined']");

            var nodeRootBrokenLinks = xmlToReturn.SelectSingleNode("/links/broken");


            // Inject the facts into the result tree
            var countDefined = 0;
            var countUndefined = 0;
            var countBroken = 0;
            foreach (XmlNode nodeLink in nodeListLinks)
            {
                if (IsLinkRelavant(nodeLink))
                {
                    // Get information about the article that the link is in
                    var nodeSourceArticle = nodeLink.SelectSingleNode("ancestor::article");
                    var sourceArticleId = "";
                    var sourceDataRef = "";
                    if (nodeSourceArticle != null)
                    {
                        sourceArticleId = GetAttribute(nodeSourceArticle, "id");
                        sourceDataRef = nodeSourceArticle.GetAttribute("data-ref") ?? "unknown";
                    }

                    // Import the link into the document we are building up, and add the context so that we can trace it back to the article it lives in
                    XmlNode nodeImported = xmlToReturn.ImportNode(nodeLink, true);
                    nodeImported.SetAttribute("sourceref", sourceDataRef);

                    var linkType = GetAttribute(nodeLink, "data-link-type");
                    if (string.IsNullOrEmpty(linkType))
                    {
                        var type = GetAttribute(nodeLink, "type");
                        if (!string.IsNullOrEmpty(type))
                        {
                            nodeTypeFootnote.AppendChild(nodeImported);
                            countDefined++;
                        }
                        else
                        {
                            nodeTypeUndefined.AppendChild(nodeImported);
                            countUndefined++;
                        }

                    }
                    else
                    {
                        switch (linkType)
                        {
                            case "section":
                                nodeTypeSection.AppendChild(nodeImported);
                                countDefined++;
                                break;
                            case "external":
                                nodeTypeExternal.AppendChild(nodeImported);
                                countDefined++;
                                break;
                            case "email":
                                nodeTypeEmail.AppendChild(nodeImported);
                                countDefined++;
                                break;
                            case "note":
                                nodeTypeNote.AppendChild(nodeImported);
                                countDefined++;
                                break;
                            case "footnote":
                                nodeTypeFootnote.AppendChild(nodeImported);
                                countDefined++;
                                break;

                            default:
                                nodeTypeUndefined.AppendChild(nodeImported);
                                countUndefined++;
                                break;
                        }
                    }

                    // Check if the link is broken or not
                    //Console.WriteLine(nodeInternalLink.OuterXml);

                    if (linkType == "section" || linkType == "note" || string.IsNullOrEmpty(linkType))
                    {
                        var nodeInternalLink = nodeLink;
                        var linkUri = GetAttribute(nodeInternalLink, "href");
                        if (string.IsNullOrEmpty(linkUri))
                        {
                            appLogger.LogWarning($"Link without target in @href detected, {nodeInternalLink.OuterXml}");
                        }
                        else
                        {
                            if (linkUri.StartsWith('#'))
                            {





                                // Test if the article target exists
                                linkUri = linkUri.SubstringAfter("#");
                                var nodeTargetArticle = xmlToAnalyze.SelectSingleNode($"//article[@id='{linkUri}']");
                                if (nodeTargetArticle == null)
                                {


                                    var nodeBrokenLink = xmlToReturn.CreateElementWithText("link", nodeInternalLink.InnerText);
                                    nodeBrokenLink.SetAttribute("sourceref", sourceDataRef);
                                    nodeBrokenLink.SetAttribute("href", GetAttribute(nodeInternalLink, "href"));

                                    nodeRootBrokenLinks.AppendChild(nodeBrokenLink);

                                    countBroken++;
                                }
                            }
                            else if (linkUri.StartsWith("http"))
                            {

                            }

                        }
                    }
                }



            }

            SetAttribute(xmlToReturn.DocumentElement, "countbroken", countBroken.ToString());
            SetAttribute(xmlToReturn.DocumentElement, "countdefined", countDefined.ToString());
            SetAttribute(xmlToReturn.DocumentElement, "countundefined", countUndefined.ToString());

            return xmlToReturn;

            bool IsLinkRelavant(XmlNode nodeLink)
            {

                switch (TaxxorClientId)
                {
                    case "philips":
                        if (outputVariantId.StartsWith("ar") || outputVariantId.StartsWith("20f"))
                        {
                            var classNameIrrelevant = outputVariantId.StartsWith("ar") ? "data20-f" : "dataar";
                            if (nodeLink.ParentNode?.GetAttribute("class")?.Contains(classNameIrrelevant) ?? false)
                            {
                                return false;
                            }
                            else if (nodeLink.ParentNode.ParentNode?.GetAttribute("class")?.Contains(classNameIrrelevant) ?? false)
                            {
                                return false;
                            }
                        }

                        break;

                    default:


                        break;
                }





                return true;
            }
        }

        [DataAnalysis("analyzeexternaltablerows", Name = "Find tables with empty rows", Description = "Returns the tables that contain rows without any cells. This can occur if the tables heve not been correctly marked in the Taxxor Excel plugin")]
        public static XmlDocument AnalyzeTableRows(XmlDocument xmlToAnalyze)
        {
            var xmlToReturn = new XmlDocument();
            xmlToReturn.LoadXml("<report/>");

            // Loop through the articles
            var nodeListArticles = xmlToAnalyze.SelectNodes("/div/article");
            foreach (XmlNode nodeArticle in nodeListArticles)
            {
                var articleId = GetAttribute(nodeArticle, "id") ?? "unknown";

                var newNodeArticle = xmlToReturn.CreateElement("article");
                SetAttribute(newNodeArticle, "id", articleId);


                // Find external tables
                var xPathTables = $"*//table[@data-workbookreference]";
                var nodeListTables = nodeArticle.SelectNodes(xPathTables);
                var problemTablesFound = false;
                foreach (XmlNode nodeTable in nodeListTables)
                {
                    var tableId = GetAttribute(nodeTable, "id") ?? "unknown";

                    // Find cells without a paragraph in the div
                    var xPathProblemRows = $"*//tr[not(td) and not(th)]";
                    var nodeListProblemRows = nodeTable.SelectNodes(xPathProblemRows);
                    if (nodeListProblemRows.Count > 0)
                    {
                        problemTablesFound = true;

                        var newNodeTable = xmlToReturn.CreateElement("table");
                        SetAttribute(newNodeTable, "id", tableId);
                        SetAttribute(newNodeTable, "problemrowcount", nodeListProblemRows.Count.ToString());
                        newNodeArticle.AppendChild(newNodeTable);
                    }
                }

                if (problemTablesFound)
                {
                    xmlToReturn.DocumentElement.AppendChild(newNodeArticle);
                }
            }

            return xmlToReturn;
        }


        /// <summary>
        /// Returns the tables that do not have a proper ID or no ID at all
        /// </summary>
        /// <param name="xmlToAnalyze"></param>
        /// <returns></returns>
        [DataAnalysis("analyzetableswithoutid", Name = "Find tables without proper ID or class", Description = "Returns the tables that do not have a proper ID/class or no ID at all")]
        public static XmlDocument FindTablesWithoutId(XmlDocument xmlToAnalyze)
        {
            var xmlToReturn = new XmlDocument();
            xmlToReturn.LoadXml("<report/>");

            // Loop through the articles
            var nodeListArticles = xmlToAnalyze.SelectNodes("/div/article");
            foreach (XmlNode nodeArticle in nodeListArticles)
            {
                var articleId = GetAttribute(nodeArticle, "id") ?? "unknown";

                var newNodeArticle = xmlToReturn.CreateElement("article");
                SetAttribute(newNodeArticle, "id", articleId);


                // Find tables
                var xPathTables = $"*//table";
                var nodeListTables = nodeArticle.SelectNodes(xPathTables);
                foreach (XmlNode nodeTable in nodeListTables)
                {
                    List<string> findings = new();

                    // Check table ID
                    var tableId = GetAttribute(nodeTable, "id") ?? "unknown";
                    switch (tableId)
                    {
                        case "unknown":
                            findings.Add("Table without ID");
                            break;

                        case string a when !a.StartsWith("table_"):
                            findings.Add($"Table without correct ID ({tableId})");
                            break;
                    }

                    var nodeTableWrapper = nodeTable.ParentNode;
                    if (nodeTableWrapper != null)
                    {
                        var wrapperClass = nodeTableWrapper.GetAttribute("class") ?? "unknown";
                        switch (wrapperClass)
                        {
                            case "unknown":
                                findings.Add("Table wrapper without class");
                                break;
                            case string a when !a.Contains("table-wrapper"):
                                findings.Add($"Table does not contain correct wrapper class ({wrapperClass})");
                                break;
                        }


                        var wrapperId = nodeTableWrapper.GetAttribute("id") ?? "unknown";
                        switch (wrapperId)
                        {
                            case "unknown":
                                findings.Add("Table wrapper without ID");
                                break;

                            case string a when !a.StartsWith("tablewrapper_"):
                                findings.Add($"Table wrapper without correct ID ({wrapperId})");
                                break;
                        }
                    }
                    else
                    {
                        findings.Add("Table without a table wrapper element");
                    }

                    if (findings.Count > 0)
                    {
                        var newTableIssue = xmlToReturn.CreateElement("table-issue");
                        foreach (var finding in findings)
                        {
                            var nodeFinding = xmlToReturn.CreateElementWithText("finding", finding);
                            newTableIssue.AppendChild(nodeFinding);
                        }

                        var nodeTableContent = xmlToReturn.CreateElementWithText("content", TruncateString(nodeTable.OuterXml, 800));
                        newTableIssue.AppendChild(nodeTableContent);
                        newNodeArticle.AppendChild(newTableIssue);
                        xmlToReturn.DocumentElement.AppendChild(newNodeArticle);
                    }
                }
            }

            return xmlToReturn;
        }

        /// <summary>
        /// Analyzes Structured Data Elements in a document
        /// </summary>
        /// <param name="xmlToAnalyze"></param>
        /// <returns></returns>
        [DataAnalysis("analyzestructureddataelementsduplicates", Name = "SDE report: duplicate fact ID's", Description = "Finds Structured Data Elements in the content that have a fact-id which is used elsewhere as well. Usually each structured data element needs to have a unique fact-id.")]
        public static XmlDocument AnalyzeStructuredDataEelements(XmlDocument xmlToAnalyze)
        {
            var xmlToReturn = new XmlDocument();
            xmlToReturn.LoadXml("<report><summary/><details><selectors/><not_unique/></details><errors/></report>");

            var nodeSummary = xmlToReturn.SelectSingleNode("/report/summary");
            var nodeDetails = xmlToReturn.SelectSingleNode("/report/details");
            var nodeNotUniqueRoot = nodeDetails.SelectSingleNode("not_unique");
            var nodeSelectors = nodeDetails.SelectSingleNode("selectors");

            var nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlToAnalyze, false);

            var nodeTotalSde = xmlToReturn.CreateElementWithText("total_sde", nodeListStructuredDataElements.Count.ToString());
            nodeSummary.AppendChild(nodeTotalSde);

            var uniqueFactIdsChecker = new List<string>();
            var noneUniqueFactIds = new List<string>();


            var emptyFactIdCounter = 0;
            foreach (XmlNode nodeSde in nodeListStructuredDataElements)
            {
                var factId = nodeSde.GetAttribute("data-fact-id");
                if (string.IsNullOrEmpty(factId))
                {
                    emptyFactIdCounter++;
                }
                else
                {
                    if (uniqueFactIdsChecker.Contains(factId))
                    {
                        // This fact already exists in the content
                        noneUniqueFactIds.Add(factId);
                    }
                    else
                    {
                        uniqueFactIdsChecker.Add(factId);
                    }
                }
            }

            var noneUniqueFactNodes = new List<XmlNode>();
            foreach (XmlNode nodeSde in nodeListStructuredDataElements)
            {
                var factId = nodeSde.GetAttribute("data-fact-id");
                if (noneUniqueFactIds.Contains(factId)) noneUniqueFactNodes.Add(nodeSde);
            }


            // Log details about facts that are not unique so that we can find them easier
            foreach (XmlNode nodeFact in noneUniqueFactNodes)
            {
                var factId = nodeFact.GetAttribute("data-fact-id");
                var factValue = nodeFact.InnerText;

                var nodeGroup = nodeNotUniqueRoot.SelectSingleNode($"fact-group[@data-fact-id={GenerateEscapedXPathString(factId)}]");
                if (nodeGroup == null)
                {
                    appLogger.LogInformation("NOT Exists");
                    var nodeNewGroup = xmlToReturn.CreateElement("fact-group");
                    nodeGroup = nodeNotUniqueRoot.AppendChild(nodeNewGroup);
                    nodeGroup.SetAttribute("data-fact-id", factId);
                }
                else
                {
                    appLogger.LogInformation("Exists");
                }

                var nodeFactDetails = xmlToReturn.CreateElementWithText("fact", factValue);

                var nodeContainingTable = nodeFact.SelectSingleNode("ancestor::table");
                if (nodeContainingTable == null)
                {
                    // nodeFactDetails.SetAttribute("tableid", "");
                }
                else
                {
                    nodeFactDetails.SetAttribute("tableid", nodeContainingTable.GetAttribute("id") ?? "unkown");
                }

                // Footnotes with SDE's can occur multiple times in the document so these are most likely false-positives
                var nodeContainingFootnoteWrapper = nodeFact.SelectSingleNode("ancestor::span[contains(@class, 'footnote')]");
                if (nodeContainingFootnoteWrapper != null)
                {
                    nodeFactDetails.SetAttribute("footnoteid", nodeContainingFootnoteWrapper.GetAttribute("data-footnoteid") ?? "unkown");
                }

                var nodeContainingArticle = nodeFact.SelectSingleNode("ancestor::article");
                nodeFactDetails.SetAttribute("articleid", nodeContainingArticle.GetAttribute("id") ?? "unknown");
                nodeFactDetails.SetAttribute("articletitle", nodeContainingArticle.SelectSingleNode(".//h1")?.InnerText ?? "unknown");


                nodeGroup.AppendChild(nodeFactDetails);


            }

            //
            // => Fill the selectors node with the non-unique facts so that we can use it in a selector
            //
            var xPathNonUniqueSelector = "[";
            for (int i = 0; i < noneUniqueFactIds.Count; i++)
            {
                // Console.WriteLine(noneUniqueFactIds[i]);
                xPathNonUniqueSelector += $"@data-fact-id='{noneUniqueFactIds[i]}'";
                if (i != noneUniqueFactIds.Count - 1)
                {
                    xPathNonUniqueSelector += " or ";
                }
            }
            xPathNonUniqueSelector += "]";
            var nodeXpath = xmlToReturn.CreateElementWithText("xpath_none-unique", xPathNonUniqueSelector);
            nodeSelectors.AppendChild(nodeXpath);

            //
            // => Create summary
            //
            var nodeUniqueFactIdCounter = xmlToReturn.CreateElementWithText("sde_unique", (nodeListStructuredDataElements.Count - noneUniqueFactNodes.Count).ToString());
            nodeSummary.AppendChild(nodeUniqueFactIdCounter);

            var nodeNoneUniqueFactIdCounter = xmlToReturn.CreateElementWithText("sde_not_unique", noneUniqueFactNodes.Count.ToString());
            nodeSummary.AppendChild(nodeNoneUniqueFactIdCounter);

            var nodeEmptyFactIdCounter = xmlToReturn.CreateElementWithText("sde_without_id", emptyFactIdCounter.ToString());
            nodeSummary.AppendChild(nodeEmptyFactIdCounter);


            // Loop through the articles
            // var nodeListArticles = xmlToAnalyze.SelectNodes("/data/content/*");
            // foreach (XmlNode nodeArticle in nodeListArticles)
            // {
            //     var articleId = GetAttribute(nodeArticle, "id") ?? "unknown";

            //     var newNodeArticle = xmlToReturn.CreateElement("article");
            //     SetAttribute(newNodeArticle, "id", articleId);


            //     // Find external tables
            //     var xPathTables = $"*//table[@data-workbookreference]";
            //     var nodeListTables = nodeArticle.SelectNodes(xPathTables);


            // }

            return xmlToReturn;
        }


        /// <summary>
        /// Checks how footnotes are used in the document and attempts to locate text fragments that might be candidates for a footnote
        /// </summary>
        /// <param name="xmlToAnalyze"></param>
        /// <returns></returns>
        [DataAnalysis("analyzefootnotes", Name = "Find footnote candidates", Description = "Checks how footnotes are used in the document and attempts to locate text fragments that might be candidates for a footnote bacause they are typed into the document using superscript (where a footnote reference should probably be used instead).")]
        public static XmlDocument AnalyzeFootnotes(XmlDocument xmlToAnalyze)
        {
            var xmlToReturn = new XmlDocument();
            xmlToReturn.LoadXml("<report><summary/><details><candidates/></details><errors/></report>");


            var nodeFootnoteCandidates = xmlToReturn.SelectSingleNode("/report/details/candidates");


            // Loop through the articles
            var nodeListArticles = xmlToAnalyze.SelectNodes("/div/article");
            foreach (XmlNode nodeArticle in nodeListArticles)
            {
                var articleId = GetAttribute(nodeArticle, "id") ?? "unknown";
                var articleDataRef = GetAttribute(nodeArticle, "data-ref") ?? "unknown";
                var articleLang = GetAttribute(nodeArticle, "lang") ?? GetAttribute(nodeArticle, "data-lang") ?? "unknown";
                var articleTitle = RetrieveFirstHeaderText(nodeArticle);
                if (string.IsNullOrEmpty(articleTitle)) articleTitle = "unknown";


                // Find paragraphs or other elements that may contain manually typed footnotes
                var xPath = $"*//*[(local-name()='p' or local-name()='li' or local-name()='h1' or local-name()='h2' or local-name()='h3' or local-name()='h4' or local-name()='h5' or local-name()='h6') and (contains(text(), '*') or contains(text(), ')'))]";
                var nodeListParagraphs = nodeArticle.SelectNodes(xPath);
                foreach (XmlNode nodeParagraph in nodeListParagraphs)
                {
                    var paragraphContent = RemoveNewLines(nodeParagraph.InnerXml);

                    if (nodeParagraph.SelectNodes("ancestor::td").Count == 0)
                    {
                        var reportThisParagraph = false;

                        // Typed "*" where this probably needs to be a footnote reference
                        if (RegExpTest(@"\w\*", paragraphContent, false))
                        {
                            reportThisParagraph = true;
                        }

                        // Typed ")" where this probably needs to be a footnote reference
                        if (!reportThisParagraph && RegExpTest(@"([a-zA-Z]|,|,\s|\s)\d{1}\)", paragraphContent, false))
                        {
                            reportThisParagraph = true;
                        }

                        if (reportThisParagraph)
                        {
                            var nodeCandidate = xmlToReturn.CreateElement("candidate");
                            nodeCandidate.SetAttribute("article-title", articleTitle);
                            nodeCandidate.SetAttribute("article-id", articleId);
                            nodeCandidate.SetAttribute("article-data-ref", articleDataRef);
                            // nodeCandidate.SetAttribute("article-lang", articleLang);

                            nodeCandidate.InnerText = paragraphContent;
                            nodeFootnoteCandidates.AppendChild(nodeCandidate);
                        }
                    }


                }


            }

            return xmlToReturn;
        }



    }
}