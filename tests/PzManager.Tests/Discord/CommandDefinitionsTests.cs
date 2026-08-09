using Discord;
using PzManager.Core.Services;
using PzManager.Discord;
using PzManager.Server;
using PzManager.Server.LogReader;
using PzManager.Server.Parsers;
using PzManager.Tests.Server.Support;

namespace PzManager.Tests.Discord;

/// <summary>
/// Discord rejects the *entire* command registration batch if a single
/// command/option name or description is out of bounds - this has broken
/// production startup twice already (a >100-char option description threw
/// during BuildCommands(), taking down the whole bot before it could
/// register anything). These limits are cheap to check up front instead of
/// finding out at deploy time.
/// </summary>
public sealed class CommandDefinitionsTests
{
    private const int MaxNameLength = 32;
    private const int MaxDescriptionLength = 100;

    [Fact]
    public void AdminCommands_AllNamesAndDescriptions_FitWithinDiscordLimits()
    {
        AssertWithinDiscordLimits(CreateAdminCommands().BuildCommands());
    }

    [Fact]
    public void PlayerCommands_AllNamesAndDescriptions_FitWithinDiscordLimits()
    {
        AssertWithinDiscordLimits(new PlayerCommands(new PlayerService()).BuildCommands());
    }

    private static void AssertWithinDiscordLimits(IEnumerable<SlashCommandBuilder> commands)
    {
        foreach (var command in commands)
        {
            Assert.True(
                command.Name!.Length <= MaxNameLength,
                $"/{command.Name}: name is {command.Name.Length} chars (max {MaxNameLength}).");

            Assert.True(
                command.Description!.Length <= MaxDescriptionLength,
                $"/{command.Name}: description is {command.Description.Length} chars (max {MaxDescriptionLength}): \"{command.Description}\"");

            foreach (var option in command.Options ?? [])
            {
                Assert.True(
                    option.Name!.Length <= MaxNameLength,
                    $"/{command.Name} option '{option.Name}': name is {option.Name.Length} chars (max {MaxNameLength}).");

                Assert.True(
                    option.Description!.Length <= MaxDescriptionLength,
                    $"/{command.Name} option '{option.Name}': description is {option.Description.Length} chars "
                    + $"(max {MaxDescriptionLength}): \"{option.Description}\"");
            }
        }
    }

    private static AdminCommands CreateAdminCommands()
    {
        var settings = new PzServerSettings("TestServer", new AdminCredentials("admin", "password"));
        var process = new FakeServerProcess();

        var serverManager = new ServerManager(
            new PlayerService(),
            new FakeEventSink(),
            new PerkLogLocator(),
            new PerkLogReader(),
            new SkillLogParser(),
            process,
            settings);

        return new AdminCommands(
            serverManager, new ServerConfigProvisioner(), new ServerUpdater(), process,
            settings, adminChannelId: 1);
    }
}
