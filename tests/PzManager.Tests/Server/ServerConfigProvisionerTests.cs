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

    [Fact]
    public void EnsureMaxMemory_ReplacesExistingXmxArgAndLeavesRestUntouched()
    {
        var path = ResolveProjectZomboid64JsonPath();
        var original = File.ReadAllText(path);

        try
        {
            var settings = CreateSettings("/unused", maxMemory: "3g");

            new ServerConfigProvisioner().EnsureMaxMemory(settings);

            var json = File.ReadAllText(path);
            Assert.Contains("\"-Xmx3g\"", json);
            Assert.Contains("\"mainClass\": \"zombie/network/GameServer\"", json);
            Assert.Contains("\"-XX:+UseZGC\"", json);
        }
        finally
        {
            File.WriteAllText(path, original);
        }
    }

    [Fact]
    public void EnsureMaxMemory_WhenAlreadyCorrect_LeavesFileByteForByteUntouched()
    {
        var path = ResolveProjectZomboid64JsonPath();
        var original = File.ReadAllText(path);

        try
        {
            new ServerConfigProvisioner().EnsureMaxMemory(CreateSettings("/unused", maxMemory: "5g"));

            Assert.Equal(original, File.ReadAllText(path));
        }
        finally
        {
            File.WriteAllText(path, original);
        }
    }

    private static string ResolveProjectZomboid64JsonPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "PzManager.Server", "Data", "pz-server", "ProjectZomboid64.json");

            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate ProjectZomboid64.json from the test run directory.");
    }

    [Fact]
    public void SetPredefinedConfig_WithSettingsPassword_WritesItToTheIniFile()
    {
        var root = CreateTempDir();

        try
        {
            var serverDir = Path.Combine(root, "Server");
            Directory.CreateDirectory(serverDir);

            File.Copy(
                "/home/frzzn38/workspace/pz_server_manager/src/PzManager.Server/Data/Zomboid/Server/ZomboidServer.ini",
                Path.Combine(serverDir, "TestServer.ini"));

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

            File.Copy(
                "/home/frzzn38/workspace/pz_server_manager/src/PzManager.Server/Data/Zomboid/Server/ZomboidServer.ini",
                Path.Combine(serverDir, "TestServer.ini"));

            var luaPath = Path.Combine(serverDir, "TestServer_SandboxVars.lua");
            File.Copy(
                "/home/frzzn38/workspace/pz_server_manager/src/PzManager.Server/Data/Zomboid/Server/ZomboidServer_SandboxVars.lua",
                luaPath);

            var originalLua = File.ReadAllText(luaPath);

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
