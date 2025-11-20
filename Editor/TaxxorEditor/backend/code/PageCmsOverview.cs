using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities used on the CMS Overview Page
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Machine-to-machine logic for rendering a list of projects in JSON, XML or HTML format
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task ListProjects(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Always return XML when requesting the project list by the Admin UI
            if (reqVars.pageId == "listprojects" && request.RetrieveFirstHeaderValueOrDefault<string>("Accept") == "application/xml, text/xml, application/json") reqVars.returnType = ReturnTypeEnum.Xml;

            try
            {
                var xmlProjects = await RenderProjectOverview(projectVars);

                switch (reqVars.returnType)
                {
                    case ReturnTypeEnum.Html:

                        XsltArgumentList xsltArgs = new XsltArgumentList();
                        xsltArgs.AddParam("render_chrome", "", "no");
                        string defaultClient = projectVars.currentUser.RetrieveUserPreferenceKey("default_client") ?? "";
                        if (!string.IsNullOrEmpty(defaultClient)) xsltArgs.AddParam("default_client", "", defaultClient);
                        xsltArgs.AddParam("permissions", "", string.Join(",", [.. projectVars.currentUser.Permissions.Permissions]));
                        xsltArgs.AddParam("uristaticassets", "", projectVars.uriStaticAssets);

                        await response.OK(TransformXml(xmlProjects, "cms_xsl_generate-overview", xsltArgs), reqVars.returnType, true);
                        break;

                    default:
                        await response.OK(TransformXmlToDocument(xmlProjects, "cms_xsl_generate-overview-api"), reqVars.returnType, true);
                        break;
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was an error rendering the project list");
                await response.Error(ex, reqVars.returnType);
            }
        }


        /// <summary>
        /// Generates the overview of projects that a user is allowed to edit
        /// </summary>
        /// <param name="renderChrome">Render just the basic information (ajax requests) or the complete HTML (page loads)</param>
        /// <returns></returns>
        public static async Task<string?> RenderCmsOverviewBody(string renderChrome = "no")
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Retrieve the list of projects
            //
            XmlDocument xmlProjects = await RenderProjectOverview(projectVars, reqVars.isDebugMode);

            //
            // => Render the HTML
            //
            XsltArgumentList xsltArgs = new XsltArgumentList();
            xsltArgs.AddParam("render_chrome", "", renderChrome);
            string? defaultClient = projectVars.currentUser.RetrieveUserPreferenceKey("default_client");
            if (!string.IsNullOrEmpty(defaultClient)) xsltArgs.AddParam("default_client", "", defaultClient);
            xsltArgs.AddParam("permissions", "", string.Join(",", [.. projectVars.currentUser.Permissions.Permissions]));
            xsltArgs.AddParam("uristaticassets", "", projectVars.uriStaticAssets);

            return TransformXml(xmlProjects, "cms_xsl_generate-overview", xsltArgs);
        }


        /// <summary>
        /// Renders the XML document containing the available projects for the current user
        /// </summary>
        /// <param name="debugMode"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RenderProjectOverview(ProjectVariables projectVars, bool debugMode = false)
        {
            //
            // Clone the application configuration xml because we do not want to mess around in the one we are using for the rest of the application
            //
            XmlDocument xmlApplicationConfigurationCloned = new();
            xmlApplicationConfigurationCloned.ReplaceContent(xmlApplicationConfiguration);

            //
            // Prepare application configuration and mark the projects that we have access to using the information we retrieve from the RBAC service
            //
            var xmlNodeListProjects = xmlApplicationConfigurationCloned.SelectNodes("/configuration/cms_projects/cms_project");

            // a) Mark all for no access 
            var reReportingPeriod = new Regex(@"^(.)(.)(..)$");
            var resourceIdList = new List<string>();
            foreach (XmlNode xmlNodeProject in xmlNodeListProjects)
            {
                var currentProjectId = GetAttribute(xmlNodeProject, "id");
                SetAttribute(xmlNodeProject, "access", "", "false");


                // Set a string that we can sort the projects on
                var sortString = xmlNodeProject.GetAttribute("date-publication")?.SubstringBefore("T") ?? "0";
                if (sortString == "0")
                {
                    // Potentially use the reporting period to sort on
                    var nodeReportingPeriod = xmlNodeProject.SelectSingleNode("reporting_period");
                    if (nodeReportingPeriod != null)
                    {
                        // Parse the elements
                        var reportingPeriod = nodeReportingPeriod.InnerText;
                        var match = reReportingPeriod.Match(reportingPeriod);

                        if (match.Success)
                        {
                            double longYear = 1970;
                            double shortYear = 0;
                            if (Double.TryParse(match.Groups[3].Value, out shortYear))
                            {
                                longYear = 2000 + shortYear;
                            }

                            double period = 0;
                            if (reportingPeriod.StartsWith("q"))
                            {
                                if (Double.TryParse(match.Groups[2].Value, out period)) { }
                            }

                            sortString = createIsoTimestamp(_getPeriodEndDate(longYear, period));
                        }
                    }

                }
                xmlNodeProject.SetAttribute("sortstring", sortString);


                // Add the resource ID's that we need to query for in the RBAC service
                resourceIdList.Add($"get__taxxoreditor__cms_project-details__{GetAttribute(xmlNodeProject, "id")}");
            }

            // b) Retrieve the permissions for all the resource ID's that we have compiled above
            XmlDocument xmlPermissions = await AccessControlService.RetrievePermissionsForResources(string.Join(":", resourceIdList));
            // Console.WriteLine("################");
            // Console.WriteLine(PrettyPrintXml(xmlPermissions.OuterXml));
            // Console.WriteLine("################");
            if (XmlContainsError(xmlPermissions))
            {
                appLogger.LogWarning("Could not retrieve permissions for resources. (error: {permissions})", ConvertErrorXml(xmlPermissions));
            }



            // c) Only mark the projects available for this user when there are explicit permissions set
            foreach (XmlNode nodeAccessItem in xmlPermissions.SelectNodes("/items/item[permissions]"))
            {
                var itemId = GetAttribute(nodeAccessItem, "id");
                var currentProjectId = RegExpReplace("^.*__(.*)$", itemId, "$1");

                SetAttribute(nodeAccessItem, "projectid", currentProjectId);

                var xmlNodeProject = xmlApplicationConfigurationCloned.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{currentProjectId}']");
                if (xmlNodeProject == null)
                {
                    appLogger.LogError("Could not find project to mark access rights");
                }
                else
                {
                    SetAttribute(xmlNodeProject, "access", "", "true");
                }
            }
            // Console.WriteLine("################");
            // Console.WriteLine(PrettyPrintXml(xmlPermissions.OuterXml));
            // Console.WriteLine("################");

            // Append the permissions that we have found into the configuration document so that we can use it to determine the access level
            var nodePermissionsImported = xmlApplicationConfigurationCloned.ImportNode(xmlPermissions.DocumentElement, true);
            var nodePermissionsWrapper = xmlApplicationConfigurationCloned.CreateElement("permissions");
            nodePermissionsWrapper.AppendChild(nodePermissionsImported);
            xmlApplicationConfigurationCloned.DocumentElement.AppendChild(nodePermissionsWrapper);

            //
            // Extend the list of Taxxor clients with the projects
            //

            // 1) load the entity structure
            // for now from application configuration - should come from XBRL browser / LEXTRA
            var nodeListClients = xmlApplicationConfigurationCloned.SelectNodes("/configuration/taxxor/clients");
            foreach (XmlNode nodeClients in nodeListClients)
            {
                //1a) mark the entities that this user has access to
                var nodeListLegalEnities = nodeClients.SelectNodes("client/entity_groups//entity");
                foreach (XmlNode nodeLegalEntity in nodeListLegalEnities)
                {
                    var guidLegalEntity = GetAttribute(nodeLegalEntity, "guidLegalEntity");
                    // Console.WriteLine("- guidLegalEntity=" + guidLegalEntity);

                    //2) find the projects that this user has access to and mark these
                    nodeLegalEntity.SetAttribute("access", "true");

                    //inject a projects node which will contain the projects for the current entity
                    var nodeLegalEntityProjects = xmlApplicationConfigurationCloned.CreateNode(XmlNodeType.Element, null, "projects", null);
                    var nodeLegalEntityProjectsInjected = nodeLegalEntity.AppendChild(nodeLegalEntityProjects);

                    //3) find the projects that correspond to the current entity guid
                    var nodeListProjects = xmlApplicationConfigurationCloned.SelectNodes("/configuration/cms_projects/cms_project[system/entities/entity/@guidLegalEntity='" + guidLegalEntity + "']");
                    //var counter = 0;
                    foreach (XmlNode nodeProject in nodeListProjects)
                    {
                        //3a) inject these projects into the entity structure
                        var nodeProjectCloned = nodeProject.CloneNode(true);
                        nodeLegalEntityProjectsInjected.AppendChild(nodeProjectCloned);
                        //counter++;
                        //Response.Write("- counter=" + counter + "<br/>" + Environment.NewLine);

                    }

                }

                // XmlDocument xmlLog = new XmlDocument();
                // xmlLog.LoadXml(nodeClients.OuterXml);
                // await xmlLog.SaveAsync(logRootPathOs + "/documents_overview.xml");
            }

            // Analyze how many reporting types per legal entity are used
            var nodeListEntityProject = xmlApplicationConfigurationCloned.SelectNodes("/configuration/taxxor/clients/client/entity_groups/entity_group/entity/projects");
            foreach (XmlNode nodeEntityProject in nodeListEntityProject)
            {
                var reportTypes = new List<string>();
                var nodeListNestedProjects = nodeEntityProject.SelectNodes("cms_project");
                foreach (XmlNode nodeNestedProject in nodeListNestedProjects)
                {
                    var projectType = nodeNestedProject.GetAttribute("report-type") ?? "";
                    if (projectType != "" && !reportTypes.Contains(projectType)) reportTypes.Add(projectType);
                }

                nodeEntityProject.SetAttribute("reporttypecount", reportTypes.Count.ToString());
            }

            // Potentially filter the list of visible project to one or more legal entities
            var legalEnitityFilter = RetrieveLegalEntityFilter();
            if (legalEnitityFilter.Count > 0)
            {
                // Remove the project that are not part of the filter
                var nodeListProjects = xmlApplicationConfigurationCloned.SelectNodes("/configuration//clients/client/entity_groups/entity_group/entity/projects/cms_project");
                foreach (XmlNode nodeProject in nodeListProjects)
                {
                    var nodeLegalEntity = nodeProject.SelectSingleNode("system/entities/entity");
                    if (nodeLegalEntity != null)
                    {
                        var currentLegalEntityGuid = GetAttribute(nodeLegalEntity, "guidLegalEntity");
                        if (!string.IsNullOrEmpty(currentLegalEntityGuid) && !legalEnitityFilter.Contains(currentLegalEntityGuid))
                        {
                            RemoveXmlNode(nodeProject);
                        }
                    }

                }

                // Remove the entity groups that are not part of the filter
                var nodeListEntityGroups = xmlApplicationConfigurationCloned.SelectNodes("/configuration//clients/client/entity_groups/entity_group");
                foreach (XmlNode nodeEntityGroup in nodeListEntityGroups)
                {
                    var removeGroup = true;
                    var nodeListLegalEnities = nodeEntityGroup.SelectNodes("entity");
                    foreach (XmlNode nodeLegalEntityNested in nodeListLegalEnities)
                    {
                        var currentLegalEntityGuid = GetAttribute(nodeLegalEntityNested, "guidLegalEntity");
                        if (!string.IsNullOrEmpty(currentLegalEntityGuid) && legalEnitityFilter.Contains(currentLegalEntityGuid))
                        {
                            removeGroup = false;
                        }
                    }

                    if (removeGroup) RemoveXmlNode(nodeEntityGroup);

                }
            }


            if (debugMode) await SaveXmlDocument(xmlApplicationConfigurationCloned, logRootPathOs + "/cms_overview.xml");

            return xmlApplicationConfigurationCloned;

            /// <summary>
            /// Retrieves a list of projects to show based on legal entity guids
            /// </summary>
            /// <returns></returns>
            List<string> RetrieveLegalEntityFilter()
            {
                List<string> legalEntitiesToShow = new List<string>();

                string legalEntitiesToShowRaw = projectVars.currentUser.RetrieveUserPreferenceKey("legalentityfilter") ?? "all";

                if (legalEntitiesToShowRaw != "all")
                {
                    if (legalEntitiesToShowRaw.Contains(","))
                    {
                        legalEntitiesToShow.AddRange(legalEntitiesToShowRaw.Split(","));
                    }
                    else
                    {
                        legalEntitiesToShow.Add(legalEntitiesToShowRaw);
                    }
                }

                return legalEntitiesToShow;
            }
        }


        /// <summary>
        /// Lists the GIT repositories in use by this installation and it's status (if it needs to be updated)
        /// </summary>
        public static string RenderGitRepositoryStatusOverview()
        {
            var html = new StringBuilder();

            // Main git repositories
            var nodeGitRepositories = xmlApplicationConfiguration.SelectNodes("/configuration/repositories/*/repro[not(@hidefromui)]");
            foreach (XmlNode nodeGitRepository in nodeGitRepositories)
            {
                var repositoryVersion = RetrieveAttributeValueIfExists("location/@version", nodeGitRepository);
                var repositoryVersionHtml = (string.IsNullOrEmpty(repositoryVersion)) ? "" : $" ({repositoryVersion})";
                html.AppendLine("<li class=\"status-" + GetAttribute(nodeGitRepository, "id") + "\"><i class=\"ace-icon fa fa-check green\"></i>" + RetrieveNodeValueIfExists("name", nodeGitRepository) + repositoryVersionHtml + "</li>");
            }

            // Editors
            var nodeGitRepositoriesEditors = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor");
            foreach (XmlNode nodeGitRepositoryEditor in nodeGitRepositoriesEditors)
            {
                var repositoryVersion = RetrieveAttributeValueIfExists("path/@version", nodeGitRepositoryEditor);
                var repositoryVersionHtml = (string.IsNullOrEmpty(repositoryVersion)) ? "" : $" ({repositoryVersion})";
                html.AppendLine("<li class=\"status-" + GetAttribute(nodeGitRepositoryEditor, "id") + "\"><i class=\"ace-icon fa fa-check green\"></i>" + RetrieveNodeValueIfExists("name", nodeGitRepositoryEditor) + repositoryVersionHtml + "</li>");
            }

            return html.ToString();
        }

        /// <summary>
        /// Renders the GIT repositories overview directly to the client
        /// </summary>
        /// <returns>The git repository status overview.</returns>
        public async static Task WriteGitRepositoryStatusOverview()
        {
            var context = System.Web.Context.Current;

            await context.Response.WriteAsync(RenderGitRepositoryStatusOverview());
        }
    }
}