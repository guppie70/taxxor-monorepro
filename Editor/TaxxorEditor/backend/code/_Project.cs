using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using DocumentStore.Protos;
using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;
using static Taxxor.ConnectedServices.DocumentStoreService;

namespace Taxxor.Project
{

    /// <summary>
    /// Variables and logic specific for the Taxxor Editor
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {
        // Begin required variables, properties and fields

        public static bool BypassSecurity = false;

        public static string? CmsRootPath = null;
        public static string? CmsRootPathOs = null;

        public static string? CmsCustomFrontEndRootPath = null;
        public static string? CmsCustomFrontEndRootPathOs = null;

        public static string? LocalSharedFolderPathOs { get; set; } = null;

        public static string PdfRenderEngine = "pagedjs";

        // For GIT
        public static string? GitUser = null;
        public static string? GitUserEmail = null;

        // For internal system calls
        public static string? SystemUser = null;

        // For referencing to this application via HTTP (fields to be filled via ProjectVariables middleware)
        public static bool RefreshLocalAddressAndIp = true;
        public static string? LocalWebAddressIp = null;
        public static string? LocalWebAddressDomain = null;


        /// <summary>
        /// Contains tenant specific settings
        /// </summary>
        public struct TenantSettings
        {
            public int FullYearOffsetInMonths { get; set; }
            public string Setting2 { get; set; }

            public TenantSettings()
            {
                // Check if an offset was provided ("gebroken boekjaar")
                int offsetMonths = 0;
                var nodeMonthOffset = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='fymonthoffset']");
                if (nodeMonthOffset != null)
                {
                    var offsetMonthsString = nodeMonthOffset.InnerText;
                    if (!int.TryParse((string)offsetMonthsString, out offsetMonths))
                    {
                        appLogger.LogWarning($"Unable to parse 'fymonthoffset' value: {offsetMonthsString} to an integer");
                    }
                }
                FullYearOffsetInMonths = offsetMonths;
            }
        }

        /// <summary>
        /// Contains settings for each tenant in the system, with the tenant id as the key and the settings as the value.
        /// </summary>
        /// <value></value>
        public static Dictionary<string, TenantSettings> TenantSpecificSettings { get; set; } = [];


        /// <summary>
        /// Used to prevent two processes updating the metadata at the same time
        /// </summary>
        /// <value></value>
        public static bool MetadataBeingUpdated { get; set; } = false;

        /// <summary>
        /// Contains metadata of the (content) files in use for all projects
        /// </summary>
        /// <returns></returns>
        public static XmlDocument XmlCmsContentMetadata = new XmlDocument();

        public static XmlDocument XmlDataConfiguration = new XmlDocument();

        // For debugging webservices
        public static bool DebugAllWebservices = false;

        /// <summary>
        /// Dirty way of keeping track of the data reference which is used for bulk transformation
        /// </summary>
        /// <value></value>
        public static string DataReferenceForBulkTransform { get; set; } = "";

        /// <summary>
        /// Regular expression is used for bypassing authentication static files (css, js, etc.)
        /// The expression below will never match (https://stackoverflow.com/questions/1723182/a-regex-that-will-never-be-matched-by-anything) so all static assets are protected by default
        /// You can define the expression used in base_configuration.xml -> /configuration/general/settings/setting[@id='staticfiles-bypass-regex']
        /// </summary>
        public static Regex StaticFilesBypassAuthentication = new Regex(@"x\by", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static FilingLockStorage FilingLockStore = new FilingLockStorage(memoryCache);


        /// <summary>
        /// Used to trigger Taxxor Access Control permissions cache removal
        /// </summary>
        public static string RbacPermissionsHash = null;

        /// <summary>
        /// Used to cache permissions retrieved from the Taxxor Access Control Service
        /// </summary>
        /// <returns></returns>
        public static ConcurrentDictionary<string, XmlDocument> RbacCacheData { get; set; } = new ConcurrentDictionary<string, XmlDocument>();

        /// <summary>
        /// Used for caching information about the sections that a user has access to
        /// </summary>
        /// <typeparam name="string"></typeparam>
        /// <typeparam name="string"></typeparam>
        /// <returns></returns>
        public static ConcurrentDictionary<string, string> UserSectionInformationCacheData { get; set; } = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Regular expression used for parsing a path of an asset that we need to retrieve from the Taxxor Document Store
        /// </summary>
        /// <returns></returns>
        public static Regex ReDataServiceAssetsPathParser = new Regex(@"^\/dataserviceassets\/(.*?)(\/.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// XSLT stylesheet that converts XML attributes to nodes - used for pretty-print JSON output and pre-loaded in memory for performance reasons
        /// </summary>
        /// <returns></returns>
        public static XslCompiledTransform XslAttributesToNodes = new XslCompiledTransform();



        /// <summary>
        /// Delegate that defines a fixed method signature for the data transform methods
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="lang"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public delegate Task<TaxxorReturnMessage> DataTransformationDelegate(string projectId, string lang, XmlDocument input);

        /// <summary>
        /// Dictionary that is used to map a function ID to a delegate that can be invoked
        /// </summary>
        public static Dictionary<string, DataTransformationDelegate> DataTransformationMethods;

        /// <summary>
        /// Lists all data transformation routines that we have defined in this code set
        /// </summary>
        /// <value></value>
        public static List<DataTransformationAttribute> DataTransformationList { get; set; }

        /// <summary>
        /// Maintains a list of user sessions so we can check for simultaneous sessions of the same user
        /// </summary>
        /// <value></value>
        public static Dictionary<string, UserBrowserSessionInfo> UserBrowserSessions { get; set; }

        /// <summary>
        /// Cache for XSLT stylesheets used in the PDF generation process
        /// </summary>
        /// <value></value>
        public static PdfXslStylesheetCache PdfXslCache { get; set; } = null;

        /// <summary>
        /// Setting that determines if the application needs to check the Cross Site Request Forgery tokens or not
        /// </summary>
        /// <value></value>
        public static bool EnableCrossSiteRequestForgery { get; set; } = true;

        /// <summary>
        /// Taxxor customer/client ID ("philips", "")
        /// </summary>
        /// <value></value>
        public static string TaxxorClientId { get; set; } = null;

        /// <summary>
        /// Taxxor customer/client Name
        /// </summary>
        /// <value></value>
        public static string TaxxorClientName { get; set; } = null;

        /// <summary>
        /// Flags if the system should log all the incoming and outgoing URI's
        /// </summary>
        /// <value></value>
        public static bool UriLogEnabled { get; set; } = false;

        /// <summary>
        /// Contains a list of URI's that are used by the system to connect to backend services
        /// </summary>
        /// <typeparam name="string"></typeparam>
        /// <returns></returns>
        public static List<string> UriLogBackend { get; set; } = [];

        /// <summary>
        /// Contains the cache of the paged-media content that is rendered in the Taxxor Editor
        /// </summary>
        /// <returns></returns>
        public static PagedMediaContentCache ContentCache = new PagedMediaContentCache();


        public static string ThumbnailFilenameTemplate { get; set; } = null;
        public static string ThumbnailFileExtension { get; set; } = null;
        public static int ThumbnailMaxSize { get; set; } = 120;
        public static string ImageRenditionsFolderName { get; set; } = null;
        public static byte[] BrokenImageBytes { get; set; } = null;
        public static string BrokenImageBase64 { get; set; } = null;


        /// <summary>
        /// To assure only one project is created at the same time
        /// </summary>
        /// <value></value>
        public static bool ProjectCreationInProgress { get; set; } = false;

        public static string StaticAssetsVersion { get; set; } = null;

        public static string CspContents { get; set; } = null;

        public static ConcurrentDictionary<string, ErpImportRunDetails> ErpImportStatus { get; set; } = [];

        public static ConcurrentDictionary<string, SdsSyncRunDetails> SdsSynchronizationStatus { get; set; } = [];

        public static string StaticAssetsLocation { get; set; } = null;



        /// <summary>
        /// Utility that calculates the values for project specific variables
        /// </summary>
        public static void InitProjectLogic()
        {
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (TimeStartup)
            {
                Console.WriteLine("=> Starting InitProjectLogic performance measurement <=");
                stopWatch.Restart();
            }

            // Git user
            GitUser = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='git-username']", xmlApplicationConfiguration);
            GitUserEmail = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='git-email']", xmlApplicationConfiguration);

            if (TimeStartup)
            {
                Console.WriteLine($" -> Git user setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // PDF render engine
            var nodePdfRenderEngine = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='pdfrenderengine']");
            if (nodePdfRenderEngine != null) PdfRenderEngine = nodePdfRenderEngine.InnerText;

            // System user
            SystemUser = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='system-userid']", xmlApplicationConfiguration);

            // ID of the current client using this Taxxor installation
            TaxxorClientId = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='taxxor-client-id']")?.InnerText ?? "";
            TaxxorClientName = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='taxxor-client-name']")?.InnerText ?? "";

            if (TimeStartup)
            {
                Console.WriteLine($" -> Configuration setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Retrieve the regular expression that optionally defines the static files that may bypass the authentication process
            var staticFilesBypassAuthenticationRegex = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='staticfiles-bypass-regex']", xmlApplicationConfiguration);
            if (!string.IsNullOrEmpty(staticFilesBypassAuthenticationRegex))
            {
                StaticFilesBypassAuthentication = new Regex(staticFilesBypassAuthenticationRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            // Dump the values that we have found to the console
            if (hostingEnvironment.EnvironmentName == "Development")
            {
                AddDebugVariable("gitUser", GitUser);
                AddDebugVariable("gitUserEmail", GitUserEmail);
                AddDebugVariable("systemUser", SystemUser);
            }
            AddDebugVariable("TaxxorClientId", TaxxorClientId);

            if (TimeStartup)
            {
                Console.WriteLine($" -> Debug variable setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Create important application directories
            List<string> dirsToCreate = [$"{logRootPathOs}/inspector", $"{logRootPathOs}/profiling"];

            foreach (string directoryPathOs in dirsToCreate)
            {
                if (!Directory.Exists(directoryPathOs))
                {
                    try
                    {
                        Directory.CreateDirectory(directoryPathOs);
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Unable to create application directory {directoryPathOs}... stack-trace: {GetStackTrace()}");
                    }
                }
            }

            if (TimeStartup)
            {
                Console.WriteLine($" -> Directory creation: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Retrieve the service descriptions of the connected services
            UpdateTaxxorServicesInformation(siteType).GetAwaiter().GetResult();

            if (TimeStartup)
            {
                Console.WriteLine($" -> Service information update: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Initiate the attribute to node XSLT document
            var xsltPathOs = CalculateFullPathOs("xsl_attributes-to-nodes");
            XsltSettings xsltSettings = new XsltSettings(true, true);
            // Required to load external stylesheets via xsl:include ...
            var resolver = new XmlUrlResolver();
            resolver.Credentials = CredentialCache.DefaultCredentials;
            XslAttributesToNodes.Load(xsltPathOs, xsltSettings, resolver);

            if (TimeStartup)
            {
                Console.WriteLine($" -> XSLT setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Reflection logic
            SetupCustomMethodReferenceLists();

            // Call the custom version of the project variables using the special Extensions object that is initiated in _Main.cs
            Extensions.RetrieveProjectVariablesCustom();

            if (TimeStartup)
            {
                Console.WriteLine($" -> Custom method setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Initiates the object used to detect simultaneous sessions
            UserBrowserSessions = [];

            // Used to determine HTTP Response Headers
            RenderHttpHeaderAppVersion = (xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/settings/setting[@id='header_application_version' and domain/@type='{siteType}']") != null);

            // Enable/disable csrf in the client
            var enableCrossSiteRequestForgeryString = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='enable-cross-site-request-forgery-check']", xmlApplicationConfiguration);
            if (!string.IsNullOrEmpty(enableCrossSiteRequestForgeryString) && siteType != "prev")
            {
                EnableCrossSiteRequestForgery = (enableCrossSiteRequestForgeryString == "true");
            }
            AddDebugVariable("EnableCrossSiteRequestForgery", EnableCrossSiteRequestForgery.ToString());

            // Enable/disable uri logging
            var enableUriLoggingString = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='log-uris']", xmlApplicationConfiguration);
            if (!string.IsNullOrEmpty(enableUriLoggingString) && siteType != "prev")
            {
                UriLogEnabled = (enableUriLoggingString == "true");
            }
            AddDebugVariable("UriLogEnabled", UriLogEnabled.ToString());

            if (TimeStartup)
            {
                Console.WriteLine($" -> Miscellaneous setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Retrieve the thumbnail settings
            var nodeThumbnailSettings = xmlApplicationConfiguration.SelectSingleNode("/configuration/renditions/thumbnail");
            ThumbnailFilenameTemplate = nodeThumbnailSettings.GetAttribute("namingconvention");
            ThumbnailFileExtension = nodeThumbnailSettings.GetAttribute("extension");
            var thumbnailMaxSize = 120;
            if (!int.TryParse(nodeThumbnailSettings.GetAttribute("max-size"), out thumbnailMaxSize))
            {
                appLogger.LogWarning("Unable to retrieve thumbnail size");
            }
            ThumbnailMaxSize = thumbnailMaxSize;

            var nodeRenditionLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/renditions/locations/location[@id='projectimagesrenditions']");
            if (nodeRenditionLocation != null)
            {
                ImageRenditionsFolderName = RegExpReplace(@"^.*\/(_.*?)\/.*$", nodeRenditionLocation.InnerText, "$1");
            }
            else
            {
                appLogger.LogError("Unable to find location for image renditions");
            }

            if (TimeStartup)
            {
                Console.WriteLine($" -> Thumbnail setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Capture the Base64 encoded content of the broken image
            var brokenImagePathOs = $"{dataRootPathOs}/broken-image.jpg";
            if (File.Exists(brokenImagePathOs))
            {
                BrokenImageBytes = File.ReadAllBytes(brokenImagePathOs);
                var base64ImageContent = Base64EncodeBinaryFile(brokenImagePathOs);
                var mimeType = GetContentType(brokenImagePathOs);
                BrokenImageBase64 = $"data:{mimeType};base64,{base64ImageContent}";
            }
            else
            {
                appLogger.LogError($"Unable to locate the broken image file at {brokenImagePathOs}");
            }

            // Bypass security setting
            var bypassSecurityString = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='bypass-security']", xmlApplicationConfiguration) ?? "false";
            BypassSecurity = (bypassSecurityString == "true");

            if (TimeStartup)
            {
                Console.WriteLine($" -> Security setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Retrieve the CMS metadata contents
            try
            {
                UpdateCmsMetadata().GetAwaiter().GetResult();
                appLogger.LogInformation($"XmlCmsContentMetadata content: {TruncateString(XmlCmsContentMetadata.OuterXml, 150)}");
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "Unable to retrieve content metadata");
            }

            if (TimeStartup)
            {
                Console.WriteLine($" -> CMS metadata update: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Retrieve the initial value of the RBAC permissions settings
            try
            {
                RbacPermissionsHash = AccessControlService.RetrieveHash().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "Unable to retrieve RBAC hash");
            }

            if (TimeStartup)
            {
                Console.WriteLine($" -> RBAC permissions setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Import data configuration from the Document Store
            try
            {
                TaxxorReturnMessage updateApplicationConfigResult = UpdateDataConfigurationInApplicationConfiguration().GetAwaiter().GetResult();
                if (updateApplicationConfigResult.Success == false)
                {
                    appLogger.LogWarning($"Could not restore data configuration. message: {updateApplicationConfigResult.Message}, debuginfo: {updateApplicationConfigResult.DebugInfo}");
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "Unable to import data configuration in application configuration");
            }

            if (TimeStartup)
            {
                Console.WriteLine($" -> Data configuration update: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // GIT repository information
            try
            {
                var successfullyUpdatedRepositoryVersionInformation = RetrieveTaxxorGitRepositoriesVersionInfo(false).GetAwaiter().GetResult();
                if ((siteType == "local" || siteType == "dev") && !successfullyUpdatedRepositoryVersionInformation) appLogger.LogInformation($"- RetrieveTaxxorGitRepositoriesVersionInfo.success: {successfullyUpdatedRepositoryVersionInformation.ToString()}");

                // Update the version information in the ProjectLogic part
                if (successfullyUpdatedRepositoryVersionInformation)
                {
                    RetrieveAppVersion();
                    // Console.WriteLine($"=> applicationVersion: {applicationVersion}");
                }
                else
                {
                    appLogger.LogWarning("Unable to retrieve application version information");
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "Unable to retrieve information about the GIT repositories");
            }

            if (TimeStartup)
            {
                Console.WriteLine($" -> GIT repository information: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }


            // Static assets location
            StaticAssetsLocation = Environment.GetEnvironmentVariable("StaticAssetsLocation") ?? CalculateFullPathOs("staticassets");
            AddDebugVariable("StaticAssetsLocation", StaticAssetsLocation);
            if (TimeStartup)
            {
                Console.WriteLine($" -> Update static assets location: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Static assets version
            UpdateStaticAssetsVersion();
            if (TimeStartup)
            {
                Console.WriteLine($" -> Update static assets information: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }


            // Application has been successfully initiated
            AppInitiated = true;

            if (TimeStartup)
            {
                totalStopwatch.Stop();
                Console.WriteLine($"=> Total time for InitProjectLogic: {totalStopwatch.ElapsedMilliseconds}ms <=");
            }
        }

        /// <summary>
        /// Use reflection to loop through the C# code and build up references to methods so that we can dynamically execute them
        /// </summary>
        public static void SetupCustomMethodReferenceLists()
        {
            // Find all the data transformation scenarios that we have available in the ProjectLocic code set
            // By looping through all the methods that have the custom attribute set
            DataTransformationList = [];

            DataTransformationMethods = [];
            foreach (MethodInfo methodInfo in typeof(ProjectLogic).GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                DataTransformationAttribute? customDataTransformationAttribute = methodInfo.GetCustomAttribute<DataTransformationAttribute>();
                if (customDataTransformationAttribute != null)
                {
                    DataTransformationList.Add(customDataTransformationAttribute);

                    try
                    {
                        DataTransformationMethods[customDataTransformationAttribute.Id] = (DataTransformationDelegate)Delegate.CreateDelegate(typeof(DataTransformationDelegate), methodInfo, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not add method with ID {customDataTransformationAttribute.Id}. error: {ex}");
                    }

                }
            }
        }


        /// <summary>
        /// Adds the project specific information to the hierarchy document
        /// </summary>
        /// <param name="xmlHierarchy"></param>
        /// <param name="projectId"></param>
        /// <param name="projectRootPath"></param>
        /// <returns></returns>
        public static XmlDocument ReworkHierarchy(XmlDocument xmlHierarchy, string projectId, string projectRootPath)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            var allAllowed = RetrieveHierarchyAllAllowedValue();

            //var rootPath = "";
            var accessControl = "xxyyzz";
            if (BypassSecurity) accessControl = "all";

            var allowedXpathStatement = (HierarchyUsingViewEditAcl(xmlHierarchy)) ? "access_control/view" : "access_control";

            if ((projectId != null) || (reqVars.pageId == "cms-overview"))
            {
                if (BypassSecurity)
                {
                    var xmlNodeListViewRightsHierarchy = xmlHierarchy.SelectNodes($"/items/unstructured//item/{allowedXpathStatement}/allowed[not(text()='all')]");
                    foreach (XmlNode xmlNodeViewRightsHierarchy in xmlNodeListViewRightsHierarchy)
                    {
                        xmlNodeViewRightsHierarchy.InnerText = allAllowed;
                    }
                }

                //change the name of the project details page so that it represents the project name and add the project id to the link
                var nodeProjectDetailsPage = xmlHierarchy.SelectSingleNode("/items/structured/item/sub_items/item[@id='cms_project-details']/web_page");
                if (nodeProjectDetailsPage != null)
                {
                    //nodeProjectDetailsPage.SelectSingleNode("linkname").InnerText = RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id='" + projectId + "']/name", xmlApplicationConfiguration);
                    nodeProjectDetailsPage.SelectSingleNode("linkname").InnerText = RetrieveLocalizedValueByKey("label_filing-portal");
                    nodeProjectDetailsPage.SelectSingleNode("path").InnerText = nodeProjectDetailsPage.SelectSingleNode("path").InnerText + "?pid=" + projectId;
                }

                //add the projectid to all the level 2 and up pages
                var nodeListLowerLevels = xmlHierarchy.SelectNodes("/items/structured/item/sub_items/item[@id='cms_project-details']/sub_items//web_page/path");
                foreach (XmlNode nodeLowerLevel in nodeListLowerLevels)
                {
                    nodeLowerLevel.InnerText = nodeLowerLevel.InnerText + "?pid=" + projectId;
                }

            }
            else
            {
                var xmlNodeListViewRightsHierarchy = xmlHierarchy.SelectNodes($"//item/{allowedXpathStatement}/allowed[text()='[projectviewrights]']");
                foreach (XmlNode xmlNodeViewRightsHierarchy in xmlNodeListViewRightsHierarchy)
                {
                    xmlNodeViewRightsHierarchy.InnerText = accessControl;
                }

            }

            // if (isDevelopmentEnvironment && !isApiRoute())
            // {
            //     var saved = SaveXmlDocument(xmlHierarchy, logRootPathOs + "/site_structure.xml");
            // }

            return xmlHierarchy;
        }



        /// <summary>
        /// Tests if the combination projectId, versionId and dataType is exists in the project definition
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        public static bool ValidateCmsPostedParameters(string projectId, string versionId = "latest", string dataType = "text")
        {
            bool validationPassed = false;
            // string xml = null;
            string errorReason = "";
            string errorDetails = "";
            XmlNode? appConfigNode;

            if (projectId != null && versionId != null && dataType != null)
            {
                appConfigNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]");
                if (appConfigNode != null)
                {
                    if (versionId == "latest")
                    {
                        appConfigNode = appConfigNode.SelectSingleNode("versions/version");
                    }
                    else
                    {
                        appConfigNode = appConfigNode.SelectSingleNode("versions/version[@id=" + GenerateEscapedXPathString(versionId) + "]");
                    }

                    if (appConfigNode != null)
                    {
                        switch (dataType)
                        {
                            case "config":
                            case "schema":
                                validationPassed = true;

                                break;
                            default:
                                appConfigNode = appConfigNode.SelectSingleNode("data/*[@id=" + GenerateEscapedXPathString(dataType) + "]");
                                if (appConfigNode != null)
                                {
                                    validationPassed = true;
                                }
                                else
                                {
                                    errorReason = "Data type does not exist";
                                    errorDetails = $"Data type {dataType} could not be found. stack-trace: {GetStackTrace()}";
                                }

                                break;
                        }

                    }
                    else
                    {
                        errorReason = "Version does not exist";
                        errorDetails = $"Version {versionId} could not be found. stack-trace: {GetStackTrace()}";
                    }
                }
                else
                {
                    errorReason = "Project does not exist";
                    errorDetails = $"Project with id {projectId} does not exist. stack-trace: {GetStackTrace()}";
                }
            }
            else
            {
                errorReason = "Did not supply enough input to resolve your request";
                errorDetails = "Data type, Version ID or Project ID was not supplied.";
            }

            if (!validationPassed)
            {
                // TODO: Force XML to return here?
                if (System.Web.Context.Current != null)
                {
                    RequestVariables reqVars = RetrieveRequestVariables(System.Web.Context.Current);
                    HandleError(reqVars, errorReason, errorDetails);
                }
                else
                {
                    appLogger.LogError($"{errorReason} Details: {errorDetails}");
                }

            }

            return validationPassed;
        }

        public static string? GenerateSubItemsList(string parentPageId, XmlDocument xmlHierarchy)
        {
            XsltArgumentList xsltArgumentList = new XsltArgumentList();
            xsltArgumentList.AddParam("parentpageid", "", parentPageId);

            XmlNode? xmlNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='cms_xsl_create-linklist']");
            var xslPathOs = CalculateFullPathOs(xmlNode);

            var html = TransformXml(xmlHierarchy, xslPathOs, xsltArgumentList);

            return html;
        }

        /// <summary>
        /// Retrieves the root folder where the data files of the CMS are located
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public static string? RetrieveDataRootFolderOs(string projectId, string versionId, ReturnTypeEnum returnType = ReturnTypeEnum.Html)
        {
            return RetrieveDataRootFolderOs(projectId, versionId, xmlApplicationConfiguration, returnType);
        }

        /// <summary>
        /// Retrieves the root folder where the data files of the CMS are located
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="xmlConfig"></param>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public static string? RetrieveDataRootFolderOs(string projectId, string versionId, XmlDocument xmlConfig, ReturnTypeEnum returnType = ReturnTypeEnum.Html)
        {
            string? currentDataFolderPathOs = null;
            XmlNode? node = xmlConfig.SelectSingleNode("/configuration/cms_projects/cms_project[@id='" + projectId + "']/path");
            if (node != null)
            {
                currentDataFolderPathOs = dataRootPathOs + node.InnerText;
                XmlNode? nodeNested = xmlConfig.SelectSingleNode("/configuration/cms_projects/cms_project[@id='" + projectId + "']/versions/version[@id='" + versionId + "']/path");
                if (nodeNested != null)
                {
                    currentDataFolderPathOs += nodeNested.InnerText.Replace("[versionid]", versionId);
                }
            }

            return currentDataFolderPathOs;
        }

        /// <summary>
        /// Retrieves the metadata ID for the output hierarchy that is used in the XML configuration and also in projectVars.cmsMetaData[]
        /// </summary>
        /// <returns>The output channel hierarchy metadata identifier.</returns>
        /// <param name="projectVars">Project variables.</param>
        public static string RetrieveOutputChannelHierarchyMetadataId(ProjectVariables projectVars)
        {
            if (string.IsNullOrEmpty(projectVars.editorId))
            {
                projectVars.editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
            }
            return RetrieveOutputChannelHierarchyMetadataId(projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
        }

        /// <summary>
        /// Test to see if we have a request that came in via the Taxxor PDF Service
        /// </summary>
        /// <returns></returns>
        public static bool IsPdfServiceRequest()
        {
            var context = System.Web.Context.Current;

            var pdfServiceRequest = false;
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            //  && (context.Request.Headers["x-tx-requestorigin"].ToString()?.ToLower()??"unknown")=="pdfservice")
            if (userAgent.ToLower().Contains("prince") || (userAgent.Contains(" HeadlessChrome/")))
            {
                pdfServiceRequest = true;
            }
            return pdfServiceRequest;
        }

        /// <summary>
        /// Renders an XML overview containing all the outputchannel hierarchies for the current project
        /// </summary>
        /// <param name="includeAllItems"></param>
        /// <param name="addFilingMetadata"></param>
        /// <param name="includeHierarchies"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RenderOutputChannelHierarchyOverview(bool includeAllItems = true, bool addFilingMetadata = false, bool includeHierarchies = true)
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            return await RenderOutputChannelHierarchyOverview(projectVars, includeAllItems, addFilingMetadata, includeHierarchies);
        }

        /// <summary>
        /// Renders an XML overview containing all the outputchannel hierarchies for the current project
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="includeAllItems"></param>
        /// <param name="addFilingMetadata"></param>
        /// <param name="includeHierarchies"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RenderOutputChannelHierarchyOverview(ProjectVariables projectVars, bool includeAllItems = true, bool addFilingMetadata = false, bool includeHierarchies = true)
        {
            // Compile one document based on all the output channels
            var xmlOutputChannelHierarchies = new XmlDocument();
            xmlOutputChannelHierarchies.AppendChild(xmlOutputChannelHierarchies.CreateElement("hierarchies"));

            //var nodeListOutputChannels = xmlDataConfiguration.SelectNodes($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels[1]/output_channel[1]/variants[1]/variant[1]/@id")

            var nodeListOutputChannels = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(projectVars.editorId) + "]/output_channels/output_channel");

            foreach (XmlNode nodeOutputChannel in nodeListOutputChannels)
            {
                string? outputChannelType = GetAttribute(nodeOutputChannel, "type");

                var nodeListOutputChannelVariants = nodeOutputChannel.SelectNodes("variants/variant");
                foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                {
                    var currentOutputChannelVariantId = GetAttribute(nodeVariant, "id");
                    var currentOutputChannelLanguage = GetAttribute(nodeVariant, "lang");


                    // Wrap the hierarchy in a wrapper node and append it to the overall document
                    var outputChannelElement = xmlOutputChannelHierarchies.CreateElement("output_channel");
                    SetAttribute(outputChannelElement, "type", outputChannelType);
                    SetAttribute(outputChannelElement, "id", currentOutputChannelVariantId);
                    SetAttribute(outputChannelElement, "lang", currentOutputChannelLanguage);

                    var nodeNameImported = xmlOutputChannelHierarchies.ImportNode(nodeVariant.SelectSingleNode("name"), true);
                    outputChannelElement.AppendChild(nodeNameImported);


                    // Retrieve the hierarchy from the Taxxor Data Store and append it to the document
                    if (includeHierarchies)
                    {
                        XmlDocument hierarchy = await FilingData.LoadHierarchy(projectVars.projectId, projectVars.versionId, projectVars.editorId, outputChannelType, currentOutputChannelVariantId, currentOutputChannelLanguage);
                        var nodeHierarchyRootImported = xmlOutputChannelHierarchies.ImportNode(hierarchy.DocumentElement, true);
                        outputChannelElement.AppendChild(nodeHierarchyRootImported);
                    }

                    xmlOutputChannelHierarchies.DocumentElement.AppendChild(outputChannelElement);
                }
            }

            if (includeAllItems && includeHierarchies)
            {
                // Add all the section elements to the overview
                var outputAllItems = xmlOutputChannelHierarchies.CreateElement("item_overview");
                XmlDocument xmlSourceDataOverview = await FilingData.SourceDataOverview(projectVars, true);
                var nodeSourceDataImported = xmlOutputChannelHierarchies.ImportNode(xmlSourceDataOverview.DocumentElement, true);
                outputAllItems.AppendChild(nodeSourceDataImported);
                xmlOutputChannelHierarchies.DocumentElement.AppendChild(outputAllItems);
            }

            // Append the metadata
            if (addFilingMetadata)
            {
                xmlOutputChannelHierarchies = await EnrichFilingHierarchyWithMetadata(xmlOutputChannelHierarchies, projectVars);
            }

            // Dump the hierarchy for inspection
            if (siteType == "local" || siteType == "dev")
            {
                await xmlOutputChannelHierarchies.SaveAsync($"{logRootPathOs}/_output-channel-hierarchy.xml", false);
            }

            return xmlOutputChannelHierarchies;

        }

        /// <summary>
        /// Calculate the connection string to the Redis service that will contain all the memory cache data
        /// </summary>
        /// <param name="env"></param>
        /// <returns></returns>
        public static string CalculateRedisHostName(IHostEnvironment env)
        {
            return "redissessioncache";
        }

        /// <summary>
        /// Prepares an XML document for automatic conversion to JSON and avoid "@foobar" notations in the JSON output
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        public static XmlDocument PrettyPrintForJsonConversion(XmlDocument xmlDoc)
        {
            return TransformXmlToDocument(xmlDoc, XslAttributesToNodes, null);
        }


        /// <summary>
        /// Adds an XHTML declaration to the XmlDocument
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        public static XmlDocument AddXhtmlNameSpace(XmlDocument xmlDoc)
        {
            xmlDoc.DocumentElement.SetAttribute("xmlns", "http://www.w3.org/1999/xhtml");
            return xmlDoc;
        }

        /// <summary>
        /// Removes an XHTML declaration from the XmlDocument
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        public static XmlDocument RemoveXhtmlNameSpace(XmlDocument xmlDoc)
        {
            xmlDoc.LoadXml(xmlDoc.OuterXml.Replace(" xmlns=\"http://www.w3.org/1999/xhtml\"", ""));
            return xmlDoc;
        }

        /// <summary>
        /// Utility for "print" media output (PDF, Word) to set the header nodes (h1, h2, ...) based on the hierarchical level derived from the output channel hierarchy
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static XmlDocument ConvertToHierarchicalHeaders(string html)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadHtml(html);
            return ConvertToHierarchicalHeaders(xmlDoc);
        }

        /// <summary>
        /// Utility for "print" media output (PDF, Word) to set the header nodes (h1, h2, ...) based on the hierarchical level derived from the output channel hierarchy
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="xsltStylesheet"></param>
        /// <returns></returns>
        public static XmlDocument ConvertToHierarchicalHeaders(XmlDocument xmlDoc, XslCompiledTransform xsltStylesheet = null, int customHierarchicalLevel = -1)
        {
            XsltArgumentList? args = null;
            if (customHierarchicalLevel > -1)
            {
                args = new XsltArgumentList();
                args.AddParam("hierarchical-level", "", customHierarchicalLevel);
            }

            if (xsltStylesheet == null)
            {
                return TransformXmlToDocument(xmlDoc, CalculateFullPathOs("cms_xsl_dynamic-header-levels"), args);
            }
            else
            {
                return TransformXmlToDocument(xmlDoc, xsltStylesheet, args);
            }
        }


        /// <summary>
        /// Removes steering attributes (like data-*) from the DOM
        /// </summary>
        /// <param name="xbrlConversionProperties"></param>
        /// <param name="debugRoutine"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage RemoveSteeringAttributes(XmlDocument xmlXbrl, XbrlConversionProperties xbrlConversionProperties, bool debugRoutine = false)
        {
            // Run the XmlDocument through the stylesheet
            // xmlXbrl.SaveAsync($"{logRootPathOs}/cleanup-before.xml", false, true).GetAwaiter().GetResult();
            var xsltArgumentList = new XsltArgumentList();
            xsltArgumentList.AddParam("channel", "", xbrlConversionProperties.Channel);
            xmlXbrl = TransformXmlToDocument(xmlXbrl, "sec_strip-attributes", xsltArgumentList);
            // xmlXbrl.SaveAsync($"{logRootPathOs}/cleanup-after.xml", false, true).GetAwaiter().GetResult();

            // Get the title of the document so that we can log something useful
            var documentTitle = xmlXbrl.SelectSingleNode("*//*[local-name()='h1' or local-name()='h2' or local-name()='h3']")?.InnerText ?? "unknown";

            // Return success
            return new TaxxorReturnMessage(true, $"Successfully removed attributes from document '{documentTitle}'", xmlXbrl);
        }

        /// <summary>
        /// Removes generated SVG of graphs from the content
        /// </summary>
        /// <param name="xmlDoc"></param>
        public static void RemoveRenderedGraphs(ref XmlDocument xmlDoc)
        {
            // Remove graph SVG
            var nodeListGraphs = xmlDoc.SelectNodes("//article//div[@class='highcharts-container']");
            foreach (XmlNode nodeGraph in nodeListGraphs)
            {
                RemoveXmlNode(nodeGraph);
            }
        }

        /// <summary>
        /// Removes empty elements from the XmlDocument
        /// </summary>
        /// <param name="xmlDoc"></param>
        public static void RemoveEmptyElements(ref XmlDocument xmlDoc)
        {
            // Remove useless empty div elements that might cause issues in the renderings
            var nodeListEmptyNodes = xmlDoc.SelectNodes("//*[(local-name() = 'div' or local-name() = 'span') and not(@class) and not(ancestor::table) and not(.//text()) and count(*) = 0]");
            foreach (XmlNode nodeEmptyElement in nodeListEmptyNodes)
            {
                RemoveXmlNode(nodeEmptyElement);
            }
        }

        /// <summary>
        /// Reworks table contents to assist the diff logic in the PDF Generator
        /// Injects unique strings and span elements in the table cells to help the diff generator to find matching strings
        /// In the PDF Generator, we remove these custom injected strings before generating the diff-pdf so the user never sees them...
        /// </summary>
        /// <param name="xmlDoc"></param>
        public static void PrepareTablesForDiffPdf(ref XmlDocument xmlDoc)
        {
            // var nodeListEmptyCells = xmlDoc.SelectNodes("//td//*[(local-name() = 'p') and not(.//text()) and count(*) = 0]");
            // foreach (XmlNode nodeEmptyCell in nodeListEmptyCells)
            // {
            //     nodeEmptyCell.InnerText = "-";
            // }

            string RenderUniqueString(int rowCount, int cellCount, int contentCount)
            {
                return $"*t123r{rowCount.ToString()}c{cellCount.ToString()}cc{contentCount.ToString()}*";
            }


            var reSpaces = new Regex(@"^\s+$");
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var nodeListTables = xmlDoc.SelectNodes("//table");
            var tableCount = 0;
            foreach (XmlNode nodeTable in nodeListTables)
            {
                tableCount++;
                var tableId = GetAttribute(nodeTable, "id") ?? $"t{tableCount.ToString()}";
                tableId = tableId.Replace("_", "").Replace("-", "");

                var nodeListTableDataRows = nodeTable.SelectNodes(".//tr");
                var rowCount = 0;
                foreach (XmlNode nodeTableDataRow in nodeListTableDataRows)
                {
                    rowCount++;

                    var nodeListTableDataCells = nodeTableDataRow.SelectNodes("*[local-name()='td' or local-name()='th']");

                    //
                    // => Wrap empty cells into a <span/> node to be sure that it get's picked up by the track changes/comparison logic
                    //
                    foreach (XmlNode nodeTableDataCell in nodeListTableDataCells)
                    {
                        var nodeListTableDataCellContent = nodeTableDataCell.InnerXml;
                        if (reSpaces.IsMatch(nodeListTableDataCellContent))
                        {
                            var nodeSpan = xmlDoc.CreateElementWithText("span", nodeListTableDataCellContent);
                            nodeSpan.SetAttribute("class", "tx-spacewrapper");
                            nodeTableDataCell.InnerXml = "";
                            nodeTableDataCell.AppendChild(nodeSpan);
                        }
                    }

                    var cellCount = 0;
                    foreach (XmlNode nodeTableDataCell in nodeListTableDataCells)
                    {
                        cellCount++;


                        var contentCount = 0;

                        //
                        // => Add markers in the table cell
                        //
                        var uniqueString = RenderUniqueString(rowCount, cellCount, contentCount);

                        // Add an attribute that we can use in the PDF Generator for finding this cell with special content and restore it's original value
                        // nodeTableDataCell.SetAttribute("data-debug", "1");
                        nodeTableDataCell.SetAttribute("data-strmatch", uniqueString);

                        // Make the content in the table cell unique by prepending the string with something unique
                        nodeTableDataCell.PrependChild(xmlDoc.CreateTextNode(uniqueString));

                        // Add a hidden span node that the diff logic will use to correctly match the content
                        var nodeSpan = xmlDoc.CreateElement("span");
                        nodeSpan.SetAttribute("style", "display:none");
                        nodeSpan.SetAttribute("data-utility", "trackchanges");
                        // nodeSpan.SetAttribute("data-debug-hidden", "1");
                        nodeSpan.InnerText = uniqueString;
                        nodeTableDataCell.AppendChild(nodeSpan);

                        //
                        // => Add markers in the table cell content nodes
                        //
                        var nodeListTableCellContent = nodeTableDataCell.SelectNodes("descendant::*[local-name()='p' or local-name()='span' or local-name()='div' or local-name()='b' or local-name()='i' or local-name()='li']");
                        foreach (XmlNode nodeTableCellContent in nodeListTableCellContent)
                        {
                            contentCount++;
                            if (nodeTableCellContent.GetAttribute("class")?.Contains("tx-spacewrapper") ?? false) continue;
                            if (nodeTableCellContent.HasAttribute("data-utility")) continue;



                            var uniqueStringInner = RenderUniqueString(rowCount, cellCount, contentCount);

                            // Add an attribute that we can use in the PDF Generator for finding this cell with special content and restore it's original value
                            // nodeTableCellContent.SetAttribute("data-debug", "2");
                            nodeTableCellContent.SetAttribute("data-strmatch", uniqueStringInner);

                            // Make the content in the table cell unique by prepending the string with something unique
                            nodeTableCellContent.PrependChild(xmlDoc.CreateTextNode(uniqueStringInner));

                            // Add a hidden span node that the diff logic will use to correctly match the content
                            var nodeSpanInner = xmlDoc.CreateElement("span");
                            nodeSpanInner.SetAttribute("style", "display:none");
                            nodeSpanInner.SetAttribute("data-utility", "trackchanges");
                            // nodeSpanInner.SetAttribute("data-debug-hidden", "2");
                            nodeSpanInner.InnerText = uniqueStringInner;
                            nodeTableCellContent.AppendChild(nodeSpanInner);
                        }

                        //
                        // => Remove attributes that we do not need
                        //
                        var nodeListAttrRemove = nodeTableDataCell.SelectNodes(".//*[@data-value]");
                        foreach (XmlNode nodeAttrRemove in nodeListAttrRemove)
                        {
                            RemoveAttribute(nodeAttrRemove, "data-value");
                        }

                        nodeListAttrRemove = nodeTableDataCell.SelectNodes(".//*[@data-fact-id]");
                        foreach (XmlNode nodeAttrRemove in nodeListAttrRemove)
                        {
                            RemoveAttribute(nodeAttrRemove, "data-fact-id");
                        }

                        nodeListAttrRemove = nodeTableDataCell.SelectNodes(".//*[@class='' or @class='selected']");
                        foreach (XmlNode nodeAttrRemove in nodeListAttrRemove)
                        {
                            RemoveAttribute(nodeAttrRemove, "class");
                        }

                    }
                }

            }



            // var nodeListDataFactId = xmlDoc.SelectNodes("//td//*[@data-fact-id]");
            // foreach (XmlNode nodeDataFactId in nodeListDataFactId)
            // {
            //     var dataFactId = GetAttribute(nodeDataFactId, "data-fact-id");
            //     RemoveAttribute(nodeDataFactId, "data-fact-id");
            //     //SetAttribute(nodeDataFactId, "id", dataFactId);
            //     var nodeSpan = xmlDoc.CreateElement("span");
            //     SetAttribute(nodeSpan, "style", "display: none");
            //     nodeSpan.InnerText = dataFactId;
            //     nodeDataFactId.AppendChild(nodeSpan);
            // }
        }

        /// <summary>
        /// Injects a Table of Contents and section numbering in the Xml data of the PDF or the full-HTML source of an output channel
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="projectVars"></param>
        /// <param name="outputChannelName">XBRL, IXBRL, PDF</param>
        public static void InsertTableOfContentsAndChapterNumbers(ref XmlDocument xmlDocument, ProjectVariables projectVars, string outputChannelName, PdfProperties pdfProperties = null)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            if (debugRoutine) xmlDocument.Save(logRootPathOs + "/_________________toc.xml");

            var nodeMetadataHierarchyItems = xmlDocument.SelectSingleNode("//metadata/hierarchy/items");

            //
            // Prepare the hierarchy for the table of contents in case we are rendering a PDF or Word document that is not a full document
            //
            if (pdfProperties != null)
            {
                if (pdfProperties.RenderScope == "single-section" && nodeMetadataHierarchyItems != null && pdfProperties.Sections != "all")
                {
                    // Create a list of section id's
                    var sectionIds = new List<string>();
                    if (pdfProperties.Sections.Contains(","))
                    {
                        sectionIds = pdfProperties.Sections.Split(',').ToList();
                    }
                    else
                    {
                        sectionIds.Add(pdfProperties.Sections);
                    }

                    // Always keep the root item
                    nodeMetadataHierarchyItems.SelectSingleNode("structured/item")?.SetAttribute("keep", "true");

                    // Mark all selected sections to keep
                    var containsTableOfContentsSection = false;
                    foreach (var sectionId in sectionIds)
                    {
                        var nodeHierarchyItem = nodeMetadataHierarchyItems.SelectSingleNode($"*//item[@id={GenerateEscapedXPathString(sectionId)}]");
                        if (nodeHierarchyItem != null)
                        {
                            nodeHierarchyItem.SetAttribute("keep", "true");
                            var dataReference = nodeHierarchyItem.GetAttribute("data-ref") ?? "";
                            if (dataReference == "toc.xml" || (xmlDocument.SelectSingleNode("*//ul[@class='toc-list' or @data-tableofcontentholder]") != null)) containsTableOfContentsSection = true;
                        }
                        else
                        {
                            appLogger.LogWarning($"Unable to find hierarchy node for sectionId: {sectionId}, stack-trace: {GetStackTrace()}");
                        }
                    }

                    // Remove the items from the hierarchy that we do not want to keep (only if we are not viewing the Table of Contents section itself)
                    if (containsTableOfContentsSection)
                    {
                        if (sectionIds.Count == 1)
                        {
                            //
                            // => To mimic what this section would look like in the full document, we need to use the first header text as the name of each section. We will be using the CMS metadata for this purpose.
                            //
                            var nodeProjectData = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]/data");
                            if (nodeProjectData != null)
                            {
                                var nodeListItems = nodeMetadataHierarchyItems.SelectNodes($"descendant::item");
                                foreach (XmlNode nodeItem in nodeListItems)
                                {
                                    var dataReference = nodeItem.GetAttribute("data-ref");
                                    if (!string.IsNullOrEmpty(dataReference))
                                    {
                                        var nodeMetadataContent = nodeProjectData.SelectSingleNode($"content[@datatype='sectiondata' and @ref='{dataReference}']");
                                        if (nodeMetadataContent != null)
                                        {
                                            var newTitle = RetrieveSectionTitleFromMetadataCache(nodeMetadataContent, projectVars.outputChannelVariantLanguage);
                                            if (!string.IsNullOrEmpty(newTitle))
                                            {
                                                var nodeLinkName = nodeItem.SelectSingleNode("web_page/linkname");
                                                if (nodeLinkName != null)
                                                {
                                                    nodeLinkName.InnerText = newTitle;
                                                }
                                                else
                                                {
                                                    appLogger.LogWarning($"Update Toc titles: Unable to find linkname node for data-ref: {dataReference}, stack-trace: {GetStackTrace()}");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            appLogger.LogWarning($"Update Toc titles: Unable to find metadata node for data-ref: {dataReference}, stack-trace: {GetStackTrace()}");
                                        }
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Update Toc titles: Unable to find item id for sectionId: {nodeItem.OuterXml}, stack-trace: {GetStackTrace()}");
                                    }
                                }


                                nodeProjectData.SetAttribute("name", sectionIds[0]);
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to inject header content into ToC as we were unable to find project data for project id: {projectVars.projectId}");
                            }

                        }
                        else
                        {
                            //
                            // => Mark the items as non visble if they are not part of the current article selection in the PDF
                            //
                            var nodeListHierarchyItemsToHide = nodeMetadataHierarchyItems.SelectNodes("*//item[not(@keep)]");
                            foreach (XmlNode nodeItemToHide in nodeListHierarchyItemsToHide)
                            {
                                nodeItemToHide.SetAttribute("hide", "true");
                            }
                        }
                    }


                }
            }

            if (debugRoutine) xmlDocument.Save(logRootPathOs + "/_________________toc-stripped-hierarchy.xml");

            //
            // => Generate the TOC's and potentially include it in the content
            //
            if (nodeMetadataHierarchyItems != null)
            {
                var nodeListTocPlaceholder = xmlDocument.SelectNodes("//article//ul[@class='toc-list' or @data-tableofcontentholder]");
                if (nodeListTocPlaceholder.Count > 0)
                {
                    var nodeTocLocation = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/locations/location[@id='toc-stylesheet']");
                    if (nodeTocLocation != null)
                    {

                        var tocXsltPathOsBase = CalculateFullPathOs(nodeTocLocation);

                        // Find the most specific XSLT for rendering the TOC
                        var tocXsltPathToUse = tocXsltPathOsBase;
                        var tocXsltPathCustomerSpecific = tocXsltPathOsBase.Replace(".xsl", $"-{TaxxorClientId}.xsl");
                        if (File.Exists(tocXsltPathCustomerSpecific))
                        {
                            tocXsltPathToUse = tocXsltPathCustomerSpecific;
                        }
                        else if (!File.Exists(tocXsltPathOsBase))
                        {
                            appLogger.LogWarning($"Inclusion of a Table of Contents skipped because the XSLT stylesheet ({tocXsltPathOsBase}) could not be found");
                        }

                        appLogger.LogDebug($"Using TOC Stylesheet: {tocXsltPathToUse}");

                        var tocCounter = 0;

                        foreach (XmlNode nodeTocPlaceholder in nodeListTocPlaceholder)
                        {
                            var tocClass = nodeTocPlaceholder.GetAttribute("class") ?? "";

                            // Render the TOC
                            var xsltArgs = new XsltArgumentList();
                            xsltArgs.AddParam("post-processing-format", "", outputChannelName);
                            xsltArgs.AddParam("toc-type", "", nodeTocPlaceholder.GetAttribute("data-toctype") ?? "regular");
                            xsltArgs.AddParam("toc-maxdepth", "", nodeTocPlaceholder.GetAttribute("data-tocmaxdepth") ?? "-1");
                            xsltArgs.AddParam("source-article-id", "", nodeTocPlaceholder.GetAttribute("data-sourcearticleid") ?? nodeTocPlaceholder.SelectSingleNode("ancestor::article")?.GetAttribute("id") ?? "");

                            var xmlToc = TransformXmlToDocument(xmlDocument, tocXsltPathToUse, xsltArgs);

                            if (debugRoutine) xmlToc.Save($"{logRootPathOs}/00{tocCounter}--toc.xml");

                            // Assure that we do not have a list without list items
                            var nodeListEmptyList = xmlToc.SelectNodes("//ul[not(li)] | //div[not(p)]");
                            if (nodeListEmptyList.Count > 0) RemoveXmlNodes(nodeListEmptyList);

                            if (xmlToc.DocumentElement != null)
                            {
                                var nodeTocImported = xmlDocument.ImportNode(xmlToc.DocumentElement, true);
                                if (tocClass.Length > 0) nodeTocImported.SetAttribute("class", tocClass);
                                ReplaceXmlNode(nodeTocPlaceholder, nodeTocImported);
                            }
                            else
                            {
                                if (debugRoutine) xmlDocument.Save($"{logRootPathOs}/00{tocCounter}--toc-source.xml");
                                appLogger.LogWarning("Unable to include Table of Contents because the system could not render a chapter reference");
                            }

                            // if (debugRoutine) xmlDocument.Save(logRootPathOs + "/_________________toc-included.xml");

                            tocCounter++;
                        }

                        appLogger.LogDebug($"Rendered {tocCounter} table of contents");
                    }
                    else
                    {
                        appLogger.LogWarning("TOC stylesheet not defined!");
                    }
                }


            }


            //
            // => Add chapter numbering in the headers by using the @data-tocnumber attribute that is returned for each article by the Taxxor Document Store
            //
            var sectionNumberingXpath = Extensions.GenerateArticleXpathForAddingSectionNumbering();
            var nodeListArticles = xmlDocument.SelectNodes(sectionNumberingXpath);
            foreach (XmlNode nodeArticle in nodeListArticles)
            {
                // Retrieve the section number to insert
                var sectionNumber = nodeArticle.GetAttribute("data-tocnumber");

                // Find the highest header in the article and insert the section number there
                var nodeListHeaders = nodeArticle.SelectNodes($".//*[local-name()='h1' or local-name()='h2']");
                if (nodeListHeaders.Count > 0)
                {

                    var nodeHeader = nodeListHeaders.Item(0);
                    var nodeSectionHeaderNumber = xmlDocument.CreateElement("span");

                    // Define specific CSS class for the chapter number
                    var chapterNumberCssClass = (sectionNumber.ToLower().Contains("note")) ? "note" : "default";

                    SetAttribute(nodeSectionHeaderNumber, "class", $"nr {chapterNumberCssClass}");
                    nodeSectionHeaderNumber.InnerText = (chapterNumberCssClass == "default") ? sectionNumber : sectionNumber.Replace("note ", "");
                    nodeHeader.PrependChild(nodeSectionHeaderNumber);
                }
                else
                {
                    var articleId = nodeArticle.GetAttribute("id") ?? "unknown";

                    appLogger.LogWarning($"Could not find any article headers to insert the section number in. articleId: {articleId}, sectionNumber: {sectionNumber}, sectionNumberingXpath: {sectionNumberingXpath}");
                }
            }


            //
            // => Add section numbers to links in wrapper nodes with @data-linkstyle='include-section-numbers'
            //
            if (nodeMetadataHierarchyItems != null)
            {
                var nodeListLinks = xmlDocument.SelectNodes($"//*[@data-linkstyle='include-section-numbers']//{RenderInternalLinkXpathSelector()}");
                foreach (XmlNode nodeLink in nodeListLinks)
                {
                    var linkTarget = GetAttribute(nodeLink, "href");

                    if (string.IsNullOrEmpty(linkTarget))
                    {
                        appLogger.LogWarning($"Could not add chapternumber to link ({nodeLink.OuterXml}) because it does not contain a target reference");
                    }
                    else
                    {
                        if (linkTarget.StartsWith("#"))
                        {
                            // Get the site structure ID from the link
                            var targetArticleId = linkTarget.Replace("#", "");

                            // Test if we can find the item in the hierarchy
                            var xPathForHierarchy = $"//item[@data-articleids='{targetArticleId}' or @id='{targetArticleId}' or contains(@data-articleids, '{targetArticleId},')]";
                            var nodeItem = nodeMetadataHierarchyItems.SelectSingleNode(xPathForHierarchy);
                            if (nodeItem == null)
                            {
                                if (IsInternalLinkRelevant(nodeLink, projectVars.outputChannelVariantId))
                                {
                                    // Retrieve the article ID where we have found this link
                                    var articleId = "unknown";
                                    var nodeArticle = nodeLink.SelectSingleNode("ancestor::article");
                                    if (nodeArticle != null) articleId = nodeArticle.GetAttribute("id") ?? "unknown";

                                    appLogger.LogError($"Could not add chapter number to link ({nodeLink.OuterXml}) in article ({articleId}) because the target {targetArticleId} could not be located in the hierarchy");
                                }
                            }
                            else
                            {
                                var sectionNumber = nodeItem.GetAttribute("data-tocnumber");
                                if (!string.IsNullOrEmpty(sectionNumber))
                                {
                                    // TODO: String should come from translation file
                                    var prefix = "Chapter ";

                                    if (sectionNumber.StartsWith("note"))
                                    {
                                        prefix = "";
                                        sectionNumber = sectionNumber.ToTitleCase();
                                        // Console.WriteLine(nodeLink.OuterXml);
                                    }

                                    nodeLink.InnerXml = $"{prefix}{sectionNumber} – {nodeLink.InnerXml}";
                                }
                                else
                                {
                                    if (!nodeLink.InnerText.ToLower().Contains("exhibit"))
                                    {
                                        // Retrieve the article ID where we have found this link
                                        var articleId = "unknown";
                                        var nodeArticle = nodeLink.SelectSingleNode("ancestor::article");
                                        if (nodeArticle != null) articleId = nodeArticle.GetAttribute("id") ?? "unknown";

                                        appLogger.LogWarning($"Could not add chapternumber to link ({nodeLink.OuterXml}) in article ({articleId}) because the item in the hierarchy does not contain a section number");
                                    }
                                }
                            }
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Adds a comment in empty elements that seem to have a function in the DOM
        /// </summary>
        /// <param name="xmlDoc"></param>
        public static void AddCommentInEmptyElements(ref XmlDocument xmlDoc)
        {
            // Search for empty nodes with a class
            foreach (XmlNode nodeEmptyElement in xmlDoc.SelectNodes("descendant::*[(local-name() = 'div' or local-name() = 'span' or local-name() = 'b' or local-name() = 'br' or local-name() = 'i' or local-name() = 'ul') and not(*) and not(normalize-space())]"))
            {
                var nodeComment = xmlDoc.CreateComment(".");
                nodeEmptyElement.AppendChild(nodeComment);
            }

            // var nodeListEmptyDivs = xmlDoc.SelectNodes("//*[(local-name() = 'div' or local-name() = 'span' or local-name() = 'b' or local-name() = 'br' or local-name() = 'i' or local-name() = 'ul') and @class and not(.//text()) and count(*) = 0]");
            // foreach (XmlNode nodeEmptyElement in nodeListEmptyDivs)
            // {
            //     var nodeComment = xmlDoc.CreateComment(".");
            //     nodeEmptyElement.AppendChild(nodeComment);
            // }
        }

        /// <summary>
        /// Converts the content that will be used to render an output channel to contain only tables
        /// </summary>
        /// <param name="xmlContent"></param>
        /// <param name="outputChannelType"></param>
        public static void OutputTablesOnly(ref XmlDocument xmlContent, string outputChannelType = "pdf")
        {
            var stylesheetId = "xsl_outputchannel-tables-only";
            var xsltStylesheet = PdfXslCache.Get(stylesheetId);
            if (xsltStylesheet == null)
            {
                HandleError("Could not load stylesheet", $"stylesheetId: {stylesheetId}, stack-trace: {GetStackTrace()}");
            }

            XsltArgumentList xslParams = new XsltArgumentList();
            xslParams.AddParam("output-channel-type", "", outputChannelType);
            xmlContent = TransformXmlToDocument(xmlContent, xsltStylesheet, xslParams);
        }

        /// <summary>
        /// Renders a list of reporting periods that can be used in the UI
        /// </summary>
        /// <param name="shortList">Renders a short list of reporting periods (not including the historical ones)</param>
        /// <param name="includeNone">Adds an empty "none" element so it can be used in select boxes</param>
        /// <param name="sortAscending">Sorts the list with the most recent year on top</param>
        /// <returns></returns>
        public static XmlDocument RenderReportingPeriods(bool shortList = true, bool includeNone = false, bool sortAscending = true, string referencePeriod = null, string reportTypeId = null)
        {
            var xmlReportingPeriods = new XmlDocument();
            var nodeRoot = xmlReportingPeriods.AppendChild(xmlReportingPeriods.CreateElement("reporting_periods"));

            // Setup base date fragments to work with
            ProjectPeriodProperties projectPeriodProperties;
            var currentYear = DateTime.Now.ToString("yyyy");
            var currentYearInt = Int32.Parse(currentYear);
            var currentMonth = DateTime.Now.ToString("MM");
            var currentMonthInt = Int32.Parse(currentMonth);
            if (!string.IsNullOrEmpty(referencePeriod))
            {
                projectPeriodProperties = new ProjectPeriodProperties(referencePeriod);
                currentYearInt = projectPeriodProperties.CurrentProjectYear;
                currentYear = currentYearInt.ToString();
                currentMonthInt = projectPeriodProperties.CurrentProjectMonth;
                currentMonth = currentMonthInt.ToString("D2");
            }

            switch (reportTypeId)
            {
                case var typeAr when typeAr != null && typeAr.Contains("annual-report"):
                case var typeQr when typeQr != null && typeQr.Contains("quarterly-report"):
                case "esg-report":
                case "investor-update":
                case "philips-investor-update":
                case null:
                    {
                        // Create a list of years
                        List<string> renderYears = [];
                        if (sortAscending)
                        {
                            if (!shortList)
                            {
                                for (int i = 12; i > 2; i--)
                                {
                                    renderYears.Add((currentYearInt - i).ToString());
                                }
                            }

                            renderYears.Add((currentYearInt - 1).ToString());
                            renderYears.Add(currentYearInt.ToString());
                            renderYears.Add((currentYearInt + 1).ToString());
                        }
                        else
                        {
                            renderYears.Add((currentYearInt + 1).ToString());
                            renderYears.Add(currentYearInt.ToString());
                            renderYears.Add((currentYearInt - 1).ToString());

                            if (!shortList)
                            {
                                for (int i = 2; i < 12; i++)
                                {
                                    renderYears.Add((currentYearInt - i).ToString());
                                }
                            }
                        }

                        var periodTypes = new List<string>();
                        switch (reportTypeId)
                        {
                            case null:
                                {
                                    string[] _periodTypes = { "q1", "q2", "q3", "q4", "ar" };
                                    periodTypes.AddRange(_periodTypes);
                                }
                                break;

                            case var typeQrNested when typeQrNested != null && typeQrNested.Contains("quarterly-report"):
                            case "investor-update":
                            case "philips-investor-update":
                                {
                                    string[] _periodTypes = { "q1", "q2", "q3", "q4" };
                                    periodTypes.AddRange(_periodTypes);
                                }
                                break;

                            case var typeArNested when typeArNested != null && typeArNested.Contains("annual-report"):
                            case "esg-report":
                                {
                                    periodTypes.Add("ar");
                                }
                                break;
                        }

                        foreach (string year in renderYears)
                        {
                            var nodeReportingYear = xmlReportingPeriods.CreateElement("reporting_year");
                            nodeReportingYear.SetAttribute("label", year);

                            var shortYear = year.Substring(year.Length - 2);

                            foreach (string periodType in periodTypes)
                            {
                                var nodePeriod = xmlReportingPeriods.CreateElement("period");
                                var calculatedPeriodId = $"{periodType}{shortYear}";
                                nodePeriod.SetAttribute("id", calculatedPeriodId);
                                if (calculatedPeriodId == referencePeriod) nodePeriod.SetAttribute("current", "true");
                                nodePeriod.InnerText = $"{periodType.ToUpper()} {year}";
                                nodeReportingYear.AppendChild(nodePeriod);
                            }

                            xmlReportingPeriods.DocumentElement.AppendChild(nodeReportingYear);
                        }
                    }
                    break;

                case "monthly-report":
                    {
                        var calculationDate = new DateTime(currentYearInt, currentMonthInt, DateTime.DaysInMonth(currentYearInt, currentMonthInt));

                        // Create a list of months
                        List<DateTime> monthsDates = [];
                        if (sortAscending)
                        {
                            if (!shortList)
                            {
                                for (int i = 12; i > 2; i--)
                                {

                                    monthsDates.Add(calculationDate.AddMonths((0 - i)));
                                }
                            }


                            monthsDates.Add(calculationDate.AddMonths(-1));
                            monthsDates.Add(calculationDate);
                            monthsDates.Add(calculationDate.AddMonths(1));
                            monthsDates.Add(calculationDate.AddMonths(2));
                            monthsDates.Add(calculationDate.AddMonths(3));


                        }
                        else
                        {
                            monthsDates.Add(calculationDate.AddMonths(3));
                            monthsDates.Add(calculationDate.AddMonths(2));
                            monthsDates.Add(calculationDate.AddMonths(1));
                            monthsDates.Add(calculationDate);
                            monthsDates.Add(calculationDate.AddMonths(-1));



                            if (!shortList)
                            {
                                for (int i = 2; i < 12; i++)
                                {
                                    monthsDates.Add(calculationDate.AddMonths((0 - i)));


                                }
                            }
                        }

                        // Build the XML nodes
                        foreach (DateTime date in monthsDates)
                        {
                            var shortYear = date.Year.ToString().Substring(date.Year.ToString().Length - 2);
                            var monthLeadingZero = date.Month.ToString("D2");
                            var calculatedPeriodId = $"m{monthLeadingZero}{shortYear}";

                            var nodePeriod = xmlReportingPeriods.CreateElement("period");
                            nodePeriod.SetAttribute("id", calculatedPeriodId);

                            if (calculatedPeriodId == referencePeriod) nodePeriod.SetAttribute("current", "true");

                            nodePeriod.InnerText = $"{date.ToString("MMMM")}, {date.Year.ToString()}";
                            xmlReportingPeriods.DocumentElement.AppendChild(nodePeriod);
                        }

                        appLogger.LogInformation($"monthsDates.Count: {monthsDates.Count}");
                    }

                    break;

                default:
                    appLogger.LogWarning($"Unsupported report type: {reportTypeId}. stack-trace: {GetStackTrace()}");
                    break;
            }


            if (includeNone)
            {
                var nodeMisc = xmlReportingPeriods.CreateElement("miscellaneous");
                nodeMisc.SetAttribute("label", "Miscellaneous");

                var nodePeriod = xmlReportingPeriods.CreateElement("period");
                SetAttribute(nodePeriod, "id", $"none");
                nodePeriod.InnerText = $"None";
                nodeMisc.AppendChild(nodePeriod);

                xmlReportingPeriods.DocumentElement.AppendChild(nodeMisc);
            }

            return xmlReportingPeriods;
        }

        /// <summary>
        /// Central function for calculating core paths (cmsDataRootPath, cmsDataRootPathOs, etc.) from a reasonably filled ProjectVariables object
        /// </summary>
        /// <param name="projectVars"></param>
        public static void FillCorePathsInProjectVariables(ref ProjectVariables projectVars)
        {
            if (projectVars.projectId != null)
            {
                var nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectVars.projectId)}]");

                if (nodeProject != null)
                {
                    projectVars.reportTypeId = nodeProject.GetAttribute("report-type");
                    if (!string.IsNullOrEmpty(projectVars.reportTypeId))
                    {
                        projectVars.editorId = RetrieveEditorIdFromReportId(projectVars.reportTypeId);
                        if (!string.IsNullOrEmpty(projectVars.editorId))
                        {
                            projectVars.projectRootPath = CmsRootPath + RetrieveNodeValueIfExists("/configuration/editors/editor[@id='" + projectVars.editorId + "']/path", xmlApplicationConfiguration);
                            projectVars.projectRootPathOs = websiteRootPathOs + projectVars.projectRootPath;
                        }
                    }
                }
                else
                {
                    appLogger.LogWarning($"Could not find project configuration for project id {projectVars.projectId} - 1");
                }
            }

            // Retrieve current year /configuration/cms_projects[1]/cms_project[1]/reporting_period[1]
            projectVars.reportingPeriod = RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/reporting_period", xmlApplicationConfiguration);

            // Default language is the first output channel language defined in the configurationc
            if (!string.IsNullOrEmpty(projectVars.editorId))
            {
                projectVars.outputChannelDefaultLanguage = RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels[1]/output_channel[1]/variants[1]/variant[1]/@lang", xmlApplicationConfiguration);
            }
        }

        /// <summary>
        /// Retrieves a list of data references (XML file names) for a CMS project
        /// </summary>
        /// <param name="projectId">Project ID that the routine should look into</param>
        /// <param name="onlyInUseReferences">Only return data references of content that is actually in use by any of the output channels</param>
        /// <returns></returns>
        public static async Task<List<string>> RetrieveDataReferences(string projectId, bool onlyInUseReferences = false)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var dataReferences = new List<string>();

            // - Use the cache of the Taxxor Document Store to loop through all the content files

            //
            // => Retrieve the metadata cache of all the files used in this project
            //
            var metadataRetrieveResult = await RetrieveCmsMetadata(projectId);
            XmlDocument xmlCmsContentMetadata = new XmlDocument();
            if (!metadataRetrieveResult.Success)
            {
                appLogger.LogError($"ERROR: unable to retrieve metadata. {metadataRetrieveResult.ToString()}");
                return dataReferences;
            }
            xmlCmsContentMetadata.ReplaceContent(metadataRetrieveResult.XmlPayload);

            var xPathForDataReferences = "/projects/cms_project/data/content[@datatype='sectiondata']";
            if (onlyInUseReferences) xPathForDataReferences = "/projects/cms_project/data/content[@datatype='sectiondata' and metadata/entry[@key='inuse']/text()='true']";

            var nodeListContentDataFiles = xmlCmsContentMetadata.SelectNodes(xPathForDataReferences);
            foreach (XmlNode nodeContentDataFile in nodeListContentDataFiles)
            {
                var dataRef = nodeContentDataFile.GetAttribute("ref");
                if (!string.IsNullOrEmpty(dataRef))
                {
                    if (!dataReferences.Contains(dataRef)) dataReferences.Add(dataRef);
                }
                else
                {
                    appLogger.LogWarning("Data reference was empty");
                }
            }

            return dataReferences;
        }


        // private static void GenerateDummyJpegAt(string outputPath, string nameToEmbed, int width, int height)
        // {
        //     using(var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
        //     {
        //         BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);
        //         byte[] noise = new byte[data.Width * data.Height * 3];
        //         new Random().NextBytes(noise); // note that if you do that in a loop or from multiple threads - you may want to store this random in outside variable
        //         Marshal.Copy(noise, 0, data.Scan0, noise.Length);
        //         bmp.UnlockBits(data);
        //         using(var g = Graphics.FromImage(bmp))
        //         {
        //             // draw white rectangle in the middle
        //             g.FillRectangle(Brushes.White, 0, height / 2 - 20, width, 40);
        //             var fmt = new StringFormat();
        //             fmt.Alignment = StringAlignment.Center;
        //             fmt.LineAlignment = StringAlignment.Center;
        //             // draw text inside that rectangle
        //             g.DrawString(nameToEmbed, SystemFonts.DefaultFont, Brushes.Black, new RectangleF(0, 0, bmp.Width, bmp.Height), fmt);
        //         }
        //         using(var fs = File.Create(outputPath))
        //         {
        //             bmp.Save(fs, System.Drawing.Imaging.ImageFormat.Jpeg);
        //         }
        //     }
        // }


        /// <summary>
        /// Determines if the current user has basic view access to a Taxxor CMS project
        /// </summary>
        /// <param name="currentProjectId"></param>
        /// <returns></returns>
        public static async Task<bool> UserHasAccessToProject(string userId, string currentProjectId)
        {
            var memoryCacheKey = $"{userId}-{currentProjectId}";
            var hasAccess = false;


            var userHasAccessToProject = RetrieveCacheContent<string>(memoryCacheKey);
            if (string.IsNullOrEmpty(userHasAccessToProject))
            {
                TaxxorPermissions? permissions = await Taxxor.ConnectedServices.AccessControlService.RetrievePermissionsForResource($"get__taxxoreditor__cms_project-details__{currentProjectId}", false, false);
                if (permissions == null)
                {
                    hasAccess = false;
                }
                else
                {
                    hasAccess = permissions.View;
                }

                // Console.WriteLine("*** Setting cache ***");
                SetMemoryCacheItem(memoryCacheKey, ((hasAccess) ? "true" : "false"), TimeSpan.FromMinutes(5));
            }
            else
            {
                // Console.WriteLine("*** Using cache ***");
                return (userHasAccessToProject == "true");
            }


            return hasAccess;
        }

        /// <summary>
        /// Retrieves the version of the static assets service
        /// </summary>
        public static void UpdateStaticAssetsVersion()
        {
            var debugRoutine = false;

            //
            // => Try to retrieve the metadata from the static assets server
            //
            var domainType = (siteType == "prod") ? "prod" : "prev";
            var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{domainType}']");
            var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
            var protocol = (nodeCurrentDomain.GetAttribute("ssl") == "true") ? "https" : "http";
            var originUrl = $"{protocol}://{currentDomainName}";
            var remoteMetadataRetrieved = false;
            var staticAssetsVersion = "";

            if (!string.IsNullOrEmpty(StaticAssetsLocation))
            {
                using (var httpClient = new HttpClient())
                {
                    try
                    {
                        httpClient.DefaultRequestHeaders.Add("Origin", originUrl);
                        httpClient.Timeout = TimeSpan.FromSeconds(5);
                        if (debugRoutine)
                        {
                            Console.WriteLine("--- STATIC ASSETS METADATA ---");
                            Console.WriteLine($"- originUrl: {originUrl}");
                            Console.WriteLine($"- reguestUrl: {StaticAssetsLocation}/meta.json");
                        }


                        string jsonString = httpClient.GetStringAsync($"{StaticAssetsLocation}/meta.json").GetAwaiter().GetResult();


                        if (debugRoutine) Console.WriteLine("- jsonString:\n" + jsonString);

                        // Parse the JSON response
                        using (JsonDocument document = JsonDocument.Parse(jsonString))
                        {
                            // Try to extract the version field
                            if (document.RootElement.TryGetProperty("version", out JsonElement versionElement))
                            {
                                string? version = versionElement.GetString();
                                if (!string.IsNullOrEmpty(version))
                                {
                                    // Update the StaticAssetsVersion with the value from the server
                                    staticAssetsVersion = version;
                                    remoteMetadataRetrieved = true;
                                    if (debugRoutine) Console.WriteLine($"- Using version from server: {version}");
                                }
                            }
                        }

                        if (debugRoutine) Console.WriteLine("------------------------------");
                    }
                    catch (HttpRequestException ex)
                    {
                        // Handle server unreachable or file not found
                        appLogger.LogWarning($"Could not retrieve static assets version: {ex.Message}");
                    }
                    catch (TaskCanceledException ex)
                    {
                        // Handle timeout
                        appLogger.LogWarning($"Timeout while retrieving static assets version: {ex.Message}");
                    }
                    catch (JsonException ex)
                    {
                        // Handle invalid JSON
                        appLogger.LogWarning($"Invalid JSON in static assets version file: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        // Handle any other unexpected errors
                        appLogger.LogError(ex, $"Error retrieving static assets version: {ex.Message}");
                    }
                }
            }
            else
            {
                appLogger.LogWarning("Static assets location is not set");
            }


            //
            // => Update global variable
            //
            if (remoteMetadataRetrieved)
            {
                StaticAssetsVersion = staticAssetsVersion;
            }
            else
            {
                StaticAssetsVersion = RandomString(8);
            }

            appLogger.LogInformation($"- StaticAssetsVersion: {StaticAssetsVersion}");
        }

        /// <summary>
        /// Strips an HTML text from potential XSS elements
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string RemoveXssSensitiveElements(string text)
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadHtml($"<?xml version='1.0' encoding='utf-8'?><root>{text}</root>");
                RemoveXssSensitiveElements(ref xmlDoc);
                return xmlDoc.DocumentElement.InnerXml;
            }
            catch (Exception ex)
            {
                appLogger.LogWarning(ex, "Failed to strip potential XSS elements because it could not be converted into XML");
                return text;
            }
        }

        /// <summary>
        /// Retrieves the path to the DLL file containing editor customizations based on the development environment.
        /// </summary>
        /// <param name="isDevelopmentEnvironment">A boolean indicating whether the current environment is a development environment.</param>
        /// <returns>
        /// A string representing the path to the DLL file containing editor customizations.
        /// If the customizations folder does not exist, an error message is printed to the console and the function returns null.
        /// </returns>
        public static string RetrieveEditorCustomizationsDllPath(bool isDevelopmentEnvironment)
        {
            // Retrieve the major version of the .NET runtime
            var dotnetMajorVerion = Environment.Version.Major.ToString();

            // Determine the architecture of the current process
            var achitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().Contains("arm", StringComparison.CurrentCultureIgnoreCase) ? "arm" : "amd";

            // Define the base folder path for customizations based on the development environment
            var customizationsBaseFolderPathOs = isDevelopmentEnvironment ?
                $"{Path.GetDirectoryName(Environment.CurrentDirectory)}/customercode" :
                    $"{Path.GetDirectoryName(Environment.CurrentDirectory)}/customercode";

            // Check if the customizations base folder exists
            if (Directory.Exists(customizationsBaseFolderPathOs))
            {
                // Construct the test folder path by appending the HOSTSERVER_ID environment variable
                var testFolderPathOs = $"{customizationsBaseFolderPathOs}/{Environment.GetEnvironmentVariable("HOSTSERVER_ID")}";

                // Determine the customer folder name based on the existence and contents of the test folder
                var customerFolderName = Directory.Exists(testFolderPathOs) && Directory.GetDirectories(testFolderPathOs).Length > 0 ?
                    Environment.GetEnvironmentVariable("HOSTSERVER_ID") :
                        "default";

                // Construct the full path to the DLL file containing editor customizations
                return $"{customizationsBaseFolderPathOs}/{customerFolderName}/net{dotnetMajorVerion}.0/{achitecture}/CustomerCode.dll";
            }
            else
            {
                // If the customizations base folder does not exist, print an error message and return null
                Console.WriteLine($"ERROR: Customizations folder not found: {customizationsBaseFolderPathOs}");
            }
            return null;
        }


        /// <summary>
        /// Class that can be used to cache XSLT stylesheets which are re-used a lot in the code
        /// </summary>
        public class XslStylesheetCache
        {

            public RequestVariables ReqVars { get; set; } = null;

            private Dictionary<string, XslCompiledTransform> _xsltObjects = [];

            public XslStylesheetCache() { }

            public XslStylesheetCache(RequestVariables reqVars)
            {
                this.ReqVars = reqVars;
            }

            public XslStylesheetCache(List<string> styleSheetIds)
            {
                Add(styleSheetIds);
            }

            public XslStylesheetCache(List<string> styleSheetIds, RequestVariables reqVars)
            {
                this.ReqVars = reqVars;
                Add(styleSheetIds);
            }

            /// <summary>
            /// Creates a new XSLT object by loading it from the disk
            /// </summary>
            /// <param name="location"></param>
            /// <returns></returns>
            private XslCompiledTransform _createXsl(string location)
            {
                var xslPathOs = (location.Contains("/")) ? location : CalculateFullPathOs(location, this.ReqVars);
                if (string.IsNullOrEmpty(xslPathOs))
                {
                    appLogger.LogError($"Unable to load XSL using location {location}");
                    return null;
                }

                if (!File.Exists(xslPathOs))
                {
                    appLogger.LogError($"Unable to load XSL from path {xslPathOs} bacause it does not exist. location: {location}");
                    return null;
                }

                XsltSettings xsltSettings = new XsltSettings();
                xsltSettings.EnableDocumentFunction = true;

                // Required to load external stylesheets via xsl:include ...
                XmlUrlResolver resolver = new XmlUrlResolver();
                resolver.Credentials = CredentialCache.DefaultCredentials;

                // Load the style sheet.
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(xslPathOs, xsltSettings, resolver);

                return xslCompiledTransform;
            }

            /// <summary>
            /// Adds a new XSLT stylesheet in the cache
            /// </summary>
            /// <param name="stylesheetId"></param>
            public void Add(string stylesheetId)
            {
                var stylesheetIds = new List<string>
                {
                    stylesheetId
                };
                this.Add(stylesheetIds);
            }

            /// <summary>
            /// Adds a new XSLT stylesheet in the cache
            /// </summary>
            /// <param name="stylesheetId"></param>
            /// <param name="xsltPathOs"></param>
            public void Add(string stylesheetId, string xsltPathOs)
            {
                if (this._xsltObjects.ContainsKey(stylesheetId))
                {
                    appLogger.LogWarning($"Cache already contains a reference to {stylesheetId}");
                }
                else
                {
                    this._xsltObjects.Add(stylesheetId, _createXsl(xsltPathOs));
                }
            }

            /// <summary>
            /// Adds a new XSLT stylesheet in the cache
            /// </summary>
            /// <param name="styleSheetIds"></param>
            public void Add(List<string> styleSheetIds)
            {
                foreach (var stylesheetId in styleSheetIds)
                {
                    if (this._xsltObjects.ContainsKey(stylesheetId))
                    {
                        appLogger.LogWarning($"Cache already contains a reference to {stylesheetId}");
                    }
                    else
                    {
                        this._xsltObjects.Add(stylesheetId, _createXsl(stylesheetId));
                    }
                }
            }

            /// <summary>
            /// Removes an XSLT from the cache
            /// </summary>
            /// <param name="stylesheetId"></param>
            public void Remove(string stylesheetId)
            {
                if (this._xsltObjects.ContainsKey(stylesheetId))
                {
                    this._xsltObjects.Remove(stylesheetId);
                }
                else
                {
                    appLogger.LogWarning($"Unable to find {stylesheetId} to remove from the XSLT cache");
                }
            }

            /// <summary>
            /// Tests if the cache contains any objects
            /// Can be used as a trigger to reload the objects in the cache
            /// </summary>
            /// <returns></returns>
            public bool ContainsObjects()
            {
                return this._xsltObjects.Count > 0;
            }

            /// <summary>
            /// Retrieves a XSLT stylesheet from the cache
            /// </summary>
            /// <param name="xsltId"></param>
            /// <returns></returns>
            public XslCompiledTransform Get(string xsltId)
            {
                if (this._xsltObjects.ContainsKey(xsltId))
                {
                    return this._xsltObjects[xsltId];
                }
                else
                {
                    return null;
                }
            }
        }

        public static GrpcProjectVariables ConvertToGrpcProjectVariables(ProjectVariables projectVars)
        {
            return new GrpcProjectVariables
            {
                UserId = projectVars.currentUser?.Id ?? "",
                ProjectId = projectVars.projectId ?? "",
                VersionId = projectVars.versionId ?? "",
                Did = projectVars.did ?? "",
                EditorId = projectVars.editorId ?? "",
                EditorContentType = projectVars.editorContentType ?? "",
                ReportTypeId = projectVars.reportTypeId ?? "",
                OutputChannelType = projectVars.outputChannelType ?? "",
                OutputChannelVariantId = projectVars.outputChannelVariantId ?? "",
                OutputChannelVariantLanguage = projectVars.outputChannelVariantLanguage ?? "",
                // User information for Git commits and audit trails
                UserFirstName = projectVars.currentUser?.FirstName ?? "",
                UserLastName = projectVars.currentUser?.LastName ?? "",
                UserEmail = projectVars.currentUser?.Email ?? "",
                UserDisplayName = projectVars.currentUser?.DisplayName ?? ""
            };

        }

        /// <summary>
        /// Fetches content from the Taxxor Static Assets server and stores it in a location provided
        /// </summary>
        /// <param name="AssetsToRetrieve"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> FetchStaticAssets(Dictionary<string, string> AssetsToRetrieve)
        {
            var debugRoutine = false;
            var fetchSuccess = 0;
            var fetchFailed = 0;
            var logContent = new List<string>();

            // Calculate the URL of the referer that we need to add to the HTTP request
            var domainType = (siteType == "prod") ? "prod" : "prev";
            var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{domainType}']");
            var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
            var protocol = (nodeCurrentDomain.GetAttribute("ssl") == "true") ? "https" : "http";
            var originUrl = $"{protocol}://{currentDomainName}";

            foreach (var asset in AssetsToRetrieve)
            {
                var uriSource = StaticAssetsLocation + asset.Key;
                var pathOsTarget = asset.Value;


                using (var httpClient = new HttpClient())
                {
                    try
                    {
                        httpClient.DefaultRequestHeaders.Add("Origin", originUrl);
                        // httpClient.DefaultRequestHeaders.Add("referer", originUrl);
                        httpClient.Timeout = TimeSpan.FromSeconds(5);

                        var staticAssetsContent = await httpClient.GetStringAsync(uriSource);

                        var sourceMd5 = md5(staticAssetsContent);
                        var targetMd5 = (File.Exists(pathOsTarget)) ? md5(File.ReadAllText(pathOsTarget)) : "";
                        if (!sourceMd5.Equals(targetMd5))
                        {
                            logContent.Add($"Updating {pathOsTarget} to match {uriSource}");
                            TextFileCreate(staticAssetsContent, pathOsTarget);
                        }
                        else
                        {
                            logContent.Add($"Skipping update of {pathOsTarget} since they are the same");
                        }

                        if (debugRoutine)
                        {
                            Console.WriteLine("------------------------------");
                            Console.WriteLine($"- uriSource: {uriSource}");
                            Console.WriteLine($"- pathOsTarget: {pathOsTarget}");
                            // Console.WriteLine("- staticAssetsContent:\n" + staticAssetsContent);
                            Console.WriteLine("------------------------------");
                        }

                        fetchSuccess++;

                    }
                    catch (HttpRequestException ex)
                    {
                        // Handle server unreachable or file not found
                        appLogger.LogWarning($"Could not retrieve taxonomy viewer page {asset.Key}: {ex.Message}");
                        fetchFailed++;
                    }
                    catch (TaskCanceledException ex)
                    {
                        // Handle timeout
                        appLogger.LogWarning($"Timeout while retrieving taxonomy viewer page {asset.Key}: {ex.Message}");
                        fetchFailed++;
                    }
                    catch (Exception ex)
                    {
                        // Handle any other unexpected errors
                        appLogger.LogError(ex, $"Error retrieving taxonomy viewer page {asset.Key}: {ex.Message}");
                        fetchFailed++;
                    }
                }
            }

            return new TaxxorReturnMessage(true, $"Fetched {fetchSuccess} successfully, {fetchFailed} failed", string.Join(", ", logContent.ToArray()));
        }

        /// <summary>
        /// Fetch a single file from the Taxxor Static Assets server and returns its content
        /// </summary>
        /// <param name="urlPath"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> FetchStaticAsset(string urlPath)
        {
            var debugRoutine = false;
            var fetchSuccess = 0;
            var fetchFailed = 0;
            var logContent = new List<string>();

            // Calculate the URL of the referer that we need to add to the HTTP request
            var domainType = (siteType == "prod") ? "prod" : "prev";
            var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{domainType}']");
            var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
            var protocol = (nodeCurrentDomain.GetAttribute("ssl") == "true") ? "https" : "http";
            var originUrl = $"{protocol}://{currentDomainName}";

            var staticAssetsContent = "";
            var uriSource = StaticAssetsLocation + urlPath;

            using (var httpClient = new HttpClient())
            {
                try
                {
                    httpClient.DefaultRequestHeaders.Add("Origin", originUrl);
                    // httpClient.DefaultRequestHeaders.Add("referer", originUrl);
                    httpClient.Timeout = TimeSpan.FromSeconds(5);

                    staticAssetsContent = await httpClient.GetStringAsync(uriSource);

                    if (debugRoutine)
                    {
                        Console.WriteLine("------------------------------");
                        Console.WriteLine($"- uriSource: {uriSource}");
                        Console.WriteLine("------------------------------");
                    }

                    fetchSuccess++;

                }
                catch (HttpRequestException ex)
                {
                    // Handle server unreachable or file not found
                    appLogger.LogWarning($"Could not retrieve static asset location {uriSource}: {ex.Message}");
                    fetchFailed++;
                }
                catch (TaskCanceledException ex)
                {
                    // Handle timeout
                    appLogger.LogWarning($"Timeout while retrieving static asset location {uriSource}: {ex.Message}");
                    fetchFailed++;
                }
                catch (Exception ex)
                {
                    // Handle any other unexpected errors
                    appLogger.LogError(ex, $"Error retrieving static asset location {uriSource}: {ex.Message}");
                    fetchFailed++;
                }
            }

            if (fetchFailed == 0)
            {
                return new TaxxorReturnMessage(true, $"Fetched {uriSource} successfully", staticAssetsContent, string.Join(", ", logContent.ToArray()));
            }
            else
            {
                return new TaxxorReturnMessage(false, $"Failed to fetch {uriSource}", string.Join(", ", logContent.ToArray()));
            }

        }

    }
}



/// <summary>
/// Extension methods unique for this project
/// </summary>
public static partial class TaxxorExtensionMethods
{
    /// <summary>
    /// Uses HtmlAgilityPack (http://html-agility-pack.net/) to transform plain HTML into XHTML and loads it into the XmlDocument
    /// </summary>
    /// <param name="xmlDoc"></param>
    /// <param name="html">The HTML that needs to be converted into XHTML</param>
    /// <param name="additionalReplacements">Restore CDATA tags before loading the result into the XML Document</param>
    public static void LoadHtml(this XmlDocument xmlDoc, string html, bool additionalReplacements = false)
    {
        var htmlPlain = HtmlAgilityPack.HtmlEntity.DeEntitize(html);


        // Framework.TextFileCreate(htmlPlain, $"{Framework.logRootPathOs}/--de-entitized.html");

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlPlain);

        htmlDoc.OptionOutputAsXml = true;

        using (StringWriter sw = new StringWriter())
        {
            using (XmlTextWriter xw = new XmlTextWriter(sw))
            {
                htmlDoc.Save(xw);

                var xhtml = sw.ToString();

                if (additionalReplacements)
                {
                    // Restore CDATA tags
                    xhtml = xhtml.Replace("&lt;![CDATA[", "<![CDATA[").Replace("]]&gt;", "]]>");

                }

                xmlDoc.LoadXml(xhtml);
                xw.Close();
            }
            sw.Close();
        }
    }
}