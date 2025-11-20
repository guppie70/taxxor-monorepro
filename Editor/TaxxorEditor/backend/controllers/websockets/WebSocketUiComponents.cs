using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
	public abstract partial class ProjectLogic : Framework
	{

		/// <summary>
		/// Websocket methods that render UI components dynamically
		/// </summary>
		public partial class WebSocketsHub : Hub
		{

			/// <summary>
			/// Retrieves reporting periods
			/// </summary>
			/// <param name="jsonData"></param>
			/// <returns></returns>
			[AuthorizeAttribute(VirtualPath = "/hub/methods/uicomponents/selectreportingperiods")]
			public async Task<TaxxorReturnMessage> RenderReportingPeriodDropdown(string? projectId = null, string? selectName = null, string? className = null, bool shortList = true, bool includeNone = false, bool sortAscending = true, bool renderNotApplicable = true, string? referencePeriod = null, string? reportTypeId = null)
			{
				try
				{
					var context = System.Web.Context.Current;
					RequestVariables reqVars = RetrieveRequestVariables(context);
					ProjectVariables projectVars = RetrieveProjectVariables(context);

					var debugRoutine = siteType == "local" || siteType == "dev";

					//
					// => Handle security
					//
					var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
					if (!securityCheckResult.Success)
					{
						appLogger.LogError(securityCheckResult.ToString());
						return securityCheckResult;
					}

					//
					// => Input data validation for string values
					//
					var inputValidationCollection = new InputValidationCollection();
					if (projectId != null) inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", false);
					if (reportTypeId != null) inputValidationCollection.Add("reportTypeId", reportTypeId, @"^[a-zA-Z0-9\-_]{2,128}$", false);
					if (selectName != null) inputValidationCollection.Add("selectName", selectName, @"^[a-zA-Z0-9\-_]{2,128}$", true);
					if (className != null) inputValidationCollection.Add("className", className, @"^[a-zA-Z0-9\-_\s]{2,128}$", false);
					if (referencePeriod == "none" || referencePeriod == "") referencePeriod = null;
					if (referencePeriod != null) inputValidationCollection.Add("referencePeriod", referencePeriod, @"^(m|q|a)(r|(\d){1,2})(\d\d)$", false);

					var validationResult = inputValidationCollection.Validate();
					if (!validationResult.Success)
					{
						appLogger.LogError(validationResult.ToString());
						return validationResult;
					}

					//
					// => Render the dropdown box (method logic)
					//

					var htmlSelectBox = RenderReportingPeriodSelect(selectName, className, shortList, sortAscending, renderNotApplicable, referencePeriod, reportTypeId);
					var returnData = new TaxxorReturnMessage(true, "Seccessfully rendered dropdown select list", htmlSelectBox, selectName);

					return returnData;

				}
				catch (Exception ex)
				{
					var errorMessage = "There was an error generating the period dropdown select box";
					appLogger.LogError(ex, errorMessage);
					return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
				}
			}


		}

	}

}