using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    /// <summary>
    /// Generic utilities and tools commonly used in the web pages of this site/application
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

		/// <summary>
		/// Writes data to the web client in JSON format
		/// </summary>
        public static async Task WriteCmsJsonData(HttpRequest request, HttpResponse response, RouteData routeData)
		{
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            string json = string.Empty;
			var retrieve = request.RetrievePostedValue("retrieve");

			if (retrieve == "currentyear")
			{
                json = "{\"year\": \"" + projectVars.reportingPeriod.ToString()+"\"}";
			}

            await response.OK(json, ReturnTypeEnum.Json, true);
		}

	}
}