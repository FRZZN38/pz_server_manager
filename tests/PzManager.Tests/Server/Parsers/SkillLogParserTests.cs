using PzManager.Core.Domain;
using PzManager.Server.LogReader;
using PzManager.Server.Parsers;

namespace PzManager.Tests.Server.Parsers;

public sealed class SkillLogParserTests
{
    private const string SkillLog =
        "[05-07-26 21:55:02.155] " +
        "[76561198064901882]" +
        "[UserName]" +
        "[6057,5349,0]" +
        "[Level Changed]" +
        "[Woodwork]" +
        "[4]" +
        "[Hours Survived: 0].";

    private static readonly DateTime TestTimestamp =
        new(2026, 7, 5, 21, 55, 2, 155);

    [Fact]
    public void Parse_ReturnsSkillLevelChangedEvent()
    {
        var parser = new SkillLogParser();

        var result = parser.Parse(CreateEntry(SkillLog));

        Assert.NotNull(result);
        Assert.IsType<SkillLevelChangedEvent>(result);
    }

    [Fact]
    public void Parse_ReadsUsername()
    {
        var parser = new SkillLogParser();

        var result = (SkillLevelChangedEvent)parser.Parse(CreateEntry(SkillLog))!;

        Assert.Equal("UserName", result.Username);
    }

    [Fact]
    public void Parse_ReadsSkill()
    {
        var parser = new SkillLogParser();

        var result = (SkillLevelChangedEvent)parser.Parse(CreateEntry(SkillLog))!;

        Assert.Equal("Woodwork", result.UpdatedSkill.Id);
    }

    [Fact]
    public void Parse_ReadsLevel()
    {
        var parser = new SkillLogParser();

        var result = (SkillLevelChangedEvent)parser.Parse(CreateEntry(SkillLog))!;

        Assert.Equal(4, result.UpdatedSkill.Level);
    }

    [Fact]
    public void Parse_PreservesTimestamp()
    {
        var parser = new SkillLogParser();

        var result = (SkillLevelChangedEvent)parser.Parse(CreateEntry(SkillLog))!;

        Assert.Equal(TestTimestamp, result.Timestamp);
    }

    private static PerkLogEntry CreateEntry(string line)
    {
        return new PerkLogEntry(TestTimestamp, line);
    }
}