using MonkLayout;
using System.Text.Json;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void Reject(Action action, string message)
{
    try { action(); } catch (InvalidOperationException) { checks++; return; }
    throw new Exception(message);
}

Check(LayoutPlan.Actions.Length == 36, "Hotbar footprint must be exactly 3 by 12.");
var occupied = LayoutPlan.Actions.Where(a => a.Action != null).ToArray();
Check(occupied.Length == 32, "Every current assignable Monk, role and common combat action must have a slot.");
Check(occupied.Select(a => a.Action).Distinct().Count() == 32, "No duplicate skills.");
Check(occupied.Select(a => a.Shortcut).Distinct().Count() == 32, "No duplicate shortcuts.");
Check(LayoutPlan.Actions.Take(3).Select(a => a.ShortcutText).SequenceEqual(new[] { "1", "2", "3" }), "Base form keys.");
Check(LayoutPlan.Actions.Skip(12).Take(3).Select(a => a.ShortcutText).SequenceEqual(new[] { "Shift+1", "Shift+2", "Shift+3" }), "Matching form keys.");
Check(LayoutPlan.Actions.Skip(24).Take(3).Select(a => a.ShortcutText).SequenceEqual(new[] { "Ctrl+1", "Ctrl+2", "Ctrl+3" }), "AoE keys.");
Check(!occupied.Any(a => a.Shortcut.Modifier == 2 && "ZXCV".Contains((char)a.Shortcut.Key)), "Keep standard undo, cut, copy and paste keys free.");
Check(!LayoutPlan.Keys.Any(k => "WASDQE".Contains((char)k)), "No default movement keys.");
Check(!occupied.Any(a => new[] { "Phantom Rush", "Elixir Burst", "Rising Phoenix" }.Contains(a.Action)), "Blitz followups must use native action replacement.");

var input = Enumerable.Range(0, 600).Select(id => new Binding(id, default, default)).ToDictionary(b => b.Id);
input[320] = new(320, new((byte)'W', 0), new((byte)'Q', 0));
input[400] = new(400, new((byte)'R', 0), new((byte)'H', 0));
input[401] = new(401, new((byte)'A', 0), new((byte)'F', 1));
input[402] = new(402, new((byte)'C', 2), new((byte)'V', 2));
var original = input.ToDictionary();
var edits = LayoutPlan.PlanKeyChanges(input.Values, new HashSet<int> { 320 });
Check(!edits.ContainsKey(320), "Movement remains unchanged.");
Check(edits[400].First == default && edits[400].Second == new Stroke((byte)'H', 0), "Clear only the conflicting primary.");
Check(edits[401].First == new Stroke((byte)'A', 0) && edits[401].Second == default, "Clear only the conflicting secondary.");
Check(!edits.ContainsKey(402), "Clipboard shortcuts remain unchanged.");
Check(input.All(kv => original[kv.Key] == kv.Value), "Planning must not mutate input.");
foreach (var edit in edits) input[edit.Key] = edit.Value;
var requested = occupied.Select(a => a.Shortcut).ToHashSet();
foreach (var stroke in requested)
    Check(input.Values.Count(b => b.First == stroke || b.Second == stroke) == 1, "Every assigned key must be unique after conflict handling.");
Check(LayoutPlan.PlanKeyChanges(input.Values, new HashSet<int> { 320 }).Count == 0, "Applying the same bindings must be idempotent.");
foreach (var edit in edits) input[edit.Key] = original[edit.Key];
Check(input.All(kv => original[kv.Key] == kv.Value), "Restoring touched bindings must recover the original map.");
input[320] = new(320, new((byte)'R', 0), default);
Reject(() => LayoutPlan.PlanKeyChanges(input.Values, new HashSet<int> { 320 }), "Custom movement conflicts must block edits.");
Reject(() => LayoutPlan.PlanKeyChanges(input.Values.Where(b => b.Id != 57), new HashSet<int>()), "Missing input table entries must block edits.");

foreach (var resolution in new[] { (960f, 600f), (1280f, 720f), (1920f, 1080f), (2560f, 1440f), (3440f, 1440f), (3840f, 2160f), (5120f, 1440f) })
    foreach (var multiplier in new[] { .8f, 1f, 1.2f })
    {
        var g = LayoutPlan.GetGeometry(resolution.Item1, resolution.Item2, multiplier);
        Check(624 * g.Scale < resolution.Item1, "Horizontal bars must fit the window.");
        Check(g.BarStep >= 49 * g.EffectiveScale, "Visible hotbar rows must not overlap.");
        Check(g.GaugeBottom > resolution.Item2 * .45f, "Keep gauges below the center of the field.");
        Check(g.ParameterBottom < g.BarBottom - 2 * g.BarStep - 72 * g.Scale, "HP must clear all hotbars.");
    }
Reject(() => LayoutPlan.GetGeometry(800, 600, 1), "Tiny windows must be rejected.");
Reject(() => LayoutPlan.GetGeometry(float.NaN, 1080, 1), "Invalid window dimensions must be rejected.");
Reject(() => LayoutPlan.GetGeometry(1920, 1080, 2), "Out-of-range scale must be rejected.");
foreach (var globalScale in new[] { 1f, 1.5f, 2f })
{
    var g = LayoutPlan.GetGeometry(3840, 2160, 1, globalScale);
    Check(g.BarStep >= 49 * g.EffectiveScale, "Global UI scaling must not cause overlapping visible rows.");
    Check(624 * g.EffectiveScale < 3840, "Scaled bars must fit on 4K displays.");
}
Reject(() => LayoutPlan.GetGeometry(960, 600, 1, 4), "Impractical UI scaling must be rejected before edits.");
Reject(() => LayoutPlan.GetGeometry(1920, 1080, 1, float.NaN), "Invalid global UI scaling must be rejected.");
Check(LayoutPlan.AddonHash("_ActionBar") == 0xC48D3605u, "Addon name hashing must match independent CRC-32 calculation.");

var bytes = Enumerable.Range(0, 16128).Select(i => (byte)(i * 73)).ToArray();
var backup = new Backup { CharacterId = 0x1122334455667788, HudLayouts = bytes, Keybinds = bytes[..1100], KeyCount = 100, Ui = new() { ["HotbarCommon01"] = 1 } };
var roundtrip = JsonSerializer.Deserialize<Backup>(JsonSerializer.Serialize(backup))!;
Check(roundtrip.CharacterId == backup.CharacterId && roundtrip.HudLayouts.SequenceEqual(bytes) && roundtrip.Keybinds.SequenceEqual(backup.Keybinds), "Backup serialization must retain raw bytes and 64-bit character identity.");

var monk = new PlayerSession(true, true, 20, 100, true, 20, 100, false, backup.CharacterId);
void Allow(PlayerSession session, bool apply = true)
{
    session.CheckReady(apply);
    checks++;
}
Allow(monk);
// Regression: a logged-in Monk must not be rejected just because the separate
// PlayerState service is absent or still shows the job before a gearset change.
Allow(monk with { StateLoaded = false, StateJobId = 0, EffectiveLevel = 0, StateContentId = 0 });
Allow(monk with { StateJobId = 2, EffectiveLevel = 0 });
Reject(() => (monk with { IsLoggedIn = false }).CheckReady(true), "The character-select screen must not permit an apply.");
Reject(() => (monk with { HasLocalPlayer = false }).CheckReady(true), "Loading screens must not permit an apply.");
Reject(() => (monk with { JobId = 2 }).CheckReady(true), "Pugilist without a soul crystal must not edit Monk hotbars.");
Reject(() => (monk with { JobId = 19 }).CheckReady(true), "A different active job must block edits even if PlayerState still says Monk.");
Reject(() => (monk with { JobId = 0 }).CheckReady(true), "An unreadable active job must block edits.");
Reject(() => (monk with { Level = 99 }).CheckReady(true), "A Monk below level 100 must not apply the preset.");
Reject(() => (monk with { EffectiveLevel = 90 }).CheckReady(true), "A level-synced Monk must not apply the preset.");
Reject(() => (monk with { IsLevelSynced = true }).CheckReady(true), "Explicit level sync must block applying.");
Allow(monk with { Level = 90, EffectiveLevel = 90, IsLevelSynced = true }, false);
Allow(monk with { StateLoaded = false, StateJobId = 0, StateContentId = 0 }, false);
var characterA = backup.CharacterId;
var characterB = characterA + 1;
Check(PlayerSession.ResolveContentId(characterA, characterA, characterA) == characterA, "Known identity must retain all 64 bits.");
Check(PlayerSession.ResolveContentId(0, characterA, characterA) == characterA, "Matching HUD and hotbar identities must recover from missing PlayerState.");
Check(PlayerSession.ResolveContentId(characterA, 0, 0) == characterA, "A valid PlayerState identity does not require redundant file identities.");
Check(PlayerSession.ResolveContentId(characterA, characterB, characterA) == 0, "A stale HUD from another character must block backup and restore.");
Check(PlayerSession.ResolveContentId(characterA, characterA, characterB) == 0, "A stale hotbar file must block backup and restore.");
Check(PlayerSession.ResolveContentId(0, characterA, characterB) == 0, "Fallback identities must agree.");
Check(PlayerSession.ResolveContentId(0, 0, characterA) == 0, "One unconfirmed file identity is insufficient.");
Check(PlayerSession.ResolveContentId(0, characterA, 0) == 0, "Missing hotbar identity must block fallback.");
Check(PlayerSession.ResolveContentId(0, 0, 0) == 0, "A zero-ID character directory must never be selected.");
Check(monk.Summary.Contains("Monk (20)") && !monk.Summary.Contains(characterA.ToString()), "The visible detection status must identify the job without exposing character identity.");
try
{
    (monk with { JobId = 2 }).CheckReady(true);
    throw new Exception("Pugilist was accepted.");
}
catch (InvalidOperationException ex)
{
    Check(ex.Message.Contains("Soul of the Monk"), "Pugilist detection must explain how to equip the actual Monk job.");
}

var whm = JobPreset.WhiteMage;
var whmActions = whm.Actions.Where(a => a.Action != null).ToArray();
var whmBound = whmActions.Where(a => a.Bind).ToArray();
Check(whm.Actions.Length == 36 && whmActions.Length == 35, "White Mage must populate 35 actions in three regular hotbars.");
Check(whmBound.Length == 32 && whmBound.Select(a => a.Shortcut).Distinct().Count() == 32, "White Mage must have 32 distinct keyboard shortcuts.");
Check(whmActions.Select(a => a.Action).Distinct().Count() == 35, "White Mage must not duplicate or omit a slot through action replacement.");
string[] expectedWhm = ["Stone", "Aero", "Cure", "Medica", "Raise", "Cure II", "Presence of Mind", "Regen", "Cure III", "Aetherial Shift", "Holy", "Medica II", "Benediction", "Afflatus Solace", "Asylum", "Assize", "Thin Air", "Tetragrammaton", "Divine Benison", "Plenary Indulgence", "Afflatus Misery", "Afflatus Rapture", "Temperance", "Aquaveil", "Liturgy of the Bell", "Glare IV", "Divine Caress", "Repose", "Esuna", "Lucid Dreaming", "Swiftcast", "Surecast", "Rescue", "Sprint", "Limit Break"];
Check(whmActions.Select(a => a.Action!).ToHashSet().SetEquals(expectedWhm), "White Mage must include every current base action, healer role action and common combat action.");
Check(whm.Actions.Take(3).Select(a => a.Label).SequenceEqual(new[] { "Glare III", "Dia", "Holy III" }), "White Mage damage must use the familiar 1-3 keys.");
Check(whm.Actions.Skip(3).Take(2).Select(a => a.Action).SequenceEqual(new[] { "Afflatus Solace", "Afflatus Rapture" }), "Both lily heals must have unmodified shortcuts.");
Check(whmActions.Single(a => a.Action == "Aetherial Shift").Shortcut == occupied.Single(a => a.Action == "Thunderclap").Shortcut, "Movement must stay on X across both jobs.");
Check(whmActions.Single(a => a.Action == "Sprint").Shortcut == occupied.Single(a => a.Action == "Sprint").Shortcut, "Sprint must stay on V across both jobs.");
Check(whmActions.Single(a => a.Action == "Surecast").Shortcut == occupied.Single(a => a.Action == "Arm's Length").Shortcut, "Knockback protection must stay on Shift+X across jobs.");
Check(whmActions.Where(a => !a.Bind).Select(a => a.Action).ToHashSet().SetEquals(new[] { "Cure", "Medica", "Repose" }), "Less-used actions must stay available for clicking without taking clipboard shortcuts.");
Check(whmActions.Where(a => !a.Bind).All(a => a.Shortcut == default && a.ShortcutText == "Click"), "Click-only actions must never synthesize a keyboard shortcut.");
Check(!whmBound.Any(a => a.Shortcut.Modifier == 2 && "ZXCV".Contains((char)a.Shortcut.Key)), "White Mage must protect Ctrl+Z/X/C/V.");
var whmKeys = LayoutPlan.PlanKeyChanges(original.Values, new HashSet<int> { 320 }, whm.Actions);
var monkKeys = LayoutPlan.PlanKeyChanges(original.Values, new HashSet<int> { 320 }, JobPreset.Monk.Actions);
Check(whmKeys.OrderBy(k => k.Key).SequenceEqual(monkKeys.OrderBy(k => k.Key)), "Changing between presets must retain the same global hotbar keyboard map.");
Check(!whmKeys.ContainsKey(402), "White Mage must preserve existing clipboard bindings.");
Check(whm.JobId == 24 && whm.BaseClassId == 6 && whm.HudLayout == 2, "White Mage must edit job 24 in HUD 3.");
Check(JobPreset.Monk.JobId == 20 && JobPreset.Monk.HudLayout == 3 && JobPreset.Monk.BackupFolder == "", "Existing Monk job, HUD 4 and backup paths must remain compatible.");
Check(whm.BackupFolder.Length > 0 && whm.BackupFolder != JobPreset.Monk.BackupFolder, "Each job must retain its own original backup and restore index.");
Check(whm.Gauges.SequenceEqual(new[] { "JobHudWHM0" }), "White Mage must use the lily gauge instead of Monk gauges.");
var whiteMage = monk with { JobId = 24, StateJobId = 24 };
whiteMage.CheckReady(true, whm); checks++;
(whiteMage with { StateLoaded = false, StateJobId = 0, EffectiveLevel = 0, StateContentId = 0 }).CheckReady(true, whm); checks++;
(whiteMage with { StateJobId = 20 }).CheckReady(true, whm); checks++;
Reject(() => monk.CheckReady(true, whm), "White Mage preset must reject a Monk before any native edits.");
Reject(() => whiteMage.CheckReady(true, JobPreset.Monk), "Monk preset must reject White Mage before any native edits.");
Reject(() => (whiteMage with { Level = 90 }).CheckReady(true, whm), "The level 100 White Mage preset must reject lower levels.");
Reject(() => (whiteMage with { IsLevelSynced = true }).CheckReady(true, whm), "White Mage apply must reject level sync.");
try { (whiteMage with { JobId = 6 }).CheckReady(true, whm); throw new Exception("Conjurer was accepted."); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("Soul of the White Mage"), "Conjurer detection must explain the correct soul crystal."); }
var oldSlots = Enumerable.Range(0, 36).Select(i => new SlotBackup(i / 12, i % 12, 1, (uint)(100 + i))).ToArray();
var legacyJson = JsonSerializer.Serialize(new { CharacterId = characterA, MonkSlots = oldSlots });
var legacyBackup = JsonSerializer.Deserialize<Backup>(legacyJson)!;
legacyBackup.CheckIdentity(characterA, JobPreset.Monk); checks++;
Check(legacyBackup.JobSlots.SequenceEqual(oldSlots), "Pre-1.3 MonkSlots backups must deserialize without losing any slot.");
Check(!JsonSerializer.Serialize(legacyBackup).Contains("MonkSlots"), "New backups must use JobSlots while still reading the legacy field.");
var whmBackup = new Backup { CharacterId = characterA, ClassJobId = 24, TargetLayout = 2, JobSlots = oldSlots };
var whmRoundtrip = JsonSerializer.Deserialize<Backup>(JsonSerializer.Serialize(whmBackup))!;
whmRoundtrip.CheckIdentity(characterA, whm); checks++;
Check(whmRoundtrip.JobSlots.SequenceEqual(oldSlots), "White Mage backup roundtrip must preserve all slots and job identity.");
Reject(() => legacyBackup.CheckIdentity(characterA, whm), "A Monk backup must never restore into White Mage.");
Reject(() => whmBackup.CheckIdentity(characterA, JobPreset.Monk), "A White Mage backup must never restore into Monk.");
Reject(() => whmBackup.CheckIdentity(characterB, whm), "White Mage backups must reject another character.");
Reject(() => whmBackup.CheckIdentity(0, whm), "White Mage backups must reject an unknown character.");
whmBackup.TargetLayout = 3;
Reject(() => whmBackup.CheckIdentity(characterA, whm), "White Mage restore must reject a backup for Monk's HUD slot.");

var wrath = JobPreset.WhiteMageWrath;
var wrathActions = wrath.Actions.Where(a => a.Action != null).ToArray();
var wrathBound = wrathActions.Where(a => a.Bind).ToArray();
Check(wrath.UsesWrath && !whm.UsesWrath && !JobPreset.Monk.UsesWrath,
    "Wrath controls must only appear for the selected Wrath profile.");
Check(wrath.Name == whm.Name && wrath.ProfileName != whm.ProfileName,
    "Profiles must have distinct display names while retaining the actual White Mage job name.");
Check(wrath.Actions.Length == 36 && wrathActions.Length == 35,
    "The Wrath profile must retain the existing 3 by 12 footprint and all 35 White Mage actions.");
Check(wrathActions.Select(a => a.Action!).ToHashSet().SetEquals(expectedWhm)
    && wrathActions.Select(a => a.Action).Distinct().Count() == 35,
    "The Wrath profile must preserve every White Mage action exactly once, including manual utilities.");
Check(wrathBound.Length == 32 && wrathBound.Select(a => a.Shortcut).Distinct().Count() == 32,
    "The Wrath profile must keep 32 unique keyboard shortcuts.");
Check(wrath.Actions.Take(6).Select(a => a.Action).SequenceEqual(new[] {
    "Stone", "Holy", "Cure", "Medica", "Afflatus Solace", "Afflatus Rapture" }),
    "Wrath's four native replacement buttons and both lily heals must occupy the six easiest keys.");
Check(wrath.Actions.Take(6).Select(a => a.ShortcutText).SequenceEqual(new[] { "1", "2", "3", "4", "5", "R" }),
    "The combo buttons must be on 1-4 and lily heals on 5 and R.");
Check(wrath.Actions.Skip(6).Take(26).Select(a => (a.Action, a.Shortcut))
    .SequenceEqual(whm.Actions.Skip(6).Take(26).Select(a => (a.Action, a.Shortcut))),
    "All manual shortcuts after the six primary buttons must remain in their existing positions.");
foreach (var (action, shortcut) in new[] {
    ("Sprint", "V"), ("Aetherial Shift", "X"), ("Surecast", "Shift+X"),
    ("Rescue", "Ctrl+F"), ("Limit Break", "Ctrl+G"), ("Benediction", "Z"),
    ("Swiftcast", "G"), ("Esuna", "C"), ("Raise", "Shift+C") })
    Check(wrathActions.Single(a => a.Action == action).ShortcutText == shortcut,
        $"Manual {action} must remain on {shortcut}.");
Check(wrathActions.Where(a => !a.Bind).Select(a => a.Action).ToHashSet().SetEquals(new[] { "Aero", "Assize", "Repose" }),
    "Dia, Assize and Repose must stay visible without occupying clipboard shortcuts.");
Check(wrathActions.Where(a => !a.Bind).All(a => a.Shortcut == default && a.ShortcutText == "Click"),
    "Click-only Wrath backup actions must not acquire synthetic shortcuts.");
Check(wrathActions.Where(a => a.General).Select(a => a.Action).ToHashSet().SetEquals(new[] { "Sprint", "Limit Break" }),
    "Sprint and Limit Break must resolve through the game's GeneralAction table.");
Check(!wrathBound.Any(a => a.Shortcut.Modifier == 2 && "ZXCV".Contains((char)a.Shortcut.Key)),
    "The Wrath profile must protect the same clipboard shortcuts as the manual profiles.");
var wrathKeys = LayoutPlan.PlanKeyChanges(original.Values, new HashSet<int> { 320 }, wrath.Actions);
Check(wrathKeys.OrderBy(k => k.Key).SequenceEqual(whmKeys.OrderBy(k => k.Key))
    && wrathKeys.OrderBy(k => k.Key).SequenceEqual(monkKeys.OrderBy(k => k.Key)),
    "Selecting the Wrath profile must not change the shared global keyboard map.");
var manualBindings = original.ToDictionary();
foreach (var edit in whmKeys) manualBindings[edit.Key] = edit.Value;
Check(LayoutPlan.PlanKeyChanges(manualBindings.Values, new HashSet<int> { 320 }, wrath.Actions).Count == 0,
    "Switching from an installed manual White Mage profile to Wrath must require no keybind changes.");
Reject(() => LayoutPlan.PlanKeyChanges(input.Values, new HashSet<int> { 320 }, wrath.Actions),
    "Wrath must retain the protection against custom movement key conflicts.");
Check(wrath.JobId == whm.JobId && wrath.BaseClassId == whm.BaseClassId && wrath.HudLayout == whm.HudLayout
    && wrath.BackupFolder == whm.BackupFolder && wrath.BackupFolder == "white-mage"
    && wrath.Gauges.SequenceEqual(whm.Gauges),
    "Both White Mage profiles must share the same saved job, HUD 3, gauge, original backup and restore index.");
whmRoundtrip.CheckIdentity(characterA, wrath); checks++;
whmRoundtrip.CheckIdentity(characterA, whm); checks++;
Reject(() => legacyBackup.CheckIdentity(characterA, wrath), "Legacy Monk backups must not restore through the Wrath profile.");
Reject(() => whmRoundtrip.CheckIdentity(characterB, wrath), "Wrath restore must reject another character's White Mage backup.");
whiteMage.CheckReady(true, wrath); checks++;
Reject(() => monk.CheckReady(true, wrath), "The Wrath profile must reject Monk before editing any game state.");
Reject(() => (whiteMage with { IsLevelSynced = true }).CheckReady(true, wrath),
    "Wrath layout apply must retain the existing level-sync protection.");
Check(WrathControls.Pause.Command == "/wrath auto off" && WrathControls.Resume.Command == "/wrath auto on",
    "Pause and Resume must use deterministic commands rather than toggle the current state.");
Check(WrathControls.OpenAutoSettings.Command == "/wrath autosettings"
    && WrathControls.OpenWhiteMage.Command == "/wrath whm",
    "Settings buttons must open only the requested Wrath configuration pages.");
Check(WrathControls.Pause.Confirmation.Contains("Check Wrath's state")
    && WrathControls.Resume.Confirmation.Contains("Check Wrath's state"),
    "A successful command dispatch must not claim that live Wrath state was verified.");

Check(NativeCompatibility.IsSupportedRuntimeHash(NativeCompatibility.CurrentStructHash),
    "The audited current native DLL must pass the runtime gate.");
Check(!NativeCompatibility.IsSupportedRuntimeHash(NativeCompatibility.LegacyBackupStructHash),
    "Backup compatibility must never permit the old native DLL at runtime.");
Check(NativeCompatibility.CurrentGameVersion == "2026.11.03.0000.0000",
    "The runtime game version must match the audited current release.");
foreach (var hash in new[] { NativeCompatibility.CurrentStructHash, NativeCompatibility.LegacyBackupStructHash })
{
    Check(NativeCompatibility.IsSupportedBackupHash(hash), "Only explicitly audited native snapshots may pass the backup hash gate.");
    var compatibleBackup = JsonSerializer.Deserialize<Backup>(JsonSerializer.Serialize(new Backup
    {
        CharacterId = characterA, ClassJobId = 24, TargetLayout = 2, StructHash = hash, JobSlots = oldSlots
    }))!;
    compatibleBackup.CheckIdentity(characterA, whm); checks++;
    compatibleBackup.CheckIdentity(characterA, wrath); checks++;
    Check(compatibleBackup.StructHash == hash && compatibleBackup.JobSlots.SequenceEqual(oldSlots),
        "Legacy-compatible and current White Mage snapshots must preserve their exact saved data.");
    Reject(() => compatibleBackup.CheckIdentity(characterA, JobPreset.Monk),
        "An accepted structure hash must not permit restoring White Mage data into Monk.");
    Reject(() => compatibleBackup.CheckIdentity(characterB, wrath),
        "An accepted structure hash must not permit restoring another character's backup.");
    compatibleBackup.TargetLayout = 3;
    Reject(() => compatibleBackup.CheckIdentity(characterA, wrath),
        "An accepted structure hash must not bypass the HUD slot identity guard.");
}
foreach (var hash in new string?[] { null, "", new string('0', 64), NativeCompatibility.CurrentStructHash.ToLowerInvariant() })
{
    Check(!NativeCompatibility.IsSupportedRuntimeHash(hash), "Missing, unknown or altered runtime hashes must be rejected.");
    Check(!NativeCompatibility.IsSupportedBackupHash(hash), "Missing, unknown or altered backup hashes must be rejected.");
}
var liveReadCount = 0;
var inFrameworkUpdate = false;
var liveSession = whiteMage;
var sessionCache = new SessionCache(() =>
{
    liveReadCount++;
    if (!inFrameworkUpdate) throw new InvalidOperationException("Not on main thread!");
    return liveSession;
});
Check(liveReadCount == 0, "Constructing session state on the plugin loader thread must not read game data.");
Check(!sessionCache.Snapshot.IsAvailable && sessionCache.Snapshot.Session == default,
    "Before the first framework update, UI callbacks must receive an unavailable snapshot.");
_ = sessionCache.Snapshot.Error;
Check(liveReadCount == 0, "Reading cached UI state must not trigger a live player lookup.");
inFrameworkUpdate = true;
sessionCache.Refresh();
var firstSnapshot = sessionCache.Snapshot;
Check(liveReadCount == 1 && firstSnapshot.IsAvailable && firstSnapshot.Session == whiteMage,
    "One framework refresh must capture exactly one coherent live session.");
inFrameworkUpdate = false;
Check(sessionCache.Snapshot.Session.JobId == 24 && liveReadCount == 1,
    "A later UI callback must be able to read White Mage state without accessing game objects.");
liveSession = monk;
inFrameworkUpdate = true;
sessionCache.Refresh();
Check(sessionCache.Snapshot.Session == monk && liveReadCount == 2,
    "A new framework tick must replace the cached session after a job change.");
Check(firstSnapshot.Session == whiteMage && !ReferenceEquals(firstSnapshot, sessionCache.Snapshot),
    "A published snapshot must remain immutable while a new snapshot replaces it atomically.");
inFrameworkUpdate = false;
sessionCache.Refresh();
Check(!sessionCache.Snapshot.IsAvailable && sessionCache.Snapshot.Session == default,
    "A failed live read must invalidate stale character and job data before any operation.");
Check(sessionCache.Snapshot.Error.Contains("Not on main thread!"),
    "A rejected framework read must retain a diagnostic message for the setup window.");
inFrameworkUpdate = true;
liveSession = default;
sessionCache.Refresh();
Check(sessionCache.Snapshot.IsAvailable && !sessionCache.Snapshot.Session.IsLoggedIn,
    "A valid logged-out snapshot must clear previously cached character information.");
liveSession = whiteMage;
sessionCache.Refresh();
Check(sessionCache.Snapshot.IsAvailable && sessionCache.Snapshot.Error.Length == 0
    && sessionCache.Snapshot.Session == whiteMage,
    "The cache must recover on a later valid framework update without reloading the plugin.");
Console.WriteLine($"PASS: {checks} Monk, White Mage manual and Wrath layout, keyboard, restore, command, character detection and startup session checks.");
