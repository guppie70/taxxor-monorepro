using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// In-memory cache for paged media content like PDF
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {



        public class PagedMediaContentCache
        {

            private bool _cacheInUse
            {
                get
                {
                    if (!_cacheInUseSet)
                    {
                        _cacheInUseValue = (PdfRenderEngine == "pagedjs");
                        _cacheInUseSet = true;
                    }
                    return _cacheInUseValue;
                }

                set
                {
                    this._cacheInUseValue = value;
                }
            }
            private bool _cacheInUseSet = false;
            private bool _cacheInUseValue = false;

            private ConcurrentDictionary<string, CacheItem> _ProjectContentCache { get; set; } = new ConcurrentDictionary<string, CacheItem>();

            public PagedMediaContentCache()
            {

            }


            public void Clear()
            {
                this._ProjectContentCache.Clear();
            }

            public void Clear(string projectId)
            {
                if (!this._cacheInUse) return;

                foreach (var item in this._ProjectContentCache)
                {
                    var key = item.Key;

                    if (key.StartsWith(_generateCacheKey(projectId)))
                    {
                        if (!this._ProjectContentCache.TryRemove(key, out _))
                        {
                            appLogger.LogWarning($"Could not remove paged media cache key (key: {key}, projectId: {projectId})");
                        }
                    }
                }
            }

            public void Clear(string projectId, string outputChannelVariantId)
            {
                if (!this._cacheInUse) return;

                foreach (var item in this._ProjectContentCache)
                {
                    var key = item.Key;

                    if (key.StartsWith(_generateCacheKey(projectId, outputChannelVariantId)))
                    {
                        if (!this._ProjectContentCache.TryRemove(key, out _))
                        {
                            appLogger.LogWarning($"Could not remove paged media cache key (key: {key}, projectId: {projectId}, outputChannelVariantId: {outputChannelVariantId})");
                        }
                    }
                }
            }

            public void Clear(string projectId, string outputChannelVariantId, string itemId)
            {
                if (!this._cacheInUse) return;

                var key = _generateCacheKey(projectId, outputChannelVariantId, itemId);
                if (this._ProjectContentCache.ContainsKey(key))
                {
                    if (!this._ProjectContentCache.TryRemove(key, out _))
                    {
                        appLogger.LogWarning($"Could not remove paged media cache key (key: {key}, projectId: {projectId}, outputChannelVariantId: {outputChannelVariantId}, itemId: {itemId})");
                    }
                }
                else
                {
                    appLogger.LogInformation($"Paged media cache key {key} not present so we did not remove it (projectId: {projectId}, outputChannelVariantId: {outputChannelVariantId}, itemId: {itemId})");
                }
            }

            public void SetContent(ProjectVariables projectVars, string itemId, string htmlContent)
            {
                if (!this._cacheInUse) return;

                var key = _generateCacheKey(projectVars.projectId, projectVars.outputChannelVariantId, itemId);

                // Calculate the content hash which is a combination of all the content blocks that this content consists of
                var contentHash = _generateContentHash(projectVars, itemId);

                // Use thread-safe method of updating the global cache entry
                this._ProjectContentCache.AddOrUpdate(key, (item) =>
                {
                    return new CacheItem(contentHash, htmlContent);
                }, (key, existingItem) =>
                {
                    existingItem.CacheKey = contentHash;
                    existingItem.Html = htmlContent;
                    return existingItem;
                });
            }


            public string? RetrieveContent(ProjectVariables projectVars, string itemId)
            {
                if (!this._cacheInUse) return null;

                var key = _generateCacheKey(projectVars.projectId, projectVars.outputChannelVariantId, itemId);
                CacheItem? cacheItem = null;
                string? htmlContent = null;

                if (this._ProjectContentCache.TryGetValue(key, out cacheItem))
                {
                    // Test if the cache item is still valid
                    var currentContentHash = _generateContentHash(projectVars, itemId);
                    if (cacheItem.CacheKey != currentContentHash)
                    {
                        appLogger.LogInformation($"!!!! Invalid cache !!!! (projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}, itemId: {itemId})");
                        return null;
                    }

                    htmlContent = cacheItem.Html;
                }
                else
                {
                    appLogger.LogInformation($"Unable to retrieve paged media content from the cache (projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}, itemId: {itemId})");
                }

                return htmlContent;
            }




            /// <summary>
            /// Generates a unique key to use in the cache dictionary
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="outputChannelVariantId"></param>
            /// <param name="itemId"></param>
            /// <returns></returns>
            private string _generateCacheKey(string projectId, string outputChannelVariantId = null, string itemId = null)
            {
                if (outputChannelVariantId == null && itemId == null)
                {
                    return $"{projectId}___";
                }
                else if (itemId == null)
                {
                    return $"{projectId}___{outputChannelVariantId}";
                }

                return $"{projectId}___{outputChannelVariantId}___{itemId}";
            }

            private string _generateContentHash(ProjectVariables projectVars, string itemId)
            {
                try
                {
                    // Load the hierarchy
                    var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);
                    var xmlOutputChannelHierarchy = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;

                    // Figure out all the data references that are used in this section 
                    var dataReferences = new List<string>();
                    var xPathMainElement = $"/items/structured/item/sub_items//item[@id='{itemId}']";
                    if (itemId == "all") xPathMainElement = $"/items/structured/item";
                    var nodeItem = xmlOutputChannelHierarchy.SelectSingleNode(xPathMainElement);
                    if (nodeItem != null)
                    {
                        var dataReference = XpathDataRef(nodeItem.GetAttribute("data-ref"));
                        if (!dataReferences.Contains(dataReference)) dataReferences.Add(dataReference);

                        var nodeListSubItems = nodeItem.SelectNodes($"sub_items//item");
                        var totalSiteStructureItems = nodeListSubItems.Count + 1;
                        foreach (XmlNode node in nodeListSubItems)
                        {
                            dataReference = XpathDataRef(node.GetAttribute("data-ref"));
                            if (!dataReferences.Contains(dataReference)) dataReferences.Add(dataReference);
                        }

                        // Generate an xPath part so we only need to query once
                        var xpathSelector = string.Join(" or ", dataReferences.ToArray());

                        // Retrieve a content hash for all the content elements using the local metadata cache
                        var sbTotalHash = new StringBuilder();
                        var nodeListMetadataEntries = XmlCmsContentMetadata.SelectNodes($"/projects/cms_project[@id='{projectVars.projectId}']/data/content[{xpathSelector}]");
                        var totalMetadataItems = nodeListMetadataEntries.Count;

                        // Simple check to see if we have found all the entries from the site structure in the metadata object
                        if (totalSiteStructureItems != totalMetadataItems)
                        {
                            appLogger.LogError($"Error generating content hash for cache the number of items in the sitestructure {totalSiteStructureItems} does not match the number of metadata itens {totalMetadataItems}");
                            return null;
                        }

                        foreach (XmlNode nodeMetadataEntry in nodeListMetadataEntries)
                        {
                            sbTotalHash.Append(nodeMetadataEntry.GetAttribute("hash"));
                        }

                        return md5(sbTotalHash.ToString());
                    }
                    else
                    {
                        appLogger.LogError($"Error generating content hash for cache bacause the root node could not be located. xPathMainElement: {xPathMainElement}, projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}, itemId: {itemId}");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Error generating content hash for cache. projectId: {projectVars.projectId}, outputChannelVariantId: {projectVars.outputChannelVariantId}, itemId: {itemId}");
                    return null;
                }


                string XpathDataRef(string dataRef)
                {
                    return $"@ref='{dataRef}'";
                }
            }

        }


        public class CacheItem
        {
            public string CacheKey { get; set; } = null;
            public string Html { get; set; } = null;

            public CacheItem(string cacheKey, string html)
            {
                this.CacheKey = cacheKey;
                this.Html = html;
            }

        }


    }
}