using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LibGit2Sharp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;


namespace Taxxor.Project
{
    /// <summary>
    /// Git Utilities
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Renders an escaped json format of author information to be used in the GIT commit message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="authorId"></param>
        /// <param name="authorName"></param>
        /// <param name="authorEmail"></param>
        /// <param name="ipAddressCustom"></param>
        /// <param name="xmlTemplateId">ID of the XML template to use for the commit message</param>
        /// <returns></returns>
        public static string RenderGitCommitMessageInformation(string message, string? authorId = null, string? authorName = null, string? authorEmail = null, string? ipAddressCustom = null, string xmlTemplateId = "git-commit_message")
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            authorId ??= projectVars.currentUser.Id;
            authorName ??= projectVars.currentUser.FirstName + " " + projectVars.currentUser.LastName;
            authorEmail ??= projectVars.currentUser.Email;
            ipAddressCustom ??= reqVars.ipAddress;

            XmlDocument? xmlMessage = RetrieveTemplate(xmlTemplateId);

            // Message
            if (message.Contains('<') && message.Contains('>'))
            {
                // We assume that the message we want to inject is XML
                xmlMessage.SelectSingleNode("//message").InnerXml = message;
            }
            else
            {
                xmlMessage.SelectSingleNode("//message").InnerText = message;
            }

            // Current date
            XmlNode? nodeDate = xmlMessage.SelectSingleNode("//date");
            DateTime dateNow = DateTime.Now;
            nodeDate.InnerText = dateNow.ToString();
            SetAttribute(nodeDate, "epoch", ToUnixTime(dateNow).ToString());

            // Author info
            xmlMessage.SelectSingleNode("//author/id").InnerText = authorId;
            xmlMessage.SelectSingleNode("//author/name").InnerText = authorName;
            // xmlMessage.SelectSingleNode("//author/ip").InnerText = ipAddressCustom;

            // Convert to XML to string so we can inject it into the commit
            return xmlMessage.OuterXml.Replace("\"", @"\""");
        }


        /// <summary>
        /// Commit all the content to the git repository
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GitCommit(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Get posted data
            var locationId = request.RetrievePostedValue("locationid", @"^([a-z|A-Z|0-9|\-|_]){3,30}$", true, reqVars.returnType);
            var message = request.RetrievePostedValue("message", RegexEnum.None, true, reqVars.returnType);

            var baseDebugInfo = $"locationId: {locationId}, message: {message}";


            var xpath = $"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/location[@id={GenerateEscapedXPathString(locationId)}]";
            var gitDirectory = RetrieveNodeValueIfExists(xpath, xmlApplicationConfiguration);
            if (string.IsNullOrEmpty(gitDirectory))
            {
                await response.Error(GenerateErrorXml("Could not locate repository", $"xpath: {xpath}, {baseDebugInfo}, stack-trace: {GetStackTrace()}"));
            }
            else
            {
                // Retrieve the paths we need to work with
                var gitRepositoryPathOs = dataRootPathOs + gitDirectory;

                if (!GitCommit(message, gitRepositoryPathOs))
                {
                    await response.Error(GenerateErrorXml("Could not commit content", $"gitRepositoryPathOs: {gitRepositoryPathOs}, {baseDebugInfo}, stack-trace: {GetStackTrace()}"));
                }
                else
                {
                    await response.OK(GenerateSuccessXml("Successfully committed content in the repository", $"gitRepositoryPathOs: {gitRepositoryPathOs}, {baseDebugInfo}"));
                }
            }
        }

        /// <summary>
        /// Commit all the content to the git repository
        /// </summary>
        /// <param name="message"></param>
        /// <param name="pathOs"></param>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public static bool GitCommit(string message, string pathOs, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            bool debugRoutine = false;
            List<string> gitCommandList = ["add -A"];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (debugRoutine)
            {
                appLogger.LogDebug("");
                appLogger.LogDebug($"-- git command: '{gitCommandList[0]}'");
                appLogger.LogDebug($"-- gitResult: '{gitResult}'");
            }
            if (gitResult == null)
            {
                HandleGitError("Could not stage to repository.", $"message: {message}, pathOs: {pathOs}, gitResult: {gitResult} - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return false;
            }

            //sleep a bit to update the internal references
            Thread.Sleep(100);

            gitCommandList.Clear();
            gitCommandList.Add("commit -a -m \"" + RenderGitCommitMessageInformation(message) + "\"");
            gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (debugRoutine)
            {
                appLogger.LogDebug("");
                appLogger.LogDebug($"-- git command: '{gitCommandList[0]}'");
                appLogger.LogDebug($"-- gitResult: '{gitResult}'");
            }
            if (string.IsNullOrEmpty(gitResult) || gitResult.Contains("nothing to commit"))
            {
                HandleGitError("Could not commit to repository as there is nothing to commit.", $"message: {message}, pathOs: {pathOs}, gitResult: {gitResult} - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return false;
            }
            else
            {
                if (gitResult.Contains("index.lock") && gitResult.ToLower().Contains("fatal"))
                {
                    appLogger.LogError($"Found a lock file in the git repository {pathOs}, so we will actively remove it to avoid further problems");
                    try
                    {
                        var cmd = $"find {pathOs}/ -type f -name \"index.lock\" -delete";
                        var result = ExecuteCommandLineAsync(cmd, dataRootPathOs).GetAwaiter().GetResult();
                        if (!result.Success)
                        {
                            appLogger.LogWarning($"Unable to execute git lock removal routine. result: {result.ToString()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"There was an error removing the git lock files. error: {ex}");
                    }
                    HandleGitError("Could not commit to repository because it's locked.", $"message: {message}, pathOs: {pathOs}, gitResult: {gitResult} - more information in the Taxxor log files", returnType, stopProcessingOnError);

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Retrieves all the commit messages of a GIT repository as a string
        /// </summary>
        /// <param name="pathOs">GIT repository path</param>
        /// <param name="grepFilter">A string that needs to be contained in the message (find string)</param>
        /// <param name="outputFormat">GIT --pretty format (see https://git-scm.com/docs/git-log, section PRETTY FORMATS), by default shows only the commit message</param>
        /// <param name="limitResults">Limit the results to the provided number</param>
        /// <param name="returnType">Error message format</param>
        /// <param name="stopProcessingOnError">Should we stop when we encounter a problem?</param>
        /// <returns></returns>
        public static string GitRetrieveCommitMessages(string pathOs, string? grepFilter = null, string outputFormat = @"""%s""", int limitResults = -1, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            // Construct the GIT command
            string gitCommand = @"--no-pager log --decorate=short --pretty=format:" + outputFormat;
            if (grepFilter != null) gitCommand += @" --grep=""" + grepFilter + @"""";
            if (limitResults > 0) gitCommand += " -n" + limitResults.ToString();

            List<string> gitCommandList = [gitCommand];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                HandleGitError("Could not retrieve commit messages.", "pathOs: " + pathOs + " - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return "";
            }

            return gitResult;
        }



        /// <summary>
        /// Retrieve the commit message of a single commit
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="pathOs"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string GitRetrieveMessageOfHash(string hash, string pathOs, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            List<string> gitCommandList = [$"git log -n 1 --pretty=format:%s {hash}"];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                string errorMessage = $"Unable to retrieve message of tag {hash}";
                HandleGitError(errorMessage, $"tag: {hash}, pathOs: {pathOs} - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return $"ERROR: {errorMessage}";
            }

            return gitResult.Trim();
        }


        /// <summary>
        /// Retrieve the message of an annotated tag
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="pathOs"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string GitRetrieveMessageOfTag(string tagName, string pathOs, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            List<string> gitCommandList = [$"git tag -l --format='%(contents)' {tagName}"];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                string errorMessage = $"Unable to retrieve message of tag {tagName}";
                HandleGitError(errorMessage, $"tag: {tagName}, pathOs: {pathOs} - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return $"ERROR: {errorMessage}";
            }

            return gitResult.Trim();
        }

        /// <summary>
        /// Retrieves the hash of the first commit in the repository
        /// </summary>
        /// <param name="pathOs"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string GitRetrieveHashFirstCommit(string pathOs, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {

            List<string> gitCommandList = ["git rev-list --max-parents=0 HEAD"];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                string errorMessage = "Unable to retrieve reference to first commit";
                HandleGitError(errorMessage, $"pathOs: {pathOs} - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return $"ERROR: {errorMessage}";
            }

            return gitResult.Trim();
        }

        /// <summary>
        /// Retrieves the commit hash of a tag
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="pathOs"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string GitRetrieveHashOfTag(string tagName, string pathOs, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            List<string> gitCommandList = [$"git show-ref -s {tagName}"];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                var errorMessage = $"Unable to retrieve reference to tag {tagName}";
                HandleGitError(errorMessage, $"tag: {tagName}, pathOs: {pathOs} - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return $"ERROR: {errorMessage}";
            }

            return gitResult.Trim();
        }

        /// <summary>
        /// Retrieves the files involved in a commit
        /// </summary>
        /// <param name="hash">Hash of the commit to retrieve the files from</param>
        /// <param name="pathOs"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string[] GitRetrieveFilesFirstCommit(string hash, string pathOs, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {

            List<string> gitCommandList = [$"git ls-tree --name-only -r {hash}"];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                HandleGitError("Unable to retrieve the files of the first commit", $"pathOs: {pathOs} - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return [];
            }
            string[] fileEntries = Regex.Split(gitResult, "[,\r\n]+");
            fileEntries = Array.ConvertAll(fileEntries, x => x.Trim());
            fileEntries = fileEntries.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            return fileEntries;
        }

        /// <summary>
        /// Creates an annotated git tag
        /// </summary>
        /// <param name="version"></param>
        /// <param name="message"></param>
        /// <param name="pathOs"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static bool GitCreateTag(string version, string message, string pathOs, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            List<string> gitCommandList =
            [
                "tag -a " + version + " -m \"" + RenderGitCommitMessageInformation(message, null, null, null, null, "git-tag_message") + "\"",
            ];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                HandleGitError("Could not create tag.", $"version: {version}, pathOs: {pathOs} - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return false;
            }

            return true;
        }


        /// <summary>
        /// Retrieves all git tag subject messages
        /// </summary>
        /// <param name="pathOs"></param>
        /// <param name="outputFormat"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string GitRetrieveTagMessages(string pathOs, string outputFormat = @"""%(contents)""", ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            var debugRoutine = false;

            // Construct the GIT command
            //for-each-ref --format="<tag-meta tagname='%(refname:short)' hash='%(objectname)'>%(subject)</tag-meta>" refs/tags
            var gitCommand = @"for-each-ref --format=" + outputFormat + " refs/tags";

            if (debugRoutine)
            {
                Console.WriteLine("###############");
                Console.WriteLine($"- pathOs: {pathOs}");
                Console.WriteLine($"- command: {gitCommand}");
            }

            List<string> gitCommandList = [gitCommand];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                HandleGitError("Could not retrieve tags.", "pathOs: " + pathOs + " - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return "";
            }

            // For some reason, git may ignore the echo off statement provided and return the complete commandline output...
            if (gitResult.Contains("echo off"))
            {
                gitResult = After(gitResult, gitCommand);
            }

            if (debugRoutine)
            {
                Console.WriteLine($"- result: {gitResult}");
                Console.WriteLine("###############");
            }

            return gitResult;
        }

        /// <summary>
        /// Returns the difference between two commits in a GIT repository
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GitDiffBetweenCommits(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Get posted data
            var baseCommitHash = (applicationId == "taxxoreditor") ? request.RetrievePostedValue("basecommithash", RegexEnum.Hash, true, reqVars.returnType) : request.RetrievePostedValue("basecommithash");
            var latestCommitHash = (applicationId == "taxxoreditor") ? request.RetrievePostedValue("latestcommithash", RegexEnum.Hash, true, reqVars.returnType) : request.RetrievePostedValue("latestcommithash");
            var locationId = request.RetrievePostedValue("locationid", @"^([a-z|A-Z|0-9|\-|_]){3,30}$", true, reqVars.returnType);
            var gitFilePath = request.RetrievePostedValue("gitfilepath", RegexEnum.FilePath, false, reqVars.returnType);

            var baseDebugInfo = $"baseCommitHash: {baseCommitHash}, latestCommitHash: {latestCommitHash}, locationId: {locationId}" + ((string.IsNullOrEmpty(gitFilePath)) ? "" : $", gitFilePath: {gitFilePath}");


            var xpath = $"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/location[@id={GenerateEscapedXPathString(locationId)}]";
            var gitDirectory = RetrieveNodeValueIfExists(xpath, xmlApplicationConfiguration);
            if (string.IsNullOrEmpty(gitDirectory))
            {
                await response.Error(GenerateErrorXml("Could not locate repository", $"xpath: {xpath}, {baseDebugInfo}, stack-trace: {GetStackTrace()}"));
            }
            else
            {
                var cmsDataRootPathOs = dataRootPathOs + gitDirectory;

                if (!GitIsActiveRepository(cmsDataRootPathOs))
                {
                    await response.Error(GenerateErrorXml("Location is not a GIT repository", $"cmsDataRootPathOs: {cmsDataRootPathOs}, {baseDebugInfo}, stack-trace: {GetStackTrace()}"));
                }
                else
                {

                    var gitDiffResult = GitDiffBetweenCommits(cmsDataRootPathOs, baseCommitHash, latestCommitHash, gitFilePath, ReturnTypeEnum.Txt, true);

                    if (!string.IsNullOrEmpty(gitDiffResult))
                    {
                        await response.OK(GenerateSuccessXml($"Successfully generated diff", $"{baseDebugInfo}", gitDiffResult));
                    }
                    else
                    {
                        await response.OK(GenerateSuccessXml("No differences found", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}"));
                    }
                }
            }
        }

        /// <summary>
        /// Returns the difference between two commits in a GIT repository
        /// </summary>
        /// <param name="repositoryPathOs"></param>
        /// <param name="baseCommitHash"></param>
        /// <param name="latestCommitHash"></param>
        /// <param name="gitFilePath"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string GitDiffBetweenCommits(string repositoryPathOs, string baseCommitHash, string latestCommitHash, string? gitFilePath = null, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            // Execute the command to restore the file contents and save it to a new locaion
            // git diff 5b4c649d34dea644276c1f6b66fa73c810c3dbd0 13b69f27ed19a8c89ea304e9bf45a085b043ca53
            string gitCommand = $"git diff {baseCommitHash} {latestCommitHash}";
            if (!string.IsNullOrEmpty(gitFilePath)) gitCommand += $" {gitFilePath}";

            //DebugVariable(() => gitCommand);

            List<string> gitCommandList = [gitCommand];

            string? gitResult = GitCommand(gitCommandList, repositoryPathOs, returnType, stopProcessingOnError);
            if (gitResult == null || gitResult.Trim().ToLower().StartsWith("fatal:"))
            {
                HandleGitError("Could render diff.", "repositoryPathOs: " + repositoryPathOs + " - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return "";
            }

            // For some reason, git may ignore the echo off statement provided and return the complete commandline output...
            if (gitResult.Contains("echo off"))
            {
                gitResult = After(gitResult, gitCommand);
            }

            return gitResult;
        }


        /// <summary>
        /// Extract all the files in a GIT commit to a location on the disk
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GitExtractAll(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Get posted data
            var commitHash = request.RetrievePostedValue("commithash", RegexEnum.HashOrTag, true, reqVars.returnType);
            var locationId = request.RetrievePostedValue("locationid", @"^([a-z|A-Z|0-9|\-|_]){3,30}$", true, reqVars.returnType);
            var extractFolderPath = request.RetrievePostedValue("extractfolderpath", RegexEnum.FilePath, true, reqVars.returnType);

            var extractAllResults = GitExtractAll(projectVars.projectId, locationId, commitHash, extractFolderPath);
            if (extractAllResults.Success)
            {
                await response.OK(GenerateSuccessXml($"Successfully restored {extractAllResults.Payload.Split(",").Length} files", $"restoreResults: {extractAllResults.Payload}"));
            }
            else
            {
                await response.Error(extractAllResults);
            }
        }

        /// <summary>
        /// Extract all the files in a GIT commit to a location on the disk
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="locationId"></param>
        /// <param name="commitHash"></param>
        /// <param name="extractFolderPath"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage GitExtractAll(string projectId, string locationId, string commitHash, string extractFolderPath)
        {
            var baseDebugInfo = $"commitHash: {commitHash}, locationId: {locationId}, extractFolderPath: {extractFolderPath}";

            var xpath = $"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectId)}]/location[@id={GenerateEscapedXPathString(locationId)}]";
            var gitDirectory = RetrieveNodeValueIfExists(xpath, xmlApplicationConfiguration);
            if (string.IsNullOrEmpty(gitDirectory))
            {
                return new TaxxorReturnMessage(false, "Could not locate repository", $"xpath: {xpath}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                // Retrieve the paths we need to work with
                var cmsDataRootPathOs = dataRootPathOs + gitDirectory;
                var extractFolderPathOs = (RegExpTest(@"^[a-zA-Z0-9\.\-_].*$", extractFolderPath)) ? $"{RetrieveSharedFolderPathOs()}/{extractFolderPath}" : extractFolderPath;

                // Make sure that the extract folder exists

                try
                {
                    if (!Directory.Exists(extractFolderPathOs)) Directory.CreateDirectory(extractFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not create extract directory", $"extractFolderPathOs: {extractFolderPathOs}, error: {ex}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");
                }


                if (!GitIsActiveRepository(cmsDataRootPathOs))
                {
                    return new TaxxorReturnMessage(false, "Location is not a GIT repository", $"cmsDataRootPathOs: {cmsDataRootPathOs}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    var restoreResults = new List<string>();
                    var filesInCommit = GitRetrieveFileListFromCommit(cmsDataRootPathOs, commitHash, ReturnTypeEnum.Html, true);
                    foreach (var relativeFilePath in filesInCommit)
                    {
                        var fileSourcePathOs = $"{cmsDataRootPathOs}/{relativeFilePath}";

                        var fileTargetPathOs = $"{extractFolderPathOs}/{Path.GetFileName(fileSourcePathOs)}";

                        // Restore the file from the repository
                        var result = GitExtractSingleFile(cmsDataRootPathOs, commitHash, fileSourcePathOs, fileTargetPathOs, ReturnTypeEnum.Html, true);

                        restoreResults.Add(result);
                    }

                    if (restoreResults.Count > 0)
                    {
                        return new TaxxorReturnMessage(true, $"Successfully restored {restoreResults.Count} files", string.Join(",", restoreResults.ToArray()), $"{baseDebugInfo}");
                    }
                    else
                    {
                        return new TaxxorReturnMessage(false, "Unable to restore any files", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");
                    }
                }
            }
        }

        /// <summary>
        /// Extract one specific file from a GIT commit to a location on the disk
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GitExtractSingleFile(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Get posted data
            var commitHash = request.RetrievePostedValue("commithash", RegexEnum.HashOrTag, true, reqVars.returnType);
            var locationId = request.RetrievePostedValue("locationid", @"^([a-z|A-Z|0-9|\-|_]){3,30}$", true, reqVars.returnType);
            var sourceFilePath = request.RetrievePostedValue("sourcefilepath", RegexEnum.FilePath, true, reqVars.returnType);
            var extractFolderPath = request.RetrievePostedValue("extractfolderpath", RegexEnum.FilePath, false, reqVars.returnType, null);

            var baseDebugInfo = $"commitHash: {commitHash}, locationId: {locationId}, sourceFilePath: {sourceFilePath}, extractFolderPath: {extractFolderPath}";

            // Do we need to return the contents of the file or do we need to extract it on the disk somewhere?
            var fileContentsMode = string.IsNullOrEmpty(extractFolderPath);

            // Only return the contents of a text based file
            if (fileContentsMode)
            {
                var fileType = GetFileType(sourceFilePath);
                if (fileType != "text") await response.Error(GenerateErrorXml($"Cannot return the file contents of a {fileType}", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}"));
            }


            // TODO: this is 95% the same as GitExtractAll() above - should merge

            var xpath = $"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/location[@id={GenerateEscapedXPathString(locationId)}]";
            var gitDirectory = RetrieveNodeValueIfExists(xpath, xmlApplicationConfiguration);
            if (string.IsNullOrEmpty(gitDirectory))
            {
                await response.Error(GenerateErrorXml("Could not locate repository", $"xpath: {xpath}, {baseDebugInfo}, stack-trace: {GetStackTrace()}"));
            }
            else
            {
                // Retrieve the paths we need to work with
                var cmsDataRootPathOs = dataRootPathOs + gitDirectory;
                var extractFolderPathOs = (RegExpTest(@"^[a-zA-Z0-9\.\-_].*$", extractFolderPath)) ? $"{RetrieveSharedFolderPathOs()}/{extractFolderPath}" : extractFolderPath;

                // Make sure that the extract folder exists
                var directoryCheckAndCreateSuccess = true;
                if (!fileContentsMode)
                {
                    try
                    {
                        if (!Directory.Exists(extractFolderPathOs)) Directory.CreateDirectory(extractFolderPathOs);
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Could not create extract directory");
                        await response.Error(GenerateErrorXml("Could not create extract directory", $"extractFolderPathOs: {extractFolderPathOs}, error: {ex}, {baseDebugInfo}, stack-trace: {GetStackTrace()}"));
                        directoryCheckAndCreateSuccess = false;
                    }
                }


                if (directoryCheckAndCreateSuccess)
                {
                    if (!GitIsActiveRepository(cmsDataRootPathOs))
                    {
                        appLogger.LogError($"Location is not a GIT repository");
                        await response.Error(GenerateErrorXml("Location is not a GIT repository", $"cmsDataRootPathOs: {cmsDataRootPathOs}, {baseDebugInfo}, stack-trace: {GetStackTrace()}"));
                    }
                    else
                    {
                        var restoreResults = new List<string>();
                        var filesInCommit = GitRetrieveFileListFromCommit(cmsDataRootPathOs, commitHash, ReturnTypeEnum.Html, true);
                        foreach (var relativeFilePath in filesInCommit)
                        {
                            // Console.WriteLine($"- sourceFilePath: {sourceFilePath}");
                            // Console.WriteLine($"- relativeFilePath: {relativeFilePath}");
                            var fileSourcePathOs = $"{cmsDataRootPathOs}/{relativeFilePath}";
                            // Console.WriteLine($"- fileSourcePathOs: {fileSourcePathOs}");

                            if (sourceFilePath.EndsWith(relativeFilePath))
                            {

                                if (fileContentsMode)
                                {
                                    // Retrieve the file contents from the repository
                                    var result = GitExtractSingleFileContents(cmsDataRootPathOs, commitHash, fileSourcePathOs, ReturnTypeEnum.Html, true);

                                    restoreResults.Add(result);
                                }
                                else
                                {
                                    // This is the file we want to restore
                                    var fileTargetPathOs = $"{extractFolderPathOs}/{Path.GetFileName(fileSourcePathOs)}";


                                    // Restore the file from the repository
                                    var result = GitExtractSingleFile(cmsDataRootPathOs, commitHash, fileSourcePathOs, fileTargetPathOs, ReturnTypeEnum.Html, true);

                                    restoreResults.Add(result);
                                }
                            }
                        }

                        if (restoreResults.Count > 0)
                        {
                            if (fileContentsMode)
                            {
                                await response.OK(GenerateSuccessXml($"Successfully restored {restoreResults.Count} files", baseDebugInfo, restoreResults[0]));
                            }
                            else
                            {
                                await response.OK(GenerateSuccessXml($"Successfully restored {restoreResults.Count} files", $"restoreResults: {string.Join(",", restoreResults.ToArray())}, {baseDebugInfo}"));
                            }
                        }
                        else
                        {
                            appLogger.LogError($"Unable to restore any files {baseDebugInfo}");
                            HandleError(ReturnTypeEnum.Xml, "Unable to restore any files", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");
                        }
                    }
                }

            }

        }


        /// <summary>
        /// Extracts a file from a GIT commit and saves it to a new location
        /// </summary>
        /// <param name="repositoryPathOs"></param>
        /// <param name="commitHash"></param>
        /// <param name="filePathOsSource"></param>
        /// <param name="filePathOsTarget"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string GitExtractSingleFile(string repositoryPathOs, string commitHash, string filePathOsSource, string filePathOsTarget, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            if (!filePathOsSource.Contains(repositoryPathOs))
            {
                HandleGitError("Path mismatch", $"repositoryPathOs: {repositoryPathOs} not contained in filePathOsSource: {filePathOsSource} - more information in the Taxxor log files", returnType, stopProcessingOnError);
            }

            // Normalize the paths
            var gitFilePath = filePathOsSource.Replace($"{repositoryPathOs}/", "");


            // Execute the command to restore the file contents and save it to a new location
            string gitCommand = $"git show {commitHash}:{gitFilePath} > {filePathOsTarget}";

            //DebugVariable(() => gitCommand);

            List<string> gitCommandList = [gitCommand];

            string? gitResult = GitCommand(gitCommandList, repositoryPathOs, returnType, stopProcessingOnError);
            if (gitResult == null || gitResult.Trim().ToLower().StartsWith("fatal:"))
            {
                HandleGitError("Could restore file from repository.", "repositoryPathOs: " + repositoryPathOs + " - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return "";
            }

            // For some reason, git may ignore the echo off statement provided and return the complete commandline output...
            if (gitResult.Contains("echo off"))
            {
                gitResult = After(gitResult, gitCommand);
            }

            return gitResult;
        }

        /// <summary>
        /// Extracts the file contents from a git commit and returns it as a string
        /// </summary>
        /// <param name="repositoryPathOs"></param>
        /// <param name="commitHash"></param>
        /// <param name="filePathOsSource"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string GitExtractSingleFileContents(string repositoryPathOs, string commitHash, string filePathOsSource, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            if (!filePathOsSource.Contains(repositoryPathOs))
            {
                HandleGitError("Path mismatch", $"repositoryPathOs: {repositoryPathOs} not contained in filePathOsSource: {filePathOsSource} - more information in the Taxxor log files", returnType, stopProcessingOnError);
            }

            // Normalize the paths
            var gitFilePath = filePathOsSource.Replace($"{repositoryPathOs}/", "");


            // Execute the command to restore the file contents and save it to a new location
            string gitCommand = $"git show {commitHash}:{gitFilePath}";

            //DebugVariable(() => gitCommand);

            List<string> gitCommandList = [gitCommand];

            string? gitResult = GitCommand(gitCommandList, repositoryPathOs, returnType, stopProcessingOnError);
            if (gitResult == null || gitResult.Trim().ToLower().StartsWith("fatal:"))
            {
                HandleGitError("Could restore file from repository.", "repositoryPathOs: " + repositoryPathOs + " - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return "";
            }

            // For some reason, git may ignore the echo off statement provided and return the complete commandline output...
            if (gitResult.Contains("echo off"))
            {
                gitResult = After(gitResult, gitCommand);
            }

            return gitResult;
        }


        /// <summary>
        /// Returns a list of files that were part of a GIT commit
        /// </summary>
        /// <param name="repositoryPathOs"></param>
        /// <param name="commitHash"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static List<string> GitRetrieveFileListFromCommit(string repositoryPathOs, string commitHash, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            var fileList = new List<string>();

            // Execute the command to restore the file contents and save it to a new locaion
            string gitCommand = $"git diff-tree --no-commit-id --name-only -r {commitHash}";

            //DebugVariable(() => gitCommand);

            List<string> gitCommandList = [gitCommand];

            string? gitResult = GitCommand(gitCommandList, repositoryPathOs, returnType, stopProcessingOnError);
            if (gitResult == null || gitResult.Trim().ToLower().StartsWith("fatal:"))
            {
                HandleGitError("Could list files from commit in repository.", "repositoryPathOs: " + repositoryPathOs + " - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return fileList;
            }

            // For some reason, git may ignore the echo off statement provided and return the complete commandline output...
            if (gitResult.Contains("echo off"))
            {
                gitResult = After(gitResult, gitCommand);
            }

            // Parse the result into a list
            string[] filesFromCommitRaw = gitResult.Split(new[] { System.Environment.NewLine }, StringSplitOptions.None);
            foreach (var fileEntryRaw in filesFromCommitRaw)
            {
                var fileEntry = fileEntryRaw.Trim();
                if (!string.IsNullOrEmpty(fileEntry) && RegExpTest(@"^.*\.([a-z]|[A-Z]|[0-9]){1,5}$", fileEntry))
                {
                    fileList.Add(fileEntry);
                }
            }

            // Console.WriteLine("%%%%%%%%%% GitRetrieveFileListFromCommit %%%%%%%%%%");
            // Console.WriteLine(gitCommand);
            // Console.WriteLine("---");
            // Console.WriteLine(gitResult);
            // Console.WriteLine("---");
            // Console.WriteLine(string.Join(",", fileList.ToArray()));
            // Console.WriteLine("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");

            return fileList;
        }


        /// <summary>
        /// Retrieves the last commit message from a GIT repository
        /// </summary>
        /// <param name="location"></param>
        /// <param name="messageOnly"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string? GitRetrieveLatestCommitInfo(string location, bool messageOnly = true, bool stopProcessingOnError = false)
        {
            string message;
            string debugInfo;
            ReturnTypeEnum returnType = ReturnTypeEnum.Txt;

            var pathOs = location.Contains('/') ? location : RetrieveGitRepositoryLocation(location);

            List<string> gitCommandList = new List<string>();
            gitCommandList.Add(messageOnly ? "git log -1 --pretty=%B" : "git log -1");
            var gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                message = "Could not retrieve the last git commit message";
                debugInfo = $"pathOs: {pathOs} - more information in the log files of {applicationId}";
                if (stopProcessingOnError)
                {
                    HandleGitError(message, debugInfo, returnType, stopProcessingOnError);
                }
                else
                {
                    return $"ERROR: {message}, {debugInfo}";
                }
            }
            else
            {
                if (gitResult.Contains("fatal: "))
                {
                    message = "Could not retrieve the last commit message";
                    debugInfo = $"pathOs: {pathOs} - cmdresult: {gitResult}";
                    if (stopProcessingOnError)
                    {
                        HandleGitError(message, debugInfo, returnType, stopProcessingOnError);
                    }
                    else
                    {
                        return $"ERROR: {message}, {debugInfo}";
                    }
                }
            }


            return gitResult;
        }

        /// <summary>
        /// Retrieve the branchname of the branch currently selected branch
        /// </summary>
        /// <param name="location"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static string? GitRetrieveCurrentBranchName(string location, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            string message;
            string debugInfo;

            var pathOs = location.Contains('/') ? location : RetrieveGitRepositoryLocation(location);
            List<string> gitCommandList = ["git branch"];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                message = "Could not retrieve the branchname";
                debugInfo = $"pathOs: {pathOs} - more information in the log files of {applicationId}";
                if (stopProcessingOnError)
                {
                    HandleGitError(message, debugInfo, returnType, stopProcessingOnError);
                }
                else
                {
                    return $"ERROR: {message}, {debugInfo}";
                }
            }
            else
            {
                if (gitResult.Contains("fatal: "))
                {
                    message = "Could not retrieve the branchname";
                    debugInfo = $"pathOs: {pathOs} - cmdresult: {gitResult}";
                    if (stopProcessingOnError)
                    {
                        HandleGitError(message, debugInfo, returnType, stopProcessingOnError);
                    }
                    else
                    {
                        return $"ERROR: {message}, {debugInfo}";
                    }
                }
            }

            // Retrieve the current branchname from the output
            string? currentBranchName = RegExpReplace(@"^.*\*\s+([a-z0-9\-_]+)([\n\r]+.*)*$", gitResult, "$1", true);

            // Console.WriteLine($"- currentBranchName: {currentBranchName}");

            return currentBranchName;
        }

        /// <summary>
        /// Pulls a git repository and it's submodules 
        /// </summary>
        /// <param name="location">Location ID or full path to GIT repository</param>
        /// <param namr="remoteName">Name of the remote repository (defaults to "origin")</param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage GitPull(string location, string remoteName = "origin", string? branchName = null, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            var pathOs = location.Contains('/') ? location : RetrieveGitRepositoryLocation(location);
            string message;
            string debugInfo;

            // Assume that we need to pull the branch currently selected when not explicitly specified
            var currentBranchName = branchName ?? GitRetrieveCurrentBranchName(pathOs, ReturnTypeEnum.Txt, true);

            // Pull the repositories and all submodules
            List<string> gitCommandList =
            [
                $"git pull --progress --recurse-submodules --no-rebase origin {currentBranchName}",
                "git submodule update --init --recursive",
            ];

            string? gitResult = GitCommand(gitCommandList, pathOs, returnType, false);
            if (gitResult == null)
            {
                message = "Could not pull the repository";
                debugInfo = $"pathOs: {pathOs} - more information in the log files of the Taxxor Document Store";
                if (stopProcessingOnError)
                {
                    HandleGitError(message, debugInfo, returnType, stopProcessingOnError);
                }
                else
                {
                    return new TaxxorReturnMessage(false, message, debugInfo);
                }
            }
            else
            {
                if (gitResult.Contains("fatal: "))
                {
                    message = "Could not pull the repository";
                    debugInfo = $"pathOs: {pathOs} - cmdresult: {gitResult}";
                    if (stopProcessingOnError)
                    {
                        HandleGitError(message, debugInfo, returnType, stopProcessingOnError);
                    }
                    else
                    {
                        return new TaxxorReturnMessage(false, message, debugInfo);
                    }
                }
            }

            // Get the latest commit message and use that as the payload content
            var latestCommitMessage = GitRetrieveLatestCommitInfo(location, false);

            // For some reason, git may ignore the echo off statement provided and return the complete commandline output...
            if (gitResult.Contains("echo off"))
            {
                gitResult = After(gitResult, gitCommandList[gitCommandList.Count - 1]);
            }

            // Return the result
            return new TaxxorReturnMessage(true, "Successfully pulled repository", latestCommitMessage, gitResult);
        }

        /// <summary>
        /// Pulls a GIT repository and it's submodules and streams the result to the client
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GitPull(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");
            var extendedLogging = false;

            if (extendedLogging) appLogger.LogCritical("***************************** In GitPull() Method ****************************");

            // Get posted data
            var location = context.Request.RetrievePostedValue("location", @"^[a-zA-Z_\-\d\/\:]{1,512}$", true, reqVars.returnType);
            var remoteName = context.Request.RetrievePostedValue("remotename", @"^[a-zA-Z_\-\d]{1,70}$", false, reqVars.returnType, "origin");
            var component = context.Request.RetrievePostedValue("component", @"^(taxxoreditor|documentstore)$", true, reqVars.returnType);

            // Test if we need to make a remote request for the git pull or not
            TaxxorReturnMessage gitPullResult;
            if (applicationId == "taxxoreditor" && component == "documentstore")
            {
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectVars.projectId },
                    { "location", location },
                    { "remotename", remoteName },
                    { "component", component }
                };
                XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitpull", dataToPost, debugRoutine);

                // Directly convert the received XML from the remote to a Taxxor Return Message and proceed
                gitPullResult = new TaxxorReturnMessage(responseXml);
            }
            else
            {
                gitPullResult = GitPull(location, remoteName);
            }

            //
            // => Exit the method completely if the GIT pull was not successful
            //
            if (!gitPullResult.Success) HandleError(reqVars.returnType, gitPullResult.ToXml());

            var messagePrefix = RenderMessagePrefix("Git pull");
            gitPullResult.Payload = $"{messagePrefix}* result:\n{gitPullResult.DebugInfo}\n* commit-message:\n{gitPullResult.Payload}";
            gitPullResult.DebugInfo = $"";


            if (location.StartsWith("devpack"))
            {
                //
                // => Clear the file system exists cache so that newly created files are picked up
                //
                Extensions.ClearFsExistsCache();

                //
                // => Re-compile the output channel assets using the Taxxor Asset Compiler
                //
                var outputChannelType = location.SubstringAfter("-");
                var dataToPost = new Dictionary<string, string>
                {
                    { "type", outputChannelType },
                    { "clientid", TaxxorClientId }
                };

                XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AssetCompiler, RequestMethodEnum.Get, "compileassets", dataToPost, debugRoutine);
                if (debugRoutine) Console.WriteLine(responseXml.OuterXml);
                if (XmlContainsError(responseXml))
                {
                    HandleError(reqVars.returnType, responseXml);
                }
                else
                {
                    //
                    // => Append the information from the asset compiler to the response
                    //
                    messagePrefix = RenderMessagePrefix("Asset compiler");
                    var assetCompilerMessage = responseXml.SelectSingleNode("//success")?.InnerText ?? "";
                    if (string.IsNullOrEmpty(assetCompilerMessage)) assetCompilerMessage = responseXml.SelectSingleNode("//message")?.InnerText ?? "";
                    gitPullResult.DebugInfo += $"";
                    gitPullResult.Payload += $"{messagePrefix}* result:\n{responseXml.SelectSingleNode("//debuginfo")?.InnerText ?? ""}\n\n{responseXml.SelectSingleNode("//error")?.InnerText ?? ""}\n* compiler messages:\n{responseXml.SelectSingleNode("//success")?.InnerText ?? responseXml.SelectSingleNode("//message")?.InnerText ?? ""}";

                    //
                    // => Clear the file system exists cache so that newly created files are picked up
                    //
                    Extensions.ClearFsExistsCache();
                }
            }

            // Update the stored tags with the new tag information of the package we just pulled
            var successfullyUpdatedRepositoryVersionInformation = await RetrieveTaxxorGitRepositoriesVersionInfo(false);
            if ((siteType == "local" || siteType == "dev") && !successfullyUpdatedRepositoryVersionInformation) appLogger.LogInformation($"- RetrieveTaxxorGitRepositoriesVersionInfo.success: {successfullyUpdatedRepositoryVersionInformation.ToString()}");

            if (extendedLogging) appLogger.LogCritical("******************************************************************************");

            //
            // => Clear the paged media cache
            //
            ContentCache.Clear();


            if (gitPullResult.Success)
            {
                await response.OK(gitPullResult.ToXml(), reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars.returnType, gitPullResult.ToXml());
            }

            static string RenderMessagePrefix(string basePrefix)
            {
                return $"\n\n=> {basePrefix}:\n";
            }
        }

        /// <summary>
        /// Determines if a GIT repository is active (has a .git directory) or not on a remote machine
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GitIsActiveRepository(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Get posted data
            var location = context.Request.RetrievePostedValue("location", @"^[a-zA-Z_\-\d\/\:]{1,512}$", true, reqVars.returnType);

            if (GitIsActiveRepository(location))
            {
                await response.OK(GenerateSuccessXml("true"), reqVars.returnType, true);
            }
            else
            {
                await response.OK(GenerateSuccessXml("false"), reqVars.returnType, true);
            }
        }

        /// <summary>
        /// Returns if the location is an active GIT repository or not
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public static bool GitIsActiveRepository(string location)
        {
            var pathOs = location.Contains('/') ? location : RetrieveGitRepositoryLocation(location) ?? CalculateFullPathOs(location);
            if (string.IsNullOrEmpty(pathOs))
            {
                appLogger.LogWarning($"GIT repository in location {location} could not be resolved");
                return false;
            }
            var repositoryFolderOs = (pathOs.Contains(".git")) ? pathOs : $"{pathOs}/.git";
            return Directory.Exists(repositoryFolderOs);
        }




        /// <summary>
        /// Exports a complete working tree of a GIT repository commit hash to the output directory provided
        /// </summary>
        /// <param name="reproPathOs">Fully qualified OS path to GIT repository we need to use</param>
        /// <param name="commitHash">Commit hash to use as a base for the extraction logic</param>
        /// <param name="outputSubDirectory">Subdirectory or fully qualified path or ID of a location from the configuration used to expand the files to</param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns>The full OS path of the directory where the files were unzipped to</returns>
        public static string? ExportGitWorkingDirectory(string reproPathOs, string commitHash, string outputSubDirectory, string? projectId = null, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Create a temporary path for the ZIP we want to extract
            var zipPathOs = applicationRootPathOs + "/temp/" + GenerateRandomString(12, false) + ".zip";

            // Construct a folder path where we want to extract the files to
            var outputDirectoryPathOs = dataRootPathOs + "/temp/" + ((string.IsNullOrEmpty(projectId) ? "unknown_projectid" : projectId)) + "/" + ((string.IsNullOrEmpty(outputSubDirectory) ? GenerateRandomString(8, false) : outputSubDirectory));

            // If the path supplied is a fully qualified folder path, then use it directly
            if (outputSubDirectory.Contains('/') || outputSubDirectory.Contains('\\') || outputSubDirectory.Contains(@": "))
            {
                outputDirectoryPathOs = outputSubDirectory;
            }
            else
            {
                // Check if the path supplied can be found in a location node in the xml application configuration
                XmlNode? nodeLocation = xmlApplicationConfiguration.SelectSingleNode(" //location[@id='" + outputSubDirectory + "']");
                if (nodeLocation == null)
                {
                    // We could not find a path to extract to...
                    //return "ERROR: Could not construct a path to extract the GIT ZIP file to";
                }
                else
                {
                    // The outputSubDirectory passed is a location in the configuration - calculate the full path
                    outputDirectoryPathOs = CalculateFullPathOs(nodeLocation);
                }
            }


            // Store the output path as the response message
            var responseMessage = outputDirectoryPathOs;

            // Create directory for export zip
            if (!Directory.Exists(Path.GetDirectoryName(zipPathOs)))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(zipPathOs));
                }
                catch (Exception ex)
                {
                    HandleGitError("Could not create directory for exporting zip", ex.ToString(), returnType, stopProcessingOnError);
                }
            }


            // Create directory for expanding files
            if (!Directory.Exists(outputDirectoryPathOs))
            {
                try
                {
                    Directory.CreateDirectory(outputDirectoryPathOs);
                }
                catch (Exception ex)
                {
                    HandleGitError("Could not create directory for expanding files", ex.ToString(), returnType, stopProcessingOnError);
                }
            }
            else
            {
                // The directory already exists - we need to clean it up
                DelTree(outputDirectoryPathOs, false);
            }


            // Export the commit as a ZIP file

            List<string> gitCommandList = new List<string>();
            gitCommandList.Clear();
            // Can also directly be used with a tagname
            // git archive --format=zip -o /Users/jthijs/Downloads/git.zip 1.17.4
            gitCommandList.Add("archive -o \"" + zipPathOs + "\" " + commitHash);
            string? gitResult = GitCommand(gitCommandList, reproPathOs, returnType, false);
            if (gitResult == null)
            {
                HandleGitError("Could not export the archive from the GIT repository.", "reproPathOs: " + reproPathOs + " - more information in the Taxxor log files", returnType, stopProcessingOnError);
                return "ERROR: no response from GIT process";
            }

            // Expand the complete ZIP to the output directory
            try
            {
                ZipFile.ExtractToDirectory(zipPathOs, outputDirectoryPathOs);
            }
            catch (Exception ex)
            {
                // Cleanup the extraction directory
                try
                {
                    DelTree(outputDirectoryPathOs);
                }
                catch (Exception exDelTree)
                {
                    // There was an error deleting the folder tree
                    HandleGitError("There was an issue removing the ZIP extraction folder", exDelTree.ToString(), returnType, stopProcessingOnError);
                }

                // There was a problem extracting the files from the ZIP file
                WriteErrorMessageToConsole("There was an error extracting the ZIP file", ex.ToString());

                // Store the error in the response message, but continue the script
                responseMessage = "ERROR: Could not extract the ZIP file";
            }

            // Remove the temporary zip file
            try
            {
                File.Delete(zipPathOs);
            }
            catch (Exception ex)
            {
                WriteErrorMessageToConsole("There was an error removing the temporary ZIP file", ex.ToString());
            }

            // Return the response
            return responseMessage;
        }




        /// <summary>
        /// Retrieves the full OS path of a git repository based on an ID from the configuration
        /// </summary>
        /// <param name="reproId">Repository or editor ID</param>
        /// <returns></returns>
        public static string? RetrieveGitRepositoryLocation(string reproId)
        {
            string? pathOs = null;

            //XmlNode nodeLocation=xmlApp

            // 1) Search for non-editor repositories
            XmlNode? nodeLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/repositories/*/repro[@id='" + reproId + "']/location");
            if (nodeLocation == null)
            {
                // 2) Search for non-editor submodules
                nodeLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/repositories/*/repro/submodules/location[@id='" + reproId + "']");
                // 3) Search for an editor
                nodeLocation ??= xmlApplicationConfiguration.SelectSingleNode("/configuration/editors/editor[@id='" + reproId + "']/path");
            }

            // Calculate the full OS type path
            if (nodeLocation != null) pathOs = CalculateFullPathOs(nodeLocation);


            return pathOs;
        }

        /// <summary>
        /// Renders the git error message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="debugInfo">Debug info.</param>
        /// <param name="returnType">Return type.</param>
        /// <param name="stopProcessingOnError">If set to <c>true</c> stop processing on error.</param>
        private static void HandleGitError(string message, string debugInfo, ReturnTypeEnum returnType, bool stopProcessingOnError)
        {
            if (stopProcessingOnError)
            {
                HandleError(new RequestVariables { returnType = returnType }, message, debugInfo);
            }
            else
            {
                WriteErrorMessageToConsole(message, debugInfo);
            }
        }

        /// <summary>
        /// Retrieves the latest GIT tag from a GIT repository
        /// </summary>
        /// <param name="repositoryPath"></param>
        /// <returns></returns>
        public static string? RetrieveLatestGitTag(string repositoryPath)
        {
            using (var repo = new Repository(repositoryPath))
            {
                var tag = repo.Tags.OrderByDescending(t => t.Target.Peel<Commit>().Author.When).FirstOrDefault();
                return tag?.FriendlyName;
            }
        }

        /// <summary>
        /// Reytrieves the current GIT branch namer
        /// </summary>
        /// <param name="repositoryPath"></param>
        /// <returns></returns>
        public static string RetrieveCurrentBranchName(string repositoryPath)
        {
            using (var repo = new LibGit2Sharp.Repository(repositoryPath))
            {
                return repo.Head.FriendlyName;
            }
        }


        /// <summary>
        /// Adds a version attribute to the XmlNode containing the version which is extracted from the latest GIT tag
        /// </summary>
        /// <param name="nodeToMark">Node to mark.</param>
        /// <param name="repositoryPathOs">Repository path os.</param>
        /// <param name="logIssues">If set to <c>true</c> log issues.</param
        private static void _markGitTagContentInNode(XmlNode nodeToMark, string repositoryPathOs, bool logIssues = true)
        {
            var gitRepositoryType = RetrieveGitRepositoryType(repositoryPathOs);
            if (gitRepositoryType == GitTypeEnum.None)
            {
                if (logIssues) appLogger.LogWarning($"_markGitTagContentInNode() not a GIT repository {repositoryPathOs}/.git (stack-trace: {GetStackTrace()})");
            }
            else
            {
                var latestTag = RetrieveLatestGitTag(repositoryPathOs)?.Trim() ?? "";
                if (!RegExpTest(@"^(v)?(\d+\.\d+\.\d+)$", latestTag))
                {
                    appLogger.LogWarning($"Unable to retrieve GIT tag for path {repositoryPathOs} - initial GIT command returned {latestTag}");
                    // var latestTagHash = GitCommand("git rev-list --tags --max-count=1", repositoryPathOs).Trim();
                    // latestTag = GitCommand($"git describe --tags {latestTagHash}", repositoryPathOs).Trim();
                    // if (!RegExpTest(@"^(v)?(\d+\.\d+\.\d+)$", latestTag))
                    // {
                    //     latestTag = GitCommand($"git tag | tail -1", repositoryPathOs).Trim();
                    // }
                }

                if (!string.IsNullOrEmpty(latestTag))
                {
                    SetAttribute(nodeToMark, "version", latestTag);
                }

                // Also retrieve the branch that we are on -> git branch --show-current
                var currentBranchName = RetrieveCurrentBranchName(repositoryPathOs).Trim();
                if (!string.IsNullOrEmpty(currentBranchName)) SetAttribute(nodeToMark, "branch", currentBranchName);
            }
        }

        /// <summary>
        /// Normalizes a path to approot so that it can be used without a ProjectVariables instance
        /// </summary>
        /// <returns>The path to approot.</returns>
        /// <param name="relativePath">Relative path.</param>
        /// <param name="pathType">Path type.</param>
        private static string? _calculateFullPathOsWithoutProjectVariables(string relativePath, string pathType)
        {
            var relativeTo = pathType switch
            {
                "solutionroot" => "solutionroot",
                "dataroot" => "dataroot",
                "cmsdatarootbase" => "dataroot",
                "cmscontentroot" => "dataroot",
                _ => "approot"
            };
            var normalizedPath = pathType switch
            {
                "cmsroot" => (xmlApplicationConfiguration.SelectSingleNode("/configuration/general/locations/location[@id='webserverroot']").InnerText + relativePath),
                "cmscustomfrontendroot" => (xmlApplicationConfiguration.SelectSingleNode("/configuration/general/locations/location[@id='webserverroot']").InnerText + xmlApplicationConfiguration.SelectSingleNode("/configuration/general/locations/location[@id='cmscustomfrontendroot']").InnerText + relativePath),
                "cmstemplatesroot" => $"{xmlApplicationConfiguration.SelectSingleNode("/configuration/general/locations/location[@id='cmstemplatesroot']").InnerText}{relativePath}",
                "cmsdatarootbase" => $"{xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project/location[@id='reportdataroot']").InnerText}{relativePath}",
                "cmscontentroot" => $"{xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project/location[@id='reportdataroot']").InnerText}/content{relativePath}",
                _ => relativePath,
            };
            return CalculateFullPathOs(normalizedPath, relativeTo, null, null, true);
        }


    }
}