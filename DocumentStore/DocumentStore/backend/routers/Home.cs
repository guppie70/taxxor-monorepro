using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework
    {
        public IRouter HomePageRouter(IApplicationBuilder app)
        {
            var routeBuilder = new RouteBuilder(app);

            // Route to render the service description displaying the details about this web service
			routeBuilder.MapGet("", RenderServiceDescription);

            return routeBuilder.Build();
        }


    }
}