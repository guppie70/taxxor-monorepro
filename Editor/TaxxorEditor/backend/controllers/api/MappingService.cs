using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Stores new mapping information using the Taxxor Mapping Service
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task StoreMappingInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            string? jsonMappingCluster = context.Request.RetrievePostedValue("mappingcluster", RegexEnum.None, true, reqVars.returnType);
            var postedId = request.RetrievePostedValue("did", RegexEnum.Loose, true, ReturnTypeEnum.Json);

            // Additional security checks:
            // - Does this user have permissions to edit this section?
            var checkSectionPermissions = true;
            // - Does this section contain the SDE for which we want to store the mapping?
            var checkIfSdeExistsInSectionContent = false;


            //
            // => Check if this user has access to the section in which the SDE that we want to update is located
            //
            if (ValidateCmsPostedParameters(projectVars.projectId))
            {
                XmlDocument responseXml = new XmlDocument();
                XmlDocument? xmlHierarchyDoc = null;

                //
                // => Retrieve output channel hierarchy
                //
                var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);
                // var outputChannelHierarchy = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;

                if (!projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                {
                    appLogger.LogInformation("Retrieving metadata from the server");
                    var hierarchyRetrieveResult = await RetrieveOutputChannelHierarchiesMetaData(projectVars, reqVars, true);
                    if (!hierarchyRetrieveResult) HandleError(ReturnTypeEnum.Json, "Unable to retrieve the outputchannel hierarchy", $"Failed to retrieve all hierarchy metadata - stack-trace: {GetStackTrace()}");
                }

                if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                {
                    // - Retrieve the output channel hierarchy
                    xmlHierarchyDoc = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;
                }
                else
                {
                    HandleError(ReturnTypeEnum.Json, "Unable to retrieve the outputchannel hierarchy", $"projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}']=null, stack-trace: {GetStackTrace()}");
                }

                //
                // => Check permissions: is this user allowed to save data
                //

                // - Render a special version of the hierarchy that we can use to generate a breadcrumbtrail that we can send to the access control service
                var xmlDocumentForPermissionsCheck = CreateHierarchyForRbacPermissionsRetrieval(reqVars.xmlHierarchy, xmlHierarchyDoc);

                // - Find the section that we want to save in this special hierarchy
                var nodeItem = xmlDocumentForPermissionsCheck.SelectSingleNode($"/items/structured//item[@id={GenerateEscapedXPathString(postedId)}]");
                if (nodeItem != null)
                {
                    if (checkSectionPermissions)
                    {
                        // - Retrieve the permissions and test if this user is allowed to edit the section that we are about to save
                        var permissions = await AccessControlService.RetrievePermissions(RequestMethodEnum.Get, projectVars.projectId, nodeItem, true, debugRoutine);
                        // if (!permissions.HasPermission("editcontent") || !permissions.HasPermission("editmappingtarget"))
                        // if (!permissions.HasPermission("editcontent") && !permissions.HasPermission("editmappingtarget"))
                        if (!permissions.HasPermission("editcontent") && !permissions.HasPermission("editmappingtarget"))
                        {
                            appLogger.LogWarning($"!!!!! User permissions: \n{string.Join("\n", permissions.Permissions.ToArray())} !!!!!");
                            appLogger.LogError($"Unauthorized mapping update. User '{projectVars.currentUser.Id}' is trying to update an SDE in section '{postedId}', but the the user doesn't have enough permissions to do so.");
                            HandleError(ReturnTypeEnum.Json, "Not allowed", $"postedId: {postedId}, stack-trace: {GetStackTrace()}", 403);
                        }
                    }
                }
                else
                {
                    HandleError(ReturnTypeEnum.Json, "Unable to locate section in the outputchannel hierarchy", $"postedId: {postedId}, stack-trace: {GetStackTrace()}");
                }
            }

            //
            // => Parse the mapping cluster data to XML so we can process further
            //
            XmlDocument? xmlMappingClusterRaw = null;
            try
            {
                xmlMappingClusterRaw = ConvertJsonToXml(jsonMappingCluster);
                if (debugRoutine) await xmlMappingClusterRaw.SaveAsync($"{logRootPathOs}/_mappingcluster.xml", false);

                if (debugRoutine)
                {
                    Console.WriteLine("***************** MAPPING CLUSTER INFO RECIEVED ********************");
                    Console.WriteLine(PrettyPrintXml(xmlMappingClusterRaw));
                    Console.WriteLine("********************************************************************");
                }

            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "There was a problem storing the mapping cluster information", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

            //
            // => Check if this section contains the SDE fact ID that we are trying to store
            //
            var sdeFactId = xmlMappingClusterRaw.SelectSingleNode("/mappingcluster/internal/mappingfactid")?.InnerText ?? "";
            var updateMappingMode = !(string.IsNullOrEmpty(sdeFactId));
            // if(debugRoutine) appLogger.LogInformation($"- updateMappingMode: {updateMappingMode}");

            if (updateMappingMode && checkIfSdeExistsInSectionContent)
            {
                // Load the original XHTML source from the Taxxor Document Store
                var xmlFilingContentSourceData = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadSourceData(projectVars.projectId, postedId, "latest", debugRoutine);

                if (XmlContainsError(xmlFilingContentSourceData))
                {
                    HandleError(ReturnTypeEnum.Json, xmlFilingContentSourceData);
                }

                // Test if the SDE is contained in the source data that we want to update
                var xPathSde = $"/data/content[@lang={GenerateEscapedXPathString(projectVars.outputChannelVariantLanguage)}]//*[@data-fact-id={GenerateEscapedXPathString(sdeFactId)}]";
                var nodeSde = xmlFilingContentSourceData.SelectSingleNode(xPathSde);
                if (nodeSde == null)
                {
                    appLogger.LogError($"Unauthorized mapping update. User '{projectVars.currentUser.Id}' is trying to update SDE with ID '{sdeFactId}' in section '{postedId}' of project '{projectVars.projectId}' using xPath: {xPathSde}, but the SDE does not exist in this section");
                    HandleError(ReturnTypeEnum.Json, "Not allowed", $"postedId: {postedId}, stack-trace: {GetStackTrace()}", 403);
                }
            }


            //
            // => Store the mapping information in the Structured Data Store
            //
            try
            {
                // Convert the XML into the format that the mapping service expects
                var xmlMappingToPost = TransformXmlToDocument(xmlMappingClusterRaw, "cms_xsl_mappingclusterconvert");

                if (debugRoutine)
                {
                    Console.WriteLine("******************* MAPPING CLUSTER INFORMATION POSTED ******************");
                    Console.WriteLine(PrettyPrintXml(xmlMappingToPost));
                    Console.WriteLine("*************************************************************************");
                }


                var xmlMappingServiceResult = await MappingService.StoreMappingEntry(xmlMappingToPost);

                if (XmlContainsError(xmlMappingServiceResult))
                {
                    HandleError(reqVars.returnType, "There was an error soring the mapping cluster on the server", $"xmlMappingEntryContent: {xmlMappingServiceResult.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // Console.WriteLine("****************** StoreMappingInformation RESPONSE *******************");
                    // Console.WriteLine(PrettyPrintXml(xmlMappingServiceResult));
                    // Console.WriteLine("*************************************");
                }

                //
                // => Retrieve the mapping ID from the response
                //
                var mappingId = "";
                var nodeMappingId = xmlMappingServiceResult.SelectSingleNode("/MappingEntry/mapping");
                if (nodeMappingId == null)
                {
                    mappingId = xmlMappingServiceResult.DocumentElement.InnerText;
                }
                else
                {
                    mappingId = nodeMappingId.InnerText;
                }

                // Construct a response message for the client
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully stored mapping information";
                jsonData.result.internalmappingid = mappingId;
                if (debugRoutine)
                {
                    jsonData.result.debuginfo = $"";
                }
                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                // Render the response
                await response.OK(jsonToReturn, reqVars.returnType, true);
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "There was a problem storing the mapping cluster information", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }


        /// <summary>
        /// Filters a list of fact-id's / mapping entries
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FilterMappingEntries(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            bool? hasFlagFilter = null;
            bool? hasCommentFilter = null;

            // Get posted data
            var factGuid = request.RetrievePostedValue("factguids", @"^.{1,1000000}$", true, reqVars.returnType);
            var hasFlagString = request.RetrievePostedValue("hasflag", RegexEnum.Boolean);
            if (hasFlagString != null) hasFlagFilter = (hasFlagString == "true");
            var hasCommentString = request.RetrievePostedValue("hascomment", RegexEnum.Boolean);
            if (hasCommentString != null) hasCommentFilter = (hasCommentString == "true");
            string? mappingIdFilter = request.RetrievePostedValue("mapping", RegexEnum.Default);
            string? periodFilter = request.RetrievePostedValue("period", RegexEnum.Default);
            string? schemeFilter = request.RetrievePostedValue("scheme", RegexEnum.Default);
            string? statusFilter = request.RetrievePostedValue("status", RegexEnum.Default);

            var basicDebugInfo = $"hasFlagFilter: {hasFlagFilter.ToString()}, hasCommentFilter: {hasCommentFilter.ToString()}, mappingIdFilter: {mappingIdFilter}, periodFilter: {periodFilter}, schemeFilter: {schemeFilter}, statusFilter: {statusFilter}";

            // Convert it into a list
            var indentifiers = new List<string>();
            if (factGuid.Contains(","))
            {
                indentifiers = factGuid.Split(',').ToList();
            }
            else
            {
                indentifiers.Add(factGuid);
            }

            // Retrieve the filtered list from the Taxxor Mapping Service
            List<string> filteredGuids = await MappingService.FilterMappingEntries(indentifiers, projectVars.projectId, hasFlagFilter, hasCommentFilter, periodFilter, schemeFilter, statusFilter, mappingIdFilter);

            // Construct the message to return
            dynamic jsonData = new ExpandoObject();
            jsonData.result = new ExpandoObject();
            jsonData.result.message = "Successfully rendered filtered entries";
            jsonData.result.filteredentries = filteredGuids;
            if ((siteType == "local" || siteType == "dev"))
            {
                jsonData.result.debuginfo = basicDebugInfo;
            }
            var json = (string)ConvertToJson(jsonData);

            // Write the response to the client
            await response.OK(json, ReturnTypeEnum.Json, true);
        }


        /// <summary>
        /// Filters an XBRL taxonomy
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task FilterTaxonomy(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            bool? isAbstractFilter = null;
            bool? isDimensionFilter = null;
            bool? isMemberFilter = null;
            bool? isAxisFilter = null;

            // Get posted data
            var taxonomyIdentifier = request.RetrievePostedValue("taxonomyid", RegexEnum.Default);

            string? periodFilter = request.RetrievePostedValue("period", @"^(instant|duration)$", false, reqVars.returnType, null);
            string? baseDataTypeFilter = request.RetrievePostedValue("basetype", RegexEnum.Default);
            string? textFilter = request.RetrievePostedValue("filtertext", RegexEnum.Default);

            var isAbstractString = request.RetrievePostedValue("isabstract", RegexEnum.Boolean);
            if (isAbstractString != null) isAbstractFilter = (isAbstractString == "true");
            var isDimensionString = request.RetrievePostedValue("isdimension", RegexEnum.Boolean);
            if (isDimensionString != null) isDimensionFilter = (isDimensionString == "true");
            var isMemberString = request.RetrievePostedValue("ismember", RegexEnum.Boolean);
            if (isMemberString != null) isMemberFilter = (isMemberString == "true");
            var isAxisString = request.RetrievePostedValue("isaxis", RegexEnum.Boolean);
            if (isAxisString != null) isAxisFilter = (isAxisString == "true");

            var basicDebugInfo = $"projectId: {projectVars.projectId}, taxonomyIdentifier: {taxonomyIdentifier}, isAbstractFilter: {isAbstractFilter.ToString()}, isDimensionFilter: {isDimensionFilter.ToString()}, periodFilter: {periodFilter}, baseDataTypeFilter: {baseDataTypeFilter}, isMemberFilter: {isMemberFilter.ToString()}, isAxisFilter: {isAxisFilter.ToString()}, textFilter: {textFilter}";

            //HandleError("Thrown on purpose in FilterTaxonomy()", basicDebugInfo);

            var xmlFilterTaxonomyResult = await MappingService.FilterTaxonomy(projectVars.projectId, taxonomyIdentifier, periodFilter, baseDataTypeFilter, isAbstractFilter, isDimensionFilter, isMemberFilter, isAxisFilter, textFilter);

            if (XmlContainsError(xmlFilterTaxonomyResult))
            {
                HandleError(reqVars.returnType, "There was an error filtering the taxonomy", $"{basicDebugInfo}, xmlFilterTaxonomyResult: {xmlFilterTaxonomyResult.OuterXml}, stack-trace: {GetStackTrace()}");
            }

            // Dump the response
            if (debugRoutine) await xmlFilterTaxonomyResult.SaveAsync(logRootPathOs + "/_filter-taxo.xml", false);

            // Resolve the hierarchies by nesting the DTS elements inside the structure
            var xmlResultHierarchical = TransformXmlToDocument(xmlFilterTaxonomyResult, "cms_xsl_dts-resolve");

            // Convert the XML to JSON format
            var jsonToReturn = ConvertToJson(xmlResultHierarchical);

            // Render response
            await response.OK(jsonToReturn, reqVars.returnType, true);
        }

        /// <summary>
        /// Clones a mapping cluster and potentially adjusts the project ID or the period
        /// Returns the new fact ID and the new fact value in the payload of the json message that is returned
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task MappingServiceCloneCluster(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Global variables
            // var errorMessage = "Unable to clone the mapping cluster";

            // Get posted data
            var factGuid = request.RetrievePostedValue("factid", @"^[a-zA-Z0-9_\-\d\:\%\.;\+\s]{2,1024}$", true, reqVars.returnType);
            var newProjectId = request.RetrievePostedValue("newprojectid", RegexEnum.Default);
            var newPeriod = request.RetrievePostedValue("newperiod", RegexEnum.Default);
            var requestId = request.RetrievePostedValue("requestid", RegexEnum.Default, false, reqVars.returnType, "none");

            var factGuids = new List<string>();
            if (factGuid.Contains(","))
            {
                factGuids = factGuid.Split(',').ToList();
            }
            else
            {
                factGuids.Add(factGuid);
            }

            dynamic jsonData = new ExpandoObject();
            jsonData.result = new ExpandoObject();
            jsonData.result.requestid = requestId;
            jsonData.result.payload = new List<ExpandoObject>();

            if (string.IsNullOrEmpty(newProjectId) && string.IsNullOrEmpty(newPeriod))
            {
                //
                // => Clone the mapping cluster using the simple method
                //
                var xmlCloneResult = await MappingService.CloneMappingCluster(projectVars.projectId, factGuids, debugRoutine);


                // Process the result
                if (XmlContainsError(xmlCloneResult))
                {
                    string.Join(",", factGuids);

                    appLogger.LogError($"Could not clone mapping cluster for factId: {string.Join(",", factGuids)}, message: {xmlCloneResult.SelectSingleNode("//message")?.InnerText ?? ""}, debuginfo: {xmlCloneResult.SelectSingleNode("//debuginfo")?.InnerText ?? ""}");

                    // Append the error
                    dynamic factDetails = new ExpandoObject();
                    factDetails.error = new ExpandoObject();
                    factDetails.error.message = xmlCloneResult.SelectSingleNode("//message")?.InnerText ?? "";
                    factDetails.error.debuginfo = xmlCloneResult.SelectSingleNode("//debuginfo")?.InnerText ?? "";
                    factDetails.error.originalfactid = string.Join(",", factGuids);
                    jsonData.result.payload.Add(factDetails);

                }
                else
                {
                    var nodeListItems = xmlCloneResult.SelectNodes("/response/item");
                    foreach (XmlNode nodeItem in nodeListItems)
                    {
                        // Append the new internal ID
                        dynamic factDetails = new ExpandoObject();
                        factDetails.factid = nodeItem.SelectSingleNode("factId")?.InnerText;
                        factDetails.originalfactid = nodeItem.SelectSingleNode("orgFactId")?.InnerText;

                        // Retrieve display value in a JSON format
                        dynamic jsonDataNested = new ExpandoObject();
                        jsonDataNested.values = new List<ExpandoObject>();
                        foreach (XmlNode nodeValue in nodeItem.SelectNodes("value/localisedvalue"))
                        {
                            dynamic valueResult = new ExpandoObject();
                            valueResult.lang = nodeValue.GetAttribute("locale");
                            valueResult.value = nodeValue?.InnerText ?? "";
                            jsonDataNested.values.Add(valueResult);
                        }

                        factDetails.value = (string)ConvertToJson(jsonDataNested);


                        // Extend the response with the details about the new internal (fact)
                        jsonData.result.payload.Add(factDetails);
                    }
                }
            }
            else
            {
                //
                // => Clone the mapping cluster using the advanced method
                //
                foreach (var factId in factGuids)
                {
                    // Clone the mapping cluster
                    var xmlCloneResult = await MappingService.CloneMappingCluster(projectVars.projectId, factId, newPeriod, newProjectId, null, null, debugRoutine);

                    // HandleError("Testing error handling");

                    // Process the result
                    if (XmlContainsError(xmlCloneResult))
                    {
                        appLogger.LogError($"Could not clone mapping cluster for factId: {factId}, message: {xmlCloneResult.SelectSingleNode("//message")?.InnerText ?? ""}, debuginfo: {xmlCloneResult.SelectSingleNode("//debuginfo")?.InnerText ?? ""}");

                        // Append the error
                        dynamic factDetails = new ExpandoObject();
                        factDetails.error = new ExpandoObject();
                        factDetails.error.message = xmlCloneResult.SelectSingleNode("//message")?.InnerText ?? "";
                        factDetails.error.debuginfo = xmlCloneResult.SelectSingleNode("//debuginfo")?.InnerText ?? "";
                        factDetails.error.originalfactid = factId;
                        jsonData.result.payload.Add(factDetails);
                    }
                    else
                    {
                        // Retrieve the new fact ID (Internal mapping ID) that was created
                        var newFactId = xmlCloneResult.SelectSingleNode("//mapping|//string")?.InnerText ?? "";
                        if (string.IsNullOrEmpty(newFactId))
                        {
                            HandleError("Could not retrieve new factId", $"xmlCloneResult: {xmlCloneResult.OuterXml}");
                        }
                        else
                        {
                            // Append the new internal ID
                            dynamic factDetails = new ExpandoObject();
                            factDetails.factid = newFactId;
                            factDetails.originalfactid = factId;

                            // Retrieve display value
                            try
                            {
                                var structuredDataElementValueResult = await RetieveStructuredDataValue(newFactId, projectVars.projectId, projectVars.outputChannelVariantLanguage, debugRoutine);
                                if (structuredDataElementValueResult.Success)
                                {
                                    factDetails.value = structuredDataElementValueResult.Payload;
                                }
                                else
                                {
                                    factDetails.value = null;
                                }

                            }
                            catch (Exception ex)
                            {
                                appLogger.LogWarning($"Could not retrieve display value. error: {ex}");
                                factDetails.value = null;
                            }

                            // Extend the response with the details about the new internal (fact)
                            jsonData.result.payload.Add(factDetails);
                        }
                    }

                }


            }


            string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

            // Render the response
            await response.OK(jsonToReturn, reqVars.returnType, true);
        }

        /// <summary>
        /// Search and replaces information in a mapping cluster
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task MappingServiceClusterSearchReplace(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Global variables
            // var errorMessage = "Unable to clone the mapping cluster";

            // Get posted data
            var dataReferenceOrFactIds = request.RetrievePostedValue("reference", @"^[a-zA-Z0-9_\-\d\:\%\.;\+\s]{2,1024}$", true, reqVars.returnType);
            var erpDataset = request.RetrievePostedValue("dataset", @"^[a-zA-Z0-9_\-\d\:\%\.;\+\s]{2,1024}$", false, reqVars.returnType, "");
            var searchPhrase = request.RetrievePostedValue("searchphrase", RegexEnum.Default, true, reqVars.returnType);
            var replacePhrase = request.RetrievePostedValue("replacephrase", RegexEnum.Default, true, reqVars.returnType);
            var regExReplaceModeString = request.RetrievePostedValue("regexreplacemode", RegexEnum.Boolean, false, reqVars.returnType, "false");
            var regExReplaceMode = (regExReplaceModeString == "true");
            var testRunString = request.RetrievePostedValue("testrun", RegexEnum.Boolean, false, reqVars.returnType, "true");
            var testRun = (testRunString == "true");

            var replaceResult = await MappingServiceClusterSearchReplace(projectVars, dataReferenceOrFactIds, erpDataset, searchPhrase, replacePhrase, regExReplaceMode, testRun);

            if (replaceResult.Success)
            {
                await response.OK(replaceResult, reqVars.returnType, true);
            }
            else
            {
                await response.Error(replaceResult, reqVars.returnType, true);
            }

        }

        /// <summary>
        /// Search and replaces information in a mapping cluster
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="dataReferenceOrFactIds"></param>
        /// <param name="erpDataset"></param>
        /// <param name="searchPhrase"></param>
        /// <param name="replacePhrase"></param>
        /// <param name="regExReplaceMode"></param>
        /// <param name="testRun"></param>
        /// <returns></returns> 
        public async static Task<TaxxorReturnMessage> MappingServiceClusterSearchReplace(ProjectVariables projectVars, string dataReferenceOrFactIds, string erpDataset, string searchPhrase, string replacePhrase, bool regExReplaceMode, bool testRun)
        {
            var errorMessage = "There was an error search and replace in the mappings";


            var debugRoutine = (siteType == "local" || siteType == "dev");
            var replaceResult = new StringBuilder();

            try
            {

                //
                // => Global variables
                //
                var checkErpDataSet = !string.IsNullOrEmpty(erpDataset);
                var debugInfo = $"projectId: {projectVars.projectId}, dataReference: {dataReferenceOrFactIds}, erpDataset: {erpDataset}, searchPhrase: {searchPhrase}, replacePhrase: {replacePhrase}, regExReplaceMode: {regExReplaceMode}, testRun: {testRun}";

                //
                // => Render a list of fact ID's we need to search and replace
                //
                var factIds = new List<string>();

                if (dataReferenceOrFactIds.Contains(".xml"))
                {
                    //
                    // => Retrieve the source data file
                    //
                    if (debugRoutine) appLogger.LogInformation($"Loading section data with reference: '{dataReferenceOrFactIds}'");
                    var xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{dataReferenceOrFactIds}", "cmsdataroot", debugRoutine, false);
                    if (XmlContainsError(xmlFilingContentSourceData))
                    {
                        appLogger.LogError($"{dataReferenceOrFactIds} failed to load with error: {xmlFilingContentSourceData.OuterXml}");
                        return new TaxxorReturnMessage(false, $"{dataReferenceOrFactIds} failed to load", ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {xmlFilingContentSourceData.OuterXml}, " + debugInfo : ""));
                    }

                    //
                    // => Generate a list of fact id's in the content
                    //
                    var nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlFilingContentSourceData, false);
                    foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                    {
                        var factId = nodeStructuredDataElement.GetAttribute("data-fact-id").Replace("\n", "");
                        if (!string.IsNullOrEmpty(factId))
                        {
                            if (!factIds.Contains(factId)) factIds.Add(factId);
                        }
                        else
                        {
                            appLogger.LogWarning("Found a SDE without an ID");
                        }
                    }
                }
                else
                {
                    //
                    // => Convert the list of fact ID's
                    //
                    if (dataReferenceOrFactIds.Contains(','))
                    {
                        factIds = dataReferenceOrFactIds.Split(',').ToList();
                    }
                    else
                    {
                        factIds.Add(dataReferenceOrFactIds);
                    }
                }


                //
                // => Retrieve the mapping information for the data reference
                //
                var xmlMappingInformation = await MappingService.RetrieveMappingInformation(factIds, projectVars.projectId, false);

                var factIdMapping = new Dictionary<string, string>();
                if (!XmlContainsError(xmlMappingInformation))
                {

                    var nodeListMappingClusters = xmlMappingInformation.SelectNodes("/mappingClusters/mappingCluster");
                    foreach (XmlNode nodeMappingCluster in nodeListMappingClusters)
                    {
                        var originalFactId = nodeMappingCluster.GetAttribute("requestId");

                        // - prepare a new mapping cluster to store in the database
                        var xmlMappingToPost = new XmlDocument();
                        xmlMappingToPost.ReplaceContent(nodeMappingCluster);
                        // xmlMappingToPost.DocumentElement.RemoveAttribute("id");
                        xmlMappingToPost.DocumentElement.RemoveAttribute("requestId");
                        xmlMappingToPost.DocumentElement.SetAttribute("projectId", projectVars.projectId);

                        var nodeListEntries = xmlMappingToPost.SelectNodes("//entry");
                        foreach (XmlNode nodeEntry in nodeListEntries)
                        {
                            var mappingScheme = nodeEntry.GetAttribute("scheme")?.ToLower() ?? "";
                            nodeEntry.RemoveAttribute("id");


                            // if (mappingScheme == "internal")
                            // {
                            //     var nodeMapping = nodeEntry.SelectSingleNode("mapping");
                            //     if (nodeMapping != null) RemoveXmlNode(nodeMapping);
                            // }

                            if (mappingScheme == "efr")
                            {
                                var nodeMapping = nodeEntry.SelectSingleNode("mapping");
                                if (nodeMapping != null)
                                {
                                    var originalMappingJson = nodeMapping.InnerText;
                                    var replacedMappingJson = nodeMapping.InnerText;
                                    var executeReplacement = true;
                                    if (checkErpDataSet)
                                    {
                                        if (!originalMappingJson.Contains($"\"dataset\":\"{erpDataset}\""))
                                        {
                                            executeReplacement = false;
                                        }
                                    }

                                    if (executeReplacement)
                                    {
                                        if (regExReplaceMode)
                                        {
                                            // var regEx = new Regex( @$"{searchPhrase}");
                                            // Console.WriteLine(regEx.Replace(originalMappingJson, replacePhrase));
                                            replacedMappingJson = RegExpReplace(searchPhrase, originalMappingJson, replacePhrase);
                                        }
                                        else
                                        {
                                            replacedMappingJson = originalMappingJson.Replace(searchPhrase, replacePhrase);
                                        }
                                    }

                                    if (replacedMappingJson != originalMappingJson)
                                    {
                                        nodeMapping.InnerText = replacedMappingJson;

                                        Console.WriteLine("*** MATCH ***");
                                        Console.WriteLine("-> Original mapping");
                                        Console.WriteLine(PrettyPrintXml(nodeMappingCluster.OuterXml));
                                        Console.WriteLine("");
                                        Console.WriteLine("-> Updated mapping");
                                        Console.WriteLine(PrettyPrintXml(xmlMappingToPost.OuterXml));
                                        Console.WriteLine("**************");

                                        if (!testRun)
                                        {
                                            // Save the result
                                            var xmlUpdateResult = await MappingService.UpdateMappingEntry(xmlMappingToPost);

                                            if (!XmlContainsError(xmlUpdateResult))
                                            {
                                                replaceResult.AppendLine($"* Replaced mapping in {originalFactId} ({originalMappingJson} -> {replacedMappingJson})");
                                            }
                                            else
                                            {
                                                replaceResult.AppendLine($"* Error saving updated mapping with factId: {originalFactId} ({originalMappingJson} -> {replacedMappingJson})");
                                            }
                                        }
                                        else
                                        {
                                            replaceResult.AppendLine($"* Matched mapping in {originalFactId} ({originalMappingJson} -> {replacedMappingJson}) but did not replace it");
                                        }
                                    }


                                }
                                else
                                {
                                    appLogger.LogError($"Unable to locate ERP mapping node in {nodeEntry.OuterXml}");
                                }
                            }
                        }
                    }
                }
                else
                {
                    errorMessage = $"Failed to retrieve mapping information for {dataReferenceOrFactIds} failed to save with error: {xmlMappingInformation.OuterXml}";
                    appLogger.LogError(errorMessage);
                }

                if (!string.IsNullOrEmpty(replaceResult.ToString()))
                {
                    return new TaxxorReturnMessage(true, replaceResult.ToString(), ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? debugInfo : ""));
                }
                else
                {
                    return new TaxxorReturnMessage(true, "No matches found", ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? debugInfo : ""));
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, errorMessage);
                var errorResult = new TaxxorReturnMessage(false, errorMessage, ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : ""));

                return errorResult;
            }
        }


    }
}