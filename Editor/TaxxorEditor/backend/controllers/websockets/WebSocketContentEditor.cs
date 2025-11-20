using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Websocket methods that render UI components dynamically
        /// </summary>
        public partial class WebSocketsHub : Hub
        {

            /// <summary>
            /// Retrieves groups that a user is a member of
            /// </summary>
            /// <param name="userId"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveSectionOccurences(string projectId, string outputChannelVariantId, string itemId)
            {
                var errorMessage = "There was an error retrieving the section occurences information";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("outputChannelVariantId", outputChannelVariantId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("itemId", itemId, @"^[a-zA-Z0-9\-_]{2,1024}$", true);

                    //
                    // => Prepare the project variables object so that we can use it in the authorization process
                    //
                    projectVars.Fill(projectId, outputChannelVariantId);


                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    SetProjectVariables(context, projectVars);

                    // projectVars.rbacCache = new RbacCache(projectVars.currentUser.Id, projectVars.projectId);

                    //
                    // => Retrieve hierarchy overview
                    //
                    var xmlHierarchyOverview = await RenderOutputChannelHierarchyOverview(projectVars, false, true, true);
                    // await xmlHierarchyOverview.SaveAsync($"{logRootPathOs}/hieroverview.xml", true, true);

                    //
                    // => Create an overview of all output channels and the section id's where this section is also available in
                    //

                    // - find datareference
                    var xPath = $"/hierarchies/output_channel[@id='{outputChannelVariantId}']/items/structured//item[@id='{itemId}']";
                    var nodeSourceItem = xmlHierarchyOverview.SelectSingleNode(xPath);
                    if (nodeSourceItem == null)
                    {
                        var errorDebugInformation = $"xpath: {xPath} returned no results";
                        appLogger.LogError(errorDebugInformation);
                        return new TaxxorReturnMessage(false, errorMessage, errorDebugInformation);
                    }

                    var dataRef = nodeSourceItem.GetAttribute("data-ref");
                    if (string.IsNullOrEmpty(dataRef))
                    {
                        var errorDebugInformation = $"data reference could not be located";
                        appLogger.LogError(errorDebugInformation);
                        return new TaxxorReturnMessage(false, errorMessage, errorDebugInformation);
                    }

                    // - loop through the output channels
                    dynamic jsonData = new ExpandoObject();
                    jsonData.outputchannels = new Dictionary<string, List<string>>();
                    var nodeListOutputChannels = xmlHierarchyOverview.SelectNodes("/hierarchies/output_channel");
                    foreach (XmlNode nodeChannel in nodeListOutputChannels)
                    {
                        var channelId = nodeChannel.GetAttribute("id");
                        var sectionIds = new List<string>();
                        var nodeListItems = nodeChannel.SelectNodes($"items/structured//item[@data-ref='{dataRef}']");
                        foreach (XmlNode nodeItem in nodeListItems)
                        {
                            var sectionItemId = nodeItem.GetAttribute("id");
                            if (!string.IsNullOrEmpty(sectionItemId) && !sectionIds.Contains(sectionItemId)) sectionIds.Add(sectionItemId);
                        }
                        jsonData.outputchannels.Add(channelId, sectionIds);
                    }

                    // Transform to JSON
                    var json = (string)ConvertToJson(jsonData);

                    // Render the result as a TaxxorReturnMessage
                    return new TaxxorReturnMessage(true, "Successfully rendered group information", json, $"itemId: {itemId}");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Retrieves website placeholders for a given output channel variant and project id
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="outputChannelVariantId"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveWebsitePlaceholders(string projectId, string outputChannelVariantId)
            {
                var errorMessage = "There was an error retrieving the website placeholders";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("outputChannelVariantId", outputChannelVariantId, @"^[a-zA-Z0-9\-_]{2,128}$", true);

                    //
                    // => Prepare the project variables object so that we can use it in the authorization process
                    //
                    projectVars.Fill(projectId, outputChannelVariantId);


                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    SetProjectVariables(context, projectVars);

                    //
                    // => Retrieve the website placeholders
                    //
                    var placeholders = Extensions.RenderWebsiteReplacements(projectVars);


                    // Render the result as a TaxxorReturnMessage
                    return new TaxxorReturnMessage(true, "Successfully retrieved website placeholders", ConvertToJson(placeholders), $"projectId: {projectId}, outputChannelVariantId: {outputChannelVariantId}");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Retrieves the editor left menu
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="outputChannelVariantId"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveEditorLeftMenu(string projectId, string outputChannelVariantId)
            {
                var errorMessage = "There was an error retrieving editor navigation";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("outputChannelVariantId", outputChannelVariantId, @"^[a-zA-Z0-9\-_]{2,128}$", true);

                    //
                    // => Prepare the project variables object so that we can use it in the authorization process
                    //
                    projectVars.Fill(projectId, outputChannelVariantId);


                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    SetProjectVariables(context, projectVars);

                    //
                    // => Retrieve the website placeholders
                    //
                    JObject? projectPreferences = null;
                    var nodeProjectPreferences = projectVars.currentUser.XmlUserPreferences.SelectSingleNode($"/settings/setting[@id='projectpreferences-{projectVars.projectId}']");
                    if (nodeProjectPreferences != null)
                    {
                        var projectPreferencesJson = nodeProjectPreferences.InnerText;
                        projectPreferences = JObject.Parse(projectPreferencesJson);
                    }
                    var htmlLeftMenu = await GenerateEditorNavigation(projectPreferences);


                    // Render the result as a TaxxorReturnMessage
                    return new TaxxorReturnMessage(true, "Successfully rendered editor left navigation", htmlLeftMenu, $"projectId: {projectId}, outputChannelVariantId: {outputChannelVariantId}");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }



            /// <summary>
            /// Migrates the display template of one or more SDE's
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="data"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/managertools")]
            public async Task<TaxxorReturnMessage> MigrateSdeDisplayOption(string projectId, string dataRef, List<string> factGuids, string templateId)
            {
                var errorMessage = "There was an error migrating the SDE display templates";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("dataRef", dataRef, RegexEnum.FileName, true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Migrate the SDE display options
                    //

                    //
                    // => Construct the URL to post the Structured Data Store request to
                    //
                    var uriStructuredDataStore = GetServiceUrl(ConnectedServiceEnum.StructuredDataStore);
                    string url = $"{uriStructuredDataStore}/api/formatting/mappings";
                    var sdsDataToPost = new List<KeyValuePair<string, string>>();
                    sdsDataToPost.Add(new KeyValuePair<string, string>("pid", projectId));
                    var languages = RetrieveProjectLanguages(RetrieveEditorIdFromProjectId(projectId));
                    foreach (var lang in languages)
                    {
                        sdsDataToPost.Add(new KeyValuePair<string, string>("locale", lang));
                    }

                    var apiUrl = QueryHelpers.AddQueryString(url, sdsDataToPost);

                    //
                    // => Construct the data that we want to post
                    //
                    dynamic data = new ExpandoObject();
                    data.mappings = new List<ExpandoObject>();
                    foreach (var factGuid in factGuids)
                    {
                        dynamic mapping = new ExpandoObject();
                        mapping.factId = factGuid;
                        mapping.formattingId = templateId;
                        data.mappings.Add(mapping);
                    }
                    var jsonData = JsonConvert.SerializeObject(data);
                    var contentData = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    // Console.WriteLine(apiUrl);

                    //
                    // => Make the request
                    //
                    string? structuredDataStoreResponse = null;
                    try
                    {
                        using (HttpClient _httpClient = new HttpClient())
                        {
                            // Properties for the HTTP client
                            _httpClient.Timeout = TimeSpan.FromMinutes(1);
                            _httpClient.DefaultRequestHeaders.Add("X-Tx-UserId", SystemUser);
                            _httpClient.DefaultRequestHeaders.Accept.Clear();
                            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                            using (HttpResponseMessage result = await _httpClient.PostAsync(apiUrl, contentData))
                            {
                                if (result.IsSuccessStatusCode)
                                {
                                    // Retrieve the XML response string
                                    structuredDataStoreResponse = await result.Content.ReadAsStringAsync();

                                    if (debugRoutine)
                                    {
                                        Console.WriteLine("------------- Clone change SDE template result --------------");
                                        Console.WriteLine(structuredDataStoreResponse);
                                        Console.WriteLine("-------------------------------------------------------------");
                                    }
                                }
                                else
                                {
                                    var errorContent = $"{result.ReasonPhrase}: ";
                                    errorContent += await result.Content.ReadAsStringAsync();
                                    var errorDebugInfo = $"url: {url}, HTTP Status Code: {result.StatusCode}, client-response: {errorContent}, incoming-request: {RenderHttpRequestDebugInformation()}";
                                    appLogger.LogError($"{errorMessage}, {errorDebugInfo}");

                                    return new TaxxorReturnMessage(false, errorMessage, errorDebugInfo);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"{errorMessage} {apiUrl}");
                        return new TaxxorReturnMessage(false, errorMessage, "");
                    }

                    //
                    // => Regenerate the SDE cache
                    //
                    appLogger.LogInformation("Starting SDE cache creation");
                    var dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("projectid", projectId);
                    dataToPost.Add("snapshotid", "1");
                    dataToPost.Add("dataref", dataRef);

                    var xmlSyncResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorsyncstructureddatasnapshotperdataref", dataToPost, true);
                    if (XmlContainsError(xmlSyncResponse))
                    {
                        return new TaxxorReturnMessage(false, "There was an error creating the SDE cache", $"{xmlSyncResponse.OuterXml}");
                    }
                    else
                    {
                        appLogger.LogInformation("SDE cache creation successfully completed");
                    }

                    //
                    // => Clear the cache
                    //
                    ContentCache.Clear(projectId);

                    // Render the result as a TaxxorReturnMessage
                    return new TaxxorReturnMessage(true, "Successfully migrated SDE display templates and regenerated cache", structuredDataStoreResponse, $"projectId: {projectId}");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }







        }

    }

}