using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Taxxor.Project;

namespace DocumentStore.Controllers
{
    [ApiController]
    [Route("v2api/sandbox")]
    public class SandboxController : ControllerBase
    {
        [HttpGet("test/{type}")]
        public async Task<IActionResult> TestResponse(string type)
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Create route data with the type parameter
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            routeData.Values.Add("type", type);
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            await ProjectLogic.RenderTestResponseSandbox(Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteCommand([FromBody] SandboxCommandModel command)
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // This is a new endpoint that provides a more generic way to execute sandbox commands
            // It would map to different sandbox methods based on the command parameter
            
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            
            switch (command.Command.ToLower())
            {
                case "mergedataconfiguration":
                    await ProjectLogic.SandboxMergeDataConfiguration(Request, response, routeData);
                    break;
                    
                case "auditormessage":
                    await ProjectLogic.SandboxAuditorMessage(Request, response, routeData);
                    break;
                    
                default:
                    return BadRequest($"Unknown sandbox command: {command.Command}");
            }
            
            return new EmptyResult(); // Response already sent
        }
        
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            // This is a new endpoint that provides information about the sandbox environment
            
            return Ok(new
            {
                Status = "Running",
                Version = "9.0",
                Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            });
        }
    }
    
    public class SandboxCommandModel
    {
        public string? Command { get; set; }
        public string? Parameters { get; set; }
    }
}
