using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Taxxor.Project;
using DocumentStore.Protos;

namespace DocumentStore.Services
{
    public class FilingDataService : Protos.FilingDataService.FilingDataServiceBase
    {
        private readonly RequestContext _requestContext;

        public FilingDataService(RequestContext requestContext)
        {
            _requestContext = requestContext;
        }

        public override async Task<TaxxorGrpcResponseMessage> CheckFileExists(
            FileExistsRequest request, ServerCallContext context)
        {
            try
            {
                // Calculate the full path
                string pathOs = CalculateFullPathOs(request.LocationId, request.Path, request.RelativeTo);

                if (string.IsNullOrEmpty(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Unable to calculate file path",
                        Debuginfo = $"locationId: {request.LocationId}, path: {request.Path}, relativeTo: {request.RelativeTo}"
                    };
                }

                bool exists = File.Exists(pathOs);

                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = exists ? "File exists" : "File does not exist",
                    Data = exists.ToString().ToLower(),
                    Debuginfo = pathOs
                };
            }
            catch (Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error checking file existence: {ex.Message}",
                    Debuginfo = $"Stack trace: {ex.StackTrace}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> GetFileContents(
            FileContentsRequest request, ServerCallContext context)
        {
            try
            {
                // Calculate the full path
                string pathOs = CalculateFullPathOs(request.LocationId, request.Path, request.RelativeTo);

                if (string.IsNullOrEmpty(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Unable to calculate file path",
                        Debuginfo = $"locationId: {request.LocationId}, path: {request.Path}, relativeTo: {request.RelativeTo}"
                    };
                }

                // URL decode path
                pathOs = pathOs.Replace("%20", " ");

                if (!File.Exists(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"File not found: {Path.GetFileName(pathOs)}",
                        Debuginfo = pathOs
                    };
                }

                // Read file contents and create XML envelope
                var xmlResponse = await CreateTaxxorXmlEnvelopeForFileTransport(pathOs);

                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "File retrieved successfully",
                    Data = xmlResponse.OuterXml,
                    Debuginfo = pathOs
                };
            }
            catch (Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error retrieving file contents: {ex.Message}",
                    Debuginfo = $"Stack trace: {ex.StackTrace}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> PutFileContents(
            PutFileContentsRequest request, ServerCallContext context)
        {
            try
            {
                // Calculate the full path
                string pathOs = CalculateFullPathOs(request.LocationId, request.Path, request.RelativeTo);

                if (string.IsNullOrEmpty(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Unable to calculate file path",
                        Debuginfo = $"locationId: {request.LocationId}, path: {request.Path}, relativeTo: {request.RelativeTo}"
                    };
                }

                // Ensure directory exists
                string dirPath = Path.GetDirectoryName(pathOs);
                if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                // Determine file type and save
                string fileExtension = Path.GetExtension(pathOs).Replace(".", "").ToLower();

                if (fileExtension == "xml" || fileExtension == "html")
                {
                    var xmlDocument = new XmlDocument();
                    try
                    {
                        // Decode HTML entities if needed
                        string dataToLoad = System.Net.WebUtility.HtmlDecode(request.Data);
                        xmlDocument.LoadXml(dataToLoad);
                        xmlDocument.Save(pathOs);
                    }
                    catch (XmlException xmlEx)
                    {
                        return new TaxxorGrpcResponseMessage
                        {
                            Success = false,
                            Message = "Invalid XML data provided",
                            Debuginfo = xmlEx.Message
                        };
                    }
                }
                else
                {
                    // Save as text file
                    string dataToStore = System.Net.WebUtility.HtmlDecode(request.Data);
                    File.WriteAllText(pathOs, dataToStore);
                }

                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "File saved successfully",
                    Debuginfo = pathOs
                };
            }
            catch (Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error saving file: {ex.Message}",
                    Debuginfo = $"Stack trace: {ex.StackTrace}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> DeleteDirectory(
            DeleteDirectoryRequest request, ServerCallContext context)
        {
            try
            {
                // Calculate the full path
                string pathOs = CalculateFullPathOs(request.LocationId, request.DirectoryPath, request.RelativeTo);

                if (string.IsNullOrEmpty(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Unable to calculate directory path",
                        Debuginfo = $"locationId: {request.LocationId}, path: {request.DirectoryPath}, relativeTo: {request.RelativeTo}"
                    };
                }

                if (!Directory.Exists(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"Directory not found: {Path.GetFileName(pathOs)}",
                        Debuginfo = pathOs
                    };
                }

                try
                {
                    // Delete directory and all its contents
                    Directory.Delete(pathOs, recursive: true);

                    // If requested, create the root folder again
                    if (request.LeaveRootFolder)
                    {
                        Directory.CreateDirectory(pathOs);
                    }

                    return new TaxxorGrpcResponseMessage
                    {
                        Success = true,
                        Message = "Directory deleted successfully",
                        Debuginfo = pathOs
                    };
                }
                catch (IOException ioEx)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"Error deleting directory: {ioEx.Message}",
                        Debuginfo = ioEx.StackTrace
                    };
                }
            }
            catch (Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error in delete directory operation: {ex.Message}",
                    Debuginfo = $"Stack trace: {ex.StackTrace}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> GetDirectoryContents(
            DirectoryContentsRequest request, ServerCallContext context)
        {
            try
            {
                // Calculate the full path
                string pathOs = CalculateFullPathOs(request.LocationId, request.Path, request.RelativeTo);

                if (string.IsNullOrEmpty(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Unable to calculate directory path",
                        Debuginfo = $"locationId: {request.LocationId}, path: {request.Path}, relativeTo: {request.RelativeTo}"
                    };
                }

                if (!Directory.Exists(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"Directory not found: {Path.GetFileName(pathOs)}",
                        Debuginfo = pathOs
                    };
                }

                var dirInfo = new DirectoryInfo(pathOs);
                var xmlResult = new XmlDocument();
                var rootElement = xmlResult.CreateElement("directory");
                rootElement.SetAttribute("path", pathOs);
                rootElement.SetAttribute("name", dirInfo.Name);
                xmlResult.AppendChild(rootElement);

                // Add directories
                var directoriesElement = xmlResult.CreateElement("directories");
                rootElement.AppendChild(directoriesElement);

                foreach (var dir in dirInfo.GetDirectories())
                {
                    var dirElement = xmlResult.CreateElement("directory");
                    dirElement.SetAttribute("name", dir.Name);
                    dirElement.SetAttribute("created", dir.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    dirElement.SetAttribute("modified", dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    directoriesElement.AppendChild(dirElement);
                }

                // Add files
                var filesElement = xmlResult.CreateElement("files");
                rootElement.AppendChild(filesElement);

                foreach (var file in dirInfo.GetFiles())
                {
                    var fileElement = xmlResult.CreateElement("file");
                    fileElement.SetAttribute("name", file.Name);
                    fileElement.SetAttribute("size", file.Length.ToString());
                    fileElement.SetAttribute("created", file.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    fileElement.SetAttribute("modified", file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    filesElement.AppendChild(fileElement);
                }

                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "Directory contents retrieved successfully",
                    Data = xmlResult.OuterXml,
                    Debuginfo = pathOs
                };
            }
            catch (Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error retrieving directory contents: {ex.Message}",
                    Debuginfo = $"Stack trace: {ex.StackTrace}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> GetFileProperties(
            FilePropertiesRequest request, ServerCallContext context)
        {
            try
            {
                // Calculate the full path
                string pathOs = CalculateFullPathOs(request.LocationId, request.Path, request.RelativeTo);

                if (string.IsNullOrEmpty(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Unable to calculate file path",
                        Debuginfo = $"locationId: {request.LocationId}, path: {request.Path}, relativeTo: {request.RelativeTo}"
                    };
                }

                if (!File.Exists(pathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"File not found: {Path.GetFileName(pathOs)}",
                        Debuginfo = pathOs
                    };
                }

                var fileInfo = new FileInfo(pathOs);
                var xmlResult = new XmlDocument();
                var fileElement = xmlResult.CreateElement("file");

                fileElement.SetAttribute("name", fileInfo.Name);
                fileElement.SetAttribute("path", pathOs);
                fileElement.SetAttribute("size", fileInfo.Length.ToString());
                fileElement.SetAttribute("extension", fileInfo.Extension);
                fileElement.SetAttribute("created", fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
                fileElement.SetAttribute("modified", fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                fileElement.SetAttribute("isReadOnly", fileInfo.IsReadOnly.ToString().ToLower());

                xmlResult.AppendChild(fileElement);

                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "File properties retrieved successfully",
                    Data = xmlResult.OuterXml,
                    Debuginfo = pathOs
                };
            }
            catch (Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error retrieving file properties: {ex.Message}",
                    Debuginfo = $"Stack trace: {ex.StackTrace}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> CopyDirectory(
            CopyDirectoryRequest request, ServerCallContext context)
        {
            try
            {
                // Calculate source and target paths
                string sourcePathOs = CalculateFullPathOs(request.SourceLocationId, request.SourcePath, request.SourceRelativeTo);
                string targetPathOs = CalculateFullPathOs(request.TargetLocationId, request.TargetPath, request.TargetRelativeTo);

                if (string.IsNullOrEmpty(sourcePathOs) || string.IsNullOrEmpty(targetPathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Unable to calculate source or target path",
                        Debuginfo = $"source: {sourcePathOs}, target: {targetPathOs}"
                    };
                }

                if (!Directory.Exists(sourcePathOs))
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"Source directory not found: {Path.GetFileName(sourcePathOs)}",
                        Debuginfo = sourcePathOs
                    };
                }

                // Check if target exists and handle override settings
                if (Directory.Exists(targetPathOs) && !request.OverrideTargetFiles)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = "Target directory already exists",
                        Debuginfo = targetPathOs
                    };
                }

                // Create target directory if needed
                if (request.CreateTargetDirectory && !Directory.Exists(targetPathOs))
                {
                    Directory.CreateDirectory(targetPathOs);
                }

                // Copy directory recursively
                try
                {
                    CopyDirectoryRecursive(sourcePathOs, targetPathOs, request.OverrideTargetFiles);

                    return new TaxxorGrpcResponseMessage
                    {
                        Success = true,
                        Message = "Directory copied successfully",
                        Debuginfo = $"source: {sourcePathOs}, target: {targetPathOs}"
                    };
                }
                catch (IOException ioEx)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Success = false,
                        Message = $"Error copying directory: {ioEx.Message}",
                        Debuginfo = ioEx.StackTrace
                    };
                }
            }
            catch (Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = $"Error in copy directory operation: {ex.Message}",
                    Debuginfo = $"Stack trace: {ex.StackTrace}"
                };
            }
        }

        // Helper methods

        private string CalculateFullPathOs(string locationId, string path, string relativeTo)
        {
            // This is a placeholder implementation - the actual implementation depends on the
            // ProjectLogic.CalculateFullPathOs method which handles path calculations
            try
            {
                if (!string.IsNullOrEmpty(locationId))
                {
                    return ProjectLogic.CalculateFullPathOs(locationId, new Framework.RequestVariables(null));
                }
                else if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(relativeTo))
                {
                    return ProjectLogic.CalculateFullPathOs(path, relativeTo, new Framework.RequestVariables(null));
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<XmlDocument> CreateTaxxorXmlEnvelopeForFileTransport(string pathOs)
        {
            // Use the static method from ProjectLogic
            return await ProjectLogic.CreateTaxxorXmlEnvelopeForFileTransport(pathOs);
        }

        private void CopyDirectoryRecursive(string sourceDir, string targetDir, bool overrideFiles)
        {
            var sourceDirInfo = new DirectoryInfo(sourceDir);

            // Create target directory
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Copy files
            foreach (var file in sourceDirInfo.GetFiles())
            {
                string targetFilePath = Path.Combine(targetDir, file.Name);
                file.CopyTo(targetFilePath, overrideFiles);
            }

            // Recursively copy subdirectories
            foreach (var subDir in sourceDirInfo.GetDirectories())
            {
                string targetSubDirPath = Path.Combine(targetDir, subDir.Name);
                CopyDirectoryRecursive(subDir.FullName, targetSubDirPath, overrideFiles);
            }
        }
    }
}
