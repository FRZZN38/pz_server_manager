using PzManager.Core.Logger;

namespace PzManager.Server;

public sealed class ServerConfigProvisioner
{
    private static readonly string[] TemplateFiles =
    [
        "ZomboidServer.ini",
        "ZomboidServer_SandboxVars.lua",
        "ZomboidServer_spawnpoints.lua",
        "ZomboidServer_spawnregions.lua"
    ];

    public void Provision(PzServerSettings settings)
    {
        var templatesDir = ServerPaths.TemplatesDirectory;
        var serverDir = Path.Combine(settings.ConfigDirectory, "Server");

        Directory.CreateDirectory(serverDir);

        foreach (var templateFile in TemplateFiles)
        {
            var templatePath = Path.Combine(templatesDir, templateFile);

            if (!File.Exists(templatePath))
                continue;

            var destinationFileName = templateFile.Replace(
                "ZomboidServer",
                settings.ServerName);

            var destinationPath = Path.Combine(serverDir, destinationFileName);

            if (File.Exists(destinationPath))
            {
                Log.Info($"[SERVER CONFIG] {destinationFileName} already exists. Leaving it untouched.");
                continue;
            }

            File.Copy(templatePath, destinationPath);

            Log.Info($"[SERVER CONFIG] Provisioned {destinationFileName} from template.");
        }
    }
}
