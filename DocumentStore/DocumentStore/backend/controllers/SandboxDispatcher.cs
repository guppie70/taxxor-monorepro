using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Xml;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.IO;

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

            // Console.WriteLine($"*********** In Sandbox Dispatcher with PageID: '{reqVars.pageId}' ***********");

            if (!reqVars.isStaticAsset)
            {
                switch (reqVars.pageId)
                {
                    case "sandbox-root":
                        // await SandboxRestoreFileFromGit(request, response, routeData);
                        // await SandboxGenerateDummyCatLargeOverviewTable(request, response, routeData);
                        break;

                    case "sandbox-requestok":  
                        routeData.Values.Add("type", Path.GetFileName(reqVars.thisUrlPath));
                        await RenderTestResponseSandbox(request, response, routeData);
                        break;

                    case "sandbox-requesterror":
                        routeData.Values.Add("type", Path.GetFileName(reqVars.thisUrlPath));
                        await RenderTestResponseSandbox(request, response, routeData);
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