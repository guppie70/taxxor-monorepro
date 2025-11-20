using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using static Taxxor.Project.ProjectLogic;

namespace Taxxor.Project
{

    /// <summary>
    /// Logic for dealing with CMS (section) metadata
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Updates the in-memory CMS Metadata XML object
        /// </summary>
        /// <param name="projectId">When passed, updates only the metadata structure of a specific project in the structure</param>
        /// <returns></returns>
        public static async Task UpdateCmsMetadata(string? projectId = null)
        {
            // Execute the update routine
            var updateResult = await _UpdateCmsMetadata(projectId);

            // Handle result
            if (!updateResult.Success)
            {
                Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
                Console.WriteLine(updateResult.Message);
                Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            }
        }

        /// <summary>
        /// Updates the in-memory CMS Metadata XML object
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        private static async Task<TaxxorReturnMessage> _UpdateCmsMetadata(string? projectId = null)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Render the CMS Content metadata
            if (!MetadataBeingUpdated)
            {
                MetadataBeingUpdated = true;
                XmlDocument xmlUpdatedCmsContentMetadata = CompileCmsMetadata(projectId);



                var errorCount = GetAttribute(xmlUpdatedCmsContentMetadata.DocumentElement, "failure-count") ?? "";
                if (errorCount == "0")
                {
                    var currentMetadataHash = (XmlCmsContentMetadata.ChildNodes.Count > 0) ? GenerateXmlHash(XmlCmsContentMetadata) : "nothing";

                    if (projectId == null)
                    {
                        // Inject or replace the information we have just generated
                        var nodeListUpdatedProjectMetadata = xmlUpdatedCmsContentMetadata.SelectNodes("/projects/cms_project");
                        foreach (XmlNode nodeUpdatedProjectMetadata in nodeListUpdatedProjectMetadata)
                        {
                            var currentProjectId = nodeUpdatedProjectMetadata.GetAttribute("id");
                            var nodeExistingProject = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{currentProjectId}']");
                            if (nodeExistingProject != null)
                            {
                                // Replace
                                ReplaceXmlNode(nodeExistingProject, nodeUpdatedProjectMetadata);
                            }
                            else
                            {
                                // Append
                                var nodeUpdatedProjectMetadataImported = XmlCmsContentMetadata.ImportNode(nodeUpdatedProjectMetadata, true);
                                XmlCmsContentMetadata.DocumentElement.AppendChild(nodeUpdatedProjectMetadataImported);
                            }
                        }
                    }
                    else
                    {
                        // Inject the retrieved cms_project node into the overall XML structure
                        var nodeUpdatedProject = xmlUpdatedCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']");
                        if (nodeUpdatedProject != null)
                        {
                            var nodeUpdatedProjectImported = XmlCmsContentMetadata.ImportNode(nodeUpdatedProject, true);
                            var nodeProjectToReplace = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']");
                            if (nodeProjectToReplace != null)
                            {
                                // Replace
                                appLogger.LogDebug("Project replaced in XmlCmsContentMetadata");
                                ReplaceXmlNode(nodeProjectToReplace, nodeUpdatedProjectImported);
                            }
                            else
                            {
                                // Append
                                appLogger.LogDebug("Project appended to XmlCmsContentMetadata");
                                XmlCmsContentMetadata.DocumentElement.AppendChild(nodeUpdatedProjectImported);
                            }
                        }
                        else
                        {
                            return new TaxxorReturnMessage(false, "Unable to update XmlCmsContentMetadata for a single project because we could not find a node to import");
                        }

                    }


                    MetadataBeingUpdated = false;

                    var newMetadataHash = (XmlCmsContentMetadata.ChildNodes.Count > 0) ? GenerateXmlHash(XmlCmsContentMetadata) : "nothing";

                    // Store the content in the cache directory so that we can speed up the boot time of the application when it launches
                    // if (debugRoutine) Console.WriteLine($"currentMetadataHash = {currentMetadataHash}, newMetadataHash = {newMetadataHash}");
                    if (currentMetadataHash != newMetadataHash) await XmlCmsContentMetadata.SaveAsync(CmsContentMetadataFilePathOs);
                }
                else
                {
                    MetadataBeingUpdated = false;

                    return new TaxxorReturnMessage(false, "FAILED XmlCmsContentMetadata");
                }

            }
            else
            {
                return new TaxxorReturnMessage(false, "Unable to update XmlCmsContentMetadata because another process is updating it");
            }

            // Return a success message
            return new TaxxorReturnMessage(true, "Successfully updated XmlCmsContentMetadata");
        }


        /// <summary>
        /// Remove a single metadata entry from the global metadata XmlDocument object
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="fileReference"></param>
        /// <returns></returns>
        public static async Task RemoveCmsMetadataEntry(string projectId, string fileReference)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            try
            {
                if (!MetadataBeingUpdated)
                {
                    MetadataBeingUpdated = true;

                    XmlDocument xmlUpdatedCmsContentMetadata = new XmlDocument();
                    xmlUpdatedCmsContentMetadata.ReplaceContent(XmlCmsContentMetadata);

                    var nodeMetadata = xmlUpdatedCmsContentMetadata.CreateElement("metadata");

                    var nodeContent = xmlUpdatedCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']/data/content[@ref='{fileReference}' and @datatype='sectiondata']");
                    if (nodeContent != null)
                    {
                        RemoveXmlNode(nodeContent);
                    }
                    else
                    {
                        appLogger.LogWarning($"Could not find '{fileReference}' in project '{projectId}'");
                    }

                    // Update the global variable
                    XmlCmsContentMetadata.ReplaceContent(xmlUpdatedCmsContentMetadata);

                    MetadataBeingUpdated = false;

                    // Store the content in the log directory so that we can inspect it
                    if (debugRoutine) await xmlUpdatedCmsContentMetadata.SaveAsync($"{logRootPathOs}/_cms-content-metadata.xml");
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"There was an error removing the metadata entry '{fileReference}' in project '{projectId}'");

            }
            finally
            {
                MetadataBeingUpdated = false;
            }
        }



        /// <summary>
        /// Updates a single entry in the global xmlCmsContentMetadata XmlDocument with the information stored in the section data
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlSectionData"></param>
        /// <param name="fileReference"></param>
        public static async Task UpdateCmsMetadataEntry(string projectId, string fileReference)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            try
            {
                if (!MetadataBeingUpdated)
                {

                    MetadataBeingUpdated = true;

                    // Determine the folder where the data lives
                    var cmsDataRootPathOs = RetrieveFilingDataFolderPathOs(projectId);
                    var filePathOs = $"{cmsDataRootPathOs}/{fileReference}";

                    var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectId, false);

                    XmlDocument xmlUpdatedCmsContentMetadata = new XmlDocument();
                    xmlUpdatedCmsContentMetadata.ReplaceContent(XmlCmsContentMetadata);
                    var nodeProject = xmlUpdatedCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']");

                    //                                 
                    // => Render a metadata content elemement and replace it in the metadata object
                    // 
                    var metadataContentResult = _createMetadataContent(projectId, filePathOs, xmlHierarchyOverview);
                    if (!metadataContentResult.Success)
                    {
                        appLogger.LogError($"{metadataContentResult.Message}, {metadataContentResult.LogToString()}");
                    }
                    var nodeContentImported = xmlUpdatedCmsContentMetadata.ImportNode(metadataContentResult.XmlPayload.DocumentElement, true);

                    //
                    // => Update or append the metadata information
                    //
                    var nodeContent = nodeProject.SelectSingleNode($"data/content[@ref='{fileReference}']");
                    if (nodeContent == null)
                    {
                        // This is a new data reference file, so we should just append it to the list
                        nodeProject.SelectSingleNode($"data")?.AppendChild(nodeContentImported);
                    }
                    else
                    {
                        // Replace the existing definition
                        ReplaceXmlNode(nodeContent, nodeContentImported);
                    }


                    // Calculate a new hash for the project contents
                    var sbProjectContentHash = new StringBuilder();
                    if (nodeProject != null)
                    {
                        var xPathContent = $"data/content[@hash]";
                        var nodeListContents = nodeProject.SelectNodes(xPathContent);
                        foreach (XmlNode node in nodeListContents)
                        {
                            sbProjectContentHash.Append(node.GetAttribute("hash"));
                        }
                        // Set a hash on the project node to indicate if files in this folder have been modified
                        SetAttribute(nodeProject, "contentupdatedhash", md5(sbProjectContentHash.ToString()));
                    }
                    else
                    {
                        appLogger.LogWarning($"Could not update the content hash on the CMS Metadata project node for {projectId}");
                    }


                    // Update the global variable
                    XmlCmsContentMetadata.ReplaceContent(xmlUpdatedCmsContentMetadata);

                    MetadataBeingUpdated = false;

                    // Store the content in the log directory so that we can inspect it
                    if (debugRoutine) await xmlUpdatedCmsContentMetadata.SaveAsync($"{logRootPathOs}/_cms-content-metadata.xml");

                }
                else
                {
                    appLogger.LogWarning($"Unable to update the metadata cache for '{fileReference}' in project '{projectId}' because another process is updating the cache file already");
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"There was an error updating the metadata cache for '{fileReference}' in project '{projectId}'");
            }
            finally
            {
                MetadataBeingUpdated = false;
            }

        }


        /// <summary>
        /// Loops through all the project data files and compiles a list of core metadata that can efficiently be used in the system by storing it in-memory
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task CompileCmsMetadata(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            string? projectId = request.RetrievePostedValue("projectid", RegexEnum.None, false, ReturnTypeEnum.Xml);
            if (string.IsNullOrEmpty(projectId) || projectId == "all") projectId = null;

            // Execute the update routine
            var updateResult = await _UpdateCmsMetadata(projectId);

            // Handle result
            if (!updateResult.Success)
            {
                appLogger.LogError(updateResult.ToString());
                HandleError(updateResult);
            }
            else
            {
                // Attach the current state of the CMS Content Metadata to the response
                updateResult.XmlPayload = XmlCmsContentMetadata;
                await response.OK(updateResult, ReturnTypeEnum.Xml, true);
            }
        }



        /// <summary>
        /// Loops through all the project data files and compiles a list of core metadata that can efficiently be used in the system by storing it in-memory
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="processOnlyOpenProjects"></param>
        /// <returns></returns>
        public static XmlDocument CompileCmsMetadata(string projectId = null, bool processOnlyOpenProjects = true)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            XmlDocument xmlCmsMetadata = new XmlDocument();
            xmlCmsMetadata.AppendChild(xmlCmsMetadata.CreateElement("projects"));
            var successCount = 0;
            var errorCount = 0;

            try
            {
                //
                // => Update the in-memory application configuration with the data configuration state because data configuration might have changed
                //
                var updateResult = UpdateDataConfigurationInApplicationConfiguration().GetAwaiter().GetResult();
                if (!updateResult.Success)
                {
                    appLogger.LogWarning($"Failed to update the in-memory application configuration. message: {updateResult.Message}, debuginfo: {updateResult.DebugInfo}");
                }

                //
                // => Find the projects for which we have to gather metadata information
                //
                var xPathProjects = (processOnlyOpenProjects) ? "/configuration/cms_projects/cms_project[versions/version/status/text() = 'open']" : "/configuration/cms_projects/cms_project";
                if (!string.IsNullOrEmpty(projectId))
                {
                    xPathProjects = $"/configuration/cms_projects/cms_project[@id='{projectId}']";
                }


                var nodeListProjects = xmlApplicationConfiguration.SelectNodes(xPathProjects);
                foreach (XmlNode nodeCurrentProject in nodeListProjects)
                {
                    var currentProjectId = GetAttribute(nodeCurrentProject, "id") ?? "";
                    var nodeProject = xmlCmsMetadata.CreateElement("cms_project");
                    SetAttribute(nodeProject, "id", currentProjectId);

                    var sbProjectContentHash = new StringBuilder();

                    var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(currentProjectId, false);

                    // Determine the folder where the data lives
                    var cmsDataRootPathOs = RetrieveFilingDataFolderPathOs(currentProjectId);

                    // Loop through the data files
                    if (Directory.Exists(cmsDataRootPathOs))
                    {
                        SetAttribute(nodeProject, "datalocation", cmsDataRootPathOs);

                        var nodeData = xmlCmsMetadata.CreateElement("data");

                        var searchPattern = "*.xml";
                        try
                        {
                            var allCacheFiles = Directory.EnumerateFiles(cmsDataRootPathOs, searchPattern, SearchOption.AllDirectories);

                            SetAttribute(nodeData, "basepathos", cmsDataRootPathOs.Replace(applicationRootPathOs, ""));

                            foreach (string filePathOs in allCacheFiles)
                            {
                                //                                 
                                // => Render a metadata content elemement
                                // 

                                var metadataContentResult = _createMetadataContent(currentProjectId, filePathOs, xmlHierarchyOverview);
                                if (metadataContentResult.Success)
                                {
                                    var nodeContentImported = xmlCmsMetadata.ImportNode(metadataContentResult.XmlPayload.DocumentElement, true);
                                    nodeData.AppendChild(nodeContentImported);
                                }
                                else
                                {
                                    appLogger.LogError($"{metadataContentResult.Message}, {metadataContentResult.LogToString()}");
                                    errorCount++;
                                }
                            }

                            nodeProject.AppendChild(nodeData);


                            //
                            // Calculate and set a hash on the project node to indicate if files in this folder have been modified
                            //
                            var nodeListContentHash = nodeProject.SelectNodes("data/content[@hash]");
                            if (nodeListContentHash.Count == 0)
                            {
                                appLogger.LogWarning("Unable to render a project content hash for the CMS Metadata object");
                                errorCount++;
                            }
                            else
                            {
                                foreach (XmlNode nodeContent in nodeListContentHash)
                                {
                                    sbProjectContentHash.Append(nodeContent.GetAttribute("hash"));
                                }
                            }

                            SetAttribute(nodeProject, "contentupdatedhash", md5(sbProjectContentHash.ToString()));

                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, "There was an error accessing the data folder");
                        }
                    }
                    else
                    {
                        SetAttribute(nodeProject, "error", $"data directory not found: {cmsDataRootPathOs}");
                    }


                    xmlCmsMetadata.DocumentElement.AppendChild(nodeProject);
                }
            }
            catch (Exception overallError)
            {
                errorCount++;
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
                Console.WriteLine("There was an error in CompileCmsMetadata()");
                Console.WriteLine(overallError.ToString());
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            }


            // Set timestamp and other meta information on the root node
            SetAttribute(xmlCmsMetadata.DocumentElement, "date-created", createIsoTimestamp());
            SetAttribute(xmlCmsMetadata.DocumentElement, "succes-count", successCount.ToString());
            SetAttribute(xmlCmsMetadata.DocumentElement, "failure-count", errorCount.ToString());

            var currentMetadataHash = (XmlCmsContentMetadata.ChildNodes.Count > 0) ? GenerateXmlHash(XmlCmsContentMetadata) : "nothing";
            var newMetadataHash = (xmlCmsMetadata.ChildNodes.Count > 0) ? GenerateXmlHash(xmlCmsMetadata) : "nothing";

            // Store the content in the cache directory so that we can speed up the boot time of the application when it launches
            // if (debugRoutine) Console.WriteLine($"currentMetadataHash = {currentMetadataHash}, newMetadataHash = {newMetadataHash}");
            if (currentMetadataHash != newMetadataHash) xmlCmsMetadata.Save(CmsContentMetadataFilePathOs);

            return xmlCmsMetadata;
        }

        private static TaxxorLogReturnMessage _createMetadataContent(string currentProjectId, string filePathOs, XmlDocument xmlHierarchyOverview = null)
        {
            /*
           <content ref="notes-to-the-consolidated-financial-statements.xml" datemodified="2021-07-13T22:04:04.0000000+02:00" dateaccessed="2021-07-20T09:41:35.0000000+02:00" hash="e3mkN5lE7xaZOSvTgyPOR2GpOYg=" datatype="sectiondata">
                <metadata>
                    <entry key="datecreated">2021-07-12T14:38:34.0458877+00:00</entry>
                    <entry key="datemodified">2021-07-12T14:38:34.0458877+00:00</entry>
                    <entry key="articletype">regular</entry>
                    <entry key="languages">en</entry>
                    <entry key="ids">notes-to-the-consolidated-financial-statements</entry>
                    <entry key="titles">Notes to the Consolidated Financial Statements</entry>
                    <entry key="inuse">true</entry>
                    <entry key="hasgraphs">false</entry>
                    <entry key="hasdrawings">false</entry>
                    <entry key="hasimages">false</entry>
                </metadata>
            </content> 
    */
            var logMessage = "";
            var returnMessage = new TaxxorLogReturnMessage();

            // The XmlDocument we will be building up
            var xmlCmsMetadataContent = new XmlDocument();

            // Determine the folder where the data lives
            var cmsDataRootPathOs = RetrieveFilingDataFolderPathOs(currentProjectId);
            var fileReference = filePathOs.Replace(cmsDataRootPathOs, "");
            if (fileReference.StartsWith('/')) fileReference = fileReference.Substring(1);

            var nodeContent = xmlCmsMetadataContent.CreateElement("content");
            SetAttribute(nodeContent, "ref", fileReference);

            var nodeMetadata = xmlCmsMetadataContent.CreateElement("metadata");

            // Add dates associated with the file
            var fileInfo = new FileInfo(filePathOs);
            var dateFileCreated = fileInfo.CreationTime.ToString("o");
            var dateFileModified = fileInfo.LastWriteTime.ToString("o");
            var dateFileAccessed = fileInfo.LastAccessTime.ToString("o");

            // nodeContent.SetAttribute("datecreated",fileInfo.CreationTime.ToString("o"));
            nodeContent.SetAttribute("datemodified", dateFileModified);
            nodeContent.SetAttribute("dateaccessed", dateFileAccessed);

            try
            {
                var xmlSectionData = new XmlDocument();
                xmlSectionData.Load(filePathOs);

                // Calculate a hash of the file contents
                var fileContentHash = "";
                try
                {
                    fileContentHash = GenerateXmlHash(xmlSectionData);
                }
                catch (Exception ex)
                {
                    logMessage = $"Could not calculate the content hash of data file with ref: {fileReference}, currentProjectId: {currentProjectId}";
                    appLogger.LogError(ex, logMessage);
                    returnMessage.ErrorLog.Add(logMessage);
                }
                nodeContent.SetAttribute("hash", fileContentHash);
                // sbProjectContentHash.Append(fileContentHash);

                // Detect the type of XML document we are processing
                var dataType = "";
                if (xmlSectionData.SelectSingleNode("/data/content") != null)
                {
                    dataType = "sectiondata";
                }
                else if (xmlSectionData.SelectSingleNode("/*/tableDefinition") != null)
                {
                    dataType = "tablecache";
                }
                else if (xmlSectionData.SelectSingleNode("/footnotes") != null)
                {
                    dataType = "footnote";
                }
                else if (xmlSectionData.SelectSingleNode("/elements") != null)
                {
                    dataType = "structureddatacache";
                }
                else if (xmlSectionData.SelectSingleNode("/syncreport") != null)
                {
                    dataType = "syncreport";
                }

                SetAttribute(nodeContent, "datatype", dataType);

                try
                {
                    switch (dataType)
                    {
                        case "tablecache":
                            // Clone the metadata entries
                            foreach (XmlNode nodeEntry in xmlSectionData.SelectNodes("/html/tableDefinition/metaData/entry"))
                            {
                                var nodeEntryImported = xmlCmsMetadataContent.ImportNode(nodeEntry, true);
                                nodeMetadata.AppendChild(nodeEntryImported);
                            }

                            nodeContent.AppendChild(nodeMetadata);
                            break;

                        case "sectiondata":
                            // Fill the metadata node with the information found in the section XML data
                            if (xmlHierarchyOverview == null)
                            {
                                logMessage = $"Cannot render content for the CMS metadata without the xmlHierarchyOverview object for file with ref: {fileReference}, currentProjectId: {currentProjectId}";
                                appLogger.LogError(logMessage);
                                returnMessage.ErrorLog.Add(logMessage);
                            }
                            else
                            {
                                _createSectionDataMetadataNodeContent(ref nodeMetadata, currentProjectId, xmlCmsMetadataContent, xmlSectionData, fileReference, xmlHierarchyOverview);

                                nodeContent.AppendChild(nodeMetadata);
                            }

                            break;

                        case "footnote":
                            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadataContent, "count", xmlSectionData.SelectNodes("/footnotes/footnote").Count.ToString()));
                            nodeContent.AppendChild(nodeMetadata);
                            break;

                        case "structureddatacache":
                            FileInfo fi = new FileInfo(filePathOs);
                            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadataContent, "datecreated", dateFileCreated));
                            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadataContent, "datemodified", dateFileModified));
                            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadataContent, "count", xmlSectionData.SelectNodes("/elements/element").Count.ToString()));
                            nodeContent.AppendChild(nodeMetadata);
                            break;

                        // Files for which we do not need to create an entry in the metadata report
                        case "syncreport":
                            break;

                        default:
                            appLogger.LogWarning($"Unknown data type ({dataType}) - filePathOs: {filePathOs}. stack-trace: {GetStackTrace()}");
                            break;
                    }

                    returnMessage.SuccessLog.Add("Successfully created CMS Metadata Entry");
                }
                catch (Exception exMetadataSetError)
                {
                    logMessage = $"Could not create metadata for file with ref: {fileReference}, currentProjectId: {currentProjectId}";
                    appLogger.LogError(exMetadataSetError, logMessage);
                    returnMessage.WarningLog.Add(logMessage);
                }


            }
            catch (Exception xmlLoadException)
            {
                logMessage = $"Could not load data file with ref: {fileReference}, currentProjectId: {currentProjectId}";
                appLogger.LogError(xmlLoadException, logMessage);
                returnMessage.WarningLog.Add(logMessage);
            }

            xmlCmsMetadataContent.AppendChild(nodeContent);

            if (returnMessage.ErrorLog.Count > 0)
            {
                returnMessage.Success = false;
                returnMessage.Message = "Unable to render CMS Metadata content node";
            }
            else
            {
                returnMessage.Success = true;
                returnMessage.Message = "Successfully rendered CMS Metadata content node";
                returnMessage.XmlPayload = xmlCmsMetadataContent;
            }

            return returnMessage;
        }

        /// <summary>
        /// Create the filing section metadata entry XmlElement
        /// </summary>
        /// <param name="nodeMetadata"></param>
        /// <param name="projectId"></param>
        /// <param name="xmlCmsMetadata"></param>
        /// <param name="xmlSectionData"></param>
        /// <param name="dataReference"></param>
        /// <param name="xmlHierarchyOverview"></param>
        private static void _createSectionDataMetadataNodeContent(ref XmlElement nodeMetadata, string projectId, XmlDocument xmlCmsMetadata, XmlDocument xmlSectionData, string dataReference, XmlDocument xmlHierarchyOverview)
        {
            // Dates
            var dateCreated = RetrieveNodeValueIfExists("/data/system/date_created", xmlSectionData);
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "datecreated", dateCreated));
            var dateModified = RetrieveNodeValueIfExists("/data/system/date_created", xmlSectionData);
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "datemodified", dateModified));
            // Article information
            var nodeListArticles = xmlSectionData.SelectNodes("/data/content/*");
            var articleType = GetAttribute(nodeListArticles.Item(0), "data-articletype");
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "articletype", articleType));
            var languages = new List<string>();
            var articleIds = new List<string>();
            var articleTitles = new List<string>();
            var reportingRequirements = new Dictionary<string, string>();
            var contentStatusIndicators = new Dictionary<string, string>(); ;
            foreach (XmlNode nodeArticle in nodeListArticles)
            {
                var contentLang = GetAttribute(nodeArticle.ParentNode, "lang") ?? "";
                languages.Add(contentLang);
                articleIds.Add(nodeArticle.GetAttribute("id") ?? "");

                var nodeHeader = RetrieveFirstHeaderNode(nodeArticle, false, true);
                if (nodeHeader != null)
                {
                    // Remove comment wrappers
                    RemoveCommentWrappers(ref nodeHeader);

                    // Remove unwanted elements from the header so that it can safely be used elsewhere
                    var nodeListBreaks = nodeHeader.SelectNodes("br");
                    if (nodeListBreaks.Count > 0) RemoveXmlNodes(nodeListBreaks);

                    var nodeListFootnoteReferences = nodeHeader.SelectNodes("sup[@class='fn']");
                    if (nodeListFootnoteReferences.Count > 0) RemoveXmlNodes(nodeListFootnoteReferences);

                }
                var articleTitle = (nodeHeader == null) ? "" : nodeHeader.InnerXml;
                articleTitles.Add(articleTitle);

                var reportingRequirement = nodeArticle.GetAttribute("data-reportingrequirements");
                if (!string.IsNullOrEmpty(reportingRequirement)) reportingRequirements.Add(contentLang, reportingRequirement);

                var contentStatus = nodeArticle.GetAttribute("data-contentstatus") ?? "";
                if (!string.IsNullOrEmpty(contentStatus)) contentStatusIndicators.Add(contentLang, contentStatus);
            }
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "languages", string.Join(",", languages)));
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "ids", string.Join(",", articleIds)));
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "titles", string.Join("||||", articleTitles)));

            foreach (var contentStatusIndicator in contentStatusIndicators)
            {
                nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, $"contentstatus-{contentStatusIndicator.Key}", contentStatusIndicator.Value));
            }

            foreach (var reportingRequirement in reportingRequirements)
            {
                nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, $"reportingrequirements-{reportingRequirement.Key}", reportingRequirement.Value));
            }

            // Is this section XML file in use or not
            var fileInUse = (xmlHierarchyOverview.SelectNodes($"/hierarchies/output_channel/items//item[@data-ref='{dataReference}']").Count > 0) ? "true" : "false";
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "inuse", fileInUse));


            // Does the section have graphs tx-renderedchart
            var nodeListGraphSourceTables = xmlSectionData.SelectNodes($"//article{RetrieveGraphElementsBaseXpath()}");
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "hasgraphs", (nodeListGraphSourceTables.Count > 0).ToString().ToLower()));

            // Does the section contain drawings
            var nodeListSvgSources = xmlSectionData.SelectNodes($"//article//{RetrieveSvgElementsBaseXpath()}");
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "hasdrawings", (nodeListSvgSources.Count > 0).ToString().ToLower()));

            // Does the section contain images
            var nodeListImages = xmlSectionData.SelectNodes("//article//img");
            nodeMetadata.AppendChild(_CreateEntryNode(xmlCmsMetadata, "hasimages", (nodeListImages.Count > 0).ToString().ToLower()));

        }


        /// <summary>
        /// Retrieves the metadata of content for a CMS project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RetrieveCmsMetadata(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");
            var returnErrorIfProjectNotFound = false;

            var projectId = request.RetrievePostedValue("projectid", RegexEnum.None, true, ReturnTypeEnum.Xml);

            //
            // => Initially attempt to use the pre-generated version of the metadata which is in-memory
            //
            if (projectId == "all")
            {
                await response.OK(GenerateSuccessXml("Successfully retrieved full content metadata list", $"projectId: {projectId}", HttpUtility.HtmlEncode(XmlCmsContentMetadata.OuterXml)), ReturnTypeEnum.Xml, true);
            }
            else
            {
                // Filter the data for the project that we need it for
                var nodeContent = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']");
                if (nodeContent != null)
                {
                    await response.OK(GenerateSuccessXml("Successfully retrieved content metadata list", $"projectId: {projectId}", HttpUtility.HtmlEncode(RenderXmlContentToReturn(nodeContent))), ReturnTypeEnum.Xml, true);
                }
                else
                {
                    //
                    // => If we cannot find the metadata in-memory, then we need to compile it on-the-fly
                    //
                    appLogger.LogInformation("Creating metadata on-the-fly");
                    var xmlContentMetadataProject = CompileCmsMetadata(projectId, false);

                    nodeContent = xmlContentMetadataProject.SelectSingleNode($"/projects/cms_project[@id='{projectId}']");
                    if (nodeContent != null)
                    {
                        await response.OK(GenerateSuccessXml("Successfully retrieved content metadata list", $"projectId: {projectId}", HttpUtility.HtmlEncode(RenderXmlContentToReturn(nodeContent))), ReturnTypeEnum.Xml, true);
                    }
                    else
                    {
                        if (returnErrorIfProjectNotFound)
                        {
                            HandleError(ReturnTypeEnum.Xml, "Unable to retrieve metadata for this project", $"projectId: {projectId}, stack-trace: {GetStackTrace()}");
                        }
                        else
                        {
                            await response.OK(GenerateSuccessXml("No metadata found for this project", $"projectId: {projectId}", HttpUtility.HtmlEncode($"<projects><cms_project id=\"{projectId}\"/></projects>")), ReturnTypeEnum.Xml, true);
                        }
                    }
                }
            }

            string RenderXmlContentToReturn(XmlNode nodeContent)
            {
                // Retrieve the attributes set on the project node
                var xmlDoc = nodeContent.OwnerDocument;

                var xmlDocToReturn = new XmlDocument();
                var nodeProjectToReturn = xmlDocToReturn.CreateElement("projects");
                nodeProjectToReturn.SetAttribute("succes-count", xmlDoc.DocumentElement.GetAttribute("succes-count") ?? "");
                nodeProjectToReturn.SetAttribute("failure-count", xmlDoc.DocumentElement.GetAttribute("failure-count") ?? "");

                nodeProjectToReturn.AppendChild(xmlDocToReturn.ImportNode(nodeContent, true));

                xmlDocToReturn.AppendChild(nodeProjectToReturn);

                return xmlDocToReturn.OuterXml;
            }
        }



        /// <summary>
        /// Generic handler for editing metadata in the CMS content
        /// (currently only used for storing the reporting requirements in the source XML data)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task SetCmsMetadata(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            await DummyAwaiter();

            var metadataValue = request.RetrievePostedValue("scheme", RegexEnum.None, false, ReturnTypeEnum.Json, null) ?? request.RetrievePostedValue("value", RegexEnum.None, false, ReturnTypeEnum.Json, null);
            var action = request.RetrievePostedValue("action", RegexEnum.None, true, ReturnTypeEnum.Json);
            var metadataType = request.RetrievePostedValue("metadatatype", RegexEnum.None, true, ReturnTypeEnum.Json);
            var includeChildSections = request.RetrievePostedValue("includechildsections", RegexEnum.None, true, ReturnTypeEnum.Json)?.ToLower();

            var basicDebugString = $"metadataValue: {metadataValue}, action: {action}, includeChildSections: {includeChildSections}, type: {metadataType}";

            var sectionIds = new List<string>();
            sectionIds.Add(projectVars.did);
            if (includeChildSections == "true")
            {
                var xmlHierarchy = RenderFilingHierarchy(reqVars, projectVars);
                var nodeListItems = xmlHierarchy.SelectNodes($"//item[@id='{projectVars.did}']//item");
                foreach (XmlNode nodeItem in nodeListItems)
                {
                    var itemId = nodeItem.GetAttribute("id");
                    if (!string.IsNullOrEmpty(itemId))
                    {
                        if (!sectionIds.Contains(itemId))
                        {
                            sectionIds.Add(itemId);
                        }
                        else
                        {
                            appLogger.LogError($"Hierarchy ID already exists in the list - ${itemId}");
                        }
                    }
                }
            }

            basicDebugString += $", sectionIds: {string.Join(',', sectionIds.ToArray())}";

            foreach (string sectionId in sectionIds)
            {
                // Calculate the base path to the xml file that contains the section XML file
                var xmlSectionPathOs = RetrieveInlineFilingComposerXmlPathOs(reqVars, projectVars, sectionId, debugRoutine);

                if (!File.Exists(xmlSectionPathOs)) HandleError(ReturnTypeEnum.Xml, "Could not find filing data file", $"xmlSectionPathOs: {xmlSectionPathOs}, {basicDebugString}, stack-trace: {GetStackTrace()}", 424);

                try
                {
                    var xmlSection = new XmlDocument();
                    xmlSection.Load(xmlSectionPathOs);

                    var xPath = $"/data/content[@lang='{projectVars.outputChannelVariantLanguage}']/*";
                    var nodeArticle = xmlSection.SelectSingleNode(xPath);
                    if (nodeArticle == null) HandleError("Could not find article to set the metadata for.", $"xmlSectionPathOs: {xmlSectionPathOs}, xPath: {xPath}, {basicDebugString}, stack-trace: {GetStackTrace()}");

                    // Determine attribute value
                    var attributeName = "";
                    switch (metadataType)
                    {
                        case "reportingrequirement":
                            attributeName = "data-reportingrequirements";
                            break;

                        case "contentstatus":
                            attributeName = "data-contentstatus";
                            break;

                        default:
                            HandleError("Unknown metadata type", $"xmlSectionPathOs: {xmlSectionPathOs}, {basicDebugString}, stack-trace: {GetStackTrace()}");
                            break;
                    }

                    // Set the data-* attribute on the article node
                    switch (metadataType)
                    {
                        case "reportingrequirement":
                            var existingReportingRequirements = GetAttribute(nodeArticle, attributeName);
                            if (string.IsNullOrEmpty(existingReportingRequirements))
                            {
                                switch (action)
                                {
                                    case "set":
                                        var newReportingRequirementsString = modifyReportingRequirementSetting(existingReportingRequirements);
                                        if (string.IsNullOrEmpty(newReportingRequirementsString))
                                        {
                                            RemoveAttribute(nodeArticle, attributeName);
                                        }
                                        else
                                        {
                                            SetAttribute(nodeArticle, attributeName, newReportingRequirementsString);
                                        }
                                        break;

                                    case "add":
                                        SetAttribute(nodeArticle, attributeName, metadataValue);
                                        break;

                                    default:
                                        HandleError("Metadata action not supported", basicDebugString);
                                        break;
                                }
                            }
                            else
                            {
                                switch (action)
                                {
                                    case "set":
                                        if (string.IsNullOrEmpty(metadataValue))
                                        {
                                            RemoveAttribute(nodeArticle, attributeName);
                                        }
                                        else
                                        {
                                            var newReportingRequirementsString = modifyReportingRequirementSetting(existingReportingRequirements);
                                            if (string.IsNullOrEmpty(newReportingRequirementsString))
                                            {
                                                RemoveAttribute(nodeArticle, attributeName);
                                            }
                                            else
                                            {
                                                SetAttribute(nodeArticle, attributeName, newReportingRequirementsString);
                                            }
                                        }

                                        break;

                                    case "add":
                                    case "remove":
                                        var reportingRequirementSchemes = existingReportingRequirements.Split(',').ToList();
                                        if (action == "add")
                                        {
                                            if (!reportingRequirementSchemes.Contains(metadataValue)) reportingRequirementSchemes.Add(metadataValue);
                                        }
                                        else
                                        {
                                            if (reportingRequirementSchemes.Contains(metadataValue)) reportingRequirementSchemes.Remove(metadataValue);
                                        }
                                        SetAttribute(nodeArticle, attributeName, string.Join(",", reportingRequirementSchemes));
                                        break;

                                    default:
                                        HandleError("Metadata action not supported", basicDebugString);
                                        break;
                                }

                            }

                            // Save the filing data
                            xmlSection.Save(xmlSectionPathOs);
                            break;

                        case "contentstatus":
                            switch (action)
                            {
                                case "set":
                                case "add":
                                    if (string.IsNullOrEmpty(metadataValue) || metadataValue == "notset")
                                    {
                                        RemoveAttribute(nodeArticle, attributeName);
                                    }
                                    else
                                    {
                                        SetAttribute(nodeArticle, attributeName, metadataValue);
                                    }
                                    break;

                                case "remove":
                                    RemoveAttribute(nodeArticle, attributeName);
                                    break;

                                default:
                                    HandleError("Metadata action not supported", basicDebugString);
                                    break;
                            }
                            // Save the filing data
                            xmlSection.Save(xmlSectionPathOs);

                            break;


                        default:
                            HandleError("Action for metadata not defined yet", $"xmlSectionPathOs: {xmlSectionPathOs}, {basicDebugString}, stack-trace: {GetStackTrace()}");
                            break;

                    }

                    // Retrieve all the hierarchies defined for this project
                    var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectVars.projectId, false);

                    // Retrieve information about this section content item from the hierarchy and use this for defining the commit message
                    var itemInformation = RetrieveHierarchyItemInformation(xmlHierarchyOverview, projectVars, sectionId);


                    // HandleError("Stopped on purpose");
                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "metadataupdatereportingrequirements"; // Update
                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = ((string.IsNullOrEmpty(itemInformation.Linkname)) ? "" : itemInformation.Linkname); // Linkname
                    xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", projectVars.outputChannelVariantLanguage ?? "");
                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = itemInformation.Id; // Page ID
                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                    // Commit the result in the GIT repository
                    CommitFilingData(message, ReturnTypeEnum.Xml, false);


                }
                catch (Exception ex)
                {
                    HandleError("Something went wrong while trying to edit the metadata", $"error: {ex}, xmlSectionPathOs: {xmlSectionPathOs}, {basicDebugString}, stack-trace: {GetStackTrace()}");
                }
            }

            // Update the metadata information
            await UpdateCmsMetadata(projectVars.projectId);

            // Render the response
            await response.OK(GenerateSuccessXml("Successfully changed metadata for the filing content data", basicDebugString), ReturnTypeEnum.Xml, true);

            /// <summary>
            /// Helper routine to render a new reporting requirement string
            /// </summary>
            /// <param name="existingReportingRequirements"></param>
            /// <returns></returns>
            string modifyReportingRequirementSetting(string existingReportingRequirements)
            {
                var newReportingRequirements = new List<string>();

                if (!string.IsNullOrEmpty(existingReportingRequirements))
                {
                    var currentReportingRequirements = existingReportingRequirements.Split(',');
                    foreach (var currentReportingRequirement in currentReportingRequirements)
                    {
                        if (!metadataValue.Contains($"{currentReportingRequirement}|")) newReportingRequirements.Add(currentReportingRequirement);
                    }
                }

                // Parse the information we have received
                var reportingRequirementElements = metadataValue.Split(',');
                foreach (var reportingRequirementElement in reportingRequirementElements)
                {
                    var tmp = reportingRequirementElement.Split('|');
                    if (tmp.Length == 2)
                    {
                        var requirement = tmp[0];
                        var selected = (tmp[1] == "true");
                        if (!newReportingRequirements.Contains(requirement) && selected) newReportingRequirements.Add(requirement);
                    }
                    else
                    {
                        appLogger.LogError($"Invalid format for {reportingRequirementElement} detected. stack-trace: {GetStackTrace()}");
                    }
                }

                return string.Join(',', newReportingRequirements.ToArray());
            }
        }

        /// <summary>
        /// Renders a metadata entry node
        /// </summary>
        /// <param name="xmlCmsMetadata"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static XmlNode _CreateEntryNode(XmlDocument xmlCmsMetadata, string key, string value)
        {
            var nodeEntryDateCreated = xmlCmsMetadata.CreateElement("entry");
            SetAttribute(nodeEntryDateCreated, "key", key);
            nodeEntryDateCreated.InnerText = value ?? "";
            return nodeEntryDateCreated;
        }



    }
}