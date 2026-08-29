namespace Fahrenheit.Mods.Qol;

/// <summary>
///     Keeps the analog sticks neutral while the window does not own input.
///
///     <para>
///     The engine's own handling is the bug: while its window-active flag is clear,
///     <c>AsyncControllerTaskManager::GetControllerInfo</c> writes zeroes to both the button word
///     and the four axis words. Zero is correct for buttons, where it means nothing is pressed. For
///     an axis whose rest position sits in the middle of its range, zero is full deflection, which
///     is why the character walks and the menu cursor climbs while the game is in the background.
///     </para>
///
///     <para>
///     The neutral word is not hardcoded because the axis packing has not been read off the running
///     game yet. Until <see cref="QolConfig.AxisNeutral"/> is set, this module only reports what the
///     axes hold while focused, which is what the value has to be derived from.
///     </para>
/// </summary>
[FhLoad(FhGameId.FFX)]
public unsafe sealed class FocusInputModule : FhModule
{
    private QolConfig _config = new();

    private bool _was_focused = true;
    private long _corrections;
    private int  _samples_logged;

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate void d_get_controller_info(nint ptr_this, uint* buttons, uint* axes);

    public override bool init(FhModContext mod_context, FileStream global_state_file)
    {
        _config = QolConfig.Load(Path.Combine(AppContext.BaseDirectory, "fhqol.config.json"));

        if (!new FhMethodHandle<d_get_controller_info>(new FhMethodLocation(EngineAddresses.GetControllerInfo, 0)).hook(this, h_get_controller_info))
        {
            _logger.Error("[QoL] Could not hook GetControllerInfo; the focus-loss drift stays.");
            return false;
        }

        _logger.Info(_config.AxisNeutral.HasValue
            ? $"[QoL] Focus fix active, neutral axis word 0x{_config.AxisNeutral.Value:X8}."
            : "[QoL] Focus fix in survey mode: axis words are logged while focused. Set axis_neutral in fhqol.config.json to enable the correction.");

        return true;
    }

    private void h_get_controller_info(nint ptr_this, uint* buttons, uint* axes)
    {
        new FhMethodHandle<d_get_controller_info>(new FhMethodLocation(EngineAddresses.GetControllerInfo, 0))
            .chain_from(h_get_controller_info).fnptr!(ptr_this, buttons, axes);

        bool focused = FhUtil.get_at<int>(EngineAddresses.ODBegin) != 0;

        if (focused)
        {
            // One sample per regained focus is enough to read the packing without flooding the log.
            if (!_was_focused || _samples_logged < 3)
            {
                if (axes != null && _samples_logged < 16)
                {
                    _samples_logged++;
                    _logger.Info($"[QoL] Axes while focused: {axes[0]:X8} {axes[1]:X8} {axes[2]:X8} {axes[3]:X8}");
                }
            }

            _was_focused = true;
            return;
        }

        if (_was_focused)
        {
            _was_focused = false;
            _logger.Info("[QoL] Focus lost.");
        }

        if (_config.AxisNeutral is not uint neutral || axes == null) return;

        axes[0] = neutral;
        axes[1] = neutral;
        axes[2] = neutral;
        axes[3] = neutral;

        _corrections++;
    }
}
