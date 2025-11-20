using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Find and replace text content in all the data files of this project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FindReplace(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var searchFragment = request.RetrievePostedValue("searchfragment", RegexEnum.TextArea, true, reqVars.returnType);
            var replaceFragment = request.RetrievePostedValue("replacefragment", RegexEnum.TextArea, false, reqVars.returnType) ?? "";
            var outputChannelLanguage = request.RetrievePostedValue("oclang", RegexEnum.None, false, reqVars.returnType, "en");
            var dryRunString = request.RetrievePostedValue("dryrun", "(true|false)", false, reqVars.returnType, "false");
            var dryRun = (dryRunString == "true");
            var onlyInUseString = request.RetrievePostedValue("onlyinuse", RegexEnum.None, false, reqVars.returnType, "false");
            bool onlyInUse = (onlyInUseString == "true");
            var includeFootnotesString = request.RetrievePostedValue("includefootnotes", "(true|false)", false, reqVars.returnType, "false");
            var includeFootnotes = (includeFootnotesString == "true");

            var errorText = "";
            var totalDataReferencesSearched = 0;
            var totalFound = 0;
            var totalReplace = 0;

            var xmlFindReplaceResult = new XmlDocument();
            xmlFindReplaceResult.LoadXml("<findreplace></findreplace>");

            // Retrieve all the hierarchies defined for this project
            var xmlHierarchyOverview = RenderOutputChannelHierarchyOverview(projectVars.projectId, false);

            // Retrieve detailed information about the content data used in this project
            var xmlContentMetadata = CompileCmsMetadata(projectVars.projectId);
            xmlContentMetadata.Save($"{logRootPathOs}/_findandreplace-content-metadata.xml");

            try
            {
                // Retrieve the base path where we can find the content files
                var nodeProjectRoot = xmlContentMetadata.SelectSingleNode("/projects/cms_project");
                var nodeDataRoot = nodeProjectRoot.SelectSingleNode("data");
                if (nodeProjectRoot == null || nodeDataRoot == null)
                {
                    // Return an error
                    await response.Error(GenerateErrorXml(
                        "Could not locate the information in the metadata file",
                        $"stack-trace: {GetStackTrace()}"
                    ), reqVars.returnType, true);
                }
                else
                {
                    var dataFolderPathOs = GetAttribute(nodeProjectRoot, "datalocation");

                    if (Directory.Exists(dataFolderPathOs))
                    {

                        // Find the content items that this routine needs to work with
                        XmlNodeList nodeListContent = null;
                        var xPath = "";
                        if (onlyInUse)
                        {
                            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
                            var editorContentType = "regular";
                            var outputChannelType = "pdf";
                            var reportTypeId = RetrieveReportTypeIdFromProjectId(projectVars.projectId);
                            var outputChannelVariantId = RetrieveFirstOutputChannelVariantIdFromEditorId(editorId, outputChannelType, projectVars.outputChannelVariantLanguage);

                            var xmlHierarchy = RenderFilingHierarchy(reqVars, projectVars.projectId, "1", editorId, editorContentType, reportTypeId, outputChannelType, outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                            xPath = "/items/structured//item";
                            nodeListContent = xmlHierarchy.SelectNodes(xPath);
                        }
                        else
                        {
                            // Use the metadata content to locate the datafiles we need to search in (scope also determined by the "inuse" parameter)
                            var onlyInUseSelector = (onlyInUse) ? " and metadata/entry[@key='inuse']/text()='true'" : "";
                            xPath = $"content[@datatype='sectiondata' and contains(metadata/entry[@key='languages'], '{outputChannelLanguage}'){onlyInUseSelector}]";
                            nodeListContent = nodeDataRoot.SelectNodes(xPath);
                        }

                        foreach (XmlNode nodeContent in nodeListContent)
                        {
                            var contentReference = GetAttribute(nodeContent, ((onlyInUse) ? "data-ref" : "ref"));

                            if (!string.IsNullOrEmpty(contentReference))
                            {
                                // Add to result
                                var nodeResultItem = xmlFindReplaceResult.CreateElement("content");

                                var nodeRef = xmlFindReplaceResult.CreateElement("ref");
                                nodeRef.InnerText = contentReference;
                                nodeResultItem.AppendChild(nodeRef);

                                var dataFilePathOs = $"{dataFolderPathOs}/{contentReference}";
                                if (File.Exists(dataFilePathOs))
                                {
                                    var xmlContentData = new XmlDocument();
                                    xmlContentData.Load(dataFilePathOs);

                                    // Check if we can find the search phrase in the content
                                    xPath = $"/data/content[@lang='{outputChannelLanguage}']/*[local-name()='article' or local-name()='html']";
                                    var nodeContentToSearch = xmlContentData.SelectSingleNode(xPath);
                                    if (nodeContentToSearch != null)
                                    {
                                        var searchXpath = $"//*[text()[contains(., '{searchFragment}')]]";
                                        var nodeListTextNodes = nodeContentToSearch.SelectNodes($"{xPath}{searchXpath}");

                                        // Added for debugging purposes
                                        if (contentReference == "812515-financial-performance.xml---xyz")
                                        {
                                            Console.WriteLine($"- Found {nodeListTextNodes.Count.ToString()} matches for {searchXpath}");
                                        }


                                        var nodeFound = xmlFindReplaceResult.CreateElement("found");
                                        totalFound += nodeListTextNodes.Count;
                                        nodeFound.InnerText = nodeListTextNodes.Count.ToString();
                                        nodeResultItem.AppendChild(nodeFound);

                                        var countReplace = 0;

                                        // Execute the replace
                                        if (!dryRun)
                                        {
                                            foreach (XmlNode nodeText in nodeListTextNodes)
                                            {
                                                nodeText.InnerXml = nodeText.InnerXml.Replace(searchFragment, replaceFragment);
                                                countReplace++;
                                            }
                                        }

                                        // Save the XML document
                                        if (countReplace > 0)
                                        {
                                            xmlContentData.Save(dataFilePathOs);

                                            // Construct the commit message

                                            // - Linkname and site structure id
                                            var itemInformation = RetrieveHierarchyItemInformation(xmlHierarchyOverview, projectVars, contentReference);

                                            XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                                            xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "findreplace";
                                            xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = ((string.IsNullOrEmpty(itemInformation.Linkname)) ? "" : itemInformation.Linkname); // Linkname
                                            xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", outputChannelLanguage);
                                            xmlCommitMessage.SelectSingleNode("/root/id").InnerText = itemInformation.Id; // Page ID
                                            var message = xmlCommitMessage.DocumentElement.InnerXml;

                                            // Commit the result in the GIT repository
                                            CommitFilingData(message, ReturnTypeEnum.Xml, true);
                                        }


                                        var nodeReplace = xmlFindReplaceResult.CreateElement("replace");
                                        totalReplace += countReplace;
                                        nodeReplace.InnerText = countReplace.ToString();
                                        nodeResultItem.AppendChild(nodeReplace);
                                    }
                                    else
                                    {
                                        errorText = $"Could not find content to search in (dataFilePathOs: {dataFilePathOs})";
                                        appLogger.LogWarning(errorText);
                                        var nodeError = xmlFindReplaceResult.CreateElement("error");
                                        nodeError.InnerText = errorText;
                                        nodeResultItem.AppendChild(nodeError);
                                    }
                                    totalDataReferencesSearched++;

                                }
                                else
                                {
                                    appLogger.LogWarning($"Could not find content data file {dataFilePathOs} in FindReplace()");
                                }

                                xmlFindReplaceResult.DocumentElement.AppendChild(nodeResultItem);

                            }
                            else
                            {
                                appLogger.LogWarning("Could not find content reference in FindReplace()");
                            }



                        }

                        // Inject the totals as nodes so that they are easy to convert to JSON
                        var nodeTotalReferencesSearched = xmlFindReplaceResult.CreateElement("references-searched");
                        nodeTotalReferencesSearched.InnerText = totalDataReferencesSearched.ToString();
                        xmlFindReplaceResult.DocumentElement.PrependChild(nodeTotalReferencesSearched);

                        var nodeTotalReplace = xmlFindReplaceResult.CreateElement("replace");
                        nodeTotalReplace.InnerText = totalReplace.ToString();
                        xmlFindReplaceResult.DocumentElement.PrependChild(nodeTotalReplace);

                        var nodeTotalFound = xmlFindReplaceResult.CreateElement("found");
                        nodeTotalFound.InnerText = totalFound.ToString();
                        xmlFindReplaceResult.DocumentElement.PrependChild(nodeTotalFound);

                        xmlFindReplaceResult.Save($"{logRootPathOs}/findreplace-result.xml");

                        // Return an error
                        await response.OK(GenerateSuccessXml(
                            "Successfully replaced content",
                            $"searchFragment: {searchFragment}, replaceFragment: {replaceFragment}, dryRun: {dryRun}, includeFootnotes: {includeFootnotes}",
                            HttpUtility.HtmlEncode(xmlFindReplaceResult.OuterXml)
                        ), reqVars.returnType, true);


                    }
                    else
                    {
                        // Return an error
                        await response.Error(GenerateErrorXml(
                            $"Could not find location of the content data for project with ID {projectVars.projectId}",
                            $"dataFolderPathOs: {dataFolderPathOs}, stack-trace: {GetStackTrace()}"
                        ), reqVars.returnType, true);
                    }

                }


            }
            catch (Exception ex)
            {
                // Return the error
                await response.Error(GenerateErrorXml(
                        "There was an error while searching and replacing text in the data files",
                        $"error: {ex}"),
                    reqVars.returnType, true);
            }

        }

    }

}