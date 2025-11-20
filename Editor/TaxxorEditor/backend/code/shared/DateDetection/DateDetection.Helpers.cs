using System;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Helper methods for date detection and cell content retrieval
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Retrieves the content node from a table cell
        /// </summary>
        /// <param name="nodeTableCell">The table cell node</param>
        /// <param name="showWarnings">Whether to log warnings</param>
        /// <returns>The content node or null if not found</returns>
        private static XmlNode _retrieveCellContentNode(XmlNode nodeTableCell, bool showWarnings = false)
        {
            var baseXpathSelector = "*[(local-name()='p' or local-name()='div' or local-name()='b' or local-name()='i' or local-name()='strong' or local-name()='em')]";
            var cellContainsNodes = nodeTableCell.SelectSingleNode(baseXpathSelector) != null;
            if (!cellContainsNodes) return nodeTableCell;

            var nodeListCellContent = nodeTableCell.SelectNodes($"descendant::{baseXpathSelector}");
            if (nodeListCellContent.Count == 0)
            {
                if (showWarnings) appLogger.LogWarning($"Could not find cell content");
                return null;
            }
            else
            {
                var nodeCellContent = nodeListCellContent.Item(nodeListCellContent.Count - 1);
                if (nodeCellContent == null)
                {
                    if (showWarnings) appLogger.LogWarning($"Cell content XML node could not be found");
                    return null;
                }
                else
                {
                    return nodeCellContent;
                }
            }
        }

        /// <summary>
        /// Retrieves the text of a cell
        /// </summary>
        /// <param name="nodeTableCell">The table cell node</param>
        /// <param name="showWarnings">Whether to log warnings</param>
        /// <returns>The cell content as a string or null if not found</returns>
        private static string _retrieveCellContent(XmlNode nodeTableCell, bool showWarnings = false)
        {
            var nodeCellContent = _retrieveCellContentNode(nodeTableCell, showWarnings);
            if (nodeCellContent == null)
            {
                if (showWarnings) appLogger.LogWarning($"Cell content XML node could not be found");
                return null;
            }
            else
            {
                return nodeCellContent.InnerXml.Trim();
            }
        }

        /// <summary>
        /// Attempts to convert a month date string to an integer
        /// Supports both English and Dutch month names
        /// </summary>
        /// <param name="month">Month name (e.g., "January", "Jan", "januari")</param>
        /// <returns>Month number (1-12)</returns>
        private static int _getMonthInt(string month)
        {
            // Dutch month name mapping (lowercase for case-insensitive comparison)
            var dutchMonths = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "januari", 1 },
                { "februari", 2 },
                { "maart", 3 },
                { "april", 4 },
                { "mei", 5 },
                { "juni", 6 },
                { "juli", 7 },
                { "augustus", 8 },
                { "september", 9 },
                { "oktober", 10 },
                { "november", 11 },
                { "december", 12 }
            };

            // Check if it's a Dutch month name
            if (dutchMonths.TryGetValue(month, out int monthNumber))
            {
                return monthNumber;
            }

            // Fall back to English month names using DateTime conversion
            var dateForConversion = $"{month} 01, 1900";
            return Convert.ToDateTime(dateForConversion).Month;
        }

        /// <summary>
        /// Returns the name of the month in English
        /// </summary>
        /// <param name="month">Month number (1-12)</param>
        /// <param name="longName">If true returns full name, otherwise abbreviated</param>
        /// <returns>Month name</returns>
        private static string _getMonthName(int month, bool longName = true)
        {
            return new DateTime(2010, month, 1).ToString(longName ? "MMMM" : "MMM");
        }

        /// <summary>
        /// Returns the name of the month in Dutch
        /// </summary>
        /// <param name="month">Month number (1-12)</param>
        /// <returns>Dutch month name</returns>
        private static string _getDutchMonthName(int month)
        {
            return month switch
            {
                1 => "januari",
                2 => "februari",
                3 => "maart",
                4 => "april",
                5 => "mei",
                6 => "juni",
                7 => "juli",
                8 => "augustus",
                9 => "september",
                10 => "oktober",
                11 => "november",
                12 => "december",
                _ => throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12")
            };
        }

        /// <summary>
        /// Checks if a string contains Dutch month names
        /// </summary>
        /// <param name="text">Text to check</param>
        /// <returns>True if Dutch month name is found</returns>
        private static bool _containsDutchMonthName(string text)
        {
            var dutchMonths = new[] { "januari", "februari", "maart", "april", "mei", "juni",
                                     "juli", "augustus", "september", "oktober", "november", "december" };
            var lowerText = text.ToLower();
            foreach (var month in dutchMonths)
            {
                if (lowerText.Contains(month))
                    return true;
            }
            return false;
        }
    }
}
