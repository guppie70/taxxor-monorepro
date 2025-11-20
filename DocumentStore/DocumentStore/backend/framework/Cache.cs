using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Utilities for working with memory cache
/// </summary>
public abstract partial class Framework
{
    private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

    /// <summary>
    /// Tests if a memory key is still valid
    /// </summary>
    /// <param name="memCacheId">ID from application configuration or xpath of location where to find the configuration of the cache</param>
    /// <returns></returns>
    public static bool IsCacheValid(string memCacheId)
    {
        //retrieve a dictionary of memcache keys
        Dictionary<string, string> cacheKeys = GenerateCacheKeys(memCacheId);

        return IsCacheValid(memCacheId, cacheKeys["keycontent"], cacheKeys["keydate"], null);
    }

    /// <summary>
    /// Tests if a memory key is still valid
    /// </summary>
    /// <param name="memCacheId">ID from application configuration or xpath of location where to find the configuration of the cache</param>
    /// <param name="memCacheKeyBase">Core string to base the keys on</param>
    /// <returns></returns>
    public static bool IsCacheValid(string memCacheId, string memCacheKeyBase)
    {
        return IsCacheValid(memCacheId, memCacheKeyBase, memCacheKeyBase + "_datecreated", null);
    }

    /// <summary>
    /// Tests if a memory key is still valid
    /// </summary>
    /// <param name="memCacheId">ID from application configuration or xpath of location where to find the configuration of the cache</param>
    /// <param name="keyDynamicResource">Unique memory key for the content</param>
    /// <param name="keyDynamicResourceCreated">Unique memory key to store the date</param>
    /// <returns></returns>
    public static bool IsCacheValid(string memCacheId, string keyDynamicResource, string keyDynamicResourceCreated)
    {
        return IsCacheValid(memCacheId, keyDynamicResource, keyDynamicResourceCreated, null);
    }

    /// <summary>
    /// Tests if a memory key is still valid
    /// </summary>
    /// <param name="memCacheId">ID from application configuration or xpath of location where to find the configuration of the cache</param>
    /// <param name="keyContent">Unique memory key for the content</param>
    /// <param name="keyDate">Unique memory key to store the date</param>
    /// <param name="nodeListCustom">Custom xml node list containing "location" nodes of custom depandency files</param>
    /// <returns></returns>
    public static bool IsCacheValid(string memCacheId, string keyContent, string keyDate, XmlNodeList? nodeListCustom)
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);

        //by default the cache is invalid
        var isCacheValid = false;
        var isCurrentlyValidated = false;

        //test if this calculation is already perfromed in another C#/aspx page
        var keyCurrentlyValidated = keyContent + "_currentlyvalidated";
        if (MemoryCacheItemExists(keyCurrentlyValidated))
        {
            if (RetrieveMemoryCacheItem(keyCurrentlyValidated)?.ToString() == "yes") isCurrentlyValidated = true;
        }
        else
        {
            SetMemoryCacheItem(keyCurrentlyValidated, "yes");
        }

        if (reqVars.debugCacheSystem) reqVars.debugCacheContent.AppendLine("- in IsCacheValid('" + memCacheId + "')");

        //only recalculate the validity of the cache if no other process is currently doing the same...
        if (!isCurrentlyValidated)
        {

            //calculate the nodelist to find the definition of the cache
            XmlNodeList xmlNodeListCacheEntries = RetrieveCacheDefinitionNode(memCacheId);

            if (xmlNodeListCacheEntries == null) return isCacheValid;

            if (xmlNodeListCacheEntries.Count > 0)
            {
                var xmlNodeCache = xmlNodeListCacheEntries.Item(0);
                if (xmlNodeCache == null) return isCacheValid;

                //1) check if the two keys exist
                if (MemoryCacheItemExists(keyContent) && MemoryCacheItemExists(keyDate))
                {
                    //2) is cache variable still valid?

                    //attempt to retrieve the creationdate from the memcache
                    //var dateMemCacheCreated = (DateTime)memoryCacheUtilities.Retrieve(keyDate);
                    DateTime dateMemCacheCreated;
                    Object? objDataCacheCreatedTemp = RetrieveMemoryCacheItem(keyDate);
                    if (objDataCacheCreatedTemp != null)
                    {
                        try
                        {
                            var dateMemCacheCreatedTemp = (DateTime) objDataCacheCreatedTemp;
                            string dateFromCacheKey = dateMemCacheCreatedTemp.ToString();
                            if (DateTime.TryParse(dateFromCacheKey, out dateMemCacheCreated))
                            {
                                //nothing to do - this is a valid date
                            }
                            else
                            {
                                //use a date in the past to assure that the cache will be recalculated
                                dateMemCacheCreated = DateTime.Today.AddDays(-1);
                            }
                        }
                        catch (Exception ex)
                        {
                            //log the error
                            WriteErrorMessageToConsole("Something went wrong in the cache system", ex.ToString() + ", memCacheId=" + memCacheId + ", keyContent=" + keyContent + ", keyDate=" + keyDate);

                            //use a date in the past to assure that the cache will be recalculated
                            dateMemCacheCreated = DateTime.Today.AddDays(-1);
                        }

                    }
                    else
                    {
                        //use a date in the past to assure that the cache will be recalculated
                        dateMemCacheCreated = DateTime.Today.AddDays(-1);
                    }

                    //check if we have exceeded the interval check - we only want to re-calculate the cache if it's older thn the given cache interval

                    //default to checking in miliseconds
                    var intervalFormat = "ms";
                    if (xmlNodeCache.Attributes["validation-interval-format"] != null)
                    {
                        intervalFormat = xmlNodeCache.Attributes["validation-interval-format"].Value;
                    }
                    var interval = Convert.ToInt32(xmlNodeCache.Attributes["validation-interval"].Value);
                    var revalidateCache = false;
                    if (interval == 0) revalidateCache = true;

                    if (!revalidateCache)
                    {
                        //check if we have passed the interval
                        TimeSpan diffDates = DateTime.Now - dateMemCacheCreated;
                        Int32 compareInterval;

                        switch (intervalFormat)
                        {
                            case "s":
                                compareInterval = Convert.ToInt32(diffDates.TotalSeconds);
                                break;
                            case "m":
                                compareInterval = Convert.ToInt32(diffDates.TotalMinutes);
                                break;
                            case "h":
                                compareInterval = Convert.ToInt32(diffDates.TotalHours);
                                break;
                            default:
                                compareInterval = Convert.ToInt32(diffDates.TotalMilliseconds);
                                break;
                        }

                        if (compareInterval > interval)
                        {
                            revalidateCache = true;
                            SetMemoryCacheItem(keyDate, DateTime.Now);
                        }
                    }

                    bool memCacheStillValid = true;

                    if (reqVars.debugCacheSystem) reqVars.debugCacheContent.AppendLine("- passed interval -> IsCacheValid('" + memCacheId + "').revalidate=" + revalidateCache);

                    if (revalidateCache)
                    {
                        bool isFiledepandantCache = false;

                        //check for a custom nodelist that can optionally be passed here
                        if (nodeListCustom != null) isFiledepandantCache = true;

                        var nodeListFileDepandancies = xmlNodeCache.SelectNodes("cache_entry[@type='content']/dependencies/location");
                        if (nodeListFileDepandancies.Count > 0) isFiledepandantCache = true;

                        if (isFiledepandantCache)
                        {
                            //the interval has exceeded - now check if one of the file dependancies has mod date later than when the cache was created

                            //a) try configured locations
                            foreach (XmlNode xmlNode in nodeListFileDepandancies)
                            {
                                memCacheStillValid = IsCacheDateYoungerThanFileModDate(xmlNode, dateMemCacheCreated);
                                if (!memCacheStillValid) break;

                            }

                            //b) try passed locations
                            if (nodeListCustom != null)
                            {
                                foreach (XmlNode xmlNode in nodeListCustom)
                                {
                                    memCacheStillValid = IsCacheDateYoungerThanFileModDate(xmlNode, dateMemCacheCreated);
                                    if (!memCacheStillValid) break;
                                }
                            }
                        }
                        else
                        {
                            //we must recalculate the cache as there is no way to predict the value of the cache
                            memCacheStillValid = false;
                        }
                    }

                    if (reqVars.debugCacheSystem) reqVars.debugCacheContent.AppendLine("- IsCacheValid('" + memCacheId + "').memCacheStillValid=" + memCacheStillValid);

                    if (memCacheStillValid)
                    {
                        isCacheValid = true;
                    }
                    else
                    {
                        isCacheValid = false;
                    }
                }

                //update the key to flag that re-validation is allowed again
                SetMemoryCacheItem(keyCurrentlyValidated, "no");
            }

        }

        return isCacheValid;
    }

    /// <summary>
    /// Routine that tests if a file was being modified after the time that the memcache was created
    /// </summary>
    /// <param name="xmlNode"></param>
    /// <param name="dateMemCacheCreated"></param>
    /// <returns></returns>
    private static bool IsCacheDateYoungerThanFileModDate(XmlNode xmlNode, DateTime dateMemCacheCreated)
    {
        bool memCacheStillValid = true;
        var directoryCreateError = "";
        var pathOs = CalculateFullPathOs(xmlNode);
        if (File.Exists(pathOs))
        {
            DateTime lastWriteTime = File.GetLastWriteTime(pathOs);
            int dateCompareResult = DateTime.Compare(dateMemCacheCreated, lastWriteTime);
            if (dateCompareResult < 0)
            {
                memCacheStillValid = false;
            }
        }
        else
        {
            //resource could not be located - this needs to be logged somehow...
            //JT: use system log routine for this (maybe in combination with the log4net library?
            //CreateEntryInEventViewer("Category", "bla", "Cache Warning: file " + pathOs + " could not be found");
            var logFilePathOs = applicationRootPathOs + "/log/cache.log";
            var logFolderPathOs = Path.GetDirectoryName(logFilePathOs);
            var succeeded = false;
            if (!Directory.Exists(logFolderPathOs))
            {
                try
                {
                    Directory.CreateDirectory(logFolderPathOs);
                }
                catch (Exception ex)
                {
                    //there is no way to do anything with this information...
                    directoryCreateError = ex.ToString();
                }
            }
            else
            {
                succeeded = true;
            }

            if (succeeded) AppendLogDataInFile("Cache Warning: file " + pathOs + " could not be found", logFilePathOs);
        }

        return memCacheStillValid;

    }

    /// <summary>
    /// Retrieves a value from the memory cache
    /// 'RetrieveCacheContent&lt;System.Xml.XmlDocument&gt;("cacheid")' or 'RetrieveCacheContent&lt;string&gt;("cacheid")'
    /// </summary>
    /// <typeparam name="T">System.String or System.Xml.XmlDocument are supported</typeparam>
    /// <param name="memoryCacheKey"></param>
    /// <returns></returns>
    public static T RetrieveCacheContent<T>(String memoryCacheKey)
    {
        /*
		Determine the type of variable to return and normalize it for later use 
		*/
        var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

        //retrieve the value from the memory cache and return the correct type
        switch (returnType)
        {
            case "xml":

                /*
				Now base the error handling on the type of status that was encountered
				*/
                var xmlResponse = new XmlDocument();
                xmlResponse = (XmlDocument) RetrieveMemoryCacheItem(memoryCacheKey);

                return (T) Convert.ChangeType(xmlResponse, typeof(T));
            case "dictionary<string,int>":
                var dictStringInt = new Dictionary<string, int>();
                dictStringInt = (Dictionary<string, int>) RetrieveMemoryCacheItem(memoryCacheKey);
                return (T) Convert.ChangeType(dictStringInt, typeof(T));
            case "dictionary<string,string>":
                var dictStringString = new Dictionary<string, string>();
                dictStringString = (Dictionary<string, string>) RetrieveMemoryCacheItem(memoryCacheKey);
                return (T) Convert.ChangeType(dictStringString, typeof(T));
            default:
                var strResponse = string.Empty;
                strResponse = (string) RetrieveMemoryCacheItem(memoryCacheKey);

                //default to string
                return (T) Convert.ChangeType(strResponse, typeof(T));
        }

    }

    /// <summary>
    /// Sets the value of a memory cache key
    /// </summary>
    /// <param name="memCacheId">Cache config id or the base key to create the mem cache key from</param>
    /// <param name="content"></param>
    public static void SetCacheContent(string memCacheId, string content)
    {
        //retrieve a dictionary of memcache keys
        Dictionary<string, string> cacheKeys = GenerateCacheKeys(memCacheId);

        SetMemoryCacheItem(cacheKeys["keycontent"], content);
        SetMemoryCacheItem(cacheKeys["keydate"], DateTime.Now);
    }

    /// <summary>
    /// Sets the value of a memory cache key
    /// </summary>
    /// <param name="memCacheId">Cache config id or the base key to create the mem cache key from</param>
    /// <param name="content"></param>
    public static void SetCacheContent(string memCacheId, XmlDocument content)
    {
        //retrieve a dictionary of memcache keys
        Dictionary<string, string> cacheKeys = GenerateCacheKeys(memCacheId);

        SetMemoryCacheItem(cacheKeys["keycontent"], content);
        SetMemoryCacheItem(cacheKeys["keydate"], DateTime.Now);
    }

    /// <summary>
    /// Sets the value of a memory cache key
    /// </summary>
    /// <param name="keyContent">Custom supplied unique key for the content</param>
    /// <param name="keyDate">Custom supplied unique key for the date</param>
    /// <param name="content"></param>
    public static void SetCacheContent(string keyContent, string keyDate, string content)
    {
        SetMemoryCacheItem(keyContent, content);
        SetMemoryCacheItem(keyDate, DateTime.Now);
    }

    /// <summary>
    /// Sets the value of a memory cache key
    /// </summary>
    /// <param name="keyContent">Custom supplied unique key for the content</param>
    /// <param name="keyDate">Custom supplied unique key for the date</param>
    /// <param name="content"></param>
    public static void SetCacheContent(string keyContent, string keyDate, XmlDocument content)
    {
        SetMemoryCacheItem(keyContent, content);
        SetMemoryCacheItem(keyDate, DateTime.Now);
    }

    /// <summary>
    /// Sets the value of a memory cache key
    /// </summary>
    /// <param name="keyContent">Custom supplied unique key for the content</param>
    /// <param name="keyDate">Custom supplied unique key for the date</param>
    /// <param name="content"></param>
    public static void SetCacheContent(string keyContent, string keyDate, Dictionary<string, int> content)
    {
        SetMemoryCacheItem(keyContent, content);
        SetMemoryCacheItem(keyDate, DateTime.Now);
    }

    /*
	Helper utilities
	*/

    /// <summary>
    /// Creates a dictionary containing the cachecontent and cache date key
    /// </summary>
    /// <param name="memCacheId">ID from application configuration or xpath of location where to find the configuration of the cache</param>
    /// <returns></returns>
    private static Dictionary<string, string> GenerateCacheKeys(string memCacheId)
    {
        Dictionary<string, string> returnDict = new Dictionary<string, string>();

        //retrieve the cache definition in xml
        XmlNodeList xmlNodeListCacheEntries = RetrieveCacheDefinitionNode(memCacheId);

        if (xmlNodeListCacheEntries.Count > 0)
        {
            var xmlNodeCache = xmlNodeListCacheEntries.Item(0);

            string keyContent = xmlNodeCache.SelectSingleNode("cache_entry[@type='content']").Attributes["name"].Value;
            string keyDate = xmlNodeCache.SelectSingleNode("cache_entry[@type='date']").Attributes["name"].Value;

            //place some default [xyz] replacements here???

            returnDict.Add("keycontent", keyContent);
            returnDict.Add("keydate", keyDate);

        }
        else
        {
            returnDict.Add("keycontent", memCacheId);
            returnDict.Add("keydate", memCacheId + "_datecreated");

        }

        return returnDict;
    }

    /// <summary>
    /// Retrieves the root node of the memory cache definition from application configuration
    /// </summary>
    /// <param name="memCacheId">ID from application configuration or xpath of location where to find the configuration of the cache</param>
    /// <returns></returns>
    private static XmlNodeList RetrieveCacheDefinitionNode(string memCacheId)
    {
        XmlNodeList? xmlNodeListCacheEntries = null;

        if (memCacheId.Contains("/"))
        {
            xmlNodeListCacheEntries = xmlApplicationConfiguration.SelectNodes(memCacheId);
        }
        else
        {
            xmlNodeListCacheEntries = xmlApplicationConfiguration.SelectNodes("/configuration/cache_system/cache_item[@id='" + memCacheId + "']");
        }

        return xmlNodeListCacheEntries;
    }

    /// <summary>
    /// Provides a "soft" way of removing the site structure memory cache by forcing it to refresh at the next load (setting cache data 1 year ago)
    /// </summary>
    public static void MarkSiteStructureCacheInvalid()
    {
        foreach (XmlNode node in xmlApplicationConfiguration.SelectNodes("/configuration/locales/locale/language"))
        {
            string? langId = GetAttribute(node, "code");
            string keyContent = "hierarchy_" + langId;
            string keyCreated = keyContent + "_datecreated";
            if (MemoryCacheItemExists(keyCreated))
            {
                DateTime dateLongTimeAgo = DateTime.Now;
                dateLongTimeAgo = dateLongTimeAgo.AddDays(-2);
                SetMemoryCacheItem(keyCreated, dateLongTimeAgo);
            }
        }
    }

    /// <summary>
    /// Removes generated site structure and application structure files
    /// </summary>
    public static void RemoveCachedFrameworkFiles(bool writeOutputResult)
    {
        RemoveCachedFrameworkFiles(writeOutputResult, true);
    }

    /// <summary>
    /// Removes generated site structure and (optionally) application structure files
    /// </summary>
    public static void RemoveCachedFrameworkFiles(bool writeOutputResult, bool removeAppConfigCache)
    {
        /*
		var result = "";
		string appConfigCachePath = RetrieveNodeValueIfExists("/configuration/locations/location[@id='xml_application_configuration_cache']", xmlApplicationConfiguration);
		string hierarchyCachePath = RetrieveNodeValueIfExists("/configuration/locations/location[@id='xml_hierarchy_cache']", xmlApplicationConfiguration);

		//loop through the languages
		foreach (XmlNode node in xmlApplicationConfiguration.SelectNodes("/configuration/locales/locale/language"))
		{
			string langId = GetAttribute(node, "code");

			//remove app config cache files
			if (removeAppConfigCache)
			{
				if (appConfigCachePath != null)
				{
					string pathOs = (applicationRootPathOs + configurationRootPath + appConfigCachePath).Replace("[lang]", langId);
					if (File.Exists(pathOs))
					{
						result += " Removed: " + pathOs + "<br/>";
						File.Delete(pathOs);
					}
					else
					{
						result += " Could not find: " + pathOs + "<br/>";
					}
				}
			}

			//remove hierarchy cache files
			if (hierarchyCachePath != null)
			{
				string pathOs = (applicationRootPathOs + hierarchyRootPath + hierarchyCachePath).Replace("[lang]", langId);
				if (File.Exists(pathOs))
				{
					result += " Removed: " + pathOs + "<br/>";
					File.Delete(pathOs);
				}
				else
				{
					result += " Could not find: " + pathOs + "<br/>";
				}
			}

		}




		if (writeOutputResult)
		{
			dynamic jsonResponseObject = new ExpandoObject();
			jsonResponseObject.message = "success";
			jsonResponseObject.result = "removed cache files<br/>" + result;

			string jsonOut = JsonConvert.SerializeObject(jsonResponseObject, Newtonsoft.Json.Formatting.Indented);
			Response.Write(jsonOut);
		}
		else
		{
			//write to log so we can at least somehow find out what has happened
			ErrorHandlingTrace("Removed cache files", result, false, true);

		}
		*/

    }

    /// <summary>
    /// This method stores an object in the cache which is not set to expire at a specific time. It will only expire once the server or site has been reset.
    /// </summary>
    /// <param name="key">Variable name.</param>
    /// <param name="value">Object to store.</param>
    public static void SetMemoryCacheItem(string key, object value)
    {
        var cacheOptions = new MemoryCacheEntryOptions().SetPriority(CacheItemPriority.Normal);
        cacheOptions.AbsoluteExpiration = DateTime.Now.AddYears(2);
        cacheOptions.AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));
        memoryCache.Set(key, value, cacheOptions);
    }
    /// <summary>
    /// This method stores an object in the cache.
    /// </summary>
    /// <param name="key">variable name</param>
    /// <param name="value">Object to store</param>
    /// <param name="datetime">DateTime the cache variable should expire</param>
    public static void SetMemoryCacheItem(string key, object value, DateTime datetime)
    {
        var cacheOptions = new MemoryCacheEntryOptions().SetPriority(CacheItemPriority.Normal);
        cacheOptions.AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));
        cacheOptions.AbsoluteExpiration = datetime;
        memoryCache.Set(key, value, cacheOptions);
    }
    /// <summary>
    /// This method stores an object in the cache.
    /// </summary>
    /// <param name="key">Variable name.</param>
    /// <param name="value">Object to store.</param>
    /// <param name="timespan">TimeSpan the cache variable should expires.</param>
    public static void SetMemoryCacheItem(string key, object value, TimeSpan timespan)
    {
        var cacheOptions = new MemoryCacheEntryOptions().SetPriority(CacheItemPriority.Normal);
        cacheOptions.AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));
        cacheOptions.AbsoluteExpirationRelativeToNow = timespan;
        memoryCache.Set(key, value, cacheOptions);
    }

    /// <summary>
    /// This method retrieves an object from the cache.
    /// </summary>
    /// <param name="key">Variable name.</param>
    /// <returns>The request object from cache or null (requires casting).</returns>
    public static object? RetrieveMemoryCacheItem(string key)
    {
        memoryCache.TryGetValue<object>(key, out object? value);

        return value;
    }

    /// <summary>
    /// This method removes an object from the cache.
    /// </summary>
    /// <param name="key">Variable name.</param>
    public static void RemoveMemoryCacheItem(string key)
    {
        try
        {
            memoryCache.Remove(key);
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, $"Could not remove entry '{key}' from the memory cache...");
        }
    }

    /// <summary>
    /// This method removes all objects from the system memory.
    /// </summary>
    public static void RemoveMemoryCacheAll()
    {
        if (_resetCacheToken != null && !_resetCacheToken.IsCancellationRequested && _resetCacheToken.Token.CanBeCanceled)
        {
            _resetCacheToken.Cancel();
            _resetCacheToken.Dispose();
        }

        _resetCacheToken = new CancellationTokenSource();
    }

    /// <summary>
    /// This method checks if an object exsits the cache.
    /// </summary>
    /// <param name="key">Variable name.</param>
    /// <returns>true or false</returns>
    public static bool MemoryCacheItemExists(String key)
    {
        if (memoryCache.TryGetValue<object>(key, out object? _))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// This method displays an object from the cache for debugging purposes.
    /// </summary>
    /// <param name="key">Variable name.</param>
    public static void Display(String key)
    {
        //if (debugCacheSystem) HttpContext.Current.Response.Write(GenerateDebugInformationKey(key));
    }

    /// <summary>
    /// This method displays all objects from the system memory for debugging purposes.
    /// </summary>
    public void DisplayAll()
    {
        //if (debugCacheSystem) HttpContext.Current.Response.Write(this.GenerateDebugInformation());
    }

    /// <summary>
    /// This method generates a string that displays all memory cache variables in use for debugging purposes
    /// </summary>
    /// <returns></returns>
    public string GenerateDebugInformation()
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);
        StringBuilder debugInfo = new StringBuilder();

        debugInfo.Append("<div class=\"memcacheinfo\"><h4>Memory Cache Variables</h4><div class=\"details\">").AppendLine();;

        if (reqVars.debugCacheSystem)
        {
            // In dotnet core there is no way to iterate over the memory cache items

            //List<string> keys = new List<string>();
            //IDictionaryEnumerator enumerator = memoryCache.ge HttpContext.Current.Cache.GetEnumerator();

            //while (enumerator.MoveNext())
            //{
            //    keys.Add(enumerator.Key.ToString());
            //}

            ////sort the keys
            //keys.Sort();

            ////generate the debug information
            //for (int i = 0; i < keys.Count; i++)
            //{
            //    debugInfo.Append(GenerateDebugInformationKey(keys[i])).AppendLine();
            //}
            ////HttpContext.Current.Response.Write(HttpContext.Current.Cache.EffectivePercentagePhysicalMemoryLimit.ToString());
        }

        debugInfo.Append("</div></div>").AppendLine();;

        return debugInfo.ToString();
    }

    /// <summary>
    /// This method generates debug information from a named memory cache key so that it can be used for bebugging purposes.
    /// </summary>
    /// <param name="key">Memory key variable name.</param>
    public static string GenerateDebugInformationKey(String key)
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);
        StringBuilder info = new StringBuilder();
        const int maxLength = 100;
        if (MemoryCacheItemExists(key) && reqVars.debugCacheSystem)
        {
            string? cacheContent = RetrieveMemoryCacheItem(key).ToString();
            if (cacheContent.Length > maxLength) cacheContent = cacheContent.Substring(0, maxLength);
            cacheContent = System.Web.HttpUtility.HtmlEncode(cacheContent);
            info.Append("Cache['").Append(key).Append("'] = '").Append(cacheContent).Append("'<br/>").AppendLine();
        }
        return info.ToString();
    }

}