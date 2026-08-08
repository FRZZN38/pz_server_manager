namespace PzManager.Server;

/// <summary>
/// Resolves file paths relative to the PzManager.Server project's own source
/// directory (found by walking up from the running executable to the
/// repository's .sln file), rather than to whichever project's build output
/// happens to be running. This guarantees a single, stable install location
/// (Scripts, Overrides, Data) regardless of which app hosts ServerManager.
/// </summary>
internal static class ServerPaths
{
    public static string RootDirectory { get; } = LocateRoot();

    public static string ScriptsDirectory =>
        Path.Combine(RootDirectory, "Scripts");

    // Only field-override JSON files live here (no full config templates,
    // and nothing sensitive like the server join password - see
    // ServerConfigProvisioner.BootstrapIfNewAsync for why real config files
    // are no longer pre-seeded from a checked-in template).
    public static string OverridesDirectory =>
        Path.Combine(RootDirectory, "Overrides");

    public static string DataDirectory =>
        Path.Combine(RootDirectory, "Data");

    private static string LocateRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "PzManager.Server");

            if (Directory.Exists(candidate)
                && File.Exists(Path.Combine(directory.FullName, "PzManager.sln")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Unable to locate the PzManager.Server project directory. " +
            $"No PzManager.sln was found above {AppContext.BaseDirectory}.");
    }
}
