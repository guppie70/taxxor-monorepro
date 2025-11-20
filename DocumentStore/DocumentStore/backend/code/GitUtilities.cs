using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    /// <summary>
    /// Utility functions to work with GIT repositories in the context of the Taxxor Data Store
    /// </summary>

    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Commit the filing data (XHTML + hierarchy files) part of the Taxxor filing repository
        /// </summary>
        /// <param name="message"></param>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public static bool CommitFilingData(string message, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            return GitCommit(message, projectVars.cmsDataRootBasePathOs, returnType, stopProcessingOnError);
        }

        /// <summary>
        /// Commits the project assets (images, downloads, rendered HTML)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static bool CommitFilingAssets(string message, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            return GitCommit(message, projectVars.cmsContentRootPathOs, returnType, stopProcessingOnError);
        }

        /// <summary>
        /// Commits content for the data part in the Taxxor datawarehouse
        /// </summary>
        /// <param name="message"></param>
        /// <param name="returnType"></param>
        /// <param name="stopProcessingOnError"></param>
        /// <returns></returns>
        public static bool CommitDataContent(string message, ReturnTypeEnum returnType = ReturnTypeEnum.Txt, bool stopProcessingOnError = false)
        {
            return GitCommit(message, dataRootPathOs, returnType, stopProcessingOnError);
        }

        // TODO: Consider using below for GIT commits for project data and project content
        // var gitRootPathOs = RetrieveGitRepositoryLocation("project-data");
        //     gitRootPathOs = RetrieveGitRepositoryLocation("project-content");

        /// <summary>
        /// Retrieves the message of a commit or a tag including the files that the commit contains
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GitRetrieveFullMessage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Get posted data
            var source = context.Request.RetrievePostedValue("source", RegexEnum.None, true, reqVars.returnType); // filingdata || filingassets
            var hashOrTag = context.Request.RetrievePostedValue("hashortag", RegexEnum.Default, true, reqVars.returnType);
            var component = context.Request.RetrievePostedValue("component", RegexEnum.None, true, reqVars.returnType);


            // Test if we need to make a remote request for the git pull or not
            TaxxorReturnMessage? gitMessageRetrieveResult = null;
            if (applicationId == "taxxoreditor" && component == "documentstore")
            {
                //
                // => Proxy this information through to the data service
                //
                var dataToPost = new Dictionary<string, string?>();
                dataToPost.Add("pid", projectVars.projectId);
                dataToPost.Add("source", source);
                dataToPost.Add("hashortag", hashOrTag);
                dataToPost.Add("component", component);
                XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitcommitinfo", dataToPost, debugRoutine);

                // Directly convert the received XML from the remote to a Taxxor Return Message and proceed
                gitMessageRetrieveResult = new TaxxorReturnMessage(responseXml);
            }
            else
            {
                //
                // => Generate the complete commit message by combining what was stores in the message with the files contained in the commit
                //
                gitMessageRetrieveResult = new TaxxorReturnMessage();
                try
                {
                    var repositoryPathOs = (source == "filingassets") ? projectVars.cmsContentRootPathOs : projectVars.cmsDataRootBasePathOs;

                    var commitHash = hashOrTag;
                    var baseMessage = "";
                    if (!RegExpTest(RegexEnum.Hash.Value, hashOrTag))
                    {
                        // This is a tag - retrieve the hash associated with it
                        commitHash = GitRetrieveHashOfTag(hashOrTag, repositoryPathOs);
                        baseMessage = GitRetrieveMessageOfTag(hashOrTag, repositoryPathOs);
                    }
                    else
                    {
                        baseMessage = GitRetrieveMessageOfHash(hashOrTag, repositoryPathOs);
                    }

                    var xmlCommitMessage = new XmlDocument();

                    xmlCommitMessage.LoadXml(baseMessage);
                    xmlCommitMessage.DocumentElement.SetAttribute("hash", commitHash);

                    if (hashOrTag.StartsWith('v')) xmlCommitMessage.DocumentElement.SetAttribute("tag", hashOrTag);


                    var nodeFiles = xmlCommitMessage.CreateElement("files");
                    var filesInCommit = GitRetrieveFilesFirstCommit(commitHash, repositoryPathOs);
                    nodeFiles.InnerText = string.Join(",", filesInCommit);
                    xmlCommitMessage.DocumentElement.AppendChild(nodeFiles);


                    gitMessageRetrieveResult.Success = true;
                    gitMessageRetrieveResult.Message = "Successfully retrieved commit message";
                    gitMessageRetrieveResult.XmlPayload = xmlCommitMessage;
                }
                catch (Exception ex)
                {
                    gitMessageRetrieveResult.Success = false;
                    gitMessageRetrieveResult.Message = "There was an error retrieving the full commit message";
                    gitMessageRetrieveResult.DebugInfo = $"error: {ex}";
                }
            }




            if (gitMessageRetrieveResult.Success)
            {
                await response.OK(gitMessageRetrieveResult.ToXml(), reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars.returnType, gitMessageRetrieveResult.ToXml());
            }
        }

        /// <summary>
        /// Returns a list of files that were part of a GIT commit
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task GitRetrieveFileListFromCommit(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Get posted data
            var commitHash = request.RetrievePostedValue("commithash", RegexEnum.HashOrTag, true, reqVars.returnType);
            var locationId = request.RetrievePostedValue("locationid", RegexEnum.None, true, reqVars.returnType);

            var baseDebugInfo = $"commitHash: {commitHash}, locationId: {locationId}";

            var cmsDataRootPathOs = (locationId != "reportdataroot") ? projectVars.cmsContentRootPathOs : projectVars.cmsDataRootBasePathOs;

            var commitContents = GitRetrieveFileListFromCommit(cmsDataRootPathOs, commitHash);

            await response.OK(GenerateSuccessXml($"Successfully retrieved file list", $"cmsDataRootPathOs: {cmsDataRootPathOs}, {baseDebugInfo}", string.Join(",", commitContents)));

        }


    }
}