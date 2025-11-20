using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public abstract partial class Framework
{

    /// <summary>
    /// Singleton pattern for dynamically retrieving the project specific extensions to the framework
    /// </summary>
    /// <value>The extensions.</value>
    public static FrameworkExtensions Extensions
    {
        get { return _ExtensionsInstance; }
    }
    private static FrameworkExtensions _ExtensionsInstance = new ProjectExtensions();

    /// <summary>
    /// Generic options for the framework
    /// </summary>
    public class FrameworkOptions
    {
        public bool DebugMode { get; set; }
        public bool RequestVariablesDebugOutput { get; set; }
        public string ProjectType { get; set; }
        public FrameworkOptions()
        {
            this.DebugMode = false;
            this.RequestVariablesDebugOutput = true;
            this.ProjectType = "site";
        }
    }

    /// <summary>
    /// Stores the details about the hosting environment in a globally available field
    /// </summary>
    public static IHostEnvironment? hostingEnvironment { get; set; }

    /// <summary>
    /// Generic logger factory that can be re-used throughout the application
    /// </summary>
    /// <value>The app logger factory.</value>
    public static ILoggerFactory? appLoggerFactory { get; set; }

    /// <summary>
    /// Generic logger that can be re-used throughout the application
    /// </summary>
    /// <value>The app logger.</value>
    public static ILogger appLogger { get; set; } = null!;

    /// <summary>
    /// The type of the site (local | dev | prev | prod).
    /// </summary>
    public static string? siteType;

    // TODO: In a multilingual environment, we need to compile the application confugurations for all the locales and store them in a dictionary
    public static IDictionary<object, object>? applicationConfigurations;
    // TODO: This probably needs to move into the RequestVariables class because in a multi lingual environment, the application configuration to use is depending on the locale being processed
    public static XmlDocument? xmlApplicationConfiguration { get; set; }

    // TODO: In a multilingual environment, we need to compile the hierarchies for all the locales and store them in a dictionary
    public static IDictionary<object, object>? hierarchies;
    // TODO: This probably needs to move into the RequestVariables class because in a multi lingual environment, the hierarchy to use is depending on the locale being processed
    public static XmlDocument xmlHierarchy = new XmlDocument();


    /// <summary>
    /// Application memory cache
    /// </summary>
    public static IMemoryCache? memoryCache;

    //timer settings - for profiling scripts
    public static Stopwatch stopWatch = new Stopwatch();
    public static Stopwatch stopWatchProfiler = new Stopwatch();
    public static Stopwatch stopWatchProfilerTotal = new Stopwatch();
    public static bool useTimer = true;
    public static bool isProfilingMode = false;

    //used to determine site setup and logging
    public static bool isDevelopmentEnvironment = false;

    public static string? sitesRootPathOs;
    public static string? applicationRootPathOs;
    public static string? websiteRootPath;
    public static string? websiteRootPathOs;

    public static string? defaultDocument;

    public static string? baseConfigurationPathOs;
    public static string? smtpHost;

    public static string? applicationId;
    public static string applicationVersion = "";
    public static string applicationType = "webapp"; // webapp | webservice
    public static string? siteIdentifier;

    public static string? applicationConfigurationPathOs;
    public static string? hierarchyPathOs;

    public static string? configurationRootPath;
    public static string? xmlRootPath;
    public static string? dataRootPath;
    public static string? xslRootPath;
    public static string? hierarchyRootPath;
    public static string? logRootPath;

    public static string? configurationRootPathOs;
    public static string? xmlRootPathOs;
    public static string? dataRootPathOs;
    public static string? xslRootPathOs;
    public static string? hierarchyRootPathOs;
    public static string? logRootPathOs;

    /// <summary>
    /// Globally available regular expression for matching (active) file content
    /// </summary>
    public static Regex contentFilePath = new Regex(@"^.*((\.html|\.aspx)|\/|([a-zA-Z_\-]){5,}|\/([^\.]){1,4})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string sessionConfigured = "";

    public static string keyRequestVariables = "reqvars";
    public static string keyProjectVariables = "projectvars";

    public static string siteStructureMemberTestMethod = "roles";

    public static bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool isMac = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static bool isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    #region Static webserver settings
    public static bool serveStaticFiles = false;
    public static bool staticFilesRequireAuthentication = true;
    #endregion

    #region Debugging precision
    public static bool debugConsoleDumpRequestVariables = false;
    public static bool debugConsoleDumpProjectVariables = false;
    public static bool debugStoreInspectorLogFiles = false;
    public static bool debugRedirects = false;
    #endregion

    public static string? localIpAddress = null;

    public static int localPortNumber = -1;

    #region HTTP Response Headers
    public static bool RenderHttpHeaderAppVersion { get; set; }

    public static string ContentSecurityPolicyScriptSource { get; set; } = "";
    public static string ContentSecurityPolicyImageSource { get; set; } = "";
    public static string ContentSecurityPolicyStyleSource { get; set; } = "";
    public static string ContentSecurityPolicyFontSource { get; set; } = "";
    public static string ContentSecurityPolicyFrameSource { get; set; } = "";


    public static bool AppInitiated { get; set; } = false;

    #endregion

    public static bool TimeStartup { get; set; } = false;

    /// <summary>
    /// Placeholder function that can be utilized on "project level" to do some sort of translation of the site hierarchy
    /// </summary>
    /// <param name="xml">Site structure xml document</param>
    /// <returns>Translates the site structure document</returns>
    public static XmlDocument XslTranslateHierarchy(XmlDocument xml)
    {
        return xml;
    }

    /// <summary>
    /// Initiates the application by filling out the most important fields and compiling the application and hierarchy XML documents
    /// Overload function that parses information from the ApplicationBuilder object
    /// </summary>
    /// <param name="app"></param>
    /// <param name="env"></param>
    /// <param name="cache"></param>
    public static void InitApplicationLogic(IApplicationBuilder app, IHostEnvironment env, IMemoryCache cache, IConfiguration? config = null)
    {

        // Find application port number from the server addresses from the IServerAddressesFeature
        var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
        var serverAddresses = serverAddressesFeature?.Addresses.Distinct();
        foreach (var serverAddress in serverAddresses)
        {
            if (!int.TryParse(RegExpReplace(@"^.*:(\d+).*$", serverAddress, "$1"), out localPortNumber))
            {
                appLogger.LogWarning($"Could not determine port number from serverAddress: '{serverAddress}'");
            }
            else
            {
                appLogger.LogInformation($"Found port number: {localPortNumber}");
            }
        }

        InitApplicationLogic(env, cache, config);
    }

    /// <summary>
    /// Initiates the application by filling out the most important fields and compiling the application and hierarchy XML documents
    /// </summary>
    /// <param name="env">Hosting Environment object</param>
    /// <param name="cache"></param>
    public static void InitApplicationLogic(IHostEnvironment env, IMemoryCache cache, IConfiguration? config = null)
    {
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (TimeStartup)
        {
            Console.WriteLine(" -> Starting InitApplicationLogic performance measurement");
            stopWatch.Restart();
        }

        // Store the hosting environment object as a global field in the ApplicationLogic static class so that we can access it everywhere
        hostingEnvironment = env;

        // Store the memory cache variable as a global field so that we can access it everywhere
        memoryCache = cache;

        bool isDebugMode = env.EnvironmentName == "Development";
        applicationRootPathOs = env.ContentRootPath;

        if (TimeStartup)
        {
            Console.WriteLine($" -> Initial setup: {stopWatch.ElapsedMilliseconds}ms");
            stopWatch.Restart();
        }

        // The directory just "above" the current project directory or in case of a "new style" .NET project the path above the solution directory
        sitesRootPathOs = Path.GetDirectoryName(applicationRootPathOs);
        if (Path.GetFileName(sitesRootPathOs) == Path.GetFileName(applicationRootPathOs)) sitesRootPathOs = Path.GetDirectoryName(sitesRootPathOs);

        var logger = appLoggerFactory.CreateLogger("InitApplicationLogic()");

        if (TimeStartup)
        {
            Console.WriteLine($" -> Path setup: {stopWatch.ElapsedMilliseconds}ms");
            stopWatch.Restart();
        }

        #region Init Application Configuration
        // The only hard coded path to a configuration file
        baseConfigurationPathOs = applicationRootPathOs + @"/config/base_configuration.xml";

        // Application configuration defaults to base configuration and will be extended with the nodes contained in the other configuration documents (if required)
        xmlApplicationConfiguration = new XmlDocument();
        xmlApplicationConfiguration.Load(baseConfigurationPathOs);

        // Get the site type
        siteType = RetrieveSiteType(env.EnvironmentName, xmlApplicationConfiguration);

        // Get the starting values from base configuration
        RetrieveApplicationVariables(xmlApplicationConfiguration, true);

        if (TimeStartup)
        {
            Console.WriteLine($" -> Initial configuration setup: {stopWatch.ElapsedMilliseconds}ms");
            stopWatch.Restart();
        }

        //
        // => Wait until the Taxxor Document Store is present
        //
        if (applicationId != "documentstore")
        {
            var taxxorDataServiceAlive = false;
            var checks = 0;
            var testUri = CalculateFullPathOs("/configuration/configuration_system/config//location[@id = 'project_configuration']");
            while (!taxxorDataServiceAlive && checks < 12)
            {
                // Attempt to retrieve the services information from the Taxxor Document Store
                try
                {
                    var xmlTestResponse = new XmlDocument();
                    xmlTestResponse.Load(testUri);
                    taxxorDataServiceAlive = true;
                }
                catch (Exception)
                {
                    Console.WriteLine($"DEBUG: Waiting for the Taxxor Document Store to become available {checks} - {testUri}");
                }
                finally
                {
                    if (!taxxorDataServiceAlive)
                    {
                        Thread.Sleep(5000);
                    }

                    checks++;
                }
            }
        }

        if (TimeStartup)
        {
            Console.WriteLine($" -> Taxxor Document Store check: {stopWatch.ElapsedMilliseconds}ms");
            stopWatch.Restart();
        }

        // Generate application configuration by merging the several nested xml config files (base, project and site)
        xmlApplicationConfiguration = CompileApplicationConfiguration(xmlApplicationConfiguration, "en", isDebugMode);

        // Calculate the values again now that we have compiled a complete XML configuration
        RetrieveApplicationVariables(xmlApplicationConfiguration, true);

        // Write complete config to disk - used in the classic asp pages and for debugging purposes
        if (isDevelopmentEnvironment)
        {
            var applicationConfigCachePathOs = CalculateFullPathOs("xml_application_configuration_cache");
            if (applicationConfigCachePathOs != null)
            {
                try
                {
                    TextFileCreate(PrettyPrintXml(xmlApplicationConfiguration.OuterXml), applicationConfigCachePathOs, isDebugMode);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Could not store application configuration cache");
                }
            }
        }

        if (TimeStartup)
        {
            Console.WriteLine($" -> Application configuration compilation: {stopWatch.ElapsedMilliseconds}ms");
            stopWatch.Restart();
        }
        #endregion

        #region Init Site Structure Hierarchy
        // Compile a hierarchy
        xmlHierarchy = CompileHierarchy();

        // Optionally apply an xslt translation to the document
        xmlHierarchy = XslTranslateHierarchy(xmlHierarchy);

        // Determine the method for testing if a user has access to a page in the site structure
        siteStructureMemberTestMethod = RetrieveAttributeValueIfExists("/items/@membertest", xmlHierarchy) ?? "roles";

        if (TimeStartup)
        {
            Console.WriteLine($" -> Site structure hierarchy setup: {stopWatch.ElapsedMilliseconds}ms");
            stopWatch.Restart();
        }
        #endregion

        // Retrieve local IP address
        localIpAddress = GetLocalIpAddress();

        // Retrieve port number that this web application is using if there is no specific port number found yet
        if (localPortNumber == -1 || localPortNumber == 8080)
        {
            var rawInputContainingPort = "";

            // 1) - Attempt to retrieve domain name information from appsettings.json in the kestrel configuration
            if (config != null)
            {
                rawInputContainingPort = config.GetValue<string>("Kestrel:Endpoints:Service:Url");
            }
            else
            {
                Console.WriteLine("WARNING: Config not available");
            }

            // 2) Attempt to retrieve port number from the environment variables
            if (rawInputContainingPort == "")
            {
                Console.WriteLine("INFO: Kestrel:Endpoints:Service:Url not found in appsettings.json or environment variables");

                string[] environmentVariables = ["ASPNETCORE_URLS", "URLS"];

                foreach (string variable in environmentVariables)
                {
                    rawInputContainingPort = Environment.GetEnvironmentVariable(variable);

                    if (!string.IsNullOrEmpty(rawInputContainingPort))
                    {
                        if (int.TryParse(RegExpReplace(@"^.*:(\d+).*$", rawInputContainingPort, "$1"), out localPortNumber))
                        {
                            break;
                        }
                    }
                }
            }

            if (localPortNumber == -1 || rawInputContainingPort == "")
            {
                appLogger.LogWarning("Could not determine port number from any of the environment variables");
            }

            if (!int.TryParse(RegExpReplace(@"^.*:(\d+).*$", rawInputContainingPort, "$1"), out localPortNumber))
            {
                appLogger.LogWarning($"Could not determine port number from rawInputContainingPort: '{rawInputContainingPort}'");
            }
        }

        var localPortNumberDebug = localPortNumber.ToString();

        if (TimeStartup)
        {
            Console.WriteLine($" -> IP and port setup: {stopWatch.ElapsedMilliseconds}ms");
            stopWatch.Restart();
        }

        // Settings for headers
        RenderHttpHeaderAppVersion = true;

        // Content Security Policies
        if (applicationId == "taxxoreditor")
        {
            // Expand the list with custom values
            XmlNode? nodeScriptDomains = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/response_headers/security-policy/script-src");
            if (nodeScriptDomains != null && nodeScriptDomains.InnerText.Trim() != "") ContentSecurityPolicyScriptSource = ContentSecurityPolicyScriptSource + " " + nodeScriptDomains.InnerText;
            XmlNode? nodeImageDomains = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/response_headers/security-policy/image-src");
            if (nodeImageDomains != null && nodeImageDomains.InnerText.Trim() != "") ContentSecurityPolicyImageSource = ContentSecurityPolicyImageSource + " " + nodeImageDomains.InnerText;
            XmlNode? nodeStyleDomains = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/response_headers/security-policy/style-src");
            if (nodeStyleDomains != null && nodeStyleDomains.InnerText.Trim() != "") ContentSecurityPolicyStyleSource = ContentSecurityPolicyStyleSource + " " + nodeStyleDomains.InnerText;
            XmlNode? nodeFontDomains = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/response_headers/security-policy/font-src");
            if (nodeFontDomains != null && nodeFontDomains.InnerText.Trim() != "") ContentSecurityPolicyFontSource = ContentSecurityPolicyFontSource + " " + nodeFontDomains.InnerText;
            XmlNode? nodeFrameDomains = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/response_headers/security-policy/frame-src");
            if (nodeFrameDomains != null && nodeFrameDomains.InnerText.Trim() != "") ContentSecurityPolicyFrameSource = ContentSecurityPolicyFrameSource + " " + nodeFrameDomains.InnerText;
        }

        if (TimeStartup)
        {
            Console.WriteLine($" -> Header and security policy setup: {stopWatch.ElapsedMilliseconds}ms");
            stopWatch.Restart();
        }

        // Dump information to the console for debugging purposes
        logger.LogInformation("-------- ApplicationLogic Fields --------");
        AddDebugVariable(() => applicationRootPathOs);
        AddDebugVariable(() => sitesRootPathOs);
        AddDebugVariable(() => websiteRootPathOs);
        AddDebugVariable(() => dataRootPath);
        AddDebugVariable(() => dataRootPathOs);
        AddDebugVariable(() => xmlRootPath);
        AddDebugVariable(() => xmlRootPathOs);
        AddDebugVariable(() => configurationRootPath);
        AddDebugVariable(() => configurationRootPathOs);
        AddDebugVariable(() => xslRootPath);
        AddDebugVariable(() => xslRootPathOs);
        AddDebugVariable(() => hierarchyRootPath);
        AddDebugVariable(() => hierarchyRootPathOs);
        AddDebugVariable(() => hierarchyPathOs);
        AddDebugVariable(() => logRootPath);
        AddDebugVariable(() => logRootPathOs);
        AddDebugVariable(() => smtpHost);
        AddDebugVariable(() => applicationId);
        AddDebugVariable(() => applicationType);
        AddDebugVariable(() => siteIdentifier);
        AddDebugVariable(() => siteType);
        AddDebugVariable(() => isDevelopmentEnvironment);
        AddDebugVariable(() => siteStructureMemberTestMethod);
        AddDebugVariable(() => isWindows);
        AddDebugVariable(() => isMac);
        AddDebugVariable(() => isLinux);
        AddDebugVariable(() => serveStaticFiles);
        AddDebugVariable(() => staticFilesRequireAuthentication);
        AddDebugVariable(() => localIpAddress);
        AddDebugVariable(() => localPortNumberDebug);
        // Debug fields
        AddDebugVariable(() => debugConsoleDumpProjectVariables);
        AddDebugVariable(() => debugConsoleDumpRequestVariables);
        AddDebugVariable(() => debugRedirects);
        AddDebugVariable(() => debugStoreInspectorLogFiles);

        Console.WriteLine("");

        if (TimeStartup)
        {
            totalStopwatch.Stop();
            Console.WriteLine($"=> Total time for InitApplicationLogic: {totalStopwatch.ElapsedMilliseconds}ms <=");
        }

    }

    /// <summary>
    /// Retrieve initial values from xmlBaseConfiguration
    /// </summary>
    /// <param name="xmlConfiguration"></param>
    /// <param name="extendedMode"></param>
    public static void RetrieveApplicationVariables(XmlDocument xmlConfiguration, bool extendedMode)
    {
        // Load variables that are defined in application configuration
        string[] diskLocationPaths = RetrieveFilePathsFromConfiguration(xmlConfiguration, "/configuration/general/locations/location[@id='dataroot']");

        // Check if the environment variable DATA_LOCATION is present
        var alternativeDataLocation = Environment.GetEnvironmentVariable("DATA_LOCATION");
        var applicationEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrEmpty(alternativeDataLocation) && !string.IsNullOrEmpty(applicationEnvironment) && applicationEnvironment.Equals("development", StringComparison.CurrentCultureIgnoreCase))
        {
            appLogger.LogInformation($"USING ALTERNATIVE DATA_LOCATION ({alternativeDataLocation})");
            dataRootPath = "/" + Path.GetFileName(alternativeDataLocation);
            dataRootPathOs = alternativeDataLocation;
            appLogger.LogInformation($"USING dataRootPath ({dataRootPath})");
            appLogger.LogInformation($"USING dataRootPathOs ({dataRootPathOs})");
        }
        else
        {
            dataRootPath = diskLocationPaths[0];
            dataRootPathOs = diskLocationPaths[1];
        }

        xmlRootPath = dataRootPath;
        xmlRootPathOs = dataRootPathOs;

        diskLocationPaths = RetrieveFilePathsFromConfiguration(xmlConfiguration, "/configuration/general/locations/location[@id='configroot']");
        configurationRootPath = diskLocationPaths[0];
        configurationRootPathOs = diskLocationPaths[1];

        diskLocationPaths = RetrieveFilePathsFromConfiguration(xmlConfiguration, "/configuration/general/locations/location[@id='xslroot']");
        xslRootPath = diskLocationPaths[0];
        xslRootPathOs = diskLocationPaths[1];

        diskLocationPaths = RetrieveFilePathsFromConfiguration(xmlConfiguration, "/configuration/general/locations/location[@id='hierarchyroot']");
        hierarchyRootPath = diskLocationPaths[0];
        hierarchyRootPathOs = diskLocationPaths[1];

        diskLocationPaths = RetrieveFilePathsFromConfiguration(xmlConfiguration, "/configuration/general/locations/location[@id='logroot']");
        logRootPath = diskLocationPaths[0];
        logRootPathOs = diskLocationPaths[1];

        diskLocationPaths = RetrieveFilePathsFromConfiguration(xmlConfiguration, "/configuration/general/locations/location[@id='webserverroot']");
        websiteRootPath = diskLocationPaths[0];
        websiteRootPathOs = diskLocationPaths[1];

        hierarchyPathOs = CalculateFullPathOs("/configuration/hierarchy_system/hierarchy/location");
        if (hierarchyPathOs == null) WriteErrorMessageToConsole("Hierarchy definition could not be located in the configuration.", "xmlApplicationConfiguration does not contain a definition for the location of the main hierarchy file");

        // Determine if this is a development environment
        var nodeDevServerSetup = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='development_environment']/domain[@type='" + siteType + "']");
        if (nodeDevServerSetup != null) isDevelopmentEnvironment = true;

        // JT: check if extended mode is still required if "project_configuration.xml" is not in use
        if (extendedMode)
        {
            defaultDocument = RetrieveNodeValueIfExists("/configuration/general/default_document", xmlApplicationConfiguration);

            smtpHost = RetrieveNodeValueIfExists("/configuration/general/smtp_host", xmlApplicationConfiguration);

            siteIdentifier = RetrieveNodeValueIfExists("/configuration/general/site_identifier", xmlApplicationConfiguration);
            applicationId = RetrieveNodeValueIfExists("/configuration/general/application_id", xmlApplicationConfiguration);

            if (!string.IsNullOrEmpty(applicationId))
            {
                if (xmlApplicationConfiguration.SelectNodes($"/configuration/taxxor/components/service/services/service[@id='{applicationId}']").Count > 0) applicationType = "webservice";
            }

            string? serveStaticFilesString = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='serve-static-files']", xmlApplicationConfiguration);
            if (!string.IsNullOrEmpty(serveStaticFilesString))
            {
                serveStaticFiles = serveStaticFilesString == "true";
            }

            string? staticFilesRequireAuthenticationString = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='static-files-require-authentication']", xmlApplicationConfiguration);
            if (!string.IsNullOrEmpty(staticFilesRequireAuthenticationString))
            {
                staticFilesRequireAuthentication = staticFilesRequireAuthenticationString != "false";
            }

            // Determine the debugging precision for Local and Development environments -> all other environments do not use these debugging features because of performance issues
            if (siteType == "local" || siteType == "dev")
            {
                string? debugValueFromConfig = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='console-dump-projectvariables']", xmlApplicationConfiguration);
                if (!string.IsNullOrEmpty(debugValueFromConfig))
                {
                    debugConsoleDumpProjectVariables = debugValueFromConfig == "true";
                }
                debugValueFromConfig = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='console-dump-requestvariables']", xmlApplicationConfiguration);
                if (!string.IsNullOrEmpty(debugValueFromConfig))
                {
                    debugConsoleDumpRequestVariables = debugValueFromConfig == "true";
                }
                debugValueFromConfig = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='store-inspector-files']", xmlApplicationConfiguration);
                if (!string.IsNullOrEmpty(debugValueFromConfig))
                {
                    debugStoreInspectorLogFiles = debugValueFromConfig == "true";
                }

                // Determine if we need to be able to see redirects that the application initiates
                debugValueFromConfig = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='debug-redirects']", xmlApplicationConfiguration);
                if (!string.IsNullOrEmpty(debugValueFromConfig))
                {
                    debugRedirects = debugValueFromConfig == "true";
                }
            }

            // Dumps the logdata to the disk if possible and needed
            // DumpLogData();
        }

    }

    /// <summary>
    /// Retrieves a file/folder path from the confuguration xml document and returns an array containing the normal path and the fully qualified OS path
    /// </summary>
    /// <param name="xmlConfiguration"></param>
    /// <param name="xPath"></param>
    /// <returns></returns>
    private static string[] RetrieveFilePathsFromConfiguration(XmlDocument xmlConfiguration, string xPath)
    {
        string[] returnArray = new string[2];

        var xmlNodeList = xmlConfiguration.SelectNodes(xPath);
        if (xmlNodeList.Count > 0)
        {
            // the url type path
            returnArray[0] = xmlNodeList.Item(0).InnerText;

            // the path on the disk
            returnArray[1] = CalculateFullPathOs(xmlNodeList.Item(0));
        }
        else
        {
            WriteErrorMessageToConsole("File definition could not be located in the configuration.", $"xPath: '{xPath}' did not return any results.");
        }

        return returnArray;
    }

    /// <summary>
    /// Sets up a static files web server
    /// </summary>
    /// <param name="app"></param>
    public void StaticServer(IApplicationBuilder app, IHostEnvironment env)
    {
        var provider = new FileExtensionContentTypeProvider();

        // Add new mappings
        provider.Mappings[".jpg2"] = MediaTypeNames.Image.Jpeg;
        provider.Mappings[".properties"] = MediaTypeNames.Application.Octet;

        // Default document serving
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = new PhysicalFileProvider(websiteRootPathOs)
        });

        // Serve static files
        app.UseStaticFiles(new StaticFileOptions
        {
            // Add HTTP headers
            OnPrepareResponse = staticFileResponseContext =>
                {
                    staticFileResponseContext.Context.Response.Headers.Append("Cache-Control", "public,max-age=600");

                    // Application ID version and ID
                    staticFileResponseContext.Context.Response.Headers.Append("X-Tx-AppId", applicationId);

                    // Application version
                    if (RenderHttpHeaderAppVersion && !string.IsNullOrEmpty(applicationVersion)) staticFileResponseContext.Context.Response.Headers.Append("X-Tx-AppVersion", Framework.applicationVersion);

                    // For development purposes
                    if (env.IsDevelopment())
                    {
                        staticFileResponseContext.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                    }
                },
            FileProvider = new PhysicalFileProvider(websiteRootPathOs ?? Path.Combine(Directory.GetCurrentDirectory(), @"frontend", @"public")),
            ContentTypeProvider = provider
        });

        // Directory browsing
        app.UseDirectoryBrowser();
    }

}