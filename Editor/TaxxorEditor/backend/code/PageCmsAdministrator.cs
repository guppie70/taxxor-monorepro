using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    /// <summary>
    /// CMS Administrator tools
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Lists the different static asset sources by reading a JSON file stored on one of the S3 buckets
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task ListStaticAssetsSources(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var debugRoutine = siteType == "local" || siteType == "dev";

            try
            {
                //
                // => List all static asset sources that are available
                //

                // Retrieve JSON data from the URL
                var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{siteType}']");
                var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
                var protocol = (nodeCurrentDomain.GetAttribute("ssl") == "true") ? "https" : "http";
                var originUrl = $"{protocol}://{currentDomainName}";

                List<string>? s3Folders = new List<string>();
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Origin", originUrl);
                    string jsonString = await httpClient.GetStringAsync("https://static.test.taxxordm.com/s3folders.json");
                    s3Folders = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jsonString);
                }


                // Calculate the state of the different location by inspecting the session
                string? staticAssetsLocation;
                bool isStaticAssetsLocationSet = context.Session.TryGetValue("staticassetslocation", out byte[]? staticAssetsLocationBytes);

                if (isStaticAssetsLocationSet)
                {
                    staticAssetsLocation = System.Text.Encoding.UTF8.GetString(staticAssetsLocationBytes);
                }
                else
                {
                    staticAssetsLocation = null;
                }

                var staticAssetsLocationProd = xmlApplicationConfiguration.SelectSingleNode($"/configuration/locations/location[@id='staticassets']/domain[@type='prod']")?.InnerText ?? StaticAssetsLocation;

                // Calculate the ID used in the dropdown
                var staticAssetsLocationStaging = xmlApplicationConfiguration.SelectSingleNode($"/configuration/locations/location[@id='staticassets']/domain[@type='prev']")?.InnerText ?? StaticAssetsLocation;
                var staticAssetsSourceId = "";
                if (isStaticAssetsLocationSet && staticAssetsLocation != staticAssetsLocationProd)
                {
                    Uri uri = new Uri(staticAssetsLocation);
                    staticAssetsSourceId = uri.AbsolutePath?.TrimStart('/') ?? "unknown";
                }
                if (!string.IsNullOrEmpty(staticAssetsSourceId)) staticAssetsLocationStaging = staticAssetsLocationStaging.Replace("/develop", $"/{staticAssetsSourceId}");

                dynamic jsonResponseData = new ExpandoObject();
                jsonResponseData.sources = new Dictionary<string, dynamic>();
                jsonResponseData.sources["default"] = new ExpandoObject();
                jsonResponseData.sources["default"].selected = staticAssetsLocation == null;
                jsonResponseData.sources["default"].name = "Default";

                foreach (var folder in s3Folders)
                {
                    if (folder != "develop")
                    {
                        jsonResponseData.sources[folder] = new ExpandoObject();
                        jsonResponseData.sources[folder].selected = staticAssetsSourceId == folder;
                        jsonResponseData.sources[folder].name = char.ToUpper(folder[0]) + folder.Substring(1);
                    }
                }

                // Convert to JSON and return it to the client
                string jsonToReturn = JsonConvert.SerializeObject(jsonResponseData, Newtonsoft.Json.Formatting.Indented);

                await response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "There was an error rendering the static assets sources list", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Sets a session variable for the alternative static asset source for this user
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task SetStaticAssetsSources(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var debugRoutine = siteType == "local" || siteType == "dev";

            var staticAssetsSourceId = context.Request.RetrievePostedValue("staticassetssourceid", @"^(\w|\d|\-|\.|_){2,512}$", true, ReturnTypeEnum.Json);

            try
            {

                //
                // => Sets the alternative static asset source for this user's session
                //
                switch (staticAssetsSourceId)
                {
                    case "default":
                        // Clear the session variable for the alternative static asset source
                        context.Session.Remove("staticassetslocation");
                        break;

                    case "master":
                        // Pick the production location from the configuration file
                        var staticAssetsLocationProd = xmlApplicationConfiguration.SelectSingleNode($"/configuration/locations/location[@id='staticassets']/domain[@type='prod']")?.InnerText ?? StaticAssetsLocation; ;
                        context.Session.SetString("staticassetslocation", staticAssetsLocationProd);
                        break;

                    default:
                        var staticAssetsLocationStaging = xmlApplicationConfiguration.SelectSingleNode($"/configuration/locations/location[@id='staticassets']/domain[@type='prev']")?.InnerText ?? StaticAssetsLocation; ;
                        staticAssetsLocationStaging = staticAssetsLocationStaging.Replace("/develop", $"/{staticAssetsSourceId}");
                        context.Session.SetString("staticassetslocation", staticAssetsLocationStaging);
                        break;
                }

                var returnMessage = new TaxxorReturnMessage(true, "Static asset source set successfully");

                await response.OK(returnMessage, ReturnTypeEnum.Json, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "There was an error setting the static assets source", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

        }




    }
}