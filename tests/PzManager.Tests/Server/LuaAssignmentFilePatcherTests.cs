using PzManager.Server;

namespace PzManager.Tests.Server;

public sealed class LuaAssignmentFilePatcherTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lua-patcher-{Guid.NewGuid():N}.lua");
    private readonly LuaAssignmentFilePatcher _patcher = new();

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private static readonly string[] SampleFile =
    [
        "SandboxVars = {",
        "    VERSION = 6,",
        "    -- Comment",
        "    Zombies = 5,",
        "    ZombieLore = {",
        "        Speed = 3,",
        "        Strength = 3,",
        "    },",
        "    MultiplierConfig = {",
        "        Global = 1.0,",
        "    },",
        "}"
    ];

    [Fact]
    public void ApplyOverrides_ChangesTopLevelAndNestedKeysByDottedPath()
    {
        File.WriteAllLines(_path, SampleFile);

        _patcher.ApplyOverrides(_path, new Dictionary<string, string>
        {
            ["SandboxVars.Zombies"] = "1",
            ["SandboxVars.ZombieLore.Speed"] = "1"
        });

        var lines = File.ReadAllLines(_path);

        Assert.Equal("    Zombies = 1,", lines[3]);
        Assert.Equal("        Speed = 1,", lines[5]);
        Assert.Equal("        Strength = 3,", lines[6]);
        Assert.Equal("        Global = 1.0,", lines[9]);
        Assert.Equal("    VERSION = 6,", lines[1]);
    }

    [Fact]
    public void ApplyOverrides_DoesNotConfuseKeysWithSameNameInDifferentTables()
    {
        File.WriteAllLines(_path, SampleFile);

        _patcher.ApplyOverrides(_path, new Dictionary<string, string>
        {
            ["SandboxVars.ZombieLore.Strength"] = "5"
        });

        var lines = File.ReadAllLines(_path);

        Assert.Equal("        Strength = 5,", lines[6]);
    }

    [Fact]
    public void ApplyOverrides_WithEmptyOverrides_LeavesFileUntouched()
    {
        File.WriteAllLines(_path, SampleFile);

        _patcher.ApplyOverrides(_path, new Dictionary<string, string>());

        Assert.Equal(SampleFile, File.ReadAllLines(_path));
    }

    [Fact]
    public void ApplyOverrides_WithUnknownKey_ThrowsAndLeavesFileUntouched()
    {
        File.WriteAllLines(_path, SampleFile);

        Assert.Throws<InvalidOperationException>(() =>
            _patcher.ApplyOverrides(_path, new Dictionary<string, string>
            {
                ["SandboxVars.DoesNotExist"] = "true"
            }));

        Assert.Equal(SampleFile, File.ReadAllLines(_path));
    }

    [Fact]
    public void ApplyOverrides_UnprefixedKey_IsTreatedAsUnknown()
    {
        File.WriteAllLines(_path, SampleFile);

        Assert.Throws<InvalidOperationException>(() =>
            _patcher.ApplyOverrides(_path, new Dictionary<string, string>
            {
                ["Zombies"] = "1"
            }));
    }

    [Fact]
    public void TryGetValue_WithNestedDottedKey_ReturnsItsCurrentValue()
    {
        File.WriteAllLines(_path, SampleFile);

        var found = _patcher.TryGetValue(_path, "SandboxVars.ZombieLore.Speed", out var value);

        Assert.True(found);
        Assert.Equal("3", value);
    }

    [Fact]
    public void TryGetValue_DoesNotConfuseKeysWithSameNameInDifferentTables()
    {
        File.WriteAllLines(_path, SampleFile);

        var found = _patcher.TryGetValue(_path, "SandboxVars.MultiplierConfig.Global", out var value);

        Assert.True(found);
        Assert.Equal("1.0", value);
    }

    [Fact]
    public void TryGetValue_WithUnknownKey_ReturnsFalse()
    {
        File.WriteAllLines(_path, SampleFile);

        var found = _patcher.TryGetValue(_path, "SandboxVars.DoesNotExist", out var value);

        Assert.False(found);
        Assert.Equal("", value);
    }

    [Fact]
    public void TryGetValue_WithComment_ReturnsThePrecedingCommentLines()
    {
        File.WriteAllLines(_path, SampleFile);

        var found = _patcher.TryGetValue(_path, "SandboxVars.Zombies", out var value, out var comment);

        Assert.True(found);
        Assert.Equal("5", value);
        Assert.Equal("Comment", comment);
    }

    [Fact]
    public void TryGetValue_WithMultiLineComment_JoinsAllPrecedingLines()
    {
        File.WriteAllLines(_path,
        [
            "SandboxVars = {",
            "    -- Controls zombie speed. Default=Random",
            "    -- 1 = Sprinters",
            "    -- 2 = Random",
            "    Speed = 3,",
            "}"
        ]);

        var found = _patcher.TryGetValue(_path, "SandboxVars.Speed", out _, out var comment);

        Assert.True(found);
        Assert.Equal(
            "Controls zombie speed. Default=Random" + Environment.NewLine
            + "1 = Sprinters" + Environment.NewLine
            + "2 = Random",
            comment);
    }

    [Fact]
    public void TryGetValue_WithNoPrecedingComment_ReturnsNullComment()
    {
        File.WriteAllLines(_path, SampleFile);

        var found = _patcher.TryGetValue(_path, "SandboxVars.MultiplierConfig.Global", out _, out var comment);

        Assert.True(found);
        Assert.Null(comment);
    }
}
