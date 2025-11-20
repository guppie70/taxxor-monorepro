using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Proxies the start filing XHR request to the edgar filer
        /// </summary>
        /// <returns>The anonymous dynamic content request.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task EdgarFilerXhr(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);

            // Retrieve posted data
            string? cik = request.RetrievePostedValue("CIK");
            string? password = request.RetrievePostedValue("Password", RegexEnum.None);
            string? ccc = request.RetrievePostedValue("CCC", RegexEnum.None);
            string? filingType = request.RetrievePostedValue("filingType");
            string? filingPeriod = request.RetrievePostedValue("filingPeriod");
            string? contactName = request.RetrievePostedValue("contactName");
            string? contactPhone = request.RetrievePostedValue("contactPhone");
            string? uploadFolder = request.RetrievePostedValue("uploadFolder");
            string? capturesFolder = request.RetrievePostedValue("capturesFolder");
            string? submissionType = request.RetrievePostedValue("submissionType");
            string? SROs = request.RetrievePostedValue("SROs", RegexEnum.None);

            // Debug information
            var baseDebugInfo = $"cik: {cik}, password: {password}, ccc: {ccc}, filingType: {filingType}, filingPeriod: {filingPeriod}, contactName: {contactName}, contactPhone: {contactPhone}, uploadFolder: {uploadFolder}, capturesFolder: {capturesFolder}, submissionType: {submissionType}, filingType: {SROs}";

            // URL to service
            var nodePluginEdgarFiler = xmlApplicationConfiguration.SelectSingleNode("//service[@id='taxxoredgarfiler']/uri");
            if (nodePluginEdgarFiler != null)
            {

                var baseUriEdgarFiler = CalculateFullPathOs(nodePluginEdgarFiler);
                if (debugRoutine)
                {
                    Console.WriteLine("***************** URL TO EDGAR FILING SERVICE *****************");
                    Console.WriteLine(baseUriEdgarFiler);
                    Console.WriteLine("***************************************************************");
                }
                var uriToService = $"{baseUriEdgarFiler}/api/startfilingxhr";


                // Set the headers
                CustomHttpHeaders customHttpHeaders = new CustomHttpHeaders();
                customHttpHeaders.RequestType = ReturnTypeEnum.Json;

                // Build the object to POST
                dynamic jsonPost = new ExpandoObject();
                jsonPost.CIK = cik;
                jsonPost.Password = password;
                jsonPost.CCC = ccc;
                jsonPost.filingType = filingType;
                jsonPost.filingPeriod = filingPeriod;
                jsonPost.contactName = contactName;
                jsonPost.contactPhone = contactPhone;
                jsonPost.uploadFolder = uploadFolder;
                jsonPost.capturesFolder = capturesFolder;
                jsonPost.submissionType = submissionType;
                jsonPost.SROs = SROs;

                var jsonToPost = ConvertToJson(jsonPost, Newtonsoft.Json.Formatting.Indented);

                if (UriLogEnabled)
                {
                    if (!UriLogBackend.Contains(uriToService)) UriLogBackend.Add(uriToService);
                }

                // Post to the Edgar Filing Service
                XmlDocument renderEngineResult = await RestRequest<XmlDocument>(RequestMethodEnum.Post, uriToService, jsonToPost, customHttpHeaders, 10000, true);

                // Handle response
                if (XmlContainsError(renderEngineResult))
                {
                    await response.Error(renderEngineResult, ReturnTypeEnum.Json, true);
                }
                else
                {
                    dynamic jsonData = new ExpandoObject();
                    jsonData.result = new ExpandoObject();
                    jsonData.result.message = "Filing process started";
                    jsonData.result.debuginfo = baseDebugInfo;

                    var json = (string) ConvertToJson(jsonData);
                    await response.OK(json, ReturnTypeEnum.Json, true);
                }
            }
            else
            {
                await response.Error(GenerateErrorXml("There was an error staring the filing submission", $"URL to service could not be found, {baseDebugInfo}"), ReturnTypeEnum.Json, true);
            }
        }

    }
}