"""公开动作语义的记录/重放（同一固定 Host、scope、seed 的跨会话可复放）。

本模块只读公开 NDJSON 响应（``describe``/``start``/``step`` 的 snapshot），不访问 Engine
状态、不调用随机数、不自己结算玩法。它把"提交哪个候选"从**会话绑定的字面** ``candidate_id``
换成**公开动作语义**：

    语义 = {action_type, fields}，fields 按 field_id 排序；
    每个字段的 key 是会话无关的规范值：
      * 已知公开实例引用（值等于当前公开实体的 instance_id）→ 公开身份摘要 + 公开集合定位；
      * 其它已知标量 → 规范化 JSON；
      * 非 known 状态 → 状态标记本身。

公开身份摘要取该实例**当前公开**的 ``kind_id``/``content_id``/全部 known 字段；公开集合定位取该实例
在公开 ``sequences``（按元素下标）、完整 ``piles``（按 ``visible_members`` 下标）或 ``relations``
（按 source/target 与下标）中的首个出现。两者都只来自同一份公开快照，因此同一构建/seed 的两个独立
会话会得到**逐字相同的语义 key**，而字面 ``candidate_id`` 与公开 ``instance_id`` 都不会进入 key。

重放时每步在实时 ``legal_actions`` 里找 key 完全一致的候选：恰好一个才提交它**当前的**
``candidate_id`` + ``observation_id``；零个、多个或身份不符一律带首差停止，绝不退化成"取第一个"。
"""

from __future__ import annotations

import hashlib
import json
from typing import Any, Optional

SCHEMA = "base-spire-dev/semantic-actions@1"

#: 旧夹具字段：只记字面 candidate_id 的输入在句柄按会话派生后不可重放。
LEGACY_FIXTURE_FIELDS = ("candidate_ids",)

#: 重放/记录的一致失败码（首差分类）。
MISSING = "missing"
AMBIGUOUS = "ambiguous"
IDENTITY_MISMATCH = "identity_mismatch"
MATCHED = "matched"


class SemanticsError(RuntimeError):
    """语义记录/重放本身的错误（用法、schema、输入形态）。"""


def short(name: Optional[str]) -> str:
    return (name or "").rsplit(":", 1)[-1]


def _coerce(value: Any) -> Any:
    if isinstance(value, str) and value.lstrip("-").isdigit():
        return int(value)
    return value


def known_fields(entity: Optional[dict]) -> dict:
    """公开实体的全部 known 字段（短名 → 值，十进制字符串按整数）。"""
    out: dict = {}
    for f in (entity or {}).get("fields") or []:
        if f.get("state") == "known":
            out[short(f.get("field_id"))] = _coerce(f.get("value"))
    return out


def _canonical_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def _digest(payload: Any) -> str:
    return hashlib.sha256(_canonical_json(payload).encode("utf-8")).hexdigest()[:16]


def _locator_repr(locator: Optional[dict]) -> str:
    if not locator:
        return "none"
    if locator["source"] == "sequence":
        return f"sequence:{locator['type_id']}#{locator['index']}"
    if locator["source"] == "pile":
        return f"pile:{locator['type_id']}#{locator['index']}"
    return f"relation:{locator['type_id']}:{locator['role']}#{locator['index']}"


def build_entity_index(snapshot: dict) -> dict:
    """把一份公开快照摊成 instance_id → {kind_id, content_id, fields, locator, digest}。

    定位只取公开来源：``sequences`` 元素、``members_complete`` 的 ``piles.visible_members``、
    ``relations`` 的 source/target。不完整牌堆只发布计数，其成员不作为定位来源。
    """
    index: dict = {}
    for entity in snapshot.get("entities") or []:
        instance = entity.get("instance_id")
        if not instance:
            continue
        index[instance] = {
            "instance_id": instance,
            "kind_id": entity.get("kind_id"),
            "content_id": entity.get("content_id") or "",
            "fields": known_fields(entity),
            "locator": None,
        }

    def place(instance: Any, locator: dict) -> None:
        entry = index.get(instance)
        if entry is not None and entry["locator"] is None:
            entry["locator"] = locator

    for sequence in snapshot.get("sequences") or []:
        for position, member in enumerate(sequence.get("elements") or []):
            place(member, {"source": "sequence", "type_id": sequence.get("type_id"), "index": position})
    for pile in snapshot.get("piles") or []:
        if not pile.get("members_complete"):
            continue
        kind_id = (index.get(pile.get("pile_ref")) or {}).get("kind_id")
        for position, member in enumerate(pile.get("visible_members") or []):
            place(member, {"source": "pile", "type_id": kind_id, "index": position})
    for relation in snapshot.get("relations") or []:
        place(relation.get("source_ref"), {"source": "relation", "type_id": relation.get("type_id"),
                                          "role": "source", "index": 0})
        place(relation.get("target_ref"), {"source": "relation", "type_id": relation.get("type_id"),
                                          "role": "target", "index": 0})

    for entry in index.values():
        entry["digest"] = _digest({"kind_id": entry["kind_id"], "content_id": entry["content_id"],
                                   "fields": entry["fields"]})
    return index


def canonical_value(value: Any, index: dict) -> str:
    """会话无关的规范值：公开实例引用 → 身份摘要 + 公开定位；其它值 → 规范化 JSON。"""
    if isinstance(value, str) and value in index:
        entry = index[value]
        return f"entity:{entry['digest']}@{_locator_repr(entry['locator'])}"
    return "value:" + _canonical_json(value)


def canonical_field(field: dict, index: dict) -> dict:
    """一个候选字段的记录形态：key 参与匹配，raw/entity 只作原始帧证据与身份核对。"""
    state = field.get("state")
    raw = field.get("value")
    record = {"field_id": field.get("field_id"), "state": state, "key": canonical_value(raw, index),
              "raw": raw}
    if state != "known":
        record["key"] = "state:" + str(state)
        record["raw"] = None
        return record
    entry = index.get(raw) if isinstance(raw, str) else None
    if entry is not None:
        record["entity"] = {"instance_id": entry["instance_id"], "kind_id": entry["kind_id"],
                            "content_id": entry["content_id"], "fields": entry["fields"],
                            "locator": entry["locator"], "digest": entry["digest"]}
    return record


def action_semantics(candidate: dict, index: dict) -> dict:
    """`{action_type, fields}`：fields 按 field_id 排序（同 field_id 保持公开顺序）。"""
    fields = sorted((canonical_field(f, index) for f in candidate.get("fields") or []),
                    key=lambda f: f["field_id"] or "")
    return {"action_type": candidate.get("action_type"), "fields": fields}


def semantics_key(semantics: dict) -> tuple:
    return (semantics["action_type"],
            tuple((f["field_id"], f["state"], f["key"]) for f in semantics["fields"]))


def _non_ref_keys(semantics: dict) -> tuple:
    return tuple((f["field_id"], f["state"], f["key"]) for f in semantics["fields"] if "entity" not in f)


def _ref_raws(semantics: dict) -> tuple:
    return tuple((f["field_id"], f["raw"]) for f in semantics["fields"] if "entity" in f)


def match_candidates(snapshot: dict, semantics: dict, index: Optional[dict] = None) -> dict:
    """在实时快照里为记录语义找**唯一**候选。

    返回 ``{"status", "candidate", "detail", "offer", "refs"}``：
      * ``matched``：恰好一个 key 一致，且每个引用字段的公开身份/定位与记录一致；
      * ``ambiguous``：多于一个 key 一致的候选（记录语义不足以唯一定位）；
      * ``identity_mismatch``：存在 action_type 与非引用字段都一致、且字面引用值也相同的候选，
        但该引用当前的公开身份或定位与记录不同（旧句柄指向了别的东西）；
      * ``missing``：没有任何候选匹配。
    ``offer`` 永远列出实时全部候选的 key/原始值（首差证据），不用于挑选。
    """
    live_index = index if index is not None else build_entity_index(snapshot)
    offered = snapshot.get("legal_actions") or []
    live = [(action_semantics(candidate, live_index), candidate) for candidate in offered]
    want = semantics_key(semantics)
    matches = [(semantics_, candidate) for semantics_, candidate in live if semantics_key(semantics_) == want]
    offer = [{"candidate_id": candidate.get("candidate_id"), "action_type": semantics_["action_type"],
              "fields": [{"field_id": f["field_id"], "state": f["state"], "key": f["key"], "raw": f["raw"]}
                         for f in semantics_["fields"]]}
             for semantics_, candidate in live]

    if len(matches) == 1:
        semantics_live, candidate = matches[0]
        by_id: dict = {}
        for live_field in semantics_live["fields"]:
            by_id.setdefault(live_field["field_id"], []).append(live_field)
        refs = []
        for want_field in semantics["fields"]:
            if "entity" not in want_field:
                continue
            live_field = by_id[want_field["field_id"]].pop(0)
            entry = live_index.get(live_field["raw"])
            refs.append({"field_id": want_field["field_id"], "recorded_raw": want_field["raw"],
                         "live_raw": live_field["raw"],
                         "identity_match": bool(entry and entry["digest"] == want_field["entity"]["digest"]
                                                and _locator_repr(entry["locator"])
                                                == _locator_repr(want_field["entity"]["locator"]))})
        mismatch = [r for r in refs if not r["identity_match"]]
        if mismatch:
            return {"status": IDENTITY_MISMATCH, "candidate": None, "offer": offer, "refs": refs,
                    "detail": f"匹配候选的公开引用身份与记录不一致：{mismatch}"}
        return {"status": MATCHED, "candidate": candidate, "offer": offer, "refs": refs,
                "detail": "唯一候选，引用身份一致"}

    if len(matches) > 1:
        return {"status": AMBIGUOUS, "candidate": None, "offer": offer, "refs": [],
                "detail": f"{len(matches)} 个候选语义相同，记录不足以唯一定位"}

    # 零匹配：区分"记录的字面引用还在、但公开身份已变"与"根本没有这个动作"。
    wanted_non_ref = _non_ref_keys(semantics)
    wanted_ref_raws = _ref_raws(semantics)
    for semantics_live, _candidate in live:
        if semantics_live["action_type"] != semantics["action_type"]:
            continue
        if _non_ref_keys(semantics_live) != wanted_non_ref or _ref_raws(semantics_live) != wanted_ref_raws:
            continue
        return {"status": IDENTITY_MISMATCH, "candidate": None, "offer": offer, "refs": [],
                "detail": "记录的字面引用在当前快照仍存在，但其公开身份/定位已改变；不按原始值继续"}
    return {"status": MISSING, "candidate": None, "offer": offer, "refs": [],
            "detail": "实时 legal_actions 中没有匹配的记录语义"}


def entity_of(snapshot: dict, ref: Any) -> Optional[dict]:
    if not isinstance(ref, str):
        return None
    for entity in snapshot.get("entities") or []:
        if entity.get("instance_id") == ref:
            return entity
    return None


def find_entity(snapshot: dict, kind_suffix: str) -> Optional[dict]:
    for entity in snapshot.get("entities") or []:
        kind = entity.get("kind_id") or ""
        if kind == kind_suffix or kind.endswith(":" + kind_suffix):
            return entity
    return None


def sequence(snapshot: dict, type_suffix: str) -> list:
    for item in snapshot.get("sequences") or []:
        type_id = item.get("type_id") or ""
        if type_id == type_suffix or type_id.endswith(":" + type_suffix):
            return list(item.get("elements") or [])
    return []


def pile_visible(snapshot: dict, kind_suffix: str) -> list:
    index = build_entity_index(snapshot)
    for pile in snapshot.get("piles") or []:
        kind = (index.get(pile.get("pile_ref")) or {}).get("kind_id") or ""
        if kind == kind_suffix or kind.endswith(":" + kind_suffix):
            return list(pile.get("visible_members") or [])
    return []


def public_facts(snapshot: dict, index: Optional[dict] = None) -> dict:
    """公开游戏事实（会话无关）：用于两次独立会话"公开事实一致"的逐步对照。

    run/player 的每个 known 字段都按同一规范值处理（引用字段也变身份摘要），集合成员用身份摘要，
    不含 ``instance_id`` 与 ``candidate_id``，因此同一构建/seed 的两次独立会话应逐字相同。
    """
    live_index = index if index is not None else build_entity_index(snapshot)

    def fields_of(kind_suffix: str) -> dict:
        entity = find_entity(snapshot, kind_suffix)
        if entity is None:
            return {}
        return {name: canonical_value(value, live_index)
                for name, value in known_fields(entity).items()}

    members = lambda refs: [canonical_value(ref, live_index) for ref in refs]  # noqa: E731
    piles = []
    for pile in snapshot.get("piles") or []:
        kind = (live_index.get(pile.get("pile_ref")) or {}).get("kind_id")
        piles.append({"kind": kind, "members_complete": bool(pile.get("members_complete")),
                      "total_count": pile.get("total_count"),
                      "visible": members(pile.get("visible_members") or [])})
    return {
        "observation_id": snapshot.get("observation_id"),
        "status": snapshot.get("status"),
        "terminal_result": snapshot.get("terminal_result"),
        "run": fields_of("Run"),
        "player": fields_of("Player"),
        "monsters": members(sequence(snapshot, "Monsters")),
        "hand": members(pile_visible(snapshot, "HandPile")),
        "potion_belt": members(sequence(snapshot, "PotionBelt")),
        "reward_cards": members(sequence(snapshot, "RewardCards")),
        "reward_potions": members(sequence(snapshot, "RewardPotions")),
        "piles": piles,
    }


def diff_facts(recorded: dict, live: dict) -> list:
    """列出公开事实的第一批差异（供首差报告），不修改任何输入。"""
    differences = []
    for key in sorted(set(recorded) | set(live)):
        left, right = recorded.get(key), live.get(key)
        if left == right:
            continue
        if isinstance(left, dict) and isinstance(right, dict):
            for sub in sorted(set(left) | set(right)):
                if left.get(sub) != right.get(sub):
                    differences.append({"path": f"{key}.{sub}", "recorded": left.get(sub), "live": right.get(sub)})
        else:
            differences.append({"path": key, "recorded": left, "live": right})
    return differences


# -- 轨迹提取（记录模式：只从公开轨迹读，不自己找路线） ----------------------------------------------------


def _jsonl(path) -> list:
    import pathlib

    text = pathlib.Path(path).read_text(encoding="utf-8-sig")
    return [json.loads(line) for line in text.splitlines() if line.strip()]


def extract_from_frames(frames_path, *, scope: str, trajectory_sha256: Optional[str] = None) -> dict:
    """从 ``run_game.py`` 的 ``frames.ndjson``（全部双向原始帧）提取语义动作。

    预处理：客户端 ``step`` 帧发出时以"当时最后一份公开快照"为记录基准；宿主响应带回新快照。
    被宿主拒绝的 step（无快照、``ok=false``）单独记为 ``known_fault``，不算作已重放动作。
    """
    actions = []
    pending: dict = {}
    last_snapshot: Optional[dict] = None
    identity: Optional[dict] = None
    seed: Optional[str] = None
    session_id: Optional[str] = None
    scenario_profile: Optional[str] = None
    rejected: Optional[dict] = None
    initial_facts: Optional[dict] = None

    for entry in _jsonl(frames_path):
        frame = entry.get("frame") or {}
        if entry.get("dir") == "client->host":
            operation = frame.get("operation")
            payload = frame.get("payload") or {}
            if operation == "start":
                identity = payload.get("expected_identity")
                seed = payload.get("seed")
                scenario_profile = payload.get("scenario_profile")
            elif operation == "step":
                if last_snapshot is None:
                    raise SemanticsError("step 之前没有可用的公开快照，无法建立语义基准")
                index = build_entity_index(last_snapshot)
                wanted = payload.get("candidate_id")
                offered = [c for c in last_snapshot.get("legal_actions") or []
                           if c.get("candidate_id") == wanted]
                if len(offered) != 1:
                    raise SemanticsError(
                        f"轨迹自相矛盾：step 的 candidate_id {wanted!r} 在当步公开候选里出现 {len(offered)} 次")
                pending[frame.get("request_id")] = {
                    "semantics": action_semantics(offered[0], index),
                    "recorded": {"candidate_id": wanted,
                                 "expected_observation_id": payload.get("expected_observation_id")},
                    "pre_facts": public_facts(last_snapshot, index),
                }
            continue

        response = frame
        request_id = response.get("request_id")
        result = response.get("result") or {}
        snapshot = result.get("snapshot") if isinstance(result, dict) else None
        if isinstance(snapshot, dict):
            last_snapshot = snapshot
            session_id = snapshot.get("session_id") or session_id
            if initial_facts is None:
                initial_facts = public_facts(snapshot)
        pending_step = pending.pop(request_id, None)
        if pending_step is None:
            continue
        if not response.get("ok"):
            error = response.get("error") or {}
            rejected = {**pending_step["semantics"], "recorded": pending_step["recorded"],
                        "error": error, "next_actions_submitted": 0}
            continue
        if not isinstance(snapshot, dict):
            raise SemanticsError("step 成功响应没有公开快照，无法记录该步公开事实")
        actions.append({**pending_step["semantics"], "recorded": pending_step["recorded"],
                        "facts": public_facts(snapshot)})

    if not actions:
        raise SemanticsError(f"轨迹里没有可提取的公开动作：{frames_path}")
    for number, action in enumerate(actions):
        action["seq"] = number
    return {"identity": identity, "seed": seed, "scope": scope, "scenario_profile": scenario_profile,
            "session_id": session_id, "actions": actions, "known_fault": rejected,
            "initial_facts": initial_facts, "trajectory_sha256": trajectory_sha256}


def extract_from_smoke_trajectory(trajectory_path, *, scope: str,
                                  trajectory_sha256: Optional[str] = None) -> dict:
    """从旧 ``act1_smoke.py`` 的 ``public-trajectory.jsonl`` 提取语义动作（迁移入口）。

    该格式首行是 ``{describe, start}``，其后每行是 ``{instruction, response}``；语义基准取该步
    **之前**的快照（首步即 start 快照）。旧记录里的候选是当时会话的字面句柄，只作证据，不参与重放。
    """
    entries = _jsonl(trajectory_path)
    if not entries or "start" not in (entries[0] or {}):
        raise SemanticsError(f"不是旧冒烟轨迹格式（首行缺少 start）：{trajectory_path}")
    started = entries[0]["start"]
    if not started.get("ok"):
        raise SemanticsError("旧轨迹的 start 未成功，无法提取语义")
    snapshot = started["result"]["snapshot"]
    identity = entries[0].get("describe", {}).get("result", {}).get("identity")
    actions = []
    rejected = None
    initial_facts = public_facts(snapshot)
    session_id = started["result"].get("session_id")
    for entry in entries[1:]:
        index = build_entity_index(snapshot)
        instruction = entry.get("instruction") or {}
        recorded = [c for c in snapshot.get("legal_actions") or []
                    if c.get("action_type") == instruction.get("action")
                    and [(f["field_id"], f["value"]) for f in c.get("fields") or []]
                    == [(f["field_id"], f["value"]) for f in (instruction.get("fields") or [])]]
        if len(recorded) != 1:
            raise SemanticsError(f"旧轨迹自相矛盾：instruction 在当步公开候选里出现 {len(recorded)} 次")
        response = entry.get("response") or {}
        expected_observation_id = snapshot.get("observation_id")
        if not response.get("ok"):
            rejected = {**action_semantics(recorded[0], index),
                        "recorded": {"candidate_id": recorded[0].get("candidate_id"),
                                     "expected_observation_id": expected_observation_id},
                        "error": response.get("error") or {}, "next_actions_submitted": 0}
            continue
        snapshot = response["result"].get("snapshot", response["result"])
        actions.append({**action_semantics(recorded[0], index),
                        "recorded": {"candidate_id": recorded[0].get("candidate_id"),
                                     "expected_observation_id": expected_observation_id},
                        "facts": public_facts(snapshot)})
    if not actions:
        raise SemanticsError(f"轨迹里没有可提取的公开动作（只有开头或全部被拒绝）：{trajectory_path}")
    for number, action in enumerate(actions):
        action["seq"] = number
    return {"identity": identity, "seed": started["result"].get("seed"), "scope": scope,
            "scenario_profile": None, "session_id": session_id, "actions": actions,
            "known_fault": rejected, "initial_facts": initial_facts,
            "trajectory_sha256": trajectory_sha256}


def extract(trajectory_path, *, scope: str) -> dict:
    """按内容自动识别轨迹格式并提取语义动作。"""
    import pathlib

    path = pathlib.Path(trajectory_path)
    if not path.is_file():
        raise SemanticsError(f"轨迹文件不存在：{path}")
    sha256 = hashlib.sha256(path.read_bytes()).hexdigest()
    first = _jsonl(path)[0]
    if "frame" in first:
        return extract_from_frames(path, scope=scope, trajectory_sha256=sha256)
    return extract_from_smoke_trajectory(path, scope=scope, trajectory_sha256=sha256)
