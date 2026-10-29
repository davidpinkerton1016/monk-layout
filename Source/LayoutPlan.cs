using System.Text;

namespace MonkLayout;

public sealed record ActionSpec(int Bar, int Slot, string? Action, string Label, bool General = false)
{
    public Stroke Shortcut => new(LayoutPlan.Keys[Slot], (byte)(Bar == 1 ? 1 : Bar == 2 ? 2 : 0));
    public string ShortcutText => (Bar == 1 ? "Shift+" : Bar == 2 ? "Ctrl+" : "") + (char)Shortcut.Key;
}

public readonly record struct Stroke(byte Key, byte Modifier);
public sealed record Binding(int Id, Stroke First, Stroke Second);
public sealed record Geometry(float Scale, float EffectiveScale, float BarBottom, float BarStep, float ParameterBottom, float GaugeBottom);

public static class LayoutPlan
{
    // Avoid the default movement keys, Q/E and mouse buttons.
    public static readonly byte[] Keys = "12345RFGZXCV".Select(c => (byte)c).ToArray();
    public const int FirstHotbarInputId = 57;
    // Native 6x2 and 4x3 hotbars add 49 units for each extra icon row.
    // The 72-unit addon bounds include padding that need not separate rows.
    public const float HotbarRowPitch = 49;
    public const float DefaultRowGap = 5;
    public static readonly ActionSpec[] Actions = Build();

    private static ActionSpec[] Build()
    {
        string?[][] names = [
            ["Bootshine", "True Strike", "Snap Punch", "Perfect Balance", "Masterful Blitz",
             "Steeled Meditation", "Riddle of Fire", "Brotherhood", "Riddle of Wind", "Thunderclap", "True North", "Sprint"],
            ["Dragon Kick", "Twin Snakes", "Demolish", "Form Shift", "Six-sided Star",
             "Inspirited Meditation", "Fire's Reply", "Feint", "Wind's Reply", "Arm's Length", "Second Wind", "Bloodbath"],
            ["Arm of the Destroyer", "Four-point Fury", "Rockbreaker", "Mantra", "Riddle of Earth",
             "Earth's Reply", "Leg Sweep", "Limit Break", null, null, null, null]
        ];
        var labels = new Dictionary<string, string>
        {
            ["Bootshine"] = "Leaping Opo",
            ["True Strike"] = "Rising Raptor",
            ["Snap Punch"] = "Pouncing Coeurl",
            ["Arm of the Destroyer"] = "Shadow of the Destroyer",
            ["Steeled Meditation"] = "Meditation / The Forbidden Chakra",
            ["Inspirited Meditation"] = "Meditation / Enlightenment"
        };
        return names.SelectMany((row, bar) => row.Select((name, slot) =>
            new ActionSpec(bar, slot, name, name == null ? "Empty" : labels.GetValueOrDefault(name, name),
                name is "Sprint" or "Limit Break"))).ToArray();
    }

    public static Dictionary<int, Binding> PlanKeyChanges(IEnumerable<Binding> original, ISet<int> protectedInputs)
    {
        var before = original.ToDictionary(b => b.Id);
        var desired = Actions.Where(a => a.Action != null).ToDictionary(a => FirstHotbarInputId + a.Bar * 12 + a.Slot, a => a.Shortcut);
        foreach (var id in desired.Keys)
            if (!before.ContainsKey(id)) throw new InvalidOperationException("The game's hotbar keybind table has changed.");
        var strokes = desired.Values.ToHashSet();
        foreach (var binding in before.Values)
            if (protectedInputs.Contains(binding.Id) && (strokes.Contains(binding.First) || strokes.Contains(binding.Second)))
                throw new InvalidOperationException("A proposed shortcut conflicts with a movement key. Uncheck Apply recommended keybinds to keep your bindings.");

        var changed = new Dictionary<int, Binding>();
        foreach (var b in before.Values)
        {
            var next = desired.TryGetValue(b.Id, out var stroke)
                ? new Binding(b.Id, stroke, default)
                : new Binding(b.Id, strokes.Contains(b.First) ? default : b.First, strokes.Contains(b.Second) ? default : b.Second);
            if (next != b) changed.Add(b.Id, next);
        }
        return changed;
    }

    public static Geometry GetGeometry(float width, float height, float multiplier, float globalScale = 1, float rowGap = DefaultRowGap)
    {
        if (!float.IsFinite(width) || !float.IsFinite(height) || width < 960 || height < 600)
            throw new InvalidOperationException("Use a game window at least 960 by 600 before applying the HUD.");
        if (!float.IsFinite(multiplier) || multiplier < .8f || multiplier > 1.2f)
            throw new InvalidOperationException("HUD size must be between 80% and 120%.");
        if (!float.IsFinite(globalScale) || globalScale < .5f || globalScale > 4)
            throw new InvalidOperationException("The game's global UI scaling could not be read.");
        if (!float.IsFinite(rowGap) || rowGap < 0 || rowGap > 16)
            throw new InvalidOperationException("Row spacing must be between 0 and 16.");
        var proposed = Math.Clamp(Math.Min(width / 1920f, height / 1080f) * multiplier / globalScale, .6f, 1.6f);
        // Use the game's discrete HUD scales and reserve room for both gauges.
        float[] scales = [.6f, .8f, .9f, 1f, 1.1f, 1.2f, 1.4f, 1.6f];
        var scale = scales.Where(s => s <= proposed + .001f).LastOrDefault(.6f);
        var effective = scale * globalScale;
        if (624 * effective > width * .9f || 400 * effective > height * .65f)
            throw new InvalidOperationException("Enlarge the game window or lower the game's global UI scale before applying this layout.");
        var bottom = height * .97f;
        var step = (HotbarRowPitch + rowGap) * effective;
        var parameterBottom = bottom - 2 * step - 72 * effective - 4 * effective;
        return new(scale, effective, bottom, step, parameterBottom, parameterBottom - 42 * effective);
    }

    public static uint AddonHash(string name)
    {
        uint crc = uint.MaxValue;
        foreach (var b in Encoding.UTF8.GetBytes(name + "_a"))
        {
            crc ^= b;
            for (var n = 0; n < 8; n++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        }
        return ~crc;
    }
}
