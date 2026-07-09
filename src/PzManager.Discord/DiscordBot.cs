using Discord;
using Discord.WebSocket;

namespace PzManager.Discord;

public sealed class DiscordBot
{
    private readonly DiscordSocketClient _client = new();
    private IMessageChannel? _channel;

    public async Task Connect(
        string token,
        ulong channelId)
    {
        _client.Ready += () =>
        {
            _channel = _client.GetChannel(channelId) as IMessageChannel;
            return Task.CompletedTask;
        };

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }

    public async Task Send(string message)
    {
        if (_channel == null)
            return;

        await _channel.SendMessageAsync(message);
    }
}