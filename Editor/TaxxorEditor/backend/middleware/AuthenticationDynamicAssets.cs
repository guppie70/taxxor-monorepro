using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthenticationDynamicAssetsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthenticationDynamicAssetsMiddleware>();
    }
}

public class AuthenticationDynamicAssetsMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;

    public AuthenticationDynamicAssetsMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Rerieve the request variables
        var reqVars = RetrieveRequestVariables(context);


        // if (isDevelopmentEnvironment || reqVars.isDebugMode) appLogger.LogInformation("** In Authentication Middleware **");
        if (!reqVars.isStaticAsset)
        {
            // Rerieve project variables
            var projectVars = RetrieveProjectVariables(context);

            // Security
            if (reqVars.pageId != "ulogout-dotnet" && reqVars.pageId != "ulogout-dotnet-final")
            {
                // Authenticate this request
                var authenticateRequestResult = AuthenticateRequest(reqVars, projectVars, reqVars.pageId, "AuthenticationDynamicAssets.cs");
                if (!authenticateRequestResult.Success)
                {
                    HandleError(reqVars, authenticateRequestResult, (authenticateRequestResult.Message == "Not authenticated") ? 403 : 500);
                }

                // If we have an authenticated user then add the user to the .NET Identity system so that we can use it in websocket SignalR framework
                if (projectVars.currentUser.IsAuthenticated)
                {
                    var claims = new List<Claim>();
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, projectVars.currentUser.Id));
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
                    System.Threading.Thread.CurrentPrincipal = context.User;

                    // Setup a new rbac cache utility class
                    projectVars.rbacCache = new RbacCache(projectVars.currentUser.Id, projectVars.projectId);
                }

                // Authorize this request
                var authorizeRequestResult = await AuthorizeRequest(reqVars, projectVars, reqVars.pageId, "AuthenticationDynamicAssets.cs");
                if (!authorizeRequestResult.Success)
                {
                    HandleError(reqVars, authorizeRequestResult, 403);
                }

                if (!projectVars.isInternalServicePage)
                {
                    // Generate the stripped hierarchy on which the navigation elements of the TaxxorEditor application are based
                    var xmlHierarchyStripped = await GenerateStrippedRbacHierarchy("taxxoreditor", reqVars.xmlHierarchy, reqVars, projectVars, true);

                    // Make sure that all the items are removed from the hierarchy
                    RemoveMarkedItemsFromHierarchy(ref xmlHierarchyStripped);

                    reqVars.xmlHierarchyStripped.ReplaceContent(xmlHierarchyStripped);
                }
            }
            else
            {
                reqVars.xmlHierarchyStripped.ReplaceContent(reqVars.xmlHierarchy);
            }

            // if (projectVars.currentUser.Id == systemUser)
            // {
            //     appLogger.LogInformation("-------------------------------- INTERNAL REQUEST --------------------------------");
            //     appLogger.LogInformation($"projectVars.currentUser.Id: {projectVars.currentUser.Id}");
            //     appLogger.LogInformation($"projectVars.currentUser.IsAuthenticated: {projectVars.currentUser.IsAuthenticated}");
            //     appLogger.LogInformation("----------------------------------------------------------------------------------");
            // }


            // Dump some debugging information on the disk
            if (isDevelopmentEnvironment || reqVars.isDebugMode)
            {
                // Dump internal documents so that they can be inspected     
                try
                {
                    // Log something
                    // appLogger.LogDebug($"Dumping site-structure-stripped, reqVars.thisUrlPath: {reqVars.thisUrlPath}, stack-trace: {GetStackTrace()}");

                    // Dump the files
                    if (reqVars.thisUrlPath.EndsWith(".html")) await TextFileCreateAsync(PrettyPrintXml(reqVars.xmlHierarchyStripped.OuterXml), $"{logRootPathOs}/inspector/site-structure-stripped.xml");
                }
                catch (Exception ex)
                {
                    WriteErrorMessageToConsole("Something went wrong in storing the inspector data", ex.ToString());
                }
            }

        }

        // Proceed with the next middleware component in the pipeline
        await _next(context);

    }
}