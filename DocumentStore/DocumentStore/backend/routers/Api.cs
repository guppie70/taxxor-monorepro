using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework {

        /// <summary>
        /// RRegisters the routes for the API external interface and redirects those the controllers
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public IRouter ApiRouter(IApplicationBuilder app) {
            var routeBuilder = new RouteBuilder(app);

            var baseFolder = "api";

            // Routes
            routeBuilder.MapGet(baseFolder, DispatchApi);
            routeBuilder.MapGet(baseFolder + "/{*subpath}", DispatchApi);
            routeBuilder.MapPost(baseFolder, DispatchApi);
            routeBuilder.MapPost(baseFolder + "/{*subpath}", DispatchApi);
            routeBuilder.MapPut(baseFolder, DispatchApi);
            routeBuilder.MapPut(baseFolder + "/{*subpath}", DispatchApi);
            routeBuilder.MapDelete(baseFolder, DispatchApi);
            routeBuilder.MapDelete(baseFolder + "/{*subpath}", DispatchApi);

            return routeBuilder.Build();
        }

    }
}