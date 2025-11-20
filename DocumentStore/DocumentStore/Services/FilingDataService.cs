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
using System.IO;
using System.Collections.Generic;

namespace DocumentStore.Services
{
    public class FilingDataService : Protos.FilingDataService.FilingDataServiceBase
    {
        private readonly RequestContext _requestContext;
        private readonly IMapper _mapper;

        public FilingDataService(RequestContext requestContext, IMapper mapper)
        {
            _requestContext = requestContext;
            _mapper = mapper;
        }

        /// <summary>
        /// Saves filing composer source data to the DocumentStore
        /// </summary>
        public override async Task<TaxxorGrpcResponseMessage> SaveSourceData(
            SaveSourceDataRequest request, ServerCallContext context)
        {
            var reqVars = _requestContext.RequestVariables;
            var baseDebugInfo = "";

            try
            {
                // Map gRPC request to ProjectVariables (including user info from GrpcProjectVariables)
                var projectVars = _mapper.Map<ProjectVariables>(request);
                FillCorePathsInProjectVariables(ref projectVars);

                baseDebugInfo = $"projectId: '{projectVars.projectId}', versionId: '{projectVars.versionId}', did: '{request.Did}', contentLanguage: '{request.ContentLanguage}'";

                // Validate parameters
                if (!ValidateCmsPostedParameters(projectVars, "text") || string.IsNullOrEmpty(request.Did))
                {
                    appLogger.LogError($"Validation failed for SaveSourceData: {baseDebugInfo}");
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Invalid parameters provided",
                        Debuginfo = baseDebugInfo
                    };
                }

                // Load the hierarchy file to get the linkname for commit message
                XmlDocument xmlFilingDocumentHierarchy = new XmlDocument();
                var hierarchyPathOs = CalculateHierarchyPathOs(reqVars, projectVars.projectId, projectVars.versionId,
                    projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId,
                    projectVars.outputChannelType, projectVars.outputChannelVariantId, request.ContentLanguage);

                try
                {
                    xmlFilingDocumentHierarchy.Load(hierarchyPathOs);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Could not load hierarchy file: {hierarchyPathOs}");
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not load output channel hierarchy",
                        Debuginfo = $"hierarchyPathOs: {hierarchyPathOs}, error: {ex.Message}"
                    };
                }

                // Calculate the path to the XML file
                var xmlFilePathOs = RetrieveInlineFilingComposerXmlPathOs(reqVars, projectVars, request.Did, false);

                // Load and save the XML data
                XmlDocument xmlDocument = new XmlDocument();
                try
                {
                    xmlDocument.LoadXml(request.Data);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Could not parse posted data as XML");
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not parse posted data as XML",
                        Debuginfo = $"error: {ex.Message}"
                    };
                }

                // Save the data
                var saveResult = await SaveXmlInlineFilingComposerData(xmlDocument, xmlFilePathOs, projectVars);
                if (!saveResult.Success)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = saveResult.Message,
                        Debuginfo = saveResult.DebugInfo
                    };
                }

                // Create commit message
                var linkname = RetrieveLinkName(request.Did, xmlFilingDocumentHierarchy);
                if (string.IsNullOrEmpty(linkname))
                {
                    var nodeLinkName = xmlDocument.SelectSingleNode($"//item[@id='{request.Did}']/linknames/linkname[@lang='{request.ContentLanguage}']");
                    if (nodeLinkName != null) linkname = nodeLinkName.InnerText;
                }

                XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "u"; // Update
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = linkname ?? "";
                xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", projectVars.outputChannelVariantLanguage);
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = request.Did;
                var message = xmlCommitMessage.DocumentElement.InnerXml;

                // Commit to Git - use Core version that accepts projectVars explicitly
                var committed = CommitFilingDataCore(message, projectVars, ReturnTypeEnum.Xml, false);
                if (!committed)
                {
                    appLogger.LogWarning("Unable to commit the section data to version control");
                }

                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "Successfully stored the data",
                    Debuginfo = saveResult.DebugInfo,
                    Data = GenerateSuccessXml("Successfully stored the data", saveResult.DebugInfo).OuterXml
                };
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in SaveSourceData: {baseDebugInfo}");
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error saving source data: {ex.Message}",
                    Debuginfo = $"Exception: {ex}"
                };
            }
        }

        /// <summary>
        /// Deletes filing composer source data sections
        /// </summary>
        public override async Task<TaxxorGrpcResponseMessage> DeleteSourceData(
            DeleteSourceDataRequest request, ServerCallContext context)
        {
            var reqVars = _requestContext.RequestVariables;
            var baseDebugInfo = "";

            try
            {
                // Map gRPC request to ProjectVariables (including user info from GrpcProjectVariables)
                var projectVars = _mapper.Map<ProjectVariables>(request);
                FillCorePathsInProjectVariables(ref projectVars);

                baseDebugInfo = $"projectId: '{projectVars.projectId}', versionId: '{projectVars.versionId}', dataType: '{request.DataType}'";

                // Validate parameters
                if (!ValidateCmsPostedParameters(projectVars, request.DataType))
                {
                    appLogger.LogError($"Validation failed for DeleteSourceData: {baseDebugInfo}");
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Invalid parameters provided",
                        Debuginfo = baseDebugInfo
                    };
                }

                // Load the delete actions XML
                var xmlDeleteActionsData = new XmlDocument();
                try
                {
                    xmlDeleteActionsData.LoadXml(request.XmlDeleteActions);
                }
                catch (Exception ex)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not parse delete actions XML",
                        Debuginfo = $"error: {ex.Message}"
                    };
                }

                var nodeListItemsToDelete = xmlDeleteActionsData.SelectNodes("/report/itemtodelete");
                if (nodeListItemsToDelete.Count == 0)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not find any items to delete",
                        Debuginfo = $"xmlDeleteActions: {request.XmlDeleteActions}"
                    };
                }

                var dataFolderPathOs = RetrieveFilingDataFolderPathOs(projectVars.projectId);

                // Process each item to delete (logic extracted from FilingComposerDataDelete.cs)
                foreach (XmlNode nodeItemToDelete in nodeListItemsToDelete)
                {
                    var dataRefToDelete = nodeItemToDelete.GetAttribute("data-ref");
                    if (string.IsNullOrEmpty(dataRefToDelete))
                    {
                        return new TaxxorGrpcResponseMessage
                        {
                            Success = false,
                            Message = "Could not find data reference to delete",
                            Debuginfo = $"xmlDeleteActions: {request.XmlDeleteActions}"
                        };
                    }

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

                            var xpath = $"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/variants/variant[@id='{outputChannelVariantId}']/@metadata-id-ref";
                            var hierarchyMetadataId = RetrieveAttributeValueIfExists(xpath, xmlApplicationConfiguration);
                            if (string.IsNullOrEmpty(hierarchyMetadataId))
                            {
                                return new TaxxorGrpcResponseMessage
                                {
                                    Success = false,
                                    Message = "Could not find id of the hierarchy",
                                    Debuginfo = $"xpath: {xpath}"
                                };
                            }

                            xpath = $"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/metadata_system/metadata[@id='{hierarchyMetadataId}']/location";
                            var hierarchyLocationNode = xmlApplicationConfiguration.SelectSingleNode(xpath);
                            if (hierarchyLocationNode == null)
                            {
                                return new TaxxorGrpcResponseMessage
                                {
                                    Success = false,
                                    Message = "Could not find location node of the hierarchy",
                                    Debuginfo = $"xpath: {xpath}"
                                };
                            }

                            var hierarchyFilePathOs = CalculateFullPathOs(hierarchyLocationNode, reqVars);
                            var xmlOutputChannelHierarchy = new XmlDocument();
                            xmlOutputChannelHierarchy.Load(hierarchyFilePathOs);

                            xpath = $"//item[@data-ref='{dataRefToDelete}']";
                            var nodeOutputChannelHierarchyItemToDelete = xmlOutputChannelHierarchy.SelectSingleNode(xpath);
                            if (nodeOutputChannelHierarchyItemToDelete == null)
                            {
                                return new TaxxorGrpcResponseMessage
                                {
                                    Success = false,
                                    Message = "Could not find node in hierarchy to delete",
                                    Debuginfo = $"xpath: {xpath}"
                                };
                            }

                            // Retrieve information about the item we are about to delete
                            var linkName = RetrieveNodeValueIfExists("web_page/linkname", nodeOutputChannelHierarchyItemToDelete);
                            var hierarchyId = GetAttribute(nodeOutputChannelHierarchyItemToDelete, "id");

                            RemoveXmlNode(nodeOutputChannelHierarchyItemToDelete);

                            var saved = await SaveXmlDocument(xmlOutputChannelHierarchy, hierarchyFilePathOs);
                            if (!saved)
                            {
                                return new TaxxorGrpcResponseMessage
                                {
                                    Success = false,
                                    Message = "Could not store the hierarchy",
                                    Debuginfo = $"hierarchyFilePathOs: {hierarchyFilePathOs}"
                                };
                            }

                            // Commit the change in GIT
                            var sectionTitle = linkName ?? "[unknown]";
                            XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                            xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "d"; // Delete
                            xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = sectionTitle; // Linkname
                            xmlCommitMessage.SelectSingleNode("/root/id").InnerText = hierarchyId; // Section ID
                            var message = xmlCommitMessage.DocumentElement.InnerXml;

                            // Commit the result using Core version
                            CommitFilingDataCore(message, projectVars, ReturnTypeEnum.Xml, true);
                        }
                    }

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

                        // Commit the result using Core version
                        CommitFilingDataCore(message, projectVars, ReturnTypeEnum.Xml, false);
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorGrpcResponseMessage
                        {
                            Success = false,
                            Message = "Could not remove section data xml",
                            Debuginfo = $"error: {ex}"
                        };
                    }
                }

                // Update the CMS metadata content
                await UpdateCmsMetadata(projectVars.projectId);

                // Get the updated section overview using the non-HTTP overload
                var overviewResult = RetrieveFilingDataOverview(dataFolderPathOs);

                if (overviewResult.Success)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = true,
                        Message = "Successfully deleted the data",
                        Data = overviewResult.XmlPayload.OuterXml
                    };
                }
                else
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not compile overview after delete",
                        Debuginfo = overviewResult.DebugInfo
                    };
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in DeleteSourceData: {baseDebugInfo}");
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error deleting source data: {ex.Message}",
                    Debuginfo = $"Exception: {ex}"
                };
            }
        }

        /// <summary>
        /// Creates a new filing composer source data section
        /// </summary>
        public override async Task<TaxxorGrpcResponseMessage> CreateSourceData(
            CreateSourceDataRequest request, ServerCallContext context)
        {
            var reqVars = _requestContext.RequestVariables;
            var baseDebugInfo = "";

            try
            {
                // Map gRPC request to ProjectVariables (including user info from GrpcProjectVariables)
                var projectVars = _mapper.Map<ProjectVariables>(request);
                FillCorePathsInProjectVariables(ref projectVars);

                baseDebugInfo = $"projectId: '{projectVars.projectId}', versionId: '{projectVars.versionId}', sectionId: '{request.SectionId}', dataType: '{request.DataType}'";

                // Validate parameters
                if (!ValidateCmsPostedParameters(projectVars, request.DataType))
                {
                    appLogger.LogError($"Validation failed for CreateSourceData: {baseDebugInfo}");
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Invalid parameters provided",
                        Debuginfo = baseDebugInfo
                    };
                }

                // Load the XML data
                var xmlFilingSectionData = new XmlDocument();
                try
                {
                    xmlFilingSectionData.LoadXml(request.Data);
                }
                catch (Exception ex)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not parse data as XML",
                        Debuginfo = $"error: {ex.Message}"
                    };
                }

                var dataFolderPathOs = RetrieveFilingDataFolderPathOs(projectVars.projectId);

                // Calculate a new unique filename (logic from FilingComposerDataCreate.cs)
                List<string> filenames = new List<string>();
                string[] sectionFilePathOs = Directory.GetFiles(dataFolderPathOs, "*.xml", SearchOption.TopDirectoryOnly);
                foreach (string xmlPathOs in sectionFilePathOs)
                {
                    try
                    {
                        filenames.Add(Path.GetFileName(xmlPathOs));
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorGrpcResponseMessage
                        {
                            Success = false,
                            Message = "There was an error reading the section xml files",
                            Debuginfo = $"xmlPathOs: {xmlPathOs}, error: {ex}"
                        };
                    }
                }

                var postfix = 1;
                var newSectionId = request.SectionId;
                var newSectionFileName = $"{request.SectionId}.xml";
                while (filenames.Exists(element => element == newSectionFileName))
                {
                    newSectionId = $"{request.SectionId}-{postfix}";
                    newSectionFileName = $"{newSectionId}.xml";
                    postfix++;
                }

                // Update the section id's in the file to match the filename
                var nodeListArticles = xmlFilingSectionData.SelectNodes("//article");
                foreach (XmlNode nodeArticle in nodeListArticles)
                {
                    nodeArticle.SetAttribute("id", newSectionId);
                    nodeArticle.SetAttribute("data-guid", newSectionId);
                    nodeArticle.SetAttribute("data-fact-id", newSectionId);
                }

                // Store the new section XML file
                string xmlFilePathOs = $"{dataFolderPathOs}/{newSectionFileName}";
                var postProcessResult = await SaveXmlInlineFilingComposerData(xmlFilingSectionData, xmlFilePathOs, projectVars);
                if (!postProcessResult.Success)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = postProcessResult.Message,
                        Debuginfo = postProcessResult.DebugInfo
                    };
                }

                // Update the metadata entry
                await UpdateCmsMetadataEntry(projectVars.projectId, Path.GetFileName(xmlFilePathOs));

                // Commit in GIT
                var sectionTitle = "[unknown]";
                var nodeContentHeader = xmlFilingSectionData.SelectSingleNode("/data/content/article//section/h1") ?? xmlFilingSectionData.SelectSingleNode("/data/content//title");
                if (nodeContentHeader != null) sectionTitle = nodeContentHeader.InnerText;

                XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "c"; // Create
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = sectionTitle; // Linkname
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = request.SectionId; // Section ID
                var message = xmlCommitMessage.DocumentElement.InnerXml;

                // Commit the result using Core version
                CommitFilingDataCore(message, projectVars, ReturnTypeEnum.Xml, true);

                // Get the updated section overview using the non-HTTP overload
                var overviewResult = RetrieveFilingDataOverview(dataFolderPathOs);

                if (overviewResult.Success)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = true,
                        Message = "Successfully created the new section",
                        Data = overviewResult.XmlPayload.OuterXml,
                        Debuginfo = postProcessResult.DebugInfo
                    };
                }
                else
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not compile overview after create",
                        Debuginfo = overviewResult.DebugInfo
                    };
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in CreateSourceData: {baseDebugInfo}");
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error creating source data: {ex.Message}",
                    Debuginfo = $"Exception: {ex}"
                };
            }
        }
    }
}
