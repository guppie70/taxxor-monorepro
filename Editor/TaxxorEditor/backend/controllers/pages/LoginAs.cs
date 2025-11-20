using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Routine that makes it possible to login as another user by storing an alternative user ID in a session parameter
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task LoginAs(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            // To please the compiler
            await DummyAwaiter();

            // Retrieve posted values
            var forcedUserId = request.RetrievePostedValue("userid", RegexEnum.Email, true, ReturnTypeEnum.Html);
            var finalReloadFlag = request.RetrievePostedValue("final", @"^(true|false)$", false, ReturnTypeEnum.Html, "false");

            // Clear any previously stored session and cookie data
            RemoveSessionCompletely(context, false);

            // Reload the page if we have not been called with the final=true parameter
            if (finalReloadFlag == "false")
            {
                response.Redirect($"{reqVars.thisUrlPath}?final=true&userid={forcedUserId}");
            }
            else
            {
                // Store the forced user id in a session
                context.Session.SetString("forceduserid", forcedUserId);

                // Redirect
                response.Redirect(RetrieveUrlFromHierarchy("cms-overview", reqVars.xmlHierarchyStripped));
            }
        }
    }
}