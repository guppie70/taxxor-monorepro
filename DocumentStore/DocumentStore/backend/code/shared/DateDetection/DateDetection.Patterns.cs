using System.Text.RegularExpressions;

namespace Taxxor.Project
{
    /// <summary>
    /// Regex patterns and format strings for date detection
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {
        // Regular expressions for matching cell content to find periods in cells
        // Using static readonly with RegexOptions.Compiled for better performance

        #region Year Patterns

        private static readonly Regex reYear = new Regex(@"^((FY|fy)\s+)?(20\d{2})$", RegexOptions.Compiled);
        private const string formatYear = "yyyy";

        private static readonly Regex reYearExtended = new Regex(@"^(20\d{2})(\s*<.*)$", RegexOptions.Compiled);

        private const string formatFullYear = "fy yyyy";
        private static readonly Regex reFullYearNoYear = new Regex(@"^(FY|fy)\s*[^\d]*$", RegexOptions.Compiled);
        private const string formatFullYearNoYear = "fy";

        private static readonly Regex reYearBroken = new Regex(@"^(20\d{2})\s*\/\s*(20\d{2})$", RegexOptions.Compiled);
        private const string formatYearBroken = "yyyy / yyyy";

        // Used mainly for graphs
        private static readonly Regex reYearShort = new Regex(@"^(')(\d{2})$", RegexOptions.Compiled);
        private const string formatYearShort = "'yy";

        #endregion

        #region Quarter Patterns

        private static readonly Regex reQuarter = new Regex(@"^(Q(1|2|3|4))(.*)$", RegexOptions.Compiled);
        private const string formatQuarter = "Q";

        private static readonly Regex reQuarterFull = new Regex(@"^((Q(1|2|3|4))\s+(20\d{2}))(.*)$", RegexOptions.Compiled);
        private const string formatQuarterFull = "Q yyyy";

        private static readonly Regex reQuarterTechnical = new Regex(@"^(20\d{2})_(q(1|2|3|4))$", RegexOptions.Compiled);
        private const string formatQuarterTechnical = "yyyy_q";

        #endregion

        #region Written Date Formats

        private static readonly Regex reDateWrittenFormat1 = new Regex(@"^(.*?)((Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\.\s+(\d{1,2}),\s+(20\d{2}))(.*)$", RegexOptions.Compiled);
        private const string formatWrittenFormat1 = "MMM. d, yyyy";

        private static readonly Regex reDateWrittenFormat2 = new Regex(@"^(.*?)((January|February|March|April|May|June|July|August|September|October|November|December) (\d{1,2}), (20\d{2}))(.*)$", RegexOptions.Compiled);
        private const string formatWrittenFormat2 = "MMMM d, yyyy";

        private static readonly Regex reDateWrittenFormat3 = new Regex(@"^(.*?)((\d{2})/(\d{2})/(20\d{2}))(.*)$", RegexOptions.Compiled);
        private const string formatWrittenFormat3 = "dd/MM/yyyy";

        private static readonly Regex reDateWrittenFormat4 = new Regex(@"^(.*?)((\d{1,2}) (January|February|March|April|May|June|July|August|September|October|November|December) (20\d{2}))(.*)$", RegexOptions.Compiled);
        private const string formatWrittenFormat4 = "d MMMM yyyy";

        private static readonly Regex reDateWrittenFormat5 = new Regex(@"^(.*?)((\d{1,2})-(\d{1,2})-(20\d{2}))(.*)$", RegexOptions.Compiled);
        private const string formatWrittenFormat5 = "dd-MM-yyyy";

        // Dutch date format: "1 januari 2021", "23 februari 2024"
        private static readonly Regex reDateWrittenFormatDutch = new Regex(@"^(.*?)((\d{1,2}) (januari|februari|maart|april|mei|juni|juli|augustus|september|oktober|november|december) (20\d{2}))(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private const string formatWrittenFormatDutch = "d MMMM yyyy";

        #endregion

        #region Simple Written Date Formats

        private static readonly Regex reDateWrittenFormatSimple1 = new Regex(@"^((Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\.\s+(20\d{2}))(.*)$", RegexOptions.Compiled);
        private const string formatWrittenFormatSimple1 = "MMM. yyyy";

        private static readonly Regex reDateWrittenFormatSimple2 = new Regex(@"^((January|February|March|April|May|June|July|August|September|October|November|December)\s+(20\d{2}))(.*)$", RegexOptions.Compiled);
        private const string formatWrittenFormatSimple2 = "MMMM yyyy";

        private static readonly Regex reDateWrittenFormatSimple3 = new Regex(@"^((January|February|March|April|May|June|July|August|September|October|November|December)\s*,\s*(20\d{2}))(.*)$", RegexOptions.Compiled);
        private const string formatWrittenFormatSimple3 = "MMMM, yyyy";

        #endregion

        #region Simple Written Date Formats (No Year)

        private static readonly Regex reDateWrittenFormatSimpleNoYear1 = new Regex(@"^(.*?)((Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+(\d{1,2}))([^0-9]*)$", RegexOptions.Compiled);
        private const string formatWrittenFormatSimpleNoYear1 = "MMM d";

        private static readonly Regex reDateWrittenFormatSimpleNoYear2 = new Regex(@"^(.*?)((Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\.\s+(\d{1,2}))([^0-9]*)$", RegexOptions.Compiled);
        private const string formatWrittenFormatSimpleNoYear2 = "MMM. d";

        private static readonly Regex reDateWrittenFormatSimpleNoYear3 = new Regex(@"^(.*?)((January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2}))([^0-9]*)$", RegexOptions.Compiled);
        private const string formatWrittenFormatSimpleNoYear3 = "MMMM d";

        #endregion

        #region Long Period Patterns

        private static readonly Regex reLongPeriodWithYear = new Regex(@"(January|February|March|April|May|June|July|August|September|October|November|December)\s*to\s*(January|February|March|April|May|June|July|August|September|October|November|December)(,|\s)+(20\d{2})", RegexOptions.Compiled);
        private const string formatLongPeriodWithYear = "MMMMstart to MMMMend yyyy";
        private const string formatLongPeriodWithYearComma = "MMMMstart to MMMMend, yyyy";

        private static readonly Regex reLongPeriodWithYear2 = new Regex(@"(Jan\.|Feb\.|Mar\.|Apr\.|May\.|Jun\.|Jul\.|Aug\.|Sep\.|Oct\.|Nov\.|Dec\.)\s*[to\-]+\s*(Jan\.|Feb\.|Mar\.|Apr\.|May\.|Jun\.|Jul\.|Aug\.|Sep\.|Oct\.|Nov\.|Dec\.)(,|\s)+(20\d{2})", RegexOptions.Compiled);
        private const string formatLongPeriodWithYear2 = "MMM.start to MMM.end yyyy";

        private static readonly Regex reLongPeriodWithYearAndDay = new Regex(@"(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2})\s*to\s*(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2})(,|\s)+(20\d{2})", RegexOptions.Compiled);
        private const string formatLongPeriodWithYearAndDay = "MMMMdstart to MMMMdend yyyy";
        private const string formatLongPeriodWithYearAndDayComma = "MMMMdstart to MMMMdend, yyyy";

        private static readonly Regex reLongPeriodNoYear = new Regex(@"(January|July)\s*to\s*(June|December)", RegexOptions.Compiled);
        private const string formatLongPeriodNoYear = "MMMMstart to MMMMend";

        #endregion

        #region Running Period Patterns

        private static readonly Regex reRunningPeriodNoYear = new Regex(@"(January)-(March|June|September|December)", RegexOptions.Compiled);
        private const string formatRunningPeriodNoYear = "MMMMstart-MMMMend";

        private static readonly Regex reRunningPeriodNoYear3 = new Regex(@"((January|February|March|April|May|June|July|August|September|October|November|December)(\s*to\s*)(January|February|March|April|May|June|July|August|September|October|November|December))(.*)", RegexOptions.Compiled);
        private const string formatRunningPeriodNoYear3 = "MMMMstart to MMMMend";

        private static readonly Regex reRunningPeriodNoYear4 = new Regex(@"((January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2})(\s*to\s*)(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2}))(.*)", RegexOptions.Compiled);
        private const string formatRunningPeriodNoYear4 = "MMMMdstart to MMMMdend";

        #endregion

        #region Half Year and YTD Patterns

        private static readonly Regex reHalfYearWithYear = new Regex(@"^(.*?)(HY\s+)(20\d{2})(.*)$", RegexOptions.Compiled);
        private const string formatHalfYearWithYear = "HY yyyy";

        private static readonly Regex reYtdWithYear = new Regex(@"^(.*?)(YTD\s+)(20\d{2})(.*)$", RegexOptions.Compiled);
        private const string formatYtdWithYear = "YTD yyyy";

        private static readonly Regex reYtd = new Regex(@"^(.*?)(YTD)(.*)$", RegexOptions.Compiled);
        private const string formatYtd = "YTD";

        #endregion

        #region Year Comparison Patterns

        private static readonly Regex reYearOnYearComparison = new Regex(@"(20\d{2})\s+versus\s+(20\d{2})", RegexOptions.Compiled);
        private const string formatYearOnYearComparison = "yyyystart versus yyyyend";

        private static readonly Regex reYearPeriod = new Regex(@"(20\d{2})\s*-\s*(20\d{2})", RegexOptions.Compiled);
        private const string formatYearPeriod = "yyyystart - yyyyend";

        private static readonly Regex reDatePeriodDashFormat = new Regex(@"^(\d{1,2})-(\d{1,2})-(20\d{2})\s{2,}(\d{1,2})-(\d{1,2})-(20\d{2})$", RegexOptions.Compiled);
        private const string formatDatePeriodDashFormat = "dd-MM-yyyy  dd-MM-yyyy";

        #endregion
    }
}
