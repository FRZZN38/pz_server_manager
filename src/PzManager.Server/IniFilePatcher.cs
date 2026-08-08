namespace PzManager.Server;

/// <summary>
/// Applies key=value overrides to an existing .ini file in place, touching
/// only the keys listed in the overrides. Every other line (comments, blank
/// lines, and any key not listed) is left byte-for-byte untouched, so
/// server-managed fields such as ResetID, Seed or ServerPlayerID are never
/// at risk of being clobbered.
/// </summary>
internal sealed class IniFilePatcher : IConfigFilePatcher
{
    public void ApplyOverrides(string path, IReadOnlyDictionary<string, string> overrides)
    {
        if (overrides.Count == 0)
            return;

        var lines = File.ReadAllLines(path);
        var pending = new Dictionary<string, string>(overrides, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Length && pending.Count > 0; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (trimmed.Length == 0 || trimmed[0] == '#')
                continue;

            var separatorIndex = line.IndexOf('=');

            if (separatorIndex < 0)
                continue;

            var key = line[..separatorIndex].Trim();

            if (!pending.TryGetValue(key, out var newValue))
                continue;

            lines[i] = $"{key}={newValue}";
            pending.Remove(key);
        }

        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"[SERVER CONFIG] Overrides reference unknown key(s) in {Path.GetFileName(path)}: "
                + string.Join(", ", pending.Keys));
        }

        File.WriteAllLines(path, lines);
    }

    public bool TryGetValue(string path, string key, out string value)
    {
        return TryGetValue(path, key, out value, out _);
    }

    public bool TryGetValue(string path, string key, out string value, out string? comment)
    {
        var lines = File.ReadAllLines(path);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (trimmed.Length == 0 || trimmed[0] == '#')
                continue;

            var separatorIndex = line.IndexOf('=');

            if (separatorIndex < 0)
                continue;

            var lineKey = line[..separatorIndex].Trim();

            if (!string.Equals(lineKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            value = line[(separatorIndex + 1)..];
            comment = CommentBlockReader.ExtractPrecedingComment(lines, i, "#");
            return true;
        }

        value = "";
        comment = null;
        return false;
    }
}
