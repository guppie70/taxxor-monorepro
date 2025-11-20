using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Lists all the Taxxor Document Store tools which are available in the service
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveDataServiceTools(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            /*
             * Call the Taxxor Document Store to retrieve the data
             */
            var dataToPost = new Dictionary<string, string>();
            XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditordataservicetools", dataToPost, debugRoutine);

            // Handle error
            if (XmlContainsError(responseXml)) HandleError(reqVars.returnType, "There was an error while retrieving the data analysis response", $"responseXml: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");

            // The XML content that we are looking for is in decoded form available in the message node
            var xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/payload").InnerText);

            try
            {
                var xmlDataServiceTools = new XmlDocument();
                xmlDataServiceTools.LoadXml(xml);

                // Retrieve a list of available projects
                var nodeMeta = xmlDataServiceTools.CreateElement("meta");

                var xmlProjectList = ListProjects();
                var nodeProjectsImported = xmlDataServiceTools.ImportNode(xmlProjectList.DocumentElement, true);

                nodeMeta.AppendChild(nodeProjectsImported);
                xmlDataServiceTools.DocumentElement.AppendChild(nodeMeta);

                // if (debugRoutine)
                // {
                //     Console.WriteLine("*************************************");
                //     Console.WriteLine(PrettyPrintXml(xmlDataServiceTools));
                //     Console.WriteLine("*************************************");
                // }


                var jsonToReturn = ConvertToJson(xmlDataServiceTools, Newtonsoft.Json.Formatting.Indented, true);

                // Render the response
                await response.OK(jsonToReturn, reqVars.returnType, true);
            }
            catch (Exception ex)
            {
                HandleError(reqVars.returnType, "Could not load the tool list from the Taxxor Document Store", $"error: {ex}, xml: {TruncateString(xml, 100)}, stack-trace: {GetStackTrace()}");
            }
        }


        /// <summary>
        /// Executes the tool on the Taxxor Document Store for the projects passed
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ExecuteDataServiceTool(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted variables
            var projectIdsPosted = request.RetrievePostedValue("pids", @"^[a-zA-Z_\-\d,]{3,3000}$", true, ReturnTypeEnum.Json);
            var toolId = request.RetrievePostedValue("toolid", @"^(\w|\d|\-|_,){2,512}$", true, ReturnTypeEnum.Json);

            // HandleError(ReturnTypeEnum.Json, "Thrown on purpose in Taxxor Editor", $"projectIdsPosted: {projectIdsPosted.ToString()}, toolId: {toolId}, reqVars.returnType: {reqVars.returnType}, stack-trace: {GetStackTrace()}");


            //
            // => Call the Taxxor Document Store to execute the tool and return the response
            //
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pids", projectIdsPosted);
            dataToPost.Add("toolid", toolId);

            XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditordataservicetools", dataToPost, debugRoutine);

            // Handle error
            if (XmlContainsError(responseXml)) HandleError(reqVars.returnType, "There was an error while retrieving the Taxxor Document Store Tool response", $"projectIdsPosted: {projectIdsPosted.ToString()}, toolId: {toolId}, responseXml: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");

            //
            // => Update the local and remote version of the CMS Metadata XML object now that we are fully done with the transformation
            //
            await UpdateCmsMetadataRemoteAndLocal(projectVars.projectId);

            // The XML content that we are looking for is in decoded form available in the message node
            var xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/message").InnerText);

            try
            {
                //
                // => Clear the complete paged media cache
                // 
                ContentCache.Clear();

                var xmlSourceData = new XmlDocument();
                xmlSourceData.LoadXml(xml);
                await context.Response.OK(PrettyPrintXml(xmlSourceData), reqVars.returnType, true);
            }
            catch (Exception ex)
            {
                HandleError(reqVars.returnType, "Could not execute the Taxxor Document Store Tool", $"error: {ex}, xml: {TruncateString(xml, 100)}, projectIdsPosted: {projectIdsPosted.ToString()}, toolId: {toolId}, stack-trace: {GetStackTrace()}");
            }
        }

    }

}