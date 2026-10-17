"""Run broad regression checks in GitHub Actions or explicitly on local request."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[2]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("suite", choices=["core", "base", "net", "differential"])
    parser.add_argument("--configuration", choices=["Debug", "Release"], default="Release")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    output = (args.output or ROOT / ".artifacts/ci" / args.suite).resolve()
    output.mkdir(parents=True, exist_ok=True)
    config = args.configuration

    def build(project: str) -> list[str]:
        return ["dotnet", "build", project, "-c", config]

    def run(project: str) -> list[str]:
        return ["dotnet", "run", "--no-build", "--project", project, "-c", config]

    if args.suite == "core":
        project = "core-the-spire/Core.TheSpire.Engine.Tests"
        commands = [build(project), run(project)]
    elif args.suite == "base":
        project = "mod-the-spire/base-spire/BaseSpire.Slice.Tests"
        commands = [build(project), run(project) + ["--", "--suite", "runtime", "--report", str(output / "behavior")],
                    build("tools/base-spire-dev/BaseSpire.Dev.csproj"),
                    [sys.executable, "-X", "utf8", "tools/base-spire-dev/verify_examples.py"]]
    elif args.suite == "net":
        project = "net-the-spire/Net.TheSpire.Tests"
        commands = [build(project), run(project)]
    else:
        project = "tools/differential/Differential.csproj"
        commands = [[sys.executable, "-X", "utf8", "tools/differential/prepare_reference.py"],
                    build(project), run(project) + ["--", "test", "--suite", "regression", "--output", str(output / "regression")]]
    env = dict(os.environ, PYTHONUTF8="1",
               BASE_SPIRE_DEV_DLL=str(ROOT / f"tools/base-spire-dev/bin/{config}/net9.0/BaseSpire.Dev.dll"))
    top = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=ROOT, capture_output=True, text=True)
    commit = None
    if top.returncode == 0 and Path(top.stdout.strip()).resolve() == ROOT:
        commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    result = {"suite": args.suite, "configuration": config, "commit": commit,
              "reference_lock": json.loads((ROOT / "reference/baselines/sts2-sim.json").read_text(encoding="utf-8"))
              if args.suite == "differential" else None, "steps": [], "exit_code": 0}
    try:
        for index, command in enumerate(commands, 1):
            log = output / f"{index:02d}.log"
            print(f"\n[{args.suite}] {subprocess.list2cmdline(command)}", flush=True)
            with log.open("w", encoding="utf-8") as stream:
                process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=subprocess.PIPE,
                                           stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
                for line in process.stdout:
                    stream.write(line)
                    print(line, end="", flush=True)
                code = process.wait()
            result["steps"].append({"command": command, "exit_code": code, "log": log.name})
            if code:
                result["exit_code"] = code
                break
    except OSError as error:
        result["error"] = str(error)
        result["exit_code"] = 127
    finally:
        (output / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        if summary := os.environ.get("GITHUB_STEP_SUMMARY"):
            with Path(summary).open("a", encoding="utf-8") as stream:
                stream.write(f"### {args.suite}\n\nConfiguration: `{config}`; exit code: `{result['exit_code']}`.\n\n")
                for step in result["steps"]:
                    stream.write(f"- `{subprocess.list2cmdline(step['command'])}` → `{step['exit_code']}`\n")
    return result["exit_code"]


if __name__ == "__main__":
    raise SystemExit(main())
