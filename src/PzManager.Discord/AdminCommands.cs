using Discord;
using Discord.WebSocket;
using PzManager.Core.Logger;
using PzManager.Server;

namespace PzManager.Discord;

public sealed class AdminCommands
{
    private static readonly string[] CommandNames =
    [
        "save", "broadcast", "kick", "start", "restart", "stop", "admin_status",
        "set_config", "get_config", "update_server", "admin_help"
    ];

    // Commands that reach into the shared IServerProcess (directly, or via
    // ServerManager.Save/Broadcast/Kick/Start/Stop/Restart) - unsafe to run
    // while Creating/Updating currently own that same process. See the
    // guard in Handle().
    private static readonly string[] ProcessSensitiveCommands =
    [
        "save", "broadcast", "kick", "start", "restart", "stop", "update_server"
    ];

    private readonly ServerManager _serverManager;
    private readonly ServerConfigProvisioner _configProvisioner;
    private readonly ServerUpdater _serverUpdater;
    private readonly IServerProcess _process;
    private readonly PzServerSettings _settings;
    private readonly ulong _adminChannelId;

    public AdminCommands(
        ServerManager serverManager,
        ServerConfigProvisioner configProvisioner,
        ServerUpdater serverUpdater,
        IServerProcess process,
        PzServerSettings settings,
        ulong adminChannelId)
    {
        _serverManager = serverManager;
        _configProvisioner = configProvisioner;
        _serverUpdater = serverUpdater;
        _process = process;
        _settings = settings;
        _adminChannelId = adminChannelId;
    }

    public bool CanHandle(string commandName)
    {
        return CommandNames.Contains(commandName);
    }

    public IReadOnlyList<SlashCommandBuilder> BuildCommands()
    {
        var save = new SlashCommandBuilder()
            .WithName("save")
            .WithDescription("Save the world immediately.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        var broadcast = new SlashCommandBuilder()
            .WithName("broadcast")
            .WithDescription("Send a message to every connected player.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(
                "message",
                ApplicationCommandOptionType.String,
                "Message to broadcast",
                isRequired: true);

        var kick = new SlashCommandBuilder()
            .WithName("kick")
            .WithDescription("Kick a connected player.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(
                "username",
                ApplicationCommandOptionType.String,
                "Player username",
                isRequired: true);

        var start = new SlashCommandBuilder()
            .WithName("start")
            .WithDescription("Start the Project Zomboid server.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        var restart = new SlashCommandBuilder()
            .WithName("restart")
            .WithDescription("Restart the Project Zomboid server.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        var stop = new SlashCommandBuilder()
            .WithName("stop")
            .WithDescription("Stop the Project Zomboid server.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        var status = new SlashCommandBuilder()
            .WithName("admin_status")
            .WithDescription("Show the Project Zomboid server's full status.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        // Same 3-tier shape for both set_config and get_config: no file, no
        // param_name -> everything (both files); file only -> that whole
        // file; param_name given -> just that field (file is an optional
        // narrowing hint there, not required - see AllConfigFiles searches).
        var setConfigFileOption = new SlashCommandOptionBuilder()
            .WithName("file")
            .WithDescription("Which config file. Omit to affect/search both.")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(false)
            .AddChoice("ZomboidServer.ini", "ini")
            .AddChoice("ZomboidServer_SandboxVars.lua", "sandboxvars");

        var setConfig = new SlashCommandBuilder()
            .WithName("set_config")
            .WithDescription("Set one field (param_name), one file's overrides (file only), or all overrides (neither).")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(setConfigFileOption)
            .AddOption(
                "param_name",
                ApplicationCommandOptionType.String,
                "Field name: plain (PVP) for the .ini, dotted (SandboxVars.Zombies). Omit to re-apply overrides.",
                isRequired: false)
            .AddOption(
                "value",
                ApplicationCommandOptionType.String,
                "New value, exactly as written (e.g. true, 5, \"\" for empty Lua string). Required with param_name.",
                isRequired: false);

        var getFileOption = new SlashCommandOptionBuilder()
            .WithName("file")
            .WithDescription("Which config file. Omit to affect/search both.")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(false)
            .AddChoice("ZomboidServer.ini", "ini")
            .AddChoice("ZomboidServer_SandboxVars.lua", "sandboxvars");

        var getConfig = new SlashCommandBuilder()
            .WithName("get_config")
            .WithDescription("Read one field (param_name), one whole file (file only), or both files (neither).")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(getFileOption)
            .AddOption(
                "param_name",
                ApplicationCommandOptionType.String,
                "Field name (see /set_config). file is optional - both files are searched if omitted.",
                isRequired: false);

        var updateServer = new SlashCommandBuilder()
            .WithName("update_server")
            .WithDescription("Update the Project Zomboid dedicated server via SteamCMD. Server must be stopped first.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        var help = new SlashCommandBuilder()
            .WithName("admin_help")
            .WithDescription("Show available admin commands.")
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

        return
        [
            save, broadcast, kick,
            start, restart, stop, status,
            setConfig, getConfig, updateServer, help
        ];
    }

    public async Task Handle(
        SocketSlashCommand command)
    {
        if (command.Channel.Id != _adminChannelId)
        {
            await command.RespondAsync(
                "This command can only be used in the admin channel.",
                ephemeral: true);

            return;
        }

        // Config-mutating commands stay public: if one admin changes a
        // setting, the others need to see it happened (and what changed),
        // or a later "why did PVP just turn off" goes unanswered. Every
        // other command - status/help queries and simple action acks (the
        // real state transitions are announced separately via
        // ServerManager.StateChanged anyway) - is ephemeral to keep the
        // channel from filling up with messages only the invoker cares about.
        var ephemeral = command.Data.Name is not ("set_config" or "update_server");

        // Some commands (stop, in particular) take longer than Discord's 3s
        // ack window - a graceful save+quit can run up to 30s. Defer first
        // so Discord always sees an immediate ack, then send the real
        // result as a followup once the work is done.
        await command.DeferAsync(ephemeral: ephemeral);

        // Creating/Updating mean BootstrapIfNewAsync/ServerUpdater currently
        // own the shared IServerProcess directly, outside of ServerManager's
        // own session - any of these commands reaching in at the same time
        // (an admin /kick-ing someone mid-bootstrap, a /stop racing the
        // update script) would step on that. ServerManager's own Offline
        // checks below only catch this by accident (Creating/Updating both
        // read as "not Offline"), so make it an explicit, clearly-worded
        // rejection instead.
        if (ProcessSensitiveCommands.Contains(command.Data.Name)
            && _serverManager.State.ConnectionState is ServerConnectionState.Creating or ServerConnectionState.Updating)
        {
            var activity = _serverManager.State.ConnectionState == ServerConnectionState.Creating
                ? "being created for the first time"
                : "being updated";

            await command.FollowupAsync(
                $"The server is currently {activity} - try again in a moment.", ephemeral: ephemeral);

            return;
        }

        switch (command.Data.Name)
        {
            // save/broadcast/kick/start/restart/stop all follow the same
            // shape: acknowledge that the action was requested right away,
            // then perform it - no waiting for the server to actually
            // finish before responding. Real state transitions (Starting,
            // Running, Stopping, Offline, Crashed) get their own separate
            // notification via ServerManager.StateChanged, so the command
            // reply doesn't need to describe the outcome, only confirm the
            // action was received. Errors still get a follow-up.
            case "save":
                await command.FollowupAsync("Server save requested....", ephemeral: ephemeral);
                await RunAndReportErrors(command, "save", _serverManager.SaveAsync, ephemeral);
                break;

            case "broadcast":
                var message = ReadOption(command);

                await command.FollowupAsync($"Server broadcast requested: {message}", ephemeral: ephemeral);
                await RunAndReportErrors(command, "broadcast", () => _serverManager.BroadcastAsync(message), ephemeral);
                break;

            case "kick":
                var username = ReadOption(command);

                await command.FollowupAsync($"Server kick requested: `{username}`.", ephemeral: ephemeral);
                await RunAndReportErrors(command, "kick", () => _serverManager.KickAsync(username), ephemeral);
                break;

            case "start":
                // Crashed is accepted too - it's how a failed Creating
                // attempt (see the catch below) shows up, and the whole
                // point of surfacing it that way instead of silently
                // resetting to Offline is that an admin can just /start
                // again to retry.
                if (_serverManager.State.ConnectionState is not (ServerConnectionState.Offline
                    or ServerConnectionState.Crashed))
                {
                    await command.FollowupAsync(
                        "The server is already running (or starting).", ephemeral: ephemeral);

                    break;
                }

                // Covers the "never run before" case (no config files yet)
                // without requiring a full pzmanager restart to trigger it.
                // BootstrapIfNewAsync is a cheap no-op (early exit) once a
                // config already exists, but the predefined overrides and
                // the max-heap cap only need (re)applying right after it
                // actually generates fresh config files - a normal /start
                // on an already-provisioned server shouldn't silently
                // stomp on settings an admin changed by hand since then.
                var creating = false;

                try
                {
                    var wasBootstrapped = await _configProvisioner.BootstrapIfNewAsync(
                        _process, _settings,
                        onBootstrapping: async () =>
                        {
                            creating = true;

                            // BootstrapIfNewAsync can take a few minutes (it
                            // starts the game once for real, waits for it to
                            // report started, then stops it again) - without
                            // this, Discord shows nothing but "thinking..."
                            // the whole time, which reads as the command
                            // having hung.
                            await command.FollowupAsync(
                                "No existing server found - creating it for the first time "
                                + "(this can take a few minutes)...",
                                ephemeral: ephemeral);

                            await _serverManager.BeginCreateAsync();
                        });

                    if (wasBootstrapped)
                    {
                        _configProvisioner.SetPredefinedConfig(_settings);
                        _configProvisioner.EnsureMaxMemory(_settings);
                        await _serverManager.EndCreateAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Anything that breaks this flow goes to Crashed rather
                    // than getting stuck in Creating or silently resetting
                    // to Offline as if nothing happened - Crashed is the
                    // one state that already means "an admin needs to look
                    // at this, but /start (or /update_server) can retry".
                    if (creating)
                        await _serverManager.MarkCrashedAsync();

                    await RespondError(command, "start", ex, ephemeral);
                    break;
                }

                await command.FollowupAsync("Server start requested....", ephemeral: ephemeral);
                await RunAndReportErrors(command, "start", _serverManager.StartAsync, ephemeral);
                break;

            case "restart":
                if (_serverManager.State.ConnectionState == ServerConnectionState.Offline)
                {
                    await command.FollowupAsync(
                        "The server isn't running. Use `/start` instead.", ephemeral: ephemeral);

                    break;
                }

                await command.FollowupAsync("Server restart requested....", ephemeral: ephemeral);
                await RunAndReportErrors(command, "restart", _serverManager.RestartAsync, ephemeral);
                break;

            case "stop":
                if (_serverManager.State.ConnectionState == ServerConnectionState.Offline)
                {
                    await command.FollowupAsync(
                        "The server is already stopped.", ephemeral: ephemeral);

                    break;
                }

                await command.FollowupAsync("Server stop requested....", ephemeral: ephemeral);
                await RunAndReportErrors(command, "stop", _serverManager.StopAsync, ephemeral);
                break;

            case "admin_status":
                await command.FollowupAsync(
                    $"```{ServerStateFormatter.Build(_serverManager.State)}```", ephemeral: ephemeral);
                break;

            case "admin_help":
                await command.FollowupAsync(BuildHelpText(), ephemeral: ephemeral);
                break;

            // set_config and get_config share the same 3-tier shape:
            //   neither file nor param_name -> everything (both files)
            //   file only                   -> that whole file
            //   param_name given            -> just that one field (file is
            //     an optional narrowing hint - both files get searched for
            //     the field when it's omitted, see ServerConfigProvisioner)
            case "set_config":
                var setFileChoice = ReadOptionalOption(command, "file");
                var setParamName = ReadOptionalOption(command, "param_name");

                if (setParamName is null)
                {
                    var setFile = setFileChoice is null ? (PzConfigFile?)null : ParseConfigFile(setFileChoice);
                    IReadOnlyList<ConfigChange> predefinedChanges;

                    try
                    {
                        predefinedChanges = _configProvisioner.SetPredefinedConfig(_settings, setFile);
                    }
                    catch (Exception ex)
                    {
                        await RespondError(command, "set_config", ex, ephemeral);
                        break;
                    }

                    var scope = setFileChoice is null ? "all files" : DescribeFile(setFileChoice);

                    if (predefinedChanges.Count == 0)
                    {
                        await command.FollowupAsync(
                            $"Server config re-applied ({scope}). Nothing to apply.", ephemeral: ephemeral);
                        break;
                    }

                    var summary = $"Server config re-applied ({scope})." + DescribeChanges(predefinedChanges);

                    if (predefinedChanges.Any(c => c.Changed))
                        summary += RunningWarningSuffix();

                    await command.FollowupAsync(summary, ephemeral: ephemeral);
                    break;
                }

                var setValue = ReadOptionalOption(command, "value");

                if (setValue is null)
                {
                    await command.FollowupAsync(
                        "`value` is required when `param_name` is given.", ephemeral: ephemeral);

                    break;
                }

                string? oldValue;

                try
                {
                    oldValue = setFileChoice is null
                        ? _configProvisioner.GetConfigValue(_settings, setParamName)
                        : _configProvisioner.GetConfigValue(_settings, ParseConfigFile(setFileChoice), setParamName);
                }
                catch (Exception ex)
                {
                    await RespondError(command, "set_config", ex, ephemeral);
                    break;
                }

                if (oldValue == setValue)
                {
                    await command.FollowupAsync(
                        $"`{setParamName}` is already `{setValue}` - no change.", ephemeral: ephemeral);
                    break;
                }

                try
                {
                    if (setFileChoice is null)
                        _configProvisioner.SetConfigValue(_settings, setParamName, setValue);
                    else
                        _configProvisioner.SetConfigValue(_settings, ParseConfigFile(setFileChoice), setParamName, setValue);
                }
                catch (Exception ex)
                {
                    await RespondError(command, "set_config", ex, ephemeral);
                    break;
                }

                await command.FollowupAsync(
                    $"Server config set: `{setParamName}` from `{oldValue}` to `{setValue}`.{RunningWarningSuffix()}",
                    ephemeral: ephemeral);
                break;

            case "get_config":
                var getFileChoice = ReadOptionalOption(command, "file");
                var getParamName = ReadOptionalOption(command, "param_name");

                try
                {
                    if (getParamName is not null)
                    {
                        string value2;
                        string? comment;

                        if (getFileChoice is null)
                            value2 = _configProvisioner.GetConfigValue(_settings, getParamName, out comment);
                        else
                            value2 = _configProvisioner.GetConfigValue(
                                _settings, ParseConfigFile(getFileChoice), getParamName, out comment);

                        var commentBlock = string.IsNullOrEmpty(comment) ? "" : $"\n```{comment}```";

                        await command.FollowupAsync($"`{getParamName} = {value2}`{commentBlock}", ephemeral: ephemeral);
                    }
                    else if (getFileChoice is null)
                    {
                        // Neither given: both files, one attachment each.
                        await command.FollowupWithFileAsync(
                            ToStream(_configProvisioner.GetConfigFileContent(_settings, PzConfigFile.Ini)),
                            $"{_settings.ServerName}.ini",
                            ephemeral: ephemeral);

                        await command.FollowupWithFileAsync(
                            ToStream(_configProvisioner.GetConfigFileContent(_settings, PzConfigFile.SandboxVars)),
                            $"{_settings.ServerName}_SandboxVars.lua",
                            ephemeral: ephemeral);
                    }
                    else
                    {
                        var configFile = ParseConfigFile(getFileChoice);
                        var content = _configProvisioner.GetConfigFileContent(_settings, configFile);
                        var fileName = getFileChoice == "ini"
                            ? $"{_settings.ServerName}.ini"
                            : $"{_settings.ServerName}_SandboxVars.lua";

                        await command.FollowupWithFileAsync(ToStream(content), fileName, ephemeral: ephemeral);
                    }
                }
                catch (Exception ex)
                {
                    await RespondError(command, "get_config", ex, ephemeral);
                }

                break;

            case "update_server":
                // Crashed accepted too, same reasoning as /start - it's how
                // a failed update shows up, and an admin should be able to
                // just retry with /update_server.
                if (_serverManager.State.ConnectionState is not (ServerConnectionState.Offline
                    or ServerConnectionState.Crashed))
                {
                    await command.FollowupAsync(
                        "The server must be stopped before updating. Use `/stop` first.", ephemeral: ephemeral);

                    break;
                }

                await command.FollowupAsync("Server update started (this can take a while)...", ephemeral: ephemeral);

                await _serverManager.BeginUpdateAsync();

                try
                {
                    await _serverUpdater.RunAsync();

                    // SteamCMD's "app_update ... validate" only touches
                    // Data/pz-server (the install dir) - the per-server
                    // .ini/.lua overrides live under ConfigDirectory
                    // (Data/Zomboid), a completely separate tree it never
                    // writes to, so only the max-heap cap (which does live
                    // inside Data/pz-server, in ProjectZomboid64.json) needs
                    // reapplying here.
                    var maxMemoryChange = _configProvisioner.EnsureMaxMemory(_settings);
                    var updateChanges = maxMemoryChange is null
                        ? []
                        : new List<ConfigChange> { maxMemoryChange };

                    await _serverManager.EndUpdateAsync();

                    await command.FollowupAsync(
                        "Server update completed." + DescribeChanges(updateChanges), ephemeral: ephemeral);
                }
                catch (Exception ex)
                {
                    // Same reasoning as /start's catch: a failed update
                    // goes to Crashed, not back to a falsely-clean Offline
                    // and not stuck in Updating - /update_server can retry.
                    await _serverManager.MarkCrashedAsync();
                    await RespondError(command, "update_server", ex, ephemeral);
                }

                break;
        }
    }

    private static string DescribeChanges(IReadOnlyList<ConfigChange> changes)
    {
        var changed = changes.Where(c => c.Changed).ToList();
        var unchanged = changes.Where(c => !c.Changed).ToList();
        var summary = "";

        if (changed.Count > 0)
        {
            var changedLines = changed
                .Select(c => $"- `{c.ParamName}`: `{c.OldValue ?? "(unset)"}` -> `{c.NewValue}`");

            summary += $"\nChanged:\n{string.Join('\n', changedLines)}";
        }

        if (unchanged.Count > 0)
        {
            var unchangedLines = unchanged
                .Select(c => $"- `{c.ParamName}`: `{c.NewValue}`");

            summary += $"\nAlready set:\n{string.Join('\n', unchangedLines)}";
        }

        return summary;
    }

    private static string DescribeFile(string fileChoice)
    {
        return fileChoice == "ini" ? "the .ini" : "SandboxVars.lua";
    }

    private static MemoryStream ToStream(string content)
    {
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
    }

    private string BuildHelpText()
    {
        return SlashCommandHelpFormatter.Build("**Admin commands:**", BuildCommands());
    }

    private static PzConfigFile ParseConfigFile(string choice)
    {
        return choice switch
        {
            "ini" => PzConfigFile.Ini,
            "sandboxvars" => PzConfigFile.SandboxVars,
            _ => throw new InvalidOperationException($"Unknown file choice: {choice}")
        };
    }

    private static async Task RespondError(SocketSlashCommand command, string commandName, Exception ex, bool ephemeral)
    {
        Log.Error($"[DISCORD ADMIN] `/{commandName}` failed: {ex}");

        await command.FollowupAsync($"`/{commandName}` failed: {ex.Message}", ephemeral: ephemeral);
    }

    /// <summary>
    /// Runs the actual server action after the "requested" acknowledgement
    /// has already been sent. Success is silent - state-change notifications
    /// cover that - but a failure still gets its own follow-up so it's never
    /// swallowed.
    /// </summary>
    private static async Task RunAndReportErrors(
        SocketSlashCommand command, string commandName, Func<Task> action, bool ephemeral)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await RespondError(command, commandName, ex, ephemeral);
        }
    }

    private string RunningWarningSuffix()
    {
        return _serverManager.State.ConnectionState != ServerConnectionState.Offline
            ? " The server is still running - this won't take effect until it's restarted (`/stop` then `/start`, or `/restart`)."
            : "";
    }

    private static string ReadOption(SocketSlashCommand command)
    {
        return command.Data.Options
            .First()
            .Value!
            .ToString()!;
    }

    private static string? ReadOptionalOption(SocketSlashCommand command, string name)
    {
        return command.Data.Options.FirstOrDefault(o => o.Name == name)?.Value is string value
            ? value
            : null;
    }
}
