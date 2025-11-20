using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Date offset and period calculation methods
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Calculates how far off the date is compared to the current reporting period (in years)
        /// </summary>
        /// <param name="year">Year value from cell</param>
        /// <param name="quarter">Quarter value from cell</param>
        /// <param name="currentProjectYear">Current project year</param>
        /// <param name="currentProjectQuarter">Current project quarter</param>
        /// <returns>Offset in years</returns>
        private static double _calculateDateOffset(double year, double quarter, int currentProjectYear, int currentProjectQuarter)
        {
            double _quarterOffset = 0;
            double _yearOffset = 0;

            // Determine the offset
            _yearOffset = year - currentProjectYear;
            _quarterOffset = (quarter - currentProjectQuarter) * .25;
            return _yearOffset + _quarterOffset;
        }

        /// <summary>
        /// Determines the offset between two dates
        /// </summary>
        /// <param name="year">Year value from cell</param>
        /// <param name="month">Month name from cell</param>
        /// <param name="day">Day value from cell</param>
        /// <param name="currentProjectYear">Current project year</param>
        /// <param name="currentProjectQuarter">Current project quarter</param>
        /// <returns>Offset in years</returns>
        private static double _calculateDateOffset(double year, string month, double day, int currentProjectYear, int currentProjectQuarter)
        {
            DateTime dateProject = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);

            DateTime dateForCalculation = new DateTime(Convert.ToInt32(year), _getMonthInt(month), Convert.ToInt32(day), 0, 0, 0);

            var compareResult = (double)Math.Round((double)(dateForCalculation - dateProject).TotalDays / 365.25, 2);

            return compareResult;
        }

        /// <summary>
        /// Retrieves the number of months in between two dates
        /// </summary>
        /// <param name="dateCell">Date from cell</param>
        /// <param name="dateCurrentPeriodEnd">Current period end date</param>
        /// <returns>Number of months difference</returns>
        private static double _getMonthsBetween(DateTime dateCell, DateTime dateCurrentPeriodEnd)
        {
            return Convert.ToDouble(((dateCell.Year - dateCurrentPeriodEnd.Year) * 12) + dateCell.Month - dateCurrentPeriodEnd.Month);
        }

        /// <summary>
        /// Uses an offset to calculate a new quarter string
        /// </summary>
        /// <param name="offset">Offset value</param>
        /// <param name="currentProjectYear">Current project year</param>
        /// <param name="currentProjectQuarter">Current project quarter</param>
        /// <param name="includeYear">Whether to include year in result</param>
        /// <returns>Quarter string (e.g., "Q3" or "Q3 2024")</returns>
        private static string _calculateQuarterFromOffset(double offset, int currentProjectYear, int currentProjectQuarter, bool includeYear)
        {
            var dummyDay = 0;
            var dummyMonth = 0;
            var dummyQuarter = 0;
            var dummyYear = 0;

            return _calculateQuarterFromOffset(offset, currentProjectYear, currentProjectQuarter, includeYear, ref dummyDay, ref dummyMonth, ref dummyQuarter, ref dummyYear);
        }

        /// <summary>
        /// Uses an offset to calculate a new quarter string and return the details of the date used as ref parameters
        /// </summary>
        /// <param name="offset">Offset value</param>
        /// <param name="currentProjectYear">Current project year</param>
        /// <param name="currentProjectQuarter">Current project quarter</param>
        /// <param name="includeYear">Whether to include year in result</param>
        /// <param name="day">Returns calculated day</param>
        /// <param name="month">Returns calculated month</param>
        /// <param name="quarter">Returns calculated quarter</param>
        /// <param name="year">Returns calculated year</param>
        /// <returns>Quarter string (e.g., "Q3" or "Q3 2024")</returns>
        private static string _calculateQuarterFromOffset(double offset, int currentProjectYear, int currentProjectQuarter, bool includeYear, ref int day, ref int month, ref int quarter, ref int year)
        {
            DateTime dateProject = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
            DateTime calculatedDate = new DateTime();

            if (offset >= 0)
            {
                calculatedDate = dateProject.AddDays(offset * 364);
            }
            else
            {
                calculatedDate = dateProject.AddDays(offset * 370);
            }

            var calculatedQuarter = calculatedDate.GetQuarter();
            DateTime calculatedDatePrecise = _getPeriodEndDate(calculatedDate.Year, calculatedQuarter);
            day = calculatedDatePrecise.Day;
            month = calculatedDatePrecise.Month;
            quarter = calculatedQuarter;
            year = calculatedDatePrecise.Year;

            if (includeYear)
            {
                return $"Q{calculatedQuarter} {calculatedDate.Year}";
            }
            else
            {
                return $"Q{calculatedQuarter}";
            }
        }

        /// <summary>
        /// Calculates a mapping cluster period string
        /// </summary>
        /// <param name="offset">Offset value</param>
        /// <param name="currentProjectYear">Current project year</param>
        /// <param name="currentProjectQuarter">Current project quarter</param>
        /// <param name="duration">Whether to return a duration period</param>
        /// <returns>Period string in yyyyMMdd format</returns>
        public static string CalculateQuarterPeriodFromOffset(double offset, int currentProjectYear, int currentProjectQuarter, bool duration)
        {
            DateTime dateProject = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
            DateTime calculatedDate = new DateTime();

            if (offset >= 0)
            {
                calculatedDate = dateProject.AddDays(offset * 364);
            }
            else
            {
                calculatedDate = dateProject.AddDays(offset * 370);
            }

            var calculatedQuarter = calculatedDate.GetQuarter();

            var calculatedPeriodEndDate = _getPeriodEndDate(calculatedDate.Year, calculatedQuarter);
            if (duration)
            {
                var calculatedPeriodStartDate = new DateTime();
                if (currentProjectQuarter == 0)
                {
                    // Start date is Jan 1 of the year
                    calculatedPeriodStartDate = new DateTime(currentProjectYear, 1, 1, 0, 0, 1);
                }
                else
                {
                    var previousQuarter = (calculatedQuarter == 1) ? 4 : calculatedQuarter - 1;
                    var previousYear = (calculatedQuarter == 1) ? calculatedDate.Year - 1 : calculatedDate.Year;
                    calculatedPeriodStartDate = _getPeriodEndDate(previousYear, previousQuarter).AddSeconds(2);
                }

                return $"{calculatedPeriodStartDate.ToString("yyyyMMdd")}_{calculatedPeriodEndDate.ToString("yyyyMMdd")}";
            }
            else
            {
                return calculatedPeriodEndDate.ToString("yyyyMMdd");
            }
        }

        /// <summary>
        /// Retrieves the end date of a period (year or quarter)
        /// </summary>
        /// <param name="periodYear">Year</param>
        /// <param name="periodQuarter">Quarter (optional)</param>
        /// <returns>Period end date</returns>
        public static DateTime _getPeriodEndDate(double periodYear, double periodQuarter = 0)
        {
            return _getPeriodEndDate(Convert.ToInt32(periodYear), Convert.ToInt32(periodQuarter));
        }

        /// <summary>
        /// Rounds to nearest quarter
        /// </summary>
        /// <param name="x">Value to round</param>
        /// <returns>Rounded value</returns>
        public static double _roundToQuarter(double x)
        {
            return Math.Round(x * 4, MidpointRounding.ToEven) / 4;
        }

        /// <summary>
        /// Tests if the date provided is the last day of a quarter
        /// </summary>
        /// <param name="dateToInspect">Date to check</param>
        /// <returns>True if last day of quarter</returns>
        public static bool _isLastDayOfQuarter(DateTime dateToInspect)
        {
            var quarter = dateToInspect.GetQuarter();
            var dateQuarterEnd = _getPeriodEndDate(dateToInspect.Year, quarter);
            return dateToInspect.Month == dateQuarterEnd.Month && dateToInspect.Day == dateQuarterEnd.Day;
        }

        /// <summary>
        /// Retrieves the end date of a period (year or quarter)
        /// </summary>
        /// <param name="periodYear">Year</param>
        /// <param name="periodVariable">Quarter (for qr) or Month (for mr)</param>
        /// <param name="projectType">"ar", "qr" or "mr" supported</param>
        /// <returns>Period end date</returns>
        public static DateTime _getPeriodEndDate(int periodYear, int periodVariable = 0, string projectType = "qr")
        {
            // TODO: this needs to become dynamic based on the tenant ID
            var offsetInMonths = TenantSpecificSettings.FirstOrDefault().Value.FullYearOffsetInMonths;

            // Determine the project type to use within this method
            var _projectType = "ar";
            if (periodVariable > 0)
            {
                _projectType = projectType;
            }

            DateTime dateProject;
            try
            {
                switch (_projectType)
                {
                    case "annual-report":
                    case "ar":
                    case "esg-report":
                    case "esg":
                        // This is an Annual Report, so the date to compare with is dec 31
                        dateProject = new DateTime(periodYear, 12, 31, 23, 59, 59).AddMonths(offsetInMonths); ;
                        break;

                    case "quarterly-report":
                    case "qr":
                        switch (periodVariable)
                        {
                            case 1:
                                // First quarter March 31
                                dateProject = new DateTime(periodYear, 3, 31, 23, 59, 59).AddMonths(offsetInMonths); ;
                                break;

                            case 2:
                                // Second quarter - June 30
                                dateProject = new DateTime(periodYear, 6, 30, 23, 59, 59).AddMonths(offsetInMonths); ;
                                break;

                            case 3:
                                // Third quarter - September 30
                                dateProject = new DateTime(periodYear, 9, 30, 23, 59, 59).AddMonths(offsetInMonths); ;
                                break;

                            case 4:
                                // Fourth quarter - Dec 31
                                dateProject = new DateTime(periodYear, 12, 31, 23, 59, 59).AddMonths(offsetInMonths); ;
                                break;
                            default:
                                dateProject = new DateTime();
                                appLogger.LogError($"There was an error determining the date offset - unknown currentProjectQuarter: {periodVariable}. stack-trace: {GetStackTrace()}");
                                break;
                        }
                        break;

                    case "monthly-report":
                    case "mr":
                        dateProject = new DateTime(periodYear, periodVariable, DateTime.DaysInMonth(periodYear, periodVariable), 23, 59, 59);
                        break;

                    default:
                        throw new Exception($"Unsupported project type: {_projectType}");

                }

            }
            catch (Exception ex)
            {
                dateProject = DateTime.Now;
                appLogger.LogError(ex, $"There was an error creating the date object. periodYear: {periodYear}, periodVariable: {periodVariable}. stack-trace: {GetStackTrace()}");
            }

            return dateProject;
        }
    }
}
