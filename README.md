# FFX Quality of Life

[![CI](https://github.com/Exoridus/ffx-qol-mod/actions/workflows/ci.yml/badge.svg)](https://github.com/Exoridus/ffx-qol-mod/actions/workflows/ci.yml)

Three independently switchable conveniences for Final Fantasy X HD Remaster, loaded by Fahrenheit.

## Status

**Work in progress / experimental preview.** A successful build verifies compilation and packaging, not complete in-game compatibility. Final Fantasy X only; Final Fantasy X-2 is unsupported. Keep a backup of your saves and report the game build, scene and enabled settings when filing an issue.

## Features

- **NoSplash:** skips the startup splash sequence and boot videos.
- **NoLauncher:** prevents the launcher from reopening when the game closes.
- **FocusInput:** neutralizes controller input while the game window is unfocused.

Settings and diagnostic pad logging are available in Fahrenheit's settings panel. English and German labels are included.

## Requirements

- Windows and a legitimate installation of Final Fantasy X HD Remaster.
- [Fahrenheit](https://github.com/Fahrenheit-Modding/Fahrenheit), compatible with the revision pinned in `fahrenheit.release.ref`.
- The mod is a Windows x86 managed plugin, not a standalone executable. Fahrenheit provides its runtime and loader dependencies.

## Installation

1. Install Fahrenheit following its upstream instructions.
2. Download the ZIP and its SHA-256 checksum from [Releases](https://github.com/Exoridus/ffx-qol-mod/releases).
3. Extract the ZIP into the game's `fahrenheit/` directory. The resulting path must be `fahrenheit/mods/fhqol/fhqol.dll`.
4. Add `fhqol` on its own line to `fahrenheit/mods/loadorder`.
5. Launch the game and adjust the mod's settings in Fahrenheit.

To uninstall, close the game, remove that loadorder entry and remove `fahrenheit/mods/fhqol/`. Do not overwrite the game's executable or data archives.

Verify the ZIP with `Get-FileHash <downloaded.zip> -Algorithm SHA256` and compare the value with the `.sha256` file.

## Build from source

Requires Git, PowerShell 7 and the .NET 10 SDK selected by `global.json`.

```powershell
git clone https://github.com/Exoridus/ffx-qol-mod.git
cd ffx-qol-mod
pwsh tools/bootstrap.ps1
pwsh build.ps1 -Configuration Release -OutDir .release/stage
```

Bootstrap checks out the pinned public Fahrenheit source. No game installation or private sibling repository is required to compile. `-Deploy` is an optional local convenience with a machine-specific destination in `build.ps1`; use staged output for other installations.

## Releases and CI

CI builds on Windows and exercises the same package and release-note scripts used by releases. The ZIP contains only `mods/fhqol/`: the mod DLL, manifest, dependency metadata and localization files. Fahrenheit itself and game assets are not bundled.

Maintainers set the manifest version, merge to `main`, then dispatch:

```powershell
gh workflow run release.yml -R Exoridus/ffx-qol-mod -f version=v<VERSION> -f dry_run=true
```

Omit `dry_run` to publish an experimental prerelease with a ZIP and SHA-256 file. The version must match the manifest; the workflow creates its tag only after building and packaging succeed.

## Related projects

- [Fahrenheit Parry Mod](https://github.com/Exoridus/fahrenheit-parry-mod)
- [FFX Quality of Life](https://github.com/Exoridus/ffx-qol-mod)
- [FFX FPS Unlock](https://github.com/Exoridus/ffx-fpsunlock-mod)
