using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestVariablesMiddleware(this IApplicationBuilder builder, Framework.FrameworkOptions frameworkOptions)
    {
        return builder.UseMiddleware<RequestVariablesMiddleware>(Options.Create(frameworkOptions));
    }
}

public class RequestVariablesMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;
    private readonly Framework.FrameworkOptions _frameworkOptions;

    public RequestVariablesMiddleware(RequestDelegate next, IOptions<Framework.FrameworkOptions> frameworkOptions)
    {
        this._next = next;
        this._frameworkOptions = frameworkOptions.Value;
    }

    public async Task Invoke(HttpContext context)
    {

        // if(isDevelopmentEnvironment) appLogger.LogInformation("** In RequestVariables Middleware **");

        // Instantiate a new RequestVariables class
        var reqVars = new Framework.RequestVariables(context);
        reqVars.isMiddlewareCreated = true;

        // Assure that vital objects are available
        var recompileFrameworkFields = false;
        try
        {
            if (string.IsNullOrEmpty(xmlApplicationConfiguration.DocumentElement.LocalName))
            {
                recompileFrameworkFields = true;
            }
        }
        catch (Exception)
        {
            recompileFrameworkFields = true;
        }
        if (recompileFrameworkFields)
        {
            WriteErrorMessageToConsole("Need to recompile static fields for the application");
            InitApplicationLogic(hostingEnvironment, memoryCache);
            InitProjectLogic();
        }

        // Check if we need to test the availability of session management (using global string variable)
        if (string.IsNullOrEmpty(sessionConfigured))
        {
            try
            {
                var bogus = context.Session.GetString("user_id");
                sessionConfigured = "yes";
            }
            catch (Exception)
            {
                sessionConfigured = "no";
            }
        }

        // Force asynchroneous session data CRUD
        if (sessionConfigured == "yes")
        {
            await context.Session.LoadAsync();
        }

        // Test if the incoming request is a gRPC request
        reqVars.isGrpcRequest = reqVars.thisUrlPath.StartsWith("/grpc.");
        if (reqVars.isGrpcRequest) reqVars.isStaticAsset = false;

        // Set information related to the hierarchy of this site
        if (contentFilePath.Match(reqVars.thisUrlPath).Success && !reqVars.thisUrlPath.EndsWith(".properties") && !reqVars.thisUrlPath.EndsWith("negotiate") && !reqVars.thisUrlPath.StartsWith("/custom/") && !reqVars.isGrpcRequest)
        {
            reqVars.isStaticAsset = false;

            // Rework the hierarchy
            SetRequestVariables(context, reqVars);
            reqVars.xmlHierarchy = Extensions.ReworkHierarchy(context, reqVars.xmlHierarchy);

            // Attempt to find the page ID
            reqVars.pageId = RetrievePageId(reqVars, reqVars.thisUrlPath, reqVars.xmlHierarchy, ReturnTypeEnum.Txt, true);
            reqVars.pageTitle = RetrieveNodeValueIfExists("//item[@id=" + GenerateEscapedXPathString(reqVars.pageId) + "]/web_page/linkname", reqVars.xmlHierarchy);

            // Log something to mark the following entries in the console
            if (!IsInternalServicePage(reqVars) && (isDevelopmentEnvironment || reqVars.isDebugMode) && (reqVars.pageId != "filingeditorlistlocks" && reqVars.pageId != "systemstate"))
            {
                appLogger.LogInformation($"*------- Dynamic Asset ({reqVars.rawUrl}, Method: {reqVars.method}, ID: {reqVars.pageId}) -------*");
            }
        }

        if (reqVars.isDebugMode && reqVars.isGrpcRequest)
        {
            appLogger.LogInformation($"*------- gRPC Request ({reqVars.rawUrl}) -------*");
        }

        // Append the RequestVariables instance to the context
        SetRequestVariables(context, reqVars);



        // => Parse incoming JSON POST data as XML so that we can use it
        if (
            reqVars.returnType == ReturnTypeEnum.Json &&
            reqVars.method != RequestMethodEnum.Get &&
            !context.Request.ContentType.Contains("application/x-www-form-urlencoded") &&
            !context.Request.HasFormContentType &&
            !reqVars.thisUrlPath.StartsWith("/proxy")
        )
        {
            // Console.WriteLine("---------------------");
            // Console.WriteLine("Converting posted JSON data to XmlDocument");
            // Console.WriteLine("---------------------");            
            try
            {
                context.Request.EnableBuffering();


                if (context.Request.Body.CanSeek)
                {
                    // Capture the request body so that we can put it back later
                    var body = context.Request.Body;

                    var buffer = new byte[Convert.ToInt32(context.Request.ContentLength)];
                    await ReadExactlyAsync(context.Request.Body, buffer, 0, buffer.Length);
                    string postData = Encoding.UTF8.GetString(buffer);

                    try
                    {
                        reqVars.xmlJsonData = ConvertJsonToXml(postData, "data");
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Could not convert posted JSON data to XML. postData: {postData}, url: {reqVars.thisUrlPath}, error: {ex}");
                    }

                    try
                    {
                        // Set the request body back to it's original value so that we can use it in further processing
                        context.Request.Body.Position = 0;
                        context.Request.Body = body;
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Could not restore Request.Body. error: {ex}");
                    }
                }
                else
                {
                    appLogger.LogError($"Could not convert JSON in XML because the Request.BodyCanSeek = false");
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Could not convert posted JSON data to XML. url: {reqVars.rawUrl}, context.Request.ContentLength: {context.Request.ContentLength}, error: {ex}");
            }
        }


        // Create debug information if needed
        if ((isDevelopmentEnvironment || reqVars.isDebugMode) && _frameworkOptions.RequestVariablesDebugOutput)
        {
            appLogger.LogInformation("-------- RequestVariables Properties --------");
            Console.WriteLine("** From RequestVariables Middleware **");
            Console.WriteLine(reqVars.DebugVariables());
            Console.WriteLine("");
        }

        // Proceed with the next middleware component in the pipeline
        await this._next.Invoke(context);
    }




}