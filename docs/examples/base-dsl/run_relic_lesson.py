"""Run a lesson through the existing real stdio client; no gameplay is implemented here."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "net-the-spire/client"))
from net_client import NetClient, NetClientError  # noqa: E402


def result(response: dict) -> dict:
    if not response.get("ok"):
        raise ValueError(f"Host rejected request: {response.get('error')}")
    return response["result"]


def block(snapshot: dict) -> int:
    values = [field["value"] for entity in snapshot["entities"] for field in entity["fields"]
              if field["field_id"] == "net:field:base-spire:Player.Block" and field["state"] == "known"]
    if len(values) != 1:
        raise ValueError("The lesson needs one visible Player.Block field.")
    return int(values[0])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--host-dll", type=Path, default=ROOT / "net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll")
    parser.add_argument("--obtain", action="append", default=[])
    parser.add_argument("--draw-count", type=int, default=1)
    parser.add_argument("--expect-block", type=int, required=True)
    parser.add_argument("--expect-trace", help="Expected block after each draw, comma-separated integers.")
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    if args.draw_count < 0 or args.draw_count > 20:
        parser.error("--draw-count must be between 0 and 20")
    client = None
    try:
        expected_trace = None if args.expect_trace is None else [int(v) for v in args.expect_trace.split(",")]
        if not args.host_dll.is_file():
            raise FileNotFoundError("Build tools/base-spire-dev first, or pass --host-dll explicitly.")
        manifest = args.manifest.resolve()
        profile = json.loads(manifest.read_text(encoding="utf-8-sig"))["default_scope"]
        client = NetClient(["dotnet", str(args.host_dll.resolve()), "--package", str(manifest)], cwd=ROOT)
        description = result(client.describe(timeout=15))
        identity = description["identity"]
        result(client.get_catalog(identity["catalog_id"], timeout=15))
        started = result(client.start(identity, profile, seed="BASE-DSL-LESSON-00", timeout=15))
        session_id, snapshot = started["session_id"], started["snapshot"]

        def submit(action: str) -> None:
            nonlocal snapshot
            matches = [a for a in snapshot["legal_actions"] if a["action_type"] == "net:action:base-spire:" + action]
            if len(matches) != 1:
                raise ValueError(f"Expected one legal candidate for {action}, got {len(matches)}")
            result(client.step(session_id, snapshot["observation_id"], matches[0]["candidate_id"], timeout=15))
            snapshot = result(client.observe(session_id, timeout=15))["snapshot"]

        for action in args.obtain:
            submit(action)
        trace = []
        for _ in range(args.draw_count):
            submit("Draw")
            trace.append(block(snapshot))
        actual = block(snapshot)
        passed = actual == args.expect_block and (expected_trace is None or trace == expected_trace)
        report = {"manifest": str(manifest), "identity": identity, "scope": profile, "seed": started["seed"],
                  "obtained_actions": args.obtain, "draw_count": args.draw_count, "block_trace": trace,
                  "block": actual, "expected_block": args.expect_block, "expected_trace": expected_trace,
                  "passed": passed, "independent_verified": False}
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(report, ensure_ascii=False))
        return 0 if passed else 1
    except (OSError, ValueError, KeyError, NetClientError) as error:
        print(f"lesson failed: {error}", file=sys.stderr)
        return 2
    finally:
        if client is not None:
            client.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
