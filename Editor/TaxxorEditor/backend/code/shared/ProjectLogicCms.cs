using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    /// <summary>
    /// Collection of CMS specific utilities
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Class that will contain the content of the different metadata structures in use by the CMS
        /// </summary>
        public class MetaData
        {
            //generic properties for the user class
            public string Id = string.Empty;
            public string Name = string.Empty;
            public XmlDocument Xml = new XmlDocument();

            public MetaData(string id, string name, XmlDocument xmlData)
            {
                this.Name = name;
                this.Id = id;
                this.Xml = xmlData;
            }

            /// <summary>
            /// Formats the content of the metadata object instance as a string for debugging purposes
            /// </summary>
            /// <returns></returns>
            public string AsString()
            {
                return $"id: {this.Id}, name: {this.Name}, xml: {TruncateString(this.Xml.OuterXml, 200)}";
            }
        }


        public static string EnableAdditionalCache { get; set; } = "notset";

        /// <summary>
        /// Fills the cmsMetaData dictionary with outputchannel hierarchies
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="reqVars"></param>
        /// <param name="forceRetrieve"></param>
        /// <returns></returns>
        public static async Task<bool> RetrieveOutputChannelHierarchiesMetaData(ProjectVariables projectVars, RequestVariables reqVars, bool forceRetrieve = false, bool forceStoreInCache = false)
        {
            // Determine additional caching layer settings
            if (EnableAdditionalCache == "notset")
            {
                var nodeEnableAdditionalCache = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='enable-additional-rbac-caching']");
                if (nodeEnableAdditionalCache != null) { 
                    EnableAdditionalCache = nodeEnableAdditionalCache.InnerText; 
                    } else { 
                        EnableAdditionalCache = "true"; 
                        }
            }
            bool enableAdditionalCache = EnableAdditionalCache == "true";

            bool debugRoutine = siteType == "local";
            bool dumpFiles = debugRoutine && !isApiRoute();
            var debugHierarchyId = "hierarchy-participations-tochecklater";
            var stopProcessingOnError = applicationId == "taxxoreditor";
            // Console.WriteLine($"&&&& stopProcessingOnError: {stopProcessingOnError}, forceRetrieve: {forceRetrieve}, forceStoreInCache: {forceStoreInCache} &&&&");

            // Back out of this routine for specific pages that do not require the output channnel hierarchies
            if (!forceRetrieve)
            {
                if (
                    reqVars.pageId == "auditorviewcontent" ||
                    reqVars.pageId == "cms_auditor-view" ||
                    reqVars.pageId == "filingeditorlistlocks" ||
                    (reqVars.pageId == "filingeditorlock" && reqVars.method == RequestMethodEnum.Get) ||
                    (reqVars.pageId == "contentversion" && reqVars.method == RequestMethodEnum.Get) ||
                    reqVars.thisUrlPath.Contains("/proxy") ||
                    reqVars.thisUrlPath.Contains("/mappingservice") ||
                    reqVars.thisUrlPath.Contains("/dynamic_resources") ||
                    reqVars.thisUrlPath.Contains("_blazor") ||
                    reqVars.thisUrlPath.EndsWith("negotiate")
                ) return true;
            }

            try
            {
                var xmlNodeList = xmlApplicationConfiguration.SelectNodes($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/metadata_system/metadata");
                foreach (XmlNode xmlNode in xmlNodeList)
                {
                    var id = xmlNode.Attributes["id"].Value;
                    var name = xmlNode.SelectSingleNode("name").InnerText;
                    var xmlNodeLocation = xmlNode.SelectSingleNode("location");
                    var locationType = xmlNodeLocation.Attributes["type"].Value;

                    try
                    {
                        // Determine if we want to set the cache or not
                        var setRbacCache = false;

                        // Enable additional cache for specific pages
                        if (enableAdditionalCache)
                        {
                            if (applicationId == "taxxoreditor" && projectVars.currentUser.IsAuthenticated && !string.IsNullOrEmpty(projectVars.projectId))
                            {
                                setRbacCache = true;

                                if (reqVars.pageId == "cms_hierarchy-manager")
                                {
                                    setRbacCache = false;
                                }
                            }
                        }


                        // Log to console
                        // if (debugRoutine)
                        // {
                        //     appLogger.LogInformation($"id: {id}, name: {name}, setRbacCache: {setRbacCache.ToString().ToLower()}, url: {reqVars.rawUrl}");
                        // }

                        // Initiate the metadata document
                        XmlDocument? xmlHierarchyDocument = null;

                        // Attempt to use the RBAC cache for the Taxxor Editor
                        if (applicationId == "taxxoreditor" && projectVars.rbacCache != null)
                        {
                            xmlHierarchyDocument = projectVars.rbacCache.GetHierarchy(id, "stripped");
                        }

                        if (xmlHierarchyDocument == null)
                        {
                            if (debugRoutine && applicationId == "taxxoreditor")
                            {
                                appLogger.LogInformation($"Retrieve new output channel hierarchy with ID {id} and locationType {locationType} and incoming URL {reqVars.thisUrlPath}");
                            }

                            //
                            // => Retrieve the raw xml content for the metadata from the Taxxor Document Store
                            //
                            xmlHierarchyDocument = new XmlDocument();

                            if (locationType == "file")
                            {
                                if (applicationId == "documentstore")
                                {
                                    // Pick up the file from the local disk
                                    var xmlDocumentPathOs = CalculateFullPathOs(xmlNodeLocation);
                                    if (string.IsNullOrEmpty(xmlDocumentPathOs))
                                    {
                                        appLogger.LogError($"Could not calculate path. id: {id}, name: {name}, locationType: {locationType}, stack-trace: {GetStackTrace()}");
                                        if (stopProcessingOnError) { return false; } else { continue; }
                                    }

                                    if (File.Exists(xmlDocumentPathOs))
                                    {
                                        xmlHierarchyDocument.Load(xmlDocumentPathOs);
                                    }
                                    else
                                    {
                                        appLogger.LogError($"Could not find hierarchy file. xmlDocumentPathOs: {xmlDocumentPathOs}, stack-trace: {GetStackTrace()}");
                                        if (stopProcessingOnError) { return false; } else { continue; }
                                    }

                                }
                                else
                                {
                                    // - Attempt to retrieve the raw hierarchy from the cache
                                    if (projectVars.rbacCache != null)
                                    {
                                        xmlHierarchyDocument = projectVars.rbacCache.GetHierarchy(id, "raw");
                                    }


                                    // Call the Taxxor Document Store to retrieve the file
                                    if (xmlHierarchyDocument == null)
                                    {
                                        if (debugRoutine) appLogger.LogInformation($"* Retrieve new raw hierarchy for {id}");
                                        xmlHierarchyDocument = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.Load<XmlDocument>(xmlNodeLocation, true);
                                        if (XmlContainsError(xmlHierarchyDocument))
                                        {
                                            appLogger.LogError($"Failed to load metadata. id: {id}, name: {name}, locationType: {locationType}, stack-trace: {GetStackTrace()}");
                                            if (stopProcessingOnError) { return false; } else { continue; }
                                        }
                                        else
                                        {
                                            projectVars.rbacCache.AddHierarchy(id, "raw", xmlHierarchyDocument, "RetrieveOutputChannelHierarchiesMetaData()");
                                        }
                                    }
                                    else
                                    {
                                        //  if (debugRoutine) appLogger.LogInformation($"* Using cached version of raw hierarchy {id}");
                                    }
                                }

                            }


                            //
                            // => Check if this person has access to this output channel at all
                            //
                            var hasAccessToOutputChannel = (applicationId == "documentstore") ? true : false;
                            if (applicationId == "taxxoreditor")
                            {
                                var nodeHierarchyRootItem = xmlHierarchyDocument.SelectSingleNode("/items/structured/item");
                                if (nodeHierarchyRootItem == null)
                                {
                                    appLogger.LogError($"Could not find output channel hierarchy root node. id: {id}, name: {name}, locationType: {locationType}, stack-trace: {GetStackTrace()}");
                                    if (stopProcessingOnError) { return false; } else { continue; }

                                }

                                if (projectVars.currentUser.IsAuthenticated)
                                {
                                    XmlDocument xmlPermissions = await AccessControlService.RetrievePermissionsForResources($"get__taxxoreditor__{nodeHierarchyRootItem.GetAttribute("id")}__{projectVars.projectId},get__taxxoreditor__cms_content-editor,get__taxxoreditor__cms_project-details,get__taxxoreditor__cms-overview,root");
                                    if (XmlContainsError(xmlPermissions))
                                    {
                                        appLogger.LogWarning($"Could not retrieve permissions for resources. (projectId: {projectVars.projectId}, id: {id}, name: {name}, error: {ConvertErrorXml(xmlPermissions)}, stack-trace: {GetStackTrace()})");
                                    }

                                    hasAccessToOutputChannel = xmlPermissions.SelectSingleNode($"/items/item/permissions/permission[@id='view' or @id='all']") != null;
                                }
                                else
                                {
                                    appLogger.LogWarning($"Unable not retrieve permissions for resources as the user is not authenticated. (projectId: {projectVars.projectId}, id: {id}, name: {name}, stack-trace: {GetStackTrace()})");
                                }
                            }

                            //
                            // => If this user has access to the output channel, then continue processing
                            //
                            if (hasAccessToOutputChannel)
                            {
                                //
                                // => proceed with operations if nessecary
                                //
                                var xmlNodeListNested = xmlNode.SelectNodes("operations/operation");
                                //Response.Write(xmlNodeListNested.Count.ToString()+"<br/><br/><br/>");

                                foreach (XmlNode xmlNodeNested in xmlNodeListNested)
                                {
                                    var operationType = xmlNodeNested.Attributes["type"].Value;
                                    //Response.Write(operationType + "<br/><br/><br/>");
                                    switch (operationType)
                                    {
                                        case "server-side_function":
                                            if (xmlNodeNested.InnerText == "mark_edit_allowed")
                                            {

                                                if (applicationId == "taxxoreditor")
                                                {
                                                    // First render the stripped version of the hierarchy (and store the full version in the process as well)
                                                    xmlHierarchyDocument = await GenerateStrippedRbacHierarchy(id, xmlHierarchyDocument, reqVars, projectVars, false);
                                                }

                                                // TODO: this needs to become similar to GenerateStrippedRbacHierarchy()
                                                MarkHierarchyAccessRights(ref xmlHierarchyDocument);
                                            }
                                            break;

                                    }

                                }

                                if (id == debugHierarchyId)
                                {
                                    appLogger.LogInformation($"Debugging {debugHierarchyId}");
                                }

                                // Only add the stripped hierarchy when there is at least one editable element
                                if (setRbacCache)
                                {
                                    if (xmlHierarchyDocument.SelectNodes("/items/structured//item[@editable='true']").Count == 0) setRbacCache = false;
                                }

                                // Add the hierarchy to the cache
                                if (applicationId == "taxxoreditor" && setRbacCache)
                                {
                                    projectVars.rbacCache.AddHierarchy(id, "stripped", xmlHierarchyDocument, "RetrieveOutputChannelHierarchiesMetaData() - 1");
                                }
                                else
                                {
                                    //
                                    // => User has access to the output channel, but there are no editable sections in the hierarchy
                                    //

                                    // Console.WriteLine($"!!!! Stripped hierarchy for {id} not stored (editable nodes: {xmlHierarchyDocument.SelectNodes("/items/structured/item/sub_items//item[@editable='true']").Count}) !!!!");
                                }
                            }
                            else
                            {
                                // appLogger.LogInformation($"No need to fetch {id} as the user does not have permissions to this output channel at all");

                                //
                                // => This user does not have access to the output channel at all so we can insert a dummy hierarchy in the RBAC cache to prevent further processing
                                //
                                var xmlDummyHierarchy = new XmlDocument();
                                xmlDummyHierarchy.AppendChild(xmlDummyHierarchy.CreateElement("items"));
                                projectVars.rbacCache.AddHierarchy(id, "stripped", xmlDummyHierarchy, "RetrieveOutputChannelHierarchiesMetaData() - dummy because no access at all");
                            }
                        }
                        else
                        {
                            // if (debugRoutine && applicationId == "taxxoreditor") appLogger.LogInformation($"Using cache for output channel hierarchy with ID {id} :-)");
                        }

                        // Add the stripped output channel hierarchy to the project variables object
                        // if (debugRoutine) appLogger.LogInformation($"MetaData('{id}', '{name}', <<XmlDocument>>)");
                        var metaData = new MetaData(id, name, xmlHierarchyDocument);

                        // Add the metadata to the collection
                        if (projectVars.cmsMetaData.ContainsKey(id))
                        {
                            projectVars.cmsMetaData[id] = metaData;
                        }
                        else
                        {
                            projectVars.cmsMetaData.Add(id, metaData);
                        }

                        if (dumpFiles)
                        {
                            await xmlHierarchyDocument.SaveAsync($"{logRootPathOs}/inspector/outputchannel-structure-stripped-{projectVars.projectId}-{id}-{projectVars.currentUser.Id}.xml", false);
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Failed to load metadata. projectId: {projectVars.projectId}, hierarchyId: {id}, hierarchyName: {name}, userId: {projectVars.currentUser.Id}");

                        if (stopProcessingOnError) { return false; } else { continue; }
                    }
                }

                // Return something here
                return true;
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"There was an error in RetrieveOutputChannelHierarchiesMetaData(). stack-trace: {GetStackTrace()}");
                return false;
            }

        }

        /// <summary>
        /// Appends attributes on the hierarchy document to mark if this user can access it or not
        /// </summary>
        /// <param name="xmlOutputChannelHierarchy"></param>
        public static void MarkHierarchyAccessRights(ref XmlDocument xmlOutputChannelHierarchy)
        {

            foreach (XmlNode nodeHierarchyItem in xmlOutputChannelHierarchy.SelectNodes("//item"))
            {
                var editableValue = "false";

                if (nodeHierarchyItem.SelectNodes("permissions/permission[@id='all' or @id='editsection']").Count > 0) editableValue = "true";

                SetAttribute(nodeHierarchyItem, "editable", editableValue);
            }
        }

        public static void CopyAccessRights(XmlDocument xmlSource, ref XmlDocument xmlTarget)
        {

            var xmlNodeList = xmlTarget.SelectNodes("//item");
            foreach (XmlNode xmlNode in xmlNodeList)
            {

                var targetId = xmlNode.Attributes["id"].Value;
                var xmlNodeListSource = xmlSource.SelectNodes("//item[@id='" + targetId + "']/access_control");

                if (xmlNodeListSource.Count > 0)
                {
                    var xmlNodeToRemove = xmlNode.SelectSingleNode("access_control");
                    xmlNodeToRemove.ParentNode.RemoveChild(xmlNodeToRemove);

                    //Response.Write(xmlNodeListSource.Count.ToString()+" - "+targetId+"<br/>");
                    foreach (XmlNode xmlNodeNested in xmlNodeListSource)
                    {
                        var xmlNodeCloned = xmlTarget.ImportNode(xmlNodeNested, true);
                        xmlNode.AppendChild(xmlNodeCloned);
                    }
                }

            }

        }





    }

}