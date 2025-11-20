using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Contains helper functions for working with HttpRequest and HttpResponse objects
/// </summary>
public abstract partial class Framework
{

    /// <summary>
    /// Extracts information from the incoming request to display as debug information
    /// Inspired on: https://gist.github.com/elanderson/c50b2107de8ee2ed856353dfed9168a2 and https://elanderson.net/2017/02/log-requests-and-responses-in-asp-net-core/
    /// </summary>
    /// <returns>The request.</returns>
    /// <param name="request">Request.</param>
    public static async Task<string> DebugIncomingRequest(HttpRequest request, int maxLength = 400)
    {

        string? bodyAsText = null;

        var contentType = request.ContentType;
        var streamIsNull = request.Body == Stream.Null;
        var isHttpGet = request.Method == "GET";
        //var streamIsRewindable = request.Body.CanSeek;
        var isFormUrlEncoded = contentType != null && CultureInfo.InvariantCulture.CompareInfo.IndexOf(contentType, "application/x-www-form-urlencoded", CompareOptions.IgnoreCase) >= 0;

        // A) Retrieve the body content if available
        if (!streamIsNull && !isHttpGet && request.Body.CanSeek)
        {
            // Store the request body
            var body = request.Body;
            if (isFormUrlEncoded)
            {
                // Retrieve the form fields that were posted
                var formData = await request.ReadFormAsync();

                foreach (string key in formData.Keys)
                {
                    var value = "";
                    try
                    {
                        value = TruncateString(formData[key], maxLength);
                    }
                    catch (Exception ex)
                    {
                        value = $"[could not retrieve value: {ex}]";
                    }

                    bodyAsText += $"- {key}: {value}\n";
                }

            }
            else
            {
                // Retrieve the body content that was posted
                var buffer = new byte[Convert.ToInt32(request.ContentLength)];
                
                await ReadExactlyAsync(request.Body, buffer, 0, buffer.Length);

                bodyAsText = TruncateString(Encoding.UTF8.GetString(buffer), maxLength);

                // bodyAsText = await new StreamReader(request.Body).ReadToEndAsync();
            }

            // Set the request body back to it's original value so that we can use it in further processing
            request.Body.Position = 0;
            request.Body = body;
        }

        // B) Retrieve the headers
        List<string> requestHeaders = new List<string>();
        var uniqueRequestHeaders = request.Headers
            .Where(x => requestHeaders.All(r => r != x.Key))
            .Select(x => x.Key);

        requestHeaders.AddRange(uniqueRequestHeaders);

        // Build the string
        StringBuilder httpRequestHeaderInfo = new StringBuilder();
        foreach (string httpHeaderKey in requestHeaders.OrderBy(x => x))
        {
            string currentServerVariable = request.RetrieveFirstHeaderValueOrDefault<string>(httpHeaderKey);
            httpRequestHeaderInfo.Append($"{httpHeaderKey}: {currentServerVariable}").AppendLine();
        }

        // Build the string containing the debug information
        var info = $"General:\nRequest URL: {request.Scheme}://{request.Host}{request.Path}{request.QueryString}\nRequest Method: {request.Method}\n\nRequest Headers:\n{httpRequestHeaderInfo.ToString()}";

        // Add information about the body content
        if (!streamIsNull && !isHttpGet)
        {
            if (isFormUrlEncoded)
            {
                info += $"\n\nForm data:\n{bodyAsText}";
            }
            else
            {
                info += $"\n\nBody (request.ContentLength: {request.ContentLength}):\n{bodyAsText}";
            }
        }

        return info;
    }

    /// <summary>
    /// Enumerator for HTTP request methods - replaces System.Net.Http.HttpMethod because those methods are not defined as enumerators
    /// </summary>
    public enum RequestMethodEnum
    {
        Get,
        Post,
        Put,
        Delete,
        Head,
        Trace
    }

    /// <summary>
    /// Converts an HTTP method in string format to HttpMethos enumerable
    /// </summary>
    /// <returns>The method from string.</returns>
    /// <param name="method">Method.</param>
    public static RequestMethodEnum RequestMethodEnumFromString(string method)
    {
        switch (method.ToLower())
        {
            case "get":
                return RequestMethodEnum.Get;

            case "post":
                return RequestMethodEnum.Post;

            case "put":
                return RequestMethodEnum.Put;

            case "delete":
                return RequestMethodEnum.Delete;

            case "head":
                return RequestMethodEnum.Head;

            case "trace":
                return RequestMethodEnum.Trace;

            default:
                // Default to GET, but this clause should never be reached
                return RequestMethodEnum.Get;
        }
    }

    /// <summary>
    /// Converts a RequestMethodEnum to string.
    /// </summary>
    /// <returns>String version of the RequestMethodEnum</returns>
    /// <param name="requestMethodEnum">Request method enum.</param>
    public static string RequestMethodEnumToString(RequestMethodEnum requestMethodEnum)
    {
        switch (requestMethodEnum)
        {
            case RequestMethodEnum.Get:
                return "GET";

            case RequestMethodEnum.Post:
                return "POST";

            case RequestMethodEnum.Put:
                return "PUT";

            case RequestMethodEnum.Delete:
                return "DELETE";

            case RequestMethodEnum.Head:
                return "HEAD";

            case RequestMethodEnum.Trace:
                return "TRACE";

            default:
                return "UNKNOWN";
        }
    }

    /// <summary>
    /// Converts an HTTP status code to a readable message
    /// </summary>
    /// <returns>The status code to message.</returns>
    /// <param name="responseStatusCode">Response status code.</param>
    /// <param name="requestMethodEnum">Request method enum.</param>
    public static string HttpStatusCodeToMessage(string responseStatusCode, RequestMethodEnum requestMethodEnum = RequestMethodEnum.Get)
    {
        // Convert the status code to an integer
        int statusCode = 0;
        Int32.TryParse(responseStatusCode, out statusCode);

        return HttpStatusCodeToMessage(statusCode, requestMethodEnum);
    }

    /// <summary>
    /// Converts an HTTP status code to a readable message
    /// </summary>
    /// <returns>The status code to message.</returns>
    /// <param name="responseStatusCode">Response status code.</param>
    /// <param name="requestMethodEnum">Request method enum.</param>
    public static string HttpStatusCodeToMessage(int responseStatusCode, RequestMethodEnum requestMethodEnum = RequestMethodEnum.Get)
    {
        switch (responseStatusCode)
        {
            case 200:
            case 204:
            case 304:
                return "OK";

                // Special non-standard value
            case -1:
                return $"Unable to reach the server, the server might be down or too busy";

            case 400:
                return $"Remote server returned a bad request 400 result";

            case 403:
                return $"Not allowed to visit this resource on the remote webserver";

            case 404:
                return $"The resource on the remote webserver could not be found";

            case 405:
                return $"The remote resource does not allow {RequestMethodEnumToString(requestMethodEnum)} to be used";

            case 500:
                return $"An error occured at the remote webserver";

            case 503:
                return $"Server not available";

            default:
                return $"An unknown http error '{responseStatusCode}' has occured while requesting the remote resource";
        }
    }

}