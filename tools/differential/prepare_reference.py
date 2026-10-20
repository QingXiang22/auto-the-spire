"""Prepare and verify the clean pinned upstream reference; existing checkouts are read-only."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]


def git(directory: Path, *args: str) -> str:
    # Public reference fetches are anonymous, with no stored credential or interactive prompt.
    return subprocess.check_output(
        ["git", "-c", "credential.helper=", "-c", "core.askPass=", "-c", "http.extraHeader=",
         "-C", str(directory), *args], text=True, encoding="utf-8",
        env=dict(os.environ, GIT_TERMINAL_PROMPT="0"),
    ).strip()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", help="Existing upstream clone for offline initialization")
    args = parser.parse_args()
    lock = json.loads((ROOT / "reference/baselines/sts2-sim.json").read_text(encoding="utf-8"))
    target = ROOT / "reference/sts2-sim"
    if not target.exists():
        target.mkdir(parents=True)
        git(target, "init")
        git(target, "config", "core.autocrlf", "false")
        git(target, "remote", "add", "origin", args.source or lock["repository"])
        git(target, "fetch", "--depth", "1", "origin", lock["commit"])
        git(target, "checkout", "--detach", "FETCH_HEAD")
    if (Path(git(target, "rev-parse", "--show-toplevel")).resolve() != target.resolve()
            or git(target, "rev-parse", "HEAD") != lock["commit"]
            or git(target, "rev-parse", "HEAD^{tree}") != lock["tree"]
            or git(target, "status", "--porcelain", "--untracked-files=all")):
        raise SystemExit("Reference mismatch or local changes; refusing to overwrite the checkout.")
    print(f"Verified clean upstream reference {lock['commit']}; tree={lock['tree']}")


if __name__ == "__main__":
    main()
