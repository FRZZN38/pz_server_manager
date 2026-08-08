using PzManager.Core.Domain;
using PzManager.Discord;

namespace PzManager.Tests.Discord;

public sealed class PlayerMessageFormatterTests
{
    [Fact]
    public void BuildSummary_IncludesPlayerDetailsAndFormatsRelevantSkills()
    {
        var player = new Player(
            new DateTime(2026, 7, 9, 12, 30, 0),
            12345678901234567,
            "TestPlayer",
            new Position(10, 20, 0),
            42);

        player.SetSkill(new Skill("Cooking", 2));
        player.SetSkill(new Skill("Woodwork", 5));
        player.SetSkill(new Skill("Fitness", 0));
        player.SetSkill(new Skill("Aiming", 2));

        var embed = PlayerMessageFormatter.BuildSummary(player);

        Assert.Equal("TestPlayer", embed.Title);
        Assert.Contains(embed.Fields, field => field.Name == "Status" && field.Value?.ToString() == "Alive");
        Assert.Contains(embed.Fields, field => field.Name == "Hours Survived (in-game)" && field.Value?.ToString() == "42");
        Assert.Contains(embed.Fields, field => field.Name == "Location" && field.Value?.ToString() == "`X:10` `Y:20` `Z:0`");
        Assert.Contains(embed.Fields, field => field.Name == "Skills" && field.Value?.ToString() == "`Woodwork`: **5**\n`Aiming`: **2**\n`Cooking`: **2**");
        Assert.DoesNotContain(embed.Fields, field => field.Name == "Steam ID");
        Assert.DoesNotContain(embed.Fields, field => field.Value?.ToString() == "Fitness: 0");
    }
}
