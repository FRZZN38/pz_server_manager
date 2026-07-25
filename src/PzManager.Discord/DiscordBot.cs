using Discord;
using Discord.WebSocket;
using PzManager.Core.Services;

namespace PzManager.Discord;

public sealed class DiscordBot
{
    private readonly DiscordSocketClient _client;
    private readonly PlayerService _players;
    private IMessageChannel? _channel;
    private IMessageChannel? _adminChannel;

    public DiscordBot(PlayerService players)
    {
        _players = players;

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                | GatewayIntents.GuildMessages
                | GatewayIntents.MessageContent
        });

        _client.MessageReceived += HandleMessageAsync;
    }

    public async Task Connect(
        string token,
        ulong channelId,
        ulong adminChannelId)
    {
        var readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _client.Ready += () =>
        {
            _channel = _client.GetChannel(channelId) as IMessageChannel;
            _adminChannel = _client.GetChannel(adminChannelId) as IMessageChannel;

            if (_channel == null)
                throw new InvalidOperationException(
                    $"Unable to resolve public channel {channelId}.");

            if (_adminChannel == null)
                throw new InvalidOperationException(
                    $"Unable to resolve admin channel {adminChannelId}.");

            readyTcs.TrySetResult();
            return Task.CompletedTask;
        };

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
        await readyTcs.Task;
    }

    private async Task HandleMessageAsync(SocketMessage message)
    {
        if (message.Author.IsBot)
            return;

        if (_channel == null || message.Channel.Id != _channel.Id)
            return;

        var content = message.Content.Trim();
        if (content.Equals("!help", StringComparison.OrdinalIgnoreCase))
        {
            await _channel.SendMessageAsync(BotHelpFormatter.BuildHelpMessage());
            return;
        }

        if (content.StartsWith("!player", StringComparison.OrdinalIgnoreCase))
        {
            var parts = content.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                await _channel.SendMessageAsync("Usage: `!player <username>`");
                return;
            }

            var username = parts[1].Trim();
            var player = _players.GetPlayer(username);
            if (player == null)
            {
                await _channel.SendMessageAsync($"No player found for `{username}`.");
                return;
            }

            var embed = PlayerMessageFormatter.BuildSummary(player);
            await _channel.SendMessageAsync(embed: embed);
        }
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
