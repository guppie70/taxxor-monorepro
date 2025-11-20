using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Helper routines
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {




        /// <summary>
        /// Updates xmlApplicationConfiguration with the nodes that are present in the data configuration file
        /// </summary>
        /// <returns>Information if the merge was successful.</returns>
        /// <param name="updateInternalDataConfugurationReference">If set to <c>true</c> update internal data confuguration reference.</param>
        public static async Task<TaxxorReturnMessage> UpdateDataConfigurationInApplicationConfiguration(bool updateInternalDataConfugurationReference = true)
        {
            // Load a fresh version of the data configuration document
            XmlDocument xmlFreshDataConfiguration = new XmlDocument();
            if (applicationId == "documentstore")
            {
                xmlFreshDataConfiguration.Load(CalculateFullPathOs("data_configuration"));
            }
            else
            {
                xmlFreshDataConfiguration = await CallTaxxorDataService<XmlDocument>(RequestMethodEnum.Get, "dataconfiguration");
            }

            // Run the overload method
            return await UpdateDataConfigurationInApplicationConfiguration(xmlFreshDataConfiguration, updateInternalDataConfugurationReference);
        }

        /// <summary>
        /// Updates xmlApplicationConfiguration with the nodes that are present in the data configuration file
        /// </summary>
        /// <returns>Information if the merge was successful.</returns>
        /// <param name="xmlDataConfig">Xml data config.</param>
        /// <param name="updateInternalDataConfugurationReference">If set to <c>true</c> update internal data confuguration reference.</param>
        public static async Task<TaxxorReturnMessage> UpdateDataConfigurationInApplicationConfiguration(XmlDocument xmlDataConfig, bool updateInternalDataConfugurationReference = true)
        {
            await DummyAwaiter();

            // Update the internal version of the data configuration with the one that we just received
            if (updateInternalDataConfugurationReference) XmlDataConfiguration.ReplaceContent(xmlDataConfig);

            // Loop through the nodes and replace them in application configuration
            var xpathDataConfiguration = "/configuration/*";
            var xpathApplicationConfigurationBase = "/configuration/";
            var nodeListDataConfiguration = xmlDataConfig.SelectNodes(xpathDataConfiguration);
            foreach (XmlNode nodeSource in nodeListDataConfiguration)
            {
                if (nodeSource.LocalName == "editors" || nodeSource.LocalName == "report_types" || nodeSource.LocalName == "cms_projects")
                {
                    var xpathApplicationConfiguration = xpathApplicationConfigurationBase + nodeSource.LocalName;
                    var nodeTarget = xmlApplicationConfiguration.SelectSingleNode(xpathApplicationConfiguration);
                    if (nodeTarget == null) return new TaxxorReturnMessage(false, "Could not locate target node", $"xpath: '{xpathApplicationConfiguration}', stack-trace: {GetStackTrace()}");

                    try
                    {
                        ReplaceXmlNode(nodeTarget, nodeSource);
                    }
                    catch (Exception ex)
                    {
                        return new TaxxorReturnMessage(false, "Something went wrong trying to replace an XML node", $"xpath: '{xpathApplicationConfiguration}', error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }


            }

            // Run a test to see if the imports have succeesed by comparing the source and target nodes
            if (siteType != "prod")
            {
                foreach (XmlNode nodeSource in nodeListDataConfiguration)
                {
                    if (nodeSource.LocalName == "editors" || nodeSource.LocalName == "report_types" || nodeSource.LocalName == "cms_projects")
                    {
                        var xpathApplicationConfiguration = xpathApplicationConfigurationBase + nodeSource.LocalName;
                        var nodeTarget = xmlApplicationConfiguration.SelectSingleNode(xpathApplicationConfiguration);
                        if (nodeTarget == null) return new TaxxorReturnMessage(false, "Could not locate target node", $"xpath: '{xpathApplicationConfiguration}', stack-trace: {GetStackTrace()}");

                        // Source and target hash
                        var sourceHash = GenerateXmlHash(nodeSource);
                        var targetHash = GenerateXmlHash(nodeTarget);

                        if (sourceHash != targetHash)
                        {
                            return new TaxxorReturnMessage(false, "Source and target sections/nodes are not the same", $"xpath: '{xpathApplicationConfiguration}', stack-trace: {GetStackTrace()}");
                        }
                    }
                }
            }

            // Return a success message
            return new TaxxorReturnMessage(true, "Successfully imported new information from data configuration");
        }

        /// <summary>
        /// Helper function that wraps content of a file on the disk in a standard XML success message
        /// </summary>
        /// <returns>The taxxor xml envelope for file transport.</returns>
        /// <param name="pathOs">Path os.</param>
        public static async Task<XmlDocument> CreateTaxxorXmlEnvelopeForFileTransport(string pathOs)
        {
            if (!File.Exists(pathOs)) throw new Exception($"File {pathOs} does not exist");

            var fileType = GetFileType(pathOs);
            if (fileType == null) throw new Exception("Could not determine the file type to use");

            var fileContent = "";
            if (fileType == "text")
            {
                var content = await RetrieveTextFile(pathOs);
                fileContent = HttpUtility.HtmlEncode(content);
            }
            else
            {
                // This is a binary so encode it in Base64 format so that we can transport it in an XML envelope
                fileContent = Base64EncodeBinaryFile(pathOs);
            }

            // Stick the file content in the message field of the success xml and the file path into the debuginfo node
            return GenerateSuccessXml(fileContent, pathOs);
        }

        /// <summary>
        /// Renders the hierarchy of a document based on the passed project and output channel id's
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="editorId"></param>
        /// <param name="editorContentType"></param>
        /// <param name="outputChannelVariantId"></param>
        /// <returns></returns>
        public static XmlDocument PostProcessOutputChannelHierarchy(XmlDocument xmlOutputChannelHierarchy, string editorId)
        {
            // Set the editable allowed node
            if (editorId != "default_editor")
            {
                MarkHierarchyAccessRights(ref xmlOutputChannelHierarchy);
            }

            return xmlOutputChannelHierarchy;
        }

        /// <summary>
        /// Retrieves the metadata ID for the output hierarchy that is used in the XML configuration and also in projectVars.cmsMetaData[]
        /// </summary>
        /// <returns>The output channel hierarchy metadata identifier.</returns>
        /// <param name="editorId">Editor identifier.</param>
        /// <param name="outputChannelType">Output channel type.</param>
        /// <param name="outputChannelVariantId">Output channel variant identifier.</param>
        /// <param name="outputChannelVariantLanguage">Output channel variant language.</param>
        public static string RetrieveOutputChannelHierarchyMetadataId(string editorId, string? outputChannelType, string? outputChannelVariantId, string? outputChannelVariantLanguage)
        {
            //
            // => Fill empty variables so we will maximize the chance of success in this functiob
            //
            if (string.IsNullOrEmpty(outputChannelVariantId))
            {
                string? langToUse = string.IsNullOrEmpty(outputChannelVariantLanguage) ? null : outputChannelVariantLanguage;
                outputChannelVariantId = RetrieveFirstOutputChannelVariantIdFromEditorId(editorId, "pdf", langToUse);
            }

            if (string.IsNullOrEmpty(outputChannelType) || string.IsNullOrEmpty(outputChannelVariantLanguage))
            {
                ProjectVariables projectVars = RetrieveProjectVariables(System.Web.Context.Current);

                if (string.IsNullOrEmpty(outputChannelType)) outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectVars.projectId, outputChannelVariantId);
                if (string.IsNullOrEmpty(outputChannelVariantLanguage)) outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, outputChannelVariantId);
            }


            //
            // => Retrieve the metadata key for the hierarchy of the output channel
            //
            var xpath = $"/configuration/editors/editor[@id='{editorId}']/output_channels/output_channel[@type='{outputChannelType}']/variants/variant[@id='{outputChannelVariantId}' and @lang='{outputChannelVariantLanguage}']/@metadata-id-ref";
            var outputChannelHierarchyId = RetrieveAttributeValueIfExists(xpath, xmlApplicationConfiguration);
            if (string.IsNullOrEmpty(outputChannelHierarchyId))
            {
                // appLogger.LogInformation($"Could not find outputchannel hierarchy id, xpath: {xpath}, RetrieveOutputChannelHierarchyMetadataId('{editorId}', '{outputChannelType}', '{outputChannelVariantId}', '{outputChannelVariantLanguage}')");

                // Fix for case when outputChannelVariantLanguage is wrong
                xpath = $"/configuration/editors/editor[@id='{editorId}']/output_channels/output_channel[@type='{outputChannelType}']/variants/variant[@id='{outputChannelVariantId}' or @lang='{outputChannelVariantLanguage}']/@metadata-id-ref";
                outputChannelHierarchyId = RetrieveAttributeValueIfExists(xpath, xmlApplicationConfiguration);
                if (string.IsNullOrEmpty(outputChannelHierarchyId))
                {
                    appLogger.LogWarning($"Fallback attempt: could not find outputchannel hierarchy id, xpath: {xpath}");
                }
            }
            return outputChannelHierarchyId ?? "main_hierarchy";
        }


        /// <summary>
        /// Retrieves the node where the taxonomy location is defined
        /// </summary>
        /// <returns>The taxonomy xml node.</returns>
        /// <param name="projectId">Project identifier.</param>
        public static XmlNode? RetrieveOutputchannelHierarchyLocationXmlNode(string projectId, string hierarchyId)
        {
            var xpath = $"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectId)}]/metadata_system/metadata[@id={GenerateEscapedXPathString(hierarchyId)}]/location";
            return xmlApplicationConfiguration.SelectSingleNode(xpath);
        }

        /// <summary>
        /// Renders the full path to a data file based on the type of data that is being passed
        /// </summary>
        /// <param name="basePathOrName"></param>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        public static string RenderDataXmlPathOs(string basePathOrName, string projectId, string versionId, string dataType)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            var xmlFilePathOs = "";
            var xmlFolderPathOs = "";

            string xPath = "/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]/versions/version[@id=" + GenerateEscapedXPathString(versionId) + "]/data";
            switch (dataType)
            {
                case "text":
                    xPath += "/textual_data/path";
                    break;
                case "cbsdata":
                    xPath = "/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]/content_types/content_management/type[@id='regular']/xml[1]";
                    break;
                case "efr":
                    xPath += "/financial_data/path";
                    break;
                case "metadata":
                    xPath += "/meta_data/path";
                    break;
                default:
                    xPath += "/*/path";
                    break;
            }

            XmlNode? xmlNode = xmlApplicationConfiguration.SelectSingleNode(xPath);

            if (xmlNode != null)
            {
                xmlFolderPathOs = CalculateFullPathOs(xmlNode);
                if (xmlFolderPathOs == null)
                {
                    HandleError(reqVars, "<error><reason>Could not resolve base path</reason></error>", "");
                }
                else
                {
                    if (!String.IsNullOrEmpty(basePathOrName))
                    {
                        if (Left(basePathOrName, 1) != "/")
                        {
                            basePathOrName = "/" + basePathOrName;
                        }
                        xmlFilePathOs = xmlFolderPathOs + basePathOrName;
                    }
                    else
                    {
                        xmlFilePathOs = xmlFolderPathOs;
                    }

                }
            }
            else
            {
                var xml = "<error><reason>Could not find source folder definition</reason></error>";
                HandleError(reqVars, xml);
            }

            return xmlFilePathOs;

        }


        /// <summary>
        /// Retrieves the GIT Tags of the repositories available in this service and stores it in application configuration
        /// </summary>
        /// <returns><c>true</c>, if taxxor git repositories version info was retrieved, <c>false</c> otherwise.</returns>
        /// <param name="includeTaxxorEditors">If set to <c>true</c> include taxxor editors.</param>
        public static async Task<bool> RetrieveTaxxorGitRepositoriesVersionInfo(bool includeTaxxorEditors)
        {
            var error = false;
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var extendedLogging = false;

            // Variables for the cache
            bool useCache = siteType != "local";
            var xmlCacheDocument = new XmlDocument();
            var repositoriesCachePathOs = $"{applicationRootPathOs}/_repro-info.xml";

            var pathType = "";
            var relativePath = "";

            try
            {
                XmlNode? nodeRepositoriesRoot = null;
                XmlNode? nodeEditorsRoot = null;

                if (useCache && File.Exists(repositoriesCachePathOs))
                {
                    xmlCacheDocument.Load(repositoriesCachePathOs);
                    nodeRepositoriesRoot = xmlCacheDocument.SelectSingleNode("/repros/repositories");
                    nodeEditorsRoot = xmlCacheDocument.SelectSingleNode("/repros/editors");
                }
                else
                {
                    // Build a new cache file
                    var nodeRepros = xmlCacheDocument.CreateElement("repros");
                    nodeRepositoriesRoot = xmlCacheDocument.CreateElement("repositories");
                    nodeRepros.AppendChild(nodeRepositoriesRoot);
                    nodeEditorsRoot = xmlCacheDocument.CreateElement("editors");
                    nodeRepros.AppendChild(nodeEditorsRoot);
                    xmlCacheDocument.AppendChild(nodeRepros);
                }



                // A) Main git repositories
                var nodeListGitRepositoryLocations = xmlApplicationConfiguration.SelectNodes("/configuration/repositories/*/repro//location");
                foreach (XmlNode nodeGitRepositoryLocation in nodeListGitRepositoryLocations)
                {
                    // nodeGitRepositoryLocation.SetAttribute("checked", "true");
                    var repositoryName = nodeGitRepositoryLocation.GetAttribute("id") ?? "unknown";
                    pathType = nodeGitRepositoryLocation.GetAttribute("path-type");
                    relativePath = nodeGitRepositoryLocation.InnerText;
                    var repositoryPathOs = _calculateFullPathOsWithoutProjectVariables(relativePath, pathType);

                    LogRepositoryProcessing(repositoryName, repositoryPathOs);

                    if (string.IsNullOrEmpty(repositoryPathOs))
                    {
                        if (debugRoutine) appLogger.LogWarning($"RetrieveTaxxorGitRepositoriesVersionInfo() repositoryPathOs null or empty for location with ID {GetAttribute(nodeGitRepositoryLocation, "id")}");
                        error = true;
                    }
                    else
                    {
                        if (repositoryPathOs.Contains("devpack") || !useCache)
                        {
                            if (extendedLogging) appLogger.LogCritical("    -> processing");
                            _markGitTagContentInNode(nodeGitRepositoryLocation, repositoryPathOs, false);

                            var repositoryId = GetAttribute(nodeGitRepositoryLocation, "id");
                            var repositoryVersion = GetAttribute(nodeGitRepositoryLocation, "version");
                            if (!string.IsNullOrEmpty(repositoryId) && !string.IsNullOrEmpty(repositoryVersion))
                            {
                                // Remove the old repository nodes
                                var nodeListRepositoriesToRemove = nodeRepositoriesRoot.SelectNodes($"repository[@id='{repositoryId}']");
                                if (nodeListRepositoriesToRemove != null && nodeListRepositoriesToRemove.Count > 0) RemoveXmlNodes(nodeListRepositoriesToRemove);


                                var newRepositoryNode = xmlCacheDocument.CreateElement("repository");
                                SetAttribute(newRepositoryNode, "id", repositoryId);
                                SetAttribute(newRepositoryNode, "version", repositoryVersion);
                                nodeRepositoriesRoot.AppendChild(newRepositoryNode);
                            }
                        }
                        else
                        {
                            if (extendedLogging) appLogger.LogCritical("    -> skipped");
                        }


                    }
                }

                // B) Editors
                if (includeTaxxorEditors)
                {
                    var nodeListGitRepositoriesEditors = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor");
                    foreach (XmlNode nodeGitRepositoryEditor in nodeListGitRepositoriesEditors)
                    {
                        var nodeLocation = nodeGitRepositoryEditor.SelectSingleNode("path");
                        if (nodeLocation == null)
                        {
                            if (debugRoutine) appLogger.LogWarning($"RetrieveTaxxorGitRepositoriesVersionInfo() could not find path information for editor with ID {GetAttribute(nodeGitRepositoryEditor, "id")}");
                        }
                        else
                        {
                            var repositoryName = nodeGitRepositoryEditor.GetAttribute("id") ?? "unknown";
                            pathType = GetAttribute(nodeLocation, "path-type");
                            relativePath = nodeLocation.InnerText;
                            var repositoryPathOs = _calculateFullPathOsWithoutProjectVariables(relativePath, pathType);

                            LogRepositoryProcessing(repositoryName, repositoryPathOs);

                            if (string.IsNullOrEmpty(repositoryPathOs))
                            {
                                if (debugRoutine) appLogger.LogWarning($"RetrieveTaxxorGitRepositoriesVersionInfo() repositoryPathOs null or empty for editor location with ID {GetAttribute(nodeLocation, "id")}");
                                error = true;
                            }
                            else
                            {
                                if (repositoryPathOs.Contains("devpack") || !useCache)
                                {
                                    if (extendedLogging) appLogger.LogCritical("    -> processing");
                                }
                                _markGitTagContentInNode(nodeGitRepositoryEditor, repositoryPathOs);

                                var editorId = GetAttribute(nodeGitRepositoryEditor, "id");
                                var editorVersion = GetAttribute(nodeGitRepositoryEditor, "version");
                                if (!string.IsNullOrEmpty(editorId) && !string.IsNullOrEmpty(editorVersion))
                                {
                                    var newEditorNode = xmlCacheDocument.CreateElement("editor");
                                    SetAttribute(newEditorNode, "id", editorId);
                                    SetAttribute(newEditorNode, "version", editorVersion);
                                    nodeEditorsRoot.AppendChild(newEditorNode);
                                }
                                else
                                {
                                    if (extendedLogging) appLogger.LogCritical("    -> skipped");
                                }
                            }
                        }
                    }
                }

                // Store the version information on the disk so that we can include it when we do not want to retrieve it on runtime
                if (debugRoutine) await xmlCacheDocument.SaveAsync(repositoriesCachePathOs);





                // Set the version information in the application configuration

                // A) For the repositories of the application
                foreach (XmlNode nodeRepository in xmlCacheDocument.SelectNodes("/repros/repositories/repository"))
                {
                    var repositoryId = GetAttribute(nodeRepository, "id");
                    var repositoryVersion = GetAttribute(nodeRepository, "version");
                    if (!string.IsNullOrEmpty(repositoryId) && !string.IsNullOrEmpty(repositoryVersion))
                    {
                        var nodeLocation = xmlApplicationConfiguration.SelectSingleNode($"/configuration/repositories/*/repro//location[@id='{repositoryId}']");
                        if (nodeLocation == null)
                        {
                            if (!repositoryId.Contains("website")) appLogger.LogWarning($"Could not locate repository location with ID '{repositoryId}'");
                            // await xmlApplicationConfiguration.SaveAsync(logRootPathOs + "/---appconfig.xml", false, true);
                        }
                        else
                        {
                            nodeLocation.SetAttribute("version", repositoryVersion);
                        }
                    }
                }

                // B) For the repositories of the editors
                foreach (XmlNode nodeRepository in xmlCacheDocument.SelectNodes("/repros/editors/editor"))
                {
                    var editorId = GetAttribute(nodeRepository, "id");
                    var editorVersion = GetAttribute(nodeRepository, "version");
                    if (!string.IsNullOrEmpty(editorId) && !string.IsNullOrEmpty(editorVersion))
                    {
                        var nodeEditor = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id='{editorId}']");
                        if (nodeEditor == null)
                        {
                            appLogger.LogWarning($"Could not locate editor with ID '{editorId}'");
                        }
                        else
                        {
                            SetAttribute(nodeEditor, "version", editorVersion);
                        }
                    }
                }


                // Set the version of the application using the version attribute that was set on the location of the root node
                applicationVersion = RetrieveAttributeValueIfExists("/configuration/repositories//location[@id='application-root']/@version", xmlApplicationConfiguration);
            }
            catch (Exception ex)
            {
                WriteErrorMessageToConsole("RetrieveTaxxorGitRepositoriesVersionInfo() failed", ex.ToString());
                error = true;
            }


            return !error;


            void LogRepositoryProcessing(string repositoryName, string repositoryPathOs)
            {
                if (extendedLogging)
                {
                    if (string.IsNullOrEmpty(repositoryPathOs))
                    {
                        appLogger.LogCritical("** repository ('{0}') git tag for path {1} **", repositoryName, repositoryPathOs ?? "unknown");
                    }
                    else
                    {
                        if (repositoryPathOs.Contains("devpack"))
                        {
                            appLogger.LogCritical("** devpackage ('{0}') git tag for path {1} **", repositoryName, repositoryPathOs);
                        }
                        else
                        {
                            appLogger.LogCritical("** regular ('{0}') git tag for path {1} **", repositoryName, repositoryPathOs);
                        }
                    }

                }
            }
        }

        /// <summary>
        /// Fills the global field applicationVersion with the current version number of the application
        /// </summary>
        public static void RetrieveAppVersion()
        {
            var xPathVersionNode = "/configuration/repositories//repro[@id='root']/location[@id='application-root']/@version";
            var taxxorEditorVersion = RetrieveAttributeValueIfExists(xPathVersionNode, xmlApplicationConfiguration);

            if (!string.IsNullOrEmpty(taxxorEditorVersion))
            {
                applicationVersion = taxxorEditorVersion;
            }
            else
            {
                // Check on the disk if there is a .git folder in the root of the application and if so retrieve the latest version from GIT
                if (Directory.Exists(sitesRootPathOs + "/.git"))
                {
                    // Retrieve the latest tag from GIT
                    var latestGitTag = RetrieveLatestGitTag(sitesRootPathOs);
                    if (!string.IsNullOrEmpty(latestGitTag))
                    {
                        appLogger.LogInformation($"############ Retrieved latest GIT tag '{latestGitTag}' ############");
                        applicationVersion = latestGitTag;
                    }
                }

                if (string.IsNullOrEmpty(applicationVersion))
                {
                    // Failover in case we have not been able to obtain the application version, then we use a random string
                    var randomApplicationVersion = RandomString(16, false);
                    appLogger.LogError($"There was an error retrieving the application version. randomApplicationVersion: {randomApplicationVersion}, stack-trace: {GetStackTrace()}");
                    Taxxor.Project.ProjectLogic.applicationVersion = randomApplicationVersion;

                    // Obtain some information in an attempt to find out why we were unable to determine the application version
                    var nodeWebApplication = xmlApplicationConfiguration.SelectSingleNode("/configuration/taxxor/components/service/web-applications/web-application[@id='taxxoreditor']");
                    var nodeWebApplicationExists = (nodeWebApplication != null);
                    var nodeRootRepository = xmlApplicationConfiguration.SelectSingleNode("/configuration/taxxor/components/service/web-applications/web-application[@id='taxxoreditor']/meta/details/repositories/repro[@id='root']");
                    var nodeRootRepositoryExists = (nodeRootRepository != null);

                    var nodeDebug = xmlApplicationConfiguration.SelectSingleNode("/configuration/taxxor/components/service/web-applications/web-application[@id='taxxoreditor']/meta/details");
                    var repositoriesInfo = "--";
                    if (nodeDebug != null) repositoriesInfo = nodeDebug.OuterXml;

                    // Log the issue and save the XML file so that we can inspect it
                    xmlApplicationConfiguration.Save($"{logRootPathOs}/repository.xml");
                    appLogger.LogWarning($"Unable to update the application version variable, so we are using the previous version instead\napplicationVersion: {applicationVersion}\nrepositoriesInfo: {repositoriesInfo}\nnodeWebApplicationExists: {nodeWebApplicationExists.ToString()}\nnodeRootRepositoryExists: {nodeRootRepositoryExists.ToString()}\nstack-trace: {GetStackTrace()}");
                }
            }
        }

        /// <summary>
        /// Generates an array of all the languages used in a CMS project
        /// </summary>
        /// <param name="editorId"></param>
        /// <returns></returns>
        public static string[] RetrieveProjectLanguages(string editorId)
        {
            var languages = new List<string>();

            var nodeListVariants = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{editorId}']/output_channels/output_channel/variants/variant");
            foreach (XmlNode nodeVariant in nodeListVariants)
            {
                var currentLanguage = GetAttribute(nodeVariant, "lang");
                if (!string.IsNullOrEmpty(currentLanguage))
                {
                    if (!languages.Contains(currentLanguage)) languages.Add(currentLanguage);
                }
            }

            return languages.ToArray();
        }

        /// <summary>
        /// Renders an XmlDocument containing information about the languages used in this project
        /// </summary>
        /// <param name="editorId"></param>
        /// <returns></returns>
        public static XmlDocument RetrieveProjectLanguagesXml(string editorId, string outputChannelDefaultLanguage)
        {
            var xmlLanguages = new XmlDocument();
            xmlLanguages.LoadXml("<languages/>");

            var languages = RetrieveProjectLanguages(editorId);
            foreach (var lang in languages)
            {
                var nodeLang = xmlLanguages.CreateElement("lang");
                SetAttribute(nodeLang, "id", lang);
                if (lang == outputChannelDefaultLanguage) SetAttribute(nodeLang, "default", "true");
                xmlLanguages.DocumentElement.AppendChild(nodeLang);
            }

            return xmlLanguages;
        }

        /// <summary>
        /// Renders an xpath to be able to select an internal link
        /// </summary>
        /// <returns></returns>
        public static string RenderInternalLinkXpathSelector()
        {
            return "a[@data-link-type='section' or @data-link-type='note' or (not(@data-link-type) and contains(@href, '#') and not(starts-with(@href, 'http')))]";
        }

        /// <summary>
        /// Tests if an internal link is relevant to test if it's broken or not
        /// An internal link may be wrapped in a special class which may make the link irrelevant for the output channel
        /// </summary>
        /// <param name="nodeLink"></param>
        /// <param name="outputChannelId"></param>
        /// <returns></returns>
        public static bool IsInternalLinkRelevant(XmlNode nodeLink, string outputChannelId)
        {
            var projectType = "irrelevant";

            // TODO - this is really philips specific and we probably need to find a proper fix for this
            if (outputChannelId.Contains("20f"))
            {
                projectType = "20f";
            }
            else if (outputChannelId.StartsWith("ar"))
            {
                projectType = "ar";
            }
            else
            {
                return true;
            }

            // By default we need to test the link, but not if it's wrapped in an element that will be stripped or hidden from the output channel after generation
            var isRelevantLink = true;
            if (projectType == "20f")
            {
                // Test if this link is contained in a wrapper node containing the class .dataar
                if (nodeLink.SelectNodes("ancestor-or-self::*[contains(@class, 'dataar')]").Count > 0)
                {
                    isRelevantLink = false;
                }
            }
            else if (projectType == "ar")
            {
                // Test if this link is contained in a wrapper node containing the class .data20-f
                if (nodeLink.SelectNodes("ancestor-or-self::*[contains(@class, 'data20-f')]").Count > 0)
                {
                    isRelevantLink = false;
                }
            }

            return isRelevantLink;
        }

        /// <summary>
        /// Retrieves the textual content of the first header found in the xml data content file
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="renderWarningWhenNotFound"></param>
        /// <returns></returns>
        public static string? RetrieveFirstHeaderText(XmlDocument xmlDoc, bool renderWarningWhenNotFound = true)
        {
            var nodeHeader = RetrieveFirstHeaderNode(xmlDoc, renderWarningWhenNotFound, true);
            if (nodeHeader != null)
            {
                return nodeHeader.InnerText;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves the textual content of the first header found in the xml data content file
        /// </summary>
        /// <param name="nodeArticle"></param>
        /// <param name="renderWarningWhenNotFound"></param>
        /// <returns></returns>
        public static string? RetrieveFirstHeaderText(XmlNode nodeArticle, bool renderWarningWhenNotFound = true)
        {
            var nodeHeader = RetrieveFirstHeaderNode(nodeArticle, renderWarningWhenNotFound, true);
            if (nodeHeader != null)
            {
                return nodeHeader.InnerText;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves the first header node (h1, h2, ...) of an xml document
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="renderWarningWhenNotFound"></param>
        /// <returns></returns>
        public static XmlNode? RetrieveFirstHeaderNode(XmlDocument xmlDoc, bool renderWarningWhenNotFound = true)
        {
            return RetrieveFirstHeaderNode(xmlDoc.DocumentElement, renderWarningWhenNotFound);
        }

        /// <summary>
        /// Retrieves the first header node (h1, h2, ...) of an article
        /// </summary>
        /// <param name="nodeArticle"></param>
        /// <param name="renderWarningWhenNotFound"></param>
        /// <returns></returns>
        public static XmlNode? RetrieveFirstHeaderNode(XmlNode nodeArticle, bool renderWarningWhenNotFound = true, bool onlySelectNonEmptyHeaders = false)
        {
            var whitespaceSelector = (onlySelectNonEmptyHeaders) ? @"[translate(text(), '  &#13;&#10;&#09;&#xa;&#x0;&#160;', '') != '' or count(*)>0]" : "";
            var nodeHeader = nodeArticle.SelectSingleNode($"descendant::h1{whitespaceSelector}");
            if (nodeHeader != null)
            {
                return nodeHeader;
            }
            else
            {
                nodeHeader = nodeArticle.SelectSingleNode($"descendant::h2");
                if (nodeHeader != null)
                {
                    return nodeHeader;
                }
                else
                {
                    nodeHeader = nodeArticle.SelectSingleNode($"descendant::h3");
                    if (nodeHeader != null)
                    {
                        return nodeHeader;
                    }
                    else
                    {
                        nodeHeader = nodeArticle.SelectSingleNode($"descendant::h4");
                        if (nodeHeader != null)
                        {
                            return nodeHeader;
                        }
                        else
                        {
                            nodeHeader = nodeArticle.SelectSingleNode($"descendant::h5");
                            if (nodeHeader != null)
                            {
                                return nodeHeader;
                            }
                            else
                            {
                                nodeHeader = nodeArticle.SelectSingleNode($"descendant::h6");
                                if (nodeHeader != null)
                                {
                                    return nodeHeader;
                                }
                                else
                                {
                                    nodeHeader = nodeArticle.SelectSingleNode($"descendant::h7");
                                    if (nodeHeader != null)
                                    {
                                        return nodeHeader;
                                    }
                                    else
                                    {
                                        if (renderWarningWhenNotFound)
                                        {

                                            ProjectVariables projectVars = RetrieveProjectVariables(System.Web.Context.Current);
                                            var nodeName = nodeArticle.LocalName;
                                            var debugInfo = $"projectId: {projectVars.projectId}, articleId: {nodeArticle.GetAttribute("id") ?? "unknown"}, articleType: {nodeArticle.GetAttribute("data-articletype") ?? "unknown"}";

                                            switch (nodeName)
                                            {

                                                default:
                                                    debugInfo += $", nodeName: {nodeName} (";
                                                    try
                                                    {
                                                        foreach (XmlAttribute attr in nodeArticle.Attributes)
                                                        {
                                                            debugInfo += $"{attr.Name}: {attr.Value}, ";
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        appLogger.LogWarning(ex.Message);
                                                    }
                                                    debugInfo += ")";
                                                    break;
                                            }


                                            appLogger.LogWarning($"Unable to find a header node to return. {debugInfo}");
                                        }

                                        return null;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Returns hierarchy item core information based upon the hierarchy-overview XML document
        /// </summary>
        /// <param name="xmlHierarchyOverview">The hierarchy overview document containing all the hierarchies that have been configured for a project</param>
        /// <param name="projectVars">ProjectVariables object</param>
        /// <param name="selector">Site structure ID or data reference (usually XML file name)</param>
        /// <param name="defaultValue">Default value for the tuple elements to return</param>
        /// <returns></returns>
        public static SiteStructureItem RetrieveHierarchyItemInformation(XmlDocument xmlHierarchyOverview, ProjectVariables projectVars, string selector, string defaultValue = "")
        {

            var itemSelector = (selector.EndsWith(".xml")) ? $"[@data-ref='{selector}']" : $"[@id='{selector}']";
            var xPathForHierarchy = $"/hierarchies/output_channel[@lang='{projectVars.outputChannelVariantLanguage}']/items/structured//item{itemSelector}";
            var nodeItem = xmlHierarchyOverview.SelectSingleNode(xPathForHierarchy);
            if (nodeItem == null)
            {
                xPathForHierarchy = $"/hierarchies/output_channel/items/structured//item{itemSelector}";
            }

            // if (nodeItem == null && siteType == "local")
            // {
            //     Console.WriteLine($"** xPathForHierarchy: {xPathForHierarchy} retrieved no results **");
            // }

            return RetrieveHierarchyItemInformation(nodeItem, defaultValue);
        }

        /// <summary>
        /// Returns hierarchy item core information based upon the hierarchy XML document
        /// </summary>
        /// <param name="xmlHierarchy">Output channel hierarchy</param>
        /// <param name="selector">Site structure ID or data reference (usually XML file name)</param>
        /// <param name="defaultValue">Default value for the tuple elements to return</param>
        /// <returns></returns>
        public static SiteStructureItem RetrieveHierarchyItemInformation(XmlDocument xmlHierarchy, string selector, string defaultValue = "")
        {
            var itemSelector = (selector.EndsWith(".xml")) ? $"[@data-ref='{selector}']" : $"[@id='{selector}']";
            var nodeItem = xmlHierarchy.SelectSingleNode($"//items/structured//item{itemSelector}");

            return RetrieveHierarchyItemInformation(nodeItem, defaultValue);
        }

        /// <summary>
        /// Returns hierarchy item core information based upon the hierarchy XML document
        /// </summary>
        /// <param name="nodeItem">Hierarchy node</param>
        /// <param name="defaultValue">Default value for the tuple elements to return</param>
        /// <returns></returns>
        public static SiteStructureItem RetrieveHierarchyItemInformation(XmlNode? nodeItem, string defaultValue = "")
        {
            var siteStructureId = nodeItem?.GetAttribute("id") ?? defaultValue;
            var dataReference = nodeItem?.GetAttribute("data-ref") ?? defaultValue;
            var linkname = nodeItem?.SelectSingleNode("web_page/linkname")?.InnerText ?? defaultValue;

            return new SiteStructureItem(siteStructureId, linkname, dataReference);
        }



        /// <summary>
        /// Use the metadata cache file to retrieve the section title of a data reference
        /// </summary>
        /// <param name="dataRef"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static string RetrieveSectionTitleFromMetadataCache(XmlDocument xmlCmsContentMetadata, string dataRef, ProjectVariables projectVars)
        {
            return RetrieveSectionTitleFromMetadataCache(xmlCmsContentMetadata, dataRef, projectVars.projectId, projectVars.outputChannelVariantLanguage);
        }

        /// <summary>
        /// Use the metadata cache file to retrieve the section title of a data reference
        /// </summary>
        /// <param name="dataRef"></param>
        /// <param name="projectId"></param>
        /// <param name="outputChannelVariantLanguage"></param>
        /// <returns></returns>
        public static string? RetrieveSectionTitleFromMetadataCache(XmlDocument xmlCmsContentMetadata, string dataRef, string projectId, string outputChannelVariantLanguage)
        {
            // Try to retrieve the article title from the metadata document
            if (dataRef == "_footnotes.xml") return "Footnotes data store";
            var xPath = $"/projects/cms_project[@id='{projectId}']/data/content[@ref='{dataRef}' and @datatype='sectiondata']";
            var nodeMetadataContent = xmlCmsContentMetadata.SelectSingleNode(xPath);

            if (nodeMetadataContent == null)
            {
                appLogger.LogWarning($"Unable to find metadata entry. xPath: {xPath}, stack-trace: {GetStackTrace()}");
                return null;
            }

            return RetrieveSectionTitleFromMetadataCache(nodeMetadataContent, outputChannelVariantLanguage);
        }

        /// <summary>
        /// Use the metadata cache file to retrieve the section title of a data reference
        /// </summary>
        /// <param name="xmlCmsContentMetadata"></param>
        /// <param name="dataRef"></param>
        /// <param name="outputChannelVariantLanguage"></param>
        /// <returns></returns>
        public static string? RetrieveSectionTitleFromMetadataCache(XmlDocument xmlCmsContentMetadata, string dataRef, string outputChannelVariantLanguage)
        {
            if (dataRef == "_footnotes.xml") return "Footnotes data store";
            var xPath = $"//data/content[@ref='{dataRef}' and @datatype='sectiondata']";
            var nodeMetadataContent = xmlCmsContentMetadata.SelectSingleNode(xPath);

            if (nodeMetadataContent == null)
            {
                appLogger.LogWarning($"Unable to find metadata entry. xPath: {xPath}, stack-trace: {GetStackTrace()}");
                return null;
            }

            return RetrieveSectionTitleFromMetadataCache(nodeMetadataContent, outputChannelVariantLanguage);
        }

        /// <summary>
        /// Use the metadata cache file to retrieve the section title of a data reference
        /// </summary>
        /// <param name="nodeMetadataContent"></param>
        /// <param name="outputChannelVariantLanguage"></param>
        /// <returns></returns>
        public static string? RetrieveSectionTitleFromMetadataCache(XmlNode? nodeMetadataContent, string outputChannelVariantLanguage)
        {
            string? sectionTitle = null;

            if (nodeMetadataContent != null)
            {
                var articleLanguages = nodeMetadataContent.SelectSingleNode("metadata/entry[@key='languages']")?.InnerText ?? "";
                var articleTitles = nodeMetadataContent.SelectSingleNode("metadata/entry[@key='titles']")?.InnerText;
                if (articleLanguages.Contains(","))
                {
                    var languages = articleLanguages.Split(',');
                    var linknames = articleTitles.Split("||||");
                    for (int i = 0; i < languages.Length; i++)
                    {
                        var lang = languages[i];
                        if ((lang == outputChannelVariantLanguage || string.IsNullOrEmpty(outputChannelVariantLanguage) && string.IsNullOrEmpty(sectionTitle)))
                        {
                            try
                            {
                                sectionTitle = linknames[i];
                            }
                            catch (Exception) { }
                        }
                    }
                }
                else
                {
                    sectionTitle = articleTitles;
                }

                if (sectionTitle == null)
                {
                    appLogger.LogWarning($"Unable to resolve the article title via the metadata content. dataReference: {(nodeMetadataContent.GetAttribute("ref") ?? "unknown")}, outputChannelVariantLanguage: {outputChannelVariantLanguage}, stack-trace: {GetStackTrace()}");
                    return null;
                }
            }
            else
            {
                appLogger.LogWarning($"Could not find an entry in the metadatacache for retrieving the article title. dataReference: {(nodeMetadataContent.GetAttribute("ref") ?? "unknown")}, outputChannelVariantLanguage: {outputChannelVariantLanguage}, stack-trace: {GetStackTrace()}");
            }

            return sectionTitle;
        }

        /// <summary>
        /// Retrieve the section article ID using the metadata cache and the content language
        /// </summary>
        /// <param name="xmlCmsContentMetadata"></param>
        /// <param name="dataRef"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static string? RetrieveArticleIdFromMetadataCache(XmlDocument xmlCmsContentMetadata, string dataRef, ProjectVariables projectVars)
        {
            return RetrieveArticleIdFromMetadataCache(xmlCmsContentMetadata, dataRef, projectVars.projectId, projectVars.outputChannelVariantLanguage);
        }

        /// <summary>
        /// Retrieve the section article ID using the metadata cache and the content language
        /// </summary>
        /// <param name="xmlCmsContentMetadata"></param>
        /// <param name="dataRef"></param>
        /// <param name="projectId"></param>
        /// <param name="outputChannelVariantLanguage"></param>
        /// <returns></returns>
        public static string? RetrieveArticleIdFromMetadataCache(XmlDocument xmlCmsContentMetadata, string dataRef, string projectId, string outputChannelVariantLanguage)
        {
            // Try to retrieve the article title from the metadata document
            var nodeMetadataContent = xmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']/data/content[@ref='{dataRef}' and @datatype='sectiondata']");

            return RetrieveArticleIdFromMetadataCache(nodeMetadataContent, outputChannelVariantLanguage);
        }

        /// <summary>
        /// Retrieve the section article ID using the metadata cache and the content language
        /// </summary>
        /// <param name="nodeMetadataContent"></param>
        /// <param name="outputChannelVariantLanguage"></param>
        /// <returns></returns>
        public static string? RetrieveArticleIdFromMetadataCache(XmlNode? nodeMetadataContent, string outputChannelVariantLanguage)
        {
            string? articleId = null;

            if (nodeMetadataContent != null)
            {
                var articleLanguages = nodeMetadataContent.SelectSingleNode("metadata/entry[@key='languages']")?.InnerText ?? "";
                var articleIds = nodeMetadataContent.SelectSingleNode("metadata/entry[@key='ids']")?.InnerText;
                if (articleLanguages.Contains(","))
                {
                    var languages = articleLanguages.Split(',');
                    var ids = articleIds.Split(',');
                    for (int i = 0; i < languages.Length; i++)
                    {
                        var lang = languages[i];
                        if (lang == outputChannelVariantLanguage || string.IsNullOrEmpty(outputChannelVariantLanguage))
                        {
                            try
                            {
                                articleId = ids[i];
                            }
                            catch (Exception) { }
                        }
                    }
                }
                else
                {
                    articleId = articleIds;
                }

                if (articleId == null)
                {
                    appLogger.LogWarning($"Unable to resolve the article id via the metadata content. dataReference: {(nodeMetadataContent.GetAttribute("ref") ?? "unknown")}, stack-trace: {GetStackTrace()}");
                    return null;
                }
            }
            else
            {
                appLogger.LogWarning($"Could not find an entry in the metadatacache for retrieving the article id. dataReference: {(nodeMetadataContent.GetAttribute("ref") ?? "unknown")}, outputChannelVariantLanguage: {outputChannelVariantLanguage}, stack-trace: {GetStackTrace()}");
            }

            return articleId;
        }


        /// <summary>
        /// Checks if a section data reference XML file is in use or not
        /// </summary>
        /// <param name="xmlCmsContentMetadata"></param>
        /// <param name="projectIdToSync"></param>
        /// <param name="dataReferencePath"></param>
        /// <returns></returns>
        public static bool IsSectionDataFileInUse(XmlDocument xmlCmsContentMetadata, string projectIdToSync, string dataReferencePath)
        {
            var dataReference = (dataReferencePath.Contains("/")) ? Path.GetFileName(dataReferencePath) : dataReferencePath;
            var xPathForFileInUse = $"/projects/cms_project[@id='{projectIdToSync}']/data/content[@ref='{Path.GetFileName(dataReference)}' and @datatype='sectiondata']/metadata/entry[@key='inuse' and text()='true']";
            return (xmlCmsContentMetadata.SelectNodes(xPathForFileInUse).Count > 0);
        }

        /// <summary>
        /// Returns the dafault layout used for rendering an output channel
        /// </summary>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static (string Layout, bool Forced) RetrieveOutputChannelDefaultLayout(ProjectVariables projectVars)
        {
            return RetrieveOutputChannelDefaultLayout(projectVars.projectId, projectVars.outputChannelVariantId, projectVars.editorId, projectVars.outputChannelType);
        }

        /// <summary>
        /// Returns the dafault layout used for rendering an output channel
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="outputChannelVariantId"></param>
        /// <param name="editorId"></param>
        /// <param name="outputChannelType"></param>
        /// <returns></returns>
        public static (string Layout, bool Forced) RetrieveOutputChannelDefaultLayout(string projectId, string outputChannelVariantId, string? editorId = null, string? outputChannelType = null)
        {
            var defaultLayout = "regular";
            var forcedLayout = false;

            outputChannelType ??= RetrieveOutputChannelTypeFromOutputChannelVariantId(projectId, outputChannelVariantId);
            editorId ??= RetrieveEditorIdFromProjectId(projectId);

            var nodeOutputChannel = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id='{editorId}']/output_channels/output_channel[@type='{outputChannelType}']");
            if (nodeOutputChannel == null)
            {
                appLogger.LogWarning($"Unable to find output channel information. editorId: {editorId}, outputChannelType: {outputChannelType}");
                return (defaultLayout, forcedLayout);
            }
            var nodeVariant = nodeOutputChannel.SelectSingleNode($"variants/variant[@id='{outputChannelVariantId}']");
            if (nodeVariant == null)
            {
                appLogger.LogWarning($"Unable to find output channel variant information. outputChannelVariantId: {outputChannelVariantId}");
                return (defaultLayout, forcedLayout);
            }

            if (nodeVariant.HasAttribute("defaultlayout"))
            {
                // appLogger.LogInformation($"!!! Found default layout for {outputChannelVariantId}");
                defaultLayout = nodeVariant.GetAttribute("defaultlayout");
            }

            if (nodeVariant.HasAttribute("forcedlayout"))
            {
                forcedLayout = (nodeVariant.GetAttribute("forcedlayout") == "true");
            }

            return (defaultLayout, forcedLayout);
        }



        /// <summary>
        /// Helper routine for finding information to be used in a commit message for the versioning system
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="xmlSectionData"></param>
        /// <param name="dataReference"></param>
        /// <param name="xmlHierarchy"></param>
        /// <returns></returns>
        public static SiteStructureItem RetrieveCommitInformation(string projectId, XmlDocument xmlSectionData, string dataReference, XmlDocument xmlHierarchy, XmlDocument xmlCmsContentMetadata)
        {

            // This always works as a fallback
            var articleHeader = RetrieveFirstHeaderText(xmlSectionData, false);
            var fileReference = (dataReference.Contains("/")) ? Path.GetFileName(dataReference) : dataReference;
            var linkName = articleHeader;
            var identifier = fileReference;

            // Retrieve information from the hierarchy
            var dataFileInUse = IsSectionDataFileInUse(xmlCmsContentMetadata, projectId, fileReference);
            if (dataFileInUse)
            {
                // Retrieve information about this data reference file
                SiteStructureItem itemInformation = RetrieveHierarchyItemInformation(xmlHierarchy, fileReference);
                linkName = itemInformation.Linkname;
                identifier = itemInformation.Id;
            }

            if (string.IsNullOrEmpty(linkName))
            {
                // Use the filename of the data reference as a linkname
                linkName = Path.GetFileNameWithoutExtension(fileReference);
                appLogger.LogWarning($"Unable to find linkname for {fileReference} in project {projectId}, using section data file reference as the linkname");
            }

            return new SiteStructureItem(identifier, linkName, fileReference);
        }

        /// <summary>
        /// Central utility to calculate the xPath pointing to the filestructure definition section in the configuration
        /// </summary>
        /// <param name="editorId"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage RetrieveFileStructureXpath(string editorId)
        {
            var websiteAssetsBaseXpath = $"/configuration/editors/editor[@id={GenerateEscapedXPathString(editorId)}]/output_channels/output_channel[@type='website']/filestructure/root/folder";

            // Gracefully exit the routine when no website has been configured
            if (xmlApplicationConfiguration.SelectSingleNode(websiteAssetsBaseXpath) == null)
            {
                // Failover to a generic definition that we have defined in the base configuration of the Taxxor Editor
                websiteAssetsBaseXpath = $"/configuration/general/filestructure/root/folder";

                if (xmlApplicationConfiguration.SelectSingleNode(websiteAssetsBaseXpath) == null) return new TaxxorReturnMessage(true, "Unable to find a definition for the assets filestructure", $"websiteAssetsBaseXpath: {websiteAssetsBaseXpath}");
            }

            return new TaxxorReturnMessage(true, "Found filestructure definition", websiteAssetsBaseXpath, "");
        }

        /// <summary>
        /// Renders the base xpath to select SVG assets from the content
        /// </summary>
        /// <returns></returns>
        public static string RetrieveSvgElementsBaseXpath()
        {
            return "*[(local-name()='object' and @type='image/svg+xml') or (local-name()='img' and (contains(@src, '.svg') or contains(@src, '.SVG')))]";
        }

        /// <summary>
        /// Renders a base xpath for finding the wrapper elements of graph elements in the content
        /// </summary>
        /// <param name="onlyEcharts"></param>
        /// <param name="excludeHidden"></param>
        /// <returns></returns>
        public static string RetrieveGraphElementsBaseXpath(bool onlyEcharts = false, bool excludeHidden = false)
        {
            var hiddenCheck = excludeHidden ? " and not(contains(@class, 'hide'))" : "";
            if (onlyEcharts)
            {
                return $"//div[div/@class='tx-renderedchart' and table{hiddenCheck}]";
            }
            return $"//div[(div/@class='chart-content' or div/@class='tx-renderedchart') and table{hiddenCheck}]";
        }


        /// <summary>
        /// Renders an overview of the SDE's in a data-reference and sorts them by XBRL level
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        public static XmlDocument RenderStructuredDataElementOverview(XmlDocument xmlDoc)
        {
            var xmlToReturn = new XmlDocument();
            xmlToReturn.AppendChild(xmlToReturn.CreateElement("result"));

            var nodeLevelZero = xmlToReturn.CreateElement("facts");
            nodeLevelZero.SetAttribute("level", "0");
            xmlToReturn.DocumentElement.AppendChild(nodeLevelZero);

            var nodeLevelOne = xmlToReturn.CreateElement("facts");
            nodeLevelOne.SetAttribute("level", "1");
            xmlToReturn.DocumentElement.AppendChild(nodeLevelOne);

            var nodeLevelTwo = xmlToReturn.CreateElement("facts");
            nodeLevelTwo.SetAttribute("level", "2");
            xmlToReturn.DocumentElement.AppendChild(nodeLevelTwo);

            var nodeLevelThree = xmlToReturn.CreateElement("facts");
            nodeLevelThree.SetAttribute("level", "3");
            xmlToReturn.DocumentElement.AppendChild(nodeLevelThree);

            var nodeLevelFour = xmlToReturn.CreateElement("facts");
            nodeLevelFour.SetAttribute("level", "4");
            xmlToReturn.DocumentElement.AppendChild(nodeLevelFour);

            var nodeLevelUndefined = xmlToReturn.CreateElement("facts");
            nodeLevelUndefined.SetAttribute("level", "undefined");
            xmlToReturn.DocumentElement.AppendChild(nodeLevelUndefined);

            // Query the document and append matches to the result document
            var xPath = "//*[(local-name()='span' or local-name()='small' or local-name()='div' or local-name()='article' or local-name()='p') and @data-fact-id]";
            xPath = "//*[@data-fact-id]";
            var nodeListFacts = xmlDoc.SelectNodes(xPath);

            // Inject the facts into the result tree
            var countDefined = 0;
            var countUndefined = 0;
            foreach (XmlNode nodeFact in nodeListFacts)
            {
                var classNames = GetAttribute(nodeFact, "class") ?? "";
                var factId = GetAttribute(nodeFact, "data-fact-id") ?? "";
                XmlNode? nodeImported = null;
                //var tagLevel = "-1";
                if (classNames.Contains("xbrl-level-0"))
                {
                    nodeImported = xmlToReturn.ImportNode(nodeFact, true);
                    nodeLevelZero.AppendChild(nodeImported);
                    countDefined++;
                }
                else if (classNames.Contains("xbrl-level-1"))
                {
                    nodeImported = xmlToReturn.ImportNode(nodeFact, false);
                    nodeLevelOne.AppendChild(nodeImported);
                    countDefined++;
                }
                else if (classNames.Contains("xbrl-level-2"))
                {
                    nodeImported = xmlToReturn.ImportNode(nodeFact, false);
                    nodeLevelTwo.AppendChild(nodeImported);
                    countDefined++;
                }
                else if (classNames.Contains("xbrl-level-3"))
                {
                    nodeImported = xmlToReturn.ImportNode(nodeFact, false);
                    nodeLevelThree.AppendChild(nodeImported);
                    countDefined++;
                }
                else if (classNames.Contains("xbrl-level-4") || factId.Contains("Cell_"))
                {
                    nodeImported = xmlToReturn.ImportNode(nodeFact, true);
                    nodeLevelFour.AppendChild(nodeImported);
                    countDefined++;
                }
                else
                {
                    nodeImported = xmlToReturn.ImportNode(nodeFact, false);
                    nodeLevelUndefined.AppendChild(nodeImported);
                    countUndefined++;
                }

            }

            SetAttribute(xmlToReturn.DocumentElement, "counttotal", nodeListFacts.Count.ToString());
            SetAttribute(xmlToReturn.DocumentElement, "countdefined", countDefined.ToString());
            SetAttribute(xmlToReturn.DocumentElement, "countundefined", countUndefined.ToString());

            return xmlToReturn;



        }

        /// <summary>
        /// Removes elements from the content that are sensitive to XSS injection
        /// </summary>
        /// <param name="xmlDoc"></param>
        public static void RemoveXssSensitiveElements(ref XmlDocument xmlDoc)
        {
            string[] nodeNamesToRemove = ["script", "style", "link", "noembed", "textarea", "noscript", "iframe", "xmp", "noframes", "video", "form"];

            for (int i = 0; i < nodeNamesToRemove.Length; i++)
            {
                RemoveXmlNodes(xmlDoc.SelectNodes($"//{nodeNamesToRemove[i]}"));
            }

            // - Remove input fields if they are not checkboxes
            RemoveXmlNodes(xmlDoc.SelectNodes("//input[not(@type='checkbox')]"));

            // - Image tags may only contain an src that points to a local file
            RemoveXmlNodes(xmlDoc.SelectNodes("//img[contains(@src, 'http://') or contains(@src, 'https://')]"));

            // - Object tags may not contain script in data tags
            RemoveXmlNodes(xmlDoc.SelectNodes("//object[contains(@data, 'script') or contains(@data, 'java')]"));

            // - Javscript event handlers
            var nodeListEventHandlers = xmlDoc.SelectNodes("//@*[starts-with(name(), 'on')]");
            foreach (XmlAttribute attrEventHandler in nodeListEventHandlers)
            {
                attrEventHandler.Remove();
            }
        }

        /// <summary>
        /// Removes comment system wrapper nodes from an XmlNode
        /// </summary>
        /// <param name="node"></param>
        public static void RemoveCommentWrappers(ref XmlNode node)
        {
            if (node.InnerXml.Contains("tx-comment"))
            {
                var nodelistCommentWrappers = node.SelectNodes("//span[@class='tx-comment']");
                foreach (XmlNode nodeCommentWrapper in nodelistCommentWrappers)
                {
                    XmlNode? parentElement = nodeCommentWrapper?.ParentNode;

                    // Make sure both nodes are found
                    if (nodeCommentWrapper != null && parentElement != null)
                    {
                        // Append the text content of the <span> node to the <h1> node's InnerXml
                        parentElement.InnerXml = parentElement.InnerXml.Replace(nodeCommentWrapper.OuterXml, nodeCommentWrapper.InnerXml);
                    }
                }
            }
        }

        /// <summary>
        /// Test the status of the connected Taxxor Web Services
        /// </summary>
        /// <returns></returns>
        public static TaxxorServiceStateReturnMessage CheckStatusTaxxorServices()
        {
            var nodeListServicesWithIssues = xmlApplicationConfiguration.SelectNodes("/configuration/taxxor/components/service/services/service[not(@status='OK' or @status='Not checked' or @status='')]");
            if (nodeListServicesWithIssues.Count > 0)
            {
                // Determine the alert that we need to show
                var errorServiceIds = new List<string>();
                var showCriticalAlert = false;
                foreach (XmlNode nodeService in nodeListServicesWithIssues)
                {
                    var serviceRole = GetAttribute(nodeService, "role") ?? "default";
                    if (serviceRole == "core") showCriticalAlert = true;
                    var serviceId = nodeService.GetAttribute("id");
                    try
                    {
                        errorServiceIds.Add((!string.IsNullOrEmpty(serviceId) ? serviceId : "unknown"));
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, "Could not add the ID of the service that failed");
                    }

                }
                return new TaxxorServiceStateReturnMessage(false, showCriticalAlert, errorServiceIds);

            }
            else
            {
                return new TaxxorServiceStateReturnMessage(true);
            }
        }


        /// <summary>
        /// Returns details of the state of the connected Taxxor (web) Services
        /// </summary>
        public class TaxxorServiceStateReturnMessage
        {
            public bool Success { get; set; }

            public bool CriticalError { get; set; } = false;

            public List<string>? ErrorServiceIds { get; set; }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="success"></param>
            /// <param name="criticalError"></param>
            /// <param name="errorServiceIds"></param>
            public TaxxorServiceStateReturnMessage(bool success, bool criticalError = false, List<string>? errorServiceIds = null)
            {
                this.Success = success;
                this.CriticalError = criticalError;
                this.ErrorServiceIds = errorServiceIds;
            }
        }

    }
}