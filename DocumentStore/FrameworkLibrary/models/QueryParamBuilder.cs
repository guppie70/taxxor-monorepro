using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FrameworkLibrary.models
{

    /// <summary>
    /// Helper class for building query parameters
    /// </summary>
    public class QueryParamBuilder
    {
        private readonly Dictionary<string, string> _fields = new();

        public QueryParamBuilder Add(string key, string value)
        {
            _fields.Add(key, value);
            return this;
        }

        public string Build()
        {
            return $"?{string.Join("&", _fields.Select(pair => $"{HttpUtility.UrlEncode(pair.Key)}={HttpUtility.UrlEncode(pair.Value)}"))}";
        }

        public static QueryParamBuilder New => new();
    }
}