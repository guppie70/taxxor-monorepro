using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthenticationStaticAssetsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthenticationStaticAssetsMiddleware>();
    }
}

public class AuthenticationStaticAssetsMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;

    public AuthenticationStaticAssetsMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Rerieve the request and project variables
        var reqVars = RetrieveRequestVariables(context);

        // if (isDevelopmentEnvironment || reqVars.isDebugMode) appLogger.LogInformation("** In Authentication Static Assets Middleware **");

        if (reqVars.isStaticAsset)
        {
            // Configure the static webserver
            if (serveStaticFiles)
            {
                if (staticFilesRequireAuthentication)
                {
                    // Check if we have enough rights to view the static files
                    bool enoughSessionRights = sessionConfigured == "yes" && !string.IsNullOrEmpty(context.Session.GetString("user_id"));

                    // - Check if we have a session cookie
                    // var enoughSessionRights = false;
                    // if (sessionConfigured == "yes")
                    // {
                    //     var nodeSessionCookieName = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='sessioncookies']/name");
                    //     if (nodeSessionCookieName != null)
                    //     {
                    //         var sessionCookieContent = context.Request.Cookies[nodeSessionCookieName.InnerText] ?? "";
                    //         if (!string.IsNullOrEmpty(sessionCookieContent)) enoughSessionRights = true;
                    //     }
                    // }

                    // - Check if we have a valid temporary access token
                    bool validToken = AccessToken.Validate(context.Request);

                    if (enoughSessionRights == false && validToken == false)
                    {
                        // Test if there is no authentication needed for this static asset anyhow
                        bool mayBypassAuthentication = StaticFilesBypassAuthentication.Match(reqVars.thisUrlPath).Success;

                        // Test if this request came from the PDF Service
                        if (!mayBypassAuthentication)
                        {
                            if (IsPdfServiceRequest())
                            {
                                mayBypassAuthentication = true;
                            }
                            else
                            {
                                // appLogger.LogInformation("-------- Static Asset Incoming Request Information --------");
                                // Console.WriteLine(await DebugIncomingRequest(context.Request));
                                // Console.WriteLine("");
                            }
                        }
                        if (!mayBypassAuthentication)
                        {
                            HandleError(reqVars, "Not allowed", $"You need to be logged in in order to retrieve '{reqVars.rawUrl}', enoughSessionRights: {enoughSessionRights}, mayBypassAuthentication: {mayBypassAuthentication}, stack-trace: {GetStackTrace()}", 403);
                        }
                    }

                }
            }
            else
            {
                // Short circuit the middleware flow if we have been configured not to serve any static files
                return;
            }
        }

        // Proceed with the next middleware component in the pipeline
        await this._next.Invoke(context);

    }
}