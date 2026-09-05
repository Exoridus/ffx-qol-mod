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

    /// <summary>NeedShowJapanLogo. Cdecl, returns non-zero while the Japan logo gate is open.</summary>
    public const nint NeedShowJapanLogo = 0x387450;

    /// <summary>
    ///     The FMV manager's skip poll. Thiscall. It watches Triangle to arm a skip and Cross to
    ///     commit it; hooking it lets the boot videos be skipped without input.
    /// </summary>
    public const nint FmvSkipPoll = 0x2D9590;

    /// <summary>int. Non-zero while a movie is playing.</summary>
    public const nint GMoviePlay = 0xD2A008;

    /// <summary>byte. The engine's own movie-skip flag.</summary>
    public const nint GMovieSkipFlag = 0x8DED21;

    /// <summary>byte. Non-zero once the menu is up, which is the end of the boot sequence.</summary>
    public const nint MenuState = 0xF407E4;

    /// <summary>int. Id of the event currently running.</summary>
    public const nint EventId = 0xEFBBF8;

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
}
