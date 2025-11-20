using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharpReverseProxy;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Registers the routes for the "sandbox" test scripts and redirects those the controllers
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public IRouter UtilitiesRouter(IApplicationBuilder app)
        {
            var routeBuilder = new RouteBuilder(app);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Special routes that we need to proxy to an underlying application
            // TODO: the ID of this application needs to change to "taxxorstructureddatastore"
            var nodePluginEdgarFiler = xmlApplicationConfiguration.SelectSingleNode("//service[@id='taxxoredgarfiler']/uri");
            if (nodePluginEdgarFiler != null)
            {
                var urlEdgarFiler = CalculateFullPathOs(nodePluginEdgarFiler);

                app.UseProxy(new List<ProxyRule>
                    {
                        // => Edgar filer
                        new ProxyRule
                        {
                            Matcher = uri => uri.AbsoluteUri.Contains("/utilities/edgarfiler"),
                                Modifier = (req, user) =>
                                {
                                    // Console.WriteLine("IN STRUCTURED DATA STORE PROXY");

                                    // Calculate the path including the querystring that we need to proxy
                                    var subPathAndQuery = req.RequestUri.PathAndQuery.Replace("/utilities/edgarfiler", "");

                                    if (subPathAndQuery == "")
                                    {
                                        req.RequestUri = new Uri(urlEdgarFiler);
                                    }
                                    else
                                    {
                                        req.RequestUri = new Uri($"{urlEdgarFiler}{subPathAndQuery}");
                                    }

                                },
                                RequiresAuthentication = false
                        }
                    },
                    r =>
                    {
                        // Optionally log the URL we are using
                        if (UriLogEnabled && r.ProxyStatus == ProxyStatus.Proxied)
                        {
                            var logUri = GenerateLogUri(r.ProxiedUri);
                            if (!UriLogBackend.Contains(logUri)) UriLogBackend.Add(logUri);
                        }

                        // Limit logging of the proxied elements to only the API paths
                        if (r.OriginalUri.ToString().Contains("/utilities"))
                        {
                            if (r.ProxyStatus == ProxyStatus.Proxied)
                            {
                                if (r.HttpStatusCode > 200)
                                {
                                    appLogger.LogError($"* Proxied Url (Utilities.cs): {r.ProxiedUri.AbsoluteUri}, Status: {r.HttpStatusCode}, Original Url: {r.OriginalUri}, Time: {r.Elapsed}");
                                }
                                else
                                {
                                    if (debugRoutine) appLogger.LogDebug($"* Proxied Url (Utilities.cs): {r.ProxiedUri.AbsoluteUri}, Status: {r.HttpStatusCode}, Original Url: {r.OriginalUri}, Time: {r.Elapsed}");
                                }
                            }
                        }

                    }
                );
            }


            //
            // Dynamically build the routes to that only "dynamic" content is catched and processed by C# controllers - all other content will be served by the static webserver
            //
            var routeTemplates = CreateRouteTemplatePaths("utilities");

            // Console.WriteLine(@"---------------- Start routeTemplates ----------------");
            // Console.WriteLine($"{string.Join("\n", routeTemplates)}");
            // Console.WriteLine(@"---------------- End routeTemplates ----------------");

            // GET routes
            foreach (var routeTemplate in routeTemplates)
            {
                routeBuilder.MapGet(routeTemplate, UtilitiesDispatcher);
            }

            // POST routes
            foreach (var routeTemplate in routeTemplates)
            {
                routeBuilder.MapPost(routeTemplate, UtilitiesDispatcher);
            }

            return routeBuilder.Build();
        }
    }


}