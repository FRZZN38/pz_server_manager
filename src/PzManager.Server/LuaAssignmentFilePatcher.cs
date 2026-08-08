using System.Text.RegularExpressions;

namespace PzManager.Server;

/// <summary>
/// Applies overrides to Lua files that follow the "SandboxVars.lua" shape:
/// a tree of <c>Key = value,</c> assignments, possibly nested inside
/// <c>Key = { ... }</c> tables (Basement, ZombieLore, etc.).
/// Override keys are dotted paths matching that nesting, e.g.
/// "SandboxVars.ZombieLore.Speed" or "SandboxVars.ZombieConfig.PopulationMultiplier".
/// Only matching assignment lines are rewritten - the surrounding structure,
/// comments and every other key are left untouched.
/// </summary>
internal sealed partial class LuaAssignmentFilePatcher : IConfigFilePatcher
{
    [GeneratedRegex(@"^(\s*)([A-Za-z_]\w*)\s*=\s*\{\s*$")]
    private static partial Regex OpenTableRegex();

    [GeneratedRegex(@"^\s*\}\s*,?\s*$")]
    private static partial Regex CloseTableRegex();

    [GeneratedRegex(@"^(\s*)([A-Za-z_]\w*)\s*=\s*(.*?)\s*$")]
    private static partial Regex AssignmentRegex();

    public void ApplyOverrides(string path, IReadOnlyDictionary<string, string> overrides)
    {
        if (overrides.Count == 0)
            return;

        var lines = File.ReadAllLines(path);
        var pending = new Dictionary<string, string>(overrides, StringComparer.Ordinal);
        var pathStack = new List<string>();

        for (var i = 0; i < lines.Length && pending.Count > 0; i++)
        {
            var line = lines[i];

            var openMatch = OpenTableRegex().Match(line);

            if (openMatch.Success)
            {
                pathStack.Add(openMatch.Groups[2].Value);
                continue;
            }

            if (CloseTableRegex().IsMatch(line))
            {
                if (pathStack.Count > 0)
                    pathStack.RemoveAt(pathStack.Count - 1);

                continue;
            }

            var assignMatch = AssignmentRegex().Match(line);

            if (!assignMatch.Success)
                continue;

            var indent = assignMatch.Groups[1].Value;
            var key = assignMatch.Groups[2].Value;
            var remainder = assignMatch.Groups[3].Value;

            var dottedKey = pathStack.Count == 0
                ? key
                : string.Join('.', pathStack) + "." + key;

            if (!pending.TryGetValue(dottedKey, out var newValue))
                continue;

            var comma = remainder.EndsWith(',') ? "," : "";

            lines[i] = $"{indent}{key} = {newValue}{comma}";
            pending.Remove(dottedKey);
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
        var pathStack = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            var openMatch = OpenTableRegex().Match(line);

            if (openMatch.Success)
            {
                pathStack.Add(openMatch.Groups[2].Value);
                continue;
            }

            if (CloseTableRegex().IsMatch(line))
            {
                if (pathStack.Count > 0)
                    pathStack.RemoveAt(pathStack.Count - 1);

                continue;
            }

            var assignMatch = AssignmentRegex().Match(line);

            if (!assignMatch.Success)
                continue;

            var lineKey = assignMatch.Groups[2].Value;
            var remainder = assignMatch.Groups[3].Value;

            var dottedKey = pathStack.Count == 0
                ? lineKey
                : string.Join('.', pathStack) + "." + lineKey;

            if (!string.Equals(dottedKey, key, StringComparison.Ordinal))
                continue;

            value = remainder.EndsWith(',') ? remainder[..^1] : remainder;
            comment = CommentBlockReader.ExtractPrecedingComment(lines, i, "--");
            return true;
        }

        value = "";
        comment = null;
        return false;
    }
}
