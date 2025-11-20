using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices.DocumentStoreService;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves details of a footnote from the Taxxor Document Store
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task StreamGeneratedReport(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var guid = request.RetrievePostedValue("guid", RegexEnum.Guid, true, reqVars.returnType);

            var (Success, ApiUrl, OriginalFileName, XbrlScheme) = await GetGeneratedReportApiUrl(projectVars.projectId, guid);

            if (Success)
            {
                await StreamBinary(context.Response, ApiUrl, OriginalFileName, true);
            }
            else
            {
                appLogger.LogError("Unable to retrieve the generated report from the repository");
                await response.Error("Error retrieving report", ReturnTypeEnum.Html);
            }
        }


        /// <summary>
        /// Generates information about the URL to retrieve a generated report from the Taxxor Document Store
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static async Task<(bool Success, string ApiUrl, string OriginalFileName, string XbrlScheme)> GetGeneratedReportApiUrl(string projectId, string guid)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";
            try
            {
                //
                // => Retrieve the repository contents
                //
                var retrieveResult = await GeneratedReportsRepository.RetrieveContent(null, null, null, debugRoutine);
                if (!retrieveResult.Success)
                {
                    appLogger.LogError(retrieveResult.ToString());
                    throw new Exception(retrieveResult.Message);
                }

                //
                // => Get information about the requested report
                //
                var nodeReport = retrieveResult.XmlPayload.SelectSingleNode($"/reports/report[@id={GenerateEscapedXPathString(guid)}]");
                if (nodeReport == null)
                {
                    throw new Exception("Report not found");
                }
                var originalFileName = nodeReport.GetAttribute("filename");
                var storedFileName = $"{Path.GetFileNameWithoutExtension(originalFileName)}---{nodeReport.GetAttribute("epoch")}{Path.GetExtension(originalFileName)}";
                if (debugRoutine) appLogger.LogDebug($"originalFileName: {originalFileName}, storedFileName: {storedFileName}");

                var xbrlScheme = nodeReport.GetAttribute("scheme");

                //
                // => Construct the URL for retrieving the report from the Taxxor Document Store
                //
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "relativeto", "projectreports" },
                    { "path", $"/{storedFileName}" }
                };

                var apiUrl = QueryHelpers.AddQueryString(GetServiceUrlByMethodId(ConnectedServiceEnum.DocumentStore, "taxxoreditorfilingbinary"), dataToPost);

                return (true, apiUrl, originalFileName, xbrlScheme);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "Unable to retrieve the generated report from the repository");
                return (false, "Error retrieving API url", $"error: {ex}", "");
            }
        }

    }

}