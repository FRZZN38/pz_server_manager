namespace PzManager.Core.Domain;

public sealed record Position(
    int X,
    int Y,
    int Z);

public interface IDomainEvent
{
    DateTime Timestamp { get; }

    long SteamId { get; }

    string Username { get; }

    Position Position { get; }

    int HoursSurvived { get; }
}

public sealed record PlayerLoggedInEvent(
    DateTime Timestamp,
    long SteamId,
    string Username,
    Position Position,
    int HoursSurvived) : IDomainEvent;

public sealed record PlayerSetSkillsEvent(
    DateTime Timestamp,
    long SteamId,
    string Username,
    Position Position,
    IReadOnlyCollection<Skill> Skills,
    int HoursSurvived) : IDomainEvent;

public sealed record SkillLevelChangedEvent(
    DateTime Timestamp,
    long SteamId,
    string Username,
    Position Position,
    Skill UpdatedSkill,
    int HoursSurvived) : IDomainEvent;

public sealed record PlayerDiedEvent(
    DateTime Timestamp,
    long SteamId,
    string Username,
    Position Position,
    int HoursSurvived) : IDomainEvent;

public sealed record PlayerCreatedEvent(
    DateTime Timestamp,
    long SteamId,
    string Username,
    Position Position,
    int HoursSurvived) : IDomainEvent;