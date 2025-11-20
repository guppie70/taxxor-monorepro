using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// Removes a new project from the Taxxor Document Store
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Removes a new project from the Taxxor Document Store
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task DeleteProject(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            if (projectVars.currentUser.IsAuthenticated)
            {
                var projectIdToDelete = request.RetrievePostedValue("id", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
                var removeSdeInformationString = request.RetrievePostedValue("removesdeinformation", RegexEnum.Boolean, false, ReturnTypeEnum.Json, "false");
                var removeSdeInformation = (removeSdeInformationString == "true");

                var projectDeleteResult = await DeleteProject(projectIdToDelete, removeSdeInformation);

                // Reset the global variable
                ProjectCreationInProgress = false;

                // Render the response
                if (projectDeleteResult.Success)
                {
                    await response.OK(projectDeleteResult, ReturnTypeEnum.Json, true);
                }
                else
                {
                    HandleError(ReturnTypeEnum.Json, projectDeleteResult);
                }
            }
            else
            {
                HandleError(reqVars.returnType, "Not authenticated", $"stack-trace: {CreateStackInfo()}", 403);
            }
        }

        /// <summary>
        /// Removes a project from the Taxxor Document Store
        /// </summary>
        /// <param name="projectIdToDelete"></param>
        /// <param name="removeSdeInformation"></param>
        /// <param name="strictErrorHandling"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> DeleteProject(string projectIdToDelete, bool removeSdeInformation, bool strictErrorHandling = true)
        {
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


            try
            {
                // Create data to post                            
                var deleteProjectDataToPost = new Dictionary<string, string>();
                deleteProjectDataToPost.Add("id", projectIdToDelete);

                // Remove all the project structures in the Taxxor Data Store
                bool projectStructureDeleteResult = await CallTaxxorDataService<bool>(RequestMethodEnum.Delete, "cmsprojectmanagement", deleteProjectDataToPost, true);

                // Delete the resource ID's from the Taxxor Access Control Service
                bool resourcesDeleteResult = await Taxxor.ConnectedServices.AccessControlService.DeleteResources(projectIdToDelete);

                // Handle errors
                if (!projectStructureDeleteResult)
                {
                    // Reset the global variable
                    ProjectCreationInProgress = false;

                    return new TaxxorReturnMessage(false, "Project delete failed", $"Error removing the project structure. projectIdToDelete: {projectIdToDelete}, stack-trace: {GetStackTrace()}");
                }
                if (!resourcesDeleteResult && strictErrorHandling)
                {
                    // Reset the global variable
                    ProjectCreationInProgress = false;

                    return new TaxxorReturnMessage(false, "Project delete failed", $"Error removing the resources from the RBAC service. projectIdToDelete: {projectIdToDelete}, stack-trace: {GetStackTrace()}");
                }

                // Delete all mapping clusters for this project
                if (removeSdeInformation)
                {
                    var xmlMappingClusterDeleteResult = await MappingService.DeleteAllMappingClusters(projectIdToDelete, true);
                    if (XmlContainsError(xmlMappingClusterDeleteResult))
                    {
                        var mappingClusterDeleteResult = new TaxxorReturnMessage(xmlMappingClusterDeleteResult);
                        if (mappingClusterDeleteResult.DebugInfo.ToLower().Contains("http status code: 404") && mappingClusterDeleteResult.DebugInfo.ToLower().Contains("does not exist"))
                        {
                            // Assume that there were no mapping clusters for this project in the StructuredDataStore
                            appLogger.LogWarning($"Unable to delete mapping clusters because there were no mapping clusters defined for this project ({projectIdToDelete})");
                        }
                        else
                        {
                            // Reset the global variable
                            ProjectCreationInProgress = false;

                            return new TaxxorReturnMessage(xmlMappingClusterDeleteResult);
                        }

                        // appLogger.LogError($"Unable to delete mapping clusters. Message: {xmlMappingClusterDeleteResult.SelectSingleNode("//message")?.InnerText?? ""}, DebugInfo: {xmlMappingClusterDeleteResult.SelectSingleNode("//debuginfo")?.InnerText?? ""}");
                    }
                }
                else
                {
                    appLogger.LogInformation("Skipping SDE information removal");
                }


                // Update the information in Application Configuration with the new content of Data Configuration
                TaxxorReturnMessage updateApplicationConfigResult = await UpdateDataConfigurationInApplicationConfiguration();
                if (!updateApplicationConfigResult.Success)
                {
                    // Reset the global variable
                    ProjectCreationInProgress = false;

                    return new TaxxorReturnMessage(false, "Update application configuration failed", $"updateApplicationConfigResult: {GenerateDebugObjectString(updateApplicationConfigResult)}, stack-trace: {GetStackTrace()}");
                }

                //
                // => Update the local CMS metadata object
                //
                await UpdateCmsMetadata();

                //
                // => Clear the user section information cache data
                //
                UserSectionInformationCacheData.Clear();

                // Reset the global variable
                ProjectCreationInProgress = false;

                // Return success message
                return new TaxxorReturnMessage(true, "Successfully removed project", $"projectIdToDelete: {projectIdToDelete}");
            }
            catch (Exception ex)
            {
                // Reset the global variable
                ProjectCreationInProgress = false;

                appLogger.LogError(ex, "There was an error deleting the project");
                return new TaxxorReturnMessage(false, "Unable to delete project completely", ex.Message);
            }

        }

    }


}