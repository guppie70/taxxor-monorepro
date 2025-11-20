using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Taxxor.Models;

/// <summary>
/// Extends the Framework class with utilities and object that contain details about the connected services
/// These routines contain some hard coded paths and URI's, but this is acceptable since we share this file with all the other Taxxor applications
/// </summary>
public abstract partial class Framework
{

    /// <summary>
    /// Cache to avoid expensive xpath lookup for service URL's
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <typeparam name="string"></typeparam>
    /// <returns></returns>
    private static ConcurrentDictionary<string, string> _ConnectedServicesUriCache { get; set; } = new ConcurrentDictionary<string, string>();

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
    /// Returns ConnectedServiceEnum based on a connected service ID from the configuration
    /// </summary>
    /// <param name="connectedServiceId">string input (xml, json, html, txt)</param>
    /// <returns></returns>
    public static ConnectedServiceEnum GetConnectedServicesEnum(string connectedServiceId)
    {
        switch (connectedServiceId)
        {
            case "taxxoreditor":
                return ConnectedServiceEnum.Editor;
            case "documentstore":
                return ConnectedServiceEnum.DocumentStore;
            case "taxxoraccesscontrolservice":
                return ConnectedServiceEnum.AccessControlService;
            case "taxxorxbrlservice":
                return ConnectedServiceEnum.XbrlService;
            case "taxxorpdfservice":
                return ConnectedServiceEnum.PdfService;
            case "taxxorgenericdataconnector":
                return ConnectedServiceEnum.GenericDataConnectorService;
            case "taxxormappingservice":
                return ConnectedServiceEnum.MappingService;
            case "conversionservice":
                return ConnectedServiceEnum.ConversionService;
            case "edgararelleservice":
                return ConnectedServiceEnum.EdgarArelleService;
            case "taxxorassetcompiler":
                return ConnectedServiceEnum.AssetCompiler;
            case "taxxordatastore":
                return ConnectedServiceEnum.StructuredDataStore;
            case "staticassets":
                return ConnectedServiceEnum.StaticAssets;

            default:
                return ConnectedServiceEnum.Unknown;
        }
    }

    /// <summary>
    /// Converts a ConnectedServiceEnum value to an ID
    /// </summary>
    /// <param name="connectedServiceEnum"></param>
    /// <returns></returns>
    public static string ConnectedServicesEnumToString(ConnectedServiceEnum connectedServiceEnum)
    {
        switch (connectedServiceEnum)
        {
            case ConnectedServiceEnum.Editor:
                return "taxxoreditor";
            case ConnectedServiceEnum.DocumentStore:
                return "documentstore";
            case ConnectedServiceEnum.AccessControlService:
                return "taxxoraccesscontrolservice";
            case ConnectedServiceEnum.XbrlService:
                return "taxxorxbrlservice";
            case ConnectedServiceEnum.PdfService:
                return "taxxorpdfservice";
            case ConnectedServiceEnum.GenericDataConnectorService:
                return "taxxorgenericdataconnector";
            case ConnectedServiceEnum.MappingService:
                return "taxxormappingservice";
            case ConnectedServiceEnum.ConversionService:
                return "conversionservice";
            case ConnectedServiceEnum.EdgarArelleService:
                return "edgararelleservice";
            case ConnectedServiceEnum.AssetCompiler:
                return "taxxorassetcompiler";
            case ConnectedServiceEnum.StructuredDataStore:
                return "taxxordatastore";
            case ConnectedServiceEnum.StaticAssets:
                return "staticassets";

            default:
                return "unknown";
        }
    }


    /// <summary>
    /// Renders the URL for retrieving the Taxxor services information (containing the API definitions of all the Taxxor services)
    /// </summary>
    /// <param name="siteType"></param>
    /// <returns></returns>
    public static string RetrieveTaxxorServicesUri(string siteType)
    {
        return "https://documentstore:4813/api/configuration/taxxorservices";
    }


    /// <summary>
    /// Updates the taxxor services information in the application configuration in-memory object.
    /// To be used as a background task in the Taxxor connected slave services and first line applications
    /// </summary>
    /// <returns>The taxxor services information.</returns>
    /// <param name="siteType">Site type.</param>
    public async static Task UpdateTaxxorServicesInformation(string siteType)
    {
        try
        {
            var serviceDetailsUrl = "";
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Set the URL where we can retrieve XML file that contains the details of the services
            serviceDetailsUrl = RetrieveTaxxorServicesUri(siteType);

            if (string.IsNullOrEmpty(serviceDetailsUrl))
            {
                Console.WriteLine($"ERROR: Could not retrieve URL to retrieve the Taxxor Services Description. siteType: {siteType}");
            }
            else
            {
                // Check if a services node exists in the application configuration
                XmlNode? taxxorSystemInformationNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/taxxor");

                if (taxxorSystemInformationNode == null)
                {
                    taxxorSystemInformationNode = xmlApplicationConfiguration.CreateNode(XmlNodeType.Element, "taxxor", null);
                    xmlApplicationConfiguration.DocumentElement.AppendChild(taxxorSystemInformationNode);
                }

                XmlNode? taxxorComponentsNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/taxxor/components");
                if (taxxorComponentsNode == null)
                {
                    taxxorComponentsNode = xmlApplicationConfiguration.CreateNode(XmlNodeType.Element, "components", null);
                    taxxorSystemInformationNode.AppendChild(taxxorComponentsNode);
                }

                // Store in the log if needed and configured
                if (Taxxor.Project.ProjectLogic.UriLogEnabled)
                {
                    if (!Taxxor.Project.ProjectLogic.UriLogBackend.Contains(serviceDetailsUrl)) Taxxor.Project.ProjectLogic.UriLogBackend.Add(serviceDetailsUrl);
                }

                // Retrieve the services document from the master (data service) web service
                XmlDocument xmlServicesDetails = await RestRequest<XmlDocument>(RequestMethodEnum.Get, serviceDetailsUrl);

                // Path to failover document
                string? serviceInformationFailoverPathOs = null;
                var serviceInformationLocationNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='xml_service_information_failover']");
                if (serviceInformationLocationNode != null) serviceInformationFailoverPathOs = CalculateFullPathOs(serviceInformationLocationNode);

                if (!XmlContainsError(xmlServicesDetails))
                {
                    // Replace the current services information with the one that we have just received
                    ReplaceXmlNode(taxxorComponentsNode, xmlServicesDetails.DocumentElement);

                    try
                    {
                        await xmlServicesDetails.SaveAsync(serviceInformationFailoverPathOs);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"WARNING: Could not store service details. error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }
                else
                {
                    Console.WriteLine($"ERROR: Unable to update services information because the request to '{serviceDetailsUrl}' returned with an error. xmlServicesDetails: {xmlServicesDetails.OuterXml}, stack-trace: {CreateStackInfo()}");

                    var xmlFailoverServicesInformation = new XmlDocument();
                    try
                    {
                        xmlFailoverServicesInformation.Load(serviceInformationFailoverPathOs);

                        // Prepare it by removing the paths of the taxxor data service
                        var nodeTaxxorDataServiceInformation = xmlFailoverServicesInformation.SelectSingleNode("/components/service/services/service[@id='documentstore']");
                        if (nodeTaxxorDataServiceInformation != null)
                        {
                            // Set the service unavailable attribute
                            SetAttribute(nodeTaxxorDataServiceInformation, "status", HttpStatusCodeToMessage(503));

                            // Remove the routes from the Taxxor Document Store
                            // RemoveXmlNode(nodeTaxxorDataServiceInformation.SelectSingleNode("routes"));

                            // Replace the current services information with the failover information
                            ReplaceXmlNode(taxxorComponentsNode, xmlFailoverServicesInformation.DocumentElement);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"WARNING: Unable to load failover service information file. serviceInformationFailoverPathOs: {serviceInformationFailoverPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }

            }

            // Store the result of the routine on the disk so we can inspect it
            if (debugRoutine)
            {
                var logFilePathOs = $"{logRootPathOs}/inspector/application-configuration.xml";
                var xmlAppConfigContent = xmlApplicationConfiguration.OuterXml;
                try
                {
                    await xmlApplicationConfiguration.SaveAsync(logFilePathOs, true, true);
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: There was an error updating the Taxxor services information. error: {ex}, stack-trace: {GetStackTrace()}");
        }

    }

    /// <summary>
    /// Retrieves the full URL for a connected Taxxor webservice based on the method ID supplied
    /// </summary>
    /// <returns>The service URL by method identifier.</returns>
    /// <param name="connectedServiceEnum">Connected service enum.</param>
    /// <param name="methodId">Method identifier.</param>
    public static string? GetServiceUrlByMethodId(ConnectedServiceEnum connectedServiceEnum, string methodId)
    {
        var debugRoutine = (siteType == "local" || siteType == "dev");
        var baseUrl = "";
        string? serviceUrl = null;

        var serviceId = ConnectedServicesEnumToString(connectedServiceEnum);
        var cacheKey = $"{serviceId}___{methodId}";

        // If we can find the location in the cache, then we return it here
        if (_ConnectedServicesUriCache.TryGetValue(cacheKey, out serviceUrl))
        {
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                // if (debugRoutine) appLogger.LogInformation($"Found {cacheKey} in cache, returning {serviceUrl}");
                return serviceUrl;
            }
        }

        var xPathMainService = $"/configuration/taxxor/components/service[@id='{serviceId}']";
        var xPath = $"/configuration/taxxor/components/service//*[(local-name()='service' or local-name()='web-application') and @id='{serviceId}']";

        XmlNode? serviceNode = xmlApplicationConfiguration.SelectSingleNode(xPath);
        if (serviceNode == null)
        {
            serviceNode = xmlApplicationConfiguration.SelectSingleNode(xPathMainService);
        }

        if (serviceNode == null)
        {
            if (debugRoutine) xmlApplicationConfiguration.Save(logRootPathOs + "/---rbac-appconfig.xml");
            WriteErrorMessageToConsole($"Could not find information for service ID: {serviceId}", $"xPath: {xPath} and {xPathMainService} returned no results, stack-info: {CreateStackInfo()}");
            return null;
        }
        else
        {
            var nodeLocation = (serviceNode.SelectSingleNode("uri/domain") == null) ? serviceNode.SelectSingleNode("uri") : serviceNode.SelectSingleNode($"uri/domain[@type='{siteType}']");
            baseUrl = nodeLocation?.InnerText ?? "";

            if (string.IsNullOrEmpty(baseUrl))
            {
                WriteErrorMessageToConsole($"Could not find a base URL for service ID: {serviceId} and method ID: {methodId}", $"xPath: {xPath}, serviceNode: {serviceNode.OuterXml}, stack-info: {CreateStackInfo()}");
                return null;
            }
            else
            {
                xPath = $"routes//route[@id='{methodId}']/@uri";
                var path = RetrieveAttributeValueIfExists(xPath, serviceNode);
                if (string.IsNullOrEmpty(path))
                {
                    WriteErrorMessageToConsole($"Could not find a URL for service ID: {ConnectedServicesEnumToString(connectedServiceEnum)} and method ID: {methodId}", $"xPath: {xPath}, serviceNode: {serviceNode.OuterXml}, stack-info: {CreateStackInfo()}");
                    return null;
                }
                else
                {
                    if (!path.StartsWith('/')) path = $"/{path}";

                    serviceUrl = $"{baseUrl}{path}";

                    // Fill the cache
                    _ConnectedServicesUriCache.TryAdd(cacheKey, serviceUrl);

                    return serviceUrl;
                }
            }
        }
    }

    /// <summary>
    /// Retrieves the main domain name where a connected service can be reached
    /// </summary>
    /// <param name="connectedServiceEnum"></param>
    /// <returns></returns>
    public static string? GetServiceUrl(ConnectedServiceEnum connectedServiceEnum)
    {
        string? serviceUrl = null;
        var serviceId = ConnectedServicesEnumToString(connectedServiceEnum);


        // If we can find the location in the cache, then we return it here
        if (_ConnectedServicesUriCache.TryGetValue(serviceId, out serviceUrl))
        {
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                // appLogger.LogInformation($"Found serviceid {serviceId} in cache, returning {serviceUrl}");
                return serviceUrl;
            }
        }

        var xPathMainService = $"/configuration/taxxor/components/service[@id='{serviceId}']";
        var xPath = $"/configuration/taxxor/components/service//*[(local-name()='service' or local-name()='web-application') and @id='{serviceId}']";

        XmlNode? serviceNode = xmlApplicationConfiguration.SelectSingleNode(xPath);
        if (serviceNode == null)
        {
            serviceNode = xmlApplicationConfiguration.SelectSingleNode(xPathMainService);
        }

        if (serviceNode == null)
        {
            WriteErrorMessageToConsole($"Could not find information for service ID: {serviceId}", $"xPath: {xPath} and {xPathMainService} returned no results, stack-info: {CreateStackInfo()}");
            return null;
        }
        else
        {
            var nodeDomain = serviceNode.SelectSingleNode($"uri/domain[@type='{siteType}']");
            if (nodeDomain != null)
            {
                serviceUrl = nodeDomain.InnerText;
            }
            else
            {
                serviceUrl = serviceNode.SelectSingleNode("uri")?.InnerText ?? "";
            }

            if (string.IsNullOrEmpty(serviceUrl))
            {
                WriteErrorMessageToConsole($"Could not find a base URL for service ID: {serviceId}", $"xPath: {xPath} returned no results, stack-info: {CreateStackInfo()}");
                return null;
            }
            else
            {
                // Fill the cache
                _ConnectedServicesUriCache.TryAdd(serviceId, serviceUrl);

                return serviceUrl;
            }
        }
    }

    /// <summary>
    /// Renders the Taxxor service description XML document that displays information about the web service
    /// </summary>
    /// <returns>The service description in XML format.</returns>
    /// <param name="request">Request.</param>
    /// <param name="response">Response.</param>
    /// <param name="routeData">Route data.</param>
    // MIGRATED - CAN BE REMOVED
    public async static Task RenderServiceDescription(HttpRequest request, HttpResponse response, RouteData routeData)
    {
        // Access the current HTTP Context
        var context = System.Web.Context.Current;
        var reqVars = RetrieveRequestVariables(context);

        // Call the core logic method
        var result = await RenderServiceDescriptionCore();

        // Return the response
        if (result.Success)
        {
            await response.OK(result.ResponseContent, ReturnTypeEnum.Xml, true);
        }
        else
        {
            HandleError(reqVars, result.ErrorMessage, result.DebugInfo, result.StatusCode);
        }
    }

    /// <summary>
    /// Core logic for rendering service description without HTTP dependencies
    /// </summary>
    /// <returns>Service description result</returns>
    public async static Task<ServiceDescriptionResult> RenderServiceDescriptionCore()
    {
        var result = new ServiceDescriptionResult();

        try
        {
            // Get the service description
            result.XmlResponse = GenerateServiceDescription();
            result.ResponseContent = result.XmlResponse; // For backward compatibility
            result.Success = true;

            await Task.CompletedTask; // To satisfy the async requirement
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = "Error generating service description";
            result.DebugInfo = $"Error: {ex.Message}, Stack trace: {GetStackTrace()}";
            return result;
        }
    }

    /// <summary>
    /// Generates the Service Description XML document
    /// </summary>
    /// <returns></returns>
    public static XmlDocument GenerateServiceDescription()
    {
        // Clone the site structure XML object so that we can manipulate it
        XmlDocument xmlHierarchyCloned = new XmlDocument();
        xmlHierarchyCloned.LoadXml(xmlHierarchy.OuterXml);

        // Add the repository information to the cloned hierarchy
        var nodeRepositoryInfo = xmlApplicationConfiguration.SelectSingleNode("/configuration/repositories");
        var nodeRepositoryInfoImported = xmlHierarchyCloned.ImportNode(nodeRepositoryInfo, true);
        xmlHierarchyCloned.DocumentElement.AppendChild(nodeRepositoryInfoImported);

        // Add the filing composers which also originate from a GIT repository
        if (applicationId == "taxxoreditor")
        {
            var nodeRepositoryRoot = xmlHierarchyCloned.SelectSingleNode("//repositories");
            // /configuration/editors[1]/editor[1]/@version
            foreach (XmlNode nodeFilingEditor in xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor"))
            {
                var filingEditorId = GetAttribute(nodeFilingEditor, "id");
                var filingEditorVersion = GetAttribute(nodeFilingEditor, "version");
                var filingEditorName = nodeFilingEditor.SelectSingleNode("name").InnerText;
                var nodeRepro = xmlHierarchyCloned.CreateElement("repro");
                SetAttribute(nodeRepro, "id", filingEditorId);
                SetAttribute(nodeRepro, "name", filingEditorName);
                if (!string.IsNullOrEmpty(filingEditorVersion)) SetAttribute(nodeRepro, "version", filingEditorVersion);

                nodeRepositoryRoot.AppendChild(nodeRepro);
            }
        }

        // Render an XML that contains a list of methods/services that this application exposes to the outside world
        var xsltArgumentList = new XsltArgumentList();
        xsltArgumentList.AddParam("service-name", "", RetrieveNodeValueIfExists("/configuration/general/site_identifier", xmlApplicationConfiguration));
        xsltArgumentList.AddParam("service-id", "", RetrieveNodeValueIfExists("/configuration/general/application_id", xmlApplicationConfiguration));

        // Return the generated XML document
        return TransformXmlToDocument(xmlHierarchyCloned, $"{applicationRootPathOs}/backend/code/shared/xslt/service-description.xsl", xsltArgumentList);
    }

}

namespace Taxxor.Models
{
    /// <summary>
    /// Result class for service description operations
    /// </summary>
    public class ServiceDescriptionResult
    {
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public string? DebugInfo { get; set; }
        public int StatusCode { get; set; } = 200;
        public XmlDocument? XmlResponse { get; set; }
        public XmlDocument? ResponseContent { get; set; }
    }
}