using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// Routines used in the project details page
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        private static string GenerateHierarchyLockKey(RequestVariables reqVars, string hierarchyIdToShow)
        {
            return $"{reqVars.pageId}__{hierarchyIdToShow}";
        }

        /// <summary>
        /// Renders the body content of the hierarchy manager page
        /// </summary>
        /// <param name="renderCompletePage"></param>
        /// <returns></returns>
        public static async Task<string> RenderCmsHierarchyManagerBody(bool renderCompletePage = true)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // var errorMessage = "";
            // var errorDebugInfo = "";
            var debugRoutine = (siteType == "local");

            // Parse the posted variables or use the user preferences
            var hierarchyIdToShow = "-";
            var filterAllItemsList = "yes";
            var filterAdditionalInformation = "yes";
            var filterContentStatusMarkers = "yes";
            var target = context.Request.RetrievePostedValue("target", RegexEnum.Strict, false, ReturnTypeEnum.Xml, "unknown");
            if (!renderCompletePage)
            {
                hierarchyIdToShow = context.Request.RetrievePostedValue("hierarchyid", @"^(\w{3,100}|\-)", false, ReturnTypeEnum.Html, "-");
                filterAllItemsList = context.Request.RetrievePostedValue("filterallitemslist", "^(yes|no)$", false, ReturnTypeEnum.Html, "no");
                filterAdditionalInformation = context.Request.RetrievePostedValue("filteradditionalinformation", "^(yes|no)$", false, ReturnTypeEnum.Html, "no");
                filterContentStatusMarkers = context.Request.RetrievePostedValue("filtercontentstatusmarkers", "^(yes|no)$", false, ReturnTypeEnum.Html, "no");
            }
            else
            {
                // Retrieve a hierarchy (output channel) id from the user preferences
                var userPreferenceHierarchyIdToShow = projectVars.currentUser.RetrieveUserPreferenceKey("hierarchyoutputchannel-" + projectVars.projectId);
                if (!string.IsNullOrEmpty(userPreferenceHierarchyIdToShow)) hierarchyIdToShow = userPreferenceHierarchyIdToShow;

                var userPreferenceFilterAllItemsList = projectVars.currentUser.RetrieveUserPreferenceKey("hierarchyallitemsfilter-" + projectVars.projectId);
                if (!string.IsNullOrEmpty(userPreferenceFilterAllItemsList)) filterAllItemsList = userPreferenceFilterAllItemsList;

                var userPreferenceFilterAdditionalInformation = projectVars.currentUser.RetrieveUserPreferenceKey("hierarchyadditionalinfofilter-" + projectVars.projectId);
                if (!string.IsNullOrEmpty(userPreferenceFilterAdditionalInformation)) filterAdditionalInformation = userPreferenceFilterAdditionalInformation;

                var userPreferenceFilterContentStatusMarkers = projectVars.currentUser.RetrieveUserPreferenceKey("hierarchycontentstatusfilter-" + projectVars.projectId);
                if (!string.IsNullOrEmpty(userPreferenceFilterContentStatusMarkers)) filterContentStatusMarkers = userPreferenceFilterContentStatusMarkers;
            }

            //
            // => Check locking system
            //
            var isEditableHierarchy = true;
            var lockUserName = "";
            var lockUserId = "";
            if (target == "hierarchyeditor")
            {
                // - Is this section locked by another user?
                var pageType = "hierarchy"; // filing | page | hierarchy

                // Retrieve the lock information
                var xmlLock = new XmlDocument();
                xmlLock = FilingLockStore.RetrieveLockForPageId(projectVars, GenerateHierarchyLockKey(reqVars, hierarchyIdToShow), pageType);
                lockUserId = xmlLock?.SelectSingleNode("/lock/userid")?.InnerText ?? "";
                // if (debugRoutine)
                // {
                //     Console.WriteLine("&&&&&&&&&&&& Lock Info &&&&&&&&&&&&");
                //     Console.WriteLine(xmlLock.OuterXml);
                //     Console.WriteLine($"Current user id: {projectVars.currentUser.Id}");
                //     Console.WriteLine($"Lock user id: {lockUserId}");
                //     Console.WriteLine($"Section id: {did}");
                //     Console.WriteLine("&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&");
                // }

                if (!string.IsNullOrEmpty(lockUserId) && lockUserId != projectVars.currentUser.Id)
                {
                    isEditableHierarchy = false;
                    lockUserName = xmlLock?.SelectSingleNode("/lock/username")?.InnerText ?? "";
                }

                // - Lock this section
                var createLockResult = FilingLockStore.AddLock(projectVars, GenerateHierarchyLockKey(reqVars, hierarchyIdToShow), pageType);
                if (!createLockResult.Success)
                {
                    HandleError(ReturnTypeEnum.Html, "Unable to create lock for this content", createLockResult.DebugInfo);
                }
                else
                {
                    // Update all connected clients with the new lock information
                    //     try
                    //     {
                    //         var hubContext = context.RequestServices.GetRequiredService<IHubContext<WebSocketsHub>>();
                    //         var listLocksResult = ListLocks(projectVars, pageType);
                    //         await hubContext.Clients.All.SendAsync("UpdateSectionLocks", listLocksResult);
                    //     }
                    //     catch (Exception ex)
                    //     {
                    //         appLogger.LogError(ex, "Could not update all connected clients with the new lock information using SignalR");
                    //     }
                }
            }

            // Retrieve the Output Channel Variant ID from the string
            hierarchyIdToShow = RegExpReplace(@"^.*ocvariantid=(.*?)\:.*$", hierarchyIdToShow, "$1");

            var contentLang = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, hierarchyIdToShow);

            // Compile one document based on all the output channels
            var xmlOutputChannelHierarchies = await RenderOutputChannelHierarchyOverview(projectVars, true, true, (!renderCompletePage));


            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);

            var baseXPath = $"/configuration/editors/editor[@id='{editorId}']/output_channels/";

            // Hierarchy type master | slave | none
            var hierarchyType = "none";
            var masterOutputChannelId = "";
            var masterOutputChannelName = "";
            var masterHierarchyId = "";
            var slaveOutputChannelIds = new List<string>();
            var slaveHierarchyNames = new List<string>();
            var slaveHierarchyIds = new List<string>();

            // Test if we are serving a slave hierarchy
            var nodesOutputVariant = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{hierarchyIdToShow}' and @slave-of]");
            if (nodesOutputVariant != null)
            {
                hierarchyType = "slave";
                masterOutputChannelId = nodesOutputVariant.GetAttribute("slave-of");
                masterOutputChannelName = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{masterOutputChannelId}']/name")?.InnerText ?? "unknown";
                masterHierarchyId = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{masterOutputChannelId}']")?.GetAttribute("metadata-id-ref") ?? "unknown";
                var nodeListSlaveVariants = xmlApplicationConfiguration.SelectNodes($"{baseXPath}/variants/variant[@slave-of='{masterOutputChannelId}']");
                foreach (XmlNode nodeSlave in nodeListSlaveVariants)
                {
                    slaveOutputChannelIds.Add(GetAttribute(nodeSlave, "id"));
                    slaveHierarchyNames.Add(nodeSlave.SelectSingleNode("name")?.InnerText ?? GetAttribute(nodeSlave, "id") ?? "unknown");
                    slaveHierarchyIds.Add(nodeSlave.GetAttribute("metadata-id-ref") ?? "unknown");
                }
            }
            else
            {
                // Check if we are serving a master hierarchy
                var nodeListSlaveVariants = xmlApplicationConfiguration.SelectNodes($"{baseXPath}/variants/variant[@slave-of]");
                foreach (XmlNode nodeSlaveVariant in nodeListSlaveVariants)
                {
                    masterOutputChannelId = nodeSlaveVariant.GetAttribute("slave-of");
                    if (masterOutputChannelId == hierarchyIdToShow)
                    {
                        hierarchyType = "master";
                        masterOutputChannelName = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{masterOutputChannelId}']/name")?.InnerText ?? "unknown";
                        masterHierarchyId = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{masterOutputChannelId}']")?.GetAttribute("metadata-id-ref") ?? "unknown";
                        var nodeListSlaves = xmlApplicationConfiguration.SelectNodes($"{baseXPath}/variants/variant[@slave-of='{masterOutputChannelId}']");
                        foreach (XmlNode nodeSlave in nodeListSlaves)
                        {
                            var variantId = GetAttribute(nodeSlave, "id");
                            var variantName = nodeSlave.SelectSingleNode("name")?.InnerText ?? GetAttribute(nodeSlave, "id") ?? "unknown";
                            var hierarchyId = nodeSlave.GetAttribute("metadata-id-ref") ?? "unknown";
                            if (!slaveOutputChannelIds.Contains(variantId)) slaveOutputChannelIds.Add(variantId);
                            if (!slaveHierarchyNames.Contains(variantName)) slaveHierarchyNames.Add(variantName);
                            if (!slaveHierarchyIds.Contains(hierarchyId)) slaveHierarchyIds.Add(hierarchyId);
                        }
                    }
                }
            }

            // Test if we are dealing with linked hierarchies
            var linkedHierarchyNames = new List<string>();
            if (hierarchyType == "master" || hierarchyType == "slave")
            {
                var isLinkedHierarchy = true;
                foreach (var hierarchyId in slaveHierarchyIds)
                {
                    if (hierarchyId != masterHierarchyId) isLinkedHierarchy = false;
                }
                if (isLinkedHierarchy)
                {
                    hierarchyType = "linked";
                    linkedHierarchyNames.AddRange(slaveHierarchyNames);
                    linkedHierarchyNames.Add(masterOutputChannelName);
                    var currentOutputChannelName = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{hierarchyIdToShow}']/name")?.InnerText ?? "unknown";
                    if (linkedHierarchyNames.Contains(currentOutputChannelName)) linkedHierarchyNames.Remove(currentOutputChannelName);
                }
            }


            if (debugRoutine)
            {
                Console.WriteLine("$$$$$$$$$$$");
                Console.WriteLine($"- hierarchyType: {hierarchyType}");
                Console.WriteLine($"- masterOutputChannelId: {masterOutputChannelId}");
                Console.WriteLine($"- masterHierarchyId: {masterHierarchyId}");
                Console.WriteLine($"- renderCompletePage: {renderCompletePage}");
                Console.WriteLine($"- slaveOutputChannelIds: {string.Join<string>(",", slaveOutputChannelIds)} => {slaveOutputChannelIds.Count.ToString()}");
                Console.WriteLine($"- slaveHierarchyIds: {string.Join<string>(",", slaveHierarchyIds)} => {slaveHierarchyIds.Count.ToString()}");
                Console.WriteLine($"- linkedHierarchyNames: {string.Join<string>(",", linkedHierarchyNames)} => {linkedHierarchyNames.Count.ToString()}");
                Console.WriteLine($"- isEditableHierarchy: {isEditableHierarchy}, lockUserName: {lockUserName}, lockUserId: {lockUserId}");
                Console.WriteLine("$$$$$$$$$$$");
            }


            if (hierarchyType == "slave")
            {

            }

            /*
            var html = "<pre>" + Server.HtmlEncode(PrettyPrintXml(xmlOutputChannelHierarchy.OuterXml)) + "</pre>";
            Response.Write(html);
            */

            // Render the output
            XsltArgumentList xsltArgumentList = new XsltArgumentList();
            if (renderCompletePage)
            {
                xsltArgumentList.AddParam("render-full-page", "", "yes");
            }
            else
            {
                xsltArgumentList.AddParam("render-full-page", "", "no");
            }
            xsltArgumentList.AddParam("output-channel-reference", "", hierarchyIdToShow);
            xsltArgumentList.AddParam("filter-full-list", "", filterAllItemsList);
            xsltArgumentList.AddParam("filter-additional-info", "", filterAdditionalInformation);
            xsltArgumentList.AddParam("filter-contentstatus-markers", "", filterContentStatusMarkers);
            xsltArgumentList.AddParam("hierarchy-type", "", hierarchyType);
            xsltArgumentList.AddParam("contentlang", "", contentLang ?? projectVars.outputChannelDefaultLanguage);
            xsltArgumentList.AddParam("master-outputchannel-id", "", masterOutputChannelId);
            xsltArgumentList.AddParam("master-outputchannel-name", "", masterOutputChannelName);
            xsltArgumentList.AddParam("slave-outputchannel-ids", "", string.Join<string>(",", slaveOutputChannelIds));
            xsltArgumentList.AddParam("slave-outputchannel-names", "", string.Join<string>("\n", slaveHierarchyNames));
            xsltArgumentList.AddParam("linked-outputchannel-names", "", string.Join<string>("\n", linkedHierarchyNames));
            xsltArgumentList.AddParam("locked", "", (isEditableHierarchy) ? "no" : "yes");
            xsltArgumentList.AddParam("lock-user-id", "", lockUserId);
            xsltArgumentList.AddParam("lock-user-name", "", lockUserName);

            var html = TransformXml(xmlOutputChannelHierarchies, CalculateFullPathOs("cms_xsl_channels-to-list"), xsltArgumentList);

            return html;
        }


        /// <summary>
        /// Renders information about the available output channels
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RenderAvailableOutputChannelOverview(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Compile one document based on all the output channels
            var xmlOutputChannelHierarchies = await RenderOutputChannelHierarchyOverview();

            // Transform the document so that we can compile a consise overview
            var xmlOutputChannnelOverview = TransformXmlToDocument(xmlOutputChannelHierarchies, "cms_xsl_outputchanneloverview");

            // Make sure that when we transform the XML to JSON that the JSON structure is always the same
            if (reqVars.returnType == ReturnTypeEnum.Json)
            {
                var nodeListOutputChannels = xmlOutputChannnelOverview.SelectNodes("/outputchannels/outputchannel");
                foreach (XmlNode nodeOutputChannel in nodeListOutputChannels)
                {
                    var attrForceArray = xmlOutputChannnelOverview.CreateAttribute("json", "Array", "http://james.newtonking.com/projects/json");
                    attrForceArray.InnerText = "true";
                    nodeOutputChannel.Attributes.Append(attrForceArray);
                }
            }

            // Console.WriteLine("***********************");
            // Console.WriteLine(PrettyPrintXml(xmlOutputChannnelOverview));
            // Console.WriteLine("***********************");

            await response.OK(xmlOutputChannnelOverview, reqVars.returnType, true);
        }

        /// <summary>
        /// Generates the content of the hierarcy manager page
        /// </summary>
        public static async Task WriteCmsHierarchyManagerBody(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var html = await RenderCmsHierarchyManagerBody(false);

            await response.OK(html, ReturnTypeEnum.Html, true);
        }


        /// <summary>
        /// Retrieves a hierarchy from the hierarchy manager in JSON, converts it to XML and the sends it to the Taxxor Document Store for storage
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task SaveFilingHierarchy(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = reqVars.isDebugMode;

            XmlDocument xmlPostedHierarchy = new XmlDocument();

            var jsonHierarchy = request.RetrievePostedValue("jsonhierarchy", RegexEnum.None, true, reqVars.returnType);
            if (debugRoutine) await TextFileCreateAsync(jsonHierarchy, logRootPathOs + "/_hierarchy-saved.json");


            //
            // => Check locking system
            //
            // var isEditableHierarchy = true;
            // var lockUserName = "";
            // var lockUserId = "";

            // // - Is this section locked by another user?
            // var pageType = "hierarchy"; // filing | page | hierarchy

            // // Retrieve the lock information
            // var xmlLock = new XmlDocument();
            // xmlLock = FilingLockStore.RetrieveLockForPageId(projectVars, GenerateHierarchyLockKey(reqVars, hierarchyIdToShow), pageType);
            // lockUserId = xmlLock?.SelectSingleNode("/lock/userid")?.InnerText ?? "";
            // // if (debugRoutine)
            // // {
            // //     Console.WriteLine("&&&&&&&&&&&& Lock Info &&&&&&&&&&&&");
            // //     Console.WriteLine(xmlLock.OuterXml);
            // //     Console.WriteLine($"Current user id: {projectVars.currentUser.Id}");
            // //     Console.WriteLine($"Lock user id: {lockUserId}");
            // //     Console.WriteLine($"Section id: {did}");
            // //     Console.WriteLine("&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&");
            // // }

            // if (!string.IsNullOrEmpty(lockUserId) && lockUserId != projectVars.currentUser.Id)
            // {
            //     isEditableHierarchy = false;
            //     lockUserName = xmlLock?.SelectSingleNode("/lock/username")?.InnerText ?? "";
            // }






            // Some basic validation
            if (jsonHierarchy.ToLower().Contains("<script")) HandleError(ReturnTypeEnum.Json, "Error converting your posted data to xml", $"JavaScript injection detected, stack-trace: {GetStackTrace()}");

            try
            {
                string jsonToConvert = @"{
				  '?xml': {
					'@version': '1.0',
					'@standalone': 'no'
				  },
				  'root': {
					data: " + jsonHierarchy + @"
				  }
				}";

                xmlPostedHierarchy = (XmlDocument)JsonConvert.DeserializeXmlNode(jsonToConvert);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "Error converting output channel hierarchy", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

            // Store the xml in the log directory
            var saved = false;
            if (debugRoutine) saved = await SaveXmlDocument(xmlPostedHierarchy, logRootPathOs + "/_hierarchy-saved.xml");

            // Grab the XHTML tree from the saved hierarchy
            var xhtmlPostedHierarchy = new XmlDocument();
            try
            {
                xmlPostedHierarchy.LoadHtml(WebUtility.HtmlDecode(xmlPostedHierarchy.SelectSingleNode("/root/data[1]/items[1]/html[1]").InnerText));
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "Error loading converted output channel hierarchy", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
            if (debugRoutine) saved = await SaveXmlDocument(xmlPostedHierarchy, logRootPathOs + "/_hierarchy-saved-xhtml.xml");

            // Perform some basic checks on what was saved
            // - schema check
            var schemaPathOs = applicationRootPathOs + "/backend/views/schemas/hierarchy-xhtml.xsd";
            var schemaCheckResult = await ValidateXml(xmlPostedHierarchy, schemaPathOs);

            if (!schemaCheckResult.Success) throw new Exception($"Validation failed: {schemaCheckResult.Message}, Debug information: {schemaCheckResult.DebugInfo}");

            // - sanity checks
            var nodeListIncomingItems = xmlPostedHierarchy.SelectNodes("//li");
            if (nodeListIncomingItems.Count <= 1)
            {
                HandleError(ReturnTypeEnum.Json, "Hierarchy seems to be containing no elements", $"Debug information: {GetStackTrace()}");
            }

            var validationErrorDetailsList = new List<string>();
            foreach (XmlNode nodeIncomingItem in nodeListIncomingItems)
            {
                var linkName = (nodeIncomingItem.SelectSingleNode("div//span[contains(@class, 'linkname')]")?.InnerText ?? "").Trim();
                var path = (nodeIncomingItem.SelectSingleNode("div//span[contains(@class, 'path')]")?.InnerText ?? "").Trim();
                if (string.IsNullOrEmpty(path))
                {
                    appLogger.LogWarning($"Detected item without path: {nodeIncomingItem.SelectSingleNode("div")?.OuterXml ?? "unknown"}");
                    var nodePath = nodeIncomingItem.SelectSingleNode("div//span[contains(@class, 'path')]");
                    if (nodePath != null)
                    {
                        path = "/";
                        nodePath.InnerText = path;
                    }
                };
                if(string.IsNullOrEmpty(linkName)){
                    appLogger.LogWarning($"Detected item without linkname: {nodeIncomingItem.SelectSingleNode("div")?.OuterXml??""}");
                    var nodeLinkName = nodeIncomingItem.SelectSingleNode("div//span[contains(@class, 'linkname')]");
                    if (nodeLinkName!= null)
                    {
                        linkName = "Unnamed";
                        nodeLinkName.InnerText = linkName;
                    }
                }
                if (string.IsNullOrEmpty(linkName) || string.IsNullOrEmpty(path))
                {
                    validationErrorDetailsList.Add(nodeIncomingItem.SelectSingleNode("div").OuterXml);
                };
            }
            if (validationErrorDetailsList.Count > 0)
            {
                appLogger.LogError($"Incoming hierarchy has errors: {string.Join('\n', [.. validationErrorDetailsList])} errors found");
                HandleError(ReturnTypeEnum.Json, "Hierarchy is missing linkname or path", string.Join('\n', [.. validationErrorDetailsList]));
            }



            // Retrieve the hierarchy from the Taxxor Data Store
            XmlDocument outputChannelHierarchy = await DocumentStoreService.FilingData.LoadHierarchy(projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);

            // Retrieve the overview of all the available sections from the Taxxor Data Store 
            XmlDocument xmlSourceDataOverview = await DocumentStoreService.FilingData.SourceDataOverview(projectVars.projectId, projectVars.versionId, true);

            // Render a new hierarchy file based on the combination of the data we have saved and the data that was already present in the Taxxor Data Store
            XsltArgumentList xsltArgs = new XsltArgumentList();
            xsltArgs.AddParam("doc-hierarchy", "", outputChannelHierarchy);
            xsltArgs.AddParam("doc-allitems", "", xmlSourceDataOverview);
            var xmlConvertedSiteStructureHierarchy = TransformXmlToDocument(xmlPostedHierarchy, "cms_xsl_hierarchymanager-save-1", xsltArgs);
            if (debugRoutine) saved = await SaveXmlDocument(xmlConvertedSiteStructureHierarchy, logRootPathOs + "/_hierarchy-saved-xml.xml");

            // Test if we have correctly transformed the hierarchy leaving as many elements in place as we received in the incoming hierarchy
            var nodeListItemsGenerated = xmlConvertedSiteStructureHierarchy.SelectNodes("//item");
            if (nodeListItemsGenerated.Count != nodeListIncomingItems.Count)
            {
                HandleError("Error: The hierarchy was not correctly transformed", $"Stack-trace: {GetStackTrace()}");
            }
            else
            {
                appLogger.LogInformation($"** Hierarchy validation succesful (generated items: {nodeListItemsGenerated.Count}, incoming items: {nodeListIncomingItems.Count}) **");
            }

            // Save the hierarchy in the Taxxor Data Store
            XmlDocument xmlSaveResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.SaveHierarchy(xmlConvertedSiteStructureHierarchy, projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage, true, true);

            if (XmlContainsError(xmlSaveResult))
            {
                HandleError(xmlSaveResult);
            }
            else
            {
                // Clear the RBAC cache entry
                var hierarchyMetadataId = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                if (string.IsNullOrEmpty(hierarchyMetadataId)) HandleError("Could not find metadata id for hierarchy", $"stack-trace: {GetStackTrace()}");
                projectVars.rbacCache.ClearHierarchy(hierarchyMetadataId);

                // Update the in-memory CMS metadata content
                await UpdateCmsMetadata(projectVars.projectId);

                // Clear the complete content cache for paged media content
                ContentCache.Clear(projectVars.projectId, projectVars.outputChannelVariantId);

                // Construct a response message for the client
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully saved your hierarchy";
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = xmlSaveResult.SelectSingleNode("//debuginfo").InnerText;
                }

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
        }

        /// <summary>
        /// Updates a single hierarchy element
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task UpdateCmsHierarchyItem(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            await DummyAwaiter();

            HandleError("Needs to be implemented", $"stack-trace: {GetStackTrace()}");
        }

        /// <summary>
        /// Retrieves the data of a single hierarchy element
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GetCmsHierarchyItem(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            await DummyAwaiter();

            HandleError("Needs to be implemented", $"stack-trace: {GetStackTrace()}");
        }

        /// <summary>
        /// Generates a report containing the consequences of removing a section XML fragment from the system
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RenderCmsHierarchyDeleteResultReport(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var dataRefsToDelete = context.Request.RetrievePostedValue("datarefs", @"^[a-zA-Z_\|\-#',\.\d:\/]{4,4096}$", true, reqVars.returnType);

            // Render a report containing the consequences of the items that the user wants to delete
            List<string> dataReferencesToDelete = new List<string>();
            switch (dataRefsToDelete)
            {
                case string a when a.Contains("|"):
                    dataReferencesToDelete = dataRefsToDelete.Split('|').ToList();
                    break;

                case string b when b.Contains(","):
                    dataReferencesToDelete = dataRefsToDelete.Split(',').ToList();
                    break;

                default:
                    dataReferencesToDelete.Add(dataRefsToDelete);

                    break;
            }


            var deleteResult = await RenderDeleteFilingSectionReport(dataReferencesToDelete);
            if (!deleteResult.Success)
            {
                HandleError("Could not remove section", $"debuginfo: {deleteResult.DebugInfo}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                // Render HTML content
                XsltArgumentList xsltArgs = new XsltArgumentList();
                var htmlReport = TransformXml(deleteResult.XmlPayload, "cms_xsl_delete-report", xsltArgs);

                // Construct a response message for the client
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully rendered report";
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = $"dataRefsToDelete: {dataRefsToDelete}";
                }
                jsonData.result.html = htmlReport;

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }

        }

        /// <summary>
        /// Renders a report containing the consequences when a section XML is being removed from the system
        /// </summary>
        /// <param name="dataRefs"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RenderDeleteFilingSectionReport(List<string> dataRefs)
        {
            var xmlReport = new XmlDocument();
            xmlReport.AppendChild(xmlReport.CreateElement("report"));

            try
            {
                // Compile one document based on all the output channels
                var xmlOutputChannelHierarchies = await RenderOutputChannelHierarchyOverview();

                if (XmlContainsError(xmlOutputChannelHierarchies)) return new TaxxorReturnMessage(xmlOutputChannelHierarchies);

                foreach (string dataRef in dataRefs)
                {
                    // Find information about the element to delete
                    var nodeSectionToRemove = xmlOutputChannelHierarchies.SelectSingleNode($"/hierarchies/item_overview/items/item[@data-ref={GenerateEscapedXPathString(dataRef)}]");
                    if (nodeSectionToRemove == null)
                    {
                        return new TaxxorReturnMessage(false, "Could not find information about the section to delete", $"data-ref: {dataRef}, stack-trace: {GetStackTrace()}");
                    }
                    var nodeSectionToRemoveTitle = nodeSectionToRemove.SelectSingleNode("h1");
                    if (nodeSectionToRemoveTitle == null)
                    {
                        return new TaxxorReturnMessage(false, "Could not find title of the section to delete", $"data-ref: {dataRef}, stack-trace: {GetStackTrace()}");
                    }

                    var nodeItemToDelete = xmlReport.CreateElement("itemtodelete");
                    SetAttribute(nodeItemToDelete, "data-ref", dataRef);

                    var nodeItemToDeleteTitle = xmlReport.CreateElement("name");
                    nodeItemToDeleteTitle.InnerText = nodeSectionToRemoveTitle.InnerText;
                    nodeItemToDelete.AppendChild(nodeItemToDeleteTitle);

                    var nodeItemReferences = xmlReport.CreateElement("references");

                    // Find the references
                    foreach (XmlNode nodeHierarchyItem in xmlOutputChannelHierarchies.SelectNodes($"/hierarchies/output_channel//item[@data-ref={GenerateEscapedXPathString(dataRef)}]"))
                    {
                        // Retrieve information about this hierarchy item
                        var nodeLinkName = nodeHierarchyItem.SelectSingleNode("web_page/linkname");
                        if (nodeLinkName == null) return new TaxxorReturnMessage(false, "Could not find title of the referenced section", $"data-ref: {dataRef}, stack-trace: {GetStackTrace()}");

                        var nodeItemReference = xmlReport.CreateElement("reference");
                        SetAttribute(nodeItemReference, "from", "hierarchy");
                        SetAttribute(nodeItemReference, "children", nodeHierarchyItem.SelectNodes("sub_items//item").Count.ToString());

                        // Add information about the output channel that we are dealing with
                        var nodeOutputChannelRoot = nodeHierarchyItem.SelectSingleNode("ancestor::output_channel");
                        if (nodeOutputChannelRoot != null)
                        {
                            SetAttribute(nodeItemReference, "id", GetAttribute(nodeOutputChannelRoot, "id"));
                            SetAttribute(nodeItemReference, "type", GetAttribute(nodeOutputChannelRoot, "type"));
                            SetAttribute(nodeItemReference, "lang", GetAttribute(nodeOutputChannelRoot, "lang"));
                            SetAttribute(nodeItemReference, "name", nodeOutputChannelRoot.SelectSingleNode("name").InnerText);
                        }

                        var nodeItemReferenceTitle = xmlReport.CreateElement("name");
                        nodeItemReferenceTitle.InnerText = nodeLinkName.InnerText;
                        nodeItemReference.AppendChild(nodeItemReferenceTitle);

                        nodeItemReferences.AppendChild(nodeItemReference);
                    }

                    nodeItemToDelete.AppendChild(nodeItemReferences);

                    xmlReport.DocumentElement.AppendChild(nodeItemToDelete);
                }

                // TODO: Find links from content that point to hierarchy elements that we are about to delete ("intra document section links")

                // TODO: Find links in the content that refer to this content (for example shared content nodes "intra document element links" (for the website content))

                return new TaxxorReturnMessage(true, "Successfully removed filing sections", xmlReport);

            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "Could not render section remove report", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Renders the form fields for the hierarchy item properties modal dialog
        /// </summary>
        /// <returns></returns>
        public static string RenderReportingRequirementsFormFields(ProjectVariables projectVars)
        {
            XsltArgumentList xslArgs = new();
            xslArgs.AddParam("projectid", "", projectVars.projectId);
            return TransformXml(xmlApplicationConfiguration, "cms_xsl_hierarchymanager-reportingrequirements", xslArgs);
        }

        /// <summary>
        /// Adds or removes a reporting requirement scheme to the filing data
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task SetFilingDataReportingRequirementMeta(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            var did = request.RetrievePostedValue("did", RegexEnum.Loose, true, ReturnTypeEnum.Json);
            var scheme = request.RetrievePostedValue("scheme", @"^[a-zA-Z_\-\d,|]{1,512}$", true, ReturnTypeEnum.Json);
            var action = request.RetrievePostedValue("action", @"^(add|remove|set)$", true, ReturnTypeEnum.Json);
            var includeChildSections = request.RetrievePostedValue("includechildsections", @"^(true|false)$", true, ReturnTypeEnum.Json);
            var metaDataType = "reportingrequirement";


            //
            // => Test if sections within the scope of this request are locked by another user
            //
            var numberOfLocks = 0;
            var sectionsInScopeLocked = false;
            // - Only test if this particular section is locked by another user
            sectionsInScopeLocked = FilingLockStore.LockExists(projectVars, did, "filing");
            if (sectionsInScopeLocked) numberOfLocks++;

            if (!sectionsInScopeLocked && includeChildSections == "true")
            {
                // - Find out all the section id's in scope via the output channel hierarchy
                var xmlOutputChannelHierarchy = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadHierarchy(projectVars.projectId, projectVars.outputChannelVariantId, debugRoutine);

                var nodeListItems = xmlOutputChannelHierarchy.SelectNodes($"//item[@id={GenerateEscapedXPathString(did)}]/sub_items//item");
                foreach (XmlNode nodeItem in nodeListItems)
                {
                    var sectionId = nodeItem.GetAttribute("id") ?? "";
                    if (sectionId != "")
                    {
                        var currentSectionLocked = FilingLockStore.LockExists(projectVars, sectionId, "filing");
                        if (!sectionsInScopeLocked && currentSectionLocked) sectionsInScopeLocked = true;

                        if (currentSectionLocked) numberOfLocks++;
                    }
                    else
                    {
                        appLogger.LogWarning($"Unable to locate item id to set the filing reporting requirements");
                    }
                }
            }

            if (numberOfLocks > 0)
            {
                HandleError($"Could not proceed because there are {numberOfLocks.ToString()} sections locked within the scope of your request. Updating metadata is only available when there are no sections locked for editing.", $"stack-trace: {GetStackTrace()}");
            }

            //
            // => Update the metadata using the API on the Project Data Store
            //
            var dataToPost = new Dictionary<string, string>();

            dataToPost.Add("scheme", scheme);
            dataToPost.Add("action", action);
            dataToPost.Add("metadatatype", metaDataType);
            dataToPost.Add("includechildsections", includeChildSections);

            dataToPost.Add("pid", projectVars.projectId);
            dataToPost.Add("projectid", projectVars.projectId);

            dataToPost.Add("vid", "latest");
            // Data types supported are "text", "config" - but since we need the content for the editor we will fix it to "text"
            dataToPost.Add("type", "text");
            dataToPost.Add("did", did);
            dataToPost.Add("ctype", projectVars.editorContentType);
            dataToPost.Add("rtype", projectVars.reportTypeId);
            dataToPost.Add("octype", projectVars.outputChannelType);
            dataToPost.Add("ocvariantid", projectVars.outputChannelVariantId);
            dataToPost.Add("oclang", projectVars.outputChannelVariantLanguage);

            XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorfilingmetadata", dataToPost, debugRoutine);


            if (XmlContainsError(responseXml))
            {
                await context.Response.Error(responseXml, reqVars.returnType, true);
            }
            else
            {
                // Construct a response message for the client
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully set metadata on filing section";
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = "You can place debug information here";
                }

                // Convert to JSON and return it to the client
                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }

        }

        /// <summary>
        /// Imports another outputchannel hierarchy and leaves the accesscontrol settings in place
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task ImportHierarchy(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var sourceOutputChannelVariantId = request.RetrievePostedValue("sourceocvariantid", RegexEnum.Default, true);
            var debugInfo = $"sourceOutputChannelVariantId: {sourceOutputChannelVariantId}, projectVars: {projectVars.DumpToString()}";
            var errorMessage = "";

            try
            {
                // Source is the hierarchy we want to import
                var sourceHierarchyId = RetrieveOutputChannelHierarchyMetadataId(
                    projectVars.editorId,
                    RetrieveOutputChannelTypeFromOutputChannelVariantId(projectVars.projectId, sourceOutputChannelVariantId),
                    sourceOutputChannelVariantId,
                    RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, sourceOutputChannelVariantId)
                );
                var nodeLocationSourceHierarchy = RetrieveOutputchannelHierarchyLocationXmlNode(projectVars.projectId, sourceHierarchyId);

                // Target is the hierarchy we want to replace
                var targetHierarchyId = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                var nodeLocationTargetHierarchy = RetrieveOutputchannelHierarchyLocationXmlNode(projectVars.projectId, targetHierarchyId);


                //
                // => Retrieve the hierarchies from the project data store
                //
                await MessageToCurrentClient("HierarchyImportProgress", "Retrieving output channel hierarchies");
                var xmlHierarchySource = await DocumentStoreService.FilingData.Load<XmlDocument>(nodeLocationSourceHierarchy, debugRoutine);
                var xmlHierarchyTarget = await DocumentStoreService.FilingData.Load<XmlDocument>(nodeLocationTargetHierarchy, debugRoutine);

                if (debugRoutine)
                {
                    Console.WriteLine("* Hierarchies to work with *");
                    Console.WriteLine($"-> Source ({sourceHierarchyId}):");
                    Console.WriteLine(TruncateString(xmlHierarchySource.OuterXml, 500).Trim());
                    Console.WriteLine($"-> Target ({targetHierarchyId}):");
                    Console.WriteLine(TruncateString(xmlHierarchyTarget.OuterXml, 500).Trim());
                    Console.WriteLine($"Debug info: {debugInfo}");
                    Console.WriteLine("****************************");
                }


                //
                // => Create a dictionary <dataref, accesscontrolinfo> for each item in the original target hierarchy
                //
                await MessageToCurrentClient("HierarchyImportProgress", "Retrieve access control definitions");
                var accessControlRecords = new Dictionary<string, List<RbacAccessRecord>>();
                var hierarchyItemsInfo = new Dictionary<string, string>();
                var dataReferenceRoot = "";
                var existingResourceIds = new List<string>();

                var nodeListAllItems = xmlHierarchyTarget.SelectNodes("/items/structured//item");
                if (nodeListAllItems.Count == 0)
                {
                    HandleError("Target hierarchy doesn't contain any items", debugInfo);
                }
                var loopCount = 0;
                foreach (XmlNode nodeItem in nodeListAllItems)
                {
                    var itemId = nodeItem.GetAttribute("id");
                    var dataReference = nodeItem.GetAttribute("data-ref");

                    if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(dataReference))
                    {
                        appLogger.LogWarning($"Could not find item id or item data reference. nodeItem: {TruncateString(nodeItem.OuterXml, 200)}");
                    }
                    else
                    {
                        if (loopCount == 0) dataReferenceRoot = dataReference;
                        if (!hierarchyItemsInfo.ContainsKey(itemId))
                        {
                            hierarchyItemsInfo.Add(itemId, dataReference);
                        }
                    }
                    loopCount++;
                }

                var xmlAccessRecords = await AccessControlService.ListAccessRecords();
                if (debugRoutine) await xmlAccessRecords.SaveAsync($"{logRootPathOs}/allaccessrecords.xml", false);


                var xpathAccessRecords = $"/accessrecords/accessRecord[contains(resourceRef/@ref, '__{projectVars.projectId}')]";
                var nodeListAccessRecords = xmlAccessRecords.SelectNodes(xpathAccessRecords);
                foreach (XmlNode nodeAccessRecord in nodeListAccessRecords)
                {
                    var nodeResourceReference = nodeAccessRecord.SelectSingleNode("resourceRef");
                    if (nodeResourceReference == null)
                    {
                        appLogger.LogWarning("Could not locate resourceReference");
                    }
                    else
                    {
                        var resourceId = GetAttribute(nodeResourceReference, "ref");
                        if (string.IsNullOrEmpty(resourceId))
                        {
                            appLogger.LogWarning("Could not locate resourceId");
                        }
                        else
                        {
                            var itemIdFromResourceId = RegExpReplace(@"^get__taxxoreditor__(.*?)__.*$", resourceId, "$1");
                            // if (debugRoutine) appLogger.LogInformation($"- itemIdFromResourceId: {itemIdFromResourceId}");
                            if (hierarchyItemsInfo.ContainsKey(itemIdFromResourceId))
                            {
                                // if (debugRoutine) appLogger.LogDebug($"Match and adding itemIdFromResourceId: {itemIdFromResourceId}");

                                var accessRecordDisabledString = nodeAccessRecord.GetAttribute("disabled") ?? "false";
                                var accessRecordDisabled = (accessRecordDisabledString == "true");

                                string? userGroupId = null;
                                var nodeUserId = nodeAccessRecord.SelectSingleNode("userRef");
                                if (nodeUserId != null)
                                {
                                    userGroupId = GetAttribute(nodeUserId, "ref");
                                }
                                else
                                {
                                    var nodeGroupId = nodeAccessRecord.SelectSingleNode("groupRef");
                                    if (nodeGroupId != null)
                                    {
                                        userGroupId = GetAttribute(nodeGroupId, "ref");
                                    }
                                }

                                if (string.IsNullOrEmpty(userGroupId))
                                {
                                    appLogger.LogError("Could not find a user or group");
                                }
                                else
                                {
                                    var userGroupRole = "";
                                    var nodeUserRole = nodeAccessRecord.SelectSingleNode("roleRef");
                                    if (nodeUserRole != null)
                                    {
                                        userGroupRole = nodeUserRole.GetAttribute("ref");
                                    }

                                    if (string.IsNullOrEmpty(userGroupRole))
                                    {
                                        appLogger.LogWarning($"Could not create new RBAC access record for {itemIdFromResourceId} because we were unable to determine the role reference");
                                    }
                                    else
                                    {
                                        var dataReference = hierarchyItemsInfo[itemIdFromResourceId];
                                        appLogger.LogInformation($"Adding access record. itemIdFromResourceId: {itemIdFromResourceId}, dataReference: {dataReference}, userGroupId: {userGroupId}, userGroupRole: {userGroupRole}, resourceId: {resourceId}");

                                        // Create an access record with the information about the target access control settings
                                        var rbacAccessRecord = new RbacAccessRecord(userGroupId, userGroupRole, resourceId, !accessRecordDisabled);

                                        // Only reset the inheritance on the root element of the hierarchy
                                        if (dataReference == dataReferenceRoot) rbacAccessRecord.resetinheritance = true;

                                        // Append the list of existing resource ID's so we can use it te remove the access control records we no longer need
                                        existingResourceIds.Add(resourceId);

                                        // Append the access record that we have found to the list of access records per data reference
                                        if (accessControlRecords.ContainsKey(dataReference))
                                        {
                                            accessControlRecords[dataReference].Add(rbacAccessRecord);
                                        }
                                        else
                                        {
                                            var aclList = new List<RbacAccessRecord>();
                                            aclList.Add(rbacAccessRecord);
                                            accessControlRecords.Add(dataReference, aclList);
                                        }
                                    }
                                }

                            }
                        }
                    }
                }



                //
                // => Rework source hierarchy so that each item has a unique ID
                //
                await MessageToCurrentClient("HierarchyImportProgress", "Prepare hierarchy for import");
                var nodeListSourceItems = xmlHierarchySource.SelectNodes("/items/structured//item");
                foreach (XmlNode nodeItem in nodeListSourceItems)
                {
                    var dataReference = nodeItem.GetAttribute("data-ref");
                    if (!string.IsNullOrEmpty(dataReference))
                    {
                        var newItemId = Path.GetFileNameWithoutExtension(dataReference) + RandomString(8);
                        nodeItem.SetAttribute("id", newItemId);

                        //
                        // => Rework the access control records for this item so that they contain the new item id
                        //
                        if (accessControlRecords.ContainsKey(dataReference))
                        {
                            var accessRecords = accessControlRecords[dataReference];
                            foreach (var accessRecord in accessRecords)
                            {
                                accessRecord.ChangeHierarchyItemId(newItemId);
                            }
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"No data-ref attribute on item {nodeItem.OuterXml}");
                    }
                }

                if (debugRoutine)
                {
                    Console.WriteLine("* Modified source hierarchy *");
                    Console.WriteLine("-> Source:");
                    Console.WriteLine(TruncateString(xmlHierarchySource.OuterXml, 1000).Trim());
                    Console.WriteLine("****************************");
                }


                //
                // => Create new ACL entries in the access control service by looping through the source hierarchy and mapping on dataref
                //
                await MessageToCurrentClient("HierarchyImportProgress", "Setup access control");
                var accessRecordsToAdd = new List<RbacAccessRecord>();
                foreach (KeyValuePair<string, List<RbacAccessRecord>> entry in accessControlRecords)
                {
                    // do something with entry.Value or entry.Key
                    accessRecordsToAdd.AddRange(entry.Value);
                }

                if (accessRecordsToAdd.Count > 0)
                {
                    var createAccessRecordResult = await AccessControlService.AddAccessRecord(accessRecordsToAdd, false);
                    if (!createAccessRecordResult) HandleError($"Unable to add access control information for the hierarchy to import", debugInfo);

                    var editAccessRecordResult = await AccessControlService.EditAccessRecord(accessRecordsToAdd, false);
                    if (!editAccessRecordResult) HandleError($"Unable to change access control information for the hierarchy to import", debugInfo);
                }

                //
                // => Save the new hierarchy on the project data store
                //
                await MessageToCurrentClient("HierarchyImportProgress", "Save imported hierarchy");
                var successSave = await DocumentStoreService.FilingData.Save<bool>(xmlHierarchySource, nodeLocationTargetHierarchy, debugRoutine);
                if (!successSave)
                {
                    // TODO: Remove the access control records that we have just added

                    HandleError("Failed to store the new hierarchy on the server", debugInfo);
                }


                //
                // => Remove the ACL entries used in the original hierarchy from the access control service
                //
                await MessageToCurrentClient("HierarchyImportProgress", "Remove old access control information");
                var deleteSuccessCount = 0;
                var deleteErrorCount = 0;
                foreach (var resourceId in existingResourceIds)
                {
                    var deleteSuccess = await AccessControlService.DeleteResource(resourceId);
                    if (deleteSuccess)
                    {
                        deleteSuccessCount++;
                    }
                    else
                    {
                        deleteErrorCount++;
                    }
                }

                if (deleteErrorCount > 0)
                {
                    appLogger.LogWarning($"Unable to delete all access records from the access control service. deleteErrorCount: {deleteErrorCount}");
                }


                //
                // => Commit the change in the version control system
                //
                await MessageToCurrentClient("HierarchyImportProgress", "Store the changes in the auditor view");
                // Construct the commit message
                XmlDocument? xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "u"; // Update
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = $"Output channel hierarchy ({projectVars.reportTypeId})"; // Linkname
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = projectVars.reportTypeId; // Page ID
                var message = xmlCommitMessage.DocumentElement.InnerXml;

                // Execute the commit request
                var commitResult = await DocumentStoreService.VersionControl.GitCommit(projectVars.projectId, "reportdataroot", message, debugRoutine);
                if (!commitResult.Success)
                {
                    appLogger.LogError($"Unable to commit hierarchy change to version control system. Message: {commitResult.Message}, DebugInfo: {commitResult.DebugInfo}");
                }

                //
                // => Clear RBAC cache
                //
                var hierarchyMetadataId = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                if (string.IsNullOrEmpty(hierarchyMetadataId)) HandleError("Could not find metadata id for hierarchy", $"stack-trace: {GetStackTrace()}");
                projectVars.rbacCache.ClearHierarchy(hierarchyMetadataId);


                //
                // => Response to client
                //
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = $"Successfully imported hierarchy with identifier {sourceOutputChannelVariantId}";
                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
            catch (Exception ex)
            {
                errorMessage = $"There was an error importing the hierarchy with identifier {sourceOutputChannelVariantId}";
                appLogger.LogError(ex, errorMessage);
                HandleError(errorMessage, ex.ToString());
            }
        }

        /// <summary>
        /// Clones the content of the source language in the same datareference to the target language
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task CloneSectionContentLanguage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var sourceLang = request.RetrievePostedValue("sourcelang", @"^[a-zA-Z]{2,3}$", true);
            var targetLang = request.RetrievePostedValue("targetlang", @"^[a-zA-Z]{2,3}$", true);
            var includeChildrenString = request.RetrievePostedValue("includechildren", @"^(true|false)$", false, ReturnTypeEnum.Json, "false");
            var includeChildren = (includeChildrenString == "true");

            var xmlCloneLangResult = await DocumentStoreService.FilingData.CloneSectionLanguageData(projectVars.projectId, projectVars.did, sourceLang, targetLang, includeChildren, true);

            if (XmlContainsError(xmlCloneLangResult))
            {
                HandleError(xmlCloneLangResult);
            }
            else
            {
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = $"Successfully cloned source language '{sourceLang}' to target '{targetLang}'";
                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
        }

    }

}