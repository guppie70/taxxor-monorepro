using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthenticationMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthenticationMiddleware>();
    }
}

public class AuthenticationMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {

        // Rerieve the request variables
        var reqVars = RetrieveRequestVariables(context);

        if (!reqVars.isStaticAsset && !reqVars.isGrpcRequest)
        {
            var projectVars = RetrieveProjectVariables(context);

            // Disable page http cache
            DisableCache(context);

            // Authentication
            var pageSecurityCheckResult = AuthenticateRequest(context, projectVars.currentUser, reqVars.pageId);
            if (!pageSecurityCheckResult.Success) HandleError(reqVars, pageSecurityCheckResult, (pageSecurityCheckResult.Message == "Not authenticated") ? 403 : 500);


            //
            // => Retrieve user information from the HTTP header
            //
            projectVars.currentUser.FirstName = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserFirstName") ?? "anonymous";
            projectVars.currentUser.LastName = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserLastName") ?? "anonymous";
            projectVars.currentUser.DisplayName = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserDisplayName") ?? "anonymous";
            projectVars.currentUser.Email = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserEmail") ?? "";

            // Console.WriteLine(projectVars.DumpToString());


            // Since this is a service we assume that the authentication and authorization is handled by the Taxxor ARP, Editor or Admin UI
            reqVars.xmlHierarchyStripped.ReplaceContent(reqVars.xmlHierarchy);
        }
        else
        {
            // Configure the static webserver
            if (serveStaticFiles)
            {
                if (sessionConfigured == "yes" && staticFilesRequireAuthentication && string.IsNullOrEmpty(context.Session.GetString("user_id")))
                {
                    if (!StaticFilesBypassAuthentication.Match(reqVars.thisUrlPath).Success)
                    {
                        HandleError(reqVars, "Not allowed", $"You need to be logged in in order to retrieve '{reqVars.thisUrlPath}'", 403);
                    }
                }
            }
            else if (!reqVars.isGrpcRequest)
            {
                // Short circuit the middleware flow if we have been configured not to serve any static files
                return;
            }

        }

        // Proceed with the next middleware component in the pipeline
        await this._next.Invoke(context);

    }
}