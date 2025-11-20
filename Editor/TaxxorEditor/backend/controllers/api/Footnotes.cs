using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves details of a footnote from the Taxxor Document Store
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveFootnote(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var footnoteId = context.Request.RetrievePostedValue("footnoteid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);

            /*
            Call the Taxxor Document Store to retrieve the data
            */
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pid", projectVars.projectId);
            dataToPost.Add("footnoteid", footnoteId);
            dataToPost.Add("oclang", projectVars.outputChannelVariantLanguage);

            // Call the service and retrieve the footnotes as XML
            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "footnote", dataToPost, debugRoutine);

            if (XmlContainsError(xmlResponse))
            {
                await context.Response.Error(xmlResponse, reqVars.returnType, true);
            }
            else
            {
                // Construct a response message for the client
                dynamic jsonData = new ExpandoObject();
                jsonData.footnote = new ExpandoObject();
                jsonData.footnote.id = GetAttribute(xmlResponse.DocumentElement, "id");
                jsonData.footnote.content = HttpUtility.HtmlDecode(xmlResponse.DocumentElement.InnerText);

                // Convert to JSON and return it to the client
                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
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
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var footnoteId = context.Request.RetrievePostedValue("footnoteid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var footnoteType = context.Request.RetrievePostedValue("footnotetype", @"^(normal|section)$", true, reqVars.returnType);
            var footnoteContent = context.Request.RetrievePostedValue("footnotecontent", RegexEnum.None, true, reqVars.returnType);

            /*
            Call the Taxxor Document Store to retrieve the data
            */
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pid", projectVars.projectId);
            dataToPost.Add("oclang", projectVars.outputChannelVariantLanguage);
            dataToPost.Add("footnoteid", footnoteId);
            dataToPost.Add("footnotetype", footnoteType);
            dataToPost.Add("footnotecontent", footnoteContent);

            // Call the service and retrieve the footnotes as XML
            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "footnote", dataToPost, debugRoutine);

            if (XmlContainsError(xmlResponse))
            {
                await context.Response.Error(xmlResponse, reqVars.returnType, true);
            }
            else
            {
                // Update the in-memory CMS metadata content
                await UpdateCmsMetadata(projectVars.projectId);

                // Clear the complete content cache for paged media content
                ContentCache.Clear(projectVars.projectId, projectVars.outputChannelVariantId);

                await context.Response.OK(xmlResponse, reqVars.returnType, true);
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
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var footnoteId = context.Request.RetrievePostedValue("footnoteid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);

            /*
            Call the Taxxor Document Store to retrieve the data
            */
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pid", projectVars.projectId);
            dataToPost.Add("oclang", projectVars.outputChannelVariantLanguage);
            dataToPost.Add("footnoteid", footnoteId);

            // Call the service and retrieve the footnotes as XML
            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Delete, "footnote", dataToPost, debugRoutine);

            if (XmlContainsError(xmlResponse))
            {
                await context.Response.Error(xmlResponse, reqVars.returnType, true);
            }
            else
            {
                // Update the in-memory CMS metadata content
                await UpdateCmsMetadata(projectVars.projectId);

                // Clear the complete content cache for paged media content
                ContentCache.Clear(projectVars.projectId, projectVars.outputChannelVariantId);

                await context.Response.OK(xmlResponse, reqVars.returnType, true);
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
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var footnoteId = context.Request.RetrievePostedValue("footnoteid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var footnoteContent = context.Request.RetrievePostedValue("footnotecontent", RegexEnum.None, true, reqVars.returnType);

            /*
            Call the Taxxor Document Store to retrieve the data
            */
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pid", projectVars.projectId);
            dataToPost.Add("oclang", projectVars.outputChannelVariantLanguage);
            dataToPost.Add("footnoteid", footnoteId);
            dataToPost.Add("footnotecontent", footnoteContent);

            // Call the service and retrieve the footnotes as XML
            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "footnote", dataToPost, debugRoutine);

            if (XmlContainsError(xmlResponse))
            {
                await context.Response.Error(xmlResponse, reqVars.returnType, true);
            }
            else
            {
                // Update the in-memory CMS metadata content
                await UpdateCmsMetadata(projectVars.projectId);

                // Clear the complete content cache for paged media content
                ContentCache.Clear(projectVars.projectId, projectVars.outputChannelVariantId);

                await context.Response.OK(xmlResponse, reqVars.returnType, true);
            }
        }

        /// <summary>
        /// List all the footnotes
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

            /*
            Call the Taxxor Document Store to retrieve the data
            */
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pid", projectVars.projectId);
            dataToPost.Add("oclang", projectVars.outputChannelVariantLanguage);

            // Call the service and retrieve the footnotes as XML
            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "listfootnotes", dataToPost, debugRoutine);

            if (!XmlContainsError(xmlResponse))
            {
                List<Footnote> footnotes = new List<Footnote>();

                foreach (XmlNode nodeFootnote in xmlResponse.SelectNodes("/footnotes/footnote"))
                {
                    var footnoteType = GetAttribute(nodeFootnote, "type");
                    if (string.IsNullOrEmpty(footnoteType)) footnoteType = "normal";

                    footnotes.Add(new Footnote(GetAttribute(nodeFootnote, "id"), footnoteType, HttpUtility.HtmlDecode(nodeFootnote.InnerXml)));
                }

                dynamic jsonData = new ExpandoObject();
                jsonData.footnotes = footnotes;

                // Convert to JSON and return it to the client
                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
            else
            {
                HandleError(ReturnTypeEnum.Json, xmlResponse);
            }
        }

        /// <summary>
        /// Removes the in-text footnotes from the DOM and injects a wrapper DIV containing the footnotes
        /// </summary>
        /// <param name="xmlData"></param>
        public static void CreateInTextFootnoteSummary(ref XmlDocument xmlData, ref List<string> warnings, ref List<string> errors)
        {
            var nodeListArticles = xmlData.SelectNodes("//article");
            for (int i = 0; i < nodeListArticles.Count; i++)
            {
                var nodeArticle = nodeListArticles.Item(i);
                CreateInTextFootnoteSummary(ref nodeArticle, ref warnings, ref errors);
            }
        }

        /// <summary>
        /// Removes the in-text footnotes from the DOM and injects a wrapper DIV containing the footnotes
        /// Also assures that footnote references used have a unique ID
        /// </summary>
        /// <param name="nodeArticle"></param>
        public static void CreateInTextFootnoteSummary(ref XmlNode nodeArticle, ref List<string> warnings, ref List<string> errors)
        {
            var xmlDoc = nodeArticle.OwnerDocument;
            var articleId = GetAttribute(nodeArticle, "id");
            Dictionary<string, string> footnoteIdsDict = new Dictionary<string, string>();
            var footnoteIds = new List<string>();
            var fragFootnotes = xmlDoc.CreateDocumentFragment();
            var message = "";
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Test if the footnote reference check needs to be enabled or not
            if (Extensions.EnableFootnoteReferenceCheck(nodeArticle))
            {
                try
                {
                    var dictFootnoteIdReplacement = new Dictionary<string, string>();
                    var nodeListTextFootnotes = nodeArticle.SelectNodes(Extensions.RenderIntextFootnoteSelector());
                    var footnoteCounter = 0;
                    foreach (XmlNode nodeTextFootnote in nodeListTextFootnotes)
                    {
                        var originalFootnoteIdFound = false;
                        var footnoteId = GetAttribute(nodeTextFootnote, "id");
                        if (string.IsNullOrEmpty(footnoteId))
                        {
                            footnoteId = GetAttribute(nodeTextFootnote, "data-footnoteid");
                            if (!string.IsNullOrEmpty(footnoteId))
                            {
                                // Restore the original footnote ID
                                SetAttribute(nodeTextFootnote, "id", footnoteId);
                            }
                        }
                        else
                        {
                            originalFootnoteIdFound = true;
                        }

                        // Report on a footnote ID not found in the original position
                        if (!originalFootnoteIdFound && debugRoutine) appLogger.LogWarning($"Footnote ID was not found in it's original position, but we were able to restore it (article ID: {articleId}, data-footnoteid: {footnoteId})");

                        if (!string.IsNullOrEmpty(footnoteId))
                        {
                            if (!footnoteIds.Contains(footnoteId))
                            {
                                // Generate a new footnote ID
                                var footnoteIdUnique = string.Format("{0:X}", $"fnartid{articleId}itext{footnoteCounter}id{footnoteId}".GetHashCode());

                                // Add an entry to the replacement dictionary
                                dictFootnoteIdReplacement.Add(footnoteId, footnoteIdUnique);

                                // Map the current footnote ID to a new one so that we can change the links pointing to it afterwards
                                footnoteIdsDict.Add(footnoteId, footnoteIdUnique);

                                // Correct the footnote ID to use the new unique ID
                                SetAttribute(nodeTextFootnote, "id", footnoteIdUnique);
                                SetAttribute(nodeTextFootnote, "class", "footnote section");
                                SetAttribute(nodeTextFootnote, "data-footnoteid", footnoteId);

                                // Find out if we need to add block tag ID's to the footnote
                                var nodeElementWithBlockTag = nodeTextFootnote.SelectSingleNode("ancestor-or-self::*[@data-block-id]");
                                if (nodeElementWithBlockTag != null)
                                {
                                    nodeTextFootnote.SetAttribute("data-block-id", nodeElementWithBlockTag.GetAttribute("data-block-id"));
                                }

                                // Add the footnote to the fragment
                                fragFootnotes.AppendChild(nodeTextFootnote.CloneNode(true));

                                // Add this footnote ID to the list so we only add it once
                                footnoteIds.Add(footnoteId);
                            }

                            // var nodeListFootnoteReference = nodeTextFootnote.ParentNode.SelectNodes($"sup[@class='fn' and a/@href='#{footnoteId}']");
                            // foreach (XmlNode nodeFootnoteReference in nodeListFootnoteReference)
                            // {
                            //     Console.WriteLine($"++++++++ COUNT {nodeListFootnoteReference.Count} +++++++");
                            //     Console.WriteLine(nodeFootnoteReference.OuterXml);
                            //     Console.WriteLine("+++++++++++++++");
                            // }

                            // Use the new id to the footnote reference in the link pointing to the footnote
                            var nodeFootnoteWrapper = nodeTextFootnote.ParentNode;
                            if (nodeFootnoteWrapper != null)
                            {
                                if (footnoteIdsDict.ContainsKey(footnoteId))
                                {
                                    string uniqueFootnoteId = footnoteIdsDict[footnoteId];
                                    var nodeFootnoteReferenceLink = nodeFootnoteWrapper.SelectSingleNode("a");
                                    if (nodeFootnoteReferenceLink != null)
                                    {
                                        // Use the new unique ID for the link
                                        SetAttribute(nodeFootnoteReferenceLink, "href", $"#{uniqueFootnoteId}");
                                    }
                                    else
                                    {
                                        message = $"Could not locate the footnote reference link for footnote reference with ID '{footnoteId}'. {RenderDebugInfo(nodeFootnoteWrapper)}";
                                        warnings.Add(message);
                                        appLogger.LogWarning(message);
                                    }
                                }
                                else
                                {
                                    message = $"Could not retrieve a new unique footnote ID for '{footnoteId}'. As a result this footnote reference will not link properly. {RenderDebugInfo(nodeFootnoteWrapper)}";
                                    errors.Add(message);
                                    appLogger.LogError(message);
                                }
                            }
                            else
                            {
                                message = $"Expected a footnote reference for footnote with ID {footnoteId}, but could not find it. {RenderDebugInfo(nodeFootnoteWrapper)}";
                                warnings.Add(message);
                                appLogger.LogWarning(message);
                            }
                        }
                        else
                        {
                            message = $"Could not process this footnote: {nodeTextFootnote.OuterXml} because it does not contain an ID. articleId: {articleId}";
                            errors.Add(message);
                            appLogger.LogError(message);
                        }


                        // Remove the footnote from the original position
                        RemoveXmlNode(nodeTextFootnote);
                        footnoteCounter++;
                    }

                    // Replace potential other footnote references in the article with a link to the new unique footnote ID
                    foreach (var pair in dictFootnoteIdReplacement)
                    {
                        var footnoteIdToSearch = pair.Key;
                        var footnoteIdToReplace = pair.Value;
                        var nodeListFootnoteReferences = nodeArticle.SelectNodes($".//a[@href='#{footnoteIdToSearch}']");
                        foreach (XmlNode nodeFootnoteReference in nodeListFootnoteReferences)
                        {
                            nodeFootnoteReference.SetAttribute("href", $"#{footnoteIdToReplace}");
                        }
                    }


                    // Inject the wrapper div
                    if (footnoteIds.Count > 0)
                    {
                        var footnoteWrapper = xmlDoc.CreateElement("div");
                        SetAttribute(footnoteWrapper, "class", "intext-footnote-wrapper");
                        footnoteWrapper.AppendChild(fragFootnotes);
                        nodeArticle.AppendChild(footnoteWrapper);
                    }
                }
                catch (Exception ex)
                {
                    message = $"There was an error creating the footnote summary in article with ID: {articleId}";
                    errors.Add(message);
                    appLogger.LogError(ex, message);
                }

                string RenderDebugInfo(XmlNode nodeSectionContent)
                {
                    ProjectVariables projectVars = RetrieveProjectVariables(System.Web.Context.Current);
                    var debugInfo = "";

                    if (nodeSectionContent != null)
                    {
                        string? tableIdInternal = nodeSectionContent.SelectSingleNode("ancestor::table")?.GetAttribute("id");
                        string? articleIdInternal = nodeSectionContent.SelectSingleNode("ancestor::article")?.GetAttribute("id");
                        return $"projectId: {projectVars.projectId}{((tableIdInternal != null) ? ", tableId: " + tableIdInternal + "," : "")}{((articleIdInternal != null) ? ", articleId: " + articleIdInternal : "")}";
                    }

                    return debugInfo;
                }

            }
        }

        /// <summary>
        /// Utility that loops through all the table footnote references and converts them so that they have a unique ID
        /// </summary>
        /// <param name="xmlData"></param>
        public static void CreateUniqueFootnoteReferencesInTables(ref XmlDocument xmlData)
        {

            var nodeListArticles = xmlData.SelectNodes("//article");
            var articleCounter = 0;
            foreach (XmlNode nodeArticle in nodeListArticles)
            {
                var articleId = GetAttribute(nodeArticle, "id");
                if (!string.IsNullOrEmpty(articleId))
                {
                    // Find the table footnote wrapper elements
                    var footnoteWrapperCounter = 0;
                    var nodeListTableFootnoteWrappers = xmlData.SelectNodes("*//div[@class='tablegraph-footer-wrapper' and */@data-footnoteid]");
                    foreach (XmlNode nodeTableFootnoteWrapper in nodeListTableFootnoteWrappers)
                    {

                        // Maps existing to new ID's
                        Dictionary<string, string> footnoteIdsDict = new Dictionary<string, string>();

                        var footnoteCounter = 0;
                        var nodeListTableFootnotes = nodeTableFootnoteWrapper.SelectNodes("*[@data-footnoteid]");
                        foreach (XmlNode nodeTableFootnote in nodeListTableFootnotes)
                        {
                            var footnoteId = GetAttribute(nodeTableFootnote, "id");

                            // Generate a new footnote ID
                            var footnoteIdUnique = $"fnt{string.Format("{0:X}", $"art{articleCounter}fnw{footnoteWrapperCounter}fn{footnoteCounter}".GetHashCode())}";

                            // Map the current footnote ID to a new one so that we can change the links pointing to it afterwards
                            footnoteIdsDict.Add(footnoteId, footnoteIdUnique);

                            // Correct the footnote ID to use the new unique ID
                            SetAttribute(nodeTableFootnote, "id", footnoteIdUnique);

                            // Remove the ID that is present on the footnote text
                            var nodeFootnoteText = nodeTableFootnote.SelectSingleNode("span[@id]");
                            if (nodeFootnoteText != null)
                            {
                                RemoveAttribute(nodeFootnoteText, "id");
                            }
                            // else
                            // {
                            //     appLogger.LogWarning($"Could not find the footnote text with an ID. articleId: {articleId}");
                            // }
                            footnoteCounter++;
                        }

                        // Use the new id to the footnote reference in the link pointing to the footnote
                        var nodeTableWrapper = nodeTableFootnoteWrapper.ParentNode;
                        if (nodeTableWrapper != null)
                        {
                            // Loop through the dictornary and correct the links pointing to the footnotes with the new ID
                            foreach (var pair in footnoteIdsDict)
                            {
                                var footnoteIdOriginal = pair.Key;
                                var footnoteIdUnique = pair.Value;
                                var nodeListFootnoteReference = nodeTableWrapper.SelectNodes($"*//sup[@class='fn' and a/@href='#{footnoteIdOriginal}']/a");
                                if (nodeListFootnoteReference.Count == 0)
                                {
                                    // appLogger.LogDebug($"Could not find any footnote references for table footnote with original ID {footnoteIdOriginal}, articleId: {articleId}");
                                }
                                else
                                {
                                    foreach (XmlNode nodeFootnoteReference in nodeListFootnoteReference)
                                    {
                                        // Use the new unique ID for the link
                                        SetAttribute(nodeFootnoteReference, "href", $"#{footnoteIdUnique}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Could not find the table wrapper where the footnote references should be in,  articleId: {articleId}");
                        }

                        footnoteWrapperCounter++;
                    }
                }
                else
                {
                    appLogger.LogWarning($"Missing article ID. stack-trace: {GetStackTrace()}");
                }
                articleCounter++;
            }

        }

        /// <summary>
        /// Represents a footnote
        /// </summary>
        public class Footnote
        {
            // Generic properties for the user class
            public string id;
            public string type;
            public string content;

            public Footnote(string footnoteId, string footnoteType, string footnoteContent)
            {
                this.id = footnoteId;
                this.type = footnoteType;
                this.content = footnoteContent;
            }
        }

    }

}