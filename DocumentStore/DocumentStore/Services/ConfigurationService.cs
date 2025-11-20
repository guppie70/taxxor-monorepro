using System;
using System.Threading.Tasks;
using Grpc.Core;
using DocumentStore.Protos;
using Taxxor.Project;
using System.Xml;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.Features;
using System.IO;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace DocumentStore.Services
{
    public class ConfigurationService : Protos.ConfigurationService.ConfigurationServiceBase
    {
        public override async Task<TaxxorGrpcResponseMessage> GetServiceInformation(
            GetServiceInformationRequest request, ServerCallContext context)
        {
            try
            {
                // Get the current context variables
                var reqVars = ProjectLogic.RetrieveRequestVariables(System.Web.Context.Current);
                
                // Call the core logic directly
                var result = await Framework.RenderServiceDescriptionCore();
                
                if (result.Success)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = result.XmlResponse.OuterXml,
                        Success = true,
                        Message = "Service information retrieved successfully"
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
                    Message = $"Error retrieving service information: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> GetTaxxorServices(
            GetTaxxorServicesRequest request, ServerCallContext context)
        {
            try
            {
                // Get the current context variables
                var reqVars = ProjectLogic.RetrieveRequestVariables(System.Web.Context.Current);
                
                // Call the core logic directly
                var result = await ProjectLogic.RetrieveTaxxorServicesConfigurationCore();
                
                if (result.Success)
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = result.XmlResponse.OuterXml,
                        Success = true,
                        Message = "Taxxor services configuration retrieved successfully"
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
                    Message = $"Error retrieving Taxxor services configuration: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> GetProjectConfiguration(
            GetProjectConfigurationRequest request, ServerCallContext context)
        {
            try
            {
                // Create a mock HTTP context to use with existing implementation
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                var httpResponse = new MemoryHttpResponse();
                var routeData = new Microsoft.AspNetCore.Routing.RouteData();

                // Call the existing implementation
                routeData.Values.Add("fileid", "project_configuration");
                await ProjectLogic.RetrieveXmlConfiguration(httpRequest, httpResponse, routeData);

                // Get the response content
                string content = httpResponse.GetContent();

                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = true,
                    Message = "Project configuration retrieved successfully"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error retrieving project configuration: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> GetDataConfiguration(
            GetDataConfigurationRequest request, ServerCallContext context)
        {
            try
            {
                // Create a mock HTTP context to use with existing implementation
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                var httpResponse = new MemoryHttpResponse();
                var routeData = new Microsoft.AspNetCore.Routing.RouteData();

                // Call the existing implementation
                routeData.Values.Add("fileid", "data_configuration");
                await ProjectLogic.RetrieveXmlConfiguration(httpRequest, httpResponse, routeData);

                // Get the response content
                string content = httpResponse.GetContent();

                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = true,
                    Message = "Data configuration retrieved successfully"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error retrieving data configuration: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }

        public override async Task<TaxxorGrpcResponseMessage> GetProjectTemplates(
            GetProjectTemplatesRequest request, ServerCallContext context)
        {
            try
            {
                // Create a mock HTTP context to use with existing implementation
                var httpContext = new DefaultHttpContext();
                var httpRequest = httpContext.Request;
                var httpResponse = new MemoryHttpResponse();
                var routeData = new Microsoft.AspNetCore.Routing.RouteData();

                // Call the existing implementation
                await ProjectLogic.RetrieveCmsTemlatesXmlConfiguration(httpRequest, httpResponse, routeData);

                // Get the response content
                string content = httpResponse.GetContent();

                // Return the response
                return new TaxxorGrpcResponseMessage
                {
                    Data = content,
                    Success = true,
                    Message = "Project templates retrieved successfully"
                };
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error retrieving project templates: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }

        // This method is now implemented in lines 58-96 using the Core method
        // Keeping this comment as a marker for clarity
    }

    // Memory-based HTTP response for capturing output
    public class MemoryHttpResponse : HttpResponse
    {
        private readonly MemoryStream _body;
        private readonly HeaderDictionary _headers;
        private int _statusCode;
        private string? _contentType;

        public MemoryHttpResponse()
        {
            _body = new MemoryStream();
            _headers = new HeaderDictionary();
            _statusCode = 200;
            _contentType = "text/html";
        }

        public override HttpContext HttpContext => null;

        public override IHeaderDictionary Headers => _headers;

        public override Stream Body { get => _body; set { } }

        public override long? ContentLength { get; set; }

        public override string? ContentType { get => _contentType; set => _contentType = value; }

        public override IResponseCookies Cookies => null;

        public override bool HasStarted => false;

        public override int StatusCode { get => _statusCode; set => _statusCode = value; }

        public override void OnStarting(Func<object, Task> callback, object state) { }

        public override void OnCompleted(Func<object, Task> callback, object state) { }

        public override void Redirect(string location, bool permanent) { }

        public string GetContent()
        {
            _body.Position = 0;
            using (var reader = new StreamReader(_body, leaveOpen: true))
            {
                return reader.ReadToEnd();
            }
        }
    }
}