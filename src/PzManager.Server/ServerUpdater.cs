using System.Diagnostics;
using PzManager.Core.Logger;

namespace PzManager.Server;

/// <summary>
/// Runs Scripts/update-server.sh (SteamCMD "app_update ... validate") to
/// pull the latest Project Zomboid dedicated server build. Kept separate
/// from IServerProcess/ServerProcess because it's a one-shot blocking
/// command, not a long-running game session - no output streaming into
/// player events, no "started" signal to wait for, just success/failure.
/// </summary>
public sealed class ServerUpdater
{
    public async Task RunAsync()
    {
        var script = Path.Combine(ServerPaths.ScriptsDirectory, "update-server.sh");

        if (!File.Exists(script))
            throw new FileNotFoundException($"Server update script not found: {script}");

        Log.Info($"[SERVER UPDATE] Running {script}...");

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            WorkingDirectory = Path.GetDirectoryName(script)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(script);

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                Log.Info($"[SERVER UPDATE] {e.Data}");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                Log.Error($"[SERVER UPDATE] {e.Data}");
        };

        if (!process.Start())
            throw new InvalidOperationException("Unable to start the server update script.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Server update failed (exit code {process.ExitCode}). Check the logs.");
        }

        Log.Info("[SERVER UPDATE] Update completed.");
    }
}
