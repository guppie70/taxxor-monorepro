using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using Microsoft.Extensions.Logging;

/// <summary>
/// Object to be used in SOAP, REST, Web Requests and define custom HTTP Request Header properties
/// </summary>
public class CustomHttpHeaders
{
    public string? Accept = null;
    public Framework.ReturnTypeEnum RequestType;
    public string? Connection = null;
    //public bool KeepAlive = true;
    public int ContentLength = -1;
    public string? Expect = null;
    public DateTime Date;
    public string? Host = null;
    public DateTime IfModifiedSince;
    public string? Referer = null;
    public string? TransferEncoding = null;
    public string? UserAgent = null;
    public bool DebugMode = false;
    public string SoapVersion = "1.2";
    public Dictionary<string, string> CustomHeaders = new Dictionary<string, string>();

    private bool _isSoapRequest = false;

    public CustomHttpHeaders()
    {
        this.CustomHeaders.Clear();
        this.RequestType = Framework.ReturnTypeEnum.None;
    }

    public CustomHttpHeaders(string type)
    {
        this.CustomHeaders.Clear();
        switch (type)
        {
            case "json":
                this.RequestType = Framework.ReturnTypeEnum.Json;
                this.Accept = MediaTypeNames.Application.Json;
                break;

            case "xml":
                this.RequestType = Framework.ReturnTypeEnum.Xml;
                this.Accept = MediaTypeNames.Text.Xml;
                break;

            default:
                Framework.appLogger.LogWarning($"Custom HTTP Header object could not be setup for {type}");
                this.RequestType = Framework.ReturnTypeEnum.None;
                break;
        }
    }

    /// <summary>
    /// Attaches HTTP request headers to an HttpClient request
    /// NOTE: This method should only be used for setting default headers that apply to all requests.
    /// For per-request headers, use SetHeaders(HttpRequestMessage) instead to avoid thread safety issues.
    /// </summary>
    /// <param name="httpClient">Http client.</param>
    [Obsolete("Use SetHeaders(HttpRequestMessage) for per-request headers to ensure thread safety")]
    public void SetHeaders(ref HttpClient httpClient)
    {
        // WARNING: Do not clear DefaultRequestHeaders as HttpClient instances are shared across threads
        // httpClient.DefaultRequestHeaders.Clear(); // REMOVED - causes thread safety issues

        // Only add headers if they don't already exist to avoid duplicates
        if (Accept != null)
        {
            if (!httpClient.DefaultRequestHeaders.Contains("Accept"))
                httpClient.DefaultRequestHeaders.Add("Accept", Accept);
        }
        else
        {
            if (RequestType != Framework.ReturnTypeEnum.None)
            {
                if (!httpClient.DefaultRequestHeaders.Contains("Accept"))
                    httpClient.DefaultRequestHeaders.Add("Accept", Framework.ReturnTypeEnumToMime(RequestType));
            }
        }

        // Only add other headers if they don't exist
        if (Connection != null && !httpClient.DefaultRequestHeaders.Contains("Connection"))
            httpClient.DefaultRequestHeaders.Add("Connection", Connection);
        if (ContentLength > -1 && !httpClient.DefaultRequestHeaders.Contains("Content-Length"))
            httpClient.DefaultRequestHeaders.Add("Content-Length", ContentLength.ToString());
        if (Expect != null && !httpClient.DefaultRequestHeaders.Contains("Expect"))
            httpClient.DefaultRequestHeaders.Add("Expect", Expect);
        if (Date != new DateTime() && !httpClient.DefaultRequestHeaders.Contains("Date"))
            httpClient.DefaultRequestHeaders.Add("Date", Date.ToString());
        if (Host != null && !httpClient.DefaultRequestHeaders.Contains("Host"))
            httpClient.DefaultRequestHeaders.Add("Host", Host);
        if (IfModifiedSince != new DateTime() && !httpClient.DefaultRequestHeaders.Contains("If-Modified-Since"))
            httpClient.DefaultRequestHeaders.Add("If-Modified-Since", IfModifiedSince.ToString());
        if (Referer != null && !httpClient.DefaultRequestHeaders.Contains("Referer"))
            httpClient.DefaultRequestHeaders.Add("Referer", Referer);
        if (TransferEncoding != null && !httpClient.DefaultRequestHeaders.Contains("Transfer-Encoding"))
            httpClient.DefaultRequestHeaders.Add("Transfer-Encoding", TransferEncoding);
        if (UserAgent != null && !httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        if (DebugMode && !httpClient.DefaultRequestHeaders.Contains("X-Tx-Debug"))
            httpClient.DefaultRequestHeaders.Add("X-Tx-Debug", "true");

        // Set special custom headers only if they don't exist
        foreach (var pair in CustomHeaders)
        {
            if (!httpClient.DefaultRequestHeaders.Contains(pair.Key))
                httpClient.DefaultRequestHeaders.Add(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// Sets HTTP headers on an HttpRequestMessage
    /// </summary>
    /// <param name="httpRequestMessage"></param>
    public void SetHeaders(ref HttpRequestMessage httpRequestMessage)
    {
        if (Accept != null)
        {
            httpRequestMessage.Headers.Add("Accept", Accept);
        }
        else
        {
            if (RequestType != Framework.ReturnTypeEnum.None)
            {
                httpRequestMessage.Headers.Add("Accept", Framework.ReturnTypeEnumToMime(RequestType));
            }
        }
        if (Connection != null) httpRequestMessage.Headers.Add("Connection", Connection);
        if (ContentLength > -1) httpRequestMessage.Headers.Add("Content-Length", ContentLength.ToString());
        if (Expect != null) httpRequestMessage.Headers.Add("Expect", Expect);
        if (Date != new DateTime()) httpRequestMessage.Headers.Add("Date", Date.ToString());
        if (Host != null) httpRequestMessage.Headers.Add("Host", Host);
        if (IfModifiedSince != new DateTime()) httpRequestMessage.Headers.Add("If-Modified-Since", IfModifiedSince.ToString());
        if (Referer != null) httpRequestMessage.Headers.Add("Referer", Referer);
        if (TransferEncoding != null) httpRequestMessage.Headers.Add("Transfer-Encoding", TransferEncoding);
        if (UserAgent != null) httpRequestMessage.Headers.Add("User-Agent", UserAgent);

        if (DebugMode) httpRequestMessage.Headers.Add("X-Tx-Debug", "true");

        // Set special custom headers
        foreach (var pair in CustomHeaders)
        {
            httpRequestMessage.Headers.Add(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// Adds a custom header for the remote request.
    /// </summary>
    /// <param name="requestHeaderKey">Request header key.</param>
    /// <param name="requestHeaderValue">Request header value.</param>
    public void AddCustomHeader(string requestHeaderKey, string requestHeaderValue)
    {
        if (CustomHeaders.ContainsKey(requestHeaderKey))
        {
            CustomHeaders.Remove(requestHeaderKey);
        }

        CustomHeaders.Add(requestHeaderKey, requestHeaderValue);
        if (requestHeaderKey.ToLower().Contains("soap") || requestHeaderValue.ToLower().Contains("soap"))
        {
            this._isSoapRequest = true;
        }
    }

    /// <summary>
    /// Retrieves the MIME type for this request
    /// </summary>
    /// <returns>The request MIME type.</returns>
    public string RetrieveRequestMimeType()
    {
        if (Accept != null)
        {
            return Accept;
        }
        else
        {
            if (RequestType != Framework.ReturnTypeEnum.None)
            {
                return Framework.ReturnTypeEnumToMime(RequestType);
            }
        }

        return "*/*";
    }

    public string GetHash()
    {
        var headers = new List<string>();

        if (Accept != null)
        {
            headers.Add(Accept);
        }
        else
        {
            if (RequestType != Framework.ReturnTypeEnum.None)
            {
                headers.Add(Framework.ReturnTypeEnumToMime(RequestType));
            }
        }
        if (Connection != null) headers.Add(Connection);
        // if (ContentLength > -1) headers.Add(ContentLength.ToString());
        if (Expect != null) headers.Add(Expect);
        // if (Date != new DateTime()) headers.Add(Accept);
        if (Host != null) headers.Add(Host);
        // if (IfModifiedSince != new DateTime()) headers.Add(Accept);
        if (Referer != null) headers.Add(Referer);
        if (TransferEncoding != null) headers.Add(TransferEncoding);
        if (UserAgent != null) headers.Add(UserAgent);

        if (DebugMode) headers.Add(DebugMode.ToString());

        // Set special custom headers
        foreach (var pair in CustomHeaders)
        {
            headers.Add(pair.Value);
        }

        return Framework.md5(string.Join("", headers.ToArray()));
    }

    /// <summary>
    /// Retrieves if this request is a SOAP request of not
    /// </summary>
    /// <returns><c>true</c>, if SOAP request was ised, <c>false</c> otherwise.</returns>
    public bool IsSoapRequest()
    {
        return this._isSoapRequest;
    }
}