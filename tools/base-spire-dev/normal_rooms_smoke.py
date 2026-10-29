"""Replay fixed seed-777 room routes using only public observations and current candidates."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "net-the-spire/client"))
from net_client import NetClient
from public_snapshot import action_name, candidates, entities_of, entity_of, field, find_entity, first_field_value, pile_visible

# Fixed after route discovery; no hidden state, fixture injection or seed substitution.
ROUTE = [(0, 1), (6, 2), (6, 3), (0, 4), (3, 5), (2, 6), (2, 7), (2, 8), (1, 9)]
ROOM_NAMES = {"Shop": "shop", "RestSite": "rest-site", "Treasure": "treasure"}


def replay(host: Path, output: Path, goal: str) -> None:
    frames = []
    visited = False
    bought = False
    client = NetClient(["dotnet", str(host.resolve())], cwd=ROOT)
    try:
        description = client.describe()["result"]
        started = client.start(description["identity"], description["capabilities"]["scenario_profiles"][0]["id"], seed="777")
        if not started["ok"]:
            raise RuntimeError(started)
        session = started["result"]["session_id"]
        snapshot = started["result"]["snapshot"]
        for step in range(300):
            by_name = {}
            for candidate in candidates(snapshot):
                by_name.setdefault(action_name(candidate), []).append(candidate)
            entities = entities_of(snapshot)
            player = find_entity(snapshot, "Player")
            room = field(player, "Room")
            visited |= room == ROOM_NAMES[goal]
            before_purchase = None
            if "StartNormalRun" in by_name:
                action = by_name["StartNormalRun"][0]
            elif "net.answer_decision" in by_name:
                answers = by_name["net.answer_decision"]
                options = [a for a in answers if entity_of(snapshot, first_field_value(a))]
                if options:
                    action = next(a for a in options if field(entity_of(snapshot, first_field_value(a)), "RelicId") == "base-spire:winged-boots")
                else:
                    action = answers[0]
            elif "RestAtDevelopmentCampfire" in by_name:
                action = by_name["RestAtDevelopmentCampfire"][0]
            elif "LeaveDevelopmentRoom" in by_name:
                action = by_name["LeaveDevelopmentRoom"][0]
            elif goal == "Shop" and not bought and "BuyMerchantStock" in by_name:
                action = next(a for a in by_name["BuyMerchantStock"] if field(entity_of(snapshot, first_field_value(a)), "Kind") == "card")
                stock = entity_of(snapshot, first_field_value(action))
                before_purchase = (field(player, "Gold"), field(stock, "Price"), len(pile_visible(snapshot, "RunDeckView")))
            elif "LeaveNormalItemRoom" in by_name:
                action = by_name["LeaveNormalItemRoom"][0]
            elif "ChooseMapPoint" in by_name:
                if visited:
                    assert room == "map"
                    assert goal != "Shop" or bought
                    break
                action = next(a for a in by_name["ChooseMapPoint"] if (field(entities[first_field_value(a)], "Col"), field(entities[first_field_value(a)], "Row")) in ROUTE)
            elif "ClaimDevelopmentGoldReward" in by_name:
                action = by_name["ClaimDevelopmentGoldReward"][0]
            elif "CloseDevelopmentRewards" in by_name:
                action = by_name["CloseDevelopmentRewards"][0]
            else:
                action = next(by_name[name][0] for name in ("PlayAttackFromHand", "PlayCardFromHand", "EndTurn") if name in by_name)
            response = client.step(session, snapshot["observation_id"], action["candidate_id"])
            frames.append(response)
            if not response["ok"]:
                raise RuntimeError(response)
            snapshot = response["result"]["snapshot"]
            if before_purchase:
                gold, price, count = before_purchase
                assert field(find_entity(snapshot, "Player"), "Gold") == gold - price
                assert len(pile_visible(snapshot, "RunDeckView")) == count + 1
                bought = True
        else:
            raise RuntimeError("Fixed route did not reach its expected room within 300 public actions")
        client.close_session(session)
        print(f"Normal public {goal}: {step} actions, room visited and returned to map")
    finally:
        output.mkdir(parents=True, exist_ok=True)
        (output / f"{goal}-public.json").write_text(json.dumps(frames), encoding="utf-8")
        client.shutdown()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host-dll", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--room", choices=list(ROOM_NAMES), required=True)
    args = parser.parse_args()
    replay(args.host_dll, args.output, args.room)
