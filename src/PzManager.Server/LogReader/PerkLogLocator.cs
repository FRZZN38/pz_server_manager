namespace PzManager.Server.LogReader;

public sealed class PerkLogLocator
{
    private const string DefaultPattern = "*PerkLog*";

    public string Locate(
        string directory,
        DateTime serverStartedAt)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(
                "Perk log directory is required.",
                nameof(directory));

        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(
                $"Perk log directory does not exist: {directory}");

        var file = Directory
            .GetFiles(directory, DefaultPattern)
            .Select(path => new FileInfo(path))
            .Where(file => file.LastWriteTimeUtc >= serverStartedAt)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name)
            .FirstOrDefault();

        if (file is null)
        {
            throw new FileNotFoundException(
                $"No current PerkLog file was found in: {directory}");
        }

        return file.FullName;
    }
}