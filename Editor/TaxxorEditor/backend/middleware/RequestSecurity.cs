using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestSecurityMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestSecurityMiddleware>();
    }
}



public class RequestSecurityMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;


    public RequestSecurityMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Rerieve the request variables
        var reqVars = RetrieveRequestVariables(context);

        if (!reqVars.isStaticAsset)
        {
            if (reqVars.pageId != "ulogout-dotnet" && reqVars.pageId != "ulogout-dotnet-final")
            {
                // Retrieve project variables
                var projectVars = RetrieveProjectVariables(context);

                // Disable page http cache
                DisableCache(context);

                // Handle session fixation (possible security issue)
                if (reqVars.protocol == "https")
                {
                    var sessionCheckResult = CheckSessionFixation(context);
                    if (!sessionCheckResult.ValidSession)
                    {
                        RemoveSessionCompletely(context);

                        HandleError(reqVars, "Unauthorized access.<br/><br/>You do not have enough permissions rights to view this page.<br/>Please contact your system administrator if you need access.", $"sessionCheckResult.DebugInformation: {sessionCheckResult.DebugInformation}, userId: {projectVars.currentUser.Id}, pageId: {reqVars.pageId}");
                    }
                }

                // Check the cross site request token for all the API routes that are posted to the server
                if (EnableCrossSiteRequestForgery)
                {
                    if (
                        isApiRoute(context) &&
                        !projectVars.isInternalServicePage &&
                        projectVars.currentUser.Id != SystemUser &&
                        reqVars.pageId != "grpcprojects" &&
                        reqVars.pageId != "listprojects" &&
                        reqVars.pageId != "filingeditorlock" &&
                        reqVars.pageId != "filingeditorlistlocks" &&
                        reqVars.pageId != "websocketshub" &&
                        reqVars.pageId != "websocketshubnegotiate" &&
                        reqVars.pageId != "plugin-edgarfiler" &&
                        reqVars.pageId != "plugin-edgarfilerxhr" &&
                        reqVars.pageId != "proxystructureddatastore" &&
                        reqVars.pageId != "proxymappingservice" &&
                        reqVars.pageId != "cms_load-cache-files" &&
                        reqVars.pageId != "filemanagerapi" &&
                        reqVars.pageId != "filemanagerinfoapi" &&
                        reqVars.pageId != "blazorfilemanager" &&
                        reqVars.pageId != "javascriptfilemanager" &&
                        reqVars.pageId != "datamanagerpage"
                    )
                    {
                        var tokenTestResult = CheckCrossSiteRequestForgeryToken(context);

                        if (!tokenTestResult.ValidToken)
                        {
                            HandleError(reqVars, "Unauthorized access.<br/><br/>You do not have enough permissions to view this page.<br/>Please contact your system administrator if you need access.", $"tokenTestResult.Message: {tokenTestResult.Message}, tokenTestResult.DebugInformation: {tokenTestResult.DebugInformation}, userId: {projectVars.currentUser.Id}, pageId: {reqVars.pageId}");
                        }
                    }
                }

                //
                // => Double session detection
                //
                CheckSimultaneousSession(context, reqVars, projectVars);

                //
                // => Disable specific actions when the project is locked
                //
                if (projectVars.projectStatus?.ToLower() == "closed")
                {
                    if (
                        (reqVars.pageId == "taxxoreditorcomposerdata" && reqVars.method == RequestMethodEnum.Post) ||
                        (reqVars.pageId == "hierarchymanagerhierarchy" && reqVars.method == RequestMethodEnum.Post) ||
                        (reqVars.pageId == "hierarchymanagerhierarchyitem" && reqVars.method == RequestMethodEnum.Post) ||
                        (reqVars.pageId == "versionmanager" && reqVars.method == RequestMethodEnum.Put) ||
                        (reqVars.pageId == "syncexternaltables" && reqVars.method == RequestMethodEnum.Get) ||
                        (reqVars.pageId == "syncstructureddatasnapshotdataref" && reqVars.method == RequestMethodEnum.Get) ||
                        (reqVars.pageId == "syncstructureddatasnapshotdatastart" && reqVars.method == RequestMethodEnum.Post) ||
                        (reqVars.pageId == "syncstructureddatasnapshotdatastart" && reqVars.method == RequestMethodEnum.Post)
                    )
                    {
                        HandleError(ReturnTypeEnum.Json, "Request cannot be processed because the project status is closed", $"pageId: {reqVars.pageId}", 403);
                    }
                }
            }
        }

        // Proceed with the next middleware component in the pipeline
        await _next(context);
    }
}