using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Links incoming HTTP calls to API routes to C# controllers based on the Page ID from site_structure.xml
        /// </summary>
        /// <returns>The pages.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task DispatchTools(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Console.WriteLine($"*********** In Tools Dispatcher with PageID: '{reqVars.pageId}' ***********");

            if (!reqVars.isStaticAsset)
            {
                switch (reqVars.pageId)
                {

                    case "generatejsvars":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await GenerateJsVariables();
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "u-cms_tools-view-session":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ViewSessionInformation(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxonomyviewer":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderTaxonomyViewer(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;



                    default:
                        // Start custom logic
                        bool foundControllerToProcess = await Extensions.RenderPageLogicByPageIdCustom(context, reqVars.pageId);

                        if (!foundControllerToProcess)
                        {
                            HandleError(reqVars, $"Could not process this request", $"DispatchTools(): Could not find controller for page with id '{reqVars.pageId}'", 404);
                        }
                        break;

                }
            }
            else
            {
                HandleError(reqVars, $"No static content available", $"DispatchTools(): The request for a static asset '{request.Path}' cannot be served because the path is reserved for dynamic page content");
            }
        }

    }
}