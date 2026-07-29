using Discord;
using Discord.WebSocket;
using PzManager.Core.Logger;
using PzManager.Core.Services;

namespace PzManager.Discord;

public sealed class PlayerCommands
{
    private readonly PlayerService _players;

    public PlayerCommands(PlayerService players)
    {
        _players = players;
    }

    public async Task Register(
        DiscordSocketClient client,
        ulong guildId)
    {
        var guild = client.GetGuild(guildId);

        if (guild == null)
            throw new InvalidOperationException(
                $"Unable to resolve guild {guildId}.");

        var help = new SlashCommandBuilder()
            .WithName("help")
            .WithDescription("Show available commands.");

        var player = new SlashCommandBuilder()
            .WithName("player")
            .WithDescription("Show information about a player.")
            .AddOption(
                "username",
                ApplicationCommandOptionType.String,
                "Player username",
                isRequired: true);

        await guild.CreateApplicationCommandAsync(
            help.Build());

        await guild.CreateApplicationCommandAsync(
            player.Build());
    }

    public async Task Handle(
        SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "help":
                await command.RespondAsync(
                    BotHelpFormatter.BuildHelpMessage());

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
                $"No player found for `{username}`.",
                ephemeral: true);

            return;
        }

        await command.RespondAsync(
            embed: PlayerMessageFormatter.BuildSummary(player));
    }
}