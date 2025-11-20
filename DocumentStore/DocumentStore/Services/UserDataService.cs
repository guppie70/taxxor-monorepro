using System.Threading.Tasks;
using Grpc.Core;
using DocumentStore.Protos;
using Taxxor.Project;
using System.Xml;
using Microsoft.AspNetCore.Http;
using static Framework;
using static Taxxor.Project.ProjectLogic;
using System.Collections.Generic;

namespace DocumentStore.Services
{
    public class UserDataService : Protos.UserDataService.UserDataServiceBase
    {
        public override async Task<TaxxorGrpcResponseMessage> GetUserData(
            GetUserDataRequest request, ServerCallContext context)
        {
            try
            {
                // Get the current context variables
                var reqVars = ProjectLogic.RetrieveRequestVariables(System.Web.Context.Current);
                
                // Call the core logic directly
                var result = await ProjectLogic.RetrieveUserDataCore(reqVars);
                
                if (result.Success)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = result.XmlResponse.OuterXml,
                        Success = true,
                        Message = "User data retrieved successfully"
                    };
                }
                else
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = string.Empty,
                        Success = false,
                        Message = result.ErrorMessage
                    };
                }
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error retrieving user data: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> SetUserData(
            SetUserDataRequest request, ServerCallContext context)
        {
            try
            {
                // Get the current context variables
                var reqVars = ProjectLogic.RetrieveRequestVariables(System.Web.Context.Current);
                
                // Call the core logic directly - we use hardcoded "userpreferences" as the type
                // since this is the primary type and the proto doesn't have a Type field
                var result = await ProjectLogic.SetUserDataCore(
                    "userpreferences", 
                    request.Key, 
                    request.Data, 
                    reqVars);
                
                if (result.Success)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = result.XmlResponse.OuterXml,
                        Success = true,
                        Message = "User data set successfully"
                    };
                }
                else
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = string.Empty,
                        Success = false,
                        Message = result.ErrorMessage
                    };
                }
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error setting user data: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> GetUserTempData(
            GetUserTempDataRequest request, ServerCallContext context)
        {
            try
            {
                // Create a mock HTTP context to use with existing implementation
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                
                // Add the key parameter to the query string
                httpRequest.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { "key", request.Key }
                });
                
                var httpResponse = new MemoryHttpResponse();
                var routeData = new Microsoft.AspNetCore.Routing.RouteData();
                
                // Call the existing implementation
                await ProjectLogic.RetrieveUserTempData(httpRequest, httpResponse, routeData);
                
                // Get the response content
                string content = httpResponse.GetContent();
                
                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = true,
                    Message = "User temporary data retrieved successfully"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error retrieving user temporary data: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> SetUserTempData(
            SetUserTempDataRequest request, ServerCallContext context)
        {
            try
            {
                // Create a mock HTTP context to use with existing implementation
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                
                // Add the required form data to the request
                httpRequest.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { "key", request.Key },
                    { "data", request.Data }
                });
                
                var httpResponse = new MemoryHttpResponse();
                var routeData = new Microsoft.AspNetCore.Routing.RouteData();
                
                // Call the existing implementation
                await ProjectLogic.StoreUserTempData(httpRequest, httpResponse, routeData);
                
                // Get the response content
                string content = httpResponse.GetContent();
                
                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = true,
                    Message = "User temporary data set successfully"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error setting user temporary data: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> DeleteUserTempData(
            DeleteUserTempDataRequest request, ServerCallContext context)
        {
            try
            {
                // Create a mock HTTP context to use with existing implementation
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                
                // Add the key parameter to the query string
                httpRequest.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { "key", request.Key }
                });
                
                var httpResponse = new MemoryHttpResponse();
                var routeData = new Microsoft.AspNetCore.Routing.RouteData();
                
                // Call the existing implementation
                await ProjectLogic.DeleteUserTempData(httpRequest, httpResponse, routeData);
                
                // Get the response content
                string content = httpResponse.GetContent();
                
                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = true,
                    Message = "User temporary data deleted successfully"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error deleting user temporary data: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
    }
}
