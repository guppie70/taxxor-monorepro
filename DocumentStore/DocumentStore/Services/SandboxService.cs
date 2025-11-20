using System.Threading.Tasks;
using Grpc.Core;
using DocumentStore.Protos;
using Taxxor.Project;
using System.Xml;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.Features;
using DocumentStore.Services;

namespace DocumentStore.Services
{
    public class SandboxService : Protos.SandboxService.SandboxServiceBase
    {
        public override async Task<TaxxorGrpcResponseMessage> ExecuteCommand(
            ExecuteCommandRequest request, ServerCallContext context)
        {
            try
            {
                // Get the current context variables
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                var reqVars = ProjectLogic.RetrieveRequestVariables(System.Web.Context.Current);
                
                string content = string.Empty;
                bool success = true;
                string message = $"Command '{request.Command}' executed successfully";
                
                // Process the command based on the request
                switch (request.Command.ToLower())
                {
                    case "test":
                        // Call the core logic directly
                        var testResult = await ProjectLogic.RenderTestResponseSandboxCore(
                            statusType: request.Parameters,
                            request: httpRequest,
                            reqVars: reqVars);
                        
                        content = testResult.ResponseContent;
                        success = testResult.Success;
                        if (!success)
                        {
                            message = testResult.ErrorMessage;
                        }
                        break;
                        
                    case "mergedataconfiguration":
                        // Call the core logic directly
                        var mergeResult = await ProjectLogic.SandboxMergeDataConfigurationCore();
                        content = mergeResult.ToString();
                        break;
                        
                    case "auditormessage":
                        // Call the core logic directly
                        var auditorResult = await ProjectLogic.SandboxAuditorMessageCore();
                        content = $"Project ID: {auditorResult.ProjectId}, Result: {auditorResult.Message}";
                        break;
                        
                    default:
                        return new TaxxorGrpcResponseMessage
                        {
                            Data = string.Empty,
                            Success = false,
                            Message = $"Unknown sandbox command: {request.Command}"
                        };
                }
                
                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = success,
                    Message = message
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error executing sandbox command: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override Task<TaxxorGrpcResponseMessage> GetSandboxStatus(
            GetSandboxStatusRequest request, ServerCallContext context)
        {
            try
            {
                // This is a new endpoint that provides information about the sandbox environment
                string status = "Running";
                bool available = true;
                string statusMessage = $"Sandbox environment is operational. Version: 9.0, Environment: {System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}";
                
                return Task.FromResult(new TaxxorGrpcResponseMessage
                {
                    // Put status information in the data field
                    Data = $"{{\"status\":\"{status}\",\"available\":{available.ToString().ToLower()}}}",
                    Success = true,
                    Message = statusMessage
                });
            }
            catch (System.Exception ex)
            {
                return Task.FromResult(new TaxxorGrpcResponseMessage
                {
                    Data = $"{{\"status\":\"Error\",\"available\":false}}",
                    Success = false,
                    Message = $"Error retrieving sandbox status: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                });
            }
        }
    }
}