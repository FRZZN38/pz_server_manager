using PzManager.Core.Domain;
using PzManager.Discord;
using PzManager.Tests.Discord.Support;

namespace PzManager.Tests.Discord;

public sealed class DiscordEventHandlerTests
{
    private static readonly Position SamplePosition = new(100, 200, 0);
    private static readonly DateTime SampleTimestamp = new(2026, 7, 5, 21, 55, 2);

    [Fact]
    public async Task Handle_PlayerLoggedInEvent_SendsJoinMessage()
    {
        var notifier = new FakeDiscordNotifier();
        var handler = new DiscordEventHandler(notifier);

        await handler.Handle(new PlayerLoggedInEvent(
            SampleTimestamp, 1, "TestPlayer", SamplePosition, 42));

        Assert.Equal(
            "🟢 **TestPlayer** joined the server.",
            Assert.Single(notifier.Messages));
    }

    [Fact]
    public async Task Handle_PlayerCreatedEvent_SendsCreatedMessage()
    {
        var notifier = new FakeDiscordNotifier();
        var handler = new DiscordEventHandler(notifier);

        await handler.Handle(new PlayerCreatedEvent(
            SampleTimestamp, 1, "TestPlayer", SamplePosition, 0));

        Assert.Equal(
            "✨ **TestPlayer** created a new character.",
            Assert.Single(notifier.Messages));
    }

    [Fact]
    public async Task Handle_PlayerDiedEvent_SendsDeathMessageWithHoursSurvived()
    {
        var notifier = new FakeDiscordNotifier();
        var handler = new DiscordEventHandler(notifier);

        await handler.Handle(new PlayerDiedEvent(
            SampleTimestamp, 1, "TestPlayer", SamplePosition, 73));

        Assert.Equal(
            "💀 **TestPlayer** died after surviving 73 hours.",
            Assert.Single(notifier.Messages));
    }

    [Fact]
    public async Task Handle_SkillLevelChangedEvent_SendsLevelUpMessage()
    {
        var notifier = new FakeDiscordNotifier();
        var handler = new DiscordEventHandler(notifier);

        await handler.Handle(new SkillLevelChangedEvent(
            SampleTimestamp, 1, "TestPlayer", SamplePosition, new Skill("Woodwork", 4), 10));

        Assert.Equal(
            "📈 **TestPlayer** reached **Woodwork 4**.",
            Assert.Single(notifier.Messages));
    }

    [Fact]
    public async Task Handle_PlayerSetSkillsEvent_DoesNotSendAnyMessage()
    {
        var notifier = new FakeDiscordNotifier();
        var handler = new DiscordEventHandler(notifier);

        await handler.Handle(new PlayerSetSkillsEvent(
            SampleTimestamp, 1, "TestPlayer", SamplePosition, [new Skill("Cooking", 1)], 10));

        Assert.Empty(notifier.Messages);
    }
}
