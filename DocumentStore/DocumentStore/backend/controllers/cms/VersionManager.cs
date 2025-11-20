using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to for interacting with the Taxxor Editor's Version Manager
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves data for the Taxxor Editor version manager
        /// </summary>
        /// <returns>The user data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task RetrieveVersionManagerData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local");
            // debugRoutine = true;

            //HandleError(reqVars.returnType, "Not implemented yet", $"stack-trace: {GetStackTrace()}");

            // Retrieve posted values
            var projectId = request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);

            if (projectVars.currentUser.IsAuthenticated)
            {


                var gitPrettyPrintPattern = "\"<tag-meta repro='[reproid]' tagname='%(refname:short)' hash='%(objectname)'>%(contents)</tag-meta>\"";
                var sbTagsContent = new StringBuilder();
                var tagsContent = "";
                XmlDocument xmlTags = new XmlDocument();

                var dataGitReproPathOs = RetrieveGitRepositoryLocation("project-data");
                var contentGitReproPathOs = RetrieveGitRepositoryLocation("project-content");

                if (!Directory.Exists(dataGitReproPathOs)) HandleError(reqVars.returnType, "Could not find data repository", $"path: {dataGitReproPathOs} could not be found, stack-trace: {GetStackTrace()}");
                if (!Directory.Exists(contentGitReproPathOs)) HandleError(reqVars.returnType, "Could not find content repository", $"path: {contentGitReproPathOs} could not be found, stack-trace: {GetStackTrace()}");



                // Retrieve the tags from the data repository
                try
                {
                    tagsContent = GitRetrieveTagMessages(dataGitReproPathOs, gitPrettyPrintPattern.Replace("[reproid]", "project-data"));
                    if (tagsContent.ToLower().Contains("error:"))
                    {
                        HandleError(reqVars.returnType, "Could not retrieve GIT tag data from data repository", $"tagsContent: {tagsContent}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        sbTagsContent.AppendLine(tagsContent);
                    }
                }
                catch (Exception ex)
                {
                    HandleError(reqVars.returnType, "Could not retrieve GIT tag data from data repository", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

                // Retrieve the tags from the content repository
                try
                {
                    tagsContent = GitRetrieveTagMessages(contentGitReproPathOs, gitPrettyPrintPattern.Replace("[reproid]", "project-content"));
                    if (tagsContent.ToLower().Contains("error:"))
                    {
                        HandleError(reqVars.returnType, "Could not retrieve GIT tag data from content repository", $"tagsContent: {tagsContent}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        sbTagsContent.AppendLine(tagsContent);
                    }
                }
                catch (Exception ex)
                {
                    HandleError(reqVars.returnType, "Could not retrieve GIT tag data from content repository", $"error: {ex}");
                }

                // TextFileCreate(sbTagsContent.ToString(), $"{logRootPathOs}/_versionmanager-raw.txt");

                // Attempt to load the complete dataset into an XmlDocument object
                try
                {
                    xmlTags.LoadXml("<tags>" + Environment.NewLine + sbTagsContent.ToString() + Environment.NewLine + "</tags>");
                }
                catch (Exception ex)
                {
                    HandleError(reqVars.returnType, "Could not retrieve auditor view information", $"Unable to load the xml content retrieved from the git repository in a XmlDocument object. error: {ex}, stack-trace: {GetStackTrace()}");
                }


                if (debugRoutine)
                {
                    await xmlTags.SaveAsync($"{logRootPathOs}/_versionmanager-raw.xml", false, true);
                }

                // Sort and merge the information
                xmlTags = TransformXmlToDocument(xmlTags, CalculateFullPathOs("cms_xsl_version-manager-sort"));

                // Test if the cache files are present for major versions
                var systemRootFolderPathOs = CalculateFullPathOs("cmssystemcontentroot");
                var nodeListCacheFiles = xmlTags.SelectNodes("/tags/tag/message/files/file");
                foreach (XmlNode nodeCacheFile in nodeListCacheFiles)
                {
                    var cacheFileExists = File.Exists($"{systemRootFolderPathOs}{nodeCacheFile.InnerText}");
                    nodeCacheFile.SetAttribute("exists", cacheFileExists.ToString().ToLower());
                }

                if (debugRoutine)
                {
                    await xmlTags.SaveAsync($"{logRootPathOs}/_versionmanager-done.xml", false, true);
                }

                var returnMessage = new TaxxorReturnMessage(true, "Successfully retrieved version information", xmlTags);

                await response.OK(returnMessage, reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars.returnType, "Not authenticated", $"projectId: {projectId}, stack-trace: {GetStackTrace()}", 403);
            }
        }

        /// <summary>
        /// Creates a new version of the filing content
        /// </summary>
        /// <returns>The version.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task CreateVersion(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugInfo = "";


            //HandleError(reqVars.returnType, "Not implemented yet", $"stack-trace: {GetStackTrace()}");


            // Retrieve posted data
            var projectId = request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var tagMessage = context.Request.RetrievePostedValue("message", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var tagName = context.Request.RetrievePostedValue("name", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var transportMethod = context.Request.RetrievePostedValue("transportmethod", RegexEnum.None, true, ReturnTypeEnum.Xml);

            var isMajorVersion = tagName.EndsWith(".0");
            var generatedAssetsFolderPathOs = CalculateFullPathOs("cmssystemcontentroot") + "/cache/" + tagName;

            var fileCounter = 0;
            var xmlCacheFiles = new XmlDocument();
            var storedFilesPathOs = new List<string>();
            if (isMajorVersion)
            {
                xmlCacheFiles.AppendChild(xmlCacheFiles.CreateElement("files"));

                // Create directory to store generated stuff in
                if (!Directory.Exists(generatedAssetsFolderPathOs))
                {
                    try
                    {
                        Directory.CreateDirectory(generatedAssetsFolderPathOs);
                    }
                    catch (Exception ex)
                    {
                        HandleError(reqVars.returnType, "Could not create directory for expanding files", $"error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }
                else
                {
                    // The directory already exists - we need to clean it up
                    DelTree(generatedAssetsFolderPathOs, false);
                }

                // Grab the data of the files to store, decode it and store it in the cache directory that we have just created (limited to 20 files)
                for (int i = 1; i <= 20; i++)
                {
                    var postedKey = $"file{i}";
                    var postedFileName = request.RetrievePostedValue(postedKey, false);
                    if (!string.IsNullOrEmpty(postedFileName) && (postedFileName.EndsWith(".pdf") || postedFileName.EndsWith(".xlsx") || postedFileName.EndsWith(".docx") || postedFileName.EndsWith(".zip")))
                    {
                        postedKey = $"content{i}";
                        var postedFileContent = request.RetrievePostedValue(postedKey, RegexEnum.None, false);
                        if (!string.IsNullOrEmpty(postedFileContent))
                        {

                            var filePathOs = $"{generatedAssetsFolderPathOs}/{postedFileName}";

                            if (transportMethod == "content")
                            {
                                // We receive file content in the data that was posted
                                var fileType = GetFileType(postedFileName);
                                if (fileType == "text")
                                {
                                    try
                                    {
                                        TextFileCreate(postedFileContent, filePathOs);
                                    }
                                    catch (Exception ex)
                                    {
                                        debugInfo += $", could not store '{filePathOs}' - reason: {ex}";
                                    }

                                }
                                else
                                {
                                    if (!Base64DecodeAsBinaryFile(postedFileContent, filePathOs))
                                    {
                                        debugInfo += $", could not store '{filePathOs}'";
                                    }
                                }
                            }
                            else
                            {
                                // Move the files from the shared folder to the destination path
                                var sourcePathOs = postedFileContent;
                                try
                                {
                                    File.Move(sourcePathOs, filePathOs);
                                }
                                catch (Exception ex)
                                {
                                    HandleError("Could not store the file for the version", $"sourcePathOs: {sourcePathOs}, filePathOs: {filePathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                                }
                            }

                            fileCounter++;

                            // Add file entry to xml file
                            storedFilesPathOs.Add(filePathOs);
                            DateTime creationTime = File.GetCreationTime(filePathOs);
                            var fileName = Path.GetFileNameWithoutExtension(filePathOs);
                            var outputChannelVariantId = RegExpReplace(@"^(.*)...$", fileName, "$1");
                            var filePath = filePathOs.Replace($"{CalculateFullPathOs("cmssystemcontentroot")}", "");
                            var nodeFile = xmlCacheFiles.CreateElementWithText("file", filePath);
                            nodeFile.SetAttribute("ocvariantid", outputChannelVariantId);
                            nodeFile.SetAttribute("type", Path.GetExtension(filePathOs).SubstringAfter("."));
                            nodeFile.SetAttribute("epoch", ToUnixTime(creationTime).ToString());
                            xmlCacheFiles.DocumentElement.AppendChild(nodeFile);
                        }
                    }
                }

            }

            //
            // => Create a bundle zip to allow download all files at once
            //
            if (isMajorVersion && fileCounter > 0)
            {
                var archiveFileName = $"{projectVars.projectId}-{tagName.Replace(".", "")}-bundle.zip";
                using (var stream = File.OpenWrite($"{generatedAssetsFolderPathOs}/{archiveFileName}"))
                using (ZipArchive archive = new ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
                {
                    foreach (var filePathOs in storedFilesPathOs)
                    {
                        archive.CreateEntryFromFile(filePathOs, Path.GetFileName(filePathOs), CompressionLevel.Optimal);
                    }
                }
            }

            //
            // => Commit filing data
            //
            XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
            xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "commitforversion";
            xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = "";
            xmlCommitMessage.SelectSingleNode("/root/id").InnerText = "";
            CommitFilingData(xmlCommitMessage.DocumentElement.InnerXml, ReturnTypeEnum.Xml, false);

            //
            // => Create the annotated GIT tags
            //

            // Change the tag message when we are dealing with a major version that is potentially associated with snapshot files
            if (isMajorVersion && fileCounter > 0)
            {
                var xmlMessage = new XmlDocument();
                xmlMessage.AppendChild(xmlMessage.CreateElement("root"));
                xmlMessage.DocumentElement.AppendChild(xmlMessage.CreateElementWithText("annotation", tagMessage));
                var nodeFilesImported = xmlMessage.ImportNode(xmlCacheFiles.DocumentElement, true);
                xmlMessage.DocumentElement.AppendChild(nodeFilesImported);
                tagMessage = xmlMessage.DocumentElement.InnerXml;
            }

            // 1) for the data repository
            var dataTagSuccess = GitCreateTag(tagName, tagMessage, projectVars.cmsDataRootBasePathOs);
            if (!dataTagSuccess) HandleError(reqVars.returnType, "Could not create a version of the data repository", $"Path: {projectVars.cmsDataRootBasePathOs}, stack-trace: {GetStackTrace()}");

            // 2) for the content repository
            var contentTagSuccess = GitCreateTag(tagName, tagMessage, projectVars.cmsContentRootPathOs);
            if (!contentTagSuccess) HandleError(reqVars.returnType, "Could not create a version of the content repository", $"Path: {projectVars.cmsContentRootPathOs}, stack-trace: {GetStackTrace()}");


            await response.OK(GenerateSuccessXml("Successfully created version", $"versionName: {tagName}, versionMessage: {tagMessage}, projectId: {projectId}"), reqVars.returnType, true);
        }


        /// <summary>
        /// Removes a version from the project data revision system
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task DeleteVersion(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            await DummyAwaiter();

            HandleError(reqVars.returnType, "Not implemented yet", $"stack-trace: {GetStackTrace()}");
        }

        /// <summary>
        /// Resets the version control system to the version tag
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RestoreVersion(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // 
            // => Do the below for the content and the data GIT repositories
            //

            HandleError(reqVars.returnType, "Thrown on purpose in the Taxxor Document Store", $"stack-trace: {GetStackTrace()}");

            //
            // => Find the tags in the repository
            //


            //
            // => Remove newer tags
            //

            // git tag -d v13.3

            //
            // => Reset the working tree to the tag version
            //

            // git reset --hard v13.2

            // Return the response with the XML content in the payload node
            await response.OK(GenerateSuccessXml("Successfully listed cache files", ""), reqVars.returnType, true);


        }




        /// <summary>
        /// Lists all the files from the cache directory (including those generated by the version system)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task ListVersionCacheFiles(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            var cmsSystemContentRootPathOs = CalculateFullPathOs("cmssystemcontentroot");
            var systemCacheRootFolderPathOs = $"{cmsSystemContentRootPathOs}/cache";
            var searchPattern = "*";
            try
            {
                var allCacheFiles = Directory.EnumerateFiles(systemCacheRootFolderPathOs, searchPattern, SearchOption.AllDirectories);

                var xmlFileList = new XmlDocument();
                xmlFileList.LoadXml($"<files/>");
                SetAttribute(xmlFileList.DocumentElement, "basepathos", cmsSystemContentRootPathOs.Replace(applicationRootPathOs, ""));

                foreach (string filePathOs in allCacheFiles)
                {
                    var nodeFile = xmlFileList.CreateElement("file");
                    nodeFile.InnerText = filePathOs.Replace(cmsSystemContentRootPathOs, "");
                    xmlFileList.DocumentElement.AppendChild(nodeFile);
                }

                // Return the response with the XML content in the payload node
                await response.OK(GenerateSuccessXml("Successfully listed cache files", "", HttpUtility.HtmlEncode(xmlFileList.OuterXml)), reqVars.returnType, true);
            }
            catch (Exception ex)
            {
                HandleError(reqVars.returnType, "There was an error retrieving the cache files", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

    }
}