using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Taxxor.Project;
using System.Collections.Generic;

namespace DocumentStore.Controllers
{
    [ApiController]
    [Route("v2api/taxxoreditor/projects")]
    public class ProjectManagementController : ControllerBase
    {
        [HttpGet("list")]
        public async Task<IActionResult> ListAvailableProjects()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            await ProjectLogic.ListAvailableProjects(Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }

        [HttpPut("create")]
        public async Task<IActionResult> CreateProject()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            await ProjectLogic.CreateProjectStructure(Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(string id)
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Add the project ID to the form data
            var formCollection = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "id", id }
            });
            
            // Create a new request with the form data
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Form = formCollection;
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            await ProjectLogic.DeleteProject(httpContext.Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }
        
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProjectDetails(string id)
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            await Framework.DummyAwaiter();
            
            // This is a new endpoint that wasn't in the original API
            // It would retrieve details for a specific project

            // For now, we'll return a not implemented response
            return StatusCode(501, "This endpoint is not yet implemented");
        }
    }
}
