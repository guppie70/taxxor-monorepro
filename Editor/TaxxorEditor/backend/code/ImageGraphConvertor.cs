using System;
using System.Buffers;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// Logic for converting images, drawings and graphs
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Routine that will run through the image library and update the image renditions
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task UpdateImageLibaries(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var action = request.RetrievePostedValue("action", @"^(update|rebuild)$", true, reqVars.returnType);
            var librariesRaw = request.RetrievePostedValue("libraries", @"^[\w,]{4,128}$", true, reqVars.returnType);

            var debugInfo = $"action: {action}, libraries: {librariesRaw}";

            var libraries = new List<string>();
            if (!librariesRaw.Contains(","))
            {
                libraries.Add(librariesRaw);
            }
            else
            {
                libraries = librariesRaw.Split(",").ToList();
            }

            Dictionary<string, string>? dataToPost = null;

            var overallResult = new StringBuilder();

            try
            {

                //
                // => Remove the renditions from the server
                //                
                var removeRenditionsResult = new TaxxorLogReturnMessage();
                removeRenditionsResult.Success = true;
                if (action == "rebuild")
                {
                    foreach (var library in libraries)
                    {
                        var rootFolderName = "";
                        switch (library)
                        {
                            case "image":
                                rootFolderName = "images";
                                break;

                            case "drawing":
                                rootFolderName = "drawings";
                                break;

                            case "graph":
                                rootFolderName = "graphs";
                                break;

                            default:
                                removeRenditionsResult.Success = false;
                                removeRenditionsResult.ErrorLog.Add($"No support for {library}");
                                break;
                        }

                        var renditionsDirectoryPath = $"/{ImageRenditionsFolderName}/{rootFolderName}";

                        dataToPost = new Dictionary<string, string>
                        {
                            { "pid", projectVars.projectId },
                            { "directorypath", renditionsDirectoryPath },
                            { "relativeto", "projectimages" },
                            { "leaverootfolder", "true" }
                        };

                        var xmlDelTreeResult = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditordeltree", dataToPost, true);
                        appLogger.LogInformation(xmlDelTreeResult.OuterXml);

                        if (XmlContainsError(xmlDelTreeResult))
                        {
                            removeRenditionsResult.Success = false;
                            removeRenditionsResult.ErrorLog.Add($"{xmlDelTreeResult.SelectSingleNode("//message")?.InnerXml ?? "No message"}, details: {xmlDelTreeResult.SelectSingleNode("//debuginfo")?.InnerXml ?? "No debug info"}");
                        }
                        else
                        {
                            appLogger.LogInformation($"Successfully removed renditions directory {renditionsDirectoryPath}");
                        }
                    }



                }

                //
                // => Error handling for renditions removal part
                //
                if (!removeRenditionsResult.Success)
                {
                    overallResult.AppendLine("* Cleanup renditions *");
                    ProcessTaxxorLogResult(removeRenditionsResult);
                    //construct a response message for the client
                    dynamic jsonErrorData = new ExpandoObject();
                    jsonErrorData.error = new ExpandoObject();
                    jsonErrorData.error.message = "Successfully updated the image library";
                    if (isDevelopmentEnvironment)
                    {
                        jsonErrorData.error.debuginfo = debugInfo;
                    }
                    jsonErrorData.error.log = overallResult.ToString();

                    string jsonErrorToReturn = JsonConvert.SerializeObject(jsonErrorData, Newtonsoft.Json.Formatting.Indented);


                    await response.Error(jsonErrorToReturn, ReturnTypeEnum.Json, true);
                }

                //
                // => Update the libraries
                //
                if (removeRenditionsResult.Success)
                {
                    foreach (var library in libraries)
                    {
                        switch (library)
                        {
                            case "image":
                                overallResult.AppendLine("* Update image renditions *");
                                var imageRenditionsResult = await GenerateImageRenditions(projectVars.projectId);
                                ProcessTaxxorLogResult(imageRenditionsResult);
                                break;

                            case "drawing":
                                overallResult.AppendLine("* Update drawing renditions *");
                                var drawingRenditionsResult = await GenerateDrawingRenditions(reqVars, projectVars);
                                ProcessTaxxorLogResult(drawingRenditionsResult);
                                break;

                            case "graph":
                                overallResult.AppendLine("* Update graph renditions *");
                                var graphsRenditionsResult = await GenerateGraphRenditions(reqVars, projectVars);
                                ProcessTaxxorLogResult(graphsRenditionsResult);
                                break;

                            default:
                                overallResult.AppendLine($"WARNING: No support for {library}");
                                break;
                        }
                    }


                    //construct a response message for the client
                    dynamic jsonData = new ExpandoObject();
                    jsonData.result = new ExpandoObject();
                    jsonData.result.message = "Successfully updated the image library";
                    if (isDevelopmentEnvironment)
                    {
                        jsonData.result.debuginfo = debugInfo;
                    }
                    jsonData.result.log = overallResult.ToString();

                    string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);


                    await response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = "There was an error updating the image libraries";
                appLogger.LogError(ex, errorMessage);
                HandleError(errorMessage, debugInfo);
            }


            void ProcessTaxxorLogResult(TaxxorLogReturnMessage message)
            {
                if (message.Success)
                {
                    overallResult.AppendLine("Successfully completed");
                }
                else
                {
                    overallResult.AppendLine("Failed");
                }

                if (message.InformationLog.Count > 0) overallResult.AppendLine($"informationLog: {string.Join(',', message.InformationLog.ToArray())}");
                if (message.SuccessLog.Count > 0) overallResult.AppendLine($"successLog: {string.Join(',', message.SuccessLog.ToArray())}");
                if (message.WarningLog.Count > 0) overallResult.AppendLine($"warningLog: {string.Join(',', message.WarningLog.ToArray())}");
                if (message.ErrorLog.Count > 0) overallResult.AppendLine($"errorLog: {string.Join(',', message.ErrorLog.ToArray())}");
                overallResult.AppendLine("-----------------------------------");
                overallResult.AppendLine("");
            }

        }

        /// <summary>
        /// Silently updates the drawing renditions in the image library
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static async Task UpdateDrawingLibraryRenditions(string projectId)
        {
            try
            {
                var imageRenditionsResult = await GenerateDrawingRenditions(projectId);
                var resultForDebugging = "";
                if (imageRenditionsResult.SuccessLog.Count > 0) resultForDebugging = $"successLog: {string.Join(',', imageRenditionsResult.SuccessLog.ToArray())}";
                if (imageRenditionsResult.WarningLog.Count > 0) resultForDebugging += $"\nwarningLog: {string.Join(',', imageRenditionsResult.WarningLog.ToArray())}";
                if (imageRenditionsResult.ErrorLog.Count > 0) resultForDebugging += $"\nerrorLog: {string.Join(',', imageRenditionsResult.ErrorLog.ToArray())}";

                if (!imageRenditionsResult.Success)
                {
                    appLogger.LogError($"There was an error updating the drawing library for projectId: {projectId}.\nMessage: {imageRenditionsResult.Message}\nDebugInfo: {imageRenditionsResult.DebugInfo}");
                    appLogger.LogError(resultForDebugging);
                }
                else
                {

                    if (resultForDebugging != "") appLogger.LogInformation(resultForDebugging);
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was a problem updating the drawing library");
            }
        }


        /// <summary>
        /// Generates alternative renderings for SVG files used in a project without a valid context
        /// (from a background service for instance)
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static async Task<TaxxorLogReturnMessage> GenerateDrawingRenditions(string projectId)
        {

            var context = System.Web.Context.Current;
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Exit routine if project ID was not passed
            if (string.IsNullOrEmpty(projectId)) return new TaxxorLogReturnMessage(false, "Unable to generate image renditions", "projectId is null or empty");

            try
            {
                // Initiate a custom context if a "generic" context is not available
                if (context == null)
                {
                    var contextAccessor = CreateCustomHttpContext();
                    context = contextAccessor.HttpContext;
                }


                // Make sure we have a project variables object
                ProjectVariables? projectVars = null;
                if (!ProjectVariablesExistInContext(context))
                {
                    projectVars = new ProjectVariables();
                    projectVars.projectId = projectId;
                    projectVars = await FillProjectVariablesFromProjectId(projectVars);
                    projectVars.currentUser.Id = SystemUser;


                }
                else
                {
                    projectVars = RetrieveProjectVariables(context);
                    projectVars.projectId = projectId;
                }

                // Store the updated project variables in the context
                SetProjectVariables(context, projectVars);


                // Make sure we have a request variables object
                RequestVariables? reqVars = null;
                if (!RequestVariablesExistInContext(context))
                {
                    reqVars = new RequestVariables
                    {
                        returnType = ReturnTypeEnum.Txt
                    };
                    SetRequestVariables(context, reqVars);
                }
                else
                {
                    reqVars = RetrieveRequestVariables(context);
                }

                // Start the update drawing renditions process
                return await GenerateDrawingRenditions(reqVars, projectVars);
            }
            catch (Exception ex)
            {
                return new TaxxorLogReturnMessage(false, "There was an error generating the image renditions", ex);
            }
        }

        /// <summary>
        /// Generates alternative renderings for SVG files used in a project
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static async Task<TaxxorLogReturnMessage> GenerateDrawingRenditions(RequestVariables reqVars, ProjectVariables projectVars)
        {
            var context = System.Web.Context.Current;
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var projectId = projectVars.projectId;

            var contentPath = "/_contents.xml";

            // Exit routine if project ID was not passed
            if (string.IsNullOrEmpty(projectId)) return new TaxxorLogReturnMessage(false, "Unable to generate image renditions", "projectId is null or empty");

            try
            {
                var imagesOrGraphsHaveChanged = false;
                var dataToPost = new Dictionary<string, string>();
                var useRandomOutputFolder = (siteType != "local");

                // Logs
                var successLog = new List<string>();
                var warningLog = new List<string>();
                var errorLog = new List<string>();

                // Operations list
                var drawingsAdded = new List<string>();
                var drawingsChanged = new List<string>();
                var drawingsRenamed = new Dictionary<string, string>();
                var drawingsRemoved = new List<string>();
                var invalidDrawingRenditions = new List<string>();
                var orphanedDrawingRenditions = new List<string>();

                //
                // => Retrieve the images folder contents stored in the Project Data Store
                //
                XmlDocument? xmlCachedContent = await DocumentStoreService.FilingData.Load<XmlDocument>(projectId, contentPath, "projectimages", debugRoutine, false);
                if (XmlContainsError(xmlCachedContent))
                {
                    xmlCachedContent = null;
                }

                //
                // => Retrieve the current state of the image library content
                //
                var retrieveCurrentImageLibraryContent = await RetrieveAssetLibraryContent(projectId);
                if (!retrieveCurrentImageLibraryContent.Success) return new TaxxorLogReturnMessage(retrieveCurrentImageLibraryContent);

                var xmlCurrentContent = new XmlDocument();
                xmlCurrentContent.ReplaceContent(retrieveCurrentImageLibraryContent.XmlPayload);

                // Remember the original images and graphs content of the library
                var xmlCurrentContentOriginal = new XmlDocument();
                xmlCurrentContentOriginal.ReplaceContent(xmlCurrentContent);


                //
                // => Determine the changes that have occurred between now and the previous time we checked
                //
                var xPathImages = $"//file[contains(@name, '.svg') and not(contains(@path, '{ImageRenditionsFolderName}'))]";
                var assetLibraryDelta = CalculateLibraryDifferences(xPathImages, xmlCachedContent, xmlCurrentContent, ImageRenditionsFolderName);

                //
                // => Fill the local variables with the delta calculated
                //
                drawingsAdded.AddRange(assetLibraryDelta.Added);
                drawingsChanged.AddRange(assetLibraryDelta.Changed);
                drawingsRenamed.AddRange(assetLibraryDelta.Renamed);
                drawingsRemoved.AddRange(assetLibraryDelta.Removed);
                invalidDrawingRenditions.AddRange(assetLibraryDelta.InvalidRenditions);
                orphanedDrawingRenditions.AddRange(assetLibraryDelta.OrphanedRenditions);

                var renditionsToGenerate = new List<string>();
                var renditionsToRemove = new List<string>();


                //
                // => Render new image renditions
                //

                // - all the renditions we need to generate are a combination of new images and invalid image renditions
                renditionsToGenerate = drawingsAdded.Union(invalidDrawingRenditions).ToList<string>();
                if (renditionsToGenerate.Count > 0)
                {

                    //
                    // => Setup a temporary directory on the shared folder that we can exchange files on
                    //
                    var tempFolderName = (useRandomOutputFolder) ? $"drawingconversion_{projectId}{CreateTimeBasedString("millisecond")}{RandomString(8, false)}" : $"drawingconversion_{projectId}";
                    var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/{tempFolderName}";
                    var outputFolderPathOs = $"{tempFolderPathOs}/images";


                    try
                    {
                        if (Directory.Exists(tempFolderPathOs))
                        {
                            EmptyDirectory(tempFolderPathOs);
                        }
                        else
                        {
                            Directory.CreateDirectory(tempFolderPathOs);
                        }
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorLogReturnMessage(false, "Unable to create temporary folder on shared location", ex);
                    }

                    // Create the folder where we want to capture the converted images
                    try
                    {
                        Directory.CreateDirectory(outputFolderPathOs);
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorLogReturnMessage(false, "Unable to create temporary folder on shared location", ex);
                    }

                    // Render an XML document that contains the SVG assets as links
                    var xmlDocument = RetrieveTemplate("inline-editor-content");
                    var nodeRoot = xmlDocument.SelectSingleNode("/data/content/article//section");
                    var baseUri = $"/dataserviceassets/{projectId}/images";

                    // Fill a temporary list
                    // var tempRenditionsToGenerate = new List<string>();
                    // for (var i = 0; i < 3; i++)
                    // {
                    //     tempRenditionsToGenerate.Add(renditionsToGenerate[i]);
                    // }


                    foreach (var renditionToGenerate in renditionsToGenerate)
                    {
                        var fullUri = baseUri + renditionToGenerate;
                        var identifier = NormalizeFileName(renditionToGenerate.Replace("/", "-")).SubstringBefore(".svg").SubstringAfter("-");
                        var nodeWrapper = xmlDocument.CreateElement("div");
                        nodeWrapper.SetAttribute("class", "illustration");
                        nodeWrapper.SetAttribute("id", identifier);
                        nodeWrapper.SetAttribute("component", identifier);
                        nodeWrapper.SetAttribute("data-assetnameconvert", renditionToGenerate.Replace(".svg", ".png"));
                        nodeWrapper.SetAttribute("data-assetnameuse", renditionToGenerate.Replace(".svg", ".jpg"));
                        var nodeObject = xmlDocument.CreateElement("object");
                        nodeObject.SetAttribute("type", "image/svg+xml");
                        nodeObject.SetAttribute("data", fullUri);
                        nodeWrapper.AppendChild(nodeObject);
                        nodeRoot.AppendChild(nodeWrapper);
                    }
                    // await xmlDocument.SaveAsync(logRootPathOs + "/drawingsutil.xml");

                    //
                    // => Generate the utility HTML file that contains all the graphs and visuals
                    //
                    var webAssetsRootPath = $"/outputchannels/{TaxxorClientId}/pdf";

                    var utilityFilePathOs = $"{tempFolderPathOs}/_visuals-utility.html";
                    var graphMagnificationFactor = 2;

                    // Grab the styles that we need to insert
                    var xsltPathOs = CalculateFullPathOs("generate-drawings-utility");
                    var inlineCss = "";
                    var inlineJs = "";

                    // Test if we need to render hard coded visuals
                    // var renderHardCodedDrawings = Extensions.RenderHardCodedDrawings(projectId);
                    var renderHardCodedDrawings = false;

                    var tokenExpire = (siteType == "local" || siteType == "dev") ? 3600 : 3600;
                    var accessToken = Taxxor.Project.ProjectLogic.AccessToken.GenerateToken(tokenExpire);
                    // appLogger.LogInformation($"- accessToken: {accessToken}");

                    // Hack to retrieve the correct client side assets
                    projectVars.outputChannelType = "pdf";
                    projectVars.editorId = RetrieveEditorIdFromProjectId(projectId);
                    projectVars.outputChannelVariantId = RetrieveFirstOutputChannelVariantIdFromEditorId(projectVars.editorId);
                    projectVars.outputChannelVariantLanguage = projectVars.outputChannelDefaultLanguage;
                    projectVars.reportTypeId = RetrieveReportTypeIdFromProjectId(projectId);

                    var xmlClientAssets = new XmlDocument();
                    xmlClientAssets.AppendChild(xmlClientAssets.CreateElement("assets"));

                    var nodeTaxxorEditorUri = xmlApplicationConfiguration.SelectSingleNode("/configuration/taxxor/components/service/web-applications/web-application[@id='taxxoreditor']/uri");
                    var protocol = (nodeTaxxorEditorUri == null) ? "http" : nodeTaxxorEditorUri.InnerText.SubstringBefore("://");
                    var currentWebAddressIp = CalculateFullPathOs("ownaddress-ip");
                    if (!currentWebAddressIp.StartsWith("http"))
                    {
                        currentWebAddressIp = protocol + currentWebAddressIp;
                    }

                    var currentWebAddressDomain = CalculateFullPathOs("ownaddress-domain");
                    if (!currentWebAddressDomain.StartsWith("http"))
                    {
                        currentWebAddressDomain = protocol + currentWebAddressDomain;
                    }

                    XsltArgumentList xsltArgumentList = new XsltArgumentList();
                    xsltArgumentList.AddParam("inline-css", "", inlineCss);
                    xsltArgumentList.AddParam("inline-js", "", inlineJs);
                    xsltArgumentList.AddParam("rootfolder", "", webAssetsRootPath);
                    xsltArgumentList.AddParam("baseurl", "", currentWebAddressDomain);
                    xsltArgumentList.AddParam("token", "", accessToken);
                    xsltArgumentList.AddParam("mode", "", "graphs");
                    xsltArgumentList.AddParam("addhardcodedvisuals", "", ((renderHardCodedDrawings) ? "true" : "false"));
                    xsltArgumentList.AddParam("lang", "", projectVars?.outputChannelVariantLanguage ?? "en");
                    // xsltArgumentList.AddParam("graph-magnification", "", );

                    xsltArgumentList.AddParam("clientassets", "", xmlClientAssets.DocumentElement);

                    var htmlDrawingsUtility = TransformXmlToDocument(xmlDocument, xsltPathOs, xsltArgumentList);

                    if (XmlContainsError(htmlDrawingsUtility))
                    {
                        return new TaxxorLogReturnMessage(htmlDrawingsUtility);
                    }

                    // Store the utiliy file on the shared drive
                    await htmlDrawingsUtility.SaveAsync(utilityFilePathOs);


                    //
                    // => Call the Pandoc service to convert the drawings into binary image files
                    //

                    // Potentially correct the paths
                    var utilityFilePathOsPandoc = utilityFilePathOs;
                    var outputFolderPathOsPandoc = outputFolderPathOs;
                    if (siteType == "local")
                    {
                        // We have used full paths to the disk but the conversion service runs in a docker, so we need to correct the path
                        utilityFilePathOsPandoc = ConvertToDockerPath(utilityFilePathOs);
                        outputFolderPathOsPandoc = ConvertToDockerPath(outputFolderPathOs);
                    }

                    // Call the conversion service
                    var visualsConversionResponse = await ConversionService.RenderDrawingBinariesFromUtilityPage(utilityFilePathOsPandoc, outputFolderPathOsPandoc, graphMagnificationFactor, false, false);
                    // appLogger.LogInformation(visualsConversionResponse.OuterXml);

                    if (XmlContainsError(visualsConversionResponse))
                    {
                        errorLog.Add($"{(visualsConversionResponse.SelectSingleNode("//message")?.InnerText ?? "")} - {(visualsConversionResponse.SelectSingleNode("//debuginfo")?.InnerText ?? "")}, projectId: {projectId}");
                        return new TaxxorLogReturnMessage(visualsConversionResponse, successLog, warningLog, errorLog);
                    }
                    else
                    {
                        var successConversionMessage = visualsConversionResponse.SelectSingleNode("//message")?.InnerText ?? "";
                        if (!string.IsNullOrEmpty(successConversionMessage))
                        {
                            successLog.Add($"{successConversionMessage} for projectId: '{projectId}'");
                        }
                    }

                    //
                    // => Generate the thumbnails
                    //
                    var thumbnailsCreated = 0;
                    string[] pngFiles = Directory.GetFiles(outputFolderPathOs, "*.jpg", SearchOption.AllDirectories);
                    foreach (string jpgFilePathOs in pngFiles)
                    {
                        var thumbnailPathOs = "";
                        try
                        {
                            var thumbnailImageFilename = ThumbnailFilenameTemplate.Replace("{filename}", Path.GetFileNameWithoutExtension(jpgFilePathOs)).Replace("{extension}", ThumbnailFileExtension);
                            thumbnailPathOs = jpgFilePathOs.Replace(Path.GetFileName(jpgFilePathOs), thumbnailImageFilename);

                            // Create directory if needed
                            string? directoryName = Path.GetDirectoryName(thumbnailPathOs);
                            if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);
                            
                            // Delete existing file if any
                            if (File.Exists(thumbnailPathOs)) File.Delete(thumbnailPathOs);
                            
                            // Process the image with buffer pooling and streaming
                            byte[] thumbnailBytes;
                            
                            // Use stream-based file reading with optimized settings
                            using (var fileStream = new FileStream(
                                jpgFilePathOs, 
                                FileMode.Open, 
                                FileAccess.Read, 
                                FileShare.Read,
                                bufferSize: 64 * 1024, // 64KB buffer
                                options: FileOptions.SequentialScan | FileOptions.Asynchronous))
                            {
                                // Get file size for efficient allocation
                                var fileLength = fileStream.Length;
                                
                                // Use buffer pooling to avoid excessive allocations
                                byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent((int)fileLength);
                                
                                try 
                                {
                                    // Read the file into the pooled buffer
                                    int bytesRead = 0;
                                    int totalBytesRead = 0;
                                    
                                    // Handle inexact reads by reading until we get the full content or reach EOF
                                    while (totalBytesRead < fileLength && 
                                          (bytesRead = await fileStream.ReadAsync(buffer, totalBytesRead, (int)fileLength - totalBytesRead)) > 0)
                                    {
                                        totalBytesRead += bytesRead;
                                    }
                                    
                                    // Verify if we got all the expected bytes
                                    if (totalBytesRead != fileLength)
                                    {
                                        Console.WriteLine($"Only read {totalBytesRead} of {fileLength} bytes from file {jpgFilePathOs}");
                                    }
                                    
                                    // Process the image to create thumbnail
                                    thumbnailBytes = RenderThumbnailImage(buffer, ThumbnailMaxSize, $".{ThumbnailFileExtension}");
                                }
                                finally 
                                {
                                    // Return buffer to pool
                                    System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                                }
                            }
                                
                            // Write thumbnail to disk with optimized settings
                            using (var fileStream = new FileStream(
                                thumbnailPathOs, 
                                FileMode.Create, 
                                FileAccess.Write, 
                                FileShare.None,
                                bufferSize: 64 * 1024, // 64KB buffer
                                options: FileOptions.SequentialScan | FileOptions.Asynchronous))
                            {
                                await fileStream.WriteAsync(thumbnailBytes, 0, thumbnailBytes.Length);
                            }

                            thumbnailsCreated++;
                        }
                        catch (Exception ex)
                        {
                            var message = $"Unable to render a thumbnail file at '{thumbnailPathOs}'";
                            errorLog.Add(message);
                            appLogger.LogError(ex, message);
                        }

                    }
                    successLog.Add($"Created {thumbnailsCreated} thumbnail images of total {pngFiles.Length} drawings");



                    //
                    // => Copy all the renditions into the image library on the taxxor project data store
                    //
                    var dataStoreRunningInDocker = false;
                    var nodeProjectDataStore = xmlApplicationConfiguration.SelectSingleNode("/configuration/taxxor/components/service/services/service[@id='documentstore']");
                    if (nodeProjectDataStore != null)
                    {
                        dataStoreRunningInDocker = ((nodeProjectDataStore.GetAttribute("runningindocker") ?? "true") == "true");
                        // appLogger.LogWarning($":-) dataStoreRunningInDocker: {dataStoreRunningInDocker} :-)");
                    }
                    else
                    {
                        // appLogger.LogWarning("!!!!! Could not find Project Data Store root node");
                    }

                    dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("pid", projectId);

                    // Source path is depending if the datastore is running in a docker or not
                    if (!IsRunningInDocker() && dataStoreRunningInDocker)
                    {
                        var dockerOutputFolderPathOs = ConvertToDockerPath(outputFolderPathOs);
                        dataToPost.Add("sourcepath", $"{dockerOutputFolderPathOs}");
                    }
                    else
                    {
                        dataToPost.Add("sourcepath", $"{outputFolderPathOs}");
                    }
                    dataToPost.Add("sourcerelativeto", "http");

                    dataToPost.Add("targetpath", $"/{ImageRenditionsFolderName}/drawings");
                    dataToPost.Add("targetrelativeto", "projectimages");
                    dataToPost.Add("overridetargetfiles", "true");
                    dataToPost.Add("createtargetdirectory", "false");

                    var xmlDirectoryCopyResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorcopydirectory", dataToPost, true);
                    // appLogger.LogInformation(xmlDirectoryCopyResponse.OuterXml);

                    if (XmlContainsError(xmlDirectoryCopyResponse))
                    {
                        errorLog.Add($"{(xmlDirectoryCopyResponse.SelectSingleNode("//message")?.InnerText ?? "")} - {(xmlDirectoryCopyResponse.SelectSingleNode("//debuginfo")?.InnerText ?? "")}");
                        return new TaxxorLogReturnMessage(xmlDirectoryCopyResponse, successLog, warningLog, errorLog);
                    }
                    else
                    {
                        successLog.Add($"Successfully copied drawing renditions to project data store");
                    }

                    imagesOrGraphsHaveChanged = true;
                }

                //
                // => Move drawing renditions and thumbnails
                //
                if (drawingsRenamed.Count > 0)
                {


                    // Build the string to post
                    var drawingData = new List<string>();
                    foreach (var drawingInformation in drawingsRenamed)
                    {
                        var sourceOriginalPathOs = drawingInformation.Key;
                        var sourceNewPathOs = drawingInformation.Value;

                        drawingData.Add($"{sourceOriginalPathOs},{sourceNewPathOs}");
                    }

                    var imageDataString = string.Join("|||", drawingData.ToArray());

                    Console.WriteLine($"imageDataString: {imageDataString}");

                    dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("pid", projectId);
                    dataToPost.Add("assettype", "drawings");
                    dataToPost.Add("images", imageDataString);

                    var xmlImageRenditionsResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditormoveimagerenditions", dataToPost, true);

                    // appLogger.LogInformation(xmlImageRenditionsResponse.OuterXml);

                    if (XmlContainsError(xmlImageRenditionsResponse))
                    {
                        appLogger.LogWarning(xmlImageRenditionsResponse.OuterXml);
                        warningLog.Add($"Unable to move drawing renditions. Message: {xmlImageRenditionsResponse.SelectSingleNode("//message")?.InnerText ?? "unknown"}");
                    }
                    else
                    {
                        var renditionsRemovedString = xmlImageRenditionsResponse.SelectSingleNode("/result/payload")?.InnerText ?? "0";
                        successLog.Add($"Moved {renditionsRemovedString} drawing rendition{((renditionsRemovedString == "1") ? "" : "s")}");
                    }

                    imagesOrGraphsHaveChanged = true;
                }


                //
                // => Remove images and orphaned image renditions
                //
                renditionsToRemove = drawingsRemoved.Union(orphanedDrawingRenditions).ToList<string>();
                if (renditionsToRemove.Count > 0)
                {
                    imagesOrGraphsHaveChanged = true;
                    // foreach (var renditionToRemove in renditionsToRemove)
                    // {
                    //     appLogger.LogInformation($"renditionToRemove: {renditionToRemove}");
                    // }

                    dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("pid", projectId);
                    dataToPost.Add("assettype", "drawings");
                    dataToPost.Add("images", string.Join(',', renditionsToRemove.ToArray()));

                    var xmlImageRenditionsResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorremoveimagerenditions", dataToPost, true);

                    // appLogger.LogInformation(xmlImageRenditionsResponse.OuterXml);

                    if (XmlContainsError(xmlImageRenditionsResponse))
                    {
                        appLogger.LogWarning(xmlImageRenditionsResponse.OuterXml);
                        warningLog.Add($"Unable to delete image renditions. Message: {xmlImageRenditionsResponse.SelectSingleNode("//message")?.InnerText ?? "unknown"}");
                    }
                    else
                    {
                        var renditionsRemovedString = xmlImageRenditionsResponse.SelectSingleNode("/result/payload")?.InnerText ?? "0";
                        successLog.Add($"Removed {renditionsRemovedString} image rendition{((renditionsRemovedString == "1") ? "" : "s")}");
                    }
                }


                //
                // => Update the XML presentation of the library containing all the changes that have been made
                //
                if (imagesOrGraphsHaveChanged)
                {
                    // - Rebuild the contents file on the server
                    dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("pid", projectId);
                    dataToPost.Add("projectscope", projectId);
                    dataToPost.Add("assettype", "projectimages");

                    var xmlAssetInformationResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorprojectassetsinformation", dataToPost, true);
                    if (XmlContainsError(xmlAssetInformationResponse))
                    {
                        errorLog.Add(ConvertErrorXml(xmlAssetInformationResponse));
                        return new TaxxorLogReturnMessage(false, "Unable to render a new image library content file", successLog, warningLog, errorLog);
                    }
                }
                else
                {
                    // - If the routine did not make any changes in the library we save the content we have received at the beginning of the routine on the server
                    var successFullySaved = await DocumentStoreService.FilingData.Save<bool>(xmlCurrentContentOriginal, projectId, contentPath, "projectimages", debugRoutine, false);
                    if (!successFullySaved) return new TaxxorLogReturnMessage(false, "Unable to save image library content file", successLog, warningLog, errorLog);
                }

                //
                // => Create a success entry if there is no update
                //
                if (successLog.Count == 0)
                {
                    if (drawingsAdded.Count == 0 &&
                        drawingsChanged.Count == 0 &&
                        drawingsRenamed.Count == 0 &&
                        drawingsRemoved.Count == 0 &&
                        invalidDrawingRenditions.Count == 0 &&
                        orphanedDrawingRenditions.Count == 0)
                    {
                        successLog.Add($"No need up update drawing renditions for {projectId}");
                    }
                }


                //
                // => Return a message including the log information
                //
                var imagesRenamedDump = DebugDictionary(drawingsRenamed, "orig", "new");
                var debugInformation = @$"
drawingsAdded: {string.Join(',', drawingsAdded.ToArray())}
drawingsChanged: {string.Join(',', drawingsChanged.ToArray())}
drawingsRenamed: {imagesRenamedDump}
drawingsRemoved: {string.Join(',', drawingsRemoved.ToArray())}
invalidDrawingRenditions: {string.Join(',', invalidDrawingRenditions.ToArray())}
orphanedDrawingRenditions: {string.Join(',', orphanedDrawingRenditions.ToArray())}
renditionsToGenerate: {string.Join(',', renditionsToGenerate.ToArray())}
renditionsToRemove: {string.Join(',', renditionsToRemove.ToArray())}
imagesOrGraphsHaveChanged: {imagesOrGraphsHaveChanged.ToString()}
                ";
                // appLogger.LogInformation(debugInformation);

                return new TaxxorLogReturnMessage(true, "Successfully updated drawings library", xmlCurrentContentOriginal, successLog, warningLog, errorLog, debugInformation);
            }
            catch (Exception ex)
            {
                return new TaxxorLogReturnMessage(false, "There was an error unpdating the drawings library", ex);
            }
        }



        /// <summary>
        /// Generates renditions for binary images used in the project
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static async Task<TaxxorLogReturnMessage> GenerateImageRenditions(string projectId)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var contentPath = "/_contents.xml";

            // Exit routine if project ID was not passed
            if (string.IsNullOrEmpty(projectId)) return new TaxxorLogReturnMessage(false, "Unable to generate image renditions", "projectId is null or empty");

            try
            {
                var imagesOrGraphsHaveChanged = false;
                var dataToPost = new Dictionary<string, string>();

                // Logs
                var successLog = new List<string>();
                var warningLog = new List<string>();
                var errorLog = new List<string>();

                // Operations list
                var imagesAdded = new List<string>();
                var imagesChanged = new List<string>();
                var imagesRenamed = new Dictionary<string, string>();
                var imagesRemoved = new List<string>();
                var invalidImageRenditions = new List<string>();
                var orphanedImageRenditions = new List<string>();

                //
                // => Retrieve the images folder contents stored in the Project Data Store
                //
                XmlDocument? xmlCachedContent = await DocumentStoreService.FilingData.Load<XmlDocument>(projectId, contentPath, "projectimages", debugRoutine, false);
                if (XmlContainsError(xmlCachedContent))
                {
                    xmlCachedContent = null;
                }

                //
                // => Retrieve the current state of the image library content
                //
                var retrieveCurrentImageLibraryContent = await RetrieveAssetLibraryContent(projectId);
                if (!retrieveCurrentImageLibraryContent.Success) return new TaxxorLogReturnMessage(retrieveCurrentImageLibraryContent);

                var xmlCurrentContent = new XmlDocument();
                xmlCurrentContent.ReplaceContent(retrieveCurrentImageLibraryContent.XmlPayload);

                // Remember the original images and graphs content of the library
                var xmlCurrentContentOriginal = new XmlDocument();
                xmlCurrentContentOriginal.ReplaceContent(xmlCurrentContent);


                //
                // => Determine the changes that have occurred between now and the previous time we checked
                //
                var xPathImages = $"//file[(contains(@name, '.jpg') or contains(@name, '.jpeg') or contains(@name, '.gif') or contains(@name, '.png')) and not(contains(@path, '{ImageRenditionsFolderName}'))]";
                var assetLibraryDelta = CalculateLibraryDifferences(xPathImages, xmlCachedContent, xmlCurrentContent, ImageRenditionsFolderName);

                //
                // => Fill the local variables with the delta calculated
                //
                imagesAdded.AddRange(assetLibraryDelta.Added);
                imagesChanged.AddRange(assetLibraryDelta.Changed);
                imagesRenamed.AddRange(assetLibraryDelta.Renamed);
                imagesRemoved.AddRange(assetLibraryDelta.Removed);
                invalidImageRenditions.AddRange(assetLibraryDelta.InvalidRenditions);
                orphanedImageRenditions.AddRange(assetLibraryDelta.OrphanedRenditions);

                //
                // => Render new image renditions
                //

                // - all the renditions we need to generate are a combination of new images and invalid image renditions
                var renditionsToGenerate = imagesAdded.Union(invalidImageRenditions).ToList<string>();
                if (renditionsToGenerate.Count > 0)
                {
                    imagesOrGraphsHaveChanged = true;
                    // foreach (var renditionToGenerate in renditionsToGenerate)
                    // {
                    //     appLogger.LogInformation($"renditionToGenerate: {renditionToGenerate}");
                    // }

                    dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("pid", projectId);
                    dataToPost.Add("projectscope", projectId);
                    dataToPost.Add("images", string.Join(',', renditionsToGenerate.ToArray()));

                    var xmlImageRenditionsResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorimagerenditions", dataToPost, true);

                    if (XmlContainsError(xmlImageRenditionsResponse))
                    {
                        appLogger.LogWarning(xmlImageRenditionsResponse.OuterXml);
                        warningLog.Add($"Unable to generate new image renditions. Message: {xmlImageRenditionsResponse.SelectSingleNode("//message")?.InnerText ?? "unknown"}");
                    }
                    else
                    {
                        var renditionsCreatedString = xmlImageRenditionsResponse.SelectSingleNode("/result/payload")?.InnerText ?? "0";
                        successLog.Add($"Generated {renditionsCreatedString} image rendition{((renditionsCreatedString == "1") ? "" : "s")}");
                    }
                }


                //
                // => Move images
                //
                if (imagesRenamed.Count > 0)
                {
                    imagesOrGraphsHaveChanged = true;

                    // Build the string to post
                    var imageData = new List<string>();
                    foreach (var imageInformation in imagesRenamed)
                    {
                        var sourceOriginalPathOs = imageInformation.Key;
                        var sourceNewPathOs = imageInformation.Value;

                        imageData.Add($"{sourceOriginalPathOs},{sourceNewPathOs}");
                    }

                    var imageDataString = string.Join("|||", imageData.ToArray());

                    // Console.WriteLine($"imageDataString: {imageDataString}");

                    dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("pid", projectId);
                    dataToPost.Add("assettype", "images");
                    dataToPost.Add("images", imageDataString);
                    var xmlImageRenditionsResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditormoveimagerenditions", dataToPost, true);

                    // appLogger.LogInformation(xmlImageRenditionsResponse.OuterXml);

                    if (XmlContainsError(xmlImageRenditionsResponse))
                    {
                        appLogger.LogWarning(xmlImageRenditionsResponse.OuterXml);
                        warningLog.Add($"Unable to move image renditions. Message: {xmlImageRenditionsResponse.SelectSingleNode("//message")?.InnerText ?? "unknown"}");
                    }
                    else
                    {
                        var renditionsRemovedString = xmlImageRenditionsResponse.SelectSingleNode("/result/payload")?.InnerText ?? "0";
                        successLog.Add($"Moved {renditionsRemovedString} image rendition{((renditionsRemovedString == "1") ? "" : "s")}");
                    }
                }


                //
                // => Remove images and orphaned image renditions
                //
                var renditionsToRemove = imagesRemoved.Union(orphanedImageRenditions).ToList<string>();
                if (renditionsToRemove.Count > 0)
                {
                    imagesOrGraphsHaveChanged = true;
                    // foreach (var renditionToRemove in renditionsToRemove)
                    // {
                    //     appLogger.LogInformation($"renditionToRemove: {renditionToRemove}");
                    // }

                    dataToPost = new Dictionary<string, string>();
                    dataToPost.Add("pid", projectId);
                    dataToPost.Add("assettype", "images");
                    dataToPost.Add("images", string.Join(',', renditionsToRemove.ToArray()));

                    var xmlImageRenditionsResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorremoveimagerenditions", dataToPost, true);

                    // appLogger.LogInformation(xmlImageRenditionsResponse.OuterXml);

                    if (XmlContainsError(xmlImageRenditionsResponse))
                    {
                        appLogger.LogWarning(xmlImageRenditionsResponse.OuterXml);
                        warningLog.Add($"Unable to delete drawing renditions. Message: {xmlImageRenditionsResponse.SelectSingleNode("//message")?.InnerText ?? "unknown"}");
                    }
                    else
                    {
                        var renditionsRemovedString = xmlImageRenditionsResponse.SelectSingleNode("/result/payload")?.InnerText ?? "0";
                        successLog.Add($"Removed {renditionsRemovedString} drawing rendition{((renditionsRemovedString == "1") ? "" : "s")}");
                    }
                }

                //
                // => Retrieve an updated contents XML file to reflect the changes that have been made in this routine
                //
                if (imagesOrGraphsHaveChanged)
                {
                    retrieveCurrentImageLibraryContent = await RetrieveAssetLibraryContent(projectId);
                    if (!retrieveCurrentImageLibraryContent.Success) return new TaxxorLogReturnMessage(retrieveCurrentImageLibraryContent);

                    xmlCurrentContentOriginal.ReplaceContent(retrieveCurrentImageLibraryContent.XmlPayload);
                }

                var saveCurrentState = true;

                if ((saveCurrentState && imagesOrGraphsHaveChanged) || xmlCachedContent == null)
                {
                    var successFullySaved = await DocumentStoreService.FilingData.Save<bool>(xmlCurrentContentOriginal, projectId, contentPath, "projectimages", debugRoutine, false);
                    if (!successFullySaved) return new TaxxorLogReturnMessage(false, "Unable to update image library content file");
                }

                //
                // => Create a success entry if there is no update
                //
                if (successLog.Count == 0)
                {
                    if (imagesAdded.Count == 0 &&
                        imagesChanged.Count == 0 &&
                        imagesRenamed.Count == 0 &&
                        imagesRemoved.Count == 0 &&
                        invalidImageRenditions.Count == 0 &&
                        orphanedImageRenditions.Count == 0)
                    {
                        successLog.Add($"No need up update image renditions for {projectId}");
                    }
                }

                //
                // => Return a message including the log information
                //
                var imagesRenamedDump = DebugDictionary(imagesRenamed, "orig", "new");
                var debugInformation = @$"
imagesAdded: {string.Join(',', imagesAdded.ToArray())}
imagesChanged: {string.Join(',', imagesChanged.ToArray())}
imagesRenamed: {imagesRenamedDump}
imagesRemoved: {string.Join(',', imagesRemoved.ToArray())}
invalidImageRenditions: {string.Join(',', invalidImageRenditions.ToArray())}
orphanedImageRenditions: {string.Join(',', orphanedImageRenditions.ToArray())}
renditionsToGenerate: {string.Join(',', renditionsToGenerate.ToArray())}
renditionsToRemove: {string.Join(',', renditionsToRemove.ToArray())}
imagesOrGraphsHaveChanged: {imagesOrGraphsHaveChanged.ToString()}
                ";

                return new TaxxorLogReturnMessage(true, "Successfully updated image library renditions", xmlCurrentContentOriginal, successLog, warningLog, errorLog, debugInformation);

            }
            catch (Exception ex)
            {
                return new TaxxorLogReturnMessage(false, "There was an error generating the image renditions", ex);
            }





        }

        /// <summary>
        /// Retrieves an XML representation of the files and folders of the image library
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="assetType"></param>
        /// <returns></returns>
        private static async Task<TaxxorReturnMessage> RetrieveAssetLibraryContent(string projectId, string assetType = "projectimages")
        {
            return await RetrieveAssetLibraryContent(projectId, projectId, assetType);
        }

        /// <summary>
        /// Retrieves an XML representation of the files and folders of the image library
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="projectScope"></param>
        /// <param name="assetType"></param>
        /// <returns></returns>
        private static async Task<TaxxorReturnMessage> RetrieveAssetLibraryContent(string projectId, string projectScope, string assetType = "projectimages")
        {
            var dataToPost = new Dictionary<string, string>
            {
                { "pid", projectId },
                { "projectscope", projectScope },
                { "assettype", assetType }
            };

            var xmlAssetInformationResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorprojectassetsinformation", dataToPost, true);
            var xmlCurrentContent = new XmlDocument();
            var nodePayload = xmlAssetInformationResponse.SelectSingleNode("/result/payload");
            if (nodePayload == null)
            {
                return new TaxxorReturnMessage(false, "Could not collect data", $"payload node could not be found in the response of RetrieveAssetLibraryContent(). response: {xmlAssetInformationResponse.OuterXml}");
            }
            else
            {
                var payloadContent = HttpUtility.HtmlDecode(nodePayload.InnerText);
                try
                {
                    xmlCurrentContent.LoadXml(payloadContent);

                    return new TaxxorReturnMessage(true, "Successfully retrieved ", xmlCurrentContent);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not parse the data that was returned", ex);
                }
            }
        }

        /// <summary>
        /// Renders the image renditions and thumbnails for a specific project
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RenderProjectImageRenditions(string projectId)
        {
            try
            {
                var imageRenditionsResult = await GenerateImageRenditions(projectId);
                var resultForDebugging = "";
                if (imageRenditionsResult.SuccessLog.Count > 0) resultForDebugging += $"successLog: {string.Join(',', imageRenditionsResult.SuccessLog.ToArray())}";
                if (imageRenditionsResult.WarningLog.Count > 0) resultForDebugging += $"\nwarningLog: {string.Join(',', imageRenditionsResult.WarningLog.ToArray())}";
                if (imageRenditionsResult.ErrorLog.Count > 0) resultForDebugging += $"\nerrorLog: {string.Join(',', imageRenditionsResult.ErrorLog.ToArray())}";

                if (!imageRenditionsResult.Success)
                {
                    appLogger.LogError($"There was an error updating the image library.\nMessage: {imageRenditionsResult.Message}\nDebugInfo: {imageRenditionsResult.DebugInfo}");
                    appLogger.LogError(resultForDebugging);
                    return new TaxxorReturnMessage(false, imageRenditionsResult.Message, imageRenditionsResult.DebugInfo);
                }
                else
                {
                    appLogger.LogInformation(resultForDebugging);
                }
                return new TaxxorReturnMessage(true, "Successfully updated image library", resultForDebugging);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was a problem updating the image library");
                return new TaxxorReturnMessage(false, "There was an issue rendering the image renditions", ex);
            }
        }

        /// <summary>
        /// Generates new graph renditions without a valid context (from a background service for instance)
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static async Task<TaxxorLogReturnMessage> GenerateGraphRenditions(string projectId)
        {

            var context = System.Web.Context.Current;
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Exit routine if project ID was not passed
            if (string.IsNullOrEmpty(projectId)) return new TaxxorLogReturnMessage(false, "Unable to generate image renditions", "projectId is null or empty");

            try
            {
                // Initiate a custom context if a "generic" context is not available
                if (context == null)
                {
                    var contextAccessor = CreateCustomHttpContext();
                    context = contextAccessor.HttpContext;
                }


                // Make sure we have a project variables object
                ProjectVariables? projectVars = null;
                if (!ProjectVariablesExistInContext(context))
                {
                    projectVars = new ProjectVariables();
                    projectVars.projectId = projectId;
                    projectVars = await FillProjectVariablesFromProjectId(projectVars);
                    projectVars.currentUser.Id = SystemUser;

                    // Console.WriteLine($"***** Project Variables for Graph Renditions *****");
                    // Console.WriteLine(projectVars.DumpToString());
                    // Console.WriteLine($"**************************************************");


                }
                else
                {
                    projectVars = RetrieveProjectVariables(context);
                    projectVars.projectId = projectId;
                }

                // Store the updated project variables in the context
                SetProjectVariables(context, projectVars);


                // Make sure we have a request variables object
                RequestVariables? reqVars = null;
                if (!RequestVariablesExistInContext(context))
                {
                    reqVars = new RequestVariables
                    {
                        returnType = ReturnTypeEnum.Txt
                    };
                    SetRequestVariables(context, reqVars);
                }
                else
                {
                    reqVars = RetrieveRequestVariables(context);
                }

                // Start the update drawing renditions process
                return await GenerateGraphRenditions(reqVars, projectVars);
            }
            catch (Exception ex)
            {
                return new TaxxorLogReturnMessage(false, "There was an error generating the graph renditions", ex);
            }
        }


        /// <summary>
        /// Generates new graph renditions
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="sectionIds"></param>
        /// <param name="dataRefs"></param>
        /// <returns></returns>
        public static async Task<TaxxorLogReturnMessage> GenerateGraphRenditions(RequestVariables reqVars, ProjectVariables projectVars, List<string> sectionIds = null, List<string> dataRefs = null)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var projectId = projectVars.projectId;
            var renderedGraphsFolderName = "renditions";

            var dataStoreRunningInDocker = false;
            var nodeProjectDataStore = xmlApplicationConfiguration.SelectSingleNode("/configuration/taxxor/components/service/services/service[@id='documentstore']");
            if (nodeProjectDataStore != null) dataStoreRunningInDocker = ((nodeProjectDataStore.GetAttribute("runningindocker") ?? "true") == "true");
            // appLogger.LogWarning($":-) dataStoreRunningInDocker: {dataStoreRunningInDocker} :-)");

            // Create a temoporary directory where we will store the generated binaries
            var useRandomOutputFolder = (debugRoutine) ? false : true;
            var tempFolderName = (useRandomOutputFolder) ? $"graphrenderer_{projectVars.projectId}{CreateTimeBasedString("millisecond")}{RandomString(8, false)}" : $"graphrenderer_{projectVars.projectId}";
            var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/{tempFolderName}";


            //
            // => Construct a project variables object that we can send to the Taxxor Pandoc Service
            //
            var projectVariablesForGraphGeneration = new ProjectVariables
            {
                // Fill with variables passed to this function
                projectId = projectId,
                versionId = "latest",

                // Fill with variables retrieved from the context    
                editorContentType = projectVars.editorContentType ?? "regular",
                reportTypeId = projectVars.reportTypeId ?? RetrieveReportTypeIdFromProjectId(projectId),
                outputChannelType = "pdf",
                outputChannelVariantLanguage = projectVars?.outputChannelVariantLanguage ?? projectVars.outputChannelDefaultLanguage ?? "en",
                editorId = RetrieveEditorIdFromProjectId(projectId)
            };
            if (string.IsNullOrEmpty(projectVars.outputChannelVariantId))
            {
                projectVariablesForGraphGeneration.outputChannelVariantId = RetrieveFirstOutputChannelVariantIdFromEditorId(projectVariablesForGraphGeneration.editorId);
                projectVariablesForGraphGeneration.outputChannelVariantLanguage = projectVars.outputChannelDefaultLanguage;
            }
            else
            {
                projectVariablesForGraphGeneration.outputChannelVariantId = projectVars.outputChannelVariantId;
                projectVariablesForGraphGeneration.outputChannelDefaultLanguage = projectVars.outputChannelDefaultLanguage;
            }



            // Exit routine if project ID was not passed
            if (string.IsNullOrEmpty(projectId)) return new TaxxorLogReturnMessage(false, "Unable to generate graph renditions", "projectId is null or empty");

            // Logs
            var successMessage = "";
            var successLog = new List<string>();
            var warningMessage = "";
            var warningLog = new List<string>();
            var errorMessage = "";
            var errorLog = new List<string>();

            try
            {
                var graphsHaveChanged = false;
                var dataToPost = new Dictionary<string, string>();

                // Operations list
                var graphsAdded = new List<string>();
                var graphsChanged = new List<string>();
                var graphsRemoved = new List<string>();
                var invalidGraphRenditions = new List<string>();
                var orphanedGraphRenditions = new List<string>();

                var renditionsToGenerate = new List<string>();
                var renditionsToRemove = new List<string>();



                //
                // => Retrieve the content for the graphs
                //
                var retrieveGraphContentResult = (sectionIds == null && dataRefs == null) ? await RetrieveGraphContent(projectVariablesForGraphGeneration) : await RetrieveGraphContent(projectVariablesForGraphGeneration, sectionIds, dataRefs);
                if (!retrieveGraphContentResult.Success) return new TaxxorLogReturnMessage(retrieveGraphContentResult);
                var xmlGraphContent = new XmlDocument();
                xmlGraphContent.ReplaceContent(retrieveGraphContentResult.XmlPayload);

                if (debugRoutine) await xmlGraphContent.SaveAsync($"{logRootPathOs}/-graphcontent.xml", true, true);

                //
                // => Render the graph renditions
                //
                var chartCounter = 0;
                var nodeListArticlesWithGraphs = xmlGraphContent.SelectNodes("/div/article");
                if (nodeListArticlesWithGraphs.Count > 0)
                {
                    var graphWrapperIds = new List<string>();

                    // - Prepare the document
                    foreach (XmlNode nodeArticleWithGraph in nodeListArticlesWithGraphs)
                    {
                        var contentLang = nodeArticleWithGraph.GetAttribute("lang") ?? "undefined";
                        var dataReference = nodeArticleWithGraph.GetAttribute("data-ref") ?? "undefined";

                        // - Adjust the source documents and insert references to the images
                        var xPathGraphData = $"*{RetrieveGraphElementsBaseXpath()}";
                        var nodeListGraphWrappers = nodeArticleWithGraph.SelectNodes(xPathGraphData);

                        foreach (XmlNode nodeWrapper in nodeListGraphWrappers)
                        {
                            var randomId = RandomString(10);
                            var nodeTable = nodeWrapper.SelectSingleNode("table");
                            var wrapperId = nodeWrapper.GetAttribute("id");

                            // Make sure that no double wrapper ID's are present
                            if (graphWrapperIds.Contains($"{wrapperId}-{contentLang}"))
                            {
                                warningMessage = $"Found a graph in {dataReference} and language {contentLang} with ID {wrapperId} that is not unique.";
                                appLogger.LogWarning(warningMessage);
                                warningLog.Add(warningMessage);
                            }
                            else
                            {
                                graphWrapperIds.Add($"{wrapperId}-{contentLang}");
                            }

                            // Make sure an ID is present on the wrapper div
                            if (string.IsNullOrEmpty(nodeWrapper.GetAttribute("id")))
                            {
                                warningMessage = $"Found a graph data table in {dataReference} without a wrapper element id.";
                                warningLog.Add("warningMessage");
                                appLogger.LogWarning(warningMessage);
                                nodeWrapper.SetAttribute("id", randomId);
                                wrapperId = randomId;
                            }
                            else
                            {
                                if (wrapperId.Contains("_")) wrapperId = wrapperId.SubstringAfter("_");
                            }

                            // Prepare the XML so that we can generate binaries of the graphs
                            var svgAssetFileName = $"{Path.GetFileNameWithoutExtension(dataReference)}---{wrapperId}---{contentLang}.png";
                            SetAttribute(nodeTable, "data-assetnameconvert", $"{renderedGraphsFolderName}/{svgAssetFileName}");
                            SetAttribute(nodeTable, "data-assetnameuse", $"{renderedGraphsFolderName}/{svgAssetFileName}");

                            // Set the wrapper and table nodes with an unique ID so that the utility renderer file/routine is able to process it
                            nodeWrapper.SetAttribute("id", $"tablewrapper_{randomId}");
                            nodeTable.SetAttribute("id", $"table_{randomId}");

                            chartCounter++;
                        }
                    }

                    // - Start rendering the graphs
                    TaxxorReturnMessage? generateGraphBinariesResult = null;

                    if (chartCounter > 0)
                    {


                        // - Setup the XBRL Generation Properties object that we will be using throughout the process
                        var xbrlConversionProperties = new XbrlConversionProperties(tempFolderPathOs)
                        {
                            WorkingFolderPathOs = tempFolderPathOs,
                            WorkingVisualsFolderPathOs = tempFolderPathOs,
                            XbrlVars = projectVariablesForGraphGeneration
                        };
                        xbrlConversionProperties.XmlClientAssets = Extensions.RenderOutputChannelCssAndJs(xbrlConversionProperties.XbrlVars, "reportgenerator");
                        xbrlConversionProperties.ResetLog();

                        // Console.WriteLine($"***** Project Variables for Graph Renditions *****");
                        // Console.WriteLine(xbrlConversionProperties.XbrlVars.DumpToString());
                        // Console.WriteLine($"**************************************************");

                        // Console.WriteLine("************ CLIENT ASSETS ************");
                        // Console.WriteLine(PrettyPrintXml(xbrlConversionProperties.XmlClientAssets));
                        // Console.WriteLine("***************************************");


                        xbrlConversionProperties.XmlXbrl.LoadXml(xmlGraphContent.OuterXml);

                        generateGraphBinariesResult = await GenerateBinariesForGraphs(xbrlConversionProperties, true, debugRoutine);
                        if (generateGraphBinariesResult.Success)
                        {
                            if (generateGraphBinariesResult.Message.Contains("WARNING"))
                            {
                                appLogger.LogWarning($"{generateGraphBinariesResult.Message} debuginfo: {generateGraphBinariesResult.DebugInfo}");
                                warningLog.Add(generateGraphBinariesResult.Message);
                            }
                            else
                            {
                                successMessage = $"Successfully generated {chartCounter} graphs";
                                appLogger.LogInformation(successMessage);
                                successLog.Add(successMessage);
                            }

                        }
                        else
                        {
                            errorMessage = $"ERROR: {generateGraphBinariesResult.Message}";
                            appLogger.LogError($"{generateGraphBinariesResult.Message}, debuginfo: {generateGraphBinariesResult.DebugInfo}");
                            errorLog.Add(errorMessage);
                            return new TaxxorLogReturnMessage(false, errorMessage);
                        }

                        // - Copy the generated renditions to the Taxxor Project Data Store
                        if (generateGraphBinariesResult.Success)
                        {
                            dataToPost = new Dictionary<string, string>
                            {
                                { "pid", projectId },
                                { "sourcerelativeto", "http" },
                                { "targetpath", $"/{ImageRenditionsFolderName}/graphs" },
                                { "targetrelativeto", "projectimages" },
                                { "overridetargetfiles", "true" },
                                { "createtargetdirectory", "false" }
                            };

                            // Source path is depending if the datastore is running in a docker or not
                            if (!IsRunningInDocker() && dataStoreRunningInDocker)
                            {
                                var dockerOutputFolderPathOs = ConvertToDockerPath($"{tempFolderPathOs}/{renderedGraphsFolderName}");
                                dataToPost.Add("sourcepath", $"{dockerOutputFolderPathOs}");
                            }
                            else
                            {
                                dataToPost.Add("sourcepath", $"{tempFolderPathOs}/{renderedGraphsFolderName}");
                            }


                            var xmlDirectoryCopyResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorcopydirectory", dataToPost, true);
                            // appLogger.LogInformation(xmlDirectoryCopyResponse.OuterXml);

                            if (XmlContainsError(xmlDirectoryCopyResponse))
                            {
                                errorLog.Add($"{(xmlDirectoryCopyResponse.SelectSingleNode("//message")?.InnerText ?? "")} - {(xmlDirectoryCopyResponse.SelectSingleNode("//debuginfo")?.InnerText ?? "")}");
                                return new TaxxorLogReturnMessage(xmlDirectoryCopyResponse, successLog, warningLog, errorLog);
                            }
                            else
                            {
                                successLog.Add($"Successfully copied graph renditions to project data store");
                            }

                        }

                    }




                }



                //
                // => Create a success entry if there is no update
                //
                if (successLog.Count == 0)
                {
                    if (graphsAdded.Count == 0 &&
                        graphsChanged.Count == 0 &&
                        graphsRemoved.Count == 0 &&
                        invalidGraphRenditions.Count == 0 &&
                        orphanedGraphRenditions.Count == 0)
                    {
                        successLog.Add($"No need up update graph renditions for {projectId}");
                    }
                }

                //
                // => Return a message including the log information
                //
                var debugInformation = @$"
graphsAdded: {string.Join(',', graphsAdded.ToArray())}
graphsChanged: {string.Join(',', graphsChanged.ToArray())}
graphsRemoved: {string.Join(',', graphsRemoved.ToArray())}
invalidGraphRenditions: {string.Join(',', invalidGraphRenditions.ToArray())}
orphanedGraphRenditions: {string.Join(',', orphanedGraphRenditions.ToArray())}
renditionsToGenerate: {string.Join(',', renditionsToGenerate.ToArray())}
renditionsToRemove: {string.Join(',', renditionsToRemove.ToArray())}
graphsHaveChanged: {graphsHaveChanged.ToString()}
                ";

                return new TaxxorLogReturnMessage(true, "Successfully updated graph renditions", successLog, warningLog, errorLog, debugInformation);
            }
            catch (Exception ex)
            {
                errorMessage = "There was an error generating the graph renditions";
                appLogger.LogError(ex, errorMessage);
                return new TaxxorLogReturnMessage(false, errorMessage, ex);
            }

        }


        /// <summary>
        /// Generates binary image files from Highcharts/Echarts graphs
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> GenerateBinariesForGraphs(XbrlConversionProperties xbrlConversionProperties, bool renderAllFormats, bool debugRoutine)
        {
            // => Test if there are any graphs to convert
            if (xbrlConversionProperties.XmlXbrl.SelectNodes($"//article//section{RetrieveGraphElementsBaseXpath()}").Count == 0)
            {
                return new TaxxorReturnMessage(true, "No need to render binaries from graphs");
            }

            // Attempt to retrieve domain name that we can use to generate the graphs
            string? localWebAddressDomain = LocalWebAddressDomain;
            if (string.IsNullOrEmpty(localWebAddressDomain))
            {
                localWebAddressDomain = CalculateFullPathOs("ownaddress-domain");

                if (string.IsNullOrEmpty(localWebAddressDomain)) return new TaxxorReturnMessage(false, "Unable to render graph binaries", $"Cannot generate utility file if localWebAddressDomain is null or empty. stack-trace: {GetStackTrace()}");
            }


            //
            // => Generate the utility HTML file that contains all the graphs and visuals
            //
            var webAssetsRootPath = $"/outputchannels/{TaxxorClientId}/pdf";

            var utilityFilePathOs = $"{xbrlConversionProperties.WorkingFolderPathOs}/_graphs-utility.html";
            var graphMagnificationFactor = 5;

            // Grab the styles that we need to insert
            var xsltPathOs = CalculateFullPathOs("generate-graphs-utility");
            var inlineCss = "";
            var inlineJs = "";

            XsltArgumentList xsltArgumentList = new();
            xsltArgumentList.AddParam("inline-css", "", inlineCss);
            xsltArgumentList.AddParam("inline-js", "", inlineJs);
            xsltArgumentList.AddParam("rootfolder", "", webAssetsRootPath);
            xsltArgumentList.AddParam("baseurl", "", localWebAddressDomain);
            xsltArgumentList.AddParam("token", "", xbrlConversionProperties.AccessToken);
            xsltArgumentList.AddParam("mode", "", "graphs");
            xsltArgumentList.AddParam("graphengine", "", (TaxxorClientId == "philips") ? "highcharts" : "echarts");
            xsltArgumentList.AddParam("lang", "", xbrlConversionProperties?.XbrlVars?.outputChannelVariantLanguage ?? "en");

            // xsltArgumentList.AddParam("graph-magnification", "", );
            // xbrlConversionProperties.XbrlVars.outputChannelVariantLanguage

            xsltArgumentList.AddParam("clientassets", "", xbrlConversionProperties.XmlClientAssets.DocumentElement);


            var htmlGraphsUtility = TransformXmlToDocument(xbrlConversionProperties.XmlXbrl, xsltPathOs, xsltArgumentList);

            if (XmlContainsError(htmlGraphsUtility))
            {
                return new TaxxorReturnMessage(htmlGraphsUtility);
            }

            // Store the utiliy file on the shared drive
            await htmlGraphsUtility.SaveAsync(utilityFilePathOs);

            //
            // => Call the Pandoc service to convert the drawings into binary image files
            //

            // Potentially correct the paths
            var outputFolderPathOs = xbrlConversionProperties.WorkingVisualsFolderPathOs;
            if (siteType == "local")
            {
                // We have used full paths to the disk but the conversion service runs in a docker, so we need to corret the path
                utilityFilePathOs = ConvertToDockerPath(utilityFilePathOs);
                outputFolderPathOs = ConvertToDockerPath(outputFolderPathOs);
            }

            // Call the conversion service
            var visualsConversionResponse = await ConversionService.RenderGraphBinariesFromUtilityPage(utilityFilePathOs, outputFolderPathOs, graphMagnificationFactor, false, false, renderAllFormats, renderAllFormats);
            if (XmlContainsError(visualsConversionResponse))
            {
                // appLogger.LogError(visualsConversionResponse.OuterXml);
                return new TaxxorReturnMessage(visualsConversionResponse);
            }

            //
            // => Cleanup the XML so that we get rid of the table data for the graphs and the svg data for the drawings
            //
            var xmlXbrl = new XmlDocument();
            xmlXbrl.LoadXml(xbrlConversionProperties.XmlXbrl.OuterXml);

            var nodeListSvg = xmlXbrl.SelectNodes("//article//section//*[local-name()='svg']");
            foreach (XmlNode nodeSvg in nodeListSvg)
            {
                RemoveXmlNode(nodeSvg);
            }

            var nodeListGraphDataTables = xmlXbrl.SelectNodes("//article//section//div[div/@class='chart-content']/table");
            foreach (XmlNode nodeGraphDataTable in nodeListGraphDataTables)
            {
                RemoveXmlNode(nodeGraphDataTable);
            }

            var nodeListHighChartsContent = xmlXbrl.SelectNodes("//article//section//div[contains(@class, 'chart-content')]");
            foreach (XmlNode nodeWrapperDiv in nodeListHighChartsContent)
            {
                RemoveXmlNode(nodeWrapperDiv);
            }

            //
            // => Store the processed XML file back into the properties object that we are using
            //
            try
            {
                xbrlConversionProperties.XmlXbrl.LoadXml(xmlXbrl.OuterXml);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error loading the post processed XML document back into the object", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

            return new TaxxorReturnMessage(true, visualsConversionResponse.SelectSingleNode("//message")?.InnerText ?? "Successfully converted graphs to images");

        }


        /// <summary>
        /// Retrieves the XML content on which the graph generation needs to be based
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="sectionIds"></param>
        /// <param name="dataReferences"></param>
        /// <returns></returns>
        private static async Task<TaxxorReturnMessage> RetrieveGraphContent(ProjectVariables projectVars, List<string> sectionIds = null, List<string> dataReferences = null)
        {
            var dataToPost = new Dictionary<string, string>
            {
                { "pid", projectVars.projectId },
                { "ctype", projectVars.editorContentType },
                { "rtype", projectVars.reportTypeId },
                { "octype", projectVars.outputChannelType },
                { "ocvariantid", projectVars.outputChannelVariantId },
                { "oclang", RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId) },
                { "sectionids", (sectionIds == null) ? "all" : (string.Join(",", sectionIds.ToArray())) },
                { "datarefs", (dataReferences == null) ? "all" : (string.Join(",", dataReferences.ToArray())) }
            };

            var xmlAssetInformationResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorrendergraphs", dataToPost, true, false, projectVars);
            var xmlCurrentContent = new XmlDocument();
            var nodePayload = xmlAssetInformationResponse.SelectSingleNode("/result/payload");
            if (nodePayload == null)
            {
                return new TaxxorReturnMessage(false, "Could not collect data", $"payload node could not be found in the response of RetrieveGraphContent(). response: {xmlAssetInformationResponse.OuterXml}");
            }
            else
            {
                var payloadContent = HttpUtility.HtmlDecode(nodePayload.InnerText);
                try
                {
                    xmlCurrentContent.LoadXml(payloadContent);

                    return new TaxxorReturnMessage(true, "Successfully retrieved content for graphs", xmlCurrentContent);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not parse the graph content data that was returned", ex);
                }
            }
        }


    }
}