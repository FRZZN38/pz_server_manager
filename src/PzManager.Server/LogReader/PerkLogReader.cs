using System.Globalization;

namespace PzManager.Server.LogReader;

public sealed class PerkLogReader
{
    private const string TimestampFormat = "dd-MM-yy HH:mm:ss.fff";

    public async IAsyncEnumerable<PerkLogEntry> Read(
        Stream stream,
        bool readExisting = false)
    {
        if (!readExisting)
            stream.Seek(0, SeekOrigin.End);

        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync();

            if (line is null)
            {
                await Task.Delay(500);
                continue;
            }

            if (!TryParseTimestamp(line, out var timestamp))
                continue;

            yield return new PerkLogEntry(timestamp, line);
        }
    }

    private static bool TryParseTimestamp(
        string line,
        out DateTime timestamp)
    {
        timestamp = default;

        var end = line.IndexOf(']');

        if (end <= 1)
            return false;

        var value = line.Substring(1, end - 1);

        return DateTime.TryParseExact(
            value,
            TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out timestamp);
    }
}