using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DocumentStore.Protos;




namespace Taxxor.Project
{

    /// <summary>
    /// Used to maintain a list of connected SignalR users
    /// </summary>
    public static class UserConnectionManager
    {
        private static readonly ConcurrentDictionary<string, bool> ConnectedUserIds = new();

        public static bool AddUser(string userId)
        {
            return ConnectedUserIds.TryAdd(userId, true);
        }

        public static bool RemoveUser(string userId)
        {
            return ConnectedUserIds.TryRemove(userId, out _);
        }

        public static List<string> GetAllConnectedUserIds()
        {
            return [.. ConnectedUserIds.Keys];
        }

        public static int Count(){
            return GetAllConnectedUserIds().Count;
        }
    }

    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Contains the constructor
        /// </summary>
        public partial class WebSocketsHub : Hub
        {
            private readonly TasksToRun _tasksToRun;
            private readonly BinaryFileManagementService.BinaryFileManagementServiceClient _binaryFileClient;

            public WebSocketsHub(TasksToRun tasksToRun, BinaryFileManagementService.BinaryFileManagementServiceClient binaryFileClient)
            {
                _tasksToRun = tasksToRun;
                _binaryFileClient = binaryFileClient;
            }

            public override async Task OnConnectedAsync()
            {
                // Update the UserConnectionManager
                var userId = Context.UserIdentifier;
                if (!string.IsNullOrEmpty(userId)) _ = UserConnectionManager.AddUser(userId);

                SystemState.ActiveUsers.Count = UserConnectionManager.Count();

                // Console.WriteLine($"@@@@@@@@@@@@\nOnConnectedAsync => Connection ID={Context.ConnectionId} : User={userId} : Users={string.Join(",", UserConnectionManager.GetAllConnectedUserIds())}\n@@@@@@@@@@@@");

                await base.OnConnectedAsync();
            }

            public override async Task OnDisconnectedAsync(Exception exception)
            {
                // Update the UserConnectionManager
                var userId = Context.UserIdentifier;
                if (!string.IsNullOrEmpty(userId)) _ = UserConnectionManager.RemoveUser(userId);

                SystemState.ActiveUsers.Count = UserConnectionManager.Count();

                // Console.WriteLine($"@@@@@@@@@@@@\nWeb client disconnected: Connection ID={Context.ConnectionId} : User={userId} : Users={string.Join(",", UserConnectionManager.GetAllConnectedUserIds())}\n@@@@@@@@@@@@");

                await base.OnDisconnectedAsync(exception);
            }


        }
    }
}