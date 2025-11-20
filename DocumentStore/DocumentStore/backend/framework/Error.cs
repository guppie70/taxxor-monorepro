using System;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Text;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Contains helper functions for working with Errors
/// </summary>
public abstract partial class Framework
{

    /// <summary>
    /// Handles an error by trowing an HttpStatusCodeException which invokes the middelware to render the appropriate response to the client
    /// Utility version that does not require an explicit reference to the RequestVariables object
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="debugInfo">Debug info.</param>
    /// <param name="statusCode">Status code.</param>
    /// <param name="statusSubCode">Status sub code.</param>
    public static void HandleError(string message, string? debugInfo = null, int statusCode = 500, int statusSubCode = -1)
    {
        // Attempt to retrieve the RequestVariables object from the context
        RequestVariables reqVars;
        try
        {
            var context = Context.Current;
            reqVars = RetrieveRequestVariables(context);
        }
        catch (Exception ex)
        {
            appLogger.LogInformation(ex, "Could not retrieve context for HandleError()");
            reqVars = new RequestVariables
            {
                returnType = ReturnTypeEnum.Txt
            };
        }
        HandleError(reqVars, message, debugInfo, statusCode, statusSubCode);
    }

    /// <summary>
    /// Handles an error by trowing an HttpStatusCodeException which invokes the middelware to render the appropriate response to the client
    /// </summary>
    /// <param name="xmlDoc">Xml document.</param>
    /// <param name="statusCode">Status code.</param>
    /// <param name="statusSubCode">Status sub code.</param>
    public static void HandleError(XmlDocument xmlDoc, int statusCode = 500, int statusSubCode = -1)
    {
        // Attempt to retrieve the RequestVariables object from the context
        RequestVariables reqVars;
        try
        {
            HttpContext? context = Context.Current;
            reqVars = RetrieveRequestVariables(context);
        }
        catch (Exception ex)
        {
            appLogger.LogInformation(ex, "Could not retrieve context for HandleError()");
            reqVars = new RequestVariables
            {
                returnType = ReturnTypeEnum.Txt
            };
        }

        HandleError(reqVars.returnType, xmlDoc, statusCode, statusSubCode);
    }

    /// <summary>
    /// Handles an error by trowing an HttpStatusCodeException which invokes the middelware to render the appropriate response to the client
    /// Utility version that allows explicit setting of the type of response that we need to return
    /// </summary>
    /// <param name="returnType">Return type.</param>
    /// <param name="xmlDoc">Xml document.</param>
    /// <param name="statusCode">Status code.</param>
    /// <param name="statusSubCode">Status sub code.</param>
    public static void HandleError(ReturnTypeEnum returnType, XmlDocument xmlDoc, int statusCode = 500, int statusSubCode = -1)
    {
        // Attempt to retrieve the RequestVariables object from the context
        RequestVariables reqVars;
        try
        {
            var context = Context.Current;
            reqVars = RetrieveRequestVariables(context);
            reqVars.returnType = returnType;
        }
        catch (Exception ex)
        {
            appLogger.LogInformation(ex, "Could not retrieve context for HandleError()");
            reqVars = new RequestVariables
            {
                returnType = returnType
            };
        }

        string? message;
        string? debugInfo = null;

        try
        {
            if (XmlContainsError(xmlDoc))
            {
                message = xmlDoc.SelectSingleNode("/error/message")?.InnerText;
                debugInfo = xmlDoc.SelectSingleNode("/error/debuginfo")?.InnerText;
                var nodeHttpResponse = xmlDoc.SelectSingleNode("/error/httpresponse");
                if (nodeHttpResponse != null)
                {
                    debugInfo += $", httpresponse: {nodeHttpResponse.InnerText}";
                }
            }
            else
            {
                message = xmlDoc.OuterXml;
            }
        }
        catch (Exception)
        {
            message = xmlDoc.OuterXml;
        }

        HandleError(reqVars, message, debugInfo, statusCode, statusSubCode);
    }

    /// <summary>
    /// Handles an error by trowing an HttpStatusCodeException which invokes the middelware to render the appropriate response to the client
    /// </summary>
    /// <param name="returnMessage"></param>
    /// <param name="statusCode"></param>
    /// <param name="statusSubCode"></param>
    public static void HandleError(TaxxorReturnMessage returnMessage, int statusCode = 500, int statusSubCode = -1)
    {
        // Attempt to retrieve the RequestVariables object from the context
        RequestVariables reqVars;
        try
        {
            var context = System.Web.Context.Current;
            reqVars = RetrieveRequestVariables(context);
        }
        catch (Exception ex)
        {
            appLogger.LogInformation(ex, "Could not retrieve context for HandleError()");
            reqVars = new RequestVariables
            {
                returnType = ReturnTypeEnum.Txt
            };
        }

        HandleError(reqVars.returnType, returnMessage, statusCode, statusSubCode);
    }

    /// <summary>
    /// Handles an error by trowing an HttpStatusCodeException which invokes the middelware to render the appropriate response to the client
    /// Utility version that allows explicit setting of the type of response that we need to return
    /// </summary>
    /// <param name="returnType"></param>
    /// <param name="returnMessage"></param>
    /// <param name="statusCode"></param>
    /// <param name="statusSubCode"></param>
    public static void HandleError(ReturnTypeEnum returnType, TaxxorReturnMessage returnMessage, int statusCode = 500, int statusSubCode = -1)
    {
        // Attempt to retrieve the RequestVariables object from the context
        RequestVariables reqVars;
        try
        {
            var context = System.Web.Context.Current;
            reqVars = RetrieveRequestVariables(context);
            reqVars.returnType = returnType;
        }
        catch (Exception ex)
        {
            appLogger.LogInformation(ex, "Could not retrieve context for HandleError()");
            reqVars = new RequestVariables
            {
                returnType = returnType
            };
        }

        HandleError(reqVars, returnMessage, statusCode, statusSubCode);
    }

    /// <summary>
    /// Handles an error by trowing an HttpStatusCodeException which invokes the middelware to render the appropriate response to the client
    /// </summary>
    /// <param name="reqVars"></param>
    /// <param name="returnMessage"></param>
    /// <param name="statusCode"></param>
    /// <param name="statusSubCode"></param>
    public static void HandleError(RequestVariables reqVars, TaxxorReturnMessage returnMessage, int statusCode = 500, int statusSubCode = -1)
    {
        string? message;
        string? debugInfo;

        if (returnMessage.Success)
        {
            message = "Error incorrectly handled because the return message was a success";
            debugInfo = returnMessage.ToString();
        }
        else
        {
            message = returnMessage.Message;
            debugInfo = returnMessage.DebugInfo;
            if (!string.IsNullOrEmpty(returnMessage.Payload))
            {
                debugInfo = $", payload (truncated): {TruncateString(returnMessage.Payload, 512)}";
            }
        }

        HandleError(reqVars, message, debugInfo, statusCode, statusSubCode);
    }

    /// <summary>
    /// Handles an error by trowing an HttpStatusCodeException which invokes the middelware to render the appropriate response to the client
    /// Utility version that allows explicit setting of the type of response that we need to return
    /// </summary>
    /// <param name="returnType">Return type.</param>
    /// <param name="message">Message.</param>
    /// <param name="debugInfo">Debug info.</param>
    /// <param name="statusCode">Status code.</param>
    /// <param name="statusSubCode">Status sub code.</param>
    public static void HandleError(ReturnTypeEnum returnType, string message, string? debugInfo = null, int statusCode = 500, int statusSubCode = -1)
    {
        // Attempt to retrieve the RequestVariables object from the context
        RequestVariables reqVars;
        try
        {
            var context = System.Web.Context.Current;
            reqVars = RetrieveRequestVariables(context);
            reqVars.returnType = returnType;
        }
        catch (Exception ex)
        {
            appLogger.LogInformation(ex, "Could not retrieve context for HandleError()");
            reqVars = new RequestVariables
            {
                returnType = returnType
            };
        }

        HandleError(reqVars, message, debugInfo, statusCode, statusSubCode);
    }

    /// <summary>
    /// Handles an error by trowing an HttpStatusCodeException which invokes the middelware to render the appropriate response to the client
    /// </summary>
    /// <param name="requestVariables">Request variables.</param>
    /// <param name="message">Message.</param>
    /// <param name="debugInfo">Debug info.</param>
    /// <param name="statusCode">Status code.</param>
    /// <param name="statusSubCode">Status sub code.</param>
    public static void HandleError(RequestVariables requestVariables, string message, string? debugInfo = null, int statusCode = 500, int statusSubCode = -1)
    {
        // Throw a custom HttpStatusCodeException that will invoke proper handling of the error in the middleware
        if (statusCode > -1)
        {
            if (statusSubCode > -1)
            {
                throw new HttpStatusCodeException(requestVariables, statusCode, statusSubCode, message, debugInfo);
            }
            else
            {
                throw new HttpStatusCodeException(requestVariables, statusCode, message, debugInfo);
            }
        }
    }

    /// <summary>
    /// Writes an error message to the console.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="debugInfo">Debug info.</param>
    public static void WriteErrorMessageToConsole(string message, string? debugInfo = null)
    {
        string displayMessage = $"{message}";
        if (!string.IsNullOrEmpty(debugInfo)) 
            displayMessage += $", Debuginfo: {debugInfo}";
        appLogger.LogError(displayMessage);
    }

    /// <summary>
    /// Writes an error message to the console.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="ex">Exception to log</param>
    public static void WriteErrorMessageToConsole(string message, Exception ex)
    {
        string displayMessage = $"{message}";
        appLogger.LogError(ex, displayMessage);
    }

    /// <summary>
    /// Generates the path to the error log file of the system
    /// </summary>
    /// <returns></returns>
    public string GenerateOsPathToSystemErrorLog()
    {
        string? filePathOs = CalculateFullPathOs(RetrieveNodeValueIfExists("/configuration/locations/location[@id='error_trace_log']", xmlApplicationConfiguration), "approot");
        return GenerateOsPathToSystemErrorLog(filePathOs);
    }

    /// <summary>
    /// Generates the os path to system error log.
    /// </summary>
    /// <returns>The os path to system error log.</returns>
    /// <param name="filePathOs">File path os.</param>
    public static string GenerateOsPathToSystemErrorLog(string filePathOs)
    {
        filePathOs = filePathOs.Replace("[year]", DateTime.Now.Year.ToString());
        filePathOs = filePathOs.Replace("[week]", GetWeekNumber(DateTime.Now).ToString());
        return filePathOs;
    }

    /// <summary>
    /// Generates an error XML document object
    /// </summary>
    /// <returns>The error xml.</returns>
    /// <param name="message">Message.</param>
    /// <param name="debugInfo">Debug info.</param>
    public static XmlDocument? GenerateErrorXml(string message, string? debugInfo = null)
    {
        if (!string.IsNullOrEmpty(debugInfo))
        {
            return GenerateErrorXml(message, debugInfo, true);
        }
        else
        {
            return GenerateErrorXml(message, null, false);
        }
    }

    /// <summary>
    /// Generates an error XML document object
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="debugInfo">Debugging information (only shown on non-production systems)</param>
    /// <param name="addDebugInfo">Add debug information or not</param>
    /// <returns></returns>
    public static XmlDocument? GenerateErrorXml(string message, string? debugInfo, bool addDebugInfo = false)
    {
        var templateId = "error_xml";
        XmlDocument? xmlError = RetrieveTemplate(templateId);

        if (xmlError == null)
        {
            appLogger.LogWarning($"Could not find XML template for '{templateId}', error-message: {message}, error-debuginfo: {debugInfo}, stack-trace: {GetStackTrace()}");
        }
        else
        {
            //fill the document
            xmlError.SelectSingleNode("/error/message").InnerText = message;
            if (addDebugInfo)
            {
                if (string.IsNullOrEmpty(debugInfo))
                {
                    xmlError.SelectSingleNode("/error/debuginfo").InnerText = "";
                }
                else
                {
                    xmlError.SelectSingleNode("/error/debuginfo").InnerText = debugInfo;
                }
            }
            else
            {
                RemoveXmlNode(xmlError.SelectSingleNode("/error/debuginfo"));
            }
        }

        //return the document
        return xmlError;
    }

    /// <summary>
    /// Generates an error dynamic object
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="debugInfo">Debugging information (only shown on non-production systems)</param>
    /// <param name="addDebugInfo">Add debug information or not</param>
    /// <returns></returns>
    public static dynamic GenerateErrorObject(string message, string debugInfo, bool addDebugInfo = false)
    {
        dynamic obj = new ExpandoObject();
        obj.error = new ExpandoObject();
        obj.error.message = message;
        if (addDebugInfo) obj.error.debuginfo = debugInfo;

        return obj;
    }

    /// <summary>
    /// Renders the error response.
    /// </summary>
    /// <returns>The error response.</returns>
    /// <param name="message">Message.</param>
    /// <param name="debugInfo">Debug info.</param>
    /// <param name="returnType">Return type.</param>
    public static string RenderErrorResponse(string message, string debugInfo, ReturnTypeEnum returnType = ReturnTypeEnum.Txt)
    {
        XmlDocument? xmlError;

        string errorResponseToClient;
        switch (returnType)
        {
            case ReturnTypeEnum.Xml:
            case ReturnTypeEnum.Json:
                errorResponseToClient = GenerateErrorXml(message, debugInfo).InnerXml;

                if (returnType == ReturnTypeEnum.Json)
                {
                    XmlDocument xmlErrorCustom = new XmlDocument();
                    xmlErrorCustom.LoadXml(errorResponseToClient);
                    errorResponseToClient = WrapJsonPIfNeeded(ConvertToJson(xmlErrorCustom), null);
                }

                break;

            case ReturnTypeEnum.Html:
                xmlError = RetrieveTemplate("error_html");
                var htmlErrorMessage = message + ((!string.IsNullOrEmpty(debugInfo)) ? $"<div class=\"debuginfo\">{Environment.NewLine} {debugInfo}</div>" : "");
                if (xmlError != null)
                {
                    errorResponseToClient = xmlError.InnerXml;
                    errorResponseToClient = errorResponseToClient.Replace("[message]", htmlErrorMessage);
                }
                else
                {
                    errorResponseToClient = $@"<html><body>{htmlErrorMessage}</body></html>";
                }

                break;

            case ReturnTypeEnum.Js:
                errorResponseToClient = $@"/* {message}{((!string.IsNullOrEmpty(debugInfo)) ? ", " + debugInfo : "")} */";
                break;

            case ReturnTypeEnum.Txt:
                errorResponseToClient = message;
                if (!string.IsNullOrEmpty(debugInfo))
                {
                    errorResponseToClient += Environment.NewLine + debugInfo;
                }
                break;

            default:
                errorResponseToClient = message;
                if (!string.IsNullOrEmpty(debugInfo))
                {
                    errorResponseToClient += "<br/>" + debugInfo;
                }
                break;
        }

        return errorResponseToClient;
    }

    /// <summary>
    /// Function to check if Xml result contains error.
    /// </summary>
    /// <param name="xml"></param>
    /// <param name="xpath"></param>
    /// <returns></returns>
    public static bool XmlContainsError(XmlDocument? xml, string? xpath = null)
    {
        if (xml == null) return true;

        string? error = RetrieveNodeValueIfExists(xpath ?? "/error", xml);

        if (error != null)
        {
            return true;
        }
        else
        {
            //in case of a soap envelope that is returned we need to have a bit more investigation
            if (xml.OuterXml.Contains(":Envelope"))
            {
                //in this case search for common error indications using simple text searches
                if (xml.OuterXml.ToLower().Contains("error")) return true;
            }

            //special cases
            if (xml.OuterXml.ToLower().Contains("<message>invalid request")) return true;

            // For a result from a framework CMD command
            if (!string.IsNullOrEmpty(RetrieveNodeValueIfExists("/result/error", xml))) return true;
        }

        return false;
    }

    /// <summary>
    /// Converts an error XML to a string
    /// </summary>
    /// <param name="xmlError"></param>
    /// <param name="returnType"></param>
    /// <returns></returns>
    public static string? ConvertErrorXml(XmlDocument xmlError, ReturnTypeEnum returnType = ReturnTypeEnum.None)
    {
        string? message = xmlError.SelectSingleNode("/error/message")?.InnerText ?? xmlError.SelectSingleNode("/result/message")?.InnerText;
        string? debugInfo = xmlError.SelectSingleNode("/error/debuginfo")?.InnerText ?? xmlError.SelectSingleNode("/result/debuginfo")?.InnerText;
        if (message == null && debugInfo == null)
        {
            return null;
        }
        else
        {
            if (siteType != "local") debugInfo = null;
            string? content;
            switch (returnType)
            {
                case ReturnTypeEnum.Html:
                    content = $"<h2>{message}</h2>";
                    if (debugInfo != null) content += $"<p>Details: {debugInfo}</p>";
                    break;
                default:
                    content = message;
                    if (debugInfo != null) content += $", debuginfo: {debugInfo}";
                    break;
            }
            return content;
        }


    }

    /// <summary>
    /// This method returns a string value containing stack information of the location from which will be logged. 
    /// </summary>
    /// <returns>The stack trace.</returns>
    public static string GetStackTrace()
    {
        return CreateStackInfo(2);
    }

    /// <summary>
    /// This method returns a string value containing stack information of the location from which will be logged. 
    /// </summary>
    /// <param name="skipFrames"></param>
    /// <returns></returns>
    public static string CreateStackInfo(int skipFrames = 1)
    {
        try
        {
            StringBuilder stackTraceInfo = new StringBuilder();
            stackTraceInfo.Append(Environment.NewLine);

            // Start at 2. 1 for this one and another for the one above that.
            StackTrace stackTrace = new StackTrace(skipFrames, true);

            // StackFrame sf = stackTrace.GetFrame(1);
            // sf.GetMethod().Name;
            var firstStackFrameIsInMiddleware = false;
            var stopLogging = false;

            var stackCounter = 0;
            foreach (StackFrame stackFrame in stackTrace.GetFrames())
            {
                string? fileName = stackFrame.GetFileName();
                if (fileName != null)
                {
                    string methodName = "null";
                    int lineNumber = -1;

                    try
                    {
                        methodName = stackFrame.GetMethod()?.Name ?? "";
                    }
                    catch (Exception) { }

                    try
                    {
                        lineNumber = stackFrame.GetFileLineNumber();
                    }
                    catch (Exception) { }


                    fileName = fileName.Substring(fileName.LastIndexOf('\\') + 1);

                    if (fileName.Contains("/middleware/") && stackCounter == 0) firstStackFrameIsInMiddleware = true;
                    if (!firstStackFrameIsInMiddleware && fileName.Contains("/middleware/"))
                    {
                        stopLogging = true;
                    }

                    if (stopLogging) continue;

                    // Add log information
                    stackTraceInfo.Append($"File: {fileName}, line: {lineNumber}, source: {methodName}()");
                    stackTraceInfo.Append(Environment.NewLine);

                    stackCounter++;
                }
            }

            return stackTraceInfo.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: There was an error retrieving the stack trace. error: {ex}";
        }
    }

}