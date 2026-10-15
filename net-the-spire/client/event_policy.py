"""Current public event candidates; no event-id/action-name mapping or content filtering.

The wire has no event-option ownership metadata. MapPhase=8 and a nonempty
CurrentEventId delimit the active visit. Only DiscardPotion is an explicitly
ignored ambient action; known other room/combat actions cause a named stop.
Unknown action types remain selectable and are never silently filtered out.
"""
from __future__ import annotations

import json
from public_snapshot import action_name, candidate_fields, field, find_entity

EVENT_PHASE = 8
AMBIENT_ACTIONS = {"net:action:base-spire:DiscardPotion"}
NON_EVENT_ACTIONS = {
    "ChooseMapPoint", "ContinueToNextAct", "EnterNextRoom", "PlayCard", "EndTurn", "UsePotion",
    "TakeRewardGold", "TakeRewardCard", "TakeRewardPotion", "SkipRewardCard", "SkipRewardPotion",
    "OpenTreasureChest", "TakeTreasureRelic", "SkipTreasureRelic", "RestAtCampfire",
    "SmithAtCampfire", "UpgradeLongTermCard", "BuyShopCard", "BuyShopRelic", "BuyShopPotion",
    "RemoveShopCard", "LeaveShop", "net.answer_decision",
}


def event_access(snapshot: dict) -> tuple:
    run = find_entity(snapshot, "Run")
    return field(run, "MapPhase"), field(run, "CurrentEventId")


def choose_event(snapshot: dict, preferred: str | None = None) -> dict | None:
    """Return a current candidate or a named stop; None means outside an event.

    Pending decisions are handled by the runner before this function. No prior
    observation, visit identity, or candidate is cached here.
    """
    phase, event_id = event_access(snapshot)
    active_id = isinstance(event_id, str) and bool(event_id)
    if phase != EVENT_PHASE and not active_id:
        return None
    legal = list(snapshot.get("legal_actions") or [])
    evidence = {"event_id": event_id, "phase": phase,
                "observation_id": snapshot.get("observation_id"),
                "offered": [{"action_type": c.get("action_type"),
                             "candidate_id": c.get("candidate_id"),
                             "fields": candidate_fields(c)} for c in legal]}

    def stop(reason: str) -> dict:
        return {"stop": reason, "detail": json.dumps(evidence, ensure_ascii=False)}

    if phase != EVENT_PHASE or not active_id:
        return stop("event_access_inconsistent")
    options = [c for c in legal if c.get("action_type") not in AMBIENT_ACTIONS]
    evidence["ignored_ambient"] = [c.get("action_type") for c in legal
                                   if c.get("action_type") in AMBIENT_ACTIONS]
    if any(action_name(c) in NON_EVENT_ACTIONS for c in options):
        return stop("event_candidate_domain_mismatch")
    if not options:
        return stop("event_no_options")
    if any(not isinstance(c.get("action_type"), str) or not c.get("action_type")
           or not c.get("candidate_id") for c in options):
        return stop("event_candidate_invalid")
    # Full public action identity, canonical public fields, then published order.
    ordered = sorted(enumerate(options), key=lambda row: (
        row[1]["action_type"],
        json.dumps(row[1].get("fields") or [], ensure_ascii=False, sort_keys=True), row[0]))
    if preferred is not None:
        # Qualified IDs must match exactly; unqualified text is a unique local suffix.
        matches = [c for c in options if (c["action_type"] == preferred if ":" in preferred
                                         else action_name(c).endswith(preferred))]
        evidence["preferred"] = preferred
        evidence["matches"] = [c["candidate_id"] for c in matches]
        if not matches:
            return stop("event_choice_not_found")
        if len(matches) != 1:
            return stop("event_choice_ambiguous")
        chosen = matches[0]
    else:
        chosen = ordered[0][1]
    evidence["ordered_options"] = [c["candidate_id"] for _, c in ordered]
    evidence["selected"] = chosen["candidate_id"]
    evidence["selection"] = "explicit_unique_match" if preferred is not None else "public_stable_order"
    return {"candidate": chosen, "evidence": evidence}
