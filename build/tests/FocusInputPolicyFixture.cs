namespace Fahrenheit.Mods.Qol;

public unsafe sealed partial class FocusInputModule
{
    private const int RefocusGraceScans = 3;
    private int _refocus_grace;
    private readonly Toggle _neutralize = new();
    public bool Focused = true;
    public int FocusReads;
    public bool Enabled { get => _neutralize.Value; set => _neutralize.Value = value; }
    public int Grace => _refocus_grace;
    private bool WindowOwnsInput() { FocusReads++; return Focused; }
    public void Ready() { lock (_focusPolicyGate) _vrrPolicyReady = true; }
    public bool Scan() => observe_scan_policy(out _, out _);
    public bool PolicyLockHeld => Monitor.IsEntered(_focusPolicyGate);
    public Action? Native;
    public int NativeCalls;
    public bool PadNeutral;
    private long _neutralized;
    private void write_neutral_pad_records() => PadNeutral = true;
    private void forward_tk_scan_controler() { NativeCalls++; Native?.Invoke(); }
    public void Publish()
    {
        bool neutralized = begin_focus_scan(out _, out long generation);
        invoke_vrr_input_publication(generation, neutralized);
    }
    private sealed class Toggle { public bool Value = true; public bool get() => Value; }
}
