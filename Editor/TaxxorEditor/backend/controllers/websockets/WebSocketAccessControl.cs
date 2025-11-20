using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

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
            [AuthorizeAttribute(VirtualPath = "/hub/methods/managertools")]
            public async Task<TaxxorReturnMessage> RetrieveGroupsByUser(string userId)
            {
                var errorMessage = "There was an error rendering the group information";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");

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
                    inputValidationCollection.Add("userId", userId, RegexEnum.Email, true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Retrieve the information from the RBAC service
                    //
                    var xmlGroupsForUser = await AccessControlService.ListGroups(userId);

                    // Transform to JSON
                    dynamic jsonData = new ExpandoObject();
                    jsonData.groups = new List<ExpandoObject>();
                    var nodeListGroups = xmlGroupsForUser.SelectNodes("/groups/group");

                    var nodeListGroupsOrdered = nodeListGroups.Cast<XmlNode>().OrderBy(x => x.SelectSingleNode("name").InnerText);

                    foreach (XmlNode nodeGroup in nodeListGroupsOrdered)
                    {
                        dynamic groupInfo = new ExpandoObject();
                        groupInfo.id = nodeGroup.GetAttribute("id");
                        groupInfo.name = nodeGroup.SelectSingleNode("name")?.InnerText ?? "";
                        jsonData.groups.Add(groupInfo);
                    }
                    var json = (string)ConvertToJson(jsonData);

                    // Render the result as a TaxxorReturnMessage
                    return new TaxxorReturnMessage(true, "Successfully rendered group information", json, $"userId: {userId}");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Retrieves the state of explicit the access control settings 
            /// </summary>
            /// <param name="userId"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveExplicitUserAccessRightsState(string projectId)
            {
                var errorMessage = "There was an error retrieving the state of the user access rights";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Prepare the project variables object so that we can use it in the authorization process
                    //
                    projectVars.projectId = projectId;
                    projectVars.rbacCache = new RbacCache(projectVars.currentUser.Id, projectVars.projectId);


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
                    // => Retrieve the state of the explicit access control
                    //
                    var explicitAccessRights = "unknown";

                    var xmlAccessRecords = await AccessControlService.ListAccessRecords();

                    if (debugRoutine) await xmlAccessRecords.SaveAsync($"{logRootPathOs}/allaccessrecords.xml", false);


                    var xpathAccessRecords = $"/accessrecords/accessRecord[contains(resourceRef/@ref, '__{projectId}')]";
                    var nodeListAccessRecords = xmlAccessRecords.SelectNodes(xpathAccessRecords);
                    var totalProjectAccessRights = nodeListAccessRecords.Count;
                    if (nodeListAccessRecords.Count > 0)
                    {
                        xpathAccessRecords = $"/accessrecords/accessRecord[contains(resourceRef/@ref, '__{projectId}') and @disabled='true']";
                        nodeListAccessRecords = xmlAccessRecords.SelectNodes(xpathAccessRecords);
                        var disabledProjectAccessRights = nodeListAccessRecords.Count;

                        // Console.WriteLine($"totalProjectAccessRights: {totalProjectAccessRights}, disabledProjectAccessRights: {disabledProjectAccessRights}");

                        // When more than 50% of the access records have been disabled then we conclude that the explicit access control records have been disabled
                        if (((double)disabledProjectAccessRights / totalProjectAccessRights) > 0.5)
                        {
                            explicitAccessRights = "disabled";
                        }
                        else
                        {
                            explicitAccessRights = "enabled";
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"No access records available for {projectId}");
                    }

                    // Render the result as a TaxxorReturnMessage
                    return new TaxxorReturnMessage(true, "Successfully retrieved the state of the explicit access control settings", explicitAccessRights, $"projectId: {projectId}");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Search and replace roles within a project
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="outputChannelVariantId"></param>
            /// <param name="itemId"></param>
            /// <param name="roleSearch"></param>
            /// <param name="roleReplace"></param>
            /// <param name="accessRecordState"></param>
            /// <param name="includeChildren"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/managertools")]
            public async Task<TaxxorReturnMessage> SeachReplaceRoles(string projectId, string outputChannelVariantId, string itemId, string roleSearch, string roleReplace, string accessRecordState, bool includeChildren)
            {
                var errorMessage = "There was an error search and replace access control roles";
                var defaultDebugInfo = $"projectId:{projectId}, outputChannelVariantId: {outputChannelVariantId}, itemId: {itemId}, roleSearch: {roleSearch}, roleReplace: {roleReplace}, includeChildren: {includeChildren}";
                var itemIds = new List<string>();
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("outputChannelVariantId", outputChannelVariantId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("roleSearch", roleSearch, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("roleReplace", roleReplace, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("accessRecordState", accessRecordState, @"^(nochange|enable|disable)$", true);
                    inputValidationCollection.Add("includeChildren", includeChildren.ToString().ToLower(), RegexEnum.Boolean, true);


                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

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

                    //
                    // => Find all the item id's for which we have to search for the role
                    //
                    itemIds.Add(itemId);
                    var nodeOriginalItem = xmlHierarchyOverview.SelectSingleNode($"/hierarchies/output_channel[@id='{projectVars.outputChannelVariantId}']/items/structured//item[@id='{itemId}']");
                    if (nodeOriginalItem == null) throw new Exception($"Unable to find item in the outputchannel hierarchy");
                    var dataReference = nodeOriginalItem.GetAttribute("data-ref");
                    if (string.IsNullOrEmpty(dataReference)) throw new Exception($"Unable to locate data reference for itemId '{itemId}'");
                    var nodeListItems = xmlHierarchyOverview.SelectNodes($"/hierarchies/output_channel/items/structured//item[@data-ref='{dataReference}']");
                    foreach (XmlNode nodeItem in nodeListItems)
                    {
                        AddIdToList(nodeItem);

                        // Add child nodes if needed
                        if (includeChildren)
                        {
                            var nodeListSubItems = nodeItem.SelectNodes("sub_items//item");
                            foreach (XmlNode nodeSubItem in nodeListSubItems)
                            {
                                AddIdToList(nodeSubItem);
                            }
                        }
                    }

                    //
                    // => Retrieve the users/groups that have acces to this project
                    //
                    var originalAccessRecords = new List<RbacAccessRecord>();
                    var adjustedAccessRecords = new List<RbacAccessRecord>();
                    var countMatch = 0;
                    foreach (var pageId in itemIds)
                    {
                        var resourceId = CalculateRbacResourceId("get", pageId, projectId, 2);
                        appLogger.LogDebug($"- resourceId: {resourceId}");

                        // a) Retrieve the access records
                        var xmlCurrentResource = await AccessControlService.ListAccessRecords(resourceId);
                        if (XmlContainsError(xmlCurrentResource))
                        {
                            appLogger.LogInformation($"Could not locate an access record for this resource: {resourceId}");
                        }
                        else
                        {
                            var nodeListAccessRecords = xmlCurrentResource.SelectNodes("/accessrecords/accessRecord");
                            if (nodeListAccessRecords.Count == 0)
                            {
                                appLogger.LogInformation($"Could not locate an access record for this resource: {resourceId}");
                            }
                            else
                            {
                                foreach (XmlNode nodeAccessRecord in nodeListAccessRecords)
                                {
                                    var roleRef = nodeAccessRecord.SelectSingleNode("roleRef")?.GetAttribute("ref");
                                    if (roleRef == null)
                                    {
                                        appLogger.LogWarning($"Could not finde role for access record: {nodeAccessRecord.OuterXml}");
                                    }
                                    else
                                    {
                                        if (roleRef == roleSearch)
                                        {
                                            appLogger.LogInformation($"Role reference match!");
                                            countMatch++;

                                            var accessRecord = new RbacAccessRecord(nodeAccessRecord);
                                            originalAccessRecords.Add(accessRecord);

                                            accessRecord = new RbacAccessRecord(nodeAccessRecord);
                                            accessRecord.roleref = roleReplace;
                                            adjustedAccessRecords.Add(accessRecord);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    //
                    // => Save the adjusted access records
                    //
                    if (adjustedAccessRecords.Count > 0)
                    {
                        appLogger.LogInformation($"Saving {adjustedAccessRecords.Count} modified access records");
                        var totalCount = 0;
                        var successDelete = 0;
                        var successCreate = 0;
                        var successEdit = 0;
                        foreach (var record in adjustedAccessRecords)
                        {
                            string? userGroupId = record.groupref ?? record.userref;
                            var successDeleteResult = await AccessControlService.DeleteAccessRecord(record.resourceref, userGroupId, originalAccessRecords[totalCount].roleref);
                            if (successDeleteResult) successDelete++;
                            var successCreateResult = await AccessControlService.AddAccessRecord(record.resourceref, userGroupId, record.roleref, false, false, false);
                            if (successCreateResult) successCreate++;
                            
                            // Set the state of the Access Record we have just created
                            switch (accessRecordState)
                            {
                                case "enable":
                                    record.enabled = true;
                                    break;
                                case "disable":
                                    record.enabled = false;
                                    break;
                            }
                            var successEditResult = await AccessControlService.EditAccessRecord(record, false);
                            if (successEditResult) successEdit++;

                            totalCount++;
                        }

                        // The overall result is determined when all records have been successfully changed
                        var updateSuccess = (
                            adjustedAccessRecords.Count == successCreate &&
                            adjustedAccessRecords.Count == successDelete &&
                            adjustedAccessRecords.Count == successEdit
                        );
                        if (!updateSuccess)
                        {
                            throw new Exception($"Unable to store {adjustedAccessRecords.Count} modified access records (successDelete: {successDelete}, successCreate: {successCreate}, successEdit: {successEdit})");
                        }
                    }



                    var json = "{}";

                    // Render the result as a TaxxorReturnMessage
                    return new TaxxorReturnMessage(true, $"Successfully replaced {adjustedAccessRecords.Count} roles", json, defaultDebugInfo);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, ex.Message, defaultDebugInfo);
                }

                /// <summary>
                /// Helper routine
                /// </summary>
                /// <param name="nodeItem"></param>
                void AddIdToList(XmlNode nodeItem)
                {
                    var id = nodeItem.GetAttribute("id");
                    if (!string.IsNullOrEmpty(id))
                    {
                        if (!itemIds.Contains(id)) itemIds.Add(id);
                    }
                    else
                    {
                        var nodeDebug = nodeItem.CloneNode(false);
                        appLogger.LogWarning($"Unable to find an item ID (node: {nodeDebug.OuterXml})");
                    }
                }

            }


        }

    }

}