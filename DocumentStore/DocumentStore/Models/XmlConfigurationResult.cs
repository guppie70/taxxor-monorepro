using System;
using System.Xml;

namespace DocumentStore.Models
{
    /// <summary>
    /// Result class for XML configuration operations
    /// </summary>
    public class XmlConfigurationResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public string DebugInfo { get; set; }
        public int StatusCode { get; set; } = 200;
        public XmlDocument XmlResponse { get; set; }
    }
}
