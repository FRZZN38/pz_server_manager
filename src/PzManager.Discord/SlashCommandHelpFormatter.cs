using Discord;

namespace PzManager.Discord;

/// <summary>
/// Builds a help listing straight from the same SlashCommandBuilder list
/// used to register commands with Discord (see AdminCommands/PlayerCommands
/// BuildCommands()), so the help text can never drift out of sync with
/// what's actually available - unlike a hand-maintained string.
/// </summary>
public static class SlashCommandHelpFormatter
{
    public static string Build(string header, IEnumerable<SlashCommandBuilder> commands)
    {
        var lines = new List<string> { header };

        foreach (var command in commands)
        {
            var options = command.Options is { Count: > 0 }
                ? " " + string.Join(
                    " ",
                    command.Options.Select(o => o.IsRequired == true ? $"<{o.Name}>" : $"[{o.Name}]"))
                : "";

            lines.Add($"`/{command.Name}{options}` - {command.Description}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
