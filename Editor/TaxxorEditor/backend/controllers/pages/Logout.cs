using System;
using System.Text;
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
        /// Kills the current sessions to force a logout of the current user
        /// </summary>
        /// <returns>The session.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task RenderLogoutPage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var debugRoutine = (siteType == "local" || siteType == "dev");

            var userIdFromSession = context.Session.GetString("user_id") ?? "unknown";



            try
            {
                // Clear simultaneous sessions for this user
                RemoveSimultaneousSessionsForUser(context);
            }
            catch (Exception ex)
            {
                HandleError(reqVars, "Could not remove simultaneous sessions", $"error: {ex}");
            }


            // Reload the page if we have not been called with the logout=true parameter
            var logoutParameter = request.RetrievePostedValue("logout", RegexEnum.Boolean, false, ReturnTypeEnum.Html, "false");
            if (logoutParameter == "false")
            {
                // Clear the rbac cache for the current user
                if (userIdFromSession != "unknown")
                {
                    var rbacCache = new RbacCache();
                    rbacCache.ClearUserCache(userIdFromSession);
                }
                else
                {
                    if (debugRoutine) appLogger.LogWarning("Unable to remove the RBAC cache for this user as we were unable to retrieve the user ID from the session");
                }

                // Remove SSO cookie
                var ssoCookieRemoval = Extensions.RemoveSSOCookie(context);

                // Redirect to the same page, but this time render some HTML
                response.Redirect($"{reqVars.thisUrlPath}?logout=true");
            }
            else
            {
                // Clear any previously stored session and cookie data
                RemoveSessionCompletely(context);

                // Render HTML
                var htmlContent = await _renderRedirectHtml(reqVars.pageTitle, "/");

                await response.OK(htmlContent, ReturnTypeEnum.Html, true);
            }
        }

        /// <summary>
        /// Second route for removing "everything" completely
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RenderLogoutPageFinal(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);


            try
            {
                // Clear any previously stored session and cookie data
                RemoveSessionCompletely(context);

                // Render HTML
                var htmlContent = await _renderRedirectHtml(reqVars.pageTitle, "/");

                await response.OK(htmlContent, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError(reqVars, "There was an error rendering the content for the redirect page", $"error: {ex}");
            }
        }

        /// <summary>
        /// Renders the HTML that should take care of the redirect
        /// </summary>
        /// <param name="pageTitle"></param>
        /// <param name="sessionCookieName"></param>
        /// <param name="redirectUri"></param>
        /// <param name="ssoCookieRemove"></param>
        /// <returns></returns>
        private static async Task<string> _renderRedirectHtml(string pageTitle, string redirectUri, string ssoCookieRemove = null)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            var redirectToArpLogout = true;
            var jsCookieDelete = new StringBuilder();
            var nodeListCookies = xmlApplicationConfiguration.SelectNodes("/configuration/general/settings/setting[@id='sessioncookies']/name");
            foreach (XmlNode nodeCookieProperties in nodeListCookies)
            {
                var cookieName = nodeCookieProperties.InnerText;
                if (!string.IsNullOrEmpty(cookieName))
                {
                    jsCookieDelete.AppendLine($"document.cookie = '{cookieName}=; Path=/; expires=Thu, 01 Jan 1970 00:00:01 GMT;';");

                    // We should not redirect to the ARP logout page if we are running behind a Shibboleth based proxy
                    if (redirectToArpLogout && cookieName == "_shibsession_proxy") redirectToArpLogout = false;
                }
            }

            if (!string.IsNullOrEmpty(ssoCookieRemove))
            {
                jsCookieDelete.AppendLine($"document.cookie = '{ssoCookieRemove}';");
            }


            //
            // => Redirect URL
            //
            var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{siteType}']");
            var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
            var protocol = (nodeCurrentDomain.GetAttribute("ssl") == "true") ? "https" : "http";
            var fullDomainName = $"{protocol}://{currentDomainName}";
            var metaRefresh = redirectToArpLogout ? $"<meta http-equiv=\"refresh\" content=\"0; url={fullDomainName}/authentication/logout\">" : "";


            // Retrieve the content of the stylesheet, because we can't retrieve it anymore through the ARP
            var cssContent = await RetrieveTextFile($"{GetServiceUrl(ConnectedServiceEnum.StaticAssets)}/static/stylesheets/main.css");


            return $@"
                    <html>
                        <head>
                            <title>{pageTitle}</title>
                            {metaRefresh}
                            <style>
                            {cssContent}
                            body{{
                                margin: 10px;
                            }}            
                            </style>
                        </head>
                        <body>
                            <h2>You have been logged out</h2>
                            <p>Your session was terminated, please use the link below to login again</p>
                            <div id='logoutlink'></div>
                            <script type='text/javascript'>
                                // Assure that the session cookie is completely removed
                                {jsCookieDelete.ToString()}
                                var logoutUri = '{redirectUri}?rnd=' + Math.random();
                                document.getElementById('logoutlink').innerHTML=""<a href='""+logoutUri+""'>Login</a>""
                            </script>                     
                        </body>
                    </html>";
        }

    }
}