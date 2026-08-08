using Discord;
using PzManager.Core.Domain;

namespace PzManager.Discord;

public static class PlayerMessageFormatter
{
    public static Embed BuildSummary(Player player)
    {
        var statusText = player.Dead ? "Dead" : "Alive";
        var skills = player.Skills.Values
            .Where(skill => skill.Level > 0)
            .OrderByDescending(skill => skill.Level)
            .ThenBy(skill => skill.Id)
            .ToList();

        var embed = new EmbedBuilder()
            .WithTitle(player.Username)
            .WithColor(player.Dead ? Color.DarkRed : Color.DarkGreen)
            .AddField("Status", statusText, inline: true)
            .AddField("Hours Survived (in-game)", player.HoursSurvived, inline: true)
            .AddField("Last Seen", player.LastUpdated.ToString("yyyy-MM-dd HH:mm"), inline: false)
            .AddField("Location", $"`X:{player.Position.X}` `Y:{player.Position.Y}` `Z:{player.Position.Z}`", inline: false);

        if (player.Skills.Count == 0)
        {
            embed.AddField("Skills", "No skills recorded.", inline: false);
        }
        else
        {
            embed.AddField(
                "Skills",
                skills.Count == 0
                    ? "No relevant skills recorded."
                    : string.Join(
                        "\n",
                        skills.Select(skill => $"`{skill.Id}`: **{skill.Level}**")),
                inline: false);
        }

        return embed.Build();
    }
}
