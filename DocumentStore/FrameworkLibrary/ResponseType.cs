using System.Net.Mime;
using FrameworkLibrary.models;

namespace FrameworkLibrary
{

    /// <summary>
    /// Contains helper functions for rendering responses to the client
    /// </summary>
    public static class FrameworkResponseType
    {
        /// Returns ReturnTypeEnum based on string input (xml, json, html, txt) or MIME type (text/xml)
        /// </summary>
        /// <param name="returnType">string input (xml, json, html, txt)</param>
        /// <returns></returns>
        public static ReturnTypeEnum GetReturnTypeEnum(string returnType)
        {
            return returnType switch
            {
                "xml" or MediaTypeNames.Text.Xml => ReturnTypeEnum.Xml,
                "json" or MediaTypeNames.Application.Json => ReturnTypeEnum.Json,
                "html" or MediaTypeNames.Text.Html => ReturnTypeEnum.Html,
                "xhtml" or "application/xhtml+xml" => ReturnTypeEnum.Xhtml,
                "js" or MediaTypeNames.Text.JavaScript => ReturnTypeEnum.Js,
                MediaTypeNames.Text.Plain => ReturnTypeEnum.Txt,
                "*/*" or "none" => ReturnTypeEnum.None,
                _ => ReturnTypeEnum.Txt,
            };
        }

        /// <summary>
        /// Converts a ReturnTypeEnum value to a string
        /// </summary>
        /// <param name="returnTypeEnum"></param>
        /// <returns></returns>
        public static string ReturnTypeEnumToString(ReturnTypeEnum returnTypeEnum)
        {
            return returnTypeEnum switch
            {
                ReturnTypeEnum.Xml => "xml",
                ReturnTypeEnum.Json => "json",
                ReturnTypeEnum.Html => "html",
                ReturnTypeEnum.Xhtml => "xhtml",
                ReturnTypeEnum.Txt => "txt",
                ReturnTypeEnum.Js => "js",
                ReturnTypeEnum.None => "none",
                _ => "txt",
            };
        }

        /// <summary>
        /// Maps a ReturnTypeEnum to a MIME type
        /// </summary>
        /// <returns>The type enum to MIME.</returns>
        /// <param name="returnTypeEnum">Return type enum.</param>
        public static string ReturnTypeEnumToMime(ReturnTypeEnum returnTypeEnum)
        {
            return returnTypeEnum switch
            {
                ReturnTypeEnum.Xml => MediaTypeNames.Text.Xml,
                ReturnTypeEnum.Json => MediaTypeNames.Application.Json,
                ReturnTypeEnum.Html => MediaTypeNames.Text.Html,
                ReturnTypeEnum.Xhtml => "application/xhtml+xml",
                ReturnTypeEnum.Js => MediaTypeNames.Text.JavaScript,
                ReturnTypeEnum.None => "*/*",
                ReturnTypeEnum.Txt => MediaTypeNames.Text.Plain,
                _ => MediaTypeNames.Text.Plain,
            };
        }

    }
}