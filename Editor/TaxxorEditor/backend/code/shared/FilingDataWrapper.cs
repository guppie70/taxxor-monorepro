using System;
using System.Threading.Tasks;
using System.Xml;
using DocumentStore.Protos;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Wrapper around the FilingData gRPC client to provide a simplified interface
/// for FilingData static methods to use instead of calling REST endpoints directly.
/// Bridges gRPC responses to REST-compatible XML format for backward compatibility.
/// </summary>
public static class FilingDataWrapper
{
    /// <summary>
    /// Retrieves file contents from the DocumentStore via gRPC
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="path">File path</param>
    /// <param name="relativeTo">Relative to location (e.g., "projectimages")</param>
    /// <param name="debug">Debug mode flag</param>
    /// <returns>XML document containing the file response</returns>
    public static async Task<XmlDocument> GetFileContentsAsync(string projectId, string path, string relativeTo, bool debug = false)
    {
        try
        {
            var context = System.Web.Context.Current;
            if (context == null)
            {
                return GenerateErrorXml("No HTTP context available", "Cannot retrieve gRPC client without HTTP context");
            }

            // Get the gRPC client from DI
            var client = context.RequestServices.GetRequiredService<FilingDataService.FilingDataServiceClient>();

            // Create the gRPC request
            var grpcRequest = new FileContentsRequest
            {
                LocationId = projectId,
                Path = path,
                RelativeTo = relativeTo
            };

            // Call the gRPC service
            var grpcResponse = await client.GetFileContentsAsync(grpcRequest);

            if (grpcResponse.Success)
            {
                // Convert the gRPC response to the REST-compatible format
                // The REST endpoint returns an XML envelope with /result/message and /result/debuginfo
                var xmlResult = new XmlDocument();
                xmlResult.AppendChild(xmlResult.CreateElement("result"));

                var messageNode = xmlResult.CreateElement("message");
                messageNode.InnerText = grpcResponse.Data;
                xmlResult.DocumentElement.AppendChild(messageNode);

                var debugNode = xmlResult.CreateElement("debuginfo");
                debugNode.InnerText = grpcResponse.Debuginfo ?? path;
                xmlResult.DocumentElement.AppendChild(debugNode);

                return xmlResult;
            }
            else
            {
                return GenerateErrorXml(
                    $"Error retrieving file contents from gRPC service: {grpcResponse.Message}",
                    grpcResponse.Debuginfo ?? "No additional debug info"
                );
            }
        }
        catch (Exception ex)
        {
            return GenerateErrorXml(
                $"Exception in GetFileContentsAsync: {ex.Message}",
                $"Stack trace: {ex.StackTrace}"
            );
        }
    }

    /// <summary>
    /// Stores file contents to the DocumentStore via gRPC
    /// </summary>
    public static async Task<XmlDocument> PutFileContentsAsync(string projectId, string path, string relativeTo, string data, bool debug = false)
    {
        try
        {
            var context = System.Web.Context.Current;
            if (context == null)
            {
                return GenerateErrorXml("No HTTP context available", "Cannot retrieve gRPC client without HTTP context");
            }

            var client = context.RequestServices.GetRequiredService<FilingDataService.FilingDataServiceClient>();

            var grpcRequest = new PutFileContentsRequest
            {
                LocationId = projectId,
                Path = path,
                RelativeTo = relativeTo,
                Data = data
            };

            var grpcResponse = await client.PutFileContentsAsync(grpcRequest);

            if (grpcResponse.Success)
            {
                var xmlResult = new XmlDocument();
                xmlResult.AppendChild(xmlResult.CreateElement("result"));

                var messageNode = xmlResult.CreateElement("message");
                messageNode.InnerText = grpcResponse.Message;
                xmlResult.DocumentElement.AppendChild(messageNode);

                var debugNode = xmlResult.CreateElement("debuginfo");
                debugNode.InnerText = grpcResponse.Debuginfo ?? path;
                xmlResult.DocumentElement.AppendChild(debugNode);

                return xmlResult;
            }
            else
            {
                return GenerateErrorXml(
                    $"Error storing file contents via gRPC: {grpcResponse.Message}",
                    grpcResponse.Debuginfo ?? "No additional debug info"
                );
            }
        }
        catch (Exception ex)
        {
            return GenerateErrorXml(
                $"Exception in PutFileContentsAsync: {ex.Message}",
                $"Stack trace: {ex.StackTrace}"
            );
        }
    }

    /// <summary>
    /// Helper method to generate error XML in the format FilingData expects
    /// </summary>
    private static XmlDocument GenerateErrorXml(string message, string debugInfo)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.AppendChild(xmlDoc.CreateElement("error"));

        var messageNode = xmlDoc.CreateElement("message");
        messageNode.InnerText = message;
        xmlDoc.DocumentElement.AppendChild(messageNode);

        var debugNode = xmlDoc.CreateElement("debuginfo");
        debugNode.InnerText = debugInfo;
        xmlDoc.DocumentElement.AppendChild(debugNode);

        return xmlDoc;
    }
}
