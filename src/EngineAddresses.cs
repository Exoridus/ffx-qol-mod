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
    ///     AsyncControllerTaskManager::GetControllerInfo(button_word*, axes*). Thiscall.
    ///     While ODBegin is clear it writes zeroes to both outputs, which is correct for the buttons
    ///     and wrong for the axes, whose neutral position is the middle of their range.
    /// </summary>
    public const nint GetControllerInfo = 0x28CE70;

    /// <summary>int. Set while the window owns input; the engine's window-active flag.</summary>
    public const nint ODBegin = 0xF3C8F0;

    /// <summary>inputSetInputInfoToCurrentFrame. Cdecl. Called from the main loop while ODBegin is set.</summary>
    public const nint InputSetInputInfoToCurrentFrame = 0x230F90;

    /// <summary>Virtuos::InputManager::getStickInfo. The accessor gameplay reads the sticks through.</summary>
    public const nint InputManagerGetStickInfo = 0x2314C0;
}
