#nullable enable

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;


namespace Taxxor.ServiceLibrary.Loggers
{
	[ProviderAlias("FileLogger")]
	public class FileLoggerProvider : FormattingLoggerProvider
	{
		public FileLoggerProvider(IOptionsMonitor<FileLoggerOptions> options) : base(options)
		{
			options.OnChange(x => LoadOptions(x));
			LoadOptions(options.CurrentValue);
		}

		private void LoadOptions(FileLoggerOptions options)
		{
			Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, options.Path));
		}

		protected override void ProcessLogQueue()
		{
			try
			{
				while (!_messageQueue.IsAddingCompleted)
				{
					FileLoggerOptions options = (FileLoggerOptions)_options.CurrentValue;

					int batchSize = options.BatchSize;
					bool result = true;
					IntervalOption interval = options.Interval;
					string dateFormat = options.DateFormat;
					for (LogMessageEntry? item = _messageQueue.Take(); result; result = _messageQueue.TryTake(out item))
					{
						StreamWriter writer = CreateWriter(options, GetSuffix(item!.Timestamp, interval, dateFormat));

						writer.WriteLine(item.Message);

						if (batchSize > 0 && --batchSize == 0)
							break;
					}

					_currentWriter!.Flush();

					int batchInterval = options.BatchInterval;
					if (batchInterval > 0)
						Thread.Sleep(batchInterval);
				}
			}
			catch
			{
				_messageQueue.CompleteAdding();
			}
		}

		private string? _currentPeriod;
		private int _subIndex = -1;
		private StreamWriter? _currentWriter;

		private StreamWriter CreateWriter(FileLoggerOptions options, string period)
		{
			FileMode mode = FileMode.Create;
			if (_subIndex < 0)
			{
				// Initialisation - determine highest index of current period
				_currentPeriod = period;
				_subIndex = 0;
				if (options.SizeLimit > 0)
				{
					string extension = options.Extension;
					if (!string.IsNullOrEmpty(extension))
						extension = $".{extension}";
					int offset = options.Prefix.Length + period.Length + 1;
					int ext = extension.Length;
					(FileInfo info, int index) = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, options.Path)).GetFiles($"{options.Prefix}{period}*{extension}").Select(x => (info: x, index: x.Name.Length > offset + ext && int.TryParse(x.Name[offset..^ext], out int y) ? y : 0)).OrderByDescending(x => x.index).FirstOrDefault();
					if (info != null)
						_subIndex = index + (info.Length > options.SizeLimit ? 1 : 0);
				}
				mode = FileMode.Append;
			}
			else if (_currentPeriod == period)
			{
				// Same period as current item; check file size and increase index if necessary
				if (options.SizeLimit == 0 || _currentWriter!.BaseStream.Position <= options.SizeLimit)
					return _currentWriter!;

				_subIndex++;
			}
			else
			{
				// New period
				_currentPeriod = period;
				_subIndex = 0;
			}
			// Close old writer and create new file
			string fileName = GetFileName(options, period);
			_currentWriter?.Close();
			_currentWriter = new StreamWriter(File.Open(fileName, mode, FileAccess.Write, FileShare.Read));

			Cleanup();
			return _currentWriter;
		}

		private string GetSuffix(DateTime timestamp, IntervalOption interval, string dateFormat)
		{
			return interval switch
			{
				IntervalOption.Daily => timestamp.ToString(dateFormat),
				IntervalOption.Weekly => timestamp.AddDays(-(int)timestamp.DayOfWeek).ToString(dateFormat),
				IntervalOption.Monthly => timestamp.AddDays(-timestamp.Day).ToString(dateFormat),
				_ => throw new Exception("Invalid logging interval")
			};
		}

		private readonly StringBuilder _stringBuilder = new();
		private string GetFileName(FileLoggerOptions options, string period)
		{
			_stringBuilder.Append(options.Prefix);
			_stringBuilder.Append(period);
			if (_subIndex > 0)
				_stringBuilder.Append('-').Append(_subIndex);
			if (!string.IsNullOrEmpty(options.Extension))
				_stringBuilder.Append('.').Append(options.Extension);
			string name = Path.Combine(options.Path, _stringBuilder.ToString());
			_stringBuilder.Clear();
			return name;
		}

		private void Cleanup()
		{
			try
			{
				FileLoggerOptions options = (FileLoggerOptions)_options.CurrentValue;

				int retainPeriods = options.RetainedPeriods;
				int retainFiles = options.RetainedFiles;
				if (retainFiles == 0 && retainPeriods == 0)
					return;

				string extension = options.Extension;
				if (!string.IsNullOrEmpty(extension))
					extension = $".{extension}";
				int offset = options.Prefix.Length;
				int ext = extension.Length;
				IOrderedEnumerable<(FileInfo info, int period, int index)> files = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, options.Path)).GetFiles($"{options.Prefix}*{extension}").Select(x =>
				{
					string[] info = x.Name[offset..^ext].Split('-');
					return (info: x, period: int.TryParse(info[0], out int p) ? p : 0, index: info.Length > 1 && int.TryParse(info[1], out int y) ? y : 0);
				}).OrderByDescending(x => x.period).ThenByDescending(x => x.index);

				int periodCutoff = retainPeriods > 0 ? files.Select(x => x.period).Distinct().Skip(retainPeriods).FirstOrDefault() : 0;
				foreach ((FileInfo info, int period, int index) in files.SkipWhile((f, i) => (retainFiles == 0 || i < retainFiles) && f.period > periodCutoff))
					info.Delete();
			}
			catch
			{ }
		}
	}
}
