using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Lists all the GIT repositories available in the Taxxor CMS Toolset
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ListGitRepositories(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var activeServices = new List<string>();
            activeServices.Add("taxxoreditor");
            activeServices.Add("documentstore");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted data
            var scope = context.Request.RetrievePostedValue("scope", @"^(all|devpackages)$", true, reqVars.returnType);

            if (scope == "devpackages") activeServices.Remove("documentstore");

            var xmlGitRepositories = TransformXmlToDocument(xmlApplicationConfiguration, "cms_git-repositories");

            // Only use the active services
            var xPath = "/services/service[not(";
            for (int i = 0; i < activeServices.Count; i++)
            {
                string activeServiceId = activeServices[i];
                xPath += $"id='{activeServiceId}'";
                if (i < (activeServices.Count - 1)) xPath += " or ";
            }
            xPath += ")]";
            // Console.WriteLine(xPath);
            RemoveXmlNodes(xmlGitRepositories.SelectNodes(xPath));

            // Check of the repositories can be used
            var nodeListServices = xmlGitRepositories.SelectNodes("/services/service");
            foreach (XmlNode nodeService in nodeListServices)
            {
                var serviceId = nodeService.SelectSingleNode("id")?.InnerText?? "";

                // Filter out the items in the list that are not active GIT repositories that you can actually do something with (when building Docker images, we sometimes do not include the .git directories in it)
                var nodeListRepros = nodeService.SelectNodes("repro");
                foreach (XmlNode nodeRepro in nodeListRepros)
                {
                    var reproId = nodeRepro.SelectSingleNode("id")?.InnerText?? "";
                    if (serviceId == "taxxoreditor")
                    {
                        var proceed = (scope == "all") ? true : (reproId.StartsWith("devpack") || reproId.StartsWith("custom-frontend"));

                        if (!proceed || !GitIsActiveRepository(reproId))
                        {
                            // appLogger.LogDebug($"Removing repository with ID {reproId}");
                            RemoveXmlNode(nodeRepro);
                        }
                    }
                    else
                    {
                        var dataToPost = new Dictionary<string, string>();
                        dataToPost.Add("pid", projectVars.projectId);
                        dataToPost.Add("location", reproId);
                        XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "gitactive", dataToPost, debugRoutine);

                        var activeRemoteRepository = true;
                        if (XmlContainsError(responseXml))
                        {
                            activeRemoteRepository = false;
                        }
                        else
                        {
                            var result = responseXml.SelectSingleNode("//message")?.InnerText?? "false";
                            activeRemoteRepository = (result == "true") ? true : false;
                        }

                        if (!activeRemoteRepository)
                        {
                            // appLogger.LogDebug($"Removing repository with ID {reproId}");
                            RemoveXmlNode(nodeRepro);
                        }
                    }
                }
            }

            // Remove any services that do not have a repository
            RemoveXmlNodes(xmlGitRepositories.SelectNodes("/services/service[not(repro)]"));


            if (debugRoutine)
            {
                Console.WriteLine("*******");
                Console.WriteLine(PrettyPrintXml(xmlGitRepositories));
                Console.WriteLine("*******");
            }

            var jsonToReturn = ConvertToJson(xmlGitRepositories, Newtonsoft.Json.Formatting.Indented, true);

            // Render the response
            await response.OK(jsonToReturn, reqVars.returnType, true);
        }

    }

}