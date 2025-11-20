using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities involved in loading XML data into the editor
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders the url for the editor to retrieve the data to edit
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="dataType"></param>
        /// <param name="pageId"></param>
        /// <returns></returns>
        public static string RenderDataRetrievalUrl(string projectId, string versionId, string dataType, string pageId)
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var id = "";

            //determine the page id to serve as a base url
            switch (dataType)
            {
                case "text":
                    id = "u-cms_data-retriever";
                    break;
                case "config":
                    id = "u-cms_config-retriever";
                    break;
                case "schema":
                    id = "u-cms_schema-retriever";
                    break;
                default:
                    id = "u-cms_data-retriever";
                    break;
            }

            var strBaseUrl = RetrieveUrlFromHierarchy(id, xmlHierarchy);
            
            var queryBuilder = new QueryParamBuilder()
                .Add("pid", HttpUtility.UrlEncode(projectId))
                .Add("vid", HttpUtility.UrlEncode(versionId))
                .Add("type", HttpUtility.UrlEncode(dataType))
                .Add("did", HttpUtility.UrlEncode(pageId))
                .Add("ctype", HttpUtility.UrlEncode(projectVars.editorContentType))
                .Add("rtype", HttpUtility.UrlEncode(projectVars.reportTypeId));

            if (!string.IsNullOrEmpty(projectVars.outputChannelType))
            {
                queryBuilder.Add("octype", HttpUtility.UrlEncode(projectVars.outputChannelType));
            }
            if (!string.IsNullOrEmpty(projectVars.outputChannelVariantId))
            {
                queryBuilder.Add("ocvariantid", HttpUtility.UrlEncode(projectVars.outputChannelVariantId));
            }
            if (!string.IsNullOrEmpty(projectVars.outputChannelVariantLanguage))
            {
                queryBuilder.Add("oclang", HttpUtility.UrlEncode(projectVars.outputChannelVariantLanguage));
            }

            // Add random string to bypass any possible caching
            queryBuilder.Add("rnd", HttpUtility.UrlEncode(GenerateRandomString(30, true)));
            
            return strBaseUrl + "?" + queryBuilder.ToString().Replace("&", "&amp;");
        }

        /// <summary>
        /// Renders the XML data received from the Taxxor Data Store to the web browser
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task WriteRetrievedFilingComposerXmlData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var errorMessage = "";
            var errorDebugInfo = "";
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted variables
            var projectId = request.RetrievePostedValue("pid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Xml);
            var versionId = request.RetrievePostedValue("vid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Xml);
            var dataType = request.RetrievePostedValue("type", "text");
            var did = request.RetrievePostedValue("did", RegexEnum.Loose, true, ReturnTypeEnum.Xml);
            var target = request.RetrievePostedValue("target", RegexEnum.Strict, false, ReturnTypeEnum.Xml, "unknown");
            projectVars.editorContentType = request.RetrievePostedValue("ctype", RegexEnum.Loose, true, ReturnTypeEnum.Xml);
            projectVars.reportTypeId = request.RetrievePostedValue("rtype", RegexEnum.Loose, true, ReturnTypeEnum.Xml);

            //
            // => Check locking system
            //
            if (target == "taxxoreditor")
            {
                // - Is this section locked by another user?
                var pageType = "filing"; // filing | page 

                // Retrieve the lock information
                var xmlLock = new XmlDocument();
                xmlLock = FilingLockStore.RetrieveLockForPageId(projectVars, did, pageType);
                string userIdLock = xmlLock?.SelectSingleNode("/lock/userid")?.InnerText ?? "";
                // if (debugRoutine)
                // {
                //     Console.WriteLine("&&&&&&&&&&&& Lock Info &&&&&&&&&&&&");
                //     Console.WriteLine(xmlLock.OuterXml);
                //     Console.WriteLine($"Current user id: {projectVars.currentUser.Id}");
                //     Console.WriteLine($"Lock user id: {userIdLock}");
                //     Console.WriteLine($"Section id: {did}");
                //     Console.WriteLine("&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&");
                // }

                if (!string.IsNullOrEmpty(userIdLock) && userIdLock != projectVars.currentUser.Id)
                {
                    errorMessage = "Unable to open this section for editing as it's locked by someone else.";
                    errorDebugInfo = $"currentUserId: {projectVars.currentUser.Id}, userIdLock: {userIdLock}";
                    appLogger.LogWarning($"{errorMessage} {errorDebugInfo}");
                    HandleError(errorMessage, errorDebugInfo);
                }


                // - Lock this section
                var createLockResult = FilingLockStore.AddLock(projectVars, did, pageType);
                if (!createLockResult.Success)
                {
                    HandleError("Unable to create lock for this content", createLockResult.DebugInfo);
                }
                else
                {
                    // Update all connected clients with the new lock information
                    // try
                    // {
                    //     var hubContext = context.RequestServices.GetRequiredService<IHubContext<WebSocketsHub>>();
                    //     var listLocksResult = ListLocks(projectVars, pageType);
                    //     await hubContext.Clients.All.SendAsync("UpdateSectionLocks", listLocksResult);
                    // }
                    // catch (Exception ex)
                    // {
                    //     appLogger.LogError(ex, "Could not update all connected clients with the new lock information using SignalR");
                    // }
                }
            }


            // HandleError("Thrown on purpose");

            XmlDocument xmlFilingContent = new XmlDocument();

            switch (dataType)
            {
                case "text":
                    xmlFilingContent = await RetrieveFilingComposerXmlData(projectId, projectVars.editorId, versionId, did, debugRoutine);
                    break;

                default:
                    HandleError(ReturnTypeEnum.Xml, "Data type not supported yet", $"dataType: {dataType}, stack-trace: {GetStackTrace()}");
                    break;
            }

            if (dataType == "text" && projectVars.outputChannelType == "website")
            {
                // For the website output channel we first check if we can write the content using an extension method
                var writtenContentUsingCustomMethod = await Extensions.WriteWebPageEditorContent(context, xmlFilingContent, did);

                // If not, then we will write the content to the output stream here
                if (!writtenContentUsingCustomMethod) await context.Response.OK(xmlFilingContent, ReturnTypeEnum.Xml, true);
            }
            else
            {
                await context.Response.OK(xmlFilingContent, ReturnTypeEnum.Xml, true);
            }

        }

        /// <summary>
        /// Retrieves the filing editor XML data from the Taxxor Data Store
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="did"></param>
        /// <param name="forceError"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RetrieveFilingComposerXmlData(ProjectVariables projectVars, string did, bool forceError, bool debugRoutine = false)
        {
            return await RetrieveFilingComposerXmlData(projectVars, projectVars.projectId, projectVars.editorId, projectVars.versionId, did, debugRoutine, forceError);
        }

        /// <summary>
        /// Retrieves the filing editor XML data from the Taxxor Data Store
        /// Utility function that allows to debug the data that the system receives
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="editorId"></param>
        /// <param name="versionId"></param>
        /// <param name="did"></param>
        /// <param name="dataType"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RetrieveFilingComposerXmlData(string projectId, string editorId, string versionId, string did, bool debugRoutine, bool forceError = true)
        {
            ProjectVariables projectVars = RetrieveProjectVariables(System.Web.Context.Current);

            return await RetrieveFilingComposerXmlData(projectVars, projectId, editorId, versionId, did, debugRoutine, forceError);
        }


        /// <summary>
        /// Retrieves the filing editor XML data from the Taxxor Data Store
        /// Utility function that allows to debug the data that the system receives
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="projectId"></param>
        /// <param name="editorId"></param>
        /// <param name="versionId"></param>
        /// <param name="did"></param>
        /// <param name="debugRoutine"></param>
        /// <param name="forceError"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RetrieveFilingComposerXmlData(ProjectVariables projectVars, string projectId, string editorId, string versionId, string did, bool debugRoutine, bool forceError = true)
        {

            var xmlFilingContentSourceData = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadSourceData(projectVars, projectId, did, "latest", debugRoutine);

            if (XmlContainsError(xmlFilingContentSourceData))
            {
                if (forceError)
                {
                    HandleError(ReturnTypeEnum.Xml, xmlFilingContentSourceData);
                }
                else
                {
                    return xmlFilingContentSourceData;
                }
            }

            // Dump the data on the disk so we can use it for inspection
            if (siteType == "local" || siteType == "dev" || siteType == "prev")
            {
                var debugFolderOs = logRootPathOs + "/debug/editor/" + projectId;
                var debugFileName = "_load-filingcomposercontent-" + projectVars.editorContentType + "-" + projectVars.reportTypeId + "_" + did + ".xml";

                try
                {
                    if (!Directory.Exists(debugFolderOs)) Directory.CreateDirectory(debugFolderOs);
                    await xmlFilingContentSourceData.SaveAsync(debugFolderOs + "/" + debugFileName, false);
                }
                catch (Exception ex)
                {
                    WriteErrorMessageToConsole("There was an error writing debug information to the disk", ex.ToString());
                }
            }

            switch (editorId)
            {
                case "default_editor":
                    // Translate the XML we have just received


                    break;

                default:
                    XsltArgumentList xsltArgumentList = new();
                    // TODO: Fix something here for dealing with multiple languages in the same editing page (for translation purposes) - 'all'
                    xsltArgumentList.AddParam("lang", "", projectVars.outputChannelVariantLanguage);

                    // /assets/xsl/data-load.xsl
                    var folderPathOs = $"{applicationRootPathOs}/frontend/public/report_editors/default";

                    var xpathForApplicationConfiguration = "/configuration/locations/location[@id='cms_xsl_filingcomposer-data-load']";
                    var xsltPathOs = folderPathOs + xmlApplicationConfiguration.SelectSingleNode(xpathForApplicationConfiguration)?.InnerText ?? "";

                    if (File.Exists(xsltPathOs))
                    {
                        xmlFilingContentSourceData = TransformXmlToDocument(xmlFilingContentSourceData, xsltPathOs, xsltArgumentList);

                        if (projectVars.outputChannelType == "website")
                        {
                            xmlFilingContentSourceData = Extensions.RenderWebPageEditorContent(System.Web.Context.Current, xmlFilingContentSourceData);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"ERROR: Could not find XSLT file at {xsltPathOs}");
                        if (forceError)
                        {
                            HandleError(ReturnTypeEnum.Xml, "Could not find XSLT file", $"xsltPathOs: {xsltPathOs}");
                        }
                        else
                        {
                            appLogger.LogError($"Could not find XSLT file at {xsltPathOs}");
                        }
                    }

                    // Append the filing section ID to the content that we stream to the client as a temporary attribute
                    xmlFilingContentSourceData.DocumentElement.SetAttribute("data-did", did);

                    break;
            }

            return xmlFilingContentSourceData;
        }

    }
}