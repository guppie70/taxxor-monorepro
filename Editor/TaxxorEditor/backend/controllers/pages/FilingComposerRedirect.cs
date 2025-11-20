using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Redirects the user to the first available filing composer with the correct settings
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FilingComposerRedirect(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            // To please the compiler
            await DummyAwaiter();

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Make sure that the hierarchy we work with contains sensible data
            if (reqVars.xmlHierarchyStripped.SelectSingleNode("/items/structured/item") == null)
            {
                HandleError("You are not allowed to access this project", $"projectId: {projectVars.projectId}, xmlHierarchyStripped: <pre>{HtmlEncodeForDisplay(PrettyPrintXml(reqVars.xmlHierarchyStripped))}</pre>", 403);
            }

            // Construct the URL for the filing composer
            var baseUrl = RetrieveUrlFromHierarchy("cms_content-editor", reqVars.xmlHierarchyStripped);

            // Redirect variables
            var idFirstOpenInEditor = "";
            var ouputChannelVariantIdToUse = "";
            var dataReferenceMode = false;

            // Only continue if we have found a base url to redirect to
            if (string.IsNullOrEmpty(baseUrl))
            {
                HandleError("You are not allowed to access this project", $"baseUrl: null, projectId: {projectVars.projectId}, xmlHierarchyStripped: <pre>{HtmlEncodeForDisplay(PrettyPrintXml(reqVars.xmlHierarchyStripped))}</pre>", 403);
            }


            var dataReference = request.RetrievePostedValue("dataref", Framework.RegexEnum.FileName, false, Framework.ReturnTypeEnum.None, "");
            dataReferenceMode = !string.IsNullOrEmpty(dataReference);


            // var hierarchyRetrieveResult = await RetrieveOutputChannelHierarchiesMetaData(projectVars, reqVars, true);
            // if (!hierarchyRetrieveResult) appLogger.LogError($"Unable to retrieve all hierarchy data for user: {projectVars.currentUser.Id}");

            // appLogger.LogDebug("****** Before delay *******");
            // await PauseExecution(1000);
            // appLogger.LogDebug("****** After delay ****");

            appLogger.LogDebug("****** Start logic ****");

            // The page that needs to open up in the filing composer should be the second section on the hierarchy

            //
            // => Gather information about the hierarchies, output channels and access to sections
            //
            var dictAccessInformation = new Dictionary<string, string>();


            //
            // => Objects used for access summary
            //
            var dictOutputChannelInformation = new Dictionary<string, string>(); // Output channel ID, Output channel variant ID
            var outputChannelVariantIdsWitoutAnyRights = new List<string>();
            var outputChannelVariantIsWithoutEditRights = new List<string>();


            //
            // => Gather information about the output channels defined for this project
            //
            if (!string.IsNullOrEmpty(projectVars.editorId))
            {
                var nodeListOutputChannels = projectVars.xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(projectVars.editorId) + "]/output_channels");
                if (nodeListOutputChannels.Count == 0) nodeListOutputChannels = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(projectVars.editorId) + "]/output_channels");

                foreach (XmlNode nodeOutputChannels in nodeListOutputChannels)
                {
                    var nodeListOutputChannel = nodeOutputChannels.SelectNodes("output_channel");
                    foreach (XmlNode nodeOutputChannel in nodeListOutputChannel)
                    {
                        string? outputChannelType = GetAttribute(nodeOutputChannel, "type");
                        var nodeListOutputChannelVariants = nodeOutputChannel.SelectNodes("variants/variant");
                        foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                        {
                            if (dataReferenceMode)
                            {
                                if (nodeVariant.GetAttribute("lang") == projectVars.outputChannelVariantLanguage)
                                {
                                    dictOutputChannelInformation.Add(nodeVariant.Attributes["id"].Value, nodeVariant.Attributes["metadata-id-ref"].Value);
                                }
                            }
                            else
                            {
                                dictOutputChannelInformation.Add(nodeVariant.Attributes["id"].Value, nodeVariant.Attributes["metadata-id-ref"].Value);
                            }
                        }
                    }
                }
            }


            //
            // => Phase 1: test if this user has edit rights somewhere
            //

            // Loop through all the outputchannel hierarchies and return the first one it finds where this user has something to access in
            foreach (var metadataPair in projectVars.cmsMetaData)
            {
                string metadataKeyHierarchy = metadataPair.Key;


                // MetaData val = metadataPair.Value;
                // appLogger.LogInformation($"metadataKeyHierarchy: {metadataKeyHierarchy}, metadata => {val.AsString()}");

                var xmlFilingDocumentHierarchy = projectVars.rbacCache.GetHierarchy(metadataKeyHierarchy, "stripped");

                if (xmlFilingDocumentHierarchy != null)
                {
                    // appLogger.LogCritical($"* Usable items: {xmlFilingDocumentHierarchy.SelectNodes("/items/structured//item").Count}");

                    if (xmlFilingDocumentHierarchy.SelectNodes("/items/structured//item").Count > 0)
                    {
                        if (dataReferenceMode)
                        {
                            //
                            // => Data reference mode
                            //

                            var nodeVariant = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/variants/variant[@metadata-id-ref='{metadataKeyHierarchy}']");
                            var outputChannelLang = nodeVariant.GetAttribute("lang");
                            if (outputChannelLang == projectVars.outputChannelVariantLanguage)
                            {
                                var hierarchyXpath = $"/items/structured//item[@data-ref={GenerateEscapedXPathString(dataReference)} and @id]";
                                var nodeItem = xmlFilingDocumentHierarchy.SelectSingleNode(hierarchyXpath);
                                if (nodeItem != null)
                                {
                                    var outputChannelVariantIds = RetrieveOutputVariantId(metadataKeyHierarchy);
                                    if (outputChannelVariantIds.Count > 0)
                                    {
                                        projectVars.outputChannelVariantId = outputChannelVariantIds[0];

                                        foreach (var outputChannelVariantId in outputChannelVariantIds)
                                        {
                                            // Add the information to the dictionary
                                            dictAccessInformation.Add(outputChannelVariantId, nodeItem.GetAttribute("id"));
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            //
                            // => Normal mode
                            //
                            var hierarchyXpath = "/items/structured/item/sub_items/item[1]";
                            if (metadataKeyHierarchy.Contains("web"))
                            {
                                hierarchyXpath = "/items/structured/item";
                            }

                            var nodeIdFirstOpenInEditor = xmlFilingDocumentHierarchy.SelectSingleNode(hierarchyXpath);
                            if (nodeIdFirstOpenInEditor != null)
                            {
                                var currentIdFirstOpenInEditor = GetAttribute(nodeIdFirstOpenInEditor, "id");
                                if (debugRoutine) appLogger.LogCritical($"** (secondary) currentIdFirstOpenInEditor: {currentIdFirstOpenInEditor}");


                                var outputChannelVariantIds = RetrieveOutputVariantId(metadataKeyHierarchy);
                                if (outputChannelVariantIds.Count > 0)
                                {
                                    projectVars.outputChannelVariantId = outputChannelVariantIds[0];

                                    foreach (var outputChannelVariantId in outputChannelVariantIds)
                                    {
                                        // Add the information to the dictionary
                                        dictAccessInformation.Add(outputChannelVariantId, nodeIdFirstOpenInEditor.GetAttribute("id"));
                                    }
                                }

                            }

                        }


                    }
                    else
                    {
                        // No editable items found for this output channel
                    }
                }
                else
                {
                    var outputChannelVariantId = dictOutputChannelInformation.FirstOrDefault(x => x.Value == metadataKeyHierarchy).Key;

                    outputChannelVariantIsWithoutEditRights.Add(outputChannelVariantId);
                }
            }

            //
            // => Phase 2: Test if the user has viewing rights somewhere in the output channels
            //
            if (dictAccessInformation.Count == 0 && !dataReferenceMode)
            {
                foreach (var metadataPair in projectVars.cmsMetaData)
                {
                    string metadataKeyHierarchy = metadataPair.Key;

                    var hierarchyXpath = "/items/structured/item/sub_items/item[1]";
                    if (metadataKeyHierarchy.Contains("web"))
                    {
                        hierarchyXpath = "/items/structured/item";
                    }
                    hierarchyXpath += "[permissions/permission/@id='view' or permissions/permission/@id='all']";
                    var xmlFilingDocumentHierarchy = new XmlDocument();
                    xmlFilingDocumentHierarchy = projectVars.cmsMetaData[metadataKeyHierarchy].Xml;
                    var nodeIdFirstOpenInEditor = xmlFilingDocumentHierarchy.SelectSingleNode(hierarchyXpath);
                    var editableForUser = nodeIdFirstOpenInEditor != null;
                    if (editableForUser)
                    {
                        var outputChannelVariantIds = RetrieveOutputVariantId(metadataKeyHierarchy);
                        if (outputChannelVariantIds.Count > 0)
                        {
                            projectVars.outputChannelVariantId = outputChannelVariantIds[0];

                            foreach (var outputChannelVariantId in outputChannelVariantIds)
                            {
                                // Add the information to the dictionary
                                dictAccessInformation.Add(outputChannelVariantId, nodeIdFirstOpenInEditor.GetAttribute("id"));
                            }

                        }

                        // if (!string.IsNullOrEmpty(outputChannelVariantId))
                        // {
                        //     projectVars.outputChannelVariantId = outputChannelVariantId;

                        //     // Add the information to the dictionary
                        //     dictAccessInformation.Add(projectVars.outputChannelVariantId, nodeIdFirstOpenInEditor.GetAttribute("id"));
                        // }
                    }
                }
            }

            //
            // => Calculate output channel variant ID's that of output channels that this user does not have access to at all
            //
            foreach (var pair in dictOutputChannelInformation)
            {
                var outputChannelVariantId = pair.Key;
                if (!dictAccessInformation.ContainsKey(outputChannelVariantId) && !outputChannelVariantIsWithoutEditRights.Contains(outputChannelVariantId)) outputChannelVariantIdsWitoutAnyRights.Add(outputChannelVariantId);
            }

            //
            // => Log output channels that this user does not have edit rights
            //
            if (outputChannelVariantIsWithoutEditRights.Count > 0 || outputChannelVariantIdsWitoutAnyRights.Count > 0)
            {
                var overview = new List<string>();
                if (outputChannelVariantIdsWitoutAnyRights.Count > 0) overview.Add("no-access: [" + string.Join(", ", outputChannelVariantIdsWitoutAnyRights) + "]");
                if (outputChannelVariantIsWithoutEditRights.Count > 0) overview.Add("no-edit: [" + string.Join(", ", outputChannelVariantIsWithoutEditRights) + "]");
                appLogger.LogWarning($"User {projectVars.currentUser.Id ?? ""} has partial access to project ID {projectVars.projectId} ({string.Join(", ", overview)})");
            }

            //
            // => Determine the output channel variant ID and the section ID to use
            //

            // Select the default section ID and OutputChannel Variant ID's to use
            ouputChannelVariantIdToUse = projectVars.outputChannelVariantId;
            idFirstOpenInEditor = projectVars.idFirstEditablePage;


            // Loop through the information we have gathered from the hierarchies and select the first element that makes sense
            var foundSensibleSet = false;
            foreach (KeyValuePair<string, string> entry in dictAccessInformation)
            {
                if (!foundSensibleSet)
                {
                    var outputChannelVariantId = entry.Key;
                    var itemId = entry.Value;
                    if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(outputChannelVariantId))
                    {
                        ouputChannelVariantIdToUse = outputChannelVariantId;
                        idFirstOpenInEditor = itemId;
                        foundSensibleSet = true;
                    }
                }
            }


            // Attempt to use a previously stored outputchannel ID
            var userPreferenceHierarchyIdToShow = projectVars.currentUser.RetrieveUserPreferenceKey("hierarchyoutputchannel-" + projectVars.projectId);
            if (!string.IsNullOrEmpty(userPreferenceHierarchyIdToShow) && userPreferenceHierarchyIdToShow.Contains("ocvariantid"))
            {
                var storedOuputChannelVariantId = RegExpReplace(@"^.*ocvariantid=(.*?)\:.*$", userPreferenceHierarchyIdToShow, "$1");

                // Check if we can find this entry in the dictionary
                if (dictAccessInformation.ContainsKey(storedOuputChannelVariantId))
                {
                    ouputChannelVariantIdToUse = storedOuputChannelVariantId;
                    idFirstOpenInEditor = dictAccessInformation[storedOuputChannelVariantId];
                }
            }


            // if (dataReferenceMode)
            // {
            //     HandleError(reqVars, $"DataRerefenceMode (ouputChannelVariantIdToUse: {ouputChannelVariantIdToUse}, idFirstOpenInEditor: {idFirstOpenInEditor})", $"projectVars: {projectVars.DumpToString()}");
            // }


            //
            // => Redirect the user
            //
            if (string.IsNullOrEmpty(idFirstOpenInEditor))
            {
                HandleError(ReturnTypeEnum.Html, "You do not have sufficient access rights to edit a section in this project", "", 403);
            }
            else
            {


                var redirectQuerystring = $"did={idFirstOpenInEditor}&ocvariantid={ouputChannelVariantIdToUse}";
                var redirectUrl = $"{baseUrl}&{redirectQuerystring}";

                if (debugRoutine && reqVars.rawUrl.StartsWith("https://editor/") && !IsRunningInDocker())
                {
                    var errorResponseToClient = $"About to redirect to <a href=\"{redirectUrl}\">{redirectUrl}</a> to access CMS project";
                    await context.Response.OK($"<h1>Redirect instruction</h1><p>{errorResponseToClient}</p><p>{Framework.RetrieveAllSessionData(Framework.ReturnTypeEnum.Html)}</p>");
                }
                else
                {
                    // Redirect to the editor page
                    response.Redirect(redirectUrl);
                }
            }


            /// <summary>
            /// Helper function to retrieve the ourtputchannel variant ID based on the metadata hierarchy ID
            /// </summary>
            /// <param name="metadataKeyHierarchy"></param>
            /// <returns></returns>
            List<string> RetrieveOutputVariantId(string metadataKeyHierarchy)
            {
                var outputChannelVariantIds = new List<string>();
                var xPath = $"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/variants/variant[@metadata-id-ref='{metadataKeyHierarchy}']";
                var nodeListVariants = xmlApplicationConfiguration.SelectNodes(xPath);
                foreach (XmlNode nodeVariant in nodeListVariants)
                {
                    var outputVariantId = nodeVariant.GetAttribute("id");
                    if (!string.IsNullOrEmpty(outputVariantId) && !outputChannelVariantIds.Contains(outputVariantId)) outputChannelVariantIds.Add(outputVariantId);
                }

                return outputChannelVariantIds;
            }
        }

    }
}