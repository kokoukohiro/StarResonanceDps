using System.Text;

namespace StarResonanceDps.Core.Logging;

public enum PacketDiagnosticLogLevel
{
    Information,
    Warning,
    Error
}

public sealed record PacketDiagnosticLogEntry(
    DateTimeOffset Timestamp,
    PacketDiagnosticLogLevel Level,
    string Source,
    string Message)
{
    public string DisplayText => $"{Timestamp:HH:mm:ss.fff} [{GetLevelText(Level)}] [{Source}] {Message}";

    private static string GetLevelText(PacketDiagnosticLogLevel level)
    {
        return level switch
        {
            PacketDiagnosticLogLevel.Warning => "WARN",
            PacketDiagnosticLogLevel.Error => "ERROR",
            _ => "INFO"
        };
    }
}

public sealed class PacketDiagnosticLogStore
{
    private const int MaximumEntryCount = 500;

    private static readonly Lazy<PacketDiagnosticLogStore> LazyInstance = new(() => new PacketDiagnosticLogStore());

    private readonly object _sync = new();
    private readonly Queue<PacketDiagnosticLogEntry> _entries = new();

    private PacketDiagnosticLogStore()
    {
    }

    public static PacketDiagnosticLogStore Instance => LazyInstance.Value;

    public event EventHandler? EntriesChanged;

    public IReadOnlyList<PacketDiagnosticLogEntry> Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _entries.ToArray();
            }
        }
    }

    public string GetDisplayText()
    {
        PacketDiagnosticLogEntry[] entries;
        lock (_sync)
        {
            entries = _entries.ToArray();
        }

        if (entries.Length == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var entry in entries)
        {
            if (text.Length > 0)
            {
                text.AppendLine();
            }

            text.Append(entry.DisplayText);
        }

        return text.ToString();
    }

    public void Information(string source, string message)
    {
        Write(PacketDiagnosticLogLevel.Information, source, message, null);
    }

    public void Warning(string source, string message, Exception? exception = null)
    {
        Write(PacketDiagnosticLogLevel.Warning, source, message, exception);
    }

    public void Error(string source, string message, Exception? exception = null)
    {
        Write(PacketDiagnosticLogLevel.Error, source, message, exception);
    }

    private void Write(
        PacketDiagnosticLogLevel level,
        string source,
        string message,
        Exception? exception)
    {
        var resolvedSource = string.IsNullOrWhiteSpace(source) ? "Packet" : source.Trim();
        var resolvedMessage = string.IsNullOrWhiteSpace(message) ? "No message." : message.Trim();
        if (exception is not null)
        {
            resolvedMessage = $"{resolvedMessage}{Environment.NewLine}{exception}";
        }

        lock (_sync)
        {
            while (_entries.Count >= MaximumEntryCount)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(new PacketDiagnosticLogEntry(
                DateTimeOffset.Now,
                level,
                resolvedSource,
                resolvedMessage));
        }

        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }
}
