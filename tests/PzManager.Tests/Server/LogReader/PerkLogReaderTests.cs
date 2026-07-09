using System.Text;

using PzManager.Server.LogReader;

using Xunit;

namespace PzManager.Tests.Server.LogReader;

public sealed class PerkLogReaderTests
{
    [Fact]
    public async Task Read_ReturnsSingleEntry()
    {
        var text =
            "[05-07-26 21:55:02.155] [76561198064901882][UserName][6057,5349,0][Level Changed][Woodwork][4][Hours Survived: 0].";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var reader = new PerkLogReader();

        var entries = await ReadAllAsync(reader, stream);

        Assert.Single(entries);

        Assert.Equal(
            new DateTime(2026, 7, 5, 21, 55, 2, 155),
            entries[0].Timestamp);
    }


    [Fact]
    public async Task Read_SkipsInvalidLines()
    {
        var text = """
hello

invalid

[05-07-26 21:55:02.155] [76561198064901882][UserName][6057,5349,0][Level Changed][Woodwork][4][Hours Survived: 0].

""";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var reader = new PerkLogReader();

        var entries = await ReadAllAsync(reader, stream);

        Assert.Single(entries);
    }


    [Fact]
    public async Task Read_SkipsEmptyLines()
    {
        var text = """





[05-07-26 21:55:02.155] [76561198064901882][UserName][6057,5349,0][Level Changed][Woodwork][4][Hours Survived: 0].

""";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var reader = new PerkLogReader();

        var entries = await ReadAllAsync(reader, stream);

        Assert.Single(entries);
    }


    [Fact]
    public async Task Read_ReturnsMultipleEntries()
    {
        var text = """
[05-07-26 21:53:44.705] [76561198064901882][UserName][6057,5349,0][Login][Hours Survived: 0].

[05-07-26 21:55:02.155] [76561198064901882][UserName][6057,5349,0][Level Changed][Woodwork][4][Hours Survived: 0].

""";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var reader = new PerkLogReader();

        var entries = await ReadAllAsync(reader, stream);

        Assert.Equal(2, entries.Count);
    }


    private static async Task<List<PerkLogEntry>> ReadAllAsync(
        PerkLogReader reader,
        Stream stream)
    {
        var result = new List<PerkLogEntry>();

        await foreach (var entry in reader.Read(stream))
        {
            result.Add(entry);
        }

        return result;
    }
}