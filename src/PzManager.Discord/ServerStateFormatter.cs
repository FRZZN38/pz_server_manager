using PzManager.Server;

namespace PzManager.Discord;

public static class ServerStateFormatter
{
    public static string Build(ServerState state)
    {
        var lastEvent = state.LastEventAt is null
            ? "None"
            : state.LastEventAt.Value.ToString("yyyy-MM-dd HH:mm:ss");

        var lastEventType = string.IsNullOrWhiteSpace(state.LastEventType)
            ? "None"
            : state.LastEventType;

        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"Server: {state.ServerName}",
                $"State: {state.ConnectionState}",
                $"Perk log: {state.PerkLogPath ?? "Not connected"}",
                $"Last event: {lastEventType} at {lastEvent}",
                $"Known players: {state.KnownPlayerCount}"
            });
    }
}
