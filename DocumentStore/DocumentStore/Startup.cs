using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScheduledTasks;
using ScheduledTasks.Cron;
using DocumentStore.Services;

namespace Taxxor.Project
{

    public class Startup : ProjectLogic
    {

        private IHostEnvironment _hostingEnvironment { get; }
        private IConfiguration _configuration { get; }

        public Startup(IHostEnvironment env, IConfiguration config)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [trace] : Startup()");
            _hostingEnvironment = env;
            _configuration = config;

            // Debug the config
            Console.WriteLine("------------------- config and environment -------------------");
            Console.WriteLine("\n" + string.Join("\n", config.AsEnumerable(false).Where(x => !x.Key.StartsWith("npm_")).OrderBy(x => x.Key).Select(x => x.Key + ": " + x.Value + "").ToList()));
            Console.WriteLine("--------------------------------------------------------------");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            Console.WriteLine($"* {DateTime.Now:yyyy-MM-dd HH:mm:ss} - [trace] : ConfigureServices()");
            if (_hostingEnvironment.IsDevelopment())
            {
                // Development services
            }
            else
            {
                // Staging/Production services
            }

            // Configure how much form data the application can handle
            services.Configure<FormOptions>(options =>
            {
                options.ValueCountLimit = 200;
                options.ValueLengthLimit = 1024 * 1024 * 100; // 100MB max len form data
                options.MultipartBodyLengthLimit = 250 * 1024 * 1024; // Allow files up to 250 MB
            });

            // To be able to access the HTTP Context
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // To define custom routes
            services.AddRouting();

            // Add memory cache service
            services.AddMemoryCache();

            // Register RequestContext as a scoped service
            services.AddScoped<RequestContext>();

            // Automapper
            services.AddAutoMapper(typeof(ProjectMappingProfile));

            // Add scheduled tasks & scheduler
            services.AddSingleton<IScheduledTask, ScheduledTaskEveryMinute>();
            services.AddSingleton<IScheduledTask, ScheduledTaskEveryTwoMinutes>();
            services.AddSingleton<IScheduledTask, ScheduledTaskEveryFiveMinutes>();
            services.AddSingleton<IScheduledTask, ScheduledTaskEveryDay>();

            // Start the background scheduler
            services.AddScheduler((sender, args) =>
            {
                Console.Write(args.Exception.Message);
                args.SetObserved();
            });

            // Add support for controllers
            services.AddControllers();

            // Add gRPC services
            services.AddGrpc();
        }

        // Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostEnvironment env, ILoggerFactory loggerFactory, IMemoryCache cache, IConfiguration config)
        {
            Console.WriteLine($"* {(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))} - [trace] : Configure()");

            // Add a re-usable logger factory
            Framework.appLoggerFactory = loggerFactory;
            Framework.appLogger = appLoggerFactory.CreateLogger("");

            // Write a marker in the console which will appear in the logfiles to act as a clear marker that the application has (re)started
            Framework.appLogger.LogCritical($"!! Starting Taxxor Document Store !!");

            // Initialize the framework logic and fill the globally available fields
            InitApplicationLogic(app, env, cache, config);

            // Retrieve the project unique variables
            InitProjectLogic();

            #region Services
            // Make sure that we can access the current HttpContext from within our controllers
            app.UseStaticHttpContext();

            #endregion

            #region Middleware
            // Context specific middleware
            if (env.IsDevelopment())
            {
                // Enable request rewinding so that we can read the request body multiple times (for debugging purposes)
                app.Use(async (context, next) =>
                {
                    context.Request.EnableBuffering();
                    await next();
                });

                // Catch regular errors and show detailed output
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // Production environment unique middleware

            }

            //
            // => Error handling middleware
            //
            app.UseCustomExceptionHandler();

            // Show something in the client when an error has occurred (like 404 Not Found)
            app.UseStatusCodePages();

            // Retrieve generic information of the incoming request
            app.UseRequestVariablesMiddleware(new FrameworkOptions
            {
                DebugMode = env.IsDevelopment(),
                RequestVariablesDebugOutput = false
            });

            // Retrieve unique information for this project
            app.UseProjectVariablesMiddleware();

            // Middleware that deals with the authentication and authorization within the application
            app.UseAuthenticationMiddleware();

            // Retrieve user data
            app.UseUserDataMiddleware();

            // Debugging middleware
            app.UseDebugMiddleware();
            #endregion

            // Add routing middleware
            app.UseRouting();

            // Configure endpoints
            app.UseEndpoints(endpoints =>
            {
                // Map controller endpoints
                endpoints.MapControllers();

                // Map gRPC services
                // endpoints.MapGrpcService<SystemStateService>();
                // endpoints.MapGrpcService<ConfigurationService>();
                // endpoints.MapGrpcService<FileManagementService>();
                endpoints.MapGrpcService<FilingComposerDataService>();
                endpoints.MapGrpcService<BinaryFileManagementService>();
                endpoints.MapGrpcService<FilingHierarchyService>();
            });

            #region Routers
            // Defines the API routes
            app.UseRouter(ApiRouter(app));

            // Sandbox testing routes
            app.UseRouter(SandboxRouter(app));

            // Renders the service description on "/"
            app.UseRouter(HomePageRouter(app));
            #endregion
        }
    }
}
