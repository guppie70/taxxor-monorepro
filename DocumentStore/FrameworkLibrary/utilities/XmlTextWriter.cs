using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace DocumentStore.backend.framework.utilities
{
    public class XmlHtmlWriter : XmlTextWriter
    {
        private string openingElement = string.Empty;

        // Use HashSet instead of List for O(1) lookups
        private static readonly HashSet<string> selfClosingElements = new HashSet<string>(
            new[] { "area", "base", "basefont", "br", "col", "embed", "hr",
                "input", "img", "keygen", "link", "meta", "param",
                "source", "track", "wbr" },
            StringComparer.OrdinalIgnoreCase); // Case insensitive comparison

        public XmlHtmlWriter(System.IO.Stream stream, Encoding en) : base(stream, en)
        {
            // No need to populate the collection here as it's now static and pre-populated
        }

        public override void WriteEndElement()
        {
            if (!selfClosingElements.Contains(openingElement))
            {
                WriteFullEndElement();
            }
            else
            {
                base.WriteEndElement();
            }
        }

        public override void WriteStartElement(string? prefix, string localName, string? ns)
        {
            base.WriteStartElement(prefix, localName, ns);
            openingElement = localName;
        }
    }
}