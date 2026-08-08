using PzManager.Core.Domain;

namespace PzManager.Core.Services;

public sealed class PlayerService
{
    private readonly Dictionary<string, Player> _players = new();

    public void Handle(IDomainEvent e)
    {
        switch (e)
        {
            case PlayerLoggedInEvent login:
                Handle(login);
                break;

            case PlayerSetSkillsEvent skills:
                Handle(skills);
                break;

            case SkillLevelChangedEvent levelChanged:
                Handle(levelChanged);
                break;
            
            case PlayerDiedEvent died:
                Handle(died);
                break;
            
            case PlayerCreatedEvent created:
                Handle(created);
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported event type {e.GetType().Name}");
        }
    }

    public void Handle(PlayerLoggedInEvent e)
    {
        if (!_players.TryGetValue(e.Username, out var player))
        {
            player = new Player(
                e.Timestamp,
                e.SteamId,
                e.Username,
                e.Position,
                e.HoursSurvived);

            _players[e.Username] = player;
            return;
        }

        player.LastUpdated = e.Timestamp;
        player.Position = e.Position;
        player.HoursSurvived = e.HoursSurvived;
    }

    public void Handle(PlayerSetSkillsEvent e)
    {
        if (!_players.TryGetValue(e.Username, out var player))
        {
            player = new Player(
                e.Timestamp,
                e.SteamId,
                e.Username,
                e.Position,
                e.HoursSurvived);

            _players[e.Username] = player;
        }
        player.LastUpdated = e.Timestamp;
        player.Position = e.Position;
        player.HoursSurvived = e.HoursSurvived;

        player.SetSkills(e.Skills);
    }

    public void Handle(SkillLevelChangedEvent e)
    {
        if (!_players.TryGetValue(e.Username, out var player))
        {
            player = new Player(
                e.Timestamp,
                e.SteamId,
                e.Username,
                e.Position,
                e.HoursSurvived);

            _players[e.Username] = player;
        }

        player.LastUpdated = e.Timestamp;
        player.Position = e.Position;
        player.HoursSurvived = e.HoursSurvived;

        player.SetSkill(e.UpdatedSkill);
    }

    public Player? GetPlayer(string username)
    {
        _players.TryGetValue(username, out var player);
        return player;
    }

    public IReadOnlyCollection<Player> GetPlayers()
    {
        return _players.Values;
    }

    public void Handle(PlayerDiedEvent e)
    {
        if (!_players.TryGetValue(e.Username, out var player))
        {
            player = new Player(
                e.Timestamp,
                e.SteamId,
                e.Username,
                e.Position,
                e.HoursSurvived);

            _players[e.Username] = player;
            return;
        }

        player.LastUpdated = e.Timestamp;
        player.Position = e.Position;
        player.HoursSurvived = e.HoursSurvived;
        player.Dead = true;
    }

    public void Handle(PlayerCreatedEvent e)
    {
        if (!_players.TryGetValue(e.Username, out var player))
        {
            player = new Player(
                e.Timestamp,
                e.SteamId,
                e.Username,
                e.Position,
                e.HoursSurvived);

            _players[e.Username] = player;
        }

        player.LastUpdated = e.Timestamp;
        player.Position = e.Position;
        player.HoursSurvived = e.HoursSurvived;
        player.Dead = false;
    }

}