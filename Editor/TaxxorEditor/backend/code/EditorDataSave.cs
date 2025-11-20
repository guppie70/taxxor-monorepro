using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities involved in saving editor data to the server
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Load XML data into the editor (text, config or other XML data)
        /// </summary>
        public static async Task SaveFilingComposerData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Global variables
            var xpath = "";

            // Retrieve posted variables
            var projectId = request.RetrievePostedValue("pid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
            var versionId = request.RetrievePostedValue("vid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
            var dataType = request.RetrievePostedValue("type", "text");
            var postedId = request.RetrievePostedValue("did", RegexEnum.Loose, true, ReturnTypeEnum.Json);
            var contentLanguage = context.Request.RetrievePostedValue("oclang", @"^\w{2}$", false, ReturnTypeEnum.Json, "en");
            var target = request.RetrievePostedValue("target", RegexEnum.Strict, false, ReturnTypeEnum.Xml, "unknown");
            projectVars.editorContentType = request.RetrievePostedValue("ctype", RegexEnum.Loose, true, ReturnTypeEnum.Json);
            projectVars.reportTypeId = request.RetrievePostedValue("rtype", RegexEnum.Loose, true, ReturnTypeEnum.Json);
            projectVars.outputChannelType = request.RetrievePostedValue("octype", RegexEnum.Loose, true, ReturnTypeEnum.Json);
            projectVars.outputChannelVariantId = request.RetrievePostedValue("ocvariantid", RegexEnum.Loose, true, ReturnTypeEnum.Json);


            var filingSectionId = "";
            var xmlFilingContentSourceData = new XmlDocument();


            var errorMessage = "";
            var errorDebugInfo = "";

            //Console.WriteLine($"** Received XML: {xmlDoc.ToString()}");

            if (ValidateCmsPostedParameters(projectId, versionId, dataType))
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                XmlDocument responseXml = new XmlDocument();
                XmlDocument? xmlHierarchyDoc = null;

                //
                // => Check locking system
                //
                if (target == "taxxoreditor")
                {
                    // - Is this section locked by another user?
                    var pageType = "filing"; // filing | page 

                    // Retrieve the lock information
                    var xmlLock = new XmlDocument();
                    xmlLock = FilingLockStore.RetrieveLockForPageId(projectVars, postedId, pageType);
                    string userIdLock = xmlLock?.SelectSingleNode("/lock/userid")?.InnerText ?? "";
                    // if (debugRoutine)
                    // {
                    //     Console.WriteLine("&&&&&&&&&&&& Lock Info &&&&&&&&&&&&");
                    //     Console.WriteLine(xmlLock.OuterXml);
                    //     Console.WriteLine($"Existing locks:\n{PrettyPrintXml(FilingLockStore.ListLocks())}");
                    //     Console.WriteLine($"Current user id: {projectVars.currentUser.Id}");
                    //     Console.WriteLine($"Lock user id: {userIdLock}");
                    //     Console.WriteLine($"Section id: {postedId}");
                    //     Console.WriteLine("&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&");
                    // }

                    if (string.IsNullOrEmpty(userIdLock))
                    {
                        appLogger.LogWarning($"Unexpected request to save section data with ID {postedId} in project {projectVars.projectId} by user {projectVars.currentUser.Id} while the section is not locked by anybody");
                    }
                    else if (userIdLock != projectVars.currentUser.Id)
                    {
                        errorMessage = "Unable to store this section content for editing as the lock information is incorrect";
                        errorDebugInfo = $"projectVars.currentUser.Id: {projectVars.currentUser.Id}, userIdLock: {userIdLock}";
                        HandleError(ReturnTypeEnum.Json, errorMessage, errorDebugInfo);
                    }
                }

                //
                // => Retrieve output channel hierarchy
                //
                var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);

                if (!projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                {
                    appLogger.LogInformation("Retrieving metadata from the server");
                    var hierarchyRetrieveResult = await RetrieveOutputChannelHierarchiesMetaData(projectVars, reqVars, true);
                    if (!hierarchyRetrieveResult) HandleError(ReturnTypeEnum.Json, "Unable to store this section because of missing hierarchy information", $"projectVars.currentUser.Id: {projectVars.currentUser.Id}");
                }

                if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                {
                    // - Retrieve the output channel hierarchy
                    xmlHierarchyDoc = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;
                }
                else
                {
                    HandleError(ReturnTypeEnum.Json, "Unable to retrieve the outputchannel hierarchy", $"projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}']=null, stack-trace: {GetStackTrace()}");
                }

                //
                // => Check permissions: is this user allowed to save data
                //

                // - Render a special version of the hierarchy that we can use to generate a breadcrumbtrail that we can send to the access control service
                var xmlDocumentForPermissionsCheck = CreateHierarchyForRbacPermissionsRetrieval(reqVars.xmlHierarchy, xmlHierarchyDoc);

                // - Find the section that we want to save in this special hierarchy
                var nodeItem = xmlDocumentForPermissionsCheck.SelectSingleNode($"/items/structured//item[@id={GenerateEscapedXPathString(postedId)}]");
                if (nodeItem != null)
                {
                    // - Retrieve the permissions and test if this user is allowed to edit the section that we are about to save
                    var permissions = await AccessControlService.RetrievePermissions(RequestMethodEnum.Get, projectId, nodeItem, true, debugRoutine);
                    // Console.WriteLine($"!!!!! User permissions: {string.Join(",",permissions.Permissions.ToArray())} !!!!!");
                    if (!permissions.HasPermission("editcontent"))
                    {
                        HandleError(ReturnTypeEnum.Json, "Not allowed", $"postedId: {postedId}, stack-trace: {GetStackTrace()}", 403);
                    }
                }
                else
                {
                    HandleError(ReturnTypeEnum.Json, "Unable to locate section in the outputchannel hierarchy", $"postedId: {postedId}, stack-trace: {GetStackTrace()}");
                }


                //
                // => Continue to save the data
                //
                var dataReference = xmlHierarchyDoc.SelectSingleNode($"/items/structured//item[@id={GenerateEscapedXPathString(postedId)}]")?.GetAttribute("data-ref") ?? "unknown";


                // Parse the content as multipart form data
                var postedMultipartData = context.Request.ReadFormAsync();

                // Loop through the posted elements
                foreach (var pair in postedMultipartData.Result)
                {
                    string fieldName = pair.Key;
                    string postedData = pair.Value.ToString();
                    // Console.WriteLine($"{fieldName}: {postedData}");

                    // Convert the data posted into XML
                    var xmlPostedData = new XmlDocument();

                    if (fieldName == "html")
                    {
                        try
                        {
                            if (debugRoutine)
                            {
                                await TextFileCreateAsync(postedData, $"{logRootPathOs}/--editor-saved.html");
                            }

                            // Deal with special case where a user typed in something like <lorem ipsum solar mid> and avoid that is being translated into XHTML
                            postedData = Regex.Replace(
                                postedData,
                                @"(&lt;)((\w+)(?: [^&]+)?)(&gt;)",
                                "$1 $2 $4",
                                RegexOptions.Multiline
                            );

                            xmlPostedData.LoadHtml(postedData, true);

                            // Execute some special logic for saving website content
                            if (projectVars.outputChannelType == "website")
                            {
                                // Pass the XHTML through a stylesheet so that we can transform it's content
                                var transformedXhtml = Extensions.SaveWebPageEditorContent(context, xmlPostedData, "", contentLanguage);
                                xmlPostedData.LoadXml(transformedXhtml.OuterXml);
                                if (XmlContainsError(xmlPostedData)) HandleError("There was an error saving your content", xmlPostedData.SelectSingleNode("//debuginfo").InnerText);
                            }



                            if (debugRoutine)
                            {
                                await xmlPostedData.SaveAsync($"{logRootPathOs}/--editor-saved.xml", false);
                            }

                            // HandleError("Thrown on purpose", "");

                            filingSectionId = GetAttribute(xmlPostedData.DocumentElement, "data-did");

                            // Log a warning when the posted ID does not match with the ID found in the content data that was posted
                            if (filingSectionId != postedId)
                            {
                                appLogger.LogWarning($"ID mismatch... filingSectionId: {filingSectionId}, postedId: {postedId}, stack-trace: {GetStackTrace()}");
                                HandleError(ReturnTypeEnum.Json, "Could not save the content because a matching identifier is missing", $"filingSectionId: {filingSectionId}, postedId: {postedId}");
                            }

                            // Remove the temporary attributes that we have set in the loading routine
                            RemoveAttribute(xmlPostedData.DocumentElement, "data-did");
                            RemoveAttribute(xmlPostedData.DocumentElement, "data-hierarchical-level");
                            RemoveAttribute(xmlPostedData.DocumentElement, "data-tocnumber");

                            // Remove the cursor marker
                            var nodeListCursorMarkers = xmlPostedData.SelectNodes("//span[@class='cursor-marker']");
                            if (nodeListCursorMarkers.Count > 0)
                            {
                                RemoveXmlNodes(nodeListCursorMarkers);
                            }

                            // Cleanup constructs that the editor may have left in the content, but is not valid XHTML
                            xmlPostedData = TransformXmlToDocument(xmlPostedData, "cms_save-cleanup");

                            // Ensure that the ID's in the content are unique
                            EnsureUniqueIds(ref xmlPostedData);

                            // Prevent XSS injections
                            if (!projectVars.outputChannelVariantId.Contains("website"))
                            {
                                RemoveXssSensitiveElements(ref xmlPostedData);
                            }

                            // Load the original XHTML source from the Taxxor Document Store
                            xmlFilingContentSourceData = await DocumentStoreService.FilingData.LoadSourceData(projectId, filingSectionId, "latest", debugRoutine);

                            if (XmlContainsError(xmlFilingContentSourceData))
                            {
                                HandleError(ReturnTypeEnum.Json, xmlFilingContentSourceData);
                            }

                            if (debugRoutine) await xmlPostedData.SaveAsync(logRootPathOs + "/---saved-content.xml", false);

                            // Get an ISO timestamp
                            var currentTimestamp = createIsoTimestamp();

                            // Update the modified date in saved xml content
                            SetAttribute(xmlPostedData.DocumentElement, "data-last-modified", currentTimestamp);
                            SetAttribute(xmlPostedData.DocumentElement, "modified-by", projectVars.currentUser.Id);

                            // Lookup the root node that we need to add the information to
                            xpath = $"/data/content[@lang={GenerateEscapedXPathString(contentLanguage)}]";
                            var nodeTarget = xmlFilingContentSourceData.SelectSingleNode(xpath);
                            if (nodeTarget == null)
                            {
                                HandleError(ReturnTypeEnum.Json, "Could not locate target node to put the posted data of the editor in", $"xpath: {xpath}, stack-trace: {GetStackTrace()}");
                            }

                            // First remove all the nodes
                            foreach (XmlNode nodeToRemove in nodeTarget.SelectNodes("*"))
                            {
                                RemoveXmlNode(nodeToRemove);
                            }

                            // Check if the data we are about to save is valid XHTML (and exclude specific datareferences)
                            var nodeListDataReferencesToExclude = xmlApplicationConfiguration.SelectNodes($"/configuration/general/schemachecker/cms_project[@report-type='{projectVars.reportTypeId}']/exclude/dataref[@name='{dataReference}']");
                            if (nodeListDataReferencesToExclude.Count > 0)
                            {
                                var xhtmlValidationResult = await ValidateXhtml(xmlPostedData, true);
                                if (!xhtmlValidationResult.Success)
                                {

                                    appLogger.LogWarning($"!!!!! XHTML validation error while saving content !!!!!\nprojectId: {projectVars.projectId}, data-ref: {dataReference}, language: {projectVars.outputChannelVariantLanguage}\n" + xhtmlValidationResult.Message + "\n" + string.Join("\n", xhtmlValidationResult.ErrorLog) + "\n!!!!");

                                    // Add the validation error details to the list so that we can process these errors later
                                    var validationErrorDetails = new ValidationErrorDetails(projectVars.projectId, dataReference, contentLanguage, projectVars.currentUser.Id, xhtmlValidationResult.ErrorLog);
                                    if (!validationErrorDetailsList.Any(v => v.ProjectId == projectVars.projectId && v.DataReference == dataReference && v.Lang == contentLanguage))
                                    {
                                        validationErrorDetailsList.Add(validationErrorDetails);
                                    }
                                }
                            }

                            // Add the saved content in the original content
                            var importedNode = xmlFilingContentSourceData.ImportNode(xmlPostedData.DocumentElement, true);
                            nodeTarget.AppendChild(importedNode);

                            // Set modified date in the metadata header
                            var nodeDateModified = xmlFilingContentSourceData.SelectSingleNode("/data/system/date_modified");
                            if (nodeDateModified == null)
                            {
                                appLogger.LogWarning("Could not find date modified node in the system section");
                            }
                            else
                            {
                                nodeDateModified.InnerText = currentTimestamp;
                            }

                            // Pretty print the result
                            xmlFilingContentSourceData.LoadXml(PrettyPrintXml(xmlFilingContentSourceData.OuterXml));

                            // Assure that SDE's will always have a value
                            var nodeListEmptySdes = xmlFilingContentSourceData.SelectNodes("//span[@data-fact-id][not(*)][not(normalize-space())]");
                            if (nodeListEmptySdes.Count > 0 && debugRoutine)
                            {
                                appLogger.LogWarning($"!!!!!!!!!!!!! There are empty SDE's in the XML file: {nodeListEmptySdes.Count} !!!!!!!!!!!!!!!");
                            }
                            foreach (XmlNode nodeEmptySde in nodeListEmptySdes)
                            {
                                nodeEmptySde.InnerText = " ";
                            }


                            // Store some logging information
                            if (debugRoutine) await xmlFilingContentSourceData.SaveAsync(logRootPathOs + "/---new-content.xml");
                        }
                        catch (Exception ex)
                        {
                            errorMessage = "Unable to store the content because we could not parse saved data";
                            errorDebugInfo = $"projectId: {projectId}, postedId: {postedId}, userId: {projectVars.currentUser.Id}";

                            // Store the data on the server so that we can potentially use it again
                            await TextFileCreateAsync(postedData, $"{logRootPathOs}/--editordatasaveerror-{projectVars.projectId}-{postedId}-{ToUnixTime(DateTime.Now)}.html");

                            appLogger.LogError(ex, $"{errorMessage}. {errorDebugInfo}");
                            HandleError(ReturnTypeEnum.Json, errorMessage, $"error: {ex}, {errorDebugInfo}, stack-trace: {GetStackTrace()}");
                        }

                    }
                    else
                    {
                        HandleError(ReturnTypeEnum.Json, "Not implemented yet", $"stack-trace: {GetStackTrace()}");
                    }

                }

                //
                // => Clear the paged media cache
                //
                ContentCache.Clear(projectId, projectVars.outputChannelVariantId, postedId);
                // - execute for parents as well
                nodeItem = xmlHierarchyDoc.SelectSingleNode($"/items/structured//item[@id={GenerateEscapedXPathString(postedId)}]");
                var nodeListParentItems = nodeItem.SelectNodes("ancestor::item[parent::sub_items]");
                foreach (XmlNode nodeParentItem in nodeListParentItems)
                {
                    ContentCache.Clear(projectId, projectVars.outputChannelVariantId, nodeParentItem.GetAttribute("id"));
                }

                //
                // => Save the content on the Project Data Store
                //
                responseXml = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.SaveSourceData(xmlFilingContentSourceData, filingSectionId, contentLanguage, debugRoutine);

                // Handle error message that might return from the Taxxor Document Store
                if (XmlContainsError(responseXml)) HandleError(ReturnTypeEnum.Json, responseXml);


                //
                // => Optionally re-render graph renditions for this section
                //
                var nodeListGraphContentWrappers = xmlFilingContentSourceData.SelectNodes($"//article{RetrieveGraphElementsBaseXpath()}");
                if (nodeListGraphContentWrappers.Count > 0)
                {
                    var sectionIds = new List<string>
                    {
                        postedId
                    };
                    var dataRefs = new List<string> {
                        dataReference
                    };
                    var graphRenditionsUpdateResult = await GenerateGraphRenditions(reqVars, projectVars, null, dataRefs);

                    if (!graphRenditionsUpdateResult.Success)
                    {
                        HandleError(ReturnTypeEnum.Json, "Unable to update the graph renditions", graphRenditionsUpdateResult.ToString());
                    }
                    else
                    {
                        appLogger.LogInformation(graphRenditionsUpdateResult.ToString());
                    }
                }

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


                // Handle success
                await context.Response.OK(GenerateSuccessXml("Successfully stored the data", responseXml.SelectSingleNode("/result/debuginfo").InnerText), ReturnTypeEnum.Json, true);
            }
            else
            {
                HandleError(ReturnTypeEnum.Json, GenerateErrorXml("Could not validate posted data", $"projectId: {projectId}, versionId: {versionId}, dataType: {dataType}, stack-trace: {GetStackTrace()}"));
            }

            /// <summary>
            /// Function that scans throuhh the posted data and ensures that all elements have a unique ID.
            /// </summary>
            /// <param name="doc"></param>
            static void EnsureUniqueIds(ref XmlDocument doc)
            {
                // Dictionary to track existing IDs
                Dictionary<string, XmlElement> idMap = new();
                // Queue to track elements that need a unique ID
                Queue<XmlElement> elementsToFix = new();

                // Traverse all elements and check IDs
                foreach (XmlElement element in doc.DocumentElement.GetElementsByTagName("*"))
                {
                    if (element.HasAttribute("id"))
                    {
                        string id = element.GetAttribute("id");
                        if (string.IsNullOrEmpty(id))
                        {
                            // Inject an id value starting with "tx" for elements with an empty id
                            id = "tx-" + Guid.NewGuid().ToString("N");
                            element.SetAttribute("id", id);
                        }

                        // Exclude <span/> elements with class "footnote" from the unique ID check
                        if (element.Name == "span" && (element.GetAttribute("class") ?? "").Contains("footnote"))
                        {
                            continue;
                        }

                        if (idMap.ContainsKey(id))
                        {
                            // If ID is already used, add to elementsToFix queue
                            elementsToFix.Enqueue(element);
                        }
                        else
                        {
                            // If ID is unique, add to idMap
                            idMap[id] = element;
                        }
                    }
                }

                // Fix IDs for elements in elementsToFix queue
                int uniqueSuffix = new Random().Next(1, 100000);
                while (elementsToFix.Count > 0)
                {
                    XmlElement element = elementsToFix.Dequeue();
                    string id = element.GetAttribute("id");
                    string newId;
                    int suffix = 1;
                    do
                    {
                        newId = id + "-" + suffix++;
                    } while (idMap.ContainsKey(newId));

                    element.SetAttribute("id", newId + uniqueSuffix);
                    idMap[newId] = element;
                }
            }


        }

    }
}