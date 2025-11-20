using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders the meta information required for the file manager
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FileManagerStartupInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            try
            {
                var baseImagePath = $"/dataserviceassets/{projectVars.projectId}/images";
                var baseImagePlaceholderPath = "/dataserviceassets/{siteid}/images";
                var baseDownloadsPath = $"/dataserviceassets/{projectVars.projectId}/downloads";
                var baseDownloadsPlaceholderPath = "/dataserviceassets/{siteid}/downloads";

                if (projectVars.outputChannelType == "website")
                {
                    var resultFileStructureDefinitionXpath = RetrieveFileStructureXpath(projectVars.editorId);
                    if (!resultFileStructureDefinitionXpath.Success)
                    {
                        throw new Exception(resultFileStructureDefinitionXpath.Message);
                    }


                    var nodeWebImagesLocation = xmlApplicationConfiguration.SelectSingleNode($"{resultFileStructureDefinitionXpath.Payload}[@content='images']");
                    if (nodeWebImagesLocation != null)
                    {
                        var basePath = nodeWebImagesLocation.GetAttribute("name");
                        if (!string.IsNullOrEmpty(basePath))
                        {
                            baseImagePlaceholderPath = Extensions.RenderWebsiteImagePlaceholderPath(basePath);
                        }
                    }

                    var nodeWebDownloadsLocation = xmlApplicationConfiguration.SelectSingleNode($"{resultFileStructureDefinitionXpath.Payload}[@content='downloads']");
                    if (nodeWebDownloadsLocation != null)
                    {
                        var basePath = nodeWebDownloadsLocation.GetAttribute("name");
                        if (!string.IsNullOrEmpty(basePath))
                        {
                            baseDownloadsPlaceholderPath = Extensions.RenderWebsiteDownloadPlaceholderPath(basePath.Replace("[language]", projectVars.outputChannelVariantLanguage));
                        }
                    }
                }

                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully retrieved images and downloads base path";
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = $"projectid: {projectVars.projectId}";
                }
                jsonData.result.images = new ExpandoObject();
                jsonData.result.images.basepath = baseImagePath;
                jsonData.result.images.placeholderpath = baseImagePlaceholderPath;
                jsonData.result.downloads = new ExpandoObject();
                jsonData.result.downloads.basepath = baseDownloadsPath;
                jsonData.result.downloads.placeholderpath = baseDownloadsPlaceholderPath;

                // Convert to JSON and return it to the client
                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
            catch (Exception ex)
            {
                HandleError("Error while retrieving file manager paths", $"error: {ex}");
            }

        }



        /// <summary>
        /// Streams images from the Taxxor Project Data store to show as thumbnail in the image manager
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ImageManagerRetrieveImage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            try
            {
                // Retrieve posted values
                var imagePath = request.RetrievePostedValue("path", @"^([a-zA-Z]?\:)?(\/|[a-zA-Z_\-0-9\[\]\}\{])([\w\./\s\-\]\}\{\(\)])*$", true, reqVars.returnType);

                // Retrieve the project ID from the cookie set by JS
                var fileManagerInformationRaw = request.Cookies["filemanagerdata"] ?? "";
                if (string.IsNullOrEmpty(fileManagerInformationRaw))
                {
                    appLogger.LogError("Could not retrieve file manager information, filemanager cookie not found");
                    await StreamErrorImage(response);
                    return;
                }

                var fileManagerInformation = fileManagerInformationRaw.Split('|');
                projectVars.projectId = fileManagerInformation[0];

                var hasAccess = await UserHasAccessToProject(projectVars.currentUser.Id, projectVars.projectId);
                if (!hasAccess)
                {
                    appLogger.LogError($"Access denied: User does not have access to this asset - {Path.GetFileName(imagePath)}");
                    await StreamErrorImage(response);
                    return;
                }

                var assetPathToRetrieve = $"/images{imagePath}";
                var dataToPost = new Dictionary<string, string>
        {
            { "pid", projectVars.projectId },
            { "relativeto", "cmscontentroot" },
            { "thumbnailsize", ThumbnailMaxSize.ToString() },
            { "path", assetPathToRetrieve }
        };

                var apiUrl = QueryHelpers.AddQueryString(
                    GetServiceUrlByMethodId(ConnectedServiceEnum.DocumentStore, "taxxoreditorfilingimage"),
                    dataToPost);

                // Use memory-efficient streaming approach
                await StreamDocumentStoreImageDirect(response, apiUrl);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in ImageManagerRetrieveImage: {ex.Message}");
                await StreamErrorImage(response);
            }
        }

        /// <summary>
        /// Stream directly from document store to response
        /// </summary>
        /// <param name="response"></param>
        /// <param name="apiUrl"></param>
        /// <returns></returns>
        private static async Task StreamDocumentStoreImageDirect(HttpResponse response, string apiUrl)
        {
            try
            {
                // Create HTTP client if not exists (this is thread-safe)
                _httpBinaryClient ??= CreateSimpleHttp2Client(null); // Don't set base URL here

                // Use ResponseHeadersRead to start processing as soon as headers arrive
                using var httpResponse = await _httpBinaryClient.GetAsync(
                    apiUrl,
                    HttpCompletionOption.ResponseHeadersRead);

                // Set content type based on the response from document store
                response.ContentType = httpResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

                if (!httpResponse.IsSuccessStatusCode)
                {
                    await StreamErrorImage(response);
                    return;
                }

                // Stream directly from source to response
                using var responseStream = await httpResponse.Content.ReadAsStreamAsync();
                await responseStream.CopyToAsync(response.Body);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error streaming image from document store: {ex.Message}");
                await StreamErrorImage(response);
            }
        }

        /// <summary>
        /// error image streaming method
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        ///
        private static async Task StreamErrorImage(HttpResponse response)
        {
            response.ContentType = GetContentType("jpg");

            // Use pooled buffer to avoid large allocations if called repeatedly
            if (BrokenImageBytes != null && BrokenImageBytes.Length > 0)
            {
                await response.Body.WriteAsync(BrokenImageBytes, 0, BrokenImageBytes.Length);
            }
            else
            {
                // Fallback text response if image is not available
                var errorMessage = System.Text.Encoding.UTF8.GetBytes("Image not available");
                await response.Body.WriteAsync(errorMessage, 0, errorMessage.Length);
            }
        }


        /// <summary>
        /// Sync fusion file manager operations
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FileManagerOperations(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var rebuildCache = false;

            // Retrieve information for the routine set by the cookie set using JS
            var fileManagerInformationRaw = request.Cookies["filemanagerdata"] ?? "";
            if (string.IsNullOrEmpty(fileManagerInformationRaw)) HandleError("Could not retrieve file manager information", $"");
            var fileManagerInformation = fileManagerInformationRaw.Split('|');

            projectVars.projectId = fileManagerInformation[0];
            var assetType = fileManagerInformation[1];
            var processingInstruction = (fileManagerInformation.Length > 2) ? fileManagerInformation[2] : "";
            var fixedFileName = (fileManagerInformation.Length > 3) ? fileManagerInformation[3] : "";
            if (debugRoutine)
            {
                Console.WriteLine("----- File Manager Information -----");
                Console.WriteLine($"projectId: {projectVars.projectId}");
                Console.WriteLine($"assetType: {assetType}");
                Console.WriteLine($"processingInstruction: {processingInstruction}");
                Console.WriteLine($"fixedFileName: {fixedFileName}");
                Console.WriteLine("------------------------------------");
            }

            var modifiedFileExtensions = new List<string>();

            if (string.IsNullOrEmpty(projectVars.projectId) || string.IsNullOrEmpty(assetType)) HandleError("Not enough information to continue", $"projectVars.projectId: {projectVars.projectId ?? ""}, assetType: {assetType ?? ""}");

            try
            {
                // Will contain posted JSON data
                string? postedJsonData = "";


                // Test if we have posted binary data to upload in the project data service
                if (request.HasFormContentType && request.Form.Files.Count > 0)
                {
                    rebuildCache = true;

                    if (debugRoutine)
                    {
                        Console.WriteLine("!!!!!!!!!!!! Dealing with file upload !!!!!!!!!!!!!!!");
                        await TextFileCreateAsync(postedJsonData, logRootPathOs + "/uploaddata.txt");
                    }

                    var uploadedAssestsBasePath = "";
                    var fileManagerAction = "";
                    postedJsonData = "";

                    foreach (var formField in request.Form)
                    {
                        // Form data 
                        var formFieldName = formField.Key;
                        var formFieldValue = formField.Value;

                        if (debugRoutine)
                        {
                            Console.WriteLine($"- formFieldName: {formFieldName}");
                            Console.WriteLine($"- formFieldValue: {formFieldValue}");
                        }


                        switch (formFieldName)
                        {
                            case "path":
                                uploadedAssestsBasePath = formFieldValue;
                                break;

                            case "action":
                                fileManagerAction = formFieldValue;
                                break;

                            case "data":
                                postedJsonData = formFieldValue;
                                break;

                            default:
                                appLogger.LogWarning($"Unknown form field {formFieldName} with value {formFieldValue} encountered");
                                break;

                        }

                    }

                    if (string.IsNullOrEmpty(uploadedAssestsBasePath) || string.IsNullOrEmpty(fileManagerAction) || string.IsNullOrEmpty(postedJsonData))
                    {
                        HandleError("Not enough data posted to continue", $"uploadedAssestsBasePath: {uploadedAssestsBasePath ?? ""}, fileManagerAction: {fileManagerAction ?? ""}, postedJsonData: {postedJsonData ?? ""}");
                    }

                    // Temporary folder location where we will store the files which have been uploaded via the web client
                    var tempFolderPath = $"temp/upload_{projectVars.projectId}-{RandomString(24)}";
                    var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/{tempFolderPath}";
                    Directory.CreateDirectory(tempFolderPathOs);

                    var savedFiles = new List<string>();

                    switch (fileManagerAction)
                    {
                        case "save":
                            // Loop through the posted binary files and save them in the temporary location
                            foreach (IFormFile postedBinaryFile in request.Form.Files)
                            {
                                var uploadedMimeType = postedBinaryFile.ContentType;
                                var uploadedAssetType = GetAssetType(uploadedMimeType);
                                if (string.IsNullOrEmpty(uploadedAssetType))
                                {
                                    appLogger.LogError($"Could not determine the file type of the uploaded binary mime-type: {uploadedMimeType}");
                                }
                                else if (uploadedAssetType == "executable")
                                {
                                    appLogger.LogError($"mime-type: {uploadedMimeType} was found to be a {uploadedAssetType} which is not allowed to be uploaded");
                                }
                                else
                                {
                                    // Make sure that the correct type of file is uploaded if we are dealing with the image of download manager
                                    if ((assetType == "projectimages" && uploadedAssetType != "image") || (assetType == "projectdownloads" && uploadedAssetType != "download"))
                                    {
                                        appLogger.LogError($"uploaded file with type {uploadedAssetType} not allowed to be uploaded in {assetType} library");
                                    }
                                    else
                                    {

                                        var fileNameToSave = "";
                                        switch (processingInstruction)
                                        {
                                            case "fixedpath":
                                                fileNameToSave = fixedFileName;
                                                break;

                                            default:
                                                var fileNameRaw = postedBinaryFile.FileName;

                                                // Normalize the filename so that they are web-friendly
                                                fileNameToSave = NormalizeFileName(fileNameRaw);

                                                // Log in case if we had to correct the filename
                                                if (fileNameRaw != fileNameToSave) appLogger.LogDebug($"Renamed {fileNameRaw} to normalized asset name {fileNameToSave}");

                                                break;
                                        }

                                        var passedValidation = false;
                                        var typeOfFile = "unknown";

                                        // Storing the file in the temporary folder
                                        var targetFilePathOs = $"{tempFolderPathOs}/{fileNameToSave}";
                                        using (FileStream output = System.IO.File.Create(targetFilePathOs))
                                        {
                                            await postedBinaryFile.CopyToAsync(output);
                                        }

                                        // Test if the binary complies to the definition of the asset type
                                        switch (assetType)
                                        {
                                            case "projectimages":
                                                var binaryInspectedType = FileInspector.TryGetExtension(targetFilePathOs);
                                                if (!string.IsNullOrEmpty(binaryInspectedType))
                                                {
                                                    // Check the extension in the XML
                                                    typeOfFile = GetAssetType(binaryInspectedType);
                                                    if (debugRoutine) Console.WriteLine($"---> typeOfFile: {typeOfFile} <---");
                                                    passedValidation = (typeOfFile == "image");
                                                }
                                                break;

                                            default:
                                            case "projectdownloads":
                                                passedValidation = true;
                                                appLogger.LogInformation("Skipping binary check for project downloads");
                                                break;
                                        }

                                        // Handle validation of the uploaded file
                                        if (passedValidation)
                                        {
                                            savedFiles.Add(fileNameToSave);
                                        }
                                        else
                                        {
                                            File.Delete(targetFilePathOs);
                                            HandleError("Unsupported file type", $"typeOfFile: {typeOfFile}, uploadedAssetType: {uploadedAssetType}");
                                        }


                                        if (debugRoutine)
                                        {
                                            Console.WriteLine($"* Stored uploaded binary in {tempFolderPathOs}/{fileNameToSave}");
                                        }

                                        // Update the global list so that we can figure out the type of library update we need to perform
                                        if (!modifiedFileExtensions.Contains(Path.GetExtension(targetFilePathOs))) modifiedFileExtensions.Add(Path.GetExtension(targetFilePathOs));
                                    }
                                }
                            }

                            break;

                        default:
                            HandleError("Unknown file manager action", $"fileManagerAction: {fileManagerAction}");

                            break;

                    }

                    if (savedFiles.Count == 0)
                    {
                        // Create a sync fusion error message
                        dynamic jsonData = new ExpandoObject();
                        jsonData.Error = new ExpandoObject();

                        jsonData.Error.Message = "Unable to upload files";
                        jsonData.Error.Code = "500";

                        var json = (string)ConvertToJson(jsonData);

                        await response.Error(json, ReturnTypeEnum.Json);
                    }
                    else
                    {
                        // Post details about the uploaded files and where we have temporary stored them to the Project Data Store
                        var dataToPost = new Dictionary<string, string>();
                        dataToPost.Add("pid", projectVars.projectId);
                        dataToPost.Add("assettype", assetType);
                        dataToPost.Add("basepath", uploadedAssestsBasePath);
                        dataToPost.Add("tempfolder", tempFolderPath);
                        dataToPost.Add("tempfiles", string.Join(",", savedFiles.ToArray()));
                        dataToPost.Add("jsondata", postedJsonData);
                        XmlDocument xmlFileManagerResponse = await CallTaxxorDataService<XmlDocument>(RequestMethodEnum.Put, "filemanagerapi", dataToPost, true);

                        if (XmlContainsError(xmlFileManagerResponse)) HandleError(xmlFileManagerResponse);

                        // Sync fusion expects an empty string upon successful upload of files...
                        await response.OK("", ReturnTypeEnum.Json, true);
                    }

                }
                else
                {

                    // Retrieve the posted JSON
                    // var buffer = new byte[Convert.ToInt32(context.Request.ContentLength)];
                    // await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
                    // postedJsonData = Encoding.UTF8.GetString(buffer);

                    postedJsonData = await context.Request.RetrieveRawBodyStringAsync();

                    // Retrieve information of the assets that have changed so that we can update the library

                    if (!IsValidJson(postedJsonData))
                    {
                        appLogger.LogError("Invalid JSON format in file manager request:");
                        appLogger.LogError(postedJsonData);
                        HandleError(ReturnTypeEnum.Json, "Invalid JSON format", "Invalid JSON format in file manager request");
                    }

                    // Console.WriteLine("### FILE MANAGER JSON ORIG ###");
                    // Console.WriteLine(postedJsonData);
                    // Console.WriteLine("##############################");

                    if (!postedJsonData.StartsWith("{\"action\":\"read\""))
                    {
                        rebuildCache = true;

                        var xmlPostedData = new XmlDocument();

                        try
                        {
                            xmlPostedData = ConvertJsonToXml(postedJsonData, "data");
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError($"Error in file manager");
                            appLogger.LogError($"Error converting JSON to XML: {ex.Message}");
                            appLogger.LogError($"JSON received: {postedJsonData}");
                            Console.WriteLine(postedJsonData);
                            throw;
                        }

                        // Console.WriteLine("+++++++++++++++");
                        // Console.WriteLine(PrettyPrintXml(xmlPostedData));
                        // Console.WriteLine("+++++++++++++++");

                        var fileName = xmlPostedData?.SelectSingleNode("/data/names")?.InnerText ?? xmlPostedData?.SelectSingleNode("/data/name")?.InnerText ?? "";

                        if (!string.IsNullOrEmpty(fileName))
                        {
                            // Update the global list so that we can figure out the type of library update we need to perform
                            if (!modifiedFileExtensions.Contains(Path.GetExtension(fileName))) modifiedFileExtensions.Add(Path.GetExtension(fileName));
                        }

                        //
                        // => If we are creating a folder, then we need to make sure it adheres to the naming convention
                        //
                        var action = xmlPostedData.SelectSingleNode("/data/action")?.InnerText ?? "";
                        if (action == "create")
                        {
                            var folderName = fileName;
                            var folderNameNormalized = NormalizeFileNameWithoutExtension(folderName);
                            if (folderName != folderNameNormalized)
                            {
                                // Adjust the JSON that we will send to the server to make sure that only folders with a normalized name can be created
                                var jsonData = JObject.Parse(postedJsonData);
                                JToken? tokenFolderName = jsonData.SelectToken("names") ?? jsonData.SelectToken("name");
                                if (tokenFolderName != null)
                                {
                                    // var currentFolderName = tokenFolderName.Value<string>();
                                    // Console.WriteLine($"** currentFolderName: {currentFolderName} **");

                                    var jpropertyFolderName = tokenFolderName.Parent as JProperty;
                                    jpropertyFolderName.Value = folderNameNormalized;

                                    // Convert the adjusted object back to JSON again
                                    postedJsonData = jsonData.ToString(Formatting.Indented);
                                }
                            }
                        }
                    }


                    // Console.WriteLine("### FILE MANAGER JSON REPLACED ###");
                    // Console.WriteLine(postedJsonData);
                    // Console.WriteLine("##################################");

                    // Send this data to the Project Data Store
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "pid", projectVars.projectId },
                        { "assettype", assetType },
                        { "jsondata", postedJsonData }
                    };

                    // Request the data from the Project Data Store
                    XmlDocument xmlFileManagerResponse = await CallTaxxorDataService<XmlDocument>(RequestMethodEnum.Post, "filemanagerapi", dataToPost, true);

                    if (XmlContainsError(xmlFileManagerResponse)) HandleError(xmlFileManagerResponse);

                    var jsonToReturn = xmlFileManagerResponse.SelectSingleNode("/result/payload").InnerText;
                    // await TextFileCreateAsync(jsonToReturn, logRootPathOs + "/filemanager.orig.json");

                    //
                    // => Do not show the renditions folder and other files/folders that we want to hide for our users
                    //
                    if (assetType == "projectimages")
                    {
                        //Console.WriteLine(PrettyPrintXml(ConvertJsonToXml(xmlFileManagerResponse.SelectSingleNode("/result/payload").InnerText, "data")));

                        // Remove elements from the response
                        var xmlFileManagerData = ConvertJsonToXml(jsonToReturn, "data");

                        // Assure that a files node is always present
                        var nodeListFiles = xmlFileManagerData.SelectNodes($"/data/files");
                        if (nodeListFiles.Count == 0)
                        {
                            // Inject a string that we can use to efficiently find the element and force it to become an empty array in the end result
                            xmlFileManagerData.DocumentElement.AppendChild(xmlFileManagerData.CreateElementWithText("files", "taxxorforcearraytobenulltaxxor"));
                        }

                        // Files should always be an array of objects
                        nodeListFiles = xmlFileManagerData.SelectNodes($"/data/files");
                        foreach (XmlNode nodeFile in nodeListFiles)
                        {
                            var attrForceArray = xmlFileManagerData.CreateAttribute("json", "Array", "http://james.newtonking.com/projects/json");
                            attrForceArray.InnerText = "true";
                            nodeFile.Attributes.Append(attrForceArray);

                            // Change file type of an SVG to force the UI to render an image preview node
                            var nodeType = nodeFile.SelectSingleNode("type");
                            if ((nodeType?.InnerText ?? ".unknown") == ".svg") nodeType.InnerText = ".png";
                        }

                        // await xmlFileManagerData.SaveAsync(logRootPathOs + "/filemanager.converted.xml");

                        // Remove the folders and files that we do not want to show in the file manager
                        var nodeListToRemove = xmlFileManagerData.SelectNodes($"/data/files[name/text()='{ImageRenditionsFolderName}' or contains(name/text(), '.xml') or contains(name/text(), '.txt')]");
                        nodeListToRemove.Remove();

                        // Convert the XML back to JSON
                        jsonToReturn = ConvertToJson(xmlFileManagerData, Newtonsoft.Json.Formatting.None, true, true).Replace("\"taxxorforcearraytobenulltaxxor\"", "");
                        // await TextFileCreateAsync(jsonToReturn, logRootPathOs + "/filemanager.new.json");
                    }

                    // Return the result
                    await response.OK(jsonToReturn, reqVars.returnType, true);
                }

                //
                // => Update the image library if needed
                //
                if (modifiedFileExtensions.Count > 0 && assetType == "projectimages")
                {
                    // Console.WriteLine("*****************");
                    // Console.WriteLine($"modifiedFileExtensions: {string.Join(',', modifiedFileExtensions.ToArray())}");
                    // Console.WriteLine("*****************");
                    if (modifiedFileExtensions.Contains(".svg"))
                    {
                        await UpdateDrawingLibraryRenditions(projectVars.projectId);
                    }

                    if (modifiedFileExtensions.Contains(".jpeg") || modifiedFileExtensions.Contains(".jpg") || modifiedFileExtensions.Contains(".png") || modifiedFileExtensions.Contains(".gif"))
                    {
                        await UpdateImageLibraryRenditions(projectVars.projectId);
                    }

                }

                //
                // => Rebuild the PagedJS cache if needed
                //
                if (rebuildCache && PdfRenderEngine == "pagedjs")
                {
                    ContentCache.Clear();
                }


            }
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "There was an error executing", $"(file manager operation), projectId: {projectVars.projectId}, assetType: {assetType}, processingInstruction: {processingInstruction}, fixedFileName: {fixedFileName}, error: {ex}");
            }

        }


    }
}