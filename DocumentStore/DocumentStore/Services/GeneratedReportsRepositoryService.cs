using AutoMapper;
using System.Threading.Tasks;
using Grpc.Core;
using DocumentStore.Protos;
using Taxxor.Project;
using System.Xml;
using Microsoft.AspNetCore.Http;
using System.Web;
using static Framework;
using static Taxxor.Project.ProjectLogic;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace DocumentStore.Services
{

    public class GeneratedReportsRepositoryService : Protos.GeneratedReportsRepositoryService.GeneratedReportsRepositoryServiceBase
    {

        private readonly RequestContext _requestContext;
        private readonly IMapper _mapper;

        public GeneratedReportsRepositoryService(RequestContext requestContext, IMapper mapper)
        {
            _requestContext = requestContext;
            _mapper = mapper;
        }

        /// <summary>
        /// Adds a generated report to the repository
        /// </summary>
        public override async Task<TaxxorGrpcResponseMessage> Add(
            GeneratedReportsRepositoryAddRequest request, ServerCallContext context)
        {
            var baseDebugInfo = "";

            // To please the compiler
            await DummyAwaiter();

            try
            {
                // Map gRPC request to ProjectVariables
                var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);

                baseDebugInfo = $"projectId: '{projectVars.projectId}', " +
                    $"path: '{request.Path}', " +
                    $"reportRequirementScheme: '{request.ReportRequirementScheme}'";

                // Parse the XML validation information
                var xmlValidationInformation = new XmlDocument();
                if (!string.IsNullOrEmpty(request.XmlValidationInformation))
                {
                    xmlValidationInformation.LoadXml(request.XmlValidationInformation);
                }
                else
                {
                    xmlValidationInformation.AppendChild(xmlValidationInformation.CreateElement("validationinformation"));
                }

                // Extract file info
                var path = request.Path;
                var scheme = request.ReportRequirementScheme;
                var user = projectVars.currentUser?.Id ?? "unknown";

                // Perform the actual repository add logic
                var xmlContents = new XmlDocument();
                var xmlFilePathOs = CalculateFullPathOs($"/_contents.xml", "projectreports", _requestContext.RequestVariables, projectVars);

                if (!File.Exists(xmlFilePathOs))
                {
                    xmlContents.AppendChild(xmlContents.CreateElement("reports"));
                    var attrForceArray = xmlContents.CreateAttribute("json", "Array", "http://james.newtonking.com/projects/json");
                    attrForceArray.InnerText = "true";
                    xmlContents.DocumentElement.Attributes.Append(attrForceArray);
                }
                else
                {
                    xmlContents.Load(xmlFilePathOs);
                }

                // Copy the file
                var sourcePathOs = $"{RetrieveSharedFolderPathOs()}/{path}";
                var sourceFileName = Path.GetFileName(sourcePathOs);
                var epoch = DateTimeOffset.Now.ToUnixTimeSeconds();
                var targetFileName = $"{Path.GetFileNameWithoutExtension(sourcePathOs)}---{epoch}{Path.GetExtension(sourcePathOs)}";
                var targetFilePathOs = CalculateFullPathOs($"/{targetFileName}", "projectreports", _requestContext.RequestVariables, projectVars);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFilePathOs));
                File.Copy(sourcePathOs, targetFilePathOs, true);

                // Add report node to XML
                var nodeReport = xmlContents.CreateElement("report");
                var reportId = Guid.NewGuid().ToString();
                nodeReport.SetAttribute("id", reportId);
                nodeReport.SetAttribute("filename", sourceFileName);
                nodeReport.SetAttribute("epoch", epoch.ToString());
                nodeReport.SetAttribute("user", user);
                nodeReport.SetAttribute("scheme", scheme);
                nodeReport.AppendChild(xmlContents.CreateElement("comment"));
                var nodeValidationInformation = xmlContents.ImportNode(xmlValidationInformation.DocumentElement, true);
                nodeReport.AppendChild(nodeValidationInformation);

                xmlContents.DocumentElement.AppendChild(nodeReport);
                await xmlContents.SaveAsync(xmlFilePathOs, true, true);

                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "Successfully added report to the repository",
                    Debuginfo = baseDebugInfo,
                    Data = reportId
                };
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in GeneratedReportsRepository.Add: {baseDebugInfo}");
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = "There was an error adding the report to the repository",
                    Debuginfo = $"error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Retrieves (filtered) contents of the generated reports repository
        /// </summary>
        public override async Task<TaxxorGrpcResponseMessage> RetrieveContent(
            GeneratedReportsRepositoryRetrieveContentRequest request, ServerCallContext context)
        {
            var baseDebugInfo = "";

            // To please the compiler
            await DummyAwaiter();

            try
            {
                // Map gRPC request to ProjectVariables
                var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);

                baseDebugInfo = $"projectId: '{projectVars.projectId}', " +
                    $"filterScheme: '{request.FilterScheme}', " +
                    $"filterUser: '{request.FilterUser}', " +
                    $"filterGuid: '{request.FilterGuid}'";

                // Load the repository content
                var xmlContents = new XmlDocument();
                var xmlFilePathOs = CalculateFullPathOs($"/_contents.xml", "projectreports", _requestContext.RequestVariables, projectVars);

                if (!File.Exists(xmlFilePathOs))
                {
                    xmlContents.AppendChild(xmlContents.CreateElement("reports"));
                }
                else
                {
                    xmlContents.Load(xmlFilePathOs);
                }

                // Apply filters if needed
                if (!string.IsNullOrEmpty(request.FilterScheme))
                {
                    var nodeListReportsToRemove = xmlContents.SelectNodes($"/reports/report[not(@scheme='{request.FilterScheme}')]");
                    if (nodeListReportsToRemove.Count > 0) RemoveXmlNodes(nodeListReportsToRemove);
                }
                if (!string.IsNullOrEmpty(request.FilterUser))
                {
                    var nodeListReportsToRemove = xmlContents.SelectNodes($"/reports/report[not(@user='{request.FilterUser}')]");
                    if (nodeListReportsToRemove.Count > 0) RemoveXmlNodes(nodeListReportsToRemove);
                }
                if (!string.IsNullOrEmpty(request.FilterGuid))
                {
                    var nodeListReportsToRemove = xmlContents.SelectNodes($"/reports/report[not(@id='{request.FilterGuid}')]");
                    if (nodeListReportsToRemove.Count > 0) RemoveXmlNodes(nodeListReportsToRemove);
                }

                return new TaxxorGrpcResponseMessage
                {
                    Success = true,
                    Message = "Successfully retrieved repository content",
                    Debuginfo = baseDebugInfo,
                    Data = HttpUtility.HtmlEncode(xmlContents.DocumentElement.OuterXml)
                };
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error in GeneratedReportsRepository.RetrieveContent: {baseDebugInfo}");
                return new TaxxorGrpcResponseMessage
                {
                    Success = false,
                    Message = "There was an error retrieving the repository content",
                    Debuginfo = $"error: {ex.Message}"
                };
            }
        }
    }

}
