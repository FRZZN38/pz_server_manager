using System.Globalization;
using System.Runtime.CompilerServices;

namespace PzManager.Server.LogReader;

public sealed class PerkLogReader
{
    private const string TimestampFormat = "dd-MM-yy HH:mm:ss.fff";

    public async IAsyncEnumerable<PerkLogEntry> Read(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                await Task.Delay(500, cancellationToken);
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

        return DateTime.TryParseExact(
            line.AsSpan(1, end - 1),
            TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out timestamp);
    }
}