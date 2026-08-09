using System.Text.Json;
using PzManager.Server;
using PzManager.Tests.Server.Support;

namespace PzManager.Tests.Server;

public sealed class ServerConfigProvisionerTests
{
    [Fact]
    public async Task BootstrapIfNewAsync_WhenConfigAlreadyExists_ReturnsFalseWithoutStartingProcess()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false");

            var process = new FakeServerProcess();
            var settings = CreateSettings(root);
            var provisioner = new ServerConfigProvisioner();

            var result = await provisioner.BootstrapIfNewAsync(process, settings);

            Assert.False(result);
            Assert.Equal(0, process.StartCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task BootstrapIfNewAsync_WhenServerStartsCorrectly_StopsItAndReturnsTrue()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            var iniPath = Path.Combine(serverDir, "TestServer.ini");

            var process = new FakeServerProcess
            {
                // Simulates the real PZ process writing its default config
                // bundle as part of booting up to "*** SERVER STARTED ***".
                OnStart = () =>
                {
                    Directory.CreateDirectory(serverDir);
                    File.WriteAllText(iniPath, "PVP=false");
                }
            };

            var settings = CreateSettings(root);
            var provisioner = new ServerConfigProvisioner();

            var result = await provisioner.BootstrapIfNewAsync(process, settings);

            Assert.True(result);
            Assert.Equal(1, process.StartCount);
            Assert.False(process.IsRunning);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task BootstrapIfNewAsync_WhenServerFailsToStart_StopsProcessAndThrows()
    {
        var root = CreateTempDir();

        try
        {
            var process = new FakeServerProcess { FailNextStarts = 1 };
            var settings = CreateSettings(root);
            var provisioner = new ServerConfigProvisioner();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provisioner.BootstrapIfNewAsync(process, settings));

            Assert.Equal(1, process.StartCount);
            Assert.False(process.IsRunning);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task BootstrapIfNewAsync_WhenServerStartsButNeverWritesIni_Throws()
    {
        var root = CreateTempDir();

        try
        {
            var process = new FakeServerProcess();
            var settings = CreateSettings(root);
            var provisioner = new ServerConfigProvisioner();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provisioner.BootstrapIfNewAsync(process, settings));

            Assert.Equal(1, process.StartCount);
            Assert.False(process.IsRunning);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetConfigValue_WithPlainKey_PatchesTheIniFile()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false\nMaxPlayers=32");

            var settings = CreateSettings(root);

            new ServerConfigProvisioner().SetConfigValue(settings, PzConfigFile.Ini, "MaxPlayers", "8");

            var ini = File.ReadAllLines(Path.Combine(serverDir, "TestServer.ini"));
            Assert.Contains("MaxPlayers=8", ini);
            Assert.Contains("PVP=false", ini);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetConfigValue_WithDottedKey_PatchesTheSandboxVarsFile()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"),
            [
                "SandboxVars = {",
                "    Zombies = 5,",
                "}"
            ]);

            var settings = CreateSettings(root);

            new ServerConfigProvisioner().SetConfigValue(settings, PzConfigFile.SandboxVars, "SandboxVars.Zombies", "3");

            var lua = File.ReadAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"));
            Assert.Contains("    Zombies = 3,", lua);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetConfigValue_WithExplicitSandboxVarsFile_AcceptsBareNameWithoutPrefix()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"),
            [
                "SandboxVars = {",
                "    Zombies = 5,",
                "}"
            ]);

            var settings = CreateSettings(root);

            // File is already explicit, so "Zombies" (no "SandboxVars."
            // prefix) should work exactly like "SandboxVars.Zombies".
            new ServerConfigProvisioner().SetConfigValue(settings, PzConfigFile.SandboxVars, "Zombies", "3");

            var lua = File.ReadAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"));
            Assert.Contains("    Zombies = 3,", lua);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetConfigValue_WithExplicitSandboxVarsFile_AcceptsBareNameWithoutPrefix()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"),
            [
                "SandboxVars = {",
                "    Zombies = 5,",
                "}"
            ]);

            var settings = CreateSettings(root);

            var value = new ServerConfigProvisioner().GetConfigValue(settings, PzConfigFile.SandboxVars, "Zombies");

            Assert.Equal("5", value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetConfigValue_WhenTargetFileDoesNotExist_Throws()
    {
        var root = CreateTempDir();

        try
        {
            var settings = CreateSettings(root);

            Assert.Throws<InvalidOperationException>(() =>
                new ServerConfigProvisioner().SetConfigValue(settings, PzConfigFile.Ini, "PVP", "true"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetConfigValue_WithUnknownKey_Throws()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false");

            var settings = CreateSettings(root);

            Assert.Throws<InvalidOperationException>(() =>
                new ServerConfigProvisioner().SetConfigValue(settings, PzConfigFile.Ini, "DoesNotExist", "true"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetConfigValue_WithPlainKey_ReadsFromTheIniFile()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false\nMaxPlayers=32");

            var settings = CreateSettings(root);

            var value = new ServerConfigProvisioner().GetConfigValue(settings, PzConfigFile.Ini, "MaxPlayers");

            Assert.Equal("32", value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetConfigValue_WithUnknownKey_Throws()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false");

            var settings = CreateSettings(root);

            Assert.Throws<InvalidOperationException>(() =>
                new ServerConfigProvisioner().GetConfigValue(settings, PzConfigFile.Ini, "DoesNotExist"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetConfigValue_WhenTargetFileDoesNotExist_Throws()
    {
        var root = CreateTempDir();

        try
        {
            var settings = CreateSettings(root);

            Assert.Throws<InvalidOperationException>(() =>
                new ServerConfigProvisioner().GetConfigValue(settings, PzConfigFile.Ini, "PVP"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetConfigFileContent_ReturnsTheRawFileContents()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false\nMaxPlayers=32");

            var settings = CreateSettings(root);

            var content = new ServerConfigProvisioner().GetConfigFileContent(settings, PzConfigFile.Ini);

            Assert.Equal("PVP=false\nMaxPlayers=32", content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // Steam ships ProjectZomboid64.json with its own CRLF/tab formatting -
    // reproduced here verbatim (rather than reading whatever the machine's
    // real file currently contains) so these tests are hardcoded and
    // reproducible regardless of what SteamCMD last wrote or what a
    // previous EnsureMaxMemory call left behind.
    private const string SampleProjectZomboid64Json =
        "{\r\n" +
        "\t\"mainClass\": \"zombie/network/GameServer\",\r\n" +
        "\t\"classpath\": [\r\n" +
        "\t\t\"java/.\",\r\n" +
        "\t\t\"java/projectzomboid.jar\"\r\n" +
        "\t],\r\n" +
        "\t\"vmArgs\": [\r\n" +
        "\t\t\"-Djava.awt.headless=true\",\r\n" +
        "\t\t\"-Xmx8g\",\r\n" +
        "\t\t\"-Dzomboid.steam=1\",\r\n" +
        "\t\t\"-XX:+UseZGC\"\r\n" +
        "\t]\r\n" +
        "}\r\n";

    [Fact]
    public void EnsureMaxMemory_ReplacesExistingXmxArgAndLeavesRestUntouched()
    {
        var path = ResolveProjectZomboid64JsonPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var original = File.Exists(path) ? File.ReadAllText(path) : null;

        try
        {
            File.WriteAllText(path, SampleProjectZomboid64Json);

            var settings = CreateSettings("/unused", maxMemory: "3g");

            new ServerConfigProvisioner().EnsureMaxMemory(settings);

            var json = File.ReadAllText(path);
            Assert.Contains("\"-Xmx3g\"", json);
            Assert.DoesNotContain("-Xmx8g", json);
            Assert.Contains("\"mainClass\": \"zombie/network/GameServer\"", json);
            Assert.Contains("\"-XX:+UseZGC\"", json);
        }
        finally
        {
            RestoreOrDelete(path, original);
        }
    }

    [Fact]
    public void EnsureMaxMemory_WhenAlreadyCorrect_LeavesFileByteForByteUntouched()
    {
        var path = ResolveProjectZomboid64JsonPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var original = File.Exists(path) ? File.ReadAllText(path) : null;

        try
        {
            var alreadyCorrect = SampleProjectZomboid64Json.Replace("-Xmx8g", "-Xmx5g");
            File.WriteAllText(path, alreadyCorrect);

            new ServerConfigProvisioner().EnsureMaxMemory(CreateSettings("/unused", maxMemory: "5g"));

            Assert.Equal(alreadyCorrect, File.ReadAllText(path));
        }
        finally
        {
            RestoreOrDelete(path, original);
        }
    }

    private static void RestoreOrDelete(string path, string? original)
    {
        if (original is null)
            File.Delete(path);
        else
            File.WriteAllText(path, original);
    }

    // Resolves the deterministic on-disk path without requiring the file to
    // already exist - a fresh checkout has no Data/ at all (it's
    // gitignored, only ever populated by actually running the game), so
    // these tests must be able to create it themselves rather than assuming
    // some prior local run left it behind.
    private static string ResolveProjectZomboid64JsonPath() =>
        ResolveServerProjectPath("Data", "pz-server", "ProjectZomboid64.json");

    // Builds a minimal .ini containing a "Key=placeholder" line for every
    // key in the real, checked-in Overrides/ZomboidServer.overrides.json,
    // plus Password (which SetPredefinedConfig merges in dynamically from
    // PzServerSettings, not from that file) - SetPredefinedConfig always
    // reads overrides from the real Overrides/ file (there's no way to
    // inject a fake source), and IniFilePatcher throws if any override key
    // isn't already a line in the destination file. Deriving this from the
    // tracked overrides file (instead of copying the gitignored,
    // machine-local Data/Zomboid/Server/ZomboidServer.ini a real game
    // install writes) keeps these tests working on a fresh checkout with no
    // prior local run.
    private static string BuildIniFixtureFromRealOverrides()
    {
        var overridesPath = ResolveServerProjectPath("Overrides", "ZomboidServer.overrides.json");

        var overrides = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(overridesPath))
            ?? [];

        var lines = overrides.Keys.Append("Password").Select(key => $"{key}=placeholder");

        return string.Join(Environment.NewLine, lines);
    }

    private static string ResolveServerProjectPath(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var serverProject = Path.Combine(directory.FullName, "src", "PzManager.Server");

            if (Directory.Exists(serverProject) && File.Exists(Path.Combine(directory.FullName, "PzManager.sln")))
                return Path.Combine([serverProject, .. relativeSegments]);

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the PzManager.Server project directory from the test run directory.");
    }

    [Fact]
    public void SetPredefinedConfig_WithSettingsPassword_WritesItToTheIniFile()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);

            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), BuildIniFixtureFromRealOverrides());

            var settings = new PzServerSettings(
                "TestServer",
                new AdminCredentials("admin", "adminpassword"),
                configDirectory: root,
                password: "NewJoinPassword");

            new ServerConfigProvisioner().SetPredefinedConfig(settings);

            var ini = File.ReadAllLines(Path.Combine(serverDir, "TestServer.ini"));
            Assert.Contains("Password=NewJoinPassword", ini);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetPredefinedConfig_WithFileRestriction_LeavesTheOtherFileUntouched()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);

            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), BuildIniFixtureFromRealOverrides());

            // Content doesn't matter - PzConfigFile.Ini below means
            // SetPredefinedConfig should never even open this file, let
            // alone need any particular key in it.
            var luaPath = Path.Combine(serverDir, "TestServer_SandboxVars.lua");
            var originalLua = "-- placeholder, untouched by an Ini-restricted SetPredefinedConfig call";
            File.WriteAllText(luaPath, originalLua);

            var settings = new PzServerSettings(
                "TestServer",
                new AdminCredentials("admin", "adminpassword"),
                configDirectory: root,
                password: "NewJoinPassword");

            new ServerConfigProvisioner().SetPredefinedConfig(settings, PzConfigFile.Ini);

            var ini = File.ReadAllLines(Path.Combine(serverDir, "TestServer.ini"));
            Assert.Contains("Password=NewJoinPassword", ini);
            Assert.Equal(originalLua, File.ReadAllText(luaPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetConfigValue_WithoutFile_FindsFieldInWhicheverFileHasIt()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false");
            File.WriteAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"),
            [
                "SandboxVars = {",
                "    Zombies = 5,",
                "}"
            ]);

            var settings = CreateSettings(root);
            var provisioner = new ServerConfigProvisioner();

            provisioner.SetConfigValue(settings, "PVP", "true");
            provisioner.SetConfigValue(settings, "SandboxVars.Zombies", "1");

            var ini = File.ReadAllLines(Path.Combine(serverDir, "TestServer.ini"));
            var lua = File.ReadAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"));
            Assert.Contains("PVP=true", ini);
            Assert.Contains("    Zombies = 1,", lua);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetConfigValue_WithoutFile_WhenFieldIsInNeitherFile_Throws()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false");
            File.WriteAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"), ["SandboxVars = {", "}"]);

            var settings = CreateSettings(root);

            Assert.Throws<InvalidOperationException>(() =>
                new ServerConfigProvisioner().SetConfigValue(settings, "DoesNotExist", "true"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetConfigValue_WithoutFile_FindsFieldInWhicheverFileHasIt()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false");
            File.WriteAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"),
            [
                "SandboxVars = {",
                "    Zombies = 5,",
                "}"
            ]);

            var settings = CreateSettings(root);
            var provisioner = new ServerConfigProvisioner();

            Assert.Equal("false", provisioner.GetConfigValue(settings, "PVP"));
            Assert.Equal("5", provisioner.GetConfigValue(settings, "SandboxVars.Zombies"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetConfigValue_WithoutFile_WhenFieldIsInNeitherFile_Throws()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestServer.ini"), "PVP=false");
            File.WriteAllLines(Path.Combine(serverDir, "TestServer_SandboxVars.lua"), ["SandboxVars = {", "}"]);

            var settings = CreateSettings(root);

            Assert.Throws<InvalidOperationException>(() =>
                new ServerConfigProvisioner().GetConfigValue(settings, "DoesNotExist"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static PzServerSettings CreateSettings(string root, string? maxMemory = null)
    {
        return new PzServerSettings(
            "TestServer",
            new AdminCredentials("admin", "password"),
            configDirectory: root,
            maxMemory: maxMemory);
    }
}
