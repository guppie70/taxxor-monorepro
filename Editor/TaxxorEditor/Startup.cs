using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScheduledTasks;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using NetInterface;
using CustomerCodeInterface;
using DocumentStore.Protos;

namespace Taxxor.Project
{

    public class Startup : ProjectLogic
    {

        private IHostEnvironment _hostingEnvironment { get; }
        private IConfiguration _configuration { get; }

        public Startup(IHostEnvironment env, IConfiguration config)
        {
            Console.WriteLine($"{(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))} - [trace] : Startup()");
            _hostingEnvironment = env;
            _configuration = config;

            // Debug the config
            Console.WriteLine("------------------- config and environment -------------------");
            Console.WriteLine("\n" + string.Join("\n", config.AsEnumerable(false).Where(x => !x.Key.StartsWith("npm_")).OrderBy(x => x.Key).Select(x => x.Key + ": " + x.Value + "").ToList()));
            Console.WriteLine("--------------------------------------------------------------");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            Console.WriteLine($"* {(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))} - [trace] : ConfigureServices()");

            if (TimeStartup)
            {
                Console.WriteLine("-> Starting ConfigureServices performance measurement");
                stopWatch.Restart();
            }

            // Enable accessing the HTTP Context from "anywhere"
            services.AddHttpContextAccessor();

            // To define custom routes
            services.AddRouting();

            if (TimeStartup)
            {
                Console.WriteLine($" -> Basic services setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Setup hosting for memory cache and session data
            // Register the IMemoryCache service
            if (_hostingEnvironment.IsDevelopment() || _hostingEnvironment.IsProduction() || _hostingEnvironment.IsStaging())
            {
                var redisHostName = CalculateRedisHostName(_hostingEnvironment);

                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = $"{redisHostName}:6379";
                    options.InstanceName = redisHostName;
                });

                // Store the encryption keys in the redis database
                var redis = ConnectionMultiplexer.Connect(redisHostName);
                services.AddDataProtection().PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
            }
            else
            {
                // Fall back to in-memory caching if we can't determine the environment
                services.AddDistributedMemoryCache();
            }

            if (TimeStartup)
            {
                Console.WriteLine($" -> Redis and caching setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Configure how much data the application can handle
            services.Configure<FormOptions>(options =>
            {
                options.ValueCountLimit = 200;
                options.ValueLengthLimit = 1024 * 1024 * 100; // 100MB max len form data
                options.MultipartBodyLengthLimit = 250 * 1024 * 1024; // Allow files up to 250 MB
            });

            // Add SignalR websockets
            services.AddSignalR(hubOptions =>
            {
                hubOptions.EnableDetailedErrors = true;
            });

            services.AddSingleton<WebSocketsHub>();

            if (TimeStartup)
            {
                Console.WriteLine($" -> SignalR setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Add session support
            services.AddSession(options =>
            {
                options.Cookie.Name = $"sessionidtxeditor{(_hostingEnvironment.IsStaging() ? "staging" : "")}";
                options.IdleTimeout = TimeSpan.FromMinutes(120);
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOST_MACHINE"))) ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.Cookie.IsEssential = true;
            });

            // Add memory cache service
            services.AddMemoryCache();

            if (TimeStartup)
            {
                Console.WriteLine($" -> Session and memory cache setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

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

            services.AddSingleton<TasksToRun>();
            services.AddHostedService<TaxxorBackgroundTaskService>();
            services.AddTransient<IErpImportingService, ErpImportService>();
            services.AddTransient<ISdsSyncService, SdsSyncService>();

            if (TimeStartup)
            {
                Console.WriteLine($" -> Scheduled tasks and background services setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Restart();
            }

            // Customer specific C# code
            var editorCustomizationsDll = RetrieveEditorCustomizationsDllPath(_hostingEnvironment.IsDevelopment());
            if (editorCustomizationsDll != null)
            {
                services.AddFromAssembly<ICustomerCode>(editorCustomizationsDll);
            }

            if (TimeStartup)
            {
                Console.WriteLine($" -> Customer specific code setup: {stopWatch.ElapsedMilliseconds}ms");
                stopWatch.Stop();
            }


            services.AddGrpcClient<FilingComposerDataService.FilingComposerDataServiceClient>(o =>
            {
                o.Address = new Uri("https://documentstore:4813");
            });

            services.AddGrpcClient<BinaryFileManagementService.BinaryFileManagementServiceClient>(o =>
            {
                o.Address = new Uri("https://documentstore:4813");
            });

            services.AddGrpcClient<FilingDataService.FilingDataServiceClient>(o =>
            {
                o.Address = new Uri("https://documentstore:4813");
            });

        }
        // Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostEnvironment env, ILoggerFactory loggerFactory, IMemoryCache cache, IConfiguration config)
        {
            Console.WriteLine($"* {(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))} - [trace] : Configure()");

            // Add a re-usable logger factory
            Framework.appLoggerFactory = loggerFactory;
            Framework.appLogger = appLoggerFactory.CreateLogger("");

            // Write a marker in the console which will appear in the logfiles to act as a clear marker that the application has (re)started
            Framework.appLogger.LogCritical($"!! Starting Taxxor Editor !!");

            // Send some logging to the console
            Console.WriteLine($"- HostingEnvironmentName: '{env.EnvironmentName}'");

            // Register Syncfusion license
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("MjUwNDcwQDMxMzgyZTMxMmUzMGhNaUlNTUF5RDVEaWVxS3lNQTlDeEFscGJIeTRsSFBMUFBIaGtoSms4azQ9");

            // ILogger timerLogger = appLoggerFactory.CreateLogger("RequestTimer");
            // app.Use(async (context, next) =>
            // {
            //     DateTime start = DateTime.UtcNow;
            //     await next();
            //     timerLogger.LogInformation($"Request {context.TraceIdentifier} {context.Request.Host}{context.Request.Path} has been completed in {(DateTime.UtcNow - start).TotalMilliseconds} ms");
            // });

            // Initialize the framework logic and fill the globally available fields
            InitApplicationLogic(app, env, cache, config);

            // Retrieve the project unique variables
            InitProjectLogic();

            // Make sure that we can access the current HttpContext from within static methods using System.Web.Context.Current
            app.UseStaticHttpContext();

            // Make sure that we can access the current HubContext from within static methods using System.Web.SignalRHub.Current
            app.UseStaticWebSocketsHub();

            // Enable background tasks
            app.UseTasksToRun();



            // Enable sessions
            app.UseSession();

            // Use endpoint routing
            app.UseRouting();

            // Add HTTP security response headers
            app.UseSecurityHeaders(config =>
            {
                config.AddXssProtectionBlock();
                config.AddContentTypeOptionsNoSniff();
                config.RemoveServerHeader();
                config.AddStrictTransportSecurityMaxAgeIncludeSubDomains();
            });

            // Add CSP security response headers
            app.Use((context, next) =>
                {

                    // Calculate the nonnce
                    var nonce = string.Empty;
                    byte[] nonceBytes = new byte[32]; // 128 bits
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(nonceBytes);
                    }
                    nonce = Convert.ToBase64String(nonceBytes);

                    // Append the nonce into the context so that we can use it later on
                    context.Items.Add("nonce", nonce);

                    // Render the CSP header (only if we did not receive a request with an 'internal' which we have explicitly added to an internal request)
                    if (context.Request.Query["internal"].Count == 0)
                    {
                        if (CspContents == null)
                        {
                            // Console.WriteLine($"* {(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))} - [trace] : CspContents == null");

                            var sbCsp = new StringBuilder();
                            sbCsp.Append($"default-src * wss: data:;");
                            sbCsp.Append($" manifest-src 'self' data: *;");
                            sbCsp.Append($" connect-src 'self' *.taxxordm.com ws: wss:{(env.IsDevelopment() ? " " + ContentSecurityPolicyScriptSource : "")};");
                            sbCsp.Append($" frame-ancestors 'self' *.taxxordm.com ").Append(ContentSecurityPolicyFrameSource).Append(';');
                            sbCsp.Append($" img-src 'self' *.taxxordm.com blob: data: ").Append(ContentSecurityPolicyImageSource).Append(';');
                            sbCsp.Append($" font-src 'self' *.taxxordm.com blob: data: ").Append(ContentSecurityPolicyFontSource).Append(';');
                            sbCsp.Append($" script-src 'self' *.taxxordm.com 'unsafe-inline' 'unsafe-eval' https://apis.google.com https://www.gstatic.com ").Append(ContentSecurityPolicyScriptSource).Append(';');
                            // sbCsp.Append($" script-src 'self' 'nonce-{nonce}' ").Append(ContentSecurityPolicyScriptSource).Append(';');
                            sbCsp.Append($" style-src 'self' *.taxxordm.com 'unsafe-inline' ").Append(ContentSecurityPolicyStyleSource).Append(';');
                            // sbCsp.Append($" style-src 'self' 'unsafe-inline' ").Append(ContentSecurityPolicyStyleSource).Append(';');

                            CspContents = sbCsp.ToString();
                        }

                        context.Response.Headers.Append("Content-Security-Policy", CspContents);

                        // Add CORS headers to response
                        var allowedOrigins = new[] { "https://static.test.taxxordm.com", "https://static.taxxordm.com" };
                        var origin = context.Request.Headers["Origin"].FirstOrDefault();
                        if (allowedOrigins.Contains(origin))
                        {
                            context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                            context.Response.Headers.Append("Vary", "Origin");
                        }
                        // context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
                    }


                    return next();
                }
            );


            //
            // => Context specific middleware
            //
            if (env.IsDevelopment())
            {
                // Development specific middleware

                // Enable request rewinding so that we can read the request body multiple times (for debugging purposes)
                app.Use(async (context, next) =>
                {
                    context.Request.EnableBuffering();
                    await next();
                });

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

            // Authenticate static assets
            app.UseAuthenticationStaticAssetsMiddleware();

            // Setup a static files server
            StaticServer(app, env);

            // Retrieve unique information for this project
            app.UseProjectVariablesMiddleware();

            // Stream assets from the Taxxor Project Data Store
            app.UseDataServiceAssetsMiddleware();

            // Middleware that deals with the authentication and authorization within the application
            app.UseAuthenticationDynamicAssetsMiddleware();

            // Security measures for this route
            app.UseRequestSecurityMiddleware();

            // Retrieve user data
            app.UseUserDataMiddleware();

            // Debugging middleware
            app.UseDebugMiddleware();

            // // Support for gRPC-web
            // app.UseGrpcWeb();

            // Defines Sandbox routes
            app.UseRouter(SandboxRouter(app));

            // Defines Authentication paths
            app.UseRouter(AuthenticationRouter(app));

            // Defines the API routes
            app.UseRouter(ApiRouter(app));

            // Special router for proxy-ing requests directly to underlying services
            app.UseRouter(ProxyRouter(app));

            // Defines the routes to tools pages
            app.UseRouter(ToolsRouter(app));

            // Defines the routes to thr utility pages
            app.UseRouter(UtilitiesRouter(app));

            // Defines the routes for the pages in the website
            app.UseRouter(PagesRouter(app));



            app.UseEndpoints(endpoints =>
            {
                // Websockets SignalR and Blazor
                endpoints.MapHub<WebSocketsHub>("/hub", options =>
                {
                    options.Transports =
                        HttpTransportType.WebSockets |
                        HttpTransportType.LongPolling;
                    options.TransportMaxBufferSize = 128000;
                    options.ApplicationMaxBufferSize = 128000;
                });

            });

            // Console.WriteLine("Server started.");

        }

    }

}