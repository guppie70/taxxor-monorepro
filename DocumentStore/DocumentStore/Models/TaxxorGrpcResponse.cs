using System;
using Google.Protobuf;

namespace DocumentStore.Models
{
    /// <summary>
    /// Standard response message used across all services
    /// </summary>
    public class TaxxorGrpcResponse
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Human-readable message about the operation
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Additional debug information (not typically shown to end users)
        /// </summary>
        public string DebugInfo { get; set; }

        /// <summary>
        /// String data payload (e.g., JSON, XML)
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Binary data payload
        /// </summary>
        public byte[] Binary { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public TaxxorGrpcResponse()
        {
            Success = false;
            Message = string.Empty;
            DebugInfo = string.Empty;
            Data = string.Empty;
            Binary = null;
        }

        /// <summary>
        /// Constructor with success and message
        /// </summary>
        public TaxxorGrpcResponse(bool success, string message)
        {
            Success = success;
            Message = message;
            DebugInfo = string.Empty;
            Data = string.Empty;
            Binary = null;
        }

        /// <summary>
        /// Constructor with success, message, and data
        /// </summary>
        public TaxxorGrpcResponse(bool success, string message, string data)
        {
            Success = success;
            Message = message;
            Data = data;
            DebugInfo = string.Empty;
            Binary = null;
        }

        /// <summary>
        /// Constructor with all parameters
        /// </summary>
        public TaxxorGrpcResponse(bool success, string message, string debugInfo, string data, byte[] binary)
        {
            Success = success;
            Message = message;
            DebugInfo = debugInfo;
            Data = data;
            Binary = binary;
        }

        /// <summary>
        /// Create a success response with data
        /// </summary>
        public static TaxxorGrpcResponse CreateSuccess(string message, string data = "")
        {
            return new TaxxorGrpcResponse(true, message, data);
        }

        /// <summary>
        /// Create an error response
        /// </summary>
        public static TaxxorGrpcResponse CreateError(string message, string debugInfo = "")
        {
            var response = new TaxxorGrpcResponse(false, message);
            response.DebugInfo = debugInfo;
            return response;
        }

        /// <summary>
        /// Create an error response from an exception
        /// </summary>
        public static TaxxorGrpcResponse CreateError(string message, Exception ex)
        {
            var response = new TaxxorGrpcResponse(false, message);
            response.DebugInfo = $"Exception: {ex.ToString()}";
            return response;
        }
    }
}