using System;
using System.Text.RegularExpressions;

namespace FrameworkLibrary
{
    /// <summary>
    /// Regular expressions helper logic and components
    /// </summary>
    public static class FrameworkRegex
    {

        //public class RegexEnumNew
        //{
        //    public const string Default = @"^[a-zA-Z_\-!\?@#',\.\s\d:\/]{1,256}$";
        //    public const string TextArea = @"^[a-zA-Z_\-%!\?@#',.:\s\d\)\(\{\}\|&;\[\]\{\}]{1,2000}$";
        //    public const string Email = @"^([0-9a-zA-Z]([-.\w]*[0-9a-zA-Z])*@([0-9a-zA-Z][-\w]*[0-9a-zA-Z]\.)+[a-zA-Z]{2,9})$";
        //    public const string FilePath = @"^([a-zA-Z]?\:)?(\/|[a-zA-Z_\-0-9\[\]\}\{])([\w\./\s\-\]\}\{])*$";
        //    public const string FileName = @"^([\w\d\./\s\-_]){3,100}$";
        //    public const string Url = @"^(https?|ftp):\/\/(-\.)?([^\s/?\.#]+\.?)+(\/[^\s]*)?$";
        //    public const string Uri = @"^((https?|ftp):\/\/|file:\/\/\/|[c-x|C-X]:\/|\/)([^\s/?\.#-]+\.?)+(\/[^\s]*)?$";
        //    public const string IsoDate = @"^\d{4}(-\d\d(-\d\d(T\d\d:\d\d(:\d\d)?(\.\d+)?(([+-]\d\d:\d\d)|Z)?)?)?)?$";
        //    public const string IsoDateFull = @"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?(([+-]\d\d:\d\d)|Z)?$";

        //    public const string None = @".*";
        //}

        /// <summary>
        /// Replaces a string using regular expressions
        /// </summary>
        /// <param name="pattern">Regular expression pattern</param>
        /// <param name="input">Source string to be replaced</param>
        /// <param name="replacePattern">Replace pattern</param>
        /// <returns></returns>
        public static string? RegExpReplace(Regex pattern, string? input, string replacePattern)
        {
            if (input == null)
                return null;
            return pattern.Replace(input, replacePattern);
        }

        /// <summary>
        /// Replaces a string using regular expressions
        /// </summary>
        /// <param name="searchPattern">Regular expression pattern</param>
        /// <param name="input">Source string to be replaced</param>
        /// <param name="replacePattern">Replace pattern</param>
        /// <returns></returns>
        public static string? RegExpReplace(string searchPattern, string? input, string replacePattern)
        {
            if (input == null)
                return null;
            return createRegex(searchPattern, false).Replace(input, replacePattern);
        }

        /// <summary>
        /// Replaces a string using regular expressions
        /// </summary>
        /// <param name="searchPattern">Regular expression pattern</param>
        /// <param name="input">Source string to be replaced</param>
        /// <param name="replacePattern">Replace pattern</param>
        /// <param name="forceSingleLineMode">Makes the regular expression work across multiple lines</param>
        /// <returns></returns>
        public static string? RegExpReplace(string searchPattern, string? input, string replacePattern, bool forceSingleLineMode)
        {
            if (input == null)
                return null;
            return createRegex(searchPattern, forceSingleLineMode).Replace(input, replacePattern);
        }

        /// <summary>
        /// Tests a string against a regular expression
        /// </summary>
        /// <param name="searchPattern">Regular expression pattern</param>
        /// <param name="input">String to investigate</param>
        /// <returns></returns>
        public static bool RegExpTest(string searchPattern, string? input)
        {
            if (input == null)
                return false;
            return createRegex(searchPattern, false).IsMatch(input);
        }

        /// <summary>
        /// Tests a string against a regular expression
        /// </summary>
        /// <param name="searchPattern">Regular expression pattern</param>
        /// <param name="input">String to investigate</param>
        /// <returns></returns>
        public static bool RegExpTest(string searchPattern, string? input, bool forceSingleLineMode)
        {
            if (input == null) return false;
            return createRegex(searchPattern, forceSingleLineMode).IsMatch(input);
        }

        /// <summary>
        /// This method return the first match by a pattern.
        /// </summary>
        /// <param name="pattern">Regular expression.</param>
        /// <param name="subject">String to search in.</param>
        /// <returns>String matched by the pattern.</returns>
        public static string? RegExpMatch(string pattern, string? subject)
        {
            if (subject == null) 
                return null;
            return RegExpMatch(pattern, subject, false);
        }

        /// <summary>
        /// This method return the first match by a pattern.
        /// </summary>
        /// <param name="pattern">Regular expression.</param>
        /// <param name="subject">String to search in.</param>
        /// <param name="forceSingleLineMode">Makes the regular expression work across multiple lines</param>
        /// <returns>String matched by the pattern.</returns>
        public static string? RegExpMatch(string pattern, string? subject, bool forceSingleLineMode)
        {
            if (subject == null) 
                return null;
            string output = string.Empty;

            //var regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Match match = createRegex(pattern, forceSingleLineMode).Match(subject);
            if (match.Success)
            {
                output = match.Value;
            }

            return output;
        }

        /// <summary>
        /// This method returns all the matches by a pattern.
        /// </summary>
        /// <param name="pattern">Regular expression.</param>
        /// <param name="subject">String to search in.</param>
        /// <returns>Matches matched by the pattern.</returns>
        public static MatchCollection RegExpMatches(string pattern, string subject)
        {
            return createRegex(pattern, false).Matches(subject);
        }

        /// <summary>
        /// This method returns all the matches by a pattern.
        /// </summary>
        /// <param name="pattern">Regular expression.</param>
        /// <param name="subject">String to search in.</param>
        /// <param name="forceSingleLineMode">Makes the regular expression work across multiple lines</param>
        /// <returns>Matches matched by the pattern.</returns>
        public static MatchCollection RegExpMatches(string pattern, string subject, bool forceSingleLineMode)
        {
            return createRegex(pattern, forceSingleLineMode).Matches(subject);
        }

        /// <summary>
        /// Helper utility to create the regular expression object
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="forceSingleLineMode">Makes the regular expression work across multiple lines</param>
        /// <returns></returns>
        private static Regex createRegex(string pattern, bool forceSingleLineMode)
        {
            RegexOptions lineOption = RegexOptions.Multiline;
            if (forceSingleLineMode) lineOption = RegexOptions.Singleline;
            return new Regex(pattern, lineOption | RegexOptions.IgnoreCase);
        }
    }
}