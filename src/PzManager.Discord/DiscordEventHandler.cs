using PzManager.Core.Domain;

namespace PzManager.Discord;

public sealed class DiscordEventHandler
{
    private readonly DiscordBot _bot;

    public DiscordEventHandler(DiscordBot bot)
    {
        _bot = bot;
    }

    public async Task Handle(IDomainEvent e)
    {
        switch (e)
        {
            case PlayerLoggedInEvent login:
                await Handle(login);
                break;

            case PlayerCreatedEvent created:
                await Handle(created);
                break;

            case PlayerDiedEvent died:
                await Handle(died);
                break;

            case SkillLevelChangedEvent levelChanged:
                await Handle(levelChanged);
                break;

            case PlayerSetSkillsEvent:
                // El resumen inicial de perks no genera mensaje.
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported event type {e.GetType().Name}");
        }
    }

    private async Task Handle(PlayerLoggedInEvent e)
    {
        await SendMessage(
            $"🟢 **{e.Username}** joined the server.");
    }

    private async Task Handle(PlayerCreatedEvent e)
    {
        await SendMessage(
            $"✨ **{e.Username}** created a new character.");
    }

    private async Task Handle(PlayerDiedEvent e)
    {
        await SendMessage(
            $"💀 **{e.Username}** died after surviving {e.HoursSurvived} hours.");
    }

    private async Task Handle(SkillLevelChangedEvent e)
    {
        await SendMessage(
            $"📈 **{e.Username}** reached **{e.UpdatedSkill.Id} {e.UpdatedSkill.Level}**.");
    }

    private async Task SendMessage(string message)
    {
        await _bot.Send(message);
    }
}