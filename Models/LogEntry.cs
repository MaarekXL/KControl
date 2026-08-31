namespace KeryxControl.Models;

public enum LogSeverity { Info, Warning, Error }

public sealed record LogEntry(DateTime Timestamp, string Message, LogSeverity Severity)
{
    public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";
    public string Foreground => Severity switch
    {
        LogSeverity.Error => "#FF6B73",
        LogSeverity.Warning => "#F2B84B",
        _ => "#A6CBB1"
    };
}
