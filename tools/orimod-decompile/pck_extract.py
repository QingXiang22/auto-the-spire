"""Minimal Godot 4.5 .pck reader, used to pull localization text out of OriMod.pck.

The mod ships its strings inside the pack (the C# only registers table names via
`CustomLocTableManager.Register`), so the Chinese display names of cards/powers/relics
are not visible in the decompiled source.

Empirically derived layout for pack format 3 (Godot 4.5.1), which differs from the
format-2 layout documented in older write-ups:

    u32  magic            'GDPC' (0x43504447)
    u32  pack_format      3
    u32  ver_major/minor/patch
    u32  pack_flags       bit1 = PACK_REL_FILEBASE
    u64  file_base
    u64  dir_offset       <- index lives at the END of the pack in format 3
    ...  zero padding up to file_base
    data section
    index at dir_offset:
        u32  file_count
        per file: u32 path_len, path bytes, u64 offset, u64 size, u8[16] md5, u32 flags

Index paths are pack-relative (`res://` implied). `offset` is **relative to `file_base`**,
not to the start of the pack: entry 0 of OriMod.pck is `OriMod.json` with offset 0 and its
bytes really do sit at `file_base` (= 112, right after the header).

Usage:
    python pck_extract.py list <pack> [--grep SUBSTR]
    python pck_extract.py dump <pack> <outdir> [--grep SUBSTR]
"""

from __future__ import annotations

import argparse
import os
import struct
import sys

MAGIC = 0x43504447  # 'GDPC' read little-endian


def _pad4(n: int) -> int:
    return (n + 3) & ~3


def read_index(path: str):
    size = os.path.getsize(path)
    with open(path, "rb") as fh:
        head = fh.read(40)
        magic, fmt, vmaj, vmin, vpatch, flags = struct.unpack("<IIIIII", head[:24])
        if magic != MAGIC:
            raise SystemExit(f"not a Godot pack (magic={magic:#x})")
        (file_base,) = struct.unpack("<Q", head[24:32])
        (dir_offset,) = struct.unpack("<Q", head[32:40])

        if not (0 < dir_offset <= size):
            raise SystemExit(f"implausible directory offset {dir_offset} (file size {size})")

        fh.seek(dir_offset)
        (file_count,) = struct.unpack("<I", fh.read(4))
        entries = []
        for _ in range(file_count):
            (raw_len,) = struct.unpack("<I", fh.read(4))
            raw = fh.read(_pad4(raw_len))
            name = raw.split(b"\x00", 1)[0].decode("utf-8", "replace")
            offset, fsize = struct.unpack("<QQ", fh.read(16))
            fh.read(16)  # md5
            (eflags,) = struct.unpack("<I", fh.read(4))
            entries.append((name, offset + file_base, fsize, eflags))
        return fmt, (vmaj, vmin, vpatch), flags, file_base, file_count, entries


def extract(pack: str, entry, outdir: str) -> str | None:
    name, offset, fsize, eflags = entry
    if eflags & 1:
        return None  # encrypted entry
    rel = name.replace("res://", "").replace("user://", "").lstrip("/")
    dest = os.path.join(outdir, rel.replace("/", os.sep))
    os.makedirs(os.path.dirname(dest) or outdir, exist_ok=True)
    with open(pack, "rb") as fh:
        fh.seek(offset)
        data = fh.read(fsize)
    with open(dest, "wb") as out:
        out.write(data)
    return dest


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["list", "dump"])
    ap.add_argument("pack")
    ap.add_argument("outdir", nargs="?")
    ap.add_argument("--grep", default=None)
    args = ap.parse_args()

    fmt, ver, flags, file_base, count, entries = read_index(args.pack)
    print(f"pack_format={fmt} godot={ver[0]}.{ver[1]}.{ver[2]} flags={flags} "
          f"file_base={file_base} files={count}", file=sys.stderr)

    selected = [e for e in entries if args.grep is None or args.grep.lower() in e[0].lower()]

    if args.mode == "list":
        for name, offset, fsize, eflags in selected:
            print(f"{fsize:>12}  {'ENC' if eflags & 1 else '   '}  {name}")
        print(f"--- {len(selected)} / {count} entries shown", file=sys.stderr)
        return 0

    if not args.outdir:
        raise SystemExit("dump requires an output directory")
    written = skipped = 0
    for entry in selected:
        if extract(args.pack, entry, args.outdir) is None:
            skipped += 1
        else:
            written += 1
    print(f"wrote {written} files ({skipped} encrypted skipped) to {args.outdir}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
