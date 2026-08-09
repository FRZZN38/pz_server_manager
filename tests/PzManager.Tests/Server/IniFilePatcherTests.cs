using PzManager.Server;

namespace PzManager.Tests.Server;

public sealed class IniFilePatcherTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"ini-patcher-{Guid.NewGuid():N}.ini");
    private readonly IniFilePatcher _patcher = new();

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Fact]
    public void ApplyOverrides_ChangesOnlyListedKeys()
    {
        File.WriteAllLines(_path,
        [
            "# comment",
            "PVP=false",
            "ResetID=9406388",
            "Seed=BFyawrgiOzcjeZkR",
            "MaxPlayers=32"
        ]);

        _patcher.ApplyOverrides(_path, new Dictionary<string, string>
        {
            ["PVP"] = "true",
            ["MaxPlayers"] = "16"
        });

        var lines = File.ReadAllLines(_path);

        Assert.Equal("# comment", lines[0]);
        Assert.Equal("PVP=true", lines[1]);
        Assert.Equal("ResetID=9406388", lines[2]);
        Assert.Equal("Seed=BFyawrgiOzcjeZkR", lines[3]);
        Assert.Equal("MaxPlayers=16", lines[4]);
    }

    [Fact]
    public void ApplyOverrides_WithEmptyOverrides_LeavesFileUntouched()
    {
        var original = new[] { "PVP=false", "ResetID=9406388" };
        File.WriteAllLines(_path, original);

        _patcher.ApplyOverrides(_path, new Dictionary<string, string>());

        Assert.Equal(original, File.ReadAllLines(_path));
    }

    [Fact]
    public void ApplyOverrides_WithUnknownKey_ThrowsAndLeavesFileUntouched()
    {
        var original = new[] { "PVP=false" };
        File.WriteAllLines(_path, original);

        Assert.Throws<InvalidOperationException>(() =>
            _patcher.ApplyOverrides(_path, new Dictionary<string, string>
            {
                ["DoesNotExist"] = "true"
            }));

        Assert.Equal(original, File.ReadAllLines(_path));
    }

    [Fact]
    public void TryGetValue_WithExistingKey_ReturnsItsCurrentValue()
    {
        File.WriteAllLines(_path, ["# comment", "PVP=false", "MaxPlayers=32"]);

        var found = _patcher.TryGetValue(_path, "MaxPlayers", out var value);

        Assert.True(found);
        Assert.Equal("32", value);
    }

    [Fact]
    public void TryGetValue_IsCaseInsensitive()
    {
        File.WriteAllLines(_path, ["PVP=false"]);

        var found = _patcher.TryGetValue(_path, "pvp", out var value);

        Assert.True(found);
        Assert.Equal("false", value);
    }

    [Fact]
    public void TryGetValue_WithUnknownKey_ReturnsFalse()
    {
        File.WriteAllLines(_path, ["PVP=false"]);

        var found = _patcher.TryGetValue(_path, "DoesNotExist", out var value);

        Assert.False(found);
        Assert.Equal("", value);
    }

    [Fact]
    public void TryGetValue_WithComment_ReturnsThePrecedingCommentLines()
    {
        File.WriteAllLines(_path,
        [
            "# Players can hurt and kill other players",
            "PVP=false",
            "MaxPlayers=32"
        ]);

        var found = _patcher.TryGetValue(_path, "PVP", out var value, out var comment);

        Assert.True(found);
        Assert.Equal("false", value);
        Assert.Equal("Players can hurt and kill other players", comment);
    }

    [Fact]
    public void TryGetValue_WithMultiLineComment_JoinsAllPrecedingLines()
    {
        File.WriteAllLines(_path,
        [
            "# Ping limit, in milliseconds, before a player is kicked.",
            "# Set to 0 to disable.",
            "PingLimit=0"
        ]);

        var found = _patcher.TryGetValue(_path, "PingLimit", out _, out var comment);

        Assert.True(found);
        Assert.Equal(
            "Ping limit, in milliseconds, before a player is kicked." + Environment.NewLine + "Set to 0 to disable.",
            comment);
    }

    [Fact]
    public void TryGetValue_WithNoPrecedingComment_ReturnsNullComment()
    {
        File.WriteAllLines(_path, ["PVP=false", "MaxPlayers=32"]);

        var found = _patcher.TryGetValue(_path, "MaxPlayers", out _, out var comment);

        Assert.True(found);
        Assert.Null(comment);
    }
}
