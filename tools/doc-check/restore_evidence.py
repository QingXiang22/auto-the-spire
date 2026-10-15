"""Verify and restore the historical evidence linked by the committed documentation."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import zipfile


ROOT = Path(__file__).resolve().parents[2]
BUNDLE = ROOT / "docs/review/evidence/linked-evidence-20261015.json"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify without writing files")
    args = parser.parse_args()
    manifest = json.loads(BUNDLE.read_text(encoding="utf-8"))
    archive = BUNDLE.parent / manifest["archive"]
    if hashlib.sha256(archive.read_bytes()).hexdigest() != manifest["sha256"]:
        raise SystemExit("Evidence archive digest mismatch")
    entries = {entry["path"]: entry for entry in manifest["files"]}
    directories = manifest["directories"]
    expected = set(entries) | {name + "/" for name in directories}

    def destination(name: str) -> Path:
        if not name.startswith((".audit/", ".artifacts/", "artifacts/",
                                "tools/orimod-decompile/verify/")):
            raise ValueError(f"Unexpected evidence path: {name}")
        target = (ROOT / name).resolve()
        if not target.is_relative_to(ROOT):
            raise ValueError(f"Evidence path escapes repository: {name}")
        return target

    with zipfile.ZipFile(archive) as bundle:
        if len(bundle.namelist()) != len(expected) or set(bundle.namelist()) != expected:
            raise SystemExit("Evidence archive member list mismatch")
        # Validate the entire archive and every existing destination before writing anything.
        for name in directories:
            target = destination(name + "/")
            if target.exists() and not target.is_dir():
                raise SystemExit(f"Evidence directory conflicts with a file: {target}")
        for name, entry in entries.items():
            target = destination(name)
            data = bundle.read(name)
            if len(data) != entry["size"] or hashlib.sha256(data).hexdigest() != entry["sha256"]:
                raise SystemExit(f"Evidence member digest mismatch: {name}")
            if target.exists() and (not target.is_file() or target.read_bytes() != data):
                raise SystemExit(f"Refusing to overwrite different local evidence: {target}")
        if not args.check:
            for name in directories:
                destination(name + "/").mkdir(parents=True, exist_ok=True)
            for name in entries:
                target = destination(name)
                if not target.exists():
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(bundle.read(name))
    print(f"{'Verified' if args.check else 'Restored'} {len(entries)} historical evidence files")


if __name__ == "__main__":
    main()
