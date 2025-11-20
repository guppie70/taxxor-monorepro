#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;


using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace Taxxor.Project
{

    /// <summary>
    /// Contains information about an ERP import run
    /// </summary>
    public class ErpImportRunDetails : BackgroundTaskRunDetails
    {


        public ErpImportRunDetails()
        {
            this.StartTime = DateTime.Now;
            this.EpochStartTime = ToUnixTime(this.StartTime);
        }

        public override async Task Done(ProjectVariables projectVars, bool success)
        {
            this.Success = success;
            this.EndTime = DateTime.Now;
            this.EpochEndTime = ToUnixTime(this.EndTime);

            //
            // => Loop through the log and inject the Epoch End Time into the entry containing data-epochend=\"\"
            //
            for (int i = 0; i < this.Log.Count; i++)
            {
                if (this.Log[i].Contains("data-epochend=\""))
                {
                    this.Log[i] = ReplaceAttributeContent(this.Log[i], "data-epochend", this.EpochEndTime.ToString());
                }
            }

            //
            // => Store the complete ERP import state on the disk of the server so that it can be used in the version manager
            //
            try
            {

                // Store the log of the latest run in a JSON file in the document store
                var currentImportStatus = ErpImportStatus[this.RunId];
                var json = System.Text.Json.JsonSerializer.Serialize(currentImportStatus, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var successSave = Taxxor.ConnectedServices.DocumentStoreService.FilingData.Save<bool>(json, projectVars.projectId, $"/textual/logs/import-erp.json", "cmsdataroot", true, false, projectVars).GetAwaiter().GetResult();

                if (!successSave)
                {
                    appLogger.LogError("Could not save ERP import log to the document store");
                }
                else
                {

                    // Commit the new and updated structured data cache files in the version control system
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "pid", projectVars.projectId },
                        { "vid", "latest" },
                        { "type", "text" },
                        { "linknames", "none" },
                        { "sectionids", "import-erp" },
                        { "crud", "importstructureddata" }
                    };

                    XmlDocument responseXml = CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "contentversion", dataToPost, false, false, projectVars).GetAwaiter().GetResult();

                    if (responseXml.OuterXml.Contains("nothing to commit"))
                    {
                        appLogger.LogWarning("No changes were detected in the cache system by the version control application");
                    }
                    else
                    {
                        // Handle error
                        if (XmlContainsError(responseXml))
                        {
                            appLogger.LogError($"There was an error committing the import log to the version control system. stack-trace: {GetStackTrace()}");
                        }
                    }

                    // Create a version in the version control system
                    var versionCreationResult = GenerateVersion(projectVars.projectId, "<posterpimport>Post ERP structured data import</posterpimport>", false, projectVars).GetAwaiter().GetResult();
                    if (!versionCreationResult.Success)
                    {
                        appLogger.LogError($"There was an error creating a version in the version control system. error: {versionCreationResult.ToString()}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        appLogger.LogInformation($"Successfully created minor version version {versionCreationResult.ToString()}");
                    }

                }

            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error storing ErpImportStatus in the DocumentStore and creating a version");
            }

            //
            // => Communicate with connected clients
            //
            await CommunicateErpImportProgress();

            //
            // => Remove the object from the ErpImportStatus dictionary
            //
            if (!ErpImportStatus.TryRemove(this.RunId, out _))
            {
                appLogger.LogError($"Failed to remove ErpImportStatus object for {RunId}. ");
            }
        }

        public override async Task AddLog(string log)
        {
            this.Log.Add(log);

            // Communicate with connected clients
            await CommunicateErpImportProgress();
        }

        /// <summary>
        /// Updates the connected clients through the SignalR hub with the status
        /// </summary>
        private async Task CommunicateErpImportProgress()
        {
            try
            {
                var logForUi = string.Join("\n", this.Log);
                await System.Web.SignalRHub.Current.Clients.All.SendAsync("ErpImportProgress", ProjectId, logForUi, Progress, SystemState.ErpImport.IsRunning, Success);
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error sending ERP import status update message to client");
            }
        }

    }

    /// <summary>
    /// Settings to use for the ERP Import
    /// </summary>
    public class ErpImportSettings(ProjectLogic.ProjectVariables projectVars)
    {
        public ProjectVariables ProjectVars { get; set; } = projectVars;

        public string RunId { get; set; } = Guid.NewGuid().ToString();

        public bool PartOfSdsSync { get; set; } = false;

        public bool ForceFullImport { get; set; } = false;

        /// <summary>
        /// Defines if we need to add a delay after the import is done to make sure the SDS can sync with the new data we have imported
        /// </summary>
        /// <value></value>
        public bool AddCooldownPeriod { get; set; } = false;
    }

    /// <summary>
    /// ERP import class containing the logic for the ERP Import process
    /// </summary>
    /// <typeparam name="ErpImportService"></typeparam>
    public class ErpImportService(ILogger<ErpImportService> logger) : IErpImportingService
    {
        private ConcurrentDictionary<string, int> _dataSetsCounter { get; set; } = new ConcurrentDictionary<string, int>();

        private readonly ILogger<ErpImportService> _logger = logger;

        private readonly SemaphoreSlim _semaphore = new(1, 1);


        public async Task<bool> Import(ErpImportSettings settings)
        {
            var useSemaphore = false;
            try
            {

                if (settings.ProjectVars.projectId == null)
                {
                    throw new Exception("Project ID send to ERP import background process is null");
                }

                // Retrieve the import details object
                var runId = settings?.RunId ?? "willnevermatch";
                var erpImportDetails = ErpImportStatus[runId];
                erpImportDetails.RunId = runId;

                // Update the system state
                await UpdateErpImportState(true, settings?.ProjectVars?.projectId);

                switch (TaxxorClientId)
                {
                    case "philips":
                    case "taxxor2":
                        //
                        // => Start the EFR import process
                        //
                        var importResult = await ImportEfrData(settings ?? new ErpImportSettings(new ProjectVariables()));
                        if (!importResult.Success)
                        {
                            _logger.LogError(importResult.Message);
                            // erpImportDetails.AddLog("EFR import failed: " + importResult.Message);
                        }
                        else
                        {
                            _logger.LogInformation(importResult.Message);
                            // erpImportDetails.AddLog("EFR import succeeded: " + importResult.Message);
                        }
                        await UpdateErpImportState(false, null);
                        return true;

                    default:
                        await UpdateErpImportState(false, null);
                        erpImportDetails.Success = false;
                        await erpImportDetails.Done(settings?.ProjectVars ?? new ProjectVariables(), false);
                        _logger.LogWarning($"ErpImportService.Import: Unknown Taxxor Client Id: {TaxxorClientId}");
                        return true;
                }

            }
            catch (Exception ex)
            {
                SystemState.ErpImport.IsRunning = false;
                SystemState.ErpImport.ProjectId = null;
                var errorMessage = $"Error occured during ERP import";
                _logger.LogError(ex, errorMessage);
                return false;
            }

            async Task UpdateErpImportState(bool isRunning, string? projectId)
            {
                if (useSemaphore)
                {
                    try
                    {
                        await _semaphore.WaitAsync();
                        SystemState.ErpImport.IsRunning = isRunning;
                        SystemState.ErpImport.ProjectId = projectId;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occured while waiting for semaphore");
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                else
                {
                    SystemState.ErpImport.IsRunning = isRunning;
                    SystemState.ErpImport.ProjectId = projectId;
                }
            }

        }


        /// <summary>
        /// Imports the EFR data into the Taxxor Structured Data Store
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        private async Task<TaxxorReturnMessage> ImportEfrData(ErpImportSettings settings)
        {
            var debugRoutine = true;
            var errorMessage = "Unable to start EFR import process";

            // Retrieve the import details object
            var runId = settings.RunId;
            var erpImportDetails = ErpImportStatus[runId];



            erpImportDetails.ProjectId = settings.ProjectVars.projectId;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            // erpImportDetails.Log.Clear();

            try
            {
                // Retrieve the project name
                var projectName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{settings?.ProjectVars?.projectId}']/name")?.InnerText ?? "unknown";

                // Initial log lines
                if (debugRoutine) _logger.LogCritical("---------- START EFR IMPORT ----------");
                await erpImportDetails.AddLog($"Start importing EFR data for project: {projectName}");


                // Reset the number of datasets that we are importing
                var cacheKey = RenderKey();
                _dataSetsCounter.AddOrUpdate(cacheKey, 0, (key, value) => value = 0);


                //
                // => Determine the pattern of the datasets to import
                //

                // Retrieve date fragments from the project variables
                var (Year, Quarter, Month) = ParseProjectIdInDateFragments();

                // Q1, Q2, Q3, AR (Q4 gebruikt de AR set), M1, M2, M4, M5, M7, M8, M10, M11 (M3/6/9 gebruiken Q1/2/3, en M12 gebruikt AR)
                var datasetPattern = "* ";
                switch (settings?.ProjectVars?.reportTypeId)
                {
                    case "monthly-report":

                        switch (Month)
                        {
                            case 3:
                                datasetPattern += $"Q1 {Year.ToString()}";
                                break;
                            case 6:
                                datasetPattern += $"Q2 {Year.ToString()}";
                                break;
                            case 9:
                                datasetPattern += $"Q3 {Year.ToString()}";
                                break;
                            case 12:
                                datasetPattern += $"AR {Year.ToString()}";
                                break;
                            default:
                                // "* M1 2024"
                                datasetPattern += $"M{Month.ToString()} {Year.ToString()}";
                                break;
                        }

                        break;

                    case "philips-quarterly-report":
                    case "quarterly-report":

                        switch (Quarter)
                        {
                            case 4:
                                datasetPattern += "AR";
                                break;
                            default:
                                datasetPattern += $"Q{Quarter.ToString()}";
                                break;
                        }

                        datasetPattern += $" {Year.ToString()}";

                        break;

                    case var typeAr when typeAr != null && typeAr.Contains("annual-report"):
                        datasetPattern += $"AR {Year.ToString()}";

                        break;

                    default:
                        throw new Exception($"Unsupported ERP import report type: {settings?.ProjectVars?.reportTypeId}");

                }
                _logger.LogWarning($"datasetPattern: {datasetPattern}");

                //
                // => Dertermine the start date
                //
                var startDate = "";
                switch (TaxxorClientId)
                {

                    case "philips":
                    case "taxxor":
                        // - Determine report end date
                        DateTime dateReportEnd = new DateTime();
                        var isJanOrFeb = Month == 1 || Month == 2;


                        if (isJanOrFeb && settings?.ProjectVars?.reportTypeId != "monthly-report")
                        {
                            dateReportEnd = _getPeriodEndDate(Year);
                        }
                        else
                        {
                            switch (settings?.ProjectVars?.reportTypeId)
                            {
                                case "monthly-report":
                                    dateReportEnd = new DateTime(Year, Month, 1);
                                    break;

                                case "philips-quarterly-report":
                                case "quarterly-report":
                                    dateReportEnd = _getPeriodEndDate(Year, Quarter);
                                    break;

                                case var typeAr when typeAr != null && typeAr.Contains("annual-report"):
                                    dateReportEnd = _getPeriodEndDate(Year);
                                    break;

                                default:
                                    throw new Exception($"Unsupported ERP import report type: {settings?.ProjectVars?.reportTypeId}");
                            }
                        }

                        switch (settings?.ProjectVars?.reportTypeId)
                        {
                            case "monthly-report":
                                var deteStartCalculated = dateReportEnd.AddMonths(-1);
                                startDate = createIsoTimestamp(deteStartCalculated, true);
                                break;

                            default:
                                // - Compare with current date to dynamically calculate the start date for the ERP data import
                                if (DateTime.Today > dateReportEnd)
                                {
                                    // The date of the import needs to be two months befor the end date of the report or 2023-12-01 in case we are in Jan/Feb
                                    // _logger.LogCritical($"Current date is BEYOND annual report end date: {createIsoTimestamp(dateReportEnd, false)}");
                                    var dateTwoMonthsAgo = dateReportEnd.AddMonths(isJanOrFeb ? 0 : -2);
                                    var dateTwoMonthsAgoStart = new DateTime(dateTwoMonthsAgo.Year, dateTwoMonthsAgo.Month, 1);
                                    startDate = createIsoTimestamp(dateTwoMonthsAgoStart, true);
                                }
                                else
                                {
                                    // _logger.LogCritical($"Current date is BEFORE annual report end date: {createIsoTimestamp(dateReportEnd, false)}");
                                    // Import starts two months ago
                                    var dateTwoMonthsAgo = DateTime.Today.AddMonths(-2);
                                    var dateTwoMonthsAgoStart = new DateTime(dateTwoMonthsAgo.Year, dateTwoMonthsAgo.Month, 1);
                                    startDate = createIsoTimestamp(dateTwoMonthsAgoStart, true);
                                }
                                break;
                        }

                        break;

                    default:
                        throw new Exception($"ERP import not implemented for {TaxxorClientId}");
                }

                if (debugRoutine) _logger.LogCritical($"- startDate: {startDate} -");


                //
                // => Start the import process
                //
                CustomHttpHeaders customHttpHeaders = new();
                if (!string.IsNullOrEmpty(SystemUser))
                {
                    customHttpHeaders.AddTaxxorUserInformation(SystemUser);
                }

                var baseApiUri = $"{RetrieveErpServiceUrl()}/api/datasets/update";

                var dataToPost = new Dictionary<string, string?>
                                {
                                    { "set", datasetPattern },
                                    { "start", startDate },
                                    { "updateonchange", "true" }
                                };

                if (settings != null && settings.ForceFullImport) dataToPost.Add("fullupdate", "true");
                var fullApiUri = QueryHelpers.AddQueryString(baseApiUri, dataToPost);
                Console.WriteLine($"ERP import apiUrl: {fullApiUri}");

                // throw new Exception($"Thrown on purpose. (datasetPattern: {datasetPattern})");

                using HttpClient http2ClientEfrStart = CreateSimpleHttp2Client(fullApiUri);

                http2ClientEfrStart.DefaultRequestHeaders.Clear();
                http2ClientEfrStart.DefaultRequestHeaders.Add("Accept", "text/xml");
                http2ClientEfrStart.DefaultRequestHeaders.Add("X-Tx-UserId", SystemUser);

                /*
                curl -H "Accept: text/xml" -H "X-Tx-UserId: system@taxxor.com" http://philipsefrdataservice:4815/api/datasets/update?set=*%20M2%202024&start=2024-01-01&updateonchange=true
                */

                using HttpResponseMessage resultEfrStart = await http2ClientEfrStart.GetAsync(fullApiUri);
                var httpResponseStatusCodeEfrStart = (int)resultEfrStart.StatusCode;
                if (debugRoutine) _logger.LogInformation($"httpResponseStatusCodeEfrStart: {httpResponseStatusCodeEfrStart}");

                if (!resultEfrStart.IsSuccessStatusCode)
                {

                    _logger.LogCritical($"ERP import failed with status code: {httpResponseStatusCodeEfrStart}");

                    if (settings != null) await erpImportDetails.Done(settings.ProjectVars, false);
                    await SystemState.ErpImport.Reset(true);
                    return new TaxxorReturnMessage(false, "Failed to start the EFR import process.");
                }

                SystemState.ErpImport.IsRunning = true;
                SystemState.ErpImport.ProjectId = settings?.ProjectVars?.projectId;

                if (debugRoutine) _logger.LogCritical($"- EFR import started (baseApiUri: {baseApiUri}) -");



                //
                // => Monitor the progress of the import
                //
                if (debugRoutine) _logger.LogCritical($"- START POLLING PHASE -");

                await erpImportDetails.AddLog("Start monitoring the EFR import process");

                var refreshFrequencyInSeconds = 2;
                var maxRunningTimeInHours = 5;
                var maxPollingFailures = 2;
                var currentPollingFailures = 0;
                errorMessage = "Unable to retrieve the progress of the EFR import process";
                baseApiUri = $"{RetrieveErpServiceUrl()}/api/update/tasks";

                /*
                curl -H "Accept: text/xml" -H "X-Tx-UserId: system@taxxor.com" http://philipsefrdataservice:4815/api/update/tasks
                */


                // Set a timestamp for two hours from now
                DateTime stopTime = DateTime.Now.AddHours(maxRunningTimeInHours);

                // Loop to poll the progress of the EFR import process
                var previousDatasetNumber = 0;
                DateTime iterationStartTime = DateTime.Now;
                TimeSpan split = TimeSpan.Zero;
                while (DateTime.Now < stopTime)
                {
                    using HttpClient http2ClientEfrProgress = CreateSimpleHttp2Client(baseApiUri);

                    http2ClientEfrProgress.DefaultRequestHeaders.Clear();
                    http2ClientEfrProgress.DefaultRequestHeaders.Add("Accept", "text/xml");
                    http2ClientEfrProgress.DefaultRequestHeaders.Add("X-Tx-UserId", SystemUser);

                    using HttpResponseMessage result = await http2ClientEfrProgress.GetAsync(baseApiUri);
                    var httpResponseStatusCode = (int)result.StatusCode;
                    string resultContent = await result.Content.ReadAsStringAsync();
                    // _logger.LogInformation($"httpResponseStatusCode: {httpResponseStatusCode}");
                    // _logger.LogInformation($"resultContent: {resultContent}");



                    if (result.IsSuccessStatusCode)
                    {
                        try
                        {
                            var xmlResponse = new XmlDocument();
                            xmlResponse.LoadXml(resultContent);
                            var numberOfDatasetsToImport = Convert.ToInt32(xmlResponse?.DocumentElement?.InnerText?.Trim() ?? "-1");

                            // Store the maximum number of datasets to be imported in the ConcurrentDictionary
                            // The initial response provides the maximum number of datasets to import
                            int totalNumberOfDatasetsToBeImported;
                            if (!_dataSetsCounter.TryGetValue(cacheKey, out totalNumberOfDatasetsToBeImported)) _logger.LogError($"Unable to retrieve item with key '{cacheKey}' from the concurrent dictionary");
                            if (totalNumberOfDatasetsToBeImported == 0)
                            {
                                totalNumberOfDatasetsToBeImported = (int)numberOfDatasetsToImport;
                                _dataSetsCounter.AddOrUpdate(cacheKey, totalNumberOfDatasetsToBeImported, (key, value) => value = totalNumberOfDatasetsToBeImported);
                            }



                            if (numberOfDatasetsToImport > 0)
                            {
                                var currentDatasetNumber = totalNumberOfDatasetsToBeImported - numberOfDatasetsToImport + 1;

                                // _logger.LogCritical($"===> currentDatasetNumber: {currentDatasetNumber}, totalNumberOfDatasetsToBeImported: {totalNumberOfDatasetsToBeImported}, numberOfDatasetsToImport: {numberOfDatasetsToImport} <==");

                                // Calculate the progress of the EFR import process
                                double overallCounter = 1;
                                if (settings != null && settings.PartOfSdsSync) overallCounter = 2;
                                SystemState.ErpImport.Progress = ((double)(currentDatasetNumber - 1) / (double)totalNumberOfDatasetsToBeImported) / overallCounter;

                                if (currentDatasetNumber != previousDatasetNumber)
                                {
                                    // End time of the current iteration
                                    DateTime iterationEndTime = DateTime.Now;

                                    // Calculate and report on the duration of this iteration
                                    TimeSpan iterationDuration = iterationEndTime - iterationStartTime;
                                    var formattedTime = string.Format("{0}:{1:D2}", (int)iterationDuration.TotalMinutes, iterationDuration.Seconds);

                                    if (previousDatasetNumber > 0) await erpImportDetails.AddLog($"=> Dataset import took {formattedTime} minutes");

                                    iterationStartTime = DateTime.Now;
                                    previousDatasetNumber = currentDatasetNumber;
                                }

                                // Make sure the system state is updated
                                SystemState.ErpImport.IsRunning = true;

                                // Still in progress
                                await erpImportDetails.AddLog($"Importing dataset {(totalNumberOfDatasetsToBeImported - numberOfDatasetsToImport + 1).ToString()} of {totalNumberOfDatasetsToBeImported.ToString()}");
                            }
                            else
                            {
                                // Make sure the system state is updated
                                SystemState.ErpImport.IsRunning = false;

                                // Import done
                                var formatedTime = string.Format("{0}:{1:D2}", (int)watch.Elapsed.TotalMinutes, watch.Elapsed.Seconds);
                                await erpImportDetails.AddLog($"[<span data-epochend=\"{ToUnixTime(DateTime.Now)}\">{GenerateLogTimestamp(false)}</span>]\nERP data import finalized successfully in {formatedTime} minutes");

                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, errorMessage);

                            if (settings != null) await erpImportDetails.Done(settings.ProjectVars, false);
                            await SystemState.ErpImport.Reset(true);
                            return new TaxxorReturnMessage(false, errorMessage, $"httpResponseStatusCode: {httpResponseStatusCode}");
                        }
                    }
                    else
                    {
                        _logger.LogError($"Polling failed. (uri: {baseApiUri}, httpResponseStatusCode: {httpResponseStatusCode})");
                        currentPollingFailures++;
                        if (currentPollingFailures >= maxPollingFailures)
                        {

                            if (settings != null) await erpImportDetails.Done(settings.ProjectVars, false);
                            await SystemState.ErpImport.Reset(true);
                            return new TaxxorReturnMessage(false, errorMessage, $"httpResponseStatusCode: {httpResponseStatusCode}");
                        }
                    }


                    // Wait for a period before polling again
                    await Task.Delay(refreshFrequencyInSeconds * 1000);
                }

                if (debugRoutine) _logger.LogCritical($"- END POLLING PHASE -");

                // If the loop exits due to two hours limit, print a message
                if (DateTime.Now >= stopTime)
                {
                    errorMessage = $"Max EFR polling exceeded {maxRunningTimeInHours} hours limit. (uri: {baseApiUri})";

                    _logger.LogError(errorMessage);

                    if (settings != null) await erpImportDetails.Done(settings.ProjectVars, false);
                    await SystemState.ErpImport.Reset(true);
                    return new TaxxorReturnMessage(false, errorMessage);
                }

                // Add a delay doing nothing to give the SDS time to process the data
                if (settings != null && settings.AddCooldownPeriod)
                {
                    await erpImportDetails.AddLog($"Waiting for database processing to complete");
                    await PauseExecution(15 * 1000);
                    await erpImportDetails.AddLog($"Finalizing ERP import");
                }


                if (debugRoutine) _logger.LogCritical("---------- END EFR IMPORT ----------");

                if (settings != null) await erpImportDetails.Done(settings.ProjectVars, true);
                await SystemState.ErpImport.Reset(true);
                return new TaxxorReturnMessage(true, "Successfully imported EFR data");
            }
            catch (Exception ex)
            {
                errorMessage = $"Error occured during EFR import";
                _logger.LogError(ex, errorMessage);

                if (settings != null)
                {
                    await erpImportDetails.AddLog(errorMessage);
                    await erpImportDetails.Done(settings.ProjectVars, false);
                }

                await SystemState.ErpImport.Reset(true);
                return new TaxxorReturnMessage(false, errorMessage);
            }



            /// <summary>
            /// Retrieves the base URI of the ERP connector
            /// </summary>
            /// <returns></returns>
            string? RetrieveErpServiceUrl()
            {
                var serviceId = "philipsdataconnector";
                var xPath = $"/configuration/taxxor/components/service//*[(local-name()='service' or local-name()='web-application') and @id='{serviceId}']";

                XmlNode? serviceNode = xmlApplicationConfiguration.SelectSingleNode(xPath);

                if (serviceNode == null)
                {
                    WriteErrorMessageToConsole($"Could not find information for service ID: {serviceId}", $"xPath: {xPath} and returned no results, stack-info: {CreateStackInfo()}");
                    return null;
                }
                else
                {
                    var nodeLocation = (serviceNode.SelectSingleNode("uri/domain") == null) ? serviceNode.SelectSingleNode("uri") : serviceNode.SelectSingleNode($"uri/domain[@type='{siteType}']");
                    return nodeLocation?.InnerText;
                }
            }

            /// <summary>
            /// Renders a dictionary key to be used in the ERP datasets counter
            /// </summary>
            /// <param name="projectVars"></param>
            /// <returns></returns>
            string RenderKey()
            {
                return $"{settings?.ProjectVars?.currentUser?.Id}__{settings?.ProjectVars?.projectId}";
            }

            /// <summary>
            /// Uses project ID to parse date fragments
            /// </summary>
            /// <returns></returns>
            (int Year, int Quarter, int Month) ParseProjectIdInDateFragments()
            {
                var year = -1;
                var quarter = -1;
                var month = -1;

                switch (settings?.ProjectVars?.reportTypeId)
                {
                    case "monthly-report":
                        {
                            // Parse the project ID into month and year
                            Match match = Regex.Match(settings?.ProjectVars?.projectId ?? "", @"^m(\d\d)(\d\d)(\-\d+)?$", RegexOptions.IgnoreCase);

                            if (match.Success)
                            {
                                string monthString = match.Groups[1].Value;
                                string yearString = "20" + match.Groups[2].Value;
                                _logger.LogInformation($"month: {monthString}, year: {yearString}");

                                year = int.Parse(yearString);
                                month = int.Parse(monthString);
                            }
                            else
                            {
                                throw new Exception($"Unable to parse monthly report date from {settings?.ProjectVars?.reportTypeId}");
                            }
                        }

                        break;

                    case "philips-quarterly-report":
                    case "quarterly-report":
                        {
                            // Parse the project ID into quarter and year
                            Match match = Regex.Match(settings?.ProjectVars?.projectId ?? "", @"^q(\d)(\d\d)(\-\d+)?$", RegexOptions.IgnoreCase);

                            if (match.Success)
                            {
                                string quarterString = match.Groups[1].Value;
                                string yearString = "20" + match.Groups[2].Value;
                                _logger.LogInformation($"quarter: {quarterString}, year: {yearString}");

                                year = int.Parse(yearString);
                                quarter = int.Parse(quarterString);
                            }
                            else
                            {
                                throw new Exception($"Unable to parse quarterly report date from {settings?.ProjectVars?.reportTypeId}");
                            }
                        }

                        break;

                     case var typeAr when typeAr != null && typeAr.Contains("annual-report"):
                        {
                            // Parse the project ID into quarter and year
                            Match match = Regex.Match(settings?.ProjectVars?.projectId ?? "", @"^ar(\d\d)(\-\d+)?$", RegexOptions.IgnoreCase);

                            if (match.Success)
                            {
                                string yearString = "20" + match.Groups[1].Value;
                                _logger.LogInformation($"year: {yearString}");

                                year = int.Parse(yearString);
                            }
                            else
                            {
                                throw new Exception($"Unable to parse annual report date from {settings?.ProjectVars?.reportTypeId}");
                            }
                        }

                        break;
                    default:
                        throw new Exception($"Unsupported ERP import report type: {settings?.ProjectVars?.reportTypeId}");
                }

                return (year, quarter, month);

            }
        }

    }

    public interface IErpImportingService
    {
        Task<bool> Import(ErpImportSettings settings);
    }



}