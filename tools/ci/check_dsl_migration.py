"""Check migration boundaries, the migrated original case, and strict comparison failures."""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / "tools/base-migration-baseline"


def source_hash(path: Path) -> str:
    return hashlib.sha256(path.read_text(encoding="utf-8").encode("utf-8")).hexdigest()


def check_projects() -> int:
    solution = (ROOT / "AutoTheSpire.sln").read_text(encoding="utf-8-sig")
    projects = [ROOT / p.replace("\\", "/") for p in re.findall(r'"([^"\n]+\.csproj)"', solution)]
    seen: set[Path] = set()
    while projects:
        project = projects.pop().resolve()
        if project in seen:
            continue
        seen.add(project)
        if BASELINE in project.parents or not project.is_file():
            raise ValueError(f"invalid active project dependency: {project}")
        for node in ET.fromstring(project.read_text(encoding="utf-8-sig")).iter():
            for name, value in node.attrib.items():
                if name in ("Include", "Update") and ("base-migration-baseline" in value or "BaseSpire.csproj" in value):
                    raise ValueError(f"retired rule dependency: {project}:{value}")
            if node.tag == "ProjectReference":
                projects.append(project.parent / node.attrib["Include"].replace("\\", "/"))
            if node.tag == "HintPath" and "base-migration-baseline" in (node.text or ""):
                raise ValueError(f"retired assembly reference: {project}")
    return len(seen)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", choices=["Debug", "Release"], default="Release")
    parser.add_argument("--output", type=Path, default=ROOT / ".artifacts/ci/migration")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    try:
        projects = check_projects()
        lock = json.loads((ROOT / "reference/baselines/sts2-sim.json").read_text(encoding="utf-8"))
        fixtures = ROOT / "tools/base-spire-dev/fixtures"
        current_vectors = ["source-rng-vectors", "normal-reward-vectors", "enemy-move-vectors", "pickup-upgrade-vectors",
                           "normal-opening-vectors", "act-one-map-vectors", "map-gaussian-vectors", "normal-combat-vectors",
                           "slimes-vectors", "reference-pool-metadata", "ironclad-source-metadata", "normal-unknown-room-vector", "normal-event-selection-vector", "normal-sapphire-seed-vector", "normal-dense-vegetation-vector", "battleworn-continuation-vector", "neow-effects-vector", "draw-permission-vector"]
        hashes = {}
        for name in current_vectors:
            vector = json.loads((fixtures / (name + ".json")).read_text(encoding="utf-8"))
            if vector.get("reference_lock") != lock or vector.get("source_hash_encoding") != "utf8-lf":
                raise ValueError(f"{name}: current vectors must identify the actual locked reference")
            for path, digest in vector.get("source_sha256", {}).items():
                if path in hashes and hashes[path] != digest:
                    raise ValueError(f"conflicting current source digest: {path}")
                hashes[path] = digest
            if name == "ironclad-source-metadata":
                for row in vector["entries"]:
                    hashes["reference/sts2-sim/src/Sts2Sim.Core/Models/Cards/" + row["name"] + ".cs"] = row["source_sha256"]
        for path, digest in hashes.items():
            source = (ROOT / path).resolve()
            if not source.is_relative_to((ROOT / "reference/sts2-sim/src/Sts2Sim.Core").resolve()) or hashlib.sha256(source.read_bytes().replace(b'\r\n', b'\n')).hexdigest() != digest:
                raise ValueError(f"current vector source differs from locked checkout: {path}")
        record = json.loads((ROOT / "tools/ci/fixtures/legacy-dsl-checks.json").read_text(encoding="utf-8"))
        if record.get("hash_encoding") != "utf8-lf":
            raise ValueError("retired-source hashes must declare canonical text encoding")
        actual = set()
        for folder in ("BaseSpire.Slice.Tests", "net-tests", "differential"):
            actual.update(p.relative_to(ROOT).as_posix() for p in (BASELINE / folder).rglob("*.cs") if "bin" not in p.parts and "obj" not in p.parts)
        recorded = [row["source"] for row in record["groups"]]
        if len(recorded) != len(set(recorded)) or set(recorded) != actual:
            raise ValueError("missing or duplicate retired source-group disposition")
        for row in record["groups"]:
            if source_hash(ROOT / row["source"]) != row["sha256"] or row["executed_in_current_gate"] is not False:
                raise ValueError(f"stale or misleading retired-group evidence: {row['source']}")
        for row in record["suites"]:
            active = ROOT / row["active_definition"]
            if source_hash(active) != row["sha256"]:
                raise ValueError(f"changed original suite denominator: {active}")
            definition = json.loads(active.read_text(encoding="utf-8"))
            if definition["expected_case_ids"] != row["expected_case_ids"]:
                raise ValueError(f"original required case IDs changed: {active}")
            if "previous_definition_sha256" in row:
                original_path = ROOT / row["source"]
                if source_hash(original_path) != row["previous_definition_sha256"]:
                    raise ValueError("missing original suite evidence before derivation metadata migration")
                migrated = next(c for c in definition["cases"] if c["id"] == "random-reshuffle-unsupported")
                if migrated["setup"]["initial_piles"]["rng_derivation"] != "locked-native-sequential@1":
                    raise ValueError("reshuffle fixture must name its actual native derivation")
                migrated["setup"]["initial_piles"]["rng_derivation"] = "source-compat-sts2-frozen@1"
                if definition != json.loads(original_path.read_text(encoding="utf-8")):
                    raise ValueError("derivation metadata migration changed original inputs, scripts or dimensions")
        executable = ROOT / f"tools/differential/bin/{args.configuration}/net9.0/Differential.dll"

        def run(name: str, arguments: list[str], expected_code: int, dll: Path = executable) -> dict | None:
            folder = args.output / name
            folder.mkdir(parents=True, exist_ok=True)
            result = folder / "result.json"
            result.unlink(missing_ok=True)
            command = ["dotnet", str(dll), *arguments]
            if arguments[0] == "test":
                command += ["--output", str(folder)]
            process = subprocess.run(command, cwd=ROOT, capture_output=True, text=True,
                                     encoding="utf-8", errors="replace", timeout=120)
            (folder / "command.log").write_text(process.stdout + process.stderr, encoding="utf-8")
            if process.returncode != expected_code:
                raise ValueError(f"{name}: expected exit {expected_code}, got {process.returncode}; see {folder / 'command.log'}")
            return json.loads(result.read_text(encoding="utf-8")) if result.exists() else None

        dummy = run("battle-friend", ["battle-friend-compare", "--output", str(args.output / "battle-friend")], 0)
        if (dummy is None or dummy["status"] != "passed" or dummy["independent_verified"] is not True
                or dummy["normal_run_verified"] is not False or dummy["parent_event_verified"] is not False
                or len(dummy["cases"]) != 13 or any(c["status"] != "passed" for c in dummy["cases"])):
            raise ValueError("BattleFriend controlled component comparison did not execute all required cases")

        wriggler = run("wriggler", ["wriggler-compare", "--output", str(args.output / "wriggler")], 0)
        wriggler_cases = [seed + ":" + mode for seed in ("777", "A😀B")
                          for mode in ("four", "stunned", "hp-exhausted", "block", "unpowered", "death", "retain", "ethereal", "auto", "force-auto", "cost", "nonhand")]
        if (wriggler is None or wriggler["status"] != "passed" or wriggler["independent_verified"] is not True
                or wriggler["normal_run_verified"] is not False or wriggler["parent_event_verified"] is not False
                or [c["id"] for c in wriggler["cases"]] != wriggler_cases or any(c["status"] != "passed" for c in wriggler["cases"])):
            raise ValueError("Wriggler/Infection controlled component comparison did not execute all required cases")

        unknown = run("unknown-rooms", ["unknown-room-compare", "--output", str(args.output / "unknown-rooms")], 0)
        unknown_cases = [seed + ":" + mode for seed in ("777", "A😀B")
                         for mode in ("normal", "exclude-shop", "double", "event-only", "no-event")]
        if (unknown is None or unknown["status"] != "passed" or unknown["independent_verified"] is not True
                or unknown["normal_run_verified"] is not False
                or [c["id"] for c in unknown["cases"]] != unknown_cases
                or any(c["status"] != "passed" for c in unknown["cases"])):
            raise ValueError("Unknown room comparison did not execute all required controlled cases")
        for case in unknown["cases"]:
            for side in ("left", "right"):
                boundaries = case[side]
                if (len(boundaries) != 20 or boundaries[0]["at"] != "start"
                        or boundaries[13]["at"] != "reset" or boundaries[13]["draws"] != 12
                        or boundaries[-1]["draws"] != 18
                        or sum(b["float_bits"] is not None for b in boundaries) != 18
                        or any(len(b["state"]) != 4 or len(b["odds"]) != 4 for b in boundaries)):
                    raise ValueError("Unknown room comparison lost source draws, complete state or reset boundaries")

        events = run("event-selection", ["event-selection-compare", "--output", str(args.output / "event-selection")], 0)
        event_modes = ("default", "gold43", "gold44", "gold100", "gold148", "gold149", "gold150",
                       "hp9", "hp10", "hp56", "hp57", "floor6", "floor7", "no-deck", "one-card",
                       "eternal-deck", "empty-bag", "two-potions", "egg", "byrdpip", "paels-legion")
        event_ids = [seed + ":" + mode for seed in ("777", "A😀B") for mode in event_modes]
        if (events is None or events["status"] != "passed" or events["independent_verified"] is not True
                or events["normal_run_verified"] is not False
                or [c["id"] for c in events["cases"]] != event_ids
                or any(c["status"] != "passed" or c["left"] != c["right"] for c in events["cases"])):
            raise ValueError("Event selection comparison did not execute every required controlled case")
        # Obtain the complete source denominator from the independently generated opening vector.
        opening = json.loads((fixtures / "normal-opening-vectors.json").read_text(encoding="utf-8"))
        expected_events = sorted(row["id"] for row in opening["definitions"] if row["category"] == "event" and row["act"] == 0)
        if len(expected_events) != 31 or len(set(expected_events)) != 31:
            raise ValueError("First-act event source denominator changed")
        for case in events["cases"]:
            for side in ("left", "right"):
                result = case[side]
                if (result["random_unchanged"] is not True or result["queries_read_only"] is not True
                        or [e["id"] for e in result["eligibility"]] != expected_events
                        or len(result["boundaries"]) != 41 or result["boundaries"][0] != {"selected": "none", "cursor": 1, "visited": []}
                        or any(b["selected"] not in expected_events or b["selected"] not in b["visited"]
                               or b["cursor"] <= previous["cursor"]
                               for previous, b in zip(result["boundaries"], result["boundaries"][1:]))):
                    raise ValueError("Event selection lost full eligibility, scan history or read-only/RNG evidence")

        for row in record["suites"]:
            definition = json.loads((ROOT / row["active_definition"]).read_text(encoding="utf-8"))
            suite = definition["suite"]
            report = run(suite, ["test", "--suite", suite], 3)
            expected = definition["expected_case_ids"]
            if (report["expected_case_ids"] != expected or report["independent_verified"] is not False
                    or [r["id"] for r in report["cases"]] != expected or report["complete"] is not True):
                raise ValueError(f"{suite}: original case identities or incomplete-suite failure lost")
            if suite == "regression":
                if report["executed"] != 2 or report["passed"] != 1 or report["cases"][0]["status"] != "passed":
                    raise ValueError("strike-normal must actually execute independently and pass")
                migrated = report["cases"][0]
                if (migrated["left"]["backend"] != "dsl:base-spire" or migrated["right"]["backend"] != "reference:sts2-sim"
                        or migrated["left"]["script_random_count"] != 0 or migrated["right"]["script_random_count"] != 0):
                    raise ValueError("strike-normal backend or measured RNG changed")
                reshuffle = next(c for c in report["cases"] if c["id"] == "random-reshuffle-unsupported")
                if reshuffle["status"] != "diverged" or not reshuffle.get("first_difference") or reshuffle["independent_verified"]:
                    raise ValueError("original random case must retain its real independent first difference")
                for side in ("left", "right"):
                    backend = reshuffle[side]
                    if (backend["script_random_count"] != 9 or len(backend["boundaries"]) != 4
                            or len(backend["boundaries"][-1]["random"]["calls"]) != 9
                            or any(b["random"]["dropped"] or b["random"]["truncated"] for b in backend["boundaries"])):
                        raise ValueError("random case did not extract all real original reshuffle calls")
                import sys
                sys.path.insert(0, str(ROOT / "tools/rng-oracle"))
                spec = importlib.util.spec_from_file_location("source_rng_oracle", ROOT / "tools/rng-oracle/locked_rng_oracle.py")
                oracle = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(oracle)
                initialization = reshuffle["right"]["initialization"]
                raw_seed = initialization["raw_game_seed"]
                if initialization["source_run_seed"] != oracle.deterministic_hash_code(raw_seed):
                    raise ValueError("reference source profile root differs from independent oracle")
                if {row["source"] for row in initialization["run_streams"]} != set(oracle.RUN_RNG_TYPES):
                    raise ValueError("reference source profile omitted a run stream")
                for row in initialization["run_streams"]:
                    if row["seed"] != oracle.stream_seed(oracle.deterministic_hash_code(raw_seed), row["source"]):
                        raise ValueError(f"reference stream derivation differs from independent oracle: {row['source']}")
                # Verify actual extracted calls against independent source vectors, not only agreeing adapters.
                for side in ("left", "right"):
                    source_rng = oracle.Rng(oracle.stream_seed(oracle.deterministic_hash_code(raw_seed), "Shuffle"))
                    calls = reshuffle[side]["boundaries"][-1]["random"]["calls"]
                    for index, call in enumerate(calls):
                        bound = 10 - index
                        if (call["operation"] != "shuffle" or call["minimum"] != "0" or call["maximum"] != str(bound)
                                or call["result"] != str(source_rng.next_int(bound)) or call["before"] != index
                                or call["after"] != index + 1 or len(call["candidates"]) != bound):
                            raise ValueError(f"{side}: extracted call differs from independent source vector at {index}")
                remainder = [c for c in report["cases"] if c["id"] not in ("strike-normal", "random-reshuffle-unsupported")]
            else:
                if report["executed"] != 0 or report["passed"] != 0:
                    raise ValueError(f"{suite}: unmigrated suite claims execution")
                remainder = report["cases"]
            if any(r["status"] != "unsupported" for r in remainder):
                raise ValueError(f"{suite}: unsupported original case status changed")

        partial = run("strike-only", ["test", "--suite", "regression", "--case", "strike-normal"], 1)
        if partial["complete"] or partial["independent_verified"] or partial["cases"][0]["status"] != "passed":
            raise ValueError("selected case must pass without claiming the entire suite passed")
        run("comparator-checks", ["self-test"], 0)
        normal_output = args.output / "normal-preflight"
        normal = run("normal-preflight", ["normal-preflight", "--output", str(normal_output)], 3)
        if (normal["expected_case_ids"] != ["m01-claim", "m01-skip"] or normal["executed"] != 2
                or normal["complete"] or normal["independent_verified"]
                or any(c["status"] != "blocked_compare" or not c["dsl_returned_to_map"]
                       or c["first_difference"] is not None or c["left"]["facts"] != c["right"]["facts"] or not c["unverified_dimensions"]
                       or c["right"]["diagnostic"]["normal_compatibility_alignment"] for c in normal["cases"])):
            raise ValueError("normal preflight must match every opening fact and retain both public returns, while rejecting incomplete M01")
        whistle = run("whistle", ["whistle-compare", "--output", str(args.output / "whistle")], 0)
        whistle_cases = [seed + ":" + mode for seed in ("777", "A😀B")
                         for mode in ("first", "upgraded", "pending-buff", "pending-attack", "random", "two", "lethal", "repeat")]
        if (whistle is None or whistle["status"] != "passed" or whistle["independent_verified"] is not True
                or whistle["normal_run_verified"] is not False
                or [c["id"] for c in whistle["cases"]] != whistle_cases or any(c["status"] != "passed" for c in whistle["cases"])):
            raise ValueError("Whistle controlled comparison did not execute all required paid-card and stun cases")

        rotations = run("enemy-rotations", ["enemy-rotation-compare", "--output", str(args.output / "enemy-rotations")], 0)
        rotation_names = ("AssassinRubyRaider", "AxeRubyRaider", "BowlbugEgg", "BruteRubyRaider", "CrossbowRubyRaider", "Stabbot", "TrackerRubyRaider")
        rotation_ids = [seed + ":" + name + ":" + mode for seed in ("777", "A😀B")
                        for name in rotation_names for mode in ("rotation", "stun", "two")]
        rotation_ids += [seed + ":TrackerRubyRaider:" + mode for seed in ("777", "A😀B")
                         for mode in ("vigor", "retaliation", "lethal", "lethal-clean")]
        if (rotations is None or rotations["status"] != "passed" or rotations["independent_verified"] is not True
                or rotations["normal_run_verified"] is not False or [case["id"] for case in rotations["cases"]] != rotation_ids):
            raise ValueError("Enemy rotations must execute and pass all original controlled cases")
        for case in rotations["cases"]:
            boundaries = 3 if case["id"].rsplit(":", 1)[1] in ("vigor", "retaliation", "lethal", "lethal-clean") else 5
            if case["status"] != "passed" or case["left"] != case["right"] or len(case["left"]) != boundaries:
                raise ValueError(f"Enemy rotation changed its measured boundaries: {case['id']}")

        amounts = run("power-amounts", ["power-amount-compare", "--output", str(args.output / "power-amounts")], 0)
        amount_steps = {
            "new-debuff": 2, "stack-debuff": 3, "modify-debuff": 3, "negative-strength": 2,
            "positive-strength": 2, "new-zero": 2, "existing-zero": 3, "remove-debuff": 3,
            "negative-offset": 3, "negative-stack": 3, "negative-debuff": 2, "single-debuff": 2,
            "single-buff-stack": 3, "blocked-mangle": 2, "artifact-exhaust": 3,
            "flex": 4, "setup-strike": 4, "mangle": 4, "flex-artifact-expiry": 3,
            "setup-artifact-expiry": 3, "mangle-artifact-expiry": 3, "independent": 3,
            "per-applier": 5, "source": 5,
        }
        amount_ids = [seed + ":" + side + ":" + name for seed in ("777", "A😀B")
                      for side in ("player", "enemy") for name in amount_steps]
        if (amounts is None or amounts["schema"] != "dsl-migration/power-amounts@1"
                or amounts["status"] != "passed" or amounts["independent_verified"] is not True
                or amounts["normal_run_verified"] is not False or amounts["reference"]["commit"] != lock["commit"]
                or [case["id"] for case in amounts["cases"]] != amount_ids):
            raise ValueError("Power amount comparison must execute and pass all 96 controlled cases")
        amount_dimensions = {"at", "player", "enemy", "player_hp", "enemy_hp", "ai_draws", "ai_state", "shuffle_draws", "shuffle_state"}
        power_dimensions = {"definition", "amount", "type", "stack_type", "skip_tick", "applier"}
        for case in amounts["cases"]:
            if (case["status"] != "passed" or case["left"] != case["right"]
                    or len(case["left"]) != amount_steps[case["id"].rsplit(":", 1)[1]] + 1
                    or any(set(boundary) != amount_dimensions or len(boundary["ai_state"]) != 4
                           or len(boundary["shuffle_state"]) != 4
                           or any(set(power) != power_dimensions for side in ("player", "enemy") for power in boundary[side])
                           for boundary in case["left"])):
                raise ValueError(f"Power amount comparison omitted or changed a required boundary: {case['id']}")

        paired_output = args.output / "normal-paired"
        paired = run("normal-paired", ["normal-compare", "--output", str(paired_output)], 3)
        paired_ids = ["m01-claim", "m01-skip", "m01-targeted-claim", "m01-targeted-skip"]
        if (paired["expected_case_ids"] != paired_ids or paired["executed"] != 4
                or [c["id"] for c in paired["cases"]] != paired_ids
                or paired["complete"] or paired["independent_verified"]
                or any(c["status"] != "blocked_compare" or c["independent_verified"] or c["complete"]
                       or c["reason"] or c["first_difference"] or not c["returned_to_map"] or len(c["boundaries"]) < 19
                       or not c["boundaries"][-1]["left"]["missing_dimensions"]
                       or len(c["protocol_checks"]) != len(c["boundaries"])
                       or any(not p["evidence"]["passed"] for p in c["protocol_checks"])
                       or any(b["left"]["diagnostic"]["unmapped_public_actions"] for b in c["boundaries"])
                       or any("public.piles" not in b[side]["values"] or "random.stream_states" not in b[side]["values"]
                              or "events.observed_order" not in b[side]["values"]
                              or "lifecycle.settlement" not in b[side]["values"]
                              or any(path not in b[side]["values"] for path in (
                                  "public.map", "public.run", "public.neow", "public.reward_offer", "public.potion_inventory",
                                  "public.relic_inventory", "public.cards", "public.player", "public.enemies", "public.powers", "public.visits"))
                              for b in c["boundaries"] for side in ("left", "right"))
                       or any(not json.loads(c["boundaries"][-1][side]["values"]["events.observed_order"])
                              for side in ("left", "right"))
                       or any(len(json.loads(c["boundaries"][-1][side]["values"]["lifecycle.settlement"])) != 2
                              for side in ("left", "right"))
                       or (c["id"].startswith("m01-targeted-")
                           and not any(a["kind"] == "choose-target" for a in c["prefix"])) for c in paired["cases"])):
            raise ValueError("all four normal paths must return with matching dimensions, real target decisions and protocol evidence while rejecting incomplete M01")
        random_case = run("reshuffle-only", ["test", "--suite", "regression", "--case", "random-reshuffle-unsupported"], 1)
        if random_case["passed"] or random_case["independent_verified"] or random_case["cases"][0]["status"] != "diverged":
            raise ValueError("original random case must fail explicitly on the real difference")
        run("unknown-case", ["test", "--case", "not-registered"], 3)
        original = json.loads((ROOT / "tools/differential/suites/regression.json").read_text(encoding="utf-8"))
        for name in ("zero-cases", "missing-dimension", "duplicate-case", "unregistered-case", "reduced-suite", "wrong-terminal"):
            mutated = json.loads(json.dumps(original))
            if name == "zero-cases":
                mutated["expected_case_ids"] = []; mutated["cases"] = []
            elif name == "missing-dimension":
                mutated["cases"][0]["required_dimensions"].remove("pile-order")
            elif name == "duplicate-case":
                mutated["cases"].append(mutated["cases"][0])
            elif name == "unregistered-case":
                mutated["expected_case_ids"].pop()
            elif name == "reduced-suite":
                mutated["expected_case_ids"] = ["strike-normal"]; mutated["cases"] = mutated["cases"][:1]
            else:
                mutated["cases"][0]["expected_terminal"] = "victory"
            source = args.output / f"{name}.json"
            source.write_text(json.dumps(mutated), encoding="utf-8")
            report = run(name, ["test", "--suite-file", str(source), "--case", "strike-normal"], 3)
            if report and (report["independent_verified"] or report["passed"]):
                raise ValueError(f"{name}: invalid input counted as passed")

        # Separate build outputs verify missing-adapter failure without altering the production checkout or default build.
        isolated = args.output / "without-reference"
        build = subprocess.run(["dotnet", "build", str(ROOT / "tools/differential/Differential.csproj"), "-c", args.configuration,
                                "-p:UseReference=false", "--artifacts-path", str(isolated)], cwd=ROOT,
                               capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=120)
        (args.output / "without-reference-build.log").write_text(build.stdout + build.stderr, encoding="utf-8")
        if build.returncode:
            raise ValueError("missing-reference build failed")
        fallback = isolated / "bin/Differential" / args.configuration.lower() / "Differential.dll"
        missing = run("missing-reference", ["test", "--case", "strike-normal"], 3, fallback)
        if missing["executed"] or missing["passed"] or "unsupported_reference_adapter" not in missing["cases"][0]["reason"]:
            raise ValueError("missing reference adapter was not rejected explicitly")
        absent_output = args.output / "normal-without-reference"
        absent = run("normal-without-reference", ["normal-preflight", "--output", str(absent_output)], 3, fallback)
        if (absent["executed"] or absent["complete"] or absent["independent_verified"]
                or len(absent["cases"]) != 2 or any(c["status"] != "blocked_compare"
                   or "unsupported_reference_adapter" not in c["reason"] for c in absent["cases"])):
            raise ValueError("normal preflight must reject a missing native reference and zero execution")
        absent_paired_output = args.output / "paired-without-reference"
        absent_paired = run("paired-without-reference", ["normal-compare", "--output", str(absent_paired_output)], 3, fallback)
        if (absent_paired["executed"] or absent_paired["complete"] or absent_paired["independent_verified"]
                or absent_paired["expected_case_ids"] != paired_ids or len(absent_paired["cases"]) != 4 or any(c["status"] != "blocked_compare"
                   or "unsupported_reference_adapter" not in c["reason"] for c in absent_paired["cases"])):
            raise ValueError("normal paired comparison must reject missing reference and zero execution")
        result = {"gate": "migration-contract", "active_projects": projects, "retired_source_groups": len(recorded),
                  "original_suites": len(record["suites"]), "original_regression_exit": 3,
                  "original_regression_executed": 2, "original_regression_passed": 1, "random_trace_verified": True,
                  "strict_failure_checks": True, "normal_preflight": "native-opening-match; incomplete-M01",
                  "normal_paired": "four-claim-skip-targeted-map-returns; protocol-replay-verified; incomplete-M01", "independent_verified": False}
        (args.output / "migration-contract.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(result))
        return 0
    except (OSError, ValueError, KeyError, ET.ParseError, subprocess.TimeoutExpired) as error:
        print(f"migration contract failure: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
