using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace backend.Hosting;

public sealed class FileLoggerOptions
{
    public const string Section = "Logging:File";

    public bool Enabled { get; init; }

    /// <summary>Thư mục log – đường dẫn tương đối tính từ thư mục của backend.exe.</summary>
    public string Path { get; init; } = "logs";

    /// <summary>Giữ log bao nhiêu ngày; file cũ hơn bị xoá khi sang ngày mới.</summary>
    public int RetainDays { get; init; } = 14;
}

/// <summary>
/// Ghi log ra file theo ngày (<c>logs/backend-20261029.log</c>) – đủ cho 1 tiến trình chạy tay / Windows Service,
/// không cần thêm thư viện. Mức log lấy theo cấu hình Logging:LogLevel như console.
/// </summary>
public sealed class FileLoggerProvider(FileLoggerOptions options, string baseDirectory, TimeProvider time) : ILoggerProvider
{
    private readonly string _directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory, options.Path));
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly Lock _lock = new();
    private StreamWriter? _writer;
    private DateOnly _day;

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    internal string CurrentFile => FilePath(DateOnly.FromDateTime(time.GetLocalNow().DateTime));

    private string FilePath(DateOnly day) => System.IO.Path.Combine(_directory, $"backend-{day:yyyyMMdd}.log");

    internal void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var now = time.GetLocalNow();
        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" [").Append(Level(level)).Append("] ")
            .Append(category).Append(": ").Append(message);
        if (exception is not null) line.AppendLine().Append(exception);

        lock (_lock)
        {
            var day = DateOnly.FromDateTime(now.DateTime);
            if (_writer is null || day != _day) Roll(day);
            _writer!.WriteLine(line.ToString());
        }
    }

    private void Roll(DateOnly day)
    {
        _writer?.Dispose();
        Directory.CreateDirectory(_directory);
        _day = day;
        // FileShare.ReadWrite: mở xem log bằng Notepad / Get-Content -Wait khi service đang chạy
        var stream = new FileStream(FilePath(day), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        var cutoff = day.AddDays(-options.RetainDays);
        foreach (var file in Directory.EnumerateFiles(_directory, "backend-*.log"))
        {
            var stamp = System.IO.Path.GetFileNameWithoutExtension(file)["backend-".Length..];
            if (DateOnly.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDay)
                && fileDay < cutoff)
            {
                try { File.Delete(file); } catch (IOException) { /* đang bị mở – để lần sau */ }
            }
        }
    }

    private static string Level(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "CRT",
    };

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Lọc mức log do LoggerFactory làm theo cấu hình
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) provider.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
