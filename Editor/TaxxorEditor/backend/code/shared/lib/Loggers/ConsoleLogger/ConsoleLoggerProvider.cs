using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;


namespace Taxxor.ServiceLibrary.Loggers
{
	[ProviderAlias("ConsoleLogger")]
	public class ConsoleLoggerProvider : FormattingLoggerProvider
	{
		public ConsoleLoggerProvider(IOptionsMonitor<ConsoleLoggerOptions> options) : base(options)
		{ }

		protected override void ProcessLogQueue()
		{
			try
			{
				foreach (LogMessageEntry item in _messageQueue.GetConsumingEnumerable())
					Console.WriteLine(item.Message);
			}
			catch
			{
				_messageQueue.CompleteAdding();
			}
		}
	}
}
