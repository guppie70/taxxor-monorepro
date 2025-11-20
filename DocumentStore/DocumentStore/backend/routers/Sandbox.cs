using System;
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

            var baseFolder = "sandbox";

            // Routes
            routeBuilder.MapGet(baseFolder, DispatchSandboxTestScripts);
            routeBuilder.MapGet(baseFolder + "/{*subpath}", DispatchSandboxTestScripts);
            routeBuilder.MapPost(baseFolder, DispatchSandboxTestScripts);
            routeBuilder.MapPost(baseFolder + "/{*subpath}", DispatchSandboxTestScripts);
            routeBuilder.MapPut(baseFolder, DispatchSandboxTestScripts);
            routeBuilder.MapPut(baseFolder + "/{*subpath}", DispatchSandboxTestScripts);
            routeBuilder.MapDelete(baseFolder, DispatchSandboxTestScripts);
            routeBuilder.MapDelete(baseFolder + "/{*subpath}", DispatchSandboxTestScripts);


            return routeBuilder.Build();
        }



    }


}
