using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;

namespace Taxxor.Project
{

    /// <summary>
    /// Logic for creating new projects/filings/documents
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Logic to create a new project using Taxxor information
        /// </summary>
        /// <param name="projectProperties"></param>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CreateNewProject(CmsProjectProperties projectProperties, ReturnTypeEnum returnType = ReturnTypeEnum.Xml)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = siteType == "local" || siteType == "dev" || siteType == "prev";

            // To please the compiler...
            await DummyAwaiter();


            try
            {
                // => Some basic checks
                // - does the project ID already exist
                if (xmlApplicationConfiguration.SelectNodes("//cms_project[@id=" + GenerateEscapedXPathString(projectProperties.projectId) + "]").Count > 0)
                {
                    return new TaxxorReturnMessage(false, "An application with this ID already exists.", "Project with xpath //cms_project[@id='" + projectProperties.projectId + $"'] already exists, stack-trace: {CreateStackInfo()}");
                }

                // => Retrieve the editor id from the configuration
                var editorId = RetrieveEditorIdFromReportId(projectProperties.reportTypeId);
                if (projectProperties.reportTypeId == null)
                {
                    return new TaxxorReturnMessage(false, "Could not locate the editor ID for this filing.", "RetrieveEditorIdFromReportId('" + projectProperties.reportTypeId + $"') returned null..., stack-trace: {CreateStackInfo()}");
                }

                // => Load the project templates document (in order to find by editor-id)
                var projectTemplatesConfigPathOs = CalculateFullPathOs("cms_config_project-templates");
                XmlDocument xmlFilingConfigurationTemplates = new XmlDocument();
                if (File.Exists(projectTemplatesConfigPathOs))
                {
                    xmlFilingConfigurationTemplates.Load(projectTemplatesConfigPathOs);
                }
                else
                {
                    return new TaxxorReturnMessage(false, "Could not load project templates configuration.", $"Path '{projectTemplatesConfigPathOs}' was not found, stack-trace: {CreateStackInfo()}");
                }

                var xpathProjectXmlTemplate = $"/project_templates/customer[contains(@taxxorclientid, '{TaxxorClientId}')]/cms_project[@editorId='{editorId}']";
                var nodeTemplateProject = xmlFilingConfigurationTemplates.SelectSingleNode(xpathProjectXmlTemplate);
                if (nodeTemplateProject == null)
                {
                    // Fallback to a generic template if a specific one could not be found
                    var xpathProjectXmlTemplateGeneric = $"/project_templates/customer[@taxxorclientid='generic']/cms_project[@editorId='{editorId}']";
                    nodeTemplateProject = xmlFilingConfigurationTemplates.SelectSingleNode(xpathProjectXmlTemplateGeneric);

                    if (nodeTemplateProject == null)
                    {
                        return new TaxxorReturnMessage(false, "Could not find a project template.", $"{xpathProjectXmlTemplate} and {xpathProjectXmlTemplateGeneric} returned no results, stack-trace: {CreateStackInfo()}");
                    }
                }

                // => Calculate new path for the data for this project and create it
                var newDataPath = "/projects/" + projectProperties.reportTypeId + "/" + projectProperties.projectId;
                var newDataPathOs = dataRootPathOs + newDataPath;
                if (!Directory.Exists(newDataPathOs)) Directory.CreateDirectory(newDataPathOs);

                // => Prepare project configuration
                var dataConfigurationPathOs = CalculateFullPathOs("/configuration/configuration_system//config/location[@id='data_configuration']");
                var xmlDataConfiguration = new XmlDocument();
                xmlDataConfiguration.Load(dataConfigurationPathOs);

                var nodeProjectsRoot = xmlDataConfiguration.SelectSingleNode("//cms_projects");

                var nodeNewProject = xmlDataConfiguration.ImportNode(nodeTemplateProject, true);
                // Add project specifics in the cloned node
                nodeNewProject.Attributes["id"].Value = projectProperties.projectId;
                nodeNewProject.Attributes["report-type"].Value = projectProperties.reportTypeId;

                string publicationDate = projectProperties.GetPublicationDate();
                if (!string.IsNullOrEmpty(publicationDate)) nodeNewProject.SetAttribute("date-publication", publicationDate);

                // if (!string.IsNullOrEmpty(guidCalendarEvent)) nodeNewProject.Attributes["guidCalendarEvent"].Value = guidCalendarEvent;
                nodeNewProject.SelectSingleNode("name").InnerText = projectProperties.name;
                nodeNewProject.SelectSingleNode("location[@id='reportdataroot']").InnerText = newDataPath;
                nodeNewProject.SelectSingleNode("system/entities/entity").Attributes["guidLegalEntity"].Value = projectProperties.guidLegalEntity;
                nodeNewProject.SelectSingleNode("reporting_period").InnerText = projectProperties.reportingPeriod;

                // Delete the editorId as this was only required to select the XML structure from the template file 
                RemoveAttribute(nodeNewProject, "editorId");

                var datePattern = @"yyyy-MM-dd HH:mm:ss";
                DateTime dateNow = DateTime.Now;
                var currentDate = dateNow.ToString(datePattern);
                nodeNewProject.SelectSingleNode("versions/version/date_created").InnerText = currentDate;

                nodeProjectsRoot.AppendChild(nodeNewProject);
                //xmlDataConfiguration.Save(logRootPathOs + "/projectconfig.xml");

                //Response.Write(Server.HtmlEncode(xmlProjectConfiguration.OuterXml));

                // => Retrieve the path to the editor
                var editorPath = "xyzzz";
                if (!string.IsNullOrEmpty(editorId))
                {
                    editorPath = RetrieveNodeValueIfExists("/configuration/editors/editor[@id='" + editorId + "']/path", xmlApplicationConfiguration);
                }

                // => Prepare the data folder (needs to become a call to the data service)
                if (!SetupFilingDataStructure(newDataPathOs, returnType, true, debugRoutine))
                {
                    //could not setup the default data structure & git repositories
                    return new TaxxorReturnMessage(false, "Could not create the initial filing repository.", "newDataPathOs='" + newDataPathOs + "' - examine the log for more info.");
                }

                // => Copy the predefined file and directory structure from the templates repository to the location where we will be storing our data
                var templateDataFolderPathOs = CalculateFullPathOs("cms_data_project-templates").Replace("[editor_id]", editorId).Replace("[taxxor-client-id]", TaxxorClientId);

                // Fallback to a generic type for AR / QR project types
                if (!Directory.Exists(templateDataFolderPathOs))
                {
                    templateDataFolderPathOs = CalculateFullPathOs("cms_data_project-templates").Replace("[editor_id]", editorId).Replace("[taxxor-client-id]", "generic");
                }

                //var templateDataFolderPathOs = cmsTemplatesRootPathOs + "/projects/" + filingDocumentTypeId;
                //var newProjectDataFolderPathOs = dataRootPathOs + RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectProperties.projectId) + "]/path", xmlProjectConfiguration);
                CopyDirectoryRecursive(templateDataFolderPathOs, newDataPathOs, true);

                // => If all went well - store new and updated project configutaion xml file
                var saved = await SaveXmlDocument(xmlDataConfiguration, dataConfigurationPathOs);
                if (!saved)
                {
                    return new TaxxorReturnMessage(false, "Could not save the data configuration file.", $"stack-trace: {GetStackTrace()}");
                }

                // => Commit the new information in the git repository
                GitCommit("Filing start data", newDataPathOs, returnType, true);

                // => Update the CMS metadata content
                await UpdateCmsMetadata();

                // Return a success object
                return new TaxxorReturnMessage(true, "Successfully setup the new project", $"newDataPathOs: {newDataPathOs}");
            }
            // To make sure HandleError is correctly executed
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was a problem setting up your new project structure", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }


        }

        /// <summary>
        /// Sets up the default folder and git structures and repositories for a filing
        /// </summary>
        /// <param name="rootPathOs"></param>
        /// <param name="returnTypeError"></param>
        /// <param name="stopOnError"></param>
        /// <param name="debugMode"></param>
        /// <returns></returns>
        public static bool SetupFilingDataStructure(string rootPathOs, ReturnTypeEnum returnTypeError = ReturnTypeEnum.Txt, bool stopOnError = true, bool debugMode = false)
        {
            var success = true;
            var result = new StringBuilder();

            var errorMessage = "";
            var errorDebugInfo = "";

            // 1) Create root folder
            try
            {
                if (!Directory.Exists(rootPathOs))
                {
                    Directory.CreateDirectory(rootPathOs);
                    result.AppendLine("Created folder: " + rootPathOs);
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Could not create folder";
                errorDebugInfo = "rootPathOs=" + rootPathOs + " - " + ex.ToString();

                if (stopOnError)
                {
                    WriteErrorMessageToConsole(errorMessage, errorDebugInfo);
                }
                else
                {
                    HandleError(returnTypeError, errorMessage, errorDebugInfo);
                }

                return false;
            }

            // 2) Initiate empty git repro
            result.AppendLine($"1) Create new empty GIT repository in '{rootPathOs}'");

            //gitUser = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='git-username']", xmlApplicationConfiguration);
            //gitUserEmail = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='git-email']", xmlApplicationConfiguration);

            List<string> gitCommandList = new List<string>();
            gitCommandList.Add("init");
            gitCommandList.Add("config user.email \"" + GitUserEmail + "\"");
            gitCommandList.Add("config user.name \"" + GitUser + "\"");
            gitCommandList.Add("config core.autocrlf false");
            string gitResult = GitCommand(gitCommandList, rootPathOs, returnTypeError, false);
            if (gitResult == null)
            {
                // Something went wrong
                errorMessage = "Could not setup initial repository";
                errorDebugInfo = $"Git init failed (examine log for more details) - commands tried: {string.Join(", ", gitCommandList.ToArray())}, status: {result.ToString()}, stack-trace: {GetStackTrace()}";
                if (stopOnError)
                {
                    WriteErrorMessageToConsole(errorMessage, errorDebugInfo);
                }
                else
                {
                    HandleError(returnTypeError, errorMessage, errorDebugInfo);
                }

                return false;
            }
            else
            {
                result.AppendLine(gitResult);

                // 3) Copy files and folders from the template repro to the new repro
                var templatePathOs = CalculateFullPathOs("cms_data_git-templates") + "/lextra_filing";
                CopyDirectoryRecursive(templatePathOs, rootPathOs, true);
                result.AppendLine("2) Sucessfully copied " + templatePathOs + " to " + rootPathOs);

                // 4) Stage the initial data in the repository
                result.AppendLine($"3) Stage new content in '{rootPathOs}'");
                gitCommandList.Clear();
                gitCommandList.Add("add .");
                gitCommandList.Add("commit -m \"" + RenderGitCommitMessageInformation("Initial commit") + "\"");
                gitResult = GitCommand(gitCommandList, rootPathOs, returnTypeError, false);
                if (gitResult == null)
                {
                    // Something went wrong
                    errorMessage = "Could not commit initial content";
                    errorDebugInfo = $"Git commit failed (examine log for more details) - commands tried: {string.Join(", ", gitCommandList.ToArray())}, status: {result.ToString()}, stack-trace: {GetStackTrace()}";

                    if (stopOnError)
                    {
                        WriteErrorMessageToConsole(errorMessage, errorDebugInfo);
                    }
                    else
                    {
                        HandleError(returnTypeError, errorMessage, errorDebugInfo);
                    }

                    return false;
                }
                else
                {
                    result.AppendLine(gitResult);

                    // 5) Create the nested content repository
                    var contentPathOs = rootPathOs + "/content";
                    result.AppendLine($"4) Create new nested GIT repository in {contentPathOs}");
                    gitCommandList.Clear();
                    gitCommandList.Add("init");
                    gitCommandList.Add("config user.email \"" + GitUserEmail + "\"");
                    gitCommandList.Add("config user.name \"" + GitUser + "\"");
                    gitCommandList.Add("config core.autocrlf false");
                    gitCommandList.Add("add .");
                    gitCommandList.Add("commit -m \"" + RenderGitCommitMessageInformation("Initial commit") + "\"");
                    gitResult = GitCommand(gitCommandList, contentPathOs, returnTypeError, false);
                    if (gitResult == null)
                    {
                        // Something went wrong
                        errorMessage = "Could not create nested content repository";
                        errorDebugInfo = $"Git failed (examine log for more details) - commands tried: {string.Join(", ", gitCommandList.ToArray())}, status: {result.ToString()}, stack-trace: {GetStackTrace()}";

                        if (stopOnError)
                        {
                            WriteErrorMessageToConsole(errorMessage, errorDebugInfo);
                        }
                        else
                        {
                            HandleError(returnTypeError, errorMessage, errorDebugInfo);
                        }

                        return false;
                    }
                    else
                    {
                        result.AppendLine(gitResult);
                    }

                }
            }

            if (debugMode)
            {
                TextFileCreate(result.ToString(), logRootPathOs + "/setup-filing-datastructure.log");
            }

            return success;
        }

    }
}