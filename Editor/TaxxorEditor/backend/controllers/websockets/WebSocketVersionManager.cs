using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Auditor view websocket methods
        /// </summary>
        public partial class WebSocketsHub : Hub
        {

            /// <summary>
            /// Retrieves the SDE sync log
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveSdeSyncLog(string projectId, string commitHash)
            {
                var debugRoutine = siteType == "local" || siteType == "dev";

                // Centralized error messsage
                var errorMessage = "There was an error retrieving the SDE sync log";

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                try
                {
                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }
                    else
                    {
                        if (SystemState.ErpImport.IsRunning || SystemState.SdsSync.IsRunning)
                        {
                            var returnMessage = new TaxxorReturnMessage(false, "There is already an ERP import or SDS data sync in progress. Please wait until these have been completed again later", SystemState.ToString());
                            appLogger.LogError(returnMessage.ToString());
                            return returnMessage;
                        }
                        else
                        {

                            // Validate the data
                            var inputValidationCollection = new InputValidationCollection();
                            inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                            inputValidationCollection.Add("commitHash", commitHash, RegexEnum.HashOrTag, true);
                            var validationResult = inputValidationCollection.Validate();
                            if (!validationResult.Success)
                            {
                                appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                                return validationResult;
                            }
                            else
                            {
                                // Fill the request and project variables in the context with the posted values
                                var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
                                if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                                var sdeLogResult = await RenderSdeSyncLog(reqVars, projectVars, commitHash);
                                if (!sdeLogResult.Success) return sdeLogResult;

                                return new TaxxorReturnMessage(true, "Successfully rendered SDE sync report", sdeLogResult.Payload, $"projectId: {projectId}, commiitHash: {commitHash}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, (projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : "");
                }
            }




            /// <summary>
            /// Retrieves the ERP data import log
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveErpImportLog(string projectId, string commitHash)
            {
                var debugRoutine = siteType == "local" || siteType == "dev";

                // Centralized error messsage
                var errorMessage = "There was an error retrieving the ERP import log";

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                try
                {
                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }
                    else
                    {
                        if (SystemState.ErpImport.IsRunning || SystemState.SdsSync.IsRunning)
                        {
                            var returnMessage = new TaxxorReturnMessage(false, "There is already an ERP import or SDS data sync in progress. Please wait until these have been completed again later", SystemState.ToString());
                            appLogger.LogError(returnMessage.ToString());
                            return returnMessage;
                        }
                        else
                        {

                            // Validate the data
                            var inputValidationCollection = new InputValidationCollection();
                            inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                            inputValidationCollection.Add("commitHash", commitHash, RegexEnum.HashOrTag, true);
                            var validationResult = inputValidationCollection.Validate();
                            if (!validationResult.Success)
                            {
                                appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                                return validationResult;
                            }
                            else
                            {
                                // Fill the request and project variables in the context with the posted values
                                var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
                                if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                                // Retrieve file version_1/textual/logs/sde-synclog.xml from the repository
                                var gitLocationId = "reportdataroot";
                                var restoreFilingContentDataResult = await Taxxor.ConnectedServices.DocumentStoreService.VersionControl.GitExtractSingleFile(projectVars.projectId, gitLocationId, commitHash, $"version_1/textual/logs/import-erp.json", null, debugRoutine);
                                if (!restoreFilingContentDataResult.Success)
                                {
                                    appLogger.LogError($"{errorMessage}, restoreFilingContentDataResult.Message: {restoreFilingContentDataResult.Message}, restoreFilingContentDataResult.DebugInfo: {restoreFilingContentDataResult.DebugInfo}");
                                    return restoreFilingContentDataResult;
                                }
                                else
                                {
                                    var erpRunDetails = System.Text.Json.JsonSerializer.Deserialize<ErpImportRunDetails>(restoreFilingContentDataResult.Payload);

                                    // Construct a log message to show in the UI
                                    var logContent = string.Join('\n', erpRunDetails.Log);

                                    // Find the two timestamps in the log (format [yyyy-MM-dd HH:mm:ss]) and replace it with the epoch start and end times
                                    

                                    return new TaxxorReturnMessage(true, "Successfully retrieved ERP import log", logContent, $"projectId: {projectId}, commitHash: {commitHash}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, (projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : "");
                }
            }








        }



    }

}