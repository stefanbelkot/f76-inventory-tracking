#!/usr/bin/env python3
"""Decode InventoryTracking export files into one pretty JSON document.

Handles:
  * "zlib+base64" files (the compact single-file format) - decoded and inflated,
  * plain part files (rows stored directly), including array rows with a "cols" header,
  * multi-part exports - parts of the same storage + snapshot are merged in order.

Usage (run it in the folder that holds the export files, e.g. Data/modsdata):
  python decode_export.py                     reads ./itemdump_*.json, writes ./decoded/<character>_<storage>_<snapshot>.json
  python decode_export.py FILE_OR_DIR ...     read these instead  [-o OUT.json] [--outdir DIR]

In a directory only files named itemdump_*.json are read; files given explicitly are always read.
Before writing, the output folder's old *.json files are removed (only when something was decoded).

Each (account, character, storage, snapshot) group becomes one output document:
  { "storage", "title", "snapshot", "rows", "complete", "items": [ {...}, ... ] }
"""
import argparse
import base64
import json
import sys
import zlib
from collections import defaultdict
from pathlib import Path


def parse_effects(desc):
    """Split a card DESC text into effect lines.

    The game ends each legendary effect with one marker per star ("¬", "¬¬", ...);
    "§" and unmarked trailing lines are kept with star=None.
    """
    effects = []
    for line in desc.replace("\r", "\n").split("\n"):
        text = line.strip()
        if not text:
            continue
        stripped = text.rstrip("¬§ ")
        marker = text[len(stripped):].strip()
        star = marker.count("¬") or None
        effects.append({"star": star, "text": stripped, "marker": marker or None})
    return effects


def load_rows(doc):
    """Return the list of item objects stored in one export file."""
    encoding = doc.get("encoding")
    if encoding == "zlib+base64":
        raw = zlib.decompress(base64.b64decode(doc["data"]))
        expected = doc.get("rawBytes")
        if expected is not None and expected != len(raw):
            raise ValueError(f"rawBytes mismatch: header says {expected}, got {len(raw)}")
        rows = json.loads(raw.decode("utf-8"))
    elif encoding in (None, "plain"):
        rows = doc["data"]
    else:
        raise ValueError(f"unknown encoding {encoding!r}")
    cols = doc.get("cols")
    if cols:
        rows = [dict(zip(cols, row)) if isinstance(row, list) else row for row in rows]
    for row in rows:
        if isinstance(row, dict) and isinstance(row.get("desc"), str):
            row["effects"] = parse_effects(row["desc"])
    return rows


PREFIX = "itemdump_"  # exports are named itemdump_<account>_<character>_<storage>_<part>.json


def collect_files(inputs):
    files = []
    for item in inputs:
        path = Path(item)
        if path.is_dir():
            files += sorted(p for p in path.glob("*.json") if p.name.startswith(PREFIX))
        else:
            files.append(path)
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("inputs", nargs="*", help="export files or directories (default: the current directory)")
    parser.add_argument("-o", "--output", help="write a single group to this file (error if several groups)")
    parser.add_argument("--outdir", default="decoded", help="where to write the decoded files (default: ./decoded)")
    args = parser.parse_args()

    groups = defaultdict(dict)  # (account, character, storage, snapshot) -> {part: (doc, rows, filename)}
    for path in collect_files(args.inputs or ["."]):
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
            if not isinstance(doc, dict) or "data" not in doc:
                continue
            rows = load_rows(doc)
        except Exception as exc:  # report and continue with the other files
            print(f"skip {path.name}: {exc}", file=sys.stderr)
            continue
        key = (doc.get("account", ""), doc.get("character", ""), doc.get("storage", doc.get("source", "unknown")), doc.get("snapshot", doc.get("snap", "?")))
        groups[key][doc.get("part", 1)] = (doc, rows, path.name)

    if not groups:
        print("no decodable export files found", file=sys.stderr)
        return 1
    if args.output and len(groups) > 1:
        print(f"{len(groups)} groups found; use --outdir instead of -o", file=sys.stderr)
        return 1

    if not args.output:
        # Start from a clean output folder: remove only the .json files left by earlier runs.
        outdir = Path(args.outdir)
        if outdir.is_dir():
            old = [p for p in outdir.glob("*.json") if p.is_file()]
            for p in old:
                p.unlink()
            if old:
                print(f"cleared {len(old)} old file(s) in {outdir}")

    for (account, character, storage, snapshot), parts in sorted(groups.items()):
        first = parts[min(parts)][0]
        total_parts = first.get("parts", len(parts))
        missing = [n for n in range(1, total_parts + 1) if n not in parts]
        items = []
        for number in sorted(parts):
            items += parts[number][1]
        result = {
            "account": account,
            "character": character,
            "storage": storage,
            "title": first.get("title"),
            "storageMode": first.get("storageMode"),
            "snapshot": snapshot,
            "rows": len(items),
            "expectedRows": first.get("rows"),
            "partsFound": sorted(parts),
            "partsExpected": total_parts,
            "complete": not missing and len(items) == first.get("rows", len(items)),
            "items": items,
        }
        # Player exports carry the character's currencies: [{"type", "amount", "max"}] (see final-spec.md).
        currencies = next((doc.get("currencies") for _, (doc, _rows, _name) in sorted(parts.items()) if doc.get("currencies")), None)
        if currencies:
            result["currencies"] = currencies
        if missing:
            print(f"WARNING {storage}/{snapshot}: missing parts {missing}", file=sys.stderr)
        text = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
        if args.output:
            Path(args.output).write_text(text, encoding="utf-8")
            print(f"wrote {args.output} ({len(items)} items)")
        else:
            outdir = Path(args.outdir)
            outdir.mkdir(parents=True, exist_ok=True)
            who = "".join(ch for ch in character.lower() if ch.isalnum()) or "unknown"
            target = outdir / f"{who}_{storage}_{snapshot}.json"
            target.write_text(text, encoding="utf-8")
            print(f"wrote {target} ({len(items)} items, complete={result['complete']})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
