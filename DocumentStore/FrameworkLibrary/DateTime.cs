using System;

namespace FrameworkLibrary
{
    /// <summary>
    /// Contains helper functions for working with dates and time
    /// </summary>
    public static class FrameworkDateTime
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
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }

        /// <summary>
        /// Converts a C# DateTime object to UNIX Epoch time
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static long ToUnixTime(DateTime date)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt64((date.ToUniversalTime() - epoch).TotalSeconds);
        }

    }
}