using System;
using Newtonsoft.Json.Linq;


/// <summary>
/// Custom error used for purposely thrown errors that need to display a message in a web client
/// </summary>
public class HttpStatusCodeException : Exception
{
    public int StatusCode { get; set; }
    public int StatusSubCode { get; set; } = -1;
    public string? DebugInfo { get; set; } = null;
    public Framework.RequestVariables ReqVars { get; set; }


    private static Framework.RequestVariables _requestVariablesDummy = new Framework.RequestVariables
    {
        returnType = Framework.ReturnTypeEnum.Txt,
        isDebugMode = false
    };




    public HttpStatusCodeException(int statusCode)
    {
        this.ReqVars = _requestVariablesDummy;
        this.StatusCode = statusCode;
    }

    public HttpStatusCodeException(Framework.RequestVariables requestVariables, int statusCode, string message) : base(message)
    {
        this.ReqVars = requestVariables;
        this.StatusCode = statusCode;

    }

    public HttpStatusCodeException(Framework.RequestVariables requestVariables, int statusCode, string message, string? debugInfo) : base(message)
    {
        this.ReqVars = requestVariables;
        this.StatusCode = statusCode;
        if(!string.IsNullOrEmpty(debugInfo))this.DebugInfo = debugInfo;
    }

    public HttpStatusCodeException(Framework.RequestVariables requestVariables, int statusCode, int statusSubCode, string message) : base(message)
    {
        this.ReqVars = requestVariables;
        this.StatusCode = statusCode;
        this.StatusSubCode = statusSubCode;
    }

    public HttpStatusCodeException(Framework.RequestVariables requestVariables, int statusCode, int statusSubCode, string message, string? debugInfo) : base(message)
    {
        this.ReqVars = requestVariables;
        this.StatusCode = statusCode;
        this.StatusSubCode = statusSubCode;
        if (!string.IsNullOrEmpty(debugInfo)) this.DebugInfo = debugInfo;
    }


    public HttpStatusCodeException(int statusCode, Exception inner) : this(_requestVariablesDummy, statusCode, inner.ToString()) { }


    public HttpStatusCodeException(int statusCode, JObject errorObject) : this(_requestVariablesDummy, statusCode, errorObject.ToString()){}
}
