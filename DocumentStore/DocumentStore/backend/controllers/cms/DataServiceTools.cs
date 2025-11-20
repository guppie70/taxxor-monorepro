using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Custom attribute that we can use to decorate data Document Store Tools methods
        /// </summary>
        [AttributeUsage(AttributeTargets.Method)]
        public class DataServiceToolAttribute : Attribute
        {
            public string Id { get; }
            public string Name { get; set; }
            public string Description { get; set; }

            public DataServiceToolAttribute(string id)
            {
                Id = id;
            }
        }

        [GeneratedRegex("^\\/dataserviceassets\\/(.*?)(\\/.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-NL")]
        private static partial Regex ReDataServiceAssets();


        /// <summary>
        /// Lists the data service tools which are available
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task DataServiceToolsList(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Construct a response message for the client
            var xmlDataServiceTools = new XmlDocument();
            xmlDataServiceTools.LoadXml("<dataservicetools/>");
            var dataServiceMethodsListSorted = DataServiceMethodsList.OrderBy(o => o.Name);
            foreach (var toolMethod in dataServiceMethodsListSorted)
            {
                var nodeTool = xmlDataServiceTools.CreateElement("tool");

                var nodeToolMethodId = xmlDataServiceTools.CreateElement("id");
                nodeToolMethodId.InnerText = toolMethod.Id;
                nodeTool.AppendChild(nodeToolMethodId);

                var nodeToolMethodName = xmlDataServiceTools.CreateElement("name");
                nodeToolMethodName.InnerText = toolMethod.Name;
                nodeTool.AppendChild(nodeToolMethodName);

                var nodeToolMethodDescription = xmlDataServiceTools.CreateElement("description");
                nodeToolMethodDescription.InnerText = toolMethod.Description;
                nodeTool.AppendChild(nodeToolMethodDescription);

                xmlDataServiceTools.DocumentElement.AppendChild(nodeTool);
            }

            // Stick the file content in the message field of the success xml and the file path into the debuginfo node
            await response.OK(GenerateSuccessXml("Successfully retrieved Document Store Tools method list", "", HttpUtility.HtmlEncode(xmlDataServiceTools.OuterXml)), ReturnTypeEnum.Xml, true);
        }


        /// <summary>
        /// Retrieves the source XHTML data for the PDF Service to use as the source material
        /// </summary>
        /// <returns>The user data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task ExecuteDataServiceTool(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted variables
            var projectIdsPosted = request.RetrievePostedValue("pids", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var toolId = request.RetrievePostedValue("toolid", RegexEnum.None, true, ReturnTypeEnum.Xml);

            var baseDebugInfo = $"toolId: {toolId}, projectIdsPosted: '{projectIdsPosted}'";

            // HandleError(ReturnTypeEnum.Json, "Thrown on purpose in Taxxor Document Store", $"projectIdsPosted: {projectIdsPosted.ToString()}, toolId: {toolId}, reqVars.returnType: {reqVars.returnType}, stack-trace: {GetStackTrace()}");

            if (projectVars.currentUser.IsAuthenticated)
            {

                var xmlToReturn = new XmlDocument();
                xmlToReturn.LoadXml("<dataservicetoolresults/>");


                string[] projectIds = projectIdsPosted.Split(',');

                foreach (var projectId in projectIds)
                {
                    try
                    {
                        // Determine the tool to run
                        if (DataServiceToolMethods.TryGetValue(toolId, out DataServiceToolDelegate method))
                        {
                            // Invoke the transformation method
                            var toolResult = await method(projectId);

                            var nodeProject = xmlToReturn.CreateElement("project");
                            SetAttribute(nodeProject, "id", projectId);
                            SetAttribute(nodeProject, "success", toolResult.Success.ToString().ToLower());

                            var nodeMessage = xmlToReturn.CreateElement("message");
                            nodeMessage.InnerText = toolResult.Message;
                            nodeProject.AppendChild(nodeMessage);

                            var nodeDebugInfo = xmlToReturn.CreateElement("debuginfo");
                            nodeDebugInfo.InnerText = toolResult.DebugInfo;
                            nodeProject.AppendChild(nodeDebugInfo);

                            xmlToReturn.DocumentElement.AppendChild(nodeProject);
                        }
                        else
                        {
                            appLogger.LogError("Could not map service tool id: {ToolId} to a method that we can invoke...", toolId);
                            HandleError(ReturnTypeEnum.Xml, "Unknown data service tool method", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");
                        }


                    }
                    catch (Exception ex)
                    {
                        HandleError(ReturnTypeEnum.Xml, "There was an error executing the data service tool", $"error: {ex}, {baseDebugInfo}, stack-trace: {GetStackTrace()}");
                    }
                }
                // HandleError(reqVars, "Thrown on purpose Taxxor Document Store - RetrieveFactIds()", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}");


                // Stick the file content in the message field of the success xml and the file path into the debuginfo node
                await response.OK(GenerateSuccessXml(HttpUtility.HtmlEncode(xmlToReturn.OuterXml), baseDebugInfo), ReturnTypeEnum.Xml, true);

            }
            else
            {
                HandleError(ReturnTypeEnum.Xml, "Not authenticated", $"{baseDebugInfo}, stack-trace: {GetStackTrace()}", 403);
            }
        }


        /// <summary>
        /// Retrieves some basic statistics about the project
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [DataServiceTool("projectinformation", Name = "Utility: Retrieve basic project information", Description = "Retrieves basic information about the projects in scope by inspecting the project folder contents")]
        public async static Task<TaxxorReturnMessage> RetrieveProjectInformation(dynamic projectId)
        {
            await DummyAwaiter();

            // # - delete all the objects w/o references
            // # git prune --progress  
            // # - aggressively collect garbage; may take a lot of time on large repos               
            // # git gc --aggressive

            // Calculate the project size

            var nodeProjectRoot = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects");
            var nodeCurrentProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{projectId}']");
            var sourceDataFolderPathOs = CalculateFullPathOs(nodeCurrentProject.SelectSingleNode("location[@id='reportdataroot']"));

            DirectoryInfo dir = new(sourceDataFolderPathOs);
            FileInfo[] files = dir.GetFiles("*.*", SearchOption.AllDirectories);
            long totalByteSize = files.Sum(f => f.Length);

            string readableFolderSize = CalculateFileSize(totalByteSize);

            return new TaxxorReturnMessage(true, $"Folder size: {readableFolderSize}", $"parameter: {projectId.ToString()}, sourceDataFolderPathOs: {sourceDataFolderPathOs}");
        }


        /// <summary>
        /// Bulk change paths to assets (images, svg's, etc) across multiple projects in one go
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [DataServiceTool("assetpathchanger", Name = "Transform: Change asset paths", Description = "Bulk change paths to assets (images, svg's, etc) across multiple projects in one go. Makes the paths project independent injecting a placeholder in the path to make sure that images always refer to the current project images library.")]
        public async static Task<TaxxorReturnMessage> ToolChangeAssetsPaths(dynamic projectId)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Change the existing project variables object to reflect the project context of this method
            //
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
            if (!renderProjectVarsResult) appLogger.LogError("Unable to render project variables from a project ID");
            FillCorePathsInProjectVariables(ref projectVars);

            // - Use a fake section id in order to hide the replacement warning
            projectVars.did = "foobar";
            SetProjectVariables(context, projectVars);

            //
            // => Global variables for this section
            //

            var nodeProjectRoot = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects");
            var nodeCurrentProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{projectId}']");
            var cmsDataRootPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId);
            //var sourceDataFolderPathOs = projectVars.cmsDataRootPathOs;

            // Retrieve all the hierarchies defined for this project
            var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectVars.projectId, false);

            Regex reDataServiceAssetsPathParser = ReDataServiceAssets();
            var adjustedDataReferenceFiles = new List<string>();

            if (debugRoutine)
            {
                Console.WriteLine("--------------------------------------------");
                Console.WriteLine($"- sourceDataFolderPathOs: {cmsDataRootPathOs}");
                Console.WriteLine("--------------------------------------------");
            }


            //
            // Loop through the data files
            //
            // var replacedOne = false;
            if (Directory.Exists(cmsDataRootPathOs))
            {


                var searchPattern = "*.xml";
                try
                {
                    var allProjectDataFiles = Directory.EnumerateFiles(cmsDataRootPathOs, searchPattern, SearchOption.AllDirectories);


                    foreach (string filePathOs in allProjectDataFiles)
                    {
                        var fileReference = filePathOs.Replace(cmsDataRootPathOs, "").Replace("/", "");

                        try
                        {
                            var xmlSectionData = new XmlDocument();
                            xmlSectionData.Load(filePathOs);

                            var replacedPaths = 0;

                            // Detect the type of XML document we are processing
                            var nodeListContent = xmlSectionData.SelectNodes("/data/content");
                            foreach (XmlNode nodeContent in nodeListContent)
                            {
                                var detectedLanguage = nodeContent?.GetAttribute("lang") ?? "";

                                //
                                // => Replace the paths
                                //
                                var basePath = "/dataserviceassets";

                                var nodeListVisuals = nodeContent.SelectNodes($"*//img[starts-with(@src, '{basePath}')]");
                                foreach (XmlNode nodeVisual in nodeListVisuals)
                                {
                                    var originalSrc = nodeVisual.GetAttribute("src");
                                    if (!originalSrc.Contains("{projectid}"))
                                    {
                                        Match regexMatches = reDataServiceAssetsPathParser.Match(originalSrc);
                                        if (regexMatches.Success)
                                        {
                                            var currentProjectId = regexMatches.Groups[1].Value;
                                            var assetPathToRetrieve = regexMatches.Groups[2].Value;
                                            nodeVisual.SetAttribute("src", $"{basePath}/{{projectid}}{assetPathToRetrieve}");
                                            replacedPaths++;
                                        }
                                    }
                                }

                                var nodeListDrawings = nodeContent.SelectNodes($"*//object[starts-with(@data, '{basePath}')]");
                                foreach (XmlNode nodeDrawing in nodeListDrawings)
                                {
                                    var originalDataSource = nodeDrawing.GetAttribute("data");
                                    if (!originalDataSource.Contains("{projectid}"))
                                    {
                                        Match regexMatches = reDataServiceAssetsPathParser.Match(originalDataSource);
                                        if (regexMatches.Success)
                                        {
                                            var currentProjectId = regexMatches.Groups[1].Value;
                                            var assetPathToRetrieve = regexMatches.Groups[2].Value;
                                            nodeDrawing.SetAttribute("data", $"{basePath}/{{projectid}}{assetPathToRetrieve}");
                                            replacedPaths++;
                                        }
                                    }
                                }

                                var nodeListImages = nodeContent.SelectNodes($"*//img[contains(@src, '/custom/{TaxxorClientId}/groupcontrol/')]");
                                if (nodeListImages.Count > 0)
                                {
                                    // if (debugRoutine && replacedOne) continue;

                                    Console.WriteLine($"- {fileReference} is section data file with one or more images");
                                    foreach (XmlNode nodeImage in nodeListImages)
                                    {
                                        nodeImage.SetAttribute("src", nodeImage.GetAttribute("src").Replace($"/custom/{TaxxorClientId}/groupcontrol/", $"/outputchannels/{TaxxorClientId}/pdf/"));
                                    }

                                    replacedPaths++;
                                }

                                //
                                // => Commit the changes to the file
                                //
                                if (replacedPaths > 0)
                                {
                                    // Save the filing data
                                    xmlSectionData.Save(filePathOs);

                                    // Retrieve the information to use in the commit message
                                    SiteStructureItem commitInformation = RetrieveCommitInformation(projectVars.projectId, xmlSectionData, filePathOs, xmlHierarchyOverview, XmlCmsContentMetadata);

                                    // Construct a commit message
                                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "transform";
                                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = commitInformation.Linkname;
                                    xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", detectedLanguage);
                                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = commitInformation.Id;
                                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                                    // Commit the result in the GIT repository
                                    CommitFilingData(message, ReturnTypeEnum.Xml, false);

                                    adjustedDataReferenceFiles.Add(fileReference);
                                }

                            }

                            // Add the data reference to the changed datareferences list
                            if (replacedPaths > 0 && !adjustedDataReferenceFiles.Contains(fileReference))
                            {
                                adjustedDataReferenceFiles.Add(fileReference);
                            }
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, "Could not load or save section xml data. filePathOs: {filePathOs}, error: {ex}", filePathOs, ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "There was a problem looping through the data files. error: {ex}", ex);
                }
            }
            else
            {
                appLogger.LogError($"Could not find the root folder of the data files. cmsDataRootPathOs: {cmsDataRootPathOs}, stack-trace: ${GetStackTrace()}");
            }


            // Return the success message
            var projectName = nodeCurrentProject.SelectSingleNode("name")?.InnerText ?? projectId;
            var returnMessage = (adjustedDataReferenceFiles.Count > 0) ? $"Successfully changed paths in {adjustedDataReferenceFiles.Count} content data files in project '{projectName}'" : $"No need to change paths in project '{projectName}'";
            var debugInfo = (adjustedDataReferenceFiles.Count > 0) ? $"dataFilesChanged: {string.Join(", ", adjustedDataReferenceFiles.ToArray())}, " : "";
            return new TaxxorReturnMessage(true, returnMessage, $"{debugInfo}sourceDataFolderPathOs: {cmsDataRootPathOs}");
        }


        /// <summary>
        /// Bulk transform all XML source data files across multiple projects in one go
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [DataServiceTool("bulktransformdatafiles", Name = "Transform: Bulk transform all XML source data", Description = "Bulk transform all XML source data files across multiple projects in one go. Currently configured to execute the <code>CleanupDataReference()</code> routine removes unwanted attributes, unnecessary nested spans and empty elements.")]
        public async static Task<TaxxorReturnMessage> ToolBulkTransformAllDataFiles(dynamic projectId)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Change the existing project variables object to reflect the project context of this method
            //
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
            if (!renderProjectVarsResult) appLogger.LogError("Unable to render project variables from a project ID");
            FillCorePathsInProjectVariables(ref projectVars);

            // - Use a fake section id in order to hide the replacement warning
            projectVars.did = "foobar";
            SetProjectVariables(context, projectVars);

            //
            // => Global variables for this section
            //

            var nodeProjectRoot = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects");
            var nodeCurrentProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{projectId}']");
            var cmsDataRootPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId);
            //var sourceDataFolderPathOs = projectVars.cmsDataRootPathOs;

            // Retrieve all the hierarchies defined for this project
            var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectVars.projectId, false);

            var adjustedDataReferenceFiles = new List<string>();

            if (debugRoutine)
            {
                Console.WriteLine("--------------------------------------------");
                Console.WriteLine($"- sourceDataFolderPathOs: {cmsDataRootPathOs}");
                Console.WriteLine("--------------------------------------------");
            }


            //
            // Loop through the data files
            //
            // var replacedOne = false;
            if (Directory.Exists(cmsDataRootPathOs))
            {


                var searchPattern = "*.xml";
                try
                {
                    var allDataFiles = Directory.EnumerateFiles(cmsDataRootPathOs, searchPattern, SearchOption.AllDirectories);


                    foreach (string filePathOs in allDataFiles)
                    {
                        var fileReference = filePathOs.Replace(cmsDataRootPathOs, "").Replace("/", "");

                        try
                        {
                            var xmlSectionData = new XmlDocument();
                            xmlSectionData.Load(filePathOs);

                            var adjustments = 0;
                            var articleAdjustments = 0;

                            //
                            // => Detect the type of XML document we are processing
                            //
                            var nodeListContent = xmlSectionData.SelectNodes("/data/content");
                            var isProjectDataFile = (nodeListContent.Count > 0);

                            if (isProjectDataFile)
                            {

                                //
                                // => Transformations for the complete document
                                //
                                var xmlTranslated = CleanupDataReference(xmlSectionData, projectVars);

                                if (GenerateXmlHash(xmlSectionData) != GenerateXmlHash(xmlTranslated))
                                {
                                    // Something has changed
                                    xmlSectionData.ReplaceContent(xmlTranslated, true);

                                    adjustments++;
                                }


                                //
                                // => Commit the changes to the file
                                //
                                if (adjustments > 0)
                                {
                                    // Save the filing data
                                    xmlSectionData.Save(filePathOs);

                                    // Retrieve the information to use in the commit message
                                    SiteStructureItem commitInformation = RetrieveCommitInformation(projectVars.projectId, xmlSectionData, fileReference, xmlHierarchyOverview, XmlCmsContentMetadata);

                                    // Construct a commit message
                                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "transform";
                                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = commitInformation.Linkname;
                                    xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", projectVars.outputChannelDefaultLanguage);
                                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = commitInformation.Id;
                                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                                    // Commit the result in the GIT repository
                                    CommitFilingData(message, ReturnTypeEnum.Xml, false);
                                }

                                //
                                // => Transformations for each of the articles within the document
                                //

                                foreach (XmlNode nodeContent in nodeListContent)
                                {
                                    articleAdjustments = 0;

                                    var detectedLanguage = nodeContent?.GetAttribute("lang") ?? "";

                                    //
                                    // => Make the adjustments
                                    //
                                    var nodeArticle = nodeContent.SelectSingleNode("article");
                                    if (nodeArticle != null)
                                    {

                                        // - Add a default article type
                                        var articleType = nodeArticle.GetAttribute("data-articletype");
                                        if (string.IsNullOrEmpty(articleType))
                                        {
                                            nodeArticle.SetAttribute("data-articletype", "regular");
                                            articleAdjustments++;
                                        }
                                    }

                                    //
                                    // => Commit the changes to the file
                                    //
                                    if (articleAdjustments > 0)
                                    {
                                        // Save the filing data
                                        xmlSectionData.Save(filePathOs);

                                        // Retrieve the information to use in the commit message
                                        SiteStructureItem commitInformation = RetrieveCommitInformation(projectVars.projectId, xmlSectionData, fileReference, xmlHierarchyOverview, XmlCmsContentMetadata);

                                        // Construct a commit message
                                        XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                                        xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "transform";
                                        xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = commitInformation.Linkname;
                                        xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", detectedLanguage);
                                        xmlCommitMessage.SelectSingleNode("/root/id").InnerText = commitInformation.Id;
                                        var message = xmlCommitMessage.DocumentElement.InnerXml;

                                        // Commit the result in the GIT repository
                                        CommitFilingData(message, ReturnTypeEnum.Xml, false);
                                    }
                                }
                            }




                            // Add the data reference to the changed datareferences list
                            if (adjustments > 0 || articleAdjustments > 0)
                            {
                                if (!adjustedDataReferenceFiles.Contains(fileReference)) adjustedDataReferenceFiles.Add(fileReference);
                            }
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Could not load or save section xml data. filePathOs: {filePathOs}, error: {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"There was a problem looping through the data files. error: {ex}");
                }
            }
            else
            {
                appLogger.LogError($"Could not find the root folder of the data files. cmsDataRootPathOs: {cmsDataRootPathOs}, stack-trace: ${GetStackTrace()}");
            }


            // Return the success message
            var projectName = nodeCurrentProject.SelectSingleNode("name")?.InnerText ?? projectId;
            var returnMessage = (adjustedDataReferenceFiles.Count > 0) ? $"Successfully bulk transformed in {adjustedDataReferenceFiles.Count} content data files in project '{projectName}'" : $"No need to bulk transform in project '{projectName}'";
            var debugInfo = (adjustedDataReferenceFiles.Count > 0) ? $"dataFilesChanged: {string.Join(", ", adjustedDataReferenceFiles.ToArray())}, " : "";
            return new TaxxorReturnMessage(true, returnMessage, $"{debugInfo}sourceDataFolderPathOs: {cmsDataRootPathOs}");
        }


        [DataServiceTool("renderstructureddataelementlist", Name = "Utility: SDE fact-id list", Description = "Renders a complete list containing all the fact-id's used by the Structured Data Elements in (a) project(s)")]
        public async static Task<TaxxorReturnMessage> RenderStructuredDataElementListProject(dynamic projectId)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Change the existing project variables object to reflect the project context of this method
            //
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
            if (!renderProjectVarsResult) appLogger.LogError("Unable to render project variables from a project ID");
            FillCorePathsInProjectVariables(ref projectVars);

            // - Use a fake section id in order to hide the replacement warning
            projectVars.did = "foobar";
            SetProjectVariables(context, projectVars);

            //
            // => Global variables for this section
            //

            var nodeProjectRoot = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects");
            var nodeCurrentProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{projectId}']");
            var cmsDataRootPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId);
            //var sourceDataFolderPathOs = projectVars.cmsDataRootPathOs;

            var factIds = new List<string>();



            if (debugRoutine)
            {
                Console.WriteLine("--------------------------------------------");
                Console.WriteLine($"- sourceDataFolderPathOs: {cmsDataRootPathOs}");
                Console.WriteLine("--------------------------------------------");
            }


            //
            // Loop through the data files
            //
            // var replacedOne = false;
            if (Directory.Exists(cmsDataRootPathOs))
            {


                var searchPattern = "*.xml";
                try
                {
                    var allDataFiles = Directory.EnumerateFiles(cmsDataRootPathOs, searchPattern, SearchOption.AllDirectories);


                    foreach (string filePathOs in allDataFiles)
                    {
                        var fileReference = filePathOs.Replace(cmsDataRootPathOs, "").Replace("/", "");

                        try
                        {
                            var xmlSectionData = new XmlDocument();
                            xmlSectionData.Load(filePathOs);

                            //
                            // => Detect the type of XML document we are processing
                            //
                            var nodeListContent = xmlSectionData.SelectNodes("/data/content");
                            var isProjectDataFile = (nodeListContent.Count > 0);

                            if (isProjectDataFile)
                            {

                                //
                                // => Find the fact id's
                                //
                                var xmlFactIds = RenderStructuredDataElementList(xmlSectionData);
                                var nodeListFacts = xmlFactIds.SelectNodes("/facts/fact");

                                foreach (XmlNode nodeFact in nodeListFacts)
                                {
                                    var factId = nodeFact.GetAttribute("id");
                                    if (!string.IsNullOrEmpty(factId))
                                    {
                                        if (!factIds.Contains(factId)) factIds.Add(factId);
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Detected fact without an ID. projectId: {projectVars.projectId}, xmlFileName: {Path.GetFileName(filePathOs)}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError($"Could not load or save section xml data. filePathOs: {filePathOs}, error: {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError($"There was a problem looping through the data files. error: {ex}");
                }
            }
            else
            {
                appLogger.LogError($"Could not find the root folder of the data files. cmsDataRootPathOs: {cmsDataRootPathOs}, stack-trace: ${GetStackTrace()}");
            }


            // Return the success message
            var projectName = nodeCurrentProject.SelectSingleNode("name")?.InnerText ?? projectId;
            return new TaxxorReturnMessage(true, $"Found {factIds.Count} unique SDE id's in project {projectName}", $"{string.Join(",", factIds)}");
        }


        /// <summary>
        /// Loops through the selected projects and transforms/adjusts all the website source data references in one run
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [DataServiceTool("bulktransformwebsitedatareferences", Name = "Transform: Bulk transform website data references (source XML data)", Description = "Loops through the selected projects and transforms/adjusts all the website source data references in one run")]
        public async static Task<TaxxorReturnMessage> ToolBulkTransformWebsiteDataReferences(dynamic projectId)
        {

            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Change the existing project variables object to reflect the project context of this method
            //
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
            if (!renderProjectVarsResult) appLogger.LogError("Unable to render project variables from a project ID");
            FillCorePathsInProjectVariables(ref projectVars);

            // - Use a fake section id in order to hide the replacement warning
            projectVars.did = "foobar";
            SetProjectVariables(context, projectVars);

            //
            // => Global variables for this section
            //

            var nodeProjectRoot = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects");
            var nodeCurrentProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{projectId}']");
            var cmsDataRootPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId);
            //var sourceDataFolderPathOs = projectVars.cmsDataRootPathOs;

            //
            // => Load the data configuration
            //
            var dataConfigurationPathOs = CalculateFullPathOs("/configuration/configuration_system//config/location[@id='data_configuration']");
            var xmlDataConfiguration = new XmlDocument();
            xmlDataConfiguration.Load(dataConfigurationPathOs);

            //
            // => Find the website hierarchies for this project
            //
            var hierarchyIds = new List<string>();
            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);


            var nodeListWebsiteVariants = xmlDataConfiguration.SelectNodes($"/configuration/editors/editor[@id='{editorId}']/output_channels/output_channel[@type='website']/variants/variant");
            foreach (XmlNode nodeVariant in nodeListWebsiteVariants)
            {
                var hierarchyId = nodeVariant.GetAttribute("metadata-id-ref");
                if (!string.IsNullOrEmpty(hierarchyId) && !hierarchyIds.Contains(hierarchyId)) hierarchyIds.Add(hierarchyId);
            }
            if (hierarchyIds.Count == 0) return new TaxxorReturnMessage(true, "WARNING: Could not find any website hierarchies for this project", $"projectId: {projectVars.projectId}");

            //
            // => Find all the datareferences in the website hierarchies
            //
            var websiteDataReferences = new Dictionary<string, string>();
            var websiteHierarchies = new Dictionary<string, XmlDocument>();
            foreach (string hierarchyId in hierarchyIds)
            {

                // Retrieve the output channel hierarchy
                XmlDocument xmlWebsiteHierarchy = new();
                var pathOs = CalculateHierarchyPathOs(projectVars.projectId, hierarchyId, false);

                if (pathOs != null)
                {
                    try
                    {
                        if (File.Exists(pathOs))
                        {
                            xmlWebsiteHierarchy.Load(pathOs);

                            var nodeListItems = xmlWebsiteHierarchy.SelectNodes("/items/structured//item[@data-ref]");
                            foreach (XmlNode nodeItem in nodeListItems)
                            {
                                var dataReference = nodeItem.GetAttribute("data-ref");
                                var itemId = nodeItem.GetAttribute("id");
                                if (!string.IsNullOrEmpty(dataReference) && !string.IsNullOrEmpty(itemId) && !websiteDataReferences.ContainsKey(dataReference))
                                {
                                    websiteDataReferences.Add(dataReference, itemId);
                                    var xmlHierarchy = new XmlDocument();
                                    xmlHierarchy.ReplaceContent(xmlWebsiteHierarchy);
                                    websiteHierarchies.Add(dataReference, xmlHierarchy);
                                }
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Could not load hierarchy in ToolBulkTransformWebsiteDataReferences(). pathOs: {pathOs}, projectId: {projectVars.projectId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"There was an error processing the hierarchy of project: {projectVars.projectId}");
                    }
                }
            }
            if (websiteDataReferences.Count == 0) return new TaxxorReturnMessage(true, "WARNING: Could not find any website datareferences for this project", $"projectId: {projectVars.projectId}");


            //
            // => Transform the datareferences that we have found
            //
            var adjustedDataReferenceFiles = new List<string>();
            foreach (var websiteDataReferencePair in websiteDataReferences)
            {
                var websiteDataReference = websiteDataReferencePair.Key;
                var itemId = websiteDataReferencePair.Value;
                var xmlHierarchy = websiteHierarchies[websiteDataReference];

                // Calculate path to the data file
                var websiteDataReferencePathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId) + "/" + websiteDataReference;

                try
                {
                    if (File.Exists(websiteDataReferencePathOs))
                    {
                        var xmlDataReference = new XmlDocument();
                        xmlDataReference.Load(websiteDataReferencePathOs);

                        // Execute the transformation
                        var xmlAdjustedDataReference = await TransformWebsiteDataReference(xmlDataReference);

                        if (xmlDataReference.Canonicalize() != xmlAdjustedDataReference.Canonicalize())
                        {
                            // A replacement has been made
                            xmlAdjustedDataReference.Save(websiteDataReferencePathOs);

                            // Retrieve the information to use in the commit message
                            SiteStructureItem commitInformation = RetrieveCommitInformation(projectVars.projectId, xmlDataReference, websiteDataReferencePathOs, xmlHierarchy, XmlCmsContentMetadata);

                            // Construct a commit message
                            XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                            xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "transform";
                            xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = commitInformation.Linkname;
                            xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", projectVars.outputChannelDefaultLanguage);
                            xmlCommitMessage.SelectSingleNode("/root/id").InnerText = commitInformation.Id;
                            var message = xmlCommitMessage.DocumentElement.InnerXml;

                            // Commit the result in the GIT repository
                            CommitFilingData(message, ReturnTypeEnum.Xml, true);

                            adjustedDataReferenceFiles.Add(websiteDataReference);
                        }
                        else
                        {
                            appLogger.LogInformation($"No replacements were made in the datareference file. websiteDataReferencePathOs: {websiteDataReferencePathOs}, projectId: {projectVars.projectId}");
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"Could not load datareference because it does not exist on the disk. websiteDataReferencePathOs: {websiteDataReferencePathOs}, projectId: {projectVars.projectId}");
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"There was an error transforming the data reference: websiteDataReferencePathOs: {websiteDataReferencePathOs}, projectId: {projectVars.projectId}");
                }
            }

            // Return the success message
            var projectName = nodeCurrentProject.SelectSingleNode("name")?.InnerText ?? projectId;
            var returnMessage = (adjustedDataReferenceFiles.Count > 0) ? $"Successfully transformed {adjustedDataReferenceFiles.Count} content data files in project '{projectName}'" : $"No need to change paths in project '{projectName}'";
            var debugInfo = (adjustedDataReferenceFiles.Count > 0) ? $"dataFilesChanged: {string.Join(", ", adjustedDataReferenceFiles.ToArray())}, " : "";
            return new TaxxorReturnMessage(true, returnMessage, $"{debugInfo}sourceDataFolderPathOs: {cmsDataRootPathOs}");
        }


        /// <summary>
        /// Customizable logic to transform the content of a website datareference file
        /// </summary>
        /// <param name="dataReference"></param>
        /// <returns></returns>
        private async static Task<XmlDocument> TransformWebsiteDataReference(XmlDocument dataReference)
        {
            await DummyAwaiter();
            var transformedDataRefence = new XmlDocument();
            transformedDataRefence.ReplaceContent(dataReference);

            // Replace the subscribe link
            var nodeListSubscribeLinks = transformedDataRefence.SelectNodes("//a[contains(@href, 'http://www.feedback.philips.com/dedicated/investor-subscription')]");
            foreach (XmlNode nodeSubscribeLink in nodeListSubscribeLinks)
            {
                nodeSubscribeLink.SetAttribute("href", "https://www.philips.com/c-e/investor-relations.html");
                nodeSubscribeLink.SetAttribute("target", "_blank");
            }

            return transformedDataRefence;
        }


        /// <summary>
        /// Bulk restore content from version control system
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [DataServiceTool("bulkrestore", Name = "Utility: Bulk restore content", Description = "Restores content data files from the version control system across multiple projects in one go")]
        public async static Task<TaxxorReturnMessage> RestoreFromVersionControl(dynamic projectId)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Change the existing project variables object to reflect the project context of this method
            //
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
            if (!renderProjectVarsResult) appLogger.LogError("Unable to render project variables from a project ID");
            FillCorePathsInProjectVariables(ref projectVars);

            // - Use a fake section id in order to hide the replacement warning
            projectVars.did = "foobar";
            SetProjectVariables(context, projectVars);

            //
            // => Global variables for this section
            //

            var nodeProjectRoot = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects");
            var nodeCurrentProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{projectId}']");
            var cmsDataRootPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId);
            //var sourceDataFolderPathOs = projectVars.cmsDataRootPathOs;

            // Retrieve all the hierarchies defined for this project
            var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectVars.projectId, false);

            var dataReferencesToRestore = new List<string>();
            // dataReferencesToRestore.Add("version_1/textual/website.xml");
            dataReferencesToRestore.Add("version_1/textual/website-historical.xml");

            var adjustedDataReferenceFiles = new List<string>();
            var failedDataReferences = new List<string>();

            if (debugRoutine)
            {
                Console.WriteLine("--------------------------------------------");
                Console.WriteLine($"- projectVars.cmsDataRootBasePathOs: {projectVars.cmsDataRootBasePathOs}");
                Console.WriteLine("--------------------------------------------");
            }

            //
            // => Create a temporary folder to restore the data references in
            // 
            var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/gitrestore-{projectId}";
            if (Directory.Exists(tempFolderPathOs))
            {
                EmptyDirectory(tempFolderPathOs);
            }
            else
            {
                Directory.CreateDirectory(tempFolderPathOs);
            }

            //
            // => Use GIT log to find the commit hashes of the files we want to restore
            //
            var commitsToRestore = new Dictionary<string, string>();

            foreach (var dataReference in dataReferencesToRestore)
            {
                string gitResult = "";
                List<string> gitCommandList = new List<string>();
                gitCommandList.Add($"git log --after=\"2020-12-02 00:00\" --before=\"2020-12-02 23:59\" -- {dataReference}");
                gitResult = GitCommand(gitCommandList, projectVars.cmsDataRootBasePathOs, ReturnTypeEnum.Xml, false);
                if (debugRoutine)
                {
                    appLogger.LogDebug("");
                    appLogger.LogDebug($"-- git command: '{gitCommandList[0]}'");
                    appLogger.LogDebug($"-- gitResult: '{gitResult}'");
                }

                var commitHash = RegExpReplace(@"^(.*?commit\s+)([a-f0-9]{32,40}|[v\d\.]{3,9})(.*)$", gitResult, "$2", true);
                if (!string.IsNullOrEmpty(commitHash) && commitHash != gitResult)
                {
                    commitsToRestore.Add(dataReference, commitHash);
                }
                else
                {
                    appLogger.LogError($"Unable to extract GIT commit hash from '{gitResult}'");
                }

                if (gitResult == null)
                {
                    appLogger.LogWarning($"GIT command ${gitCommandList[0]} retuned {gitResult} in path {projectVars.cmsDataRootBasePathOs}");
                }
            }

            //
            // => Restore the files from the repository
            //
            foreach (var restoreInfo in commitsToRestore)
            {
                var pathToRestore = restoreInfo.Key;
                var commitHash = restoreInfo.Value;

                var restoreResult = GitExtractSingleFile(projectVars.cmsDataRootBasePathOs, commitHash, $"{projectVars.cmsDataRootBasePathOs}/{pathToRestore}", $"{tempFolderPathOs}/{Path.GetFileName(pathToRestore)}");
                Console.WriteLine($"- restoreResult: {restoreResult}");
            }

            //
            // => Rework the files so that they comply with the new structure of the website
            //
            var restoredFiles = Directory.GetFiles(tempFolderPathOs);
            foreach (var filePathOs in restoredFiles)
            {
                var xmlWebsiteDataSource = new XmlDocument();
                xmlWebsiteDataSource.Load(filePathOs);
                xmlWebsiteDataSource = TransformXmlToDocument(xmlWebsiteDataSource, $"{applicationRootPathOs}/backend/code/custom/website-fixer.xsl");
                xmlWebsiteDataSource.Save(filePathOs);
            }

            //
            // => Replace the current files in the project data store with the ones we have restored and fixed
            //
            foreach (var restoreInfo in commitsToRestore)
            {
                var baseFilePath = restoreInfo.Key;
                var commitHash = restoreInfo.Value;
                var sourceFilePathOs = $"{tempFolderPathOs}/{Path.GetFileName(baseFilePath)}";
                var targetFilePathOs = $"{projectVars.cmsDataRootBasePathOs}/{baseFilePath}";

                if (File.Exists(targetFilePathOs))
                {
                    File.Delete(targetFilePathOs);
                    File.Copy(sourceFilePathOs, targetFilePathOs);

                    var xmlSectionData = new XmlDocument();
                    xmlSectionData.Load(targetFilePathOs);

                    // Find information about the item in the hierarchy
                    SiteStructureItem commitInformation = RetrieveCommitInformation(projectVars.projectId, xmlSectionData, Path.GetFileName(targetFilePathOs), xmlHierarchyOverview, XmlCmsContentMetadata);


                    // Construct the commit message
                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                    SetAttribute(xmlCommitMessage.SelectSingleNode("/root/crud"), "originalcommithash", commitHash);
                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "contentdatarestore";
                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = commitInformation.Linkname;
                    xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", projectVars.outputChannelDefaultLanguage);
                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = commitInformation.Id;
                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                    // Commit the result in the GIT repository
                    CommitFilingData(message, ReturnTypeEnum.Xml, false);

                    adjustedDataReferenceFiles.Add(baseFilePath);
                }
                else
                {
                    appLogger.LogError($"Unable to locate the target file in the project data store {targetFilePathOs}");
                }

            }

            // Return the success message
            var projectName = nodeCurrentProject.SelectSingleNode("name")?.InnerText ?? projectId;
            var returnMessage = (adjustedDataReferenceFiles.Count > 0) ? $"Successfully restored {adjustedDataReferenceFiles.Count} content data files in project '{projectName}'" : $"Nothing needed to be restored in project '{projectName}'";
            var debugInfo = (adjustedDataReferenceFiles.Count > 0) ? $"commitsToRestore: {string.Join(", ", commitsToRestore.ToArray())}, " : $"commitsToRestore: {string.Join(", ", commitsToRestore.ToArray())}, ";
            return new TaxxorReturnMessage(true, returnMessage, $"{debugInfo}sourceDataFolderPathOs: {cmsDataRootPathOs}");
        }


        /// <summary>
        /// Bulk transform all Structured Data Element cache files in one go
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [DataServiceTool("bulktransformdatasdecachefiles", Name = "Transform: Bulk transform all SDE cache files", Description = "Bulk transform all Structured Data Element cache files in one go. Uses XSLT stylesheet <code>sdecache-convert.xsl</code>")]
        public async static Task<TaxxorReturnMessage> ToolBulkTransformAllStructuredDataCacheFiles(dynamic projectId)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Change the existing project variables object to reflect the project context of this method
            //
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
            if (!renderProjectVarsResult) appLogger.LogError("Unable to render project variables from a project ID");
            FillCorePathsInProjectVariables(ref projectVars);

            // - Use a fake section id in order to hide the replacement warning
            projectVars.did = "foobar";
            SetProjectVariables(context, projectVars);

            //
            // => Global variables for this section
            //

            var nodeProjectRoot = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects");
            var nodeCurrentProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{projectId}']");
            var cmsDataRootPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId);
            //var sourceDataFolderPathOs = projectVars.cmsDataRootPathOs;

            // Retrieve all the hierarchies defined for this project
            var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectVars.projectId, false);

            // Retrieve the languages for this project
            var languages = RetrieveProjectLanguages(RetrieveEditorIdFromProjectId(projectId));

            var adjustedSdeCacheFiles = new List<string>();

            if (debugRoutine)
            {
                Console.WriteLine("--------------------------------------------");
                Console.WriteLine($"- sourceDataFolderPathOs: {cmsDataRootPathOs}");
                Console.WriteLine("--------------------------------------------");
            }


            //
            // Loop through the data files
            //
            // var replacedOne = false;
            if (Directory.Exists(cmsDataRootPathOs))
            {


                var searchPattern = "*.xml";
                try
                {
                    var allDataFiles = Directory.EnumerateFiles(cmsDataRootPathOs, searchPattern, SearchOption.AllDirectories);


                    foreach (string filePathOs in allDataFiles)
                    {
                        var fileReference = filePathOs.Replace(cmsDataRootPathOs, "").Replace("/", "");

                        try
                        {
                            var xmlSdeCacheData = new XmlDocument();
                            xmlSdeCacheData.Load(filePathOs);

                            var adjustments = 0;

                            //
                            // => Detect the type of XML document we are processing
                            //
                            var nodeListContent = xmlSdeCacheData.SelectNodes("/elements/element[@id and @status]");
                            var isSdeCacheFile = (nodeListContent.Count > 0);

                            if (isSdeCacheFile)
                            {

                                //
                                // => Transformations for the complete document
                                //
                                var xsltArgumentList = new XsltArgumentList();
                                for (int i = 0; i < languages.Length; i++)
                                {
                                    var language = languages[i];
                                    var parameterName = $"language{(i + 1)}";
                                    xsltArgumentList.AddParam(parameterName, "", language);
                                }
                                var xmlTranslated = TransformXmlToDocument(xmlSdeCacheData, "xsl_sdecache-convert", xsltArgumentList);


                                if (GenerateXmlHash(xmlSdeCacheData) != GenerateXmlHash(xmlTranslated))
                                {
                                    // Something has changed
                                    xmlSdeCacheData.ReplaceContent(xmlTranslated, true);

                                    adjustments++;
                                }


                                //
                                // => Commit the changes to the file
                                //
                                if (adjustments > 0)
                                {
                                    // Save the filing data
                                    xmlSdeCacheData.Save(filePathOs);

                                    // Retrieve the information to use in the commit message
                                    SiteStructureItem commitInformation = RetrieveCommitInformation(projectVars.projectId, xmlSdeCacheData, fileReference, xmlHierarchyOverview, XmlCmsContentMetadata);

                                    // Construct a commit message
                                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "transform";
                                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = commitInformation.Linkname;
                                    xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", projectVars.outputChannelDefaultLanguage);
                                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = commitInformation.Id;
                                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                                    // Commit the result in the GIT repository
                                    CommitFilingData(message, ReturnTypeEnum.Xml, false);
                                }


                            }




                            // Add the data reference to the changed datareferences list
                            if (adjustments > 0)
                            {
                                if (!adjustedSdeCacheFiles.Contains(fileReference)) adjustedSdeCacheFiles.Add(fileReference);
                            }
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Could not load or save SDE cache xml data. filePathOs: {filePathOs}, error: {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"There was a problem looping through the data files. error: {ex}");
                }
            }
            else
            {
                appLogger.LogError($"Could not find the root folder of the data files. cmsDataRootPathOs: {cmsDataRootPathOs}, stack-trace: ${GetStackTrace()}");
            }


            // Return the success message
            var projectName = nodeCurrentProject.SelectSingleNode("name")?.InnerText ?? projectId;
            var returnMessage = (adjustedSdeCacheFiles.Count > 0) ? $"Successfully bulk transformed in {adjustedSdeCacheFiles.Count} SDE cache files in project '{projectName}'" : $"No need to bulk transform in project '{projectName}'";
            var debugInfo = (adjustedSdeCacheFiles.Count > 0) ? $"sdeCacheFilesChanged: {string.Join(", ", adjustedSdeCacheFiles.ToArray())}, " : "";
            return new TaxxorReturnMessage(true, returnMessage, $"{debugInfo}sourceDataFolderPathOs: {cmsDataRootPathOs}");
        }

        /// <summary>
        /// Migrate external data (Excel) ID's and their mappings using a mapping table stored in this method
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [DataServiceTool("changeexternaldataids", Name = "Utility: Change external data (Excel workbook) ID's", Description = "Use a dictionary stored in method <code>ToolChangeExternalDataIds</code> to change the ID's of the external datasets. Useful when a customer added an Excel workbook with an ID that for instance contains a data reference.<br/><strong>Use this tool only on all projects!</strong>")]
        public async static Task<TaxxorReturnMessage> ToolChangeExternalDataIds(dynamic projectId)
        {
            var logMessages = new List<string>();
            try
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");


                var context = System.Web.Context.Current;
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                //
                // => Change the existing project variables object to reflect the project context of this method
                //
                var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
                if (!renderProjectVarsResult) appLogger.LogError("Unable to render project variables from a project ID");
                FillCorePathsInProjectVariables(ref projectVars);

                // - Use a fake section id in order to hide the replacement warning
                projectVars.did = "foobar";
                SetProjectVariables(context, projectVars);

                //
                // => Global variables for this section
                //

                var nodeProjectRoot = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects");
                var nodeCurrentProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{projectId}']");
                var cmsDataRootPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId);
                //var sourceDataFolderPathOs = projectVars.cmsDataRootPathOs;

                // Retrieve all the hierarchies defined for this project
                var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectVars.projectId, false);

                // Retrieve the languages for this project
                var languages = RetrieveProjectLanguages(RetrieveEditorIdFromProjectId(projectId));

                var adjustedContentFiles = new List<string>();

                if (debugRoutine)
                {
                    Console.WriteLine("--------------------------------------------");
                    Console.WriteLine($"- sourceDataFolderPathOs: {cmsDataRootPathOs}");
                    Console.WriteLine("--------------------------------------------");
                }

                //
                // => Define a mapping of id's per Taxxor client id
                //

                // - Excel workbook set by the Taxxor Excel plugin
                var externalDatasetIdConversionMappings = new Dictionary<string, Dictionary<string, string>>();
                // For tiekinetix
                externalDatasetIdConversionMappings.Add("tiekinetix", new Dictionary<string, string>()
                {
                    {"fy22q1-tradingupdate", "lucanet-q1-data"},
                    {"lucanet-hyreport2022", "lucanet-hy-data"},
                    {"ESGReportFY22", "esgreport-data"},
                    {"fy22q3-tradingupdate", "lucanet-q3-data"}
                });

                // - Mappings used in the Editor to link an Excel workbook in the content so that the sheets are available in the "Insert external table" tool
                var editorExcelIdConversionMappings = new Dictionary<string, Dictionary<string, string>>();
                // For tiekinetix
                editorExcelIdConversionMappings.Add("tiekinetix", new Dictionary<string, string>()
                {
                    {"hy-financial-data", "financial-data"},
                    {"q4-financial-data", "financial-data"},
                    {"q1-financial-data", "financial-data"},
                    {"ESGreport", "esg-data"},
                    {"ar-financial-data", "financial-data"}
                });

                //
                // => Test if the current Taxxor Client ID is present in one of the mapping dictionaries
                //
                if (!externalDatasetIdConversionMappings.ContainsKey(TaxxorClientId) && !editorExcelIdConversionMappings.ContainsKey(TaxxorClientId))
                {
                    return new TaxxorReturnMessage(false, "No conversion mappings defined for this Taxxor customer");
                }

                var genericDataStoreConversionMappings = externalDatasetIdConversionMappings[TaxxorClientId];
                var editorMappings = editorExcelIdConversionMappings[TaxxorClientId];

                //
                // => Change the ID's in the Generic Data Store XML file
                //
                if (!IsRunningInDocker())
                {
                    var xmlGenericDataSoreFilePathOs = dataRootPathOs.SubstringBefore("/DocumentStore") + "/GenericDataConnector/Filestorage.xml";
                    xmlGenericDataSoreFilePathOs = $"/Users/jthijs/Documents/my_projects/taxxor/tdm/data/{TaxxorClientId}/GenericDataConnector/Filestorage.xml";
                    // Console.WriteLine($"- xmlGenericDataStorePathOs: {xmlGenericDataSoreFilePathOs}");
                    if (!File.Exists(xmlGenericDataSoreFilePathOs)) return new TaxxorReturnMessage(false, "Unable to find GenericDataService source file", $"xmlGenericDataStorePathOs: {xmlGenericDataSoreFilePathOs}");

                    var xmlGenericDataStore = new XmlDocument();
                    xmlGenericDataStore.Load(xmlGenericDataSoreFilePathOs);
                    var changedMappings = 0;

                    foreach (var pair in genericDataStoreConversionMappings)
                    {
                        var sourceId = pair.Key;
                        var targetId = pair.Value;
                        var nodeListWorksheets = xmlGenericDataStore.SelectNodes($"/fileStorage/fileEntry[@workbookid='{sourceId}']");
                        changedMappings = changedMappings + nodeListWorksheets.Count;
                        foreach (XmlNode nodeWorksheet in nodeListWorksheets)
                        {
                            nodeWorksheet.SetAttribute("workbookid", targetId);
                        }
                    }

                    // Console.WriteLine($"- changed mapping ID's in generic data service {changedMappings}");

                    if (changedMappings > 0)
                    {
                        logMessages.Add($"Changed {changedMappings} in Filestorage. PLEASE UPLOAD THE FILE TO THE SERVER");
                        await xmlGenericDataStore.SaveAsync(xmlGenericDataSoreFilePathOs, true, true);
                        Console.WriteLine($"- Stored updated Generic Data Service file in {xmlGenericDataSoreFilePathOs}");
                    }
                }
                else
                {
                    logMessages.Add("Cannot update the filedata settings on the server, use this tool locally");
                }


                //
                // => Update the mapping in the document store data file
                //
                var changedConfig = false;
                var dataConfigurationPathOs = CalculateFullPathOs("/configuration/configuration_system//config/location[@id='data_configuration']");
                var xmlDataConfiguration = new XmlDocument();
                xmlDataConfiguration.Load(dataConfigurationPathOs);

                var nodeListEditorExternalDataSetMappings = xmlDataConfiguration.SelectNodes($"/configuration/cms_projects/cms_project[@id='{projectId}']/repositories/external_data/sets/set");
                foreach (XmlNode nodeSet in nodeListEditorExternalDataSetMappings)
                {
                    var setId = nodeSet.GetAttribute("id");
                    if (genericDataStoreConversionMappings.ContainsKey(setId))
                    {
                        nodeSet.SetAttribute("id", genericDataStoreConversionMappings[setId]);
                        changedConfig = true;
                    }

                    var nodeSetName = nodeSet.SelectSingleNode("name");
                    if (editorMappings.ContainsKey(nodeSetName.InnerText))
                    {
                        nodeSetName.InnerText = editorMappings[nodeSetName.InnerText];
                        changedConfig = true;
                    }
                }

                if (changedConfig)
                {
                    await xmlDataConfiguration.SaveAsync(dataConfigurationPathOs, true, true);
                }






                //
                // Loop through the data files
                //
                // var replacedOne = false;
                if (Directory.Exists(cmsDataRootPathOs))
                {


                    var searchPattern = "*.xml";
                    try
                    {
                        var allDataFiles = Directory.EnumerateFiles(cmsDataRootPathOs, searchPattern, SearchOption.AllDirectories);


                        foreach (string filePathOs in allDataFiles)
                        {
                            var fileReference = filePathOs.Replace(cmsDataRootPathOs, "").Replace("/", "");

                            try
                            {
                                var xmlContentData = new XmlDocument();
                                xmlContentData.Load(filePathOs);

                                var adjustments = 0;

                                //
                                // => Detect the type of XML document we are processing
                                //
                                var nodeListContent = xmlContentData.SelectNodes("/data/content/article");
                                var isContentFile = (nodeListContent.Count > 0);

                                if (isContentFile)
                                {

                                    //
                                    // => Find the tables which we may need to adjust
                                    //
                                    var nodeListTables = xmlContentData.SelectNodes("/data/content/article//table[@data-workbookreference]");
                                    if (nodeListTables.Count > 0)
                                    {
                                        Console.WriteLine("Found " + nodeListTables.Count.ToString() + " tables to process in " + Path.GetFileName(filePathOs));
                                        foreach (XmlNode nodeTable in nodeListTables)
                                        {
                                            var currentWorkbookId = nodeTable.GetAttribute("data-workbookreference");
                                            if (editorMappings.ContainsKey(currentWorkbookId))
                                            {
                                                nodeTable.SetAttribute("data-workbookreference", editorMappings[currentWorkbookId]);
                                                adjustments++;
                                            }
                                        }
                                    }




                                    //
                                    // => Commit the changes to the file
                                    //
                                    if (adjustments > 0)
                                    {
                                        // Save the filing data
                                        xmlContentData.Save(filePathOs);

                                        // Retrieve the information to use in the commit message
                                        SiteStructureItem commitInformation = RetrieveCommitInformation(projectVars.projectId, xmlContentData, fileReference, xmlHierarchyOverview, XmlCmsContentMetadata);

                                        // Construct a commit message
                                        XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                                        xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "transform";
                                        xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = commitInformation.Linkname;
                                        xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", projectVars.outputChannelDefaultLanguage);
                                        xmlCommitMessage.SelectSingleNode("/root/id").InnerText = commitInformation.Id;
                                        var message = xmlCommitMessage.DocumentElement.InnerXml;

                                        // Commit the result in the GIT repository
                                        CommitFilingData(message, ReturnTypeEnum.Xml, false);
                                    }


                                }




                                // Add the data reference to the changed datareferences list
                                if (adjustments > 0)
                                {
                                    if (!adjustedContentFiles.Contains(fileReference)) adjustedContentFiles.Add(fileReference);
                                }
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, $"Could not load or save SDE cache xml data. filePathOs: {filePathOs}, error: {ex}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"There was a problem looping through the data files. error: {ex}");
                    }
                }
                else
                {
                    appLogger.LogError($"Could not find the root folder of the data files. cmsDataRootPathOs: {cmsDataRootPathOs}, stack-trace: ${GetStackTrace()}");
                }


                // Return the success message
                var projectName = nodeCurrentProject.SelectSingleNode("name")?.InnerText ?? projectId;
                var returnMessage = (adjustedContentFiles.Count > 0) ? $"Successfully replaced workbook ID's in {adjustedContentFiles.Count} content files for project '{projectName}'" : $"No need to change workbook ID's in project '{projectName}'";
                var debugInfo = (adjustedContentFiles.Count > 0) ? $"adjustedContentFiles: {string.Join(", ", adjustedContentFiles.ToArray())}, " : "";
                return new TaxxorReturnMessage(true, returnMessage, $"{string.Join(", ", logMessages.ToArray())}{debugInfo}cmsDataRootPathOs: {cmsDataRootPathOs}");
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "Something went wrong", ex);
            }

        }

        /// <summary>
        /// Prepare the Document Store for the Generated Reports Repository
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [DataServiceTool("preparegeneratedreportsrepositories", Name = "Utility: Prepare generated reports repository", Description = "Prepare the Document Store for the Generated Reports Repository")]
        public async static Task<TaxxorReturnMessage> PrepareGeneratedReportsRepository(dynamic projectId)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Change the existing project variables object to reflect the project context of this method
            //
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
            if (!renderProjectVarsResult) appLogger.LogError("Unable to render project variables from a project ID");
            FillCorePathsInProjectVariables(ref projectVars);

            // - Use a fake section id in order to hide the replacement warning
            projectVars.did = "foobar";
            SetProjectVariables(context, projectVars);

            //
            // => Global variables for this section
            //

            var nodeProjectRoot = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects");
            var nodeCurrentProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{projectId}']");
            var cmsDataRootPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectId);
            //var sourceDataFolderPathOs = projectVars.cmsDataRootPathOs;


            //
            // => Add the reports folder to gitignore file
            //
            var gitIgnorePathOs = $"{projectVars.cmsContentRootPathOs}/.gitignore";
            if (File.Exists(gitIgnorePathOs))
            {
                var gitIgnoreContent = File.ReadAllText(gitIgnorePathOs);
                if (!gitIgnoreContent.Contains("\n/reports")) TextFileAppend("/reports", gitIgnorePathOs);
            }
            else
            {
                appLogger.LogError($"Unable to find the.gitignore file at {gitIgnorePathOs}");
            }


            var reportsLibraryRootPathOs = CalculateFullPathOs("", "projectreports", null, projectVars);


            // Return the success message
            var projectName = nodeCurrentProject.SelectSingleNode("name")?.InnerText ?? projectId;
            return new TaxxorReturnMessage(true, $"Prepared Generated Reports Repository in project {projectName}", $"reportsLibraryRootPathOs: {reportsLibraryRootPathOs}");
        }

        
    }

}