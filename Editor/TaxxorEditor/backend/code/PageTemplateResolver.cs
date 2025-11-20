using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using UAParser;
using HtmlAgilityPack;

namespace Taxxor.Project
{

    /// <summary>
    /// Routines for rendering HTML pages for the Taxxor Editor
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders the HTML output of a typical Taxxor Editor page by using the *.template file from the website public folder and replacing the placeholders in it with actively calculated content
        /// </summary>
        /// <returns>The taxxor editor page.</returns>
        /// <param name="pageId">Page identifier.</param>
        public static async Task RenderTaxxorEditorPage(string pageId)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var errorMessage = "Unable to render Taxxor DM page";
            var debugRoutine = false;

            try
            {
                var originalUrl = RetrievePageUrl(pageId, reqVars.xmlHierarchyStripped);
                if (originalUrl.Contains('?'))
                {
                    originalUrl = RegExpReplace(@"^(.*)(\?.*)$", originalUrl, "$1");

                    if (originalUrl != context.Request.Path)
                    {

                    }
                }
                var currentReportingPeriod = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/reporting_period")?.InnerText;

                if (string.IsNullOrEmpty(originalUrl)) HandleError(reqVars.returnType, "Could not find URI of requested page", $"pageId: '{pageId}'");

                // Calculate the path to the template file
                var templateFilePath = "";
                if (originalUrl.EndsWith(".html", StringComparison.CurrentCulture) || originalUrl.EndsWith(".aspx", StringComparison.CurrentCulture))
                {
                    templateFilePath = RegExpReplace(@"^(.*\.)(.*)$", originalUrl, "$1template");
                }
                else if (originalUrl == "/")
                {
                    templateFilePath = "/index.template";
                }
                else
                {
                    templateFilePath = $"{originalUrl}.template";
                }
                var templateFilePathOs = websiteRootPathOs + templateFilePath;

                // Convert the template file into an HTML page that we can send to the client
                if (File.Exists(templateFilePathOs))
                {
                    var templateContent = await RetrieveTextFile(templateFilePathOs);

                    // Replace standard placeholders

                    // A) Variables
                    templateContent = templateContent.Replace("[pageid]", pageId);
                    templateContent = templateContent.Replace("[sitetype]", siteType);
                    templateContent = templateContent.Replace("[projectid]", projectVars.projectId);
                    templateContent = templateContent.Replace("[user-displayname]", projectVars.currentUser.DisplayName);
                    templateContent = templateContent.Replace("[projectstatus]", projectVars.projectStatus ?? "unknown");
                    templateContent = templateContent.Replace("[nonce]", context.Items["nonce"]?.ToString() ?? "");

                    // B) Results from rendering functions
                    var pageHeadElementId = "page-head_default";
                    var pageBodyEndElementId = "";
                    switch (projectVars.editorId)
                    {
                        case "default_editor":
                            pageBodyEndElementId = "page-body-end";
                            break;
                        default:
                            pageBodyEndElementId = "page-body-end_content-tools-editor";
                            break;
                    }
                    switch (pageId)
                    {
                        case "ulogout-dotnet":
                            pageHeadElementId = "page-head_logout";
                            break;
                    }

                    templateContent = templateContent.Replace("[page-head-content]", await RenderPageHead(pageId, pageHeadElementId));
                    templateContent = templateContent.Replace("[page-body-start]", RenderPageBodyStart(pageId));
                    templateContent = templateContent.Replace("[page-maincolumn-start]", RenderPageMainColumnStart(pageId));
                    templateContent = templateContent.Replace("[page-maincolumn-end]", RenderPageMainColumnEnd(pageId));
                    templateContent = templateContent.Replace("[page-body-end]", RenderPageBodyEnd(pageId, pageBodyEndElementId));

                    templateContent = templateContent.Replace("[document-name]", RetrieveDocumentName(reqVars, projectVars));

                    templateContent = templateContent.Replace("[uri-staticassets]", projectVars.uriStaticAssets);

                    // C) Page specific replacements
                    switch (pageId)
                    {
                        case "cms-overview":
                            //
                            // => The below takes care of VS Code Server redirecting to the root of the website
                            //
                            if (context.Request.Cookies.ContainsKey("lastClickedLinkUrl"))
                            {
                                var lastClickedLinkUrl = context.Request.Cookies["lastClickedLinkUrl"];
                                // Check if the URL contains a query string with the parameter 'folder'
                                var queryString = context.Request.QueryString.Value;
                                var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString);
                                if (queryParams.ContainsKey("folder") || queryParams.ContainsKey("workspace"))
                                {
                                    // Redirect to the value of lastClickedLinkUrl with the complete query string
                                    context.Response.Redirect(lastClickedLinkUrl + "/" + queryString);
                                    return;
                                }
                            }



                            templateContent = templateContent.Replace("[cms-overview-body]", await RenderCmsOverviewBody("yes"));
                            templateContent = templateContent.Replace("[cms-gitrepository-status-overview]", RenderGitRepositoryStatusOverview());

                            // Render the dropdown content for the reporting periods
                            templateContent = templateContent.Replace("[select-reporting-period-clone]", RenderReportingPeriodSelect("select-reportingperiod"));
                            templateContent = templateContent.Replace("[select-reporting-period-new]", RenderReportingPeriodSelect("select-reportingperiod-new", "form-control", false, false));

                            // Render advanced options or not
                            templateContent = templateContent.Replace("[delete-modal-advancedoptions]", projectVars.currentUser.Permissions.ViewDeveloperTools ? " style='display:block'" : "");

                            // Test if we encounter a browser that we do not support
                            var browserUserAgent = context.Request.Headers["User-Agent"].ToString();
                            var uaParser = Parser.GetDefault();
                            var clientInfo = uaParser.Parse(browserUserAgent);
                            var browserMajorVersion = 0;
                            if (clientInfo.UA.Family == "Edge")
                            {
                                if (!int.TryParse(clientInfo.UA.Major, out browserMajorVersion))
                                {
                                    appLogger.LogWarning($"Could not parse Edge browser major version using {clientInfo.UA.Major} as input");
                                }
                            }
                            templateContent = templateContent.Replace("[class-browser-not-supported]", ((clientInfo.UA.Family == "Edge" && browserMajorVersion < 90) || clientInfo.UA.Family == "IE") ? " unsupported-browser" : "");
                            break;

                        case "cms_content-editor":
                            // When the Taxxor Editor page loads, any current locks of this user should be removed
                            FilingLockStore.RemoveLocksForUser(projectVars.currentUser.Id, "filing");

                            // Retrieve the project prefereces
                            JObject? projectPreferences = null;
                            var nodeProjectPreferences = projectVars.currentUser.XmlUserPreferences.SelectSingleNode($"/settings/setting[@id='projectpreferences-{projectVars.projectId}']");
                            if (nodeProjectPreferences != null)
                            {
                                var projectPreferencesJson = nodeProjectPreferences.InnerText;
                                projectPreferences = JObject.Parse(projectPreferencesJson);
                            }

                            templateContent = templateContent.Replace("[cms-rootpath]", CmsRootPath);
                            templateContent = templateContent.Replace("[projectid]", projectVars.projectId);
                            templateContent = templateContent.Replace("[versionid]", projectVars.versionId);
                            templateContent = templateContent.Replace("[firsteditablepageid]", projectVars.idFirstEditablePage);
                            templateContent = templateContent.Replace("[currentyear]", Convert.ToString(projectVars.reportingPeriod));
                            templateContent = templateContent.Replace("[project-rootpath]", projectVars.projectRootPath);
                            templateContent = templateContent.Replace("[outputchannel-variant-language]", projectVars.outputChannelVariantLanguage);

                            templateContent = templateContent.Replace("[cms-editor-navigation]", await GenerateEditorNavigation(projectPreferences));

                            var did = context.Request.RetrievePostedValue("did", RegexEnum.Loose, true, reqVars.returnType);

                            templateContent = templateContent.Replace("[outputchannel-links]", RenderOutputChannelSelector());

                            templateContent = templateContent.Replace("[url-xmldata-retrieve]", RenderDataRetrievalUrl(projectVars.projectId, projectVars.versionId, "text", projectVars.idFirstEditablePage));
                            templateContent = templateContent.Replace("[url-xsd-retrieve]", RenderDataRetrievalUrl(projectVars.projectId, projectVars.versionId, "schema", projectVars.idFirstEditablePage));


                            templateContent = templateContent.Replace("[layoutoptions]", _renderOutputChannelLayoutOptions(projectVars, projectPreferences));
                            templateContent = templateContent.Replace("[leftmenufiltercheckbox]", _renderLeftMenuFilter(projectVars, projectPreferences));
                            templateContent = templateContent.Replace("[showsectionnumbersinmenucheckbox]", _renderShowSectionNumbersCheckbox(projectVars, projectPreferences));

                            templateContent = templateContent.Replace("[datalineagerenderingbuttons]", _renderDataLineageButtonRows(projectVars, projectPreferences));

                            // Show/hide customer specific options
                            // - table properties
                            var xhtmlTablePropertiesCustomization = xmlApplicationConfiguration.SelectSingleNode($"/configuration/customizations/page_elements/element[@id='editor-tableproperties-extraoptions']")?.InnerXml ?? "";
                            templateContent = templateContent.Replace("[projectconfig:editor-tableproperties-extraoptions]", xhtmlTablePropertiesCustomization);

                            break;

                        case "cms_version-manager":
                            templateContent = templateContent.Replace("[first-outputchannel-name]", RetrieveNodeValueIfExists("/configuration/editors/editor[@id='" + projectVars.editorId + "']/output_channels/output_channel/variants/variant/name", xmlApplicationConfiguration));

                            // Show/hide generate version button
                            if (projectVars.currentUser.Permissions.ViewFilingVersions)
                            {
                                templateContent = templateContent.Replace("[button-generate-version]", "<button id=\"btn-newversion\" class=\"btn btn-info btn-sm\" type=\"button\" onclick=\"createNewVersion()\"><i class=\"glyphicon glyphicon-plus\"></i> Create version</button>");
                            }
                            else
                            {
                                templateContent = templateContent.Replace("[button-generate-version]", "");
                            }

                            break;

                        case "cms_preview-pdfdocument":
                            templateContent = templateContent.Replace("[project-rootpath]", projectVars.projectRootPath);

                            // Add own IP address
                            // TODO: In a complete docker environment, we should use a domain name here i.e. http://pdfservice
                            templateContent = templateContent.Replace("[own-ip]", localIpAddress);

                            // Determines what the preview PDF page will show
                            var documentSectionIdToShow = projectVars.idFirstEditablePage;
                            if (projectVars.editorId == "default_filing") documentSectionIdToShow = "all";
                            templateContent = templateContent.Replace("[id-first-editable]", documentSectionIdToShow);
                            templateContent = templateContent.Replace("[outputchannel-links]", RenderOutputChannelSelector());

                            break;

                        case "cms_hierarchy-manager":
                            // When the Taxxor Hierarchy page loads, any current locks of this user should be removed
                            FilingLockStore.RemoveLocksForUser(projectVars.currentUser.Id, "hierarchy");

                            var hierarchyManagerBodyContent = await RenderCmsHierarchyManagerBody(true);
                            templateContent = templateContent.Replace("[cms-hierarchy-manager-body]", hierarchyManagerBodyContent);
                            templateContent = templateContent.Replace("[reporting-requirements-form-fields]", RenderReportingRequirementsFormFields(projectVars));

                            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
                            var languages = RetrieveProjectLanguages(editorId);

                            // Create a simple XmlDocument containing the language information
                            var xmlLanguages = RetrieveProjectLanguagesXml(editorId, projectVars.outputChannelDefaultLanguage);
                            // Console.WriteLine("&&&&& xml languages &&&&&");
                            // Console.WriteLine(PrettyPrintXml(xmlLanguages));
                            // Console.WriteLine("&&&&&&&&&&&&&&&&&&&&&&&&&");

                            // For the language clone utilities
                            /*
                            <languages>
                            <lang id="en" default="true" />
                            <lang id="zh" />
                            </languages>
                            */
                            var nodeListLanguages = xmlLanguages.SelectNodes("/languages/lang");
                            templateContent = templateContent.Replace("[class-showhidelanguageclone]", (nodeListLanguages.Count > 1) ? "show" : "hide");
                            if (nodeListLanguages.Count > 1)
                            {
                                // The options shown in the clone language select boxes
                                var optionsSourceLang = "";
                                var optionsTargetLang = "";
                                var targetLangDefaultSet = false;
                                foreach (XmlNode nodeLang in nodeListLanguages)
                                {
                                    var lang = nodeLang.GetAttribute("id");
                                    var defaultLanguage = lang == projectVars.outputChannelDefaultLanguage;
                                    optionsSourceLang += $"<option value='{lang}'{(defaultLanguage ? " selected='selected'" : "")}>{lang.ToUpper()}</option>";

                                    var targetSelected = "";
                                    if (lang != projectVars.outputChannelDefaultLanguage && !targetLangDefaultSet)
                                    {
                                        targetSelected = " selected='selected'";
                                        targetLangDefaultSet = true;
                                    }
                                    optionsTargetLang += $"<option value='{lang}'{targetSelected}>{lang.ToUpper()}</option>";
                                }
                                templateContent = templateContent.Replace("[clone-sourcelang]", optionsSourceLang);
                                templateContent = templateContent.Replace("[clone-targetlang]", optionsTargetLang);

                                templateContent = templateContent.Replace("[translationtoolshidestart]", "");
                                templateContent = templateContent.Replace("[translationtoolshideend]", "");
                            }
                            else
                            {
                                templateContent = templateContent.Replace("[translationtoolshidestart]", "<!--");
                                templateContent = templateContent.Replace("[translationtoolshideend]", "-->");
                            }

                            // For the import hierarchy utility
                            var nodeListOutputChannels = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel/variants/variant");
                            templateContent = templateContent.Replace("[class-showhideimporthierarchy]", (nodeListOutputChannels.Count > 1) ? "show-hierarchyimporttool" : "hide-hierarchyimporttool");

                            /*
                            [clone-sourcelang]
                            <option value="en" selected="selected">English</option>
                                                                        <option value="zh">Mandarin</option>

                            */



                            break;

                        case "cms_accesscontrolmanager":
                            templateContent = templateContent.Replace("[outputchannel-links]", RenderOutputChannelSelector(false, "select-outputchannels form-control input-sm"));

                            var xmlUsersForSimulation = await Taxxor.ConnectedServices.AccessControlService.ListUsers();
                            if (XmlContainsError(xmlUsersForSimulation)) HandleError(ReturnTypeEnum.Json, "Could not retrieve information", $"xmlUsers: {xmlUsersForSimulation.OuterXml}, stack-trace: {GetStackTrace()}");
                            var htmlUserSelectBox = _renderUserSelect(xmlUsersForSimulation, "userid", "form-control input-sm");
                            templateContent = templateContent.Replace("[user-select-box]", htmlUserSelectBox);

                            var xmlRoles = await Taxxor.ConnectedServices.AccessControlService.ListRoles();
                            templateContent = templateContent.Replace("[roleslist-search]", _renderRolesSelect(xmlRoles, "tx-rolereplace-search", "form-control tx-rolereplace-search"));
                            templateContent = templateContent.Replace("[roleslist-replace]", _renderRolesSelect(xmlRoles, "tx-rolereplace-replace", "form-control tx-rolereplace-replace"));

                            break;

                        case "cms_development-page":
                            templateContent = templateContent.Replace("[session-data]", RetrieveAllSessionData());

                            templateContent = templateContent.Replace("[select-reporting-period]", RenderReportingPeriodSelect("select-reportingperiod-dateshift", "form-control", false, false, false, currentReportingPeriod, projectVars.reportTypeId));
                            templateContent = templateContent.Replace("[select-reporting-period-source]", RenderReportingPeriodSelect("select-reportingperiod-dateshift-baseperiod", "form-control", false, false, false, null, projectVars.reportTypeId));
                            break;

                        case "cms_publicationvariants":
                            templateContent = templateContent.Replace("[dynamic-publication-variants]", await RenderPublicationVariantsOverview());
                            break;

                        case "cms_administration-page":

                            // Fill the login as box
                            var xmlUsers = await Taxxor.ConnectedServices.AccessControlService.ListUsers();
                            if (XmlContainsError(xmlUsers)) HandleError(ReturnTypeEnum.Json, "Could not retrieve information", $"xmlUsers: {xmlUsers.OuterXml}, stack-trace: {GetStackTrace()}");
                            var userSelectBox = _renderUserSelect(xmlUsers, "userid");
                            templateContent = templateContent.Replace("[user-select-box]", userSelectBox);


                            // Use HTML Agility Pack for HTML/XHTML manipulation
                            var htmlDoc = new HtmlDocument();
                            htmlDoc.LoadHtml(templateContent);

                            // Find all div nodes with data-permissions attribute
                            var divsWithPermissions = htmlDoc.DocumentNode.SelectNodes("//div[@data-permissions]");
                            if (divsWithPermissions != null)
                            {
                                if (debugRoutine) Console.WriteLine($"Found {divsWithPermissions.Count} div(s) with data-permissions attribute");
                                foreach (var divNode in divsWithPermissions)
                                {
                                    // Log the value of data-permissions attribute
                                    var permissionsValue = divNode.GetAttributeValue("data-permissions", "");
                                    if (debugRoutine) Console.WriteLine($"data-permissions value: {permissionsValue}");

                                    // Check if user has at least one of the required permissions
                                    bool hasPermission = false;

                                    if (!string.IsNullOrEmpty(permissionsValue) && projectVars?.currentUser?.Permissions?.Permissions != null)
                                    {
                                        // Split comma-delimited permissions
                                        var requiredPermissions = permissionsValue.Split(',')
                                            .Select(p => p.Trim())
                                            .Where(p => !string.IsNullOrEmpty(p));

                                        // Check if user has at least one of the required permissions
                                        foreach (var requiredPerm in requiredPermissions)
                                        {
                                            if (projectVars.currentUser.Permissions.Permissions.Contains(requiredPerm))
                                            {
                                                hasPermission = true;
                                                if (debugRoutine) Console.WriteLine($"User has permission: {requiredPerm}");
                                                break;
                                            }
                                        }

                                    }
                                    else if (string.IsNullOrEmpty(permissionsValue))
                                    {
                                        // Empty permission value means no restriction
                                        hasPermission = false;
                                        if (debugRoutine) Console.WriteLine("No permission restriction (empty value)");
                                        // if (debugRoutine) Console.WriteLine("Removed div element - empty permission value");
                                        divNode.Attributes.Add("data=remove", "true");
                                    }

                                    // Only process attribute removal if node wasn't removed
                                    if (hasPermission)
                                    {
                                        if (debugRoutine) Console.WriteLine($"User has required permission: {hasPermission}");
                                        // Remove the data-permissions attribute but keep the div
                                        divNode.Attributes.Remove("data-permissions");
                                        if (debugRoutine) Console.WriteLine("Removed data-permissions attribute, kept div element");
                                    }
                                    else
                                    {
                                        if (debugRoutine) Console.WriteLine("Removed div element - user lacks required permissions");
                                        // Remove the div element if user doesn't have permission
                                        divNode.Remove();
                                    }
                                }
                            }
                            else
                            {
                                if (debugRoutine) Console.WriteLine("No div elements with data-permissions attribute found");
                            }

                            // Grab the cleaned-up content and set the template content based on that
                            templateContent = htmlDoc.DocumentNode.OuterHtml;


                            break;

                        case "cms_html-styler":
                            var htmlStylerDataFolderPathOs = RenderHtmlStylerOutputPathOs(projectVars.projectId, null);
                            var htmlSelectOptions = "";

                            if (Directory.Exists(htmlStylerDataFolderPathOs))
                            {
                                foreach (var directoryPathOs in Directory.EnumerateDirectories(htmlStylerDataFolderPathOs))
                                {
                                    var directoryName = Path.GetFileName(directoryPathOs);
                                    var md5 = EncryptText(directoryPathOs, EncryptionTypeEnum.MD5);
                                    var selected = (htmlSelectOptions == "") ? "selected=\"selected\"" : "";
                                    htmlSelectOptions += $"<option value=\"{md5}\" {selected}>{directoryName}</option>";
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"HTML Styler problem: directory {htmlStylerDataFolderPathOs} does not exist");
                            }

                            templateContent = templateContent.Replace("[htmlstylerdocumentoptions]", htmlSelectOptions);

                            break;

                        case "cms_vscode-reportdesignpackages":
                            // User the user preferences to select the correct Report Design Package
                            var userPreferencesKey = $"hierarchyoutputchannel-{projectVars.projectId}";
                            nodeProjectPreferences = projectVars.currentUser.XmlUserPreferences.SelectSingleNode($"/settings/setting[@id='{userPreferencesKey}']");
                            var outputChannelType = "pdf";
                            if (nodeProjectPreferences != null)
                            {
                                outputChannelType = RegExpReplace(@"^octype=(.*?)\:.*$", nodeProjectPreferences.SelectSingleNode("valuelorem")?.InnerText ?? "pdf", "$1");
                            }

                            // Inject the Taxxor DM JavaScript at the end of the page
                            templateContent = templateContent
                                .Replace("[octype]", outputChannelType)
                                .Replace("[page-body-end-scripts]", RetrieveInterfaceElement("page-body-end_content-tools-editor", xmlApplicationConfiguration, reqVars))
                                .Replace("[querystringversion]", $"?v={StaticAssetsVersion}")
                                .Replace("[uri-staticassets]", projectVars.uriStaticAssets);

                            break;

                        default:
                            break;
                    }

                    await context.Response.OK(templateContent, ReturnTypeEnum.Html, true);
                }
                else
                {
                    HandleError(errorMessage, $"Template file: '{templateFilePathOs}' could not be found");
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, errorMessage);
                HandleError(errorMessage, $"error: {ex}");
            }
        }

        /// <summary>
        /// Renders an HTML select (drop-down) box containing relevant reporting periods
        /// </summary>
        /// <param name="selectName"></param>
        /// <param name="className"></param>
        /// <param name="shortList"></param>
        /// <param name="sortAscending"></param>
        /// <param name="renderNotApplicable"></param>
        /// <returns></returns>
        public static string RenderReportingPeriodSelect(string selectName, string className = "form-control", bool shortList = true, bool sortAscending = true, bool renderNotApplicable = true, string? referencePeriod = null, string? reportTypeId = null)
        {
            // Render the dropdown content for the reporting periods
            var sbSelectBox = new StringBuilder();
            sbSelectBox.AppendLine($"<select name=\"{selectName}\" id=\"{selectName}\" class=\"{className}\">");
            sbSelectBox.AppendLine($"<option value=\"none\" selected=\"selected\">-- Select a reporting period --</option>");

            // Retrieve an XML Document containing the reporting periods
            var xmlReportingPeriods = RenderReportingPeriods(shortList, false, sortAscending, referencePeriod, reportTypeId);

            if (xmlReportingPeriods.SelectSingleNode("/reporting_periods/*/period") != null)
            {
                // Contains year seperators
                var nodeListReportingYears = xmlReportingPeriods.SelectNodes("/reporting_periods/*");
                foreach (XmlNode nodeReportingYear in nodeListReportingYears)
                {
                    var nodeListPeriods = nodeReportingYear.SelectNodes("period");

                    if (nodeListPeriods.Count > 1) sbSelectBox.AppendLine($"<optgroup label=\"{nodeReportingYear.GetAttribute("label")}\">");
                    renderOptions(nodeListPeriods);
                    if (nodeListPeriods.Count > 1) sbSelectBox.AppendLine($"</optgroup>");
                }
            }
            else
            {
                var nodeListPeriods = xmlReportingPeriods.SelectNodes("/reporting_periods/period");
                renderOptions(nodeListPeriods);
            }



            if (renderNotApplicable) sbSelectBox.AppendLine($"<option value=\"not-applicable\">Not applicable</option>");
            sbSelectBox.AppendLine("</select>");

            return sbSelectBox.ToString();

            /// <summary>
            /// Helper method to render the select box options
            /// </summary>
            /// <param name="nodeListOptions"></param>
            void renderOptions(XmlNodeList nodeListOptions)
            {
                foreach (XmlNode nodePeriod in nodeListOptions)
                {
                    sbSelectBox.AppendLine($"<option{((nodePeriod.GetAttribute("current") == "true") ? "  selected=\"selected\"" : "")} value=\"{GetAttribute(nodePeriod, "id")}\">{nodePeriod.InnerText}</option>");
                }
            }
        }

        /// <summary>
        /// Renders a select box containing all the users
        /// </summary>
        /// <param name="xmlUsers"></param>
        /// <param name="selectName"></param>
        /// <param name="className"></param>
        /// <param name="shortList"></param>
        /// <returns></returns>
        private static string _renderUserSelect(XmlDocument xmlUsers, string selectName, string className = "form-control", bool shortList = true)
        {
            // Build a list of user id's that we can sort
            var userIds = new List<string>();
            var nodeListUsers = xmlUsers.SelectNodes("//user");
            foreach (XmlNode nodeUser in nodeListUsers)
            {
                var userId = nodeUser.GetAttribute("id");
                if (!string.IsNullOrEmpty(userId))
                {
                    userIds.Add(userId);
                }
                else
                {
                    appLogger.LogWarning($"Could not find a user id. stack-trace: {GetStackTrace()}");
                }
            }

            // Sort the list
            userIds.Sort();

            // Render the HTML of the dropdown content for the users
            var sbSelectBox = new StringBuilder();
            sbSelectBox.AppendLine($"<select name=\"{selectName}\" id=\"{selectName}\" class=\"{className}\">");
            sbSelectBox.AppendLine($"<option value=\"none\" selected=\"selected\">-- Select a user --</option>");
            foreach (var userId in userIds)
            {
                sbSelectBox.AppendLine($"<option value=\"{userId}\">{userId}</option>");
            }
            sbSelectBox.AppendLine("</select>");

            // Return the HTML as a string
            return sbSelectBox.ToString();
        }


        /// <summary>
        /// Renders an HTML select box containing the roles defined in the Access Control service
        /// </summary>
        /// <param name="xmlRoles"></param>
        /// <param name="selectName"></param>
        /// <param name="className"></param>
        /// <param name="shortList"></param>
        /// <returns></returns>
        private static string _renderRolesSelect(XmlDocument xmlRoles, string selectName, string className = "form-control", bool shortList = true)
        {
            // Build a list of user id's that we can sort
            var roles = new Dictionary<string, string>();
            var nodeListRoles = xmlRoles.SelectNodes("/roles/role");
            foreach (XmlNode nodeRole in nodeListRoles)
            {
                var userId = nodeRole.GetAttribute("id");
                if (!string.IsNullOrEmpty(userId))
                {
                    roles.Add(userId, nodeRole.FirstChild.InnerText);
                }
                else
                {
                    appLogger.LogWarning($"Could not find a user id. stack-trace: {GetStackTrace()}");
                }
            }

            // Sort the dictionary
            var rolesOrdered = roles.OrderBy(x => x.Value);

            // Render the HTML of the dropdown content for the users
            var sbSelectBox = new StringBuilder();
            sbSelectBox.AppendLine($"<select name=\"{selectName}\" id=\"{selectName}\" class=\"{className}\">");
            sbSelectBox.AppendLine($"<option value=\"none\" selected=\"selected\">-- Select a role --</option>");
            foreach (var roleInfo in rolesOrdered)
            {
                sbSelectBox.AppendLine($"<option value=\"{roleInfo.Key}\">{roleInfo.Value}</option>");
            }
            sbSelectBox.AppendLine("</select>");

            // Return the HTML as a string
            return sbSelectBox.ToString();
        }


        /// <summary>
        /// Renders the checkbox shown in the content editor preferences modal dialog for the left menu filter
        /// </summary>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        private static string _renderLeftMenuFilter(ProjectVariables projectVars, JObject projectPreferences)
        {
            var leftMenuFilterActive = false;
            if (projectPreferences != null)
            {
                try
                {
                    leftMenuFilterActive = projectPreferences.SelectToken("leftmenufilter")?.Value<bool>() ?? false;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Error parsing the state of the left menu filter (left menu filter)");
                }

            }
            // Console.WriteLine($"- leftMenuFilterActive: {leftMenuFilterActive}");

            return $"<input type='checkbox' class='bla' id='cbxhideuneditable'{(leftMenuFilterActive ? " checked='checked'" : "")}/>".Replace((char)39, (char)34);
        }

        /// <summary>
        /// Renders the checkbox that allows a user to optionally show section numbers in the left menu
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="projectPreferences"></param>
        /// <returns></returns>
        private static string _renderShowSectionNumbersCheckbox(ProjectVariables projectVars, JObject projectPreferences)
        {
            var showSectionNumbersInMenu = false;
            if (projectPreferences != null)
            {
                try
                {
                    showSectionNumbersInMenu = projectPreferences.SelectToken("showsectionnumbersinmenu")?.Value<bool>() ?? false;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Error parsing the state of the option to show section numbers in left menu");
                }

            }

            return $"<input type='checkbox' class='bla' id='cbxshowsectionnumbersinmenu'{(showSectionNumbersInMenu ? " checked='checked'" : "")}/>".Replace((char)39, (char)34);
        }

        private static string _renderDataLineageButtonRows(ProjectVariables projectVars, JObject projectPreferences)
        {
            var sbHtml = new StringBuilder();

            // Get the reporting requirements for the current project
            var nodeListRequirements = xmlApplicationConfiguration.SelectNodes(
                $"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/reporting_requirements/reporting_requirement");

            if (nodeListRequirements == null || nodeListRequirements.Count == 0)
            {
                // Return empty string if no reporting requirements found
                return "";
            }

            // Track processed taxonomies to avoid duplicates
            var processedTaxonomies = new HashSet<string>();

            foreach (XmlNode nodeRequirement in nodeListRequirements)
            {
                // Get the ref-taxonomy attribute if it exists
                var nodeTaxonomy = nodeRequirement.GetAttribute("ref-taxonomy");

                // Skip if this taxonomy has already been processed
                if (!string.IsNullOrEmpty(nodeTaxonomy))
                {
                    if (processedTaxonomies.Contains(nodeTaxonomy))
                        continue;
                    processedTaxonomies.Add(nodeTaxonomy);
                }

                // Determine the scheme (similar to XSLT logic)
                var scheme = !string.IsNullOrEmpty(nodeTaxonomy)
                    ? nodeTaxonomy
                    : nodeRequirement.GetAttribute("ref-mappingservice");

                // Determine the display name (similar to XSLT logic)
                var displayName = !string.IsNullOrEmpty(nodeTaxonomy)
                    ? nodeTaxonomy
                    : nodeRequirement.SelectSingleNode("name")?.InnerText ?? scheme;

                // Get other attributes
                var outputChannelVariant = nodeRequirement.GetAttribute("ref-outputchannelvariant");
                if (outputChannelVariant == projectVars.outputChannelVariantId)
                {
                    var format = nodeRequirement.GetAttribute("format");

                    // Generate the table row
                    sbHtml.AppendLine($@"            <tr class=""datalineage-row scheme-{scheme} outputchannelvariant-{outputChannelVariant} format-{format}"">
                <td>Data lineage ({displayName})</td>
                <td></td>
                <td></td>
                <td><button class=""btn-rendersectiondatalineage"" data-scheme=""{scheme}"">Render</button></td>
            </tr>");
                }

            }

            return sbHtml.ToString();
        }

        /// <summary>
        /// Renders the layout variations available for an output channel (to be used in the content editor preferences modal dialog)
        /// </summary>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        private static string _renderOutputChannelLayoutOptions(ProjectVariables projectVars, JObject projectPreferences)
        {
            var layoutDetails = RetrieveOutputChannelDefaultLayout(projectVars);

            if (layoutDetails.Forced) return "";

            string? storedLayoutOptionFromUserPreferences = null;
            if (projectPreferences != null)
            {
                try
                {
                    if (
                        projectPreferences?.SelectToken($"outputchannels") != null &&
                        projectPreferences?.SelectToken($"outputchannels.{projectVars.outputChannelVariantId}") != null &&
                        projectPreferences?.SelectToken($"outputchannels.{projectVars.outputChannelVariantId}.layout") != null
                        )
                    {
                        storedLayoutOptionFromUserPreferences = projectPreferences?.SelectToken($"outputchannels.{projectVars.outputChannelVariantId}.layout")?.ToString();
                        switch (storedLayoutOptionFromUserPreferences)
                        {
                            case string a when !string.IsNullOrEmpty(a) && a.Contains('{'):
                                // This is a nested JSON definition that accidentally was stored as a user preference for the layout, so we ignore it
                                storedLayoutOptionFromUserPreferences = null;
                                break;

                            case string b when !string.IsNullOrEmpty(b):
                                // Found a direct match, so we use it
                                storedLayoutOptionFromUserPreferences = b;
                                break;

                            default:
                                // Layout option was not found, so we reset it to null
                                storedLayoutOptionFromUserPreferences = null;
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Error parsing the state of the left menu filter (output channel layout) for user {projectVars.currentUser.Id}");
                }
            }
            // Console.WriteLine($"- storedLayoutOptionFromUserPreferences: {storedLayoutOptionFromUserPreferences}");
            var nodeListLayoutOptions = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel[@type='{projectVars.outputChannelType}']/layouts/layout");

            // If there is only one layout option, then it does not make sense to show the panel
            if (nodeListLayoutOptions.Count == 1) return "";

            var htmlToReturn = $@"
        <div class='form-group'>
            <label for='oututchannelLayoutOptions' class='col-sm-3 control-label'>{projectVars.outputChannelType.ToUpper()} view mode</label>
            <div class='col-sm-9'>
            ";

            // Generate the radio buttons to allow the user to select the layout option
            foreach (XmlNode nodeLayout in nodeListLayoutOptions)
            {
                string? layoutId = nodeLayout.GetAttribute("id");
                if (storedLayoutOptionFromUserPreferences != null)
                {
                    htmlToReturn += RenderOption(layoutId, nodeLayout.SelectSingleNode("name").InnerText, layoutId == storedLayoutOptionFromUserPreferences);
                }
                else
                {
                    htmlToReturn += RenderOption(layoutId, nodeLayout.SelectSingleNode("name").InnerText, layoutId == layoutDetails.Layout);
                }
            }

            htmlToReturn += @"
            </div>
        </div>
            ";

            return htmlToReturn.Replace((char)39, (char)34);

            string RenderOption(string id, string label, bool defaultSelected)
            {
                return $@"
                <div class='radio'>
                    <label>
                        <input type='radio' name='oututchannelLayoutOptions' value='{id}'{(defaultSelected ? " checked='checked'" : "")}/>
                        {label}
                    </label>
                </div>";
            }
        }

    }
}