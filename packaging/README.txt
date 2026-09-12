FFX Quality of Life (fhqol) with Fahrenheit
============================================

Small fixes for Final Fantasy X HD Remaster (PC): skips the boot splash
sequence, stops the launcher reopening when you close the game, keeps the
analog sticks neutral while the window is not focused, and offers a
hold-to-fast-forward for cutscenes. Final Fantasy X only; FFX-2 is not
supported.


Installation
------------

1. Copy the contents of this archive into the game directory, next to
   FFX.exe. On Steam that is typically:
   C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY FFX&FFX-2 HD Remaster\

   Afterwards the layout must be:
     <game>\FFX.exe
     <game>\start-ffx-with-mods.cmd
     <game>\fahrenheit\bin\fhstage0.exe
     <game>\fahrenheit\mods\fhqol\fhqol.dll
     <game>\fahrenheit\mods\loadorder

2. Microsoft .NET Runtime 10 (x86) must be installed. The launcher checks
   for it and offers to install it through winget. Manual download:
   https://dotnet.microsoft.com/en-us/download/dotnet/10.0
   -> ".NET Runtime 10", Windows x86.

3. Always start the game through start-ffx-with-mods.cmd, not through Steam
   and not through FFX.exe directly. Steam has to be running.

On first start Fahrenheit creates the config, logs, saves and state folders
under fahrenheit\ on its own.


Settings
--------

Fahrenheit's settings window (ImGui) opens in game. Every feature has its
own switch under "Quality of Life".


Uninstall
---------

Close the game, then delete the fahrenheit\ folder and
start-ffx-with-mods.cmd. FFX.exe and the game data are never modified.


Sources
-------

fhqol        https://github.com/Exoridus/ffx-qol-mod
Fahrenheit   https://github.com/fahrenheit-crew/fahrenheit
