using System;
using System.Dynamic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Kicks off find and replace functionality
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task LogClientSideJavaScriptError(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var jsMessage = request.RetrievePostedValue("message", @"^(.*){0,1024}$", false, reqVars.returnType, "");
            var jsUrl = request.RetrievePostedValue("url", @"^(.*){0,1024}$", false, reqVars.returnType, "");
            var jsLine = request.RetrievePostedValue("line", @"^(.*){0,1024}$", false, reqVars.returnType, "");
            var pageUrl = request.RetrievePostedValue("pageurl", @"^(.*){0,1024}$", false, reqVars.returnType, "");

            try
            {
                // Dump the JS error in the log
                appLogger.LogError($"JSERROR: message: {jsMessage}");
                appLogger.LogError($"JSERROR: line: {jsLine}");
                appLogger.LogError($"JSERROR: script location: {jsUrl}");
                appLogger.LogError($"JSERROR: page url: {pageUrl}");
                appLogger.LogError($"JSERROR: userid: {projectVars.currentUser.Id}");

                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = $"Successfully stored error in log";

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
            catch (Exception ex)
            {
                // Return the error
                await response.Error(GenerateErrorXml("There was an error storing the JS error in the log", $"error: {ex}"), reqVars.returnType, true);
            }
        }

    }

}