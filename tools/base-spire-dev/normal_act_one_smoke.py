"""Replay the frozen reference seed-777 first-act victory through the public stdio protocol."""
from __future__ import annotations
import argparse
import json
import re
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "net-the-spire/client"))
from net_client import NetClient
from public_snapshot import candidates, entity_of, entities_of, field, find_entity, first_field_value, pile_visible


def replay(host: Path, output: Path) -> None:
    source = json.loads((ROOT / "tools/base-spire-dev/fixtures/normal-act-one-route.json").read_text(encoding="utf8"))
    frames = []
    client = NetClient(["dotnet", str(host.resolve())], cwd=ROOT)
    try:
        description = client.describe()["result"]
        started = client.start(description["identity"], description["capabilities"]["scenario_profiles"][0]["id"], seed=source["seed"])
        assert started["ok"], started
        session = started["result"]["session_id"]
        snapshot = started["result"]["snapshot"]

        def choose(name, predicate=lambda a: True):
            available = candidates(snapshot, name)
            return next(a for a in available if predicate(a))

        def step(action):
            nonlocal snapshot
            response = client.step(session, snapshot["observation_id"], action["candidate_id"])
            frames.append(response)
            assert response["ok"], response
            snapshot = response["result"]["snapshot"]

        def identified(name, definition):
            return choose(name, lambda a: (field(entity_of(snapshot, first_field_value(a)), "DefinitionId") or entity_of(snapshot, first_field_value(a)).get("content_id")) == definition)

        def target_answer(combat_id, before):
            target = next(e for e in before["enemies"] if e["id"] == combat_id)
            definition = "base-spire:" + re.sub(r"(?<!^)(?=[A-Z])", "-", target["name"]).lower()
            return choose("net.answer_decision", lambda a: entity_of(snapshot, first_field_value(a)).get("content_id") == definition and field(entity_of(snapshot, first_field_value(a)), "Hp") == target["hp"])

        step(choose("StartNormalRun"))
        for index, expected in enumerate(source["steps"]):
            choice = expected["choice"]
            # Native driver closes these completed rooms before asking for its next map choice.
            if choice["kind"] == "map":
                for leave in ("LeaveDevelopmentRoom", "LeaveNormalItemRoom"):
                    if candidates(snapshot, leave):
                        step(choose(leave))
            player = find_entity(snapshot, "Player")
            run = find_entity(snapshot, "Run")
            actual = (field(run, "TotalFloor"), field(player, "Hp"), field(player, "Gold"))
            wanted = (expected["floor"], expected["hp"], expected["gold"])
            assert actual == wanted, (index, choice, "reference pre-action floor/HP/gold", wanted, actual)
            kind = choice["kind"]
            if kind == "neow":
                step(choose("net.answer_decision", lambda a: field(entity_of(snapshot, first_field_value(a)), "RelicId") == "base-spire:winged-boots"))
                step(choose("net.answer_decision"))
            elif kind == "map":
                step(choose("ChooseMapPoint", lambda a: (field(entity_of(snapshot, first_field_value(a)), "Col"), field(entity_of(snapshot, first_field_value(a)), "Row")) == (choice["col"], choice["row"])))
            elif kind == "play":
                if choice["target"] is None:
                    step(identified("PlayCardFromHand", choice["id"]))
                else:
                    step(identified("PlayTargetedCard", choice["id"]))
                    step(target_answer(choice["target"], expected))
            elif kind == "end-turn":
                step(choose("EndTurn"))
            elif kind == "reward-card":
                step(identified("ClaimDevelopmentCardReward", choice["id"]))
            elif kind == "reward":
                action = {"TakeGold": "ClaimDevelopmentGoldReward", "SkipCard": "SkipDevelopmentCardReward", "Done": "CloseDevelopmentRewards"}.get(choice["id"])
                if choice["id"] == "TakePotion":
                    from public_snapshot import sequence
                    action = "ClaimDevelopmentPotionReward" if len(sequence(snapshot, "Potions")) < 3 else "SkipDevelopmentPotionReward"
                step(choose(action))
            elif kind == "shop":
                if choice["id"] == "leave":
                    step(choose("LeaveNormalItemRoom"))
                else:
                    step(identified("BuyMerchantStock", choice["id"]))
            elif kind == "rest":
                step(choose("RestAtDevelopmentCampfire"))
            elif kind == "potion":
                targeted = choice["target"] != 0
                step(identified("UseTargetedPotion" if targeted else "UsePotion", choice["id"]))
                if targeted:
                    step(target_answer(choice["target"], expected))
            else:
                raise AssertionError((index, "unhandled frozen source decision", choice))
            print(f"ok {index + 1}/{len(source['steps'])} {kind}", flush=True)
        player = find_entity(snapshot, "Player")
        run = find_entity(snapshot, "Run")
        end = source["expected"]
        for key, name in (("act_index", "ActIndex"), ("floor", "TotalFloor"), ("phase", "Phase"), ("ancient", "AncientId")):
            assert field(run, name) == end[key], (key, field(run, name), end[key])
        assert field(player, "Hp") == end["hp"] and field(player, "Gold") == end["gold"]
        assert len(pile_visible(snapshot, "RunDeckView")) == end["deck_count"]
        assert not candidates(snapshot, "ChooseMapPoint") and candidates(snapshot, "EnterNextAncient")
        client.close_session(session)
        print(f"Normal first-act victory: {len(frames)} public actions; act 2 entry, HP {end['hp']}, gold {end['gold']}, deck {end['deck_count']}")
    finally:
        output.mkdir(parents=True, exist_ok=True)
        (output / "normal-act-one-public.json").write_text(json.dumps(frames, ensure_ascii=False), encoding="utf8")
        client.shutdown()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host-dll", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    replay(args.host_dll, args.output)
