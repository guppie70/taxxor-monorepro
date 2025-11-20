using AutoMapper;
using System.Threading.Tasks;
using Grpc.Core;
using DocumentStore.Protos;
using Taxxor.Project;
using System.Xml;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.Features;
using System.Web;
using static Framework;
using static Taxxor.Project.ProjectLogic;
using Microsoft.Extensions.Logging;
using System;

namespace DocumentStore.Services
{

    public class FilingComposerDataService : Protos.FilingComposerDataService.FilingComposerDataServiceBase
    {

        private readonly RequestContext _requestContext;
        private readonly IMapper _mapper;

        public FilingComposerDataService(RequestContext requestContext, IMapper mapper)
        {
            _requestContext = requestContext;
            _mapper = mapper;
        }


        /// <summary>
        /// Retrieves filing composer data (to use in the editor UI for example)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns>
        public override async Task<TaxxorGrpcResponseMessage> GetFilingComposerData(GetFilingComposerDataRequest request, ServerCallContext context)
        {
            var reqVars = _requestContext.RequestVariables;
            var baseDebugInfo = "";

            // To please the compiler
            await DummyAwaiter();

            try
            {
                // var projectVarsForDataRetrieval = new ProjectVariables
                // {
                //     projectId = request.GrpcProjectVariables.ProjectId,
                //     versionId = request.GrpcProjectVariables.VersionId,
                //     editorId = request.GrpcProjectVariables.EditorId,
                //     outputChannelType = request.GrpcProjectVariables.OutputChannelType,
                //     outputChannelVariantId = request.GrpcProjectVariables.OutputChannelVariantId,
                //     outputChannelVariantLanguage = request.GrpcProjectVariables.OutputChannelVariantLanguage,
                //     editorContentType = request.GrpcProjectVariables.EditorContentType,
                //     reportTypeId = request.GrpcProjectVariables.ReportTypeId
                // };
                // FillCorePathsInProjectVariables(ref projectVarsForDataRetrieval);

                // Create a project variables object from the request
                var projectVarsForDataRetrieval = _mapper.Map<ProjectVariables>(request);
                FillCorePathsInProjectVariables(ref projectVarsForDataRetrieval);

                baseDebugInfo = $"projectId: '{projectVarsForDataRetrieval.projectId}', versionId: '{projectVarsForDataRetrieval.versionId}', dataType: '{request.DataType}', id (section): '{request.Did}', projectVars.editorContentType: '{projectVarsForDataRetrieval.editorContentType}', projectVars.reportTypeId: '{projectVarsForDataRetrieval.reportTypeId}'";

                if (!ValidateCmsPostedParameters(projectVarsForDataRetrieval, request.DataType))
                {
                    appLogger.LogError($"There was an error validating the posted values {baseDebugInfo}, stack-trace: {GetStackTrace()}");
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = string.Empty,
                        Success = false,
                        Message = $"There was an error validating the posted values",
                        Debuginfo = baseDebugInfo
                    };
                }


                // Retrieve the XML data that we need to return
                var xml = "nothing";
                var debugRoutine = false;

                if (debugRoutine)
                {
                    appLogger.LogInformation($"projectId: {projectVarsForDataRetrieval.projectId}, versionId: {projectVarsForDataRetrieval.versionId}, dataType: {request.DataType}, id: {request.Did}");
                }

                switch (request.DataType)
                {
                    case "text":

                                // Calculate the base path to the xml file that contains the section XML file
            var xmlSectionPathOs = RetrieveInlineFilingComposerXmlPathOs(reqVars, projectVarsForDataRetrieval, request.Did, debugRoutine);

            // Load and resolve the section XML data and return the result as a string
            xml =  LoadAndResolveInlineFilingComposerData(reqVars, projectVarsForDataRetrieval, xmlSectionPathOs).OuterXml;
                        break;

                    case "config":
                        //configuration files to retrieve
                        switch (request.Did)
                        {
                            case "appconfig":
                                xml = xmlApplicationConfiguration.InnerXml;
                                break;

                            case "snapshotconfig":
                                xml = xmlApplicationConfiguration.SelectSingleNode("//cms_project[@id=" + GenerateEscapedXPathString(projectVarsForDataRetrieval.projectId) + "]").OuterXml;
                                break;

                        }
                        break;
                }

                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = xml,
                    Success = true,
                    Message = "Filing data retrieved successfully",
                    Debuginfo = baseDebugInfo
                };
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"There was an error retrieving the section data {baseDebugInfo}");

                // Catch-all for unexpected errors
                throw new RpcException(new Status(
                    StatusCode.Internal,
                    $"Internal error: {ex.Message}"
                ));
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> SetFilingComposerData(
            SetFilingComposerDataRequest request, ServerCallContext context)
        {
            try
            {
                // Get the current context variables
                var httpContext = new DefaultHttpContext();
                var reqVars = ProjectLogic.RetrieveRequestVariables(System.Web.Context.Current);
                var projectVars = ProjectLogic.RetrieveProjectVariables(System.Web.Context.Current);

                // Call the core logic directly
                var result = await ProjectLogic.StoreFilingDataCore(
                    locationId: null,
                    path: request.Id,
                    relativeTo: "project",
                    data: request.Data,
                    projectVars: projectVars,
                    reqVars: reqVars);

                if (result.Success)
                {
                    // Convert XmlDocument to string
                    string content = result.XmlResponse.OuterXml;

                    // Return the response
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = content,
                        Success = true,
                        Message = "Filing data stored successfully"
                    };
                }
                else
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = string.Empty,
                        Success = false,
                        Message = $"Error storing filing data: {result.ErrorMessage}"
                    };
                }
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error storing filing data: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> GetFilingComposerDataList(
            GetFilingComposerDataListRequest request, ServerCallContext context)
        {
            try
            {
                // This is a new endpoint that wasn't in the original API
                // It would retrieve a list of available filing data
                await Framework.DummyAwaiter();

                // For now, we'll return a not implemented response
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = "This endpoint is not yet implemented"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error retrieving filing data list: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
    }
}