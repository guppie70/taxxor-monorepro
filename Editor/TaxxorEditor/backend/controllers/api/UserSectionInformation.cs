using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Returns JSON information about the permissions of all the sections that this user has access to
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveUserSectionInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var processOnlyOpenProjectsString = request.RetrievePostedValue("onlyopenprojects", RegexEnum.Boolean, false, reqVars.returnType, "true");
            var processOnlyOpenProjects = (processOnlyOpenProjectsString == "true");
            var includePermissionDetailsString = request.RetrievePostedValue("includepermissiondetails", RegexEnum.Boolean, false, reqVars.returnType, "false");
            var includePermissionDetails = (includePermissionDetailsString == "true");
            var includeNonEditableSectionsString = request.RetrievePostedValue("includenoneditablesections", RegexEnum.Boolean, false, reqVars.returnType, "false");
            var includeNonEditableSections = (includeNonEditableSectionsString == "true");
            var projectIdFilterString = request.RetrievePostedValue("projectidfilter", RegexEnum.Loose, false, reqVars.returnType, "ar23,q223"); // all | single project ID | comma separated list of project IDs
            var projectIdFilter = new List<string>();
            if (projectIdFilterString != "all")
            {
                if (projectIdFilterString.Contains(','))
                {
                    projectIdFilter = [.. projectIdFilterString.Split(',')];
                }
                else
                {
                    projectIdFilter.Add(projectIdFilterString);
                }
            }

            //
            // => Test if this user is authenticated or not
            //
            if (!projectVars.currentUser.IsAuthenticated || string.IsNullOrEmpty(projectVars.currentUser.Id))
            {
                HandleError(reqVars.returnType, "Not authenticated", $"RetrieveUserSectionInformation() failed because the user is not authenticated", 403);
            }
            else
            {
                //
                // => Retrieve the information about the sections that this user has access to
                //
                var userSectionInformationResult = await RetrieveUserSectionInformation(reqVars, projectVars, processOnlyOpenProjects, includePermissionDetails, includeNonEditableSections, projectIdFilter);

                //
                // => Render a response
                //
                if (userSectionInformationResult.Success)
                {
                    await response.OK(userSectionInformationResult.Payload, ReturnTypeEnum.Json, true);
                }
                else
                {
                    await response.Error(userSectionInformationResult, ReturnTypeEnum.Json, true);
                }
            }



        }

        /// <summary>
        /// Calculates JSON information about the permissions of all the sections that this user has access to
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="processOnlyOpenProjects"></param>
        /// <param name="includePermissionDetails"></param>
        /// <param name="includeNonEditableSections"></param>
        /// <returns></returns>
        public async static Task<TaxxorReturnMessage> RetrieveUserSectionInformation(RequestVariables reqVars, ProjectVariables projectVars, bool processOnlyOpenProjects, bool includePermissionDetails, bool includeNonEditableSections, List<string> projectIdFilter)
        {

            var debugRoutine = siteType != "prod";
            var enableCache = siteType == "prod" || siteType == "prev";
            var errorMessage = "There was an error rendering user section information";
            var debugHierarchyId = "";

            //
            // => Make sure that the projectVars contains an initiated RbacCache object
            //
            projectVars.rbacCache ??= new RbacCache(projectVars.currentUser.Id, null);

            //
            // => Determine a unique cache key identifier
            //
            var cacheKey = $"RetrieveUserSectionInformation_{projectVars.currentUser.Id}_{processOnlyOpenProjects}_{includePermissionDetails}_{includeNonEditableSections}_{string.Join(",", projectIdFilter)}";

            // try to get a previously generated JSON data from the UserSectionInformationCacheData cache
            if (enableCache && UserSectionInformationCacheData.TryGetValue(cacheKey, out string? cacheJSONData))
            {
                if (debugRoutine) appLogger.LogInformation($"=> Retrieved user section information from cache for user {projectVars.currentUser.Id} <=");
                return new TaxxorReturnMessage(true, "Successfully retrieved the user section information from the cache", cacheJSONData, "");
            }


            dynamic jsonResponseData = new ExpandoObject();
            try
            {
                jsonResponseData.projectids = new Dictionary<string, dynamic>();

                //
                // => Retrieve all open projects
                //

                var xPathProjects = (processOnlyOpenProjects) ? "/configuration/cms_projects/cms_project[versions/version/status/text() = 'open']" : "/configuration/cms_projects/cms_project";
                var nodeListProjects = xmlApplicationConfiguration.SelectNodes(xPathProjects);
                foreach (XmlNode nodeCurrentProject in nodeListProjects)
                {
                    var currentProjectId = GetAttribute(nodeCurrentProject, "id") ?? "";
                    if (debugRoutine) appLogger.LogInformation($"currentProjectId: {currentProjectId}");

                    var currentProjectName = nodeCurrentProject.SelectSingleNode("name")?.InnerText ?? "";

                    //
                    // => Test if the current user has access to the project
                    //
                    var hasAccessToProject = await UserHasAccessToProject(projectVars.currentUser.Id, currentProjectId);
                    if (!hasAccessToProject)
                    {
                        if (debugRoutine) appLogger.LogInformation($"User {projectVars.currentUser.Id} does not have access to open project {currentProjectId}");
                        continue;
                    }


                    if (!string.IsNullOrEmpty(currentProjectId))
                    {
                        //
                        // => Optionally apply the project id filter
                        //
                        if (projectIdFilter.Count > 0)
                        {
                            if (!projectIdFilter.Contains(currentProjectId))
                            {
                                if (debugRoutine) appLogger.LogInformation($"Project {currentProjectId} is not part of the project filter ({string.Join(",", projectIdFilter.ToArray())}), so we will not process it");
                                continue;
                            }
                        }


                        // var userHasAccessToAnOutputChannel = false;
                        dynamic projectAccessInformation = new ExpandoObject();

                        projectAccessInformation.name = currentProjectName;

                        projectAccessInformation.outputchannelvariantids = new Dictionary<string, dynamic>();

                        //
                        // => Retrieve the stripped hierarchy for all output channel hierarchies
                        //
                        var nodeListMetadata = xmlApplicationConfiguration.SelectNodes($"/configuration/cms_projects/cms_project[@id='{currentProjectId}']/metadata_system/metadata");
                        foreach (XmlNode nodeMetadata in nodeListMetadata)
                        {
                            var hierarchyId = nodeMetadata.Attributes["id"].Value;
                            var name = nodeMetadata.SelectSingleNode("name").InnerText;
                            var xmlNodeLocation = nodeMetadata.SelectSingleNode("location");
                            var locationType = xmlNodeLocation.Attributes["type"].Value;

                            var outputChannelVariantIds = RetrieveOutputVariantId(currentProjectId, hierarchyId);

                            try
                            {

                                if (hierarchyId == debugHierarchyId)
                                {
                                    appLogger.LogInformation($"Debugging {debugHierarchyId}");
                                }


                                // Log to console
                                // if (debugRoutine)
                                // {
                                //     appLogger.LogInformation($"id: {id}, name: {name}, setRbacCache: {setRbacCache.ToString().ToLower()}, url: {reqVars.rawUrl}");
                                // }


                                if (debugRoutine)
                                {
                                    appLogger.LogInformation($"Retrieve output channel hierarchy with ID {hierarchyId} and locationType {locationType} and incoming URL {reqVars.thisUrlPath}");
                                }

                                //
                                // => Retrieve the raw xml content for the metadata from the Taxxor Document Store
                                //
                                var xmlStrippedHierarchyDocument = new XmlDocument();

                                if (locationType == "file")
                                {
                                    if (debugRoutine) appLogger.LogInformation($"* Retrieve new raw hierarchy for {hierarchyId}");
                                    xmlStrippedHierarchyDocument = await DocumentStoreService.FilingData.Load<XmlDocument>(currentProjectId, xmlNodeLocation, true);
                                    if (XmlContainsError(xmlStrippedHierarchyDocument))
                                    {
                                        appLogger.LogError($"Failed to load metadata. id: {hierarchyId}, name: {name}, locationType: {locationType}, stack-trace: {GetStackTrace()}");
                                    }
                                }


                                //
                                // => Check if this person has access to this output channel at all
                                //
                                var hasAccessToOutputChannel = false;

                                var nodeHierarchyRootItem = xmlStrippedHierarchyDocument.SelectSingleNode("/items/structured/item");
                                if (nodeHierarchyRootItem == null)
                                {
                                    appLogger.LogError($"Could not find output channel hierarchy root node. id: {hierarchyId}, name: {name}, locationType: {locationType}, stack-trace: {GetStackTrace()}");
                                }

                                if (projectVars.currentUser.IsAuthenticated)
                                {
                                    XmlDocument xmlPermissions = await AccessControlService.RetrievePermissionsForResources($"get__taxxoreditor__{nodeHierarchyRootItem.GetAttribute("id")}__{currentProjectId},get__taxxoreditor__cms_content-editor,get__taxxoreditor__cms_project-details,get__taxxoreditor__cms-overview,root");
                                    if (XmlContainsError(xmlPermissions))
                                    {
                                        appLogger.LogWarning($"Could not retrieve permissions for resources. (projectId: {currentProjectId}, id: {hierarchyId}, name: {name}, error: {ConvertErrorXml(xmlPermissions)}, stack-trace: {GetStackTrace()})");
                                    }

                                    hasAccessToOutputChannel = xmlPermissions.SelectSingleNode($"/items/item/permissions/permission[@id='view' or @id='all']") != null;
                                }
                                else
                                {
                                    appLogger.LogWarning($"Unable not retrieve permissions for resources as the user is not authenticated. (projectId: {currentProjectId}, id: {hierarchyId}, name: {name}, stack-trace: {GetStackTrace()})");
                                }


                                //
                                // => If this user has access to the output channel, then continue processing
                                //
                                if (hasAccessToOutputChannel)
                                {
                                    //
                                    // => proceed with operations if nessecary
                                    //
                                    var xmlNodeListNested = nodeMetadata.SelectNodes("operations/operation");
                                    //Response.Write(xmlNodeListNested.Count.ToString()+"<br/><br/><br/>");




                                    //
                                    // => Generate the contents for the JSON object that we want to return
                                    //
                                    foreach (var outputChannelVariantId in outputChannelVariantIds)
                                    {

                                        var currentOutputChannelLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(currentProjectId, outputChannelVariantId);

                                        foreach (XmlNode xmlNodeNested in xmlNodeListNested)
                                        {
                                            var operationType = xmlNodeNested.Attributes["type"].Value;
                                            switch (operationType)
                                            {
                                                case "server-side_function":
                                                    if (xmlNodeNested.InnerText == "mark_edit_allowed")
                                                    {

                                                        var projectVariablesForStrippedHierarchyGeneration = new ProjectVariables
                                                        {
                                                            projectId = currentProjectId,
                                                            outputChannelVariantId = outputChannelVariantId,
                                                            currentUser = projectVars.currentUser
                                                        };
                                                        // projectVariablesForStrippedHierarchyGeneration = await FillProjectVariablesFromProjectId(projectVariablesForStrippedHierarchyGeneration, false);

                                                        // First render the stripped version of the hierarchy (and store the full version in the process as well)
                                                        xmlStrippedHierarchyDocument = await GenerateStrippedRbacHierarchy(hierarchyId, xmlStrippedHierarchyDocument, reqVars, projectVariablesForStrippedHierarchyGeneration, false, true);

                                                        // TODO: this needs to become similar to GenerateStrippedRbacHierarchy()
                                                        MarkHierarchyAccessRights(ref xmlStrippedHierarchyDocument);
                                                    }
                                                    break;
                                            }
                                        }



                                        dynamic outputChannelDetails = new ExpandoObject();
                                        outputChannelDetails.oclang = currentOutputChannelLanguage;
                                        outputChannelDetails.octype = RetrieveOutputChannelNameFromOutputChannelVariantId(currentProjectId, outputChannelVariantId);
                                        outputChannelDetails.name = RetrieveOutputChannelTypeFromOutputChannelVariantId(currentProjectId, outputChannelVariantId);

                                        outputChannelDetails.sectionids = new Dictionary<string, dynamic>();

                                        if (xmlStrippedHierarchyDocument != null)
                                        {
                                            // appLogger.LogCritical($"* Usable items: {xmlFilingDocumentHierarchy.SelectNodes("/items/structured//item").Count}");
                                            var nodeListItems = xmlStrippedHierarchyDocument.SelectNodes("/items/structured//item");
                                            foreach (XmlNode nodeItem in nodeListItems)
                                            {
                                                var sectionId = nodeItem.GetAttribute("id");
                                                var dataReference = nodeItem.GetAttribute("data-ref") ?? "thiswillnevermatch";
                                                if (!string.IsNullOrEmpty(sectionId))
                                                {
                                                    dynamic sectionDetails = new ExpandoObject();

                                                    var sectionIsEditable = (nodeItem.GetAttribute("editable") ?? "false") == "true";
                                                    sectionDetails.editable = sectionIsEditable;

                                                    sectionDetails.dataref = dataReference;

                                                    var sectionTitle = RetrieveSectionTitleFromMetadataCache(XmlCmsContentMetadata, dataReference, currentProjectId, currentOutputChannelLanguage)?.Trim();
                                                    sectionDetails.title = sectionTitle;

                                                    if (includePermissionDetails)
                                                    {
                                                        sectionDetails.permissions = new List<string>();
                                                        var nodeListPermissions = nodeItem.SelectNodes("permissions/permission");
                                                        foreach (XmlNode nodePermission in nodeListPermissions)
                                                        {
                                                            sectionDetails.permissions.Add(nodePermission.GetAttribute("id"));
                                                        }
                                                    }

                                                    if (includeNonEditableSections)
                                                    {
                                                        outputChannelDetails.sectionids.Add(sectionId, sectionDetails);
                                                    }
                                                    else if (sectionIsEditable)
                                                    {
                                                        outputChannelDetails.sectionids.Add(sectionId, sectionDetails);
                                                    }
                                                }
                                            }

                                        }
                                        else
                                        {
                                            appLogger.LogWarning($"Unable to retrieve stripped hierarchy from projectVars.cmsMetaData (user id: {projectVars.currentUser.Id ?? ""}, hierarchy id: {hierarchyId}, project id: {currentProjectId} in RetrieveUserSectionInformation())");
                                        }


                                        //
                                        // => Add the output channel variant section information if we have any details to share
                                        //
                                        if (outputChannelDetails.sectionids.Count > 0) projectAccessInformation.outputchannelvariantids.Add(outputChannelVariantId, outputChannelDetails);

                                    }

                                }
                                else
                                {
                                    // appLogger.LogInformation($"No need to fetch {id} as the user does not have permissions to this output channel at all");


                                }
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, $"Failed to load hierarchy. Details: " +
                                    $"ProjectId: {currentProjectId}, " +
                                    $"HierarchyId: {hierarchyId}, " +
                                    $"HierarchyName: {name}, " +
                                    $"UserId: {projectVars.currentUser.Id}, " +
                                    $"ProcessOnlyOpenProjects: {processOnlyOpenProjects}, " +
                                    $"IncludePermissionDetails: {includePermissionDetails}, " +
                                    $"IncludeNonEditableSections: {includeNonEditableSections}, " +
                                    $"ProjectIdFilter: [{string.Join(", ", projectIdFilter)}]");
                            }
                        }

                        // if (userHasAccessToAnOutputChannel) jsonResponseData.projectids.Add(currentProjectId, projectAccessInformation);
                        jsonResponseData.projectids.Add(currentProjectId, projectAccessInformation);
                    }
                    else
                    {
                        appLogger.LogError($"Unable to retrieve current project id from nodeCurrentProject (currentProjectId: {currentProjectId}, stack-trace: {GetStackTrace()})");
                        throw new Exception($"Unable to retrieve current project id from nodeCurrentProject (stack-trace:");
                    }
                }

                //
                // => Convert the dynamic to JSON
                //
                var json = (string)ConvertToJson(jsonResponseData, Newtonsoft.Json.Formatting.Indented);

                //
                // => Add the JSON to UserSectionInformationCacheData
                //
                if (enableCache)
                {
                    UserSectionInformationCacheData.AddOrUpdate(cacheKey, json, (key, existingValue) =>
                    {
                        // If the key already exists in the dictionary, update the value with the new JSON data
                        existingValue = json;
                        return existingValue;
                    });
                }


                if (debugRoutine)
                {
                    var message = (!enableCache) ? $"=> Rendered new user section information for user {projectVars.currentUser.Id} <=" : $"=> Rendered new user section information and put it in cache for user {projectVars.currentUser.Id} <=";
                    appLogger.LogInformation(message);
                }

                //
                // => Return the JSON data wrapped in a TaxxorReturnMessage object
                //
                return new TaxxorReturnMessage(true, "Successfully rendered the user section information", json, "");

                /// <summary>
                /// Helper function to retrieve the ourtputchannel variant ID based on the metadata hierarchy ID
                /// </summary>
                /// <param name="metadataKeyHierarchy"></param>
                /// <returns></returns>
                static List<string> RetrieveOutputVariantId(string projectId, string metadataKeyHierarchy)
                {
                    var outputChannelVariantIds = new List<string>();
                    var xPath = $"/configuration/editors/editor[@id='{RetrieveEditorIdFromProjectId(projectId)}']/output_channels/output_channel/variants/variant[@metadata-id-ref='{metadataKeyHierarchy}']";
                    var nodeListVariants = xmlApplicationConfiguration.SelectNodes(xPath);
                    foreach (XmlNode nodeVariant in nodeListVariants)
                    {
                        var outputVariantId = nodeVariant.GetAttribute("id");
                        if (!string.IsNullOrEmpty(outputVariantId) && !outputChannelVariantIds.Contains(outputVariantId)) outputChannelVariantIds.Add(outputVariantId);
                    }

                    return outputChannelVariantIds;
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, errorMessage);

                return new TaxxorReturnMessage(false, errorMessage, ex.ToString());
            }
        }

    }

}