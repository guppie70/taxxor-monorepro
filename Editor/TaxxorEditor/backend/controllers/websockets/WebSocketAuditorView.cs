using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

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
            /// Restores section content data selected from the Auditor View in the Taxxor Editor
            /// </summary>
            /// <param name="jsonData"></param>
            /// /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/restorecontent")]
            public async Task<TaxxorReturnMessage> RestoreSectionContent(string jsonData)
            {
                var errorMessage = "Unable to restore section content";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

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
                        var repositoryId = _extractPostedXmlValue(xmlDataPosted, "repositoryidentifier");
                        var commitHash = _extractPostedXmlValue(xmlDataPosted, "commithash");
                        var operationId = _extractPostedXmlValue(xmlDataPosted, "operationid");
                        var siteStructureId = _extractPostedXmlValue(xmlDataPosted, "sitestructureid");
                        var objectType = _extractPostedXmlValue(xmlDataPosted, "objecttype");
                        var linkName = _extractPostedXmlValue(xmlDataPosted, "linkname");

                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add(token, @"^[a-zA-Z0-9]{10,40}$", true);
                        inputValidationCollection.Add(projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        inputValidationCollection.Add(repositoryId, @"^(\w|\-|\d){2,32}$", true);
                        inputValidationCollection.Add(commitHash, RegexEnum.Hash, true);
                        inputValidationCollection.Add(operationId, @"^(\w|\-|\d){1,32}$$", true);
                        inputValidationCollection.Add(siteStructureId, @"^(\w|\-|\d|\.){1,512}$", true);
                        inputValidationCollection.Add(objectType, @"^(\w|\-|\d\s){4,32}$", true);
                        inputValidationCollection.Add(linkName, @"^.{1,1024}$", false);
                        var validationResult = inputValidationCollection.Validate();
                        if (!validationResult.Success)
                        {
                            appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                            return validationResult;
                        }
                        else
                        {
                            // Fill the request and project variables in the context with the posted values
                            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId.Value);
                            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                            // Console.WriteLine($"- currentUser: {projectVars.currentUser.Id}");
                            // if (linkName.indexOf('channel hierarchy') > -1 || linkName.indexOf('hierarchy (') > -1)


                            var restoreResult = await ProjectLogic.RestoreSectionContent(projectId.Value, projectVars.currentUser.Id, repositoryId.Value, commitHash.Value, operationId.Value, siteStructureId.Value, linkName.Value, objectType.Value);

                            if (!restoreResult.Success)
                            {
                                appLogger.LogError(restoreResult.ToString());
                            }

                            return restoreResult;
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
            /// Retrieves the history of a specific section using the auditorview data
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/retrievefilingcontentversions")]
            public async Task<TaxxorReturnMessage> RetrieveSectionHistory(string jsonData)
            {
                var debugRoutine = siteType == "local" || siteType == "dev" || siteType == "prev";
                var errorMessage = "There was an error retrieving the section history";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

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
                        var commitHash = _extractPostedXmlValue(xmlDataPosted, "commithash");
                        var siteStructureId = _extractPostedXmlValue(xmlDataPosted, "sitestructureid");
                        var objectType = _extractPostedXmlValue(xmlDataPosted, "objecttype");
                        var linkName = _extractPostedXmlValue(xmlDataPosted, "linkname");
                        var clientTimeOffsetMinutesString = _extractPostedXmlValue(xmlDataPosted, "timezoneoffset");

                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add(token, @"^[a-zA-Z0-9]{10,40}$", true);
                        inputValidationCollection.Add(projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        inputValidationCollection.Add(commitHash, RegexEnum.Hash, true);
                        inputValidationCollection.Add(siteStructureId, @"^(\w|\-|\d|\.){1,512}$", true);
                        inputValidationCollection.Add(objectType, @"^(\w|\-|\d\s){4,32}$", true);
                        inputValidationCollection.Add(linkName, @"^.{1,1024}$", true);
                        inputValidationCollection.Add(clientTimeOffsetMinutesString, @"\d{1,5}", false);
                        var validationResult = inputValidationCollection.Validate();
                        if (!validationResult.Success)
                        {
                            appLogger.LogWarning($"Could not validate input data. error: {validationResult.ToString()}, stack-trace: {GetStackTrace()}");
                            return validationResult;
                        }
                        else
                        {
                            // Fill the request and project variables in the context with the posted values
                            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId.Value);
                            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                            // Calculate the time difference between client and server
                            var clientServerOffsetInMinutes = CalculateClientServerOffsetInMinutes(clientTimeOffsetMinutesString.Value);

                            if (debugRoutine)
                            {
                                Console.WriteLine("-- Client & Server time difference --");
                                // Console.WriteLine($"- clientOffset: {clientTimeOffsetMinutes.ToString()}, serverOffset: {serverOffsetMinutes.ToString()}");
                                Console.WriteLine($"diff: {clientServerOffsetInMinutes} minutes");
                                Console.WriteLine($"createIsoFilenameTimestamp: {createIsoFilenameTimestamp("foo.xml", false, true, "_", 60)}");
                                Console.WriteLine("-------------------------------------");
                            }

                            var sectionHistoryResult = await ProjectLogic.RetrieveSectionHistory(projectId.Value, commitHash.Value, siteStructureId.Value, objectType.Value, linkName.Value, projectVars.currentUser.Id);

                            if (!sectionHistoryResult.Success)
                            {
                                appLogger.LogError(sectionHistoryResult.ToString());
                            }

                            return sectionHistoryResult;



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
            /// Compares two sections stored in the GIT repository and returns a track changes PDF or raw HTML containing the differences between the two
            /// </summary>
            /// <param name="jsonData"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/comparefilingcontent")]
            public async Task<TaxxorReturnMessage> CompareSectionContent(string jsonData)
            {
                var errorMessage = "There was an error generating the comparison content";
                try
                {

                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

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
                        var outputChannelLanguage = _extractPostedXmlValue(xmlDataPosted, "oclang");
                        var dataFileReference = _extractPostedXmlValue(xmlDataPosted, "datareference");
                        var repositoryId = _extractPostedXmlValue(xmlDataPosted, "repositoryidentifier");
                        var latestCommitHash = _extractPostedXmlValue(xmlDataPosted, "latestcommithash");
                        var baseCommitHash = _extractPostedXmlValue(xmlDataPosted, "basecommithash");
                        var outputFormat = _extractPostedXmlValue(xmlDataPosted, "outputformat");
                        var siteStructureId = _extractPostedXmlValue(xmlDataPosted, "sitestructureid");
                        var linkName = _extractPostedXmlValue(xmlDataPosted, "linkname");

                        // Validate the data
                        var inputValidationCollection = new InputValidationCollection();
                        inputValidationCollection.Add(token, @"^[a-zA-Z0-9]{10,40}$", true);
                        inputValidationCollection.Add(projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                        inputValidationCollection.Add(outputChannelLanguage, @"^[a-z]{2,3}$", true);
                        inputValidationCollection.Add(dataFileReference, RegexEnum.FileName, true);
                        inputValidationCollection.Add(repositoryId, @"^(\w|\-|\d){2,32}$", true);
                        inputValidationCollection.Add(latestCommitHash, RegexEnum.HashOrTag, true);
                        inputValidationCollection.Add(baseCommitHash, RegexEnum.HashOrTag, true);
                        inputValidationCollection.Add(outputFormat, @"^(pdf|raw)$", true);
                        inputValidationCollection.Add(siteStructureId, @"^(\w|\-|\d|\.){1,256}$", true);
                        inputValidationCollection.Add(linkName, @"^.{1,1024}$", true);

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


                            var compareSectionResult = await ProjectLogic.CompareSectionContent(projectId.Value, repositoryId.Value, outputChannelLanguage.Value, dataFileReference.Value, latestCommitHash.Value, baseCommitHash.Value, outputFormat.Value, siteStructureId.Value, linkName.Value);

                            if (!compareSectionResult.Success)
                            {
                                appLogger.LogError(compareSectionResult.ToString());
                            }

                            return compareSectionResult;
                        }
                    }

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false,errorMessage, $"error: {ex}");
                }
            }


            /// <summary>
            /// Retrieves details about a commit in the version manager
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="repositoryIdentifier"></param>
            /// <param name="commitHash"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveCommitDetails(string projectId, string repositoryIdentifier, string commitHash)
            {
                var errorMessage = "There was an error retrieving the commit details";
                var defaultDebugInfo = $"projectId:{projectId}, repositoryIdentifier: {repositoryIdentifier}, commitHash: {commitHash}";
                var excludeIpAddress = true;
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }

                    //
                    // => Prepare the project variables object so that we can use it in the authorization process
                    //
                    projectVars.projectId = projectId;
                    projectVars.rbacCache = new RbacCache(projectVars.currentUser.Id, projectVars.projectId);


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
                    // => Retrieve the full information of the commit
                    //
                    var gitLocationId = "";
                    var gitSource = "";
                    switch (repositoryIdentifier)
                    {
                        case "project-data":
                            gitLocationId = "reportdataroot";
                            gitSource = "filingdata";
                            break;
                        case "project-content":
                        default:
                            gitLocationId = "reportcontent";
                            gitSource = "filingassets";
                            break;

                    }

                    var dataToPost = new Dictionary<string, string>
                    {
                        { "source", gitSource },
                        { "hashortag", commitHash },
                        { "component", "documentstore" },
                        { "pid", projectId },
                        { "commithash", commitHash },
                        { "locationid", gitLocationId }
                    };

                    XmlDocument responseXmlCommitInfo = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitcommitinfo", dataToPost, debugRoutine);
                    if (XmlContainsError(responseXmlCommitInfo))
                    {
                        appLogger.LogError($"{errorMessage}. {responseXmlCommitInfo.OuterXml}");
                        return new TaxxorReturnMessage(false, errorMessage);
                    }

                    // - Parse the response
                    string? encodedXml = responseXmlCommitInfo.SelectSingleNode("/result/payload")?.InnerText;
                    var payloadContent = HttpUtility.HtmlDecode(encodedXml);
                    var xmlCommitInfo = new XmlDocument();
                    try
                    {
                        xmlCommitInfo.LoadXml(payloadContent);
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, "There was a problem loading the data");
                    }

                    // - Remove the IP address that we may have logged
                    if (excludeIpAddress)
                    {
                        var nodeIp = xmlCommitInfo.SelectSingleNode("/commit/author/ip");
                        if (nodeIp != null) RemoveXmlNode(nodeIp);
                    }


                    //
                    // => Retrieve a comma delimited string of files that have been changed in the commit
                    //
                    XmlDocument responseXmlCommitFiles = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitcommitfiles", dataToPost, debugRoutine);
                    if (XmlContainsError(responseXmlCommitInfo))
                    {
                        appLogger.LogError($"{errorMessage}. {responseXmlCommitFiles.OuterXml}");
                        return new TaxxorReturnMessage(false, errorMessage);
                    }
                    // Console.WriteLine(responseXmlCommitFiles.OuterXml);
                    string? fileListString = responseXmlCommitFiles.SelectSingleNode("/result/payload")?.InnerText;

                    if (fileListString != null)
                    {
                        // - Append as a sorted changed file list to the XML
                        var fileList = fileListString.Split(new char[] { ',' });
                        Array.Sort(fileList, (s1, s2) => Path.GetFileName(s1).CompareTo(Path.GetFileName(s2)));
                        var nodeChangedFiles = xmlCommitInfo.CreateElement("changed");
                        for (int i = 0; i < fileList.Length; i++)
                        {
                            var fileNameToShow = fileList[i];
                            if (fileNameToShow.Contains("/textual/")) fileNameToShow = fileNameToShow.SubstringAfter("/textual/");
                            if (fileNameToShow.Contains("/metadata/")) fileNameToShow = fileNameToShow.SubstringAfter("/metadata/");

                            var nodeFile = xmlCommitInfo.CreateElementWithText("file", fileNameToShow);
                            var attr = xmlCommitInfo.CreateAttribute("json", "Array", "http://james.newtonking.com/projects/json");
                            attr.Value = "true";
                            nodeFile.SetAttributeNode(attr);
                            nodeChangedFiles.AppendChild(nodeFile);
                        }
                        xmlCommitInfo.DocumentElement.AppendChild(nodeChangedFiles);
                    }



                    // xmlCommitInfo = PrettyPrintForJsonConversion(xmlCommitInfo);

                    var json = ConvertToJson(xmlCommitInfo, Newtonsoft.Json.Formatting.Indented, true);

                    // Render the result as a TaxxorReturnMessage
                    return new TaxxorReturnMessage(true, "Successfully rendered commit information", json, defaultDebugInfo);
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