"""Validate current DSL relic witnesses; old C# coverage reports are not accepted."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "mod-the-spire/base-spire"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    parser.add_argument("--require-complete", action="store_true")
    args = parser.parse_args()
    try:
        report = json.loads(args.report.read_text(encoding="utf-8-sig"))
        if report.get("schema") != "engine-dsl/package-behavior@1":
            raise ValueError("expected actual pure DSL behavior evidence; legacy C# reports are unsupported")
        files = sorted(p for p in BASE.rglob("*") if p.is_file())
        material = "\n".join(p.relative_to(BASE).as_posix() + "\n" + p.read_text(encoding="utf-8-sig").replace("\r\n", "\n") for p in files)
        if report.get("package_digest") != hashlib.sha256(material.encode()).hexdigest():
            raise ValueError("behavior report does not belong to the current package")
        rows = json.loads((BASE / "relics/catalog.json").read_text(encoding="utf-8"))["entries"]
        if len(rows) != 297 or len({r["id"] for r in rows}) != 297:
            raise ValueError("source relic denominator must remain 297")
        declared = {r["id"] for r in rows if r["definition"] is not None}
        witnesses = {r["id"]: r["passed_scenarios"] for r in report.get("content", [])}
        if len(witnesses) != len(report.get("content", [])) or declared != set(witnesses):
            raise ValueError("declared DSL definitions and actually checked content differ")
        passed = set(report.get("scenarios", []))
        if not passed or any(not cases or not set(cases) <= passed for cases in witnesses.values()):
            raise ValueError("missing passed behavior scenario evidence")
        pending = sum(r["definition"] is None and not r["eligibility"].startswith("excluded-") for r in rows)
        print(f"DSL relic coverage: {len(declared)} behavior-checked; source=297; unimplemented={pending}; independent_verified=false")
        if args.require_complete and pending:
            raise ValueError("full relic gate remains unmet")
        return 0
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"relic coverage error: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
