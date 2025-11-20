using System.Threading.Tasks;
using Grpc.Core;
using DocumentStore.Protos;
using Taxxor.Project;
using System.Xml;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.Features;

namespace DocumentStore.Services
{
    public class ProjectManagementService : Protos.ProjectManagementService.ProjectManagementServiceBase
    {
        public override async Task<TaxxorGrpcResponseMessage> ListAvailableProjects(
            ListAvailableProjectsRequest request, ServerCallContext context)
        {
            try
            {
                // Create a mock HTTP context to use with existing implementation
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                var httpResponse = new MemoryHttpResponse();
                var routeData = new Microsoft.AspNetCore.Routing.RouteData();
                
                // Call the existing implementation
                await ProjectLogic.ListAvailableProjects(httpRequest, httpResponse, routeData);
                
                // Get the response content
                string content = httpResponse.GetContent();
                
                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = true,
                    Message = "Projects list retrieved successfully"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error retrieving projects list: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> CreateProject(
            CreateProjectRequest request, ServerCallContext context)
        {
            try
            {
                // Create a mock HTTP context to use with existing implementation
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                
                // Add the required form data to the request
                var formCollection = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { "name", request.Name },
                    { "rtype", request.Template },
                    { "id", System.Guid.NewGuid().ToString() }, // Generate a new ID
                    { "data", request.Data }
                });
                
                httpRequest.Form = formCollection;
                
                var httpResponse = new MemoryHttpResponse();
                var routeData = new Microsoft.AspNetCore.Routing.RouteData();
                
                // Call the existing implementation
                await ProjectLogic.CreateProjectStructure(httpRequest, httpResponse, routeData);
                
                // Get the response content
                string content = httpResponse.GetContent();
                
                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = true,
                    Message = "Project created successfully"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error creating project: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> UpdateProject(
            UpdateProjectRequest request, ServerCallContext context)
        {
            try
            {
                // This is a new endpoint that wasn't in the original API
                // It would update a specific project
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
                    Message = $"Error updating project: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> DeleteProject(
            DeleteProjectRequest request, ServerCallContext context)
        {
            try
            {
                // Create a mock HTTP context to use with existing implementation
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                
                // Add the required form data to the request
                var formCollection = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { "id", request.Id }
                });
                
                httpRequest.Form = formCollection;
                
                var httpResponse = new MemoryHttpResponse();
                var routeData = new Microsoft.AspNetCore.Routing.RouteData();
                
                // Call the existing implementation
                await ProjectLogic.DeleteProject(httpRequest, httpResponse, routeData);
                
                // Get the response content
                string content = httpResponse.GetContent();
                
                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = true,
                    Message = "Project deleted successfully"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error deleting project: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> GetProjectDetails(
            GetProjectDetailsRequest request, ServerCallContext context)
        {
            try
            {
                // This is a new endpoint that wasn't in the original API
                // It would retrieve details for a specific project

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
                    Message = $"Error retrieving project details: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
    }
}