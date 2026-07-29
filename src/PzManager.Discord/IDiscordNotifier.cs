namespace PzManager.Discord;

public interface IDiscordNotifier
{
    Task Send(string message);

    Task SendAdmin(string message);
}
