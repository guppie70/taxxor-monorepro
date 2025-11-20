using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Linq;
using Newtonsoft.Json;

namespace Taxxor.Project
{

    /// <summary>
    /// Temporary location for methods that interact with the (Philips) Render Engine
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {
        private static HttpClient _httpXsltRequestClient = null;

        /// <summary>
        /// Uses the render engine to convert an XmlDocument to lorem ipsum content
        /// </summary>
        /// <param name="xmlDocToConvert"></param>
        /// <param name="language"></param>
        /// <param name="xsltReference"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> ConvertToLoremIpsum(XmlDocument xmlDocToConvert, string language, string xsltReference = "renderengine-loremipsum")
        {
            var xsltParameters = new NameValueCollection
            {
                { "replace-lang", (language == "zh") ? "chinese-simplified" : "lorem" }
            };

            return await Xslt3TransformXmlToDocument(xmlDocToConvert, xsltReference, xsltParameters);
        }

        /// <summary>
        /// XSLT 2 Transformation using the Render Service as the engine for the transformation
        /// </summary>
        /// <param name="xmlDocToConvert"></param>
        /// <param name="xsltReference"></param>
        /// <returns></returns>
        public static async Task<string> Xslt3TransformXml(XmlDocument xmlDocToConvert, string xsltReference)
        {
            var xsltParameters = new NameValueCollection();
            var xmlTransformed = await Xslt3TransformXmlToDocument(xmlDocToConvert, xsltReference, xsltParameters);

            if (XmlContainsError(xmlTransformed))
            {
                return $"ERROR: message: {xmlTransformed.SelectSingleNode("//message")?.InnerText ?? "no error message"}, debuginfo: {xmlTransformed.SelectSingleNode("//debuginfo")?.InnerText ?? "no error details"}";
            }
            else
            {
                return xmlTransformed.OuterXml;
            }
        }

        /// <summary>
        /// XSLT 2 Transformation using the Render Service as the engine for the transformation
        /// </summary>
        /// <param name="xmlDocToConvert"></param>
        /// <param name="xsltReference"></param>
        /// <returns>Conversion result as a string</returns>
        public static async Task<string> Xslt3TransformXml(XmlDocument xmlDocToConvert, string xsltReference, NameValueCollection xsltParameters)
        {
            var xmlTransformed = await Xslt3TransformXmlToDocument(xmlDocToConvert, xsltReference, xsltParameters);

            if (XmlContainsError(xmlTransformed))
            {
                return $"ERROR: message: {xmlTransformed.SelectSingleNode("//message")?.InnerText ?? "no error message"}, debuginfo: {xmlTransformed.SelectSingleNode("//debuginfo")?.InnerText ?? "no error details"}";
            }
            else
            {
                return xmlTransformed.OuterXml;
            }
        }

        /// <summary>
        /// XSLT 2 Transformation using the Render Service as the engine for the transformation
        /// </summary>
        /// <param name="xmlDocToConvert"></param>
        /// <param name="xsltReference"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> Xslt3TransformXmlToDocument(XmlDocument xmlDocToConvert, string xsltReference)
        {
            var xsltParameters = new NameValueCollection();
            return await Xslt3TransformXmlToDocument(xmlDocToConvert, xsltReference, xsltParameters);
        }

        /// <summary>
        /// XSLT 2 Transformation using the Render Service as the engine for the transformation
        /// </summary>
        /// <param name="xmlDocToConvert"></param>
        /// <param name="xsltReference"></param>
        /// <param name="xsltParameters"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> Xslt3TransformXmlToDocument(XmlDocument xmlDocToConvert, string xsltReference, NameValueCollection xsltParameters)
        {
            try
            {
                await DummyAwaiter();

                var xsltPathOs = xsltReference.Contains('/') ? xsltReference : CalculateFullPathOs(xsltReference);

                if (string.IsNullOrEmpty(xsltPathOs))
                {
                    return GenerateErrorXml("Could not resolve path to xslt stylesheet", $"xsltReference: {xsltReference}, stack-trace: {GetStackTrace()}");
                }

                if (xmlDocToConvert == null)
                {
                    return GenerateErrorXml("Cannot transform an empty XML document", $"stack-trace: {GetStackTrace()}");
                }

                // Call the render engine and convert the document
                // using var client = new HttpClient();
                var baseUri = "http://xslt3service:4806";
                var formDataContent = new MultipartFormDataContent();


                //
                // => Query example
                //

                // Dictionary<string, string> dataToPost = new()
                // {
                //     { "xsl","current-dateTime()" },
                //     { "output", "method=text" }
                // };                
                // foreach(var key in dataToPost){
                //     formDataContent.Add(new StringContent(key.Value), key.Key);
                // }
                // var content = new FormUrlEncodedContent(dataToPost);
                // var fullUri = $"{baseUri}/query";
                // var response = await client.PostAsync(fullUri, formDataContent);
                // var responseString = await response.Content.ReadAsStringAsync();
                // var renderEngineResult = new XmlDocument();
                // renderEngineResult.AppendChild(renderEngineResult.CreateElement("result"));
                // renderEngineResult.DocumentElement.InnerXml = responseString;

                //
                // => Transformation example
                //

                // - Add XML content as a form data field
                var xmlContent = new ByteArrayContent(Encoding.UTF8.GetBytes(xmlDocToConvert.OuterXml));
                formDataContent.Add(xmlContent, "xml");

                // - Add the XSLT path as a form data field
                using (FileStream fileStream = File.OpenRead(xsltPathOs))
                using (StreamReader streamReader = new(fileStream))
                {
                    string xsltContent = streamReader.ReadToEnd();
                    var xsltContentContent = new StringContent(xsltContent);
                    formDataContent.Add(xsltContentContent, "xsl");
                }

                // - Optionally add XSLT parameters
                if (xsltParameters.Count > 0)
                {
                    string parametersString = string.Join(";", xsltParameters.AllKeys.Select(key => $"{key}={xsltParameters[key]}"));
                    var parametersContent = new StringContent(parametersString);
                    formDataContent.Add(parametersContent, "parameters");
                }


                //
                // => Execute the request
                //
                var fullUri = $"{baseUri}/transform";
                _httpXsltRequestClient ??= new HttpClient();
                var response = await _httpXsltRequestClient.PostAsync(fullUri, formDataContent);

                if (!response.IsSuccessStatusCode)
                {
                    string errorString = await response.Content.ReadAsStringAsync();
                    try
                    {
                        dynamic? jsonResponse = JsonConvert.DeserializeObject(errorString);
                        throw new HttpRequestException($"HTTP Response code: {jsonResponse.statusCode}, Exception type: {jsonResponse.exceptionType}, Message: {jsonResponse.message}");
                    }
                    catch (JsonReaderException)
                    {
                        throw new HttpRequestException($"HTTP Status Code: {response.StatusCode}");
                    }
                }

                //
                // => Parse the response
                //
                var renderEngineResult = new XmlDocument();
                var responseString = await response.Content.ReadAsStringAsync();
                if (!responseString.Trim().StartsWith('<') || !responseString.Trim().EndsWith('>'))
                {
                    renderEngineResult.AppendChild(renderEngineResult.CreateElement("result"));
                    renderEngineResult.DocumentElement.InnerXml = responseString;
                }
                else
                {
                    renderEngineResult.LoadXml(responseString);
                }

                return renderEngineResult;
            }
            catch (HttpRequestException ex)
            {
                appLogger.LogError($"Error during XSLT transformation (xsltReference: {xsltReference}):\n{ex.Message}");
                return GenerateErrorXml($"Request to XSLT render engine failed", $"xsltReference: {xsltReference}, stack-trace: {GetStackTrace()}");
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error during XSLT transformation: {ex.Message}");
                return GenerateErrorXml("Could not transform XML document", $"xsltReference: {xsltReference}, stack-trace: {GetStackTrace()}");
            }
        }



    }

}