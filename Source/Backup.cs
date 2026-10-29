namespace MonkLayout;

public sealed class Backup
{
    public int Schema { get; set; } = 1;
    public string StructHash { get; set; } = "";
    public ulong CharacterId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int OriginalLayout { get; set; }
    public byte[] HudLayouts { get; set; } = [];
    public SlotBackup[] MonkSlots { get; set; } = [];
    public SlotBackup[] SharedSlots { get; set; } = [];
    public byte[] Keybinds { get; set; } = [];
    public int KeyCount { get; set; }
    public Dictionary<string, uint> Ui { get; set; } = [];
    public Dictionary<string, uint> Control { get; set; } = [];
}

public sealed record SlotBackup(int Bar, int Slot, byte Type, uint Id);

public sealed class InstallState
{
    public HashSet<int> KeyInputsChanged { get; set; } = [];
    public bool KeysApplied { get; set; }
    public bool HasApplied { get; set; }
}
