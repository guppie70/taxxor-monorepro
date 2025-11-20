using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
	public abstract partial class ProjectLogic : Framework
	{

		/// <summary>
		/// Retrieves information about the connected Taxxor Services and returns the result to the client
		/// </summary>
		/// <returns>The xml configuration.</returns>
		/// <param name="request">Request.</param>
		/// <param name="response">Response.</param>
		/// <param name="routeData">Route data.</param>
		// MIGRATED - CAN BE REMOVED
		public async static Task RetrieveTaxxorServicesConfiguration(HttpRequest request, HttpResponse response, RouteData routeData)
		{
			var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            
            // Call the core logic method
            var result = await RetrieveTaxxorServicesConfigurationCore();
            
            // Return the response
            if (result.Success)
            {
                await response.OK(result.XmlResponse, ReturnTypeEnum.Xml, true);
            }
            else
            {
                HandleError(ReturnTypeEnum.Xml, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
		}
		
		/// <summary>
		/// Core logic for retrieving Taxxor services configuration without HTTP dependencies
		/// </summary>
		/// <returns>Configuration result</returns>
		public async static Task<ConfigurationResult> RetrieveTaxxorServicesConfigurationCore()
		{
		    var result = new ConfigurationResult();
		    
		    try
		    {
		        var xPath = "/configuration/taxxor/components";
		        XmlNode? taxxorServicesNode = xmlApplicationConfiguration.SelectSingleNode(xPath);
		        
		        if (taxxorServicesNode == null)
		        {
		            result.Success = false;
		            result.ErrorMessage = "Could not find Taxxor Services information";
		            result.DebugInfo = $"xPath: '{xPath}' returned no results";
		            return result;
		        }
		        
		        // Get the content to return
		        result.Success = true;
		        // Create an XmlDocument to hold the response
		        var xmlDoc = new XmlDocument();
		        xmlDoc.LoadXml(taxxorServicesNode.OuterXml);
		        result.XmlResponse = xmlDoc;
		        
		        await Task.CompletedTask; // To satisfy the async requirement
		        return result;
		    }
		    catch (Exception ex)
		    {
		        result.Success = false;
		        result.ErrorMessage = "Error retrieving Taxxor services configuration";
		        result.DebugInfo = $"Error: {ex.Message}, Stack trace: {GetStackTrace()}";
		        return result;
		    }
		}
		
	}
}