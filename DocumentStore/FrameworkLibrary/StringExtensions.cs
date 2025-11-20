using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

namespace FrameworkLibrary
{
    /// <summary>
    /// Extensions for the string class
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Test a string against multiple substrings
        /// </summary>
        /// <param name="input"></param>
        /// <param name="containsKeywords"></param>
        /// <returns></returns>
        public static bool ContainsAny(this string input, params string[] containsKeywords)
        {
            return input.ContainsAny(StringComparison.CurrentCulture, containsKeywords);
        }

        /// <summary>
        /// Test a string against multiple substrings
        /// </summary>
        /// <param name="input"></param>
        /// <param name="comparisonType"></param>
        /// <param name="containsKeywords"></param>
        /// <returns></returns>
        public static bool ContainsAny(this string input, StringComparison comparisonType, params string[] containsKeywords)
        {
            return containsKeywords.Any(keyword => input.Contains(keyword, comparisonType));
        }

        /// <summary>
        /// Test if a string ends with any of the supplied keywords
        /// </summary>
        /// <param name="input"></param>
        /// <param name="containsKeywords"></param>
        /// <returns></returns>
        public static bool EndsWithAny(this string input, params string[] containsKeywords)
        {
            return input.EndsWithAny(StringComparison.CurrentCulture, containsKeywords);
        }

        /// <summary>
        /// Test if a string ends with any of the supplied keywords
        /// </summary>
        /// <param name="input"></param>
        /// <param name="comparisonType"></param>
        /// <param name="containsKeywords"></param>
        /// <returns></returns>
        public static bool EndsWithAny(this string input, StringComparison comparisonType, params string[] containsKeywords)
        {
            return containsKeywords.Any(keyword => input.EndsWith(keyword, comparisonType));
        }

        /// <summary>
        /// Tests if a string is in URL encoded format
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool IsUrlEncoded(this string source)
        {
            return source != HttpUtility.UrlDecode(source);
        }

        /// <summary>
        /// Tests if a string is in HTML encoded format
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool IsHtmlEncoded(this string source)
        {
            // below fixes false positive &lt;<> 
            // you could add a complete blacklist, 
            // but these are the ones that cause HTML injection issues
            switch (source)
            {
                case string a when a.Contains('<'): return false;
                case string b when b.Contains('>'): return false;
                case string c when c.Contains('\\'): return false;
                case string d when d.Contains('\''): return false;
                default:
                    {
                        string decodedText = HttpUtility.HtmlDecode(source);
                        string encodedText = HttpUtility.HtmlEncode(decodedText);

                        return encodedText.Equals(source, StringComparison.OrdinalIgnoreCase);
                    }
            }
        }

        /// <summary>
        /// Get string value after the [first] a.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="substringToSearchFor"></param>
        /// <returns></returns>
        public static string? SubstringAfter(this string source, string substringToSearchFor)
        {
            int pos = source.IndexOf(substringToSearchFor, StringComparison.CurrentCulture);
            if (pos != -1)
            {
                return source.Substring(pos + substringToSearchFor.Length);
                // do something here
            }
            return null;
        }

        /// <summary>
        /// Get string value before the [first] a.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="substringToSearchFor"></param>
        /// <returns></returns>
        public static string? SubstringBefore(this string source, string substringToSearchFor)
        {
            int pos = source.IndexOf(substringToSearchFor, StringComparison.CurrentCulture);
            if (pos == -1)
            {
                return null;
            }
            return source.Substring(0, pos);
        }

        /// <summary>
        /// Tests if a string is base64 encoded
        /// </summary>
        /// <param name="base64String"></param>
        /// <param name="logError"></param>
        /// <returns></returns>
        public static bool IsBase64(this string base64String, bool logError = false)
        {
            // Credit: oybek https://stackoverflow.com/users/794764/oybek
            if (base64String == null ||
                base64String.Length == 0 ||
                base64String.Length % 4 != 0 ||
                base64String.Contains('<') ||
                base64String.Contains('>') ||
                base64String.Contains(' ') ||
                base64String.Contains('\t') ||
                base64String.Contains('\r') ||
                base64String.Contains('\n'))
            {
                return false;
            }
            else
            {
                try
                {
                    Convert.FromBase64String(base64String);
                    return true;
                }
                catch (Exception exception)
                {
                    // Handle the exception
                    if (logError)
                        Console.WriteLine($"Could not test if string was base 64 encoded... error: {exception}");
                }
                return false;
            }
        }

        /// <summary>
        /// Checks if the string contains parse-able XML
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsXml(this string str)
        {
            // Console.WriteLine("----");
            // Console.WriteLine(str);
            // Console.WriteLine("----");

            if (string.IsNullOrEmpty(str))
            {
                return false;
            }
            else
            {
                if (str.TrimStart().StartsWith('<'))
                {
                    try
                    {
                        XDocument _ = XDocument.Parse(str);
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }

                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Replaces names (HTML) entities such as &nbsp; with the numeric equivalent &#160;
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string EntityToNumeric(this string html)
        {
            var replacements = new Dictionary<string, string>();
            Regex regex = new Regex("(&[a-zA-Z]{2,7};)");
            foreach (Match match in regex.Matches(html))
            {
                if (!replacements.ContainsKey(match.Value))
                {
                    var unicode = HttpUtility.HtmlDecode(match.Value);
                    if (unicode.Length == 1)
                    {
                        replacements.Add(match.Value, string.Concat("&#", Convert.ToInt32(unicode[0]), ";"));
                    }
                }
            }
            foreach (KeyValuePair<string, string> replacement in replacements)
            {
                html = html.Replace(replacement.Key, replacement.Value);
            }
            return html;
        }

        /// <summary>
        /// Converts the first character of a string to upper case
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string FirstCharToUpper(this string input)
        {
            switch (input)
            {
                case null:
                    throw new ArgumentNullException(nameof(input));
                case "":
                    throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
                default:
                    return input.First().ToString().ToUpper() + input.Substring(1);
            }
        }

        /// <summary>
        /// Convert each word of a string so that the first character is upper case
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string ToTitleCase(this string self)
        {
            if (string.IsNullOrWhiteSpace(self))
            {
                return self;
            }

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(self);
        }

        /// <summary>
        /// Replaces text between the start and end markers
        /// </summary>
        /// <param name="input"></param>
        /// <param name="start">Substring that marks the beginning of the area where the text needs to be replaced</param>
        /// <param name="end">Substring that marks the end of the area where the text needs to be replaced</param>
        /// <param name="replacement">Text to insert between the start and end markers</param>
        /// <returns></returns>
        public static string ReplaceBetween(this string input, string start, string end, string replacement)
        {
            int startIndex = input.IndexOf(start);
            int endIndex = input.IndexOf(end, startIndex + start.Length);

            if (startIndex == -1 || endIndex == -1)
            {
                return input;
            }

            return input.Substring(0, startIndex + start.Length) + replacement + input.Substring(endIndex);
        }

    }
}