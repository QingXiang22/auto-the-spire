"""Run every sts2-core check in order and print one summary.

    python tools/sts2-core-extract/run_all.py

Order matters: structural checks first (cheap, catch corruption), then rule-fidelity checks
(catch silent over-deletion), then inventory.

Exit code is non-zero if any check fails.
"""

from __future__ import annotations

import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
PY = sys.executable

CHECKS = [
    ("delimiters + presentation residue", "verify.py", []),
    ("gameplay API fidelity", "fidelity.py", []),
    ("member survival", "member_diff.py", []),
    ("no dangling locals", "audit_dangling_locals.py", []),
    ("no lost gameplay lines", "audit_lost_lines.py", ["--gameplay-only"]),
    ("every removed line justified", "audit_deletions.py", []),
]


def run(script: str, extra: list[str]) -> tuple[int, str]:
    path = os.path.join(HERE, script)
    proc = subprocess.run(
        [PY, path, *extra],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        cwd=REPO,
    )
    return proc.returncode, (proc.stdout or "") + (proc.stderr or "")


def main() -> int:
    failures = 0
    for label, script, extra in CHECKS:
        code, out = run(script, extra)
        first = "\n".join(out.strip().splitlines()[:6])
        status = "PASS" if code == 0 else "FAIL"
        if code != 0:
            failures += 1
        print(f"[{status}] {label}")
        for line in first.splitlines():
            print(f"        {line}")
        print()

    code, out = run("inventory.py", [])
    print("[INFO] inventory")
    for line in out.strip().splitlines():
        print(f"        {line}")

    print()
    print(f"{len(CHECKS) - failures}/{len(CHECKS)} checks passed")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
