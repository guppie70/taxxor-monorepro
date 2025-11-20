using System;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Websocket methods that render UI components dynamically
        /// </summary>
        public partial class WebSocketsHub : Hub
        {

            /// <summary>
            /// Stores content status in the section data and optionally the child items
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="outputChannelVariantId"></param>
            /// <param name="itemId"></param>
            /// <param name="workflowStatus"></param>
            /// <param name="includeChildSections"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/managertools")]
            public async Task<TaxxorReturnMessage> SetContentStatus(string projectId, string outputChannelVariantId, string itemId, string workflowStatus, bool includeChildSections)
            {
                var errorMessage = "There was an error storing the content status in the section data";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("outputChannelVariantId", outputChannelVariantId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("itemId", itemId, @"^[a-zA-Z0-9\-_]{2,1024}$", true);
                    inputValidationCollection.Add("workflowStatus", workflowStatus, RegexEnum.Loose, true);
                    inputValidationCollection.Add("includeChildSections", includeChildSections.ToString(), RegexEnum.Boolean, true);

                    //
                    // => Prepare the project variables object so that we can use it in the authorization process
                    //
                    projectVars.Fill(projectId, outputChannelVariantId);


                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    SetProjectVariables(context, projectVars);

                    // throw new Exception($"Thrown on purpose. {projectVars.DumpToString()}");


                    //
                    // => Test if sections within the scope of this request are locked by another user
                    //
                    var numberOfLocks = 0;
                    var sectionsInScopeLocked = false;
                    // - Only test if this particular section is locked by another user
                    sectionsInScopeLocked = FilingLockStore.LockExists(projectVars, itemId, "filing");
                    if (sectionsInScopeLocked) numberOfLocks++;

                    if (!sectionsInScopeLocked && includeChildSections)
                    {
                        // - Find out all the section id's in scope via the output channel hierarchy
                        var xmlOutputChannelHierarchy = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadHierarchy(projectVars.projectId, projectVars.outputChannelVariantId, debugRoutine);

                        var nodeListItems = xmlOutputChannelHierarchy.SelectNodes($"//item[@id={GenerateEscapedXPathString(itemId)}]/sub_items//item");
                        foreach (XmlNode nodeItem in nodeListItems)
                        {
                            var sectionId = nodeItem.GetAttribute("id") ?? "";
                            if (sectionId != "")
                            {
                                var currentSectionLocked = FilingLockStore.LockExists(projectVars, sectionId, "filing");
                                if (!sectionsInScopeLocked && currentSectionLocked) sectionsInScopeLocked = true;

                                if (currentSectionLocked) numberOfLocks++;
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to locate item id to set the filing reporting requirements");
                            }
                        }
                    }

                    if (numberOfLocks > 0)
                    {
                        HandleError($"Could not proceed because there are {numberOfLocks.ToString()} sections locked within the scope of your request. Updating metadata is only available when there are no sections locked for editing.", $"stack-trace: {GetStackTrace()}");
                    }

                    //
                    // => Update the metadata using the API on the Project Data Store
                    //
                    var dataToPost = projectVars.RenderPostDictionary();

                    dataToPost.Add("action", "set");
                    dataToPost.Add("value", workflowStatus);
                    dataToPost.Add("metadatatype", "contentstatus");
                    dataToPost.Add("includechildsections", includeChildSections.ToString());

                    dataToPost["did"] = itemId;

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorfilingmetadata", dataToPost, debugRoutine);


                    if (XmlContainsError(responseXml))
                    {
                        return new TaxxorReturnMessage(responseXml);
                    }
                    else
                    {
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



                        // Construct a response message for the client
                        dynamic jsonData = new ExpandoObject();
                        jsonData.result = new ExpandoObject();
                        jsonData.result.message = $"Successfully set metadata on filing section{((includeChildSections) ? "s" : "")}";
                        if (isDevelopmentEnvironment)
                        {
                            jsonData.result.debuginfo = "You can place debug information here";
                        }

                        // Transform to JSON
                        var json = (string)ConvertToJson(jsonData);

                        // Render the result as a TaxxorReturnMessage
                        return new TaxxorReturnMessage(true, "Successfully set section content status", json, $"itemId: {itemId}");
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }




        }

    }

}