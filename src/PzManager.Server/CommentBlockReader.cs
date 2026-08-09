namespace PzManager.Server;

/// <summary>
/// Both ZomboidServer.ini (# comments) and SandboxVars.lua (-- comments)
/// document each field with one or more comment lines directly above it.
/// Shared here since the extraction logic is identical for both, just with
/// a different prefix.
/// </summary>
internal static class CommentBlockReader
{
    public static string? ExtractPrecedingComment(string[] lines, int matchIndex, string commentPrefix)
    {
        var commentLines = new List<string>();
        var i = matchIndex - 1;

        while (i >= 0)
        {
            var trimmed = lines[i].Trim();

            if (!trimmed.StartsWith(commentPrefix, StringComparison.Ordinal))
                break;

            commentLines.Insert(0, trimmed[commentPrefix.Length..].Trim());
            i--;
        }

        return commentLines.Count == 0 ? null : string.Join(Environment.NewLine, commentLines);
    }
}
