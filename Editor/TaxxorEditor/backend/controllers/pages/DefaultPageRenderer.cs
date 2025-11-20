using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {


        public async static Task DefaultPageRenderer(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            string frontendVersion = request?.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-FrontendVersion") ?? "1.0";

            // Create the HTML of this page using the template resolver
            switch (frontendVersion)
            {
                case "2.0":
                    // Dynamically retrieve the base HTML from the new frontend webserver
                    var uriNewFrontend = "https://editorfrontend:4832/";

                    try
                    {
                        var textFileContents = await RetrieveTextFile(uriNewFrontend, true);

                        response.StatusCode = 200;
                        response.ContentType = "text/html";
                        await response.WriteAsync(textFileContents ?? "");
                    }
                    catch (Exception ex)
                    {
                        HandleError("Failed to retieve HTML from the frontend service", ex.ToString());
                    }


                    break;

                default:
                    await RenderTaxxorEditorPage(reqVars.pageId);
                    break;
            }


        }


    }
}