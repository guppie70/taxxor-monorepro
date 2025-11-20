using System;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Taxxor.Project;

using static Taxxor.Project.ProjectLogic;
using static Framework;
using System.IO;

/// <summary>
/// Project specific extensions for the framework
/// </summary>
public partial class ProjectExtensions : FrameworkExtensions
{

    /// <summary>
    /// Adds path definitions to the "application" set using method overriding
    /// </summary>
    /// <param name="path"></param>
    /// <param name="relativeTo"></param>
    /// <returns></returns>
    public override string? CalculateFullPathOsProject(HttpContext context, string path, string relativeTo, object? objProjectVars = null, bool hideWarnings = false)
    {

        ProjectVariables projectVars;
        // Console.WriteLine($@"------------------ IN CalculateFullPathOsProject(context, ""{strPath}"", ""{strRelativeTo}"") ------------------");

        string? strPathOs = "";
        switch (relativeTo)
        {
            case "cmsroot":
                strPathOs = ProjectLogic.CmsRootPathOs + path;
                break;
            case "projectroot":

                try
                {
                    if (objProjectVars == null)
                    {
                        projectVars = ProjectLogic.RetrieveProjectVariables(context);
                    }
                    else
                    {
                        projectVars = (ProjectVariables)objProjectVars;
                    }
                }
                catch (Exception ex)
                {
                    Framework.WriteErrorMessageToConsole("Something went wrong in CalculateFullPathOsProject()", ex.ToString());

                    projectVars = new ProjectLogic.ProjectVariables();
                }

                strPathOs = projectVars.projectRootPathOs + path;
                break;
            case "cmscustomfrontendroot":
                strPathOs = ProjectLogic.CmsCustomFrontEndRootPathOs + path;
                break;
            case "sitesroot":
                // The directory just "above" the current project directory or in case of a "new style" .NET project the path above the solution directory
                var sitesRootPathOs = Path.GetDirectoryName(Framework.applicationRootPathOs);
                if (Path.GetFileName(sitesRootPathOs) == Path.GetFileName(Framework.applicationRootPathOs)) sitesRootPathOs = Path.GetDirectoryName(sitesRootPathOs);
                strPathOs = sitesRootPathOs + path;
                break;
            case "solutionroot":
                strPathOs = Path.GetDirectoryName(Framework.applicationRootPathOs) + path;
                break;
            default:
                if (!hideWarnings) appLogger.LogWarning($"FrameworkExtensions.CalculateFullPathOsProject(): path: '{path}', relativeTo: '{relativeTo}', trace: {CreateStackInfo()}");
                strPathOs = null;
                break;
        }

        return strPathOs;
    }

    /// <summary>
    /// Converts project specific placeholders in paths
    /// </summary>
    /// <returns>The full path os project replacements.</returns>
    /// <param name="context">Context.</param>
    /// <param name="strPath">String path.</param>
    public override string CalculateFullPathOsProjectReplacements(HttpContext context, string strPath, object objProjectVars = null, bool hideWarnings = false)
    {
        // Console.WriteLine($"----------------- IN CalculateFullPathOsProjectReplacements() -----------------");

        RequestVariables reqVars;
        ProjectVariables projectVars;


        // Execute the replacements
        if (strPath.Contains("[lang]"))
        {
            reqVars = Framework.RetrieveRequestVariables(context);
            strPath = strPath.Replace("[lang]", reqVars.siteLanguage);
        }

        if (strPath.Contains("[project_path]"))
        {
            try
            {
                if (objProjectVars == null)
                {
                    projectVars = ProjectLogic.RetrieveProjectVariables(context);
                }
                else
                {
                    projectVars = (ProjectVariables)objProjectVars;
                }
            }
            catch (Exception ex)
            {
                Framework.WriteErrorMessageToConsole("Something went wrong in CalculateFullPathOsProjectReplacements()", ex.ToString());

                projectVars = new ProjectLogic.ProjectVariables();
            }

            var editorIdentifier = ProjectLogic.RetrieveEditorIdFromReportId(projectVars.reportTypeId);
            if (!string.IsNullOrEmpty(editorIdentifier))
            {
                var path = Framework.RetrieveNodeValueIfExists("/configuration/editors/editor[@id='" + editorIdentifier + "']/path", Framework.xmlApplicationConfiguration);
                strPath = strPath.Replace("[project_path]", path).Replace("//", "/").Replace(":/", "://");
            }
        }

        strPath = strPath.Replace("[local-ip]", Framework.localIpAddress);

        if (strPath.Contains("[local-domain]"))
        {
            var baseUrl = "";
            var nodeService = xmlApplicationConfiguration.SelectSingleNode($"/configuration/taxxor/components/service//*[(local-name()='service' or local-name()='web-application') and @id='{Framework.applicationId}']");

            var nodeDomain = nodeService.SelectSingleNode($"uri/domain[@type='{siteType}']");
            if (nodeDomain != null)
            {
                baseUrl = nodeDomain.InnerText;
            }
            else
            {
                baseUrl = nodeService.SelectSingleNode("uri")?.InnerText ?? "";
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                if (!hideWarnings) appLogger.LogWarning("Could not calculate local domain, falling back to IP address");
                strPath = strPath.Replace("[local-domain]", Framework.localIpAddress);
            }
            else
            {
                Uri currentApplicationUri = new Uri(baseUrl);
                strPath = strPath.Replace("[local-domain]", currentApplicationUri.Host);
            }
        }

        strPath = strPath.Replace("[local-port-number]", Framework.localPortNumber.ToString());

        if (strPath.Contains("[http-protocol]"))
        {
            reqVars = Framework.RetrieveRequestVariables(context);
            strPath = strPath.Replace("[http-protocol]", reqVars.protocol);
        }

        if (strPath.Contains("[taxxor-client-id]"))
        {
            strPath = strPath.Replace("[taxxor-client-id]", ProjectLogic.TaxxorClientId);
        }


        return strPath;
    }

    /// <summary>
    /// Reworks the hierarchy so that the [projectroot] placeholders are replaced by the path pointing to the editor
    /// </summary>
    /// <returns>The hierarchy.</returns>
    /// <param name="context">Context.</param>
    /// <param name="xmlHierarchy">Xml hierarchy.</param>
    public override XmlDocument ReworkHierarchy(HttpContext context, XmlDocument xmlHierarchy)
    {

        // Attempt to grab the project ID from the request
        var projectId = context.Request.RetrievePostedValue("pid", @"^[\[\]a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.None, "[undefined]");

        if (projectId == "[undefined]" || string.IsNullOrEmpty(projectId))
        {
            // Return the hierarchy "as is"
            return xmlHierarchy;
        }
        else
        {
            var reportType = RetrieveAttributeValueIfExists(@"/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]/@report-type", xmlApplicationConfiguration);
            if (reportType == null)
            {
                HandleError(ReturnTypeEnum.Txt, "Unable to retrieve report type.", $"Xpath /configuration/cms_project/cms_project[@id='{projectId}']/@report-type returned null, stack-trace: {GetStackTrace()}");
            }
            else
            {
                string? nodeProjectEditorPath = null;
                var editorId = ProjectLogic.RetrieveEditorIdFromReportId(reportType);
                if (!string.IsNullOrEmpty(editorId))
                {
                    nodeProjectEditorPath = RetrieveNodeValueIfExists("/configuration/editors/editor[@id='" + editorId + "']/path", xmlApplicationConfiguration);
                }

                if (nodeProjectEditorPath == null)
                {
                    HandleError(ReturnTypeEnum.Txt, "Unable to retrieve report path.", @"Xpath /configuration/editors/editor[@id='" + editorId + "']/path returned null");
                }
                else
                {
                    // Rework the hierarchy with the information we have just grabbed
                    var cmsRootPath = RetrieveNodeValueIfExists("/configuration/general/locations/location[@id='cmsroot']", xmlApplicationConfiguration);
                    var projectRootPath = cmsRootPath + nodeProjectEditorPath;
                    // reqVars.xmlHierarchy = ReworkHierarchy(reqVars.xmlHierarchy, postedProjectId, postedProjectPath);

                    // Inject project root path
                    var xmlNodeListPath = xmlHierarchy.SelectNodes("//item/web_page/path[contains(text(),'[projectroot]')]");
                    foreach (XmlNode xmlNodePath in xmlNodeListPath)
                    {
                        var path = xmlNodePath.InnerText;
                        path = path.Replace("[projectroot]", projectRootPath);
                        xmlNodePath.InnerText = path;
                    }

                }
            }
        }

        // Return the adjusted hierarchy
        return xmlHierarchy;
    }

    /// <summary>
    /// Logic that can be executed when a page could not be located in the hierarchy
    /// </summary>
    /// <param name="requestVariables"></param>
    /// <param name="pageUrl"></param>
    /// <param name="xmlHierarchyPassed"></param>
    public override void HandlePageIdNotFoundCustom(RequestVariables requestVariables, string pageUrl, XmlDocument xmlHierarchyPassed)
    {
        if (!string.IsNullOrEmpty(requestVariables.currentUser.Id))
        {
            //
            // => Remove user RBAC cache from global cache
            //
            if (RbacCacheData.ContainsKey(requestVariables.currentUser.Id))
            {
                var xmlRbacInfo = new XmlDocument();
                RbacCacheData.TryRemove(requestVariables.currentUser.Id, out xmlRbacInfo);
            }
            else
            {
                appLogger.LogInformation($"No cache available so nothing to remove for {requestVariables.currentUser.Id}");
            }
        }
    }


    /// <summary>
    /// Generates the XPath selector for the articles where we need to inject a section number to
    /// </summary>
    /// <returns></returns>
    public override string GenerateArticleXpathForAddingSectionNumbering()
    {
        switch (TaxxorClientId)
        {
            case "philips":
                return "//article[@data-tocnumber and number(@data-hierarchical-level) < 4]";
        }

        return "//article[@data-tocnumber]";
    }

    /// <summary>
    /// Generates a path to an image used in the website which uses curly bracket placeholders to dynamically render full paths based on external variables
    /// </summary>
    /// <param name="basePath"></param>
    /// <returns></returns>
    public override string RenderWebsiteImagePlaceholderPath(string basePath)
    {
        return basePath.Replace("publication/", "/publications/{siteid}/");
    }

    /// <summary>
    /// Generates a path to a download used in the website which uses curly bracket placeholders to dynamically render full paths based on external variables
    /// </summary>
    /// <param name="basePath"></param>
    /// <returns></returns>
    public override string RenderWebsiteDownloadPlaceholderPath(string basePath)
    {
        return basePath.Replace("publication/", "/publications/{siteid}/");
    }



}