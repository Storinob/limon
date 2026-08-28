<img width="250" height="250" alt="logo-gh" src="https://github.com/user-attachments/assets/26eee073-d543-43f7-8e32-4c8b78c00df9" />

# limon

A lightweight C# (.NET 8.0) utility as a replacement for ShareX and other screenshot programs (there is also an eyedropper function for copying colour from the screen). This program works in the background without an icon on the taskbar.

First of all, I built this utility for myself, but I also prepared it for public use. Ready to download and run the `.exe` file is in the repository.

![Platform](https://img.shields.io/badge/platform-Windows_10-0078D6?logo=windows&logoColor=white)
[![Latest Release](https://img.shields.io/github/v/release/Storinob/limon?color=007acc&label=release)](https://github.com/Storinob/limon/releases/)


## Features

* Utility independently requests administrator rights at startup to avoid conflicts with programs that have a higher priority in the system, such as Task Manager, etc.
* The utility saves screenshots in the `C:\Users\{user}\Pictures\screenshots\{month_year}` folder with a random 8-character alphanumeric name.
* After taking a screenshot, the program plays the `done.wav` sound.
* Global Hotkeys:
    * `PrintScreen` — Screenshot of the screen area.
        * `Ctrl + LMB` — Drawing red lines.
        * `Shift + LMB` — Drawing red rectangle.
        * `Alt + LMB` — Covering the area with a solid maroon rectangle.
        * `Z` or `BackSpace` — Undo drawn.
        * `RMB` or `Esc` — Reset drawn/selection.
    * `Shift + PrintScreen` — Instant screenshot of the entire screen.
    * `Ctrl + PrintScreen` — Dropper (Color Picker). Left-click copies the HEX code of the colour to the clipboard.

## Build

[![License](https://img.shields.io/github/license/Storinob/limon?color=green)](https://github.com/Storinob/limon/blob/main/LICENSE)
![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
[![Last Commit](https://img.shields.io/github/last-commit/Storinob/limon?color=brightgreen&label=updated)](https://github.com/Storinob/limon/commits/main)
![Repo Size](https://img.shields.io/github/repo-size/Storinob/limon?color=orange)

Command must be executed inside the project folder.

If you just want to compile a program (~240KB):
```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true
```
If you want to build a program completely independent (packs some libraries into the program itself, increasing its weight to ~160MB. `BASICALLY NOT NEEDED`):
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```
To compile the project, you will need the **.NET 8.0 SDK**.
