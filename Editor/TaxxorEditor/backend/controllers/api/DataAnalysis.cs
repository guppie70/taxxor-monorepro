using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        public async static Task AnalyzeEditorDataListScenarios(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Construct a project variables object that we can send to the Taxxor Pandoc Service
            var projectVarsForDataAnalysis = new ProjectVariables(projectVars.projectId, projectVars.outputChannelVariantId);

            /*
             * Call the Taxxor Document Store to retrieve the data
             */
            var dataToPost = projectVarsForDataAnalysis.RenderPostDictionary();
            var result = await CallTaxxorConnectedService<TaxxorReturnMessage>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorcontentanalysis", dataToPost, debugRoutine);

            if (result.Success)
            {
                var jsonToReturn = ConvertToJson(result.XmlPayload);

                // Render the response
                await response.OK(jsonToReturn, reqVars.returnType, true);
            }
            else
            {
                await context.Response.Error(result, reqVars.returnType, true);
            }
        }


        /// <summary>
        /// Bulk analyze editor data
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task AnalyzeEditorData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var analysisType = request.RetrievePostedValue("analysistype", @"^(\w|\d|\-|_,){2,512}$", true, reqVars.returnType);

            var result = await DataAnalysisExecute(projectVars.projectId, projectVars.outputChannelVariantId, analysisType, debugRoutine);
            if (result.Success)
            {
                await context.Response.OK(PrettyPrintXml(result.XmlPayload), reqVars.returnType, true);
            }
            else
            {
                await context.Response.Error(result, reqVars.returnType, true);
            }
        }


        /// <summary>
        /// Executes a data alalysis tool - useful for intenal purposes
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="outputChannelVariantId"></param>
        /// <param name="methodId"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> DataAnalysisExecute(string projectId, string outputChannelVariantId, string methodId, bool debugRoutine = false)
        {
            // Construct a project variables object that we can send to the Taxxor Pandoc Service
            var projectVarsForDataAnalysis = new ProjectVariables(projectId, outputChannelVariantId);

            /*
             * Call the Taxxor Document Store to retrieve the data
             */
            var dataToPost = projectVarsForDataAnalysis.RenderPostDictionary();
            dataToPost.Add("sections", "all");
            dataToPost.Add("renderscope", "include-children");
            dataToPost.Add("analysistype", methodId);

            return await CallTaxxorConnectedService<TaxxorReturnMessage>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorcontentanalysis", dataToPost, debugRoutine);
        }

        /// <summary>
        /// Serves the complete contents of a filing as a download so that it can be analyzed
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task DownloadCompleteProjectContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var scope = request.RetrievePostedValue("scope", @"^(outputchannel|source)$", true, reqVars.returnType);

            //
            // => Stylesheet cache
            //
            SetupPdfStylesheetCache(reqVars);

            // ocvariantid
            // oclang
            var downloadFileName = "";
            var xmlContentRetrieved = new XmlDocument();
            if (scope == "outputchannel")
            {
                downloadFileName = $"{TaxxorClientId}-{projectVars.projectId}-{projectVars.outputChannelVariantId}.xml";

                // Use the PDF XHTML type to retrieve the content
                projectVars.outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);

                var xhtmlContent = await RetrieveLatestPdfHtml(projectVars, "all", false, false);

                try
                {
                    xmlContentRetrieved.LoadXml(xhtmlContent);
                }
                catch (Exception ex)
                {
                    HandleError("Could not load full content", $"error: {ex}, projectVars: {projectVars.ToString()}");
                }

            }
            else
            {
                // Retrieve all the content in one file
                var lang = request.RetrievePostedValue("lang", @"^(\w){2,3}$", true, reqVars.returnType);
                var onlyInUseString = request.RetrievePostedValue("onlyinuse", @"^(true|false)$", true, reqVars.returnType);

                bool onlyInUse = (onlyInUseString == "true");

                downloadFileName = $"{TaxxorClientId}-{projectVars.projectId}-{lang}-{onlyInUseString}.xml";

                xmlContentRetrieved = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadAll<XmlDocument>(projectVars.projectId, lang, onlyInUse, debugRoutine, true);
            }

            await response.Attachment(xmlContentRetrieved, downloadFileName, ReturnTypeEnum.Xml, true);

        }


    }

}