using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

/// <summary>
/// Utilities to access REST webservices, with optimized memory usage
/// </summary>
public abstract partial class Framework
{
    private static int defaultRequestTimeout = 3000;

    // HttpClient cache with expiration tracking
    private static readonly ConcurrentDictionary<string, HttpClientCacheItem> _httpClients = new ConcurrentDictionary<string, HttpClientCacheItem>();

    // Last time the client cache was cleaned up
    private static DateTime _lastClientCleanupTime = DateTime.UtcNow;

    // Client expiration time (30 minutes of non-use)
    private static readonly TimeSpan _clientExpirationTime = TimeSpan.FromMinutes(30);

    // Class to track client usage and creation time
    private class HttpClientCacheItem
    {
        public HttpClient Client { get; }
        public DateTime LastUsed { get; private set; }
        public DateTime Created { get; }

        public HttpClientCacheItem(HttpClient client)
        {
            Client = client;
            LastUsed = DateTime.UtcNow;
            Created = DateTime.UtcNow;
        }

        public void UpdateLastUsed()
        {
            LastUsed = DateTime.UtcNow;
        }
    }

    #region Legacy methods (RestRequest) keeping for backward compatibility

    /// <summary>
    /// Simple SOAP request implementation
    /// </summary>
    /// <returns>The request.</returns>
    /// <param name="url">URL.</param>
    /// <param name="soapAction">SOAP action.</param>
    /// <param name="requestData">Request data.</param>
    /// <param name="customHttpHeaders">Custom http headers.</param>
    /// <param name="debug">If set to <c>true</c> debug.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public async static Task<T> SoapRequestUtility<T>(string url, string soapAction, XmlDocument requestData, CustomHttpHeaders customHttpHeaders, bool debug = false)
    {
        customHttpHeaders.AddCustomHeader("SOAPAction", soapAction);

        // Convert the data we want to post to a format that the HttpClient accepts
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);

        // Use the newer optimized RemoteRequestSingleCustomizableClient instead of the old RemoteRequest
        return await RemoteRequestSingleCustomizableClient<T>(RequestMethodEnum.Post, url, dataToPost, customHttpHeaders, new Version(1, 1), defaultRequestTimeout, debug, false);
    }

    /// <summary>
    /// Legacy RestRequest methods redirected to newer implementations - maintained for backwards compatibility
    /// </summary>
    public async static Task<T> RestRequest<T>(RequestMethodEnum method, string url, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        // Convert the Dictionary to a List containing KeyValuePair objects
        var requestKeyValueData = new List<KeyValuePair<string, string>>();
        var requestEncodedData = new FormUrlEncodedContent(requestKeyValueData);

        return await RemoteRequestSingleCustomizableClient<T>(method, url, requestEncodedData, new CustomHttpHeaders(), new Version(1, 1), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    /// <summary>
    /// Requests a remote resource without any query or post data using HTTP GET method
    /// </summary>
    public async static Task<T> WebRequest<T>(string url, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        var requestKeyValueData = new List<KeyValuePair<string, string>>();
        var requestEncodedData = new FormUrlEncodedContent(requestKeyValueData);

        return await RemoteRequestSingleCustomizableClient<T>(RequestMethodEnum.Get, url, requestEncodedData, new CustomHttpHeaders(), new Version(1, 1), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequest<T>(RequestMethodEnum method, string url, CustomHttpHeaders customHttpHeaders, bool debug = false)
    {
        var requestKeyValueData = new List<KeyValuePair<string, string>>();
        var requestEncodedData = new FormUrlEncodedContent(requestKeyValueData);

        return await RemoteRequestSingleCustomizableClient<T>(method, url, requestEncodedData, customHttpHeaders, new Version(1, 1), defaultRequestTimeout, debug, false);
    }

    public async static Task<T> RestRequest<T>(string url, CustomHttpHeaders customHttpHeaders, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        var requestKeyValueData = new List<KeyValuePair<string, string>>();
        var requestEncodedData = new FormUrlEncodedContent(requestKeyValueData);

        return await RemoteRequestSingleCustomizableClient<T>(RequestMethodEnum.Post, url, requestEncodedData, customHttpHeaders, new Version(1, 1), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> WebRequest<T>(string url, CustomHttpHeaders customHttpHeaders, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        var requestKeyValueData = new List<KeyValuePair<string, string>>();
        var requestEncodedData = new FormUrlEncodedContent(requestKeyValueData);

        return await RemoteRequestSingleCustomizableClient<T>(RequestMethodEnum.Get, url, requestEncodedData, customHttpHeaders, new Version(1, 1), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequest<T>(RequestMethodEnum method, string url, dynamic requestData, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        var customHttpHeaders = new CustomHttpHeaders();
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);

        return await RemoteRequestSingleCustomizableClient<T>(method, url, dataToPost, customHttpHeaders, new Version(1, 1), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequest<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);
        return await RemoteRequestSingleCustomizableClient<T>(method, url, dataToPost, customHttpHeaders, new Version(1, 1), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequest<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, Int32 timeout, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);
        return await RemoteRequestSingleCustomizableClient<T>(method, url, dataToPost, customHttpHeaders, new Version(1, 1), timeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequest<T>(string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);
        var method = (requestData == null) ? RequestMethodEnum.Get : RequestMethodEnum.Post;
        return await RemoteRequestSingleCustomizableClient<T>(method, url, dataToPost, customHttpHeaders, new Version(1, 1), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequest<T>(string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, Int32 timeout, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);
        var method = (requestData == null) ? RequestMethodEnum.Get : RequestMethodEnum.Post;
        return await RemoteRequestSingleCustomizableClient<T>(method, url, dataToPost, customHttpHeaders, new Version(1, 1), timeout, debug, suppressResponseExceptionLogging);
    }

    /// <summary>
    /// Deprecated - kept for backward compatibility but redirects to the new implementation
    /// </summary>
    [Obsolete("Use RemoteRequestSingleCustomizableClient instead")]
    public async static Task<T> RemoteRequest<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, bool debug, Int32 timeout = 3000, bool suppressResponseExceptionLogging = false)
    {
        return await RemoteRequestSingleCustomizableClient<T>(method, url, requestData, customHttpHeaders, new Version(1, 1), timeout, debug, suppressResponseExceptionLogging);
    }
    #endregion

    #region HTTP/2 Methods
    /// <summary>
    /// HTTP/2 Methods for modern optimized HTTP/2 requests
    /// </summary>
    public async static Task<T> RestRequestHttp2<T>(RequestMethodEnum method, string url, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        var requestKeyValueData = new List<KeyValuePair<string, string>>();
        var requestEncodedData = new FormUrlEncodedContent(requestKeyValueData);

        return await RemoteRequestSingleCustomizableClient<T>(method, url, requestEncodedData, new CustomHttpHeaders(), new Version(2, 0), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequestHttp2<T>(RequestMethodEnum method, string url, CustomHttpHeaders customHttpHeaders, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        var requestKeyValueData = new List<KeyValuePair<string, string>>();
        var requestEncodedData = new FormUrlEncodedContent(requestKeyValueData);

        return await RemoteRequestSingleCustomizableClient<T>(method, url, requestEncodedData, customHttpHeaders, new Version(2, 0), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequestHttp2<T>(RequestMethodEnum method, string url, dynamic requestData, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        var customHttpHeaders = new CustomHttpHeaders();
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);

        return await RemoteRequestSingleCustomizableClient<T>(method, url, dataToPost, customHttpHeaders, new Version(2, 0), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequestHttp2<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);
        return await RemoteRequestSingleCustomizableClient<T>(method, url, dataToPost, customHttpHeaders, new Version(2, 0), defaultRequestTimeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequestHttp2<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, Int32 timeout = 3000, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);
        return await RemoteRequestSingleCustomizableClient<T>(method, url, dataToPost, customHttpHeaders, new Version(2, 0), timeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequestHttp1<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, Int32 timeout = 3000, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);
        return await RemoteRequestSingleCustomizableClient<T>(method, url, dataToPost, customHttpHeaders, new Version(1, 1), timeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequestPredefinedClientHttp1<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, Int32 timeout = 3000, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);
        return await RemoteRequestSinglePredefinedClient<T>(method, url, dataToPost, customHttpHeaders, new Version(1, 1), timeout, debug, suppressResponseExceptionLogging);
    }

    public async static Task<T> RestRequestPredefinedClientHttp2<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, Int32 timeout = 3000, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        dynamic dataToPost = ConvertRequestDataToPostData(requestData, customHttpHeaders);
        return await RemoteRequestSinglePredefinedClient<T>(method, url, dataToPost, customHttpHeaders, new Version(2, 0), timeout, debug, suppressResponseExceptionLogging);
    }
    #endregion

    #region Core implementation methods

    /// <summary>
    /// Utility method that converts data we want to post to a format that the HttpClient will accept
    /// </summary>
    /// <returns>The request data to post data.</returns>
    /// <param name="requestData">Request data.</param>
    /// <param name="customHttpHeaders">Custom http headers.</param>
    private static dynamic ConvertRequestDataToPostData(dynamic requestData, CustomHttpHeaders customHttpHeaders = null)
    {
        // By default we will post an empty form data object
        dynamic requestEncodedData = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>());

        if (requestData != null)
        {
            string requestDataObjectType = requestData.GetType().ToString();

            if (requestDataObjectType.Contains("System.Collections.Generic.Dictionary"))
            {
                // Convert the Dictionary to form data manually
                // This overcomes the limit of FormUrlEncodedContent object
                // See: https://github.com/dotnet/corefx/issues/22369#issuecomment-343317124 (answer below)
                List<string> formData = new List<string>();

                foreach (KeyValuePair<string, string> pair in requestData)
                {
                    formData.Add(WebUtility.UrlEncode(pair.Key) + "=" + WebUtility.UrlEncode(pair.Value));
                }

                requestEncodedData = new StringContent(String.Join("&", formData), null, "application/x-www-form-urlencoded");
            }
            else if (requestDataObjectType.Contains("System.String"))
            {
                requestEncodedData = new StringContent(requestData, Encoding.UTF8, customHttpHeaders?.RetrieveRequestMimeType() ?? MediaTypeNames.Application.Json);
            }
            else if (requestDataObjectType.Contains("XmlDocument"))
            {
                if (customHttpHeaders?.IsSoapRequest() == true)
                {
                    // Determine the MIME type that we need to use for the remote request
                    var mimeContentType = (customHttpHeaders.SoapVersion == "1.1") ? MediaTypeNames.Text.Xml : MediaTypeNames.Application.Soap;
                    requestEncodedData = new StringContent(requestData.OuterXml, Encoding.UTF8, mimeContentType);
                }
                else
                {
                    requestEncodedData = new StringContent(requestData.OuterXml, Encoding.UTF8, customHttpHeaders?.RetrieveRequestMimeType() ?? MediaTypeNames.Text.Xml);
                }
            }
        }

        return requestEncodedData;
    }

    /// <summary>
    /// Optionally adds posted data to a URL as a querystring.
    /// </summary>
    /// <returns>The data as querystring to URL.</returns>
    /// <param name="url">URL.</param>
    /// <param name="requestData">Request data.</param>
    private async static Task<string> _appendDataAsQuerystringToUrl(string url, dynamic requestData)
    {
        if (requestData != null)
        {
            try
            {
                var query = await requestData.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(query))
                {
                    if (url.Contains('?'))
                    {
                        url += $"&{query}";
                    }
                    else
                    {
                        url += $"?{query}";
                    }
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "Could not convert postValue to a querystring");
            }
        }
        return url;
    }

    /// <summary>
    /// Performs cleanup of expired HttpClient instances to prevent memory leaks
    /// </summary>
    private static void CleanupExpiredHttpClients()
    {
        // Only run cleanup every 5 minutes to avoid excessive overhead
        if (DateTime.UtcNow - _lastClientCleanupTime < TimeSpan.FromMinutes(5))
            return;

        try
        {
            var now = DateTime.UtcNow;
            var keysToRemove = new List<string>();

            // Find expired clients
            foreach (var kvp in _httpClients)
            {
                if (now - kvp.Value.LastUsed > _clientExpirationTime)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            // Remove and dispose expired clients
            foreach (var key in keysToRemove)
            {
                if (_httpClients.TryRemove(key, out var cacheItem))
                {
                    try
                    {
                        cacheItem.Client.Dispose();
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogWarning($"Error disposing HttpClient: {ex.Message}");
                    }
                }
            }

            if (keysToRemove.Count > 0)
            {
                appLogger.LogInformation($"Cleaned up {keysToRemove.Count} expired HttpClient instances");
            }

            _lastClientCleanupTime = now;
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, "Error during HttpClient cleanup");
        }
    }

    /// <summary>
    /// Creates a very simple HTTP/2 client
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public static HttpClient CreateSimpleHttp2Client(string url)
    {
        var uri = new Uri(url);
        var baseDomainName = $"{uri.Scheme}{Uri.SchemeDelimiter}{uri.Host}:{uri.Port}";
        return new HttpClient()
        {
            BaseAddress = new Uri(baseDomainName),
            DefaultRequestVersion = new Version(2, 0)
        };
    }

    /// <summary>
    /// Core method for sending HTTP requests with customizable configuration
    /// </summary>
    public async static Task<T> RemoteRequestSingleCustomizableClient<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, Version httpVersion, Int32 timeout, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        // Global variables
        var debugRoutine = false;
        var requestIdentifier = debugRoutine ? RandomString(2) : "";

        string? remoteContent = null;
        string? errorReason = null;
        string? errorContent = null;
        int responseStatusCode = -1;
        bool requestError = false;
        HttpRequestMessage? requestMessage = null;
        HttpResponseMessage? responseMessage = null;
        object? responseObject = null;

        //
        // => HTTP Clients cache cleanup of expired clients
        //
        CleanupExpiredHttpClients();

        // Get return type once
        var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

        try
        {
            // Validate URL
            if (string.IsNullOrEmpty(url))
            {
                var errorMessage = $"Web request not possible without a uri";
                appLogger.LogError($"{errorMessage}. stack-trace: {GetStackTrace()}");
                throw new Exception(errorMessage);
            }

            // Determine the mime type for the request
            string mimeTypePostedData = MediaTypeNames.Text.Plain;
            switch (returnType)
            {
                case "xml":
                    mimeTypePostedData = MediaTypeNames.Text.Xml;
                    break;
                case "string":
                    mimeTypePostedData = MediaTypeNames.Application.Json;
                    break;
            }

            // Create cache key for HTTP client
            Uri uri = new Uri(url);
            string httpClientKey = $"customizable{uri.Port}{timeout}";

            if (debugRoutine)
            {
                Console.WriteLine($"************ ({requestIdentifier})");
                Console.WriteLine($"url: {url}, httpClientKey: {httpClientKey}, method: {method}");
            }



            //
            // => Get or create a client from cache
            //
            HttpClient? httpClient = null;

            if (!_httpClients.TryGetValue(httpClientKey, out HttpClientCacheItem? cacheItem))
            {
                // - Create a new client

                if (debugRoutine) Console.WriteLine("Creating new HttpClient");


                var httpClientHandler = new HttpClientHandler
                {
                    // Make sure that we can decompress GZIP compressed responses
                    AutomaticDecompression = DecompressionMethods.GZip,

                    // Ignore certificate errors
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }
                };

                // Create the client based on HTTP version
                switch (httpVersion.Major)
                {
                    case 1:
                        httpClient = new HttpClient(httpClientHandler);
                        break;

                    case 2:
                        var baseDomainName = $"{uri.Scheme}{Uri.SchemeDelimiter}{uri.Host}:{uri.Port}";
                        httpClient = new HttpClient(httpClientHandler)
                        {
                            BaseAddress = new Uri(baseDomainName),
                            DefaultRequestVersion = new Version(2, 0)
                        };
                        break;

                    default:
                        throw new Exception($"Creating a client for HTTP version {httpVersion.ToString()} is not supported");
                }


                // Setup the http client
                httpClient.DefaultRequestHeaders.Connection.Clear();
                httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                httpClient.DefaultRequestHeaders.ExpectContinue = false;
                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));

                if (timeout > 0)
                {
                    httpClient.Timeout = TimeSpan.FromMilliseconds(NormalizeToMilliseconds(timeout));
                }


                // Add to cache
                cacheItem = new HttpClientCacheItem(httpClient);
                if (!_httpClients.TryAdd(httpClientKey, cacheItem))
                {
                    appLogger.LogWarning($"Unable to add http client with key {httpClientKey} in RemoteRequestSingleCustomizableClient()");
                    // Dispose if we couldn't add it
                    httpClient.Dispose();

                    // Try to get existing client
                    if (!_httpClients.TryGetValue(httpClientKey, out cacheItem))
                    {
                        throw new Exception($"Failed to retrieve HTTP client for key {httpClientKey}");
                    }
                }
            }
            else
            {
                if (debugRoutine) Console.WriteLine("Reusing HttpClient");

                // Update last used time
                cacheItem.UpdateLastUsed();
            }

            httpClient = cacheItem.Client;


            //
            // => Execute the request
            //
            string cachedRequestDataForLogging = null;
            try
            {
                // Cache request content for logging (before it gets consumed/disposed)
                try
                {
                    if (requestData != null && (method == RequestMethodEnum.Post || method == RequestMethodEnum.Put))
                    {
                        // Buffer the content into memory first to make it re-readable
                        // This is essential for StreamContent which can only be read once
                        await requestData.LoadIntoBufferAsync();

                        // Now we can safely read it for caching, and it can still be read again by SendAsync
                        cachedRequestDataForLogging = await requestData.ReadAsStringAsync();
                    }
                }
                catch (Exception ex)
                {
                    // If we can't read it, generate debug info
                    cachedRequestDataForLogging = GenerateDebugObjectString(requestData);
                    appLogger.LogWarning($"Unable to pre-read request data for logging... error: {ex}");
                }

                // Create HttpRequestMessage for thread-safe per-request headers
                HttpRequestMessage httpRequestMessage = null;

                try
                {
                    // Determine the HTTP method and URL
                    HttpMethod httpMethod;
                    string requestUrl = url;
                    HttpContent requestContent = null;

                    switch (method)
                    {
                        case RequestMethodEnum.Post:
                            httpMethod = HttpMethod.Post;
                            requestContent = requestData;
                            break;

                        case RequestMethodEnum.Put:
                            httpMethod = HttpMethod.Put;
                            requestContent = requestData;
                            break;

                        case RequestMethodEnum.Delete:
                            httpMethod = HttpMethod.Delete;
                            requestUrl = await _appendDataAsQuerystringToUrl(url, requestData);
                            break;

                        case RequestMethodEnum.Get:
                            httpMethod = HttpMethod.Get;
                            requestUrl = await _appendDataAsQuerystringToUrl(url, requestData);
                            break;

                        default:
                            WriteErrorMessageToConsole($"Method: '{RequestMethodEnumToString(method)}' not natively supported by the RemoteRequest function, fall back to a simple GET");
                            httpMethod = HttpMethod.Get;
                            requestUrl = await _appendDataAsQuerystringToUrl(url, requestData);
                            break;
                    }

                    // Create the request message
                    httpRequestMessage = new HttpRequestMessage(httpMethod, requestUrl);

                    // Add request content if needed
                    if (requestContent != null)
                    {
                        httpRequestMessage.Content = requestContent;
                    }

                    // Set headers on the request message (thread-safe per-request)
                    if (customHttpHeaders != null)
                    {
                        customHttpHeaders.SetHeaders(ref httpRequestMessage);
                    }
                    else
                    {
                        httpRequestMessage.Headers.Add("Accept", mimeTypePostedData);
                    }

                    // Execute the request using SendAsync
                    responseMessage = await httpClient.SendAsync(httpRequestMessage);
                }
                finally
                {
                    // Don't dispose HttpRequestMessage since the Content (requestData) was passed in from outside
                    // and we've already cached it for logging. Disposing would dispose content we don't own.
                    // The HttpClient will handle cleanup of the request resources.
                    httpRequestMessage = null;
                }

                // Grab the HTTP status code that the remote server returned
                responseStatusCode = (int)responseMessage.StatusCode;


                if (debugRoutine) Console.WriteLine($"Status code: {responseStatusCode}");

                // Process response
                if (responseMessage.IsSuccessStatusCode)
                {
                    // Memory-efficient reading for large responses
                    if ((returnType == "string" || returnType == "xml") && responseMessage.Content != null)
                    {
                        if (debugRoutine) Console.WriteLine($"!! In memory-efficient reading for large responses");
                        using (var stream = await responseMessage.Content.ReadAsStreamAsync())
                        {
                            // Use StringBuilder for efficient string building
                            var stringBuilder = new StringBuilder();

                            // Rent buffer from shared pool
                            byte[] buffer = ArrayPool<byte>.Shared.Rent(32 * 1024); // 32KB buffer
                            try
                            {
                                // Read in chunks
                                using (var reader = new StreamReader(stream, Encoding.UTF8, true, buffer.Length, leaveOpen: false))
                                {
                                    char[] charBuffer = ArrayPool<char>.Shared.Rent(16 * 1024); // 16KB char buffer
                                    try
                                    {
                                        int charsRead;
                                        while ((charsRead = await reader.ReadAsync(charBuffer, 0, charBuffer.Length)) > 0)
                                        {
                                            stringBuilder.Append(charBuffer, 0, charsRead);
                                        }
                                    }
                                    finally
                                    {
                                        ArrayPool<char>.Shared.Return(charBuffer);
                                    }
                                }

                                remoteContent = stringBuilder.ToString();
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }

                        if (debugRoutine && url.Contains("conversionservice"))
                        {
                            Console.WriteLine($"Response content character length: {remoteContent.Length}");
                        }
                    }
                    else
                    {
                        if (debugRoutine) Console.WriteLine($"In regular reading responses");
                        remoteContent = await responseMessage.Content.ReadAsStringAsync();
                    }
                }
                else
                {
                    errorReason = responseMessage.ReasonPhrase;
                    using (var stream = await responseMessage.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        errorContent = await reader.ReadToEndAsync();
                    }
                    requestError = true;
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("Connection refused"))
                {
                    appLogger.LogError($"There was an error executing the remote request and receiving the response in RemoteRequestSingleCustomizableClient<T>(). url: {url}, reason: Connection refused");
                }
                else
                {
                    appLogger.LogError(ex, $"There was an error executing the remote request and receiving the response in RemoteRequestSingleCustomizableClient<T>(). url: {url}");
                }
                requestError = true;
            }

            //
            // => Process response based on success/failure
            //
            if (!requestError)
            {
                // Process successful responses based on return type
                switch (returnType)
                {
                    case "xml":
                        if (debugRoutine && url.Contains("conversionservice"))
                        {
                            Console.WriteLine($"1) In parsing conversion service XML");
                            await TextFileCreateAsync(remoteContent, $"{logRootPathOs}/_conversionservice-response.txt");
                        }


                        // Handle XML responses
                        if (IsValidJson(remoteContent))
                        {
                            if (debugRoutine && url.Contains("conversionservice")) Console.WriteLine($"1.1) In parsing conversion service XML");

                            // Convert JSON to XML efficiently
                            responseObject = ConvertJsonToXml(remoteContent, "root");

                            if (debugRoutine && url.Contains("conversionservice")) Console.WriteLine($"1.2) Parsed XML character length: {((XmlDocument)responseObject).OuterXml.Length}");
                        }
                        else
                        {
                            try
                            {
                                if (remoteContent.Trim().StartsWith('<'))
                                {
                                    if (debugRoutine && url.Contains("conversionservice")) Console.WriteLine($"2.1) In parsing conversion service XML");


                                    // Use XDocument for memory efficiency
                                    var xdoc = XDocument.Parse(remoteContent);
                                    responseObject = xdoc.ToXmlDocument();


                                    if (debugRoutine && url.Contains("conversionservice")) Console.WriteLine($"2.2) In parsing conversion service XML");

                                    // responseObject = new XmlDocument();
                                    // ((XmlDocument)responseObject).LoadXml(remoteContent);

                                    if (debugRoutine && url.Contains("conversionservice")) Console.WriteLine($"2.3) Parsed XML character length: {((XmlDocument)responseObject).OuterXml.Length}");

                                }
                                else
                                {
                                    responseObject = GenerateSuccessXml("Successfully executed remote request", $"method: {RequestMethodEnumToString(method)}, url: {url}", remoteContent);
                                }
                            }
                            catch (Exception xmlEx)
                            {
                                // Handle XML parsing errors
                                var xmlResponse = new XmlDocument();
                                var responseRoot = xmlResponse.CreateElement("error");
                                responseRoot.AppendChild(xmlResponse.CreateElementWithText("message", "Unable to load the response from the webservice into an XmlDocument"));
                                responseRoot.AppendChild(xmlResponse.CreateElementWithText("debuginfo", xmlEx.ToString()));
                                responseRoot.AppendChild(xmlResponse.CreateElementWithText("httpresponse", remoteContent));
                                xmlResponse.AppendChild(responseRoot);
                                responseObject = xmlResponse;
                            }
                        }
                        break;

                    default:
                        // Default to returning string content
                        responseObject = remoteContent;
                        break;
                }
            }
            else
            {
                // Handle request errors
                // Use the cached request data that was read before sending
                var requestDataDebug = cachedRequestDataForLogging ?? "";

                // Truncate debug info for logging
                if (requestDataDebug.Length > 1024)
                    requestDataDebug = TruncateString(requestDataDebug, 1024);

                // URL decode if needed
                if (requestDataDebug.IsUrlEncoded())
                {
                    requestDataDebug = HttpUtility.UrlDecode(requestDataDebug);
                }

                // Prepare error information
                var errorMessage = "Status code of the remote response is not 'OK' or 'CREATED'";
                var errorDebugInfo = $"- URL: {url}\n- responseStatusCode: {responseStatusCode.ToString()}\n- method: {RequestMethodEnumToString(method)}\n- requestData: {requestDataDebug}\n- errorReason: {errorReason}";

                // Log errors unless suppressed
                if (!suppressResponseExceptionLogging)
                {
                    switch (responseStatusCode)
                    {
                        case 424:
                            appLogger.LogError($"ERROR finding asset in remote service");
                            break;

                        default:
                            appLogger.LogError($"ERROR while retrieving remote resource in RemoteRequestSingleCustomizableClient<T>().\n{errorDebugInfo}\n- errorContent: {((responseStatusCode == 404) ? TruncateString(errorContent, 500) : errorContent)}\n- incoming-request: {RenderHttpRequestDebugInformation()}\n- stack-trace: {CreateStackInfo()}");
                            break;
                    }
                }

                // Create appropriate error response based on return type
                switch (returnType)
                {
                    case "xml":
                        var xmlResponse = new XmlDocument();
                        CreateXmlErrorResponse(xmlResponse, errorMessage, errorDebugInfo, responseStatusCode, errorContent);
                        responseObject = xmlResponse;
                        break;

                    default:
                        responseObject = CreateStringErrorResponse(errorReason, errorDebugInfo, errorContent, responseStatusCode);
                        break;
                }
            }


            if (debugRoutine) Console.WriteLine($"************ ({requestIdentifier})");

            //
            // => Return the response object cast to the requested type
            //
            return (T)Convert.ChangeType(responseObject, typeof(T));
        }
        catch (Exception we)
        {
            // Handle exceptions
            var debugUrl = (method == RequestMethodEnum.Post || method == RequestMethodEnum.Put) ?
                url :
                (await _appendDataAsQuerystringToUrl(url, requestData)).Replace("&amp;", "&");

            string errorDebugInfo = $"Remote URL: '{debugUrl}',\n" +
                (!string.IsNullOrEmpty(errorContent) ? $" Response content: '{errorContent}',\n" : "") +
                $" Error message: {we.Message},\n Error details: '{we.ToString()}'";

            string errorMessage = we.Message;

            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = $"The remote webserver could not be reached. The server might be down.";

            if (responseStatusCode > 299 || responseStatusCode == -1)
            {
                errorMessage = HttpStatusCodeToMessage(responseStatusCode);
            }

            // Return appropriate error response based on the requested return type
            switch (returnType)
            {
                case "xml":
                    var xmlResponse = new XmlDocument();
                    CreateXmlErrorResponse(xmlResponse, errorMessage, errorDebugInfo, responseStatusCode, errorContent);
                    return (T)Convert.ChangeType(xmlResponse, typeof(T));

                default:
                    return (T)Convert.ChangeType(CreateStringErrorResponse(errorMessage, errorDebugInfo, errorContent, responseStatusCode), typeof(T));
            }
        }
        finally
        {
            // Clean up resources to prevent memory leaks
            if (requestMessage != null)
            {
                requestMessage.Dispose();
            }

            if (responseMessage != null)
            {
                if (responseMessage.Content != null)
                {
                    responseMessage.Content.Dispose();
                }
                responseMessage.Dispose();
            }

            // Release large strings to help garbage collection
            if (remoteContent != null)
            {
                var triggerGarbageCollection = remoteContent.Length > 1_000_000;
                remoteContent = null;

                if (debugRoutine)
                {
                    Console.WriteLine("------------------------------------------");
                    Console.WriteLine("Garbage collection start.");
                }

                if (triggerGarbageCollection)
                {
                    GC.Collect();
                    // GC.Collect(0, GCCollectionMode.Optimized);
                    GC.WaitForPendingFinalizers();
                }

                if (debugRoutine)
                {
                    Console.WriteLine("Garbage collection done.");
                    Console.WriteLine("------------------------------------------");
                }
            }
        }

    }


    /// <summary>
    /// Implementation for clients with predefined headers
    /// Delegates to the customizable client implementation
    /// </summary>
    public async static Task<T> RemoteRequestSinglePredefinedClient<T>(RequestMethodEnum method, string url, dynamic requestData, CustomHttpHeaders customHttpHeaders, Version httpVersion, Int32 timeout, bool debug = false, bool suppressResponseExceptionLogging = false)
    {
        // For predefined clients, we use the same implementation but with different header handling
        // For now, delegate to the customizable implementation
        return await RemoteRequestSingleCustomizableClient<T>(method, url, requestData, customHttpHeaders, httpVersion, timeout, debug, suppressResponseExceptionLogging);
    }

    /// <summary>
    /// Fills the xmlResponse object with error information
    /// </summary>
    private static void CreateXmlErrorResponse(XmlDocument xmlResponse, string message = null, string debugInfo = null, int httpStatusCode = -1, string httpResponse = null)
    {
        var nodeRoot = xmlResponse.CreateElement("error");
        nodeRoot.AppendChild(xmlResponse.CreateElementWithText("message", message ?? string.Empty));
        nodeRoot.AppendChild(xmlResponse.CreateElementWithText("debuginfo", debugInfo ?? string.Empty));
        nodeRoot.AppendChild(xmlResponse.CreateElementWithText("httpstatuscode", httpStatusCode.ToString()));
        nodeRoot.AppendChild(xmlResponse.CreateElementWithText("httpresponse", string.Empty));
        xmlResponse.AppendChild(nodeRoot);

        try
        {
            if (IsValidJson(httpResponse))
            {
                var jsonXml = ConvertJsonToXml(httpResponse, "root");
                var nodeImported = xmlResponse.ImportNode(jsonXml.DocumentElement, true);
                xmlResponse.SelectSingleNode("/error/httpresponse").AppendChild(nodeImported);
                SetAttribute(xmlResponse.SelectSingleNode("/error/httpresponse"), "formatOriginal", "json");
            }
            else
            {
                xmlResponse.SelectSingleNode("/error/httpresponse").InnerText = httpResponse?.Trim() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            appLogger.LogInformation($"Could not parse response content. error: {ex}");
            xmlResponse.SelectSingleNode("/error/httpresponse").InnerText = httpResponse?.Trim() ?? string.Empty;
        }

        // Ensure all nodes are properly set
        xmlResponse.SelectSingleNode("/error/debuginfo").InnerText = debugInfo ?? string.Empty;
        xmlResponse.SelectSingleNode("/error/message").InnerText = message ?? string.Empty;
        xmlResponse.SelectSingleNode("/error/httpstatuscode").InnerText = httpStatusCode.ToString();
    }

    /// <summary>
    /// Creates a formatted error response for string return types
    /// </summary>
    private static string CreateStringErrorResponse(string message, string debugInfo, string errorContent, int responseStatusCode)
    {
        return $"ERROR:<br/><b>{message}</b><p>Response:<br/>{errorContent}</p><p>Debug info:<br/>{debugInfo}</p><p>HTTP Status Code: {responseStatusCode.ToString()}</p>";
    }
    #endregion



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
            return $"?{String.Join("&", _fields.Select(pair => $"{HttpUtility.UrlEncode(pair.Key)}={HttpUtility.UrlEncode(pair.Value)}"))}";
        }

        public static QueryParamBuilder New => new();
    }
}

