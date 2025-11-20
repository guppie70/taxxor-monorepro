using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;


/// <summary>
/// Contains helper functions for working with XML
/// </summary>
public abstract partial class Framework
{
    /* fixformat ignore:start */
#nullable enable
    /* fixformat ignore:end */


    /// <summary>
    /// Regular Expression for testing if we want to find an attribute value using xPath
    /// </summary>
    private static Regex _regexAttributeXpath = new(@"^(.*)/(@)(.*)$");

    /// <summary>
    /// Retrieves a nodelist from an xml document using namespaces based on the given Xpath expression. Returns null if no match can be found.
    /// </summary>
    /// <param name="xpath">Xpath expression</param>
    /// <param name="xmlDocument">XmlDocument to retrieve the value from</param>
    /// <param name="nameSpaces">Array of namespaces that is required to execute the xpath query</param>
    /// <returns></returns>
    public static XmlNodeList? RetrieveNodes(string xpath, XmlDocument xmlDocument, String[] nameSpaces)
    {
        string nameSpace;
        string nameSpaceValue;

        try
        {
            XmlNamespaceManager xmlNamespaceManager = new(xmlDocument.NameTable);

            if (nameSpaces != null)
            {
                foreach (String s in nameSpaces)
                {
                    String[] temp = s.Split(Char.Parse("#"));
                    if (temp.Length == 2)
                    {
                        nameSpace = temp[0];
                        nameSpaceValue = temp[1];

                        xmlNamespaceManager.AddNamespace(nameSpace, nameSpaceValue);
                    }
                }
            }
            //xmlNamespaceManager.AddNamespace("xi", "http://www.w3.org/2003/XInclude");

            return xmlDocument.SelectNodes(xpath, xmlNamespaceManager);
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, "Exception occurred in RetrieveNodes()");
            return null;
        }
    }

    /// <summary>
    /// Retrieves a value from an xml document based on the given Xpath expression. Returns null id no match can be found.
    /// </summary>
    /// <param name="xpath">Xpath expression</param>
    /// <param name="xmlDocument">XmlDocument to retrieve the value from</param>
    /// <returns></returns>
    public static string? RetrieveNodeValueIfExists(string xpath, XmlDocument xmlDocument)
    {
        return RetrieveNodeValueIfExists(xpath, xmlDocument, false);
    }

    /// <summary>
    /// Retrieves a value from an xml document based on the given Xpath expression. Returns null id no match can be found.
    /// </summary>
    /// <param name="xpath">Xpath expression</param>
    /// <param name="xmlDocument">XmlDocument to retrieve the value from</param>
    /// <param name="valueIsXml">If true we will return the InnerXml value instead of the InnerText</param>
    /// <returns></returns>
    public static string? RetrieveNodeValueIfExists(string xpath, XmlDocument xmlDocument, bool valueIsXml)
    {
        if (xmlDocument != null)
        {
            var node = xmlDocument.SelectSingleNode(xpath);
            if (node != null)
            {
                if (valueIsXml)
                {
                    return node.InnerXml;
                }
                else
                {
                    return node.InnerText;
                }

            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves the value of an attribute in an xpath statement "/bla/bla/@id" - this is MUCH more efficient than using RetrieveNodeValueIfExists...
    /// </summary>
    /// <param name="xpath"></param>
    /// <param name="xmlDocument"></param>
    /// <returns></returns>
    public static string? RetrieveAttributeValueIfExists(string xpath, XmlDocument xmlDocument)
    {

        Match match = _regexAttributeXpath.Match(xpath);
        if (match.Success)
        {
            var xpathNode = match.Groups[1].Value;
            var attributeName = match.Groups[3].Value;

            return xmlDocument.SelectSingleNode(xpathNode)?.GetAttribute(attributeName);
        }
        else
        {
            return RetrieveNodeValueIfExists(xpath, xmlDocument, false);
        }
    }

    /// <summary>
    /// Retrieves the value of an attribute in an xpath statement "/bla/bla/@id" - this is MUCH more efficient than using RetrieveNodeValueIfExists...
    /// </summary>
    /// <returns>The attribute value if exists.</returns>
    /// <param name="xpath">Xpath.</param>
    /// <param name="xmlNode">Xml node.</param>
    public static string? RetrieveAttributeValueIfExists(string xpath, XmlNode xmlNode)
    {

        Match match = _regexAttributeXpath.Match(xpath);
        if (match.Success)
        {
            var xpathNode = match.Groups[1].Value;
            var attributeName = match.Groups[3].Value;

            var node = xmlNode.SelectSingleNode(xpathNode);
            if (node != null)
            {
                var attributeValue = GetAttribute(node, attributeName);
                return attributeValue;
            }

        }
        else
        {
            return RetrieveNodeValueIfExists(xpath, xmlNode, false);
        }

        return null;
    }

    /// <summary>
    /// Retrieves a text value from an xml node list based on the given Xpath expression. Returns null if no match can be found.
    /// </summary>
    /// <param name="xpath">Xpath expression</param>
    /// <param name="xmlNodeIn">NodeList to retrieve the value from</param>
    /// <returns></returns>
    public static string? RetrieveNodeValueIfExists(string xpath, XmlNode xmlNodeIn)
    {
        return RetrieveNodeValueIfExists(xpath, xmlNodeIn, false);
    }

    /// <summary>
    /// Retrieves a text value from an xml node list based on the given Xpath expression. Returns null if no match can be found.
    /// </summary>
    /// <param name="xpath">Xpath expression</param>
    /// <param name="xmlNodeIn">NodeList to retrieve the value from</param>
    /// <param name="valueIsXml">If true we will return the InnerXml value instead of the InnerText</param>
    /// <returns></returns>
    public static string? RetrieveNodeValueIfExists(string xpath, XmlNode xmlNodeIn, bool valueIsXml)
    {
        var node = xmlNodeIn.SelectSingleNode(xpath);
        if (node != null)
        {
            if (valueIsXml)
            {
                return node.InnerXml;
            }
            else
            {
                return node.InnerText;
            }

        }
        return null;
    }

    /// <summary>
    /// Retrieves a value from an xml document using namespaces based on the given Xpath expression. Returns null id no match can be found.
    /// </summary>
    /// <param name="xpath">Xpath expression</param>
    /// <param name="xmlDocument">XmlDocument to retrieve the value from</param>
    /// <param name="nameSpaces">Array of namespaces that is required to execute the xpath query</param>
    /// <returns></returns>
    public static string? RetrieveNodeValueIfExists(string xpath, XmlDocument xmlDocument, string[] nameSpaces)
    {
        string nameSpace;
        string nameSpaceValue;

        try
        {
            XmlNamespaceManager xmlNamespaceManager = new(xmlDocument.NameTable);

            if (nameSpaces != null)
            {
                foreach (string s in nameSpaces)
                {
                    nameSpace = s.Split(Char.Parse("#"))[0];
                    nameSpaceValue = s.Substring((nameSpace.Length + 1), (s.Length - (nameSpace.Length + 1)));

                    xmlNamespaceManager.AddNamespace(nameSpace, nameSpaceValue);
                }
            }

            return xmlDocument.SelectSingleNode(xpath, xmlNamespaceManager)?.InnerText;
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, $"An exception occurred in RetrieveNodeValueIfExists('{xpath}')");
            return null;
        }
    }

    /// <summary>
    /// Retrieves a text value from an xpathnavigator (xpathdocument derived) Xpath expression. Returns null id no match can be found.
    /// </summary>
    /// <param name="xpath">Xpath expression</param>
    /// <param name="xPathNavigator">XPathNavigator instance where we need to start the search</param>
    /// <returns></returns>
    public static string? RetrieveNodeValueIfExists(string xpath, XPathNavigator xPathNavigator)
    {
        XPathNodeIterator xPathIterator = xPathNavigator.Select(xpath);
        while (xPathIterator.MoveNext())
        {
            return xPathIterator.Current?.Value;
        }
        return null;
    }


    /// <summary>
    /// Converts a location ID, path on the disk or URI to a path we can further process
    /// </summary>
    /// <returns>The location path.</returns>
    /// <param name="location">Location.</param>
    private static string? _getLocationPath(string? location)
    {
        if (string.IsNullOrEmpty(location)) return null;

        /* fixformat ignore:start */
        if (location.Contains("://"))
        {
            // URI
            return location;
        }
        else if (location.Contains('/') || location.Contains('\\') || location.Contains('.'))
        {
            // Path on the local disk
            // TODO: check if the file exists?
            return location;
        }
        else
        {
            try
            {
                try
                {
                    HttpContext? context = System.Web.Context.Current;
                    if (context == null)
                    {
                        return CalculateFullPathOs(location);
                    }
                    else
                    {
                        return CalculateFullPathOs(location, RetrieveRequestVariables(context));
                    }
                }
                catch (Exception)
                {
                    return CalculateFullPathOs(location);
                }

            }
            catch (Exception)
            {
                return CalculateFullPathOs(location);
            }
        }
        /* fixformat ignore:end */
    }

    /// <summary>
    /// Basic XSLT trasformation utility that returns the string after transformation
    /// </summary>
    /// <param name="xmlLocation">Location ID, OS path or URI pointing to the XML file</param>
    /// <param name="xslLocation">Location ID, OS path or URI pointing to the XSLT file</param>
    /// <returns></returns>
    public static string? TransformXml(string xmlLocation, string xslLocation)
    {
        XmlDocument xmlDocument = new();
        var pathOs = _getLocationPath(xmlLocation);
        if (pathOs == null) return null;
        xmlDocument.Load(pathOs);

        return TransformXml(xmlDocument, _getLocationPath(xslLocation), null);
    }

    /// <summary>
    /// Basic xslt transformation utility that returns the string after transformation
    /// </summary>
    /// <param name="xmlDocument">XmlDocument to transform</param>
    /// <param name="xslLocation">Location ID, OS path or URI pointing to the XSLT file</param>
    /// <returns></returns>
    public static string? TransformXml(XmlDocument xmlDocument, string xslLocation)
    {
        return TransformXml(xmlDocument, _getLocationPath(xslLocation), null);
    }

    /// <summary>
    /// XSLT transformation with arguments
    /// </summary>
    /// <param name="xmlLocation">Location ID, OS path or URI pointing to the XML file</param>
    /// <param name="xslLocation">Location ID, OS path or URI pointing to the XSLT file</param>
    /// <param name="xsltArgumentList">Arguments for the transform</param>
    /// <returns></returns>
    public static string? TransformXml(string xmlLocation, string xslLocation, XsltArgumentList xsltArgumentList)
    {
        XmlDocument xmlDocument = new();
        string? pathOs = _getLocationPath(xmlLocation);
        if (pathOs == null) return null;
        xmlDocument.Load(pathOs);

        return TransformXml(xmlDocument, _getLocationPath(xslLocation), xsltArgumentList);
    }

    /// <summary>
    /// XSLT transformation with arguments
    /// </summary>
    /// <param name="xmlDocument">XmlDocument to transform</param>
    /// <param name="xslLocation">Location ID, OS path or URI pointing to the XSLT file</param>
    /// <param name="xsltArgumentList">Arguments for the transform</param>
    /// <returns></returns>
    public static string? TransformXml(XmlDocument xmlDocument, string? xslLocation, XsltArgumentList? xsltArgumentList)
    {
        if (string.IsNullOrEmpty(xslLocation)) HandleError("Could not find transformation template", "xslLocation is null or empty");

        // Path to the xsl
        var xslPathOs = _getLocationPath(xslLocation);
        if (xslPathOs == null) return null;

        XsltSettings xsltSettings = new() { EnableDocumentFunction = true };

        // Required to load external stylesheets via xsl:include ...
        XmlUrlResolver resolver = new() { Credentials = CredentialCache.DefaultCredentials };

        // Load the style sheet.
        XslCompiledTransform xslCompiledTransform = new();
        xslCompiledTransform.Load(xslPathOs, xsltSettings, resolver);

        return TransformXml(xmlDocument, xslCompiledTransform, xsltArgumentList);
    }

    /// <summary>
    /// XSLT transformation with arguments
    /// </summary>
    /// <param name="xmlDocument">XmlDocument to transform</param>
    /// <param name="xslCompiledTransform">Compiled transform XSLT object</param>
    /// <param name="xsltArgumentList">Arguments for the transform</param>
    /// <returns></returns>
    public static string TransformXml(XmlDocument xmlDocument, XslCompiledTransform xslCompiledTransform, XsltArgumentList? xsltArgumentList)
    {
        string returnValue;

        // Required to load external stylesheets via xsl:include ...
        XmlUrlResolver resolver = new() { Credentials = CredentialCache.DefaultCredentials };

        // Transform the file and output the result as a string    
        using (MemoryStream output = new())
        {
            using (XmlHtmlWriter writer = new(output, Encoding.UTF8))
            {
                //consider this to make optional with an overload or so
                writer.Formatting = Formatting.Indented;

                using (XmlNodeReader xmlNodeReader = new(xmlDocument))
                {
                    xslCompiledTransform.Transform(xmlNodeReader, xsltArgumentList, writer, resolver);

                    output.Seek(0, SeekOrigin.Begin);

                    using (StreamReader sr = new(output))
                    {
                        returnValue = sr.ReadToEnd();
                        sr.Close();
                    }
                    xmlNodeReader.Close();
                }
            }
            output.Close();
        }

        return returnValue;
    }


    /// <summary>
    /// XSLT transformation that returns a XmlDocument
    /// </summary>
    /// <param name="xmlLocation">Location ID, OS path or URI pointing to the XML file</param>
    /// <param name="xslLocation">Location ID, OS path or URI pointing to the XSLT file</param>
    /// <returns></returns>
    public static XmlDocument TransformXmlToDocument(string xmlLocation, string xslLocation)
    {
        XmlDocument xmlDocument = new();
        var pathOs = _getLocationPath(xmlLocation);
        if (pathOs == null) return new XmlDocument();
        xmlDocument.Load(pathOs);

        return TransformXmlToDocument(xmlDocument, _getLocationPath(xslLocation), null);
    }

    /// <summary>
    /// XSLT transformation that returns a XmlDocument
    /// </summary>
    /// <param name="xmlDocument">XmlDocument to transform</param>
    /// <param name="xslLocation">Location ID, OS path or URI pointing to the XSLT file</param>
    /// <returns></returns>
    public static XmlDocument TransformXmlToDocument(XmlDocument xmlDocument, string xslLocation)
    {
        return TransformXmlToDocument(xmlDocument, _getLocationPath(xslLocation), null);
    }

    /// <summary>
    /// XSLT transformation with arguments that returns a XmlDocument
    /// </summary>
    /// <param name="xmlLocation">Location ID, OS path or URI pointing to the XML file</param>
    /// <param name="xslLocation">Location ID, OS path or URI pointing to the XSLT file</param>
    /// <param name="xsltArgumentList">Transformation arguments</param>
    /// <returns></returns>
    public static XmlDocument TransformXmlToDocument(string xmlLocation, string xslLocation, XsltArgumentList xsltArgumentList)
    {
        XmlDocument xmlDocument = new();
        var pathOs = _getLocationPath(xmlLocation);
        if (pathOs == null) return new XmlDocument();
        xmlDocument.Load(pathOs);

        return TransformXmlToDocument(xmlDocument, _getLocationPath(xslLocation), xsltArgumentList);
    }

    /// <summary>
    /// XSLT transformation with arguments that returns a XmlDocument
    /// </summary>
    /// <param name="xmlDocument">XmlDocument to transform</param>
    /// <param name="xslLocation">Location ID, OS path or URI pointing to the XSLT file</param>
    /// <param name="xsltArgumentList">Transformation arguments</param>
    /// <returns></returns>
    public static XmlDocument TransformXmlToDocument(XmlDocument xmlDocument, string? xslLocation, XsltArgumentList? xsltArgumentList)
    {
        var xslPathOs = _getLocationPath(xslLocation);
        if (xslPathOs == null) return new XmlDocument();
        XsltSettings xsltSettings = new(true, true);

        // Required to load external stylesheets via xsl:include ...
        var resolver = new XmlUrlResolver { Credentials = CredentialCache.DefaultCredentials };

        // Load the style sheet.
        XslCompiledTransform xslCompiledTransform = new();

        xslCompiledTransform.Load(xslPathOs, xsltSettings, resolver);

        return TransformXmlToDocument(xmlDocument, xslCompiledTransform, xsltArgumentList);
    }

    /// <summary>
    /// XSLT transformation with arguments that returns a XmlDocument
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <param name="xslCompiledTransform"></param>
    /// <param name="xsltArgumentList"></param>
    /// <returns></returns>
    public static XmlDocument TransformXmlToDocument(XmlDocument xmlDocument, XslCompiledTransform xslCompiledTransform, XsltArgumentList? xsltArgumentList)
    {
        var xmlDocumentOut = new XmlDocument();

        // Required to load external stylesheets via xsl:include ...
        var resolver = new XmlUrlResolver
        {
            Credentials = CredentialCache.DefaultCredentials
        };

        // Load the style sheet.

        using (StringReader stringReader = new(xmlDocument?.DocumentElement?.OuterXml ?? ""))
        {
            using XmlTextReader xmlTextReader = new(stringReader);
            // Transform the file and output the result as a xmldocument     

            using (MemoryStream output = new())
            {

                using (XmlHtmlWriter writer = new(output, Encoding.UTF8))
                {
                    writer.Formatting = Formatting.Indented;
                    xslCompiledTransform.Transform(xmlTextReader, xsltArgumentList, writer, resolver);

                    output.Seek(0, SeekOrigin.Begin);
                    xmlDocumentOut.Load(output);
                }

                output.Close();
            }

            xmlTextReader.Close();
        }


        return xmlDocumentOut;
    }

    /// <summary>
    /// Tests if an attribute exists on thw passed XmlNode object
    /// </summary>
    /// <returns><c>true</c>, if attribute exists, <c>false</c> otherwise.</returns>
    /// <param name="node">Node.</param>
    /// <param name="name">Name.</param>
    /// <param name="namespaceURI">Namespace URI.</param>
    public static bool AttributeExists(XmlNode node, string name, string? namespaceURI = null)
    {
        if (node.Attributes == null) return false;
        if (namespaceURI == null)
        {
            return !(node.Attributes[name] == null);
        }
        else
        {
            var attr = node.Attributes.GetNamedItem(name, namespaceURI);
            if (attr == null) return false;
            var xmlAttr = (XmlAttribute)attr;
            if (xmlAttr == null) return false;
            return true;
        }

    }

    /// <summary>
    /// Gets the attribute value of the provided XmlNode
    /// </summary>
    /// <param name="node">xml node to get the attribute value</param> 
    /// <param name="name">name of the attribute</param>
    /// <returns>The attribute value or null if it cannot be found</returns>
    public static string? GetAttribute(XmlNode? node, string name)
    {
        if (node != null)
        {
            if (node.Attributes == null) return null;
            return node.Attributes[name]?.Value;
        }

        return null;
    }

    /// <summary>
    /// Creates an attribute on the provided XmlNode
    /// </summary>
    /// <param name="node">xml node to create the attribute on</param>
    /// <param name="name">name of the attribute</param>
    /// <param name="value">String value of the attribute</param>
    public static void SetAttribute(XmlNode node, string name, string value)
    {
        SetAttribute(node, name, null, value);
    }

    /// <summary>
    /// Creates an attribute on the provided XmlNode
    /// </summary>
    /// <param name="node">Node.</param>
    /// <param name="name">Name.</param>
    /// <param name="namespaceURI">Namespace URI.</param>
    /// <param name="value">String value of the attribute</param>
    public static void SetAttribute(XmlNode node, string name, string? namespaceURI, string value)
    {
        try
        {
            var xmlDoc = node.OwnerDocument;
            if (xmlDoc != null)
            {
                XmlAttribute newAttribute = xmlDoc.CreateAttribute(name, namespaceURI);
                newAttribute.Value = value;

                if (node.Attributes != null)
                {
                    var existingAttribute = node.Attributes[newAttribute.Name];
                    if (existingAttribute != null)
                    {
                        existingAttribute.Value = newAttribute.Value;
                    }
                    else
                    {
                        node.Attributes.Append(newAttribute);
                    }
                }
            }
            else
            {
                WriteErrorMessageToConsole($"SetAttribute(node, '{name}', {((namespaceURI == null) ? "null" : $"'{namespaceURI}'")},'{value}') failed because no OwnerDocument could be found", $"stack-trace: {GetStackTrace()}");
            }
        }
        catch (Exception ex)
        {
            WriteErrorMessageToConsole($"Error occured in SetAttribute(node, '{name}', {((namespaceURI == null) ? "null" : $"'{namespaceURI}'")},'{value}')", $"Error Message: {ex.Message}, Stack-trace: {CreateStackInfo()}, Error Details: {ex}");
        }
    }

    /// <summary>
    /// Removes an attribute from an XmlNode
    /// </summary>
    /// <param name="node">Node that contains the attribute</param>
    /// <param name="name">Name of the attribute to remove (* is interpreted as a wildcard if the name starts or ends with it)</param>
    public static void RemoveAttribute(XmlNode node, string name)
    {
        if (node.Attributes != null)
        {
            if (name.EndsWith('*') || name.StartsWith('*'))
            {
                var attributePartToMatch = name.Replace("*", "");

                // 1) Find attribute names that match the wildcard
                var attributeNamesToRemove = new List<string>();
                foreach (XmlAttribute? xmlAttribute in node.Attributes)
                {
                    if (xmlAttribute != null)
                    {
                        if (xmlAttribute.Name.Contains(attributePartToMatch))
                        {
                            attributeNamesToRemove.Add(xmlAttribute.Name);
                            //node.Attributes.Remove(xmlAttribute);
                        }
                    }
                }

                // 2) Remove the attribute from the collection
                foreach (string attributeName in attributeNamesToRemove)
                {
                    node.Attributes.Remove(node.Attributes[attributeName]);
                }


            }
            else
            {
                if (node.Attributes[name] != null) node.Attributes.Remove(node.Attributes[name]);
            }
        }
    }

    /// <summary>
    /// Sets the text value of a XmlNode
    /// </summary>
    /// <param name="node">Xml node</param>
    /// <param name="value">Value to be set</param>
    public static void SetTextValue(XmlNode node, string value)
    {
        node.InnerXml = String.Empty;
        if (!String.IsNullOrEmpty(value)) // don't create empty text nodes
        {
            node.InnerText = value;
        }
    }

    /// <summary>
    /// Returns the namespace (=prefix) of the node that is being passed
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static string? FindUnusedPrefix(XmlNode node)
    {
        return FindUnusedPrefix(node, null);
    }

    /// <summary>
    /// Returns the namespace (=prefix) of the node that is being passed
    /// </summary>
    /// <param name="node"></param>
    /// <param name="prefixHint"></param>
    /// <returns></returns>
    public static string? FindUnusedPrefix(XmlNode node, string? prefixHint)
    {
        string? pp = prefixHint;
        if (prefixHint == null || prefixHint.Length == 0)
            pp = "n";
        else
        {
            String uri = node.GetNamespaceOfPrefix(pp ?? "n");
            if (uri == null || uri.Length == 0)
                return pp;
        }

        int n = 1;
        while (true)
        {
            String s = String.Format(pp + "{0}", n++);
            String uri = node.GetNamespaceOfPrefix(s);
            if (uri == null || uri.Length == 0)
                return s;
        }
    }

    /// <summary>
    /// Returns the namespace (=prefix) of the node that is being passed
    /// </summary>
    /// <param name="node"></param>
    /// <param name="uri"></param>
    /// <returns></returns>
    private static String FindPrefixForNamespace(XmlNode node, string uri)
    {
        if (uri == "http://www.w3.org/XML/1998/namespace")
            return "xml";

        return node.GetPrefixOfNamespace(uri);
    }

    /// <summary>
    /// Escapes a string value to avoid xpath injection
    /// </summary>
    /// <param name="predicateString"></param>
    /// <returns></returns>
    public static String GenerateEscapedXPathString(string predicateString)
    {
        //double check escape routine
        predicateString ??= "";
        return "concat('" + predicateString.Replace("'", "', \"'\", '") + "', '')";
    }

    /// <summary>
    /// Replace the target node with the second xml node supplied
    /// </summary>
    /// <param name="xmlNodeTarget"></param>
    /// <param name="xmlNodeToInsert"></param>
    public static void ReplaceXmlNode(XmlNode xmlNodeTarget, XmlNode xmlNodeToInsert)
    {
        if (xmlNodeTarget != null && xmlNodeToInsert != null)
        {
            if (xmlNodeTarget.ParentNode != null && xmlNodeTarget.OwnerDocument != null)
            {
                //1) import the new node into the target xml document
                XmlNode xmlNodeCloned = xmlNodeTarget.OwnerDocument.ImportNode(xmlNodeToInsert, true);

                //2) insert the node at the place of the target node
                xmlNodeTarget.ParentNode.ReplaceChild(xmlNodeCloned, xmlNodeTarget);
            }
            else
            {
                throw new Exception("Could not replace the XML node because the the owner document or the target parent node is null");
            }
        }
        else
        {
            if (xmlNodeTarget == null && xmlNodeToInsert == null)
            {
                throw new Exception("Could not replace the XML node because the target node and the node to insert are null");
            }
            else if (xmlNodeTarget == null)
            {
                throw new Exception("Could not replace the XML node because the target node is null");
            }
            else
            {
                throw new Exception("Could not replace the XML node because the node to insert is null");
            }
        }

    }

    /// <summary>
    /// Utility function that removes a node from an XmlDocument
    /// </summary>
    /// <param name="xmlNodeToRemove">Node to remove</param>
    public static void RemoveXmlNode(XmlNode xmlNodeToRemove)
    {
        if (xmlNodeToRemove != null)
        {
            if (xmlNodeToRemove.ParentNode != null)
            {
                //1) remove the target node
                xmlNodeToRemove.ParentNode.RemoveChild(xmlNodeToRemove);
            }
            else
            {
                throw new Exception("Could not delete the XML node to remove does not contain a parent node");
            }
        }
        else
        {
            throw new Exception("Could not delete the XML node because it was 'null' ...");
        }
    }

    /// <summary>
    /// Removes all the nodes in the nodelist from the XmlDocument
    /// </summary>
    /// <param name="nodeListToRemove">Nodes to remove</param>
    public static void RemoveXmlNodes(XmlNodeList nodeListToRemove)
    {
        if (nodeListToRemove != null)
        {
            foreach (XmlNode? nodeToRemove in nodeListToRemove)
            {
                if (nodeToRemove != null)
                {
                    RemoveXmlNode(nodeToRemove);
                }
            }
        }
        else
        {
            throw new Exception("Could not delete the XML nodelist because it was 'null' ...");
        }
    }

    /// <summary>
    /// Looks for xi:include nodes in an XmlDocument and inserts the document into it
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <returns></returns>
    public static XmlDocument ResolveXIncludes(XmlDocument xmlDocument)
    {
        String[] nameSpaces = ["xi#http://www.w3.org/2003/XInclude"];
        XmlNodeList? nodeListIncludes = RetrieveNodes("//xi:include", xmlDocument, nameSpaces);

        if (nodeListIncludes != null)
        {
            foreach (XmlNode? xmlNode in nodeListIncludes)
            {
                if (xmlNode != null)
                {
                    // Get the resource location
                    string? href = xmlNode.GetAttribute("href");
                    if (href != null)
                    {
                        //check if the location is relative or absolute
                        bool isRelative = true;
                        if (href.StartsWith('/')) isRelative = false;

                        String pathToSourceXml = configurationRootPathOs + "/" + href;
                        if (!isRelative)
                        {
                            pathToSourceXml = websiteRootPathOs + href;
                        }

                        XmlDocument xmlSource = new();
                        xmlSource.Load(pathToSourceXml);

                        if (xmlSource.DocumentElement != null)
                        {
                            ReplaceXmlNode(xmlNode, xmlSource.DocumentElement);
                        }
                    }
                }
            }
        }


        return xmlDocument;
    }

    /// <summary>
    /// Tests if the current node contains <![CDATA[ ]]> content
    /// </summary>
    /// <param name="xmlNode">The xmlNode to test</param>
    /// <returns></returns>
    public static bool HasCDataContent(XmlNode xmlNode)
    {
        bool cdataContent = false;

        //this function needs to be revised so that we really test the content which is inside the node instead of using a predefined attribute
        var cdataValue = xmlNode.GetAttribute("cdata");
        if (cdataValue != null)
        {
            if (cdataValue == "true") cdataContent = true;
        }

        return cdataContent;
    }

    /// <summary>
    /// Converts a XmlDocument to a XPathDocument
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <returns></returns>
    public static XPathDocument ConvertXmlDocumentToXPathDocument(XmlDocument xmlDocument)
    {
        using (var sr = new StringReader(xmlDocument.OuterXml))
        {
            using (XmlTextReader xmlTextReader = new(sr))
            {
                XPathDocument xPathDocument = new(xmlTextReader);
                xmlTextReader.Close();
                sr.Close();
                return xPathDocument;
            }
        }
    }

    /// <summary>
    /// Saves an XPathNavigatorDocument to the disk
    /// </summary>
    /// <param name="xPathDocument">XPathDocument object to save</param>
    /// <param name="filePathOs">Path on the disk to save to</param>
    public static void SaveXPathDocument(XPathDocument xPathDocument, string filePathOs)
    {
        XPathNavigator xPathNavigator = xPathDocument.CreateNavigator();
        XmlTextWriter xmlTextWriter = new XmlTextWriter(filePathOs, System.Text.Encoding.UTF8)
        {
            Formatting = Formatting.Indented,
            Indentation = 3
        };
        xmlTextWriter.WriteStartDocument();
        xmlTextWriter.WriteNode(xPathNavigator, true);
        xmlTextWriter.WriteEndDocument();
        xmlTextWriter.Close();
        xmlTextWriter.Dispose();
    }

    /// <summary>
    /// This routine strips all the namespace content from a xml document
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <param name="useXslt">Switch to older XSLT logic if needed</param>
    /// <returns>The Xml document without the namespaces</returns>
    public static XmlDocument StripNameSpaces(XmlDocument xmlDocument, bool useXslt = false)
    {
        if (useXslt)
        {
            if (xmlDocument.DocumentElement == null) return xmlDocument;

            XmlDocument xmlDocumentOut = new();

            String xslContent = "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><xsl:template match=\"@*|comment()|processing-instruction()|text()\"><xsl:copy-of select=\".\"/></xsl:template><xsl:template match=\"*\"><xsl:element name=\"{local-name()}\"><xsl:apply-templates select=\"@*|node()\"/></xsl:element></xsl:template></xsl:stylesheet>";

            // Load the style sheet.
            XslCompiledTransform xslCompiledTransform = new();
            xslCompiledTransform.Load(new XmlTextReader(new StringReader(xslContent)));

            // -- to support "strip-space" use:
            using (XmlTextReader xmlTextReader = new(new StringReader(xmlDocument.DocumentElement.OuterXml)))
            {
                // Transform the file and output the result as a xmldocument     
                using (MemoryStream output = new())
                {


                    using (XmlHtmlWriter writer = new(output, Encoding.UTF8))
                    {
                        writer.Formatting = Formatting.Indented;
                        xslCompiledTransform.Transform(xmlTextReader, null, writer);

                        output.Seek(0, SeekOrigin.Begin);
                        xmlDocumentOut.Load(output);
                        writer.Close();
                    }

                    output.Close();
                }
                xmlTextReader.Close();
            }

            return xmlDocumentOut;
        }
        else
        {
            XDocument d = XDocument.Parse(xmlDocument.OuterXml);
            d?.Root?.Descendants().Attributes().Where(x => x.IsNamespaceDeclaration).Remove();
            var descs = d?.Descendants();
            if (descs == null) return xmlDocument;
            foreach (var elem in descs)
                elem.Name = elem.Name.LocalName;

            var xmlDocumentOut = new XmlDocument();
            var reader = d?.CreateReader();
            if (reader == null) return xmlDocument;
            xmlDocumentOut.Load(reader);

            return xmlDocumentOut;
        }
    }



    /// <summary>
    /// Renames the node that is being passed with a new name
    /// </summary>
    /// <param name="node">Node to be renamed</param>
    /// <param name="qualifiedName">New name of the node</param>
    /// <returns></returns>
    public static XmlNode RenameNode(XmlNode node, string qualifiedName)
    {
        return RenameNode(node, null, qualifiedName);
    }

    /// <summary>
    /// Renames the node that is being passed with a new name
    /// </summary>
    /// <param name="node">Node to be renamed</param>
    /// <param name="namespaceURI">Namespace (null if does not apply)</param>
    /// <param name="qualifiedName">New name of the node</param>
    /// <returns></returns>
    public static XmlNode RenameNode(XmlNode node, string? namespaceURI, string qualifiedName)
    {
        if (node.OwnerDocument == null) return node;
        if (node.NodeType == XmlNodeType.Element)
        {
            XmlElement oldElement = (XmlElement)node;
            XmlElement newElement = node.OwnerDocument.CreateElement(qualifiedName, namespaceURI);

            while (oldElement.HasAttributes)
            {
                var removedAttribute = oldElement.RemoveAttributeNode(oldElement.Attributes[0]);
                if (removedAttribute != null) newElement.SetAttributeNode(removedAttribute);
            }

            while (oldElement.HasChildNodes)
            {
                if (oldElement.FirstChild != null) newElement.AppendChild(oldElement.FirstChild);
            }

            oldElement.ParentNode?.ReplaceChild(newElement, oldElement);

            return newElement;
        }
        else
        {
            return node;
        }
    }

    /// <summary>
    /// Utility that transforms (bla)(/bla) in (bla/)
    /// </summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    public static XmlDocument ForceSelfClosingTags(XmlDocument xml)
    {
        var nodeList = xml.SelectNodes("descendant::*[not(*) and not(normalize-space())]");
        if (nodeList != null)
        {
            foreach (XmlElement? el in nodeList)
            {
                if (el != null) el.IsEmpty = true;
            }
        }
        return xml;
    }


    /// <summary>
    /// Assures that the document that we pass is converted to contain valid HTML self-closing (or void) tags
    /// </summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    public static XmlDocument ValidHtmlVoidTags(XmlDocument xml)
    {
        var nodeList = xml.SelectNodes("descendant::*[not(*) and not(normalize-space())]");
        if (nodeList != null)
        {
            foreach (XmlElement? el in nodeList)
            {
                if (el != null)
                {
                    // The only valid void HTML tags are area, base, br, col, embed, hr, img, input, keygen, link, meta, param, source, track, wbr
                    switch (el.LocalName)
                    {
                        case "area":
                        case "base":
                        case "br":
                        case "col":
                        case "embed":
                        case "hr":
                        case "img":
                        case "input":
                        case "keygen":
                        case "link":
                        case "meta":
                        case "param":
                        case "source":
                        case "track":
                        case "wbr":
                            el.IsEmpty = true;
                            break;

                        default:
                            el.IsEmpty = false;
                            break;
                    }
                }
            }
        }
        return xml;
    }

    /// <summary>
    /// Utility that attempts to convert a C# object into an XmlDocument
    /// </summary>
    /// <param name="obj">Any C# object</param>
    /// <returns></returns>
    public static XmlDocument ConvertObjectToXml(object obj)
    {
        System.Xml.Serialization.XmlSerializer x = new(obj.GetType());

        XmlDocument xmlContent = new();

        using (StringWriter stringWriter = new())
        {
            using (XmlWriter xmlWriter = XmlWriter.Create(stringWriter))
            {
                x.Serialize(xmlWriter, obj);
                xmlContent.LoadXml(stringWriter.ToString());
                xmlWriter.Close();
            }
            stringWriter.Close();
        }


        return xmlContent;
    }

    /// <summary>
    /// Gets the X-Path to a given XmlNode
    /// </summary>
    /// <param name="node">The Node to get the X-Path from</param>
    /// <returns>The X-Path of the Node</returns>
    public static string GetXPathToNode(XmlNode? node)
    {
        if (node == null) return "";

        if (node.NodeType == XmlNodeType.Attribute)
        {
            // attributes have an OwnerElement, not a ParentNode; also they have             
            // to be matched by name, not found by position             
            return string.Format("{0}/@{1}", GetXPathToNode(((XmlAttribute)node).OwnerElement), node.Name);
        }
        if (node.ParentNode == null)
        {
            // the only node with no parent is the root node, which has no path
            return "";
        }

        // Get the Index
        int indexInParent = 1;
        var siblingNode = node?.PreviousSibling;
        // Loop through all Siblings
        while (siblingNode != null)
        {
            // Increase the Index if the Sibling has the same Name
            if (siblingNode.Name == node?.Name)
            {
                indexInParent++;
            }
            siblingNode = siblingNode.PreviousSibling;
        }

        // the path to a node is the path to its parent, plus "/node()[n]", where n is its position among its siblings.         
        return String.Format("{0}/{1}[{2}]", GetXPathToNode(node?.ParentNode), node?.Name, indexInParent);
    }

    /// <summary>
    /// Pretty prints the content of an Xml Document
    /// </summary>
    /// <param name="xmlDoc"></param>
    /// <returns></returns>
    public static string PrettyPrintXml(XmlDocument xmlDoc)
    {
        return PrettyPrintXml(xmlDoc.OuterXml);
    }

    /// <summary>
    /// Pretty prints the content of an XmlNode
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static string PrettyPrintXml(XmlNode node)
    {
        return PrettyPrintXml(node.OuterXml);
    }

    /// <summary>
    /// Pretty prints an XML valid string
    /// </summary>
    /// <param name="xml">Valid XML string</param>
    /// <returns></returns>
    public static string PrettyPrintXml(string xml)
    {
        try
        {
            var stringBuilder = new StringBuilder();

            var element = XElement.Parse(xml);

            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = false
            };

            using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
            {
                element.Save(xmlWriter);
                xmlWriter.Close();
            }
            return stringBuilder.ToString();
        }
        catch (Exception ex)
        {
            TextFileCreate(xml, $"{logRootPathOs}/-----pretty-print.xml");
            appLogger.LogError(ex, "There was a problem pretty printing the document");
            // TextFileCreate(xml, logRootPathOs + "/prettyprintxml-error.xml");
            WriteErrorMessageToConsole("Error occured in PrettyPrintXml()", $"Error Message: {ex.Message}, XML: {TruncateString(xml, 200)}, Stack-trace: {CreateStackInfo()}, Error Details: {ex}");
        }
        return xml;
    }

    /// <summary>
    /// Converts a XmlDocument to an ExpandoObject
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>
    public static dynamic? XmlToObject(XmlDocument document)
    {
        var root = document.DocumentElement;
        if (root == null) return null;
        return XmlToObject(root, new ExpandoObject());
    }

    /// <summary>
    /// Converts a XmlNode to an ExpandoObject
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static dynamic XmlToObject(XmlNode node)
    {
        return XmlToObject(node, new ExpandoObject());
    }

    /// <summary>
    /// Private routine used for Xml to ExpandoObject conversion
    /// </summary>
    /// <param name="node"></param>
    /// <param name="config"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    private static dynamic XmlToObject(XmlNode node, ExpandoObject config, int count = 1)
    {
        var parent = config as IDictionary<string, object>;
        var attributes = node.Attributes;
        if (attributes != null)
        {
            foreach (XmlAttribute? nodeAttribute in attributes)
            {
                if (nodeAttribute != null)
                {
                    var nodeAttrName = UpperFirst(nodeAttribute.Name);
                    parent[nodeAttrName] = nodeAttribute.Value;
                }
            }
        }

        foreach (XmlNode? nodeChild in node.ChildNodes)
        {
            if (nodeChild != null)
            {
                if (IsTextOrCDataSection(nodeChild))
                {
                    parent["Value"] = nodeChild?.Value ?? "";
                }
                else
                {
                    string nodeChildName = UpperFirst(nodeChild.Name);
                    if (parent.ContainsKey(nodeChildName))
                    {
                        parent[nodeChildName + "_" + count] = XmlToObject(nodeChild, new ExpandoObject(), count++);
                    }
                    else
                    {
                        parent[nodeChildName] = XmlToObject(nodeChild, new ExpandoObject());
                    }
                }
            }
        }
        return config;
    }

    /// <summary>
    /// Determines if a XmlNode has text or CData content
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static bool IsTextOrCDataSection(XmlNode node)
    {
        return node.Name == "#text" || node.Name == "#cdata-section";
    }

    /// <summary>
    /// Utility to save an XmlDocument file to the disk using the BaseURI property of the path if supplied
    /// </summary>
    /// <param name="xmlDoc">XmlDocument object to save</param>
    /// <param name="xmlPathOs">Optionally overwrite the BaseURI path for some other path on the disk</param>
    /// <param name="message">An optional message to send to the project function</param>
    /// <returns></returns>
    public static async Task<bool> SaveXmlDocument(XmlDocument xmlDoc, string? xmlPathOs = null, string? message = null)
    {
        bool success = true;

        HttpContext? context;
        try
        {
            context = System.Web.Context.Current;
        }
        catch (Exception)
        {
            context = null;
        }


        try
        {
            if (xmlPathOs == null)
            {
                var xmlBasePath = xmlDoc.BaseURI;
                if (xmlBasePath.Contains("file:///"))
                {
                    xmlPathOs = xmlBasePath.Replace(@"file:///", "");
                }
                else
                {
                    WriteErrorMessageToConsole("Could not save xml file", "Incorrect path format: " + xmlBasePath);
                    success = false;
                }

            }

            if (xmlPathOs != null && await HasExclusiveFileAccessAsync(xmlPathOs))
            {
                await xmlDoc.SaveAsync(xmlPathOs, true, true);

                //post process the save function
                success = await Extensions.SaveXmlDocumentProject(context, xmlDoc, xmlPathOs, message);
            }
            else
            {
                Console.WriteLine($"ERROR: Could not save xml file in {xmlPathOs} bacause we could not obtain exclusive permissions to it. ");
                success = false;
            }


            // TODO: Fix the below
            // if (HasExclusiveFileAccess(xmlPathOs))
            // {
            //  await	xmlDoc.SaveAsync(xmlPathOs);

            // 	//post process the save function
            // 	success = SaveXmlDocumentProject(xmlDoc, xmlPathOs, message);
            // }
            // else
            // {
            // 	ErrorHandlingTrace("Could not save xml file", "Could not obtain exclusive permissions to save xml file: " + xmlPathOs, false, true);
            // 	success = false;
            // }
        }
        catch (Exception ex)
        {
            WriteErrorMessageToConsole("Could not save xml file", $"error-details: {ex}, stack-trace: {CreateStackInfo()}");
            success = false;
        }

        return success;
    }

    /// <summary>
    /// Utility to save an XmlDocument file to the disk using the BaseURI property of the path if supplied
    /// </summary>
    /// <param name="xmlDoc"></param>
    /// <param name="xmlPathOs"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static bool SaveXmlDocumentSync(XmlDocument xmlDoc, string? xmlPathOs = null, string? message = null)
    {
        bool success = true;

        HttpContext? context;
        try
        {
            context = System.Web.Context.Current;
        }
        catch (Exception)
        {
            context = null;
        }


        try
        {
            if (xmlPathOs == null)
            {
                var xmlBasePath = xmlDoc.BaseURI;
                if (xmlBasePath.Contains("file:///"))
                {
                    xmlPathOs = xmlBasePath.Replace(@"file:///", "");
                }
                else
                {
                    WriteErrorMessageToConsole("Could not save xml file", "Incorrect path format: " + xmlBasePath);
                    success = false;
                }

            }

            if (xmlPathOs != null)
            {
                xmlDoc.Save(xmlPathOs);

                //post process the save function
                success = Extensions.SaveXmlDocumentProject(context, xmlDoc, xmlPathOs, message).GetAwaiter().GetResult();

                // TODO: Fix the below
                // if (HasExclusiveFileAccess(xmlPathOs))
                // {
                //  await	xmlDoc.SaveAsync(xmlPathOs);

                // 	//post process the save function
                // 	success = SaveXmlDocumentProject(xmlDoc, xmlPathOs, message);
                // }
                // else
                // {
                // 	ErrorHandlingTrace("Could not save xml file", "Could not obtain exclusive permissions to save xml file: " + xmlPathOs, false, true);
                // 	success = false;
                // }                
            }
            else
            {
                throw new Exception("Could not save xml file because a file path could not be retrieved");
            }


        }
        catch (Exception ex)
        {
            WriteErrorMessageToConsole("Could not save xml file", $"error-details: {ex}, stack-trace: {CreateStackInfo()}");
            success = false;
        }

        return success;
    }
    /// <summary>
    /// Generates a generic success xml document.
    /// </summary>
    /// <returns>The success xml.</returns>
    /// <param name="message">Message.</param>
    /// <param name="debugInfo">Debug info.</param>

    public static XmlDocument GenerateSuccessXml(string message, string? debugInfo = null, string? payload = null)
    {
        var templateId = (payload == null) ? "success_xml" : "success_payload_xml";
        XmlDocument? xmlDoc = RetrieveTemplate(templateId);
        if (xmlDoc == null)
        {
            appLogger.LogWarning($"Could not find XML template for '{templateId}', success-message: {message}, stack-trace: {GetStackTrace()}");
            return new XmlDocument();
        }
        else
        {
            var nodeMessage = xmlDoc.SelectSingleNode("/result/message");
            if (nodeMessage != null) nodeMessage.InnerText = message;

            var removeDebugInfoNode = false;
            if (string.IsNullOrEmpty(debugInfo))
            {
                removeDebugInfoNode = true;
            }
            else if (debugInfo.Trim() == "")
            {
                removeDebugInfoNode = true;
            }

            var nodeDebugInfo = xmlDoc.SelectSingleNode("/result/debuginfo");
            if (nodeDebugInfo != null)
            {
                if (!removeDebugInfoNode)
                {
                    nodeDebugInfo.InnerText = debugInfo ?? "";
                }
                else
                {
                    RemoveXmlNode(nodeDebugInfo);
                }
            }



            if (payload != null)
            {
                var nodePayload = xmlDoc.SelectSingleNode("/result/payload");
                if (nodePayload != null)
                {
                    nodePayload.InnerText = payload;
                }
            }
            return xmlDoc;
        }
    }

    /// <summary>
    /// Strips the debug info node from the standard envelope
    /// </summary>
    /// <param name="xmlDoc"></param>
    public static void StripDebugInfo(ref XmlDocument xmlDoc)
    {
        var nodeDebugInfo = xmlDoc.SelectSingleNode("/result/debuginfo");
        if (nodeDebugInfo != null)
        {
            RemoveXmlNode(nodeDebugInfo);
        }
    }

    /// <summary>
    /// Tests if the XmlDocument is most likely a standard envelope
    /// </summary>
    /// <param name="xmlDoc"></param>
    /// <returns></returns>
    public static bool IsStandardEnvelope(XmlDocument xmlDoc)
    {
        return (xmlDoc.SelectSingleNode("/result/message") != null);
    }

    /// <summary>
    /// Converts a regular xpath to a syntax that can be used to select elements regardless of their namespace
    /// </summary>
    /// <param name="xpath"></param>
    /// <param name="debug"></param>
    /// <returns></returns>
    public static string ConvertToNamespaceAgnosticXpath(string xpath, bool debug = false)
    {
        // TODO: See if this can be optimized by initiating one regex object and then re-using it for the following replacements
        var axisPart = (xpath.Contains("::")) ? $"{xpath.SubstringBefore("::")}::" : "";
        var xpathConverted = (xpath.Contains("::")) ? xpath.SubstringAfter("::") : xpath;

        xpathConverted = RegExpReplace(@"(\/)([a-z]+)(\[)", xpathConverted, @"/*[local-name()='$2' and ");
        xpathConverted = RegExpReplace(@"(\/)([a-z]+)", xpathConverted, @"/*[local-name()='$2']");
        xpathConverted = RegExpReplace(@"^([a-z]+)(\[)", xpathConverted, @"*[local-name()='$1' and ");
        xpathConverted = RegExpReplace(@"^([a-z]+)", xpathConverted, @"*[local-name()='$1']");

        xpathConverted = $"{axisPart}{xpathConverted}";
        // Log the namespace agnostic xpath to the console so that we can inspect it
        if (debug)
        {
            Console.WriteLine($"- xpathNamespaceAgnostic: {xpathConverted}");
        }

        return xpathConverted.Trim();
    }

    /// <summary>
    /// Generates a digital signature hash from the content of the provided XmlNode
    /// </summary>
    /// <returns>The xml hash.</returns>
    /// <param name="xmlNode">Xml node.</param>
    public static string GenerateXmlHash(XmlNode xmlNode)
    {
        XmlDocument xmlDocument = new();
        xmlDocument.LoadXml(xmlNode.OuterXml);
        return GenerateXmlHash(xmlDocument);
    }

    /// <summary>
    /// Convert an XmlDocument to HTML (without self closing tags)
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <returns></returns>
    public static string XmlToHtml(XmlDocument xmlDocument)
    {
        using (var stream = new StringWriter())
        {
            var settings = new XmlWriterSettings();
            var propInfo = settings.GetType().GetProperty("OutputMethod");
            propInfo?.SetValue(settings, XmlOutputMethod.Html, null);
            var writer = XmlWriter.Create(stream, settings);
            xmlDocument.Save(writer);
            writer.Close();
            writer.Dispose();
            return stream.ToString();
        }
    }

    /// <summary>
    /// Generates a digital signature hash from the content of the provided XmlDocument
    /// </summary>
    /// <returns>The xml hash.</returns>
    /// <param name="xmlDocument">Xml document.</param>
    public static string GenerateXmlHash(XmlDocument xmlDocument)
    {
        var t = new System.Security.Cryptography.Xml.XmlDsigC14NTransform();
        t.LoadInput(xmlDocument);
        var s = (Stream)t.GetOutput(typeof(Stream));
        var cryptSha1 = SHA1.Create();

        var hash = cryptSha1.ComputeHash(s);
        var base64String = Convert.ToBase64String(hash);
        s.Close();
        s.Dispose();
        return base64String;
    }

    /// <summary>
    /// Converts a C# object to an XML string
    /// </summary>
    /// <returns>The to xml.</returns>
    /// <param name="obj">C# object to convert</param>
    public static string ObjectToXml(object obj)
    {
        using (StringWriter stringWriter = new())
        {
            try
            {
                XmlSerializer serializer = new(obj.GetType());

                using (var xmlTextWriter = new XmlTextWriter(stringWriter))
                {
                    serializer.Serialize(xmlTextWriter, obj);
                    xmlTextWriter.Close();
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                stringWriter.Close();
            }
            return stringWriter.ToString();
        }
    }

    /// <summary>
    /// Converts an XML string to a C# object
    /// use as: Employee employee = (Employee)ObjectToXML(xml,typeof(Employee));
    /// </summary>
    /// <returns>The to object.</returns>
    /// <param name="xml">Xml.</param>
    /// <param name="objectType">Object type.</param>
    public static Object? XmlToObject(string xml, Type objectType)
    {
        XmlSerializer? xmlSerializer;

        Object? obj = null;
        try
        {
            using (var stringReader = new StringReader(xml))
            {
                xmlSerializer = new XmlSerializer(objectType);

                using (var xmlTextReader = new XmlTextReader(stringReader))
                {
                    obj = xmlSerializer.Deserialize(xmlTextReader);

                    xmlTextReader.Close();
                }
                stringReader.Close();
            }
        }
        catch (Exception)
        {
            throw;
        }
        return obj;
    }
}

/// <summary>
/// Extensions for the XML classes
/// </summary>
public static class XmlExtensions
{
    /// <summary>
    /// Selects a single node in an XmlDocument regardless of the namespace
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="xpath"></param>
    /// <param name="debug"></param>
    /// <returns></returns>
    public static XmlNode? SelectSingleNodeAgnostic(this XmlDocument doc, string xpath, bool debug = false)
    {
        return doc.SelectSingleNode(Framework.ConvertToNamespaceAgnosticXpath(xpath, debug));
    }

    /// <summary>
    /// Selects a single node in an XmlNode regardless of the namespace
    /// </summary>
    /// <param name="xmlNode"></param>
    /// <param name="xpath"></param>
    /// <param name="debug"></param>
    /// <returns></returns>
    public static XmlNode? SelectSingleNodeAgnostic(this XmlNode xmlNode, string xpath, bool debug = false)
    {
        return xmlNode.SelectSingleNode(Framework.ConvertToNamespaceAgnosticXpath(xpath, debug));
    }

    /// <summary>
    /// Selects xml nodes from an XmlDocument regardless of the namespace
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="xpath"></param>
    /// <param name="debug"></param>
    /// <returns></returns>
    public static XmlNodeList? SelectNodesAgnostic(this XmlDocument doc, string xpath, bool debug = false)
    {
        return doc.SelectNodes(Framework.ConvertToNamespaceAgnosticXpath(xpath, debug));
    }

    /// <summary>
    /// Selects xml nodes from an XmlNode regardless of the namespace
    /// </summary>
    /// <param name="xmlNode"></param>
    /// <param name="xpath"></param>
    /// <param name="debug"></param>
    /// <returns></returns>
    public static XmlNodeList? SelectNodesAgnostic(this XmlNode xmlNode, string xpath, bool debug = false)
    {
        return xmlNode.SelectNodes(Framework.ConvertToNamespaceAgnosticXpath(xpath, debug));
    }

    /// <summary>
    /// Converts an XDocument to an XmlDocument
    /// </summary>
    /// <param name="xDocument"></param>
    /// <returns></returns>
    public static XmlDocument ToXmlDocument(this XDocument xDocument)
    {
        var xmlDocument = new XmlDocument();
        using (var xmlReader = xDocument.CreateReader())
        {
            xmlDocument.Load(xmlReader);
        }
        return xmlDocument;
    }

    /// <summary>
    /// Converts an XmlDocument to an XDocument
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <returns></returns>
    public static XDocument ToXDocument(this XmlDocument xmlDocument)
    {
        using (var nodeReader = new XmlNodeReader(xmlDocument))
        {
            nodeReader.MoveToContent();
            return XDocument.Load(nodeReader);
        }
    }

    /// <summary>
    /// Creates or sets the valie of an attribute on the provided XmlNode
    /// </summary>
    /// <param name="node"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public static void SetAttribute(this XmlNode node, string name, string value)
    {
        SetAttribute(node, name, null, value);
    }

    /// <summary>
    /// Creates or sets the valie of an attribute on the provided XmlNode
    /// </summary>
    /// <param name="node">Node.</param>
    /// <param name="name">Name.</param>
    /// <param name="namespaceURI">Namespace URI.</param>
    /// <param name="value">String value of the attribute</param>
    public static void SetAttribute(this XmlNode node, string name, string? namespaceURI, string value)
    {
        Framework.SetAttribute(node, name, namespaceURI, value);
    }

    /// <summary>
    /// Retrieves an attribute value from an XmlNode
    /// </summary>
    /// <param name="node"></param>
    /// <param name="name"></param>
    /// <returns>Returns the attribute value as a string or null if the attribute cannot be found on the XmlNode</returns>
    public static string? GetAttribute(this XmlNode node, string name)
    {
        if (node != null)
        {
            return Framework.GetAttribute(node, name);
        }

        return null;
    }

    /// <summary>
    /// Indicates if the XmlNode contains the attribute or not
    /// </summary>
    /// <param name="node"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool HasAttribute(this XmlNode node, string name)
    {
        if (node != null)
        {
            var element = (XmlElement)node;
            return element.HasAttribute(name);
        }

        return false;
    }

    /// <summary>
    /// Removes an attribute from an XmlNode
    /// </summary>
    /// <param name="node"></param>
    /// <param name="name"></param>
    public static void RemoveAttribute(this XmlNode node, string name)
    {
        Framework.RemoveAttribute(node, name);
    }

    /// <summary>
    /// Removes this node from the XmlDocument
    /// </summary>
    /// <param name="node"></param>
    public static void Remove(this XmlNode node)
    {
        Framework.RemoveXmlNode(node);
    }

    /// <summary>
    /// Removes all the nodes from the XmlDocument
    /// </summary>
    /// <param name="nodeList"></param>
    public static void Remove(this XmlNodeList nodeList)
    {
        Framework.RemoveXmlNodes(nodeList);
    }

    /// <summary>
    /// Creates an XmlElement with InnerText
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <param name="nodeName"></param>
    /// <param name="nodeText"></param>
    /// <returns></returns>
    public static XmlElement CreateElementWithText(this XmlDocument xmlDocument, string nodeName, string nodeText)
    {
        var newElement = xmlDocument.CreateElement(nodeName);
        if (nodeText != null) newElement.InnerText = nodeText;
        return newElement;
    }

    /// <summary>
    /// Replaces the complete content of the XmlDocument with the content of the XmlDocument passed
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <param name="xmlDocumentToImport"></param>
    ///  <param name="useStringMethod">Use old-fashioned LoadXml() string based method for cloning content which might work better with (X)HTML type of content</param>
    public static void ReplaceContent(this XmlDocument xmlDocument, XmlDocument xmlDocumentToImport, bool useStringMethod = false)
    {
        //xmlDocument = (XmlDocument) xmlDocumentToImport.CloneNode(true);
        //xmlDocument.InnerXml = xmlDocumentToImport.OuterXml;
        if (useStringMethod)
        {
            xmlDocument.LoadXml(xmlDocumentToImport.OuterXml);
        }
        else
        {
            if (xmlDocumentToImport.DocumentElement != null)
            {
                if (xmlDocument.DocumentElement != null)
                {
                    xmlDocument.ReplaceChild(xmlDocument.ImportNode(xmlDocumentToImport.DocumentElement, true), xmlDocument.DocumentElement);
                }
                else
                {
                    xmlDocument.AppendChild(xmlDocument.ImportNode(xmlDocumentToImport.DocumentElement, true));
                }
            }
        }

    }

    /// <summary>
    /// Replaces the complete content of the XmlDocument with the content of the XmlNode passed
    /// </summary>
    /// <param name="nodeToImport"></param>
    /// <returns></returns>
    public static void ReplaceContent(this XmlDocument xmlDocument, XmlNode nodeToImport, bool useStringMethod = false)
    {
        //xmlDocument = (XmlDocument) nodeToImport.CloneNode(true);
        if (useStringMethod)
        {
            xmlDocument.LoadXml(nodeToImport.OuterXml);
        }
        else
        {
            if (xmlDocument.DocumentElement != null)
            {
                xmlDocument.ReplaceChild(xmlDocument.ImportNode(nodeToImport, true), xmlDocument.DocumentElement);
            }
            else
            {
                xmlDocument.AppendChild(xmlDocument.ImportNode(nodeToImport, true));
            }
        }
    }

    /// <summary>
    /// Canonicalizes the contents of an XmlDocument so that it can be used for string comparisons or for generating signatures
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <returns></returns>
    public static string Canonicalize(this XmlDocument xmlDocument)
    {
        XmlDsigExcC14NTransform serializer = new();
        XmlDocument doc = new();
        doc.ReplaceContent(xmlDocument);
        serializer.LoadInput(doc);
        string c14n = new StreamReader((Stream)serializer.GetOutput(typeof(Stream))).ReadToEnd();
        return c14n;
    }

    /// <summary>
    /// Saves the content of an XmlDocument async on a location on the disk
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <param name="pathOs">Path on the disk to save the content to</param>
    /// <param name="throwError">Optionally stop throwing an error when the write fails (can be handy for non critical files)</param>
    /// <param name="prettyPrint">Optionally pretty print the file before saving it to the disk</param>
    /// <returns></returns>
    public static async Task SaveAsync(this XmlDocument xmlDocument, string pathOs, bool throwError = true, bool prettyPrint = false)
    {
        if (throwError)
        {
            if (!prettyPrint)
            {
                await Framework.TextFileCreateAsync(xmlDocument.OuterXml, pathOs);
            }
            else
            {
                await Framework.TextFileCreateAsync(Framework.PrettyPrintXml(xmlDocument), pathOs);
            }
        }
        else
        {
            try
            {
                if (!prettyPrint)
                {
                    await Framework.TextFileCreateAsync(xmlDocument.OuterXml, pathOs);
                }
                else
                {
                    await Framework.TextFileCreateAsync(Framework.PrettyPrintXml(xmlDocument), pathOs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not save XML. pathOs: {pathOs}, error: {ex}");
            }
        }

    }


}