namespace FrameworkLibrary.models
{
    /// <summary>
    /// Defines the type of response that we want to return to the client
    /// </summary>
    public enum ReturnTypeEnum
    {
        Xml,
        Json,
        Html,
        Xhtml,
        Txt,
        Js,
        None
    }

    /// <summary>
    /// Defines the supported hashing algorithms
    /// </summary>
    public enum EncryptionTypeEnum
    {
        MD5,
        SHA1,
        SHA256,
        HMACSHA1
    }

    /// <summary>
    /// Defines the different GIT reposotory types
    /// </summary>
    public enum GitTypeEnum
    {
        Normal,
        Submodule,
        None
    }

    /// <summary>
    /// Enumerator for HTTP request methods - replaces System.Net.Http.HttpMethod because those methods are not defined as enumerators
    /// </summary>
    public enum RequestMethodEnum
    {
        Get,
        Post,
        Put,
        Delete,
        Head,
        Trace
    }

    /// <summary>
    /// To describe the auditor area/scope in a standardized way
    /// </summary>
    public enum AuditorAreaEnum
    {
        FilingData,
        FilingContent,
        UserManagement,
        AccessRights,
        Unknown
    }

    /// <summary>
    /// Defines a project status
    /// </summary>
    public enum ProjectStatusEnum
    {
        Open,
        Closed
    }

    public enum HttpClientEnum
    {
        Standard,
        EfficientPredifinedHttp1,
        EfficientPredefinedHttp2,
        EfficientCustomizableHttp1,
        EfficientCustomizableHttp2
    }

    public enum ConnectedServiceEnum
    {
        Editor,
        DocumentStore,
        AccessControlService,
        XbrlService,
        PdfService,
        GenericDataConnectorService,
        MappingService,
        StructuredDataStore,
        ConversionService,
        EdgarArelleService,
        AssetCompiler,
        StaticAssets,
        Unknown
    }

    /// <summary>
    /// Random string types
    /// </summary>
    public enum RandomStringTypeEnum
    {
        Text,
        Numbers,
        Combined
    }

    public static class Contants
    {
        public const string NAMESPACE_DSIG = "http://www.w3.org/2000/09/xmldsig#";
    }

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
}
