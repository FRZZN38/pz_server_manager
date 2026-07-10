using PzManager.Discord;

namespace PzManager.Tests.Discord;

public sealed class BotHelpFormatterTests
{
    [Fact]
    public void BuildHelpMessage_ListsAvailableCommands()
    {
        var message = BotHelpFormatter.BuildHelpMessage();

        Assert.Contains("!player <username>", message);
        Assert.Contains("!help", message);
        Assert.DoesNotContain("Steam ID", message);
    }
}
