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
        IEnumerable<string>? startArguments = null)
    {
        ServerName = serverName;
        Admin = admin;
        StartArguments = startArguments?.ToArray() ?? [];

        ConfigDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Zomboid");

        PerkLogDirectory = Path.Combine(
            ConfigDirectory,
            "Logs");
    }

    public string ServerName { get; }

    public string ConfigDirectory { get; }

    public string PerkLogDirectory { get; }

    public AdminCredentials Admin { get; }

    public IReadOnlyList<string> StartArguments { get; }
}

public sealed class ServerManager
{
    private readonly PlayerService _playerService;
    private readonly IDomainEventSink _eventSink;
    private readonly PerkLogLocator _perkLogLocator;
    private readonly PerkLogReader _reader;
    private readonly SkillLogParser _parser;
    private readonly PzServerSettings _settings;
    private readonly ServerProcess _process;

    private ServerState _state;

    public ServerState State => _state;

    public event EventHandler<ServerState>? StateChanged;

    public ServerManager(
        PlayerService playerService,
        IDomainEventSink eventSink,
        PerkLogLocator perkLogLocator,
        PerkLogReader reader,
        SkillLogParser parser,
        ServerProcess process,
        PzServerSettings settings)
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

    public Task PrepareAsync()
    {
        Log.Info($"[SERVER MANAGER] Server name: {_settings.ServerName}");
        Log.Info($"[SERVER MANAGER] Config directory: {_settings.ConfigDirectory}");
        Log.Info($"[SERVER MANAGER] Perk log directory: {_settings.PerkLogDirectory}");

        UpdateState(s => s with
        {
            ConnectionState = ServerConnectionState.Offline
        });

        return Task.CompletedTask;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        UpdateState(s => s with
        {
            ConnectionState = ServerConnectionState.Starting
        });

        Log.Info("[SERVER MANAGER] Starting Project Zomboid server...");

        await _process.Start();

        await _process.WaitUntilStarted(cancellationToken);

        Log.Info("[SERVER MANAGER] Project Zomboid server started.");

        var perkLogPath = await WaitForPerkLogAsync(
            cancellationToken);

        Log.Info($"[SERVER MANAGER] PerkLog found: {perkLogPath}");

        UpdateState(s => s with
        {
            ConnectionState = ServerConnectionState.Running,
            PerkLogPath = perkLogPath
        });

        Log.Info($"[SERVER MANAGER] Watching perk log: {perkLogPath}");

        using var stream = new FileStream(
            perkLogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        await foreach (var entry in _reader.Read(stream)
            .WithCancellation(cancellationToken))
        {
            var evt = _parser.Parse(entry);

            if (evt is null)
                continue;

            try
            {
                _playerService.Handle(evt);

                UpdateState(s => s with
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

        UpdateState(s => s with
        {
            ConnectionState = ServerConnectionState.Offline
        });
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

    private void UpdateState(
        Func<ServerState, ServerState> update)
    {
        _state = update(_state);
        StateChanged?.Invoke(this, _state);
    }
}