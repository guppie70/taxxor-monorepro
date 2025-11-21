using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities involved in loading hierarchy data for the filing composer
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Routine that retrieves the hierarchy that is used in the editor
        /// </summary>
        public static async Task RetrieveFilingHierarchy(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted variables
            var projectId = context.Request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var versionId = context.Request.RetrievePostedValue("vid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var editorId = context.Request.RetrievePostedValue("editorid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var outputChannelType = context.Request.RetrievePostedValue("octype", true, ReturnTypeEnum.Xml);
            var outputChannelVariantId = context.Request.RetrievePostedValue("ocvariantid", true, ReturnTypeEnum.Xml);
            var outputChannelVariantLanguage = context.Request.RetrievePostedValue("oclang", false, ReturnTypeEnum.Xml);
            projectVars.editorContentType = context.Request.RetrievePostedValue("ctype", true, ReturnTypeEnum.Xml);
            projectVars.reportTypeId = context.Request.RetrievePostedValue("rtype", true, ReturnTypeEnum.Xml);


            // appLogger.LogWarning($"** Protocol used: {request.Protocol} (RetrieveFilingHierarchy) **");
            // Console.WriteLine($"** Protocol used: {request.Protocol} (RetrieveFilingHierarchy) **");

            var baseDebugInfo = $"projectId: '{projectId}', " +
                $"editorId: '{editorId}', " +
                $"versionId: '{versionId}', " +
                $"outputChannelType: '{outputChannelType}', " +
                $"outputChannelVariantId: '{outputChannelVariantId}', " +
                $"outputChannelVariantLanguage: '{outputChannelVariantLanguage}', " +
                $"projectVars.editorContentType: '{projectVars.editorContentType}', " +
                $"projectVars.reportTypeId: '{projectVars.reportTypeId}'";

            var xmlFilingHierarcy = new XmlDocument();

            // Determine the way that we will generate the filing hierarchy
            // editorId = philips-qr-report_filing
            switch (editorId)
            {
                default:
                    xmlFilingHierarcy = RenderFilingHierarchy(reqVars, projectId, versionId, editorId, projectVars.editorContentType, projectVars.reportTypeId, outputChannelType, outputChannelVariantId, outputChannelVariantLanguage);
                    break;
            }

            if (XmlContainsError(xmlFilingHierarcy))
            {
                await response.Error(xmlFilingHierarcy, ReturnTypeEnum.Xml, true);
            }
            else
            {
                // Stick the file content in the message field of the success xml and the file path into the debuginfo node
                await response.OK(GenerateSuccessXml(HttpUtility.HtmlEncode(xmlFilingHierarcy.OuterXml), baseDebugInfo), ReturnTypeEnum.Xml, true);
            }
        }

        /// <summary>
        /// Removes input most validation from the request to test if that improves the performance
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RetrieveFilingHierarchyImproved(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            appLogger.LogWarning($"** Protocol used: {request.Protocol} (RetrieveFilingHierarchyImproved) **");
            // Console.WriteLine($"** Protocol used: {request.Protocol} (RetrieveFilingHierarchyImproved) **");

            var baseDebugInfo = $"projectVars.projectId: '{projectVars.projectId}', " +
                $"projectVars.editorId: '{projectVars.editorId}', " +
                $"projectVars.versionId: '{projectVars.versionId}', " +
                $"projectVars.outputChannelType: '{projectVars.outputChannelType}', " +
                $"projectVars.outputChannelVariantId: '{projectVars.outputChannelVariantId}', " +
                $"projectVars.outputChannelVariantLanguage: '{projectVars.outputChannelVariantLanguage}', " +
                $"projectVars.editorContentType: '{projectVars.editorContentType}', " +
                $"projectVars.reportTypeId: '{projectVars.reportTypeId}'";

            XmlDocument? xmlFilingHierarcy = null;

            // Determine the way that we will generate the filing hierarchy
            // editorId = philips-qr-report_filing
            switch (projectVars.editorId)
            {
                default:
                    xmlFilingHierarcy = RenderFilingHierarchy(reqVars, projectVars);
                    break;
            }

            if (XmlContainsError(xmlFilingHierarcy))
            {
                await response.Error(xmlFilingHierarcy, ReturnTypeEnum.Xml, true);
            }
            else
            {
                // Stick the file content in the message field of the success xml and the file path into the debuginfo node
                await response.OK(GenerateSuccessXml(HttpUtility.HtmlEncode(xmlFilingHierarcy.OuterXml), baseDebugInfo), ReturnTypeEnum.Xml, true);
            }
        }

        /// <summary>
        /// Retrieves the filing hierarchy from the local repository and returns it
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="projectId">Project identifier.</param>
        /// <param name="versionId">Version identifier.</param>
        /// <param name="appendArticleIds">Indicate if we need to include the article ID's of the articles found in the section XML data</param>
        /// <returns></returns>
        public static XmlDocument RenderFilingHierarchy(RequestVariables reqVars, ProjectVariables projectVars, string? projectId = null, string? versionId = null, bool appendArticleIds = false)
        {
            if (projectId == null) projectId = projectVars.projectId;
            if (versionId == null) versionId = projectVars.versionId;

            return RenderFilingHierarchy(reqVars, projectId, versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage, appendArticleIds);
        }


        /// <summary>
        /// Retrieves the filing hierarchy from the local repository and returns it
        /// </summary>
        /// <returns>The filing hierarchy.</returns>
        /// <param name="reqVars">Req variables.</param>
        /// <param name="projectId">Project identifier.</param>
        /// <param name="versionId">Version identifier.</param>
        /// <param name="editorId">Editor identifier.</param>
        /// <param name="editorContentType">Editor content type.</param>
        /// <param name="reportTypeId">Report type identifier.</param>
        /// <param name="outputChannelVariantId">Output channel variant identifier.</param>
        /// <param name="outputChannelVariantLanguage">Output channel variant language.</param>
        /// <param name="appendArticleIds">Indicate if we need to include the article ID's of the articles found in the section XML data</param>
        public static XmlDocument RenderFilingHierarchy(RequestVariables reqVars, string? projectId, string? versionId, string? editorId, string? editorContentType, string? reportTypeId, string? outputChannelType, string? outputChannelVariantId, string? outputChannelVariantLanguage, bool appendArticleIds = false)
        {
            var baseDebugInfo = $"projectId: '{projectId}', " +
                $"versionId: '{versionId}', " +
                $"editorId: '{editorId}', " +
                $"outputChannelType: '{outputChannelType}', " +
                $"outputChannelVariantId: '{outputChannelVariantId}', " +
                $"outputChannelVariantLanguage: '{outputChannelVariantLanguage}', " +
                $"editorContentType: '{editorContentType}', " +
                $"reportTypeId: '{reportTypeId}'";

            var hierarchyPathOs = CalculateHierarchyPathOs(reqVars, projectId, versionId, editorId, editorContentType, reportTypeId, outputChannelType, outputChannelVariantId, outputChannelVariantLanguage);

            var xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.Load(hierarchyPathOs);

                // Extend the hierarchy with the id's of the sections from the cache
                if (appendArticleIds)
                {
                    ExtendHierarchyWithArticleIds(ref xmlDoc, projectId);
                }

            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Failed to load hierarchy");
                xmlDoc = RetrieveTemplate("error_xml");
                xmlDoc.SelectSingleNode("//message").InnerText = "Failed to load hierarchy";
                xmlDoc.SelectSingleNode("//debuginfo").InnerText = $"error: {ex}, {baseDebugInfo}";
            }

            return xmlDoc;
        }

        /// <summary>
        /// Extends a site-structure XML document with the article id's used in the content
        /// </summary>
        /// <param name="xmlHierarchy"></param>
        /// <param name="projectId"></param>
        public static void ExtendHierarchyWithArticleIds(ref XmlDocument xmlHierarchy, string projectId)
        {
            if (XmlCmsContentMetadata != null && XmlCmsContentMetadata.DocumentElement != null)
            {
                var nodeProjectContentMetadata = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']/data");

                // If for some reason the project content metadata node is null, then we try to compile it for this project
                if (nodeProjectContentMetadata == null)
                {
                    var updateResult = _UpdateCmsMetadata(projectId).GetAwaiter().GetResult();
                    if (!updateResult.Success)
                    {
                        appLogger.LogError($"Failed to update CMS metadata for project {projectId}. error: {updateResult.ToString()}");
                    }
                    else
                    {
                        nodeProjectContentMetadata = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']/data");
                    }
                }


                if (nodeProjectContentMetadata != null)
                {
                    var nodeListItems = xmlHierarchy.SelectNodes("/items/structured//item");

                    foreach (XmlNode nodeItem in nodeListItems)
                    {
                        var dataReference = nodeItem.GetAttribute("data-ref");
                        if (!string.IsNullOrEmpty(dataReference))
                        {
                            var nodeDataReferenceMetadataArticleIds = nodeProjectContentMetadata.SelectSingleNode($"content[@ref='{dataReference}']/metadata/entry[@key='ids']");
                            if (nodeDataReferenceMetadataArticleIds != null)
                            {
                                nodeItem.SetAttribute("data-articleids", nodeDataReferenceMetadataArticleIds.InnerText);
                            }
                        }
                    }
                }
                else
                {
                    appLogger.LogWarning($"Could not extend hierarchy with article ID's. Unable to find project ID '{projectId}' in XmlCmsMetadata.");
                }
            }
            else
            {
                appLogger.LogWarning($"Could not extend hierarchy with article ID's. XmlCmsMetadata not available (yet).");
            }
        }

        /// <summary>
        /// Calculates the path to a site-structure XML file
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static string? CalculateHierarchyPathOs(RequestVariables reqVars, ProjectVariables projectVars)
        {
            return CalculateHierarchyPathOs(reqVars, projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage, projectVars);
        }

        /// <summary>
        /// Calculates the path to a site-structure XML file
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="editorId"></param>
        /// <param name="editorContentType"></param>
        /// <param name="reportTypeId"></param>
        /// <param name="outputChannelType"></param>
        /// <param name="outputChannelVariantId"></param>
        /// <param name="outputChannelVariantLanguage"></param>
        /// <returns></returns>
        public static string? CalculateHierarchyPathOs(RequestVariables reqVars, string? projectId, string? versionId, string? editorId, string? editorContentType, string? reportTypeId, string? outputChannelType, string? outputChannelVariantId, string? outputChannelVariantLanguage, ProjectVariables? projectVars = null)
        {
            var baseDebugInfo = $"projectId: '{projectId}', " +
                $"versionId: '{versionId}', " +
                $"editorId: '{editorId}', " +
                $"outputChannelType: '{outputChannelType}', " +
                $"outputChannelVariantId: '{outputChannelVariantId}', " +
                $"outputChannelVariantLanguage: '{outputChannelVariantLanguage}', " +
                $"editorContentType: '{editorContentType}', " +
                $"reportTypeId: '{reportTypeId}'";

            // Retrieve the information for loading the hierarchy XML file
            var hierarchyMetadataId = RetrieveOutputChannelHierarchyMetadataId(editorId, outputChannelType, outputChannelVariantId, outputChannelVariantLanguage);
            if (string.IsNullOrEmpty(hierarchyMetadataId)) HandleError("Could not find metadata id for hierarchy", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");

            return CalculateHierarchyPathOs(projectId, hierarchyMetadataId, reqVars, baseDebugInfo, projectVars);
        }


        /// <summary>
        /// Calculates the path to a site-structure XML file
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectId"></param>
        /// <param name="hierarchyMetadataId"></param>
        /// <param name="baseDebugInfo"></param>
        /// <returns></returns>
        public static string? CalculateHierarchyPathOs(string projectId, string hierarchyMetadataId, RequestVariables? reqVars = null, string? baseDebugInfo = null, ProjectVariables? projectVars = null)
        {
            return CalculateHierarchyPathOs(projectId, hierarchyMetadataId, true, reqVars, baseDebugInfo, projectVars);
        }


        /// <summary>
        /// Calculates the path to a site-structure XML file
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="hierarchyMetadataId"></param>
        /// <param name="stopOnError"></param>
        /// <param name="reqVars"></param>
        /// <param name="baseDebugInfo"></param>
        /// <returns></returns>
        public static string? CalculateHierarchyPathOs(string projectId, string hierarchyMetadataId, bool stopOnError, RequestVariables? reqVars = null, string? baseDebugInfo = null, ProjectVariables? projectVars = null)
        {
            var xpath = $"/configuration/cms_projects/cms_project[@id='{projectId}']/metadata_system/metadata[@id='{hierarchyMetadataId}']/location";
            // Console.WriteLine("****");
            // Console.WriteLine(xpath);
            // Console.WriteLine("****\n");
            var nodeHierarchyLocation = xmlApplicationConfiguration.SelectSingleNode(xpath);
            var debugInfo = string.IsNullOrEmpty(baseDebugInfo) ? "" : baseDebugInfo + ", ";
            if (nodeHierarchyLocation == null)
            {
                if (stopOnError)
                {
                    HandleError("Could not find location node for hierarchy", $"xpath: {xpath}, {debugInfo}stack-trace: {GetStackTrace()}");
                }
                else
                {
                    appLogger.LogWarning($"Could not find location node for hierarchy. xpath: {xpath}, {debugInfo}");
                    return null;
                }
            }

            if (nodeHierarchyLocation.GetAttribute("path-type") == "cmsdataroot")
            {
                return RetrieveFilingDataRootFolderPathOs(projectId) + nodeHierarchyLocation.InnerText;
            }
            else
            {
                return CalculateFullPathOs(nodeHierarchyLocation, reqVars, projectVars);
            }
        }



        /// <summary>
        /// Saves a filing hierarchy
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

            var debugRoutine = (siteType == "local" || siteType == "dev");
            var baseXPath = $"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel[@type='{projectVars.outputChannelType}']";

            try
            {
                // Hierarchy type master | slave | none
                var hierarchyType = "none";
                var masterOutputChannelId = "";
                var masterHierarchyId = "";
                var slaveOutputChannelIds = new List<string>();
                var slaveHierarchyIds = new List<string>();
                string outputChannelName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels/output_channel/variants/variant[@id={GenerateEscapedXPathString(projectVars.outputChannelVariantId)}]/name")?.InnerText ?? "unknown";

                // Test if we are saving a slave hierarchy
                var nodesOutputVariant = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{projectVars.outputChannelVariantId}' and @slave-of]");
                if (nodesOutputVariant != null)
                {
                    hierarchyType = "slave";
                    masterOutputChannelId = nodesOutputVariant.GetAttribute("slave-of");
                    masterHierarchyId = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{masterOutputChannelId}']")?.GetAttribute("metadata-id-ref") ?? "unknown";
                    var nodeListSlaveVariants = xmlApplicationConfiguration.SelectNodes($"{baseXPath}/variants/variant[@slave-of='{masterOutputChannelId}']");
                    foreach (XmlNode nodeSlave in nodeListSlaveVariants)
                    {
                        slaveOutputChannelIds.Add(GetAttribute(nodeSlave, "id"));
                        slaveHierarchyIds.Add(nodeSlave.GetAttribute("metadata-id-ref") ?? "unknown");
                    }
                }
                else
                {
                    // Check if we are saving a master hierarchy
                    var nodeListSlaveVariants = xmlApplicationConfiguration.SelectNodes($"{baseXPath}/variants/variant[@slave-of]");
                    foreach (XmlNode nodeSlaveVrnt in nodeListSlaveVariants)
                    {
                        masterOutputChannelId = nodeSlaveVrnt.GetAttribute("slave-of");
                        if (masterOutputChannelId == projectVars.outputChannelVariantId)
                        {
                            hierarchyType = "master";
                            masterHierarchyId = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{masterOutputChannelId}']")?.GetAttribute("metadata-id-ref") ?? "unknown";
                            var nodeListSlaves = xmlApplicationConfiguration.SelectNodes($"{baseXPath}/variants/variant[@slave-of='{masterOutputChannelId}']");
                            foreach (XmlNode nodeSlave in nodeListSlaves)
                            {
                                var variantId = GetAttribute(nodeSlave, "id");
                                var hierarchyId = nodeSlave.GetAttribute("metadata-id-ref") ?? "unknown";
                                if (!slaveOutputChannelIds.Contains(variantId)) slaveOutputChannelIds.Add(variantId);
                                if (!slaveHierarchyIds.Contains(hierarchyId)) slaveHierarchyIds.Add(hierarchyId);
                            }
                        }
                    }
                }

                // Test if we are dealing with linked hierarchies
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
                    }
                }


                if (debugRoutine)
                {
                    Console.WriteLine("$$$$$$$$$$$");
                    Console.WriteLine($"- hierarchyType: {hierarchyType}");
                    Console.WriteLine($"- masterOutputChannelId: {masterOutputChannelId}");
                    Console.WriteLine($"- slaveHierarchyIds: {string.Join<string>(",", slaveOutputChannelIds)} => {slaveOutputChannelIds.Count.ToString()}");
                    Console.WriteLine("$$$$$$$$$$$");
                }

                // Check if we need to commit the changes into the version control system
                var commitChangesString = request.RetrievePostedValue("commitchanges", RegexEnum.None, true, reqVars.returnType);
                var commitChanges = (commitChangesString == "true");

                // Retrieve the XML content that we need to save
                var hierarchyContent = request.RetrievePostedValue("hierarchy", RegexEnum.None, true, reqVars.returnType);

                // Create a new XmlDocument object and load the xml string
                var xmlHierarchyToSave = new XmlDocument();
                xmlHierarchyToSave.LoadXml(hierarchyContent);

                if (debugRoutine) xmlHierarchyToSave.Save(logRootPathOs + "/rawhierarchytosave.xml");

                // Correct optionally strange combinations of hierarchy item attributes
                var nodeListItemStyleEnd = xmlHierarchyToSave.SelectNodes($"//item[@data-tocend='true' and @data-tocstyle]");
                foreach (XmlNode nodeItemStyleEnd in nodeListItemStyleEnd)
                {
                    nodeItemStyleEnd.RemoveAttribute("data-tocstyle");
                }

                // Add section numbers to the hierarchy items
                if (debugRoutine) xmlHierarchyToSave.Save(logRootPathOs + "/strippedhierarchytosave.xml");

                int firstHierarchicalLevelForNumbering = 1;
                var nodeFirstHierarchicalLevelForNumbering = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='toc_first-hierarchical-level']");
                if (
                    nodeFirstHierarchicalLevelForNumbering != null &&
                    !int.TryParse(nodeFirstHierarchicalLevelForNumbering.InnerText, out firstHierarchicalLevelForNumbering)
                )
                {
                    appLogger.LogWarning($"Unable to parse 'toc_first-hierarchical-level' value: {nodeFirstHierarchicalLevelForNumbering.InnerText} to an integer");
                }

                XsltArgumentList xslParams = new();
                xslParams.AddParam("first-hierarchical-level", "", firstHierarchicalLevelForNumbering);
                xmlHierarchyToSave.LoadXml(TransformXml(xmlHierarchyToSave, "cms_hierarchy-add-sectionnumbers", xslParams));

                // Save the file
                var xmlPathOs = CalculateHierarchyPathOs(reqVars, projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                var saved = await SaveXmlDocument(xmlHierarchyToSave, xmlPathOs);
                if (!saved) HandleError("Could not save output channel hierarchy", $"xmlPathOs: {xmlPathOs}, stack-trace: {GetStackTrace()}");

                // => When we save the master hierarchy, then use that as a template for the slave hierarchies
                if (hierarchyType == "master")
                {
                    // - Throw an error if we are dealing with an annual report project as this is much more complicated to sync properly
                    // TODO: Sync hierarchies for complex reports such as Annual Report
                    if (projectVars.editorId.Contains("annual")) HandleError("Cannot save master hierarchy", "No save routine has been implemented to sync the slave hierarchy with the master hierarchy yet!!");

                    // - Adjust the slave hierarchies (if any)
                    foreach (string slaveOutputChannelId in slaveOutputChannelIds)
                    {
                        // - Load the slave hierarchy file
                        var nodeVariant = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{slaveOutputChannelId}']");
                        var metadataReferenceId = GetAttribute(nodeVariant, "metadata-id-ref");


                        var slaveHierarchyPathOs = CalculateHierarchyPathOs(projectVars.projectId, metadataReferenceId);
                        if (string.IsNullOrEmpty(slaveHierarchyPathOs))
                        {
                            appLogger.LogWarning($"Could not calculate path to hierarchy. projectVars.projectId: {projectVars.projectId}, metadataReferenceId: {metadataReferenceId}, stack-trace: {GetStackTrace()}");
                        }
                        else
                        {
                            if (File.Exists(slaveHierarchyPathOs))
                            {
                                XmlDocument xmlHierarchySlave = new XmlDocument();
                                xmlHierarchySlave.Load(slaveHierarchyPathOs);

                                XmlDocument xmlHierarchyNewSlave = new XmlDocument();

                                // - Insert the complete master hierarchy in the new slave
                                xmlHierarchyNewSlave.LoadXml(xmlHierarchyToSave.OuterXml);

                                // - Select reference nodes in the new slave hierarchy file
                                var nodeFirstNewSlaveLevel1 = xmlHierarchyNewSlave.SelectSingleNode("/items/structured/item/sub_items/item[1]");
                                var nodeSubItemsNewSlave = xmlHierarchyNewSlave.SelectSingleNode("/items/structured/item/sub_items");

                                // - Loop through the original level 1 slave hierarchy items and find the items that do not exist in the master hierarchy, so that we can insert these again
                                var nodeListLevel1SlaveHierarchyItems = xmlHierarchySlave.SelectNodes("/items/structured/item/sub_items/item");
                                foreach (XmlNode nodeLevel1SlaveItem in nodeListLevel1SlaveHierarchyItems)
                                {
                                    var dataReference = GetAttribute(nodeLevel1SlaveItem, "data-ref");
                                    if (!string.IsNullOrEmpty(dataReference))
                                    {
                                        var nodeItemMaster = xmlHierarchyNewSlave.SelectSingleNode($"/items/structured/item/sub_items/item[@data-ref='{dataReference}']");
                                        if (nodeItemMaster == null)
                                        {
                                            Console.WriteLine($"!!!! Node with data-ref {dataReference} exists in the original slave hierarchy, but not in the master hierarchy !!!!");

                                            // TODO: This needs to become much more specific!!!
                                            var nodeItemImported = xmlHierarchyNewSlave.ImportNode(nodeLevel1SlaveItem, true);

                                            // - Insert the item at the beginning of the sub-items list in the new slave hierarchy
                                            nodeSubItemsNewSlave.InsertBefore(nodeItemImported, nodeFirstNewSlaveLevel1);
                                        }
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Data reference could not be found in the slave hierarchy item");
                                    }
                                }

                                // - Correct the id's of the items so that they match the original id's from the slave hierarchy
                                var nodeListItems = xmlHierarchySlave.SelectNodes("/items//item");
                                var itemCounter = 0;
                                foreach (XmlNode nodeSlaveItem in nodeListItems)
                                {
                                    if (itemCounter == 0)
                                    {
                                        // This is the root node, copy all the item attributes
                                        var rootNodeSlave = xmlHierarchyNewSlave.SelectSingleNode($"/items/structured/item");
                                        if (rootNodeSlave != null)
                                        {
                                            // - id
                                            var originalRootId = GetAttribute(nodeSlaveItem, "id");
                                            if (!string.IsNullOrEmpty(originalRootId))
                                            {
                                                SetAttribute(rootNodeSlave, "id", originalRootId);
                                            }
                                            else
                                            {
                                                appLogger.LogWarning($"Could not restore the ID attribute for the root node. stack-trace: {GetStackTrace()}");
                                            }

                                            // - data ref
                                            var originalDataReference = GetAttribute(nodeSlaveItem, "data-ref");
                                            if (!string.IsNullOrEmpty(originalRootId))
                                            {
                                                SetAttribute(rootNodeSlave, "data-ref", originalDataReference);
                                            }
                                            else
                                            {
                                                appLogger.LogWarning($"Could not restore the data-ref attribute for the root node. stack-trace: {GetStackTrace()}");
                                            }

                                            // - linkname
                                            var originalLinkName = nodeSlaveItem.SelectSingleNode("web_page/linkname")?.InnerText ?? "";
                                            if (!string.IsNullOrEmpty(originalLinkName))
                                            {
                                                rootNodeSlave.SelectSingleNode("web_page/linkname").InnerText = originalLinkName;
                                            }
                                            else
                                            {
                                                appLogger.LogWarning($"Could not restore the linkname for the root node. stack-trace: {GetStackTrace()}");
                                            }
                                        }
                                        else
                                        {
                                            appLogger.LogWarning($"Could not find the root node in the slave document. stack-trace: {GetStackTrace()}");
                                        }
                                    }

                                    var dataReference = GetAttribute(nodeSlaveItem, "data-ref");
                                    var originalSlaveItemId = GetAttribute(nodeSlaveItem, "id");
                                    if (!string.IsNullOrEmpty(dataReference))
                                    {
                                        var nodeNewSlaveItem = xmlHierarchyNewSlave.SelectSingleNode($"/items//item[@data-ref='{dataReference}']");
                                        if (nodeNewSlaveItem != null)
                                        {
                                            SetAttribute(nodeNewSlaveItem, "id", originalSlaveItemId);
                                        }
                                        else
                                        {
                                            appLogger.LogWarning($"Could not locate the hierarchy item with data-ref '{dataReference}' in the new slave document. stack-trace: {GetStackTrace()}");
                                        }
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Data reference could not be found in the slave hierarchy item");
                                    }
                                    itemCounter++;
                                }

                                //xmlHierarchyNewSlave.Save(logRootPathOs + "/______slavehierarchy.xml");

                                xmlHierarchyNewSlave.Save(slaveHierarchyPathOs);
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to load the hierarchy from path {slaveHierarchyPathOs}, stack-trace: {GetStackTrace()}");
                            }
                        }
                    }
                }

                if (commitChanges)
                {
                    // Commit the change
                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "u"; // Update
                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = $"{outputChannelName} {projectVars.outputChannelType} hierarchy ({projectVars.outputChannelVariantId})"; // Linkname
                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = projectVars.outputChannelVariantId; // Page ID
                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                    // Commit the result in the GIT repository
                    CommitFilingData(message, ReturnTypeEnum.Xml, false);
                }

                // Update the CMS metadata content
                await UpdateCmsMetadata(projectVars.projectId);

                // Render a response
                await response.OK(GenerateSuccessXml("Successfully saved hierarchy", $"xmlPathOs: {xmlPathOs}"), ReturnTypeEnum.Xml, true);
            }
            // To make sure HandleError is correctly executed
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was an error while saving the hierarchy");
                HandleError("Error while storing the hierarchy", $"error: {ex},stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Clones the language of a section to another language
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

            var sourceLang = context.Request.RetrievePostedValue("sourcelang", RegexEnum.None, true);
            var targetLang = context.Request.RetrievePostedValue("targetlang", RegexEnum.None, true);
            var includeChildrenString = request.RetrievePostedValue("includechildren", RegexEnum.None, true);
            var includeChildren = (includeChildrenString == "true");


            // Map did (sitestructure id) to data-ref in order to calculate the correct path for the XML file that we need to retrieve
            var xmlFilingHierarchyPathOs = CalculateHierarchyPathOs(reqVars, projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
            var xmlFilingHierarchy = new XmlDocument();
            xmlFilingHierarchy.Load(xmlFilingHierarchyPathOs);

            var basicDebugInfo = $"sourceLang: {sourceLang}, targetLang: {targetLang}, projectId: {projectVars.projectId}, did: {projectVars.did}, includeChildren: {includeChildrenString}";

            //
            // => Select the references that we need to work with
            //
            var dataReferences = new List<string>();
            var subItemSelector = (includeChildren) ? $"ancestor-or-self::item/@id={GenerateEscapedXPathString(projectVars.did)}" : $"@id={GenerateEscapedXPathString(projectVars.did)}";

            var xPath = $"/items/structured//item[{subItemSelector}]";
            appLogger.LogDebug($"- xPath: {xPath}");
            var nodeListItems = xmlFilingHierarchy.SelectNodes(xPath);
            if (nodeListItems.Count == 0)
            {
                HandleError("Could not locate filing hierarchy items to clone the language for", $"xPath: {xPath}, {basicDebugInfo}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                appLogger.LogDebug($"- nodeListItems.Count: {nodeListItems.Count}");
            }

            foreach (XmlNode nodeItem in nodeListItems)
            {
                var dataReference = GetAttribute(nodeItem, "data-ref");
                if (string.IsNullOrEmpty(dataReference)) HandleError("Could not locate the data reference from the filing hierarchy item", $"{basicDebugInfo}, stack-trace: {GetStackTrace()}");
                if (!dataReferences.Contains(dataReference)) dataReferences.Add(dataReference);
            }



            //
            // => Perform the language clone
            //
            foreach (var dataReference in dataReferences)
            {

                // Calculate the base path to the xml file that contains the section XML file
                var dataFilePathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectVars.projectId) + "/" + dataReference;

                if (!File.Exists(dataFilePathOs))
                {
                    HandleError("Could not find the section data content", $"dataFilePathOs: {dataFilePathOs}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    var xmlSection = new XmlDocument();
                    try
                    {
                        xmlSection.Load(dataFilePathOs);
                        var xpath = $"/data/content[@lang='{sourceLang}']/article";
                        var nodeSourceArticle = xmlSection.SelectSingleNode(xpath);
                        if (nodeSourceArticle == null)
                        {
                            HandleError("Could not find source content data", $"xpath: {xpath}, dataFilePathOs: {dataFilePathOs}, stack-trace: {GetStackTrace()}");
                        }
                        var nodeTargetContent = xmlSection.SelectSingleNode($"/data/content[@lang='{targetLang}']");
                        if (nodeTargetContent == null)
                        {
                            nodeTargetContent = xmlSection.CreateElement("content");
                            SetAttribute(nodeTargetContent, "lang", targetLang);
                            var nodeData = xmlSection.SelectSingleNode("/data");
                            if (nodeData == null)
                            {
                                HandleError("Could not retrieve data node", $"dataFilePathOs: {dataFilePathOs}, stack-trace: {GetStackTrace()}");
                            }
                            else
                            {
                                nodeTargetContent = nodeData.AppendChild(nodeTargetContent);
                            }
                        }

                        // Custom post-process logic
                        nodeSourceArticle = CustomFPostProcessLangClone(nodeSourceArticle, projectVars);

                        nodeTargetContent.InnerXml = nodeSourceArticle.OuterXml;

                        xmlSection.Save(dataFilePathOs);

                        var linkname = nodeSourceArticle.SelectSingleNode("//*[local-name()='h1' or local-name()='h2' or local-name()='h3']")?.InnerText ?? Path.GetFileName(dataReference);

                        XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                        xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "clonelanguage"; // Update
                        xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = ((string.IsNullOrEmpty(linkname)) ? "" : linkname); // Linkname
                        xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", targetLang);
                        xmlCommitMessage.SelectSingleNode("/root/id").InnerText = projectVars.did; // Page ID
                        var message = xmlCommitMessage.DocumentElement.InnerXml;

                        // Commit the result in the GIT repository
                        var commitSuccess = CommitFilingData(message, ReturnTypeEnum.Xml, false);
                    }
                    catch (Exception ex)
                    {
                        HandleError("There was an error cloning the language data", $"dataref: {Path.GetFileName(dataReference)}, error: {ex}");
                    }

                }
            }


            await response.OK(GenerateSuccessXml($"Successfully cloned source lang {sourceLang} to target lang {targetLang} of {dataReferences.Count} datareferences", ""));
        }

    }


}