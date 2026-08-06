namespace PzManager.Server;

/// <summary>
/// Resolves file paths relative to the PzManager.Server project's own source
/// directory (found by walking up from the running executable to the
/// repository's .sln file), rather than to whichever project's build output
/// happens to be running. This guarantees a single, stable install location
/// (Scripts, Templates, Data) regardless of which app hosts ServerManager.
/// </summary>
internal static class ServerPaths
{
    public static string RootDirectory { get; } = LocateRoot();

    public static string ScriptsDirectory =>
        Path.Combine(RootDirectory, "Scripts");

    public static string TemplatesDirectory =>
        Path.Combine(RootDirectory, "Templates");

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
