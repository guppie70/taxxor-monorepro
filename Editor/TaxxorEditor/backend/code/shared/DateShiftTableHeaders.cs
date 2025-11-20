using System;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Moves the dates in table headers
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Moves dates in table cells and table headers to match the current reporting period
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlFilingContentSourceData"></param>
        /// <param name="lang">Use 'all' or a specific language where you want this logic to apply to all languages, or a specific language</param>
        /// <param name="reportingPeriodOverrule">By default the logic will use the reporting period as defined for the project, but you can pass a custom period if needed</param>
        /// <returns></returns>
        public static XmlDocument MoveDatesInTableCells(string projectId, XmlDocument xmlFilingContentSourceData, string lang, string reportingPeriodOverrule = null)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";


            //
            // => Retrieve project period information
            //
            var projectPeriodMetadata = new ProjectPeriodProperties(projectId, reportingPeriodOverrule);

            if (!projectPeriodMetadata.Success)
            {
                return xmlFilingContentSourceData;
            }
            var reportingPeriod = projectPeriodMetadata.ReportingPeriod;
            var projectType = projectPeriodMetadata.ProjectType;
            int currentProjectYear = projectPeriodMetadata.CurrentProjectYear;
            int currentProjectQuarter = projectPeriodMetadata.CurrentProjectQuarter;
            int currentProjectMonth = projectPeriodMetadata.CurrentProjectMonth;
            // appLogger.LogCritical($"- reportingPeriod: {reportingPeriod}");
            // appLogger.LogCritical($"- currentProjectYear: {currentProjectYear}");
            // appLogger.LogCritical($"- currentProjectQuarter: {currentProjectQuarter}");


            // For storing debugging information
            var debugBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

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

                if (articleId == "20476893-reconciliation-of-non-ifrs-information")
                {
                    appLogger.LogInformation("Logged");
                }

                // <span class="txdynamicdate" data-dateflexible="true" data-dateperiodtype="year" data-dateformat="yyyystart - yyyyend" data-dateoffset="-2,0">
                var nodeListDynamicDates = nodeContent.SelectNodes("*//span[@class='txdynamicdate' or @data-dateformat]");
                foreach (XmlNode nodeDynamicDate in nodeListDynamicDates)
                {

                    // TODO: The code in this loop should actually be inside a try/catch, but since this method returns an XmlDocument it is hard to push the error to the calling service
                    // try
                    // {

                    // }
                    // catch (Exception ex)
                    // {
                    //     appLogger.LogError(ex, "There was an error shifting the dynamic date for {dynamicDateContent} in article {articleId}", nodeDynamicDate.InnerText, articleId);
                    //     errorBuilder.AppendLine($"Error shifting the dynamic date for {nodeDynamicDate.InnerText} in article {articleId}");
                    // }


                    //
                    // => Retrieve date information
                    //
                    var dateWrapperProperties = new DateWrapperProperties(nodeDynamicDate);

                    // Test if one of the parent nodes forces a specific date format
                    var nodeWithSteeringAttribute = nodeDynamicDate.SelectSingleNode("ancestor::*[@data-txdynamicdate-forcedformat]");
                    if (nodeWithSteeringAttribute != null)
                    {
                        dateWrapperProperties.Format = nodeWithSteeringAttribute.GetAttribute("data-txdynamicdate-forcedformat");
                    }

                    // Special processing instructions for shifting dates
                    DateTime fixedDate = new();
                    var isFixedDate = false;
                    nodeWithSteeringAttribute = nodeDynamicDate.SelectSingleNode("ancestor::*[@data-txdynamicdate-processinginstruction]");
                    if (nodeWithSteeringAttribute != null)
                    {
                        var dateShiftProceessingInstruction = nodeWithSteeringAttribute.GetAttribute("data-txdynamicdate-processinginstruction");
                        if (dateWrapperProperties.Month == 0)
                        {
                            appLogger.LogWarning($"Cannot calculate fixed table date because the month is unknown");
                        }
                        else
                        {
                            isFixedDate = true;
                            var yearForCalculation = dateWrapperProperties.Year > 0 ? dateWrapperProperties.Year : currentProjectYear;

                            switch (dateShiftProceessingInstruction)
                            {
                                case "sticktopriormonthend":
                                    {
                                        var calculateMonth = (currentProjectMonth == 1) ? 12 : currentProjectMonth - 1;
                                        fixedDate = new DateTime(yearForCalculation, calculateMonth, DateTime.DaysInMonth(yearForCalculation, calculateMonth));
                                    }
                                    break;

                                case "sticktopriorquarterend":
                                    {
                                        // use currentProjectQuarter!!!!!
                                        var currentDate = new DateTime(yearForCalculation, currentProjectMonth, 1);
                                        var currentQuarter = currentDate.GetQuarter();
                                        var previousQuarter = (currentQuarter == 1) ? 4 : currentQuarter - 1;

                                        fixedDate = _getPeriodEndDate(yearForCalculation, previousQuarter, "qr");
                                    }
                                    break;

                                case "sticktoprioryearend":
                                    {
                                        fixedDate = new DateTime(currentProjectYear - 1, 12, 31);
                                    }
                                    break;

                                case "sticktoprioryearstart":
                                    {
                                        fixedDate = new DateTime(currentProjectYear - 1, 1, 1);
                                    }
                                    break;

                                case "sticktomonthstart":
                                    {
                                        if (dateWrapperProperties.Month == 12 && currentProjectMonth < 12)
                                        {
                                            // We have passed the year boundary, so we need to add a year to the fixed date
                                            fixedDate = new DateTime(yearForCalculation + 1, currentProjectMonth, 1);
                                        }
                                        else
                                        {
                                            fixedDate = new DateTime(yearForCalculation, currentProjectMonth, 1);
                                        }
                                    }
                                    break;

                                case "sticktoquarterstart":
                                    {

                                        DateTime currentDate = new DateTime();
                                        if (dateWrapperProperties.Month == 10 && currentProjectMonth < 10)
                                        {
                                            // We have passed the year boundary, so we need to add a year to the fixed date
                                            currentDate = new DateTime(yearForCalculation + 1, currentProjectMonth, 1);
                                        }
                                        else
                                        {
                                            currentDate = new DateTime(yearForCalculation, currentProjectMonth, 1);
                                        }

                                        fixedDate = currentDate.GetQuarterStartDate();
                                    }
                                    break;

                                case "sticktoyearend":
                                    {
                                        fixedDate = new DateTime(currentProjectYear, 12, 31);
                                    }
                                    break;

                                case "sticktoyearstart":
                                    {
                                        fixedDate = new DateTime(currentProjectYear, 1, 1);
                                    }
                                    break;

                                default:
                                    appLogger.LogWarning("dateShiftProceessingInstruction '{dateShiftProceessingInstruction}' not supported yet", dateShiftProceessingInstruction);
                                    break;
                            }
                        }
                    }


                    // Parsed date fragments from the RegExp
                    var cellContentDay = 0;
                    var cellContentMonth = 0;
                    var cellContentQuarter = 0;
                    var cellContentYear = 0;

                    var cellContentStartDay = 0;
                    var cellContentStartMonth = 0;
                    var cellContentStartYear = 0;

                    string newDate = "";


                    if (dateWrapperProperties.Flexible)
                    {
                        double offset = 0;
                        double offsetStart = 0;
                        double offsetEnd = 0;

                        var offsetParsed = true;
                        var offsetFormat = dateWrapperProperties.Offset.Substring(dateWrapperProperties.Offset.Length - 1);
                        dateWrapperProperties.Offset = dateWrapperProperties.Offset.Remove(dateWrapperProperties.Offset.Length - 1);

                        if (dateWrapperProperties.Offset.Contains(","))
                        {
                            var counter = 0;
                            string[] offsets = dateWrapperProperties.Offset.Split(",");
                            foreach (string offsetString in offsets)
                            {
                                double offsetOut = 0;
                                if (!double.TryParse(offsetString, out offsetOut))
                                {
                                    offsetParsed = false;
                                }
                                else
                                {
                                    if (counter == 0)
                                    {
                                        offsetStart = offsetOut;
                                    }
                                    else
                                    {
                                        offsetEnd = offsetOut;
                                    }
                                }
                                counter++;
                            }
                        }
                        else
                        {
                            if (!double.TryParse(dateWrapperProperties.Offset, out offset))
                            {
                                offsetParsed = false;
                            }
                        }



                        if (offsetParsed)
                        {
                            switch (dateWrapperProperties.Periodtype)
                            {
                                case "year":
                                    switch (dateWrapperProperties.Format)
                                    {
                                        case "yyyy":
                                            cellContentYear = Convert.ToInt32(currentProjectYear + offset);
                                            if (isFixedDate) cellContentYear = fixedDate.Year;
                                            newDate = cellContentYear.ToString();
                                            break;
                                        case "yyyystart - yyyyend":
                                            cellContentStartYear = Convert.ToInt32(currentProjectYear + offsetStart);
                                            cellContentYear = Convert.ToInt32(currentProjectYear + offsetEnd);
                                            if (isFixedDate) cellContentYear = fixedDate.Year;
                                            newDate = cellContentStartYear.ToString() + " - " + cellContentYear.ToString();
                                            break;
                                        case "'yy":
                                            cellContentYear = Convert.ToInt32(currentProjectYear - 2000 + offset);
                                            if (isFixedDate) cellContentYear = fixedDate.Year;
                                            newDate = "'" + cellContentYear.ToString();
                                            break;
                                        case "fy yyyy":
                                            cellContentYear = Convert.ToInt32(currentProjectYear + offset);
                                            if (isFixedDate) cellContentYear = fixedDate.Year;
                                            newDate = "FY " + cellContentYear.ToString();
                                            break;

                                        case "yyyy / yyyy":
                                            cellContentYear = Convert.ToInt32(currentProjectYear + offset);
                                            if (isFixedDate) cellContentYear = fixedDate.Year;
                                            newDate = (cellContentYear - 1).ToString() + " / " + cellContentYear.ToString();
                                            break;

                                        default:
                                            errorBuilder.AppendLine($"Year dateWrapperProperties.Format: {dateWrapperProperties.Format} not supported yet!");
                                            break;
                                    }
                                    break;


                                case "fullquarter":
                                case "shortquarter":
                                    switch (dateWrapperProperties.Format)
                                    {
                                        case "Q yyyy":
                                            if (isFixedDate)
                                            {
                                                newDate = $"{fixedDate.GetQuarter()} {fixedDate.Year}";
                                            }
                                            else
                                            {
                                                newDate = _calculateQuarterFromOffset(offset, currentProjectYear, currentProjectQuarter, true, ref cellContentDay, ref cellContentMonth, ref cellContentQuarter, ref cellContentYear);
                                            }
                                            break;
                                        case "Q":
                                            if (isFixedDate)
                                            {
                                                newDate = $"{fixedDate.GetQuarter()}";
                                            }
                                            else
                                            {
                                                newDate = _calculateQuarterFromOffset(offset, currentProjectYear, currentProjectQuarter, false, ref cellContentDay, ref cellContentMonth, ref cellContentQuarter, ref cellContentYear);
                                            }

                                            break;
                                        case "yyyy_q":
                                            if (isFixedDate)
                                            {
                                                newDate = $"{fixedDate.Year}_{fixedDate.GetQuarter()}";
                                            }
                                            else
                                            {
                                                var tempDate = _calculateQuarterFromOffset(offset, currentProjectYear, currentProjectQuarter, true, ref cellContentDay, ref cellContentMonth, ref cellContentQuarter, ref cellContentYear).ToLower();
                                                newDate = tempDate.SubstringAfter(" ") + "_" + tempDate.SubstringBefore(" ");
                                            }
                                            break;

                                        default:
                                            errorBuilder.AppendLine($"Full/short quarter dateWrapperProperties.Format: {dateWrapperProperties.Format} not supported yet!");
                                            break;
                                    }
                                    break;


                                case "halfyear":
                                    switch (dateWrapperProperties.Format)
                                    {
                                        case "HY yyyy":
                                            {
                                                // Calculate new half-year
                                                // HY refers to half-year, similar to quarter but 6-month periods
                                                var referenceDate = _getPeriodEndDate(
                                                    currentProjectYear,
                                                    currentProjectQuarter,
                                                    projectType
                                                );

                                                // Calculate which half year based on offset
                                                var dateCellForCalculation = new DateTime();

                                                switch (offsetFormat)
                                                {
                                                    case "q":
                                                        // Quarters to half-years: use quarter-based calculation
                                                        dateCellForCalculation = referenceDate.AddQuarters(Convert.ToInt32(offset));
                                                        break;
                                                    case "y":
                                                        // Years to half-years
                                                        dateCellForCalculation = referenceDate.AddYears(Convert.ToInt32(offset));
                                                        break;
                                                    case "m":
                                                        // Months to half-years
                                                        dateCellForCalculation = referenceDate.AddMonths(Convert.ToInt32(offset));
                                                        break;
                                                    default:
                                                        appLogger.LogWarning($"Unsupported offset format for halfyear: {offsetFormat}");
                                                        break;
                                                }

                                                cellContentYear = isFixedDate ? fixedDate.Year : dateCellForCalculation.Year;
                                                newDate = $"HY {cellContentYear}";
                                            }
                                            break;

                                        default:
                                            errorBuilder.AppendLine($"Half year dateWrapperProperties.Format: {dateWrapperProperties.Format} not supported yet!");
                                            break;
                                    }
                                    break;


                                case "writtendate":
                                    switch (dateWrapperProperties.Format)
                                    {
                                        case "MMM. d, yyyy":
                                        case "MMMM d, yyyy":
                                        case "MMM. yyyy":
                                        case "MMMM yyyy":
                                        case "MMMM, yyyy":
                                        case "dd/MM/yyyy":
                                        case "dd-MM-yyyy":
                                        case "d MMMM yyyy":
                                            {
                                                // Calculate a new date based on the offset
                                                // Console.WriteLine("for debugger");
                                                var referenceDate = _getPeriodEndDate(currentProjectYear, (currentProjectQuarter == 0) ? currentProjectMonth : currentProjectQuarter, projectType);

                                                DateTime dateCell = new DateTime();
                                                bool isDutchDate = _containsDutchMonthName(dateWrapperProperties.InnerText);

                                                if (isDutchDate)
                                                {
                                                    // Parse Dutch date manually: "1 januari 2021"
                                                    var parts = dateWrapperProperties.InnerText.Split(' ');
                                                    if (parts.Length == 3)
                                                    {
                                                        int day = int.Parse(parts[0]);
                                                        int month = _getMonthInt(parts[1]);
                                                        int year = int.Parse(parts[2]);
                                                        dateCell = new DateTime(year, month, day);
                                                    }
                                                }
                                                else if (dateWrapperProperties.Format == "MMM. yyyy" && dateWrapperProperties.InnerText.Contains(","))
                                                {
                                                    dateCell = DateTime.ParseExact(dateWrapperProperties.InnerText, "MMMM, yyyy", System.Globalization.CultureInfo.InvariantCulture);
                                                }
                                                else
                                                {
                                                    dateCell = DateTime.ParseExact(dateWrapperProperties.InnerText, dateWrapperProperties.Format, System.Globalization.CultureInfo.InvariantCulture);
                                                }

                                                //var dateCellForCalculation = new DateTime(currentProjectYear, dateCell.Month, dateCell.Day);
                                                //
                                                var dateCellForCalculation = new DateTime();

                                                switch (offsetFormat)
                                                {
                                                    case "q":
                                                        dateCellForCalculation = referenceDate.AddQuarters(Convert.ToInt32(offset));
                                                        break;

                                                    case "y":
                                                        if (dateWrapperProperties.Format == "MMM. yyyy" || dateWrapperProperties.Format == "MMMM yyyy")
                                                        {
                                                            dateCellForCalculation = new DateTime(currentProjectYear, dateCell.Month, 1);
                                                        }
                                                        else
                                                        {
                                                            dateCellForCalculation = new DateTime(currentProjectYear, dateCell.Month, dateCell.Day);
                                                        }

                                                        if (dateCell.Month == 12 && dateCell.Day == 31)
                                                        {
                                                            dateCellForCalculation = new DateTime(currentProjectYear, dateCell.Month, dateCell.Day);
                                                        }

                                                        dateCellForCalculation = dateCellForCalculation.AddYears(Convert.ToInt32(offset));
                                                        break;

                                                    case "m":
                                                        dateCellForCalculation = referenceDate.AddMonths(Convert.ToInt32(offset));
                                                        break;

                                                    default:
                                                        appLogger.LogWarning($"Unsupported offset format: {offsetFormat}. stack-trace: {GetStackTrace()}");
                                                        break;
                                                }

                                                cellContentDay = isFixedDate ? fixedDate.Day : dateCellForCalculation.Day;
                                                cellContentMonth = isFixedDate ? fixedDate.Month : dateCellForCalculation.Month;
                                                cellContentYear = isFixedDate ? fixedDate.Year : dateCellForCalculation.Year;

                                                // Format date - use Dutch month names for Dutch dates
                                                if (isDutchDate)
                                                {
                                                    var finalDate = isFixedDate ? fixedDate : dateCellForCalculation;
                                                    newDate = $"{finalDate.Day} {_getDutchMonthName(finalDate.Month)} {finalDate.Year}";
                                                }
                                                else
                                                {
                                                    newDate = isFixedDate ? fixedDate.ToString(dateWrapperProperties.Format) : dateCellForCalculation.ToString(dateWrapperProperties.Format);
                                                }
                                            }
                                            break;

                                        case "MMM d":
                                        case "MMM. d":
                                        case "MMMM d":
                                            {
                                                newDate = dateWrapperProperties.InnerText;

                                                var dateCell = DateTime.ParseExact($"{dateWrapperProperties.InnerText}, {currentProjectYear}", $"{dateWrapperProperties.Format}, yyyy", System.Globalization.CultureInfo.InvariantCulture);
                                                cellContentDay = isFixedDate ? fixedDate.Day : dateCell.Day;
                                                cellContentMonth = isFixedDate ? fixedDate.Month : dateCell.Month;
                                                cellContentYear = 0;

                                                switch (projectType)
                                                {
                                                    case "quarterly-report":
                                                    case "qr":
                                                        {
                                                            var referenceDate = _getPeriodEndDate(currentProjectYear, currentProjectQuarter, projectType);

                                                            //var dateCellForCalculation = new DateTime(currentProjectYear, dateCell.Month, dateCell.Day);
                                                            //
                                                            var dateCellForCalculation = dateCell.AddMonths((int)offset);

                                                            cellContentDay = isFixedDate ? fixedDate.Day : dateCellForCalculation.Day;
                                                            cellContentMonth = isFixedDate ? fixedDate.Month : dateCellForCalculation.Month;
                                                            cellContentYear = 0;

                                                            newDate = isFixedDate ? fixedDate.ToString(dateWrapperProperties.Format) : dateCellForCalculation.ToString(dateWrapperProperties.Format);
                                                        }

                                                        break;

                                                    case "monthly-report":
                                                    case "mr":
                                                        {
                                                            var referenceDate = _getPeriodEndDate(currentProjectYear, currentProjectMonth, projectType);
                                                            var dateCellForCalculation = referenceDate.AddMonths((int)offset);

                                                            cellContentDay = isFixedDate ? fixedDate.Day : dateCellForCalculation.Day;
                                                            cellContentMonth = isFixedDate ? fixedDate.Month : dateCellForCalculation.Month;
                                                            cellContentYear = 0;

                                                            newDate = isFixedDate ? fixedDate.ToString(dateWrapperProperties.Format) : dateCellForCalculation.ToString(dateWrapperProperties.Format);
                                                        }


                                                        break;
                                                    default:
                                                        var dateForDisplay = new DateTime(1970, cellContentMonth, cellContentDay);

                                                        newDate = isFixedDate ? fixedDate.ToString(dateWrapperProperties.Format) : dateForDisplay.ToString(dateWrapperProperties.Format);

                                                        break;

                                                }

                                            }
                                            break;

                                        default:
                                            errorBuilder.AppendLine($"Written date dateWrapperProperties.Format: {dateWrapperProperties.Format} not supported yet!");
                                            break;
                                    }

                                    break;

                                case "runningperiod":
                                case "runningperiodytd":
                                case "runningperiodqtd":
                                case "runningperioditm":
                                    var connectionString = dateWrapperProperties.Format.Contains(" to ") ? " to " : "-";
                                    cellContentMonth = isFixedDate ? fixedDate.Month : dateWrapperProperties.Month;
                                    cellContentStartMonth = dateWrapperProperties.MonthStart;

                                    switch (dateWrapperProperties.Format)
                                    {
                                        case "MMMMdstart to MMMMdend":
                                        case "MMMMdstart to MMMMdend, yyyy":
                                        case "MMMMstart to MMMMend":
                                        case "MMMMstart-MMMMend":

                                            var useDays = dateWrapperProperties.Format.Replace("end", "").Contains("d");
                                            if (useDays)
                                            {
                                                cellContentDay = dateWrapperProperties.Day;
                                                cellContentStartDay = isFixedDate ? fixedDate.Day : dateWrapperProperties.DayStart;
                                            }

                                            var useYears = dateWrapperProperties.Format.Contains(", yyyy");

                                            switch (projectType)
                                            {
                                                case "ar":
                                                    newDate = dateWrapperProperties.InnerText;

                                                    if (useYears)
                                                    {
                                                        cellContentYear = Convert.ToInt32(currentProjectYear + offset);
                                                        if (isFixedDate) cellContentYear = fixedDate.Year;
                                                        newDate = $"{newDate.SubstringBefore(", ")}, {cellContentYear}";
                                                    }
                                                    break;

                                                case "qr":
                                                    {
                                                        // Calculate a new date based on the offset
                                                        DateTime dateProject = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);

                                                        // The new date is the old date plus the difference in months
                                                        var dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offset, offsetFormat));
                                                        cellContentMonth = dateForCalculation.Month;


                                                        if (useDays)
                                                        {
                                                            cellContentDay = dateForCalculation.Day;
                                                            newDate = $"{_getMonthName((isFixedDate ? fixedDate.Month : cellContentStartMonth))} {cellContentStartDay}{connectionString}{dateForCalculation.ToString("MMMM")} {cellContentDay}";
                                                        }
                                                        else
                                                        {
                                                            newDate = $"{_getMonthName((isFixedDate ? fixedDate.Month : cellContentStartMonth))}{connectionString}{dateForCalculation.ToString("MMMM")}";
                                                        }

                                                        if (useYears)
                                                        {
                                                            cellContentYear = dateForCalculation.Year;
                                                            newDate += $", {cellContentYear}";
                                                        }
                                                    }
                                                    break;

                                                case "mr":
                                                    switch (dateWrapperProperties.Periodtype)
                                                    {
                                                        case "runningperioditm":
                                                            {
                                                                // Shift both begin and end months
                                                                DateTime dateProject = useYears ? new DateTime(currentProjectYear, Convert.ToInt32(currentProjectMonth), 1) : new DateTime(1970, Convert.ToInt32(currentProjectMonth), 1);

                                                                // The new date is the old date plus the difference in months
                                                                var dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offset, offsetFormat));
                                                                if (isFixedDate) dateForCalculation = fixedDate;
                                                                cellContentMonth = dateForCalculation.Month;
                                                                cellContentStartMonth = dateForCalculation.Month;

                                                                // if (dateWrapperProperties.Periodtype.EndsWith("ytd"))
                                                                // {
                                                                //     endMonth2 = dateProject.ToString("MMMM");
                                                                // }

                                                                if (useDays)
                                                                {
                                                                    // TODO: Buggy solution that doesn't work for February
                                                                    cellContentDay = DateTime.DaysInMonth(1970, cellContentMonth);
                                                                    newDate = $"{dateForCalculation.ToString("MMMM")} {cellContentStartDay}{connectionString}{dateForCalculation.ToString("MMMM")} {cellContentDay}";
                                                                }
                                                                else
                                                                {
                                                                    newDate = $"{dateForCalculation.ToString("MMMM")}{connectionString}{dateForCalculation.ToString("MMMM")}";
                                                                }

                                                                if (useYears)
                                                                {
                                                                    cellContentYear = dateForCalculation.Year;
                                                                    newDate += $", {cellContentYear}";
                                                                }
                                                            }
                                                            break;

                                                        case "runningperiodqtd":
                                                            {
                                                                // Shift end month and potentially start month if in another quarter
                                                                // Calculate a new date based on the offset
                                                                DateTime dateProject = useYears ? new DateTime(currentProjectYear, Convert.ToInt32(currentProjectMonth), 1) : new DateTime(1970, Convert.ToInt32(currentProjectMonth), 1);

                                                                // The new date is the old date plus the difference in months
                                                                var dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offset, offsetFormat));
                                                                cellContentMonth = dateForCalculation.Month;

                                                                var dateStartCalculation = isFixedDate ? fixedDate : dateForCalculation;
                                                                cellContentStartMonth = dateStartCalculation.GetQuarterStartDate().Month;

                                                                if (useDays)
                                                                {
                                                                    cellContentDay = DateTime.DaysInMonth(1970, cellContentMonth);
                                                                    cellContentStartDay = isFixedDate ? fixedDate.Day : cellContentStartDay;
                                                                    newDate = $"{dateStartCalculation.GetQuarterStartDate().ToString("MMMM")} {cellContentStartDay}{connectionString}{dateForCalculation.ToString("MMMM")} {cellContentDay}";
                                                                }
                                                                else
                                                                {
                                                                    newDate = $"{dateStartCalculation.GetQuarterStartDate().ToString("MMMM")}{connectionString}{dateForCalculation.ToString("MMMM")}";
                                                                }

                                                                if (useYears)
                                                                {
                                                                    cellContentYear = dateForCalculation.Year;
                                                                    newDate += $", {cellContentYear}";
                                                                }
                                                            }
                                                            break;

                                                        case "runningperiodytd":
                                                            {
                                                                // Only move the end date
                                                                // Calculate a new date based on the offset
                                                                DateTime dateProject = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");

                                                                // The new date is the old date plus the difference in months
                                                                var dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offset, offsetFormat));
                                                                cellContentMonth = dateForCalculation.Month;
                                                                cellContentStartMonth = 1;

                                                                if (useDays)
                                                                {
                                                                    cellContentDay = DateTime.DaysInMonth(1970, cellContentMonth);
                                                                    cellContentStartDay = 1;
                                                                    newDate = $"{_getMonthName(cellContentStartMonth)} {cellContentStartDay}{connectionString}{dateForCalculation.ToString("MMMM")} {cellContentDay}";
                                                                }
                                                                else
                                                                {
                                                                    newDate = $"{_getMonthName(cellContentStartMonth)}{connectionString}{dateForCalculation.ToString("MMMM")}";
                                                                }

                                                                if (useYears)
                                                                {
                                                                    cellContentYear = dateForCalculation.Year;
                                                                    newDate += $", {cellContentYear}";
                                                                }
                                                            }
                                                            break;

                                                        default:
                                                            {
                                                                // Move start and end date

                                                                // Calculate a new date based on the offset
                                                                DateTime dateProject = _getPeriodEndDate(currentProjectYear, currentProjectMonth, "mr");

                                                                // The new date is the old date plus the difference in months
                                                                var dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offset, offsetFormat));
                                                                if (isFixedDate) dateForCalculation = fixedDate;
                                                                cellContentMonth = dateForCalculation.Month;

                                                                dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offsetStart, offsetFormat));
                                                                cellContentStartMonth = dateForCalculation.Month;
                                                                cellContentStartMonth = isFixedDate ? fixedDate.Month : cellContentStartMonth;

                                                                if (useDays)
                                                                {
                                                                    cellContentDay = DateTime.DaysInMonth(1970, cellContentMonth);
                                                                    cellContentStartDay = isFixedDate ? fixedDate.Day : cellContentStartDay;
                                                                    newDate = $"{_getMonthName(cellContentStartMonth)} {cellContentStartDay}{connectionString}{dateForCalculation.ToString("MMMM")} {cellContentDay}";
                                                                }
                                                                else
                                                                {
                                                                    newDate = $"{_getMonthName(cellContentStartMonth)}{connectionString}{dateForCalculation.ToString("MMMM")}";
                                                                }

                                                                if (useYears)
                                                                {
                                                                    cellContentYear = dateForCalculation.Year;
                                                                    newDate += $", {cellContentYear}";
                                                                }
                                                            }
                                                            break;

                                                    }

                                                    break;

                                                default:
                                                    errorBuilder.AppendLine($"Project type: {projectType} not supported yet!");
                                                    break;
                                            }

                                            break;


                                        default:
                                            errorBuilder.AppendLine($"Running period dateWrapperProperties.Format: {dateWrapperProperties.Format} not supported yet!");
                                            break;
                                    }

                                    break;

                                case "yearonyear":
                                    switch (dateWrapperProperties.Format)
                                    {
                                        case "yyyystart versus yyyyend":
                                            // Calculate a new date based on the offset
                                            cellContentStartYear = Convert.ToInt32(currentProjectYear + offsetStart);
                                            cellContentYear = Convert.ToInt32(currentProjectYear + offsetEnd);

                                            newDate = cellContentStartYear.ToString() + " versus " + cellContentYear.ToString();
                                            break;

                                        case "dd-MM-yyyy  dd-MM-yyyy":
                                            {
                                                // Calculate start and end dates based on their respective offsets
                                                // offsetStart and offsetEnd are already parsed from comma-separated offset string

                                                // Get reference date for calculations
                                                var referenceDate = _getPeriodEndDate(currentProjectYear, (currentProjectQuarter == 0) ? currentProjectMonth : currentProjectQuarter, projectType);

                                                // Calculate start date
                                                var dateStartForCalculation = new DateTime(currentProjectYear, dateWrapperProperties.MonthStart, dateWrapperProperties.DayStart);

                                                switch (offsetFormat)
                                                {
                                                    case "q":
                                                        dateStartForCalculation = referenceDate.AddQuarters(Convert.ToInt32(offsetStart));
                                                        break;
                                                    case "y":
                                                        dateStartForCalculation = dateStartForCalculation.AddYears(Convert.ToInt32(offsetStart));
                                                        break;
                                                    case "m":
                                                        dateStartForCalculation = referenceDate.AddMonths(Convert.ToInt32(offsetStart));
                                                        break;
                                                    default:
                                                        appLogger.LogWarning($"Unsupported offset format for start date: {offsetFormat}. stack-trace: {GetStackTrace()}");
                                                        break;
                                                }

                                                // Calculate end date
                                                var dateEndForCalculation = new DateTime(currentProjectYear, dateWrapperProperties.Month, dateWrapperProperties.Day);

                                                switch (offsetFormat)
                                                {
                                                    case "q":
                                                        dateEndForCalculation = referenceDate.AddQuarters(Convert.ToInt32(offsetEnd));
                                                        break;
                                                    case "y":
                                                        dateEndForCalculation = dateEndForCalculation.AddYears(Convert.ToInt32(offsetEnd));
                                                        break;
                                                    case "m":
                                                        dateEndForCalculation = referenceDate.AddMonths(Convert.ToInt32(offsetEnd));
                                                        break;
                                                    default:
                                                        appLogger.LogWarning($"Unsupported offset format for end date: {offsetFormat}. stack-trace: {GetStackTrace()}");
                                                        break;
                                                }

                                                // Update cell content variables
                                                cellContentStartDay = isFixedDate ? fixedDate.Day : dateStartForCalculation.Day;
                                                cellContentStartMonth = isFixedDate ? fixedDate.Month : dateStartForCalculation.Month;
                                                cellContentStartYear = isFixedDate ? fixedDate.Year : dateStartForCalculation.Year;
                                                cellContentDay = isFixedDate ? fixedDate.Day : dateEndForCalculation.Day;
                                                cellContentMonth = isFixedDate ? fixedDate.Month : dateEndForCalculation.Month;
                                                cellContentYear = isFixedDate ? fixedDate.Year : dateEndForCalculation.Year;

                                                // Format output: "dd-MM-yyyy  dd-MM-yyyy" (with 2 spaces between dates)
                                                newDate = $"{cellContentStartDay:D2}-{cellContentStartMonth:D2}-{cellContentStartYear}  {cellContentDay:D2}-{cellContentMonth:D2}-{cellContentYear}";
                                            }
                                            break;

                                        default:
                                            errorBuilder.AppendLine($"Year on year period dateWrapperProperties.Format: {dateWrapperProperties.Format} not supported yet!");
                                            break;
                                    }

                                    break;

                                case "longperiod":
                                case "longperioditm":
                                case "longperiodqtd":
                                case "longperiodytd":
                                case "longperiodfullyear":
                                    {
                                        var newYear = "";
                                        var startMonth = "";
                                        var endMonth = "";
                                        var startDay = "";
                                        var endDay = "";
                                        var useDays = dateWrapperProperties.Format.Replace("end", "").Contains("d");

                                        // Calculate a new date based on the offset
                                        var referenceDate = _getPeriodEndDate(currentProjectYear, (currentProjectQuarter == 0) ? currentProjectMonth : currentProjectQuarter, projectType);


                                        switch (dateWrapperProperties.Periodtype)
                                        {

                                            // case "":


                                            //     break;


                                            case "longperiodfullyear":
                                                {
                                                    startMonth = "January";
                                                    endMonth = "December";
                                                    startDay = "1";
                                                    endDay = "31";

                                                    // Calculate the year we need to use
                                                    var dateCellForCalculation = new DateTime();
                                                    switch (offsetFormat)
                                                    {
                                                        case "q":
                                                            dateCellForCalculation = referenceDate.AddQuarters(Convert.ToInt32(offset));
                                                            break;

                                                        case "y":
                                                            dateCellForCalculation = referenceDate.AddYears(Convert.ToInt32(offset));
                                                            break;

                                                        case "m":
                                                            dateCellForCalculation = referenceDate.AddMonths(Convert.ToInt32(offset));
                                                            break;

                                                        default:
                                                            appLogger.LogWarning($"Unsupported offset format: {offsetFormat}. stack-trace: {GetStackTrace()}");
                                                            break;
                                                    }

                                                    newYear = isFixedDate ? fixedDate.Year.ToString() : dateCellForCalculation.Year.ToString();
                                                }


                                                break;

                                            default:

                                                {

                                                    switch (projectType)
                                                    {
                                                        case "ar":
                                                        case "qr":
                                                            if (offset % 0.5 == 0)
                                                            {
                                                                if (currentProjectQuarter == 1 || currentProjectQuarter == 2)
                                                                {
                                                                    newYear = (currentProjectYear + Math.Floor(offset)).ToString();
                                                                }
                                                                else
                                                                {
                                                                    newYear = (currentProjectYear + Math.Ceiling(offset)).ToString();
                                                                }
                                                            }
                                                            else
                                                            {
                                                                newYear = (currentProjectYear + offset).ToString();
                                                            }
                                                            if (isFixedDate) newYear = fixedDate.Year.ToString();


                                                            if (dateWrapperProperties.Format == "MMMMstart to MMMMend yyyy" && offsetFormat == "y" && (offset % 1 == 0))
                                                            {
                                                                startMonth = _getMonthName(isFixedDate ? fixedDate.Month : dateWrapperProperties.MonthStart);
                                                                endMonth = _getMonthName(dateWrapperProperties.Month);
                                                                cellContentDay = DateTime.DaysInMonth(Convert.ToInt32(newYear), dateWrapperProperties.Month);
                                                                cellContentStartDay = 1;
                                                                endDay = cellContentDay.ToString();
                                                                startDay = cellContentStartDay.ToString();
                                                            }
                                                            else
                                                            {
                                                                if (offset % 1 == 0)
                                                                {
                                                                    if (currentProjectQuarter == 1 || currentProjectQuarter == 2)
                                                                    {
                                                                        startMonth = "January";
                                                                        endMonth = "June";
                                                                    }
                                                                    else
                                                                    {
                                                                        startMonth = "July";
                                                                        endMonth = "December";
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    if (offset % 0.5 == 0)
                                                                    {
                                                                        if (currentProjectQuarter == 1 || currentProjectQuarter == 2)
                                                                        {
                                                                            startMonth = "July";
                                                                            endMonth = "December";
                                                                        }
                                                                        else
                                                                        {
                                                                            startMonth = "January";
                                                                            endMonth = "June";
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        errorBuilder.AppendLine($"Long period with offset: {offset.ToString()} not supported!");
                                                                    }
                                                                }
                                                            }

                                                            // Year to date
                                                            if (dateWrapperProperties.Periodtype.EndsWith("ytd"))
                                                            {
                                                                // Fix the start month
                                                                startMonth = "January";


                                                                DateTime dateProject = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                                                                endMonth = dateProject.ToString("MMMM");
                                                            }

                                                            if (!string.IsNullOrEmpty(dateWrapperProperties.OffsetStart) && dateWrapperProperties.OffsetStart.EndsWith("m"))
                                                            {
                                                                // We need to shift the start month too
                                                                var offsetInMonths = Int32.Parse(dateWrapperProperties.OffsetStart.TrimEnd('m'));
                                                                startMonth = new DateTime(2010, _getMonthInt(startMonth), 1).AddMonths(offsetInMonths).ToString("MMMM");
                                                            }


                                                            cellContentStartMonth = isFixedDate ? fixedDate.Month : _getMonthInt(startMonth);
                                                            if (isFixedDate)
                                                            {
                                                                startMonth = fixedDate.ToString("MMMM");
                                                            }
                                                            cellContentMonth = _getMonthInt(endMonth);

                                                            cellContentDay = DateTime.DaysInMonth(Convert.ToInt32(newYear), cellContentMonth);
                                                            cellContentStartDay = DateTime.DaysInMonth(Convert.ToInt32(newYear), cellContentMonth);
                                                            if (dateWrapperProperties.Periodtype.EndsWith("ytd")) cellContentStartDay = 1;
                                                            endDay = cellContentDay.ToString();
                                                            startDay = isFixedDate ? fixedDate.Day.ToString() : cellContentStartDay.ToString();


                                                            break;

                                                        case "mr":
                                                            // Calculate the year we need to use
                                                            var dateCellForCalculation = new DateTime();
                                                            switch (offsetFormat)
                                                            {
                                                                case "q":
                                                                    dateCellForCalculation = referenceDate.AddQuarters(Convert.ToInt32(offset));
                                                                    break;

                                                                case "y":
                                                                    dateCellForCalculation = referenceDate.AddYears(Convert.ToInt32(offset));
                                                                    break;

                                                                case "m":
                                                                    dateCellForCalculation = referenceDate.AddMonths(Convert.ToInt32(offset));
                                                                    break;

                                                                default:
                                                                    appLogger.LogWarning($"Unsupported offset format: {offsetFormat}. stack-trace: {GetStackTrace()}");
                                                                    break;
                                                            }

                                                            newYear = isFixedDate ? fixedDate.Year.ToString() : dateCellForCalculation.Year.ToString();

                                                            // Calculate the months we need to use
                                                            switch (dateWrapperProperties.Periodtype)
                                                            {
                                                                case "longperioditm":
                                                                    {
                                                                        // Shift both begin and end months
                                                                        DateTime dateProject = new DateTime(dateCellForCalculation.Year, Convert.ToInt32(currentProjectMonth), 1);

                                                                        // The new date is the old date plus the difference in months
                                                                        var dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offset, offsetFormat));
                                                                        if (isFixedDate) dateForCalculation = fixedDate;
                                                                        cellContentMonth = dateForCalculation.Month;
                                                                        cellContentStartMonth = dateForCalculation.Month;

                                                                        cellContentDay = DateTime.DaysInMonth(Convert.ToInt32(newYear), cellContentMonth);
                                                                        cellContentStartDay = 1;
                                                                    }
                                                                    break;

                                                                case "longperiodqtd":
                                                                    {
                                                                        // Shift end month and start month if in another quarter
                                                                        // Calculate a new date based on the offset
                                                                        DateTime dateProject = new DateTime(dateCellForCalculation.Year, Convert.ToInt32(currentProjectMonth), 1);

                                                                        // The new date is the old date plus the difference in months
                                                                        var dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offset, offsetFormat));

                                                                        cellContentMonth = dateForCalculation.Month;

                                                                        var dateStartCalculation = isFixedDate ? fixedDate : dateForCalculation;
                                                                        cellContentStartMonth = dateStartCalculation.GetQuarterStartDate().Month;

                                                                        cellContentDay = DateTime.DaysInMonth(Convert.ToInt32(newYear), cellContentMonth);
                                                                        cellContentStartDay = isFixedDate ? fixedDate.Day : 1;
                                                                    }
                                                                    break;

                                                                case "longperiodytd":
                                                                    {
                                                                        // Only move the end date
                                                                        // Calculate a new date based on the offset
                                                                        DateTime dateProject = _getPeriodEndDate(dateCellForCalculation.Year, currentProjectMonth, "mr");

                                                                        // The new date is the old date plus the difference in months
                                                                        var dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offset, offsetFormat));
                                                                        cellContentMonth = dateForCalculation.Month;
                                                                        cellContentStartMonth = 1;

                                                                        cellContentDay = DateTime.DaysInMonth(Convert.ToInt32(newYear), cellContentMonth);
                                                                        cellContentStartDay = 1;
                                                                    }
                                                                    break;

                                                                default:
                                                                    {
                                                                        // Move start and end date
                                                                        // Calculate a new date based on the offset
                                                                        DateTime dateProject = _getPeriodEndDate(dateCellForCalculation.Year, currentProjectMonth, "mr");

                                                                        // The new date is the old date plus the difference in months
                                                                        var dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offset, offsetFormat));
                                                                        cellContentMonth = dateForCalculation.Month;

                                                                        dateForCalculation = dateProject.AddMonths(CalculateOffsetInMonths(offsetStart, offsetFormat));
                                                                        cellContentStartMonth = isFixedDate ? fixedDate.Month : dateForCalculation.Month;

                                                                        cellContentDay = DateTime.DaysInMonth(Convert.ToInt32(newYear), cellContentMonth);
                                                                        cellContentStartDay = isFixedDate ? fixedDate.Day : 1;
                                                                    }
                                                                    break;
                                                            }

                                                            // Convert the month integers to a date string
                                                            endMonth = _getMonthName(cellContentMonth, dateWrapperProperties.Format.Contains("MMMMstart") || dateWrapperProperties.Format.Contains("MMMMdstart"));
                                                            startMonth = _getMonthName(cellContentStartMonth, dateWrapperProperties.Format.Contains("MMMMstart") || dateWrapperProperties.Format.Contains("MMMMdstart"));

                                                            cellContentDay = DateTime.DaysInMonth(Convert.ToInt32(newYear), cellContentMonth);
                                                            endDay = cellContentDay.ToString();
                                                            startDay = cellContentStartDay.ToString();

                                                            break;
                                                    }


                                                    if (!int.TryParse(newYear, out cellContentYear)) { }

                                                }

                                                break;


                                        }


                                        // Format the output
                                        switch (dateWrapperProperties.Format)
                                        {
                                            case "MMMMstart to MMMMend":
                                                newDate = $"{startMonth} to {endMonth}";
                                                break;

                                            case "MMMMstart to MMMMend, yyyy":
                                            case "MMMMstart to MMMMend yyyy":
                                            case "MMMstart to MMMend yyyy":
                                                newDate = $"{startMonth} to {endMonth}{(dateWrapperProperties.Format.Contains(",") ? "," : "")} {newYear}";
                                                break;

                                            case "MMM.start to MMM.end yyyy":
                                                newDate = $"{startMonth}. to {endMonth}.{(dateWrapperProperties.Format.Contains(",") ? "," : "")} {newYear}";
                                                break;

                                            case "MMMMdstart to MMMMdend":
                                                newDate = $"{startMonth} {startDay} to {endMonth} {endDay}";
                                                break;

                                            case "MMMMdstart to MMMMdend yyyy":
                                            case "MMMMdstart to MMMMdend, yyyy":
                                                newDate = $"{startMonth} {startDay} to {endMonth} {endDay}{(dateWrapperProperties.Format.Contains(",") ? "," : "")} {newYear}";
                                                break;

                                            case "YTD":
                                                newDate = "YTD";
                                                break;

                                            case "YTD yyyy":
                                                newDate = $"YTD {newYear}";
                                                break;

                                            default:
                                                errorBuilder.AppendLine($"Long period dateWrapperProperties.Format: {dateWrapperProperties.Format} not supported yet!");
                                                break;
                                        }

                                    }
                                    break;

                                default:
                                    errorBuilder.AppendLine($"periodType: {dateWrapperProperties.Periodtype} not supported yet!");
                                    break;
                            }
                        }
                        else
                        {
                            errorBuilder.AppendLine($"Could not parse offset");
                        }
                    }



                    if (!string.IsNullOrEmpty(newDate))
                    {
                        try
                        {
                            // Inject the new date
                            switch (newDate)
                            {
                                case string x when x.Contains("Sept."):
                                    newDate = newDate.Replace("Sept.", "Sep.");
                                    break;
                                case string x when x.Contains("May."):
                                    newDate = newDate.Replace("May.", "May,");
                                    break;
                            }
                            nodeDynamicDate.InnerText = newDate;

                            // Update the date fragments
                            nodeDynamicDate.SetAttribute("data-dateday", cellContentDay.ToString());
                            nodeDynamicDate.SetAttribute("data-datemonth", cellContentMonth.ToString());
                            nodeDynamicDate.SetAttribute("data-datequarter", cellContentQuarter.ToString());
                            nodeDynamicDate.SetAttribute("data-dateyear", cellContentYear.ToString());
                            nodeDynamicDate.SetAttribute("data-dateformat", dateWrapperProperties.Format);

                            if (cellContentStartDay > 0 || cellContentStartMonth > 0 || cellContentStartYear > 0)
                            {
                                nodeDynamicDate.SetAttribute("data-datestartday", cellContentStartDay.ToString());
                                nodeDynamicDate.SetAttribute("data-datestartmonth", cellContentStartMonth.ToString());
                                nodeDynamicDate.SetAttribute("data-datestartyear", cellContentStartYear.ToString());
                            }

                            debugBuilder.AppendLine($"currentDate: {dateWrapperProperties.InnerText} => newDate: {newDate} (Flexible: {dateWrapperProperties.Flexible} - Periodtype: {dateWrapperProperties.Periodtype} - Format: {dateWrapperProperties.Format}, Offset: {dateWrapperProperties.Offset})");
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"There was an error inserting the new migrated date value. newDate: {newDate}");
                        }
                    }
                    else
                    {
                        if (dateWrapperProperties.Flexible)
                        {
                            errorBuilder.AppendLine($"DATE NOT SHIFTED (InnerText: {dateWrapperProperties.InnerText} - Flexible: {dateWrapperProperties.Flexible} - Periodtype: {dateWrapperProperties.Periodtype} - Format: {dateWrapperProperties.Format}, Offset: {dateWrapperProperties.Offset}, articleId: {articleId}, lang: {contentLang})");
                        }
                    }



                }
                debugBuilder.AppendLine("");

            }

            // Log some content if needed
            if (debugRoutine)
            {
                char[] trimChars = { ',', ' ' };
                appLogger.LogInformation(Environment.NewLine + debugBuilder.ToString().TrimEnd(trimChars));
            }

            if (errorBuilder.Length > 0)
            {
                appLogger.LogError("!! Errors detected during period processing !!");
                appLogger.LogError(errorBuilder.ToString());
            }

            return xmlFilingContentSourceData;

            static int CalculateOffsetInMonths(double offsetValue, string offsetFormat)
            {
                switch (offsetFormat)
                {
                    case "y":
                        return Convert.ToInt32(offsetValue * 12);

                    case "q":
                        return Convert.ToInt32(offsetValue * 4);

                    case "m":
                        return Convert.ToInt32(offsetValue);

                    case "d":
                        return Convert.ToInt32(Math.Floor(offsetValue / 30));

                    default:
                        appLogger.LogWarning($"Unknown offset format {offsetFormat}");
                        return 0;
                }
            }
        }



    }
}