namespace PzManager.Core.Events;

public interface IDomainEvent
{
    DateTime Timestamp { get; }
}