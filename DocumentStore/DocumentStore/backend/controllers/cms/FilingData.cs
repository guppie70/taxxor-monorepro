using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal loading data for a Taxxor Editor Filing
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Streams an image from the project data store as a binary content
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        // MIGRATED - CAN BE REMOVED
        public static async Task RetrieveImageData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var locationId = request.RetrievePostedValue("locationid", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var path = request.RetrievePostedValue("path", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var relativeTo = request.RetrievePostedValue("relativeto", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var thumbnailSize = request.RetrievePostedValue("thumbnailsize", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var autoConvertString = request.RetrievePostedValue("autocovert", RegexEnum.Boolean, false, ReturnTypeEnum.Xml);
            var autoConvert = autoConvertString == "true";

            // This is a short term fix for URL encoded spaces in paths
            if (path.Contains("%20"))
            {
                path = path.Replace("%20", " ");
            }

            // Calculate the full path
            string? pathOs;
            if (string.IsNullOrEmpty(locationId))
            {
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(relativeTo)) HandleError(reqVars, "Not enough information recieved to load image", $"path: {path}, relativeTo: {relativeTo}");
                pathOs = CalculateFullPathOs(path, relativeTo, reqVars);
            }
            else
            {
                pathOs = CalculateFullPathOs(locationId, reqVars);
            }

            // Error handling
            if (string.IsNullOrEmpty(pathOs)) HandleError(reqVars, "Path to image file was empty", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}");

            if (!File.Exists(pathOs)) HandleError(reqVars, $"Could not find image file {Path.GetFileName(pathOs)}", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}", 424);

            // Retrieve the file type
            var fileType = GetFileType(pathOs);
            if (fileType == null) HandleError(reqVars, $"Unknown image file type {fileType}", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}");

            int imageThumbnailSize = -1;
            if (!string.IsNullOrEmpty(thumbnailSize))
            {
                if (!int.TryParse(thumbnailSize.Trim(), out imageThumbnailSize))
                {
                    HandleError("Invalid thubmbail size", $"thumbnailSize: {thumbnailSize}");
                }
            }

            if (imageThumbnailSize < 0)
            {
                await StreamImage(response, pathOs, autoConvert);
            }
            else
            {
                // Check if a thumbnail file is ready to be streamed to the frontend
                var convertOnTheFly = true;
                if (imageThumbnailSize == ThumbnailMaxSize)
                {
                    var thumbnailImageFilename = ThumbnailFilenameTemplate.Replace("{filename}", Path.GetFileNameWithoutExtension(pathOs)).Replace("{extension}", ThumbnailFileExtension);
                    var thumbnailImagePathOs = $"{pathOs.Replace(path, "")}/images/{ImageRenditionsFolderName}/{Path.GetDirectoryName(path)}/{thumbnailImageFilename}".Replace("//", "/");
                    if (File.Exists(thumbnailImagePathOs))
                    {
                        convertOnTheFly = false;
                        // appLogger.LogInformation("Streaming thumbnail");
                        await StreamImage(response, thumbnailImagePathOs);
                    }
                }

                if (convertOnTheFly)
                {
                    // appLogger.LogInformation("Converting full image on-the-fly");
                    await StreamImageThumbnail(response, pathOs, imageThumbnailSize);
                }
            }
        }

        // MIGRATED - CAN BE REMOVED
        public static async Task RetrieveFileBinary(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var locationId = request.RetrievePostedValue("locationid", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var path = request.RetrievePostedValue("path", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var relativeTo = request.RetrievePostedValue("relativeto", RegexEnum.None, false, ReturnTypeEnum.Xml);


            // Calculate the full path
            string? pathOs = null;
            if (string.IsNullOrEmpty(locationId))
            {
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(relativeTo)) HandleError(reqVars, "Not enough information recieved to load image", $"path: {path}, relativeTo: {relativeTo}");
                pathOs = CalculateFullPathOs(path, relativeTo, reqVars);
            }
            else
            {
                pathOs = CalculateFullPathOs(locationId, reqVars);
            }

            // Error handling
            if (string.IsNullOrEmpty(pathOs)) HandleError(reqVars, "Path to binary file was empty", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}");

            if (!File.Exists(pathOs)) HandleError(reqVars, $"Could not find binary file {Path.GetFileName(pathOs)}", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}", 424);

            // Retrieve the file type
            var fileType = GetFileType(pathOs);
            if (fileType == null) HandleError(reqVars, $"Unknown image file type {fileType}", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}");

            await StreamImage(response, pathOs);
        }

        /// <summary>
        /// Retrieves the data for the current Taxxor user, combines it in one XmlDocument and returns it to the client
        /// </summary>
        /// <returns>The user data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task RetrieveFilingData(HttpRequest request, HttpResponse response, RouteData routeData)
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
                    // Create a standard XML format that contains the content of the file in an encoded format
                    var xmlResponse = await CreateTaxxorXmlEnvelopeForFileTransport(pathOs);

                    // Stick the file content in the message field of the success xml and the file path into the debuginfo node
                    await response.OK(xmlResponse, reqVars.returnType, true);
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


        /// <summary>
        /// Core logic for retrieving filing data without HTTP dependencies
        /// </summary>
        /// <param name="locationId">Location ID</param>
        /// <param name="path">Path</param>
        /// <param name="relativeTo">Relative to</param>
        /// <param name="projectVars">Project variables</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>Filing data result</returns>
        public static async Task<FilingDataResult> RetrieveFilingDataCore(
            string locationId,
            string path,
            string relativeTo,
            ProjectVariables projectVars,
            RequestVariables reqVars)
        {
            var result = new FilingDataResult();

            if (!projectVars.currentUser.IsAuthenticated)
            {
                result.Success = false;
                result.ErrorMessage = "Not authenticated";
                result.DebugInfo = $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                result.StatusCode = 403;
                return result;
            }

            // Calculate the full path
            string? pathOs = null;
            if (string.IsNullOrEmpty(locationId))
            {
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(relativeTo))
                {
                    result.Success = false;
                    result.ErrorMessage = "Not enough information received";
                    result.DebugInfo = $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                    return result;
                }
                pathOs = CalculateFullPathOs(path, relativeTo, reqVars);
            }
            else
            {
                pathOs = CalculateFullPathOs(locationId, reqVars);
            }

            // Error handling
            if (string.IsNullOrEmpty(pathOs))
            {
                result.Success = false;
                result.ErrorMessage = "Path to file was empty";
                result.DebugInfo = $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                return result;
            }

            // Url decode path
            pathOs = pathOs.Replace("%20", " ");

            // Test if the file exists on the disk
            if (!File.Exists(pathOs))
            {
                result.Success = false;
                result.ErrorMessage = $"Could not find file {Path.GetFileName(pathOs)}";
                result.DebugInfo = $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                result.StatusCode = 424;
                return result;
            }

            // Retrieve the file type
            var fileType = GetFileType(pathOs);
            if (fileType == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Unknown file type {fileType}";
                result.DebugInfo = $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                return result;
            }

            try
            {
                // Create a standard XML format that contains the content of the file in an encoded format
                result.XmlResponse = await CreateTaxxorXmlEnvelopeForFileTransport(pathOs);
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "There was an error retrieving the filing data";
                result.DebugInfo = $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, error: {ex}, stack-trace: {GetStackTrace()}";
                return result;
            }
        }

        /// <summary>
        /// Stores a filing data file in the project store
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task StoreFilingData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var locationId = request.RetrievePostedValue("locationid", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var path = request.RetrievePostedValue("path", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var relativeTo = request.RetrievePostedValue("relativeto", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var data = request.RetrievePostedValue("data", RegexEnum.None, true, ReturnTypeEnum.Xml);


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

                // Retrieve the file type
                var fileType = GetFileType(pathOs);
                if (fileType == null) HandleError(reqVars, "Unknown file type", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");

                var fileExtension = Path.GetExtension(pathOs).Replace(".", "");

                switch (fileType)
                {
                    case "text":
                        if (fileExtension == "xml" || fileExtension == "html")
                        {
                            var xmlDocumentToStore = new XmlDocument();
                            try
                            {
                                if (data.IsHtmlEncoded())
                                {
                                    xmlDocumentToStore.LoadXml(HttpUtility.HtmlDecode(data));
                                }
                                else
                                {
                                    xmlDocumentToStore.LoadXml(data);
                                }

                                xmlDocumentToStore.Save(pathOs);

                                await response.OK(GenerateSuccessXml("Successfully stored XML data", ""));
                            }
                            catch (Exception ex)
                            {
                                HandleError(reqVars, "There was an error loading the filing data to save", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, error: {ex}, stack-trace: {GetStackTrace()}");
                            }
                        }
                        else
                        {
                            var textContentToStore = (data.IsHtmlEncoded()) ? HttpUtility.HtmlDecode(data) : data;
                            try
                            {
                                TextFileCreate(textContentToStore, pathOs);

                                await response.OK(GenerateSuccessXml("Successfully stored Text data", ""));
                            }
                            catch (Exception ex)
                            {
                                HandleError(reqVars, "There was an error saving the text file", $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, error: {ex}, stack-trace: {GetStackTrace()}");
                            }
                        }

                        break;


                    default:
                        HandleError(reqVars, "Filetype not implemented yet", $"fileType: {fileType}, fileExtension: {fileExtension} , pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");
                        break;


                }
            }
            else
            {
                HandleError(reqVars.returnType, "Not authenticated", $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}", 403);
            }
        }

        /// <summary>
        /// Core logic for storing filing data without HTTP dependencies
        /// </summary>
        /// <param name="locationId">Location ID</param>
        /// <param name="path">Path</param>
        /// <param name="relativeTo">Relative to</param>
        /// <param name="data">Data to store</param>
        /// <param name="projectVars">Project variables</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>Filing data result</returns>
        public static async Task<FilingDataResult> StoreFilingDataCore(
            string locationId,
            string path,
            string relativeTo,
            string data,
            ProjectVariables projectVars,
            RequestVariables reqVars)
        {
            var result = new FilingDataResult();

            await DummyAwaiter();

            if (!projectVars.currentUser.IsAuthenticated)
            {
                result.Success = false;
                result.ErrorMessage = "Not authenticated";
                result.DebugInfo = $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                result.StatusCode = 403;
                return result;
            }

            // Calculate the full path
            string? pathOs = null;
            if (string.IsNullOrEmpty(locationId))
            {
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(relativeTo))
                {
                    result.Success = false;
                    result.ErrorMessage = "Not enough information received";
                    result.DebugInfo = $"path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                    return result;
                }
                pathOs = CalculateFullPathOs(path, relativeTo, reqVars);
            }
            else
            {
                pathOs = CalculateFullPathOs(locationId, reqVars);
            }

            // Error handling
            if (string.IsNullOrEmpty(pathOs))
            {
                result.Success = false;
                result.ErrorMessage = "Path to file was empty";
                result.DebugInfo = $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                return result;
            }

            // Retrieve the file type
            var fileType = GetFileType(pathOs);
            if (fileType == null)
            {
                result.Success = false;
                result.ErrorMessage = "Unknown file type";
                result.DebugInfo = $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                return result;
            }

            var fileExtension = Path.GetExtension(pathOs).Replace(".", "");

            switch (fileType)
            {
                case "text":
                    if (fileExtension == "xml" || fileExtension == "html")
                    {
                        var xmlDocumentToStore = new XmlDocument();
                        try
                        {
                            if (data.IsHtmlEncoded())
                            {
                                xmlDocumentToStore.LoadXml(HttpUtility.HtmlDecode(data));
                            }
                            else
                            {
                                xmlDocumentToStore.LoadXml(data);
                            }

                            xmlDocumentToStore.Save(pathOs);

                            result.Success = true;
                            result.XmlResponse = GenerateSuccessXml("Successfully stored XML data", "");
                            return result;
                        }
                        catch (Exception ex)
                        {
                            result.Success = false;
                            result.ErrorMessage = "There was an error loading the filing data to save";
                            result.DebugInfo = $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, error: {ex}, stack-trace: {GetStackTrace()}";
                            return result;
                        }
                    }
                    else
                    {
                        var textContentToStore = (data.IsHtmlEncoded()) ? HttpUtility.HtmlDecode(data) : data;
                        try
                        {
                            TextFileCreate(textContentToStore, pathOs);

                            result.Success = true;
                            result.XmlResponse = GenerateSuccessXml("Successfully stored Text data", "");
                            return result;
                        }
                        catch (Exception ex)
                        {
                            result.Success = false;
                            result.ErrorMessage = "There was an error saving the text file";
                            result.DebugInfo = $"pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, error: {ex}, stack-trace: {GetStackTrace()}";
                            return result;
                        }
                    }

                default:
                    result.Success = false;
                    result.ErrorMessage = "Filetype not implemented yet";
                    result.DebugInfo = $"fileType: {fileType}, fileExtension: {fileExtension} , pathOs: {pathOs}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}";
                    return result;
            }
        }

        /// <summary>
        /// Result class for filing data operations
        /// </summary>
        public class FilingDataResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? DebugInfo { get; set; }
            public int StatusCode { get; set; } = 500;
            public XmlDocument? XmlResponse { get; set; }
        }
    }
}
