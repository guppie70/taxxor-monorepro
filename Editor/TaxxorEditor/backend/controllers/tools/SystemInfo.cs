using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Generates the content for the about modal dialog
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task AboutTaxxor(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);

            XsltArgumentList xsltArgumentList = new XsltArgumentList();
            xsltArgumentList.AddParam("type", "", "about");

            var xmlApplicationConfigurationCloned = new XmlDocument();
            xmlApplicationConfigurationCloned.ReplaceContent(xmlApplicationConfiguration);

            // Try to add the cached version of the repository information to the application configuration
            var repositoriesCachePathOs = $"{applicationRootPathOs}/_repro-info.xml";
            if (File.Exists(repositoriesCachePathOs))
            {
                try
                {
                    var xmlCachedRepositoryInfo = new XmlDocument();
                    xmlCachedRepositoryInfo.Load(repositoriesCachePathOs);
                    var nodeImported = xmlApplicationConfigurationCloned.ImportNode(xmlCachedRepositoryInfo.DocumentElement, true);
                    xmlApplicationConfigurationCloned.DocumentElement.AppendChild(nodeImported);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Could not append cached repository info information to the XML document. {repositoriesCachePathOs}");
                }

            }

            await response.OK(TransformXml(xmlApplicationConfigurationCloned, CalculateFullPathOs("cms_xsl_taxxor-info"), xsltArgumentList), reqVars.returnType, true);
        }

        /// <summary>
        /// Generates the content for the status modal dialog
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ConnectedServices(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            XsltArgumentList xsltArgumentList = new XsltArgumentList();
            xsltArgumentList.AddParam("type", "", "status");
            var displayDetails = "no";
            if (projectVars.currentUser.Permissions.ViewDeveloperTools) displayDetails = "yes";
            xsltArgumentList.AddParam("displaydetails", "", displayDetails);


            await response.OK(TransformXml(xmlApplicationConfiguration, CalculateFullPathOs("cms_xsl_taxxor-info"), xsltArgumentList), reqVars.returnType, true);
        }

    }
}