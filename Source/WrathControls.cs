namespace MonkLayout;

internal sealed record WrathControl(string Label, string Command, string Confirmation);

internal static class WrathControls
{
    // These commands are dispatched only by an explicit button click.
    // Layout selection, Apply and Restore must never change Wrath's state.
    internal static readonly WrathControl OpenAutoSettings = new(
        "Open Wrath settings", "/wrath autosettings", "Requested Wrath's Auto-Rotation settings.");
    internal static readonly WrathControl OpenWhiteMage = new(
        "Open WHM settings", "/wrath whm", "Requested Wrath's White Mage settings.");
    internal static readonly WrathControl Pause = new(
        "Pause Wrath", "/wrath auto off", "Sent /wrath auto off. Check Wrath's state indicator.");
    internal static readonly WrathControl Resume = new(
        "Resume Wrath", "/wrath auto on", "Sent /wrath auto on. Check Wrath's state indicator.");
}
