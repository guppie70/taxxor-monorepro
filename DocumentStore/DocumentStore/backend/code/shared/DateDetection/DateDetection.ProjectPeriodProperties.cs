using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Project period properties class
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Project period information
        /// </summary>
        public class ProjectPeriodProperties
        {
            public bool Success { get; } = false;
            public string? ReportingPeriod { get; } = null;
            public string? ProjectType { get; } = null;
            public int CurrentProjectYear { get; } = 0;
            public int CurrentProjectQuarter { get; } = 0;
            public int CurrentProjectMonth { get; } = 0;
            public DateTime PeriodEnd { get; }
            public DateTime PeriodStart { get; }


            public ProjectPeriodProperties(string projectIdOrReportingPeriod, string reportingPeriodOverrule = null)
            {
                // TODO: this needs to become dynamic based on the tenant ID
                var offsetInMonths = TenantSpecificSettings.FirstOrDefault().Value.FullYearOffsetInMonths;

                // Determine if we have passed a project ID or a reporting period
                string? reportingPeriod = null;
                var nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectIdOrReportingPeriod}']");
                if (nodeProject == null)
                {
                    // Reporting period was passed
                    reportingPeriod = projectIdOrReportingPeriod;
                }
                else
                {
                    // Project ID was passed - retrieve the reporting period from the project node
                    if (reportingPeriodOverrule == null)
                    {
                        reportingPeriod = nodeProject.SelectSingleNode("reporting_period")?.InnerText;
                        if (string.IsNullOrEmpty(reportingPeriod) || reportingPeriod == "none")
                        {
                            // Use the publication date
                            var publicationDate = nodeProject.GetAttribute("date-publication");
                            if (!string.IsNullOrEmpty(publicationDate))
                            {
                                var parsedDate = DateTime.Parse(publicationDate);
                                reportingPeriod = parsedDate.ToString("yyyy-MM-dd");
                            }
                        }
                    }
                    else
                    {
                        reportingPeriod = reportingPeriodOverrule;
                    }
                }

                if (string.IsNullOrEmpty(reportingPeriod))
                {
                    appLogger.LogError("Could not create a project properties object because a period was not defined");
                }
                else
                {
                    this.ReportingPeriod = reportingPeriod;


                    if (reportingPeriod.Contains("-"))
                    {
                        var parsedDate = DateTime.ParseExact(reportingPeriod, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

                        this.ProjectType = "unknown";
                        this.CurrentProjectYear = parsedDate.Year;

                        switch (parsedDate.Month)
                        {
                            case 1:
                            case 2:
                            case 3:
                                this.CurrentProjectQuarter = 1;
                                break;

                            case 4:
                            case 5:
                            case 6:
                                this.CurrentProjectQuarter = 2;
                                break;

                            case 7:
                            case 8:
                            case 9:
                                this.CurrentProjectQuarter = 3;
                                break;

                            default:
                                this.CurrentProjectQuarter = 4;
                                break;

                        }

                        var isFixedPeriodProjectType =
                            projectIdOrReportingPeriod.StartsWith("ar") ||
                            projectIdOrReportingPeriod.StartsWith("qr") ||
                            projectIdOrReportingPeriod.StartsWith("mr")
                        ;
                        var previousQuarter = (this.CurrentProjectQuarter == 1) ? 4 : this.CurrentProjectQuarter - 1;
                        var previousYear = (this.CurrentProjectQuarter == 1) ? this.PeriodEnd.Year - 1 : this.PeriodEnd.Year;
                        if (!isFixedPeriodProjectType && previousYear <= 1)
                        {
                            previousYear = DateTime.Now.Year - 1;
                        }
                        this.PeriodStart = _getPeriodEndDate(previousYear, previousQuarter).AddSeconds(2);
                        this.PeriodEnd = _getPeriodEndDate(this.CurrentProjectYear, this.CurrentProjectQuarter);

                        this.Success = true;
                    }
                    else
                    {
                        // Site type
                        this.ProjectType = reportingPeriod.StartsWith("q") ? "qr" : reportingPeriod.StartsWith("m") ? "mr" : "ar";

                        // Parse the reporting period into integers so that we can determine the offset
                        int currentProjectYear = 0;

                        /* fixformat ignore:start */
                        var reportingPeriodParts = Regex.Match(reportingPeriod, @"^(m|q|a)(r|(\d){1,2})(\d\d)$");
                        /* fixformat ignore:end */

                        if (!reportingPeriodParts.Success)
                        {
                            appLogger.LogError($"Could not add date wrappers in the table cells because the reporting period was supplied ({reportingPeriod}) in an unkown format");
                        }
                        else
                        {
                            if (Int32.TryParse(reportingPeriodParts.Groups[4].Value, out currentProjectYear))
                            {
                                this.CurrentProjectYear = currentProjectYear + 2000;
                                this.Success = true;
                            }
                            else
                            {
                                appLogger.LogError($"Could not add date wrappers in the table cells because we could not determine the current reporting year from '{reportingPeriod}'");
                            }
                        }

                        if (this.ProjectType == "qr")
                        {
                            int currentProjectQuarter = 0;
                            if (Int32.TryParse(reportingPeriodParts.Groups[2].Value, out currentProjectQuarter))
                            {
                                this.CurrentProjectQuarter = currentProjectQuarter;
                                this.Success = true;
                            }
                            else
                            {
                                appLogger.LogError($"Could not add date wrappers in the table cells because we could not determine the current quarter from '{reportingPeriod}'");
                                this.Success = false;
                            }
                        }
                        else if (this.ProjectType == "mr")
                        {
                            int currrentProjectMonth = 0;
                            if (Int32.TryParse(reportingPeriodParts.Groups[2].Value, out currrentProjectMonth))
                            {
                                this.CurrentProjectMonth = currrentProjectMonth;
                                this.Success = true;
                            }
                            else
                            {
                                appLogger.LogError($"Could not add date wrappers in the table cells because we could not determine the current month from '{reportingPeriod}'");
                                this.Success = false;
                            }
                        }

                        // - Calculate the period end date
                        if (this.Success)
                        {
                            switch (this.ProjectType)
                            {
                                case "ar":
                                    {
                                        this.PeriodEnd = _getPeriodEndDate(this.CurrentProjectYear, this.CurrentProjectQuarter);
                                        this.PeriodStart = new DateTime(this.CurrentProjectYear, 1, 1, 0, 0, 1).AddMonths(offsetInMonths);
                                    }

                                    break;
                                case "qr":
                                    {
                                        this.PeriodEnd = _getPeriodEndDate(this.CurrentProjectYear, this.CurrentProjectQuarter);
                                        var previousQuarter = (this.CurrentProjectQuarter == 1) ? 4 : this.CurrentProjectQuarter - 1;
                                        var previousYear = (this.CurrentProjectQuarter == 1) ? this.PeriodEnd.Year - 1 : this.PeriodEnd.Year;
                                        this.PeriodStart = _getPeriodEndDate(previousYear, previousQuarter).AddSeconds(2);
                                    }
                                    break;

                                case "mr":
                                    {
                                        this.PeriodEnd = _getPeriodEndDate(this.CurrentProjectYear, this.CurrentProjectMonth, this.ProjectType);
                                        this.PeriodStart = new DateTime(this.PeriodEnd.Year, this.PeriodEnd.Month, 1, 1, 0, 0, 1);
                                    }

                                    break;

                                default:
                                    appLogger.LogWarning($"Unsupported project type: {this.ProjectType}. stack-trace: {GetStackTrace()}");
                                    break;
                            }
                        }
                    }


                }


            }

            public string DumpToString()
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine($"Success: {this.Success}");
                sb.AppendLine($"ReportingPeriod: {this.ReportingPeriod}");
                sb.AppendLine($"ProjectType: {this.ProjectType}");
                sb.AppendLine($"CurrentProjectYear: {this.CurrentProjectYear}");
                sb.AppendLine($"CurrentProjectQuarter: {this.CurrentProjectQuarter}");
                sb.AppendLine($"CurrentProjectMonth: {this.CurrentProjectMonth}");
                sb.AppendLine($"PeriodStart: {this.PeriodStart}");
                sb.AppendLine($"PeriodEnd: {this.PeriodEnd}");

                return sb.ToString();
            }
        }
    }
}
