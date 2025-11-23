using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using static Framework;
using static Taxxor.Project.ProjectLogic;
using static Taxxor.Project.TaxxorExtensionMethods;

namespace Taxxor
{

    /// <summary>
    /// Utility methods for interacting with the other Taxxor webservices
    /// Partial class so that it can be "extended" on a project basis
    /// </summary>
    public partial class ConnectedServices
    {

        /// <summary>
        /// Generates the standard debug string from the data that we will post to the remote service
        /// </summary>
        /// <returns>The standard debug string.</returns>
        /// <param name="dataToPost">Data to post.</param>
        private static string _generateStandardDebugString(Dictionary<string, string> dataToPost)
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in dataToPost)
            {
                var currentValue = (pair.Value != null && pair.Value.Length > 60) ? TruncateString(pair.Value, 60) : pair.Value ?? "null";
                builder.Append(pair.Key).Append(": ").Append(currentValue).Append(',');
            }
            string debugContent = builder.ToString();
            return debugContent.TrimEnd(',');
        }

        /// <summary>
        /// Generates the standard debug string from the data that we will post to the remote service
        /// </summary>
        /// <param name="objectIn"></param>
        /// <returns></returns>
        private static string _generateStandardDebugString(object objectIn)
        {
            return GenerateDebugObjectString(objectIn, ReturnTypeEnum.Txt);
        }

        /// <summary>
        /// Attempts to extract basic information from a standard API XML response for debugging purposes
        /// </summary>
        /// <param name="xmlServerResponse"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        private static string _ExtractXmlApiDebugInfo(XmlDocument xmlServerResponse, string? prefix = null)
        {
            string returnMessage = "";
            string? statusCode = xmlServerResponse.SelectSingleNode("//httpstatuscode")?.InnerText;
            string? responseMessage = xmlServerResponse.SelectSingleNode("//httpresponse")?.InnerText;
            if (statusCode != null) returnMessage = $"HTTP status code: {statusCode}";
            if (responseMessage != null)
            {
                if (returnMessage != "") returnMessage += ", ";
                returnMessage += $"HTTP response: {responseMessage}";
            }



            if ((statusCode != null || responseMessage != null) && prefix != null)
            {
                return $"{prefix.TrimEnd()} {returnMessage}";
            }

            return returnMessage;
        }

        /// <summary>
        /// Utility class to assist with the interaction between the Taxxor Project Data Store and the other Taxxor components
        /// </summary>
        public static partial class DocumentStoreService
        {
            /// <summary>
            /// Interfaces with the Taxxor Document Store for user data stored in the temporary repository
            /// </summary>
            public static class UserTempData
            {

                /// <summary>
                /// Loads user data from the temporary repository
                /// </summary>
                /// <returns>The data that the webservive returned or an XML error document</returns>
                /// <param name="key">Relative path in the repository where we want to load the file contents from</param>
                /// <param name="debug">If set to <c>true</c> debug.</param>
                /// <typeparam name="T">The 1st type parameter.</typeparam>
                public static async Task<T> Load<T>(string key, bool debug = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // Retrieve the type of data that we need to return
                    var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                    // Construct and execute the request to the Taxxor Document Store
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "key", key }
                    };

                    XmlDocument responseXmlEnvelope = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorusertempdata", dataToPost, debug);

                    // Error handling
                    if (XmlContainsError(responseXmlEnvelope)) HandleError(responseXmlEnvelope);

                    // Received dapa processing
                    var fileContent = responseXmlEnvelope.SelectSingleNode("/result/message").InnerText;
                    var fileType = GetFileType(responseXmlEnvelope.SelectSingleNode("/result/debuginfo").InnerText);
                    if (fileType == "text")
                    {
                        // Now we need to HTML decode the content
                        fileContent = HttpUtility.HtmlDecode(responseXmlEnvelope.SelectSingleNode("/result/message").InnerText);
                    }
                    else if (returnType == "xml")
                    {
                        // Return the complete envelope "as-is" that we have received from the Taxxor Document Store
                        return (T)Convert.ChangeType(responseXmlEnvelope, typeof(T));
                    }

                    // Return the content of the file that we have requested
                    switch (returnType)
                    {
                        case "xml":
                            // Return the XmlDocument that we have received from the remote service
                            XmlDocument? xmlDocument = new XmlDocument();
                            try
                            {
                                xmlDocument.LoadXml(fileContent);
                            }
                            catch (Exception ex)
                            {
                                xmlDocument = GenerateErrorXml("Could not parse recieved content into an XML document", $"recieved content: {TruncateString(fileContent, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                            }

                            return (T)Convert.ChangeType(xmlDocument, typeof(T));

                        default:
                            // Return the string (useful for base 64 encoded blobs)
                            return (T)Convert.ChangeType(fileContent, typeof(T));
                    }
                }

                /// <summary>
                /// Delete user data from the temporary repository
                /// </summary>
                /// <returns>The delete.</returns>
                /// <param name="key">Key.</param>
                /// <param name="debug">If set to <c>true</c> debug.</param>
                /// <typeparam name="T">The 1st type parameter.</typeparam>
                public static async Task<T> Delete<T>(string key, bool debug = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // Retrieve the type of data that we need to return
                    var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                    // Construct and execute the request to the Taxxor Document Store
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "key", key }
                    };

                    XmlDocument responseXmlEnvelope = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Delete, "taxxoreditorusertempdata", dataToPost, debug);

                    // Handle error
                    if (XmlContainsError(responseXmlEnvelope))
                    {
                        if (returnType == "boolean")
                        {
                            appLogger.LogError(responseXmlEnvelope.SelectSingleNode("/error/message").InnerText);
                            appLogger.LogError(responseXmlEnvelope.SelectSingleNode("/error/debuginfo").InnerText);
                            var nodeHttpResponse = responseXmlEnvelope.SelectSingleNode("/error/httpresponse");
                            if (nodeHttpResponse != null)
                            {
                                appLogger.LogError($"Httpresponse: {nodeHttpResponse.InnerText}");
                            }

                            return (T)Convert.ChangeType(false, typeof(T));
                        }
                        else
                        {
                            HandleError(responseXmlEnvelope);
                        }
                    }

                    // Return the result
                    switch (returnType)
                    {
                        case "xml":
                            return (T)Convert.ChangeType(GenerateSuccessXml("Successfully deleted file", responseXmlEnvelope.SelectSingleNode("/result/debuginfo").InnerText), typeof(T));

                        case "boolean":
                            return (T)Convert.ChangeType(true, typeof(T));

                        default:
                            // Return the string
                            return (T)Convert.ChangeType("Successfully deleted user temporary file", typeof(T));
                    }

                }

                /// <summary>
                /// Saves user data from in the temporary repository
                /// </summary>
                /// <returns>The save.</returns>
                /// <param name="key">Relative path in the repository where we want to store the data in</param>
                /// <param name="value">The file contents (for binary files use Base64 encoded blob)</param>
                /// <param name="debug">If set to <c>true</c> debug.</param>
                /// <typeparam name="T">The 1st type parameter.</typeparam>
                public static async Task<T> Save<T>(string key, string value, bool debug = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                    var dataToPost = new Dictionary<string, string>
                    {
                        { "key", key },
                        { "value", value }
                    };

                    XmlDocument responseXmlEnvelope = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "taxxoreditorusertempdata", dataToPost, debug);

                    // Handle error
                    if (XmlContainsError(responseXmlEnvelope))
                    {
                        if (returnType == "boolean")
                        {
                            appLogger.LogError(responseXmlEnvelope.SelectSingleNode("/error/message").InnerText);
                            appLogger.LogError(responseXmlEnvelope.SelectSingleNode("/error/debuginfo").InnerText);
                            var nodeHttpResponse = responseXmlEnvelope.SelectSingleNode("/error/httpresponse");
                            if (nodeHttpResponse != null)
                            {
                                appLogger.LogError($"Httpresponse: {nodeHttpResponse.InnerText}");
                            }

                            return (T)Convert.ChangeType(false, typeof(T));
                        }
                        else
                        {
                            HandleError(responseXmlEnvelope);
                        }
                    }

                    // Return the result
                    switch (returnType)
                    {
                        case "xml":
                            return (T)Convert.ChangeType(GenerateSuccessXml("Successfully stored file", responseXmlEnvelope.SelectSingleNode("/result/debuginfo").InnerText), typeof(T));

                        case "boolean":
                            return (T)Convert.ChangeType(true, typeof(T));

                        default:
                            // Return the string
                            return (T)Convert.ChangeType("Successfully stored user temporary file", typeof(T));
                    }

                }

                /// <summary>
                /// Streams the output of the call to the temporary user data 
                /// </summary>
                /// <returns>The stream.</returns>
                /// <param name="key">Key.</param>
                /// <param name="forceDownload">If set to <c>true</c> force download.</param>
                /// <param name="displayFileName">Display file name.</param>
                /// <param name="debug">If set to <c>true</c> debug.</param>
                public static async Task Stream(string key, bool forceDownload = false, string displayFileName = "", bool debug = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // Construct and execute the request to the Taxxor Document Store
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "key", key }
                    };

                    XmlDocument responseXmlEnvelope = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorusertempdata", dataToPost, debug);

                    // Error handling
                    if (XmlContainsError(responseXmlEnvelope)) HandleError(responseXmlEnvelope);

                    // Retrieve information from the XML envelope
                    var filePathOs = responseXmlEnvelope.SelectSingleNode("/result/debuginfo").InnerText;
                    var encodedContent = responseXmlEnvelope.SelectSingleNode("/result/message").InnerText;

                    // Retrieve information about the file that we are processing
                    var fileType = GetFileType(filePathOs);
                    var contentType = GetContentType(Path.GetExtension(filePathOs).Replace(".", ""));

                    // Stream the file to the client
                    if (fileType == "text")
                    {
                        // Decode the file content
                        var decodedFileContent = HttpUtility.HtmlDecode(encodedContent);

                        // Write the response
                        SetHeaders(context.Response, displayFileName, contentType, decodedFileContent.Length, forceDownload);
                        await context.Response.WriteAsync(decodedFileContent);
                    }
                    else
                    {
                        // Decode the file content
                        byte[] bytes = Base64DecodeToBytes(encodedContent);

                        // Write the response
                        SetHeaders(context.Response, displayFileName, contentType, bytes.Length, forceDownload);
                        await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                    }

                }

            }

            /// <summary>
            /// Interfaces with the Taxxor Document Store for filing data
            /// </summary>
            public static partial class FilingData
            {

                /// <summary>
                /// Loads the filing data file from taxxor data service.
                /// </summary>
                /// <returns>The filing data file from taxxor data service.</returns>
                /// <param name="locationId">Location identifier.</param>
                /// <param name="debug">If set to <c>true</c> debug.</param>
                /// <typeparam name="T">The 1st type parameter.</typeparam>
                public async static Task<T> Load<T>(string locationId, bool debug = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // TODO: Add support for binary data
                    var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                    // Use gRPC FileManagementService instead of REST endpoint
                    // locationId is treated as the "path" parameter for the gRPC call
                    XmlDocument responseXml = await FilingDataWrapper.GetFileContentsAsync(projectVars.projectId, locationId, "", debug);

                    if (XmlContainsError(responseXml)) HandleError(responseXml);

                    var fileType = GetFileType(responseXml.SelectSingleNode("/result/debuginfo").InnerText);
                    if (fileType != "text") HandleError("File type not supported", $"file path on remote server: {responseXml.SelectSingleNode("/result/debuginfo").InnerText}, stack-trace: {GetStackTrace()}");

                    var decodedFileContent = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/message").InnerText);

                    switch (returnType)
                    {
                        case "xml":
                            // Return the XmlDocument that we have received from the remote service
                            XmlDocument? xmlDocument = new XmlDocument();
                            try
                            {
                                xmlDocument.LoadXml(decodedFileContent);
                            }
                            catch (Exception ex)
                            {
                                xmlDocument = GenerateErrorXml("Could not parse recieved content into an XML document", $"recieved content: {TruncateString(decodedFileContent, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                            }

                            return (T)Convert.ChangeType(xmlDocument, typeof(T));

                        default:
                            // Return the string
                            return (T)Convert.ChangeType(decodedFileContent, typeof(T));
                    }
                }

                /// <summary>
                /// Loads the filing data file from taxxor data service.
                /// </summary>
                /// <returns>The filing data file from taxxor data service.</returns>
                /// <param name="nodeLocation">Node location.</param>
                /// <param name="debug">If set to <c>true</c> debug.</param>
                /// <typeparam name="T">The 1st type parameter.</typeparam>
                public async static Task<T> Load<T>(XmlNode nodeLocation, bool debug = false)
                {
                    string? path = null;
                    string? relativeTo = null;

                    // Test if this definition contains site type specific locations
                    XmlNodeList? xmlNodeList = nodeLocation.SelectNodes("domain[@type='" + siteType + "']");
                    if (xmlNodeList.Count > 0)
                    {
                        var xmlNodeNested = xmlNodeList.Item(0);
                        path = RetrieveNodeValueIfExists(".", xmlNodeNested);
                        relativeTo = RetrieveNodeValueIfExists("@path-type", xmlNodeNested);
                    }
                    else
                    {
                        path = RetrieveNodeValueIfExists(".", nodeLocation);
                        relativeTo = RetrieveNodeValueIfExists("@path-type", nodeLocation);
                    }

                    // Handle case not found
                    if (path == null || relativeTo == null) HandleError("Could not parse location node", $"nodeLocation: {nodeLocation.OuterXml}, stack-trace: {GetStackTrace()}");

                    return await Load<T>(path, relativeTo, debug);
                }

                /// <summary>
                /// Loads the filing data file from taxxor data service.
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="projectId"></param>
                /// <param name="nodeLocation"></param>
                /// <param name="debug"></param>
                /// <returns></returns>
                public async static Task<T> Load<T>(string projectId, XmlNode nodeLocation, bool debug = false)
                {
                    string? path = null;
                    string? relativeTo = null;

                    // Test if this definition contains site type specific locations
                    XmlNodeList? xmlNodeList = nodeLocation.SelectNodes("domain[@type='" + siteType + "']");
                    if (xmlNodeList.Count > 0)
                    {
                        var xmlNodeNested = xmlNodeList.Item(0);
                        path = RetrieveNodeValueIfExists(".", xmlNodeNested);
                        relativeTo = RetrieveNodeValueIfExists("@path-type", xmlNodeNested);
                    }
                    else
                    {
                        path = RetrieveNodeValueIfExists(".", nodeLocation);
                        relativeTo = RetrieveNodeValueIfExists("@path-type", nodeLocation);
                    }

                    // Handle case not found
                    if (path == null || relativeTo == null) HandleError("Could not parse location node", $"nodeLocation: {nodeLocation.OuterXml}, stack-trace: {GetStackTrace()}");

                    return await Load<T>(projectId, path, relativeTo, debug);
                }

                /// <summary>
                /// Loads the filing data file from taxxor data service.
                /// </summary>
                /// <returns>The filing data file from taxxor data service.</returns>
                /// <param name="path">Path.</param>
                /// <param name="relativeTo">Relative to.</param>
                /// <param name="debug">If set to <c>true</c> debug.</param>
                /// <param name="stopOnError">Optionally return an error instead of rendering an error when things go wrong</param>
                /// <typeparam name="T">The 1st type parameter.</typeparam>
                public async static Task<T> Load<T>(string path, string relativeTo, bool debug = false, bool stopOnError = true)
                {
                    var context = System.Web.Context.Current;
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    return await Load<T>(projectVars.projectId, path, relativeTo, debug, stopOnError);
                }

                /// <summary>
                /// Loads the filing data file from taxxor data service.
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="path"></param>
                /// <param name="relativeTo"></param>
                /// <param name="debug"></param>
                /// <param name="stopOnError"></param>
                /// <typeparam name="T"></typeparam>
                /// <returns></returns>
                public async static Task<T> Load<T>(string projectId, string path, string relativeTo, bool debug = false, bool stopOnError = true)
                {

                    var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                    // Use gRPC FileManagementService instead of REST endpoint
                    XmlDocument responseXml = await FilingDataWrapper.GetFileContentsAsync(projectId, path, relativeTo, debug);

                    if (XmlContainsError(responseXml))
                    {
                        if (stopOnError)
                        {
                            HandleError(responseXml);
                        }
                        else
                        {
                            switch (returnType)
                            {
                                case "xml":
                                    return (T)Convert.ChangeType(responseXml, typeof(T));

                                default:
                                    var baseErrorMessage = "ERROR: There was an error retrieving the file from the data store";
                                    appLogger.LogError($"{baseErrorMessage}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");
                                    return (T)Convert.ChangeType(baseErrorMessage, typeof(T));
                            }

                        }
                    }


                    // TODO: Add support for binary data
                    // Misusing the debuginfo field to retrieve the filetype that we have requested
                    var nodeDebugInfo = responseXml.SelectSingleNode("/result/debuginfo");
                    if (nodeDebugInfo != null)
                    {
                        var fileType = GetFileType(nodeDebugInfo.InnerText);
                        if (fileType != "text") HandleError("File type not supported", $"file path on remote server: {(responseXml.SelectSingleNode("/result/debuginfo")?.InnerText ?? "")}, stack-trace: {GetStackTrace()}");
                    }


                    // TODO: Use the payload node to transport file data
                    var decodedFileContent = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/message").InnerText);

                    switch (returnType)
                    {
                        case "xml":
                            // Return the XmlDocument that we have received from the remote service
                            XmlDocument? xmlDocument = new XmlDocument();
                            try
                            {
                                xmlDocument.LoadXml(decodedFileContent);
                            }
                            catch (Exception ex)
                            {
                                xmlDocument = GenerateErrorXml("Could not parse recieved content into an XML document", $"recieved content: {TruncateString(decodedFileContent, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                            }

                            return (T)Convert.ChangeType(xmlDocument, typeof(T));

                        default:
                            // Return the string
                            return (T)Convert.ChangeType(decodedFileContent, typeof(T));
                    }
                }

                /// <summary>
                /// Retrieves all the data of a project in one request
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="path"></param>
                /// <param name="relativeTo"></param>
                /// <param name="debug"></param>
                /// <param name="stopOnError"></param>
                /// <typeparam name="T"></typeparam>
                /// <returns></returns>
                public async static Task<T> LoadAll<T>(string projectId, string lang = "all", bool onlyInUse = true, bool debug = false, bool stopOnError = true)
                {

                    var dataToPost = new Dictionary<string, string>
                    {
                        { "pid", projectId },
                        { "lang", lang },
                        { "inuse", onlyInUse.ToString().ToLower() }
                    };

                    var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorallcontent", dataToPost, debug);

                    if (XmlContainsError(responseXml))
                    {
                        if (stopOnError)
                        {
                            HandleError(responseXml);
                        }
                        else
                        {
                            switch (returnType)
                            {
                                case "xml":
                                    return (T)Convert.ChangeType(responseXml, typeof(T));

                                default:
                                    var baseErrorMessage = "ERROR: There was an error retrieving the complete filing data from the data store";
                                    appLogger.LogError($"{baseErrorMessage}, projectId: {projectId}, lang: {lang}, onlyInUse: {onlyInUse.ToString()}, stack-trace: {GetStackTrace()}");
                                    return (T)Convert.ChangeType(baseErrorMessage, typeof(T));
                            }

                        }
                    }


                    // Retrieve the content of the file and decode it
                    var decodedFileContent = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/payload").InnerText);

                    switch (returnType)
                    {
                        case "xml":
                            // Return the XmlDocument that we have received from the remote service
                            XmlDocument? xmlDocument = new XmlDocument();
                            try
                            {
                                xmlDocument.LoadXml(decodedFileContent);
                            }
                            catch (Exception ex)
                            {
                                xmlDocument = GenerateErrorXml("Could not parse recieved content into an XML document", $"recieved content: {TruncateString(decodedFileContent, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                            }

                            return (T)Convert.ChangeType(xmlDocument, typeof(T));

                        default:
                            // Return the string
                            return (T)Convert.ChangeType(decodedFileContent, typeof(T));
                    }
                }


                /// <summary>
                /// tores an XmlDocument in a specific location in the project directory on the Taxxor Data Store
                /// </summary>
                /// <param name="xmlDocument"></param>
                /// <param name="nodeLocation"></param>
                /// <param name="debug"></param>
                /// <returns></returns>
                public async static Task<T> Save<T>(XmlDocument xmlDocument, XmlNode nodeLocation, bool debug = false)
                {
                    string? path = null;
                    string? relativeTo = null;

                    // Test if this definition contains site type specific locations
                    XmlNodeList? xmlNodeList = nodeLocation.SelectNodes("domain[@type='" + siteType + "']");
                    if (xmlNodeList.Count > 0)
                    {
                        var xmlNodeNested = xmlNodeList.Item(0);
                        path = RetrieveNodeValueIfExists(".", xmlNodeNested);
                        relativeTo = RetrieveNodeValueIfExists("@path-type", xmlNodeNested);
                    }
                    else
                    {
                        path = RetrieveNodeValueIfExists(".", nodeLocation);
                        relativeTo = RetrieveNodeValueIfExists("@path-type", nodeLocation);
                    }

                    // Handle case not found
                    if (path == null || relativeTo == null) HandleError("Could not parse location node", $"nodeLocation: {nodeLocation.OuterXml}, stack-trace: {GetStackTrace()}");

                    return await Save<T>(xmlDocument, path, relativeTo, debug);
                }

                /// <summary>
                /// Stores an XmlDocument in a specific location in the project directory on the Taxxor Data Store
                /// </summary>
                /// <param name="xmlDocument"></param>
                /// <param name="path"></param>
                /// <param name="relativeTo"></param>
                /// <param name="debug"></param>
                /// <param name="stopOnError"></param>
                /// <typeparam name="T"></typeparam>
                /// <returns></returns>
                public async static Task<T> Save<T>(XmlDocument xmlDocument, string path, string relativeTo, bool debug = false, bool stopOnError = true)
                {
                    return await Save<T>(xmlDocument.OuterXml, path, relativeTo, debug, stopOnError);
                }

                /// <summary>
                /// Stores an XmlDocument in a specific location in the project directory on the Taxxor Data Store
                /// </summary>
                /// <param name="xmlDocument"></param>
                /// <param name="projectId"></param>
                /// <param name="path"></param>
                /// <param name="relativeTo"></param>
                /// <param name="debug"></param>
                /// <param name="stopOnError"></param>
                /// <typeparam name="T"></typeparam>
                /// <returns></returns>
                public async static Task<T> Save<T>(XmlDocument xmlDocument, string projectId, string path, string relativeTo, bool debug = false, bool stopOnError = true)
                {
                    return await Save<T>(xmlDocument.OuterXml, projectId, path, relativeTo, debug, stopOnError);
                }

                /// <summary>
                /// Stores a string in a file within the project directory on the Taxxor Data Store
                /// </summary>
                /// <param name="data"></param>
                /// <param name="path"></param>
                /// <param name="relativeTo"></param>
                /// <param name="debug"></param>
                /// <param name="stopOnError"></param>
                /// <typeparam name="T"></typeparam>
                /// <returns></returns>
                public async static Task<T> Save<T>(string data, string path, string relativeTo, bool debug = false, bool stopOnError = true)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    return await Save<T>(data, projectVars.projectId, path, relativeTo, debug, stopOnError);
                }

                /// <summary>
                /// Stores a string in a file within the project directory on the Taxxor Data Store
                /// </summary>
                /// <param name="data"></param>
                /// <param name="projectId"></param>
                /// <param name="path"></param>
                /// <param name="relativeTo"></param>
                /// <param name="debug"></param>
                /// <param name="stopOnError"></param>
                /// <param name="projectVars"></param> 
                /// <typeparam name="T"></typeparam>
                /// <returns></returns>
                public async static Task<T> Save<T>(string data, string projectId, string path, string relativeTo, bool debug = false, bool stopOnError = true, ProjectVariables? projectVars = null)
                {
                    var returnType = GenericTypeToNormalizedString(typeof(T).ToString());

                    // Call the gRPC FilingDataService via wrapper to store file contents
                    XmlDocument responseXml = await FilingDataWrapper.PutFileContentsAsync(projectId, path, relativeTo, data, debug);

                    if (XmlContainsError(responseXml))
                    {
                        if (stopOnError)
                        {
                            HandleError(responseXml);
                        }
                        else
                        {
                            var baseErrorMessage = "ERROR: There was an error storing the content the file on the Taxxor Document Store";
                            switch (returnType)
                            {
                                case "xml":
                                    return (T)Convert.ChangeType(responseXml, typeof(T));

                                case "boolean":
                                    appLogger.LogError($"{baseErrorMessage}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");
                                    appLogger.LogError(responseXml.SelectSingleNode("/error/message").InnerText);
                                    appLogger.LogError(responseXml.SelectSingleNode("/error/debuginfo").InnerText);
                                    var nodeHttpResponse = responseXml.SelectSingleNode("/error/httpresponse");
                                    if (nodeHttpResponse != null)
                                    {
                                        appLogger.LogError($"Httpresponse: {nodeHttpResponse.InnerText}");
                                    }

                                    return (T)Convert.ChangeType(false, typeof(T));

                                default:

                                    appLogger.LogError($"{baseErrorMessage}, path: {path}, relativeTo: {relativeTo}, stack-trace: {GetStackTrace()}");
                                    return (T)Convert.ChangeType(baseErrorMessage, typeof(T));
                            }

                        }
                    }

                    switch (returnType)
                    {
                        case "xml":
                            return (T)Convert.ChangeType(responseXml, typeof(T));

                        case "boolean":


                            return (T)Convert.ChangeType(true, typeof(T));

                        default:
                            return (T)Convert.ChangeType("Successfully stored file on the Taxxor Document Store", typeof(T));
                    }

                }

                /// <summary>
                /// Streams the output of the call to the filing data service to the client
                /// </summary>
                /// <returns>The stream.</returns>
                /// <param name="path">Path.</param>
                /// <param name="relativeTo">Relative to.</param>
                /// <param name="forceDownload">If set to <c>true</c> force download.</param>
                /// <param name="displayFileName">Display file name.</param>
                /// <param name="debug">If set to <c>true</c> debug.</param>
                public static async Task Stream(string path, string relativeTo, bool forceDownload = false, string displayFileName = "", int errorCode = 500, bool debug = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // Call the gRPC service via wrapper to retrieve file contents
                    XmlDocument responseXmlEnvelope = await FilingDataWrapper.GetFileContentsAsync(projectVars.projectId, path, relativeTo, debug);

                    // Error handling
                    if (XmlContainsError(responseXmlEnvelope))
                    {
                        if (errorCode == 500)
                        {
                            HandleError(responseXmlEnvelope);
                        }
                        else
                        {
                            HandleError(HttpStatusCodeToMessage(errorCode), (responseXmlEnvelope.SelectSingleNode("//debuginfo")?.InnerText ?? ""), errorCode);
                        }
                    }
                    else
                    {
                        // Retrieve information from the XML envelope
                        var filePathOs = responseXmlEnvelope.SelectSingleNode("/result/debuginfo").InnerText;
                        var encodedContent = responseXmlEnvelope.SelectSingleNode("/result/message").InnerText;

                        // Retrieve information about the file that we are processing
                        var fileType = GetFileType(filePathOs);
                        var contentType = GetContentType(Path.GetExtension(filePathOs).Replace(".", ""));

                        // Stream the file to the client
                        if (fileType == "text")
                        {
                            // Decode the file content
                            var decodedFileContent = HttpUtility.HtmlDecode(encodedContent);

                            // Write the response
                            SetHeaders(context.Response, displayFileName, contentType, decodedFileContent.Length, forceDownload);
                            await context.Response.WriteAsync(decodedFileContent);
                        }
                        else
                        {
                            // Decode the file content
                            byte[] bytes = Base64DecodeToBytes(encodedContent);

                            // Write the response
                            SetHeaders(context.Response, displayFileName, contentType, bytes.Length, forceDownload);
                            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                        }
                    }
                }

                /// <summary>
                /// Retrieves the metadata of all the data files which are contained the Taxxor Document Store for open projects
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> LoadContentMetadata(string projectId = "all", bool debugRoutine = false)
                {
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "pid", projectId },
                        { "projectid", projectId }
                    };

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorfilingmetadata", dataToPost, debugRoutine);

                    // Console.WriteLine("*************************************");
                    // Console.WriteLine(responseXml.OuterXml);
                    // Console.WriteLine("*************************************");

                    return new TaxxorReturnMessage(responseXml);
                }

                /// <summary>
                /// Compiles a new CMS Metadata XML object in the Project Data Store and returns the result
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> CompileCmsMetadata(string projectId = "all", bool debugRoutine = false)
                {
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "pid", projectId },
                        { "projectid", projectId }
                    };

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "taxxoreditorfilingmetadata", dataToPost, debugRoutine);

                    // Console.WriteLine("*************************************");
                    // Console.WriteLine(responseXml.OuterXml);
                    // Console.WriteLine("*************************************");

                    return new TaxxorReturnMessage(responseXml);
                }

            }

            /// <summary>
            /// Interacts with the Taxxor Document Store for maintenance of filing versions
            /// </summary>
            public static class FilingVersion
            {

                /// <summary>
                /// Loads information about the filing versions from the Taxxor Document Store
                /// </summary>
                /// <returns>The load.</returns>
                /// <param name="retrieveAllVersions">If set to <c>true</c> retrieve all versions.</param>
                /// <param name="debug">If set to <c>true</c> debug.</param>
                public async static Task<XmlDocument?> Load(bool retrieveAllVersions, bool debug = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var dataToPost = new Dictionary<string, string?>
                    {
                        { "pid", projectVars.projectId }
                    };

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorversionmanager", dataToPost, debug);

                    if (XmlContainsError(responseXml)) HandleError(responseXml);

                    var decodedFileContent = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/payload").InnerText);

                    XmlDocument? xmlDocument = new XmlDocument();
                    try
                    {
                        xmlDocument.LoadXml(decodedFileContent);
                    }
                    catch (Exception ex)
                    {
                        xmlDocument = GenerateErrorXml("Could not parse recieved content into an XML document", $"recieved content: {TruncateString(decodedFileContent, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }

                    return xmlDocument;
                }

                /// <summary>
                /// Creates a filing version
                /// </summary>
                /// <param name="versionName"></param>
                /// <param name="versionMessage"></param>
                /// <param name="files"></param>
                /// <param name="debug"></param>
                /// <returns></returns>
                public async static Task<XmlDocument> Create(string versionName, string versionMessage, Dictionary<string, string> files, bool debug = false, ProjectVariables projectVars = null)
                {
                    var context = System.Web.Context.Current;
                    if (projectVars == null) projectVars = RetrieveProjectVariables(context);

                    var dataToPost = new Dictionary<string, string?>
                    {
                        { "pid", projectVars.projectId },
                        { "name", versionName },
                        { "message", versionMessage }
                    };

                    // Add the (encoded) files to the data that we will be posting to the Taxxor Document Store
                    int counter = 1;
                    var postFiles = false;
                    foreach (KeyValuePair<string, string> pair in files)
                    {
                        // if (debug) appLogger.LogInformation($"- key: {pair.Key}");
                        dataToPost.Add($"file{counter}", pair.Key);
                        dataToPost.Add($"content{counter}", pair.Value);
                        if (!postFiles && pair.Value.IsBase64(false)) postFiles = true;
                        counter++;
                    }

                    // Add a variable that indicates if we are sending paths to files of if we are sending the (binary) content of the files themselves
                    var transportMethod = (postFiles) ? "content" : "path";
                    dataToPost.Add("transportmethod", transportMethod);

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "taxxoreditorversionmanager", dataToPost, debug, false, projectVars);

                    if (XmlContainsError(responseXml)) HandleError(responseXml);

                    return responseXml;
                }

            }

            /// <summary>
            /// Interfaces with the Taxxor Document Store for data stored in the project data cache
            /// </summary>
            public static class CacheData
            {
                /// <summary>
                /// Lists all the files in the cache directory of the project data folder
                /// </summary>
                /// <param name="debug"></param>
                /// <returns></returns>
                public async static Task<XmlDocument?> ListFiles(bool debug = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var dataToPost = new Dictionary<string, string?>
                    {
                        { "pid", projectVars.projectId }
                    };

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorcachefiles", dataToPost, debug);

                    if (XmlContainsError(responseXml)) HandleError(responseXml);

                    var decodedFileContent = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/payload").InnerText);

                    // Return the XmlDocument that we have received from the remote service
                    XmlDocument? xmlDocument = new XmlDocument();
                    try
                    {
                        xmlDocument.LoadXml(decodedFileContent);
                    }
                    catch (Exception ex)
                    {
                        xmlDocument = GenerateErrorXml("Could not parse recieved content into an XML document", $"recieved content: {TruncateString(decodedFileContent, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }

                    return xmlDocument;
                }
            }

        }


        /// <summary>
        /// Interfaces with the Taxxor Role Based Access Control service
        /// </summary>
        public static class AccessControlService
        {

            /// <summary>
            /// Flushes the cache in the access control service and reloads it's source data fresh from the disk
            /// </summary>
            /// <returns></returns>
            public static async Task<TaxxorReturnMessage> Flush()
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                // Call the service and retrieve the result as a string
                try
                {
                    var flushResponse = await CallTaxxorConnectedService<string>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "flushrbac", debugRoutine);
                    if (flushResponse.Contains("error", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return new TaxxorReturnMessage(false, "Error clearing the Access Control Service cache", flushResponse);
                    }
                    else
                    {
                        return new TaxxorReturnMessage(true, "Successfully cleared the Accesas Control Service cache", flushResponse);
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Error clearing the Access Control Service cache");
                    return new TaxxorReturnMessage(false, "Error clearing the Access Control Service cache", ex.ToString());
                }
            }


            /// <summary>
            /// Returns all the groups that have been defined in the Access Control Service
            /// Optionally add a user id so that the routine returns the groups that a specific user is a member of
            /// </summary>
            /// <param name="userId"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> ListGroups(string userId = null)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                if (userId != null)
                {
                    //
                    // => Retrieve all the groups so we can grab the group name
                    //
                    var groupInformation = new Dictionary<string, string>();
                    var xmlAllGroups = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "groupsList", debugRoutine);
                    xmlAllGroups = PostProcessGroupsResponse(xmlAllGroups);
                    foreach (XmlNode nodeGroup in xmlAllGroups.SelectNodes("/groups/group"))
                    {
                        var groupId = nodeGroup.GetAttribute("id");
                        var groupName = nodeGroup.SelectSingleNode("name")?.InnerText ?? "";
                        groupInformation.Add(groupId, groupName);
                    }

                    //
                    // => Now retrieve the groups per user and then combine that with the information we have grabbed above
                    //
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "userid", userId }
                    };
                    var xmlUserGroups = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "get_api_users_id_groups", dataToPost, true);

                    if (XmlContainsError(xmlUserGroups))
                    {
                        // There are special status codes that the RBAC service returns when the user cannot be found
                        var rbacResponseCheck = CheckRbacErrorResponse(projectVars, xmlUserGroups);
                        if (!rbacResponseCheck.Success)
                        {
                            return rbacResponseCheck.ToXml();
                        }
                        return GenerateErrorXml($"Could not retrieve groups for user '{userId}' from RBAC service", $"original-url: {reqVars.rawUrl}, error: {xmlUserGroups.OuterXml}, stack-trace: {GetStackTrace()}");
                    }

                    var xmlUserGroupsToReturn = new XmlDocument();
                    var nodeGroups = xmlUserGroupsToReturn.CreateElement("groups");
                    foreach (XmlNode nodeId in xmlUserGroups.SelectNodes("/user/groups/id"))
                    {
                        var nodeGroup = xmlUserGroupsToReturn.CreateElement("group");
                        nodeGroup.SetAttribute("id", nodeId.InnerText);

                        var nodeGroupName = xmlUserGroupsToReturn.CreateElementWithText("name", groupInformation[nodeId.InnerText]);
                        nodeGroup.AppendChild(nodeGroupName);

                        nodeGroups.AppendChild(nodeGroup);
                    }
                    xmlUserGroupsToReturn.AppendChild(nodeGroups);
                    return xmlUserGroupsToReturn;

                }
                else
                {
                    // Call the service and retrieve the result as XML
                    var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "groupsList", debugRoutine);

                    return PostProcessGroupsResponse(responseXml);
                }

                XmlDocument PostProcessGroupsResponse(XmlDocument responseXml)
                {

                    // Handle error
                    if (XmlContainsError(responseXml))
                    {
                        // There are special status codes that the RBAC service returns when the user cannot be found
                        var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                        if (!rbacResponseCheck.Success)
                        {
                            return rbacResponseCheck.ToXml();
                        }

                        return GenerateErrorXml($"Could not retrieve groups from RBAC service", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        var xml = "";
                        XmlDocument xmlGroups = new XmlDocument();

                        try
                        {
                            // The XML content that we are looking for is in decoded form available in the message node
                            xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                            xmlGroups.LoadXml(xml);

                            // Rename the root node
                            RenameNode(xmlGroups.DocumentElement, "", "groups");

                            return xmlGroups;
                        }
                        catch (Exception ex)
                        {
                            return GenerateErrorXml("Could not parse recieved RBAC group information content into an XML document", $"recieved content: {TruncateString(xml, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                        }
                    }

                }


            }

            /// <summary>
            /// Retrieves all the users from the RBAC service
            /// </summary>
            /// <returns></returns>
            public static async Task<XmlDocument> ListUsers(bool includeDisabledUsers = false)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);


                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "usersList", debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        return rbacResponseCheck.ToXml();
                    }

                    return GenerateErrorXml("Could not retrieve users from RBAC service", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    var xml = "";
                    XmlDocument xmlUsers = new XmlDocument();

                    try
                    {
                        // The XML content that we are looking for is in decoded form available in the message node
                        xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                        xmlUsers.LoadXml(xml);

                        // Rename the root node
                        RenameNode(xmlUsers.DocumentElement, "", "users");

                        // Optionally filter out disabled users
                        if (!includeDisabledUsers)
                        {
                            var nodeListDisabledUsers = xmlUsers.SelectNodes("/users/user[@disabled='true']");
                            if (nodeListDisabledUsers.Count > 0)
                            {
                                RemoveXmlNodes(nodeListDisabledUsers);
                            }
                        }

                        return xmlUsers;
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml("Could not parse recieved users content into an XML document", $"recieved content: {TruncateString(xml, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }


            }

            /// <summary>
            /// Lists the users from a group
            /// </summary>
            /// <param name="groupId"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> ListUsersInGroup(string groupId)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                var dataToPost = new Dictionary<string, string>
                {
                    /*
   Call the Taxxor Access Control Service to retrieve the data
   */
                    { "groupid", groupId }
                };

                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "groupsRead", dataToPost, debugRoutine);

                if (debugRoutine)
                {
                    Console.WriteLine("----- ListUsersInGroup -----");
                    Console.WriteLine($"- responseXml: {responseXml.OuterXml}");
                    Console.WriteLine("-----------------------------------------------------------------");
                }


                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        return rbacResponseCheck.ToXml();
                    }

                    return GenerateErrorXml("Could not retrieve group content from RBAC service", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    var xml = "";
                    XmlDocument xmlUsers = new XmlDocument();


                    try
                    {
                        // The XML content that we are looking for is in decoded form available in the message node
                        xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                        xmlUsers.LoadXml(xml);

                        // Rename the root node
                        RenameNode(xmlUsers.DocumentElement, "", "users");

                        return xmlUsers;
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml("Could not parse recieved users content into an XML document", $"recieved content: {TruncateString(xml, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }
            }

            /// <summary>
            /// Lists all the roles that have been defined in the Access Control Service
            /// </summary>
            /// <returns></returns>
            public static async Task<XmlDocument> ListRoles()
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "rolesList", debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        return rbacResponseCheck.ToXml();
                    }

                    return GenerateErrorXml("Could not list roles", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    XmlDocument xmlRoles = new XmlDocument();
                    var xml = "";

                    try
                    {
                        // The XML content that we are looking for is in decoded form available in the message node
                        xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                        xmlRoles.LoadXml(xml);

                        // Rename the root node
                        RenameNode(xmlRoles.DocumentElement, "", "roles");

                        return xmlRoles;
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml("Could not parse recieved roles content into an XML document", $"recieved content: {TruncateString(xml, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }


            }

            /// <summary>
            /// Retrieves the permissions that the current user has for the current resource (page/route)
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<TaxxorPermissions> RetrievePermissions(string projectId = null, bool debugRoutine = false)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                // Exclude "polling" routes from debugging output
                if (reqVars.pageId == "filingeditorlistlocks" || projectVars.isInternalServicePage) debugRoutine = false;

                string? projectIdToUse = projectId ?? projectVars.projectId;

                return await RetrievePermissions(reqVars.method, projectIdToUse, reqVars.currentHierarchyNode, false, debugRoutine);
            }


            /// <summary>
            /// Retrieves the permissions that the current user has for the current resource (page/route)
            /// </summary>
            /// <param name="method"></param>
            /// <param name="projectId"></param>
            /// <param name="nodeItem"></param>
            /// <param name="disableApiRouteCheck"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<TaxxorPermissions?> RetrievePermissions(RequestMethodEnum method, string projectId, XmlNode nodeItem, bool disableApiRouteCheck, bool debugRoutine = false)
            {
                // The filing source data
                var dataToPost = new Dictionary<string, string>
                {
                    /*
   Call the Taxxor Access Control Service to retrieve the data
   */
                    { "resourcebreadcrumbids", GenerateRbacBreadcrumbTrail(method, projectId, nodeItem, disableApiRouteCheck) }
                };
                // Console.WriteLine($"projectId: {projectId}, breadcrumb: {dataToPost["resourcebreadcrumbids"]}");

                // Call the service and retrieve the result as XML
                XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "effectivePermissions", dataToPost, debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        appLogger.LogError($"{rbacResponseCheck.Message}, debuginfo: {rbacResponseCheck.DebugInfo}, stack-trace: {GetStackTrace()}");

                        return null;
                    }
                    else
                    {
                        // Log the error so that we can inspect what went wrong
                        Console.WriteLine($"ERROR: Could not retrieve permissions from RBAC service. error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");

                        // Return nothing
                        return null;
                    }
                }
                else
                {
                    // The XML content that we are looking for is in decoded form available in the message node
                    var xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                    try
                    {
                        var xmlSourceData = new XmlDocument();
                        xmlSourceData.LoadXml(xml);

                        // if (debugRoutine && !isApiRoute()) await xmlSourceData.SaveAsync($"{logRootPathOs}/effective-permissions.xml", false);

                        var taxxorPermissions = new TaxxorPermissions(xmlSourceData);

                        return taxxorPermissions;
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, "Could not load the source data for the RBAC permissions");
                        Console.WriteLine($"ERROR: Could not load the source data for the RBAC permissions. error: {ex}, xml: {TruncateString(xml, 100)}, {_generateStandardDebugString(dataToPost)}, stack-trace: {GetStackTrace()}");

                        return null;
                    }
                }
            }

            /// <summary>
            /// Retrieves the effective permissions for a specific resource ID
            /// </summary>
            /// <param name="resourceId"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<TaxxorPermissions?> RetrievePermissionsForResource(string resourceId, bool logWarnings, bool debugRoutine)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                // Exclude "polling" routes from debugging output
                if (reqVars.pageId == "filingeditorlistlocks" || projectVars.isInternalServicePage) debugRoutine = false;

                string generateDebugString()
                {
                    return $"projectId: {projectVars.projectId}, resourceId: {resourceId}, path: {reqVars.rawUrl}";
                }


                // The filing source data
                var dataToPost = new Dictionary<string, string>
                {
                    /*
   Call the Taxxor Access Control Service to retrieve the data
   */
                    { "resourcebreadcrumbids", resourceId }
                };

                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "effectivePermissions", dataToPost, debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        appLogger.LogError($"{rbacResponseCheck.Message}, debuginfo: {rbacResponseCheck.DebugInfo}, {generateDebugString()}, stack-trace: {GetStackTrace()}");
                        return null;
                    }
                    else
                    {
                        appLogger.LogError($"Could not retrieve permissions. (error: {responseXml.OuterXml}, {generateDebugString()}, stack-trace: {GetStackTrace()})");
                        return null;
                    }
                }
                else
                {
                    // The XML content that we are looking for is in decoded form available in the message node
                    var xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                    if (debugRoutine)
                    {
                        appLogger.LogDebug($"ACL response: \n{PrettyPrintXml(responseXml)}");
                    }

                    try
                    {
                        var xmlSourceData = new XmlDocument();
                        xmlSourceData.LoadXml(xml);

                        // if (debugRoutine && !isApiRoute()) await xmlSourceData.SaveAsync($"{logRootPathOs}/effective-permissions.xml", false);

                        var nodeListPermissions = xmlSourceData.SelectNodes("/ArrayOfPermission/permission");
                        if (nodeListPermissions.Count == 0)
                        {
                            if (logWarnings) appLogger.LogWarning($"No permissions were received. ({generateDebugString()})");
                            return new TaxxorPermissions();
                            // return null; //TODO: should we return null in this case?
                        }
                        else
                        {
                            return new TaxxorPermissions(nodeListPermissions);
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError($"Could not retrieve RBAC permissions for the resource. (error: {ex}, xml: {TruncateString(xml, 100)}, {generateDebugString()}, stack-trace: {GetStackTrace()})");

                        return null;
                    }
                }

            }

            /// <summary>
            /// Retrieves the effective permissions for the current user for multiple resource ID's
            /// </summary>
            /// <param name="resourceIds">Format (multiple sets devided by ":") item-1,parent-1,grand-parent-1,root:item-2,parent-2,root</param>
            /// <param name="userId">A specific user ID to retrieve the effective permissions for</param>
            /// <param name="debugRoutine">Run in debug mode</param>
            /// <param name="logFileName">Filename to dump the XML to that we will be sending to the webservice</param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrievePermissionsForResources(string resourceIds, string? userId = null, bool debugRoutine = false, string? logFileName = null)
            {
                debugRoutine = debugRoutine || (siteType == "local" || siteType == "dev");
                var logFolderPathOs = $"{logRootPathOs}/inspector";

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                var xmlToPost = new XmlDocument();
                var nodeRequest = xmlToPost.CreateElement("request");
                xmlToPost.AppendChild(nodeRequest);

                if (resourceIds.Contains(":"))
                {
                    // Loop trough the sets
                    string[] breadcrumbtrails = resourceIds.Split(':');
                    foreach (string breadcrumb in breadcrumbtrails)
                    {
                        var nodeResource = xmlToPost.CreateElement("resource");
                        nodeResource.InnerText = breadcrumb;
                        nodeRequest.AppendChild(nodeResource);
                    }
                }
                else
                {
                    var nodeResource = xmlToPost.CreateElement("resource");
                    nodeResource.InnerText = resourceIds;
                    nodeRequest.AppendChild(nodeResource);
                }

                var userIdToCheck = userId ?? projectVars.currentUser.Id;
                xmlToPost.DocumentElement.SetAttribute("userId", userIdToCheck);

                // Console.WriteLine($"--- DUMP of XML ---");
                // Console.WriteLine(xmlToPost.OuterXml);
                // Console.WriteLine("");

                if (string.IsNullOrEmpty(userIdToCheck) || userIdToCheck == "unknown")
                {
                    return GenerateErrorXml("User not logged in", $"userId: {((string.IsNullOrEmpty(userIdToCheck)) ? "" : userIdToCheck)}, stack-trace: {GetStackTrace()}");
                }


                if (logFileName != null)
                {
                    if (!Directory.Exists(logFolderPathOs)) Directory.CreateDirectory(logFolderPathOs);
                    await xmlToPost.SaveAsync($"{logFolderPathOs}/{logFileName}", false, true);
                }



                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Post, "effectivePermissionsResources", xmlToPost, debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        return rbacResponseCheck.ToXml();
                    }

                    // If there is no special status code, then simply return a generic error
                    return GenerateErrorXml("Could not retrieve permissions for resources", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // The XML content that we are looking for is in decoded form available in the message node
                    var xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                    try
                    {
                        var xmlSourceData = new XmlDocument();
                        xmlSourceData.LoadXml(xml);

                        if (logFileName != null)
                        {
                            await xmlSourceData.SaveAsync($"{logFolderPathOs}/{Path.GetFileName(logFileName)}.response{Path.GetExtension(logFileName)}", false, true);
                        }

                        var xmlNormalizedReturnData = new XmlDocument();
                        xmlNormalizedReturnData.AppendChild(xmlNormalizedReturnData.CreateElement("items"));
                        // xmlNormalizedReturnData.LoadXml("<items/>");

                        foreach (XmlNode nodeOriginalItem in xmlSourceData.SelectNodes("/response/items/resourceItem"))
                        {
                            XmlElement nodeItem = xmlNormalizedReturnData.CreateElement("item");
                            nodeItem.SetAttribute("id", GetAttribute(nodeOriginalItem, "id"));

                            var nodeListPermissions = nodeOriginalItem.SelectNodes("permissions/permission");

                            if (nodeListPermissions.Count > 0)
                            {

                                var nodePermissions = xmlNormalizedReturnData.CreateElement("permissions");
                                // If the list of permissions returned contains "all", then this is the only permission that we need to return
                                if (nodeOriginalItem.SelectSingleNode("permissions/permission[@id='all']") != null)
                                {
                                    var nodeAllPermission = xmlNormalizedReturnData.CreateElement("permission");
                                    nodeAllPermission.SetAttribute("id", "all");
                                    nodePermissions.AppendChild(nodeAllPermission);
                                }
                                else
                                {
                                    foreach (XmlNode nodeOriginalItemPermission in nodeListPermissions)
                                    {
                                        var clonedPermissionNode = nodeOriginalItemPermission.CloneNode(false);
                                        var importedPermissionNode = xmlNormalizedReturnData.ImportNode(clonedPermissionNode, false);
                                        nodePermissions.AppendChild(importedPermissionNode);
                                    }
                                }
                                nodeItem.AppendChild(nodePermissions);
                            }

                            xmlNormalizedReturnData.DocumentElement.AppendChild(nodeItem);

                        }

                        return xmlNormalizedReturnData;
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml("Could not retrieve permissions for multiple resources", $"error: {ex}, xml: {TruncateString(xml, 100)}, resourceIds: {resourceIds}, stack-trace: {GetStackTrace()}");
                    }
                }

            }

            /// <summary>
            /// Retrieves users and groups that are interited from ACL settings set on one of the parent elements
            /// </summary>
            /// <param name="resourceIds">Breadcrumbtrail of comma delimited resource ID's. Format item-1,parent-1,grand-parent-1,root:item-2,parent-2,root</param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveEffectiveRolesPerUserGroup(string resourceIds, bool debugRoutine = false)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                // The filing source data
                var dataToPost = new Dictionary<string, string>
                {
                    /*
   Call the Taxxor Access Control Service to retrieve the data
   */
                    { "resourcebreadcrumbids", resourceIds }
                };

                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "effectiveRolesPerUserGroup", dataToPost, debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        return rbacResponseCheck.ToXml();
                    }

                    // If there is no special status code, then simply return a generic error
                    return GenerateErrorXml($"Could not retrieve roles for a user or group. original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // The XML content that we are looking for is in decoded form available in the message node
                    var xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                    try
                    {
                        var xmlSourceData = new XmlDocument();
                        xmlSourceData.LoadXml(xml);

                        return xmlSourceData;
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml("Could not retrieve user roles for a user or group", $"error: {ex}, xml: {TruncateString(xml, 100)}, resourceIds: {resourceIds}, stack-trace: {GetStackTrace()}");
                    }
                }
            }

            /// <summary>
            /// Retrieves the hash from the RBAC service which indicates the status of the RBAC source data. When the data is changed, then the hash updates as well
            /// </summary>
            /// <param name="debug"></param>
            /// <returns></returns>
            public static async Task<string?> RetrieveHash(bool debug = false)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev" || debug);

                // Call the service and retrieve the result as XML
                XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "calculateHash", debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    Console.WriteLine($"ERROR: Could not retrieve RBAC hash. response: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");

                    return null;
                }
                else
                {
                    // Return the hash
                    try
                    {
                        return responseXml.SelectSingleNode("/template/result/payload").InnerText;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR: Could not retrieve the RBAC hash. error: {ex}, stack-trace: {GetStackTrace()}");

                        return null;
                    }
                }
            }

            /// <summary>
            /// Retrieves a single resource from the RBAC service
            /// </summary>
            /// <param name="resourceId"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveResource(string resourceId, bool exitOnError = true)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                // The filing source data
                var dataToPost = new Dictionary<string, string>
                {
                    /*
   Call the Taxxor Access Control Service to retrieve the data
   */
                    { "resourceid", resourceId }
                };

                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "resourcesRead", dataToPost, debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    if (exitOnError)
                    {
                        // There are special status codes that the RBAC service returns when the user cannot be found
                        var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                        if (!rbacResponseCheck.Success)
                        {
                            return rbacResponseCheck.ToXml();
                        }

                        return GenerateErrorXml("Could not retrieve RBAC resource", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        return responseXml;
                    }
                }
                else
                {
                    string xml = "";
                    try
                    {
                        // The XML content that we are looking for is in decoded form available in the message node
                        xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                        XmlDocument xmlResource = new XmlDocument();

                        xmlResource.LoadXml(xml);

                        return xmlResource;
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml("Could not parse recieved RBAC resource content into an XML document", $"recieved content: {TruncateString(xml, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }
            }

            /// <summary>
            /// Edit an existing RBAC resource record
            /// </summary>
            /// <param name="resourceId"></param>
            /// <param name="resetInheritance"></param>
            /// <param name="isPersistant"></param>
            /// <returns></returns>
            public static async Task<bool> EditResource(string resourceId, bool resetInheritance, bool isPersistant)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                // First test if the resource exists and if it doesn't then we create it first
                var xmlAclResources = await ListResources();
                Console.WriteLine(xmlAclResources.OuterXml);
                if (xmlAclResources.SelectSingleNode($"/resources/resource[@id='{resourceId}']") == null)
                {
                    // Resource record does not exist yet, so we create it
                    appLogger.LogInformation($"CREATE resource record with ID '{resourceId}'");

                    // Create the resource in the Access Control Service
                    if (!resetInheritance)
                    {
                        return await AddResource(resourceId, resetInheritance, true);
                    }
                    else
                    {
                        var addResourceResult = await AddResource(resourceId, resetInheritance, true);
                        if (!addResourceResult) return false;

                        // Add the Taxxor administrator as a minimum required access control record
                        /*
                        <accessRecord>
                        <groupRef ref="administrators" />
                        <roleRef ref="taxxor-administrator" />
                        <resourceRef ref="get__taxxoreditor__cms_project-details__ar20" />
                        </accessRecord>
                        */

                        return await AddAccessRecord(resourceId, "administrators", "taxxor-administrator");
                    }


                }
                else
                {
                    // Resource record exists, so we can edit it
                    appLogger.LogInformation($"EDIT resource record with ID '{resourceId}'");

                    var xmlPost = new XmlDocument();
                    xmlPost.LoadXml($"<resource id=\"{resourceId}\" />");
                    if (resetInheritance)
                    {
                        SetAttribute(xmlPost.DocumentElement, "reset-inheritance", "true");
                    }
                    if (isPersistant)
                    {
                        SetAttribute(xmlPost.DocumentElement, "isPersistant", "true");
                    }

                    // Call the service and retrieve the result as XML
                    var response = await CallTaxxorConnectedService<string>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Put, "resourcesUpdate", xmlPost, debugRoutine);

                    // Dump debug information to the console
                    // if (debugRoutine)
                    // {
                    //     Console.WriteLine("");
                    //     Console.WriteLine("");
                    //     Console.WriteLine("EditResource:");
                    //     Console.WriteLine($"|{response}|");
                    //     Console.WriteLine("");
                    //     Console.WriteLine("");
                    //     Console.WriteLine("");
                    // }

                    // Check in the XML what the status is
                    if (string.IsNullOrEmpty(response.Trim()))
                    {
                        return true;
                    }
                    else
                    {
                        appLogger.LogError($"Could not edit RBAC resource. response: {response}, original-url: {reqVars.rawUrl}, resourceId: {resourceId}, resetInheritance: {resetInheritance.ToString()}, isPersistant: {isPersistant.ToString()}, stack-trace: {GetStackTrace()}");
                        return false;
                    }
                }


            }

            /// <summary>
            /// List all resources and optionally filter for a project ID
            /// </summary>
            /// <param name="projectId"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> ListResources(string? projectId = null, bool includeAccessRecordCount = false)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                XmlDocument xmlResources = new XmlDocument();

                if (includeAccessRecordCount)
                {
                    //
                    // => Retrieve a list of access records including a counter that indicates how many access records are associated with that access record
                    //

                    var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "AccessRecordsPerResource", debugRoutine);
                    if (XmlContainsError(responseXml))
                    {
                        // There are special status codes that the RBAC service returns when the user cannot be found
                        var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                        if (!rbacResponseCheck.Success)
                        {
                            return rbacResponseCheck.ToXml();
                        }

                        return GenerateErrorXml("Could not list RBAC resources", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                    }

                    // Normalize the response

                    var reworkedXmlResponse = new XmlDocument();
                    var nodeRoot = reworkedXmlResponse.CreateElement("resources");
                    foreach (XmlNode nodeItem in responseXml.SelectNodes("/accessRecordsPerResource/item"))
                    {
                        string? resourceId = nodeItem.SelectSingleNode("id")?.InnerText;
                        if (resourceId != null)
                        {
                            // Filter the resources
                            var renderResource = true;
                            if (projectId != null && !resourceId.Contains($"__{projectId}")) renderResource = false;


                            if (renderResource)
                            {
                                var nodeResource = reworkedXmlResponse.CreateElement("resource");
                                nodeResource.SetAttribute("id", resourceId);
                                nodeResource.SetAttribute("accessrecordcount", nodeItem.SelectSingleNode("count")?.InnerText ?? "");

                                var nodeResetInheritance = nodeItem.SelectSingleNode("reset-inheritance");
                                if (nodeResetInheritance != null)
                                {
                                    if (nodeResetInheritance.InnerText == "true")
                                    {
                                        nodeResource.SetAttribute("reset-inheritance", "true");
                                    }
                                }

                                nodeRoot.AppendChild(nodeResource);
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Unable to find a resource ID in {nodeItem.OuterXml}");
                        }
                    }
                    reworkedXmlResponse.AppendChild(nodeRoot);

                    return reworkedXmlResponse;

                }
                else
                {
                    //
                    // => Retrieve a plain list of access records
                    //
                    var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "resourcesList", debugRoutine);

                    return PostProcessResourcesListResponse(responseXml);
                }

                /// <summary>
                /// Post processes the result from a call to resourcesList
                /// </summary>
                /// <param name="responseXml"></param>
                /// <returns></returns>
                XmlDocument PostProcessResourcesListResponse(XmlDocument responseXml)
                {

                    // Handle error
                    if (XmlContainsError(responseXml))
                    {
                        // There are special status codes that the RBAC service returns when the user cannot be found
                        var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                        if (!rbacResponseCheck.Success)
                        {
                            return rbacResponseCheck.ToXml();
                        }

                        return GenerateErrorXml("Could not list RBAC resources", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                    }

                    string xml = "";

                    try
                    {
                        // The XML content that we are looking for is in decoded form available in the message node
                        xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                        xmlResources.LoadXml(xml);

                        // Rename the root node
                        RenameNode(xmlResources.DocumentElement, "", "resources");

                        // Filter the list to only contain the nodes that we need
                        if (projectId != null)
                        {
                            var nodeListResources = xmlResources.SelectNodes("/resources/resource");
                            foreach (XmlNode nodeResource in nodeListResources)
                            {
                                if (!GetAttribute(nodeResource, "id").Contains($"__{projectId}")) RemoveXmlNode(nodeResource);
                            }
                        }

                        return xmlResources;
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml("Could not parse recieved resource content into an XML document", $"recieved content: {TruncateString(xml, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }


            }

            /// <summary>
            /// Retrieve all the access records available in the RBAC service
            /// </summary>
            /// <returns></returns>
            public static async Task<XmlDocument> ListAccessRecords()
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                XmlDocument xmlAccessRecords = new XmlDocument();

                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "accessRecords", debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        return rbacResponseCheck.ToXml();
                    }

                    return GenerateErrorXml("Could not list all RBAC access records", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    string xml = "";

                    try
                    {
                        // The XML content that we are looking for is in decoded form available in the message node
                        xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                        xmlAccessRecords.LoadXml(xml);

                        // Rename the root node
                        RenameNode(xmlAccessRecords.DocumentElement, "", "accessrecords");

                        return xmlAccessRecords;
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml("Could not parse recieved complete RBAC access records into an XML document", $"recieved content: {TruncateString(xml, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }
            }

            /// <summary>
            /// Retrieves a list of all the access records for a resource
            /// </summary>
            /// <param name="resourceId"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> ListAccessRecords(string resourceId)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                XmlDocument xmlAccessRecords = new XmlDocument();

                var dataToPost = new Dictionary<string, string>
                {
                    { "resourceid", resourceId }
                };

                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "accessRecordsPerResource", dataToPost, debugRoutine);

                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        return rbacResponseCheck.ToXml();
                    }

                    return GenerateErrorXml("Could not list RBAC access records for a resource", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    var xml = "";

                    try
                    {
                        // The XML content that we are looking for is in decoded form available in the message node
                        xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);


                        xmlAccessRecords.LoadXml(xml);

                        // Rename the root node
                        RenameNode(xmlAccessRecords.DocumentElement, "", "accessrecords");

                        return xmlAccessRecords;
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml("Could not parse recieved RBAC access records into an XML document", $"recieved content: {TruncateString(xml, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }
            }


            /// <summary>
            /// Adds an access record (combination of resource ID, user/group ID and role ID) to the system
            /// </summary>
            /// <param name="rbacAccessRecords"></param>
            /// <param name="logAddResourceIssues"></param>
            /// <returns></returns>
            public static async Task<bool> AddAccessRecord(List<RbacAccessRecord> rbacAccessRecords, bool logAddResourceIssues = true)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");
                var successCounter = 0;
                var errorCounter = 0;

                foreach (var rbacAccessRecord in rbacAccessRecords)
                {
                    var resourceId = rbacAccessRecord.resourceref;
                    var userGroupId = (rbacAccessRecord.groupref == null) ? rbacAccessRecord.userref : rbacAccessRecord.groupref;
                    var roleId = rbacAccessRecord.roleref;
                    var resetInheritance = rbacAccessRecord.resetinheritance;

                    var success = await AddAccessRecord(resourceId, userGroupId, roleId, true, resetInheritance, logAddResourceIssues);
                    if (success)
                    {
                        successCounter++;
                    }
                    else
                    {
                        appLogger.LogError($"Failed to add access record. resourceId: {resourceId}, userGroupId: {userGroupId}, roleId: {roleId}");
                        errorCounter++;
                    }
                }

                if (errorCounter == 0)
                {
                    appLogger.LogInformation($"Successfully added {successCounter} access records");
                    return true;
                }
                else if (successCounter == 0)
                {
                    appLogger.LogError($"Failed to add {errorCounter} access records, no access records were added");
                    return false;
                }
                else
                {
                    // Partly successful
                    appLogger.LogWarning($"Unable to add all access records: errors: {errorCounter} success: {successCounter}");
                    return true;
                }
            }


            /// <summary>
            /// Adds an access record (combination of resource ID, user/group ID and role ID) to the system
            /// (optionally adds a resource record as well)
            /// </summary>
            /// <param name="resourceId"></param>
            /// <param name="userGroupId"></param>
            /// <param name="roleId"></param>
            /// <param name="addResource"></param>
            /// <param name="resetInheritance"></param>
            /// <param name="logAddResourceIssues"></param>
            /// <returns></returns>
            public static async Task<bool> AddAccessRecord(string resourceId, string userGroupId, string roleId, bool addResource = true, bool resetInheritance = false, bool logAddResourceIssues = true)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                if (addResource)
                {
                    var resultAddResource = await AddResource(resourceId, resetInheritance, logAddResourceIssues);
                    if (!resultAddResource && logAddResourceIssues)
                    {
                        appLogger.LogWarning($"Unable to add resource to the RBAC information, bacause it probably already exists. resourceId: {resourceId}, userGroupId: {userGroupId}, roleId: {roleId}, stack-trace: {GetStackTrace()}");
                    }
                }

                /*
                Call the Taxxor Access Control Service to add the access record
                */
                var dataToPost = new Dictionary<string, string>
                {
                    { "resourceid", resourceId },
                    { "usergroupid", userGroupId },
                    { "roleid", roleId }
                };

                // Call the service and retrieve the result as XML
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Post, "setUserGroupPermissions", dataToPost, debugRoutine);

                // Dump debug information to the console
                // if (debugRoutine)
                // {
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("AddAccessRecord:");
                //     Console.WriteLine($"|{xmlResponse.OuterXml}|");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                // }

                // Check in the XML what the status is
                var nodeStatus = xmlResponse.SelectSingleNode("/template/result/message");
                if (nodeStatus != null && nodeStatus.InnerText.ToLower() == "ok")
                {
                    return true;
                }
                else
                {
                    appLogger.LogWarning($"Unable to add access record to the RBAC information, bacause it probably already exists. xmlResponse: {xmlResponse.OuterXml}, resourceId: {resourceId}, userGroupId: {userGroupId}, roleId: {roleId}, stack-trace: {GetStackTrace()}");
                    return false;
                }
            }

            /// <summary>
            /// Edits an existing RBAC Access Records
            /// </summary>
            /// <param name="rbacAccessRecord"></param>
            /// <param name="logAddResourceIssues"></param>
            /// <returns></returns>
            public static async Task<bool> EditAccessRecord(RbacAccessRecord rbacAccessRecord, bool logAddResourceIssues = true)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var accessRecordsToEdit = new List<RbacAccessRecord>
                {
                    rbacAccessRecord
                };

                return await EditAccessRecord(accessRecordsToEdit, logAddResourceIssues);
            }

            /// <summary>
            /// Bulk edits a list of RBAC Access Records
            /// </summary>
            /// <param name="rbacAccessRecords"></param>
            /// <param name="logAddResourceIssues"></param>
            /// <returns></returns>
            public static async Task<bool> EditAccessRecord(List<RbacAccessRecord> rbacAccessRecords, bool logAddResourceIssues = true)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                /*
                Call the Taxxor Access Control Service to add the access record
                */
                var xmlToPost = new XmlDocument();
                var nodeRequest = xmlToPost.CreateElement("request");
                var nodeItems = xmlToPost.CreateElement("items");
                nodeRequest.AppendChild(nodeItems);
                xmlToPost.AppendChild(nodeRequest);
                foreach (var rbacAccessRecord in rbacAccessRecords)
                {
                    var nodeAccessRecordImported = xmlToPost.ImportNode(rbacAccessRecord.Export().DocumentElement, true);
                    nodeItems.AppendChild(nodeAccessRecordImported);
                }



                // Call the service and retrieve the result as XML
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Put, "updateAccessRecords", xmlToPost, debugRoutine);

                // Dump debug information to the console
                // if (debugRoutine)
                // {
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("EditAccessRecord:");
                //     Console.WriteLine($"|{xmlResponse.OuterXml}|");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                // }

                // Check in the XML what the status is
                var nodeStatus = xmlResponse.SelectSingleNode("/template/result/message");
                if (nodeStatus != null && nodeStatus.InnerText.ToLower() == "ok")
                {
                    if (xmlResponse.OuterXml.Contains("Records found: 0"))
                    {
                        appLogger.LogError($"Unable to find access records in the RBAC service and edit its information. xmlResponse: {xmlResponse.OuterXml}, xmlToPost: {xmlToPost.OuterXml}, stack-trace: {GetStackTrace()}");
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    appLogger.LogError($"Unable to edit access records RBAC information. xmlResponse: {xmlResponse.OuterXml}, xmlToPost: {xmlToPost.OuterXml}, stack-trace: {GetStackTrace()}");
                    return false;
                }
            }




            /// <summary>
            /// Removes an access record from the RBAC service
            /// </summary>
            /// <param name="resourceId"></param>
            /// <param name="userGroupId"></param>
            /// <param name="roleId"></param>
            /// <returns></returns>
            public static async Task<bool> DeleteAccessRecord(string resourceId, string userGroupId, string roleId)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                /*
                Call the Taxxor Access Control Service to add the access record
                */
                var dataToPost = new Dictionary<string, string>
                {
                    { "resourceid", resourceId },
                    { "usergroupid", userGroupId },
                    { "roleid", roleId }
                };

                // Call the service and retrieve the result as XML
                var response = await CallTaxxorConnectedService<string>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Delete, "removeUserGroupPermissions", dataToPost, debugRoutine);

                // Dump debug information to the console
                // if (debugRoutine)
                // {
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("DeleteAccessRecord:");
                //     Console.WriteLine($"|{response}|");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                // }

                // Check in the XML what the status is
                if (string.IsNullOrEmpty(response.Trim()))
                {
                    return true;
                }
                else
                {
                    appLogger.LogError($"Unable to remove access record from the RBAC information. response: {response}, resourceId: {resourceId}, userGroupId: {userGroupId}, roleId: {roleId}, stack-trace: {GetStackTrace()}");
                    return false;
                }
            }

            /// <summary>
            /// Adds a resource to the RBAC service
            /// </summary>
            /// <param name="resourceId"></param>
            /// <param name="resetInheritance"></param>
            /// <param name="logIssues"></param>
            /// <returns></returns>
            public static async Task<bool> AddResource(string resourceId, bool resetInheritance = false, bool logIssues = true)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                /*
                Call the Taxxor Access Control Service to retrieve the data
                */
                var xmlPost = new XmlDocument();
                xmlPost.LoadXml($"<resource id=\"{resourceId}\" />");
                if (resetInheritance)
                {
                    SetAttribute(xmlPost.DocumentElement, "reset-inheritance", "true");
                }

                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Post, "resourcesCreate", xmlPost, debugRoutine);

                // Dump debug information to the console
                // if (debugRoutine)
                // {
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("AddResource");
                //     Console.WriteLine($"|{responseXml.OuterXml}|");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                // }

                // If the response is empty, then the operation was successful
                var nodeStatus = responseXml.SelectSingleNode("/template/result/message");
                if (nodeStatus != null && nodeStatus.InnerText.ToLower() == "ok")
                {
                    return true;
                }
                else
                {
                    if (logIssues) appLogger.LogError($"There was a problem adding the resource. responseXml: {responseXml.OuterXml}, resourceId: {resourceId}, stack-trace: {GetStackTrace()}");
                    return false;
                }
            }

            /// <summary>
            /// Deletes all resources for a project
            /// </summary>
            /// <param name="projectId"></param>
            /// <returns></returns>
            public static async Task<bool> DeleteResources(string projectId)
            {
                var xmlResourcesFiltered = await ListResources(projectId);
                var nodeListResources = xmlResourcesFiltered.SelectNodes("/resources/resource");
                var overallSuccess = true;
                foreach (XmlNode nodeResoure in nodeListResources)
                {
                    var deleteSuccess = await DeleteResource(GetAttribute(nodeResoure, "id"));
                    if (!deleteSuccess) overallSuccess = false;
                }
                return overallSuccess;
            }

            /// <summary>
            /// Deletes a single resource
            /// </summary>
            /// <param name="resourceId"></param>
            /// <returns></returns>
            public static async Task<bool> DeleteResource(string resourceId)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                // The filing source data
                var dataToPost = new Dictionary<string, string>
                {
                    /*
   Call the Taxxor Access Control Service to retrieve the data
   */
                    { "resourceid", resourceId }
                };

                // Call the service and retrieve the result as XML
                var response = await CallTaxxorConnectedService<string>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Delete, "resourcesDelete", dataToPost, debugRoutine);

                // // Dump debug information to the console
                // if (debugRoutine)
                // {
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine($"|{response}|");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                // }

                // If the response is empty, then the operation was successfull
                if (string.IsNullOrEmpty(response))
                {
                    return true;
                }
                else
                {
                    appLogger.LogError(response);
                    return false;
                }
            }

            /// <summary>
            /// Clone all the RBAC resource and access records for a project
            /// </summary>
            /// <param name="sourceProjectId"></param>
            /// <param name="targetProjectId"></param>
            /// <returns></returns>
            public static async Task<bool> CloneResources(string sourceProjectId, string targetProjectId)
            {
                var xmlResourcesFiltered = await ListResources(sourceProjectId);
                var nodeListResources = xmlResourcesFiltered.SelectNodes("/resources/resource");
                var overallSuccess = true;
                foreach (XmlNode nodeResoure in nodeListResources)
                {
                    var sourceResourceId = GetAttribute(nodeResoure, "id");
                    var targetResourceId = sourceResourceId.Replace($"__{sourceProjectId}", $"__{targetProjectId}");

                    var cloneSuccess = await CloneResource(sourceResourceId, targetResourceId);
                    if (!cloneSuccess) overallSuccess = false;
                }
                return overallSuccess;
            }

            /// <summary>
            /// Clone a single RBAC resource and associated access records
            /// </summary>
            /// <param name="sourceResourceId"></param>
            /// <param name="targetResourceId"></param>
            /// <returns></returns>
            public static async Task<bool> CloneResource(string sourceResourceId, string targetResourceId)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                /*
                Call the Taxxor Access Control Service to retrieve the data
                */
                var dataToPost = new Dictionary<string, string>
                {
                    { "sourceresourceid", sourceResourceId },
                    { "targetresourceid", targetResourceId }
                };

                // Call the service and retrieve the result as XML
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "resourcesClone", dataToPost, debugRoutine);

                // Dump debug information to the console
                // if (debugRoutine)
                // {
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine($"|{xmlResponse.OuterXml}|");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                //     Console.WriteLine("");
                // }

                /// Check in the XML what the status is
                var nodeStatus = xmlResponse.SelectSingleNode("/template/result/message");
                if (nodeStatus != null && nodeStatus.InnerText.ToLower() == "ok")
                {
                    return true;
                }
                else
                {
                    appLogger.LogError($"Unable to clone resource to the RBAC information, bacause it probably already exists. xmlResponse: {xmlResponse.OuterXml}, sourceResourceId: {sourceResourceId}, targetResourceId: {targetResourceId}, stack-trace: {GetStackTrace()}");

                    return false;
                }
            }


            /// <summary>
            /// Dumps the complete content of the RBAC database as XML so that we can easily check it's content
            /// </summary>
            /// <returns></returns>
            public static async Task<XmlDocument?> DumpDbAsXml()
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                XmlDocument? xmlDatabaseContent = new XmlDocument();

                // Call the service and retrieve the result as XML
                var responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.AccessControlService, RequestMethodEnum.Get, "downloadRbacData", debugRoutine);

                if (debugRoutine)
                {
                    Console.WriteLine("----- DumpDbAsXml -----");
                    Console.WriteLine($"- responseXml: {responseXml.OuterXml}");
                    Console.WriteLine("-----------------------------------------------------------------");
                }


                // Handle error
                if (XmlContainsError(responseXml))
                {
                    // There are special status codes that the RBAC service returns when the user cannot be found
                    var rbacResponseCheck = CheckRbacErrorResponse(projectVars, responseXml);
                    if (!rbacResponseCheck.Success)
                    {
                        return rbacResponseCheck.ToXml();
                    }

                    return GenerateErrorXml("Could not retrieve database content from RBAC service", $"original-url: {reqVars.rawUrl}, error: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // The XML content that we are looking for is in decoded form available in the message node
                    var xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/template/result/payload").InnerText);

                    try
                    {
                        xmlDatabaseContent.LoadXml(xml);
                    }
                    catch (Exception ex)
                    {
                        xmlDatabaseContent = GenerateErrorXml("Could not parse recieved users content into an XML document", $"recieved content: {TruncateString(xml, 200)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }

                return xmlDatabaseContent;
            }

            /// <summary>
            /// Tests an error response from the RBAC service and determine if we need to render a special error message for the client
            /// </summary>
            /// <param name="responseXml"></param>
            /// <returns></returns>
            private static TaxxorReturnMessage CheckRbacErrorResponse(XmlDocument responseXml)
            {
                var context = System.Web.Context.Current;
                ProjectVariables projectVars = RetrieveProjectVariables(context);

                return CheckRbacErrorResponse(projectVars, responseXml);
            }


            /// <summary>
            /// Tests an error response from the RBAC service and determine if we need to render a special error message for the client
            /// </summary>
            /// <param name="projectVars"></param>
            /// <param name="responseXml"></param>
            private static TaxxorReturnMessage CheckRbacErrorResponse(ProjectVariables projectVars, XmlDocument responseXml)
            {
                // There are special status codes that the RBAC service returns when the user cannot be found
                var nodeStatusCode = responseXml.SelectSingleNode("/error/httpstatuscode");
                if (nodeStatusCode != null)
                {
                    var httpStatusCode = nodeStatusCode.InnerText;
                    if (httpStatusCode == "494")
                    {
                        var context = System.Web.Context.Current;
                        RequestVariables reqVars = RetrieveRequestVariables(context);
                        appLogger.LogDebug("----- RBAC Service Response -----");
                        appLogger.LogDebug(responseXml.OuterXml);
                        appLogger.LogDebug("---------------------------------");

                        return new TaxxorReturnMessage(false, "Not allowed", $"User {projectVars.currentUser.Id} is not allowed to view this resource ({reqVars.urlPath}) bacause the user could not be found in the Taxxor Access Control Service");
                    }
                }

                return new TaxxorReturnMessage(true, "Nothing special");
            }

        }

        /// <summary>
        /// Interfaces with the Generic Data Connector Service
        /// </summary>
        public static class GenericDataConnector
        {

            /// <summary>
            /// List all the tables that are available for this project
            /// </summary>
            /// <param name="projectId"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> ListExternalTables(string projectId)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);

                /*
                Call the Taxxor Document Store to retrieve the data
                */
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId }
                };

                // Call the service and retrieve the footnotes as XML
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.GenericDataConnectorService, RequestMethodEnum.Get, "tablelist", dataToPost, debugRoutine);

                var xmlSemiStructuredTables = new XmlDocument();
                if (!XmlContainsError(xmlResponse))
                {
                    // Process the data that we have just received

                    var nodePayload = xmlResponse.SelectSingleNode("/template/result/payload");
                    if (nodePayload == null)
                    {
                        HandleError(reqVars.returnType, "No response from Taxxor Generic Data Connector Service received", $"xmlResponse: {xmlResponse.OuterXml}, stack-trace: {GetStackTrace()}");
                    }

                    var payloadContent = HttpUtility.HtmlDecode(nodePayload.InnerText);

                    try
                    {
                        xmlSemiStructuredTables.LoadXml(payloadContent);
                    }
                    catch (Exception ex)
                    {
                        HandleError(reqVars.returnType, "Could not parse payload from Taxxor Generic Data Connector Service", $"error: {ex}, payloadContent: {payloadContent}, stack-trace: {GetStackTrace()}");
                    }
                }

                return xmlSemiStructuredTables;
            }

            /// <summary>
            /// Retrieves an external table
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="workbookId"></param>
            /// <param name="tableId"></param>
            /// <param name="asHtml"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveExternalTable(string projectId, string workbookId, string tableId, bool asHtml = true)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);

                // Normalise the table ID that was passed to this routine to make sure that the unique table reference is always posted to the Generic Data Connector
                var tableIdToUse = "table_" + CalculateBaseExternalTableId(tableId);

                /*
                Call the Taxxor Document Store to retrieve the data
                */
                // New URI format: http://genericdataconnector:4823/api/tables/download/table_xid11520308965830440725/ar20-sustainability
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "tableid", tableIdToUse },
                    { "workbookid", workbookId }
                };
                if (asHtml) dataToPost.Add("transform", "htmlTable"); // Forces the webservice to return an HTML table like structure

                // Call the service and retrieve the footnotes as XML
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.GenericDataConnectorService, RequestMethodEnum.Get, "downloadTable", dataToPost, debugRoutine);

                // HandleError(reqVars.returnType, "Thrown on purpose - RetrieveSemiStructuredTable() - Taxxor Editor", $"xmlResponse: {xmlResponse.OuterXml} stack-trace: {GetStackTrace()}");

                var xhtmlInlineFilingTable = new XmlDocument();

                if (XmlContainsError(xmlResponse))
                {
                    return xmlResponse;
                }
                else
                {

                    var nodePayload = xmlResponse.SelectSingleNode("/template/result/payload");
                    if (nodePayload == null)
                    {
                        HandleError(reqVars.returnType, "No response from Taxxor Generic Data Connector Service received", $"xmlResponse: {xmlResponse.OuterXml}, stack-trace: {GetStackTrace()}");
                    }

                    var payloadContent = HttpUtility.HtmlDecode(nodePayload.InnerText);

                    try
                    {
                        xhtmlInlineFilingTable.LoadXml(payloadContent);
                    }
                    catch (Exception ex)
                    {
                        HandleError(reqVars.returnType, "Could not parse payload from Taxxor Generic Data Connector Service", $"error: {ex}, payloadContent: {payloadContent}, stack-trace: {GetStackTrace()}");
                    }
                }

                return xhtmlInlineFilingTable;
            }

            /// <summary>
            /// Retrieves a table from the Generic Data Connector 
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="workbookId"></param>
            /// <param name="tableId"></param>
            /// <param name="asHtml"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveSdeTablePreview(string projectId, string workbookId, string tableId)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);

                // Normalise the table ID that was passed to this routine to make sure that the unique table reference is always posted to the Generic Data Connector
                var tableIdToUse = "table_" + CalculateBaseExternalTableId(tableId);

                /*
                Call the Taxxor Document Store to retrieve the data
                */
                // New URI format: http://genericdataconnector:4823/api/tables/download/table_xid11520308965830440725/ar20-sustainability
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "tableid", tableIdToUse },
                    { "workbookid", workbookId }
                };

                // Call the service and retrieve the footnotes as XML
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.GenericDataConnectorService, RequestMethodEnum.Get, "get_api_table_workbookId_sheetHash_preview", dataToPost, debugRoutine);

                // HandleError(reqVars.returnType, "Thrown on purpose - RetrieveSemiStructuredTable() - Taxxor Editor", $"xmlResponse: {xmlResponse.OuterXml} stack-trace: {GetStackTrace()}");

                var xhtmlInlineFilingTable = new XmlDocument();

                if (XmlContainsError(xmlResponse))
                {
                    return xmlResponse;
                }
                else
                {
                    try
                    {
                        xhtmlInlineFilingTable.LoadXml(xmlResponse.OuterXml);
                    }
                    catch (Exception ex)
                    {
                        HandleError(reqVars.returnType, "Could not parse response from Taxxor Generic Data Connector Service", $"error: {ex}, xhtmlInlineFilingTable: {xhtmlInlineFilingTable.OuterXml}, stack-trace: {GetStackTrace()}");
                    }
                }

                return xhtmlInlineFilingTable;
            }

            /// <summary>
            /// Retrieves an HTML table with SDE's which are created in the Structured Data Store when this route is requested
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="workbookId"></param>
            /// <param name="tableId"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveSdeTable(string projectId, string workbookId, string tableId)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);

                // Normalise the table ID that was passed to this routine to make sure that the unique table reference is always posted to the Generic Data Connector
                var tableIdToUse = "table_" + CalculateBaseExternalTableId(tableId);

                /*
                Call the Taxxor Document Store to retrieve the data
                */
                // New URI format: http://genericdataconnector:4823/api/table/{workbookId}/{sheetHash}/insert/{projectId}
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "tableid", tableIdToUse },
                    { "workbookid", workbookId }
                };

                // Call the service and retrieve the footnotes as XML
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.GenericDataConnectorService, RequestMethodEnum.Get, "get_api_table_workbookId_sheetHash_insert_projectId", dataToPost, debugRoutine);

                // HandleError(reqVars.returnType, "Thrown on purpose - RetrieveSemiStructuredTable() - Taxxor Editor", $"xmlResponse: {xmlResponse.OuterXml} stack-trace: {GetStackTrace()}");

                var xhtmlInlineFilingTable = new XmlDocument();

                if (XmlContainsError(xmlResponse))
                {
                    return xmlResponse;
                }
                else
                {

                    try
                    {
                        xhtmlInlineFilingTable.LoadXml(xmlResponse.OuterXml);
                    }
                    catch (Exception ex)
                    {
                        HandleError(reqVars.returnType, "Could not parse response from Taxxor Generic Data Connector Service", $"error: {ex}, xhtmlInlineFilingTable: {xhtmlInlineFilingTable.OuterXml}, stack-trace: {GetStackTrace()}");
                    }

                }

                return xhtmlInlineFilingTable;
            }

            /// <summary>
            /// Retrieves fresh SDE values for a specific SDE table
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="tableId"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveSdeTableValues(string projectId, string tableId)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);

                // Normalise the table ID that was passed to this routine to make sure that the unique table reference is always posted to the Generic Data Connector
                var tableIdToUse = "table_" + CalculateBaseExternalTableId(tableId);

                /*
                Call the Taxxor Document Store to retrieve the data
                */
                // New URI format: http://genericdataconnector:4823/api/table/{workbookId}/{sheetHash}/insert/{projectId}
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "tableid", tableIdToUse }
                };

                // Call the service and retrieve the footnotes as XML
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.GenericDataConnectorService, RequestMethodEnum.Get, "get_api_table_id_update", dataToPost, debugRoutine);

                // HandleError(reqVars.returnType, "Thrown on purpose - RetrieveSemiStructuredTable() - Taxxor Editor", $"xmlResponse: {xmlResponse.OuterXml} stack-trace: {GetStackTrace()}");

                var xhtmlInlineFilingTable = new XmlDocument();

                if (XmlContainsError(xmlResponse))
                {
                    return xmlResponse;
                }
                else
                {

                    var nodePayload = xmlResponse.SelectSingleNode("/template/result/payload");
                    if (nodePayload == null)
                    {
                        HandleError(reqVars.returnType, "No response from Taxxor Generic Data Connector Service received", $"xmlResponse: {xmlResponse.OuterXml}, stack-trace: {GetStackTrace()}");
                    }

                    var payloadContent = HttpUtility.HtmlDecode(nodePayload.InnerText);

                    try
                    {
                        xhtmlInlineFilingTable.LoadXml(payloadContent);
                    }
                    catch (Exception ex)
                    {
                        HandleError(reqVars.returnType, "Could not parse payload from Taxxor Generic Data Connector Service", $"error: {ex}, payloadContent: {payloadContent}, stack-trace: {GetStackTrace()}");
                    }
                }

                return xhtmlInlineFilingTable;
            }

            /// <summary>
            /// Lists all the workbooks available in the Taxxor Generic Document Store
            /// </summary>
            /// <returns></returns>
            public static async Task<XmlDocument> ListWorkbooks()
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);


                // Call the service and retrieve the workbook list as XML
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.GenericDataConnectorService, RequestMethodEnum.Get, "workbookitems", debugRoutine);

                // HandleError(reqVars.returnType, "Thrown on purpose - ListWorkbooks() - Taxxor Editor", $"xmlResponse: {xmlResponse.OuterXml} stack-trace: {GetStackTrace()}");

                var xmlWorkbookList = new XmlDocument();

                if (XmlContainsError(xmlResponse))
                {
                    return xmlResponse;
                }
                else
                {

                    var nodePayload = xmlResponse.SelectSingleNode("/template/result/payload");
                    if (nodePayload == null)
                    {
                        HandleError(reqVars.returnType, "No response from Taxxor Generic Data Connector Service received", $"xmlResponse: {xmlResponse.OuterXml}, stack-trace: {GetStackTrace()}");
                    }

                    var payloadContent = HttpUtility.HtmlDecode(nodePayload.InnerText);

                    try
                    {
                        xmlWorkbookList.LoadXml(payloadContent);
                    }
                    catch (Exception ex)
                    {
                        HandleError(reqVars.returnType, "Could not parse payload from Taxxor Generic Data Connector Service", $"error: {ex}, payloadContent: {payloadContent}, stack-trace: {GetStackTrace()}");
                    }
                }

                return xmlWorkbookList;
            }
        }


        /// <summary>
        /// Interfaces with the Taxxor Mapping Service
        /// </summary>
        public static class MappingService
        {
            /// <summary>
            /// Retrieve mapping information of a single fact ID
            /// </summary>
            /// <param name="factGuid"></param>
            /// <param name="projectId"></param>
            /// <param name="includeSdeValueInResponse"></param>
            /// <param name="mappingType"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveMappingInformation(string factGuid, string projectId, bool includeSdeValueInResponse, string mappingType = "", bool debugRoutine = false)
            {
                List<string> factGuids = [factGuid];

                var xmlResult = await RetrieveMappingInformation(factGuids, projectId, includeSdeValueInResponse, mappingType, debugRoutine);

                if (XmlContainsError(xmlResult))
                {
                    return xmlResult;
                }
                else
                {
                    var nodeMappingCluster = xmlResult.SelectSingleNode("//mappingCluster");
                    if (nodeMappingCluster == null)
                    {
                        return GenerateErrorXml("Could not find mapping information", $"Could not find mapping cluster node, xmlResult: {xmlResult.OuterXml}, stack-trace: {GetStackTrace()}");
                    }
                    else if (GetAttribute(nodeMappingCluster, "status")?.ToLower() == "notfound")
                    {
                        return GenerateErrorXml("Could not find mapping information", $"Could not find result in mapping service, xmlResult: {xmlResult.OuterXml}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        var responseXml = new XmlDocument();
                        responseXml.ReplaceContent(nodeMappingCluster);
                        return responseXml;
                    }
                }
            }

            /// <summary>
            /// Retrieves mapping information for a list of fact ID's
            /// </summary>
            /// <param name="factGuids"></param>
            /// <param name="projectId"></param>
            /// <param name="includeSdeValueInResponse"></param>
            /// <param name="mappingType"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveMappingInformation(List<string> factGuids, string projectId, bool includeSdeValueInResponse, string mappingType = "", bool debugRoutine = false)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                // Build the XML data that we need to POST to the service
                var xmlPost = new XmlDocument();
                xmlPost.AppendChild(xmlPost.CreateElement("request"));

                // Define the languages for which we want to receive values
                // var languages = RetrieveProjectLanguages(RetrieveEditorIdFromProjectId(projectId));
                // xmlPost.DocumentElement.SetAttribute("lang", string.Join(",", languages));

                SetAttribute(xmlPost.DocumentElement, "projectid", projectId);
                SetAttribute(xmlPost.DocumentElement, "includevalue", includeSdeValueInResponse.ToString().ToLower());
                foreach (string factGuid in factGuids)
                {
                    xmlPost.DocumentElement.AppendChild(xmlPost.CreateElementWithText("item", factGuid));
                }

                // Console.WriteLine("*************************************");
                // Console.WriteLine(PrettyPrintXml(xmlPost));
                // Console.WriteLine("*************************************");

                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Post, "FindMappingEntriesPost", xmlPost, debugRoutine);
            }

            /// <summary>
            /// Changes an existing mapping cluster
            /// </summary>
            /// <param name="projectId">Current project ID</param>
            /// <param name="factGuid">Fact ID to base the cloned cluster on</param>
            /// <param name="newPeriod">Optionally supply a new year for the cloned cluster</param>
            /// <param name="sourceReplacements">Optional set of search and replace strings for the source entry of the mapping cluster</param>
            /// <param name="targetReplacements">Optional set of search and replace strings for the target entries of the mapping cluster</param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> ChangeMappingCluster(string projectId, string factGuid, string? newPeriod = null, Dictionary<string, string>? sourceReplacements = null, Dictionary<string, string>? targetReplacements = null, bool debugRoutine = false)
            {
                return await _cloneOrEditMappingCluster(projectId, factGuid, newPeriod, null, sourceReplacements, targetReplacements, false, debugRoutine);
            }

            /// <summary>
            /// Clones a mapping cluster and returns the new fact ID
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="factGuid"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> CloneMappingCluster(string projectId, string factGuid, bool debugRoutine)
            {
                var factGuids = new List<string>
                {
                    factGuid
                };
                return await CloneMappingCluster(projectId, factGuids, debugRoutine);
            }

            /// <summary>
            /// Clones a mapping cluster and potentially adjusts the project ID or the period
            /// </summary>
            /// <param name="projectId">Current project ID</param>
            /// <param name="factGuid">Fact ID to base the cloned cluster on</param>
            /// <param name="newPeriod">Optionally supply a new year for the cloned cluster</param>
            /// <param name="newProjectId">Optionally store the new cluster under a new project ID</param>
            /// <param name="sourceReplacements">Optional set of search and replace strings for the source entry of the mapping cluster</param>
            /// <param name="targetReplacements">Optional set of search and replace strings for the target entries of the mapping cluster</param>
            /// 
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> CloneMappingCluster(string projectId, string factGuid, string? newPeriod = null, string? newProjectId = null, Dictionary<string, string>? sourceReplacements = null, Dictionary<string, string>? targetReplacements = null, bool debugRoutine = false)
            {
                return await _cloneOrEditMappingCluster(projectId, factGuid, newPeriod, newProjectId, sourceReplacements, targetReplacements, true, debugRoutine);
            }


            /// <summary>
            /// Clones a mapping cluster without changing anything
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="factGuids"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> CloneMappingCluster(string projectId, List<string> factGuids, bool debugRoutine = false)
            {
                var errorMessage = "There was a problem cloning the mapping cluster";

                //
                // => Construct the URL to post the request to
                //
                var uriStructuredDataStore = GetServiceUrl(ConnectedServiceEnum.StructuredDataStore);
                string url = $"{uriStructuredDataStore}/api/mapping/copy";
                var dataToPost = new List<KeyValuePair<string, string?>>
                {
                    new KeyValuePair<string, string?>("pid", projectId),
                    new KeyValuePair<string, string?>("includeValue", "true")
                };
                foreach (var factId in factGuids)
                {
                    dataToPost.Add(new KeyValuePair<string, string?>("factId", factId));
                }

                var languages = RetrieveProjectLanguages(RetrieveEditorIdFromProjectId(projectId));
                foreach (var lang in languages)
                {
                    dataToPost.Add(new KeyValuePair<string, string?>("locale", lang));
                }
                // dataToPost.Add("includeValue", "true");

                var apiUrl = QueryHelpers.AddQueryString(url, dataToPost);

                // Console.WriteLine(apiUrl);

                //
                // => Make the request
                //
                try
                {
                    using (HttpClient _httpClient = new HttpClient())
                    {
                        // Properties for the HTTP client
                        _httpClient.Timeout = TimeSpan.FromMinutes(1);
                        _httpClient.DefaultRequestHeaders.Add("X-Tx-UserId", SystemUser);
                        _httpClient.DefaultRequestHeaders.Accept.Clear();
                        _httpClient.DefaultRequestHeaders.Add("Accept", "text/xml");
                        // _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                        using (HttpResponseMessage result = await _httpClient.GetAsync(apiUrl))
                        {
                            if (result.IsSuccessStatusCode)
                            {
                                // Retrieve the XML response string
                                var responseString = await result.Content.ReadAsStringAsync();
                                /*
                                <response xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                                    <item>
                                        <orgFactId>573c77af-3348-430f-ae2e-fad292ca59bb</orgFactId>
                                        <factId>abfcfbc9-4e4f-474c-ad0b-e55e516e9965</factId>
                                        <value id="abfcfbc9-4e4f-474c-ad0b-e55e516e9965" result="Ok" datatype="monetary" displayoption="Rounded x.x billion without unit" time="0">
                                            <value>4.5</value>
                                            <localisedvalue locale="en">4.5</localisedvalue>
                                            <basevalue/>
                                        </value>
                                    </item>
                                </response>
                                */
                                if (debugRoutine)
                                {
                                    Console.WriteLine("------------- Clone mapping result --------------");
                                    Console.WriteLine(responseString);
                                    Console.WriteLine("-------------------------------------------------");
                                }


                                try
                                {
                                    var xmlResponse = new XmlDocument();
                                    xmlResponse.LoadXml(responseString);
                                    return xmlResponse;
                                }
                                catch (Exception ex)
                                {
                                    if (debugRoutine)
                                    {
                                        Console.WriteLine("------------- Clone mapping ERROR result --------------");
                                        Console.WriteLine(responseString);
                                        Console.WriteLine("-------------------------------------------------------");
                                    }
                                    appLogger.LogError(ex, errorMessage);

                                    return GenerateErrorXml($"There was a problem parsing the result of the clone mappings route into an XML Document.", responseString);
                                }
                            }
                            else
                            {
                                var errorContent = $"{result.ReasonPhrase}: ";
                                errorContent += await result.Content.ReadAsStringAsync();
                                var errorDebugInfo = $"url: {url}, HTTP Status Code: {result.StatusCode}, client-response: {errorContent}, incoming-request: {RenderHttpRequestDebugInformation()}";
                                appLogger.LogError($"{errorMessage}, {errorDebugInfo}");

                                return GenerateErrorXml(errorMessage, errorDebugInfo);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Unable to clone mapping cluster {apiUrl}");
                    return GenerateErrorXml(errorMessage, "");
                }
            }

            /// <summary>
            /// Clones a mapping cluster or update/edit an existing mapping cluster
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="factGuid"></param>
            /// <param name="newPeriod"></param>
            /// <param name="newProjectId"></param>
            /// <param name="sourceReplacements"></param>
            /// <param name="targetReplacements"></param>
            /// <param name="createNewMappingClusters"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            private static async Task<XmlDocument> _cloneOrEditMappingCluster(string projectId, string factGuid, string? newPeriod = null, string? newProjectId = null, Dictionary<string, string>? sourceReplacements = null, Dictionary<string, string>? targetReplacements = null, bool createNewMappingClusters = true, bool debugRoutine = false)
            {
                try
                {
                    //
                    // => Basic structure for a new (cloned) cluster
                    //
                    var xmlUpdatedClusterInformation = new XmlDocument();
                    xmlUpdatedClusterInformation.LoadXml("<mappingCluster/>");

                    xmlUpdatedClusterInformation.DocumentElement.SetAttribute("projectId", ((string.IsNullOrEmpty(newProjectId)) ? projectId : newProjectId));

                    //
                    // => Retrieve existing mapping cluster
                    //
                    var xmlMappingCluster = await RetrieveMappingInformation(factGuid, projectId, false);

                    if (debugRoutine)
                    {
                        Console.WriteLine("***************** ORIGINAL CLUSTER ********************");
                        Console.WriteLine(PrettyPrintXml(xmlMappingCluster));
                        Console.WriteLine("*************************************");
                    }

                    // - Exit the routine if we cannot retrieve the mapping cluster for this Structured Data Element
                    if (XmlContainsError(xmlMappingCluster))
                    {
                        return xmlMappingCluster;
                    }

                    // - Internal entry
                    var nodeInternalEntry = _getInternalEntry(xmlMappingCluster.DocumentElement, factGuid);
                    var nodeInternalEntryImported = xmlUpdatedClusterInformation.ImportNode(nodeInternalEntry, true);
                    xmlUpdatedClusterInformation.DocumentElement.AppendChild(nodeInternalEntryImported);
                    nodeInternalEntryImported.RemoveAttribute("id");
                    nodeInternalEntryImported.SetAttribute("scheme", "Internal");

                    // Remove the mapping of the internal entry
                    if (createNewMappingClusters)
                    {
                        var nodeInternalMapping = nodeInternalEntryImported.SelectSingleNode("mapping");
                        if (nodeInternalMapping != null)
                        {
                            RemoveXmlNode(nodeInternalMapping);
                        }
                    }


                    // Test if a datatype is present
                    // if (string.IsNullOrEmpty(nodeInternalEntryImported.GetAttribute("datatype")))
                    // {
                    //     nodeInternalEntryImported.SetAttribute("datatype", "monetary");
                    // }


                    // Update the period if a new period string was supplied
                    if (!string.IsNullOrEmpty(newPeriod))
                    {
                        var existingPeriod = nodeInternalEntryImported.GetAttribute("period");
                        var generatedPeriod = "";
                        var replacePattern = "";


                        if (!string.IsNullOrEmpty(existingPeriod))
                        {
                            if (newPeriod.Length == 4)
                            {
                                // Perform a rough replacement on the year
                                if (existingPeriod.Contains('_'))
                                {
                                    replacePattern = newPeriod + "$2" + newPeriod + "$4";

                                    Match match = Regex.Match(existingPeriod, @"^(20\d\d)(\d+_)(20\d\d)(.*)$");
                                    if (match.Success)
                                    {
                                        generatedPeriod = $"{newPeriod}{match.Groups[2].Value}{newPeriod}{match.Groups[4].Value}";
                                    }
                                }
                                else
                                {
                                    Match match = Regex.Match(existingPeriod, @"^(20\d\d)(.*)$");
                                    if (match.Success)
                                    {
                                        generatedPeriod = $"{newPeriod}{match.Groups[2].Value}";
                                    }
                                }
                            }
                            else
                            {
                                // Replace with the new period as provided in the parameter of this method
                                generatedPeriod = newPeriod;
                            }

                        }
                        if (!string.IsNullOrEmpty(generatedPeriod))
                        {
                            nodeInternalEntryImported.SetAttribute("period", generatedPeriod);
                        }
                    }

                    // - Source entry
                    var nodeSourceEntry = _getSourceEntry(xmlMappingCluster.DocumentElement);
                    if (nodeSourceEntry != null)
                    {
                        var nodeSourceEntryImported = xmlUpdatedClusterInformation.ImportNode(nodeSourceEntry, true);
                        nodeSourceEntryImported.RemoveAttribute("id");

                        // Potentially make adjustments in the source JSON string
                        if (sourceReplacements != null)
                        {
                            var sourceMappingJson = nodeSourceEntryImported.FirstChild.InnerText;
                            foreach (var replacementPair in sourceReplacements)
                            {
                                var searchString = replacementPair.Key;
                                var replacementString = replacementPair.Value;
                                sourceMappingJson = sourceMappingJson.Replace(searchString, replacementString);
                            }
                            nodeSourceEntryImported.FirstChild.InnerText = sourceMappingJson;
                        }

                        xmlUpdatedClusterInformation.DocumentElement.AppendChild(nodeSourceEntryImported);
                    }

                    // - Clone the target entries
                    var nodeListTargetMappings = xmlMappingCluster.DocumentElement.SelectNodes("entry[not(@scheme='Internal') and not(@scheme='Excel_Named_Range') and not(@scheme='EFR') and not(@scheme='')]");
                    foreach (XmlNode nodeTargetEntry in nodeListTargetMappings)
                    {
                        var nodeTargetEntryImported = xmlUpdatedClusterInformation.ImportNode(nodeTargetEntry, true);
                        nodeTargetEntryImported.RemoveAttribute("id");

                        // Potentially make adjustments in the target JSON string
                        if (targetReplacements != null)
                        {
                            var targetMappingJson = nodeTargetEntryImported.FirstChild.InnerText;
                            foreach (var replacementPair in targetReplacements)
                            {
                                var searchString = replacementPair.Key;
                                var replacementString = replacementPair.Value;
                                targetMappingJson = targetMappingJson.Replace(searchString, replacementString);
                            }
                            nodeTargetEntryImported.FirstChild.InnerText = targetMappingJson;
                        }
                        xmlUpdatedClusterInformation.DocumentElement.AppendChild(nodeTargetEntryImported);
                    }


                    if (debugRoutine)
                    {
                        Console.WriteLine("***************** NEW CLUSTER ********************");
                        Console.WriteLine(PrettyPrintXml(xmlUpdatedClusterInformation));
                        Console.WriteLine("*************************************");
                    }



                    if (createNewMappingClusters)
                    {
                        //
                        // => Create a new cluster using the XML we have just created
                        //
                        var xmlCreateResult = await CreateMappingEntry(xmlUpdatedClusterInformation);

                        if (debugRoutine)
                        {
                            Console.WriteLine("***************** CREATE RESULT ********************");
                            Console.WriteLine(PrettyPrintXml(xmlCreateResult));
                            Console.WriteLine("*************************************");

                        }

                        return xmlCreateResult;
                    }
                    else
                    {
                        //
                        // => Update the existing cluster using the XML we have just created
                        //
                        var xmlUpdateResult = await UpdateMappingEntry(xmlUpdatedClusterInformation);
                        // if (debugRoutine)
                        // {
                        //     Console.WriteLine("***************** UPDATE RESULT ********************");
                        //     Console.WriteLine(PrettyPrintXml(xmlUpdateResult));
                        //     Console.WriteLine("*************************************");

                        // }

                        return xmlUpdateResult;
                    }

                }
                catch (Exception ex)
                {
                    return GenerateErrorXml("There was an error cloning the mapping cluster", $"error: {ex}");
                }

            }

            /// <summary>
            /// Clones all the mapping clusters for a project to a new project ID
            /// </summary>
            /// <param name="sourceProjectId">Source project ID</param>
            /// <param name="targetProjectId">Target project ID (where the mappings will be imported to)</param>
            /// <param name="factGuids">List of SDE fact ID's that we need to import into the target project</param>
            /// <param name="overrideTargetMapping">'true' forces the process to push the source mappings to the target even if they already exist</param>
            /// <param name="debugRoutine">Log debug information on the console and save in-between results for inspection</param>
            /// <returns></returns>
            public static async Task<XmlDocument> CloneAllMappingClusters(string sourceProjectId, string targetProjectId, List<string> factGuids, bool overrideTargetMapping = false, bool debugRoutine = false)
            {
                try
                {

                    var xmlStructuredDataQuery = new XmlDocument();
                    xmlStructuredDataQuery.AppendChild(xmlStructuredDataQuery.CreateElement("request"));
                    foreach (var factId in factGuids)
                    {
                        xmlStructuredDataQuery.DocumentElement.AppendChild(xmlStructuredDataQuery.CreateElementWithText("item", factId));
                    }


                    // - Retrieve the URL of the mapping service (for some reason, the "proxy" route is not part of the service description)
                    var mappingServiceApiUri = $"{GetServiceUrl(ConnectedServiceEnum.MappingService)}/api/v2/projects/{sourceProjectId}/copy/{targetProjectId}";

                    // - Overwrite any existing mappings in the database or not
                    if (overrideTargetMapping) mappingServiceApiUri += "?overwrite=true";

                    // - By default we want to add the target XBRL mappings of the source in the clone as well
                    var xPath = $"/configuration/cms_projects/cms_project[@id='{sourceProjectId}']/reporting_requirements/reporting_requirement";
                    var nodeListTargetModels = xmlApplicationConfiguration.SelectNodes(xPath);
                    foreach (XmlNode nodeTargetModel in nodeListTargetModels)
                    {
                        var targetModel = nodeTargetModel.GetAttribute("ref-mappingservice");
                        if (!string.IsNullOrEmpty(targetModel))
                        {
                            mappingServiceApiUri += ((mappingServiceApiUri.Contains("?")) ? "&" : "?") + $"targetModel={targetModel}";
                        }
                    }


                    // - Call the mapping service to retrieve the Structured Data Element value
                    CustomHttpHeaders customHttpHeaders = new CustomHttpHeaders();
                    customHttpHeaders.AddTaxxorUserInformation(SystemUser);
                    customHttpHeaders.RequestType = ReturnTypeEnum.Xml;
                    customHttpHeaders.Accept = "text/xml";

                    if (UriLogEnabled)
                    {
                        if (!UriLogBackend.Contains(mappingServiceApiUri)) UriLogBackend.Add(mappingServiceApiUri);
                    }

                    if (debugRoutine)
                    {
                        appLogger.LogInformation("** Mapping Service Request for copy mappings **");
                        appLogger.LogInformation($"URL: {mappingServiceApiUri}");
                        appLogger.LogInformation($"Method: POST");
                        appLogger.LogInformation(TruncateString(PrettyPrintXml(xmlStructuredDataQuery), 1024));
                        appLogger.LogInformation("***********************************************");
                    }

                    var mappingServiceResult = await RestRequest<XmlDocument>(RequestMethodEnum.Post, mappingServiceApiUri, xmlStructuredDataQuery.OuterXml.ToString(), customHttpHeaders, 300000, debugRoutine);

                    if (debugRoutine)
                    {
                        appLogger.LogInformation("** Mapping Service Response for copy mappings **");
                        appLogger.LogInformation(mappingServiceResult.OuterXml);
                        appLogger.LogInformation("************************************************");
                    }


                    return mappingServiceResult;
                }
                catch (Exception ex)
                {
                    return GenerateErrorXml("There was an error of all mapping clusters", $"error: {ex}");
                }

            }

            /// <summary>
            /// Removes all the mapping clusters for a project
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> DeleteAllMappingClusters(string projectId, bool debugRoutine = false)
            {
                try
                {
                    // - Retrieve the URL of the mapping service (for some reason, the "proxy" route is not part of the service description)
                    var mappingServiceApiUri = $"{GetServiceUrl(ConnectedServiceEnum.MappingService)}/api/v2/projects/{projectId}";

                    // - Call the mapping service to retrieve the Structured Data Element value
                    CustomHttpHeaders customHttpHeaders = new CustomHttpHeaders();
                    customHttpHeaders.AddTaxxorUserInformation(SystemUser);
                    customHttpHeaders.RequestType = ReturnTypeEnum.Xml;
                    customHttpHeaders.Accept = "text/xml";

                    if (UriLogEnabled)
                    {
                        if (!UriLogBackend.Contains(mappingServiceApiUri)) UriLogBackend.Add(mappingServiceApiUri);
                    }

                    var mappingServiceResult = await RestRequest<string>(RequestMethodEnum.Delete, mappingServiceApiUri, customHttpHeaders, debugRoutine);
                    if (mappingServiceResult.StartsWith("ERROR:"))
                    {
                        return GenerateErrorXml("There was an error deleting the mapping cluster(s)", mappingServiceResult);
                    }

                    var xmlMappingServiceResult = new XmlDocument();
                    xmlMappingServiceResult.LoadXml("<result/>");
                    xmlMappingServiceResult.DocumentElement.InnerText = mappingServiceResult;
                    return xmlMappingServiceResult;
                }
                catch (Exception ex)
                {
                    return GenerateErrorXml("There was an error deleting the mapping cluster", $"error: {ex}");
                }
            }


            public static async Task<XmlDocument> RetrieveMappingInformationV2(List<string> factGuids, string projectId, string mappingType = "", bool debugRoutine = false)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                // Build the XML data that we need to POST to the service
                var xmlPost = new XmlDocument();
                xmlPost.LoadXml($"<request />");
                SetAttribute(xmlPost.DocumentElement, "projectid", projectId);
                foreach (string factGuid in factGuids)
                {
                    var nodeItem = xmlPost.CreateElement("item");
                    nodeItem.InnerText = factGuid;
                    xmlPost.DocumentElement.AppendChild(nodeItem);
                }

                // Console.WriteLine("*************************************");
                // Console.WriteLine(PrettyPrintXml(xmlPost));
                // Console.WriteLine("*************************************");


                // Call the service and retrieve the result as XML
                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Post, "post_api_v2_project_projectId_mappingentries", xmlPost, debugRoutine);
            }

            /// <summary>
            /// Stores or updates a new or existing mapping cluster on the server
            /// </summary>
            /// <param name="xmlMappingClusterInformation"></param>
            /// <param name="onlyChangeDate"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> StoreMappingEntry(XmlDocument xmlMappingClusterInformation, bool onlyChangeDate = false)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var existingFact = false;
                if (!string.IsNullOrEmpty(GetAttribute(xmlMappingClusterInformation.DocumentElement, "requestId"))) existingFact = true;

                var xmlResponse = new XmlDocument();
                if (existingFact)
                {
                    appLogger.LogInformation($"Update the fact information in the Taxxor Mapping Service");
                    xmlResponse = await UpdateMappingEntry(xmlMappingClusterInformation, onlyChangeDate);
                }
                else
                {
                    appLogger.LogInformation($"Generate a new fact in the Taxxor Mapping Service");
                    xmlResponse = await CreateMappingEntry(xmlMappingClusterInformation);
                }

                // Console.WriteLine("*************************************");
                // Console.WriteLine(PrettyPrintXml(xmlResponse));
                // Console.WriteLine("*************************************");

                return xmlResponse;
            }

            /// <summary>
            /// Creates a new mapping cluster based on the information stored in the XmlDocument
            /// </summary>
            /// <param name="xmlMappingClusterInformation"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> CreateMappingEntry(XmlDocument xmlMappingClusterInformation)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                // Call the service and retrieve the result as XML
                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Post, "CreateMapping", xmlMappingClusterInformation, debugRoutine);
            }

            /// <summary>
            /// Updates an existing mapping cluster based on the information stored in the XmlDocument
            /// </summary>
            /// <param name="xmlMappingClusterInformation"></param>
            /// <param name="onlyChangeDate"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> UpdateMappingEntry(XmlDocument xmlMappingClusterInformation, bool onlyChangeDate = false)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                xmlMappingClusterInformation.DocumentElement.SetAttribute("dateonly", onlyChangeDate.ToString().ToLower());

                // if (debugRoutine)
                // {
                //     Console.WriteLine("******************* UPDATE MAPPING ENTRY ******************");
                //     Console.WriteLine(PrettyPrintXml(xmlMappingClusterInformation));
                //     Console.WriteLine("************************************************************");
                // }


                // Call the service and retrieve the result as XML
                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Put, "UpdateMapping", xmlMappingClusterInformation, debugRoutine);
            }


            /// <summary>
            /// Filters a list of mapping ID's based on the parameters passed
            /// </summary>
            /// <param name="factGuids"></param>
            /// <param name="projectId"></param>
            /// <param name="flagged"></param>
            /// <param name="hasComment"></param>
            /// <param name="period"></param>
            /// <param name="scheme"></param>
            /// <param name="status"></param>
            /// <param name="mapping"></param>
            /// <returns></returns>
            public static async Task<List<string>> FilterMappingEntries(List<string> factGuids, string projectId, bool? flagged = null, bool? hasComment = null, string period = null, string scheme = null, string status = null, string mapping = null)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                var filterQueryString = "";
                var queryElements = new List<string>();
                if (flagged != null) queryElements.Add($"flagged={flagged.ToString().ToLower()}");
                if (hasComment != null) queryElements.Add($"hascomment={hasComment.ToString().ToLower()}");
                if (period != null) queryElements.Add($"period={period}");
                if (scheme != null) queryElements.Add($"scheme={scheme}");
                if (status != null) queryElements.Add($"status={status}");
                if (mapping != null) queryElements.Add($"mapping={mapping}");

                // Render the querystring for the request to the mapping service
                if (queryElements.Count == 0)
                {
                    filterQueryString = $"?project={projectId}&includeValue=false";
                }
                else
                {
                    filterQueryString = $"?{string.Join("&", queryElements)}&project={projectId}&includeValue=false";
                }

                // Build the XML data that we need to POST to the service
                var xmlPost = new XmlDocument();
                xmlPost.LoadXml($"<request />");
                SetAttribute(xmlPost.DocumentElement, "projectid", projectId);
                SetAttribute(xmlPost.DocumentElement, "query", filterQueryString);
                SetAttribute(xmlPost.DocumentElement, "includeValue", "false");
                foreach (string factGuid in factGuids)
                {
                    var nodeItem = xmlPost.CreateElement("item");
                    nodeItem.InnerText = factGuid;
                    xmlPost.DocumentElement.AppendChild(nodeItem);
                }

                // Console.WriteLine("*************************************");
                // Console.WriteLine(PrettyPrintXml(xmlPost));
                // Console.WriteLine("*************************************");


                // Call the service and retrieve the result as XML
                var xmlResult = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Post, "QueryMappingEntriesPost", xmlPost, debugRoutine);

                // Console.WriteLine("*************************************");
                // Console.WriteLine(PrettyPrintXml(xmlResult));
                // Console.WriteLine("*************************************");

                var filteredGuids = new List<string>();

                var nodeListEntries = xmlResult.SelectNodes("/mappingEntries/mappingEntry/mapping");
                foreach (XmlNode nodeEntry in nodeListEntries)
                {
                    filteredGuids.Add(nodeEntry.InnerText);
                }

                return filteredGuids;
            }


            /// <summary>
            /// Retrieves possible values for the mapping entry (workflow) status
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="mappingType"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveMappingStatusList(string projectId, string mappingType = "", bool debugRoutine = false)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId }
                };

                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Get, "ListMappingStatus", dataToPost, debugRoutine);
            }


            /// <summary>
            /// Updates the (workflow) status of a mapping entry
            /// </summary>
            /// <param name="mappingClusterId"></param>
            /// <param name="mappingEntryId"></param>
            /// <param name="mappingEntryStatus"></param>
            /// <param name="projectId"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> UpdateMappingEntryStatus(string mappingClusterId, string mappingEntryId, string mappingEntryStatus, string projectId, bool debugRoutine = false)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                // Build the XML data that we need to POST to the service
                var xmlPost = new XmlDocument();
                xmlPost.LoadXml($"<statusUpdate />");
                var nodeProjectId = xmlPost.CreateElement("projectId");
                xmlPost.DocumentElement.AppendChild(nodeProjectId);
                nodeProjectId.InnerText = projectId;
                var nodeClusterId = xmlPost.CreateElement("clusterId");
                xmlPost.DocumentElement.AppendChild(nodeClusterId);
                nodeClusterId.InnerText = mappingClusterId;
                var nodeEntryId = xmlPost.CreateElement("entryId");
                xmlPost.DocumentElement.AppendChild(nodeEntryId);
                nodeEntryId.InnerText = mappingEntryId;
                var nodeStatusId = xmlPost.CreateElement("statusId");
                nodeStatusId.InnerText = mappingEntryStatus;
                xmlPost.DocumentElement.AppendChild(nodeStatusId);


                // Console.WriteLine("*************************************");
                // Console.WriteLine(PrettyPrintXml(xmlPost));
                // Console.WriteLine("*************************************");


                // Call the service and retrieve the result as XML
                var commentStoreResult = await CallTaxxorConnectedService<string>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Put, "SetMappingStatus", xmlPost, debugRoutine);

                if (commentStoreResult.ToLower().StartsWith("error:"))
                {
                    return GenerateErrorXml("There was an error storing the mapping entry status", $"projectId: {projectId}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    return GenerateSuccessXml("Successfully stored mapping entry status", $"mappingEntryStatus: {mappingEntryStatus}");
                }
            }


            /// <summary>
            /// Parses text data into elements used in XBRL (dataType, currency, etc.)
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="textToParse"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> ParseTextData(string projectId, string textToParse, string datatype, string unittype, bool flipSign = false, bool debugRoutine = false)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                // Build the XML data that we need to POST to the service
                var xmlPost = new XmlDocument();
                xmlPost.LoadXml($"<string />");
                xmlPost.DocumentElement.InnerText = textToParse;

                // Add additional parameters as attributes to the XML node so that we can use it later on and pass the information through
                SetAttribute(xmlPost.DocumentElement, "projectid", projectId);
                SetAttribute(xmlPost.DocumentElement, "datatype", datatype);
                SetAttribute(xmlPost.DocumentElement, "unit", unittype);
                SetAttribute(xmlPost.DocumentElement, "flipsign", flipSign.ToString().ToLower());



                // Console.WriteLine("*************************************");
                // Console.WriteLine(PrettyPrintXml(xmlPost));
                // Console.WriteLine("*************************************");


                // Call the service and retrieve the result as XML
                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Post, "ParseData", xmlPost, debugRoutine);
            }


            /// <summary>
            /// Retrieves base types used in the XBRL taxonomy from the Taxxor Mapping Service
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="periodType"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveBaseDataTypes(string projectId, bool debugRoutine = false)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId }
                };

                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Get, "GetBaseTypes", dataToPost, debugRoutine);
            }

            /// <summary>
            /// Retrieves a list of currencies that can be used in the XBRL filing
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="debugRoutine"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> RetrieveCurrencies(string projectId, bool debugRoutine = false)
            {
                debugRoutine = (siteType == "local" || siteType == "dev");

                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectId }
                };

                return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Get, "GetCurrencies", dataToPost, debugRoutine);
            }

            /// <summary>
            /// Filters an XBRL taxonomy
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="taxonomyIdentifier"></param>
            /// <param name="periodFilter"></param>
            /// <param name="baseDataTypeFilter"></param>
            /// <param name="isAbstractFilter"></param>
            /// <param name="isDimensionFilter"></param>
            /// <param name="isMemberFilter"></param>
            /// <param name="isAxisFilter"></param>
            /// <param name="textFilter"></param>
            /// <returns></returns>
            public static async Task<XmlDocument> FilterTaxonomy(string projectId, string taxonomyIdentifier, string? periodFilter = null, string? baseDataTypeFilter = null, bool? isAbstractFilter = null, bool? isDimensionFilter = null, bool? isMemberFilter = null, bool? isAxisFilter = null, string? textFilter = null)
            {
                var debugRoutine = (siteType == "local" || siteType == "dev");

                // Construct the data we want to post to the remote service
                var dataToPost = new Dictionary<string, string>
                {
                    { "projectid", projectId },
                    { "taxonomyid", taxonomyIdentifier }
                };
                if (periodFilter != null) dataToPost.Add("period", periodFilter);
                if (baseDataTypeFilter != null) dataToPost.Add("basetype", baseDataTypeFilter);
                if (isAbstractFilter != null) dataToPost.Add("isabstract", isAbstractFilter.ToString().ToLower());
                if (isDimensionFilter != null) dataToPost.Add("isdimension", isDimensionFilter.ToString().ToLower());
                if (isMemberFilter != null) dataToPost.Add("ismember", isMemberFilter.ToString().ToLower());
                if (isAxisFilter != null) dataToPost.Add("isaxis", isAxisFilter.ToString().ToLower());
                if (textFilter != null) dataToPost.Add("filterText", textFilter);


                // Call the service and retrieve the result as XML
                var xmlResult = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.MappingService, RequestMethodEnum.Get, "QueryHierarchical", dataToPost, debugRoutine);

                // Console.WriteLine("*************************************");
                // Console.WriteLine(PrettyPrintXml(xmlResult));
                // Console.WriteLine("*************************************");

                return xmlResult;
            }


        }

    }
}

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Represents an RBAC Access Record
        /// </summary>
        public class RbacAccessRecord
        {
            // Generic properties for the user class
            public string? groupref = null;
            public string? userref = null;
            public string? roleref;
            public string? resourceref;
            public bool enabled = true;
            public bool resetinheritance = false;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="userGroupId"></param>
            /// <param name="roleId"></param>
            /// <param name="resourceId"></param>
            /// <param name="enabled"></param>
            public RbacAccessRecord(string userGroupId, string roleId, string resourceId, bool enabled = true)
            {
                if (userGroupId.Contains('@'))
                {
                    this.userref = userGroupId;
                }
                else
                {
                    this.groupref = userGroupId;
                }

                this.roleref = roleId;
                this.resourceref = resourceId;
                this.enabled = enabled;
            }

            /// <summary>
            /// Constructor accepting a access record XML node
            /// </summary>
            /// <param name="nodeAccessRecord"></param>
            public RbacAccessRecord(XmlNode nodeAccessRecord)
            {
                this.userref = nodeAccessRecord.SelectSingleNode("userRef")?.GetAttribute("ref");
                this.groupref = (this.userref == null) ? nodeAccessRecord.SelectSingleNode("groupRef")?.GetAttribute("ref") : null;
                var disabledString = nodeAccessRecord.GetAttribute("disabled") ?? "false";
                this.enabled = (disabledString == "false");
                this.roleref = nodeAccessRecord.SelectSingleNode("roleRef")?.GetAttribute("ref");
                this.resourceref = nodeAccessRecord.SelectSingleNode("resourceRef")?.GetAttribute("ref");
            }

            /// <summary>
            /// Updates the hierarchy ID part of the access record resource reference to a new value
            /// </summary>
            /// <param name="newHierarchyItemId"></param>
            public void ChangeHierarchyItemId(string newHierarchyItemId)
            {

                Match match = Regex.Match(this.resourceref, @"^([a-zA-Z]+__[a-zA-Z0-9]+__)(.*?)(__.*)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    this.resourceref = match.Groups[1].Value + newHierarchyItemId + match.Groups[3].Value;
                }
                else
                {
                    appLogger.LogError($"Could not change the hierarchy ID because {newHierarchyItemId} did not match in the regular expression");
                }
            }

            /// <summary>
            /// Exports the RBAC access record as XML
            /// </summary>
            /// <returns></returns>
            public XmlDocument Export()
            {
                // Basic xml structure to use
                var xmlRbac = new XmlDocument();
                var nodeAccessRecord = xmlRbac.CreateElement("accessRecord");
                nodeAccessRecord.AppendChild(xmlRbac.CreateElement("roleRef"));
                nodeAccessRecord.AppendChild(xmlRbac.CreateElement("resourceRef"));
                xmlRbac.AppendChild(nodeAccessRecord);

                // Add a user or group node
                if (string.IsNullOrEmpty(this.groupref))
                {
                    var nodeUserRef = xmlRbac.CreateElement("userRef");
                    SetAttribute(nodeUserRef, "ref", this.userref);
                    xmlRbac.DocumentElement.PrependChild(nodeUserRef);
                }
                else
                {
                    var nodeGroupRef = xmlRbac.CreateElement("groupRef");
                    SetAttribute(nodeGroupRef, "ref", this.groupref);
                    xmlRbac.DocumentElement.PrependChild(nodeGroupRef);
                }

                // Add the remaining attributes
                if (!enabled)
                {
                    SetAttribute(xmlRbac.DocumentElement, "disabled", (!enabled) ? "true" : "false");
                }
                SetAttribute(xmlRbac.SelectSingleNode("/accessRecord/roleRef"), "ref", this.roleref);
                SetAttribute(xmlRbac.SelectSingleNode("/accessRecord/resourceRef"), "ref", this.resourceref);

                return xmlRbac;
            }
        }
    }
}