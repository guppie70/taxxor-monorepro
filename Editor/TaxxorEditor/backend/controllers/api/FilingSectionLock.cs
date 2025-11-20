using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves details of a lock for the current user or for a specific page id
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveLock(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted variables
            var pageId = request.RetrievePostedValue("pageid", "");
            var pageType = request.RetrievePostedValue("pagetype", "filing"); // filing | page 

            // Retrieve the lock information
            var xmlLock = new XmlDocument();

            if (string.IsNullOrEmpty(pageId))
            {
                xmlLock = FilingLockStore.RetrieveLockForUser(projectVars, pageType);
            }
            else
            {
                xmlLock = FilingLockStore.RetrieveLockForPageId(projectVars, pageId, pageType);
            }

            List<FilingSectionLock> locks = new List<FilingSectionLock>();

            if (XmlContainsError(xmlLock))
            {
                appLogger.LogWarning($"Could not find a lock for this user or pageid (pageId: {pageId}, pageType: {pageType}, userId: {projectVars.currentUser.Id})");
            }
            else
            {
                locks.Add(new FilingSectionLock(xmlLock.SelectSingleNode("/lock/pageids/pageid").InnerText, xmlLock.SelectSingleNode("/lock/userid").InnerText, xmlLock.SelectSingleNode("/lock/username").InnerText));
            }

            // Construct a response message for the client
            dynamic jsonData = new ExpandoObject();
            jsonData.locks = locks;
            // jsonData.message = "Successfully retrieved the locks";
            if (isDevelopmentEnvironment)
            {
                jsonData.debuginfo = $"pageId: {pageId}, pageType: {pageType}, userId: {projectVars.currentUser.Id}";
            }

            string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

            await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
        }

        /// <summary>
        /// Creates a new lock
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task CreateLock(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted variables
            var pageId = request.RetrievePostedValue("pageid", true, ReturnTypeEnum.Json);
            var pageType = request.RetrievePostedValue("pagetype", "filing"); // filing | page 

            // Create the lock
            var addLockResult = FilingLockStore.AddLock(projectVars, pageId, pageType);

            if (!addLockResult.Success)
            {
                await response.Error(addLockResult, ReturnTypeEnum.Json, true);
            }
            else
            {
                // Update all connected clients with the new lock information
                try
                {
                    var hubContext = context.RequestServices.GetRequiredService<IHubContext<WebSocketsHub>>();
                    var listLocksResult = ListLocks(projectVars, pageType);
                    await hubContext.Clients.All.SendAsync("UpdateSectionLocks", listLocksResult);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Could not update all connected clients with the new lock information using SignalR");
                }


                await response.OK(addLockResult, ReturnTypeEnum.Json, true);
            }
        }

        /// <summary>
        /// Removes a lock
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RemoveLock(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            try
            {
                // Access the current HTTP Context by using the custom created service 
                var context = System.Web.Context.Current;

                // Retrieve the request variables object
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                // Retrieve posted variables
                var pageId = request.RetrievePostedValue("pageid", "");
                var pageType = request.RetrievePostedValue("pagetype", "filing"); // filing | page 

                // Console.WriteLine("!!!!!!!!!!! DELETE LOCK !!!!!!!!!!!");
                // Console.WriteLine($"pageId: '{pageId}', pageType: '{pageType}'");
                // Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

                if (string.IsNullOrEmpty(pageId))
                {
                    FilingLockStore.RemoveLocksForUser(projectVars.currentUser.Id, pageType);
                }
                else
                {
                    FilingLockStore.RemoveLockForPage(projectVars, pageId, pageType);
                }

                await context.Response.OK(GenerateSuccessXml("Successfully removed the lock", $"pageId: {pageId}, pageType: {pageType}"), ReturnTypeEnum.Json, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "Could not remove lock", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Updates an existing lock
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task UpdateLock(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            await DummyAwaiter();

            HandleError("Needs to be implemented", $"stack-trace: {GetStackTrace()}");
        }

        /// <summary>
        /// List all the locks available in the system
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ListLocks(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted data
            var pageType = request.RetrievePostedValue("pagetype", "filing"); // filing | page 

            var listLocksResult = ListLocks(projectVars, pageType);

            if (listLocksResult.Success)
            {
                await context.Response.OK(listLocksResult.Payload, ReturnTypeEnum.Json, true);
            }
            else
            {
                await response.Error(listLocksResult, ReturnTypeEnum.Json, true);
            }
        }


        /// <summary>
        /// List the locks
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="pageType"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage ListLocks(ProjectVariables projectVars, string pageType)
        {
            // appLogger.LogDebug($"Retrieving locks for pageType: {pageType}, projectId: {projectVars.projectId}, userId: {projectVars.currentUser.Id} ({GetStackTrace()})");
            
            try
            {
                // Retrieve all the locks
                var xmlLocks = FilingLockStore.ListLocks();
                // appLogger.LogDebug(PrettyPrintXml(xmlLocks.OuterXml));

                // Return the result
                if (XmlContainsError(xmlLocks))
                {
                    return new TaxxorReturnMessage(xmlLocks);
                }
                else
                {
                    // Filter the locks to only return the relevant ones
                    List<FilingSectionLock> locks = new List<FilingSectionLock>();

                    var metadataId = RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);

                    // The locks that we show in the array are all the locks that the users have set EXCEPT for the lock that the current user owns
                    var xPath = $"/locks/lock[@projectId='{projectVars.projectId}' and @type='{pageType}' and not(userid/text()='{projectVars.currentUser.Id}')]/pageids/pageid[@metadata-hierarchy-id='{metadataId}']";
                    var nodeListLockedPageIds = xmlLocks.SelectNodes(xPath);

                    foreach (XmlNode nodeLockedPageId in nodeListLockedPageIds)
                    {
                        var nodeLock = nodeLockedPageId.ParentNode.ParentNode;
                        locks.Add(new FilingSectionLock(nodeLockedPageId.InnerText, nodeLock.SelectSingleNode("userid").InnerText, nodeLock.SelectSingleNode("username").InnerText));
                    }

                    // Construct a response message for the client
                    dynamic jsonData = new ExpandoObject();
                    jsonData.locks = locks;
                    // Counts the total locks set in the project
                    jsonData.count = FilingLockStore.RetrieveNumberOfLocks(projectVars);
                    // jsonData.message = "Successfully retrieved the locks";
                    if (isDevelopmentEnvironment)
                    {
                        jsonData.debuginfo = $"xPath: {xPath}, nodeListLocks.Count: {nodeListLockedPageIds.Count.ToString()}";
                    }

                    string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                    //HandleError("Needs to be implemented", $"xmlAllItems: {HtmlEncodeForDisplay(xmlAllItems.OuterXml)}, xmlStoreResult: {HtmlEncodeForDisplay(html)}, stack-trace: {GetStackTrace()}");

                    return new TaxxorReturnMessage(true, "Successfully generated locks overview", jsonToReturn, "");
                }
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error to retrieving the locks", ex.ToString());
            }

        }

        /// <summary>
        /// Represents a lock for a section or a page in the Taxxor Editor
        /// </summary>
        public class FilingSectionLock
        {
            // Generic properties for the user class
            public string PageId;
            public string UserId;
            public string DisplayName;

            public FilingSectionLock(string pageId, string userId, string displayName)
            {
                this.PageId = pageId;
                this.UserId = userId;
                this.DisplayName = displayName;
            }
        }

    }
}