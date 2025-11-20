using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
        // Retrieve the request variables
        var reqVars = RetrieveRequestVariables(context);

        // Get RequestContext from the scoped service provider
        if (reqVars.isGrpcRequest)
        {
            if (context.RequestServices.GetService(typeof(RequestContext)) is RequestContext requestContext) 
                requestContext.RequestVariables = reqVars;
        }

        // Only retrieve additional information if we are dealing with an asset that needs to be calculated
        if (!reqVars.isStaticAsset && !reqVars.isGrpcRequest)
        {
            XmlNode? nodeProject = null;

            // Create a project variables instance
            ProjectVariables projectVars = new ProjectVariables()
            {
                // Fill the project variables object
                isMiddlewareCreated = true,
                // Are we dealing with a page that is only used for internal (Taxxor system) information exchange
                isInternalServicePage = IsInternalServicePage(reqVars)
            };

            // Tenant specific settings
            string tenantIdFromHeader = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-Tenant-Id");
            if (!string.IsNullOrEmpty(tenantIdFromHeader)) projectVars.tenantId = tenantIdFromHeader;
            if (!TenantSpecificSettings.ContainsKey(projectVars.tenantId)) TenantSpecificSettings[projectVars.tenantId] = new TenantSettings();

            //project id passed as a querystring variable
            if (projectVars.projectId == null)
            {
                var projectId = context.Request.RetrievePostedValue("pid");
                if (!string.IsNullOrEmpty(projectId))
                {
                    projectVars.projectId = projectId;

                    // Test if the project ID exists in the configuration, otherwise return an error
                    if (projectId != "all")
                    {
                        nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]");
                        if (nodeProject == null)
                        {
                            HandleError(reqVars, "Project does not exist", $"projectId: {projectId}");
                        }
                        else
                        {
                            projectVars.reportTypeId = nodeProject.GetAttribute("report-type");
                            if (!string.IsNullOrEmpty(projectVars.reportTypeId))
                            {
                                projectVars.editorId = RetrieveEditorIdFromReportId(projectVars.reportTypeId);
                            }
                        }
                    }
                }
            }

            //retrieve the variables needed to edit this document
            if (projectVars.projectId != null)
            {
                // projectVars.reportTypeId = RetrieveAttributeValueIfExists("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/@report-type", xmlApplicationConfiguration);

                if (!string.IsNullOrEmpty(projectVars.reportTypeId))
                {
                    projectVars.versionId = context.Request.RetrievePostedValue("vid", null);
                    if (string.IsNullOrEmpty(projectVars.versionId))
                    {
                        projectVars.versionId = "1";
                    }

                    if (projectVars.versionId == "latest")
                    {
                        projectVars.versionId = "1";
                    }

                    FillCorePathsInProjectVariables(ref projectVars);
                }

            }

            SetProjectVariables(context, projectVars);

            // TODO: Review below code -> we do not fill dataRootPathOs anywhere??
            if (!string.IsNullOrEmpty(dataRootPathOs))
            {
                if (string.IsNullOrEmpty(CmsDataAppRootPathOs))
                {
                    CmsDataAppRootPathOs = Path.GetDirectoryName(dataRootPathOs);
                    if (isDevelopmentEnvironment || reqVars.isDebugMode)
                    {
                        AddDebugVariable("cmsDataAppRootPathOs", CmsDataAppRootPathOs);
                    }
                }

                // Location for the templates
                var nodeTemplatesLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/locations/location[@id='cmstemplatesroot']");
                if (nodeTemplatesLocation != null) projectVars.cmsTemplatesRootPathOs = CalculateFullPathOs(nodeTemplatesLocation);
            }

            // TODO: check if there is a more solid way to do this...
            //default values if needed 
            if (string.IsNullOrEmpty(projectVars.cmsDataRootPath)) projectVars.cmsDataRootPath = dataRootPath + RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project/location[@id='reportdataroot']", xmlApplicationConfiguration);
            if (string.IsNullOrEmpty(projectVars.cmsDataRootBasePathOs)) projectVars.cmsDataRootBasePathOs = dataRootPathOs + RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project/location[@id='reportdataroot']", xmlApplicationConfiguration);
            if (string.IsNullOrEmpty(projectVars.cmsDataRootPathOs)) projectVars.cmsDataRootPathOs = dataRootPathOs;

            projectVars.cmsContentRootPathOs = projectVars.cmsDataRootBasePathOs + "/content"; //cmscontentroot

            SetProjectVariables(context, projectVars);

            // Retrieve the document section id that we want to edit
            projectVars.did = context.Request.RetrievePostedValue("did");

            //retrieve the editor content type
            projectVars.editorContentType = context.Request.RetrievePostedValue("ctype", null);
            if (string.IsNullOrEmpty(projectVars.editorContentType) && !string.IsNullOrEmpty(projectVars.projectId) && projectVars.projectId != "all")
            {
                if (nodeProject == null) nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]");
                // Default to the first editor content type that we can find in the configuration
                XmlNode? nodeEditorContentType = nodeProject.SelectSingleNode("content_types/content_management/type");
                if (nodeEditorContentType != null)
                {
                    projectVars.editorContentType = GetAttribute(nodeEditorContentType, "id");
                }
            }
            projectVars.outputChannelType = context.Request.RetrievePostedValue("octype");
            projectVars.outputChannelVariantId = context.Request.RetrievePostedValue("ocvariantid");
            projectVars.outputChannelVariantLanguage = context.Request.RetrievePostedValue("oclang");

            if (!string.IsNullOrEmpty(projectVars.editorId))
            {
                // Default value for output channel variables (first definition listed in the configuration)
                if (string.IsNullOrEmpty(projectVars.outputChannelType)) projectVars.outputChannelType = RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/@type", xmlApplicationConfiguration);
                if (string.IsNullOrEmpty(projectVars.outputChannelVariantId)) projectVars.outputChannelVariantId = RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/variants/variant/@id", xmlApplicationConfiguration);
                if (string.IsNullOrEmpty(projectVars.outputChannelVariantLanguage)) projectVars.outputChannelVariantLanguage = RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/variants/variant/@lang", xmlApplicationConfiguration);
            }

            // Rework the reqVars pageId and pageTitle based on the new hierarchy that we have created above
            reqVars.pageId = RetrievePageId(reqVars, reqVars.thisUrlPath, reqVars.xmlHierarchy, true);
            reqVars.pageTitle = RetrieveNodeValueIfExists($"//item[@id={GenerateEscapedXPathString(reqVars.pageId)}]/web_page/linkname", reqVars.xmlHierarchy);

            // Store the project variables instance in the context so that we can access it later when rendering the content in the controllers
            SetProjectVariables(context, projectVars);

        }

        // Proceed with the next middleware component in the pipeline
        await this._next.Invoke(context);
    }

}