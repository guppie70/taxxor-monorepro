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
        /// Special router for proxy-ing requests to Taxxor webservices
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public IRouter ProxyRouter(IApplicationBuilder app)
        {
            var routeBuilder = new RouteBuilder(app);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Special routes that we need to proxy to an underlying application
            var nodeStructuredDataServiceUrl = xmlApplicationConfiguration.SelectSingleNode("//service[@id='taxxordatastore']/uri");
            var urlStructuredDataStore = CalculateFullPathOs(nodeStructuredDataServiceUrl);

            var nodeMappingServiceUrl = xmlApplicationConfiguration.SelectSingleNode("//service[@id='taxxormappingservice']/uri");
            var urlMappingService = CalculateFullPathOs(nodeMappingServiceUrl);

            app.UseProxy(new List<ProxyRule>
                {

                    // => Mapping Service
                    new ProxyRule
                    {
                        Matcher = uri => uri.AbsoluteUri.Contains("/proxy/mappingservice"),
                            Modifier = (req, user) =>
                            {
                                // Console.WriteLine("IN MAPPING SERVICE PROXY");

                                // Calculate the path including the querystring that we need to proxy
                                var subPathAndQuery = req.RequestUri.PathAndQuery.Replace("/proxy/mappingservice", "");

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
                        Matcher = uri => uri.AbsoluteUri.Contains("/proxy/structureddatastore"),
                            Modifier = (req, user) =>
                            {
                                // Console.WriteLine("IN STRUCTURED DATA STORE PROXY");

                                // Calculate the path including the querystring that we need to proxy
                                var subPathAndQuery = req.RequestUri.PathAndQuery.Replace("/proxy/structureddatastore", "");

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
                    if (r.OriginalUri.ToString().Contains("/proxy"))
                    {
                        if (r.ProxyStatus == ProxyStatus.Proxied)
                        {
                            if (r.HttpStatusCode > 200)
                            {
                                appLogger.LogError($"* Proxied Url (Proxy.cs): {r.ProxiedUri.AbsoluteUri}, Status: {r.HttpStatusCode}, Original Url: {r.OriginalUri}, Time: {r.Elapsed}");
                            }
                            else
                            {
                                if (debugRoutine) appLogger.LogDebug($"* Proxied Url (Proxy.cs): {r.ProxiedUri.AbsoluteUri}, Status: {r.HttpStatusCode}, Original Url: {r.OriginalUri}, Time: {r.Elapsed}");
                            }
                        }
                    }

                }
            );

            return routeBuilder.Build();
        }

    }
}