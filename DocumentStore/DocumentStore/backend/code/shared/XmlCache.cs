using System.Collections.Concurrent;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// XML cache utility
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Class for in-memory storage of XmlDocument objects using a condition (like a hash or a date string)
        /// </summary>
        public class ConditionalXmlCache
        {


            private ConcurrentDictionary<string, XmlCacheItem> _XmlCache { get; set; } = new ConcurrentDictionary<string, XmlCacheItem>();

            public ConditionalXmlCache()
            {

            }


            public void Clear()
            {
                this._XmlCache.Clear();
            }

            public void Clear(string projectId)
            {

                foreach (var item in this._XmlCache)
                {
                    var key = item.Key;

                    if (key.StartsWith(_generateCacheKey(projectId)))
                    {
                        if (!this._XmlCache.TryRemove(key, out _))
                        {
                            appLogger.LogWarning($"Could not remove xml cache key '{key}'");
                        }
                    }
                }
            }

            public void Clear(string projectId, string outputChannelVariantId)
            {

                foreach (var item in this._XmlCache)
                {
                    var key = item.Key;

                    if (key.StartsWith(_generateCacheKey(projectId, outputChannelVariantId)))
                    {
                        if (!this._XmlCache.TryRemove(key, out _))
                        {
                            appLogger.LogWarning($"Could not remove xml cache key '{key}'");
                        }
                    }
                }
            }

            public void Clear(string projectId, string outputChannelVariantId, string key)
            {


                var cacheKey = _generateCacheKey(projectId, outputChannelVariantId, key);
                if (!this._XmlCache.TryRemove(cacheKey, out _))
                {
                    appLogger.LogWarning($"Could not remove xml cache key '{cacheKey}'");
                }
            }

            /// <summary>
            /// Inserts a new XmlDocument in the cache with a condition
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="key"></param>
            /// <param name="condition"></param>
            /// <param name="xmlDoc"></param>
            public void Set(ProjectVariables projectVars, string key, string condition, XmlDocument xmlDoc)
            {
                var cacheKey = _generateCacheKey(projectVars.projectId, projectVars.outputChannelVariantId, key);

                // Use thread-safe method of updating the global cache entry
                this._XmlCache.AddOrUpdate(cacheKey, (item) =>
                {
                    return new XmlCacheItem(condition, xmlDoc);
                }, (key, existingItem) =>
                {
                    existingItem.Condition = condition;
                    existingItem.Xml = xmlDoc;
                    return existingItem;
                });
            }


            /// <summary>
            /// Retrieves content from the cache if it maches the condition
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="key"></param>
            /// <param name="condition"></param>
            /// <returns></returns>
            public XmlDocument? Retrieve(ProjectVariables projectVars, string key, string condition)
            {
                var cacheKey = _generateCacheKey(projectVars.projectId, projectVars.outputChannelVariantId, key);
                XmlCacheItem? cacheItem = null;
                XmlDocument? xmlDoc = null;

                if (this._XmlCache.TryGetValue(cacheKey, out cacheItem))
                {
                    // Test if the cache item is still valid
                    if (cacheItem.Condition != condition)
                    {
                        appLogger.LogInformation($"!!!! Invalid cache !!!! (projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}, Key: {key})");
                        return null;
                    }

                    xmlDoc = cacheItem.Xml;
                }
                else
                {
                    appLogger.LogInformation($"Unable to retrieve paged media content from the cache (projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}, Key: {key})");
                }

                return xmlDoc;
            }




            /// <summary>
            /// Generates a unique key to use in the cache dictionary
            /// </summary>
            /// <param name="key"></param>
            /// <param name="projectId"></param>
            /// <param name="outputchannelVariantId"></param>
            /// <returns></returns>
            private string _generateCacheKey(string key, string? projectId = null, string? outputchannelVariantId = null)
            {
                if (projectId == null && outputchannelVariantId == null)
                {
                    return $"{key}___";
                }
                else if (outputchannelVariantId == null)
                {
                    return $"{key}___{projectId}";
                }

                return $"{key}___{projectId}___{outputchannelVariantId}";
            }
        }

        /// <summary>
        /// Represents a conditional XML cache item
        /// </summary>
        public class XmlCacheItem
        {
            public string? Condition { get; set; } = null;
            public XmlDocument? Xml { get; set; } = null;

            public XmlCacheItem(string condition, XmlDocument xml)
            {
                this.Condition = condition;
                this.Xml = new XmlDocument();
                this.Xml.ReplaceContent(xml);
            }

        }



    }
}