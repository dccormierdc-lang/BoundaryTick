# BoundaryTick

BoundaryTick is a small Windows tray utility that adds a short "tick" when the
mouse crosses between adjacent monitors.

## Usage

1. Download `BoundaryTick.exe`.
2. Double-click `BoundaryTick.exe`.
3. Right-click the `BoundaryTick` tray icon to enable/disable it or change the resistance strength.

Direct download:

https://github.com/dccormierdc-lang/BoundaryTick/raw/refs/heads/main/BoundaryTick.exe

## Build From Source

If you want to build it yourself, run `build.cmd`. This creates `BoundaryTick.exe`.
You can also run `run.cmd`, which builds the app if needed and starts it.

## How It Works

- Detects only internal edges where two monitors touch.
- Holds the cursor on the current monitor edge for about `140ms` by default.
- If you keep pushing, the cursor crosses to the next monitor.
- Settings are saved in `BoundaryTick.ini`.

## Exit

Right-click the tray icon and choose the exit menu item.
If you cannot find the tray icon, run `stop.cmd`.

## Notes

This uses a global low-level mouse hook on Windows. Unsigned builds may trigger
SmartScreen or antivirus warnings.
