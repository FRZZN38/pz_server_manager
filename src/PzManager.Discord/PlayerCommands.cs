using Discord;
using Discord.WebSocket;
using PzManager.Core.Logger;
using PzManager.Core.Services;
using PzManager.Server;

namespace PzManager.Discord;

public sealed class PlayerCommands
{
    private readonly PlayerService _players;
    private ServerManager? _serverManager;

    public PlayerCommands(PlayerService players)
    {
        _players = players;
    }

    // Set once ServerManager exists (it's constructed after PlayerCommands -
    // see Program.cs), so /status can read the current connection state.
    public void AttachServerManager(ServerManager serverManager)
    {
        _serverManager = serverManager;
    }

    public IReadOnlyList<SlashCommandBuilder> BuildCommands()
    {
        var help = new SlashCommandBuilder()
            .WithName("help")
            .WithDescription("Show available commands.");

        var status = new SlashCommandBuilder()
            .WithName("status")
            .WithDescription("Show the Project Zomboid server's current status.");

        var player = new SlashCommandBuilder()
            .WithName("player")
            .WithDescription("Show information about a player.")
            .AddOption(
                "username",
                ApplicationCommandOptionType.String,
                "Player username",
                isRequired: true);

        return [help, status, player];
    }

    public async Task Handle(
        SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "help":
                await command.RespondAsync(
                    SlashCommandHelpFormatter.Build("**Available commands:**", BuildCommands()), ephemeral: true);

                break;

            case "status":
                var text = _serverManager is null
                    ? "Status is not available right now."
                    : ServerStateFormatter.BuildPublic(_serverManager.State);

                await command.RespondAsync(text, ephemeral: true);

                break;

            case "player":
                await HandlePlayer(command);

                break;
        }
    }

    private async Task HandlePlayer(
        SocketSlashCommand command)
    {
        var username = command.Data.Options
            .First()
            .Value!
            .ToString()!;

        var player = _players.GetPlayer(username);

        if (player == null)
        {
            await command.RespondAsync(
                $"No player found for `{username}`.", ephemeral: true);

            return;
        }

        await command.RespondAsync(
            embed: PlayerMessageFormatter.BuildSummary(player), ephemeral: true);
    }
}