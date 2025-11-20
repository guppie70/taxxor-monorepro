using System;
using System.Xml;
using System.Threading.Tasks;

/// <summary>
/// Contains functions and utilities related to SOAP webservice interaction
/// </summary>
public abstract partial class Framework
{
    /// <summary>
    /// Sends SoapRequest and return XMLDocument as SoapResponse
    /// (Overload method)
    /// </summary>
    /// <param name="webserviceId">ApplicationId in configuration file</param>
    /// <param name="soapMethod">Webservice methodname</param>
    /// <param name="xmlSoapCallData">Parameters to sent to the webservice in XmlDocument format</param>
    /// <param name="applicationConfiguration">XmlDocument containing all configuration data</param>
    /// <returns></returns>
    public static async Task<XmlDocument?> SoapRequest(string webserviceId, string soapMethod, XmlDocument xmlSoapCallData, XmlDocument applicationConfiguration, bool debug = false)
    {
        // overload function
        xmlSoapCallData.PreserveWhitespace = true;
        XmlDocument objXmlSoapEnvelope = new XmlDocument();
        return await SoapRequest(webserviceId, soapMethod, xmlSoapCallData.DocumentElement.OuterXml, objXmlSoapEnvelope, applicationConfiguration, debug);
    }

    /// <summary>
    /// Sends SoapRequest and return XMLDocument as SoapResponse
    /// (Overload method)
    /// </summary>
    /// <param name="webserviceId">ApplicationId in configuration file</param>
    /// <param name="soapMethod">Webservice methodname</param>
    /// <param name="soapCallData">A string containing the (valid) xml data to sent to the webservice</param>
    /// <param name="applicationConfiguration">XmlDocument containing all configuration data</param>
    /// <returns></returns>
    public static async Task<XmlDocument?> SoapRequest(string webserviceId, string soapMethod, string soapCallData, XmlDocument applicationConfiguration)
    {
        // overload function
        XmlDocument objXmlSoapEnvelope = new XmlDocument();
        return await SoapRequest(webserviceId, soapMethod, soapCallData, objXmlSoapEnvelope, applicationConfiguration);
    }

    /// <summary>
    /// Sends SoapRequest and return XMLDocument as SoapResponse
    /// (Overload method)
    /// </summary>
    /// <param name="webserviceId">ApplicationId in configuration file</param>
    /// <param name="soapMethod">Webservice methodname</param>
    /// <param name="xmlSoapCallData">XmlDocument containing Soap data</param>
    /// <param name="applicationConfiguration">XmlDocument containing all configuration data</param>
    /// <returns></returns>
    public static async Task<XmlDocument?> SoapRequest(string webserviceId, string soapMethod, XmlDocument xmlSoapCallData, XmlDocument xmlSoapEnvelope, XmlDocument applicationConfiguration)
    {
        // overload function
        xmlSoapCallData.PreserveWhitespace = true;
        return await SoapRequest(webserviceId, soapMethod, xmlSoapCallData.DocumentElement.OuterXml, xmlSoapEnvelope, applicationConfiguration);
    }

    /// <summary>
    /// Sends SoapRequest and return XMLDocument as SoapResponse
    /// </summary>
    /// <param name="webserviceId"></param>
    /// <param name="soapMethod"></param>
    /// <param name="xmlSoapCallData"></param>
    /// <param name="xmlSoapEnvelope"></param>
    /// <param name="applicationConfiguration"></param>
    /// <param name="debug"></param>
    /// <param name="httpCallTimeout">Optional: Custom timeout for HTTP call (defaults to 240000 ms)</param>
    /// <returns></returns>
    public static async Task<XmlDocument?> SoapRequest(string webserviceId, string soapMethod, XmlDocument xmlSoapCallData, XmlDocument xmlSoapEnvelope, XmlDocument applicationConfiguration, bool debug, int httpCallTimeout = 480000)
    {
        // overload function
        xmlSoapCallData.PreserveWhitespace = true;
        return await SoapRequest(webserviceId, soapMethod, xmlSoapCallData.DocumentElement.OuterXml, xmlSoapEnvelope, applicationConfiguration, debug);
    }




    /// <summary>
    /// Sends SoapRequest and returns XMLDocument as SoapResponse
    /// </summary>
    /// <param name="webserviceId">ApplicationId in configuration file</param>
    /// <param name="soapMethod">Webservice methodname</param>
    /// <param name="strXmlSoapCallData">Xml string containing Soap data</param>
    /// <param name="xmlSoapEnvelope"></param>
    /// <param name="applicationConfiguration">XmlDocument containing all configuration data</param>
    /// <param name="debug">Optional: Boolean indicating if the routine needs to run in debug mode (default is false)</param>
    /// <param name="userName">Optional: Specify a username for webserver authentication (default is null)</param>
    /// <param name="password">Optional: Specify a password for webserver authentication (default is null)</param>
    /// <param name="domain">Optional: Domain of the user to be authenticated (default is null)</param>
    /// <param name="authenticationMethod">Optional: Supported are the strings "NTLM" and "Basic" (default is "Basic")</param>
    /// <param name="httpCallTimeout">Optional: Custom timeout for HTTP call (defaults to 240000 ms)</param>
    /// <returns></returns>
    public static async Task<XmlDocument?> SoapRequest(string webserviceId, string soapMethod, string strXmlSoapCallData, XmlDocument xmlSoapEnvelope, XmlDocument applicationConfiguration, bool debug = false, string? userName = null, string? password = null, string? domain = null, string authenticationMethod = "Basic", int httpCallTimeout = 480000)
    {

        XmlDocument xdXmlSoapResponse = new XmlDocument();
        XmlDocument xmlSoapResponseOrig = new XmlDocument();
        if (!string.IsNullOrEmpty(strXmlSoapCallData))
        {
            try
            {
                xmlSoapEnvelope.LoadXml(strXmlSoapCallData);
            }
            catch (Exception ex)
            {
                xdXmlSoapResponse.LoadXml("<error><message/><debuginfo/><httpresponse/></error>");
                xdXmlSoapResponse.SelectSingleNode("/error/message").InnerText = "Could not load the xmldata provided";
                xdXmlSoapResponse.SelectSingleNode("/error/httpresponse").InnerText = "700";
                xdXmlSoapResponse.SelectSingleNode("/error/debuginfo").InnerText = strXmlSoapCallData + " - " + ex.ToString();
                return xdXmlSoapResponse;
            }

        }
        string soapDataRootNodeName = (((xmlSoapEnvelope).DocumentElement)).LocalName;

        // Declare variables
        string? url = String.Empty, strNamespacePrefix = String.Empty, strNamespaceUri = String.Empty;
        string soapVersion = String.Empty;
        string strLogFileRequestPathOs = String.Empty, strLogFileResponsePathOs = String.Empty;

        XmlDsig oXmlDsig = new XmlDsig(applicationConfiguration, webserviceId);

        // Indicates if we need to embed the passed xml data into a "<data/>" node
        bool wrapParametersInXmlStruct = false;

        // Set to true when we are executing a secure soap call using digital signatures
        bool soapDsgCall = false;
        if (xmlApplicationConfiguration.SelectNodes("/configuration/applications/application[@id='" + webserviceId + "']/security").Count > 0) soapDsgCall = true;

        // For debugging purposes
        #region Debug
        bool bolLogWebservices = false;
        if (debug || url.Contains("logwebrequest=true")) bolLogWebservices = true;

        if (bolLogWebservices)
        {
            strLogFileRequestPathOs = logRootPathOs + "/soap-request_" + soapMethod.ToLower() + ".xml";
            strLogFileResponsePathOs = logRootPathOs + "/soap-response_" + soapMethod.ToLower() + ".xml";
        }

        //Context.Response.Write(" - logRootPathOs=" + logRootPathOs + "<br/>");
        //Context.Response.Write(" - strLogFileRequestPathOs=" + strLogFileRequestPathOs + "<br/>");
        #endregion

        // Retrieve some default info from application configuration
        XmlNodeList? objNodeList = applicationConfiguration.SelectNodes("/configuration/applications/application[@id='" + webserviceId + "']");
        if (objNodeList.Count == 0)
        {
            HandleError("Webservice: " + webserviceId + ", could not be found in the application configuration");
        }
        else
        {
            var xmlNodeList = objNodeList.Item(0).SelectNodes("site_root");
            if (xmlNodeList.Count == 0)
            {
                url = CalculateFullPathOs(objNodeList.Item(0).SelectNodes("settings/setting[@id='webservice_url']"));
            }
            else
            {
                url = CalculateFullPathOs(xmlNodeList.Item(0));
            }
            strNamespacePrefix = objNodeList.Item(0).SelectSingleNode("namespace/@prefix").InnerText;
            strNamespaceUri = objNodeList.Item(0).SelectSingleNode("namespace/@uri").InnerText;
            soapVersion = "1.2";
            if (objNodeList.Item(0).Attributes["soap_version"] != null)
            {
                soapVersion = objNodeList.Item(0).Attributes["soap_version"].Value;
            }

            //check if we need to wrap the xml parameters in an xml structure
            if (soapDsgCall)
            {
                wrapParametersInXmlStruct = true;
            }
            else
            {
                var nodeList = objNodeList.Item(0).SelectNodes("methods/method[@name='" + soapMethod + "']/xml_parameter");
                foreach (XmlNode xmlNode in nodeList)
                {
                    if (xmlNode.Attributes["wrapinsidexml"] != null)
                    {
                        if (xmlNode.Attributes["wrapinsidexml"].Value == "true") wrapParametersInXmlStruct = true;
                    }
                }

                if (!wrapParametersInXmlStruct)
                {
                    nodeList = objNodeList.Item(0).SelectNodes("methods");
                    foreach (XmlNode xmlNode in nodeList)
                    {
                        if (xmlNode.Attributes["wrapinsidexml"] != null)
                        {
                            if (xmlNode.Attributes["wrapinsidexml"].Value == "true") wrapParametersInXmlStruct = true;
                        }
                    }

                }
            }

        }

        //Context.Response.Write(" - strUrl=" + strUrl + "<br/>");
        //Context.Response.Write(" - strNamespacePrefix=" + strNamespacePrefix + "<br/>");
        //Context.Response.Write(" - strLogFileRequestPathOs=" + strLogFileRequestPathOs + "<br/>");
        //Context.Response.Write(" - strSoapVersion=" + strSoapVersion + "<br/>");

        // Check if soap call data is contains complete soap envelope
        // In this case we do not need to build the envelope
        if (soapDataRootNodeName != "Envelope")
        {
            // Load the base soap envelope in an xml object
            xmlSoapEnvelope.PreserveWhitespace = true;
            XmlNamespaceManager nsManagerSoapEnvelopeRequest = new XmlNamespaceManager(xmlSoapEnvelope.NameTable);

            if (soapVersion == "1.2")
            {
                xmlSoapEnvelope.LoadXml("<env:Envelope xmlns:env=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:" + strNamespacePrefix + "=\"" + strNamespaceUri + "\"><env:Body/></env:Envelope>");
                nsManagerSoapEnvelopeRequest.AddNamespace("env", "http://www.w3.org/2003/05/soap-envelope");
            }
            else
            {
                xmlSoapEnvelope.LoadXml("<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:" + strNamespacePrefix + "=\"" + strNamespaceUri + "\"><SOAP-ENV:Header/><SOAP-ENV:Body/></SOAP-ENV:Envelope>");
                nsManagerSoapEnvelopeRequest.AddNamespace("SOAP-ENV", "http://schemas.xmlsoap.org/soap/envelope/");
            }
            nsManagerSoapEnvelopeRequest.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            nsManagerSoapEnvelopeRequest.AddNamespace(strNamespacePrefix, strNamespaceUri);

            // Create the node that specifies the soap method to call
            XmlNode objNodeSoapMethod = xmlSoapEnvelope.CreateElement(strNamespacePrefix, soapMethod, strNamespaceUri);

            // Add the soap method to the soap envelope
            XmlNode? objNodeSoapBody = xmlSoapEnvelope.SelectSingleNode("/env:Envelope/env:Body", nsManagerSoapEnvelopeRequest);
            objNodeSoapBody.AppendChild(objNodeSoapMethod);

            // Create the soap object that contains the xml data that needs to be sent to the server
            XmlDocument objXmlSoapCallData = new XmlDocument();
            //objXmlSoapCallData.PreserveWhitespace = true;
            XmlNamespaceManager nsManagerSoapCall = new XmlNamespaceManager(objXmlSoapCallData.NameTable);
            nsManagerSoapCall.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            nsManagerSoapCall.AddNamespace(strNamespacePrefix, strNamespaceUri);
            String strXmlSoapCallDataFixed = "<" + strNamespacePrefix + ":data xmlns:" + strNamespacePrefix + "=\"" + strNamespaceUri + "\"><xml><data>" + strXmlSoapCallData + "</data></xml></" + strNamespacePrefix + ":data>";
            if (!wrapParametersInXmlStruct) strXmlSoapCallDataFixed = strXmlSoapCallData;
            objXmlSoapCallData.LoadXml(strXmlSoapCallDataFixed);

            // Sign the soap call data
            if (soapDsgCall)
            {
                objXmlSoapCallData = ForceSelfClosingTags(objXmlSoapCallData);
                objXmlSoapCallData = oXmlDsig.XmlSignatureCreate(objXmlSoapCallData);
            }

            // Insert the (signed) soap parameters into the soap envelope
            XmlNode objNodeSoapCallImported = xmlSoapEnvelope.ImportNode(objXmlSoapCallData.DocumentElement, true);
            objNodeSoapBody.FirstChild.AppendChild(objNodeSoapCallImported);
        }

        //if (bolLogWebservices) await objXmlSoapEnvelope.SaveAsync(strLogFileRequestPathOs);

        if (bolLogWebservices)
        {
            await TextFileCreateAsync(xmlSoapEnvelope.OuterXml, strLogFileRequestPathOs);
        }

        // Check if we have been able to construct a URL to the webservice
        if (string.IsNullOrEmpty(url)) return GenerateErrorXml($"Could not render call to remote service", "Path to URL of remote service not defined");

        string? methodPrefix = "";
        var nodeMethodPrefix = objNodeList.Item(0).SelectSingleNode("namespace[@prefix='methodprefix']");
        if (nodeMethodPrefix != null) methodPrefix = GetAttribute(nodeMethodPrefix, "uri");



        // Build the parameters for calling the SOAP service
        var customHttpHeaders = new CustomHttpHeaders
        {
            RequestType = ReturnTypeEnum.Xml,
            SoapVersion = soapVersion
        };
        customHttpHeaders.AddCustomHeader("SOAPAction", methodPrefix + soapMethod);

        // Convert the data we want to post to a format that the HttpClient accepts
        dynamic dataToPost = ConvertRequestDataToPostData(xmlSoapEnvelope, customHttpHeaders);

        // Execute the SOAP request  
        xmlSoapResponseOrig = await RemoteRequest<XmlDocument>(RequestMethodEnum.Post, url, dataToPost, customHttpHeaders, debug, httpCallTimeout);

        if (bolLogWebservices)
        {
            await TextFileCreateAsync(xmlSoapResponseOrig.OuterXml, strLogFileResponsePathOs);
        }


        if (XmlContainsError(xmlSoapResponseOrig)) return xmlSoapResponseOrig;


        // Parse the message that we have received

        // Add the namespaces to the SOAP message we have recieved - this to extract the information we are looking for
        XmlNamespaceManager nsManagerSoapEnvelopeResponse = new XmlNamespaceManager(xmlSoapResponseOrig.NameTable);
        if (soapVersion == "1.2")
        {
            nsManagerSoapEnvelopeResponse.AddNamespace("env", "http://www.w3.org/2003/05/soap-envelope");
        }
        else
        {
            nsManagerSoapEnvelopeResponse.AddNamespace("SOAP-ENV", "http://schemas.xmlsoap.org/soap/envelope/");
        }

        if (soapDsgCall)
        {
            // Validate the signature
            bool bolPassedSignatureValidation = oXmlDsig.XmlSignatureValidate(xmlSoapResponseOrig);

            if (bolPassedSignatureValidation)
            {
                // Strip the soap envelope data from the response
                XmlNode? objNodeData = xmlSoapResponseOrig.SelectSingleNode("//data");
                XmlNode objNodeDataImported = xdXmlSoapResponse.ImportNode(objNodeData, true);
                xdXmlSoapResponse.AppendChild(objNodeDataImported);
            }
            else
            {
                // Failed signature validation
                // Return an xml document with the error message in it
                return GenerateErrorXml("Signature validation error while parsing the response", "WebserviceId: " + webserviceId + ", method: " + soapMethod);
            }
        }
        else
        {
            // Prepare the response
            if (xmlSoapResponseOrig.SelectNodes("//data").Count > 0)
            {
                // Only return the data part of the SOAP envelope
                XmlNode? objNodeData = xmlSoapResponseOrig.SelectSingleNode("//data");
                XmlNode objNodeDataImported = xdXmlSoapResponse.ImportNode(objNodeData, true);
                xdXmlSoapResponse.AppendChild(objNodeDataImported);
            }
            else
            {
                // Strip any namespace from the response
                XmlDocument xmlResponseWithoutNs = StripNameSpaces(xmlSoapResponseOrig);

                // Fallback - return the complete data inside the SOAP envelope
                if (xmlResponseWithoutNs.DocumentElement.ChildNodes.Count == 1)
                {
                    XmlNode? objNodeData = xmlResponseWithoutNs.DocumentElement.ChildNodes.Item(0);
                    XmlNode objNodeDataImported = xdXmlSoapResponse.ImportNode(objNodeData, true);
                    xdXmlSoapResponse.AppendChild(objNodeDataImported);
                }
                else
                {
                    XmlNode? objNodeData = xmlResponseWithoutNs.DocumentElement.ChildNodes.Item(1);
                    XmlNode objNodeDataImported = xdXmlSoapResponse.ImportNode(objNodeData, true);
                    xdXmlSoapResponse.AppendChild(objNodeDataImported);
                }
            }
        }

        return xdXmlSoapResponse;


    }



    /// <summary>
    /// Utility that tests the response from the webservice call utility ('SoapRequest()') to see if there was an internal error or not (for example a 500 response from the SOAP server)
    /// </summary>
    /// <param name="xmlSoapResponse">The XmlDocument as returned by the SoapRequest() utility</param>
    /// <returns></returns>
    public static bool IsWebserviceCallSuccesfull(XmlDocument xmlSoapResponse)
    {
        if (xmlSoapResponse.DocumentElement.LocalName.ToString() == "error")
        {
            return false;
        }
        return true;
    }

}

