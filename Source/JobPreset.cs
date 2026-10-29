namespace MonkLayout;

internal sealed record JobPreset(string Name, int JobId, int BaseClassId, string BaseClassName,
    string SoulCrystal, int HudLayout, string BackupFolder, string[] Gauges, ActionSpec[] Actions, string Description)
{
    internal static readonly JobPreset Monk = new("Monk", 20, 2, "Pugilist", "Soul of the Monk", 3, "",
        ["JobHudMNK0", "JobHudMNK1"], LayoutPlan.Actions,
        "Paired form attacks, nearby burst buttons, and centered Monk gauges.");

    internal static readonly JobPreset WhiteMage = new("White Mage", 24, 6, "Conjurer", "Soul of the White Mage", 2, "white-mage",
        ["JobHudWHM0"], LayoutPlan.WhiteMageActions,
        "Damage on 1-3, lily heals on 4-5, nearby emergency tools, and a party list beside the center of the screen. Cure, Medica and Repose remain visible for clicking.");
}
