using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PzManager.Core.Logger;

namespace PzManager.Server;

/// <summary>
/// The two config files whose fields can be targeted individually (see
/// <see cref="ServerConfigProvisioner.SetConfigValue"/>). spawnpoints.lua
/// and spawnregions.lua are lists of tables with no stable per-entry key,
/// so they aren't addressable this way.
/// </summary>
public enum PzConfigFile
{
    Ini,
    SandboxVars
}

/// <summary>
/// A single field changed by <see cref="ServerConfigProvisioner.SetPredefinedConfig"/>.
/// <see cref="OldValue"/> is null if the field wasn't present in the file
/// before the override was applied.
/// </summary>
public sealed record ConfigChange(string ParamName, string? OldValue, string NewValue);

public sealed class ServerConfigProvisioner
{
    private static readonly TimeSpan BootstrapTimeout = TimeSpan.FromMinutes(5);

    // Only files that are a flat/nested tree of "field = value" assignments
    // can be safely patched by field name. spawnpoints.lua and
    // spawnregions.lua are lists of tables with no stable key per entry, so
    // they are left exactly as Project Zomboid generated them and are not
    // part of SetPredefinedConfig.
    private static readonly (string BaseFileName, string OverridesFileName, IConfigFilePatcher Patcher)[]
        OverridableFiles =
        [
            ("ZomboidServer.ini", "ZomboidServer.overrides.json", new IniFilePatcher()),
            ("ZomboidServer_SandboxVars.lua", "ZomboidServer_SandboxVars.overrides.json", new LuaAssignmentFilePatcher())
        ];

    /// <summary>
    /// For a server that has never run before, lets Project Zomboid generate
    /// its own default config files (they reflect the exact game version
    /// running) by actually starting it. Starts the real server process and
    /// waits for it to reach "*** SERVER STARTED ***" - confirming it booted
    /// correctly, not just that it wrote a file - then stops it via
    /// <see cref="IServerProcess.Stop"/> (graceful save+quit, falling back to
    /// a kill only if it doesn't respond in time) so the config files are
    /// ready to patch and the process is fully exited before the real start.
    /// If the server fails to start (crashes, or does not report started
    /// within the timeout), the bootstrap process is stopped and the failure
    /// is rethrown so the caller aborts startup instead of patching or
    /// running against a server that never came up cleanly.
    /// Returns false without doing anything if the server already has a
    /// config file (existing server - nothing to bootstrap).
    /// </summary>
    public async Task<bool> BootstrapIfNewAsync(
        IServerProcess process,
        PzServerSettings settings,
        CancellationToken cancellationToken = default)
    {
        var serverDir = Path.Combine(settings.ConfigDirectory, "Server");
        Directory.CreateDirectory(serverDir);

        var iniFileName = "ZomboidServer.ini".Replace("ZomboidServer", settings.ServerName);
        var iniPath = Path.Combine(serverDir, iniFileName);

        if (File.Exists(iniPath))
            return false;

        Log.Info(
            "[SERVER CONFIG] No existing config found. Starting the server once to confirm it "
            + "boots correctly and to let it generate its own default config files...");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(BootstrapTimeout);

        await process.Start();

        try
        {
            await process.WaitUntilStarted(timeoutCts.Token);
        }
        catch (Exception ex)
        {
            Log.Error($"[SERVER CONFIG] Bootstrap failed: the server did not start correctly: {ex.Message}");

            await process.Stop();

            throw new InvalidOperationException(
                "Project Zomboid failed to start during bootstrap. Aborting startup.", ex);
        }

        Log.Info("[SERVER CONFIG] Bootstrap server started correctly. Stopping it to apply predefined config...");

        await process.Stop();

        if (!File.Exists(iniPath))
        {
            throw new InvalidOperationException(
                $"Project Zomboid started successfully but did not generate {iniFileName}.");
        }

        Log.Info("[SERVER CONFIG] Bootstrap config files generated.");

        return true;
    }

    /// <summary>
    /// Forces the JVM -Xmx heap cap (<see cref="PzServerSettings.MaxMemory"/>)
    /// on Data/pz-server/ProjectZomboid64.json, to avoid the server OOM-ing
    /// the host. Unlike the per-server .ini/.lua files, this one lives inside
    /// the SteamCMD install and can be silently reset by "app_update ...
    /// validate" (install/update scripts), so it must be reapplied before
    /// every launch rather than relying on it staying put. Only the -Xmx
    /// entry is touched - every other JVM arg is left as shipped.
    /// </summary>
    public void EnsureMaxMemory(PzServerSettings settings)
    {
        var path = Path.Combine(ServerPaths.DataDirectory, "pz-server", "ProjectZomboid64.json");

        if (!File.Exists(path))
        {
            Log.Warn("[SERVER CONFIG] ProjectZomboid64.json not found yet. Skipping max memory enforcement.");
            return;
        }

        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidOperationException("ProjectZomboid64.json is not a valid JSON object.");

        var vmArgs = root["vmArgs"]?.AsArray()
            ?? throw new InvalidOperationException("ProjectZomboid64.json has no 'vmArgs' array.");

        var xmxArg = $"-Xmx{settings.MaxMemory}";
        var existingIndex = -1;

        for (var i = 0; i < vmArgs.Count; i++)
        {
            if (vmArgs[i]!.GetValue<string>().StartsWith("-Xmx", StringComparison.Ordinal))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0 && vmArgs[existingIndex]!.GetValue<string>() == xmxArg)
            return; // already correct, don't touch the file

        if (existingIndex >= 0)
            vmArgs[existingIndex] = xmxArg;
        else
            vmArgs.Add(xmxArg);

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));

        Log.Info($"[SERVER CONFIG] Set max JVM heap to {settings.MaxMemory} in ProjectZomboid64.json.");
    }

    /// <summary>
    /// Applies the predefined field overrides (Overrides/*.overrides.json)
    /// onto the server's config files. Must run after
    /// <see cref="BootstrapIfNewAsync"/> and before the Project Zomboid
    /// process starts for real. Only the keys present in each overrides file
    /// are touched - everything else (including server-managed fields such
    /// as ResetID, Seed or ServerPlayerID) is left exactly as the server
    /// last wrote it.
    /// Per-deployment fields that live in appsettings.json rather than the
    /// checked-in overrides (<see cref="PzServerSettings.PublicName"/> and
    /// <see cref="PzServerSettings.Password"/>) are merged in on top of the
    /// .ini overrides, taking precedence over whatever the static JSON says
    /// for that same key.
    /// Restricts to a single file when <paramref name="file"/> is given
    /// (e.g. re-apply only the .ini's overrides); applies to both when null.
    /// </summary>
    public IReadOnlyList<ConfigChange> SetPredefinedConfig(PzServerSettings settings, PzConfigFile? file = null)
    {
        var changes = new List<ConfigChange>();
        var serverDir = Path.Combine(settings.ConfigDirectory, "Server");
        var onlyBaseFileName = file is null ? null : BaseFileName(file.Value);

        foreach (var (baseFileName, overridesFileName, patcher) in OverridableFiles)
        {
            if (onlyBaseFileName is not null && baseFileName != onlyBaseFileName)
                continue;

            var destinationFileName = baseFileName.Replace("ZomboidServer", settings.ServerName);
            var destinationPath = Path.Combine(serverDir, destinationFileName);

            if (!File.Exists(destinationPath))
            {
                Log.Warn($"[SERVER CONFIG] {destinationFileName} does not exist yet. Skipping predefined config.");
                continue;
            }

            var overridesPath = Path.Combine(ServerPaths.OverridesDirectory, overridesFileName);

            var overrides = File.Exists(overridesPath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(overridesPath)) ?? []
                : [];

            if (baseFileName == "ZomboidServer.ini" && settings.PublicName is not null)
                overrides["PublicName"] = settings.PublicName;

            // The join password is never passed as a process argument (it
            // would show up in `ps`, journalctl, /proc/<pid>/cmdline, and
            // the dedicated server doesn't even accept a -password CLI flag
            // - see ServerProcess.cs). It only ever lands here, written
            // straight into the .ini.
            if (baseFileName == "ZomboidServer.ini" && settings.Password is not null)
                overrides["Password"] = settings.Password;

            if (overrides.Count == 0)
                continue;

            var oldValues = new Dictionary<string, string?>();

            foreach (var key in overrides.Keys)
                oldValues[key] = patcher.TryGetValue(destinationPath, key, out var oldValue) ? oldValue : null;

            patcher.ApplyOverrides(destinationPath, overrides);

            Log.Info($"[SERVER CONFIG] Applied {overrides.Count} predefined override(s) to {destinationFileName}.");

            // Password is never surfaced back (e.g. to Discord) - see the
            // comment above where it's merged into `overrides`.
            foreach (var (key, newValue) in overrides)
            {
                if (key == "Password")
                    continue;

                if (oldValues[key] != newValue)
                    changes.Add(new ConfigChange(key, oldValues[key], newValue));
            }
        }

        return changes;
    }

    /// <summary>
    /// Sets a single field on one of the server's config files, for ad-hoc
    /// changes (e.g. from a Discord command) without editing the checked-in
    /// Overrides/*.json. For <see cref="PzConfigFile.SandboxVars"/>, a bare
    /// name like "Zombies" or "ZombieLore.Speed" is auto-prefixed with
    /// "SandboxVars." (the file's always-present root table) if not already
    /// given that way - since the file is already explicit here, repeating
    /// the prefix would just be redundant. For <see cref="PzConfigFile.Ini"/>,
    /// use the plain field name (e.g. "PVP"). Throws if the target file
    /// doesn't exist yet or the field name isn't found in it - same
    /// unknown-key guard as <see cref="SetPredefinedConfig"/>.
    /// </summary>
    public void SetConfigValue(PzServerSettings settings, PzConfigFile file, string paramName, string value)
    {
        paramName = NormalizeParamName(file, paramName);

        var (patcher, destinationPath) = ResolveExisting(settings, file);

        patcher.ApplyOverrides(destinationPath, new Dictionary<string, string> { [paramName] = value });

        Log.Info($"[SERVER CONFIG] Set {paramName}={value} in {Path.GetFileName(destinationPath)}.");
    }

    /// <summary>
    /// Same as <see cref="SetConfigValue(PzServerSettings, PzConfigFile, string, string)"/>
    /// but figures out which file the field belongs to instead of requiring
    /// the caller to know. Safe because the two files' key namespaces never
    /// collide - SandboxVars.lua keys are always dotted paths starting with
    /// "SandboxVars.", the .ini's are always plain names - so at most one of
    /// the two files can ever contain a given field. Throws only if the
    /// field is found in neither file; finding it in just one (the normal
    /// case) is not an error.
    /// </summary>
    public void SetConfigValue(PzServerSettings settings, string paramName, string value)
    {
        foreach (var file in AllConfigFiles)
        {
            if (!TryResolve(settings, file, out var patcher, out var destinationPath))
                continue;

            if (!patcher.TryGetValue(destinationPath, paramName, out _))
                continue;

            patcher.ApplyOverrides(destinationPath, new Dictionary<string, string> { [paramName] = value });

            Log.Info($"[SERVER CONFIG] Set {paramName}={value} in {Path.GetFileName(destinationPath)}.");

            return;
        }

        throw new InvalidOperationException(
            $"'{paramName}' was not found in the .ini or in SandboxVars.lua.");
    }

    /// <summary>
    /// Reads the current value of a single field from one of the server's
    /// config files (e.g. for a Discord "get" command). Same key format as
    /// <see cref="SetConfigValue(PzServerSettings, PzConfigFile, string, string)"/>.
    /// Throws if the file doesn't exist yet or the field name isn't found in it.
    /// </summary>
    public string GetConfigValue(PzServerSettings settings, PzConfigFile file, string paramName)
    {
        return GetConfigValue(settings, file, paramName, out _);
    }

    /// <summary>
    /// Same as <see cref="GetConfigValue(PzServerSettings, PzConfigFile, string)"/>,
    /// plus the comment block the game itself wrote directly above the
    /// field (its own explanation of what it does), if any.
    /// </summary>
    public string GetConfigValue(PzServerSettings settings, PzConfigFile file, string paramName, out string? comment)
    {
        paramName = NormalizeParamName(file, paramName);

        var (patcher, destinationPath) = ResolveExisting(settings, file);

        if (!patcher.TryGetValue(destinationPath, paramName, out var value, out comment))
        {
            throw new InvalidOperationException(
                $"'{paramName}' was not found in {Path.GetFileName(destinationPath)}.");
        }

        return value;
    }

    /// <summary>
    /// Same as <see cref="GetConfigValue(PzServerSettings, PzConfigFile, string)"/>
    /// but searches both config files instead of requiring the caller to
    /// know which one holds the field - see <see cref="SetConfigValue(PzServerSettings, string, string)"/>
    /// for why that's safe. Throws only if the field is found in neither file.
    /// </summary>
    public string GetConfigValue(PzServerSettings settings, string paramName)
    {
        return GetConfigValue(settings, paramName, out _);
    }

    /// <summary>
    /// Same as <see cref="GetConfigValue(PzServerSettings, string)"/>, plus
    /// the comment block directly above the field - see
    /// <see cref="GetConfigValue(PzServerSettings, PzConfigFile, string, out string)"/>.
    /// </summary>
    public string GetConfigValue(PzServerSettings settings, string paramName, out string? comment)
    {
        foreach (var file in AllConfigFiles)
        {
            if (!TryResolve(settings, file, out var patcher, out var destinationPath))
                continue;

            if (patcher.TryGetValue(destinationPath, paramName, out var value, out comment))
                return value;
        }

        comment = null;

        throw new InvalidOperationException(
            $"'{paramName}' was not found in the .ini or in SandboxVars.lua.");
    }

    /// <summary>
    /// Reads the full raw contents of one of the server's config files
    /// (e.g. for a Discord "get" command with no field name given).
    /// </summary>
    public string GetConfigFileContent(PzServerSettings settings, PzConfigFile file)
    {
        var (_, destinationPath) = ResolveExisting(settings, file);

        return File.ReadAllText(destinationPath);
    }

    private const string SandboxVarsRootTable = "SandboxVars.";

    /// <summary>
    /// When the file is explicit, "SandboxVars." is redundant to type by
    /// hand every time (that's the file's one and only root table) - accept
    /// "Zombies" as well as "SandboxVars.Zombies" and normalize to the
    /// latter, which is what the patcher actually needs to match.
    /// </summary>
    private static string NormalizeParamName(PzConfigFile file, string paramName)
    {
        if (file == PzConfigFile.SandboxVars && !paramName.StartsWith(SandboxVarsRootTable, StringComparison.Ordinal))
            return SandboxVarsRootTable + paramName;

        return paramName;
    }

    private static string BaseFileName(PzConfigFile file)
    {
        return file switch
        {
            PzConfigFile.Ini => "ZomboidServer.ini",
            PzConfigFile.SandboxVars => "ZomboidServer_SandboxVars.lua",
            _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
        };
    }

    private static IConfigFilePatcher CreatePatcher(PzConfigFile file)
    {
        return file switch
        {
            PzConfigFile.Ini => new IniFilePatcher(),
            PzConfigFile.SandboxVars => new LuaAssignmentFilePatcher(),
            _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
        };
    }

    private static readonly PzConfigFile[] AllConfigFiles = [PzConfigFile.Ini, PzConfigFile.SandboxVars];

    private static (IConfigFilePatcher Patcher, string DestinationPath) ResolveExisting(
        PzServerSettings settings, PzConfigFile file)
    {
        if (!TryResolve(settings, file, out var patcher, out var destinationPath))
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(destinationPath)} does not exist yet. Start the server at least once first.");
        }

        return (patcher, destinationPath);
    }

    private static bool TryResolve(
        PzServerSettings settings, PzConfigFile file, out IConfigFilePatcher patcher, out string destinationPath)
    {
        var baseFileName = BaseFileName(file);
        patcher = CreatePatcher(file);

        var destinationFileName = baseFileName.Replace("ZomboidServer", settings.ServerName);
        var serverDir = Path.Combine(settings.ConfigDirectory, "Server");
        destinationPath = Path.Combine(serverDir, destinationFileName);

        return File.Exists(destinationPath);
    }
}
