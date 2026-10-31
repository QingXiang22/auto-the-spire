"""Run the four frozen event-deck branches through the public stdio protocol."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "net-the-spire/client"))
from net_client import NetClient
from public_snapshot import candidates, entity_of, field, find_entity, first_field_value, pile_visible

INSTANCE = "net:field:net.answer_decision.Instance"
SECOND = "net:field:net.answer_decision.SecondInstance"


def replay(host: Path, output: Path) -> None:
    fixture = json.loads((ROOT / "tools/base-spire-dev/fixtures/normal-event-deck-vector.json").read_text(encoding="utf-8"))
    output.mkdir(parents=True, exist_ok=True)
    results = []
    for row in fixture["branches"]:
        frames = []
        client = NetClient(["dotnet", str(host.resolve())], cwd=ROOT)
        try:
            description = client.describe()["result"]
            started = client.start(description["identity"], description["capabilities"]["scenario_profiles"][0]["id"], seed=row["seed"])
            assert started["ok"], started
            session = started["result"]["session_id"]
            snapshot = started["result"]["snapshot"]

            def choose(name, predicate=lambda _: True):
                return next(a for a in candidates(snapshot, name) if predicate(a))

            def values(action):
                return {f["field_id"]: f["value"] for f in action["fields"]}

            def step(action, verify_replay=False):
                nonlocal snapshot
                previous = snapshot["observation_id"]
                request_id = "event-deck-step-" + str(len(frames))
                response = client.step(session, previous, action["candidate_id"], request_id=request_id)
                frames.append(response)
                assert response["ok"], response
                snapshot = response["result"]["snapshot"]
                if verify_replay:
                    assert client.step(session, previous, action["candidate_id"], request_id=request_id) == response
                    rejected = client.step(session, previous, action["candidate_id"], request_id=request_id + "-stale")
                    assert not rejected["ok"] and rejected["error"]["code"] == "stale_observation", rejected
                    assert client.step(session, previous, action["candidate_id"], request_id=request_id + "-stale") == rejected
                    assert client.observe(session)["result"]["snapshot"] == snapshot

            def map_at(coordinates):
                return choose("ChooseMapPoint", lambda a: (field(entity_of(snapshot, first_field_value(a)), "Col"),
                    field(entity_of(snapshot, first_field_value(a)), "Row")) == tuple(coordinates))

            step(choose("StartNormalRun"))
            step(choose("net.answer_decision", lambda a: field(entity_of(snapshot, first_field_value(a)), "RelicId") == "base-spire:booming-conch"))
            step(choose("net.answer_decision"))
            step(map_at(row["first"]))
            for _ in range(100):
                if candidates(snapshot, "CloseDevelopmentRewards"):
                    break
                hand = pile_visible(snapshot, "HandView")
                action = None
                for name in ("PlayAttackFromHand", "PlayCardFromHand"):
                    offered = candidates(snapshot, name)
                    action = next((a for card in hand for a in offered if first_field_value(a) == card), None)
                    if action:
                        break
                step(action or choose("EndTurn"))
            assert candidates(snapshot, "CloseDevelopmentRewards"), "registered normal starter-deck prefix did not win"
            if row["claim_gold"]:
                step(choose("ClaimDevelopmentGoldReward"))
            step(choose("CloseDevelopmentRewards"))
            deck = pile_visible(snapshot, "RunDeckView")
            # Public piles expose membership, not the private deck/hand order.
            # Bind these starter-deck choices by definition and occurrence; the
            # engine/reference check separately verifies exact source append order.
            logical_deck = []
            for expected in row["before"]["deck"]:
                logical_deck.append(next(i for i in deck if i not in logical_deck
                    and entity_of(snapshot, i).get("content_id") == expected["id"]
                    and field(entity_of(snapshot, i), "Upgraded") == expected["upgraded"]))
            step(map_at(row["unknown"]))
            visit = find_entity(snapshot, "EventVisit")
            assert visit.get("content_id") == row["definition"]
            visit_id = visit["instance_id"]
            before_hp = field(find_entity(snapshot, "Player"), "Hp")
            assert before_hp > 0
            for key, name in (("max_hp", "MaxHp"), ("gold", "Gold")):
                assert field(find_entity(snapshot, "Player"), name) == row["before"][key], (row["branch"], name)
            first_option = row["branch"] in ("LET_GO", "GROUP")
            step(choose("net.answer_decision", lambda a: any(f["value"] is first_option for f in a["fields"])))
            for selection in row["selections"]:
                selected = [logical_deck[i] for i in selection["selected"]]
                offered = candidates(snapshot, "net.answer_decision")
                if len(selected) == 2:
                    pairs = [(values(a)[INSTANCE], values(a)[SECOND]) for a in offered]
                    count = len(selection["candidates"])
                    assert len(pairs) == count * (count - 1) and len(set(pairs)) == len(pairs)
                    assert all(x != y and (y, x) in pairs for x, y in pairs)
                    answer = next(a for a in offered if (values(a)[INSTANCE], values(a)[SECOND]) == tuple(selected))
                else:
                    answer = next(a for a in offered if values(a).get(INSTANCE) == selected[0])
                assert field(find_entity(snapshot, "Player"), "Gold") == selection["gold"]
                step(answer, verify_replay=True)
            actual = pile_visible(snapshot, "RunDeckView")
            assert len(actual) == len(row["after"]["deck"])
            matched = []
            for expected in row["after"]["deck"]:
                instance = logical_deck[expected["original_index"]] if expected["original_index"] >= 0 else next(
                    i for i in actual if i not in deck and i not in matched and entity_of(snapshot, i).get("content_id") == expected["id"])
                matched.append(instance)
                card = entity_of(snapshot, instance)
                assert card.get("content_id") == expected["id"] and field(card, "Upgraded") == expected["upgraded"]
                if expected["original_index"] >= 0:
                    assert instance == logical_deck[expected["original_index"]]
                else:
                    assert instance not in deck
            assert set(actual) == set(matched)
            # This public candidate policy can produce a different first-fight HP
            # from private source hand order. Compare the independently exported
            # event effect at the public path's actual living entry HP.
            assert field(find_entity(snapshot, "Player"), "Hp") == before_hp + row["after"]["hp"] - row["before"]["hp"]
            for key, name in (("max_hp", "MaxHp"), ("gold", "Gold")):
                assert field(find_entity(snapshot, "Player"), name) == row["after"][key]
            assert not candidates(snapshot, "net.answer_decision")
            step(choose("CloseNormalEvent"), verify_replay=True)
            assert field(find_entity(snapshot, "Player"), "Room") == "map"
            assert field(entity_of(snapshot, visit_id), "Status") == "closed"
            next_nodes = [entity_of(snapshot, first_field_value(a)) for a in candidates(snapshot, "ChooseMapPoint")]
            assert sorted([field(n, "Col"), field(n, "Row")] for n in next_nodes) == row["successors"]
            visible = json.dumps(snapshot)
            assert not any(secret in visible for secret in ("NormalEventHistory", "NormalEventIndex", "UnknownRoomOdds", "event_rng"))
            results.append({"seed": row["seed"], "branch": row["branch"], "status": "passed", "actions": len(frames),
                "entry_hp": before_hp, "reference_prefix_hp": row["before"]["hp"], "hp": field(find_entity(snapshot, "Player"), "Hp"),
                "gold": field(find_entity(snapshot, "Player"), "Gold")})
            print("ok", row["seed"], row["branch"], "real public map return", flush=True)
            client.close_session(session)
        finally:
            (output / (row["seed"] + "-" + row["branch"] + ".json")).write_text(json.dumps(frames, ensure_ascii=False), encoding="utf-8")
            client.shutdown()
    (output / "result.json").write_text(json.dumps({"format": "normal-event-deck-public@1", "status": "passed", "cases": results}, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host-dll", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    replay(args.host_dll, args.output)
