using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with user preferences and data
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves the data for the current Taxxor user, combines it in one XmlDocument and returns it to the client
        /// </summary>
        /// <returns>The user data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task RetrieveCalendarData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            if (projectVars.currentUser.IsAuthenticated)
            {
                if (CreateUserDataContainer())
                {
                    var pathOs = CalculateFullPathOs("calendar_data");
                    try
                    {
                        var xmlCalendarDataToReturn = new XmlDocument();
                        xmlCalendarDataToReturn.Load(pathOs);
                        await response.OK(xmlCalendarDataToReturn, reqVars.returnType, true);
                    }
                    catch (Exception ex)
                    {
                        HandleError(reqVars, "There was an error retrieving the calendar data", $"pathOs: {pathOs}, error details: {ex}");
                    }

                }
            }
            else
            {
                HandleError(reqVars.returnType, "Not authenticated", $"stack-trace: {CreateStackInfo()}", 403);
            }
        }

        /// <summary>
        /// Updates or creates the user data.
        /// </summary>
        /// <returns>The user data.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public static async Task StoreCalendarData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Retrieve request and project variables
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            string value = request.RetrievePostedValue("xmldata", RegexEnum.None, true, reqVars.returnType);

            // Response document
            var xmlResponse = new XmlDocument();

            if (!CreateUserDataContainer())
            {
                HandleError(reqVars, "Could not create the user data folder", $"projectVars.currentUser.Id: {projectVars.currentUser.Id}, stack-trace: {GetStackTrace()}");
            }

            var xmlCalendarData = new XmlDocument();

            try
            {
                xmlCalendarData.LoadXml(value);
            }
            catch (Exception ex)
            {
                HandleError(reqVars, "Could not process posted user calendar data", $"SetUserData(), Error Details: {ex}");
            }

            //store the data in the user preferences folder
            var pathOs = CalculateFullPathOs("calendar_data");
            var saved = await SaveXmlDocument(xmlCalendarData, pathOs, "Stored calendar data");
            if (!saved)
            {
                HandleError("There was an error saving the calendar data in the user data folder", $"pathOs: {pathOs}, stack-trace: {GetStackTrace()}");
            }
            else
            {
                xmlResponse = GenerateSuccessXml($"Sucessfully stored calendar data", $"path: {pathOs}");
            }

            await response.OK(xmlResponse, reqVars.returnType, true);
        }

    }
}