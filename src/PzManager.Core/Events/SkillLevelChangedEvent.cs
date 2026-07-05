namespace PzManager.Core.Events;

public sealed record SkillLevelChangedEvent(
    DateTime Timestamp,
    string Username,
    string SkillName,
    int NewLevel
) : IDomainEvent;