using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
using UAParser;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Attempts to detect multiple sessions of the same user from a different browser
        /// </summary>
        /// <param name="context"></param>
        /// <param name="requestVariables"></param>
        /// <param name="projectVars"></param>
        public static void CheckSimultaneousSession(HttpContext context, RequestVariables reqVars, ProjectVariables projectVars)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            debugRoutine = false;

            //
            // => Only perform the simultaneous session check on "real" web pages
            //
            if (!isApiRoute(context) && !projectVars.isInternalServicePage && projectVars.currentUser.Id != SystemUser && reqVars.pageId != "ulogout-dotnet" && reqVars.pageId != "ulogout-dotnet-final")
            {
                //
                // => Test if double session detection is enabled
                //
                var nodeSimultaneousSessionCheck = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/settings/setting[@id='double_session_detection' and domain/@type='{siteType}']");
                if (nodeSimultaneousSessionCheck != null && projectVars.currentUser.IsAuthenticated)
                {
                    if (debugRoutine)
                    {
                        Console.WriteLine("---------");
                        Console.WriteLine("Check for simultaneous sessions");

                    }

                    //
                    // => Retrieve double session detection metadata
                    //
                    int maxMinutes = Int32.Parse(nodeSimultaneousSessionCheck.GetAttribute("timeout") ?? "25");
                    if (debugRoutine) Console.WriteLine($"- maxMinutes: {maxMinutes}");

                    //
                    // => Current date
                    //
                    var currentDate = DateTime.Now;

                    //
                    // => Retrieve current user information (ID, User Agent, Remote IP)
                    //
                    var userId = projectVars.currentUser.Id;
                    var userIp = reqVars.ipAddress;
                    var browserUserAgent = context.Request.Headers["User-Agent"].ToString();


                    //
                    // => Render a simultaneous session key
                    //
                    var uniqueUserKey = _generateSimultaneousSessionKey(userId, userIp, browserUserAgent);
                    context.Session.SetString("simultaneous_sessionkey", uniqueUserKey);

                    //
                    // => Loop through the existing simultaneous session object and remove old keys
                    //
                    var keysToRemove = new List<string>();
                    foreach (var keyValuePair in UserBrowserSessions)
                    {
                        var userBrowserSession = keyValuePair.Value;

                        // Check the date difference and if the session is older than configured, remove the session from the list
                        TimeSpan ts = currentDate - userBrowserSession.Created;
                        if (ts.TotalMinutes > maxMinutes)
                        {
                            keysToRemove.Add(keyValuePair.Key);
                        }
                    }
                    foreach (string keyToRemove in keysToRemove)
                    {
                        UserBrowserSessions.Remove(keyToRemove);
                    }

                    //
                    // => Detect if the user has another session simultaneously running in the Taxxor editor - if so, throw an error
                    //
                    var otherUserBrowserSessionInfo = HasUserAnotherRunningSession(userId, browserUserAgent);
                    if (otherUserBrowserSessionInfo.HasAnotherSession)
                    {
                        var otherSessionInfo = otherUserBrowserSessionInfo.OtherSessionInfo;

                        var browserMessage = new StringBuilder();
                        browserMessage.AppendLine($"<h2>Simultaneous session detected...</h2>");
                        browserMessage.AppendLine($"<p>Another web browser using your user ID ({otherSessionInfo.UserId}) is currently being used to access the Taxxor application.</p>");
                        browserMessage.AppendLine($"<p>For security reasons only one active browser session is allowed at the same time.<br/>");
                        browserMessage.AppendLine($"Please logout in the other browser session first and then try to access Taxxor with this browser again.</p>");
                        browserMessage.AppendLine($"<p>Details of the other browser session are:</p>");
                        browserMessage.AppendLine($"<ul>");
                        browserMessage.AppendLine($"<li>Browser family: {otherSessionInfo.UserAgentParsed.UA.Family}</li>");
                        browserMessage.AppendLine($"<li>Operating system: {otherSessionInfo.UserAgentParsed.OS.Family}</li>");
                        browserMessage.AppendLine($"<li>Device: {otherSessionInfo.UserAgentParsed.Device.Family}</li>");
                        browserMessage.AppendLine($"</ul>");
                        browserMessage.AppendLine($"<p>Please contact the system administrator if you do not recognize the details listed above and you need support to fix this problem.</p>");
                        browserMessage.AppendLine("<style>button, span{display:none}</style>");
                        // We have detected a simultaneous session, now we need to throw an exception to stop any further processing
                        HandleError(browserMessage.ToString(), $"More details: {otherSessionInfo.ConvertToString()}", 406);
                    }
                    else
                    {
                        //
                        // => Add a new user info object in the UserBrowserSessions object or update the created time stamp
                        //                        
                        if (UserBrowserSessions.ContainsKey(uniqueUserKey))
                        {
                            UserBrowserSessions[uniqueUserKey].Created = DateTime.Now;
                        }
                        else
                        {
                            UserBrowserSessions.Add(uniqueUserKey, new UserBrowserSessionInfo(userId, userIp, browserUserAgent));
                        }
                    }


                    //
                    // => Debug some information
                    //
                    if (debugRoutine)
                    {
                        Console.WriteLine($"- UserBrowserSessions.Count: {UserBrowserSessions.Count}");
                        Console.WriteLine("---------");
                    }

                }
            }



        }


        /// <summary>
        /// Generates a unique key to use for this user in the simultaneous session object
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userIp"></param>
        /// <param name="browserUserAgent"></param>
        /// <returns></returns>
        private static string _generateSimultaneousSessionKey(string userId, string userIp, string browserUserAgent)
        {
            return md5($"-{userId}-{userIp}-{browserUserAgent}-");
        }

        /// <summary>
        /// Removes all the user browser session elements for the current user using the key stored in the session
        /// </summary>
        /// <param name="context"></param>
        public static void RemoveSimultaneousSessionsForUser(HttpContext context)
        {
            var userBrowserSessionKey = RetrieveSimultaneousSessionKey(context);
            if (!string.IsNullOrEmpty(userBrowserSessionKey))
            {
                RemoveSimultaneousSessionsForUser(userBrowserSessionKey);
            }
        }

        /// <summary>
        /// Removes all the user browser session elements for the current user
        /// </summary>
        /// <param name="identifier">User ID or simultaneous session key</param>
        public static void RemoveSimultaneousSessionsForUser(string identifier)
        {
            var userId = "";
            if (!identifier.Contains("@"))
            {
                // Try to retrieve the user ID by finding the UserBrowserSessionInfo object in the list
                foreach (var userBrowserSession in UserBrowserSessions)
                {
                    if (userBrowserSession.Key == identifier)
                    {
                        userId = userBrowserSession.Value.UserId;
                    }
                }
            }
            else
            {
                userId = identifier;
            }


            var keysToRemove = new List<string>();
            foreach (var keyValuePair in UserBrowserSessions)
            {
                var userBrowserSession = keyValuePair.Value;
                if (userBrowserSession.UserId == userId)
                {
                    keysToRemove.Add(keyValuePair.Key);
                }
            }
            foreach (string keyToRemove in keysToRemove)
            {
                UserBrowserSessions.Remove(keyToRemove);
            }
        }



        /// <summary>
        /// Retrieves the simultaneous session key that can be used to lookup browser user session information
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string RetrieveSimultaneousSessionKey(HttpContext context)
        {
            if (!string.IsNullOrEmpty(context.Session.GetString("simultaneous_sessionkey")))
            {
                return context.Session.GetString("simultaneous_sessionkey");
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// Tests if the user already has another session running
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userIp"></param>
        /// <param name="browserUserAgent"></param>
        /// <returns></returns>
        public static(bool HasAnotherSession, UserBrowserSessionInfo OtherSessionInfo) HasUserAnotherRunningSession(string userId, string browserUserAgent)
        {
            using(Dictionary<string, UserBrowserSessionInfo>.Enumerator userBrowserSessionsEnum = UserBrowserSessions.GetEnumerator())
            {
                for (int i = 0; i < UserBrowserSessions.Count; i++)
                {
                    userBrowserSessionsEnum.MoveNext();

                    var userBrowserSessionInfo = userBrowserSessionsEnum.Current.Value;
                    if (userBrowserSessionInfo.UserId == userId && userBrowserSessionInfo.UserAgent != browserUserAgent)
                    {
                        return (HasAnotherSession: true, OtherSessionInfo: userBrowserSessionInfo);
                    }
                }
                return (HasAnotherSession: false, OtherSessionInfo: null);
            }
        }

        /// <summary>
        /// Represents a user session with a browser
        /// </summary>
        public class UserBrowserSessionInfo
        {


            // Generic properties for the user class
            public string UserId { get; set; }
            public string UserIP { get; set; }
            public string UserAgent { get; set; }
            public ClientInfo UserAgentParsed { get; set; }

            public DateTime Created { get; set; }

            public UserBrowserSessionInfo(string userId, string userIp, string userAgent)
            {
                this.UserId = userId;
                this.UserIP = userIp;
                this.UserAgent = userAgent;

                // Parse the user agent string
                var uaParser = Parser.GetDefault();
                this.UserAgentParsed = uaParser.Parse(userAgent);

                // Stick the current date in the Created property
                this.Created = DateTime.Now;
            }

            public string ConvertToString()
            {
                return $"UserId: {this.UserId}, UserIP: {this.UserIP}, UserAgent: {this.UserAgent}, Created: {this.Created.ToString()}";
            }
        }

    }


}