using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

public abstract partial class Framework
{

    /// <summary>
    /// Class that is initiated in the middleware and contains information unique for an HTTP request
    /// </summary>
    public class RequestVariables
    {
        private HttpContext? _context = null;


        // JT: Using fields instead of properties because it's naming convention in C# is similar to variable naming conventions which is basically how we will be using these fields anyhow
        public string siteLocale { get; set; }
        public string? siteLanguage { get; set; }
        public string? siteCountry { get; set; }

        public bool isGrpcRequest { get; set; } = false;

        public Uri? uri { get; set; }

        /// <summary>
        /// The complete URL of the incoming request including the querystring
        /// </summary>
        public string? rawUrl
        {
            get
            {
                if (_rawUrl == null && _context != null)
                {
                    _rawUrl = UriHelper.GetEncodedUrl(_context.Request);
                }
                return _rawUrl;
            }
        }
        private string? _rawUrl = null;


        /// <summary>
        /// Current request full domain name including http(s) and port number
        /// </summary>
        public string? fullDomainName
        {
            get
            {
                if (_fullDomainName == null && _context != null)
                {
                    _fullDomainName = uri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port, UriFormat.UriEscaped);
                }
                return _fullDomainName;
            }
        }
        private string? _fullDomainName = null;

        /// <summary>
        /// Current request domain name
        /// </summary>
        public string? domainName
        {
            get
            {
                if (_domainName == null && _context != null)
                {
                    _domainName = uri.GetComponents(UriComponents.Host, UriFormat.UriEscaped);
                }
                return _domainName;
            }
        }
        private string? _domainName = null;

        /// <summary>
        /// The protocol of the request ("http" or "https")
        /// </summary>
        public string? protocol
        {
            get
            {
                if (_protocol == null && _context != null)
                {
                    _protocol = uri.GetComponents(UriComponents.Scheme, UriFormat.UriEscaped);
                }
                return _protocol;
            }
        }
        private string? _protocol = null;


        /// <summary>
        /// The ip address of the client
        /// </summary>
        public string? ipAddress
        {
            get
            {
                if (_ipAddress == null && _context != null)
                {
                    _ipAddress = RetrieveClientIp(_context);
                }
                return _ipAddress;
            }

            set
            {
                _ipAddress = value;
            }
        }
        private string? _ipAddress = null;

        /// <summary>
        /// The URL path as it was used to request the resource
        /// </summary>
        public string? urlPath
        {
            get
            {
                if (_urlPath == null && _context != null)
                {
                    _urlPath = "/" + uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
                }
                return _urlPath;
            }
        }
        private string? _urlPath = null;

        /// <summary>
        /// The this URL path including a default document so that we can use it to query the site structure hierarchy
        /// </summary>
        public string? thisUrlPath
        {
            get
            {
                if (_thisUrlPath == null && _context != null)
                {
                    _thisUrlPath = urlPath;
                    if (urlPath.EndsWith("/", StringComparison.CurrentCulture)) _thisUrlPath = urlPath + defaultDocument;
                }
                return _thisUrlPath;
            }
        }
        private string? _thisUrlPath = null;

        /// <summary>
        /// The querystring variables of the URL used to request this resourse
        /// </summary>
        public QueryString querystringVariables;


        /// <summary>
        /// The HTTP method used for accessing this resource
        /// </summary>
        public RequestMethodEnum method
        {
            get
            {
                if (!_methodSet && _context != null)
                {
                    _method = RequestMethodEnumFromString(_context.Request.Method);
                    _methodSet = true;
                }
                return _method;
            }
        }
        private bool _methodSet = false;
        private RequestMethodEnum _method = RequestMethodEnum.Get;



        /// <summary>
        /// The current hierarchy node.
        /// </summary>
        public XmlNode? currentHierarchyNode;


        /// <summary>
        /// The current page ID from the site hierarchy
        /// </summary>
        public string? pageId;
        /// <summary>
        /// The title of the page
        /// </summary>
        public string? pageTitle;
        /// <summary>
        /// The ID of the XSLT template used to render the HTML of this page
        /// </summary>
        public string? pageTemplate;

        /// <summary>
        /// The type of response to return to the client.
        /// </summary>
        public ReturnTypeEnum returnType
        {
            get
            {
                if (!_returnTypeEnumSet && _context != null)
                {
                    // TODO: Improve this - very very basic...
                    var acceptHttpHeaderValue = _context.Request.RetrieveFirstHeaderValueOrDefault<string>("Accept");
                    // appLogger.LogInformation($"AcceptHttpHeaderValue: {acceptHttpHeaderValue}");
                    if (!string.IsNullOrEmpty(acceptHttpHeaderValue))
                    {
                        switch (acceptHttpHeaderValue.ToLower())
                        {
                            case string b when b.Contains(MediaTypeNames.Application.Json):
                                _returnTypeEnum = ReturnTypeEnum.Json;
                                break;                             
                            
                            case string c when c.Contains(MediaTypeNames.Text.JavaScript):
                                _returnTypeEnum = ReturnTypeEnum.Js;
                                break;

                            case string d when (d.Contains(MediaTypeNames.Text.Xml) || d.Contains(MediaTypeNames.Application.Xml) && !d.Contains(MediaTypeNames.Text.Html)):
                                _returnTypeEnum = ReturnTypeEnum.Xml;
                                break;

                            case string e when e.Contains(MediaTypeNames.Text.Plain):
                                _returnTypeEnum = ReturnTypeEnum.Txt;
                                break;

                            default:
                                _returnTypeEnum = ReturnTypeEnum.Html;
                                break;
                        }
                    }

                    // Override the potential returntype provided above if a specific return type was provided in the posted data
                    // TODO: Attempt to use specific type querystring or post parameter to find out which content type we need to return
                    var postedContentType = _context.Request.RetrievePostedValue("type", null, RegexEnum.Default) ?? _context.Request.RetrievePostedValue("_type", "", RegexEnum.Default);
                    if (!string.IsNullOrEmpty(postedContentType))
                    {
                        switch (postedContentType)
                        {
                            case "html":
                                _returnTypeEnum = ReturnTypeEnum.Html;
                                break;
                            case "xml":
                                _returnTypeEnum = ReturnTypeEnum.Xml;
                                break;   
                            case "json":
                                _returnTypeEnum = ReturnTypeEnum.Json;
                                break;
                        }
                    }

                    _returnTypeEnumSet = true;
                }
                return _returnTypeEnum;
            }

            set
            {
                this._returnTypeEnum = value;
            }
        }

        private bool _returnTypeEnumSet = false;
        private ReturnTypeEnum _returnTypeEnum = ReturnTypeEnum.Html;

        /// <summary>
        /// The current user.
        /// </summary>
        public AppUser currentUser;

        /// <summary>
        /// Contains a copy of the global available hierarchy
        /// </summary>
        public XmlDocument xmlHierarchy = new XmlDocument();

        /// <summary>
        /// Contains a version of the website hierarchy containing only the elements (pages) that the current user has access to
        /// </summary>
        public XmlDocument xmlHierarchyStripped = new XmlDocument();

        /// <summary>
        /// Defines if the application needs to write extra dubug variables for the incoming requests
        /// </summary>
        /// <value></value>
        public bool isDebugMode
        {
            get
            {
                if (!_isDebugModeSet && _context != null)
                {
                    _isDebugMode = RetrieveDebugMode(_context.Request);
                    _isDebugModeSet = true;
                }
                return _isDebugMode;
            }

            set
            {
                this._isDebugMode = value;
            }
        }
        private bool _isDebugModeSet = false;
        private bool _isDebugMode = false;

        /// <summary>
        /// Defines if the cache system needs to be debugged for this request
        /// </summary>
        /// <value></value>
        public bool debugCacheSystem
        {
            get
            {
                if (!_debugCacheSystemSet && _context != null)
                {
                    _debugCacheSystem = RetrieveDebugMode(_context.Request);
                    _debugCacheSystemSet = true;
                }
                return _debugCacheSystem;
            }

            set
            {
                this._debugCacheSystem = value;
            }
        }
        private bool _debugCacheSystemSet = false;
        private bool _debugCacheSystem = false;

        /// <summary>
        /// Indicates if this request is processing an active page or a static asset (like css for instance)
        /// </summary>
        public bool isStaticAsset = true;

        /// <summary>
        /// Indicate if the instance of this class is created in middleware or not
        /// </summary>
        public bool isMiddlewareCreated = false;

        /// <summary>
        /// Is session management enabled for this request
        /// </summary>
        public bool isSessionEnabled
        {
            get
            {
                if (!_isSessionEnabledSet && !string.IsNullOrEmpty(sessionConfigured))
                {

                    _isSessionEnabled = (sessionConfigured == "yes");
                    _isSessionEnabledSet = true;
                }
                return _isSessionEnabled;
            }
        }
        private bool _isSessionEnabledSet = false;
        private bool _isSessionEnabled = false;


        /// <summary>
        /// Extend the RequestVariables object with custom items without the need to change the class
        /// </summary>
        /// <value>The items.</value>
        public IDictionary<object, object>? items;

        // StringBuilders used for gathering debugging information
        public StringBuilder debugContent = new StringBuilder();
        public StringBuilder debugCacheContent = new StringBuilder();
        public StringBuilder profilingResult = new StringBuilder();

        /// <summary>
        /// Contains JSON data that was submitted to this application in a POST request converted to an XmlDocument
        /// </summary>
        public XmlDocument? xmlJsonData = null;

        // Constructor that sets some default values
        public RequestVariables(HttpContext? context = null)
        {
            this._context = context;

            if (this._context != null)
            {
                this.uri = new Uri(this.rawUrl);
                this.querystringVariables = context.Request.QueryString;
            }

            this.siteLocale = "global";
            this.siteLanguage = "en";
            this.siteCountry = "US";

            // By default we return HTML to the client
            this.returnType = ReturnTypeEnum.Html;


            this.currentUser = new AppUser();

            // TODO: In a multi-lingual environment we might need to pick the language specific version of the hierarchy
            // Pick up the hierarchy XmlDocument that is present in the ApplicationLogic layer and clone it so that we can use that as a basis
            if (Framework.xmlHierarchy.DocumentElement != null)
            {
                this.xmlHierarchy.ReplaceContent(Framework.xmlHierarchy);
            }
            else
            {
                appLogger.LogWarning("Global application hierarchy does not seem to have content so we compile it on-the-fly");

                var xmlNewHierarchy = CompileHierarchy();

                // Optionally apply an xslt translation to the document
                this.xmlHierarchy.ReplaceContent(XslTranslateHierarchy(xmlNewHierarchy));
            }

        }


        public string DebugVariables()
        {
            var debugString = new StringBuilder();

            debugString.AppendLine($"- siteLocale: {this.siteLocale}");
            debugString.AppendLine($"- siteLanguage: {this.siteLanguage}");
            debugString.AppendLine($"- siteCountry: {this.siteCountry}");
            debugString.AppendLine($"- pageId: {this.pageId}");
            debugString.AppendLine($"- pageTitle: {this.pageTitle}");
            debugString.AppendLine($"- rawUrl: {this._rawUrl}");
            debugString.AppendLine($"- fullDomainName: {this._fullDomainName}");
            debugString.AppendLine($"- domainName: {this._domainName}");
            debugString.AppendLine($"- protocol: {this._protocol}");
            debugString.AppendLine($"- ipAddress: {this._ipAddress}");
            debugString.AppendLine($"- urlPath: {this._urlPath}");
            debugString.AppendLine($"- thisUrlPath: {this._thisUrlPath}");
            debugString.AppendLine($"- method: {RequestMethodEnumToString(this._method)}");
            debugString.AppendLine($"- returnType: {ReturnTypeEnumToString(this._returnTypeEnum)}");
            debugString.AppendLine($"- isDebugMode: {this._isDebugMode.ToString().ToLower()}");
            debugString.AppendLine($"- debugCacheSystem: {this._debugCacheSystem.ToString().ToLower()}");
            debugString.AppendLine($"- isStaticAsset: {this.isStaticAsset.ToString().ToLower()}");
            debugString.AppendLine($"- isMiddlewareCreated: {this.isMiddlewareCreated.ToString().ToLower()}");
            debugString.AppendLine($"- isSessionEnabled: {this._isSessionEnabled.ToString().ToLower()}");


            // debugString.AppendLine($"- rawUrl: {this.rawUrl}");
            // debugString.AppendLine($"- rawUrl: {this.rawUrl}");
            // debugString.AppendLine($"- rawUrl: {this.rawUrl}");
            // debugString.AppendLine($"- rawUrl: {this.rawUrl}");


            return debugString.ToString();
        }


    }

    /// <summary>
    /// Retrieves the request variables object
    /// </summary>
    /// <returns>The request variables.</returns>
    /// <param name="context">Context.</param>
    public static RequestVariables RetrieveRequestVariables(HttpContext context, bool logError = true)
    {
        if (context != null)
        {
            foreach (var pair in context.Items)
            {
                if ((string)pair.Key == keyRequestVariables)
                {
                    return (RequestVariables)context.Items[keyRequestVariables];
                }
            }
        }

        if (siteType == "local" || siteType == "dev")
        {
            if (logError)
            {
                // Log the issue as a warning in the console so that we can inspect it
                appLogger.LogInformation($"RetrieveRequestVariables(): Could not retrieve the RequestVariables object from the context, because {((context == null) ? "there is no context" : "it does not exist in the items array")}.");
                if (siteType == "local" || siteType == "dev") appLogger.LogInformation($"stacktrace: {GetStackTrace()}");
            }
        }

        // Return a new RequestVariables object to avoid issues in the code
        return new RequestVariables();
    }

    /// <summary>
    /// Adds/stores the RequestVariables object to the HttpContext
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="requestVariables">Request variables.</param>
    public static void SetRequestVariables(HttpContext context, RequestVariables requestVariables)
    {
        foreach (var pair in context.Items)
        {
            if ((string)pair.Key == keyRequestVariables)
            {
                context.Items.Remove(keyRequestVariables);
                break;
            }
        }

        context.Items.Add(keyRequestVariables, requestVariables);
    }

    /// <summary>
    /// Tests if a request variables object exists in the context
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static bool RequestVariablesExistInContext(HttpContext context)
    {
        var exists = false;
        foreach (var pair in context.Items)
        {
            if ((string)pair.Key == keyRequestVariables)
            {
                exists = true;
                break;
            }
        }
        return exists;
    }

}