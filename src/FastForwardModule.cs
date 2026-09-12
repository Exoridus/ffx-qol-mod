namespace Fahrenheit.Mods.Qol;

/// <summary>
///     Hold a button to run the simulation faster, in the states the shipped speed booster refuses.
///
///     <para>
///     The booster itself is one function. <c>Sg_MainLoop</c> opens by calling
///     <c>SpdCtrl_ScaleElapsedTime</c>, which returns the elapsed time the pass is given multiplied
///     by a factor from a three-entry table - 1.0, 2.0, 4.0 - indexed by the global the pause menu
///     cycles. Nothing further down is told about speed: the pass count is derived from that time as
///     <c>floor(elapsed * 30)</c> and <c>updateFFX</c> repeats the loop that many times, drawing only
///     the last. So the whole feature is the value this function returns.
///     </para>
///
///     <para>
///     Its gate is what excludes cutscenes, and the exclusion is the condition rather than a check
///     of its own: the factor applies only where the player has control or a battle is running. A
///     cutscene is by definition neither, so the function returns the delta unchanged however the
///     booster is set. That is why writing the booster global would do nothing here, and why this
///     module overrides the return value instead.
///     </para>
///
///     <para>
///     Two things have to come with it. The multi-pass gate <c>FUN_0081fe40</c> vetoes extra
///     simulation passes for a long list of scenes, and <c>SpdCtrl_IsSpeedingUp</c> is the first
///     thing it tests and the only way past it - a scaled delta with the veto in place buys nothing.
///     And voice and video do not scale, which the shipped booster answers by calling
///     <c>Sg_SetMuteSound(1)</c> while it runs; in the states this module reaches, the engine has not
///     done that, so it does it here.
///     </para>
///
///     <para>
///     Where the engine's own booster is already active this module stays out of the way: it
///     overrides a return value only when the engine returned the delta unchanged. Two factors
///     multiplied together is not a mode anyone asked for.
///     </para>
///
///     <para>
///     Not covered, and it cannot be from here: the three syncdata-paced scenes - Seymour
///     <c>mcyt0600</c>, Home <c>azit0300</c>, Yunalesca <c>dome0600</c>. Those do not run on the
///     wall clock at all; <c>FUN_00821f90</c> busy-waits until the recorded PS2 frame time catches
///     up, so a larger delta is simply waited away.
///     </para>
/// </summary>
[FhLoad(FhGameId.FFX)]
public unsafe sealed class FastForwardModule : FhModule
{
    private readonly FhSettingToggle _enabled = new("fhqol.ff.enabled", true);
    private readonly FhSettingToggle _mute    = new("fhqol.ff.mute_while_held", true);
    private readonly FhSettingToggle _survey  = new("fhqol.ff.survey", false);

    /// <summary>
    ///     How much faster to run while the button is held. The shipped booster offers 2 and 4; 4 is
    ///     too fast to follow a conversation and is here for skipping rather than watching. Values
    ///     that are not whole numbers are fine - the pass count carries its remainder forward, so
    ///     1.5 averages correctly rather than rounding away.
    /// </summary>
    private readonly FhSettingNumber<float> _factor = new("fhqol.ff.factor", 2.0f, 1.0f, 4.0f, 0.5f);

    /// <summary>
    ///     Which button to hold, as a mask for the engine's own button word. The default is the one
    ///     the shipped booster polls, which is also the one already bound to speeding up, so it needs
    ///     no key assignment of its own.
    ///
    ///     Turn <c>fhqol.ff.survey</c> on for one run to pick a different button: it logs the raw
    ///     word whenever it changes, so pressing the intended button once names its bit.
    /// </summary>
    private readonly FhSettingNumber<int> _button = new("fhqol.ff.button_mask", 0x10000, 0, int.MaxValue, 1);

    private bool _held;
    private bool _muted;
    private long _overrides;
    private long _engine_active;
    private int  _probe_logged;
    private int  _last_survey_word;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate double d_spd_ctrl_scale_elapsed_time(float elapsed);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte d_spd_ctrl_is_speeding_up();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void d_sg_set_mute_sound(int mute);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte d_input_is_press_button(int mask);

    public FastForwardModule()
    {
        settings = new FhSettingsCategory("fhqol.ff", [_enabled, _factor, _button, _mute, _survey]);
    }

    public override bool init(FhModContext mod_context, FileStream global_state_file)
    {
        if (!_enabled.get())
        {
            _logger.Info("[QoL] Fast forward disabled by config.");
            return true;
        }

        bool ok = new FhMethodHandle<d_spd_ctrl_scale_elapsed_time>(new FhMethodLocation(EngineAddresses.SpdCtrlScaleElapsedTime, 0)).hook(this, h_scale_elapsed_time)
               && new FhMethodHandle<d_spd_ctrl_is_speeding_up>(new FhMethodLocation(EngineAddresses.SpdCtrlIsSpeedingUp, 0)).hook(this, h_is_speeding_up);

        _logger.Info(ok
            ? $"[QoL] Fast forward armed: hold button mask 0x{_button.get():X} for {_factor.get():F1}x."
            : "[QoL] Fast forward could not install its hooks.");

        return ok;
    }

    /// <summary>
    ///     The whole feature. Chain first so the engine's own gate runs and its side effects - the
    ///     mute, the menu callbacks - happen where they should, then override the result only when
    ///     the engine left the delta alone.
    /// </summary>
    private double h_scale_elapsed_time(float elapsed)
    {
        var orig = new FhMethodHandle<d_spd_ctrl_scale_elapsed_time>(new FhMethodLocation(EngineAddresses.SpdCtrlScaleElapsedTime, 0))
            .chain_from(h_scale_elapsed_time).fnptr;

        double engine = orig is null ? elapsed : orig(elapsed);

        _held = read_button();
        survey();

        // A result the engine already scaled is the shipped booster doing its job. Multiplying that
        // by a second factor is not a mode anyone asked for, and the engine has muted sound for it
        // already.
        bool engine_scaled = engine > elapsed * 1.0001;
        if (engine_scaled) _engine_active++;

        apply_mute(_held && !engine_scaled);

        if (!_held || engine_scaled) return engine;

        _overrides++;

        double scaled = elapsed * _factor.get();

        // The narrowing from the engine's long double to a double is the one thing about this hook
        // that cannot be argued from the decompilation, so the first few results are logged and the
        // question is answered from a run rather than from the reasoning.
        if (_probe_logged < 5)
        {
            _probe_logged++;
            _logger.Info($"[QoL] Fast forward: elapsed={elapsed:F6}s engine={engine:F6}s scaled={scaled:F6}s.");
        }

        return scaled;
    }

    /// <summary>
    ///     Answers the multi-pass gate, which asks this before anything else and gives up on the
    ///     scene if the answer is no. Left alone when the engine's own booster is on, so its state
    ///     still decides in the places it governs.
    /// </summary>
    private byte h_is_speeding_up()
    {
        var orig = new FhMethodHandle<d_spd_ctrl_is_speeding_up>(new FhMethodLocation(EngineAddresses.SpdCtrlIsSpeedingUp, 0))
            .chain_from(h_is_speeding_up).fnptr;

        byte engine = orig is null ? (byte)0 : orig();

        return engine != 0 || _held ? (byte)1 : (byte)0;
    }

    private bool read_button()
    {
        int mask = _button.get();
        if (mask == 0) return false;

        var poll = new FhMethodHandle<d_input_is_press_button>(new FhMethodLocation(EngineAddresses.InputIsPressButton, 0)).fnptr;
        return poll is not null && poll(mask) != 0;
    }

    /// <summary>
    ///     Muted while this module is the one speeding things up, and unmuted on release. Tracked
    ///     rather than written every frame: Sg_SetMuteSound is not a flag setter, it walks the mixer,
    ///     and the engine's own booster calls it once per transition for the same reason.
    /// </summary>
    private void apply_mute(bool want)
    {
        if (!_mute.get()) want = false;
        if (want == _muted) return;

        var mute = new FhMethodHandle<d_sg_set_mute_sound>(new FhMethodLocation(EngineAddresses.SgSetMuteSound, 0)).fnptr;
        if (mute is null) return;

        mute(want ? 1 : 0);
        _muted = want;

        // One line per press rather than per frame, which is also the only place the counters are
        // worth reading: how often this module overrode the delta, and how often the shipped booster
        // was already doing it and was left alone.
        if (!want) _logger.Info($"[QoL] Fast forward released: overrides={_overrides}, engine booster active={_engine_active}.");
    }

    /// <summary>
    ///     Logs the engine's button word whenever it changes, so one run names the bit of whichever
    ///     button the player wants to hold. Off by default; it is a line per press.
    /// </summary>
    private void survey()
    {
        if (!_survey.get()) return;

        var poll = new FhMethodHandle<d_input_is_press_button>(new FhMethodLocation(EngineAddresses.InputIsPressButton, 0)).fnptr;
        if (poll is null) return;

        int word = 0;
        for (int bit = 0; bit < 32; bit++)
            if (poll(1 << bit) != 0) word |= 1 << bit;

        if (word == _last_survey_word) return;
        _last_survey_word = word;

        if (word != 0) _logger.Info($"[QoL] Buttons held: 0x{word:X}.");
    }

}
