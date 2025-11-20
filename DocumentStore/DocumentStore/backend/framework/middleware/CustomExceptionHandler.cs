using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    /// <summary>
    /// Custom middleware to handle exceptions which are not catch in the code
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseCustomExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UseCustomExceptionHandler>();
    }
}

public class UseCustomExceptionHandler //: ProjectLogic
{
    private readonly RequestDelegate _next;

    public UseCustomExceptionHandler(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        string? message = null;
        string? errorResponseToClient = null;

        try
        {
            // Proceed with the next middleware component in the pipeline
            await this._next.Invoke(context);
        }
        catch (HttpStatusCodeException httpStatusCodeException)
        {
            //
            // => Handle custom exception that is usually thrown by the HandleError() routine
            //           
            if (context.Response.HasStarted)
            {
                Console.WriteLine("The response has already started, the exception handler middleware (httpStatusCodeException) will not be executed.");
            }
            else
            {
                // Retrieve the main message
                message = httpStatusCodeException.Message;

                // Log to console
                Framework.WriteErrorMessageToConsole(message, httpStatusCodeException.DebugInfo);

                // Include debug information in the return message if we are not the Taxxor Editor and we are not forced in debug mode
                var returnDebugInformation = Framework.applicationId != "taxxoreditor";
                if (Framework.applicationId != "taxxoreditor" && httpStatusCodeException.ReqVars.isDebugMode) returnDebugInformation = true;

                // Render a readable message to the client
                errorResponseToClient = Framework.RenderErrorResponse(message, ((returnDebugInformation) ? httpStatusCodeException.DebugInfo : ""), (httpStatusCodeException.ReqVars?.returnType ?? Framework.ReturnTypeEnum.Txt));
                errorResponseToClient = errorResponseToClient.Replace("[nonce]", context.Items["nonce"]?.ToString()?? "");

                // Write the error response to the client
                await context.Response.Custom(httpStatusCodeException.StatusCode, httpStatusCodeException.StatusSubCode, errorResponseToClient, (httpStatusCodeException.ReqVars?.returnType ?? Framework.ReturnTypeEnum.Txt), true);
            }
        }
        catch (RedirectException redirectException)
        {
            //
            // => Purposely thrown custom error for redirection purposes (error stops all further processing in the middleware pipeline :-) )
            //
            if (context.Response.HasStarted)
            {
                Console.WriteLine("The response has already started, the exception handler middleware (redirectException) will not be executed.");
            }
            else
            {
                // URL that we want to redirect the client to
                var redirectUrl = redirectException.RedirectUrl;

                if (redirectException.DebugMode && Framework.debugRedirects)
                {
                    errorResponseToClient = $"About to redirect to <a href=\"{redirectUrl}\">{redirectUrl}</a>\nFrom RedirectToPage()";
                    Console.WriteLine(errorResponseToClient);
                    await context.Response.OK($"<h1>From RedirectException</h1><p>{errorResponseToClient}</p><p>{Framework.RetrieveAllSessionData(Framework.ReturnTypeEnum.Html)}</p>");
                }
                else
                {
                    context.Response.Redirect(redirectUrl);
                }
            }
        }
        catch (Exception exception)
        {
            //
            // => Catch all for all other errors
            //
            if (context.Response.HasStarted)
            {
                Console.WriteLine("The response has already started, the exception handler middleware (exception) will not be executed.");
            }
            else
            {
                // Request variables are used to determine the type of response that we need to return to the client
                var reqVars = Framework.RetrieveRequestVariables(context);

                // Retrieve the main message
                message = exception.Message;

                // Log to console
                Framework.WriteErrorMessageToConsole(message, exception.ToString());

                // Render a readable message to the client
                var debugInfo = "";
                try
                {
                    debugInfo = (reqVars.isDebugMode) ? exception.ToString() : "";
                }
                catch (Exception exInner)
                {
                    Console.WriteLine($"ERROR in UseDefaultExceptionHandler(): there was an issue retrieving the exception details. error: {exInner.ToString()}");
                }
                errorResponseToClient = Framework.RenderErrorResponse(message, debugInfo, (reqVars?.returnType ?? Framework.ReturnTypeEnum.Txt));

                // Write the error response to the client
                await context.Response.Error(errorResponseToClient, (reqVars?.returnType ?? Framework.ReturnTypeEnum.Txt));
            }
        }



    }
}