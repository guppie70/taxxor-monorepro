using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities for file management
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Routine to check if a file exists
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FileExists(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var locationId = request.RetrievePostedValue("locationid", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);
            var path = request.RetrievePostedValue("path", @"^([a-zA-Z]?\:)?(\/|[a-zA-Z_\-0-9\[\]\}\{])([\w\./\s\-\]\}\{\(\)])*$", false, ReturnTypeEnum.Xml);
            var relativeTo = request.RetrievePostedValue("relativeto", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);

            try
            {
                // Calculate the full path
                string? pathOs = null;
                if (string.IsNullOrEmpty(locationId))
                {
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(relativeTo)) HandleError(reqVars, "Not enough information recieved", $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");
                    pathOs = CalculateFullPathOs(path, relativeTo, reqVars);
                }
                else
                {
                    pathOs = CalculateFullPathOs(locationId, reqVars);
                }

                // Error handling
                if (string.IsNullOrEmpty(pathOs)) HandleError(reqVars, "Path to file was empty", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");

                TaxxorReturnMessage result = new TaxxorReturnMessage(true, ((File.Exists(pathOs)) ? "File exists" : "File does not exist"), (File.Exists(pathOs).ToString().ToLower()), $"pathOs: {pathOs}");
                await response.OK(result, ReturnTypeEnum.Xml);
            }
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await response.Error($"There was an error checking if the file exists. {ex}", ReturnTypeEnum.Xml);
            }
        }

        /// <summary>
        /// Deletes a complete directory structure
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task DelTree(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var locationId = request.RetrievePostedValue("locationid", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);
            var directoryPath = request.RetrievePostedValue("directorypath", @"^([a-zA-Z]?\:)?(\/|[a-zA-Z_\-0-9\[\]\}\{])([\w\./\s\-\]\}\{\(\)])*$", false, ReturnTypeEnum.Xml);
            var relativeTo = request.RetrievePostedValue("relativeto", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);
            var leaveRootFolderString = request.RetrievePostedValue("leaverootfolder", @"^(true|false)$", false, ReturnTypeEnum.Xml);
            var leaveRootFolder = (leaveRootFolderString == "true");

            var debugInfo = $"projectId: {projectVars.projectId}, locationId: {locationId}, directoryPath: {directoryPath}, relativeTo: {relativeTo}, leaveRootFolder: {leaveRootFolder}";


            try
            {
                // Calculate the full path
                string? directoryPathOs = null;
                if (string.IsNullOrEmpty(locationId))
                {
                    if (string.IsNullOrEmpty(directoryPath) || string.IsNullOrEmpty(relativeTo)) HandleError(reqVars, "Not enough information recieved", $"path: {directoryPath}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");
                    directoryPathOs = CalculateFullPathOs(directoryPath, relativeTo, reqVars);
                }
                else
                {
                    directoryPathOs = CalculateFullPathOs(locationId, reqVars);
                }
                Console.WriteLine($"** directoryPathOs: {directoryPathOs} **");

                // Empty path
                if (string.IsNullOrEmpty(directoryPathOs)) HandleError(reqVars, "Path to file was empty", $"pathOs: {directoryPathOs}, path: {directoryPath}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");

                // Directory does not exits
                if (!Directory.Exists(directoryPathOs)) HandleError("Directory does not exist", $"directoryPathOs: {directoryPathOs}, {debugInfo}");

                //
                // => Remove the complete directory
                //
                DelTree(directoryPathOs, !leaveRootFolder);


                TaxxorReturnMessage result = new TaxxorReturnMessage(true, "Successfully removed directory", $"directoryPathOs: {directoryPathOs}, {debugInfo}");
                await response.OK(result, ReturnTypeEnum.Xml);
            }
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await response.Error($"There was an error checking if the file exists. {ex}", ReturnTypeEnum.Xml);
            }
        }


        /// <summary>
        /// Recursively copies a complete directory and file structure from source to target
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task CopyDirectory(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var sourceLocationId = request.RetrievePostedValue("sourcelocationid", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);
            var sourcePath = request.RetrievePostedValue("sourcepath", @"^([a-zA-Z]?\:)?(\/|[a-zA-Z_\-0-9\[\]\}\{])([\w\./\s\-\]\}\{\(\)])*$", false, ReturnTypeEnum.Xml);
            var sourceRelativeTo = request.RetrievePostedValue("sourcerelativeto", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);

            var targetLocationId = request.RetrievePostedValue("targetlocationid", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);
            var targetPath = request.RetrievePostedValue("targetpath", @"^([a-zA-Z]?\:)?(\/|[a-zA-Z_\-0-9\[\]\}\{])([\w\./\s\-\]\}\{\(\)])*$", false, ReturnTypeEnum.Xml);
            var targetRelativeTo = request.RetrievePostedValue("targetrelativeto", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);

            var overrideTargetFilesString = request.RetrievePostedValue("overridetargetfiles", RegexEnum.Boolean, false, ReturnTypeEnum.Xml, "false");
            var overrideTargetFiles = (overrideTargetFilesString == "true");

            var createTargetDirectoryString = request.RetrievePostedValue("createtargetdirectory", RegexEnum.Boolean, false, ReturnTypeEnum.Xml, "false");
            var createTargetDirectory = (createTargetDirectoryString == "true");


            try
            {
                //
                // => Calculate the full source path
                //
                string? sourcePathOs = null;
                if (string.IsNullOrEmpty(sourceLocationId))
                {
                    if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(sourceRelativeTo)) HandleError(reqVars, "Not enough information recieved", $"sourcePath: {sourcePath}, sourceRelativeTo: {sourceRelativeTo}, stack-trace: {GetStackTrace()}");
                    sourcePathOs = CalculateFullPathOs(sourcePath, sourceRelativeTo, reqVars);
                }
                else
                {
                    sourcePathOs = CalculateFullPathOs(sourceLocationId, reqVars);
                }
                if (string.IsNullOrEmpty(sourcePathOs)) HandleError(reqVars, "Path to source directory was empty", $"sourcePathOs: {sourcePathOs}, sourcePath: {sourcePath}, sourceRelativeTo: {sourceRelativeTo}, stack-trace: {GetStackTrace()}");


                //
                // => Calculate the full target path
                //
                string? targetPathOs = null;
                if (string.IsNullOrEmpty(sourceLocationId))
                {
                    if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(targetRelativeTo)) HandleError(reqVars, "Not enough information recieved", $"targetPath: {targetPath}, targetRelativeTo: {targetRelativeTo}, stack-trace: {GetStackTrace()}");
                    targetPathOs = CalculateFullPathOs(targetPath, targetRelativeTo, reqVars);
                }
                else
                {
                    targetPathOs = CalculateFullPathOs(sourceLocationId, reqVars);
                }
                if (string.IsNullOrEmpty(targetPathOs)) HandleError(reqVars, "Path to target directory was empty", $"targetPathOs: {targetPathOs}, targetPath: {targetPath}, relativeTo: {targetRelativeTo}, stack-trace: {GetStackTrace()}");

                // appLogger.LogInformation($"sourcePathOs: {sourcePathOs}, targetPathOs: {targetPathOs}");
                // HandleError(reqVars, "Thrown on purpose", $"sourcePathOs: {sourcePathOs}, targetPathOs: {targetPathOs}");

                //
                // => Test if the source and target directory exists
                //
                if (!Directory.Exists(sourcePathOs)) HandleError(reqVars, "Unable to find source directory", $"sourcePathOs: {sourcePathOs}, stack-trace: {GetStackTrace()}");
                if (!Directory.Exists(targetPathOs))
                {
                    if (createTargetDirectory)
                    {
                        Directory.CreateDirectory(targetPathOs);
                    }
                    else
                    {
                        HandleError(reqVars, "Unable to find target directory", $"targetPathOs: {targetPathOs}, stack-trace: {GetStackTrace()}");
                    }
                }

                //
                // => Copy the complete directory and file structure
                //
                CopyDirectoryRecursive(sourcePathOs, targetPathOs, overrideTargetFiles);


                //
                // => Render the result
                //
                TaxxorReturnMessage result = new TaxxorReturnMessage(true, "Successfully copied directory", $"sourcePathOs: {sourcePathOs}, targetPathOs: {targetPathOs}");
                await response.OK(result, ReturnTypeEnum.Xml);
            }
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleError(reqVars, "There was a problem copying the directories", ex.ToString());
            }
        }

        /// <summary>
        /// Retrieves file properties
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FileProperties(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var locationId = request.RetrievePostedValue("locationid", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);
            var path = request.RetrievePostedValue("path", @"^([a-zA-Z]?\:)?(\/|[a-zA-Z_\-0-9\[\]\}\{])([\w\./\s\-\]\}\{\(\)])*$", false, ReturnTypeEnum.Xml);
            var relativeTo = request.RetrievePostedValue("relativeto", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);

            try
            {
                // Calculate the full path
                string? pathOs = null;
                if (string.IsNullOrEmpty(locationId))
                {
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(relativeTo)) HandleError(reqVars, "Not enough information recieved", $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");
                    pathOs = CalculateFullPathOs(path, relativeTo, reqVars);
                }
                else
                {
                    pathOs = CalculateFullPathOs(locationId, reqVars);
                }


                // Error handling
                if (string.IsNullOrEmpty(pathOs)) HandleError(reqVars, "Path to file was empty", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");

                // Retrieve the properties of the file
                if (!File.Exists(pathOs)) HandleError(reqVars, "File does not exist", $"pathOs: {pathOs}", 424);

                var xmlToReturn = GenerateSuccessXml("Successfully retrieved file properties", $"pathOs: {pathOs}");
                FileInfo fi = new FileInfo(pathOs);
                xmlToReturn.DocumentElement.AppendChild(xmlToReturn.CreateElementWithText("size", fi.Length.ToString()));
                xmlToReturn.DocumentElement.AppendChild(xmlToReturn.CreateElementWithText("created", fi.CreationTimeUtc.ToString()));
                xmlToReturn.DocumentElement.AppendChild(xmlToReturn.CreateElementWithText("accessed", fi.LastAccessTimeUtc.ToString()));
                xmlToReturn.DocumentElement.AppendChild(xmlToReturn.CreateElementWithText("written", fi.LastWriteTimeUtc.ToString()));

                await response.OK(xmlToReturn, ReturnTypeEnum.Xml);
            }
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await response.Error($"There was an error checking if the file exists. {ex}", ReturnTypeEnum.Xml);
            }
        }


        /// <summary>
        /// Lists all the files in the specified directory
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task DirectoryContents(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var locationId = request.RetrievePostedValue("locationid", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);
            var path = request.RetrievePostedValue("path", @"^([a-zA-Z]?\:)?(\/|[a-zA-Z_\-0-9\[\]\}\{])([\w\./\s\-\]\}\{\(\)])*$", false, ReturnTypeEnum.Xml);
            var relativeTo = request.RetrievePostedValue("relativeto", @"^[a-zA-Z_\-\d]{1,70}$", false, ReturnTypeEnum.Xml);

            try
            {
                // Calculate the full path
                string? pathOs = null;
                if (string.IsNullOrEmpty(locationId))
                {
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(relativeTo)) HandleError(reqVars, "Not enough information recieved", $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");
                    pathOs = CalculateFullPathOs(path, relativeTo, reqVars);
                }
                else
                {
                    pathOs = CalculateFullPathOs(locationId, reqVars);
                }


                // Error handling
                if (string.IsNullOrEmpty(pathOs)) HandleError(reqVars, "Path to file was empty", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");

                // Retrieve the properties of the file
                if (!Directory.Exists(pathOs)) HandleError(reqVars, "File does not exist", $"pathOs: {pathOs}", 424);

                var xmlToReturn = GenerateSuccessXml("Successfully retrieved file properties", $"pathOs: {pathOs}");
                var nodeDirectory = xmlToReturn.CreateElement("directory");
                nodeDirectory.SetAttribute("path", pathOs);
                xmlToReturn.DocumentElement.AppendChild(nodeDirectory);


                DirectoryInfo d = new(pathOs);
                foreach (var fi in d.GetFiles("*.txt", SearchOption.TopDirectoryOnly))
                {
                    var nodeFile = xmlToReturn.CreateElement("file");
                    nodeFile.AppendChild(xmlToReturn.CreateElementWithText("name", fi.Name));
                    nodeFile.AppendChild(xmlToReturn.CreateElementWithText("size", fi.Length.ToString()));
                    nodeFile.AppendChild(xmlToReturn.CreateElementWithText("created", fi.CreationTimeUtc.ToString()));
                    nodeFile.AppendChild(xmlToReturn.CreateElementWithText("accessed", fi.LastAccessTimeUtc.ToString()));
                    nodeFile.AppendChild(xmlToReturn.CreateElementWithText("written", fi.LastWriteTimeUtc.ToString()));

                    nodeDirectory.AppendChild(nodeFile);
                }




                // FileInfo fi = new FileInfo(pathOs);


                await response.OK(xmlToReturn, ReturnTypeEnum.Xml);
            }
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await response.Error($"There was an error checking if the file exists. {ex}", ReturnTypeEnum.Xml);
            }
        }


        public static async Task RetrieveFileContents(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var locationId = request.RetrievePostedValue("locationid", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var path = request.RetrievePostedValue("path", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var relativeTo = request.RetrievePostedValue("relativeto", RegexEnum.None, false, ReturnTypeEnum.Xml);

            if (projectVars.currentUser.IsAuthenticated)
            {
                // Calculate the full path
                string? pathOs = null;
                if (string.IsNullOrEmpty(locationId))
                {
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(relativeTo)) HandleError(reqVars, "Not enough information recieved", $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");
                    pathOs = CalculateFullPathOs(path, relativeTo, reqVars);
                }
                else
                {
                    pathOs = CalculateFullPathOs(locationId, reqVars);
                }



                // Error handling
                if (string.IsNullOrEmpty(pathOs)) HandleError(reqVars, "Path to file was empty", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");

                // Url decode path
                pathOs = pathOs.Replace("%20", " ");

                // Test if the file exists on the disk
                if (!File.Exists(pathOs)) HandleError(reqVars, $"Could not find file {Path.GetFileName(pathOs)}", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}", 424);

                // Retrieve the file type
                var fileType = GetFileType(pathOs);
                if (fileType == null) HandleError(reqVars, $"Unknown file type {fileType}", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");


                try
                {
                    if (File.Exists(pathOs))
                    {
                        var contents = await RetrieveTextFile(pathOs);
                        var xmlToReturn = GenerateSuccessXml("Successfully retrieved file contents", $"pathOs: {pathOs}", contents);
                        await response.OK(xmlToReturn, reqVars.returnType, true);
                    }
                    else
                    {
                        HandleError(reqVars, $"Could not find file {Path.GetFileName(pathOs)}", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}", 424);
                    }
                }
                catch (Exception ex)
                {
                    HandleError(reqVars, "There was an error retrieving the filing data", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, error: {ex}, stack-trace: {GetStackTrace()}");
                }

            }
            else
            {
                HandleError(reqVars.returnType, "Not authenticated", $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}", 403);
            }
        }

    }
}