namespace PzManager.Server;

public interface IServerProcess
{
    bool IsRunning { get; }

    DateTime StartedAt { get; }

    event Action<string>? OutputReceived;

    event Action<string>? ErrorReceived;

    event Action<int>? Exited;

    Task Start();

    Task WaitUntilStarted(CancellationToken cancellationToken = default);

    Task Stop();

    Task SendCommand(string command);
}
