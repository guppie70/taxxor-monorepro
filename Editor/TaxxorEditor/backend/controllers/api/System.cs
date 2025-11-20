using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Attempts to rest the SignalR connection
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ResetSignalR(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var errorMessage = "There was an error resetting the SignalR connection";
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            try
            {

                IHubContext<WebSocketsHub> hubContext;
                if (context != null)
                {
                    hubContext = context.RequestServices.GetRequiredService<IHubContext<WebSocketsHub>>();
                }
                else
                {
                    hubContext = System.Web.SignalRHub.Current;
                }



                 var result = new TaxxorReturnMessage(true, "Successfully reset SignalR connection");
                await response.OK(result, ReturnTypeEnum.Json, true);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, errorMessage);
                await response.Error(new TaxxorReturnMessage(false, errorMessage, ex.ToString()), ReturnTypeEnum.Json, true);
            }
        }

    }

}