using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MonkLayout;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IGameGui Gui { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly string PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unknown";
    private bool open = true;
    private bool applyKeys = true;
    private float size = 1;
    private float rowGap = LayoutPlan.DefaultRowGap;
    private Vector2 viewport = new(1920, 1080);
    private int pending;
    private bool busy;
    private ulong characterId;
    private JobPreset preset = JobPreset.Monk;
    private string status = "Log in as your level 100 Monk, then press Apply Monk layout.";
    private readonly string compatibilityError;
    private readonly string directory;

    public Plugin()
    {
        directory = Interface.GetPluginConfigDirectory();
        Directory.CreateDirectory(directory);
        try
        {
            using var stream = File.OpenRead(typeof(AddonConfig).Assembly.Location);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            compatibilityError = hash == NativeLayout.StructHash ? "" : "Your Dalamud build uses a different HUD storage format. This package needs a matching build before it can apply or restore layouts.";
        }
        catch (Exception ex) { compatibilityError = $"Could not verify compatibility: {ex.Message}"; }
        Commands.AddHandler("/monklayout", new CommandInfo((_, _) => OpenPreset(JobPreset.Monk)) { HelpMessage = "Open the Monk HUD and hotbar preset." });
        Commands.AddHandler("/whmlayout", new CommandInfo((_, _) => OpenPreset(JobPreset.WhiteMage)) { HelpMessage = "Open the White Mage HUD and hotbar preset." });
        if (Objects.LocalPlayer?.ClassJob.RowId == 24) SelectPreset(JobPreset.WhiteMage);
        Interface.UiBuilder.Draw += Draw;
        Interface.UiBuilder.OpenMainUi += Open;
        Interface.UiBuilder.OpenConfigUi += Open;
        Framework.Update += OnUpdate;
    }

    private void SelectPreset(JobPreset value)
    {
        preset = value;
        status = $"Switch to your level 100 {preset.Name}, then press Apply {preset.Name} layout.";
    }

    private void OpenPreset(JobPreset value) { if (busy || pending != 0) return; SelectPreset(value); open = true; }
    private void Open() => OpenPreset(Objects.LocalPlayer?.ClassJob.RowId == 24 ? JobPreset.WhiteMage : JobPreset.Monk);
    private string CharacterDirectory => characterId != 0
        ? Path.Combine(directory, characterId.ToString("X16"))
        : throw new InvalidOperationException("The character identity has not been verified yet. No settings were changed.");
    private string JobDirectory => Path.Combine(CharacterDirectory, preset.BackupFolder);
    private string OriginalPath => Path.Combine(JobDirectory, "original-settings.json");
    private string StatePath => Path.Combine(JobDirectory, "install-state.json");

    private static PlayerSession ReadSession()
    {
        var local = Objects.LocalPlayer;
        return new(Client.IsLoggedIn, local != null, local?.ClassJob.RowId ?? 0, local?.Level ?? 0,
            Player.IsLoaded, Player.ClassJob.RowId, Player.EffectiveLevel, Player.IsLevelSynced, Player.ContentId);
    }

    private void Draw()
    {
        if (!open) return;
        viewport = ImGui.GetMainViewport().Size;
        ImGui.SetNextWindowSize(new Vector2(740, 650), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Monk and White Mage Layouts###MonkLayout", ref open))
        {
            ImGui.TextUnformatted($"{preset.Name} Layout {PluginVersion} | Keyboard and mouse | PvE");
            ImGui.TextWrapped(ReadSession().Summary);
            ImGui.BeginDisabled(busy || pending != 0);
            if (ImGui.RadioButton("Monk", preset == JobPreset.Monk)) SelectPreset(JobPreset.Monk);
            ImGui.SameLine();
            if (ImGui.RadioButton("White Mage", preset == JobPreset.WhiteMage)) SelectPreset(JobPreset.WhiteMage);
            ImGui.EndDisabled();
            ImGui.Separator();
            ImGui.TextWrapped($"{preset.Description} Uses HUD layout {preset.HudLayout + 1} and {preset.Name} hotbars 1-3. Bars 1-3 become unshared; sharing is a character-wide setting.");
            ImGui.Spacing();
            ImGui.Checkbox("Apply recommended keybinds", ref applyKeys);
            if (applyKeys)
                ImGui.TextWrapped("Keyboard bindings apply to every job. This assigns 1-5, R, F, G, Z, X, C and V, plus Shift and Ctrl variants. Menu shortcuts that conflict are cleared. Movement and mouse bindings are protected.");
            else
                ImGui.TextWrapped("Your current keyboard bindings will stay in place. The preview below shows the recommended keys only.");
            ImGui.SliderFloat("HUD size", ref size, .8f, 1.2f, "%.2fx");
            ImGui.SliderFloat("Row spacing", ref rowGap, 0, 16, "%.0f");
            ImGui.TextWrapped("Compact spacing groups the hotbars, HP and job gauge together. Press Apply after adjusting either slider. Ctrl+Z/X/C/V remain available for clipboard shortcuts.");
            ImGui.TextWrapped($"The first backup for each job stays on this PC. Restore original settings returns HUD {preset.HudLayout + 1}, this job's bars and any changed keys to that job's backup. You can disable the plugin after applying.");
            ImGui.Spacing();
            if (compatibilityError.Length > 0) ImGui.TextWrapped(compatibilityError);
            ImGui.BeginDisabled(busy || pending != 0 || compatibilityError.Length > 0);
            if (ImGui.Button($"Apply {preset.Name} layout", new Vector2(225, 36))) pending = 1;
            ImGui.SameLine();
            ImGui.BeginDisabled(characterId == 0 || !File.Exists(OriginalPath));
            if (ImGui.Button("Restore original settings", new Vector2(225, 36))) pending = 2;
            ImGui.EndDisabled();
            ImGui.EndDisabled();
            ImGui.Spacing();
            ImGui.TextWrapped(status);
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Key and skill preview", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("MonkKeys", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 290)))
                {
                    ImGui.TableSetupColumn("Key");
                    ImGui.TableSetupColumn("Normal");
                    ImGui.TableSetupColumn("Shift");
                    ImGui.TableSetupColumn("Ctrl");
                    ImGui.TableHeadersRow();
                    for (var s = 0; s < 12; s++)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); ImGui.TextUnformatted(((char)LayoutPlan.Keys[s]).ToString());
                        for (var b = 0; b < 3; b++)
                        {
                            var action = preset.Actions[b * 12 + s];
                            ImGui.TableNextColumn(); ImGui.TextWrapped(action.Label + (action.Action != null && !action.Bind ? " [click]" : ""));
                        }
                    }
                    ImGui.EndTable();
                }
            }
        }
        ImGui.End();
    }

    private void OnUpdate(IFramework _)
    {
        if (open || pending != 0)
        {
            try { characterId = compatibilityError.Length == 0 ? NativeLayout.GetLoadedCharacterId(ReadSession()) : 0; }
            catch { characterId = 0; } // The operation below reports any readiness error.
        }
        if (pending == 0 || busy) return;
        var command = pending; pending = 0; busy = true;
        try
        {
            if (compatibilityError.Length > 0) throw new InvalidOperationException(compatibilityError);
            var session = ReadSession();
            Log.Information("Job Layout {Version}, preset {Job}: {Session}", PluginVersion, preset.Name, session.Summary);
            characterId = NativeLayout.CheckReady(session, Condition, Gui, preset, command == 1);
            Directory.CreateDirectory(JobDirectory);
            if (command == 1) Apply(); else Restore();
        }
        catch (Exception ex)
        {
            status = ex.Message;
            Chat.PrintError($"[{preset.Name} Layout] {ex.Message}");
            Log.Error(ex, "Job layout operation failed");
        }
        finally { busy = false; }
    }

    private void Apply()
    {
        // Resolve every action and key conflict before editing any game settings.
        var plan = NativeLayout.ResolveActions(Data, preset);
        var globalScale = NativeLayout.GetHudGlobalScale(Gui);
        _ = LayoutPlan.GetGeometry(viewport.X, viewport.Y, size, globalScale, rowGap);
        var keys = applyKeys ? NativeLayout.KeyChanges(preset) : new Dictionary<int, Binding>();
        var before = NativeLayout.Capture(characterId, GameConfig, preset);
        var previousState = LoadState();
        if (File.Exists(OriginalPath)) NativeLayout.ValidateBackup(ReadBackup(OriginalPath), characterId, preset);
        else SaveJson(OriginalPath, before, false);
        var transactionPath = Path.Combine(JobDirectory, $"before-apply-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}.json");
        SaveJson(transactionPath, before, false);
        // Save the restore index before editing, so it survives an interrupted apply.
        var nextState = new InstallState
        {
            HasApplied = true,
            KeysApplied = applyKeys || previousState.KeysApplied,
            KeyInputsChanged = previousState.KeyInputsChanged.Union(keys.Keys).ToHashSet()
        };
        SaveJson(StatePath, nextState, true);
        try
        {
            NativeLayout.Apply(plan, before, GameConfig, keys, applyKeys, viewport.X, viewport.Y, size, globalScale, rowGap, preset);
            status = $"Applied and verified: HUD {preset.HudLayout + 1}, all 36 {preset.Name} slots, and the selected keyboard changes. Your original settings are backed up.";
            Chat.Print($"[{preset.Name} Layout] {status}");
        }
        catch (Exception applyError)
        {
            try
            {
                NativeLayout.Restore(before, keys.Keys, GameConfig, characterId, preset, applyKeys, true);
                SaveJson(StatePath, previousState, true);
            }
            catch (Exception restoreError)
            {
                // Keep the failed-apply snapshot as the restore target. It is the
                // only snapshot that can undo a partial apply without losing later edits.
                SaveJson(Path.Combine(JobDirectory, "recovery-settings.json"), before, true);
                throw new InvalidOperationException($"Apply stopped: {applyError.Message} Automatic restore also stopped: {restoreError.Message} Use Restore original settings for recovery.", restoreError);
            }
            throw new InvalidOperationException($"Apply stopped and the previous settings were restored. {applyError.Message}", applyError);
        }
    }

    private void Restore()
    {
        var state = LoadState();
        var recovery = Path.Combine(JobDirectory, "recovery-settings.json");
        var backup = ReadBackup(File.Exists(recovery) ? recovery : OriginalPath);
        NativeLayout.Restore(backup, state.KeyInputsChanged, GameConfig, characterId, preset, state.KeysApplied);
        if (File.Exists(recovery)) File.Delete(recovery);
        SaveJson(StatePath, new InstallState(), true);
        status = "Original settings restored and hotbars verified.";
        Chat.Print($"[{preset.Name} Layout] {status}");
    }

    private InstallState LoadState() => File.Exists(StatePath)
        ? JsonSerializer.Deserialize<InstallState>(File.ReadAllText(StatePath), Json) ?? throw new InvalidOperationException("The local restore index could not be read.")
        : new InstallState();

    private static Backup ReadBackup(string path) => JsonSerializer.Deserialize<Backup>(File.ReadAllText(path), Json)
        ?? throw new InvalidOperationException("The local backup could not be read.");

    private static void SaveJson<T>(string path, T value, bool overwrite)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(fs, value, Json);
                fs.Flush(true);
            }
            File.Move(temp, path, overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public void Dispose()
    {
        Framework.Update -= OnUpdate;
        Interface.UiBuilder.Draw -= Draw;
        Interface.UiBuilder.OpenMainUi -= Open;
        Interface.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/monklayout");
        Commands.RemoveHandler("/whmlayout");
    }
}
