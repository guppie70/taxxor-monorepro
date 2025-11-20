using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using static Framework;

public static class HttpContextExtensions
{
    /// <summary>
    /// Retrieves the first HTTP header value if available
    /// </summary>
    /// <returns>The first header value or default.</returns>
    /// <param name="request">Request.</param>
    /// <param name="headerKey">Header key.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public static T? RetrieveFirstHeaderValueOrDefault<T>(this HttpRequest request, string headerKey)
    {
        // Default value to return
        var returnValue = default(T);

        // Parse the value from the headers collection
        request.Headers.TryGetValue(headerKey, out StringValues headerValues);

        // Attempt to find the HTTP header value
        if (!StringValues.IsNullOrEmpty(headerValues))
        {
            using (StringValues.Enumerator headerEnumerator = headerValues.GetEnumerator())
            {
                using (IEnumerator<string> enumer = headerValues.GetEnumerator())
                {
                    // Immediately return the first value
                    if (enumer.MoveNext()) return (T)Convert.ChangeType(enumer.Current, typeof(T));
                }
            }
        }

        return returnValue;
    }

    /// <summary>
    /// Retrieve the raw body as a string from the Request.Body stream
    /// </summary>
    /// <param name="request">Request instance to apply to</param>
    /// <param name="encoding">Optional - Encoding, defaults to UTF8</param>
    /// <returns></returns>
    public static async Task<string> RetrieveRawBodyStringAsync(this HttpRequest request, Encoding encoding = null)
    {
        encoding ??= Encoding.UTF8;

        using StreamReader reader = new(request.Body, encoding);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Retrieves the raw body as a byte array from the Request.Body stream
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static async Task<byte[]> RetrieveRawBodyBytesAsync(this HttpRequest request)
    {
        using (var ms = new MemoryStream(2048))
        {
            await request.Body.CopyToAsync(ms);
            return ms.ToArray();
        }
    }

    public static string? GetAntiforgeryToken(this HttpContext httpContext)
    {
        var antiforgery = (IAntiforgery)httpContext.RequestServices.GetService(typeof(IAntiforgery));
        var tokenSet = antiforgery.GetAndStoreTokens(httpContext);
        string fieldName = tokenSet.FormFieldName;
        string? requestToken = tokenSet.RequestToken;
        return requestToken;
    }

    /// <summary>
    /// Returns true if the request headers contain the special debug header
    /// </summary>
    /// <returns><c>true</c>, if debug header was containsed, <c>false</c> otherwise.</returns>
    /// <param name="request">Request.</param>
    public static bool ContainsDebugHeader(this HttpRequest request)
    {
        return String.Equals(request.Headers["X-Tx-Debug"], "true", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Helper class to determine the string content and the mimetype to return to the client
    /// </summary>
    private class _ResponseContentHelper
    {
        public string content { get; }
        public Framework.ReturnTypeEnum returnTypeEnum { get; }

        public _ResponseContentHelper(dynamic contentForClient, Framework.ReturnTypeEnum? returnType = null)
        {
            // Converts the generic input object to a string and optionally attempts to guess the return type to use
            switch (Type.GetTypeCode(contentForClient.GetType()))
            {
                case TypeCode.String:
                case TypeCode.Boolean:
                    content = (string)contentForClient;
                    returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Html;
                    break;

                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                    content = contentForClient.ToString();
                    returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Html;
                    break;

                default:

                    switch (contentForClient.GetType().Name.ToString())
                    {
                        case "XmlDocument":
                            // Do some magic string conversion here
                            var xmlDocument = (XmlDocument)contentForClient;

                            // Strip debug information on production and staging systems
                            if (IsStandardEnvelope(xmlDocument) && (siteType == "prev" || siteType == "prod") && applicationType == "webapp") StripDebugInfo(ref xmlDocument);

                            // Generate the output content
                            if (returnType == Framework.ReturnTypeEnum.Json)
                            {
                                content = Framework.ConvertToJson(xmlDocument);
                                returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Json;
                            }
                            else
                            {
                                content = xmlDocument.OuterXml;
                                returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Xml;
                            }
                            break;

                        case "TaxxorReturnMessage":
                            var returnMessage = (TaxxorReturnMessage)contentForClient;
                            XmlDocument xmlReturn = returnMessage.ToXml();

                            // Strip debug information on production and staging systems
                            if (siteType == "prev" || siteType == "prod" && applicationType == "webapp") StripDebugInfo(ref xmlReturn);

                            // Generate the output content
                            switch (returnType)
                            {
                                case ReturnTypeEnum.Xml:
                                case ReturnTypeEnum.Html:

                                    content = xmlReturn.OuterXml;
                                    returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Xml;

                                    break;

                                case ReturnTypeEnum.Json:
                                    content = Framework.ConvertToJson(xmlReturn);
                                    returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Json;
                                    break;

                                default:
                                    content = returnMessage.ToString();
                                    returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Txt;
                                    break;
                            }

                            break;

                        case "TaxxorLogReturnMessage":
                            var returnLogMessage = (TaxxorLogReturnMessage)contentForClient;
                            XmlDocument xmlLogReturn = returnLogMessage.ToXml();

                            // Strip debug information on production and staging systems
                            if (siteType == "prev" || siteType == "prod" && applicationType == "webapp") StripDebugInfo(ref xmlLogReturn);

                            // Generate the output content
                            switch (returnType)
                            {
                                case ReturnTypeEnum.Xml:
                                case ReturnTypeEnum.Html:

                                    content = xmlLogReturn.OuterXml;
                                    returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Xml;

                                    break;

                                case ReturnTypeEnum.Json:
                                    content = Framework.ConvertToJson(xmlLogReturn);
                                    returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Json;
                                    break;

                                default:
                                    content = returnLogMessage.ToString();
                                    returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Txt;
                                    break;
                            }

                            break;

                        default:
                            Console.WriteLine("----------- ERROR -----------------");
                            Console.WriteLine($"unsupported object to convert to string contentForClient.GetType(): {contentForClient.GetType()}, contentForClient.GetType().Name: {contentForClient.GetType().Name}");
                            Console.WriteLine("-----------------------------------");

                            content = contentForClient.ToString();
                            returnTypeEnum = returnType ?? Framework.ReturnTypeEnum.Txt;
                            break;
                    }

                    break;
            }
        }

    }


    /// <summary>
    /// Writes a response back to the web client
    /// </summary>
    /// <returns>The response.</returns>
    /// <param name="response">Response.</param>
    /// <param name="statusCode">Status code.</param>
    /// <param name="contentForClient">Content for client.</param>
    /// <param name="returnTypeString">Return type.</param>
    /// <param name="disableCache">If set to <c>true</c> disable cache.</param>
    /// <param name="attachmentMode">Defines if we need to serve the content as a download</param>
    /// <param name="attachmentFileName">Defines the name of the file we want as a download</param>
    private async static Task _ClientResponse(HttpResponse response, int statusCode, dynamic contentForClient, string returnTypeString, bool disableCache, bool attachmentMode = false, string attachmentFileName = null, bool disableChunkedTransferEncoding = false)
    {
        ReturnTypeEnum returnType = Framework.GetReturnTypeEnum(returnTypeString);
        var responseContent = new _ResponseContentHelper(contentForClient, returnType);

        // HTTP Headers
        response.StatusCode = statusCode;
        response.ContentType = Framework.ReturnTypeEnumToMime(responseContent.returnTypeEnum);
        if (disableCache)
        {
            Framework.DisableCache(response);
        }
        response.Headers.Append("X-Tx-AppId", Framework.applicationId);
        if (RenderHttpHeaderAppVersion && !string.IsNullOrEmpty(Framework.applicationVersion)) response.Headers.Append("X-Tx-AppVersion", Framework.applicationVersion);

        if (returnType == ReturnTypeEnum.Html)
        {
            try
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                response.Headers.Append("X-Frame-Options", $"ALLOW-FROM {reqVars.domainName}");

                // response.Headers.Add("X-Frame-Options", $"ALLOW-FROM {reqVars.fullDomainName}");
                // response.Headers.Add("X-Frame-Options", $"ALLOW-FROM {reqVars.protocol}://{reqVars.domainName}");
                // response.Headers.Add("X-Frame-Options", $"SAMEORIGIN");
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "Could not add X-Frame-Options HTTP header");
            }
        }

        // Serve the content as a file download
        if (attachmentMode)
        {
            var headerFileName = attachmentFileName ?? $"{Framework.CreateTimeBasedString("second")}.{Framework.ReturnTypeEnumToString(returnType)}";

            // Response...
            //System.Net.Mime.ContentDisposition cd = new System.Net.Mime.ContentDisposition
            //{
            //    FileName = headerFileName,
            //    Inline = false  // false = prompt the user for downloading;  true = browser to try to show the file inline
            //};
            response.Headers.Append("Content-Disposition", $"attachment; filename={headerFileName}");
            response.Headers.Append("X-Content-Type-Options", "nosniff");

            //
            // return File(System.IO.File.ReadAllBytes(file), "application/pdf");
            //
        }

        // Convert the object that was passed to a string
        string responseContentForClient = responseContent.content;

        // Add content-length HTTP response header for JSON responses
        if (disableChunkedTransferEncoding)
        {
            response.Headers.Append("Content-Length", Encoding.UTF8.GetBytes(responseContentForClient ?? "").Length.ToString());
        }


        //Console.WriteLine("----------------------------");
        //Console.WriteLine($"contentForClient.GetType(): {contentForClient.GetType()}");
        //Console.WriteLine($"response.ContentType: {response.ContentType}");
        //Console.WriteLine("----------------------------");

        // Write to client
        await response.WriteAsync(responseContentForClient);
    }

    /// <summary>
    /// Helper function that converts a ReturnTypeEnum object that can be null to a string that we can process further
    /// </summary>
    /// <returns>The type enum to string.</returns>
    /// <param name="returnType">Return type.</param>
    private static string _ReturnTypeEnumToString(Framework.ReturnTypeEnum? returnType = null)
    {
        return ((returnType == null) ? "html" : Framework.ReturnTypeEnumToString((Framework.ReturnTypeEnum)returnType));
    }

    /// <summary>
    /// Writes a 200 OK response to the web client
    /// </summary>
    /// <param name="response"></param>
    /// <param name="contentForClient"></param>
    /// <param name="returnType"></param>
    /// <param name="disableCache"></param>
    /// <param name="disableChunkedTransferEncoding">Forces Content-Length HTTP response header instead of the default Transfer-Encoding: chunked set by Kestrel</param>
    /// <returns></returns>
    public async static Task OK(this HttpResponse response, dynamic contentForClient, Framework.ReturnTypeEnum? returnType = null, bool disableCache = false, bool disableChunkedTransferEncoding = false)
    {
        await _ClientResponse(response, StatusCodes.Status200OK, contentForClient, _ReturnTypeEnumToString(returnType), disableCache, false, null, disableChunkedTransferEncoding);
    }

    /// <summary>
    /// Returns the content as a download for the user
    /// </summary>
    /// <returns>The attachment.</returns>
    /// <param name="response">Response.</param>
    /// <param name="contentForClient">Content for client.</param>
    /// <param name="returnType">Return type.</param>
    /// <param name="disableCache">If set to <c>true</c> disable cache.</param>
    public async static Task Attachment(this HttpResponse response, dynamic contentForClient, string fileName, Framework.ReturnTypeEnum? returnType = null, bool disableCache = false)
    {
        await _ClientResponse(response, StatusCodes.Status200OK, contentForClient, _ReturnTypeEnumToString(returnType), disableCache, true, fileName);
    }

    /// <summary>
    /// Writes a 404 Not Found response to the web client
    /// </summary>
    /// <returns>The found.</returns>
    /// <param name="response">Response.</param>
    /// <param name="contentForClient">Content for client.</param>
    /// <param name="returnType">Return type.</param>
    /// <param name="disableCache">If set to <c>true</c> disable cache.</param>
    public async static Task NotFound(this HttpResponse response, dynamic contentForClient, Framework.ReturnTypeEnum? returnType = null, bool disableCache = true)
    {
        await _ClientResponse(response, StatusCodes.Status404NotFound, contentForClient, _ReturnTypeEnumToString(returnType), disableCache);
    }

    /// <summary>
    /// Writes a 204 No Content HTTP response to the web client
    /// </summary>
    /// <returns>The content.</returns>
    /// <param name="response">Response.</param>
    /// <param name="contentForClient">Content for client.</param>
    /// <param name="returnType">Return type.</param>
    /// <param name="disableCache">If set to <c>true</c> disable cache.</param>
    public async static Task NoContent(this HttpResponse response, dynamic contentForClient, Framework.ReturnTypeEnum? returnType = null, bool disableCache = true)
    {
        await _ClientResponse(response, StatusCodes.Status204NoContent, contentForClient, _ReturnTypeEnumToString(returnType), disableCache);
    }

    /// <summary>
    /// Writes a 400 Bad Request HTTP response to the web client (for example when input validation fails)
    /// </summary>
    /// <returns>The request.</returns>
    /// <param name="response">Response.</param>
    /// <param name="contentForClient">Content for client.</param>
    /// <param name="returnType">Return type.</param>
    /// <param name="disableCache">If set to <c>true</c> disable cache.</param>
    public async static Task BadRequest(this HttpResponse response, dynamic contentForClient, Framework.ReturnTypeEnum? returnType = null, bool disableCache = true)
    {
        await _ClientResponse(response, StatusCodes.Status400BadRequest, contentForClient, _ReturnTypeEnumToString(returnType), disableCache);
    }

    /// <summary>
    /// Writes a 500 Internal Server error response to the web client
    /// </summary>
    /// <returns>The error.</returns>
    /// <param name="response">Response.</param>
    /// <param name="contentForClient">Content for client.</param>
    /// <param name="returnType">Return type.</param>
    /// <param name="disableCache">If set to <c>true</c> disable cache.</param>
    public async static Task Error(this HttpResponse response, dynamic contentForClient, Framework.ReturnTypeEnum? returnType = null, bool disableCache = true)
    {
        await _ClientResponse(response, StatusCodes.Status500InternalServerError, contentForClient, _ReturnTypeEnumToString(returnType), disableCache);
    }

    /// <summary>
    /// Writes a response to the web client with a custom HTTP response code and subcode
    /// </summary>
    /// <returns>The custom.</returns>
    /// <param name="response">Response.</param>
    /// <param name="statusCode">Status code.</param>
    /// <param name="statusSubCode">Status sub code.</param>
    /// <param name="contentForClient">Content for client.</param>
    /// <param name="returnType">Return type.</param>
    /// <param name="disableCache">If set to <c>true</c> disable cache.</param>
    public async static Task Custom(this HttpResponse response, int statusCode, int statusSubCode, dynamic contentForClient, Framework.ReturnTypeEnum? returnType = null, bool disableCache = true)
    {
        // TODO: find a solution to render HTTP response subcodes
        await _ClientResponse(response, statusCode, contentForClient, _ReturnTypeEnumToString(returnType), disableCache);
    }

    /// <summary>
    /// Retrieves a value posted from a PUT, POST or querystring and if it does not exist returns null
    /// </summary>
    /// <returns>The posted value.</returns>
    /// <param name="request">Request.</param>
    /// <param name="key">Key.</param>
    public static string? RetrievePostedValue(this HttpRequest request, string key)
    {
        return request.RetrievePostedValue(key, (applicationId == "taxxoreditor") ? Framework.RegexEnum.Default.Value : Framework.RegexEnum.None.Value, false, Framework.ReturnTypeEnum.None, null);
    }

    /// <summary>
    /// Retrieves a value posted from a PUT, POST or querystring and if it does not exist return the default value
    /// </summary>
    /// <returns>The posted value.</returns>
    /// <param name="request">Request.</param>
    /// <param name="key">String parameter.</param>
    /// <param name="defaultValue">Default value.</param>
    public static string? RetrievePostedValue(this HttpRequest request, string key, string? defaultValue)
    {
        return request.RetrievePostedValue(key, (applicationId == "taxxoreditor") ? Framework.RegexEnum.Default.Value : Framework.RegexEnum.None.Value, false, Framework.ReturnTypeEnum.None, defaultValue);
    }

    /// <summary>
    /// Retrieves a value posted from a PUT, POST or querystring and optionally generate an error when the value is not supplied
    /// </summary>
    /// <returns>The posted value.</returns>
    /// <param name="request">Request.</param>
    /// <param name="key">String parameter.</param>
    /// <param name="generateErrorOnEmpty">If set to <c>true</c> generate error on empty.</param>
    /// <param name="returnType">Return type.</param>
    public static string? RetrievePostedValue(this HttpRequest request, string key, bool generateErrorOnEmpty, Framework.ReturnTypeEnum returnType = Framework.ReturnTypeEnum.None)
    {
        return request.RetrievePostedValue(key, (applicationId == "taxxoreditor") ? Framework.RegexEnum.Default.Value : Framework.RegexEnum.None.Value, generateErrorOnEmpty, returnType, null);
    }

    /// <summary>
    /// Retrieves a value posted from a PUT, POST or querystring and if it does not exist return the default value
    /// </summary>
    /// <returns>The posted value.</returns>
    /// <param name="request">Request.</param>
    /// <param name="key">String parameter.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="regexEnum">Regex enum.</param>
    /// <param name="returnType">Return type.</param>
    public static string? RetrievePostedValue(this HttpRequest request, string? key, string defaultValue, Framework.RegexEnum regexEnum, Framework.ReturnTypeEnum returnType = Framework.ReturnTypeEnum.None)
    {
        // Retrieve the regular expression as a string
        var regExp = (regexEnum == null) ? ((applicationId == "taxxoreditor") ? Framework.RegexEnum.Default.Value : Framework.RegexEnum.None.Value) : regexEnum.Value;
        return request.RetrievePostedValue(key, regExp, false, returnType, defaultValue);
    }

    /// <summary>
    /// Retrieves a value posted from a PUT, POST or querystring using a fixed set of predefined regular expressions for input validation
    /// </summary>
    /// <returns>The posted value.</returns>
    /// <param name="request">Request.</param>
    /// <param name="key">String parameter.</param>
    /// <param name="regexEnum">Regex enum.</param>
    /// <param name="generateErrorOnEmpty">If set to <c>true</c> generate error on empty.</param>
    /// <param name="returnType">Return type.</param>
    /// <param name="defaultValue">Default value (cannot appear in combination with generateErrorOnEmpty = true).</param>
    public static string? RetrievePostedValue(this HttpRequest request, string key, Framework.RegexEnum regexEnum, bool generateErrorOnEmpty = false, Framework.ReturnTypeEnum returnType = Framework.ReturnTypeEnum.None, string? defaultValue = null)
    {
        // Retrieve the regular expression as a string
        var regExp = (regexEnum == null) ? ((applicationId == "taxxoreditor") ? Framework.RegexEnum.Default.Value : Framework.RegexEnum.None.Value) : regexEnum.Value;
        return request.RetrievePostedValue(key, regExp, generateErrorOnEmpty, returnType, defaultValue);
    }

    /// <summary>
    /// Retrieves a value posted from a PUT, POST or querystring
    /// </summary>
    /// <returns>The posted value.</returns>
    /// <param name="request">Request.</param>
    /// <param name="key">String parameter.</param>
    /// <param name="validationPattern">Validation pattern.</param>
    /// <param name="generateErrorOnEmpty">If set to <c>true</c> generate error on empty.</param>
    /// <param name="returnType">Return type.</param>
    /// <param name="defaultValue">Default value (cannot appear in combination with generateErrorOnEmpty = true).</param>
    public static string? RetrievePostedValue(this HttpRequest request, string? key, string validationPattern, bool generateErrorOnEmpty, Framework.ReturnTypeEnum returnType = Framework.ReturnTypeEnum.None, string? defaultValue = null)
    {
        var context = System.Web.Context.Current;
        var reqVars = Framework.RetrieveRequestVariables(context);

        string? postedValue = defaultValue;

        // Initially attempt to retrieve the posted value from the querystring as it is possible to do a POST request with querystring variables included
        if (request.Query.Count > 0)
        {
            if (request.Query[key].Count > 0)
            {
                postedValue = (defaultValue != null && request.Query[key].ToString() == "") ? defaultValue : request.Query[key].ToString();
                if (request.Query[key].Count > 1)
                {
                    Framework.appLogger.LogWarning($"The querystring values were not unique, merged them to '{postedValue}'");
                }
            }
        }
        else
        {
            try
            {
                if (request.HasFormContentType)
                {
                    if ((request.Method == "POST" || request.Method == "PUT" || request.Method == "DELETE") && request.Form[key].Count > 0)
                    {
                        postedValue = (defaultValue != null && request.Form[key].ToString() == "") ? defaultValue : request.Form[key].ToString();
                        if (request.Form[key].Count > 1)
                        {
                            Framework.appLogger.LogWarning($"The posted values were not unique, merged them to '{postedValue}'");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (reqVars.isDebugMode)
                {

                    appLogger.LogWarning(ex, $"There was an error reading the posted data. url: {reqVars.thisUrlPath}, form-key: {key}, error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }
        }

        // Validate against expression
        if (!string.IsNullOrEmpty(postedValue))
        {
            if (validationPattern != ".*")
            {
                if (!Framework.RegExpTest(validationPattern, postedValue, true))
                {
                    // If we have explicitly provided a returntype enum, then we need to respect that and override the negotiated one stored in the request variables object
                    if (returnType != Framework.ReturnTypeEnum.None) reqVars.returnType = returnType;

                    Framework.HandleError(reqVars, $"No valid data supplied in {key} field", $"parameter: '{key}' with value '{HttpUtility.UrlEncode(postedValue)}' did not pass validation pattern '{validationPattern}', stack-trace: {Framework.GetStackTrace()}", 400);
                }
            }
        }
        else
        {
            // Looks like there was no data supplied for this form element...
            if (generateErrorOnEmpty)
            {
                // If we have explicitly provided a returntype enum, then we need to respect that and override the negotiated one stored in the request variables object
                if (returnType != Framework.ReturnTypeEnum.None) reqVars.returnType = returnType;

                Framework.HandleError(reqVars, $"Not enough valid data supplied", $"parameter: '{key}' was empty or not supplied, stack-trace: {Framework.GetStackTrace()}", 400);
            }
        }

        return postedValue;
    }

}

/// <summary>
/// Http extensions for working with JSON data
/// </summary>
public static class HttpExtensions
{
    private static readonly JsonSerializer Serializer = new JsonSerializer();

    public static void WriteJson<T>(this HttpResponse response, T obj)
    {
        response.ContentType = MediaTypeNames.Application.Json;
        using (var writer = new HttpResponseStreamWriter(response.Body, Encoding.UTF8))
        {
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                jsonWriter.CloseOutput = false;
                jsonWriter.AutoCompleteOnClose = false;

                Serializer.Serialize(jsonWriter, obj);
                jsonWriter.Close();
            }
            writer.Close();
        }
    }

    public static T? ReadFromJson<T>(this HttpContext httpContext)
    {
        using (var streamReader = new StreamReader(httpContext.Request.Body))
        {
            using (var jsonTextReader = new JsonTextReader(streamReader))
            {
                T? obj = Serializer.Deserialize<T>(jsonTextReader);

                List<ValidationResult> results = new List<ValidationResult>();
                if (obj != null && Validator.TryValidateObject(obj, new ValidationContext(obj), results))
                {
                    return obj;
                }

                httpContext.Response.StatusCode = 400;
                httpContext.Response.WriteJson(results);

                return default(T);
            }
        }
    }
}