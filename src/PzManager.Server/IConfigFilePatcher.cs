namespace PzManager.Server;

/// <summary>
/// Applies a set of field overrides to an existing config file in place,
/// touching only the keys present in <paramref name="overrides"/> and
/// leaving everything else - including fields the Project Zomboid server
/// itself manages, such as ResetID, Seed or ServerPlayerID - untouched.
/// </summary>
internal interface IConfigFilePatcher
{
    void ApplyOverrides(string path, IReadOnlyDictionary<string, string> overrides);

    /// <summary>
    /// Reads the current value of a single field, exactly as written in the
    /// file (no parsing/unquoting). Returns false if the key isn't found.
    /// </summary>
    bool TryGetValue(string path, string key, out string value);

    /// <summary>
    /// Same as <see cref="TryGetValue(string, string, out string)"/>, plus
    /// the comment block immediately above the field (the game's own
    /// explanation of what it does), if any - null if the field has no
    /// comment directly preceding it, or isn't found at all.
    /// </summary>
    bool TryGetValue(string path, string key, out string value, out string? comment);
}
