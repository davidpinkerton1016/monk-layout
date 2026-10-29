# Monk and White Mage Layouts

A personal Dalamud plugin that applies compact level 100 Monk and White Mage HUD and keyboard presets. Open `/monklayout` for Monk or `/whmlayout` for White Mage, review the preview, and press **Apply**. Each job has its own first local backup and restore index.

## Installation

1. Open `/xlsettings` and select the tab containing **Custom Plugin Repositories**.
2. Add the URL below, enable its entry, and save.
3. Open `/xlplugins`, search for **Monk & White Mage Layouts**, and install it. An existing Monk Layout installation receives the same update.
4. Switch to your level 100 job in a town. Open `/monklayout` or `/whmlayout` and press the corresponding **Apply** button.

Repository URL:

```text
https://raw.githubusercontent.com/davidpinkerton1016/monk-layout/main/repo.json
```

Dalamud handles the plugin installation and updates. The setup window includes a skill preview and an option to keep your existing keyboard bindings.

## Version 1.3.0.0

White Mage uses HUD 3, a centered lily gauge, a party list nearer the center-left, and the compact spacing from the Monk preset. Damage uses 1-3, lily heals use 4-5, and emergency healing, Swiftcast, movement and Esuna sit on nearby keys. All 35 base, role and common combat actions are present. Cure, Medica and Repose remain visible for clicking; the other 32 actions have keyboard shortcuts.

Monk keeps HUD 4. Both presets use the same global hotbar keyboard map, including X for movement, Shift+X for knockback protection and V for Sprint. White Mage's original backup and restore index live in a separate job folder. Backups from earlier Monk versions remain readable.

Update the existing plugin through `/xlplugins`, switch to White Mage, run `/whmlayout`, and choose **Apply White Mage layout**. Confirm **1.3.0.0** in the window. The repository URL remains the same.

The game automatically loads each job's saved hotbar contents. Select HUD 3 for White Mage and HUD 4 for Monk through the game's HUD layout selector when changing between them; the plugin does not automatically switch HUD slots.

### Version 1.2.0.0

The compact preset reduces hotbar row spacing from 78 to 54 units at 100% effective scale. HP and Monk gauges sit closer to the bars, and the two gauges share a consistent scaled gap. The **Row spacing** slider starts at 5; use a lower value for tighter rows or a higher value for more room, then press **Apply Monk layout**.

Update Monk Layout through `/xlplugins`, confirm **1.2.0.0** in the setup window, and apply again to use the new spacing. The first original-settings backup remains available through **Restore original settings**.

### Version 1.1.2.0

Apply now saves the live HUD through the game's layout switch before checking stored HUD entries. The earlier check ran before that save and could stop at a missing `_ActionBar` entry. The plugin returns to the starting layout before capturing its backup. Restore no longer requires the current layout to contain all preset elements.

### Version 1.1.1.0

Job detection now reads the active character instead of relying on the separate PlayerState service. A missing or stale PlayerState job no longer rejects a logged-in Monk. When its character ID is unavailable, the loaded HUD and hotbar file identities must agree before any backup or edit. The window shows the version and detected login, job, level and player-data state. Login, loading and incorrect-job errors now have separate messages.

## Layout

- HUD 4 holds the Monk preset; HUD 3 holds the White Mage preset.
- Three closely stacked horizontal hotbars sit at the bottom center, with HP/MP and the job gauge above.
- Monk uses paired form attacks on 1-3 and Shift+1-3, with AoE on Ctrl+1-3.
- White Mage uses damage on 1-3 and instant lily heals on 4-5, with its party list closer to the character.
- The [Monk keyboard reference](KEYBOARD_REFERENCE.md) and [White Mage keyboard reference](WHITE_MAGE_KEYBOARD_REFERENCE.md) list every assignment.

Hotbars 1–3 become unshared, which is a character-wide setting. Optional keyboard bindings affect all jobs. Movement keys and mouse bindings are protected. Other jobs' saved hotbar contents remain intact.

## Compatibility and validation

This release targets Dalamud API 15, Dalamud 15.0.3.9, and game version 2026.10.16.0000.0000. A native-library compatibility check stops layout edits when the installed UI structures differ. Native game updates can require a new build.

The plugin compiles and its portable checks pass, including action coverage, shared keyboard assignments, job checks, separate backup identities and compatibility with legacy Monk backups. The package and repository index are validated against the current Dalamud manifest format. The Monk preset through 1.2.0.0 was reported to apply successfully in game. The White Mage preset in 1.3.0.0 has not been tested in a live FFXIV session.

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

References: [Dalamud custom repository format](https://dalamud.dev/plugin-publishing/custom-repositories/), [Monk action guide](https://na.finalfantasyxiv.com/jobguide/monk/), [White Mage action guide](https://na.finalfantasyxiv.com/jobguide/whitemage/).
