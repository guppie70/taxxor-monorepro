using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// Auditor View Page Logic
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders the content of the auditor view when the page loads
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RenderAuditorViewStartContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            var debugRoutine = siteType == "local";
            debugRoutine = false;

            try
            {
                var filterLatestString = request.RetrievePostedValue("filterlatest", "(true|false)", false, reqVars.returnType, "false");
                var filterLatest = filterLatestString == "true";

                var populateCacheString = request.RetrievePostedValue("populatecache", "(true|false)", false, reqVars.returnType, "false");
                var populateCache = populateCacheString == "true";

                var clientEpochString = request.RetrievePostedValue("epoch", @"\d{10}", true, reqVars.returnType);
                var clientEpoch = Convert.ToInt64(clientEpochString);

                // if (populateCache)
                // {
                //     var auditorViewContent = await _retrieveAuditorViewContentAndCache(false, true);
                //     await response.OK(auditorViewContent, ReturnTypeEnum.Html, true);
                // }
                // else
                // {

                // }

                var auditorViewContent = await RenderAuditorViewContent(null, null, null, null, filterLatest, debugRoutine);
                await response.OK(auditorViewContent, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Html, "There was an error rendering the auditor view", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Filters the Auditor View content using the filters posted from the client (attempts to use the cached version of the GIT repro content to boost performance)
        /// </summary>
        public static async Task FilterAuditorViewContent()
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = siteType == "local";
            debugRoutine = false;

            var datePattern = @"^(\d{4}\-\d{2}\-\d{2})\s\-\s(\d{4}\-\d{2}\-\d{2})$";

            var filterDatePosted = context.Request.RetrievePostedValue("filterdate");
            var filterUserPosted = context.Request.RetrievePostedValue("filteruser");
            var filterMessagePosted = context.Request.RetrievePostedValue("filtermessage");

            var filterLatestString = context.Request.RetrievePostedValue("filterlatest", "(true|false)", false, reqVars.returnType, "false");
            var filterLatest = filterLatestString == "true";

            // Grab the start and end date from the posted date ramge
            string? filterStartDate = null;
            string? filterEndDate = null;
            if (!string.IsNullOrEmpty(filterDatePosted))
            {
                Match match = Regex.Match(filterDatePosted, datePattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    filterStartDate = match.Groups[1].Value;
                    filterEndDate = match.Groups[2].Value;
                }
            }

            string? filterDate = string.IsNullOrEmpty(filterDatePosted) ? null : filterDatePosted;
            string? filterUser = string.IsNullOrEmpty(filterUserPosted) ? null : filterUserPosted.ToLower();
            string? filterMessage = string.IsNullOrEmpty(filterMessagePosted) ? null : filterMessagePosted.ToLower();

            // Retrieve the filtered auditor view content and send that back to the client
            string html = await RenderAuditorViewContent(filterUser, filterStartDate, filterEndDate, filterMessage, filterLatest, debugRoutine);
            await context.Response.WriteAsync(html);
        }

        /// <summary>
        /// Retrieves the auditor view content from the Taxxor Document Store
        /// </summary>
        /// <param name="filterLatest"></param>
        /// <param name="cacheResult"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        private static async Task<XmlDocument> _retrieveAuditorViewContentAndCache(bool filterLatest, bool cacheResult, bool debugRoutine = false)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            if (siteType == "local") debugRoutine = true;


            // Call the Taxxor Document Store to retrieve the auditor view XML data 
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pid", projectVars.projectId);
            dataToPost.Add("filterlatest", filterLatest.ToString().ToLower());

            var watch = System.Diagnostics.Stopwatch.StartNew();

            XmlDocument auditorOverviewDataEnvelope = await CallTaxxorDataService<XmlDocument>(RequestMethodEnum.Get, "taxxoreditorauditordataoverview", dataToPost, true);


            if (siteType == "local")
            {
                watch.Stop();
                appLogger.LogInformation($"Base auditor information retreival took: {watch.ElapsedMilliseconds.ToString()} ms");
            }

            if (XmlContainsError(auditorOverviewDataEnvelope)) HandleError(auditorOverviewDataEnvelope);

            // Extract the data from the envelope and inject it into our XmlDocument
            if (siteType == "local") watch = System.Diagnostics.Stopwatch.StartNew();
            var xmlCommits = new XmlDocument();
            try
            {
                xmlCommits.LoadXml(HttpUtility.HtmlDecode(auditorOverviewDataEnvelope.SelectSingleNode("/result/message").InnerText));
            }
            catch (Exception ex)
            {
                HandleError(reqVars, "Could not load information received from the Taxxor Document Store", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
            if (siteType == "local")
            {
                watch.Stop();
                appLogger.LogInformation($"Create local XmlDocument took: {watch.ElapsedMilliseconds.ToString()} ms");
            }

            // Mark the elements that are not the latest version of an section
            if (siteType == "local") watch = System.Diagnostics.Stopwatch.StartNew();
            var processedSectionIds = new List<string>();
            var nodeListProjectDataCommits = xmlCommits.SelectNodes("/commits/commit-meta[@repro='project-data']");
            foreach (XmlNode nodeProjectDataCommit in nodeListProjectDataCommits)
            {
                var sectionIds = nodeProjectDataCommit.SelectSingleNode("commit/message/id")?.InnerText ?? "";
                if (!string.IsNullOrEmpty(sectionIds))
                {
                    if (!sectionIds.Contains(","))
                    {
                        if (processedSectionIds.Contains(sectionIds))
                        {
                            SetAttribute(nodeProjectDataCommit, "latest", "false");
                        }

                    }
                    else
                    {
                        // Deal with multiple ID's
                    }

                    // Add item to the list
                    if (!sectionIds.Contains(","))
                    {
                        if (!processedSectionIds.Contains(sectionIds)) processedSectionIds.Add(sectionIds);
                    }

                }
            }
            if (siteType == "local")
            {
                watch.Stop();
                appLogger.LogInformation($"Mark older versions took: {watch.ElapsedMilliseconds.ToString()} ms");
            }

            var saved = false;
            if (debugRoutine) saved = await SaveXmlDocument(xmlCommits, logRootPathOs + "/_auditorview.xml");

            // 4) Sort the data found
            if (siteType == "local") watch = System.Diagnostics.Stopwatch.StartNew();
            xmlCommits = TransformXmlToDocument(xmlCommits, CalculateFullPathOs("cms_xsl_auditor-view-sort"));
            if (debugRoutine) saved = await SaveXmlDocument(xmlCommits, logRootPathOs + "/_auditorview-sorted.xml");
            if (siteType == "local")
            {
                watch.Stop();
                appLogger.LogInformation($"Sorting auditor view content took: {watch.ElapsedMilliseconds.ToString()} ms");
            }

            // Dump into memcache for two minutes if we have just retrieved the full list of commits
            if (cacheResult)
            {
                SetMemoryCacheItem(_renderAuditorViewCacheKey(reqVars, projectVars), xmlCommits.OuterXml, TimeSpan.FromMinutes(2));
            }

            return xmlCommits;
        }

        /// <summary>
        /// Renders the memory cache key for retrieving and storing the auditor view content retrieved from the Taxxor Document Store
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        private static string _renderAuditorViewCacheKey(RequestVariables reqVars, ProjectVariables projectVars)
        {
            return _renderAuditorViewCacheKey(projectVars.projectId, reqVars.siteLanguage, projectVars.currentUser.Id);
        }

        /// <summary>
        /// Renders the memory cache key for retrieving and storing the auditor view content retrieved from the Taxxor Document Store
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="siteLanguage"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        private static string _renderAuditorViewCacheKey(string projectId, string siteLanguage, string userId)
        {
            string keyContent = $"auditorview_{projectId}_{siteLanguage}";
            if (!string.IsNullOrEmpty(userId)) keyContent += $"_{userId}";
            return keyContent;
        }


        /// <summary>
        /// Renders the Auditor View page by reading out all the commits from the repository as XML and then translate that with XSLT to HTML
        /// </summary>
        /// <param name="forceReload"></param>
        /// <param name="filterUser"></param>
        /// <param name="filterDateStart"></param>
        /// <param name="filterDateEnd"></param>
        /// <param name="filterMessage"></param>
        /// <param name="filterLatest"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<string> RenderAuditorViewContent(string? filterUser = null, string? filterDateStart = null, string? filterDateEnd = null, string? filterMessage = null, bool filterLatest = false, bool debugRoutine = false)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);


            // Force debug mode on local development machine
            // if (siteType == "local") debugRoutine = true;
            var enableCache = false;
            var saved = false;
            var html = new StringBuilder();

            var xmlCommits = new XmlDocument();

            string? xmlCommitContent = null;
            string memCacheKey = _renderAuditorViewCacheKey(reqVars, projectVars);

            // If possible and forcereload==false then try to grab the content from the cache
            if (enableCache && MemoryCacheItemExists(memCacheKey) && (filterLatest == false))
            {
                // Grab the value from the cache
                if (debugRoutine) appLogger.LogInformation($"Use cache with key: {memCacheKey}");
                xmlCommitContent = RetrieveCacheContent<String>(memCacheKey);

                // Load it into the XmlDocument
                try
                {
                    xmlCommits.LoadXml(xmlCommitContent);
                }
                catch (Exception ex)
                {
                    HandleError(ReturnTypeEnum.Html, "Could not retrieve auditor view information", $"Unable to load the xml content retrieved from the git repository (CACHED) in a XmlDocument object. error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }

            // Load the data from the GIT repositories from the Taxxor Document Store
            if (string.IsNullOrEmpty(xmlCommitContent))
            {
                // It looks like we somehow failed to use the cache
                if (siteType == "local") appLogger.LogInformation($"Do not use cache with key: {memCacheKey}");

                var cacheResult = filterLatest ? false : enableCache ? true : false;
                xmlCommits = await _retrieveAuditorViewContentAndCache(filterLatest, cacheResult, debugRoutine);
            }

            if (debugRoutine) await xmlCommits.SaveAsync($"{logRootPathOs}/_auditorview--filter.xml", false, true);


            // 5) Parse the data so that we can display the commits per day and we can filter
            XmlNode? nodeDay = null;
            var now = DateTime.Now;
            int daysPassed = -1;

            // Calculate days passed since this year
            var dateFirstJanuary = new DateTime(now.Year - 1, 12, 31);
            TimeSpan elapsedSinceBeginningThisYear = now.Subtract(dateFirstJanuary);
            int daysAgoSinceNewYear = Convert.ToInt32(elapsedSinceBeginningThisYear.TotalDays);

            // Calculate the days we need to use for the date filter
            var inFilterDateRange = true;
            DateTime dateFilterStart = DateTime.Parse("1970-01-02");
            DateTime dateFilterEnd = DateTime.Parse("2080-01-01");

            if (filterDateStart != null) dateFilterStart = DateTime.Parse(filterDateStart);
            if (filterDateEnd != null) dateFilterEnd = DateTime.Parse(filterDateEnd);

            // We need to add 24 hours to the end date
            //dateFilterEnd.AddHours(23).AddMinutes(59).AddSeconds(59);
            dateFilterEnd = dateFilterEnd.AddDays(1);

            long epochDateFilterStart = ToUnixTime(dateFilterStart);
            long epochDateFilterEnd = ToUnixTime(dateFilterEnd);

            var inFilterMessage = true;
            var inFilterUser = true;

            var nodeListCommits = xmlCommits.SelectNodes("/commits/commit");
            foreach (XmlNode nodeCommit in nodeListCommits)
            {
                var nodeDate = nodeCommit.SelectSingleNode("date");

                if (nodeDate != null)
                {
                    var dateCommit = new DateTime();

                    // Attempt to parse the string into a date object
                    try
                    {
                        var epochTimeStamp = GetAttribute(nodeDate, "epoch");
                        if (!string.IsNullOrEmpty(epochTimeStamp))
                        {
                            dateCommit = FromUnixTime(epochTimeStamp);
                        }
                        else
                        {
                            // Attempt to parse the date using the string of the date that we have stored
                            // 06/14/2018 format mm/dd/yyyy (.InvariantCulture)
                            // 14/06/2018 format dd/mm/yyyy (.CurrentCulture)

                            dateCommit = DateTime.Parse(nodeDate.InnerText, CultureInfo.CurrentCulture);
                        }

                    }
                    catch (Exception ex)
                    {
                        HandleError(ReturnTypeEnum.Html, $"Could not parse dates", $"nodeDate.InnerText: '{nodeDate.InnerText}', error: {ex}, stack-trace: {GetStackTrace()}");
                    }

                    // Add the current time as an attribute to the date node
                    SetAttribute(nodeDate, "time", dateCommit.ToString("HH:mm:ss"));

                    // Check if this commit falls in the date range
                    try
                    {
                        long epochDateCommit = Convert.ToInt64(GetAttribute(nodeDate, "epoch"));
                        if (debugRoutine) html.AppendLine("- epochDateCommit: " + epochDateCommit.ToString() + ", epochDateFilterStart: " + epochDateFilterStart.ToString() + ", epochDateFilterEnd: " + epochDateFilterEnd.ToString() + "<br/>" + Environment.NewLine);
                        if (epochDateCommit >= epochDateFilterStart && epochDateCommit <= epochDateFilterEnd)
                        {
                            inFilterDateRange = true;
                        }
                        else
                        {
                            inFilterDateRange = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogDebug(ex, "Could not retrieve epoch time stamp for the current commit");
                    }

                    if (debugRoutine) DebugVariable(() => inFilterDateRange);

                    // Check if this commit falls in the user range
                    if (!string.IsNullOrEmpty(filterUser))
                    {
                        var commitUser = RetrieveNodeValueIfExists("author/name", nodeCommit);
                        if (!string.IsNullOrEmpty(commitUser))
                        {
                            if (commitUser.ToLower().Contains(filterUser))
                            {
                                inFilterUser = true;
                            }
                            else
                            {
                                inFilterUser = false;
                            }
                        }
                    }

                    // Check if this commit falls in the message range
                    if (!string.IsNullOrEmpty(filterMessage))
                    {
                        var commitMessage = RetrieveNodeValueIfExists("message", nodeCommit);
                        if (!string.IsNullOrEmpty(commitMessage))
                        {
                            if (commitMessage.ToLower().Contains(filterMessage))
                            {
                                inFilterMessage = true;
                            }
                            else
                            {
                                inFilterMessage = false;
                            }
                        }
                    }

                    if (inFilterDateRange && inFilterUser && inFilterMessage)
                    {

                        // Calculate the number of days ago
                        TimeSpan elapsed = now.Subtract(dateCommit);
                        int daysAgo = Convert.ToInt32(elapsed.TotalDays);
                        if (debugRoutine) DebugVariable(() => daysAgo);

                        // Add a new day node when a commit was older than the previous commit we parsed
                        if (daysAgo > daysPassed)
                        {
                            nodeDay = xmlCommits.CreateElement("day", null);

                            // Generate a readable label
                            string label = "Today";
                            if (daysAgo == 0)
                            {
                                label = "Today";
                            }
                            else
                            {
                                if (daysAgo == 1)
                                {
                                    label = "Yesterday";
                                }
                                else
                                {
                                    if (daysAgo <= daysAgoSinceNewYear)
                                    {
                                        label = dateCommit.ToString("M");
                                    }
                                    else
                                    {
                                        label = dateCommit.ToString("M") + ", " + dateCommit.Year.ToString();
                                    }
                                }
                            }

                            // Add attributes to the new day node
                            SetAttribute(nodeDay, "label", label);
                            SetAttribute(nodeDay, "daysAgo", daysAgo.ToString());

                            // Append the new day node
                            xmlCommits.DocumentElement.AppendChild(nodeDay);

                            // Store the current days ago variable to avoid that each commit is wrapped into a new <day/> node
                            daysPassed = daysAgo;
                        }

                    }

                    // Stick the current commit node inside the day node
                    if (inFilterDateRange && inFilterUser && inFilterMessage)
                    {
                        nodeDay.AppendChild(nodeCommit);
                    }

                }

            }

            if (debugRoutine)
            {
                html.AppendLine("<pre>" + Environment.NewLine + HtmlEncodeForDisplay(xmlCommits.OuterXml) + Environment.NewLine + "</pre>");
                saved = await SaveXmlDocument(xmlCommits, logRootPathOs + "/_auditorview-parsed.xml");
            }

            var languages = RetrieveProjectLanguages(projectVars.editorId);

            XsltArgumentList xsltArgumentList = new XsltArgumentList();
            xsltArgumentList.AddParam("permissions", "", string.Join(",", projectVars.currentUser.Permissions.Permissions.ToArray()));
            var langCount = 1;
            foreach (string lang in languages)
            {
                xsltArgumentList.AddParam($"language{langCount}", "", lang);
                langCount++;
            }

            html.AppendLine(TransformXml(xmlCommits, CalculateFullPathOs("cms_xsl_auditor-view"), xsltArgumentList));

            return html.ToString();
        }


        /// <summary>
        /// Search through the auditor view content to retrieve the history of an item from the auditor view
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="commitHash"></param>
        /// <param name="sitestructureId"></param>
        /// <param name="linkName"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RetrieveSectionHistory(string projectId, string commitHash, string sitestructureId, string objectType, string linkName, string userId)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = siteType == "local" || siteType == "dev";

            // Force a delay to simulate prod environment
            // if (siteType == "local") await PauseExecution(3000);

            // TODO: Consider using git log...
            // git log --follow -p -- path-to-file
            // or - alternatively - https://jonas.github.io/tig/doc/tig.1.html
            // https://stackoverflow.com/questions/278192/view-the-change-history-of-a-file-using-git-versioning

            // Basic debug string
            var baseDebugInfo = $"projectId: {projectId}, commitHash: {commitHash}, sitestructureId: {sitestructureId}, objectType: {objectType}, linkName: {linkName}";

            try
            {
                var contentLanguage = "";
                var sectionXmlFileName = "";

                switch (objectType)
                {
                    case "section":
                        // => Retrieve meta information about the item that we are comparing
                        var (Result, SiteStructureId, ContentLanguage, FileName) = await _retrieveFilingDataMetaInformation(projectVars, sitestructureId, linkName);
                        if (!Result.Success)
                        {
                            // Append the base debug information
                            Result.DebugInfo = $"{Result.DebugInfo}, {baseDebugInfo}";
                            return Result;
                        }
                        sitestructureId = SiteStructureId;
                        contentLanguage = ContentLanguage;
                        sectionXmlFileName = FileName;
                        break;

                    case "hierarchy":
                        contentLanguage = sitestructureId[Math.Max(0, sitestructureId.Length - 2)..];
                        var editorId = RetrieveEditorIdFromProjectId(projectId);
                        var nodeEditor = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id='{editorId}']");
                        if (nodeEditor == null) return new TaxxorReturnMessage(false, "Editor not found", baseDebugInfo);
                        var nodeVariant = nodeEditor.SelectSingleNode($"output_channels/output_channel/variants/variant[@id='{sitestructureId}']");
                        if (nodeVariant == null) return new TaxxorReturnMessage(false, "Variant not found", baseDebugInfo);
                        var metadataId = nodeVariant.Attributes["metadata-id-ref"]?.Value ?? "";
                        if (string.IsNullOrEmpty(metadataId)) return new TaxxorReturnMessage(false, "Metadata id not found", baseDebugInfo);
                        var nodeHierarchy = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']/metadata_system/metadata[@id='{metadataId}']/location");
                        if (nodeHierarchy == null) return new TaxxorReturnMessage(false, "Hierarchy node not found", baseDebugInfo);
                        sectionXmlFileName = nodeHierarchy.InnerText;

                        break;


                    default:
                        return new TaxxorReturnMessage(false, "Content type not supported", baseDebugInfo);

                }


                if (debugRoutine) Console.WriteLine($"- sitestructureId: {sitestructureId}, contentLanguage: {contentLanguage}, sectionXmlFileName: {sectionXmlFileName}");

                // => Retrieve the sorted XML Commit Data
                var xmlCommits = new XmlDocument();

                string? xmlCommitContent = null;
                string memCacheKey = _renderAuditorViewCacheKey(projectId, projectVars.outputChannelDefaultLanguage, userId);
                if (MemoryCacheItemExists(memCacheKey))
                {
                    // Grab the value from the cache
                    if (debugRoutine) appLogger.LogInformation($"Use cache with key: {memCacheKey}");
                    xmlCommitContent = RetrieveCacheContent<String>(memCacheKey);

                    // Load it into the XmlDocument
                    try
                    {
                        xmlCommits.LoadXml(xmlCommitContent);
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorReturnMessage(false, "Could not retrieve auditor view information", $"Unable to load the xml content retrieved from the git repository (CACHED) in a XmlDocument object. error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }

                // Load the data from the GIT repositories from the Taxxor Document Store
                if (string.IsNullOrEmpty(xmlCommitContent))
                {
                    // It looks like we somehow failed to use the cache
                    if (siteType == "local") appLogger.LogInformation($"Do not use cache with key: {memCacheKey}");

                    xmlCommits = await _retrieveAuditorViewContentAndCache(false, true, debugRoutine);
                }

                if (debugRoutine)
                {
                    await xmlCommits.SaveAsync($"{logRootPathOs}/commitinfo.xml");
                }

                //
                // => Check if this file was in part of the initial commit -> if so, then it needs to be added
                //
                var initialTag = "v0.0";
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectVars.projectId },
                    { "source", "filingdata" },
                    { "hashortag", initialTag },
                    { "component", "documentstore" }
                };
                XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitcommitinfo", dataToPost, true);

                XmlDocument xmlCommitMessage = new XmlDocument();
                if (!XmlContainsError(responseXml))
                {
                    xmlCommitMessage.LoadXml(HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/payload").InnerText));

                    // Console.WriteLine(PrettyPrintXml(xmlCommitMessage));

                    var filesInCommit = xmlCommitMessage.SelectSingleNode("*/files")?.InnerText ?? "";

                    var searchString = (objectType == "hierarchy") ? sectionXmlFileName : $"/textual/{sectionXmlFileName}";

                    if (filesInCommit.Contains(searchString))
                    {
                        if (debugRoutine) Console.WriteLine($"!!!! {sectionXmlFileName} IN INITIAL COMMIT !!!!");

                        //
                        // => Construct a new commit message abd append it to the xmlCommitContent object
                        //
                        RenameNode(xmlCommitMessage.DocumentElement, "commit");
                        RemoveXmlNode(xmlCommitMessage.SelectSingleNode("*/files"));

                        // Since all the compare logic uses the value of the hash to extract the files, we stick the tag value into the hash attribute
                        xmlCommitMessage.DocumentElement.SetAttribute("hash", initialTag);

                        xmlCommitMessage.DocumentElement.SetAttribute("repro", "project-data");
                        xmlCommitMessage.DocumentElement.SetAttribute("latest", "false");
                        var nodeMessage = xmlCommitMessage.SelectSingleNode("//message");
                        nodeMessage.InnerText = "";
                        var nodeCrud = xmlCommitMessage.CreateElementWithText("crud", "c");
                        var nodeLinkName = xmlCommitMessage.CreateElementWithText("linkname", $"{linkName} (original version)");
                        var nodeItemId = xmlCommitMessage.CreateElementWithText("id", sitestructureId);
                        nodeMessage.AppendChild(nodeCrud);
                        nodeMessage.AppendChild(nodeLinkName);
                        nodeMessage.AppendChild(nodeItemId);
                        if (debugRoutine) Console.WriteLine($"- xmlCommitMessage: \n\n{xmlCommitMessage.OuterXml}");

                        var nodeInitialCommit = xmlCommits.ImportNode(xmlCommitMessage.DocumentElement, true);
                        xmlCommits.DocumentElement.AppendChild(nodeInitialCommit);
                    }
                    else
                    {
                        if (debugRoutine) Console.WriteLine($"!!!! UNABLE TO LOCATE {sectionXmlFileName} IN INITIAL COMMIT !!!!");
                    }
                }
                else
                {

                }

                if (debugRoutine)
                {
                    await xmlCommits.SaveAsync($"{logRootPathOs}/commitinfo.complete.xml");
                }


                //
                // => Filter the list so that it only contains versions older than the current hash
                //
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var xmlSectionHistory = new XmlDocument();
                xmlSectionHistory.AppendChild(xmlSectionHistory.CreateElement("commits"));
                var xPathSearch = $"/commits/commit[(message/id = '{sitestructureId}' or message/id = '{sectionXmlFileName}') and (message/crud = 'c' or message/crud = 'u' or message/crud = 'd' or message/crud = 'transform' or message/crud = 'findreplace' or message/crud = 'contentdatarestore')]";
                // Console.WriteLine($"- xPathSearch: {xPathSearch}");
                // Console.WriteLine($"- commitHash: {commitHash}");
                var nodeListCommitsCurrentSection = xmlCommits.SelectNodes(xPathSearch);
                var startClone = false;
                foreach (XmlNode nodeCommit in nodeListCommitsCurrentSection)
                {
                    if (startClone)
                    {
                        var nodeCommitCloned = nodeCommit.CloneNode(true);
                        var nodeCommitImported = xmlSectionHistory.ImportNode(nodeCommitCloned, true);
                        xmlSectionHistory.DocumentElement.AppendChild(nodeCommitImported);
                    }
                    if (nodeCommit.GetAttribute("hash") == commitHash) startClone = true;
                }
                if (siteType == "local")
                {
                    watch.Stop();
                    appLogger.LogInformation($"Filter commits took: {watch.ElapsedMilliseconds.ToString()} ms");
                }

                if (debugRoutine)
                {
                    var saved = await SaveXmlDocument(xmlSectionHistory, $"{logRootPathOs}/_auditorview-itemhistory.xml");
                }


                // => Build JSON to return
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully generated section history";
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = "You can place debug information here";
                }
                jsonData.result.meta = new ExpandoObject();
                jsonData.result.meta.contentlanguage = contentLanguage;
                jsonData.result.meta.datareference = sectionXmlFileName;
                jsonData.result.history = new List<ExpandoObject>();

                var nodeListCommits = xmlSectionHistory.SelectNodes("/commits/commit");
                foreach (XmlNode nodeCommit in nodeListCommits)
                {
                    dynamic commitDetails = new ExpandoObject();
                    commitDetails.hash = nodeCommit.GetAttribute("hash");
                    commitDetails.linkname = nodeCommit.SelectSingleNode("message/linkname")?.InnerText ?? "";
                    commitDetails.repro = nodeCommit.GetAttribute("repro");
                    commitDetails.dateepoch = Convert.ToInt32(nodeCommit.SelectSingleNode("date")?.GetAttribute("epoch") ?? "0");
                    commitDetails.author = new ExpandoObject();
                    commitDetails.author.id = nodeCommit.SelectSingleNode("author/id")?.InnerText ?? "";
                    commitDetails.author.name = nodeCommit.SelectSingleNode("author/name")?.InnerText ?? "";
                    jsonData.result.history.Add(commitDetails);
                }

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);


                return new TaxxorReturnMessage(true, "Successfully retrieved section history", jsonToReturn, baseDebugInfo);


            }
            catch (Exception overallError)
            {
                return new TaxxorReturnMessage(false, "There was a problem retrieving the history of your section content", $"error: {overallError.ToString()}, {baseDebugInfo}");
            }
        }

        /// <summary>
        /// Compares two sections from the GIT history
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="repositoryIdentifier"></param>
        /// <param name="outputChannelLanguage"></param>
        /// <param name="dataFileReference"></param>
        /// <param name="latestCommitHash"></param>
        /// <param name="baseCommitHash"></param>
        /// <param name="outputFormat"></param>
        /// <param name="sitestructureId"></param>
        /// <param name="linkName"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CompareSectionContent(string projectId, string repositoryIdentifier, string outputChannelLanguage, string dataFileReference, string latestCommitHash, string baseCommitHash, string outputFormat, string sitestructureId, string linkName)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";

            // Basic debug string
            var baseDebugInfo = $"projectId: {projectId}, repositoryIdentifier: {repositoryIdentifier}, outputChannelLanguage: {outputChannelLanguage}, dataFileReference: {dataFileReference}, latestCommitHash: {latestCommitHash}, baseCommitHash: {baseCommitHash}, outputFormat: {outputFormat}, sitestructureId: {sitestructureId}, linkName: {linkName}";

            try
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);



                // Make sure that all the stylesheets are pr-loaded in memory
                SetupPdfStylesheetCache(reqVars);


                // Map the repository identifier to a location ID as defined in the data_configuration.xml file
                var gitLocationId = "";
                switch (repositoryIdentifier)
                {
                    case "project-data":
                        gitLocationId = "reportdataroot";
                        break;

                    default:
                        return new TaxxorReturnMessage(false, "Unable to compare content", $"Repository type '{repositoryIdentifier}' not yet supported");
                }

                var xpath = "";
                var step = 1;
                var stepMax = (outputFormat == "pdf") ? 5 : 2;
                projectVars.outputChannelVariantLanguage = outputChannelLanguage;
                projectVars.outputChannelType = "pdf";

                var generatedTrackChangesPdfFileName = $"diff-{sitestructureId}.pdf";
                var generatedTrackChangesHtmlFileName = $"diff-{sitestructureId}.html";


                // await _restoreMessageToClient($"DEBUG: restore-result: {restoreFilingContentDataResult.ToString()}");


                //
                // => Create a folder on the shared location that will contain the files that we are working with
                //
                var tempFolderPath = $"temp/contentcompare_{projectVars.projectId}-{RandomString(24)}";
                var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/{tempFolderPath}";
                var restoredFilingContentDataPathOs = dataFileReference.StartsWith("/metadata/") ? $"{tempFolderPathOs}/{dataFileReference.Replace("/metadata/", "")}" : $"{tempFolderPathOs}/{dataFileReference}";
                try
                {
                    Directory.CreateDirectory(tempFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Unable to create folder for storing the compare data", $"error: {ex}, tempFolderPathOs: {tempFolderPathOs}, {baseDebugInfo}");
                }


                //
                // => Render the difference as a track changes PDF file
                //
                if (outputFormat == "pdf")
                {
                    var hierarchicalLevel = -1;


                    //
                    // => Find the outputchannel variant ID by using the sitestructure ID and trying to find out if we can find it in one of the hierarchies
                    //
                    var foundWithItemId = false;
                    var outputChannelVariantId = projectVars.outputChannelVariantId;
                    var xmlOutputChannelHierarchies = await RenderOutputChannelHierarchyOverview(projectVars, false, false, true);
                    var nodeOutputChannel = xmlOutputChannelHierarchies.SelectSingleNode($"/hierarchies/output_channel[items/structured//item[@id={GenerateEscapedXPathString(sitestructureId)} and @data-ref={GenerateEscapedXPathString(dataFileReference)}]]");
                    if (nodeOutputChannel == null)
                    {
                        nodeOutputChannel = xmlOutputChannelHierarchies.SelectSingleNode($"/hierarchies/output_channel[@data-ref={GenerateEscapedXPathString(dataFileReference)}]");
                    }
                    else
                    {
                        foundWithItemId = true;
                    }

                    if (nodeOutputChannel != null && !string.IsNullOrEmpty(nodeOutputChannel.GetAttribute("id")))
                    {
                        outputChannelVariantId = nodeOutputChannel.GetAttribute("id");
                        // appLogger.LogCritical($"Found output channel variant ID: {outputChannelVariantId}");

                        var nodeItem = nodeOutputChannel.SelectSingleNode(foundWithItemId ? $"items/structured//item[@id={GenerateEscapedXPathString(sitestructureId)}]" : $"items/structured//item[@data-ref={GenerateEscapedXPathString(dataFileReference)}]");
                        if (nodeItem != null) hierarchicalLevel = nodeItem.SelectNodes($"ancestor::item").Count;
                    }
                    else
                    {
                        appLogger.LogWarning($"Unable to locate data reference with sitestructure id: '{sitestructureId}' and datareference: '{dataFileReference}' in an output channel hierarchy");
                    }


                    //
                    // => Retrieve the latest version of the section content
                    //
                    step = await _compareMessageToClient($"Retrieving latest version of section-ref {dataFileReference} with language {outputChannelLanguage.ToUpper()}.", step, stepMax);

                    // Restore the file from the commit hash
                    var pathToRestore = dataFileReference.StartsWith("/metadata")
                        ? $"version_1{dataFileReference}"
                        : $"version_1/textual/{dataFileReference}";
                    var xmlLatestFilingContentData = new XmlDocument();
                    var latestSectionXmlFileName = dataFileReference.Replace(".xml", "_latest.xml");
                    var latestSectionXmlFilePathOs = $"{tempFolderPathOs}/{latestSectionXmlFileName}";
                    var restoreFilingContentDataResult = await DocumentStoreService.VersionControl.GitExtractSingleFile(projectId, gitLocationId, latestCommitHash, pathToRestore, tempFolderPath, debugRoutine);
                    if (!restoreFilingContentDataResult.Success)
                    {
                        return restoreFilingContentDataResult;
                    }

                    // Test if restored content data file has successfully arrived on it's new location
                    if (!File.Exists(restoredFilingContentDataPathOs))
                    {
                        return new TaxxorReturnMessage(false, "Could not locate latest restored file", $"restoredFilingContentDataPathOs: {restoredFilingContentDataPathOs}, {baseDebugInfo}");
                    }

                    // Rename the file to match the "latest" version name
                    try
                    {
                        File.Move(restoredFilingContentDataPathOs, latestSectionXmlFilePathOs);
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorReturnMessage(false, "Could not rename latest restored file", $"restoredFilingContentDataPathOs: {restoredFilingContentDataPathOs}, latestSectionXmlFilePathOs: {latestSectionXmlFilePathOs}, error: {ex}, {baseDebugInfo}");
                    }

                    // Load the latest section content
                    try
                    {
                        xmlLatestFilingContentData.Load(latestSectionXmlFilePathOs);
                        var nodeListArticles = xmlLatestFilingContentData.SelectNodes("//article");
                        foreach (XmlNode nodeArticle in nodeListArticles)
                        {
                            nodeArticle.SetAttribute("data-ref", dataFileReference);
                        }
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorReturnMessage(false, "Could not load content of the latest restored content data file", $"error: {ex}, latestSectionXmlFilePathOs: {latestSectionXmlFilePathOs}, {baseDebugInfo}");
                    }

                    // Make sure that a data-guidlegalentity is present on the body node
                    AddLegalEntityGuids(ref xmlLatestFilingContentData);

                    //
                    // => Retrieve the base version of the section content
                    //
                    step = await _compareMessageToClient($"Retrieving base version of section-ref {dataFileReference} with language {outputChannelLanguage.ToUpper()}.", step, stepMax);
                    pathToRestore = dataFileReference.StartsWith("/metadata")
                        ? $"version_1{dataFileReference}"
                        : $"version_1/textual/{dataFileReference}";
                    var xmlBaseFilingContentData = new XmlDocument();
                    var baseSectionXmlFileName = dataFileReference.Replace(".xml", "_base.xml");
                    var baseSectionXmlFilePathOs = $"{tempFolderPathOs}/{baseSectionXmlFileName}";
                    restoreFilingContentDataResult = await DocumentStoreService.VersionControl.GitExtractSingleFile(projectId, gitLocationId, baseCommitHash, pathToRestore, tempFolderPath, debugRoutine);
                    if (!restoreFilingContentDataResult.Success)
                    {
                        return restoreFilingContentDataResult;
                    }

                    // Test if restored content data file has successfully arrived on it's new location
                    if (!File.Exists(restoredFilingContentDataPathOs))
                    {
                        return new TaxxorReturnMessage(false, "Could not locate base restored file", $"restoredFilingContentDataPathOs: {restoredFilingContentDataPathOs}, {baseDebugInfo}");
                    }

                    // Rename the file to match the "base" version name
                    try
                    {
                        File.Move(restoredFilingContentDataPathOs, baseSectionXmlFilePathOs);
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorReturnMessage(false, "Could not rename base restored file", $"restoredFilingContentDataPathOs: {restoredFilingContentDataPathOs}, baseSectionXmlFilePathOs: {baseSectionXmlFilePathOs}, error: {ex}, {baseDebugInfo}");
                    }

                    // Load the base section content
                    try
                    {
                        xmlBaseFilingContentData.Load(baseSectionXmlFilePathOs);
                        var nodeListArticles = xmlBaseFilingContentData.SelectNodes("//article");
                        foreach (XmlNode nodeArticle in nodeListArticles)
                        {
                            nodeArticle.SetAttribute("data-ref", dataFileReference);
                        }
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorReturnMessage(false, "Could not load content of the base restored content data file", $"error: {ex}, latestSectionXmlFilePathOs: {latestSectionXmlFilePathOs}, {baseDebugInfo}");
                    }

                    // Make sure that a data-guidlegalentity is present on the body node
                    AddLegalEntityGuids(ref xmlBaseFilingContentData);


                    //
                    // => Setup the properties for the PDF we want to generate
                    //
                    step = await _compareMessageToClient($"Prepare PDF comparison objects.", step, stepMax);
                    PdfProperties pdfProperties = new PdfProperties();
                    pdfProperties.Sections = sitestructureId;
                    pdfProperties.Secure = true;
                    pdfProperties.TrackChanges = true;
                    pdfProperties.Mode = "diff";
                    pdfProperties.Latest = latestCommitHash;
                    pdfProperties.Base = baseCommitHash;
                    var layoutDetails = RetrieveOutputChannelDefaultLayout(projectVars.projectId, outputChannelVariantId, projectVars.editorId, "pdf");
                    pdfProperties.Layout = layoutDetails.Layout;

                    // Retrieve output channel hierarchy to get the top node value and use that as a string to send to the PDF generator
                    var booktitle = "";
                    var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, "pdf", outputChannelVariantId, projectVars.outputChannelVariantLanguage);

                    if (!projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                    {
                        var hierarchyRetrieveResult = await RetrieveOutputChannelHierarchiesMetaData(projectVars, reqVars, true);
                        if (!hierarchyRetrieveResult) return new TaxxorReturnMessage(false, "Unable to retrieve hierarchy metadata", baseDebugInfo);
                    }

                    if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                    {
                        var xmlHierarchyDoc = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;
                        booktitle = xmlHierarchyDoc.SelectSingleNode("/items/structured/item/web_page/linkname")?.InnerText ?? "";
                    }
                    else
                    {
                        appLogger.LogError($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}'], stack-trace: {GetStackTrace()}");
                    }
                    if (booktitle != "")
                    {
                        pdfProperties.PdfGeneratorStringSet.Add("booktitle", booktitle);
                    }

                    //
                    // => Convert to dynamic headers
                    //
                    var stylesheetId = "cms_xsl_dynamic-header-levels";
                    var xsltStylesheet = PdfXslCache.Get(stylesheetId);
                    xmlLatestFilingContentData = ConvertToHierarchicalHeaders(xmlLatestFilingContentData, xsltStylesheet, hierarchicalLevel);
                    xmlBaseFilingContentData = ConvertToHierarchicalHeaders(xmlBaseFilingContentData, xsltStylesheet, hierarchicalLevel);


                    //
                    // => Generate the HTML that we need to send to to the PDF generator
                    //   
                    var xmlHtmlLatestFilingContentData = ConvertXmlFilingDataToBasicPdfGeneratorDocument(xmlLatestFilingContentData, dataFileReference, outputChannelLanguage);
                    var xmlHtmlBaseFilingContentData = ConvertXmlFilingDataToBasicPdfGeneratorDocument(xmlBaseFilingContentData, dataFileReference, outputChannelLanguage);

                    //
                    // => Prepare the documents
                    //
                    var latestHtmlPreperationResult = await PreparePdfHtmlForRendering(xmlHtmlLatestFilingContentData, projectVars, pdfProperties);
                    if (!latestHtmlPreperationResult.Success)
                    {
                        return latestHtmlPreperationResult;
                    }
                    xmlHtmlLatestFilingContentData.LoadXml(latestHtmlPreperationResult.XmlPayload.OuterXml);

                    var baseHtmlPreperationResult = await PreparePdfHtmlForRendering(xmlHtmlBaseFilingContentData, projectVars, pdfProperties);
                    if (!baseHtmlPreperationResult.Success)
                    {
                        return baseHtmlPreperationResult;
                    }
                    xmlHtmlBaseFilingContentData.LoadXml(baseHtmlPreperationResult.XmlPayload.OuterXml);


                    var xhtmlLatestFilingContentData = await PdfGeneratorPreprocessXml(xmlHtmlLatestFilingContentData, "pdf", sitestructureId, projectVars);
                    var xhtmlBaseFilingContentData = await PdfGeneratorPreprocessXml(xmlHtmlBaseFilingContentData, "pdf", sitestructureId, projectVars);

                    // Latest version includes all the chrome
                    var latestXhtmlPreparationResult = await PrepareSectionXhtmlForDiffPdfGeneration(xhtmlLatestFilingContentData, reqVars, projectVars, pdfProperties);
                    if (!latestXhtmlPreparationResult.Success)
                    {
                        return latestXhtmlPreparationResult;
                    }
                    xhtmlLatestFilingContentData = latestXhtmlPreparationResult.Payload;

                    // Base version without chrome
                    var baseXhtmlPreparationResult = PrepareSectionXhtmlForDiffPdfGeneration(xhtmlBaseFilingContentData);
                    if (!baseXhtmlPreparationResult.Success)
                    {
                        return baseXhtmlPreparationResult;
                    }
                    xhtmlBaseFilingContentData = baseXhtmlPreparationResult.Payload;


                    //
                    // => Prepare the XML objects so that they resolve in the same way as when you would load them via the editor (partially complete)
                    //
                    var xmlTempLatest = new XmlDocument();
                    var xmlTempBase = new XmlDocument();
                    xmlTempLatest.LoadXml(xhtmlLatestFilingContentData);
                    xmlTempBase.LoadXml(xhtmlBaseFilingContentData);

                    xmlTempLatest.ReplaceContent(DynamicPlaceholdersResolve(xmlTempLatest, reqVars, projectVars, null), true);
                    xmlTempBase.ReplaceContent(DynamicPlaceholdersResolve(xmlTempBase, reqVars, projectVars, null), true);

                    xhtmlLatestFilingContentData = xmlTempLatest.OuterXml;
                    xhtmlBaseFilingContentData = xmlTempBase.OuterXml;

                    if (debugRoutine)
                    {
                        await TextFileCreateAsync(xhtmlLatestFilingContentData, $"{tempFolderPathOs}/{latestSectionXmlFileName.Replace(".xml", ".html")}");
                        await TextFileCreateAsync(xhtmlBaseFilingContentData, $"{tempFolderPathOs}/{baseSectionXmlFileName.Replace(".xml", ".html")}");
                    }

                    // 
                    // => Generate the diff HTML                    
                    // 
                    var diffHtml = await PdfService.RenderDiffHtml(xhtmlBaseFilingContentData, xhtmlLatestFilingContentData);

                    if (debugRoutine)
                    {
                        await TextFileCreateAsync(diffHtml, $"{logRootPathOs}/_diff-pdf-auditor.html");
                    }


                    //
                    // => Replace the dynamic placeholders in the paths
                    //
                    var xmlDiffHtml = new XmlDocument();
                    xmlDiffHtml.LoadHtml(diffHtml);


                    //
                    // => Improve the track changes marks
                    //
                    PostProcessTrackChangesHtml(ref xmlDiffHtml);
                    if (debugRoutine)
                    {
                        await TextFileCreateAsync(diffHtml, $"{logRootPathOs}/_diff-pdf-auditor.postprocessed.html");
                    }


                    xmlDiffHtml.ReplaceContent(DynamicPlaceholdersResolve(xmlDiffHtml, reqVars, projectVars, null), true);


                    var xmlDiffHtmlReworked = new XmlDocument();
                    // In this new diff xhtml document, use the body node from the "latest" version as this one contains all the required attributes which we need to correctly render the HTML
                    var nodeBody = xmlDiffHtmlReworked.ImportNode(xmlTempLatest.SelectSingleNode("//body"), false);
                    var nodeContent = xmlDiffHtmlReworked.CreateElement("content");
                    nodeBody.AppendChild(nodeContent);
                    xmlDiffHtmlReworked.AppendChild(nodeBody);

                    // Select the articles and stick in basic XML structure
                    var nodeListDiffArticles = xmlDiffHtml.SelectNodes("//article");
                    foreach (XmlNode nodeDiffArticle in nodeListDiffArticles)
                    {
                        nodeContent.AppendChild(xmlDiffHtmlReworked.ImportNode(nodeDiffArticle, true));
                    }

                    //
                    // => Generate the PDF
                    //
                    step = await _compareMessageToClient($"Generate comparison/track changes PDF file", step, stepMax);
                    var dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("serveasdownload", "false");
                    dataToPost.Add("type", "json");
                    dataToPost.Add("securepdf", pdfProperties.Secure ? "true" : "false");


                    //
                    // => Post process the XHTML for the PDF Generator based on the render engine used
                    //
                    switch (PdfRenderEngine)
                    {
                        case "pagedjs":

                            // - Inject the PDF HTML into the viewer file wich is part of the Report Design Package
                            var pagedMediaEnvelopeResult = await PdfService.InjectInPagedMediaEnvelope(projectVars, xmlDiffHtmlReworked.OuterXml);
                            if (!pagedMediaEnvelopeResult.Success) return pagedMediaEnvelopeResult;

                            // - Replace the existing HTML data with the one we received back from the routine
                            dataToPost.Add("url", pagedMediaEnvelopeResult.Payload);

                            break;

                        case "prince":
                            // - Inject the diff HTML into the original xmlTempLatest object which contains all the information for Prince
                            var nodeContentImported = xmlTempLatest.ImportNode(nodeContent, true);
                            var nodeLatestBody = xmlTempLatest.SelectSingleNode("//body");
                            var nodeListToRemove = nodeLatestBody.SelectNodes("*");
                            RemoveXmlNodes(nodeListToRemove);
                            nodeLatestBody.AppendChild(nodeContentImported);
                            dataToPost.Add("url", xmlTempLatest.OuterXml);

                            break;

                        // Other render engines to be placed here


                        default:
                            return new TaxxorReturnMessage(false, "There was a problem comparing your section content", $"unsupported PDF render engine: {PdfRenderEngine}, {baseDebugInfo}");
                    }

                    //
                    // => Add parameters for the PDF generation engine
                    //
                    ExtendPdfGeneratorPostParameters(ref dataToPost, pdfProperties, projectVars.editorId);

                    //
                    // => Dump the HTML that we are sending to the PDF Generator on the disk
                    //
                    await TextFileCreateAsync(dataToPost["url"], CalculateFullPathOs("pdfgenerator-dumpfile"));

                    //
                    // => Render the PDF and grab the result in an XML envelope
                    //
                    var xmlPdfGenerationResult = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.PdfService, RequestMethodEnum.Post, "renderpdf", dataToPost, true);

                    if (XmlContainsError(xmlPdfGenerationResult))
                    {
                        // An error has occurred - now constuct a solid error message
                        var errorMessage = "An error occurred while generating your PDF";
                        var debugInfo = xmlPdfGenerationResult.SelectSingleNode("/error/debuginfo").InnerText;

                        // Check if we can capture additional details from the PDF Generator
                        XmlNode? nodeErrorInfoPdfGenerator = xmlPdfGenerationResult.SelectSingleNode("/error/httpresponse/root/error");
                        if (nodeErrorInfoPdfGenerator != null)
                        {
                            try
                            {
                                errorMessage = nodeErrorInfoPdfGenerator.SelectSingleNode("message").InnerText;
                                debugInfo = nodeErrorInfoPdfGenerator.SelectSingleNode("debuginfo").InnerText;
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, "Something went wrong trying to retrieve additional error information");
                            }

                        }
                        return new TaxxorReturnMessage(false, errorMessage, $"debug-info: {debugInfo}, {baseDebugInfo}");
                    }

                    // Capture the base64 encoded result
                    xpath = "/root/content";
                    var nodePdfContent = xmlPdfGenerationResult.SelectSingleNode(xpath);
                    if (nodePdfContent == null)
                    {
                        return new TaxxorReturnMessage(false, "Could not retrieve base64 content of track changes PDF", $"xpath: {xpath}, {baseDebugInfo}");
                    }

                    // Grab the base 64 encoded blob from the XmlDocument that we have received
                    var base64PdfContent = nodePdfContent.InnerText;


                    //
                    // => Store the PDF in the temporary location and return the sub-path to the client so that it can be streamed
                    //
                    step = await _compareMessageToClient($"Store the comparison/track changes PDF file", step, stepMax);
                    Byte[] bytes = Base64DecodeToBytes(base64PdfContent);
                    File.WriteAllBytes($"{tempFolderPathOs}/{generatedTrackChangesPdfFileName}", bytes);

                    // Copy the generated PDF file to the temp root folder path so that the download routine can pick it up
                    try
                    {
                        var targetFilePathOs = $"{RetrieveSharedFolderPathOs()}/{generatedTrackChangesPdfFileName}";
                        if (File.Exists(targetFilePathOs)) File.Delete(targetFilePathOs);
                        File.Copy($"{tempFolderPathOs}/{generatedTrackChangesPdfFileName}", targetFilePathOs);
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorReturnMessage(false, "Could not move track changes PDF", $"error: {ex}, {baseDebugInfo}");
                    }


                }

                //
                // => Render the changes in a technical comparison format
                //
                else if (outputFormat == "raw")
                {
                    //
                    // => Use GIT Diff to generate a report of the XML differences between the two files
                    //
                    step = await _compareMessageToClient($"Retrieving the diff content", step, stepMax);
                    var pathToRestore = dataFileReference.StartsWith("/metadata")
                        ? $"version_1{dataFileReference}"
                        : $"version_1/textual/{dataFileReference}";
                    var gitDiffResult = await DocumentStoreService.VersionControl.GitDiffBetweenCommits(projectId, gitLocationId, baseCommitHash, latestCommitHash, pathToRestore, debugRoutine);

                    if (!gitDiffResult.Success)
                    {
                        return gitDiffResult;
                    }

                    //
                    // => Create an HTML file, with the content of diff report
                    //
                    step = await _compareMessageToClient($"Creating the diff-report", step, stepMax);
                    var htmlDiffContent = $@"
                    <html>
                        <head>
                            <title>Diff Report</title>
                            <meta charset='UTF-8'>
                            <link rel='stylesheet' type='text/css' href='{CalculateFullPathOs("html-diff-css")}'>
                            <script type='text/javascript' src='{CalculateFullPathOs("html-diff-js")}'></script>
                            <style type='text/css'>
                            	body {{
                            		font-family: 'Roboto', Helvetica, Arial, sans-serif;
                            		font-size: 14px;
									line-height: 1.42857;
									color: #333333;
                            	}}
                            </style>
                        </head>
                        <body>
                            <h1>Diff Report</h1>
                            <p>Difference report for page-id: {sitestructureId}, linkname: {linkName}</p>
                            <p>Base commit reference: {baseCommitHash}, latest commit reference: {latestCommitHash}</p>
                            <p>Difference details:</p>
                            <div id='diffresult' style='display: none'>
                            {(gitDiffResult.Message.ToLower().Contains("no differences found") ? gitDiffResult.Message : HtmlEncode(gitDiffResult.Payload))}
                            </div>
                            <div id='formatted'></div>
                            <script type='text/javascript'>
                                // Plain JS function to decode an HTML encoded string
                                var decodeHTML = function (html) {{
                                    var txt = document.createElement('textarea');
                                    txt.innerHTML = html;
                                    return txt.value;
                                }};
                                // Turn the diff output into a nicely formatted HTML view
                                var elDiffResult=document.getElementById('diffresult');
                                var elFormattedDiff=document.getElementById('formatted');
                                var diffHtml = Diff2Html.getPrettyHtml(
                                    decodeHTML(elDiffResult.innerHTML),
                                    {{
                                        inputFormat: 'diff', 
                                        showFiles: true, 
                                        matching: 'lines', 
                                        outputFormat: 'line-by-line'
                                    }}
                                );
                                elFormattedDiff.innerHTML = diffHtml;
                            </script>
                        </body>
                    </html>";


                    // Store the file
                    await TextFileCreateAsync(htmlDiffContent, $"{tempFolderPathOs}/{generatedTrackChangesHtmlFileName}");

                    // Copy the generated PDF file to the temp root folder path so that the download routine can pick it up
                    try
                    {
                        var targetFilePathOs = $"{RetrieveSharedFolderPathOs()}/{generatedTrackChangesHtmlFileName}";
                        if (File.Exists(targetFilePathOs)) File.Delete(targetFilePathOs);
                        File.Copy($"{tempFolderPathOs}/{generatedTrackChangesHtmlFileName}", targetFilePathOs);
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorReturnMessage(false, "Could not move diff HTML report", $"error: {ex}, {baseDebugInfo}");
                    }

                }
                else
                {
                    return new TaxxorReturnMessage(false, "Comparison output format not defined", $"outputFormat: {outputFormat}, {baseDebugInfo}");
                }

                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully generated section " + ((outputFormat == "pdf") ? " diff PDF" : " diff report");
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = "You can place debug information here";
                }
                jsonData.result.filename = (outputFormat == "pdf") ? generatedTrackChangesPdfFileName : generatedTrackChangesHtmlFileName;

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);


                return new TaxxorReturnMessage(true, jsonData.result.message, jsonToReturn, baseDebugInfo);


                void AddLegalEntityGuids(ref XmlDocument xmlData)
                {
                    var nodeBody = xmlData.SelectSingleNode("//body");
                    var guidLegalEntityValue = nodeBody.GetAttribute("data-guidlegalentity") ?? "";
                    if (guidLegalEntityValue == "" && nodeBody != null) nodeBody.SetAttribute("data-guidlegalentity", projectVars.guidLegalEntity ?? "");
                }
            }
            catch (Exception overallError)
            {
                var errorMessage = "There was a problem comparing your section content";
                appLogger.LogError(overallError, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, $"error: {overallError.ToString()}, {baseDebugInfo}");
            }



        }




        /// <summary>
        /// Restores content from a commit shown in the auditor view
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="userId"></param>
        /// <param name="repositoryIdentifier"></param>
        /// <param name="commitHash"></param>
        /// <param name="operationId"></param>
        /// <param name="sitestructureId"></param>
        /// <param name="linkName"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RestoreSectionContent(string projectId, string userId, string repositoryIdentifier, string commitHash, string operationId, string sitestructureId, string linkName, string objectType)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var debugRoutine = siteType == "local" || siteType == "dev";

            // Basic debug string
            var baseDebugInfo = $"userId: {userId}, projectId: {projectId}, commitHash: {commitHash}, repositoryIdentifier: {repositoryIdentifier}, operationId: {operationId}, sitestructureId: {sitestructureId}, linkName: {linkName}, objectType: {objectType}, siteType: {siteType}";

            // Map the repository identifier to a location ID as defined in the data_configuration.xml file
            var gitLocationId = "";
            switch (repositoryIdentifier)
            {
                case "project-data":
                    gitLocationId = "reportdataroot";
                    break;

                default:
                    return new TaxxorReturnMessage(false, "Unable to restore content", $"Repository type '{repositoryIdentifier}' not yet supported");
            }

            var xpath = "";
            var step = 1;
            var stepMax = 6;

            // await MessageToOtherClient("rutger.scheepens@philips.com", "From johan");



            try
            {
                //
                // => Figure out the language and filename of the content that we are trying to restore
                //
                step = await _restoreMessageToClient($"Determine language and filingcontent data reference.", step, stepMax);

                var contentLanguage = "";
                var sectionXmlFileName = "";
                var metadataId = "";

                switch (objectType)
                {
                    case "section":
                        // => Retrieve meta information about the item that we are comparing
                        var (Result, SiteStructureId, ContentLanguage, FileName) = await _retrieveFilingDataMetaInformation(projectVars, sitestructureId, linkName);
                        if (!Result.Success)
                        {
                            // Append the base debug information
                            Result.DebugInfo = $"{Result.DebugInfo}, {baseDebugInfo}";
                            return Result;
                        }
                        sitestructureId = SiteStructureId;
                        contentLanguage = ContentLanguage;
                        sectionXmlFileName = FileName;
                        break;

                    case "hierarchy":
                        contentLanguage = sitestructureId[Math.Max(0, sitestructureId.Length - 2)..];
                        var editorId = RetrieveEditorIdFromProjectId(projectId);
                        var nodeEditor = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id='{editorId}']");
                        if (nodeEditor == null) return new TaxxorReturnMessage(false, "Editor not found", baseDebugInfo);
                        var nodeVariant = nodeEditor.SelectSingleNode($"output_channels/output_channel/variants/variant[@id='{sitestructureId}']");
                        if (nodeVariant == null) return new TaxxorReturnMessage(false, "Variant not found", baseDebugInfo);
                        metadataId = nodeVariant.Attributes["metadata-id-ref"]?.Value ?? "";
                        if (string.IsNullOrEmpty(metadataId)) return new TaxxorReturnMessage(false, "Metadata id not found", baseDebugInfo);
                        var nodeHierarchy = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']/metadata_system/metadata[@id='{metadataId}']/location");
                        if (nodeHierarchy == null) return new TaxxorReturnMessage(false, "Hierarchy node not found", baseDebugInfo);
                        sectionXmlFileName = nodeHierarchy.InnerText;

                        break;


                    default:
                        return new TaxxorReturnMessage(false, "Content type not supported", baseDebugInfo);

                }

                var filePathToRestore = (objectType == "hierarchy") ? sectionXmlFileName : $"/textual/{sectionXmlFileName}";
                var fileNameToRestore = Path.GetFileName(filePathToRestore);


                // => Retrieve the current section content
                step = await _restoreMessageToClient($"Retrieving current version of section-ref {fileNameToRestore} with language {contentLanguage.ToUpper()}.", step, stepMax);
                var xmlCurrentFilingContentData = await DocumentStoreService.FilingData.Load<XmlDocument>(projectId, filePathToRestore, "cmsdataroot", true, false);
                if (XmlContainsError(xmlCurrentFilingContentData))
                {
                    return new TaxxorReturnMessage(false, "Unable to retrieve current version of the filing data", $"xpath: {xpath}, {baseDebugInfo}");
                }

                // appLogger.LogInformation(PrettyPrintXml(xmlCurrentFilingContentSourceData));


                //
                // => Retrieve the content from the GIT commit (the restored content)
                //
                step = await _restoreMessageToClient($"Retrieving archived version of section-ref {fileNameToRestore} with language {contentLanguage.ToUpper()}.", step, stepMax);

                // Create the folder to extract the restored content file to
                var tempFolderPath = $"temp/gitrestore_{projectVars.projectId}-{RandomString(24)}";
                var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/{tempFolderPath}";
                try
                {
                    Directory.CreateDirectory(tempFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Unable to create folder for extracting the data", $"error: {ex}, tempFolderPathOs: {tempFolderPathOs}, {baseDebugInfo}");
                }

                // Restore the file from the commit hash
                var restoreFilingContentDataResult = await DocumentStoreService.VersionControl.GitExtractSingleFile(projectId, gitLocationId, commitHash, $"version_1{filePathToRestore}", tempFolderPath, debugRoutine);
                if (!restoreFilingContentDataResult.Success)
                {
                    return restoreFilingContentDataResult;
                }

                // await _restoreMessageToClient($"DEBUG: restore-result: {restoreFilingContentDataResult.ToString()}");

                // Load the restored content data file
                var xmlRestoredFilingContentData = new XmlDocument();
                var restoredFilingContentDataPathOs = $"{tempFolderPathOs}/{fileNameToRestore}";
                if (!File.Exists(restoredFilingContentDataPathOs))
                {
                    return new TaxxorReturnMessage(false, "Could not locate restored file", $"restoredFilingContentTargetDataPathOs: {restoredFilingContentDataPathOs}, {baseDebugInfo}");
                }
                try
                {
                    xmlRestoredFilingContentData.Load(restoredFilingContentDataPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not load content of the restored content data file", $"error: {ex}, restoredFilingContentDataPathOs: {restoredFilingContentDataPathOs}, {baseDebugInfo}");
                }

                //
                // => Replace the current content (lang specific) with the restored content
                //
                step = await _restoreMessageToClient($"Replacing current content of language {contentLanguage.ToUpper()} with the content we restored.", step, stepMax);

                switch (objectType)
                {
                    case "section":

                        var nodeRestoredContent = xmlRestoredFilingContentData.SelectSingleNode($"/data/content[@lang='{contentLanguage}']");
                        var nodeOriginalContent = xmlCurrentFilingContentData.SelectSingleNode($"/data/content[@lang='{contentLanguage}']");
                        if (nodeOriginalContent == null)
                        {
                            return new TaxxorReturnMessage(true, "Could not find original XML content", $"{baseDebugInfo}");
                        }
                        if (nodeRestoredContent == null)
                        {
                            return new TaxxorReturnMessage(false, "Could not find restored XML content", $"{baseDebugInfo}");
                        }

                        try
                        {
                            ReplaceXmlNode(nodeOriginalContent, nodeRestoredContent);
                        }
                        catch (Exception ex)
                        {
                            return new TaxxorReturnMessage(true, "There was a problem replacing the original content of the data with the content we restored", $"error: {ex}, {baseDebugInfo}");
                        }

                        //
                        // => Update the modified date
                        //
                        var nodeDateModified = xmlCurrentFilingContentData.SelectSingleNode("/data/system/date_lastmodified");
                        if (nodeDateModified != null) xmlCurrentFilingContentData.SelectSingleNode("/data/system/date_lastmodified").InnerText = RetrieveDateAsString();

                        break;

                    case "hierarchy":
                        xmlCurrentFilingContentData.ReplaceContent(xmlRestoredFilingContentData);
                        break;


                    default:
                        return new TaxxorReturnMessage(false, "Content type not supported", baseDebugInfo);

                }

                //
                // => Save this data on the data service
                //
                step = await _restoreMessageToClient($"Saving the restored content back to the Taxxor Document Store.", step, stepMax);
                var xmlSaveResult = await DocumentStoreService.FilingData.Save<XmlDocument>(xmlCurrentFilingContentData, filePathToRestore, "cmsdataroot", true, false);
                if (XmlContainsError(xmlSaveResult))
                {
                    return new TaxxorReturnMessage(xmlSaveResult);
                }

                //
                // => Create a special commit that is clearly marked as a restore
                //
                step = await _restoreMessageToClient($"Adding a special auditor view commit.", step, stepMax);

                // Construct the commit message
                XmlDocument? xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                SetAttribute(xmlCommitMessage.SelectSingleNode("/root/crud"), "originalcommithash", commitHash);
                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "contentdatarestore";
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = linkName;
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = sitestructureId;
                var message = xmlCommitMessage.DocumentElement.InnerXml;

                // Execute the commit request
                var commitResult = await DocumentStoreService.VersionControl.GitCommit(projectId, gitLocationId, message, debugRoutine);
                if (!commitResult.Success)
                {
                    return commitResult;
                }

                //
                // => Clear the paged media cache
                //
                ContentCache.Clear(projectId);

                if (objectType == "hierarchy")
                {
                    //
                    // => Clears the RBAC cache
                    //
                    projectVars.rbacCache.ClearAll();

                    //
                    // => Clear the user section information cache data
                    //
                    UserSectionInformationCacheData.Clear();
                }

                //
                // => Update the in-memory CMS metadata content
                //
                await UpdateCmsMetadata(projectVars.projectId);

                return new TaxxorReturnMessage(true, "Successfully restored content", $"{baseDebugInfo}");
            }
            catch (Exception overallError)
            {
                var errorMessage = "There was a problem restoring your content";
                appLogger.LogError(overallError, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, $"error: {overallError.ToString()}, {baseDebugInfo}");
            }

        }

        /// <summary>
        /// Sends a progress message over the websocket connection about the progress of the restore process
        /// </summary>
        /// <param name="message"></param>
        /// <param name="step"></param>
        /// <param name="maxStep"></param>
        /// <returns></returns>
        private static async Task<int> _restoreMessageToClient(string message, int step = -1, int maxStep = -1)
        {
            if (step == -1)
            {
                await MessageToCurrentClient("RestoreSectionContentProgress", message);
            }
            else
            {
                await MessageToCurrentClient("RestoreSectionContentProgress", $"Step {step}/{maxStep}: {message}");
                step++;
            }

            return step;
        }

        /// <summary>
        /// Sends a progress message over the websocket connection about the progress of the compare process
        /// </summary>
        /// <param name="message"></param>
        /// <param name="step"></param>
        /// <param name="maxStep"></param>
        /// <returns></returns>
        private static async Task<int> _compareMessageToClient(string message, int step = -1, int maxStep = -1)
        {
            if (step == -1)
            {
                await MessageToCurrentClient("CompareSectionContentProgress", message);
            }
            else
            {
                await MessageToCurrentClient("CompareSectionContentProgress", $"Step {step}/{maxStep}: {message}");
                step++;
            }

            return step;
        }

        /// <summary>
        /// Retrieves important information about the section
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="sectionIdentifier">Can be a data reference or a site structure ID</param>
        /// <param name="linkName"></param>
        /// <returns></returns>
        private static async Task<(TaxxorReturnMessage Result, string SiteStructureId, string ContentLanguage, string FileName)> _retrieveFilingDataMetaInformation(ProjectVariables projectVars, string sectionIdentifier, string linkName)
        {
            var xmlOutputChannelHierarchies = await RenderOutputChannelHierarchyOverview(projectVars, false, true);

            if (XmlContainsError(xmlOutputChannelHierarchies, "/hierarchies//error"))
            {
                return (new TaxxorReturnMessage(false, "Unable to retrieve hierarchy overview", $"error: {xmlOutputChannelHierarchies.SelectSingleNode("//debuginfo")?.InnerText ?? ""}"), "", "", "");
            }

            return _retrieveFilingDataMetaInformation(xmlOutputChannelHierarchies, projectVars, sectionIdentifier, linkName);
        }

        /// <summary>
        /// Retrieves important information about the section
        /// </summary>
        /// <param name="xmlOutputChannelHierarchies"></param>
        /// <param name="projectVars"></param>
        /// <param name="identifier">Can be a data reference or a site structure ID</param>
        /// <param name="linkName"></param>
        /// <returns></returns>
        private static (TaxxorReturnMessage Result, string SiteStructureId, string ContentLanguage, string FileName) _retrieveFilingDataMetaInformation(XmlDocument xmlOutputChannelHierarchies, ProjectVariables projectVars, string identifier, string linkName)
        {
            var xpath = "";
            var siteStructureId = "";
            var contentLanguage = "";
            var sectionXmlFileName = "";
            var baseDebugInfo = $"identifier: {identifier}, linkName: {linkName}";
            var debugRoutine = siteType == "local";

            // Retrieve the site structure ID and the data reference
            if (identifier.EndsWith(".xml"))
            {
                sectionXmlFileName = identifier;
                xpath = $"/hierarchies/output_channel[items/structured//item/@data-ref={GenerateEscapedXPathString(identifier)}]";
            }
            else
            {
                siteStructureId = identifier;
                xpath = $"/hierarchies/output_channel[items/structured//item/@id={GenerateEscapedXPathString(identifier)}]";
            }

            var nodeListOutputChannels = xmlOutputChannelHierarchies.SelectNodes(xpath);
            if (nodeListOutputChannels.Count == 0)
            {
                appLogger.LogError($"Unable to find meta information about this item with xPath: {xpath}. number of items: {xmlOutputChannelHierarchies.SelectNodes("//item").Count}");
                if (debugRoutine) xmlOutputChannelHierarchies.Save($"{logRootPathOs}/-metadatahierarcyoverview.xml");
                return (new TaxxorReturnMessage(false, "Unable to find information of the section you want to compare or restore, probably because the section is not in use anymore", $"xpath: {xpath}"), "", "", "");
            }
            else if (nodeListOutputChannels.Count == 1)
            {
                // Found the site structure node based on the identifier
                contentLanguage = nodeListOutputChannels.Item(0).GetAttribute("lang");
                if (sectionXmlFileName == "")
                {
                    sectionXmlFileName = GetAttribute(nodeListOutputChannels.Item(0).SelectSingleNode($"items/structured//item[@id={GenerateEscapedXPathString(identifier)}]"), "data-ref");
                }
                if (siteStructureId == "")
                {
                    siteStructureId = GetAttribute(nodeListOutputChannels.Item(0).SelectSingleNode($"items/structured//item[@data-ref={GenerateEscapedXPathString(identifier)}]"), "id");
                }
            }
            else
            {
                // Test if the result consists of multiple languages
                var languagesFound = new List<string>();
                foreach (XmlNode nodeOutputChannnel in nodeListOutputChannels)
                {
                    var lang = nodeOutputChannnel.GetAttribute("lang");
                    if (!string.IsNullOrEmpty(lang) && !languagesFound.Contains(lang)) languagesFound.Add(lang);
                }

                if (languagesFound.Count > 1)
                {
                    // await _restoreMessageToClient("WARNING: multiple languages detected, attempt further refinement");

                    // This is a ubiquitous result that needs further refinement
                    var languagesFoundNested = new List<string>();
                    xpath = $"/hierarchies/output_channel[items/structured//item[(@id={GenerateEscapedXPathString(identifier)} or @data-ref={GenerateEscapedXPathString(identifier)}) and web_page/linkname={GenerateEscapedXPathString(linkName)}]]";
                    var nodeListOutputChannelsNested = xmlOutputChannelHierarchies.SelectNodes(xpath);
                    foreach (XmlNode nodeOutputChannelNested in nodeListOutputChannelsNested)
                    {
                        var lang = nodeOutputChannelNested.GetAttribute("lang");
                        if (!string.IsNullOrEmpty(lang) && !languagesFoundNested.Contains(lang)) languagesFoundNested.Add(lang);
                    }

                    xpath = $"/hierarchies/output_channel/items/structured//item[(@id={GenerateEscapedXPathString(identifier)} or @data-ref={GenerateEscapedXPathString(identifier)}) and web_page/linkname={GenerateEscapedXPathString(linkName)}]";
                    var nodeListItemsFound = xmlOutputChannelHierarchies.SelectNodes(xpath);

                    if (languagesFoundNested.Count == 1)
                    {
                        contentLanguage = nodeListOutputChannelsNested.Item(0).GetAttribute("lang");
                        sectionXmlFileName = GetAttribute(nodeListOutputChannelsNested.Item(0).SelectSingleNode($"items/structured//item[(@id={GenerateEscapedXPathString(identifier)} or @data-ref={GenerateEscapedXPathString(identifier)}) and web_page/linkname={GenerateEscapedXPathString(linkName)}]"), "data-ref");
                    }
                    else
                    {
                        contentLanguage = projectVars.outputChannelDefaultLanguage;
                        xpath = $"/hierarchies/output_channel[@lang={GenerateEscapedXPathString(contentLanguage)}]/items/structured//item[(@id={GenerateEscapedXPathString(identifier)} or @data-ref={GenerateEscapedXPathString(identifier)}) and web_page/linkname={GenerateEscapedXPathString(linkName)}]";
                        sectionXmlFileName = GetAttribute(xmlOutputChannelHierarchies.SelectSingleNode(xpath), "data-ref");
                        //await _restoreMessageToClient($"WARNING: multiple languages detected, defaulting to {contentLanguage.ToString().ToUpper()}");
                    }

                }
                else
                {
                    contentLanguage = nodeListOutputChannels.Item(0).GetAttribute("lang");
                    sectionXmlFileName = GetAttribute(nodeListOutputChannels.Item(0).SelectSingleNode($"items/structured//item[(@id={GenerateEscapedXPathString(identifier)} or @data-ref={GenerateEscapedXPathString(identifier)})]"), "data-ref");
                }

            }

            if (string.IsNullOrEmpty(siteStructureId))
            {
                return (new TaxxorReturnMessage(false, "There was an error retrieving all information about the section data", $"siteStructureId is null or empty, {baseDebugInfo}"), "", "", "");
            }

            if (string.IsNullOrEmpty(contentLanguage))
            {
                return (new TaxxorReturnMessage(false, "There was an error retrieving all information about the section data", $"contentLanguage is null or empty, {baseDebugInfo}"), "", "", "");
            }

            if (string.IsNullOrEmpty(sectionXmlFileName))
            {
                return (new TaxxorReturnMessage(false, "There was an error retrieving all information about the section data", $"sectionXmlFileName is null or empty, {baseDebugInfo}"), "", "", "");
            }


            return (new TaxxorReturnMessage(true, "Successfully retrieved section content metadata"), siteStructureId, contentLanguage, sectionXmlFileName);
        }


        /// <summary>
        /// Retrieves the commit log message from the XML stored in the GIT commit message
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="commitHash"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RetrieveGitCommitLogContents(string projectId, string commitHash)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";
            var errorMessage = "There was an error retrieving the GIT commit log contents";
            var gitLocationId = "reportdataroot";
            try
            {
                var dataToPost = new Dictionary<string, string>
                    {
                        { "source", "filingdata" },
                        { "hashortag", commitHash },
                        { "component", "documentstore" },
                        { "pid", projectId },
                        // { "commithash", commitHash },
                        { "locationid", gitLocationId }
                    };


                XmlDocument responseXmlCommitInfo = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitcommitinfo", dataToPost, debugRoutine);
                if (XmlContainsError(responseXmlCommitInfo))
                {
                    appLogger.LogError($"{errorMessage}. {responseXmlCommitInfo.OuterXml}");
                    return new TaxxorReturnMessage(false, errorMessage, responseXmlCommitInfo.OuterXml);
                }

                // - Parse the response
                string? encodedXml = responseXmlCommitInfo.SelectSingleNode("/result/payload")?.InnerText;
                var payloadContent = HttpUtility.HtmlDecode(encodedXml);
                var xmlCommitInfo = new XmlDocument();
                try
                {
                    xmlCommitInfo.LoadXml(payloadContent);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "There was a problem loading the data");
                }
                // if (debugRoutine) xmlCommitInfo.Save($"{logRootPathOs}/_versionmanager_commitinfo.xml");

                var logContents = xmlCommitInfo.SelectSingleNode("/tag/message/data/log")?.InnerText ?? "";
                // if (debugRoutine) Console.WriteLine(renderedLog);

                return new TaxxorReturnMessage(true, "Successfully retrieved git commit log contents", logContents, $"projectId: {projectId}, commitHash: {commitHash}");
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, errorMessage, ex.ToString());
            }
        }


        /// <summary>
        /// Renders an SDE sync log that can be used in the Editor UI. Attemps to retrieve the log from the XML sync log file. In case that fails, attempts to retrieve the log from the GIT commit log.
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="commitHash"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RenderSdeSyncLog(RequestVariables reqVars, ProjectVariables projectVars, string commitHash)
        {

            var debugRoutine = siteType == "local" || siteType == "dev";
            var errorMessage = "There was an error retrieving the SDE sync log";
            var renderedFromSyncLog = true;
            var renderedLog = "";
            var gitLocationId = "reportdataroot";
            var errorDataRefs = new List<string>();

            try
            {
                //
                // Retrieve the sync log from the repository in the document store
                //
                var xmlSyncLog = new XmlDocument();
                var restoreFilingContentDataResult = await DocumentStoreService.VersionControl.GitExtractSingleFile(projectVars.projectId, gitLocationId, commitHash, $"version_1/textual/logs/sde-synclog.xml", null, debugRoutine);
                if (!restoreFilingContentDataResult.Success)
                {
                    appLogger.LogError($"{errorMessage}, restoreFilingContentDataResult.Message: {restoreFilingContentDataResult.Message}, restoreFilingContentDataResult.DebugInfo: {restoreFilingContentDataResult.DebugInfo}");
                    renderedFromSyncLog = false;
                }
                else
                {
                    xmlSyncLog.ReplaceContent(restoreFilingContentDataResult.XmlPayload);
                    if (debugRoutine) xmlSyncLog.Save($"{logRootPathOs}/_versionmanager_sde-synclog.xml");

                    //
                    // => Create a new sync log to use in the UI based on the XML log file we just recieved (overcomes the problem of the sync log being too long)
                    //

                    // Create a new SdeSyncStatictics object and fill it with the data from the XML

                    var syncStatsOverall = new SdeSyncStatictics();

                    // Loop through the data references
                    var nodeListDataRefs = xmlSyncLog.SelectNodes("/syncreport/datareference");
                    foreach (XmlNode nodeDataRef in nodeListDataRefs)
                    {
                        var dataRef = nodeDataRef.GetAttribute("dataref");
                        var nodeListSdes = nodeDataRef.SelectNodes("sde");
                        var syncStats = new SdeSyncStatictics();

                        var dataRefContainsError = false;
                        var sdesWithNewValueFound = 0;
                        var updatedSdeValues = 0;
                        var sameSdeValues = 0;
                        var sdeIdsUnique = new List<string>();
                        foreach (XmlNode nodeSde in nodeListSdes)
                        {
                            var factId = nodeSde.GetAttribute("id");
                            var nodeValue = nodeSde.SelectSingleNode("value-new");
                            if (nodeValue == null)
                            {
                                // appLogger.LogWarning($"No value found for fact {factId}");
                                // appLogger.LogWarning(nodeSde.OuterXml);
                                continue;
                            }
                            var status = nodeValue.GetAttribute("status");
                            var value = nodeValue.InnerText;
                            if (value.Trim().Length == 0) value = "no-value";
                            syncStats.AddSdeSyncInfo(factId, status, value);

                            if (!sdeIdsUnique.Contains(factId))
                            {
                                sdeIdsUnique.Add(factId);
                            }

                            if (value != (nodeSde.SelectSingleNode("value-old")?.InnerText ?? ""))
                            {
                                updatedSdeValues++;
                            }
                            else
                            {
                                sameSdeValues++;
                            }

                            if (status != "200-ok" && status != "201-nodatasource") dataRefContainsError = true;

                            sdesWithNewValueFound++;
                        }

                        if (sdesWithNewValueFound > 0)
                        {
                            if (dataRefContainsError && !errorDataRefs.Contains(dataRef)) errorDataRefs.Add(dataRef);

                            syncStats.StructuredDataElementsFound = sdesWithNewValueFound;
                            syncStats.StructuredDataElementsUnique = sdeIdsUnique.Count;
                            syncStats.StructuredDataElementsUpdated = updatedSdeValues;
                            syncStats.StructuredDataElementWithoutUpdate = sameSdeValues;

                            syncStatsOverall.ConsolidateSdeSyncInfo(dataRef, syncStats);
                        }
                        else
                        {
                            // appLogger.LogWarning($"No new SDE values found for data reference {dataRef}");
                        }
                    }

                    renderedLog = _renderStructuredDataSyncResponseMessage(projectVars.projectId, "1", syncStatsOverall, XmlCmsContentMetadata);
                }


                if (!renderedFromSyncLog)
                {
                    //
                    // => Get the commit message/info that contains the SDE sync log as it was stored originally after the SDE sync
                    //
                    appLogger.LogWarning("Unable to retrieve the XML sync log from the document store. Trying to retrieve it from the GIT commit log.");
                    var gitCommitLogContentsResult = await RetrieveGitCommitLogContents(projectVars.projectId, commitHash);
                    if (!gitCommitLogContentsResult.Success)
                    {
                        appLogger.LogError(errorMessage);
                        return gitCommitLogContentsResult;
                    }
                    renderedLog = gitCommitLogContentsResult.Payload;
                }



                //
                // => Adjust the links in the log message so we can directly use it in the UI
                //
                var xmlSyncLogAdjusted = new XmlDocument();
                xmlSyncLogAdjusted.AppendChild(xmlSyncLogAdjusted.CreateElement("log"));
                xmlSyncLogAdjusted.SelectSingleNode("log").InnerXml = renderedLog;

                // Retrieve hierarchy overview
                var xmlHierarchyOverview = await RenderOutputChannelHierarchyOverview(false, true);
                foreach (var dataReference in errorDataRefs)
                {
                    // Find the first item in all the hierarchies that matches the data reference that we are looking for
                    var nodeItem = xmlHierarchyOverview.SelectSingleNode($"/hierarchies/output_channel/items//item[@data-ref='{dataReference}']");

                    if (nodeItem == null)
                    {
                        appLogger.LogError($"Could not locate output channel information for data reference {dataReference}");
                    }
                    else
                    {
                        // Retrieve output channel variant id
                        var nodeOutputChannel = nodeItem.SelectSingleNode("ancestor-or-self::output_channel");
                        var outputChannelVariantId = nodeOutputChannel.GetAttribute("id");

                        // Retrieve the url of the editor
                        var baseUrl = RetrieveUrlFromHierarchy("cms_content-editor", reqVars.xmlHierarchy).Replace("[projectroot]", projectVars.projectRootPath);

                        // Retrieve the site structure item id
                        var itemId = nodeItem.GetAttribute("id");

                        // if (debugRoutine)
                        // {
                        //     Console.WriteLine($"dataReference: {dataReference}, baseUrl: {baseUrl}, itemId: {itemId}, outputChannelVariantId: {outputChannelVariantId}");
                        // }

                        // Replace the links
                        var nodeWrapper = xmlSyncLogAdjusted.SelectSingleNode($"/log//div[@class='tx-sdesyncdetails' and @data-ref='{dataReference}']");
                        if (nodeWrapper != null)
                        {
                            var queryString = $"?pid={projectVars.projectId}&ocvariantid={outputChannelVariantId}&did=foobar#did={itemId}";
                            var nodeSectionLink = nodeWrapper.SelectSingleNode("descendant-or-self::a[contains(@class,'title')]");
                            if (nodeSectionLink != null)
                            {
                                nodeSectionLink.SetAttribute("href", $"{baseUrl}{queryString}");
                                nodeSectionLink.SetAttribute("target", "_blank");
                            }
                            else
                            {
                                appLogger.LogError("Could not locate the section link in the SDE sync log");
                            }

                            var nodeListSdeLinks = nodeWrapper.SelectNodes("descendant-or-self::a[@data-factid]");
                            foreach (XmlNode nodeSdeLink in nodeListSdeLinks)
                            {
                                nodeSdeLink.SetAttribute("href", $"{baseUrl}{queryString}&factid={nodeSdeLink.GetAttribute("data-factid")}");
                            }
                        }
                        else
                        {
                            appLogger.LogError($"Could not locate the wrapper for the data reference '{dataReference}'");
                        }
                    }
                }

                // Log to show in the UI
                var logForUi = xmlSyncLogAdjusted.DocumentElement.InnerXml;

                return new TaxxorReturnMessage(true, "Successfully generated SDE sync log", logForUi, $"projectId: {projectVars.projectId}, commitHash: {commitHash}");

            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
            }
        }


    }
}