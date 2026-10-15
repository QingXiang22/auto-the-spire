"""Public snapshot access shared by clients. Reads only published fields and members.

Names passed as suffixes refer to the selected base-spire profile.
For multiple mods, pass the qualified id rather than an ambiguous local name.
"""

from __future__ import annotations

from typing import Any, Optional


def _coerce(value: Any) -> Any:
    if isinstance(value, str) and value.lstrip("-").isdigit():
        return int(value)
    return value


def field(entity: Optional[dict], name: str) -> Any:
    """读公开字段（`state == "known"`）；十进制字符串按整数返回，与 act1_smoke 同规则。"""
    if not entity:
        return None
    for f in entity.get("fields") or []:
        fid = f.get("field_id") or ""
        if fid.endswith("." + name) and f.get("state") == "known":
            return _coerce(f.get("value"))
    return None


def public_fields(entity: Optional[dict]) -> dict:
    """把实体的**全部**公开字段摊成 `{短名: 值}`（未 known 的保留 state）。"""
    out: dict = {}
    if not entity:
        return out
    for f in entity.get("fields") or []:
        name = (f.get("field_id") or "").rsplit(".", 1)[-1]
        if f.get("state") == "known":
            out[name] = _coerce(f.get("value"))
        else:
            out[name] = {"state": f.get("state")}
    return out


def flag(entity: Optional[dict], name: str) -> bool:
    """公开布尔字段：`true` / `"true"` 都算真。"""
    value = field(entity, name)
    return value is True or (isinstance(value, str) and value.lower() == "true")


def short(name: Optional[str]) -> str:
    return (name or "").split(":")[-1]


def action_name(candidate: dict) -> str:
    return short(candidate.get("action_type"))


def candidate_fields(candidate: dict) -> list:
    return [{"field_id": f.get("field_id"), "name": short(f.get("field_id")), "state": f.get("state"),
             "value": f.get("value")} for f in candidate.get("fields") or []]


def first_field_value(candidate: dict) -> Any:
    for f in candidate.get("fields") or []:
        if f.get("state") == "known":
            return f.get("value")
    return None


def entities_of(snapshot: dict) -> dict:
    return {e["instance_id"]: e for e in snapshot.get("entities") or []}


def _matches(identifier: Optional[str], name: str) -> bool:
    return identifier == name if ":" in name else (identifier or "").endswith(":" + name)


def entity_of(snapshot: dict, ref: Any) -> Optional[dict]:
    return entities_of(snapshot).get(ref) if isinstance(ref, str) else None


def find_entity(snapshot: dict, kind_suffix: str) -> Optional[dict]:
    for e in snapshot.get("entities") or []:
        if _matches(e.get("kind_id"), kind_suffix):
            return e
    return None


def sequence(snapshot: dict, type_suffix: str) -> list:
    for s in snapshot.get("sequences") or []:
        if _matches(s.get("type_id"), type_suffix):
            return list(s.get("elements") or [])
    return []


def pile_visible(snapshot: dict, kind_suffix: str) -> list:
    """公开可见的牌堆成员（抽牌堆这类 `members_complete=false` 的堆只会给出空列表）。"""
    entities = entities_of(snapshot)
    for p in snapshot.get("piles") or []:
        pile = entities.get(p.get("pile_ref"))
        if pile and _matches(pile.get("kind_id"), kind_suffix):
            return list(p.get("visible_members") or [])
    return []


def all_known(candidate: dict) -> bool:
    return all((f.get("state") == "known") for f in candidate.get("fields") or [])


def candidates(snapshot: dict, action: Optional[str] = None) -> list:
    """Return current public candidates in host order; never reuse them across snapshots."""
    offered = snapshot.get("legal_actions") or []
    if action is None:
        return list(offered)
    return [c for c in offered if (c.get("action_type") == action if ":" in action else action_name(c) == action)]


def relation_targets(snapshot: dict, type_suffix: str) -> dict:
    """Read published relation edges as source_ref -> ordered target_refs."""
    targets: dict = {}
    for relation in snapshot.get("relations") or []:
        if _matches(relation.get("type_id"), type_suffix):
            targets.setdefault(relation.get("source_ref"), []).append(relation.get("target_ref"))
    return targets
