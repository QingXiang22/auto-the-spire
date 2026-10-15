"""Restore the recorded upstream revision and local reference patch into a new clone."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
BASELINES = ROOT / "reference/baselines"


def git(directory: Path, *args: str) -> str:
    return subprocess.check_output(
        ["git", "-C", str(directory), *args], text=True, encoding="utf-8"
    ).strip()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", help="Optional existing clone for an offline restore")
    args = parser.parse_args()
    lock = json.loads((BASELINES / "sts2-sim.json").read_text(encoding="utf-8"))
    patch = BASELINES / lock["patch"]
    if hashlib.sha256(patch.read_bytes()).hexdigest() != lock["patch_sha256"]:
        raise SystemExit("Reference patch digest mismatch")
    target = ROOT / "reference/sts2-sim"
    if target.exists():
        raise SystemExit(f"Refusing to overwrite an existing reference: {target}")
    subprocess.run(
        ["git", "clone", "--no-checkout", args.source or lock["repository"], str(target)],
        check=True,
    )
    git(target, "config", "core.autocrlf", "false")
    git(target, "checkout", "--detach", lock["commit"])
    git(target, "apply", "--index", str(patch))
    actual = git(target, "write-tree")
    if actual != lock["patched_tree"]:
        raise SystemExit(f"Patched reference tree mismatch: {actual}")
    subprocess.run(["git", "-C", str(target), "diff", "--exit-code"], check=True)
    print(f"Restored {lock['commit']} + local patch; tree={actual}")


if __name__ == "__main__":
    main()
