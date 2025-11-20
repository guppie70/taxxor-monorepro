using System;

/// <summary>
/// Contains helper functions for working with dates and time
/// </summary>
public abstract partial class Framework
{

    /// <summary>
    /// Retrieves the current date as a string in the agreed format
    /// </summary>
    /// <returns></returns>
    public static string RetrieveDateAsString()
    {
        return RetrieveDateAsString(DateTime.Now);
    }

    /// <summary>
    /// Converts a DateTime object in a string
    /// </summary>
    /// <param name="date"></param>
    /// <param name="datePattern"></param>
    /// <returns></returns>
    public static string RetrieveDateAsString(DateTime date, string datePattern = @"yyyy-MM-dd HH:mm:ss")
    {
        return date.ToString(datePattern);
    }

    /// <summary>
    /// Converts a UNIX Epoch time to a C# DateTime object
    /// </summary>
    /// <param name="unixTime"></param>
    /// <returns></returns>
    public static DateTime FromUnixTime(string unixTime)
    {
        return FromUnixTime(Convert.ToInt64(unixTime));
    }

    /// <summary>
    /// Converts a UNIX Epoch time to a C# DateTime object
    /// </summary>
    /// <param name="unixTime"></param>
    /// <returns></returns>
    public static DateTime FromUnixTime(long unixTime)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(unixTime);
    }

    /// <summary>
    /// Converts a C# DateTime object to UNIX Epoch time
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static long ToUnixTime(DateTime date)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return Convert.ToInt64((date.ToUniversalTime() - epoch).TotalSeconds);
    }

}

/// <summary>
/// Extensions to date time object
/// </summary>
public static class DateTimeExtensions
{

    /// <summary>
    /// Retrieves the quarter of the date that was passed
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static int GetQuarter(this DateTime date)
    {
        return (date.Month - 1) / 3 + 1;
    }

    /// <summary>
    /// Returns the quarter end date of the quarter that we are in
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static DateTime GetQuarterEndDate(this DateTime date)
    {
        int periodQuarter = date.GetQuarter();
        DateTime dateProject = new DateTime();

        switch (periodQuarter)
        {
            case 1:
                // First quarter March 31
                dateProject = new DateTime(date.Year, 3, 31, 23, 59, 59);
                break;

            case 2:
                // Second quarter - June 30
                dateProject = new DateTime(date.Year, 6, 30, 23, 59, 59);
                break;

            case 3:
                // Third quarter - September 30
                dateProject = new DateTime(date.Year, 9, 30, 23, 59, 59);
                break;

            case 4:
                // Fourth quarter - Dec 31
                dateProject = new DateTime(date.Year, 12, 31, 23, 59, 59);
                break;
        }

        return dateProject;
    }

    /// <summary>
    /// Returns the quarter start date of the quarter that we are in
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static DateTime GetQuarterStartDate(this DateTime date)
    {
        int periodQuarter = date.GetQuarter();
        DateTime dateProject = new DateTime();

        switch (periodQuarter)
        {
            case 1:
                // First quarter Jan 1
                dateProject = new DateTime(date.Year, 1, 1, 00, 00, 01);
                break;

            case 2:
                // Second quarter - April 1
                dateProject = new DateTime(date.Year, 4, 1, 00, 00, 01);
                break;

            case 3:
                // Third quarter - July 1
                dateProject = new DateTime(date.Year, 7, 1, 00, 00, 01);
                break;

            case 4:
                // Fourth quarter - October 1
                dateProject = new DateTime(date.Year, 10, 1, 00, 00, 01);
                break;
        }

        return dateProject;
    }

    /// <summary>
    /// Adds quarters to a date
    /// </summary>
    /// <param name="date"></param>
    /// <param name="quarters"></param>
    /// <returns></returns>
    public static DateTime AddQuarters(this DateTime date, int quarters)
    {
        var dateShifted = date.AddMonths(quarters * 3);
        return dateShifted.GetQuarterEndDate();
    }

    /// <summary>
    /// Counts the months in between the dates
    /// </summary>
    /// <param name="date1"></param>
    /// <param name="date2"></param>
    /// <returns></returns>
    public static int ElapsedMonths(this DateTime date1, DateTime date2)
    {
        DateTime earlierDate = (date1 > date2) ? date2 : date1;
        DateTime laterDate = (date1 > date2) ? date1 : date2;
        var eMonths = (laterDate.Month - earlierDate.Month) + 12 * (laterDate.Year - earlierDate.Year) -
            ((earlierDate.Day > laterDate.Day) ? 1 : 0);
        return eMonths;
    }

    /// <summary>
    /// Returns true if the passed date is before
    /// </summary>
    /// <param name="dateBase"></param>
    /// <param name="dateCompare"></param>
    /// <returns></returns>
    public static bool IsBefore(this DateTime dateBase, DateTime dateCompare)
    {
        return DateTime.Compare(dateBase, dateCompare) < 0;
    }

    /// <summary>
    /// Returns true if the passed date is after 
    /// </summary>
    /// <param name="dateBase"></param>
    /// <param name="dateCompare"></param>
    /// <returns></returns>
    public static bool IsAfter(this DateTime dateBase, DateTime dateCompare)
    {
        return DateTime.Compare(dateBase, dateCompare) > 0;
    }

    /// <summary>
    /// Returns true if the passed date is the same
    /// </summary>
    /// <param name="dateBase"></param>
    /// <param name="dateCompare"></param>
    /// <returns></returns>
    public static bool IsEqual(this DateTime dateBase, DateTime dateCompare)
    {
        return DateTime.Compare(dateBase, dateCompare) == 0;
    }

}