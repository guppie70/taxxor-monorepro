using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using TableSpans.HtmlAgilityPack;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Sends a typical OK or Error response to the client for testing API client behavior
        /// </summary>
        /// <returns>The request sandbox.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task RenderTestResponseSandbox(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Retrieve the posted fileid from the route
            var statusType = (string)routeData.Values["type"];
            
            // Call the core logic method
            var result = await RenderTestResponseSandboxCore(statusType, request, reqVars);
            
            // Return the response
            if (result.Success)
            {
                await response.OK(result.ResponseContent, reqVars.returnType, true);
            }
            else
            {
                await response.Error(result.ResponseContent, reqVars.returnType, true);
            }
        }

        /// <summary>
        /// Core logic for rendering test response without HTTP dependencies
        /// </summary>
        /// <param name="statusType">Status type (ok or error)</param>
        /// <param name="request">HTTP request (for extracting posted values)</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>Sandbox result</returns>
        public async static Task<SandboxResult> RenderTestResponseSandboxCore(
            string statusType, 
            HttpRequest request, 
            RequestVariables reqVars)
        {
            var result = new SandboxResult();

            await DummyAwaiter();
            
            if (statusType == null)
            {
                result.Success = false;
                result.ErrorMessage = "No valid status to work with found";
                result.DebugInfo = $"- statusType: {statusType}";
                result.StatusCode = 500;
                return result;
            }

            // Set return type to XML
            reqVars.returnType = ReturnTypeEnum.Xml;

            // Logging something to the console so that we can see what is going on
            appLogger.LogInformation($"Sandbox remote request routine using statusType: '{statusType}'");

            // Retrieve posted values
            var foo = "nothing";
            try
            {
                foo = request.RetrievePostedValue("foo");
            }
            catch (Exception) { }

            // Load the XML document
            var xmlResponse = new XmlDocument();

            if (statusType == "ok")
            {
                // Fill with some bogus content
                xmlResponse.LoadXml($"<foo><bar>baz received posted value: '{foo}'</bar></foo>");

                // Set the response content
                result.ResponseContent = xmlResponse.DocumentElement.OuterXml;
                result.Success = true;
            }
            else
            {
                xmlResponse.LoadXml("<foo>Error was forced</foo>");

                // Set the response content
                result.ResponseContent = xmlResponse.DocumentElement.OuterXml;
                result.Success = false;
            }

            return result;
        }

        public async static Task SandboxMergeDataConfiguration(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Call the core logic method
            var result = await SandboxMergeDataConfigurationCore();
            
            // Create an HTML response to return to the client
            var responseToClient = $@"
                <html><body>
                <h1>Merge new information from Data Configuration in Application Configuration</h1>
                {GenerateDebugObjectString(result)}
                </body></html>";

            // Write response to client
            await response.OK(responseToClient, reqVars.returnType, true);
        }

        /// <summary>
        /// Core logic for merging data configuration without HTTP dependencies
        /// </summary>
        /// <returns>Result of the operation</returns>
        public async static Task<TaxxorReturnMessage> SandboxMergeDataConfigurationCore()
        {
            return await UpdateDataConfigurationInApplicationConfiguration();
        }

        public async static Task SandboxAuditorMessage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Call the core logic method
            var result = await SandboxAuditorMessageCore();
            
            // Create an HTML response to return to the client
            var responseToClient = $@"
                <html><body>
                <h1>Commit auditor info for project ID: {result.ProjectId}</h1>
                {GenerateDebugObjectString(result.Message)}
                </body></html>";

            // Write response to client
            await response.OK(responseToClient, reqVars.returnType, true);
        }

        /// <summary>
        /// Core logic for creating auditor message without HTTP dependencies
        /// </summary>
        /// <returns>Result of the operation with project ID</returns>
        public async static Task<(string ProjectId, TaxxorReturnMessage Message)> SandboxAuditorMessageCore()
        {
            var currentProjectId = GetAttribute(xmlApplicationConfiguration.SelectSingleNode("//cms_project[position()=last()]"), "id");

            // Create a test file that we can use to submit
            var projectDataPathOs = dataRootPathOs + RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id='" + currentProjectId + "']/location[@id='reportdataroot']", xmlApplicationConfiguration);
            var testFilePathOs = $"{projectDataPathOs}/foo.bar";
            TextFileCreate(RandomString(100, false), testFilePathOs);

            TaxxorReturnMessage result = await CreateAuditorMessage(AuditorAreaEnum.FilingData, "Test message", currentProjectId);
            
            return (currentProjectId, result);
        }

        /// <summary>
        /// Result class for sandbox operations
        /// </summary>
        public class SandboxResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? DebugInfo { get; set; }
            public int StatusCode { get; set; } = 500;
            public string? ResponseContent { get; set; }
        }
    }
}
