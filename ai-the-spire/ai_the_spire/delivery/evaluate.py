"""D1-C：同一保留 seed 上的三策略对照（均匀基线 / 初始模型 / 训练候选）。

契约（任务包 §3 D1-C、§4 评估执行）：

* 三个策略**各自新开独立会话**，使用相同 Gym、限额与执行器规则（执行器种子 = 环境种子 + 固定偏移）；
* 报告平均回报、胜负/截断/故障数、确认决策数、耗时，以及**候选相对两种对照**的同种子配对差值与可用区间；
* 自助区间：重抽 1000 次、seed=20261003（来自冻结 Profile，不是代码默认值）；
* 故障不能从表中消失：失败局逐条记录、不进均值，**且让命令非零退出**；
* 小样本 / 截断 / 无改善都明确注明；训练命令从未读取 final 种子，这里才第一次用。
"""

from __future__ import annotations

import pathlib
import time
from typing import Any, Mapping

from ..evaluation import (
    EpisodeOutcome,
    EvaluationPlan,
    EvaluationReport,
    MetricSpec,
    PolicySpec,
    aggregate,
    paired_comparison,
)
from ..evaluation.runner import (
    METRIC_REWARD,
    METRIC_STEPS,
    METRIC_TERMINAL_WIN,
    METRIC_WALL_MS,
    USABLE_ENDINGS,
)
from ..runner.baseline import BASELINE_MODEL_ID, EXECUTOR_PROFILE_ID
from .config import load_profile
from .environment import observe_environment, verify_environment
from .episodes import confirmed_action_indices, run_episode
from .errors import DeliveryError, FaultError, UsageError
from .model_access import load_model_raw, model_summary
from .reward_v1 import d1_reward_profile
from .runtime import (
    CommandBudget,
    console_print,
    ensure_numpy,
    machine_report,
    open_bounded_client,
    resolve_output_dir,
    weight_bytes,
    write_json,
    write_markdown,
)

COMMAND = "evaluate"

#: 三个策略 id（进计划身份；换任一即换实验）。
INITIAL_POLICY_ID = "initial-model"
CANDIDATE_POLICY_ID = "candidate-model"

#: 对照实验身份版本。
COMPARISON_FORMAT_VERSION = "d1-comparison@1"


def run(args) -> int:
    ensure_numpy()
    run_dir = _resolve_run_dir(args.run)
    output = resolve_output_dir(args.output, command=COMMAND)
    recorded_manifest = _read_json(run_dir / "manifest.json")
    recorded_state = _read_json(run_dir / "run-state.json")

    profile = load_profile(recorded_manifest["config_path"])
    if profile.config_digest != recorded_manifest["config_digest"]:
        raise UsageError(
            f"{COMMAND}: run 的配置摘要与当前配置文件不一致（不静默迁移）："
            f"{recorded_manifest['config_digest']!r} != {profile.config_digest!r}"
        )
    if int(recorded_state.get("rounds_completed", 0)) < 1:
        raise UsageError(f"{COMMAND}: run 还没有任何成功更新的轮次，没有训练候选可比较")

    reward_profile = d1_reward_profile(
        hp_lost_weight=profile.reward["hp_lost_weight"],
        victory_weight=profile.reward["victory_weight"],
        defeat_weight=profile.reward["defeat_weight"],
        discount=profile.reward["discount"],
    )
    recorded_reward = (recorded_manifest.get("reward_profile") or {}).get("digest")
    if recorded_reward != reward_profile.digest():
        raise UsageError(
            f"{COMMAND}: 奖励 Profile 与 run 不一致（拒绝混用奖励来源）："
            f"{recorded_reward!r} != {reward_profile.digest()!r}"
        )

    seeds = tuple(profile.final_seeds() if args.split == "final" else profile.validation_seeds())
    budget = CommandBudget(limit_s=float(profile.budget["evaluate_command_time_limit_s"]),
                           command=COMMAND)
    comparison: dict[str, Any] = {
        "comparison_format_version": COMPARISON_FORMAT_VERSION,
        "command": COMMAND,
        "profile_id": profile.profile_id,
        "config_digest": profile.config_digest,
        "split": args.split,
        "seeds": list(seeds),
        "source_run": str(run_dir),
        "output_dir": str(output),
    }
    client = open_bounded_client(profile)
    try:
        environment = observe_environment(profile, client)
        verify_environment(recorded_manifest, environment, where=COMMAND)
        initial_model = load_model_raw(str(run_dir / "initial-model"), environment, where=COMMAND)
        candidate_model = load_model_raw(str(run_dir / "candidate-model"), environment, where=COMMAND)
        if initial_model.model_id == candidate_model.model_id:
            raise UsageError(
                f"{COMMAND}: 初始模型与候选模型的身份相同（{initial_model.model_id!r}）："
                "说明权重没有真正更新或身份过期，拒绝把同一次前向当成两个策略比较"
            )
        comparison["models"] = {
            "initial": {**model_summary(initial_model, state="untrained(random-init)"),
                        "weight_bytes": weight_bytes(initial_model)},
            "candidate": {**model_summary(candidate_model, state="trained(ppo)"),
                          "weight_bytes": weight_bytes(candidate_model)},
        }
        comparison["environment_identity"] = recorded_manifest["environment_identity"]

        plan = _build_plan(profile, environment, reward_profile, seeds)
        comparison["plan"] = plan.to_wire()
        comparison["plan_digest"] = plan.digest()

        outcomes: list[EpisodeOutcome] = []
        failures: list[str] = []
        sequences: dict[tuple, tuple] = {}
        wall_started = time.perf_counter()
        for spec in plan.policies:
            policy, model_id = _policy_of(spec.policy_id, initial_model, candidate_model)
            for seed in plan.seeds:
                budget.check(where=f"{COMMAND} {spec.policy_id} seed={seed} 之前")
                run = run_episode(profile, client, environment, reward_profile,
                                  environment_seed=seed,
                                  executor_seed=profile.executor_seed(seed),
                                  policy=policy, model_id=model_id, require_usable=False)
                outcome = _outcome_of(spec.policy_id, seed, run)
                outcomes.append(outcome)
                sequences[(spec.policy_id, seed)] = tuple(
                    transition.chosen_semantic_key for transition in run.result.transitions
                    if transition.status not in ("terminal", "unknown"))
                if outcome.ending not in USABLE_ENDINGS:
                    failures.append(f"{spec.policy_id}/seed={seed}: ending={outcome.ending}"
                                    f"（{run.result.ending_detail}）")
        total_wall_s = time.perf_counter() - wall_started

        # 执行器可判别性：模型概率与均匀概率的差异如果太小，抽样就会选出同一串动作——
        # 此时"三策略回报相同"是执行器分辨率的结果，不是策略等价的证据。如实报告。
        equality = {}
        for policy_id in plan.candidate_policy_ids():
            same = sum(1 for seed in plan.seeds
                       if sequences.get((policy_id, seed)) == sequences.get((BASELINE_MODEL_ID, seed)))
            equality[policy_id] = {"identical_action_sequence_seeds": same,
                                   "seeds": len(plan.seeds)}
        comparison["action_sequence_equality"] = equality

        summaries = aggregate(outcomes, plan)
        comparisons = []
        for policy_id in plan.candidate_policy_ids():
            for metric_id in plan.metric_ids:
                comparisons.append(paired_comparison(outcomes, plan, policy_id, metric_id))

        notes = _notes(profile, plan, outcomes, failures, equality)
        report = EvaluationReport(
            plan=plan.to_wire(), plan_digest=plan.digest(), outcomes=tuple(outcomes),
            policies=summaries, comparisons=tuple(comparisons), failures=tuple(failures),
            notes=tuple(notes),
            ending="completed" if not failures else "completed_with_failures",
        )
        comparison.update({
            "report": report.to_wire(),
            "report_digest": report.digest(),
            "total_wall_s": total_wall_s,
            "command_seconds": budget.elapsed_s,
            "machine": machine_report(),
            "artifacts": {
                "comparison_json": str(output / "comparison.json"),
                "comparison_markdown": str(output / "comparison.md"),
            },
        })
        if failures:
            comparison["exit_category"] = "fault"
            comparison["exit_reason"] = (
                f"有 {len(failures)} 局未产生可用指标（已逐条记录、未混入均值）：技术故障不允许阶段完成"
            )
        else:
            comparison["exit_category"] = "ok"
            comparison["exit_reason"] = "该命令完成其承诺：三策略同种子配对对照与区间"
        write_json(output / "comparison.json", comparison)
        markdown = _render_markdown(comparison, plan, summaries, comparisons, outcomes, notes)
        write_markdown(output / "comparison.md", markdown)
        console_print("\n".join(markdown))
        console_print(f"\n产物：{output / 'comparison.md'}　{output / 'comparison.json'}")
        console_print(f"退出类别：{comparison['exit_category']}　原因：{comparison['exit_reason']}")

        if failures:
            raise FaultError(comparison["exit_reason"])
        return 0
    except DeliveryError:
        raise
    finally:
        client.shutdown()


# --------------------------------------------------------------------------- #
# 计划与逐局记录
# --------------------------------------------------------------------------- #
def _build_plan(profile, environment, reward_profile, seeds) -> EvaluationPlan:
    return EvaluationPlan(
        plan_id=f"d1:{profile.profile_id}:{seeds[0]}-{seeds[-1]}",
        semantic_version="1",
        description=("D1 三策略对照：均匀基线 / 随机初始化初始模型 / 训练候选；"
                     "同保留种子、同 Gym、同限额、同执行器规则"),
        policies=(
            PolicySpec(policy_id=BASELINE_MODEL_ID, kind="uniform_baseline", status="baseline",
                       description="均匀合法候选 + 规范语义键执行器"),
            PolicySpec(policy_id=INITIAL_POLICY_ID, kind="small_model", status="untrained",
                       description="冻结种子随机初始化的小模型（未训练）"),
            PolicySpec(policy_id=CANDIDATE_POLICY_ID, kind="small_model", status="trained",
                       description="训练 run 的最后成功轮模型"),
        ),
        baseline_policy_id=BASELINE_MODEL_ID,
        seeds=tuple(seeds),
        metrics=(
            MetricSpec(METRIC_REWARD, "reward", "higher_is_better",
                       "未折扣总回报 = Σ 逐动作(-0.01*hp_lost + victory - defeat)"),
            MetricSpec(METRIC_STEPS, "steps", "descriptive",
                       "确认决策数：**描述性行为/成本**，不因更多或更少自动判策略改善或变差"),
            MetricSpec(METRIC_WALL_MS, "milliseconds", "lower_is_better", "单局墙钟耗时"),
            MetricSpec(METRIC_TERMINAL_WIN, "boolean", "higher_is_better", "是否以胜利终止"),
        ),
        reward_profile_id=reward_profile.profile_id,
        reward_profile_digest=reward_profile.digest(),
        executor_profile_id=EXECUTOR_PROFILE_ID,
        scenario_profile=environment.identities.scenario_profile,
        gym_id=environment.identities.gym_id,
        max_steps=int(profile.budget["max_decisions_per_episode"]),
        bootstrap_resamples=int(profile.evaluation["bootstrap_resamples"]),
        bootstrap_seed=int(profile.evaluation["bootstrap_seed"]),
    )


def _policy_of(policy_id: str, initial_model, candidate_model):
    if policy_id == BASELINE_MODEL_ID:
        return None, BASELINE_MODEL_ID
    if policy_id == INITIAL_POLICY_ID:
        return initial_model.policy, initial_model.model_id
    return candidate_model.policy, candidate_model.model_id


def _outcome_of(policy_id: str, seed: int, run) -> EpisodeOutcome:
    metrics: dict[str, float] = {}
    unavailable: list[str] = []
    result = run.result
    if run.reward is not None:
        metrics[METRIC_REWARD] = float(run.reward.scalar)
        metrics[METRIC_STEPS] = float(len(confirmed_action_indices(result)))
    else:
        unavailable.append(METRIC_REWARD)
        metrics[METRIC_STEPS] = float(run.confirmed_actions)
    metrics[METRIC_WALL_MS] = float(run.wall_s * 1000.0)
    if result.ending == "terminal":
        metrics[METRIC_TERMINAL_WIN] = 1.0 if run.terminal_outcome == "win" else 0.0
    else:
        unavailable.append(METRIC_TERMINAL_WIN)
    return EpisodeOutcome(
        policy_id=policy_id,
        seed=int(seed),
        ending=result.ending,
        terminal_outcome=run.terminal_outcome,
        metrics=metrics,
        unavailable=tuple(sorted(unavailable)),
        steps=int(run.confirmed_actions),
        wall_ms=float(run.wall_s * 1000.0),
        reward_scalar=None if run.reward is None else float(run.reward.scalar),
        reward_vector=None if run.reward is None else run.reward.vector.to_wire(),
    )


def _notes(profile, plan, outcomes, failures, equality) -> list[str]:
    truncated = sum(1 for outcome in outcomes if outcome.ending == "truncated")
    notes = [
        f"保留种子 {plan.seeds[0]}..{plan.seeds[-1]}（{len(plan.seeds)} 个）：这是**一个固定场景内的"
        "有限种子对照**，不声称已有多场景泛化。",
        "每个 (策略, 种子) 各自新开独立会话；三个策略共用同一 Gym、限额与执行器规则"
        f"（执行器种子 = 环境种子 + {profile.seeds['executor_offset']}）。",
        f"自助区间重抽 {plan.bootstrap_resamples} 次、seed={plan.bootstrap_seed}；"
        "配对数 < 2 或区间跨 0 时**不声称改善**。",
        "本报告不替调用方下'晋升'结论；无改善也如实交付。",
        "确认决策数是**描述性行为/成本**指标（direction=descriptive）：走得更久、操作更多或更少"
        "都不自动等于策略更好或更差，只有奖励/胜负走带方向的配对判定。",
        "训练命令从未读取 final 种子，final 结果也不驱动追加训练；本次使用的划分见上方 split。",
    ]
    if len(plan.seeds) < 5:
        notes.append(f"小样本：只有 {len(plan.seeds)} 个种子，区间很宽，任何'改善'都只能当作弱信号。")
    if truncated:
        notes.append(f"有 {truncated} 局在决策上限处截断：截断局按已确认动作计回报，胜负指标不可用。")
    if failures:
        notes.append(f"有 {len(failures)} 局未产生可用指标，已逐条记录且**未混入均值**；命令非零退出。")
    for policy_id, payload in equality.items():
        if payload["identical_action_sequence_seeds"]:
            notes.append(
                f"执行器分辨率提示：{policy_id} 在 {payload['identical_action_sequence_seeds']}/"
                f"{payload['seeds']} 个保留种子上与均匀基线走出**完全相同的动作序列**——"
                "回报相同是'同一串动作'的结果，不等于策略等价；要区分策略需要更大的概率差异或更多种子。"
            )
    return notes


# --------------------------------------------------------------------------- #
# 渲染
# --------------------------------------------------------------------------- #
def _render_markdown(comparison, plan, summaries, comparisons, outcomes, notes) -> list[str]:
    lines = [
        "# D1 三策略对照（同一保留 seed）",
        "",
        f"- run：`{comparison['source_run']}`　split：**{comparison['split']}**　"
        f"种子：{comparison['seeds'][0]}..{comparison['seeds'][-1]}（{len(comparison['seeds'])} 个）",
        f"- Gym：`{plan.gym_id}`　场景：`{plan.scenario_profile}`　"
        f"执行器：`{plan.executor_profile_id}`",
        f"- 奖励 Profile：`{plan.reward_profile_id}`（`{plan.reward_profile_digest}`）",
        f"- 计划摘要：`{comparison['plan_digest']}`　报告摘要：`{comparison['report_digest']}`",
        f"- 初始模型：`{comparison['models']['initial']['model_id']}`"
        f"（权重 `{comparison['models']['initial']['weights_digest']}`）",
        f"- 训练候选：`{comparison['models']['candidate']['model_id']}`"
        f"（权重 `{comparison['models']['candidate']['weights_digest']}`）",
        f"- 总墙钟：{comparison['total_wall_s']:.3f}s",
        "",
        "## 逐策略汇总",
        "",
        "| 策略 | 状态 | 局数 | 可用 | 完成/截断/故障 | 平均未折扣回报 | 样本标准差 | 95% 自助区间 | 平均确认决策 | 总耗时(s) |",
        "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    for summary in summaries:
        endings = summary.endings
        reward = summary.metrics.get(METRIC_REWARD)
        average = "n/a" if reward is None else f"{reward.mean:.4f}"
        deviation = "n/a" if reward is None else f"{reward.standard_deviation:.4f}"
        interval = "n/a"
        if reward is not None and reward.ci_low is not None:
            interval = f"[{reward.ci_low:.4f}, {reward.ci_high:.4f}]"
        decisions = (summary.total_decisions / summary.usable) if summary.usable else 0.0
        lines.append(
            f"| {summary.policy_id} | {summary.status} | {summary.episodes} | {summary.usable} | "
            f"{endings.get('terminal', 0)}/{endings.get('truncated', 0)}/"
            f"{endings.get('faulted', 0) + endings.get('interrupted', 0)} | {average} | {deviation} | "
            f"{interval} | {decisions:.1f} | {summary.total_wall_ms / 1000.0:.2f} |")
    lines.append("")
    lines += [
        "## 同种子配对（差值 = 策略 − 均匀基线；负的差值在回报上表示更差）",
        "",
        "| 候选策略 | 指标 | 方向 | 配对数 | 平均差值 | 区间 | 判定 |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ]
    for item in comparisons:
        interval = ("n/a" if item.ci_low is None
                    else f"[{item.ci_low:.4f}, {item.ci_high:.4f}]")
        lines.append(
            f"| {item.policy_id} | {item.metric_id} | {item.direction} | {item.pairs} | "
            f"{item.mean_difference:.4f} | {interval} | **{item.verdict}**（{item.reason}） |")
    lines.append("")
    equality = comparison.get("action_sequence_equality") or {}
    if equality:
        lines += ["## 执行器可判别性（动作序列是否与均匀基线相同）", "",
                  "| 候选策略 | 与基线动作序列相同的种子数 | 种子数 |",
                  "| --- | --- | --- |"]
        for policy_id in sorted(equality):
            payload = equality[policy_id]
            lines.append(f"| {policy_id} | {payload['identical_action_sequence_seeds']} | "
                         f"{payload['seeds']} |")
        lines += ["",
                  "> 相同序列意味着该种子下两个策略**执行了同一串动作**：回报相同是这件事的结果，"
                  "不构成策略等价的证据，也不构成改善证据。"]
        lines.append("")
    lines += ["## 逐局记录", "",
              "| 策略 | 种子 | 结束 | 终局 | 确认决策 | 回报 | 墙钟(ms) |",
              "| --- | --- | --- | --- | --- | --- | --- |"]
    for outcome in outcomes:
        reward = "n/a" if outcome.reward_scalar is None else f"{outcome.reward_scalar:.4f}"
        lines.append(f"| {outcome.policy_id} | {outcome.seed} | {outcome.ending} | "
                     f"{outcome.terminal_outcome or '-'} | {outcome.steps} | {reward} | "
                     f"{outcome.wall_ms:.1f} |")
    lines.append("")
    if comparison.get("report", {}).get("failures"):
        lines += ["## 失败局（未混入均值）", ""]
        lines += [f"- {item}" for item in comparison["report"]["failures"]]
        lines.append("")
    lines += ["## 说明", ""]
    lines += [f"- {note}" for note in notes]
    return lines


def _resolve_run_dir(raw: str) -> pathlib.Path:
    target = pathlib.Path(raw)
    resolved = (pathlib.Path.cwd() / target).resolve() if not target.is_absolute() else target.resolve()
    if not resolved.is_dir():
        raise UsageError(f"{COMMAND}: run 目录不存在：{resolved}")
    for name in ("manifest.json", "run-state.json"):
        if not (resolved / name).is_file():
            raise UsageError(f"{COMMAND}: run 目录缺少 {name}：{resolved}")
    return resolved


def _read_json(path: pathlib.Path) -> dict:
    import json

    if not path.is_file():
        raise UsageError(f"{COMMAND}: 缺少 {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as failure:
        raise UsageError(f"{COMMAND}: {path} 不是合法 JSON（{failure}）") from failure


__all__ = ["COMMAND", "run"]
