"""D1 固定奖励语义（任务包 §5）：`r = -0.01 * hp_lost + victory - defeat`。

三个分量**保留向量与单位**，逐分量可审计：

============  =========  ==================================================================
分量 id       单位       语义（只读公开事实）
============  =========  ==================================================================
`hp_lost`     hp         `max(0, player_hp_before - player_hp_after)`，前后取自**公开快照**
`victory`     boolean    该动作**首次**把游戏带进真实终止且 `outcome=win` 时计 1
`defeat`      boolean    同上，`outcome=loss` 时计 1
============  =========  ==================================================================

实现方式：每个**已确认动作**都拿它的真实前快照与真实后快照组成一对，直接走现有
`reward.derive` 派生层（不写第二套奖励实现）；没有后快照（未确认结果）就**没有样本**。
"无终止、可确认继续运行时胜负分量为 0"是**缺公开事实**的自然结果（optional 记 unavailable），
不是把缺失当 0 补上。
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Any, Mapping, Optional, Sequence

from ..reward import RewardProfile, RewardVector, RewardComponent, derive_episode_reward
from ..reward.profile import SLICE_FACT_PATHS
from .errors import FaultError

#: D1 三分量奖励 Profile 的身份（换权重/换公开路径即换 id/digest）。
REWARD_PROFILE_ID = "net:reward:d1-three-component-v1"

COMPONENT_HP_LOST = "net:reward:hp_lost"
COMPONENT_VICTORY = "net:reward:outcome_win"
COMPONENT_DEFEAT = "net:reward:outcome_loss"

D1_COMPONENT_ORDER = (COMPONENT_HP_LOST, COMPONENT_VICTORY, COMPONENT_DEFEAT)


def d1_reward_profile(*, hp_lost_weight: float, victory_weight: float, defeat_weight: float,
                      discount: float) -> RewardProfile:
    """按冻结 Profile 的显式权重构造奖励 Profile（权重不来自任何"默认值"）。"""
    components = (
        RewardComponent(
            component_id=COMPONENT_HP_LOST,
            unit="hp",
            source=("前后两份公开快照里 net:kind:base-spire:Player 的 "
                    "net:field:base-spire:Player.Hp 之差，截断到 ≥0"),
            scale=1.0,
            optional=False,
            description="该动作造成的玩家生命损失（HP）",
        ),
        RewardComponent(
            component_id=COMPONENT_VICTORY,
            unit="boolean",
            source="后一份公开快照 terminal_result 声明 outcome=win（只在首次进入终止时计一次）",
            scale=1.0,
            optional=True,
            description="该动作是否让游戏以胜利终止",
        ),
        RewardComponent(
            component_id=COMPONENT_DEFEAT,
            unit="boolean",
            source="后一份公开快照 terminal_result 声明 outcome=loss（只在首次进入终止时计一次）",
            scale=1.0,
            optional=True,
            description="该动作是否让游戏以失败终止",
        ),
    )
    return RewardProfile(
        profile_id=REWARD_PROFILE_ID,
        semantic_version="1",
        components=components,
        weights={
            COMPONENT_HP_LOST: float(hp_lost_weight),
            COMPONENT_VICTORY: float(victory_weight),
            COMPONENT_DEFEAT: float(defeat_weight),
        },
        discount=float(discount),
        description="D1 固定三分量工程奖励：-0.01*hp_lost + victory - defeat（单位/来源显式）",
        facts=SLICE_FACT_PATHS,
    )


@dataclass(frozen=True)
class EpisodeReward:
    """一局逐动作奖励 + 未折扣分量/总回报 + 折扣回报（训练用）。"""

    per_action: tuple
    values: Mapping[str, float]
    units: Mapping[str, str]
    contributions: Mapping[str, float]
    scalar: float
    discounted_return: float
    steps: int
    vector: RewardVector

    def facts(self) -> dict:
        return {
            "hp_lost": float(self.values.get(COMPONENT_HP_LOST, 0.0)),
            "victory": float(self.values.get(COMPONENT_VICTORY, 0.0)),
            "defeat": float(self.values.get(COMPONENT_DEFEAT, 0.0)),
        }


def _check_finite(name: str, value: float) -> float:
    if not math.isfinite(float(value)):
        raise FaultError(f"奖励分量 {name} 不是有限数：{value!r}（数值失败，停止当前命令）")
    return float(value)


def action_rewards(snapshots: Sequence[Any], profile: RewardProfile) -> list[RewardVector]:
    """逐动作奖励：``snapshots[i]``（动作前公开快照）与 ``snapshots[i+1]``（真实后公开快照）。

    长度必须是 ``动作数 + 1``；少一份后快照就意味着该动作**未被确认**，调用方必须先把它剔除，
    不能在这里补一份。
    """
    if len(snapshots) < 2:
        raise FaultError("逐动作奖励至少需要「前快照 + 后快照」两份公开快照；没有确认动作就没有奖励")
    vectors: list[RewardVector] = []
    for index in range(len(snapshots) - 1):
        vector = derive_episode_reward([snapshots[index], snapshots[index + 1]], profile, steps=1)
        _check_finite(COMPONENT_HP_LOST, vector.scalar)
        vectors.append(vector)
    return vectors


def episode_reward(vectors: Sequence[RewardVector], profile: RewardProfile) -> EpisodeReward:
    """把逐动作向量汇总成 episode 向量（未折扣分量/总回报）与折扣回报。"""
    if not vectors:
        raise FaultError("一局没有任何已确认动作：没有奖励可汇总")
    values: dict[str, float] = {}
    contributions: dict[str, float] = {}
    units: dict[str, str] = {}
    for component_id in D1_COMPONENT_ORDER:
        total_value = 0.0
        total_contribution = 0.0
        for vector in vectors:
            if component_id in vector.values:
                total_value += float(vector.values[component_id])
                total_contribution += float(vector.contributions[component_id])
        values[component_id] = total_value
        contributions[component_id] = total_contribution
        if component_id == COMPONENT_HP_LOST:
            units[component_id] = "hp"
        elif any(component_id in vector.units for vector in vectors):
            units[component_id] = "boolean"
    scalar = sum(float(vector.scalar) for vector in vectors)
    _check_finite("episode.scalar", scalar)
    gamma = float(profile.discount)
    discounted = 0.0
    for index, vector in enumerate(vectors):
        discounted += (gamma ** index) * float(vector.scalar)
    _check_finite("episode.discounted_return", discounted)
    aggregate = RewardVector(
        profile_id=profile.profile_id,
        profile_digest=profile.digest(),
        values=values,
        units=units,
        contributions=contributions,
        scalar=scalar,
        steps=len(vectors),
        unavailable=tuple(sorted({item for vector in vectors for item in vector.unavailable})),
    )
    return EpisodeReward(
        per_action=tuple(vectors),
        values=values,
        units=units,
        contributions=contributions,
        scalar=scalar,
        discounted_return=discounted,
        steps=len(vectors),
        vector=aggregate,
    )


def discounted_returns(action_scalars: Sequence[float], *, gamma: float,
                       bootstrap: Optional[float] = None) -> list[float]:
    """折扣回报 ``G_i = Σ_{t>=i} γ^{t-i} r_t``；截断局把 ``γ^{n-i} V_old(s_T)`` 加到每个 ``G_i``。

    ``bootstrap`` 必须是**本轮冻结行为模型**在最后公开快照上的 ``V_old(s_T)``；
    正常终止（含"最后一个动作进入终止"）时 bootstrap 为 0，**不是** None。
    """
    if not action_scalars:
        raise FaultError("没有已确认动作就没有折扣回报")
    if not 0.0 < float(gamma) <= 1.0:
        raise FaultError(f"gamma={gamma!r} 必须在 (0, 1] 内")
    count = len(action_scalars)
    returns = [0.0] * count
    running = 0.0
    for index in range(count - 1, -1, -1):
        running = float(action_scalars[index]) + float(gamma) * running
        returns[index] = running
    if bootstrap is not None:
        _check_finite("bootstrap", bootstrap)
        for index in range(count):
            returns[index] += (float(gamma) ** (count - index)) * float(bootstrap)
    for value in returns:
        _check_finite("discounted_return", value)
    return returns
