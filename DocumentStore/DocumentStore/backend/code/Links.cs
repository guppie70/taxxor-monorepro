using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{

    /// <summary>
    /// Logic for dealing with internal links
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Loops through the section links in an XML (section) document and 
        /// - checks if link target exists in the output channel hierarchy
        /// - forces the link text to become the text of the item in the hierarchy
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="context"></param>
        /// <param name="xmlHierarchy"></param>
        /// <returns></returns>
        public static XmlDocument ProcessInternalDocumentLinks(RequestVariables reqVars, ProjectVariables projectVars, XmlDocument xmlDocument, string xmlSectionPathOs, XmlDocument xmlHierarchy = null)
        {
            // Global variables
            var linkValid = 0;
            var linkInvalid = 0;
            var linkTargetInvalid = new List<string>();

            var errorMissingUri = new List<XmlNode>();
            var errorMissingTarget = new List<XmlNode>();
            var errorMissingTargetLinkText = new List<XmlNode>();
            var errorWrongLinkType = new List<XmlNode>();


            //
            // => Only check links when loading content for open projects
            //
            var nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']");
            if (nodeProject == null)
            {
                appLogger.LogError($"Could not locate project with ID {projectVars.projectId}. stack-trace: {GetStackTrace()}");
                return xmlDocument;
            }

            var projectStatus = nodeProject.SelectSingleNode("versions/version/status")?.InnerText ?? "unknown";
            if (projectStatus != "open")
            {
                appLogger.LogInformation($"Do not check links for a project of which the status not is 'open'. projectId: {projectVars.projectId}");
                return xmlDocument;
            }

            // Retrieve the hierarchy
            xmlHierarchy ??= RenderFilingHierarchy(reqVars, projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage, true);

            // Check if the current article is a note
            var sourceIsNote = false;
            var dataRefArticle = Path.GetFileName(xmlSectionPathOs);
            var nodeCurrentItem = xmlHierarchy.SelectSingleNode($"/items/structured//item[@data-ref='{dataRefArticle}']");
            if (nodeCurrentItem != null)
            {
                var currentItemitemTocDefinition = nodeCurrentItem.SelectSingleNode("ancestor::item[@data-tocstyle]") ?? nodeCurrentItem.SelectSingleNode("preceding-sibling::item[@data-tocstyle]") ?? (nodeCurrentItem.HasAttribute("data-tocstyle") ? nodeCurrentItem : null);
                if (currentItemitemTocDefinition != null) sourceIsNote = (currentItemitemTocDefinition.GetAttribute("data-tocstyle") ?? "").StartsWith("note");
                // Console.WriteLine($"!! Current article is a note: {sourceIsNote} !!");                
            }

            // TODO: Check if external links start with http and not with "#"

            // Find the internal links
            var nodeArticle = xmlDocument.SelectSingleNode($"/data/content[@lang='{projectVars.outputChannelVariantLanguage}']/article");
            var articleType = nodeArticle.GetAttribute("data-articletype") ?? "";

            var xPathForInternalLinks = $"/data/content[@lang='{projectVars.outputChannelVariantLanguage}']/*//{RenderInternalLinkXpathSelector()}";
            var nodeListLinks = xmlDocument.SelectNodes(xPathForInternalLinks);

            try
            {
                // - Strip any links from the text that have no link text
                var numberOfEmptyLinksStripped = 0;
                foreach (XmlNode nodeLink in nodeListLinks)
                {
                    if (nodeLink.InnerText.Trim() == "")
                    {
                        RemoveXmlNode(nodeLink);
                        numberOfEmptyLinksStripped++;
                    }
                }

                // - Check if the links can be resolved
                if (numberOfEmptyLinksStripped > 0) nodeListLinks = xmlDocument.SelectNodes(xPathForInternalLinks);
                foreach (XmlNode nodeLink in nodeListLinks)
                {
                    var linkErrorStatus = "";
                    var linkTarget = GetAttribute(nodeLink, "href");
                    var linkType = nodeLink.GetAttribute("data-link-type") ?? "unknown";
                    if (string.IsNullOrEmpty(linkTarget))
                    {
                        linkErrorStatus = "missing-uri";
                        linkInvalid++;
                        errorMissingUri.Add(nodeLink);
                    }
                    else
                    {
                        //
                        // => Check internal links
                        //
                        if (linkTarget.StartsWith('#') && projectVars.outputChannelType == "pdf")
                        {
                            // Get the site structure ID from the link
                            var targetArticleId = linkTarget.Replace("#", "");

                            // - Test if the link is broken
                            var xPathForHierarchy = $"/items/structured//item[@data-articleids='{targetArticleId}' or starts-with(@data-articleids, '{targetArticleId},') or contains(@data-articleids, ',{targetArticleId}')]";
                            var nodeItem = xmlHierarchy.SelectSingleNode(xPathForHierarchy);
                            if (nodeItem == null)
                            {
                                if (IsInternalLinkRelevant(nodeLink, projectVars.outputChannelVariantId))
                                {
                                    linkErrorStatus = "missing-link-target";
                                    linkInvalid++;
                                    errorMissingTarget.Add(nodeLink);
                                }
                            }
                            else
                            {
                                //
                                // => Link exists -> Force the internal links to use the text of the first header defined in the section
                                //
                                var customLinkText = nodeLink.GetAttribute("data-customlinktext") ?? "false";
                                var forceLinkText = (customLinkText == "false");
                                // Console.WriteLine($"- forceLinkText: {forceLinkText}");



                                // - Check if the target is a section or a note and retrieve the target toc numbering style
                                var targetIsNote = false;
                                var tocNumberingStyle = "";
                                var itemTocDefinition = nodeItem.SelectSingleNode("ancestor::item[@data-tocstyle]") ?? 
                                                        nodeItem.SelectSingleNode("preceding-sibling::item[@data-tocstyle]") ?? 
                                                        nodeItem.SelectSingleNode("../../preceding-sibling::item[@data-tocstyle]") ?? 
                                                        (nodeItem.HasAttribute("data-tocstyle") ? nodeItem : null);
                                if (itemTocDefinition != null)
                                {
                                    tocNumberingStyle = itemTocDefinition.GetAttribute("data-tocstyle");
                                    targetIsNote = tocNumberingStyle.StartsWith("note");
                                }

                                // - Set the notenumber if the target is a note
                                if (targetIsNote)
                                {
                                    nodeLink.SetAttribute("data-noteid", nodeItem.GetAttribute("data-tocnumber").SubstringAfter(" "));
                                }

                                // - Set the link type if for some reason it hasn't been defined yet
                                if (linkType == "unknown")
                                {
                                    nodeLink.SetAttribute("data-link-type", ((targetIsNote) ? "note" : "section"));
                                }

                                // - Force the text of the links to the article header title
                                if (forceLinkText)
                                {

                                    // - Links pointing to notes in the big financial tables need to inject the note number and not the article title
                                    if ((articleType == "megatable" || sourceIsNote) && targetIsNote && (nodeLink.SelectSingleNode("ancestor::table") != null))
                                    {
                                        nodeLink.InnerText = nodeLink.GetAttribute("data-noteid") ?? string.Empty;

                                        // Set special class on these note links so we can set a special styling on those
                                        nodeLink.SetAttribute("class", "nbl");
                                    }
                                    else
                                    {
                                        // If the link points to a note, but lives outside the large financial tables, then the special class should be removed
                                        if (targetIsNote) nodeLink.RemoveAttribute("class");

                                        var dataRef = nodeItem.GetAttribute("data-ref");
                                        var linkNameResolvedViaMetadata = false;
                                        if (!string.IsNullOrEmpty(dataRef))
                                        {
                                            // Try to retrieve the article title from the metadata document
                                            var sectionTitle = RetrieveSectionTitleFromMetadataCache(XmlCmsContentMetadata, dataRef, projectVars);

                                            if (sectionTitle != null)
                                            {
                                                linkNameResolvedViaMetadata = true;
                                                nodeLink.InnerXml = sectionTitle;
                                            }
                                            else
                                            {
                                                linkErrorStatus = "missing-target-sectionlinktext";
                                                linkInvalid++;
                                                errorMissingTargetLinkText.Add(nodeLink);
                                            }
                                        }

                                        if (!linkNameResolvedViaMetadata)
                                        {
                                            var nodeItemLinkText = nodeItem.SelectSingleNode("web_page/linkname");
                                            if (nodeItemLinkText == null)
                                            {
                                                linkErrorStatus = "missing-target-hierarchylinktext";
                                                linkInvalid++;
                                                errorMissingTargetLinkText.Add(nodeLink);
                                            }
                                            else
                                            {
                                                nodeLink.InnerXml = nodeItemLinkText.InnerXml;
                                            }
                                        }
                                    }


                                }
                                else
                                {
                                    appLogger.LogInformation($"Link text for with target {linkTarget} doesn't need to be updated.");
                                }

                                linkValid++;



                            }
                        }
                        else
                        {
                            if (linkType == "section")
                            {
                                linkErrorStatus = "wrong-link-type";
                                linkInvalid++;
                                errorWrongLinkType.Add(nodeLink);
                            }
                        }
                    }


                    if (linkErrorStatus != "")
                    {
                        linkTargetInvalid.Add(linkTarget);
                        SetAttribute(nodeLink, "data-link-error", linkErrorStatus);
                    }
                    else
                    {
                        RemoveAttribute(nodeLink, "data-link-error");
                    }

                }

                if (linkInvalid > 0)
                {
                    // Retrieve some section information that we can use to better understand where the link issues came from
                    var articleId = GetAttribute(xmlDocument.SelectSingleNode("/data/content/*"), "id") ?? "";

                    // Construct error document
                    var xmlLinkErrors = new XmlDocument();
                    xmlLinkErrors.LoadXml($"<linkerrors articleid=\"{articleId}\"><category type=\"missing-uri\"/><category type=\"missing-link-target\"/><category type=\"missing-target-linktext\"/><category type=\"wrong-link-type\"/></linkerrors>");

                    if (errorMissingUri.Count > 0)
                    {
                        var nodeMissingUri = xmlLinkErrors.SelectSingleNode("/linkerrors/category[@type='missing-uri']");
                        foreach (XmlNode nodeToImport in errorMissingUri)
                        {
                            nodeMissingUri.AppendChild(xmlLinkErrors.ImportNode(nodeToImport, true));
                        }
                    }
                    if (errorMissingTarget.Count > 0)
                    {
                        var nodeMissingTarget = xmlLinkErrors.SelectSingleNode("/linkerrors/category[@type='missing-link-target']");
                        foreach (XmlNode nodeToImport in errorMissingTarget)
                        {
                            nodeMissingTarget.AppendChild(xmlLinkErrors.ImportNode(nodeToImport, true));
                        }
                    }
                    if (errorMissingTargetLinkText.Count > 0)
                    {
                        var nodeMissingTargetLinkText = xmlLinkErrors.SelectSingleNode("/linkerrors/category[@type='missing-target-linktext']");
                        foreach (XmlNode nodeToImport in errorMissingTargetLinkText)
                        {
                            nodeMissingTargetLinkText.AppendChild(xmlLinkErrors.ImportNode(nodeToImport, true));
                        }
                    }
                    if (errorWrongLinkType.Count > 0)
                    {
                        var nodeWrongLinkType = xmlLinkErrors.SelectSingleNode("/linkerrors/category[@type='wrong-link-type']");
                        foreach (XmlNode nodeToImport in errorWrongLinkType)
                        {
                            nodeWrongLinkType.AppendChild(xmlLinkErrors.ImportNode(nodeToImport, true));
                        }
                    }


                    // Show a warning
                    appLogger.LogWarning($"Found {linkInvalid.ToString()} invalid links (and {linkValid.ToString()} valid links) with targets ({string.Join(",", linkTargetInvalid)}) in article-id: {articleId}, project-id: {projectVars.projectId}, lang: {projectVars.outputChannelVariantLanguage}");
                    //appLogger.LogWarning(PrettyPrintXml(xmlLinkErrors));

                    // Save the error report
                    try
                    {
                        if (!Directory.Exists($"{logRootPathOs}/links/{projectVars.projectId}"))
                        {
                            Directory.CreateDirectory($"{logRootPathOs}/links/{projectVars.projectId}");
                        }

                        xmlLinkErrors.Save($"{logRootPathOs}/links/{projectVars.projectId}/{articleId}.xml");
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, "Could not store link error report");
                    }
                }
            }
            catch (Exception ex)
            {
                // Make sure that we log the issue
                appLogger.LogError(ex, "There was a problem processing the links");

                // Rethrow the error, to make sure that the loading logic stops
                throw;
            }




            return xmlDocument;
        }



    }
}