using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal loading data for a Taxxor Editor Filing
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Adds a generated report to the GeneratedReports repository
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task AddGeneratedReport(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var path = request.RetrievePostedValue("path", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var scheme = request.RetrievePostedValue("scheme", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var user = request.RetrievePostedValue("user", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var relativeTo = request.RetrievePostedValue("relativeto", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var validationInformation = request.RetrievePostedValue("xmlvalidationinformation", RegexEnum.None, false, ReturnTypeEnum.Xml);

            var xmlValidationInformation = new XmlDocument();
            xmlValidationInformation.LoadXml(validationInformation);

            var sourcePathOs = "";
            var targetFilePathOs = "";
            var epoch = DateTimeOffset.Now.ToUnixTimeSeconds();
            try
            {
                // Load or create new xml document containing all reports
                var xmlContents = new XmlDocument();
                var xmlFilePathOs = CalculateFullPathOs($"/_contents.xml", "projectreports", reqVars, projectVars);
                if (!File.Exists(xmlFilePathOs))
                {
                    xmlContents.AppendChild(xmlContents.CreateElement("reports"));
                    // Assure that in the JSON output we always render an array
                    var attrForceArray = xmlContents.CreateAttribute("json", "Array", "http://james.newtonking.com/projects/json");
                    attrForceArray.InnerText = "true";
                    xmlContents.DocumentElement.Attributes.Append(attrForceArray);
                }
                else
                {
                    xmlContents.Load(xmlFilePathOs);
                }

                // Copy the generated report to the target folder
                sourcePathOs = $"{RetrieveSharedFolderPathOs()}/{path}";
                var sourceFileName = Path.GetFileName(sourcePathOs);
                var targetFileName = $"{Path.GetFileNameWithoutExtension(sourcePathOs)}---{epoch}{Path.GetExtension(sourcePathOs)}";
                targetFilePathOs = CalculateFullPathOs($"/{targetFileName}", "projectreports", reqVars, projectVars);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFilePathOs));
                File.Copy(sourcePathOs, targetFilePathOs, true);

                // Add a node with information about the report to the xml document and save it
                var nodeReport = xmlContents.CreateElement("report");
                var id = Guid.NewGuid().ToString();
                nodeReport.SetAttribute("id", id);
                nodeReport.SetAttribute("filename", sourceFileName);
                nodeReport.SetAttribute("epoch", epoch.ToString());
                nodeReport.SetAttribute("user", user);
                nodeReport.SetAttribute("scheme", scheme);
                nodeReport.AppendChild(xmlContents.CreateElement("comment"));
                var nodeValidationInformation = xmlContents.ImportNode(xmlValidationInformation.DocumentElement, true);
                nodeReport.AppendChild(nodeValidationInformation);

                xmlContents.DocumentElement.AppendChild(nodeReport);
                await xmlContents.SaveAsync(xmlFilePathOs, true, true);

                await response.OK(
                    new TaxxorReturnMessage(
                        true,
                        "Successfully added report to the repository",
                        id,
                        $"sourcePathOs: {sourcePathOs}, targetFilePathOs: {targetFilePathOs}"
                        )
                    , ReturnTypeEnum.Xml
                );
            }
            catch (Exception ex)
            {
                var errorMessage = "There was an error adding the report to the repository";
                appLogger.LogError(ex, errorMessage);
                await response.Error(
                    new TaxxorReturnMessage(
                        false,
                        errorMessage,
                        $"sourcePathOs: {sourcePathOs}, targetFilePathOs: {targetFilePathOs}"
                        )
                    , ReturnTypeEnum.Xml
                );
            }

        }

        /// <summary>
        /// Adds a comment to the repository item
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task AddGeneratedReportComment(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var guid = request.RetrievePostedValue("guid", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var comment = request.RetrievePostedValue("comment", RegexEnum.None, false, ReturnTypeEnum.Xml);


            try
            {
                // Load or create new xml document containing all reports
                var xmlContents = new XmlDocument();
                var xmlFilePathOs = CalculateFullPathOs($"/_contents.xml", "projectreports", reqVars, projectVars);
                if (!File.Exists(xmlFilePathOs))
                {
                    xmlContents.AppendChild(xmlContents.CreateElement("reports"));
                }
                else
                {
                    xmlContents.Load(xmlFilePathOs);
                }

                var nodeReport = xmlContents.SelectSingleNode($"/reports/report[@id='{guid}']");
                if (nodeReport != null)
                {
                    nodeReport.SelectSingleNode("comment").InnerText = comment;
                    await xmlContents.SaveAsync(xmlFilePathOs, true, true);

                }
                else
                {
                    throw new Exception($"Report with guid '{guid}' not found");
                }


                await response.OK(
                    new TaxxorReturnMessage(
                        true,
                        "Successfully stored repository comment",
                        $"guid: {guid}"
                        )
                    , ReturnTypeEnum.Xml
                );
            }
            catch (Exception ex)
            {
                var errorMessage = "There was an error adding the report to the repository";
                appLogger.LogError(ex, errorMessage);
                await response.Error(
                    new TaxxorReturnMessage(
                        false,
                        errorMessage,
                        $"guid: {guid}"
                        )
                    , ReturnTypeEnum.Xml
                );
            }

        }


        /// <summary>
        /// Retrieves (filtered) contents of the generated reports repository
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public static async Task RetrieveRepositoryContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var scheme = request.RetrievePostedValue("filterscheme", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var user = request.RetrievePostedValue("filteruser", RegexEnum.None, false, ReturnTypeEnum.Xml);
            var guid = request.RetrievePostedValue("filterguid", RegexEnum.None, false, ReturnTypeEnum.Xml);


            try
            {
                // Load or create new xml document containing all reports
                var xmlContents = new XmlDocument();
                var xmlFilePathOs = CalculateFullPathOs($"/_contents.xml", "projectreports", reqVars, projectVars);
                if (!File.Exists(xmlFilePathOs))
                {
                    xmlContents.AppendChild(xmlContents.CreateElement("reports"));
                }
                else
                {
                    xmlContents.Load(xmlFilePathOs);
                }

                // Apply filters if needed
                if (!string.IsNullOrEmpty(scheme))
                {
                    var nodeListReportsToRemove = xmlContents.SelectNodes($"/reports/report[not(@scheme='{scheme}')]");
                    if (nodeListReportsToRemove.Count > 0) RemoveXmlNodes(nodeListReportsToRemove);
                }
                if (!string.IsNullOrEmpty(user))
                {
                    var nodeListReportsToRemove = xmlContents.SelectNodes($"/reports/report[not(@user='{user}')]");
                    if (nodeListReportsToRemove.Count > 0) RemoveXmlNodes(nodeListReportsToRemove);
                }
                if (!string.IsNullOrEmpty(guid))
                {
                    var nodeListReportsToRemove = xmlContents.SelectNodes($"/reports/report[not(@guid='{guid}')]");
                    if (nodeListReportsToRemove.Count > 0) RemoveXmlNodes(nodeListReportsToRemove);
                }


                await response.OK(
                    new TaxxorReturnMessage(
                        true,
                        "Successfully retrived repository content",
                        xmlContents,
                        $"scheme: {scheme}"
                        )
                    , ReturnTypeEnum.Xml
                );
            }
            catch (Exception ex)
            {
                var errorMessage = "There was an error retrieving the repository content";
                appLogger.LogError(ex, errorMessage);
                await response.Error(
                    new TaxxorReturnMessage(
                        false,
                        errorMessage,
                        $"scheme: {scheme}"
                        )
                    , ReturnTypeEnum.Xml
                );
            }

        }

    }
}