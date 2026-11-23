namespace MonkLayout;

internal static class NativeCompatibility
{
    internal const string CurrentStructHash = "FD39ED3C6D69E8292E1C458BA4688C101E63AEAB3EE2F6248852D55746FB4306";
    internal const string CurrentGameVersion = "2026.11.03.0000.0000";
    internal const string LegacyBackupStructHash = "F5A2C68CCECD3E199BDA572050E98902B2E7594066C95DC7826C2D4297CD0564";

    // The 15.0.4.1 source/binary audit matched all 22 native types used here
    // against 15.0.3.9, including the HUD entry and keybind byte layouts.
    // This permits those old snapshots, never the old DLL at runtime.
    internal static bool IsSupportedRuntimeHash(string? hash) => hash == CurrentStructHash;

    internal static bool IsSupportedBackupHash(string? hash) =>
        hash is CurrentStructHash or LegacyBackupStructHash;
}
