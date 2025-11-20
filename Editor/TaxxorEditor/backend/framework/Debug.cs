using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using static Taxxor.Project.ProjectLogic;

/// <summary>
/// Debugging routines to aid/help debugging tasks
/// </summary>
public abstract partial class Framework
{
    /// <summary>
    /// Retrieves if we are running in debug mode or not
    /// </summary>
    /// <returns></returns>
    public static bool RetrieveDebugMode(HttpRequest httpRequest)
    {
        var debugMode = false || siteType == "local" || siteType == "dev" || hostingEnvironment.EnvironmentName == "Development" || httpRequest.ContainsDebugHeader() || httpRequest.QueryString.ToString().Contains("debug=true");
        return debugMode;
    }

    /// <summary>
    /// Retrieves if we need to debug the cache system or not
    /// </summary>
    /// <returns></returns>
    public static bool RetrieveDebugCacheMode(HttpRequest httpRequest)
    {
        var debugMode = false || httpRequest.RetrievePostedValue("debugcache", "false") == "true";
        return debugMode;
    }

    /// <summary>
    /// Generates an overview of the sessions data
    /// </summary>
    /// <param name="returnType">Supported are ReturnTypeEnum.Html and ReturnTypeEnum.Txt (defaults to HTML)</param>
    /// <returns>Formatted html with the session data ready for display in browser</returns>
    public static string RetrieveAllSessionData(ReturnTypeEnum returnType = ReturnTypeEnum.Html)
    {
        var context = System.Web.Context.Current;
        var title = "Session Information";
        var newline = ((returnType == ReturnTypeEnum.Html) ? ("<br/>" + Environment.NewLine) : Environment.NewLine);

        // TODO: Fix session management

        // Header
        var sessionInfo = (returnType == ReturnTypeEnum.Html) ? $"<div class=\"sessioninfo\"><h4>{title}</h4><div class=\"details\">" : title + newline;
        sessionInfo += $"Number of Objects: {context.Session.Keys.Count()}{newline}";
        sessionInfo += $"Session ID: {context.Session.Id}{newline}";

        foreach (string key in context.Session.Keys)
        {
            var value = "";
            try
            {
                value = context.Session.GetString(key);

                //      // Expand the values if this is an ArrayList
                //      if (value == "System.Collections.ArrayList")
                //      {
                //          ArrayList TargetArrayList = (ArrayList)Session[key];
                //          value = "'System.Collections.ArrayList:' " + string.Join(", ", (string[])TargetArrayList.ToArray(Type.GetType("System.String")));
                //      }
            }
            catch (Exception ex)
            {
                value = $"[could not retrieve value: {ex}]";
            }

            sessionInfo += $"- {key}: {value}{newline}";
        }

        if (returnType == ReturnTypeEnum.Html) sessionInfo += "</div></div>";
        return sessionInfo;
    }

    /// <summary>
    /// Retrieves all the server variables as a string
    /// </summary>
    /// <param name="returnType">Supported are ReturnTypeEnum.Html and ReturnTypeEnum.Txt (defaults to HTML)</param>
    /// <returns></returns>
    public static string RetrieveAllServerVariables(ReturnTypeEnum returnType = ReturnTypeEnum.Html)
    {
        var title = "Server Variables Information";
        var newline = ((returnType == ReturnTypeEnum.Html) ? ("<br/>" + Environment.NewLine) : Environment.NewLine);

        // TODO: Server variables are HTTP headers in dotnetcore
        // Header
        var serverInfo = (returnType == ReturnTypeEnum.Html) ? $"<div class=\"servervarsinfo\"><h4>{title}</h4><div class=\"details\">" : title + newline;
        // serverInfo += "Number of Objects: " + Request.Headers.Count.ToString() + newline;

        // foreach (string key in Request.ServerVariables)
        // {
        // 	serverInfo += "  * " + key + " - " + Request.ServerVariables[key] + newline;
        // }
        if (returnType == ReturnTypeEnum.Html) serverInfo += "</div></div>";
        return serverInfo;
    }

    /// <summary>
    /// Retrieves all the HTML request headers as a string
    /// </summary>
    /// <param name="returnType">Supported are ReturnTypeEnum.Html and ReturnTypeEnum.Txt (defaults to HTML)</param>
    /// <returns></returns>
    public static string RetrieveAllHttpRequestHeaderValues(HttpRequest Request, ReturnTypeEnum returnType = ReturnTypeEnum.Html)
    {
        var title = "HTTP Request Headers Information";
        var newline = ((returnType == ReturnTypeEnum.Html) ? ("<br/>" + Environment.NewLine) : Environment.NewLine);

        

        // Header
        var httpHeaderInfo = (returnType == ReturnTypeEnum.Html) ? $"<div class=\"httpheaderinfo\"><h4>{title}</h4><div class=\"details\">" : title + newline;
        if(Request == null) return $"{httpHeaderInfo}{newline}Request is not available";
        httpHeaderInfo += "Number of Objects: " + Request.Headers.Count.ToString() + newline;

        List<string> RequestHeaders = new List<string>();
        var uniqueRequestHeaders = Request.Headers
            .Where(x => RequestHeaders.All(r => r != x.Key))
            .Select(x => x.Key);

        RequestHeaders.AddRange(uniqueRequestHeaders);

        // Build the string
        foreach (string httpHeaderKey in RequestHeaders.OrderBy(x => x))
        {
            string currentServerVariable = Request.RetrieveFirstHeaderValueOrDefault<string>(httpHeaderKey);
            httpHeaderInfo += $"- Header['{httpHeaderKey}'] = '{currentServerVariable}'{newline}";
        }

        if (returnType == ReturnTypeEnum.Html) httpHeaderInfo += "</div></div>";
        return httpHeaderInfo;
    }

    /// <summary>
    /// Generates a web browser display friendly string based on html/xml type input
    /// </summary>
    /// <param name="strIn">string to convert</param>
    /// <returns>Formatted html encoded string ready for display in browser</returns>
    public static string HtmlEncodeForDisplay(string strIn)
    {
        return "<pre>" + HtmlEncode(strIn).Replace("&gt;&lt;", "&gt;<br/>&lt;") + "</pre>";
    }

    /// <summary>
    /// HTML encodes a string
    /// </summary>
    /// <param name="strIn"></param>
    /// <returns></returns>
    public static string HtmlEncode(string strIn)
    {
        using(StringWriter stringWriter = new StringWriter())
        {
            WebUtility.HtmlEncode(strIn, stringWriter);
            var encodedOutput = stringWriter.ToString();
            stringWriter.Close();
            return encodedOutput;
        }
    }

    /// <summary>
    /// Generates a dump in HTML format of an object and all its members
    /// </summary>
    /// <param name="objectIn">The object to investigate</param>
    public static void DebugObject(HttpResponse response, object objectIn)
    {

        response.WriteAsync(GenerateDebugObjectString(objectIn));

    }

    /// <summary>
    /// Generates a string so that you can "peek" into any object
    /// </summary>
    /// <param name="objectIn"></param>
    /// <param name="returnType">Supported are ReturnTypeEnum.Html and ReturnTypeEnum.Txt (defaults to HTML)</param>
    /// <returns></returns>
    public static string GenerateDebugObjectString(object objectIn, ReturnTypeEnum returnType = ReturnTypeEnum.Html)
    {
        var newline = ((returnType == ReturnTypeEnum.Html) ? ("<br/>" + Environment.NewLine) : Environment.NewLine);
        var debugInfo = new StringBuilder();
        // Get the type.
        Type objectType = objectIn.GetType();
        string? objectValue = "";

        debugInfo.Append(((returnType == ReturnTypeEnum.Html) ? $"<div class=\"objectinfo\"><h4>Object: {objectType.FullName}</h4><br/><div class=\"details\"><u>Fields:</u><br/>" : "Object: " + objectType.FullName + newline + "Fields: " + newline));

        //list the fields        
        foreach (FieldInfo fieldInfo in objectType.GetFields())
        {
            if (returnType == ReturnTypeEnum.Html)
            {
                debugInfo.Append(string.Format(("- {0} = {1}<br/>" + newline), fieldInfo.Name, fieldInfo.GetValue(objectIn)));
            }
            else
            {
                debugInfo.Append(string.Format(("{0}: {1}, " + newline), fieldInfo.Name, fieldInfo.GetValue(objectIn)));
            }
        }

        //list the properties.        
        debugInfo.Append(((returnType == ReturnTypeEnum.Html) ? "<u>Properties:</u><br/>" : ("Properties:" + newline)));
        PropertyInfo[] pi = objectType.GetProperties();
        //Context.Response.Write(pi.Length.ToString());

        foreach (PropertyInfo propertyInfo in objectType.GetProperties())
        {
            //strDebugText += p.Name + " = " + p.GetValue(o,null);
            //Response.Write(p.Name);

            try
            {
                if (propertyInfo.GetValue(objectIn, null) != null)
                {
                    objectValue = propertyInfo.GetValue(objectIn, null).ToString();
                    if (returnType == ReturnTypeEnum.Html)
                    {
                        debugInfo.Append(string.Format(("- {0} = {1}<br/>" + newline), propertyInfo.Name, objectValue));
                    }
                    else
                    {
                        debugInfo.Append(string.Format(("{0}: {1}, " + newline), propertyInfo.Name, objectValue));
                    }
                }
            }
            catch (Exception ex)
            {
                WriteErrorMessageToConsole("Could not convert object element to a string", "Details: " + ex.ToString());
                //DebugObject(ex); 
            }
        }
        if (returnType == ReturnTypeEnum.Html) debugInfo.Append("</div></div>");
        return debugInfo.ToString();
    }

    /// <summary>
    /// Generates a dump in HTML format of a string variable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="expr"></param>
    public static void DebugVariable<T>(Func<T> expr)
    {
        // get IL code behind the delegate  
        var il = expr.Method.GetMethodBody()?.GetILAsByteArray();
        // bytes 2-6 represent the field handle  
        var fieldHandle = BitConverter.ToInt32(il, 2);
        // resolve the handle  
        var field = expr.Target.GetType().Module.ResolveField(fieldHandle);

        string output = "- " + field.Name + " = " + expr();

        Console.WriteLine(output);

        // TODO: fix this
        // HttpResponse.WriteAsync(output);
    }

    /// <summary>
    /// Appends a new piece of debug information to the global debug variable
    /// </summary>
    /// <param name="newDebugContent">(HTML) content to add</param>
    public static void AddDebugVariable(string newDebugContent)
    {
        HttpContext? context = null;

        try
        {
            context = System.Web.Context.Current;
            if (context != null)
            {
                try
                {
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    reqVars.debugContent.Append(newDebugContent);
                }
                catch (Exception)
                {
                    // Simply accept that in some cases we are not able to add specific data to the debugContent string builder
                    // appLogger.LogInformation("Could not add debug variable");
                }
            }
        }
        catch (Exception) { }

    }

    /// <summary>
    /// Adds a new string variable debug information to the global debug variable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="expr"></param>
    /*
    public void AddDebugVariable<T>(Func<T> expr)
    {
    	// get IL code behind the delegate  
    	var il = expr.Method.GetMethodBody().GetILAsByteArray();
    	// bytes 2-6 represent the field handle  
    	var fieldHandle = BitConverter.ToInt32(il, 2);
    	// resolve the handle  
    	var field = expr.Target.GetType().Module.ResolveField(fieldHandle);

    	string newDebugContent = "- " + field.Name + " = " + expr() + "<br/>";

    	AddDebugVariable(newDebugContent);
    }
    */
    public static void AddDebugVariable<T>(Expression<Func<T>> expr)
    {
        var body = (MemberExpression) expr.Body;
        string variableName = body.Member.Name;

        var variableValue = "";
        try
        {
            // This seems to work with static fields
            var field = typeof(Framework).GetField(variableName, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                // This seems to work with non-static fields
                variableValue = (string) ((FieldInfo) body.Member).GetValue(((ConstantExpression) body.Expression).Value);
            }
            else
            {
                var tempValue = field.GetValue(null);
                if (tempValue != null)
                {
                    var valueType = tempValue.GetType().ToString();
                    if (valueType == "System.String")
                    {
                        variableValue = (string) field.GetValue(null);
                    }
                    else if (valueType == "System.Boolean")
                    {
                        variableValue = tempValue.ToString()?.ToLower();
                    }
                }
                else
                {
                    variableValue = "{null}";
                }
            }

            //variableValue = (string)field.GetValue(null); 
        }
        catch (Exception)
        {
            try
            {
                // This seems to work with non-static fields
                variableValue = (string) ((FieldInfo) body.Member).GetValue(((ConstantExpression) body.Expression).Value);
            }
            catch (Exception)
            {
                variableValue = "{unable to retrieve...'}";
            }
        }

        AddDebugVariable(variableName, variableValue);
    }

    /// <summary>
    /// Adds a new string variable debug information to the global debug variable
    /// </summary>
    /// <param name="variableName">Variable name.</param>
    /// <param name="variableValue">Variable value.</param>
    public static void AddDebugVariable(string variableName, object variableValue)
    {
        // Basic debugging content
        var newDebugContent = $"- {variableName} = {(string)variableValue}";

        // Write to the console
        appLogger.LogInformation(newDebugContent);

        // Add HTML stuff
        newDebugContent += "<br/>" + Environment.NewLine;

        // Add the content to the global debugging string builder
        AddDebugVariable(newDebugContent);
    }

    /// <summary>
    /// Retrieve the profiling mode
    /// </summary>
    /// <returns></returns>
    public static bool RetrieveProfilingMode(HttpRequest httpRequest)
    {
        bool profilingMode = httpRequest.RetrievePostedValue("profiling", "false") == "true";
        return profilingMode;
    }

    /// <summary>
    /// Starts the profiling/timing utility
    /// </summary>
    /// <param name="message"></param>
    public static void ProfilerStart(HttpRequest httpRequest, string message)
    {
        var reqVars = RetrieveRequestVariables(System.Web.Context.Current);
        if (!isProfilingMode) isProfilingMode = RetrieveProfilingMode(httpRequest);
        //use the globally defined stopwatch classes and profiler result class for the measurement
        ProfilerStart(message, isProfilingMode, ReturnTypeEnum.Html, ref reqVars.profilingResult, ref stopWatchProfilerTotal, ref stopWatchProfiler);
    }

    /// <summary>
    /// Starts the profiling/timing utility used for custom measurements
    /// </summary>
    /// <param name="message"></param>
    /// <param name="forceProfilingMode">Set to true if you want to start a custom profiling session</param>
    public static void ProfilerStart(string message, bool forceProfilingMode, ReturnTypeEnum returnType)
    {
        isProfilingMode = forceProfilingMode;
        if (isProfilingMode)
        {
            stopWatchProfilerTotal.Start();
            stopWatchProfiler.Start();
            ProfilerMeasure(message, returnType);
        }
    }

    /// <summary>
    /// Starts the profiling/timing utility used for custom measurements in combination with custom stopwatch objects
    /// </summary>
    /// <param name="message"></param>
    /// <param name="forceProfilingMode"></param>
    /// <param name="returnType"></param>
    /// <param name="stopWatchProfilerTotal">ref to custom stopwatch object (total)</param>
    /// <param name="stopWatchProfiler">ref to custom stopwatch object (laptime)</param>
    public static void ProfilerStart(string message, bool forceProfilingMode, ReturnTypeEnum returnType, ref StringBuilder profilingResult, ref Stopwatch stopWatchProfilerTotal, ref Stopwatch stopWatchProfiler)
    {
        isProfilingMode = forceProfilingMode;
        if (isProfilingMode)
        {
            stopWatchProfilerTotal.Start();
            stopWatchProfiler.Start();
            ProfilerMeasure(message, returnType);
        }
    }

    /// <summary>
    /// Generates a "laptime" - time elapsed between previous measuring point or start moment of the profiler
    /// </summary>
    /// <param name="message"></param>
    public static void ProfilerMeasure(string message)
    {
        ProfilerMeasure(message, ReturnTypeEnum.Html);
    }

    /// <summary>
    /// Generates a "laptime" - time elapsed between previous measuring point or start moment of the profiler
    /// </summary>
    /// <param name="message"></param>
    /// <param name="returnType"></param>
    public static void ProfilerMeasure(string message, ReturnTypeEnum returnType)
    {
        var reqVars = RetrieveRequestVariables(System.Web.Context.Current);
        ProfilerMeasure(message, returnType, ref reqVars.profilingResult, ref stopWatchProfiler);
    }

    /// <summary>
    /// Generates a "laptime" - time elapsed between previous measuring point or start moment of the profiler
    /// </summary>
    /// <param name="message"></param>
    /// <param name="returnType"></param>
    /// <param name="stopWatchProfiler">ref parameter to custom stopwatch instance (laptime)</param>
    public static void ProfilerMeasure(string message, ReturnTypeEnum returnType, ref StringBuilder profilingResult, ref Stopwatch stopWatchProfiler)
    {
        if (isProfilingMode)
        {
            if (stopWatchProfiler.IsRunning && stopWatchProfilerTotal.IsRunning)
            {
                stopWatchProfiler.Stop();
                var newline = ((returnType == ReturnTypeEnum.Html) ? ("<br/>" + Environment.NewLine) : Environment.NewLine);
                string profilingResultBase = "- Elapsed: " + stopWatchProfiler.Elapsed + ", " + message + "." + newline;
                profilingResult.AppendLine(profilingResultBase);
                stopWatchProfiler.Reset();
                stopWatchProfiler.Start();
            }
        }
    }

    /// <summary>
    /// Stops the profiler and returns the debug string that it created
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static string ProfilerStop(string message)
    {
        var reqVars = RetrieveRequestVariables(System.Web.Context.Current);
        return ProfilerStop(message, ReturnTypeEnum.Html, ref reqVars.profilingResult, ref stopWatchProfilerTotal, ref stopWatchProfiler);
    }

    /// <summary>
    /// Stops the profiler and returns the debug string that it created using the specified format
    /// </summary>
    /// <param name="message"></param>
    /// <param name="returnType"></param>
    /// <returns></returns>
    public static string ProfilerStop(string message, ReturnTypeEnum returnType)
    {
        var reqVars = RetrieveRequestVariables(System.Web.Context.Current);
        return ProfilerStop(message, returnType, ref reqVars.profilingResult, ref stopWatchProfilerTotal, ref stopWatchProfiler);
    }

    /// <summary>
    /// Stops the profiler and returns the debug string that it created using the specified format
    /// </summary>
    /// <param name="message"></param>
    /// <param name="returnType"></param>
    /// <param name="stopWatchProfilerTotal">ref to custom stopwatch object (total)</param>
    /// <param name="stopWatchProfiler">ref to custom stopwatch object (laptime)</param>
    /// <returns></returns>
    public static string ProfilerStop(string message, ReturnTypeEnum returnType, ref StringBuilder profilingResult, ref Stopwatch stopWatchProfilerTotal, ref Stopwatch stopWatchProfiler)
    {
        string profilerContent = "";
        if (isProfilingMode)
        {
            if (stopWatchProfiler.IsRunning && stopWatchProfilerTotal.IsRunning)
            {
                ProfilerMeasure(message);
                stopWatchProfiler.Stop();
                stopWatchProfilerTotal.Stop();
                var newline = ((returnType == ReturnTypeEnum.Html) ? ("<br/>" + Environment.NewLine) : Environment.NewLine);
                string profilingResultBase = "- Total time taken: " + stopWatchProfilerTotal.Elapsed + "." + newline;
                profilingResult.AppendLine(profilingResultBase);

                profilerContent = profilingResult.ToString();
            }
        }

        return profilerContent;
    }

    /// <summary>
    /// Helper function to dump a dictionary for logging
    /// </summary>
    /// <param name="dict"></param>
    /// <param name="keyName"></param>
    /// <param name="valueName"></param>
    /// <returns></returns>
    public static string DebugDictionary(Dictionary<string, string> dict, string keyName = "Key", string valueName = "Value")
    {
        return string.Join(',', dict.Select(x => $"{keyName}: {x.Key} - {valueName}: {x.Value}").ToArray());
    }

    
    /// <summary>
    /// Renders the location in the script for debugging purposes
    /// </summary>
    /// <param name="name"></param>
    /// <param name="path"></param>
    /// <param name="line"></param>
    public static string LogCurrentFrame([CallerMemberName] string name = null, [CallerFilePath] string path = null, [CallerLineNumber] int line = 0)
    {
        return $"{name}() in {path.Replace(applicationRootPathOs, "")} at line {line}";
    }

        /// <summary>
    /// Helper routine to render some debug information in case a remote HTTP call fails
    /// </summary>
    /// <returns></returns>
    public static string RenderHttpRequestDebugInformation(string delimiter = "=")
    {
        var url = "unknown";
        var method = "unknown";
        var pageId = "unknown";
        var userId = "unknown";

        try
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context, false);
            ProjectVariables projectVars = RetrieveProjectVariables(context, false);

            url = reqVars.rawUrl;
            method = RequestMethodEnumToString(reqVars.method);
            pageId = reqVars.pageId;
            userId = projectVars?.currentUser?.Id ?? "unknown";
        }
        catch (Exception ex)
        {
            appLogger.LogWarning(ex, "There was a problem retrieving debug information");
        }

        return $"url{delimiter}{url}, method{delimiter}{method}, pageId{delimiter}{pageId}, userId{delimiter}{userId}";
    }

}