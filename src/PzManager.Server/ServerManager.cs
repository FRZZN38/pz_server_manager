using PzManager.Core.Domain;
using PzManager.Core.Logger;
using PzManager.Core.Services;
using PzManager.Server.LogReader;
using PzManager.Server.Parsers;

namespace PzManager.Server;

public sealed class AdminCredentials
{
    public string Username { get; }

    public string Password { get; }

    public AdminCredentials(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        Username = username;
        Password = password;
    }
}

public sealed class PzServerSettings
{
    public PzServerSettings(
        string serverName,
        AdminCredentials admin,
        IEnumerable<string>? startArguments = null,
        string? configDirectory = null,
        string? perkLogDirectory = null,
        string? password = null,
        string? publicName = null,
        string? maxMemory = null)
    {
        ServerName = serverName;
        Admin = admin;
        StartArguments = startArguments?.ToArray() ?? [];
        Password = string.IsNullOrWhiteSpace(password) ? null : password;
        PublicName = string.IsNullOrWhiteSpace(publicName) ? null : publicName;
        MaxMemory = string.IsNullOrWhiteSpace(maxMemory) ? "5g" : maxMemory;

        ConfigDirectory = string.IsNullOrWhiteSpace(configDirectory)
            ? Path.Combine(ServerPaths.DataDirectory, "Zomboid")
            : configDirectory;

        PerkLogDirectory = string.IsNullOrWhiteSpace(perkLogDirectory)
            ? Path.Combine(ConfigDirectory, "Logs")
            : perkLogDirectory;
    }

    public string ServerName { get; }

    public string ConfigDirectory { get; }

    public string PerkLogDirectory { get; }

    public AdminCredentials Admin { get; }

    public IReadOnlyList<string> StartArguments { get; }

    public string? Password { get; }

    /// <summary>
    /// Name shown in the in-game/Steam server browser (ZomboidServer.ini's
    /// PublicName). Per-deployment like ServerName, so it lives in
    /// appsettings.json rather than the checked-in Overrides/*.json - handy
    /// for telling a test run apart from the real server without touching
    /// tracked files. Null/blank means "leave whatever is already there".
    /// </summary>
    public string? PublicName { get; }

    /// <summary>
    /// JVM -Xmx value enforced on ProjectZomboid64.json (e.g. "5g") to cap
    /// server memory and avoid OOM. Defaults to "5g". Must be reapplied
    /// before every launch, not just once, because ProjectZomboid64.json is
    /// part of the SteamCMD depot and "app_update ... validate" (install or
    /// update scripts) can silently reset it to whatever the game ships.
    /// </summary>
    public string MaxMemory { get; }
}

public sealed class ServerManager
{
    private static readonly TimeSpan DefaultMinCrashBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultMaxCrashBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultCrashBackoffResetThreshold = TimeSpan.FromMinutes(2);

    private readonly PlayerService _playerService;
    private readonly IDomainEventSink _eventSink;
    private readonly PerkLogLocator _perkLogLocator;
    private readonly PerkLogReader _reader;
    private readonly SkillLogParser _parser;
    private readonly PzServerSettings _settings;
    private readonly IServerProcess _process;
    private readonly TimeSpan _minCrashBackoff;
    private readonly TimeSpan _maxCrashBackoff;
    private readonly TimeSpan _crashBackoffResetThreshold;

    private volatile bool _stopRequested;
    private int _crashCount;
    private DateTime _currentAttemptStartedAt;

    private readonly object _sessionLock = new();
    private Task? _sessionTask;

    private ServerState _state;

    public ServerState State => _state;

    /// <summary>
    /// Fires only when ConnectionState itself changes (Starting/Running/
    /// Stopping/Offline/...), not on every minor field update (player
    /// events update LastEventAt/KnownPlayerCount far more often than the
    /// connection state actually changes). Awaited by <see cref="UpdateStateAsync"/>
    /// rather than fire-and-forget, so callers like Program.cs's shutdown
    /// sequence can rely on the final Offline notification having actually
    /// been sent (e.g. to Discord) before disconnecting.
    /// </summary>
    public event Func<ServerState, Task>? StateChanged;

    public ServerManager(
        PlayerService playerService,
        IDomainEventSink eventSink,
        PerkLogLocator perkLogLocator,
        PerkLogReader reader,
        SkillLogParser parser,
        IServerProcess process,
        PzServerSettings settings,
        TimeSpan? minCrashBackoff = null,
        TimeSpan? maxCrashBackoff = null,
        TimeSpan? crashBackoffResetThreshold = null)
    {
        ArgumentNullException.ThrowIfNull(playerService);
        ArgumentNullException.ThrowIfNull(eventSink);
        ArgumentNullException.ThrowIfNull(perkLogLocator);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(settings);

        _playerService = playerService;
        _eventSink = eventSink;
        _perkLogLocator = perkLogLocator;
        _reader = reader;
        _parser = parser;
        _process = process;
        _settings = settings;
        _minCrashBackoff = minCrashBackoff ?? DefaultMinCrashBackoff;
        _maxCrashBackoff = maxCrashBackoff ?? DefaultMaxCrashBackoff;
        _crashBackoffResetThreshold = crashBackoffResetThreshold ?? DefaultCrashBackoffResetThreshold;

        _state = new ServerState(
            settings.ServerName,
            settings.ConfigDirectory,
            settings.PerkLogDirectory,
            null,
            ServerConnectionState.Offline,
            null,
            null,
            0);
    }

    public async Task PrepareAsync()
    {
        Log.Info($"[SERVER MANAGER] Server name: {_settings.ServerName}");
        Log.Info($"[SERVER MANAGER] Config directory: {_settings.ConfigDirectory}");
        Log.Info($"[SERVER MANAGER] Perk log directory: {_settings.PerkLogDirectory}");

        await UpdateStateAsync(s => s with
        {
            ConnectionState = ServerConnectionState.Offline
        });
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await RunOnceAsync(cancellationToken);

                if (_stopRequested)
                {
                    _stopRequested = false;
                    break;
                }

                if (DateTime.UtcNow - _currentAttemptStartedAt >= _crashBackoffResetThreshold)
                    _crashCount = 0;

                _crashCount++;

                var delay = ComputeCrashBackoff(_crashCount);

                await UpdateStateAsync(s => s with
                {
                    ConnectionState = ServerConnectionState.Crashed
                });

                Log.Warn(
                    $"[SERVER MANAGER] Server exited unexpectedly. Restarting in {delay.TotalSeconds:0}s (attempt {_crashCount})...");

                await Task.Delay(delay, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down.
        }

        await UpdateStateAsync(s => s with
        {
            ConnectionState = ServerConnectionState.Offline
        });
    }

    public Task StartAsync()
    {
        lock (_sessionLock)
        {
            if (_sessionTask is { IsCompleted: false })
            {
                Log.Info("[SERVER MANAGER] Start requested but a session is already running.");
                return Task.CompletedTask;
            }

            Log.Info("[SERVER MANAGER] Start requested.");

            // Every explicit start (initial launch, or after a restart's
            // stop-then-start) is a deliberate fresh attempt, not a
            // continuation of whatever crash-backoff streak came before.
            _crashCount = 0;
            _sessionTask = RunAsync();
        }

        return Task.CompletedTask;
    }

    public Task WaitUntilStoppedAsync()
    {
        Task? sessionTask;

        lock (_sessionLock)
        {
            sessionTask = _sessionTask;
        }

        return sessionTask ?? Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Log.Info("[SERVER MANAGER] Stop requested.");

        _stopRequested = true;

        await UpdateStateAsync(s => s with
        {
            ConnectionState = ServerConnectionState.Stopping
        });

        await _process.Stop();
    }

    /// <summary>
    /// Literally stop, then start again - no separate "Restarting" state.
    /// The connection state honestly goes through Stopping -> Offline ->
    /// Starting -> Running, reusing the same already-tested paths as the
    /// standalone /stop and /start commands instead of a parallel "restart
    /// mode" with its own flag and looping logic.
    /// </summary>
    public async Task RestartAsync()
    {
        Log.Info("[SERVER MANAGER] Restart requested.");

        await StopAsync();
        await WaitUntilStoppedAsync();
        await StartAsync();
    }

    public Task SaveAsync()
    {
        return _process.SendCommand("save");
    }

    public Task BroadcastAsync(string message)
    {
        return _process.SendCommand($"servermsg \"{SanitizeCommandArgument(message)}\"");
    }

    public Task KickAsync(string username)
    {
        return _process.SendCommand($"kickuser \"{SanitizeCommandArgument(username)}\"");
    }

    private async Task RunOnceAsync(
        CancellationToken cancellationToken)
    {
        await UpdateStateAsync(s => s with
        {
            ConnectionState = ServerConnectionState.Starting
        });

        Log.Info("[SERVER MANAGER] Starting Project Zomboid server...");

        _currentAttemptStartedAt = DateTime.UtcNow;

        using var exitedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        void OnExited(int exitCode) => exitedCts.Cancel();

        _process.Exited += OnExited;

        try
        {
            await _process.Start();

            await _process.WaitUntilStarted(exitedCts.Token);

            Log.Info("[SERVER MANAGER] Project Zomboid server started.");

            // The PZ process itself reporting "*** SERVER STARTED ***" is
            // the real signal that it's up and accepting connections - mark
            // Running here. PerkLog.txt is written by connected player
            // clients (see ISPerkLog.lua), not by the server on boot, so if
            // nobody has logged in yet it may not exist for a while (or at
            // all). Waiting for it before reporting Running left the status
            // stuck on "Starting" forever on an empty server.
            await UpdateStateAsync(s => s with
            {
                ConnectionState = ServerConnectionState.Running
            });

            var perkLogPath = await WaitForPerkLogAsync(exitedCts.Token);

            Log.Info($"[SERVER MANAGER] PerkLog found: {perkLogPath}");

            await UpdateStateAsync(s => s with
            {
                PerkLogPath = perkLogPath
            });

            Log.Info($"[SERVER MANAGER] Watching perk log: {perkLogPath}");

            using var stream = new FileStream(
                perkLogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            await foreach (var entry in _reader.Read(stream, exitedCts.Token)
                .WithCancellation(exitedCts.Token))
            {
                var evt = _parser.Parse(entry);

                if (evt is null)
                    continue;

                try
                {
                    _playerService.Handle(evt);

                    await UpdateStateAsync(s => s with
                    {
                        LastEventAt = evt.Timestamp,
                        LastEventType = evt.GetType().Name,
                        KnownPlayerCount = _playerService.GetPlayers().Count
                    });

                    await _eventSink.Handle(evt);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // The process exited (stop, restart or crash) while we were waiting on it.
        }
        catch (Exception ex)
        {
            Log.Error($"[SERVER MANAGER] Server run failed: {ex}");
        }
        finally
        {
            _process.Exited -= OnExited;
        }
    }

    private async Task<string> WaitForPerkLogAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return _perkLogLocator.Locate(
                    _settings.PerkLogDirectory,
                    _process.StartedAt);
            }
            catch (FileNotFoundException)
            {
                Log.Info(
                    "[SERVER MANAGER] Waiting for PerkLog to be created...");

                await Task.Delay(
                    TimeSpan.FromMilliseconds(500),
                    cancellationToken);
            }
        }
    }

    private TimeSpan ComputeCrashBackoff(int attempt)
    {
        var seconds = Math.Min(
            _minCrashBackoff.TotalSeconds * Math.Pow(2, attempt - 1),
            _maxCrashBackoff.TotalSeconds);

        return TimeSpan.FromSeconds(seconds);
    }

    private static string SanitizeCommandArgument(string value)
    {
        return value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\"", "'");
    }

    private async Task UpdateStateAsync(
        Func<ServerState, ServerState> update)
    {
        var previousConnectionState = _state.ConnectionState;

        _state = update(_state);

        if (_state.ConnectionState == previousConnectionState)
            return;

        if (StateChanged is null)
            return;

        foreach (Func<ServerState, Task> handler in StateChanged.GetInvocationList())
            await handler(_state);
    }
}
