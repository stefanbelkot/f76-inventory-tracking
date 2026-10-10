# Inventory Tracking Viewer

A local viewer for the exports of the InventoryTracking game mod. It reads the `itemdump_*.json` files that the mod writes into the game's `modsdata` folder and shows them in your default web browser: characters and storages, a sortable and filterable item table, text search over names and legendary effects, item details with effects by star, search links (Google and Fandom wiki), and CSV/JSON export of the current view.

The specification (decisions, architecture, input format) is in [`../spec/project-guide.md`](../spec/project-guide.md), sections 6.2 and 9.

- The tool only reads the game files. The only thing it writes is its settings file `%LOCALAPPDATA%\InventoryTrackingViewer\settings.json` (export folders, theme, column layout). No inventory data is ever stored.
- It listens on `127.0.0.1` only (port 8080, or the next free port), lives in the system tray (no console window) and opens the browser. Starting it again opens the browser on the running instance. *Quit* is in the tray menu and in the page header.

## Several export folders

The viewer reads any number of folders together: the Steam and the Xbox / Game Pass `modsdata` folders, and any folder you copied exports into (for example from another computer). Add them on the setup page or under *Settings > Export folders*: each detected install has its own button, *Add all detected* adds every install that was found, and a path can be typed or browsed for. A folder can be the game install folder, its `modsdata` folder, or any folder that holds `itemdump_*.json` files. Folders can be removed again from the same list; a folder that is gone (a disconnected drive) is shown as not found and does not stop the others.

Files are merged by account, character and storage as before. An identical file found in several folders is shown once; the same file name with different content (two snapshots) is kept, and the storage then shows the "mixed snapshots" status. Hover a storage card to see which folder it was read from. Settings saved by the first version (one `folder`) are migrated into the list automatically.

## Modes: local, web, both

The same code runs in three modes (`--mode` of the exe, fixed to web in the IIS build):

| Mode | Data comes from | Pages | Built by |
|---|---|---|---|
| **local** (default) | export folders on this computer | Inventory, Settings | `InventoryTracking.Server` (exe) |
| **web** | files uploaded into sessions on the server | Inventory, Import | `InventoryTracking.WebHost` (IIS) |
| **both** (debugging) | local folders and uploads together | Inventory, Settings, Import | the exe with `--mode both` |

**Sessions (web and both).** Opening the site creates a session and adds `?session=<32 hex digits>` to the address (a random 128-bit GUID). Whoever has the link can browse, upload and delete, so it can be shared between players. The browser remembers its last session (local storage), so opening the plain address returns to it; *Import > Start a new session* creates another one. Every API call names its session in the `X-InventoryTracking-Session` header.

**Import page.** Multi-file upload by file chooser or drag and drop (files are checked one by one; a file with the name of an uploaded one replaces it), the list of uploaded files with single and multi-select delete, *Delete all files*, *Delete this session*, and the link to share. Only `itemdump_*.json` files that the decoder accepts are stored.

**Data handling.** Uploaded files live in `<application folder>/session-data/<id>/` (the exe uses `<settings folder>/sessions`; `--sessions-dir` or the `Viewer:SessionsDirectory` setting change it) and are also held parsed in memory. The memory copy of a session is dropped after **1 hour** without use and is read from the folder again on demand; every upload or delete rebuilds it completely. A session folder is deleted **1 week after its last use** (checked at start and every 5 minutes). Defaults: 256 KB per file, 100 files and 5 MB per session, 5000 sessions (`Viewer:*` settings).

**In web mode** there is no folder setup, folder browsing, saved settings file or quit button; theme and column choices are kept in each browser's local storage.

## Web deployment on Windows IIS

The web-only build is `src/InventoryTracking.WebHost` (in-process ASP.NET Core Module).

1. On the server install the **.NET 8 Hosting Bundle** (runtime and IIS module), then restart IIS.
2. Publish and copy the output to the site folder:
   ```powershell
   dotnet publish src/InventoryTracking.WebHost -c Release -o publish-web
   ```
3. Create an IIS site (application pool: *No managed code*) with the folder as its physical path and your host name and HTTPS certificate in the binding. IIS terminates TLS; the app itself speaks HTTP behind it.
4. Give the application pool identity (`IIS AppPool\<pool name>`) **modify** permission on the session folder (by default the `session-data` folder inside the site folder; it is created on first use). A folder outside the site, set with `Viewer:SessionsDirectory` in `appsettings.json` (for example `D:\InventoryTrackingData\sessions`), is better; relative paths are resolved against the site folder.
5. Settings are in `appsettings.json` under `Viewer` (limits, `MemoryIdleMinutes`, `SessionLifetimeDays`, `AllowedHosts` = the host names the site answers to; empty accepts any). The upload size limit of IIS is set in `web.config` (`maxAllowedContentLength`, 5.5 MB) and must stay at least as large as 20 files of `MaxFileBytes`; the folder name `session-data` is a hidden segment there, so IIS never serves it directly. That name must not match a segment of an API address (`/api/v1/sessions`): IIS answers such requests with 404 before the app sees them.

Security notes: there are no accounts, the long random session link is the only secret. Every API call needs the custom `X-InventoryTracking` header and no CORS headers are sent, so other sites cannot call the API from a visitor's browser. Uploaded files contain account and character names; the Import page says so and offers *Delete this session*. Rate limiting and DDoS protection are left to IIS and the network in front of it.

## Layout

```
InventoryTracking.Viewer.sln
src/InventoryTracking.Core      portable library: decoding, grouping, queries, links, export, folder setup logic
src/InventoryTracking.Api       shared library: JSON API, sessions and uploads, request guard, embedded Vue app, settings
src/InventoryTracking.Server    net8.0-windows exe: single instance, tray, browser launch, local folders (modes local, web, both)
src/InventoryTracking.WebHost   net8.0 web-only host for Windows IIS (mode web)
web/                            Vue 3 + Vite + TypeScript app (embedded into the Api library)
tests/InventoryTracking.Core.Tests     xUnit, includes parity tests against tools/decode_export.py output
tests/InventoryTracking.Server.Tests   xUnit, runs a real server on loopback ports
tests/fixtures/real             the five real exports of 2026-10-04 (read-only copies)
tests/fixtures/oracle           output of tools/decode_export.py for those files (the parity oracle)
```

The Vue app is built by the Api project: an MSBuild target runs `npm ci` (when `web/node_modules` is missing) and `npm run build` whenever `web/dist/index.html` is missing or older than a web source file, and embeds `web/dist` into the assembly; both hosts therefore carry the app. Requirements: .NET SDK 8 and Node 20.19+ or 22.12+ with npm.

## Build, run, test, publish

All commands run from this folder.

```powershell
dotnet build                                   # builds the web app first when needed
dotnet run --project src/InventoryTracking.Server
dotnet test                                    # Core and Server tests
cd web; npm test; npm run type-check           # frontend unit tests and strict type check

dotnet publish src/InventoryTracking.Server -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

The publish step produces one self-contained `InventoryTrackingViewer.exe` (about 74 MB) that runs from any folder.

#### Smaller, framework-dependent publish

```powershell
dotnet publish src/InventoryTracking.Server -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -o publish-small
```

This produces a single `InventoryTrackingViewer.exe` of about 0.5 MB. It needs the .NET 8 Desktop Runtime and the ASP.NET Core 8 Runtime installed on the machine. Compression must be switched off, because the SDK only allows it for self-contained apps.

Pass `-p:SkipWebBuild=true` to skip the npm build (then the executable has no UI; use it only for backend work, with `-p:TreatWarningsAsErrors=false`).

### Command line flags

| Flag | Effect |
|---|---|
| `--no-browser` | do not open the browser (also when a second start asks the running instance) |
| `--no-tray` | no tray icon and no message boxes; run until quit is requested, errors go to standard error |
| `--folder <path>` | use this modsdata (or game) folder for this run; repeat the flag for several folders; they are not saved |
| `--settings-dir <path>` | use another folder for `settings.json` |
| `--port <n>` | first port to try (default 8080; up to 100 ports are tried) |
| `--instance-id <name>` | separate single-instance lock, for automated runs |
| `--mode local\|web\|both` | where the data comes from (default local, see *Modes*) |
| `--sessions-dir <path>` | where web sessions are kept (default: a `sessions` folder next to `settings.json`) |

### Frontend development

Start the server, then the Vite dev server with hot reload; it forwards `/api` to the server.

```powershell
dotnet run --project src/InventoryTracking.Server -- --no-browser --port 8080
cd web; npm run dev                            # http://localhost:5173
```

Set `VIEWER_API` to proxy to another address.

## API (`/api/v1`)

Every call needs the header `X-InventoryTracking: 1` and a local `Host` header with the server's port; no CORS headers are sent. Downloads therefore use `fetch` and a browser-side save.

| Call | Purpose |
|---|---|
| `GET capabilities` | mode (local, web or both), features, import limits, item type table |
| `GET status` | data version, the configured export folders (path, exists, export file count), load issues, whether setup is needed |
| `POST refresh` | reload the files now |
| `POST sessions` | create a session (web and both; no session header needed) |
| `GET session` | the current session: id and its files |
| `POST session/files` | upload files (multipart/form-data, field `files`, several allowed); one result per file |
| `POST session/files/delete` | delete named files (`{ names: [...] }`) |
| `POST session/delete` | delete the whole session |
| `GET setup/candidates`, `GET setup/dirs?path=`, `POST setup/folder` | find installs and add export folders (`confirm:false` previews, `confirm:true` adds the folder to the saved list) |
| `POST setup/folder/remove` | remove a folder from the list (`{ "path": ... }`); with none left the setup page shows again |
| `GET groups` | characters and storages with status and capture time |
| `GET items?group=&type=&q=&legendary=&stars=&favorite=&equipped=&quest=&sort=&page=&pageSize=` | filtered, sorted, paged items and totals |
| `GET items/{key}` | all fields and effects of one item |
| `GET search?q=` | where an item occurs, with counts per storage |
| `GET links?item=&q=` | search links for an item, or for edited terms |
| `GET export?format=csv\|json&...` | the filtered view (same filters as `items`, no paging) |
| `GET/PUT preferences` | theme and column layout |
| `POST shutdown` | stop the tool (local mode) |
