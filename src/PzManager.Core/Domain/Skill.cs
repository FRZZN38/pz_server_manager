namespace PzManager.Core.Domain;

public sealed class Skill
{
    public string Id { get; init; }

    public int Level { get; init; }

    public Skill(string id, int level)
    {
        Id = id;
        Level = level;
    }
}