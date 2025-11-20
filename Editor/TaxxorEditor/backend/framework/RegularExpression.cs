using System;
using System.Text.RegularExpressions;

/// <summary>
/// Regular expressions helper logic and components
/// </summary>
public abstract partial class Framework
{
    /// <summary>
    /// Contains a predefined set of regular expressions
    /// </summary>
    public class RegexEnum
    {
        private RegexEnum(string value) { Value = value; }

        public string Value { get; set; }

        public static RegexEnum Default { get { return new RegexEnum(@"^[a-zA-Z_\-!\?@#',\.\s\d:\/\(\)\p{IsCJKUnifiedIdeographs}]{1,256}$"); } }
        public static RegexEnum Strict { get { return new RegexEnum(@"^[a-zA-Z_\-\d]{1,256}$"); } }
        public static RegexEnum Loose { get { return new RegexEnum(@"^[a-zA-Z_\-\d,\s\.!\?@#]{1,512}$"); } }
        public static RegexEnum UltraLoose { get { return new RegexEnum(@"^[a-zA-Z_\-\d,\s\.!\?@#%]{1,10000}$"); } }
        public static RegexEnum DefaultLong { get { return new RegexEnum(@"^[a-zA-Z_\-!\?@#',\.\s\d:\/\(\)\p{IsCJKUnifiedIdeographs}]{1,10000}$"); } }
        public static RegexEnum TextArea { get { return new RegexEnum(@"^[a-zA-Z_\+/\-%!\?@#=',.:\/\s\d\)\(\{\}\|&;\[\]\{\}\p{IsCJKUnifiedIdeographs}]{1,2000}$"); } }
        public static RegexEnum Email { get { return new RegexEnum(@"^([0-9a-zA-Z]([-.\w]*[0-9a-zA-Z])*@([0-9a-zA-Z][-\w]*[0-9a-zA-Z]\.)+[a-zA-Z]{2,9})$"); } }
        public static RegexEnum FilePath { get { return new RegexEnum(@"^([a-zA-Z]?\:)?(\/|[a-zA-Z_\-0-9\[\]\}\{])([\w\./\s\-\]\}\{])*$"); } }
        public static RegexEnum FileName { get { return new RegexEnum(@"^([\w\d\./\s\-_]){3,256}$"); } }
        public static RegexEnum Url { get { return new RegexEnum(@"^(https?|ftp|s3):\/\/(-\.)?([^\s/?\.#]+\.?)+(\/[^\s]*)?$"); } }
        public static RegexEnum Uri { get { return new RegexEnum(@"^((https?|ftp|s3):\/\/|file:\/\/\/|[c-x|C-X]:\/|\/)([^\s/?\.#]+\.?)+(\/[^\s]*)?$"); } }
        public static RegexEnum RelativeUri { get { return new RegexEnum(@"^([^\s/?\.#]+\.?)+(\/[^\s]*)?$"); } }
        public static RegexEnum isodate { get { return new RegexEnum(@"^\d{4}(-\d\d(-\d\d(T\d\d:\d\d(:\d\d)?(\.\d+)?(([+-]\d\d:\d\d)|Z)?)?)?)?$"); } }
        public static RegexEnum isodatefull { get { return new RegexEnum(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?(([+-]\d\d:\d\d)|Z)?$"); } }
        public static RegexEnum Boolean { get { return new RegexEnum("^(true|false|True|False|0|1|yes|no)$"); } }
        public static RegexEnum Hash { get { return new RegexEnum("^[a-f0-9]{32,40}$"); } }
        public static RegexEnum HashOrTag { get { return new RegexEnum(@"^([a-f0-9]{32,40}|[v\d\.]{3,9})$"); } }
        public static RegexEnum Guid { get { return new RegexEnum(@"(?im)^[{(]?[0-9A-F]{8}[-]?(?:[0-9A-F]{4}[-]?){3}[0-9A-F]{12}[)}]?$"); } }
        public static RegexEnum None { get { return new RegexEnum(".*"); } }

    }

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
        if (input == null) return null;
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
        if (input == null) return null;
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
        if (input == null) return null;
        return createRegex(searchPattern, forceSingleLineMode).Replace(input, replacePattern);
    }

    /// <summary>
    /// Tests a string against a regular expression
    /// </summary>
    /// <param name="searchPattern">Regular expression pattern</param>
    /// <param name="input">String to investigate</param>
    /// <returns></returns>
    public static Boolean RegExpTest(string searchPattern, string? input)
    {
        if (input == null) return false;
        return createRegex(searchPattern, false).IsMatch(input);
    }

    /// <summary>
    /// Tests a string against a regular expression
    /// </summary>
    /// <param name="searchPattern">Regular expression pattern</param>
    /// <param name="input">String to investigate</param>
    /// <returns></returns>
    public static Boolean RegExpTest(string searchPattern, string? input, bool forceSingleLineMode)
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
    public static String? RegExpMatch(string pattern, string? subject)
    {
        if (subject == null) return null;
        return RegExpMatch(pattern, subject, false);
    }

    /// <summary>
    /// This method return the first match by a pattern.
    /// </summary>
    /// <param name="pattern">Regular expression.</param>
    /// <param name="subject">String to search in.</param>
    /// <param name="forceSingleLineMode">Makes the regular expression work across multiple lines</param>
    /// <returns>String matched by the pattern.</returns>
    public static String? RegExpMatch(string pattern, string? subject, bool forceSingleLineMode)
    {
        if (subject == null) return null;
        string output = String.Empty;

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
        var lineOption = RegexOptions.Multiline;
        if (forceSingleLineMode) lineOption = RegexOptions.Singleline;
        return new Regex(pattern, lineOption | RegexOptions.IgnoreCase);
    }
}