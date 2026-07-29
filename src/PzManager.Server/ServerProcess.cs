using System.Diagnostics;
using PzManager.Core.Logger;

namespace PzManager.Server;

public sealed class ServerProcess : IServerProcess
{
    private readonly PzServerSettings _settings;

    private Process? _process;
    private TaskCompletionSource _started =
        CreateStartedSource();

    public bool IsRunning =>
        _process is { HasExited: false };

    public DateTime StartedAt { get; private set; }

    public event Action<string>? OutputReceived;

    public event Action<string>? ErrorReceived;

    public event Action<int>? Exited;

    public ServerProcess(PzServerSettings settings)
    {
        _settings = settings;
    }

    public Task Start()
    {
        if (IsRunning)
            return Task.CompletedTask;

        _started = CreateStartedSource();

        var script = Path.Combine(
            AppContext.BaseDirectory,
            "Scripts",
            "start-server.sh");

        if (!File.Exists(script))
            throw new FileNotFoundException(
                $"Server start script not found: {script}");

        Log.Info($"[SERVER PROCESS] Launching Project Zomboid server process ({script})...");

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            WorkingDirectory = Path.GetDirectoryName(script)!,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add("-adminusername");
        startInfo.ArgumentList.Add(_settings.Admin.Username);
        startInfo.ArgumentList.Add("-adminpassword");
        startInfo.ArgumentList.Add(_settings.Admin.Password);

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            Log.Info($"[SERVER] {e.Data}");

            if (e.Data.Contains("*** SERVER STARTED ***"))
                _started.TrySetResult();

            OutputReceived?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            Log.Error($"[SERVER] {e.Data}");
            ErrorReceived?.Invoke(e.Data);
        };

        process.Exited += (_, _) =>
        {
            if (!_started.Task.IsCompleted)
            {
                _started.TrySetException(
                    new InvalidOperationException(
                        $"Project Zomboid server exited before it started. Exit code: {process.ExitCode}"));
            }

            Exited?.Invoke(process.ExitCode);
        };

        if (!process.Start())
        {
            process.Dispose();

            throw new InvalidOperationException(
                "Unable to start Project Zomboid server.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _process = process;
        StartedAt = DateTime.UtcNow;

        Log.Info($"[SERVER PROCESS] Process launched (pid={process.Id}).");

        return Task.CompletedTask;
    }

    public Task WaitUntilStarted(
        CancellationToken cancellationToken = default)
    {
        return _started.Task.WaitAsync(cancellationToken);
    }

    public async Task Stop()
    {
        var process = _process;

        if (process is null || process.HasExited)
            return;

        Log.Info("[SERVER PROCESS] Stopping Project Zomboid server process...");

        await SendCommand("save");
        await SendCommand("quit");

        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(30));

        try
        {
            await process.WaitForExitAsync(cts.Token);

            Log.Info("[SERVER PROCESS] Process stopped.");
        }
        catch (OperationCanceledException)
        {
            Log.Warn(
                "[SERVER PROCESS] Server did not stop gracefully. Killing process...");

            process.Kill(true);
            await process.WaitForExitAsync();

            Log.Info("[SERVER PROCESS] Process killed.");
        }
    }

    public async Task SendCommand(string command)
    {
        var process = _process;

        if (process is null || process.HasExited)
            return;

        await process.StandardInput.WriteLineAsync(command);
        await process.StandardInput.FlushAsync();
    }

    private static TaskCompletionSource CreateStartedSource()
    {
        return new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }
}