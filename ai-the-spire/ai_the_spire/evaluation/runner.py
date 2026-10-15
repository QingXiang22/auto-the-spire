"""评估执行器与报告：配对 episode、按指标聚合、与基线比较、记录成本与**所有失败**。

职责边界：

* 只驱动**真实 stdio** 会话（`net-the-spire` 的客户端）与显式策略；不实现游戏规则；
* 每个 (policy, seed) 跑一局，按 `EpisodeRunner` 的结束分类如实记录
  `terminal` / `truncated` / `interrupted` / `faulted`；**失败局不混进指标均值**；
* 指标只在**可用**时进入聚合（例如未到终局就没有"是否胜利"），并逐指标报告样本数；
* 比较采用**同种子配对**：差值 = 候选 − 基线（按计划声明的方向给出判定），
  区间用自助法且记录重抽次数与种子；`n_pairs < 2` 或区间跨 0 时 **不声称改善**。
"""

from __future__ import annotations

import hashlib
import time
from dataclasses import dataclass
from typing import Any, Mapping, Optional, Sequence

from ..contracts import layers
from ..reward import RewardProfile, RewardVector, derive_episode_reward
from ..runner import EpisodeRunner
from .plan import EvaluationPlan, PolicySpec
from .stats import bootstrap_ci, mean, standard_deviation

#: 可复现验证用的指标 id（报告与计划共用同一批字符串）。
METRIC_REWARD = "net:metric:reward_scalar"
METRIC_STEPS = "net:metric:steps"
METRIC_WALL_MS = "net:metric:wall_ms"
METRIC_TERMINAL_WIN = "net:metric:terminal_win"

#: 终局/截断都算"可用于统计"，中断与故障不算。
USABLE_ENDINGS = ("terminal", "truncated")


class EvaluationError(RuntimeError):
    """评估执行失败（策略不可用、计划与策略不匹配等）。"""


@dataclass(frozen=True)
class EpisodeOutcome:
    """一局评估的原始记录（含结束分类、逐指标可用性与成本）。"""

    policy_id: str
    seed: int
    ending: str
    terminal_outcome: Optional[str]
    metrics: Mapping[str, float]
    unavailable: tuple
    steps: int
    wall_ms: float
    reward_scalar: Optional[float]
    reward_vector: Optional[dict] = None

    def to_wire(self) -> dict:
        return {
            "policy_id": self.policy_id,
            "seed": int(self.seed),
            "ending": self.ending,
            "terminal_outcome": self.terminal_outcome,
            "metrics": {key: repr(float(self.metrics[key])) for key in sorted(self.metrics)},
            "unavailable": list(self.unavailable),
            "steps": int(self.steps),
            "wall_ms": repr(float(self.wall_ms)),
            "reward_scalar": None if self.reward_scalar is None else repr(float(self.reward_scalar)),
        }


@dataclass(frozen=True)
class MetricSummary:
    """一个指标在一个策略上的聚合（含样本数与区间；样本不足时不编造区间）。"""

    metric_id: str
    unit: str
    samples: int
    mean: float
    standard_deviation: float
    ci_low: Optional[float]
    ci_high: Optional[float]

    def to_wire(self) -> dict:
        return {
            "metric_id": self.metric_id,
            "unit": self.unit,
            "samples": int(self.samples),
            "mean": repr(float(self.mean)),
            "standard_deviation": repr(float(self.standard_deviation)),
            "ci_low": None if self.ci_low is None else repr(float(self.ci_low)),
            "ci_high": None if self.ci_high is None else repr(float(self.ci_high)),
        }


@dataclass(frozen=True)
class PolicySummary:
    """一个策略的评估摘要：可用局数、结束分类计数、逐指标聚合与成本。"""

    policy_id: str
    status: str
    episodes: int
    usable: int
    endings: Mapping[str, int]
    metrics: Mapping[str, MetricSummary]
    total_wall_ms: float
    total_decisions: int
    parameter_total: Optional[int] = None

    def to_wire(self) -> dict:
        return {
            "policy_id": self.policy_id,
            "status": self.status,
            "episodes": int(self.episodes),
            "usable": int(self.usable),
            "endings": {key: int(value) for key, value in sorted(self.endings.items())},
            "metrics": {key: self.metrics[key].to_wire() for key in sorted(self.metrics)},
            "total_wall_ms": repr(float(self.total_wall_ms)),
            "total_decisions": int(self.total_decisions),
            "parameter_total": None if self.parameter_total is None else int(self.parameter_total),
        }


@dataclass(frozen=True)
class PairedComparison:
    """候选 vs 基线的同种子配对比较（差值 = 候选 − 基线）。"""

    policy_id: str
    baseline_policy_id: str
    metric_id: str
    direction: str
    pairs: int
    mean_difference: float
    standard_deviation: float
    ci_low: Optional[float]
    ci_high: Optional[float]
    resamples: int
    bootstrap_seed: int
    verdict: str          # improved / worse / undecided / insufficient_samples
    reason: str

    def to_wire(self) -> dict:
        return {
            "policy_id": self.policy_id,
            "baseline_policy_id": self.baseline_policy_id,
            "metric_id": self.metric_id,
            "direction": self.direction,
            "pairs": int(self.pairs),
            "mean_difference": repr(float(self.mean_difference)),
            "standard_deviation": repr(float(self.standard_deviation)),
            "ci_low": None if self.ci_low is None else repr(float(self.ci_low)),
            "ci_high": None if self.ci_high is None else repr(float(self.ci_high)),
            "resamples": int(self.resamples),
            "bootstrap_seed": int(self.bootstrap_seed),
            "verdict": self.verdict,
            "reason": self.reason,
        }


@dataclass(frozen=True)
class EvaluationReport:
    """一次评估的完整产物（自足：计划身份 + 逐局记录 + 摘要 + 比较 + 失败与声明）。"""

    plan: dict
    plan_digest: str
    outcomes: tuple
    policies: tuple
    comparisons: tuple
    failures: tuple
    notes: tuple
    ending: str = "completed"

    def to_wire(self) -> dict:
        return {
            "report_format_version": "evaluation-report@1",
            "plan": self.plan,
            "plan_digest": self.plan_digest,
            "ending": self.ending,
            "notes": list(self.notes),
            "failures": list(self.failures),
            "episodes": [outcome.to_wire() for outcome in self.outcomes],
            "policies": [summary.to_wire() for summary in self.policies],
            "comparisons": [comparison.to_wire() for comparison in self.comparisons],
        }

    def digest(self) -> str:
        return "sha256:" + hashlib.sha256(layers.canonical_bytes(self.to_wire())).hexdigest()

    def comparison(self, policy_id: str, metric_id: str) -> Optional[PairedComparison]:
        for comparison in self.comparisons:
            if comparison.policy_id == policy_id and comparison.metric_id == metric_id:
                return comparison
        return None


# --------------------------------------------------------------------------- #
# 聚合与比较（纯函数，便于用已知答案的合成数据检查）
# --------------------------------------------------------------------------- #
def aggregate(outcomes: Sequence[EpisodeOutcome], plan: EvaluationPlan,
              parameter_totals: Optional[Mapping[str, int]] = None) -> tuple:
    """逐策略聚合：只用**可用**局，逐指标报告样本数与自助区间；成本与失败都记录。"""
    totals = dict(parameter_totals or {})
    by_policy: dict[str, list] = {}
    for outcome in outcomes:
        by_policy.setdefault(outcome.policy_id, []).append(outcome)
    summaries = []
    for spec in plan.policies:
        episodes = by_policy.get(spec.policy_id, [])
        endings: dict[str, int] = {}
        for outcome in episodes:
            endings[outcome.ending] = endings.get(outcome.ending, 0) + 1
        usable = [outcome for outcome in episodes if outcome.ending in USABLE_ENDINGS]
        metrics: dict[str, MetricSummary] = {}
        for metric in plan.metrics:
            values = [outcome.metrics[metric.metric_id] for outcome in usable
                      if metric.metric_id in outcome.metrics]
            if not values:
                continue
            low, high = (None, None)
            if len(values) >= 2:
                low, high = bootstrap_ci(values, resamples=plan.bootstrap_resamples,
                                         seed=plan.bootstrap_seed)
            metrics[metric.metric_id] = MetricSummary(
                metric_id=metric.metric_id,
                unit=metric.unit,
                samples=len(values),
                mean=mean(values),
                standard_deviation=standard_deviation(values),
                ci_low=low,
                ci_high=high,
            )
        summaries.append(PolicySummary(
            policy_id=spec.policy_id,
            status=spec.status,
            episodes=len(episodes),
            usable=len(usable),
            endings=endings,
            metrics=metrics,
            total_wall_ms=sum(outcome.wall_ms for outcome in episodes),
            total_decisions=sum(outcome.steps for outcome in episodes),
            parameter_total=totals.get(spec.policy_id),
        ))
    return tuple(summaries)


def paired_comparison(outcomes: Sequence[EpisodeOutcome], plan: EvaluationPlan,
                      policy_id: str, metric_id: str) -> PairedComparison:
    """同种子配对比较；样本不足或区间跨 0 时给 `undecided`/`insufficient_samples`。

    ``direction="descriptive"`` 的指标**只报告差值**，永远给 ``descriptive``：
    行为/成本量（例如确认动作数）变大变小都不自动等于策略变好或变差。
    """
    metric = plan.metric(metric_id)
    candidate = {outcome.seed: outcome for outcome in outcomes
                 if outcome.policy_id == policy_id and outcome.ending in USABLE_ENDINGS
                 and metric_id in outcome.metrics}
    baseline = {outcome.seed: outcome for outcome in outcomes
                if outcome.policy_id == plan.baseline_policy_id and outcome.ending in USABLE_ENDINGS
                and metric_id in outcome.metrics}
    shared = sorted(set(candidate) & set(baseline))
    differences = [candidate[seed].metrics[metric_id] - baseline[seed].metrics[metric_id]
                   for seed in shared]
    if not differences:
        return PairedComparison(
            policy_id=policy_id, baseline_policy_id=plan.baseline_policy_id, metric_id=metric_id,
            direction=metric.direction, pairs=0, mean_difference=0.0, standard_deviation=0.0,
            ci_low=None, ci_high=None, resamples=plan.bootstrap_resamples,
            bootstrap_seed=plan.bootstrap_seed, verdict="insufficient_samples",
            reason="没有任何一个种子在两侧都产出该指标（失败局不计入配对）",
        )
    average = mean(differences)
    spread = standard_deviation(differences)
    if metric.direction == "descriptive":
        low, high = (bootstrap_ci(differences, resamples=plan.bootstrap_resamples,
                                  seed=plan.bootstrap_seed) if len(differences) >= 2
                     else (None, None))
        return PairedComparison(
            policy_id=policy_id, baseline_policy_id=plan.baseline_policy_id, metric_id=metric_id,
            direction=metric.direction, pairs=len(differences), mean_difference=average,
            standard_deviation=spread, ci_low=low, ci_high=high,
            resamples=plan.bootstrap_resamples, bootstrap_seed=plan.bootstrap_seed,
            verdict="descriptive",
            reason="描述性行为/成本指标：只报告实际差值，不因更多或更少自动判改善/变差",
        )
    if len(differences) < 2:
        return PairedComparison(
            policy_id=policy_id, baseline_policy_id=plan.baseline_policy_id, metric_id=metric_id,
            direction=metric.direction, pairs=1, mean_difference=average, standard_deviation=0.0,
            ci_low=None, ci_high=None, resamples=plan.bootstrap_resamples,
            bootstrap_seed=plan.bootstrap_seed, verdict="undecided",
            reason="只有一对可用样本：区间无意义，不据此声称改善",
        )
    low, high = bootstrap_ci(differences, resamples=plan.bootstrap_resamples,
                             seed=plan.bootstrap_seed)
    if low > 0.0:
        verdict = "improved" if metric.direction == "higher_is_better" else "worse"
    elif high < 0.0:
        verdict = "worse" if metric.direction == "higher_is_better" else "improved"
    else:
        verdict = "undecided"
    return PairedComparison(
        policy_id=policy_id, baseline_policy_id=plan.baseline_policy_id, metric_id=metric_id,
        direction=metric.direction, pairs=len(differences), mean_difference=average,
        standard_deviation=spread, ci_low=low, ci_high=high,
        resamples=plan.bootstrap_resamples, bootstrap_seed=plan.bootstrap_seed,
        verdict=verdict,
        reason=("自助区间不跨 0" if verdict != "undecided" else "自助区间跨 0：不声称改善"),
    )


# --------------------------------------------------------------------------- #
# 执行器
# --------------------------------------------------------------------------- #
class EvaluationRunner:
    """按计划跑评估：每个 (policy, seed) 一局，逐局记录并聚合。"""

    def __init__(self, client: Any, plan: EvaluationPlan, reward_profile: RewardProfile, *,
                 encoding_config: Any = None, executor_seed: int = 20261002) -> None:
        if not isinstance(plan, EvaluationPlan):
            raise TypeError(f"plan 必须是 EvaluationPlan，得到 {type(plan).__name__}")
        if not isinstance(reward_profile, RewardProfile):
            raise TypeError(f"reward_profile 必须是 RewardProfile，得到 {type(reward_profile).__name__}")
        if reward_profile.digest() != plan.reward_profile_digest:
            raise EvaluationError(
                "奖励 Profile 与计划不一致："
                f"{reward_profile.digest()!r} != {plan.reward_profile_digest!r}（换权重就是新实验）"
            )
        self.client = client
        self.plan = plan
        self.reward_profile = reward_profile
        self.encoding_config = encoding_config
        self.executor_seed = executor_seed
        self._policies: dict[str, Any] = {}
        self._parameter_totals: dict[str, int] = {}

    # -- 策略装配 -------------------------------------------------------- #
    def bind_model_policy(self, policy_id: str, model: Any) -> None:
        """把 `SmallPolicyModel` 绑到计划里的 `small_model` 策略（模型身份必须与声明一致）。"""
        spec = self.plan.policy(policy_id)
        if spec.kind != "small_model":
            raise EvaluationError(f"策略 {policy_id} 的种类是 {spec.kind}，不能绑定模型")
        if spec.status == "untrained" and getattr(model, "weights_digest", None) is None:
            raise EvaluationError("untrained 策略也必须给出模型权重的身份摘要")
        self._policies[policy_id] = model.policy
        self._parameter_totals[policy_id] = int(model.scorer.params.total_size())

    def _policy_callable(self, spec: PolicySpec):
        if spec.kind == "uniform_baseline":
            return None  # EpisodeRunner 的默认策略就是均匀基线
        if spec.policy_id not in self._policies:
            raise EvaluationError(
                f"策略 {spec.policy_id} 是 {spec.kind}，但还没绑定：先调用 bind_model_policy()"
            )
        return self._policies[spec.policy_id]

    # -- 执行 ------------------------------------------------------------ #
    def run(self) -> EvaluationReport:
        outcomes: list[EpisodeOutcome] = []
        failures: list[str] = []
        notes: list[str] = []
        # 先**全量装配**所有策略：未绑定的模型策略必须在跑任何一局之前就报错
        # （否则会在跑了若干局之后才失败，浪费真实会话并留下半个报告）。
        callables = {spec.policy_id: self._policy_callable(spec) for spec in self.plan.policies}
        for spec in self.plan.policies:
            policy = callables[spec.policy_id]
            for seed in self.plan.seeds:
                outcome = self._run_episode(spec, seed, policy)
                outcomes.append(outcome)
                if outcome.ending not in USABLE_ENDINGS:
                    failures.append(f"{spec.policy_id}/seed={seed}: ending={outcome.ending}")
        candidates = tuple(outcome for outcome in outcomes
                           if outcome.policy_id != self.plan.baseline_policy_id)
        unchanged = {outcome.policy_id for outcome in candidates}
        for policy_id in sorted(unchanged):
            spec = self.plan.policy(policy_id)
            if spec.status == "untrained":
                notes.append(
                    f"策略 {policy_id} 声明 status=untrained：本报告只验证评估工程，"
                    "不构成任何学习改善结论"
                )
        if failures:
            notes.append(f"有 {len(failures)} 局未产出可用指标（已逐条记录，未混入均值）")
        notes.append(
            "本报告不替调用方下'晋升'结论；'改善'只在自助区间不跨 0 且配对数 ≥2 时给出。"
        )
        summaries = aggregate(outcomes, self.plan, self._parameter_totals)
        comparisons = []
        for policy_id in self.plan.candidate_policy_ids():
            for metric_id in self.plan.metric_ids:
                comparisons.append(paired_comparison(outcomes, self.plan, policy_id, metric_id))
        return EvaluationReport(
            plan=self.plan.to_wire(),
            plan_digest=self.plan.digest(),
            outcomes=tuple(outcomes),
            policies=summaries,
            comparisons=tuple(comparisons),
            failures=tuple(failures),
            notes=tuple(notes),
        )

    def _run_episode(self, spec: PolicySpec, seed: int, policy) -> EpisodeOutcome:
        runner = EpisodeRunner(
            self.client, seed=seed, max_steps=self.plan.max_steps,
            encoding_config=self.encoding_config, executor_seed=self.executor_seed,
            policy=policy,
            model_id=(spec.policy_id if spec.kind == "small_model" else "baseline:uniform-legal-candidates@1"),
        )
        started = time.perf_counter()
        result = runner.run(raise_on_failure=False)
        wall_ms = (time.perf_counter() - started) * 1000.0
        snapshots = [transition.snapshot for transition in result.transitions]
        metrics: dict[str, float] = {}
        unavailable: list[str] = []
        reward_scalar: Optional[float] = None
        reward_vector: Optional[dict] = None
        if snapshots:
            vector: RewardVector = derive_episode_reward(snapshots, self.reward_profile,
                                                         steps=result.steps)
            reward_scalar = vector.scalar
            reward_vector = vector.to_wire()
            if METRIC_REWARD in self.plan.metric_ids:
                metrics[METRIC_REWARD] = vector.scalar
        if METRIC_REWARD in self.plan.metric_ids and METRIC_REWARD not in metrics:
            unavailable.append(METRIC_REWARD)
        if METRIC_STEPS in self.plan.metric_ids:
            metrics[METRIC_STEPS] = float(result.steps)
        if METRIC_WALL_MS in self.plan.metric_ids:
            metrics[METRIC_WALL_MS] = wall_ms
        if METRIC_TERMINAL_WIN in self.plan.metric_ids:
            if result.ending == "terminal":
                outcome = (result.transitions[-1].terminal or {}).get("outcome") \
                    if result.transitions else None
                metrics[METRIC_TERMINAL_WIN] = 1.0 if outcome == "win" else 0.0
            else:
                unavailable.append(METRIC_TERMINAL_WIN)
        for metric_id in self.plan.metric_ids:
            if metric_id not in metrics and metric_id not in unavailable:
                unavailable.append(metric_id)
        return EpisodeOutcome(
            policy_id=spec.policy_id,
            seed=int(seed),
            ending=result.ending,
            terminal_outcome=((result.transitions[-1].terminal or {}).get("outcome")
                              if result.ending == "terminal" and result.transitions else None),
            metrics=metrics,
            unavailable=tuple(sorted(unavailable)),
            steps=int(result.steps),
            wall_ms=wall_ms,
            reward_scalar=reward_scalar,
            reward_vector=reward_vector,
        )
