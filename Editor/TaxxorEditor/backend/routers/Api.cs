using System;
using System.Collections.Generic;
// using AspNetCore.Proxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharpReverseProxy;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// RRegisters the routes for the API external interface and redirects those the controllers
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public IRouter ApiRouter(IApplicationBuilder app)
        {
            var routeBuilder = new RouteBuilder(app);

            var baseFolder = "api";
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Special routes that we need to proxy to an underlying application
            // TODO: the ID of this application needs to change to "taxxorstructureddatastore"
            var nodeStructuredDataServiceUrl = xmlApplicationConfiguration.SelectSingleNode("//service[@id='taxxordatastore']/uri");
            var urlStructuredDataStore = CalculateFullPathOs(nodeStructuredDataServiceUrl);

            var nodeMappingServiceUrl = xmlApplicationConfiguration.SelectSingleNode("//service[@id='taxxormappingservice']/uri");
            var urlMappingService = CalculateFullPathOs(nodeMappingServiceUrl);

            app.UseProxy(new List<ProxyRule>
                {
                    // => Mapping Service
                    new ProxyRule
                    {
                        Matcher = uri => uri.AbsoluteUri.Contains("/api/mappingservice/proxy"),
                            Modifier = (req, user) =>
                            {
                                // Console.WriteLine("IN MAPPING SERVICE PROXY");

                                // Calculate the path including the querystring that we need to proxy
                                var subPathAndQuery = req.RequestUri.PathAndQuery.Replace("/api/mappingservice/proxy", "");

                                if (subPathAndQuery == "")
                                {
                                    req.RequestUri = new Uri(urlMappingService);
                                }
                                else
                                {
                                    req.RequestUri = new Uri($"{urlMappingService}{subPathAndQuery}");
                                }

                            },
                            RequiresAuthentication = false
                    },
                    // => Structured Data Store
                    new ProxyRule
                    {
                        Matcher = uri => uri.AbsoluteUri.Contains("/api/structureddatastore/proxy"),
                            Modifier = (req, user) =>
                            {
                                // Console.WriteLine("IN STRUCTURED DATA STORE PROXY");

                                // Calculate the path including the querystring that we need to proxy
                                var subPathAndQuery = req.RequestUri.PathAndQuery.Replace("/api/structureddatastore/proxy", "");

                                if (subPathAndQuery == "")
                                {
                                    req.RequestUri = new Uri(urlStructuredDataStore);
                                }
                                else
                                {
                                    req.RequestUri = new Uri($"{urlStructuredDataStore}{subPathAndQuery}");
                                }

                            },
                            RequiresAuthentication = false
                    },


                    // => Test proxy                 
                    new ProxyRule
                    {
                        Matcher = uri => uri.AbsoluteUri.Contains("/api/proxy/sandbox"),
                            Modifier = (req, user) =>
                            {
                                var context = System.Web.Context.Current;
                                RequestVariables reqVars = RetrieveRequestVariables(context);
                                ProjectVariables projectVars = RetrieveProjectVariables(context);

                                req.RequestUri = new Uri("http://localhost:4813/api/configuration/taxxorservices");
                                // For the Philips case -> attempt to modify the request header to pass the Shibboleth user along
                                req.Headers.Add("current-user-id", projectVars.currentUser.Id);
                                req.Headers.Remove("Cookie");

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
                    if (r.OriginalUri.ToString().Contains("/api"))
                    {
                        if (r.ProxyStatus == ProxyStatus.Proxied)
                        {


                            if (r.HttpStatusCode > 200)
                            {
                                appLogger.LogError($"* Proxied Url (Api.cs): {r.ProxiedUri.AbsoluteUri}, Status: {r.HttpStatusCode}, Original Url: {r.OriginalUri}, Time: {r.Elapsed}");
                            }
                            else
                            {
                                if (debugRoutine) appLogger.LogDebug($"* Proxied Url (Api.cs): {r.ProxiedUri.AbsoluteUri}, Status: {r.HttpStatusCode}, Original Url: {r.OriginalUri}, Time: {r.Elapsed}");
                            }
                        }
                    }



                }
            );

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