"""Event consumer discriminators. Reuses public shop/map/decision fixtures; no Host builds."""
import unittest

from event_policy import choose_event
from run_game import FixedOrderPolicy, Stop
from test_shop_public import entity, candidate, shop_snapshot, shop_policy


def option(cid, action):
    return {"candidate_id": cid, "action_type": action if ":" in action else
            "net:action:base-spire:" + action, "fields": []}


def event_snapshot(event_id="Probe", legal=(), phase=8, observation_id=10):
    snap = shop_snapshot(legal=legal, phase=phase, observation_id=observation_id)
    snap["entities"][0]["fields"].append({"field_id": "net:field:base-spire:Run.CurrentEventId",
                                          "state": "known", "value": event_id})
    return snap


class EventPolicyChecks(unittest.TestCase):
    def test_different_event_ids_and_unprefixed_unknown_options_share_policy(self):
        # Catch per-event mappings, unsupported-option filtering, and ambient selection.
        legal = [candidate("ambient", "DiscardPotion", "Potion", "p"),
                 option("z", "ZZUnsupportedChoice"), option("a", "AAUnknownChoice")]
        for event_id in ("Probe", "EntirelyDifferentEvent"):
            with self.subTest(event_id=event_id):
                result = choose_event(event_snapshot(event_id, legal))
                self.assertEqual(result["candidate"], legal[2])
                self.assertEqual(result["evidence"]["ordered_options"], ["a", "z"])
                self.assertEqual(len(result["evidence"]["offered"]), 3)

    def test_explicit_current_choice_full_id_or_unique_suffix(self):
        legal = [option("a", "ProbeFirst"), option("b", "ProbeSecond")]
        for text in ("net:action:base-spire:ProbeSecond", "ProbeSecond", "Second"):
            result = choose_event(event_snapshot(legal=legal), text)
            self.assertIs(result["candidate"], legal[1])

    def test_missing_choice_never_uses_old_candidate_or_first_replacement(self):
        policy = shop_policy(event_choice="Second")
        old = event_snapshot(legal=[option("old", "ProbeSecond")])
        self.assertEqual(policy.choose(old).candidate["candidate_id"], "old")
        fresh = event_snapshot("Other", [option("new", "OtherFirst")], observation_id=11)
        choice = policy.choose(fresh)
        self.assertIsInstance(choice, Stop)
        self.assertEqual(choice.reason, "event_choice_not_found")
        self.assertIn("new", choice.detail)

    def test_ambiguous_suffix_or_parameterized_candidates_stop(self):
        for legal, text in (([option("a", "ProbeAccept"), option("b", "OtherAccept")], "Accept"),
                            ([option("a", "ProbeAccept"), option("b", "ProbeAccept")], "ProbeAccept")):
            self.assertEqual(choose_event(event_snapshot(legal=legal), text)["stop"],
                             "event_choice_ambiguous")

    def test_non_event_room_action_cannot_be_consumed_as_event(self):
        for action in ("ChooseMapPoint", "PlayCard", "LeaveShop", "TakeRewardCard"):
            snap = event_snapshot(legal=[option("event", "UnknownChoice"), option("wrong", action)])
            result = choose_event(snap)
            self.assertEqual(result["stop"], "event_candidate_domain_mismatch")

    def test_inconsistent_event_access_does_not_guess(self):
        for phase, event_id in ((8, ""), (2, "Probe")):
            self.assertEqual(choose_event(event_snapshot(event_id, [option("a", "Choice")], phase))["stop"],
                             "event_access_inconsistent")
        snap = event_snapshot(legal=[option("a", "Choice")])
        snap["entities"][0]["fields"][-1]["state"] = "unknown"
        self.assertEqual(choose_event(snap)["stop"], "event_access_inconsistent")

    def test_event_close_resumes_map_and_does_not_linger_on_actual_room_type(self):
        policy = shop_policy()
        opened = event_snapshot(legal=[option("event", "AnyChoice")])
        policy.choose(opened)
        node = entity("node", "MapNode", {"Type": "Unknown"})
        closed = event_snapshot("", [candidate("map", "ChooseMapPoint", "Node", "node")], phase=2)
        closed["entities"].append(node)
        closed["entities"][0]["fields"].append({"field_id": "net:field:base-spire:Run.ActualRoomType",
                                               "state": "known", "value": "Event"})
        result = policy.choose(closed)
        self.assertEqual(result.candidate["candidate_id"], "map")
        self.assertEqual([r["event"] for r in policy.event_events],
                         ["event_enter", "event_option", "event_exit"])
        policy.choose(closed)
        self.assertEqual(sum(r["event"] == "event_exit" for r in policy.event_events), 1)

    def test_event_pending_decision_uses_existing_generic_current_path(self):
        snap = event_snapshot(legal=[candidate("answer", "net:action:net.answer_decision", "Instance", "card")])
        self.assertEqual(shop_policy(event_choice="Absent").choose(snap).candidate["candidate_id"], "answer")

    def test_unknown_and_elite_submit_but_ancient_stays_client_boundary(self):
        for kind in ("Unknown", "Elite", "Ancient"):
            snap = event_snapshot("", [candidate("map", "ChooseMapPoint", "Node", "node")], phase=2)
            snap["entities"].append(entity("node", "MapNode", {"Type": kind}))
            result = shop_policy().choose(snap)
            if kind == "Ancient":
                self.assertIsInstance(result, Stop)
                self.assertEqual(result.reason, "unsupported_map_room")
            else:
                self.assertEqual(result.candidate["candidate_id"], "map")

    def test_shop_visit_flags_do_not_leak_across_event_and_second_shop(self):
        policy = shop_policy(shop_policy="buy")
        first = shop_snapshot(legal=[option("leave", "LeaveShop")])
        policy.choose(first)
        policy.shop_bought = True
        policy.shop_removed = True
        policy.choose(event_snapshot(legal=[option("event", "AnyChoice")]))
        # Leaving the first shop through another room closes all visit-local state.
        second = shop_snapshot(legal=[option("leave2", "LeaveShop")], observation_id=12)
        policy.choose(second)
        self.assertFalse(policy.shop_bought)
        self.assertFalse(policy.shop_removed)
        self.assertEqual(sum(e["event"] == "shop_buy_unavailable" for e in policy.events), 2)

    def test_frontier_remains_non_victory_boundary(self):
        snap = event_snapshot("", [option("next", "ContinueToNextAct")], phase=6)
        self.assertEqual(shop_policy().choose(snap).reason, "act_completed_dev_boundary")

    def test_multiact_plan_continues_without_frontier_probe(self):
        snap = event_snapshot("", [option("next", "ContinueToNextAct")], phase=6)
        run = snap["entities"][0]
        run["fields"].extend([
            {"field_id": "net:field:base-spire:Run.CurrentActIndex", "state": "known", "value": "0"},
            {"field_id": "net:field:base-spire:Run.ActCount", "state": "known", "value": "3"},
        ])
        self.assertEqual(shop_policy().choose(snap).action, "ContinueToNextAct")
        run["fields"][-2]["value"] = "2"
        self.assertEqual(shop_policy().choose(snap).reason, "act_completed_dev_boundary")
        self.assertEqual(shop_policy(step_frontier=True).choose(snap).candidate["candidate_id"], "next")


if __name__ == "__main__":
    unittest.main()
