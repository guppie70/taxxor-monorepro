using System.Dynamic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves output channel and section information
        /// </summary>
        /// <returns>The anonymous dynamic content request.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task RetrieveOutputChannelInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var dataReference = request.RetrievePostedValue("dataref", RegexEnum.FileName, true, reqVars.returnType);

            // Retrieve hierarchy overview
            var xmlHierarchyOverview = await RenderOutputChannelHierarchyOverview(false, true);

            // Find the first item in all the hierarchies that matches the data reference that we are looking for
            var nodeItem = xmlHierarchyOverview.SelectSingleNode($"/hierarchies/output_channel/items//item[@data-ref='{dataReference}']");

            if (nodeItem == null)
            {
                HandleError("Could not locate output channel information", $"dataReference: {dataReference}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                // Retrieve output channel variant id
                var nodeOutputChannel = nodeItem.SelectSingleNode("ancestor-or-self::output_channel");
                var outputChannelVariantId = nodeOutputChannel.GetAttribute("id");

                // Retrieve the url of the editor
                var baseUrl = RetrieveUrlFromHierarchy("cms_content-editor", reqVars.xmlHierarchyStripped);

                // Retrieve the site structure item id
                var itemId = nodeItem.GetAttribute("id");

                // Render a response object
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully retrieved output channel variant id";
                jsonData.result.ocvariantid = outputChannelVariantId;
                jsonData.result.did = itemId;
                jsonData.result.baseuri = baseUrl;

                var json = (string) ConvertToJson(jsonData);
                await response.OK(json, ReturnTypeEnum.Json, true);
            }
        }

    }
}