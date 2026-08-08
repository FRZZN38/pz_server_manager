using Discord;
using Discord.WebSocket;

using PzManager.Core.Logger;

namespace PzManager.Discord;

public sealed class DiscordBot : IDiscordNotifier
{
    private readonly DiscordSocketClient _client;
    private IMessageChannel? _channel;
    private IMessageChannel? _adminChannel;

    private ulong _guildId;

    private readonly PlayerCommands _playerCommands;
    private AdminCommands? _adminCommands;

    public DiscordBot(PlayerCommands playerCommands)
    {
        _playerCommands = playerCommands;

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                | GatewayIntents.GuildMessages
                | GatewayIntents.MessageContent
        });

        _client.SlashCommandExecuted += HandleSlashCommandAsync;

        _client.Log += message =>
        {
            Log.Info($"[DISCORD.NET] {message.Severity} {message.Source}: {message.Message} {message.Exception}");
            return Task.CompletedTask;
        };
    }

    public void AttachAdminCommands(AdminCommands adminCommands)
    {
        _adminCommands = adminCommands;
    }

    public async Task Connect(
        string token,
        ulong guildId,
        ulong channelId,
        ulong adminChannelId)
    {
        _guildId = guildId;

        var readyTcs = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _client.Ready += () =>
        {
            // Discord.Net dispatches this handler on the gateway task, so
            // awaiting REST calls here directly blocks heartbeats. Offload
            // the work to a background task instead.
            _ = Task.Run(async () =>
            {
                try
                {
                    Log.Info("[DISCORD] Ready event received. Resolving channels...");

                    _channel = _client.GetChannel(channelId) as IMessageChannel;
                    _adminChannel = _client.GetChannel(adminChannelId) as IMessageChannel;

                    if (_channel == null)
                        throw new InvalidOperationException(
                            $"Unable to resolve public channel {channelId}.");

                    if (_adminChannel == null)
                        throw new InvalidOperationException(
                            $"Unable to resolve admin channel {adminChannelId}.");

                    Log.Info("[DISCORD] Channels resolved. Registering commands...");

                    var guild = _client.GetGuild(_guildId)
                        ?? throw new InvalidOperationException($"Unable to resolve guild {_guildId}.");

                    var commands = _playerCommands.BuildCommands()
                        .Concat(_adminCommands?.BuildCommands() ?? [])
                        .Select(builder => builder.Build())
                        .ToArray();

                    // Bulk overwrite replaces the guild's entire command set
                    // atomically, so any command left over from a previous
                    // version of the bot (renamed/removed here) disappears
                    // instead of lingering forever - CreateApplicationCommandAsync
                    // only adds/updates by name, it never deletes.
                    await guild.BulkOverwriteApplicationCommandAsync(commands);

                    Log.Info($"[DISCORD] {commands.Length} command(s) registered.");

                    readyTcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    readyTcs.TrySetException(ex);
                }
            });

            return Task.CompletedTask;
        };

        Log.Info("[DISCORD] Logging in...");
        await _client.LoginAsync(TokenType.Bot, token);

        Log.Info("[DISCORD] Logged in. Starting client...");
        await _client.StartAsync();

        Log.Info("[DISCORD] Client started. Waiting for Ready event...");
        await readyTcs.Task;

        Log.Info("[DISCORD] Connect completed.");
    }

    public async Task Disconnect()
    {
        Log.Info("[DISCORD] Disconnecting...");

        await _client.LogoutAsync();
        await _client.StopAsync();

        Log.Info("[DISCORD] Disconnected.");
    }

    private async Task HandleSlashCommandAsync(
        SocketSlashCommand command)
    {
        if (_adminCommands is not null && _adminCommands.CanHandle(command.Data.Name))
        {
            await _adminCommands.Handle(command);
            return;
        }

        await _playerCommands.Handle(command);
    }

    public async Task Send(string message)
    {
        if (_channel == null)
            return;

        await _channel.SendMessageAsync(message);
    }

    public async Task SendAdmin(string message)
    {
        if (_adminChannel == null)
            return;

        await _adminChannel.SendMessageAsync(message);
    }
}
