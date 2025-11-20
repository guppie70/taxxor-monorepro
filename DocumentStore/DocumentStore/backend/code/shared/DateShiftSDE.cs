using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities for shifting Structured Data Elements (SDE's)
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Shifts the dates for Structured Data Elements used in this project so that they match with the chosen reporting period for the current project
        /// Moves the dates based on the base reporting period passed to this function
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <param name="lang"></param>
        /// <param name="reportingPeriodBase"></param>
        /// <param name="reportingPeriodCurrent"></param>
        /// <param name="shiftScope"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> MovePeriodsForStructuredDataElementsUsingBaseReportingPeriod(string projectId, XmlDocument xmlFilingContentSourceData, string lang, string reportingPeriodBase, string reportingPeriodCurrent = null, string shiftScope = "tables")
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var baseDebugInfo = $"projectId: {projectId}, lang: {lang}, reportingPeriodBase: {reportingPeriodBase}, reportingPeriodCurrent: {reportingPeriodCurrent}, shiftScope: {shiftScope}";

            // For storing debugging information
            var debugBuilder = new StringBuilder();
            var warningBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            int sdeShifted = 0;
            var factIdDebug = "d41e1f9e-8bd7-43ee-a299-92e531ab21fd";

            var reportingPeriodBaseMetadata = new ProjectPeriodProperties(projectId, reportingPeriodBase);
            if (!reportingPeriodBaseMetadata.Success)
            {
                return new TaxxorReturnMessage(false, "Error retrieving base project metadata", $"{baseDebugInfo}");
            }
            var reportingPeriodBaseYear = reportingPeriodBaseMetadata.CurrentProjectYear;

            var reportingPeriodCurrentMetadata = new ProjectPeriodProperties(projectId, reportingPeriodCurrent);
            if (!reportingPeriodCurrentMetadata.Success)
            {
                return new TaxxorReturnMessage(false, "Error retrieving current project metadata", $"{baseDebugInfo}");
            }
            var reportingPeriodCurrentYear = reportingPeriodCurrentMetadata.CurrentProjectYear;

            // return new TaxxorReturnMessage(false,"Thrown on purpose", $"{baseDebugInfo}");

            var factInfoCool = new Dictionary<string,
                (string Context, string TableId, string ArticleId, string SdeProcessingInstruction)>();

            try
            {
                //
                // => Find the fact id's in the source data
                //
                XmlNodeList? nodeListStructuredDataElements = null;
                switch (shiftScope)
                {
                    case "tables":
                        var tablesXpath = "//table[not(@data-sdedateprocessinginstruction='shiftrelativetobaseperiod')]";
                        if (lang.ToLower() != "all")
                        {
                            if (xmlFilingContentSourceData.SelectSingleNode("/data/content") != null) tablesXpath = $"/data/content[@lang='{lang}']/*{tablesXpath}";
                        }

                        var sdeTablesXpath = $"{tablesXpath}{_retrieveStructuredDataElementsBaseXpath()}";
                        nodeListStructuredDataElements = xmlFilingContentSourceData.SelectNodes(sdeTablesXpath);
                        appLogger.LogInformation($"* shiftScope: {shiftScope}, sdeTablesXpath: {sdeTablesXpath}, nodeListStructuredDataElements.Count: {nodeListStructuredDataElements.Count}");
                        break;


                    case "textandmarkedtables":
                    case "markedtables":
                        var markedTablesXpath = "//table[@data-sdedateprocessinginstruction='shiftrelativetobaseperiod']";
                        if (lang.ToLower() != "all")
                        {
                            if (xmlFilingContentSourceData.SelectSingleNode("/data/content") != null) markedTablesXpath = $"/data/content[@lang='{lang}']/*{markedTablesXpath}";
                        }

                        var sdeMarkedTablesXpath = $"{markedTablesXpath}{_retrieveStructuredDataElementsBaseXpath()}";
                        nodeListStructuredDataElements = xmlFilingContentSourceData.SelectNodes(sdeMarkedTablesXpath);
                        appLogger.LogInformation($"* shiftScope: {shiftScope}, sdeMarkedTablesXpath: {sdeMarkedTablesXpath}, nodeListStructuredDataElements.Count: {nodeListStructuredDataElements.Count}");
                        break;


                    case "text":
                        var xPathSdesInText = RenderSdeTextXpath();
                        nodeListStructuredDataElements = xmlFilingContentSourceData.SelectNodes(xPathSdesInText);
                        appLogger.LogInformation($"* shiftScope: {shiftScope}, xPathSdesInText: {xPathSdesInText}, nodeListStructuredDataElements.Count: {nodeListStructuredDataElements.Count}");
                        break;

                    case "textandtables":
                        nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlFilingContentSourceData, false);
                        appLogger.LogInformation($"* shiftScope: {shiftScope}, nodeListStructuredDataElements.Count: {nodeListStructuredDataElements.Count}");
                        break;
                    default:
                        return new TaxxorReturnMessage(false, "Dateshift scope not supported", baseDebugInfo);
                }

                var factIds = new List<string>();
                foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                {
                    var factId = nodeStructuredDataElement.GetAttribute("data-fact-id").Replace("\n", "");
                    if (!factIds.Contains(factId))
                    {
                        factIds.Add(factId);
                    }
                }

                // - append the text SDE's to the list if we need to process text and marked tables
                if (shiftScope == "textandmarkedtables")
                {
                    var xPathSdesInText = RenderSdeTextXpath();
                    nodeListStructuredDataElements = xmlFilingContentSourceData.SelectNodes(xPathSdesInText);
                    appLogger.LogInformation($"* (add text SDEs) shiftScope: {shiftScope}, xPathSdesInText: {xPathSdesInText}, nodeListStructuredDataElements.Count: {nodeListStructuredDataElements.Count}");
                    foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                    {
                        var factId = nodeStructuredDataElement.GetAttribute("data-fact-id").Replace("\n", "");
                        if (!factIds.Contains(factId)) factIds.Add(factId);
                    }
                }

                //
                // => Create a dictionary with context information about the SDE facts
                //

                foreach (var factId in factIds)
                {
                    var nodeSde = xmlFilingContentSourceData.SelectSingleNode($"//*[@data-fact-id='{factId}']");
                    if (nodeSde != null)
                    {
                        string? articleIdentifier = nodeSde.SelectSingleNode("ancestor::article")?.GetAttribute("id");

                        string? tableIdentifier = nodeSde.SelectSingleNode("ancestor::table")?.GetAttribute("id");

                        var factContext = shiftScope;
                        if (shiftScope == "tables" || shiftScope == "textandtables" || shiftScope == "textandmarkedtables")
                        {
                            factContext = (tableIdentifier == null) ? "text" : "table";
                        }

                        var sdeProcessingInstruction = nodeSde.SelectSingleNode("ancestor::*[@data-sdedateprocessinginstruction]")?.GetAttribute("data-sdedateprocessinginstruction") ?? "";

                        factInfoCool.Add(factId, (Context: factContext, TableId: tableIdentifier, ArticleId: articleIdentifier, SdeProcessingInstruction: sdeProcessingInstruction));
                    }
                    else
                    {
                        appLogger.LogError($"Unable to locate SDE with factId {factId}");
                    }
                }

                //
                // => Retrieve the mapping information for the SDE's in scope
                //
                var xmlMappingInformation = await Taxxor.ConnectedServices.MappingService.RetrieveMappingInformation(factIds, projectId, false);

                if (!XmlContainsError(xmlMappingInformation))
                {

                    //
                    // => Loop through the mapping clusters we have received
                    //
                    var nodeListMappingClusters = xmlMappingInformation.SelectNodes("//mappingCluster");
                    foreach (XmlNode nodeMappingCluster in nodeListMappingClusters)
                    {
                        var factId = "";
                        var existingPeriod = "";
                        var newPeriod = "";
                        var isFixedDate = false;


                        var nodeInternalEntry = _getInternalEntry(nodeMappingCluster);

                        if (nodeInternalEntry != null)
                        {
                            existingPeriod = nodeInternalEntry.GetAttribute("period");
                            factId = nodeInternalEntry.SelectSingleNode("mapping")?.InnerText;

                            var isFixedDateString = nodeInternalEntry.GetAttribute("isAbsolute") ?? "false";
                            isFixedDate = (isFixedDateString == "true");
                        }
                        else
                        {
                            errorBuilder.AppendLine($"Could not find internal mapping entry in nodeInternalEntry: {nodeInternalEntry?.OuterXml ?? nodeMappingCluster?.OuterXml ?? "null"}.");

                            // Skip the rest of the processing
                            continue;
                        }

                        if (debugRoutine && factId == factIdDebug)
                        {
                            Console.WriteLine("-- ORGINAL CLUSTER --");
                            Console.WriteLine(PrettyPrintXml(nodeMappingCluster));
                        }

                        if (nodeMappingCluster.SelectNodes("entry").Count == 1)
                        {
                            errorBuilder.AppendLine($"Mapping cluster for {factId} contains 1 internal mapping and can therefore not be shifted");

                            // Skip the rest of the processing
                            continue;
                        }

                        if (factId == factIdDebug)
                        {
                            appLogger.LogInformation($"Debug fact ID {factIdDebug}");
                        }

                        // Trace information used in the logging
                        var baseDebugInformation = RenderFactDebugString(factId);
                        if (string.IsNullOrEmpty(baseDebugInformation)) continue;


                        if (isFixedDate)
                        {
                            // - This is a fixed date so we should not shift it
                            debugBuilder.AppendLine($"{factId} has a fixed date, so we will not shift it. {baseDebugInformation}");

                            // Skip the rest of the processing
                            continue;
                        }

                        //
                        // => Shift the period
                        //
                        newPeriod = MovePeriod(factId, existingPeriod);

                        if (newPeriod == existingPeriod)
                        {
                            // debugBuilder.AppendLine($"No need to store mapping cluster for {factId}, because the period {existingPeriod} has not been shifted. {baseDebugInformation}");

                            // Skip the rest of the processing
                            continue;
                        }
                        else
                        {
                            //
                            // => Create an updated mapping cluster with the new shifted period
                            //
                            var xmlUpdatedMappingCluster = new XmlDocument();

                            xmlUpdatedMappingCluster.ReplaceContent(nodeMappingCluster);

                            var nodeNewInternalEntry = _getInternalEntry(xmlUpdatedMappingCluster.DocumentElement, factId);
                            if (nodeNewInternalEntry != null)
                            {
                                nodeNewInternalEntry.SetAttribute("period", newPeriod);
                                xmlUpdatedMappingCluster.DocumentElement.SetAttribute("projectId", projectId);
                                xmlUpdatedMappingCluster.DocumentElement.RemoveAttribute("requestId");

                                var xmlUpdateResult = await Taxxor.ConnectedServices.MappingService.UpdateMappingEntry(xmlUpdatedMappingCluster, true);
                                if (XmlContainsError(xmlUpdateResult))
                                {
                                    errorBuilder.AppendLine($"Updating factId {factId} to period {newPeriod} failed. Error: {xmlUpdateResult?.OuterXml ?? "null"} ({baseDebugInformation})");
                                }
                                else
                                {
                                    sdeShifted++;
                                }

                                if (debugRoutine && factId == factIdDebug)
                                {
                                    Console.WriteLine("***************** UPDATE RESULT ********************");
                                    Console.WriteLine(PrettyPrintXml(xmlUpdateResult));
                                    Console.WriteLine("*************************************");
                                }

                                sdeShifted++;
                            }
                            else
                            {
                                errorBuilder.AppendLine($"Could not find internal entry node to update to period {newPeriod} ({baseDebugInformation})");
                            }

                        }
                    }
                }
                else
                {
                    appLogger.LogError($"There was an error retrieving the mapping information of the SDE's. stack-trace: {GetStackTrace()}");
                    return new TaxxorReturnMessage(xmlMappingInformation);
                }

                //
                // => Generate a result message
                //
                var xmlResultDetails = _generateSdeDateShiftResultDetailedReport(xmlFilingContentSourceData, debugBuilder, warningBuilder, errorBuilder, debugRoutine);

                return new TaxxorReturnMessage(true, $"Successfully shifted {sdeShifted} of {factIds.Count} SDE periods relative to a base period", xmlResultDetails, baseDebugInfo);
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was an error shifting the dates of the SDE elements", $"error: {ex}, {baseDebugInfo}");
            }



            /// <summary>
            /// Routine that moves a period date string forward
            /// </summary>
            /// <param name="factId"></param>
            /// <param name="basePeriod"></param>
            /// <returns></returns>
            string MovePeriod(string factId, string existingPeriod)
            {
                // Trace information used in the logging
                // string baseDebugInformation = RenderFactDebugString(factId) ?? "";

                // Parse the incoming period
                var parsedPeriod = ParseMappingClusterPeriod(existingPeriod);
                if (!parsedPeriod.Success)
                {
                    errorBuilder.AppendLine($"Unable to parse existing period {existingPeriod}");
                    return existingPeriod;
                }

                // Retrieve matadata for this fact
                string factContext;
                (string Context, string TableId, string ArticleId, string SdeProcessingInstruction) factInformation;
                if (!factInfoCool.TryGetValue(factId, out factInformation))
                {
                    errorBuilder.AppendLine($"Unable to find the metadata for fact {factId} with period {existingPeriod}");
                    return existingPeriod;
                }
                factContext = factInformation.Context;


                var newPeriod = existingPeriod;
                if (parsedPeriod.IsDuration)
                {
                    var movedStartDate = MovePeriodFragment(parsedPeriod.PeriodStart, factId, factContext, parsedPeriod.PeriodType, parsedPeriod.IsDuration, false);
                    var movedEndDate = MovePeriodFragment(parsedPeriod.PeriodEnd, factId, factContext, parsedPeriod.PeriodType, parsedPeriod.IsDuration, true);

                    newPeriod = $"{movedStartDate}_{movedEndDate}";

                    var (Success, IsDuration, PeriodType, PeriodStart, PeriodEnd) = ParseMappingClusterPeriod(newPeriod);

                    //
                    // => Test if the start date is earlier than the end date
                    //
                    var calculatedDataIsValid = (DateTime.Compare(PeriodStart, PeriodEnd) < 0);
                    if (!calculatedDataIsValid)
                    {
                        errorBuilder.AppendLine($"Could not update from '{existingPeriod}' to '{newPeriod}' as the new period is invalid  ({(RenderFactDebugString(factId) ?? "")})");
                        return existingPeriod;
                    }

                    //
                    // => Test if the calculated start and end dates are valid.
                    //
                    var calculatedEndDateBeyondProjectEndDate = (DateTime.Compare(PeriodEnd, reportingPeriodCurrentMetadata.PeriodEnd) > 0);
                    var calculatedStartDateBeyondProjectEndDate = (DateTime.Compare(PeriodStart, reportingPeriodCurrentMetadata.PeriodEnd) > 0);

                    //
                    // => Handle invalid dates
                    //
                    if (factInformation.SdeProcessingInstruction != "allowfutureperiods" && (calculatedEndDateBeyondProjectEndDate || calculatedStartDateBeyondProjectEndDate))
                    {
                        if (calculatedEndDateBeyondProjectEndDate && calculatedStartDateBeyondProjectEndDate)
                        {
                            errorBuilder.AppendLine($"Could not update from '{existingPeriod}' to '{newPeriod}' as the calculated start and end dates are beyond the project end date ({(RenderFactDebugString(factId) ?? "")})");
                        }
                        else if (calculatedEndDateBeyondProjectEndDate)
                        {
                            errorBuilder.AppendLine($"Could not update from '{existingPeriod}' to '{newPeriod}' as the calculated end date is beyond the project end date ({(RenderFactDebugString(factId) ?? "")})");
                        }
                        else if (calculatedStartDateBeyondProjectEndDate)
                        {
                            errorBuilder.AppendLine($"Could not update from '{existingPeriod}' to '{newPeriod}' as the calculated start date is beyond the project end date ({(RenderFactDebugString(factId) ?? "")})");
                        }
                        return existingPeriod;
                    }


                    // //
                    // // => Test if the calculated end date of the period is beyond the end date of period of the current project
                    // //

                    // if (calculatedEndDateBeyondProjectEndDate)
                    // {
                    //     warningBuilder.AppendLine($"Corrected the calculated end date '{newPeriod}' to '{movedStartDate}_{reportingPeriodCurrentMetadata.PeriodEnd.ToString("yyyyMMdd")}' because it lies beyond the project end date (existingPeriod: {existingPeriod}, {(RenderFactDebugString(factId) ?? "")})");
                    //     newPeriod = $"{movedStartDate}_{reportingPeriodCurrentMetadata.PeriodEnd.ToString("yyyyMMdd")}";
                    // }

                    // //
                    // // => Test if the calculated start date of the period is beyond the end date of period of the current project
                    // //

                    // if (calculatedStartDateBeyondProjectEndDate)
                    // {
                    //     var originalNewPeriod = newPeriod;
                    //     var endPeriod = newPeriod.SubstringAfter("_");

                    //     if ((newPeriodParsed.PeriodStart.Day == 1 && newPeriodParsed.PeriodStart.Month == 1) && (reportingPeriodCurrentMetadata.PeriodStart.Day == 1 && reportingPeriodCurrentMetadata.PeriodStart.Month == 1))
                    //     {
                    //         newPeriod = $"{reportingPeriodCurrentMetadata.PeriodStart.ToString("yyyyMMdd")}_{endPeriod}";
                    //     }
                    //     else
                    //     {
                    //         // Use the original date, but correct the year to the current year
                    //         var correctedStartDate = new DateTime(reportingPeriodCurrentMetadata.PeriodStart.Year, newPeriodParsed.PeriodStart.Month, newPeriodParsed.PeriodStart.Day);
                    //         if (DateTime.Compare(correctedStartDate, reportingPeriodCurrentMetadata.PeriodEnd) > 0)
                    //         {
                    //             correctedStartDate = new DateTime(reportingPeriodCurrentMetadata.PeriodStart.Year, reportingPeriodCurrentMetadata.PeriodStart.Month, reportingPeriodCurrentMetadata.PeriodStart.Day);
                    //         }

                    //         newPeriod = $"{correctedStartDate.ToString("yyyyMMdd")}_{endPeriod}";
                    //     }

                    //     warningBuilder.AppendLine($"Corrected the start calculated date '{originalNewPeriod}' to '{newPeriod}' because it lies beyond the project end date (existingPeriod: {existingPeriod}, {(RenderFactDebugString(factId) ?? "")})");
                    // }

                }
                else
                {
                    newPeriod = MovePeriodFragment(parsedPeriod.PeriodEnd, factId, factContext, parsedPeriod.PeriodType, parsedPeriod.IsDuration, true);

                    var (Success, IsDuration, PeriodType, PeriodStart, PeriodEnd) = ParseMappingClusterPeriod(newPeriod);

                    //
                    // => Test if the calculated end date of the period is beyond the end date of period of the current project
                    //
                    if (factInformation.SdeProcessingInstruction != "allowfutureperiods" && DateTime.Compare(PeriodEnd, reportingPeriodCurrentMetadata.PeriodEnd) > 0)
                    {
                        warningBuilder.AppendLine($"Corrected the calculated date '{newPeriod}' to '{reportingPeriodCurrentMetadata.PeriodEnd.ToString("yyyyMMdd")}' because it lies beyond the project end date (existingPeriod: {existingPeriod}, {(RenderFactDebugString(factId) ?? "")})");
                        newPeriod = reportingPeriodCurrentMetadata.PeriodEnd.ToString("yyyyMMdd");
                    }
                }

                if (debugRoutine && factId == factIdDebug)
                {
                    Console.WriteLine($"**** MOVED PERIOD ***");
                    Console.WriteLine($"existingPeriod: {existingPeriod}");
                    Console.WriteLine($"newPeriod:      {newPeriod}");
                    Console.WriteLine($"*********************");
                }

                return newPeriod;
            }

            /// <summary>
            /// Calculates a new period date based on the metadata of the current and source project and the metadata of the period itself
            /// </summary>
            /// <param name="dateFragment"></param>
            /// <param name="factContext"></param>
            /// <returns></returns>
            string MovePeriodFragment(DateTime dateFragment, string factId, string factContext, string periodType, bool isDuration, bool isPeriodEndDate)
            {
                DateTime dateFragmentMoved = new DateTime(dateFragment.Year, dateFragment.Month, dateFragment.Day);

                DateTime dateJustBeforePeriodStart = reportingPeriodBaseMetadata.PeriodStart.AddDays(-1);


                // Start dates in text content are assumed to be linked to the start of the period and not something custom or a YTD period // && (reportingPeriodCurrentMetadata.ProjectType == "qr" || reportingPeriodCurrentMetadata.ProjectType == "mr")
                if (isDuration && !isPeriodEndDate && factContext == "text" && dateFragment.Day == 1)
                {
                    dateFragmentMoved = new DateTime(dateFragment.Year, reportingPeriodCurrentMetadata.PeriodStart.Month, reportingPeriodCurrentMetadata.PeriodStart.Day);
                    dateFragmentMoved = dateFragmentMoved.AddYears(reportingPeriodCurrentYear - reportingPeriodBaseYear);
                }

                // Simple cases - jan 1 or dec 31 -> move the year
                else if ((dateFragment.Day == 1 && dateFragment.Month == 1) || (dateFragment.Day == 31 && dateFragment.Month == 12))
                {
                    dateFragmentMoved = dateFragmentMoved.AddYears(reportingPeriodCurrentYear - reportingPeriodBaseYear);
                }

                // Date matches the start date of the period
                else if (dateFragment.Day == reportingPeriodBaseMetadata.PeriodStart.Day && dateFragment.Month == reportingPeriodBaseMetadata.PeriodStart.Month)
                {
                    dateFragmentMoved = new DateTime(dateFragment.Year, reportingPeriodCurrentMetadata.PeriodStart.Month, reportingPeriodCurrentMetadata.PeriodStart.Day);
                    dateFragmentMoved = dateFragmentMoved.AddYears(reportingPeriodCurrentYear - reportingPeriodBaseYear);
                }

                // Date matches the end date of the period
                else if (dateFragment.Day == reportingPeriodBaseMetadata.PeriodEnd.Day && dateFragment.Month == reportingPeriodBaseMetadata.PeriodEnd.Month)
                {
                    dateFragmentMoved = new DateTime(dateFragment.Year, reportingPeriodCurrentMetadata.PeriodEnd.Month, reportingPeriodCurrentMetadata.PeriodEnd.Day);
                    dateFragmentMoved = dateFragmentMoved.AddYears(reportingPeriodCurrentYear - reportingPeriodBaseYear);
                }

                // Date matches the start date of the period minus one
                else if ((dateFragment - dateJustBeforePeriodStart).Days == 0)
                {
                    dateFragmentMoved = reportingPeriodCurrentMetadata.PeriodStart.AddDays(-1);
                }

                else
                {
                    dateFragmentMoved = dateFragmentMoved.AddYears(reportingPeriodCurrentYear - reportingPeriodBaseYear);
                    // debugBuilder.AppendLine($"No explicit support for period fragment '{dateFragment.ToString("yyyyMMdd")}' so we shifted the year to '{dateFragmentMoved.ToString("yyyyMMdd")}' ({(RenderFactDebugString(factId) ?? "")})");
                }

                return dateFragmentMoved.ToString("yyyyMMdd");
            }

            /// <summary>
            /// Renders an xpath to catch all the structured data elements used in text
            /// </summary>
            /// <returns></returns>
            string RenderSdeTextXpath()
            {
                var baseXpath = "";
                if (lang.ToLower() != "all")
                {
                    baseXpath = $"/data/content[@lang='{lang}']/*";

                    if (xmlFilingContentSourceData.SelectSingleNode("/data/content") == null) baseXpath = "";
                }
                var sdeTextXpath = $"{baseXpath}//span[@data-fact-id and not(ancestor::table) and not(contains(@class, 'xbrl-level-2'))]";
                return sdeTextXpath;
            }

            /// <summary>
            /// Renders a log/debug string to make it easier to find the SDE's in the document
            /// </summary>
            /// <param name="sdeId"></param>
            /// <returns></returns>
            string RenderFactDebugString(string sdeId)
            {
                // Retrieve metadata for this fact
                (string Context, string TableId, string ArticleId, string SdeProcessingInstruction) factInformation;
                if (!factInfoCool.TryGetValue(sdeId, out factInformation))
                {
                    errorBuilder.AppendLine($"Unable to find the metadata for fact {sdeId}");

                    // Skip the rest of the processing
                    return "";
                }

                // Trace information used in the logging
                StringBuilder debugInfo = new StringBuilder();
                debugInfo.Append($"factId: {sdeId}");
                if (!string.IsNullOrEmpty(factInformation.TableId)) debugInfo.Append($", tableId: {factInformation.TableId}");
                if (!string.IsNullOrEmpty(factInformation.ArticleId)) debugInfo.Append($", articleId: {factInformation.ArticleId}");
                if (!string.IsNullOrEmpty(factInformation.SdeProcessingInstruction)) debugInfo.Append($", sdeProcessingInstruction: {factInformation.SdeProcessingInstruction}");
                return debugInfo.ToString();
            }

        }


        /// <summary>
        /// Shifts the dates for Structured Data Elements used in this project so that they match with the chosen reporting period for the current project
        /// Uses the (dynamic) dates used in the table
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <param name="lang"></param>
        /// <param name="reportingPeriodOverrule"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> MovePeriodsForStructuredDataElementsUsingTableInformation(string projectId, XmlDocument xmlFilingContentSourceData, string lang, string reportingPeriodOverrule = null)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // For storing debugging information
            var debugBuilder = new StringBuilder();
            var warningMessage = "";
            var warningBuilder = new StringBuilder();
            var errorMessage = "";
            var errorBuilder = new StringBuilder();

            var debugAllFacts = false;
            var factIdDebug = new List<string>();
            factIdDebug.Add("bla");

            var baseDebugString = $"projectId: {projectId}, lang: {lang}";
            var sdeInvestigated = 0;
            int sdeShifted = 0;

            //
            // => Retrieve project period information
            //
            var projectPeriodMetadata = new ProjectPeriodProperties(projectId, reportingPeriodOverrule);
            if (!projectPeriodMetadata.Success)
            {
                return new TaxxorReturnMessage(false, "Error retrieving project metadata", $"{baseDebugString}");
            }


            var articleId = "";
            var tableId = "";

            //
            // => Find the tables in the document
            //
            try
            {
                var tablesXpath = "//table[not(@data-sdedateprocessinginstruction='shiftrelativetobaseperiod')]";
                if (lang.ToLower() != "all")
                {
                    tablesXpath = $"/data/content[@lang='{lang}']/*{tablesXpath}";
                }
                var nodeListTables = xmlFilingContentSourceData.SelectNodes(tablesXpath);
                foreach (XmlNode nodeTable in nodeListTables)
                {
                    tableId = nodeTable.GetAttribute("id");
                    articleId = nodeTable.SelectSingleNode("ancestor::article")?.GetAttribute("id") ?? "unknown";

                    // Grab the table head and body elements
                    var nodeTableHead = nodeTable.SelectSingleNode("thead");
                    var nodeTableBody = nodeTable.SelectSingleNode("tbody");

                    var wrapperClass = nodeTable.ParentNode.GetAttribute("class") ?? "";
                    var nodeListStructuredDataElements = nodeTableBody.SelectNodes(".//*[@data-fact-id and not(*)]");
                    var isStructuredDataTable = nodeListStructuredDataElements.Count > 0;
                    var sdeProcessingInstruction = nodeTable.GetAttribute("data-sdedateprocessinginstruction") ?? "";
                    debugBuilder.AppendLine($"Table ID: {tableId}");


                    if (nodeTableHead != null && nodeTableBody != null)
                    {
                        var continueParsingTable = false;

                        //
                        // => Parse the table head section to distill information about the context of the cells
                        //
                        var dateWrappers = new List<DateWrapperProperties>();
                        var nodeListTableHeaderRows = nodeTableHead.SelectNodes("tr[*//span[@data-dateperiodtype]]");
                        var tableBodyContainsContext = false;
                        var tableHeadContainsContext = (nodeListTableHeaderRows.Count > 0);
                        if (tableHeadContainsContext)
                        {
                            // - Work on this logic using a clone of the XML so that we do not influence the original
                            var xmlTableHead = new XmlDocument();
                            xmlTableHead.LoadXml(nodeTableHead.OuterXml);
                            nodeTableHead = xmlTableHead.DocumentElement;

                            nodeListTableHeaderRows = nodeTableHead.SelectNodes("tr");

                            // - Normalize the cells in the table head so that it's easier to loop through
                            // Phase 1 - deal with column spans
                            foreach (XmlNode nodeTableHeadRow in nodeListTableHeaderRows)
                            {
                                // Loop through the cells
                                var nodeListHeaderCells = nodeTableHeadRow.SelectNodes("th");
                                foreach (XmlNode nodeHeaderCell in nodeListHeaderCells)
                                {
                                    var colspanString = nodeHeaderCell.GetAttribute("colspan") ?? "1";
                                    if (!string.IsNullOrEmpty(colspanString))
                                    {
                                        int colspan = 1;
                                        if (!int.TryParse(colspanString, out colspan))
                                        {
                                            appLogger.LogTrace($"Could not parse colspan attribute value {colspanString}");
                                        }

                                        if (colspan > 1)
                                        {
                                            for (var i = 1; i < colspan; i++)
                                            {
                                                var nodeHeaderCellCloned = nodeHeaderCell.CloneNode(true);
                                                nodeTableHeadRow.InsertAfter(nodeHeaderCellCloned, nodeHeaderCell);
                                                nodeHeaderCellCloned.RemoveAttribute("colspan");

                                            }
                                        }

                                        nodeHeaderCell.RemoveAttribute("colspan");
                                    }

                                }

                            }

                            // Phase 2 - deal with row spans
                            while (nodeListTableHeaderRows.Item(0).SelectNodes("th[@rowspan]").Count > 0)
                            {
                                var nodeListHeaderCellsFirstRow = nodeListTableHeaderRows.Item(0).SelectNodes("th");
                                var nodeListHeaderCellsSecondRow = nodeListTableHeaderRows.Item(1).SelectNodes("th");
                                var headerCellCounter = 0;
                                foreach (XmlNode nodeHeaderCell in nodeListHeaderCellsFirstRow)
                                {
                                    var rowSpan = nodeHeaderCell.GetAttribute("rowspan") ?? "";
                                    Console.WriteLine($"* rowspan: {rowSpan}");
                                    if (rowSpan != "")
                                    {
                                        var nodeNewCell = xmlTableHead.CreateElementWithText("th", "foobar");
                                        var nodeReferenceCell = nodeListHeaderCellsSecondRow.Item(headerCellCounter);
                                        nodeReferenceCell.ParentNode.InsertBefore(nodeNewCell, nodeReferenceCell);
                                        nodeHeaderCell.RemoveAttribute("rowspan");
                                        break;
                                    }

                                    headerCellCounter++;
                                }
                            }


                            // Console.WriteLine("%%%% Normalized Table Head %%%%");
                            // Console.WriteLine(PrettyPrintXml(nodeTableHead.OuterXml));
                            // Console.WriteLine("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");

                            // return new TaxxorReturnMessage(false, "Thrown on purpose");


                            // - Parse the information of the table head into the headerDateWrappers list (including the context if provided)
                            var numberOfHeaderRows = nodeListTableHeaderRows.Count;

                            continueParsingTable = true;

                            var topRowNumberToInspect = 0;
                            var bottomRowNumberToInspect = 0;

                            // Find the table header rows that contain date information
                            if (numberOfHeaderRows > 1)
                            {
                                var topRowFound = false;
                                for (int headerRowCounter = 0; headerRowCounter < numberOfHeaderRows; headerRowCounter++)
                                {
                                    var node = nodeListTableHeaderRows.Item(headerRowCounter);
                                    if (!topRowFound)
                                    {
                                        if (node.SelectNodes("*//span[@data-dateperiodtype]").Count > 0)
                                        {
                                            topRowNumberToInspect = headerRowCounter;

                                            // - There might be two header rows involved to get the complete period context of the cells in the table
                                            if (headerRowCounter + 1 < numberOfHeaderRows)
                                            {
                                                bottomRowNumberToInspect = topRowNumberToInspect + 1;
                                            }
                                            topRowFound = true;
                                            break;
                                        }
                                    }
                                }

                                // topRowNumberToInspect = numberOfHeaderRows - 2;
                                // bottomRowNumberToInspect = numberOfHeaderRows - 1;
                            }


                            var nodeListMainContext = nodeListTableHeaderRows[bottomRowNumberToInspect].SelectNodes("th");
                            var nodeListSubContext = nodeListTableHeaderRows[topRowNumberToInspect].SelectNodes("th");

                            // In some cases, the main context is present in the top row
                            if (nodeListTableHeaderRows[bottomRowNumberToInspect].SelectNodes("*//span[@data-dateperiodtype]").Count == 0)
                            {
                                nodeListMainContext = nodeListTableHeaderRows[topRowNumberToInspect].SelectNodes("th");
                                nodeListSubContext = nodeListTableHeaderRows[bottomRowNumberToInspect].SelectNodes("th");
                            }

                            var mainContextPosition = 0;
                            foreach (XmlNode nodeMainContext in nodeListMainContext)
                            {
                                var nodeDateWrapper = nodeMainContext.SelectSingleNode(".//span[@data-dateperiodtype]");
                                if (nodeDateWrapper != null)
                                {
                                    var dateWrapperProperties = new DateWrapperProperties(nodeDateWrapper);
                                    if (numberOfHeaderRows > 1)
                                    {
                                        appLogger.LogInformation($"Need to parse sub context[1]. articleId: {articleId}, tableId: {tableId}");

                                        // Retrieve the corresponding node from the first row
                                        var nodeSubContext = nodeListSubContext.Item(mainContextPosition);
                                        if (nodeSubContext.InnerText.Trim() != "")
                                        {
                                            var nodeSubContextDateWrapper = nodeSubContext.SelectSingleNode(".//span[@data-dateperiodtype]");
                                            if (nodeSubContextDateWrapper != null)
                                            {
                                                dateWrapperProperties.Context = new DateWrapperProperties(nodeSubContextDateWrapper);
                                            }
                                            else
                                            {
                                                dateWrapperProperties.Context = new DateWrapperProperties(nodeSubContext.InnerText);
                                            }
                                        }

                                    }
                                    else
                                    {


                                    }

                                    // Calculate the period fragments
                                    var periodCreationResult = dateWrapperProperties.CalculatePeriodFragments(projectPeriodMetadata, nodeDateWrapper);
                                    if (!periodCreationResult.Success)
                                    {
                                        return periodCreationResult;
                                    }

                                    // Add to list
                                    dateWrappers.Add(dateWrapperProperties);
                                }
                                else
                                {
                                    // - Store the inner text of the header nodes in the wrapper
                                    var dateWrapperProperties = new DateWrapperProperties(nodeMainContext.InnerText);
                                    if (numberOfHeaderRows > 1)
                                    {
                                        appLogger.LogInformation($"Need to parse sub context[2]. articleId: {articleId}, tableId: {tableId}");

                                        // Retrieve the corresponding node from the first row
                                        var nodeSubContext = nodeListSubContext.Item(mainContextPosition);
                                        if (nodeSubContext.InnerText.Trim() != "")
                                        {
                                            dateWrapperProperties.Context = new DateWrapperProperties(nodeSubContext.InnerText);
                                        }

                                    }

                                    dateWrappers.Add(dateWrapperProperties);
                                }
                                mainContextPosition++;
                            }



                        }
                        else
                        {
                            // Test if we can find date wrapper elements in the body content
                            var nodeListDateWrappersTableBody = nodeTableBody.SelectNodes(".//span[@data-dateperiodtype]");
                            if (nodeListDateWrappersTableBody.Count > 0)
                            {
                                continueParsingTable = true;
                                tableBodyContainsContext = true;

                                foreach (XmlNode nodeDateWrapperTableBody in nodeListDateWrappersTableBody)
                                {
                                    var dateWrapperProperties = new DateWrapperProperties(nodeDateWrapperTableBody);

                                    // Calculate the period fragments
                                    var periodCreationResult = dateWrapperProperties.CalculatePeriodFragments(projectPeriodMetadata, nodeDateWrapperTableBody);
                                    if (!periodCreationResult.Success)
                                    {
                                        return periodCreationResult;
                                    }

                                    // Add to list
                                    dateWrappers.Add(dateWrapperProperties);
                                }
                            }

                        }

                        if (continueParsingTable && isStructuredDataTable)
                        {
                            if (tableHeadContainsContext || tableBodyContainsContext)
                            {
                                //
                                // => If the table head contains the context, then figure out if there are date fragments in the table body row headers (we do this once)
                                //
                                XmlNodeList? nodeListBodyRowHeaderDateWrappers = null;
                                var tableBodyRowHeaderCellsContainDynamicDates = false;
                                if (tableHeadContainsContext)
                                {
                                    nodeListBodyRowHeaderDateWrappers = nodeTableBody.SelectNodes("descendant-or-self::tr/td[1]//span[@class='txdynamicdate']");
                                    tableBodyRowHeaderCellsContainDynamicDates = nodeListBodyRowHeaderDateWrappers.Count > 0;
                                }


                                //
                                // => Find the mapping information for all the SDE elements in this table
                                //
                                

                                // - Generate a list of fact ids
                                var factIds = new List<string>();
                                foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                                {
                                    var factId = nodeStructuredDataElement.GetAttribute("data-fact-id");
                                    if (!string.IsNullOrEmpty(factId))
                                    {
                                        factIds.Add(factId);
                                    }
                                    else
                                    {
                                        appLogger.LogWarning("Could not retrieve a valid factID - 1");
                                    }
                                }
                                var xmlMappingInformation = await Taxxor.ConnectedServices.MappingService.RetrieveMappingInformation(factIds, projectId, false);

                                if (!XmlContainsError(xmlMappingInformation))
                                {

                                    //
                                    // => Find the rows in the table body that contain cells with SDE
                                    //
                                    var nodeListTableBodyRows = nodeTableBody.SelectNodes(".//tr[*//@data-fact-id]");
                                    var bodyRowCounter = 0;
                                    // appLogger.LogError($"Found {nodeListTableBodyRows.Count} rows containing SDE");

                                    foreach (XmlNode nodeTableBodyRow in nodeListTableBodyRows)
                                    {
                                        // - Variable that contains the row header
                                        var rowHeaderText = "";

                                        // Loop through all the cells in the row in order to properly match against headerDateWrappers list

                                        var bodyCellCounter = 0;
                                        var nodeListTableBodyRowCells = nodeTableBodyRow.SelectNodes("td");
                                        foreach (XmlNode nodeCell in nodeListTableBodyRowCells)
                                        {
                                            var isFixedDate = false;
                                            var colspanString = nodeCell.GetAttribute("colspan") ?? "1";
                                            int colspan = 1;

                                            if (!int.TryParse(colspanString, out colspan))
                                            {
                                                appLogger.LogTrace($"Could not parse colspan attribute value {colspanString}. articleId: {articleId}, tableId: {tableId}");
                                            }
                                            var nodeStructuredDataElement = nodeCell.SelectSingleNode(".//*[@data-fact-id and not(*)]");
                                            if (bodyCellCounter == 0 && nodeStructuredDataElement == null)
                                            {
                                                rowHeaderText = nodeCell.InnerText.ToLower();
                                            }

                                            if (nodeStructuredDataElement != null)
                                            {
                                                var nodeListSdeProcessingInstruction = nodeStructuredDataElement.SelectNodes("ancestor::*[@data-sdedateprocessinginstruction]");
                                                var sdeProcessingInstructionNested = (nodeListSdeProcessingInstruction.Count > 0) ? nodeListSdeProcessingInstruction[nodeListSdeProcessingInstruction.Count - 1]?.GetAttribute("data-sdedateprocessinginstruction") : "";

                                                var factId = nodeStructuredDataElement.GetAttribute("data-fact-id");

                                                // Remove attributes on the SDE if needed
                                                switch (sdeProcessingInstruction)
                                                {
                                                    case "hide-future-elements":
                                                        nodeStructuredDataElement.RemoveAttribute("data-hidevalue");
                                                        break;
                                                }

                                                if (!string.IsNullOrEmpty(factId))
                                                {
                                                    var existingPeriod = "";
                                                    var newPeriod = "";

                                                    // - To place a breakpoint...
                                                    if (factIdDebug.Contains(factId) || debugAllFacts)
                                                    {
                                                        appLogger.LogDebug($"Debug factId: {factId}");
                                                    }

                                                    //
                                                    // => Retrieve the mapping cluster for the current Structured Data Element
                                                    //
                                                    var xmlUpdatedMappingCluster = new XmlDocument();
                                                    var nodeMappingCluster = xmlMappingInformation.SelectSingleNode($"//mappingCluster[@requestId={GenerateEscapedXPathString(factId)}]");


                                                    if (nodeMappingCluster != null)
                                                    {
                                                        xmlUpdatedMappingCluster.LoadXml(nodeMappingCluster.OuterXml);

                                                        var nodeInternalEntry = _getInternalEntry(nodeMappingCluster, factId);

                                                        if (nodeInternalEntry != null)
                                                        {
                                                            // Console.WriteLine("###### INTERNAL MAPPING ######");
                                                            // Console.WriteLine(nodeInternalEntry.OuterXml);
                                                            // Console.WriteLine("##############################");

                                                            existingPeriod = nodeInternalEntry.GetAttribute("period");

                                                            var isFixedDateString = nodeInternalEntry.GetAttribute("isAbsolute") ?? "false";
                                                            isFixedDate = (isFixedDateString == "true");
                                                        }
                                                        else
                                                        {
                                                            warningBuilder.AppendLine($"Could not find internal mapping entry for {factId} in table ID {tableId}. (articleId: {articleId})");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        warningBuilder.AppendLine($"Could not find mapping cluster for {factId} in table ID {tableId}. (articleId: {articleId})");
                                                    }

                                                    if (isFixedDate)
                                                    {
                                                        // - This is a fixed date so we should not shift it
                                                        appLogger.LogInformation($"{factId} has a fixed date, so we will not shift it");
                                                    }
                                                    else
                                                    {
                                                        //
                                                        // => Get the datewrapper properties object that matches to the cell we are working with
                                                        //
                                                        DateWrapperProperties? dateWrapperProperties = null;
                                                        DateWrapperProperties? dateWrapperPropertiesNext = null;
                                                        if (tableHeadContainsContext)
                                                        {
                                                            // - The corresponding date wrapper object is in the same position as the cell is
                                                            dateWrapperProperties = dateWrappers[bodyCellCounter];

                                                            // - Potentially add the context of preceeding data markers in row header cells
                                                            if (tableBodyRowHeaderCellsContainDynamicDates)
                                                            {

                                                                if (nodeListBodyRowHeaderDateWrappers.Count == 2)
                                                                {
                                                                    // Assume that the row headers are redefining the start and end dates
                                                                    var nodeDateStart = nodeListBodyRowHeaderDateWrappers.Item(0);
                                                                    var nodeDateEnd = nodeListBodyRowHeaderDateWrappers.Item(1);

                                                                    var dateStartFormat = nodeDateStart.GetAttribute("data-dateformat") ?? "";
                                                                    var dateEndFormat = nodeDateEnd.GetAttribute("data-dateformat") ?? "";

                                                                    if (!string.IsNullOrEmpty(dateStartFormat) && !string.IsNullOrEmpty(dateEndFormat))
                                                                    {
                                                                        // Create a new date wrapper node that we can use for calculating the context
                                                                        var nodeDateCalculated = nodeDateEnd.CloneNode(true);

                                                                        var dateFormatsAreComparible = (dateStartFormat == dateEndFormat);
                                                                        if (!dateFormatsAreComparible)
                                                                        {
                                                                            if ((dateStartFormat == "MMMM d" && dateEndFormat == "MMM d") || (dateStartFormat == "MMM d" && dateEndFormat == "MMMM d")) dateFormatsAreComparible = true;
                                                                        }


                                                                        if (dateFormatsAreComparible)
                                                                        {
                                                                            switch (dateStartFormat)
                                                                            {
                                                                                case "MMM d":
                                                                                case "MMMM d":
                                                                                    nodeDateCalculated.SetAttribute("data-dateperiodtype", "runningperiod");
                                                                                    nodeDateCalculated.SetAttribute("data-dateformat", "MMMMdstart to MMMMdend");
                                                                                    nodeDateCalculated.SetAttribute("data-datestartday", nodeDateStart.GetAttribute("data-dateday") ?? "");
                                                                                    nodeDateCalculated.SetAttribute("data-datestartmonth", nodeDateStart.GetAttribute("data-datemonth") ?? "");
                                                                                    nodeDateCalculated.InnerText = $"{nodeDateStart.InnerText} to {nodeDateEnd.InnerText}";
                                                                                    break;


                                                                                default:
                                                                                    warningBuilder.AppendLine($"No support yet for defining context for format {dateStartFormat} for {factId} in table ID {tableId} which is not supported yet. (articleId: {articleId})");

                                                                                    break;
                                                                            }

                                                                            dateWrapperProperties.Context = new DateWrapperProperties(nodeDateCalculated);

                                                                            // Calculate the period fragments
                                                                            var periodCreationResult = dateWrapperProperties.CalculatePeriodFragments(projectPeriodMetadata, nodeDateCalculated);
                                                                            if (!periodCreationResult.Success)
                                                                            {
                                                                                errorBuilder.AppendLine($"Could not re-calculate period fragments for {factId} in table ID {tableId} based on row context {nodeDateStart.InnerText} to {nodeDateEnd.InnerText}. (articleId: {articleId})");
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            warningBuilder.AppendLine($"The start and end date formats are not the same for {factId} in table ID {tableId} which is not supported yet. (dateStartFormat: {dateStartFormat}, dateEndFormat: {dateEndFormat}, articleId: {articleId})");
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        warningBuilder.AppendLine($"Could not apply table row context for mapping cluster for {factId} in table ID {tableId} bacause one of the date formats is empty. (articleId: {articleId})");
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    // Stick the nearest marker in the context (and hope to use it later)
                                                                    var nodeListPrecedingDateWrappers = nodeTableBodyRow.SelectNodes("(self::tr[*//span[@data-dateperiodtype]]|preceding-sibling::tr[*//span[@data-dateperiodtype]])");
                                                                    var nodeNearestRowHeaderDynamicDate = nodeListPrecedingDateWrappers.Item(0).SelectSingleNode("descendant::span[@data-dateperiodtype]");
                                                                    dateWrapperProperties.Context = new DateWrapperProperties(nodeNearestRowHeaderDynamicDate);

                                                                    var periodCreationResult = dateWrapperProperties.CalculatePeriodFragments(projectPeriodMetadata, nodeNearestRowHeaderDynamicDate);
                                                                    if (!periodCreationResult.Success)
                                                                    {
                                                                        errorBuilder.AppendLine($"Could not re-calculate period fragments for {factId} in table ID {tableId} based on row context {nodeNearestRowHeaderDynamicDate.InnerText}. (articleId: {articleId})");
                                                                    }
                                                                }

                                                            }
                                                        }
                                                        else
                                                        {
                                                            // - Locate the datewrapper by looking up the number of preceeding date wrapper elements
                                                            var nodeListPrecedingDateWrappers = nodeTableBodyRow.SelectNodes("(self::tr[*//span[@data-dateperiodtype]]|preceding-sibling::tr[*//span[@data-dateperiodtype]])");
                                                            if (nodeListPrecedingDateWrappers.Count > 0 && dateWrappers.Count >= nodeListPrecedingDateWrappers.Count)
                                                            {
                                                                dateWrapperProperties = dateWrappers[nodeListPrecedingDateWrappers.Count - 1];
                                                                if (sdeProcessingInstruction == "balancecomparison")
                                                                {
                                                                    var isLastRow = true;
                                                                    var nextTableRow = nodeTableBodyRow.NextSibling;
                                                                    if (nextTableRow != null && nextTableRow.NodeType.ToString() == "Element") isLastRow = false;


                                                                    // var isLastRow =( nodeTableBodyRow?.NextSibling?.NodeType.ToString() ?? "unknown" == "Element") ?? false;
                                                                    if (!isLastRow)
                                                                    {
                                                                        try
                                                                        {
                                                                            dateWrapperPropertiesNext = dateWrappers[nodeListPrecedingDateWrappers.Count];
                                                                        }
                                                                        catch (Exception ex)
                                                                        {
                                                                            appLogger.LogDebug(ex, $"Error attempting to locate the next date wrapper (articleId: {articleId}, tableId: {tableId})");
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (nodeListPrecedingDateWrappers.Count == 0)
                                                                {
                                                                    warningMessage = $"Could not shift dates for factId {factId} because the date wrapper element in the table body could not be located (articleId: {articleId}, tableId: {tableId})";
                                                                    appLogger.LogWarning(warningMessage);
                                                                    warningBuilder.AppendLine(warningMessage);
                                                                }
                                                                else
                                                                {
                                                                    warningMessage = $"Could not shift dates for factId {factId} because the datewrapper precededing date wrapper count was too large (articleId: {articleId}, tableId: {tableId})";
                                                                    appLogger.LogWarning(warningMessage);
                                                                    warningBuilder.AppendLine(warningMessage);
                                                                }
                                                            }

                                                        }




                                                        var isDuration = existingPeriod.Contains("_");
                                                        if (dateWrapperProperties != null && dateWrapperProperties.WrapperParsed)
                                                        {
                                                            if (!string.IsNullOrEmpty(existingPeriod))
                                                            {
                                                                //
                                                                // => Calculate the new period
                                                                //

                                                                if (isDuration)
                                                                {
                                                                    // Special case where we compare balances and start - end date are set by the current and next date indicators
                                                                    if (
                                                                        dateWrapperProperties.PeriodStart == dateWrapperProperties.PeriodEnd &&
                                                                        sdeProcessingInstruction == "balancecomparison" &&
                                                                        dateWrapperPropertiesNext != null
                                                                    )
                                                                    {
                                                                        var balanceStart = dateWrapperProperties.PeriodStart;
                                                                        var balanceEnd = dateWrapperPropertiesNext.PeriodEnd;
                                                                        newPeriod = $"{balanceStart}_{balanceEnd}";

                                                                        // dateWrapperProperties.PeriodStart = projectPeriodMetadata.PeriodStart.ToString("yyyyMMdd");
                                                                    }
                                                                    else
                                                                    {
                                                                        newPeriod = $"{dateWrapperProperties.PeriodStart}_{dateWrapperProperties.PeriodEnd}";
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    switch (sdeProcessingInstructionNested)
                                                                    {
                                                                        case "sticktoperiodstart":
                                                                            newPeriod = dateWrapperProperties.PeriodStart;
                                                                            break;

                                                                        case "sticktoquarterstart":
                                                                            newPeriod = dateWrapperProperties.Year.ToString() + projectPeriodMetadata.PeriodEnd.GetQuarterStartDate().ToString("MMdd");
                                                                            break;

                                                                        case "sticktoyearstart":
                                                                            newPeriod = dateWrapperProperties.Year.ToString() + "0101";
                                                                            break;

                                                                        case "sticktoyearend":
                                                                            newPeriod = dateWrapperProperties.Year.ToString() + "1231";
                                                                            break;

                                                                        default:
                                                                            newPeriod = dateWrapperProperties.PeriodEnd;
                                                                            break;
                                                                    }

                                                                }
                                                            }
                                                            else
                                                            {
                                                                appLogger.LogWarning($"Could not find period for fact-id: {factId}. (articleId: {articleId}, tableId: {tableId})");
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (dateWrapperProperties != null && !dateWrapperProperties.WrapperParsed)
                                                            {
                                                                //
                                                                // => Attempt to "guess" how the dates should be shifted by hardcoding on table head and using the existing period as a template
                                                                //
                                                                var guessNewPeriodResult = Extensions.GuessNewStructuredDataPeriod(factId, tableId, existingPeriod, dateWrapperProperties, projectPeriodMetadata);
                                                                if (guessNewPeriodResult.Success)
                                                                {
                                                                    newPeriod = guessNewPeriodResult.Payload;
                                                                }
                                                                else
                                                                {
                                                                    appLogger.LogWarning(guessNewPeriodResult.ToString());
                                                                    warningBuilder.AppendLine(guessNewPeriodResult.Message);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                warningMessage = $"Could not shift dates for factId {factId} because the main date context is unavailable (articleId: {articleId}, tableId: {tableId})";
                                                                appLogger.LogWarning(warningMessage);
                                                                warningBuilder.AppendLine(warningMessage);
                                                            }
                                                        }




                                                        /*
                                                        <mappingCluster projectId="ar19">
                                                            <entry scheme="Internal" status="Undefined" period="20180101_20181231" datatype="monetary" displayOption="Rounded x million without unit" isFlagged="false" isAbsolute="false" context="1152338-discontinued-operations-and-assets-classified-as-held-for-sale">
                                                                <mapping>Discops_Lumileds_P_L_tableid__Cell_3_ResultOnTheSaleOfDiscontinuedOperations</mapping>
                                                            </entry>
                                                            <entry scheme="EFR" status="Undefined" isFlagged="false" isAbsolute="false">
                                                                <mapping>{"dataset":"table_uid1779246104_model","coordinates":[{"dimension":"Concepts_table_uid1779246104","members":["ResultOnTheSaleOfDiscontinuedOperations"]}]}</mapping>
                                                            </entry>
                                                        </mappingCluster>


                                                        <mappingCluster projectId="q319" requestId="573c77af-3348-430f-ae2e-fad292ca59bb">
                                                            <entry scheme="Internal" status="Undefined" period="20190701_20190930" flipsign="false" isFlagged="false" isAbsolute="false" context="philips-performance" datatype="monetary" displayOption="Rounded x million without unit">
                                                                <mapping>573c77af-3348-430f-ae2e-fad292ca59bb</mapping>
                                                            </entry>
                                                            <entry scheme="EFR" status="Undefined" flipsign="false" isFlagged="false">
                                                                <mapping>{"dataset":"PL Q3 2019_model","coordinates":[{"dimension":"PITEM_PCINTERNAL_20190930","members":["PC/280999","PC/211121"]}]}</mapping>
                                                            </entry>
                                                            <entry scheme="" status="Undefined" flipsign="false" isFlagged="false"></entry>
                                                            <fact value="4631923820.21" unit="EUR" period="duration" basetype="monetary" display="Rounded x million without unit"></fact>
                                                        </mappingCluster>
                                                        */



                                                        if (!string.IsNullOrEmpty(newPeriod))
                                                        {
                                                            if (debugRoutine)
                                                            {
                                                                Console.WriteLine("***************************");
                                                                Console.WriteLine($"existingPeriod: {existingPeriod}, newPeriod: {newPeriod}");
                                                                Console.WriteLine("***************************");
                                                            }

                                                            //
                                                            // => Test if the start date is earlier than the end date
                                                            //
                                                            var calculatedDataIsValid = true;
                                                            if (isDuration)
                                                            {
                                                                var newPeriodParsed = ParseMappingClusterPeriod(newPeriod);
                                                                calculatedDataIsValid = (DateTime.Compare(newPeriodParsed.PeriodStart, newPeriodParsed.PeriodEnd) < 0);
                                                            }


                                                            if (calculatedDataIsValid)
                                                            {
                                                                if (newPeriod != existingPeriod)
                                                                {
                                                                    //
                                                                    // => Update the mapping cluster with the new period in the database
                                                                    //
                                                                    var nodeNewInternalEntry = _getInternalEntry(xmlUpdatedMappingCluster.DocumentElement, factId);
                                                                    if (nodeNewInternalEntry != null)
                                                                    {
                                                                        nodeNewInternalEntry.SetAttribute("period", newPeriod);
                                                                        xmlUpdatedMappingCluster.DocumentElement.SetAttribute("projectId", projectId);
                                                                        xmlUpdatedMappingCluster.DocumentElement.RemoveAttribute("requestId");

                                                                        var xmlUpdateResult = await Taxxor.ConnectedServices.MappingService.UpdateMappingEntry(xmlUpdatedMappingCluster, true);
                                                                        if (XmlContainsError(xmlUpdateResult))
                                                                        {
                                                                            errorBuilder.AppendLine($"Updating factId {factId} to period {newPeriod} failed. Error: {xmlUpdateResult.OuterXml}, articleId: {articleId}, tableId: {tableId}");
                                                                        }
                                                                        else
                                                                        {
                                                                            sdeShifted++;
                                                                        }
                                                                        // if (debugRoutine)
                                                                        // {
                                                                        //     Console.WriteLine("***************** UPDATE RESULT ********************");
                                                                        //     Console.WriteLine(PrettyPrintXml(xmlUpdateResult));
                                                                        //     Console.WriteLine("*************************************");
                                                                        // }
                                                                    }
                                                                    else
                                                                    {
                                                                        errorBuilder.AppendLine($"Could not find internal entry node for factId: {factId} to update to period {newPeriod}. articleId: {articleId}, tableId: {tableId}");
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    debugBuilder.AppendLine($"No need to store the mapping cluster as we did not calculate a new period for this factId: {factId}. articleId: {articleId}, tableId: {tableId}");
                                                                }

                                                                //
                                                                // => Add additional attributes on the SDE if needed
                                                                //
                                                                switch (sdeProcessingInstruction)
                                                                {
                                                                    case "hide-future-elements":
                                                                        var newPeriodParsed = ParseMappingClusterPeriod(newPeriod);
                                                                        if (newPeriodParsed.PeriodEnd > projectPeriodMetadata.PeriodEnd)
                                                                        {
                                                                            nodeStructuredDataElement.SetAttribute("data-hidevalue", "true");
                                                                        }


                                                                        break;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                errorBuilder.AppendLine($"Could not update {factId} from {existingPeriod} to {newPeriod} as the new period is invalid. articleId: {articleId}, tableId: {tableId}");
                                                            }

                                                        }
                                                        else
                                                        {
                                                            warningBuilder.AppendLine($"Unable to update mapping cluster because new period was empty. articleId: {articleId}, tableId: {tableId}");
                                                        }
                                                    }


                                                }
                                                else
                                                {
                                                    appLogger.LogWarning("Could not retrieve a valid factID - 2");
                                                }

                                                // - Update the investigated SDE counter
                                                sdeInvestigated++;
                                            }
                                            else
                                            {
                                                appLogger.LogTrace("Skipping cell because it does not contain a structured data element");
                                            }

                                            // Cellcounter needs to be colspan aware
                                            bodyCellCounter = bodyCellCounter + 1 + (colspan - 1);
                                        }


                                        bodyRowCounter++;
                                    }

                                }
                                else
                                {
                                    errorMessage = $"Could not retrieve mapping clusters. articleId: {articleId}, tableId: {tableId}";
                                    errorBuilder.AppendLine(errorMessage);
                                    appLogger.LogError($"{errorMessage}, message: {xmlMappingInformation.SelectSingleNode("//message")?.InnerText ?? ""}, debuginfo: {xmlMappingInformation.SelectSingleNode("//debuginfo")?.InnerText ?? ""}");
                                }

                            }
                            else
                            {
                                warningBuilder.AppendLine($"Could not parse table {tableId} because there is no support yet for tables without context in the table head. articleId: {articleId}");
                            }
                        }
                        else
                        {
                            if (!continueParsingTable) warningBuilder.AppendLine($"Could not parse table {tableId} because the header section structure is not supported. articleId: {articleId}");
                            if (!isStructuredDataTable) warningBuilder.AppendLine($"Skipped {tableId} because it is not marked as a structured data table. articleId: {articleId}");
                        }

                    }
                    else
                    {
                        if (nodeTableHead == null) warningBuilder.AppendLine($"Table head could not be located. articleId: {articleId}, tableId: {tableId}");
                        if (nodeTableBody == null) warningBuilder.AppendLine($"Table body could not be located. articleId: {articleId}, tableId: {tableId}");
                    }
                }

                //
                // => Generate a result message
                //
                var xmlResultDetails = _generateSdeDateShiftResultDetailedReport(xmlFilingContentSourceData, debugBuilder, warningBuilder, errorBuilder, debugRoutine);



                //
                // => Return a result
                //
                return new TaxxorReturnMessage(true, $"SDE investigated: {sdeInvestigated}, SDE shifted: {sdeShifted}", xmlResultDetails, $"{baseDebugString}");
            }
            catch (Exception ex)
            {
                //
                // => Make sure we log this problem appropriately
                //
                appLogger.LogError(ex, $"There was a problem shifting SDE's in article: {articleId}, tableId: {tableId}, {baseDebugString}");


                //
                // => Generate a result message
                //
                var xmlResultDetails = _generateSdeDateShiftResultDetailedReport(xmlFilingContentSourceData, debugBuilder, warningBuilder, errorBuilder, debugRoutine);

                //
                // => Return a result
                //
                return new TaxxorReturnMessage(false, $"There was an error shifting dates (article ID: {articleId}, table ID: {tableId})", xmlResultDetails, $"Status: SDE investigated: {sdeInvestigated}, SDE shifted: {sdeShifted}");
            }
        }


        /// <summary>
        /// Generates an XML Document containing detailed information about the dateshifts of the Structured Data Elements
        /// </summary>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <param name="debugBuilder"></param>
        /// <param name="warningBuilder"></param>
        /// <param name="errorBuilder"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        private static XmlDocument _generateSdeDateShiftResultDetailedReport(XmlDocument xmlFilingContentSourceData, StringBuilder debugBuilder, StringBuilder warningBuilder, StringBuilder errorBuilder, bool debugRoutine)
        {
            var warningCount = Regex.Matches(warningBuilder.ToString(), Environment.NewLine).Count;
            var errorCount = Regex.Matches(errorBuilder.ToString(), Environment.NewLine).Count;

            var sectionId = "";
            if (warningCount > 0 || errorCount > 0)
            {
                var nodeListArticles = xmlFilingContentSourceData.SelectNodes("//article");
                if (nodeListArticles.Count < 3)
                {
                    // We have most likely shifted a single section
                    sectionId = nodeListArticles.Item(0).GetAttribute("id") ?? "";
                }
            }

            var xmlResultDetails = RetrieveTemplate($"{((warningCount > 0 || errorCount > 0) ? "error" : "success")}_xml");
            xmlResultDetails.SelectSingleNode("//message").InnerText = (warningCount > 0 || errorCount > 0) ? $"Problems detected while shifting SDE dates in {sectionId}" : "Successfully shifted SDE's dates";
            xmlResultDetails.SelectSingleNode("//debuginfo").InnerText = debugBuilder.ToString();


            if (debugRoutine)
            {
                Console.WriteLine("");
                Console.WriteLine("++ SDE period shift result ++");
                appLogger.LogDebug(debugBuilder.ToString());
                Console.WriteLine("+++++++++++++++++++++++++++++");
                Console.WriteLine("");
                Console.WriteLine("");
            }

            if (warningCount > 0 || errorCount > 0)
            {
                appLogger.LogWarning($"++ ERRORS and/or WARNINGS in SDE period shift {(!string.IsNullOrEmpty(sectionId) ? "of " + sectionId + " " : "")}detected! ++");
                var nodeLog = xmlResultDetails.CreateElement("log");
                nodeLog.SetAttribute("sectionid", sectionId);
                if (warningCount > 0)
                {
                    appLogger.LogWarning(warningBuilder.ToString());
                    var nodeWarning = xmlResultDetails.CreateElementWithText("warnings", warningBuilder.ToString());
                    nodeWarning.SetAttribute("count", Regex.Matches(warningBuilder.ToString(), Environment.NewLine).Count.ToString());
                    nodeLog.AppendChild(nodeWarning);
                }
                if (errorCount > 0)
                {
                    appLogger.LogError(errorBuilder.ToString());
                    var nodeError = xmlResultDetails.CreateElementWithText("errors", errorBuilder.ToString());
                    nodeError.SetAttribute("count", Regex.Matches(errorBuilder.ToString(), Environment.NewLine).Count.ToString());
                    nodeLog.AppendChild(nodeError);
                }

                xmlResultDetails.DocumentElement.AppendChild(nodeLog);
            }
            else
            {
                appLogger.LogInformation($"++ Successfully shifted all SDE's {(!string.IsNullOrEmpty(sectionId) ? "in " + sectionId + " " : "")}++");
            }

            return xmlResultDetails;
        }


    }
}