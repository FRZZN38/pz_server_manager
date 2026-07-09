namespace PzManager.Server.LogReader;

public sealed record PerkLogEntry(
    DateTime Timestamp,
    string Line);