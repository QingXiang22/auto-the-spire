#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""W-04 评估检查：计划身份、配对统计与失败处理。

分组：

* ``eval-plan``：计划校验（策略/基线/种子/指标单位与方向/自助配置/版本与摘要往返）；
* ``eval-stats``：统计口径（均值/样本标准差/分位数/自助区间，用**已知答案**与可复现性核对）；
* ``eval-failures``：失败局不混入均值、样本不足不编造区间、未训练策略必须带声明。

**证据边界**：本套件用合成 episode 记录检查评估**工程**（身份、配对、区间、失败分类、成本记录）；
真实 stdio 上的同一套流程由 `j01_episode.py` 的 `evaluation` 组覆盖，且**不**声称任何学习改善。
"""

from __future__ import annotations

import pathlib
import sys
from typing import Any, Callable, Optional

_PACKAGE_ROOT = pathlib.Path(__file__).resolve().parents[1]
if str(_PACKAGE_ROOT) not in sys.path:
    sys.path.insert(0, str(_PACKAGE_ROOT))
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from ai_the_spire.evaluation import (  # noqa: E402
    METRIC_UNITS,
    EpisodeOutcome,
    EvaluationPlan,
    EvaluationPlanError,
    MetricSpec,
    PairedComparison,
    PolicySpec,
    aggregate,
    bootstrap_ci,
    mean,
    paired_comparison,
    percentile,
    standard_deviation,
)
from ai_the_spire.evaluation.runner import (  # noqa: E402
    METRIC_REWARD,
    METRIC_STEPS,
    METRIC_TERMINAL_WIN,
    METRIC_WALL_MS,
    EvaluationError,
    EvaluationRunner,
)

from fixture_suite import Outcome  # noqa: E402

SUITE_PLAN = "eval-plan"
SUITE_STATS = "eval-stats"
SUITE_FAILURES = "eval-failures"

SUITE_NAMES = (SUITE_PLAN, SUITE_STATS, SUITE_FAILURES)

BASELINE = "baseline:uniform-legal-candidates@1"
CANDIDATE = "model:small-embedding-scorer@1"


def _outcome(suite: str, case_id: str, ok: bool, detail: str = "") -> Outcome:
    return Outcome(suite, case_id, bool(ok), detail)


def _guard(suite: str, case_id: str, run: Callable[[], Any]) -> Outcome:
    try:
        result = run()
    except Exception as exc:  # noqa: BLE001 - 单条失败不中止整套
        return _outcome(suite, case_id, False, f"{type(exc).__name__}: {exc}")
    if isinstance(result, Outcome):
        return result
    ok, detail = result
    return _outcome(suite, case_id, ok, detail)


# --------------------------------------------------------------------------- #
# 合成 episode 记录（评估工程的可判别输入）
# --------------------------------------------------------------------------- #
def _episode(policy_id: str, seed: int, reward: float, *, ending: str = "terminal",
             steps: int = 10, win: Optional[float] = 1.0, wall_ms: float = 5.0) -> EpisodeOutcome:
    metrics: dict[str, float] = {METRIC_REWARD: reward, METRIC_STEPS: float(steps),
                                 METRIC_WALL_MS: wall_ms}
    unavailable: list[str] = []
    if win is None:
        unavailable.append(METRIC_TERMINAL_WIN)
    else:
        metrics[METRIC_TERMINAL_WIN] = win
    return EpisodeOutcome(
        policy_id=policy_id, seed=seed, ending=ending,
        terminal_outcome=("win" if win == 1.0 else ("loss" if win == 0.0 else None)),
        metrics=metrics, unavailable=tuple(unavailable), steps=steps, wall_ms=wall_ms,
        reward_scalar=reward, reward_vector=None,
    )


def _plan(**overrides) -> EvaluationPlan:
    values = {
        "plan_id": "net:eval:probe-v1",
        "semantic_version": "1",
        "policies": (
            PolicySpec(BASELINE, "uniform_baseline", "baseline", "随机合法基线"),
            PolicySpec(CANDIDATE, "small_model", "untrained", "小模型（未训练）"),
        ),
        "baseline_policy_id": BASELINE,
        "seeds": (11, 12, 13),
        "metrics": (
            MetricSpec(METRIC_REWARD, "reward", "higher_is_better"),
            MetricSpec(METRIC_STEPS, "steps", "lower_is_better"),
            MetricSpec(METRIC_TERMINAL_WIN, "boolean", "higher_is_better"),
        ),
        "reward_profile_id": "net:reward:probe-v1",
        "reward_profile_digest": "sha256:0000000000000000000000000000000000000000000000000000000000000000",
        "executor_profile_id": "net:executor:canonical-cdf-uniform-rng@1",
        "scenario_profile": "base-spire-combat-slice-v1",
        "gym_id": "net:gym:base-spire-combat-slice-v1",
        "max_steps": 30,
    }
    values.update(overrides)
    return EvaluationPlan(**values)


# --------------------------------------------------------------------------- #
# eval-plan
# --------------------------------------------------------------------------- #
def _plan_cases() -> list:
    def plan_roundtrip_and_digest() -> tuple:
        plan = _plan()
        restored = EvaluationPlan.from_wire(plan.to_wire())
        document = plan.to_wire()
        document["evaluation_plan_version"] = "evaluation-plan@999"
        rejected = False
        try:
            EvaluationPlan.from_wire(document)
        except EvaluationPlanError:
            rejected = True
        return (restored == plan and restored.digest() == plan.digest() and rejected,
                f"digest={plan.digest()[:20]} version_rejected={rejected}")

    def baseline_must_be_declared_and_distinct() -> tuple:
        problems = []
        try:
            _plan(baseline_policy_id="nobody")
        except EvaluationPlanError as failure:
            if "基线" not in str(failure):
                problems.append(f"缺基线的报错不点名：{failure}")
        else:
            problems.append("基线不在策略列表里必须被拒绝")
        try:
            _plan(policies=(
                PolicySpec(BASELINE, "uniform_baseline", "baseline"),
                PolicySpec(BASELINE, "uniform_baseline", "baseline"),
            ))
        except EvaluationPlanError as failure:
            if "重复" not in str(failure):
                problems.append(f"重复策略 id 的报错不点名：{failure}")
        else:
            problems.append("重复策略 id 必须被拒绝")
        try:
            _plan(policies=(PolicySpec(BASELINE, "uniform_baseline", "baseline"),))
        except EvaluationPlanError:
            pass
        else:
            problems.append("只有基线没有候选必须被拒绝")
        return (not problems, "; ".join(problems))

    def seeds_and_metrics_are_validated() -> tuple:
        problems = []
        for overrides, marker in (
            ({"seeds": ()}, "种子"),
            ({"seeds": (1, 1)}, "重复"),
            ({"seeds": (1, -1)}, "非负"),
            ({"metrics": ()}, "指标"),
            ({"metrics": (MetricSpec(METRIC_REWARD, "reward", "higher_is_better"),
                          MetricSpec(METRIC_REWARD, "reward", "higher_is_better"))}, "重复"),
            ({"max_steps": 0}, "max_steps"),
            ({"bootstrap_resamples": 0}, "bootstrap_resamples"),
        ):
            try:
                _plan(**overrides)
            except EvaluationPlanError as failure:
                if marker not in str(failure):
                    problems.append(f"{overrides}: {failure}")
            else:
                problems.append(f"{overrides} 必须被拒绝")
        for unit_override, direction_override in (("parsecs", "higher_is_better"),
                                                  ("reward", "sideways")):
            try:
                MetricSpec("m", unit_override, direction_override)
            except EvaluationPlanError:
                pass
            else:
                problems.append(f"MetricSpec({unit_override!r}, {direction_override!r}) 必须被拒绝")
        return (not problems, "; ".join(problems))

    def policy_status_is_consistent() -> tuple:
        problems = []
        try:
            PolicySpec("p", "uniform_baseline", "untrained")
        except EvaluationPlanError as failure:
            if "status" not in str(failure):
                problems.append(f"基线状态不一致的报错不点名：{failure}")
        else:
            problems.append("均匀基线的 status 必须是 baseline")
        try:
            PolicySpec("p", "small_model", "halfway")
        except EvaluationPlanError:
            pass
        else:
            problems.append("未知 status 必须被拒绝")
        return (not problems, "; ".join(problems))

    def plan_digest_changes_with_seeds_and_rewards() -> tuple:
        base = _plan()
        other = _plan(seeds=(11, 12, 14))
        third = _plan(reward_profile_id="net:reward:probe-v2")
        return (base.digest() != other.digest() and base.digest() != third.digest(),
                "换保留种子或换奖励 Profile 必须是新实验")

    def metric_units_are_declared() -> tuple:
        plan = _plan()
        units = {metric.metric_id: metric.unit for metric in plan.metrics}
        return (all(unit in METRIC_UNITS for unit in units.values())
                and plan.metric(METRIC_REWARD).direction == "higher_is_better"
                and plan.metric(METRIC_STEPS).direction == "lower_is_better",
                f"units={units}")

    return [
        _guard(SUITE_PLAN, "plan/roundtrip-and-digest", plan_roundtrip_and_digest),
        _guard(SUITE_PLAN, "plan/baseline-must-exist-and-differ",
               baseline_must_be_declared_and_distinct),
        _guard(SUITE_PLAN, "plan/seeds-and-metrics-validated", seeds_and_metrics_are_validated),
        _guard(SUITE_PLAN, "plan/policy-status-consistent", policy_status_is_consistent),
        _guard(SUITE_PLAN, "plan/digest-binds-seeds-and-reward", plan_digest_changes_with_seeds_and_rewards),
        _guard(SUITE_PLAN, "plan/metric-units-declared", metric_units_are_declared),
    ]


# --------------------------------------------------------------------------- #
# eval-stats
# --------------------------------------------------------------------------- #
def _stats_cases() -> list:
    def descriptive_statistics_match_known_values() -> tuple:
        values = [1.0, 2.0, 3.0, 4.0]
        expected_mean = 2.5
        expected_sd = ((1.5 ** 2 + 0.5 ** 2 + 0.5 ** 2 + 1.5 ** 2) / 3) ** 0.5
        return (abs(mean(values) - expected_mean) < 1e-12
                and abs(standard_deviation(values) - expected_sd) < 1e-12
                and abs(percentile(values, 0.5) - 2.5) < 1e-12
                and abs(percentile(values, 0.0) - 1.0) < 1e-12
                and abs(percentile(values, 1.0) - 4.0) < 1e-12,
                f"mean={mean(values)} sd={standard_deviation(values)}")

    def single_sample_has_no_spread() -> tuple:
        return (standard_deviation([5.0]) == 0.0 and mean([5.0]) == 5.0,
                "n=1 时样本标准差为 0（调用方必须按 n 说明其无意义）")

    def bootstrap_is_reproducible_and_brackets_the_mean() -> tuple:
        values = [1.0, 2.0, 2.5, 3.0, 3.5, 4.0, 10.0]
        left = bootstrap_ci(values, resamples=500, seed=7)
        right = bootstrap_ci(values, resamples=500, seed=7)
        other = bootstrap_ci(values, resamples=500, seed=8)
        average = mean(values)
        return (left == right and left != other and left[0] <= average <= left[1],
                f"ci={left} other_seed={other} mean={average}")

    def interval_widens_with_variance_and_narrows_with_n() -> tuple:
        tight = [1.0, 1.01, 0.99, 1.0, 1.02, 0.98]
        wide = [1.0, 5.0, -3.0, 1.0, 8.0, -6.0]
        tight_ci = bootstrap_ci(tight, resamples=1000, seed=3)
        wide_ci = bootstrap_ci(wide, resamples=1000, seed=3)
        small_n = bootstrap_ci(tight[:3], resamples=1000, seed=3)
        return ((wide_ci[1] - wide_ci[0]) > (tight_ci[1] - tight_ci[0])
                and (small_n[1] - small_n[0]) > (tight_ci[1] - tight_ci[0]),
                f"tight={tight_ci} wide={wide_ci} n3={small_n}")

    def paired_comparison_uses_only_shared_seeds() -> tuple:
        plan = _plan()
        outcomes = [
            _episode(BASELINE, 11, 1.0), _episode(BASELINE, 12, 1.0), _episode(BASELINE, 13, 1.0),
            _episode(CANDIDATE, 11, 3.0), _episode(CANDIDATE, 13, 5.0),
        ]
        comparison = paired_comparison(outcomes, plan, CANDIDATE, METRIC_REWARD)
        return (comparison.pairs == 2 and abs(comparison.mean_difference - 3.0) < 1e-12
                and comparison.verdict == "improved",
                f"pairs={comparison.pairs} diff={comparison.mean_difference} "
                f"verdict={comparison.verdict}")

    def comparison_respects_metric_direction() -> tuple:
        plan = _plan()
        # 步数越少越好：候选步数更多 ⇒ 判定为 worse
        outcomes = [
            _episode(BASELINE, 11, 0.0, steps=10), _episode(BASELINE, 12, 0.0, steps=12),
            _episode(CANDIDATE, 11, 0.0, steps=14), _episode(CANDIDATE, 12, 0.0, steps=16),
        ]
        comparison = paired_comparison(outcomes, plan, CANDIDATE, METRIC_STEPS)
        return (comparison.verdict == "worse",
                f"direction={comparison.direction} diff={comparison.mean_difference} "
                f"verdict={comparison.verdict}")

    return [
        _guard(SUITE_STATS, "stats/descriptive-match-known-values",
               descriptive_statistics_match_known_values),
        _guard(SUITE_STATS, "stats/single-sample-spread", single_sample_has_no_spread),
        _guard(SUITE_STATS, "stats/bootstrap-reproducible", bootstrap_is_reproducible_and_brackets_the_mean),
        _guard(SUITE_STATS, "stats/interval-widens-and-narrows", interval_widens_with_variance_and_narrows_with_n),
        _guard(SUITE_STATS, "stats/paired-uses-shared-seeds", paired_comparison_uses_only_shared_seeds),
        _guard(SUITE_STATS, "stats/direction-respected", comparison_respects_metric_direction),
    ]


# --------------------------------------------------------------------------- #
# eval-failures
# --------------------------------------------------------------------------- #
def _failure_cases() -> list:
    def failed_episodes_stay_out_of_means() -> tuple:
        plan = _plan()
        outcomes = [
            _episode(BASELINE, 11, 1.0), _episode(BASELINE, 12, 1.0),
            _episode(CANDIDATE, 11, 3.0),
            _episode(CANDIDATE, 12, 999.0, ending="faulted"),          # 失败局：不得进均值
            _episode(CANDIDATE, 13, 999.0, ending="interrupted"),      # 同上
        ]
        summaries = aggregate(outcomes, plan)
        candidate = next(item for item in summaries if item.policy_id == CANDIDATE)
        comparison = paired_comparison(outcomes, plan, CANDIDATE, METRIC_REWARD)
        return (candidate.metrics[METRIC_REWARD].samples == 1
                and abs(candidate.metrics[METRIC_REWARD].mean - 3.0) < 1e-12
                and candidate.endings.get("faulted") == 1
                and candidate.endings.get("interrupted") == 1
                and comparison.pairs == 1 and comparison.verdict == "undecided"
                and comparison.ci_low is None,
                f"samples={candidate.metrics[METRIC_REWARD].samples} "
                f"mean={candidate.metrics[METRIC_REWARD].mean} verdict={comparison.verdict}")

    def no_usable_episodes_gives_no_metrics() -> tuple:
        plan = _plan()
        outcomes = [_episode(policy, seed, 1.0, ending="faulted")
                    for policy in (BASELINE, CANDIDATE) for seed in (11, 12, 13)]
        summaries = aggregate(outcomes, plan)
        comparisons = [paired_comparison(outcomes, plan, CANDIDATE, metric_id)
                       for metric_id in plan.metric_ids]
        return (all(summary.metrics == {} and summary.usable == 0 for summary in summaries)
                and all(item.verdict == "insufficient_samples" for item in comparisons),
                f"usable={[summary.usable for summary in summaries]}")

    def unavailable_metric_is_not_aggregated() -> tuple:
        plan = _plan()
        # 未到终局（truncated）⇒ terminal_win 不可用，但其它指标仍可用
        outcomes = [
            _episode(BASELINE, 11, 1.0, ending="truncated", win=None),
            _episode(BASELINE, 12, 1.0, ending="truncated", win=None),
            _episode(CANDIDATE, 11, 2.0, ending="truncated", win=None),
            _episode(CANDIDATE, 12, 2.0, ending="truncated", win=None),
        ]
        summaries = aggregate(outcomes, plan)
        candidate = next(item for item in summaries if item.policy_id == CANDIDATE)
        truncated_win = paired_comparison(outcomes, plan, CANDIDATE, METRIC_TERMINAL_WIN)
        return (METRIC_TERMINAL_WIN not in candidate.metrics
                and METRIC_REWARD in candidate.metrics
                and truncated_win.verdict == "insufficient_samples",
                f"metrics={sorted(candidate.metrics)} win_verdict={truncated_win.verdict}")

    def report_records_failures_and_notes() -> tuple:
        plan = _plan()
        outcomes = [
            _episode(BASELINE, 11, 1.0), _episode(BASELINE, 12, 1.0), _episode(BASELINE, 13, 1.0),
            _episode(CANDIDATE, 11, 2.0), _episode(CANDIDATE, 12, 2.0),
            _episode(CANDIDATE, 13, 2.0, ending="interrupted"),
        ]
        summaries = aggregate(outcomes, plan)
        comparisons = tuple(paired_comparison(outcomes, plan, CANDIDATE, metric_id)
                            for metric_id in plan.metric_ids)
        from ai_the_spire.evaluation import EvaluationReport
        report = EvaluationReport(
            plan=plan.to_wire(), plan_digest=plan.digest(), outcomes=tuple(outcomes),
            policies=summaries, comparisons=comparisons,
            failures=("model/seed=13: ending=interrupted",),
            notes=("策略 model 声明 status=untrained：本报告只验证评估工程，不构成任何学习改善结论",),
        )
        wire = report.to_wire()
        return (wire["failures"] == ["model/seed=13: ending=interrupted"]
                and "untrained" in wire["notes"][0]
                and report.comparison(CANDIDATE, METRIC_REWARD).pairs == 2
                and report.digest().startswith("sha256:"),
                f"failures={wire['failures']} digest={report.digest()[:20]}")

    def report_digest_is_deterministic() -> tuple:
        plan = _plan()
        outcomes = [_episode(BASELINE, seed, 1.0) for seed in plan.seeds] + \
                   [_episode(CANDIDATE, seed, 2.0) for seed in plan.seeds]
        from ai_the_spire.evaluation import EvaluationReport

        def build():
            return EvaluationReport(
                plan=plan.to_wire(), plan_digest=plan.digest(), outcomes=tuple(outcomes),
                policies=aggregate(outcomes, plan),
                comparisons=tuple(paired_comparison(outcomes, plan, CANDIDATE, metric_id)
                                  for metric_id in plan.metric_ids),
                failures=(), notes=("n",),
            )
        return (build().digest() == build().digest(), "同一输入必须得到同一报告摘要")

    def runner_requires_matching_reward_profile() -> tuple:
        from ai_the_spire.reward import standard_short_task_profile

        plan = _plan()
        profile = standard_short_task_profile({})  # 摘要与计划里的占位摘要不同
        try:
            EvaluationRunner(client=None, plan=plan, reward_profile=profile)
        except EvaluationError as failure:
            return ("奖励 Profile 与计划不一致" in str(failure), f"{failure}")
        return False, "奖励 Profile 与计划不一致必须被拒绝"

    def runner_requires_bound_model_policy() -> tuple:
        from ai_the_spire.reward import standard_short_task_profile

        profile = standard_short_task_profile({})
        plan = _plan(reward_profile_digest=profile.digest())
        runner = EvaluationRunner(client=None, plan=plan, reward_profile=profile)
        try:
            runner.run()
        except EvaluationError as failure:
            return ("还没绑定" in str(failure), f"{failure}")
        return False, "未绑定模型的 small_model 策略必须显式失败"

    return [
        _guard(SUITE_FAILURES, "failures/failed-episodes-excluded", failed_episodes_stay_out_of_means),
        _guard(SUITE_FAILURES, "failures/no-usable-episodes", no_usable_episodes_gives_no_metrics),
        _guard(SUITE_FAILURES, "failures/unavailable-metric-not-aggregated",
               unavailable_metric_is_not_aggregated),
        _guard(SUITE_FAILURES, "failures/report-records-failures-and-notes",
               report_records_failures_and_notes),
        _guard(SUITE_FAILURES, "failures/report-digest-deterministic", report_digest_is_deterministic),
        _guard(SUITE_FAILURES, "failures/reward-profile-must-match-plan",
               runner_requires_matching_reward_profile),
        _guard(SUITE_FAILURES, "failures/model-policy-must-be-bound", runner_requires_bound_model_policy),
    ]


def run_all(only: Optional[str] = None) -> list:
    outcomes: list = []
    if only is not None and only not in SUITE_NAMES:
        return outcomes
    if only in (None, SUITE_PLAN):
        outcomes.extend(_plan_cases())
    if only in (None, SUITE_STATS):
        outcomes.extend(_stats_cases())
    if only in (None, SUITE_FAILURES):
        outcomes.extend(_failure_cases())
    return outcomes


__all__ = [
    "SUITE_FAILURES",
    "SUITE_NAMES",
    "SUITE_PLAN",
    "SUITE_STATS",
    "run_all",
]
