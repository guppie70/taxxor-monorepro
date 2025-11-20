using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
    /// <param name="context"></param>
    /// <param name="strPath"></param>
    /// <param name="strRelativeTo"></param>
    /// <param name="objProjectVars"></param>
    /// <returns></returns>
    public override string? CalculateFullPathOsProject(HttpContext context, string strPath, string strRelativeTo, object? objProjectVars = null, bool hideWarnings = false)
    {

        ProjectVariables projectVars;
        try
        {
            if (objProjectVars == null)
            {
                projectVars = ProjectLogic.RetrieveProjectVariables(context, !hideWarnings);
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

        // Console.WriteLine($@"------------------ IN CalculateFullPathOsProject(context, ""{strPath}"", ""{strRelativeTo}"") ------------------");
        string? strPathOs = "";
        try
        {

            switch (strRelativeTo)
            {
                case "cmsdataapproot":
                    strPathOs = ProjectLogic.CmsDataAppRootPathOs + strPath;
                    break;
                case "cmsdataroot":
                    strPathOs = projectVars.cmsDataRootPathOs + strPath;
                    break;
                case "cmscontentroot":
                    strPathOs = projectVars.cmsContentRootPathOs + strPath;
                    break;
                case "cmsdatarootbase":
                    strPathOs = projectVars.cmsDataRootBasePathOs + strPath;
                    break;
                case "cmsuserdataroot":
                    if (projectVars.currentUser.IsAuthenticated)
                    {
                        strPathOs = Framework.dataRootPathOs + "/users/" + projectVars.currentUser.Id + strPath;
                    }
                    else
                    {
                        strPathOs = null;
                    }
                    break;
                case "cmstemplatesroot":
                    strPathOs = projectVars.cmsTemplatesRootPathOs + strPath;
                    break;
                case "solutionroot":
                    strPathOs = Path.GetDirectoryName(Framework.applicationRootPathOs) + strPath;
                    break;
                case "sitesroot":
                    // The directory just "above" the current project directory or in case of a "new style" .NET project the path above the solution directory
                    var sitesRootPathOs = Path.GetDirectoryName(Framework.applicationRootPathOs);
                    if (Path.GetFileName(sitesRootPathOs) == Path.GetFileName(Framework.applicationRootPathOs)) sitesRootPathOs = Path.GetDirectoryName(sitesRootPathOs);
                    strPathOs = sitesRootPathOs + strPath;
                    break;
                case "projectdownloads":
                case "projectimages":
                case "projectreports":
                    strPathOs = RetrieveAssetTypeRootPathOs(projectVars, strRelativeTo) + strPath;
                    break;
                default:
                    strPathOs = null;
                    break;
            }
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, $"Could not generate path. error: {ex}");
        }


        return strPathOs;
    }

    /// <summary>
    /// Converts project specific placeholders in paths
    /// </summary>
    /// <param name="context"></param>
    /// <param name="strPath"></param>
    /// <param name="objProjectVars"></param>
    /// <returns></returns>
    public override string CalculateFullPathOsProjectReplacements(HttpContext context, string strPath, object? objProjectVars = null, bool hideWarnings = false)
    {
        // Global variables
        RequestVariables reqVars;
        ProjectVariables projectVars;


        // Execute the replacements
        if (strPath.Contains("[lang]"))
        {
            reqVars = Framework.RetrieveRequestVariables(context);
            strPath = strPath.Replace("[lang]", reqVars.siteLanguage);
        }

        if (strPath.Contains("[did]"))
        {
            projectVars = (objProjectVars == null) ?
             ProjectLogic.RetrieveProjectVariables(context) :
             (ProjectVariables)objProjectVars;
            if (projectVars.did == null && !hideWarnings) Framework.appLogger.LogWarning($"Could not replace [did] in path: {strPath}. method: CalculateFullPathOsProjectReplacements(), projectId: {projectVars.projectId??"unknown"}, stack-trace: {GetStackTrace()}");
            strPath = strPath.Replace("[did]", projectVars.did ?? "");
        }

        if (strPath.Contains("[project_path]"))
        {
            projectVars = ProjectLogic.RetrieveProjectVariables(context);
            var editorIdentifier = ProjectLogic.RetrieveEditorIdFromReportId(projectVars.reportTypeId);
            if (!string.IsNullOrEmpty(editorIdentifier))
            {
                var path = Framework.RetrieveNodeValueIfExists("/configuration/editors/editor[@id='" + editorIdentifier + "']/path", Framework.xmlApplicationConfiguration);
                strPath = strPath.Replace("[project_path]", path).Replace("//", "/");
            }
        }

        return strPath;
    }

    /// <summary>
    /// When an xml document is saved, attempt to commit to the git repository
    /// </summary>
    /// <param name="xmlDoc"></param>
    /// <param name="xmlPathOs"></param>
    /// <returns></returns>
    public new static bool SaveXmlDocumentProject(HttpContext context, XmlDocument xmlDoc, string xmlPathOs, string message)
    {
        ProjectLogic.ProjectVariables projectVars = ProjectLogic.RetrieveProjectVariables(context);
        if (!string.IsNullOrEmpty(message))
        {
            if (xmlPathOs.Contains(projectVars.cmsDataRootBasePathOs))
            {
                if (string.IsNullOrEmpty(message)) message = "Saving data";
                if (xmlPathOs.Contains("/content"))
                {
                    var repositoryPathOs = xmlPathOs.SubstringBefore("/content") + "/content";

                    // Images, downloads, etc.
                    return ProjectLogic.GitCommit(message, repositoryPathOs, ReturnTypeEnum.Txt, false);
                }
                else
                {
                    return ProjectLogic.CommitFilingData(message);
                }
            }
            else
            {
                if (xmlPathOs.Contains(ProjectLogic.dataRootPathOs))
                {
                    if (string.IsNullOrEmpty(message)) message = "Saving data";
                    return ProjectLogic.CommitDataContent(message);
                }
            }
        }

        return true;
    }
}