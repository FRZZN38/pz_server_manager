using Microsoft.Extensions.Configuration;
using PzManager.Core.Logger;
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
        ulong guildId,
        ulong publicChannelId,
        ulong adminChannelId)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Discord:Token is required.");

        if (guildId == 0)
            throw new InvalidOperationException("Discord:GuildId is required.");

        if (publicChannelId == 0)
            throw new InvalidOperationException("Discord:PublicChannelId is required.");

        if (adminChannelId == 0)
            throw new InvalidOperationException("Discord:AdminChannelId is required.");

        Token = token;
        GuildId = guildId;
        PublicChannelId = publicChannelId;
        AdminChannelId = adminChannelId;
    }

    public string Token { get; }

    public ulong GuildId { get; }

    public ulong PublicChannelId { get; }

    public ulong AdminChannelId { get; }
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var version = System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version;

        Log.Info($"[APP] PzManager starting up (v{version})...");

        Log.Info("[APP] Loading configuration...");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional:false)
            .AddEnvironmentVariables()
            .Build();

        Log.Info("[APP] Configuration loaded.");

        var discordSettings = new DiscordSettings(
            configuration["Discord:Token"]
                ?? throw new InvalidOperationException("Discord:Token is required."),
            configuration.GetValue<ulong>("Discord:GuildId"),
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
                .Get<string[]>() ?? [],
            configuration["ProjectZomboid:ConfigDirectory"],
            configuration["ProjectZomboid:PerkLogDirectory"],
            configuration["ProjectZomboid:Password"],
            configuration["ProjectZomboid:PublicName"],
            configuration["ProjectZomboid:MaxMemory"]);

        var playerService = new PlayerService();

        var playerCommands =
            new PlayerCommands(playerService);

        var bot =
            new DiscordBot(playerCommands);

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

        var configProvisioner = new ServerConfigProvisioner();

        playerCommands.AttachServerManager(serverManager);

        bot.AttachAdminCommands(
            new AdminCommands(serverManager, configProvisioner, projectZomboidSettings, discordSettings.AdminChannelId));

        Log.Info("[APP] Connecting to Discord...");

        await bot.Connect(
            discordSettings.Token,
            discordSettings.GuildId,
            discordSettings.PublicChannelId,
            discordSettings.AdminChannelId);

        Log.Info("[APP] Discord connected. Preparing server...");

        configProvisioner.EnsureMaxMemory(projectZomboidSettings);
        await configProvisioner.BootstrapIfNewAsync(process, projectZomboidSettings);
        configProvisioner.SetPredefinedConfig(projectZomboidSettings);

        await serverManager.PrepareAsync();

        serverManager.StateChanged += async state =>
        {
            await bot.SendAdmin(ServerStateFormatter.BuildChangeNotification(state));
        };

        var shutdownRequested = 0;

        var shutdownCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task RequestShutdownAsync()
        {
            if (Interlocked.Exchange(ref shutdownRequested, 1) != 0)
                return;

            Log.Info("[APP] Shutdown requested. Stopping Project Zomboid server...");

            await serverManager.StopAsync();
            await serverManager.WaitUntilStoppedAsync();

            await bot.Disconnect();

            shutdownCompleted.TrySetResult();
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _ = RequestShutdownAsync();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            RequestShutdownAsync().GetAwaiter().GetResult();
        };

        Log.Info("[APP] Startup complete. Starting Project Zomboid server...");

        await serverManager.StartAsync();

        await shutdownCompleted.Task;

        Log.Info("[APP] Shutdown complete.");
    }
}