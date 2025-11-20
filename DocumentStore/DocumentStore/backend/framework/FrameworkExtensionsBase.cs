using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using static Framework;
using static Taxxor.Project.ProjectLogic;

/// <summary>
/// Contains utilities that can be overrided at project level
/// </summary>
public partial class FrameworkExtensions
{
    /// <summary>
    /// Placeholder method for rendering paths on the "project level"
    /// </summary>
    /// <returns>The full path os project.</returns>
    /// <param name="context">Context.</param>
    /// <param name="path">String path.</param>
    /// <param name="relativeTo">String relative to.</param>
    /// <param name="objProjectVars">ProjectVariables object</param>
    public virtual string? CalculateFullPathOsProject(HttpContext context, string path, string relativeTo, object? objProjectVars = null, bool hideWarnings = false)
    {
        return null;
    }

    /// <summary>
    /// Used to execute specific path replacements on "project level"
    /// To overwrite this function in the project layer, use public new static string CalculateFullPathOsProjectReplacements()
    /// </summary>
    /// <param name="context"></param>
    /// <param name="strPath"></param>
    /// <param name="objProjectVars"></param>
    /// <returns>A path with the [xyz] pattern to be raplaced with some value</returns>
    public virtual string? CalculateFullPathOsProjectReplacements(HttpContext context, string strPath, object? objProjectVars = null, bool hideWarnings = false)
    {
        Framework.RequestVariables? reqVars;
        try
        {
            reqVars = Framework.RetrieveRequestVariables(context);
        }
        catch (Exception)
        {
            reqVars = null;
        }

        // Default to hard coded values in case that no RequestVariables object is passed to this function
        var siteLanguage = (reqVars != null) ? reqVars.siteLanguage : "en";

        // Replace placeholders in the path
        if (strPath.Contains('['))
        {
            if (reqVars == null)
            {
                Framework.appLogger.LogWarning($"requestVariables was null which probably needs to be corrected. strPath: '{strPath}' will be replaced using hard coded values.");
            }
            strPath = strPath.Replace("[lang]", siteLanguage);
        }

        return strPath;
    }

    /// <summary>
    /// Alters the hierarchy so before we are trying to find a page ID in it.
    /// </summary>
    /// <returns>The hierarchy.</returns>
    /// <param name="xmlHierarchy">Xml hierarchy.</param>
    public virtual XmlDocument ReworkHierarchy(HttpContext context, XmlDocument xmlHierarchy)
    {
        return xmlHierarchy;
    }

    /// <summary>
    /// Logic that can be executed when a page could not be located in the hierarchy
    /// </summary>
    /// <param name="requestVariables"></param>
    /// <param name="pageUrl"></param>
    /// <param name="xmlHierarchyPassed"></param>
    public virtual void HandlePageIdNotFoundCustom(RequestVariables requestVariables, string? pageUrl, XmlDocument xmlHierarchyPassed) { }

    /// <summary>
    /// This function can be overwritten by custom client logic 
    /// </summary>
    public virtual void RetrieveProjectVariablesCustom() { }

    /// <summary>
    /// Placeholder to render custom page ID logic
    /// </summary>
    /// <returns>The page logic by page identifier custom.</returns>
    /// <param name="context"></param>
    /// <param name="pageId">Page identifier.</param>
    public virtual async Task<bool> RenderPageLogicByPageIdCustom(HttpContext context, string pageId)
    {
        // Inserted a bogus asynchroneous statement just to make the compiler happy
        await DummyAwaiter();

        // Method needs to return true if it found something to process
        return false;
    }

    /// <summary>
    /// Optionally add HTML into the HTML HEAD section in the CustomLogic layer
    /// </summary>
    /// <param name="context"></param>
    /// <param name="pageId">Current Page ID</param>
    /// <param name="html">HTML head content rendered thus far</param>
    /// <returns></returns>
    public virtual string RenderPageHeadCustom(HttpContext context, string pageId, string html)
    {
        return html;
    }

    /// <summary>
    /// Optionally add HTML right after the BODY node in the CustomLogic layer
    /// </summary>
    /// <param name="context"></param>
    /// <param name="pageId"></param>
    /// <param name="html"></param>
    /// <returns></returns>
    public virtual string RenderPageBodyStartCustom(HttpContext context, string pageId, string html)
    {
        return html;
    }

    /// <summary>
    /// Optionally add HTML just before the BODY node closes in the CustomLogic layer
    /// </summary>
    /// <param name="context"></param>
    /// <param name="pageId"></param>
    /// <param name="html"></param>
    /// <returns></returns>
    public virtual string RenderPageBodyEndCustom(HttpContext context, string pageId, string html)
    {
        return html;
    }

    /// <summary>
    ///  Optionally add HTML at the start of the main column of the page in the CustomLogic layer
    /// </summary>
    /// <param name="context"></param>
    /// <param name="pageId"></param>
    /// <param name="html"></param>
    /// <returns></returns>
    public virtual string RenderPageMainColumnStartCustom(HttpContext context, string pageId, string html)
    {
        return html;
    }

    /// <summary>
    /// Optionally add HTML at the end of the main column of the page in the CustomLogic layer
    /// </summary>
    /// <param name="context"></param>
    /// <param name="pageId"></param>
    /// <param name="html"></param>
    /// <returns></returns>
    public virtual string RenderPageMainColumnEndCustom(HttpContext context, string pageId, string html)
    {
        return html;
    }

    /// <summary>
    /// Function that can be overwridden in project mode that executes after the XmlDocument has successfully been saved
    /// To overwrite this method in the project layer, please use public new static bool SaveXmlDocumentProject
    /// </summary>
    /// <param name="context"></param>
    /// <param name="xmlDoc"></param>
    /// <param name="xmlPathOs"></param>
    /// <param name="message">Message for the save function on project level</param>
    /// <returns></returns>
    public virtual async Task<bool> SaveXmlDocumentProject(HttpContext context, XmlDocument xmlDoc, string xmlPathOs, string message)
    {
        await DummyAwaiter();
        return true;
    }

    /// <summary>
    /// Optionally override this function to have special replacements in the HTML of the web page visible in the editor 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="html"></param>
    /// <returns></returns>
    public virtual XmlDocument RenderWebPageEditorContent(HttpContext context, XmlDocument xmlWebDoc)
    {
        return xmlWebDoc;
    }

    /// <summary>
    /// Method to export website content to some remote location
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sourceLocation"></param>
    /// <param name="targetLocation"></param>
    /// <returns></returns>
    public virtual async Task<TaxxorReturnMessage> ExportWebsiteContent(HttpContext context, string sourceLocation, string targetLocation)
    {
        await DummyAwaiter();
        return new TaxxorReturnMessage(false, "Export function not defined", "Export function for website content needs to be defined in an extension method");
    }

    /// <summary>
    /// Optionally writes webpage content to the editor
    /// </summary>
    /// <param name="context"></param>
    /// <param name="pageId"></param>
    /// <returns></returns>
    public virtual async Task<bool> WriteWebPageEditorContent(HttpContext context, XmlDocument webpageContent, string pageId)
    {
        // Inserted a bogus asynchroneous statement just to make the compiler happy
        await DummyAwaiter();

        // Method needs to return true if it found something to process
        return false;
    }

    /// <summary>
    /// Applies post-save transformation for the XHTML that is saved for a web-page using the inline editor
    /// </summary>
    /// <param name="context"></param>
    /// <param name="xmlWebDoc"></param>
    /// <returns></returns>
    public virtual XmlDocument SaveWebPageEditorContent(HttpContext context, XmlDocument xmlWebDoc, string xmlDataFilePathOs, string language)
    {
        return xmlWebDoc;
    }

    /// <summary>
    /// Renders the default HTML content that needs to appear inside the iframe that the inline editor works with
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual string RenderEditorIframeContent(HttpContext context)
    {
        // Using single quotes for HTML node attributes to prevent issues with code formatting in VS Code
        var defaultEditorIframeContent = $@"
            <html>
                <head>
                    <title>Default Editor Content</title>
                </head>
                <body>
                    <div class='tx-editorwrapper'>
                        <div class='styles'></div>
                        <div class='content'></div>
                    </div>
                </body>
            </html>";

        return defaultEditorIframeContent.Replace("'", "\"");
    }

    /// <summary>
    /// Generates an XmlDocument that contains the CSS and JS files that are used in the Taxxor Editor and in the PDF Generation
    /// </summary>
    /// <param name="context"></param>
    /// <param name="mode"></param>
    /// <param name="layout"></param>
    /// <returns></returns>
    public virtual XmlDocument RenderOutputChannelCssAndJs(Taxxor.Project.ProjectLogic.ProjectVariables projectVars, string mode = "normal", string? layout = null)
    {
        var xmlClientAssets = new XmlDocument();

        var assetsNode = xmlClientAssets.CreateElement("assets");
        assetsNode.AppendChild(xmlClientAssets.CreateElement("css"));
        assetsNode.AppendChild(xmlClientAssets.CreateElement("js"));

        xmlClientAssets.AppendChild(assetsNode);
        return xmlClientAssets;
    }

    /// <summary>
    /// Renders a filename for a PDF file generated for the website
    /// </summary>
    /// <param name="projectVariablesForPdfGeneration"></param>
    /// <param name="reportingPeriod"></param>
    /// <returns></returns>
    public virtual string RenderPdfFileNameForWebsite(Taxxor.Project.ProjectLogic.ProjectVariables projectVariablesForPdfGeneration, string reportingPeriod)
    {
        return $"{reportingPeriod}-{projectVariablesForPdfGeneration.outputChannelVariantLanguage}.pdf";
    }

    /// <summary>
    /// Renders a filename for a website XHTML file
    /// </summary>
    /// <param name="context"></param>
    /// <param name="dataFileName"></param>
    /// <returns></returns>
    public virtual string RenderHtmlFileNameForWebsite(HttpContext context, string dataFileName)
    {
        return $"{Path.GetFileNameWithoutExtension(dataFileName)}.html";
    }

    /// <summary>
    /// Preprocesses the website XML data file, before converting it to (X)HTML
    /// </summary>
    /// <param name="context"></param>
    /// <param name="xmlDataFile">XmlDocument object of the data file we are processing</param>
    /// <param name="xmlDataFilePathOs">Original source path where the XML file is located</param>
    /// <param name="tempFolderPathOs">Working directory where we generate the website in (on a shared folder)</param>
    /// <param name="websiteLanguage">Website language</param>
    /// <returns></returns>
    public virtual async Task<XmlDocument> PreprocessWebsiteXmlDatafile(HttpContext context, XmlDocument xmlDataFile, string xmlDataFilePathOs, string tempFolderPathOs, string websiteLanguage, bool useLoremIpsum)
    {
        await DummyAwaiter();
        return xmlDataFile;
    }

    /// <summary>
    /// Renders the XHTML content of a web page
    /// </summary>
    /// <param name="context"></param>
    /// <param name="xmlDataFile"></param>
    /// <param name="xmlDataFilePathOs"></param>
    /// <param name="websiteLanguage"></param>
    /// <param name="renderPdfFiles"></param>
    /// <returns></returns>
    public virtual async Task<XmlDocument> RenderXhtmlContent(HttpContext context, XmlDocument xmlDataFile, string xmlDataFilePathOs, string websiteLanguage, bool renderPdfFiles)
    {
        await DummyAwaiter();
        return xmlDataFile;
    }

    /// <summary>
    /// Renders website data - generic for all languages
    /// </summary>
    /// <param name="context"></param>
    /// <param name="websiteFolderPathOs"></param>
    /// <param name="dataFolderPathOs"></param>
    public virtual TaxxorReturnMessage RenderWebsiteData(HttpContext context, string websiteFolderPathOs, string dataFolderPathOs)
    {
        return new TaxxorReturnMessage(false, $"Did not render agnostic data files", $"The extension method RenderWebsiteData() needs to be implemented on a per Taxxor client basis.");
    }

    /// <summary>
    /// Renders the data files for a website (language specific)
    /// </summary>
    /// <param name="context"></param>
    /// <param name="websiteFolderPathOs"></param>
    /// <param name="dataFolderPathOs"></param>
    /// <param name="websiteLanguage"></param>
    /// <param name="renderPdfFiles"></param>
    /// <returns></returns>
    public virtual TaxxorReturnMessage RenderWebsiteDataForLanguage(HttpContext context, string websiteFolderPathOs, string dataFolderPathOs, string websiteLanguage, bool renderPdfFiles)
    {
        return new TaxxorReturnMessage(false, $"Did not render data files for language {websiteLanguage}", $"The extension method RenderWebsiteDataForLanguage() needs to be implemented on a per Taxxor client basis.");
    }

    /// <summary>
    /// Calculates the project id prefix
    /// </summary>
    /// <param name="reportingPeriod"></param>
    /// <param name="publicationDate"></param>
    /// <param name="reportType"></param>
    /// <returns></returns>
    public virtual string GetBaseProjectId(string reportingPeriod, string publicationDate, string reportType)
    {
        if (string.IsNullOrEmpty(reportingPeriod))
        {
            return reportType + "_" + CreateTimeBasedString("second");
        }
        else
        {
            return reportType + "_" + ((!string.IsNullOrEmpty(reportingPeriod)) ? reportingPeriod : publicationDate);
        }
    }

    /// <summary>
    /// Allows to define custom formatting methods for formatting the result of a configuration link in the content
    /// </summary>
    /// <param name="value">Configuration value</param>
    /// <param name="methodname">Name of the custom method to format or manipulate the configuration value</param>
    /// <param name="arguments">Custom method arguments</param>
    /// <returns></returns>
    public virtual TaxxorReturnMessage CustomConfigurationLinkMethod(string value, string methodname, string arguments)
    {
        // Use the "payload" field to return the manipulated string
        return new TaxxorReturnMessage(false, "No custom methods defined", value, "Custom methods need to be defined in the extensions class");
    }

    /// <summary>
    /// Extension method for removing an SSO cookie - can be defined per Taxxor customer
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual TaxxorReturnMessage RemoveSSOCookie(HttpContext context)
    {
        return new TaxxorReturnMessage(true, "No custom SSO Cookie remove method defined", "Define a custom method in the extensions class");
    }

    /// <summary>
    /// Extension method for guessing a Structured Data Element period when we could not find a hint for that period in the table headers
    /// </summary>
    /// <param name="factId"></param>
    /// <param name="tableId"></param>
    /// <param name="existingPeriod"></param>
    /// <param name="dateWrapperProperties"></param>
    /// <param name="projectPeriodMetadata"></param>
    /// <returns></returns>
    public virtual TaxxorReturnMessage GuessNewStructuredDataPeriod(string factId, string tableId, string existingPeriod, DateWrapperProperties dateWrapperProperties, ProjectPeriodProperties projectPeriodMetadata)
    {
        return new TaxxorReturnMessage(true, "No custom GuessNewStructuredDataPeriod method defined", "", "Define a custom method in the extensions class");
    }

    /// <summary>
    /// Generates the XPath selector for the articles where we need to inject a section number to
    /// </summary>
    /// <returns></returns>
    public virtual string GenerateArticleXpathForAddingSectionNumbering()
    {
        return "//article[@data-tocnumber]";
    }

    /// <summary>
    /// Determines if this website output channel is allowed to be published using the Quick Publish method
    /// </summary>
    /// <param name="projectVars"></param>
    /// <returns></returns>
    public virtual bool CheckIfWebsiteIsAllowedToBeQuickPublished(ProjectVariables projectVars)
    {
        return true;
    }

    /// <summary>
    /// Publish a web page directly on the hosting platform
    /// </summary>
    /// <param name="context"></param>
    /// <param name="targetPlatform"></param>
    /// <param name="dispatchWebsocketMessages"></param>
    /// <returns></returns>
    public virtual async Task<TaxxorReturnMessage> QuickPublishWebPage(HttpContext context, string targetPlatform, bool dispatchWebsocketMessages = true)
    {
        await DummyAwaiter();
        return new TaxxorReturnMessage(false, "Quick publish needs to be implemented as an extension");
    }

    /// <summary>
    /// Returns a list of project ID's that should not be published when the "Quick publish all web content" routine is executed
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual async Task<string[]> RetrieveProjectIdsNotToBePublished(HttpContext context)
    {
        await DummyAwaiter();
        string[] projectIdsToBeSkipped = [];
        return projectIdsToBeSkipped;
    }

    /// <summary>
    /// Renders the xpath to select in-text footnote references with
    /// </summary>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public virtual string RenderIntextFootnoteSelector(string? projectId = null)
    {

        return "*//span[@class='footnote section']";
    }

    /// <summary>
    /// Generates a path to an image used in the PDF which uses curly bracket placeholders to dynamically render full paths based on external variables
    /// </summary>
    /// <param name="basePath"></param>
    /// <returns></returns>
    public virtual string RenderPdfImagePlaceholderPath(string basePath)
    {
        return basePath;
    }

    /// <summary>
    /// Generates a path to a download used in the PDF which uses curly bracket placeholders to dynamically render full paths based on external variables
    /// </summary>
    /// <param name="basePath"></param>
    /// <returns></returns>
    public virtual string RenderPdfDownloadPlaceholderPath(string basePath)
    {
        return basePath;
    }

    /// <summary>
    /// Generates a path to an image used in the website which uses curly bracket placeholders to dynamically render full paths based on external variables
    /// </summary>
    /// <param name="basePath"></param>
    /// <returns></returns>
    public virtual string RenderWebsiteImagePlaceholderPath(string basePath)
    {
        return basePath;
    }

    /// <summary>
    /// Generates a path to a download used in the website which uses curly bracket placeholders to dynamically render full paths based on external variables
    /// </summary>
    /// <param name="basePath"></param>
    /// <returns></returns>
    public virtual string RenderWebsiteDownloadPlaceholderPath(string basePath)
    {
        return basePath;
    }

    /// <summary>
    /// Implements a custom search and replace method when cloning sections
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="mappingSearch"></param>
    /// <param name="mappingReplace"></param>
    /// <returns></returns>
    public virtual string? CustomMappingClusterReplacements(string mapping, string mappingSearch, string mappingReplace)
    {
        return null;
    }


    /// <summary>
    /// Renders hard coded drawings for a report
    /// </summary>
    /// <param name="xmlDoc"></param>
    /// <param name="projectId"></param>
    /// <param name="channel"></param>
    /// <param name="sourceFolderPathOs"></param>
    /// <param name="targetFolderPathOs"></param>
    /// <returns></returns>
    public virtual TaxxorReturnMessage RenderHardCodedDrawings(XmlDocument xmlDoc, string projectId, string channel, string sourceFolderPathOs, string targetFolderPathOs)
    {
        return new TaxxorReturnMessage(true, "Needs to be implemented on a per customer basis", xmlDoc);
    }

    /// <summary>
    /// Potentially post process the XHTML for filings right after it has been loaded from the Taxxor Project Data Store
    /// </summary>
    /// <param name="xmlDoc"></param>
    /// <param name="projectId"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    public virtual TaxxorReturnMessage XhtmlFilingPostProcessing(XmlDocument xmlDoc, string projectId, string channel)
    {
        return new TaxxorReturnMessage(true, "Could be implemented on a per customer basis", xmlDoc);
    }

    /// <summary>
    /// Potentially post process the XHTML that was received for the PDF or Word generation process
    /// </summary>
    /// <param name="xmlDoc"></param>
    /// <param name="projectVars"></param>
    /// <returns></returns>
    public virtual TaxxorReturnMessage XhtmlPdfPostProcessing(XmlDocument xmlDoc, ProjectVariables projectVars)
    {
        return new TaxxorReturnMessage(true, "Could be implemented on a per customer basis", xmlDoc);
    }

    /// <summary>
    /// Generates the xpath to select the PDF documents we need to render for the website
    /// </summary>
    /// <param name="projectVars"></param>
    /// <returns></returns>
    public virtual string RenderWebsitePdfGenerationXpath(ProjectVariables projectVars)
    {
        return "variants/variant";
    }

    /// <summary>
    /// Determines specific PDF settings for the (website) output channel
    /// </summary>
    /// <param name="pdfProperties"></param>
    /// <param name="projectVars"></param>
    public virtual void SetPdfPropertiesForOutputChannel(ref PdfProperties pdfProperties, ProjectVariables projectVars, string outputChannelType)
    {

    }

    /// <summary>
    /// Utility to cleanup a website source folder before it gets exported
    /// </summary>
    /// <param name="websiteOutputFolderPathOs"></param>
    /// <returns></returns>
    public virtual TaxxorReturnMessage CleanupWebsiteAssets(string websiteOutputFolderPathOs)
    {
        return new TaxxorReturnMessage(true, "Could be implemented on a per customer basis");
    }

    /// <summary>
    /// Routine used to test if the footnote reference check should be performed or not
    /// </summary>
    /// <param name="nodeArticle"></param>
    /// <returns></returns>
    public virtual bool EnableFootnoteReferenceCheck(XmlNode nodeArticle)
    {
        return true;
    }

    /// <summary>
    /// Extends the list of datareferences to be included in a ERP data sync
    /// </summary>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public virtual List<string> ExtendSdeSycDataReferences(string projectId)
    {
        return new List<string>();
    }

    /// <summary>
    /// Customer specific code to post process the SDE overview XML
    /// </summary>
    /// <param name="xmlDoc"></param>
    public virtual void PostProcessSdeOverview(ref XmlDocument xmlDoc)
    {

    }

    /// <summary>
    /// Customer specific logic to post-process the MS Word content which is being send to the Conversion Service
    /// </summary>
    /// <param name="xmlDoc"></param>
    public virtual void PostProcessMsWordDoc(ref XmlDocument xmlDoc, ProjectVariables projectVars)
    {

    }


    /// <summary>
    /// Customer specific logic for post processing (PDF) source HTML using content status indicators
    /// </summary>
    /// <param name="xmlDoc"></param>
    /// <param name="projectVars"></param>
    public virtual void PostProcessPdfHtmlForContentStatus(ref XmlDocument xmlDoc, ProjectVariables projectVars)
    {

    }

    public virtual Dictionary<string, string> RenderWebsiteReplacements(ProjectVariables projectVars, string? reportingPeriod = null, string siteLanguage = "en", bool baseList = false)
    {
        return new Dictionary<string, string>();
    }

}

// Placeholder class to make sure that the C# code compiles even if the FrameworkExtensions class has not been explicitly defined
public partial class ProjectExtensions : FrameworkExtensions { }