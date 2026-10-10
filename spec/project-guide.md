# InventoryTracking - project guide for agents

This is the single document about the project. Read it first and completely; nothing else in the repository is needed to take over. It covers the product, repository layout, environment, working rules, the game mod (behavior, file format, runtime facts, build, deploy, modification rules, bugs fixed), the compatibility work with InventOmaticStash-UO, the decoder, the viewer (decisions, architecture, security, web/IIS), the research record that led to the design, and the backlog. Last updated 2026-10-04.

## 1. What the project is

Four parts, all in this repository (`D:\FalloutModding\Repos\f76-inventory-tracking`):

| Part | What it is | Folder |
|---|---|---|
| **Game mod** | A Fallout 76 interface mod. In a full storage menu (stash, scrap box, ammo box, aid box) it adds buttons that export the complete player inventory or the opened box to one compressed JSON file via the xScal bridge. Since v1.4 its code lives in its own child SWF (`interface/InventoryTracking.swf`) loaded by a tiny hook in `securetrade.swf`. | `exporter/` |
| **Decoder** | A small Python tool that decodes the export files (reference implementation and test oracle for the viewer). | `tools/decode_export.py` |
| **Viewer** | A .NET 8 + Vue 3 web app that reads the export files and lets you browse, filter and search them. Runs locally (exe) and/or as an upload-based site on Windows IIS. | `inventorytracking-ui/` |
| **Syncer** | A Windows tray exe that watches the game export folders and uploads the files into a session of the hosted viewer automatically (section 9a). | `inventorytracking-syncer/` |

The owner plays Fallout 76 on Microsoft Store/Xbox PC and Steam (two accounts). The runtime is xScal 0.2.20 (a native bridge). ZFE was considered but is untested and disabled itself on the Game Pass runtime.

## 2. Status (2026-10-04)

- **Game mod v1.4 (merged into `main`, merge commit `1c2d34b`; the branch `inventomaticcompaitblity` can be deleted):** child-SWF architecture, three build modes (section 6). The `inventomatic` build (their SecureTrade + our hook + our child) is deployed on the **Xbox** install and verified in game by the owner: InventOmaticStash-UO's features and our export buttons show together, exports save correctly. **Not yet seen in game:** the `standalone` and `child-only` builds (they compile and pack; the child code is the same). **Steam install still has the old single-file v1.3 build.** Previously deployed versions: v1.3 added `vending` per row (CAMP machine assignment and vendor price), v1.2 currencies and legendary-module cards, v1.1 the modal-menu fix.
- **Decoder:** done.
- **Viewer:** done. Reads several export folders together (Steam + Xbox + user-added); three modes: local (exe, default), web (IIS: uploads in sessions with a shareable `?session=<guid>` link, files kept 1 week, memory copy 1 hour) and both (debugging). Features: stars filter and clean names for legendary modules, currency chips, vendor filter/price/FOR SALE badge, CSV/JSON export, character filter, phone layout, the Mod calculator tab (9.3) and the Guide tab with downloads (9b). Tests at the last run: 151 Core, 145 Server, 47 web. The IIS build runs on the owner's IIS server (deployed from `D:\FalloutModding\Deploy\InventoryTrackingWeb`). The Import page in a browser was only verified by code reading; the phone layout and the Mod calculator were checked by type-check, unit and API tests and a production build, not yet by looking at them on a phone or in the deployed site.
- **Syncer:** done and tried by the owner on his desktop (section 9a): auto-detected Steam/Xbox folders, session link, mirrored deletions, tray menu, start with Windows. Open: reboot/autostart check and the end-to-end update after an in-game export.
- **Upstream:** the InventOmaticStash-UO author has not been asked yet to add the hook (section 7.5).
- **Git state:** the exporter, hook, viewer projects and baseline are committed. `exporter/build/` (generated output) is currently tracked too, which makes every build show as a modification; the owner may add a `.gitignore` for it. Uncommitted: this consolidated guide, the removal of the other `spec/` files, link fixes in `inventorytracking-ui/README.md` and the handover note, and the new `tools/ba2x.py`.

## 3. Repository layout

```
spec/
  project-guide.md               this file (the only specification)
  README.md                      pointer to this file
exporter/
  child/InventoryTracking.as     the whole exporter (prefix "it"), compiled to interface/InventoryTracking.swf
  hook/hook-methods.as           loader method added to SecureTrade (loads the child; a missing file is ignored)
  hook/hook-bridge-field.as      `public var __SFCodeObj` field (standalone build only)
  hook/hook-methods-debug.as     debug variant of the hook (on-screen step box)
  build_exporter.py              builds the archives; modes standalone | inventomatic | child-only; never installs
  host-methods.as                OBSOLETE (v1.3 single-file injected block); kept only as history, no longer built
  build/                         generated output (untracked)
inventomaticstash-mod/           pristine copy of InventOmaticStash-UO 2.85 (archive files + decompiled source), the baseline for diffs
tools/decode_export.py           Python decoder
tools/ba2x.py                    small GNRL BA2 extractor: ba2x.py <archive> [outdir]  (lists when no outdir)
tools/package_release.py         builds the download zips and downloads.json for the Guide page (section 9b)
exporter/vanilla/                the vanilla build INPUTS (moved here from the removed probe/ folder)
  securetrade.swf                vanilla securetrade.swf of runtime 1.7.26.22 (input of the standalone build)
  source/                        its FFDec script export
inventorytracking-ui/            the viewer
inventorytracking-syncer/        the Windows tray uploader (section 9a)
  src/InventoryTracking.Core     portable library: decoding, grouping, queries, links, export, folder setup
  src/InventoryTracking.Api      shared library: JSON API, sessions/uploads, request guard, embedded Vue app, settings (namespaces still InventoryTracking.Server.*)
  src/InventoryTracking.Server   net8.0-windows exe: single instance, tray, browser, local folders; --mode local|web|both
  src/InventoryTracking.WebHost  net8.0 web-only host for Windows IIS (upload sessions, no local folders)
  web/                           Vue 3 + Vite + TypeScript app (embedded into the server)
  tests/                         xUnit (Core, Server) + fixtures: real exports and decoder-output oracle
  README.md                      build/run/test/publish/IIS steps for the viewer
handoverfiles/                   an earlier agent handover note (historical)
```

## 4. Environment

| Item | Location / version |
|---|---|
| Workspace | `D:\FalloutModding` (not itself a git repo; the repo is `Repos\f76-inventory-tracking`, branch `main`) |
| OS / shell | Windows 11; use PowerShell (Windows PowerShell 5.1: no `&&`, no `Join-String`) |
| Python | `C:\Python312\python.exe` |
| FFDec (Flash decompiler/compiler) | `C:\Program Files (x86)\FFDec\ffdec.jar`, needs Java (openjdk 11) |
| BA2 packer | `D:\FalloutModding\pack_ba2.py` (OUTSIDE the repo; `build_exporter.py` imports it) |
| .NET SDK / Node | 8.0.202 / 22.12 with npm 10.9 |
| Xbox/Game Pass game | `D:\XboxGames\Fallout 76\Content` (mods in `Data`, logs `xscal.log`, exports in `Data\modsdata`); process `Project76_GamePass.exe` |
| Steam game | `C:\Program Files (x86)\Steam\steamapps\common\Fallout76` |
| Game INIs | `<your Documents folder>\My Games\Fallout 76\Project76Custom.ini` (Xbox, key `sResourceArchive2List`) and `Fallout76Custom.ini` (Steam). Our archive must be the LAST entry. |
| Mod manager staging | `Data\InventoryTracking.ba2` is a symlink to `D:\FalloutModding\ModsDownload\mods_staging\22\91785916594021136\InventoryTracking.ba2` (Xbox); Steam uses `...\23\...`. Update the target; never replace the symlink. Keep a `.prev-backup`/`.vNN-backup` next to it. |
| InventOmaticStash-UO | `Data\InventOmaticStash-UO.ba2` -> `D:\FalloutModding\ModsDownload\mods_staging\2335\23596\` |
| Web deployment output | `D:\FalloutModding\Deploy\InventoryTrackingWeb` |

## 5. Working rules (from the owner)

- **Ask before deploying or running a build that touches the game**, and never kill the game; ask the owner to close it when the archive is locked. (The owner has sometimes said "deploy, you do not need to ask first" for a specific round; that does not carry over.)
- Do not commit unless asked; the owner reviews changes in git. Do not add a `.gitignore` unless told.
- Preserve existing documentation comments in the ActionScript (discoveries about properties).
- Put new decisions in this file.
- Reports: state what was verified and how; do not claim runtime correctness from a compile. Only a test in the game proves the mod works.
- The session may run in auto mode: if a shell command is blocked by the safety check, do not retry or reword it; ask the owner.
- Tooling: use PowerShell. Windows PowerShell 5.1 `Set-Content -Encoding utf8` adds a BOM (breaks `.as`/`.py` files) and `Get-Content` reads UTF-8 as ANSI (it corrupted `¬`/`§` once). Edit with the Edit tool, Python, or `[IO.File]::WriteAllText(path, text, New-Object Text.UTF8Encoding($false))`.

## 6. Game mod

### 6.1 Behavior (as built)
Verified on Fallout 76 runtime 1.7.26.22, xScal 0.2.20; all five export kinds saved and decoded on Game Pass; the xScal author reports no difference on Steam.

**Where the buttons appear.** Drawn at the top left of the menu, re-checked every 700 ms. Visible only when `ContainerOptionsData` reports `storageMode` 6 (personal stash), 7 (scrap box), 8 (ammo box) or 9 (aid box), not a workbench/workshop, and not corpse looting (`OtherInventoryData.CorpseInventories` non-empty).

| Button | Shown when | Source list |
|---|---|---|
| Export player inventory | stash menu only (mode 6) | `PlayerInventoryData.InventoryList` |
| Export stash / scrap box / ammo box / aid box | any supported mode | `OtherInventoryData.InventoryList` |
| Part N | any supported mode | cycles 1 to 8 |
| `IT vX.Y` | any supported mode | not clickable; proves which build runs. `IT_VERSION` in the child; bump it for every deployed build |

The player list is restricted in the scrap/ammo/aid menus (measured 4, 3 and 7 rows), so the player export is offered only where it is complete. xScal binds **one storage name per menu instance**: after one successful save the buttons say "close and reopen the menu"; a different part or export needs a new menu open.

**Export flow.** (1) Freeze the list, sorted by `serverHandleID`. (2) For every **legendary** row, and for legendary modules (mod boxes), read the item card: select the row by dispatching the menu's own `SecureTrade::OnItemSelected` event (`serverHandleID`, `isSelectionValid:true`, `fromContainer`) and wait for the matching `InventoryItemCardData`; a card the game already holds for that item is used directly; one retry after 1.5 s; then the row is marked missing. Identical modules share one card read. (3) Return the selection to the item that was selected before. (4) Serialize, compress (`ByteArray.compress`, zlib), base64, split only if needed, register the name, save, read back and compare; success is reported only if the save returned true and the read-back matches. Nothing is transferred, scrapped, renamed or modified; non-legendary rows never get a card request. The message on success: `Exported <Player inventory|Stash|Scrap box|Ammo box|Aid box> Part n/N - please reopen the box for other exports`.

### 6.2 Output files
Name: `itemdump_<account>_<character>_<storage>_<part>.json`, e.g. `itemdump_testacc01_tester_stash_1.json`. `<storage>` is `player`, `stash`, `scrap`, `ammo`, `aid` (`modeN` for an unexpected mode). Account and character come from `AccountInfoData.name` and `CharacterInfoData.name`. **Only lowercase letters, digits and underscores** (an uppercase letter was refused); other characters are removed and a 4-hex-digit hash of the original is appended; a token is at most 14 characters. Re-exporting the same account/character/storage replaces only that file; no history.

Content: a valid UTF-8 JSON document of at most 40,960 bytes (storage limit). Header in plain text, rows as one compressed field:

```
{
"v":1, "tool":"InventoryTracking", "account":"test.account", "character":"Tester",
"storage":"stash", "storageMode":6, "title":"My Stash Box (STASH)",
"currencies":[{"type":0,"amount":11523,"max":40000}, ...],   // player export only (since v1.2)
"snapshot":"1259b7e1",          // hash of all item ids and counts
"rows":719, "part":1, "parts":1, "rowsInPart":719,
"cards":"legendary+mods", "cardsOk":64, "cardsMissing":0,
"encoding":"zlib+base64", "rawBytes":<bytes before compression>,
"data":"<base64 of zlib(JSON array of rows)>"
}
```

Row fields: `name` (displayed name incl. renames and legendary prefixes; not a base name), `count`, `id` (server handle), `value`, `equipState`, `filter` (bit mask), `level`, `health`, `maxHealth`, `weight`, `legendary`, `stars`, `favorite`, `quest`, `category`. Legendary rows add `card` (`ok`, `missing`, `timeout`, `no-desc`, `select-error`) and for `ok`: `legendaryMods` (effect count) and `desc` (description, one effect per line, each ending in one `¬` per star; `§` marks special effect lines). Legendary modules (mod boxes: `filter` bit 32768, name starting with `¬`) are not flagged legendary and have `stars` 0: the star count is the number of leading `¬` characters in `name`; since v1.2 they also get `card`/`desc` (`legendaryMods` omitted when the card has no such entry). The viewer turns the markers into `stars` and a clean name and shows `¬` in descriptions as `*`.

Rows of items assigned to a CAMP machine (since v1.3) add `vending`: `{"machine":1,"price":250,"other":false}`. `machine` = `vendingData.machineType` (1 player vending machine, 2 display case, 3 dispenser, 4 fermenter, 5 refrigerator, 6 ally, 7 recharger, 8 freezer, 9 pet; type 0 = no `vending` field), `price` = listed price per item (vending machine only, else null), `other` = assigned to another machine than the open one. The viewer treats machine 1 as "player vendor" (filter, FOR SALE badge, price column).

`currencies` (player exports only) is read from `CharacterInfoData.currencies` (same list the Improved HUD mod shows, found by decompiling `ImprovedHBS21.ba2 > ImprovedBars.swf`): `{type, amount, max}` with type 0 caps, 1 legendary tokens, 2 possum badges, 3 tadpole badges, 4 gold bullion, 5 perk coins, 6-9 legendary cores tier 1-4, 10 ultracite batteries, 11 expedition stamps, 12 supplies. Unknown types show as `Currency <n>`.

Parts: if the compressed data does not fit 40 KB, rows are split evenly into the fewest parts that fit (up to 60), each a complete file with its own `part`. In practice one part suffices (stash 719 rows/64 cards = 19,495 bytes; player 575 rows = 16,596 bytes; compression about 12x).

Item type bits (`filter`; also `Core/Types/itemtypes.json`): Weapon 4, Armor 8, Apparel 16, Food 32, Aid 64, Notes 1024|2048, Misc 4096|8192, Junk 16384|262144, Mods 32768, Ammo 65536, Holotapes 131072. Bits 1, 2 and 524288 occur but are unexplained; keep the raw value visible and show unknown bits as "other". `category` is always 0.

### 6.3 Runtime facts the design rests on (measured, xScal 0.2.20)
- `modStorage` has exactly `register`, `save`, `load`. One successful `register` per menu instance (a different name afterwards returns false; the same name works; a failed registration locks nothing). Names: **lowercase letters, digits, underscore**, at most 64 characters. `save` accepts **only valid JSON** (returns false otherwise) up to 40 KiB. A file written in one menu open can be read in a later open by registering the same name (a missing file loads as the string `false`).
- Static variables do not persist across menu opens; SharedObject fails (Error #1501). The saved file is the only persistence.
- Helper SWFs loaded by the menu get no independent native bridge in any application domain/reload variant (probe v5/v6, Game Pass), so one writer per helper is impossible. The child SWF uses the **menu's** `__SFCodeObj` (a public field xScal fills).
- `ByteArray.compress()` exists (standard zlib). `Number.toString(16)` prints values above 2^31 as signed; build hex from two halves.
- `bridge.chatInterface` is a chat-service client; never call it. `bridge.call("version")` threw a TypeError and an earlier probe calling `call` with guessed names crashed the game: never probe `call` with guessed names.
- Package-level functions called by fully qualified name (e.g. `flash.utils.getQualifiedClassName(...)`, `flash.utils.setTimeout(...)`) failed at run time with ReferenceError #1065; use imports. `JSONEncoder` and similar classes of other mods are not available; build JSON by hand and escape strings.
- Text fields: copy the text format from `PlayerInventory_mc.Header_mc.Header_tf` and set `embedFonts`; device fonts render as dark blocks.
- Item-card entries carry stats and description text but **no applied mod names** and no base FormID. Storage modes: 6 stash, 7 scrap, 8 ammo, 9 aid.
- Probe evidence: v6 wrote `itprobe_..._host.json` (1,591 bytes) on disk; xScal 0.2.20 gamepass reported same-name registration true, different name false. The probe mod (versions v1-v12) and its folder were deleted on 2026-10-04 (it is in git history before that date if ever needed); never enable a probe archive together with the exporter. Section 6.3a explains what was learned and why.

### 6.3a How a file is saved, and why we probed (the export method in one place)
**Method.** A mod's ActionScript cannot write files itself (no AIR `File`/`FileStream`, no `SharedObject`, no `FileReference.save`). The only way out is the native bridge xScal injects into the menu as the public field `__SFCodeObj`. Its `modStorage` object lets the menu write one JSON document to `Data\modsdata\<name>.json`:
1. `storage = __SFCodeObj.modStorage`.
2. `storage.register(name)`: claims the file name for this menu instance (must return `true`; lowercase letters, digits, underscore; at most 64 characters). Only ONE name can be claimed per menu instance (re-registering the same name is fine; a different one returns false).
3. `storage.save(payload)`: writes the file (replacing it); the payload must be valid JSON, at most 40,960 bytes UTF-8; returns `true` on success.
4. `storage.load()`: reads the registered file back (a missing file returns the string `"false"`); the mod compares it with the payload and reports success only if both agree.
The name is built from identity (`itemdump_<account>_<character>_<storage>_<part>`), so each export replaces only its own file. The file content is a JSON wrapper whose rows are zlib-compressed and base64-encoded (section 6.2) so that a normal inventory fits into one file.

**Consequences (what you can and cannot do):** one file per menu open; a second export or another part requires closing and reopening the menu (the buttons say so); files of earlier opens stay on disk, so several parts or exports accumulate over several opens (that is how "multiple files" works: one name per open, a different name per open). There is no deletion and no listing of files.

**Why we probed.** Nothing in the documentation said how many files a menu may write or whether helper SWFs could write too, and the target was one file per account/character/storage plus possible parts (and, originally, several files per export). Probe mods (a panel in `securetrade.swf` plus helper SWFs, v1-v12, built from the real game SWF) answered, on xScal 0.2.20 Game Pass: only `register/save/load` exist; one name per open; failed registrations lock nothing; helper SWFs (any Loader, application domain or reload variant) get no bridge of their own, so they cannot write; files persist across opens but program memory does not; the name must be lowercase; `save` accepts only valid JSON; `ByteArray.compress()` works (about 12x on the rows), which made one file per export enough. Details and numbers: 10.1. The probe mod and its folder were removed because everything it established is recorded here; the export no longer needs experiments, only the rules above.

### 6.4 Build
`python exporter\build_exporter.py [standalone|inventomatic|child-only] [--debug]` (no mode = all three). Output `exporter\build\<mode>\` with `InventoryTracking.ba2`, `hashes.json`, and for host modes `securetrade.swf` + `securetrade-hook.diff`. It never installs. The packer needs `D:\FalloutModding\pack_ba2.py`.

| mode | archive contains | base file (hash-locked, build refuses on mismatch) |
|---|---|---|
| standalone | patched vanilla securetrade.swf + InventoryTracking.swf | `exporter/vanilla/securetrade.swf`, SHA-256 `ff82f06175376fa3938a45f0cdf746037aed50f268a515f6593df92d9975415b` |
| inventomatic | patched InventOmaticStash-UO securetrade.swf + InventoryTracking.swf | `inventomaticstash-mod/archive/interface/securetrade.swf`, SHA-256 `b62142393cd08ae0f8aa2eb2e0525dece8485c423f79483b28fc79d187d1f250` |
| child-only | InventoryTracking.swf only (for when the other author has added the hook) | none |

How it works: `ffdec -importScript` replaces only classes that already exist, so the child is compiled into a generated stub SWF (`empty_swf()` in the build script: a minimal FWS with a stub class `InventoryTracking`). The hook is inserted into the base `SecureTrade.as` after `super.onAddedToStage();` (the anchor must occur exactly once) as `this.loadInventoryTracking();` plus the method from `hook/hook-methods.as`; standalone also adds the `__SFCodeObj` field. The result is compiled with FFDec into a copy of the base SWF. `--debug` swaps in `hook/hook-methods-debug.as`: a black multi-line box lists each step (`hook`, `loaded`, `child attached`, which class-lookup route worked, `ctx <ok> mode <n>`, errors); the child's `itDbg()` calls feed it (no-ops in release because the host has no `itDbgMsg`).

After a game update: re-extract `interface/securetrade.swf` from `Data\SeventySix - Interface.ba2` to `exporter/vanilla/securetrade.swf`, FFDec-export its scripts to `exporter/vanilla/source`, update the hash in the script, re-check the injection. **Verify every build by decompiling** (`java -jar ffdec.jar -export script <dir> <swf>` and search for the new functions): FFDec can skip a script that fails to compile without a clear error; a clean compile is not proof of correct runtime behavior.

### 6.5 Deploy (needs the owner's go-ahead; the game must be closed)
1. Confirm with the owner and check `Project76_GamePass` is not running (never kill it).
2. Check the symlink: `(Get-Item "D:\XboxGames\Fallout 76\Content\Data\InventoryTracking.ba2").Target`.
3. Back up the staging target, copy `exporter\build\<mode>\InventoryTracking.ba2` over it, compare SHA-256 of staging file and the `Data` path. The INI already lists the archive (last).
4. The owner starts the game, opens a storage menu and uses the buttons; exports appear in `Data\modsdata`. Steam: same with the `...\23\...` staging target.
Combined case: `InventOmaticStash-UO.ba2` must stay enabled (it supplies `InventOmaticStash.swf`); its own securetrade.swf is shadowed by ours.

### 6.6 Modifying the mod: rules learned the hard way
- All code is in `exporter/child/InventoryTracking.as` (prefix `it`); `host` is the SecureTrade instance found by walking up the parents until one has `PlayerInventory_mc`. Reach the menu only through public members.
- The child cannot see the menu's classes through `getDefinitionByName` or `ApplicationDomain.currentDomain` (ReferenceError #1065); use `host.loaderInfo.applicationDomain.getDefinition(name)` (`itClass()` tries several routes and reports the one that worked in debug). `BSUIDataManager` and `CustomEvent` are fetched that way.
- Private menu members are reproduced from public data: `m_CorpseLootMode` -> `OtherInventoryData.CorpseInventories`; `updateModalActive()` -> modal `opened/open/Active` flags and `host.modalActive`.
- `setTimeout`/`clearTimeout` need `import flash.utils.setTimeout/clearTimeout` and plain calls.
- Do not set properties on `Stage`. Do not rely on classes of other mods.
- The mod must never change inventory: no transfer, scrap or rename events.
- The whole `SecureTrade` class is recompiled by FFDec, so vanilla behaviour can change in ways a decompile does not show (section 6.7). After every change, test an ordinary transfer (quantity dialog, accept, cancel) in game, not only the export.
- `exporter/build/<mode>/host/SecureTrade.as` is generated output, not source.
- Keep documentation comments that describe discoveries.

### 6.7 Fixed bug: wrong item from the ammo box, and the menu that stayed dark (2026-10-04)
**Report:** with the mod loaded, clicking an item opened "Set quantity"; moving the mouse over another row selected that row, and accepting transferred the *other* item; without the mod the whole menu greys while the dialog is open and the lists ignore the mouse.

**Cause (measured, builds v14-v17):** the game's modal state is correct (`ModalSetQuantity_mc.opened`, `modalActive`, lists get `disableInput`), but in the FFDec-rebuilt SWF the menu timeline does **not play** the fade animation: the playhead jumps to frame 50 (`modalFadeOut`, first frame, light) when the dialog opens and to frame 60 (`modalFadeIn`, first frame, grey) when it closes, and stays there. Timeline tags, labels, frame scripts (stop on 1, 32, 49, 59, 69) and the setter are identical to vanilla; the root cause was **not found**. Compiled-code differences: FFDec writes setter assignments with `setproperty` where vanilla used `initproperty` (flipping all 166 back did not help; reverted) and adds `debug` opcodes. A/B tests without the polling timer or the stage text fields changed nothing.

**Fix (in the child):** `itGuard`: a capture-phase stage handler swallows mouse events aimed outside an open quantity/price dialog. `itFrame` (ENTER_FRAME): when the dialog opens or closes it re-runs the modal-state logic (`itUpdateModalActive`) and then `host.gotoAndStop(59)` (grey end frame) on open or `gotoAndStop(69)` (light end frame) on close. Owner verified on test build v17; also still needed (and in use) on the merged InventOmaticStash menu. Open: why the playhead stalls (a cleaner fix would find that), and whether the guard is still needed (harmless). If a diagnostic is needed again, add a temporary label showing `ModalSetQuantity_mc.opened`, `modalActive` and `currentFrame`.

Facts about selection: the mod only changes selection during an export (dispatches `SecureTrade::OnItemSelected` per legendary row, then re-dispatches the originally selected item; never a transfer). Vanilla transfers read `selectedListEntry` and send `Container::TransferItem` (`serverHandleID`, `quantity`, `fromContainer`, `containerID`). The vanilla selection event also carries `containerID`; the mod's dispatches omit it (rows carry a `containerID` field, so it could be sent).

## 7. Compatibility with InventOmaticStash-UO

### 7.1 The problem
Both mods ship `interface/securetrade.swf`; the game loads only one: the archive listed last in `sResourceArchive2List` wins. Ours is last, so before v1.4 our vanilla-based SecureTrade hid InventOmaticStash-UO's features (learned-mod markers, scrap info, "Store all junk", "Inspect", MODS/JUNK tabs, ...).

InventOmaticStash-UO (v2.85; upstream https://github.com/MsZelia/Invent-O-Matic-Stash, GPL-3.0, folders `Config/`, `InventOmaticStash/scripts`, `securetrade/scripts`) consists of:
- `interface/securetrade.swf`: the vanilla SecureTrade menu with 11 of 126 classes edited (SecureTrade, ItemCard, ItemCard_Entry, ItemCard_ItemHealthEntry, OfferListEntry, PlayerListEntry, SecureTradeInventory, SecureTradeOfferInventory, SecureTradePlayerInventory, SecureTradeScrapConfirmModal, QuantityMenu). Its SecureTrade loads the child with `loadMod("InventOmaticStash.swf")` (Loader, `ApplicationDomain.currentDomain`) and has `public var __SFCodeObj:Object = new Object()`.
- `interface/InventOmaticStash.swf`: the features; reaches the menu through `_parent` and uses `__SFCodeObj.call("writeItemsModFile", ...)` optionally (null-checked; does not conflict with our `modStorage` use of the same field).

### 7.2 The solution
Our code lives in its own child SWF; SecureTrade only carries a small hook (about one call plus a 14-line method). Two ways to ship it:
- **Merged (works today, local use):** our archive contains InventOmaticStash's `securetrade.swf` plus the hook, and `InventoryTracking.swf`. Our archive is last, so ours is the menu the game loads; their archive stays enabled for `InventOmaticStash.swf`. The copy of their menu shadows their updates until we rebuild (7.4).
- **Child-only (target):** the other author adds the hook (7.5); we ship only `InventoryTracking.swf`, no copy of their code.

### 7.3 `inventomaticstash-mod/` (tracked baseline)
Pristine, unmodified copy of 2.85, committed first so every change we make to their code is a git diff: `archive/interface/*.swf` (as shipped), `source/securetrade` and `source/InventOmaticStash` (FFDec export: `ffdec -export script <dir> <swf>`), `inventOmaticStashConfig.json`, README. Source archive: `D:\FalloutModding\ModsDownload\mods_staging\2335\23596\InventOmaticStash-UO.ba2` (extract with `tools/ba2x.py`). The build never edits these files; it copies and patches `source/securetrade/scripts/SecureTrade.as`.

### 7.4 When InventOmaticStash-UO (or the game) updates
1. Extract the new `interface/securetrade.swf` and `InventOmaticStash.swf` from the updated BA2 into `inventomaticstash-mod/archive/interface/`.
2. Re-export both with FFDec into `source/securetrade` and `source/InventOmaticStash` (replace the folders), update README/version; `git diff` shows what the author changed; commit as its own baseline commit.
3. Update `BASES['inventomatic']['sha256']` in `exporter/build_exporter.py` (lowercase SHA-256).
4. Build `inventomatic`, check `securetrade-hook.diff` still shows only the hook, deploy (ask first) and test (7.6). Same procedure for a new game version but for `standalone`.

### 7.5 Giving the hook to the other author (preferred long-term fix)
Send `exporter/build/inventomatic/securetrade-hook.diff`: one added line after `super.onAddedToStage();` plus one method (needs an IOError listener so a missing file is ignored). Their `loadMod(filename)` already takes a file name, so the minimal form is a second Loader field plus `loadMod("InventoryTracking.swf")`. A pull request against `securetrade/scripts/SecureTrade.as` is the natural route (GPL-3.0). Once shipped, use `child-only`. The merged build contains their GPL code: fine for personal use; publishing it would require GPL terms and their source.

### 7.6 Verification in the game
Open the stash: their features show (learned-mod markers, "Store all junk", "Inspect", MODS/JUNK tabs, scrap info) AND our buttons (`Export player inventory`, `Export stash`, `Part n`, `IT vX.Y`). Run an export: the status must say "Exported ...". Take an item (quantity dialog): the menu must not stay grey and the right item must be transferred. With a `--debug` build the black box shows the steps; **no box at all = our securetrade.swf is not the one loaded** (check archive order); `class ... via host.loaderInfo` and `ctx true mode 6` are the healthy lines.

### 7.7 Lessons (history of the integration, do not repeat)
- First deploy showed no buttons: the debug box showed `child attached` then `init error ReferenceError #1065`, later `ctx err ... #1065` from `getDefinitionByName` failing for `BSUIDataManager` (the child cannot see the menu's classes that way). Fixed by the `host.loaderInfo.applicationDomain` route.
- The fully qualified `flash.utils.setTimeout(...)` form also failed; imports fixed it.
- Whole-class recompile of their SecureTrade by FFDec has the same behaviour change as for vanilla (modal fade stall), which the child's `itFrame` works around; their file's other behaviours were not re-verified beyond the visible features.
- A BOM written by PowerShell breaks `.as`/`.py`; the build script reads hook files without BOM issues now.

## 8. Decoder
The viewer decodes in C# (`inventorytracking-ui/src/InventoryTracking.Core/Decoding`). The Python decoder is the reference implementation and the oracle for the C# parity tests; it is not updated for newer fields (for example `currencies`, `vending`).
```powershell
cd "D:\XboxGames\Fallout 76\Content\Data\modsdata"
python decode_export.py          # reads ./itemdump_*.json, clears ./decoded/*.json, writes ./decoded/<character>_<storage>_<snapshot>.json
```
It reads `itemdump_*.json`, decodes `zlib+base64`, verifies `rawBytes`, merges parts of the same account/character/storage/snapshot, warns about missing parts, and splits each legendary `desc` into `effects` `{star, text, marker}` (lines with `§` or without marker get `star: null`). The copy in the game's `modsdata` folders is not needed any more.

## 9. Viewer

### 9.1 Run, build, test
```powershell
cd D:\FalloutModding\Repos\f76-inventory-tracking\inventorytracking-ui
dotnet build                              # runs npm ci / npm run build when web/dist is missing or stale, and embeds web/dist
dotnet run --project src/InventoryTracking.Server
dotnet test                               # use -c Release while a Debug copy runs (it locks the Debug output)
cd web; npm test; npm run type-check
dotnet publish src/InventoryTracking.Server -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish   # 73.7 MB; add --no-self-contained -p:EnableCompressionInSingleFile=false for a 0.48 MB build that needs the .NET 8 runtimes
dotnet publish src/InventoryTracking.WebHost -c Release   # IIS build; steps in the viewer README
```
Test data: `tests/fixtures/real` (five real exports: player 575 rows, stash 719, scrap 42, ammo 33, aid 38) and `tests/fixtures/oracle` (decoder output for them; C# output must equal it). Flags for tests/scripted runs: `--no-browser`, `--no-tray`, `--folder` (repeatable), `--settings-dir`, `--port`, `--instance-id`, `--mode local|web|both`. When starting from PowerShell, quote paths with spaces yourself in `Start-Process -ArgumentList` (a string, not an array).

### 9.2 Decisions
| Question | Decision |
|---|---|
| Form | One .NET program: local web server with all logic plus an HTML/JS UI in the default browser; no in-app window |
| Frontend | Vue 3 + Vite + TypeScript, embedded in the executable (no CDN) |
| Port | 8080, then +1 up to 100 tries; a second start never starts a second backend, it opens the browser on the running one (per-user named pipe) |
| Data | No database, no history: files are read directly and decoded in memory; the only file written is `%LOCALAPPDATA%\InventoryTrackingViewer\settings.json` (folders, UI prefs) |
| Folders | Several export folders read together (Steam, Xbox, added ones; `folders` in settings, old single `folder` migrated); first-run page offers detected installs (Steam registry/libraries, `XboxGames\Fallout 76\Content\Data\modsdata`), a folder browser backed by the server, or a typed path (game install or `modsdata` folder both resolve) |
| Quit | Tray icon (*Open in browser*, *Quit*; double click opens) and a *Quit* button (confirm, then `POST /shutdown`); closing the tab does not stop it; no console window; startup problems in a message box |
| Links | Google and wiki *search* links only (`Fallout 76` + cleaned name); no direct wiki page links. Name cleaning removes leading dots, `¢`, trailing counts like `(8)`, star glyphs |
| Windows | Server host Windows-only (tray); Core portable |

Input handling: reads `itemdump_*.json` directly inside the selected folder only; decodes per file (parse header, base64, inflate, check `rawBytes`, parse rows; also accepts plain probe files with a `data` array); rejects `rawBytes` over 16 MB or inflated size over `rawBytes`; header `v` must be 1 (a higher major shows "needs a newer viewer", extra fields ignored); groups by account/character/storage/snapshot and merges parts, marking groups complete, incomplete (missing parts listed) or mixed snapshots (separate groups); a damaged file shows name and reason without stopping the rest; the server watches the folder, the page polls a data-version value every few seconds, a refresh button forces a reload, export age is shown. Effects: each `desc` line ends with one `¬` per star; `§` lines are special; unmarked lines are extra description. Totals (rows, count, weight = count x weight, legendary count, value) are computed; the file has no weight limit.

### 9.3 Features
**Inventory page**
- Overview of characters and storages (row counts, capture age, completeness, currency chips per character). Selecting is hierarchical: *Everything*, a **character** (click its name: all storages of that character; the account is part of the selection because two accounts can have a character with the same name), or one **storage** card. A selected character hides the character column; "Clear filters" keeps the selection and sort.
- Sortable item table with a column chooser; type chips; legendary only, favorites, equipped, quest; star count (including the stars filter for legendary mod boxes); vendor filter and listed price ("FOR SALE" badge); text search over names and effect text (every word must match, highlighted); details pane (all fields, effects by star); Google/wiki search links; copy, and CSV/JSON download of the filtered view (the export follows the selected character too).
- **Guide tab:** user guide and downloads, see 9b. **Phone layout** (window narrower than 700 px, `useNarrow`): storages, filters and tools fold into one "Storages & filters" bar (closed by default, shows the number of active filters) with a sort picker and direction button; the list becomes two-line cards (name, stars, count, total weight, total value, listed price) and keeps at least 65% of the screen height (the page scrolls otherwise); item details open as a full-screen overlay with a "Back to list" button; the calculator stacks its settings and scrolls its table sideways. Not adapted yet: Import and Settings pages (the header tabs wrap).

**Mod calculator tab** (all modes; rules, defaults, API and tests in 9.3a) turns legendary mods into legendary modules and Leaders so a stash can be priced for bulk trade.
- A legendary mod is an item of type `mods` with `stars >= 1`. Modules per mod = 3 x 2^(stars-1) (3, 6, 12, 24, 48). Rows with the same name and star count are combined (vendor and loose stacks, several storages). Scope: everything or one storage.
- **Price in L** = modules x rate. One rate, default 0.3 L per module; a switch chooses whether it is entered per module or per 1-star mod (= 3 modules). Only one price column (L) is shown; there is no other unit and no caps price.
- **Junk** = every legendary mod that is not on the valuable list. The valuable list is **star-specific** (2* Luck and 3* Luck are separate entries), edited directly in the table with a *Make valuable / Make junk* button per row (splitting an old all-tier entry when only one tier changes); presets *Default list*, *Prime effects only*, *Nothing valuable*, *Reset everything*. Default list: the prime effects (1* Bloodied, Bolstering, Furious, Mutant's, Quad, Stalker's, Two Shot, Unyielding; 2* Vital; 3* Lucky Hit), 29 sought 4* mods taken from one day of a trade channel, and the owner's additions (4* Ranger's, Runner's; 3* V.A.T.S. Optimized, Swift; 1* Vampire's; 2* Luck, Intelligence, Strength, Crippling, Endurance, Explosive, Powered, Rapid). Details and the reasoning are in the dev spec. Names match without regard to case, punctuation and a trailing plural "s".
- Display: a *Show* choice (junk only; all, with valuable rows marked and tinted; valuable only); a foldable header row per star tier with that tier's mods, modules and price in L (valuable mods are shown but in no sum); a totals line with the mods per star, a grand total row, and *Copy as text* (junk rows, tab separated).
- **Browser storage:** the calculator settings (valuable list, rate and method, view, folded tiers) are in `localStorage` key `inventorytracking.moduleSettings`, read defensively and sent with each request; the server stores nothing. The theme/column preferences stay as before (settings file locally, `localStorage` in the web version).

Later ideas (no storage needed): "where is it" search across all characters, totals by name, legendary explorer by effect, trade-list builder, privacy mode (mask account), shortcuts. A hint that suggests valuable mods by comparing vendor prices with the base price per tier (1* 1000, 2* 3000, 3* 5000, 4* 7000) was offered, not built; the vendor maximum of 10,000 also appears on non-prime 4* mods (Pyromaniac's, Viper's), so it is only a hint. Parked (needs stored data): history/diffs, notes/tags, plan checklist.
### 9.3a Mod calculator: rules, defaults, API, tests (merged from the former dev spec)
**Purpose.** Players trade legendary mods in bulk; junk mods are priced by the legendary module they break down into, quoted in Leaders (L). The tab replaces asking a chat assistant to count a screenshot (the original examples were such chat answers: mod, stars, count, modules, leaders per tier, subtotals and a grand total).

**Rules.**
- A legendary mod is an item with type `mods` (filter bit 32768) and `stars >= 1` (the decoder already strips the leading `¬` characters). Stars above 20 are ignored.
- Modules per mod = `3 x 2^(stars-1)`. Price in L = modules x rate (default 0.3 L per module, from trade chat: about 3 L per 10 modules). Decimals are used so 0.3 x modules is exact.
- Rows with the same name and star count are combined (vendor-listed and loose stacks, several storages); the exporter always writes a `count`.
- Vendor status does not matter: loose copies and vendor-listed stacks of a junk mod all count. (Open: an option "count only copies not listed at the vending machine" is not built.)
- Valuable list entries are `{name, stars|null}`; `null` matches every tier, a number only that tier. Matching ignores case, punctuation and a trailing plural "s" (`Pin-Pointer's` = `pinpointers`) but is not fuzzy, so spellings must resemble the game's. Entries are star-specific by default because the same name exists at several tiers (Intelligence, Luck, Endurance are 2* and 3* mods and only some tiers are valuable).

**Settings** live only in the browser (`localStorage` `inventorytracking.moduleSettings`: `valuable`, `leadersPerModule` stored per module, `method` = `perModule`/`perOneStar`, `view` = `junk`/`all`/`valuable`, `collapsed` star tiers), also in local mode, because the calculator is a per-person convenience; they travel with each request. Reading is defensive: garbage or blocked storage gives the defaults; an empty list is valid and means "all junk"; an old `showValuable` flag becomes `view`; old price fields are ignored; an untouched copy of the first (all-tier) default list is replaced by the current default, an edited list is kept. A changed default list therefore only reaches browsers that have not saved one, or after *Default list* is clicked.

**Default valuable list** (`web/src/utils/valuableMods.ts`):
1. Prime effects (10,000 caps and up; tiers as seen in a real export): 1* Bloodied, Bolstering, Furious, Mutant's, Quad, Stalker's, Two Shot, Unyielding; 2* Vital; 3* Lucky Hit. The 4* Pyromaniac's and Viper's were also listed at 10,000 but count as junk: that is the vendor maximum, not a premium.
2. Sought 4* mods from one day of a PC trade channel (junk 4* mods quoted 3-7.5 L, these 15-45 L), all 4*: Aegis, Battle Loader's, Bruiser's, Bully's, Charged, Choo-Choo's, Combo-Breaker's, Conductor's, Electrician's, Fencer's, Fracturer's, Hauler's, Icemen's, Limit-Breaking, Miasma's, Pin-Pointer's, Polished, Pounder's, Raging, Rejuvenator's, Reflective, Satiated, Scanner's, Severing, Surgeon's, Tanky's, Tarnished, Thrill-Seeker's, Vector.
3. Owner additions: 4* Ranger's, Runner's; 3* V.A.T.S. Optimized, Swift; 1* Vampire's; 2* Luck, Intelligence, Strength, Crippling, Endurance, Explosive, Powered, Rapid.

Caveats: part 2 is a one-day sample and errs towards "valuable"; the earlier hand-made counts treated e.g. Limit-Breaking, Scanner's, Bully's as junk, so totals differ by design. The page never learns prices; check the list against the real stash and adjust. The tiers of Ranger's, Runner's and V.A.T.S. Optimized were taken from the first chat-made lists; if one is wrong it counts as junk until the entry is fixed.

**API.** `POST /api/v1/modules/summary` (all modes; current catalog or upload session like `/items`):
```
{ "group": null | "<group id>", "valuable": [{"name":"Quad","stars":null}], "leadersPerModule": 0.3 }
-> { "tiers":[{ "stars":1, "count":.., "modulesEach":3, "modules":.., "leaders":.., "valuableCount":..,
                "rows":[{ name, stars, count, modulesEach, modules, leaders, valuable }] }],
     "count":.., "modules":.., "leaders":.., "valuableCount":.. }
```
Rows carry `valuable`; tier and total sums cover junk rows only. Validation: at most 500 valuable entries, names 1-64 characters, stars 1-20, `leadersPerModule` 0..1000; 400 with `{error}` otherwise, 404 for an unknown group. Code: `Core/Modules/ModuleCalculator.cs`, `Api/Api/ModuleEndpoints.cs`, UI `web/src/pages/ModulesPage.vue`, `composables/useModuleSettings.ts`, `utils/valuableMods.ts`.

**UI details.** The valuable list has no text editor; each row ends with *Make valuable* / *Make junk* (adds or removes a star-specific entry; turning one tier of an all-tier entry to junk splits it into entries for the other tiers). Valuable rows (views "all" and "valuable only"; in the latter a tier without valuable mods is hidden) are marked ◆ and tinted, show their count and `-` for modules and price. Tier header rows fold on click (state saved). The page recalculates, debounced, on any setting, storage or data change. *Copy as text* copies the junk rows (tab separated, tier subtotals, grand total).

**Tests.** Core `ModuleCalculatorTests` (module formula, name matching, combining rows, valuable by name and by tier, unknown group); Server `ModuleSummaryTests` (shape, validation); web `valuableMods.test.ts` (list parsing, toggling) and `useModuleSettings.test.ts` (defensive read, legacy default upgrade, tier-specific default).

**Open questions.** Vendor-listed stacks treated differently (see rules)? Is the sought list the right cut? Is 0.3 L per module right for the current market (it is a setting)?
### 9.4 Architecture and API
```
 Browser (Vue UI) <--HTTP/JSON--> Kestrel (one executable) --> Core library --> files (read-only) or uploaded session files
```
All business logic is in the backend (discovery, decoding, merging, grouping, type mapping, effects, filtering, searching, sorting, paging, totals, links, export); the UI only renders and sends queries. Data comes through `IExportSource` (local folders, composite of several folders, uploaded session). `/api/v1`: `GET /capabilities` (hides what the mode cannot do), `/status`, `/setup/candidates`, `/setup/dirs?path=`, `POST /setup/folder`, `/groups`, `/items?group=&account=&character=&type=&q=&legendary=&stars=&sort=&page=&pageSize=` (`character` + `account` select all storages of one character; `group` wins when both are given), `/items/{id}`, `/search?q=`, `/links?item=`, `/export?format=csv|json&...`, `GET /downloads/{file}` (static files of the `downloads` folder, 9b), `POST /modules/summary` (body: `group`, `valuable[{name,stars}]`, `leadersPerModule`; answers tiers with rows, sums and `valuableCount`; logic in `Core/Modules/ModuleCalculator.cs`), `POST /shutdown` (local only), plus session endpoints in web mode (create, resume by GUID, upload multiple files, delete one/several).

### 9.5 Security
Local mode: bind `127.0.0.1` only; check the `Host` header (DNS rebinding); no CORS; required header `X-InventoryTracking: 1` on every API call (downloads use fetch + browser save); files restricted to `itemdump_*.json` directly inside the selected folder (no traversal), read-only; folder browser lists directory names only; `POST /shutdown` has the same protections and is advertised only in local mode; size-limited inflate. Out of scope: other users/malware on the same machine. Web mode: request guard on allowed host names, upload limits (`MaxFileBytes`, `MaxFilesPerSession`, `MaxSessionBytes`, `MaxSessions` in `appsettings.json` section `Viewer`), session = unguessable GUID, files kept 1 week and memory copy 1 hour (`SessionMaintenance` every 5 minutes), inventory and account names leave the user's machine (consent needed). A privacy-preserving alternative would be decoding in the browser.

### 9.6 Web viewer on IIS: lessons from the first deployment
- `web.config` hides the session folder name as an IIS "hidden segment"; a hidden segment equal to a segment of an API address (`sessions` vs `/api/v1/sessions`) makes IIS answer those calls with its own 404. The default folder is therefore `session-data`; `IisConfigTests` guards this.
- A fresh site returned a blank 500 for `POST /api/v1/sessions` until the application pool identity (`IIS AppPool\<pool>`) had Modify permission on the session folder. The API now answers 503 with a readable message (`ApiErrors`), startup logs the folder problem, `ViewerRuntime.StorageProblem` holds it.
- Publish output goes to `D:\FalloutModding\Deploy\InventoryTrackingWeb`; `sessions/` and `session-data/` are excluded from publish and `.gitignore`.
- A redeploy of the newest IIS build (clearer error messages) is optional.

## 9a. Syncer (Windows tray uploader to the hosted viewer)
Folder `inventorytracking-syncer/` (README there has build steps). A minimal WinForms tray program (`net8.0-windows`, one self-contained single-file exe of about 63 MB, no installer) that watches the game's export folders and uploads new or changed `itemdump_*.json` files into an upload session of the hosted viewer (`https://f76inventorytracker.bestesoftware.eu/`, the IIS web build), so the hosted page stays current without the manual Import page. Non-goals: no viewing, decoding or editing, never changes or deletes local files, no login (the session id is the only credential, as in the viewer). Status: implemented, 35 unit tests plus a live test against a local web-mode viewer pass; the owner tried the tray and settings window on his desktop ("basic functions work"; the too-small window and missing icon were fixed afterwards).

**Behaviour.**
- **First start / settings window:** a question "start with Windows?" (Yes/No) on the very first start, then the settings window: viewer address (default the hosted site; must be https, http only for loopback), *Session link or id* (accepts the full viewer link with `?session=`, other query parameters, or the bare 32-hex id, any case; if the link points to another server the address is offered for update) with *Paste*, *Create new session* (`POST /api/v1/sessions`, puts the link into the box and on the clipboard) and *Check session* (`GET /api/v1/session`), the folder list (checkbox per folder, *Add folder...*, *Remove*, *Detect Steam / Xbox*), the start-with-Windows checkbox, a privacy note (the upload sends inventory data including account and character names to that address; the session id is the access key) and *Save and start* / *Cancel*. Saving checks the session first: unknown (404) blocks the save with a message, a network problem asks "Save anyway?". The window scales with Windows display scaling and can be resized (an earlier fixed size cut off the buttons at 150%).
- **Folder detection** reuses `InventoryTracking.Core.Setup` (project reference, Core stays portable): Steam via `HKCU\Software\Valve\Steam\SteamPath` and `libraryfolders.vdf` -> `steamapps\common\Fallout76\Data\modsdata`; Xbox `<fixed drive>:\XboxGames\Fallout 76\Content\Data\modsdata`; *Add folder* accepts an install folder or a `modsdata` folder (`GameFolderResolver`). A folder that does not exist yet may stay enabled; tray item *Update Xbox / Steam folders* re-detects and offers only new finds (existing entries and the user's disable choices are never removed or changed).
- **Sync:** a round runs at start, about 2 s after file events (`FileSystemWatcher`, debounced because the game writes in a burst), every 60 s as a rescan (watchers miss events on Xbox/OneDrive-managed folders; `rescanSeconds` in `settings.json`, 10..3600, no UI), and on *Sync now*. A file is uploaded when the SHA-256 of its bytes differs from what the tool last uploaded under that name to that session (hashes are kept in `settings.json`, so a restart re-uploads nothing and a new session starts clean). Several changed files go in one `multipart/form-data` request (field `files`, up to 20 per request) to `POST /api/v1/session/files` with headers `X-InventoryTracking: 1` and `X-InventoryTracking-Session: <id>`; the same name replaces the old file. If Steam and Xbox hold the same file name, the newest write time wins. Checks before sending mirror the server: name rule `itemdump_<...>.json` (length <=120, safe characters), 1 B..256 KB, at most 100 files and 5 MB per session; skipped files are logged.
- **Mirrored deletions** (owner decision): a file this tool uploaded that is gone from every enabled folder is removed from the session (`POST /api/v1/session/files/delete`). Safety: only names this tool recorded for that session are deleted (manual uploads stay), nothing is deleted while any enabled folder is missing or unreadable (an unplugged drive must not wipe the session), a locked file that still exists counts as present.
- **Errors:** 404/401 -> status "Session unknown or expired", one balloon, checks again only after an hour or on *Update session*/*Sync now*; network errors and 5xx -> retry with back-off 30 s, 1, 2, 5, 10 min; a refused file (per-file `accepted:false`) is not sent again until its content changes; a locked file is retried after 5 s; redirects are not followed (the session header must not leave the configured host); TLS errors are never ignored; server answers are size-limited JSON.
- **Tray:** icon drawn in code (colour = state: grey not set up, green ok, blue syncing, red problem/offline/expired) plus the exe icon `app.ico` (green circle, "IS"); tooltip `Inventory Tracking Syncer - <status>` (63 characters max); double click opens the settings. Menu: status line, *Sync now*, *Open viewer in browser* (`<server>?session=<id>`), *Update session...* (paste dialog), *Update Xbox / Steam folders...*, *Settings...*, *Start with Windows* (checkable), *Open log folder*, *Quit*. Balloons only for the first successful sync and for a change to a problem state.
- **Start with Windows:** value `InventoryTrackingSyncer` = `"<exe path>" --background` in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (no admin rights); `--background` starts hidden unless the configuration is incomplete; the value is rewritten at start when enabled and the exe moved. One instance per user (named mutex); a second start asks the running one to show its settings window.
- **Files and command line:** `%LOCALAPPDATA%\InventoryTrackingSyncer\settings.json` (atomic write; unknown fields are kept; a damaged file is renamed `.bad` and the first-start flow runs again) and `syncer.log` (about 1 MB, one old file kept; no file contents, session id not logged in full). Flags: `--background`, `--server <url>`, `--settings-dir <path>` (tests). Settings JSON: `version`, `server`, `session`, `folders[{path, enabled, origin: steam|xbox|manual}]`, `startWithWindows`, `askedAboutAutostart`, `rescanSeconds`, `uploaded{<session>{<file name>: <sha256>}}`.
- **Decisions (owner):** can create sessions from the tool; newest write time wins for duplicate names; deletions are mirrored (with the safety rules); self-contained single file; autostart question on first start; one session per installation (two PCs may share one: same names replace each other, deletions only touch names the PC uploaded).

**Code layout.** `src/InventoryTracking.Syncer/`: `Program.cs` (single instance, flags), `SessionLink.cs` (link/id parsing, server normalisation), `Settings/` (`SyncerSettings`, `SettingsStore`), `Sync/` (`SyncModel` with `SyncPlanner` and the interfaces, `FolderScanner`, `SessionClient`, `SyncEngine` = one round, `SyncRunner` = watchers, timer, back-off), `Startup/` (`Autostart` over `IRunKey`, `FolderDetector`), `Shell/` (`TrayApplication`, `SettingsForm`, `SessionDialog`, `TrayIconFactory`), `Log.cs`; `tests/InventoryTracking.Syncer.Tests/` (xUnit on `net8.0-windows`: link parsing, planner, scanner, settings store, engine with fakes, autostart, `SessionClient` with a fake handler, and `LiveViewerTests`, skipped unless `INVENTORY_VIEWER_URL` names a viewer started with `--mode web --no-browser --no-tray --port 5099`; `INVENTORY_FIXTURES` may name a folder of export files). Publish: `dotnet publish src/InventoryTracking.Syncer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish`.

**Server side.** No viewer change was needed: it uses `POST /api/v1/sessions`, `GET /api/v1/session`, `POST /api/v1/session/files`, `POST /api/v1/session/files/delete` with the two headers; the viewer's `AllowedHosts` must allow the public host name (true for the owner's site). Sessions expire one week after last use (every upload counts as use).

**Not done / ideas.** No self-update; code signing (SmartScreen may warn on a copied exe); the rescan interval has no settings UI; upload only after the game has closed; a diff notification ("stash changed: +3 rows"); a portable mode with settings next to the exe; a dedicated API token or hash-check endpoint on the server instead of the session id. Not yet verified by the owner: autostart after a real reboot, and the update of the hosted page after an in-game export (expected within about a minute).
## 9b. Guide page and downloads (viewer)
A **Guide** tab in the viewer (all modes; always visible, also before a folder is set up; `#guide` and `#guide-<section>` open it) explains the whole flow to a player and offers the downloads. Audience: a Fallout 76 PC player, not a developer (no "child SWF", "hook" etc.). English only. Files: `web/src/pages/GuidePage.vue` (static text, no runtime fetch), `components/DownloadsTable.vue`, `utils/downloads.ts` (manifest parsing), `utils/guideFacts.ts` (the one place for the tested versions - `modLabel` `IT v1.4`, game 1.7.26.22, xScal 0.2.20, InventOmaticStash-UO 2.85 - plus the section list and hash parsing). A test checks that every section id has an element on the page and that the page states "Not tested with ZFE". On a phone the contents list folds into a "Contents" button.

**Sections:** what this is (4-step flow: mod in the game -> files in `Data\modsdata` -> viewer or Syncer -> this site); what you need (Fallout 76 on Steam or Xbox/Game Pass, the xScal Scaleform Extender, optional InventOmaticStash-UO; a callout with the tested versions and "Not tested with ZFE ... should work, nobody has checked"); which mod version; install the mod; use it in the game; get the data into the viewer; using the viewer; troubleshooting; downloads.
- **Which version:** *Standalone* (no InventOmaticStash-UO) or *Compatible with InventOmaticStash-UO* (**guaranteed with 2.85**; keep their `InventOmaticStash-UO.ba2` enabled). Use exactly one; other mods that replace the storage menu cannot be combined; after an InventOmaticStash-UO update the compatible build may hide the update's menu changes until a new build is published; the page says the author has not been contacted and that the compatible build is our own build for 2.85. The child-only build is not offered (nobody ships the hook).
- **Install by hand:** copy `InventoryTracking.ba2` into `Data` (Steam `...\steamapps\common\Fallout76\Data`, Xbox `<drive>:\XboxGames\Fallout 76\Content\Data`); edit `Fallout76Custom.ini` (Steam) or `Project76Custom.ini` (Xbox/Game Pass) in `Documents\My Games\Fallout 76`; add the archive **last** in `sResourceArchive2List` (comma separated, no spaces); start the game, the label `IT v1.4` at the top left of a storage menu proves it is loaded. **Mod managers (owner's recommendation):** **Nukamods** (Fallout 76 specific, Steam and Xbox, copies archives into `Data`, enable/disable; beta, back up the INI) and **Fallout 76 Quick Configuration** (INI editor and mod manager, detects Steam and Xbox paths and INIs, drag-and-drop `.ba2`, bulk enable/disable, live log); **Vortex is deliberately not recommended**, Mod Organizer 2 is not mentioned; with any manager check that the archive is last and the label shows; managers are community tools, the manual way always works. (Descriptions came from Nexus page snippets found by web search on 2026-10-09, not from the full pages.)
- **In the game:** stash, scrap box, ammo box, aid box; buttons top left; table of buttons (player export in the stash menu only, box export in each, Part N for very big boxes, version label); one export per menu open ("close and reopen"); big boxes take a few seconds (legendary cards are read one by one); several characters and accounts are safe because file names carry account and character, re-exporting replaces only that file, no history; a suggested routine per character (stash menu: player + stash, reopen, then scrap, ammo, aid); files go to `Data\modsdata`.
- **Into the viewer:** local viewer exe (finds the folders itself), hosted site + Syncer (Import tab creates a session and the link; paste it once), manual upload; a short Syncer summary and a privacy callout (account and character names are uploaded; the session link is the only key; sessions kept a week after last use; SmartScreen may warn because the exe is not code-signed, compare the SHA-256).
- **Troubleshooting:** no buttons (archive missing/not last, xScal missing - `xscal.log` in `Data` -, another mod replaces the menu), save fails (xScal, second export in one menu open, huge box), nothing in the viewer (not exported, wrong folder, expired session), Syncer "Session unknown or expired" (new link, tray: Update session), buttons gone after an update (outdated archive), odd state after an export (close and reopen, report the game version). The page links the GitHub repository (https://github.com/stefanbelkot/f76-inventory-tracking, MIT) for source and issues.

**Downloads.** The site serves a `downloads` folder next to the application at `GET /downloads/{file}` (`EmbeddedUi`, web and both mode, option `Viewer:DownloadsDirectory`, default `downloads`; read per request so the folder can be added without a restart; `downloads.json` is `no-store`, other files are attachments with range support; unknown or escaping paths 404; local mode has no downloads and the table then says downloads are not available on this server). The files are not in git and not embedded in the DLLs (the Syncer zip alone is 57.6 MB). `tools/package_release.py [--out <folder>] [--syncer <exe>]` (default out `D:\FalloutModding\Deploy\InventoryTrackingDownloads`) reads the built archives (refuses on a hash mismatch with `hashes.json` or when the mod source is more than 5 s newer than the build), `IT_VERSION` from `exporter/child/InventoryTracking.as`, the published Syncer exe and the Syncer `<Version>` from its csproj (the single-file exe's version resource is not readable from Python), and writes `InventoryTracking-<ver>-standalone.zip`, `InventoryTracking-<ver>-with-InventOmaticStash-UO-2.85.zip` (both: `InventoryTracking.ba2` at the zip root + `INSTALL.txt` for managers and hand installs), `InventoryTrackingSyncer-<ver>.zip` (exe + README.txt) and `downloads.json` (`{updated, files[{id, title, version, file, bytes, sha256, notes, requires{game, xscal, inventomatic}}]}`). The page shows size, requirements, a copyable SHA-256 and a download button per row. It never builds and never touches the game. The standalone zip's note says that the standalone menu variant is not yet confirmed in game; remove that sentence in the script once it was tested.
- **Release procedure:** build the mod (`python exporter/build_exporter.py`), publish the Syncer (`dotnet publish ... -o <folder>`; quit a running Syncer first or publish to another folder and pass `--syncer`), `dotnet publish src/InventoryTracking.WebHost -c Release -o D:\FalloutModding\Deploy\InventoryTrackingWeb`, then `python tools/package_release.py --out D:\FalloutModding\Deploy\InventoryTrackingWeb\downloads [--syncer <exe>]`; copy the whole folder to the IIS site (stop the site or pool first, keep the server's own `appsettings.json`; the package has no session folders). The 2026-10-10 package was started locally and checked: manifest 200 and not cached, zip as attachment, page served.
- **Tests:** web `downloads.test.ts` (manifest parsing, sizes, urls), `guideFacts.test.ts` (sections, ZFE statement, hash routing); Server `DownloadsTests` (attachment, manifest cache header, 404 and path escape, no downloads in local mode). Not yet looked at in a browser or on the deployed site.
## 10. Research record (how the design was reached; kept for reference)

**The old mod (InventOmaticStash 2.76 export).** The original exporter (`BaseItemExtractor`/`ItemExtractor`) wrote `Data/itemsmod.ini` through `__SFCodeObj.call("writeItemsModFile", data)` and printed "Done saving items!" without checking a result. Defects found by static analysis (not a diagnosis of the owner's failure): second pane always exported as "stash"; export read from the UI lists whose content depends on the tab (the New tab substitutes the backing list, `m_NewItems`); selection followed by an immediate cache read (timing races, stale shared card cache); rows silently lost when a card read threw; live mutation of game row objects; no busy guard or cleanup; changed pane/selection without restoring it; no snapshot/completeness/schema contract; nonlegendary cards not requested. The new mod avoids these: it reads the providers (`PlayerInventoryData`/`OtherInventoryData` `.InventoryList`), freezes and sorts rows, correlates cards by handle with timeout and retry, restores the selection, verifies the save by read-back and reports an honest status.

**Providers and identity.** `SecureTrade` subscribes to `PlayerInventoryData`, `OtherInventoryData`, `OtherInventoryTypeData`, `ContainerOptionsData`, `InventoryItemCardData`. Storage mode values: container 0, scrap 7, ammo 8, aid 9 (`SecureTradeShared.as`; `isScrapStash` etc.), plus `storageMode` 6 for the stash as observed. `menuType`, `menuSubType` and `storageMode` are separate fields. `serverHandleID` and `containerID` are session identities, not base FormIDs.

**Bridge options considered.** xScal `modStorage` (custom file name under `Data/modsdata`, 40 KiB, one name per movie session), the common legacy `writeItemsModFile` (fixed `Data/itemsmod.ini`, cannot keep separate captures), and ZFE (`writeStorage` with vendor + relative path, 1 MiB, also implements modStorage; untested; disabled on Game Pass with runtime identity 1.0.171.0 vs expected 1.0.169.0). General ZFE replies are JSON with a `success` field (do not test the string for truthiness). The author pages: xScal API https://www.nexusmods.com/fallout76/articles/259, modStorage https://www.nexusmods.com/fallout76/articles/262, ZFE https://www.nexusmods.com/fallout76/articles/248, 254, 255.

**Design that was planned and then replaced.** The original plan (partitioned output: chunk files with manifest, two reusable banks `a`/`b`, an index file, optional debug log `itemdump_log_001.json`, a schema with `raw` row copies and per-item `itemCard` status enum, a full export state machine PREFLIGHT..VERIFY) assumed several files per export. The probes (v5/v6) showed that helper SWFs get no storage binding and modStorage allows one name per menu open, so the shipped design is one compressed file per export, one export per menu open, optional parts needing several menu opens. The extra ideas are not built: opt-in development logs, multi-file chunk manifests, base-name resolution. Useful principles from that plan that still hold: measure UTF-8 bytes against the 40,960 limit before every write; never silently drop rows or truncate; a correlated empty card is different from no response; keep native identity fields (handles) and display name separate and never infer a base name from a label.

**Validation habits that remain useful.** Compare exported counts with the in-game list; test renamed items, equipped/favorite/locked items, empty boxes, rapid close/reopen, a transfer during export (must not mix), large inventories; record game build, source SWF hash, bridge version and edition with any evidence; never include a private character dump in fixtures except the sanitized ones already in `tests/fixtures`.

### 10.1 Chronology of problems met and tricks found (read before changing anything in the mod)
Probe phase (SecureTrade + a test panel; versions v1-v12; the probe folder was removed, see 6.3a):
1. **v1 panel invisible.** A one-shot constructor timer and an older local base SWF were the suspects. Fix: build from the currently installed vanilla SWF and initialize from `onAddedToStage` (what the mod does now: the hook runs right after `super.onAddedToStage()`).
2. **v2 panel text unreadable** (dark blocks): Arial device font. Trick: copy the `TextFormat` and `embedFonts` of `PlayerInventory_mc.Header_mc.Header_tf`, set size/color again after assigning text.
3. **v3: Error #1056 "cannot create property on Stage", host `modStorage` unavailable.** The vanilla class lacks the public `__SFCodeObj` field that xScal fills (the InventOmatic menu declares it). Declare `public var __SFCodeObj:Object` on the class; never set properties on `Stage`.
4. **v4/v5: Error #1065 before any output** from a fully qualified package-level call (`flash.utils.getQualifiedClassName`). Same failure class returned in v1.4 with `flash.utils.setTimeout`. Use imports / avoid reflection.
5. **v5: the report itself failed** because it used `JSONEncoder` (a class of another mod, not in vanilla). Build JSON by hand.
6. **v6: first real file** (`itprobe_..._host.json`, 1,591 bytes). Proof that one small host write works; screen showed `report save: true`, `report immediate read matches: true`.
7. **v7 results:** `modStorage` has only `register`, `save`, `load`; the bridge enumerates `call`, `version`, `modStorage`, `chatInterface`. Failed registrations (`bad name!`, empty string, `null`, `a/b`) return false and lock nothing. A name registered in an earlier menu open can be read later; a missing file loads as the string `"false"`; static variables reset to 0 each open; SharedObject fails with Error #1501; a `LocalConnection` connects (cross-movie routes were never pursued).
8. **v8 crashed the game** after calling `bridge.call(...)` with guessed names (`help`, `list`, `version`, ...). The exact step was never isolated; v9 wrote breadcrumbs before each risky step, but **breadcrumbs saved as plain text were silently rejected because `save` only accepts valid JSON**. `call("version")` throws TypeError #1009 (not a string dispatcher); `chatInterface` is a chat-service client. Rule: never probe `call` or `chatInterface` again.
9. **Why one file per open:** helper SWFs (Loader, new ApplicationDomain, unload/reload) get no native bridge (own AS3 root, shared stage, placeholder bridge with no native call/storage); registering a different name through the host fails. Do not try more Loader/ApplicationDomain/variable tricks. ZFE would allow several files (1 MiB, vendor + relative path) but was disabled on Game Pass (`unsupported runtime identity: 1.0.171.0 (expected 1.0.169.0)`), so the design stayed on stock xScal.
10. **Size measurement (575 player rows):** readable named keys 227 B/row (131,081 B, 4 parts); compact arrays 78 B/row (44,890 B, 2 parts); `ByteArray.compress()` exists: part 1 went from 37,501 B to 8,124 B deflated (~10.8 KB as base64). This decided the final format: one compressed base64 field inside a JSON wrapper (valid JSON is required by `save`), decoded by a tool; the readable alternatives needed twice the menu opens.
11. **Snapshot hash negative** (`-c37b404`): `toString(16)` prints values above 2^31 signed in this runtime; hex is built from two 16-bit halves.
12. **Storage name refused** (`itemdump_testacc01_Tester_stash_1`): the uppercase `A`. All accepted names were lowercase letters, digits, underscores; account/character tokens are lowercased (exact xScal rule never confirmed because the Nexus docs return 403 to the tooling used). The refusal message includes the name length.
13. **Export design facts:** the old InventOmatic exporter's bugs (section 10 above) were avoided; the player list is restricted in scrap/ammo/aid menus, so the player export exists only in the stash menu; cards are read only for legendary items and legendary modules (cheap, fast).

Mod phase:
14. **Legendary modules (mod boxes) are not flagged legendary** (`stars` 0): the star count is the number of leading `¬` in the name, filter bit 32768. v1.2 reads cards for them too (one read per distinct name).
15. **Currencies:** not in the inventory lists; found by decompiling the Improved HUD mod (`ImprovedHBS21.ba2 > ImprovedBars.swf`): `CharacterInfoData.currencies`.
16. **Vendor data:** every row carries `vendingData` (found in the game's own list code `OfferListEntry.UpdateCurrency`); machine type 0 = not assigned.
17. **Ammo-box wrong-item / menu stays dark bug** (section 6.7): selection hover during the quantity dialog and a stalled fade animation; the first guesses (a refresh on write, a button-interference variant, no polling timer, no stage text fields, `initproperty` vs `setproperty`) did not help; the working fix was the capture-phase mouse guard plus re-running the modal state and `gotoAndStop(59/69)`. The deploy rule came from this episode: a screenshot only proves a build if the version label is bumped, and the owner once asked "was it really deployed?" (verify the hash through the `Data` symlink and show it).
18. **Deployment traps:** `Data\InventoryTracking.ba2` is a mod-manager symlink; `Get-Item.Length` is 0 for the link (that is not an empty archive); hash the content or the target; copying fails while the game holds the file (ask the owner to close it, never kill it); keep the symlink and overwrite the staging target; keep numbered backups beside it. Do not touch other mods' INI entries; InventOmaticStash-UO had been removed from the archive list during the probe phase because both replace `securetrade.swf` and was re-enabled for the compatibility work (section 7).
19. **FFDec traps:** a script that fails to compile can be skipped without a clear error (verify by decompiling the result); `-importScript` only replaces existing classes (hence the stub SWF for the child); whole-class recompile changes compiled behaviour (setter opcodes, debug opcodes, the fade stall); the vanilla SWF is hash-locked so a game update is noticed.
20. **Text-encoding traps:** PowerShell 5.1 re-encoding corrupted `¬` and `§` in a spec file once (restored from git) and BOMs break sources; use BOM-less writes (section 5).

Viewer phase:
21. **Debug build locks its output:** run `dotnet test -c Release` while a Debug copy of the viewer runs; the owner must restart the old running viewer to get new features.
22. **Starting from PowerShell:** quote paths with spaces yourself in `Start-Process -ArgumentList` (a string, not an array).
23. **IIS:** hidden-segment collision (404), missing app-pool write permission (500 -> now a 503 message), test sessions shipped in the publish folder (excluded), README fence/escaping slips (fixed), `AllowedHosts` for host names, `stdoutLogEnabled="true"` in `web.config` plus a `logs` folder for startup errors, 500.19/500.30 usually mean a missing .NET 8 Hosting Bundle or an unreadable folder, app pool ".NET CLR version" = No Managed Code, only Modify (not Full control) is needed.

InventOmaticStash compatibility phase: see 7.7 (debug-box method, `getDefinitionByName` failure, timer imports, merged-build behaviour, GPL note, the owner created the branch `inventomaticcompaitblity` after the baseline commit landed on `main`; it was cherry-picked onto the branch and later merged).
## 11. Backlog / open points
- **Base (original) item name** (nice to have): the exported `name` may be a player rename (`.mycustomnamedRifle`) or carry a legendary prefix; row fields and cards contain no base FormID. Candidate sources: a native resolver in a bridge extension; the Examine menu data (opening it from a script could trigger game actions; needs approval); a name-pattern reference list (cannot identify arbitrary renames). Keep any future value in separate nullable provenance-tagged fields (`baseName`, `baseNameSource`); never overwrite `name`.
- **More per-item flags** (cheap, the game supplies them per row): learned-recipe flag, tradable/scrap-allowed, transfer-lock, spoiled, set-item. Needs a header version bump or tolerant readers (the viewer ignores unknown fields).
- **ZFE: test it, and maybe add a ZFE export later (not done, nothing about ZFE has been tested).** Facts from the author documentation (not verified on the owner's machines): ZFE implements the legacy `writeItemsModFile` and the same `modStorage` contract as xScal (so our current export might work unchanged under ZFE, but one name per open, 40 KiB and name rules are unconfirmed there), and has its own storage API `writeStorage`/`readStorage` taking a vendor plus a relative path with 1 MiB text limit (Steam: `Data/ZFE/Storage/<vendor>/<path>`; Game Pass may fall back to Documents or LocalAppData), which could allow one file per list regardless of size and several files per menu open. Its replies are JSON with a `success` field (do not test the returned string for truthiness); `GetXSRuntimeInfo` alone is not proof of the runtime. On the owner's Game Pass install ZFE disabled itself (log 2026-10-01, unsupported runtime identity 1.0.171.0, expected 1.0.169.0), so ZFE can only be tested on Steam (or after a ZFE update). Plan: (1) on Steam with ZFE active, run the existing build and see whether `__SFCodeObj.modStorage` exists and `register/save/load` behave as under xScal (check the `Data/modsdata` file and the read-back); (2) if it works, document it; (3) optionally write a separate ZFE adapter behind capability discovery (xScal stays the default; the adapter must be explicitly configured and show its resolved location; no automatic switching by size; no silent fallback to another mod's file) using `writeStorage`, then relax the one-file-per-open and 40 KB limits for ZFE users only; (4) keep the file format and the viewer unchanged (the viewer reads `itemdump_*.json` from any added folder, so a ZFE storage folder can simply be added). Author pages: https://www.nexusmods.com/fallout76/articles/248, 254, 255 (the tooling gets 403 from Nexus; the owner can paste the text).
- Opt-in bounded development logs (planned, not built).
- Applied weapon/armor mod names only if a safe data source is found (none in the stash menu).
- Why the rebuilt SWF's modal fade stalls (a cleaner fix than `gotoAndStop`).
- Ask the InventOmaticStash-UO author to add the hook (7.5); then ship `child-only`.
- Test the `standalone` and `child-only` builds in the game; update the Steam install.
- Hosted viewer extras (accounts, share links beyond sessions, privacy-preserving in-browser decoding) are not planned.

## Appendix: decisions recorded
| Question | Decision |
|---|---|
| Menus | Full storage menus only: stash, scrap, ammo, aid box |
| Player export | Stash menu only (the player list is incomplete elsewhere) |
| Item card data | Legendary items and legendary modules only |
| Weapon/armor stats, applied mods | Not exported |
| File format | Compressed base64 inside a JSON wrapper, decoded by a tool |
| History | None; latest export per account, character and storage |
| Runtime | xScal 0.2.20 on Steam and Xbox PC; ZFE untested |
| Base item name | Follow-up |
| Other mods that replace securetrade.swf | Coexist through the child-SWF hook (section 7) |

## Appendix: public repository (2026-10-10)
The project lives in the public GitHub repository `stefanbelkot/f76-inventory-tracking` (local `D:\FalloutModding\Repos\f76-inventory-tracking`, MIT, started with a fresh history). Not in the repository (see `.gitignore`): Bethesda's SecureTrade menu (`exporter/vanilla/`, filled from the owner's installation, see its README), the InventOmaticStash-UO baseline (`inventomaticstash-mod/`, README), build output (`exporter/build/`), upload sessions of local web-host runs. The `.gitignore` patterns for sessions are anchored to `src/InventoryTracking.WebHost/` because the unanchored pattern also hid the source folder `InventoryTracking.Api/Sessions`. Test fixtures use the fake account `test.account` and character `Tester`. The mod zips carry a "Source and credits" section in `INSTALL.txt` (modified Bethesda menu code, InventOmaticStash-UO credit); the SmartScreen plan (spec/dev-smartscreen.md) continues with SignPath once the repository is public.
