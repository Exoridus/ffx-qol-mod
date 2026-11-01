namespace Fahrenheit.Mods.Qol;

public unsafe sealed partial class FocusInputModule
{
    private readonly object _focusPolicyGate = new();
    private bool _vrrPolicyReady, _focusPolicyObserved, _focusPolicyFocused, _focusPolicyEnabled;
    private int _focusPolicyGrace;
    private long _focusPolicyGeneration = 1;
    private readonly uint[] _focusBlockedButtons = new uint[EngineAddresses.PadRecordCount];
    private readonly HashSet<nint> _focusInvalidDeviceReads = [];
    private bool _focusRearmPending;

    /// <summary>
    /// Reports version 1 focus neutralization without consuming a scan or changing pad input.
    /// Returns false until initialization succeeds, for an unsupported version, or while focused
    /// buttons still require release after neutralization. Native publication remains available
    /// during rearming, with only quarantined buttons masked. The generation
    /// changes on observed focus, setting, grace or button-ownership changes; callers must recheck
    /// it at publication. A failed device read cannot confirm release until that device recovers.
    /// This query is a source compatibility contract, not qualification of native input replay.
    /// </summary>
    public bool TryGetVrrInputPolicy(int contractVersion, out long generation, out bool neutralize)
    {
        generation = 0;
        neutralize = false;
        lock (_focusPolicyGate)
        {
            if (contractVersion != 1 || !_vrrPolicyReady || _vrrPublicationFaulted) return false;
            neutralize = observe_focus_policy(WindowOwnsInput(), _neutralize.get());
            generation = _focusPolicyGeneration;
            return neutralize || !_focusRearmPending && !Array.Exists(_focusBlockedButtons, mask => mask != 0);
        }
    }

    private object? _vrrPublicationOwner;
    private Action<long, bool>? _vrrPublicationBefore;
    private Action? _vrrPublicationAfter;
    private bool _vrrPublicationFaulted, _vrrPublicationActive;

    /// <summary>
    /// Binds version 1 publication callbacks, optionally before initialization. The same owner and
    /// callback instances may bind repeatedly; another binding is refused. Once initialized,
    /// before receives the actual scan generation and neutralization decision after all pad writes,
    /// followed by one native continuation and after in finally. Callbacks execute without the
    /// policy lock. Their exceptions revoke cooperation and query readiness without skipping native
    /// publication or masking its exception. This contract does not qualify native replay.
    /// </summary>
    public bool TryBindVrrInputPublication(int version, object owner, Action<long, bool> before, Action after)
    {
        lock (_focusPolicyGate)
        {
            if (version != 1 || owner is null || before is null || after is null) return false;
            if (_vrrPublicationOwner is not null)
                return ReferenceEquals(owner, _vrrPublicationOwner) && ReferenceEquals(before, _vrrPublicationBefore)
                    && ReferenceEquals(after, _vrrPublicationAfter);
            _vrrPublicationOwner = owner;
            _vrrPublicationBefore = before;
            _vrrPublicationAfter = after;
            return true;
        }
    }

    private bool begin_focus_scan(out bool focused, out long generation)
    {
        bool neutralized = observe_scan_policy(out focused, out generation);
        lock (_focusPolicyGate)
        {
            for (int port = 0; port < EngineAddresses.PadRecordCount; port++)
            {
                uint buttons = read_pad_buttons(port);
                if (neutralized || _focusRearmPending)
                    set_blocked_buttons(port, _focusBlockedButtons[port] | buttons);
                if (!neutralized) write_pad_buttons(port, buttons & ~_focusBlockedButtons[port]);
            }
            if (neutralized)
            {
                write_neutral_pad_records();
                _neutralized++;
            }
            else if (_focusRearmPending)
            {
                _focusRearmPending = false;
                _focusPolicyGeneration++;
            }
            generation = _focusPolicyGeneration;
        }
        return neutralized;
    }

    private void observe_focus_buttons(int port, uint buttons)
    {
        lock (_focusPolicyGate)
        {
            bool neutralized = observe_focus_policy(WindowOwnsInput(), _neutralize.get());
            if (!_focusPolicyEnabled) return;
            // Publication may reread our own zeroes; only a native refresh can confirm release.
            if (neutralized || _focusRearmPending)
                set_blocked_buttons(port, _focusBlockedButtons[port] | buttons);
            else if (_focusInvalidDeviceReads.Count == 0)
                set_blocked_buttons(port, _focusBlockedButtons[port] & buttons);
        }
    }

    private bool focus_button_observation_required()
    {
        lock (_focusPolicyGate)
            return _focusRearmPending || _focusInvalidDeviceReads.Count != 0
                || Array.Exists(_focusBlockedButtons, mask => mask != 0);
    }

    private void observe_focus_device_read(nint device, bool matchesPublishedState)
    {
        lock (_focusPolicyGate)
        {
            bool changed = matchesPublishedState ? _focusInvalidDeviceReads.Remove(device) : _focusInvalidDeviceReads.Add(device);
            if (changed) _focusPolicyGeneration++;
        }
    }

    private void set_blocked_buttons(int port, uint buttons)
    {
        if (_focusBlockedButtons[port] == buttons) return;
        _focusBlockedButtons[port] = buttons;
        _focusPolicyGeneration++;
    }

    private void invoke_vrr_input_publication(long generation, bool neutralized)
    {
        Action<long, bool>? before = null;
        Action? after = null;
        lock (_focusPolicyGate)
        {
            if (_vrrPolicyReady && !_vrrPublicationFaulted && _vrrPublicationBefore is not null)
            {
                if (_vrrPublicationActive) _vrrPublicationFaulted = true;
                else
                {
                    _vrrPublicationActive = true;
                    before = _vrrPublicationBefore;
                    after = _vrrPublicationAfter;
                }
            }
        }
        try
        {
            try { before?.Invoke(generation, neutralized); }
            catch (Exception) { lock (_focusPolicyGate) _vrrPublicationFaulted = true; }
            forward_tk_scan_controler();
        }
        finally
        {
            try { after?.Invoke(); }
            catch (Exception) { lock (_focusPolicyGate) _vrrPublicationFaulted = true; }
            finally
            {
                if (before is not null)
                    lock (_focusPolicyGate) _vrrPublicationActive = false;
            }
        }
    }

    private bool observe_scan_policy(out bool focused, out long generation)
    {
        lock (_focusPolicyGate)
        {
            focused = WindowOwnsInput();
            if (!focused) _refocus_grace = RefocusGraceScans;
            else if (_refocus_grace > 0) _refocus_grace--;
            bool neutralized = observe_focus_policy(focused, _neutralize.get());
            generation = _focusPolicyGeneration;
            return neutralized;
        }
    }

    private bool observe_focus_policy(bool focused, bool enabled)
    {
        if (!_focusPolicyObserved || focused != _focusPolicyFocused || enabled != _focusPolicyEnabled
            || _refocus_grace != _focusPolicyGrace)
        {
            _focusPolicyGeneration++;
            _focusPolicyObserved = true;
            _focusPolicyFocused = focused;
            _focusPolicyEnabled = enabled;
            _focusPolicyGrace = _refocus_grace;
        }
        bool neutralized = enabled && (!focused || _refocus_grace > 0);
        if (neutralized && !_focusRearmPending)
        {
            _focusRearmPending = true;
            _focusPolicyGeneration++;
        }
        else if (!enabled)
        {
            if (_focusRearmPending) { _focusRearmPending = false; _focusPolicyGeneration++; }
            for (int port = 0; port < EngineAddresses.PadRecordCount; port++) set_blocked_buttons(port, 0);
        }
        return neutralized;
    }
}
