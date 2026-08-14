using System.Collections.Concurrent;

namespace L850Manager.Services;

public sealed record LogEntry(DateTimeOffset Timestamp, string Area, string Message)
{
    public override string ToString() => $"{Timestamp:HH:mm:ss}  {Area,-7} {Message}";
}

public sealed class AppLog
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    public event EventHandler? Changed;

    public IReadOnlyList<LogEntry> Entries => _entries.Reverse().Take(250).Reverse().ToArray();

    public void Info(string area, string message)
    {
        string sanitized = message.Replace("\r", " ").Replace("\n", " ");
        _entries.Enqueue(new(DateTimeOffset.Now, area.ToUpperInvariant(), sanitized));
        while (_entries.Count > 250) _entries.TryDequeue(out _);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
