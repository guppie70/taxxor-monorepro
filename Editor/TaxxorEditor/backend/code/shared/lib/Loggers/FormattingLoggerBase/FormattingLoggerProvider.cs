#nullable enable

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Taxxor.ServiceLibrary.Loggers
{
	public abstract class FormattingLoggerProvider : ILoggerProvider, ISupportExternalScope
	{
		private readonly ConcurrentDictionary<string, FormattingLogger> _loggers = new();
		internal readonly IOptionsMonitor<FormattingLoggerOptions> _options;

		protected readonly BlockingCollection<LogMessageEntry> _messageQueue = new();
		private readonly Thread _outputThread;

		private int _suppressQueueWarning = 0;
		private int _messageSinceWarning = 0;

		public FormattingLoggerProvider(IOptionsMonitor<FormattingLoggerOptions> options)
		{
			_options = options;

			_outputThread = new Thread(ProcessLogQueue)
			{
				IsBackground = true,
				Name = $"{GetType().Name} queue processing thread"
			};
			_outputThread.Start();
		}

		public ILogger CreateLogger(string name) => _loggers.GetOrAdd(name, loggerName => new FormattingLogger(this, name));

		public void Dispose()
		{
			_messageQueue.CompleteAdding();
			try
			{
				_outputThread.Join(1500);
			}
			catch
			{ }
		}

		public IExternalScopeProvider? ScopeProvider { get; private set; }
		public void SetScopeProvider(IExternalScopeProvider scopeProvider) => ScopeProvider = scopeProvider;

		// Allow for category-specific fitlering?
		internal virtual bool IsEnabled(string name, LogLevel logLevel) => logLevel >= _options.CurrentValue.MinLogLevel;

		internal virtual void CreateLogEntry(DateTime timestamp, string message) => AddLogEntry(new LogMessageEntry(timestamp, message));

		protected virtual void AddLogEntry(LogMessageEntry message)
		{
			if (!_messageQueue.IsAddingCompleted)
				try
				{
					int capacity = _options.CurrentValue.QueueCapacity;
					if (capacity > 0 && _messageQueue.Count >= capacity)
					{
						if (Interlocked.CompareExchange(ref _suppressQueueWarning, 1, 0) == 0)
						{
							_messageSinceWarning = 0;
							_messageQueue.Add(new LogMessageEntry(DateTime.Now, $"{GetType().Name}: The logging queue is full; messages have been dropped"));
						}
					}
					else
					{
						_messageQueue.Add(message);
						if (_suppressQueueWarning != 0 && Interlocked.Increment(ref _messageSinceWarning) > 40)
						{
							_messageSinceWarning = 0;
							_suppressQueueWarning = 0;
						}
					}
				}
				catch
				{ }
		}

		protected abstract void ProcessLogQueue();
	}
}
