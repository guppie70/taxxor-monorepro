using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Xsl;
using Microsoft.Extensions.Logging;
using XmlValidator;

#nullable enable

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {
        private static XmlSchemaSet? XhtmlSchemaSet = null;


        /// <summary>
        /// Verifies if the XML node content is valid against the Xhtml schema
        /// </summary>
        /// <param name="nodeToCheck"></param>
        /// <param name="relaxedMode"></param>
        /// <returns></returns>
        public static async Task<TaxxorLogReturnMessage> ValidateXhtml(XmlNode nodeToCheck, bool relaxedMode = false)
        {
            var xmlFilingContent = new XmlDocument();
            xmlFilingContent.AppendChild(xmlFilingContent.ImportNode(nodeToCheck, true));


            return await ValidateXhtml(xmlFilingContent, relaxedMode);
        }

        /// <summary>
        /// Verifies if the XML document is valid against the Xhtml schema
        /// </summary>
        /// <param name="xmlFilingContent"></param>
        /// <param name="relaxedMode"></param>
        /// <returns></returns> <summary>
        public static async Task<TaxxorLogReturnMessage> ValidateXhtml(XmlDocument xmlFilingContent, bool relaxedMode = false)
        {
            if (xmlFilingContent == null)
            {
                return new TaxxorLogReturnMessage(false, "xmlFilingContent is null");
            }
            else
            {
                string xhtmlContent = "";
                var needsToBeWrapped = xmlFilingContent?.DocumentElement?.LocalName == "article" || xmlFilingContent?.DocumentElement?.LocalName == "section" || xmlFilingContent?.DocumentElement?.LocalName == "div";

                if (needsToBeWrapped)
                {
                    // - Make sure that the most obvious node names (like 'article','section') are transformed to 'div'
                    XsltArgumentList xsltArgumentList = new();

                    var xmlContent = TransformXml(xmlFilingContent ?? new XmlDocument(), "xsd_validation", xsltArgumentList);

                    if (xmlContent == null)
                    {
                        return new TaxxorLogReturnMessage(false, "Transformation failed");
                    }
                    xhtmlContent = @"
        <html xml:lang=""en"" xmlns=""http://www.w3.org/1999/xhtml"">
          <head><title>XHTML test content</title></head>
          <body>
          " + xmlContent + @"
          </body>
        </html>";
                }
                else
                {
                    xhtmlContent = xmlFilingContent?.OuterXml ?? "";
                }

                return await ValidateXhtml(xhtmlContent, relaxedMode);

            }

        }



        /// <summary>
        /// Validates XHTML content against the W3C XHTML 1.0 Strict schema (with some minor relaxations)
        /// </summary>
        /// <param name="xhtmlContent"></param>
        /// <returns></returns>
        public static async Task<TaxxorLogReturnMessage> ValidateXhtml(string xhtmlContent, bool relaxedMode = false)
        {

            var errorMessage = "";
            var validationIssues = new List<string>();

            if (XhtmlSchemaSet == null)
            {
                appLogger.LogInformation("Retrieving XHTML schema for validation");
                (XhtmlSchemaSet, List<string> validationErrors) = ValidatingReader.GetSchemaSet(new XmlUrlResolver(), "http://www.w3.org/2002/08/xhtml/xhtml1-strict.xsd");
                if (XhtmlSchemaSet == null || validationErrors.Any())
                {
                    errorMessage = "Unable to retrieve XHTML schema for validation";
                    appLogger.LogError(errorMessage);
                    foreach (string error in validationErrors)
                    {
                        validationIssues.Add(error);
                    }

                    return new TaxxorLogReturnMessage
                    {
                        Success = false,
                        Message = errorMessage,
                        ErrorLog = validationIssues
                    };
                }
            }



            //
            // => Defines an attribute filter that removes the following attributes:
            //
            Func<(string elementName, string elementNs), (string attributeName, string attributeNs), string, string?>? attributeFilter = (element, attribute, value) =>
            {
                if (relaxedMode)
                {
                    return attribute.attributeName switch
                    {
                        var name when name.StartsWith("data-") => null,
                        var name when name == "_echarts_instance_" => null,
                        var name when name == "contenteditable" => null,
                        var name when name == "component" => null,
                        var name when name == "guid" => null,
                        var name when name == "class" && string.IsNullOrEmpty(value) => null,
                        var name when name == "id" && !string.IsNullOrEmpty(value) && char.IsDigit(value[0]) => null,
                        "target" when element.elementName == "a" => null,
                        "src" when element.elementName == "img" => string.Empty,
                        _ => value
                    };
                }
                else
                {
                    // Define stricter rules here
                    return attribute.attributeName switch
                    {
                        var name when name.StartsWith("data-") => null,
                        "src" when element.elementName == "img" => string.Empty,
                        _ => value
                    };
                }
            };


            Func<(string name, string ns), bool>? elementFilter = (element) =>
            {
                if (relaxedMode)
                {
                    return element.name != "svg" && element.name != "header" && element.name != "sidebar";
                }
                return true;
            };

            using ValidatingReader vr = new(xhtmlContent, XhtmlSchemaSet, elementFilter, attributeFilter);

            try
            {
                await vr.ValidateAsync();
                // or load using reader
                //XDocument document = await XDocument.LoadAsync(vr, LoadOptions.PreserveWhitespace, CancellationToken.None);
                if (vr.ValidationMessages.Any())
                {
                    foreach (IGrouping<string, (string element, int line, int position, System.Xml.Schema.XmlSeverityType severity, string message)> group in vr.ValidationMessages.GroupBy(x => x.message))
                    {
                        validationIssues.Add($" {group.First().severity} for element(s) {string.Join("/", group.Select(x => x.element).Distinct())}: {group.Key} ({string.Join("; ", group.Select(x => $"{x.line}, {x.position}"))})");
                    }

                    // appLogger.LogWarning($"Invalid XHTML detected: {string.Join("\n", validationIssues)}");

                    return new TaxxorLogReturnMessage
                    {
                        Success = false,
                        Message = "Document is invalid",
                        ErrorLog = validationIssues
                    };


                }
                else
                {

                    return new TaxxorLogReturnMessage
                    {
                        Success = true,
                        Message = "Document is valid",
                        ErrorLog = validationIssues
                    };
                }
            }
            catch (Exception e)
            {
                validationIssues.Add($"{e.GetType().Name}: {e.Message}");
                appLogger.LogWarning(e, $"Failed to validate XHTML content: {validationIssues.Last()}");

                return new TaxxorLogReturnMessage
                {
                    Success = false,
                    Message = "Document failed to validate",
                    ErrorLog = validationIssues
                };
            }

        }

        public static async Task<TaxxorLogReturnMessage> ValidateXml(XmlDocument xmlDoc, string xsdPathOs, bool relaxedMode = false)
        {

            var errorMessage = "";
            var validationIssues = new List<string>();

            appLogger.LogInformation("Retrieving schema for validation");
            (XhtmlSchemaSet, List<string> validationErrors) = (siteType == "prod") ?
                ValidatingReader.GetSchemaSet(new XmlUrlResolver(), xsdPathOs) :
                    ValidatingReader.GetSchemaSetNoCache(new XmlUrlResolver(), xsdPathOs);
            if (XhtmlSchemaSet == null || validationErrors.Any())
            {
                errorMessage = "Unable to retrieve XHTML schema for validation";
                appLogger.LogError(errorMessage);
                foreach (string error in validationErrors)
                {
                    validationIssues.Add(error);
                }

                return new TaxxorLogReturnMessage
                {
                    Success = false,
                    Message = errorMessage,
                    ErrorLog = validationIssues
                };
            }



            //
            // => Defines an attribute filter that removes the following attributes:
            //
            Func<(string elementName, string elementNs), (string attributeName, string attributeNs), string, string?>? attributeFilter = (element, attribute, value) =>
            {
                return value;
            };

            Func<(string name, string ns), bool>? elementFilter = (element) =>
            {
                if (relaxedMode)
                {
                    return element.name != "someelementthatwillnevermatch" && element.name != "anotherelementthatwillnevermatch";
                }
                return true;
            };

            using ValidatingReader vr = new(xmlDoc.OuterXml, XhtmlSchemaSet, elementFilter, attributeFilter);

            try
            {
                await vr.ValidateAsync();
                // or load using reader
                //XDocument document = await XDocument.LoadAsync(vr, LoadOptions.PreserveWhitespace, CancellationToken.None);
                if (vr.ValidationMessages.Any())
                {
                    foreach (IGrouping<string, (string element, int line, int position, System.Xml.Schema.XmlSeverityType severity, string message)> group in vr.ValidationMessages.GroupBy(x => x.message))
                    {
                        validationIssues.Add($" {group.First().severity} for element(s) {string.Join("/", group.Select(x => x.element).Distinct())}: {group.Key} ({string.Join("; ", group.Select(x => $"{x.line}, {x.position}"))})");
                    }

                    // appLogger.LogWarning($"Invalid XHTML detected: {string.Join("\n", validationIssues)}");

                    return new TaxxorLogReturnMessage
                    {
                        Success = false,
                        Message = "Document is invalid",
                        ErrorLog = validationIssues
                    };


                }
                else
                {

                    return new TaxxorLogReturnMessage
                    {
                        Success = true,
                        Message = "Document is valid",
                        ErrorLog = validationIssues
                    };
                }
            }
            catch (Exception e)
            {
                validationIssues.Add($"{e.GetType().Name}: {e.Message}");
                appLogger.LogWarning(e, $"Failed to validate XHTML content: {validationIssues.Last()}");

                return new TaxxorLogReturnMessage
                {
                    Success = false,
                    Message = "Document failed to validate",
                    ErrorLog = validationIssues
                };
            }

        }


    }
}

namespace XmlValidator
{
    public class ValidatingReader : XmlReader
    {
        private readonly XmlReader _reader;
        private readonly XmlSchemaValidator _validator;
        private readonly Stack<string> _elementStack = new();
        private readonly HashSet<string> _ids = [];

        private readonly Func<(string, string), bool>? _includeElement;
        private readonly Func<(string, string), (string, string), string, string?>? _includeAttribute;

        private int _suspended = -1;

        public List<(string element, int line, int position, XmlSeverityType severity, string message)> ValidationMessages { get; } = [];

        public ValidatingReader(string xmlContent, XmlSchemaSet schemaSet, Func<(string name, string ns), bool>? includeElement = null, Func<(string name, string ns), (string name, string ns), string, string?>? includeAttribute = null)
        {
            var stringReader = new StringReader(xmlContent);
            var settings = new XmlReaderSettings { Async = true };
            var reader = XmlReader.Create(stringReader, settings);
            (_reader, _includeElement, _includeAttribute) = (reader, includeElement, includeAttribute);
            _validator = new(reader.NameTable, schemaSet, (IXmlNamespaceResolver)reader, XmlSchemaValidationFlags.None) { LineInfoProvider = reader as IXmlLineInfo };
            _validator.ValidationEventHandler += ValidationHandler;
            _validator.Initialize();
        }

        public ValidatingReader(XmlReader reader, XmlSchemaSet schemaSet, Func<(string name, string ns), bool>? includeElement = null, Func<(string name, string ns), (string name, string ns), string, string?>? includeAttribute = null)
        {
            (_reader, _includeElement, _includeAttribute) = (reader, includeElement, includeAttribute);
            _validator = new(reader.NameTable, schemaSet, (IXmlNamespaceResolver)reader, XmlSchemaValidationFlags.None) { LineInfoProvider = reader as IXmlLineInfo };
            _validator.ValidationEventHandler += ValidationHandler;
            _validator.Initialize();
        }

        private void ValidationHandler(object? sender, ValidationEventArgs args)
        {
            // keep track of path to add to validation
            ValidationMessages.Add((_elementStack.Peek(), args.Exception.LineNumber, args.Exception.LinePosition, args.Severity, args.Message));
        }

        public override int AttributeCount => _reader.AttributeCount;
        public override string BaseURI => _reader.BaseURI;
        public override int Depth => _reader.Depth;
        public override bool EOF => _reader.EOF;
        public override bool IsEmptyElement => _reader.IsEmptyElement;
        public override string LocalName => _reader.LocalName;
        public override string NamespaceURI => _reader.NamespaceURI;
        public override XmlNameTable NameTable => _reader.NameTable;
        public override XmlNodeType NodeType => _reader.NodeType;
        public override string Prefix => _reader.Prefix;
        public override ReadState ReadState => _reader.ReadState;
        public override string Value => _reader.Value;

        public override string GetAttribute(int i) => _reader.GetAttribute(i);
        public override string? GetAttribute(string name) => _reader.GetAttribute(name);
        public override string? GetAttribute(string name, string? namespaceURI) => _reader.GetAttribute(name, namespaceURI);
        public override string? LookupNamespace(string prefix) => _reader.LookupNamespace(prefix);
        public override bool MoveToAttribute(string name) => _reader.MoveToAttribute(name);
        public override bool MoveToAttribute(string name, string? ns) => _reader.MoveToAttribute(name, ns);
        public override bool MoveToElement() => _reader.MoveToElement();
        public override bool MoveToFirstAttribute() => _reader.MoveToFirstAttribute();
        public override bool MoveToNextAttribute() => _reader.MoveToNextAttribute();

        public override bool Read()
        {
            if (!_reader.Read())
            {
                _validator.EndValidation();
                return false;
            }

            if (_reader.Depth >= (uint)_suspended)
            {
                if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == _suspended)
                    _suspended = -1;
                return true;
            }

            switch (_reader.NodeType)
            {
                case XmlNodeType.Element:
                    _elementStack.Push(_reader.Name);
                    string localName = _reader.LocalName;
                    string ns = _reader.NamespaceURI;
                    if (_includeElement != null && !_includeElement((localName, ns)))
                    {
                        _suspended = _reader.Depth;
                        break;
                    }
                    _validator.ValidateElement(localName, ns, null);
                    bool selfClosing = _reader.IsEmptyElement;
                    while (_reader.MoveToNextAttribute())
                    {
                        string? v = _reader.Value;
                        if (_reader.LocalName == "id" && !_ids.Add(v))
                            ValidationMessages.Add((_elementStack.Peek(), ((IXmlLineInfo)_reader).LineNumber, ((IXmlLineInfo)_reader).LinePosition, XmlSeverityType.Error, $"Duplicate id {v}"));
                        if (_includeAttribute == null || (v = _includeAttribute((localName, ns), (_reader.LocalName, _reader.NamespaceURI), v)) != null)
                            _validator.ValidateAttribute(_reader.LocalName, _reader.NamespaceURI, v, null);
                    }
                    _reader.MoveToElement();
                    _validator.ValidateEndOfAttributes(null);
                    if (selfClosing)
                    {
                        _validator.ValidateEndElement(null);
                        _elementStack.Pop();
                    }
                    break;
                case XmlNodeType.EndElement:
                    _validator.ValidateEndElement(null);
                    _elementStack.Pop();
                    break;
                case XmlNodeType.Text:
                    _validator.ValidateText(_reader.Value);
                    break;
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    _validator.ValidateWhitespace(_reader.Value);
                    break;
            }
            return true;
        }

        public override async Task<bool> ReadAsync()
        {
            if (!await _reader.ReadAsync())
            {
                _validator.EndValidation();
                return false;
            }

            if (_reader.Depth >= (uint)_suspended)
            {
                if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == _suspended)
                    _suspended = -1;
                return true;
            }

            switch (_reader.NodeType)
            {
                case XmlNodeType.Element:
                    _elementStack.Push(_reader.Name);
                    string localName = _reader.LocalName;
                    string ns = _reader.NamespaceURI;
                    if (_includeElement != null && !_includeElement((localName, ns)))
                    {
                        _suspended = _reader.Depth;
                        break;
                    }
                    _validator.ValidateElement(localName, ns, null);
                    bool selfClosing = _reader.IsEmptyElement;
                    while (_reader.MoveToNextAttribute())
                    {
                        string? v = await _reader.GetValueAsync();
                        if (_reader.LocalName == "id" && !_ids.Add(v))
                            ValidationMessages.Add((_elementStack.Peek(), ((IXmlLineInfo)_reader).LineNumber, ((IXmlLineInfo)_reader).LinePosition, XmlSeverityType.Error, $"Duplicate id {v}"));
                        if (_includeAttribute == null || (v = _includeAttribute((localName, ns), (_reader.LocalName, _reader.NamespaceURI), v)) != null)
                            _validator.ValidateAttribute(_reader.LocalName, _reader.NamespaceURI, v, null);
                    }
                    _reader.MoveToElement();
                    _validator.ValidateEndOfAttributes(null);
                    if (selfClosing)
                    {
                        _validator.ValidateEndElement(null);
                        _elementStack.Pop();
                    }
                    break;
                case XmlNodeType.EndElement:
                    _validator.ValidateEndElement(null);
                    _elementStack.Pop();
                    break;
                case XmlNodeType.Text:
                    _validator.ValidateText(await _reader.GetValueAsync());
                    break;
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    _validator.ValidateWhitespace(await _reader.GetValueAsync());
                    break;
            }
            return true;
        }

        public override bool ReadAttributeValue() => _reader.ReadAttributeValue();
        public override void ResolveEntity() => _reader.ResolveEntity();

        public override Task<string> GetValueAsync() => _reader.GetValueAsync();

        public void Validate()
        {
            while (Read()) ;
        }
        public async Task ValidateAsync()
        {
            while (await ReadAsync()) ;
        }

        public static (XmlSchemaSet? schemaSet, List<string> validationErrors) GetSchemaSetNoCache(XmlResolver resolver, params string[] schemas)
        {
            return _GetSchemaSet(resolver, false, schemas);
        }

        public static (XmlSchemaSet? schemaSet, List<string> validationErrors) GetSchemaSet(XmlResolver resolver, params string[] schemas)
        {
            return _GetSchemaSet(resolver, true, schemas);
        }

        private static Dictionary<string, (XmlSchemaSet, List<string>)> _schemaSets = [];
        private static (XmlSchemaSet? schemaSet, List<string> validationErrors) _GetSchemaSet(XmlResolver resolver, bool useCache, params string[] schemas)
        {
            lock (_schemaSets)
            {
                string key = string.Join(";", schemas);
                if (useCache && _schemaSets.TryGetValue(key, out (XmlSchemaSet schemaSet, List<string> validationErrors) result))
                    return result;

                List<string> validationErrors = [];
                try
                {
                    XmlSchemaSet schemaSet = new XmlSchemaSet() { XmlResolver = resolver };
                    foreach (string schemaUri in schemas)
                    {
                        using XmlReader reader = Create(schemaUri, new XmlReaderSettings() { XmlResolver = resolver });
                        XmlSchema schema = XmlSchema.Read(reader, (s, e) => validationErrors.Add($"{schemaUri}: {e.Message}")) ?? throw new XmlSchemaException($"Schema {schemaUri} could not be read");
                        schemaSet.Add(schema);
                    }
                    schemaSet.Compile();
                    result = (schemaSet, validationErrors);
                    if (useCache) _schemaSets[key] = result;
                }
                catch (Exception e)
                {
                    validationErrors.Add($"{e.GetType().Name}: {e.Message}");
                    result = (null!, validationErrors);
                }
                return result;
            }
        }
    }


}
