namespace PzManager.Server.LogReader;

public sealed record RawLogEntry(
    DateTime Timestamp,
    string Message
);
