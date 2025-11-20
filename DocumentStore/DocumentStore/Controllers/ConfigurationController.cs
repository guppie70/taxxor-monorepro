using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Taxxor.Project;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.Features;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace DocumentStore.Controllers
{
    [ApiController]
    [Route("v2api/taxxoreditor/configuration")]
    public class ConfigurationController : ControllerBase
    {
        [HttpGet("serviceinformation")]
        public async Task<IActionResult> GetServiceInformation()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Call the core logic method
            var result = await Framework.RenderServiceDescriptionCore();
            
            // Return the response based on the result
            if (result.Success)
            {
                // Set content type and return XML response
                Response.ContentType = "application/xml";
                await Response.WriteAsync(result.XmlResponse.OuterXml);
                return new EmptyResult(); // Response already sent
            }
            else
            {
                return BadRequest(result.ErrorMessage);
            }
        }
        
        [HttpGet("project_configuration")]
        public async Task<IActionResult> GetProjectConfiguration()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            routeData.Values.Add("fileid", Path.GetFileName(reqVars.thisUrlPath));
            await ProjectLogic.RetrieveXmlConfiguration(Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }
        
        [HttpGet("data_configuration")]
        public async Task<IActionResult> GetDataConfiguration()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            routeData.Values.Add("fileid", Path.GetFileName(reqVars.thisUrlPath));
            await ProjectLogic.RetrieveXmlConfiguration(Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }
        
        [HttpGet("project_templates")]
        public async Task<IActionResult> GetProjectTemplates()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            await ProjectLogic.RetrieveCmsTemlatesXmlConfiguration(Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }
        
        [HttpGet("taxxorservices")]
        public async Task<IActionResult> GetTaxxorServices()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Call the core logic method
            var result = await ProjectLogic.RetrieveTaxxorServicesConfigurationCore();
            
            // Return the response based on the result
            if (result.Success)
            {
                // Set content type and return XML response
                Response.ContentType = "application/xml";
                await Response.WriteAsync(result.XmlResponse.OuterXml);
                return new EmptyResult(); // Response already sent
            }
            else
            {
                return BadRequest(result.ErrorMessage);
            }
        }
    }
    
    // Mock HTTP Response class to adapt the existing implementation
    public class MockHttpResponse : HttpResponse
    {
        private readonly HttpResponse _response;
        
        public MockHttpResponse(HttpResponse response)
        {
            _response = response;
        }
        
        public override HttpContext HttpContext => _response.HttpContext;
        
        public override IHeaderDictionary Headers => _response.Headers;
        
        public override Stream Body { get => _response.Body; set => _response.Body = value; }
        
        public override long? ContentLength { get => _response.ContentLength; set => _response.ContentLength = value; }
        
        public override string? ContentType { get => _response.ContentType; set => _response.ContentType = value; }
        
        public override IResponseCookies Cookies => _response.Cookies;
        
        public override bool HasStarted => _response.HasStarted;
        
        public override int StatusCode { get => _response.StatusCode; set => _response.StatusCode = value; }
        
        public override void OnStarting(Func<object, Task> callback, object state)
        {
            _response.OnStarting(callback, state);
        }
        
        public override void OnCompleted(Func<object, Task> callback, object state)
        {
            _response.OnCompleted(callback, state);
        }
        
        public override void Redirect(string location, bool permanent)
        {
            _response.Redirect(location, permanent);
        }
    }
}
