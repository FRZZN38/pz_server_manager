namespace PzManager.Server;

public enum ServerConnectionState
{
    Offline,

    Starting,

    Running,

    Saving,

    Restarting,

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
