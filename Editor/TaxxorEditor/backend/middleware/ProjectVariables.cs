using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseProjectVariablesMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ProjectVariablesMiddleware>();
    }
}

public class ProjectVariablesMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;

    public ProjectVariablesMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Rerieve the request variables
        var reqVars = RetrieveRequestVariables(context);

        // if (isDevelopmentEnvironment || reqVars.isDebugMode) appLogger.LogInformation("** In ProjectVariables Middleware **");

        // Only retrieve additional information if we are dealing with an asset that needs to be calculated
        if (!reqVars.isStaticAsset)
        {
            XmlNode? xmlNode;
            XmlNode? nodeProject = null;


            // Create a project variables instance
            var projectVars = new ProjectVariables(reqVars.pageId);

            // Fill the project variables object
            projectVars.isMiddlewareCreated = true;

            // Are we dealing with a page that is only used for internal (Taxxor system) information exchange
            projectVars.isInternalServicePage = IsInternalServicePage(reqVars);

            // Tenant specific settings
            var tenantIdFromHeader = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-Tenant-Id");
            if (!string.IsNullOrEmpty(tenantIdFromHeader)) projectVars.tenantId = tenantIdFromHeader;
            if (!TenantSpecificSettings.ContainsKey(projectVars.tenantId)) TenantSpecificSettings[projectVars.tenantId] = new TenantSettings();

            // Attempt to retrieve an alternative user ID from the HTTP header
            projectVars.userIdFromHeader = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserId");
            if (!string.IsNullOrEmpty(projectVars.userIdFromHeader)) projectVars.userIdFromHeader = projectVars.userIdFromHeader.ToLower();

            // Special debug statement for PDF Genarator content
            if (reqVars.pageId == "xyz")
            {
                //
                // Dump the incoming request for inspection
                //
                appLogger.LogInformation("-------- PDF Generator Incoming Request Information --------");
                Console.WriteLine(await DebugIncomingRequest(context.Request));
                Console.WriteLine("");
            }

            if (RefreshLocalAddressAndIp)
            {
                // Calculate the addresses that we can use to reach this application
                LocalWebAddressIp = CalculateFullPathOs("ownaddress-ip");
                // if (reqVars.isDebugMode) AddDebugVariable("localWebAddressIp", LocalWebAddressIp);
                LocalWebAddressDomain = CalculateFullPathOs("ownaddress-domain");
                // if (reqVars.isDebugMode) AddDebugVariable("localWebAddressDomain", LocalWebAddressDomain);
                RefreshLocalAddressAndIp = false;
            }

            // Retrieve project root path variable (default value)
            var xmlNodeListReportTypeLocations = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor/path");
            for (var i = 0; i < xmlNodeListReportTypeLocations.Count; i++)
            {
                string basePath;
                XmlNode? xmlNodeReportTypeLocation = xmlNodeListReportTypeLocations.Item(i);
                if (xmlNodeReportTypeLocation.Attributes["path-type"].Value == "cmsroot")
                {
                    basePath = CmsRootPath + xmlNodeReportTypeLocation.InnerText;
                }
                else
                {
                    basePath = xmlNodeReportTypeLocation.InnerText;
                }

                if (reqVars.thisUrlPath.IndexOf(basePath, StringComparison.CurrentCulture) > -1)
                {
                    projectVars.projectRootPath = basePath;
                    projectVars.projectRootPathOs = websiteRootPathOs + projectVars.projectRootPath;
                    projectVars.reportTypeId = xmlApplicationConfiguration.SelectSingleNode("/configuration/report_types/report_type")?.GetAttribute("id") ?? "";
                    projectVars.projectId = null;
                    break;
                }
            }


            // Project id passed as a querystring variable
            if (projectVars.projectId == null)
            {
                // Use JSON data if this was posted
                if (reqVars.returnType == ReturnTypeEnum.Json && reqVars.method == RequestMethodEnum.Post && reqVars.xmlJsonData != null)
                {
                    // => Extract the Project ID, etc from the JSON data
                    var nodeProjectId = reqVars.xmlJsonData.SelectSingleNode("//pid");
                    if (nodeProjectId != null) projectVars.projectId = nodeProjectId.InnerText;
                }

                if (projectVars.projectId == null)
                {
                    if (!string.IsNullOrEmpty(context.Request.RetrievePostedValue("pid")))
                    {
                        projectVars.projectId = context.Request.RetrievePostedValue("pid", @"^[a-zA-Z_\-\d]{1,70}$", false);
                    }
                }

                if (projectVars.projectId != null)
                {
                    nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]");

                    if (nodeProject != null)
                    {
                        projectVars.reportTypeId = nodeProject.GetAttribute("report-type");
                        if (!string.IsNullOrEmpty(projectVars.reportTypeId))
                        {
                            projectVars.editorId = RetrieveEditorIdFromReportId(projectVars.reportTypeId);
                            if (!string.IsNullOrEmpty(projectVars.editorId))
                            {
                                projectVars.projectRootPath = CmsRootPath + RetrieveNodeValueIfExists("/configuration/editors/editor[@id='" + projectVars.editorId + "']/path", xmlApplicationConfiguration);
                                projectVars.projectRootPathOs = websiteRootPathOs + projectVars.projectRootPath;
                            }
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"Could not find project configuration for project id {projectVars.projectId} - 1");
                    }
                }
            }

            // - Set projectId (pid), reportTypeId (rtype), editorId, projectRootPath, projectRootPathOs
            SetProjectVariables(context, projectVars);

            //retrieve the variables needed to edit this document
            if (projectVars.projectId != null)
            {
                if (nodeProject == null) nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]");
                if (nodeProject != null)
                {
                    projectVars.reportTypeId = nodeProject.GetAttribute("report-type");

                    if (!string.IsNullOrEmpty(projectVars.reportTypeId))
                    {
                        if (string.IsNullOrEmpty(projectVars.editorId)) projectVars.editorId = RetrieveEditorIdFromReportId(projectVars.reportTypeId);

                        projectVars.versionId = context.Request.RetrievePostedValue("vid", "1");

                        // retrieve current reporting period
                        projectVars.reportingPeriod = nodeProject.SelectSingleNode("reporting_period")?.InnerText ?? "";

                        // Retrieve project status
                        projectVars.projectStatus = nodeProject.SelectSingleNode("versions/version/status")?.InnerText;

                        // Retrieve the language of the first output channel and set that as the default
                        projectVars.outputChannelDefaultLanguage = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels[1]/output_channel[1]/variants[1]/variant[1]")?.GetAttribute("lang") ?? "en";
                    }
                }
                else
                {
                    appLogger.LogWarning($"Could not find project configuration for project id {projectVars.projectId} - 2");
                }
            }

            // - Set projectId (pid), reportTypeId (rtype), versionId (vid), editorId, currentYear, projectStatus projectRootPath, projectRootPathOs
            SetProjectVariables(context, projectVars);

            // CMS root path
            if (CmsRootPath == null)
            {
                xmlNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/locations/location[@id='cmsroot']");

                if (xmlNode != null)
                {
                    CmsRootPath = xmlNode.InnerText;
                    CmsRootPathOs = CalculateFullPathOs(xmlNode);
                    if (isDevelopmentEnvironment || reqVars.isDebugMode)
                    {
                        AddDebugVariable("cmsRootPath", CmsRootPath);
                        AddDebugVariable("cmsRootPathOs", CmsRootPathOs);
                    }
                }
                else
                {
                    appLogger.LogWarning($"Could not calculate cmsRootPath");
                }
            }

            // Front end customizations root path
            if (CmsCustomFrontEndRootPath == null)
            {
                xmlNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/locations/location[@id='cmscustomfrontendroot']");
                if (xmlNode != null)
                {
                    CmsCustomFrontEndRootPath = CmsRootPath + xmlNode.InnerText;
                    CmsCustomFrontEndRootPathOs = CalculateFullPathOs(xmlNode);
                    if (isDevelopmentEnvironment || reqVars.isDebugMode)
                    {
                        AddDebugVariable("cmsCustomFrontEndRootPath", CmsCustomFrontEndRootPath);
                        AddDebugVariable("cmsCustomFrontEndRootPathOs", CmsCustomFrontEndRootPathOs);
                    }
                }
                else
                {
                    appLogger.LogWarning($"Could not calculate cmsCustomFrontEndRootPath");
                }
            }

            //
            // Posted data
            //
            projectVars.did = context.Request.RetrievePostedValue("did", (reqVars.method == RequestMethodEnum.Post) ? RegexEnum.UltraLoose : RegexEnum.Loose, false, Framework.ReturnTypeEnum.None, null);
            projectVars.outputChannelVariantId = context.Request.RetrievePostedValue("ocvariantid", RegexEnum.Strict, false, Framework.ReturnTypeEnum.None, null);


            projectVars.outputChannelType = context.Request.RetrievePostedValue("octype", RegexEnum.Strict, false, Framework.ReturnTypeEnum.None, null);
            projectVars.outputChannelVariantLanguage = context.Request.RetrievePostedValue("oclang", RegexEnum.Strict, false, Framework.ReturnTypeEnum.None, null);

            //retrieve the editor content type
            projectVars.editorContentType = context.Request.RetrievePostedValue("ctype", "regular");
            if (string.IsNullOrEmpty(projectVars.editorContentType) && !string.IsNullOrEmpty(projectVars.projectId))
            {
                if (nodeProject == null) nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]");

                // Default to the first editor content type that we can find in the configuration
                XmlNode? nodeEditorContentType = nodeProject.SelectSingleNode("content_types/content_management/type");
                if (nodeEditorContentType != null)
                {
                    projectVars.editorContentType = GetAttribute(nodeEditorContentType, "id");
                }
            }

            // Auto expand the project variables if we need to
            if (!string.IsNullOrEmpty(projectVars.projectId) && !string.IsNullOrEmpty(projectVars.outputChannelVariantId))
            {
                if (string.IsNullOrEmpty(projectVars.outputChannelVariantLanguage)) projectVars.outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);
                if (string.IsNullOrEmpty(projectVars.reportTypeId)) projectVars.reportTypeId = RetrieveReportTypeIdFromProjectId(projectVars.projectId);
                if (string.IsNullOrEmpty(projectVars.outputChannelType)) projectVars.outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);
                if (string.IsNullOrEmpty(projectVars.outputChannelVariantLanguage)) projectVars.outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);
            }


            projectVars.token = string.IsNullOrEmpty(context.Session.GetString("sessionToken")) ? "" : context.Session.GetString("sessionToken");

            // Adjust the hierarchies
            // For each request we need to compile a special version of the website hiearchy
            reqVars.xmlHierarchy = ReworkHierarchy(reqVars.xmlHierarchy, projectVars.projectId, projectVars.projectRootPath);

            if (reqVars.isDebugMode && !isApiRoute()) await reqVars.xmlHierarchy.SaveAsync($"{logRootPathOs}/project-hierarchy.xml", false);

            // Rework the reqVars pageId and pageTitle based on the new hierarchy that we have created above
            reqVars.pageId = RetrievePageId(reqVars, reqVars.thisUrlPath, reqVars.xmlHierarchy, true);
            reqVars.pageTitle = RetrieveNodeValueIfExists($"//item[@id={GenerateEscapedXPathString(reqVars.pageId)}]/web_page/linkname", reqVars.xmlHierarchy);

            // Set the URI where the static access can be found
            string? staticAssetsLocation;
            bool isStaticAssetsLocationSet = context.Session.TryGetValue("staticassetslocation", out byte[]? staticAssetsLocationBytes);
            if (isStaticAssetsLocationSet)
            {
                staticAssetsLocation = System.Text.Encoding.UTF8.GetString(staticAssetsLocationBytes);
            }
            else
            {
                staticAssetsLocation = null;
            }
            projectVars.uriStaticAssets = string.IsNullOrEmpty(staticAssetsLocation) ? StaticAssetsLocation : staticAssetsLocation;

            // Store the project variables instance in the context so that we can access it later when rendering the content in the controllers
            SetProjectVariables(context, projectVars);
        }
        else
        {
            // For static assets, create a lightweight instance of the ProjectVariables object
            var projectVars = new ProjectVariables(true);

            // Fill the project variables object
            projectVars.isMiddlewareCreated = true;

            // Attempt to retrieve an alternative user ID from the HTTP header
            projectVars.userIdFromHeader = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserId");
            if (!string.IsNullOrEmpty(projectVars.userIdFromHeader)) projectVars.userIdFromHeader = projectVars.userIdFromHeader.ToLower();

            // Store the project variables instance in the context so that we can access it later when rendering the content in the controllers
            SetProjectVariables(context, projectVars);
        }

        // Proceed with the next middleware component in the pipeline
        await this._next.Invoke(context);
    }

}