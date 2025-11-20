using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace System.Web
{
    /// <summary>
    /// Allows to use the SignalR hub in the context of static methods
    /// </summary>
    public static class SignalRHub
    {
        private static IHubContext<WebSocketsHub> _hubContext;

        public static IHubContext<WebSocketsHub> Current => _hubContext;

        internal static void Configure(IHubContext<WebSocketsHub> hubContext)
        {
            _hubContext = hubContext;
        }
    }

}

public static partial class ProjectServicesExtensions
{

    /// <summary>
    /// Allows to use the SignalR hub in the context of static methods
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseStaticWebSocketsHub(this IApplicationBuilder app)
    {
        var webSocketsHubAccessor = app.ApplicationServices.GetRequiredService<IHubContext<WebSocketsHub>>();
        System.Web.SignalRHub.Configure(webSocketsHubAccessor);
        return app;
    }
}


public partial class ProjectExtensions : FrameworkExtensions
{

    /// <summary>
    /// Uses SignalR to communicate with connected clients about the status of a synchronization or import process
    /// </summary>
    /// <param name="clientEventName"></param>
    /// <param name="isRunning"></param>
    /// <param name="projectId"></param>
    public override async Task UpdateSyncAndImportSystemStatusInClient(string clientEventName, bool isRunning, string projectId, double progress, List<string> runIds)
    {
        // Framework.appLogger.LogCritical($"UpdateSyncAndImportSystemStatusInClient() clientEventName: {clientEventName}, isRunning: {isRunning}, projectId: {projectId}, progress: {progress}, runIds: [{string.Join(", ", runIds)}]");
        try
        {
            await System.Web.SignalRHub.Current.Clients.All.SendAsync(clientEventName, isRunning, projectId, progress, runIds);
        }
        catch (Exception ex)
        {
            Framework.appLogger.LogError(ex, $"Error sending {clientEventName} system status update message to client");
        }
    }

    /// <summary>
    /// Uses SignalR to update the system state object in the connected clients with the number of active users
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    public override async Task UpdateActiveUsersInClient(int count)
    {
        // Framework.appLogger.LogCritical($"UpdateSyncAndImportSystemStatusInClient() clientEventName: {clientEventName}, isRunning: {isRunning}, projectId: {projectId}, progress: {progress}, runIds: [{string.Join(", ", runIds)}]");
        var clientEventName = "SystemStateUpdateActiveUsers";
        try
        {
            await System.Web.SignalRHub.Current.Clients.All.SendAsync(clientEventName, count);
        }
        catch (Exception ex)
        {
            Framework.appLogger.LogError(ex, $"Error sending {clientEventName} system status update message to client");
        }
    }

    public override List<string> GetErpImportRunIds(List<string> runIds)
    {
        var runIdsFromGlobalDict = new List<string>();
        foreach (var key in ErpImportStatus.Keys)
        {
            runIdsFromGlobalDict.Add(key);
        }
        return runIdsFromGlobalDict;
    }

    /// <summary>
    /// Removes the complete ERP import state object from the global dictionary
    /// </summary>
    /// <param name="runId"></param>
    public override void RemoveErpImportObject(string runId)
    {
        // PauseExecution(3000).GetAwaiter().GetResult();
        if (!ErpImportStatus.TryRemove(runId, out _))
        {
            appLogger.LogError($"Failed to remove ErpImportStatus object for {runId}. ");
        }
    }

    /// <summary>
    /// Removes all entries from the ERP import state object from the global dictionary
    /// </summary>
    /// <returns></returns>
    public override TaxxorReturnMessage ClearFsExistsCache()
    {
        try
        {
            FsExistsCache.Clear();
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, "Error clearing the file system exists cache");
            return new TaxxorReturnMessage(false, ex.Message, ex.ToString());
        }


        return new TaxxorReturnMessage(true, "Successfully cleared the file system exists cache");
    }

    /// <summary>
    /// Retrieves a source data reference from the DocumentStore
    /// </summary>
    /// <param name="projectVarsForXhtmlCheck"></param>
    /// <param name="dataRef"></param>
    /// <returns></returns>
    public override async Task<XmlDocument> RetrieveFilingComposerXmlData(ProjectVariables projectVarsForXhtmlCheck, string dataRef)
    {
        return await Taxxor.Project.ProjectLogic.RetrieveFilingComposerXmlData(projectVarsForXhtmlCheck, dataRef, true, true);
    }

}