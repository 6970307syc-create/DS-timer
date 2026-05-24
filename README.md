# DS Timer

A small Windows hotkey timer app.

- Press `Alt + F` to start a 60-second countdown.
- Up to four countdowns can run at the same time.
- New timers always use the first free slot, so completed earlier slots are reused before later slots.
- The original `down.wav` sound plays when a 60-second timer starts.
- The original `megabeam.wav` sound plays when a 60-second timer ends.
- Each trigger also starts a hidden 15-second voice countdown: `three`, `two`, `one`, `go`.

## Build

Run `build.ps1` from Windows PowerShell. The packaged executable is written to `release/DS_Timer.exe`.
