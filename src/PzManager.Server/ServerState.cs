namespace PzManager.Server;

public enum ServerConnectionState
{
    Offline,

    Starting,

    Running,

    Stopping,

    Crashed,

    /// <summary>
    /// Running <c>update-server.sh</c> (SteamCMD) while the game process
    /// itself is not running - distinct from Starting/Stopping since no
    /// game session is involved. Only entered/exited from
    /// <see cref="ServerManager.BeginUpdateAsync"/>/<see cref="ServerManager.EndUpdateAsync"/>.
    /// </summary>
    Updating,

    /// <summary>
    /// First-ever bootstrap (see <see cref="ServerConfigProvisioner.BootstrapIfNewAsync"/>):
    /// no config exists yet, so the game is started once just to let it
    /// generate its own defaults, then stopped again before the real
    /// session begins. Distinct from Updating - no SteamCMD/update is
    /// involved, this is Project Zomboid itself generating config files.
    /// Only entered/exited from <see cref="ServerManager.BeginCreateAsync"/>/
    /// <see cref="ServerManager.EndCreateAsync"/>.
    /// </summary>
    Creating
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
