using PzManager.Core.Domain;

namespace PzManager.Core.Services;

public interface IDomainEventSink
{
    Task Handle(IDomainEvent e);
}
