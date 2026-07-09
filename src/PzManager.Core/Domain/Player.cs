namespace PzManager.Core.Domain;

public sealed class Player
{
    public DateTime LastUpdated { get; set; }
    public long SteamId { get; set; }
    public string Username { get; set;}
    public Position Position { get; set; }
    public Dictionary<string, Skill> Skills { get; set; } = new();
    public int HoursSurvived { get; set; }
    public bool Dead { get; set; } = false;

    public Player(DateTime datetime, long steam_id, string username, Position position, int hours_survived)
    {
        LastUpdated = datetime;
        SteamId = steam_id;
        Username = username;
        Position = position;
        HoursSurvived = hours_survived;
    }

    public void SetSkill(Skill skill)
    {
        Skills[skill.Id] = skill;
    }

    public void SetSkills(IEnumerable<Skill> skills)
    {
        Skills.Clear();

        foreach (var skill in skills)
        {
            Skills[skill.Id] = skill;
        }
    }
}