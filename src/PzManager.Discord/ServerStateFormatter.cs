using PzManager.Server;

namespace PzManager.Discord;

public static class ServerStateFormatter
{
    /// <summary>
    /// Unified per-state icon, shared by every place that shows connection
    /// state (change notifications, /status). Starting/Stopping share the
    /// same "in progress" icon since they're both transitional. There's no
    /// separate Restarting state - a restart honestly goes through
    /// Stopping -> Offline -> Starting -> Running like any stop+start.
    /// </summary>
    private static string StatusEmoji(ServerConnectionState state)
    {
        return state switch
        {
            ServerConnectionState.Starting
                or ServerConnectionState.Stopping => "⏳",
            ServerConnectionState.Running => "🟢",
            ServerConnectionState.Offline => "🔴",
            ServerConnectionState.Crashed => "⚠️",
            _ => ""
        };
    }

    public static string Build(ServerState state)
    {
        var lastEvent = state.LastEventAt is null
            ? "None"
            : state.LastEventAt.Value.ToString("yyyy-MM-dd HH:mm:ss");

        var lastEventType = string.IsNullOrWhiteSpace(state.LastEventType)
            ? "None"
            : state.LastEventType;

        // No server name: this app only ever manages one server, so there's
        // nothing to disambiguate - "the server" is unambiguous on its own.
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"State: {state.ConnectionState}",
                $"Perk log: {state.PerkLogPath ?? "Not connected"}",
                $"Last event: {lastEventType} at {lastEvent}",
                $"Known players: {state.KnownPlayerCount}"
            });
    }

    /// <summary>
    /// Sent to the admin channel every time ConnectionState actually
    /// changes (see ServerManager.StateChanged). Deliberately terse - no
    /// server name, perk log path, last event or player count, since none
    /// of that is relevant to "did the state just change".
    /// </summary>
    public static string BuildChangeNotification(ServerState state)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                "#### SERVER STATUS CHANGED ####",
                $"{StatusEmoji(state.ConnectionState)} State: {state.ConnectionState}"
            });
    }

    /// <summary>
    /// Safe to show to anyone: connection state and player count only - no
    /// filesystem paths, last event details, or anything else internal.
    /// </summary>
    public static string BuildPublic(ServerState state)
    {
        var emoji = StatusEmoji(state.ConnectionState);

        return state.ConnectionState switch
        {
            ServerConnectionState.Running =>
                $"{emoji} The server is online. Known players: {state.KnownPlayerCount}",
            ServerConnectionState.Offline => $"{emoji} The server is offline.",
            ServerConnectionState.Starting => $"{emoji} The server is starting...",
            ServerConnectionState.Stopping => $"{emoji} The server is stopping...",
            ServerConnectionState.Crashed => $"{emoji} The server crashed and is restarting...",
            _ => $"State: {state.ConnectionState}"
        };
    }
}
