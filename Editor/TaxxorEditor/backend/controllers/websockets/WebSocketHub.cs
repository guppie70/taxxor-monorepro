using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Class for dealing with websocket connections
        /// </summary>
        public partial class WebSocketsHub : Hub
        {

            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveSystemStatus()
            {
                var errorMessage = "There was an error retrieving the system status.";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Compile an object that contains details about the application state on the server
                    //

                    var jsonSystemState = SystemState.ToJson();

                    return new TaxxorReturnMessage(true, "Successfully retrieved system status", jsonSystemState, "");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }
            }





            /// <summary>
            /// Returns JSON information about the permissions of all the sections that this user has access to
            /// </summary>
            /// <param name="processOnlyOpenProjects"></param>
            /// <param name="includePermissionDetails"></param>
            /// <param name="includeNonEditableSections"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/editortools")]
            public async Task<TaxxorReturnMessage> RetrieveUserSectionInformation(bool processOnlyOpenProjects, bool includePermissionDetails, bool includeNonEditableSections, List<string> projectIdFilter)
            {
                var errorMessage = "There was an error rendering user section information";
                try
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    var debugRoutine = (siteType == "local" || siteType == "dev");

                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("processOnlyOpenProjects", processOnlyOpenProjects.ToString().ToLower(), RegexEnum.Boolean, true);
                    inputValidationCollection.Add("includePermissionDetails", includePermissionDetails.ToString().ToLower(), RegexEnum.Boolean, true);
                    inputValidationCollection.Add("includeNonEditableSections", includeNonEditableSections.ToString().ToLower(), RegexEnum.Boolean, true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        return validationResult;
                    }


                    //
                    // => Retrieve the information about the sections that this user has access to
                    //
                    var userSectionInformationResult = await Taxxor.Project.ProjectLogic.RetrieveUserSectionInformation(reqVars, projectVars, processOnlyOpenProjects, includePermissionDetails, includeNonEditableSections, projectIdFilter);

                    //
                    // => Return the TaxxorReturnMessage
                    //
                    return userSectionInformationResult;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, $"error: {ex}");
                }

            }



            /// <summary>
            /// 
            /// </summary>
            /// <param name="clientListener"></param>
            /// <param name="message"></param>
            /// <returns></returns>
            public async Task MessageToAllClients(string clientListener, string message)
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);

                // Console.WriteLine($"IN MessageToAllClients('{clientListener}', '{message}')");
                WebsocketReturnMessage? dispatcherResult = null;
                XmlDocument? xmlDataPosted = null;
                try
                {
                    xmlDataPosted = ConvertJsonToXml(message, "data");
                    // Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                    dispatcherResult = await DispatchGenericWebsocketRequest(clientListener, xmlDataPosted);
                }
                catch (Exception ex)
                {
                    // Log the exception
                    appLogger.LogError(ex, $"Error dispatching the websocket request. clientListener: {clientListener}, message: {message}");

                    // Create an error response
                    dispatcherResult = new WebsocketReturnMessage(false, "foo", "There was an error processing this request", $"clientListener: {clientListener}, message: {message}, error: {ex}");
                }
                finally
                {
                    await Clients.All.SendAsync(clientListener, dispatcherResult.ToJson());
                }
            }


            public async Task MessageToCurrentClient(string clientListener, string message)
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);

                Console.WriteLine($"IN MessageToCurrentClient('{clientListener}', '{message}')");
                WebsocketReturnMessage? dispatcherResult = null;
                XmlDocument? xmlDataPosted = null;
                try
                {
                    xmlDataPosted = ConvertJsonToXml(message, "data");
                    // Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                    dispatcherResult = await DispatchGenericWebsocketRequest(clientListener, xmlDataPosted);
                }
                catch (Exception ex)
                {
                    // Log the exception
                    appLogger.LogError(ex, $"Error dispatching the websocket request. clientListener: {clientListener}, message: {message}");

                    // Create an error response
                    dispatcherResult = new WebsocketReturnMessage(false, "foo", "There was an error processing this request", $"clientListener: {clientListener}, message: {message}, error: {ex}");
                }
                finally
                {
                    await Clients.Caller.SendAsync(clientListener, dispatcherResult.ToJson());
                }
            }

            public async Task MessageToOtherClients(string clientListener, string message)
            {
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);

                Console.WriteLine($"IN MessageToOtherClients('{clientListener}', '{message}')");
                WebsocketReturnMessage? dispatcherResult = null;
                XmlDocument? xmlDataPosted = null;
                try
                {
                    xmlDataPosted = ConvertJsonToXml(message, "data");
                    // Console.WriteLine($"- xmlDataPosted: {PrettyPrintXml(xmlDataPosted)}");

                    dispatcherResult = await DispatchGenericWebsocketRequest(clientListener, xmlDataPosted);
                }
                catch (Exception ex)
                {
                    // Log the exception
                    appLogger.LogError(ex, $"Error dispatching the websocket request. clientListener: {clientListener}, message: {message}");

                    // Create an error response
                    dispatcherResult = new WebsocketReturnMessage(false, "foo", "There was an error processing this request", $"clientListener: {clientListener}, message: {message}, error: {ex}");
                }
                finally
                {
                    await Clients.Others.SendAsync(clientListener, dispatcherResult.ToJson());
                }
            }


            private async Task<WebsocketReturnMessage> DispatchGenericWebsocketRequest(string clientListener, XmlDocument xmlData)
            {
                WebsocketReturnMessage? dispatchResult = null;


                // Please the compiler
                await DummyAwaiter();

                switch (clientListener)
                {
                    case "foobar":
                        // Dispatch this request to another service that returns a TaxxorReturnMessage or a WebsocketReturnMessage
                        break;

                    default:
                        dispatchResult = new WebsocketReturnMessage(false, "foo", "Could not dispatch this request", $"No match for clientListener: {clientListener} in dispatcher");
                        break;
                }

                return dispatchResult;
            }

        }



        /// <summary>
        /// Sends a message to all the connected websocket users
        /// </summary>
        /// <param name="clientListener">Javascript event listener</param>
        /// <param name="message">Message to send over websocket</param>
        /// <param name="debugInfo">Debug information to send to all connected clients</param>
        /// <returns></returns>
        public static async Task MessageToAllClients(string clientListener, string message, string? debugInfo = null)
        {
            // Remove the debug info message from the websocket message
            if (siteType == "prod" || siteType == "prev") debugInfo = null;

            await MessageToAllClients(clientListener, new TaxxorReturnMessage(true, message, debugInfo));
        }

        /// <summary>
        /// Sends a message to all the connected websocket users
        /// </summary>
        /// <param name="clientListener">Javascript event listener</param>
        /// <param name="taxxorReturnMessage">Content to return to all connected clients</param>
        /// <returns></returns>
        public static async Task MessageToAllClients(string clientListener, TaxxorReturnMessage taxxorReturnMessage)
        {
            var context = System.Web.Context.Current;
            IHubContext<WebSocketsHub> hubContext;
            if (context != null)
            {
                hubContext = context.RequestServices.GetRequiredService<IHubContext<WebSocketsHub>>();
            }
            else
            {
                hubContext = System.Web.SignalRHub.Current;
            }

            // Remove the debug info message from the websocket message
            if (siteType == "prod" || siteType == "prev") taxxorReturnMessage.DebugInfo = "";

            await hubContext.Clients.All.SendAsync(clientListener, taxxorReturnMessage.ToJson());
        }

        /// <summary>
        /// Sends a message to the current user via websockets
        /// </summary>
        /// <param name="clientListener">Javascript event listener</param>
        /// <param name="message">Message to send over websocket</param>
        /// <param name="debugInfo">Debug information to send to the client</param>
        /// <returns></returns>
        public static async Task MessageToCurrentClient(string clientListener, string message, string? debugInfo = null)
        {
            // Remove the debug info message from the websocket message
            if (siteType == "prod" || siteType == "prev") debugInfo = null;

            await MessageToCurrentClient(clientListener, new TaxxorReturnMessage(true, message, debugInfo));
        }

        /// <summary>
        /// Sends a message to the current user via websockets
        /// </summary>
        /// <param name="clientListener">Javascript event listener</param>
        /// <param name="taxxorReturnMessage">Content to return to the client</param>
        /// <returns></returns>
        public static async Task MessageToCurrentClient(string clientListener, TaxxorReturnMessage taxxorReturnMessage)
        {
            var context = System.Web.Context.Current;

            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var hubContext = context.RequestServices.GetRequiredService<IHubContext<WebSocketsHub>>();

            // Remove the debug info message from the websocket message
            if (siteType == "prod" || siteType == "prev") taxxorReturnMessage.DebugInfo = "";

            await hubContext.Clients.Users(projectVars.currentUser.Id).SendAsync(clientListener, taxxorReturnMessage.ToJson());
        }



        /// <summary>
        /// Sends a message to another user via websockets
        /// </summary>
        /// <param name="clientListener">Javascript event listener</param>
        /// <param name="userId">User ID who needs to receive the message</param>
        /// <param name="message">Message to send over websocket</param>
        /// <param name="debugInfo">Debug information to send to the client</param>
        /// <returns></returns>
        public static async Task MessageToOtherClient(string clientListener, string userId, string message, string? debugInfo = null)
        {
            // Remove the debug info message from the websocket message
            if (siteType == "prod" || siteType == "prev") debugInfo = null;

            await MessageToOtherClient(clientListener, userId, new TaxxorReturnMessage(true, message, debugInfo));
        }

        /// <summary>
        /// Sends a message to another user via websockets
        /// </summary>
        /// <param name="clientListener">Javascript event listener</param>
        /// <param name="userId">User ID who needs to receive the message</param>
        /// <param name="taxxorReturnMessage">Content to return to the other user's websocket connection</param>
        /// <returns></returns>
        public static async Task MessageToOtherClient(string clientListener, string userId, TaxxorReturnMessage taxxorReturnMessage)
        {
            var context = System.Web.Context.Current;
            IHubContext<WebSocketsHub> hubContext;
            if (context != null)
            {
                hubContext = context.RequestServices.GetRequiredService<IHubContext<WebSocketsHub>>();
            }
            else
            {
                hubContext = System.Web.SignalRHub.Current;
            }

            // Remove the debug info message from the websocket message
            if (siteType == "prod" || siteType == "prev") taxxorReturnMessage.DebugInfo = "";

            await hubContext.Clients.Users(userId).SendAsync(clientListener, taxxorReturnMessage.ToJson());
        }

        /// <summary>
        /// Helper function to retrieve a value from a simple XmlDocument (key - value pairs)
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        private static KeyValuePair<string, string> _extractPostedXmlValue(XmlDocument xmlDocument, string key, string defaultValue = null)
        {
            return new KeyValuePair<string, string>(key, xmlDocument.DocumentElement.SelectSingleNode(key)?.InnerText ?? defaultValue);
        }

        /// <summary>
        /// Constructs a typical response for a websocket request
        /// </summary>
        public class WebsocketReturnMessage : TaxxorReturnMessage
        {
            /// <summary>
            /// Contains a string to notify the controller in the client-side routine which routine to start
            /// </summary>
            /// <value></value>
            public string Action { get; set; } = "";

            public WebsocketReturnMessage(bool success, string action, string message, XmlDocument xmlPayload, string debugInfo = "")
            {
                this.Action = action;
                this.Success = success;
                this.Message = message;
                this.DebugInfo = debugInfo;
                this.XmlPayload = xmlPayload;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Framework.WebsocketReturnMessage"/> class.
            /// </summary>
            /// <param name="success">Indicate if the process completed successfully</param>
            /// <param name="message">Message to return</param>
            /// <param name="debugInfo">Debug information</param>
            public WebsocketReturnMessage(bool success, string action, string message, string debugInfo = "")
            {
                this.Action = action;
                this.Success = success;
                this.Message = message;
                this.DebugInfo = debugInfo;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Framework.WebsocketReturnMessage"/> class by trying to convert the XmlDocument to a WebsocketReturnMessage.
            /// </summary>
            /// <param name="xmlDocument">Xml document.</param>
            public WebsocketReturnMessage(XmlDocument xmlDocument)
            {
                this._convertXmlToProperties(xmlDocument);

                // Retrieve the action
                this.Action = xmlDocument.SelectSingleNode("/result/action")?.InnerText ?? "";
            }

            public override XmlDocument ToXml()
            {
                var xmlDoc = this._convertPropertiesToXml();

                // Add the action
                var nodeAction = xmlDoc.CreateElement("action", this.Action);
                xmlDoc.DocumentElement.AppendChild(nodeAction);
                return xmlDoc;
            }

        }

    }

}