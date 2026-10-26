#!/usr/bin/env python3
"""Cross-check the two independent requirement -> scenario traceability tables.

Table A: AI  spec section 5  (需求 ID | 可验证要求 | 场景)
Table B: net spec section 2  (需求 ID | 触发与可观察要求 | 对应验收)
Table C: ai-net-verification section 4 (需求 | 设计落点 | 场景)

Reports every disagreement between the scenario sets of A/B and C.
Read-only; this is a review aid, not part of the build.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROW_RE = re.compile(r"^\|([^|]+)\|([^|]*)\|([^|]*)\|")
SCENARIO_RE = re.compile(r"\bV-\d+\b")
REQ_RE = re.compile(r"\b(AI-\d+|NET-\d+)\b")


def rows(path: Path, min_cols: int = 3):
    for line in path.read_text(encoding="utf-8").splitlines():
        m = ROW_RE.match(line.strip())
        if not m:
            continue
        cells = [c.strip() for c in m.groups()]
        if len(cells) < min_cols:
            continue
        yield cells


def collect(path: Path, id_col: int, scenario_cols: list[int]) -> dict[str, set[str]]:
    found: dict[str, set[str]] = {}
    for cells in rows(path, max(scenario_cols) + 1):
        ids = REQ_RE.findall(cells[id_col])
        if not ids:
            continue
        scenarios: set[str] = set()
        for col in scenario_cols:
            scenarios |= set(SCENARIO_RE.findall(cells[col]))
        for req in ids:
            found.setdefault(req, set()).update(scenarios)
    return found


def main() -> int:
    root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    ai = collect(root / "docs/areas/ai/spec/overview.md", 0, [2])
    net = collect(root / "docs/areas/net/spec/overview.md", 0, [2])
    spec = {**ai, **net}
    matrix = collect(root / "docs/specs/ai-net/verification.md", 0, [2])

    problems = 0
    for req in sorted(spec, key=lambda r: (r[:2], int(r[3:]))):
        spec_set = spec.get(req, set())
        matrix_set = matrix.get(req, set())
        if spec_set != matrix_set:
            problems += 1
            only_spec = sorted(spec_set - matrix_set)
            only_matrix = sorted(matrix_set - spec_set)
            print(f"MISMATCH {req}: spec-only={only_spec or '-'} matrix-only={only_matrix or '-'}")
        else:
            print(f"ok       {req}: {sorted(spec_set)}")
    print(f"\n{problems} mismatching requirement row(s) of {len(spec)}")
    return min(problems, 255)


if __name__ == "__main__":
    sys.exit(main())
