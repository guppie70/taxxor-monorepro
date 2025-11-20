using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Registers the routes for the authentication process
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public IRouter AuthenticationRouter(IApplicationBuilder app)
        {
            var routeBuilder = new RouteBuilder(app);

            routeBuilder.MapGet("{filename:regex(^(login|loginas|logout|logoutfinal|oauthcallback)$)}", AuthenticationDispatcher);

            return routeBuilder.Build();
        }

    }
}
