# fahrenheit-qol-mod

Three small conveniences for Final Fantasy X HD Remaster, in one Fahrenheit mod.

- **NoSplash** takes the boot sequence straight to the title screen: the splash
  events are redirected, the Japan-logo gate is answered no, and the boot videos
  are skipped without input.
- **NoLauncher** stops the engine reopening `FFX&X-2_LAUNCHER.exe` when the game
  window is closed.
- **FocusInput** keeps the analog sticks neutral while the window does not own
  input.

## The focus bug

`AsyncControllerTaskManager::GetControllerInfo` writes zeroes to both the button
word and the four axis words while the engine's window-active flag is clear:

```c
if (ODBegin == 0) {
  *param_1 = 0;   // buttons
  *param_2 = 0;   // axes
  param_2[1] = 0;
  param_2[2] = 0;
  param_2[3] = 0;
  return;
}
```

Zero is right for buttons and wrong for axes: their rest position is the middle
of the range, so zero reads as full deflection. That is why the character walks
and the menu cursor climbs while the game sits in the background.

The neutral word is not hardcoded, because the axis packing has not been read
off the running game yet. Out of the box the module runs in survey mode and only
logs the axis words it sees while focused. Put the value it reports into
`fhqol.config.json` to switch the correction on:

```json
{ "axis_neutral": 32768 }
```

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
