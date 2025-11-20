using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Taxxor.Project;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace DocumentStore.Controllers
{
    [ApiController]
    [Route("v2api/taxxoreditor")]
    public class SystemStateController : ControllerBase
    {
        [HttpGet("systemstate")]
        public async Task<IActionResult> GetSystemState()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);

            // Determine the response format based on the request
            switch (reqVars.returnType)
            {
                case ReturnTypeEnum.Xml:
                    // Set content type and return XML response
                    Response.ContentType = "application/xml";
                    await Response.WriteAsync(SystemState.ToXml().OuterXml);
                    return new EmptyResult(); // Response already sent

                case ReturnTypeEnum.Json:
                    // Return JSON response
                    return Content(SystemState.ToJson(), "application/json");

                default:
                    // Handle unsupported format
                    return BadRequest("Data type not supported");
            }
        }

        [HttpPut("systemstate")]
        public async Task<IActionResult> SetSystemState()
        {
            // Access the current HTTP Context
            var context = System.Web.Context.Current;
            var reqVars = Framework.RetrieveRequestVariables(context);

            try
            {
                // Retrieve posted data
                var dataToStore = Request.RetrievePostedValue("data", RegexEnum.None, true, ReturnTypeEnum.Xml);

                var xmlData = new XmlDocument();
                xmlData.LoadXml(dataToStore);

                // Update the system state
                SystemState.FromXml(xmlData);

                var message = new TaxxorReturnMessage(true, "Successfully updated the system state object");

                // Determine the response format based on the request
                switch (reqVars.returnType)
                {
                    case ReturnTypeEnum.Xml:
                        // Set content type and return XML response
                        Response.ContentType = "application/xml";
                        await Response.WriteAsync(message.ToXml().OuterXml);
                        return new EmptyResult(); // Response already sent

                    case ReturnTypeEnum.Json:
                        // Return JSON response
                        return Content(message.ToJson(), "application/json");

                    default:
                        // Handle unsupported format
                        return BadRequest("Data type not supported");
                }
            }
            catch (System.Exception ex)
            {
                return BadRequest($"Error updating system state: {ex.Message}");
            }
        }
    }
}
