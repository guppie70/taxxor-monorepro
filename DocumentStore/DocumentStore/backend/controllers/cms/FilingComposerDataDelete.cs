using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities involved in saving editor data to the server
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Remove a filing data section from the Taxxor Data Store and potentially also from the outputchannel hierarchies
        /// </summary>
        public static async Task DeleteFilingComposerData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");
            var xpath = "";

            // Retrieve the data that we want to work with
            var dataType = request.RetrievePostedValue("type", true, ReturnTypeEnum.Xml);
            var deleteActionsData = request.RetrievePostedValue("xmldeleteactions", RegexEnum.None, true, reqVars.returnType);
            var dataFolderPathOs = RetrieveFilingDataFolderPathOs();

            //Console.WriteLine($"** Received Data: {deleteActionsData}");

            if (ValidateCmsPostedParameters(projectVars.projectId, projectVars.versionId, dataType))
            {

                try
                {
                    // Load the data that we have received
                    var xmlDeleteActionsData = new XmlDocument();
                    xmlDeleteActionsData.LoadXml(deleteActionsData);

                    var nodeListItemsToDelete = xmlDeleteActionsData.SelectNodes("/report/itemtodelete");
                    if (nodeListItemsToDelete.Count == 0) HandleError(ReturnTypeEnum.Xml, "Could not find any items to delete");

                    foreach (XmlNode nodeItemToDelete in nodeListItemsToDelete)
                    {
                        var dataRefToDelete = nodeItemToDelete.GetAttribute("data-ref");
                        if (string.IsNullOrEmpty(dataRefToDelete)) HandleError(ReturnTypeEnum.Xml, "Could not find data reference to delete", $"xmlDeleteActionsData: {xmlDeleteActionsData.OuterXml}, stack-trace: {GetStackTrace()}");

                        // 1) Delete the elements from the hierarchy
                        var nodeListHierarchyItemsToDelete = nodeItemToDelete.SelectNodes("references/reference");
                        if (nodeListHierarchyItemsToDelete.Count > 0)
                        {
                            foreach (XmlNode nodeReferenceItem in nodeListHierarchyItemsToDelete)
                            {
                                // Calculate path to the hierarchy file
                                var outputChannelVariantId = GetAttribute(nodeReferenceItem, "id");
                                var hierarchyType = GetAttribute(nodeReferenceItem, "type");
                                var hierarchyLang = GetAttribute(nodeReferenceItem, "lang");

                                xpath = $"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/variants/variant[@id='{outputChannelVariantId}']/@metadata-id-ref";
                                var hierarchyMetadataId = RetrieveAttributeValueIfExists(xpath, xmlApplicationConfiguration);
                                if (string.IsNullOrEmpty(hierarchyMetadataId)) HandleError(ReturnTypeEnum.Xml, "Could not find id of the hierarchy", $"xpath: {xpath}, stack-trace: {GetStackTrace()}");

                                xpath = $"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/metadata_system/metadata[@id='{hierarchyMetadataId}']/location";
                                var hierarchyLocationNode = xmlApplicationConfiguration.SelectSingleNode(xpath);
                                if (hierarchyLocationNode == null) HandleError(ReturnTypeEnum.Xml, "Could not find location node of the hierarchy", $"xpath: {xpath}, stack-trace: {GetStackTrace()}");

                                var hierarchyFilePathOs = CalculateFullPathOs(hierarchyLocationNode, reqVars);
                                // Console.WriteLine($"hierarchyFilePathOs: {hierarchyFilePathOs}");

                                var xmlOutputChannelHierarchy = new XmlDocument();
                                xmlOutputChannelHierarchy.Load(hierarchyFilePathOs);

                                xpath = $"//item[@data-ref='{dataRefToDelete}']";
                                var nodeOutputChannelHierarchyItemToDelete = xmlOutputChannelHierarchy.SelectSingleNode(xpath);
                                if (nodeOutputChannelHierarchyItemToDelete == null) HandleError(ReturnTypeEnum.Xml, "Could not find node in hierarchy to delete", $"xpath: {xpath}, stack-trace: {GetStackTrace()}");

                                // Retrieve information about the item we are about to delete
                                var linkName = RetrieveNodeValueIfExists("web_page/linkname", nodeOutputChannelHierarchyItemToDelete);
                                var hierarchyId = GetAttribute(nodeOutputChannelHierarchyItemToDelete, "id");

                                RemoveXmlNode(nodeOutputChannelHierarchyItemToDelete);

                                var saved = await SaveXmlDocument(xmlOutputChannelHierarchy, hierarchyFilePathOs);
                                if (!saved)
                                {
                                    HandleError(ReturnTypeEnum.Xml, "Could not store the hierarchy", $"hierarchyFilePathOs: {hierarchyFilePathOs}, stack-trace: {GetStackTrace()}");
                                }
                                else
                                {
                                    // Commit the change in GIT
                                    var sectionTitle = linkName ?? "[unknown]";
                                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "d"; // Delete
                                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = sectionTitle; // Linkname
                                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = hierarchyId; // Section ID
                                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                                    // Commit the result in the GIT repository
                                    CommitFilingData(message, ReturnTypeEnum.Xml, true);
                                }

                            }

                        }

                        // 2) Handle links
                        // TODO: Handle updating of links and linked elements

                        // 3) Remove the section data itself
                        var sectionFilePathOs = $"{dataFolderPathOs}/{dataRefToDelete}";
                        try
                        {
                            var xmlFilingData = new XmlDocument();
                            xmlFilingData.Load(sectionFilePathOs);
                            var nodeSectionTitle = xmlFilingData.SelectSingleNode("/data/content/*//h1");
                            var sectionTitle = "[unknown]";
                            if (nodeSectionTitle != null) sectionTitle = nodeSectionTitle.InnerText;

                            File.Delete(sectionFilePathOs);

                            // Remove the entry from the global metadata object
                            await RemoveCmsMetadataEntry(projectVars.projectId, Path.GetFileName(sectionFilePathOs));

                            // Commit the change in GIT
                            XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                            xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "d"; // Delete
                            xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = sectionTitle; // Linkname
                            xmlCommitMessage.SelectSingleNode("/root/id").InnerText = dataRefToDelete; // Section ID
                            var message = xmlCommitMessage.DocumentElement.InnerXml;

                            // Commit the result in the GIT repository
                            CommitFilingData(message, ReturnTypeEnum.Xml, false);

                        }
                        catch (Exception ex)
                        {
                            HandleError(ReturnTypeEnum.Xml, "Could not remove section data xml", $"error: {ex}, stack-trace: {GetStackTrace()}");

                        }
                    }



                    // Update the CMS metadata content
                    await UpdateCmsMetadata(projectVars.projectId);

                    // Render the overview and return it
                    await RetrieveFilingDataOverview(request, response, routeData);
                }
                // To make sure HandleError is correctly executed
                catch (HttpStatusCodeException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    HandleError(ReturnTypeEnum.Xml, "There was an error deleting filing composer data", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

            }
            else
            {
                HandleError(ReturnTypeEnum.Json, GenerateErrorXml("Could not validate posted data", $"projectId: {projectVars.projectId}, versionId: {projectVars.versionId}, dataType: {dataType}, stack-trace: {GetStackTrace()}"));
            }
        }

    }
}