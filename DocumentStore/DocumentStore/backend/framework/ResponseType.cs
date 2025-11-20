using System.Net.Mime;

/// <summary>
/// Contains helper functions for rendering responses to the client
/// </summary>
public abstract partial class Framework
{
    /// <summary>
    /// Defines the type of response that we want to return to the client
    /// </summary>
    public enum ReturnTypeEnum
    {
        Xml,
        Json,
        Html,
        Xhtml,
        Txt,
        Js,
        None
    }

    /// <summary>
    /// Returns ReturnTypeEnum based on string input (xml, json, html, txt) or MIME type (text/xml)
    /// </summary>
    /// <param name="returnType">string input (xml, json, html, txt)</param>
    /// <returns></returns>
    public static ReturnTypeEnum GetReturnTypeEnum(string returnType)
    {
        switch (returnType)
        {
            case "xml":
            case MediaTypeNames.Text.Xml:
                return ReturnTypeEnum.Xml;
            case "json":
            case MediaTypeNames.Application.Json:
                return ReturnTypeEnum.Json;
            case "html":
            case MediaTypeNames.Text.Html:
                return ReturnTypeEnum.Html;
            case "xhtml":
            case "application/xhtml+xml":
                return ReturnTypeEnum.Xhtml;
            case "js":
            case MediaTypeNames.Text.JavaScript:
                return ReturnTypeEnum.Js;
            case MediaTypeNames.Text.Plain:
                return ReturnTypeEnum.Txt;
            case "*/*":
            case "none":
                return ReturnTypeEnum.None;
            default:
                return ReturnTypeEnum.Txt;
        }
    }

    /// <summary>
    /// Converts a ReturnTypeEnum value to a string
    /// </summary>
    /// <param name="returnTypeEnum"></param>
    /// <returns></returns>
    public static string ReturnTypeEnumToString(ReturnTypeEnum returnTypeEnum)
    {
        switch (returnTypeEnum)
        {
            case ReturnTypeEnum.Xml:
                return "xml";
            case ReturnTypeEnum.Json:
                return "json";
            case ReturnTypeEnum.Html:
                return "html";
            case ReturnTypeEnum.Xhtml:
                return "xhtml";
            case ReturnTypeEnum.Txt:
                return "txt";
            case ReturnTypeEnum.Js:
                return "js";
            case ReturnTypeEnum.None:
                return "none";
            default:
                return "txt";
        }
    }

    /// <summary>
    /// Maps a ReturnTypeEnum to a MIME type
    /// </summary>
    /// <returns>The type enum to MIME.</returns>
    /// <param name="returnTypeEnum">Return type enum.</param>
    public static string ReturnTypeEnumToMime(ReturnTypeEnum returnTypeEnum)
    {
        switch (returnTypeEnum)
        {
            case ReturnTypeEnum.Xml:
                return "text/xml";
            case ReturnTypeEnum.Json:
                return "application/json";
            case ReturnTypeEnum.Html:
                return "text/html";
            case ReturnTypeEnum.Xhtml:
                return "application/xhtml+xml";
            case ReturnTypeEnum.Txt:
                return "text/plain";
            case ReturnTypeEnum.Js:
                return "text/javascript";
            case ReturnTypeEnum.None:
                return "*/*";
            default:
                return "text/plain";
        }
    }

}