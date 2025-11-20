using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities that define authorization in the application
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Generates a stripped hierarchy based on the permissions retrieved from the Taxxor Access Control (RBAC) service and store the permissions in the hierarchy so that we can re-use it later
        /// This routine works in condjunction with the RbacCache object to limit the load on the RBAC service
        /// </summary>
        /// <param name="hierarchyId"></param>
        /// <param name="hierarchy"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="storeHierarchyInCache"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> GenerateStrippedRbacHierarchy(string hierarchyId, XmlDocument hierarchy, RequestVariables reqVars, ProjectVariables projectVars, bool storeHierarchyInCache, bool disableApiRouteCheck = false)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev") && (reqVars.pageId != "filingeditorlistlocks");
            debugRoutine = false;
            var debugInfo = "";
            var debugHierarchyId = "hierarchy-ar-pdf-en-xyz";
            var dumpInfo = reqVars.pageId == "cms-overview" || reqVars.pageId == "cms_hierarchy-manager" || reqVars.pageId == "cms_content-editor";

            //
            // => Make sure that the projectVars contains an initiated RbacCache object
            //
            projectVars.rbacCache ??= new RbacCache(projectVars.currentUser.Id, null);

            // Attempt to retrieve the stripped hierarchy from the rbacCache
            XmlDocument? xmlStrippedHierarchyInternal = projectVars?.rbacCache?.GetHierarchy(hierarchyId, "stripped");
            if (hierarchyId == debugHierarchyId)
            {
                appLogger.LogInformation($"Debugging {debugHierarchyId} with projectId: {projectVars.projectId ?? ""}");
            }

            if (xmlStrippedHierarchyInternal == null)
            {
                if (debugRoutine) appLogger.LogInformation($"Calculate new stripped hierarchy (hierarchyId: {hierarchyId})");

                // For retrieving the permissions
                var xmlPermissionsHierarchy = new XmlDocument();

                // For creating the stripped hierarchy
                xmlStrippedHierarchyInternal = new XmlDocument();

                // Boolean that can be used for preventing the cache to be stored regardless of the value of storeHierarchyInCache
                var setCache = true;

                // Clone the hierarchy document so that we can manipulate it freely
                if (hierarchyId == "taxxoreditor")
                {
                    xmlPermissionsHierarchy.ReplaceContent(reqVars.xmlHierarchy);
                    xmlStrippedHierarchyInternal.ReplaceContent(reqVars.xmlHierarchy);
                }
                else
                {
                    // Create the hierarchy we need to determine the permissions
                    xmlPermissionsHierarchy = CreateHierarchyForRbacPermissionsRetrieval(reqVars.xmlHierarchy, hierarchy);

                    // The base hierarchy that we will base the stripped version on is the hierarchy that we passed to this function
                    xmlStrippedHierarchyInternal.ReplaceContent(hierarchy);

                    // Determine if we need to store the hierarchy in the cache
                    try
                    {
                        var nodeFilingEditor = reqVars?.xmlHierarchy?.SelectSingleNode("/items/structured/item/sub_items//item[@id='cms_content-editor']");
                        if (nodeFilingEditor != null)
                        {
                            var ancestorHierarchyItems = nodeFilingEditor.SelectNodes("ancestor-or-self::item");
                            if (ancestorHierarchyItems.Count <= 2) setCache = false;
                        }
                        else
                        {
                            setCache = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"There was an error trying to determine if we need to store the hierarchy in the cache (projectId: {projectVars.projectId}, hierarchyId: {hierarchyId})");
                        setCache = false;
                    }
                }

                // Determine the project if to use and optionally strip a lot of elements from the hierarchy
                string? projectIdToUse = null;
                if (string.IsNullOrEmpty(projectVars.projectId))
                {
                    debugInfo += "In static removal";
                    // NOTE: this is really rough - probably needs some serious refinement
                    var nodeListItemsToRemove = xmlStrippedHierarchyInternal.SelectNodes("/items/structured/item/sub_items/item[not(@id='apiroot' or contains(@id, 'admin'))]");
                    foreach (XmlNode nodeItemToRemove in nodeListItemsToRemove)
                    {
                        RemoveXmlNode(nodeItemToRemove);
                    }
                }
                else
                {
                    projectIdToUse = projectVars.projectId;
                }

                // Retrieve a list of breadcrumbtrails
                var dictItemIdsBreadcrumbTrails = new Dictionary<string, string>();
                var nodeListAllItems = xmlPermissionsHierarchy.SelectNodes("/items//item");

                foreach (XmlNode nodeItem in nodeListAllItems)
                {
                    if (!dictItemIdsBreadcrumbTrails.TryAdd(GetAttribute(nodeItem, "id"), GenerateRbacBreadcrumbTrail(RequestMethodEnum.Get, projectIdToUse, nodeItem, disableApiRouteCheck)))
                    {
                        appLogger.LogWarning($"Could not add {GetAttribute(nodeItem, "id")} to the dictItemIdsBreadcrumbTrails dictionary as it already exists... (hierarchyId: {hierarchyId})");
                    }
                }

                // Generate an array of the breadcrumbtrails that we have generated
                var breadcrumbTrails = dictItemIdsBreadcrumbTrails.Select(x => x.Value);

                if (hierarchyId == debugHierarchyId)
                {
                    Console.WriteLine($"=> dump breadcrumbtrails: hierarchyId: {hierarchyId}, projectIdToUse: {projectIdToUse} <=");
                    for (int i = 1; i < 5; i++)
                    {
                        string breadcrumbTrail = breadcrumbTrails.ElementAt(i);
                        Console.WriteLine($"\t{i}: {breadcrumbTrail}");
                    }
                    Console.WriteLine($"=> ------------------------- <=");
                }


                // Retrieve the effective permissions from the Taxxor Access Control Service
                string? logFileName = (hierarchyId == debugHierarchyId) ? $"RetrievePermissionsForResources_{hierarchyId}-{projectVars.currentUser.Id}.xml" : null;
                XmlDocument xmlPermissions = await AccessControlService.RetrievePermissionsForResources(string.Join(":", breadcrumbTrails), null, debugRoutine, logFileName);

                if (XmlContainsError(xmlPermissions))
                {
                    appLogger.LogWarning($"Could not retrieve permissions for resources. (hierarchyId: {hierarchyId}, error: {ConvertErrorXml(xmlPermissions)})");
                }

                // if (hierarchyId == debugHierarchyId) await xmlPermissions.SaveAsync($"{logRootPathOs}/_permissions-{hierarchyId}.xml", false);

                // Decorate the hierarchy with the permissions that we have received
                nodeListAllItems = xmlStrippedHierarchyInternal.SelectNodes("/items//item");
                foreach (XmlNode nodeItem in nodeListAllItems)
                {
                    var currentItemId = GetAttribute(nodeItem, "id");
                    if (!string.IsNullOrEmpty(currentItemId))
                    {
                        // Find the breadcrumbtrail that we used for looking up the data
                        if (dictItemIdsBreadcrumbTrails.TryGetValue(currentItemId, out string? currentBreadcrumbTrail))
                        {
                            // Retrieve the permissions from the RBAC xml
                            var nodeRbacPermissions = xmlPermissions.SelectSingleNode($"/items/item[@id='{currentBreadcrumbTrail}']/permissions");
                            if (nodeRbacPermissions != null)
                            {
                                // if (debugRoutine && hierarchyId == "hierarchy-pdf-en")
                                // {
                                //     appLogger.LogInformation(nodeRbacPermissions.OuterXml);
                                // }

                                // Import the permissions in the xml hierarchy
                                var nodeRbacPermissionsImported = xmlStrippedHierarchyInternal.ImportNode(nodeRbacPermissions, true);
                                var nodeWebPage = nodeItem.SelectSingleNode("web_page");
                                if (nodeWebPage != null)
                                {


                                    nodeWebPage.ParentNode.InsertAfter(nodeRbacPermissionsImported, nodeWebPage);

                                    // Now we can mark this node if it should be stripped or not
                                    var nodeListSpecialPermissions = nodeItem.SelectNodes("special_permissions/permission");
                                    if (nodeListSpecialPermissions.Count > 0)
                                    {

                                        if (nodeRbacPermissionsImported.SelectSingleNode("permission[@id='all']") != null)
                                        {
                                            // No need to do anything
                                        }
                                        else
                                        {
                                            // We need "view" and the special permissions rights
                                            if (nodeRbacPermissionsImported.SelectSingleNode("permission[@id='view']") == null)
                                            {
                                                // If we don't have "view" permissions then we do not need to look any further
                                                SetAttribute(nodeItem, "delete", "true");
                                            }
                                            else
                                            {

                                                // In order to keep the item in the hierarchy one of the special permissions needs to be present
                                                var specialPermissionPresent = false;
                                                foreach (XmlNode nodeSpecialPermission in nodeListSpecialPermissions)
                                                {
                                                    var specialPermission = GetAttribute(nodeSpecialPermission, "id");
                                                    if (!string.IsNullOrEmpty(specialPermission))
                                                    {
                                                        if (!specialPermissionPresent)
                                                        {
                                                            if (nodeRbacPermissionsImported.SelectSingleNode($"permission[@id='{specialPermission}']") != null) specialPermissionPresent = true;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        appLogger.LogWarning($"Could not retrieve special permissions for item with ID: {currentItemId}, stack-trace: {GetStackTrace()}");
                                                    }

                                                }

                                                var setDeleteFlag = specialPermissionPresent ? false : true;

                                                if (setDeleteFlag)
                                                {
                                                    SetAttribute(nodeItem, "delete", "true");
                                                }
                                                else
                                                {
                                                    // SetAttribute(nodeItem, "delete", "false");
                                                }
                                            }

                                        }

                                    }
                                    else
                                    {
                                        // Check if we have "view" permissions
                                        if (nodeRbacPermissionsImported.SelectSingleNode("permission[@id='view' or @id='all']") == null)
                                        {
                                            SetAttribute(nodeItem, "delete", "true");
                                        }
                                        else
                                        {
                                            // SetAttribute(nodeItem, "delete", "false");
                                        }
                                    }
                                }
                                else
                                {
                                    appLogger.LogWarning($"web-page node could not be located for item with ID: {currentItemId}, stack-trace: {GetStackTrace()}");
                                }
                            }
                            else
                            {
                                // No permissions found means that we need to strip this item because this user does not have any permissions on this item
                                SetAttribute(nodeItem, "delete", "true");
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Could not find breadcrumbtrail for item with ID: '{currentItemId}', stack-trace: {GetStackTrace()}");
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"Could not find item id, stack-trace: {GetStackTrace()}");
                    }
                }

                // Add the full hierarchy to the cache
                if (setCache && !isApiRoute())
                {
                    projectVars.rbacCache.AddHierarchy(hierarchyId, "full", xmlStrippedHierarchyInternal, "GenerateStrippedRbacHierarchy()");
                }
                else
                {
                    // appLogger.LogCritical($"!! Skipped adding full hierarchy to RBAC cache (projectId: {projectVars.projectId}, hierarchyId: {hierarchyId})");
                }

                // Generate the stripped hierarchy by removing all the elements that we do not want to work with anymore
                RemoveMarkedItemsFromHierarchy(ref xmlStrippedHierarchyInternal);

                // Add the stripped hierarchy to the cache
                if (hierarchyId != "taxxoreditor" && setCache && !projectVars.currentUser.IsAuthenticated)
                {
                    setCache = false;
                }

                if (!setCache || !storeHierarchyInCache)
                {
                    if (!projectVars.rbacCache.ContainsHierarchy(hierarchyId, "stripped"))
                    {
                        storeHierarchyInCache = true;
                        setCache = true;
                    }
                }

                if (storeHierarchyInCache && !isApiRoute() && setCache)
                {
                    projectVars.rbacCache.AddHierarchy(hierarchyId, "stripped", xmlStrippedHierarchyInternal, "GenerateStrippedRbacHierarchy()");
                }
                else
                {
                    // appLogger.LogCritical($"!! Skipped adding stripped hierarchy to RBAC cache (projectId: {projectVars.projectId}, hierarchyId: {hierarchyId}, storeHierarchyInCache: {storeHierarchyInCache}, isApiRoute(), {isApiRoute()}, setCache: {setCache})");
                }

                // if (dumpInfo && debugRoutine)
                // {
                //     Console.WriteLine("################");
                //     Console.WriteLine(PrettyPrintXml(xmlPermissions.OuterXml));
                //     Console.WriteLine("################");

                //     Console.WriteLine("** Breadcrumbtrails **");
                //     Console.WriteLine(string.Join(Environment.NewLine, dictItemIdsBreadcrumbTrails));
                //     Console.WriteLine("");

                //     Console.WriteLine("");
                //     Console.WriteLine("--------- STRIPPED HIERARCHY ---------");
                //     Console.WriteLine($"- debugInfo: {debugInfo}");
                //     Console.WriteLine(PrettyPrintXml(xmlStrippedHierarchyInternal.OuterXml).Substring(0, 2000));
                //     Console.WriteLine("--------------------------------------");
                //     Console.WriteLine("");
                // }

                if (debugRoutine && hierarchyId == debugHierarchyId)
                {
                    await xmlPermissions.SaveAsync($"{logRootPathOs}/inspector/permissions-{projectVars.projectId}-{hierarchyId}-{projectVars.currentUser.Id}.xml", false);
                }
            }
            else
            {
                if (debugRoutine) appLogger.LogInformation($"Using cached hierarchy  (hierarchyId: {hierarchyId}) :-)");
            }

            return xmlStrippedHierarchyInternal;

        }

        /// <summary>
        /// Generates a special hierarchy that can be used to check generate breadcrumbtrails and check the permissions of a specific section in the output channel
        /// </summary>
        /// <param name="xmlTaxxorHierarchy"></param>
        /// <param name="xmlOutputChannelHierarchy"></param>
        /// <returns></returns>
        public static XmlDocument CreateHierarchyForRbacPermissionsRetrieval(XmlDocument xmlTaxxorHierarchy, XmlDocument xmlOutputChannelHierarchy)
        {
            try
            {
                // Hierarchy that we will be building up
                var xmlPermissionsHierarchy = new XmlDocument();

                // We assume that we are dealing with an output channel hierarchy - we need to mimic that we are inside the hierarchy in order to retrieve the correct permissions from the RBAC service
                var nodeItems = xmlPermissionsHierarchy.CreateNode(XmlNodeType.Element, "items", null);
                var fakeHierarchyRootNode = xmlPermissionsHierarchy.CreateNode(XmlNodeType.Element, "structured", null);
                nodeItems.AppendChild(fakeHierarchyRootNode);
                xmlPermissionsHierarchy.AppendChild(nodeItems);


                var nodeFilingEditor = xmlTaxxorHierarchy.SelectSingleNode("/items/structured/item/sub_items//item[@id='cms_content-editor']");
                var ancestorHierarchyItems = nodeFilingEditor.SelectNodes("ancestor-or-self::item");
                foreach (XmlNode hierarchyNode in ancestorHierarchyItems)
                {
                    // Add item/sub_items structure
                    var itemId = GetAttribute(hierarchyNode, "id");
                    var nodeItem = xmlPermissionsHierarchy.CreateElement("item");
                    SetAttribute(nodeItem, "id", itemId);

                    var nodeWebPageImported = xmlPermissionsHierarchy.ImportNode(hierarchyNode.SelectSingleNode("web_page"), true);
                    nodeItem.AppendChild(nodeWebPageImported);

                    fakeHierarchyRootNode = fakeHierarchyRootNode.AppendChild(nodeItem);
                    fakeHierarchyRootNode = fakeHierarchyRootNode.AppendChild(xmlPermissionsHierarchy.CreateElement("sub_items"));
                }

                var nodeSubItems = xmlPermissionsHierarchy.SelectSingleNode("/items/structured/item//item/sub_items[item/@id='cms_content-editor']");
                var nodeListSiblings = nodeFilingEditor.SelectNodes("following-sibling::item[not(@hidefromui)]");
                foreach (XmlNode nodeSibling in nodeListSiblings)
                {
                    var nodeItemImported = xmlPermissionsHierarchy.ImportNode(nodeSibling, true);
                    nodeSubItems.AppendChild(nodeItemImported);
                }

                // Add the complete output channel hierarchy
                var outputChannelRootNode = xmlOutputChannelHierarchy.SelectSingleNode("/items/structured/item");
                if (outputChannelRootNode != null)
                {
                    outputChannelRootNode.SetAttribute("outputchannelroot", "true");
                    var nodeOutputChannelImported = xmlPermissionsHierarchy.ImportNode(outputChannelRootNode, true);
                    fakeHierarchyRootNode.AppendChild(nodeOutputChannelImported);

                    // if (debugRoutine && hierarchyId == "hierarchy-pdf-en")
                    // {
                    //     Console.WriteLine("");
                    //     Console.WriteLine("");
                    //     Console.WriteLine("");
                    //     Console.WriteLine("");
                    //     Console.WriteLine(PrettyPrintXml(xmlPermissionsHierarchy.OuterXml));
                    //     Console.WriteLine("");
                    //     Console.WriteLine("");
                    //     Console.WriteLine("");
                    //     Console.WriteLine("");
                    // }

                    return xmlPermissionsHierarchy;
                }


                var errorMessage = "Unable to generate permissions hierarchy because root item not found in the taxxor hierarchy";
                appLogger.LogError(errorMessage);
                return GenerateErrorXml(errorMessage, "");
            }
            catch (Exception ex)
            {
                var errorMessage = "Unable to generate permissions hierarchy";
                appLogger.LogError(ex, errorMessage);
                return GenerateErrorXml(errorMessage, ex.ToString());
            }

        }


        /// <summary>
        /// Helper function to removed marked elements from a hierarchy
        /// </summary>
        /// <param name="xmlHierarchy"></param>
        public static void RemoveMarkedItemsFromHierarchy(ref XmlDocument xmlHierarchy)
        {
            foreach (XmlNode nodeToRemove in xmlHierarchy.SelectNodes("//item[@delete='true']"))
            {
                try
                {
                    nodeToRemove.ParentNode.RemoveChild(nodeToRemove);
                }
                catch (Exception ex)
                {
                    appLogger.LogWarning($"Could not remove node. error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }
        }
    }
}