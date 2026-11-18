using System.Text;

namespace GameLocker.Service.Logging;

/// <summary>
/// Settings for <see cref="FileLoggerProvider"/>.
/// </summary>
public sealed class FileLoggerOptions
{
    /// <summary>The log file to write to.</summary>
    public string Path { get; set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GameLocker", "logs", "service.log");

    /// <summary>The size at which the current file is rotated.</summary>
    public long FileSizeLimitBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>How many rotated files to keep alongside the current one.</summary>
    public int RetainedFileCount { get; set; } = 3;

    /// <summary>The lowest level written to the file.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}

/// <summary>
/// Writes log entries to a size-rotated file.
/// </summary>
/// <remarks>
/// A Windows Service has no console, and the Event Log is awkward to read through a long sequence of
/// per-folder operations. Diagnostics previously went to <c>Console.WriteLine</c>, which in
/// production reached nothing at all. See ARCHITECTURE-REVIEW.md finding [T-9].
/// <para>
/// Deliberately dependency-free and small: correctness here matters more than features, and adding
/// a logging framework to a service that encrypts user data is not a trade worth making.
/// </para>
/// </remarks>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions _options;
    private readonly object _gate = new();
    private bool _disabled;

    /// <summary>
    /// Creates a provider.
    /// </summary>
    /// <param name="options">Where and how to write.</param>
    public FileLoggerProvider(FileLoggerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private bool IsEnabled(LogLevel level) => !_disabled && level >= _options.MinimumLevel;

    private void Write(string line)
    {
        lock (_gate)
        {
            if (_disabled)
                return;

            try
            {
                var directory = Path.GetDirectoryName(_options.Path);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                RotateIfNeeded();

                File.AppendAllText(_options.Path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Logging must never take the service down. If the file cannot be written - the
                // disk is full, the path is denied - stop trying rather than throwing on every
                // entry. The Event Log provider is still attached.
                _disabled = true;
            }
        }
    }

    private void RotateIfNeeded()
    {
        var current = new FileInfo(_options.Path);

        if (!current.Exists || current.Length < _options.FileSizeLimitBytes)
            return;

        // service.log -> service.1.log -> service.2.log, oldest discarded.
        var directory = current.DirectoryName ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(_options.Path);
        var extension = Path.GetExtension(_options.Path);

        string Rotated(int index) => Path.Combine(directory, $"{stem}.{index}{extension}");

        var oldest = Rotated(_options.RetainedFileCount);
        if (File.Exists(oldest))
            File.Delete(oldest);

        for (var index = _options.RetainedFileCount - 1; index >= 1; index--)
        {
            var from = Rotated(index);
            if (File.Exists(from))
                File.Move(from, Rotated(index + 1), overwrite: true);
        }

        File.Move(_options.Path, Rotated(1), overwrite: true);
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            ArgumentNullException.ThrowIfNull(formatter);

            var builder = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                .Append(" [").Append(Level(logLevel)).Append("] ")
                .Append(ShortCategory(_category)).Append(": ")
                .Append(formatter(state, exception));

            if (exception is not null)
                builder.AppendLine().Append(exception);

            _provider.Write(builder.ToString());
        }

        private static string Level(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???",
        };

        private static string ShortCategory(string category)
        {
            var lastDot = category.LastIndexOf('.');
            return lastDot >= 0 && lastDot < category.Length - 1
                ? category[(lastDot + 1)..]
                : category;
        }
    }
}
