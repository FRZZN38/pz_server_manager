namespace PzManager.Server;

public enum ServerConnectionState
{
    Offline,

    Starting,

    Running,

    Stopping,

    Crashed
}

public sealed record ServerState(
    string ServerName,
    string ConfigDirectory,
    string PerkLogDirectory,
    string? PerkLogPath,
    ServerConnectionState ConnectionState,
    DateTime? LastEventAt,
    string? LastEventType,
    int KnownPlayerCount);
