using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Syncs external tables in the content of this project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task SyncExternalTables(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            var testForLocks = false;
            var generateVersions = (siteType != "local");
            generateVersions = true;

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var projectId = context.Request.RetrievePostedValue("pid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var workbookReference = context.Request.RetrievePostedValue("workbookreference", @"^[a-zA-Z_\s\-\d]{1,256}$", true, reqVars.returnType);
            var workbookId = context.Request.RetrievePostedValue("workbookid", @"^[a-zA-Z_\s\-\d]{1,256}$", true, reqVars.returnType);

            // await _syncExternalTablesMessageToClient("Start external table sync");

            var step = 1;
            var stepMax = 2;
            if (generateVersions) stepMax = stepMax + 2;

            // HandleError("Thrown on purpose", $"projectId: {projectId}, workbookReference: {workbookReference}, workbookId: {workbookId}");

            // Check if there are any sections locked - if so, then back out 
            if (testForLocks)
            {
                var numberOfLocks = FilingLockStore.RetrieveNumberOfLocks(projectVars);
                if (numberOfLocks > 0)
                {
                    HandleError($"Could not proceed because there {((numberOfLocks == 1) ? "is 1 section" : "are " + numberOfLocks + " sections")} locked in this project. External Table sync is only available when there are no sections locked in a project.", $"stack-trace: {GetStackTrace()}");
                }
            }

            // A) Create a minor version before we sync
            if (generateVersions)
            {
                step = await _syncExternalTablesMessageToClient("Create the pre-sync version of the content", step, stepMax);
                var preSyncVerionResult = await GenerateVersion(projectId, $"Pre external tables synchronization (workbook reference: {workbookReference}, id: {workbookId}) version", false);
                if (!preSyncVerionResult.Success) HandleError(ReturnTypeEnum.Json, preSyncVerionResult.Message, preSyncVerionResult.DebugInfo);
            }

            //
            // => Stylesheet cache
            //
            SetupPdfStylesheetCache(reqVars);


            // B) Call the service and sync the external tables
            step = await _syncExternalTablesMessageToClient("Sync the external tables with new information from the source", step, stepMax);
            var dataToPost = new Dictionary<string, string>();
            dataToPost.Add("projectid", projectId);
            dataToPost.Add("workbookreference", workbookReference);
            dataToPost.Add("workbookid", workbookId);

            var xmlResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorsyncexternaltables", dataToPost, debugRoutine);
            if (XmlContainsError(xmlResponse)) HandleError(ReturnTypeEnum.Json, xmlResponse);

            // The message field that is returned contains the log of the sync process
            var nodeSyncResultMessage = xmlResponse.SelectSingleNode("/result/message");


            // C) Create another minor version after the sync was completed
            if (generateVersions)
            {
                step = await _syncExternalTablesMessageToClient("Create the post-sync version of the content", step, stepMax);
                // Render an XML formatted message where we can fit in the log of the sync result as well
                var xmlTagMessage = new XmlDocument();
                xmlTagMessage.LoadXml("<data><title/><log/></data>");
                xmlTagMessage.SelectSingleNode("/data/title").InnerText = $"Post external tables synchronization (workbook reference: {workbookReference}, id: {workbookId}) version";
                if (nodeSyncResultMessage != null)
                {
                    // If the sync log is too long, truncate it
                    var syncLog = TruncateString(nodeSyncResultMessage.InnerText, 4500);

                    // Add the log to the XML that we dump as a message in the version
                    xmlTagMessage.SelectSingleNode("/data/log").InnerText = syncLog;
                }

                var postSyncVersionResult = await GenerateVersion(projectId, xmlTagMessage.OuterXml, false);
                if (!postSyncVersionResult.Success) HandleError(ReturnTypeEnum.Json, postSyncVersionResult.Message, postSyncVersionResult.DebugInfo);
            }

            //
            // => Generate graph renditions
            //
            step = await _syncExternalTablesMessageToClient("Generating graph renditions", step, stepMax);
            var graphRenditionsResult = await GenerateGraphRenditions(reqVars, projectVars);
            if (!graphRenditionsResult.Success)
            {
                appLogger.LogError($"Failed rendering graph renditions. project id: {projectVars.projectId}, {graphRenditionsResult.LogToString()}");
                await _syncExternalTablesMessageToClient("ERROR: Failed to render graph renditions");
            }

            //
            // => Clear the paged media cache
            // 
            ContentCache.Clear(projectVars.projectId);

            //
            // => Clear the Document Store cache
            //
            var clearCacheResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.ClearCache(debugRoutine);
            if (!clearCacheResult.Success)
            {
                appLogger.LogWarning($"Failed to clear the document store cache. details: {clearCacheResult.ToString()}");
            }


            // Construct a response message for the client
            var responseMessage = $"Successfully synchronized external tables. Please check the Auditor View for details";

            if (nodeSyncResultMessage != null)
            {
                responseMessage = $"<pre>{nodeSyncResultMessage.InnerText}</pre>";
            }
            dynamic jsonData = new ExpandoObject();
            jsonData.result = new ExpandoObject();
            jsonData.result.message = responseMessage;
            jsonData.result.debuginfo = $"xmlResponse: {xmlResponse.OuterXml}";

            // Convert to JSON and return it to the client
            string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

            await response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
        }


        /// <summary>
        /// Utility routine to send a message to the client over a websocket
        /// </summary>
        /// <param name="message"></param>
        /// <param name="step"></param>
        /// <param name="maxStep"></param>
        /// <returns></returns>
        private static async Task<int> _syncExternalTablesMessageToClient(string message, int step = -1, int maxStep = -1)
        {
            if (step == -1)
            {
                await MessageToCurrentClient("SyncExternalTablesProgress", message);
            }
            else
            {
                await MessageToCurrentClient("SyncExternalTablesProgress", $"Step {step}/{maxStep}: {message}");
                step++;
            }

            return step;
        }

    }

}