using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Generic utilities and tools for the framework
/// </summary>
public abstract partial class Framework
{
    /// <summary>
    /// Avoids expensive xpath lookups for file mime types
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <typeparam name="string"></typeparam>
    /// <returns></returns>
    private static ConcurrentDictionary<string, string> _MimeTypeCache { get; set; } = new ConcurrentDictionary<string, string>();


    /// <summary>
    /// Loads the site structure hierarchy document
    /// </summary>
    /// <param name="path">OS type path of the hierarchy file</param>
    /// <returns></returns>
    public static XmlDocument LoadHierarchy(string path)
    {
        var xmlDocument = new XmlDocument();
        if (File.Exists(hierarchyPathOs))
        {
            xmlDocument.Load(path);
        }
        else
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            HandleError(reqVars, "Hierarchy could not be located", $"path: '{path}' could not be found");
        }

        return xmlDocument;
    }

    /// <summary>
    /// Checks application_configuration to determine the type of site which is currently displayed (prod, test, dev, local)
    /// </summary>
    /// <param name="name"></param>
    /// <param name="xmlConfig"></param>
    /// <returns></returns>
    public static string RetrieveSiteType(string name, XmlDocument xmlConfig)
    {
        string? siteType = null;

        // Test if there is a specific override defined in the application configuration
        if (!string.IsNullOrEmpty(name))
        {
            var nodeCustomEnvironmentMapping = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/environment_mapping/environment[@name='{name}']");
            if (nodeCustomEnvironmentMapping != null) return nodeCustomEnvironmentMapping.InnerText;
        }

        // In case that we pass a dotnetcore environment name
        switch (name)
        {
            case "Development":
                if (!Taxxor.Project.ProjectLogic.IsRunningInDocker())
                {
                    siteType = "local";
                }
                else
                {
                    siteType = "dev";
                }

                break;

            case "Production":
                siteType = "prod";
                break;

            case "Test":
            case "Staging":
                siteType = "prev";
                break;
            default:
                // Assume that we have passed a domain name
                siteType = RetrieveNodeValueIfExists("/configuration/general/domains/domain[text()=" + GenerateEscapedXPathString(name) + "]/@type", xmlConfig);
                break;
        }

        return siteType ?? "prod";
    }

    /// <summary>
    /// Retrieves the url from the hierarchy xml file
    /// </summary>
    /// <param name="input">Page id or linkname</param>
    /// <param name="xmlHierarchy">The hierarchy file to query</param>
    /// <returns>Page id if found otherwise null</returns>
    public static string? RetrieveUrlFromHierarchy(string input, XmlDocument xmlHierarchy)
    {
        string? url = null;

        //attempt to treat the input string as an id
        url = RetrieveNodeValueIfExists("//item[@id=" + GenerateEscapedXPathString(input) + "]/web_page/path", xmlHierarchy);

        if (url == null)
        {
            //attempt to treat input as a linkname
            url = RetrieveNodeValueIfExists("//item/web_page[linkname=" + GenerateEscapedXPathString(input) + "]/path", xmlHierarchy);
        }

        return url;
    }

    /// <summary>
    /// Retrieves the id from the siste_structure based on the URL passed
    /// </summary>
    /// <param name="input"></param>
    /// <param name="xmlHierarchy"></param>
    /// <returns></returns>
    public static string? RetrieveIdFromHierarchy(RequestVariables requestVariables, string input, XmlDocument xmlHierarchy)
    {
        return RetrieveIdFromHierarchy(requestVariables, input, xmlHierarchy, false);
    }

    /// <summary>
    /// Retrieves the id from the site_structure based on the URL passed
    /// </summary>
    /// <param name="input"></param>
    /// <param name="xmlHierarchy"></param>
    /// <param name="setCurrentHierarchyNode">Set to true if you like to update the global variable that contains the node in the xmlHierarchy that represents the current page</param>
    /// <returns></returns>
    public static string? RetrieveIdFromHierarchy(RequestVariables requestVariables, string input, XmlDocument xmlHierarchy, bool setCurrentHierarchyNode)
    {
        string? id = null;

        //correct the url id needed
        if (input.EndsWith("/", StringComparison.CurrentCulture)) input += defaultDocument;

        if (string.IsNullOrEmpty(xmlHierarchy.OuterXml)) return null;

        if (setCurrentHierarchyNode)
        {
            requestVariables.currentHierarchyNode = xmlHierarchy.SelectSingleNode("//item[web_page/path=" + GenerateEscapedXPathString(input) + "]");
            if (requestVariables.currentHierarchyNode != null)
            {
                id = GetAttribute(requestVariables.currentHierarchyNode, "id");
            }
        }
        else
        {
            id = RetrieveAttributeValueIfExists("//item[web_page/path=" + GenerateEscapedXPathString(input) + "]/@id", xmlHierarchy);
        }

        if (id == null)
        {
            //attempt to find the page by stripping off the querystring
            string? urlStripped = RegExpReplace(@"^(.*)(\?.*)$", input, "$1");
            if (urlStripped.EndsWith("/", StringComparison.CurrentCulture)) urlStripped += defaultDocument;

            if (setCurrentHierarchyNode)
            {
                requestVariables.currentHierarchyNode = xmlHierarchy.SelectSingleNode("//item[web_page/path=" + GenerateEscapedXPathString(urlStripped) + "]");
                if (requestVariables.currentHierarchyNode != null)
                {
                    id = GetAttribute(requestVariables.currentHierarchyNode, "id");
                }
            }
            else
            {
                id = RetrieveAttributeValueIfExists("//item[web_page/path=" + GenerateEscapedXPathString(urlStripped) + "]/@id", xmlHierarchy);
            }

        }

        if (id == null)
        {
            //in case the hierarchy contains query parameters attempt to match those against the path that was supplied
            string? urlStripped = RegExpReplace(@"^(.*)(\?.*)$", input, "$1");
            if (urlStripped.EndsWith("/", StringComparison.CurrentCulture)) urlStripped += defaultDocument;

            XmlNodeList? xmlNodeList = xmlHierarchy.SelectNodes("//item[contains(web_page/path, '?')]");
            foreach (XmlNode nodeHierarchy in xmlNodeList)
            {
                string? hierarchyUrlStripped = RegExpReplace(@"^(.*)(\?.*)$", nodeHierarchy.SelectSingleNode("web_page/path").InnerText, "$1");
                if (urlStripped == hierarchyUrlStripped)
                {
                    if (setCurrentHierarchyNode)
                    {
                        requestVariables.currentHierarchyNode = nodeHierarchy;
                        if (requestVariables.currentHierarchyNode != null)
                        {
                            id = GetAttribute(requestVariables.currentHierarchyNode, "id");
                        }
                    }
                    else
                    {
                        id = GetAttribute(nodeHierarchy, "id");
                    }

                }
            }
        }

        if (id == null)
        {
            // Attempt to match the uncoming URL by testing it against paths with a wildcard
            var nodeListWildcardItems = xmlHierarchy.SelectNodes("//item[contains(web_page/path, '/**/*')]");
            foreach (XmlNode nodeHierarchyItem in nodeListWildcardItems)
            {
                if (id == null)
                {
                    var baseUrlPath = nodeHierarchyItem.SelectSingleNode("web_page/path").InnerText.Replace("/**/*", "");
                    if (input.StartsWith(baseUrlPath))
                    {
                        if (setCurrentHierarchyNode)
                        {
                            requestVariables.currentHierarchyNode = nodeHierarchyItem;
                            if (requestVariables.currentHierarchyNode != null)
                            {
                                id = GetAttribute(requestVariables.currentHierarchyNode, "id");
                            }
                        }
                        else
                        {
                            id = GetAttribute(nodeHierarchyItem, "id");
                        }
                    }
                }
            }
        }

        return id;
    }

    /// <summary>
    /// Retrieves the page attribute from the hierarchy xml file
    /// </summary>
    /// <param name="attribute"></param>
    /// <returns></returns>
    public static string? RetrieveAttributeValueFromHierarchyIfExists(string pageId, string attribute)
    {
        return RetrieveAttributeValueIfExists("//item[@id='" + pageId + "']/@" + attribute, xmlHierarchy);
    }

    /// <summary>
    /// Retrieves value from Dictionary. If key is not found it returns null
    /// </summary>
    /// <param name="key"></param>
    /// <param name="dictionary"></param>
    /// <returns></returns>
    public static string? RetrieveValueFromDictionary(String key, IDictionary<String, String> dictionary)
    {
        if (dictionary.TryGetValue(key, out string? value))
        {
            return value;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a fully qualified OS type path based on a <location /> node in application configuration
    /// </summary>
    /// <param name="locator">Can contain xpath to a location, or just the id of the location (defaults to: /configuration/locations/location[@id])</param>
    /// <param name="requestVariables"></param>
    /// <returns></returns>
    public static string? CalculateFullPathOs(string locator, RequestVariables? requestVariables = null, object? objProjectVars = null)
    {
        string xpathForApplicationConfiguration;
        if (!locator.Contains('/'))
        {
            xpathForApplicationConfiguration = "/configuration/locations/location[@id='" + locator + "']";
        }
        else
        {
            xpathForApplicationConfiguration = locator;
        }
        var xmlNodeList = xmlApplicationConfiguration.SelectNodes(xpathForApplicationConfiguration);

        // Handle case not found
        if (xmlNodeList.Count == 0)
        {
            if (!locator.Contains('/'))
            {
                xpathForApplicationConfiguration = "//location[@id='" + locator + "']";
                xmlNodeList = xmlApplicationConfiguration.SelectNodes(xpathForApplicationConfiguration);
                if (xmlNodeList.Count == 0) return null;
            }
            else
            {
                return null;
            }
        }

        return CalculateFullPathOs(xmlNodeList, requestVariables);
    }

    /// <summary>
    /// Creates a fully qualified OS type path based on a <location /> node in application configuration
    /// </summary>
    /// <param name="xmlNodeList"></param>
    /// <param name="requestVariables"></param>
    /// <returns></returns>
    public static string? CalculateFullPathOs(XmlNodeList xmlNodeList, RequestVariables? requestVariables = null, object? objProjectVars = null)
    {
        if (xmlNodeList.Count > 0)
        {
            return CalculateFullPathOs(xmlNodeList.Item(0), requestVariables, objProjectVars);
        }
        else
        {
            return null;
        }
    }

    public static string? CalculateFullPathOs(XmlNode xmlNode, bool hideWarnings)
    {
        return CalculateFullPathOs(xmlNode, null, null, hideWarnings);
    }

    /// <summary>
    /// Creates a fully qualified OS type path based on a <location /> node in application configuration
    /// </summary>
    /// <param name="xmlNode"></param>
    /// <param name="requestVariables"></param>
    /// <returns></returns>
    public static string? CalculateFullPathOs(XmlNode xmlNode, RequestVariables? requestVariables = null, object? objProjectVars = null, bool hideWarnings = false)
    {
        string? path = null;
        string? relativeTo = null;

        if (xmlNode == null)
        {
            appLogger.LogWarning($"Could not calculate path because the XmlNode passed was empty. stack-trace: {GetStackTrace()}");
        }
        else
        {
            //test if this definition contains site type specific locations
            XmlNodeList? xmlNodeList = xmlNode.SelectNodes("domain[@type='" + siteType + "']");
            if (xmlNodeList.Count > 0)
            {
                var xmlNodeNested = xmlNodeList.Item(0);
                path = RetrieveNodeValueIfExists(".", xmlNodeNested);
                relativeTo = GetAttribute(xmlNodeNested, "path-type");
            }
            else
            {
                path = RetrieveNodeValueIfExists(".", xmlNode);
                relativeTo = GetAttribute(xmlNode, "path-type");
            }
        }

        //handle case not found
        if (path == null || relativeTo == null) return null;

        return CalculateFullPathOs(path, relativeTo, requestVariables, objProjectVars, hideWarnings);
    }

    /// <summary>
    /// Creates a fully qualified OS type path based on a <location /> node in application configuration
    /// </summary>
    /// <param name="path"></param>
    /// <param name="relativeTo"></param>
    /// <param name="requestVariables"></param>
    /// <returns></returns>
    public static string? CalculateFullPathOs(string? path, string relativeTo, RequestVariables? requestVariables = null, object? objProjectVars = null, bool hideWarnings = false)
    {
        HttpContext? context = null;
        string? exceptionDetails = null;

        try
        {
            context = System.Web.Context.Current;
        }
        catch (Exception ex)
        {
            if (requestVariables == null)
            {
                exceptionDetails = $"error details: {ex}";
            }
        }

        string? pathOs = relativeTo switch
        {
            "approot" => applicationRootPathOs + path,
            "configroot" => configurationRootPathOs + path,
            "dataroot" => dataRootPathOs + path,
            "webroot" => websiteRootPathOs + path,
            "xslroot" => xslRootPathOs + path,
            "hierarchyroot" => hierarchyRootPathOs + path,
            "logroot" => logRootPathOs + path,
            "sitesroot" => sitesRootPathOs + path,
            "http" => path,
            _ => null,
        };

        // We could not determine a path, so we will try to create a path via the project specific method
        if (pathOs == null)
        {
            if (!string.IsNullOrEmpty(exceptionDetails))
            {
                // appLogger.LogWarning($"In CalculateFullPathOs('{path}', '{relativeTo}') Exception - exceptionDetails: {exceptionDetails}");
            }
            pathOs = Extensions.CalculateFullPathOsProject(context, path, relativeTo, objProjectVars, hideWarnings);
        }

        if (pathOs != null)
        {
            // Replace placeholders in paths
            pathOs = Extensions.CalculateFullPathOsProjectReplacements(context, pathOs, objProjectVars, hideWarnings);
        }

        return pathOs;
    }

    /// <summary>
    /// Changes the HTTP headers to prevent the page from being cached
    /// </summary>
    /// <param name="context">Context</param>
    public static void DisableCache(HttpContext context)
    {
        DisableCache(context.Response);
    }

    /// <summary>
    /// Changes the HTTP headers to prevent the page from being cached
    /// </summary>
    /// <param name="response">Response.</param>
    public static void DisableCache(HttpResponse response)
    {
        response.Headers.TryAdd("Cache-Control", "no-cache, no-store, must-revalidate");
        response.Headers.TryAdd("Pragma", "no-cache");
        response.Headers.TryAdd("Expires", "0");

        /*
        Response.Cache.SetCacheability(HttpCacheability.Public);
        Response.Cache.SetCacheability(HttpCacheability.ServerAndNoCache);
        
        DateTime dt = DateTime.Now.AddMinutes(30);
        Response.Cache.SetMaxAge(new TimeSpan(dt.Ticks - DateTime.Now.Ticks));  
        Response.Cache.SetExpires(DateTime.Now.AddMinutes(-1));

        */
    }

    /// <summary>
    /// Redirects the user to a new page (url)
    /// </summary>
    /// <param name="url">Web address to redirect to</param>
    /// <param name="forceDebugMode">Optionally force a debugmode</param>
    public static void RedirectToPage(string url, bool forceDebugMode = false)
    {
        // Grab the request variables
        RequestVariables reqVars;
        try
        {
            var context = System.Web.Context.Current;
            reqVars = RetrieveRequestVariables(context);
        }
        catch (Exception ex)
        {
            appLogger.LogInformation(ex, "Could not retrieve context");
            reqVars = new RequestVariables
            {
                returnType = ReturnTypeEnum.Txt
            };
        }

        // Throw the special error to stop any code from further processing and perform the redirection from the special exception handler middleware
        throw new RedirectException(url, (forceDebugMode || reqVars.isDebugMode));
        // throw new RedirectException(url);
    }

    /// <summary>
    /// Compiles the application configuration file by merging base, project and site configuration files into one. While doing so this function retrieves the site locale (if any)
    /// </summary>
    /// <param name="xmlApplicationConfiguration">Application configuration XmlDocument</param>
    /// <param name="siteLocale">Locale of the application configuration that we are setting up</param>
    /// <returns></returns>
    public static XmlDocument CompileApplicationConfiguration(XmlDocument xmlApplicationConfiguration, string siteLocale = "en", bool debugRoutine = false)
    {

        // Check if we can retrieve the project configuration XML file via HTTP or not
        var useHttpProjectConfigurationVersion = true;
        var projectConfigurationPathOs = "";
        var projectConfigurationUrl = "";

        if (applicationId == "documentstore")
        {
            useHttpProjectConfigurationVersion = false;
        }
        else
        {
            var projectConfigurationLocationNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='xml_project_configuration_failover']");
            if (projectConfigurationLocationNode != null)
            {
                projectConfigurationPathOs = CalculateFullPathOs(projectConfigurationLocationNode);
                // Console.WriteLine($"- projectConfigurationPathOs: {projectConfigurationPathOs}");
                projectConfigurationUrl = CalculateFullPathOs("/configuration/configuration_system/config//location[@id = 'project_configuration']");
                // Console.WriteLine($"- projectConfigurationUrl: {projectConfigurationUrl}");

                try
                {
                    var xmlProjectConfiguration = new XmlDocument();
                    xmlProjectConfiguration.Load(projectConfigurationUrl);
                    useHttpProjectConfigurationVersion = true;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"There was an error retrieving the project configuration failover file from {projectConfigurationUrl}");
                    useHttpProjectConfigurationVersion = false;
                }
            }
        }



        // Check if we can retrieve the data configuration XML file via HTTP or not
        var useHttpDataConfigurationVersion = true;
        var dataConfigurationPathOs = "";
        if (applicationId == "documentstore")
        {
            useHttpDataConfigurationVersion = true;
        }
        else
        {
            var dataConfigurationLocationNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='xml_data_configuration_failover']");
            if (dataConfigurationLocationNode != null)
            {
                dataConfigurationPathOs = CalculateFullPathOs(dataConfigurationLocationNode);
                string? dataConfigurationUrl = CalculateFullPathOs("/configuration/configuration_system/config//location[@id='data_configuration']");

                try
                {
                    var xmlDataConfiguration = new XmlDocument();
                    xmlDataConfiguration.Load(dataConfigurationUrl);
                    useHttpDataConfigurationVersion = true;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"There was an error retrieving the data configuration file from {dataConfigurationUrl}");
                    useHttpDataConfigurationVersion = false;
                }
            }
        }



        var xmlNodeList = xmlApplicationConfiguration.SelectNodes("/configuration/configuration_system/config/config");
        if (xmlNodeList.Count > 0)
        {
            bool projectConfigurationDefined = false;
            if (xmlNodeList.Item(0).SelectNodes("//location[@id='project_configuration']").Count > 0) projectConfigurationDefined = true;

            // Nested configuration is available
            var mergeMode = xmlNodeList.Item(0).Attributes["mode"].Value;
            if (mergeMode == "merge")
            {
                //start merging the xml documents
                var xslPath = CalculateFullPathOs("/configuration/locations/location[@id='xsl_merge']");

                var xsltArgumentList = new XsltArgumentList();


                if (projectConfigurationDefined)
                {
                    // Calculate the path for retrieving the project_configuration.xml data
                    var projectConfigurationUri = (useHttpProjectConfigurationVersion) ? projectConfigurationUrl : projectConfigurationPathOs;
                    if (applicationId == "documentstore") projectConfigurationUri = CalculateFullPathOs(xmlNodeList.Item(0).FirstChild);

                    try
                    {

                        xsltArgumentList.AddParam("normalize", "", "yes");
                        xsltArgumentList.AddParam("replace", "", "true()");
                        xsltArgumentList.AddParam("with", "", $"{projectConfigurationUri}");
                        xmlApplicationConfiguration = TransformXmlToDocument(xmlApplicationConfiguration, xslPath, xsltArgumentList);
                    }
                    catch (Exception ex)
                    {
                        // throw new Exception($"Project configuration file could not be located from location {projectConfigurationPathOs}", ex);
                        // Configuration file could not be located - log the issue in the console
                        WriteErrorMessageToConsole("!!!! Project configuration file could not be retrieved from the Taxxor Document Store !!!!\nContinue with failover routine", $"Location: {projectConfigurationUri}, error: {ex}");

                    }
                }

                // Fill the global locale variable
                RetrieveSiteLocale(null, xmlApplicationConfiguration);

                // Find the nested configuration files
                var xPath = "/configuration/configuration_system/config/config/config[@locale='*' or @locale='" + siteLocale + "']";
                if (!projectConfigurationDefined) xPath = "/configuration/configuration_system/config/config[@locale='*' or @locale='" + siteLocale + "']";
                var xmlNodeListNested = xmlApplicationConfiguration.SelectNodes(xPath);
                if (xmlNodeListNested.Count > 0)
                {
                    mergeMode = xmlNodeListNested.Item(0).Attributes["mode"].Value;
                    if (mergeMode == "merge")
                    {
                        XmlNodeList? xmlNodeListLocation = xmlNodeListNested.Item(0).SelectNodes("location");

                        foreach (XmlNode xmlNodeLocation in xmlNodeListLocation)
                        {
                            var nestedConfigurationUri = CalculateFullPathOs(xmlNodeLocation);
                            var locationId = GetAttribute(xmlNodeLocation, "id");
                            if (locationId == "data_configuration" && (useHttpDataConfigurationVersion == false) && (applicationId != "documentstore"))
                            {
                                nestedConfigurationUri = dataConfigurationPathOs;
                            }
                            try
                            {
                                xsltArgumentList.RemoveParam("with", "");
                                xsltArgumentList.AddParam("with", "", nestedConfigurationUri);
                                xmlApplicationConfiguration = TransformXmlToDocument(xmlApplicationConfiguration, xslPath, xsltArgumentList);
                            }
                            catch (Exception ex)
                            {
                                // Configuration file could not be located - log the issue in the console
                                WriteErrorMessageToConsole("!!!! Nested configuration file could not be located !!!!", $"Location: {nestedConfigurationUri}, id: {locationId}, Error: {ex}");
                            }
                        }
                    }
                }

            }
            else
            {
                //other merge modes need to be placed here (overwrite??)
            }
        }
        else
        {
            //no nested project configuration found
        }

        //resolve any x-includes if supplied
        xmlApplicationConfiguration = ResolveXIncludes(xmlApplicationConfiguration);

        if (debugRoutine)
        {
            var logFilePathOs = $"{logRootPathOs}/inspector/application-configuration.xml";
            var xmlAppConfigContent = xmlApplicationConfiguration.OuterXml;
            try
            {
                Console.WriteLine($"DEBUG: Writing the application configuration to '{logFilePathOs}'");
                xmlApplicationConfiguration.SaveAsync(logFilePathOs, true, true).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var applicationConfigurationContent = "";
                try
                {
                    applicationConfigurationContent = TruncateString(xmlAppConfigContent, 50);
                }
                catch (Exception) { }

                Console.WriteLine($"ERROR: Could not store application configuration in '{logFilePathOs}' - content: {applicationConfigurationContent}, error: {ex}");
            }
        }

        return xmlApplicationConfiguration;
    }

    /// <summary>
    /// Generates a language unique version of site_structure.xml by merging the base hierarchy with the language specific one.
    /// </summary>
    /// <returns></returns>
    public static XmlDocument CompileHierarchy(string siteLocale = "global")
    {
        XmlDocument xmlCompiledHierarchy = new XmlDocument();

        var xmlNodeList = xmlApplicationConfiguration.SelectNodes("/configuration/hierarchy_system/hierarchy/location");
        if (xmlNodeList.Count > 0)
        {
            XmlNode? hierarchyLocationNode = xmlNodeList.Item(0);

            //load the base configuration
            var pathOs = CalculateFullPathOs(hierarchyLocationNode);

            if (pathOs != null)
            {
                if (File.Exists(pathOs))
                {
                    xmlCompiledHierarchy.Load(pathOs);

                    //find a nested hierarchy
                    var nodeListNested = hierarchyLocationNode.ParentNode.SelectNodes("hierarchy[@locale='" + siteLocale + "']");

                    foreach (XmlNode nodeNested in nodeListNested)
                    {
                        //nested configuration is available
                        var mergeMode = nodeNested.Attributes["mode"].Value;
                        if (mergeMode == "merge")
                        {
                            hierarchyLocationNode = nodeNested.FirstChild;
                            pathOs = CalculateFullPathOs(hierarchyLocationNode);

                            if (pathOs != null && File.Exists(pathOs))
                            {
                                //step 1: copy all the information from the nested hierarchy in existing elements from the parent hierarchy
                                var xslPath = CalculateFullPathOs("/configuration/locations/location[@id='xsl_merge_site_structure_1']");
                                var xsltArgumentList = new XsltArgumentList();
                                xsltArgumentList.AddParam("slavepath", "", pathOs);
                                xmlCompiledHierarchy = TransformXmlToDocument(xmlCompiledHierarchy, xslPath, xsltArgumentList);

                                //step 2: inject new elements from the nested site structure into the parent hierarchy
                                xslPath = CalculateFullPathOs("/configuration/locations/location[@id='xsl_merge_site_structure_2']");
                                xsltArgumentList = new XsltArgumentList();
                                xsltArgumentList.AddParam("slavepath", "", pathOs);
                                xmlCompiledHierarchy = TransformXmlToDocument(xmlCompiledHierarchy, xslPath, xsltArgumentList);
                            }

                        }

                    }
                }
                else
                {
                    WriteErrorMessageToConsole("Hierarchy file could not be located in the server.", $"The base hierarchy file could not be located on the server file system (pathOs: {pathOs})");
                }

            }
            else
            {
                WriteErrorMessageToConsole("Hierarchy file could not be located in the configuration.", "xmlApplicationConfiguration does not contain a definition for the location of the main hierarchy file");
            }
        }

        return xmlCompiledHierarchy;
    }

    /// <summary>
    /// Retrieves the client IP address
    /// </summary>
    /// <returns>IP address</returns>
    public static string? RetrieveClientIp(HttpContext context)
    {
        // TO DO: directly return value after "if" check; do not assign to clientIp variable multiple times
        var request = context.Request;
        string? clientIp = context.Connection.RemoteIpAddress?.ToString();

        string headerValue = request.RetrieveFirstHeaderValueOrDefault<string>("HTTP_CLIENT_IP");
        if (CheckIp(request, headerValue))
        {
            clientIp = headerValue;
        }
        else
        {
            headerValue = request.RetrieveFirstHeaderValueOrDefault<string>("HTTP_X_FORWARDED_FOR");
            if (!string.IsNullOrEmpty(headerValue))
            {
                string[] arrIps = headerValue.Split(new Char[] { ',' });
                foreach (string strIp in arrIps)
                {
                    if (!string.IsNullOrEmpty(clientIp))
                    {
                        clientIp = strIp;
                    }
                }
            }
            else
            {
                headerValue = request.RetrieveFirstHeaderValueOrDefault<string>("HTTP_X_FORWARDED");
                if (CheckIp(request, headerValue))
                {
                    clientIp = headerValue;
                }
                else
                {
                    headerValue = request.RetrieveFirstHeaderValueOrDefault<string>("HTTP_X_CLUSTER_CLIENT_IP");
                    if (CheckIp(request, headerValue))
                    {
                        clientIp = headerValue;
                    }
                    else
                    {
                        headerValue = request.RetrieveFirstHeaderValueOrDefault<string>("HTTP_FORWARDED");
                        if (CheckIp(request, headerValue))
                        {
                            clientIp = headerValue;
                        }
                        else
                        {
                            headerValue = request.RetrieveFirstHeaderValueOrDefault<string>("HTTP_VIA");
                            if (CheckIp(request, headerValue))
                            {
                                clientIp = headerValue;
                            }
                            else
                            {
                                headerValue = request.RetrieveFirstHeaderValueOrDefault<string>("REMOTE_ADDR");
                                if (CheckIp(request, headerValue))
                                {
                                    clientIp = headerValue;
                                }
                            }
                        }
                    }
                }
            }
        }

        return clientIp;
    }

    /// <summary>
    /// Tests if the IP address passed is outside the rage specified in the function
    /// </summary>
    /// <param name="strIp"></param>
    /// <returns></returns>
    public static bool CheckIp(HttpRequest Request, string strIp)
    {
        //Response.Write(strIp);
        if (!String.IsNullOrEmpty(strIp))
        {
            Int64 intIp = ConvertIpToLongInteger(strIp);
            String[] arrIpsMin = new String[]
            {
                "0.0.0.0",
                "10.0.0.0'",
                "127.0.0.0",
                "169.254.0.0",
                "172.16.0.0",
                "192.0.2.0",
                "192.168.0.0",
                "255.255.255.0"
            };
            String[] arrIpsMax = new String[]
            {
                "2.255.255.255",
                "10.255.255.255",
                "127.255.255.255",
                "169.254.255.255",
                "172.31.255.255",
                "192.0.2.255",
                "192.168.255.255",
                "255.255.255.255"
            };

            //remove a couple of elements if we are running on a non-production system (identified by being non-https)
            if (!Request.IsHttps)
            {
                //use linq to remove elements from the list
                arrIpsMin = arrIpsMin.Where(val => val != "192.168.0.0").ToArray();
                arrIpsMin = arrIpsMin.Where(val => val != "127.0.0.0").ToArray();

                arrIpsMax = arrIpsMax.Where(val => val != "192.168.255.255").ToArray();
                arrIpsMax = arrIpsMax.Where(val => val != "127.255.255.255").ToArray();
            }

            for (int i = 0; i < arrIpsMin.Length; i++)
            {
                String strIpsMin = arrIpsMax[i];
                String strIpsMax = arrIpsMax[i];

                Int64 intIpsMin = ConvertIpToLongInteger(strIpsMin);
                Int64 intIpsMax = ConvertIpToLongInteger(strIpsMax);

                if (intIp >= intIpsMin && intIp <= intIpsMax)
                {
                    return false;
                }
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts an IP address to a number so that you can calculate with it
    /// </summary>
    /// <param name="strIp"></param>
    /// <returns></returns>
    public static Int64 ConvertIpToLongInteger(String strIp)
    {
        Int64 longIp = 0;

        if (!String.IsNullOrEmpty(strIp))
        {
            String[] ips = strIp.Split(new Char[] { '.' });

            try
            {
                longIp = ((Int64.Parse(ips[3])) + (Int64.Parse(ips[2]) * 256) + (Int64.Parse(ips[1]) * 65536) + (Int64.Parse(ips[0]) * 16777216));
            }
            catch (Exception e)
            {
                appLogger.LogError(e, $"Exception occurred in ConvertIpToLongInteger('{strIp}')");
                return 0;
            }
        }

        return longIp;
    }

    /// <summary>
    /// Uses the URL supplied to retrieve the id from the hierarchy xml file
    /// </summary>
    /// <param name="requestVariables"></param>
    /// <param name="pageUrl"></param>
    /// <param name="objXmlHierarchy"></param>
    /// <returns></returns>
    public static string? RetrievePageId(RequestVariables requestVariables, string pageUrl, XmlDocument objXmlHierarchy)
    {
        //for backward compatibility with "old" page classes
        bool setCurrentHierarchyNode = false;
        if (requestVariables.currentHierarchyNode == null) setCurrentHierarchyNode = true;
        return RetrievePageId(requestVariables, pageUrl, objXmlHierarchy, ReturnTypeEnum.Html, setCurrentHierarchyNode);
    }

    /// <summary>
    /// Uses the URL supplied to retrieve the id from the hierarchy xml file
    /// </summary>
    /// <returns>The page identifier.</returns>
    /// <param name="requestVariables">Request variables.</param>
    /// <param name="pageUrl">Page URL.</param>
    /// <param name="objXmlHierarchy">Object xml hierarchy.</param>
    /// <param name="setCurrentHierarchyNode">If set to <c>true</c> set current hierarchy node.</param>
    public static string? RetrievePageId(RequestVariables requestVariables, string pageUrl, XmlDocument objXmlHierarchy, bool setCurrentHierarchyNode)
    {
        //for backward compatibility with "old" page classes
        return RetrievePageId(requestVariables, pageUrl, objXmlHierarchy, ReturnTypeEnum.Html, setCurrentHierarchyNode);
    }

    /// <summary>
    /// Retrieves the id of the page from the hierarchy
    /// </summary>
    /// <param name="requestVariables"></param>
    /// <param name="pageUrl"></param>
    /// <param name="objXmlHierarchy"></param>
    /// <param name="returnType"></param>
    /// <returns></returns>
    public static string? RetrievePageId(RequestVariables requestVariables, string pageUrl, XmlDocument objXmlHierarchy, ReturnTypeEnum returnType)
    {
        return RetrievePageId(requestVariables, pageUrl, objXmlHierarchy, returnType, false);
    }

    /// <summary>
    /// Retrieves the page id from the hierarchy.
    /// </summary>
    /// <returns>The page identifier.</returns>
    /// <param name="requestVariables">Request variables.</param>
    /// <param name="pageUrl">Page URL.</param>
    /// <param name="xmlHierarchyPassed">Xml hierarchy passed.</param>
    /// <param name="returnType">Return type.</param>
    /// <param name="setCurrentHierarchyNode">If set to <c>true</c> set current hierarchy node.</param>
    public static string? RetrievePageId(RequestVariables requestVariables, string pageUrl, XmlDocument xmlHierarchyPassed, ReturnTypeEnum returnType, bool setCurrentHierarchyNode)
    {
        //retrieve current page id
        string? pageId = RetrieveIdFromHierarchy(requestVariables, pageUrl, xmlHierarchyPassed, setCurrentHierarchyNode);

        if (string.IsNullOrEmpty(pageId))
        {

            //failover - in some cases we can get a pageId=null because the cache still needs to be updated

            //compile a new hierarchy, update the global hierarchy variable and store this one in the cache
            xmlHierarchy = CompileHierarchy(requestVariables.siteLocale);

            // optionally apply an xslt translation to the document
            xmlHierarchy = XslTranslateHierarchy(xmlHierarchy);

            //test if we can now find the page id
            pageId = RetrieveIdFromHierarchy(requestVariables, pageUrl, xmlHierarchy, setCurrentHierarchyNode);

            if (string.IsNullOrEmpty(pageId))
            {
                //render an error message, because there is no way we can find a hierarchy
                var errorMessage = "The page you requested could not be located in hierarchy. For security reasons it can not be displayed. Please call the system administrator if you would like the page you requested be part of the site's hierarchy.";
                string? debugInfo = null;
                if (requestVariables.isDebugMode || siteType == "local")
                {
                    debugInfo = $"- pageUrl: '{pageUrl}'<br/>{Environment.NewLine}";
                    debugInfo += "- xmlHierarchy: <pre>" + HtmlEncodeForDisplay(TruncateString(PrettyPrintXml(xmlHierarchyPassed.InnerXml), 400)) + "</pre>" + Environment.NewLine;
                }
                else
                {
                    appLogger.LogError($"pageUrl: '{pageUrl}' could not be located in the hierarchy");
                }

                // Potentially kick off custom logic
                Extensions.HandlePageIdNotFoundCustom(requestVariables, pageUrl, xmlHierarchyPassed);

                HandleError(requestVariables, errorMessage, debugInfo, 403);
            }
        }

        return pageId;
    }

    /// <summary>
    /// Uses the URL or Page ID supplied to retrieve the linkname from the hierarchy xml file
    /// </summary>
    /// <param name="identifier">A url or page id to search for in the hierarchy</param>
    /// <param name="objXmlHierarchy"></param>
    /// <returns></returns>
    public static string RetrieveLinkName(string identifier, XmlDocument objXmlHierarchy)
    {
        string? linkName;

        //current page id
        if (identifier.Contains('/'))
        {
            linkName = RetrieveNodeValueIfExists("//item[web_page/path=" + GenerateEscapedXPathString(identifier) + "]/web_page/linkname", objXmlHierarchy);
        }
        else
        {
            linkName = RetrieveNodeValueIfExists("//item[@id=" + GenerateEscapedXPathString(identifier) + "]/web_page/linkname", objXmlHierarchy);
        }

        if (linkName == null)
        {
            //attempt to find the page by stripping off the querystring
            var urlStripped = RegExpReplace(@"^(.*)(\?.*)$", identifier, "$1");
            linkName = RetrieveNodeValueIfExists("//item[web_page/path='" + urlStripped + "']/web_page/linkname", objXmlHierarchy);

            if (String.IsNullOrEmpty(linkName))
            {
                return String.Empty;
            }
        }

        return linkName;
    }

    /// <summary>
    /// Uses the pageId supplied to retrieve the path from the hierarchy xml file
    /// </summary>
    /// <param name="pageId"></param>
    /// <param name="objXmlHierarchy"></param>
    /// <returns></returns>
    /// 
    public static string? RetrievePageUrl(string pageId, XmlDocument objXmlHierarchy)
    {
        return RetrievePageUrl(pageId, objXmlHierarchy, ReturnTypeEnum.Html);
    }

    /// <summary>
    /// Uses the pageId supplied to retrieve the path from the hierarchy xml file
    /// </summary>
    /// <returns>The page URL.</returns>
    /// <param name="pageId">Page identifier.</param>
    /// <param name="objXmlHierarchy">Object xml hierarchy.</param>
    /// <param name="returnType">Return type.</param>
    public static string? RetrievePageUrl(string pageId, XmlDocument objXmlHierarchy, ReturnTypeEnum returnType)
    {
        string? pageUrl = RetrieveNodeValueIfExists("//item[@id='" + pageId + "']/web_page/path", objXmlHierarchy);

        if (string.IsNullOrEmpty(pageUrl))
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            string message = $"The page you requested (pageId: '{pageId}') could not be located in hierarchy<br/>For security reasons it can not be displayed. Please call the system administrator if you would like the page you requested be part of the site's hierarchy.";
            string? debugInfo = null;
            if (siteType == "local" || siteType == "dev")
            {
                debugInfo = $"pageId: {pageId}, stack-trace: {GetStackTrace()}";
            }

            // Potentially kick off custom logic
            Extensions.HandlePageIdNotFoundCustom(reqVars, pageUrl, objXmlHierarchy);

            HandleError(reqVars, message, debugInfo);
        }

        return pageUrl;
    }

    /// <summary>
    /// Creates the time based string
    /// </summary>
    /// <returns>The time based string.</returns>
    /// <param name="strType">"year", "month", "day", "hour", "minite", "second", "millisecond"</param>
    public static string CreateTimeBasedString(string? strType = null)
    {
        string year = DateTime.Now.Year.ToString();
        string month = DateTime.Now.Month.ToString();
        string day = DateTime.Now.Day.ToString();
        string hour = DateTime.Now.Hour.ToString();
        string minute = DateTime.Now.Minute.ToString();
        string second = DateTime.Now.Second.ToString();
        string millisecond = DateTime.Now.Millisecond.ToString();

        string? result = strType switch
        {
            "year" => year,
            "month" => year + month,
            "day" => year + month + day,
            "hour" => year + month + day + hour,
            "minute" => year + month + day + hour + minute,
            "second" => year + month + day + hour + minute + second,
            _ => year + month + day + hour + minute + second + millisecond,
        };
        return result;
    }

    /// <summary>
    /// Retrieves the content type of a file-type from the configuration file
    /// </summary>
    /// <param name="returnTypeEnum"></param>
    /// <returns></returns>
    public string GetContentType(ReturnTypeEnum returnTypeEnum)
    {
        return GetContentType(ReturnTypeEnumToString(returnTypeEnum));
    }

    /// <summary>
    /// Retrieves the content type of a file-type from the configuration file
    /// </summary>
    /// <param name="key">file extenstion or a filepath</param>
    /// <returns></returns>
    public static string GetContentType(string? key)
    {
        var mimeType = "text/plain";
        if (string.IsNullOrEmpty(key))
        {
            return mimeType;
        }
        else
        {
            // If we can find the mometype in the cache, then we return it here
            if (_MimeTypeCache.TryGetValue(key, out mimeType))
            {
                if (!string.IsNullOrEmpty(mimeType))
                {
                    // appLogger.LogInformation($"Found extensiom {key} in cache, returning {mimeType}");
                    return mimeType;
                }
            }

            if (Path.HasExtension(key))
            {
                key = Path.GetExtension(key).SubstringAfter(".");
            }
            else if (key.StartsWith('.'))
            {
                key = key.SubstringAfter(".");
            }

            if (_MimeTypeCache.TryGetValue(key, out mimeType))
            {
                if (!string.IsNullOrEmpty(mimeType))
                {
                    // appLogger.LogInformation($"Found extensiom {key} in cache, returning {mimeType}");
                    return mimeType;
                }
            }

            mimeType = RetrieveAttributeValueIfExists($"/configuration/file_definitions//file[@extension={GenerateEscapedXPathString(key)}]/@mime-type", xmlApplicationConfiguration);
            if (string.IsNullOrEmpty(mimeType))
            {
                mimeType = "text/plain";
            }

            _MimeTypeCache.TryAdd(key, mimeType);

            return mimeType;
        }
    }

    /// <summary>
    /// Retrieves the file type ("download", "image", "executable", "text")
    /// </summary>
    /// <param name="filePathOrMimeType"></param>
    /// <returns>"download", "image", "executable", "text"</returns>
    public static string? GetAssetType(string filePathOrMimeType)
    {
        switch (filePathOrMimeType)
        {
            case string when filePathOrMimeType.Length < 4 && !filePathOrMimeType.Contains('.'):
                {
                    // Assume a file path or file name is supplied
                    var nodeAssetType = xmlApplicationConfiguration.SelectSingleNode("/configuration/file_definitions//file[@extension='" + filePathOrMimeType + "']");
                    if (nodeAssetType != null)
                    {
                        return nodeAssetType.ParentNode.LocalName.TrimEnd('s');
                    }
                }
                break;

            case string when filePathOrMimeType.Contains('.'):
                {
                    // Assume a file path or file name is supplied
                    var nodeAssetType = xmlApplicationConfiguration.SelectSingleNode("/configuration/file_definitions//file[@extension='" + Path.GetExtension(filePathOrMimeType).TrimStart('.') + "']");
                    if (nodeAssetType != null)
                    {
                        return nodeAssetType.ParentNode.LocalName.TrimEnd('s');
                    }
                }
                break;
            case string when filePathOrMimeType.Contains('/'):
                {
                    // Assume a mime-type is supplied
                    var nodeAssetType = xmlApplicationConfiguration.SelectSingleNode("/configuration/file_definitions//file[@mime-type='" + filePathOrMimeType.ToLower() + "']");
                    if (nodeAssetType != null)
                    {
                        return nodeAssetType.ParentNode.LocalName.TrimEnd('s');
                    }
                }
                break;
        }

        return null;
    }

    /// <summary>
    /// Converts html characters like > or & to their HTML entity equivalent
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string ConvertToHtmlEntities(String input)
    {
        return HttpUtility.HtmlEncode(input);
    }

    /// <summary>
    /// Determines if a XML hierarchy document uses differentiation between "view" and "edit" rights
    /// </summary>
    /// <returns><c>true</c>, if the hierarchy uses "view" and "edit" rights, <c>false</c> otherwise.</returns>
    /// <param name="xmlHierarchy">XML hierarchy document.</param>
    public static bool HierarchyUsingViewEditAcl(XmlDocument xmlHierarchy)
    {
        return xmlHierarchy.SelectNodes("//access_control/view").Count > 0;
    }

    /// <summary>
    /// Retrieves the value that is used to indicate that everyone is allowed to access the resource in site structure
    /// </summary>
    /// <returns>The hierarchy all allowed value.</returns>
    public static string RetrieveHierarchyAllAllowedValue()
    {
        return (siteStructureMemberTestMethod == "roles") ? "allroles" : "all";
    }

    /// <summary>
    /// Retrieves a xml/html template from the configuration xml file
    /// </summary>
    /// <param name="templateId"></param>
    /// <returns></returns>
    public static XmlDocument? RetrieveTemplate(string templateId)
    {
        var nodeTemplate = xmlApplicationConfiguration.SelectSingleNode("/configuration/templates/template[@id='" + templateId + "']/*");
        if (nodeTemplate != null)
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.ReplaceContent(nodeTemplate);
            return xmlDocument;
        }

        return null;
    }

    /// <summary>
    /// Retrieves a user interface element from the configuration xml file
    /// </summary>
    /// <param name="elementId"></param>
    /// <param name="xmlConfig"></param>
    /// <returns></returns>
    public static string? RetrieveInterfaceElement(String elementId, XmlDocument xmlConfig)
    {
        return RetrieveInterfaceElement(elementId, xmlConfig, true);
    }

    /// <summary>
    /// Retrieves a user interface element from the configuration file
    /// </summary>
    /// <param name="elementId"></param>
    /// <param name="xmlConfig"></param>
    /// <param name="reqVars"></param>
    /// <returns></returns>
    public static string? RetrieveInterfaceElement(String elementId, XmlDocument xmlConfig, RequestVariables reqVars)
    {
        return RetrieveInterfaceElement(elementId, xmlConfig, true, false, reqVars);
    }

    /// <summary>
    /// Retrieves a user interface element from the configuration xml file
    /// </summary>
    /// <param name="elementId"></param>
    /// <param name="xmlConfig"></param>
    /// <param name="doLocalizationReplacement"></param>
    /// <returns></returns>
    public static string? RetrieveInterfaceElement(String elementId, XmlDocument xmlConfig, bool doLocalizationReplacement)
    {
        return RetrieveInterfaceElement(elementId, xmlConfig, doLocalizationReplacement, false);
    }

    /// <summary>
    /// Retrieves a user interface element from the configuration xml file and optionally resolves asset hashes to path (asset manager system)
    /// </summary>
    /// <param name="elementId"></param>
    /// <param name="xmlConfig"></param>
    /// <param name="doLocalizationReplacement"></param>
    /// <param name="performAssetManagerLookup"></param>
    /// <returns></returns>
    public static string? RetrieveInterfaceElement(string elementId, XmlDocument xmlConfig, bool doLocalizationReplacement, bool performAssetManagerLookup, RequestVariables? requestVariables = null)
    {
        var siteLanguage = "en";
        if (requestVariables == null)
        {
            appLogger.LogInformation("No RequestVariables object passed to RetrieveInterfaceElement, therefor we default to siteLanguage = 'en'");
        }
        else
        {
            siteLanguage = requestVariables.siteLanguage;
        }

        XmlNodeList? xmlNodeList = xmlConfig.SelectNodes("/configuration/page_elements/element[@id='" + elementId + "']");
        foreach (XmlNode xmlNode in xmlNodeList)
        {
            String? html;
            if (HasCDataContent(xmlNode))
            {
                html = xmlNode.InnerText;
            }
            else
            {
                if (performAssetManagerLookup)
                {
                    // TODO: Check if we still want to use this asset manager
                    // XmlNodeList nodeListImages = xmlNode.SelectNodes("//img");
                    // foreach (XmlNode node in nodeListImages) {
                    //     if (node.Attributes.Count > 0) {
                    //         string src = node.Attributes["src"].Value;
                    //         string newSrc = RetrieveUrlByHash(src, "images");
                    //         node.Attributes["src"].Value = newSrc;
                    //     }
                    // }
                }
                html = xmlNode.InnerXml;
            }

            // Replace localization text elements
            if (doLocalizationReplacement)
            {
                html = ReplaceLocalizedValuesInHtml(html);
            }
            html = html.Replace("[site_language]", siteLanguage);
            //html = html.Replace(Environment.NewLine, "");

            //check if we need to insert customizations
            var nodelistCustomizations = xmlConfig.SelectNodes("/configuration/customizations/page_elements/element[@id='" + elementId + "']/placeholder");
            foreach (XmlNode nodePlaceholder in nodelistCustomizations)
            {
                var placeholder = GetAttribute(nodePlaceholder, "text");
                if (!string.IsNullOrEmpty(placeholder))
                {
                    html = html.Replace(placeholder, nodePlaceholder.InnerXml);
                }
            }
            if (html.Contains("[customization_"))
            {
                html = RegExpReplace(@"\[customization_.*?\]", html, "", false);
            }

            return html;
        }
        return string.Empty;
    }

    /// <summary>
    /// Renders a user interface element from configuration xml file
    /// mainly used on non dynamic pages (search, ask a question, etc.)
    /// </summary>
    /// <param name="elementId"></param>
    public static async Task RenderInterfaceElement(HttpResponse response, string elementId)
    {
        string? html = RetrieveInterfaceElement(elementId, xmlApplicationConfiguration);
        await response.WriteAsync(html);
    }

    /// <summary>
    /// Calculates Week number
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static int GetWeekNumber(DateTime date)
    {
        CultureInfo cultureInfo = CultureInfo.CurrentCulture;
        int weekNumber = cultureInfo.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return weekNumber;
    }

    /// <summary>
    /// Converts BB style of coding to HTML
    /// </summary>
    /// <param name="strBBcodes"></param>
    /// <returns></returns>
    public static string ConvertBBCodesToHtml(String strBBcodes)
    {

        String strHtml = strBBcodes;

        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "br/]", "<br/>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "b]", "<strong>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "/b]", "</strong>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "i]", "<em>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "/i]", "</em>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "u]", "<u>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "/u]", "</u>");

        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "ol]", "<ol>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "/ol]", "</ol>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "ul]", "<ul>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "/ul]", "</ul>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "li]", "<li>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "/li]", "</li>");

        // MAIL
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "url=(mailto:[_a-z0-9-]+(" + Regex.Escape(".") + "[_a-z0-9-]+)*@[a-z0-9-]+(" + Regex.Escape(".") + "[a-z0-9-]+)*(" + Regex.Escape(".") + "[a-z]{2,3}))](.*?)" + Regex.Escape("[") + "/url]", "<a href='$1'>$5</a>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "url](mailto:[_a-z0-9-]+(" + Regex.Escape(".") + "[_a-z0-9-]+)*@[a-z0-9-]+(" + Regex.Escape(".") + "[a-z0-9-]+)*(" + Regex.Escape(".") + "[a-z]{2,3}))" + Regex.Escape("[") + "/url]", "<a href='$1'>$1</a>");

        // URLS
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "url=((https?)://([a-z0-9+$_-]+" + Regex.Escape(".") + ")*[a-z0-9+$_-]{2,3}(/([a-z0-9+$_-]" + Regex.Escape(".") + "?)+)*/?(" + Regex.Escape("?") + "[a-z+&$_.-][a-z0-9;:@/&%=+$_.-]*)?(#[a-z_.-][a-z0-9+$_.-]*)?)](.*?)" + Regex.Escape("[") + "/url]", "<a href='$1'>$8</a>");
        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "url]((https?)://([a-z0-9+$_-]+" + Regex.Escape(".") + ")*[a-z0-9+$_-]{2,3}(/([a-z0-9+$_-]" + Regex.Escape(".") + "?)+)*/?(" + Regex.Escape("?") + "[a-z+&$_.-][a-z0-9;:@/&%=+$_.-]*)?(#[a-z_.-][a-z0-9+$_.-]*)?)" + Regex.Escape("[") + "/url]", "<a href='$1'>$1</a>");

        strHtml = Regex.Replace(strHtml, Regex.Escape("[") + "(.*?)]", "");

        return strHtml;
    }

    /// <summary>
    /// Normalizes a passed integer to "seconds" format if the value is too high
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public static int NormalizeToSeconds(int timeout)
    {
        int timeoutInSeconds = timeout;
        //some legacy calls may pass miliseconds - in that case normalize to seconds
        if (timeout > 500)
        {
            timeoutInSeconds = (int)Math.Round((timeout / (float)1000), 0);
        }
        return timeoutInSeconds;
    }

    /// <summary>
    /// Normalizes a passed integer to "milliseconds" format if the value is too high
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public static int NormalizeToMilliseconds(int timeout)
    {
        int timeoutInMilliseconds = timeout;

        if (timeout <= 120)
        {
            timeoutInMilliseconds = timeout * 1000;
        }

        return timeoutInMilliseconds;
    }

    /// <summary>
    /// Uses async/await to pause the execution of the script for a few miliseconds without blocking the thread.
    /// </summary>
    /// <returns>The execution.</returns>
    /// <param name="delayInMilliseconds">Delay in milliseconds.</param>
    public async static Task PauseExecution(int delayInMilliseconds)
    {
        await Task.Delay(delayInMilliseconds);
    }

    /// <summary>
    /// Helps to prevent a compiler warning when you have defined an async routine without any calls to another async function in it.
    /// Use like this "await DummyAwaiter();"
    /// </summary>
    /// <returns>The awaiter.</returns>
    public static Task DummyAwaiter()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Converts a "Generic Type Parameter" that you would typically pass to a "Method<T>()" into a normalized string that can be used to return the correct type of object 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>The type to normalized string.</returns>
    public static string GenericTypeToNormalizedString(object obj)
    {
        switch (obj)
        {
            case XmlDocument:
                return "xml";

            case string:
                return "string";

            case bool:
                return "boolean";

            case Dictionary<string, string>:
                return "dictionary<string,string>";

            case Dictionary<string, int>:
                return "dictionary<string,int>";

            case Byte[]:
                return "bytearray";

            default:
                return "unknown";
        }
    }

    /// <summary>
    /// Converts a "Generic Type Parameter" that you would typically pass to a "Method<T>()" into a normalized string that can be used to return the correct type of object 
    /// </summary>
    /// <returns>The type to normalized string.</returns>
    /// <param name="type">Type.</param>
    public static string GenericTypeToNormalizedString(string type)
    {
        switch (type)
        {
            case "System.Xml.XmlDocument":
                return "xml";

            case "System.Boolean":
                return "boolean";

            case "System.String":
                return "string";

            case "System.Collections.Generic.Dictionary`2[System.String,System.Int32]":
                return "dictionary<string,int>";

            case "System.Collections.Generic.Dictionary`2[System.String,System.String]":
                return "dictionary<string,string>";

            case "System.Byte[]":
                return "bytearray";

            case "Framework+TaxxorReturnMessage":
                return "taxxorreturnmessage";

            default:
                return "unknown";
        }
    }

    /// <summary>
    /// Creates a custom HTTP Context object that can be used for cases where you need a context, but do not have one
    /// usage:
    /// var contextAccessor = CreateCustomHttpContext();
    /// context = contextAccessor.HttpContext;
    /// </summary>
    /// <param name="incomingRequestUrl"></param>
    /// <param name="host"></param>
    /// <returns></returns>
    public static IHttpContextAccessor CreateCustomHttpContext(string incomingRequestUrl = "/lorem.html", string host = "localhost")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = incomingRequestUrl;
        context.Request.Host = new HostString(host);

        //Do your thing here...

        var obj = new HttpContextAccessor();
        obj.HttpContext = context;
        return obj;
    }


    /// <summary>
    /// Retrieves the local IP address of the machine we are running on (might need improvement)
    /// </summary>
    /// <param name="logIssues"></param>
    /// <returns></returns>
    public static string GetLocalIpAddress(bool logIssues = false)
    {
        // Attempt 1 - retrieve the IP address by using the domain names
        var hostName = Dns.GetHostName();
        if (!string.IsNullOrEmpty(hostName))
        {
            try
            {
                IPAddress[] domainNameResult = Dns.GetHostEntry(hostName)?.AddressList ?? [];
                foreach (var ipAddressAll in domainNameResult)
                {
                    var ipAddress = ipAddressAll.MapToIPv4().ToString();
                    if (isReasonableIpAddress(ipAddress)) return ipAddressAll.MapToIPv4().ToString();
                }
            }
            catch (Exception ex)
            {
                appLogger.LogDebug($"Unable to retrieve the IP address by using the domain name {hostName}. Error: {ex.Message}");
            }
        }


        // Attempt 2 - try to find the IP address by looping through the network interfaces
        var nitVals = Enum.GetValues<NetworkInterfaceType>().Cast<NetworkInterfaceType>();
        foreach (var nitVal in nitVals)
        {
            if (nitVal.ToString().ToLower() == "ethernet")
            {
                var ipAddress = GetLocalIPv4(nitVal);
                if (isReasonableIpAddress(ipAddress)) return ipAddress;
            }
        }

        // Attempt 3 - old code but expensive code to fallback on
        UnicastIPAddressInformation? mostSuitableIp = null;

        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (var network in networkInterfaces)
        {
            if (network.OperationalStatus != OperationalStatus.Up)
                continue;

            var properties = network.GetIPProperties();

            if (properties.GatewayAddresses.Count == 0)
                continue;

            foreach (var address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (IPAddress.IsLoopback(address.Address))
                    continue;

                // TODO: This is very expensive....
                try
                {
                    if (OperatingSystem.IsWindows() && !address.IsDnsEligible)
                    {
                        mostSuitableIp ??= address;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    if (logIssues) Console.WriteLine($"WARNING: Could not obtain IP address. {ex}");
                }

                // The best IP is the IP got from DHCP server
                try
                {
                    if (OperatingSystem.IsWindows() && address.PrefixOrigin != PrefixOrigin.Dhcp)
                    {
                        if (mostSuitableIp == null || !mostSuitableIp.IsDnsEligible)
                            mostSuitableIp = address;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    if (logIssues) Console.WriteLine($"WARNING: Could not obtain IP address. {ex}");
                }

                return address.Address.ToString();
            }
        }

        return mostSuitableIp != null ?
            mostSuitableIp.Address.ToString() :
            "";

        /// <summary>
        /// Retrieves the IP v4 address from a network interface
        /// </summary>
        /// <param name="_type"></param>
        /// <returns></returns>
        string? GetLocalIPv4(NetworkInterfaceType _type)
        {
            string? output = null;
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }

        /// <summary>
        /// Checks if a the string passed a a sensible IP address
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        bool isReasonableIpAddress(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress)) return false;
            if (ipAddress.StartsWith("127.0.")) return false;
            return true;
        }
    }
}


public static class ReflectionExtensions
{
    /// <summary>
    /// Helper method for easiliy retrieving the name of the current method using reflection
    /// </summary>
    /// <param name="methodBase"></param>
    /// <param name="memberName"></param>
    /// <returns></returns>
    public static string GetDeclaringName(this MethodBase methodBase, [CallerMemberName] string memberName = "")
    {
        return memberName;
    }
}

public static class GeneralExtensions
{
    public static void AddRangeOverride<TKey, TValue>(this IDictionary<TKey, TValue> dic, IDictionary<TKey, TValue> dicToAdd)
    {
        dicToAdd.ForEach(x => dic[x.Key] = x.Value);
    }

    public static void AddRangeNewOnly<TKey, TValue>(this IDictionary<TKey, TValue> dic, IDictionary<TKey, TValue> dicToAdd)
    {
        dicToAdd.ForEach(x => { if (!dic.ContainsKey(x.Key)) dic.Add(x.Key, x.Value); });
    }

    public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dic, IDictionary<TKey, TValue> dicToAdd)
    {
        dicToAdd.ForEach(x => dic.Add(x.Key, x.Value));
    }

    public static bool ContainsKeys<TKey, TValue>(this IDictionary<TKey, TValue> dic, IEnumerable<TKey> keys)
    {
        bool result = false;
        keys.ForEachOrBreak((x) => { result = dic.ContainsKey(x); return result; });
        return result;
    }

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
            action(item);
    }

    public static void ForEachOrBreak<T>(this IEnumerable<T> source, Func<T, bool> func)
    {
        foreach (var item in source)
        {
            bool result = func(item);
            if (result) break;
        }
    }

}