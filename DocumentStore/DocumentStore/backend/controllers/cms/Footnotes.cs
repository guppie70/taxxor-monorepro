using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves footnote text
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveFootnote(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Determine if this is the master or slave language
            var masterLanguage = (projectVars.outputChannelDefaultLanguage == projectVars.outputChannelVariantLanguage);

            // Get posted data
            var footnoteId = context.Request.RetrievePostedValue("footnoteid", RegexEnum.None, true, ReturnTypeEnum.Json);

            try
            {
                var xmlFootnotes = _getFootnoteRepository(projectVars);

                // Test if this footnote already exists
                var nodeFootnote = xmlFootnotes.SelectSingleNode("/footnotes/footnote[@id=" + GenerateEscapedXPathString(footnoteId) + "]");
                if (nodeFootnote == null)
                {
                    HandleError(ReturnTypeEnum.Xml, "Footnote could not be found", $"footnoteId: {footnoteId}, stack-trace: {GetStackTrace()}");
                }

                // Potentially strip out the elements that do not conform to this language
                var nodeListFootnoteContentToRemove = nodeFootnote.SelectNodes($"span[@lang][not(@lang='{projectVars.outputChannelVariantLanguage}')]");
                foreach (XmlNode nodeFootnoteContentToRemove in nodeListFootnoteContentToRemove)
                {
                    RemoveXmlNode(nodeFootnoteContentToRemove);
                }

                // Return the footnote with the content in encoded format
                await context.Response.OK(nodeFootnote.OuterXml, ReturnTypeEnum.Xml, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Xml, "Something went wrong while retrieving footnotes list from the Taxxor Document Store", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Creates a new footnote
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task CreateFootnote(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var footnoteId = context.Request.RetrievePostedValue("footnoteid", RegexEnum.None, true, ReturnTypeEnum.Json);
            var footnoteType = context.Request.RetrievePostedValue("footnotetype", RegexEnum.None, true, reqVars.returnType);
            var footnoteContent = context.Request.RetrievePostedValue("footnotecontent", RegexEnum.None, true, ReturnTypeEnum.Json);

            // Make sure that entity references (like &nbsp;) are converted to their numerical equivalentents before storing it
            footnoteContent = footnoteContent.EntityToNumeric();

            var xmlFootnoteContent = new XmlDocument();
            try
            {
                xmlFootnoteContent.LoadXml(footnoteContent);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Xml, "Something went wrong while updating a footnote in the Taxxor Document Store", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }


            // Store the footnote content
            try
            {
                var xmlFootnotes = _getFootnoteRepository(projectVars);

                // Test if this footnote already exists
                var nodeFootnote = xmlFootnotes.SelectSingleNode("/footnotes/footnote[@id=" + GenerateEscapedXPathString(footnoteId) + "]");
                if (nodeFootnote != null)
                {
                    HandleError(ReturnTypeEnum.Xml, "Footnote already exists", $"footnoteId: {footnoteId}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // Add the footnote
                    var nodeNewFootnote = xmlFootnotes.CreateElement("footnote");
                    SetAttribute(nodeNewFootnote, "id", footnoteId);
                    SetAttribute(nodeNewFootnote, "type", footnoteType);

                    // Import the content
                    var newFootnoteImported = xmlFootnotes.ImportNode(xmlFootnoteContent.DocumentElement, true);
                    SetAttribute(newFootnoteImported, "lang", projectVars.outputChannelVariantLanguage);
                    nodeNewFootnote.AppendChild(newFootnoteImported);

                    // Assure that we have a translation for each of the languages defined in this project
                    var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
                    var projectLanguages = RetrieveProjectLanguages(editorId);
                    foreach (var lang in projectLanguages)
                    {
                        var nodeFootnoteContent = nodeNewFootnote.SelectSingleNode($"span[@lang='{lang}']");
                        if (nodeFootnoteContent == null)
                        {
                            newFootnoteImported = xmlFootnotes.ImportNode(xmlFootnoteContent.DocumentElement, true);
                            SetAttribute(newFootnoteImported, "lang", lang);
                            nodeNewFootnote.AppendChild(newFootnoteImported);
                        }
                    }

                    // Append the content
                    xmlFootnotes.DocumentElement.AppendChild(nodeNewFootnote);

                    // Save the repository
                    xmlFootnotes.Save(_retrieveFootnotesPathOs(projectVars));

                    // Rebuild the SDE cache file if needed
                    var sdeCacheUpdateResult = _updateFootnoteSdeCacheEntries(projectVars, (siteType == "local" || siteType == "dev"));
                    if (!sdeCacheUpdateResult.Success)
                    {
                        appLogger.LogWarning($"Unable to update the footnote SDE cache file. {sdeCacheUpdateResult.ToString()}");
                    }
                }

                // Rebuild the cache
                await UpdateCmsMetadataEntry(projectVars.projectId, Path.GetFileName(_retrieveFootnotesPathOs(projectVars)));

                // Return the result
                await context.Response.OK(GenerateSuccessXml("Successfully added footnote", $"footnoteId: {footnoteId}"), ReturnTypeEnum.Xml, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Xml, "Something went wrong while retrieving footnotes list from the Taxxor Document Store", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Removes a footnote
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RemoveFootnote(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var footnoteId = context.Request.RetrievePostedValue("footnoteid", RegexEnum.None, true, ReturnTypeEnum.Json);

            try
            {
                var xmlFootnotes = _getFootnoteRepository(projectVars);

                // Test if this footnote already exists
                var nodeFootnote = xmlFootnotes.SelectSingleNode("/footnotes/footnote[@id=" + GenerateEscapedXPathString(footnoteId) + "]");
                if (nodeFootnote == null)
                {
                    HandleError(ReturnTypeEnum.Xml, "Footnote does not exists in the repository", $"footnoteId: {footnoteId}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // Remove the footnote
                    RemoveXmlNode(nodeFootnote);

                    // Save the repository
                    xmlFootnotes.Save(_retrieveFootnotesPathOs(projectVars));

                    // TODO: Find all existing footnote references in the content and romove those as well
                }

                // Rebuild the cache
                await UpdateCmsMetadataEntry(projectVars.projectId, Path.GetFileName(_retrieveFootnotesPathOs(projectVars)));

                // Return the result
                await context.Response.OK(GenerateSuccessXml("Successfully removed footnote", $"footnoteId: {footnoteId}"), ReturnTypeEnum.Xml, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Xml, "Something went wrong while retrieving footnotes list from the Taxxor Document Store", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Updates an existing footnote
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task UpdateFootnote(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var footnoteId = context.Request.RetrievePostedValue("footnoteid", RegexEnum.None, true, ReturnTypeEnum.Json);
            var footnoteContent = context.Request.RetrievePostedValue("footnotecontent", RegexEnum.None, true, ReturnTypeEnum.Json);
            var footnoteLanguage = projectVars.outputChannelVariantLanguage;

            // Determine if this is the master or slave language
            var masterLanguage = (projectVars.outputChannelDefaultLanguage == projectVars.outputChannelVariantLanguage);

            // Make sure that entity references (like &nbsp;) are converted to their numerical equivalentents before storing it
            footnoteContent = footnoteContent.EntityToNumeric();

            var xmlFootnoteContent = new XmlDocument();
            try
            {
                xmlFootnoteContent.LoadXml(footnoteContent);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Xml, "Something went wrong while updating a footnote in the Taxxor Document Store", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }


            try
            {
                var xmlFootnotes = _getFootnoteRepository(projectVars);

                // Test if this footnote already exists
                var nodeFootnote = xmlFootnotes.SelectSingleNode("/footnotes/footnote[@id=" + GenerateEscapedXPathString(footnoteId) + "]");
                if (nodeFootnote == null)
                {
                    HandleError(ReturnTypeEnum.Xml, "Footnote could not be found", $"footnoteId: {footnoteId}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // Test if we need to split the content of the footnote for languages
                    var nodeListFootnoteContentFragments = nodeFootnote.SelectNodes("span");
                    var footnotesSplitted = nodeListFootnoteContentFragments.Count > 1;
                    if (!masterLanguage && !footnotesSplitted)
                    {
                        foreach (XmlNode nodeFootnoteFragment in nodeListFootnoteContentFragments)
                        {
                            // Mark the existing footnote for the master language
                            SetAttribute(nodeFootnoteFragment, "lang", projectVars.outputChannelDefaultLanguage);
                        }
                    }

                    // Mark the footnote that we want to store with the output channel language
                    SetAttribute(xmlFootnoteContent.DocumentElement, "lang", projectVars.outputChannelVariantLanguage);


                    var nodeFootnoteImported = xmlFootnotes.ImportNode(xmlFootnoteContent.DocumentElement, true);
                    var xPathFootnoteContent = (masterLanguage) ?
                        $"span[not(@lang) or @lang='{projectVars.outputChannelVariantLanguage}']" :
                        $"span[@lang='{projectVars.outputChannelVariantLanguage}']";

                    var nodeCurrentFootnoteContent = nodeFootnote.SelectSingleNode(xPathFootnoteContent);
                    if (nodeCurrentFootnoteContent == null)
                    {
                        // Append the footnote content
                        nodeFootnote.AppendChild(nodeFootnoteImported);
                    }
                    else
                    {
                        // Replace the current footnote
                        ReplaceXmlNode(nodeCurrentFootnoteContent, nodeFootnoteImported);
                    }

                    // Save the repository
                    xmlFootnotes.Save(_retrieveFootnotesPathOs(projectVars));

                    // Rebuild the SDE cache file if needed
                    var sdeCacheUpdateResult = _updateFootnoteSdeCacheEntries(projectVars, (siteType == "local" || siteType == "dev"));
                    if (!sdeCacheUpdateResult.Success)
                    {
                        appLogger.LogWarning($"Unable to update the footnote SDE cache file. {sdeCacheUpdateResult.ToString()}");
                    }
                }

                // Rebuild the cache
                await UpdateCmsMetadataEntry(projectVars.projectId, Path.GetFileName(_retrieveFootnotesPathOs(projectVars)));

                // Return the result
                await context.Response.OK(GenerateSuccessXml("Successfully changed the content of the footnote", $"footnoteId: {footnoteId}"), ReturnTypeEnum.Xml, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Xml, "Something went wrong while updating the footnote content in the Taxxor Document Store", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// List all the footnotes available in the system for this project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ListFootnotes(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            try
            {
                var xmlFootnotes = _getFootnoteRepository(projectVars);

                //var nodeListFootnoteContentFrag

                // Strip out the elements that do not conform to the current language
                var nodeListFootnoteContentToRemove = xmlFootnotes.SelectNodes($"/footnotes/footnote[count(span) > 1]/span[not(@lang='{projectVars.outputChannelVariantLanguage}')]");
                foreach (XmlNode nodeFootnoteContentToRemove in nodeListFootnoteContentToRemove)
                {
                    RemoveXmlNode(nodeFootnoteContentToRemove);
                }

                // appLogger.LogInformation(xmlFootnotes.OuterXml);
                await context.Response.OK(xmlFootnotes, ReturnTypeEnum.Xml, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Xml, "Something went wrong while retrieving footnotes list from the Taxxor Document Store", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Synchronizes the footnote text in the editor data with what was stored in the footnotes repository
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFolderPathOs"></param>
        /// <returns></returns>
        public static XmlDocument SyncFootnoteContent(XmlDocument xmlDocument, ProjectVariables projectVars, string? xmlSectionFolderPathOs = null)
        {
            // Global variables
            StringBuilder sbDebug = new StringBuilder();
            XmlDocument? xmlFootnotes = null;

            var articleId = "[unknown]";


            // TODO: Instead of only syncing the footnote content consider rebuilding the HTML structure completely as this works better with translations
            // After: <sup class="fn"><a href="#1575030289746" data-link-type="footnote">*</a></sup>
            // Render: <span class="footnote section" id="1575030289746"><sup class="fn">*</sup><span lang="zh">See also https://www.philips.com/a-w/about/company/our-management/executive-committee.html</span></span>
            // <sup class="fn" contenteditable="false"><a href="#1548437113445" data-link-type="footnote" data-mce-href="#1548437113445">*</a><span class="footnote section" id="1548437113445" contenteditable="false" data-syncstatus="200-ok"><sup class="fn" contenteditable="false">*</sup><span id="1548437113445"><span>Subject to SvB approval January 28, 2019</span></span></span></sup>

            // Sync the text from the footnotes
            var baseXpathToFootnotes = $"/data/content[@lang='{projectVars.outputChannelVariantLanguage}']//*[@class='footnote' or @class='footnote section' or @class='footnote table']";
            var nodeListFootnotes = xmlDocument.SelectNodes(baseXpathToFootnotes);
            // appLogger.LogCritical($"nodeListFootnotes.Count: {nodeListFootnotes.Count}");
            if (nodeListFootnotes.Count > 0)
            {
                xmlFootnotes = _getFootnoteRepository(projectVars, xmlSectionFolderPathOs);
                var nodeArticle = nodeListFootnotes.Item(0).SelectSingleNode("ancestor::article");
                if (nodeArticle != null) articleId = GetAttribute(nodeArticle, "id");
            }

            foreach (XmlNode nodeFootnote in nodeListFootnotes)
            {
                var syncStatus = "200-ok";

                var footnoteId = GetAttribute(nodeFootnote, "id");

                if (!string.IsNullOrEmpty(footnoteId))
                {
                    // Attempt to find the footnote text in the footnote repository
                    var nodeOriginalFootnoteFromFootnoteRepository = xmlFootnotes.SelectSingleNode($"/footnotes/footnote[@id='{footnoteId}']");
                    if (nodeOriginalFootnoteFromFootnoteRepository == null)
                    {
                        appLogger.LogWarning($"Footnote source node could not found in the footnote repository (footnoteId: {footnoteId}, articleId: {articleId}, language: {projectVars.outputChannelVariantLanguage}, projectId: {projectVars.projectId})");
                        syncStatus = "404-missing-footnote";
                    }
                    else
                    {
                        // Check if we have the translation of this footnote
                        XmlNode? nodeFootnoteContent = null;
                        var nodeListFootnoteContentFragments = nodeOriginalFootnoteFromFootnoteRepository.SelectNodes("span");
                        if (nodeListFootnoteContentFragments.Count == 1)
                        {
                            nodeFootnoteContent = nodeListFootnoteContentFragments.Item(0);
                        }
                        else
                        {
                            // Search to see if we can find a translation
                            foreach (XmlNode nodeFragment in nodeListFootnoteContentFragments)
                            {
                                var footnoteFragmentLanguage = GetAttribute(nodeFragment, "lang");
                                if (footnoteFragmentLanguage == projectVars.outputChannelVariantLanguage)
                                {
                                    nodeFootnoteContent = nodeFragment;
                                }
                            }
                        }

                        if (nodeFootnoteContent == null)
                        {
                            appLogger.LogWarning($"Footnote content could not be found (footnoteId: {footnoteId}, articleId: {articleId}, language: {projectVars.outputChannelVariantLanguage}, projectId: {projectVars.projectId})");
                            syncStatus = "404-missing-footnote-translation";
                        }
                        else
                        {
                            try
                            {
                                // Sync the footnote text
                                var nodeImportedFootnoteContent = xmlDocument.ImportNode(nodeFootnoteContent, true);
                                var nodeFootnoteTextWrapper = nodeFootnote.SelectSingleNode("span[not(@id)]");
                                if (nodeFootnoteTextWrapper != null)
                                {
                                    // Footnote in the text
                                    ReplaceXmlNode(nodeFootnoteTextWrapper, nodeImportedFootnoteContent);
                                }
                                else
                                {
                                    nodeFootnoteTextWrapper = nodeFootnote.SelectSingleNode("span");
                                    if (nodeFootnoteTextWrapper != null)
                                    {
                                        // Footnote in the table
                                        ReplaceXmlNode(nodeFootnoteTextWrapper.FirstChild, nodeImportedFootnoteContent);
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Could not sync the footnote content because the wrapper could not be found (footnoteId: {footnoteId}, articleId: {articleId}, language: {projectVars.outputChannelVariantLanguage}, projectId: {projectVars.projectId})");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError($"Error while syncing footnote content. error: {ex}, stack-trace: {GetStackTrace()}");
                            }
                        }
                    }
                }
                else
                {
                    appLogger.LogWarning($"Unable to sync footnote content without an ID (nodeFootnote: {nodeFootnote.OuterXml}, articleId: {articleId}, language: {projectVars.outputChannelVariantLanguage}, projectId: {projectVars.projectId})");
                }

                // Mark the footnote with an attribute so that we can give it a very ugly color if it's not "ok"
                SetAttribute(nodeFootnote, "data-syncstatus", syncStatus);

            }

            return xmlDocument;
        }

        /// <summary>
        /// Retrieves the path to the footnotes repository
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFolderPathOs"></param>
        /// <returns></returns>
        private static string? _retrieveFootnotesPathOs(ProjectVariables projectVars, string? xmlSectionFolderPathOs = null)
        {
            // appLogger.LogCritical("**************");
            // appLogger.LogDebug($"current: {projectVars.cmsDataRootBasePathOs}/version_1/textual/_footnotes.xml");
            // appLogger.LogDebug($"new: {CalculateFullPathOs("footnote_repository")}");
            // appLogger.LogCritical("**************");
            var footnoteLocationId = "footnote_repository";

            if (xmlSectionFolderPathOs == null)
            {
                return CalculateFullPathOs(footnoteLocationId);
            }
            else
            {
                var footnoteRepositoryFilename = Path.GetFileName(xmlApplicationConfiguration.SelectSingleNode($"/configuration/locations/location[@id='{footnoteLocationId}']")?.InnerText ?? "_footnotes.xml");
                return $"{xmlSectionFolderPathOs}/{footnoteRepositoryFilename}";
            }
        }

        /// <summary>
        /// Retrieves the footnote repository as an XmlDocument
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFolderPathOs"></param>
        /// <returns></returns>
        private static XmlDocument _getFootnoteRepository(ProjectVariables projectVars, string? xmlSectionFolderPathOs = null, bool syncSdeElements = true)
        {
            XmlDocument xmlFootnotes = new XmlDocument();
            var repositoryPathOs = _retrieveFootnotesPathOs(projectVars, xmlSectionFolderPathOs);
            if (!File.Exists(repositoryPathOs))
            {
                xmlFootnotes.LoadXml("<footnotes/>");
                try
                {
                    xmlFootnotes.Save(repositoryPathOs);
                }
                catch (Exception ex)
                {
                    appLogger.LogWarning(ex, $"Could not retrieve an initial version of the footnotes repository. path: {repositoryPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }
            else
            {
                xmlFootnotes.Load(repositoryPathOs);
            }

            // Sync the SDE elements
            if (syncSdeElements)
            {
                try
                {
                    xmlFootnotes.ReplaceContent(SyncStructuredDataElements(xmlFootnotes, repositoryPathOs, projectVars.projectId));
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Footnote repository: unable to sync structured data. repositoryPathOs: {repositoryPathOs}");
                }
            }

            return xmlFootnotes;
        }

        /// <summary>
        /// Updates the SDE cache file of the footnote reference repository
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        private static TaxxorReturnMessage _updateFootnoteSdeCacheEntries(ProjectVariables projectVars, bool debugRoutine = false)
        {

            // - Load the footnote references repository
            var xmlFilePathOs = _retrieveFootnotesPathOs(projectVars);
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(xmlFilePathOs);

            // - Update the cache file with whatever changed in the footnote reference file
            var rebuildStructuredDataCacheOnSave = false;
            var removeUnusedCacheEntries = true;

            return UpdateStructuredDataElementsCacheFile(xmlDocument, xmlFilePathOs, projectVars.projectId, projectVars.outputChannelVariantLanguage, rebuildStructuredDataCacheOnSave, removeUnusedCacheEntries, debugRoutine);
        }

    }
}