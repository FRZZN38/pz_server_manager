using PzManager.Discord;
using PzManager.Server;

namespace PzManager.Tests.Discord;

public sealed class ServerStateFormatterTests
{
    [Fact]
    public void BuildPublic_DoesNotLeakFilesystemPathsOrLastEventDetails()
    {
        var state = new ServerState(
            "TestServer",
            "/srv/config",
            "/srv/logs",
            "/srv/logs/PerkLog.txt",
            ServerConnectionState.Running,
            DateTime.UtcNow,
            "PlayerLoggedInEvent",
            3);

        var text = ServerStateFormatter.BuildPublic(state);

        Assert.Contains("3", text);
        Assert.DoesNotContain("/srv", text);
        Assert.DoesNotContain("PlayerLoggedInEvent", text);
    }

    [Theory]
    [InlineData(ServerConnectionState.Running, "online")]
    [InlineData(ServerConnectionState.Offline, "offline")]
    [InlineData(ServerConnectionState.Crashed, "crashed")]
    public void BuildPublic_ReflectsConnectionState(ServerConnectionState connectionState, string expectedWord)
    {
        var state = new ServerState(
            "TestServer", "/srv/config", "/srv/logs", null,
            connectionState, null, null, 0);

        var text = ServerStateFormatter.BuildPublic(state);

        Assert.Contains(expectedWord, text);
    }

    [Fact]
    public void BuildChangeNotification_HasHeaderAndOnlyTheStateLine()
    {
        var state = new ServerState(
            "TestServer",
            "/srv/config",
            "/srv/logs",
            "/srv/logs/PerkLog.txt",
            ServerConnectionState.Running,
            DateTime.UtcNow,
            "PlayerLoggedInEvent",
            3);

        var text = ServerStateFormatter.BuildChangeNotification(state);

        Assert.Contains("#### SERVER STATUS CHANGED ####", text);
        Assert.Contains("State: Running", text);
        Assert.DoesNotContain("Server:", text);
        Assert.DoesNotContain("Perk log", text);
        Assert.DoesNotContain("Last event", text);
        Assert.DoesNotContain("Known players", text);
    }

    [Theory]
    [InlineData(ServerConnectionState.Starting, "⏳")]
    [InlineData(ServerConnectionState.Stopping, "⏳")]
    [InlineData(ServerConnectionState.Running, "🟢")]
    [InlineData(ServerConnectionState.Offline, "🔴")]
    [InlineData(ServerConnectionState.Crashed, "⚠️")]
    public void BuildChangeNotification_UsesTheUnifiedIconPerState(
        ServerConnectionState connectionState, string expectedIcon)
    {
        var state = new ServerState(
            "TestServer", "/srv/config", "/srv/logs", null,
            connectionState, null, null, 0);

        var text = ServerStateFormatter.BuildChangeNotification(state);

        Assert.Contains(expectedIcon, text);
    }
}
