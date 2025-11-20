using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Links incoming HTTP calls to sandbox test functionality to C# controllers based on the Page ID from site_structure.xml
        /// </summary>
        /// <returns>The sandbox test scripts.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task DispatchSandboxTestScripts(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var executeMethod = siteType != "prod" && siteType != "prev";
            executeMethod = projectVars.currentUser.Permissions.All;
            // Console.WriteLine($"*********** In Sandbox Dispatcher with PageID: '{reqVars.pageId}' and executeMethod: {executeMethod} ***********");

            if (!reqVars.isStaticAsset)
            {
                switch (reqVars.pageId)
                {
                    case "sandbox-generic":

                        if (executeMethod)
                        {


                            switch (reqVars.method)
                            {
                                case RequestMethodEnum.Get:
                                    // await SandboxPostProcessTrackChanges(request, response, routeData);
                                    // await SandboxCloneMappingDb(request, response, routeData);
                                    await SandboxPostProcessDataImport(request, response, routeData);
                                    break;

                                case RequestMethodEnum.Post:
                                    // await SandboxTestMaxPostedLength(request, response, routeData);
                                    break;

                                default:
                                    _handleMethodNotSupported(reqVars);
                                    break;
                            }
                            break;




                        }
                        else
                        {
                            HandleError("Not found", null, 404);
                        }
                        break;

                    case "sandbox-rbac":
                        if (executeMethod)
                        {
                            // await SandboxGetErpImportStatus(request, response, routeData);
                            // await SandboxFixRbacContent(request, response, routeData);
                        }
                        else
                        {
                            HandleError("Not found", null, 404);
                        }
                        break;

                    case "sandbox-tool":
                        if (executeMethod)
                        {

                            await SandboxRetrieveTool(request, response, routeData);
                            // await SandboxChangeTaxesPaidMappingCluster(request, response, routeData);
                        }
                        else
                        {
                            HandleError("Not found", null, 404);
                        }
                        break;


                    default:
                        // Start custom logic
                        bool foundControllerToProcess = await Extensions.RenderPageLogicByPageIdCustom(context, reqVars.pageId);

                        if (!foundControllerToProcess)
                        {
                            HandleError(reqVars, $"Could not process this request", $"DispatchSandboxTestScripts(): Could not find controller for page with id '{reqVars.pageId}'", 404);
                        }

                        break;

                }
            }
            else
            {
                HandleError(reqVars, $"No static content available", $"DispatchSandboxTestScripts(): The request for a static asset '{request.Path}' cannot be served because the path is reserved for dynamic page content");
            }
        }


    }
}