using WpfPilot.Mcp.Core.Models;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// Bounded, file-rotated audit log. In-memory ring buffer + spill-to-disk JSONL.
/// Default cap: 10 000 entries in memory, 5 MB per file, 3 files rotated.
/// </summary>
public sealed class AuditLog : IAuditLog
{
    private const int DefaultInMemoryCap = 10_000;
    private const long DefaultMaxFileBytes = 5 * 1024 * 1024;
    private const int DefaultRetainedFiles = 3;

    private readonly object _lock = new();
    private readonly LinkedList<AuditEntry> _entries = new();
    private readonly int _inMemoryCap;
    private readonly long _maxFileBytes;
    private readonly int _retainedFiles;
    private readonly string _directory;
    private long _nextId;

    public AuditLog()
        : this(DefaultInMemoryCap, DefaultMaxFileBytes, DefaultRetainedFiles, DefaultDirectory()) { }

    public AuditLog(int inMemoryCap, long maxFileBytes, int retainedFiles, string directory)
    {
        _inMemoryCap = inMemoryCap;
        _maxFileBytes = maxFileBytes;
        _retainedFiles = retainedFiles;
        _directory = directory;
        try { Directory.CreateDirectory(directory); } catch { /* best effort */ }
    }

    private static string DefaultDirectory()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "WpfPilot", "audit");
    }

    public void Record(AuditEntry entry)
    {
        lock (_lock)
        {
            entry.Id = ++_nextId;
            _entries.AddFirst(entry);
            while (_entries.Count > _inMemoryCap)
                _entries.RemoveLast();
        }
    }

    public void Record(string tool, ElementCriteria? selector = null, string? path = null,
                Dictionary<string, object?>? parameters = null,
                string result = "success", string? error = null, long durationMs = 0)
    {
        Record(new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Tool = tool,
            Selector = selector,
            Path = path,
            Parameters = parameters,
            Result = result,
            Error = error,
            DurationMs = durationMs
        });
    }

    public IReadOnlyList<AuditEntry> GetEntries(int? limit = null)
    {
        lock (_lock)
        {
            var list = _entries.ToList();
            if (limit is int n && n < list.Count)
                list = list.GetRange(0, n);
            return list;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    public void Flush()
    {
        List<AuditEntry> snapshot;
        lock (_lock)
        {
            snapshot = _entries.ToList();
        }
        if (snapshot.Count == 0) return;

        try
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var path = Path.Combine(_directory, $"audit-{date}.jsonl");
            var currentSize = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (currentSize > _maxFileBytes)
            {
                Rotate(path);
                path = Path.Combine(_directory, $"audit-{date}.jsonl");
            }
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            foreach (var entry in snapshot)
            {
                writer.WriteLine(JsonSerializer.Serialize(entry, JsonDefaults.Options));
            }
        }
        catch
        {
            // best effort - audit flushing must never fail user operations
        }
    }

    private void Rotate(string path)
    {
        try
        {
            for (int i = _retainedFiles - 1; i >= 0; i--)
            {
                var src = i == 0 ? path : Path.Combine(_directory, $"audit-{DateTime.UtcNow:yyyy-MM-dd}.{i}.jsonl");
                var dst = Path.Combine(_directory, $"audit-{DateTime.UtcNow:yyyy-MM-dd}.{i + 1}.jsonl");
                if (File.Exists(src))
                {
                    if (i + 1 >= _retainedFiles) File.Delete(src);
                    else File.Move(src, dst);
                }
            }
        }
        catch { /* best effort */ }
    }
}
