using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Routines used in the Publication Variants page
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders the UI part that shows the different publication variants that the user can generate as a download
        /// </summary>
        public static async Task<string> RenderPublicationVariantsOverview()
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var xmlApplicationConfigurationCloned = new XmlDocument();
            xmlApplicationConfigurationCloned.ReplaceContent(xmlApplicationConfiguration);

            // Retrieve a hierarchy (output channel) id from the user preferences
            var userPreferenceHierarchyInformationToShow = projectVars.currentUser.RetrieveUserPreferenceKey("hierarchyoutputchannel-" + projectVars.projectId);
            // Console.WriteLine($"- userPreferenceHierarchyInformationToShow: {userPreferenceHierarchyInformationToShow}");
            string? outputChannelVariantId = null;
            if (string.IsNullOrEmpty(userPreferenceHierarchyInformationToShow))
            {
                // Use the first one that we can find in the configuration
                outputChannelVariantId = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels[1]/output_channel[1]/variants[1]/variant")?.GetAttribute("id");
            }
            else
            {
                // Parse the string retrieved from the user preference
                outputChannelVariantId = userPreferenceHierarchyInformationToShow.SubstringBefore(":oclang").SubstringAfter("ocvariantid=");
            }
            // Console.WriteLine($"- outputChannelVariantId: {outputChannelVariantId}");

            //
            // => Append the user project preferences to the application configuration so we can use it to build the UI
            //
            var nodeProjectPreferences = xmlApplicationConfigurationCloned.CreateElement("projectpreferences");
            var nodeOutputChannels = xmlApplicationConfigurationCloned.CreateElement("outputchannels");
            nodeProjectPreferences.AppendChild(nodeOutputChannels);


            var userProjectPreferencesJson = await RenderProjectPreferencesJson(projectVars, false);

            if (!string.IsNullOrEmpty(userProjectPreferencesJson))
            {
                // - Convert the user settings to an XML document
                var xmlUserProjectPreferences = new XmlDocument();
                xmlUserProjectPreferences = ConvertJsonToXml(userProjectPreferencesJson, "projectpreferences");

                // - Rework the XML so that it has a more logical structure
                var nodeListOutputchannelVariants = xmlUserProjectPreferences.SelectNodes("/projectpreferences/outputchannels/*");
                foreach (XmlNode nodeVariant in nodeListOutputchannelVariants)
                {
                    var variantId = nodeVariant.LocalName;
                    var variantLayout = nodeVariant.SelectSingleNode("layout")?.InnerText ?? "unknown";

                    // - Sometimes the json convertor generates node names with hex characters - so we need to convert these
                    if (variantId.StartsWith("_x"))
                    {
                        try
                        {
                            var hexCodeCharacter = RegExpReplace(@"^_x(\d+)_(.*)$", variantId, "$1");
                            var suffix = RegExpReplace(@"^_x(\d+)_(.*)$", variantId, "$2");
                            variantId = (char)Int16.Parse(hexCodeCharacter, NumberStyles.AllowHexSpecifier) + suffix;

                            // Console.WriteLine($"- variantId: {variantId}");                            
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogWarning($"Could not convert {variantId} to an ID. error: {ex.Message}");
                        }
                    }

                    var nodeNormalizedVariant = xmlApplicationConfigurationCloned.CreateElement("variant");
                    nodeNormalizedVariant.SetAttribute("id", variantId);
                    nodeNormalizedVariant.SetAttribute("layout", variantLayout);
                    nodeOutputChannels.AppendChild(nodeNormalizedVariant);
                }
            }
            xmlApplicationConfigurationCloned.DocumentElement.AppendChild(nodeProjectPreferences);
            // Console.WriteLine(PrettyPrintXml(xmlApplicationConfigurationCloned.SelectSingleNode("/configuration/projectpreferences")));


            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
            var reportTypeId = RetrieveReportTypeIdFromProjectId(projectVars.projectId);

            var xsltArgumentList = new XsltArgumentList();
            xsltArgumentList.AddParam("sitetype", "", siteType);
            xsltArgumentList.AddParam("project-id", "", projectVars.projectId);
            xsltArgumentList.AddParam("editor-id", "", editorId);
            xsltArgumentList.AddParam("reporttype-id", "", reportTypeId);
            xsltArgumentList.AddParam("ocvariantid", "", outputChannelVariantId);
            xsltArgumentList.AddParam("permissions", "", string.Join(",", projectVars.currentUser.Permissions.Permissions.ToArray()));

            return TransformXml(xmlApplicationConfigurationCloned, "cms_xsl_publication-variants", xsltArgumentList);
        }


        /// <summary>
        /// Renders an XBRL collision report in semi XHR mode
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RenderCollisionReport(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var errorMessage = "There was an error rendering the collision report";
            var errorDetails = $"projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}";

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Detect if we want to stream the outcome of the report rendering as a web-response or as a message over the SignalR connection
            var webSocketModeString = context.Request.RetrievePostedValue("websocketmode", "(true|false)", false, reqVars.returnType, "false");
            var webSocketMode = (webSocketModeString == "true");

            if (webSocketMode)
            {
                //
                // => Return data to the web client to close the XHR call
                //
                await context.Response.OK(GenerateSuccessXml("Started collision report generation process", $"projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}"), ReturnTypeEnum.Json, true, true);
                await context.Response.CompleteAsync();
            }

            try
            {
                //
                // => Auto-fill the project variable object
                //
                projectVars.Fill();
                SetProjectVariables(context, projectVars);

                if (debugRoutine)
                {
                    Console.WriteLine("----------------------------------------------------------------");
                    Console.WriteLine(projectVars.DumpToString());
                    Console.WriteLine("----------------------------------------------------------------");
                }


                // throw new Exception("Thrown on purpose");

                //
                // => Generate the collision report in websocket mode so that we can stream the updates to the client
                //
                var collisionReportResult = await RenderCollisionReport(projectVars, webSocketMode);
                if (!collisionReportResult.Success) throw new Exception(collisionReportResult.Message);

                //
                // => Store the generated report on the server
                //
                var collisionReportFilename = RandomString(12, false) + ".html";
                var generatedCollisionReportFilePathOs = dataRootPathOs + "/temp/" + collisionReportFilename;
                await collisionReportResult.XmlPayload.SaveAsync(generatedCollisionReportFilePathOs, true, true);


                //
                // => Generate a response
                //
                var successMessage = "Successfully rendered the collision report";
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = successMessage;
                jsonData.result.filename = collisionReportFilename;
                var json = (string)ConvertToJson(jsonData);

                if (webSocketMode)
                {
                    await MessageToCurrentClient("CollisionReportGenerationDone", new TaxxorReturnMessage(true, successMessage, json, ""));
                }
                else
                {
                    // Stream the response to the client
                    switch (reqVars.returnType)
                    {
                        case ReturnTypeEnum.Json:
                            await context.Response.OK(json, ReturnTypeEnum.Json, true);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"{errorMessage}. {errorDetails}");
                var errorReturnMessage = new TaxxorReturnMessage(false, errorMessage);
                if (webSocketMode)
                {
                    await MessageToCurrentClient("WordGenerationDone", errorReturnMessage);
                }
                else
                {
                    errorReturnMessage.DebugInfo = errorDetails;
                    HandleError(errorReturnMessage);
                }
            }
        }

        /// <summary>
        /// Renders a collosion report for an output channel
        /// </summary>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public async static Task<TaxxorReturnMessage> RenderCollisionReport(ProjectVariables projectVars, bool webSocketMode)
        {
            var errorMessage = "There was an error rendering the collision report";
            var errorDetails = $"projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}";
            var debugRoutine = (siteType == "local" || siteType == "dev");
            try
            {
                //
                // => Test if there is an XBRL reporting requirement associated with the output channel
                //
                var reportingRequirementName = "";
                var targetReportingModelId = "";
                var nodeReportingRequirement = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/reporting_requirements/reporting_requirement[@ref-outputchannelvariant='{projectVars.outputChannelVariantId}' and @ref-mappingservice]");
                if (nodeReportingRequirement == null)
                {
                    appLogger.LogError($"Could not find a reporting requirement scheme to base the collision report on. {errorDetails}");
                    return new TaxxorReturnMessage(false, errorMessage, $"Could not find a reporting requirement scheme to base the collision report on. {errorDetails}");
                }
                targetReportingModelId = nodeReportingRequirement.GetAttribute("ref-mappingservice");
                reportingRequirementName = nodeReportingRequirement.SelectSingleNode("name")?.InnerText ?? "unknown";
                if (webSocketMode) await _outputGenerationProgressMessage($"Start generating {reportingRequirementName} collision report");

                //
                // => Retrieve the outpuchannel hierarchy overview to resolve a data reference to an itemId
                //
                var xmlHierarchyOverview = await RenderOutputChannelHierarchyOverview(projectVars, false, true, true);

                //
                // => Render an XML containing details of the fact 
                //
                if (webSocketMode) await _outputGenerationProgressMessage($"Retrieving SDE information");
                var factOverviewResult = await DataAnalysisExecute(projectVars.projectId, projectVars.outputChannelVariantId, "renderstructureddataelementlist", true);
                if (!factOverviewResult.Success) return new TaxxorReturnMessage(false, errorMessage, factOverviewResult.ToString());
                var xmlFactOverview = factOverviewResult.XmlPayload;
                if (debugRoutine) await xmlFactOverview.SaveAsync($"{logRootPathOs}/factoverview.xml");

                //
                // => Strip the items that do not fall under the XBRL regime
                //
                var dataReferencesXbrl = new List<string>();
                var xPath = $"/hierarchies/output_channel[@id='{projectVars.outputChannelVariantId}']/items/structured//item[@data-ref and contains(@data-reportingrequirements-{projectVars.outputChannelVariantLanguage}, '{targetReportingModelId}')]";
                var nodeListXbrlItems = xmlHierarchyOverview.SelectNodes(xPath);
                if (nodeListXbrlItems.Count == 0) return new TaxxorReturnMessage(false, $"Output channel '{projectVars.outputChannelVariantId}' does not contain any items that are defined as XBRL with target '{targetReportingModelId}'", projectVars.DumpToString());
                foreach (XmlNode nodeXbrlItem in nodeListXbrlItems)
                {
                    var dataRef = nodeXbrlItem.GetAttribute("data-ref");
                    if (!dataReferencesXbrl.Contains(dataRef)) dataReferencesXbrl.Add(dataRef);
                }
                var nodeListFacts = xmlFactOverview.SelectNodes("/facts/fact");
                foreach (XmlNode nodeFact in nodeListFacts)
                {
                    var dataRef = nodeFact.GetAttribute("data-ref");
                    if (!dataReferencesXbrl.Contains(dataRef)) nodeFact.SetAttribute("delete", "true");
                }
                var nodeListFactsToDelete = xmlFactOverview.SelectNodes("/facts/fact[@delete]");
                if (nodeListFactsToDelete.Count > 0)
                {
                    RemoveXmlNodes(nodeListFactsToDelete);
                }
                if (debugRoutine) await xmlFactOverview.SaveAsync($"{logRootPathOs}/factoverview.stripped.xml");

                //
                // => Request the collision report data from the Structured Data Store
                //
                if (webSocketMode) await _outputGenerationProgressMessage($"Retrieving collisions");

                // - URL to use for the request
                var uriStructuredDataStore = GetServiceUrl(ConnectedServiceEnum.StructuredDataStore);
                string url = $"{uriStructuredDataStore}/api/mappingconflicts?pid={projectVars.projectId}&targetModel={targetReportingModelId}";

                // - Posted content
                var httpContent = new StringContent(xmlFactOverview.OuterXml, Encoding.UTF8, "text/xml");
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Text.Xml);


                // - Execute the request
                var xmlConflictReport = new XmlDocument();
                using (HttpClient _httpClient = new HttpClient())
                {
                    // Properties for the HTTP client
                    _httpClient.Timeout = TimeSpan.FromMinutes(5);
                    _httpClient.DefaultRequestHeaders.Add("X-Tx-UserId", SystemUser);
                    _httpClient.DefaultRequestHeaders.Accept.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Accept", "text/xml");

                    using (HttpResponseMessage result = await _httpClient.PostAsync(url, httpContent))
                    {
                        if (result.IsSuccessStatusCode)
                        {
                            // Retrieve the XML response string
                            var xmlResponse = await result.Content.ReadAsStringAsync();
                            // Console.WriteLine("---------------------------");
                            // Console.WriteLine(xmlResponse);
                            // Console.WriteLine("---------------------------");

                            try
                            {
                                xmlConflictReport.LoadXml(xmlResponse);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("---------------------------");
                                Console.WriteLine(xmlResponse);
                                Console.WriteLine("---------------------------");
                                appLogger.LogError(ex, errorMessage);
                                return new TaxxorReturnMessage(false, errorMessage, $"There was a problem parsing the result of the service into an XML Document. {errorDetails}");
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

                // - Loop through the conflicts and add a site structure item ID
                var nodeListConflicts = xmlConflictReport.SelectNodes("/report/conflict/mapping");
                foreach (XmlNode nodeConflict in nodeListConflicts)
                {
                    var dataReference = nodeConflict.GetAttribute("section");
                    if (!string.IsNullOrEmpty(dataReference))
                    {
                        // Attempt to find the item ID using the hierarchy overview document 
                        xPath = $"/hierarchies/output_channel[@id='{projectVars.outputChannelVariantId}']/items/structured//item[@data-ref='{dataReference}']";
                        var nodeItem = xmlHierarchyOverview.SelectSingleNode(xPath);
                        if (nodeItem != null)
                        {
                            var itemId = nodeItem.GetAttribute("id");
                            if (!string.IsNullOrEmpty(itemId))
                            {
                                nodeConflict.SetAttribute("sectionId", itemId);
                                nodeConflict.SetAttribute("status", "ok");

                                // Add section name and potentially section number
                                var linkName = nodeItem.SelectSingleNode("web_page/linkname")?.InnerText ?? "unknown";
                                var sectionNumber = nodeItem.GetAttribute("data-tocnumber") ?? "";
                                if (!string.IsNullOrEmpty(sectionNumber)) nodeConflict.SetAttribute("sectionNumber", sectionNumber);
                                nodeConflict.SetAttribute("sectionName", linkName);
                            }
                            else
                            {
                                var nodeItemCloned = nodeItem.CloneNode(false);
                                appLogger.LogWarning($"Unable to find an ID on sitestructure node: {nodeItemCloned.OuterXml}");
                                nodeConflict.SetAttribute("status", "noid");
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Datareference {dataReference} is not used in {projectVars.outputChannelVariantId}. xPath: {xPath}");
                            nodeConflict.SetAttribute("status", "notinuse");
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"Found a conflict without a section deta reference. {nodeConflict.OuterXml}");
                        nodeConflict.SetAttribute("status", "nodataref");
                    }
                }


                if (debugRoutine) await xmlConflictReport.SaveAsync($"{logRootPathOs}/conflictreport.xml", false, true);

                //
                // => Render the collision report in HTML
                //
                if (webSocketMode) await _outputGenerationProgressMessage($"Rendering the report in HTML");

                var baseDomainName = "";
                var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{siteType}']");
                var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
                var protocol = (nodeCurrentDomain.GetAttribute("ssl") == "true") ? "https" : "http";

                switch (siteType)
                {
                    case "local":
                        baseDomainName = $"{protocol}://{currentDomainName}:4812";
                        break;
                    default:
                        baseDomainName = $"{protocol}://{currentDomainName}";
                        break;
                }

                XsltArgumentList xsltArgs = new XsltArgumentList();
                xsltArgs.AddParam("base-uri", "", baseDomainName);
                xsltArgs.AddParam("project-id", "", projectVars.projectId);
                xsltArgs.AddParam("project-name", "", (xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/name")?.InnerText ?? "unknown"));
                xsltArgs.AddParam("outputchannel-id", "", projectVars.outputChannelVariantId);
                xsltArgs.AddParam("outputchannel-name", "", (xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(projectVars.outputChannelVariantId)}]/name")?.InnerText ?? "unknown"));
                xsltArgs.AddParam("targetmodel-id", "", targetReportingModelId);
                xsltArgs.AddParam("targetmodel-name", "", reportingRequirementName);
                xsltArgs.AddParam("timestamp", "", createIsoTimestamp());
                var xHtmlConflictReport = TransformXmlToDocument(xmlConflictReport, "taxxor_xsl_xbrl-conflict-report", xsltArgs);

                // - Store the file for debugging purposes
                if (debugRoutine) await xHtmlConflictReport.SaveAsync($"{logRootPathOs}/conflictreport.html", false, true);


                //
                // => Render return value
                //
                return new TaxxorReturnMessage(true, "Successfully rendered conflict report", xHtmlConflictReport, errorDetails);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage, errorDetails);
            }
        }

    }
}