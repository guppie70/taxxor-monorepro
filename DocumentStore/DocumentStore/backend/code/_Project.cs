using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using AutoMapper;
using DocumentStore.Protos;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using static Framework;

namespace Taxxor.Project
{

    /// <summary>
    /// Variables and logic specific for the Taxxor Editor
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        // Begin required variables, properties and fields

        public static string? CmsDataAppRootPathOs = null;
        public static string? CmsDataConfigRootPathOs = null;

        public static string? CmsProjectConfigRootPathOs = null;

        public static string? LocalSharedFolderPathOs { get; set; } = null;

        // For GIT
        public static string? GitUser = null;
        public static string? GitUserEmail = null;

        // For internal system calls
        public static string? SystemUser = null;

        public static XmlDocument XmlDataConfiguration = new XmlDocument();

        /// <summary>
        /// Contains metadata of the (content) files in use for all projects
        /// </summary>
        /// <returns></returns>
        public static XmlDocument XmlCmsContentMetadata = new XmlDocument();
        public static string CmsContentMetadataFilePathOs = null;

        /// <summary>
        /// Regular expression is used for bypassing authentication static files (css, js, etc.)
        /// The expression below will never match (https://stackoverflow.com/questions/1723182/a-regex-that-will-never-be-matched-by-anything) so all static assets are protected by default
        /// You can define the expression used in base_configuration.xml -> /configuration/general/settings/setting[@id='staticfiles-bypass-regex']
        /// </summary>
        public static Regex StaticFilesBypassAuthentication = new Regex(@"x\by", RegexOptions.Compiled | RegexOptions.IgnoreCase);


        /// <summary>
        /// Used to cache permissions retrieved from the Taxxor Access Control Service
        /// </summary>
        /// <returns></returns>
        public static ConcurrentDictionary<string, XmlDocument> RbacCacheData { get; set; } = new ConcurrentDictionary<string, XmlDocument>();

        /// <summary>
        /// Conditional XML cache
        /// </summary>
        public static ConditionalXmlCache XmlCache { get; set; } = new ConditionalXmlCache();

        /// <summary>
        /// Delegate that defines a fixed method signature for the data transform methods
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public delegate XmlDocument DataAnalysisDelegate(XmlDocument input);

        /// <summary>
        /// Dictionary that is used to map a function ID to a delegate that can be invoked
        /// </summary>
        public static Dictionary<string, DataAnalysisDelegate> DataAnalysisMethods;

        /// <summary>
        /// Lists all data transformation routines that we have defined in this code set
        /// </summary>
        /// <value></value>
        public static List<DataAnalysisAttribute> DataAnalysisList { get; set; }


        /// <summary>
        /// Delegate that defines a fixed method signature for the data transform methods
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public delegate Task<TaxxorReturnMessage> DataServiceToolDelegate(dynamic input);

        /// <summary>
        /// Dictionary that is used to map a function ID to a delegate that can be invoked
        /// </summary>
        public static Dictionary<string, DataServiceToolDelegate> DataServiceToolMethods;

        /// <summary>
        /// Lists all data transformation routines that we have defined in this code set
        /// </summary>
        /// <value></value>
        public static List<DataServiceToolAttribute> DataServiceMethodsList { get; set; }

        /// <summary>
        /// Taxxor client ID ("philips", "")
        /// </summary>
        /// <value></value>
        public static string? TaxxorClientId { get; set; } = null;

        /// <summary>
        /// Contains tenant specific settings
        /// </summary>
        public struct TenantSettings
        {
            public int FullYearOffsetInMonths { get; set; }
            public string? Setting2 { get; set; }

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
        /// Flags if the system should log all the incoming and outgoing URI's
        /// </summary>
        /// <value></value>
        public static bool UriLogEnabled { get; set; } = false;

        /// <summary>
        /// Contains a list of URI's that are used by the system to connect to backend services
        /// </summary>
        /// <typeparam name="string"></typeparam>
        /// <returns></returns>
        public static List<string> UriLogBackend { get; set; } = new List<string>();


        public static string? ThumbnailFilenameTemplate { get; set; } = null;
        public static string? ThumbnailFileExtension { get; set; } = null;
        public static int ThumbnailMaxSize { get; set; } = 120;
        public static string? ImageRenditionsFolderName { get; set; } = null;
        public static byte[]? BrokenImageBytes { get; set; } = null;

        public static ConcurrentDictionary<string, string> ContentCache = new ConcurrentDictionary<string, string>();

        public static ConcurrentDictionary<string, string[]> SyncSections = new ConcurrentDictionary<string, string[]>();


        /// <summary>
        /// Utility that calculates the values for project specific variables
        /// </summary>
        public static void InitProjectLogic()
        {

            // Project specific configuration path
            CmsProjectConfigRootPathOs = CalculateFullPathOs("cmsprojectconfigroot");

            // Data configuration root folder path
            CmsDataConfigRootPathOs = CalculateFullPathOs("cmsdataconfigroot");

            // Git user
            GitUser = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='git-username']", xmlApplicationConfiguration);
            GitUserEmail = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='git-email']", xmlApplicationConfiguration);

            // ID of the current client using this Taxxor installation
            TaxxorClientId = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='taxxor-client-id']")?.InnerText ?? "";

            // System user
            SystemUser = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='system-userid']", xmlApplicationConfiguration);

            CompileConnectedServicesInformation(siteType).GetAwaiter().GetResult();

            // Console.WriteLine($"**********");
            // Console.WriteLine($"- dataRootPathOs: {dataRootPathOs}");
            // Console.WriteLine($"- logRootPathOs: {logRootPathOs}");
            // Console.WriteLine($"*********");

            // Create important application directories
            List<string> dirsToCreate = new List<string>();
            dirsToCreate.Add($"{dataRootPathOs}/users");
            dirsToCreate.Add($"{logRootPathOs}/inspector");
            dirsToCreate.Add($"{logRootPathOs}/profiling");

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

            // Dump the values that we have found to the console
            // Console.WriteLine($"********** ProjectLogic fields *************");
            if (hostingEnvironment.EnvironmentName == "Development")
            {
                // Somehow this syntax does not seem to work here: AddDebugVariable(() => cmsRootPath);
                AddDebugVariable("cmsDataConfigRootPathOs", CmsDataConfigRootPathOs);
                AddDebugVariable("cmsProjectConfigRootPathOs", CmsProjectConfigRootPathOs);
                AddDebugVariable("gitUser", GitUser);
                AddDebugVariable("gitUserEmail", GitUserEmail);
            }
            AddDebugVariable("TaxxorClientId", TaxxorClientId);

            // Enable/disable uri logging
            var enableUriLoggingString = RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='log-uris']", xmlApplicationConfiguration);
            if (!string.IsNullOrEmpty(enableUriLoggingString) && siteType != "prev")
            {
                UriLogEnabled = (enableUriLoggingString == "true");
            }
            AddDebugVariable("UriLogEnabled", UriLogEnabled.ToString());

            // Reflection logic
            SetupCustomMethodReferenceLists();

            // Call the custom version of the project variables
            Extensions.RetrieveProjectVariablesCustom();

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

            try
            {
                // Update the information about the connected Taxxor webservices
                CompileConnectedServicesInformation(siteType).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was a problem collecting the connected services information");
            }


            try
            {
                CmsContentMetadataFilePathOs = $"{dataRootPathOs}/cache/cmscontentmetadata.xml";
                // Render the CMS Content metadata
                var watch = System.Diagnostics.Stopwatch.StartNew();

                if (!File.Exists(CmsContentMetadataFilePathOs))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(CmsContentMetadataFilePathOs))) Directory.CreateDirectory(Path.GetDirectoryName(CmsContentMetadataFilePathOs));
                    Console.WriteLine("**** Start compiling full CMS metadata document ****");
                    XmlCmsContentMetadata.ReplaceContent(CompileCmsMetadata(null, false));
                    watch.Stop();
                    Console.WriteLine($"**** Finished compiling full CMS metadata document (took: {watch.ElapsedMilliseconds.ToString()} ms) ****");
                }
                else
                {
                    Console.WriteLine("**** Retrieve the full CMS metadata document from the cache ****");
                    XmlCmsContentMetadata.Load(CmsContentMetadataFilePathOs);
                    watch.Stop();
                    Console.WriteLine($"**** Finished loading the full CMS metadata document (took: {watch.ElapsedMilliseconds.ToString()} ms) ****");
                }



                // XmlCmsContentMetadata.Save(logRootPathOs + "/metadata-startup.xml");
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was a problem rendering the CMS content metadata object");
            }


            try
            {
                // Update the information about the GIT repositories used in this application so that we can use that when rendering the service description
                var success = RetrieveTaxxorGitRepositoriesVersionInfo(false);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was a problem updating the GIT repository version information");
            }

            // Application has been successfully initiated
            AppInitiated = true;
        }


        /// <summary>
        /// Use reflection to loop through the C# code and build up references to methods so that we can dynamically execute them
        /// </summary>
        public static void SetupCustomMethodReferenceLists()
        {
            // Find all the data transformation scenarios that we have available in the ProjectLocic code set
            // By looping through all the methods that have the custom attribute set
            DataAnalysisList = new List<DataAnalysisAttribute>();
            DataAnalysisMethods = new Dictionary<string, DataAnalysisDelegate>();

            DataServiceMethodsList = new List<DataServiceToolAttribute>();
            DataServiceToolMethods = new Dictionary<string, DataServiceToolDelegate>();

            foreach (MethodInfo methodInfo in typeof(ProjectLogic).GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                // Fill the data analysis tool list
                DataAnalysisAttribute customDataAnalysisAttribute = methodInfo.GetCustomAttribute<DataAnalysisAttribute>();
                if (customDataAnalysisAttribute != null)
                {
                    DataAnalysisList.Add(customDataAnalysisAttribute);

                    try
                    {
                        DataAnalysisMethods[customDataAnalysisAttribute.Id] = (DataAnalysisDelegate)Delegate.CreateDelegate(typeof(DataAnalysisDelegate), methodInfo, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not add data analysis method with ID {customDataAnalysisAttribute.Id}. error: {ex}");
                    }

                }

                // Fill the data service tools list
                DataServiceToolAttribute customDataServiceToolAttribute = methodInfo.GetCustomAttribute<DataServiceToolAttribute>();
                if (customDataServiceToolAttribute != null)
                {
                    DataServiceMethodsList.Add(customDataServiceToolAttribute);

                    try
                    {
                        DataServiceToolMethods[customDataServiceToolAttribute.Id] = (DataServiceToolDelegate)Delegate.CreateDelegate(typeof(DataServiceToolDelegate), methodInfo, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not add data tool method with ID {customDataAnalysisAttribute.Id}. error: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Tests if the combination projectId, versionId and dataType is exists in the project definition
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        public static bool ValidateCmsPostedParameters(ProjectVariables projectVars, string dataType)
        {
            return ValidateCmsPostedParameters(projectVars.projectId, projectVars.versionId, dataType);
        }

        /// <summary>
        /// Tests if the combination projectId, versionId and dataType is exists in the project definition
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        public static bool ValidateCmsPostedParameters(string projectId, string versionId, string dataType)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

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
                                    errorDetails = "Data type " + dataType + " could not be found.";
                                }

                                break;
                        }

                    }
                    else
                    {
                        errorReason = "Version does not exist";
                        errorDetails = "Version " + versionId + " could not be found.";
                    }
                }
                else
                {
                    errorReason = "Project does not exist";
                    errorDetails = "Project with id " + projectId + " does not exist.";
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
                HandleError(reqVars, errorReason, errorDetails);
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
        /// Retrieves the full folder path where the content of a filing is located
        /// </summary>
        /// <returns></returns>
        public static string RetrieveFilingDataFolderPathOs()
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var dataFolderPathOs = $"{projectVars.cmsDataRootPathOs}/textual";

            return dataFolderPathOs;
        }

        /// <summary>
        /// Retrieves the filing data root path from a project ID
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static string RetrieveFilingDataFolderPathOs(string projectId)
        {
            // Determine the folder where the data lives
            return RetrieveFilingDataRootFolderPathOs(projectId) + "/textual";
        }

        /// <summary>
        /// Retrieves the metadata folder location path from a project ID
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static string? RetrieveFilingDataRootFolderPathOs(string projectId)
        {
            // Determine the folder where the data lives
            var nodeProject = xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]");
            if (nodeProject == null)
            {
                return null;
            }
            else
            {
                var cmsDataRootPathOs = dataRootPathOs + nodeProject.SelectSingleNode("location[@id='reportdataroot']")?.InnerText ?? "";
                var versionSubPath = nodeProject.SelectSingleNode("versions/version[@id='1']/path")?.InnerText ?? "";
                return cmsDataRootPathOs + versionSubPath.Replace("[versionid]", "1");
            }
        }

        /// <summary>
        /// Retrieve the OS path location for the project images
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static string RetrieveImagesRootFolderPathOs(string projectId)
        {
            return RetrieveImagesRootFolderPathOs(xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]"));
        }

        /// <summary>
        /// Retrieve the OS path location for the project images
        /// </summary>
        /// <param name="nodeProject"></param>
        /// <returns></returns>
        public static string RetrieveImagesRootFolderPathOs(XmlNode nodeProject)
        {
            if (nodeProject == null)
            {
                return null;
            }
            else
            {
                var cmsDataRootPathOs = dataRootPathOs + nodeProject.SelectSingleNode("location[@id='reportdataroot']")?.InnerText ?? "";
                var subPath = nodeProject.SelectSingleNode("repositories/assets/repository/location[@id='filing_images']")?.InnerText ?? "";
                return cmsDataRootPathOs + subPath;
            }
        }

        /// <summary>
        /// Retrieve the OS path location for the project downloads
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static string? RetrieveDownloadsRootFolderPathOs(string projectId)
        {
            return RetrieveDownloadsRootFolderPathOs(xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]"));
        }

        /// <summary>
        /// Retrieve the OS path location for the project downloads
        /// </summary>
        /// <param name="nodeProject"></param>
        /// <returns></returns>
        public static string? RetrieveDownloadsRootFolderPathOs(XmlNode nodeProject)
        {
            if (nodeProject == null)
            {
                return null;
            }
            else
            {
                var cmsDataRootPathOs = dataRootPathOs + nodeProject.SelectSingleNode("location[@id='reportdataroot']")?.InnerText ?? "";
                var subPath = nodeProject.SelectSingleNode("repositories/assets/repository/location[@id='filing_downloads']")?.InnerText ?? "";
                return cmsDataRootPathOs + subPath;
            }
        }

        /// <summary>
        /// Renders an XML overview containing all the outputchannel hierarchies for the current project
        /// </summary>
        /// <param name="includeAllItems"></param>
        /// <param name="addFilingMetadata"></param>
        /// <returns></returns>
        public static XmlDocument RenderOutputChannelHierarchyOverview(string projectId, bool includeAllItems = true)
        {
            var debugRoutine = (siteType == "local");

            // Compile one document based on all the output channels
            var xmlOutputChannelHierarchies = new XmlDocument();
            xmlOutputChannelHierarchies.LoadXml("<hierarchies/>");

            var editorId = RetrieveEditorIdFromProjectId(projectId);

            var nodeListOutputChannels = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(editorId) + "]/output_channels/output_channel");

            foreach (XmlNode nodeOutputChannel in nodeListOutputChannels)
            {
                string? outputChannelType = GetAttribute(nodeOutputChannel, "type");

                var nodeListOutputChannelVariants = nodeOutputChannel.SelectNodes("variants/variant");
                foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                {
                    var currentOutputChannelVariantId = GetAttribute(nodeVariant, "id");
                    var currentOutputChannelLanguage = GetAttribute(nodeVariant, "lang");
                    var hierarchyId = GetAttribute(nodeVariant, "metadata-id-ref");

                    // Retrieve the hierarchy from the Taxxor Data Store
                    XmlDocument hierarchy = new XmlDocument();
                    var pathOs = CalculateHierarchyPathOs(projectId, hierarchyId, false);

                    if (pathOs != null)
                    {
                        try
                        {
                            if (File.Exists(pathOs))
                            {
                                hierarchy.Load(pathOs);

                                // Wrap the hierarchy in a wrapper node and append it to the overall document
                                var outputChannelElement = xmlOutputChannelHierarchies.CreateElement("output_channel");
                                SetAttribute(outputChannelElement, "type", outputChannelType);
                                SetAttribute(outputChannelElement, "id", currentOutputChannelVariantId);
                                SetAttribute(outputChannelElement, "lang", currentOutputChannelLanguage);

                                var nodeNameImported = xmlOutputChannelHierarchies.ImportNode(nodeVariant.SelectSingleNode("name"), true);
                                outputChannelElement.AppendChild(nodeNameImported);

                                var nodeHierarchyRootImported = xmlOutputChannelHierarchies.ImportNode(hierarchy.DocumentElement, true);
                                outputChannelElement.AppendChild(nodeHierarchyRootImported);
                                xmlOutputChannelHierarchies.DocumentElement.AppendChild(outputChannelElement);
                            }
                            else
                            {
                                appLogger.LogWarning($"Could not load hierarchy in RenderOutputChannelHierarchyOverview(). pathOs: {pathOs}, projectId: {projectId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"There was an error processing the hierarchy of project: {projectId}");
                        }
                    }

                }
            }

            if (includeAllItems)
            {
                // Add all the section elements to the overview
                var outputAllItems = xmlOutputChannelHierarchies.CreateElement("item_overview");

                var cmsDataRootPathOs = RetrieveFilingDataFolderPathOs(projectId);
                var retrieveOverviewResult = RetrieveFilingDataOverview(cmsDataRootPathOs);
                if (retrieveOverviewResult.Success)
                {
                    XmlDocument xmlSourceDataOverview = retrieveOverviewResult.XmlPayload;
                    var nodeSourceDataImported = xmlOutputChannelHierarchies.ImportNode(xmlSourceDataOverview.DocumentElement, true);
                    outputAllItems.AppendChild(nodeSourceDataImported);
                    xmlOutputChannelHierarchies.DocumentElement.AppendChild(outputAllItems);
                }
            }

            // Dump the hierarchy for inspection
            if (debugRoutine)
            {
                var saved = SaveXmlDocument(xmlOutputChannelHierarchies, logRootPathOs + $"/_output-channel-hierarchy-{projectId}.xml");
            }

            return xmlOutputChannelHierarchies;
        }


        /// <summary>
        /// Central function for calculating core paths (cmsDataRootPath, cmsDataRootPathOs, etc.) from a reasonably filled ProjectVariables object
        /// </summary>
        /// <param name="projectVars"></param>
        public static void FillCorePathsInProjectVariables(ref ProjectVariables projectVars)
        {
            projectVars.cmsDataRootPath = dataRootPath + RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/location[@id='reportdataroot']", xmlApplicationConfiguration);
            projectVars.cmsDataRootBasePathOs = dataRootPathOs + RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/location[@id='reportdataroot']", xmlApplicationConfiguration);
            projectVars.cmsContentRootPathOs = projectVars.cmsDataRootBasePathOs + "/content";

            //add the version path to the cms data root path
            var xpath = "/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/versions/version[@id=" + GenerateEscapedXPathString(((projectVars.versionId == "latest") ? "1" : projectVars.versionId)) + "]/path";
            var versionSubPath = RetrieveNodeValueIfExists(xpath, xmlApplicationConfiguration);

            if (string.IsNullOrEmpty(versionSubPath))
            {
                var errorMessage = $"Could not find the version sub path";
                appLogger.LogError($"{errorMessage}. xpath: {xpath}, stack-trace: {GetStackTrace()}");
                throw new Exception(errorMessage);
            }
            //Response.Write("**"+versionSubPath);
            //Response.End();
            versionSubPath = versionSubPath.Replace("[versionid]", ((projectVars.versionId == "latest") ? "1" : projectVars.versionId));
            projectVars.cmsDataRootPathOs = projectVars.cmsDataRootBasePathOs + versionSubPath;



            // Retrieve current year /configuration/cms_projects[1]/cms_project[1]/reporting_period[1]
            projectVars.reportingPeriod = RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/reporting_period", xmlApplicationConfiguration);

            // Default language is the first output channel language defined in the configurationc
            if (!string.IsNullOrEmpty(projectVars.editorId))
            {
                projectVars.outputChannelDefaultLanguage = RetrieveAttributeValueIfExists($"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels[1]/output_channel[1]/variants[1]/variant[1]/@lang", xmlApplicationConfiguration);
            }
        }

        /// <summary>
        /// Centralized helper method to initialize ProjectVariables for gRPC requests.
        ///
        /// This method replicates the behavior of the ProjectVariables middleware (ProjectVariablesMiddleware.cs)
        /// for gRPC handlers, ensuring backward compatibility with REST API behavior.
        ///
        /// The method performs two key operations:
        /// 1. Uses AutoMapper to map from GrpcProjectVariables (or any request containing them) to ProjectVariables
        /// 2. Calls FillCorePathsInProjectVariables to calculate all derived path properties
        ///
        /// Path properties calculated by FillCorePathsInProjectVariables include:
        /// - cmsDataRootPath: Web path to project data root
        /// - cmsDataRootBasePathOs: OS path to project data root base
        /// - cmsDataRootPathOs: Full OS path including project ID and version (e.g., /mnt/data/projects/project-name/ar24/version_1)
        /// - cmsContentRootPathOs: OS path to content folder
        /// - reportingPeriod: Project reporting period
        /// - outputChannelDefaultLanguage: Default output channel language
        ///
        /// Usage in gRPC handlers:
        /// <code>
        /// var projectVars = InitializeProjectVariablesForGrpc(mapper, request);
        /// </code>
        ///
        /// This replaces the previous pattern of:
        /// <code>
        /// var projectVars = mapper.Map&lt;ProjectVariables&gt;(request);
        /// FillCorePathsInProjectVariables(ref projectVars);
        /// </code>
        /// </summary>
        /// <param name="mapper">AutoMapper instance for mapping GrpcProjectVariables to ProjectVariables</param>
        /// <param name="source">Source object containing GrpcProjectVariables (can be any gRPC request type)</param>
        /// <returns>Fully initialized ProjectVariables with all paths and properties calculated</returns>
        /// <remarks>
        /// This method is essential for maintaining consistency between REST and gRPC endpoints.
        /// The REST middleware automatically initializes these properties for every request,
        /// and this method ensures gRPC handlers have the same initialized state.
        ///
        /// The AutoMapper configuration (AutoMapper.cs) handles mapping from various request types:
        /// - GetFilingComposerDataRequest → ProjectVariables
        /// - SaveSourceDataRequest → ProjectVariables
        /// - GrpcProjectVariables → ProjectVariables (direct)
        /// - object → ProjectVariables (generic, extracts nested GrpcProjectVariables)
        /// </remarks>
        public static ProjectVariables InitializeProjectVariablesForGrpc(IMapper mapper, object source)
        {
            ProjectVariables projectVars = null;

            // BEST PRACTICE: Extract GrpcProjectVariables first and use the central mapping
            // This ensures consistent user credential handling across all gRPC request types
            if (source != null)
            {
                var sourceType = source.GetType();
                var grpcPropInfo = sourceType.GetProperty("GrpcProjectVariables");

                if (grpcPropInfo != null)
                {
                    var grpcProjectVariables = grpcPropInfo.GetValue(source) as GrpcProjectVariables;

                    if (grpcProjectVariables != null)
                    {
                        // Use the central GrpcProjectVariables → ProjectVariables mapping
                        // This mapping is guaranteed to include currentUser handling
                        projectVars = mapper.Map<ProjectVariables>(grpcProjectVariables);
                    }
                }
            }

            // Fallback: If we couldn't extract GrpcProjectVariables, use the request object directly
            if (projectVars == null)
            {
                projectVars = mapper.Map<ProjectVariables>(source);
            }

            // Calculate all derived path properties (cmsDataRootPathOs, cmsContentRootPathOs, etc.)
            // This replicates what the REST middleware does
            FillCorePathsInProjectVariables(ref projectVars);

            return projectVars;
        }

        /// <summary>
        /// Placeholder class
        /// </summary>
        public class PdfProperties
        {

        }





    }
}

/// <summary>
/// Aims to contain all the necessary data and variables for a request
/// </summary>
public class RequestContext
{
    public RequestVariables RequestVariables { get; set; } = new RequestVariables();
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