using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Main date detection logic - loops through XML tables and marks date cells
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Loops through a section XML file and attempts to mark cells of tables that contain dates
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <param name="lang">Use 'all' or a specific language where you want this logic to apply to all languages, or a specific language</param>
        /// <param name="removeExistingMarkings">Define if you want to re-calculate the date wrappers or only search for un-tagged dates</param>
        /// <param name="reportingPeriodOverrule">By default the logic will use the reporting period as defined for the project, but you can pass a custom period if needed</param>
        /// <returns></returns>
        public static (XmlDocument XmlData, StringBuilder DebugContent, StringBuilder ErrorContent) MarkDatesInTableCells(string projectId, XmlDocument xmlFilingContentSourceData, string lang, bool removeExistingMarkings = true, string reportingPeriodOverrule = null)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";

            // For storing debugging information
            var debugBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            //
            // => Retrieve project period information
            //
            var projectPeriodMetadata = new ProjectPeriodProperties(projectId, reportingPeriodOverrule);

            if (!projectPeriodMetadata.Success)
            {
                return (XmlData: xmlFilingContentSourceData, DebugContent: debugBuilder, ErrorContent: errorBuilder);
            }
            var reportingPeriod = projectPeriodMetadata.ReportingPeriod;
            var projectType = projectPeriodMetadata.ProjectType;
            int currentProjectYear = projectPeriodMetadata.CurrentProjectYear;
            int currentProjectQuarter = projectPeriodMetadata.CurrentProjectQuarter;
            int currentProjectMonth = projectPeriodMetadata.CurrentProjectMonth;

            // appLogger.LogDebug($"- currentProjectYear: {currentProjectYear}");
            // appLogger.LogDebug($"- currentProjectQuarter: {currentProjectQuarter}");

            //
            // => Potentially start the process by removing existing date wrappers
            //
            if (removeExistingMarkings)
            {
                XsltArgumentList xsltArgs = new XsltArgumentList();
                xsltArgs.AddParam("output-channel-language", "", lang);
                xmlFilingContentSourceData = TransformXmlToDocument(xmlFilingContentSourceData, "xsl_remove-dynamic-date-wrappers", xsltArgs);
            }

            var tableCellCounter = 0;

            // Language specific query
            var xpathContent = (lang == "all") ? "//content" : $"//content[@lang='{lang}']";
            var nodeListContent = xmlFilingContentSourceData.SelectNodes(xpathContent);
            if (nodeListContent.Count == 0)
            {
                var errorMessage = $"Could not find any content to transform. xpathContent: {xpathContent} returned no matches";
                appLogger.LogWarning(errorMessage);
                errorBuilder.AppendLine($"{errorMessage}");
            }

            foreach (XmlNode nodeContent in nodeListContent)
            {
                var articleId = "";
                var nodeArticle = nodeContent.SelectSingleNode("article");
                if (nodeArticle != null)
                {
                    articleId = nodeArticle.GetAttribute("id") ?? "";
                }
                else
                {
                    articleId = "website";
                }

                var contentLang = nodeContent.GetAttribute("lang") ?? "unknown";

                debugBuilder.AppendLine($"\nArticle ID: {articleId}, lang: {contentLang}");

                if (articleId == "1166898-reconciliation-of-non-ifrs-information")
                {
                    appLogger.LogInformation("Logged");
                }


                //
                // => Inspect table cell content
                //
                var nodeListTables = nodeContent.SelectNodes("*//table");
                foreach (XmlNode nodeTable in nodeListTables)
                {
                    var tableId = nodeTable.GetAttribute("id") ?? "";
                    debugBuilder.AppendLine($"Table ID: {tableId}");
                    var nodeListTableCells = nodeTable.SelectNodes("descendant::tr/*[(local-name()='td' and position()=1) or local-name()='th']");

                    if (tableId == "table_uid-208790957")
                    {
                        appLogger.LogInformation("Logged");
                    }

                    foreach (XmlNode nodeTableCell in nodeListTableCells)
                    {
                        tableCellCounter++;

                        // Retrieve the node containing the content of the cell and the content itself
                        var nodeCellContent = _retrieveCellContentNode(nodeTableCell);
                        string? cellContent = (nodeCellContent != null) ? nodeCellContent.InnerXml.Trim() : null;

                        // Test if we have cell content and if it already contains a special date wrapper - then we do not need to add it
                        if (cellContent != null && nodeTableCell.SelectNodes("descendant-or-self::span[@class='txdynamicdate']").Count == 0)
                        {
                            // debugBuilder.Append($"{cellContent}, ");

                            // Parsed date fragments from the RegExp
                            double cellContentDay = 0;
                            double cellContentMonth = 0;
                            double cellContentQuarter = 0;
                            double cellContentYear = 0;

                            double cellContentStartDay = 0;
                            double cellContentStartMonth = 0;
                            double cellContentStartYear = 0;

                            var contentBefore = "";
                            var contentMatched = "";
                            var contentAfter = "";

                            // Information of the cell content that is parsed
                            var needsToBeWrapped = false;
                            var isFlexibleDate = true;
                            var periodType = "";
                            var dateFormat = "";
                            double offset = 0;
                            double offsetStart = 0.001;
                            string? offsetString = null;
                            var offsetFormat = "y";

                            var periodEndMonth = "";

                            // Check if the cell content matches with any of the date expressions defined above
                            var match = reQuarterFull.Match(cellContent);
                            try
                            {
                                if (match.Success)
                                {

                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "fullquarter";
                                    dateFormat = formatQuarterFull;
                                    offsetFormat = "y";

                                    if (Double.TryParse(match.Groups[3].Value, out cellContentQuarter) && Double.TryParse(match.Groups[4].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentMatched = match.Groups[1].Value;
                                        contentAfter = match.Groups[5].Value;

                                        var datePeriodEnd = _getPeriodEndDate(cellContentYear, cellContentQuarter);

                                        cellContentDay = (double)datePeriodEnd.Day;
                                        cellContentMonth = (double)datePeriodEnd.Month;

                                        // Determine the offset
                                        offset = _calculateDateOffset(cellContentYear, datePeriodEnd.ToString("MMMM"), Convert.ToDouble(datePeriodEnd.Day), currentProjectYear, currentProjectQuarter);

                                        // Round to nearest quarter
                                        offset = _roundToQuarter(offset);

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a full quarter with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reQuarterFull", $"'{match.Groups[3].Value}' or '{match.Groups[4].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match full quarter
                                //
                                match = reQuarterTechnical.Match(cellContent);
                                if (match.Success)
                                {

                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "fullquarter";
                                    dateFormat = formatQuarterTechnical;
                                    offsetFormat = "y";

                                    if (Double.TryParse(match.Groups[3].Value, out cellContentQuarter) && Double.TryParse(match.Groups[1].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentMatched = cellContent;

                                        var datePeriodEnd = _getPeriodEndDate(cellContentYear, cellContentQuarter);

                                        cellContentDay = (double)datePeriodEnd.Day;
                                        cellContentMonth = (double)datePeriodEnd.Month;

                                        // Determine the offset
                                        offset = _calculateDateOffset(cellContentYear, datePeriodEnd.ToString("MMMM"), Convert.ToDouble(datePeriodEnd.Day), currentProjectYear, currentProjectQuarter);

                                        // Round to nearest quarter
                                        offset = _roundToQuarter(offset);

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a full quarter technical with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reQuarterTechnical", $"'{match.Groups[1].Value}' or '{match.Groups[2].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match half year with year
                                //
                                match = reHalfYearWithYear.Match(cellContent);
                                if (match.Success)
                                {

                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "halfyear";
                                    dateFormat = formatHalfYearWithYear;
                                    offsetFormat = "y";

                                    if (Double.TryParse(match.Groups[3].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = $"{match.Groups[2].Value}{match.Groups[3].Value}";
                                        contentAfter = match.Groups[4].Value;

                                        if (currentProjectQuarter == 1 || currentProjectQuarter == 2)
                                        {
                                            //  var datePeriodEnd = _getPeriodEndDate(cellContentYear, 2);
                                            // var datePeriodStart = _getPeriodStartDate(cellContentYear, 2);
                                            cellContentDay = 30;
                                            cellContentMonth = 6;
                                            cellContentStartDay = 1;
                                            cellContentStartMonth = 1;
                                        }
                                        if (currentProjectQuarter == 3 || currentProjectQuarter == 4)
                                        {
                                            cellContentDay = 31;
                                            cellContentMonth = 12;
                                            cellContentStartDay = 1;
                                            cellContentStartMonth = 7;
                                        }

                                        // Determine the year offset
                                        offset = cellContentYear - currentProjectYear;


                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a half year with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reHalfYearWithYear", $"'{match.Groups[3].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match YTD with year
                                //
                                match = reYtdWithYear.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "longperiodytd";
                                    dateFormat = formatYtdWithYear;
                                    offsetFormat = "y";

                                    if (Double.TryParse(match.Groups[3].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = $"{match.Groups[2].Value}{match.Groups[3].Value}";
                                        contentAfter = match.Groups[4].Value;

                                        // Start date
                                        cellContentStartDay = 1;
                                        cellContentStartMonth = 1;
                                        cellContentStartYear = cellContentYear;

                                        // End date
                                        switch (projectType)
                                        {
                                            case "ar":
                                                cellContentDay = 31;
                                                cellContentMonth = 12;
                                                break;

                                            case "qr":
                                                {
                                                    var datePeriodEnd = _getPeriodEndDate((int)cellContentYear, currentProjectQuarter, projectType);
                                                    cellContentDay = (double)datePeriodEnd.Day;
                                                    cellContentMonth = (double)datePeriodEnd.Month;
                                                }
                                                break;

                                            case "mr":
                                                {
                                                    var datePeriodEnd = _getPeriodEndDate((int)cellContentYear, currentProjectMonth, projectType);
                                                    cellContentDay = (double)datePeriodEnd.Day;
                                                    cellContentMonth = (double)datePeriodEnd.Month;
                                                }
                                                break;

                                            default:
                                                {
                                                    var datePeriodEnd = _getPeriodEndDate(cellContentYear);
                                                    cellContentDay = (double)datePeriodEnd.Day;
                                                    cellContentMonth = (double)datePeriodEnd.Month;
                                                }
                                                break;

                                        }

                                        // Determine the year offset
                                        offset = cellContentYear - currentProjectYear;

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a ytd with year and offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reYtdWithYear", $"'{match.Groups[3].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match YTD
                                //
                                match = reYtd.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "longperiodytd";
                                    dateFormat = formatYtd;
                                    offsetFormat = "y";

                                    // Grab the content fragments
                                    contentBefore = match.Groups[1].Value;
                                    contentMatched = match.Groups[2].Value;
                                    contentAfter = match.Groups[3].Value;

                                    // Start date
                                    cellContentStartDay = 1;
                                    cellContentStartMonth = 1;
                                    cellContentStartYear = 0;

                                    // End date
                                    switch (projectType)
                                    {
                                        case "ar":
                                            cellContentDay = 31;
                                            cellContentMonth = 12;
                                            break;

                                        case "qr":
                                            {
                                                var datePeriodEnd = _getPeriodEndDate((int)currentProjectYear, currentProjectQuarter, projectType);
                                                cellContentDay = (double)datePeriodEnd.Day;
                                                cellContentMonth = (double)datePeriodEnd.Month;
                                            }
                                            break;

                                        case "mr":
                                            {
                                                var datePeriodEnd = _getPeriodEndDate((int)currentProjectYear, currentProjectMonth, projectType);
                                                cellContentDay = (double)datePeriodEnd.Day;
                                                cellContentMonth = (double)datePeriodEnd.Month;
                                            }
                                            break;

                                        default:
                                            {
                                                var datePeriodEnd = _getPeriodEndDate(currentProjectYear);
                                                cellContentDay = (double)datePeriodEnd.Day;
                                                cellContentMonth = (double)datePeriodEnd.Month;
                                            }
                                            break;

                                    }

                                    if (debugRoutine) appLogger.LogDebug($"{cellContent} is a ytd and offset {offset}{offsetFormat}");

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match long period with year (two versions)
                                //
                                match = reLongPeriodWithYear.Match(cellContent);
                                if (match.Success)
                                {
                                    dateFormat = formatLongPeriodWithYear;
                                    if (cellContent.Contains(",")) dateFormat = formatLongPeriodWithYearComma;
                                }
                                else
                                {
                                    match = reLongPeriodWithYear2.Match(cellContent);
                                    dateFormat = formatLongPeriodWithYear2;
                                }

                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "longperiod";

                                    var longPeriodBeginMonth = match.Groups[1].Value;
                                    var longPeriodEndMonth = match.Groups[2].Value;
                                    if (!Double.TryParse(match.Groups[4].Value, out cellContentYear))
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reLongPeriodWithYear", match.Groups[4].Value, cellContent));
                                        goto GenerateDynamicDateWrapperTable;
                                    }


                                    // Grab the content fragments
                                    contentMatched = cellContent;
                                    cellContentMonth = (double)_getMonthInt(longPeriodEndMonth);
                                    cellContentStartMonth = (double)_getMonthInt(longPeriodBeginMonth);
                                    cellContentStartYear = cellContentYear;

                                    var longPeriodType = "halfyear";
                                    if (cellContentStartMonth == 1 && cellContentMonth == 12)
                                    {
                                        periodType = "longperiodfullyear";
                                        longPeriodType = "fullyear";
                                    }

                                    switch (projectType)
                                    {
                                        case "ar":
                                            offset = cellContentYear - currentProjectYear;
                                            break;

                                        case "qr":
                                            // Determine the year offset
                                            offset = cellContentYear - currentProjectYear;

                                            if (longPeriodType == "halfyear")
                                            {
                                                if ((currentProjectQuarter == 1 || currentProjectQuarter == 2) && cellContentStartMonth == 7 && cellContentMonth == 12)
                                                {
                                                    offset = offset + 0.50;
                                                }
                                                if ((currentProjectQuarter == 3 || currentProjectQuarter == 4) && cellContentStartMonth == 1 && cellContentMonth == 6)
                                                {
                                                    offset = offset - 0.50;
                                                }
                                            }

                                            // Year to date
                                            if (currentProjectQuarter > 1 && cellContentStartMonth == 1)
                                            {
                                                periodType = "longperiodytd";
                                            }
                                            break;


                                        case "mr":
                                            offsetFormat = "m";

                                            // Determine the date offset for the end date
                                            // var dateForCalculation = new DateTime(1970, currentProjectMonth, 15);
                                            var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                            var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, dateCurrentPeriodEnd.Day);
                                            var dateCell = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentMonth), 1);
                                            offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);

                                            if (cellContentMonth == cellContentStartMonth && cellContentStartDay == 1 && Convert.ToDouble(currentProjectMonth) == cellContentMonth)
                                            {
                                                periodType = "longperioditm";
                                            }
                                            else
                                            {
                                                var dayStart = (cellContentStartDay > 0) ? Convert.ToInt32(cellContentStartDay) : 1;
                                                var dayEnd = (cellContentDay > 0) ? Convert.ToInt32(cellContentDay) : 1;

                                                var datePeriodStart = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentStartMonth), dayStart);
                                                var datePeriodEnd = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentMonth), dayEnd);

                                                var periodStartQuarter = datePeriodStart.GetQuarter();
                                                var periodEndQuarter = datePeriodEnd.GetQuarter();
                                                if (periodStartQuarter == periodEndQuarter && datePeriodStart.Month == datePeriodStart.GetQuarterStartDate().Month && dayStart == 1)
                                                {
                                                    periodType = "longperiodqtd";
                                                }
                                                else
                                                {
                                                    if (cellContentStartMonth == 1 && cellContentMonth > 1)
                                                    {
                                                        periodType = "longperiodytd";
                                                    }
                                                    else
                                                    {
                                                        // This is a regular period for which we also need to calculate a start offset
                                                        offsetStart = _getMonthsBetween(datePeriodStart, dateForOffsetCalculation);
                                                    }

                                                }
                                            }

                                            break;

                                        default:
                                            appLogger.LogWarning($"{projectType} not supported for parsing {cellContent} with offset {offset}{offsetFormat}");
                                            break;
                                    }

                                    if (debugRoutine) appLogger.LogDebug($"{cellContent} is a long {longPeriodType} period with offset {offset}{offsetFormat}");

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match long period with year and day
                                //
                                match = reLongPeriodWithYearAndDay.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "longperiod";
                                    dateFormat = cellContent.Contains(",") ? formatLongPeriodWithYearAndDayComma : formatLongPeriodWithYearAndDay;
                                    var longPeriodBeginMonth = match.Groups[1].Value;
                                    var longPeriodEndMonth = match.Groups[3].Value;
                                    if (!Double.TryParse(match.Groups[6].Value, out cellContentYear))
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reLongPeriodWithYearAndDay", match.Groups[6].Value, cellContent));
                                        goto GenerateDynamicDateWrapperTable;
                                    }

                                    if (!Double.TryParse(match.Groups[4].Value, out cellContentDay))
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reLongPeriodWithYearAndDay", match.Groups[4].Value, cellContent));
                                        goto GenerateDynamicDateWrapperTable;
                                    }

                                    if (!Double.TryParse(match.Groups[2].Value, out cellContentStartDay))
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reLongPeriodWithYearAndDay", match.Groups[2].Value, cellContent));
                                        goto GenerateDynamicDateWrapperTable;
                                    }



                                    // Grab the content fragments
                                    contentMatched = cellContent;
                                    cellContentMonth = (double)_getMonthInt(longPeriodEndMonth);
                                    cellContentStartMonth = (double)_getMonthInt(longPeriodBeginMonth);
                                    cellContentStartYear = cellContentYear;

                                    var longPeriodType = "halfyear";
                                    if (cellContentStartMonth == 1 && cellContentStartDay == 1 && cellContentMonth == 12 && cellContentDay == 31)
                                    {
                                        periodType = "longperiodfullyear";
                                        longPeriodType = "fullyear";
                                    }

                                    switch (projectType)
                                    {
                                        case "ar":
                                            offset = cellContentYear - currentProjectYear;
                                            break;

                                        case "qr":
                                            // Determine the year offset
                                            offset = cellContentYear - currentProjectYear;

                                            if (longPeriodType == "halfyear")
                                            {
                                                if ((currentProjectQuarter == 1 || currentProjectQuarter == 2) && cellContentStartMonth == 7 && cellContentMonth == 12)
                                                {
                                                    offset = offset + 0.50;
                                                }
                                                if ((currentProjectQuarter == 3 || currentProjectQuarter == 4) && cellContentStartMonth == 1 && cellContentMonth == 6)
                                                {
                                                    offset = offset - 0.50;
                                                }
                                            }

                                            // Year to date
                                            if (currentProjectQuarter > 1 && cellContentStartMonth == 1)
                                            {
                                                periodType = "longperiodytd";
                                            }
                                            break;


                                        case "mr":
                                            offsetFormat = "m";

                                            // Determine the date offset for the end date
                                            // var dateForCalculation = new DateTime(1970, currentProjectMonth, 15);
                                            var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                            var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, dateCurrentPeriodEnd.Day);
                                            var dateCell = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentMonth), Convert.ToInt32(cellContentDay));
                                            offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);

                                            if (cellContentMonth == cellContentStartMonth && cellContentStartDay == 1 && Convert.ToDouble(currentProjectMonth) == cellContentMonth)
                                            {
                                                periodType = "longperioditm";
                                            }
                                            else
                                            {
                                                var dayStart = (cellContentStartDay > 0) ? Convert.ToInt32(cellContentStartDay) : 1;
                                                var dayEnd = (cellContentDay > 0) ? Convert.ToInt32(cellContentDay) : 1;

                                                var datePeriodStart = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentStartMonth), dayStart);
                                                var datePeriodEnd = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentMonth), dayEnd);

                                                var periodStartQuarter = datePeriodStart.GetQuarter();
                                                var periodEndQuarter = datePeriodEnd.GetQuarter();
                                                if (periodStartQuarter == periodEndQuarter && datePeriodStart.Month == datePeriodStart.GetQuarterStartDate().Month && dayStart == 1)
                                                {
                                                    periodType = "longperiodqtd";
                                                }
                                                else
                                                {
                                                    if (cellContentStartMonth == 1 && cellContentMonth > 1)
                                                    {
                                                        periodType = "longperiodytd";
                                                    }
                                                    else
                                                    {
                                                        // This is a regular period for which we also need to calculate a start offset
                                                        offsetStart = _getMonthsBetween(datePeriodStart, dateForOffsetCalculation);
                                                    }

                                                }
                                            }

                                            break;

                                        default:
                                            appLogger.LogWarning($"{projectType} not supported for parsing {cellContent} with offset {offset}{offsetFormat}");
                                            break;
                                    }

                                    if (debugRoutine) appLogger.LogDebug($"{cellContent} is a long {longPeriodType} period with offset {offset}{offsetFormat}");

                                    goto GenerateDynamicDateWrapperTable;
                                }



                                //
                                // => Match running period no year 1
                                //
                                match = reRunningPeriodNoYear.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = (projectType == "ar") ? false : true;
                                    periodType = "runningperiod";
                                    dateFormat = formatRunningPeriodNoYear;

                                    // The end date determines the offset
                                    periodEndMonth = match.Groups[2].Value;

                                    cellContentMonth = (double)_getMonthInt(periodEndMonth);
                                    cellContentStartMonth = 1;

                                    if (projectType == "ar")
                                    {
                                        offset = 0;
                                    }
                                    else
                                    {
                                        offset = _roundToQuarter(_calculateDateOffset((double)currentProjectYear, periodEndMonth, 28, currentProjectYear, currentProjectQuarter));

                                        // Year to date
                                        if (currentProjectQuarter > 1)
                                        {
                                            periodType = "runningperiodytd";
                                        }
                                    }

                                    // Grab the content fragments
                                    contentMatched = cellContent;

                                    if (debugRoutine) appLogger.LogDebug($"{cellContent} is a running period without a year and no offset");

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match running period without year 3
                                //
                                match = reRunningPeriodNoYear3.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = (projectType == "ar") ? false : true;
                                    periodType = "runningperiod";
                                    dateFormat = formatRunningPeriodNoYear3;

                                    // The end date determines the offset
                                    periodEndMonth = match.Groups[4].Value;
                                    cellContentMonth = (double)_getMonthInt(periodEndMonth);

                                    cellContentStartMonth = _getMonthInt(match.Groups[2].Value);

                                    switch (projectType)
                                    {
                                        case "ar":
                                            offset = 0;
                                            break;

                                        case "qr":
                                            offset = _roundToQuarter(_calculateDateOffset((double)currentProjectYear, periodEndMonth, 28, currentProjectYear, currentProjectQuarter));

                                            // Year to date
                                            if (currentProjectQuarter > 1)
                                            {
                                                periodType = "runningperiodytd";
                                            }

                                            break;

                                        case "mr":
                                            offset = 0;
                                            offsetFormat = "m";
                                            if (cellContentMonth == cellContentStartMonth && Convert.ToDouble(currentProjectMonth) == cellContentMonth)
                                            {
                                                periodType = "runningperioditm";
                                            }
                                            else
                                            {
                                                var dateForCalculation = new DateTime(1970, currentProjectMonth, 15);
                                                var datePeriodStart = new DateTime(1970, Convert.ToInt32(cellContentStartMonth), 15);
                                                var datePeriodEnd = new DateTime(1970, Convert.ToInt32(cellContentMonth), 15);

                                                // Determine offset

                                                // offset end is relative to this month
                                                offset = _getMonthsBetween(datePeriodEnd, dateForCalculation);

                                                var periodStartQuarter = datePeriodStart.GetQuarter();
                                                var periodEndQuarter = datePeriodEnd.GetQuarter();

                                                if (periodStartQuarter == periodEndQuarter && datePeriodStart.Month == datePeriodStart.GetQuarterStartDate().Month)
                                                {
                                                    periodType = "runningperiodqtd";
                                                }
                                                else
                                                {
                                                    if (cellContentStartMonth == 1 && cellContentMonth > 1)
                                                    {
                                                        periodType = "runningperiodytd";
                                                    }
                                                    else
                                                    {
                                                        // This is a regular period for which we also need to calculate a start offset
                                                        offsetStart = _getMonthsBetween(datePeriodStart, dateForCalculation);
                                                    }
                                                }
                                            }

                                            break;
                                    }


                                    // Grab the content fragments
                                    contentMatched = match.Groups[1].Value;
                                    contentAfter = match.Groups[5].Value;

                                    if (debugRoutine) appLogger.LogDebug($"{cellContent} is a running period (3) without a year and {offset} offset");

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match running period without year (and day) 4
                                //
                                match = reRunningPeriodNoYear4.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = (projectType == "ar") ? false : true;
                                    periodType = "runningperiod";
                                    dateFormat = formatRunningPeriodNoYear4;

                                    // The end date determines the offset
                                    periodEndMonth = match.Groups[5].Value;
                                    cellContentMonth = (double)_getMonthInt(periodEndMonth);
                                    if (!Double.TryParse(match.Groups[6].Value, out cellContentDay))
                                    {
                                        appLogger.LogError(GenerateTableParseErrorMessage("reRunningPeriodNoYear4", $"", cellContent));
                                    }

                                    // Start of period
                                    cellContentStartMonth = _getMonthInt(match.Groups[2].Value);
                                    if (!Double.TryParse(match.Groups[3].Value, out cellContentStartDay))
                                    {
                                        appLogger.LogError(GenerateTableParseErrorMessage("reRunningPeriodNoYear4", $"", cellContent));
                                    }

                                    switch (projectType)
                                    {
                                        case "ar":
                                            offset = 0;
                                            break;

                                        case "qr":
                                            offset = _roundToQuarter(_calculateDateOffset((double)currentProjectYear, periodEndMonth, 28, currentProjectYear, currentProjectQuarter));

                                            // Year to date
                                            if (currentProjectQuarter > 1)
                                            {
                                                periodType = "runningperiodytd";
                                            }

                                            break;

                                        case "mr":
                                            offset = 0;
                                            offsetFormat = "m";
                                            if (cellContentMonth == cellContentStartMonth && cellContentStartDay == 1 && Convert.ToDouble(currentProjectMonth) == cellContentMonth)
                                            {
                                                periodType = "runningperioditm";
                                            }
                                            else
                                            {
                                                var dateForCalculation = new DateTime(1970, currentProjectMonth, 15);
                                                var datePeriodStart = new DateTime(1970, Convert.ToInt32(cellContentStartMonth), Convert.ToInt32(cellContentStartDay));
                                                var datePeriodEnd = new DateTime(1970, Convert.ToInt32(cellContentMonth), Convert.ToInt32(cellContentDay));

                                                // Determine offset

                                                // offset end is relative to this month
                                                offset = _getMonthsBetween(dateForCalculation, datePeriodEnd);

                                                var periodStartQuarter = datePeriodStart.GetQuarter();
                                                var periodEndQuarter = datePeriodEnd.GetQuarter();

                                                if (periodStartQuarter == periodEndQuarter && datePeriodStart.Month == datePeriodStart.GetQuarterStartDate().Month && cellContentStartDay == 1)
                                                {
                                                    periodType = "runningperiodqtd";
                                                }
                                                else
                                                {
                                                    if (cellContentStartMonth == 1 && cellContentMonth > 1 && cellContentStartDay == 1)
                                                    {
                                                        periodType = "runningperiodytd";
                                                    }
                                                    else
                                                    {
                                                        // This is a regular period for which we also need to calculate a start offset
                                                        offsetStart = _getMonthsBetween(datePeriodStart, dateForCalculation);
                                                    }
                                                }
                                            }

                                            break;
                                    }



                                    // Grab the content fragments
                                    contentMatched = match.Groups[1].Value;
                                    contentAfter = match.Groups[7].Value;

                                    if (debugRoutine) appLogger.LogDebug($"{cellContent} is a running period (4) without a year and no offset");

                                    goto GenerateDynamicDateWrapperTable;
                                }



                                //
                                // => Match written date 1
                                //
                                match = reDateWrittenFormat1.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormat1;

                                    if (Double.TryParse(match.Groups[4].Value, out cellContentDay) && Double.TryParse(match.Groups[5].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = match.Groups[2].Value;
                                        contentAfter = match.Groups[6].Value;

                                        var monthString = match.Groups[3].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        if (projectType == "ar" || (_getMonthInt(monthString) == 12 && cellContentDay == 31) || (_getMonthInt(monthString) == 1 && cellContentDay == 1))
                                        {
                                            // This date will move in years, so de define the offset in years
                                            offsetFormat = "y";
                                            offset = cellContentYear - currentProjectYear;
                                        }
                                        else
                                        {
                                            if (projectType == "mr")
                                            {
                                                // Provide the offset in months
                                                offsetFormat = "m";

                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 1);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 1);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                            else
                                            {
                                                // This date will move in months, so we define the offset in months
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 15);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 15);

                                                var parsedDate = DateTime.ParseExact(contentMatched, dateFormat, System.Globalization.CultureInfo.InvariantCulture);
                                                if (_isLastDayOfQuarter(parsedDate))
                                                {
                                                    // Provide the offset in quarters
                                                    offsetFormat = "q";

                                                    var datePeriodEnd = _getPeriodEndDate(cellContentYear, cellContentQuarter);

                                                    // Determine the offset
                                                    offset = _calculateDateOffset(Convert.ToDouble(parsedDate.Year), parsedDate.ToString("MMMM"), Convert.ToDouble(parsedDate.Day), currentProjectYear, currentProjectQuarter);

                                                    // Round to nearest quarter
                                                    offset = _roundToQuarter(offset) * 4;
                                                }
                                                else
                                                {
                                                    // Provide the offset in months
                                                    offsetFormat = "m";
                                                    offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                                }
                                            }

                                        }

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a written-date-format1 with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormat1", $"'{match.Groups[4].Value}' or '{match.Groups[5].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match written date 2
                                //
                                match = reDateWrittenFormat2.Match(cellContent);
                                if (match.Success)
                                {

                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormat2;

                                    if (Double.TryParse(match.Groups[4].Value, out cellContentDay) && Double.TryParse(match.Groups[5].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = match.Groups[2].Value;
                                        contentAfter = match.Groups[6].Value;

                                        var monthString = match.Groups[3].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        if ((projectType == "ar") || (_getMonthInt(monthString) == 12 && cellContentDay == 31 && projectType != "mr") || (_getMonthInt(monthString) == 1 && cellContentDay == 1 && projectType != "mr"))
                                        {
                                            // This date will move in years, so de define the offset in years
                                            offsetFormat = "y";
                                            offset = cellContentYear - currentProjectYear;
                                        }
                                        else
                                        {
                                            if (projectType == "mr")
                                            {
                                                // Provide the offset in months
                                                offsetFormat = "m";

                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 1);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 1);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                            else
                                            {
                                                // This date will move in months, so we define the offset in months
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 15);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 15);

                                                var parsedDate = DateTime.ParseExact(contentMatched, dateFormat, System.Globalization.CultureInfo.InvariantCulture);
                                                if (_isLastDayOfQuarter(parsedDate))
                                                {
                                                    // Provide the offset in quarters
                                                    offsetFormat = "q";

                                                    var datePeriodEnd = _getPeriodEndDate(cellContentYear, cellContentQuarter);

                                                    // Determine the offset
                                                    offset = _calculateDateOffset(Convert.ToDouble(parsedDate.Year), parsedDate.ToString("MMMM"), Convert.ToDouble(parsedDate.Day), currentProjectYear, currentProjectQuarter);

                                                    // Round to nearest quarter
                                                    offset = _roundToQuarter(offset) * 4;
                                                }
                                                else
                                                {
                                                    // Provide the offset in months
                                                    offsetFormat = "m";
                                                    offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                                }
                                            }
                                        }


                                        // Determine the offset
                                        // offset = _calculateDateOffset(cellContentYear, match.Groups[3].Value, cellContentDay, currentProjectYear, currentProjectQuarter);

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a written-date-format2 with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormat2", $"'{match.Groups[4].Value}' or '{match.Groups[5].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }



                                //
                                // => Match written date 4
                                //
                                match = reDateWrittenFormat4.Match(cellContent);
                                if (match.Success)
                                {

                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormat4;

                                    if (Double.TryParse(match.Groups[3].Value, out cellContentDay) && Double.TryParse(match.Groups[5].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = match.Groups[2].Value;
                                        contentAfter = match.Groups[6].Value;

                                        var monthString = match.Groups[4].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        if (projectType == "ar" || (_getMonthInt(monthString) == 12 && cellContentDay == 31) || (_getMonthInt(monthString) == 1 && cellContentDay == 1))
                                        {
                                            // This date will move in years, so de define the offset in years
                                            offsetFormat = "y";
                                            offset = cellContentYear - currentProjectYear;
                                        }
                                        else
                                        {
                                            if (projectType == "mr")
                                            {
                                                // Provide the offset in months
                                                offsetFormat = "m";

                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 1);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 1);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                            else
                                            {
                                                // This date will move in months, so we define the offset in months
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 15);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 15);

                                                var parsedDate = DateTime.ParseExact(contentMatched, dateFormat, System.Globalization.CultureInfo.InvariantCulture);
                                                if (_isLastDayOfQuarter(parsedDate))
                                                {
                                                    // Provide the offset in quarters
                                                    offsetFormat = "q";

                                                    var datePeriodEnd = _getPeriodEndDate(cellContentYear, cellContentQuarter);

                                                    // Determine the offset
                                                    offset = _calculateDateOffset(Convert.ToDouble(parsedDate.Year), parsedDate.ToString("MMMM"), Convert.ToDouble(parsedDate.Day), currentProjectYear, currentProjectQuarter);

                                                    // Round to nearest quarter
                                                    offset = _roundToQuarter(offset) * 4;
                                                }
                                                else
                                                {
                                                    // Provide the offset in months
                                                    offsetFormat = "m";
                                                    offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                                }
                                            }
                                        }


                                        // Determine the offset
                                        // offset = _calculateDateOffset(cellContentYear, match.Groups[3].Value, cellContentDay, currentProjectYear, currentProjectQuarter);

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a written-date-format4 with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormat4", $"'{match.Groups[3].Value}' or '{match.Groups[5].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match Dutch written date (d MMMM yyyy)
                                //
                                match = reDateWrittenFormatDutch.Match(cellContent);
                                if (match.Success)
                                {

                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormatDutch;

                                    if (Double.TryParse(match.Groups[3].Value, out cellContentDay) && Double.TryParse(match.Groups[5].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = match.Groups[2].Value;
                                        contentAfter = match.Groups[6].Value;

                                        var monthString = match.Groups[4].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        if (projectType == "ar" || (_getMonthInt(monthString) == 12 && cellContentDay == 31) || (_getMonthInt(monthString) == 1 && cellContentDay == 1))
                                        {
                                            // This date will move in years, so de define the offset in years
                                            offsetFormat = "y";
                                            offset = cellContentYear - currentProjectYear;
                                        }
                                        else
                                        {
                                            if (projectType == "mr")
                                            {
                                                // Provide the offset in months
                                                offsetFormat = "m";

                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 1);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 1);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                            else
                                            {
                                                // This date will move in months, so we define the offset in months
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 15);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 15);

                                                var parsedDate = DateTime.ParseExact(contentMatched, dateFormat, System.Globalization.CultureInfo.InvariantCulture);
                                                if (_isLastDayOfQuarter(parsedDate))
                                                {
                                                    // Provide the offset in quarters
                                                    offsetFormat = "q";

                                                    var datePeriodEnd = _getPeriodEndDate(cellContentYear, cellContentQuarter);

                                                    // Determine the offset
                                                    offset = _calculateDateOffset(Convert.ToDouble(parsedDate.Year), parsedDate.ToString("MMMM"), Convert.ToDouble(parsedDate.Day), currentProjectYear, currentProjectQuarter);

                                                    // Round to nearest quarter
                                                    offset = _roundToQuarter(offset) * 4;
                                                }
                                                else
                                                {
                                                    // Provide the offset in months
                                                    offsetFormat = "m";
                                                    offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                                }
                                            }
                                        }


                                        // Determine the offset
                                        // offset = _calculateDateOffset(cellContentYear, match.Groups[3].Value, cellContentDay, currentProjectYear, currentProjectQuarter);

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a Dutch written-date with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormatDutch", $"'{match.Groups[3].Value}' or '{match.Groups[5].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match written date 3
                                //
                                match = reDateWrittenFormat3.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormat3;

                                    if (Double.TryParse(match.Groups[3].Value, out cellContentDay) && Double.TryParse(match.Groups[4].Value, out cellContentMonth) && Double.TryParse(match.Groups[5].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = match.Groups[2].Value;
                                        contentAfter = match.Groups[6].Value;

                                        if (projectType == "ar" || (cellContentMonth == 12 && cellContentDay == 31) || (cellContentMonth == 1 && cellContentDay == 1))
                                        {
                                            // This date will move in years, so de define the offset in years
                                            offsetFormat = "y";
                                            offset = cellContentYear - currentProjectYear;
                                        }
                                        else
                                        {
                                            if (projectType == "mr")
                                            {
                                                // Provide the offset in months
                                                offsetFormat = "m";

                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 1);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentMonth), 1);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                            else
                                            {
                                                // This date will move in months, so we define the offset in months
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 15);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentMonth), 15);

                                                var parsedDate = DateTime.ParseExact(contentMatched, dateFormat, System.Globalization.CultureInfo.InvariantCulture);
                                                if (_isLastDayOfQuarter(parsedDate))
                                                {
                                                    // Provide the offset in quarters
                                                    offsetFormat = "q";

                                                    var datePeriodEnd = _getPeriodEndDate(cellContentYear, cellContentQuarter);

                                                    // Determine the offset
                                                    offset = _calculateDateOffset(Convert.ToDouble(parsedDate.Year), parsedDate.ToString("MMMM"), Convert.ToDouble(parsedDate.Day), currentProjectYear, currentProjectQuarter);

                                                    // Round to nearest quarter
                                                    offset = _roundToQuarter(offset) * 4;
                                                }
                                                else
                                                {
                                                    // Provide the offset in months
                                                    offsetFormat = "m";
                                                    offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                                }
                                            }
                                        }


                                        // Determine the offset
                                        // offset = _calculateDateOffset(cellContentYear, match.Groups[3].Value, cellContentDay, currentProjectYear, currentProjectQuarter);

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a written-date-format3 with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormat3", $"'{match.Groups[3].Value}' or '{match.Groups[4].Value}' or '{match.Groups[5].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match written date 4
                                //
                                match = reDateWrittenFormatSimple1.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormatSimple1;
                                    if (Double.TryParse(match.Groups[3].Value, out cellContentYear))
                                    {
                                        // Grab the content
                                        contentMatched = match.Groups[1].Value;
                                        contentAfter = match.Groups[4].Value;

                                        // This date will move in months, so we define the offset in months
                                        var monthString = match.Groups[2].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        if (projectType == "ar" || (projectType == "qr" && _getMonthInt(monthString) == 1))
                                        {
                                            // We only need to shift the year
                                            offsetFormat = "y";

                                            offset = cellContentYear - currentProjectYear;
                                        }
                                        else
                                        {
                                            // Assume that we need to shift it along with the quarter
                                            offsetFormat = "m";

                                            if (projectType == "mr")
                                            {
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 1);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 1);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                            else
                                            {
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 15);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 15);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormatSimple1", match.Groups[3].Value, cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match written date 6
                                //
                                match = reDateWrittenFormatSimple2.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormatSimple2;
                                    if (Double.TryParse(match.Groups[3].Value, out cellContentYear))
                                    {
                                        // Grab the content
                                        contentMatched = match.Groups[1].Value;
                                        contentAfter = match.Groups[4].Value;

                                        // This date will move in months, so we define the offset in months
                                        var monthString = match.Groups[2].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        if (projectType == "ar" || (projectType == "qr" && _getMonthInt(monthString) == 1))
                                        {
                                            // We only need to shift the year
                                            offsetFormat = "y";

                                            offset = cellContentYear - currentProjectYear;
                                        }
                                        else
                                        {
                                            // Assume that we need to shift it along with the quarter
                                            offsetFormat = "m";

                                            if (projectType == "mr")
                                            {
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 1);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 1);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                            else
                                            {
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 15);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 15);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormatSimple2", match.Groups[3].Value, cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match written date 7
                                //
                                match = reDateWrittenFormatSimple3.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormatSimple3;
                                    if (Double.TryParse(match.Groups[3].Value, out cellContentYear))
                                    {
                                        // Grab the content
                                        contentMatched = match.Groups[1].Value;
                                        contentAfter = match.Groups[4].Value;

                                        // This date will move in months, so we define the offset in months
                                        var monthString = match.Groups[2].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        if (projectType == "ar" || (projectType == "qr" && _getMonthInt(monthString) == 1))
                                        {
                                            // We only need to shift the year
                                            offsetFormat = "y";

                                            offset = cellContentYear - currentProjectYear;
                                        }
                                        else
                                        {
                                            // Assume that we need to shift it along with the quarter
                                            offsetFormat = "m";

                                            if (projectType == "mr")
                                            {
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 1);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 1);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                            else
                                            {
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 15);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), _getMonthInt(monthString), 15);

                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormatSimple3", match.Groups[3].Value, cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match date period with dashes (DD-MM-YYYY  DD-MM-YYYY)
                                //
                                match = reDatePeriodDashFormat.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "yearonyear";
                                    dateFormat = formatDatePeriodDashFormat;

                                    double dayStart, monthStart, yearStart;
                                    double dayEnd, monthEnd, yearEnd;

                                    if (Double.TryParse(match.Groups[1].Value, out dayStart) &&
                                        Double.TryParse(match.Groups[2].Value, out monthStart) &&
                                        Double.TryParse(match.Groups[3].Value, out yearStart) &&
                                        Double.TryParse(match.Groups[4].Value, out dayEnd) &&
                                        Double.TryParse(match.Groups[5].Value, out monthEnd) &&
                                        Double.TryParse(match.Groups[6].Value, out yearEnd))
                                    {
                                        // Grab the content
                                        contentMatched = match.Groups[0].Value;

                                        // Store both dates
                                        cellContentYear = yearEnd;
                                        cellContentMonth = monthEnd;
                                        cellContentDay = dayEnd;

                                        cellContentStartYear = yearStart;
                                        cellContentStartMonth = monthStart;
                                        cellContentStartDay = dayStart;

                                        // Calculate offsets for both dates
                                        var dateStart = new DateTime(Convert.ToInt32(yearStart),
                                            Convert.ToInt32(monthStart), Convert.ToInt32(dayStart));
                                        var dateEnd = new DateTime(Convert.ToInt32(yearEnd),
                                            Convert.ToInt32(monthEnd), Convert.ToInt32(dayEnd));

                                        double offsetStartCalc = _calculateDateOffset(yearStart,
                                            dateStart.ToString("MMMM"), dayStart, currentProjectYear, currentProjectQuarter);
                                        double offsetEndCalc = _calculateDateOffset(yearEnd,
                                            dateEnd.ToString("MMMM"), dayEnd, currentProjectYear, currentProjectQuarter);

                                        // Use offsetString to store both offsets (similar to year comparison)
                                        offsetString = $"{offsetStartCalc:F2}y,{offsetEndCalc:F2}y";
                                        offsetFormat = "y";

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a date period (dd-MM-yyyy  dd-MM-yyyy) with offset {offsetString}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDatePeriodDashFormat", "date parsing failed", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match written date 5 (DD-MM-YYYY with dashes)
                                //
                                match = reDateWrittenFormat5.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormat5;

                                    if (Double.TryParse(match.Groups[3].Value, out cellContentDay) &&
                                        Double.TryParse(match.Groups[4].Value, out cellContentMonth) &&
                                        Double.TryParse(match.Groups[5].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = match.Groups[2].Value;
                                        contentAfter = match.Groups[6].Value;

                                        // Same logic as reDateWrittenFormat3 (dd/MM/yyyy)
                                        if (projectType == "ar" || (cellContentMonth == 12 && cellContentDay == 31) ||
                                            (cellContentMonth == 1 && cellContentDay == 1))
                                        {
                                            // Year-end dates: offset in years
                                            offsetFormat = "y";
                                            offset = cellContentYear - currentProjectYear;
                                        }
                                        else
                                        {
                                            if (projectType == "mr")
                                            {
                                                // Monthly reports: offset in months
                                                offsetFormat = "m";
                                                var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");
                                                var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 1);
                                                var dateCell = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentMonth), 1);
                                                offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                            }
                                            else
                                            {
                                                // Quarterly reports: check if quarter-end, otherwise use months
                                                var parsedDate = DateTime.ParseExact(contentMatched, dateFormat,
                                                    System.Globalization.CultureInfo.InvariantCulture);

                                                if (_isLastDayOfQuarter(parsedDate))
                                                {
                                                    offsetFormat = "q";
                                                    cellContentQuarter = parsedDate.GetQuarter();
                                                    offset = _calculateDateOffset(Convert.ToDouble(parsedDate.Year),
                                                        parsedDate.ToString("MMMM"), Convert.ToDouble(parsedDate.Day),
                                                        currentProjectYear, currentProjectQuarter);
                                                    offset = _roundToQuarter(offset) * 4;
                                                }
                                                else
                                                {
                                                    offsetFormat = "m";
                                                    var dateCurrentPeriodEnd = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                    var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, 15);
                                                    var dateCell = new DateTime(Convert.ToInt32(cellContentYear), Convert.ToInt32(cellContentMonth), 15);
                                                    offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                                }
                                            }
                                        }

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a written-date-format5 (dd-MM-yyyy) with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormat5",
                                            $"'{match.Groups[3].Value}' or '{match.Groups[4].Value}' or '{match.Groups[5].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match written date month day 1
                                //
                                match = reDateWrittenFormatSimpleNoYear1.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormatSimpleNoYear1;
                                    if (Double.TryParse(match.Groups[4].Value, out cellContentDay))
                                    {
                                        // Grab the content
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = match.Groups[2].Value;
                                        contentAfter = match.Groups[5].Value;

                                        // This date will move in months, so we define the offset in months
                                        var monthString = match.Groups[3].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        // Assume that we need to shift it along with the quarter
                                        offsetFormat = "m";

                                        var quarterForCalculation = (projectType == "ar") ? 0 : currentProjectQuarter;
                                        var dateCurrentPeriodEnd = (projectType == "mr") ? _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr") : _getPeriodEndDate(currentProjectYear, quarterForCalculation);
                                        var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, (int)cellContentDay);
                                        var dateCell = new DateTime(Convert.ToInt32(currentProjectYear), _getMonthInt(monthString), (int)cellContentDay);

                                        offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormatSimpleNoYear1", match.Groups[4].Value, cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match written date month day 2
                                //
                                match = reDateWrittenFormatSimpleNoYear2.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormatSimpleNoYear2;
                                    if (Double.TryParse(match.Groups[4].Value, out cellContentDay))
                                    {
                                        // Grab the content
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = match.Groups[2].Value;
                                        contentAfter = match.Groups[5].Value;

                                        // This date will move in months, so we define the offset in months
                                        var monthString = match.Groups[3].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        // Assume that we need to shift it along with the quarter
                                        offsetFormat = "m";

                                        var quarterForCalculation = (projectType == "ar") ? 0 : currentProjectQuarter;
                                        var dateCurrentPeriodEnd = (projectType == "mr") ? _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr") : _getPeriodEndDate(currentProjectYear, quarterForCalculation);
                                        var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, (int)cellContentDay);
                                        var dateCell = new DateTime(Convert.ToInt32(currentProjectYear), _getMonthInt(monthString), (int)cellContentDay);

                                        offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormatSimpleNoYear2", match.Groups[4].Value, cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match written date month day 3
                                //
                                match = reDateWrittenFormatSimpleNoYear3.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "writtendate";
                                    dateFormat = formatWrittenFormatSimpleNoYear3;
                                    if (Double.TryParse(match.Groups[4].Value, out cellContentDay))
                                    {
                                        // Grab the content
                                        contentBefore = match.Groups[1].Value;
                                        contentMatched = match.Groups[2].Value;
                                        contentAfter = match.Groups[5].Value;

                                        // This date will move in months, so we define the offset in months
                                        var monthString = match.Groups[3].Value;

                                        cellContentMonth = (double)_getMonthInt(monthString);

                                        // Assume that we need to shift it along with the quarter
                                        offsetFormat = "m";

                                        var quarterForCalculation = (projectType == "ar") ? 0 : currentProjectQuarter;
                                        var dateCurrentPeriodEnd = (projectType == "mr") ? _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr") : _getPeriodEndDate(currentProjectYear, quarterForCalculation);
                                        try
                                        {
                                            var dateForOffsetCalculation = new DateTime(dateCurrentPeriodEnd.Year, dateCurrentPeriodEnd.Month, dateCurrentPeriodEnd.Day);
                                            var dateCell = new DateTime(Convert.ToInt32(currentProjectYear), _getMonthInt(monthString), (int)cellContentDay);

                                            offset = _getMonthsBetween(dateCell, dateForOffsetCalculation);
                                        }
                                        catch (Exception ex)
                                        {
                                            appLogger.LogError(ex, $"Unable to calculate offset for '{cellContent}'. {currentProjectYear}, {_getMonthInt(monthString)}, {cellContentDay}");
                                        }

                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reDateWrittenFormatSimpleNoYear3", match.Groups[2].Value, cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }





                                //
                                // => Match long period without year
                                //
                                match = reLongPeriodNoYear.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = (projectType == "ar") ? false : true;
                                    periodType = "longperiod";
                                    dateFormat = formatLongPeriodNoYear;

                                    var longPeriodBeginMonth = match.Groups[1].Value;
                                    var longPeriodEndMonth = match.Groups[2].Value;

                                    cellContentMonth = (double)_getMonthInt(longPeriodEndMonth);
                                    cellContentStartMonth = (double)_getMonthInt(longPeriodBeginMonth);

                                    var longPeriodType = "halfyear";
                                    // TODO: Make this language independent
                                    if (longPeriodBeginMonth.ToLower() == "january" && longPeriodEndMonth.ToLower() == "december")
                                    {
                                        periodType = "longperiodfullyear";
                                        longPeriodType = "fullyear";
                                    }

                                    // Grab the content fragments
                                    contentMatched = cellContent;

                                    if (projectType == "qr" && longPeriodType == "halfyear")
                                    {
                                        if ((currentProjectQuarter == 1 || currentProjectQuarter == 2) && longPeriodBeginMonth.ToLower() == "july" && longPeriodEndMonth.ToLower() == "december")
                                        {
                                            offset = 0 + 0.50;
                                        }
                                        if ((currentProjectQuarter == 3 || currentProjectQuarter == 4) && longPeriodBeginMonth.ToLower() == "january" && longPeriodEndMonth.ToLower() == "june")
                                        {
                                            offset = 0 - 0.50;
                                        }

                                        // Year to date
                                        if (currentProjectQuarter > 1 && longPeriodBeginMonth.ToLower() == "january")
                                        {
                                            periodType = "longperiodytd";
                                        }
                                    }

                                    if (debugRoutine) appLogger.LogDebug($"{cellContent} is a long {longPeriodType} period without a year and with offset {offset}{offsetFormat}");

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match quarter
                                //
                                match = reQuarter.Match(cellContent);
                                if (match.Success)
                                {
                                    // For calculating offset
                                    var quarterYear = currentProjectYear;

                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;

                                    // In some cases, the quarter in a table is fixed
                                    if (projectType == "qr")
                                    {


                                        // Find the cellposition of the cell that we are investigating which helps to find the year information potentially
                                        var currentCellPosition = nodeTableCell.SelectNodes($"preceding-sibling::*").Count + 1;
                                        // appLogger.LogCritical($"- currentCellPosition: {currentCellPosition}");

                                        // The quarter is most likely not flexible if it's used in combination with a year in the row above
                                        var nodeCurrentRow = nodeTableCell.ParentNode;
                                        var nodePreviousRow = nodeCurrentRow.PreviousSibling;
                                        if (nodePreviousRow != null)
                                        {
                                            // Test if any of the cells in this row contains a year date
                                            var nodeListHeaderCells = nodePreviousRow.SelectNodes("*");
                                            var previousHeaderCellCounter = 0;
                                            foreach (XmlNode nodeHeaderCell in nodeListHeaderCells)
                                            {
                                                previousHeaderCellCounter++;

                                                // Retrieve columnspan
                                                var colspan = 0;
                                                var previousHeaderCellColumnSpan = nodeHeaderCell.GetAttribute("colspan");
                                                if (!string.IsNullOrEmpty(previousHeaderCellColumnSpan))
                                                {
                                                    if (int.TryParse(previousHeaderCellColumnSpan.Trim(), out colspan))
                                                    {
                                                        // appLogger.LogCritical("Found colspan");
                                                    }
                                                }

                                                var headerCellContent = _retrieveCellContent(nodeHeaderCell);

                                                if (!string.IsNullOrEmpty(headerCellContent))
                                                {
                                                    if (headerCellContent.Contains("<span") && headerCellContent.Contains("dynamicdate"))
                                                    {
                                                        headerCellContent = RegExpReplace(@"^.*<span.*?>(.*?)</span.*$", headerCellContent, "$1");
                                                    }

                                                    var yearMatch = reYear.Match(headerCellContent);
                                                    if (yearMatch.Success)
                                                    {
                                                        isFlexibleDate = false;
                                                        // appLogger.LogCritical("Found non-flexible quarterly date");

                                                        if (currentCellPosition >= previousHeaderCellCounter && currentCellPosition < (colspan + previousHeaderCellCounter))
                                                        {
                                                            if (int.TryParse(headerCellContent.Trim(), out quarterYear))
                                                            {
                                                                // appLogger.LogError($"Found quarteryear {quarterYear}");
                                                            }
                                                        }

                                                    }
                                                }

                                                previousHeaderCellCounter = previousHeaderCellCounter + colspan;
                                            }

                                        }
                                    }
                                    else
                                    {
                                        isFlexibleDate = false;
                                    }


                                    periodType = "shortquarter";
                                    dateFormat = formatQuarter;

                                    // Determine the offset
                                    if (Double.TryParse(match.Groups[2].Value, out cellContentQuarter))
                                    {
                                        // Grab the content fragments
                                        contentMatched = match.Groups[1].Value;
                                        contentAfter = match.Groups[3].Value;

                                        // Determine the time offset
                                        // TODO: check strange offsets
                                        offset = quarterYear - currentProjectYear + ((cellContentQuarter - currentProjectQuarter) * .25);

                                        if (offset > 5 || offset < 5)
                                        {
                                            appLogger.LogWarning($"Potentially strange offset {offset} in {cellContent} (articleId: {articleId}, tableId: {tableId}).");
                                        }

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a sort-quarter with flexible date {isFlexibleDate} and offset {offset}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reQuarter", $"", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }



                                //
                                // => Match complex year
                                //
                                match = reYearExtended.Match(cellContent);
                                if (match.Success)
                                {

                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "year";
                                    dateFormat = formatYear;

                                    if (Double.TryParse(match.Groups[1].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentMatched = match.Groups[1].Value;
                                        contentAfter = match.Groups[2].Value;

                                        // Determine the year offset
                                        offset = cellContentYear - currentProjectYear;

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a year with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reYearExtended", match.Groups[1].Value, cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match short year
                                //
                                match = reYearShort.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "year";
                                    dateFormat = formatYearShort;
                                    double shortYear = 0;
                                    if (Double.TryParse(match.Groups[2].Value, out shortYear))
                                    {
                                        cellContentYear = 2000 + shortYear;
                                        // Grab the content fragments
                                        contentMatched = cellContent;

                                        // Determine the year offset
                                        offset = cellContentYear - currentProjectYear;

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a sort-year with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reYearShort", match.Groups[2].Value, cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match year on year comparison
                                //
                                match = reYearOnYearComparison.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "yearonyear";
                                    dateFormat = formatYearOnYearComparison;

                                    double yearStart = 0;
                                    double yearEnd = 0;

                                    if (Double.TryParse(match.Groups[1].Value, out yearStart) && Double.TryParse(match.Groups[2].Value, out yearEnd))
                                    {
                                        // Grab the content fragments
                                        contentMatched = match.Groups[0].Value;

                                        cellContentYear = yearEnd;
                                        cellContentStartYear = yearStart;

                                        // Determine the year offset
                                        double offsetYearStart = yearStart - currentProjectYear;
                                        double offsetYearEnd = yearEnd - currentProjectYear;
                                        offsetString = $"{offsetYearStart},{offsetYearEnd}";

                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a year-on-year comparison with offset {offsetString}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reYearOnYearComparison", $"'{match.Groups[1].Value}' or '{match.Groups[2].Value}'", cellContent));
                                    }

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match a full year indicator
                                //
                                match = reFullYearNoYear.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = false;
                                    periodType = "longperiodfullyear";
                                    dateFormat = formatFullYearNoYear;

                                    // Grab the content fragments
                                    contentMatched = match.Groups[0].Value;

                                    // Determine the year offset
                                    offset = 0;
                                    if (debugRoutine) appLogger.LogDebug($"{cellContent} is a fullyear indicator");

                                    goto GenerateDynamicDateWrapperTable;
                                }


                                //
                                // => Match broken year
                                //
                                /*
                                    var reYearBroken = new Regex(@"^(20\d{2})\s+\/\s+(20\d{2})$");
                                    var formatYearBroken = "yyyy / yyyy";
                                */
                                match = reYearBroken.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "year";
                                    dateFormat = formatYearBroken;

                                    if (double.TryParse(match.Groups[2].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentMatched = match.Groups[0].Value;

                                        // Determine the year offset
                                        offset = cellContentYear - currentProjectYear;
                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a year with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reYearBroken", match.Groups[2].Value, cellContent));
                                    }
                                    goto GenerateDynamicDateWrapperTable;
                                }

                                //
                                // => Match year
                                //
                                match = reYear.Match(cellContent);
                                if (match.Success)
                                {
                                    needsToBeWrapped = true;
                                    isFlexibleDate = true;
                                    periodType = "year";
                                    dateFormat = string.IsNullOrEmpty(match.Groups[2].Value) ? formatYear : formatFullYear;

                                    if (double.TryParse(match.Groups[3].Value, out cellContentYear))
                                    {
                                        // Grab the content fragments
                                        contentMatched = match.Groups[0].Value;

                                        // Determine the year offset
                                        offset = cellContentYear - currentProjectYear;
                                        if (debugRoutine) appLogger.LogDebug($"{cellContent} is a year with offset {offset}{offsetFormat}");
                                    }
                                    else
                                    {
                                        needsToBeWrapped = false;
                                        appLogger.LogError(GenerateTableParseErrorMessage("reYear", match.Groups[2].Value, cellContent));
                                    }
                                }

                            }
                            catch (Exception ex)
                            {
                                var errorMessage = $"There was an error parsing period content in table cells. Article ID: {articleId}, Table ID: {nodeTable.GetAttribute("id")}, cellContent: {cellContent}";
                                appLogger.LogError(ex, errorMessage);
                                errorBuilder.AppendLine($"{errorMessage}, error: {ex}");
                            }


                        //
                        // => Generate the dynamic wrapper
                        //
                        GenerateDynamicDateWrapperTable:

                            // Wrap the period into a special <span/> element
                            if (!string.IsNullOrEmpty(dateFormat) && needsToBeWrapped)
                            {
                                var xmlContentNodeWrappedContent = $"";
                                try
                                {
                                    // Create the new content
                                    string offsetAttrContent = ((offsetString == null) ? offset.ToString() : offsetString) + offsetFormat;
                                    if (offsetAttrContent == "-0q" || offsetAttrContent == "+0q") offsetAttrContent = "0q";
                                    string offsetStartAttrContent = (offsetStart == 0.001) ? "" : offsetStart + offsetFormat;

                                    // Attributes for defining date fragments
                                    var dateFragmentAttributes = $" data-dateday=\"{cellContentDay}\" data-datemonth=\"{cellContentMonth}\" data-datequarter=\"{cellContentQuarter}\" data-dateyear=\"{cellContentYear}\"";

                                    var dateStartFragmentAttributes = "";
                                    if (cellContentStartDay > 0 || cellContentStartMonth > 0 || cellContentStartYear > 0)
                                    {
                                        dateStartFragmentAttributes = $" data-datestartday=\"{cellContentStartDay}\" data-datestartmonth=\"{cellContentStartMonth}\" data-datestartyear=\"{cellContentStartYear}\"";
                                    }

                                    // Test to see if the wrapper element overwites the data-dateflexible attribute
                                    var storedFlexibleDateSetting = nodeCellContent.GetAttribute("data-dateflexiblestored") ?? "";
                                    if (storedFlexibleDateSetting != "")
                                    {
                                        isFlexibleDate = storedFlexibleDateSetting == "true";
                                        nodeCellContent.RemoveAttribute("data-dateflexiblestored");
                                    }

                                    // Test if one of the parent nodes forces a specific date format
                                    var nodeWithSteeringAttribute = nodeCellContent.SelectSingleNode("ancestor-or-self::*[@data-txdynamicdate-forcedformat]");
                                    if (nodeWithSteeringAttribute != null)
                                    {
                                        dateFormat = nodeWithSteeringAttribute.GetAttribute("data-txdynamicdate-forcedformat");
                                    }

                                    // Test if the parent node forces a specific period type
                                    nodeWithSteeringAttribute = nodeCellContent.SelectSingleNode("ancestor-or-self::*[@data-txdynamicdate-forcedperiodtype]");
                                    if (nodeWithSteeringAttribute != null)
                                    {
                                        periodType = nodeWithSteeringAttribute.GetAttribute("data-txdynamicdate-forcedperiodtype");
                                    }

                                    // Create the new cell content with the wrapper
                                    xmlContentNodeWrappedContent = $"{contentBefore}<span class=\"txdynamicdate\" data-dateflexible=\"{isFlexibleDate.ToString().ToLower()}\" data-dateperiodtype=\"{periodType}\" data-dateformat=\"{dateFormat}\" data-dateoffset=\"{offsetAttrContent}\"{((offsetStartAttrContent == "") ? "" : " data-dateoffsetstart=\"" + offsetStartAttrContent + "\"")}{dateStartFragmentAttributes}{dateFragmentAttributes}>{contentMatched}</span>{contentAfter}";

                                    // Replace it
                                    nodeCellContent.InnerXml = xmlContentNodeWrappedContent;

                                    // Mark the table cell so that we can style it properly in the editor
                                    var tableCellClassContent = nodeTableCell.GetAttribute("class") ?? "";

                                    if (tableCellClassContent != "")
                                    {
                                        tableCellClassContent = tableCellClassContent.Replace("txdynamicdate ", "").Replace("txdynamicdate", "");
                                        tableCellClassContent = RegExpReplace(@"\s{2,}", tableCellClassContent, "");
                                        tableCellClassContent = tableCellClassContent.Trim() + " ";
                                    }

                                    nodeTableCell.SetAttribute("class", $"{tableCellClassContent}txdynamicdate");
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"There was an error wrapping a date value. xmlContentNodeWrappedContent: {xmlContentNodeWrappedContent}");
                                }
                            }
                        }

                        /// <summary>
                        /// Central function to generate a parse error
                        /// </summary>
                        /// <param name="regexName"></param>
                        /// <param name="parsedValue"></param>
                        /// <returns></returns>
                        string GenerateTableParseErrorMessage(string regexName, string parsedValue, string content)
                        {
                            // var tableId = nodeParagraph.SelectSingleNode("ancestor::div[contains(@class, 'tablegraph-header-wrapper')]")?.SelectSingleNode("//table")?.GetAttribute("id") ?? "";
                            var parsedValueDisplay = parsedValue.StartsWith("'") ? parsedValue : $"'{parsedValue}'";
                            return $"Could not parse {regexName} match to determine the offset ({parsedValueDisplay} cannot be parsed, articleId: {articleId}, tableId: {tableId}, content: {content})";
                        }


                    }
                    debugBuilder.AppendLine("");
                }



                //
                // => Inspect table header content
                //
                var nodeListTableGraphHeaderContent = nodeContent.SelectNodes("*//div[contains(@class, 'tablegraph-header-wrapper')]//p");
                foreach (XmlNode nodeParagraph in nodeListTableGraphHeaderContent)
                {
                    var paragraphContent = nodeParagraph.InnerText.Trim();
                    debugBuilder.Append($"{paragraphContent}, ");

                    // Parsed date fragments from the RegExp
                    double cellContentYear = 0;

                    var contentBefore = "";
                    var contentMatched = "";
                    var contentAfter = "";

                    // Information of the cell content that is parsed
                    var needsToBeWrapped = false;
                    var isFlexibleDate = true;
                    var periodType = "";
                    var dateFormat = "";
                    double offset = 0;
                    double offsetStart = 0.001;
                    string? offsetString = null;
                    string offsetFormat = "y";

                    // Check if the cell content matches with any of the date expressions defined above
                    var match = reYearPeriod.Match(paragraphContent);
                    try
                    {
                        if (match.Success)
                        {

                            needsToBeWrapped = true;
                            isFlexibleDate = true;
                            periodType = "year";
                            dateFormat = formatYearPeriod;

                            double yearStart = 0;
                            double yearEnd = 0;

                            if (Double.TryParse(match.Groups[1].Value, out yearStart) && Double.TryParse(match.Groups[2].Value, out yearEnd))
                            {
                                // Grab the content fragments
                                contentMatched = match.Groups[0].Value;

                                // Determine the year offset
                                double offsetYearStart = yearStart - currentProjectYear;
                                double offsetYearEnd = yearEnd - currentProjectYear;
                                offsetString = $"{offsetYearStart},{offsetYearEnd}";

                                if (debugRoutine) appLogger.LogDebug($"{paragraphContent} is a year-period with offset {offsetString}");
                            }
                            else
                            {
                                needsToBeWrapped = false;
                                appLogger.LogError(GenerateHeaderParseErrorMessage("reYearPeriod", $"'{match.Groups[1].Value}' or '{match.Groups[2].Value}'", paragraphContent));
                            }

                            goto GenerateDynamicDateWrapperTableHeader;
                        }

                        match = reYearBroken.Match(paragraphContent);
                        if (match.Success)
                        {
                            needsToBeWrapped = true;
                            isFlexibleDate = true;
                            periodType = "year";
                            dateFormat = formatYear;



                            if (double.TryParse(match.Groups[3].Value, out cellContentYear))
                            {
                                // Grab the content fragments
                                contentMatched = match.Groups[0].Value;

                                // Determine the year offset
                                offset = cellContentYear - currentProjectYear;
                                if (debugRoutine) appLogger.LogDebug($"{paragraphContent} is a year with offset {offset}{offsetFormat}");
                            }
                            else
                            {
                                needsToBeWrapped = false;
                                appLogger.LogError(GenerateHeaderParseErrorMessage("reYear", match.Groups[3].Value, paragraphContent));
                            }

                            goto GenerateDynamicDateWrapperTableHeader;
                        }


                        match = reYear.Match(paragraphContent);
                        if (match.Success)
                        {
                            needsToBeWrapped = true;
                            isFlexibleDate = true;
                            periodType = "year";
                            dateFormat = formatYear;

                            if (double.TryParse(match.Groups[3].Value, out cellContentYear))
                            {
                                // Grab the content fragments
                                contentMatched = match.Groups[0].Value;

                                // Determine the year offset
                                offset = cellContentYear - currentProjectYear;
                                if (debugRoutine) appLogger.LogDebug($"{paragraphContent} is a year with offset {offset}{offsetFormat}");
                            }
                            else
                            {
                                needsToBeWrapped = false;
                                appLogger.LogError(GenerateHeaderParseErrorMessage("reYear", match.Groups[3].Value, paragraphContent));
                            }

                            goto GenerateDynamicDateWrapperTableHeader;
                        }



                        /// <summary>
                        /// Central function to generate a parse error
                        /// </summary>
                        /// <param name="regexName"></param>
                        /// <param name="parsedValue"></param>
                        /// <returns></returns>
                        string GenerateHeaderParseErrorMessage(string regexName, string parsedValue, string content)
                        {
                            var tableId = nodeParagraph.SelectSingleNode("ancestor::div[contains(@class, 'tablegraph-header-wrapper')]")?.ParentNode?.SelectSingleNode("descendant-or-self::table")?.GetAttribute("id") ?? "";
                            var parsedValueDisplay = parsedValue.StartsWith("'") ? parsedValue : $"'{parsedValue}'";
                            return $"Could not parse {regexName} match to determine the offset ({parsedValueDisplay} cannot be parsed, articleId: {articleId}, tableId: {tableId}, content: {content})";
                        }


                    //
                    // => Generate the dynamic wrapper
                    //
                    GenerateDynamicDateWrapperTableHeader:

                        // Wrap the period into a special <span/> element
                        if (!string.IsNullOrEmpty(dateFormat) && needsToBeWrapped)
                        {
                            var xmlParagraphNodeWrappedContent = $"";
                            try
                            {
                                // Test to see if the wrapper element overwites the data-dateflexible attribute
                                var storedFlexibleDateSetting = nodeParagraph.GetAttribute("data-dateflexiblestored") ?? "";
                                if (storedFlexibleDateSetting != "")
                                {
                                    isFlexibleDate = storedFlexibleDateSetting == "true";
                                    nodeParagraph.RemoveAttribute("data-dateflexiblestored");
                                }

                                // Test if one of the parent nodes forces a specific date format
                                var nodeWithSteeringAttribute = nodeParagraph.SelectSingleNode("ancestor::*[@data-txdynamicdate-forcedformat]");
                                if (nodeWithSteeringAttribute != null)
                                {
                                    dateFormat = nodeWithSteeringAttribute.GetAttribute("data-txdynamicdate-forcedformat");
                                }

                                // Create the new content
                                string offsetAttrContent = ((offsetString == null) ? offset.ToString() : offsetString) + offsetFormat;
                                string offsetStartAttrContent = (offsetStart == 0.001) ? "" : offsetStart + offsetFormat;

                                xmlParagraphNodeWrappedContent = $"{contentBefore}<span class=\"txdynamicdate\" data-dateflexible=\"{isFlexibleDate.ToString().ToLower()}\" data-dateperiodtype=\"{periodType}\" data-dateformat=\"{dateFormat}\" data-dateoffset=\"{offsetAttrContent}\"{((offsetStartAttrContent == "") ? "" : " data-dateoffsetstart=\"" + offsetStartAttrContent + "\"")}>{contentMatched}</span>{contentAfter}";

                                // Replace it
                                nodeParagraph.InnerXml = xmlParagraphNodeWrappedContent;

                                // Mark the table cell so that we can style it properly in the editor
                                var paragraphClassContent = nodeParagraph.GetAttribute("class") ?? "";
                                if (paragraphClassContent != "")
                                {
                                    paragraphClassContent = paragraphClassContent.Replace("txdynamicdate ", "").Replace("txdynamicdate", "");
                                    paragraphClassContent = paragraphClassContent.Trim() + " ";
                                }

                                nodeParagraph.SetAttribute("class", $"{paragraphClassContent}txdynamicdate");
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, $"There was an error wrapping a date value. xmlContentNodeWrappedContent: {xmlParagraphNodeWrappedContent}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"There was an error parsing period content for table headers. Article ID: {articleId}, paragraphContent: {paragraphContent}";
                        appLogger.LogError(ex, errorMessage);
                        errorBuilder.AppendLine($"{errorMessage}, error: {ex}");
                    }

                }

            }

            return (XmlData: xmlFilingContentSourceData, DebugContent: debugBuilder, ErrorContent: errorBuilder);


        }


        /// <summary>
        /// Parses and analyzes a mapping cluster period
        /// </summary>
        /// <param name="period"></param>
        /// <returns></returns>
        public static (bool Success, bool IsDuration, string PeriodType, DateTime PeriodStart, DateTime PeriodEnd) ParseMappingClusterPeriod(string period)
        {
            DateTime periodStart = new DateTime();
            DateTime periodEnd = new DateTime();
            var periodType = "moment";
            try
            {
                var isDuration = period.Contains("_");
                if (isDuration)
                {
                    periodStart = DateTime.ParseExact(period.SubstringBefore("_"), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                    periodEnd = DateTime.ParseExact(period.SubstringAfter("_"), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

                    // Number of quarters between the two dates
                    int monthsBetween = Math.Abs(periodStart.ElapsedMonths(periodEnd));

                    switch (monthsBetween)
                    {
                        case 2:
                            periodType = "quarter";
                            break;
                        case 5:
                            periodType = "halfyear";
                            break;
                        case 11:
                            periodType = "year";
                            break;
                        default:
                            periodType = $"{monthsBetween}months";
                            break;
                    }
                }
                else
                {
                    periodEnd = DateTime.ParseExact(period, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                }

                return (Success: true, IsDuration: isDuration, PeriodType: periodType, PeriodStart: periodStart, PeriodEnd: periodEnd);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"There was an error parsing mapping cluster period: {period}, stack-trace: {GetStackTrace()}");
                return (Success: false, IsDuration: false, PeriodType: "unknown", PeriodStart: periodStart, PeriodEnd: periodEnd);
            }
        }



        /// <summary>
        /// Retrieves the node from the table cell that actually contains it's content
        /// </summary>
        /// <param name="nodeTableCell"></param>
        /// <param name="showWarnings"></param>
        /// <returns></returns>
    }
}
