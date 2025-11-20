using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to work with the auditor view
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Creates an auditor message
        /// </summary>
        /// <returns>The auditor view message.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task StoreAuditorViewMessage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);


            if (projectVars.currentUser.IsAuthenticated)
            {

                // Retrieve posted values
                var projectId = context.Request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);
                var message = context.Request.RetrievePostedValue("message", RegexEnum.None, true, ReturnTypeEnum.Xml);
                var auditorAreaId = context.Request.RetrievePostedValue("auditorarea", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Xml);
                var auditorAreaEnum = AuditorAreaEnumFromString(auditorAreaId);

                if (auditorAreaEnum != AuditorAreaEnum.Unknown)
                {
                    // Create the auditor message
                    TaxxorReturnMessage auditorMessageCreateResult = await CreateAuditorMessage(auditorAreaEnum, message, projectId);

                    // Render the result to return to the client
                    if (auditorMessageCreateResult.Success)
                    {
                        await context.Response.OK(GenerateSuccessXml(auditorMessageCreateResult.Message, auditorMessageCreateResult.DebugInfo), reqVars.returnType, true);
                    }
                    else
                    {
                        HandleError(reqVars.returnType, auditorMessageCreateResult.Message, $"auditorMessageCreateResult.DebugInfo: {auditorMessageCreateResult.DebugInfo}");
                    }
                }
                else
                {
                    HandleError(reqVars.returnType, "Unknown scope for the auditor message", $"stack-trace: {CreateStackInfo()}");
                }


            }
            else
            {
                HandleError(reqVars.returnType, "Not authenticated", $"stack-trace: {CreateStackInfo()}", 403);
            }
        }

        /// <summary>
        /// Retrieves the auditor overview data and returns that in a standard XML success structure to the client.
        /// </summary>
        /// <returns>The auditor view.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task RetrieveAuditorOverviewData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted data
            var filterLatestString = request.RetrievePostedValue("filterlatest", "(true|false)", false, reqVars.returnType, "false");
            var filterLatest = (filterLatestString == "true");
            int numberOfCommitsToRequest = (filterLatest) ? 200 : -1;


            var debugRoutine = (siteType == "local" || siteType == "dev");
            var debugContent = new StringBuilder();
            XmlDocument xmlAuditorView = new XmlDocument();

            // 1) Retrieve the commits from the data GIT repository and load that into an XmlDocument object
            var sbCommitContent = new StringBuilder();
            var gitRootPathOs = RetrieveGitRepositoryLocation("project-data");
            //@"<commit-meta hash=""%H"">""%s""</commit-meta>"
            sbCommitContent.AppendLine(GitRetrieveCommitMessages(gitRootPathOs, "<", @"""<commit-meta repro='project-data' hash='%H'>%s</commit-meta>""", numberOfCommitsToRequest));

            // 2) Retrieve commits from the content GIT repository and add those to the list
            gitRootPathOs = RetrieveGitRepositoryLocation("project-content");
            sbCommitContent.AppendLine(GitRetrieveCommitMessages(RetrieveGitRepositoryLocation("project-content"), "<", @"""<commit-meta repro='project-content' hash='%H'>%s</commit-meta>""", numberOfCommitsToRequest));
            if (debugRoutine)
            {
                DebugVariable(() => gitRootPathOs);
                debugContent.AppendLine("<h2>Filing Data and Filing Content Commits</h2><pre>" + Environment.NewLine + HtmlEncodeForDisplay(sbCommitContent.ToString()) + Environment.NewLine + "</pre>");
            }

            // 3) Load all the commit data into an XmlDocument so that we can process it
            var auditorViewXmlContent = "<commits>" + Environment.NewLine + sbCommitContent.ToString() + Environment.NewLine + "</commits>";
            try
            {
                xmlAuditorView.LoadXml(auditorViewXmlContent);
                // if (siteType == "local" || siteType == "dev") await xmlAuditorView.SaveAsync($"{logRootPathOs}/auditorviewcontent.xml", false, true);
            }
            catch (Exception ex)
            {
                HandleError(reqVars.returnType, "Could not retrieve auditor view information", $"Unable to load the xml content retrieved from the git repository in a XmlDocument object. auditorViewXmlContent: {auditorViewXmlContent}, error: {ex}, stack-trace: {GetStackTrace()}");
            }

            //
            // => Fill linkname for items that are related to SDE sync messages
            //
            var nodeListCommitMessages = xmlAuditorView.SelectNodes("/commits/commit-meta[@repro='project-data']/commit/message[crud='structureddatasync' and linkname='none']");
            // Console.WriteLine("##############");
            // Console.WriteLine($"Found {nodeListCommitMessages.Count} nodes to fill the linkname for");
            // Console.WriteLine("##############");
            if (nodeListCommitMessages.Count > 0)
            {
                // var watch = System.Diagnostics.Stopwatch.StartNew();
                var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectVars.projectId);

                foreach (XmlNode nodeCommitMessage in nodeListCommitMessages)
                {
                    var siteStructureId = nodeCommitMessage.SelectSingleNode("id")?.InnerText ?? "";
                    if (!siteStructureId.Contains(".xml"))
                    {
                        if (siteStructureId != "")
                        {
                            var linkName = xmlHierarchyOverview.SelectSingleNode($"/hierarchies/output_channel/items/structured//item[@id='{siteStructureId}']/web_page/linkname")?.InnerText ?? "";
                            if (linkName != "") nodeCommitMessage.SelectSingleNode("linkname").InnerText = linkName;
                            // Console.WriteLine($"* siteStructureId: {siteStructureId}, linkName: {linkName}");
                        }
                    }
                    else
                    {
                        var nodeCrud = nodeCommitMessage.SelectSingleNode("crud");
                        string[] filesInvolved = siteStructureId.Split(',');
                        var numberOfFilesInvolved = filesInvolved.Length;
                        nodeCrud.SetAttribute("filecount", numberOfFilesInvolved.ToString());
                        var numberOfItems = XmlCmsContentMetadata.SelectNodes($"/projects/cms_project[@id='{projectVars.projectId}']/data/content[@datatype='sectiondata']").Count;
                        // Console.WriteLine($"* numberOfFilesInvolved: {numberOfFilesInvolved}, numberOfItems: {numberOfItems}, factor: {Math.Round((double)numberOfFilesInvolved / numberOfItems, 2)}");
                        if (Math.Round((double)numberOfFilesInvolved / numberOfItems, 2) > 0.75)
                        {
                            nodeCrud.SetAttribute("type", "full");
                        }
                        else
                        {
                            nodeCrud.SetAttribute("type", "section");
                        }
                    }
                }

                // watch.Stop();
                // Console.WriteLine($"!! Adding sync information took: {watch.ElapsedMilliseconds.ToString()} ms");
            }


            await context.Response.OK(GenerateSuccessXml(HttpUtility.HtmlEncode(xmlAuditorView.OuterXml), debugContent.ToString()), reqVars.returnType, true);
        }
    }
}