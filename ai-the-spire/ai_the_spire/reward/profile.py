"""奖励 Profile：分量定义（含**单位**与公开来源）+ 显式标量化权重 + 版本与摘要。

设计约束（逐条可核对）：

* 每个分量必须声明 ``unit``（来自 :data:`STANDARD_UNITS`）、``source``（它读哪些**公开事实**）
  与 ``scale``（单位换算，显式数值）；没有来源说明的分量不接受。
* ``weights`` 必须**恰好覆盖**声明的分量（可以显式写 0.0，但不能漏）——漏一个分量会让
  "这个实验用了什么偏好"变得不可核对；出现未知分量直接拒绝。
* ``optional`` 的分量在轨迹里缺公开事实时**不参与**标量化，并被记录为 unavailable；
  非 optional 的分量缺事实则显式失败（绝不用 0 冒充"没发生"）。
"""

from __future__ import annotations

import hashlib
import math
from dataclasses import dataclass, fields
from typing import Any, Mapping, Sequence

from ..contracts import layers

#: 奖励 Profile 格式版本。
REWARD_PROFILE_VERSION = "reward-profile@1"

#: 允许的单位集合（新增单位必须显式加进来，避免"无单位数字"）。
STANDARD_UNITS = ("hp", "turns", "events", "boolean", "steps")


@dataclass(frozen=True)
class RewardFactPaths:
    """派生层要读的**公开路径**（按 Profile 声明，不把某个 Gym 的字段 id 写死在代码里）。

    每个路径都必须是目录声明过的公开 id（`net:field:...` / `net:kind:...` / group type）。
    为 None 表示"该 Gym 没有这条公开事实"：对应分量随之不可用（optional ⇒ 记 unavailable，
    必需 ⇒ 显式失败），而不是被读成 0。
    """

    player_kind_id: str
    player_hp_field_id: str
    monster_kind_id: str
    monster_hp_field_id: str
    player_turn_field_id: Optional[str] = None
    potion_belt_group_type_id: Optional[str] = None

    def __post_init__(self) -> None:
        for name in ("player_kind_id", "player_hp_field_id", "monster_kind_id",
                     "monster_hp_field_id"):
            value = getattr(self, name)
            if not isinstance(value, str) or not value.strip():
                raise RewardProfileError(f"facts.{name} 必须是非空公开 id，得到 {value!r}")
        for name in ("player_turn_field_id", "potion_belt_group_type_id"):
            value = getattr(self, name)
            if value is not None and (not isinstance(value, str) or not value.strip()):
                raise RewardProfileError(f"facts.{name} 必须是公开 id 或 null，得到 {value!r}")

    def to_wire(self) -> dict:
        return {
            "player_kind_id": self.player_kind_id,
            "player_hp_field_id": self.player_hp_field_id,
            "monster_kind_id": self.monster_kind_id,
            "monster_hp_field_id": self.monster_hp_field_id,
            "player_turn_field_id": self.player_turn_field_id,
            "potion_belt_group_type_id": self.potion_belt_group_type_id,
        }

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "RewardFactPaths":
        expected = {item.name for item in fields(cls)}
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise RewardProfileError(f"RewardFactPaths 键不匹配：缺少 {missing}，多出 {extra}")
        return cls(**{item.name: doc[item.name] for item in fields(cls)})


#: 受控切片（`base-spire:combat-slice-v1`，作者变体同形）的公开路径。
SLICE_FACT_PATHS = RewardFactPaths(
    player_kind_id="net:kind:base-spire:Player",
    player_hp_field_id="net:field:base-spire:Player.Hp",
    monster_kind_id="net:kind:base-spire:Monster",
    monster_hp_field_id="net:field:base-spire:Monster.Hp",
    player_turn_field_id="net:field:base-spire:Player.Turn",
    potion_belt_group_type_id="net:group_type:base-spire:potion_belt",
)


class RewardProfileError(ValueError):
    """奖励 Profile 非法：一律带分量/字段名与原因。"""


def _canonical_decimal(value: float) -> str:
    return repr(float(value))


@dataclass(frozen=True)
class RewardComponent:
    """一个奖励分量：唯一 id + 单位 + 公开来源说明 + 缩放 + 是否可选。"""

    component_id: str
    unit: str
    source: str
    scale: float = 1.0
    optional: bool = False
    description: str = ""

    def __post_init__(self) -> None:
        if not isinstance(self.component_id, str) or not self.component_id:
            raise RewardProfileError(f"component_id 必须是非空字符串，得到 {self.component_id!r}")
        if self.unit not in STANDARD_UNITS:
            raise RewardProfileError(
                f"{self.component_id}.unit={self.unit!r} 不是允许的单位（{STANDARD_UNITS}）"
            )
        if not isinstance(self.source, str) or not self.source.strip():
            raise RewardProfileError(f"{self.component_id}.source 必须说明它所读的公开事实")
        if isinstance(self.scale, bool) or not isinstance(self.scale, (int, float)):
            raise RewardProfileError(f"{self.component_id}.scale 必须是数值，得到 {self.scale!r}")
        if not math.isfinite(float(self.scale)) or float(self.scale) == 0.0:
            raise RewardProfileError(f"{self.component_id}.scale 必须是非零有限数值，得到 {self.scale!r}")
        if not isinstance(self.optional, bool):
            raise RewardProfileError(f"{self.component_id}.optional 必须是布尔值")

    def to_wire(self) -> dict:
        return {
            "component_id": self.component_id,
            "unit": self.unit,
            "source": self.source,
            "scale": _canonical_decimal(self.scale),
            "optional": self.optional,
            "description": self.description,
        }

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "RewardComponent":
        expected = {item.name for item in fields(cls)}
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise RewardProfileError(f"RewardComponent 键不匹配：缺少 {missing}，多出 {extra}")
        raw_scale = doc["scale"]
        if isinstance(raw_scale, bool) or not isinstance(raw_scale, (int, float, str)):
            raise RewardProfileError(f"scale 必须是数值或规范十进制字符串，得到 {raw_scale!r}")
        try:
            scale = float(raw_scale)
        except (TypeError, ValueError) as failure:
            raise RewardProfileError(f"scale 不可解析：{raw_scale!r}") from failure
        return cls(
            component_id=doc["component_id"],
            unit=doc["unit"],
            source=doc["source"],
            scale=scale,
            optional=doc["optional"],
            description=doc["description"],
        )


@dataclass(frozen=True)
class RewardProfile:
    """一份版本化的奖励/偏好定义：分量 + 权重 + 折扣。"""

    profile_id: str
    semantic_version: str
    components: Sequence[RewardComponent]
    weights: Mapping[str, float]
    discount: float = 1.0
    description: str = ""
    facts: RewardFactPaths = SLICE_FACT_PATHS

    def __post_init__(self) -> None:
        for name in ("profile_id", "semantic_version"):
            value = getattr(self, name)
            if not isinstance(value, str) or not value.strip():
                raise RewardProfileError(f"{name} 必须是非空字符串，得到 {value!r}")
        components = tuple(self.components)
        if not components:
            raise RewardProfileError("RewardProfile 至少要有一个分量（空奖励不可核对）")
        object.__setattr__(self, "components", components)
        seen: set[str] = set()
        for component in components:
            if not isinstance(component, RewardComponent):
                raise RewardProfileError(f"分量必须是 RewardComponent，得到 {type(component).__name__}")
            if component.component_id in seen:
                raise RewardProfileError(f"分量 id 重复：{component.component_id}")
            seen.add(component.component_id)
        weights = dict(self.weights)
        missing = sorted(seen - set(weights))
        unknown = sorted(set(weights) - seen)
        if missing:
            raise RewardProfileError(
                f"权重必须显式覆盖每个分量（可以写 0.0），缺少 {missing}"
            )
        if unknown:
            raise RewardProfileError(f"权重出现未声明的分量 {unknown}")
        for component_id, weight in weights.items():
            if isinstance(weight, bool) or not isinstance(weight, (int, float)):
                raise RewardProfileError(f"权重 {component_id} 必须是数值，得到 {weight!r}")
            if not math.isfinite(float(weight)):
                raise RewardProfileError(f"权重 {component_id} 必须是有限数值，得到 {weight!r}")
        object.__setattr__(self, "weights", {key: float(weights[key]) for key in weights})
        if isinstance(self.discount, bool) or not isinstance(self.discount, (int, float)):
            raise RewardProfileError(f"discount 必须是数值，得到 {self.discount!r}")
        if not math.isfinite(float(self.discount)) or not 0.0 < float(self.discount) <= 1.0:
            raise RewardProfileError(f"discount 必须在 (0, 1] 内，得到 {self.discount!r}")
        if not isinstance(self.description, str):
            raise RewardProfileError("description 必须是字符串")
        if not isinstance(self.facts, RewardFactPaths):
            raise RewardProfileError(
                f"facts 必须是 RewardFactPaths，得到 {type(self.facts).__name__}"
            )

    # -- 读取 ------------------------------------------------------------ #
    def component(self, component_id: str) -> RewardComponent:
        for item in self.components:
            if item.component_id == component_id:
                return item
        raise RewardProfileError(f"Profile {self.profile_id} 没有分量 {component_id!r}")

    @property
    def component_ids(self) -> tuple[str, ...]:
        return tuple(item.component_id for item in self.components)

    def units(self) -> dict[str, str]:
        return {item.component_id: item.unit for item in self.components}

    def scale(self, component_id: str) -> float:
        return float(self.component(component_id).scale)

    # -- 序列化 ---------------------------------------------------------- #
    def to_wire(self) -> dict:
        return {
            "reward_profile_version": REWARD_PROFILE_VERSION,
            "profile_id": self.profile_id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "discount": _canonical_decimal(self.discount),
            "facts": self.facts.to_wire(),
            "components": [item.to_wire() for item in self.components],
            "weights": {key: _canonical_decimal(self.weights[key]) for key in sorted(self.weights)},
        }

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "RewardProfile":
        expected = {
            "reward_profile_version", "profile_id", "semantic_version", "description", "discount",
            "facts", "components", "weights",
        }
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise RewardProfileError(f"RewardProfile 键不匹配：缺少 {missing}，多出 {extra}")
        if doc["reward_profile_version"] != REWARD_PROFILE_VERSION:
            raise RewardProfileError(
                f"奖励 Profile 版本 {doc['reward_profile_version']!r} 与当前 {REWARD_PROFILE_VERSION!r} 不同"
            )
        if not isinstance(doc["components"], list) or not doc["components"]:
            raise RewardProfileError("components 必须是非空数组")
        if not isinstance(doc["weights"], Mapping):
            raise RewardProfileError("weights 必须是对象")
        try:
            discount = float(doc["discount"])
        except (TypeError, ValueError) as failure:
            raise RewardProfileError(f"discount 不可解析：{doc['discount']!r}") from failure
        return cls(
            profile_id=doc["profile_id"],
            semantic_version=doc["semantic_version"],
            components=tuple(RewardComponent.from_wire(item) for item in doc["components"]),
            weights={key: float(value) for key, value in doc["weights"].items()},
            discount=discount,
            description=doc["description"],
            facts=RewardFactPaths.from_wire(doc["facts"]),
        )

    def digest(self) -> str:
        return "sha256:" + hashlib.sha256(layers.canonical_bytes(self.to_wire())).hexdigest()


# --------------------------------------------------------------------------- #
# 受控小任务的标准 Profile（**不是** W-07 的实验偏好）
# --------------------------------------------------------------------------- #
def standard_short_task_profile(weights: Mapping[str, float] | None = None, *,
                               profile_id: str = "net:reward:standard-short-task-v1",
                               semantic_version: str = "1",
                               discount: float = 1.0,
                               facts: RewardFactPaths = SLICE_FACT_PATHS) -> RewardProfile:
    """工程用标准分量表（单位/来源固定）；权重默认全 0，调用方必须**显式**给出偏好。

    它存在的意义是"有限合成检查与接口联调有可核对的分量表"，**不**代表尖塔的训练目标；
    真实实验权重由 W-07 冻结的 Profile 给出。
    """
    components = (
        RewardComponent(
            component_id="net:reward:enemy_hp_delta",
            unit="hp",
            source="快照 entities 里 Monster 的 net:field:base-spire:Monster.Hp（首末两份快照之差）",
            scale=1.0,
            description="对敌人造成的生命减少（正数 = 造成的伤害）",
        ),
        RewardComponent(
            component_id="net:reward:player_hp_delta",
            unit="hp",
            source="快照 entities 里 Player 的 net:field:base-spire:Player.Hp（首末两份快照之差）",
            scale=1.0,
            description="玩家生命的减少（正数 = 承受的伤害）",
        ),
        RewardComponent(
            component_id="net:reward:turn_count",
            unit="turns",
            source="快照 entities 里 Player 的 net:field:base-spire:Player.Turn（首末两份快照之差）",
            scale=1.0,
            description="消耗的回合数",
        ),
        RewardComponent(
            component_id="net:reward:decision_count",
            unit="events",
            source="轨迹里的转移条数（每个决策一次，公开可数）",
            scale=1.0,
            description="决策次数（步数成本）",
        ),
        RewardComponent(
            component_id="net:reward:potion_spent",
            unit="events",
            source="药水栏 group（net:group_type:base-spire:potion_belt）成员数首末之差",
            scale=1.0,
            optional=True,
            description="消耗的药水数（没有药水栏的 Profile 下不可用）",
        ),
        RewardComponent(
            component_id="net:reward:outcome_win",
            unit="boolean",
            source="末份快照 terminal_result.reason_id 的 outcome=win",
            scale=1.0,
            optional=True,
            description="是否以胜利结束（未到下终局的轨迹不可用）",
        ),
        RewardComponent(
            component_id="net:reward:outcome_loss",
            unit="boolean",
            source="末份快照 terminal_result.reason_id 的 outcome=loss",
            scale=1.0,
            optional=True,
            description="是否以失败结束（未到下终局的轨迹不可用）",
        ),
    )
    resolved = {item.component_id: 0.0 for item in components}
    for key, value in (weights or {}).items():
        resolved[key] = float(value)
    return RewardProfile(
        profile_id=profile_id,
        semantic_version=semantic_version,
        components=components,
        weights=resolved,
        discount=discount,
        description="工程用标准分量表（权重必须由调用方显式给出；真实实验权重由 W-07 冻结）",
        facts=facts,
    )
