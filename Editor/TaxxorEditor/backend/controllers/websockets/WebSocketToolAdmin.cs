using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using DocumentStore.Protos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Auditor view websocket methods
        /// </summary>
        public partial class WebSocketsHub : Hub
        {

            /// <summary>
            /// Lists the available projects
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/listprojects")]
            public async Task<TaxxorReturnMessage> ListCmsProjects(string jsonData)
            {
                var errorMessage = "There was an error listing the source projects";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var generateVersions = siteType != "local";
                    // generateVersions = true;

                    // Handle security
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }
                    else
                    {
                        // Retrieve the posted data by converting JSON to XML
                        var xmlDataPosted = ConvertJsonToXml(jsonData, "data");
                        // Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                        // Extract the posted data
                        var token = _extractPostedXmlValue(xmlDataPosted, "token");
                        var projectId = _extractPostedXmlValue(xmlDataPosted, "pid");
                        var filterReportType = _extractPostedXmlValue(xmlDataPosted, "filterreporttype");

                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add(token, @"^[a-zA-Z0-9]{10,40}$", true);
                        inputValidationCollection.Add(projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        inputValidationCollection.Add(filterReportType, @"^(\w|\-|\d){1,128}$", false);
                        var validationResult = inputValidationCollection.Validate();
                        if (!validationResult.Success)
                        {
                            appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                            return validationResult;
                        }
                        else
                        {
                            // Fill the request and project variables in the context with the posted values
                            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId.Value, true);
                            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                            // Retrieve the project information

                            var xmlProjectList = ListProjects(null, string.IsNullOrEmpty(filterReportType.Value) ? null : filterReportType.Value);

                            // Construct the response
                            var xmlResponse = RetrieveTemplate("success_xml");
                            xmlResponse.SelectSingleNode("/result/message").InnerText = "Successfully retrieved CMS project list";
                            xmlResponse.SelectSingleNode("/result/debuginfo").InnerText = $"filterProjectType: {filterReportType.Value}";

                            var nodeProjectImported = xmlResponse.ImportNode(xmlProjectList.DocumentElement, true);
                            xmlResponse.DocumentElement.AppendChild(nodeProjectImported);

                            // Convert to JSON and return it to the client
                            string jsonToReturn = JsonConvert.SerializeObject(xmlResponse, Newtonsoft.Json.Formatting.Indented);

                            return new TaxxorReturnMessage(true, "Successfully retrieved CMS project list", jsonToReturn, "");
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Lists the available datareferences
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/listdatareferences")]
            public async Task<TaxxorReturnMessage> ListDataReferences(string jsonData)
            {
                var errorMessage = "There was an error listing the data references";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var generateVersions = siteType != "local";
                    // generateVersions = true;

                    // Handle security
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }
                    else
                    {
                        // Retrieve the posted data by converting JSON to XML
                        var xmlDataPosted = ConvertJsonToXml(jsonData, "data");
                        // Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                        // Extract the posted data
                        var token = _extractPostedXmlValue(xmlDataPosted, "token");
                        var projectId = _extractPostedXmlValue(xmlDataPosted, "pid");
                        var sourceProjectId = _extractPostedXmlValue(xmlDataPosted, "sourcepid");

                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add(token, @"^[a-zA-Z0-9]{10,40}$", true);
                        inputValidationCollection.Add(projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        inputValidationCollection.Add(sourceProjectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        var validationResult = inputValidationCollection.Validate();
                        if (!validationResult.Success)
                        {
                            appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                            return validationResult;
                        }
                        else
                        {
                            var dataReferences = await RetrieveDataReferences(sourceProjectId.Value);

                            dataReferences.Sort();

                            dynamic jsonResponseData = new ExpandoObject();
                            jsonResponseData.result = new ExpandoObject();
                            jsonResponseData.result.message = "Successfully retrieved data references";
                            jsonResponseData.result.debuginfo = $"sourceProjectId: {sourceProjectId.Value}";
                            jsonResponseData.result.datareferences = dataReferences;

                            // Convert to JSON and return it to the client
                            string jsonToReturn = JsonConvert.SerializeObject(jsonResponseData, Newtonsoft.Json.Formatting.Indented);

                            return new TaxxorReturnMessage(true, "Successfully retrieved data references", jsonToReturn, "");
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Import filing data and mappings
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/importfilingdataandmappings")]
            public async Task<TaxxorReturnMessage> ImportFilingDataAndMappingsWs(string jsonData)
            {
                var errorMessage = "There was an error importing filing data and mappings";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var generateVersions = siteType != "local";
                    // generateVersions = true;

                    // Handle security
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }
                    else
                    {
                        // Retrieve the posted data by converting JSON to XML
                        var xmlDataPosted = ConvertJsonToXml(jsonData, "data");
                        // Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                        // Extract the posted data
                        var token = _extractPostedXmlValue(xmlDataPosted, "token");
                        var projectId = _extractPostedXmlValue(xmlDataPosted, "pid");
                        var sourceProjectId = _extractPostedXmlValue(xmlDataPosted, "sourcepid");
                        var sourceDataReferences = _extractPostedXmlValue(xmlDataPosted, "sourcedatarefs");

                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add(token, @"^[a-zA-Z0-9]{10,40}$", true);
                        inputValidationCollection.Add(projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        inputValidationCollection.Add(sourceProjectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        inputValidationCollection.Add(sourceDataReferences, @"^[a-zA-Z0-9\-_,\.]{2,2048}$", true);
                        var validationResult = inputValidationCollection.Validate();
                        if (!validationResult.Success)
                        {
                            appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                            return validationResult;
                        }
                        else
                        {
                            var dataReferences = new List<string>();
                            if (!sourceDataReferences.Value.Contains(","))
                            {
                                dataReferences.Add(sourceDataReferences.Value);
                            }
                            else
                            {
                                dataReferences = sourceDataReferences.Value.Split(',').ToList();
                            }

                            var importResult = await ImportFilingDataAndMappings(sourceProjectId.Value, projectId.Value, dataReferences);

                            //
                            // => Update the local and remote version of the CMS Metadata XML object now that we are fully done with the transformation
                            //
                            if (importResult.Success)
                            {
                                await UpdateCmsMetadataRemoteAndLocal(projectId.Value);
                            }

                            //
                            // => Clear the paged media cache
                            // 
                            ContentCache.Clear(projectId.Value);

                            // Convert to JSON and return it to the client
                            string jsonToReturn = JsonConvert.SerializeObject(importResult.ToXml(), Newtonsoft.Json.Formatting.Indented);

                            return new TaxxorReturnMessage(true, "Successfully imported filing data and mappings", jsonToReturn, "");
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Clear the Document Store cache
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> ClearDocumentStoreCache()
            {
                var errorMessage = "There was an error clearing the Document Store cache";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Execute the clear cache utility on the Document Store
                    //
                    var clearCacheResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.ClearCache(debugRoutine);

                    return clearCacheResult;

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Clear the XHTML validation results
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> ClearXhtmlValidationResults()
            {
                var errorMessage = "There was an error clearing the XHTML validation results";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Clear the previously found XHTML validation results
                    //
                    validationErrorDetailsList.Clear();

                    return new TaxxorReturnMessage(true, "Successfully cleared XHTML validation results");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Rebuild the CMS Metadata XML object (remote and local versions)
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> RebuildCmsMetadata()
            {
                var errorMessage = "There was an error rebuilding the CMS Metadata";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Rebuilds the complete CMS Metadata XML object (remote and local versions)
                    //
                    await UpdateCmsMetadataRemoteAndLocal();

                    return new TaxxorReturnMessage(true, "Successfully rebuilt the CMS Metadata");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Makes the internal locking system information available for display to the outside world
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> DumpInternalLockingSystemInformation()
            {
                var errorMessage = "There was an retrieving locking system information";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Retrieve the locking information and return the result
                    //
                    var xmlLockInformation = FilingLockStore.ListLocks();

                    return new TaxxorReturnMessage(true, "Successfully retrieved locking information", HtmlEncodeForDisplay(PrettyPrintXml(xmlLockInformation)), "");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Retrieves the system state information from the Editor and Document Store
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> DumpSystemStateInformation()
            {
                var errorMessage = "There was an retrieving system state information";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Retrieve the system state information and return the result
                    //
                    var xmlCompleteSystemStateInformation = new XmlDocument();
                    var nodeRoot = xmlCompleteSystemStateInformation.CreateElement("systemstate");
                    var nodeLocal = xmlCompleteSystemStateInformation.CreateElement("editor");

                    var xmlLocalSystemStateInformation = SystemState.ToXml();
                    var xmlLocalImported = xmlCompleteSystemStateInformation.ImportNode(xmlLocalSystemStateInformation.DocumentElement, true);
                    nodeLocal.AppendChild(xmlLocalImported);
                    nodeRoot.AppendChild(nodeLocal);

                    var nodeRemote = xmlCompleteSystemStateInformation.CreateElement("documentstore");
                    XmlDocument xmlRemoteSystemStateInformation = await Taxxor.Project.ProjectLogic.CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "systemstate");
                    if (!XmlContainsError(xmlRemoteSystemStateInformation))
                    {
                        var xmlRemoteImported = xmlCompleteSystemStateInformation.ImportNode(xmlRemoteSystemStateInformation.DocumentElement, true);
                        nodeRemote.AppendChild(xmlRemoteImported);
                    }
                    else
                    {
                        nodeRemote.InnerText = "There was an error retrieving the remote system state information";
                    }
                    nodeRoot.AppendChild(nodeRemote);
                    xmlCompleteSystemStateInformation.AppendChild(nodeRoot);


                    return new TaxxorReturnMessage(true, "Successfully retrieved system state information", HtmlEncodeForDisplay(PrettyPrintXml(xmlCompleteSystemStateInformation)), "");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Resets the system state information in the Editor and Document Store
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> ResetSystemStateInformation()
            {
                var errorMessage = "There was an retrieving system state information";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Reset the system state information objects
                    //
                    await SystemState.SdsSync.Reset(true);
                    await SystemState.ErpImport.Reset(true);

                    return new TaxxorReturnMessage(true, "Successfully resetted system state information in the Editor and DocumentStore components", "");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Starts the process of recording the URI's which are send out by the Taxxor Editor
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> UriRecordingStart()
            {
                var errorMessage = "There was an error starting the URI recording process.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Empty the list with URI's recorded previously
                    //
                    UriLogBackend.Clear();

                    return new TaxxorReturnMessage(true, "Successfully started the recording process");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Returns the result of the uri recording process
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> UriRecordingShowResults()
            {
                var errorMessage = "There was an error showing the result of the URI recording process.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Loop through the results of the recording process
                    //
                    var xmlLog = new XmlDocument();
                    xmlLog.AppendChild(xmlLog.CreateElement("log"));

                    foreach (var uriString in UriLogBackend)
                    {
                        if (!string.IsNullOrEmpty(uriString))
                        {
                            var currentUri = new Uri(uriString);
                            var currentServiceName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/taxxor/components//*[uri and (contains(uri, '{currentUri.Port}') or contains(uri/domain, '{currentUri.Port}'))]")?.GetAttribute("id") ?? "unknown";

                            var nodeServiceRoot = xmlLog.SelectSingleNode($"/log/{currentServiceName}");
                            if (nodeServiceRoot == null)
                            {
                                var nodeNewServiceRoot = xmlLog.CreateElement(currentServiceName);
                                nodeServiceRoot = xmlLog.DocumentElement.AppendChild(nodeNewServiceRoot);
                            }

                            var nodeUri = nodeServiceRoot.SelectSingleNode($"uri[text()='{uriString}']");
                            if (nodeUri == null)
                            {
                                nodeUri = xmlLog.CreateElementWithText("uri", uriString);
                                nodeServiceRoot.AppendChild(nodeUri);
                            }
                        }
                    }

                    return new TaxxorReturnMessage(true, "Successfully rendered the results of the URI logging process", PrettyPrintXml(xmlLog), "");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Lists the Editor and Document Store logs that are available
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> ListAppLogs()
            {
                var errorMessage = "There was an error listing the application logs.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Read the log files and return the results
                    //
                    dynamic jsonData = new ExpandoObject();

                    jsonData.editor = new ExpandoObject();
                    jsonData.editor.logfiles = new Dictionary<string, string>();

                    DirectoryInfo d = new DirectoryInfo(logRootPathOs);
                    foreach (var file in d.GetFiles("*.txt", SearchOption.TopDirectoryOnly))
                    {
                        if (file.Name.StartsWith("logfile-"))
                        {
                            var logFilePathOs = file.FullName;
                            var logFileName = file.Name;
                            var fileLastAccessed = file.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss");
                            jsonData.editor.logfiles.Add(logFileName, fileLastAccessed);
                        }
                    }

                    jsonData.documentstore = new ExpandoObject();
                    jsonData.documentstore.logfiles = new Dictionary<string, string>();

                    var dataToPost = new Dictionary<string, string>
                    {
                        { "pid", projectVars.projectId },
                        { "path", "/" },
                        { "relativeto", "logroot" }
                    };
                    var xmlDirectoryContents = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditordirectorycontents", dataToPost, true);
                    // appLogger.LogInformation(xmlDirectoryContents.OuterXml);
                    if (!XmlContainsError(xmlDirectoryContents))
                    {
                        var nodeListLogFiles = xmlDirectoryContents.SelectNodes("/result/directory/file[contains(name, 'logfile-')]");
                        foreach (XmlNode nodeLogFile in nodeListLogFiles)
                        {
                            var logFileName = nodeLogFile.SelectSingleNode("name")?.InnerText;

                            // Parse the date into an ISO like format
                            var dateString = nodeLogFile.SelectSingleNode("written")?.InnerText;
                            var fileLastAccessed = DateTime.ParseExact(dateString, "MM/dd/yyyy HH:mm:ss", null).ToString("yyyy-MM-dd HH:mm:ss");

                            jsonData.documentstore.logfiles.Add(logFileName, fileLastAccessed);
                        }
                    }
                    else
                    {
                        return new TaxxorReturnMessage(xmlDirectoryContents);
                    }



                    var json = (string)ConvertToJson(jsonData);

                    return new TaxxorReturnMessage(true, "Successfully listed log files", json, "");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Retrieve an Editor or Document Store log
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> ReadAppLog(string serviceName, string logFileName)
            {
                var errorMessage = "There was an error retrieving the log file contents.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    inputValidationCollection.Add("serviceName", serviceName, @"^editor|documentstore$", true);
                    inputValidationCollection.Add("logFileName", logFileName, @"^logfile-([a-z0-9\-\s]){3,80}.txt$", true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Loop through the results of the recording process
                    //
                    var logFileContents = "";
                    switch (serviceName)
                    {
                        case "editor":
                            logFileContents = await RetrieveTextFile($"{logRootPathOs}/{logFileName}");
                            break;

                        case "documentstore":
                            // return new TaxxorReturnMessage(false, "Not supported yet", "", "");
                            var dataToPost = new Dictionary<string, string>
                            {
                                { "pid", projectVars.projectId },
                                { "path", $"/{logFileName}" },
                                { "relativeto", "logroot" }
                            };

                            var xmlFileContents = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorfilecontents", dataToPost, true);
                            if (!XmlContainsError(xmlFileContents))
                            {
                                logFileContents = xmlFileContents.SelectSingleNode("/result/payload")?.InnerText ?? "";
                            }
                            else
                            {
                                return new TaxxorReturnMessage(xmlFileContents);
                            }
                            break;

                        default:
                            return new TaxxorReturnMessage(false, "Invalid service name", "", "");
                    }

                    // Reverse the lines to get the actual log contents to print the latest on top
                    // var lines = logFileContents.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    // Array.Reverse(lines);
                    // logFileContents = string.Join(Environment.NewLine, lines);

                    return new TaxxorReturnMessage(true, "Successfully retrieved log file", logFileContents, "");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Retrieves a list of active Taxxor DM users
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> GetActiveUsers()
            {
                var errorMessage = "There was an error retrieving the list of active users.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Querties the user connection manager to get a list of all the active user ids
                    //
                    var activeUserIds = UserConnectionManager.GetAllConnectedUserIds();
                    var json = (string)ConvertToJson(activeUserIds);

                    return new TaxxorReturnMessage(true, "Successfully retrieved active users", json, "");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Runs through all the source data references and checks for XHTML errors
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> CheckXhtmlCompliance()
            {
                var errorMessage = "There was an error checking XHTML compliance of source data.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Run the XHTML test
                    //

                    // Get the gRPC client service
                    FilingComposerDataService.FilingComposerDataServiceClient filingComposerDataClient = System.Web.Context.Current.RequestServices.GetRequiredService<FilingComposerDataService.FilingComposerDataServiceClient>();

                    // Cleanup validation report issues
                    validationErrorDetailsList.Clear();

                    // Run the validation rountine
                    await CheckAllXhtmlSections(filingComposerDataClient);

                    // Render the validation report
                    var validationReport = RenderXhtmlValidationReport(validationErrorDetailsList, false, ReturnTypeEnum.Html);

                    // Store the result to a file
                    await TextFileCreateAsync(validationReport, CalculateFullPathOs("xhtml-validation-report"));

                    return new TaxxorReturnMessage(true, "Successfully executed the XHTML compliance test", validationReport, "");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Retrieves the latest generated XHTML compliance report
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> RetrieveXhtmlComplianceReport()
            {
                var errorMessage = "There was an error retrieving the XHTML compliance report.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Retrieve the validation report
                    //
                    var reportPathOs = CalculateFullPathOs("xhtml-validation-report");

                    if (File.Exists(reportPathOs))
                    {
                        var validationReportContents = File.ReadAllText(reportPathOs);
                        var validationReport = new XmlDocument();
                        validationReport.LoadHtml(validationReportContents);
                        return new TaxxorReturnMessage(true, "Successfully retrieved the XHTML compliance report", validationReport.SelectSingleNode("//body")?.InnerXml ?? validationReport.OuterXml, "");
                    }
                    else
                    {
                        return new TaxxorReturnMessage(false, "The XHTML compliance report does not exist", "", "");
                    }

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }

            /// <summary>
            /// Clears in memory cache of the Taxxor Editor
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> ClearEditorSystemCache()
            {
                var errorMessage = "There was an error clearing the editor system cache.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    // var authorizeAttribute = (AuthorizeAttribute)MethodBase.GetCurrentMethod()
                    //     .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                    //     .FirstOrDefault() as AuthorizeAttribute;

                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Clears all forms of in-memory cache of the Taxxor Editor
                    //
                    ProjectExtensions.FsExistsCache.Clear();

                    return new TaxxorReturnMessage(true, "Successfully cleared the editor cache");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Regenerates the CMS configuration cache
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> RebuidConfigurationCache()
            {
                var errorMessage = "There was an error rebuilding the CMS configuration cache.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Generate application configuration by merging the several nested xml config files (base, project and site)
                    //
                    xmlApplicationConfiguration = CompileApplicationConfiguration(xmlApplicationConfiguration);

                    return new TaxxorReturnMessage(true, "Successfully regenerated CMS configuration");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }



            /// <summary>
            /// Regenerates the CMS configuration cache
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> ClearPagedMediaCache()
            {
                var errorMessage = "There was an error removing the paged media cache.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Clears the paged media cache
                    //
                    ContentCache.Clear();

                    return new TaxxorReturnMessage(true, "Successfully cleared the paged media cache");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Regenerates the access control list (ACL) cache
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> ClearRbacCache()
            {
                var errorMessage = "There was an error clearing the RBAC cache.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Clears the RBAC cache
                    //
                    projectVars.rbacCache.ClearAll();

                    //
                    // => Clear the user section information cache data
                    //
                    UserSectionInformationCacheData.Clear();

                    return new TaxxorReturnMessage(true, "Successfully cleared the RBAC cache");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Dumps the contents of the RBAC cache so that it can be inspected
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> DumpRbacCache()
            {
                var errorMessage = "There was an error storing the contents the RBAC cache for inspection.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Dump the contents of the RBAC cache on the file system
                    //
                    await DumpRbacContents(true);

                    return new TaxxorReturnMessage(true, "Successfully stored the RBAC cache for inspection");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Connects to the txutils container and starts the data synchronization process to pull productio data to staging
            /// </summary>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> Prod2Staging()
            {
                var errorMessage = "There was an error synchronizing production data to staging.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = siteType == "local" || siteType == "dev";

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Execute the synchronization process by calling the API endpoint in txutils
                    //
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(10);

                    var requestUrl = $"http://vscodeserver-txutils:3000/api/prod2staging";
                    var response = await httpClient.PostAsync(requestUrl, null);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        appLogger.LogError("Error during prod2staging synchronization: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                        return new TaxxorReturnMessage(false, $"Error during production to staging synchronization: {response.StatusCode}", errorContent, "");
                    }

                    //
                    // => Clear all caches after successful synchronization
                    //

                    // Force the access control service to reload the data from the disk (required if for example a new project is created in PROD)
                    var flushRbacResult = await Taxxor.ConnectedServices.AccessControlService.Flush();
                    if (!flushRbacResult.Success)
                    {
                        appLogger.LogWarning("Failed to flush RBAC cache: {Message}", flushRbacResult.Message);
                    }

                    // Clear Document Store cache
                    var clearCacheResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.ClearCache(debugRoutine);
                    if (!clearCacheResult.Success)
                    {
                        appLogger.LogWarning("Failed to clear Document Store cache: {Message}", clearCacheResult.Message);
                    }
                    
                    // Clear XHTML validation results
                    validationErrorDetailsList.Clear();
                    
                    // Clear editor system cache
                    ProjectExtensions.FsExistsCache.Clear();
                    
                    // Clear paged media cache
                    ContentCache.Clear(projectVars.projectId);
                    
                    // Clear RBAC cache
                    projectVars.rbacCache.ClearAll();
                    
                    // Clear user section information cache
                    UserSectionInformationCacheData.Clear();
                    

                    var successContent = await response.Content.ReadAsStringAsync();
                    return new TaxxorReturnMessage(true, "Successfully synchronized production data to staging", successContent, "");

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }


        }




    }

}