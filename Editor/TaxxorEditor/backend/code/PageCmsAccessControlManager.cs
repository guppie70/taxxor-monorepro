using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    /// <summary>
    /// CMS Administrator tools
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Rebuilds the configuration cache
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RenderAccessControlHierarchyOverview(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var hierarchyIdToShow = request.RetrievePostedValue("hierarchyid", @"^(\w{3,100}|\-)", true, ReturnTypeEnum.Html, "-");
            var simulatorUserId = request.RetrievePostedValue("simulatoruserid", RegexEnum.Email, false, ReturnTypeEnum.Html, null);

            // Process input data
            var outputChannelVariantId = RegExpReplace(@"^.*ocvariantid=(.*?)\:.*$", hierarchyIdToShow, "$1");
            var outputChannelType = RegExpReplace(@"^octype=(.*?)\:.*$", hierarchyIdToShow, "$1");
            var outputChannelVariantLanguage = RegExpReplace(@"^.*oclang=(.*)$", hierarchyIdToShow, "$1");

            if (debugRoutine)
            {
                Console.WriteLine("$$$$$$$$$$$");
                Console.WriteLine($"- outputChannelVariantId: {outputChannelVariantId}");
                Console.WriteLine($"- outputChannelType: {outputChannelType}");
                Console.WriteLine($"- outputChannelVariantLanguage: {outputChannelVariantLanguage}");
                Console.WriteLine($"- simulatorUserId: {simulatorUserId}");
                // Console.WriteLine($"- masterOutputChannelId: {masterOutputChannelId}");
                // Console.WriteLine($"- slaveHierarchyIds: {string.Join<string>(",", slaveOutputChannelIds)} => {slaveOutputChannelIds.Count.ToString()}");
                Console.WriteLine("$$$$$$$$$$$");
            }

            try
            {
                var contentLang = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, outputChannelVariantId);

                var xmlHierarchyDocument = projectVars.cmsMetaData[RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, outputChannelType, outputChannelVariantId, outputChannelVariantLanguage)].Xml;
                var xmlPermissionsHierarchy = CreateHierarchyForRbacPermissionsRetrieval(reqVars.xmlHierarchy, xmlHierarchyDocument);
                if (XmlContainsError(xmlPermissionsHierarchy))
                {

                    var json = (string)ConvertToJson(xmlPermissionsHierarchy);
                    await context.Response.OK(json, ReturnTypeEnum.Json, true);
                }
                else
                {

                    if (debugRoutine) await xmlPermissionsHierarchy.SaveAsync($"{logRootPathOs}/hierarchypermissions.xml", true, true);

                    //
                    // => Retrieve project name and set that dynamically in the project root node
                    //
                    var projectName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/name")?.InnerText ?? "unknown";
                    xmlPermissionsHierarchy.SelectSingleNode($"/items/structured//item[@id='cms_project-details']/web_page/linkname").InnerXml = $"{projectName} <small>(Taxxor DM project)</small>";

                    //
                    // => Retieve the output channel name and set it dynamically in the output channel root node
                    //
                    var nodeOutputChannelRoot = xmlPermissionsHierarchy.SelectSingleNode("/items/structured/item/sub_items/item/sub_items//item[@level='0']");
                    if (nodeOutputChannelRoot != null)
                    {
                        var editorId = (string.IsNullOrEmpty(projectVars.editorId)) ? RetrieveEditorIdFromProjectId(projectVars.projectId) : projectVars.editorId;
                        if (string.IsNullOrEmpty(editorId)) HandleError("Unable to retrieve editor ID");

                        string? outputChannelName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(outputChannelVariantId)}]/name")?.InnerText;
                        if (outputChannelName != null) nodeOutputChannelRoot.SelectSingleNode("web_page/linkname").InnerXml = $"{outputChannelName}<div class='tx-label'>Output channel hierarchy</div>";
                    }
                    else
                    {
                        appLogger.LogWarning($"Unable to set the outputchannel name dynamically in the outputchannel root node");
                    }

                    //
                    // => Retrieve all the access control resources that have been set for this project
                    //
                    var xmlResourcesFiltered = await AccessControlService.ListResources(projectVars.projectId, true);
                    if (XmlContainsError(xmlResourcesFiltered))
                    {
                        HandleError(reqVars, "Could not render the access control overview", $"error: {xmlResourcesFiltered.OuterXml}");
                    }

                    //
                    // => Mark the items in the hierarchy where explicit ACL settings were set
                    //
                    var splitString = new string[] { "__" };
                    var nodeListResources = xmlResourcesFiltered.SelectNodes("/resources/resource");
                    foreach (XmlNode nodeResource in nodeListResources)
                    {
                        var resourceId = nodeResource.GetAttribute("id");
                        var accessRecordCountString = nodeResource.GetAttribute("accessrecordcount");
                        var accessRecordCount = 0;
                        if (!Int32.TryParse(accessRecordCountString, out accessRecordCount))
                        {
                            appLogger.LogWarning($"Unable to parse accessrecordcount '{accessRecordCountString}' to an integer");
                        }
                        if (resourceId != null && accessRecordCount > 0)
                        {
                            var resourceIdElements = resourceId.Split(splitString, StringSplitOptions.None);
                            if (resourceIdElements.Length == 4)
                            {
                                var siteStructureId = resourceIdElements[2];
                                // appLogger.LogInformation($"- siteStructureId: {siteStructureId}");

                                var siteStructureItem = xmlPermissionsHierarchy.SelectSingleNode($"/items/structured//item[@id='{siteStructureId}']");
                                if (siteStructureItem != null)
                                {
                                    // appLogger.LogInformation("Setting marker");
                                    siteStructureItem.SetAttribute("aclrecord", "true");

                                    if (nodeResource.HasAttribute("reset-inheritance"))
                                    {
                                        if (nodeResource.GetAttribute("reset-inheritance") == "true") siteStructureItem.SetAttribute("reset-inheritance", "true");
                                    }
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"Unknown resourceId format - resourceIdElements.Length: {resourceIdElements.Length}");
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Could not find a resource ID");
                        }
                    }

                    //
                    // => Simulate a user
                    //
                    var userSimulatorMode = false;
                    if (!string.IsNullOrEmpty(simulatorUserId))
                    {
                        userSimulatorMode = true;
                        appLogger.LogInformation($"In user simulator mode for {simulatorUserId}");

                        // Retrieve a list of breadcrumbtrails
                        var dictItemIdsBreadcrumbTrails = new Dictionary<string, string>();
                        var nodeListAllItems = xmlPermissionsHierarchy.SelectNodes("/items//item");

                        foreach (XmlNode nodeItem in nodeListAllItems)
                        {
                            // Remove existing permissions node
                            var nodePermissionsToRemove = nodeItem.SelectSingleNode("permissions");
                            if (nodePermissionsToRemove != null) RemoveXmlNode(nodePermissionsToRemove);

                            if (!dictItemIdsBreadcrumbTrails.TryAdd(GetAttribute(nodeItem, "id"), GenerateRbacBreadcrumbTrail(RequestMethodEnum.Get, projectVars.projectId, nodeItem, true)))
                            {
                                appLogger.LogWarning($"Could not add {GetAttribute(nodeItem, "id")} to the dictItemIdsBreadcrumbTrails dictionary as it already exists...");
                            }
                        }

                        // Generate an array of the breadcrumbtrails that we have generated
                        var breadcrumbTrails = dictItemIdsBreadcrumbTrails.Select(x => x.Value);

                        // Retrieve the effective permissions from the Taxxor Access Control Service
                        string? logFileName = null;
                        if (debugRoutine) logFileName = $"RetrievePermissionsForResources_{outputChannelVariantId}-{simulatorUserId}.xml";
                        XmlDocument xmlPermissions = await AccessControlService.RetrievePermissionsForResources(string.Join(":", breadcrumbTrails), simulatorUserId, debugRoutine, logFileName);

                        if(XmlContainsError(xmlPermissions)){
                            HandleError(xmlPermissions);
                        }

                        // Decorate the hierarchy with the permissions that we have received
                        nodeListAllItems = xmlPermissionsHierarchy.SelectNodes("/items//item");
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
                                        var nodeRbacPermissionsImported = xmlPermissionsHierarchy.ImportNode(nodeRbacPermissions, true);
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

                                                        var setDeleteFlag = (specialPermissionPresent) ? false : true;

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
                    }

                    if (debugRoutine) await xmlPermissionsHierarchy.SaveAsync($"{logRootPathOs}/hierarchypermissions.decorated.xml", true, true);




                    /*
                    var html = "<pre>" + Server.HtmlEncode(PrettyPrintXml(xmlOutputChannelHierarchy.OuterXml)) + "</pre>";
                    Response.Write(html);
                    */

                    // Render the output
                    XsltArgumentList xsltArgumentList = new XsltArgumentList();


                    xsltArgumentList.AddParam("render-full-page", "", "no");
                    xsltArgumentList.AddParam("output-channel-reference", "", outputChannelVariantId);
                    xsltArgumentList.AddParam("mode", "", "acl-overview");
                    xsltArgumentList.AddParam("user-simulator", "", ((userSimulatorMode) ? "yes" : "no"));
                    xsltArgumentList.AddParam("hierarchy-type", "", "none");
                    xsltArgumentList.AddParam("contentlang", "", contentLang ?? projectVars.outputChannelDefaultLanguage);

                    var html = TransformXml(xmlPermissionsHierarchy, CalculateFullPathOs("cms_xsl_channels-to-list"), xsltArgumentList);




                    dynamic jsonData = new ExpandoObject();
                    jsonData.result = new ExpandoObject();

                    jsonData.result.message = "Successfully rendered ";
                    jsonData.result.payload = html;


                    var json = (string)ConvertToJson(jsonData);
                    await context.Response.OK(json, ReturnTypeEnum.Json, true);


                }

            }
            catch(HttpStatusCodeException){
                throw;
            }
            catch (Exception ex)
            {
                HandleError(reqVars, "Could not render the access control overview", $"error: {ex}");
            }

        }



    }
}