using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Exposes the system state
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        // MIGRATED - CAN BE REMOVED
        public async static Task RetrieveSystemState(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);

            // Call the core logic method
            var result = await RetrieveSystemStateCore(reqVars);
            
            // Return the response
            if (result.Success)
            {
                await response.OK(result.ResponseContent, reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
        }

        /// <summary>
        /// Core logic for retrieving system state without HTTP dependencies
        /// </summary>
        /// <param name="reqVars">Request variables</param>
        /// <returns>System state result</returns>
        public async static Task<SystemStateResult> RetrieveSystemStateCore(RequestVariables reqVars)
        {
            var result = new SystemStateResult();

            await DummyAwaiter();
            
            try
            {
                // Render the response based on return type
                switch (reqVars.returnType)
                {
                    case ReturnTypeEnum.Xml:
                        result.ResponseContent = SystemState.ToXml();
                        result.Success = true;
                        break;

                    case ReturnTypeEnum.Json:
                        result.ResponseContent = SystemState.ToJson();
                        result.Success = true;
                        break;

                    default:
                        result.Success = false;
                        result.ErrorMessage = "Data type not supported";
                        result.DebugInfo = $"Return type: {reqVars.returnType}";
                        break;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error retrieving system state";
                result.DebugInfo = $"Error: {ex.Message}, Stack trace: {GetStackTrace()}";
                return result;
            }
        }

        /// <summary>
        /// Sets the system state based from an incoming request from a remote server
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        // MIGRATED - CAN BE REMOVED
        public async static Task SetSystemState(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);

            // Retrieve posted data
            var dataToStore = request.RetrievePostedValue("data", RegexEnum.None, true, ReturnTypeEnum.Xml);
            
            // Call the core logic method
            var result = await SetSystemStateCore(dataToStore, reqVars);
            
            // Return the response
            if (result.Success)
            {
                await response.OK(result.ResponseContent, reqVars.returnType, true);
            }
            else
            {
                HandleError(reqVars, result.ErrorMessage, result.DebugInfo, result.StatusCode);
            }
        }
        
        /// <summary>
        /// Core logic for setting system state without HTTP dependencies
        /// </summary>
        /// <param name="dataToStore">XML data to store</param>
        /// <param name="reqVars">Request variables</param>
        /// <returns>System state result</returns>
        public async static Task<SystemStateResult> SetSystemStateCore(string dataToStore, RequestVariables reqVars)
        {
            var result = new SystemStateResult();
            
            await DummyAwaiter();
            
            try
            {
                var xmlData = new XmlDocument();
                xmlData.LoadXml(dataToStore);

                // Update the system state
                SystemState.FromXml(xmlData);

                var message = new TaxxorReturnMessage(true, "Successfully updated the system state object");
                result.Success = true;
                result.ResponseContent = message;

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error setting system state";
                result.DebugInfo = $"Error: {ex.Message}, Stack trace: {GetStackTrace()}";
                return result;
            }
        }
        
        /// <summary>
        /// Result class for system state operations
        /// </summary>
        public class SystemStateResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? DebugInfo { get; set; }
            public int StatusCode { get; set; } = 500;
            public object? ResponseContent { get; set; }
        }
    }
}
