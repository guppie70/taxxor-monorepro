using AutoMapper;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Grpc.Core;
using DocumentStore.Protos;
using Taxxor.Project;
using Microsoft.Extensions.Logging;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace DocumentStore.Services
{
    public class BinaryFileManagementService : Protos.BinaryFileManagementService.BinaryFileManagementServiceBase
    {
        private readonly RequestContext _requestContext;
        private readonly IMapper _mapper;

        public BinaryFileManagementService(RequestContext requestContext, IMapper mapper)
        {
            _requestContext = requestContext;
            _mapper = mapper;
        }

        public override async Task<TaxxorGrpcResponseMessage> StoreFile(
            StoreFileRequest request, ServerCallContext context)
        {
            // To please the compiler
            await DummyAwaiter();

            try
            {
                // Map request to ProjectVariables
                var projectVars = _mapper.Map<ProjectVariables>(request);
                FillCorePathsInProjectVariables(ref projectVars);

                var baseDebugInfo = $"projectId: '{projectVars.projectId}', assetType: '{request.AssetType}', folderPath: '{request.FolderPath}', fileName: '{request.FileName}'";

                // Validate assetType
                if (request.AssetType != "projectimages" && request.AssetType != "projectdownloads")
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Invalid assetType. Must be 'projectimages' or 'projectdownloads'",
                        Debuginfo = baseDebugInfo
                    };
                }

                // Validate file extension
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg", ".tiff", ".pdf", ".zip", ".doc", ".docx" };
                var extension = Path.GetExtension(request.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"Invalid file type: {extension}. Allowed: {string.Join(", ", allowedExtensions)}",
                        Debuginfo = baseDebugInfo
                    };
                }

                // Validate folderPath doesn't escape directory
                if (request.FolderPath.Contains(".."))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Invalid folder path: cannot contain '..'",
                        Debuginfo = baseDebugInfo
                    };
                }

                // Validate file data
                if (request.FileData == null || request.FileData.Length == 0)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "File data is empty",
                        Debuginfo = baseDebugInfo
                    };
                }

                // Calculate paths
                var assetRootPathOs = RetrieveAssetTypeRootPathOs(projectVars, request.AssetType);
                if (string.IsNullOrEmpty(assetRootPathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"Unable to determine asset root path for assetType: {request.AssetType}",
                        Debuginfo = baseDebugInfo
                    };
                }

                var targetFolderPathOs = $"{assetRootPathOs}{request.FolderPath}";

                // Apply filename normalization if requested
                var finalFileName = request.NormalizeFilename
                    ? NormalizeFileName(request.FileName)
                    : request.FileName;

                // Create directory if needed
                if (!Directory.Exists(targetFolderPathOs))
                {
                    Directory.CreateDirectory(targetFolderPathOs);
                }

                // Write file (overwrite if exists)
                var targetFilePathOs = Path.Combine(targetFolderPathOs, finalFileName);
                if (File.Exists(targetFilePathOs))
                {
                    File.Delete(targetFilePathOs);
                }
                File.WriteAllBytes(targetFilePathOs, request.FileData.ToByteArray());

                // Update XML cache to reflect directory changes
                // Construct cache path: {cmsContentRootPathOs}/asset-manager/{assetType}.xml
                // assetType is "projectimages" or "projectdownloads", but cache file is "images.xml" or "downloads.xml"
                var cacheFileName = request.AssetType.Replace("project", "") + ".xml";
                var xmlCachePathOs = Path.Combine(projectVars.cmsContentRootPathOs, "asset-manager", cacheFileName);

                // Ensure asset-manager directory exists
                var assetManagerDirOs = Path.Combine(projectVars.cmsContentRootPathOs, "asset-manager");
                if (!Directory.Exists(assetManagerDirOs))
                {
                    Directory.CreateDirectory(assetManagerDirOs);
                }

                var dir = new DirectoryInfo(assetRootPathOs);
                var xmlDirInfo = new XDocument(_getDirectoryXml(dir, dir.FullName));
                xmlDirInfo.Save(xmlCachePathOs);

                // Construct commit message using XML template
                XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                SetAttribute(xmlCommitMessage.SelectSingleNode("/root/crud"), "application", "filemanager");
                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "upload";
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = finalFileName;
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = "na";

                // Commit to Git - call GitCommit directly since we already have projectVars
                // (CommitFilingAssets relies on HTTP context which isn't available in gRPC)
                // TODO: Consider refactoring Git commit logic to avoid code duplication
                //GitCommit(xmlCommitMessage.DocumentElement.InnerXml, projectVars.cmsContentRootPathOs, ReturnTypeEnum.Xml, false);

                // Return success
                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = $"Successfully stored file: {finalFileName}",
                    Debuginfo = $"{baseDebugInfo}, finalPath: {request.FolderPath}/{finalFileName}"
                };
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error storing file: {request.FileName}");

                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error storing file: {ex.Message}",
                    Debuginfo = ex.ToString()
                };
            }
        }
    }
}
