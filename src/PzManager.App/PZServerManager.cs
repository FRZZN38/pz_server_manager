using Microsoft.Extensions.Configuration;
using PzManager.Core.Services;
using PzManager.Discord;
using PzManager.Server;
using PzManager.Server.LogReader;
using PzManager.Server.Parsers;

namespace PzManager.App;

public sealed class DiscordSettings
{
    public DiscordSettings(
        string token,
        ulong publicChannelId,
        ulong adminChannelId)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Discord:Token is required.");

        if (publicChannelId == 0)
            throw new InvalidOperationException("Discord:PublicChannelId is required.");

        if (adminChannelId == 0)
            throw new InvalidOperationException("Discord:AdminChannelId is required.");

        Token = token;
        PublicChannelId = publicChannelId;
        AdminChannelId = adminChannelId;
    }

    public string Token { get; }

    public ulong PublicChannelId { get; }

    public ulong AdminChannelId { get; }
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional:false)
            .Build();

        var discordSettings = new DiscordSettings(
            configuration["Discord:Token"]
                ?? throw new InvalidOperationException("Discord:Token is required."),
            configuration.GetValue<ulong>("Discord:PublicChannelId"),
            configuration.GetValue<ulong>("Discord:AdminChannelId"));

        var adminCredentials = new AdminCredentials(
            configuration["ProjectZomboid:Admin:Username"]
                ?? throw new InvalidOperationException("ProjectZomboid:Admin:Username is required."),
            configuration["ProjectZomboid:Admin:Password"]
                ?? throw new InvalidOperationException("ProjectZomboid:Admin:Password is required."));

        var projectZomboidSettings = new PzServerSettings(
            configuration["ProjectZomboid:ServerName"]
                ?? throw new InvalidOperationException("ProjectZomboid:ServerName is required."),
            adminCredentials,
            configuration
                .GetSection("ProjectZomboid:StartArguments")
                .Get<string[]>() ?? []);

        var playerService = new PlayerService();

        var bot = new DiscordBot(playerService);

        await bot.Connect(
            discordSettings.Token,
            discordSettings.PublicChannelId,
            discordSettings.AdminChannelId);

        var discordEventHandler = new DiscordEventHandler(bot);

        var process = new ServerProcess(projectZomboidSettings);

        var serverManager = new ServerManager(
            playerService,
            discordEventHandler,
            new PerkLogLocator(),
            new PerkLogReader(),
            new SkillLogParser(),
            process,
            projectZomboidSettings);

        await serverManager.PrepareAsync();

        serverManager.StateChanged += async (_, state) =>
        {
            await bot.SendAdmin(ServerStateFormatter.Build(state));
        };

        await bot.SendAdmin(ServerStateFormatter.Build(serverManager.State));

        await serverManager.RunAsync();
    }
}