using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Exposes the system state
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveSystemState(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);

            // Render the response
            switch (reqVars.returnType)
            {
                case ReturnTypeEnum.Xml:
                    await response.OK(SystemState.ToXml(), reqVars.returnType, true);
                    break;

                case ReturnTypeEnum.Json:
                    await response.OK(SystemState.ToJson(), reqVars.returnType, true);
                    break;

                default:
                    HandleError("Data type not supported");
                    break;
            }
        }

        /// <summary>
        /// Sets the system state based from an incoming request from a remote server
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task SetSystemState(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);

            // Retrieve posted data
            // Retrieve the (xml) data that we want to store
            var dataToStore = request.RetrievePostedValue("data", RegexEnum.None, true, ReturnTypeEnum.Xml);

            var xmlData = new XmlDocument();
            xmlData.LoadXml(dataToStore);

            // Update the system state
            SystemState.FromXml(xmlData);

            var message = new TaxxorReturnMessage(true, "Successfully updated the system state object");

            await response.OK(message, reqVars.returnType, true);
        }

    }


}

