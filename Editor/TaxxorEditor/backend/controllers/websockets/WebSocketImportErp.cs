using System;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
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
            /// Initiates an ERP data import
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/importerpdata/init")]
            public async Task<TaxxorReturnMessage> ImportErpDataInit(string projectId)
            {

                // Centralized error messsage
                var errorMessage = "There was an error importing ERP data";

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
                            var returnMessage = new TaxxorReturnMessage(false, "There is already an ERP import or SDS data sync in progress. Please wait until these have been completed and try again later", SystemState.ToString());
                            appLogger.LogError(returnMessage.ToString());
                            return returnMessage;
                        }
                        else
                        {

                            // Validate the data
                            var inputValidationCollection = new InputValidationCollection();
                            inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
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

                                // Retrieve the project name
                                var projectName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/name")?.InnerText ?? "unknown";

                                // Create the ERP import background task
                                var erpImportSettings = new ErpImportSettings(projectVars);
                                var erpRunDetails = new ErpImportRunDetails();
                                ErpImportStatus.TryAdd(erpImportSettings.RunId, erpRunDetails);

                                // Add a log line to the erp import status
                                await ErpImportStatus[erpImportSettings.RunId].AddLog($"[<span data-epochstart=\"{erpRunDetails.EpochStartTime}\">{GenerateLogTimestamp(false)}</span>]\nPreparing import EFR data task");

                                // Schedule the task so that is will be picked up by the scheduler
                                var task = TaskSettings.Create(erpImportSettings);
                                _tasksToRun.Enqueue(task);

                                // Send the result of the synchronization process back to the server
                                return new TaxxorReturnMessage(true, $"Started the ERP data import\n  * project name: '{projectName}'", ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"\n  * projectId: {projectId}" : "")); ;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await SystemState.ErpImport.Reset(true);

                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : ""));
                }
            }

            /// <summary>
            /// Retrieves the current state of an ERP import process
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/importerpdata/progress")]
            public async Task<TaxxorReturnMessage> ImportErpDataProgress(string projectId)
            {
                // Centralized base error message
                var errorMessage = $"There was an error retrieving the state of the ERP data import";

                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // Handle security
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }
                    else
                    {
                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
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

                            // Retrieve the name of the project we are importing for
                            var projectName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/name")?.InnerText ?? "unknown";


                            // Handle unexpected states
                            dynamic jsonPayloadError = new ExpandoObject();
                            jsonPayloadError.progress = 0;

                            if (!SystemState.ErpImport.IsRunning)
                            {
                                errorMessage = "No ERP import is running at the moment";
                                jsonPayloadError.log = $"ERROR: {errorMessage}";
                                return new TaxxorReturnMessage(false, "failed", (string)ConvertToJson(jsonPayloadError), errorMessage);
                            }

                            // Loop through the ErpImportStatus dictionary and determine the total progress of the import
                            double totalProgress = 0;
                            var importLogs = new StringBuilder();

                            foreach (var importLog in ErpImportStatus.Values)
                            {
                                var logStatus = string.Join("\n", importLog.Log);
                                totalProgress += importLog.Progress / ErpImportStatus.Count;
                                importLogs.AppendLine(logStatus);
                            }

                            dynamic jsonPayload = new ExpandoObject();
                            jsonPayload.log = importLogs.ToString();
                            jsonPayload.progress = $"{(int)(totalProgress * 100)}%";

                            var json = (string)ConvertToJson(jsonPayload);

                            return new TaxxorReturnMessage(true, "inprogress", json, $"projectId: {projectId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Make sure the system state is updated
                    SystemState.ErpImport.IsRunning = false;

                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, "failed", $"error: {ex}");
                }
            }
        }

    }

}