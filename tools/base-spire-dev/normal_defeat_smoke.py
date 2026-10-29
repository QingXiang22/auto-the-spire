"""Replay an intentionally lost normal first-act combat through the public stdio protocol."""
from __future__ import annotations
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "net-the-spire/client"))
from net_client import NetClient
from public_snapshot import candidates, entity_of, field, find_entity, first_field_value


def replay(host: Path, output: Path) -> None:
    frames = []
    client = NetClient(["dotnet", str(host.resolve())], cwd=ROOT)
    try:
        description = client.describe()["result"]
        started = client.start(description["identity"], description["capabilities"]["scenario_profiles"][0]["id"], seed="777")
        assert started["ok"], started
        session = started["result"]["session_id"]
        snapshot = started["result"]["snapshot"]

        def choose(name, predicate=lambda a: True):
            return next(a for a in candidates(snapshot, name) if predicate(a))

        def step(action):
            nonlocal snapshot
            response = client.step(session, snapshot["observation_id"], action["candidate_id"])
            frames.append(response)
            assert response["ok"], response
            snapshot = response["result"]["snapshot"]

        step(choose("StartNormalRun"))
        step(choose("net.answer_decision", lambda a: field(entity_of(snapshot, first_field_value(a)), "RelicId") == "base-spire:winged-boots"))
        step(choose("net.answer_decision"))
        step(choose("ChooseMapPoint"))
        turns = 0
        while snapshot["status"] != "terminal":
            ends = candidates(snapshot, "EndTurn")
            assert ends, (turns, snapshot["status"], snapshot["legal_actions"])
            step(ends[0])
            turns += 1
            assert turns < 200, "the synthetic defeat never resolved"
        terminal = snapshot["terminal_result"]
        assert terminal["outcome"] == "loss", terminal
        assert terminal["reason_id"].endswith("NormalRunDefeat"), terminal
        assert not snapshot["legal_actions"], "a terminal snapshot publishes no actions"
        assert field(find_entity(snapshot, "Player"), "Hp") == 0, "the losing player is at zero HP"
        assert not candidates(snapshot, "ChooseMapPoint"), "defeat exposes no map successors"
        client.close_session(session)
        print(f"Normal run defeat: {turns} end-turn actions; outcome {terminal['outcome']}; reason {terminal['reason_id']}; facts {len(terminal['facts'])}")
    finally:
        output.mkdir(parents=True, exist_ok=True)
        (output / "normal-defeat-public.json").write_text(json.dumps(frames, ensure_ascii=False), encoding="utf8")
        client.shutdown()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host-dll", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    replay(args.host_dll, args.output)
