using System.Text.Json.Serialization;

namespace MonkLayout;

public sealed class Backup
{
    public int Schema { get; set; } = 1;
    // Defaults retain compatibility with Monk backups made before job presets.
    public int ClassJobId { get; set; } = 20;
    public int TargetLayout { get; set; } = 3;
    public string StructHash { get; set; } = "";
    public ulong CharacterId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int OriginalLayout { get; set; }
    public byte[] HudLayouts { get; set; } = [];
    public SlotBackup[] JobSlots { get; set; } = [];
    [JsonPropertyName("MonkSlots"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SlotBackup[]? LegacyMonkSlots
    {
        get => null;
        set { if (value != null) JobSlots = value; }
    }
    public SlotBackup[] SharedSlots { get; set; } = [];
    public byte[] Keybinds { get; set; } = [];
    public int KeyCount { get; set; }
    public Dictionary<string, uint> Ui { get; set; } = [];
    public Dictionary<string, uint> Control { get; set; } = [];

    internal void CheckIdentity(ulong characterId, JobPreset preset)
    {
        if (characterId == 0 || CharacterId != characterId || ClassJobId != preset.JobId || TargetLayout != preset.HudLayout)
            throw new InvalidOperationException("The backup belongs to a different character, job or HUD slot.");
    }
}

public sealed record SlotBackup(int Bar, int Slot, byte Type, uint Id);

public sealed class InstallState
{
    public HashSet<int> KeyInputsChanged { get; set; } = [];
    public bool KeysApplied { get; set; }
    public bool HasApplied { get; set; }
}
