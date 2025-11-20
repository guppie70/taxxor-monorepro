using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves a file list of data files for this project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveCmsDataFiles(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;



            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            try
            {
                // Retrieve all the datareferences from the Taxxor Project Data Store
                var dataReferences = await RetrieveDataReferences(projectVars.projectId);


                // Sort the list
                dataReferences.Sort();

                // Generate JSON string
                var jsonToReturn = ConvertToJson(dataReferences);

                // Render the response
                await response.OK(jsonToReturn, reqVars.returnType, true);
            }
            catch (Exception ex)
            {
                HandleError(reqVars.returnType, "Could not load project data metadata content from the Taxxor Document Store", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

        /// <summary>
        /// Wraps date references in special wrapper span element that includes metadata
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task CreateDynamicDateWrappersSingleDataFile(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var checkReportingPeriod = false;

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var lang = request.RetrievePostedValue("contentlanguage", @"^[a-z]{2,3}$", false, ReturnTypeEnum.Json, "all");
            string? reportingPeriod = request.RetrievePostedValue("reportingperiod", RegexEnum.Default, true, ReturnTypeEnum.Json);
            var dataReferencePosted = request.RetrievePostedValue("datareference", RegexEnum.Default, true, ReturnTypeEnum.Json);

            if (reportingPeriod == "none") reportingPeriod = null;

            var dataReferences = new List<string>();
            if (dataReferencePosted == "all")
            {
                // - Use the cache of the Taxxor Document Store to loop through all the content files
                dataReferences = await RetrieveDataReferences(projectVars.projectId);
                Console.WriteLine($"- dataReferences: {string.Join(", ", dataReferences)}");
                HandleError("Routine only works with individual files");
            }
            else
            {
                dataReferences.Add(dataReferencePosted);
            }


            var results = new Dictionary<string, string>();
            foreach (var dataReference in dataReferences)
            {
                var result = "";

                //
                // => Load the data file from the Taxxor Document Store
                //
                if (debugRoutine) appLogger.LogInformation($"Loading data with reference: '{dataReference}'");

                var xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{dataReference}", "cmsdataroot", true, false);

                if (XmlContainsError(xmlFilingContentSourceData))
                {
                    result = $"ERROR: Could not load data. dataReference: {dataReference}, response: {xmlFilingContentSourceData.OuterXml}";
                    appLogger.LogError(result);
                    results.Add(dataReference, result);
                }
                else
                {
                    // Check if the period that was passed is different than the period that was passed
                    var nodeSpecialHeader = xmlFilingContentSourceData.SelectSingleNode("//h2[@class='reportingperiod']");
                    var currentReportingPeriod = nodeSpecialHeader.GetAttribute("data-reportingperiod");
                    if (currentReportingPeriod != reportingPeriod && checkReportingPeriod)
                    {
                        result = $"WARNING: The data file is already in reporting period {reportingPeriod}, reprtingPeriodPassed: {reportingPeriod}, reportingPeriodFromFile: {currentReportingPeriod}";
                        appLogger.LogWarning(result);
                        results.Add(dataReference, result);
                    }
                    else
                    {
                        //
                        // => Create the wrappers
                        //
                        var markDatesResult = MarkDatesInTableCells(projectVars.projectId, xmlFilingContentSourceData, lang, true, reportingPeriod);


                        //
                        // => Handle result
                        //
                        var debugResult = "";
                        var errorResult = "";

                        // Log some content if needed
                        if (markDatesResult.DebugContent.Length > 0)
                        {
                            char[] trimChars = { ',', ' ' };
                            debugResult = markDatesResult.DebugContent.ToString().TrimEnd(trimChars);
                            appLogger.LogInformation(debugResult);
                        }

                        if (markDatesResult.ErrorContent.Length > 0)
                        {
                            appLogger.LogError("!! Errors detected during period processing !!");
                            errorResult = markDatesResult.ErrorContent.ToString();
                            appLogger.LogError(errorResult);
                        }

                        // Replace the content of the Data XML file with the content containing the date marks
                        xmlFilingContentSourceData.LoadXml(markDatesResult.XmlData.OuterXml);

                        //
                        // => Fill the header with the reporting period that was used in the move dates routine
                        //
                        nodeSpecialHeader = xmlFilingContentSourceData.SelectSingleNode("//h2[@class='reportingperiod']");
                        if (nodeSpecialHeader != null)
                        {
                            nodeSpecialHeader.InnerText = $"Reporting period: {reportingPeriod}";
                            nodeSpecialHeader.SetAttribute("data-reportingperiod", reportingPeriod);
                        }

                        //
                        // => Save the result on the Taxxor Document Store
                        //

                        var successSave = await DocumentStoreService.FilingData.Save<bool>(xmlFilingContentSourceData.OuterXml, $"/textual/{dataReference}", "cmsdataroot", true, false);

                        if (successSave)
                        {
                            //
                            // => Clear the paged media cache
                            // 
                            ContentCache.Clear(projectVars.projectId);

                            result = $"Successfully replaced date wrappers in {dataReference}";
                            results.Add(dataReference, result);
                        }
                        else
                        {
                            result = $"ERROR: Could not save the updated content on the server (dataReference: {dataReference})";
                            appLogger.LogError(result);
                            results.Add(dataReference, result);
                        }
                    }
                }

            }

            var shiftResults = new TaxxorReturnMessage(true, "Successfully eplaced date wrappers", System.Text.Json.JsonSerializer.Serialize(results), $"reportingPeriod: {reportingPeriod}");

            await response.OK(shiftResults, ReturnTypeEnum.Json, true);
        }


        /// <summary>
        /// Shift the dates inside the special wrappers to reflect the new reporting period passed for a single data file
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ShiftDatesSingleDataFile(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var checkReportingPeriod = false;

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var lang = request.RetrievePostedValue("contentlanguage", @"^[a-z]{2,3}$", false, ReturnTypeEnum.Json, "all");
            var reportingPeriod = request.RetrievePostedValue("reportingperiod", RegexEnum.Default, true, ReturnTypeEnum.Json);
            var dataReferencePosted = request.RetrievePostedValue("datareference", RegexEnum.Default, true, ReturnTypeEnum.Json);
            // if (dataReference == "all") HandleError("Routine only works with individual files");

            var dataReferences = new List<string>();
            if (dataReferencePosted == "all")
            {
                // - Use the cache of the Taxxor Document Store to loop through all the content files
                dataReferences = await RetrieveDataReferences(projectVars.projectId);
            }
            else
            {
                dataReferences.Add(dataReferencePosted);
            }

            var results = new Dictionary<string, string>();
            foreach (var dataReference in dataReferences)
            {
                var result = "";

                //
                // => Load the data file from the Taxxor Document Store
                //
                if (debugRoutine) appLogger.LogInformation($"Loading data with reference: '{dataReference}'");

                var xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{dataReference}", "cmsdataroot", true, false);

                if (XmlContainsError(xmlFilingContentSourceData))
                {
                    result = $"ERROR: Could not load data. dataReference: {dataReference}, response: {xmlFilingContentSourceData.OuterXml}";
                    appLogger.LogError(result);
                    results.Add(dataReference, result);
                }
                else
                {
                    // Check if the period that was passed is different than the period that was passed
                    var nodeSpecialHeader = xmlFilingContentSourceData.SelectSingleNode("//h2[@class='reportingperiod']");
                    var currentReportingPeriod = nodeSpecialHeader.GetAttribute("data-reportingperiod");
                    if (currentReportingPeriod != reportingPeriod && checkReportingPeriod)
                    {
                        result = $"WARNING: The data file is already in reporting period {reportingPeriod}, reprtingPeriodPassed: {reportingPeriod}, reportingPeriodFromFile: {currentReportingPeriod}";
                        appLogger.LogWarning(result);
                        results.Add(dataReference, result);
                    }
                    else
                    {
                        //
                        // => Create the wrappers
                        //
                        var xmlFilingContentShiftedDates = MoveDatesInTableCells(projectVars.projectId, xmlFilingContentSourceData, lang, reportingPeriod);

                        //
                        // => Fill the header with the reporting period that was used in the move dates routine
                        //
                        nodeSpecialHeader = xmlFilingContentShiftedDates.SelectSingleNode("//h2[@class='reportingperiod']");
                        if (nodeSpecialHeader != null)
                        {
                            nodeSpecialHeader.InnerText = $"Reporting period: {reportingPeriod}";
                            nodeSpecialHeader.SetAttribute("data-reportingperiod", reportingPeriod);
                        }


                        //
                        // => Handle result
                        //
                        if (XmlContainsError(xmlFilingContentShiftedDates))
                        {
                            result = $"ERROR: Could not move dates. dataReference: {dataReference}, response: {xmlFilingContentShiftedDates.OuterXml}";
                            appLogger.LogError(result);
                            results.Add(dataReference, result);
                        }
                        else
                        {
                            // Replace the content of the Data XML file with the content containing the date marks
                            xmlFilingContentSourceData.LoadXml(xmlFilingContentShiftedDates.OuterXml);

                            //
                            // => Save the result on the Taxxor Document Store
                            //

                            var successSave = await DocumentStoreService.FilingData.Save<bool>(xmlFilingContentSourceData.OuterXml, $"/textual/{dataReference}", "cmsdataroot", true, false);

                            if (successSave)
                            {
                                //
                                // => Clear the paged media cache
                                // 
                                ContentCache.Clear(projectVars.projectId);

                                result = $"Successfully shifted dates in {dataReference}";
                                results.Add(dataReference, result);
                            }
                            else
                            {

                                result = $"ERROR: Could not save the updated content on the server (dataReference: {dataReference})";
                                appLogger.LogError(result);
                                results.Add(dataReference, result);
                            }
                        }
                    }

                }


            }

            var shiftResults = new TaxxorReturnMessage(true, "Successfully shifted dynamic dates", System.Text.Json.JsonSerializer.Serialize(results), $"reportingPeriod: {reportingPeriod}");

            await response.OK(shiftResults, ReturnTypeEnum.Json, true);
        }

        /// <summary>
        /// Shifts the periods for the Structured Data Elements used in a single content data file
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ShiftStructuredDataPeriodsSingleDataFile(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var watch = System.Diagnostics.Stopwatch.StartNew();


            var lang = request.RetrievePostedValue("contentlanguage", @"^[a-z]{2,3}$", false, ReturnTypeEnum.Json, "all");
            var reportingPeriod = request.RetrievePostedValue("reportingperiod", RegexEnum.Default, true, ReturnTypeEnum.Json);
            var dataReference = request.RetrievePostedValue("datareference", RegexEnum.Default, true, ReturnTypeEnum.Json);

            var reportingPeriodBase = request.RetrievePostedValue("reportingperiodbase", RegexEnum.Default, true, ReturnTypeEnum.Json);
            var dateShiftMethod = request.RetrievePostedValue("dateshiftmethod", @"^matchtabledates|relativetobaseperiod$", true, ReturnTypeEnum.Json);
            var dateShiftScope = request.RetrievePostedValue("dateshiftscope", @"^tables|text|textandtables|textandmarkedtables|markedtables$", false, ReturnTypeEnum.Json, "tables");

            var baseDebugInfo = $"lang: {lang}, reportingPeriod: {reportingPeriod}, dataReference: {dataReference}, reportingPeriodBase: {reportingPeriodBase}, dateShiftMethod: {dateShiftMethod}, dateShiftScope: {dateShiftScope}";

            try
            {
                TaxxorReturnMessage dateShiftResult;

                switch (dateShiftMethod)
                {
                    case "matchtabledates":
                        dateShiftResult = await ShiftStructuredDataPeriodsSingleDataFile(projectVars.projectId, dataReference, reportingPeriod, lang, dateShiftMethod, "", "", reqVars, projectVars, debugRoutine);
                        break;

                    case "relativetobaseperiod":
                        // TODO: needs to include the scope of what we want to shift (tables and/or text)
                        dateShiftResult = await ShiftStructuredDataPeriodsSingleDataFile(projectVars.projectId, dataReference, reportingPeriod, lang, dateShiftMethod, reportingPeriodBase, dateShiftScope, reqVars, projectVars, debugRoutine);
                        break;

                    default:
                        dateShiftResult = new TaxxorReturnMessage(false, "Dateshift method not supported", baseDebugInfo);
                        break;
                }

                if (dateShiftResult.Success)
                {
                    //
                    // => Retrieve the mapping clusters that we have shifted so that we can inspect those
                    //

                    //
                    // => Load the data file from the Taxxor Document Store
                    //
                    if (debugRoutine) appLogger.LogInformation($"Loading data with reference: '{dataReference}'");
                    XmlDocument xmlFilingContentSourceData;
                    if (dataReference == "all")
                    {
                        xmlFilingContentSourceData = await DocumentStoreService.FilingData.LoadAll<XmlDocument>(projectVars.projectId, "all", false, debugRoutine, false);
                    }
                    else
                    {
                        xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{dataReference}", "cmsdataroot", true, false);
                    }

                    if (XmlContainsError(xmlFilingContentSourceData))
                    {
                        appLogger.LogError($"Could not load data. dataReference: {dataReference}, response: {xmlFilingContentSourceData.OuterXml}");
                        await response.Error(xmlFilingContentSourceData, ReturnTypeEnum.Json);
                    }
                    else
                    {
                        //
                        // => Retrieve the mapping clusters that we have moved
                        //
                        var xmlMappingClusters = new XmlDocument();
                        xmlMappingClusters.LoadXml("<clusters/>");

                        var factGuids = new List<string>();
                        var nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlFilingContentSourceData, false, lang);
                        foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                        {
                            var factGuid = nodeStructuredDataElement.GetAttribute("data-fact-id");
                            if (!factGuids.Contains(factGuid)) factGuids.Add(factGuid);
                        }

                        var xmlMappingCluster = await MappingService.RetrieveMappingInformation(factGuids, projectVars.projectId, false);
                        xmlMappingClusters.DocumentElement.AppendChild(xmlMappingClusters.ImportNode(xmlMappingCluster.DocumentElement, true));
                        Console.WriteLine("* Successfully retrieved all the mapping clusters");

                        //
                        // => Create the message we want to return
                        //
                        var messageToReturn = dateShiftResult.Message;
                        var debugInformationToReturn = $"mappingclusters: {HtmlEncodeForDisplay(PrettyPrintXml(xmlMappingClusters))}\nreportingPeriod: {reportingPeriod}\ndataReference: {dataReference}";

                        //
                        // Check if there were errors or warnings during the date shift process
                        //
                        var xmlSdePeriodShiftDetails = new XmlDocument();
                        xmlSdePeriodShiftDetails.ReplaceContent(dateShiftResult.XmlPayload);

                        var nodeDateShiftLog = xmlSdePeriodShiftDetails.SelectSingleNode("/result/dateshift//log");
                        if (nodeDateShiftLog != null)
                        {
                            messageToReturn = "Successfully shifted some SDE's, but there were ";
                            var nodeDateShiftWarnings = xmlSdePeriodShiftDetails.SelectSingleNode("//warnings");
                            if (nodeDateShiftWarnings != null)
                            {
                                messageToReturn += $"{nodeDateShiftWarnings.GetAttribute("count")} warnings";
                            }

                            var nodeDateShiftErrors = xmlSdePeriodShiftDetails.SelectSingleNode("//errors");
                            if (nodeDateShiftErrors != null)
                            {
                                if (nodeDateShiftWarnings != null) messageToReturn += " and ";
                                messageToReturn += $"{nodeDateShiftErrors.GetAttribute("count")} errors";
                            }

                            messageToReturn += " during the process";

                            debugInformationToReturn = $"log: {HtmlEncodeForDisplay(PrettyPrintXml(nodeDateShiftLog.OuterXml))}\n{debugInformationToReturn}";
                        }

                        //
                        // => Clear the paged media cache
                        // 
                        ContentCache.Clear(projectVars.projectId);

                        watch.Stop();

                        await response.OK(GenerateSuccessXml($"{messageToReturn}\n\n{debugInformationToReturn}", $"Shifting dates for {dataReference} took: {watch.ElapsedMilliseconds.ToString()} ms"), ReturnTypeEnum.Json, true);
                    }
                }
                else
                {
                    HandleError(dateShiftResult);
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was a problem shifting the SDE dates");
                HandleError("There was a problem shifting the SDE dates", $"error: {ex}, {baseDebugInfo}");
            }
            finally
            {
                if (watch.IsRunning) watch.Stop();
            }

        }


        /// <summary>
        /// Shifts the periods for the Structured Data Elements used in a single content data file
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="dataReference"></param>
        /// <param name="reportingPeriod"></param>
        /// <param name="lang"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public async static Task<TaxxorReturnMessage> ShiftStructuredDataPeriodsSingleDataFile(string projectId, string dataReference, string reportingPeriod, string lang, string dateShiftMethod, string reportingPeriodBase, string shiftScope, RequestVariables reqVars, ProjectVariables projectVars, bool debugRoutine = false)
        {

            var baseDebugInfo = $"projectId: {projectId}, lang: {lang}, reportingPeriod: {reportingPeriod}, dataReference: {dataReference}, reportingPeriodBase: {reportingPeriodBase}, dateShiftMethod: {dateShiftMethod}, shiftScope: {shiftScope}";

            try
            {
                //
                // => Load the data file from the Taxxor Document Store
                //
                var xmlOverallResults = new XmlDocument();
                xmlOverallResults.LoadXml("<result><dateshift/><cachecreate/></result>");
                if (debugRoutine) appLogger.LogInformation($"Loading data with reference: '{dataReference}'");
                XmlDocument xmlFilingContentSourceData;
                if (dataReference == "all")
                {
                    xmlFilingContentSourceData = await DocumentStoreService.FilingData.LoadAll<XmlDocument>(projectId, "all", false, debugRoutine, false);
                }
                else
                {
                    xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{dataReference}", "cmsdataroot", true, false);
                }

                if (XmlContainsError(xmlFilingContentSourceData))
                {
                    return new TaxxorReturnMessage(false, $"Could not load data. dataReference: {dataReference}", $"data service response: {xmlFilingContentSourceData.OuterXml}");
                }
                else
                {
                    var xmlSdePeriodShiftDetails = new XmlDocument();

                    //
                    // => Move the periods for the SDE's in the file
                    //
                    TaxxorReturnMessage moveSdePeriodResults;
                    switch (dateShiftMethod)
                    {
                        case "matchtabledates":
                            moveSdePeriodResults = await MovePeriodsForStructuredDataElementsUsingTableInformation(projectId, xmlFilingContentSourceData, lang, reportingPeriod);
                            break;

                        case "relativetobaseperiod":
                            moveSdePeriodResults = await MovePeriodsForStructuredDataElementsUsingBaseReportingPeriod(projectId, xmlFilingContentSourceData, lang, reportingPeriodBase, reportingPeriod, shiftScope);
                            break;

                        default:
                            moveSdePeriodResults = new TaxxorReturnMessage(false, "Dateshift method not supported", baseDebugInfo);
                            break;
                    }


                    //
                    // => Handle result
                    //
                    if (!moveSdePeriodResults.Success)
                    {
                        return moveSdePeriodResults;
                    }
                    else
                    {
                        if (debugRoutine) Console.WriteLine($"* {moveSdePeriodResults.Message}");

                        //
                        // => Parse the result of the SDE shift 
                        //
                        xmlSdePeriodShiftDetails.ReplaceContent(moveSdePeriodResults.XmlPayload);
                        xmlOverallResults.SelectSingleNode("/result/dateshift").InnerXml = xmlSdePeriodShiftDetails.DocumentElement.InnerXml;

                        var successSave = true;
                        if (dataReference != "all")
                        {
                            //
                            // => Fill the header with the reporting period that was used in the move dates routine
                            //
                            var nodeSpecialHeader = xmlFilingContentSourceData.SelectSingleNode("//h2[@class='reportingperiod']");
                            if (nodeSpecialHeader != null)
                            {
                                nodeSpecialHeader.InnerText = $"Reporting period: {reportingPeriod}";
                                nodeSpecialHeader.SetAttribute("data-reportingperiod", reportingPeriod);
                            }

                            //
                            // => Save the result on the Taxxor Document Store
                            //
                            successSave = await DocumentStoreService.FilingData.Save<bool>(xmlFilingContentSourceData.OuterXml, $"/textual/{dataReference}", "cmsdataroot", true, false);

                        }

                        if (successSave)
                        {
                            if (debugRoutine && dataReference != "all") Console.WriteLine("* Successfully saved data file");

                            //
                            // => Rebuild the cache for the structured data elements used in this page
                            //
                            if (dataReference == "all")
                            {
                                var syncSdeResult = await SyncStructuredDataElementsPerDataReference(projectId, "1", false, false, false, false, null, reqVars, projectVars);

                                if (!syncSdeResult.Success)
                                {
                                    return syncSdeResult;
                                }

                                //
                                // => Parse the result of the cache re-create process
                                //
                                xmlOverallResults.SelectSingleNode("/result/cachecreate").InnerText = syncSdeResult.Payload;
                            }
                            else
                            {
                                var dataToPost = new Dictionary<string, string>
                                {
                                    { "projectid", projectId },
                                    { "snapshotid", "1" },
                                    { "dataref", dataReference }
                                };

                                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorsyncstructureddatasnapshotperdataref", dataToPost, true);
                                if (XmlContainsError(xmlResponse))
                                {
                                    return new TaxxorReturnMessage(xmlResponse);
                                }

                                //
                                // => Parse the result of the cache re-create process
                                //
                                xmlOverallResults.SelectSingleNode("/result/cachecreate").InnerXml = xmlResponse.DocumentElement.InnerXml;
                            }

                            if (debugRoutine) Console.WriteLine("* Successfully regenerated cache file(s)");


                            var messageToReturn = $"Successfully shifted the periods of the structured data elements in in {dataReference}. {moveSdePeriodResults.Message}";
                            var debugInformationToReturn = $"reportingPeriod: {reportingPeriod}\ndataReference: {dataReference}";

                            //
                            // Check if there were errors or warnings during the date shift process
                            //
                            var nodeDateShiftLog = xmlSdePeriodShiftDetails.SelectSingleNode("//log");
                            if (nodeDateShiftLog != null)
                            {
                                messageToReturn = "Successfully shifted some SDE's, but there were ";
                                var nodeDateShiftWarnings = xmlSdePeriodShiftDetails.SelectSingleNode("//warnings");
                                if (nodeDateShiftWarnings != null)
                                {
                                    messageToReturn += $"{nodeDateShiftWarnings.GetAttribute("count")} warnings";
                                }

                                var nodeDateShiftErrors = xmlSdePeriodShiftDetails.SelectSingleNode("//errors");
                                if (nodeDateShiftErrors != null)
                                {
                                    if (nodeDateShiftWarnings != null) messageToReturn += " and ";
                                    messageToReturn += $"{nodeDateShiftErrors.GetAttribute("count")} errors";
                                }

                                messageToReturn += " during the process";

                                debugInformationToReturn = $"log: {HtmlEncodeForDisplay(PrettyPrintXml(nodeDateShiftLog.OuterXml))}\n{debugInformationToReturn}";
                            }

                            return new TaxxorReturnMessage(true, messageToReturn, xmlOverallResults, debugInformationToReturn);

                        }
                        else
                        {
                            return new TaxxorReturnMessage(false, "Could not save the updated content on the server", baseDebugInfo);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error shifting the SDE elements", $"{baseDebugInfo}, error: {ex}");
            }
        }
    }

}