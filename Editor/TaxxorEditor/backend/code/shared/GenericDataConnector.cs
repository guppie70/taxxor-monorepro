using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves an external table HTML and attributes from the Taxxor Generic Data Connector
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveExternalTable(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var tableId = context.Request.RetrievePostedValue("tableid", @"^[a-zA-Z_\s\-\d]{1,256}$", true, reqVars.returnType);
            var workbookId = context.Request.RetrievePostedValue("workbookid", @"^[a-zA-Z_\s\-\d]{1,256}$", false, reqVars.returnType, null);
            var workbookReference = context.Request.RetrievePostedValue("workbookreference", @"^[a-zA-Z_\s\-\d]{1,256}$", false, reqVars.returnType, null);

            if (workbookId == null && workbookReference == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Not enough data supplied: workbookid or workbookreference missing. stack-trace: {GetStackTrace()}");

            var nodeCmsProjectExternalDataSets = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/repositories/external_data/sets");
            if (nodeCmsProjectExternalDataSets == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Missing external data definition. workbookId: {workbookId.ToString()}, workbookReference: {workbookReference.ToString()}, stack-trace: {GetStackTrace()}");

            // Resolve table ID
            if (workbookId == null)
            {
                // Find the workbook ID based on the workbook reference that was passed
                var nodeExternalDataSet = nodeCmsProjectExternalDataSets.SelectSingleNode($"set[name/text() = {GenerateEscapedXPathString(workbookReference)}]");
                if (nodeExternalDataSet == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Could not locate external data set from workbook reference. workbookId: {workbookId.ToString()}, workbookReference: {workbookReference.ToString()}, stack-trace: {GetStackTrace()}");
                workbookId = GetAttribute(nodeExternalDataSet, "id");
            }

            if (workbookReference == null)
            {
                // Find the workbook reference based on the ID that was passed
                var nodeExternalDataSet = nodeCmsProjectExternalDataSets.SelectSingleNode($"set[@id={GenerateEscapedXPathString(workbookId)}]");
                if (nodeExternalDataSet == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Could not locate external data set from workbook ID. workbookId: {workbookId.ToString()}, workbookReference: {workbookReference.ToString()}, stack-trace: {GetStackTrace()}");
                workbookReference = RetrieveNodeValueIfExists("name", nodeExternalDataSet);

                // Make it explicit if we could not locate a reference
                if (string.IsNullOrEmpty(workbookReference))
                {
                    appLogger.LogWarning($"Could not locate a workbook reference from workbook ID: {workbookId}");
                    workbookReference = "undefined";
                }
            }

            var xhtmlInlineFilingTable = new XmlDocument();

            // Attempt to retrieve the table from the cache in the Taxxor Document Store
            xhtmlInlineFilingTable = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{CalculateCachedExternalTableFileName(tableId)}", "cmsdataroot", true, false);

            if (XmlContainsError(xhtmlInlineFilingTable))
            {
                if (debugRoutine) appLogger.LogInformation($"Could not find cached table {CalculateCachedExternalTableFileName(tableId)} in the Taxxor Data Store - fetching a fresh one from the Taxxor Generic Document Store");

                xhtmlInlineFilingTable = await Taxxor.ConnectedServices.GenericDataConnector.RetrieveExternalTable(projectVars.projectId, workbookId, tableId, true);
            }
            else
            {
                if (debugRoutine) appLogger.LogInformation($"Using cached table {CalculateCachedExternalTableFileName(tableId)} from the Taxxor Data Store");
            }

            // Transform the table into the required format for the inline editor
            if (debugRoutine) await xhtmlInlineFilingTable.SaveAsync($"{logRootPathOs}/_table-from-service.xml", false);

            if (XmlContainsError(xhtmlInlineFilingTable)) HandleError(ReturnTypeEnum.Json, "Could not retrieve table", $"xhtmlInlineFilingTable: {xhtmlInlineFilingTable.OuterXml}, stack-trace: {GetStackTrace()}");

            var xhtmlTable = _RenderExternalTableHtml(xhtmlInlineFilingTable, projectVars, workbookReference, tableId);

            // Construct a response message for the client
            dynamic jsonData = new ExpandoObject();
            jsonData.table = new ExpandoObject();
            jsonData.table.id = tableId;
            jsonData.table.content = xhtmlTable;

            // Convert to JSON and return it to the client
            string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

            await context.Response.OK(jsonToReturn, reqVars.returnType, true);
        }

        /// <summary>
        /// List all the external tables in a sequential list
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ListExternalTables(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            /*
            Call the Taxxor Document Store to retrieve the data
            */
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("pid", projectVars.projectId);

            // Call the service and retrieve the footnotes as XML
            var xmlSemiStructuredTables = await Taxxor.ConnectedServices.GenericDataConnector.ListExternalTables(projectVars.projectId);
            if (debugRoutine) await xmlSemiStructuredTables.SaveAsync(logRootPathOs + "/semi-structured-table-list.xml", false);

            if (!XmlContainsError(xmlSemiStructuredTables))
            {
                // HandleError(reqVars.returnType, "Thrown on purpose - ListSemiStructuredTables() - Taxxor Editor", $"xmlSemiStructuredTables: {xmlSemiStructuredTables.OuterXml}, stack-trace: {GetStackTrace()}");

                // Grab a list of workbook ID's that have been defined for this project so that we only show the sheets from relevant workbooks
                var listWorkbookIds = new List<string>();
                var nodeListExternalDataSets = xmlApplicationConfiguration.SelectNodes($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/repositories/external_data/sets/set");
                foreach (XmlNode nodeExternalDataSet in nodeListExternalDataSets)
                {
                    listWorkbookIds.Add(GetAttribute(nodeExternalDataSet, "id"));
                }


                // Create a list containing a key value pair, using the workbook ID as the key so that we can sort
                var sortListSemiStructuredTables = new List<KeyValuePair<string, SemiStructuredTable>>();
                foreach (XmlNode nodeEntry in xmlSemiStructuredTables.SelectNodes("/fileStorage/fileEntry"))
                {
                    var workbookId = GetAttribute(nodeEntry, "workbookid");
                    if (listWorkbookIds.Contains(workbookId))
                    {
                        var sheetId = GetAttribute(nodeEntry, "sheetHashNew");
                        var sheetName = GetAttribute(nodeEntry, "sheetName");
                        var timeStamp = GetAttribute(nodeEntry, "timestamp");

                        sortListSemiStructuredTables.Add(new KeyValuePair<string, SemiStructuredTable>(sheetName, new SemiStructuredTable(workbookId, sheetId, sheetName, timeStamp)));
                    }
                }

                // Sort the list by workbook id
                sortListSemiStructuredTables = sortListSemiStructuredTables.OrderBy(x => x.Key).ToList();

                // Build a new list that we can return to the output sorted by workbook id
                List<SemiStructuredTable> semiStructuredTables = new List<SemiStructuredTable>();
                foreach (var pair in sortListSemiStructuredTables)
                {
                    semiStructuredTables.Add(pair.Value);
                }

                // Render response data
                dynamic jsonData = new ExpandoObject();
                jsonData.tables = semiStructuredTables;

                // Convert to JSON and return it to the client
                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                await context.Response.OK(jsonToReturn, reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars.returnType, xmlSemiStructuredTables);
            }
        }

        /// <summary>
        /// Retrieves a preview of the SDE table which is shown in the table picker
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveSdeTablePreview(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var tableId = context.Request.RetrievePostedValue("tableid", @"^[a-zA-Z_\s\-\d]{1,256}$", true, reqVars.returnType);
            var workbookId = context.Request.RetrievePostedValue("workbookid", @"^[a-zA-Z_\s\-\d]{1,256}$", false, reqVars.returnType, null);
            var workbookReference = context.Request.RetrievePostedValue("workbookreference", @"^[a-zA-Z_\s\-\d]{1,256}$", false, reqVars.returnType, null);

            if (workbookId == null && workbookReference == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Not enough data supplied: workbookid or workbookreference missing. stack-trace: {GetStackTrace()}");

            var nodeCmsProjectExternalDataSets = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/repositories/external_data/sets");
            if (nodeCmsProjectExternalDataSets == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Missing external data definition. workbookId: {workbookId.ToString()}, workbookReference: {workbookReference.ToString()}, stack-trace: {GetStackTrace()}");

            // Resolve table ID
            if (workbookId == null)
            {
                // Find the workbook ID based on the workbook reference that was passed
                var nodeExternalDataSet = nodeCmsProjectExternalDataSets.SelectSingleNode($"set[name/text() = {GenerateEscapedXPathString(workbookReference)}]");
                if (nodeExternalDataSet == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Could not locate external data set from workbook reference. workbookId: {workbookId.ToString()}, workbookReference: {workbookReference.ToString()}, stack-trace: {GetStackTrace()}");
                workbookId = GetAttribute(nodeExternalDataSet, "id");
            }

            if (workbookReference == null)
            {
                // Find the workbook reference based on the ID that was passed
                var nodeExternalDataSet = nodeCmsProjectExternalDataSets.SelectSingleNode($"set[@id={GenerateEscapedXPathString(workbookId)}]");
                if (nodeExternalDataSet == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Could not locate external data set from workbook ID. workbookId: {workbookId.ToString()}, workbookReference: {workbookReference.ToString()}, stack-trace: {GetStackTrace()}");
                workbookReference = RetrieveNodeValueIfExists("name", nodeExternalDataSet);

                // Make it explicit if we could not locate a reference
                if (string.IsNullOrEmpty(workbookReference))
                {
                    appLogger.LogWarning($"Could not locate a workbook reference from workbook ID: {workbookId}");
                    workbookReference = "undefined";
                }
            }

            var xhtmlInlineFilingTable = new XmlDocument();

            // Attempt to retrieve the table from the cache in the Taxxor Document Store
            xhtmlInlineFilingTable = await Taxxor.ConnectedServices.GenericDataConnector.RetrieveSdeTablePreview(projectVars.projectId, workbookId, tableId);

            // Transform the table into the required format for the inline editor
            if (debugRoutine) await xhtmlInlineFilingTable.SaveAsync($"{logRootPathOs}/_sdepreviewtable-from-service.xml", false);

            if (XmlContainsError(xhtmlInlineFilingTable)) HandleError(ReturnTypeEnum.Json, "Could not retrieve table", $"xhtmlInlineFilingTable: {xhtmlInlineFilingTable.OuterXml}, stack-trace: {GetStackTrace()}");

            var xhtmlTable = _RenderExternalTableHtml(xhtmlInlineFilingTable, projectVars, workbookReference, tableId);

            // Construct a response message for the client
            dynamic jsonData = new ExpandoObject();
            jsonData.table = new ExpandoObject();
            jsonData.table.id = tableId;
            jsonData.table.content = xhtmlTable;

            // Convert to JSON and return it to the client
            string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

            await context.Response.OK(jsonToReturn, reqVars.returnType, true);
        }

        /// <summary>
        /// Retrieves an HTML table with SDE's which are created in the Structured Data Store when this route is requested
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveSdeTable(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var tableId = context.Request.RetrievePostedValue("tableid", @"^[a-zA-Z_\s\-\d]{1,256}$", true, reqVars.returnType);
            var workbookId = context.Request.RetrievePostedValue("workbookid", @"^[a-zA-Z_\s\-\d]{1,256}$", false, reqVars.returnType, null);
            var workbookReference = context.Request.RetrievePostedValue("workbookreference", @"^[a-zA-Z_\s\-\d]{1,256}$", false, reqVars.returnType, null);

            if (workbookId == null && workbookReference == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Not enough data supplied: workbookid or workbookreference missing. stack-trace: {GetStackTrace()}");

            var nodeCmsProjectExternalDataSets = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/repositories/external_data/sets");
            if (nodeCmsProjectExternalDataSets == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Missing external data definition. workbookId: {workbookId.ToString()}, workbookReference: {workbookReference.ToString()}, stack-trace: {GetStackTrace()}");

            // Resolve table ID
            if (workbookId == null)
            {
                // Find the workbook ID based on the workbook reference that was passed
                var nodeExternalDataSet = nodeCmsProjectExternalDataSets.SelectSingleNode($"set[name/text() = {GenerateEscapedXPathString(workbookReference)}]");
                if (nodeExternalDataSet == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Could not locate external data set from workbook reference. workbookId: {workbookId.ToString()}, workbookReference: {workbookReference.ToString()}, stack-trace: {GetStackTrace()}");
                workbookId = GetAttribute(nodeExternalDataSet, "id");
            }

            if (workbookReference == null)
            {
                // Find the workbook reference based on the ID that was passed
                var nodeExternalDataSet = nodeCmsProjectExternalDataSets.SelectSingleNode($"set[@id={GenerateEscapedXPathString(workbookId)}]");
                if (nodeExternalDataSet == null) HandleError(ReturnTypeEnum.Json, "Could not retrieve table information", $"Could not locate external data set from workbook ID. workbookId: {workbookId.ToString()}, workbookReference: {workbookReference.ToString()}, stack-trace: {GetStackTrace()}");
                workbookReference = RetrieveNodeValueIfExists("name", nodeExternalDataSet);

                // Make it explicit if we could not locate a reference
                if (string.IsNullOrEmpty(workbookReference))
                {
                    appLogger.LogWarning($"Could not locate a workbook reference from workbook ID: {workbookId}");
                    workbookReference = "undefined";
                }
            }

            var xhtmlInlineFilingTable = new XmlDocument();
            xhtmlInlineFilingTable = await Taxxor.ConnectedServices.GenericDataConnector.RetrieveSdeTable(projectVars.projectId, workbookId, tableId);

            // Transform the table into the required format for the inline editor
            if (debugRoutine) await xhtmlInlineFilingTable.SaveAsync($"{logRootPathOs}/_sdetable-from-service.xml", false);

            if (XmlContainsError(xhtmlInlineFilingTable)) HandleError(ReturnTypeEnum.Json, "Could not retrieve table", $"xhtmlInlineFilingTable: {xhtmlInlineFilingTable.OuterXml}, stack-trace: {GetStackTrace()}");

            var xhtmlTable = _RenderExternalTableHtml(xhtmlInlineFilingTable, projectVars, workbookReference, tableId);

            // Construct a response message for the client
            dynamic jsonData = new ExpandoObject();
            jsonData.table = new ExpandoObject();
            jsonData.table.id = tableId;
            jsonData.table.content = xhtmlTable;

            // Convert to JSON and return it to the client
            string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

            await context.Response.OK(jsonToReturn, reqVars.returnType, true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveSdeTableValues(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var tableId = context.Request.RetrievePostedValue("tableid", @"^[a-zA-Z_\s\-\d]{1,256}$", true, reqVars.returnType);

            var xmlSdeTableValues = new XmlDocument();
            xmlSdeTableValues = await Taxxor.ConnectedServices.GenericDataConnector.RetrieveSdeTableValues(projectVars.projectId, tableId);

            // Transform the table into the required format for the inline editor
            if (debugRoutine) await xmlSdeTableValues.SaveAsync($"{logRootPathOs}/_sdetablevalues-from-service.xml", false);

            if (XmlContainsError(xmlSdeTableValues)) HandleError(ReturnTypeEnum.Json, "Could not retrieve sde table values", $"xhtmlInlineFilingTable: {xmlSdeTableValues.OuterXml}, stack-trace: {GetStackTrace()}");


            // Convert to JSON and return it to the client
            string jsonToReturn = ConvertToJson(xmlSdeTableValues);

            await context.Response.OK(jsonToReturn, reqVars.returnType, true);
        }


        /// <summary>
        /// Calculates the cached external HTML table filename based on the wrapper or table element id that was passed
        /// </summary>
        /// <param name="externalTableId"></param>
        /// <param name="rawVersion"></param>
        /// <returns></returns>
        public static string CalculateCachedExternalTableFileName(string externalTableId, bool rawVersion = false)
        {
            var postfix = (rawVersion) ? "-raw" : "";
            return $"__table-{CalculateBaseExternalTableId(externalTableId)}{postfix}.xml";
        }

        /// <summary>
        /// Calculates the base external table id based on the wrapper or table element id that was passed
        /// </summary>
        /// <param name="externalTableId"></param>
        /// <returns></returns>
        public static string CalculateBaseExternalTableId(string externalTableId)
        {
            var tableId = externalTableId;
            if (externalTableId.StartsWith("table_"))
            {
                tableId = externalTableId.SubstringAfter("table_");
            }
            else if (externalTableId.StartsWith("tablewrapper_"))
            {
                tableId = externalTableId.SubstringAfter("tablewrapper_");
            }
            return tableId;
        }

        /// <summary>
        /// Renders a standard table structure for the worksheets that are exported from the Taxxor Excel Plugin
        /// </summary>
        /// <param name="xhtmlInlineFilingTable"></param>
        /// <param name="workbookReference"></param>
        /// <param name="tableId"></param>
        /// <returns></returns>
        private static string _RenderExternalTableHtml(XmlDocument xhtmlInlineFilingTable, ProjectVariables projectVars, string workbookReference, string tableId)
        {
            //
            // Load the standard table header definition from the editor config
            //
            XmlNode? nodeTableHeaderWrapper = xmlApplicationConfiguration.SelectSingleNode("/configuration/page_elements/element[@id='default-tablegraph-header']/*");

            //
            // Attempt to load the table header definition from the editor config, if available
            //
            var xmlEditorConfigTableHeader = LoadTableWrapperStructureFromEditorConfiguration(projectVars);
            if (xmlEditorConfigTableHeader != null)
            {
                // Console.WriteLine(PrettyPrintXml(xmlEditorConfigTableHeader));
                var nodeEditorConfigTableHeader = xmlEditorConfigTableHeader.SelectSingleNode("//div[@class='tablegraph-header-wrapper']");
                if (nodeEditorConfigTableHeader != null)
                {
                    XmlNode importedNode = nodeTableHeaderWrapper.OwnerDocument.ImportNode(nodeEditorConfigTableHeader, true);
                    nodeTableHeaderWrapper.ParentNode.ReplaceChild(importedNode, nodeTableHeaderWrapper);
                }
            }

            //
            // Replace content in the table header in case the table export contains information as metadata (tagged in the Excel add-in)
            //
            var tableCaption = xhtmlInlineFilingTable.SelectSingleNode("/html/tableDefinition/metaData/entry[@key = 'txxCaption']")?.InnerText ?? "";
            var tableScale = xhtmlInlineFilingTable.SelectSingleNode("/html/tableDefinition/metaData/entry[@key = 'txxScale']")?.InnerText ?? "";
            var tableCurrency = xhtmlInlineFilingTable.SelectSingleNode("/html/tableDefinition/metaData/entry[@key = 'txxCurrency']")?.InnerText ?? "";
            if (!string.IsNullOrEmpty(tableCaption))
            {
                var nodeWrapperCaption = nodeTableHeaderWrapper.SelectSingleNode("//div[@class='table-title']");
                if (nodeWrapperCaption != null) nodeWrapperCaption.InnerText = tableCaption;
            }
            if (!string.IsNullOrEmpty(tableScale))
            {
                var nodeWrapperScale = nodeTableHeaderWrapper.SelectSingleNode("//div[@class='table-scale']");
                if (nodeWrapperScale != null) nodeWrapperScale.InnerText = tableCaption;
            }
            if (!string.IsNullOrEmpty(tableCurrency))
            {
                var nodeWrapperCurrency = nodeTableHeaderWrapper.SelectSingleNode("//div[@class='table-currency']");
                if (nodeWrapperCurrency != null) nodeWrapperCurrency.InnerText = tableCaption;
            }

            //
            // => Replace placeholders
            //
            if (nodeTableHeaderWrapper.OuterXml.Contains("[currentyear"))
            {
                var currentProjectPeriodMetadata = new ProjectPeriodProperties(projectVars.projectId);
                var yearEnd = currentProjectPeriodMetadata.PeriodEnd.Year;
                var yearStart = currentProjectPeriodMetadata.PeriodStart.Year;
                if (yearEnd == yearStart)
                {
                    var xmlString = nodeTableHeaderWrapper.OuterXml
                        .Replace("[currentyear]", yearEnd.ToString())
                        .Replace("[currentyear-1]", (yearEnd - 1).ToString())
                        .Replace("[currentyear-2]", (yearEnd - 2).ToString())
                        .Replace("[currentyear-3]", (yearEnd - 3).ToString());
                    // Overwrite the contents of nodeTableHeaderWrapper with xmlString
                    nodeTableHeaderWrapper.InnerXml = xmlString;
                }
                else
                {
                    appLogger.LogWarning($"No support yet for a broken book year (yearStart: {yearStart}, yearEnd {yearEnd})");
                }
            }


            //
            // => Create the table result by transforming the retrieved table structure from the Generic Data Connector with the XSLT stylesheet
            //
            var xsltArgumentList = new XsltArgumentList();
            xsltArgumentList.AddParam("workbook-reference", "", workbookReference);
            xsltArgumentList.AddParam("table-id", "", CalculateBaseExternalTableId(tableId));
            xsltArgumentList.AddParam("taxxor-client-id", "", TaxxorClientId);

            // Add the nodeTableHeaderWrapper as a parameter
            if (nodeTableHeaderWrapper != null)
            {
                xsltArgumentList.AddParam("table-header-wrapper", "", nodeTableHeaderWrapper.CreateNavigator());
            }

            var xmlTableResult = TransformXmlToDocument(xhtmlInlineFilingTable, $"{applicationRootPathOs}/backend/code/shared/xslt/external-table.xsl", xsltArgumentList);

            // Generate hash values for the unique identifiers we will be using to detect table design changes
            var nodeListHeaderCells = xmlTableResult.SelectNodes("/div/table/*/tr/*[@data-cellidentifier]");
            foreach (XmlNode nodeHeaderCell in nodeListHeaderCells)
            {
                nodeHeaderCell.SetAttribute("data-cellidentifier", md5(nodeHeaderCell.GetAttribute("data-cellidentifier")));
            }

            return xmlTableResult.OuterXml;

            /// <summary>
            /// Attempts to retrieve the table wrapper structure from the editor configuration file
            /// </summary>
            /// <param name="projectVars"></param>
            /// <returns></returns>
            static XmlDocument? LoadTableWrapperStructureFromEditorConfiguration(ProjectVariables projectVars)
            {

                string filePath = $"{applicationRootPathOs}/frontend/public/outputchannels/{TaxxorClientId}/{projectVars.outputChannelType}/editor_settings/{projectVars.editorId}_editor_settings.json";

                if (!File.Exists(filePath))
                {
                    filePath = $"{applicationRootPathOs}/frontend/public/outputchannels/{TaxxorClientId}/{projectVars.outputChannelType}/{projectVars.guidLegalEntity}/editor_settings/{projectVars.editorId}_editor_settings.json";

                    if (!File.Exists(filePath))
                    {
                        appLogger.LogWarning($"The editor configuration file at {filePath} does not exist.");
                        return null;
                    }
                }

                string jsonContent = File.ReadAllText(filePath);
                JObject jsonObject = JObject.Parse(jsonContent);

                JArray tableWrapperStructureArray = (JArray)jsonObject["tablewrapperstructure"];
                string tableWrapperStructure = string.Join("", tableWrapperStructureArray);

                XmlDocument xmlDocument = new();
                xmlDocument.LoadHtml($"<root>{tableWrapperStructure}</root>");

                return xmlDocument;
            }
        }


        /// <summary>
        /// Represents a footnote
        /// </summary>
        public class SemiStructuredTable
        {
            // Generic properties for the user class
            public string? id;
            public string? name;
            public string? date;
            public string? workbookid;

            public SemiStructuredTable(string workbookId, string tableId, string tableName, string modifiedDate)
            {
                this.workbookid = workbookId;
                this.id = tableId;
                this.name = tableName;
                this.date = modifiedDate;
            }
        }

    }

}