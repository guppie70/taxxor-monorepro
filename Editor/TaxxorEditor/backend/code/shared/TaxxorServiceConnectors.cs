using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Helper routines for interacting with the other Taxxor Services in this application
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Convenience method to make a web API request to the Taxxor Document Store without sending any data
        /// </summary>
        /// <returns>The taxxor data service.</returns>
        /// <param name="method">Method.</param>
        /// <param name="methodId">Method identifier.</param>
        /// <param name="debug">If set to <c>true</c> debug.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public async static Task<T> CallTaxxorDataService<T>(RequestMethodEnum method, string methodId, bool debug = false)
        {
            return await CallTaxxorConnectedService<T>(ConnectedServiceEnum.DocumentStore, method, methodId, debug);
        }

        /// <summary>
        /// Convenience method to make a web API request to the Taxxor Document Store
        /// </summary>
        /// <returns>The taxxor data service.</returns>
        /// <param name="method">Method.</param>
        /// <param name="methodId">Method identifier.</param>
        /// <param name="requestData">Request data.</param>
        /// <param name="debug">If set to <c>true</c> debug.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public async static Task<T> CallTaxxorDataService<T>(RequestMethodEnum method, string methodId, dynamic requestData, bool debug = false)
        {
            return await CallTaxxorConnectedService<T>(ConnectedServiceEnum.DocumentStore, method, methodId, requestData, debug);
        }

        public enum HttpClientEnum
        {
            Standard,
            EfficientPredifinedHttp1,
            EfficientPredefinedHttp2,
            EfficientCustomizableHttp1,
            EfficientCustomizableHttp2
        }

        /// <summary>
        /// Generic routine to make a web API call to a connected Taxxor webservice without sending any data
        /// </summary>
        /// <returns>The taxxor connected service.</returns>
        /// <param name="connectedService">Connected service.</param>
        /// <param name="method">Method.</param>
        /// <param name="methodId">Method identifier.</param>
        /// <param name="debug">If set to <c>true</c> debug.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public async static Task<T> CallTaxxorConnectedService<T>(ConnectedServiceEnum connectedService, RequestMethodEnum method, string methodId, bool debug = false)
        {
            // Convert the Dictionary to a List containing KeyValuePair objects
            var requestKeyValueData = new List<KeyValuePair<string, string>>();
            var requestEncodedData = new FormUrlEncodedContent(requestKeyValueData);

            return await CallTaxxorConnectedService<T>(connectedService, method, methodId, requestEncodedData, debug);
        }

        /// <summary>
        /// Generic routine to make a web API call to a connected Taxxor webservice
        /// </summary>
        /// <returns>The taxxor connected service.</returns>
        /// <param name="connectedService">Connected service.</param>
        /// <param name="method">Method.</param>
        /// <param name="methodId">Method identifier.</param>
        /// <param name="requestData">Request data.</param>
        /// <param name="debug">If set to <c>true</c> debug.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public async static Task<T> CallTaxxorConnectedService<T>(ConnectedServiceEnum connectedService, RequestMethodEnum method, string methodId, dynamic requestData, bool debug = false, bool suppressResponseExceptionLogging = false, ProjectVariables? projectVars = null)
        {
            // Global variables
            var dumpRequestDetails = (siteType == "local" || siteType == "dev");



            // Retrieve project variables
            var context = System.Web.Context.Current;
            if (projectVars == null)
            {
                projectVars = new ProjectVariables();
                try
                {
                    projectVars = RetrieveProjectVariables(context, (
                        methodId != "calculateHash" &&
                        methodId != "dataconfiguration" &&
                        methodId != "taxxoreditorfilingmetadata" &&
                        methodId != "dataconfiguration" &&
                        methodId != "taxxoreditorcomposerdata" &&
                        methodId != "systemstate"
                    ));
                }
                catch (Exception) { }
            }
            // else
            // {
            //     Console.WriteLine("######################################");
            //     Console.WriteLine("projectVars not null");
            //     Console.WriteLine(projectVars.DumpToString());
            //     Console.WriteLine("######################################");
            // }

            var returnType = GenericTypeToNormalizedString(typeof(T).ToString());
            string requestDataObjectType = requestData.GetType().ToString();


            //
            // => Determine the type of Http Client to use for the request
            //
            var httpClientVersion = HttpClientEnum.Standard;
            switch (connectedService)
            {
                case ConnectedServiceEnum.DocumentStore:
                    httpClientVersion = HttpClientEnum.EfficientCustomizableHttp2;
                    break;

                case ConnectedServiceEnum.AccessControlService:
                    httpClientVersion = HttpClientEnum.EfficientCustomizableHttp1;
                    break;

                case ConnectedServiceEnum.PdfService:
                    httpClientVersion = HttpClientEnum.EfficientCustomizableHttp1;
                    break;

            }


            try
            {
                // Variables used for interacting with the Taxxor Document Store
                var apiUrl = methodId.StartsWith("http") ? methodId : GetServiceUrlByMethodId(connectedService, methodId);

                if (string.IsNullOrEmpty(apiUrl) && methodId == "dataconfiguration")
                {
                    // As a fallback, we can use the URL that is defined in the configuration system
                    apiUrl = CalculateFullPathOs("/configuration/configuration_system/config//location[@id='data_configuration']");
                }

                // Optionally log the URL we are using
                if (UriLogEnabled)
                {
                    if (!UriLogBackend.Contains(apiUrl)) UriLogBackend.Add(apiUrl);
                }

                // Force errors
                // if (methodId == "effectivePermissionsResources" || methodId == "calculateHash") apiUrl += "abcd";

                // Make sure that we have retrieved a URL that we can query
                if (string.IsNullOrEmpty(apiUrl))
                {

                    var errorMessage = $"Web request not possible without a uri";
                    var debugInfo = $"connectedService: {ConnectedServicesEnumToString(connectedService)}, methodId: {methodId}, stack-trace: {GetStackTrace()}";
                    appLogger.LogError($"{errorMessage} {debugInfo}");

                    switch (returnType)
                    {
                        case "xml":

                            return (T)Convert.ChangeType(GenerateErrorXml(errorMessage, debugInfo), typeof(T));

                        default:

                            var returnValue = "ERROR:<br/><b>" + errorMessage + "</b>";
                            if (siteType != "prod")
                            {
                                returnValue += "<p>Debug info:<br/>" + debugInfo + "</p>";
                            }

                            //default to string
                            return (T)Convert.ChangeType(returnValue, typeof(T));
                    }
                }

                /*
                Set custom request headers
                */
                CustomHttpHeaders customHttpHeaders = new CustomHttpHeaders();
                switch (connectedService)
                {
                    case ConnectedServiceEnum.Editor:
                        if (string.IsNullOrEmpty(projectVars.currentUser.Id) && methodId == "systemstate")
                        {
                            customHttpHeaders.AddCustomHeader("X-Tx-UserId", SystemUser);
                        }
                        else
                        {
                            customHttpHeaders.AddTaxxorUserInformation(projectVars.currentUser);
                        }
                        break;


                    case ConnectedServiceEnum.DocumentStore:
                        if (string.IsNullOrEmpty(projectVars.currentUser.Id) && (methodId == "systemstate" || methodId == "taxxoreditorcomposerdata"))
                        {
                            customHttpHeaders.AddCustomHeader("X-Tx-UserId", SystemUser);
                        }
                        else
                        {
                            customHttpHeaders.AddTaxxorUserInformation(projectVars.currentUser);
                        }
                        break;


                    default:
                        customHttpHeaders.AddTaxxorUserInformation(projectVars.currentUser);
                        break;
                }
                customHttpHeaders.RequestType = ReturnTypeEnum.Xml;
                if (returnType == "string") customHttpHeaders.RequestType = ReturnTypeEnum.Txt;

                /*
                Replace placeholders in URL's to the webservices
                 */
                if (connectedService == ConnectedServiceEnum.AccessControlService)
                {
                    // Replace potential placeholders in the content with actual values
                    if (methodId == "effectivePermissionsResources")
                    {
                        var nodeRoot = (XmlNode)requestData.DocumentElement;
                        var userIdFromRootNode = nodeRoot.GetAttribute("userId");
                        if (!string.IsNullOrEmpty(userIdFromRootNode))
                        {
                            nodeRoot.RemoveAttribute("userId");
                            apiUrl = apiUrl.Replace("{userId}", userIdFromRootNode);
                        }
                    }
                    else
                    {
                        apiUrl = apiUrl.Replace("{userId}", projectVars.currentUser.Id);
                    }

                    if (methodId == "get_api_users_id_groups")
                    {
                        apiUrl = apiUrl.Replace("{id}", requestData["userid"]);
                    }

                    try
                    {
                        if (apiUrl.Contains("{resourceId}") && requestData.ContainsKey("resourcebreadcrumbids"))
                        {
                            apiUrl = apiUrl.Replace("{resourceId}", requestData["resourcebreadcrumbids"]);
                        }

                        if (apiUrl.Contains("{id}") && requestData.ContainsKey("resourceid"))
                        {
                            apiUrl = apiUrl.Replace("{id}", requestData["resourceid"]);
                        }
                        if (apiUrl.Contains("{id}") && requestData.ContainsKey("groupid"))
                        {
                            apiUrl = apiUrl.Replace("{id}", requestData["groupid"]);
                        }

                        if (apiUrl.Contains("{resourceId}") && requestData.ContainsKey("resourceid"))
                        {
                            apiUrl = apiUrl.Replace("{resourceId}", requestData["resourceid"]);
                        }

                        if (apiUrl.Contains("{userGroupId}") && requestData.ContainsKey("usergroupid"))
                        {
                            apiUrl = apiUrl.Replace("{userGroupId}", requestData["usergroupid"]);
                        }

                        if (apiUrl.Contains("{roleId}") && requestData.ContainsKey("roleid"))
                        {
                            apiUrl = apiUrl.Replace("{roleId}", requestData["roleid"]);
                        }

                        if (apiUrl.Contains("{idExisting}") && requestData.ContainsKey("sourceresourceid"))
                        {
                            apiUrl = apiUrl.Replace("{idExisting}", requestData["sourceresourceid"]);
                        }

                        if (apiUrl.Contains("{idNew}") && requestData.ContainsKey("targetresourceid"))
                        {
                            apiUrl = apiUrl.Replace("{idNew}", requestData["targetresourceid"]);
                        }

                        // Set the accept custom header
                        if (methodId == "resourcesUpdate")
                        {
                            customHttpHeaders.RequestType = ReturnTypeEnum.Xml;
                        }

                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError($"There was an error creating replacing placeholder values for {ConnectedServicesEnumToString(connectedService)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }

                if (connectedService == ConnectedServiceEnum.GenericDataConnectorService)
                {
                    try
                    {
                        if (methodId == "downloadId")
                        {
                            if (apiUrl.Contains("{id}") && requestData.ContainsKey("tableid"))
                            {
                                apiUrl = apiUrl.Replace("{id}", requestData["tableid"]);
                            }

                            if (apiUrl.Contains("{transform?}"))
                            {
                                if (requestData.ContainsKey("transform"))
                                {
                                    apiUrl = apiUrl.Replace("{transform?}", requestData["transform"]);
                                }
                                else
                                {
                                    apiUrl = apiUrl.Replace("{transform?}", "");
                                }
                            }
                        }

                        if (methodId == "downloadTable" || methodId == "get_api_table_workbookId_sheetHash_preview" || methodId == "get_api_table_workbookId_sheetHash_insert_projectId" || methodId == "get_api_table_id_update")
                        {
                            if (apiUrl.Contains("{projectId}") && requestData.ContainsKey("pid"))
                            {
                                apiUrl = apiUrl.Replace("{projectId}", requestData["pid"]);
                            }

                            if (apiUrl.Contains("{sheetHash}") && requestData.ContainsKey("tableid"))
                            {
                                apiUrl = apiUrl.Replace("{sheetHash}", requestData["tableid"]);
                            }

                            if (apiUrl.Contains("{workbookId}"))
                            {
                                if (requestData.ContainsKey("workbookid"))
                                {
                                    apiUrl = apiUrl.Replace("{workbookId}", requestData["workbookid"]);
                                }
                                else
                                {
                                    apiUrl = apiUrl.Replace("{workbookId}", "");
                                }
                            }

                            if (apiUrl.Contains("{id}"))
                            {
                                if (requestData.ContainsKey("workbookid"))
                                {
                                    apiUrl = apiUrl.Replace("{id}", requestData["workbookid"]);
                                }
                                else
                                {
                                    apiUrl = apiUrl.Replace("{id}", "");
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"There was an error creating replacing placeholder values for {ConnectedServicesEnumToString(connectedService)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }


                if (connectedService == ConnectedServiceEnum.MappingService)
                {
                    if (!string.IsNullOrEmpty(SystemUser))
                    {
                        customHttpHeaders.AddTaxxorUserInformation(SystemUser);
                    }

                    // Correct the headers that we will be sending to the Taxxor Mapping Service
                    if (requestDataObjectType.Contains("XmlDocument"))
                    {
                        customHttpHeaders.RequestType = ReturnTypeEnum.Xml;
                    }

                    try
                    {
                        // Inject fact guids


                        if (methodId == "FindMappingEntry")
                        {
                            if (apiUrl.Contains("{type?}"))
                            {
                                if (requestData.ContainsKey("type"))
                                {
                                    apiUrl = apiUrl.Replace("{type?}", requestData["type"]);
                                }
                                else
                                {
                                    apiUrl = apiUrl.Replace("{type?}", "");
                                }
                            }
                        }

                        if (methodId == "ParseData")
                        {
                            // Append the flipsign data as a querystring to the URL
                            var nodeRoot = (XmlNode)requestData.DocumentElement;
                            var flipSign = GetAttribute(nodeRoot, "flipsign");
                            apiUrl += $"?flipsign={flipSign}";
                        }

                        // Append a query to the URL
                        if (methodId == "QueryMappingEntriesPost")
                        {
                            var nodeRoot = (XmlNode)requestData.DocumentElement;
                            var mappingEntriesQuery = GetAttribute(nodeRoot, "query");
                            apiUrl += mappingEntriesQuery;
                        }

                        if (methodId == "FindMappingEntriesPost")
                        {
                            var nodeRoot = (XmlNode)requestData.DocumentElement;
                            var projectId = GetAttribute(nodeRoot, "projectid");
                            var includeValue = GetAttribute(nodeRoot, "includevalue");
                            apiUrl += $"?project={projectId}&includeValue={includeValue}";
                        }

                        if (methodId == "UpdateMapping")
                        {
                            var nodeRoot = (XmlNode)requestData.DocumentElement;
                            var dateOnly = GetAttribute(nodeRoot, "dateonly");
                            apiUrl += $"?dateOnly={dateOnly}";
                        }

                    }
                    catch (Exception ex)
                    {

                        appLogger.LogError(ex, $"There was an error creating replacing placeholder values for {ConnectedServicesEnumToString(connectedService)}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }

                // Timeout for remote requests
                int remoteServiceTimeout = 3000;
                switch (connectedService)
                {
                    case ConnectedServiceEnum.DocumentStore:
                        switch (methodId)
                        {
                            case "cloneproject":
                                remoteServiceTimeout = 180000;
                                break;
                            case "FindMappingEntriesPost":
                                remoteServiceTimeout = 720000;
                                break;
                            case "taxxoreditorsyncstructureddatasnapshotperdataref":
                                remoteServiceTimeout = 1400000;
                                break;
                            case "taxxoreditordataservicetools":
                                remoteServiceTimeout = 300000;
                                break;
                            case "taxxoreditorimagerenditions":
                                remoteServiceTimeout = 300000;
                                break;
                            default:
                                remoteServiceTimeout = 60000;
                                break;
                        }
                        break;

                    case ConnectedServiceEnum.MappingService:
                    case ConnectedServiceEnum.AssetCompiler:
                        remoteServiceTimeout = 60000;
                        break;

                    case ConnectedServiceEnum.PdfService:
                    case ConnectedServiceEnum.ConversionService:
                    case ConnectedServiceEnum.XbrlService:
                        remoteServiceTimeout = 300000;
                        break;

                    case ConnectedServiceEnum.GenericDataConnectorService:
                        switch (methodId)
                        {
                            case "get_api_table_workbookId_sheetHash_insert_projectId":
                                remoteServiceTimeout = 180000;
                                break;
                            default:
                                remoteServiceTimeout = 5000;
                                break;
                        }
                        break;

                    case ConnectedServiceEnum.AccessControlService:
                        remoteServiceTimeout = 2000;
                        break;
                }


                // Dump information about the request
                if (dumpRequestDetails && connectedService == ConnectedServiceEnum.AccessControlService && methodId == "effectivePermissionsResourcesXYZ")
                {
                    RequestVariables reqVars = new RequestVariables();
                    try
                    {
                        reqVars = RetrieveRequestVariables(context);
                    }
                    catch (Exception) { }


                    Console.WriteLine("");
                    Console.WriteLine($"------- CallTaxxorConnectedService() Details -------");
                    Console.WriteLine($"- method: {method}");
                    Console.WriteLine($"- apiUrl: {apiUrl}");
                    var requestDataDump = "";
                    switch (requestData.ToString())
                    {
                        case "System.Xml.XmlDocument":
                        case "XmlDocument":
                            var xmlDataForRequest = (XmlDocument)requestData;
                            requestDataDump = "\n" + PrettyPrintXml(xmlDataForRequest);
                            break;
                        default:
                            requestDataDump = requestData.ToString();
                            break;
                    }
                    requestDataDump = TruncateString(requestDataDump, 500);
                    Console.WriteLine($"- requestData: {requestDataDump}");
                    Console.WriteLine($"- request URL: {reqVars.thisUrlPath}");
                    Console.WriteLine($"- stack-trace: {GetStackTrace()}");
                    Console.WriteLine($"----------------------------------------------------");
                    Console.WriteLine("");
                }

                // Return the result in the requested format
                XmlDocument? xmlTaxxorServiceCallResult = null;
                switch (returnType)
                {
                    case "boolean":
                    case "xml":
                    case "taxxorreturnmessage":

                        // Request the remote Taxxor Service
                        switch (httpClientVersion)
                        {
                            case HttpClientEnum.EfficientCustomizableHttp2:
                                xmlTaxxorServiceCallResult = await RestRequestHttp2<XmlDocument>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug, suppressResponseExceptionLogging);
                                break;

                            case HttpClientEnum.EfficientCustomizableHttp1:
                                xmlTaxxorServiceCallResult = await RestRequestHttp1<XmlDocument>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug, suppressResponseExceptionLogging);
                                break;

                            case HttpClientEnum.EfficientPredefinedHttp2:
                                xmlTaxxorServiceCallResult = await RestRequestPredefinedClientHttp2<XmlDocument>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug, suppressResponseExceptionLogging);
                                break;

                            case HttpClientEnum.EfficientPredifinedHttp1:
                                xmlTaxxorServiceCallResult = await RestRequestPredefinedClientHttp1<XmlDocument>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug, suppressResponseExceptionLogging);
                                break;

                            default:
                                xmlTaxxorServiceCallResult = await RestRequest<XmlDocument>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug, suppressResponseExceptionLogging);
                                break;
                        }

                        switch (returnType)
                        {
                            case "boolean":
                                // - Return a boolean based on the information if the remote request was successful or not 
                                if (XmlContainsError(xmlTaxxorServiceCallResult))
                                {
                                    WriteErrorMessageToConsole(xmlTaxxorServiceCallResult.SelectSingleNode("/error/message").InnerText, xmlTaxxorServiceCallResult.SelectSingleNode("/error/debuginfo").InnerText);
                                    return (T)Convert.ChangeType(false, typeof(T));
                                }
                                else
                                {
                                    return (T)Convert.ChangeType(true, typeof(T));
                                }

                            case "taxxorreturnmessage":
                                var taxxorReturnMessage = new TaxxorReturnMessage(xmlTaxxorServiceCallResult);
                                return (T)Convert.ChangeType(taxxorReturnMessage, typeof(T));

                            default:
                                // - Return the XmlDocument that we have received from the remote service
                                return (T)Convert.ChangeType(xmlTaxxorServiceCallResult, typeof(T));

                        }

                    default:
                        // Request the remote Taxxor Service
                        string? taxxorServiceCallResult = null;

                        // Request the remote Taxxor Service
                        switch (httpClientVersion)
                        {
                            case HttpClientEnum.EfficientCustomizableHttp2:
                                taxxorServiceCallResult = await RestRequestHttp2<string>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug);
                                break;

                            case HttpClientEnum.EfficientCustomizableHttp1:
                                taxxorServiceCallResult = await RestRequestHttp1<string>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug);
                                break;

                            case HttpClientEnum.EfficientPredefinedHttp2:
                                taxxorServiceCallResult = await RestRequestPredefinedClientHttp2<string>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug);
                                break;

                            case HttpClientEnum.EfficientPredifinedHttp1:
                                taxxorServiceCallResult = await RestRequestPredefinedClientHttp1<string>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug);
                                break;

                            default:
                                taxxorServiceCallResult = await RestRequest<string>(method, apiUrl, requestData, customHttpHeaders, remoteServiceTimeout, debug);
                                break;
                        }

                        // Return the string
                        return (T)Convert.ChangeType(taxxorServiceCallResult, typeof(T));
                }


            }
            catch (Exception overallException)
            {
                var errorMessage = $"There was an error remote requesting {methodId}";
                var debugInfo = $"error: {overallException.ToString()}";
                appLogger.LogError(overallException, errorMessage);
                switch (returnType)
                {
                    case "boolean":
                        return (T)Convert.ChangeType(false, typeof(T));


                    case "xml":
                        // Request the remote Taxxor Service
                        XmlDocument? xmlTaxxorServiceCallResult = GenerateErrorXml(errorMessage, debugInfo);

                        // Return the XmlDocument that we have received from the remote service
                        return (T)Convert.ChangeType(xmlTaxxorServiceCallResult, typeof(T));

                    default:
                        // Request the remote Taxxor Service
                        string taxxorServiceCallResult = $"ERROR: {errorMessage}";

                        // Return the string
                        return (T)Convert.ChangeType(taxxorServiceCallResult, typeof(T));
                }
            }
        }

    }
}