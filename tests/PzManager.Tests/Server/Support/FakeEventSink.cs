using PzManager.Core.Domain;
using PzManager.Core.Services;

namespace PzManager.Tests.Server.Support;

public sealed class FakeEventSink : IDomainEventSink
{
    public List<IDomainEvent> Events { get; } = [];

    public Task Handle(IDomainEvent e)
    {
        Events.Add(e);
        return Task.CompletedTask;
    }
}
