using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Links routes for user authentication related pages to C# controllers based on the Page ID from site_structure.xml
        /// </summary>
        /// <returns>The dispatcher.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task AuthenticationDispatcher(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);


            if (!reqVars.isStaticAsset)
            {
                switch (reqVars.pageId)
                {
                    case "ulogindotnet":
                        // Render the login page
                        await DefaultPageRenderer(request, response, routeData);
                        break;

                    case "ulogout-dotnet":
                        // Render the logout page
                        await RenderLogoutPage(request, response, routeData);
                        break;

                    case "ulogout-dotnet-final":
                        // Render the logout page
                        await RenderLogoutPageFinal(request, response, routeData);
                        break;    

                    case "uloginas":
                        await LoginAs(request, response, routeData);
                        break;
                }
            }
            else
            {
                HandleError(reqVars, $"No static content available", $"AuthenticationDispatcher(): The request for a static asset '{request.Path}' cannot be served because the path is reserved for dynamic page content");
            }
        }

    }
}