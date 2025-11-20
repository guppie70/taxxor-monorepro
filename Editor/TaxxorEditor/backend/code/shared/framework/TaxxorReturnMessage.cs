using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Xml;
using Microsoft.Extensions.Logging;

/// <summary>
/// Defines standard object that is used to standardize the information that a method can return
/// </summary>
public abstract partial class Framework
{

    /// <summary>
    /// Standardizes typical information that a Taxxor method would return
    /// </summary>
    public class TaxxorReturnMessage
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string? DebugInfo { get; set; } = "";
        public XmlDocument? XmlPayload { get; set; } = null;
        public string Payload { get; set; } = "";

        /// <summary>
        /// Initializes a new empty instance of the <see cref="T:Framework.TaxxorReturnMessage"/> class.
        /// </summary>
        public TaxxorReturnMessage()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Framework.TaxxorReturnMessage"/> class.
        /// </summary>
        /// <param name="success">Indicate if the process completed successfully</param>
        /// <param name="message">Message.</param>
        /// <param name="debugInfo">Debug info.</param>
        public TaxxorReturnMessage(bool success, string message, string? debugInfo = "")
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = debugInfo;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Framework.TaxxorReturnMessage"/> class.
        /// </summary>
        /// <param name="success">Indicate if the process completed successfully</param>
        /// <param name="message">Message to return</param>
        /// <param name="ex">Exception that occured</param>
        public TaxxorReturnMessage(bool success, string message, Exception ex)
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = ex.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Framework.TaxxorReturnMessage"/> class.
        /// </summary>
        /// <param name="success">Indicate if the process completed successfully</param>
        /// <param name="message">Message to return</param>
        /// <param name="xmlPayload">XmlDocument to return</param>
        /// <param name="debugInfo">Debug information</param>
        public TaxxorReturnMessage(bool success, string message, XmlDocument xmlPayload, string debugInfo = "")
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = debugInfo;
            this.XmlPayload = xmlPayload;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Framework.TaxxorReturnMessage"/> class.
        /// </summary>
        /// <param name="success">Indicate if the process completed successfully</param>
        /// <param name="message">Message to return</param>
        /// <param name="payload">String as format to return additional information</param>
        /// <param name="debugInfo">Debug information</param>
        public TaxxorReturnMessage(bool success, string message, string payload, string debugInfo = "")
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = debugInfo;
            this.Payload = payload;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Framework.TaxxorReturnMessage"/> class by trying to convert the XmlDocument to <see langword="await"/> TaxxorReturnMessage.
        /// </summary>
        /// <param name="xmlDocument">Xml document.</param>
        public TaxxorReturnMessage(XmlDocument xmlDocument)
        {
            this._convertXmlToProperties(xmlDocument);
        }

        /// <summary>
        /// Converts a TaxxorReturnMessage to a default XmlDocument response
        /// </summary>
        /// <returns></returns>
        public virtual XmlDocument ToXml()
        {
            return _convertPropertiesToXml();
        }

        /// <summary>
        /// Converts a TaxxorReturnMessage to a JSON formatted string
        /// </summary>
        /// <returns></returns>
        public virtual string ToJson()
        {
            return ConvertToJson(this.ToXml());
        }

        /// <summary>
        /// Converts a TaxxorReturnMessage to a string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (this.Success)
            {
                return $"success: {this.Success}, message: {this.Message ?? "null"}, debuginfo: {this.DebugInfo ?? "null"}, payload: {this.Payload ?? "null"}, xmlpayload: {this.XmlPayload?.OuterXml ?? "null"}";
            }
            else
            {
                return $"success: {this.Success}, message: {this.Message ?? "null"}, debuginfo: {this.DebugInfo ?? "null"}, payload: {this.Payload ?? "null"}, xmlpayload: {this.XmlPayload?.OuterXml ?? "null"}, stack-trace: {GetStackTrace()}";
            }
        }

        public string ToString(string seperator)
        {
            if (seperator == "") seperator = ", ";
            if (this.Success)
            {
                return $"success: {this.Success}{seperator}message: {this.Message ?? "null"}{seperator}debuginfo: {this.DebugInfo ?? "null"}{seperator}payload: {this.Payload ?? "null"}{seperator}xmlpayload: {this.XmlPayload?.OuterXml ?? "null"}";
            }
            else
            {
                return $"success: {this.Success}{seperator}message: {this.Message ?? "null"}{seperator}debuginfo: {this.DebugInfo ?? "null"}{seperator}payload: {this.Payload ?? "null"}{seperator}xmlpayload: {this.XmlPayload?.OuterXml ?? "null"}{seperator}stack-trace: {GetStackTrace()}";
            }
        }

        /// <summary>
        /// Converts the properties of this class to a standard XML Document
        /// </summary>
        /// <returns></returns>
        private protected XmlDocument _convertPropertiesToXml()
        {
            if (this.Success)
            {
                if (this.XmlPayload == null && string.IsNullOrEmpty(this.Payload))
                {
                    return GenerateSuccessXml(this.Message, this.DebugInfo);
                }
                else
                {
                    if (this.XmlPayload == null)
                    {
                        // Use the string based payload
                        return GenerateSuccessXml(this.Message, this.DebugInfo, this.Payload);
                    }
                    else
                    {
                        // Use the XmlDocument based payload
                        return GenerateSuccessXml(this.Message, this.DebugInfo, HttpUtility.HtmlEncode(this.XmlPayload.OuterXml));
                    }
                }
            }
            else
            {
                return GenerateErrorXml(this.Message, this.DebugInfo);
            }
        }


        /// <summary>
        /// Inpects an XmlDocument and retrieves the properties for this class
        /// </summary>
        /// <param name="xmlDocument"></param>
        private protected void _convertXmlToProperties(XmlDocument xmlDocument)
        {
            if (XmlContainsError(xmlDocument))
            {
                this.Success = false;
                this.Message = xmlDocument.SelectSingleNode("/error/message").InnerText;
                var nodeDebugInfo = xmlDocument.SelectSingleNode("/error/debuginfo");
                if (nodeDebugInfo != null) this.DebugInfo = nodeDebugInfo.InnerText;

                this._addHttpResponseInformation(xmlDocument);
            }
            else
            {
                // Test if we are dealing with a standard message that we can properly parse
                var nodeMessage = xmlDocument.SelectSingleNode("/result/message");
                if (nodeMessage == null)
                {
                    HandleError("Unsupported format to process", $"XML: {xmlDocument.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    this.Success = true;
                    this.Message = nodeMessage.InnerText;
                    var nodeDebugInformation = xmlDocument.SelectSingleNode("/result/debuginfo");
                    if (nodeDebugInformation != null)
                    {
                        this.DebugInfo = nodeDebugInformation.InnerText;
                    }
                    var nodePayload = xmlDocument.SelectSingleNode("/result/payload");
                    if (nodePayload != null)
                    {
                        var payload = nodePayload.InnerText;

                        if (payload.IsHtmlEncoded())
                        {
                            payload = HttpUtility.HtmlDecode(payload);
                        }

                        if (payload.IsXml())
                        {
                            try
                            {
                                this.XmlPayload = new XmlDocument();
                                this.XmlPayload.LoadXml(payload);
                            }
                            catch (Exception ex)
                            {
                                this.Payload = payload;
                                appLogger.LogWarning($"There was an error converting the XML payload. payload: {TruncateString(payload, 1000)}, error: {ex}, stack-trace: {GetStackTrace()}");
                            }
                        }
                        else
                        {
                            this.Payload = payload;
                        }
                    }

                    // this._addHttpResponseInformation(xmlDocument);
                }
            }
        }

        /// <summary>
        /// Adds information potentially returned from an API call to the payload
        /// </summary>
        /// <param name="xmlDocument"></param>
        private protected void _addHttpResponseInformation(XmlDocument xmlDocument)
        {
            // Sometimes the XML passed to this function is the result of an HTTP call and important information needs to be added which is located in the httpresponse node
            var nodeHttpResponse = xmlDocument.SelectSingleNode("//httpresponse");
            if (nodeHttpResponse != null)
            {
                var httpResponseText = nodeHttpResponse.InnerText;
                if (httpResponseText.IsXml())
                {
                    // Attempt to decode and parse the result as XML
                    try
                    {
                        var xmlHttpResponse = new XmlDocument();
                        xmlHttpResponse.LoadXml(httpResponseText);
                        AppendToPayload($"httpresponse message: {xmlHttpResponse.SelectSingleNode("//message")?.InnerText ?? "unknown"}, httpresponse debuginfo: {xmlHttpResponse.SelectSingleNode("//debuginfo")?.InnerText ?? "Unknown"}");
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogWarning($"Could not convert the httpresponse information to xml. error: {ex}");
                        AppendToPayload($"httpresponse: {httpResponseText}");
                    }
                }
                else
                {
                    if (httpResponseText.Trim().StartsWith("&lt;"))
                    {
                        // Attempt to decode and parse the result as XML
                        try
                        {
                            var xmlHttpResponse = new XmlDocument();
                            xmlHttpResponse.LoadXml(HttpUtility.HtmlDecode(httpResponseText));
                            AppendToPayload($"httpresponse message: {xmlHttpResponse.SelectSingleNode("//message")?.InnerText ?? "unknown"}, httpresponse debuginfo: {xmlHttpResponse.SelectSingleNode("//debuginfo")?.InnerText ?? "Unknown"}");
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogWarning($"Could not convert the httpresponse information to xml. error: {ex}");
                            AppendToPayload($"httpresponse: {httpResponseText}");
                        }
                    }
                    else
                    {
                        this.Payload += $"httpresponse: {httpResponseText}";
                    }
                }

                void AppendToPayload(string text)
                {
                    if (string.IsNullOrEmpty(this.Payload))
                    {
                        this.Payload = text;
                    }
                    else { 
                        this.Payload += $", {text}";
                    }
                }
            }


        }

    }



    /// <summary>
    /// Special type of return object that contains logging information
    /// </summary>
    public class TaxxorLogReturnMessage : TaxxorReturnMessage
    {
        public List<string> SuccessLog { get; set; } = new List<string>();
        public List<string> WarningLog { get; set; } = new List<string>();
        public List<string> ErrorLog { get; set; } = new List<string>();
        public List<string> InformationLog { get; set; } = new List<string>();

        /// <summary>
        /// Create an instance with logging information
        /// </summary>
        public TaxxorLogReturnMessage()
        {
            this.XmlPayload = new XmlDocument();
        }

        public TaxxorLogReturnMessage(bool success, string message, LogInfo logInfo, string debugInfo = "")
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = debugInfo;

            Append(logInfo);
        }

        public TaxxorLogReturnMessage(bool success, string message, XmlDocument xmlPayload, LogInfo logInfo, string debugInfo = "")
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = debugInfo;
            this.XmlPayload = xmlPayload;

            Append(logInfo);
        }

        /// <summary>
        /// Create an instance with logging information
        /// </summary>
        /// <param name="success"></param>
        /// <param name="message"></param>
        /// <param name="xmlPayload"></param>
        /// <param name="successLog"></param>
        /// <param name="warningLog"></param>
        /// <param name="errorLog"></param>
        /// <param name="debugInfo"></param>
        public TaxxorLogReturnMessage(bool success, string message, XmlDocument xmlPayload, List<string> successLog, List<string> warningLog, List<string> errorLog, string debugInfo = "")
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = debugInfo;
            this.XmlPayload = xmlPayload;
            if (successLog != null) this.SuccessLog.AddRange(successLog);
            if (warningLog != null) this.WarningLog.AddRange(warningLog);
            if (errorLog != null) this.ErrorLog.AddRange(errorLog);
        }

        /// <summary>
        /// Create an instance with logging information
        /// </summary>
        /// <param name="success"></param>
        /// <param name="message"></param>
        /// <param name="successLog"></param>
        /// <param name="warningLog"></param>
        /// <param name="errorLog"></param>
        /// <param name="debugInfo"></param>
        public TaxxorLogReturnMessage(bool success, string message, List<string> successLog, List<string> warningLog, List<string> errorLog, string debugInfo = "")
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = debugInfo;
            if (successLog != null) this.SuccessLog.AddRange(successLog);
            if (warningLog != null) this.WarningLog.AddRange(warningLog);
            if (errorLog != null) this.ErrorLog.AddRange(errorLog);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Framework.WebsocketReturnMessage"/> class.
        /// </summary>
        /// <param name="success">Indicate if the process completed successfully</param>
        /// <param name="message">Message to return</param>
        /// <param name="debugInfo">Debug information</param>
        public TaxxorLogReturnMessage(bool success, string message, string debugInfo = "")
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = debugInfo;
        }

        public TaxxorLogReturnMessage(bool success, string message, TaxxorLogReturnMessage taxxorLogMessage, string debugInfo = "")
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = debugInfo;

            // Retrieve information from the taxxor log message that was passed
            _parseTaxxorLogMessageInformation(taxxorLogMessage);
        }

        public TaxxorLogReturnMessage(bool success, string message, Exception ex, TaxxorLogReturnMessage taxxorLogMessage = null)
        {
            this.Success = success;
            this.Message = message;
            this.DebugInfo = ex.ToString();

            // Retrieve information from the taxxor log message that was passed
            if (taxxorLogMessage != null)
            {
                _parseTaxxorLogMessageInformation(taxxorLogMessage);
            }
        }

        public TaxxorLogReturnMessage(TaxxorReturnMessage taxxorReturnMessage)
        {
            this.Success = taxxorReturnMessage.Success;
            this.Message = taxxorReturnMessage.Message;
            this.DebugInfo = taxxorReturnMessage.DebugInfo;
            if (taxxorReturnMessage.XmlPayload != null) this.XmlPayload = taxxorReturnMessage.XmlPayload;
            if (!string.IsNullOrEmpty(taxxorReturnMessage.Payload)) this.Payload = taxxorReturnMessage.Payload;
        }

        public TaxxorLogReturnMessage(TaxxorReturnMessage taxxorReturnMessage, List<string> successLog, List<string> warningLog, List<string> errorLog)
        {
            this.Success = taxxorReturnMessage.Success;
            this.Message = taxxorReturnMessage.Message;
            this.DebugInfo = taxxorReturnMessage.DebugInfo;
            if (taxxorReturnMessage.XmlPayload != null) this.XmlPayload = taxxorReturnMessage.XmlPayload;
            if (!string.IsNullOrEmpty(taxxorReturnMessage.Payload)) this.Payload = taxxorReturnMessage.Payload;
            if (successLog != null) this.SuccessLog.AddRange(successLog);
            if (warningLog != null) this.WarningLog.AddRange(warningLog);
            if (errorLog != null) this.ErrorLog.AddRange(errorLog);
        }

        public TaxxorLogReturnMessage(XmlDocument xmlDocument)
        {
            this._convertXmlToProperties(xmlDocument);

            // Retrieve the logs from the XML
            var nodeLogRoot = xmlDocument.SelectSingleNode("//logs");
            if (nodeLogRoot != null)
            {
                var nodeSuccessRoot = nodeLogRoot.SelectSingleNode("success");
                if (nodeSuccessRoot != null)
                {
                    var nodeListEntries = nodeSuccessRoot.SelectNodes("entry");
                    foreach (XmlNode nodeEntry in nodeListEntries)
                    {
                        this.SuccessLog.Add(nodeEntry.InnerText);
                    }
                }

                var nodeWarningRoot = nodeLogRoot.SelectSingleNode("warning");
                if (nodeWarningRoot != null)
                {
                    var nodeListEntries = nodeWarningRoot.SelectNodes("entry");
                    foreach (XmlNode nodeEntry in nodeListEntries)
                    {
                        this.WarningLog.Add(nodeEntry.InnerText);
                    }
                }

                var nodeErrorRoot = nodeLogRoot.SelectSingleNode("error");
                if (nodeErrorRoot != null)
                {
                    var nodeListEntries = nodeErrorRoot.SelectNodes("entry");
                    foreach (XmlNode nodeEntry in nodeListEntries)
                    {
                        this.ErrorLog.Add(nodeEntry.InnerText);
                    }
                }

                var nodeInfoRoot = nodeLogRoot.SelectSingleNode("info");
                if (nodeErrorRoot != null)
                {
                    var nodeListEntries = nodeInfoRoot.SelectNodes("entry");
                    foreach (XmlNode nodeEntry in nodeListEntries)
                    {
                        this.InformationLog.Add(nodeEntry.InnerText);
                    }
                }
            }

        }

        public TaxxorLogReturnMessage(XmlDocument xmlDocument, List<string> successLog, List<string> warningLog, List<string> errorLog)
        {
            this._convertXmlToProperties(xmlDocument);
            if (successLog != null) this.SuccessLog.AddRange(successLog);
            if (warningLog != null) this.WarningLog.AddRange(warningLog);
            if (errorLog != null) this.ErrorLog.AddRange(errorLog);
        }

        public override XmlDocument ToXml()
        {
            var xmlDoc = this._convertPropertiesToXml();

            // Add the logs to the generated XML
            if (this.SuccessLog.Count > 0 || this.WarningLog.Count > 0 || this.ErrorLog.Count > 0 || this.InformationLog.Count > 0)
            {
                var nodeLogRoot = xmlDoc.CreateElement("logs");

                if (this.SuccessLog.Count > 0)
                {
                    var nodeSuccessLogRoot = xmlDoc.CreateElement("success");
                    foreach (var entry in this.SuccessLog)
                    {
                        var nodeEntry = xmlDoc.CreateElementWithText("entry", entry);
                        nodeSuccessLogRoot.AppendChild(nodeEntry);
                    }
                    nodeLogRoot.AppendChild(nodeSuccessLogRoot);
                }

                if (this.WarningLog.Count > 0)
                {
                    var nodeWarningLogRoot = xmlDoc.CreateElement("warning");
                    foreach (var entry in this.WarningLog)
                    {
                        var nodeEntry = xmlDoc.CreateElementWithText("entry", entry);
                        nodeWarningLogRoot.AppendChild(nodeEntry);
                    }
                    nodeLogRoot.AppendChild(nodeWarningLogRoot);
                }

                if (this.ErrorLog.Count > 0)
                {
                    var nodeWarningLogRoot = xmlDoc.CreateElement("error");
                    foreach (var entry in this.ErrorLog)
                    {
                        var nodeEntry = xmlDoc.CreateElementWithText("entry", entry);
                        nodeWarningLogRoot.AppendChild(nodeEntry);
                    }
                    nodeLogRoot.AppendChild(nodeWarningLogRoot);
                }

                if (this.InformationLog.Count > 0)
                {
                    var nodeInfoLogRoot = xmlDoc.CreateElement("info");
                    foreach (var entry in this.InformationLog)
                    {
                        var nodeEntry = xmlDoc.CreateElementWithText("entry", entry);
                        nodeInfoLogRoot.AppendChild(nodeEntry);
                    }
                    nodeLogRoot.AppendChild(nodeInfoLogRoot);
                }

                xmlDoc.DocumentElement.AppendChild(nodeLogRoot);
            }

            return xmlDoc;
        }
        /// <summary>
        /// Appends a generic loginfo instance information to the log message
        /// </summary>
        /// <param name="logInfo"></param>
        public void Append(LogInfo logInfo)
        {
            if (logInfo.SuccessLog.Count > 0) this.SuccessLog.AddRange(logInfo.SuccessLog);
            if (logInfo.WarningLog.Count > 0) this.WarningLog.AddRange(logInfo.WarningLog);
            if (logInfo.ErrorLog.Count > 0) this.ErrorLog.AddRange(logInfo.ErrorLog);
            if (logInfo.InformationLog.Count > 0) this.InformationLog.AddRange(logInfo.InformationLog);
        }


        public string LogToString()
        {
            var overallResult = new StringBuilder();
            if (this.Success)
            {
                overallResult.AppendLine("Success");
            }
            else
            {
                overallResult.AppendLine("Failure");
            }

            if (this.InformationLog.Count > 0) overallResult.AppendLine($"InformationLog: {string.Join(',', this.InformationLog.ToArray())}");
            if (this.SuccessLog.Count > 0) overallResult.AppendLine($"SuccessLog: {string.Join(',', this.SuccessLog.ToArray())}");
            if (this.WarningLog.Count > 0) overallResult.AppendLine($"WarningLog: {string.Join(',', this.WarningLog.ToArray())}");
            if (this.ErrorLog.Count > 0) overallResult.AppendLine($"ErrorLog: {string.Join(',', this.ErrorLog.ToArray())}");
            return overallResult.ToString();
        }

        private void _parseTaxxorLogMessageInformation(TaxxorLogReturnMessage taxxorLogMessage)
        {
            if (taxxorLogMessage.XmlPayload != null) this.XmlPayload = taxxorLogMessage.XmlPayload;
            if (!string.IsNullOrEmpty(taxxorLogMessage.Payload)) this.Payload = taxxorLogMessage.Payload;

            if (taxxorLogMessage.SuccessLog.Count > 0) this.SuccessLog.AddRange(taxxorLogMessage.SuccessLog);
            if (taxxorLogMessage.WarningLog.Count > 0) this.WarningLog.AddRange(taxxorLogMessage.WarningLog);
            if (taxxorLogMessage.ErrorLog.Count > 0) this.ErrorLog.AddRange(taxxorLogMessage.ErrorLog);
            if (taxxorLogMessage.InformationLog.Count > 0) this.InformationLog.AddRange(taxxorLogMessage.InformationLog);
        }



    }

    /// <summary>
    /// Generic class for storing log information
    /// </summary>
    public class LogInfo
    {
        public List<string> SuccessLog { get; set; } = new List<string>();
        public List<string> WarningLog { get; set; } = new List<string>();
        public List<string> ErrorLog { get; set; } = new List<string>();
        public List<string> InformationLog { get; set; } = new List<string>();

        private ILogger _logger { get; set; }

        public LogInfo()
        {

            _logger = appLoggerFactory.CreateLogger("");
        }

        public void Append(LogInfo logInfo)
        {
            if (logInfo.SuccessLog.Count > 0) this.SuccessLog.AddRange(logInfo.SuccessLog);
            if (logInfo.WarningLog.Count > 0) this.SuccessLog.AddRange(logInfo.WarningLog);
            if (logInfo.ErrorLog.Count > 0) this.SuccessLog.AddRange(logInfo.ErrorLog);
            if (logInfo.InformationLog.Count > 0) this.SuccessLog.AddRange(logInfo.InformationLog);
        }

        public void AddSuccessLogEntry(string message)
        {
            _logger.LogInformation(message);
            this.SuccessLog.Add(message);
        }

        public void AddWarningLogEntry(string message)
        {
            _logger.LogWarning(message);
            this.WarningLog.Add(message);
        }

        public void AddErrorLogEntry(string message)
        {
            _logger.LogError(message);
            this.ErrorLog.Add(message);
        }

        public void AddErrorLogEntry(Exception ex, string message)
        {
            _logger.LogError(ex, message);
            this.ErrorLog.Add(message);
        }

        public void AddInformationLogEntry(string message)
        {
            _logger.LogInformation(message);
            this.InformationLog.Add(message);
        }

        public string LogToString(ReturnTypeEnum returnType = ReturnTypeEnum.Txt)
        {
            var overallResult = new StringBuilder();

            if (this.InformationLog.Count > 0) overallResult.AppendLine(_logToReturnType("InformationLog", this.InformationLog, returnType));
            if (this.SuccessLog.Count > 0) overallResult.AppendLine(_logToReturnType("SuccessLog", this.SuccessLog, returnType));
            if (this.WarningLog.Count > 0) overallResult.AppendLine(_logToReturnType("WarningLog", this.WarningLog, returnType));
            if (this.ErrorLog.Count > 0) overallResult.AppendLine(_logToReturnType("ErrorLog", this.ErrorLog, returnType));
            return overallResult.ToString();
        }

        /// <summary>
        /// Formats a log list for display
        /// </summary>
        /// <param name="label"></param>
        /// <param name="logEntries"></param>
        /// <param name="returnType"></param>
        /// <returns></returns>
        private string _logToReturnType(string label, List<string> logEntries, ReturnTypeEnum returnType)
        {
            switch (returnType)
            {
                case ReturnTypeEnum.None:
                    return "";

                default:
                    // Render as text
                    return $"{label}: {string.Join("\n* ", logEntries.ToArray())}";
            }
        }

    }

}