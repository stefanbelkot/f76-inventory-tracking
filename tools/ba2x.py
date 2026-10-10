"""Extract a GNRL BA2: ba2x.py <archive> <outdir>  (list only when outdir is omitted)."""
import struct
import sys
import zlib
import os

path = sys.argv[1]
out = sys.argv[2] if len(sys.argv) > 2 else None
data = open(path, 'rb').read()
magic, version, kind, count, names_off = struct.unpack_from('<4sI4sIQ', data)
assert magic == b'BTDX' and kind == b'GNRL', (magic, kind)
names = []
p = names_off
for _ in range(count):
    n = struct.unpack_from('<H', data, p)[0]
    names.append(data[p + 2:p + 2 + n].decode('utf-8', 'replace'))
    p += 2 + n
p = 24
for i in range(count):
    h, ext, dh, flags, off, packed, unpacked, pad = struct.unpack_from('<I4sIIQIII', data, p)
    p += 36
    raw = data[off:off + (packed or unpacked)]
    body = zlib.decompress(raw) if packed else raw
    print(f'{names[i]}  {len(body)} bytes')
    if out:
        target = os.path.join(out, names[i].replace('/', os.sep))
        os.makedirs(os.path.dirname(target), exist_ok=True)
        open(target, 'wb').write(body)
