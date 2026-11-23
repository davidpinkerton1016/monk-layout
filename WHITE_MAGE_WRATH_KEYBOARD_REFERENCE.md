# White Mage layout for Wrath Combo

Version 1.4.0.1. Update **Monk & White Mage Layouts** in `/xlplugins`, switch to level 100 White Mage in a town, then open `/whmwrath` and choose **Apply White Mage Wrath layout**.

This profile uses HUD 3 and White Mage hotbars 1-3. It keeps the compact spacing, centered lily gauge and nearby party list from the existing White Mage profile. Both White Mage profiles share the same original backup and restore history. `/whmlayout` still opens the manual profile.

## Key map

| Key | Normal | Shift | Ctrl |
| --- | --- | --- | --- |
| 1 | Glare III, Wrath single-target damage | Glare IV | Medica III |
| 2 | Holy III, Wrath AoE damage | Afflatus Misery | Cure III |
| 3 | Cure, Wrath single-target healing | Presence of Mind | Plenary Indulgence |
| 4 | Medica, Wrath AoE healing | Tetragrammaton | Temperance |
| 5 | Afflatus Solace | Cure II | Divine Caress |
| R | Afflatus Rapture | Asylum | Liturgy of the Bell |
| F | Regen | Divine Benison | Rescue |
| G | Swiftcast | Thin Air | Limit Break |
| Z | Benediction | Aquaveil | Dia, click only |
| X | Aetherial Shift | Surecast | Assize, click only |
| C | Esuna | Raise | Repose, click only |
| V | Sprint | Lucid Dreaming | Empty |

The four combo buttons work as manual backups when Wrath Auto-Rotation is paused. Auto-Rotation can execute enabled combos without pressing those keys. Cure and Medica are the actual base healing buttons Wrath replaces. Cure II and Medica II are different buttons.

All 35 White Mage, healer-role and common combat actions remain available. There are 32 bound actions and three click-only actions. Ctrl+Z, Ctrl+X, Ctrl+C and Ctrl+V retain their existing functions. The stored base actions Stone, Aero, Holy and Medica II upgrade through the game's traits.

## Configure Wrath separately

Applying this profile arranges the hotbars and HUD. It does not configure Wrath or enable automatic casting. The setup window provides buttons to open Wrath settings, open White Mage settings, pause Auto-Rotation and resume it.

For the Wrath Combo 1.0.4.27 setup audited for this profile, enable each of these parent combos and its **Auto-Mode** checkbox:

1. **Advanced DPS Mode - Single Target**, using **On Stones/Glares**.
2. **Simple DPS Mode - AoE**.
3. **Advanced Healing Mode - Single Target**.
4. **Advanced Healing Mode - AoE**.

Disable the other Simple or Advanced version in each category. Simple AoE is intentional: the audited Advanced AoE damage implementation exposes a Lucid Dreaming option but never calls it. Simple AoE includes automatic Lucid Dreaming at 7,500 MP.

### Reserve Swiftcast for Raise

- Enable **Auto-Resurrect** and **Require Swiftcast/Dualcast** in Wrath Auto-Rotation settings.
- Keep **Swift Holy Pull Opener Option** off, including if you later switch to Advanced AoE damage.
- Leave **Alternative Raise Feature** off to keep G as a standalone Swiftcast button and Shift+C as Raise.
- The recommended damage and healing combos do not intentionally activate Swiftcast for other spells. Balance Opener also contains no Swiftcast use, but disabling the opener gives Auto-Resurrect access during the opening sequence.

Wrath has no pending-Raise lock. If the situation changes after Swiftcast activates, another spell can consume its buff. For a deliberate manual raise, pause Wrath, select the dead party member, use G then Shift+C, and resume when the raise completes.

### Manual controls

Keep V for Sprint, X for Aetherial Shift, Shift+X for Surecast, Ctrl+F for Rescue and Ctrl+G for Limit Break. The audited Wrath White Mage routines do not automatically decide when to use those abilities. Keep Z for Benediction, C for Esuna and Shift+C for Raise for manual recovery.

Pause with `/wrath auto off`; resume with `/wrath auto on`. `/wrath combo off` only disables action replacement and does not stop Auto-Rotation. The plugin's Pause and Resume buttons send the explicit on/off commands.

## Applying and restoring

Use a town outside duties, combat and level sync. Close the game's HUD and configuration editors first. Leave **Apply recommended keybinds** checked to use the table above, or clear it to keep your current bindings.

Applying either White Mage profile replaces the arrangement in White Mage's saved hotbars and HUD 3. Both profiles retain the same first original backup. Select **Restore original settings** to restore that backup, or reapply the manual profile through `/whmlayout` to get the earlier manual layout.

Hotbar sharing for bars 1-3 and keyboard bindings are character-wide settings. Other jobs' saved hotbar contents remain intact. Monk still uses HUD 4. The plugin does not switch HUD slots automatically when changing jobs.

## Verification and sources

The code is compiled and checked locally. Live FFXIV behavior still needs an in-game check on the user's character. The profile contains no rotation engine, action hooks or automatic combat actions of its own.

- [Wrath White Mage action implementation, release 1.0.4.27](https://github.com/PunishXIV/WrathCombo/blob/1.0.4.27/WrathCombo/Combos/PvE/WHM/WHM.cs)
- [Wrath preset definitions](https://github.com/PunishXIV/WrathCombo/blob/1.0.4.27/WrathCombo/Combos/CustomComboPreset.cs)
- [Wrath Auto-Rotation controller](https://github.com/PunishXIV/WrathCombo/blob/1.0.4.27/WrathCombo/AutoRotation/AutoRotationController.cs)
- [Official White Mage job guide](https://na.finalfantasyxiv.com/jobguide/whitemage/)
