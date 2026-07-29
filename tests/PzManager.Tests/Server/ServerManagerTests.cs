using PzManager.Core.Services;
using PzManager.Server;
using PzManager.Server.LogReader;
using PzManager.Server.Parsers;
using PzManager.Tests.Server.Support;

namespace PzManager.Tests.Server;

public sealed class ServerManagerTests
{
    [Fact]
    public async Task RunAsync_ReachesRunningState_ThenStopAsync_EndsGracefullyWithoutRestart()
    {
        var (manager, process, root) = CreateManager();

        try
        {
            var runTask = manager.RunAsync();

            await WaitForStateAsync(manager, ServerConnectionState.Running);

            Assert.NotNull(manager.State.PerkLogPath);

            await manager.StopAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(ServerConnectionState.Offline, manager.State.ConnectionState);
            Assert.Equal(1, process.StartCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_OnCrash_AutomaticallyRestartsAfterBackoff()
    {
        var (manager, process, root) = CreateManager(
            minCrashBackoff: TimeSpan.FromMilliseconds(20),
            maxCrashBackoff: TimeSpan.FromMilliseconds(20));

        try
        {
            var runTask = manager.RunAsync();

            await WaitForStateAsync(manager, ServerConnectionState.Running);

            process.SimulateCrash();

            await WaitForStateAsync(manager, ServerConnectionState.Crashed);
            await WaitForStateAsync(manager, ServerConnectionState.Running);

            Assert.Equal(2, process.StartCount);

            await manager.StopAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_FailedStart_IsTreatedAsCrashAndRetried()
    {
        var (manager, process, root) = CreateManager(
            minCrashBackoff: TimeSpan.FromMilliseconds(20),
            maxCrashBackoff: TimeSpan.FromMilliseconds(20));

        process.FailNextStarts = 1;

        try
        {
            var runTask = manager.RunAsync();

            await WaitForStateAsync(manager, ServerConnectionState.Crashed);
            await WaitForStateAsync(manager, ServerConnectionState.Running);

            Assert.Equal(2, process.StartCount);

            await manager.StopAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RestartAsync_GoesThroughRestartingState_WithoutBeingTreatedAsCrash()
    {
        var (manager, process, root) = CreateManager();

        try
        {
            var runTask = manager.RunAsync();

            await WaitForStateAsync(manager, ServerConnectionState.Running);

            var statesSeen = new List<ServerConnectionState>();
            manager.StateChanged += (_, state) => statesSeen.Add(state.ConnectionState);

            await manager.RestartAsync();

            await WaitForStateAsync(manager, ServerConnectionState.Running);

            Assert.Equal(2, process.StartCount);
            Assert.Contains(ServerConnectionState.Restarting, statesSeen);
            Assert.DoesNotContain(ServerConnectionState.Crashed, statesSeen);

            await manager.StopAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_SendsSaveCommand()
    {
        var (manager, process, root) = CreateManager();

        try
        {
            await manager.SaveAsync();

            Assert.Contains("save", process.Commands);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task BroadcastAsync_SendsSanitizedServerMsgCommand()
    {
        var (manager, process, root) = CreateManager();

        try
        {
            await manager.BroadcastAsync("Hello \"world\"\nsecond line");

            Assert.Equal(
                "servermsg \"Hello 'world' second line\"",
                Assert.Single(process.Commands));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task KickAsync_SendsSanitizedKickUserCommand()
    {
        var (manager, process, root) = CreateManager();

        try
        {
            await manager.KickAsync("bad\"user\r\n");

            Assert.Equal(
                "kickuser \"bad'user  \"",
                Assert.Single(process.Commands));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (ServerManager manager, FakeServerProcess process, string root) CreateManager(
        TimeSpan? minCrashBackoff = null,
        TimeSpan? maxCrashBackoff = null,
        TimeSpan? crashBackoffResetThreshold = null)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        File.WriteAllText(Path.Combine(root, "PerkLog.txt"), string.Empty);

        var process = new FakeServerProcess();

        var settings = new PzServerSettings(
            "TestServer",
            new AdminCredentials("admin", "password"),
            configDirectory: root,
            perkLogDirectory: root);

        var manager = new ServerManager(
            new PlayerService(),
            new FakeEventSink(),
            new PerkLogLocator(),
            new PerkLogReader(),
            new SkillLogParser(),
            process,
            settings,
            minCrashBackoff ?? TimeSpan.FromMilliseconds(20),
            maxCrashBackoff ?? TimeSpan.FromMilliseconds(20),
            crashBackoffResetThreshold ?? TimeSpan.FromMinutes(5));

        return (manager, process, root);
    }

    private static async Task WaitForStateAsync(
        ServerManager manager,
        ServerConnectionState state,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));

        while (manager.State.ConnectionState != state)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException(
                    $"Timed out waiting for state {state}. Last state: {manager.State.ConnectionState}");

            await Task.Delay(10);
        }
    }
}
