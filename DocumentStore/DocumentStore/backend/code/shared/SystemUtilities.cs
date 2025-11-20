using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Generic utilities for making the application work
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Generates an array of route templates that can be used in the routers that mix active C# content with static files served by the static webserver
        /// </summary>
        /// <returns>The route template paths.</returns>
        /// <param name="baseFolder">Base folder.</param>
        /// <param name="routeSubFolderDepth">Route sub folder depth.</param>
        public string[] CreateRouteTemplatePaths(string baseFolder, int routeSubFolderDepth = 7)
        {
            string routeSubPath = "";
            string routeConstraint = "length(2,100)";
            string filenameRouteTemplate = @"{filename}.";
            string[] dynamicPathExtensions = { "html", "aspx" };

            // Build the list containing the route templates
            List<string> routeTemplates = new List<string>();
            for (int depth = 0; depth < routeSubFolderDepth; depth++)
            {
                string? routeFolderPath = RegExpReplace(@"^(.*):(.*)(.)$", routeSubPath, "$1:alpha$3");
                routeTemplates.Add($"{baseFolder}{routeFolderPath}");
                foreach (string dynamicExtension in dynamicPathExtensions)
                {
                    routeTemplates.Add(baseFolder + routeSubPath + "/" + filenameRouteTemplate + dynamicExtension);
                }

                // Append the next directory to the string

                // Somehow, the below does not work
                // routeSubPath += "/{folder" + depth.ToString() + @":regex(^..[[\\w\\-\\d]]{{1,300}}$)}";

                // So we have to restrict folder names to /^[a-zA-Z]+$/
                routeSubPath += "/{folder" + depth.ToString() + @":" + routeConstraint + @"}";
            }

            return routeTemplates.ToArray();
        }

        /// <summary>
        /// Adds/stores the RequestVariables object to the HttpContext
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="projectVars">Request variables.</param>
        public static void SetProjectVariables(HttpContext context, ProjectVariables projectVars)
        {
            foreach (var pair in context?.Items)
            {
                if ((string)pair.Key == keyProjectVariables)
                {
                    context.Items.Remove(keyProjectVariables);
                    break;
                }
            }

            context?.Items?.Add(keyProjectVariables, projectVars);
        }

        /// <summary>
        /// Test if the current page/rout is an internal service page
        /// </summary>
        /// <returns><c>true</c>, if internal service page was ised, <c>false</c> otherwise.</returns>
        /// <param name="requestVars">Request variables.</param>
        public static bool IsInternalServicePage(RequestVariables requestVars)
        {
            return requestVars.xmlHierarchy.SelectNodes($"/items/structured/item/sub_items/item[@id='apiroot']/sub_items/item[@id='internalservicetools']/sub_items/item[@id='{requestVars.pageId}']").Count > 0;
        }

        /// <summary>
        /// Throws a standard error when a route is requested with a method that the server is not configured to handle
        /// </summary>
        /// <param name="reqVars">Req variables.</param>
        private static void _handleMethodNotSupported(RequestVariables reqVars)
        {
            HandleError(reqVars, "Unsupported method", $"HTTP method '{reqVars.method}' is not supported for {reqVars.pageId}, stack-trace: {GetStackTrace()}");
        }


        /// <summary>
        /// Generates an ISO compliant timestamp containing offset
        /// </summary>
        /// <param name="shortFormat">If set to true, then a date will be returned in yyyy-MM-dd format</param>
        /// <returns></returns>
        public static string createIsoTimestamp(bool shortFormat = false)
        {
            DateTime localTime = DateTime.Now;
            return createIsoTimestamp(localTime, shortFormat);
        }

        /// <summary>
        /// Generates an ISO compliant timestamp containing offset
        /// </summary>
        /// <param name="date"></param>
        /// <param name="shortFormat"></param>
        /// <returns></returns>
        public static string? createIsoTimestamp(string date, bool shortFormat = false)
        {
            try
            {
                var parsedDate = DateTime.Parse(date);
                return createIsoTimestamp(parsedDate, shortFormat);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Could not convert {date} to a datetime object");
                return null;
            }
        }


        /// <summary>
        /// Generates an ISO compliant timestamp containing offset
        /// </summary>
        /// <param name="date"></param>
        /// <param name="shortFormat">If set to true, then a date will be returned in yyyy-MM-dd format</param>
        /// <returns></returns>
        public static string createIsoTimestamp(DateTime date, bool shortFormat = false)
        {
            if (shortFormat)
            {
                return date.ToString("yyyy-MM-dd");
            }
            else
            {
                DateTimeOffset localTimeAndOffset = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
                return localTimeAndOffset.ToString("o");
            }
        }

        /// <summary>
        /// Decorates a filename with a timestamp
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="shortFormat"></param>
        /// <param name="stripMilliSeconds"></param>
        /// <param name="devider"></param>
        /// <param name="minutesOffset"></param>
        /// <returns></returns>
        public static string createIsoFilenameTimestamp(string filename, bool shortFormat = false, bool stripMilliSeconds = true, string devider = "_", double minutesOffset = 0)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev" || siteType == "prev");
            var useAdjustedTime = (minutesOffset > 0 || minutesOffset < 0);
            DateTime localTime = DateTime.Now;
            DateTime adjustedTime = new DateTime();
            if (useAdjustedTime)
            {
                adjustedTime = localTime.AddMinutes(minutesOffset);
                if (debugRoutine)
                {
                    Console.WriteLine($"-------- createIsoFilenameTimestamp() --------");
                    Console.WriteLine($"- minutesOffset: {minutesOffset}");
                    Console.WriteLine($"- localTime: {localTime.ToString("o")}");
                    Console.WriteLine($"- adjustedTime: {adjustedTime.ToString("o")}");
                    Console.WriteLine("----------------------------------------------------");
                }

            }
            var postFix = createIsoTimestamp(((useAdjustedTime) ? adjustedTime : localTime), shortFormat).Replace("-", "").Replace("T", "-").Replace(":", "").Replace(".", "-")[0..^5];
            if (!shortFormat && stripMilliSeconds) postFix = RegExpReplace(@"^(.*)(\-\d+)$", postFix, "$1");
            return Path.GetFileNameWithoutExtension(filename) + devider + postFix + Path.GetExtension(filename);
        }

        /// <summary>
        /// Calculates the time offset between the client and the server
        /// </summary>
        /// <param name="clientOffsetMinutes"></param>
        /// <returns></returns>
        public static double CalculateClientServerOffsetInMinutes(string clientOffsetMinutes)
        {
            double clientTimeOffsetMinutes = 0;
            if (!double.TryParse(clientOffsetMinutes, out clientTimeOffsetMinutes))
            {
                appLogger.LogWarning($"Could not parse client time offset minutes: {clientOffsetMinutes}");
            }

            // Calculate the time difference between client and server
            return CalculateClientServerOffsetInMinutes(clientTimeOffsetMinutes);
        }

        /// <summary>
        /// Calculates the time offset between the client and the server
        /// </summary>
        /// <param name="clientTimeOffsetMinutes"></param>
        /// <returns></returns>
        public static double CalculateClientServerOffsetInMinutes(double clientTimeOffsetMinutes)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev" || siteType == "prev");

            if (debugRoutine)
            {
                Console.WriteLine($"-------- CalculateClientServerOffsetInMinutes() --------");
                Console.WriteLine($"- clientTimeOffsetMinutes: {clientTimeOffsetMinutes}");
                Console.WriteLine($"- serverTimeOffsetMinutes: {TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes}");
                Console.WriteLine("---------------------------------------------------------");
            }

            // Invert it because the JS offset is from the UTC perspective
            clientTimeOffsetMinutes = -clientTimeOffsetMinutes;


            // Calculate the time difference between client and server
            return clientTimeOffsetMinutes - TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
        }

        /// <summary>
        /// Tests if current path is a path to an API route
        /// </summary>
        /// <returns></returns>
        public static bool isApiRoute()
        {
            return isApiRoute(System.Web.Context.Current);
        }

        public static bool isApiRoute(HttpContext context)
        {
            RequestVariables reqVars = RetrieveRequestVariables(context);
            if (reqVars.currentHierarchyNode == null) return false;
            return (reqVars.currentHierarchyNode.SelectNodes("ancestor::item[@id='apiroot']").Count == 1);
        }

        /// <summary>
        /// Generate a unique resource ID to use in the RBAC service
        /// </summary>
        /// <param name="httpMethod"></param>
        /// <param name="hierarchyItemId"></param>
        /// <param name="projectId"></param>
        /// <param name="hierarchicalLevel"></param>
        /// <param name="isApiRoute"></param>
        /// <returns></returns>
        public static string CalculateRbacResourceId(string httpMethod, string hierarchyItemId, string projectId, int hierarchicalLevel, bool isApiRoute = false)
        {

            string ancestorResourceId = "";
            if (string.IsNullOrEmpty(projectId) || hierarchicalLevel == 1 || isApiRoute)
            {
                ancestorResourceId = $"{httpMethod.ToLower()}__{applicationId}__{hierarchyItemId}";
            }
            else
            {
                if (string.IsNullOrEmpty(projectId))
                {
                    ancestorResourceId = $"{httpMethod.ToLower()}__{applicationId}__{hierarchyItemId}";
                }
                else
                {
                    ancestorResourceId = $"{httpMethod.ToLower()}__{applicationId}__{hierarchyItemId}__{projectId}";
                }

            }

            return ancestorResourceId;

        }

        /// <summary>
        /// Generates a breadcrumbtrail (using a comma delimited string) used to uniquely identify a hierarchical element for the RBAC service
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectIdToUse"></param>
        /// <param name="nodeItem"></param>
        /// <param name="disableApiRouteCheck"></param>
        /// <returns></returns>
        public static string GenerateRbacBreadcrumbTrail(RequestMethodEnum requestMethod, string projectIdToUse, XmlNode nodeItem, bool disableApiRouteCheck = false)
        {

            var debugRoutine = (siteType == "local" || siteType == "dev");

            bool apiRoute = (disableApiRouteCheck) ? false : isApiRoute();

            // Retrieve the list of ancestors
            var ancestorHierarchyItems = nodeItem.SelectNodes("ancestor-or-self::item");

            // TODO: Should we make the resource ID's also unique for output channel (variants) as well??

            // Create a "breadcrumb" array of id's describing the hierarchical position of the current element
            // get__taxxoreditor__cms-overview
            // <<http-method>>__<<application-id>>__<<route-id>>
            // <<http-method>>__<<application-id>>__<<route-id>>__<<project-id>>
            var ancestorHierarchicalLevel = 0;
            List<string> resourceBreadcrumbIds = new List<string>();
            foreach (XmlNode hierarchyNode in ancestorHierarchyItems)
            {
                ancestorHierarchicalLevel++;
                var itemId = GetAttribute(hierarchyNode, "id");

                var ancestorResourceId = CalculateRbacResourceId(RequestMethodEnumToString(requestMethod), itemId, projectIdToUse, ancestorHierarchicalLevel, apiRoute);

                resourceBreadcrumbIds.Add(ancestorResourceId);
            }
            resourceBreadcrumbIds.Reverse();
            resourceBreadcrumbIds.Add("root");

            if (debugRoutine)
            {
                // appLogger.LogDebug($"- rbac breadcrumb: {string.Join(",", resourceBreadcrumbIds)}");
            }

            return string.Join(",", resourceBreadcrumbIds);
        }

        /// <summary>
        /// Utility function to determine if we are running within a docker environment or not
        /// </summary>
        /// <returns></returns>
        public static bool IsRunningInDocker()
        {
            return Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        }

        /// <summary>
        /// Retrieves the OS path to the shared folder that we can use to exchange files between different Taxxor components
        /// </summary>
        /// <param name="forceDocker"></param>
        /// <returns></returns>
        public static string? RetrieveSharedFolderPathOs(bool forceDocker = false)
        {
            if (forceDocker || IsRunningInDocker())
            {
                // This is the default path that we map in every docker container in the Taxxor Eco System
                return "/mnt/shared";
            }
            else
            {
                if (siteType == "local")
                {
                    if (string.IsNullOrEmpty(LocalSharedFolderPathOs))
                    {
                        string[] potentialSharedPaths = {
                            $"{Path.GetDirectoryName(Path.GetDirectoryName(sitesRootPathOs))}/data/{TaxxorClientId}/_shared",
                            $"{Path.GetDirectoryName(sitesRootPathOs)}/data/{TaxxorClientId}/_shared",
                            $"{sitesRootPathOs}/_dockerfiles/persistent/shared"
                        };
                        foreach (var sharedPathOs in potentialSharedPaths)
                        {
                            if (Directory.Exists(sharedPathOs))
                            {
                                LocalSharedFolderPathOs = sharedPathOs;
                                return sharedPathOs;
                            }
                        }
                        return null;
                    }
                    else
                    {
                        return LocalSharedFolderPathOs;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Fills the current project variables object from the context with base information
        /// </summary>
        /// <param name="newProjectId"></param>
        /// <param name="retrieveMeta"></param>
        /// <returns></returns>
        public static async Task<bool> ProjectVariablesFromProjectId(string newProjectId, bool retrieveMeta = false, bool forceStoreInCache = false)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Fill the projectVars variable with information about the project we have just created
            projectVars.projectId = newProjectId;
            projectVars = await FillProjectVariablesFromProjectId(projectVars);

            // Store the updated project variables in the context
            SetProjectVariables(context, projectVars);

            // Set the correct RBAC cache
            projectVars.rbacCache = new RbacCache(projectVars.currentUser.Id, projectVars.projectId);

            // Store the updated project variables in the context
            SetProjectVariables(context, projectVars);

            // Retrieve the the metadata that for the new project
            if (retrieveMeta)
            {
                var hierarchyRetrieveResult = await RetrieveOutputChannelHierarchiesMetaData(projectVars, reqVars, false, forceStoreInCache);
                if (!hierarchyRetrieveResult) return false;
            }

            return true;
        }

        /// <summary>
        /// Renders a project variables object based on "key/value" type of XML posted via a SignalR type of request
        /// </summary>
        /// <param name="xmlDataPosted"></param>
        /// <param name="retrieveMeta"></param>
        /// <returns></returns>
        public static async Task<bool> ProjectVariablesFromXml(XmlDocument xmlDataPosted, bool retrieveMeta = false)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            /*
<data>
  <pid>q320</pid>
  <did>front-cover</did>
  <ocvariantid>qrpdf</ocvariantid>
  <pageid>front-cover</pageid>
  <token>9b56b7bb98b42e7f3673ad1fd66cf0efc1025ae9</token>
  <vid>1</vid>
  <ctype>regular</ctype>
  <rtype>philips-quarterly-report</rtype>
  <octype>pdf</octype>
  <oclang>en</oclang>
</data>

            */

            // Fill the projectVars variable with information about the project we have just created
            projectVars.projectId = xmlDataPosted.SelectSingleNode("/data/pid")?.InnerText ?? "";
            projectVars = await FillProjectVariablesFromProjectId(projectVars);

            // Override the values with ones that came from the XML document
            string? valueFound = null;
            valueFound = RetrieveAndCheckXmlData("/data/ocvariantid");
            if (valueFound != null) projectVars.outputChannelVariantId = valueFound;

            valueFound = RetrieveAndCheckXmlData("/data/token");
            if (valueFound != null) projectVars.token = valueFound;

            valueFound = RetrieveAndCheckXmlData("/data/octype");
            if (valueFound != null) projectVars.outputChannelType = valueFound;

            valueFound = RetrieveAndCheckXmlData("/data/oclang");
            if (valueFound != null) projectVars.outputChannelVariantLanguage = valueFound;

            valueFound = RetrieveAndCheckXmlData("/data/did");
            if (valueFound != null) projectVars.did = valueFound;

            valueFound = RetrieveAndCheckXmlData("/data/pageid");
            if (valueFound != null) projectVars.did = valueFound;

            // Store the updated project variables in the context
            SetProjectVariables(context, projectVars);

            // Set the correct RBAC cache
            projectVars.rbacCache = new RbacCache(projectVars.currentUser.Id, projectVars.projectId);

            // Store the updated project variables in the context
            SetProjectVariables(context, projectVars);

            // Retrieve the the metadata that for the new project
            if (retrieveMeta)
            {
                var hierarchyRetrieveResult = await RetrieveOutputChannelHierarchiesMetaData(projectVars, reqVars, true);
                if (!hierarchyRetrieveResult) return false;
            }

            return true;

            /// <summary>
            /// Helper function to retrieve and validate data from XML
            /// </summary>
            /// <param name="xPath"></param>
            /// <returns></returns>
            string? RetrieveAndCheckXmlData(string xPath)
            {
                string? valueFound = null;
                var nodeInfo = xmlDataPosted.SelectSingleNode(xPath);
                if (nodeInfo != null)
                {
                    var nodeValue = nodeInfo.InnerText;
                    if (RegExpTest(RegexEnum.Default.Value, nodeValue)) return nodeValue;
                }

                return valueFound;
            }

        }


        /// <summary>
        /// Fills the passed ProjectVariables object with a guestimate of variables based on the projectid field and optionally renders the metadata content as well
        /// </summary>
        /// <param name="projectVarsToFill"></param>
        /// <param name="retrieveMeta"></param>
        /// <returns></returns>
        public static async Task<ProjectVariables> FillProjectVariablesFromProjectId(ProjectVariables projectVarsToFill, bool retrieveMeta = false)
        {
            if (string.IsNullOrEmpty(projectVarsToFill.projectId))
            {
                HandleError("Variable set not complete", $"Could not calculate project variables object, because the projectid field was not supplied. stack-trace: {GetStackTrace()}");
            }

            projectVarsToFill.editorId = RetrieveEditorIdFromProjectId(projectVarsToFill.projectId);
            projectVarsToFill.reportTypeId = RetrieveReportTypeIdFromProjectId(projectVarsToFill.projectId);
            projectVarsToFill.isMiddlewareCreated = false;
            var xPathEditorConfig = $"/configuration/editors/editor[@id='{projectVarsToFill.editorId}']/path";
            var nodeEditorPath = xmlApplicationConfiguration.SelectSingleNode(xPathEditorConfig);
            if (nodeEditorPath == null)
            {
                appLogger.LogWarning($"Could not find the editor definition. projectId: {projectVarsToFill.projectId}, editorId: {projectVarsToFill.editorId}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                var pathType = nodeEditorPath.GetAttribute("path-type");
                switch (pathType)
                {
                    case "cmsroot":
                        var cmsRootPath = RetrieveNodeValueIfExists("/configuration/general/locations/location[@id='cmsroot']", xmlApplicationConfiguration);
                        projectVarsToFill.projectRootPath = cmsRootPath + nodeEditorPath.InnerText;
                        break;
                    default:
                        projectVarsToFill.projectRootPath = nodeEditorPath.InnerText;
                        break;
                }

                projectVarsToFill.projectRootPathOs = websiteRootPathOs + projectVarsToFill.projectRootPath;
            }

            projectVarsToFill.outputChannelVariantId = RetrieveFirstOutputChannelVariantIdFromEditorId(projectVarsToFill.editorId);
            projectVarsToFill.outputChannelDefaultLanguage = RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVarsToFill.editorId)}]/output_channels[1]/output_channel[1]/variants[1]/variant[1]/@lang", Taxxor.Project.ProjectLogic.xmlApplicationConfiguration);
            projectVarsToFill.outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVarsToFill.projectId, projectVarsToFill.outputChannelVariantId);
            projectVarsToFill.outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectVarsToFill.projectId, projectVarsToFill.outputChannelVariantId);
            projectVarsToFill.versionId = "latest";

            FillCorePathsInProjectVariables(ref projectVarsToFill);

            // Retrieve the the metadata that for the new project
            if (retrieveMeta)
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                var hierarchyRetrieveResult = await RetrieveOutputChannelHierarchiesMetaData(projectVarsToFill, reqVars);
                if (!hierarchyRetrieveResult)
                {
                    appLogger.LogError($"Unable to retrieve hierarchy information - stack-trace: {GetStackTrace()}");
                }
            }

            return projectVarsToFill;
        }

        /// <summary>
        /// Retrieves the ID of the editor based on the Project ID
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static string? RetrieveEditorIdFromProjectId(string projectId)
        {
            var reportId = RetrieveReportTypeIdFromProjectId(projectId);
            if (string.IsNullOrEmpty(reportId)) return null;
            return RetrieveEditorIdFromReportId(reportId);
        }

        /// <summary>
        /// Retrieves the report type ID from the project ID
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static string? RetrieveReportTypeIdFromProjectId(string projectId)
        {
            var nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectId)}]");
            if (nodeProject == null) return null;
            var reportId = GetAttribute(nodeProject, "report-type");
            if (string.IsNullOrEmpty(reportId)) return null;
            return reportId;
        }

        /// <summary>
        /// Retrieves the ID of the editor based on the Report Type ID
        /// </summary>
        /// <param name="reportId"></param>
        /// <returns></returns>
        public static string? RetrieveEditorIdFromReportId(string reportId)
        {
            return RetrieveAttributeValueIfExists($"/configuration/report_types/report_type[@id={GenerateEscapedXPathString(reportId)}]/@editorId", xmlApplicationConfiguration);
        }

        /// <summary>
        /// Retrieves the output channel variant ID that is the first one defined for a specific type (pdf|web) in the configuration
        /// </summary>
        /// <param name="editorId"></param>
        /// <param name="outputChannelType"></param>
        /// <returns></returns>
        public static string? RetrieveFirstOutputChannelVariantIdFromEditorId(string editorId, string outputChannelType = "pdf", string? outputChannelVariantLanguage = null)
        {
            var outputChannelVariantSelector = (string.IsNullOrEmpty(outputChannelVariantLanguage) ? "1" : $"@lang='{outputChannelVariantLanguage}'");
            return RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id={GenerateEscapedXPathString(editorId)}]/output_channels/output_channel[@type={GenerateEscapedXPathString(outputChannelType)}]/variants/variant[{outputChannelVariantSelector}]/@id", xmlApplicationConfiguration);
        }

        /// <summary>
        /// Retrieves the output channel language from the project id and output channel variant id
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="outputChannelVariantId"></param>
        /// <returns></returns>
        public static string? RetrieveOutputChannelLanguageFromOutputChannelVariantId(string projectId, string outputChannelVariantId)
        {
            var editorId = RetrieveEditorIdFromProjectId(projectId);
            if (string.IsNullOrEmpty(editorId)) return null;

            return RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id={GenerateEscapedXPathString(editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(outputChannelVariantId)}]/@lang", xmlApplicationConfiguration);
        }

        /// <summary>
        /// Retrieves the output channel name from the outputchannel variant id
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="outputChannelVariantId"></param>
        /// <returns></returns>
        public static string? RetrieveOutputChannelNameFromOutputChannelVariantId(string projectId, string outputChannelVariantId)
        {
            var editorId = RetrieveEditorIdFromProjectId(projectId);
            if (string.IsNullOrEmpty(editorId)) return null;

            return xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(outputChannelVariantId)}]/name")?.InnerText;
        }

        /// <summary>
        /// Retrieves the output channel type from the outputchannel variant id
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="outputChannelVariantId"></param>
        /// <returns></returns>
        public static string? RetrieveOutputChannelTypeFromOutputChannelVariantId(string projectId, string outputChannelVariantId)
        {
            var editorId = RetrieveEditorIdFromProjectId(projectId);
            if (string.IsNullOrEmpty(editorId)) return null;

            return RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id={GenerateEscapedXPathString(editorId)}]/output_channels/output_channel[variants/variant/@id={GenerateEscapedXPathString(outputChannelVariantId)}]/@type", xmlApplicationConfiguration);
        }

        /// <summary>
        /// Utility that returns a URL that we can use for logging
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static string GenerateLogUri(Uri uri)
        {
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath}";
        }




    }


}