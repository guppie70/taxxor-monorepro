using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    /// <summary>
    /// Date wrapper properties class
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {
        public class DateWrapperProperties
        {
            public XmlNode NodeWrapper { get; }
            public bool WrapperParsed { get; set; } = false;

            public bool Flexible { get; set; }

            public string Periodtype { get; set; }

            public string Format { get; set; }

            public string Offset { get; set; }
            public string OffsetStart { get; set; }

            public string InnerText { get; set; }

            public int Day { get; } = 0;
            public int Month { get; } = 0;
            public int Quarter { get; } = 0;
            public int Year { get; } = 0;
            public int DayStart { get; } = 0;
            public int MonthStart { get; } = 0;
            public int YearStart { get; } = 0;


            public string? PeriodStart { get; set; } = null;
            public string? PeriodEnd { get; set; } = null;

            private string _dateWrapperContent { get; set; } = "";

            public DateWrapperProperties? Context { get; set; } = null;


            public DateWrapperProperties(XmlNode nodeDateWrapper)
            {
                this.NodeWrapper = nodeDateWrapper;
                this._dateWrapperContent = nodeDateWrapper.OuterXml;

                // Fixed properties
                var dateFlexible = nodeDateWrapper.GetAttribute("data-dateflexible") ?? "";
                this.Flexible = dateFlexible == "true";
                this.Periodtype = nodeDateWrapper.GetAttribute("data-dateperiodtype") ?? "";
                this.Format = nodeDateWrapper.GetAttribute("data-dateformat") ?? "";
                this.Offset = nodeDateWrapper.GetAttribute("data-dateoffset") ?? "";
                this.OffsetStart = nodeDateWrapper.GetAttribute("data-dateoffsetstart") ?? "";
                this.InnerText = nodeDateWrapper?.InnerText ?? "";

                // Parse date fragments
                int day = 0;
                if (int.TryParse(nodeDateWrapper.GetAttribute("data-dateday"), out day)) { }
                this.Day = day;

                int month = 0;
                if (int.TryParse(nodeDateWrapper.GetAttribute("data-datemonth"), out month)) { }
                this.Month = month;

                int quarter = 0;
                if (int.TryParse(nodeDateWrapper.GetAttribute("data-datequarter"), out quarter)) { }
                this.Quarter = quarter;

                int year = 0;
                if (int.TryParse(nodeDateWrapper.GetAttribute("data-dateyear"), out year)) { }
                this.Year = year;

                // Optional date fragments
                int dayStart = 0;
                if (int.TryParse(nodeDateWrapper.GetAttribute("data-datestartday"), out dayStart)) { }
                this.DayStart = dayStart;

                int monthStart = 0;
                if (int.TryParse(nodeDateWrapper.GetAttribute("data-datestartmonth"), out monthStart)) { }
                this.MonthStart = monthStart;

                int yearStart = 0;
                if (int.TryParse(nodeDateWrapper.GetAttribute("data-datestartyear"), out yearStart)) { }
                this.YearStart = yearStart;

                // Boolean to indicate if we found sensible data in the wrapper
                if (this.Periodtype != "" && this.InnerText != "") this.WrapperParsed = true;
            }

            public DateWrapperProperties(string innerText)
            {
                this.Flexible = true;
                this.Periodtype = "";
                this.Format = "";
                this.Offset = "";
                this.InnerText = innerText.Trim();
            }

            public TaxxorReturnMessage CalculatePeriodFragments(ProjectPeriodProperties projectPeriodMetadata, XmlNode nodeSectionContent = null)
            {
                var dateStartCreated = false;
                DateTime dateStartCalculated = new DateTime();
                var dateEndCreated = false;
                DateTime dateEndCalculated = new DateTime();


                var previousQuarter = 0;
                var previousYear = 0;

                try
                {

                    if (this.WrapperParsed)
                    {
                        switch (this.Periodtype)
                        {
                            case "year":
                                switch (this.Format)
                                {
                                    case "'yy":
                                    case "yyyy":
                                    case "fy yyyy":
                                        var periodVar = (projectPeriodMetadata.ProjectType == "ar") ? 0 : (projectPeriodMetadata.ProjectType == "qr") ? projectPeriodMetadata.CurrentProjectQuarter : (projectPeriodMetadata.ProjectType == "mr") ? projectPeriodMetadata.CurrentProjectMonth : 0;

                                        dateEndCalculated = _getPeriodEndDate(this.Year, periodVar, projectPeriodMetadata.ProjectType);
                                        dateStartCalculated = new DateTime(this.Year, 1, 1);
                                        dateStartCreated = true;
                                        dateEndCreated = true;
                                        this.PeriodEnd = dateEndCalculated.ToString("yyyyMMdd");
                                        this.PeriodStart = dateStartCalculated.ToString("yyyyMMdd");
                                        if (projectPeriodMetadata.ProjectType == "ar")
                                        {

                                        }

                                        // Add context if there was any
                                        if (this.Context?.NodeWrapper != null)
                                        {
                                            if (this.Context.WrapperParsed)
                                            {
                                                switch (this.Context.Periodtype)
                                                {
                                                    case "fullyear":
                                                        dateStartCalculated = new DateTime(dateStartCalculated.Year, 1, 1);
                                                        dateEndCalculated = RenderPeriodEnd(dateEndCalculated.Year, 12, 31);
                                                        break;

                                                    case "shortquarter":
                                                        // We need to use the year we found in combination with the quarter context

                                                        // - The end date is the end date of the quarted of this year
                                                        dateEndCalculated = _getPeriodEndDate(this.Year, this.Context.Quarter);

                                                        // - The start date is the end date of the previous quarter with two seconds added
                                                        previousQuarter = (this.Context.Quarter == 1) ? 4 : this.Context.Quarter - 1;
                                                        previousYear = (this.Context.Quarter == 1) ? this.Year - 1 : this.Year;
                                                        dateStartCalculated = _getPeriodEndDate(previousYear, previousQuarter).AddSeconds(2);

                                                        break;

                                                    case "longperiod":
                                                    case "longperiodytd":
                                                        switch (this.Context.Format)
                                                        {
                                                            case "MMMMstart to MMMMend":
                                                                if (this.Context.Offset == "-0.5y")
                                                                {
                                                                    // First half year
                                                                    dateStartCalculated = new DateTime(this.Year, 1, 1).AddSeconds(2);
                                                                    dateEndCalculated = _getPeriodEndDate(this.Year, 2);
                                                                }
                                                                else if (this.Context.Offset == "0.5y")
                                                                {
                                                                    // Second half year
                                                                    dateStartCalculated = new DateTime(this.Year, 7, 1).AddSeconds(2);
                                                                    dateEndCalculated = _getPeriodEndDate(this.Year, 4);
                                                                }
                                                                else if (this.Context.Offset == "0y")
                                                                {
                                                                    var startMonth = this.Context.MonthStart;
                                                                    var endMonth = this.Context.Month;
                                                                    var lastDayOfMonth = DateTime.DaysInMonth(this.Year, endMonth);

                                                                    dateStartCalculated = new DateTime(this.Year, startMonth, 1).AddSeconds(2);
                                                                    dateEndCalculated = RenderPeriodEnd(this.Year, endMonth, lastDayOfMonth, true);
                                                                }
                                                                else
                                                                {
                                                                    appLogger.LogWarning($"No support for parsing context for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo(this.Context?.NodeWrapper)}, stack-trace: {GetStackTrace()}");
                                                                }
                                                                break;

                                                            default:
                                                                appLogger.LogWarning($"No support for parsing context for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo(this.Context?.NodeWrapper)}, stack-trace: {GetStackTrace()}");
                                                                break;
                                                        }


                                                        break;


                                                    case "longperiodqtd":
                                                        switch (this.Context.Format)
                                                        {
                                                            case "MMMMdstart to MMMMdend":

                                                                var startMonth = this.Context.MonthStart;
                                                                var startDay = this.Context.DayStart;
                                                                var endMonth = this.Context.Month;
                                                                var endDay = this.Context.Day;

                                                                dateStartCalculated = new DateTime(this.Year, startMonth, startDay).AddSeconds(2);
                                                                dateEndCalculated = RenderPeriodEnd(this.Year, endMonth, endDay, true);
                                                                break;

                                                            default:
                                                                appLogger.LogWarning($"No support for parsing context for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo(this.Context?.NodeWrapper)}, stack-trace: {GetStackTrace()}");
                                                                break;
                                                        }

                                                        break;


                                                    case "runningperiod":
                                                    case "runningperioditm":
                                                    case "runningperiodqtd":
                                                    case "runningperiodytd":
                                                        switch (this.Context.Format)
                                                        {
                                                            case "MMMMstart to MMMMend":
                                                                {
                                                                    var startMonth = this.Context.MonthStart;
                                                                    var endMonth = this.Context.Month;
                                                                    var lastDayOfMonth = DateTime.DaysInMonth(this.Year, endMonth);

                                                                    dateStartCalculated = new DateTime(this.Year, startMonth, 1).AddSeconds(2);
                                                                    dateEndCalculated = RenderPeriodEnd(this.Year, endMonth, lastDayOfMonth, true);
                                                                }
                                                                break;

                                                            case "MMMMdstart to MMMMdend":
                                                                {
                                                                    var startMonth = this.Context.MonthStart;
                                                                    var startDay = this.Context.DayStart;
                                                                    var endMonth = this.Context.Month;
                                                                    var endDay = this.Context.Day;

                                                                    dateStartCalculated = new DateTime(this.Year, startMonth, startDay).AddSeconds(2);
                                                                    dateEndCalculated = RenderPeriodEnd(this.Year, endMonth, endDay, true);
                                                                }
                                                                break;

                                                            default:
                                                                appLogger.LogWarning($"No support for parsing context for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo(this.Context?.NodeWrapper)}, stack-trace: {GetStackTrace()}");
                                                                break;
                                                        }
                                                        break;


                                                    case "longperiodfullyear":
                                                        appLogger.LogInformation($"No need to add this context: {this.Context.InnerText}");
                                                        break;


                                                    case "writtendate":
                                                        switch (this.Context.Format)
                                                        {
                                                            case "MMM d":
                                                            case "MMM. d":
                                                            case "MMMM d":
                                                                // - Default to the current year
                                                                dateEndCalculated = RenderPeriodEnd(this.Year, this.Context.Month, this.Context.Day, true);
                                                                break;

                                                            default:
                                                                appLogger.LogWarning($"No support for parsing context for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo(this.Context?.NodeWrapper)}, stack-trace: {GetStackTrace()}");
                                                                break;
                                                        }

                                                        break;



                                                    default:
                                                        appLogger.LogWarning($"No support for parsing context of type {this.Context.Periodtype} for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo(this.Context?.NodeWrapper)}, stack-trace: {GetStackTrace()}");
                                                        break;

                                                }
                                            }
                                            else
                                            {
                                                if (this.HasContent())
                                                {
                                                    appLogger.LogWarning($"No support for parsing text based context for Context.ToJson()[1]: {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo(this.Context?.NodeWrapper)}");
                                                }
                                            }

                                        }

                                        break;


                                    case "yyyystart - yyyyend":
                                        dateEndCalculated = RenderPeriodEnd(this.Year, 12, 31);
                                        dateStartCalculated = new DateTime(this.YearStart, 1, 1);
                                        dateStartCreated = true;
                                        dateEndCreated = true;
                                        break;

                                    default:
                                        return new TaxxorReturnMessage(false, $"Year this.Format: {this.Format} not supported yet, {RenderDebugInfo()}");

                                }
                                break;


                            case "fullquarter":
                            case "shortquarter":
                                switch (this.Format)
                                {
                                    case "Q yyyy":
                                    case "yyyy_q":
                                        // - The end date is the end date of the quarted of this year
                                        dateEndCalculated = _getPeriodEndDate(this.Year, this.Quarter);

                                        // - The start date is the end date of the previous quarter with two seconds added
                                        previousQuarter = (this.Quarter == 1) ? 4 : this.Quarter - 1;
                                        previousYear = (this.Quarter == 1) ? this.Year - 1 : this.Year;
                                        dateStartCalculated = _getPeriodEndDate(previousYear, previousQuarter).AddSeconds(2);

                                        dateStartCreated = true;
                                        dateEndCreated = true;
                                        break;


                                    case "Q":
                                        // - Default to the current year
                                        var yearForCalculation = projectPeriodMetadata.PeriodEnd.Year;

                                        // Add context
                                        if (this.Context?.NodeWrapper != null)
                                        {
                                            if (this.Context.WrapperParsed)
                                            {
                                                switch (this.Context.Periodtype)
                                                {
                                                    case "year":
                                                        // We need to use the year we found in combination with the quarter context
                                                        yearForCalculation = this.Context.Year;
                                                        break;

                                                    default:
                                                        appLogger.LogWarning($"No support for parsing context for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo()}, stack-trace: {GetStackTrace()}");
                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                if (this.HasContent())
                                                {
                                                    appLogger.LogWarning($"No support for parsing text based context for Context.ToJson()[2]: {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo()}");
                                                }
                                            }

                                        }


                                        dateEndCalculated = _getPeriodEndDate(yearForCalculation, this.Quarter);

                                        // - The start date is the end date of the previous quarter with two seconds added
                                        previousQuarter = (this.Quarter == 1) ? 4 : this.Quarter - 1;
                                        previousYear = (this.Quarter == 1) ? yearForCalculation - 1 : yearForCalculation;
                                        dateStartCalculated = _getPeriodEndDate(previousYear, previousQuarter).AddSeconds(2);

                                        dateStartCreated = true;
                                        dateEndCreated = true;
                                        break;

                                    default:
                                        return new TaxxorReturnMessage(false, $"Full/short quarter this.Format: {this.Format} not supported yet, {RenderDebugInfo()}");
                                }
                                break;


                            case "writtendate":
                                switch (this.Format)
                                {
                                    case "MMM. d, yyyy":
                                    case "MMMM d, yyyy":
                                    case "d MMMM yyyy":
                                        // Calculate the date of the header cell
                                        dateEndCalculated = DateTime.ParseExact(this.InnerText, this.Format, System.Globalization.CultureInfo.InvariantCulture);
                                        dateEndCreated = true;

                                        dateStartCalculated = RenderDefaultPeriodStart();
                                        break;

                                    case "MMM. yyyy":
                                    case "MMMM yyyy":
                                    case "MMMM, yyyy":
                                        dateEndCalculated = RenderPeriodEnd(this.Year, this.Month, DateTime.DaysInMonth(this.Year, this.Month));
                                        dateEndCreated = true;

                                        dateStartCalculated = RenderDefaultPeriodStart();
                                        break;

                                    case "MMM d":
                                    case "MMM. d":
                                    case "MMMM d":
                                        // - Default to the current year
                                        var yearForCalculation = projectPeriodMetadata.PeriodEnd.Year;
                                        dateEndCalculated = RenderPeriodEnd(yearForCalculation, this.Month, this.Day);
                                        dateEndCreated = true;

                                        dateStartCalculated = RenderDefaultPeriodStart();
                                        break;

                                    default:
                                        return new TaxxorReturnMessage(false, $"Written date this.Format: {this.Format} not supported yet, {RenderDebugInfo()}");

                                }

                                break;

                            case "longperiod":
                            case "longperioditm":
                            case "longperiodqtd":
                            case "longperiodytd":
                            case "longperiodfullyear":
                            case "runningperiod":
                            case "runningperioditm":
                            case "runningperiodqtd":
                            case "runningperiodytd":
                                switch (this.Format)
                                {
                                    case "MMMMstart to MMMMend yyyy":
                                    case "MMMMstart to MMMMend, yyyy":
                                    case "MMM.start to MMM.end yyyy":
                                        dateEndCalculated = RenderPeriodEnd(this.Year, this.Month, DateTime.DaysInMonth(this.Year, this.Month));
                                        dateStartCalculated = new DateTime(this.Year, this.MonthStart, 1);

                                        dateStartCreated = true;
                                        dateEndCreated = true;
                                        break;

                                    case "MMMMdstart to MMMMdend yyyy":
                                    case "MMMMdstart to MMMMdend, yyyy":
                                        dateEndCalculated = RenderPeriodEnd(this.Year, this.Month, this.Day);
                                        dateStartCalculated = new DateTime(this.Year, this.MonthStart, this.DayStart);

                                        dateStartCreated = true;
                                        dateEndCreated = true;
                                        break;

                                    case "MMMMstart to MMMMend":
                                    case "MMMMstart-MMMMend":
                                        {
                                            // - Default to the current year
                                            var yearForCalculation = projectPeriodMetadata.PeriodEnd.Year;
                                            // Add context
                                            if (this.Context?.NodeWrapper != null)
                                            {
                                                if (this.Context.WrapperParsed)
                                                {
                                                    switch (this.Context.Periodtype)
                                                    {
                                                        case "year":
                                                            // We need to use the year we found in combination with the quarter context
                                                            yearForCalculation = this.Context.Year;
                                                            break;

                                                        default:
                                                            appLogger.LogWarning($"No support for parsing context for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo()}, stack-trace: {GetStackTrace()}");
                                                            break;
                                                    }
                                                }
                                                else
                                                {
                                                    if (this.HasContent())
                                                    {
                                                        appLogger.LogWarning($"No support for parsing text based context for Context.ToJson()[3]: {this.Context.ToJson()}, Periodtype: {this.Periodtype}, {RenderDebugInfo()}, Format: {this.Format}");
                                                    }
                                                }

                                            }
                                            dateEndCalculated = RenderPeriodEnd(yearForCalculation, this.Month, DateTime.DaysInMonth(yearForCalculation, this.Month));
                                            dateStartCalculated = new DateTime(yearForCalculation, this.MonthStart, 1);

                                            dateStartCreated = true;
                                            dateEndCreated = true;
                                        }
                                        break;

                                    case "MMMMdstart to MMMMdend":
                                        {
                                            // - Default to the current year
                                            var yearForCalculation = projectPeriodMetadata.PeriodEnd.Year;
                                            // Add context
                                            if (this.Context?.NodeWrapper != null)
                                            {
                                                if (this.Context.WrapperParsed)
                                                {
                                                    switch (this.Context.Periodtype)
                                                    {
                                                        case "year":
                                                            // We need to use the year we found in combination with the quarter context
                                                            yearForCalculation = this.Context.Year;
                                                            break;

                                                        default:
                                                            appLogger.LogWarning($"No support for parsing context for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo()}, stack-trace: {GetStackTrace()}");
                                                            break;
                                                    }
                                                }
                                                else
                                                {
                                                    if (this.HasContent())
                                                    {
                                                        appLogger.LogWarning($"[No support for parsing text based context for Context.ToJson()[4]]: {this.Context.ToJson()}, Periodtype: {this.Periodtype}, {RenderDebugInfo()}, Format: {this.Format}");
                                                    }
                                                }

                                            }
                                            dateEndCalculated = RenderPeriodEnd(yearForCalculation, this.Month, this.Day);
                                            dateStartCalculated = new DateTime(yearForCalculation, this.MonthStart, this.DayStart);

                                            dateStartCreated = true;
                                            dateEndCreated = true;
                                        }
                                        break;

                                    case "fy":
                                        {
                                            // - Default to the current year
                                            var yearForCalculation = projectPeriodMetadata.PeriodEnd.Year;
                                            // Add context
                                            if (this.Context?.NodeWrapper != null)
                                            {
                                                if (this.Context.WrapperParsed)
                                                {
                                                    switch (this.Context.Periodtype)
                                                    {
                                                        case "year":
                                                            // We need to use the year we found in combination with the quarter context
                                                            yearForCalculation = this.Context.Year;
                                                            break;

                                                        default:
                                                            appLogger.LogWarning($"No support for parsing context for Context.ToJson(): {this.Context.ToJson()}, Periodtype: {this.Periodtype}, Format: {this.Format}, {RenderDebugInfo()}, stack-trace: {GetStackTrace()}");
                                                            break;
                                                    }
                                                }
                                                else
                                                {
                                                    if (this.HasContent())
                                                    {
                                                        appLogger.LogWarning($"[No support for parsing text based context for Context.ToJson()[5]]: {this.Context.ToJson()}, Periodtype: {this.Periodtype}, {RenderDebugInfo()}, Format: {this.Format}");
                                                    }
                                                }

                                            }

                                            dateEndCalculated = RenderPeriodEnd(yearForCalculation, 12, 31);
                                            dateStartCalculated = new DateTime(yearForCalculation, 1, 1);

                                            dateStartCreated = true;
                                            dateEndCreated = true;
                                        }
                                        break;

                                    case "YTD":
                                    case "YTD yyyy":
                                        dateEndCalculated = RenderPeriodEnd(this.Year, this.Month, DateTime.DaysInMonth(this.Year, this.Month));
                                        dateStartCalculated = new DateTime(this.Year, this.MonthStart, 1);

                                        dateStartCreated = true;
                                        dateEndCreated = true;
                                        break;


                                    default:
                                        return new TaxxorReturnMessage(false, $"Long period this.Format: {this.Format} not supported yet, {RenderDebugInfo()}");
                                }
                                break;

                            case "yearonyear":
                                dateEndCalculated = RenderPeriodEnd(this.Year, 12, 31);
                                dateStartCalculated = new DateTime(this.YearStart, 1, 1);
                                dateStartCreated = true;
                                dateEndCreated = true;
                                break;

                            default:
                                return new TaxxorReturnMessage(false, $"this.Periodtype: {this.Periodtype} not supported yet, {RenderDebugInfo()}");
                        }

                    }

                    if (!dateEndCreated)
                    {
                        return new TaxxorReturnMessage(false, "Unable to create period start and period end dates", RenderDebugInfo());
                    }
                    else
                    {
                        if (dateEndCreated) this.PeriodEnd = dateEndCalculated.ToString("yyyyMMdd");
                        if (dateStartCreated) this.PeriodStart = dateStartCalculated.ToString("yyyyMMdd");

                        return new TaxxorReturnMessage(true, "Successfully created period start and period end date");
                    }

                }
                catch (Exception ex)
                {
                    var errorMessage = $"Unable to calculate the period fragments for the date wrapper";
                    appLogger.LogError(ex, errorMessage);
                    throw new Exception(errorMessage, ex);
                    // return new TaxxorReturnMessage(false, errorMessage, ex.Message);
                }

                DateTime RenderDefaultPeriodStart()
                {
                    switch (projectPeriodMetadata.ProjectType)
                    {
                        case "mr":
                            dateStartCreated = true;
                            return new DateTime(this.Year, this.Month, 1);


                        case "ar":
                        case "qr":
                            dateStartCreated = true;
                            return projectPeriodMetadata.PeriodStart;


                        default:
                            appLogger.LogWarning($"Unsupported project type: {projectPeriodMetadata.ProjectType}. stack-trace: {GetStackTrace()}");
                            dateStartCreated = false;

                            return projectPeriodMetadata.PeriodStart;
                    }
                }

                DateTime RenderPeriodEnd(int year, int month, int day, bool renderTime = false)
                {
                    if (!renderTime)
                    {
                        if (month == 2 && day == 29)
                        {
                            try
                            {
                                return new DateTime(year, month, day);
                            }
                            catch (Exception)
                            {
                                return new DateTime(year, month, 28);
                            }
                        }
                        else
                        {
                            return new DateTime(year, month, day);
                        }
                    }
                    else
                    {
                        if (month == 2 && day == 29)
                        {
                            try
                            {
                                return new DateTime(year, month, day, 23, 59, 59);
                            }
                            catch (Exception)
                            {
                                return new DateTime(year, month, 28, 23, 59, 59);
                            }
                        }
                        else
                        {
                            return new DateTime(year, month, day, 23, 59, 59);
                        }
                    }
                }

                string RenderDebugInfo(XmlNode nodeForTableAndArticleResolvingPassed = null)
                {
                    var debugInfo = "";

                    XmlNode nodeForTableAndArticleResolving = nodeForTableAndArticleResolvingPassed != null ? nodeForTableAndArticleResolvingPassed : nodeSectionContent;

                    if (nodeForTableAndArticleResolving != null)
                    {
                        string? tableIdInternal = nodeForTableAndArticleResolving.SelectSingleNode("ancestor::table")?.GetAttribute("id");
                        string? articleIdInternal = nodeForTableAndArticleResolving.SelectSingleNode("ancestor::article")?.GetAttribute("id");
                        return $"{((tableIdInternal != null) ? " tableId: " + tableIdInternal : "")}{((articleIdInternal != null) ? ", articleId: " + articleIdInternal : "")}, stack-trace: {GetStackTrace()}";
                    }
                    else
                    {
                        debugInfo = $" stack-trace: {GetStackTrace()}";
                    }

                    return debugInfo;
                }
            }

            public string ToJson()
            {
                var xmlJson = new XmlDocument();
                xmlJson.LoadXml($"<datewrapper><flexible>{this.Flexible.ToString().ToLower()}</flexible><periodtype>{this.Periodtype}</periodtype><format>{this.Format}</format><offset>{this.Offset}</offset><content></content></datewrapper>");
                xmlJson.SelectSingleNode("/datewrapper/content").InnerText = this._dateWrapperContent;
                return ConvertToJson(xmlJson, Newtonsoft.Json.Formatting.Indented, true, true);
            }

            public bool HasContent()
            {
                return !string.IsNullOrEmpty(this._dateWrapperContent.Trim());
            }

        }

    }
}
