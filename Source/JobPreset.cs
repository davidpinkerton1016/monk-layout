namespace MonkLayout;

internal sealed record JobPreset(string Name, int JobId, int BaseClassId, string BaseClassName,
    string SoulCrystal, int HudLayout, string BackupFolder, string[] Gauges, ActionSpec[] Actions, string Description,
    bool UsesWrath = false)
{
    internal string ProfileName => UsesWrath ? "White Mage Wrath" : Name;

    internal static readonly JobPreset Monk = new("Monk", 20, 2, "Pugilist", "Soul of the Monk", 3, "",
        ["JobHudMNK0", "JobHudMNK1"], LayoutPlan.Actions,
        "Paired form attacks, nearby burst buttons, and centered Monk gauges.");

    internal static readonly JobPreset WhiteMage = new("White Mage", 24, 6, "Conjurer", "Soul of the White Mage", 2, "white-mage",
        ["JobHudWHM0"], LayoutPlan.WhiteMageActions,
        "Damage on 1-3, lily heals on 4-5, nearby emergency tools, and a party list beside the center of the screen. Cure, Medica and Repose remain visible for clicking.");

    // Both White Mage profiles edit the same saved job bars and HUD slot.
    // Share the original backup and restore index when changing profiles.
    internal static readonly JobPreset WhiteMageWrath = WhiteMage with
    {
        Actions = LayoutPlan.WhiteMageWrathActions,
        Description = "Wrath's four combo buttons use 1-4. Lily heals use 5 and R. Sprint, movement, knockback protection, Rescue and Limit Break keep their familiar keys.",
        UsesWrath = true
    };
}
