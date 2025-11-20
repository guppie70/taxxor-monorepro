using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Registers the routes for the CMS pages and redirects those to the dispatcher
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public IRouter PagesRouter(IApplicationBuilder app)
        {
            var routeBuilder = new RouteBuilder(app);

            // Homepage
            routeBuilder.MapGet("", DispatchPages);

            //
            // Dynamically build the routes to that only "dynamic" content is catched and processed by C# controllers - all other content will be served by the static webserver
            //
            var routeReportEditorTemplates = CreateRouteTemplatePaths("report_editors");

            //Console.WriteLine(@"---------------- Start routeTemplates ----------------");
            //Console.WriteLine($"{string.Join("\n", routeReportEditorTemplates)}");
            //Console.WriteLine(@"---------------- End routeTemplates ----------------");

            // GET routes
            foreach (var routeTemplate in routeReportEditorTemplates)
            {
                routeBuilder.MapGet(routeTemplate, DispatchPages);
            }

            // POST routes
            foreach (var routeTemplate in routeReportEditorTemplates)
            {
                routeBuilder.MapPost(routeTemplate, DispatchPages);
            }

            var routePageTemplates = CreateRouteTemplatePaths("pages");

            //Console.WriteLine(@"---------------- Start routeTemplates ----------------");
            //Console.WriteLine($"{string.Join("\n", routePageTemplates)}");
            //Console.WriteLine(@"---------------- End routeTemplates ----------------");

            // GET routes
            foreach (var routeTemplate in routePageTemplates)
            {
                routeBuilder.MapGet(routeTemplate, DispatchPages);
            }

            // POST routes
            foreach (var routeTemplate in routePageTemplates)
            {
                routeBuilder.MapPost(routeTemplate, DispatchPages);
            }

            var routePublicationsTemplates = CreateRouteTemplatePaths("publications");
            
            // GET routes
            foreach (var routeTemplate in routePublicationsTemplates)
            {
                routeBuilder.MapGet(routeTemplate, DispatchPages);
            }

            return routeBuilder.Build();
        }

    }
}