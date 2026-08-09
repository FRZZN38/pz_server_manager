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
            await WaitForPerkLogPathAsync(manager);

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
    public async Task StopAsync_AwaitsStateChangedSubscriberBeforeReturning()
    {
        var (manager, process, root) = CreateManager();

        try
        {
            var runTask = manager.RunAsync();

            await WaitForStateAsync(manager, ServerConnectionState.Running);

            var handlerCompleted = false;

            manager.StateChanged += async _ =>
            {
                await Task.Delay(50);
                handlerCompleted = true;
            };

            await manager.StopAsync();

            // Would have been false under the old fire-and-forget
            // (async void) event pattern, since StopAsync would have
            // returned before the subscriber's delay elapsed.
            Assert.True(handlerCompleted);

            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_WhenConnectionStateAlreadyOffline_DoesNotFireStateChanged()
    {
        var (manager, _, root) = CreateManager();

        try
        {
            var fireCount = 0;

            manager.StateChanged += _ =>
            {
                fireCount++;
                return Task.CompletedTask;
            };

            // Constructor already sets ConnectionState to Offline, so this
            // shouldn't fire the event again - only actual transitions should.
            await manager.PrepareAsync();

            Assert.Equal(0, fireCount);
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
    public async Task RestartAsync_GoesThroughStopThenStart_WithoutBeingTreatedAsCrash()
    {
        var (manager, process, root) = CreateManager();

        try
        {
            // Restart relies on the _sessionTask tracked by StartAsync (to
            // know when the old session has fully stopped before starting
            // a new one), so it must be started that way here too, not via
            // a raw RunAsync() call like the other tests in this file.
            await manager.StartAsync();

            await WaitForStateAsync(manager, ServerConnectionState.Running);

            var statesSeen = new List<ServerConnectionState>();
            manager.StateChanged += state =>
            {
                statesSeen.Add(state.ConnectionState);
                return Task.CompletedTask;
            };

            await manager.RestartAsync();
            await WaitForStateAsync(manager, ServerConnectionState.Running);

            Assert.Equal(2, process.StartCount);
            Assert.Equal(
                new[]
                {
                    ServerConnectionState.Stopping,
                    ServerConnectionState.Offline,
                    ServerConnectionState.Starting,
                    ServerConnectionState.Running
                },
                statesSeen);

            await manager.StopAsync();
            await manager.WaitUntilStoppedAsync();
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

    [Fact]
    public async Task RunAsync_ReachesRunningState_EvenWhenNoPerkLogFileExistsYet()
    {
        // PerkLog.txt is written by connected player clients, not by the
        // server itself, so on a freshly (re)started server with nobody
        // online yet, it may not exist for a while. Running must not depend
        // on it - otherwise status gets stuck on "Starting" forever.
        var (manager, process, root) = CreateManager(createPerkLog: false);

        try
        {
            var runTask = manager.RunAsync();

            await WaitForStateAsync(manager, ServerConnectionState.Running);

            Assert.Null(manager.State.PerkLogPath);

            await manager.StopAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (ServerManager manager, FakeServerProcess process, string root) CreateManager(
        TimeSpan? minCrashBackoff = null,
        TimeSpan? maxCrashBackoff = null,
        TimeSpan? crashBackoffResetThreshold = null,
        bool createPerkLog = true)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        if (createPerkLog)
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

    private static async Task WaitForPerkLogPathAsync(
        ServerManager manager,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));

        while (manager.State.PerkLogPath is null)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Timed out waiting for PerkLogPath to be set.");

            await Task.Delay(10);
        }
    }
}
