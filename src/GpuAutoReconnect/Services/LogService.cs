namespace GpuAutoReconnect.Services;

public class LogService : IDisposable
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GpuAutoReconnect", "logs");

    private StreamWriter? _writer;
    private readonly object _lock = new();
    private readonly List<string> _recentEntries = new();
    private const int MaxRecentEntries = 500;

    public event Action<string>? EntryAdded;

    public void Initialize()
    {
        Directory.CreateDirectory(LogDir);
        var fileName = $"log_{DateTime.Now:yyyyMMdd}.txt";
        var path = Path.Combine(LogDir, fileName);
        _writer = new StreamWriter(path, append: true) { AutoFlush = true };
    }

    public string LogDirectory => LogDir;

    public void Debug(string message) => Write("DEBUG", message);
    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (_lock)
        {
            _writer?.WriteLine(entry);
            _recentEntries.Add(entry);
            if (_recentEntries.Count > MaxRecentEntries)
                _recentEntries.RemoveAt(0);
        }
        EntryAdded?.Invoke(entry);
    }

    public IReadOnlyList<string> GetRecentEntries()
    {
        lock (_lock)
        {
            return _recentEntries.ToList();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
