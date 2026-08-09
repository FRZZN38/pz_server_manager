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
- `ProjectZomboid:Password` — join password required to connect to the server. Written straight
  into the server's `.ini` (`Password=`) by `ServerConfigProvisioner`, never passed as a process
  argument — the dedicated server binary doesn't have a `-password` flag, and secrets shouldn't go
  through `argv` anyway (visible in `ps`, journalctl, `/proc/<pid>/cmdline`).
- `ProjectZomboid:PublicName` — name shown in the in-game/Steam server browser. Also written into
  the `.ini`, same mechanism as `Password`.
- `ProjectZomboid:MaxMemory` — JVM `-Xmx` heap cap enforced on `ProjectZomboid64.json` before every
  launch (default `5g`). Reapplied every time because that file lives inside the SteamCMD install
  and `app_update ... validate` (install/update scripts) can silently reset it.
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
(`Offline`, `Starting`, `Running`, `Stopping`, `Crashed`, `Updating`, `Creating`), reported to the
admin Discord channel every time it actually changes (not on every minor event, e.g. a player
logging in doesn't spam the channel).

- **Bootstrap on a brand-new server**: if no config file exists yet, `PzManager` starts the real
  server once, waits for it to report `*** SERVER STARTED ***` (confirming it booted correctly,
  not just that it wrote a file), then stops it gracefully before applying the predefined config
  and starting it for real. This lets the game itself generate the `.ini`/`.lua` defaults for the
  exact version running, instead of relying on a checked-in template that could drift out of date.
  Shows as `Creating` while it's happening - both at `pzmanager` startup and from `/start` (so a
  fresh install doesn't need a full service restart to trigger it).
- **Graceful shutdown**: `/stop`, `/restart`, or stopping the manager (Ctrl+C, SIGTERM from
  systemd/Docker) sends `save` + `quit` and waits up to 30s for the process to exit on its own
  before killing it.
- **Restart** is literally stop-then-start (`Stopping → Offline → Starting → Running`) reusing the
  same paths as the standalone `/stop`/`/start` — there's no separate "restarting" state.
- **Crash recovery**: if the server exits unexpectedly (or fails to start), the manager marks it
  `Crashed` and restarts it automatically with an exponential backoff (5s, 10s, 20s, ... capped at
  60s). The backoff resets after the server has been running healthily for a couple of minutes, or
  on any explicit start (initial launch or restart). `Crashed` is also where a failed bootstrap
  (`Creating`) or a failed `/update_server` (`Updating`) ends up - those aren't retried
  automatically, but `/start` and `/update_server` both accept `Crashed` (in addition to `Offline`)
  so an admin can just try again. `save`/`broadcast`/`kick`/`start`/`restart`/`stop`/`update_server`
  all refuse to run while `Creating`/`Updating` are in progress, since those two own the underlying
  process directly and aren't safe to interrupt.

## Server configuration (ini / sandbox vars)

Config files (`.ini`, `SandboxVars.lua`) are **not** copied from a checked-in template. The real
Project Zomboid server generates them itself (see bootstrap above), and `ServerConfigProvisioner`
patches only specific fields on top — everything else, including server-managed fields like
`ResetID`, `Seed` or `ServerPlayerID`, is left exactly as the game wrote it.

- `src/PzManager.Server/Overrides/ZomboidServer.overrides.json` and
  `ZomboidServer_SandboxVars.overrides.json` — checked-in `field: value` maps re-applied on every
  startup (`set_config`/`SetPredefinedConfig`). SandboxVars keys are dotted paths matching the
  file's nesting, e.g. `SandboxVars.ZombieLore.Speed`.
- Both files are patched line-by-line, touching only the listed keys — safe to run repeatedly, and
  never risks clobbering fields the server itself manages.
- `spawnpoints.lua` and `spawnregions.lua` are left untouched entirely: they're lists of tables
  with no stable per-entry key, so field-level patching doesn't apply to them.

## Discord commands

Player commands (available to anyone, responses are private/ephemeral):
- `/help` — show available commands.
- `/status` — server status (online/offline/etc. and player count only, no internal detail).
- `/player <username>` — show a summary for a player.

Admin commands (restricted to server administrators, usable only in the admin channel):
- `/save` — save the world immediately.
- `/broadcast <message>` — send a message to every connected player.
- `/kick <username>` — kick a connected player.
- `/start` — start the Project Zomboid server.
- `/restart` — restart the Project Zomboid server.
- `/stop` — stop the Project Zomboid server.
- `/admin_status` — full server status (state, perk log path, last event, player count).
- `/admin_help` — list admin commands (generated from the same definitions registered with
  Discord, so it can't drift out of sync).
- `/set_config [file] [param_name] [value]` — set one field (`param_name` + `value`; `file` is
  optional, both config files get searched for the field if omitted), one file's predefined
  overrides (`file` only), or all predefined overrides (no options).
- `/get_config [file] [param_name]` — same 3-tier shape as `/set_config`, read-only. With no
  `param_name`, returns the file(s) as attachment(s) instead of a single value.
- `/update_server` — update the dedicated server via SteamCMD (`app_update ... validate`). Refuses
  to run while the server is up (`/stop` first); shows as `Updating` in `/admin_status` meanwhile.
  SteamCMD only touches the install dir (`Data/pz-server`), not the per-server `.ini`/`.lua`
  (those live under `ConfigDirectory`), so afterwards only the `MaxMemory` cap is re-applied -
  `ProjectZomboid64.json` lives inside the install dir too and SteamCMD can silently reset it.
  `/start` bootstraps a brand new install (and applies the predefined overrides + `MaxMemory` cap)
  the first time it's ever run, without requiring a `pzmanager` restart to trigger it - a normal
  `/start` on an already-provisioned server leaves config alone.

Except for `save`/`broadcast`/`kick`/`start`/`restart`/`stop`/`admin_status`/`admin_help`/
`get_config`, which are ephemeral, `set_config`/`update_server` responses are public — if one admin
changes a setting, the others need to see it happened.

`save`/`broadcast`/`kick`/`start`/`restart`/`stop` acknowledge the action immediately and don't
wait for the server to finish; the actual state transitions are reported separately via
`ServerManager.StateChanged`.

## Projects

- PzManager.App
- PzManager.Core
- PzManager.Server
- PzManager.Discord
- PzManager.Tests
