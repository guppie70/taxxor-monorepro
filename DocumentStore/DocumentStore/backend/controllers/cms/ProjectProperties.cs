using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
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
        /// Saves the project properties in the data configuration file
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

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted data
            var projectId = request.RetrievePostedValue("projectid", RegexEnum.None, true, ReturnTypeEnum.Json);
            var projectName = request.RetrievePostedValue("projectname", RegexEnum.None, true, ReturnTypeEnum.Json);
            var projectStatus = request.RetrievePostedValue("projectstatus", RegexEnum.None, true, ReturnTypeEnum.Json);
            var projectReportingPeriod = request.RetrievePostedValue("reportingperiod", RegexEnum.Default, true, ReturnTypeEnum.Json);
            var projectPublicationDate = request.RetrievePostedValue("publicationdate", RegexEnum.isodate, false, ReturnTypeEnum.Json, null);
            var projectStructuredDataSnapshotId = request.RetrievePostedValue("structureddatasnapshotid", RegexEnum.None, true, ReturnTypeEnum.Json);
            var projectExternalDataInfo = request.RetrievePostedValue("externaldatainfo", RegexEnum.None, false, ReturnTypeEnum.Json) ?? "";

            // TODO: Make use of the project properties object throughout this whole method
            var projectProperties = new CmsProjectProperties(projectId);
            projectProperties.name = projectName;
            projectProperties.projectStatus = GetProjectStatusEnum(projectStatus);
            projectProperties.reportingPeriod = projectReportingPeriod;
            projectProperties.SetPublicationDate(projectPublicationDate);


            var basicDebugString = $"{projectProperties.ToString()}, projectStructuredDataSnapshotId: {projectStructuredDataSnapshotId}, projectExternalDataInfo: {projectExternalDataInfo}";

            // HandleError(ReturnTypeEnum.Json, "Thrown on purpose (Taxxor Data Store)", $"{basicDebugString}, stack-trace: {GetStackTrace()}");

            try
            {

                var dataConfigurationPathOs = CalculateFullPathOs("/configuration/configuration_system//config/location[@id='data_configuration']");
                var xmlDataConfiguration = new XmlDocument();
                xmlDataConfiguration.Load(dataConfigurationPathOs);

                // Find the project that we need to adjust
                var nodeProject = xmlDataConfiguration.SelectSingleNode($"//cms_project[@id='{projectId}']");
                if (nodeProject == null) HandleError("Could not locate project", $"{basicDebugString}, stack-trace: {GetStackTrace()}");

                // Update the project name
                nodeProject.SelectSingleNode("name").InnerText = projectName;

                // Update the project status
                nodeProject.SelectSingleNode("versions/version/status").InnerText = projectStatus;

                // Update the reporting period
                nodeProject.SelectSingleNode("reporting_period").InnerText = (projectReportingPeriod == "none" || projectReportingPeriod == "not-applicable") ? "" : projectReportingPeriod;

                // Update the publication date, if it was passed
                if (!string.IsNullOrEmpty(projectPublicationDate))
                {
                    nodeProject.SetAttribute("date-publication", projectProperties.GetPublicationDate());
                }

                // Update the structured data snapshot id
                var nodeStructuredDataRepository = nodeProject.SelectSingleNode($"repositories/structured_data");

                if (nodeStructuredDataRepository == null)
                {
                    // Create the structure
                    var nodeRepositories = nodeProject.SelectSingleNode("repositories");
                    var nodeStructuredData = xmlDataConfiguration.CreateElement("structured_data");
                    nodeRepositories.AppendChild(nodeStructuredData);

                    var nodeSnapshots = xmlDataConfiguration.CreateElement("snapshots");
                    nodeStructuredData.AppendChild(nodeSnapshots);

                    var nodeSnapshot = xmlDataConfiguration.CreateElement("snapshot");
                    SetAttribute(nodeSnapshot, "id", projectStructuredDataSnapshotId);
                    nodeSnapshots.AppendChild(nodeSnapshot);
                }
                else
                {
                    var nodeStructuredDataSnapshot = nodeProject.SelectSingleNode($"repositories/structured_data/snapshots/snapshot[@id='{projectStructuredDataSnapshotId}']");
                    if (nodeStructuredDataSnapshot == null)
                    {
                        var nodeSnapshot = xmlDataConfiguration.CreateElement("snapshot");
                        SetAttribute(nodeSnapshot, "id", projectStructuredDataSnapshotId);
                        nodeProject.SelectSingleNode($"repositories/snapshots/structured_data").AppendChild(nodeSnapshot);
                    }
                }

                // Update the external dataset id
                var nodeExternalDataRepository = nodeProject.SelectSingleNode($"repositories/external_data");

                // Parse the external dataset data that we have received
                Dictionary<string, string> externalDataInformation = new Dictionary<string, string>();
                string[] externalDataInfoSet = projectExternalDataInfo.Split(',');

                foreach (var externalDataInfo in externalDataInfoSet)
                {
                    // Each set consist of a name and ID, separated by a colon
                    string[] externalDataInfoParts = externalDataInfo.Split(':');
                    if (externalDataInfoParts.Length == 2)
                    {
                        externalDataInformation.Add(externalDataInfoParts[0], externalDataInfoParts[1]);
                    }
                    else
                    {
                        appLogger.LogWarning($"Could not parse the external data information. externalDataInfo: {externalDataInfo}, externalDataInfoParts.Length: {externalDataInfoParts.Length.ToString()}, {basicDebugString}, stack-trace: {GetStackTrace()}");
                    }
                }

                XmlNode? nodeSets = null;
                if (nodeExternalDataRepository == null)
                {
                    // Create the structure
                    var nodeRepositories = nodeProject.SelectSingleNode("repositories");
                    var nodeExternalData = xmlDataConfiguration.CreateElement("external_data");
                    nodeRepositories.AppendChild(nodeExternalData);

                    nodeSets = xmlDataConfiguration.CreateElement("sets");
                    nodeExternalData.AppendChild(nodeSets);
                }
                else
                {
                    nodeSets = nodeProject.SelectSingleNode($"repositories/external_data/sets");
                }

                // Clear the data sets which are currently defined
                foreach (XmlNode nodeSet in nodeSets.SelectNodes("set"))
                {
                    RemoveXmlNode(nodeSet);
                }

                // Console.WriteLine("#########################################");
                // Console.WriteLine(PrettyPrintXml(nodeProject));
                // Console.WriteLine("#########################################");


                // Add new sets
                foreach (var externalDataInfoPair in externalDataInformation)
                {
                    var externalDataName = externalDataInfoPair.Key;
                    var externalDataId = externalDataInfoPair.Value;

                    if (string.IsNullOrEmpty(externalDataName) || string.IsNullOrEmpty(externalDataId))
                    {
                        appLogger.LogWarning($"Could not create new external data nodes, because information is not complete. {basicDebugString}, stack-trace: {GetStackTrace()}");
                    }
                    else
                    {
                        // appLogger.LogInformation($"externalDataName: {externalDataName}, externalDataId: {externalDataId}");

                        // Create a new set node construction
                        var nodeSet = xmlDataConfiguration.CreateElement("set");
                        SetAttribute(nodeSet, "id", externalDataId);

                        var nodeName = xmlDataConfiguration.CreateElement("name");
                        nodeName.InnerText = externalDataName;
                        nodeSet.AppendChild(nodeName);

                        nodeSets.AppendChild(nodeSet);
                    }
                }

                // Console.WriteLine("#########################################");
                // Console.WriteLine(PrettyPrintXml(nodeProject));
                // Console.WriteLine("#########################################");
                // HandleError("Thrown on purpose");

                // Save the xml file
                xmlDataConfiguration.Save(dataConfigurationPathOs);

                // Update the information in Application Configuration with the new content of Data Configuration
                TaxxorReturnMessage updateApplicationConfigResult = await UpdateDataConfigurationInApplicationConfiguration();

                if (!updateApplicationConfigResult.Success)
                {
                    HandleError(reqVars.returnType, updateApplicationConfigResult.Message, $"updateApplicationConfigResult.DebugInfo: {updateApplicationConfigResult.DebugInfo}");
                }
                else
                {
                    // Write a response
                    await context.Response.OK(GenerateSuccessXml("Successfully changed the project properties", basicDebugString), ReturnTypeEnum.Xml, true);
                }
            }
            catch (Exception ex)
            {
                HandleError("Could not update the project properties", $"error: {ex}, {basicDebugString}, stack-trace: {GetStackTrace()}");
            }

        }
    }
}