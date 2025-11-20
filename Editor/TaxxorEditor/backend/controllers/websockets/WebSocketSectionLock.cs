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
        /// Section locks using websocket connection
        /// </summary>
        public partial class WebSocketsHub : Hub
        {

            /// <summary>
            /// Retrieves information about locked sections in the Editor
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/listsectionlocks")]
            public async Task<TaxxorReturnMessage> ListSectionLocks(string jsonData)
            {
                var errorMessage = "There was an error listing the section locks";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");
                    // generateVersions = true;

                    // Handle security
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }
                    else
                    {
                        // Retrieve the posted data by converting JSON to XML
                        var xmlDataPosted = ConvertJsonToXml(jsonData, "data");
                        // if (debugRoutine) Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                        // Extract the posted data
                        var sectionId = _extractPostedXmlValue(xmlDataPosted, "pageid");
                        var pageType = _extractPostedXmlValue(xmlDataPosted, "pagetype");

                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add(sectionId, RegexEnum.Default, true);
                        inputValidationCollection.Add(pageType, @"^(page|filing)$", true);
                        var validationResult = inputValidationCollection.Validate();
                        if (!validationResult.Success)
                        {
                            appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                            return validationResult;
                        }
                        else
                        {
                            // Fill the request and project variables in the context with the posted values
                            var renderProjectVarsResult = await ProjectVariablesFromXml(xmlDataPosted, false);
                            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                            // if (debugRoutine) Console.WriteLine(projectVars.DumpToString());

                            // Retrieve the lock information
                            var listLocksResult = ListLocks(projectVars, pageType.Value);

                            // Console.WriteLine(listLocksResult.ToString());

                            return listLocksResult;
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Retrieves information about locked sections in the Editor
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editlocks")]
            public async Task<TaxxorReturnMessage> SetSectionLock(string jsonData)
            {
                var errorMessage = "There was an error setting the lock";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");
                    // generateVersions = true;

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    // Retrieve the posted data by converting JSON to XML
                    var xmlDataPosted = ConvertJsonToXml(jsonData, "data");
                    // if (debugRoutine) Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");


                    // Extract the posted data
                    var sectionId = _extractPostedXmlValue(xmlDataPosted, "pageid");
                    var pageType = _extractPostedXmlValue(xmlDataPosted, "pagetype");


                    // Validate the data
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add(sectionId, RegexEnum.Default, true);
                    inputValidationCollection.Add(pageType, @"^(page|filing)$", true);
                    var validationResult = inputValidationCollection.Validate();

                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    // Fill the request and project variables in the context with the posted values
                    var renderProjectVarsResult = await ProjectVariablesFromXml(xmlDataPosted, true);
                    if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");



                    // if (debugRoutine) Console.WriteLine(projectVars.DumpToString());

                    // Create the lock
                    var lockResult = FilingLockStore.AddLock(projectVars, sectionId.Value, pageType.Value);

                    // Send a message to all connected clients about the new lock
                    // if(lockResult.Success){
                    //     var listLocksResult = ListLocks(projectVars, pageType.Value);
                    //     await Clients.All.SendAsync("UpdateSectionLocks", listLocksResult);
                    // }

                    return lockResult;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Removes a lock from the locking system
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editlocks")]
            public async Task<TaxxorReturnMessage> RemoveLock(string jsonData)
            {
                var errorMessage = "There was an error setting the lock";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");
                    // generateVersions = true;

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    // Retrieve the posted data by converting JSON to XML
                    var xmlDataPosted = ConvertJsonToXml(jsonData, "data");
                    // if (debugRoutine) Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                    // Extract the posted data
                    var sectionId = _extractPostedXmlValue(xmlDataPosted, "pageid");
                    var pageType = _extractPostedXmlValue(xmlDataPosted, "pagetype");

                    // Validate the data
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add(sectionId, @"^[a-zA-Z_\-!\?@#':=,\.\s\d:\/\(\)\p{IsCJKUnifiedIdeographs}]{1,256}$", true);
                    inputValidationCollection.Add(pageType, @"^(page|filing|hierarchy)$", true);
                    var validationResult = inputValidationCollection.Validate();

                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    // Fill the request and project variables in the context with the posted values
                    var renderProjectVarsResult = await ProjectVariablesFromXml(xmlDataPosted, false);
                    if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                    if (string.IsNullOrEmpty(sectionId.Value))
                    {
                        FilingLockStore.RemoveLocksForUser(projectVars.currentUser.Id, pageType.Value);
                    }
                    else
                    {
                        FilingLockStore.RemoveLockForPage(projectVars, sectionId.Value, pageType.Value);
                    }

                    // if (debugRoutine) Console.WriteLine(projectVars.DumpToString());

                    // Create the lock
                    return new TaxxorReturnMessage(true, "Successfully removed lock(s)");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }





        }

    }

}