using Hexa.NET.ImGui;

namespace Fahrenheit.Mods.Qol;

/// <summary>Requests native game over on a fresh Start press while L1 and R1 are held.</summary>
[FhLoad(FhGameId.FFX)]
public unsafe sealed class NativeResetModule : FhModule
{
    private readonly FhSettingToggle _enabled = new("fhqol.reset.enabled", true);
    private readonly ResetChord _chord = new();
    private bool _ready, _frameworkResetPresent;

    public NativeResetModule() => _ = new Settings(this);

    public override bool init(FhModContext mod_context, FileStream global_state_file)
    {
        if (!NativeBuild.TryValidate(out string error))
        {
            _logger.Error(error);
            return false;
        }
        foreach (var context in FhApi.Mods.get_modules())
            if (context.Module.GetType().FullName == "Fahrenheit.Runtime.FhSoftResetModule")
                _frameworkResetPresent = true;
        _ready = true;
        _logger.Info("[QoL] Native controller reset ready: hold L1/R1, then press Start/Options.");
        return true;
    }

    public override void render_imgui()
    {
        if (!_ready) return;
        nint foreground = GetForegroundWindow();
        _ = GetWindowThreadProcessId(foreground, out uint process);
        bool frameworkHandlesPress = _frameworkResetPresent
            && ImGui.IsKeyDown(ImGuiKey.GamepadL1) && ImGui.IsKeyDown(ImGuiKey.GamepadR1)
            && ImGui.IsKeyPressed(ImGuiKey.GamepadStart);
        if (!_chord.Update(_enabled.get(), foreground != 0 && process == (uint)Environment.ProcessId,
            FhApi.Input.l1.is_pressed, FhApi.Input.r1.is_pressed, FhApi.Input.start.is_pressed, frameworkHandlesPress)) return;

        _logger.Info("[QoL] Native controller reset requested.");
        if (FhCall.MsBattleCheck.fnptr!() != 0)
        {
            FhUtil.set_at<ushort>(EngineAddresses.BattleEndType, 1);
            FhUtil.set_at<byte>(EngineAddresses.BattleState, 2);
            return;
        }
        FhCall.graphicDestroyFmv.fnptr!();
        FhCall.AtelJumpGameOver.fnptr!();
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint process);

    private sealed class Settings(NativeResetModule module) : FhSettingProvider<NativeResetModule>
    {
        protected override IEnumerable<FhSetting> get() => [module._enabled];
    }
}
