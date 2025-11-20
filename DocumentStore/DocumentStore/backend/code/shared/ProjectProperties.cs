using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.Extensions.Logging;

/// <summary>
/// Object to be used in SOAP, REST, Web Requests and define custom HTTP Header properties
/// </summary>
namespace Taxxor.Project
{

    /// <summary>
    /// Generic utilities for making the application work
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Defines a project status
        /// </summary>
        public enum ProjectStatusEnum
        {
            Open,
            Closed
        }

        /// <summary>
        /// Converts a project status string to a ProjectStatusEnum
        /// </summary>
        /// <param name="projectStatus"></param>
        /// <returns></returns>
        public static ProjectStatusEnum GetProjectStatusEnum(string projectStatus)
        {
            switch (projectStatus.ToLower())
            {
                case "open":
                    return ProjectStatusEnum.Open;
                case "closed":
                    return ProjectStatusEnum.Closed;
                default:
                    appLogger.LogWarning($"Unknown project status {projectStatus}, defaulting to ProjectStatusEnum.Open");
                    return ProjectStatusEnum.Open;
            }
        }

        /// <summary>
        /// Converts a ProjectStatusEnum to a string
        /// </summary>
        /// <param name="projectStatusEnum"></param>
        /// <returns></returns>
        public static string ProjectStatusEnumToString(ProjectStatusEnum projectStatusEnum)
        {
            switch (projectStatusEnum)
            {
                case ProjectStatusEnum.Open:
                    return "open";
                case ProjectStatusEnum.Closed:
                    return "closed";
                default:
                    appLogger.LogWarning($"Unknown project status enum {projectStatusEnum.ToString()}, defaulting to \"open\"");
                    return "open";
            }
        }



        /// <summary>
        /// Base class containing basic project properties
        /// </summary>
        public abstract class ProjectPropertiesBase
        {
            /// <summary>
            /// ID of the current project - pid parameter in querystring
            /// </summary>
            public string? projectId = null;

            /// <summary>
            /// Type of edited report - rtype parameter in querystring
            /// </summary>
            public string? reportTypeId = null;
        }


        /// <summary>
        /// Class that contains typical CMS Project Properties
        /// </summary>
        public class CmsProjectProperties : ProjectPropertiesBase
        {
            /// <summary>
            /// Name of the current project
            /// </summary>
            public string? name = null;

            /// <summary>
            /// Legal entity ID (owner of this project)
            /// </summary>
            public string? guidLegalEntity = null;

            /// <summary>
            /// Defines the period this project is reporting on
            /// </summary>
            public string? reportingPeriod = null;

            /// <summary>
            /// Publication date for this project (needs to become a Date object)
            /// </summary>
            public DateTime publicationDate;

            /// <summary>
            /// Status of the current project
            /// </summary>
            public ProjectStatusEnum projectStatus = ProjectStatusEnum.Open;

            /// <summary>
            /// List of reporting requirements associated with this project
            /// </summary>
            /// <typeparam name="ReportingRequirement"></typeparam>
            /// <returns></returns>
            public List<ReportingRequirement> reportingRequirements = new List<ReportingRequirement>();

            /// <summary>
            /// The external datasets which are associated with a project
            /// </summary>
            /// <typeparam name="ExternalDataSet"></typeparam>
            /// <returns></returns>
            public List<ExternalDataSet> externalDataSets = new List<ExternalDataSet>();

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="projectId">Project ID</param>
            public CmsProjectProperties(string projectId)
            {
                this.projectId = projectId;
            }

            /// <summary>
            /// Retrieves the publication date in ISO format
            /// </summary>
            /// <param name="shortFormat">Set to true if a short ISO format is needed (yyyy-mm-dd)</param>
            /// <returns></returns>
            public string? GetPublicationDate(bool shortFormat = false)
            {
                var isTimeNull = (this.publicationDate == DateTime.MinValue);
                if (isTimeNull)
                {
                    return null;
                }
                else
                {
                    return createIsoTimestamp(this.publicationDate, shortFormat);
                }
            }

            /// <summary>
            /// Sets the launch date field
            /// </summary>
            /// <param name="date"></param>
            public void SetPublicationDate(string date)
            {
                if (string.IsNullOrEmpty(date))
                {
                    appLogger.LogWarning("Could not convert empty date string to a Date object");
                }
                else
                {
                    try
                    {
                        this.publicationDate = DateTime.Parse(date);
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError($"Could not parse date. error: {ex.ToString()}, stack-trace: {GetStackTrace()}");
                    }
                }
            }

            /// <summary>
            /// Dumps the content of this object to a string so it can be used for debugging purposes
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return GenerateDebugObjectString(this, ReturnTypeEnum.Txt);
            }
        }


        /// <summary>
        /// Represents a Taxxor Reporting Requirement
        /// </summary>
        public class ReportingRequirement
        {
            /// <summary>
            /// Name
            /// </summary>
            public string? Name = null;

            /// <summary>
            /// Unique ID for this requirement (used in the mapping service for instance)
            /// </summary>
            public string? Id = null;

            /// <summary>
            /// CMS output channel ID associated with this reporting requirement
            /// </summary>
            public string? OutputChannelVariantId = null;

            /// <summary>
            /// Format of the outpt for this reporting requirement "sechtml" | "ixbrl" are supported
            /// </summary>
            public string? OutputFormat = null;

            /// <summary>
            /// ID of the regulator "sec" is supported
            /// </summary>
            public string? RegulatorId = null;

            /// <summary>
            /// Constructor
            /// </summary>
            public ReportingRequirement()
            {

            }

            /// <summary>
            /// Converts a reporting requirement to an XML Document that can be used in the configuration
            /// </summary>
            /// <returns></returns>
            public XmlDocument ToXml()
            {
                var xmlReportingRequirement = new XmlDocument();


                var nodeReportingRequirement = xmlReportingRequirement.CreateElement("reporting_requirement");
                nodeReportingRequirement.SetAttribute("ref-mappingservice", this.Id ?? "");
                nodeReportingRequirement.SetAttribute("ref-outputchannelvariant", this.OutputChannelVariantId ?? "");
                nodeReportingRequirement.SetAttribute("format", this.OutputFormat ?? "");
                nodeReportingRequirement.SetAttribute("regulator", this.RegulatorId ?? "");

                var nodeReportingRequirementName = xmlReportingRequirement.CreateElement("name");
                nodeReportingRequirementName.InnerText = this.Name;
                nodeReportingRequirement.AppendChild(nodeReportingRequirementName);

                xmlReportingRequirement.AppendChild(nodeReportingRequirement);

                return xmlReportingRequirement;
            }
        }

        /// <summary>
        /// Represents an external data set used in the project configuration
        /// </summary>
        public class ExternalDataSet
        {

            /// <summary>
            /// Unique identifier of a dataset
            /// </summary>
            public string? Id = null;

            /// <summary>
            /// Name of the external dataset
            /// </summary>
            public string? Name = null;

            /// <summary>
            /// Constructor
            /// </summary>
            public ExternalDataSet()
            {

            }

            /// <summary>
            /// Converts an external data set to xml structure that can be used in the configuration
            /// </summary>
            /// <returns></returns>
            public XmlDocument ToXml()
            {
                var xmlExternalDataSet = new XmlDocument();

                var nodeSet = xmlExternalDataSet.CreateElement("set");
                nodeSet.SetAttribute("id", this.Id);

                var nodeName = xmlExternalDataSet.CreateElement("name");
                nodeName.InnerText = this.Name;
                nodeSet.AppendChild(nodeName);

                xmlExternalDataSet.AppendChild(nodeSet);

                return xmlExternalDataSet;
            }

        }
    }

}