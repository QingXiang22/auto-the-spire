"""奖励 / 偏好**派生层**（W-04 第 3 项）：在原始公开事实之外定义、版本化并显式标量化。

依据 `training-design.md`（向量奖励 + 配置化偏好、`reward_profile_id` 随轨迹记录）与
`policy-contract.md`（模型不能把概率或 V 当成收益）：

* **只读公开事实**：派生只见 net 快照（实体/字段/关系/终局结果）与轨迹里的转移序列，
  不读隐藏状态、不推断未公开量；缺所需公开事实时**显式失败**，不用 0 冒充。
* **保留向量与单位**：每个分量有 `component_id`、单位、来源说明与缩放；标量化按**显式权重**进行，
  并把每个分量的贡献一起记录（可审计）。
* **版本化**：`RewardProfile` 有 id/语义版本/摘要；轨迹与模型包记录 `reward_profile_id`，
  换权重或换单位就是**新的 Profile**，不会让历史数据看起来像同一实验。
* 本层**不**决定"该奖励什么"：权重、折扣与时间尺度由 W-07 冻结的 Profile 给出；
  这里提供的是工程与可核对性（缺 Profile 时用显式配置跑有限检查，不猜收益权重）。
"""

from .derive import RewardError, RewardVector, derive_episode_reward, scalarize
from .profile import (
    REWARD_PROFILE_VERSION,
    SLICE_FACT_PATHS,
    RewardComponent,
    RewardFactPaths,
    RewardProfile,
    RewardProfileError,
    STANDARD_UNITS,
    standard_short_task_profile,
)

__all__ = [
    "REWARD_PROFILE_VERSION",
    "SLICE_FACT_PATHS",
    "RewardComponent",
    "RewardError",
    "RewardFactPaths",
    "RewardProfile",
    "RewardProfileError",
    "RewardVector",
    "STANDARD_UNITS",
    "derive_episode_reward",
    "scalarize",
    "standard_short_task_profile",
]
