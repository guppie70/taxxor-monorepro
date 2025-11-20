using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Registers the routes for the "sandbox" test scripts and redirects those the controllers
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public IRouter SandboxRouter(IApplicationBuilder app)
        {
            var routeBuilder = new RouteBuilder(app);

            //
            // Dynamically build the routes to that only "dynamic" content is catched and processed by C# controllers - all other content will be served by the static webserver
            //
            var routeTemplates = CreateRouteTemplatePaths("sandbox");

            //Console.WriteLine(@"---------------- Start routeTemplates ----------------");
            //Console.WriteLine($"{string.Join("\n", routeTemplates)}");
            //Console.WriteLine(@"---------------- End routeTemplates ----------------");

            // GET routes
            foreach (var routeTemplate in routeTemplates)
            {
                routeBuilder.MapGet(routeTemplate, DispatchSandboxTestScripts);
            }

            // POST routes
            foreach (var routeTemplate in routeTemplates)
            {
                routeBuilder.MapPost(routeTemplate, DispatchSandboxTestScripts);
            }

            return routeBuilder.Build();
        }



    }


}
