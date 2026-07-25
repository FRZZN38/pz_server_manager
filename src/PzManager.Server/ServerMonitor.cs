namespace PzManager.Server;

public sealed class ServerMonitor
{
    private readonly ServerProcess _process;

    private bool _lastKnownState;

    public event EventHandler? ServerStarted;

    public event EventHandler? ServerStopped;

    public ServerMonitor(ServerProcess process)
    {
        _process = process;
        _lastKnownState = process.IsRunning;
    }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var running = _process.IsRunning;

            if (running != _lastKnownState)
            {
                _lastKnownState = running;

                if (running)
                    ServerStarted?.Invoke(this, EventArgs.Empty);
                else
                    ServerStopped?.Invoke(this, EventArgs.Empty);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), token);
        }
    }
}