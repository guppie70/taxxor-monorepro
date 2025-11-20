using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to convert images and drawings to "neutral" formats that can be used in iXBRL filings
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Generates alternative renderings for the images in the image library
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static async Task<TaxxorLogReturnMessage> GenerateImageRenditions(string projectId)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // To calculate the paths we need to understand the project context
            var projectVars = new ProjectVariables();
            projectVars.projectId = projectId;
            projectVars = await FillProjectVariablesFromProjectId(projectVars);
            FillCorePathsInProjectVariables(ref projectVars);
            // Determine the folder where the data lives
            var rootPathOs = RetrieveImagesRootFolderPathOs(projectId);

            var contentPath = "/_contents.xml";

            var contentPathOs = rootPathOs + contentPath;

            try
            {

                var nodeRenditionLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/renditions/locations/location[@id='projectimagesrenditions']");
                var imagesOrGraphsHaveChanged = false;

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
                XmlDocument xmlCachedContent = new XmlDocument();
                if (File.Exists(contentPathOs))
                {
                    xmlCachedContent.Load(contentPathOs);
                }
                else
                {
                    xmlCachedContent = null;
                }

                //
                // => Retrieve the current state of the image library content
                //
                var retrieveCurrentImageLibraryContent = await RetrieveImageLibraryContent(projectId);
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

                    // Render the image renditions
                    var imageRenditionsResponse = await CreateImageRenditions(projectId, renditionsToGenerate);

                    // Deal with the response
                    if (!imageRenditionsResponse.Success)
                    {
                        appLogger.LogWarning(imageRenditionsResponse.ToString());
                        warningLog.Add($"Unable to generate new image renditions. Message: {imageRenditionsResponse.Message}");
                    }
                    else
                    {
                        successLog.Add($"Generated {imageRenditionsResponse.Payload} image rendition{((imageRenditionsResponse.Payload == "1") ? "" : "s")}");
                    }
                }


                //
                // => Move images
                //
                if (imagesRenamed.Count > 0)
                {
                    imagesOrGraphsHaveChanged = true;

                    // Move the image renditions
                    var movedImageRenditionsResponse = await MoveImageRenditions(projectId, "images", imagesRenamed);

                    // Handle the response
                    if (!movedImageRenditionsResponse.Success)
                    {
                        appLogger.LogWarning(movedImageRenditionsResponse.ToString());
                        warningLog.Add($"Unable to move image renditions. Message: {movedImageRenditionsResponse.Message}");
                    }
                    else
                    {
                        successLog.Add($"Moved {movedImageRenditionsResponse.Payload} image rendition{((movedImageRenditionsResponse.Payload == "1") ? "" : "s")}");
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

                    // Remove the image renditions
                    var removeImageRenditions = await RemoveImageRenditions(projectId, "images", renditionsToRemove);


                    // Handle the response
                    if (!removeImageRenditions.Success)
                    {
                        appLogger.LogWarning(removeImageRenditions.ToString());
                        warningLog.Add($"Unable to delete image renditions. Message: {removeImageRenditions.Message}");
                    }
                    else
                    {
                        successLog.Add($"Removed {removeImageRenditions.Payload} image rendition{((removeImageRenditions.Payload == "1") ? "" : "s")}");
                    }
                }


                //
                // => Retrieve an updated contents XML file to reflect the changes that have been made in this routine
                //
                if (imagesOrGraphsHaveChanged)
                {
                    retrieveCurrentImageLibraryContent = await RetrieveImageLibraryContent(projectId);
                    if (!retrieveCurrentImageLibraryContent.Success) return new TaxxorLogReturnMessage(retrieveCurrentImageLibraryContent);

                    xmlCurrentContentOriginal.ReplaceContent(retrieveCurrentImageLibraryContent.XmlPayload);
                }


                //
                // => Save the updated content XML document in the library
                //
                var saveCurrentState = true;

                if ((saveCurrentState && imagesOrGraphsHaveChanged) || xmlCachedContent == null)
                {
                    try
                    {
                        await xmlCurrentContentOriginal.SaveAsync(contentPathOs);
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorLogReturnMessage(false, "Unable to update image library content file", ex);
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




            /// <summary>
            /// Retrieves an XML representation of the files and folders of the image library
            /// </summary>
            /// <param name="projectScope"></param>
            /// <param name="assetType"></param>
            /// <returns></returns>
            async Task<TaxxorReturnMessage> RetrieveImageLibraryContent(string projectScope, string assetType = "projectimages")
            {
                return await RetrieveProjectAssetsInformation(projectScope, assetType);
            }


        }



        /// <summary>
        /// Retrieves file information of the assets used in a project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RetrieveProjectAssetsInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Retrieve posted data
            //
            var projectScope = request.RetrievePostedValue("projectscope", RegexEnum.None, false, ReturnTypeEnum.Xml, "all");
            var assetType = request.RetrievePostedValue("assettype", RegexEnum.None, false, ReturnTypeEnum.Xml, "projectimages");

            var assetInfoRetrieveResult = await RetrieveProjectAssetsInformation(projectScope, assetType);
            if (assetInfoRetrieveResult.Success)
            {
                await response.OK(assetInfoRetrieveResult, ReturnTypeEnum.Xml, true);
            }
            else
            {
                await response.Error(assetInfoRetrieveResult, ReturnTypeEnum.Xml, true);
            }
        }

        /// <summary>
        /// Re-create the library assets information file which contains the contents of the asset library
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task CreateProjectAssetsInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Retrieve posted data
            //
            var projectScope = request.RetrievePostedValue("projectscope", RegexEnum.None, false, ReturnTypeEnum.Xml, "all");
            var assetType = request.RetrievePostedValue("assettype", RegexEnum.None, false, ReturnTypeEnum.Xml, "projectimages");

            var rootPathOs = RetrieveImagesRootFolderPathOs(projectVars.projectId);

            try
            {

                var contentPath = "/_contents.xml";
                var contentPathOs = rootPathOs + contentPath;
                var xmlImageLibraryContents = new XmlDocument();

                var assetInfoRetrieveResult = await RetrieveProjectAssetsInformation(projectScope, assetType);
                if (assetInfoRetrieveResult.Success)
                {
                    xmlImageLibraryContents.ReplaceContent(assetInfoRetrieveResult.XmlPayload);

                    await xmlImageLibraryContents.SaveAsync(contentPathOs, true, true);

                    await response.OK(assetInfoRetrieveResult, ReturnTypeEnum.Xml, true);
                }
                else
                {
                    await response.Error(assetInfoRetrieveResult, ReturnTypeEnum.Xml, true);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = "Unable to create asset information file";
                appLogger.LogError(ex, errorMessage);
                HandleError(errorMessage, $"projectScope: {projectScope}, assetType: {assetType}, rootPathOs: {rootPathOs}, error: {ex}");
            }

        }

        /// <summary>
        /// Setup the folderstructure in the project data store for the image renditions
        /// </summary>
        /// <returns></returns>
        public static TaxxorReturnMessage SetupProjectAssetsRenditionsStructure(ProjectVariables projectVars)
        {
            try
            {
                var nodeListRenditionLocations = xmlApplicationConfiguration.SelectNodes("/configuration/renditions/locations/location");
                foreach (XmlNode nodeRenditionLocation in nodeListRenditionLocations)
                {
                    var rootFolderPathOs = CalculateFullPathOs(nodeRenditionLocation, null, projectVars);
                    // Console.BackgroundColor = ConsoleColor.Blue;
                    // Console.ForegroundColor = ConsoleColor.White;
                    // Console.WriteLine($"!! rootFolderPathOs: {rootFolderPathOs}");
                    // Console.ResetColor();
                    if (!Directory.Exists(rootFolderPathOs))
                    {
                        Directory.CreateDirectory(rootFolderPathOs);
                    }
                }

                return new TaxxorReturnMessage(true, "Successfully created folder structure");
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error setting up the asset rendition folder structure", ex);
            }
        }

        /// <summary>
        /// Retrieves file information of the assets used in a project
        /// </summary>
        /// <param name="projectScope">Project ID or "all" to retrieve the information for all the projects</param>
        /// <param name="assetType">"projectimages" or "projectdownloads"</param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RetrieveProjectAssetsInformation(string projectScope, string assetType)
        {

            var debugRoutine = (siteType == "local" || siteType == "dev");

            var debugInfo = $"projectScope: {projectScope}";

            var xmlAssets = new XmlDocument();
            xmlAssets.LoadXml("<projects/>");


            try
            {
                var xPathProjects = (projectScope == "all") ? "/configuration/cms_projects/cms_project[versions/version/status/text() = 'open']" : $"/configuration/cms_projects/cms_project[@id='{projectScope}']";

                var nodeListProjects = xmlApplicationConfiguration.SelectNodes(xPathProjects);
                foreach (XmlNode nodeCurrentProject in nodeListProjects)
                {
                    var currentProjectId = GetAttribute(nodeCurrentProject, "id") ?? "";
                    debugInfo += $", currentProjectId: {currentProjectId}";

                    // To calculate the paths we need to understand the project context
                    var projectVars = new ProjectVariables();
                    projectVars.projectId = currentProjectId;
                    projectVars = await FillProjectVariablesFromProjectId(projectVars);
                    FillCorePathsInProjectVariables(ref projectVars);

                    // Create the renditions folder structure
                    var folderStructureSetupResult = SetupProjectAssetsRenditionsStructure(projectVars);
                    if (!folderStructureSetupResult.Success) return folderStructureSetupResult;

                    // Determine the folder where the data lives
                    var rootPathOs = "";
                    rootPathOs = (assetType == "projectimages") ? RetrieveImagesRootFolderPathOs(nodeCurrentProject) : RetrieveDownloadsRootFolderPathOs(nodeCurrentProject);

                    // Console.BackgroundColor = ConsoleColor.Blue;
                    // Console.ForegroundColor = ConsoleColor.White;
                    // Console.WriteLine($"rootPathOs: {rootPathOs}");
                    // Console.ResetColor();

                    debugInfo += $", rootPathOs: {rootPathOs}";


                    // Retrieve the content of the directory
                    var dir = new DirectoryInfo(rootPathOs);
                    var reFilter = new Regex(@"^.*\.(jpg|jpeg|png|svg|gif)$", RegexOptions.IgnoreCase);
                    var xdocumentImageFolderContent = new XDocument(_getDirectoryXml(dir, dir.FullName, true, reFilter));
                    var xmlImageFolderContent = xdocumentImageFolderContent.ToXmlDocument();

                    if (debugRoutine) await xmlImageFolderContent.SaveAsync(logRootPathOs + "/assetinformation.xml");

                    // Create the project node and append it to the overall xml document
                    var nodeProjectAssets = xmlAssets.CreateElement("project");
                    nodeProjectAssets.SetAttribute("id", currentProjectId);
                    nodeProjectAssets.SetAttribute("rootfolderpathos", rootPathOs);
                    nodeProjectAssets.AppendChild(xmlAssets.ImportNode(xmlImageFolderContent.DocumentElement, true));

                    // Add to overall xml document
                    xmlAssets.DocumentElement.AppendChild(nodeProjectAssets);
                }

                // if (siteType == "local")
                // {
                //     Console.WriteLine("--------- Images Folder Content ---------");
                //     Console.WriteLine(PrettyPrintXml(xmlAssets));
                //     Console.WriteLine("-----------------------------------------");
                // }

                return new TaxxorReturnMessage(true, "Successfully retrieved project images information", xmlAssets, debugInfo);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error converting graphs and images", $"error: {ex}, {debugInfo}");
            }
        }

        /// <summary>
        /// Generate image renditions
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task CreateImageRenditions(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Retrieve posted data
            //
            var projectScope = request.RetrievePostedValue("projectscope", RegexEnum.None, true, ReturnTypeEnum.Xml, "all");
            var imagesString = request.RetrievePostedValue("images", RegexEnum.None, false, ReturnTypeEnum.Xml, "");

            List<string> images = new List<string>();
            if (projectScope != "all" && !string.IsNullOrEmpty(imagesString))
            {
                if (!imagesString.Contains(','))
                {
                    images.Add(imagesString);
                }
                else
                {
                    images.AddRange(imagesString.Split(','));
                }
            }

            var conversionResult = await CreateImageRenditions(projectScope, images);
            if (conversionResult.Success)
            {
                await response.OK(conversionResult, ReturnTypeEnum.Xml, true);
            }
            else
            {
                await response.Error(conversionResult, ReturnTypeEnum.Xml, true);
            }
        }

        /// <summary>
        /// Generate image renditions
        /// </summary>
        /// <param name="projectScope"></param>
        /// <param name="images"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> CreateImageRenditions(string projectScope, List<string>? images = null)
        {
            var renditionsCreated = 0;
            var debugInfo = $"projectScope: {projectScope}";

            try
            {
                var nodeRenditionLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/renditions/locations/location[@id='projectimagesrenditions']");

                var xPathProjects = (projectScope == "all") ? "/configuration/cms_projects/cms_project[versions/version/status/text() = 'open']" : $"/configuration/cms_projects/cms_project[@id='{projectScope}']";

                var nodeListProjects = xmlApplicationConfiguration.SelectNodes(xPathProjects);
                foreach (XmlNode nodeCurrentProject in nodeListProjects)
                {
                    var currentProjectId = GetAttribute(nodeCurrentProject, "id") ?? "";
                    debugInfo += $", currentProjectId: {currentProjectId}";

                    // To calculate the paths we need to understand the project context
                    var projectVars = new ProjectVariables();
                    projectVars.projectId = currentProjectId;
                    projectVars = await FillProjectVariablesFromProjectId(projectVars);
                    FillCorePathsInProjectVariables(ref projectVars);

                    // Determine the folder where the data lives
                    var rootPathOs = RetrieveImagesRootFolderPathOs(nodeCurrentProject);
                    debugInfo += $", rootPathOs: {rootPathOs}";

                    // Root path of the image renditions
                    var imageRenditionsPathOs = CalculateFullPathOs(nodeRenditionLocation, null, projectVars);

                    if (string.IsNullOrEmpty(imageRenditionsPathOs))
                        return new TaxxorReturnMessage(false, "Unable to calculate image rendition path");

                    //
                    // => Create a list of images to work with
                    //
                    var imagesToProcess = new List<string>();
                    if (images == null)
                    {
                        // Find the images on the disk - optimize with batch processing
                        foreach (var filePathOs in Directory.EnumerateFiles(rootPathOs, "*.*", SearchOption.AllDirectories))
                        {
                            var fileExtension = Path.GetExtension(filePathOs).ToLower();
                            if ((fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || fileExtension == ".gif")
                                && !filePathOs.Contains(ImageRenditionsFolderName))
                            {
                                imagesToProcess.Add(filePathOs);
                            }
                        }
                    }
                    else
                    {
                        // Create a full "OS" path of all the images that we need to process
                        foreach (var absolutePath in images)
                        {
                            imagesToProcess.Add($"{rootPathOs}{absolutePath}");
                        }
                    }

                    //
                    // => Images loop - process in smaller batches to limit memory usage
                    //
                    const int batchSize = 5; // Smaller batch size to reduce memory pressure
                    for (int i = 0; i < imagesToProcess.Count; i += batchSize)
                    {
                        var batch = imagesToProcess.Skip(i).Take(batchSize);

                        foreach (var imagePathOs in batch)
                        {
                            var relativeImagePath = imagePathOs.Replace(rootPathOs, "");
                            var thumbnailImageFilename = ThumbnailFilenameTemplate
                                .Replace("{filename}", Path.GetFileNameWithoutExtension(imagePathOs))
                                .Replace("{extension}", ThumbnailFileExtension);

                            var thumbnailPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeImagePath)}/{thumbnailImageFilename}";

                            // Ensure directory exists
                            var directoryPath = Path.GetDirectoryName(thumbnailPathOs);
                            if (!Directory.Exists(directoryPath))
                                Directory.CreateDirectory(directoryPath);

                            try
                            {
                                if (File.Exists(thumbnailPathOs))
                                    File.Delete(thumbnailPathOs);
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, $"Unable to remove original thumbnail file at '{thumbnailPathOs}'");
                            }

                            // - PNG and GIF needs conversion, but in the same size
                            var fileExtension = Path.GetExtension(imagePathOs).ToLower();
                            bool createJpegCopy = (fileExtension == ".png" || fileExtension == ".gif");
                            string? jpegPath = null;
                            
                            if (createJpegCopy)
                            {
                                jpegPath = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeImagePath)}/{Path.GetFileNameWithoutExtension(imagePathOs)}.jpg";
                                
                                try
                                {
                                    if (File.Exists(jpegPath))
                                        File.Delete(jpegPath);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to remove original rendition file at '{jpegPath}'");
                                }
                            }

                            // Process the image using ArrayPool
                            await ProcessImageWithArrayPoolAsync(imagePathOs, thumbnailPathOs, createJpegCopy, jpegPath);
                            
                            // Increment the counter appropriately
                            renditionsCreated++;
                            if (createJpegCopy) renditionsCreated++;
                        }
                        
                        // Force garbage collection after every batch
                        if (i + batchSize < imagesToProcess.Count)
                        {
                            // More aggressive GC strategy
                        // First collect gen 0 and 1 to clean up small objects
                        GC.Collect(1, GCCollectionMode.Forced, true);
                            // Then do a full collection for gen 2
                            GC.Collect(2, GCCollectionMode.Forced, true);
                            GC.WaitForPendingFinalizers();
                            // Request compaction to ensure memory is truly released back to the OS
                            GC.Collect(2, GCCollectionMode.Forced, true, true);
                        }
                    }
                }

                return new TaxxorReturnMessage(true, "Successfully generated image renditions", renditionsCreated.ToString(), debugInfo);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error converting graphs and images", $"error: {ex}, {debugInfo}");
            }
        }

        // Optimized file reading using ArrayPool
        private static async Task<(byte[] Buffer, int Length)> ReadFileWithArrayPoolAsync(string filePath)
        {
            // Get file size to allocate appropriate buffer
            var fileInfo = new FileInfo(filePath);
            int fileSize = (int)fileInfo.Length;
            
            // Use ArrayPool for large files to avoid large object heap allocations
            byte[] buffer = ArrayPool<byte>.Shared.Rent(fileSize);
            int bytesRead = 0;
            
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                {
                    bytesRead = await fileStream.ReadAsync(buffer, 0, fileSize);
                    // Ensure we've read the entire file
                    if (bytesRead < fileSize)
                    {
                        int currentPosition = bytesRead;
                        while (currentPosition < fileSize)
                        {
                            int additionalBytesRead = await fileStream.ReadAsync(buffer, currentPosition, fileSize - currentPosition);
                            if (additionalBytesRead == 0) break; // End of stream
                            currentPosition += additionalBytesRead;
                        }
                        bytesRead = currentPosition;
                    }
                }
            }
            catch
            {
                // Make sure to return the buffer to the pool even if an exception occurs
                ArrayPool<byte>.Shared.Return(buffer);
                throw;
            }
            
            // Return the buffer and the actual bytes read - caller is responsible for returning the buffer to the pool
            return (buffer, bytesRead);
        }

        // Helper method to process an image using ArrayPool
        private static async Task ProcessImageWithArrayPoolAsync(string sourcePath, string thumbnailPath, bool createJpegCopy = false, string? jpegPath = null)
        {
            // Rent the buffer from ArrayPool
            (byte[] buffer, int bytesRead) = await ReadFileWithArrayPoolAsync(sourcePath);
            
            try
            {
                // Process the image data using the rented buffer
                using (var memoryStream = new MemoryStream(buffer, 0, bytesRead))
                {
                    // Generate and save thumbnail
                    byte[] thumbnailBytes = null;
                    try 
                    {
                        thumbnailBytes = await RenderThumbnailImageAsync(memoryStream, ThumbnailMaxSize, Path.GetExtension(thumbnailPath));
                        await WriteFileWithStreamAsync(thumbnailPath, thumbnailBytes);
                    }
                    finally
                    {
                        // Help the GC by nulling large byte arrays when done with them
                        thumbnailBytes = null;
                    }
                    
                    // If we need to create a JPEG copy (for PNG/GIF files)
                    if (createJpegCopy && !string.IsNullOrEmpty(jpegPath))
                    {
                        byte[]? jpegBytes = null;
                        try
                        {
                            // Reset stream position
                            memoryStream.Position = 0;
                            
                            // Convert to JPEG
                            jpegBytes = await ConvertPngOrGifToJpegStreamAsync(memoryStream);
                            await WriteFileWithStreamAsync(jpegPath, jpegBytes);
                        }
                        finally
                        {
                            // Help the GC by nulling large byte arrays when done with them
                            jpegBytes = null;
                        }
                    }
                    
                    // Use memory compaction hints instead of NoGCRegion which can be problematic
                    // Just suggest to the GC that now would be a good time to collect
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }
            finally
            {
                // Always return the buffer to the pool
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = null; // Help GC by removing the reference
            }
        }

        // Helper methods that use streams instead of loading entire files into memory
        private static async Task<byte[]> RenderThumbnailImageAsync(Stream imageStream, int maxSize, string outputFormat)
        {
            // Implementation using ImageSharp that works with streams instead of byte arrays
            using (var memoryStream = new MemoryStream())
            {
                // Older ImageSharp version doesn't support ArrayPoolMemoryAllocator
                // Just use the default configuration
                using (var image = await Image.LoadAsync(imageStream))
                {
                    // Calculate new dimensions maintaining aspect ratio
                    int width = image.Width;
                    int height = image.Height;

                    if (width > height && width > maxSize)
                    {
                        height = (int)(height * ((float)maxSize / width));
                        width = maxSize;
                    }
                    else if (height > maxSize)
                    {
                        width = (int)(width * ((float)maxSize / height));
                        height = maxSize;
                    }

                    // Resize image using the older API style
                    image.Mutate(x => x.Resize(width, height));

                    // Save to memory stream with optimized encoder
                    if (outputFormat.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        outputFormat.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        var encoder = new JpegEncoder { Quality = 85 };
                        await image.SaveAsJpegAsync(memoryStream, encoder);
                    }
                    else
                    {
                        await image.SaveAsync(memoryStream, image.DetectEncoder(outputFormat));
                    }

                    // Clear image resources explicitly
                    image.Dispose();
                }

                // Return a copy of the data that will be managed by the caller
                var result = memoryStream.ToArray();
                return result;
            }
        }

        private static async Task<byte[]> ConvertPngOrGifToJpegStreamAsync(Stream imageStream)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Older ImageSharp version doesn't support ArrayPoolMemoryAllocator
                // Just use the default configuration
                using (var image = await Image.LoadAsync(imageStream))
                {
                    // Use an explicit encoder with optimal settings
                    var encoder = new JpegEncoder { Quality = 90 };
                    await image.SaveAsJpegAsync(memoryStream, encoder);
                    
                    // Explicitly dispose image resources
                    image.Dispose();
                }

                return memoryStream.ToArray();
            }
        }

        private static async Task WriteFileWithStreamAsync(string filePath, byte[] data)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await fileStream.WriteAsync(data, 0, data.Length);
                await fileStream.FlushAsync();
            }
        }

        /// <summary>
        /// Removes image renditions from the image library
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RemoveImageRenditions(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Retrieve posted data
            //
            var assetType = request.RetrievePostedValue("assettype", RegexEnum.None, true, ReturnTypeEnum.Xml, "");
            var imagesString = request.RetrievePostedValue("images", RegexEnum.None, true, ReturnTypeEnum.Xml, "");

            List<string> images = new List<string>();
            if (!string.IsNullOrEmpty(imagesString))
            {
                if (!imagesString.Contains(','))
                {
                    images.Add(imagesString);
                }
                else
                {
                    images.AddRange(imagesString.Split(','));
                }
            }

            var renditionRemoveResult = await RemoveImageRenditions(projectVars.projectId, assetType, images);
            if (renditionRemoveResult.Success)
            {
                await response.OK(renditionRemoveResult, ReturnTypeEnum.Xml, true);
            }
            else
            {
                await response.Error(renditionRemoveResult, ReturnTypeEnum.Xml, true);
            }
        }

        /// <summary>
        /// Removes image renditions from the image library
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="assetType"></param>
        /// <param name="images"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RemoveImageRenditions(string projectId, string assetType, List<string>? images = null)
        {
            // Image list can contain original image paths or rendition paths
            var renditionsRemoved = 0;

            var debugInfo = $"projectId: {projectId}";

            try
            {
                var nodeRenditionLocation = xmlApplicationConfiguration.SelectSingleNode($"/configuration/renditions/locations/location[@id='project{assetType}renditions']");

                var xPathProjects = $"/configuration/cms_projects/cms_project[@id='{projectId}']";

                var nodeCurrentProject = xmlApplicationConfiguration.SelectSingleNode(xPathProjects);

                if (nodeCurrentProject == null) return new TaxxorReturnMessage(false, "Unable to find project", $"projectId: {projectId}");

                // To calculate the paths we need to understand the project context
                var projectVars = new ProjectVariables();
                projectVars.projectId = projectId;
                projectVars = await FillProjectVariablesFromProjectId(projectVars);
                FillCorePathsInProjectVariables(ref projectVars);

                // Determine the folder where the data lives
                var rootPathOs = RetrieveImagesRootFolderPathOs(nodeCurrentProject);
                debugInfo += $", rootPathOs: {rootPathOs}";

                // Root path of the image renditions
                var imageRenditionsPathOs = CalculateFullPathOs(nodeRenditionLocation, null, projectVars);

                // Create a full "OS" path of all the images that we need to process
                var imagesToProcess = new List<string>();
                foreach (var absolutePath in images)
                {
                    imagesToProcess.Add($"{rootPathOs}{absolutePath}");
                }

                //
                // => Images loop
                //
                if (string.IsNullOrEmpty(imageRenditionsPathOs)) return new TaxxorReturnMessage(false, "Unable to calculate image rendition path");

                foreach (var imagePathOs in imagesToProcess)
                {
                    var imageRenditionPathOs = imagePathOs;

                    var relativeImagePath = imagePathOs.Replace(rootPathOs, "");

                    if (!imagePathOs.Contains($"/{ImageRenditionsFolderName}/"))
                    {
                        var thumbnailImageFilename = ThumbnailFilenameTemplate.Replace("{filename}", Path.GetFileNameWithoutExtension(imagePathOs)).Replace("{extension}", ThumbnailFileExtension);
                        imageRenditionPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeImagePath)}/{thumbnailImageFilename}";
                    }

                    // appLogger.LogCritical(imageRenditionPathOs);

                    if (File.Exists(imageRenditionPathOs))
                    {
                        try
                        {
                            File.Delete(imageRenditionPathOs);
                            renditionsRemoved++;
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Unable to remove image rendition with path '{imageRenditionPathOs}' from library");
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"Image rendition with path '{imageRenditionPathOs}' could not be found");
                    }

                    if (assetType == "drawings")
                    {
                        string[] arrExtensions = { ".jpg", ".png" };
                        foreach (var extension in arrExtensions)
                        {
                            imageRenditionPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeImagePath)}/{Path.GetFileNameWithoutExtension(imagePathOs)}{extension}";

                            if (File.Exists(imageRenditionPathOs))
                            {
                                try
                                {
                                    File.Delete(imageRenditionPathOs);
                                    renditionsRemoved++;
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to remove image rendition with path '{imageRenditionPathOs}' from library");
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"Image rendition with path '{imageRenditionPathOs}' could not be found");
                            }
                        }
                    }
                    else
                    {
                        // - PNG and GIF needs conversion, but in the same size
                        var fileExtension = Path.GetExtension(imagePathOs).ToLower();
                        if (fileExtension == ".png" || fileExtension == ".gif")
                        {
                            imageRenditionPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeImagePath)}/{Path.GetFileNameWithoutExtension(imagePathOs)}.jpg";

                            if (File.Exists(imageRenditionPathOs))
                            {
                                try
                                {
                                    File.Delete(imageRenditionPathOs);
                                    renditionsRemoved++;
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to remove image rendition with path '{imageRenditionPathOs}' from library");
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"Image rendition with path '{imageRenditionPathOs}' could not be found");
                            }
                        }
                    }



                }


                return new TaxxorReturnMessage(true, "Successfully removed imagerenditions", renditionsRemoved.ToString(), debugInfo);

            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error removing image renditions graphs and images", $"error: {ex}, {debugInfo}");
            }

        }

        /// <summary>
        /// Moves image renditions to a new location to match the new original image path
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task MoveImageRenditions(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Retrieve posted data
            //
            var assetType = request.RetrievePostedValue("assettype", RegexEnum.None, true, ReturnTypeEnum.Xml, "");
            var imagesString = request.RetrievePostedValue("images", RegexEnum.None, true, ReturnTypeEnum.Xml, "");

            Dictionary<string, string> imagesRenamed = new Dictionary<string, string>();
            if (!imagesString.Contains("|||"))
            {
                string[] imageData = imagesString.Split(",");
                if (imageData.Length == 2)
                {
                    imagesRenamed.Add(imageData[0], imageData[1]);
                }
                else
                {
                    HandleError("Not enough data supplied");
                }
            }
            else
            {
                string[] imageDataFragments = imagesString.Split("|||");
                foreach (var imageDataFragment in imageDataFragments)
                {
                    string[] imageData = imageDataFragment.Split(",");
                    if (imageData.Length == 2)
                    {
                        imagesRenamed.Add(imageData[0], imageData[1]);
                    }
                    else
                    {
                        HandleError("Not enough data supplied");
                    }
                }
            }

            var renditionMoveResult = await MoveImageRenditions(projectVars.projectId, assetType, imagesRenamed);
            if (renditionMoveResult.Success)
            {
                await response.OK(renditionMoveResult, ReturnTypeEnum.Xml, true);
            }
            else
            {
                appLogger.LogError(renditionMoveResult.ToString());
                await response.Error(renditionMoveResult, ReturnTypeEnum.Xml, true);
            }
        }

        /// <summary>
        /// Moves image renditions to a new location to match the new original image path
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="assetType"></param>
        /// <param name="imagesRenamed"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> MoveImageRenditions(string projectId, string assetType, Dictionary<string, string> imagesRenamed = null)
        {
            // Image list can contain original image paths or rendition paths
            var renditionsMoved = 0;
            var rootPathOs = "";
            var debugInfo = $"projectId: {projectId}";

            try
            {
                var nodeRenditionLocation = xmlApplicationConfiguration.SelectSingleNode($"/configuration/renditions/locations/location[@id='project{assetType}renditions']");

                var xPathProjects = $"/configuration/cms_projects/cms_project[@id='{projectId}']";

                var nodeCurrentProject = xmlApplicationConfiguration.SelectSingleNode(xPathProjects);

                if (nodeCurrentProject == null) return new TaxxorReturnMessage(false, "Unable to find project", $"projectId: {projectId}");

                // To calculate the paths we need to understand the project context
                var projectVars = new ProjectVariables();
                projectVars.projectId = projectId;
                projectVars = await FillProjectVariablesFromProjectId(projectVars);
                FillCorePathsInProjectVariables(ref projectVars);

                // Determine the folder where the data lives
                rootPathOs = RetrieveImagesRootFolderPathOs(nodeCurrentProject);
                debugInfo += $", rootPathOs: {rootPathOs}";

                // Root path of the image renditions
                var imageRenditionsPathOs = CalculateFullPathOs(nodeRenditionLocation, null, projectVars);

                // Create a full "OS" path of all the images that we need to process
                var imagesToProcess = new Dictionary<string, string>();
                foreach (var imageInfo in imagesRenamed)
                {
                    var orginalPath = imageInfo.Key;
                    var newPath = imageInfo.Value;
                    imagesToProcess.Add($"{rootPathOs}{orginalPath}", $"{rootPathOs}{newPath}");
                }

                //
                // => Images loop
                //
                if (string.IsNullOrEmpty(imageRenditionsPathOs)) return new TaxxorReturnMessage(false, "Unable to calculate image rendition path");

                foreach (var imageInformation in imagesToProcess)
                {
                    var sourceOriginalPathOs = imageInformation.Key;
                    var sourceNewPathOs = imageInformation.Value;



                    var relativeOriginalImagePath = sourceOriginalPathOs.Replace(rootPathOs, "");
                    var thumbnailOriginalImageFilename = ThumbnailFilenameTemplate.Replace("{filename}", Path.GetFileNameWithoutExtension(sourceOriginalPathOs)).Replace("{extension}", ThumbnailFileExtension);
                    var thumbnailOriginalPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeOriginalImagePath)}/{thumbnailOriginalImageFilename}".Replace("//", "/");

                    var relativeNewImagePath = sourceNewPathOs.Replace(rootPathOs, "");
                    var thumbnailNewImageFilename = ThumbnailFilenameTemplate.Replace("{filename}", Path.GetFileNameWithoutExtension(sourceNewPathOs)).Replace("{extension}", ThumbnailFileExtension);
                    var thumbnailNewPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeNewImagePath)}/{thumbnailNewImageFilename}".Replace("//", "/"); ;

                    var thumbnailMoveResult = MoveImageRendition(thumbnailOriginalPathOs, thumbnailNewPathOs);
                    if (!thumbnailMoveResult.Success) return thumbnailMoveResult;

                    if (assetType == "drawings")
                    {
                        // The renditions folder contains jpg and png renditions of the original file which we need to mark as inuse
                        string[] arrExtensions = { ".jpg", ".png" };
                        foreach (var extension in arrExtensions)
                        {
                            var imageRenditionOriginalPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeOriginalImagePath)}/{Path.GetFileNameWithoutExtension(sourceOriginalPathOs)}{extension}".Replace("//", "/"); ;
                            var imageRenditionNewPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeNewImagePath)}/{Path.GetFileNameWithoutExtension(sourceNewPathOs)}{extension}".Replace("//", "/"); ;

                            var imageRenditionMoveResult = MoveImageRendition(imageRenditionOriginalPathOs, imageRenditionNewPathOs);
                            if (!imageRenditionMoveResult.Success) return imageRenditionMoveResult;
                        }
                    }
                    else
                    {
                        // - PNG and GIF needs conversion, but in the same size
                        var fileExtension = Path.GetExtension(sourceOriginalPathOs).ToLower();
                        if (fileExtension == ".png" || fileExtension == ".gif")
                        {
                            var imageRenditionOriginalPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeOriginalImagePath)}/{Path.GetFileNameWithoutExtension(sourceOriginalPathOs)}.jpg".Replace("//", "/"); ;
                            var imageRenditionNewPathOs = $"{imageRenditionsPathOs}{Path.GetDirectoryName(relativeNewImagePath)}/{Path.GetFileNameWithoutExtension(sourceNewPathOs)}.jpg".Replace("//", "/"); ;

                            var imageRenditionMoveResult = MoveImageRendition(imageRenditionOriginalPathOs, imageRenditionNewPathOs);
                            if (!imageRenditionMoveResult.Success) return imageRenditionMoveResult;
                        }
                    }



                }


                return new TaxxorReturnMessage(true, "Successfully moved imagerenditions", renditionsMoved.ToString(), debugInfo);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error moving image renditions graphs and images", $"error: {ex}, {debugInfo}");
            }


            TaxxorReturnMessage MoveImageRendition(string renditionSourcePathOs, string renditionTargetPathOs)
            {
                var renditionTargetFolderPathOs = Path.GetDirectoryName(renditionTargetPathOs);
                try
                {
                    if (!Directory.Exists(renditionTargetFolderPathOs)) Directory.CreateDirectory(renditionTargetFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "There was an error creating new folder for image rendition", ex);
                }

                // Move the thumbnail image
                try
                {
                    if (File.Exists(renditionSourcePathOs))
                    {
                        if (File.Exists(renditionTargetPathOs))
                        {
                            var pathToDump = renditionTargetPathOs.Replace(rootPathOs, "");
                            return new TaxxorReturnMessage(false, $"Could not move image rendition, because the target file '{pathToDump}' already exists", renditionsMoved.ToString(), $"thumbnailOriginalPathOs: {renditionSourcePathOs}, thumbnailNewPathOs: {renditionTargetPathOs}, {debugInfo}");
                        }
                        else
                        {
                            File.Move(renditionSourcePathOs, renditionTargetPathOs);
                            renditionsMoved++;
                        }
                    }
                    else
                    {
                        var pathToDump = renditionSourcePathOs.Replace(rootPathOs, "");
                        return new TaxxorReturnMessage(false, $"Unable to find the original thumbnail or rendition file '{pathToDump}' to move", renditionsMoved.ToString(), $"thumbnailOriginalPathOs: {renditionSourcePathOs}, {debugInfo}");
                    }
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "There was a problem moving the thumbnail or rendition file", ex);
                }

                return new TaxxorReturnMessage(true, "Successfully moved image rendition or thumbnail");
            }

        }

        /// <summary>
        /// Renders content that the Taxxor Editor can use to generate graphs with
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RetrieveContentForGraphGeneration(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            //
            // => Retrieve posted data
            //
            string sectionIds = request.RetrievePostedValue("sectionids", null);
            string dataRefs = request.RetrievePostedValue("datarefs", null);

            var retrieveContentResult = await RetrieveContentForGraphGeneration(sectionIds, dataRefs);
            if (retrieveContentResult.Success)
            {
                await response.OK(retrieveContentResult, ReturnTypeEnum.Xml, true);
            }
            else
            {
                await response.Error(retrieveContentResult, ReturnTypeEnum.Xml, true);
            }
        }

        /// <summary>
        /// Renders the base content for graph generation
        /// </summary>
        /// <param name="comparisonMode"></param>
        /// <param name="sectionIds"></param>
        /// <param name="dataRefs"></param>
        /// <returns></returns>
        public static async Task<TaxxorLogReturnMessage> RetrieveContentForGraphGeneration(string sectionIds, string dataRefs)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = siteType == "local" || siteType == "dev";

            // Logs
            var successMessage = "";
            var warningMessage = "";
            var errorMessage = "";
            var informationMessage = "";

            // Overall result that we will fill while running through the procedure
            var logInfo = new LogInfo();

            var onlyDataReferencesInUse = true;
            var dataReferencesInUseFilter = "";
            if (onlyDataReferencesInUse) dataReferencesInUseFilter = " and metadata/entry[@key='inuse']='true'";


            if (string.IsNullOrEmpty(sectionIds) && string.IsNullOrEmpty(dataRefs)) return new TaxxorLogReturnMessage(false, "Unable to retrieve information for graph rendering", logInfo, $"No section ID's and no datareferences supplied");


            // Calculate the base path to the xml file that contains the section XML file
            var xpath = $"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/content_types/content_management/type[@id='regular']/xml";
            var nodeSourceDataLocation = xmlApplicationConfiguration.SelectSingleNode(xpath);
            if (nodeSourceDataLocation == null)
            {
                return new TaxxorLogReturnMessage(false, "Could not create graph source data because the project could not be located", logInfo, $"xpath: {xpath}, stack-trace: {GetStackTrace()}");
            }

            try
            {
                var rootPathOs = RetrieveImagesRootFolderPathOs(projectVars.projectId);
                var contentPath = "/_contents.xml";
                var contentPathOs = rootPathOs + contentPath;

                //
                // => Make sure that the contents file of the image library is up to date
                //                
                XmlDocument? xmlImageLibraryContents = null;
                var assetInfoRetrieveResult = await RetrieveProjectAssetsInformation(projectVars.projectId, "projectimages");
                if (assetInfoRetrieveResult.Success)
                {
                    xmlImageLibraryContents = new XmlDocument();
                    xmlImageLibraryContents.ReplaceContent(assetInfoRetrieveResult.XmlPayload);

                    await xmlImageLibraryContents.SaveAsync(contentPathOs, true, true);

                    informationMessage = "Successfully rendered the project assets information file";
                    appLogger.LogInformation(informationMessage);
                    logInfo.InformationLog.Add(informationMessage);
                }
                else
                {
                    errorMessage = "Unable to update the project assets information file";
                    appLogger.LogError(errorMessage);
                    logInfo.ErrorLog.Add(errorMessage);
                }


                //
                // => Loop through existing graph renditions and gather information (associated data-ref, wrapper information, date modified, date last accessed)
                //
                Dictionary<string, List<GraphRenditionProperties>> dictGraphRenditions = [];
                var nodeListSvgGraphRenditions = xmlImageLibraryContents.SelectNodes("/projects/project/dir//dir[@path='/_renditions/graphs']//file[contains(@name, '.svg')]");
                foreach (XmlNode nodeGraphRendition in nodeListSvgGraphRenditions)
                {
                    var graphRenditionProperties = new GraphRenditionProperties(nodeGraphRendition);
                    if (!dictGraphRenditions.ContainsKey(graphRenditionProperties.DataReference))
                    {
                        dictGraphRenditions.Add(graphRenditionProperties.DataReference, new List<GraphRenditionProperties>());
                    }
                    dictGraphRenditions[graphRenditionProperties.DataReference].Add(graphRenditionProperties);
                }



                // The XML Document that we will be building up and return
                Dictionary<string, List<DataReferenceGraphInfo>> dictDataReferenceGraphInformation = [];
                var xmlDocument = new XmlDocument();
                xmlDocument.AppendChild(xmlDocument.CreateElement("div"));
                var targetDocumentRootNode = xmlDocument.DocumentElement;
                targetDocumentRootNode.SetAttribute("class", "body-wrapper");

                var xmlSectionDataFolderPathOs = RetrieveFilingDataFolderPathOs(projectVars.projectId);

                //
                // => Return all the content that contains graph data
                //
                var dataRefFilter = "";
                if (sectionIds != "all")
                {
                    // Map passed section id's to data refs
                    if (sectionIds.Contains(','))
                    {
                        // TODO: needs to be implemented - lookup data references from the site structure overview document
                        errorMessage = $"Section id's method not implemented yet";
                        logInfo.AddErrorLogEntry("Section id's method not implemented yet");
                        return new TaxxorLogReturnMessage(false, errorMessage, logInfo);
                    }
                    else
                    {
                        var dataRefPathOs = RetrieveInlineFilingComposerXmlPathOs(reqVars, projectVars, sectionIds, debugRoutine);
                        logInfo.AddInformationLogEntry($"- dataRefPathOs: {dataRefPathOs}");
                        var dataRef = Path.GetFileName(dataRefPathOs);
                        logInfo.AddInformationLogEntry($"- dataRef: {dataRef}");
                        if (!string.IsNullOrEmpty(dataRef))
                        {
                            dataRefFilter = $" and @ref='{dataRef}'";
                        }
                        else
                        {
                            errorMessage = "Data reference could not be located in the structure";
                            logInfo.AddErrorLogEntry(errorMessage);
                            return new TaxxorLogReturnMessage(false, errorMessage, logInfo);
                        }
                    }
                }
                else if (dataRefs != "all")
                {
                    // Use data refs in xpath selector
                    if (dataRefs.Contains(','))
                    {
                        dataRefFilter += " and (";
                        string[] arrDataRefs = dataRefs.Split(",");
                        for (int i = 0; i < arrDataRefs.Length; i++)
                        {
                            dataRefFilter += $"@ref='{arrDataRefs[i]}'";
                            if (i < (arrDataRefs.Length - 1)) dataRefFilter += " or ";
                        }
                        dataRefFilter += ")";
                    }
                    else
                    {
                        dataRefFilter = $" and @ref='{dataRefs}'";
                    }
                }
                var xPathContentFiles = $"/projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/data/content[@datatype='sectiondata' and metadata/entry[@key='hasgraphs']='true'{dataReferencesInUseFilter}{dataRefFilter}]";
                logInfo.AddInformationLogEntry($"xPathContentFiles: {xPathContentFiles}");


                var nodeListContentItems = XmlCmsContentMetadata.SelectNodes(xPathContentFiles);
                if (nodeListContentItems.Count == 0)
                {
                    var xmlContentMetadataProject = CompileCmsMetadata(projectVars.projectId, false);
                    nodeListContentItems = xmlContentMetadataProject.SelectNodes(xPathContentFiles);
                }
                foreach (XmlNode nodeContentItem in nodeListContentItems)
                {
                    var dataRef = GetAttribute(nodeContentItem, "ref");

                    //
                    // => Retrieve graph renditions information
                    //
                    var graphInformationList = new List<GraphRenditionProperties>();
                    if (dictGraphRenditions.ContainsKey(dataRef))
                    {
                        graphInformationList.AddRange(dictGraphRenditions[dataRef]);
                    }
                    // Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
                    // Console.WriteLine($"GraphRenditionProperties.Count: {graphInformationList.Count}");
                    // Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");



                    logInfo.InformationLog.Add($"Processing {dataRef}");

                    // Calculate the path to the folder that contains the data files
                    var sectionDataFilePathOs = $"{xmlSectionDataFolderPathOs}/{dataRef}";

                    try
                    {
                        var xmlSection = LoadAndResolveInlineFilingComposerData(reqVars, projectVars, sectionDataFilePathOs, false);
                        if (XmlContainsError(xmlSection))
                        {
                            errorMessage = $"Unable to load {dataRef}. {ConvertErrorXml(xmlSection)}";
                            appLogger.LogError(errorMessage);
                            logInfo.ErrorLog.Add(errorMessage);
                            continue;
                        }

                        // Append the articles to the document
                        var nodeListArticles = xmlSection.SelectNodes("/data/content/article");
                        foreach (XmlNode nodeArticle in nodeListArticles)
                        {
                            var contentLang = nodeArticle.SelectSingleNode("ancestor::content")?.GetAttribute("lang") ?? "unknown";
                            nodeArticle.SetAttribute("lang", contentLang);
                            nodeArticle.SetAttribute("data-ref", dataRef);
                            var nodeArticleImported = xmlDocument.ImportNode(nodeArticle, false);
                            var nodeDiv = xmlDocument.CreateElement("div");
                            var nodeSection = xmlDocument.CreateElement("section");

                            // Import the header so that we can easier debug
                            var nodeHeader = RetrieveFirstHeaderNode(nodeArticle);
                            if (nodeHeader != null)
                            {
                                var nodeHeaderImported = xmlDocument.ImportNode(nodeHeader, true);
                                nodeSection.AppendChild(nodeHeaderImported);
                            }


                            var nodeListChartWrappers = nodeArticle.SelectNodes($"*{RetrieveGraphElementsBaseXpath()}");
                            foreach (XmlNode nodeChartWraper in nodeListChartWrappers)
                            {
                                //
                                // => Create an object containing the relevant information about the graph that we have found
                                //
                                var dataReferenceGraphInfo = new DataReferenceGraphInfo(nodeContentItem);
                                dataReferenceGraphInfo.DataReference = dataRef;
                                var wrapperId = nodeChartWraper.GetAttribute("id");
                                if (string.IsNullOrEmpty(wrapperId))
                                {
                                    var randomId = RandomString(10);
                                    warningMessage = $"Found a graph data table in {dataRef} without a wrapper element id.";
                                    logInfo.WarningLog.Add(warningMessage);
                                    appLogger.LogWarning(warningMessage);
                                    nodeChartWraper.SetAttribute("id", randomId);
                                    wrapperId = randomId;
                                }
                                else
                                {
                                    if (wrapperId.Contains('_')) wrapperId = wrapperId.SubstringAfter("_");
                                }

                                dataReferenceGraphInfo.GraphId = wrapperId;
                                dataReferenceGraphInfo.Language = contentLang;
                                dataReferenceGraphInfo.GenerateRenditionFileName();


                                if (!dictDataReferenceGraphInformation.ContainsKey(dataRef))
                                {
                                    dictDataReferenceGraphInformation.Add(dataRef, []);
                                }
                                dictDataReferenceGraphInformation[dataRef].Add(dataReferenceGraphInfo);

                                //
                                // => Test the type of table used for this graph
                                //
                                var wrapperClass = nodeChartWraper.GetAttribute("class") ?? "unknown";
                                var isExcelTable = wrapperClass.Contains("external-table");
                                var usesStructuredData = false;
                                if (isExcelTable)
                                {
                                    // Find the cached table file in the metadata reference file to test for the dates
                                    var tableCacheFileName = CalculateCachedExternalTableFileName(wrapperId);
                                    var xPathTableCacheFile = $"/projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/data/content[@datatype='tablecache' and @ref='{tableCacheFileName}']";
                                    var nodeTableCacheFile = XmlCmsContentMetadata.SelectSingleNode(xPathTableCacheFile);
                                    if (nodeTableCacheFile != null)
                                    {
                                        dataReferenceGraphInfo.UpdateDates(nodeTableCacheFile);
                                    }
                                    else
                                    {
                                        warningMessage = $"Cache file for table with ID '{wrapperId}' in {dataRef} not found";
                                        appLogger.LogWarning(warningMessage);
                                        logInfo.WarningLog.Add(warningMessage);
                                    }
                                }
                                else
                                {
                                    // Test if there are SDE's used in the data table
                                    var nodeListSdes = nodeChartWraper.SelectNodes("*//td//*[@data-fact-id]");
                                    if (nodeListSdes.Count > 0)
                                    {
                                        usesStructuredData = true;
                                        // Find the SDE cache file to test for the date
                                        var sdeCacheFileName = $"__structured-data--{dataRef}";
                                        var xPathTableCacheFile = $"/projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/data/content[@datatype='structureddatacache' and @ref='{sdeCacheFileName}']";
                                        var nodeSdeCacheFile = XmlCmsContentMetadata.SelectSingleNode(xPathTableCacheFile);
                                        if (nodeSdeCacheFile != null)
                                        {
                                            dataReferenceGraphInfo.UpdateDates(nodeSdeCacheFile);
                                        }
                                    }
                                }

                                if (dataRef.Contains("2041643-our-businesses"))
                                {
                                    appLogger.LogInformation("debugging");
                                }

                                //
                                // => Determine if we need to add this graph to the response or not
                                //
                                var reason = "Default is to exclude";
                                var addGraphToResponse = false;
                                GraphRenditionProperties? graphRenditionProperties = null;
                                foreach (var graphRenditionProps in graphInformationList)
                                {
                                    if (graphRenditionProps.RenditionFileName == dataReferenceGraphInfo.RenditionFileName)
                                    {
                                        graphRenditionProps.Orphaned = false;
                                        graphRenditionProperties = graphRenditionProps;
                                        break;
                                    }
                                }

                                if (graphRenditionProperties == null)
                                {
                                    // This is a new graph so we need to add the data it to the output
                                    reason = "Added because graph is new";
                                    addGraphToResponse = true;
                                }
                                else
                                {
                                    // If the rendition date is after the date of the data reference or the cache file date, then this graph rendition needs to be rendered
                                    if (graphRenditionProperties.DateModified.IsBefore(dataReferenceGraphInfo.DateModified) || graphRenditionProperties.DateLastAccessed.IsBefore(dataReferenceGraphInfo.DateLastAccessed))
                                    {
                                        reason = "Added because graph rendition is too old";
                                        addGraphToResponse = true;
                                    }
                                    else
                                    {
                                        reason = "No need to re-render the graph rendition as it's still valid";
                                    }
                                }

                                // Add the reason for adding or removing the content
                                var reasonToReport = $"{reason} ({dataReferenceGraphInfo.GraphId}, {dataReferenceGraphInfo.Language}, isExcelTable: {isExcelTable}, usesStructuredData: {usesStructuredData})";
                                logInfo.InformationLog.Add(reasonToReport);
                                var nodeComment = xmlDocument.CreateComment($" ### {reasonToReport} ### ");
                                nodeSection.AppendChild(nodeComment);


                                if (addGraphToResponse)
                                {
                                    // Fix the chart content wrapper node
                                    var nodeListChartContentWrappers = nodeChartWraper.SelectNodes("div[@class='chart-content']");
                                    foreach (XmlNode nodeChartContentWrapper in nodeListChartContentWrappers)
                                    {
                                        // - Remove non essential attributes

                                        // 1) Find attribute names that match the wildcard
                                        var attributeNamesToRemove = new List<string>();
                                        foreach (XmlAttribute xmlAttribute in nodeChartContentWrapper.Attributes)
                                        {
                                            if (xmlAttribute != null)
                                            {
                                                if (xmlAttribute.Name != "id" && xmlAttribute.Name != "class")
                                                {
                                                    attributeNamesToRemove.Add(xmlAttribute.Name);
                                                }
                                            }
                                        }

                                        // 2) Remove the attribute from the collection
                                        foreach (string attributeName in attributeNamesToRemove)
                                        {
                                            nodeChartContentWrapper.Attributes.Remove(nodeChartContentWrapper.Attributes[attributeName]);
                                        }

                                        // - Remove the content
                                        var nodeListChartContent = nodeChartContentWrapper.SelectNodes("*");
                                        RemoveXmlNodes(nodeListChartContent);

                                        // - Make sure that the div does not auto-close
                                        nodeChartContentWrapper.InnerText = "  ";


                                    }


                                    var nodeChartWrapperImported = xmlDocument.ImportNode(nodeChartWraper, true);
                                    nodeSection.AppendChild(nodeChartWrapperImported);
                                }


                            }

                            nodeDiv.AppendChild(nodeSection);
                            nodeArticleImported.AppendChild(nodeDiv);


                            // Add the article content
                            xmlDocument.DocumentElement.AppendChild(nodeArticleImported);
                        }


                    }
                    catch (Exception ex)
                    {
                        errorMessage = $"Unable to load {dataRef} and append it to the result";
                        appLogger.LogError(ex, errorMessage);
                        logInfo.ErrorLog.Add(errorMessage);
                    }

                }

                //
                // => Delete orphaned graph renderings
                //
                if (sectionIds == "all" && dataRefs == "all")
                {
                    string? renditionsRootPathOs = null;
                    if (xmlImageLibraryContents != null)
                    {
                        var nodeProjectRoot = xmlImageLibraryContents.SelectSingleNode("/projects/project[@rootfolderpathos]");
                        if (nodeProjectRoot != null)
                        {
                            var baseImageFolderPathOs = nodeProjectRoot.GetAttribute("rootfolderpathos");
                            renditionsRootPathOs = $"{baseImageFolderPathOs}/{ImageRenditionsFolderName}/graphs";
                            if (!Directory.Exists(renditionsRootPathOs))
                            {
                                appLogger.LogWarning($"Unable to locate graph renditions directory at: {renditionsRootPathOs}");
                                renditionsRootPathOs = null;
                            }
                        }
                    }
                    if (renditionsRootPathOs != null)
                    {
                        var orphanedRenderings = new List<string>();
                        foreach (KeyValuePair<string, List<GraphRenditionProperties>> entry in dictGraphRenditions)
                        {
                            // do something with entry.Value or entry.Key
                            foreach (var graphRenditionProperties in entry.Value)
                            {
                                appLogger.LogDebug($"{graphRenditionProperties.RenditionFileName}: orphaned={graphRenditionProperties.Orphaned}");
                                if (graphRenditionProperties.Orphaned) orphanedRenderings.Add(graphRenditionProperties.RenditionFileName);
                            }
                        }

                        if (debugRoutine)
                        {
                            Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
                            Console.WriteLine($"orphanedRenderings: {string.Join(",", orphanedRenderings)}");
                            Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
                        }

                        foreach (var graphRenditionFileName in orphanedRenderings)
                        {
                            string[] extensions = { ".svg", ".jpg", ".png" };
                            var baseGraphRenditionFileName = Path.GetFileNameWithoutExtension(graphRenditionFileName);
                            foreach (var extension in extensions)
                            {
                                var graphRenditionPathOs = $"{renditionsRootPathOs}/{baseGraphRenditionFileName}{extension}";
                                try
                                {
                                    File.Delete(graphRenditionPathOs);
                                    informationMessage = $"Deleted orphaned graph rendition {Path.GetFileName(graphRenditionPathOs)}";
                                    appLogger.LogInformation(informationMessage);
                                    logInfo.InformationLog.Add(informationMessage);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Could not remove graph rendition at {graphRenditionPathOs}");
                                    logInfo.ErrorLog.Add($"Unable to remove graph rendition {Path.GetFileName(graphRenditionPathOs)}");
                                }
                            }
                        }

                        if (orphanedRenderings.Count > 0)
                        {
                            assetInfoRetrieveResult = await RetrieveProjectAssetsInformation(projectVars.projectId, "projectimages");
                            if (assetInfoRetrieveResult.Success)
                            {
                                await xmlImageLibraryContents.SaveAsync(contentPathOs, true, true);

                                xmlImageLibraryContents.ReplaceContent(assetInfoRetrieveResult.XmlPayload);

                                informationMessage = "Successfully rendered the project assets information file after removal of orphaned graph renderings";
                                appLogger.LogInformation(informationMessage);
                                logInfo.InformationLog.Add(informationMessage);
                            }
                            else
                            {
                                errorMessage = "Unable to update the project assets information file after removal of orphaned graph renderings";
                                appLogger.LogError(errorMessage);
                                logInfo.ErrorLog.Add(errorMessage);
                            }
                        }
                    }
                }

                successMessage = "Successfully compiled the graph content";
                logInfo.AddSuccessLogEntry(successMessage);
                return new TaxxorLogReturnMessage(true, successMessage, xmlDocument, logInfo);
            }
            catch (HttpStatusCodeException)
            {
                // Thrown from HandleError() method - assures that the error handling will continue and is not caught by the code block below
                throw;
            }
            catch (Exception ex)
            {
                errorMessage = $"There was an error retrieving the data for graph generation.";
                appLogger.LogError(ex, errorMessage);
                logInfo.ErrorLog.Add(errorMessage);
                return new TaxxorLogReturnMessage(false, errorMessage, logInfo);
            }
        }


        /// <summary>
        /// Injects SVG graph data into the "PDF" content when it is being loaded to ensure it is always the most recent version
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static XmlDocument ProcessGraphsOnLoad(XmlDocument xmlDocument, ProjectVariables projectVars)
        {
            // Stopwatch stopwatch = new Stopwatch();
            // stopwatch.Start();

            try
            {
                // Check if there are any graphs to process
                var nodeListGraphElements = xmlDocument.SelectNodes($"/data/content[@lang='{projectVars.outputChannelVariantLanguage}']/*{RetrieveGraphElementsBaseXpath(true)}");

                // 20404038-inclusion-diversity---xid3430585293495605175---en.svg
                if (nodeListGraphElements.Count > 0)
                {
                    appLogger.LogInformation($"Need to process {nodeListGraphElements.Count} graphs");

                    string[] extensions = [".jpg", ".png"];

                    // Retrieve the contents of the image library
                    var rootPathOs = RetrieveImagesRootFolderPathOs(projectVars.projectId);
                    var graphRenditionsPath = "/_renditions/graphs";
                    var graphRenditionsRootPathOs = $"{rootPathOs}{graphRenditionsPath}";
                    if (!Directory.Exists(graphRenditionsRootPathOs)) throw new Exception($"Unable to locate graph renditions directory at: {graphRenditionsRootPathOs}");

                    foreach (XmlNode nodeGraphElement in nodeListGraphElements)
                    {
                        var nodeArticle = nodeGraphElement.SelectSingleNode("ancestor::article");
                        if (nodeArticle != null)
                        {
                            var articleId = nodeArticle.GetAttribute("id") ?? "undefined";
                            var tableId = nodeGraphElement.GetAttribute("id") ?? "undefined";
                            appLogger.LogInformation($"Processing graph for article: {articleId}");
                            if (articleId != "undefined" && tableId != "undefined")
                            {
                                var baseFileName = $"{Path.GetFileNameWithoutExtension(articleId)}---{tableId.SubstringAfter("_")}---{projectVars.outputChannelVariantLanguage}";
                                var svgFilePathOs = $"{graphRenditionsRootPathOs}/{baseFileName}.svg";
                                if (File.Exists(svgFilePathOs))
                                {
                                    var nodeGraphWrapper = nodeGraphElement.SelectSingleNode("div[@class='tx-renderedchart']");
                                    var nodeGraphWrapperElements = nodeGraphWrapper.SelectNodes("div");
                                    if (nodeGraphWrapperElements.Count > 0) RemoveXmlNodes(nodeGraphWrapperElements);
                                    var nodeDiv = xmlDocument.CreateElement("div");
                                    var nodeGraphContent = nodeGraphWrapper.AppendChild(nodeDiv);
                                    if (nodeGraphContent != null)
                                    {
                                        var errorSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""300"" height=""100"" viewBox=""0 0 300 100"">
                                                <rect width=""100%"" height=""100%"" fill=""#fff8f0"" stroke=""#e67e22"" stroke-width=""2""/>
                                                <path d=""M40,30 L60,70 L20,70 Z"" fill=""#e67e22""/>
                                                <text x=""36"" y=""63"" font-family=""Arial"" font-size=""20"" fill=""white"">!</text>
                                                <text x=""75"" y=""55"" font-family=""Arial"" font-size=""14"" fill=""#e67e22"">Corrupt SVG contents</text>
                                            </svg>";

                                        try
                                        {
                                            // Use stream reading with ArrayPool to reduce memory usage
                                            var svgContent = string.Empty;
                                            
                                            using (var fileStream = new FileStream(svgFilePathOs, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                                            {
                                                // Get file size for buffer allocation
                                                var fileSize = (int)fileStream.Length;
                                                var buffer = ArrayPool<byte>.Shared.Rent(fileSize);
                                                
                                                try 
                                                {
                                                    fileStream.ReadExactly(buffer, 0, fileSize);
                                                    svgContent = Encoding.UTF8.GetString(buffer, 0, fileSize).Replace("\0", "");
                                                }
                                                finally 
                                                {
                                                    ArrayPool<byte>.Shared.Return(buffer);
                                                }
                                            }

                                            // Parse the SVG content into an XDocument
                                            XDocument svgXDocument = XDocument.Parse(svgContent);

                                            // Safely remove the style attribute from the root svg element if it exists
                                            svgXDocument.Root.Attribute("style")?.Remove();

                                            // Check the size of the svg contents and throw a warning if it's too small
                                            var svgSource = svgXDocument.ToString();
                                            if (svgSource.Length < 25)
                                            {
                                                // Create a warning SVG to replace the corrupt content
                                                svgSource = errorSvg; ;

                                                appLogger.LogWarning($"Graph SVG content is too small in file size for articleId: {articleId}, tableId: {tableId}, svgpath: {svgFilePathOs}");
                                            }

                                            // Now you can use the modifiedSvgContent string to update the graph content in your XML document
                                            nodeGraphContent.InnerXml = svgSource;
                                        }
                                        catch (Exception ex)
                                        {
                                            appLogger.LogError(ex, $"Error parsing SVG content for articleId: {articleId}, tableId: {tableId}, svgpath: {svgFilePathOs}");

                                            // Insert the error SVG to the graph content
                                            nodeGraphContent.InnerXml = errorSvg;
                                        }


                                        // Add alternative binary renderings of the graph SVG image to the wrapper element
                                        foreach (var extension in extensions)
                                        {
                                            var filePath = $"{graphRenditionsPath}/{baseFileName}{extension}";
                                            if (File.Exists($"{rootPathOs}/{filePath}"))
                                            {
                                                nodeGraphContent.ParentNode.SetAttribute($"data-alternative-{extension[1..]}", filePath);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        appLogger.LogError($"Could not find div with class 'tx-renderedchart'");
                                    }
                                }
                                else
                                {
                                    appLogger.LogError($"Graph file not found for articleId: {articleId}, tableId: {tableId}");
                                }

                            }
                            else
                            {
                                appLogger.LogWarning($"Missing information for rendering graph element, articleId: {articleId}, tableId: {tableId}");
                            }
                        }
                        else
                        {
                            appLogger.LogWarning("Could not find article node for graph element");
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Make sure that we log the issue
                appLogger.LogError(ex, "There was a problem processing the graph SVG content on load");

                // Rethrow the error, to make sure that the loading logic stops
                throw;
            }

            // stopwatch.Stop();
            // TimeSpan elapsedTime = stopwatch.Elapsed;
            // appLogger.LogInformation($"Graph processing completed in {elapsedTime.TotalMilliseconds} milliseconds");


            return xmlDocument;

        }


        /// <summary>
        /// Contains information about a graph rendition stored on the disk
        /// </summary>
        public class GraphRenditionProperties
        {
            public string? RenditionFileName { get; set; } = null;
            public string? DataReference { get; set; } = null;
            public string? GraphId { get; set; } = null;
            public string? Language { get; set; } = null;
            public DateTime DateModified { get; set; } = new DateTime();
            public DateTime DateLastAccessed { get; set; } = new DateTime();
            public bool Orphaned { get; set; } = true;

            public GraphRenditionProperties() { }

            public GraphRenditionProperties(string graphRenditionPath)
            {
                // Filename example: 1166807-independent-auditor-s-report---uid-1282158309---en
                var graphRenditionFileName = graphRenditionPath;
                if (graphRenditionPath.Contains('/') || graphRenditionPath.Contains('\\'))
                {
                    graphRenditionFileName = Path.GetFileName(graphRenditionPath);
                }

                this.RenditionFileName = graphRenditionFileName;
                _parseGraphRenditionFileName(graphRenditionFileName);
            }


            public GraphRenditionProperties(XmlNode nodeFile)
            {
                var graphRenditionFileName = nodeFile.GetAttribute("name");
                this.RenditionFileName = graphRenditionFileName;
                _parseGraphRenditionFileName(graphRenditionFileName);

                this.DateModified = DateTime.Parse(nodeFile.GetAttribute("datemodified"));
                this.DateLastAccessed = DateTime.Parse(nodeFile.GetAttribute("dateaccessed"));
            }


            private void _parseGraphRenditionFileName(string graphRenditionFileName)
            {
                string[] graphOriginInformation = graphRenditionFileName.Split("---");
                if (graphOriginInformation.Length == 3)
                {
                    this.DataReference = $"{graphOriginInformation[0]}.xml";
                    this.GraphId = graphOriginInformation[1];
                    this.Language = graphOriginInformation[2];
                }
            }

        }


        /// <summary>
        /// Information about the graph in an XML data file
        /// </summary>
        public class DataReferenceGraphInfo : GraphRenditionProperties
        {



            public string? ExternalSourceId { get; set; } = null;
            public DataReferenceGraphInfo(XmlNode nodeCacheItem)
            {

                this.DateModified = DateTime.Parse(nodeCacheItem.GetAttribute("datemodified"));
                this.DateLastAccessed = DateTime.Parse(nodeCacheItem.GetAttribute("dateaccessed"));
            }

            public void UpdateDates(XmlNode nodeFileInfo)
            {
                var newDateModified = new DateTime();
                var newDateLastAccessed = new DateTime();
                newDateModified = DateTime.Parse(nodeFileInfo.GetAttribute("datemodified"));
                newDateLastAccessed = DateTime.Parse(nodeFileInfo.GetAttribute("dateaccessed"));

                if (newDateModified.IsAfter(this.DateModified)) this.DateModified = newDateModified;
                if (newDateLastAccessed.IsAfter(this.DateLastAccessed)) this.DateLastAccessed = newDateLastAccessed;
            }

            public void GenerateRenditionFileName()
            {
                this.RenditionFileName = $"{Path.GetFileNameWithoutExtension(this.DataReference)}---{this.GraphId}---{this.Language}.svg";
            }


        }




    }
}