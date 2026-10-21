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
CATALOGS["pools"] = None


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
        source_fixture = ROOT / "tools/base-spire-dev/fixtures/source-catalogs.json"
        frozen = json.loads(source_fixture.read_text(encoding="utf-8-sig"))
        if frozen["format"] != "base-spire/source-catalogs@1":
            raise ValueError("Expected frozen tool source facts.")
        data = frozen["domains"][args.domain]
        definitions = {}
        catalog = None
        if relative in manifest["catalogs"]:
            catalog = manifest_path.parent / relative
            runtime = json.loads(catalog.read_text(encoding="utf-8-sig"))
            if runtime["format"] != "engine-dsl/content-catalog@1":
                raise ValueError("Expected the current runtime content catalog.")
            definitions = {row["id"]: row["definition"] for row in runtime["entries"]}
        matches = [row for row in data["entries"] if row["id"] == args.id] if args.id else [
            row for row in data["entries"] if args.search.casefold() in (row["id"] + " " + row["name"]).casefold()]
        entries = []
        source_ok = True
        for row in matches[:args.limit]:
            entry = {key: row[key] for key in ("id", "name", "source_path", "source_sha256", "pools")}
            entry["exclusion"] = row.get("exclusion")
            entry["definition"] = definitions.get(row["id"])
            source = (ROOT / row["source_path"]).resolve()
            if not source.is_relative_to(ROOT):
                raise ValueError("Source catalog path escapes the repository.")
            entry["source_available"] = source.is_file()
            entry["definition_status"] = "no-loaded-definition" if entry["definition"] is None else "declared-dsl-not-behavior-proof"
            if args.verify_source:
                status = "missing" if not source.is_file() else (
                    "matches" if hashlib.sha256(source.read_bytes()).hexdigest() == row["source_sha256"] else "changed")
                entry["source_hash_status"] = status
                source_ok &= status == "matches"
            entries.append(entry)
        report = {"package": manifest["id"], "version": manifest["version"], "supported_scopes": manifest["supported_scopes"],
                  "source_fixture": str(source_fixture), "content_catalog": str(catalog) if catalog else None,
                  "source_baseline": data["source_baseline"],
                  "match_count": len(matches), "returned": len(entries), "truncated": len(entries) < len(matches),
                  "entries": entries, "note": "Catalog lookup is not package validation, gameplay execution or independent verification."}
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 1 if not matches else (0 if source_ok else 3)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"catalog lookup failed: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
