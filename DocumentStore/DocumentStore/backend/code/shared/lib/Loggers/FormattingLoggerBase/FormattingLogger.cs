#nullable enable

using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading;


namespace Taxxor.ServiceLibrary.Loggers
{
	public class FormattingLogger : ILogger
	{
		private FormattingLoggerProvider _provider;
		private string _name;
		private string _logName;
		private FormattingLoggerOptions _options => _provider._options.CurrentValue;
		private StringBuilder? _stringBuilder;

		public FormattingLogger(FormattingLoggerProvider provider, string name)
		{
			_provider = provider;
			_name = name;
			_logName = _options.ShortName ? GetShortName(name) : name;
		}

		private string GetShortName(string name)
		{
			int dotIdx = name.LastIndexOf('.');
			return dotIdx < 0 ? name : name.Substring(dotIdx + 1);
		}

		public IDisposable BeginScope<TState>(TState state) where TState : notnull => _provider.ScopeProvider?.Push(state) ?? throw new NotSupportedException("Missing scope provider");

		public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(_name, logLevel);

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
				return;

			DateTime now = DateTime.Now;
			FormattingLoggerOptions options = _options;

			// Reuse existing stringbuilder if possible
			StringBuilder? stringBuilder = null;
			Interlocked.Exchange(ref _stringBuilder, stringBuilder);
			stringBuilder ??= new StringBuilder();

			// Format message
			string? timestampFormat = options.TimestampFormat;
			if (timestampFormat != null)
				stringBuilder.Append(now.ToString(timestampFormat)).Append(" - ");

			stringBuilder.Append('[').Append(GetLogLevelString(logLevel)).Append("] ");
			stringBuilder.Append(_logName);

			if (options.IncludeEventId)
				stringBuilder.Append(" [").Append(eventId).Append("]");

			if (options.IncludeScope)
			{
				IExternalScopeProvider? scopeProvider = _provider.ScopeProvider;
				if (scopeProvider != null)
					scopeProvider.ForEachScope((scope, sb) => sb.Append(" => ").Append(scope), stringBuilder);
			}

			stringBuilder.Append(": ").Append(formatter(state, exception));
			if (exception != null)
				stringBuilder.AppendLine().Append(exception.ToString());

			_provider.CreateLogEntry(now, stringBuilder.ToString());

			// Prepare for next entry
			stringBuilder.Clear();
			if (stringBuilder.Capacity > 1024)
				stringBuilder.Capacity = 1024;
			_stringBuilder = stringBuilder;
		}

		private static string GetLogLevelString(LogLevel logLevel) => logLevel switch
		{
			LogLevel.Trace => "trce",
			LogLevel.Debug => "dbug",
			LogLevel.Information => "info",
			LogLevel.Warning => "warn",
			LogLevel.Error => "fail",
			LogLevel.Critical => "crit",
			_ => throw new ArgumentOutOfRangeException("logLevel"),
		};
	}
}
