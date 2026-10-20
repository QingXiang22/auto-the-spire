"""Check migration boundaries and explicit unsupported comparison; this is not parity evidence."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / "tools/base-migration-baseline"


def source_hash(path: Path) -> str:
    return hashlib.sha256(path.read_text(encoding="utf-8").encode("utf-8")).hexdigest()


def check_projects() -> int:
    solution = (ROOT / "AutoTheSpire.sln").read_text(encoding="utf-8-sig")
    projects = [ROOT / p.replace("\\", "/") for p in re.findall(r'"([^"\n]+\.csproj)"', solution)]
    seen: set[Path] = set()
    while projects:
        project = projects.pop().resolve()
        if project in seen:
            continue
        seen.add(project)
        if BASELINE in project.parents or not project.is_file():
            raise ValueError(f"invalid active project dependency: {project}")
        for node in ET.fromstring(project.read_text(encoding="utf-8-sig")).iter():
            for name, value in node.attrib.items():
                if name in ("Include", "Update") and ("base-migration-baseline" in value or "BaseSpire.csproj" in value):
                    raise ValueError(f"retired rule dependency: {project}:{value}")
            if node.tag == "ProjectReference":
                projects.append(project.parent / node.attrib["Include"].replace("\\", "/"))
            if node.tag == "HintPath" and "base-migration-baseline" in (node.text or ""):
                raise ValueError(f"retired assembly reference: {project}")
    return len(seen)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", choices=["Debug", "Release"], default="Release")
    parser.add_argument("--output", type=Path, default=ROOT / ".artifacts/ci/migration")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    try:
        projects = check_projects()
        record = json.loads((ROOT / "tools/ci/fixtures/legacy-dsl-checks.json").read_text(encoding="utf-8"))
        if record.get("hash_encoding") != "utf8-lf":
            raise ValueError("retired-source hashes must declare canonical text encoding")
        actual = set()
        for folder in ("BaseSpire.Slice.Tests", "net-tests", "differential"):
            actual.update(p.relative_to(ROOT).as_posix() for p in (BASELINE / folder).rglob("*.cs") if "bin" not in p.parts and "obj" not in p.parts)
        recorded = [row["source"] for row in record["groups"]]
        if len(recorded) != len(set(recorded)) or set(recorded) != actual:
            raise ValueError("missing or duplicate retired source-group disposition")
        for row in record["groups"]:
            if source_hash(ROOT / row["source"]) != row["sha256"] or row["executed_in_current_gate"] is not False:
                raise ValueError(f"stale or misleading retired-group evidence: {row['source']}")
        for row in record["suites"]:
            active = ROOT / row["active_definition"]
            if source_hash(active) != row["sha256"]:
                raise ValueError(f"changed original suite denominator: {active}")
        command = ["dotnet", "run", "--no-build", "--project", str(ROOT / "tools/differential"), "-c", args.configuration,
                   "--", "test", "--suite", "regression", "--output", str(args.output / "regression")]
        process = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
        (args.output / "differential.log").write_text(process.stdout + process.stderr, encoding="utf-8")
        if process.returncode != 3:
            raise ValueError(f"unmigrated original suite must report exit 3, got {process.returncode}")
        report = json.loads((args.output / "regression/result.json").read_text(encoding="utf-8"))
        expected = json.loads((ROOT / "tools/differential/suites/regression.json").read_text(encoding="utf-8"))["expected_case_ids"]
        if (report["expected_case_ids"] != expected or report["executed"] != 0 or report["independent_verified"] is not False
                or [r["id"] for r in report["cases"]] != expected or any(r["status"] != "unsupported" for r in report["cases"])):
            raise ValueError("unsupported report lost case identities or claims execution")
        result = {"gate": "migration-contract", "active_projects": projects, "retired_source_groups": len(recorded),
                  "original_suites": len(record["suites"]), "original_regression_exit": process.returncode,
                  "original_regression_executed": 0, "independent_verified": False}
        (args.output / "migration-contract.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(result))
        return 0
    except (OSError, ValueError, KeyError, ET.ParseError) as error:
        print(f"migration contract failure: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
