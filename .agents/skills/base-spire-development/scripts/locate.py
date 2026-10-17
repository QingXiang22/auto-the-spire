"""Find bounded source-catalog entries without loading gameplay implementations."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[4]
CATALOGS = {domain: f"{domain}/catalog.json" for domain in
            ("cards", "powers", "relics", "potions", "enemies", "events", "characters", "encounters", "run")}
CATALOGS["pools"] = "shared/source-pools.json"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("domain", choices=CATALOGS)
    selector = parser.add_mutually_exclusive_group(required=True)
    selector.add_argument("--id", help="Exact stable source ID.")
    selector.add_argument("--search", help="Case-insensitive substring of ID or source class name.")
    parser.add_argument("--limit", type=int, default=5)
    parser.add_argument("--verify-source", action="store_true", help="Compare only the selected local source files with their recorded raw-byte hashes.")
    parser.add_argument("--package", type=Path, default=ROOT / "mod-the-spire/base-spire/package.json")
    args = parser.parse_args()
    if not 1 <= args.limit <= 20 or not (args.id or args.search or "").strip():
        parser.error("provide a nonempty selector and --limit between 1 and 20")
    try:
        manifest_path = args.package.resolve()
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        relative = CATALOGS[args.domain]
        if relative not in manifest["catalogs"]:
            raise ValueError("The selected source catalog is not declared by this package.")
        catalog = manifest_path.parent / relative
        data = json.loads(catalog.read_text(encoding="utf-8-sig"))
        if data["format"] != "engine-dsl/source-catalog@1":
            raise ValueError("Expected a static source catalog, not an executable registry.")
        matches = [row for row in data["entries"] if row["id"] == args.id] if args.id else [
            row for row in data["entries"] if args.search.casefold() in (row["id"] + " " + row["name"]).casefold()]
        entries = []
        source_ok = True
        for row in matches[:args.limit]:
            entry = {key: row[key] for key in ("id", "name", "definition", "source_path", "source_sha256", "eligibility", "evidence", "pools")}
            source = (ROOT / row["source_path"]).resolve()
            if not source.is_relative_to(ROOT):
                raise ValueError("Source catalog path escapes the repository.")
            entry["source_available"] = source.is_file()
            entry["definition_status"] = "unmigrated" if row["definition"] is None else "declared-dsl-not-behavior-proof"
            if args.verify_source:
                status = "missing" if not source.is_file() else (
                    "matches" if hashlib.sha256(source.read_bytes()).hexdigest() == row["source_sha256"] else "changed")
                entry["source_hash_status"] = status
                source_ok &= status == "matches"
            entries.append(entry)
        report = {"package": manifest["id"], "version": manifest["version"], "supported_scopes": manifest["supported_scopes"],
                  "catalog": str(catalog), "source_baseline": data["source_baseline"],
                  "match_count": len(matches), "returned": len(entries), "truncated": len(entries) < len(matches),
                  "entries": entries, "note": "Catalog lookup is not package validation, gameplay execution or independent verification."}
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 1 if not matches else (0 if source_ok else 3)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"catalog lookup failed: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
