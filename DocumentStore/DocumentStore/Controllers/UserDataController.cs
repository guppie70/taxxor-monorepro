using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Taxxor.Project;

namespace DocumentStore.Controllers
{
    [ApiController]
    [Route("v2api/taxxoreditor/user")]
    public class UserDataController : ControllerBase
    {
        [HttpGet("data")]
        public async Task<IActionResult> GetUserData()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Call the core logic method
            var result = await ProjectLogic.RetrieveUserDataCore(reqVars);
            
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

        [HttpPut("data")]
        [HttpPost("data")]
        public async Task<IActionResult> SetUserData()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Get posted values
            string? type = Request.Form["type"];
            string? key = Request.Form["key"];
            string? value = Request.Form["value"];
            
            // Call the core logic method
            var result = await ProjectLogic.SetUserDataCore(type, key, value, reqVars);
            
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

        [HttpGet("data/temp")]
        public async Task<IActionResult> GetUserTempData()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            await ProjectLogic.RetrieveUserTempData(Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }

        [HttpPut("data/temp")]
        [HttpPost("data/temp")]
        public async Task<IActionResult> SetUserTempData()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            await ProjectLogic.StoreUserTempData(Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }

        [HttpDelete("data/temp")]
        public async Task<IActionResult> DeleteUserTempData()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);
            
            // Use the existing implementation logic
            var response = new MockHttpResponse(Response);
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            await ProjectLogic.DeleteUserTempData(Request, response, routeData);
            
            return new EmptyResult(); // Response already sent
        }
    }
}
