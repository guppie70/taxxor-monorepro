using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with user preferences and data
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves the data for the current Taxxor user, combines it in one XmlDocument and returns it to the client
        /// </summary>
        /// <returns>The user data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        // MIGRATED - CAN BE REMOVED
        public static async Task RetrieveUserData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            
            // Call the core logic method
            var result = await RetrieveUserDataCore(reqVars);
            
            // Return the response
            if (result.Success)
            {
                await response.OK(result.XmlResponse, reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
        }

        /// <summary>
        /// Core logic for retrieving user data without HTTP dependencies
        /// </summary>
        /// <param name="reqVars">Request variables</param>
        /// <returns>User data result</returns>
        public static async Task<UserDataResult> RetrieveUserDataCore(RequestVariables reqVars)
        {
            var result = new UserDataResult();
            ProjectVariables projectVars = RetrieveProjectVariables(System.Web.Context.Current);
            
            try
            {
                var userPreferenceUsable = true;
                var profileRoutine = (siteType == "local" || siteType == "dev");
    
                // Will contain all the user information available
                var xmlUserInformation = new XmlDocument();
    
                if (profileRoutine) ProfilerStart("Start retrieving user data", true, ReturnTypeEnum.Txt);
                
                if (projectVars.currentUser.IsAuthenticated)
                {
                    if (CreateUserDataContainer())
                    {
                        if (profileRoutine) ProfilerMeasure("After create container", ReturnTypeEnum.Txt);
                        
                        // Load the preferences
                        var userPrefsFilePathOs = projectVars.cmsUserDataRootPathOs + "/" + Path.GetFileName(CalculateFullPathOs("user_preferences_template"));
                        if (File.Exists(userPrefsFilePathOs))
                        {
                            projectVars.currentUser.XmlUserPreferences.Load(userPrefsFilePathOs);
                        }
                        else
                        {
                            userPreferenceUsable = false;
                            WriteErrorMessageToConsole("Could not load the user preference data", userPrefsFilePathOs + " does not exist");
                        }
                        if (profileRoutine) ProfilerMeasure("After load user preferences", ReturnTypeEnum.Txt);
                    }
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "Not authenticated";
                    result.DebugInfo = $"stack-trace: {CreateStackInfo()}";
                    result.StatusCode = 403;
                    return result;
                }
    
                if (profileRoutine)
                {
                    ProfilerStop("End retrieving user data", ReturnTypeEnum.Txt);
                    TextFileCreate(reqVars.profilingResult.ToString(), logRootPathOs + "/profiling/user-data_" + reqVars.pageId + ".txt");
                }
    
                // Load basic XML structure that we want to return to the client
                xmlUserInformation.LoadXml($@"<userdata userid=""{projectVars.currentUser.Id}""><preferences/><reportingrequirements/><requirementscache/></userdata>");
    
                // Add the user preferences
                if (userPreferenceUsable)
                {
                    var userPrefNode = xmlUserInformation.ImportNode(projectVars.currentUser.XmlUserPreferences.DocumentElement, true);
                    xmlUserInformation.SelectSingleNode("/userdata/preferences").AppendChild(userPrefNode);
                }
    
                // Set the success result
                result.Success = true;
                result.XmlResponse = xmlUserInformation;
                
                await Task.CompletedTask; // To satisfy the async requirement
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error retrieving user data";
                result.DebugInfo = $"Error: {ex.Message}, Stack trace: {GetStackTrace()}";
                return result;
            }
        }

        /// <summary>
        /// Updates or creates the user data.
        /// </summary>
        /// <returns>The user data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        // MIGRATED - CAN BE REMOVED
        public static async Task SetUserData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Retrieve request and project variables
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            
            // Retrieve posted values
            string type = request.RetrievePostedValue("type", RegexEnum.Default, true, reqVars.returnType);
            string key = request.RetrievePostedValue("key", RegexEnum.Default, false, reqVars.returnType);
            string value = request.RetrievePostedValue("value", RegexEnum.None, true, reqVars.returnType);
            
            // Call the core logic method
            var result = await SetUserDataCore(type, key, value, reqVars);
            
            // Return the response
            if (result.Success)
            {
                await response.OK(result.XmlResponse, reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
        }
        
        /// <summary>
        /// Core logic for setting user data without HTTP dependencies
        /// </summary>
        /// <param name="type">Type of user data to set</param>
        /// <param name="key">Key for data</param>
        /// <param name="value">Value to store</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>User data result</returns>
        public static async Task<UserDataResult> SetUserDataCore(
            string type, 
            string key, 
            string value, 
            RequestVariables reqVars)
        {
            var result = new UserDataResult();
            ProjectVariables projectVars = RetrieveProjectVariables(System.Web.Context.Current);
            
            try
            {
                // Response document
                var xmlResponse = new XmlDocument();
    
                if (!CreateUserDataContainer())
                {
                    result.Success = false;
                    result.ErrorMessage = "Could not create the user data folder";
                    result.DebugInfo = $"projectVars.currentUser.Id = {projectVars.currentUser.Id}, stack-trace: {GetStackTrace()}";
                    return result;
                }
    
                var userPrefsTempFolderPathOs = projectVars.cmsUserDataRootPathOs + "/temp";
    
                switch (type)
                {
                    case "reportingrequirement":
                    case "reportingrequirementscache":
                        var xmlReportingRequirements = new XmlDocument();
    
                        var reportingRequirementPathOs = userPrefsTempFolderPathOs + "/" + key + ".xml";
                        if (type == "reportingrequirementscache") reportingRequirementPathOs = CalculateFullPathOs("lextra_cache", reqVars);
    
                        try
                        {
                            xmlReportingRequirements.LoadXml(value);
                        }
                        catch (Exception ex)
                        {
                            result.Success = false;
                            result.ErrorMessage = "Could not process posted data";
                            result.DebugInfo = $"SetUserData(), Error Details: {ex}, stack-trace: {GetStackTrace()}";
                            return result;
                        }
    
                        // Save the information on the disk
                        if (!await SaveXmlDocument(xmlReportingRequirements, reportingRequirementPathOs, "Stored new temporary data"))
                        {
                            result.Success = false;
                            result.ErrorMessage = "There was an error saving the reporting requirements in the user data folder";
                            result.DebugInfo = reportingRequirementPathOs;
                            return result;
                        }
                        else
                        {
                            xmlResponse = GenerateSuccessXml($"Sucessfully stored data", $"path: {reportingRequirementPathOs}");
                        }
                        break;
    
                    case "userpreferences":
                        var xmlUserPreferences = new XmlDocument();
    
                        try
                        {
                            xmlUserPreferences.LoadXml(value);
                        }
                        catch (Exception ex)
                        {
                            result.Success = false;
                            result.ErrorMessage = "Could not process posted user preferences data";
                            result.DebugInfo = $"SetUserData(), Error Details: {ex}, stack-trace: {GetStackTrace()}";
                            return result;
                        }
    
                        var userPreferencesPathOs = CalculateFullPathOs("user_preferences", reqVars);
    
                        if (!await SaveXmlDocument(xmlUserPreferences, userPreferencesPathOs, "Stored user preferences data"))
                        {
                            result.Success = false;
                            result.ErrorMessage = "There was an error saving the user preferences in the user data folder";
                            result.DebugInfo = userPreferencesPathOs;
                            return result;
                        }
                        else
                        {
                            xmlResponse = GenerateSuccessXml($"Sucessfully stored user preferences data", $"path: {userPreferencesPathOs}");
                        }
    
                        break;
    
                    default:
                        result.Success = false;
                        result.ErrorMessage = "No action defined for this type";
                        result.DebugInfo = $"type: '{type}' could not be mapped in SetUserData()";
                        return result;
                }
    
                result.Success = true;
                result.XmlResponse = xmlResponse;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error setting user data";
                result.DebugInfo = $"Error: {ex.Message}, Stack trace: {GetStackTrace()}";
                return result;
            }
        }

        /// <summary>
        /// Creates a location to store user data in if needed
        /// </summary>
        public static bool CreateUserDataContainer()
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            bool success = false;

            // A) Test if the user preferences folder structure exists
            var userPrefsFolderPathOs = dataRootPathOs + "/users/" + projectVars.currentUser.Id;
            var userPrefsTempFolderPathOs = userPrefsFolderPathOs + "/temp";
            if (Directory.Exists(userPrefsTempFolderPathOs))
            {
                success = true;
            }
            else
            {
                // Create a user prefs folder and copy the default prefs document in
                try
                {
                    Directory.CreateDirectory(userPrefsTempFolderPathOs);
                }
                catch (Exception ex)
                {
                    WriteErrorMessageToConsole("Could not create user preference directory", $"userPrefsFolderPathOs: {userPrefsFolderPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }

            // B) Test if the user preference file exists
            var templateFilePathOs = CalculateFullPathOs("user_preferences_template");
            var targetFilePathOs = userPrefsFolderPathOs + "/" + Path.GetFileName(templateFilePathOs);
            if (File.Exists(targetFilePathOs))
            {
                success = true;
            }
            else
            {
                try
                {
                    File.Copy(templateFilePathOs, targetFilePathOs, false);
                    success = true;
                }
                catch (Exception ex)
                {
                    WriteErrorMessageToConsole("Could not copy the user preferences template file", $"templateFilePathOs: {templateFilePathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }

            return success;
        }

        /// <summary>
        /// Retrieves temporary user data
        /// </summary>
        /// <returns>The user temp data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task RetrieveUserTempData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Retrieve request and project variables
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted data
            string key = request.RetrievePostedValue("key", RegexEnum.Default, true, reqVars.returnType);

            var userPrefsFolderPathOs = dataRootPathOs + "/users/" + projectVars.currentUser.Id;
            var userPrefsTempFolderPathOs = userPrefsFolderPathOs + "/temp";
            var pathOs = $"{userPrefsTempFolderPathOs}/{key}";
            string fileType = GetFileType(pathOs);

            if (!Directory.Exists(userPrefsTempFolderPathOs)) HandleError(reqVars, "Could not find user preference temp folder", $"userPrefsTempFolderPathOs: {userPrefsTempFolderPathOs}, stack-trace: {GetStackTrace()}");

            if (!File.Exists(pathOs)) HandleError(reqVars, "Could not find user preference temp file", $"pathOs: {pathOs}, stack-trace: {GetStackTrace()}", 424);

            try
            {
                // Create a standard XML format that contains the content of the file in an encoded format
                var xmlResponse = await CreateTaxxorXmlEnvelopeForFileTransport(pathOs);

                // Stick the file content in the message field of the success xml and the file path into the debuginfo node
                await response.OK(xmlResponse, reqVars.returnType, true);
            }
            catch (Exception ex)
            {
                HandleError(reqVars, "There was an error retrieving the temporary user data", $"pathOs: {pathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Stores temporary user data
        /// </summary>
        /// <returns>The user temp data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task StoreUserTempData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Retrieve request and project variables
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted data
            string key = request.RetrievePostedValue("key", RegexEnum.Default, true, reqVars.returnType);
            string encoding = request.RetrievePostedValue("encoding", RegexEnum.Default.Value, false, reqVars.returnType, "utf-8");
            string value = request.RetrievePostedValue("value", RegexEnum.None, true, reqVars.returnType);

            var userPrefsFolderPathOs = dataRootPathOs + "/users/" + projectVars.currentUser.Id;
            var userPrefsTempFolderPathOs = userPrefsFolderPathOs + "/temp";
            var pathOs = $"{userPrefsTempFolderPathOs}/{key}";
            string fileType = GetFileType(pathOs);

            if (!Directory.Exists(userPrefsTempFolderPathOs)) HandleError(reqVars, "Could not find user preference temp folder", $"key: {key}, encoding: {encoding}, value: {TruncateString(value, 100)}, userPrefsTempFolderPathOs: {userPrefsTempFolderPathOs}, stack-trace: {GetStackTrace()}");

            try
            {
                if (fileType == "text")
                {
                    TextFileCreate(value, pathOs);
                }
                else
                {
                    if (!Base64DecodeAsBinaryFile(value, pathOs)) HandleError(reqVars, "Could not store binary temp file", $"key: {key}, encoding: {encoding}, value: {TruncateString(value, 100)}, pathOs: {pathOs}, stack-trace: {GetStackTrace()}");
                }

                await response.OK(GenerateSuccessXml("Successfully stored temporary user file", pathOs), reqVars.returnType, true);
            }
            catch (Exception ex)
            {
                HandleError(reqVars, "There was an error storing the temporary user data", $"key: {key}, encoding: {encoding}, value: {TruncateString(value, 100)}, pathOs: {pathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Deleted the user temp data file.
        /// </summary>
        /// <returns>The user temp data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task DeleteUserTempData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Retrieve request and project variables
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted data
            string key = request.RetrievePostedValue("key", RegexEnum.Default, true, reqVars.returnType);

            var userPrefsFolderPathOs = dataRootPathOs + "/users/" + projectVars.currentUser.Id;
            var userPrefsTempFolderPathOs = userPrefsFolderPathOs + "/temp";
            var pathOs = $"{userPrefsTempFolderPathOs}/{key}";

            if (!Directory.Exists(userPrefsTempFolderPathOs)) HandleError(reqVars, "Could not find user preference temp folder", $"key: {key}, pathOs: {pathOs}, userPrefsTempFolderPathOs: {userPrefsTempFolderPathOs}, stack-trace: {GetStackTrace()}");

            if (!File.Exists(pathOs))
            {
                HandleError(reqVars, "Could not find user preference temp file to remove", $"key: {key}, pathOs: {pathOs}, userPrefsTempFolderPathOs: {userPrefsTempFolderPathOs}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                try
                {
                    File.Delete(pathOs);

                }
                catch (Exception ex)
                {
                    HandleError(reqVars, "Could not delete user preference temp file", $"error: {ex},key: {key}, pathOs: {pathOs}, userPrefsTempFolderPathOs: {userPrefsTempFolderPathOs}, stack-trace: {GetStackTrace()}");
                }
            }

            await response.OK(GenerateSuccessXml("Successfully deleted temporary user file", pathOs), reqVars.returnType, true);
        }

    }
    
    /// <summary>
    /// Result class for user data operations
    /// </summary>
    public class UserDataResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DebugInfo { get; set; }
        public int StatusCode { get; set; } = 500;
        public XmlDocument? XmlResponse { get; set; }
    }
}