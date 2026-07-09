using PzManager.Core.Domain;
using PzManager.Core.Services;

namespace PzManager.Tests.Core.Services;

public sealed class PlayerServiceTests
{
    [Fact]
    public void Handle_PlayerLoggedInEvent_CreatesPlayer()
    {
        var service = new PlayerService();

        var timestamp = new DateTime(2026, 7, 5, 21, 55, 2);
        var steam_id = 11111111111111111;
        const string username = "UserName";
        var position = new Position(100, 200, 0);
        const int hours_survived = 10;

        service.Handle(new PlayerLoggedInEvent(
            timestamp,
            steam_id,
            username,
            position,
            hours_survived));

        var player = service.GetPlayer(username);

        Assert.NotNull(player);

        Assert.Equal(timestamp, player.LastUpdated);
        Assert.Equal(steam_id, player.SteamId);
        Assert.Equal(username, player.Username);
        Assert.Equal(position, player.Position);
        Assert.Empty(player.Skills);
        Assert.Equal(hours_survived, player.HoursSurvived);

        Assert.Single(service.GetPlayers());
    }

    [Fact]
    public void Handle_PlayerSetSkillsEvent_SetsAllPlayerSkills()
    {
        var service = new PlayerService();

        var timestamp = new DateTime(2026, 7, 5, 21, 55, 2);
        var steam_id = 11111111111111111;
        const string username = "UserName";
        var position = new Position(100, 200, 0);
        const int hoursSurvived = 10;

        var skills = new[]
        {
            new Skill("Woodwork", 4),
            new Skill("Cooking", 2),
            new Skill("Fitness", 9)
        };

        service.Handle(new PlayerSetSkillsEvent(
            timestamp,
            steam_id,
            username,
            position,
            skills,
            hoursSurvived));

        var player = service.GetPlayer(username);

        Assert.NotNull(player);

        Assert.Equal(username, player!.Username);

        Assert.Equal(3, player.Skills.Count);

        Assert.True(player.Skills.ContainsKey("Woodwork"));
        Assert.True(player.Skills.ContainsKey("Cooking"));
        Assert.True(player.Skills.ContainsKey("Fitness"));

        Assert.Equal(4, player.Skills["Woodwork"].Level);
        Assert.Equal(2, player.Skills["Cooking"].Level);
        Assert.Equal(9, player.Skills["Fitness"].Level);

        Assert.Single(service.GetPlayers());
    }
}