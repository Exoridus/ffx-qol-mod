namespace Fahrenheit.Mods.Qol;

/// <summary>
///     Hook targets and globals, as RVAs relative to the FFX.exe image base.
///     Values derive from the knowledge base function catalog, whose addresses are Ghidra VAs:
///     RVA = VA - 0x400000.
/// </summary>
public static class EngineAddresses
{
    // --- Startup skip ---

    /// <summary>AtelEventSetUp(event_id). Cdecl. Entry point for switching to an event.</summary>
    public const nint AtelEventSetUp = 0x472E90;

    /// <summary>AtelGetEventName(event_id). Cdecl, returns a pointer to the event's name.</summary>
    public const nint AtelGetEventName = 0x4796E0;

    /// <summary>
    ///     Reads the opening-screen flag. Cdecl, no arguments, returns a byte. graphicInitScene
    ///     runs its Flash render loop for as long as this stays non-zero, so answering zero ends
    ///     the loop before its first pass. The flag is set only by the routine that loads
    ///     OpeningScreen.swf and read only here.
    /// </summary>
    public const nint OpeningScreenActive = 0x260580;

    /// <summary>
    ///     The ATEL script getter behind the Common namespace's pressed-buttons query, function id
    ///     0x0044. Cdecl, no arguments, returns a ushort. A pure getter: it reads the engine's
    ///     current button word and translates it through the key-assignment table, with no side
    ///     effect of its own.
    /// </summary>
    public const nint AtelPressedButtons = 0x45D350;

    // --- Focus handling ---

    /// <summary>
    ///     TkScanControler. Cdecl, no arguments. The main loop calls it once per frame; it pushes
    ///     one sample per port into a four-slot input history ring through FUN_00889700.
    /// </summary>
    public const nint TkScanControler = 0x489350;

    /// <summary>
    ///     First pad record. FUN_00888e30 is address arithmetic on a static array,
    ///     (port + pad) * 0x100 + 0x01330248, so the record needs no call to reach.
    ///     Analog axes are bytes at +0x84 and +0x90 with 0x80 as the rest position; the button
    ///     words FUN_00889700 copies into the ring slot are at +0x98 and +0x9a.
    /// </summary>
    public const nint PadRecordBase = 0xF30248;

    public const int PadRecordStride = 0x100;

    /// <summary>TkScanControler iterates ports 0..1.</summary>
    public const int PadRecordCount = 2;

    // --- Speed control ---

    /// <summary>
    ///     SpdCtrl_ScaleElapsedTime(float). Cdecl. The first call Sg_MainLoop makes, and the whole
    ///     of the shipped speed booster: it returns the elapsed time the pass is given, multiplied
    ///     by a factor from the three-entry table at VA 0x00C38E14 (1.0, 2.0, 4.0) indexed by the
    ///     global the pause menu cycles. Everything downstream - the pass count, and with it how
    ///     far the simulation advances - follows from that one return value.
    ///
    ///     The factor is applied only where the player has control or a battle is running, and not
    ///     while a movie plays, a menu is open, or the event is 0x163 or 0x3e. A cutscene fails the
    ///     first of those by definition, which is why the shipped booster does nothing there.
    ///
    ///     Returns a long double in ST(0). A managed delegate cannot express that, so this module
    ///     declares the return as a double: on x86 both come back in ST(0), and the 80-to-64-bit
    ///     narrowing is far below the resolution of a frame delta.
    /// </summary>
    public const nint SpdCtrlScaleElapsedTime = 0x2F7430;

    /// <summary>
    ///     SpdCtrl_IsSpeedingUp(). Cdecl, no arguments, returns a bool. Read in exactly one place,
    ///     and that place matters: it is the first thing the multi-pass gate FUN_0081fe40 tests,
    ///     and a non-zero answer returns 1 from it before any of the per-scene checks run. So this
    ///     is not a status flag, it is the veto lift - without it the gate refuses the extra
    ///     simulation passes that a scaled delta asks for.
    /// </summary>
    public const nint SpdCtrlIsSpeedingUp = 0x2F7420;

    /// <summary>
    ///     Sg_SetMuteSound(int). Cdecl. The shipped booster calls this with 1 when it engages and 0
    ///     when it stops, which is how the game answers the problem that voice and video do not
    ///     scale with the simulation. A fast forward that reaches states the booster does not has
    ///     to do the same for itself.
    /// </summary>
    public const nint SgSetMuteSound = 0x421D80;

    /// <summary>
    ///     inputIsPressButton(mask). Cdecl, returns non-zero on the one scan in which a bit of the
    ///     mask goes down: InputManager::isPressButton tests the previous word clear and the current
    ///     word set. Reads the engine's button words, so it is already past key assignment and pad
    ///     abstraction. The shipped booster polls it with 0x10000.
    /// </summary>
    public const nint InputIsPressButton = 0x230EA0;

    /// <summary>
    ///     inputIsHoldButton(mask). Cdecl, returns non-zero while any bit of the mask is down:
    ///     InputManager::isHoldButton is the current word and the mask, nothing else. This is the
    ///     one to poll for a hold; the press variant above answers for a single scan.
    /// </summary>
    public const nint InputIsHoldButton = 0x230E80;
}
