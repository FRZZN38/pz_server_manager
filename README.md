# PzManager

Project Zomboid dedicated server manager.

## Configuration

Use `appsettings.json.example` as the base configuration.

The current `PzManager` setup requires:
- `Discord:Token`
- `Discord:GuildId`
- `Discord:PublicChannelId`
- `Discord:AdminChannelId`
- `ProjectZomboid:ServerName`
- `ProjectZomboid:Admin:Username`
- `ProjectZomboid:Admin:Password`

Optional settings:
- `ProjectZomboid:StartArguments` — extra arguments passed to `start-server.sh`.
- `ProjectZomboid:ConfigDirectory` — where the server's Zomboid cache/config lives. Defaults to
  `Data/Zomboid` relative to the app's executable.
- `ProjectZomboid:PerkLogDirectory` — where PerkLog files are written. Defaults to `Logs` inside
  `ConfigDirectory`. Set this if it points to an existing Project Zomboid install outside of the
  app's own directory.

Any setting can also be provided as an environment variable instead of (or as an override of)
`appsettings.json`, using `__` as the section separator, e.g. `Discord__Token`,
`ProjectZomboid__Admin__Password`. This is the recommended way to supply secrets in production
(systemd unit, Docker, CI) instead of keeping them in a plaintext file.

## Server lifecycle

`PzManager` supervises the Project Zomboid server process and keeps a `ServerConnectionState`
(`Offline`, `Starting`, `Running`, `Restarting`, `Stopping`, `Crashed`), reported to the admin
Discord channel on every change.

- **Graceful shutdown**: stopping the manager (Ctrl+C, or SIGTERM from systemd/Docker) saves the
  world and stops the Project Zomboid server before the process exits.
- **Crash recovery**: if the server exits unexpectedly (or fails to start), the manager marks it
  `Crashed` and restarts it automatically with an exponential backoff (5s, 10s, 20s, ... capped at
  60s). The backoff resets after the server has been running healthily for a couple of minutes.

## Discord commands

Player commands (available to anyone):
- `/help` — show available commands.
- `/player <username>` — show a summary for a player.

Admin commands (restricted to server administrators, usable only in the admin channel):
- `/save` — save the world immediately.
- `/broadcast <message>` — send a message to every connected player.
- `/kick <username>` — kick a connected player.
- `/restart` — restart the Project Zomboid server.
- `/stop` — stop the Project Zomboid server.

## Projects

- PzManager.App
- PzManager.Core
- PzManager.Server
- PzManager.Discord
- PzManager.Tests
