using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves the latest version of the content that is being used in the editor
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveContentVersion(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var scope = request.RetrievePostedValue("scope", @"^(latest|all)$", true, reqVars.returnType, "latest");
            var includeDateStampString = request.RetrievePostedValue("includedatestamp", RegexEnum.Boolean, false, reqVars.returnType, "false");
            var includeDateStamp = (includeDateStampString == "true");


            /*
            Call the Taxxor Document Store to retrieve the data
            */
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pid", projectVars.projectId);
            dataToPost.Add("scope", scope);
            dataToPost.Add("includedatestamp", includeDateStampString);

            // Call the service and retrieve the version label
            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "contentversion", dataToPost, debugRoutine);

            if (XmlContainsError(xmlResponse))
            {
                appLogger.LogError($"There was an error retrieving the content version. remote-error: {xmlResponse.OuterXml}, stack-trace: {GetStackTrace()}");

                // For the client we will render a "normal" message, but without a version number
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Unable to retrieve the content version";
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = $"stack-trace: {GetStackTrace()}";
                }
                jsonData.result.version = "";

                if (includeDateStamp)
                {
                    jsonData.result.epoch = "";
                }

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
            else
            {
                var contentVersion = xmlResponse.SelectSingleNode("/result/payload").InnerText.Trim();

                if (scope == "latest" && !RegExpTest(@"^v\d+\.\d+(\|.*)?$", contentVersion)) contentVersion = "";

                if (scope == "all" && !RegExpTest(@"^v\d.*\d$", contentVersion)) contentVersion = "";

                // Construct a response message for the client
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully retrieved content version";
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = $"projectVars.projectId: {projectVars.projectId}";
                }

                if (scope == "latest")
                {
                    var version = "";
                    var epoch = "";
                    if (contentVersion.Contains("|"))
                    {
                        version = contentVersion.SubstringBefore("|");
                        epoch = contentVersion.SubstringAfter("|").SubstringBefore(" +0000");
                    }
                    else
                    {
                        version = contentVersion;
                    }

                    jsonData.result.version = version;
                    if (includeDateStamp)
                    {
                        jsonData.result.epoch = epoch;
                    }
                }

                if (scope == "all")
                {
                    jsonData.result.version = contentVersion.Split(",");
                }

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }

        }

    }

}