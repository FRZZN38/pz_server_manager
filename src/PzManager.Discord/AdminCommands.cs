using Discord;
using Discord.WebSocket;
using PzManager.Core.Logger;
using PzManager.Server;

namespace PzManager.Discord;

public sealed class AdminCommands
{
    private static readonly string[] CommandNames = ["save", "broadcast", "kick", "start", "restart", "stop"];

    private readonly ServerManager _serverManager;
    private readonly ulong _adminChannelId;

    public AdminCommands(ServerManager serverManager, ulong adminChannelId)
    {
        _serverManager = serverManager;
        _adminChannelId = adminChannelId;
    }

    public bool CanHandle(string commandName)
    {
        return CommandNames.Contains(commandName);
    }

    public async Task Register(
        DiscordSocketClient client,
        ulong guildId)
    {
        var guild = client.GetGuild(guildId);

        if (guild == null)
            throw new InvalidOperationException(
                $"Unable to resolve guild {guildId}.");

        var save = new SlashCommandBuilder()
            .WithName("save")
            .WithDescription("Save the world immediately.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        var broadcast = new SlashCommandBuilder()
            .WithName("broadcast")
            .WithDescription("Send a message to every connected player.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(
                "message",
                ApplicationCommandOptionType.String,
                "Message to broadcast",
                isRequired: true);

        var kick = new SlashCommandBuilder()
            .WithName("kick")
            .WithDescription("Kick a connected player.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(
                "username",
                ApplicationCommandOptionType.String,
                "Player username",
                isRequired: true);

        var start = new SlashCommandBuilder()
            .WithName("start")
            .WithDescription("Start the Project Zomboid server.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        var restart = new SlashCommandBuilder()
            .WithName("restart")
            .WithDescription("Restart the Project Zomboid server.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        var stop = new SlashCommandBuilder()
            .WithName("stop")
            .WithDescription("Stop the Project Zomboid server.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        foreach (var command in new[] { save, broadcast, kick, start, restart, stop })
        {
            await guild.CreateApplicationCommandAsync(
                command.Build());
        }
    }

    public async Task Handle(
        SocketSlashCommand command)
    {
        if (command.Channel.Id != _adminChannelId)
        {
            await command.RespondAsync(
                "This command can only be used in the admin channel.",
                ephemeral: true);

            return;
        }

        switch (command.Data.Name)
        {
            case "save":
                await _serverManager.SaveAsync();
                await command.RespondAsync("💾 Save requested.");
                break;

            case "broadcast":
                var message = ReadOption(command);
                await _serverManager.BroadcastAsync(message);
                await command.RespondAsync($"📢 Broadcasted: {message}");
                break;

            case "kick":
                var username = ReadOption(command);
                await _serverManager.KickAsync(username);
                await command.RespondAsync($"👢 Kicked `{username}`.");
                break;

            case "start":
                if (_serverManager.State.ConnectionState != ServerConnectionState.Offline)
                {
                    await command.RespondAsync(
                        "⚠️ The server is already running (or starting).",
                        ephemeral: true);

                    break;
                }

                await command.RespondAsync("▶️ Starting the server...");
                await _serverManager.StartAsync();
                break;

            case "restart":
                if (_serverManager.State.ConnectionState == ServerConnectionState.Offline)
                {
                    await command.RespondAsync(
                        "⚠️ The server isn't running. Use `/start` instead.",
                        ephemeral: true);

                    break;
                }

                await command.RespondAsync("🔄 Restarting the server...");
                await _serverManager.RestartAsync();
                break;

            case "stop":
                if (_serverManager.State.ConnectionState == ServerConnectionState.Offline)
                {
                    await command.RespondAsync(
                        "⚠️ The server is already stopped.",
                        ephemeral: true);

                    break;
                }

                await command.RespondAsync("🛑 Stopping the server...");
                await _serverManager.StopAsync();
                break;
        }
    }

    private static string ReadOption(SocketSlashCommand command)
    {
        return command.Data.Options
            .First()
            .Value!
            .ToString()!;
    }
}
