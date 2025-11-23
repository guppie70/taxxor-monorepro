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
using System.Collections.Generic;
using System.IO;

namespace DocumentStore.Services
{

    public class FilingDataUtilityService : Protos.FilingDataUtilityService.FilingDataUtilityServiceBase
    {

        private readonly RequestContext _requestContext;
        private readonly IMapper _mapper;

        public FilingDataUtilityService(RequestContext requestContext, IMapper mapper)
        {
            _requestContext = requestContext;
            _mapper = mapper;
        }

        /// <summary>
        /// Find and replace text in all project data files
        /// </summary>
        public override async Task<TaxxorGrpcResponseMessage> FindReplace(
            FindReplaceRequest request, ServerCallContext context)
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
                    $"searchFragment: '{request.SearchFragment}', " +
                    $"replaceFragment: '{request.ReplaceFragment}', " +
                    $"dryRun: '{request.DryRun}', " +
                    $"onlyInUse: '{request.OnlyInUse}', " +
                    $"includeFootnotes: '{request.IncludeFootnotes}'";

                var debugRoutine = (siteType == "local" || siteType == "dev");

                // Get the output channel language from projectVars or default to "en"
                var outputChannelLanguage = !string.IsNullOrEmpty(projectVars.outputChannelVariantLanguage)
                    ? projectVars.outputChannelVariantLanguage
                    : "en";

                var xmlFindReplaceResult = new XmlDocument();
                xmlFindReplaceResult.LoadXml("<findreplace></findreplace>");

                // Retrieve all the hierarchies defined for this project
                var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectVars.projectId, false);

                // Retrieve detailed information about the content data used in this project
                var xmlContentMetadata = CompileCmsMetadata(projectVars.projectId);
                xmlContentMetadata.Save($"{logRootPathOs}/_findandreplace-content-metadata.xml");

                // Retrieve the base path where we can find the content files
                var nodeProjectRoot = xmlContentMetadata.SelectSingleNode("/projects/cms_project");
                var nodeDataRoot = nodeProjectRoot.SelectSingleNode("data");
                if (nodeProjectRoot == null || nodeDataRoot == null)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Could not locate the information in the metadata file",
                        Debuginfo = $"stack-trace: {GetStackTrace()}"
                    };
                }

                var dataFolderPathOs = GetAttribute(nodeProjectRoot, "datalocation");

                if (!Directory.Exists(dataFolderPathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"Could not find location of the content data for project with ID {projectVars.projectId}",
                        Debuginfo = $"dataFolderPathOs: {dataFolderPathOs}, stack-trace: {GetStackTrace()}"
                    };
                }

                var totalDataReferencesSearched = 0;
                var totalFound = 0;
                var totalReplace = 0;

                // Find the content items that this routine needs to work with
                XmlNodeList nodeListContent = null;
                var xPath = "";
                if (request.OnlyInUse)
                {
                    var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
                    var editorContentType = "regular";
                    var outputChannelType = "pdf";
                    var reportTypeId = RetrieveReportTypeIdFromProjectId(projectVars.projectId);
                    var outputChannelVariantId = RetrieveFirstOutputChannelVariantIdFromEditorId(editorId, outputChannelType, outputChannelLanguage);

                    // Create minimal RequestVariables for gRPC handler (only needed for hierarchy rendering)
                    var hierarchyReqVars = new Framework.RequestVariables
                    {
                        siteLanguage = outputChannelLanguage
                    };

                    var xmlHierarchy = RenderFilingHierarchy(hierarchyReqVars, projectVars.projectId, projectVars.versionId, editorId, editorContentType, reportTypeId, outputChannelType, outputChannelVariantId, outputChannelLanguage);
                    xPath = "/items/structured//item";
                    nodeListContent = xmlHierarchy.SelectNodes(xPath);
                }
                else
                {
                    // Use the metadata content to locate the datafiles we need to search in
                    var onlyInUseSelector = (request.OnlyInUse) ? " and metadata/entry[@key='inuse']/text()='true'" : "";
                    xPath = $"content[@datatype='sectiondata' and contains(metadata/entry[@key='languages'], '{outputChannelLanguage}'){onlyInUseSelector}]";
                    nodeListContent = nodeDataRoot.SelectNodes(xPath);
                }

                foreach (XmlNode nodeContent in nodeListContent)
                {
                    var contentReference = GetAttribute(nodeContent, (request.OnlyInUse) ? "data-ref" : "ref");

                    if (!string.IsNullOrEmpty(contentReference))
                    {
                        // Add to result
                        var nodeResultItem = xmlFindReplaceResult.CreateElement("content");

                        var nodeRef = xmlFindReplaceResult.CreateElement("ref");
                        nodeRef.InnerText = contentReference;
                        nodeResultItem.AppendChild(nodeRef);

                        var dataFilePathOs = $"{dataFolderPathOs}/{contentReference}";
                        if (File.Exists(dataFilePathOs))
                        {
                            var xmlContentData = new XmlDocument();
                            xmlContentData.Load(dataFilePathOs);

                            // Check if we can find the search phrase in the content
                            xPath = $"/data/content[@lang='{outputChannelLanguage}']/*[local-name()='article' or local-name()='html']";
                            var nodeContentToSearch = xmlContentData.SelectSingleNode(xPath);
                            if (nodeContentToSearch != null)
                            {
                                var searchXpath = $"//*[text()[contains(., '{request.SearchFragment}')]]";
                                var nodeListTextNodes = nodeContentToSearch.SelectNodes($"{xPath}{searchXpath}");

                                var nodeFound = xmlFindReplaceResult.CreateElement("found");
                                totalFound += nodeListTextNodes.Count;
                                nodeFound.InnerText = nodeListTextNodes.Count.ToString();
                                nodeResultItem.AppendChild(nodeFound);

                                var countReplace = 0;

                                // Execute the replace
                                if (!request.DryRun)
                                {
                                    foreach (XmlNode nodeText in nodeListTextNodes)
                                    {
                                        nodeText.InnerXml = nodeText.InnerXml.Replace(request.SearchFragment, request.ReplaceFragment);
                                        countReplace++;
                                    }
                                }

                                // Save the XML document
                                if (countReplace > 0)
                                {
                                    xmlContentData.Save(dataFilePathOs);

                                    // Construct the commit message
                                    var itemInformation = RetrieveHierarchyItemInformation(xmlHierarchyOverview, projectVars, contentReference);

                                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "findreplace";
                                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = ((string.IsNullOrEmpty(itemInformation.Linkname)) ? "" : itemInformation.Linkname);
                                    xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", outputChannelLanguage);
                                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = itemInformation.Id;
                                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                                    // Commit the result in the GIT repository
                                    CommitFilingData(message, ReturnTypeEnum.Xml, true);
                                }

                                var nodeReplace = xmlFindReplaceResult.CreateElement("replace");
                                totalReplace += countReplace;
                                nodeReplace.InnerText = countReplace.ToString();
                                nodeResultItem.AppendChild(nodeReplace);
                            }
                            else
                            {
                                var errorText = $"Could not find content to search in (dataFilePathOs: {dataFilePathOs})";
                                appLogger.LogWarning(errorText);
                                var nodeError = xmlFindReplaceResult.CreateElement("error");
                                nodeError.InnerText = errorText;
                                nodeResultItem.AppendChild(nodeError);
                            }
                            totalDataReferencesSearched++;
                        }
                        else
                        {
                            appLogger.LogWarning($"Could not find content data file {dataFilePathOs} in FindReplace()");
                        }

                        xmlFindReplaceResult.DocumentElement.AppendChild(nodeResultItem);
                    }
                    else
                    {
                        appLogger.LogWarning("Could not find content reference in FindReplace()");
                    }
                }

                // Inject the totals as nodes
                var nodeTotalReferencesSearched = xmlFindReplaceResult.CreateElement("references-searched");
                nodeTotalReferencesSearched.InnerText = totalDataReferencesSearched.ToString();
                xmlFindReplaceResult.DocumentElement.PrependChild(nodeTotalReferencesSearched);

                var nodeTotalReplace = xmlFindReplaceResult.CreateElement("replace");
                nodeTotalReplace.InnerText = totalReplace.ToString();
                xmlFindReplaceResult.DocumentElement.PrependChild(nodeTotalReplace);

                var nodeTotalFound = xmlFindReplaceResult.CreateElement("found");
                nodeTotalFound.InnerText = totalFound.ToString();
                xmlFindReplaceResult.DocumentElement.PrependChild(nodeTotalFound);

                xmlFindReplaceResult.Save($"{logRootPathOs}/findreplace-result.xml");

                // Wrap the result in the expected structure for the client
                var xmlResponse = new XmlDocument();
                var nodeResult = xmlResponse.CreateElement("result");
                var nodeMessage = xmlResponse.CreateElement("message");
                nodeMessage.InnerText = "Successfully replaced content";
                nodeResult.AppendChild(nodeMessage);

                // Import the findreplace result as child elements of the response
                var nodePayload = xmlResponse.CreateElement("payload");
                var importedContent = xmlResponse.ImportNode(xmlFindReplaceResult.DocumentElement, true);
                nodePayload.AppendChild(importedContent);
                nodeResult.AppendChild(nodePayload);

                xmlResponse.AppendChild(nodeResult);

                // Return success
                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "Successfully replaced content",
                    Debuginfo = $"searchFragment: {request.SearchFragment}, replaceFragment: {request.ReplaceFragment}, dryRun: {request.DryRun}, includeFootnotes: {request.IncludeFootnotes}",
                    Data = HttpUtility.HtmlEncode(xmlResponse.DocumentElement.OuterXml)
                };
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in FindReplace: {baseDebugInfo}");
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = "There was an error while searching and replacing text in the data files",
                    Debuginfo = $"error: {ex}"
                };
            }
        }

        /// <summary>
        /// Clear the memory cache on DocumentStore service
        /// </summary>
        public override async Task<TaxxorGrpcResponseMessage> ClearCache(
            ClearCacheRequest request, ServerCallContext context)
        {
            var baseDebugInfo = "";

            // To please the compiler
            await DummyAwaiter();

            try
            {
                // Map gRPC request to ProjectVariables
                var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);

                baseDebugInfo = $"projectId: '{projectVars.projectId}'";

                // Clears the XML cache
                XmlCache.Clear();

                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "Successfully cleared the Document Store cache",
                    Debuginfo = "Cleared XML cache"
                };
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in ClearCache: {baseDebugInfo}");
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = "There was an error clearing the cache",
                    Debuginfo = $"error: {ex}"
                };
            }
        }

    }

}
