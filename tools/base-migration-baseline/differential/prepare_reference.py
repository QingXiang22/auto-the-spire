"""Fetch the pinned upstream reference into an ignored directory, without local patches."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
LOCK = ROOT / "reference/baselines/sts2-sim.json"


def git(directory: Path, *args: str) -> str:
    return subprocess.check_output(
        ["git", "-C", str(directory), *args], text=True, encoding="utf-8"
    ).strip()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", help="Optional existing clone for an offline fetch")
    args = parser.parse_args()
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    target = ROOT / "reference/sts2-sim"
    if not target.exists():
        target.mkdir(parents=True)
        git(target, "init")
        git(target, "config", "core.autocrlf", "false")
        git(target, "remote", "add", "origin", args.source or lock["repository"])
        git(target, "fetch", "--depth", "1", "origin", lock["commit"])
        git(target, "checkout", "--detach", "FETCH_HEAD")
    # Never reset, patch, or update an existing checkout. A mismatch needs an explicit review.
    top = Path(git(target, "rev-parse", "--show-toplevel")).resolve()
    if top != target.resolve():
        raise SystemExit(f"Not an independent reference checkout: {target}")
    head = git(target, "rev-parse", "HEAD")
    tree = git(target, "rev-parse", "HEAD^{tree}")
    dirty = git(target, "status", "--porcelain", "--untracked-files=all")
    if head != lock["commit"] or tree != lock["tree"] or dirty:
        raise SystemExit(f"Reference mismatch or local changes; refusing to overwrite: {target}")
    print(f"Verified upstream reference {head}; tree={tree}; no local patches")


if __name__ == "__main__":
    main()
