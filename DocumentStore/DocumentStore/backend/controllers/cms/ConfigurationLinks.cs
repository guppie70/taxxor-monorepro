using System;
using System.IO;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Resolves the Configuration Links
        /// </summary>
        /// <param name="xmlSourceDocument"></param>
        /// <param name="projectVars"></param>
        /// <param name="dataFolderPathOs"></param>
        /// <returns></returns>
        public static XmlDocument ResolveConfigurationLinks(XmlDocument xmlSourceDocument, ProjectVariables projectVars, string dataFolderPathOs)
        {
            // Global variables
            string? lang = projectVars.outputChannelVariantLanguage;
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Check if this is a regular article or a website data file
            var xpathConfigurationLink = $"/data/content[@lang='{lang}']/*//*[@data-configurationlink]";

            // A) Check for links to elements
            var nodeListElementSourceLinks = xmlSourceDocument.SelectNodes(xpathConfigurationLink);
            foreach (XmlNode nodeElementSourceLink in nodeListElementSourceLinks)
            {
                var linkStatus = "ok";
                var reference = GetAttribute(nodeElementSourceLink, "data-configurationlink");

                var referenceElements = ParseConfigurationReference(reference);
                if (referenceElements.Item1 == "" || referenceElements.Item2 == "")
                {
                    appLogger.LogWarning($"Could not parse configuration link: {reference} linked from page with header '{RetrieveDocumentTitle(xmlSourceDocument)}'");
                    linkStatus = "wrong-format";
                }
                else
                {
                    var configurationKeyword = referenceElements.Item1;
                    var configurationXpath = referenceElements.Item2;
                    var customOperation = referenceElements.Item3;
                    if (debugRoutine) appLogger.LogInformation($"configurationKeyword: {configurationKeyword}, configurationXpath: {configurationXpath}, customOperation: {customOperation}");

                    var xPath = "";
                    switch (configurationKeyword)
                    {
                        case "cms_project":
                            xPath = $"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']";
                            break;
                        default:
                            appLogger.LogError($"No routine for handling configuration link with keyword {configurationKeyword} linked from page with header '{RetrieveDocumentTitle(xmlSourceDocument)}'");
                            linkStatus = "target-configkeyword-not-available";
                            break;
                    }

                    if (linkStatus == "ok")
                    {
                        var nodeBase = xmlApplicationConfiguration.SelectSingleNode(xPath);
                        if (nodeBase != null)
                        {
                            var fragmentToInsert = "";
                            if (configurationXpath.Contains("@"))
                            {
                                fragmentToInsert = RetrieveAttributeValueIfExists(configurationXpath, nodeBase);

                            }
                            else
                            {

                            }
                            if (string.IsNullOrEmpty(fragmentToInsert))
                            {
                                appLogger.LogError($"Could not find value of the configuration link to insert linked from page with header '{RetrieveDocumentTitle(xmlSourceDocument)}'");
                                fragmentToInsert = "";
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(customOperation))
                                {
                                    // Run the value through a custom formatter
                                    var baseMethod = "";
                                    var methodArgument = "";
                                    if (customOperation.Contains(":"))
                                    {
                                        string[] elements = customOperation.Split(':');
                                        baseMethod = elements[0];
                                        methodArgument = elements[1];
                                    }
                                    else
                                    {
                                        baseMethod = customOperation;
                                    }

                                    switch (baseMethod)
                                    {
                                        case "dateformat":
                                            // Format the date
                                            try
                                            {
                                                var parsedDate = DateTime.Parse(fragmentToInsert);
                                                fragmentToInsert = parsedDate.ToString(methodArgument);
                                            }
                                            catch (Exception ex)
                                            {
                                                appLogger.LogError(ex, $"Could not convert {fragmentToInsert} to a datetime object linked from page with header '{RetrieveDocumentTitle(xmlSourceDocument)}'");
                                            }

                                            break;
                                        case "writtendateformat":
                                            // Format the date
                                            try
                                            {
                                                var parsedDate = DateTime.Parse(fragmentToInsert);
                                                var day = AddOrdinal(parsedDate.Day);
                                                fragmentToInsert = $"{day} day of {parsedDate.ToString("MMMM yyyy")}";
                                            }
                                            catch (Exception ex)
                                            {
                                                appLogger.LogError(ex, $"Could not convert {fragmentToInsert} to a datetime object linked from page with header '{RetrieveDocumentTitle(xmlSourceDocument)}'");
                                            }

                                            break;
                                        default:
                                            // Attempt to format the string using a method defined in the Extensions class
                                            var customMethodResult = Extensions.CustomConfigurationLinkMethod(fragmentToInsert, baseMethod, methodArgument);
                                            if (customMethodResult.Success)
                                            {
                                                fragmentToInsert = customMethodResult.Payload;
                                            }
                                            else
                                            {
                                                appLogger.LogError($"No routine for handling configuration parse method with methodname {baseMethod} linked from page with header '{RetrieveDocumentTitle(xmlSourceDocument)}' - message: {customMethodResult.Message}, debuginfo: {customMethodResult.DebugInfo}");
                                                linkStatus = "target-methodkeyword-not-available";
                                            }

                                            break;
                                    }
                                }
                            }


                            // Insert the retrieved value
                            nodeElementSourceLink.InnerText = fragmentToInsert;
                        }
                        else
                        {
                            appLogger.LogError($"Could not find base configuration node with xpath {xPath} linked from page with header '{RetrieveDocumentTitle(xmlSourceDocument)}'");
                            linkStatus = "target-configbasenode-not-available";
                        }
                    }

                }

                // Mark the element so that we can color it in the UI in case not available
                SetAttribute(nodeElementSourceLink, "data-linkstatus", linkStatus);
                SetAttribute(nodeElementSourceLink, "contenteditable", "false");
            }




            return xmlSourceDocument;
        }


        /// <summary>
        /// Removes the injected elements from the content before saving it to the disk
        /// </summary>
        /// <param name="xmlSourceDocument"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static XmlDocument RemoveInjectedConfigurationLinkElements(XmlDocument xmlSourceDocument, ProjectVariables projectVars)
        {
            // Global variables
            string? lang = projectVars.outputChannelVariantLanguage;

            // Check if this is a regular article or a website data file
            var xpathElementLink = $"/data/content[@lang='{lang}']/*//*[@data-configurationlink]";


            // A) Check for links to elements
            var nodeListElementSourceLinks = xmlSourceDocument.SelectNodes(xpathElementLink);
            foreach (XmlNode nodeElementSourceLink in nodeListElementSourceLinks)
            {
                // Remove the attributes that we have dynamically set on the element
                RemoveAttribute(nodeElementSourceLink, "data-linkstatus");
                RemoveAttribute(nodeElementSourceLink, "data-editable");
                RemoveAttribute(nodeElementSourceLink, "contenteditable");

                // Inject an empty comment so that the div does not become a self-closing div
                nodeElementSourceLink.InnerXml = "<!-- . -->";
            }

            return xmlSourceDocument;
        }


        /// <summary>
        /// Parses a reference to a section or element link in format bla.xml#guid
        /// </summary>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static Tuple<string, string, string> ParseConfigurationReference(string reference)
        {
            var configKeyword = "";
            var configXpath = "";
            var operation = "";

            if (reference.Contains("#"))
            {
                string[] elements = reference.Split('#');
                configKeyword = elements[0];
                configXpath = elements[1];

                if (elements.Length > 2)
                {
                    operation = elements[2];
                }
            }

            return new Tuple<string, string, string>(configKeyword, configXpath, operation);
        }

        /// <summary>
        /// Retrieves the title of a section document
        /// </summary>
        /// <param name="xmlSourceDocument"></param>
        /// <returns></returns>
        public static string RetrieveDocumentTitle(XmlDocument xmlSourceDocument)
        {
            return RetrieveNodeValueIfExists("//*[local-name()='h1' or local-name()='h2' or local-name()='h3']", xmlSourceDocument) ?? "";
        }

    }
}