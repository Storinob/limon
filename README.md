# Screenshot

A lightweight C# (.NET 8.0) utility as a replacement for ShareX and other programs. It runs in the background without an icon in the taskbar.

## Features

* Utility independently requests administrator rights at startup to avoid conflicts with programs that have a higher priority in the system, such as Task Manager, etc.
* The utility saves screenshots in the `C:\Users\user\Pictures\screenshots\{month_year}` folder with a random 8-character alphanumeric name.
* After taking a screenshot, the program plays the sound `done.wav`.
* Global Hotkeys:
    * `PrintScreen` — Screenshot of the screen area.
        * `Ctrl + LMB` — Drawing red lines.
        * `Alt + LMB` — Covering the area with a solid maroon rectangle.
        * `RMB` or `Esc` — Undo/Reset Selection.
    * `Shift + PrintScreen` — Instant screenshot of the entire screen.
    * `Ctrl + PrintScreen` — Dropper (Color Picker). Left-click copies the HEX code of the colour to the clipboard.

## Build

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true
```
Для компиляции проекта вам понадобится установленный **.NET 8.0 SDK**.
