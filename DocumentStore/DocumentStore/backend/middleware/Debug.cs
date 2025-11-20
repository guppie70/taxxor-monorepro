using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseDebugMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DebugMiddleware>();
    }
}



public class DebugMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;


    public DebugMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Rerieve the request variables
        var reqVars = RetrieveRequestVariables(context);


        if (!reqVars.isStaticAsset && !reqVars.isGrpcRequest)
        {

            if (isDevelopmentEnvironment || reqVars.isDebugMode)
            {
                // Retrieve the project variables
                var projectVars = RetrieveProjectVariables(context);

                //
                // Dump the incoming request for inspection
                //
                if (reqVars.method != RequestMethodEnum.Get)
                {
                    appLogger.LogInformation("-------- Incoming Request Information --------");
                    Console.WriteLine(await DebugIncomingRequest(context.Request));
                    Console.WriteLine("");
                }


                //
                // Dump the RequestVariables object for inspection
                //
                if (debugConsoleDumpRequestVariables)
                {
                    appLogger.LogInformation("-------- RequestVariables Properties --------");
                    Console.WriteLine(GenerateDebugObjectString(reqVars, ReturnTypeEnum.Txt));
                    Console.WriteLine("");
                }


                //
                // Dump the ProjectVariables object for inspection
                //
                if (debugConsoleDumpProjectVariables)
                {
                    appLogger.LogInformation("-------- ProjectVariables Properties --------");
                    Console.WriteLine(GenerateDebugObjectString(projectVars, ReturnTypeEnum.Txt));
                    Console.WriteLine("");
                }


                // Dump internal documents so that they can be inspected  
                if (debugStoreInspectorLogFiles && reqVars.method != RequestMethodEnum.Get)
                {
                    var inspectorLogFolderPathOs = logRootPathOs + "/inspector";
                    try
                    {
                        // Create the inspector directory if needed
                        if (!Directory.Exists(inspectorLogFolderPathOs)) Directory.CreateDirectory(inspectorLogFolderPathOs);

                        // Dump the files
                        TextFileCreate(PrettyPrintXml(xmlApplicationConfiguration.OuterXml), inspectorLogFolderPathOs + "/application-configuration.xml");
                        TextFileCreate(PrettyPrintXml(reqVars.xmlHierarchy.OuterXml), inspectorLogFolderPathOs + "/site-structure.xml");

                        if (projectVars.currentUser.XmlUserPreferences.ChildNodes.Count > 0) TextFileCreate(PrettyPrintXml(projectVars.currentUser.XmlUserPreferences.OuterXml), inspectorLogFolderPathOs + "/user-preferences.xml");
                    }
                    catch (Exception ex)
                    {
                        WriteErrorMessageToConsole("Something went wrong in storing the inspector data", ex.ToString());
                    }
                }

            }
        }
        else
        {
            if ((isDevelopmentEnvironment || reqVars.isDebugMode) && !reqVars.isGrpcRequest)
            {
                appLogger.LogInformation("-------- Static Asset --------");
                appLogger.LogInformation($"- reqVars.rawUrl: {reqVars.rawUrl}");
            }
        }


        // Proceed with the next middleware component in the pipeline
        await this._next.Invoke(context);
    }


}