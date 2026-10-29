# Monk Layout

A personal Dalamud plugin that applies a level 100 Monk HUD and keyboard hotbar layout. Open `/monklayout`, review the skill preview, and choose **Apply Monk layout**. **Restore original settings** returns the affected settings to their first local backup.

## Installation

1. Open `/xlsettings` and select the tab containing **Custom Plugin Repositories**.
2. Add the URL below, enable its entry, and save.
3. Open `/xlplugins`, search for **Monk Layout**, and install it.
4. Switch to your level 100 Monk in a town, open `/monklayout`, and select **Apply Monk layout**.

Repository URL:

```text
https://raw.githubusercontent.com/davidpinkerton1016/monk-layout/main/repo.json
```

Dalamud handles the plugin installation and updates. The setup window includes a skill preview and an option to keep your existing keyboard bindings.

## Version 1.1.2.0

Apply now saves the live HUD through the game's layout switch before checking stored HUD entries. The earlier check ran before that save and could stop at a missing `_ActionBar` entry. The plugin returns to the starting layout before capturing its backup. Restore no longer requires the current layout to contain all preset elements.

To update an existing installation, open `/xlplugins` and update Monk Layout. Confirm the setup window shows **1.1.2.0**, then try applying again.

### Version 1.1.1.0

Job detection now reads the active character instead of relying on the separate PlayerState service. A missing or stale PlayerState job no longer rejects a logged-in Monk. When its character ID is unavailable, the loaded HUD and hotbar file identities must agree before any backup or edit. The window shows the version and detected login, job, level and player-data state. Login, loading and incorrect-job errors now have separate messages.

## Layout

- HUD layout 4 holds three horizontal hotbars at the bottom center.
- The Monk gauges and HP appear above the hotbars.
- Paired single-target form attacks use 1–3 and Shift+1–3.
- AoE attacks use Ctrl+1–3.
- Perfect Balance, Masterful Blitz and burst abilities sit on nearby keys.
- [The keyboard reference](KEYBOARD_REFERENCE.md) lists every assignment.

Hotbars 1–3 become unshared, which is a character-wide setting. Optional keyboard bindings affect all jobs. Movement keys and mouse bindings are protected. Other jobs' saved hotbar contents remain intact.

## Compatibility and validation

This release targets Dalamud API 15, Dalamud 15.0.3.9, and game version 2026.10.16.0000.0000. A native-library compatibility check stops layout edits when the installed UI structures differ. Native game updates can require a new build.

The plugin compiles and its portable checks pass. The package and repository index are validated against the current Dalamud manifest format. A live FFXIV session was unavailable, so native HUD rendering and end-to-end in-game behavior remain untested.

Backups stay on the player's computer inside the plugin configuration directory. The plugin does not upload settings or make network requests.

## Repository publishing

The repository owner publishes these files publicly:

- `repo.json`, the standard Dalamud repository index
- `latest.zip`, containing the DLL and plugin manifest at the archive root
- Source, tests, documentation and the packaging helper

The packaging helper regenerates the two installable files from the compiled DLL and manifest. Run it before publishing a new version:

```sh
python3 tools/package_repository.py --base-url https://raw.githubusercontent.com/davidpinkerton1016/monk-layout/main --repo-url https://github.com/davidpinkerton1016/monk-layout
```

Future updates need a higher assembly version in both the compiled DLL and manifest, followed by regenerating `latest.zip` and `repo.json`.

## Development

Build with the .NET 10 SDK and the host libraries from the matching installed Dalamud release:

```powershell
dotnet build Source/MonkLayout.csproj -c Release -p:DalamudPath=C:\path\to\Dalamud
dotnet run --project Tests/CoreChecks.csproj -c Release
```

The repository excludes launcher configuration, character settings, backups, game assets, and host libraries.

References: [Dalamud custom repository format](https://dalamud.dev/plugin-publishing/custom-repositories/), [Monk action guide](https://na.finalfantasyxiv.com/jobguide/monk/).
