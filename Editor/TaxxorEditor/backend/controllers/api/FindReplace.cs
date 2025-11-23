using System;
using System.Threading.Tasks;
using System.Web;
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
        /// Kicks off find and replace functionality
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FindReplace(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var searchFragment = request.RetrievePostedValue("searchfragment", RegexEnum.TextArea, true, reqVars.returnType);
            var replaceFragment = request.RetrievePostedValue("replacefragment", RegexEnum.TextArea, false, reqVars.returnType) ?? "";
            var outputChannelLanguage = request.RetrievePostedValue("oclang", @"^\w{2}$", false, reqVars.returnType, "en");
            var dryRunString = request.RetrievePostedValue("dryrun", "(true|false)", false, reqVars.returnType, "false");
            var dryRun = (dryRunString == "true");
            var onlyInUseString = request.RetrievePostedValue("onlyinuse", @"^(true|false)$", false, reqVars.returnType, "false");
            bool onlyInUse = (onlyInUseString == "true");
            var includeFootnotesString = request.RetrievePostedValue("includefootnotes", "(true|false)", false, reqVars.returnType, "false");
            var includeFootnotes = (includeFootnotesString == "true");
            var createVersionString = request.RetrievePostedValue("createversion", "(true|false)", false, reqVars.returnType, "false");
            var createVersion = (createVersionString == "true");

            var errorMessage = "There was an error while searching and replacing text in the data files";

            //
            // => Stylesheet cache
            //
            SetupPdfStylesheetCache(reqVars);


            try
            {
                // Create a version before we start
                if (createVersion && !dryRun)
                {
                    var preFindReplaceVersionResult = await GenerateVersion(projectVars.projectId, $"Pre find and replace version", false);
                    if (!preFindReplaceVersionResult.Success) HandleError(ReturnTypeEnum.Json, preFindReplaceVersionResult.Message, preFindReplaceVersionResult.DebugInfo);
                }

                // Global find and replace content for this project
                var xmlResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.FindReplace(searchFragment, replaceFragment, onlyInUse, includeFootnotes, dryRun, debugRoutine);

                //
                // => Update the local and remote version of the CMS Metadata XML object now that we are fully done with the transformation
                //
                await UpdateCmsMetadataRemoteAndLocal(projectVars.projectId);

                if (XmlContainsError(xmlResult))
                {
                    await response.Error(xmlResult, reqVars.returnType, true);
                }
                else
                {
                    //
                    // => Clear the paged media cache
                    // 
                    ContentCache.Clear(projectVars.projectId);

                    //
                    // => Create a post replace version
                    //
                    if (createVersion && !dryRun)
                    {
                        var postFindReplaceVersionResult = await GenerateVersion(projectVars.projectId, $"Post find and replace version", false);
                        if (!postFindReplaceVersionResult.Success) HandleError(ReturnTypeEnum.Json, postFindReplaceVersionResult.Message, postFindReplaceVersionResult.DebugInfo);
                    }

                    // Retrieve the payload that contains the details of the replace result
                    var payloadNode = xmlResult.SelectSingleNode("/result/payload");
                    if (payloadNode == null)
                    {
                        appLogger.LogError($"[FindReplace] ERROR: /result/payload node not found in response. Full XML: {xmlResult.OuterXml}");
                        await response.Error(GenerateErrorXml("Invalid gRPC response structure", $"Missing /result/payload node. Full XML: {xmlResult.OuterXml}"), reqVars.returnType, true);
                        return;
                    }

                    var xmlPayload = HttpUtility.HtmlDecode(payloadNode.InnerXml);
                    var xmlReplaceDetails = new XmlDocument();
                    xmlReplaceDetails.LoadXml(xmlPayload);

                    // Build an XmlDocument to return
                    var xmlReturn = new XmlDocument();
                    var nodeResult = xmlReturn.CreateElement("result");
                    var nodeMessage = xmlReturn.CreateElementWithText("message", xmlResult.SelectSingleNode("//message")?.InnerText ?? "");
                    var nodeDetails = xmlReturn.CreateElement("details");

                    var nodeDetailsImported = xmlReturn.ImportNode(xmlReplaceDetails.DocumentElement, true);
                    nodeDetails.AppendChild(nodeDetailsImported);

                    nodeResult.AppendChild(nodeMessage);
                    nodeResult.AppendChild(nodeDetails);
                    xmlReturn.AppendChild(nodeResult);


                    // Convert to JSON and return it to the client
                    string jsonToReturn = JsonConvert.SerializeObject(xmlReturn, Newtonsoft.Json.Formatting.Indented);

                    await response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
                }
            }
            catch (Exception ex)
            {
                // Return the error
                appLogger.LogError(ex, errorMessage);
                await response.Error(GenerateErrorXml(errorMessage, $"error: {ex}"), reqVars.returnType, true);
            }
        }

    }

}