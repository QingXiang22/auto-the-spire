"""从**公开快照序列**派生奖励向量（W-04 第 3 项）。

* 输入是 net 公开快照（`entities`/`fields`/`groups`/`terminal_result`）与转移计数；
  读取路径写死在下面的 `_facts_of` 里，逐条对应 :class:`RewardComponent.source` 的说明。
* **缺公开事实时的行为显式**：非 optional 分量 ⇒ 抛 :class:`RewardError`；
  optional 分量 ⇒ 记进 ``unavailable`` 且**不参与**标量化（不写 0 冒充"没发生"）。
* 标量化 = ``Σ weight[c] × scale[c] × value[c]``，并保留逐分量贡献（可审计）。
"""

from __future__ import annotations

import hashlib
import math
from dataclasses import dataclass
from typing import Any, Mapping, Optional, Sequence

from ..contracts import layers
from .profile import RewardFactPaths, RewardProfile, RewardProfileError


class RewardError(RuntimeError):
    """奖励派生失败：缺必需公开事实、输入形状不对等（调用方不得把它当成 0 奖励）。"""


@dataclass(frozen=True)
class RewardVector:
    """一次 episode 的奖励向量 + 标量化结果（含单位与逐分量贡献）。"""

    profile_id: str
    profile_digest: str
    values: Mapping[str, float]
    units: Mapping[str, str]
    contributions: Mapping[str, float]
    scalar: float
    steps: int
    unavailable: tuple = ()

    def to_wire(self) -> dict:
        """规范 wire：数值标量写成规范十进制字符串（共享 L0 不允许 wire 出现浮点字面量）。"""
        return {
            "profile_id": self.profile_id,
            "profile_digest": self.profile_digest,
            "units": dict(self.units),
            "values": {key: repr(float(self.values[key])) for key in sorted(self.values)},
            "contributions": {key: repr(float(self.contributions[key]))
                              for key in sorted(self.contributions)},
            "scalar": repr(float(self.scalar)),
            "steps": int(self.steps),
            "unavailable": list(self.unavailable),
        }

    def digest(self) -> str:
        return "sha256:" + hashlib.sha256(layers.canonical_bytes(self.to_wire())).hexdigest()


def _document_of(snapshot: Any) -> Mapping:
    if hasattr(snapshot, "to_wire"):
        return snapshot.to_wire()
    if isinstance(snapshot, Mapping):
        return snapshot
    raise RewardError(f"快照必须是对象或视图，得到 {type(snapshot).__name__}")


def _field_value(snapshot: Mapping, kind_id: str, field_id: str) -> Optional[float]:
    """取指定 kind 的实体上某个 number 字段（多实例取和；没有该实体返回 None）。"""
    total = 0.0
    found = False
    for entity in snapshot.get("entities") or []:
        if entity.get("kind_id") != kind_id:
            continue
        for field in entity.get("fields") or []:
            if field.get("field_id") != field_id or field.get("state") != "known":
                continue
            value = field.get("value")
            if value is None:
                raise RewardError(f"{field_id} 声明为 known 却没有值（快照非法）")
            try:
                total += float(value)
            except (TypeError, ValueError) as failure:
                raise RewardError(f"{field_id} 的值 {value!r} 不是规范十进制数字符串") from failure
            found = True
    return total if found else None


def _potion_count(snapshot: Mapping, group_type_id: Optional[str]) -> Optional[int]:
    if group_type_id is None:
        return None
    for group in snapshot.get("groups") or []:
        if group.get("type_id") == group_type_id:
            return len(group.get("members") or [])
    return None


def _outcome(snapshot: Mapping) -> Optional[str]:
    terminal = snapshot.get("terminal_result")
    if not isinstance(terminal, Mapping):
        return None
    return terminal.get("outcome")


def _facts_of(snapshots: Sequence[Mapping], steps: int, facts_paths: RewardFactPaths) -> dict[str, Any]:
    """把公开快照序列压成"分量派生所需的原始事实"（每项缺失就是 None）。"""
    if not snapshots:
        raise RewardError("必须给出至少一份公开快照")
    first, last = snapshots[0], snapshots[-1]
    facts: dict[str, Any] = {
        "player_hp_first": _field_value(first, facts_paths.player_kind_id,
                                        facts_paths.player_hp_field_id),
        "player_hp_last": _field_value(last, facts_paths.player_kind_id,
                                       facts_paths.player_hp_field_id),
        "monster_hp_first": _field_value(first, facts_paths.monster_kind_id,
                                         facts_paths.monster_hp_field_id),
        "monster_hp_last": _field_value(last, facts_paths.monster_kind_id,
                                        facts_paths.monster_hp_field_id),
        "turn_first": (None if facts_paths.player_turn_field_id is None else
                       _field_value(first, facts_paths.player_kind_id,
                                    facts_paths.player_turn_field_id)),
        "turn_last": (None if facts_paths.player_turn_field_id is None else
                      _field_value(last, facts_paths.player_kind_id,
                                   facts_paths.player_turn_field_id)),
        "potion_first": _potion_count(first, facts_paths.potion_belt_group_type_id),
        "potion_last": _potion_count(last, facts_paths.potion_belt_group_type_id),
        "outcome": _outcome(last),
        "steps": int(steps),
    }
    return facts


def _derive(component_id: str, facts: Mapping[str, Any]) -> Optional[float]:
    """分量 → 数值（None = 该公开事实不可用）。规则与 Profile 的 source 说明一一对应。"""
    if component_id == "net:reward:enemy_hp_delta":
        if facts["monster_hp_first"] is None or facts["monster_hp_last"] is None:
            return None
        return float(facts["monster_hp_first"]) - float(facts["monster_hp_last"])
    if component_id == "net:reward:player_hp_delta":
        if facts["player_hp_first"] is None or facts["player_hp_last"] is None:
            return None
        return float(facts["player_hp_first"]) - float(facts["player_hp_last"])
    if component_id == "net:reward:hp_lost":
        # D1 固定语义：`hp_lost = max(0, player_hp_before - player_hp_after)`（单位 HP）。
        # 与 `player_hp_delta` 的区别只在**截断到 0**：回血不得变成负的"损失"（否则它会去抵消真实受伤）。
        if facts["player_hp_first"] is None or facts["player_hp_last"] is None:
            return None
        return max(0.0, float(facts["player_hp_first"]) - float(facts["player_hp_last"]))
    if component_id == "net:reward:turn_count":
        if facts["turn_first"] is None or facts["turn_last"] is None:
            return None
        return float(facts["turn_last"]) - float(facts["turn_first"])
    if component_id == "net:reward:decision_count":
        return float(facts["steps"])
    if component_id == "net:reward:potion_spent":
        if facts["potion_first"] is None or facts["potion_last"] is None:
            return None
        return float(facts["potion_first"]) - float(facts["potion_last"])
    if component_id == "net:reward:outcome_win":
        return None if facts["outcome"] is None else (1.0 if facts["outcome"] == "win" else 0.0)
    if component_id == "net:reward:outcome_loss":
        return None if facts["outcome"] is None else (1.0 if facts["outcome"] == "loss" else 0.0)
    raise RewardError(
        f"分量 {component_id} 没有派生规则：Profile 声明了本层无法从公开事实得到的量；"
        "要么在 derive 层实现对应规则，要么改 Profile（不得静默给 0）"
    )


def scalarize(profile: RewardProfile, values: Mapping[str, float],
              contributions: Mapping[str, float]) -> float:
    """显式标量化：``Σ weight × contribution``（contribution 已含 scale）。"""
    total = 0.0
    for component_id in profile.component_ids:
        if component_id not in values:
            continue  # optional 且不可用：不参与
        total += float(profile.weights[component_id]) * float(contributions[component_id])
    if not math.isfinite(total):
        raise RewardError(f"标量化结果不是有限数：{total!r}")
    return total


def derive_episode_reward(snapshots: Sequence[Any], profile: RewardProfile, *,
                          steps: Optional[int] = None) -> RewardVector:
    """公开快照序列 → :class:`RewardVector`（含单位、逐分量贡献与标量）。

    ``steps`` 缺省取 ``len(snapshots) - 1``（转移条数）；调用方若已记录实际决策数应显式传入，
    以免把"每份快照"与"每个决策"混为一谈。
    """
    if not isinstance(profile, RewardProfile):
        raise TypeError(f"profile 必须是 RewardProfile，得到 {type(profile).__name__}")
    documents = [_document_of(snapshot) for snapshot in snapshots]
    resolved_steps = int(steps) if steps is not None else max(len(documents) - 1, 0)
    if resolved_steps < 0:
        raise RewardError(f"steps 不得为负：{resolved_steps}")
    facts = _facts_of(documents, resolved_steps, profile.facts)

    values: dict[str, float] = {}
    contributions: dict[str, float] = {}
    unavailable: list[str] = []
    for component in profile.components:
        try:
            raw = _derive(component.component_id, facts)
        except RewardProfileError as failure:  # pragma: no cover - Profile 已自检
            raise RewardError(str(failure)) from failure
        if raw is None:
            if not component.optional:
                raise RewardError(
                    f"分量 {component.component_id} 是必需的，但轨迹里缺少它所需的公开事实"
                    f"（来源说明：{component.source}）——不得用 0 冒充"
                )
            unavailable.append(component.component_id)
            continue
        values[component.component_id] = raw
        contributions[component.component_id] = raw * float(component.scale)

    scalar = scalarize(profile, values, contributions)
    return RewardVector(
        profile_id=profile.profile_id,
        profile_digest=profile.digest(),
        values=values,
        units={component.component_id: component.unit
               for component in profile.components if component.component_id in values},
        contributions=contributions,
        scalar=scalar,
        steps=resolved_steps,
        unavailable=tuple(unavailable),
    )
