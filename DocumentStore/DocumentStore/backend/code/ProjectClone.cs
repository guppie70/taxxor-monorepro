using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{

    /// <summary>
    /// Logic for creating new projects/filings/documents
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        public static async Task CloneExistingProject(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev" || siteType == "prev");
            var step = 0;

            // Retrieve posted values
            var sourceProjectId = context.Request.RetrievePostedValue("sourceid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var targetProjectId = context.Request.RetrievePostedValue("targetid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var targetProjectName = context.Request.RetrievePostedValue("targetprojectname", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var cloneAclRetrieved = context.Request.RetrievePostedValue("cloneacl", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var cloneAcl = (cloneAclRetrieved == "yes") ? true : false;
            var reportingPeriod = context.Request.RetrievePostedValue("reportingperiod", RegexEnum.None, true, ReturnTypeEnum.Json);
            var publicationDate = request.RetrievePostedValue("publicationdate", RegexEnum.isodate, false, ReturnTypeEnum.Xml, null);


            // => Initiate a project properties object
            var targetProjectProperties = new CmsProjectProperties(targetProjectId);
            targetProjectProperties.name = targetProjectName;
            targetProjectProperties.reportingPeriod = reportingPeriod;
            targetProjectProperties.SetPublicationDate(publicationDate);


            if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Starting clone. sourceProjectId: {sourceProjectId}, cloneAcl: {cloneAcl.ToString()}, {targetProjectProperties.ToString()}");

            // To please the compiler...
            await DummyAwaiter();

            try
            {
                // => Find and calculate base information about the source and target projects
                var dataConfigurationPathOs = CalculateFullPathOs("/configuration/configuration_system//config/location[@id='data_configuration']");
                var xmlDataConfiguration = new XmlDocument();
                xmlDataConfiguration.Load(dataConfigurationPathOs);

                var nodeProjectRoot = xmlDataConfiguration.SelectSingleNode($"//cms_projects");
                var nodeSourceProject = nodeProjectRoot.SelectSingleNode($"cms_project[@id='{sourceProjectId}']");
                var sourceDataFolderPathOs = CalculateFullPathOs(nodeSourceProject.SelectSingleNode("location[@id='reportdataroot']"), reqVars);
                var targetDataFolderPathOs = sourceDataFolderPathOs.Replace(sourceProjectId, targetProjectId);
                var currentReportType = GetAttribute(nodeSourceProject, "report-type");
                var currentEditorId = RetrieveEditorIdFromReportId(currentReportType);

                // => Clone the filesystem

                // - Setup the filing structure and GIT repositories
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, "Setup basic filing structure");
                if (!SetupFilingDataStructure(targetDataFolderPathOs, reqVars.returnType, true, debugRoutine))
                {
                    HandleError("Could not create the default filing data structure", $"sourceProjectId: {sourceProjectId}, targetProjectId: {targetProjectId}, targetProjectName: {targetProjectName}, cloneAcl: {cloneAcl.ToString()}, stack-trace: {GetStackTrace()}");
                }

                // - Copy the default structure
                var templateDataFolderPathOs = CalculateFullPathOs("cms_data_project-templates").Replace("[editor_id]", currentEditorId).Replace("[taxxor-client-id]", TaxxorClientId);

                // Fallback to a generic type for AR / QR project types
                if (!Directory.Exists(templateDataFolderPathOs))
                {
                    templateDataFolderPathOs = CalculateFullPathOs("cms_data_project-templates").Replace("[editor_id]", currentEditorId).Replace("[taxxor-client-id]", "generic");
                }


                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Copy base filesystem structure from {templateDataFolderPathOs} to {targetDataFolderPathOs}");
                CopyDirectoryRecursive(templateDataFolderPathOs, targetDataFolderPathOs, true);

                // - Copy files and folders from source to target
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Copy directory {sourceDataFolderPathOs} to {targetDataFolderPathOs}");
                CopyDirectoryAdvanced(sourceDataFolderPathOs, targetDataFolderPathOs, true, true, ".git");

                // - Remove the cache folder
                var cacheFolderPathOs = $"{targetDataFolderPathOs}/content/system/cache";
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Remove the cache folder {cacheFolderPathOs}");
                if (Directory.Exists(cacheFolderPathOs)) Directory.Delete(cacheFolderPathOs, true);

                // - Remove the generated reports folder content
                var generatedReportsFolderPathOs = $"{targetDataFolderPathOs}/content/reports";
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Remove the generated reports folder {generatedReportsFolderPathOs}");
                if (Directory.Exists(generatedReportsFolderPathOs))
                {
                    // Remove all files and folders
                    EmptyDirectory(generatedReportsFolderPathOs);
                }

                // - Do the initial commit
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Commit {targetDataFolderPathOs}");
                GitCommit("Filing start data", targetDataFolderPathOs, reqVars.returnType, true);

                // => Clone the node in data configuration, adjust the values and inject
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Update the project references in the data configuration {dataConfigurationPathOs}");
                var nodeProjectCloned = nodeSourceProject.CloneNode(true);
                nodeProjectCloned.SetAttribute("id", targetProjectProperties.projectId);
                nodeProjectCloned.SelectSingleNode("name").InnerText = targetProjectProperties.name;
                nodeProjectCloned.SelectSingleNode("location[@id='reportdataroot']").InnerText = nodeProjectCloned.SelectSingleNode("location[@id='reportdataroot']").InnerText.Replace(sourceProjectId, targetProjectId);

                string pubDate = targetProjectProperties.GetPublicationDate();
                if (!string.IsNullOrEmpty(publicationDate)) nodeProjectCloned.SetAttribute("date-publication", pubDate);

                var datePattern = @"yyyy-MM-dd HH:mm:ss";
                DateTime dateNow = DateTime.Now;
                var currentDate = dateNow.ToString(datePattern);
                nodeProjectCloned.SelectSingleNode("versions/version/date_created").InnerText = currentDate;

                // Reporting period
                nodeProjectCloned.SelectSingleNode("reporting_period").InnerText = reportingPeriod;

                // Project status
                nodeProjectCloned.SelectSingleNode("versions/version/status").InnerText = "open";

                nodeProjectRoot.AppendChild(nodeProjectCloned);

                // D) Save the data configuration file
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Save the updated data configuration file");
                xmlDataConfiguration.Save(dataConfigurationPathOs);

                // E) Update the internal configuration system
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Update the internal configuration objects");
                TaxxorReturnMessage updateApplicationConfigResult = await UpdateDataConfigurationInApplicationConfiguration();
                if (!updateApplicationConfigResult.Success) HandleError(reqVars.returnType, "Update application configuration failed", $"updateApplicationConfigResult: {GenerateDebugObjectString(updateApplicationConfigResult)}, stack-trace: {GetStackTrace()}");

                // F) Update the CMS metadata content
                await UpdateCmsMetadata();

                // G) Return the result
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"Write the response to the output stream");
                await context.Response.OK(GenerateSuccessXml("Successfully cloned the project in the Taxxor Document Store", $"sourceProjectId: {sourceProjectId}, targetProjectId: {targetProjectId}, targetProjectName: {targetProjectName}, cloneAcl: {cloneAcl.ToString()}, stack-trace: {GetStackTrace()}"), reqVars.returnType, true);
            }
            // To make sure HandleError is correctly executed
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (debugRoutine) _dumpProjectCloneDebugStatement(ref step, $"ERROR: {ex}");
                HandleError("Something went wrong while trying to clone your project", $"error: {ex}, sourceProjectId: {sourceProjectId}, targetProjectId: {targetProjectId}, targetProjectName: {targetProjectName}, cloneAcl: {cloneAcl.ToString()}, stack-trace: {GetStackTrace()}");
            }

        }

        /// <summary>
        /// Dumps a debugging statement to the console including a step indicator
        /// </summary>
        /// <param name="step"></param>
        /// <param name="message"></param>
        private static void _dumpProjectCloneDebugStatement(ref int step, string message)
        {
            step++;
            Console.WriteLine($"{step} - {message}");
        }

    }
}