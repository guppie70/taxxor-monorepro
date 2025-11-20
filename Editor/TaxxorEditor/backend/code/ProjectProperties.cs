using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// View and edit project properties
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Renders the HTML view to show in the project properties form
        /// </summary>
        public static async Task ViewProjectProperties(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted data
            var projectId = request.RetrievePostedValue("projectid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);

            try
            {
                // Clone the application configuration so that we can manipulate it
                var xmlApplicationConfigurationCloned = new XmlDocument();
                xmlApplicationConfigurationCloned.LoadXml(xmlApplicationConfiguration.OuterXml);

                var nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectId)}]");
                if (nodeProject == null)
                {
                    HandleError("Project definition could not be found", $"projectId: {projectId}, stack-trace: {GetStackTrace()}");
                }

                // Add information about external tables data - append an element that can contain the information that we have received about the access control service
                var nodeGenericData = xmlApplicationConfigurationCloned.CreateElement("generic_data");
                xmlApplicationConfigurationCloned.DocumentElement.AppendChild(nodeGenericData);

                // => Append the workbooks that we have available in the Philips Generic Data Connector
                var xmlWorkbooks = await GenericDataConnector.ListWorkbooks();
                if (XmlContainsError(xmlWorkbooks)) HandleError(ReturnTypeEnum.Json, "Could not retrieve information", $"xmlWorkbooks: {xmlWorkbooks.OuterXml}, stack-trace: {GetStackTrace()}");
                //appLogger.LogDebug(xmlRoles.OuterXml);
                nodeGenericData.AppendChild(xmlApplicationConfigurationCloned.ImportNode(xmlWorkbooks.DocumentElement, true));

                // => Add reporting periods
                var reportingPeriod = nodeProject.SelectSingleNode("reporting_period")?.InnerText;
                var reportTypeId = nodeProject.GetAttribute("report-type");
                var xmlReportingPeriods = RenderReportingPeriods(false, true, true, reportingPeriod, reportTypeId);
                var nodeReportingPeriodsImported = xmlApplicationConfigurationCloned.ImportNode(xmlReportingPeriods.DocumentElement, true);
                xmlApplicationConfigurationCloned.DocumentElement.AppendChild(nodeReportingPeriodsImported);

                // => Mark the reporting period for this project

                var nodeReportingPeriodNone = xmlApplicationConfigurationCloned.SelectSingleNode($"/configuration/reporting_periods//period[@id='none']");
                if (string.IsNullOrEmpty(reportingPeriod) || reportingPeriod == "none")
                {
                    nodeReportingPeriodNone.SetAttribute("selected", "selected");
                }
                else
                {
                    var xPath = $"/configuration/reporting_periods//period[@id='{reportingPeriod}']";
                    // Console.WriteLine($"xPath: {xPath}");
                    var nodeCurrentReportingPeriod = xmlApplicationConfigurationCloned.SelectSingleNode(xPath);
                    if (nodeCurrentReportingPeriod == null)
                    {
                        // Set the "none" item
                        nodeReportingPeriodNone.SetAttribute("selected", "selected");
                    }
                    else
                    {
                        // Mark the current reporting period
                        nodeCurrentReportingPeriod.SetAttribute("selected", "selected");
                    }
                }

                // => Retrieve the project status
                string? projectStatus = nodeProject.SelectSingleNode("versions/version/status")?.InnerText;


                // Dump the xml document that we have created for inspection
                if (siteType == "local" || siteType == "dev" || siteType == "prev") await xmlApplicationConfigurationCloned.SaveAsync(logRootPathOs + "/project-properties.xml", false, true);

                var xmlNode = xmlApplicationConfiguration.SelectSingleNode("/configuration/locations/location[@id='cms_xsl_project-properties']");
                var xslPathOs = CalculateFullPathOs(xmlNode);

                XsltArgumentList xsltArgs = new XsltArgumentList();
                xsltArgs.AddParam("projectid", "", projectId);
                xsltArgs.AddParam("editorid", "", RetrieveEditorIdFromProjectId(projectId));
                xsltArgs.AddParam("permissions", "", string.Join(",", projectVars.currentUser.Permissions.Permissions.ToArray()));
                xsltArgs.AddParam("projectstatus", "", projectStatus);
                xsltArgs.AddParam("classicsyncenabled", "", ((RetrieveNodeValueIfExists("/configuration/general/settings/setting[@id='classic-excelsync-enabled']", xmlApplicationConfiguration) == "true") ? "yes" : "no"));

                var html = TransformXml(xmlApplicationConfigurationCloned, xslPathOs, xsltArgs);

                //if (reqVars.isDebugMode) SaveXmlDocument(xmlApplicationConfiguration, logRootPathOs + "/cms_overview.xml");

                //HandleError(ReturnTypeEnum.Json, "Thrown on purpose", $"projectStructureCloneResult: {projectStructureCloneResult.ToString()}, sourceProjectId: {sourceProjectId}, targetProjectId: {targetProjectId}, targetProjectName: {targetProjectName}, cloneAcl: {cloneAcl.ToString()}, reqVars.returnType: {reqVars.returnType}, stack-trace: {GetStackTrace()}");

                // Construct a response message for the client
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully retrieved project properties";
                if (isDevelopmentEnvironment)
                {
                    jsonData.result.debuginfo = $"projectId: {projectId}";
                }
                jsonData.result.html = html;

                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                //HandleError("Needs to be implemented", $"xmlAllItems: {HtmlEncodeForDisplay(xmlAllItems.OuterXml)}, xmlStoreResult: {HtmlEncodeForDisplay(html)}, stack-trace: {GetStackTrace()}");

                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Json, "Could not retrieve project properties", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Saves the project properties to the Document Store
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task SaveProjectProperties(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = siteType == "local" || siteType == "dev";
            var generateVersions = siteType != "local";
            generateVersions = true;
            var testForLocks = true;

            var step = 1;
            var stepMax = 1;

            // Retrieve posted data
            var projectId = request.RetrievePostedValue("projectid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
            var projectName = request.RetrievePostedValue("projectname", @"^([a-zA-Z0-9'\(\)<>\s\.,\?\!\+_\-\*&\^\%\:/]){1,120}$", true, ReturnTypeEnum.Json);
            var projectStatus = request.RetrievePostedValue("projectstatus", @"^(open|closed)$", true, ReturnTypeEnum.Json);
            var projectReportingPeriod = request.RetrievePostedValue("reportingperiod", RegexEnum.Default, true, ReturnTypeEnum.Json);
            var projectPublicationDate = request.RetrievePostedValue("publicationdate", RegexEnum.isodate, false, ReturnTypeEnum.Json, null);
            var projectStructuredDataSnapshotId = request.RetrievePostedValue("structureddatasnapshotid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
            var projectExternalDataInfo = request.RetrievePostedValue("externaldatainfo", @"^[a-zA-Z_\-\d\+\s:,]{1,1024}$", false, ReturnTypeEnum.Json) ?? "";

            var reportType = RetrieveReportTypeIdFromProjectId(projectId);

            var currentProjectStatus = RetrieveNodeValueIfExists($"/configuration/cms_projects/cms_project[@id='{projectId}']/versions/version/status", xmlApplicationConfiguration);

            // Check if there is a new reporting period set
            var reportingPeriodChanged = false;
            var currentReportingPeriod = RetrieveNodeValueIfExists($"/configuration/cms_projects/cms_project[@id='{projectId}']/reporting_period", xmlApplicationConfiguration);
            // TODO: Check if this report-type needs auto-shift date
            if (projectReportingPeriod != "none")
            {
                if (projectReportingPeriod != currentReportingPeriod && (reportType.Contains("annual") || reportType.Contains("quarterly")))
                {
                    reportingPeriodChanged = true;
                    stepMax = 4;
                }
            }

            if (currentProjectStatus == "closed" && projectStatus == "open") stepMax = stepMax + 2;

            // Abort the process if there are locks and we want to change the reporting period as well
            if (reportingPeriodChanged && testForLocks)
            {
                var numberOfLocks = FilingLockStore.RetrieveNumberOfLocks(projectVars);
                if (numberOfLocks > 0)
                {
                    HandleError($"Could not proceed because there {((numberOfLocks == 1) ? "is 1 section" : "are " + numberOfLocks + " sections")} locked in this project. Updating the project properties including a filing reporting period change sync is only available when there are no sections locked in a project.", $"stack-trace: {GetStackTrace()}");
                }
            }

            // TODO: Make use of the project properties object throughout this whole method
            var projectProperties = new CmsProjectProperties(projectId);
            projectProperties.name = projectName;
            projectProperties.projectStatus = GetProjectStatusEnum(projectStatus);
            projectProperties.reportingPeriod = projectReportingPeriod;
            projectProperties.SetPublicationDate(projectPublicationDate);

            var basicDebugString = $"{projectProperties.ToString()}, projectStructuredDataSnapshotId: {projectStructuredDataSnapshotId}, projectExternalDataInfo: {projectExternalDataInfo}";

            //HandleError(ReturnTypeEnum.Json, "Thrown on purpose", $"{basicDebugString}, stack-trace: {GetStackTrace()}");

            //
            // => Stylesheet cache
            //
            SetupPdfStylesheetCache(reqVars);

            try
            {

                //
                // => Save the project properties in the Taxxor Document Store
                //
                step = await _saveProjectPropertiesMessageToClient("Saving project properties on server", step, stepMax);
                var dataToPost = new Dictionary<string, string>
                {
                    { "projectid", projectId },
                    { "projectname", projectName },
                    { "projectstatus", projectStatus },
                    { "reportingperiod", projectReportingPeriod },
                    { "publicationdate", projectPublicationDate },
                    { "structureddatasnapshotid", projectStructuredDataSnapshotId },
                    { "externaldatainfo", projectExternalDataInfo }
                };


                // Call the service
                var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "projectproperties", dataToPost, debugRoutine);

                if (XmlContainsError(xmlResponse))
                {
                    await context.Response.Error(xmlResponse, reqVars.returnType, true);
                }
                else
                {
                    // Update the information in Application Configuration with the new content of Data Configuration
                    TaxxorReturnMessage updateApplicationConfigResult = await UpdateDataConfigurationInApplicationConfiguration();
                    if (!updateApplicationConfigResult.Success) HandleError(reqVars.returnType, "Update application configuration failed", $"updateApplicationConfigResult: {GenerateDebugObjectString(updateApplicationConfigResult)}, stack-trace: {GetStackTrace()}");

                    //
                    // => Shift the dates in the tables, etc in the content
                    //
                    if (reportingPeriodChanged)
                    {
                        var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
                        if (!renderProjectVarsResult) HandleError(reqVars.returnType, "Unable to retrieve hierarchy information for shifting the reporting period", $"stack-trace: {GetStackTrace()}");

                        // - Create a minor version before we start shifting the dates
                        step = await _saveProjectPropertiesMessageToClient("Creating pre-dateshift version", step, stepMax);
                        if (generateVersions)
                        {
                            var preSyncVerionResult = await GenerateVersion(projectId, $"Pre shift dates ({currentReportingPeriod} -> {projectReportingPeriod}) version", false);
                            if (!preSyncVerionResult.Success) HandleError(ReturnTypeEnum.Json, preSyncVerionResult.Message, preSyncVerionResult.DebugInfo);
                        }


                        // - Execute the date shift for all the data files involved
                        step = await _saveProjectPropertiesMessageToClient("Shifting the dates in the data files of this project", step, stepMax);
                        // Create a project variables object to be used in the bulk transformation
                        var projectVariablesForBulkTransformation = new ProjectVariables();
                        projectVariablesForBulkTransformation.projectId = projectId;
                        projectVariablesForBulkTransformation = await FillProjectVariablesFromProjectId(projectVariablesForBulkTransformation, false);

                        // Execute the bulk transform
                        var dateShiftResult = await BulkTransformFilingContentData(projectVariablesForBulkTransformation, "all", "movedatesintables", false);

                        if (!dateShiftResult.Success)
                        {
                            HandleError(ReturnTypeEnum.Json, dateShiftResult.Message, dateShiftResult.DebugInfo);
                        }

                        // - Create a minor version before we start shifting the dates
                        step = await _saveProjectPropertiesMessageToClient("Creating post-dateshift version", step, stepMax);
                        if (generateVersions)
                        {
                            var preSyncVerionResult = await GenerateVersion(projectId, $"Post shift dates ({currentReportingPeriod} -> {projectReportingPeriod}) version", false);
                            if (!preSyncVerionResult.Success) HandleError(ReturnTypeEnum.Json, preSyncVerionResult.Message, preSyncVerionResult.DebugInfo);
                        }
                    }


                    //
                    // => Render image and graph renditions
                    //
                    if (currentProjectStatus == "closed" && projectStatus == "open")
                    {
                        step = await _saveProjectPropertiesMessageToClient("Creating image renditions", step, stepMax);
                        await UpdateImageLibraryRenditions(projectId);

                        await _saveProjectPropertiesMessageToClient("Creating drawing renditions", step, stepMax);
                        await UpdateDrawingLibraryRenditions(projectId);
                    }

                    //
                    // => Render the metadata again
                    //
                    if (projectStatus == "open")
                    {
                        await UpdateCmsMetadata(projectId);
                    }

                    //
                    // => Clear the user section information cache data
                    //
                    UserSectionInformationCacheData.Clear();

                    await context.Response.OK(GenerateSuccessXml("Successfully updated the project properties", $"{basicDebugString}"), ReturnTypeEnum.Json, true);
                }



            }
            catch (Exception ex)
            {
                HandleError("Could not update the project properties", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Retrieves the languages used in this project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RetrieveProjectLanguages(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
            var languages = RetrieveProjectLanguages(editorId);

            // Create a simple XmlDocument containing the language information
            var xmlLanguages = RetrieveProjectLanguagesXml(editorId, projectVars.outputChannelDefaultLanguage);

            switch (reqVars.returnType)
            {
                case ReturnTypeEnum.Json:
                case ReturnTypeEnum.Html:
                    await context.Response.OK(PrettyPrintForJsonConversion(xmlLanguages), ReturnTypeEnum.Json, true);
                    break;

                default:
                    await response.OK(xmlLanguages.OuterXml, ReturnTypeEnum.Xml, false);
                    break;
            }
        }

        /// <summary>
        /// Utility routine to send a message to the client over a websocket
        /// </summary>
        /// <param name="message"></param>
        /// <param name="step"></param>
        /// <param name="maxStep"></param>
        /// <returns></returns>
        private static async Task<int> _saveProjectPropertiesMessageToClient(string message, int step = -1, int maxStep = -1)
        {
            if (step == -1)
            {
                await MessageToCurrentClient("SaveProjectPropertiesProgress", message);
            }
            else
            {
                await MessageToCurrentClient("SaveProjectPropertiesProgress", $"Step {step}/{maxStep}: {message}");
                step++;
            }

            return step;
        }

    }
}