"""Run broad regression checks in GitHub Actions or explicitly on local request."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[2]


def run_batch(suites: list[str], configuration: str, output: Path) -> int:
    output.mkdir(parents=True, exist_ok=True)
    result = {"suites": [], "configuration": configuration, "exit_code": 0}
    for suite in suites:
        suite_output = output / suite
        command = [sys.executable, "-X", "utf8", str(Path(__file__).resolve()), suite,
                   "--configuration", configuration, "--output", str(suite_output)]
        try:
            code = subprocess.run(command, cwd=ROOT).returncode
        except OSError as error:
            print(f"[{suite}] {error}", file=sys.stderr, flush=True)
            code = 127
        result["suites"].append({"suite": suite, "exit_code": code, "result": str(suite_output / "result.json")})
        if code and not result["exit_code"]:
            result["exit_code"] = code
    (output / "batch-result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("\nBatch results:", flush=True)
    for item in result["suites"]:
        print(f"  {item['suite']}: exit {item['exit_code']}", flush=True)
    print(f"Report: {output / 'batch-result.json'}", flush=True)
    return result["exit_code"]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("suite", nargs="+", choices=["core", "base", "net", "differential", "migration", "docs"])
    parser.add_argument("--configuration", choices=["Debug", "Release"], default="Release")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if len(set(args.suite)) != len(args.suite):
        parser.error("each suite may be selected only once")
    if len(args.suite) > 1:
        return run_batch(args.suite, args.configuration, (args.output or ROOT / ".artifacts/ci").resolve())
    args.suite = args.suite[0]
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
        project = "tools/base-spire-dev/BaseSpire.Dev.csproj"
        commands = [build(project), run(project) + ["--", "validate"],
                    run(project) + ["--", "check", "--report", str(output / "behavior")],
                    run(project) + ["--", "check", "--normal-run-only"],
                    [sys.executable, "-X", "utf8", "tools/ci/check_relic_coverage.py", str(output / "behavior/package-behavior.json")],
                    [sys.executable, "-X", "utf8", "tools/ci/check_card_coverage.py", str(output / "behavior/package-behavior.json"), "--require-ironclad-complete"]]
    elif args.suite == "net":
        project = "net-the-spire/Net.TheSpire.Tests"
        commands = [build(project), run(project)]
    elif args.suite == "migration":
        commands = [[sys.executable, "-X", "utf8", "tools/differential/prepare_reference.py"],
                    build("tools/differential/Differential.csproj"),
                    [sys.executable, "-X", "utf8", "tools/ci/check_dsl_migration.py", "--configuration", config, "--output", str(output)]]
    elif args.suite == "docs":
        commands = [[sys.executable, "-X", "utf8", "tools/doc-check/check_docs.py"],
                    [sys.executable, "-X", "utf8", "tools/doc-check/check_terms.py"],
                    ["git", "diff", "--check"]]
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
              "rule_authority_lock": json.loads((ROOT / "reference/baselines/sts2-sim.json").read_text(encoding="utf-8")),
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
