using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseDataServiceAssetsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DataServiceAssetsMiddleware>();
    }
}

/// <summary>
/// Logic to stream assets directly from the Taxxor Data Store to the web client of the user
/// </summary>
public class DataServiceAssetsMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;

    public DataServiceAssetsMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Rerieve the request variables
        var reqVars = RetrieveRequestVariables(context);

        if (reqVars.isStaticAsset && reqVars.thisUrlPath.StartsWith("/dataserviceassets/") && (reqVars.thisUrlPath.Contains("/images/") || reqVars.thisUrlPath.Contains("/downloads/")))
        {
            var projectVars = RetrieveProjectVariables(context);
            var pdfServiceRequest = IsPdfServiceRequest();

            // Retrieve the path of the asset that we are trying to retrieve from the data service
            // Path is build-up of components that we need to parse
            var currentProjectId = "";
            var assetPathToRetrieve = "";
            Match regexMatches = ReDataServiceAssetsPathParser.Match(reqVars.thisUrlPath);
            if (regexMatches.Success)
            {
                currentProjectId = regexMatches.Groups[1].Value;
                assetPathToRetrieve = regexMatches.Groups[2].Value;
            }
            var assetFileName = Path.GetFileName(assetPathToRetrieve);

            // Test if the current project ID exists and if this user has access to it...
            var nodeCmsProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(currentProjectId)}]");
            if (nodeCmsProject == null) HandleError("Asset could not be found", $"asset: {reqVars.thisUrlPath} could not be streamed because currentProjectId: '{currentProjectId}' was not found, stack-trace: {GetStackTrace()}", 404);

            // Check if the user ID is present in the headers
            var userIdHeader = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserId");

            // Retrieve the user ID from the session
            var userIdSession = context.Session.GetString("user_id");

            // Check and valisdate a possible token
            var validToken = AccessToken.Validate(context.Request, reqVars);


            if (!string.IsNullOrEmpty(userIdSession) || pdfServiceRequest || !string.IsNullOrEmpty(userIdHeader) || validToken)
            {
                // Normally there is no user initiated for static assets, so we have to create one in order to check if the current user is allowed to view the asset
                var taxxorUser = new AppUserTaxxor();

                // Decide which user we will be using for retrieving the asset from the Taxxor Document Store
                var userIdForAssetRetrieval = SystemUser;
                if (!pdfServiceRequest && !validToken)
                {
                    if (!string.IsNullOrEmpty(userIdSession))
                    {
                        userIdForAssetRetrieval = userIdSession;
                    }
                    else if (!string.IsNullOrEmpty(userIdHeader))
                    {
                        userIdForAssetRetrieval = userIdHeader;
                    }
                    else
                    {
                        userIdForAssetRetrieval = SystemUser;
                    }
                }

                // Console.WriteLine($"- userIdForAssetRetrieval: {userIdForAssetRetrieval}");

                // Use the session information to retrieve the current user information
                taxxorUser.IsAuthenticated = true;
                taxxorUser.Id = userIdForAssetRetrieval;
                projectVars.currentUser = taxxorUser;
                projectVars.userIdFromHeader = userIdForAssetRetrieval;

                // Attach the project ID that we have parsed from the URL
                projectVars.projectId = currentProjectId;

                // Test if the current user has access to the project for which he/she is requesting this asset
                bool hasAccess = pdfServiceRequest || validToken;
                if (!hasAccess)
                {
                    hasAccess = await UserHasAccessToProject(userIdForAssetRetrieval, currentProjectId);
                }

                // Only stream a (binary) file to the client if the user has access to the project
                if (hasAccess)
                {
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "pid", projectVars.projectId },
                        { "relativeto", "cmscontentroot" },
                        { "path", assetPathToRetrieve }
                    };

                    var apiUrl = QueryHelpers.AddQueryString(GetServiceUrlByMethodId(ConnectedServiceEnum.DocumentStore, "taxxoreditorfilingimage"), dataToPost);

                    // Stream the image to the client and autoconvert TIFF to PNG when it needs to be shown in the browser
                    await StreamImage(context.Response, apiUrl, !context.Request.Headers.UserAgent.ToString().Contains("prince", StringComparison.CurrentCultureIgnoreCase));
                }
                else
                {
                    appLogger.LogError($"Unauthorized: User: '{userIdForAssetRetrieval}' does not have access to project with ID '{currentProjectId}', stack-trace: {GetStackTrace()}");

                    await StreamErrorImage();
                }

            }
            else
            {
                if (siteType == "local" || siteType == "dev")
                {
                    Console.WriteLine("");
                    Console.WriteLine(await DebugIncomingRequest(context.Request));
                    Console.WriteLine("");
                }

                appLogger.LogError($"Unauthorized: User could not be authenticated, reqVars.thisUrlPath: {reqVars.thisUrlPath}, stack-trace: {GetStackTrace()}");
            }

        }
        else
        {
            // Proceed with the next middleware component in the pipeline
            await this._next.Invoke(context);
        }

        async Task StreamErrorImage()
        {
            context.Response.ContentType = GetContentType("jpg");
            // Return the broken image binary data
            await context.Response.Body.WriteAsync(BrokenImageBytes, 0, BrokenImageBytes.Length);
        }

    }
}