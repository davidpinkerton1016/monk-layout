# Plugin startup checks

This harness loads the actual compiled plugin and the matching Dalamud host assemblies. It injects the plugin's real service interfaces through `DispatchProxy` and invokes its constructor on a background thread, matching the current host's constructor scheduling.

The object-table proxy calls the real `Dalamud.Utility.ThreadSafety.AssertMainThread` method if the plugin reads player objects outside a simulated framework update. That reproduces the load failure in version 1.4.0.0. Other proxies avoid native game memory access.

For the fixed plugin, the harness verifies that construction reads no player objects, registers its framework callback, refreshes the session on the first framework update, opens the UI without reading player objects, and disposes cleanly. The simulated session is logged out. This does not exercise native HUD edits or render the UI inside FFXIV.

Run the fixed-build check from the repository root:

```sh
dotnet run --project StartupChecks/StartupChecks.csproj -- Plugin/MonkLayout.dll /path/to/matching/Dalamud
```

To reproduce the old defect, keep a separate copy of the 1.4.0.0 DLL and run a separate process:

```sh
dotnet run --project StartupChecks/StartupChecks.csproj -- /path/to/1.4.0.0/MonkLayout.dll /path/to/matching/Dalamud --expect-load-failure
```

Each command returns zero only when its expected behavior occurs. The test writes an isolated temporary configuration directory and removes it after completion. Do not commit Dalamud host libraries or the historical binary to this test directory.
