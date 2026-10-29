using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ActionRow = Lumina.Excel.Sheets.Action;
using GeneralRow = Lumina.Excel.Sheets.GeneralAction;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace MonkLayout;

public sealed record ResolvedAction(ActionSpec Spec, RaptureHotbarModule.HotbarSlotType Type, uint Id);

internal static unsafe class NativeLayout
{
    internal const string StructHash = "F5A2C68CCECD3E199BDA572050E98902B2E7594066C95DC7826C2D4297CD0564";
    internal const string GameVersion = "2026.10.16.0000.0000";
    internal const int EntriesPerLayout = 112;
    private const int TargetLayout = 3;
    private static readonly string[] UiOptions = ["HotbarCommon01", "HotbarCommon02", "HotbarCommon03", "HotbarLock", "HotbarDispRecastTime"];
    private static readonly string[] EssentialHud = ["_ActionBar", "_ActionBar01", "_ActionBar02", "JobHudMNK0", "JobHudMNK1", "_ParameterWidget", "_TargetInfo", "_TargetInfoMainTarget", "_TargetInfoBuffDebuff", "_TargetInfoCastBar"];

    internal static float GetHudGlobalScale(IGameGui gui)
    {
        var addon = gui.GetAddonByName("_ActionBar");
        if (addon.IsNull) throw new InvalidOperationException("The main hotbar is not loaded yet.");
        return ((AtkUnitBase*)addon.Address)->IsScalingWithGlobalUIScale ? AtkUnitBase.GetGlobalUIScale() : 1;
    }

    internal static ulong CheckReady(PlayerSession session, ICondition condition, IGameGui gui, bool require100 = true)
    {
        session.CheckReady(require100);
        if (condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]
            || condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]
            || condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent])
            throw new InvalidOperationException("Apply or restore in a town, outside combat, duties and cutscenes.");
        foreach (var name in new[] { "ConfigAddon", "HudLayout", "ConfigHUD", "ConfigKeybind", "ConfigCharacter" })
        {
            var addon = gui.GetAddonByName(name);
            if (!addon.IsNull && addon.IsVisible) throw new InvalidOperationException("Close the game's HUD and configuration editors first.");
        }
        var fw = NativeFramework.Instance();
        if (fw == null || fw->GameVersionString != GameVersion)
            throw new InvalidOperationException($"This build supports game {GameVersion}. The installed game needs a matching build of Monk Layout.");
        var bars = RaptureHotbarModule.Instance();
        if (bars == null || !bars->ModuleReady || !bars->DatFileLoadedSuccessfully || bars->ActiveHotbarClassJobId != 20 || bars->PvPHotbarsActive)
            throw new InvalidOperationException("Wait for the Monk PvE hotbars to finish loading.");
        var hud = AddonConfig.Instance();
        if (hud == null || !hud->IsLoaded || hud->ActiveDataSet == null || hud->ActiveDataSet->CurrentHudLayout is < 0 or > 3)
            throw new InvalidOperationException("The native HUD configuration has not loaded yet.");
        if (hud->ActiveDataSet->HudLayoutConfigEntries.Length != 448)
            throw new InvalidOperationException("The HUD storage format has changed. Nothing was applied.");
        var input = UIInputData.Instance();
        if (input == null || input->NumKeybinds < 100 || input->NumKeybinds > 4096 || input->Keybinds == null)
            throw new InvalidOperationException("The keyboard configuration has not loaded yet.");
        var characterId = PlayerSession.ResolveContentId(session.StateContentId, hud->CharacterContentId, bars->CharacterContentId);
        if (characterId == 0)
            throw new InvalidOperationException("Monk detected, but the character identity in the HUD and hotbar settings has not finished loading or does not agree. No settings were changed. Wait a moment, then try again.");
        return characterId;
    }

    internal static ulong GetLoadedCharacterId(PlayerSession session)
    {
        if (!session.IsLoggedIn || !session.HasLocalPlayer) return 0;
        var fw = NativeFramework.Instance();
        if (fw == null || fw->GameVersionString != GameVersion) return 0;
        var hud = AddonConfig.Instance();
        var bars = RaptureHotbarModule.Instance();
        if (hud == null || !hud->IsLoaded || bars == null || !bars->ModuleReady || !bars->DatFileLoadedSuccessfully) return 0;
        return PlayerSession.ResolveContentId(session.StateContentId, hud->CharacterContentId, bars->CharacterContentId);
    }

    internal static List<ResolvedAction> ResolveActions(IDataManager data)
    {
        var actions = data.GetExcelSheet<ActionRow>(ClientLanguage.English);
        var general = data.GetExcelSheet<GeneralRow>(ClientLanguage.English);
        var plan = new List<ResolvedAction>();
        foreach (var spec in LayoutPlan.Actions)
        {
            if (spec.Action == null) { plan.Add(new(spec, RaptureHotbarModule.HotbarSlotType.Empty, 0)); continue; }
            uint id;
            if (spec.General)
            {
                var matches = general.Where(a => a.Name.ExtractText() == spec.Action).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException($"Could not resolve {spec.Action} uniquely in the installed game data.");
                id = matches[0].RowId;
            }
            else
            {
                var matches = actions.Where(a => !a.IsPvP && a.ClassJobLevel > 0 && a.ClassJobCategory.RowId != 0
                    && (a.ClassJob.RowId is 2 or 20 || a.IsRoleAction) && a.Name.ExtractText() == spec.Action).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException($"Could not resolve {spec.Action} uniquely in the installed game data. Nothing was changed.");
                id = matches[0].RowId;
            }
            if (id == 0) throw new InvalidOperationException($"The action ID for {spec.Action} is invalid.");
            plan.Add(new(spec, spec.General ? RaptureHotbarModule.HotbarSlotType.GeneralAction : RaptureHotbarModule.HotbarSlotType.Action, id));
        }
        return plan;
    }

    internal static Dictionary<int, Binding> KeyChanges()
    {
        var span = UIInputData.Instance()->GetKeybindSpan();
        var records = new List<Binding>();
        for (var i = 0; i < span.Length; i++)
            records.Add(new(i, ToStroke(span[i].KeySettings[0]), ToStroke(span[i].KeySettings[1])));
        // Include all of the game's movement, camera steering and jump bindings.
        var protectedIds = Enum.GetValues<InputId>().Where(id => id.ToString().StartsWith("MOVE_", StringComparison.Ordinal)
            || id.ToString().Contains("JUMP", StringComparison.Ordinal)).Select(id => (int)id).ToHashSet();
        return LayoutPlan.PlanKeyChanges(records, protectedIds);
    }

    private static Stroke ToStroke(KeySetting key) => new((byte)key.Key, (byte)key.KeyModifier);

    internal static Backup Capture(ulong characterId, IGameConfig config)
    {
        var hud = AddonConfig.Instance();
        var original = hud->ActiveDataSet->CurrentHudLayout;
        // The live HUD can exist before its stored layout is populated. Flush it
        // before checking saved entries. Always return to the starting layout.
        // Restore never requires the current layout to contain these elements.
        try { hud->ChangeHudLayout((uint)(original == 0 ? 1 : 0)); }
        finally { hud->ChangeHudLayout((uint)original); }
        if (hud->ActiveDataSet->CurrentHudLayout != original)
            throw new InvalidOperationException("The game did not return to your starting HUD after saving it. No Monk preset was applied.");
        ValidateSavedHud(original);
        var keys = UIInputData.Instance()->GetKeybindSpan();
        var result = new Backup
        {
            StructHash = StructHash, CharacterId = characterId, OriginalLayout = original,
            HudLayouts = MemoryMarshal.AsBytes(hud->ActiveDataSet->HudLayoutConfigEntries).ToArray(),
            MonkSlots = SavedSlots(20), SharedSlots = SavedSlots(0),
            KeyCount = keys.Length, Keybinds = MemoryMarshal.AsBytes(keys).ToArray()
        };
        foreach (var option in UiOptions)
        {
            if (!config.UiConfig.TryGet(option, out uint value)) throw new InvalidOperationException($"Missing native option: {option}.");
            result.Ui.Add(option, value);
        }
        if (config.UiControl.TryGet("DirectChat", out uint direct)) result.Control.Add("DirectChat", direct);
        ValidateBackup(result, characterId);
        return result;
    }

    private static void ValidateSavedHud(int layout)
    {
        var entries = AddonConfig.Instance()->ActiveDataSet->HudLayoutConfigEntries.Slice(layout * EntriesPerLayout, EntriesPerLayout);
        foreach (var name in EssentialHud)
        {
            if (Find(layout, name) != null) continue;
            var populated = 0;
            foreach (var entry in entries) if (entry.AddonNameHash != 0) populated++;
            // Keep the failed lookup read-only. Never invent records or change
            // native offsets to make an incompatible HUD table appear valid.
            throw new InvalidOperationException($"HUD {layout + 1} still has no saved {name} after automatically saving the live HUD ({populated}/{EntriesPerLayout} entries). No Monk preset was applied.");
        }
    }

    private static SlotBackup[] SavedSlots(int group)
    {
        var bars = RaptureHotbarModule.Instance();
        var list = new List<SlotBackup>();
        for (var b = 0; b < 3; b++) for (var s = 0; s < 12; s++)
        {
            var slot = bars->SavedHotbars[group].Hotbars[b].Slots[s];
            list.Add(new(b, s, (byte)slot.CommandType, slot.CommandId));
        }
        return list.ToArray();
    }

    internal static void ValidateBackup(Backup backup, ulong character)
    {
        if (character == 0 || backup.Schema != 1 || backup.StructHash != StructHash || backup.CharacterId != character
            || backup.OriginalLayout is < 0 or > 3 || backup.HudLayouts.Length != 448 * sizeof(AddonConfigEntry)
            || backup.KeyCount != UIInputData.Instance()->NumKeybinds || backup.Keybinds.Length != backup.KeyCount * sizeof(Keybind))
            throw new InvalidOperationException("The backup does not match this character or the installed game data structures.");
        foreach (var slots in new[] { backup.MonkSlots, backup.SharedSlots })
            if (slots.Length != 36 || slots.Any(s => s.Bar is < 0 or > 2 || s.Slot is < 0 or > 11)
                || slots.Select(s => (s.Bar, s.Slot)).Distinct().Count() != 36)
                throw new InvalidOperationException("The hotbar backup is incomplete.");
        foreach (var option in UiOptions)
            if (!backup.Ui.ContainsKey(option)) throw new InvalidOperationException("The settings backup is incomplete.");
    }

    internal static void Apply(IReadOnlyList<ResolvedAction> plan, Backup before, IGameConfig config,
        Dictionary<int, Binding> keyChanges, bool applyKeys, float width, float height, float size, float globalScale, float rowGap)
    {
        var hud = AddonConfig.Instance();
        var dataset = hud->ActiveDataSet;
        hud->ChangeHudLayout((uint)(before.OriginalLayout == 0 ? 1 : 0));
        // Never modify a stored layout while it is live. The game saves the outgoing
        // live layout during ChangeHudLayout and would overwrite those edits.
        var source = MemoryMarshal.Cast<byte, AddonConfigEntry>(before.HudLayouts).Slice(before.OriginalLayout * 112, 112);
        source.CopyTo(dataset->HudLayoutConfigEntries.Slice(TargetLayout * 112, 112));
        ArrangeHud(width, height, size, globalScale, rowGap);

        var bars = RaptureHotbarModule.Instance();
        for (var b = 0; b < 3; b++)
        {
            config.UiConfig.Set($"HotbarCommon{b + 1:00}", 0u);
            // Keep the module's documented share mask in sync even when a config
            // notification is deferred until the next frame.
            bars->HotbarShareStateBitmask[b / 8] &= (byte)~(1 << (b % 8));
            bars->LoadSavedHotbar(20, (uint)b);
        }
        foreach (var p in plan)
            bars->SetAndSaveSlot((uint)p.Spec.Bar, (uint)p.Spec.Slot, p.Type, p.Id, true, false);
        if (!SavedSlots(0).SequenceEqual(before.SharedSlots)) throw new InvalidOperationException("Shared hotbar protection failed. Restoring the backup.");
        config.UiConfig.Set("HotbarLock", 1u);
        config.UiConfig.Set("HotbarDispRecastTime", 1u);
        if (applyKeys)
        {
            var input = UIInputData.Instance();
            foreach (var item in keyChanges.Values)
            {
                var key = *input->GetKeybind(item.Id);
                key.KeySettings[0] = new KeySetting { Key = (SeVirtualKey)item.First.Key, KeyModifier = (KeyModifierFlag)item.First.Modifier };
                key.KeySettings[1] = new KeySetting { Key = (SeVirtualKey)item.Second.Key, KeyModifier = (KeyModifierFlag)item.Second.Modifier };
                input->SetKeybind((InputId)item.Id, &key);
            }
            if (before.Control.ContainsKey("DirectChat")) config.UiControl.Set("DirectChat", 0u);
            input->SaveFile(true);
        }
        bars->SaveFile(true);
        hud->ChangeHudLayout(TargetLayout);
        hud->SaveFile(true);
        Verify(plan, keyChanges, applyKeys);
    }

    private static void Verify(IReadOnlyList<ResolvedAction> plan, Dictionary<int, Binding> changes, bool keysApplied)
    {
        var bars = RaptureHotbarModule.Instance();
        foreach (var p in plan)
        {
            var stored = bars->SavedHotbars[20].Hotbars[p.Spec.Bar].Slots[p.Spec.Slot];
            var live = bars->GetSlotById((uint)p.Spec.Bar, (uint)p.Spec.Slot);
            if (stored.CommandId != p.Id || stored.CommandType != p.Type || live->CommandId != p.Id || live->CommandType != p.Type)
                throw new InvalidOperationException($"The game did not retain hotbar {p.Spec.Bar + 1}, slot {p.Spec.Slot + 1}.");
        }
        if (AddonConfig.Instance()->ActiveDataSet->CurrentHudLayout != TargetLayout)
            throw new InvalidOperationException("HUD layout 4 did not activate.");
        if (keysApplied)
            foreach (var binding in changes.Values)
            {
                var key = UIInputData.Instance()->GetKeybind(binding.Id);
                if (ToStroke(key->KeySettings[0]) != binding.First || ToStroke(key->KeySettings[1]) != binding.Second)
                    throw new InvalidOperationException("The game did not retain a requested keyboard binding.");
            }
    }

    internal static void Restore(Backup backup, IEnumerable<int> keyIds, IGameConfig config, ulong characterId, bool restoreKeyboard, bool repairShared = false)
    {
        ValidateBackup(backup, characterId);
        var ids = keyIds.Distinct().ToArray();
        if (ids.Any(id => id < 0 || id >= backup.KeyCount)) throw new InvalidOperationException("The keybind restore list is invalid.");
        var hud = AddonConfig.Instance();
        if (hud->ActiveDataSet->CurrentHudLayout == TargetLayout) hud->ChangeHudLayout(0);
        var stored = MemoryMarshal.Cast<byte, AddonConfigEntry>(backup.HudLayouts);
        stored.Slice(TargetLayout * 112, 112).CopyTo(hud->ActiveDataSet->HudLayoutConfigEntries.Slice(TargetLayout * 112, 112));
        var bars = RaptureHotbarModule.Instance();
        foreach (var s in backup.MonkSlots)
        {
            ref var slot = ref bars->SavedHotbars[20].Hotbars[s.Bar].Slots[s.Slot];
            slot.CommandType = (RaptureHotbarModule.HotbarSlotType)s.Type; slot.CommandId = s.Id;
        }
        // Shared bars are not part of the preset. Only repair them if an apply
        // failure actually changed their contents.
        if (repairShared && !SavedSlots(0).SequenceEqual(backup.SharedSlots))
            foreach (var s in backup.SharedSlots)
            {
                ref var slot = ref bars->SavedHotbars[0].Hotbars[s.Bar].Slots[s.Slot];
                slot.CommandType = (RaptureHotbarModule.HotbarSlotType)s.Type; slot.CommandId = s.Id;
            }
        foreach (var option in backup.Ui) config.UiConfig.Set(option.Key, option.Value);
        for (var b = 0; b < 3; b++)
        {
            var shared = backup.Ui[$"HotbarCommon{b + 1:00}"] != 0;
            if (shared) bars->HotbarShareStateBitmask[b / 8] |= (byte)(1 << (b % 8));
            else bars->HotbarShareStateBitmask[b / 8] &= (byte)~(1 << (b % 8));
        }
        if (ids.Length > 0)
        {
            var input = UIInputData.Instance();
            foreach (var id in ids)
            {
                var key = MemoryMarshal.Read<Keybind>(backup.Keybinds.AsSpan(id * sizeof(Keybind), sizeof(Keybind)));
                input->SetKeybind((InputId)id, &key);
            }
            input->SaveFile(true);
        }
        if (restoreKeyboard)
            foreach (var option in backup.Control) config.UiControl.Set(option.Key, option.Value);
        for (uint b = 0; b < 3; b++) bars->LoadSavedHotbar(20, b);
        bars->SaveFile(true);
        hud->ChangeHudLayout((uint)backup.OriginalLayout);
        hud->SaveFile(true);
        if (!SavedSlots(20).SequenceEqual(backup.MonkSlots) || (repairShared && !SavedSlots(0).SequenceEqual(backup.SharedSlots)))
            throw new InvalidOperationException("The restored hotbars did not pass verification. The local backup remains available.");
        if (hud->ActiveDataSet->CurrentHudLayout != backup.OriginalLayout)
            throw new InvalidOperationException("The original HUD layout did not reactivate. The local backup remains available.");
        foreach (var id in ids)
        {
            var expected = MemoryMarshal.Read<Keybind>(backup.Keybinds.AsSpan(id * sizeof(Keybind), sizeof(Keybind)));
            var actual = UIInputData.Instance()->GetKeybind(id);
            if (ToStroke(actual->KeySettings[0]) != ToStroke(expected.KeySettings[0])
                || ToStroke(actual->KeySettings[1]) != ToStroke(expected.KeySettings[1]))
                throw new InvalidOperationException("A restored keyboard binding did not pass verification. The local backup remains available.");
        }
    }

    private static AddonConfigEntry* Find(int layout, string name)
    {
        var entries = AddonConfig.Instance()->ActiveDataSet->HudLayoutConfigEntries.Slice(layout * 112, 112);
        var hash = LayoutPlan.AddonHash(name);
        for (var i = 0; i < entries.Length; i++)
            if (entries[i].AddonNameHash == hash)
                return (AddonConfigEntry*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref entries[i]);
        return null;
    }

    private static void Place(string name, float x, float y, byte anchor, float scale, bool? visible = null)
    {
        var e = Find(TargetLayout, name);
        if (e == null) return;
        e->X = x; e->Y = y; e->ByteValue1 = anchor; e->Scale = scale;
        if (visible.HasValue) e->ByteValue2 = (byte)(visible.Value ? 3 : 0);
        if (visible == true) e->Alpha = 255;
    }

    private static void ArrangeHud(float width, float height, float size, float globalScale, float rowGap)
    {
        var g = LayoutPlan.GetGeometry(width, height, size, globalScale, rowGap);
        float PercentY(float pixels) => pixels / height * 100;
        var halfGaugeGap = 4 * g.EffectiveScale / width * 100;
        for (var b = 0; b < 3; b++)
        {
            var name = b == 0 ? "_ActionBar" : $"_ActionBar{b:00}";
            Place(name, 50, PercentY(g.BarBottom - b * g.BarStep), 7, g.Scale, true);
            var e = Find(TargetLayout, name);
            e->ElementFlags = (e->ElementFlags & 0xffff00ffu) | 0x100u; // Native 12 x 1 shape.
            e->Width = 624; e->Height = 72;
        }
        Place("_ParameterWidget", 50, PercentY(g.ParameterBottom), 7, g.Scale, true);
        Place("JobHudMNK0", 50 - halfGaugeGap, PercentY(g.GaugeBottom), 8, g.Scale, true);
        Place("JobHudMNK1", 50 + halfGaugeGap, PercentY(g.GaugeBottom), 6, g.Scale, true);
        // Use native independent target elements so the enemy cast bar can sit
        // near the character while its health and effects remain at the top.
        Place("_TargetInfo", 50, 7, 1, g.Scale, true);
        Find(TargetLayout, "_TargetInfo")->ElementFlags |= 1u;
        Place("_TargetInfoMainTarget", 50, 7, 1, g.Scale, true);
        Place("_TargetInfoBuffDebuff", 50, 15, 1, g.Scale, true);
        Place("_TargetInfoCastBar", 50, 52, 4, Math.Min(g.Scale * 1.2f, 1.6f), true);
        Place("_CastBar", 50, 57, 4, g.Scale);
        Place("_FocusTargetInfo", 74, 33, 0, g.Scale);
        Place("_PartyList", 2, 26, 0, g.Scale);
        Place("_EnemyList", 78, 43, 0, g.Scale);
        Place("_Status", 2, 15, 0, g.Scale);
        Place("_StatusCustom0", 2, 4, 0, g.Scale);
        Place("_StatusCustom1", 2, 12, 0, g.Scale);
        Place("_StatusCustom2", 2, 19, 0, g.Scale);
        Place("_StatusCustom3", 72, 22, 0, g.Scale);
        Place("_LimitBreak", 50, 3, 1, g.Scale);
        foreach (var name in new[] { "_ActionCross", "_ActionDoubleCrossL", "_ActionDoubleCrossR" })
        {
            var e = Find(TargetLayout, name);
            if (e != null) e->ByteValue2 = 0;
        }
    }
}
