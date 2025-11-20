using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseUserDataMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserDataMiddleware>();
    }
}

public class UserDataMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;

    public UserDataMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Rerieve the request variables
        var reqVars = RetrieveRequestVariables(context);

        // if (isDevelopmentEnvironment || reqVars.isDebugMode) appLogger.LogInformation("** In UserDataMiddleware Middleware **");

        if (!reqVars.isStaticAsset)
        {
            // Retrieve the project variables
            var projectVars = RetrieveProjectVariables(context);

            // Retrieve the user data
            if (!projectVars.isInternalServicePage)
            {
                try
                {
                    if (projectVars.sessionCreated)
                    {
                        // If we have just created a new session, then we need to retrieve new data from the Taxxor Regulation Database
                        await RetrieveUserData(true);
                    }
                    else
                    {
                        await RetrieveUserData();
                    }

                }
                catch (Exception ex)
                {
                    WriteErrorMessageToConsole("Something went wrong while retrieving the user data", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }
            }

            // Store all the information in the ProjectVariables object
            SetProjectVariables(context, projectVars);
        }

        // Proceed with the next middleware component in the pipeline
        await _next(context);

    }
}