#nullable enable

using System;
using Microsoft.Extensions.Logging;

namespace Taxxor.ServiceLibrary.Loggers
{
	public class FormattingLoggerOptions
	{
		protected string? _timestampFormat = "yyyy-MM-dd HH:mm:ss";
		public string? TimestampFormat
		{
			get => _timestampFormat;
			set
			{
				try
				{
					DateTime.Now.ToString(value);
					_timestampFormat = value;
				}
				catch
				{
					_timestampFormat = null;
				}
			}
		}
		//public string Format { get; set; } = string.Empty;

		public LogLevel MinLogLevel { get; set; } = LogLevel.Trace;

		public int QueueCapacity { get; set; } = 10000;
		public bool ShortName { get; set; } = true;
		public bool IncludeEventId { get; set; } = false;
		public bool IncludeScope { get; set; } = false;
	}

}
