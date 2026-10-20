"""Run the pinned Vale release on every versioned or proposed Markdown document."""
from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import subprocess
import sys

from check_docs import project_files

ROOT = Path(__file__).resolve().parents[2]
VALE_VERSION = "3.24.0"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vale", default="vale", help="Vale executable or command on PATH")
    args = parser.parse_args()
    executable = shutil.which(args.vale)
    if not executable:
        print("Vale is required. See tools/doc-check/README.md for installation.", file=sys.stderr)
        return 2
    try:
        version = subprocess.check_output([executable, "--version"], text=True).strip()
        if version.split()[-1].removeprefix("v") != VALE_VERSION:
            print(f"Expected Vale {VALE_VERSION}; got {version}", file=sys.stderr)
            return 2
        paths = sorted(p.relative_to(ROOT).as_posix() for p in project_files(ROOT)
                       if p.suffix == ".md" and p.is_file() and p.name != "AGENTS.override.md")
        if not paths:
            print("No Markdown documents found.", file=sys.stderr)
            return 2
        print(f"{version}; checking {len(paths)} Markdown files", flush=True)
        # All documents are checked, including the migration reference READMEs.
        # Batches keep Windows command lines below CreateProcess's size limit.
        failed = False
        for start in range(0, len(paths), 40):
            result = subprocess.run([executable, "--config=.vale.ini",
                                     *paths[start:start + 40]], cwd=ROOT)
            if result.returncode:
                failed = True
        return 1 if failed else 0
    except (OSError, subprocess.CalledProcessError) as error:
        print(f"Unable to run Vale: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
