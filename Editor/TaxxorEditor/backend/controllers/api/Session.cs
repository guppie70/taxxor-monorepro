using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Returns session ID to the client
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task DisplaySessionInfo(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            var sessionId = context.Session.Id ?? "null";
            var returnMessage = new TaxxorReturnMessage(true, "success", sessionId);

            switch (reqVars.returnType)
            {
                case ReturnTypeEnum.Html:
                    var htmlResponse = $"<html><head><meta charset=\"utf-8\"><title>Session info</title></head><body>{returnMessage.ToString()}</body></html>";
                    await context.Response.OK(htmlResponse, ReturnTypeEnum.Html, true);
                    break;

                default:
                    await context.Response.OK(returnMessage, reqVars.returnType, true);
                    break;
            }
        }

    }

}