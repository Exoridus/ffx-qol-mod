<div align="center">

# FFX Quality of Life

### Small conveniences. Independent controls.

[![Latest](https://img.shields.io/github/v/release/Exoridus/ffx-qol-mod?include_prereleases&style=for-the-badge&logo=github&label=Latest)](https://github.com/Exoridus/ffx-qol-mod/releases)
[![CI](https://img.shields.io/github/actions/workflow/status/Exoridus/ffx-qol-mod/ci.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=fff&label=CI)](https://github.com/Exoridus/ffx-qol-mod/actions/workflows/ci.yml)

A [Fahrenheit](https://github.com/fahrenheit-crew/fahrenheit) mod for the Windows Steam version of Final Fantasy X HD Remaster.

**[Downloads](https://github.com/Exoridus/ffx-qol-mod/releases)** · **[Install](#installation)** · **[Settings](#settings)** · **[Report a bug](https://github.com/Exoridus/ffx-qol-mod/issues)**

</div>

> [!IMPORTANT]
> Experimental. Built with the official Fahrenheit alpha12 SDK and tested on Steam build **25501027**.
> FFX-2 is not supported.

## What it does

Each feature has its own category in the F7 menu and can be turned off on its own.

| Feature | Behavior |
| --- | --- |
| NoSplash | Skips the startup splash screens and boot videos. |
| NoLauncher | Stops the launcher from reopening when the game closes. |
| FocusInput | Ignores controller input while the game window is unfocused, including the HD/ESC menu and the false "up" a failed DirectInput read produces after Alt-Tab. |
| FastForward | Hold a button (default R2) to speed up the game; speed and muting are configurable. |
| NativeReset | Hold L1 + R1 and press Start/Options to request a game over. |
| Farplane | Restores the Farplane background movie and its pyreflies and fixes the movie teardown, based on Farplane Fix 1.2 by Fuxake. |

## Installation

Requires Fahrenheit with its prerequisites: **.NET Runtime 10** and the **Visual C++ Redistributable**,
both in the **x86** version (see the [Fahrenheit installation guide](https://github.com/fahrenheit-crew/fahrenheit/wiki/Installation-guide)).

| Download | Extract to | Use |
| --- | --- | --- |
| `fhqol-mod-<tag>.zip` | `GAME_DIR/fahrenheit/mods/` | Add to an existing Fahrenheit installation. Add `fhqol` to `fahrenheit/mods/loadorder`. |
| `fhqol-full-<tag>.zip` | `GAME_DIR/`, next to `FFX.exe` | Mod plus matching Fahrenheit. Start with `start-ffx-with-mods.cmd`. |

Keep the `fhqol/` folder complete. To update, replace the folder; to uninstall, delete it and its
loadorder entry. Each download has a `.sha256` file to compare with `Get-FileHash`.

## Settings

Open Fahrenheit's settings with **F7**. English and German labels are included.

## Known limitations

- FastForward cannot speed up the three syncdata-paced scenes at Seymour, Home and Yunalesca.
- Farplane: long-duration looping, reset and the second teardown path are untested.

## Build from source

Requires Git and the .NET 10 SDK pinned in [global.json](global.json).

```powershell
.\build.cmd mod-build  --sdk local --configuration Debug
.\build.cmd mod-verify --sdk local --configuration Release
.\build.cmd mod-pack   --sdk local --configuration Release --tag v0.0.0-dev --out-dir .release
```

Add `--full true` to `mod-pack` for the full package (needs the Visual Studio C++ build tools).
Pushing a `vX.Y.Z` tag that matches the project version on `main` builds with the official SDK and
publishes the release; a normal push never creates one.

## Related projects

[Fahrenheit](https://github.com/fahrenheit-crew/fahrenheit) · [FFX Parry](https://github.com/Exoridus/ffx-parry-mod) · [FFX VRR](https://github.com/Exoridus/ffx-vrr-mod)

Final Fantasy X belongs to Square Enix. This is an independent community project. No game files or
replacement movies are distributed.
