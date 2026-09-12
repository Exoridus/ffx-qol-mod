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
}
