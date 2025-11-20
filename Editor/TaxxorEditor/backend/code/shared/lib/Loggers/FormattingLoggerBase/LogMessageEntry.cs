#nullable enable

using System;

namespace Taxxor.ServiceLibrary.Loggers
{
	public class LogMessageEntry
	{
		public readonly DateTime Timestamp;
		public readonly string Message;
		public readonly object? Context;

		public LogMessageEntry(DateTime timestamp, string message, object? context = null) => (Timestamp, Message, Context) = (timestamp, message, context);
	}
}
