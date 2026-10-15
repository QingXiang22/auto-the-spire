"""评估计划：**显式**声明要比什么、在哪些保留种子上比、用哪份执行配置与奖励 Profile。

为什么要有计划对象而不是散在脚本里：

* 计划把 scenario/gym/奖励 Profile/执行器配置/种子/指标**绑定成一个身份**（带摘要）——
  换种子或换权重就是**新实验**，历史报告不会被当成同一次比较（`training-design.md` §Evaluator）；
* 指标必须显式声明**单位**与**方向**（越大越好 / 越小越好），否则"改善了"这句话无法核对；
* 计划要求至少一个基线策略，且基线必须出现在策略列表里。
"""

from __future__ import annotations

import hashlib
import math
from dataclasses import dataclass, fields
from typing import Any, Mapping, Sequence

from ..contracts import layers

#: 评估计划格式版本。
EVALUATION_PLAN_VERSION = "evaluation-plan@1"

#: 指标允许的单位（新增单位必须显式加进来，避免"无单位数字"）。
METRIC_UNITS = ("reward", "steps", "milliseconds", "count", "boolean")

#: 允许的方向（显式声明，不猜"越大越好"）。
#:
#: `descriptive` 是**中性**方向：这类指标只描述"实际发生了什么"（行为/成本），
#: **不因更大或更小自动判策略改善或变差**——例如原始确认动作数：走得更久、操作更多，
#: 可能只是更慢、更分散或活得更久，不能当成学习成功。奖励/胜负仍用带方向的配对比较。
METRIC_DIRECTIONS = ("higher_is_better", "lower_is_better", "descriptive")

#: 策略种类（决定如何构造可调用策略）。
#:
#: `rule_baseline` 是**规则基线**：它由显式规则决定动作（D2 诊断的公开信息参考策略），
#: 与学习模型区分开——不是模型、不是训练标签、也不是故障兜底。
POLICY_KINDS = ("uniform_baseline", "rule_baseline", "small_model")


class EvaluationPlanError(ValueError):
    """评估计划非法：一律带字段名与原因。"""


def _canonical_decimal(value: float) -> str:
    return repr(float(value))


@dataclass(frozen=True)
class MetricSpec:
    """一个评估指标：公开 id + 单位 + 方向。"""

    metric_id: str
    unit: str
    direction: str
    description: str = ""

    def __post_init__(self) -> None:
        if not isinstance(self.metric_id, str) or not self.metric_id.strip():
            raise EvaluationPlanError(f"metric_id 必须是非空字符串，得到 {self.metric_id!r}")
        if self.unit not in METRIC_UNITS:
            raise EvaluationPlanError(
                f"{self.metric_id}.unit={self.unit!r} 不是允许的单位（{METRIC_UNITS}）"
            )
        if self.direction not in METRIC_DIRECTIONS:
            raise EvaluationPlanError(
                f"{self.metric_id}.direction={self.direction!r} 不是允许的方向（{METRIC_DIRECTIONS}）"
            )
        if not isinstance(self.description, str):
            raise EvaluationPlanError(f"{self.metric_id}.description 必须是字符串")

    def to_wire(self) -> dict:
        return {"metric_id": self.metric_id, "unit": self.unit,
                "direction": self.direction, "description": self.description}

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "MetricSpec":
        expected = {item.name for item in fields(cls)}
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise EvaluationPlanError(f"MetricSpec 键不匹配：缺少 {missing}，多出 {extra}")
        return cls(**{item.name: doc[item.name] for item in fields(cls)})


@dataclass(frozen=True)
class PolicySpec:
    """被评估的一个策略：身份 + 种类 + 训练状态（训练状态进报告，避免把未训练模型当成果）。"""

    policy_id: str
    kind: str
    status: str
    description: str = ""

    def __post_init__(self) -> None:
        if not isinstance(self.policy_id, str) or not self.policy_id.strip():
            raise EvaluationPlanError(f"policy_id 必须是非空字符串，得到 {self.policy_id!r}")
        if self.kind not in POLICY_KINDS:
            raise EvaluationPlanError(f"{self.policy_id}.kind={self.kind!r} 不是允许的种类（{POLICY_KINDS}）")
        if self.status not in ("baseline", "untrained", "trained"):
            raise EvaluationPlanError(
                f"{self.policy_id}.status={self.status!r} 必须是 baseline/untrained/trained 之一"
            )
        if self.kind in ("uniform_baseline", "rule_baseline") and self.status != "baseline":
            raise EvaluationPlanError(
                f"{self.policy_id}: {self.kind} 的 status 必须是 baseline（基线不能自称 untrained/trained）"
            )
        if not isinstance(self.description, str):
            raise EvaluationPlanError(f"{self.policy_id}.description 必须是字符串")

    def to_wire(self) -> dict:
        return {"policy_id": self.policy_id, "kind": self.kind, "status": self.status,
                "description": self.description}

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "PolicySpec":
        expected = {item.name for item in fields(cls)}
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise EvaluationPlanError(f"PolicySpec 键不匹配：缺少 {missing}，多出 {extra}")
        return cls(**{item.name: doc[item.name] for item in fields(cls)})


@dataclass(frozen=True)
class EvaluationPlan:
    """一次评估的完整身份与配置。"""

    plan_id: str
    semantic_version: str
    policies: Sequence[PolicySpec]
    baseline_policy_id: str
    seeds: Sequence[int]
    metrics: Sequence[MetricSpec]
    reward_profile_id: str
    reward_profile_digest: str
    executor_profile_id: str
    scenario_profile: str
    gym_id: str
    max_steps: int
    bootstrap_resamples: int = 2000
    bootstrap_seed: int = 20261003
    description: str = ""

    def __post_init__(self) -> None:
        for name in ("plan_id", "semantic_version", "baseline_policy_id", "reward_profile_id",
                     "reward_profile_digest", "executor_profile_id", "scenario_profile", "gym_id"):
            value = getattr(self, name)
            if not isinstance(value, str) or not value.strip():
                raise EvaluationPlanError(f"{name} 必须是非空字符串，得到 {value!r}")
        if not isinstance(self.description, str):
            raise EvaluationPlanError("description 必须是字符串")

        policies = tuple(self.policies)
        if len(policies) < 2:
            raise EvaluationPlanError("评估至少要有一个基线与一个候选策略")
        object.__setattr__(self, "policies", policies)
        seen: set[str] = set()
        for policy in policies:
            if not isinstance(policy, PolicySpec):
                raise EvaluationPlanError(f"策略必须是 PolicySpec，得到 {type(policy).__name__}")
            if policy.policy_id in seen:
                raise EvaluationPlanError(f"策略 id 重复：{policy.policy_id}")
            seen.add(policy.policy_id)
        if self.baseline_policy_id not in seen:
            raise EvaluationPlanError(
                f"基线 {self.baseline_policy_id!r} 必须出现在策略列表里（否则无法配对比较）"
            )
        if not any(policy.policy_id != self.baseline_policy_id for policy in policies):
            raise EvaluationPlanError("除了基线之外还要有至少一个候选策略")

        seeds = tuple(self.seeds)
        if not seeds:
            raise EvaluationPlanError("必须给出非空的保留种子（不许随机现抽）")
        for seed in seeds:
            if isinstance(seed, bool) or not isinstance(seed, int) or seed < 0:
                raise EvaluationPlanError(f"种子必须是非负整数，得到 {seed!r}")
        if len(set(seeds)) != len(seeds):
            raise EvaluationPlanError(f"种子不得重复：{sorted(seeds)}")
        object.__setattr__(self, "seeds", seeds)

        metrics = tuple(self.metrics)
        if not metrics:
            raise EvaluationPlanError("必须声明至少一个指标")
        metric_ids = [metric.metric_id for metric in metrics]
        if len(set(metric_ids)) != len(metric_ids):
            raise EvaluationPlanError(f"指标 id 重复：{metric_ids}")
        for metric in metrics:
            if not isinstance(metric, MetricSpec):
                raise EvaluationPlanError(f"指标必须是 MetricSpec，得到 {type(metric).__name__}")
        object.__setattr__(self, "metrics", metrics)

        for name, minimum in (("max_steps", 1), ("bootstrap_resamples", 1), ("bootstrap_seed", 0)):
            value = getattr(self, name)
            if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
                raise EvaluationPlanError(f"{name} 必须是 ≥{minimum} 的整数，得到 {value!r}")

    # -- 读取 ------------------------------------------------------------ #
    @property
    def metric_ids(self) -> tuple[str, ...]:
        return tuple(metric.metric_id for metric in self.metrics)

    def metric(self, metric_id: str) -> MetricSpec:
        for metric in self.metrics:
            if metric.metric_id == metric_id:
                return metric
        raise EvaluationPlanError(f"计划里没有指标 {metric_id!r}")

    def policy(self, policy_id: str) -> PolicySpec:
        for policy in self.policies:
            if policy.policy_id == policy_id:
                return policy
        raise EvaluationPlanError(f"计划里没有策略 {policy_id!r}")

    def candidate_policy_ids(self) -> tuple[str, ...]:
        return tuple(policy.policy_id for policy in self.policies
                     if policy.policy_id != self.baseline_policy_id)

    # -- 序列化 ---------------------------------------------------------- #
    def to_wire(self) -> dict:
        return {
            "evaluation_plan_version": EVALUATION_PLAN_VERSION,
            "plan_id": self.plan_id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "baseline_policy_id": self.baseline_policy_id,
            "policies": [policy.to_wire() for policy in self.policies],
            "metrics": [metric.to_wire() for metric in self.metrics],
            "seeds": list(self.seeds),
            "reward_profile_id": self.reward_profile_id,
            "reward_profile_digest": self.reward_profile_digest,
            "executor_profile_id": self.executor_profile_id,
            "scenario_profile": self.scenario_profile,
            "gym_id": self.gym_id,
            "max_steps": int(self.max_steps),
            "bootstrap_resamples": int(self.bootstrap_resamples),
            "bootstrap_seed": int(self.bootstrap_seed),
        }

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "EvaluationPlan":
        expected = {
            "evaluation_plan_version", "plan_id", "semantic_version", "description",
            "baseline_policy_id", "policies", "metrics", "seeds", "reward_profile_id",
            "reward_profile_digest", "executor_profile_id", "scenario_profile", "gym_id",
            "max_steps", "bootstrap_resamples", "bootstrap_seed",
        }
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise EvaluationPlanError(f"EvaluationPlan 键不匹配：缺少 {missing}，多出 {extra}")
        if doc["evaluation_plan_version"] != EVALUATION_PLAN_VERSION:
            raise EvaluationPlanError(
                f"评估计划版本 {doc['evaluation_plan_version']!r} 与当前 {EVALUATION_PLAN_VERSION!r} 不同"
            )
        if not isinstance(doc["policies"], list) or not isinstance(doc["metrics"], list):
            raise EvaluationPlanError("policies/metrics 必须是数组")
        if not isinstance(doc["seeds"], list):
            raise EvaluationPlanError("seeds 必须是数组")
        return cls(
            plan_id=doc["plan_id"],
            semantic_version=doc["semantic_version"],
            policies=tuple(PolicySpec.from_wire(item) for item in doc["policies"]),
            baseline_policy_id=doc["baseline_policy_id"],
            seeds=tuple(int(seed) for seed in doc["seeds"]),
            metrics=tuple(MetricSpec.from_wire(item) for item in doc["metrics"]),
            reward_profile_id=doc["reward_profile_id"],
            reward_profile_digest=doc["reward_profile_digest"],
            executor_profile_id=doc["executor_profile_id"],
            scenario_profile=doc["scenario_profile"],
            gym_id=doc["gym_id"],
            max_steps=int(doc["max_steps"]),
            bootstrap_resamples=int(doc["bootstrap_resamples"]),
            bootstrap_seed=int(doc["bootstrap_seed"]),
            description=doc["description"],
        )

    def digest(self) -> str:
        return "sha256:" + hashlib.sha256(layers.canonical_bytes(self.to_wire())).hexdigest()


def finite(value: Any) -> float:
    """把指标值规范成有限 float（NaN/Inf 直接拒绝，避免"平均值"被污染）。"""
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise EvaluationPlanError(f"指标值必须是数值，得到 {value!r}")
    number = float(value)
    if not math.isfinite(number):
        raise EvaluationPlanError(f"指标值必须是有限数，得到 {value!r}")
    return number
