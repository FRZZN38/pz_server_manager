using PzManager.Core.Domain;

namespace PzManager.Tests.Core.Domain;

public sealed class EventTests
{
    [Fact]
    public void PlayerSetSkillsEvent()
    {
        var timestamp = DateTime.UtcNow;
        var steam_id = 11111111111111111;
        var user_name = "UserName";
        var position = new Position(100, 200, 0);
        var skills = new List<Skill>
        {
            new Skill("Woodwork", 4),
            new Skill("Fitness", 9)
        };
        var hours_survived = 12;

        var evt = new 
        PlayerSetSkillsEvent(
            timestamp,
            steam_id,
            user_name,
            position,
            skills,
            hours_survived);

        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Equal(steam_id, evt.SteamId);
        Assert.Equal(user_name, evt.Username);
        Assert.Equal(position, evt.Position);
        Assert.Equal(2, evt.Skills.Count);
        Assert.Contains(evt.Skills, s =>
            s.Id == "Woodwork" &&
            s.Level == 4);
        Assert.Contains(evt.Skills, s =>
            s.Id == "Fitness" &&
            s.Level == 9);
        Assert.Equal(hours_survived, evt.HoursSurvived);
    }

    [Fact]
    public void SkillLevelChangedEvent()
    {
        // Arrange
        var timestamp = new DateTime(2026, 7, 5, 21, 55, 2);
        var steam_id = 11111111111111111;
        const string username = "UserName";
        var position = new Position(100, 200, 0);
        const string skill = "Woodwork";
        const int level = 4;
        const int hours_survived = 10;

        // Act
        var evt = new SkillLevelChangedEvent(
            timestamp,
            steam_id,
            username,
            position,
            new Skill(skill, level),
            hours_survived);

        // Assert
        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Equal(steam_id, evt.SteamId);
        Assert.Equal(username, evt.Username);
        Assert.Equal(position, evt.Position);
        Assert.Equal(skill, evt.UpdatedSkill.Id);
        Assert.Equal(level, evt.UpdatedSkill.Level);
        Assert.Equal(hours_survived, evt.HoursSurvived);
    }

    [Fact]
    public void PlayerLoggedInEvent()
    {
        var timestamp = DateTime.UtcNow;
        var steam_id = 11111111111111111;
        var user_name = "UserName";
        var position = new Position(100, 200, 0);
        var hours_survived = 12;

        var evt = new PlayerLoggedInEvent(
            timestamp,
            steam_id,
            user_name,
            position,
            hours_survived);

        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Equal(steam_id, evt.SteamId);
        Assert.Equal(user_name, evt.Username);
        Assert.Equal(position, evt.Position);
        Assert.Equal(hours_survived, evt.HoursSurvived);
    }


    [Fact]
    public void PlayerDiedEvent()
    {
        var timestamp = DateTime.UtcNow;
        var steam_id = 11111111111111111;
        var user_name = "UserName";
        var position = new Position(100, 200, 0);
        var hours_survived = 42;

        var evt = new PlayerDiedEvent(
            timestamp,
            steam_id,
            user_name,
            position,
            hours_survived);

        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Equal(steam_id, evt.SteamId);
        Assert.Equal(user_name, evt.Username);
        Assert.Equal(position, evt.Position);
        Assert.Equal(hours_survived, evt.HoursSurvived);
    }
}