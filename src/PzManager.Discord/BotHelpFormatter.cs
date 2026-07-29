namespace PzManager.Discord;

public static class BotHelpFormatter
{
    public static string BuildHelpMessage()
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                "Available commands:",
                "`/player <username>` - Show a summary for a player.",
                "`/help` - Show this help message."
            });
    }
}
