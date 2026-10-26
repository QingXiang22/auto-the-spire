"""Select affected checks; reuse a successful PR only for an identical tested tree."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import urllib.error
import urllib.request


SUITES = ["core", "base", "net", "migration", "docs"]


def affected(paths: list[str]) -> list[str]:
    selected = {"docs"}
    for path in paths:
        if path.endswith(".md") or path.startswith(("tools/doc-check/", ".github/styles/")) or path == ".vale.ini":
            continue
        if path.startswith("docs/") and not path.startswith("docs/examples/") and Path(path).suffix in {".svg", ".png", ".html", ".css"}:
            continue
        if path.startswith(("mod-the-spire/base-spire/", "net-the-spire/", "tools/base-spire-dev/")):
            selected.update(("base", "net", "migration"))
        elif path.startswith(("tools/differential/", "tools/rng-oracle/")):
            selected.update(("migration", "base") if path.startswith("tools/rng-oracle/") else ("migration",))
        elif path.startswith("tools/contract-check/"):
            selected.update(("net", "migration"))
        else:
            return SUITES.copy()
    return [suite for suite in SUITES if suite in selected]


def matching_run(runs: list[dict], artifacts, tree: str) -> dict | None:
    for run in runs:
        if (run.get("event") != "pull_request" or run.get("status") != "completed"
                or run.get("conclusion") != "success" or run.get("path") != ".github/workflows/core-base.yml"):
            continue
        if any(a["name"] == "verified-tree-" + tree and not a["expired"] for a in artifacts(run["id"])):
            return run
    return None


def api(path: str):
    request = urllib.request.Request("https://api.github.com/repos/" + os.environ["GITHUB_REPOSITORY"] + "/" + path,
                                     headers={"Authorization": "Bearer " + os.environ["GH_TOKEN"],
                                              "Accept": "application/vnd.github+json", "User-Agent": "auto-the-spire-ci"})
    with urllib.request.urlopen(request, timeout=15) as response:
        return json.load(response)


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], text=True, encoding="utf-8").strip()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base")
    parser.add_argument("--head", default="HEAD")
    args = parser.parse_args()
    event_name = os.environ.get("GITHUB_EVENT_NAME", "local")
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8")) if "GITHUB_EVENT_PATH" in os.environ else {}
    tree = git("rev-parse", "HEAD^{tree}")
    reused = None
    if event_name == "push" and os.environ.get("GH_TOKEN"):
        try:
            runs = api("actions/runs?event=pull_request&status=success&per_page=20")["workflow_runs"]
            reused = matching_run(runs, lambda run: api(f"actions/runs/{run}/artifacts")["artifacts"], tree)
        except (urllib.error.URLError, OSError, KeyError, ValueError) as error:
            print(f"Previous verification unavailable; running affected checks ({type(error).__name__}).", file=sys.stderr)
    if reused:
        suites, paths = [], []
    elif event_name == "workflow_dispatch":
        suites, paths = SUITES.copy(), []
    else:
        base = args.base or (event.get("pull_request", {}).get("base", {}).get("sha") if event_name == "pull_request" else event.get("before"))
        head = event.get("pull_request", {}).get("head", {}).get("sha", args.head)
        if not base or set(base) == {"0"}:
            suites, paths = SUITES.copy(), []
        else:
            separator = "..." if event_name in {"pull_request", "local"} else ".."
            paths = subprocess.check_output(["git", "diff", "--no-renames", "--name-only", "-z", base + separator + head]).decode("utf-8").rstrip("\0").split("\0")
            paths = [path for path in paths if path]
            suites = affected(paths)
    result = {"tree": tree, "suites": suites, "paths": paths, "reused_run": reused["html_url"] if reused else ""}
    print(json.dumps(result, ensure_ascii=False))
    if output := os.environ.get("GITHUB_OUTPUT"):
        with Path(output).open("a", encoding="utf-8") as stream:
            for key in ("tree", "suites", "reused_run"):
                value = json.dumps(result[key]) if key == "suites" else result[key]
                stream.write(f"{key}={value}\n")
        record = Path(".artifacts/ci/verification.json")
        record.parent.mkdir(parents=True, exist_ok=True)
        record.write_text(json.dumps(result, ensure_ascii=False) + "\n", encoding="utf-8")
    if summary := os.environ.get("GITHUB_STEP_SUMMARY"):
        with Path(summary).open("a", encoding="utf-8") as stream:
            stream.write("Checks: " + (", ".join(suites) if suites else "reused") + "\n\n")
            if reused:
                stream.write(f"Identical tested tree `{tree}`: {reused['html_url']}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
