using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// Creates a new project in the Taxxor Document Store
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Lists the available report types
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task ListAvailableReportTypes(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted values
            var guidLegalEntity = request.RetrievePostedValue("guidLegalEntity");

            XsltArgumentList xsltArgumentList = new XsltArgumentList();
            // xsltArgumentList.AddParam("filter-editor-id", "", TaxxorClientId);
            var xmlAvailableProjectTypes = TransformXmlToDocument(xmlApplicationConfiguration, "cms_available-report-types", xsltArgumentList);

            // Generate JSON output
            var jsonToReturn = ConvertToJson(xmlAvailableProjectTypes, Newtonsoft.Json.Formatting.Indented, true);

            // Render the response
            await response.OK(jsonToReturn, reqVars.returnType, true);
        }


        /// <summary>
        /// Setup a new CMS project and use submitted form data to do so
        /// </summary>
        public static async Task CreateNewProject(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted values
            var newProjectName = request.RetrievePostedValue("name", @"^([a-zA-Z0-9'\(\)<>\s\.,\?\+_\-\*&\^\%/]){1,120}$", true, ReturnTypeEnum.Json);
            var entryPointUri = request.RetrievePostedValue("uri", RegexEnum.Uri, true, ReturnTypeEnum.Json);
            var reportingPeriod = request.RetrievePostedValue("reportingperiod", @"^[a-zA-Z_\-\d]{1,20}$", false, ReturnTypeEnum.Json, null);
            var publicationDate = request.RetrievePostedValue("publicationdate", RegexEnum.isodate, false, ReturnTypeEnum.Json, null);
            var reportType = request.RetrievePostedValue("reporttype", @"^[a-zA-Z_\-\d]{1,256}$", true, ReturnTypeEnum.Json);
            var guidLegalEntity = request.RetrievePostedValue("guidLegalEntity", true, ReturnTypeEnum.Json);

            //
            // => Stylesheet cache
            //
            SetupPdfStylesheetCache(reqVars);

            // Test that at least one of reporting period and a publication date have been submitted
            if ((string.IsNullOrEmpty(reportingPeriod) || reportingPeriod == "none") && string.IsNullOrEmpty(publicationDate))
            {
                HandleError(ReturnTypeEnum.Json, "Not enough information was supplied to setup a new project", $"reportingPeriod: {reportingPeriod.ToString()}, publicationDate: {publicationDate.ToString()}, stack-trace: {GetStackTrace()}");
            }

            // 
            // => Wait in case another project is being created
            //
            if (ProjectCreationInProgress)
            {
                var maxWaitLoops = 30;
                var loopCounter = 0;
                do
                {
                    loopCounter++;
                    appLogger.LogInformation($"Another project is being created, so we wait until that one is finished");
                    await PauseExecution(1000);
                } while ((ProjectCreationInProgress && loopCounter <= maxWaitLoops));

                if (loopCounter == (maxWaitLoops + 1))
                {
                    HandleError(ReturnTypeEnum.Json, "Another project creation process is in progress and seems to take a long time to finalize", $"loopCounter: {loopCounter.ToString()}, stack-trace: {GetStackTrace()}");
                }
            }

            // Set the global property to mark that we are creating a project
            ProjectCreationInProgress = true;

            // Render project ID
            var baseProjectId = Extensions.GetBaseProjectId(reportingPeriod, publicationDate, reportType);
            var newProjectId = CalculateUniqueProjectId(baseProjectId);

            // Console.WriteLine("@@@@@@@@@@@@@@@");
            // Console.WriteLine($"- baseProjectId: {baseProjectId}");
            // Console.WriteLine($"- newProjectId: {newProjectId}");
            // Console.WriteLine("@@@@@@@@@@@@@@@");

            // HandleError("Thrown on purpose");

            //
            // => Retrieve the report-type and editor-id from the configuration
            //
            var reportTypeId = RetrieveAttributeValueIfExists("/configuration/report_types/report_type[entry_points/uri/text()=" + GenerateEscapedXPathString(entryPointUri) + "]/@id", xmlApplicationConfiguration);
            if (reportTypeId == null)
            {
                HandleError(ReturnTypeEnum.Json, "Could not locate the filing ID for this filing.", "Report type with xpath //report_type[entry_points/uri/text()='" + entryPointUri + $"']/@id does not exist, stack-trace: {CreateStackInfo()}");
            }

            var editorId = RetrieveEditorIdFromReportId(reportTypeId);

            //
            // => Initiate a project properties object for the project we are about to create
            //
            var projectProperties = new CmsProjectProperties(newProjectId);
            projectProperties.name = newProjectName;
            projectProperties.reportingPeriod = reportingPeriod;
            projectProperties.SetPublicationDate(publicationDate);
            projectProperties.reportTypeId = reportTypeId;
            projectProperties.guidLegalEntity = guidLegalEntity;


            var baseDebugInfo = $"{projectProperties.ToString()}, entryPointUri: {entryPointUri}";

            // HandleError(ReturnTypeEnum.Json, "Thrown on purpose", $"{baseDebugInfo}");

            //
            // => Create the new project structure on the Taxxor Document Store
            //
            TaxxorReturnMessage createNewProjectResult = await CreateNewProject(projectProperties);
            if (!createNewProjectResult.Success)
            {
                await _HandleProjectCreateError(newProjectId, "Creation basic project structure failed", $"createNewProjectResult: {createNewProjectResult.ToString()}, {baseDebugInfo}");
            }

            //
            // => Update the application configuration for this application
            //
            TaxxorReturnMessage updateApplicationConfigResult = await UpdateDataConfigurationInApplicationConfiguration();
            if (!updateApplicationConfigResult.Success)
            {
                await _HandleProjectCreateError(newProjectId, "Update application configuration failed", $"updateApplicationConfigResult: {updateApplicationConfigResult.ToString()}, {baseDebugInfo}");
            }


            // => Add a resource ID for the current project in the Taxxor Access Control Service and assign the taxxor-admin group with admin access
            var itemId = "cms_project-details";
            var resourceId = CalculateRbacResourceId(RequestMethodEnumToString(RequestMethodEnum.Get), itemId, newProjectId, 3, false);
            var createAccessRecordResult = false;
            createAccessRecordResult = await AccessControlService.AddAccessRecord(resourceId, "administrators", "taxxor-administrator", true, true, debugRoutine);
            if (!createAccessRecordResult)
            {
                await _HandleProjectCreateError(newProjectId, "Creation of standard access control record failed", $"{baseDebugInfo}");
            }

            // => Test if the current user is part of the administrator user group
            var currentUserIsAdmin = false;
            var xmlAdminUsers = await AccessControlService.ListUsersInGroup("administrators");
            if (xmlAdminUsers.SelectNodes($"/users/user[@ref='{projectVars.currentUser.Id}']").Count > 0) currentUserIsAdmin = true;

            // => Create an access record for the current user if he is not a member of the taxxor admin group
            if (!currentUserIsAdmin)
            {
                createAccessRecordResult = await AccessControlService.AddAccessRecord(resourceId, projectVars.currentUser.Id, "taxxor-cms-manager", false, true, debugRoutine);
                if (!createAccessRecordResult)
                {
                    await _HandleProjectCreateError(newProjectId, "Creation of standard access control record failed", $"{baseDebugInfo}");
                }
            }

            // => Optionally create additional Taxxor Access Control entries if these have been defined in the data_configuration file
            var nodeListProjectAclEntries = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{editorId}']/default_settings/access_control/default/entry");
            foreach (XmlNode nodeProjectAclEntry in nodeListProjectAclEntries)
            {
                var userGroupId = GetAttribute(nodeProjectAclEntry, "usergroupid");
                var roleId = GetAttribute(nodeProjectAclEntry, "roleid");
                if (debugRoutine)
                {
                    Console.WriteLine("----- Additional access record settings for the new project -----");
                    Console.WriteLine($"- userGroupId: {userGroupId}");
                    Console.WriteLine($"- roleId: {roleId}");
                    Console.WriteLine("-----------------------------------------------------------------");
                }

                createAccessRecordResult = await AccessControlService.AddAccessRecord(resourceId, userGroupId, roleId, false, true, debugRoutine);
                if (!createAccessRecordResult)
                {
                    await _HandleProjectCreateError(newProjectId, $"Creation of additional access control record failed (userGroupId: {userGroupId}, roleId: {roleId})", $"{baseDebugInfo}");
                }
            }


            // Fill the project vars object with information for the new project that we are setting up
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(newProjectId, true);
            if (!renderProjectVarsResult) await _HandleProjectCreateError(newProjectId, "Creation of project variable for new project failed", $"newProjectId: {newProjectId.ToString()}, {baseDebugInfo}");

            //
            // => Create an initial version 0.0
            //
            var versionCreateResult = await GenerateVersion(newProjectId, "major", "v0.0", "Initial version", debugRoutine);
            if (!versionCreateResult.Success)
            {
                await _HandleProjectCreateError(newProjectId, "Creation of initial version failed", $"versionCreateResult: {versionCreateResult.ToString()}, {baseDebugInfo}");
            }

            // 
            // => Create the image renditions
            // 
            var overallResult = new StringBuilder();
            overallResult.AppendLine("* Update image renditions *");
            var imageRenditionsResult = await GenerateImageRenditions(projectVars.projectId);
            ProcessTaxxorLogResult(imageRenditionsResult);

            overallResult.AppendLine("* Update drawing renditions *");
            var drawingRenditionsResult = await GenerateDrawingRenditions(reqVars, projectVars);
            ProcessTaxxorLogResult(drawingRenditionsResult);

            overallResult.AppendLine("* Update graph renditions *");
            var graphsRenditionsResult = await GenerateGraphRenditions(reqVars, projectVars);
            ProcessTaxxorLogResult(graphsRenditionsResult);

            baseDebugInfo += $", image-drawing-conversion: {overallResult.ToString()}";


            //
            // => Update the local CMS metadata object
            //
            await UpdateCmsMetadata();

            // Reset the global variable to indicate that we are done
            ProjectCreationInProgress = false;

            // Clear the user section information cache data
            UserSectionInformationCacheData.Clear();

            //
            // => Response for the client
            //
            await response.OK(GenerateSuccessXml("Successfully completed setup of the new project", baseDebugInfo), ReturnTypeEnum.Json, true);


            void ProcessTaxxorLogResult(TaxxorLogReturnMessage message)
            {
                if (message.Success)
                {
                    overallResult.AppendLine("Successfully completed");
                }
                else
                {
                    overallResult.AppendLine("Failed");
                }

                if (message.InformationLog.Count > 0) overallResult.AppendLine($"informationLog: {string.Join(',', message.InformationLog.ToArray())}");
                if (message.SuccessLog.Count > 0) overallResult.AppendLine($"successLog: {string.Join(',', message.SuccessLog.ToArray())}");
                if (message.WarningLog.Count > 0) overallResult.AppendLine($"warningLog: {string.Join(',', message.WarningLog.ToArray())}");
                if (message.ErrorLog.Count > 0) overallResult.AppendLine($"errorLog: {string.Join(',', message.ErrorLog.ToArray())}");
                overallResult.AppendLine("-----------------------------------");
                overallResult.AppendLine("");
            }
        }



        /// <summary>
        /// Logic to create a new project using Taxxor information
        /// </summary>
        /// <param name="projectProperties"></param>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CreateNewProject(CmsProjectProperties projectProperties, ReturnTypeEnum returnType = ReturnTypeEnum.Json)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);


            var baseDebugInfo = projectProperties.ToString();

            var editorId = RetrieveEditorIdFromReportId(projectProperties.reportTypeId);
            if (projectProperties.reportTypeId == null)
            {
                return new TaxxorReturnMessage(false, "Could not locate the editor ID for this filing.", "RetrieveEditorIdFromReportId('" + projectProperties.reportTypeId + $"') returned null..., stack-trace: {CreateStackInfo()}");
            }

            // => Retrieve the path to the editor
            var editorPath = "xyzzz";
            if (!string.IsNullOrEmpty(editorId))
            {
                editorPath = RetrieveNodeValueIfExists("/configuration/editors/editor[@id='" + editorId + "']/path", xmlApplicationConfiguration);
            }

            var editorPathOs = CmsRootPathOs + editorPath;
            if (!Directory.Exists(editorPathOs))
            {
                return new TaxxorReturnMessage(false, "Could not locate the physical path for the editor.", "Path '" + editorPathOs + $"' could not be found, stack-trace: {CreateStackInfo()}");
            }

            // => Calculate new path for the data for this project
            var newDataPath = "/projects/" + projectProperties.reportTypeId + "/" + projectProperties.projectId;
            var newDataPathOs = dataRootPathOs + newDataPath;

            // => Setup the basic project structure on the Taxxor Data Store
            var createProjectDataToPost = new Dictionary<string, string>
            {
                { "rtype", projectProperties.reportTypeId },
                { "id", projectProperties.projectId },
                { "name", projectProperties.name },
                { "guidLegalEntity", projectProperties.guidLegalEntity },
                { "publicationdate", projectProperties.GetPublicationDate() },
                { "reportingperiod", projectProperties.reportingPeriod }
            };
            var projectStructureCreationResult = await CallTaxxorDataService<TaxxorReturnMessage>(RequestMethodEnum.Put, "cmsprojectmanagement", createProjectDataToPost, true);

            // Return an error message if we failed to create the structure
            if (!projectStructureCreationResult.Success) return projectStructureCreationResult;




            // => Return a success message
            return new TaxxorReturnMessage(true, "Successfully setup the new project", $"projectProperties.projectId: {projectProperties.projectId}");
        }


        /// <summary>
        /// Handles a project setup error by first attempting to cleanup traces in the Taxxor Data Store and then throwing the error
        /// </summary>
        /// <param name="newProjectId"></param>
        /// <param name="baseMessage"></param>
        /// <param name="baseDebugInfo"></param>
        /// <returns></returns>
        private static async Task _HandleProjectCreateError(string newProjectId, string baseMessage, string baseDebugInfo)
        {
            // Reset the global variable
            ProjectCreationInProgress = false;

            // Cleanup everything that we have potentially setup on the server
            var projectDeleteResult = await DeleteProject(newProjectId, true, false);

            // Define the debuginfo message
            var errorDebugInfo = $"{baseDebugInfo}, stack-trace: {GetStackTrace()}";

            if (projectDeleteResult.Success)
            {
                HandleError(ReturnTypeEnum.Json, $"{baseMessage}, but all traces have been cleaned up", errorDebugInfo);
            }
            else
            {
                appLogger.LogWarning($"Full delete of project structures on the Taxxor Data Store failed. projectDeleteResult: {projectDeleteResult.ToString()}");
                HandleError(ReturnTypeEnum.Json, $"{baseMessage} and there are potentially traces left on the Taxxor Data Store", errorDebugInfo);
            }
        }

    }
}