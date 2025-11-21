using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves the latest version of the content that is being used in the editor
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task CloneSection(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var originalSectionId = request.RetrievePostedValue("originalsectionid", RegexEnum.Default, true, reqVars.returnType);
            var clonedSectionName = request.RetrievePostedValue("clonedsectionname", RegexEnum.Default, true, reqVars.returnType);
            var cloneSdesString = request.RetrievePostedValue("clonesdes", @"^(true|false)$", true, reqVars.returnType);
            var cloneSdes = (cloneSdesString == "true");
            var includeChildrenString = request.RetrievePostedValue("includechildren", @"^(true|false)$", true, reqVars.returnType);
            var includeChildren = (includeChildrenString == "true");
            var mappingSearch = request.RetrievePostedValue("mappingsearch", RegexEnum.Default, false, reqVars.returnType);
            var mappingReplace = request.RetrievePostedValue("mappingreplace", RegexEnum.Default, false, reqVars.returnType);
            var executeMappingReplacement = (!string.IsNullOrEmpty(mappingSearch) && !string.IsNullOrEmpty(mappingReplace));

            var baseDebugInfo = $"originalSectionId: {originalSectionId}, cloneSdes: {cloneSdes.ToString().ToLower()}, includeChildren: {includeChildren.ToString().ToLower()}";
            if (executeMappingReplacement) baseDebugInfo += $", mappingSearch: {mappingSearch}, mappingReplace: {mappingReplace}";

            var cloneSectionResult = new TaxxorReturnMessage(true, "Successfully started the section clone process", baseDebugInfo);
            await response.OK(cloneSectionResult, ReturnTypeEnum.Json, true);
            await response.CompleteAsync();

            var xPath = "";
            List<string> existingDataReferences;

            try
            {

                await MessageToCurrentClient("SectionCloneProgress", "Retrieving hierarchy and content information");
                //
                // => Retrieve the metadata cache of all the files used in this project
                //
                existingDataReferences = await RetrieveDataReferences(projectVars.projectId);
                var dataReferenceReplace = new Dictionary<string, ClonedItemProperties>();

                //
                // => Retrieve hierarchy
                //
                var xmlHierarchy = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadHierarchy(projectVars, debugRoutine);

                //
                // => Find source sections and calculate the new datareference file names
                //

                var dataReferences = new List<string>();
                xPath = $"//item[@id={GenerateEscapedXPathString(originalSectionId)}]";
                var nodeOriginalItem = xmlHierarchy.SelectSingleNode(xPath);
                if (nodeOriginalItem == null) throw new Exception($"Could not locate section item in hierarchy.{((debugRoutine) ? $"xPath: {xPath}, {baseDebugInfo}" : "")}");

                var dataReference = nodeOriginalItem.GetAttribute("data-ref");
                if (!string.IsNullOrEmpty(dataReference) && !dataReferences.Contains(dataReference)) dataReferences.Add(dataReference);
                var newDataReference = CalculateDataReferenceCloneName(dataReference, clonedSectionName);
                var newSectionId = Path.GetFileNameWithoutExtension(newDataReference) + "-" + RandomString(8);
                ClonedItemProperties itemProperties = new ClonedItemProperties(originalSectionId, dataReference, newSectionId, newDataReference, clonedSectionName);
                dataReferenceReplace.Add(originalSectionId, itemProperties);

                var hasSubItems = nodeOriginalItem.SelectSingleNode("sub_items") != null;
                if (includeChildren && hasSubItems)
                {
                    var nodeListSubItems = nodeOriginalItem.SelectNodes("sub_items//item");
                    foreach (XmlNode nodeSubItem in nodeListSubItems)
                    {
                        var linkName = nodeSubItem.SelectSingleNode("web_page/linkname")?.InnerText ?? "";
                        originalSectionId = nodeSubItem.GetAttribute("id");
                        dataReference = nodeSubItem.GetAttribute("data-ref");
                        if (!string.IsNullOrEmpty(originalSectionId) && !string.IsNullOrEmpty(dataReference))
                        {
                            if (!dataReferenceReplace.ContainsKey(originalSectionId))
                            {
                                newDataReference = CalculateDataReferenceCloneName(dataReference);
                                newSectionId = Path.GetFileNameWithoutExtension(newDataReference) + "-" + RandomString(8);

                                itemProperties = new ClonedItemProperties(originalSectionId, dataReference, newSectionId, newDataReference, $"{linkName} (Cloned)");
                                dataReferenceReplace.Add(originalSectionId, itemProperties);
                            }
                            if (!dataReferences.Contains(dataReference)) dataReferences.Add(dataReference);
                        }
                    }
                }



                //
                // => Clone the section XML documents
                //
                var failedContentLoad = 0;
                var failedContentSave = 0;
                await MessageToCurrentClient("SectionCloneProgress", "Cloning section content");
                foreach (var pair in dataReferenceReplace)
                {
                    originalSectionId = pair.Key;
                    itemProperties = pair.Value;
                    var originalDataReference = itemProperties.OriginalReference;
                    var cloneDataReference = itemProperties.CloneReference;
                    var cloneLinkName = itemProperties.CloneLinkName;

                    if (debugRoutine) appLogger.LogInformation($"Loading section data with reference: '{originalDataReference}'");
                    var xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{originalDataReference}", "cmsdataroot", debugRoutine, false);
                    if (XmlContainsError(xmlFilingContentSourceData))
                    {
                        appLogger.LogError($"{originalDataReference} failed to load with error: {xmlFilingContentSourceData.OuterXml}");
                        failedContentLoad++;
                        continue;
                    }

                    // Set the linkname as the title for the cloned document
                    var nodeListArticles = xmlFilingContentSourceData.SelectNodes("//article");
                    foreach (XmlNode nodeArticle in nodeListArticles)
                    {
                        // Update the article ID's
                        var updatedArticleId = Path.GetFileNameWithoutExtension(cloneDataReference);
                        nodeArticle.SetAttribute("id", updatedArticleId);
                        if (!string.IsNullOrEmpty(nodeArticle.GetAttribute("data-fact-id"))) nodeArticle.SetAttribute("data-fact-id", updatedArticleId);
                        if (!string.IsNullOrEmpty(nodeArticle.GetAttribute("data-guid"))) nodeArticle.SetAttribute("data-guid", updatedArticleId);

                        // Update the header in the page
                        var nodeHeader = RetrieveFirstHeaderNode(nodeArticle);
                        if (nodeHeader != null) nodeHeader.InnerText = cloneLinkName;
                    }

                    // Potentially cleanup content in elements
                    var nodeListToCleanup = xmlFilingContentSourceData.SelectNodes("//*[@data-cleanup='true']");
                    foreach (XmlNode nodeCleanup in nodeListToCleanup)
                    {
                        nodeCleanup.InnerXml = "";
                    }

                    if (debugRoutine) appLogger.LogInformation($"Storing section data with reference: '{cloneDataReference}'");
                    var saveResult = await DocumentStoreService.FilingData.Save<XmlDocument>(xmlFilingContentSourceData, $"/textual/{cloneDataReference}", "cmsdataroot", debugRoutine, false);
                    if (XmlContainsError(saveResult))
                    {
                        appLogger.LogError($"{cloneDataReference} failed to save with error: {saveResult.OuterXml}");
                        failedContentSave++;
                        continue;
                    }
                }

                if (failedContentLoad > 0 || failedContentSave > 0)
                {
                    throw new Exception($"Section content clone failed. failed load: {failedContentLoad}, failed save {failedContentSave}");
                }

                //
                // => Duplicate the structured data elements
                //
                if (cloneSdes)
                {
                    failedContentLoad = 0;
                    failedContentSave = 0;
                    var cloneSdeProblems = 0;
                    foreach (var pair in dataReferenceReplace)
                    {
                        originalSectionId = pair.Key;
                        itemProperties = pair.Value;
                        var cloneDataReference = itemProperties.CloneReference;

                        await MessageToCurrentClient("SectionCloneProgress", $"Cloning structured data for {itemProperties.CloneReference}");

                        //
                        // => Load the cloned data reference
                        //
                        if (debugRoutine) appLogger.LogInformation($"Loading section data with reference: '{cloneDataReference}'");
                        var xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{cloneDataReference}", "cmsdataroot", debugRoutine, false);
                        if (XmlContainsError(xmlFilingContentSourceData))
                        {
                            appLogger.LogError($"{cloneDataReference} failed to load with error: {xmlFilingContentSourceData.OuterXml}");
                            failedContentLoad++;
                            continue;
                        }

                        var nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlFilingContentSourceData, false);

                        //
                        // => Generate a list of fact id's in the content
                        //
                        var factIds = new List<string>();
                        foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                        {
                            var factId = nodeStructuredDataElement.GetAttribute("data-fact-id").Replace("\n", "");
                            if (!string.IsNullOrEmpty(factId))
                            {
                                if (!factIds.Contains(factId)) factIds.Add(factId);
                            }
                            else
                            {
                                cloneSdeProblems++;
                            }
                        }

                        //
                        // => Retrieve the mapping information for all those elements
                        //
                        var xmlMappingInformation = await Taxxor.ConnectedServices.MappingService.RetrieveMappingInformation(factIds, projectVars.projectId, false);

                        // Maps current fact id to the new one - we use this to replace the factid's later on in the XML
                        var factIdMapping = new Dictionary<string, string>();
                        if (!XmlContainsError(xmlMappingInformation))
                        {

                            var nodeListMappingClusters = xmlMappingInformation.SelectNodes("/mappingClusters/mappingCluster");
                            foreach (XmlNode nodeMappingCluster in nodeListMappingClusters)
                            {

                                //
                                // => Create a clone of the mapping cluster and store as a new one
                                //
                                var originalFactId = nodeMappingCluster.GetAttribute("requestId");
                                var newFactId = "";

                                // - prepare a new mapping cluster to store in the database
                                var xmlMappingToPost = new XmlDocument();
                                xmlMappingToPost.ReplaceContent(nodeMappingCluster);
                                xmlMappingToPost.DocumentElement.RemoveAttribute("requestId");
                                xmlMappingToPost.DocumentElement.RemoveAttribute("id");
                                xmlMappingToPost.DocumentElement.SetAttribute("projectId", projectVars.projectId);

                                var nodeListEntries = xmlMappingToPost.SelectNodes("//entry");
                                foreach (XmlNode nodeEntry in nodeListEntries)
                                {
                                    nodeEntry.RemoveAttribute("id");

                                    var mappingScheme = nodeEntry.GetAttribute("scheme")?.ToLower() ?? "";
                                    if (mappingScheme == "internal")
                                    {
                                        var nodeMapping = nodeEntry.SelectSingleNode("mapping");
                                        if (nodeMapping != null) RemoveXmlNode(nodeMapping);
                                    }

                                    if (mappingScheme == "efr" && executeMappingReplacement)
                                    {
                                        var nodeMapping = nodeEntry.SelectSingleNode("mapping");
                                        if (nodeMapping != null)
                                        {
                                            nodeMapping.InnerText = nodeMapping.InnerText.Replace(mappingSearch, mappingReplace);
                                            string? customReplaceResult = Extensions.CustomMappingClusterReplacements(nodeMapping.InnerText, mappingSearch, mappingReplace);
                                            if (!string.IsNullOrEmpty(customReplaceResult)) nodeMapping.InnerText = customReplaceResult;
                                        }
                                    }
                                }

                                // Console.WriteLine("*** ORIGINAL ***");
                                // Console.WriteLine(PrettyPrintXml(xmlMappingToPost));
                                // Console.WriteLine("****************");

                                var xmlMappingServiceResult = await MappingService.StoreMappingEntry(xmlMappingToPost);

                                if (!XmlContainsError(xmlMappingServiceResult))
                                {
                                    // Console.WriteLine("*** RESPONSE ***");
                                    // Console.WriteLine(PrettyPrintXml(xmlMappingServiceResult));
                                    // Console.WriteLine("****************");
                                    // Console.WriteLine("");

                                    newFactId = xmlMappingServiceResult.SelectSingleNode("//mapping|//string")?.InnerText ?? "";
                                }
                                else
                                {
                                    appLogger.LogError($"Failed to store mapping information for factId {originalFactId} in {cloneDataReference} with error: {xmlMappingServiceResult.OuterXml}");
                                    cloneSdeProblems++;
                                    continue;
                                }

                                if (!string.IsNullOrEmpty(originalFactId) && !string.IsNullOrEmpty(newFactId))
                                {
                                    if (!factIdMapping.ContainsKey(originalFactId)) factIdMapping.Add(originalFactId, newFactId);
                                }
                                else
                                {
                                    appLogger.LogError($"Cannot fill replacement dictionary for {cloneDataReference}. originalFactId: {originalFactId}, newFactId: {newFactId}");
                                    cloneSdeProblems++;
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            appLogger.LogError($"Failed to retrieve mapping information for {cloneDataReference} failed to save with error: {xmlMappingInformation.OuterXml}");
                            cloneSdeProblems++;
                            continue;
                        }

                        //
                        // => Replace the mappings in the XML document with the new factId's
                        //
                        foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                        {
                            var originalFactId = nodeStructuredDataElement.GetAttribute("data-fact-id").Replace("\n", "");
                            if (factIdMapping.ContainsKey(originalFactId))
                            {
                                var newFactId = factIdMapping[originalFactId];
                                nodeStructuredDataElement.SetAttribute("data-fact-id", newFactId);
                            }
                            else
                            {
                                appLogger.LogError($"Failed to to retrieve facctid mapping for {originalFactId} in {cloneDataReference}");
                                cloneSdeProblems++;
                                continue;
                            }
                        }

                        //
                        // => Save the updated section content file
                        //
                        await MessageToCurrentClient("SectionCloneProgress", "Storing updated section content");
                        if (debugRoutine) appLogger.LogInformation($"Storing section data with reference: '{cloneDataReference}'");
                        var saveResult = await DocumentStoreService.FilingData.Save<XmlDocument>(xmlFilingContentSourceData, $"/textual/{cloneDataReference}", "cmsdataroot", debugRoutine, false);
                        if (XmlContainsError(saveResult))
                        {
                            appLogger.LogError($"{cloneDataReference} failed to save with error: {saveResult.OuterXml}");
                            failedContentSave++;
                            continue;
                        }

                        //
                        // => Generate SDE cache
                        //   
                        await MessageToCurrentClient("SectionCloneProgress", "Generating structured data cache");
                        var dataToPost = new Dictionary<string, string>();
                        dataToPost.Add("projectid", projectVars.projectId);
                        dataToPost.Add("snapshotid", "1");
                        dataToPost.Add("dataref", cloneDataReference);

                        var xmlSyncResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorsyncstructureddatasnapshotperdataref", dataToPost, debugRoutine);
                        if (XmlContainsError(xmlSyncResponse))
                        {
                            appLogger.LogError($"Failed to create a new SDE cache entry for {cloneDataReference}");
                            cloneSdeProblems++;
                            continue;
                        }

                    }

                    switch ((failedContentLoad, failedContentSave, cloneSdeProblems))
                    {
                        case ValueTuple<int, int, int> t when t.Item1 > 0 || t.Item2 > 0:
                            throw new Exception($"Structured data elements clone failed. failed clone: {cloneSdeProblems}, failed load: {failedContentLoad}, failed save {failedContentSave}");

                        case ValueTuple<int, int, int> t when t.Item3 > 0:
                            await MessageToCurrentClient("SectionCloneProgress", $"Warning: {cloneSdeProblems} structured data elements failed to clone");
                            break;
                    }

                    // if (failedContentLoad > 0 || failedContentSave > 0 || cloneSdeProblems > 0)
                    // {
                    //     throw new Exception($"Structured data elements clone failed. failed clone: {cloneSdeProblems}, failed load: {failedContentLoad}, failed save {failedContentSave}");
                    // }
                }

                //
                // => Add the elements in the hierarchy tree
                //
                var xmlSubTree = new XmlDocument();
                xmlSubTree.ReplaceContent(nodeOriginalItem);

                // - remove the sub items from the tree if needed
                if (hasSubItems && !includeChildren)
                {
                    var nodeSubItems = xmlSubTree.DocumentElement.SelectSingleNode("sub_items");
                    if (nodeSubItems != null) RemoveXmlNode(nodeSubItems);
                }

                // - replace the information
                var nodeListSubTreeItems = xmlSubTree.SelectNodes("//item");
                foreach (XmlNode nodeSubTreeItem in nodeListSubTreeItems)
                {
                    var originalItemId = nodeSubTreeItem.GetAttribute("id");
                    if (!string.IsNullOrEmpty(originalItemId) && dataReferenceReplace.ContainsKey(originalItemId))
                    {

                        itemProperties = dataReferenceReplace[originalItemId];

                        nodeSubTreeItem.SetAttribute("id", itemProperties.CloneId);
                        nodeSubTreeItem.SetAttribute("data-ref", itemProperties.CloneReference);
                        nodeSubTreeItem.SelectSingleNode("web_page/linkname").InnerText = itemProperties.CloneLinkName;
                    }
                    else
                    {
                        var errorMessage = $"Could not locate replacement information for {originalItemId}";
                        appLogger.LogError(errorMessage);
                        throw new Exception(errorMessage);
                    }
                }

                // - inject the content into the hierarcy, underneath the original item
                var nodeImported = xmlHierarchy.ImportNode(xmlSubTree.DocumentElement, true);
                var nodeInserted = nodeOriginalItem.ParentNode.InsertAfter(nodeImported, nodeOriginalItem);

                // - save the hierarchy
                XmlDocument xmlSaveResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.SaveHierarchy(projectVars, xmlHierarchy, false, true);

                if (XmlContainsError(xmlSaveResult))
                {
                    var errorMessage = $"Could not save the updated hierarchy";
                    appLogger.LogError(errorMessage);
                    throw new Exception(errorMessage);
                }

                // - clear the cache entry
                var hierarchyMetadataId = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                if (string.IsNullOrEmpty(hierarchyMetadataId)) HandleError("Could not find metadata id for hierarchy", $"stack-trace: {GetStackTrace()}");
                projectVars.rbacCache.ClearHierarchy(hierarchyMetadataId);
                // projectVars.rbacCache.ClearAll();


                //
                // => Update the local CMS metadata object
                //
                if (debugRoutine)
                {
                    var contentHashBefore = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectVars.projectId}']")?.GetAttribute("contentupdatedhash") ?? "unknown";
                    await UpdateCmsMetadata(projectVars.projectId);
                    var contentHashAfter = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectVars.projectId}']")?.GetAttribute("contentupdatedhash") ?? "unknown";
                    appLogger.LogInformation($"Successfully updated the local version of the XML CMS Metadata object (contentHashBefore: {contentHashBefore}, contentHashAfter: {contentHashAfter})");
                }
                else
                {
                    await UpdateCmsMetadata(projectVars.projectId);
                }


                //
                // => Commit the changes in the version management system
                //

                // - retrieve the content from the replacement dictionary
                itemProperties = dataReferenceReplace[originalSectionId];
                XmlDocument? xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "sectioncontentclone"; // Update
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = itemProperties.CloneLinkName;
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = itemProperties.CloneId;
                var message = xmlCommitMessage.DocumentElement.InnerXml;

                // Execute the commit request
                var commitResult = await Taxxor.ConnectedServices.DocumentStoreService.VersionControl.GitCommit(projectVars.projectId, "reportdataroot", message, debugRoutine);
                if (!commitResult.Success)
                {
                    appLogger.LogWarning($"There was an issue commiting the content in the version control system. {commitResult.Message}, debuginfo: {commitResult.DebugInfo}");
                }

                // Return success message
                await MessageToCurrentClient("SectionCloneDone", "Successfully cloned the section(s)");
            }
            catch (Exception ex)
            {
                var errorResponse = new TaxxorReturnMessage(false, "Cloning process failed", $"error: {ex}, {baseDebugInfo}");
                await MessageToCurrentClient("SectionCloneDone", errorResponse);
            }

            /// <summary>
            /// Calculates a new unique data reference name
            /// </summary>
            /// <param name="dataRef"></param>
            /// <param name="sectionName"></param>
            /// <returns></returns>
            string CalculateDataReferenceCloneName(string dataRef, string sectionName = null)
            {
                if (sectionName != null)
                {
                    dataRef = NormalizeFileName($"{sectionName}.xml");
                }

                // Calculate new datareference name
                var clonedDataRef = "";
                var calculatedUniqueDataReference = false;
                var counter = 0;
                while (!calculatedUniqueDataReference)
                {
                    var calculatedDataRef = "";
                    if (counter == 0)
                    {
                        calculatedDataRef = Path.GetFileNameWithoutExtension(dataRef) + Path.GetExtension(dataRef);
                    }
                    else
                    {
                        calculatedDataRef = Path.GetFileNameWithoutExtension(dataRef) + $"-{counter.ToString()}" + Path.GetExtension(dataRef);
                    }

                    if (!existingDataReferences.Contains(calculatedDataRef))
                    {
                        clonedDataRef = calculatedDataRef;
                        existingDataReferences.Add(clonedDataRef);
                        calculatedUniqueDataReference = true;
                    }
                    counter++;
                }
                return clonedDataRef;
            }

        }


        /// <summary>
        /// Helper class to capture sitestructure item properties
        /// </summary>
        public class ClonedItemProperties
        {
            public string OriginalId = null;
            public string OriginalReference = null;
            public string CloneId = null;
            public string CloneReference = null;
            public string CloneLinkName = null;

            public ClonedItemProperties(string originalId, string originalReference, string cloneId, string cloneReference, string cloneLinkName)
            {
                this.OriginalId = originalId;
                this.OriginalReference = originalReference;
                this.CloneId = cloneId;
                this.CloneReference = cloneReference;
                this.CloneLinkName = cloneLinkName;
            }
        }

    }

}