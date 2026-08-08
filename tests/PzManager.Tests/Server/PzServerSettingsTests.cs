using PzManager.Server;

namespace PzManager.Tests.Server;

public sealed class PzServerSettingsTests
{
    [Fact]
    public void Constructor_WithoutOverrides_DefaultsDirectoriesUnderServerProjectData()
    {
        var settings = new PzServerSettings(
            "TestServer",
            new AdminCredentials("admin", "password"));

        Assert.EndsWith(
            Path.Combine("PzManager.Server", "Data", "Zomboid"),
            settings.ConfigDirectory);

        Assert.Equal(
            Path.Combine(settings.ConfigDirectory, "Logs"),
            settings.PerkLogDirectory);
    }

    [Fact]
    public void Constructor_WithConfigDirectoryOverride_DerivesPerkLogDirectoryFromIt()
    {
        var settings = new PzServerSettings(
            "TestServer",
            new AdminCredentials("admin", "password"),
            configDirectory: "/srv/pz-config");

        Assert.Equal("/srv/pz-config", settings.ConfigDirectory);
        Assert.Equal(Path.Combine("/srv/pz-config", "Logs"), settings.PerkLogDirectory);
    }

    [Fact]
    public void Constructor_WithPerkLogDirectoryOverride_UsesItInsteadOfDerivingFromConfigDirectory()
    {
        var settings = new PzServerSettings(
            "TestServer",
            new AdminCredentials("admin", "password"),
            configDirectory: "/srv/pz-config",
            perkLogDirectory: "/srv/pz-logs");

        Assert.Equal("/srv/pz-config", settings.ConfigDirectory);
        Assert.Equal("/srv/pz-logs", settings.PerkLogDirectory);
    }
}
