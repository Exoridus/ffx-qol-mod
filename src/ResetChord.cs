namespace Fahrenheit.Mods.Qol;

internal sealed class ResetChord
{
    private bool _startWasDown = true;

    internal bool Update(bool enabled, bool focused, bool l1, bool r1, bool start, bool frameworkHandlesPress)
    {
        bool activate = enabled && focused && l1 && r1 && start && !_startWasDown && !frameworkHandlesPress;
        _startWasDown = !enabled || !focused || start;
        return activate;
    }
}
