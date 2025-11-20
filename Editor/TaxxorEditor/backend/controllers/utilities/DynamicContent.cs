using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Executes logic based on the method parameter that was used to access the anonymous "dynamic content" route with
        /// </summary>
        /// <returns>The anonymous dynamic content request.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task HandleAnonymousDynamicContentRequest(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);


            string? method = request.RetrievePostedValue("method", true);
            string? type = request.RetrievePostedValue("type", true);
            string? elementId = request.RetrievePostedValue("elid");

            ReturnTypeEnum returnType = GetReturnTypeEnum(type);

            switch (method)
            {
                case "retrievetoken":
                    await response.OK("{\"message\" : \"success\", \"token\" : \"" + context.Session.GetString("sessionToken") + "\"}", returnType, true);
                    break;
                default:
                    HandleError("Unable to process request", $"Method value '{method}' not found");
                    break;
            }


        }

        /// <summary>
        /// Executes logic based on the method parameter that was used to access the anonymous "dynamic content" route with
        /// </summary>
        /// <returns>The authenticated dynamic content request.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task HandleAuthenticatedDynamicContentRequest(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            Contract.Ensures(Contract.Result<Task>() != null);

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);


            string? method = request.RetrievePostedValue("method", true);
            string? type = request.RetrievePostedValue("type", true);
            string? elementId = request.RetrievePostedValue("elid");


            ReturnTypeEnum returnType = GetReturnTypeEnum(type);

            switch (method)
            {

                case "downloadxbrl":
                    var xbrlFileName = request.RetrievePostedValue("filename", RegexEnum.FileName, true, reqVars.returnType);
                    await DownloadFile(xbrlFileName, "xbrlinstance");
                    break;

                case "downloadpdfgenerator":
                    var pdfFileName = request.RetrievePostedValue("filename", RegexEnum.FileName, true, reqVars.returnType);
                    await DownloadFile(pdfFileName, "pdfcompare");
                    break;

                case "emailadministrator":
                    //EmailAdministrators(request.RetrievePostedValue("subject", RegexEnum.Default, true, reqVars.returnType), request.RetrievePostedValue("body", RegexEnum.None, true, reqVars.returnType));
                    break;

                case "retrieveuserpreferencekey":
                    string? defaultUserPreferenceValue = request.RetrievePostedValue("value", @"^[a-zA-Z_\-!\?@#',\:\.\s\d:\/=\}\{""}]{1,1024}$", false);
                    string? userPreferenceValue = projectVars.currentUser.RetrieveUserPreferenceKey(request.RetrievePostedValue("key", RegexEnum.Default, true, reqVars.returnType), defaultUserPreferenceValue);

                    // Render response
                    dynamic jsonResult = new ExpandoObject();
                    jsonResult.message = "success";
                    jsonResult.value = userPreferenceValue;
                    string json = WrapJsonPIfNeeded(ConvertToJson(jsonResult));
                    await response.OK(json, ReturnTypeEnum.Json, true);

                    break;

                case "deleteuserpreferencekey":
                    if (await projectVars.currentUser.DeleteUserPreferenceKey(request.RetrievePostedValue("key", RegexEnum.Default, true, reqVars.returnType)))
                    {
                        // Success
                        await response.OK("{\"message\":\"success\"}", ReturnTypeEnum.Json, true);
                    }
                    else
                    {
                        HandleError(ReturnTypeEnum.Json, "Could not delete user preference key", $"key: {request.RetrievePostedValue("key")}, stack-trace: {GetStackTrace()}");
                    }
                    break;
                case "updateuserpreferencekey":
                    if (await projectVars.currentUser.UpdateUserPreferenceKey(request.RetrievePostedValue("key", RegexEnum.Default, true, reqVars.returnType), request.RetrievePostedValue("value", @"^[a-zA-Z_\-!\?@#',\:\.\s\d:\/=\}\{""}]{1,1024}$", true, reqVars.returnType)))
                    {
                        // Success
                        await response.OK("{\"message\":\"success\"}", ReturnTypeEnum.Json, true);
                    }
                    else
                    {
                        HandleError(ReturnTypeEnum.Json, "Could not update user preference key", $"key: {request.RetrievePostedValue("key")}, value: {request.RetrievePostedValue("value")}, stack-trace: {GetStackTrace()}");
                    }
                    break;

                default:
                    HandleError("Unable to process request", $"Method value '{method}' not found");
                    break;
            }

        }

    }
}