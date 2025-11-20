using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Logic for converting images, drawings and graphs
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Routine that calculates the difference that has occured in an image or downloads library
        /// Compares two XML representations with each other and returns an object that contains the delta
        /// </summary>
        /// <param name="xPathAssets"></param>
        /// <param name="xmlCachedContent"></param>
        /// <param name="xmlCurrentContent"></param>
        /// <param name="renditionsRootFolderName"></param>
        /// <returns></returns>
        public static AssetLibraryDelta CalculateLibraryDifferences(string xPathAssets, XmlDocument? xmlCachedContent, XmlDocument xmlCurrentContent, string renditionsRootFolderName)
        {

            var assetLibraryDelta = new AssetLibraryDelta();

            // - Determinine if there is cached information available on the contents of the library
            var cacheFileExists = true;
            if (xmlCachedContent == null) cacheFileExists = false;

            // - Determine for which type of assets we need to calculate the delta
            var assetType = "images";
            if (xPathAssets.Contains(".svg"))
            {
                assetType = "drawings";
            }


            if (!cacheFileExists)
            {
                // Treat all images as if they were fresh uploads
                var nodeListAssetsToAdd = xmlCurrentContent.SelectNodes(xPathAssets);
                foreach (XmlNode nodeAssetToAdd in nodeListAssetsToAdd)
                {
                    var currentImagePath = nodeAssetToAdd.GetAttribute("path");
                    assetLibraryDelta.Added.Add($"/{currentImagePath}");
                }
            }
            else
            {
                var nodeListCachedImages = xmlCachedContent.SelectNodes(xPathAssets);
                var nodeListCurrentImages = xmlCurrentContent.SelectNodes(xPathAssets);

                // - Images changed
                foreach (XmlNode nodeCurrentImage in nodeListCurrentImages)
                {
                    if (string.IsNullOrEmpty(nodeCurrentImage.GetAttribute("processed")))
                    {
                        // Same path, but different hash
                        var currentImagePath = nodeCurrentImage.GetAttribute("path");
                        var currentImageHash = nodeCurrentImage.GetAttribute("hash");
                        if (!string.IsNullOrEmpty(currentImagePath) && !string.IsNullOrEmpty(currentImageHash))
                        {
                            // if (currentImagePath.Contains("cover-book-zh"))
                            // {
                            //     appLogger.LogInformation("debugging");
                            // }

                            var nodeCachedImage = xmlCachedContent.SelectSingleNode($"//file[@path='{currentImagePath}' and not(@hash='{currentImageHash}')]");
                            if (nodeCachedImage != null)
                            {
                                assetLibraryDelta.Changed.Add($"/{nodeCurrentImage.GetAttribute("path")}");
                                nodeCurrentImage.SetAttribute("processed", "true");
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Could not find all the information. nodeCurrentImage: {nodeCurrentImage.OuterXml}");
                        }
                    }
                }

                // - Images added
                foreach (XmlNode nodeCurrentImage in nodeListCurrentImages)
                {
                    if (string.IsNullOrEmpty(nodeCurrentImage.GetAttribute("processed")))
                    {
                        var currentImagePath = nodeCurrentImage.GetAttribute("path");
                        var fileHash = nodeCurrentImage.GetAttribute("hash") ?? "";
                        var nodeCachedImage = xmlCachedContent.SelectSingleNode($"//file[@hash='{fileHash}']");
                        if (nodeCachedImage == null)
                        {
                            assetLibraryDelta.Added.Add($"/{currentImagePath}");
                        }
                        else
                        {
                            nodeCurrentImage.SetAttribute("processed", "true");
                        }
                    }
                }

                // - Images renamed or moved
                foreach (XmlNode nodeCachedImage in nodeListCachedImages)
                {
                    var cachedImagePath = nodeCachedImage.GetAttribute("path");
                    var cachedImageHash = nodeCachedImage.GetAttribute("hash");
                    var cachedImageAccesedDate = nodeCachedImage.GetAttribute("dateaccessed");
                    var dateCachedImageAccessed = DateTime.Parse(cachedImageAccesedDate);

                    var xPathMovedImages = $"//file[@hash='{cachedImageHash}' and not(@path='{cachedImagePath}')]";
                    var nodeListMovedImages = xmlCurrentContent.SelectNodes(xPathMovedImages);

                    foreach (XmlNode nodeMovedImage in nodeListMovedImages)
                    {
                        var currentImagePath = nodeMovedImage.GetAttribute("path");
                        var currentImageAccessedDate = nodeMovedImage.GetAttribute("dateaccessed"); // This date needs to be after (later) than the cachedImageAccesedDate 

                        var nodeCompare1 = xmlCurrentContent.SelectSingleNode($"//file[@path='{cachedImagePath}']");
                        var nodeCompare2 = xmlCurrentContent.SelectSingleNode($"//file[@path='{currentImagePath}']");

                        if (nodeCompare1 != null && nodeCompare2 != null)
                        {

                        }
                        else
                        {
                            var dateCurrentImageAccessed = DateTime.Parse(currentImageAccessedDate);
                            if (dateCachedImageAccessed.IsBefore(dateCurrentImageAccessed))
                            {
                                assetLibraryDelta.Renamed.Add($"/{cachedImagePath}", $"/{currentImagePath}");
                                nodeMovedImage.SetAttribute("processed", "true");

                                //
                                // => Mark the thumbnail rendering as "inuse" to prevent it from being marked as an orphaned image
                                //
                                var thumbnailImageFilename = ThumbnailFilenameTemplate.Replace("{filename}", Path.GetFileNameWithoutExtension(cachedImagePath)).Replace("{extension}", ThumbnailFileExtension);
                                var thumbnailPath = $"{renditionsRootFolderName}/{assetType}/{Path.GetDirectoryName(cachedImagePath)}/{thumbnailImageFilename}".Replace("//", "/");
                                // appLogger.LogWarning($"- thumbnailPath: {thumbnailPath}");
                                var nodeThumbnailImage = xmlCurrentContent.SelectSingleNode($"/projects/project//file[@path='{thumbnailPath}']");
                                if (nodeThumbnailImage == null)
                                {
                                    appLogger.LogWarning($"Unable to locate thumbnail asset for {cachedImagePath}");
                                }
                                else
                                {
                                    nodeThumbnailImage.SetAttribute("inuse", "true");
                                }

                                var imageExtension = Path.GetExtension(cachedImagePath).ToLower();
                                if (imageExtension == ".png" || imageExtension == ".gif")
                                {
                                    // Test to see if we can find the rendition of this image in JPG
                                    var renditionPath = $"{renditionsRootFolderName}/{assetType}/{Path.GetDirectoryName(cachedImagePath)}/{Path.GetFileNameWithoutExtension(cachedImagePath)}.jpg".Replace("//", "/");
                                    var nodeRenditionImage = xmlCurrentContent.SelectSingleNode($"/projects/project//file[@path='{renditionPath}']");
                                    if (nodeRenditionImage == null)
                                    {
                                        appLogger.LogWarning($"Unable to locate rendition asset for {cachedImagePath}");
                                    }
                                    else
                                    {
                                        nodeRenditionImage.SetAttribute("inuse", "true");
                                    }
                                }
                            }
                        }
                    }

                }


                // - Images deleted
                foreach (XmlNode nodeCachedImage in nodeListCachedImages)
                {
                    var cachedImagePath = nodeCachedImage.GetAttribute("path");
                    var cachedImageHash = nodeCachedImage.GetAttribute("hash");
                    var xPathDeletedImages = $"//file[@path='{cachedImagePath}']";
                    var nodeDeletedImage = xmlCurrentContent.SelectSingleNode(xPathDeletedImages);
                    if (nodeDeletedImage == null && !assetLibraryDelta.Renamed.ContainsKey($"/{cachedImagePath}"))
                    {
                        if (!assetLibraryDelta.Removed.Contains(cachedImagePath)) assetLibraryDelta.Removed.Add($"/{cachedImagePath}");
                    }
                }


                // - Invalid renditions
                foreach (XmlNode nodeCurrentImage in nodeListCurrentImages)
                {
                    var currentImagePath = nodeCurrentImage.GetAttribute("path");

                    if (assetLibraryDelta.Renamed.ContainsValue($"/{currentImagePath}")) continue;

                    var currentImageDateModified = nodeCurrentImage.GetAttribute("datemodified");
                    var currentImageAccessedDate = nodeCurrentImage.GetAttribute("dateaccessed");
                    var dateCurrentImageModified = DateTime.Parse(currentImageDateModified);
                    var dateCurrentImageAccessed = DateTime.Parse(currentImageAccessedDate);

                    // Thumbnail information
                    var thumbnailImageFilename = ThumbnailFilenameTemplate.Replace("{filename}", Path.GetFileNameWithoutExtension(currentImagePath)).Replace("{extension}", ThumbnailFileExtension);
                    var thumbnailPath = $"{renditionsRootFolderName}/{assetType}/{Path.GetDirectoryName(currentImagePath)}/{thumbnailImageFilename}".Replace("//", "/");

                    var nodeThumbnailImage = xmlCurrentContent.SelectSingleNode($"/projects/project//file[@path='{thumbnailPath}']");

                    // if (currentImagePath == "svg/health-continuum.svg")
                    // {
                    //     appLogger.LogInformation("debugging");
                    // }
                    if (nodeThumbnailImage == null)
                    {
                        assetLibraryDelta.InvalidRenditions.Add($"/{currentImagePath}");
                    }
                    else
                    {
                        var thumbnailImageDateModified = nodeThumbnailImage.GetAttribute("datemodified");
                        var thumbnailImageAccessedDate = nodeThumbnailImage.GetAttribute("dateaccessed");

                        var dateThumbnailImageModified = DateTime.Parse(thumbnailImageDateModified);
                        var dateThumbnailImageAccessed = DateTime.Parse(thumbnailImageAccessedDate);
                        if (dateThumbnailImageAccessed.IsBefore(dateCurrentImageAccessed) || dateThumbnailImageModified.IsBefore(dateCurrentImageModified))
                        {
                            // appLogger.LogInformation($"*** thumbnailImage {thumbnailPath} is invalid");
                            assetLibraryDelta.InvalidRenditions.Add($"/{currentImagePath}");
                        }

                    }

                    // Special case for transparent images -> these need to be available in jpeg format as well
                    var imageExtension = Path.GetExtension(currentImagePath).ToLower();
                    if (imageExtension == ".png" || imageExtension == ".gif")
                    {
                        // Test to see if we can find the rendition of this image in JPG
                        var renditionPath = $"{renditionsRootFolderName}/{assetType}/{Path.GetDirectoryName(currentImagePath)}/{Path.GetFileNameWithoutExtension(currentImagePath)}.jpg".Replace("//", "/");
                        var nodeRenditionImage = xmlCurrentContent.SelectSingleNode($"/projects/project//file[@path='{renditionPath}']");
                        if (nodeRenditionImage == null)
                        {
                            assetLibraryDelta.InvalidRenditions.Add($"/{currentImagePath}");
                        }
                        else
                        {
                            var renditionImageDateModified = nodeRenditionImage.GetAttribute("datemodified");
                            var renditionImageAccessedDate = nodeRenditionImage.GetAttribute("dateaccessed");

                            var dateRenditionImageModified = DateTime.Parse(renditionImageDateModified);
                            var dateRenditionImageAccessed = DateTime.Parse(renditionImageAccessedDate);
                            if (dateRenditionImageAccessed.IsBefore(dateCurrentImageAccessed))
                            {
                                // appLogger.LogInformation($"*** rendition {thumbnailPath} is invalid");
                                if (!assetLibraryDelta.InvalidRenditions.Contains($"/{currentImagePath}")) assetLibraryDelta.InvalidRenditions.Add($"/{currentImagePath}");
                            }
                        }
                    }
                }



                // - Orphaned renditions
                foreach (XmlNode nodeCurrentImage in nodeListCurrentImages)
                {
                    var currentImagePath = nodeCurrentImage.GetAttribute("path");

                    // Thumbnail information
                    var thumbnailImageFilename = ThumbnailFilenameTemplate.Replace("{filename}", Path.GetFileNameWithoutExtension(currentImagePath)).Replace("{extension}", ThumbnailFileExtension);
                    var thumbnailPath = $"{renditionsRootFolderName}/{assetType}/{Path.GetDirectoryName(currentImagePath)}/{thumbnailImageFilename}".Replace("//", "/");

                    var nodeThumbnailImage = xmlCurrentContent.SelectSingleNode($"/projects/project//file[@path='{thumbnailPath}']");
                    if (nodeThumbnailImage != null)
                    {
                        nodeThumbnailImage.SetAttribute("inuse", "true");
                    }

                    if (assetType == "drawings")
                    {
                        // The renditions folder contains jpg and png renditions of the original file which we need to mark as inuse
                        string[] arrExtensions = { ".jpg", ".png" };
                        foreach (var extension in arrExtensions)
                        {
                            var renditionPath = $"{renditionsRootFolderName}/{assetType}/{Path.GetDirectoryName(currentImagePath)}/{Path.GetFileNameWithoutExtension(currentImagePath)}{extension}".Replace("//", "/");
                            var nodeRenditionImage = xmlCurrentContent.SelectSingleNode($"/projects/project//file[@path='{renditionPath}']");
                            if (nodeRenditionImage != null)
                            {
                                nodeRenditionImage.SetAttribute("inuse", "true");
                            }
                        }
                    }
                    else
                    {
                        var imageExtension = Path.GetExtension(currentImagePath).ToLower();
                        if (imageExtension == ".png" || imageExtension == ".gif")
                        {
                            // Test to see if we can find the rendition of this image in JPG
                            var renditionPath = $"{renditionsRootFolderName}/{assetType}/{Path.GetDirectoryName(currentImagePath)}/{Path.GetFileNameWithoutExtension(currentImagePath)}.jpg".Replace("//", "/");
                            var nodeRenditionImage = xmlCurrentContent.SelectSingleNode($"/projects/project//file[@path='{renditionPath}']");
                            if (nodeRenditionImage != null)
                            {
                                nodeRenditionImage.SetAttribute("inuse", "true");
                            }
                        }
                    }


                }
                var nodeListOrphanedRenditions = xmlCurrentContent.SelectNodes($"//dir[@path='/{renditionsRootFolderName}/{assetType}']//file[not(@inuse)]");
                foreach (XmlNode nodeOrphanedRendition in nodeListOrphanedRenditions)
                {
                    var currentImagePath = nodeOrphanedRendition.GetAttribute("path");

                    var originalInRenamedAssets = assetLibraryDelta.Renamed.ContainsKey($"/{currentImagePath}".Replace("//", "/").Replace($"/{ImageRenditionsFolderName}/{assetType}", "").Replace(".png", ".svg").Replace(".jpg", ".svg"));

                    if (!string.IsNullOrEmpty(currentImagePath) && !originalInRenamedAssets)
                    {
                        assetLibraryDelta.OrphanedRenditions.Add($"/{currentImagePath}");
                    }
                }

            }


            return assetLibraryDelta;
        }

        /// <summary>
        /// Updates the renditions in the image library and handles the result silently
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static async Task UpdateImageLibraryRenditions(string projectId)
        {
            try
            {
                var imageRenditionsResult = await GenerateImageRenditions(projectId);
                string? resultForDebugging = null;
                if (imageRenditionsResult.SuccessLog.Count > 0) resultForDebugging = $"successLog: {string.Join(',', imageRenditionsResult.SuccessLog.ToArray())} for project: {projectId}";
                if (imageRenditionsResult.WarningLog.Count > 0) resultForDebugging += $"\nwarningLog: {string.Join(',', imageRenditionsResult.WarningLog.ToArray())} for project: {projectId}";
                if (imageRenditionsResult.ErrorLog.Count > 0) resultForDebugging += $"\nerrorLog: {string.Join(',', imageRenditionsResult.ErrorLog.ToArray())} for project: {projectId}";

                if (!imageRenditionsResult.Success)
                {
                    appLogger.LogError($"There was an error updating the image library.\nMessage: {imageRenditionsResult.Message}\nDebugInfo: {imageRenditionsResult.DebugInfo}\nprojectId: {projectId}");
                    appLogger.LogError(resultForDebugging);
                }
                else
                {
                    if (resultForDebugging != null) appLogger.LogInformation(resultForDebugging);
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"There was a problem updating the image library for project: {projectId}");
            }
        }

        /// <summary>
        /// Captures the changes that have occured in the asset library
        /// </summary>
        public class AssetLibraryDelta
        {
            public List<string> Added { get; set; } = new List<string>();
            public List<string> Changed { get; set; } = new List<string>();
            public Dictionary<string, string> Renamed { get; set; } = new Dictionary<string, string>();
            public List<string> Removed { get; set; } = new List<string>();
            public List<string> InvalidRenditions { get; set; } = new List<string>();
            public List<string> OrphanedRenditions { get; set; } = new List<string>();

            // Empty constructor
            public AssetLibraryDelta()
            {

            }
        }

    }
}