using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

/// <summary>
/// Contains helper functions for working with querystrings
/// </summary>
public abstract partial class Framework
{

    /// <summary>
    /// Show how to manipulate the querystring parameters of a URL (https://benjii.me/2017/04/parse-modify-query-strings-asp-net-core/)
    /// </summary>
    public static void ExampleRoutine(){
        var rawurl = "https://bencull.com/some/path?key1=val1&key2=val2&key2=valdouble&key3=";

        var uri = new Uri(rawurl);
        var baseUri = uri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.UriEscaped);

        var query = QueryHelpers.ParseQuery(uri.Query);

        var items = query.SelectMany(x => x.Value, (col, value) => new KeyValuePair<string, string>(col.Key, value)).ToList();

        items.RemoveAll(x => x.Key == "key3"); // Remove all values for key
        items.RemoveAll(x => x.Key == "key2" && x.Value == "val2"); // Remove specific value for key

        var qb = new QueryBuilder(items);
        qb.Add("nonce", "testingnonce");
        qb.Add("payerId", "pyr_");

        var fullUri = baseUri + qb.ToQueryString();
    }
}