using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with user preferences and data
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves the data for the current Taxxor user (user preferences, reporting requirements, etc.) from the Taxxor Data Store
        /// </summary>
        /// <param name="retrieveTaxxorInfo">Optionally retrieves data from Lextra Regulation DB</param>
        public static async Task RetrieveUserData(bool retrieveTaxxorInfo = false)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var profilePerformance = false;
            var xPath = "";
            var sessionKey = "user_xmldata";
            var taxxorUserDataSource = $"session('{sessionKey}')";
            var debugRoutine = false;

            XmlDocument? xmlAllUserData = new XmlDocument();

            if (profilePerformance) ProfilerStart("Start retrieving user data", true, ReturnTypeEnum.Txt);
            if (projectVars.currentUser.IsAuthenticated)
            {

                //
                // A) Retrieve the complete raw user data as an XML document
                //
                if (string.IsNullOrEmpty(context.Session.GetString(sessionKey)))
                {
                    if (debugRoutine)
                    {
                        Console.WriteLine("############### RETREIVING FRESH USER DATA ###############");
                    }
                    taxxorUserDataSource = GetServiceUrlByMethodId(ConnectedServiceEnum.DocumentStore, "taxxoreditoruserdata");

                    CustomHttpHeaders customHttpHeaders = new CustomHttpHeaders();
                    customHttpHeaders.AddTaxxorUserInformation(projectVars.currentUser);

                    customHttpHeaders.RequestType = ReturnTypeEnum.Xml;

                    if (UriLogEnabled)
                    {
                        if (!UriLogBackend.Contains(taxxorUserDataSource)) UriLogBackend.Add(taxxorUserDataSource);
                    }

                    xmlAllUserData = await RestRequest<XmlDocument>(RequestMethodEnum.Get, taxxorUserDataSource, customHttpHeaders, true);

                    // Only set a session with valid user data
                    if (!XmlContainsError(xmlAllUserData))
                    {
                        // Set the session so that the next time we need the data again it will be very efficient to retrieve
                        context.Session.SetString(sessionKey, xmlAllUserData.OuterXml);
                    }
                    else
                    {
                        appLogger.LogWarning($"There was an error retrieving the user data. xml: {xmlAllUserData.OuterXml}, stack-trace: {GetStackTrace()}");
                    }

                }
                else
                {
                    if (debugRoutine)
                    {
                        Console.WriteLine("############### USING SESSION USER DATA ###############");
                    }
                    try
                    {
                        xmlAllUserData.LoadXml(context.Session.GetString(sessionKey));
                    }
                    catch (Exception ex)
                    {
                        xmlAllUserData = GenerateErrorXml("Could not load user details data from session", ex.ToString());
                    }
                }
                if (profilePerformance) ProfilerMeasure("After load user preferences", ReturnTypeEnum.Txt);

                //
                // B) Parse the recieved data in the user object
                //
                if (XmlContainsError(xmlAllUserData))
                {
                    WriteErrorMessageToConsole(xmlAllUserData.SelectSingleNode("/error/message").InnerText, xmlAllUserData.SelectSingleNode("/error/debuginfo").InnerText);
                }
                else
                {
                    // Load the user preferences
                    xPath = "/userdata/preferences";
                    var userPreferenceNode = xmlAllUserData.SelectSingleNode(xPath);
                    if (userPreferenceNode == null)
                    {
                        appLogger.LogWarning($"Could not find the user preferences loaded from '{taxxorUserDataSource}' using xpath: '{xPath}'");
                    }
                    else
                    {
                        if (userPreferenceNode.OuterXml.Contains("<"))
                        {
                            projectVars.currentUser.XmlUserPreferences.ReplaceContent(userPreferenceNode.FirstChild);
                        }
                        else
                        {
                            appLogger.LogWarning($"Could not find the user preferences loaded from '{taxxorUserDataSource}' using xpath: '{xPath}' because it does not contain XML data");
                        }
                    }
                }

            }

            if (profilePerformance) ProfilerStop("End retrieving user data", ReturnTypeEnum.Txt);

            if ((siteType == "local" || siteType == "dev") && !isApiRoute() && profilePerformance) await TextFileCreateAsync(reqVars.profilingResult.ToString(), logRootPathOs + "/profiling/user-data_" + reqVars.pageId + ".txt");
        }

    }
}