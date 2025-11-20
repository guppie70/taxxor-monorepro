using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with management of projects for the Taxxor Editor
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Generates a list containing all the projects available in the Taxxor Document Store
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        // MIGRATED - CAN BE REMOVED
        public static async Task ListAvailableProjects(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Call the core logic method
            var result = await ListAvailableProjectsCore(projectVars, reqVars);
            
            // Return the response
            if (result.Success)
            {
                await context.Response.OK(result.XmlResponse, reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars.returnType, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
        }
        
        /// <summary>
        /// Core logic for listing available projects without HTTP dependencies
        /// </summary>
        /// <param name="projectVars">Project variables</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>Project list result</returns>
        public static async Task<ProjectResult> ListAvailableProjectsCore(
            ProjectVariables projectVars,
            RequestVariables reqVars)
        {
            var result = new ProjectResult();
            var debugRoutine = (siteType == "local" || siteType == "dev");
            
            await DummyAwaiter();
            
            try
            {
                if (!projectVars.currentUser.IsAuthenticated)
                {
                    result.Success = false;
                    result.ErrorMessage = "Not authenticated";
                    result.DebugInfo = $"stack-trace: {GetStackTrace()}";
                    result.StatusCode = 403;
                    return result;
                }

                // Render a list of projects
                var xmlProjects = TransformXmlToDocument(xmlApplicationConfiguration, "cms_xsl_list-projects");

                // Dump the result for inspection
                if (debugRoutine) xmlProjects.Save($"{logRootPathOs}/list-projects.xml");

                // Create success response
                result.Success = true;
                result.XmlResponse = GenerateSuccessXml(
                    "Sucessfully received project listing",
                    $"projectVars.projectId: {projectVars.projectId}",
                    HttpUtility.HtmlEncode(xmlProjects.OuterXml));

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error listing available projects";
                result.DebugInfo = $"Error: {ex.Message}, Stack trace: {GetStackTrace()}";
                return result;
            }
        }

        /// <summary>
        /// Creates a project structure
        /// </summary>
        /// <returns>The project.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        // MIGRATED - CAN BE REMOVED
        public static async Task CreateProjectStructure(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted data
            var reportType = request.RetrievePostedValue("rtype", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var newProjectId = request.RetrievePostedValue("id", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var newProjectName = request.RetrievePostedValue("name", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var reportingPeriod = request.RetrievePostedValue("reportingperiod", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var guidLegalEntity = request.RetrievePostedValue("guidLegalEntity");
            var guidCalendarEvent = request.RetrievePostedValue("guidCalendarEvent");
            var publicationDate = request.RetrievePostedValue("publicationdate", RegexEnum.isodate, false, ReturnTypeEnum.Xml, null);
            
            // Call the core logic method
            var result = await CreateProjectStructureCore(
                reportType, newProjectId, newProjectName, reportingPeriod, 
                guidLegalEntity, guidCalendarEvent, publicationDate, 
                projectVars, reqVars);
            
            // Return the response
            if (result.Success)
            {
                await context.Response.OK(result.XmlResponse, reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars.returnType, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
        }
        
        /// <summary>
        /// Core logic for creating project structure without HTTP dependencies
        /// </summary>
        /// <param name="reportType">Report type</param>
        /// <param name="newProjectId">New project ID</param>
        /// <param name="newProjectName">New project name</param>
        /// <param name="reportingPeriod">Reporting period</param>
        /// <param name="guidLegalEntity">GUID legal entity</param>
        /// <param name="guidCalendarEvent">GUID calendar event</param>
        /// <param name="publicationDate">Publication date</param>
        /// <param name="projectVars">Project variables</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>Project result</returns>
        public static async Task<ProjectResult> CreateProjectStructureCore(
            string reportType,
            string newProjectId,
            string newProjectName,
            string reportingPeriod,
            string guidLegalEntity,
            string guidCalendarEvent,
            string publicationDate,
            ProjectVariables projectVars,
            RequestVariables reqVars)
        {
            var result = new ProjectResult();
            
            try
            {
                if (!projectVars.currentUser.IsAuthenticated)
                {
                    result.Success = false;
                    result.ErrorMessage = "Not authenticated";
                    result.DebugInfo = $"stack-trace: {GetStackTrace()}";
                    result.StatusCode = 403;
                    return result;
                }
                
                // => Initiate a project properties object
                var projectProperties = new CmsProjectProperties(newProjectId);
                projectProperties.name = newProjectName;
                projectProperties.reportingPeriod = reportingPeriod;
                projectProperties.SetPublicationDate(publicationDate);
                projectProperties.reportTypeId = reportType;
                projectProperties.guidLegalEntity = guidLegalEntity;

                // Execute the code that creates the project
                TaxxorReturnMessage projectStructureCreateResult = await CreateNewProject(projectProperties);

                // Update the information in Application Configuration with the new content of Data Configuration
                TaxxorReturnMessage updateApplicationConfigResult = await UpdateDataConfigurationInApplicationConfiguration();

                // Prepare the result
                if (projectStructureCreateResult.Success && updateApplicationConfigResult.Success)
                {
                    result.Success = true;
                    result.XmlResponse = GenerateSuccessXml(
                        projectStructureCreateResult.Message, 
                        projectStructureCreateResult.DebugInfo);
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = projectStructureCreateResult.Message;
                    result.DebugInfo = $"projectStructureCreateResult.DebugInfo: {projectStructureCreateResult.DebugInfo}, updateApplicationConfigResult.DebugInfo: {updateApplicationConfigResult.DebugInfo}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error creating project structure";
                result.DebugInfo = $"Error: {ex.Message}, Stack trace: {GetStackTrace()}";
                return result;
            }
        }

        /// <summary>
        /// Removes a project (structure)
        /// </summary>
        /// <returns>The project.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        // MIGRATED - CAN BE REMOVED
        public static async Task DeleteProject(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var projectIdToDelete = context.Request.RetrievePostedValue("id", RegexEnum.None, true, ReturnTypeEnum.Xml);
            
            // Call the core logic method
            var result = await DeleteProjectCore(projectIdToDelete, projectVars, reqVars);
            
            // Return the response
            if (result.Success)
            {
                await context.Response.OK(result.XmlResponse, reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars.returnType, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
        }
        
        /// <summary>
        /// Core logic for deleting project without HTTP dependencies
        /// </summary>
        /// <param name="projectIdToDelete">Project ID to delete</param>
        /// <param name="projectVars">Project variables</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>Project result</returns>
        public static async Task<ProjectResult> DeleteProjectCore(
            string projectIdToDelete,
            ProjectVariables projectVars,
            RequestVariables reqVars)
        {
            var result = new ProjectResult();
            
            try
            {
                if (!projectVars.currentUser.IsAuthenticated)
                {
                    result.Success = false;
                    result.ErrorMessage = "Not authenticated";
                    result.DebugInfo = $"stack-trace: {GetStackTrace()}";
                    result.StatusCode = 403;
                    return result;
                }
                
                // Execute the code that removes the project
                TaxxorReturnMessage deleteProjectStructureResult = await ProjectDelete(projectIdToDelete);

                // Update the information in Application Configuration with the new content of Data Configuration
                TaxxorReturnMessage updateApplicationConfigResult = await UpdateDataConfigurationInApplicationConfiguration();

                // Prepare the result
                if (deleteProjectStructureResult.Success && updateApplicationConfigResult.Success)
                {
                    result.Success = true;
                    result.XmlResponse = GenerateSuccessXml(
                        deleteProjectStructureResult.Message, 
                        deleteProjectStructureResult.DebugInfo);
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = deleteProjectStructureResult.Message;
                    result.DebugInfo = $"deleteProjectStructureResult.DebugInfo: {deleteProjectStructureResult.DebugInfo}, updateApplicationConfigResult.DebugInfo: {updateApplicationConfigResult.DebugInfo}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error deleting project";
                result.DebugInfo = $"Error: {ex.Message}, Stack trace: {GetStackTrace()}";
                return result;
            }
        }
        
        /// <summary>
        /// Result class for project operations
        /// </summary>
        public class ProjectResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? DebugInfo { get; set; }
            public int StatusCode { get; set; } = 500;
            public XmlDocument? XmlResponse { get; set; }
        }
    }
}
