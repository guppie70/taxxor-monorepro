using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

    /// <summary>
    /// Clones an existing project in the Taxxor Document Store
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Create a new project by cloning an existing one
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task CloneExistingProject(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var fixEfrReferences = true;

            var debugRoutine = (siteType == "local" || siteType == "dev" || siteType == "prev");

            var step = 1;
            var stepMax = 8;
            if (fixEfrReferences) stepMax = stepMax + 1;

            // Retrieve posted values
            var sourceProjectId = request.RetrievePostedValue("sourceid", @"^[a-zA-Z_\-\d]{1,70}$", true, ReturnTypeEnum.Json);
            var targetProjectName = request.RetrievePostedValue("targetprojectname", @"^([a-zA-Z0-9'\(\)<>\s\.,\?\!\+_\-\*&\^\%\:\\\/]){1,120}$", true, ReturnTypeEnum.Json);
            var cloneAclRetrieved = request.RetrievePostedValue("cloneacl", @"^(yes|no)$", true, ReturnTypeEnum.Json);
            var cloneAcl = (cloneAclRetrieved == "yes");
            var stripWhiteSpaceMarkersReceived = request.RetrievePostedValue("stripwhitespacemarkers", @"^(yes|no)$", true, ReturnTypeEnum.Json);
            var stripWhiteSpaceMarkers = (stripWhiteSpaceMarkersReceived == "yes");
            var forwardSdesInTextReceived = request.RetrievePostedValue("forwardsdesintext", @"^(yes|no)$", true, ReturnTypeEnum.Json);
            var forwardSdesInText = (forwardSdesInTextReceived == "yes");

            var reportingPeriod = request.RetrievePostedValue("reportingperiod", @"^[a-zA-Z_\-\d]{1,20}$", false, ReturnTypeEnum.Json);
            var publicationDate = request.RetrievePostedValue("publicationdate", RegexEnum.isodate, false, ReturnTypeEnum.Json, null);

            var reportType = RetrieveReportTypeIdFromProjectId(sourceProjectId);

            // Test that at least one of reporting period and a publication date have been submitted
            var targetReportingPeriod = reportingPeriod;
            if ((string.IsNullOrEmpty(reportingPeriod) || reportingPeriod == "none") && string.IsNullOrEmpty(publicationDate))
            {
                HandleError(ReturnTypeEnum.Json, "Not enough information was supplied to clone the project", $"reportingPeriod: {reportingPeriod}, publicationDate: {publicationDate}, stack-trace: {GetStackTrace()}");
            }
            else if (string.IsNullOrEmpty(reportingPeriod) || reportingPeriod == "none")
            {
                targetReportingPeriod = publicationDate;
            }

            // Setup the XSLT stylesheet cache
            SetupPdfStylesheetCache(reqVars);

            //
            // => Return data to the web client to close the XHR call
            //
            await response.OK(GenerateSuccessXml("Started project clone", $"projectId: {projectVars.projectId}, sourceProjectId: {sourceProjectId}"), ReturnTypeEnum.Json, true, true);
            await response.CompleteAsync();

            // 
            // => Wait in case another project clone has finished
            //
            if (ProjectCreationInProgress)
            {
                var maxWaitLoops = 90;
                var loopCounter = 0;
                do
                {
                    loopCounter++;
                    appLogger.LogInformation($"Another project is being created or cloned, so we wait until that one is finished");
                    await PauseExecution(1000);
                } while ((ProjectCreationInProgress && loopCounter <= maxWaitLoops));

                if (loopCounter == (maxWaitLoops + 1))
                {
                    HandleError(ReturnTypeEnum.Json, "Another project creation process is in progress and seems to take a long time to finalize", $"loopCounter: {loopCounter}, stack-trace: {GetStackTrace()}");
                }
            }

            // Indicate that a project creation process is running
            ProjectCreationInProgress = true;


            try
            {
                //
                // => Compare reporting periods
                //
                var currentProjectPeriodMetadata = new ProjectPeriodProperties(sourceProjectId);
                var targetProjectPeriodMetadata = new ProjectPeriodProperties(targetReportingPeriod);

                if (!currentProjectPeriodMetadata.Success) throw new Exception("Unable to parse source project metadata");
                if (!targetProjectPeriodMetadata.Success) throw new Exception("Unable to parse target project metadata");

                var moveByCompleteYear = false;
                var reportingPeriodChanged = false;
                var reportingPeriodOffsetInMonths = 0;
                var currentReportingPeriod = currentProjectPeriodMetadata.ReportingPeriod;
                // TODO: Check if this report-type needs auto-shift date
                if (reportingPeriod != currentReportingPeriod)
                {
                    // var referenceDate = _getPeriodEndDate(currentProjectYear, currentProjectQuarter);
                    reportingPeriodOffsetInMonths = ((targetProjectPeriodMetadata.PeriodEnd.Year - currentProjectPeriodMetadata.PeriodEnd.Year) * 12) + targetProjectPeriodMetadata.PeriodEnd.Month - currentProjectPeriodMetadata.PeriodEnd.Month;

                    moveByCompleteYear = (reportingPeriodOffsetInMonths % 12) == 0;
                    if (reportType.Contains("annual") || reportType.Contains("quarterly") || reportType.Contains("monthly"))
                    {
                        reportingPeriodChanged = true;
                        stepMax += 2;
                    }
                }

                // throw new Exception($"Thrown on purpose - moveByCompleteYear: {moveByCompleteYear}");

                if (!stripWhiteSpaceMarkers) stepMax--;



                var sourceProjectReportType = RetrieveReportTypeIdFromProjectId(sourceProjectId);

                // Calculate a project ID for the new project based on the reporting period that was passed
                var baseProjectId = Extensions.GetBaseProjectId(reportingPeriod, publicationDate, sourceProjectReportType);
                var targetProjectId = CalculateUniqueProjectId(baseProjectId);

                // Console.WriteLine("@@@@@@@@@@@@@@@");
                // Console.WriteLine($"- baseProjectId: {baseProjectId}");
                // Console.WriteLine($"- targetProjectId: {targetProjectId}");
                // Console.WriteLine("@@@@@@@@@@@@@@@");


                var baseDebugInfo = $"sourceProjectId: {sourceProjectId}, sourceProjectReportType: {sourceProjectReportType}, targetProjectId: {targetProjectId}, targetProjectName: {targetProjectName}, cloneAcl: {cloneAcl.ToString()}, reqVars.returnType: {reqVars.returnType}";

                // await _HandleProjectCloneError(targetProjectId, "Thrown on purpose", $"{baseDebugInfo}");

                //
                // => Clone the filestructure on the Taxxor Document Store
                //
                step = await _cloneProjectPropertiesMessageToClient("Clone the project structure in the Taxxor Document Store", step, stepMax);
                // Create data to post                            
                var cloneProjectDataToPost = new Dictionary<string, string>
                {
                    { "sourceid", sourceProjectId },
                    { "targetid", targetProjectId },
                    { "targetprojectname", targetProjectName },
                    { "cloneacl", cloneAclRetrieved },
                    { "reportingperiod", reportingPeriod },
                    { "publicationdate", publicationDate }
                };

                // Clone the filestructure and the configuration on the Taxxor Document Store
                var watch = System.Diagnostics.Stopwatch.StartNew();
                bool projectStructureCloneResult = await CallTaxxorDataService<bool>(RequestMethodEnum.Post, "cloneproject", cloneProjectDataToPost, true);
                watch.Stop();

                // Dump the time it took to generate the basic file structure on the disk 
                if (debugRoutine)
                {
                    TimeSpan ts = watch.Elapsed;
                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                    Console.WriteLine("");
                    Console.WriteLine("**********************************");
                    Console.WriteLine($"Create basic new project folder structure took: {elapsedTime}");
                    Console.WriteLine("**********************************");
                    Console.WriteLine("");
                }

                // Handle error
                if (!projectStructureCloneResult)
                {
                    await _HandleProjectCloneError(targetProjectId, "Clone of project structure and/or the configuration has failed", $"projectStructureCloneResult: {projectStructureCloneResult}, {baseDebugInfo}");
                }
                else
                {
                    //
                    // => Update the local CMS metadata object with the new files that have been cloned
                    //
                    await UpdateCmsMetadataRemoteAndLocal();
                }

                // => Update the application configuration for this application
                TaxxorReturnMessage updateApplicationConfigResult = await UpdateDataConfigurationInApplicationConfiguration();
                if (!updateApplicationConfigResult.Success)
                {
                    await _HandleProjectCloneError(targetProjectId, "Update application configuration failed", $"updateApplicationConfigResult: {updateApplicationConfigResult.ToString()}, {baseDebugInfo}");
                }

                //
                // => Create the initial version
                //
                step = await _cloneProjectPropertiesMessageToClient("Create initial version of the content", step, stepMax);

                // Fill the project vars object with information for the new project that we are setting up
                var renderProjectVarsResult = await ProjectVariablesFromProjectId(targetProjectId, true);
                if (!renderProjectVarsResult) await _HandleProjectCloneError(targetProjectId, "Creation of initial version failed", $"targetProjectId: {targetProjectId}, {baseDebugInfo}");

                var versionCreateResult = await GenerateVersion(targetProjectId, "major", "v0.0", "Initial version", debugRoutine, (siteType == "local"));
                if (!versionCreateResult.Success)
                {
                    await _HandleProjectCloneError(targetProjectId, "Creation of initial version failed", $"versionCreateResult: {versionCreateResult.ToString()}, {baseDebugInfo}");
                }
                else
                {
                    if (!string.IsNullOrEmpty(versionCreateResult.Payload))
                    {
                        await _cloneProjectPropertiesMessageToClient($"Warnings while creating initial version:\n{versionCreateResult.Payload}");
                    }
                }

                //
                // => Clone all the ACL settings or create a new one where the current user is the owner/administrator 
                //
                if (cloneAcl)
                {
                    step = await _cloneProjectPropertiesMessageToClient("Clone Access Control settings", step, stepMax);
                    var cloneRbacResourcesResult = await AccessControlService.CloneResources(sourceProjectId, targetProjectId);
                    if (!cloneRbacResourcesResult)
                    {
                        appLogger.LogWarning($"Clone of access control records failed: {cloneRbacResourcesResult.ToString()}, but we only log this problem because it is not critical.");
                        // await _HandleProjectCloneError(targetProjectId, "Clone of access control records failed", $"{baseDebugInfo}");
                    }
                }
                else
                {
                    step = await _cloneProjectPropertiesMessageToClient("Create initial Access Control settings", step, stepMax);
                    var itemId = "cms_project-details";
                    var resourceId = CalculateRbacResourceId(RequestMethodEnumToString(RequestMethodEnum.Get), itemId, targetProjectId, 3, false);

                    // Add an access recore
                    var createAccessRecordResult = false;
                    createAccessRecordResult = await AccessControlService.AddAccessRecord(resourceId, projectVars.currentUser.Id, "taxxor-administrator", true, true, debugRoutine);

                    if (!createAccessRecordResult)
                    {
                        await _HandleProjectCloneError(targetProjectId, "Creation of access control record failed", $"resourceId: {resourceId}, {baseDebugInfo}");
                    }
                }

                //
                // => Retrieve a list of all the structured data element ID's in the document that we want to clone
                //
                step = await _cloneProjectPropertiesMessageToClient("Retrieve a list of all structured data elements in this project", step, stepMax);
                var factIds = new List<string>();
                var xmlCompleteProjectContent = await DocumentStoreService.FilingData.LoadAll<XmlDocument>(sourceProjectId, "all", false, debugRoutine, false);
                if (XmlContainsError(xmlCompleteProjectContent))
                {
                    await _HandleProjectCloneError(targetProjectId, "Retrieval of complete project content failed", $"xmlCompleteProjectContent: {xmlCompleteProjectContent.OuterXml}, {baseDebugInfo}");
                }
                else
                {
                    var nodeListStructuredDataElements = _retrieveStructuredDataElements(xmlCompleteProjectContent, false);
                    foreach (XmlNode nodeStructuredDataElement in nodeListStructuredDataElements)
                    {
                        var factId = nodeStructuredDataElement.GetAttribute("data-fact-id").Replace("\n", "");
                        if (!factIds.Contains(factId)) factIds.Add(factId);
                    }

                    // Add XBRL Level 2 elements to the list (old style)
                    var nodeListLevelTwoElements = xmlCompleteProjectContent.SelectNodes("//*[contains(@class, 'xbrl-level-2') and @data-fact-id]");
                    foreach (XmlNode nodeLevelTwoElement in nodeListLevelTwoElements)
                    {
                        var factId = nodeLevelTwoElement.GetAttribute("data-fact-id").Replace("\n", "");
                        if (!factIds.Contains(factId)) factIds.Add(factId);
                    }

                    // Add XBRL Level 2 elements to the list (new style)
                    nodeListLevelTwoElements = xmlCompleteProjectContent.SelectNodes("//*[@data-block-id]");
                    foreach (XmlNode nodeLevelTwoElement in nodeListLevelTwoElements)
                    {
                        var factIdsLevelTwoRaw = nodeLevelTwoElement.GetAttribute("data-block-id") ?? "";
                        var factIdsLevelTwo = new List<string>();
                        if (factIdsLevelTwoRaw.Contains(","))
                        {
                            factIdsLevelTwo = factIdsLevelTwoRaw.Split(',').ToList();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(factIdsLevelTwoRaw)) factIdsLevelTwo.Add(factIdsLevelTwoRaw);
                        }

                        foreach (var factIdLevelTwo in factIdsLevelTwo)
                        {
                            var factIdLevelTwoToInsert = factIdLevelTwo.Replace("\n", "");
                            if (!factIds.Contains(factIdLevelTwoToInsert)) factIds.Add(factIdLevelTwoToInsert);
                        }
                    }

                }
                Console.WriteLine($"+++ Found {factIds.Count} structured data elements to clone - {TruncateString(xmlCompleteProjectContent.OuterXml, 1000)} +++");


                // Fill the project vars object with information for the new project that we are setting up
                renderProjectVarsResult = await ProjectVariablesFromProjectId(targetProjectId, true);
                if (!renderProjectVarsResult) await _HandleProjectCloneError(targetProjectId, "Creation of project variables for new project failed", $"targetProjectId: {targetProjectId}, {baseDebugInfo}");

                //
                // => Import the mapping clusters that are associated with the SDE's
                //
                var importMappingClustersResult = await ImportMappingClusters(sourceProjectId, targetProjectId, factIds, true, debugRoutine, step, stepMax);
                if (!importMappingClustersResult.Success)
                {
                    await _HandleProjectCloneError(targetProjectId, "Import of SDE's in target project failed", $"dateShiftResult: {importMappingClustersResult.ToString()}, {baseDebugInfo}");
                }
                else
                {
                    appLogger.LogInformation($"importMappingClustersResult.Message: {importMappingClustersResult.Message}");
                    appLogger.LogInformation($"importMappingClustersResult.DebugInfo: {importMappingClustersResult.DebugInfo}");
                }



                // Make sure that the websocket messages counter remains correct
                step = (reportingPeriodChanged) ? step + 1 : step;

                //
                // => Move the dymanic dates (mostly in the tables) in the cloned project if needed
                //
                if (reportingPeriodChanged)
                {
                    // - Execute the date shift for all the data files involved
                    step = await _cloneProjectPropertiesMessageToClient("Shifting the dates in the tables of this project", step, stepMax);
                    // Create a project variables object to be used in the bulk transformation
                    var projectVariablesForBulkTransformation = new ProjectVariables
                    {
                        projectId = targetProjectId
                    };
                    projectVariablesForBulkTransformation = await FillProjectVariablesFromProjectId(projectVariablesForBulkTransformation, false);

                    // Execute the bulk transform
                    var dateShiftResult = await BulkTransformFilingContentData(projectVariablesForBulkTransformation, "all", "movedatesintables", false, false);

                    if (!dateShiftResult.Success)
                    {
                        await _HandleProjectCloneError(targetProjectId, "Date shift in tables failed", $"dateShiftResult: {dateShiftResult.ToString()}, {baseDebugInfo}");
                    }

                }

                //
                // => Download the complete content of the report again to retrieve the updated content from the step above again and use that for the SDE date shifts 
                //
                var xmlCompleteProjectContentShiftedTableHeaders = await DocumentStoreService.FilingData.LoadAll<XmlDocument>(targetProjectId, "all", false, debugRoutine, false);
                if (XmlContainsError(xmlCompleteProjectContentShiftedTableHeaders))
                {
                    await _HandleProjectCloneError(targetProjectId, "Retrieval of complete project content after table headers date shift failed", $"xmlCompleteProjectContent: {xmlCompleteProjectContentShiftedTableHeaders.OuterXml}, {baseDebugInfo}");
                }

                //
                // => Move the structured data element periods
                //
                if (factIds.Count > 0 && reportingPeriodChanged)
                {
                    // - Execute the date shift for all the data files involved
                    var message = "Shifting the dates in the structured data elements for this project";
                    if (moveByCompleteYear) message = "Shifting the dates in the structured data elements by one year";
                    step = await _cloneProjectPropertiesMessageToClient(message, step, stepMax);

                    // Dump the document that we use for the dateshift routine on the disk
                    if (debugRoutine) await xmlCompleteProjectContentShiftedTableHeaders.SaveAsync($"{logRootPathOs}/dateshiftdoc.xml", true, true);

                    // 1 - Move the SDE's in the tables based on the dynamic dates used
                    TaxxorReturnMessage? sdeTableShiftResult = null;
                    if (moveByCompleteYear)
                    {
                        // If the target project is moved by exactly one or more years, then we can use the simple SDE move method without inspecting the table dates
                        sdeTableShiftResult = await MovePeriodsForStructuredDataElementsUsingBaseReportingPeriod(targetProjectId, xmlCompleteProjectContentShiftedTableHeaders, "all", currentReportingPeriod, reportingPeriod, "tables");
                    }
                    else
                    {
                        // Otherwise we try to forward the SDE's by inspecting the table context
                        sdeTableShiftResult = await MovePeriodsForStructuredDataElementsUsingTableInformation(targetProjectId, xmlCompleteProjectContentShiftedTableHeaders, "all");
                    }


                    if (!sdeTableShiftResult.Success)
                    {
                        await _HandleProjectCloneError(targetProjectId, sdeTableShiftResult.Message, $"{sdeTableShiftResult.ToString("\n")}, {baseDebugInfo}");
                    }

                    if (XmlContainsError(sdeTableShiftResult.XmlPayload))
                    {
                        appLogger.LogWarning($"Problems detected while shifting the SDE's in the tables, but these were already logged");
                        // appLogger.LogWarning(PrettyPrintXml(sdeTableShiftResult.XmlPayload));
                    }

                    // 2 - Move the SDE's in the text and specially marked tables based on the reporting period of the source project
                    var moveTextAndSpeciallyMarkedTables = !reportType.Contains("annual") || forwardSdesInText;

                    if (moveTextAndSpeciallyMarkedTables)
                    {
                        var periodMoveScope = "textandmarkedtables";

                        // // Only move the text when we are shifting a complete year - in that case, we have already moved the SDE mappings in the marked tables
                        // if (moveByCompleteYear) periodMoveScope = "text";

                        var sdeTextShiftResult = await MovePeriodsForStructuredDataElementsUsingBaseReportingPeriod(targetProjectId, xmlCompleteProjectContentShiftedTableHeaders, "all", currentReportingPeriod, reportingPeriod, periodMoveScope);

                        if (!sdeTextShiftResult.Success)
                        {
                            await _HandleProjectCloneError(targetProjectId, "Date shift of structured data elements in text and marked tables failed", $"dateShiftResult: {sdeTextShiftResult.ToString()}, {baseDebugInfo}");
                        }

                        if (XmlContainsError(sdeTextShiftResult.XmlPayload))
                        {
                            appLogger.LogWarning($"Problems detected while shifting the SDE's in the text and marked tables, but these were already logged");
                        }
                    }
                }

                //
                // => Rebuild the cache of the structured data elements used for the project
                //
                if (factIds.Count > 0)
                {
                    if (siteType != "local")
                    {
                        step = await _cloneProjectPropertiesMessageToClient("Building the structured data elements cache", step, stepMax);
                        var syncSdeResult = await SyncStructuredDataElementsPerDataReference(targetProjectId, "1", false, false, false, false, null, reqVars, projectVars);

                        if (!syncSdeResult.Success)
                        {
                            await _HandleProjectCloneError(targetProjectId, "Rebuilding the SDE cache failed", $"dateShiftResult: {syncSdeResult.ToString()}, {baseDebugInfo}");
                        }
                    }
                    else
                    {
                        var userMessage = $"!!! Skipping rebuild of structured data cache because this is a local environment !!!";
                        appLogger.LogWarning(userMessage);
                        var bogus = await _cloneProjectPropertiesMessageToClient(userMessage);
                    }
                }

                //
                // => Cleanup the rendered reports data (TAX-143)
                //



                //
                // => Create the final version
                //
                step = await _cloneProjectPropertiesMessageToClient("Create final version of the content", step, stepMax);
                versionCreateResult = await GenerateVersion(targetProjectId, "minor", "v0.1", "Shifted periods in content and structured data elements", debugRoutine);
                if (!versionCreateResult.Success)
                {
                    await _HandleProjectCloneError(targetProjectId, "Creation of initial version failed", $"versionCreateResult: {versionCreateResult.ToString()}, {baseDebugInfo}");
                }
                else
                {
                    if (!string.IsNullOrEmpty(versionCreateResult.Payload))
                    {
                        await _cloneProjectPropertiesMessageToClient($"Warnings while creating initial version:\n{versionCreateResult.Payload}");
                    }
                }

                //
                // => Remove whitespace markers from the data files
                //
                if (stripWhiteSpaceMarkers)
                {
                    step = await _cloneProjectPropertiesMessageToClient("Stripping whitespace markers from data files", step, stepMax);

                    // - Use the cache of the Taxxor Document Store find all the content files from the source project that we need to transform the whitespace markers from
                    var dataReferences = await RetrieveDataReferences(targetProjectId);

                    var (Success, Fail) = await BulkTransformFilingContentData(dataReferences, "stripwhitespacemarkers", targetProjectId, "all", debugRoutine);
                    if (Fail.Count > 0)
                    {
                        await _HandleProjectCloneError(targetProjectId, "Whitespace removal failed", $"success-references: {String.Join(",", Success)}, fail-references: {String.Join(",", Fail)}, {baseDebugInfo}");
                    }
                }

                //
                // => Generate graph renditions
                //
                step = await _cloneProjectPropertiesMessageToClient("Generate graph renditions", step, stepMax);
                var graphRenditionsResult = await GenerateGraphRenditions(reqVars, projectVars);
                if (!graphRenditionsResult.Success)
                {
                    appLogger.LogError($"Failed rendering graph renditions. project id: {projectVars.projectId}, {graphRenditionsResult.LogToString()}");
                    await _cloneProjectPropertiesMessageToClient("ERROR: Failed to render graph renditions");
                }

                //
                // => Update the local CMS metadata object
                //
                await UpdateCmsMetadata();

                //
                // => Clear the user section information cache data
                //
                UserSectionInformationCacheData.Clear();

                // Reset the global variable
                ProjectCreationInProgress = false;

                // Send the result of the synchronization process back to the server (use the message field to transport the target project id)
                await MessageToCurrentClient("CloneProjectDone", targetProjectId, baseDebugInfo);
            }
            // To make sure HandleError is correctly executed
            catch (HttpStatusCodeException)
            {
                // Reset the global variable
                ProjectCreationInProgress = false;

                throw;
            }
            catch (Exception ex)
            {
                // Reset the global variable
                ProjectCreationInProgress = false;

                appLogger.LogError(ex, $"There was an error cloning the project. stack-trace: {GetStackTrace()}");

                // Pretty dirty way of getting useful output of the error message to the user
                var clientErrorMessage = $"There was an error cloning the project.";
                if (ex.ToString().Contains("System.Exception") && ex.ToString().Contains("(article ID:") && ex.ToString().Contains(" at Taxxor.Project.ProjectLogic"))
                {
                    clientErrorMessage += ex.ToString().Replace("System.Exception:", "").SubstringBefore(" at Taxxor.Project.ProjectLogic");
                }

                var errorResult = new TaxxorReturnMessage(false, clientErrorMessage, $"error: {ex}");
                await MessageToCurrentClient("CloneProjectDone", errorResult);
            }
        }

        /// <summary>
        /// Calculates a project ID
        /// </summary>
        /// <param name="baseProjectId"></param>
        /// <returns></returns>
        public static string CalculateUniqueProjectId(string baseProjectId)
        {
            // Calculate a project ID for the new project based on the reporting period that was passed
            string? calculatedProjectId = null;
            var newProjectIdFound = false;
            var counter = 0;
            while (!newProjectIdFound)
            {
                var potentialTargetProjectId = (counter == 0) ? $"{baseProjectId}" : $"{baseProjectId}-{counter.ToString()}";
                var nodeProject = xmlApplicationConfiguration.SelectSingleNode($"//cms_projects/cms_project[@id='{potentialTargetProjectId}']");
                if (nodeProject == null)
                {
                    calculatedProjectId = potentialTargetProjectId;
                    newProjectIdFound = true;
                }
                counter++;
            }

            return calculatedProjectId;
        }




        /// <summary>
        /// Import mapping clusters from another project
        /// </summary>
        /// <param name="sourceProjectId">Source project ID</param>
        /// <param name="targetProjectId">Target project ID (where the mappings will be imported to)</param>
        /// <param name="factIds">List of SDE fact ID's that we need to import into the target project</param>
        /// <param name="overrideTargetMappings">'true' forces the process to push the source mappings to the target even if they already exist</param>
        /// <param name="debugRoutine">Log debug information on the console and save in-between results for inspection</param>
        /// <param name="step">Use this to send websocket messages back about the progress of the process</param>
        /// <param name="stepMax">Use this to send websocket messages back about the progress of the process</param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> ImportMappingClusters(string sourceProjectId, string targetProjectId, List<string> factIds, bool overrideTargetMappings, bool debugRoutine, int step = -1, int stepMax = -1)
        {
            var fixEfrReferences = false;
            var reportType = RetrieveReportTypeIdFromProjectId(sourceProjectId);
            var factIdDebug = "31f9ca42-26f8-465f-8571-a67b0c5cf54c";
            var debugInfo = "";


            if (factIds.Count == 0) return new TaxxorReturnMessage(true, "No need to import any mapping clusters as the fact ID's list is empty");

            //
            // => Retrieve meta information about the source and target projects
            //
            var sourceProjectPeriodMetadata = new ProjectPeriodProperties(sourceProjectId);
            var targetProjectPeriodMetadata = new ProjectPeriodProperties(targetProjectId);

            if (!targetProjectPeriodMetadata.Success)
            {
                return new TaxxorReturnMessage(false, "Unable to retrieve metadata for this project", $"targetProjectId: {targetProjectId}");
            }
            var projectType = targetProjectPeriodMetadata.ProjectType;


            var sourceProjectReportingPeriod = sourceProjectPeriodMetadata.ReportingPeriod;
            int sourceProjectYear = sourceProjectPeriodMetadata.CurrentProjectYear;
            int sourceProjectQuarter = sourceProjectPeriodMetadata.CurrentProjectQuarter;
            DateTime sourceProjectEndDate = _getPeriodEndDate(sourceProjectYear, sourceProjectQuarter);

            var targetProjectReportingPeriod = targetProjectPeriodMetadata.ReportingPeriod;
            int targetProjectYear = targetProjectPeriodMetadata.CurrentProjectYear;
            int targetProjectQuarter = targetProjectPeriodMetadata.CurrentProjectQuarter;
            DateTime targetProjectEndDate = _getPeriodEndDate(targetProjectYear, targetProjectQuarter);

            // if (debugRoutine)
            // {
            //     appLogger.LogDebug("****************");
            //     appLogger.LogDebug($"sourceProjectReportingPeriod: {sourceProjectReportingPeriod}");
            //     appLogger.LogDebug($"sourceProjectYear: {sourceProjectYear}");
            //     appLogger.LogDebug($"sourceProjectQuarter: {sourceProjectQuarter}");
            //     appLogger.LogDebug($"sourceProjectEndDate: {sourceProjectEndDate.ToString()}");

            //     appLogger.LogDebug($"targetProjectReportingPeriod: {targetProjectReportingPeriod}");
            //     appLogger.LogDebug($"targetProjectYear: {targetProjectYear}");
            //     appLogger.LogDebug($"targetProjectQuarter: {targetProjectQuarter}");
            //     appLogger.LogDebug($"targetProjectEndDate: {targetProjectEndDate.ToString()}");

            //     appLogger.LogDebug("****************");
            // }


            // return new TaxxorReturnMessage(false, "Thrown on purpose");

            //
            // => Clone the mapping clusters
            //
            if (step > -1) step = await _cloneProjectPropertiesMessageToClient("Clone the structured data elements for the new project", step, stepMax);
            var xmlCloneMappingClustersResult = await MappingService.CloneAllMappingClusters(sourceProjectId, targetProjectId, factIds, overrideTargetMappings, debugRoutine);

            // appLogger.LogDebug("*** MAPPING CLONE RESULT ***");
            // appLogger.LogDebug(PrettyPrintXml(xmlCloneMappingClustersResult));
            // appLogger.LogDebug("****************");

            var cloneMappingsResult = new TaxxorReturnMessage(xmlCloneMappingClustersResult);

            // return new TaxxorReturnMessage(false, "Thrown on purpose");

            if (!cloneMappingsResult.Success)
            {
                return new TaxxorReturnMessage(xmlCloneMappingClustersResult);
            }
            else
            {
                if (cloneMappingsResult.Message.ToLower().Contains("warning"))
                {
                    debugInfo += cloneMappingsResult.Message;
                }
            }

            //
            // => Change the EFR references so that they point to the correct dataset and dimensions
            //

            // Check if there is a new reporting period set
            var reportingPeriodChanged = false;
            // TODO: Check if this report-type needs auto-shift date
            if (targetProjectReportingPeriod != sourceProjectReportingPeriod && (reportType.Contains("annual") || reportType.Contains("quarterly"))) reportingPeriodChanged = true;

            if (fixEfrReferences && reportingPeriodChanged)
            {
                // TODO: This is a temporary measure until this is properly fixed in the Sturctured Data Store
                var failedUpdateFactIds = new List<string>();

                step = await _cloneProjectPropertiesMessageToClient("Changing source data references for the structured data elements", step, stepMax);

                var datasetSearchString = "";
                var datasetReplaceString = "";
                var dimensionSearchString = $"_{sourceProjectEndDate.ToString("yyyyMMdd")}";
                var dimensionReplaceString = $"_{targetProjectEndDate.ToString("yyyyMMdd")}";
                if (projectType == "qr")
                {
                    datasetSearchString = $" Q{sourceProjectQuarter} {sourceProjectYear}";
                    datasetReplaceString = $" Q{targetProjectQuarter} {targetProjectYear}";
                }
                else
                {
                    datasetSearchString = $" AR {sourceProjectYear}";
                    datasetReplaceString = $" AR {targetProjectYear}";
                }

                var sourceReplacements = new Dictionary<string, string>
                {
                    { datasetSearchString, datasetReplaceString },
                    { dimensionSearchString, dimensionReplaceString }
                };

                // - Update the mapping clusters one-by-one
                foreach (var factId in factIds)
                {
                    if (factId == factIdDebug)
                    {
                        appLogger.LogInformation("Debugging factId");
                    }
                    var xmlUpdateResult = await MappingService.ChangeMappingCluster(targetProjectId, factId, null, sourceReplacements, null, debugRoutine);
                    if (XmlContainsError(xmlUpdateResult))
                    {
                        failedUpdateFactIds.Add(factId);
                        appLogger.LogWarning($"Could not update mapping of factId {factId}");
                    }
                }

                // - More than 20% of the SDE could not be updated - throw an error
                if ((failedUpdateFactIds.Count / factIds.Count) > 0.2)
                {
                    return new TaxxorReturnMessage(false, "Error replacing source data entries in structured data elements", $"failed-fact-ids: {string.Join(",", failedUpdateFactIds.ToArray())}");
                }
            }


            return new TaxxorReturnMessage(true, "Successfully imported the mapping clusters", $"{debugInfo}, factIds.Count: {factIds.Count}");
        }

        /// <summary>
        /// Utility function to send a message over web sockets to the client
        /// </summary>
        /// <param name="message"></param>
        /// <param name="step"></param>
        /// <param name="maxStep"></param>
        /// <returns></returns>
        private static async Task<int> _cloneProjectPropertiesMessageToClient(string message, int step = -1, int maxStep = -1)
        {
            if (step == -1)
            {
                await MessageToCurrentClient("CloneProjectProgress", message);
            }
            else
            {
                await MessageToCurrentClient("CloneProjectProgress", $"Step {step}/{maxStep}: {message}");
                step++;
            }

            return step;
        }


        /// <summary>
        /// Default handler when something goes wrong with cloning projects
        /// </summary>
        /// <param name="newProjectId"></param>
        /// <param name="baseMessage"></param>
        /// <param name="baseDebugInfo"></param>
        /// <returns></returns>
        private static async Task<TaxxorReturnMessage> _HandleProjectCloneError(string newProjectId, string baseMessage, string baseDebugInfo)
        {
            appLogger.LogError($"Error while cloning project\nmessage: {baseMessage}\ndebuginfo: {baseDebugInfo}\nstack-trace: {GetStackTrace()}");

            // Reset the global variable
            ProjectCreationInProgress = false;

            // Cleanup everything that we have potentially setup on the server
            var projectDeleteResult = await DeleteProject(newProjectId, true, false);

            // Define the debuginfo message
            var errorMessage = $"{baseMessage}, but all traces have been cleaned up";
            var errorDebugInfo = $"stack-trace: {GetStackTrace()}";

            if (projectDeleteResult.Success)
            {
                // appLogger.LogWarning($"{errorMessage}, debuginfo: {errorDebugInfo}");
                throw new Exception(errorMessage);
            }
            else
            {
                errorMessage = $"{baseMessage} and there are potentially traces left on the Taxxor Data Store";
                appLogger.LogWarning($"{errorMessage}, debuginfo: {errorDebugInfo}");
                throw new Exception(errorMessage);
            }
        }

    }
}