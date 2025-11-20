using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with lock management to avoid multiple users working in the same content
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        public class FilingLockStorage
        {

            private IMemoryCache _memoryCache;

            private int _lockExpirationTime = 5;

            private bool _debugRoutine { get; set; } = false;

            private List<string> _lockEntries { get; set; } = new List<string>();

            public FilingLockStorage(IMemoryCache cache)
            {
                _memoryCache = cache;

                _debugRoutine = (siteType == "local" || siteType == "dev");
                _debugRoutine = false;
            }

            /// <summary>
            /// Generates the XML content of a lock
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="pageId"></param>
            /// <param name="pageType"></param>
            /// <returns></returns>
            private string _generateLockContent(ProjectVariables projectVars, string pageId, string memoryCacheKey, string pageType = "filing")
            {
                var xmlLock = new XmlDocument();

                var nodeLockRoot = xmlLock.CreateElement("lock");
                nodeLockRoot.SetAttribute("id", memoryCacheKey);
                nodeLockRoot.SetAttribute("projectId", projectVars.projectId);
                nodeLockRoot.SetAttribute("type", pageType);

                var nodePageIds = xmlLock.CreateElement("pageids");
                var nodePageId = xmlLock.CreateElementWithText("pageid", pageId);
                nodePageIds.AppendChild(nodePageId);
                nodeLockRoot.AppendChild(nodePageIds);
                nodeLockRoot.AppendChild(xmlLock.CreateElementWithText("userid", projectVars.currentUser.Id));
                nodeLockRoot.AppendChild(xmlLock.CreateElementWithText("username", projectVars.currentUser.DisplayName));

                xmlLock.AppendChild(nodeLockRoot);

                // Console.WriteLine("----------");
                // Console.WriteLine(PrettyPrintXml(xmlLock));
                // Console.WriteLine("----------");

                // Add the id's of the sections in the output channels that have the same data-ref
                if (pageType == "filing")
                {
                    // Remove the page elemement that we have set as a default
                    RemoveXmlNode(nodePageIds.SelectSingleNode("pageid"));

                    string? dataRef = null;
                    foreach (var dictMetadata in projectVars.cmsMetaData)
                    {
                        var metaData = dictMetadata.Value;
                        var xmlOutputChannelHierarchy = metaData.Xml;

                        var filingSectionNode = xmlOutputChannelHierarchy.SelectSingleNode($"//item[@id='{pageId}']");
                        if (filingSectionNode != null)
                        {

                            dataRef = GetAttribute(filingSectionNode, "data-ref");
                            break;
                        }
                    }
                    if (_debugRoutine) appLogger.LogDebug($"dataRef: {dataRef}");

                    if (dataRef != null)
                    {
                        foreach (var dictMetadata in projectVars.cmsMetaData)
                        {

                            var metaData = dictMetadata.Value;
                            var xmlOutputChannelHierarchy = metaData.Xml;

                            var filingSectionNode = xmlOutputChannelHierarchy.SelectSingleNode($"//item[@data-ref='{dataRef}']");
                            if (filingSectionNode != null)
                            {
                                nodePageId = xmlLock.CreateElement("pageid");
                                nodePageId.SetAttribute("data-ref", dataRef);
                                nodePageId.SetAttribute("metadata-hierarchy-id", dictMetadata.Key);
                                nodePageId.InnerText = GetAttribute(filingSectionNode, "id");

                                nodePageIds.AppendChild(nodePageId);
                            }
                        }
                    }
                    else
                    {
                        appLogger.LogWarning($"Could not find data-ref for pageId: {pageId}, projectId: {projectVars.projectId}, userId: {projectVars.currentUser.Id} stack-trace: {GetStackTrace()}");
                    }
                }

                return xmlLock.OuterXml;

            }

            /// <summary>
            /// Checks if a lock already exists for a page id
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="pageId"></param>
            /// <param name="pageType"></param>
            /// <returns></returns>
            public bool LockExists(ProjectVariables projectVars, string pageId, string pageType = "filing")
            {
                var xmlLocks = ListLocks();
                var xPath = $"/locks/lock[@projectId='{projectVars.projectId}' and @type='{pageType}']/pageids/pageid[text()='{pageId}']";
                return (xmlLocks.SelectNodes(xPath).Count > 0);
            }

            /// <summary>
            /// Test if a lock exists for the current user
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="pageType"></param>
            /// <returns></returns>
            public bool LockExists(ProjectVariables projectVars, string pageType = "filing")
            {
                var xmlLocks = ListLocks();
                var xPath = $"/locks/lock[@projectId='{projectVars.projectId}' and @type='{pageType}' and userid/text()='{projectVars.currentUser.Id}']";
                return (xmlLocks.SelectNodes(xPath).Count > 0);
            }

            /// <summary>
            /// Removes a lock from the storage
            /// </summary>
            /// <param name="key"></param>
            private void _removeLock(string key)
            {
                if (!string.IsNullOrEmpty(key) && _lockEntries.Contains(key))
                {
                    _lockEntries.Remove(key);
                    RemoveMemoryCacheItem(key);
                }
                else
                {
                    appLogger.LogError($"Could not remove memory cache key because no valid key ({key ?? "empty"}) was supplied, stack-trace: {GetStackTrace()}");
                }
            }

            /// <summary>
            /// Removes existing locks for a user
            /// </summary>
            /// <param name="userId"></param>
            /// <param name="pageType"></param>
            public void RemoveLocksForUser(string userId, string pageType = null)
            {
                // pageType = "filing"
                var xmlLocks = ListLocks();
                var xPath = (pageType == null) ? $"/locks/lock[userid/text()='{userId}']" : $"/locks/lock[userid/text()='{userId}' and @type='{pageType}']";
                List<string> memCacheKeysToRemove = new List<string>();
                foreach (XmlNode nodeLock in xmlLocks.SelectNodes(xPath))
                {
                    memCacheKeysToRemove.Add(GetAttribute(nodeLock, "id"));
                }

                foreach (var key in memCacheKeysToRemove)
                {
                    _removeLock(key);
                }

            }

            /// <summary>
            /// Removes a lock for a section ID in a filing or for a page ID
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="pageId"></param>
            /// <param name="pageType"></param>
            public void RemoveLockForPage(ProjectVariables projectVars, string pageId, string pageType = "filing")
            {
                if (LockExists(projectVars, pageId, pageType))
                {
                    var xmlLocks = ListLocks();
                    var xPath = $"/locks/lock[@projectId='{projectVars.projectId}' and @type='{pageType}' and pageids/pageid/text()='{pageId}']";
                    var nodeLock = xmlLocks.SelectSingleNode(xPath);
                    _removeLock(GetAttribute(nodeLock, "id"));
                }
            }

            /// <summary>
            /// Adds a lock to the storage
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="pageId"></param>
            /// <param name="pageType"></param>
            public TaxxorReturnMessage AddLock(ProjectVariables projectVars, string pageId, string pageType, bool failWarningIfExists = false)
            {
                var message = "";
                var debugInfo = "";
                try
                {
                    if (!LockExists(projectVars, pageId, pageType))
                    {
                        // 1) Remove any other existing locks for this user
                        RemoveLocksForUser(projectVars.currentUser.Id, pageType);

                        // 2) Set a new lock
                        var memoryCacheKey = $"lock_{RandomString(30, false)}";

                        var cacheOptions = new MemoryCacheEntryOptions()
                            // Pin to cache.
                            .SetPriority(CacheItemPriority.NeverRemove);

                        //cacheOptions.AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));
                        cacheOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_lockExpirationTime);

                        var lockContent = _generateLockContent(projectVars, pageId, memoryCacheKey, pageType);
                        _lockEntries.Add(memoryCacheKey);
                        _memoryCache.Set(memoryCacheKey, lockContent, cacheOptions);

                        return new TaxxorReturnMessage(true, "Successfully created lock", $"pageId: {pageId}, pageType: {pageType}");
                    }
                    else
                    {
                        if (!failWarningIfExists)
                        {
                            return new TaxxorReturnMessage(true, "No need to set a lock as it already exists", $"pageId: {pageId}, pageType: {pageType}");
                        }
                        else
                        {
                            message = $"Could not add the lock because it already exists.";
                            debugInfo = $"projectId: {projectVars.projectId}, pageId: {pageId}, pageType: {pageType}, routine: FilingLockStore.AddLock()";
                            appLogger.LogWarning($"{message} {debugInfo}");
                            return new TaxxorReturnMessage(false, message, debugInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    message = $"There was a problem adding the lock to the page.";
                    debugInfo = $"projectId: {projectVars.projectId}, pageId: {pageId}, pageType: {pageType}, stack-trace: {GetStackTrace()}";
                    appLogger.LogError(ex, $"{message} {debugInfo}");
                    return new TaxxorReturnMessage(false, message, debugInfo);
                }

            }

            /// <summary>
            /// Updates the lock by expanding the expiry date of the lock item in the memory cache
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="pageId"></param>
            /// <param name="pageType"></param>
            /// <returns></returns>
            public XmlDocument UpdateLock(ProjectVariables projectVars, string pageId, string pageType = "filing")
            {
                var message = "";
                var debugInfo = "";
                try
                {
                    if (LockExists(projectVars, pageId, pageType))
                    {
                        // 1) Retrieve the current lock data
                        var xmlLock = this.RetrieveLockForUser(projectVars, pageType);

                        // 2) Retrieve the memory cache key
                        var memoryCacheKey = xmlLock.DocumentElement.GetAttribute("id");

                        // 3) Update the expiry date of the lock
                        var cacheOptions = new MemoryCacheEntryOptions()
                            // Pin to cache.
                            .SetPriority(CacheItemPriority.NeverRemove);

                        //cacheOptions.AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));
                        cacheOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_lockExpirationTime);

                        _memoryCache.Set(memoryCacheKey, xmlLock.OuterXml, cacheOptions);
                        return GenerateSuccessXml("Successfully updated lock", $"pageId: {pageId}, pageType: {pageType}");
                    }
                    else
                    {
                        message = $"Could update the lock because it does not exist";
                        debugInfo = $"projectId: {projectVars.projectId}, pageId: {pageId}, pageType: {pageType}, stack-trace: {GetStackTrace()}";
                        appLogger.LogWarning($"{message} {debugInfo}");
                        return GenerateErrorXml(message, debugInfo);
                    }
                }
                catch (Exception ex)
                {
                    message = $"There was a problem updating the lock of the page.";
                    debugInfo = $"projectId: {projectVars.projectId}, pageId: {pageId}, pageType: {pageType}, stack-trace: {GetStackTrace()}";
                    appLogger.LogError(ex, $"{message} {debugInfo}");
                    return GenerateErrorXml(message, debugInfo);
                }

            }

            /// <summary>
            /// Retrieve the total number of locks
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="pageType"></param>
            /// <returns></returns>
            public int RetrieveNumberOfLocks(ProjectVariables projectVars, string pageType = "filing")
            {
                return RetrieveNumberOfLocks(projectVars.projectId, pageType);
            }

            /// <summary>
            /// Retrieve the total number of locks
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="pageType"></param>
            /// <returns></returns>
            public int RetrieveNumberOfLocks(string projectId, string pageType = "filing")
            {
                var xmlLocks = ListLocks();
                var xPath = $"/locks/lock[@projectId='{projectId}' and @type='{pageType}']";
                return xmlLocks.SelectNodes(xPath).Count;
            }

            /// <summary>
            /// Retrieves lock information for the current user
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="pageType"></param>
            /// <returns></returns>
            public XmlDocument? RetrieveLockForUser(ProjectVariables projectVars, string pageType = "filing")
            {
                if (LockExists(projectVars, pageType))
                {
                    var xmlLocks = ListLocks();
                    var xPath = $"/locks/lock[@projectId='{projectVars.projectId}' and @type='{pageType}' and userid/text()='{projectVars.currentUser.Id}']";
                    var xmlLock = new XmlDocument();
                    var nodeUserLock = xmlLocks.SelectSingleNode(xPath);
                    if (nodeUserLock != null)
                    {
                        xmlLock.ReplaceContent(nodeUserLock);
                    }
                    else
                    {
                        appLogger.LogError($"Unable to find lock for user {projectVars.currentUser.Id} in project {projectVars.projectId} with page type {pageType}");
                    }
                    return xmlLock;
                }
                else
                {
                    return GenerateErrorXml($"No lock exists for the current user", $"user-id: {projectVars.currentUser.Id}, page-type: {pageType}");
                }
            }

            /// <summary>
            /// Retrieves lock information for a specific page id
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="pageId"></param>
            /// <param name="pageType"></param>
            /// <returns></returns>
            public XmlDocument? RetrieveLockForPageId(ProjectVariables projectVars, string pageId, string pageType = "filing")
            {
                if (LockExists(projectVars, pageId, pageType))
                {
                    var xmlLocks = ListLocks();
                    var xPath = $"/locks/lock[@projectId='{projectVars.projectId}' and @type='{pageType}' and pageids/pageid/text()='{pageId}']";
                    var xmlLock = new XmlDocument();
                    var nodePageIdLock = xmlLocks.SelectSingleNode(xPath);
                    if (nodePageIdLock != null)
                    {
                        xmlLock.ReplaceContent(nodePageIdLock);
                    }
                    else
                    {
                        appLogger.LogError($"Unable to find lock for for page id {pageId} in project {projectVars.projectId} with page type {pageType}");
                    }
                    return xmlLock;
                }
                else
                {
                    return GenerateErrorXml($"No lock exists for the page id", $"page-id: {pageId}, page-type: {pageType}");
                }
            }

            /// <summary>
            /// Renders an overview of all the locks set in the system
            /// </summary>
            /// <returns></returns>
            public XmlDocument? ListLocks()
            {
                // var fakeLock = false;
                var sbXmlLocks = new StringBuilder();

                sbXmlLocks.Append("<locks>");

                // For testing purposes we add a lock for another user
                // if (fakeLock)
                // {
                //     var context = System.Web.Context.Current;

                //     // Retrieve the request variables objects
                //     RequestVariables reqVars = RetrieveRequestVariables(context);
                //     ProjectVariables projectVars = RetrieveProjectVariables(context);

                //     // Create a fake project vars object that we will use to mimic another fake user
                //     var fakeProjectVars = new ProjectVariables(reqVars.pageId);
                //     fakeProjectVars.currentUser.Id = "foo.bar@gmail.com";
                //     fakeProjectVars.currentUser.DisplayName = "Bar, Foo";
                //     fakeProjectVars.projectId = projectVars.projectId;
                //     fakeProjectVars.reportTypeId = projectVars.projectId;
                //     fakeProjectVars.editorId = projectVars.editorId;
                //     fakeProjectVars.outputChannelVariantId = projectVars.outputChannelVariantId;
                //     fakeProjectVars.outputChannelVariantLanguage = projectVars.outputChannelVariantLanguage;
                //     fakeProjectVars.did = "1156524-performance-per-segment";
                //     fakeProjectVars.versionId = projectVars.versionId;
                //     fakeProjectVars.editorContentType = projectVars.editorContentType;

                //     // Retrieve the metadata (=hierarchies)
                //     RetrieveOutputChannelHierarchiesMetaData(fakeProjectVars, reqVars).GetAwaiter().GetResult();

                //     // Append the content of the fake lock
                //     sbXmlLocks.Append(_generateLockContent(fakeProjectVars, "1156524-performance-per-segment", "foobar"));
                // }

                // Loop through the locks
                List<string> lockEntriesToRemove = new List<string>();
                foreach (var key in _lockEntries)
                {
                    var lockInformation = (string)RetrieveMemoryCacheItem(key);
                    if (string.IsNullOrEmpty(lockInformation))
                    {
                        lockEntriesToRemove.Add(key);
                    }
                    else
                    {
                        sbXmlLocks.Append(lockInformation);
                    }

                }
                sbXmlLocks.Append("</locks>");

                // Cleanup the list when there is no content anymore
                foreach (var key in lockEntriesToRemove)
                {
                    _lockEntries.Remove(key);
                }

                if (_debugRoutine)
                {
                    appLogger.LogInformation($"xmlLocks: {sbXmlLocks.ToString()}");

                    appLogger.LogInformation($"{_lockEntries.Count.ToString()} locks left...");
                }

                try
                {
                    var xmlLocks = new XmlDocument();
                    xmlLocks.LoadXml(sbXmlLocks.ToString());
                    return xmlLocks;
                }
                catch (Exception ex)
                {
                    return GenerateErrorXml("Could not render locks overview", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }

        }

    }
}