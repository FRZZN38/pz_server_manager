using PzManager.Core.Domain;

namespace PzManager.Tests.Core.Domain;

public sealed class PlayerTests
{
    [Fact]
    public void Constructor_InitializesPlayer()
    {
        var timestamp = new DateTime(2026, 7, 5, 21, 55, 2);
        var steam_id = 11111111111111111;
        var user_name = "UserName";
        var position = new Position(6057, 5349, 0);
        var hours_survived = 12;

        var player = new Player(
            timestamp,
            steam_id,
            user_name,
            position,
            hours_survived);

        Assert.Equal(timestamp, player.LastUpdated);
        Assert.Equal(user_name, player.Username);
        Assert.Equal(position, player.Position);
        Assert.Equal(hours_survived, player.HoursSurvived);
        Assert.Empty(player.Skills);
    }

    [Fact]
    public void SetSkill_AddsNewSkill()
    {
        var timestamp = new DateTime(2026, 7, 5, 21, 55, 2);
        var steam_id = 11111111111111111;
        var user_name = "UserName";
        var position = new Position(6057, 5349, 0);
        var hours_survived = 12;

        var player = new Player(
            timestamp,
            steam_id,
            user_name,
            position,
            hours_survived);

        player.SetSkill(new Skill("Woodwork", 3));

        Assert.Single(player.Skills);
        Assert.True(player.Skills.ContainsKey("Woodwork"));
        Assert.Equal(3, player.Skills["Woodwork"].Level);
    }

    [Fact]
    public void SetSkill_UpdatesExistingSkill()
    {
        var timestamp = new DateTime(2026, 7, 5, 21, 55, 2);
        var steam_id = 11111111111111111;
        var user_name = "UserName";
        var position = new Position(6057, 5349, 0);
        var hours_survived = 12;

        var player = new Player(
            timestamp,
            steam_id,
            user_name,
            position,
            hours_survived);

        player.SetSkill(new Skill("Woodwork", 3));

        player.SetSkill(new Skill("Woodwork", 4));

        Assert.Single(player.Skills);
        Assert.Equal(4, player.Skills["Woodwork"].Level);
    }
}