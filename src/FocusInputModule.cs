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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void d_set_input_info();

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate uint d_get_stick_info(nint ptr_this);

    private long _controller_info_calls;
    private long _set_input_info_calls;
    private long _get_stick_info_calls;

    public override bool init(FhModContext mod_context, FileStream global_state_file)
    {
        _config = QolConfig.Load(QolConfig.ResolvePath());

        if (!new FhMethodHandle<d_get_controller_info>(new FhMethodLocation(EngineAddresses.GetControllerInfo, 0)).hook(this, h_get_controller_info))
        {
            _logger.Error("[QoL] Could not hook GetControllerInfo; the focus-loss drift stays.");
            return false;
        }

        // Survey probes: the first attempt hooked GetControllerInfo, which never fired, so the layer
        // that actually serves input has to be identified rather than assumed.
        new FhMethodHandle<d_set_input_info>(new FhMethodLocation(EngineAddresses.InputSetInputInfoToCurrentFrame, 0)).hook(this, h_set_input_info);
        new FhMethodHandle<d_get_stick_info>(new FhMethodLocation(EngineAddresses.InputManagerGetStickInfo, 0)).hook(this, h_get_stick_info);

        _logger.Info(_config.AxisNeutral.HasValue
            ? $"[QoL] Focus fix active, neutral axis word 0x{_config.AxisNeutral.Value:X8}."
            : "[QoL] Focus fix in survey mode: axis words are logged while focused. Set axis_neutral in fhqol.config.json to enable the correction.");

        return true;
    }

    private void h_get_controller_info(nint ptr_this, uint* buttons, uint* axes)
    {
        new FhMethodHandle<d_get_controller_info>(new FhMethodLocation(EngineAddresses.GetControllerInfo, 0))
            .chain_from(h_get_controller_info).fnptr!(ptr_this, buttons, axes);

        if (++_controller_info_calls == 1) _logger.Info("[QoL] Probe: GetControllerInfo runs.");

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

    private void h_set_input_info()
    {
        if (++_set_input_info_calls == 1) _logger.Info("[QoL] Probe: inputSetInputInfoToCurrentFrame runs.");

        new FhMethodHandle<d_set_input_info>(new FhMethodLocation(EngineAddresses.InputSetInputInfoToCurrentFrame, 0))
            .chain_from(h_set_input_info).fnptr!();
    }

    private uint h_get_stick_info(nint ptr_this)
    {
        uint value = new FhMethodHandle<d_get_stick_info>(new FhMethodLocation(EngineAddresses.InputManagerGetStickInfo, 0))
            .chain_from(h_get_stick_info).fnptr!(ptr_this);

        bool focused = FhUtil.get_at<int>(EngineAddresses.ODBegin) != 0;

        if (++_get_stick_info_calls <= 3)
            _logger.Info($"[QoL] Probe: getStickInfo returned {value:X8} (focused={focused}).");

        // GetControllerInfo never fires in this build - its probe has not logged once - so the axis
        // packing has to come from the layer that does. getStickInfo returns the same value on every
        // call, which is the shape of a pointer to a static block rather than of packed axis data,
        // so the block behind it is what the neutral word has to be read from.
        //
        // Range-checked against the image before dereferencing: if the return turns out not to be a
        // pointer, the probe reports that and reads nothing, rather than faulting on the input path.
        if (focused && _stick_blocks_logged < 8)
        {
            if (value < ImageBase || value >= ImageEnd)
            {
                if (_stick_blocks_logged++ == 0)
                    _logger.Info($"[QoL] getStickInfo returned {value:X8}, outside the image - not a pointer, not dereferenced.");

                return value;
            }

            uint* block = (uint*)(nint)value;
            _stick_blocks_logged++;
            _logger.Info($"[QoL] Stick block at {value:X8} while focused: " +
                         $"{block[0]:X8} {block[1]:X8} {block[2]:X8} {block[3]:X8} " +
                         $"{block[4]:X8} {block[5]:X8} {block[6]:X8} {block[7]:X8}");
        }

        return value;
    }

    /// <summary>
    ///     The loaded image, used only to decide whether a returned value can be dereferenced.
    ///
    ///     Read from the process rather than assumed. A crash dump of this build put FFX.exe at
    ///     0x00440000 spanning 0x2380000 bytes, so the preferred base and the Ghidra image base are
    ///     both wrong at runtime, and a hardcoded window would reject live pointers or accept dead
    ///     ones depending on where the loader put the image.
    /// </summary>
    private static readonly uint ImageBase =
        (uint)(Process.GetCurrentProcess().MainModule?.BaseAddress ?? 0x400000);

    private static readonly uint ImageEnd =
        ImageBase + (uint)(Process.GetCurrentProcess().MainModule?.ModuleMemorySize ?? 0xA40000);

    private int _stick_blocks_logged;
}
