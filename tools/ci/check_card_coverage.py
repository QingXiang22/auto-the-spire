"""Check current DSL card witnesses against the frozen full Ironclad source roster."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from source_catalogs import load_sources

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "mod-the-spire/base-spire"


def validate(report_path: Path, require_ironclad_complete: bool) -> None:
    report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    if report.get("schema") != "engine-dsl/package-behavior@1":
        raise ValueError("expected actual pure DSL behavior evidence")
    files = sorted(p for p in BASE.rglob("*") if p.is_file())
    material = "\n".join(p.relative_to(BASE).as_posix() + "\n" + p.read_text(encoding="utf-8-sig").replace("\r\n", "\n") for p in files)
    if report.get("package_digest") != hashlib.sha256(material.encode()).hexdigest():
        raise ValueError("behavior report does not belong to the current package")
    rows = json.loads((BASE / "cards/catalog.json").read_text(encoding="utf-8"))["entries"]
    source = load_sources()["cards"]["entries"]
    declared = {r["id"] for r in rows}
    if len(declared) != len(rows) or not declared <= {r["id"] for r in source if "exclusion" not in r}:
        raise ValueError("runtime card definitions differ from applicable source identities")
    fixture = json.loads((ROOT / "tools/base-spire-dev/fixtures/ironclad-source-metadata.json").read_text(encoding="utf-8"))["entries"]
    frozen = {r["id"]: r for r in fixture if r["name"] != "GiantRock"}
    if len(frozen) != 90 or sum(r["multiplayer_only"] for r in frozen.values()) != 5:
        raise ValueError("frozen source pool must contain 90 unique rows and five multiplayer exclusions")
    metadata = json.loads((ROOT / "tools/base-spire-dev/fixtures/reference-pool-metadata.json").read_text(encoding="utf-8"))
    lock = json.loads((ROOT / "reference/baselines/sts2-sim.json").read_text(encoding="utf-8"))
    if metadata["reference_lock"] != lock:
        raise ValueError("native source pool must match current reference lock")
    if [(r["id"], r["rarity"], r["multiplayer_only"]) for r in frozen.values()] != [(r["id"], r["rarity"], r["multiplayer_only"]) for r in metadata["cards"]]:
        raise ValueError("frozen metadata differs from native complete source pool")
    legacy_applicable = {r["id"] for r in source if "IroncladCardPool" in r["pools"] and "exclusion" not in r}
    current_applicable = {key for key, row in frozen.items() if not row["multiplayer_only"]}
    if legacy_applicable != current_applicable:
        raise ValueError("original required singleplayer card coverage cannot be reduced")
    for content_id, expected in frozen.items():
        if expected["multiplayer_only"] and content_id in declared:
            raise ValueError(f"singleplayer exclusion must be explicit: {content_id}")
    witnesses = {r["id"]: r["passed_scenarios"] for r in report.get("cards", [])}
    if len(witnesses) != len(report.get("cards", [])) or declared != set(witnesses):
        raise ValueError("DSL card definitions and actually checked card witnesses differ")
    passed = set(report.get("scenarios", []))
    if not passed or any(not cases or not set(cases) <= passed for cases in witnesses.values()):
        raise ValueError("missing passed card behavior evidence")
    applicable = {key for key, row in frozen.items() if not row["multiplayer_only"]}
    pending = applicable - declared
    print(f"DSL cards: {len(declared)} behavior-checked; source=578; Ironclad={len(applicable - pending)}/85; multiplayer-excluded=5; independent_verified=false")
    if require_ironclad_complete and pending:
        raise ValueError("Ironclad gate unmet: " + ", ".join(sorted(pending)))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    parser.add_argument("--require-ironclad-complete", action="store_true")
    args = parser.parse_args()
    try:
        validate(args.report, args.require_ironclad_complete)
        return 0
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"card coverage error: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
