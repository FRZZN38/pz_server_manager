using PzManager.Server.LogReader;

namespace PzManager.Tests.Server.LogReader;

public sealed class PerkLogLocatorTests
{
    [Fact]
    public void Locate_ReturnsMostRecentlyUpdatedPerkLog()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var oldFile = Path.Combine(root, "olderPerkLog.txt");
            var newFile = Path.Combine(root, "newerPerkLog.txt");

            File.WriteAllText(oldFile, "old");
            File.WriteAllText(newFile, "new");

            File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(newFile, DateTime.UtcNow);

            var locator = new PerkLogLocator();

            var result = locator.Locate(root);

            Assert.Equal(newFile, result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Locate_ThrowsWhenNoPerkLogExists()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var locator = new PerkLogLocator();

            Assert.Throws<FileNotFoundException>(() => locator.Locate(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
