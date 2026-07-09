using System.Text.RegularExpressions;
using PzManager.Core.Domain;
using PzManager.Server.LogReader;

namespace PzManager.Server.Parsers;

public sealed class SkillLogParser
{
    private static readonly string[] SkillNames =
    {
        "Cooking",
        "Fitness",
        "Strength",
        "Blunt",
        "Axe",
        "Sprinting",
        "Lightfoot",
        "Nimble",
        "Sneak",
        "Woodwork",
        "Aiming",
        "Reloading",
        "Farming",
        "Fishing",
        "Trapping",
        "PlantScavenging",
        "Doctor",
        "Electricity",
        "MetalWelding",
        "Mechanics",
        "Spear",
        "Maintenance",
        "SmallBlade",
        "LongBlade",
        "SmallBlunt",
        "Tailoring"
    };

    private static readonly Regex TokenRegex =
        new(@"\[(.*?)\]", RegexOptions.Compiled);

    public IDomainEvent? Parse(PerkLogEntry entry)
    {
        var matches = TokenRegex.Matches(entry.Line);

        if (matches.Count < 5)
            return null;

        var steamId = long.Parse(matches[1].Groups[1].Value);
        var username = matches[2].Groups[1].Value;

        var coords = matches[3].Groups[1].Value.Split(',');

        var position = new Position(
            int.Parse(coords[0]),
            int.Parse(coords[1]),
            int.Parse(coords[2]));

        var action = matches[4].Groups[1].Value;

        var hours = ParseHours(entry.Line);

        return action switch
        {
            "Login" => new PlayerLoggedInEvent(
                entry.Timestamp,
                steamId,
                username,
                position,
                hours),

            "Died" => new PlayerDiedEvent(
                entry.Timestamp,
                steamId,
                username,
                position,
                hours),

            "Level Changed" => ParseSkillLevelChanged(
                entry,
                matches,
                steamId,
                username,
                position,
                hours),

            var a when a.StartsWith("Created") =>
                new PlayerCreatedEvent(
                    entry.Timestamp,
                    steamId,
                    username,
                    position,
                    hours),
            
            var a when IsSkillSummary(a) =>
                ParsePlayerSetSkills(
                    entry,
                    a,
                    steamId,
                    username,
                    position,
                    hours),

            _ => null
        };
    }

    private static SkillLevelChangedEvent? ParseSkillLevelChanged(
        PerkLogEntry entry,
        MatchCollection matches,
        long steamId,
        string username,
        Position position,
        int hoursSurvived)
    {
        if (matches.Count < 7)
            return null;

        if (!int.TryParse(matches[6].Groups[1].Value, out var level))
            return null;

        return new SkillLevelChangedEvent(
            entry.Timestamp,
            steamId,
            username,
            position,
            new Skill(matches[5].Groups[1].Value, level),
            hoursSurvived);
    }

    private static PlayerSetSkillsEvent ParsePlayerSetSkills(
        PerkLogEntry entry,
        string action,
        long steamId,
        string username,
        Position position,
        int hoursSurvived)
    {
        var skills = action
            .Split(", ")
            .Select(skill =>
            {
                var parts = skill.Split('=');

                return new Skill(
                    parts[0],
                    int.Parse(parts[1]));
            })
            .ToList();

        return new PlayerSetSkillsEvent(
            entry.Timestamp,
            steamId,
            username,
            position,
            skills,
            hoursSurvived);
    }

    private static int ParseHours(string line)
    {
        var match = Regex.Match(line, @"Hours Survived:\s*(\d+)");

        return match.Success
            ? int.Parse(match.Groups[1].Value)
            : 0;
    }

    private static bool IsSkillSummary(string action)
    {
        return SkillNames.All(skill => action.Contains($"{skill}="));
    }
}