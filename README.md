# fahrenheit-qol-mod

Three small conveniences for Final Fantasy X HD Remaster, in one Fahrenheit mod.

- **NoSplash** takes the boot sequence straight to the title screen: the splash
  events are redirected, the Japan-logo gate is answered no, and the boot videos
  are skipped without input.
- **NoLauncher** stops the engine reopening `FFX&X-2_LAUNCHER.exe` when the game
  window is closed.
- **FocusInput** holds the pad neutral while the window does not own input, so
  the menu cursor stops scrolling and the character stops walking off.

## The focus bug

The symptom is a menu cursor that walks upward and a character that walks off on
the field, from the moment the window loses focus until it regains it, **with
nothing held down**. That rules out a frozen input state, which with nothing
pressed would read as neutral, and points at a zeroed analog byte: the pad's
analog axes are bytes whose rest position is `0x80`, so a zero is full deflection
rather than centre.

The engine names that rest position itself. `FUN_00888f70` and `FUN_00888fa0` are
its own neutral writers and both store `0x80808080` into the four analog bytes,
which is also what the float-to-byte conversion produces - `FUN_00889a10` is
`-0x80 - (char)round(f * -127.0)`, so `0.0f` maps to `0x80`. This module writes
the same word rather than a value of its own.

The hook sits on `TkScanControler`, which the main loop calls once per frame and
which pushes one sample per port into a four-slot input history ring. Doing it
there is what matters: the script-facing snapshot in `FUN_00871d10` ORs the ring
over every frame since the last sync, so a bad sample keeps being reported as
held rather than lapsing after one frame.

Focus comes from Win32, because the engine has no window-active flag:
`GetForegroundWindow`, `GetActiveWindow` and `WM_ACTIVATE` appear nowhere in the
decompilation and the window belongs to Phyre's `PApplication`.

### What this replaced, and why

The first attempt hooked `AsyncControllerTaskManager::GetControllerInfo`, which
zeroes its outputs while `ODBegin` is clear, and read `ODBegin` as the
window-active flag. Both halves were wrong.

`ODBegin` is written at exactly two places, both inside
`TOBtlCtrlLuluLimitWindow`: it is **Lulu's overdrive input window**, not the
window state. And `updateFFX` calls `inputSetInputInfoToCurrentFrame` only while
`ODBegin != 0`, so the whole `Virtuos::InputManager` path - `GetControllerInfo`
included - runs only during that overdrive. The hook was installed correctly and
never fired once.

The `axis_neutral` setting that attempt asked for is gone with it. Those axes are
floats, and `0.0f` is already centre; the byte layer is where zero means
deflection.

## Settings

All three corrections are on by default and switch in Fahrenheit's own settings
panel - the mod no longer carries a config file of its own.

| Setting | Default |
| --- | --- |
| Skip Startup Splash Screens | on |
| Keep Launcher Closed After Exit | on |
| Ignore Controls While Window Is Unfocused | on |
| Log Pad State While Focused | off, diagnostic |

Labels and descriptions live in `lang/<module type name>/<locale>.json`; en-US
and de-DE ship.

## Build

```powershell
pwsh tools/bootstrap.ps1
pwsh build.ps1 -Deploy
```

## Status

Builds. NoSplash and NoLauncher are ports of implementations that were verified
working on this game build (`fahrenheit-nosplash-mod`, `fahrenheit-nolauncher-mod`,
and the bundled copy in `fahrenheit-parry-mod`). FocusInput is new and has not
been run yet.

Sibling: `fahrenheit-fps60-mod`. Kept separate on purpose, since a global retimer
and three conveniences have nothing to do with each other and should not share a
failure mode.
