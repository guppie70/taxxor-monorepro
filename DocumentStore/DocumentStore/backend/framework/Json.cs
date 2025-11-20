using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Contains helper functions for working with JSON
/// </summary>
public abstract partial class Framework
{
    /// <summary>
    /// Extremely simple function to chack if the string is valid JSON or not
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return false;

        // Avoid string allocation from Trim() for large strings
        int start = 0;
        int end = json.Length - 1;

        // Manual trimming to avoid string allocation
        while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
        while (end >= start && char.IsWhiteSpace(json[end])) end--;

        if (start > end) return false;

        // Quick check for JSON structure before parsing
        if (!((json[start] == '{' && json[end] == '}') ||
              (json[start] == '[' && json[end] == ']')))
        {
            return false;
        }

        try
        {
            // Use a custom StringReader that doesn't hold the entire string
            using (var stringReader = new LimitedStringReader(json, start, end - start + 1))
            using (var reader = new JsonTextReader(stringReader))
            {
                // Minimize memory usage
                reader.DateParseHandling = DateParseHandling.None;
                reader.MaxDepth = 64;
                reader.CloseInput = true;

                // Read through the JSON without materializing objects
                while (reader.Read()) { }

                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Custom StringReader that only holds a reference to the original string
    // and doesn't create a copy of the trimmed portion
    private class LimitedStringReader : System.IO.TextReader
    {
        private readonly string _source;
        private readonly int _start;
        private readonly int _length;
        private int _position;

        public LimitedStringReader(string source, int start, int length)
        {
            _source = source;
            _start = start;
            _length = length;
            _position = 0;
        }

        public override int Read()
        {
            if (_position >= _length)
                return -1;

            return _source[_start + _position++];
        }

        public override int Read(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (index < 0 || count < 0)
                throw new ArgumentOutOfRangeException(index < 0 ? nameof(index) : nameof(count));

            if (buffer.Length - index < count)
                throw new ArgumentException("Invalid length");

            if (_position >= _length)
                return 0;

            int charsToRead = Math.Min(count, _length - _position);
            for (int i = 0; i < charsToRead; i++)
                buffer[index + i] = _source[_start + _position + i];

            _position += charsToRead;
            return charsToRead;
        }

        protected override void Dispose(bool disposing)
        {
            // Nothing to dispose
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Converts json string data to Dictionary<string, string> object
    /// </summary>
    /// <param name="json">json string value</param>
    /// <returns></returns>
    public static Dictionary<string, string>? ConvertJsonToDictionary(string json)
    {
        return JsonConvert.DeserializeObject<Dictionary<String, String>>(json);
    }


    /// <summary>
    /// Converts JSON to an XML Document
    /// </summary>
    /// <param name="json">json string value</param>
    /// <returns></returns>
    public static XmlDocument ConvertJsonToXml(string json)
    {
        return (XmlDocument)JsonConvert.DeserializeXmlNode(json);
    }


    /// <summary>
    /// Converts JSON to an XML Document with a specified root element name
    /// </summary>
    public static XmlDocument? ConvertJsonToXml(string jsonString, string rootElementName)
    {
        try
        {
            return (XmlDocument)JsonConvert.DeserializeXmlNode(jsonString, rootElementName);
        }
        catch (Exception ex)
        {
            var errorMessage = "Error converting JSON to XML";
            appLogger.LogError(ex, errorMessage);

            return GenerateErrorXml(errorMessage, ex.ToString());
        }
    }


    /// <summary>
    /// See below for an example of the xml to json conversion:
    /// string xml = @"<?xml version=""1.0"" standalone=""no""?>
    ///<root>
    ///  <person id=""1"">
    ///        <name>Alan</name>
    ///        <url>http://www.google.com</url>
    ///  </person>
    ///  <person id=""2"">
    ///        <name>Louis</name>
    ///        <url>http://www.yahoo.com</url>
    ///  </person>
    ///</root>";
    /// 
    ///XmlDocument doc = new XmlDocument();
    ///doc.LoadXml(xml);
    ///
    ///string jsonText = JsonConvert.SerializeXmlNode(doc);
    ///{
    ///  "?xml": {
    ///    "@version": "1.0",
    ///    "@standalone": "no"
    ///  },
    ///  "root": {
    ///    "person": [
    ///      {
    ///        "@id": "1",
    ///        "name": "Alan",
    ///        "url": "http://www.google.com"
    ///      },
    ///      {
    ///        "@id": "2",
    ///        "name": "Louis",
    ///        "url": "http://www.yahoo.com"
    ///      }
    ///    ]
    ///  }
    ///}

    /// <summary>
    /// Converts a XmlDocument to a JSON string
    /// </summary>
    /// <param name="xml">XmlDocument as input</param>
    /// <param name="formatting">Newtonsoft.Json.Formatting options for the JSON output</param>
    /// <param name="omitRootObject">Omit the root node when generating the JSON</param>
    /// <param name="parseValues">Attempts to generate JSON that respects the numeric values in the XML so that &lt;foo%gt;120&lt;/foo%gt; converts to "foo": 120 instead of "foo": "120"</param>
    /// <returns></returns>
    public static string ConvertToJson(XmlDocument xml, Newtonsoft.Json.Formatting formatting = Newtonsoft.Json.Formatting.None, bool omitRootObject = false, bool parseValues = false)
    {
        if (parseValues)
        {
            var obj = JObject.Parse(JsonConvert.SerializeXmlNode(xml, formatting, omitRootObject));
            foreach (var value in obj.Descendants().OfType<JValue>().Where(v => v.Type == JTokenType.String))
            {
                if (long.TryParse((string)value, out long lVal))
                {
                    value.Value = lVal;
                    continue;
                }
                if (double.TryParse((string)value, out double dVal))
                {
                    value.Value = dVal;
                    continue;
                }
                if (decimal.TryParse((string)value, out decimal dcVal))
                {
                    value.Value = dcVal;
                    continue;
                }
                if ((string)value == "true")
                {
                    value.Value = true;
                    continue;
                }
                if ((string)value == "false")
                {
                    value.Value = false;
                    continue;
                }
            }
            return obj.ToString();
        }
        else
        {
            return JsonConvert.SerializeXmlNode(xml, formatting, omitRootObject);
        }



    }

    /// <summary>
    /// Converts an object to a JSON string
    /// </summary>
    /// <param name="data"></param>
    /// <param name="formatting"></param>
    /// <returns></returns>
    public static string ConvertToJson(Object data, Newtonsoft.Json.Formatting formatting = Newtonsoft.Json.Formatting.None)
    {
        return JsonConvert.SerializeObject(data, formatting);
    }

    /// <summary>
    /// Converts a XmlNode to a JSON string
    /// </summary>
    /// <param name="xml">XmlDocument as input</param>
    /// <param name="formatting">Newtonsoft.Json.Formatting options for the JSON output</param>
    /// <param name="omitRootObject">Omit the root node when generating the JSON</param>
    /// <returns></returns>
    public static string ConvertToJson(XmlNode node, Newtonsoft.Json.Formatting formatting = Newtonsoft.Json.Formatting.None, bool omitRootObject = false)
    {
        return JsonConvert.SerializeXmlNode(node, formatting, omitRootObject);
    }

    /// <summary>
    /// Wraps the json output in JSONP format if needed
    /// </summary>
    /// <param name="jsonResult"></param>
    /// <param name="callbackJsFunction">The javascript function in the client that needs to be executed</param>
    /// <returns></returns>
    public static string WrapJsonPIfNeeded(string jsonResult, string? callbackJsFunction)
    {
        string jsonReturn = jsonResult;
        // TODO: breaking change from earlier implementations
        // string callbackJsFunction = RetrievePostedValue("callback");
        if (callbackJsFunction != null)
        {
            jsonReturn = callbackJsFunction + "(" + jsonResult + ")";
        }
        return jsonReturn;
    }

    /// <summary>
    /// Wraps the json output in JSONP format if needed
    /// </summary>
    /// <returns>The json.</returns>
    /// <param name="jsonResult">Json result.</param>
    public static string WrapJsonPIfNeeded(string jsonResult)
    {
        var context = System.Web.Context.Current;
        var callbackJsFunction = context.Request.RetrievePostedValue("callback", null);
        return WrapJsonPIfNeeded(jsonResult, callbackJsFunction);
    }
}