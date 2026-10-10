"""Build the InventoryTracking archive; never installs it.

  python build_exporter.py [standalone|inventomatic|child-only]   (default: all three)

The exporter code itself lives in child/InventoryTracking.as and is compiled to interface/InventoryTracking.swf.
SecureTrade only gets a small hook (hook/) that loads that file:
  standalone   vanilla securetrade.swf + hook (+ the __SFCodeObj field the hook needs)       -> build/standalone/
  inventomatic InventOmaticStash-UO's securetrade.swf (pristine copy in inventomaticstash-mod/) + hook -> build/inventomatic/
  child-only   InventoryTracking.swf alone, for when InventOmaticStash-UO itself carries the hook      -> build/child-only/
Also writes build/inventomatic/inventomatic-hook.diff: exactly what the other mod's SecureTrade.as needs.
"""
from pathlib import Path
import difflib
import hashlib
import importlib.util
import json
import shutil
import struct
import subprocess
import sys

HERE = Path(__file__).resolve().parent
REPO = HERE.parent
WORKSPACE = REPO.parents[1]
FFDEC = Path(r'C:/Program Files (x86)/FFDec/ffdec.jar')
VANILLA = HERE / 'vanilla'
IOS = REPO / 'inventomaticstash-mod'
BUILD = HERE / 'build'
DEBUG = '--debug' in sys.argv
ANCHOR = '         super.onAddedToStage();'
CALL = '\n         this.loadInventoryTracking();'

BASES = {
    'standalone': dict(
        swf=VANILLA / 'securetrade.swf',
        source=VANILLA / 'source/scripts/SecureTrade.as',
        sha256='ff82f06175376fa3938a45f0cdf746037aed50f268a515f6593df92d9975415b',
        bridge_field=True),
    'inventomatic': dict(
        swf=IOS / 'archive/interface/securetrade.swf',
        source=IOS / 'source/securetrade/scripts/SecureTrade.as',
        sha256='b62142393cd08ae0f8aa2eb2e0525dece8485c423f79483b28fc79d187d1f250',
        bridge_field=False),
}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def ffdec(*args):
    result = subprocess.run(['java', '-jar', str(FFDEC), *map(str, args)], capture_output=True, text=True, encoding='utf-8', errors='replace')
    log = result.stdout + result.stderr
    if result.returncode or 'error during' in log.lower():
        raise RuntimeError(log)
    return log


def empty_swf(path):
    """A SWF holding only a stub class InventoryTracking (ffdec -importScript replaces existing classes only)."""
    def tag(code, data):
        return struct.pack('<H', (code << 6) | len(data)) + data if len(data) < 63 else struct.pack('<HI', (code << 6) | 63, len(data)) + data
    nb = 15
    bits = format(nb, '05b') + ''.join(format(v, '0%db' % nb) for v in (0, 11000, 0, 8000))
    bits += '0' * ((-len(bits)) % 8)
    rect = bytes(int(bits[i:i + 8], 2) for i in range(0, len(bits), 8))
    abc = bytearray(struct.pack('<HH', 16, 46))

    def u(*v):
        abc.extend(bytes(v))

    def st(t):
        b = t.encode()
        u(len(b))
        abc.extend(b)
    u(1, 1, 1)                      # int, uint, double pools (empty)
    u(5); st(''); st('InventoryTracking'); st('flash.display'); st('Sprite')
    u(3, 0x16, 1, 0x16, 3)          # namespaces: package "" and package flash.display
    u(1)                            # ns sets
    u(3, 7, 1, 2, 7, 2, 4)          # multinames: InventoryTracking, flash.display::Sprite
    u(3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)  # three methods ()
    u(0)                            # metadata
    u(1, 1, 2, 1, 0, 0, 0)          # class: name, super, sealed, no interfaces, iinit=0, no traits
    u(1, 0)                         # class_info: cinit=1, no traits
    u(1, 2, 1, 1, 4, 0, 0)          # script: init=2, one trait (the class)
    u(3)
    u(0, 1, 1, 0, 1, 6, 0xd0, 0x30, 0xd0, 0x49, 0x00, 0x47, 0, 0)
    u(1, 1, 1, 0, 1, 3, 0xd0, 0x30, 0x47, 0, 0)
    u(2, 3, 1, 0, 2, 15, 0xd0, 0x30, 0x65, 0, 0x60, 2, 0x30, 0x60, 2, 0x58, 0, 0x1d, 0x68, 1, 0x47, 0, 0)
    body = rect + struct.pack('<HH', 24 << 8, 1)
    body += tag(69, struct.pack('<I', 8))
    body += tag(82, struct.pack('<I', 0) + b'InventoryTracking\0' + bytes(abc))
    body += tag(76, struct.pack('<HH', 1, 0) + b'InventoryTracking\0')
    body += tag(1, b'') + tag(0, b'')
    Path(path).write_bytes(b'FWS' + bytes([34]) + struct.pack('<I', 8 + len(body)) + body)


def build_child(out_dir):
    work = out_dir / 'child'
    (work / 'scripts').mkdir(parents=True, exist_ok=True)
    shutil.copy(HERE / 'child/InventoryTracking.as', work / 'scripts/InventoryTracking.as')
    empty_swf(work / 'empty.swf')
    ffdec('-importScript', work / 'empty.swf', out_dir / 'InventoryTracking.swf', work / 'scripts')
    return out_dir / 'InventoryTracking.swf'


def build_host(mode, out_dir):
    base = BASES[mode]
    if sha(base['swf']) != base['sha256']:
        raise SystemExit(f"{mode}: base securetrade.swf changed ({sha(base['swf'])}); update the baseline first")
    original = base['source'].read_text(encoding='utf-8-sig')
    assert original.count(ANCHOR) == 1
    hook = (HERE / ('hook/hook-methods-debug.as' if DEBUG else 'hook/hook-methods.as')).read_text(encoding='utf-8')
    if base['bridge_field']:
        hook = (HERE / 'hook/hook-bridge-field.as').read_text(encoding='utf-8') + '\n' + hook
    patched = original.replace(ANCHOR, ANCHOR + CALL)
    end = patched.rfind('   }')
    patched = patched[:end] + '\n' + hook + patched[end:]
    (out_dir / 'host').mkdir(exist_ok=True)
    (out_dir / 'host/SecureTrade.as').write_text(patched, encoding='utf-8')
    ffdec('-importScript', base['swf'], out_dir / 'securetrade.swf', out_dir / 'host')
    diff = ''.join(difflib.unified_diff(original.splitlines(True), patched.splitlines(True), 'SecureTrade.as (original)', 'SecureTrade.as (with hook)'))
    (out_dir / 'securetrade-hook.diff').write_text(diff, encoding='utf-8')
    return out_dir / 'securetrade.swf'


def pack(files, output):
    spec = importlib.util.spec_from_file_location('packer', WORKSPACE / 'pack_ba2.py')
    packer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(packer)
    packer.pack_ba2([(str(src), name) for src, name in files], str(output))
    data = output.read_bytes()
    magic, version, kind, count, names_offset = struct.unpack_from('<4sI4sIQ', data)
    assert (magic, version, kind, count) == (b'BTDX', 1, b'GNRL', len(files))


def main(modes):
    for mode in modes:
        out_dir = BUILD / mode
        if out_dir.exists():
            shutil.rmtree(out_dir)
        out_dir.mkdir(parents=True)
        files = [(build_child(out_dir), 'interface/InventoryTracking.swf')]
        if mode != 'child-only':
            files.insert(0, (build_host(mode, out_dir), 'interface/securetrade.swf'))
        output = out_dir / 'InventoryTracking.ba2'
        pack(files, output)
        manifest = {name: sha(src) for src, name in files}
        manifest['InventoryTracking.ba2'] = sha(output)
        (out_dir / 'hashes.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
        print(f'{mode}: {output}  {manifest["InventoryTracking.ba2"]}')


if __name__ == '__main__':
    arguments = [a for a in sys.argv[1:] if a != '--debug'] or ['standalone', 'inventomatic', 'child-only']
    main(arguments)
