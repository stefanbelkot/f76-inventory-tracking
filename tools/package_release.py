"""Build the files offered on the viewer's Guide page: the two mod zips and the Syncer zip, plus downloads.json.

  python tools/package_release.py [--out <folder>]      (default: D:/FalloutModding/Deploy/InventoryTrackingDownloads)

Inputs (build them first, this script never builds and never touches the game):
  exporter/build/standalone/InventoryTracking.ba2 and exporter/build/inventomatic/InventoryTracking.ba2  (python exporter/build_exporter.py)
  inventorytracking-syncer/publish/InventoryTrackingSyncer.exe  (dotnet publish, see inventorytracking-syncer/README.md)
Copy the output folder to the web site as "downloads" (next to web.config). Versions are read from the build outputs.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import struct
import sys
import zipfile
from datetime import date

REPO = Path(__file__).resolve().parent.parent
GAME = '1.7.26.22'
XSCAL = '0.2.20'
INVENTOMATIC = '2.85'


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def mod_version():
    source = (REPO / 'exporter/child/InventoryTracking.as').read_text(encoding='utf-8-sig')
    match = re.search(r'IT_VERSION:String\s*=\s*"IT v([0-9.]+)"', source)
    if not match:
        sys.exit('IT_VERSION not found in exporter/child/InventoryTracking.as')
    return match.group(1)


def checked_archive(mode):
    folder = REPO / 'exporter/build' / mode
    archive = folder / 'InventoryTracking.ba2'
    hashes = folder / 'hashes.json'
    if not archive.exists() or not hashes.exists():
        sys.exit(f'{mode}: no build found in {folder} (run python exporter/build_exporter.py {mode})')
    if json.loads(hashes.read_text(encoding='utf-8')).get('InventoryTracking.ba2') != sha256(archive):
        sys.exit(f'{mode}: InventoryTracking.ba2 does not match hashes.json (rebuild)')
    child = REPO / 'exporter/child/InventoryTracking.as'
    if child.stat().st_mtime > archive.stat().st_mtime + 5:
        sys.exit(f'{mode}: the mod source is newer than the build (rebuild)')
    return archive


def syncer_version():
    """The <Version> of the Syncer project (the published exe is a compressed single file, its resource cannot be read here)."""
    text = (REPO / 'inventorytracking-syncer/src/InventoryTracking.Syncer/InventoryTracking.Syncer.csproj').read_text(encoding='utf-8')
    match = re.search(r'<Version>([0-9.]+)</Version>', text)
    if not match:
        sys.exit('<Version> missing in the Syncer csproj')
    return match.group(1)


INSTALL = """InventoryTracking {version} - {title}
=====================================================

What it is: a Fallout 76 interface mod that adds export buttons to your stash, scrap box, ammo box and aid box.
The exported files are shown by the InventoryTracking viewer. Full guide: the Guide page of the viewer.

Needs: Fallout 76 (Steam or Xbox/Game Pass) and the xScal Scaleform Extender (tested with xScal {xscal}, game {game}).
Not tested with ZFE (it should work, but nobody has checked).
{extra}
Install by hand
---------------
1. Copy InventoryTracking.ba2 into the game's Data folder:
   Steam:            ...\\steamapps\\common\\Fallout76\\Data
   Xbox / Game Pass: <drive>:\\XboxGames\\Fallout 76\\Content\\Data
2. Open (or create) the custom INI in Documents\\My Games\\Fallout 76:
   Fallout76Custom.ini (Steam) or Project76Custom.ini (Xbox / Game Pass).
3. Under [Archive] add InventoryTracking.ba2 as the LAST entry of sResourceArchive2List (comma separated, no spaces):
   [Archive]
   sResourceArchive2List=SomeOther.ba2{inv},InventoryTracking.ba2
4. Start the game and open your stash. The label "IT v{version}" at the top left shows that the mod is loaded.

With a mod manager
------------------
Recommended: Nukamods or Fallout 76 Quick Configuration. Add this zip (the archive is at the zip root), enable it, and check
that InventoryTracking.ba2 is the last archive in the list.

Use it
------
Open a storage menu and press "Export player inventory" (stash menu only) or "Export <box>". One export per menu open:
close and reopen the box for the next export. Files appear in Data\\modsdata\\itemdump_*.json.
Do not use this together with the other version of InventoryTracking; use exactly one.

Source and credits
------------------
InventoryTracking is free, MIT-licensed hobby software by Stefan Belkot. Source code and build scripts: https://github.com/stefanbelkot/f76-inventory-tracking The archive contains a modified copy of Fallout 76's own SecureTrade interface menu
(the game's menu code belongs to Bethesda Game Studios; it is changed only to add the export buttons){credit}
Not affiliated with or endorsed by Bethesda. Fallout 76 is a trademark of Bethesda Softworks / ZeniMax Media.
"""


def write_zip(path, entries):
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for name, content in entries:
            zf.writestr(name, content) if isinstance(content, str) else zf.write(content, name)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', default='D:/FalloutModding/Deploy/InventoryTrackingDownloads')
    parser.add_argument('--syncer', default=str(REPO / 'inventorytracking-syncer/publish/InventoryTrackingSyncer.exe'), help='the published Syncer exe')
    args = parser.parse_args()
    out = Path(args.out)
    version = mod_version()
    standalone = checked_archive('standalone')
    compatible = checked_archive('inventomatic')
    syncer_exe = Path(args.syncer)
    if not syncer_exe.exists():
        sys.exit(f'Syncer exe not found: {syncer_exe} (dotnet publish it first)')
    syncer_ver = syncer_version()
    out.mkdir(parents=True, exist_ok=True)

    files = []

    def add(entry_id, title, file_name, entries, version_text, notes, requires):
        target = out / file_name
        write_zip(target, entries)
        files.append({
            'id': entry_id, 'title': title, 'version': version_text, 'file': file_name,
            'bytes': target.stat().st_size, 'sha256': sha256(target), 'notes': notes, 'requires': requires,
        })

    fill = dict(version=version, xscal=XSCAL, game=GAME)
    add('mod-standalone', 'Mod: standalone', f'InventoryTracking-{version}-standalone.zip',
        [('InventoryTracking.ba2', standalone),
         ('INSTALL.txt', INSTALL.format(title='standalone', extra='\nFor players WITHOUT InventOmaticStash-UO.\n', inv='', **fill))],
        version,
        'For players without InventOmaticStash-UO. Same export code as the compatible build; the menu variant itself is not yet confirmed in game.',
        {'game': GAME, 'xscal': XSCAL})
    add('mod-compatible', f'Mod: compatible with InventOmaticStash-UO {INVENTOMATIC}',
        f'InventoryTracking-{version}-with-InventOmaticStash-UO-{INVENTOMATIC}.zip',
        [('InventoryTracking.ba2', compatible),
         ('INSTALL.txt', INSTALL.format(
             title=f'compatible with InventOmaticStash-UO {INVENTOMATIC}',
             extra=f'\nFor players WITH InventOmaticStash-UO {INVENTOMATIC}: keep InventOmaticStash-UO.ba2 enabled as well.\n'
                   'It contains a copy of that mod\'s storage menu with our export hook, so list it AFTER InventOmaticStash-UO.ba2.\n',
             inv=',InventOmaticStash-UO.ba2',
             credit=f' and a modified copy of the storage menu of InventOmaticStash-UO {INVENTOMATIC} by its author (credit to them; this build is not made or endorsed by them).\n', **fill))],
        version,
        f'Verified in game with InventOmaticStash-UO {INVENTOMATIC}. Keep InventOmaticStash-UO.ba2 enabled; list this archive last.',
        {'game': GAME, 'xscal': XSCAL, 'inventomatic': INVENTOMATIC})
    add('syncer', 'Syncer (Windows tray tool)', f'InventoryTrackingSyncer-{syncer_ver}.zip',
        [('InventoryTrackingSyncer.exe', syncer_exe),
         ('README.txt', 'Inventory Tracking Syncer\n\nA tray program that uploads your export files into a session of the viewer.\n'
          '1. Start InventoryTrackingSyncer.exe. 2. Paste your session link (Import tab of the viewer). 3. It uploads automatically.\n'
          'Windows SmartScreen may warn because the file is not code-signed: More info, then Run anyway.\n')],
        syncer_ver, 'Windows 10/11, nothing to install. Not code-signed (SmartScreen may warn).', {})

    (out / 'downloads.json').write_text(json.dumps({'updated': date.today().isoformat(), 'files': files}, indent=2), encoding='utf-8')
    for entry in files:
        print(f"{entry['file']}  {entry['bytes'] / 1024 / 1024:.1f} MB  {entry['sha256'][:16]}...")
    print(f'\nCopy the contents of {out} to the site folder "downloads" (next to web.config).')


if __name__ == '__main__':
    main()