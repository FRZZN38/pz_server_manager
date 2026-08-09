using Discord;
using PzManager.Discord;

namespace PzManager.Tests.Discord;

public sealed class SlashCommandHelpFormatterTests
{
    [Fact]
    public void Build_ListsHeaderAndEachCommandWithItsDescription()
    {
        var noArgs = new SlashCommandBuilder()
            .WithName("status")
            .WithDescription("Show the current status.");

        var withArgs = new SlashCommandBuilder()
            .WithName("player")
            .WithDescription("Show information about a player.")
            .AddOption("username", ApplicationCommandOptionType.String, "Player username", isRequired: true);

        var text = SlashCommandHelpFormatter.Build("**Header**", [noArgs, withArgs]);

        Assert.StartsWith("**Header**", text);
        Assert.Contains("`/status` - Show the current status.", text);
        Assert.Contains("`/player <username>` - Show information about a player.", text);
    }

    [Fact]
    public void Build_WrapsOptionalOptionsInBrackets()
    {
        var command = new SlashCommandBuilder()
            .WithName("get_config")
            .WithDescription("Read config.")
            .AddOption("file", ApplicationCommandOptionType.String, "Which file", isRequired: false);

        var text = SlashCommandHelpFormatter.Build("Header", [command]);

        Assert.Contains("`/get_config [file]`", text);
    }
}
