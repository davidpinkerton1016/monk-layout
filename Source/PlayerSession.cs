namespace MonkLayout;

// The active character and the separate PlayerState service can load at different
// times. Only the active character determines the currently equipped job.
internal readonly record struct PlayerSession(
    bool IsLoggedIn, bool HasLocalPlayer, uint JobId, byte Level,
    bool StateLoaded, uint StateJobId, short EffectiveLevel, bool IsLevelSynced,
    ulong StateContentId)
{
    internal string Summary => $"Login: {(IsLoggedIn ? "yes" : "no")}; character: {(HasLocalPlayer ? "present" : "loading")}; "
        + $"job: {JobName(JobId)}; level: {Level}; player data: {(StateLoaded ? "loaded" : "loading")}; data job: {StateJobId}.";

    internal void CheckReady(bool require100, JobPreset? selected = null)
    {
        var preset = selected ?? JobPreset.Monk;
        if (!IsLoggedIn)
            throw new InvalidOperationException("The game reports that you are not logged in yet. Wait for login to finish, then try again.");
        if (!HasLocalPlayer)
            throw new InvalidOperationException("Your character is still loading. Wait until you can move, then try again.");
        if (JobId == preset.BaseClassId)
            throw new InvalidOperationException($"Your active job is {preset.BaseClassName}. Equip the {preset.SoulCrystal}, then try again.");
        if (JobId != preset.JobId)
            throw new InvalidOperationException($"Your active job is {JobName(JobId)}. Switch to {preset.Name}, then try again.");
        if (require100 && (Level < 100
            || (StateLoaded && StateJobId == preset.JobId && (EffectiveLevel < 100 || IsLevelSynced))))
            throw new InvalidOperationException($"Use your level 100 {preset.Name} outside level sync. Detected character level: {Level}.");
    }

    // A missing PlayerState must not produce a zero-ID backup. The two independent
    // character settings files must agree before they can supply the identity.
    internal static ulong ResolveContentId(ulong stateId, ulong hudId, ulong hotbarId)
    {
        if (stateId != 0)
            return (hudId != 0 && hudId != stateId) || (hotbarId != 0 && hotbarId != stateId) ? 0 : stateId;
        return hudId != 0 && hudId == hotbarId ? hudId : 0;
    }

    private static string JobName(uint job) => job switch
    {
        0 => "loading",
        2 => "Pugilist (2)",
        6 => "Conjurer (6)",
        20 => "Monk (20)",
        24 => "White Mage (24)",
        _ => $"job ID {job}"
    };
}
