using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace Taxxor.ServiceLibrary.Loggers
{
	public static class LoggerExtensions
	{
		public static ILoggingBuilder AddConsoleLogger(this ILoggingBuilder builder)
		{
			builder.AddConfiguration();
			builder.Services.AddSingleton<ILoggerProvider, ConsoleLoggerProvider>();
			LoggerProviderOptions.RegisterProviderOptions<ConsoleLoggerOptions, ConsoleLoggerProvider>(builder.Services);
			return builder;
		}

		public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder)
		{
			builder.AddConfiguration();
			builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
			LoggerProviderOptions.RegisterProviderOptions<FileLoggerOptions, FileLoggerProvider>(builder.Services);
			return builder;
		}

		public static ILoggingBuilder AddFileAndConsoleLoggers(this ILoggingBuilder builder) => builder.ClearProviders().AddConsoleLogger().AddFileLogger();
	}
}
