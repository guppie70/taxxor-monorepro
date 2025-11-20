using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;

/// <summary>
/// Object to be used in SOAP, REST, Web Requests and define custom HTTP Header properties
/// </summary>
namespace Taxxor.Project
{

    /// <summary>
    /// Generic utilities for making the application work
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Utility class to work with the RBAC cache
        /// </summary>
        public class RbacCache
        {

            private bool _debugRoutine { get; set; }
            private string? _userId { get; set; }
            private string? _projectId { get; set; }
            private XmlDocument _xmlCache { get; set; } = new XmlDocument();
            private XmlNode? _nodeCurrentUser { get; set; } = null;
            private XmlNode? _nodeCurrentUserProject { get; set; } = null;
            private XmlNode? _nodeHierarchyRoot { get; set; } = null;
            // $"{dataRootPathOs}/temp/tp-issue"
            private string? _fixedCacheFolderPathOs = null;

            private XmlDocument _renderBasicProjectDocument()
            {
                /*
                <project>
                    <permissions/>
                    <hierarchies/> 
                </project>
                */

                var xmlProjectPermissions = new XmlDocument();
                var nodeProject = xmlProjectPermissions.CreateElement("project");
                nodeProject.SetAttribute("id", this._projectId);
                nodeProject.AppendChild(xmlProjectPermissions.CreateElement("permissions"));
                nodeProject.AppendChild(xmlProjectPermissions.CreateElement("hierarchies"));

                xmlProjectPermissions.AppendChild(nodeProject);
                return xmlProjectPermissions;
            }

            private XmlNode? _getBasicProjectNode()
            {
                return _renderBasicProjectDocument().DocumentElement;
            }

            private XmlDocument _renderUserCacheDocument()
            {
                /*
                <user>
                    <root>
                        <permissions/>
                        <hierarchies/> 
                    </root>
                    <projects/>
                </user>
                */

                var xmlUserPermissions = new XmlDocument();
                var nodeUser = xmlUserPermissions.CreateElement("user");
                nodeUser.SetAttribute("id", this._userId);

                var nodeRoot = xmlUserPermissions.CreateElement("root");
                nodeRoot.AppendChild(xmlUserPermissions.CreateElement("permissions"));
                nodeRoot.AppendChild(xmlUserPermissions.CreateElement("hierarchies"));
                nodeUser.AppendChild(nodeRoot);
                nodeUser.AppendChild(xmlUserPermissions.CreateElement("projects"));

                xmlUserPermissions.AppendChild(nodeUser);
                return xmlUserPermissions;
            }

            private XmlNode _getUserCacheRootNode()
            {
                return _renderUserCacheDocument().DocumentElement;
            }

            /// <summary>
            /// Empty constructor so that we can access the public methods
            /// </summary>
            public RbacCache() { }

            /// <summary>
            /// Initiates the cache
            /// </summary>
            /// <param name="userId"></param>
            /// <param name="projectId"></param>
            public RbacCache(string userId, string? projectId)
            {
                // appLogger.LogInformation($"Create RbacCache('{userId}, '{projectId}')");
                this._debugRoutine = siteType == "local";
                this._userId = userId;
                this._projectId = projectId;

                try
                {
                    if (this._fixedCacheFolderPathOs != null)
                    {
                        //
                        // => Running in special debugging mode where we use fixed cache documents in the form <<userid>>.xml to debug issues
                        //
                        if (Directory.Exists(this._fixedCacheFolderPathOs))
                        {
                            // Populate the global RBAC cache dictionary with the contents of this folder
                            RbacCacheData.Clear();
                            foreach (var rbacCacheFilePathOs in Directory.GetFiles(this._fixedCacheFolderPathOs))
                            {
                                try
                                {
                                    var debugUserId = Path.GetFileNameWithoutExtension(rbacCacheFilePathOs);
                                    var xmlDebugPermissions = new XmlDocument();
                                    xmlDebugPermissions.Load(rbacCacheFilePathOs);
                                    RbacCacheData.TryAdd(debugUserId, xmlDebugPermissions);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, "Unable to load fixed debugging RBAC cache file into the global object");
                                }
                            }

                        }
                        else
                        {
                            appLogger.LogError($"Folder with fixed RBAC cache content could not be located. this._fixedCacheFolderPathOs: {this._fixedCacheFolderPathOs}");
                        }
                    }

                    //
                    // => Initiate the entry in the cache dictionary
                    //
                    this.GenerateRbacCacheDataEntry();

                }
                catch (Exception exOverall)
                {
                    appLogger.LogError(exOverall, $"Could not initiate RBAC cache");
                }
            }

            private void GenerateRbacCacheDataEntry()
            {
                //
                // => Inject a new user RBAC cache entry if it does not exist
                //
                if (!RbacCacheData.ContainsKey(this._userId))
                {
                    if (!RbacCacheData.TryAdd(this._userId, _renderUserCacheDocument()))
                    {
                        throw new Exception($"Unable to add {this._userId} to the RbacCacheData dictionary");
                    };
                }


                //
                // => Retrieve a local copy of the RBAC cache content that we can work with
                //
                this._xmlCache.ReplaceContent(RbacCacheData[this._userId]);


                //
                // => Get the local cache root node
                //
                this._nodeCurrentUser = this._xmlCache.DocumentElement;

                // - Failover to make sure we always have something to work with
                if (this._nodeCurrentUser == null)
                {
                    appLogger.LogError("Somehow we were unable to retrieve a local copy of the user's RBAC cache from the global data source");

                    this._xmlCache = _renderUserCacheDocument();
                    this._nodeCurrentUser = this._xmlCache.DocumentElement;
                }

                //
                // => Create an entry for the project if that is required
                //
                if (!string.IsNullOrEmpty(this._projectId))
                {

                    // - Create a project entry in the global data object if needed
                    var nodeCurrentUserProject = RbacCacheData[this._userId].SelectSingleNode($"/user/projects/project[@id='{this._projectId}']");
                    if (nodeCurrentUserProject == null)
                    {
                        try
                        {
                            // Use thread-safe method of updating the global cache entry
                            RbacCacheData.AddOrUpdate(this._userId, (userId) =>
                            {
                                RbacCacheData.TryAdd(userId, _renderUserCacheDocument());
                                var projectNodeImported = RbacCacheData[userId].ImportNode(_getBasicProjectNode(), true);
                                RbacCacheData[userId].SelectSingleNode($"/user/projects").AppendChild(projectNodeImported);
                                return RbacCacheData[userId];
                            }, (userId, xmlRbacCache) =>
                            {
                                var projectNodeImported = xmlRbacCache.ImportNode(_getBasicProjectNode(), true);
                                xmlRbacCache.SelectSingleNode($"/user/projects").AppendChild(projectNodeImported);
                                return xmlRbacCache;
                            });
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Could not append project with ID '{this._projectId}' to the global permissions object. stack-trace: {GetStackTrace()}");
                        }
                        nodeCurrentUserProject = RbacCacheData[this._userId].SelectSingleNode($"/user/projects/project[@id='{this._projectId}']");
                    }


                    // - Create a project entry in the local cache object if needed
                    this._nodeCurrentUserProject = this._nodeCurrentUser.SelectSingleNode($"projects/project[@id='{this._projectId}']");
                    if (this._nodeCurrentUserProject == null)
                    {
                        try
                        {
                            var projectNodeImported = this._xmlCache.ImportNode(_getBasicProjectNode(), true);
                            this._nodeCurrentUser.SelectSingleNode($"projects").AppendChild(projectNodeImported);
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Could not append project with ID '{this._projectId}' to the local RBAC cache variable. stack-trace: {GetStackTrace()}");
                        }

                        this._nodeCurrentUserProject = this._nodeCurrentUser.SelectSingleNode($"projects/project[@id='{this._projectId}']");
                    }
                }

                // Retrieve the hierarchy root so that we can efficiently work with the hierarchies
                this._nodeHierarchyRoot = string.IsNullOrEmpty(this._projectId) ?
                    this._nodeCurrentUser.SelectSingleNode("root/hierarchies") :
                    this._nodeCurrentUserProject.SelectSingleNode("hierarchies");


            }

            /// <summary>
            /// Attempts to retrieve an hierarchy from the local cache
            /// </summary>
            /// <param name="hierarchyType">Supported are "stripped" or "full"</param>
            /// <returns></returns>
            public XmlDocument? GetHierarchy(string hierarchyId, string hierarchyType)
            {
                // if (this._debugRoutine) appLogger.LogInformation($"GetHierarchy('{hierarchyId}', '{hierarchyType}')");

                if (this._nodeHierarchyRoot != null)
                {
                    // Retrieve the node containing the hierarchy that we are looking for
                    var nodeHierarchy = this._nodeHierarchyRoot.SelectSingleNode($"hierarchy[@id='{hierarchyId}' and @type='{hierarchyType}']");

                    if (nodeHierarchy == null || nodeHierarchy.FirstChild == null)
                    {
                        // if (this._debugRoutine) appLogger.LogDebug($"Unable to find hierarchy in RBAC cache. hierarchyId: {hierarchyId}, hierarchyType: {hierarchyType}");
                        return null;
                    }
                    else
                    {
                        var xmlCachedHierarchy = new XmlDocument();
                        xmlCachedHierarchy.ReplaceContent(nodeHierarchy.FirstChild);
                        // if (this._debugRoutine) appLogger.LogCritical($"Successfully retrieved ('{hierarchyId}', '{hierarchyType}') from cache!!!!!");
                        return xmlCachedHierarchy;
                    }
                }
                else
                {
                    // if (this._debugRoutine) appLogger.LogDebug($"Unable to find hierarchy root node in RBAC cache. hierarchyId: {hierarchyId}, hierarchyType: {hierarchyType}");
                    return null;
                }
            }

            /// <summary>
            /// Adds a hierarchy to the cache
            /// </summary>
            /// <param name="hierarchyId">Hierarchy id - use "taxxoreditor" or another arbitrary unique identifier</param>
            /// <param name="hierarchyType">Supported are "raw", "stripped" or "full"</param>
            /// <param name="xmlHierarchyToCache"></param>
            public void AddHierarchy(string hierarchyId, string hierarchyType, XmlDocument xmlHierarchyToCache, string callingMethod)
            {
                if (this._fixedCacheFolderPathOs == null)
                {
                    var debugHierarchyId = "xyz";

                    if (this._debugRoutine)
                    {
                        var debugString = $"AddHierarchy('{hierarchyId}', '{hierarchyType}')";
                        appLogger.LogInformation($"{debugString.PadRight(60)}{((callingMethod == null) ? "" : " - " + callingMethod)}");
                    }

                    if (this._debugRoutine && hierarchyId == debugHierarchyId)
                    {
                        appLogger.LogInformation($"Debugging {debugHierarchyId}");
                    }

                    try
                    {
                        //
                        // => Make sure that an rbac cache entry exists for the user
                        //
                        if (!RbacCacheData.ContainsKey(this._userId))
                        {
                            try
                            {
                                // Use thread-safe method of updating the global cache entry
                                RbacCacheData.AddOrUpdate(this._userId, (userId) =>
                                {
                                    RbacCacheData.TryAdd(userId, _renderUserCacheDocument());
                                    var projectNodeImported = RbacCacheData[userId].ImportNode(_getBasicProjectNode(), true);
                                    RbacCacheData[userId].SelectSingleNode($"/user/projects").AppendChild(projectNodeImported);
                                    return RbacCacheData[userId];
                                }, (userId, xmlRbacCache) =>
                                {
                                    var projectNodeImported = xmlRbacCache.ImportNode(_getBasicProjectNode(), true);
                                    xmlRbacCache.SelectSingleNode($"/user/projects").AppendChild(projectNodeImported);
                                    return xmlRbacCache;
                                });
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, $"Could not append project with ID '{this._projectId}' to the global permissions object. stack-trace: {GetStackTrace()}");
                            }
                        }


                        //
                        // => Add to global cache object
                        //
                        XmlDocument? xmlRbacCache = null;
                        if (RbacCacheData.TryGetValue(this._userId, out xmlRbacCache))
                        {
                            var nodeHierarchyRoot = GetHierarchyRoot(xmlRbacCache);
                            XmlNode? nodeHierarchy = null;
                            if (nodeHierarchyRoot != null) nodeHierarchy = nodeHierarchyRoot.SelectSingleNode($"hierarchy[@id='{hierarchyId}' and @type='{hierarchyType}']");

                            if (nodeHierarchy == null)
                            {
                                // Use thread-safe method of updating the global cache entry
                                RbacCacheData.AddOrUpdate(this._userId, (userId) =>
                                {
                                    // User ID is not present in the RbacCacheData dictionary - so we need to try and add it
                                    this.GenerateRbacCacheDataEntry();
                                    return RbacCacheData[userId];
                                }, (userId, xmlRbacCache) =>
                                {
                                    var nodeHierarchyRootLocal = GetHierarchyRoot(xmlRbacCache);

                                    // Create a new hierarchy node
                                    if (nodeHierarchyRootLocal != null)
                                    {
                                        var nodeHierarchy = xmlRbacCache.CreateElement("hierarchy");
                                        SetAttribute(nodeHierarchy, "id", hierarchyId);
                                        SetAttribute(nodeHierarchy, "type", hierarchyType);
                                        nodeHierarchyRootLocal.AppendChild(nodeHierarchy);
                                    }

                                    return xmlRbacCache;
                                });

                                // Select the nodes in the global cache dictionary so we can use it later to update the cache
                                nodeHierarchyRoot = GetHierarchyRoot(RbacCacheData[this._userId]);
                                nodeHierarchy = nodeHierarchyRoot?.SelectSingleNode($"hierarchy[@id='{hierarchyId}' and @type='{hierarchyType}']");
                            }
                            else
                            {
                                // - Remove a hierarchy if it already exists
                                if (nodeHierarchy.FirstChild != null) RemoveXmlNode(nodeHierarchy.FirstChild);
                            }

                            //
                            // => Run some tests to make sure we are adding a hierarchy that is valid enough to store in the cache
                            //
                            var addHierarchyToCache = true;
                            switch (hierarchyType)
                            {
                                case "full":
                                    // Test if the root node contains a delete marker
                                    if (hierarchyId == "taxxoreditor")
                                    {
                                        var nodeItemTaxxorHomePage = xmlHierarchyToCache.SelectSingleNode("/items/structured/item[@id='cms-overview']");
                                        var nodeItemTaxxorEditor = xmlHierarchyToCache.SelectSingleNode("/items/structured/item/sub_items/item/sub_items/item[@id='cms_content-editor']");
                                        if (nodeItemTaxxorHomePage == null || nodeItemTaxxorEditor == null)
                                        {
                                            addHierarchyToCache = false;
                                        }
                                        else
                                        {
                                            if (nodeItemTaxxorHomePage.HasAttribute("delete"))
                                            {
                                                addHierarchyToCache = false;
                                            }
                                            else if (nodeItemTaxxorEditor.HasAttribute("delete"))
                                            {
                                                addHierarchyToCache = false;
                                            }
                                        }
                                    }

                                    break;

                                case "stripped":
                                    // Test if the hierarchy actually contains any hierarchical elements
                                    if (hierarchyId == "taxxoreditor")
                                    {
                                        if (xmlHierarchyToCache.SelectSingleNode("/items/structured/item") == null) addHierarchyToCache = false;
                                    }

                                    break;

                            }

                            //
                            // => Add the cache objects
                            //
                            if (addHierarchyToCache)
                            {
                                // - Global cache

                                // Use thread-safe method of updating the global cache entry
                                RbacCacheData.AddOrUpdate(this._userId, (userId) =>
                                {
                                    // User ID is not present in the RbacCacheData dictionary - so we need to try and add it
                                    this.GenerateRbacCacheDataEntry();
                                    return RbacCacheData[userId];
                                }, (userId, xmlRbacCache) =>
                                {
                                    var nodeHierarchyRootLocal = GetHierarchyRoot(xmlRbacCache);

                                    // Add the hierarchy to the global rbac cache collection
                                    if (nodeHierarchyRootLocal != null)
                                    {
                                        var hierarchyNodeImported = xmlRbacCache.ImportNode(xmlHierarchyToCache.DocumentElement, true);
                                        var nodeHierarchyLocal = nodeHierarchyRootLocal.SelectSingleNode($"hierarchy[@id='{hierarchyId}' and @type='{hierarchyType}']");
                                        if (nodeHierarchyLocal != null)
                                        {
                                            nodeHierarchyLocal.AppendChild(hierarchyNodeImported);
                                        }
                                        else
                                        {
                                            appLogger.LogError($"Unable to add hierarchy with ID {hierarchyId} and type {hierarchyType} the global RBAC cache for user {userId} because the local hierarchy node could not be located");
                                        }

                                        //
                                        // => Set the permissions
                                        //
                                        SetPermissions(nodeHierarchyLocal, hierarchyId, hierarchyType);
                                    }
                                    else
                                    {
                                        appLogger.LogError($"Unable to add hierarchy with ID {hierarchyId} and type {hierarchyType} the global RBAC cache for user {userId} because the root node could not be located");
                                    }

                                    return xmlRbacCache;
                                });

                                // var hierarchyNodeImported = RbacCacheData[this._userId].ImportNode(xmlHierarchyToCache.DocumentElement, true);
                                // nodeHierarchy.AppendChild(hierarchyNodeImported);

                                /// - Local cache
                                var nodeLocalHierarchy = this._nodeHierarchyRoot.SelectSingleNode($"hierarchy[@id='{hierarchyId}' and @type='{hierarchyType}']");

                                // Create an hierarchy node or remove the contents of a hierarchy node if it already exists 
                                if (nodeLocalHierarchy == null)
                                {
                                    nodeLocalHierarchy = this._xmlCache.CreateElement("hierarchy");
                                    SetAttribute(nodeLocalHierarchy, "id", hierarchyId);
                                    SetAttribute(nodeLocalHierarchy, "type", hierarchyType);
                                    this._nodeHierarchyRoot.AppendChild(nodeLocalHierarchy);
                                }
                                else
                                {
                                    if (nodeLocalHierarchy.FirstChild != null) RemoveXmlNode(nodeLocalHierarchy.FirstChild);
                                }

                                // Add the hierarchy to the Xml cache document
                                var nodeLocalHierarchyImported = this._xmlCache.ImportNode(xmlHierarchyToCache.DocumentElement, true);
                                nodeLocalHierarchy.AppendChild(nodeLocalHierarchyImported);

                                //
                                // => Set the permissions
                                //
                                SetPermissions(nodeLocalHierarchy, hierarchyId, hierarchyType);
                            }



                        }
                        else
                        {
                            appLogger.LogWarning($"Unable to add hierarchy ('{hierarchyId}', '{hierarchyType}') to cache because RbacCacheData['{this._userId}'] does not exist.");
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Unable to add hierarchy ('{hierarchyId}', '{hierarchyType}') to cache. stack-trace: {CreateStackInfo()}");
                    }
                }
                else
                {
                    appLogger.LogInformation($"Adding hierarchy with ID {hierarchyId} and type {hierarchyType} disabled");
                }

                /// <summary>
                /// Helper routine to find the root node containing the hierarchies we need to work with
                /// </summary>
                /// <param name="xmlRbacCache"></param>
                /// <returns></returns>
                XmlNode? GetHierarchyRoot(XmlDocument xmlRbacCache)
                {
                    return string.IsNullOrEmpty(this._projectId) ?
                        xmlRbacCache?.SelectSingleNode($"/user/root/hierarchies") :
                        xmlRbacCache?.SelectSingleNode($"/user/projects/project[@id='{this._projectId}']/hierarchies");
                }

                /// <summary>
                /// Stores the base permissions in a special section of the cache
                /// </summary>
                /// <param name="nodeHierarchyLocal"></param>
                /// <param name="hierarchyId"></param>
                /// <param name="hierarchyType"></param>
                void SetPermissions(XmlNode nodeHierarchyRoot, string hierarchyId, string hierarchyType)
                {
                    // Determine if we need to set the permissions
                    var setPermissions = false;
                    if (hierarchyId == "taxxoreditor" && hierarchyType == "stripped")
                    {
                        setPermissions = true;
                    }

                    // Application permissions vs project permisions
                    if (setPermissions)
                    {
                        var nodeApplicationPermissionsRoot = nodeHierarchyRoot.ParentNode.PreviousSibling;
                        if (nodeApplicationPermissionsRoot == null)
                        {
                            nodeApplicationPermissionsRoot = nodeHierarchyRoot.ParentNode.ParentNode.AppendChild(
                                nodeHierarchyRoot.OwnerDocument.CreateElement("permissions")
                            );
                        }

                        var nodeListExistingApplicationPermissions = nodeApplicationPermissionsRoot.SelectNodes("permission");
                        foreach (XmlNode nodePermission in nodeListExistingApplicationPermissions)
                        {
                            nodeApplicationPermissionsRoot.RemoveChild(nodePermission);
                        }

                        // Determine the xpath to the item that we want to use for copying the permissions
                        var xPathItem = (nodeApplicationPermissionsRoot.ParentNode.LocalName == "root") ?
                            $"items/structured/item[@id='cms-overview']/permissions/permission" :
                            $"items/structured/item/sub_items/item[@id='cms_project-details']/permissions/permission";

                        var nodeListApplicationPermissions = nodeHierarchyRoot.SelectNodes(xPathItem);
                        foreach (XmlNode nodePermission in nodeListApplicationPermissions)
                        {
                            var nodePermissionCloned = nodePermission.CloneNode(false);
                            nodeApplicationPermissionsRoot.AppendChild(nodePermissionCloned);
                        }
                    }
                }
            }

            public int CountHierarchies()
            {
                if (this._nodeHierarchyRoot != null)
                {
                    // Retrieve the node containing the hierarchy that we are looking for
                    return this._nodeHierarchyRoot.SelectNodes($"hierarchy").Count;
                }
                else
                {
                    return 0;
                }
            }

            /// <summary>
            /// Checks if a hierarchy with the given ID and type exists in the RBAC cache
            /// </summary>
            /// <param name="hierarchyId"></param>
            /// <param name="hierarchyType"></param>
            /// <returns></returns>
            public bool ContainsHierarchy(string hierarchyId, string hierarchyType)
            {
                if (this._nodeHierarchyRoot != null)
                {
                    // Retrieve the node containing the hierarchy that we are looking for
                    var nodeHierarchy = this._nodeHierarchyRoot.SelectSingleNode($"hierarchy[@id='{hierarchyId}' and @type='{hierarchyType}']");

                    if (nodeHierarchy == null || nodeHierarchy.FirstChild == null)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// Stores permissions in the RBAC cache based on a permissions nodelist received from the access control service
            /// </summary>
            /// <param name="xPathPermissionsRootNode"></param>
            /// <param name="nodeListPermissions"></param>
            public void StorePermissions(string xPathPermissionsRootNode, XmlNodeList nodeListPermissions)
            {
                // Use thread-safe method of updating the global cache entry
                RbacCacheData.AddOrUpdate(this._userId, (userId) =>
                {
                    // User ID is not present in the RbacCacheData dictionary - so we need to try and add it
                    this.GenerateRbacCacheDataEntry();
                    return RbacCacheData[userId];
                }, (userId, xmlRbacCache) =>
                {
                    var nodePermissionsRoot = xmlRbacCache.SelectSingleNode(xPathPermissionsRootNode);
                    if (nodePermissionsRoot != null)
                    {
                        // Remove potential existing permissions
                        var nodeListExistingApplicationPermissions = nodePermissionsRoot.SelectNodes("permission");
                        foreach (XmlNode nodePermission in nodeListExistingApplicationPermissions)
                        {
                            nodePermissionsRoot.RemoveChild(nodePermission);
                        }

                        // Inject the new permissions
                        var injectedPermissionIds = new List<string>();
                        foreach (XmlNode nodePermission in nodeListPermissions)
                        {
                            var permissionId = nodePermission.GetAttribute("id");
                            if (permissionId != null && !injectedPermissionIds.Contains(permissionId))
                            {
                                var nodeNewPermission = xmlRbacCache.CreateElement("permission");
                                nodeNewPermission.SetAttribute("id", permissionId);
                                nodePermissionsRoot.AppendChild(nodeNewPermission);
                                injectedPermissionIds.Add(permissionId);
                            }
                            else
                            {
                                appLogger.LogWarning($"Permission could not be added because the ID could not be found");
                            }
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"Unable to set permissions because '{xPathPermissionsRootNode}' returned null.");
                    }

                    return xmlRbacCache;
                });
            }


            /// <summary>
            /// Clears the complete global cache object
            /// </summary>
            public void ClearAll()
            {
                RbacCacheData.Clear();
            }

            /// <summary>
            /// Removes the hierarchy cache entries of a specific hierarchy of the current project or of another project (optional argument)
            /// </summary>
            /// <param name="hierarchyId"></param>
            /// <param name="projectId"></param>
            public void ClearHierarchy(string hierarchyId, string projectId = null)
            {
                var projectIdToUse = string.IsNullOrEmpty(projectId) ? this._projectId : projectId;
                if (this._fixedCacheFolderPathOs == null)
                {
                    //
                    // => Remove hierarchy for all users from the global cache object
                    //
                    foreach (var rbacEntry in RbacCacheData)
                    {
                        var userId = rbacEntry.Key;
                        var xmlRbacCache = new XmlDocument();
                        xmlRbacCache.ReplaceContent(rbacEntry.Value);
                        var nodeListGlobalHierarchiesToRemove = xmlRbacCache.SelectNodes($"/user/projects/project[@id='{projectIdToUse}']/hierarchies/hierarchy[@id='{hierarchyId}']");
                        if (nodeListGlobalHierarchiesToRemove.Count == 0)
                        {
                            appLogger.LogInformation($"No hierarchies found to remove from global cache. userId: {userId}, hierarchyId: {hierarchyId}, projectId: {projectIdToUse}");
                        }
                        else
                        {
                            foreach (XmlNode nodeHierarchyToRemove in nodeListGlobalHierarchiesToRemove)
                            {
                                if (nodeHierarchyToRemove.FirstChild != null)
                                {
                                    RemoveXmlNode(nodeHierarchyToRemove.FirstChild);
                                }
                            }

                            // - Place the updated XML document back into the dictionary
                            rbacEntry.Value.ReplaceContent(xmlRbacCache);
                        }
                    }


                    //
                    // => Remove hierarchy from local cache
                    //
                    var nodeListHierarchiesToRemove = this._xmlCache.SelectNodes($"/user/projects/project[@id='{projectIdToUse}']/hierarchies/hierarchy[@id='{hierarchyId}']");
                    if (nodeListHierarchiesToRemove.Count == 0)
                    {
                        appLogger.LogInformation($"No hierarchies found to remove from local cache. hierarchyId: {hierarchyId}, projectId: {projectIdToUse}");
                    }
                    else
                    {
                        foreach (XmlNode nodeHierarchyToRemove in nodeListHierarchiesToRemove)
                        {
                            if (nodeHierarchyToRemove.FirstChild != null)
                            {
                                RemoveXmlNode(nodeHierarchyToRemove.FirstChild);
                            }
                        }
                    }
                }
                else
                {
                    appLogger.LogInformation($"Removing hierarchy with ID {hierarchyId} and in project {projectIdToUse} disabled");
                }
            }

            /// <summary>
            /// Clears the base hierarchy from the cache
            /// </summary>
            public void ClearBaseHierarchy()
            {

                //
                // => Remove hierarchy from global cache
                //
                foreach (var rbacEntry in RbacCacheData)
                {
                    var userId = rbacEntry.Key;
                    var xmlRbacCache = new XmlDocument();
                    xmlRbacCache.ReplaceContent(rbacEntry.Value);
                    var nodeListGlobalHierarchiesToRemove = xmlRbacCache.SelectNodes($"/user/root/hierarchies");
                    if (nodeListGlobalHierarchiesToRemove.Count == 0)
                    {
                        appLogger.LogInformation($"No Taxxor root hierarchies found to remove from the global cache object. userId: {userId}");
                    }
                    else
                    {
                        foreach (XmlNode nodeHierarchyToRemove in nodeListGlobalHierarchiesToRemove)
                        {
                            if (nodeHierarchyToRemove.FirstChild != null)
                            {
                                RemoveXmlNode(nodeHierarchyToRemove.FirstChild);
                            }
                        }

                        // - Place the updated XML document back into the dictionary
                        rbacEntry.Value.ReplaceContent(xmlRbacCache);
                    }
                }


                //
                // => Remove hierarchy from local cache
                //
                var nodeListHierarchiesToRemove = this._xmlCache.SelectNodes($"/user/root/hierarchies");
                if (nodeListHierarchiesToRemove.Count == 0)
                {
                    appLogger.LogInformation($"No Taxxor root hierarchies found to remove from the local cache object. stack-trace: {GetStackTrace()}");
                }
                else
                {
                    foreach (XmlNode nodeHierarchyToRemove in nodeListHierarchiesToRemove)
                    {
                        if (nodeHierarchyToRemove.FirstChild != null)
                        {
                            RemoveXmlNode(nodeHierarchyToRemove.FirstChild);
                        }
                    }
                }

            }

            /// <summary>
            /// Removes the complete RBAC cache for a specific user
            /// </summary>
            public void ClearUserCache()
            {
                //
                // => Remove user RBAC cache from global cache
                //
                if (RbacCacheData.ContainsKey(this._userId))
                {
                    var xmlRbacInfo = new XmlDocument();
                    RbacCacheData.TryRemove(this._userId, out xmlRbacInfo);
                }
                else
                {
                    appLogger.LogInformation($"No cache available so nothing to remove for {this._userId}");
                }


                //
                // => Remove user RBAC cache from local cache
                //
                if (this._xmlCache.DocumentElement != null)
                {
                    this._xmlCache = new XmlDocument();
                }
                else
                {
                    appLogger.LogDebug($"No cache available so nothing to remove for {this._userId}");
                }
            }

            /// <summary>
            /// Removes the global RBAC cache for a specific user
            /// </summary>
            /// <param name="userId"></param>
            public void ClearUserCache(string userId)
            {
                //
                // => Remove user RBAC cache from global cache
                //
                if (RbacCacheData.ContainsKey(userId))
                {
                    var xmlRbacInfo = new XmlDocument();
                    RbacCacheData.TryRemove(userId, out xmlRbacInfo);
                }
                else
                {
                    appLogger.LogInformation($"No cache available so nothing to remove for {userId}");
                }
            }

        }


        /// <summary>
        /// Stores the complete content of the RBAC cache on the file system
        /// </summary>
        /// <param name="prettyPrintXml"></param>
        /// <returns></returns>
        public static async Task DumpRbacContents(bool prettyPrintXml = false)
        {
            try
            {
                var rootFolderPathOs = $"{logRootPathOs}/inspector/rbac-cache";
                if (!Directory.Exists(rootFolderPathOs))
                {
                    Directory.CreateDirectory(rootFolderPathOs);
                }

                DelTree(rootFolderPathOs, false);

                foreach (var rbacEntry in RbacCacheData)
                {
                    var userId = rbacEntry.Key;
                    var xmlRbacCache = rbacEntry.Value;

                    await xmlRbacCache.SaveAsync($"{rootFolderPathOs}/{userId}.xml", true, prettyPrintXml);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: There was an error storing the rbac cache content in the log directory. error: {ex}");
            }
        }

    }

}