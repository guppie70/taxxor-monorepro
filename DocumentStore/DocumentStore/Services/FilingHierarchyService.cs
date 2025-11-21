using AutoMapper;
using System.Threading.Tasks;
using Grpc.Core;
using DocumentStore.Protos;
using Taxxor.Project;
using System.Xml;
using Microsoft.AspNetCore.Http;
using System.Web;
using static Framework;
using static Taxxor.Project.ProjectLogic;
using Microsoft.Extensions.Logging;
using System;

namespace DocumentStore.Services
{

    public class FilingHierarchyService : Protos.FilingHierarchyService.FilingHierarchyServiceBase
    {

        private readonly RequestContext _requestContext;
        private readonly IMapper _mapper;

        public FilingHierarchyService(RequestContext requestContext, IMapper mapper)
        {
            _requestContext = requestContext;
            _mapper = mapper;
        }

        /// <summary>
        /// Loads filing hierarchy from DocumentStore
        /// </summary>
        public override async Task<TaxxorGrpcResponseMessage> LoadHierarchy(
            LoadHierarchyRequest request, ServerCallContext context)
        {
            var reqVars = _requestContext.RequestVariables;
            var baseDebugInfo = "";

            // To please the compiler
            await DummyAwaiter();

            try
            {
                // Map gRPC request to ProjectVariables
                var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);

                baseDebugInfo = $"projectId: '{projectVars.projectId}', " +
                    $"versionId: '{projectVars.versionId}', " +
                    $"editorId: '{projectVars.editorId}', " +
                    $"outputChannelType: '{projectVars.outputChannelType}', " +
                    $"outputChannelVariantId: '{projectVars.outputChannelVariantId}', " +
                    $"outputChannelVariantLanguage: '{projectVars.outputChannelVariantLanguage}', " +
                    $"editorContentType: '{projectVars.editorContentType}', " +
                    $"reportTypeId: '{projectVars.reportTypeId}'";

                // Load the hierarchy using the existing method
                var xmlFilingHierarchy = RenderFilingHierarchy(
                    reqVars,
                    projectVars.projectId,
                    projectVars.versionId,
                    projectVars.editorId,
                    projectVars.editorContentType,
                    projectVars.reportTypeId,
                    projectVars.outputChannelType,
                    projectVars.outputChannelVariantId,
                    projectVars.outputChannelVariantLanguage
                );

                if (XmlContainsError(xmlFilingHierarchy))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = xmlFilingHierarchy.SelectSingleNode("//message")?.InnerText ?? "Error loading hierarchy",
                        Debuginfo = xmlFilingHierarchy.SelectSingleNode("//debuginfo")?.InnerText ?? ""
                    };
                }

                // Return the hierarchy XML HTML-encoded in the data field
                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "Successfully loaded hierarchy",
                    Debuginfo = baseDebugInfo,
                    Data = HttpUtility.HtmlEncode(xmlFilingHierarchy.OuterXml)
                };
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in LoadHierarchy: {baseDebugInfo}");
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error loading hierarchy: {ex.Message}",
                    Debuginfo = $"Exception: {ex}"
                };
            }
        }

        /// <summary>
        /// Saves filing hierarchy to DocumentStore
        /// </summary>
        public override async Task<TaxxorGrpcResponseMessage> SaveHierarchy(
            SaveHierarchyRequest request, ServerCallContext context)
        {
            var reqVars = _requestContext.RequestVariables;
            var baseDebugInfo = "";

            try
            {
                // Map gRPC request to ProjectVariables
                var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);

                // Extract user information from gRPC metadata headers if not in request
                if (projectVars.currentUser == null || string.IsNullOrEmpty(projectVars.currentUser.Id))
                {
                    string? userId = null;
                    string? userFirstName = null;
                    string? userLastName = null;
                    string? userEmail = null;
                    string? userDisplayName = null;

                    foreach (var header in context.RequestHeaders)
                    {
                        if (header.Key == "x-tx-userid") userId = header.Value;
                        else if (header.Key == "x-tx-userfirstname") userFirstName = header.Value;
                        else if (header.Key == "x-tx-userlastname") userLastName = header.Value;
                        else if (header.Key == "x-tx-useremail") userEmail = header.Value;
                        else if (header.Key == "x-tx-userdisplayname") userDisplayName = header.Value;
                    }

                    if (!string.IsNullOrEmpty(userId))
                    {
                        projectVars.currentUser = new AppUserTaxxor
                        {
                            Id = userId,
                            FirstName = userFirstName ?? "anonymous",
                            LastName = userLastName ?? "anonymous",
                            Email = userEmail ?? "",
                            DisplayName = userDisplayName ?? $"{userFirstName} {userLastName}",
                            IsAuthenticated = true,
                            HasViewRights = true,
                            HasEditRights = true
                        };
                    }
                }

                baseDebugInfo = $"projectId: '{projectVars.projectId}', " +
                    $"versionId: '{projectVars.versionId}', " +
                    $"editorId: '{projectVars.editorId}', " +
                    $"outputChannelType: '{projectVars.outputChannelType}', " +
                    $"outputChannelVariantId: '{projectVars.outputChannelVariantId}', " +
                    $"outputChannelVariantLanguage: '{projectVars.outputChannelVariantLanguage}', " +
                    $"commitChanges: '{request.CommitChanges}'";

                var debugRoutine = (siteType == "local" || siteType == "dev");
                var baseXPath = $"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel[@type='{projectVars.outputChannelType}']";

                // Hierarchy type master | slave | none
                var hierarchyType = "none";
                var masterOutputChannelId = "";
                var masterHierarchyId = "";
                var slaveOutputChannelIds = new System.Collections.Generic.List<string>();
                var slaveHierarchyIds = new System.Collections.Generic.List<string>();
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

                // Parse the hierarchy XML from the request
                var xmlHierarchyToSave = new XmlDocument();
                try
                {
                    xmlHierarchyToSave.LoadXml(request.Hierarchy);
                }
                catch (Exception ex)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not parse hierarchy XML",
                        Debuginfo = $"error: {ex.Message}"
                    };
                }

                if (debugRoutine) xmlHierarchyToSave.Save(logRootPathOs + "/rawhierarchytosave.xml");

                // Correct optionally strange combinations of hierarchy item attributes
                var nodeListItemStyleEnd = xmlHierarchyToSave.SelectNodes($"//item[@data-tocend='true' and @data-tocstyle]");
                foreach (XmlNode nodeItemStyleEnd in nodeListItemStyleEnd)
                {
                    nodeItemStyleEnd.RemoveAttribute("data-tocstyle");
                }

                if (debugRoutine) xmlHierarchyToSave.Save(logRootPathOs + "/strippedhierarchytosave.xml");

                // Add section numbers to the hierarchy items
                int firstHierarchicalLevelForNumbering = 1;
                var nodeFirstHierarchicalLevelForNumbering = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='toc_first-hierarchical-level']");
                if (
                    nodeFirstHierarchicalLevelForNumbering != null &&
                    !int.TryParse(nodeFirstHierarchicalLevelForNumbering.InnerText, out firstHierarchicalLevelForNumbering)
                )
                {
                    appLogger.LogWarning($"Unable to parse 'toc_first-hierarchical-level' value: {nodeFirstHierarchicalLevelForNumbering.InnerText} to an integer");
                }

                System.Xml.Xsl.XsltArgumentList xslParams = new();
                xslParams.AddParam("first-hierarchical-level", "", firstHierarchicalLevelForNumbering);
                xmlHierarchyToSave.LoadXml(TransformXml(xmlHierarchyToSave, "cms_hierarchy-add-sectionnumbers", xslParams));

                // Save the file
                var xmlPathOs = CalculateHierarchyPathOs(reqVars, projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage, projectVars);
                var saved = await SaveXmlDocument(xmlHierarchyToSave, xmlPathOs);
                if (!saved)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not save output channel hierarchy",
                        Debuginfo = $"xmlPathOs: {xmlPathOs}, stack-trace: {GetStackTrace()}"
                    };
                }

                // => When we save the master hierarchy, then use that as a template for the slave hierarchies
                if (hierarchyType == "master")
                {
                    // - Throw an error if we are dealing with an annual report project as this is much more complicated to sync properly
                    if (projectVars.editorId.Contains("annual"))
                    {
                        return new TaxxorGrpcResponseMessage
                        {
                            Success = false,
                            Message = "Cannot save master hierarchy",
                            Debuginfo = "No save routine has been implemented to sync the slave hierarchy with the master hierarchy yet!!"
                        };
                    }

                    // - Adjust the slave hierarchies (if any)
                    foreach (string slaveOutputChannelId in slaveOutputChannelIds)
                    {
                        // - Load the slave hierarchy file
                        var nodeVariant = xmlApplicationConfiguration.SelectSingleNode($"{baseXPath}/variants/variant[@id='{slaveOutputChannelId}']");
                        var metadataReferenceId = GetAttribute(nodeVariant, "metadata-id-ref");

                        var slaveHierarchyPathOs = CalculateHierarchyPathOs(projectVars.projectId, metadataReferenceId, reqVars, null, projectVars);
                        if (string.IsNullOrEmpty(slaveHierarchyPathOs))
                        {
                            appLogger.LogWarning($"Could not calculate path to hierarchy. projectVars.projectId: {projectVars.projectId}, metadataReferenceId: {metadataReferenceId}, stack-trace: {GetStackTrace()}");
                        }
                        else
                        {
                            if (System.IO.File.Exists(slaveHierarchyPathOs))
                            {
                                XmlDocument xmlHierarchySlave = new XmlDocument();
                                xmlHierarchySlave.Load(slaveHierarchyPathOs);

                                XmlDocument xmlHierarchyNewSlave = new XmlDocument();

                                // - Insert the complete master hierarchy in the new slave
                                xmlHierarchyNewSlave.LoadXml(xmlHierarchyToSave.OuterXml);

                                // - Select reference nodes in the new slave hierarchy file
                                var nodeFirstNewSlaveLevel1 = xmlHierarchyNewSlave.SelectSingleNode("/items/structured/item/sub_items/item[1]");
                                var nodeSubItemsNewSlave = xmlHierarchyNewSlave.SelectSingleNode("/items/structured/item/sub_items");

                                // - Loop through the original level 1 slave hierarchy items and find the items that do not exist in the master hierarchy
                                var nodeListLevel1SlaveHierarchyItems = xmlHierarchySlave.SelectNodes("/items/structured/item/sub_items/item");
                                foreach (XmlNode nodeLevel1SlaveItem in nodeListLevel1SlaveHierarchyItems)
                                {
                                    var dataReference = GetAttribute(nodeLevel1SlaveItem, "data-ref");
                                    if (!string.IsNullOrEmpty(dataReference))
                                    {
                                        var nodeItemMaster = xmlHierarchyNewSlave.SelectSingleNode($"/items/structured/item/sub_items/item[@data-ref='{dataReference}']");
                                        if (nodeItemMaster == null)
                                        {
                                            appLogger.LogDebug($"Node with data-ref {dataReference} exists in the original slave hierarchy, but not in the master hierarchy");

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

                                xmlHierarchyNewSlave.Save(slaveHierarchyPathOs);
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to load the hierarchy from path {slaveHierarchyPathOs}, stack-trace: {GetStackTrace()}");
                            }
                        }
                    }
                }

                if (request.CommitChanges)
                {
                    // Commit the change
                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "u"; // Update
                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = $"{outputChannelName} {projectVars.outputChannelType} hierarchy ({projectVars.outputChannelVariantId})";
                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = projectVars.outputChannelVariantId;
                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                    // Commit the result in the GIT repository - use Core version that accepts projectVars explicitly
                    CommitFilingDataCore(message, projectVars, ReturnTypeEnum.Xml, false);
                }

                // Update the CMS metadata content
                await UpdateCmsMetadata(projectVars.projectId);

                // Return success
                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "Successfully saved hierarchy",
                    Debuginfo = $"xmlPathOs: {xmlPathOs}"
                };
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in SaveHierarchy: {baseDebugInfo}");
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error saving hierarchy: {ex.Message}",
                    Debuginfo = $"Exception: {ex}"
                };
            }
        }

    }

}
