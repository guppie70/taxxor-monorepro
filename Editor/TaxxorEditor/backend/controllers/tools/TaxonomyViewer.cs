using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Renders the Taxonomy Viwer Page by pulling it from the static assets server
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RenderTaxonomyViewer(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);

            var debugRoutine = false;


            var domainType = (siteType == "prod") ? "prod" : "prev";
            var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{domainType}']");
            var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
            var protocol = (nodeCurrentDomain.GetAttribute("ssl") == "true") ? "https" : "http";
            var originUrl = $"{protocol}://{currentDomainName}";

            string? staticAssetsLocation;

            if (context.Session.TryGetValue("staticassetslocation", out byte[]? staticAssetsLocationBytes))
            {
                staticAssetsLocation = System.Text.Encoding.UTF8.GetString(staticAssetsLocationBytes);
            }
            else
            {
                staticAssetsLocation = null;
            }

            var taxonomyViewerUri = ((staticAssetsLocation == null) ? StaticAssetsLocation : staticAssetsLocation) + "/taxonomy-viewer/index.html";

            string taxoViewerHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <title>{reqVars.pageTitle}</title>
        <style>
            body, html {{
                margin: 0;
                padding: 0;
                width: 100%;
                height: 100%;
                overflow: hidden;
            }}
            iframe {{
                width: 100%;
                height: 100%;
                border: none;
            }}
        </style>
    </head>
    <body>
        <iframe src=""{taxonomyViewerUri}"" frameborder=""0""></iframe>
    </body>
</html>";

            // Commented out HttpClient code - may be needed for future use
            // using (var httpClient = new HttpClient())
            // {
            //     try
            //     {
            //         httpClient.DefaultRequestHeaders.Add("Origin", originUrl);
            //         httpClient.Timeout = TimeSpan.FromSeconds(5);
            //         if (debugRoutine)
            //         {
            //             Console.WriteLine("--- STATIC ASSETS METADATA ---");
            //             Console.WriteLine($"- originUrl: {originUrl}");
            //             Console.WriteLine($"- reguestUrl: {StaticAssetsLocation}/meta.json");
            //         }
            //
            //
            //         taxoViewerHtml = await httpClient.GetStringAsync($"{StaticAssetsLocation}/taxonomy-viewer/index.html");
            //
            //
            //         if (debugRoutine) Console.WriteLine("- taxoViewerHtml:\n" + taxoViewerHtml);
            //
            //
            //
            //         if (debugRoutine) Console.WriteLine("------------------------------");
            //     }
            //     catch (HttpRequestException ex)
            //     {
            //         // Handle server unreachable or file not found
            //         appLogger.LogWarning($"Could not retrieve taxonomy viewer page: {ex.Message}");
            //     }
            //     catch (TaskCanceledException ex)
            //     {
            //         // Handle timeout
            //         appLogger.LogWarning($"Timeout while retrieving taxonomy viewer page: {ex.Message}");
            //     }
            //     catch (Exception ex)
            //     {
            //         // Handle any other unexpected errors
            //         appLogger.LogError(ex, $"Error retrieving taxonomy viewer page: {ex.Message}");
            //     }
            // }

            if (debugRoutine)
            {
                Console.WriteLine("--- STATIC ASSETS METADATA ---");
                Console.WriteLine($"- originUrl: {originUrl}");
                Console.WriteLine($"- iframeUrl: {StaticAssetsLocation}/taxonomy-viewer/index.html");
                Console.WriteLine($"- taxoViewerHtml:\n" + taxoViewerHtml);
                Console.WriteLine("------------------------------");
            }

            await response.OK(taxoViewerHtml, reqVars.returnType, true);

        }

    }
}