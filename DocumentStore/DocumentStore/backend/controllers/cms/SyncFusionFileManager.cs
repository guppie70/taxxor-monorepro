// using filemanager.Shared;
using System;
using Microsoft.AspNetCore.Http;
//File Manager's base functions are available in the below namespace
using Syncfusion.EJ2.FileManager.Base;
//File Manager's operations are available in the below namespace
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Syncfusion.EJ2.FileManager.PhysicalFileProvider;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {
        public async static Task HandleFileUpload(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);


            // Retrieve posted JSON from the client
            var postedJsonData = request.RetrievePostedValue("jsondata", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var assetType = request.RetrievePostedValue("assettype", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var uploadedAssestsBasePath = request.RetrievePostedValue("basepath", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var tempFolderPath = request.RetrievePostedValue("tempfolder", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var uploadedFileNamesString = request.RetrievePostedValue("tempfiles", RegexEnum.None, true, ReturnTypeEnum.Xml);
            string[] uploadedFileNames = uploadedFileNamesString.Split(',');

            // Sanity checks
            if (uploadedFileNames.Length == 0)
            {
                HandleError("No uploaded files to process");
            }

            var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/{tempFolderPath}";
            if (!Directory.Exists(tempFolderPathOs))
            {
                HandleError("Shared location could not be found", $"tempFolderPathOs: {tempFolderPathOs}");
            }


            try
            {

                // Use the images folder as the root
                var fileManagerRootPathOs = RetrieveAssetTypeRootPathOs(projectVars, assetType);

                var targetFolderPathOs = $"{fileManagerRootPathOs}{uploadedAssestsBasePath}";
                if (!Directory.Exists(targetFolderPathOs))
                {
                    Directory.CreateDirectory(targetFolderPathOs);
                }

                foreach (var uploadedFileName in uploadedFileNames)
                {
                    var sourceFilePathOs = $"{tempFolderPathOs}/{uploadedFileName}";
                    var targetFilePathOs = $"{targetFolderPathOs}/{uploadedFileName}";
                    if (!File.Exists(sourceFilePathOs))
                    {
                        HandleError("Uploaded file could not be located", $"sourceFilePathOs: {sourceFilePathOs}");
                    }
                    if (File.Exists(targetFilePathOs)) File.Delete(targetFilePathOs);

                    File.Copy(sourceFilePathOs, targetFilePathOs);
                }


                //
                // => Render a new XML cache document to indicate the file changes
                //
                var xmlCachePathOs = CalculateFullPathOs($"/configuration/applications/application[@id='asset_manager']/type[@name='{assetType}']/location");
                var dir = new DirectoryInfo(fileManagerRootPathOs);
                var xmlDirInfo = new XDocument(_getDirectoryXml(dir, dir.FullName));
                xmlDirInfo.Save(xmlCachePathOs);

                // Construct the commit message
                XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                SetAttribute(xmlCommitMessage.SelectSingleNode("/root/crud"), "application", "filemanager");
                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "upload";
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = uploadedFileNamesString;
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = "na";
                var message = xmlCommitMessage.DocumentElement.InnerXml;

                // Commit the result in the GIT repository
                CommitFilingAssets(message);

                // Wrap the result of the operation in a standard envelope
                var resultResponse = new TaxxorReturnMessage(true, "Successfully uploaded files", $"rootImageManagerPathOs: {fileManagerRootPathOs}, uploadedFileNamesString: {uploadedFileNamesString}");

                // Return success message
                await context.Response.OK(resultResponse, ReturnTypeEnum.Xml, true);
            }
            catch (Exception ex)
            {
                HandleError("There was an error retrieving information for the file manager", $"error: {ex}");
            }


        }



        /// <summary>
        /// Sync fusion file manager operations
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FileOperations(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted JSON from the client
            var postedJsonData = request.RetrievePostedValue("jsondata", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var assetType = request.RetrievePostedValue("assettype", RegexEnum.None, true, ReturnTypeEnum.Xml);

            try
            {

                // Convert the JSON to a C# object
                var fileManagerData = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(postedJsonData);

                // Validate posted values
                if (!RegExpTest(RegexEnum.FilePath.Value, fileManagerData.Path))
                {
                    appLogger.LogError($"Invalid file path supplied for the file manager. postedJsonData: {postedJsonData}");
                    throw new Exception("Invalid path supplied");
                }

                // Determine the folder to look into
                PhysicalFileProvider fileProvider = new PhysicalFileProvider();

                // Use the images folder as the root
                var fileManagerRootPathOs = RetrieveAssetTypeRootPathOs(projectVars, assetType);

                // Throw an exception in case the path is not valid and pointing to a folder outside the image or downloads folder
                if (fileManagerData.Path.Contains("../")) HandleError("Invalid path supplied", $"fileManagerData.Path: {fileManagerData.Path}");
                if (fileManagerData?.TargetPath?.Contains("../") ?? false) HandleError("Invalid path supplied", $"fileManagerData.TargetPath: {fileManagerData.TargetPath}");

                // Error handling
                if (string.IsNullOrEmpty(fileManagerRootPathOs)) HandleError("Unable to calculate root path", $"No path mapped to assetType: {assetType}");
                if (!Directory.Exists(fileManagerRootPathOs)) HandleError("Unable to find assets path", $"fileManagerRootPathOs: {fileManagerRootPathOs}");

                // Set the root path for the sync fusion file provider
                fileProvider.RootFolder(fileManagerRootPathOs);

                // Retrieve the filemanager result
                string? commitLinkName = null;
                string? fileManagerResult = null;
                switch (fileManagerData.Action)
                {
                    // Add your custom action here
                    case "read":
                        // Path - Current path; ShowHiddenItems - Boolean value to show/hide hidden items
                        fileManagerResult = fileProvider.ToCamelCase(fileProvider.GetFiles(fileManagerData.Path, fileManagerData.ShowHiddenItems));
                        break;
                    case "delete":
                        // Path - Current path where of the folder to be deleted; Names - Name of the files to be deleted
                        fileManagerResult = fileProvider.ToCamelCase(fileProvider.Delete(fileManagerData.Path, fileManagerData.Names));
                        commitLinkName = string.Join(',', fileManagerData.Names);
                        break;
                    case "copy":
                        //  Path - Path from where the file was copied; TargetPath - Path where the file/folder is to be copied; RenameFiles - Files with same name in the copied location that is confirmed for renaming; TargetData - Data of the copied file
                        fileManagerResult = fileProvider.ToCamelCase(fileProvider.Copy(fileManagerData.Path, fileManagerData.TargetPath, fileManagerData.Names, fileManagerData.RenameFiles, fileManagerData.TargetData));
                        commitLinkName = string.Join(',', fileManagerData.Names);
                        break;
                    case "move":
                        // Path - Path from where the file was cut; TargetPath - Path where the file/folder is to be moved; RenameFiles - Files with same name in the moved location that is confirmed for renaming; TargetData - Data of the moved file
                        fileManagerResult = fileProvider.ToCamelCase(fileProvider.Move(fileManagerData.Path, fileManagerData.TargetPath, fileManagerData.Names, fileManagerData.RenameFiles, fileManagerData.TargetData));
                        commitLinkName = string.Join(',', fileManagerData.Names);
                        break;
                    case "details":
                        // Path - Current path where details of file/folder is requested; Name - Names of the requested folders
                        fileManagerResult = fileProvider.ToCamelCase(fileProvider.Details(fileManagerData.Path, fileManagerData.Names));
                        break;
                    case "create":
                        // Path - Current path where the folder is to be created; Name - Name of the new folder
                        fileManagerResult = fileProvider.ToCamelCase(fileProvider.Create(fileManagerData.Path, fileManagerData.Name));
                        commitLinkName = fileManagerData.Name;
                        break;
                    case "search":
                        // Path - Current path where the search is performed; SearchString - String typed in the searchbox; CaseSensitive - Boolean value which specifies whether the search must be casesensitive
                        fileManagerResult = fileProvider.ToCamelCase(fileProvider.Search(fileManagerData.Path, fileManagerData.SearchString, fileManagerData.ShowHiddenItems, fileManagerData.CaseSensitive));
                        break;
                    case "rename":
                        // Path - Current path of the renamed file; Name - Old file name; NewName - New file name
                        var newAssetName = NormalizeFileName(fileManagerData.NewName);

                        // Log in case if we had to correct the filename
                        if (fileManagerData.NewName != newAssetName) appLogger.LogDebug($"Renamed {fileManagerData.NewName} to normalized {newAssetName}");

                        // Make sure we use the normalized name in the following C# logic
                        fileManagerData.NewName = newAssetName;

                        // Execute the rename action
                        fileManagerResult = fileProvider.ToCamelCase(fileProvider.Rename(fileManagerData.Path, fileManagerData.Name, fileManagerData.NewName));
                        commitLinkName = $"{fileManagerData.Name} -> {fileManagerData.NewName}";

                        break;
                }

                if (fileManagerResult == null)
                {
                    HandleError("Could not find a filenamager action to execute", "");
                }
                else
                {
                    if (commitLinkName != null && (assetType == "projectimages" || assetType == "projectdownloads"))
                    {
                        //
                        // => Load the existing assetcache
                        //
                        var assetCacheFilePathOs = CalculateFullPathOs($"/configuration/applications/application[@id='asset_manager']/type[@name='{assetType}']/location");
                        var xmlAssetCacheOriginal = new XmlDocument();
                        xmlAssetCacheOriginal.Load(assetCacheFilePathOs);

                        //
                        // => Render a new XML cache document to indicate the file changes
                        //
                        var dir = new DirectoryInfo(fileManagerRootPathOs);
                        var xmlDirInfo = new XDocument(_getDirectoryXml(dir, dir.FullName));
                        xmlDirInfo.Save(assetCacheFilePathOs);

                        //
                        // => Load the new cache document
                        //
                        var xmlAssetCacheNew = new XmlDocument();
                        xmlAssetCacheNew.Load(assetCacheFilePathOs);

                        var resultFileStructureDefinitionXpath = RetrieveFileStructureXpath(projectVars.editorId);
                        if (!resultFileStructureDefinitionXpath.Success)
                        {
                            appLogger.LogWarning($"{resultFileStructureDefinitionXpath.Message}, debuginfo: {resultFileStructureDefinitionXpath.DebugInfo}, stack-trace: {GetStackTrace()}");
                            throw new Exception(resultFileStructureDefinitionXpath.Message);
                        }

                        // Find the different root folders where we need to look for the source materials
                        var sourceRootFolders = new List<string>();
                        switch (assetType)
                        {
                            case "projectimages":
                                sourceRootFolders.Add("/images");
                                var nodeWebImagesLocation = xmlApplicationConfiguration.SelectSingleNode($"{resultFileStructureDefinitionXpath.Payload}[@content='images']");
                                if (nodeWebImagesLocation != null)
                                {
                                    var basePath = nodeWebImagesLocation.GetAttribute("name");
                                    if (!string.IsNullOrEmpty(basePath))
                                    {
                                        sourceRootFolders.Add($"/{Path.GetFileName(basePath)}");
                                    }
                                }
                                break;

                            case "projectdownloads":
                                sourceRootFolders.Add("/downloads");
                                var nodeWebDownloadsLocation = xmlApplicationConfiguration.SelectSingleNode($"{resultFileStructureDefinitionXpath.Payload}[@content='downloads']");
                                if (nodeWebDownloadsLocation != null)
                                {
                                    var basePath = nodeWebDownloadsLocation.GetAttribute("name");
                                    if (!string.IsNullOrEmpty(basePath))
                                    {
                                        if (basePath.Contains("[language]"))
                                        {
                                            var languages = RetrieveProjectLanguages(projectVars.editorId);

                                            foreach (var lang in languages)
                                            {
                                                basePath = basePath.Replace("[language]", lang);
                                                sourceRootFolders.Add($"/{Path.GetFileName(basePath)}");
                                            }
                                        }
                                        else
                                        {
                                            sourceRootFolders.Add($"/{Path.GetFileName(basePath)}");
                                        }
                                    }
                                }
                                break;

                            default:
                                appLogger.LogError($"No support for {assetType} in post processing file manager changes");
                                break;
                        }


                        var affectedAssets = new Dictionary<string, string>();
                        var xPathForReplacement = "";
                        var originalAssetPath = "";
                        var newAssetPath = "";

                        switch (fileManagerData.Action)
                        {

                            case "delete":
                                foreach (var fileName in fileManagerData.Names)
                                {
                                    foreach (var sourceRootPath in sourceRootFolders)
                                    {
                                        if (!affectedAssets.ContainsKey($"{sourceRootPath}{fileManagerData.Path}{fileName}")) affectedAssets.Add($"{sourceRootPath}{fileManagerData.Path}{fileName}", "");
                                    }
                                }

                                break;

                            case "move":
                                // Path - Path from where the file was cut; TargetPath - Path where the file/folder is to be moved; RenameFiles - Files with same name in the moved location that is confirmed for renaming; TargetData - Data of the moved file
                                foreach (var fileName in fileManagerData.Names)
                                {
                                    foreach (var sourceRootPath in sourceRootFolders)
                                    {
                                        if (!affectedAssets.ContainsKey($"{sourceRootPath}{fileManagerData.Path}{fileName}")) affectedAssets.Add($"{sourceRootPath}{fileManagerData.Path}{fileName}", $"{sourceRootPath}{fileManagerData.TargetPath}{fileName}");
                                    }
                                }
                                foreach (var fileName in fileManagerData.RenameFiles)
                                {

                                }
                                break;

                            // case "copy":
                            //     //  Path - Path from where the file was copied; TargetPath - Path where the file/folder is to be copied; RenameFiles - Files with same name in the copied location that is confirmed for renaming; TargetData - Data of the copied file
                            //     foreach (var fileName in fileManagerData.Names)
                            //     {
                            //         affectedAssets.Add($"{fileManagerData.Path}{fileName}", $"{fileManagerData.TargetPath}{fileName}");
                            //     }
                            //     break;


                            case "rename":
                                // Path - Current path of the renamed file; Name - Old file name; NewName - New file name

                                // If we have renamed a directory, then we need to collect all the files
                                if (IsDirectory($"{fileManagerRootPathOs}{fileManagerData.Path}{fileManagerData.NewName}"))
                                {
                                    string[] filesInNewDirectory = Directory.GetFiles($"{fileManagerRootPathOs}{fileManagerData.Path}{fileManagerData.NewName}", "*.*", SearchOption.AllDirectories);

                                    foreach (var filePathOs in filesInNewDirectory)
                                    {
                                        var relativeFilePath = filePathOs.Replace(fileManagerRootPathOs, "");

                                        newAssetPath = relativeFilePath;
                                        originalAssetPath = relativeFilePath.Replace($"{fileManagerData.Path}{fileManagerData.NewName}", $"{fileManagerData.Path}{fileManagerData.Name}");

                                        foreach (var sourceRootPath in sourceRootFolders)
                                        {
                                            affectedAssets.Add($"{sourceRootPath}{originalAssetPath}", $"{sourceRootPath}{newAssetPath}");
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var sourceRootPath in sourceRootFolders)
                                    {
                                        affectedAssets.Add($"{sourceRootPath}{fileManagerData.Path}{fileManagerData.Name}", $"{sourceRootPath}{fileManagerData.Path}{fileManagerData.NewName}");
                                    }
                                }
                                break;
                        }


                        //
                        // => Change the asset links in the data references
                        //
                        var updatedReferences = new List<string>();

                        if (affectedAssets.Count > 0)
                        {
                            var nodeCmsContentData = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectVars.projectId}']/data");
                            if (nodeCmsContentData == null)
                            {
                                appLogger.LogError("Could not find data node in xmlCmsContentMetadata to loop through all the filing content files");
                            }
                            else
                            {
                                var cmsContentDataRootPathOs = nodeCmsContentData.GetAttribute("basepathos");

                                var nodeListContentDataFiles = nodeCmsContentData.SelectNodes("content[@datatype='sectiondata']");
                                foreach (XmlNode nodeContentDataFile in nodeListContentDataFiles)
                                {
                                    var dataRef = nodeContentDataFile.GetAttribute("ref");

                                    var dataFilePathOs = $"{applicationRootPathOs}/{cmsContentDataRootPathOs}/{dataRef}";
                                    if (File.Exists(dataFilePathOs))
                                    {
                                        try
                                        {
                                            var xmlSectionData = new XmlDocument();
                                            xmlSectionData.Load(dataFilePathOs);

                                            // appLogger.LogDebug($"Searching in {dataRef}");

                                            foreach (KeyValuePair<string, string> item in affectedAssets)
                                            {
                                                var existingPath = item.Key;
                                                var newPath = item.Value;

                                                switch (assetType)
                                                {
                                                    case "projectimages":
                                                        var numberOfReplacedImages = 0;


                                                        // images
                                                        xPathForReplacement = $"//img[contains(@src, '{existingPath}')]";
                                                        var nodeListImageReplacements = xmlSectionData.SelectNodes(xPathForReplacement);
                                                        foreach (XmlNode nodeImage in nodeListImageReplacements)
                                                        {
                                                            if (newPath == "")
                                                            {
                                                                if (existingPath.Contains(".svg"))
                                                                {
                                                                    var nodeWrapper = nodeImage.ParentNode;
                                                                    var wrapperClass = nodeWrapper.GetAttribute("class") ?? "";
                                                                    if (wrapperClass.Contains("illustration"))
                                                                    {
                                                                        // Remove the wrapper node
                                                                        RemoveXmlNode(nodeWrapper);
                                                                    }
                                                                    else
                                                                    {
                                                                        // Delete the image
                                                                        RemoveXmlNode(nodeImage);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    // Delete the image
                                                                    RemoveXmlNode(nodeImage);
                                                                }

                                                            }
                                                            else
                                                            {
                                                                // Change the image path
                                                                var currentUri = nodeImage.GetAttribute("src");
                                                                nodeImage.SetAttribute("src", currentUri.Replace(existingPath, newPath));
                                                            }
                                                            numberOfReplacedImages++;
                                                        }

                                                        // SVG references
                                                        xPathForReplacement = $"//object[contains(@data, '{existingPath}')]";
                                                        var nodeListObjectReplacements = xmlSectionData.SelectNodes(xPathForReplacement);
                                                        foreach (XmlNode nodeObject in nodeListObjectReplacements)
                                                        {
                                                            if (newPath == "")
                                                            {
                                                                var nodeWrapper = nodeObject.ParentNode;
                                                                var wrapperClass = nodeWrapper.GetAttribute("class") ?? "";
                                                                if (wrapperClass.Contains("illustration"))
                                                                {
                                                                    // Remove the wrapper node
                                                                    RemoveXmlNode(nodeWrapper);
                                                                }
                                                                else
                                                                {
                                                                    // Delete the SVG
                                                                    RemoveXmlNode(nodeObject);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                // Change the SVG location
                                                                var currentUri = nodeObject.GetAttribute("data");
                                                                nodeObject.SetAttribute("data", currentUri.Replace(existingPath, newPath));

                                                            }
                                                            numberOfReplacedImages++;
                                                        }

                                                        if (numberOfReplacedImages > 0)
                                                        {
                                                            appLogger.LogDebug($"** Replaced {numberOfReplacedImages} image locations in {dataRef} **");
                                                            if (!updatedReferences.Contains(dataRef)) updatedReferences.Add(dataRef);
                                                            xmlSectionData.Save(dataFilePathOs);
                                                        }
                                                        break;

                                                    case "projectdownloads":
                                                        // Replace link locations
                                                        var numberOfReplacedLinks = 0;
                                                        xPathForReplacement = $"//a[contains(@href, '{existingPath}')]";
                                                        var nodeListLinkReplacements = xmlSectionData.SelectNodes(xPathForReplacement);
                                                        foreach (XmlNode nodeLink in nodeListLinkReplacements)
                                                        {
                                                            if (newPath == "")
                                                            {
                                                                // Replace the link with a "special" span element
                                                                var nodeSpan = xmlSectionData.CreateElement("span");
                                                                nodeSpan.InnerXml = nodeLink.InnerXml;
                                                                nodeSpan.SetAttribute("class", "tx-linkreplacement");
                                                                ReplaceXmlNode(nodeLink, nodeSpan);
                                                            }
                                                            else
                                                            {
                                                                // Change the SVG location
                                                                var currentUri = nodeLink.GetAttribute("href");
                                                                nodeLink.SetAttribute("href", currentUri.Replace(existingPath, newPath));
                                                            }
                                                            numberOfReplacedLinks++;
                                                        }

                                                        if (numberOfReplacedLinks > 0)
                                                        {
                                                            appLogger.LogDebug($"** Replaced {numberOfReplacedLinks} link locations in {dataRef} **");
                                                            if (!updatedReferences.Contains(dataRef)) updatedReferences.Add(dataRef);
                                                            xmlSectionData.Save(dataFilePathOs);
                                                        }
                                                        break;

                                                    default:
                                                        appLogger.LogError($"No support for {assetType} in post processing file manager changes");
                                                        break;
                                                }
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            appLogger.LogError(ex, $"There was an error adjusting asset paths in {dataRef}");
                                        }
                                    }
                                }
                            }
                        }



                        //
                        // => Commit all the changes in the versioning system
                        //
                        XmlDocument xmlCommitMessage = new XmlDocument();
                        if (updatedReferences.Count > 0)
                        {
                            // Construct the commit message
                            xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                            SetAttribute(xmlCommitMessage.SelectSingleNode("/root/crud"), "application", "filemanager");
                            xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = fileManagerData.Action;
                            xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = string.Join(',', updatedReferences.ToArray());
                            xmlCommitMessage.SelectSingleNode("/root/id").InnerText = string.Join(',', updatedReferences.ToArray());
                            var message = xmlCommitMessage.DocumentElement.InnerXml;
                            CommitFilingData(xmlCommitMessage.DocumentElement.InnerXml);
                        }

                        // Construct the commit message
                        xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                        SetAttribute(xmlCommitMessage.SelectSingleNode("/root/crud"), "application", "filemanager");
                        xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = fileManagerData.Action;
                        xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = commitLinkName;
                        xmlCommitMessage.SelectSingleNode("/root/id").InnerText = "na";

                        // Commit the result in the GIT repository
                        CommitFilingAssets(xmlCommitMessage.DocumentElement.InnerXml);
                    }


                    // Wrap the result of the operation in a standard envelope
                    var resultResponse = new TaxxorReturnMessage(true, "Successfully processed image manager request", fileManagerResult, $"rootImageManagerPathOs: {fileManagerRootPathOs}");

                    // Return success message
                    await context.Response.OK(resultResponse, ReturnTypeEnum.Xml, true);
                }

            }
            // To make sure HandleError is correctly executed
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleError("There was an error retrieving information for the file manager", $"error: {ex}");
            }
        }

        /// <summary>
        /// Retrieves the root location (full OS path) where the file manager needs to operate in
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="assetType"></param>
        /// <returns></returns>
        public static string? RetrieveAssetTypeRootPathOs(ProjectVariables projectVars, string assetType)
        {
            switch (assetType)
            {
                case "projectimages":
                    return $"{projectVars.cmsContentRootPathOs}/images";

                case "projectdownloads":
                    return $"{projectVars.cmsContentRootPathOs}/downloads";

                case "projectreports":
                    return $"{projectVars.cmsContentRootPathOs}/reports";
            }
            return null;
        }

        /// <summary>
        /// Recursive function to generate an XML structure from a directory structure
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="rootFolderPathOs"></param>
        /// <returns></returns>
        public static XElement _getDirectoryXml(DirectoryInfo dir, string rootFolderPathOs, bool extraInfo = false, Regex reFilter = null)
        {
            var info = new XElement("dir",
                new XAttribute("name", dir.Name), new XAttribute("path", dir.FullName.Replace(rootFolderPathOs, "")));

            foreach (var file in dir.GetFiles())
            {
                var addFile = true;
                if (reFilter != null)
                {
                    addFile = reFilter.IsMatch($"{dir.FullName}/{file.Name}");
                }

                if (addFile)
                {
                    var element = new XElement("file",
                        new XAttribute("name", file.Name),
                        new XAttribute("path", file.FullName.Replace(rootFolderPathOs, "").Substring(1)),
                        new XAttribute("hash", RenderFileHash($"{dir.FullName}/{file.Name}")),
                        extraInfo ? new XAttribute("datemodified", file.LastWriteTime) : null,
                        extraInfo ? new XAttribute("dateaccessed", file.LastAccessTime) : null

                    );

                    // if (file.Name.Contains("parrot"))
                    // {
                    //     appLogger.LogInformation("debugging");
                    // }

                    info.Add(element);
                }
            }


            foreach (var subDir in dir.GetDirectories())
                info.Add(_getDirectoryXml(subDir, rootFolderPathOs, extraInfo, reFilter));

            return info;
        }



    }
}