using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Taxxor.ServiceLibrary.Loggers;

namespace Taxxor.Project
{


    /// <summary>
    /// Main class that initiates everything
    /// </summary>
    public abstract partial class Program
    {

        /// <summary>
        /// The entry point of the program, where the program control starts and ends.
        /// </summary>
        /// <param name="args">The command-line arguments. Force a listening port by setting " --port=8090". Force an envoronment by " --launch-profile=Development"</param>
        public static void Main(string[] args)
        {
            Console.WriteLine($"\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n** {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Starting Application **\n");

            // Start the web application
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logBuilder => logBuilder.AddFileAndConsoleLoggers())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(options =>
                    {
                        options.AddServerHeader = false; // Disable the server header for security
                        options.Limits.MaxRequestBodySize = 250 * 1024 * 1024; // Set max request body size to 250 MB
                    });
                    // webBuilder.ConfigureKestrel(options =>
                    // {
                    //     // Setup a HTTP/2 endpoint without TLS.
                    //     options.ListenLocalhost(4813, o => o.Protocols = HttpProtocols.Http2);
                    // });
                    webBuilder.UseStartup<Startup>();
                }).Build().Run();
        }

    }
}