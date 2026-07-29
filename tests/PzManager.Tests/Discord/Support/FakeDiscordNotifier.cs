using PzManager.Discord;

namespace PzManager.Tests.Discord.Support;

public sealed class FakeDiscordNotifier : IDiscordNotifier
{
    public List<string> Messages { get; } = [];

    public List<string> AdminMessages { get; } = [];

    public Task Send(string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task SendAdmin(string message)
    {
        AdminMessages.Add(message);
        return Task.CompletedTask;
    }
}
