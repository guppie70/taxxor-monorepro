using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using FrameworkLibrary;
using FrameworkLibrary.models;
using Microsoft.Extensions.Logging;
using static FrameworkLibrary.FrameworkResponseType;
using static FrameworkLibrary.FrameworkEncryption;

namespace FrameworkLibrary.utilities
{

    /// <summary>
    /// Object to be used in SOAP, REST, Web Requests and define custom HTTP Request Header properties
    /// </summary>
    public class CustomHttpHeaders
    {
        public string? Accept = null;
        public ReturnTypeEnum RequestType;
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
        public Dictionary<string, string> CustomHeaders = [];

        private bool _isSoapRequest = false;

        public CustomHttpHeaders()
        {
            CustomHeaders.Clear();
            RequestType = ReturnTypeEnum.None;
        }

        public CustomHttpHeaders(string type)
        {
            CustomHeaders.Clear();
            switch (type)
            {
                case "json":
                    RequestType = ReturnTypeEnum.Json;
                    Accept = MediaTypeNames.Application.Json;
                    break;

                case "xml":
                    RequestType = ReturnTypeEnum.Xml;
                    Accept = MediaTypeNames.Text.Xml;
                    break;

                default:
                    // accessing logger not possible from a modern object
                    //appLogger.LogWarning($"Custom HTTP Header object could not be setup for {type}");
                    //RequestType = ReturnTypeEnum.None;
                    //break;
                    throw new InvalidOperationException($"Custom HTTP Header object could not be setup for {type}");
            }
        }

        /// <summary>
        /// Attaches HTTP request headers to an HttpClient request
        /// </summary>
        /// <param name="httpClient">Http client.</param>
        public void SetHeaders(ref HttpClient httpClient)
        {
            // Clear any existing headers that the client may have defined
            httpClient.DefaultRequestHeaders.Clear();

            if (Accept != null)
            {
                if (httpClient.DefaultRequestHeaders.Contains("Accept")) 
                    httpClient.DefaultRequestHeaders.Remove("Accept");
                httpClient.DefaultRequestHeaders.Add("Accept", Accept);
            }
            else
            {
                if (RequestType != ReturnTypeEnum.None)
                {
                    if (httpClient.DefaultRequestHeaders.Contains("Accept")) 
                        httpClient.DefaultRequestHeaders.Remove("Accept");
                    httpClient.DefaultRequestHeaders.Add("Accept", ReturnTypeEnumToMime(RequestType));
                }
            }
            if (Connection != null) 
                httpClient.DefaultRequestHeaders.Add("Connection", Connection);
            if (ContentLength > -1) 
                httpClient.DefaultRequestHeaders.Add("Content-Length", ContentLength.ToString());
            if (Expect != null) 
                httpClient.DefaultRequestHeaders.Add("Expect", Expect);
            if (Date != new DateTime()) 
                httpClient.DefaultRequestHeaders.Add("Date", Date.ToString());
            if (Host != null) 
                httpClient.DefaultRequestHeaders.Add("Host", Host);
            if (IfModifiedSince != new DateTime()) 
                httpClient.DefaultRequestHeaders.Add("If-Modified-Since", IfModifiedSince.ToString());
            if (Referer != null) 
                httpClient.DefaultRequestHeaders.Add("Referer", Referer);
            if (TransferEncoding != null) 
                httpClient.DefaultRequestHeaders.Add("Transfer-Encoding", TransferEncoding);
            if (UserAgent != null) 
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

            if (DebugMode) 
                httpClient.DefaultRequestHeaders.Add("X-Tx-Debug", "true");

            // Set special custom headers
            foreach (KeyValuePair<string, string> pair in CustomHeaders)
            {
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
                if (RequestType != ReturnTypeEnum.None)
                {
                    httpRequestMessage.Headers.Add("Accept", ReturnTypeEnumToMime(RequestType));
                }
            }
            if (Connection != null) 
                httpRequestMessage.Headers.Add("Connection", Connection);
            if (ContentLength > -1) 
                httpRequestMessage.Headers.Add("Content-Length", ContentLength.ToString());
            if (Expect != null) 
                httpRequestMessage.Headers.Add("Expect", Expect);
            if (Date != new DateTime()) 
                httpRequestMessage.Headers.Add("Date", Date.ToString());
            if (Host != null) 
                httpRequestMessage.Headers.Add("Host", Host);
            if (IfModifiedSince != new DateTime()) 
                httpRequestMessage.Headers.Add("If-Modified-Since", IfModifiedSince.ToString());
            if (Referer != null) 
                httpRequestMessage.Headers.Add("Referer", Referer);
            if (TransferEncoding != null) 
                httpRequestMessage.Headers.Add("Transfer-Encoding", TransferEncoding);
            if (UserAgent != null) 
                httpRequestMessage.Headers.Add("User-Agent", UserAgent);

            if (DebugMode) 
                httpRequestMessage.Headers.Add("X-Tx-Debug", "true");

            // Set special custom headers
            foreach (KeyValuePair<string, string> pair in CustomHeaders)
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
            // no need to call ContainsKey as Remove will do nothing if the key does not exist
            //if (CustomHeaders.ContainsKey(requestHeaderKey))
            CustomHeaders.Remove(requestHeaderKey);

            CustomHeaders.Add(requestHeaderKey, requestHeaderValue);
            if (requestHeaderKey.Contains("soap", StringComparison.CurrentCultureIgnoreCase) || requestHeaderValue.Contains("soap", StringComparison.CurrentCultureIgnoreCase))
            {
                _isSoapRequest = true;
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
                if (RequestType != ReturnTypeEnum.None)
                {
                    return ReturnTypeEnumToMime(RequestType);
                }
            }

            return "*/*";
        }

        public string GetHash()
        {
            List<string> headers = [];

            if (Accept != null)
            {
                headers.Add(Accept);
            }
            else
            {
                if (RequestType != ReturnTypeEnum.None)
                {
                    headers.Add(ReturnTypeEnumToMime(RequestType));
                }
            }
            if (Connection != null) 
                headers.Add(Connection);
            // if (ContentLength > -1) headers.Add(ContentLength.ToString());
            if (Expect != null) 
                headers.Add(Expect);
            // if (Date != new DateTime()) headers.Add(Accept);
            if (Host != null) 
                headers.Add(Host);
            // if (IfModifiedSince != new DateTime()) headers.Add(Accept);
            if (Referer != null) 
                headers.Add(Referer);
            if (TransferEncoding != null) 
                headers.Add(TransferEncoding);
            if (UserAgent != null) 
                headers.Add(UserAgent);

            if (DebugMode) 
                headers.Add(DebugMode.ToString());

            // Set special custom headers
            foreach (KeyValuePair<string, string> pair in CustomHeaders)
            {
                headers.Add(pair.Value);
            }

            return md5(string.Join("", headers.ToArray()));
        }

        /// <summary>
        /// Retrieves if this request is a SOAP request of not
        /// </summary>
        /// <returns><c>true</c>, if SOAP request was ised, <c>false</c> otherwise.</returns>
        public bool IsSoapRequest()
        {
            return _isSoapRequest;
        }
    }
}