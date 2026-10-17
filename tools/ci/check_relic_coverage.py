"""Check actual runtime relic evidence against the committed source and executable catalogs."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def check(report: dict, source: list[dict], definitions: list[dict], require_complete: bool = False) -> list[str]:
    errors = []
    if report.get("schema") != "base-spire/relic-coverage@1" or report.get("scope") != "controlled-formal-source-behavior":
        return ["missing or wrong runtime relic report schema/scope"]
    rows = report.get("relics", [])
    source_by_id = {row["ContentId"]: row for row in source}
    executable = {row["ContentId"] for row in definitions}
    ids = [row.get("ContentId") for row in rows]
    if len(source) != 297 or len(source_by_id) != 297 or len(rows) != 297 or set(ids) != set(source_by_id):
        errors.append("source relic denominator, duplicates or identities differ from the committed 297-item snapshot")
    if len(executable) != len(definitions):
        errors.append("duplicate executable relic definitions")
    checked, pending = 0, 0
    for row in rows:
        identity = row.get("ContentId")
        original = source_by_id.get(identity)
        if original is None:
            continue
        for field in ("Name", "Rarity", "Pools", "Eligibility", "EligibilityEvidence", "SourcePath", "SourceSha256", "Hooks"):
            if row.get(field) != original[field]:
                errors.append(f"{identity}: stale or altered source field {field}")
        declared = identity in executable
        if row.get("declared") is not declared:
            errors.append(f"{identity}: executable membership mismatch")
        cases = row.get("passed_scenarios")
        if not isinstance(cases, list) or any(not isinstance(case, str) or not case.strip() for case in cases):
            errors.append(f"{identity}: invalid passed scenario evidence")
            cases = []
        if declared:
            if original["Eligibility"] != "singleplayer" or not original["EligibilityEvidence"]:
                errors.append(f"{identity}: executable relic has no accepted single-player qualification")
            if row.get("behavior_checked") is not True or not cases or row.get("status") != "behavior-checked":
                errors.append(f"{identity}: executable relic lacks an actually passed runtime scenario")
            else:
                checked += 1
        elif original["Eligibility"].startswith("excluded-"):
            if not original["EligibilityEvidence"] or row.get("status") != original["Eligibility"]:
                errors.append(f"{identity}: exclusion lacks source evidence")
        else:
            pending += 1
            if row.get("status") != "unimplemented" or row.get("behavior_checked") is not False or cases:
                errors.append(f"{identity}: unimplemented content must not claim behavior coverage")
    for key, actual in (("source_count", len(rows)), ("declared_count", len(executable)),
                        ("behavior_checked_count", checked), ("unimplemented_count", pending)):
        if report.get(key) != actual:
            errors.append(f"{key}: summary does not match actual rows ({actual})")
    complete = pending == 0 and checked == len(executable)
    if report.get("complete") is not complete:
        errors.append("complete flag contradicts coverage rows")
    if require_complete and not complete:
        errors.append(f"full relic completion gate: {pending} unimplemented source relics remain")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    parser.add_argument("--require-complete", action="store_true")
    args = parser.parse_args()
    content = ROOT / "mod-the-spire/base-spire/BaseSpire/ContentSource"
    try:
        data = json.loads(args.report.read_text(encoding="utf-8-sig"))
        errors = check(data, json.loads((content / "relic-source-catalog.json").read_text(encoding="utf-8")),
                       json.loads((content / "relics.json").read_text(encoding="utf-8")), args.require_complete)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"relic coverage input error: {error}")
        return 1
    for error in errors:
        print(error)
    if not errors:
        print(f"relic runtime coverage: {data['behavior_checked_count']}/{data['declared_count']} executable; "
              f"source={data['source_count']}, unimplemented={data['unimplemented_count']}, complete={data['complete']}")
    return int(bool(errors))


if __name__ == "__main__":
    raise SystemExit(main())
