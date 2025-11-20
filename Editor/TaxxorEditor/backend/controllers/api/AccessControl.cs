using System;
using System.Collections.Generic;
using System.Dynamic;
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
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders the access control overview for the UI
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RenderAccessControlOverview(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var projectId = context.Request.RetrievePostedValue("pid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var pageId = context.Request.RetrievePostedValue("pageid", true, reqVars.returnType);

            var html = await _renderAclOverview(projectVars, projectId, pageId, true);

            if (html.StartsWith("ERROR"))
            {
                HandleError(ReturnTypeEnum.Json, "Could not retrieve ACL overview", $"html: {html}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                // Construct a response message for the client
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully retrieved access control overview";
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = $"projectId: {projectId}, pageId: {pageId}";
                }
                jsonData.result.html = html;

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                //HandleError("Needs to be implemented", $"xmlAllItems: {HtmlEncodeForDisplay(xmlAllItems.OuterXml)}, xmlStoreResult: {HtmlEncodeForDisplay(html)}, stack-trace: {GetStackTrace()}");

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
        }




        /// <summary>
        /// Add access rights to an existing page/section in the project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task AddAccessRecordToPage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted data
            var projectId = context.Request.RetrievePostedValue("projectid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
            var pageId = context.Request.RetrievePostedValue("pageid", true, reqVars.returnType);
            var userGroupId = context.Request.RetrievePostedValue("usergroupid", @"^[a-zA-Z_\-\d@\.]{1,256}$", true, ReturnTypeEnum.Json);
            var roleId = context.Request.RetrievePostedValue("roleid", @"^[a-zA-Z_\-\d]{1,256}$", true, ReturnTypeEnum.Json);
            var scope = context.Request.RetrievePostedValue("scope", @"^(current\-outputchannel|all\-outputchannels)$", true, ReturnTypeEnum.Json);
            var resetInheritance = context.Request.RetrievePostedValue("resetinheritance", @"^(true|false)$", true, ReturnTypeEnum.Json);

            // Build a list of resource ID's to set
            List<string> resourceIdsToStore = new List<string>();

            // Append the resource ID of this output channel to the list
            resourceIdsToStore.Add(CalculateRbacResourceId("get", pageId, projectId, 2));

            // Loop over all the other output channels and add resource ID's if needed
            if (scope == "all-outputchannels") resourceIdsToStore.AddRange(await _findSimilarRbacResourceIds(projectVars, projectId, pageId));


            // Generate a debugstring that we can use in the response
            var basicDebugString = $"projectId: {projectId}, pageId: {pageId}, userGroupId: {userGroupId}, roleId: {roleId}, resourceIdsToStore: {String.Join(", ", resourceIdsToStore.ToArray())}, scope: {scope}";

            try
            {
                var overallCreateAccessRecordsSuccess = true;

                if (debugRoutine && resourceIdsToStore.Count > 1)
                {
                    appLogger.LogInformation($"** Adding {String.Join(", ", resourceIdsToStore.ToArray())} resource records to the RBAC service **");
                }

                // Create the access records in the RBAC service
                var failedAccessRecordCreateRequests = 0;
                foreach (var resourceId in resourceIdsToStore)
                {
                    // Add the access record to the Access Control Service
                    var createAccessRecordResult = await AccessControlService.AddAccessRecord(resourceId, userGroupId, roleId, true, ((resetInheritance == "true") ? true : false), debugRoutine);
                    if (!createAccessRecordResult)
                    {
                        appLogger.LogWarning($"Could not create access record for resourceId: {resourceId}, userGroupId: {userGroupId}, roleId: {roleId}");
                        failedAccessRecordCreateRequests++;
                    }
                }

                if (failedAccessRecordCreateRequests == resourceIdsToStore.Count) overallCreateAccessRecordsSuccess = false;

                // Render the result to return to the client
                if (overallCreateAccessRecordsSuccess)
                {
                    var html = await _renderAclOverview(projectVars, projectId, pageId, false);

                    if (html.StartsWith("ERROR"))
                    {
                        HandleError(ReturnTypeEnum.Json, "Could not retrieve ACL overview", $"html: {html}, {basicDebugString}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        // Construct a response message for the client
                        dynamic jsonData = new ExpandoObject();
                        jsonData.result = new ExpandoObject();
                        jsonData.result.message = "Successfully added access control for the user/group to page";
                        if (isDevelopmentEnvironment)
                        {
                            jsonData.result.debuginfo = $"{basicDebugString}";
                        }
                        jsonData.result.html = html;

                        string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                        //HandleError("Needs to be implemented", $"xmlAllItems: {HtmlEncodeForDisplay(xmlAllItems.OuterXml)}, xmlStoreResult: {HtmlEncodeForDisplay(html)}, stack-trace: {GetStackTrace()}");

                        await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
                    }

                }
                else
                {
                    HandleError(ReturnTypeEnum.Json, "Could not add access rights for the user/group to page", $"{basicDebugString}, stack-trace: {GetStackTrace()}");
                }
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "Could not add access rights for the user/group to page", $"error: {ex}, {basicDebugString}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Changes an access record in the RBAC service
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task EditResourceRecord(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted data
            var projectId = context.Request.RetrievePostedValue("projectid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
            var pageId = context.Request.RetrievePostedValue("pageid", true, reqVars.returnType);
            var resourceId = context.Request.RetrievePostedValue("resourceid", @"^[a-zA-Z_\-\d@\.]{1,256}$", true, ReturnTypeEnum.Json);
            var resetInheritance = context.Request.RetrievePostedValue("resetinheritance", @"^(true|false)$", true, ReturnTypeEnum.Json);
            var isPersistant = context.Request.RetrievePostedValue("ispersistant", @"^(true|false)$", true, ReturnTypeEnum.Json);

            // Generate a debugstring that we can use in the response
            var basicDebugString = $"projectId: {projectId}, pageId: {pageId}, resetInheritance: {resetInheritance}, isPersistant: {isPersistant}, resourceId: {resourceId}";

            // HandleError("Needs to be implemented", $"{basicDebugString}, stack-trace: {GetStackTrace()}");

            try
            {
                // Add the access record to the Access Control Service
                var createAccessRecordResult = await AccessControlService.EditResource(resourceId, (resetInheritance == "true"), (isPersistant == "true"));

                // Render the result to return to the client
                if (createAccessRecordResult)
                {


                    var html = await _renderAclOverview(projectVars, projectId, pageId, false);

                    if (html.StartsWith("ERROR"))
                    {
                        HandleError(ReturnTypeEnum.Json, "Could not retrieve ACL overview", $"html: {html}, {basicDebugString}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        // Construct a response message for the client
                        dynamic jsonData = new ExpandoObject();
                        jsonData.result = new ExpandoObject();
                        jsonData.result.message = "Successfully added access control for the user/group to page";
                        if (isDevelopmentEnvironment)
                        {
                            jsonData.result.debuginfo = $"{basicDebugString}";
                        }
                        jsonData.result.html = html;

                        string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                        //HandleError("Needs to be implemented", $"xmlAllItems: {HtmlEncodeForDisplay(xmlAllItems.OuterXml)}, xmlStoreResult: {HtmlEncodeForDisplay(html)}, stack-trace: {GetStackTrace()}");

                        await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
                    }

                }
                else
                {
                    HandleError(ReturnTypeEnum.Json, "Could not edit resource record", $"{basicDebugString}, stack-trace: {GetStackTrace()}");
                }
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "Could not edit resource record", $"error: {ex}, {basicDebugString}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Removes access rights from an existing project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RemoveAccessRecordFromPage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted data
            var projectId = context.Request.RetrievePostedValue("projectid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
            var pageId = context.Request.RetrievePostedValue("pageid", true, reqVars.returnType);
            var userGroupId = context.Request.RetrievePostedValue("usergroupid", @"^[a-zA-Z_\-\d@\.]{1,256}$", true, ReturnTypeEnum.Json);
            var roleId = context.Request.RetrievePostedValue("roleid", @"^[a-zA-Z_\-\d]{1,256}$", true, ReturnTypeEnum.Json);
            var scope = context.Request.RetrievePostedValue("scope", @"^(current\-outputchannel|all\-outputchannels)$", true, ReturnTypeEnum.Json);


            // Build a list of resource ID's to set
            List<string> resourceIdsToRemove = new List<string>();

            // Append the resource ID of this output channel to the list
            resourceIdsToRemove.Add(CalculateRbacResourceId("get", pageId, projectId, 2));

            // Loop over all the other output channels and add resource ID's if needed
            if (scope == "all-outputchannels") resourceIdsToRemove.AddRange(await _findSimilarRbacResourceIds(projectVars, projectId, pageId));

            // Generate a debugstring that we can use in the response
            var basicDebugString = $"projectId: {projectId}, pageId: {pageId}, userGroupId: {userGroupId}, roleId: {roleId}, resourceIdsToStore: {String.Join(", ", resourceIdsToRemove.ToArray())}, scope: {scope}";

            try
            {
                var overallRemoveAccessRecordsSuccess = true;

                if (debugRoutine && resourceIdsToRemove.Count > 1)
                {
                    appLogger.LogInformation($"** Removing {String.Join(", ", resourceIdsToRemove.ToArray())} resource records to the RBAC service **");
                }

                // Create the access records in the RBAC service
                var failedAccessRecordCreateRequests = 0;
                foreach (var resourceId in resourceIdsToRemove)
                {
                    // Add the access record to the Access Control Service
                    var createAccessRecordResult = await AccessControlService.DeleteAccessRecord(resourceId, userGroupId, roleId);
                    if (!createAccessRecordResult)
                    {
                        appLogger.LogWarning($"Could not remove access record for resourceId: {resourceId}, userGroupId: {userGroupId}, roleId: {roleId}");
                        failedAccessRecordCreateRequests++;
                    }
                }

                if (failedAccessRecordCreateRequests == resourceIdsToRemove.Count) overallRemoveAccessRecordsSuccess = false;

                // Render the result to return to the client
                if (overallRemoveAccessRecordsSuccess)
                {

                    var html = await _renderAclOverview(projectVars, projectId, pageId, false);

                    if (html.StartsWith("ERROR"))
                    {
                        HandleError(ReturnTypeEnum.Json, "Could not retrieve ACL overview", $"html: {html}, {basicDebugString}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        // Construct a response message for the client
                        dynamic jsonData = new ExpandoObject();
                        jsonData.result = new ExpandoObject();
                        jsonData.result.message = "Successfully removed access rights for the user/group from the page";
                        if (isDevelopmentEnvironment)
                        {
                            jsonData.result.debuginfo = $"{basicDebugString}";
                        }
                        jsonData.result.html = html;

                        string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);



                        await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
                    }
                }
                else
                {
                    HandleError("Could not remove access rights for the user/group from the page", $"{basicDebugString}, stack-trace: {GetStackTrace()}");
                }
            }
            catch (Exception ex)
            {
                HandleError("Could not remove access rights for the user/group from the page", $"error: {ex}, {basicDebugString}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Edits an RBAC access record to enable/disable it
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task EditAccessRecord(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted data
            var projectId = context.Request.RetrievePostedValue("projectid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
            var pageId = context.Request.RetrievePostedValue("pageid", true, reqVars.returnType);
            var groupId = context.Request.RetrievePostedValue("groupref", @"^[a-zA-Z_\-\d@\.]{1,256}$", false, ReturnTypeEnum.Json);
            var userId = context.Request.RetrievePostedValue("userref", @"^[a-zA-Z_\-\d@\.]{1,256}$", false, ReturnTypeEnum.Json);

            var userGroupId = "";
            if (groupId == null && userId == null)
            {
                HandleError("Not enough information supplied", $"groupId: {groupId}, userId: {userId}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                userGroupId = (string.IsNullOrEmpty(userId)) ? groupId : userId;
            }

            var roleId = context.Request.RetrievePostedValue("roleref", @"^[a-zA-Z_\-\d]{1,256}$", true, ReturnTypeEnum.Json);
            var resourceId = context.Request.RetrievePostedValue("resourceref", @"^[a-zA-Z_\-\d@\.]{1,256}$", true, ReturnTypeEnum.Json);

            var accessRecordEnabledString = context.Request.RetrievePostedValue("enabled", "(true|false)", true, ReturnTypeEnum.Json);
            var accessRecordEnabled = (accessRecordEnabledString == "true");

            // Generate a debugstring that we can use in the response
            var basicDebugString = $"groupId: {groupId}, userId: {userId}, roleId: {roleId}, resourceId: {resourceId}, accessRecordEnabled: {accessRecordEnabled}";

            try
            {
                // Create an access record instance
                var rbacAccessRecord = new RbacAccessRecord(userGroupId, roleId, resourceId, accessRecordEnabled);

                // Add the access record to the Access Control Service
                var editAccessRecordResult = await AccessControlService.EditAccessRecord(rbacAccessRecord, debugRoutine);

                // Render the result to return to the client
                if (editAccessRecordResult)
                {
                    // Clear the RBAC cache
                    projectVars.rbacCache.ClearAll();

                    // Clear the user section information cache data
                    UserSectionInformationCacheData.Clear();

                    // // Reload the hierarchies
                    // await retrieveMetaData(projectVars, reqVars);

                    // Render the new overview
                    var html = await _renderAclOverview(projectVars, projectId, pageId, false);

                    if (html.StartsWith("ERROR"))
                    {
                        HandleError(ReturnTypeEnum.Json, "Could not retrieve ACL overview", $"html: {html}, {basicDebugString}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        // Construct a response message for the client
                        dynamic jsonData = new ExpandoObject();
                        jsonData.result = new ExpandoObject();
                        jsonData.result.message = "Successfully removed access rights for the user/group from the page";
                        if (isDevelopmentEnvironment)
                        {
                            jsonData.result.debuginfo = $"{basicDebugString}";
                        }
                        jsonData.result.html = html;

                        string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);



                        await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
                    }
                }
                else
                {
                    HandleError("Could not edit the access record in the RBAC service", $"{basicDebugString}, stack-trace: {GetStackTrace()}");
                }
            }
            catch (Exception ex)
            {
                HandleError("There was an errror editing the access record in the RBAC service", $"error: {ex}, {basicDebugString}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Bulk update information in the RBAC service
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task BulkUpdateRbacInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            await DummyAwaiter();

            // Retrieve posted data
            var method = context.Request.RetrievePostedValue("method", @"^[a-zA-Z_\-\d]{1,256}$", true, reqVars.returnType);

            // Generate a debugstring that we can use in the response
            var basicDebugString = $"method: {method}";

            // Return variables to use
            var isSuccess = false;
            var returnMessage = "";

            try
            {
                switch (method)
                {
                    case "outputchannelhierarchiesexplicitrights":
                        // TODO: This needs to be a posted value instead of a hard coded value
                        //var roleRefForFilter = "taxxor-cms-editor";
                        string? roleRefForFilter = null;

                        // Retrieve additional posted values
                        var enableAccessRecordString = context.Request.RetrievePostedValue("enable", "(true|false)", true, reqVars.returnType);
                        bool enableAccessRecord = (enableAccessRecordString == "true");

                        basicDebugString += $", enableAccessRecord: {enableAccessRecordString}";

                        // Compile one document based on all the output channels
                        var xmlOutputChannelHierarchies = await RenderOutputChannelHierarchyOverview();
                        if (debugRoutine) await xmlOutputChannelHierarchies.SaveAsync($"{logRootPathOs}/outputchannelhierarchies.xml", false);

                        var uniqueItemIds = new List<string>();
                        var nodeListAllItems = xmlOutputChannelHierarchies.SelectNodes("/hierarchies/output_channel/items/*/item/sub_items//item");
                        if (nodeListAllItems.Count == 0)
                        {
                            HandleError("No items to process", "This project does not contain a hierarchy with child elements so there is nothing to process");
                        }
                        foreach (XmlNode nodeItem in nodeListAllItems)
                        {
                            var itemId = GetAttribute(nodeItem, "id");
                            if (string.IsNullOrEmpty(itemId))
                            {
                                appLogger.LogWarning($"Could not find item id. nodeItem: {TruncateString(nodeItem.OuterXml, 200)}");
                            }
                            else
                            {
                                if (!uniqueItemIds.Contains(itemId))
                                {
                                    uniqueItemIds.Add(itemId);
                                }
                            }
                        }

                        var xmlAccessRecords = await AccessControlService.ListAccessRecords();
                        if (debugRoutine) await xmlAccessRecords.SaveAsync($"{logRootPathOs}/allaccessrecords.xml", false);

                        var accessRecordsToEdit = new List<RbacAccessRecord>();

                        var xpathAccessRecords = (roleRefForFilter == null) ? $"/accessrecords/accessRecord[contains(resourceRef/@ref, '__{projectVars.projectId}')]" : $"/accessrecords/accessRecord[roleRef/@ref='{roleRefForFilter}' and contains(resourceRef/@ref, '__{projectVars.projectId}')]";
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
                                    if (debugRoutine) appLogger.LogInformation($"- itemIdFromResourceId: {itemIdFromResourceId}");
                                    if (uniqueItemIds.Contains(itemIdFromResourceId))
                                    {
                                        if (debugRoutine) appLogger.LogDebug($"Match and adding");

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
                                                appLogger.LogWarning("Could not create new RBAC access record because we were unable to determine the role reference");
                                            }
                                            else
                                            {
                                                var rbacAccessRecord = new RbacAccessRecord(userGroupId, userGroupRole, resourceId, enableAccessRecord);

                                                accessRecordsToEdit.Add(rbacAccessRecord);
                                            }
                                        }

                                    }
                                }
                            }
                        }

                        if (accessRecordsToEdit.Count == 0)
                        {
                            HandleError("No access records found that we need to change");
                        }

                        // Clear the RBAC cache
                        projectVars.rbacCache.ClearAll();

                        // Clear the user section information cache data
                        UserSectionInformationCacheData.Clear();

                        // Bulk edit access records// Add the access record to the Access Control Service
                        isSuccess = await AccessControlService.EditAccessRecord(accessRecordsToEdit, debugRoutine);
                        if (isSuccess)
                        {
                            returnMessage = $"Successfully changed {accessRecordsToEdit.Count} access records in Taxxor Access Control Service";
                        }
                        else
                        {
                            returnMessage = "Failed to change access records in Taxxor Access Control Service";
                        }


                        break;
                    default:
                        HandleError("RBAC bulk operation not supported", $"{basicDebugString}, stack-trace: {GetStackTrace()}");
                        break;
                }

            }
            catch (Exception ex)
            {
                HandleError("There was an errror editing the access record in the RBAC service", $"error: {ex}, {basicDebugString}, stack-trace: {GetStackTrace()}");
            }

            // Write a response to the client
            if (isSuccess)
            {
                // Construct a response message for the client
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = returnMessage;
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = $"{basicDebugString}";
                }

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);



                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
            else
            {
                HandleError(returnMessage, $"{basicDebugString}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Renders the ACL overview HTML to be shown in the client
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="projectId"></param>
        /// <param name="pageId"></param>
        /// <returns></returns>
        private async static Task<string> _renderAclOverview(ProjectVariables projectVars, string projectId, string pageId, bool renderAllHtml)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            try
            {
                // Clone the application configuration so that we can manipulate it
                var xmlApplicationConfigurationCloned = new XmlDocument();
                xmlApplicationConfigurationCloned.LoadXml(xmlApplicationConfiguration.OuterXml);

                // 1) RBAC data - append an element that can contain the information that we have received about the access control service
                var nodeRbacData = xmlApplicationConfigurationCloned.CreateElement("rbac");
                xmlApplicationConfigurationCloned.DocumentElement.AppendChild(nodeRbacData);

                // Retrieve the users/groups that have acces to this project
                var resourceId = CalculateRbacResourceId("get", pageId, projectId, 2);
                // appLogger.LogDebug($"- resourceId: {resourceId}");

                // a) Retrieve the access records
                var xmlCurrentResource = await AccessControlService.RetrieveResource(resourceId, false);
                if (XmlContainsError(xmlCurrentResource))
                {
                    appLogger.LogInformation($"Could not locate an access record for this resource: {resourceId}");
                }
                else
                {
                    //appLogger.LogDebug(xmlAccessRecords.OuterXml);
                    var nodeCurrentResource = xmlApplicationConfigurationCloned.CreateElement("current-resource");
                    nodeCurrentResource.AppendChild(xmlApplicationConfigurationCloned.ImportNode(xmlCurrentResource.DocumentElement, true));
                    nodeRbacData.AppendChild(nodeCurrentResource);
                }



                // b) Retrieve the access records
                var xmlAccessRecords = await AccessControlService.ListAccessRecords(resourceId);
                if (XmlContainsError(xmlAccessRecords)) HandleError(ReturnTypeEnum.Json, "Could not retrieve information", $"xmlAccessRecords: {xmlAccessRecords.OuterXml}, stack-trace: {GetStackTrace()}");
                //appLogger.LogDebug(xmlAccessRecords.OuterXml);
                nodeRbacData.AppendChild(xmlApplicationConfigurationCloned.ImportNode(xmlAccessRecords.DocumentElement, true));

                // c) Retrieve group information
                var xmlGroups = await AccessControlService.ListGroups();
                if (XmlContainsError(xmlGroups)) HandleError(ReturnTypeEnum.Json, "Could not retrieve information", $"xmlGroups: {xmlGroups.OuterXml}, stack-trace: {GetStackTrace()}");
                //appLogger.LogDebug(xmlGroups.OuterXml);
                nodeRbacData.AppendChild(xmlApplicationConfigurationCloned.ImportNode(xmlGroups.DocumentElement, true));

                // d) Retrieve user information
                var xmlUsers = await AccessControlService.ListUsers();
                if (XmlContainsError(xmlUsers)) HandleError(ReturnTypeEnum.Json, "Could not retrieve information", $"xmlUsers: {xmlUsers.OuterXml}, stack-trace: {GetStackTrace()}");
                //appLogger.LogDebug(xmlGroups.OuterXml);
                nodeRbacData.AppendChild(xmlApplicationConfigurationCloned.ImportNode(xmlUsers.DocumentElement, true));

                // e) Retrieve role information
                var xmlRoles = await AccessControlService.ListRoles();
                if (XmlContainsError(xmlRoles)) HandleError(ReturnTypeEnum.Json, "Could not retrieve information", $"xmlRoles: {xmlRoles.OuterXml}, stack-trace: {GetStackTrace()}");
                //appLogger.LogDebug(xmlRoles.OuterXml);
                nodeRbacData.AppendChild(xmlApplicationConfigurationCloned.ImportNode(xmlRoles.DocumentElement, true));

                // f) Effective permissions on this item in the tree
                if (pageId != "cms-overview")
                {
                    var editorId = (string.IsNullOrEmpty(projectVars.editorId)) ? RetrieveEditorIdFromProjectId(projectId) : projectVars.editorId;

                    var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);

                    if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                    {
                        var xmlOutputChannelHierarchy = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;

                        // Create a combined hierarchy of the application
                        var xmlFullHierarchy = new XmlDocument();
                        xmlFullHierarchy.ReplaceContent(xmlHierarchy);

                        var nodeImportedOutputChannelHierarchy = xmlFullHierarchy.ImportNode(xmlOutputChannelHierarchy.DocumentElement, true);

                        // Add the output channel hierarchy as a child element of the overview page
                        xmlFullHierarchy.SelectSingleNode("/items/structured/item[1]/sub_items/item[1]/sub_items").AppendChild(nodeImportedOutputChannelHierarchy);

                        // Find the current node in the hierarchy
                        var nodeItem = xmlFullHierarchy.SelectSingleNode($"/items//item[@id={GenerateEscapedXPathString(pageId)}]");
                        if (nodeItem == null)
                        {
                            if (debugRoutine) appLogger.LogWarning($"Could not retrieve hierarchy node for pageId: {pageId}, stack-trace: {GetStackTrace()}");
                        }
                        else
                        {
                            var rbacBreadCrumbTrail = GenerateRbacBreadcrumbTrail(RequestMethodEnum.Get, projectId, nodeItem, true);
                            XmlDocument xmlEffectiveRoles = await AccessControlService.RetrieveEffectiveRolesPerUserGroup(rbacBreadCrumbTrail);
                            // if (debugRoutine)
                            // {
                            //     Console.WriteLine("************************");
                            //     Console.WriteLine($"- rbacBreadCrumbTrail: {rbacBreadCrumbTrail}");
                            //     Console.WriteLine("Original:");
                            //     Console.WriteLine(PrettyPrintXml(xmlEffectiveRoles));
                            // }

                            // To avoid overloading the UI with a huge amount of information we will mark the users in this list when they are also part of a group that the service returns
                            var nodeListInheritedGroups = xmlEffectiveRoles.SelectNodes("/response/groups/group");
                            foreach (XmlNode nodeInheritedGroup in nodeListInheritedGroups)
                            {
                                var groupId = nodeInheritedGroup.GetAttribute("id");
                                var nodeListGroupRoles = nodeInheritedGroup.SelectNodes("role");
                                foreach (XmlNode nodeGroupRole in nodeListGroupRoles)
                                {
                                    var groupRoleId = nodeGroupRole.InnerText ?? "";
                                    if (!string.IsNullOrEmpty(groupId) && !string.IsNullOrEmpty(groupRoleId))
                                    {
                                        // Retrieve the users in this group
                                        var nodeListGroupUsers = nodeRbacData.SelectNodes($"groups/group[@id='{groupId}']/user");
                                        foreach (XmlNode nodeGroupUser in nodeListGroupUsers)
                                        {
                                            var userId = nodeGroupUser.GetAttribute("ref");
                                            if (!string.IsNullOrEmpty(userId))
                                            {
                                                var nodeListUsersWithRole = xmlEffectiveRoles.SelectNodes($"/response/users/user[@id='{userId}' and role/text()='{groupRoleId}']");
                                                foreach (XmlNode nodeUserWithRole in nodeListUsersWithRole)
                                                {
                                                    // Remove this role node
                                                    RemoveXmlNode(nodeUserWithRole.SelectSingleNode($"role[text()='{groupRoleId}']"));

                                                }
                                            }
                                            else
                                            {
                                                appLogger.LogWarning($"Could not continue, because the userId was empty in group with id: {groupId ?? "null"}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Could not parse inherited group information, because the groupId: {groupId ?? "null"} or roleId {groupRoleId ?? "null"} is empty");
                                    }
                                }

                            }

                            // if (debugRoutine)
                            // {
                            //     Console.WriteLine("Cleaned up (1):");
                            //     Console.WriteLine(PrettyPrintXml(xmlEffectiveRoles));
                            // }

                            // Remove potential entries if the current resource record contains the same definitions
                            var nodeListInheritedUsersToInspect = xmlEffectiveRoles.SelectNodes($"/response/users/user[role]");
                            foreach (XmlNode nodeInheritedUserToInspect in nodeListInheritedUsersToInspect)
                            {
                                var userIdToInspect = nodeInheritedUserToInspect.GetAttribute("id");
                                var nodeListInheritedUserRolesToInspect = nodeInheritedUserToInspect.SelectNodes("role");
                                foreach (XmlNode nodeInheritedUserRoleToInspect in nodeListInheritedUserRolesToInspect)
                                {
                                    var userRoleToInspect = nodeInheritedUserRoleToInspect.InnerText ?? "";
                                    var nodeAccessRecord = nodeRbacData.SelectSingleNode($"accessrecords/accessRecord[userRef/@ref='{userIdToInspect}' and roleRef/@ref='{userRoleToInspect}']");
                                    if (nodeAccessRecord != null)
                                    {
                                        // Remove role node
                                        RemoveXmlNode(nodeInheritedUserRoleToInspect);
                                    }
                                }
                            }

                            // Remove the users from the list that we do not need to show
                            RemoveXmlNodes(xmlEffectiveRoles.SelectNodes($"/response/users/user[not(role)]"));

                            // if (debugRoutine)
                            // {
                            //     Console.WriteLine("Cleaned up (2):");
                            //     Console.WriteLine(PrettyPrintXml(xmlEffectiveRoles));
                            //     Console.WriteLine("************************");
                            // }


                            var nodeInheritedRoles = xmlApplicationConfigurationCloned.CreateElement("inherited-roles");
                            nodeInheritedRoles.AppendChild(xmlApplicationConfigurationCloned.ImportNode(xmlEffectiveRoles.SelectSingleNode("//users"), true));
                            nodeInheritedRoles.AppendChild(xmlApplicationConfigurationCloned.ImportNode(xmlEffectiveRoles.SelectSingleNode("//groups"), true));
                            nodeRbacData.AppendChild(nodeInheritedRoles);

                            // Mark the effective permissions which have been defined on the current item
                            var nodeListInheritedUserRoles = xmlApplicationConfigurationCloned.SelectNodes($"/configuration/rbac/inherited-roles/users/user");
                            foreach (XmlNode nodeUser in nodeListInheritedUserRoles)
                            {
                                var userId = GetAttribute(nodeUser, "id");
                                var userRole = RetrieveNodeValueIfExists("role", nodeUser);
                                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
                                {
                                    appLogger.LogWarning($"Could not locate userId or userRole. stack-trace: {GetStackTrace()}");
                                }
                                else
                                {
                                    // Test if this combination is present as an explicitly defined role on this resource
                                    var nodeAccessRecord = xmlApplicationConfigurationCloned.SelectSingleNode($"/configuration/rbac/accessrecords/accessRecord[userRef/@ref='{userId}' and roleRef/@ref='{userRole}']");

                                    // If it's present then we need to hide the user as an inherited member because it's explicitly defined
                                    if (nodeAccessRecord != null)
                                    {
                                        SetAttribute(nodeUser, "hide", "true");
                                    }
                                }
                            }
                            var nodeListInheritedGroupRoles = xmlApplicationConfigurationCloned.SelectNodes($"/configuration/rbac/inherited-roles/groups/group");
                            foreach (XmlNode nodeGroup in nodeListInheritedGroupRoles)
                            {
                                var groupId = GetAttribute(nodeGroup, "id");
                                var groupRole = RetrieveNodeValueIfExists("role", nodeGroup);
                                if (string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(groupRole))
                                {
                                    appLogger.LogWarning($"Could not locate groupId or groupRole. stack-trace: {GetStackTrace()}");
                                }
                                else
                                {
                                    // Test if this combination is present as an explicitly defined role on this resource
                                    var nodeAccessRecord = xmlApplicationConfigurationCloned.SelectSingleNode($"/configuration/rbac/accessrecords/accessRecord[groupRef/@ref='{groupId}' and roleRef/@ref='{groupRole}']");

                                    // If it's present then we need to hide the user as an inherited member because it's explicitly defined
                                    if (nodeAccessRecord != null)
                                    {
                                        SetAttribute(nodeGroup, "hide", "true");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}'], stack-trace: {GetStackTrace()}");
                    }
                }


                // Dump the xml document that we have created for inspection
                if (debugRoutine) await xmlApplicationConfigurationCloned.SaveAsync(logRootPathOs + "/accesscontrol.xml", false);

                var xmlNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='cms_xsl_accesscontrol']");
                var xslPathOs = CalculateFullPathOs(xmlNode);

                XsltArgumentList xsltArgs = new XsltArgumentList();
                xsltArgs.AddParam("projectid", "", projectId);
                xsltArgs.AddParam("pageid", "", pageId);
                xsltArgs.AddParam("resourceid", "", resourceId);

                // ProjectVariables properties that we need to pass to the XSLT
                if (!string.IsNullOrEmpty(projectVars.editorId)) xsltArgs.AddParam("editorid", "", projectVars.editorId);
                if (!string.IsNullOrEmpty(projectVars.outputChannelType)) xsltArgs.AddParam("outputchanneltype", "", projectVars.outputChannelType);
                if (!string.IsNullOrEmpty(projectVars.outputChannelVariantId)) xsltArgs.AddParam("outputchannelvariantid", "", projectVars.outputChannelVariantId);
                if (!string.IsNullOrEmpty(projectVars.outputChannelVariantLanguage)) xsltArgs.AddParam("outputchannelvariantlanguage", "", projectVars.outputChannelVariantLanguage);

                if (renderAllHtml)
                {
                    xsltArgs.AddParam("render-all", "", "true");
                }
                else
                {
                    xsltArgs.AddParam("render-all", "", "false");
                }
                xsltArgs.AddParam("permissions", "", string.Join(",", projectVars.currentUser.Permissions.Permissions.ToArray()));

                return TransformXml(xmlApplicationConfigurationCloned, xslPathOs, xsltArgs);
            }
            catch (Exception ex)
            {
                return $"ERROR: There was an error rendering the ACL overview. error: {ex}, stack-trace: {GetStackTrace()}";
            }

        }

        /// <summary>
        /// Find resource ID's of other output channels featuring the same section source
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="projectId"></param>
        /// <param name="pageId"></param>
        /// <param name="onlyLookWithinSameLanguage"></param>
        /// <returns></returns>
        private static async Task<List<string>> _findSimilarRbacResourceIds(ProjectVariables projectVars, string projectId, string pageId, bool onlyLookWithinSameLanguage = true)
        {
            var resourceIds = new List<string>();
            // Retrieve the data file associated with the current section ID and use that to look up other potential elements in the hierarchies
            var editorId = (string.IsNullOrEmpty(projectVars.editorId)) ? RetrieveEditorIdFromProjectId(projectId) : projectVars.editorId;
            var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
            var xmlOutputChannelHierarchy = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;

            var dataReference = RetrieveAttributeValueIfExists($"//item[@id='{pageId}']/@data-ref", xmlOutputChannelHierarchy);
            if (!string.IsNullOrEmpty(dataReference))
            {
                // Load an overview of all the output channels 
                var xmlOutputChannelHierarchyOverview = await RenderOutputChannelHierarchyOverview(false);

                // Find potential other section ID's that use the same data ref
                var nodeListItems = xmlOutputChannelHierarchyOverview.SelectNodes((onlyLookWithinSameLanguage) ? $"/hierarchies/output_channel[@lang='{projectVars.outputChannelVariantLanguage}']//item[@data-ref='{dataReference}']" : $"/hierarchies/output_channel//item[@data-ref='{dataReference}']");
                foreach (XmlNode nodeItem in nodeListItems)
                {
                    var itemId = GetAttribute(nodeItem, "id");
                    if (string.IsNullOrEmpty(itemId))
                    {
                        appLogger.LogWarning($"Could not locate item ID for dataReference: {dataReference}. stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        var resourceIdToAdd = CalculateRbacResourceId("get", itemId, projectId, 2);
                        if (!resourceIds.Contains(resourceIdToAdd)) resourceIds.Add(resourceIdToAdd);
                    }
                }
            }
            else
            {
                appLogger.LogInformation($"Could not locate a data reference for page ID {pageId}, outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey}, stack-trace: {GetStackTrace()}");
            }

            return resourceIds;
        }


    }

}