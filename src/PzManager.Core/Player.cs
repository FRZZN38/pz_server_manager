namespace PzManager.Core;

public sealed class Player
{
    public required string Username { get; init; }
    public IDictionary<string, Skill> Skills { get; } = new Dictionary<string, Skill>();
}
