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
Console.WriteLine($"PASS: {checks} layout, keyboard conflict, restore, backup and character detection checks.");
