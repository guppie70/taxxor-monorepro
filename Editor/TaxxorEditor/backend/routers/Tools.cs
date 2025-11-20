using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework {

        /// <summary>
        /// Registers the routes for the tools and redirects those to the dispatcher
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public IRouter ToolsRouter(IApplicationBuilder app) {
            var routeBuilder = new RouteBuilder(app);

            //
            // Dynamically build the routes to that only "dynamic" content is catched and processed by C# controllers - all other content will be served by the static webserver
            //
            var routeTemplates = CreateRouteTemplatePaths("tools");

            // GET routes
            foreach (var routeTemplate in routeTemplates)
            {
                routeBuilder.MapGet(routeTemplate, DispatchTools);
            }

            // POST routes
            foreach (var routeTemplate in routeTemplates)
            {
                routeBuilder.MapPost(routeTemplate, DispatchTools);
            }

            return routeBuilder.Build();
        }

    }
}