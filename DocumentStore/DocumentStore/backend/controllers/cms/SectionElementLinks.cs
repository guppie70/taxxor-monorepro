using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Resolves the Data Element and Data Section links in the content
        /// </summary>
        /// <param name="xmlSourceDocument"></param>
        /// <param name="projectVars"></param>
        /// <param name="dataFolderPathOs"></param>
        /// <returns></returns>
        public static XmlDocument ResolveSectionElementLinks(XmlDocument xmlSourceDocument, ProjectVariables projectVars, string dataFolderPathOs)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Global variables
            string? lang = projectVars.outputChannelVariantLanguage;

            var targetSectionXmlDocuments = new Dictionary<string, XmlDocument>();

            // Check if this is a regular article or a website data file
            var xpathElementLink = $"/data/content[@lang='{lang}']/*//*[@data-elementlink]";
            var xpathSectionLink = $"/data/content[@lang='{lang}']/*//*[@data-sectionlink]";

            var sourceArticleId = xmlSourceDocument.SelectSingleNode("//article")?.GetAttribute("id") ?? "unknown";

            // A) Check for links to elements
            var nodeListElementSourceLinks = xmlSourceDocument.SelectNodes(xpathElementLink);
            foreach (XmlNode nodeElementSourceLink in nodeListElementSourceLinks)
            {
                var linkStatus = "ok";
                var reference = GetAttribute(nodeElementSourceLink, "data-elementlink");

                var referenceElements = ParseTargetReference(reference);
                if (referenceElements.DataReference == "" || referenceElements.Selector == "")
                {
                    appLogger.LogWarning($"Could not parse element link: {reference}");
                    linkStatus = "wrong-format";
                }
                else
                {
                    var dataTargetProjectId = string.IsNullOrEmpty(referenceElements.ProjectId) ? projectVars.projectId : referenceElements.ProjectId;
                    var dataTargetReference = referenceElements.DataReference;
                    var dataTargetSelector = referenceElements.Selector;
                    var dataTargetSelectorType = referenceElements.SelectorType;
                    if (debugRoutine) appLogger.LogInformation($"dataTargetProjectId: {dataTargetProjectId}, dataTargetReference: {dataTargetReference}, dataTargetSelector: {dataTargetSelector}, dataTargetSelectorType: {dataTargetSelectorType}");

                    var dataTargetFilePathOs = $"{dataFolderPathOs}/{dataTargetReference}";

                    if (projectVars.projectId != dataTargetProjectId && System.Web.Context.Current != null)
                    {
                        //
                        // => Retrieve the path where we can locate the target data file
                        //                      
                        RequestVariables reqVars = RetrieveRequestVariables(System.Web.Context.Current);
                        var projectVariablesForTargetPath = new ProjectVariables
                        {
                            projectId = dataTargetProjectId
                        };
                        projectVariablesForTargetPath = FillProjectVariablesFromProjectId(projectVariablesForTargetPath).GetAwaiter().GetResult();

                        var nodeSourceDataLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(dataTargetProjectId) + "]/content_types/content_management/type[@id=" + GenerateEscapedXPathString("regular") + "]/xml");
                        var dataFilePathOs = CalculateFullPathOs(nodeSourceDataLocation, reqVars, projectVariablesForTargetPath);
                        dataTargetFilePathOs = Path.GetDirectoryName(dataFilePathOs) + $"/{dataTargetReference}";
                        if (debugRoutine) appLogger.LogInformation($"External dataTargetFilePathOs: {dataTargetFilePathOs}");
                    }

                    if (File.Exists(dataTargetFilePathOs))
                    {
                        var xmlSectionTarget = new XmlDocument();
                        try
                        {
                            if (!targetSectionXmlDocuments.ContainsKey(dataTargetReference))
                            {
                                xmlSectionTarget.Load(dataTargetFilePathOs);

                                ///
                                /// => Sync the table data into the source XML file where we want to pick the content from
                                /// 
                                try
                                {
                                    xmlSectionTarget.ReplaceContent(SyncExternalCachedTableData(xmlSectionTarget, projectVars.projectId, dataFolderPathOs, lang), true);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to sync externally linked table data in file {dataTargetFilePathOs}");
                                }

                                ///
                                /// => Sync the structured data element values into the source XML file where we want to pick the content from
                                /// 
                                try
                                {
                                    xmlSectionTarget.ReplaceContent(SyncStructuredDataElements(xmlSectionTarget, dataTargetFilePathOs, projectVars.projectId), true);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to sync structured data in file {dataTargetFilePathOs}");
                                }


                                ///
                                /// => Sync the footnote content into the source XML file where we want to pick the content from
                                /// 
                                try
                                {

                                    xmlSectionTarget.ReplaceContent(SyncFootnoteContent(xmlSectionTarget, projectVars, dataFolderPathOs), true);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to sync footnote content in file {dataTargetFilePathOs}");
                                }

                                ///
                                /// => Resolve the section element links it may contain
                                /// 
                                try
                                {

                                    xmlSectionTarget.ReplaceContent(ResolveSectionElementLinks(xmlSectionTarget, projectVars, dataFolderPathOs), true);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to sync footnote content in file {dataTargetFilePathOs}");
                                }

                                ///
                                /// => Process the images
                                /// 
                                try
                                {
                                    xmlSectionTarget.ReplaceContent(ProcessImagesOnLoad(xmlSectionTarget, null, projectVars, dataFolderPathOs), true);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to process images in file {dataTargetFilePathOs}");
                                }

                                ///
                                /// => Process the graph SVG data
                                /// 
                                try
                                {
                                    xmlSectionTarget.ReplaceContent(ProcessGraphsOnLoad(xmlSectionTarget, projectVars), true);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to inject graph SVG data in {dataTargetFilePathOs}");
                                }

                                ///
                                /// => Run custom Taxxor customer logic
                                /// 
                                try
                                {
                                    xmlSectionTarget.ReplaceContent(CustomFilingComposerDataGet(xmlSectionTarget, projectVars, dataFolderPathOs), true);
                                }
                                catch (System.Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to execute custom logic in file {dataTargetFilePathOs}");
                                }

                                targetSectionXmlDocuments.Add(dataTargetReference, xmlSectionTarget);
                            }
                            else
                            {
                                xmlSectionTarget = targetSectionXmlDocuments[dataTargetReference];
                            }


                            // Attempt to locate the target element
                            var xPathTargetElementLink = $"/data/content[@lang='{lang}']/*//*[@data-share-id='{dataTargetSelector}' or @guid='{dataTargetSelector}' or @id='{dataTargetSelector}']";
                            if (dataTargetSelectorType == "xpath") xPathTargetElementLink = $"/data/content[@lang='{lang}']/*{dataTargetSelector}";
                            // appLogger.LogInformation(xPathTargetElementLink);

                            // Insert all the content we can find in the source div
                            var nodeListTargetElements = xmlSectionTarget.SelectNodes(xPathTargetElementLink);
                            if (nodeListTargetElements.Count == 0)
                            {
                                linkStatus = "target-element-not-found";
                                appLogger.LogWarning($"Unable to locate {dataTargetSelector} in {dataTargetReference} (source article id: {sourceArticleId})");
                            }
                            else
                            {
                                foreach (XmlNode nodeTargetElement in nodeListTargetElements)
                                {
                                    // Optionally remove attributes from the source imported node
                                    var attributeNamesToRemoveString = GetAttribute(nodeElementSourceLink, "data-removerootattr") ?? "";
                                    var attributeNamesToRemove = new List<string>();
                                    if (attributeNamesToRemoveString.Contains(","))
                                    {
                                        attributeNamesToRemove = new List<string>(attributeNamesToRemoveString.Split(','));
                                    }
                                    else if (attributeNamesToRemoveString != "")
                                    {
                                        attributeNamesToRemove.Add(attributeNamesToRemoveString);
                                    }

                                    // Optionally add attributes to the root node
                                    var attributesToAddString = GetAttribute(nodeElementSourceLink, "data-addrootattr") ?? "";
                                    var attributeData = new Dictionary<string, string>();
                                    if (attributesToAddString != "")
                                    {
                                        var attrSets = new List<string>();
                                        if (attributesToAddString.Contains(","))
                                        {
                                            attrSets = new List<string>(attributesToAddString.Split(','));
                                        }
                                        else
                                        {
                                            attrSets.Add(attributesToAddString);
                                        }

                                        foreach (var attrInfo in attrSets)
                                        {
                                            var arrAttrInfo = attrInfo.Split(':');
                                            if (arrAttrInfo.Length == 2)
                                            {
                                                attributeData.Add(arrAttrInfo[0], arrAttrInfo[1]);
                                            }
                                            else
                                            {
                                                appLogger.LogWarning($"Not enough information to create attribute. attrInfo: {attrInfo} (source article id: {sourceArticleId})");
                                            }
                                        }
                                    }



                                    // Retrieve steering information about removal of footnotes
                                    var removeFootnotesString = GetAttribute(nodeElementSourceLink, "data-removefootnotes") ?? "false";
                                    var removeFootnotes = (removeFootnotesString == "true");

                                    // Optionally change the name of the node that we want to import
                                    var transformTo = GetAttribute(nodeElementSourceLink, "data-transformto") ?? "";

                                    // Test if we need to import a complete XHTML structure in the content or only the text of the target element 
                                    var importTextOnly = ((nodeElementSourceLink.GetAttribute("data-importtextonly") ?? "false") == "true");
                                    if (importTextOnly && transformTo == "")
                                    {
                                        // Set the text only
                                        nodeElementSourceLink.InnerText = nodeTargetElement.InnerText;
                                    }
                                    else if (importTextOnly && transformTo != "")
                                    {
                                        // Import the node
                                        var nodeImported = xmlSourceDocument.ImportNode(nodeTargetElement, true);

                                        // Insert the new element with the text from the source element
                                        var nodeContentElement = xmlSourceDocument.CreateElementWithText(transformTo, nodeTargetElement.InnerText ?? "");

                                        nodeElementSourceLink.AppendChild(nodeContentElement);
                                    }
                                    else
                                    {
                                        // Import the node
                                        var nodeImported = xmlSourceDocument.ImportNode(nodeTargetElement, true);

                                        // Remove attributes
                                        foreach (var attributeName in attributeNamesToRemove)
                                        {
                                            RemoveAttribute(nodeImported, attributeName);
                                        }

                                        if (removeFootnotes)
                                        {
                                            // Reference in table: <sup class="fn" contenteditable="false"><a href="#1605707698956" data-link-type="footnote" contenteditable="false" data-mce-href="#1605707698956">1</a></sup>
                                            // Content in table footer: <div class="footnote" id="1605707698956" data-syncstatus="200-ok"><sup class="fn" contenteditable="false">1</sup><span lang="en">Non-IFRS financial measure. Refer to <a href="#reconciliation-of-non-ifrs-information-q4" data-link-type="section" data-mce-href="#reconciliation-of-non-ifrs-information-q4" contenteditable="false">Reconciliation of non-IFRS information</a>.</span></div>
                                            // In text footnote: <sup class="fn" contenteditable="false"><a href="#1548437113445" data-link-type="footnote" data-mce-href="#1548437113445">*</a><span class="footnote section" id="1548437113445" contenteditable="false" data-syncstatus="200-ok"><sup class="fn" contenteditable="false">*</sup><span id="1548437113445"><span>Subject to SvB approval January 28, 2019</span></span></span></sup>
                                            var nodeListToRemove = nodeImported.SelectNodes("*//*[(local-name()='sup' and @class='fn') or (local-name()='span' and contains(@class, 'footnote'))]");
                                            RemoveXmlNodes(nodeListToRemove);
                                        }

                                        // Add attributes
                                        if (attributeData.Count > 0)
                                        {
                                            foreach (var pair in attributeData)
                                            {
                                                nodeImported.SetAttribute(pair.Key, pair.Value);
                                            }
                                        }

                                        // Append the node to the source div
                                        var nodeInserted = nodeElementSourceLink.AppendChild(nodeImported);



                                        if (!string.IsNullOrEmpty(transformTo))
                                        {
                                            RenameNode(nodeInserted, transformTo);
                                        }
                                    }



                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            var documentTitle = RetrieveNodeValueIfExists("//*[local-name()='h1' or local-name()='h2' or local-name()='h3']", xmlSourceDocument) ?? "";
                            appLogger.LogError(ex, $"Could not load target section file '{dataTargetFilePathOs}' linked from page with header '{documentTitle}' (source article id: {sourceArticleId})");
                            linkStatus = "target-section-error";
                        }
                    }
                    else
                    {
                        var documentTitle = RetrieveNodeValueIfExists("//*[local-name()='h1' or local-name()='h2' or local-name()='h3']", xmlSourceDocument) ?? "";
                        appLogger.LogError($"Target section file not available '{dataTargetFilePathOs}' linked from page with header '{documentTitle}' (source article id: {sourceArticleId})");
                        linkStatus = "target-section-not-available";
                    }

                }

                // Mark the element so that we can color it in the UI in case not available
                SetAttribute(nodeElementSourceLink, "data-linkstatus", linkStatus);
                SetAttribute(nodeElementSourceLink, "contenteditable", "false");
            }


            // B) Check for links to a full section content (to be implemented id needed)
            // TODO: Section links need to be implemented
            var nodeListSectionSourceLinks = xmlSourceDocument.SelectNodes(xpathSectionLink);
            foreach (XmlNode nodeSectionSourceLink in nodeListSectionSourceLinks)
            {
                var reference = GetAttribute(nodeSectionSourceLink, "data-sectionlink");

            }

            return xmlSourceDocument;


        }


        /// <summary>
        /// Removes the injected elements from the content before saving it to the disk
        /// </summary>
        /// <param name="xmlSourceDocument"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static XmlDocument RemoveInjectedSectionElements(XmlDocument xmlSourceDocument, ProjectVariables projectVars)
        {
            // Global variables
            string? lang = projectVars.outputChannelVariantLanguage;

            // Check if this is a regular article or a website data file
            var xpathElementLink = $"/data/content[@lang='{lang}']/*//*[@data-elementlink]";
            // var xpathSectionLink = $"/data/content[@lang='{lang}']/*//*[@data-sectionlink]";
            // if (xmlSourceDocument.SelectSingleNode("/data/content/article") == null)
            // {
            //     xpathElementLink = $"/data/content[@lang='{lang}']//*[namespace-uri()='http://www.w3.org/1999/xhtml' and local-name()='div' and @data-elementlink]";
            //     xpathSectionLink = $"/data/content[@lang='{lang}']//*[namespace-uri()='http://www.w3.org/1999/xhtml' and local-name()='div' and @data-sectionlink]";
            // }

            // A) Check for links to elements
            for (int i = 0; i < 2; i++)
            {
                var nodeListElementSourceLinks = xmlSourceDocument.SelectNodes(xpathElementLink);
                foreach (XmlNode nodeElementSourceLink in nodeListElementSourceLinks)
                {
                    // Remove the attributes that we have dynamically set on the element
                    RemoveAttribute(nodeElementSourceLink, "data-linkstatus");
                    RemoveAttribute(nodeElementSourceLink, "data-editable");
                    RemoveAttribute(nodeElementSourceLink, "contenteditable");

                    // Inject an empty comment so that the div does not become a self-closing div
                    nodeElementSourceLink.InnerXml = "<!-- . -->";
                }
            }


            return xmlSourceDocument;
        }


        /// <summary>
        /// Parses a reference to a section or element link in format bla.xml#guid or bla.xml/article
        /// </summary>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static (string SelectorType, string ProjectId, string DataReference, string Selector) ParseTargetReference(string reference)
        {
            var dataTargetReference = "";
            var selector = "";
            var selectorType = "";
            var projectId = "";

            if (reference.Contains("|"))
            {
                projectId = reference.SubstringBefore("|");
                reference = reference.SubstringAfter("|");
            }

            if (reference.Contains("#"))
            {
                selectorType = "id";
                string[] elements = reference.Split('#');
                dataTargetReference = elements[0];
                selector = elements[1];
                if (!dataTargetReference.EndsWith(".xml"))
                {
                    dataTargetReference = "";
                    selector = "";
                }
            }
            else if (reference.Contains("/"))
            {
                selectorType = "xpath";
                dataTargetReference = reference.SubstringBefore("/");
                selector = $"/{reference.SubstringAfter("/")}";

            }

            return (SelectorType: selectorType, ProjectId: projectId, DataReference: dataTargetReference, Selector: selector);
        }

    }
}