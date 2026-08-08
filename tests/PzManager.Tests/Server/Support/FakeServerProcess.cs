using PzManager.Server;

namespace PzManager.Tests.Server.Support;

public sealed class FakeServerProcess : IServerProcess
{
    private readonly List<string> _commands = [];

    private TaskCompletionSource _startedTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsRunning { get; private set; }

    public DateTime StartedAt { get; private set; }

    public int StartCount { get; private set; }

    public int FailNextStarts { get; set; }

    public Action? OnStart { get; set; }

    public IReadOnlyList<string> Commands => _commands;

    public event Action<string>? OutputReceived;

    public event Action<string>? ErrorReceived;

    public event Action<int>? Exited;

    public Task Start()
    {
        StartCount++;

        // Predates any PerkLog file created by the test setup so PerkLogLocator can find it immediately.
        StartedAt = DateTime.UtcNow.AddMinutes(-10);

        _startedTcs = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        OnStart?.Invoke();

        if (FailNextStarts > 0)
        {
            FailNextStarts--;
            IsRunning = false;
            _startedTcs.TrySetException(
                new InvalidOperationException("Server exited before it started."));
            Exited?.Invoke(1);
        }
        else
        {
            IsRunning = true;
            _startedTcs.TrySetResult();
        }

        return Task.CompletedTask;
    }

    public Task WaitUntilStarted(CancellationToken cancellationToken = default)
    {
        return _startedTcs.Task.WaitAsync(cancellationToken);
    }

    public void SimulateCrash(int exitCode = 1)
    {
        IsRunning = false;
        Exited?.Invoke(exitCode);
    }

    public Task Stop()
    {
        IsRunning = false;
        Exited?.Invoke(0);
        return Task.CompletedTask;
    }

    public Task SendCommand(string command)
    {
        _commands.Add(command);
        return Task.CompletedTask;
    }
}
