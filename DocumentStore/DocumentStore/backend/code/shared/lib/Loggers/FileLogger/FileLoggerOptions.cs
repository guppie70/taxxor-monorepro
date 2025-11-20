namespace Taxxor.ServiceLibrary.Loggers
{
	public class FileLoggerOptions : FormattingLoggerOptions
	{
		public string Path { get; set; } = string.Empty;
		public string Prefix { get; set; } = "log-";
		public string DateFormat = "yyyyMMdd";		// currently fixed, since cleaning up files depends on parsing this as number
		public string Extension { get; set; } = "txt";
		public int RetainedFiles { get; set; } = 0;
		public int RetainedPeriods { get; set; } = 7;
		public IntervalOption Interval { get; set; } = IntervalOption.Daily;
		public int SizeLimit { get; set; } = 10485760;
		//public int SizeLimitMB { get => SizeLimit / 1048576; set => SizeLimit = value * 1048576; }

		public int BatchSize { get; set; } = 1000;
		public int BatchInterval { get; set; } = 200;

		public FileLoggerOptions()
		{
			_timestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";
		}
	}

	public enum IntervalOption
	{
		Daily,
		Weekly,
		Monthly
	}
}
