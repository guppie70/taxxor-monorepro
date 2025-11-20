#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public static partial class FrameworkServicesExtensions
{
    /// <summary>
    /// Makes it possible to access the TasksToRun object from static methods
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseTasksToRun(this IApplicationBuilder app)
    {
        var tasksToRun = app.ApplicationServices.GetRequiredService<Taxxor.Project.TasksToRun>();
        Taxxor.Project.BackgroundTasks.Configure(tasksToRun);
        return app;
    }
}

namespace Taxxor.Project
{


    /// <summary>
    /// Helper class to make the TasksToRun object available from static methods
    /// </summary>
    public static class BackgroundTasks
    {
        private static TasksToRun? _tasksToRun;

        public static TasksToRun? TasksToRun => _tasksToRun;

        internal static void Configure(TasksToRun tasksToRun)
        {
            _tasksToRun = tasksToRun;
        }
    }



    public class TaskSettings
    {

        public ErpImportSettings? ErpImportSettings { get; set; }
        public SdsSyncSettings? SdsSyncSettings { get; set; }

        public static TaskSettings Create(ErpImportSettings erpImportSettings)
        {
            return new TaskSettings { ErpImportSettings = erpImportSettings };
        }

        public static TaskSettings Create(SdsSyncSettings sdsSyncSettings)
        {
            return new TaskSettings { SdsSyncSettings = sdsSyncSettings };
        }
    }



    public class TasksToRun
    {
        private readonly ConcurrentQueue<TaskSettings> _tasks = new();

        public TasksToRun() => _tasks = new ConcurrentQueue<TaskSettings>();

        public void Enqueue(TaskSettings settings) => _tasks.Enqueue(settings);

        public TaskSettings? Dequeue()
        {
            var hasTasks = _tasks.TryDequeue(out var settings);
            return hasTasks ? settings : null;
        }

        public bool IsEmpty => _tasks.IsEmpty;
    }

    /// <summary>
    /// Wrapper to launch a hosted background service
    /// </summary>
    /// <param name="tasks"></param>
    /// <param name="importingService"></param>
    /// <param name="logger"></param>
    /// <typeparam name="ErpImportService"></typeparam>
    public class TaxxorBackgroundTaskService(TasksToRun tasks, IErpImportingService importingService, ISdsSyncService sdsSyncService, ILogger<ErpImportService> logger) : BackgroundService
    {
        private readonly ILogger<ErpImportService> _logger = logger;
        private readonly TasksToRun _tasks = tasks;
        private readonly IErpImportingService _importingService = importingService;
        private readonly ISdsSyncService _sdsSyncService = sdsSyncService;


        /// <summary>
        /// Polling interval to check if new tasks are available
        /// </summary>
        /// <returns></returns>
        private readonly TimeSpan _timeSpan = TimeSpan.FromSeconds(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            using PeriodicTimer timer = new(_timeSpan);
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                while (!_tasks.IsEmpty)
                {
                    var taskToRun = _tasks.Dequeue();
                    if (taskToRun == null)
                    {
                        return;
                    }


                    _logger.LogInformation($"Finding the task to run");

                    // Call the relevant service based on what settings object in not NULL
                    switch (taskToRun)
                    {
                        case TaskSettings settings when settings.ErpImportSettings != null:
                            _ = await _importingService.Import(settings.ErpImportSettings);
                            break;
                        case TaskSettings settings when settings.SdsSyncSettings != null:
                            _ = await _sdsSyncService.Synchronize(settings.SdsSyncSettings);
                            break;
                        default:
                            // Handle the case when taskToRun is not a TaskSettings object
                            break;
                    }


                }

            }
        }
    }

}

