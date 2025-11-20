using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves details of a footnote from the Taxxor Document Store
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task SendSupportEmail(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var renderError = true;

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var emailContent = context.Request.RetrievePostedValue("emailcontent", RegexEnum.TextArea, true, reqVars.returnType);

            if (renderError)
            {
                // Generate a standard error XmlDocument
                var xmlError = GenerateErrorXml("Thrown on purpose", $"stack-trace: {GetStackTrace()}");

                // Render an error response - the content that will be returned to the client depends on the value of reqVars.returnType -> which is dynamically determined using content negotiation
                await context.Response.Error(xmlError, reqVars.returnType, true);
            }
            else
            {
                // Generate a standard success XmlDocument
                var xmlSuccess = GenerateSuccessXml("Success", $"Some additional information that will only be transmitted on non-production systems");
                
                // Render an error response - the content that will be returned to the client depends on the value of reqVars.returnType -> which is dynamically determined using content negotiation
                await context.Response.OK(xmlSuccess, reqVars.returnType, true);
            }
        }

    }

}