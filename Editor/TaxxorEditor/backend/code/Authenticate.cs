using Microsoft.AspNetCore.Http;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Authenticates an incoming HTTP request
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="pageId"></param>
        /// <param name="originatingRoutine"></param>
        /// <returns></returns>
        public static TaxxorReturnMessage AuthenticateRequest(RequestVariables reqVars, ProjectVariables projectVars, string pageId, string originatingRoutine)
        {
            // Global variables
            HttpContext? context = System.Web.Context.Current;
            AppUserTaxxor? user = projectVars.currentUser;
            string? userIdFromSession = context?.Session?.GetString("user_id");

            // 
            // => Handle internal API HTTP traffic
            // 
            if (BypassSecurity)
            {
                // 
                // => Bypass the security using an admin user id
                // 
                user.Id = "johan@taxxor.com";
                user.FirstName = "** Johan **";
                user.LastName = "** Taxxor **";
                user.DisplayName = "** Bypass, User **";
                user.Email = user.Id;

                user.IsAuthenticated = true;
                user.Permissions.View = true;

                // Store the user details in the session
                context.Session.SetString("user_id", user.Id);
                context.Session.SetString("user_firstname", user.FirstName);
                context.Session.SetString("user_lastname", user.LastName);
                context.Session.SetString("user_displayname", user.DisplayName);
                context.Session.SetString("user_email", user.Email);
                context.Session.SetString("user_guid", user.Email);

                projectVars.sessionCreated = true;

                // Assure that the session cannot be stolen and used within another browser
                SetSessionFixation();

                // generate token to prevent xss forgery
                SetCrossSiteRequestForgery();
            }
            else if (BypassSecurity || (projectVars.isInternalServicePage && string.IsNullOrEmpty(userIdFromSession)))
            {
                // For when Prince XML is requesting assets from this application (images, etc.)
                var pdfServiceRequest = IsPdfServiceRequest();

                if (pdfServiceRequest || projectVars.isInternalServicePage)
                {
                    user.Id = SystemUser;
                }

                // TODO: consider adding a special system user for making the internal call
                if (!string.IsNullOrEmpty(projectVars.userIdFromHeader))
                {
                    // Assume that the user is authenticated
                    user.IsAuthenticated = true;

                    // Assign the user information from the headers to the user object
                    user.Id = projectVars.userIdFromHeader;

                    // Retrieve other information from the HTTP headers
                    user.FirstName = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserFirstName") ?? "User";
                    user.LastName = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserLastName") ?? "System";
                    user.DisplayName = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserDisplayName") ?? "System, User";
                    user.Email = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserEmail") ?? "";
                }

                // TODO: check if internal system calls have the user set in the header -> if so, the below can be removed
                user.IsAuthenticated = true;
                user.Permissions.View = true;
            }
            else
            {

                if (!string.IsNullOrEmpty(userIdFromSession))
                {
                    //
                    // => Session exists - use it to authenticate and fill user information
                    //                   

                    /*
                    When we have a session, then use that information
                     */
                    user.IsAuthenticated = true;
                    user.Id = userIdFromSession.ToLower();

                    user.FirstName = context.Session.GetString("user_firstname") ?? "anonymous";
                    user.LastName = context.Session.GetString("user_lastname") ?? "anonymous";
                    user.DisplayName = context.Session.GetString("user_displayname") ?? "anonymous";
                    if (!string.IsNullOrEmpty(context.Session.GetString("user_email"))) user.Email = context.Session.GetString("user_email");
                }
                else
                {
                    //
                    // => Session does not exist - retrieve the user information from the HTTP headers passed to this request
                    //
                    string? userId = context?.Request?.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserId");

                    // Use an alternative user id for simulating another user
                    string? forcedUserId = context?.Session?.GetString("forceduserid");
                    if (!string.IsNullOrEmpty(forcedUserId)) userId = forcedUserId;


                    if (!string.IsNullOrEmpty(userId))
                    {
                        // Always use the lower case version of the user ID
                        userId = userId.ToLower();

                        // Assume that the user is authenticated
                        user.IsAuthenticated = true;

                        // Assign the user information from the headers to the user object
                        user.Id = userId;

                        // Retrieve other information from the HTTP headers
                        user.FirstName = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserFirstName") ?? "unknown";
                        user.LastName = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserLastName") ?? "unknown";
                        user.DisplayName = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserDisplayName") ?? "unknown";
                        user.Email = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserEmail") ?? "";

                        // Correct "null" values
                        if (user.FirstName.Contains("(null)")) user.FirstName = "unknown";
                        if (user.LastName.Contains("(null)")) user.LastName = "unknown";
                        if (user.DisplayName.Contains("(null)")) user.DisplayName = "unknown";
                        if (user.Email.Contains("(null)")) user.Email = "unknown";

                        // Attempt to parse the user's first and last name from the user ID (assuming that the user ID is the user's email address)
                        if (user.FirstName == "unknown" && user.LastName == "unknown" && user.DisplayName == "unknown") user.UserNameFromUserId(userId);

                        //
                        // => Set the session
                        //
                        if (string.IsNullOrEmpty(userIdFromSession))
                        {
                            // Store the user details in the session
                            context.Session.SetString("user_id", userId);
                            context.Session.SetString("user_firstname", user.FirstName);
                            context.Session.SetString("user_lastname", user.LastName);
                            context.Session.SetString("user_displayname", user.DisplayName);
                            context.Session.SetString("user_email", user.Email);
                            context.Session.SetString("user_guid", user.Email);

                            projectVars.sessionCreated = true;

                            // Assure that the session cannot be stolen and used within another browser
                            SetSessionFixation();

                            // generate token to prevent xss forgery
                            SetCrossSiteRequestForgery();
                        }

                    }
                }
            }


            // Every request to the Taxxor Editor needs to be with an authenticated user
            if (!user.IsAuthenticated)
            {
                var debugInfo = $"pageId: {(pageId ?? "unknown")}\noriginatingRoutine: {originatingRoutine}";
                if (!string.IsNullOrEmpty(pageId) && pageId != "websocketshub") debugInfo += $"\n\n{RetrieveAllHttpRequestHeaderValues(context?.Request, ReturnTypeEnum.Txt)}";
                return new TaxxorReturnMessage(false, "Not authenticated", debugInfo);
            }

            // Return success message
            return new TaxxorReturnMessage(true, "Successfully authenticated this request");
        }


    }
}