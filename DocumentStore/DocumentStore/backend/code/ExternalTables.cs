using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{

    /// <summary>
    /// Logic for dealing with complete "external" tables which have been uploaded from Excel
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Syncs external tables (used in API route)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task SyncExternalTables(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var projectId = context.Request.RetrievePostedValue("projectid", RegexEnum.None, true, ReturnTypeEnum.Json);
            var workbookReference = context.Request.RetrievePostedValue("workbookreference", RegexEnum.None, true, reqVars.returnType);
            var workbookId = context.Request.RetrievePostedValue("workbookid", RegexEnum.None, true, reqVars.returnType);

            TaxxorReturnMessage syncResult = await SyncExternalTables(projectId, workbookReference, workbookId);

            if (syncResult.Success)
            {
                await response.OK(GenerateSuccessXml(syncResult.Message, syncResult.DebugInfo), ReturnTypeEnum.Xml, true);
            }
            else
            {
                HandleError(ReturnTypeEnum.Xml, syncResult.Message, syncResult.DebugInfo);
            }
        }

        /// <summary>
        /// Adds or updates a metadata element in the cached xml file
        /// </summary>
        /// <param name="cachedTableFilePathOs"></param>
        /// <param name="metadataName"></param>
        /// <param name="metaDataValue"></param>
        private static void _storeMetaDataInCachedTableFile(string cachedTableFilePathOs, string metadataName, string metaDataValue)
        {
            var cachedExternalHtmlTable = new XmlDocument();

            if (File.Exists(cachedTableFilePathOs))
            {
                try
                {
                    // Load the cached external table content
                    cachedExternalHtmlTable.Load(cachedTableFilePathOs);

                    // Set the metadata key-value in the document
                    _storeMetaDataInCachedTableFile(ref cachedExternalHtmlTable, metadataName, metaDataValue);

                    // Save the document
                    cachedExternalHtmlTable.Save(cachedTableFilePathOs);

                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Could not store metadata in cached external table file. error: {ex}, metadataName: {metadataName}, metaDataValue: {metaDataValue}, stack-trace: {GetStackTrace()}");
                }

            }
            else
            {
                appLogger.LogError($"Could not retrieve cached external table xml file at: {cachedTableFilePathOs}, metadataName: {metadataName}, metaDataValue: {metaDataValue}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Stores a metadata key-value pair in the cached external table file
        /// </summary>
        /// <param name="cachedExternalHtmlTable"></param>
        /// <param name="metadataName"></param>
        /// <param name="metaDataValue"></param>
        private static void _storeMetaDataInCachedTableFile(ref XmlDocument cachedExternalHtmlTable, string metadataName, string metaDataValue)
        {
            // Store the structure update in the target XML file, because this should trigger a warning in the editor
            XmlNode? nodeMeta = null;
            var nodeMetaStructureStatus = cachedExternalHtmlTable.SelectSingleNode($"/html/head/meta[@name='{metadataName}']");
            if (nodeMetaStructureStatus == null)
            {
                nodeMeta = cachedExternalHtmlTable.CreateElement("meta");
                SetAttribute(nodeMeta, "name", metadataName);
                nodeMeta.InnerText = metaDataValue;
                cachedExternalHtmlTable.SelectSingleNode("/html/head").AppendChild(nodeMeta);
            }
            else
            {
                nodeMetaStructureStatus.InnerText = metaDataValue;
            }

        }

        /// <summary>
        /// Syncs external tables - called from the API (logic itself)
        /// Executed when the sync functionality for External Tables is initiated
        /// </summary>
        /// <param name="projectIdToSync"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> SyncExternalTables(string projectIdToSync, string? workbookReferenceToSync = null, string? workbookIdToSync = null)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Global variables
            var retrieveXmlVersion = false;
            var overrideCachedHtmlFiles = true;
            var workbookId = "";
            var currentWorkbookId = "";
            var tablesFound = 0;
            var tablesMatched = 0;
            var tablesSynced = 0;
            var tablesWithoutSync = 0;
            var debugTableId = "xyz";
            var contentLang = "";

            StringBuilder sbDebug = new StringBuilder();
            StringBuilder logSuccess = new StringBuilder();
            var errorMessage = "";
            StringBuilder logError = new StringBuilder();
            var warningMessage = "";
            StringBuilder logWarning = new StringBuilder();

            // Loop through all the projects
            var xPathProjects = "/configuration/cms_projects/cms_project[versions/version/status/text() = 'open']";
            if (projectIdToSync != null) xPathProjects = $"/configuration/cms_projects/cms_project[@id='{projectIdToSync}' and versions/version/status/text() = 'open']";

            var nodeListProjects = xmlApplicationConfiguration.SelectNodes(xPathProjects);
            foreach (XmlNode nodeProject in nodeListProjects)
            {
                var currentProjectId = GetAttribute(nodeProject, "id");

                var nodeCmsProjectExternalDataSets = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{currentProjectId}']/repositories/external_data/sets");
                if (nodeCmsProjectExternalDataSets == null) return new TaxxorReturnMessage(false, $"There was an error syncing the external tables for project with ID: {currentProjectId}", $"error: 'Could not locate external data set', stack-trace: {GetStackTrace()}");

                // Locate a default workbook ID
                var nodeDefaultExternalDataSet = nodeCmsProjectExternalDataSets.SelectSingleNode("set");
                if (nodeDefaultExternalDataSet == null) return new TaxxorReturnMessage(false, $"There was an error syncing the external tables for project with ID: {currentProjectId}", $"error: 'No externa data set defined', stack-trace: {GetStackTrace()}");
                workbookId = GetAttribute(nodeDefaultExternalDataSet, "id");

                Console.WriteLine("************************** START EXTERNAL TABLE SYNC ******************************");
                Console.WriteLine($"currentProjectId: {currentProjectId}");


                List<string> tableXmlPaths = new List<string>();
                List<string> contentXmlPaths = new List<string>();

                try
                {
                    sbDebug.AppendLine($"- currentProjectId: {currentProjectId}");

                    var xmlSectionFolderPathOs = RetrieveContentDataFolderPathOs(currentProjectId);
                    sbDebug.AppendLine($"- xmlSectionFolderPathOs: {xmlSectionFolderPathOs}");

                    contentXmlPaths = RetrieveSectionDataFilePaths(currentProjectId);
                    //sbDebug.AppendLine("- contentXmlPaths: " + String.Join(Environment.NewLine, contentXmlPaths.ToArray()));

                    tableXmlPaths = RetrieveTableDataFilePaths(currentProjectId);
                    //sbDebug.AppendLine("- tableXmlPaths: " + String.Join(Environment.NewLine, tableXmlPaths.ToArray()));

                    // Loop through all the datafiles in the project
                    foreach (string xmlContentFilePathOs in contentXmlPaths)
                    {
                        var htmlTableLoadSuccess = true;

                        // Load the datafile
                        var sectionDataFileReference = Path.GetFileName(xmlContentFilePathOs);
                        var xmlContent = new XmlDocument();
                        xmlContent.Load(xmlContentFilePathOs);


                        var dataFileInUse = IsSectionDataFileInUse(XmlCmsContentMetadata, projectIdToSync, xmlContentFilePathOs);
                        // Console.WriteLine("");
                        // Console.WriteLine("--------");
                        // Console.WriteLine($"{Path.GetFileName(xmlContentFilePathOs)}, dataFileInUse: {dataFileInUse.ToString().ToLower()}");
                        // Console.WriteLine(xPathForFileInUse);
                        // Console.WriteLine("--------");
                        // Console.WriteLine("");

                        // Find external tables
                        var nodeListExternalTables = xmlContent.SelectNodes("/data/content/*//div[contains(@class, 'external-table')]/table");
                        foreach (XmlNode nodeExternalTable in nodeListExternalTables)
                        {
                            contentLang = nodeExternalTable.SelectSingleNode("ancestor::content[@lang]")?.GetAttribute("lang") ?? "unknown";
                            tablesFound++;
                            var externalTableName = "";

                            var externalTableId = GetAttribute(nodeExternalTable, "id");
                            if (string.IsNullOrEmpty(externalTableId))
                            {
                                warningMessage = $"Unable to find the table ID for this external table. table html: {TruncateString(nodeExternalTable.OuterXml, 400)}, sectionDataFileReference: {sectionDataFileReference}, contentLang: {contentLang}";
                                appLogger.LogWarning(warningMessage);
                                logWarning.AppendLine(warningMessage);
                                appLogger.LogWarning($"Not processing this table any further");
                                continue;
                            }
                            else
                            {
                                externalTableId = CalculateBaseExternalTableId(externalTableId);
                            }

                            sbDebug.AppendLine($"- externalTableId: {externalTableId}");

                            // Retrieve the workbook reference from the table from the content
                            var workbookReference = GetAttribute(nodeExternalTable, "data-workbookreference");

                            // Attempt to find a workbook ID by resolving the workbook reference
                            currentWorkbookId = workbookId;
                            if (string.IsNullOrEmpty(workbookReference))
                            {
                                warningMessage = $"Could not retrieve workbook reference, so we will be using the default workbook ID. sectionDataFileReference: {sectionDataFileReference}, contentLang: {contentLang}";
                                appLogger.LogWarning(warningMessage);
                                logWarning.AppendLine(warningMessage);
                            }
                            else
                            {
                                currentWorkbookId = MapWorkbookReferenceToWorkbookId(currentProjectId, workbookReference, "");
                            }

                            if (!string.IsNullOrEmpty(currentWorkbookId))
                            {
                                // Only sync a table when it matches the workbook reference
                                if ((workbookReference == workbookReferenceToSync) || (currentWorkbookId == workbookIdToSync))
                                {
                                    /*
                                    Start the sync for this table
                                    */

                                    tablesMatched++;
                                    // currentProjectId = workbookIdToSync;
                                    appLogger.LogWarning($"Syncing table with ID: {externalTableId}");

                                    var targetExternalHtmlTable = new XmlDocument();
                                    var targetExternalHtmlTableFileName = CalculateCachedExternalTableFileName(externalTableId);
                                    var targetExternalHtmlTableFilePathOs = $"{xmlSectionFolderPathOs}/{targetExternalHtmlTableFileName}";
                                    sbDebug.AppendLine($"- targetExternalHtmlTableFileName: {targetExternalHtmlTableFileName}");

                                    var targetExternalXmlTable = new XmlDocument();
                                    var targetExternalXmlTableFileName = CalculateCachedExternalTableFileName(externalTableId, true);
                                    var targetExternalXmlTableFilePathOs = $"{xmlSectionFolderPathOs}/{targetExternalXmlTableFileName}";
                                    sbDebug.AppendLine($"- targetExternalXmlTableFileName: {targetExternalXmlTableFileName}");

                                    // Retrieve the source content from the server
                                    // A) HTML version
                                    htmlTableLoadSuccess = true;
                                    var sourceExternalHtmlTable = await Taxxor.ConnectedServices.GenericDataConnector.RetrieveExternalTable(currentProjectId, currentWorkbookId, externalTableId, true);
                                    if (XmlContainsError(sourceExternalHtmlTable))
                                    {
                                        _storeMetaDataInCachedTableFile(targetExternalHtmlTableFilePathOs, "sync-source-available", "false");
                                        errorMessage = $"Could not retrieve external html table. response: {sourceExternalHtmlTable.OuterXml}. sectionDataFileReference: {sectionDataFileReference}, contentLang: {contentLang}";
                                        appLogger.LogError(errorMessage);
                                        if (dataFileInUse) logError.AppendLine($"Could not retrieve external HTML table found in {sectionDataFileReference}, tableId: {externalTableId}, workbookId: {currentWorkbookId}, contentLang: {contentLang}");
                                        htmlTableLoadSuccess = false;
                                    }
                                    else
                                    {
                                        _storeMetaDataInCachedTableFile(targetExternalHtmlTableFilePathOs, "sync-source-available", "true");
                                    }

                                    // B) XML version
                                    var sourceExternalXmlTable = new XmlDocument();
                                    if (retrieveXmlVersion)
                                    {
                                        sourceExternalXmlTable = await Taxxor.ConnectedServices.GenericDataConnector.RetrieveExternalTable(currentProjectId, currentWorkbookId, externalTableId, false);
                                        if (XmlContainsError(sourceExternalXmlTable))
                                        {
                                            appLogger.LogError($"Could not retrieve external xml table. response: {sourceExternalXmlTable.OuterXml}. sectionDataFileReference: {sectionDataFileReference}, contentLang: {contentLang}");
                                            if (dataFileInUse) logError.AppendLine($"Could not retrieve external XML table - tableId: {externalTableId}, workbookId: {currentWorkbookId}, contentLang: {contentLang}");
                                        }
                                    }


                                    if (htmlTableLoadSuccess)
                                    {
                                        // Check if a local (cached) copy of the table exists
                                        string targetHtmlTableExists = tableXmlPaths.Find(item => item.EndsWith(targetExternalHtmlTableFileName));
                                        string targetXmlTableExists = tableXmlPaths.Find(item => item.EndsWith(targetExternalXmlTableFileName));

                                        var nodeTableName = sourceExternalHtmlTable.SelectSingleNode("//tableDefinition/metaData/entry[@key='sheet']");
                                        if (nodeTableName != null) externalTableName = nodeTableName.InnerText;

                                        // Clone the existing target table, so that we can use it for comparison purposes even if we force-override it 
                                        var targetExternalHtmlTableCloned = new XmlDocument();
                                        if (string.IsNullOrEmpty(targetHtmlTableExists))
                                        {
                                            targetExternalHtmlTableCloned.LoadXml("<empty/>");
                                        }
                                        else
                                        {
                                            targetExternalHtmlTableCloned.Load(targetExternalHtmlTableFilePathOs);
                                        }

                                        // If not: store the files on the disk
                                        if (string.IsNullOrEmpty(targetHtmlTableExists) || string.IsNullOrEmpty(targetXmlTableExists) || overrideCachedHtmlFiles)
                                        {

                                            if (string.IsNullOrEmpty(targetHtmlTableExists) || overrideCachedHtmlFiles)
                                            {
                                                sbDebug.AppendLine($"* Adding target html table - {targetExternalHtmlTableFileName}");
                                                sourceExternalHtmlTable.Save(targetExternalHtmlTableFilePathOs);
                                            }

                                            if (retrieveXmlVersion)
                                            {
                                                if (string.IsNullOrEmpty(targetXmlTableExists))
                                                {
                                                    sbDebug.AppendLine($"* Adding target xml table - {targetExternalXmlTableFileName}");
                                                    sourceExternalXmlTable.Save(targetExternalXmlTableFilePathOs);
                                                }
                                            }
                                        }

                                        // Load the source data tables
                                        targetExternalHtmlTable.Load(targetExternalHtmlTableFilePathOs);
                                        if (retrieveXmlVersion) targetExternalXmlTable.Load(targetExternalXmlTableFilePathOs);

                                        if (debugRoutine)
                                        {
                                            sourceExternalHtmlTable.Save($"{logRootPathOs}/__sync-external-table-source-{externalTableId}.xml");
                                            targetExternalHtmlTable.Save($"{logRootPathOs}/__sync-external-table-target-{externalTableId}.xml");
                                        }

                                        // Sync the numbers in the table (source is the file we received from the server, target is the file on disk)

                                        // A) Check structure
                                        var structureUpdate = false;

                                        // Check if the number of cells in the body are the same
                                        var sourceNumberOfCells = sourceExternalHtmlTable.SelectNodes("/html/tableDefinition/table/tbody/tr/td").Count;
                                        //var targetNumberOfCells = targetExternalHtmlTableCloned.SelectNodes("/html/tableDefinition/table/tbody/tr/td").Count;
                                        var targetNumberOfCells = nodeExternalTable.SelectNodes("tbody/tr/td").Count;
                                        if (sourceNumberOfCells != targetNumberOfCells) structureUpdate = true;

                                        // Store the structure update in the target XML file, because this should trigger a warning in the editor
                                        _storeMetaDataInCachedTableFile(ref targetExternalHtmlTable, "sync-structure-update", structureUpdate.ToString().ToLower());

                                        targetExternalHtmlTable.Save(targetExternalHtmlTableFilePathOs);

                                        sbDebug.AppendLine($"- structureUpdate: {structureUpdate.ToString()}");

                                        if (externalTableId == debugTableId)
                                        {
                                            appLogger.LogInformation($"structureUpdate: {structureUpdate}");
                                            appLogger.LogInformation($"overrideCachedHtmlFiles: {overrideCachedHtmlFiles}");
                                        }

                                        // B) Check data in the table
                                        if ((!structureUpdate && !overrideCachedHtmlFiles) || overrideCachedHtmlFiles)
                                        {
                                            var targetCellFound = true;
                                            var dataUpdate = false;
                                            var tableSourceValueText = CreateTableCompareString(sourceExternalHtmlTable);
                                            var tableTargetValueText = CreateTableCompareString(targetExternalHtmlTableCloned);

                                            if (externalTableId == debugTableId)
                                            {
                                                appLogger.LogInformation($"tableSourceValueText: {tableSourceValueText}");
                                                appLogger.LogInformation($"tableTargetValueText: {tableTargetValueText}");
                                            }

                                            if (tableSourceValueText != tableTargetValueText)
                                            {
                                                dataUpdate = true;

                                                // Sync the numbers
                                                if (!overrideCachedHtmlFiles)
                                                {
                                                    var nodeListSourceValues = sourceExternalHtmlTable.SelectNodes("//tbody/tr/td[position() > 1]/value");
                                                    foreach (XmlNode nodeSourceValue in nodeListSourceValues)
                                                    {
                                                        var xpathToValue = GetXPathToNode(nodeSourceValue);
                                                        if (debugRoutine) sbDebug.AppendLine($"- xpathToValue: {xpathToValue}");

                                                        if (targetCellFound)
                                                        {
                                                            var nodeTargetValue = targetExternalHtmlTable.SelectSingleNode(xpathToValue);
                                                            if (nodeTargetValue == null)
                                                            {
                                                                if (dataFileInUse) logError.AppendLine($"Could not sync table cell xpathToValue: {xpathToValue}, tableName: {externalTableName}, tableId: {externalTableId}, workbookId: {currentWorkbookId}, contentLang: {contentLang}");
                                                                targetCellFound = false;
                                                            }
                                                            else
                                                            {
                                                                nodeTargetValue.InnerText = nodeSourceValue.InnerText;
                                                            }
                                                        }
                                                    }

                                                    if (targetCellFound) targetExternalHtmlTable.Save(targetExternalHtmlTableFilePathOs);
                                                }
                                            }

                                            sbDebug.AppendLine($"- dataUpdate: {dataUpdate.ToString()}");

                                            // If they are not the same, then commit the change in the versioning system so that they popup in the auditor view   
                                            if (dataUpdate)
                                            {
                                                tablesSynced++;
                                                // Construct a table name
                                                var nodeTableSheetName = sourceExternalHtmlTable.SelectSingleNode("/html/tableDefinition/metaData/entry[@key='sheet']");
                                                var tableExcelSheetName = (nodeTableSheetName == null) ? "[unknown sheet name]" : nodeTableSheetName.InnerText;
                                                var nodeTableSheetId = sourceExternalHtmlTable.SelectSingleNode("/html/tableDefinition/metaData/entry[@key='id']");
                                                var tableSheetId = (nodeTableSheetId == null) ? "" : $" ({nodeTableSheetId})";
                                                var nodeTableSheetHash = sourceExternalHtmlTable.SelectSingleNode("/html/tableDefinition/metaData/entry[@key='sheetHash']");
                                                var tableSheetHash = (nodeTableSheetHash == null) ? "undefined" : nodeTableSheetHash.InnerText;
                                                var tableName = $"{tableExcelSheetName}{tableSheetId}";

                                                // Construct the commit message
                                                XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                                                SetAttribute(xmlCommitMessage.SelectSingleNode("/root/crud"), "application", "externaltablesync");
                                                xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "s";
                                                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = tableExcelSheetName;
                                                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = tableSheetHash;
                                                var message = xmlCommitMessage.DocumentElement.InnerXml;

                                                // Commit the result in the GIT repository
                                                GitCommit(message, Path.GetDirectoryName(Path.GetDirectoryName(xmlSectionFolderPathOs)), ReturnTypeEnum.Xml, false);
                                                if (dataFileInUse) logSuccess.AppendLine($"Updated table with new data in {sectionDataFileReference} with language {contentLang} (sheetname: {externalTableName}, tableId: {externalTableId})");


                                            }
                                            else
                                            {
                                                tablesWithoutSync++;
                                                if (dataFileInUse) logSuccess.AppendLine($"No need to update table in {sectionDataFileReference} with language {contentLang} (sheetname: {externalTableName}, tableId: {externalTableId})");
                                            }
                                        }
                                        else
                                        {
                                            sectionDataFileReference = Path.GetFileName(xmlContentFilePathOs);
                                            // Log the error when we can not sync
                                            var baseErrorMessage = (overrideCachedHtmlFiles) ?
                                                $"Table with ID {externalTableId} found in {sectionDataFileReference} and language {contentLang} will show an error message, because the table structures (cache vs. original) do not match" :
                                                $"Could not sync table with ID {externalTableId} found in {sectionDataFileReference} and language {contentLang} because the table structures (cache vs. original) do not match";

                                            logError.AppendLine($"{baseErrorMessage} - sourceNumberOfCells: {sourceNumberOfCells.ToString()}, targetNumberOfCells: {targetNumberOfCells.ToString()}, tableName: {externalTableName}, workbookId: {currentWorkbookId}");
                                        }

                                        if (structureUpdate)
                                        {
                                            sectionDataFileReference = Path.GetFileName(xmlContentFilePathOs);
                                            if (dataFileInUse) logWarning.AppendLine($"Table with ID {externalTableId} found in {sectionDataFileReference} and language {contentLang} will probably be marked invalid, because the table structures (cache vs. original) do not match - sourceNumberOfCells: {sourceNumberOfCells.ToString()}, targetNumberOfCells: {targetNumberOfCells.ToString()}, tableName: {externalTableName}, workbookId: {currentWorkbookId}");
                                        }
                                    }
                                    else
                                    {
                                        // TODO: Is additional logging required here
                                    }
                                }
                                else
                                {
                                    if (debugRoutine)
                                    {
                                        appLogger.LogInformation($"Skipping external table (id: {externalTableId}) bacause it's not part of the workbook reference (table-reference: {workbookReference}, mathod-reference: {workbookReferenceToSync})");
                                    }
                                }
                            }
                            else
                            {
                                warningMessage = $"External table sync was not executed because we could not detect a workbook ID to sync against. sectionDataFileReference: {sectionDataFileReference}, contentLang: {contentLang}";
                                appLogger.LogWarning(warningMessage);
                                logWarning.AppendLine(warningMessage);
                            }

                        }

                    }

                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, $"There was an error syncing the external tables for project with ID: {currentProjectId}", $"error: {ex}, {Environment.NewLine + sbDebug.ToString()}, stack-trace: {GetStackTrace()}");
                }

                Console.WriteLine("************************** END EXTERNAL TABLE SYNC ********************************");

            }

            // 5) Update the CMS metadata content
            await UpdateCmsMetadata(projectIdToSync);

            // 6) Return a success message
            var responseMessage = $"External data sync report:\n\n\nWorkbook ID: {workbookIdToSync}\nTotal tables found in document: {tablesFound.ToString()}\nTotal tables investigated: {tablesMatched.ToString()}\nTables updated: {tablesSynced.ToString()}\nTables without need to update: {tablesWithoutSync.ToString()}\n\n";
            responseMessage += $"Sync summary:\n- Success count: {logSuccess.ToString().Count(c => c == '\n')}\n- Warning count: {logWarning.ToString().Count(c => c == '\n')}\n- Error count: {logError.ToString().Count(c => c == '\n')}\n\n";

            // Add the logs
            responseMessage += $"Sync details:";
            if (logSuccess.Length > 0)
            {
                responseMessage += $"\n\n* Success log:\n{logSuccess.ToString()}";
            }
            if (logWarning.Length > 0)
            {
                responseMessage += $"\n\n* Warning log:\n{logWarning.ToString()}";
            }
            if (logError.Length > 0)
            {
                responseMessage += $"\n\n* Error log:\n{logError.ToString()}";
            }

            return new TaxxorReturnMessage(true, responseMessage, $"{Environment.NewLine + sbDebug.ToString()}, stack-trace: {GetStackTrace()}");
        }

        /// <summary>
        /// Generates a string that we can use to check if the data in a table has changed
        /// </summary>
        /// <param name="xmlTable"></param>
        /// <returns></returns>
        public static string CreateTableCompareString(XmlDocument xmlTable)
        {
            var tableValues = new StringBuilder();
            var nodeListSourceValues = xmlTable.SelectNodes("//tbody/tr/td[position() > 1]/value/text()");
            foreach (XmlNode nodeText in nodeListSourceValues)
            {
                tableValues.Append(nodeText.InnerText);
            }
            return tableValues.ToString();
        }

        /// <summary>
        /// Syncs external table data into section XML content
        /// Executed when a section is requested
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="projectId"></param>
        public static XmlDocument SyncExternalCachedTableData(XmlDocument xmlDocument, string projectId, string xmlSectionFolderPathOs = null, string contentLanguage = "all")
        {
            // Global variables
            var debugRoutine = (siteType == "local" || siteType == "dev");
            StringBuilder sbDebug = new StringBuilder();
            var forceFactIdOverride = true;

            var nodeCmsProjectExternalDataSets = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']/repositories/external_data/sets");
            if (nodeCmsProjectExternalDataSets == null) HandleError($"There was an error syncing the external tables for project with ID: {projectId}", $"error: 'Could not locate external data set', stack-trace: {GetStackTrace()}");

            var articleId = xmlDocument.SelectSingleNode("/data/content/article")?.GetAttribute("id") ?? "unknown";

            var containsOldStyleTables = xmlDocument.SelectNodes("//table[@data-syncstatus]").Count > 0;

            // Locate a default workbook ID
            var nodeDefaultExternalDataSet = nodeCmsProjectExternalDataSets.SelectSingleNode("set");
            if (nodeDefaultExternalDataSet == null)
            {
                if (debugRoutine && containsOldStyleTables) appLogger.LogInformation($"No external dataset has been defined for project with ID {projectId} for SyncExternalCachedTableData() in articleId {articleId}");
                return xmlDocument;
            }

            var workbookId = GetAttribute(nodeDefaultExternalDataSet, "id");

            // Sync the numbers from the external tables
            var langSelector = (contentLanguage == "all") ? "" : $"[@lang='{contentLanguage}']";
            var baseXpathToExternalTables = $"/data/content{langSelector}/*//div[contains(@class, 'external-table')]/table";
            var nodeListExternalTables = xmlDocument.SelectNodes(baseXpathToExternalTables);
            if (nodeListExternalTables.Count > 0 && xmlSectionFolderPathOs == null) xmlSectionFolderPathOs = RetrieveContentDataFolderPathOs(projectId);
            foreach (XmlNode nodeExternalTable in nodeListExternalTables)
            {
                var externalTableId = "";
                var contentLang = "";
                var nodeContent = nodeExternalTable.SelectSingleNode("ancestor::content[@lang]");
                if (nodeContent != null) contentLang = nodeContent.GetAttribute("lang");

                try
                {
                    var syncStatus = "ok";
                    var continueSync = true;

                    externalTableId = GetAttribute(nodeExternalTable, "id");
                    if (string.IsNullOrEmpty(externalTableId))
                    {
                        appLogger.LogWarning($"Unable to find the table ID for this external table. table html: {TruncateString(nodeExternalTable.OuterXml, 400)}");
                        appLogger.LogWarning($"Not processing this table any further");
                        continue;
                    }
                    else
                    {
                        externalTableId = CalculateBaseExternalTableId(externalTableId);
                    }

                    var externalTableContentHash = GetAttribute(nodeExternalTable, "data-contenthash");
                    sbDebug.Clear();
                    sbDebug.AppendLine($"- externalTableId: {externalTableId}");

                    var cachedExternalHtmlTable = new XmlDocument();
                    var cachedExternalHtmlTableFileName = CalculateCachedExternalTableFileName(externalTableId);
                    var cachedExternalHtmlTableFilePathOs = $"{xmlSectionFolderPathOs}/{cachedExternalHtmlTableFileName}";
                    sbDebug.AppendLine($"- cachedExternalHtmlTableFileName: {cachedExternalHtmlTableFileName}");

                    if (debugRoutine && cachedExternalHtmlTableFileName == "123__table-uid1440995683.xml")
                    {
                        appLogger.LogDebug("Found the table to debug");
                    }



                    // Check if we have a cached version of the table - if not, then try to retrieve it
                    if (!File.Exists(cachedExternalHtmlTableFilePathOs))
                    {
                        // Retrieve the workbook ID from the workbook reference
                        var currentWorkbookId = workbookId;
                        var workbookReference = GetAttribute(nodeExternalTable, "data-workbookreference");
                        if (string.IsNullOrEmpty(workbookReference))
                        {
                            appLogger.LogWarning($"Could not retrieve workbook reference, so we will be using the default workbook ID. articleId: {articleId}, contentLang: {contentLang}, projectId: {projectId}");
                        }
                        else
                        {
                            currentWorkbookId = MapWorkbookReferenceToWorkbookId(projectId, workbookReference, "");
                        }

                        if (!string.IsNullOrEmpty(currentWorkbookId))
                        {
                            // Retrieve the source content from the server
                            cachedExternalHtmlTable = Taxxor.ConnectedServices.GenericDataConnector.RetrieveExternalTable(projectId, currentWorkbookId, externalTableId, true).GetAwaiter().GetResult();
                            if (XmlContainsError(cachedExternalHtmlTable))
                            {
                                syncStatus = "missing-external-table";
                                appLogger.LogError($"Could not retrieve external html table. response: {cachedExternalHtmlTable.OuterXml}, articleId: {articleId}, contentLang: {contentLang}, projectId: {projectId}");
                                nodeExternalTable.SetAttribute("data-syncstatus", syncStatus);
                                continueSync = false;
                            }
                            else
                            {
                                // Save the cached file on the disk
                                cachedExternalHtmlTable.Save(cachedExternalHtmlTableFilePathOs);
                            }
                        }
                        else
                        {
                            continueSync = false;
                            appLogger.LogWarning($"Could not sync external table with ID {externalTableId} in article with ID {articleId} (language: {contentLang}) bacause the workbook to sync against could not be located");
                        }
                    }

                    if (continueSync)
                    {

                        // Load the cached table
                        if (syncStatus == "ok")
                        {
                            cachedExternalHtmlTable.Load(cachedExternalHtmlTableFilePathOs);
                            // Test if there has been a sync error because the external table structure has changed
                            var nodeMetaStructureStatus = cachedExternalHtmlTable.SelectSingleNode("/html/head/meta[@name='sync-structure-update']");

                            if (nodeMetaStructureStatus != null)
                            {
                                if (nodeMetaStructureStatus.InnerText == "true") syncStatus = "structure-error";
                            }

                            nodeMetaStructureStatus = cachedExternalHtmlTable.SelectSingleNode("/html/head/meta[@name='sync-source-available']");
                            if (nodeMetaStructureStatus != null)
                            {
                                if (nodeMetaStructureStatus.InnerText == "false") syncStatus = "missing-external-table";
                            }

                        }

                        // Check if the number of cells in the body are the same (compare cached table against the table we want to use in the editor)
                        var sourceNumberOfCells = cachedExternalHtmlTable.SelectNodes("/html/tableDefinition/table/tbody/tr/td").Count;
                        var targetNumberOfCells = nodeExternalTable.SelectNodes("tbody/tr/td").Count;
                        if (sourceNumberOfCells != targetNumberOfCells)
                        {
                            sbDebug.AppendLine($"ERROR: Table structures do not match... sourceNumberOfCells: {sourceNumberOfCells}, targetNumberOfCells: {targetNumberOfCells}");
                            if (debugRoutine) cachedExternalHtmlTable.Save($"{logRootPathOs}/-structureerror_{externalTableId}.xml");
                            syncStatus = "structure-error";
                        }
                        else
                        {
                            // Reset the error indicator in the cached version of the table
                            if (syncStatus == "structure-error")
                            {
                                _storeMetaDataInCachedTableFile(ref cachedExternalHtmlTable, "sync-structure-update", "false");

                                cachedExternalHtmlTable.Save(cachedExternalHtmlTableFilePathOs);

                                // Reset the sync status indicator so that we can continue with syncing the numbers
                                syncStatus = "ok";
                            }
                        }



                        // Start the sync
                        if (syncStatus == "ok")
                        {
                            // Determine if we need to sync the table data in the content
                            // TODO: Implememt this by comparing the cell content of the cached table against the cell content of the table used in the data file


                            // => Check hidden rows and columns in the source and hide it in the target as well
                            string[] nodeNames = { "thead", "tbody" };
                            foreach (string nodeName in nodeNames)
                            {
                                var nodeListSourceRows = cachedExternalHtmlTable.SelectNodes($"//table/{nodeName}/tr[th|td]");
                                var rowCounter = 0;
                                var xPathTargetRow = "";
                                var xPathTargetCell = "";
                                foreach (XmlNode nodeSourceRow in nodeListSourceRows)
                                {
                                    rowCounter++;
                                    var classRow = "";
                                    // appLogger.LogInformation(rowCounter.ToString());

                                    // Clear all the hidden cell classes in the target table
                                    xPathTargetRow = $"{nodeName}/tr[{rowCounter}]";
                                    if (debugRoutine && cachedExternalHtmlTableFileName == "123__table-uid1440995683.xml")
                                    {
                                        appLogger.LogDebug("Found the table to debug");
                                        appLogger.LogDebug($"xPathTargetRow: {xPathTargetRow}");
                                    }
                                    var nodeTargetRow = nodeExternalTable.SelectSingleNode(xPathTargetRow);
                                    if (nodeTargetRow == null)
                                    {
                                        appLogger.LogWarning($"Could not locate target row for show/hide rows/cells for cachedExternalHtmlTableFileName: {cachedExternalHtmlTableFileName}. xPathTargetRow: {xPathTargetRow}, articleId: {articleId}, contentLang: {contentLang}, projectId: {projectId}");
                                    }
                                    else
                                    {
                                        // Remove class from row
                                        classRow = GetAttribute(nodeTargetRow, "class") ?? "";
                                        if (classRow.Contains(" hide"))
                                        {
                                            classRow = classRow.Replace(" hide", "");
                                            SetAttribute(nodeTargetRow, "class", classRow);
                                        }

                                        // Remove class from cells
                                        var nodeListTargetCells = nodeTargetRow.SelectNodes("*");
                                        foreach (XmlNode nodeTargetCell in nodeListTargetCells)
                                        {
                                            var classCell = GetAttribute(nodeTargetCell, "class") ?? "";
                                            if (classCell.Contains(" hide"))
                                            {
                                                SetAttribute(nodeTargetCell, "class", classCell.Replace(" hide", ""));
                                            }
                                        }
                                    }

                                    if (nodeTargetRow != null)
                                    {
                                        var countCells = 0;
                                        var countCellsHidden = 0;
                                        var nodeListSourceCells = nodeSourceRow.SelectNodes("*");
                                        foreach (XmlNode nodeSourceCell in nodeListSourceCells)
                                        {
                                            countCells++;
                                            var cellVisibility = GetAttribute(nodeSourceCell, "data-visibility") ?? "";
                                            if (cellVisibility == "hidden")
                                            {
                                                countCellsHidden++;
                                                xPathTargetCell = $"*[{countCells.ToString()}]";
                                                var nodeTargetCell = nodeTargetRow.SelectSingleNode(xPathTargetCell);
                                                if (nodeTargetCell == null)
                                                {
                                                    appLogger.LogWarning($"Could not locate target cell for show/hide rows/cells. xPathTargetCell: {xPathTargetCell}, articleId: {articleId}, contentLang: {contentLang}, projectId: {projectId}");
                                                }
                                                else
                                                {
                                                    var classCell = GetAttribute(nodeTargetCell, "class") ?? "";
                                                    SetAttribute(nodeTargetCell, "class", $"{classCell} hide");
                                                }

                                            }
                                        }

                                        if (countCells == countCellsHidden)
                                        {
                                            // Hide the row
                                            if (nodeTargetRow != null)
                                            {
                                                SetAttribute(nodeTargetRow, "class", $"{classRow} hide");
                                            }
                                            // if (debugRoutine && cachedExternalHtmlTableFileName == "__table-uid1440995683.xml")
                                            // {
                                            //     appLogger.LogDebug("---");
                                            //     appLogger.LogDebug(nodeTargetRow.OuterXml);
                                            //     // appLogger.LogDebug("-");
                                            //     // appLogger.LogDebug(nodeExternalTable.OuterXml);
                                            //     appLogger.LogDebug("---");
                                            // }
                                        }
                                    }
                                }
                            }

                            // if (debugRoutine && cachedExternalHtmlTableFileName == "__table-uid1440995683.xml")
                            // {
                            //     appLogger.LogDebug(nodeExternalTable.OuterXml);
                            // }



                            // => Sync the numbers from the stored table into the XML we have just loaded
                            var nodeListSourceValues = cachedExternalHtmlTable.SelectNodes("//tbody/tr/td[position() > 1]/value");
                            foreach (XmlNode nodeSourceValue in nodeListSourceValues)
                            {
                                var xpathToValue = GetXPathToNode(nodeSourceValue);
                                if (debugRoutine) sbDebug.AppendLine($"- xpathToValue: {xpathToValue}");

                                var xpathToTargetValue = $"{xpathToValue.SubstringAfter("table[1]/").SubstringBefore("/value[1]")}//*[local-name()='p' or local-name()='span' or local-name()='div' or local-name()='b' or local-name()='i']";
                                if (debugRoutine) sbDebug.AppendLine($"- xpathToTargetValue: {xpathToTargetValue}");

                                // Use - nodeExternalTable.SelectNodes()
                                var xmlNodeListTargetValue = nodeExternalTable.SelectNodes(xpathToTargetValue);
                                var nodeListLength = xmlNodeListTargetValue.Count;
                                if (nodeListLength == 0)
                                {
                                    // Create a span element that we can use to sync the numbers in
                                    var nodeSpan = xmlDocument.CreateElement("span");
                                    nodeSpan.SetAttribute("data-fact-id", new Guid().ToString());
                                    var xPathTargetCell = xpathToTargetValue.SubstringBefore("//*[local-name()");
                                    var nodeTargetTableCell = nodeExternalTable.SelectSingleNode(xPathTargetCell);
                                    if (nodeTargetTableCell != null)
                                    {
                                        nodeTargetTableCell.AppendChild(nodeSpan);
                                        nodeListLength = 1;
                                        xmlNodeListTargetValue = nodeExternalTable.SelectNodes(xpathToTargetValue);
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Could not find target cell for inserting new span node. xPathTargetCell: {xPathTargetCell}");
                                    }
                                }


                                if (nodeListLength == 0)
                                {
                                    appLogger.LogError($"Could not locate target node for cachedExternalHtmlTableFileName: {cachedExternalHtmlTableFileName}. xpathToValue: {xpathToValue}, xpathToTargetValue: {xpathToTargetValue}, articleId: {articleId}, contentLang: {contentLang}, projectId: {projectId}, stack-trace: {GetStackTrace()}");
                                    syncStatus = "target-node-notfound";
                                }
                                else
                                {
                                    // Source data to sync
                                    var sourceValue = nodeSourceValue.InnerText.Trim();
                                    var sourceFactId = GetAttribute(nodeSourceValue.ParentNode, "data-id");

                                    // Format the negative numbers for usage in table cells
                                    if ((sourceValue.StartsWith('-') && sourceValue.Length > 1) || (sourceValue.StartsWith("&#x2212;") && sourceValue.Length > 8))
                                    {
                                        sourceValue = sourceValue.SubstringAfter("-");
                                        if (sourceValue.EndsWith('%'))
                                        {
                                            sourceValue = $"({sourceValue.SubstringBefore("%")})%";
                                        }
                                        else
                                        {
                                            sourceValue = $"({sourceValue})";
                                        }

                                    }



                                    // The last element in the list is the target node that we need to inject the value into
                                    var nodeTargetCellElement = xmlNodeListTargetValue.Item(nodeListLength - 1);

                                    // Sync the values in the target
                                    var nodeTargetContent = nodeTargetCellElement.InnerXml;
                                    if (nodeTargetContent.Contains("</"))
                                    {
                                        //if (debugRoutine) Console.WriteLine($"nodeTargetContent: {nodeTargetContent}");

                                        // This node contains XML - now assume that we need to replace the first text() element that we can find
                                        // Insert the value just before the initial tag starts
                                        var encodedValue = new XText(sourceValue).ToString();
                                        nodeTargetCellElement.InnerXml = encodedValue + "<" + nodeTargetContent.SubstringAfter("<");
                                    }
                                    else
                                    {
                                        nodeTargetCellElement.InnerText = sourceValue;
                                    }

                                    // => Attributes on the element containing the number
                                    var parentNode = nodeTargetCellElement.ParentNode;
                                    var grandParentNode = parentNode.ParentNode;
                                    var grandGrandParentNode = grandParentNode.ParentNode;

                                    // Remove steering attributes from parent nodes
                                    string[] steeringAttributesToRemove = { "data-fact-id", "data-value" };
                                    foreach (var steeringAttributeName in steeringAttributesToRemove)
                                    {
                                        RemoveAttribute(parentNode, steeringAttributeName);
                                        RemoveAttribute(grandParentNode, steeringAttributeName);
                                        RemoveAttribute(grandGrandParentNode, steeringAttributeName);
                                    }

                                    // Fact id
                                    if (!string.IsNullOrEmpty(sourceFactId))
                                    {
                                        if (forceFactIdOverride)
                                        {
                                            SetAttribute(nodeTargetCellElement, "data-fact-id", sourceFactId);
                                        }
                                        else
                                        {
                                            if (string.IsNullOrEmpty(GetAttribute(nodeTargetCellElement, "data-fact-id")))
                                            {
                                                SetAttribute(nodeTargetCellElement, "data-fact-id", sourceFactId);
                                            }
                                        }
                                    }

                                    // Retrieve the original value as defined in Excel
                                    var sourceExcelValue = GetAttribute(nodeSourceValue.ParentNode, "data-value");
                                    if (!string.IsNullOrEmpty(sourceExcelValue))
                                    {
                                        SetAttribute(nodeTargetCellElement, "data-value", sourceExcelValue);
                                    }
                                    else
                                    {
                                        RemoveAttribute(nodeTargetCellElement, "data-value");
                                    }


                                }

                            }
                        }

                        // Log debug information when something went wrong during the sync
                        if (syncStatus != "ok")
                        {
                            appLogger.LogWarning($"syncStatus: {syncStatus}, articleId: {articleId}, externalTableId: {externalTableId}, contentLang: {contentLang}, projectId: {projectId}");
                            appLogger.LogDebug(sbDebug.ToString());
                        }

                        // Set the sync status in the data that we will send to the editor
                        SetAttribute(nodeExternalTable, "data-syncstatus", syncStatus);
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"There was an issue syncing data in articleId: {articleId} table with externalTableId: {externalTableId}, contentLang: {contentLang}, projectId: {projectId}");
                }
            }

            return xmlDocument;
        }


        /// <summary>
        /// Attempts to retrieve a workbook ID from the workbook reference
        /// Relation between workbook reference and workbook ID is stored in data_configuration.xml
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="workbookReference"></param>
        /// <param name="defaultWorkbookId"></param>
        /// <returns></returns>
        public static string MapWorkbookReferenceToWorkbookId(string projectId, string workbookReference, string defaultWorkbookId)
        {
            string? workbookIdFound = null;
            var xpath = $"/configuration/cms_projects/cms_project[@id='{projectId}']/repositories/external_data/sets/set/name";
            var nodeListWorkbookReferences = xmlApplicationConfiguration.SelectNodes(xpath);
            foreach (XmlNode nodeWorkbookReference in nodeListWorkbookReferences)
            {
                if (nodeWorkbookReference.InnerText.ToLower() == workbookReference.ToLower())
                {
                    workbookIdFound = GetAttribute(nodeWorkbookReference.ParentNode, "id");
                }
            }

            if (!string.IsNullOrEmpty(workbookIdFound))
            {
                //appLogger.LogInformation($"workbookIdFound: {workbookIdFound}");
                return workbookIdFound;
            }

            appLogger.LogWarning($"Could not locate a workbook set based on the workbook reference: {workbookReference} - using default workbook ID {defaultWorkbookId} now. projectId: {projectId}");
            return defaultWorkbookId;
        }

    }
}