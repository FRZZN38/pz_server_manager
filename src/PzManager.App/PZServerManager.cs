using Microsoft.Extensions.Configuration;
using PzManager.Core.Logger;
using PzManager.Core.Services;
using PzManager.Discord;
using PzManager.Server.LogReader;
using PzManager.Server.Parsers;

namespace PzManager.App;

public sealed class AppSettings
{
    public DiscordSettings Discord { get; init; } = new();

    public ProjectZomboidSettings ProjectZomboid { get; init; } = new();
}

public sealed class DiscordSettings
{
    public string Token { get; init; } = string.Empty;

    public ulong ChannelId { get; init; }
}

public sealed class ProjectZomboidSettings
{
    public string PerkLogDirectory { get; init; } = string.Empty;
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var settings = configuration.Get<AppSettings>()!;

        var perkLogPath = Directory
            .GetFiles(
                settings.ProjectZomboid.PerkLogDirectory,
                "*PerkLog*")
            .Single();

        Log.Info($"Perk log path: {perkLogPath}");

        var playerService = new PlayerService();
        var bot = new DiscordBot(playerService);

        await bot.Connect(
            settings.Discord.Token,
            settings.Discord.ChannelId);

        var discord = new DiscordEventHandler(bot);

        var reader = new PerkLogReader();
        var parser = new SkillLogParser();

        using var stream = new FileStream(
            perkLogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        await foreach (var entry in reader.Read(stream))
        {
            var evt = parser.Parse(entry);

            if (evt is null)
            {
                Log.Warn("Ignored line.");
                continue;
            }

            try
            {
                Log.Info($"Parsed {evt.GetType().Name}");

                playerService.Handle(evt);
                await discord.Handle(evt);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }
    }
}
