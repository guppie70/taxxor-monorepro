using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using DocumentStore.Models;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Generic controller that retrieves a configuration file from this service and returns it to the client
        /// </summary>
        /// <returns>The xml configuration.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task RetrieveXmlConfiguration(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Fix this output to XML no matter what the content negotiation came up with
            reqVars.returnType = ReturnTypeEnum.Xml;

            // Retrieve the posted fileid from the route
            var configurationFileId = (string)routeData.Values["fileid"];
            
            // Call the core logic method
            var result = await RetrieveXmlConfigurationCore(configurationFileId, reqVars);
            
            // Return the response
            if (result.Success)
            {
                await response.OK(result.XmlResponse, ReturnTypeEnum.Xml, true);
            }
            else
            {
                HandleError(reqVars, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
        }

        /// <summary>
        /// Core logic for retrieving XML configuration without HTTP dependencies
        /// </summary>
        /// <param name="configurationFileId">Configuration file identifier</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>XML configuration result</returns>
        public async static Task<ConfigurationResult> RetrieveXmlConfigurationCore(
            string configurationFileId,
            RequestVariables reqVars)
        {
            var result = new ConfigurationResult();

            await DummyAwaiter();
            
            if (configurationFileId == null)
            {
                result.Success = false;
                result.ErrorMessage = "No valid configuration file ID found";
                result.DebugInfo = $"- configurationFileId: {configurationFileId}";
                result.StatusCode = 500;
                return result;
            }
            
            try
            {
                // Load the XML document
                var xmlConfiguration = new XmlDocument();
                var xmlConfigurationFilePathOs = CalculateFullPathOs(configurationFileId);
                
                if (string.IsNullOrEmpty(xmlConfigurationFilePathOs))
                {
                    result.Success = false;
                    result.ErrorMessage = "Could not find configuration file to serve";
                    result.DebugInfo = $"configurationFileId: '{configurationFileId}', stack-trace: {GetStackTrace()}";
                    result.StatusCode = 500;
                    return result;
                }
                
                xmlConfiguration.Load(xmlConfigurationFilePathOs);
                
                result.Success = true;
                result.XmlResponse = xmlConfiguration;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error loading configuration file";
                result.DebugInfo = $"configurationFileId: {configurationFileId}, error: {ex}, stack-trace: {GetStackTrace()}";
                result.StatusCode = 500;
                return result;
            }
        }

        /// <summary>
        /// Retrieves the cms temlates xml configuration file.
        /// </summary>
        /// <returns>The cms temlates xml configuration.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task RetrieveCmsTemlatesXmlConfiguration(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Fix this output to XML no matter what the content negotiation came up with
            reqVars.returnType = ReturnTypeEnum.Xml;

            // Retrieve the posted fileid from the route
            var configurationFileId = "cms_config_project-templates";
            
            // Call the core logic method
            var result = await RetrieveCmsTemlatesXmlConfigurationCore(configurationFileId, reqVars);
            
            // Return the response
            if (result.Success)
            {
                await response.OK(result.XmlResponse, ReturnTypeEnum.Xml, true);
            }
            else
            {
                HandleError(reqVars, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
        }

        /// <summary>
        /// Core logic for retrieving CMS templates XML configuration without HTTP dependencies
        /// </summary>
        /// <param name="configurationFileId">Configuration file identifier</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>Configuration result</returns>
        public async static Task<ConfigurationResult> RetrieveCmsTemlatesXmlConfigurationCore(
            string configurationFileId,
            RequestVariables reqVars)
        {
            var result = new ConfigurationResult();
            
            await DummyAwaiter();
            
            try
            {
                // Load the XML document
                var xmlConfiguration = new XmlDocument();
                var xmlConfigurationFilePathOs = CalculateFullPathOs(configurationFileId);

                if (string.IsNullOrEmpty(xmlConfigurationFilePathOs))
                {
                    result.Success = false;
                    result.ErrorMessage = "Could not find configuration file to serve";
                    result.DebugInfo = $"configurationFileId: '{configurationFileId}', stack-trace: {GetStackTrace()}";
                    result.StatusCode = 500;
                    return result;
                }

                xmlConfiguration.Load(xmlConfigurationFilePathOs);

                result.Success = true;
                result.XmlResponse = xmlConfiguration;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error loading configuration file";
                result.DebugInfo = $"configurationFileId: {configurationFileId}, error: {ex}, stack-trace: {GetStackTrace()}";
                result.StatusCode = 500;
                return result;
            }
        }

        /// <summary>
        /// Helper routine that attempts to load the requested XML configuration document and serve it to the client
        /// </summary>
        /// <returns>The xml file content.</returns>
        /// <param name="configurationFileId">Configuration file identifier.</param>
        /// <param name="response">Response.</param>
        private async static Task _serveXmlFileContent(string configurationFileId, HttpResponse response)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Fix this output to XML no matter what the content negotiation came up with
            reqVars.returnType = ReturnTypeEnum.Xml;

            // Load the XML document
            var xmlConfiguration = new XmlDocument();
            var xmlConfigurationFilePathOs = CalculateFullPathOs(configurationFileId);
            if (string.IsNullOrEmpty(xmlConfigurationFilePathOs))
            {
                throw new HttpStatusCodeException(reqVars, 500, "Could not find configuration file to serve", $"configurationFileId: '{configurationFileId}', stack-trace: {CreateStackInfo()}");
            }
            else
            {
                try
                {
                    xmlConfiguration.Load(xmlConfigurationFilePathOs);
                }
                catch (Exception ex)
                {
                    throw new HttpStatusCodeException(reqVars, 500, "No valid configuration file ID found", $"configurationFileId: {configurationFileId}, error: {ex}, stack-trace: {CreateStackInfo()}");
                }

            }

            await response.OK(xmlConfiguration, ReturnTypeEnum.Xml, true);
        }

        /// <summary>
        /// Result class for configuration operations
        /// </summary>
        public class ConfigurationResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? DebugInfo { get; set; }
            public int StatusCode { get; set; } = 500;
            public XmlDocument? XmlResponse { get; set; }
            public string? ResponseContent { get; set; }
        }
    }
}
