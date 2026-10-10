# Inventory Tracking Syncer

A small Windows tray program that watches the Fallout 76 export folders (Steam and/or Xbox, auto-detected) and uploads the
`itemdump_*.json` files into a session of the hosted viewer (https://f76inventorytracker.bestesoftware.eu/). Spec: `spec/project-guide.md`, section 9a.

## Use
1. Start `InventoryTrackingSyncer.exe`. On the first start it asks whether to start with Windows, then opens the settings window.
2. Paste the **session link** from the viewer page (or click *Create new session*), check the detected folders, *Save and start*.
3. It uploads everything at start-up and again whenever an export file changes. Tray menu: Sync now, Open viewer in browser, Update session,
   Update Xbox / Steam folders, Settings, Start with Windows, Open log folder, Quit.

Settings and log: `%LOCALAPPDATA%\InventoryTrackingSyncer\` (`settings.json`, `syncer.log`). Command line: `--background`, `--server <url>`, `--settings-dir <path>`.

## Build, test, publish
```powershell
cd D:\FalloutModding\Repos\f76-inventory-tracking\inventorytracking-syncer
dotnet test tests/InventoryTracking.Syncer.Tests -c Release
dotnet publish src/InventoryTracking.Syncer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish   # one exe, about 63 MB
```
`LiveViewerTests` runs against a real viewer when `INVENTORY_VIEWER_URL` is set (viewer started with `--mode web --no-browser --no-tray --port 5099`);
`INVENTORY_FIXTURES` may point at a folder with export files.