"""D2：可见的学习诊断（`python ai-the-spire/run.py diagnose --run <D1 run> --output <dir>`）。

回答三个问题，全部**基于已有产物与公开信息**，不新增训练：

1. **参考策略能不能赢**：均匀 / 原初始模型 / 原候选模型 / 明确版本化的公开信息规则基线，
   在固定诊断种子（40000～40007，执行器种子 = 环境种子 + 1000000）上同条件对照；
2. **当前模型为什么没改善**：在**同一批公开快照**上比较初始与候选模型的概率（总变差 TV），
   以及从公开记录 / action-samples / metrics 复核学习信号、更新量与成本结构；
3. **下一项最小可区分实验**：只给一个建议，写清唯一自变量、固定条件、最大采样量、评价与停止条件。

严格边界：

* **不调用 `Trainer`**、不更新任何模型、不把诊断数据写回训练集；
* **不读取 final（30000～30007）或未来保留（50000～50007）种子**，只使用 40000～40007；
* 不改原 run：所有产物写到 `--output` 的新目录；
* 规则基线是**规则基线**，不是学习模型、不是训练标签、也不是故障兜底；
* 全败同回报**不**直接判成 PPO 实现错误；规则基线获胜**不**推断模型必然能在 32 局内学会；
  区分不了的地方显式写"未测"。
"""

from __future__ import annotations

import json
import math
import pathlib
import time
from typing import Any, Mapping, Optional, Sequence

import numpy as np

from ..contracts import layers
from ..data.record import build_record
from ..data.store import EpisodeStore
from ..encoding.tensor import tensorize
from ..models.policy import semantic_keys_of
from ..reward import RewardProfile
from ..runner.baseline import BASELINE_MODEL_ID, EXECUTOR_PROFILE_ID
from ..runner.baseline import ordered_candidates
from .config import load_profile
from .console import catalog_labels, confirmed_action_count, render_episode_body, turn_of
from .environment import Environment, build_run_manifest, observe_environment, verify_environment
from .episodes import confirmed_action_indices, run_episode
from .errors import DeliveryError, FaultError, UsageError
from .model_access import load_model_raw, model_summary
from .reward_v1 import d1_reward_profile
from .rule_policy import (
    RULE_POLICY_ID,
    candidate_facts,
    make_rule_policy,
    rank_of,
    rule_spec,
)
from .runtime import (
    HOST_DLL,
    CommandBudget,
    check_storage,
    console_print,
    ensure_numpy,
    machine_report,
    open_bounded_client,
    resolve_output_dir,
    write_json,
    write_markdown,
)

COMMAND = "diagnose"

#: 诊断种子（任务包 §3 固定）。**不是**最终保留集——负责人已用它们探查过，因此不称"未见数据"。
DIAGNOSE_ENVIRONMENT_SEEDS: tuple[int, ...] = tuple(range(40000, 40008))

#: 诊断命令自身的限额（任务包 §3）。命令总限额优先于每局上限。
DIAGNOSE_COMMAND_TIME_LIMIT_S = 180.0
DIAGNOSE_MAX_CONFIRMED_ACTIONS = 4096
DIAGNOSE_STORAGE_LIMIT_BYTES = 512 * 1024 * 1024

#: 同局面探针取**最早多少局**（按 `episodes/index.jsonl` 次序）。
PROBE_EPISODE_COUNT = 8

INITIAL_POLICY_ID = "initial-model"
CANDIDATE_POLICY_ID = "candidate-model"

REQUIRED_RUN_FILES = ("manifest.json", "run-state.json", "resolved-config.json", "metrics.jsonl")
REQUIRED_RUN_DIRS = ("initial-model", "candidate-model", "episodes")


class DiagnoseBudget:
    """诊断命令的资源记账：命令墙钟 + 确认动作总量（都不允许被绕过）。"""

    def __init__(self) -> None:
        self.command = CommandBudget(limit_s=DIAGNOSE_COMMAND_TIME_LIMIT_S, command=COMMAND)
        self.actions_used = 0

    @property
    def remaining_actions(self) -> int:
        return max(DIAGNOSE_MAX_CONFIRMED_ACTIONS - self.actions_used, 0)

    def guard(self, *, where: str) -> int:
        self.command.check(where=where)
        if self.remaining_actions <= 0:
            raise FaultError(
                f"{COMMAND}: 确认动作总量已用尽（{self.actions_used}/"
                f"{DIAGNOSE_MAX_CONFIRMED_ACTIONS}）；不开启新局，保留已写产物后非零退出"
            )
        return self.remaining_actions

    def note(self, run) -> None:
        self.actions_used += int(run.confirmed_actions)


# --------------------------------------------------------------------------- #
# 入口
# --------------------------------------------------------------------------- #
def run(args) -> int:
    ensure_numpy()
    run_dir = _resolve_run_dir(args.run)
    output = resolve_output_dir(args.output, command=COMMAND)
    recorded_manifest = _read_json(run_dir / "manifest.json")
    recorded_state = _read_json(run_dir / "run-state.json")
    recorded_summary = _read_json(run_dir / "summary.json") if (run_dir / "summary.json").is_file() else {}

    profile = load_profile(recorded_manifest["config_path"])
    if profile.config_digest != recorded_manifest["config_digest"]:
        raise UsageError(
            f"{COMMAND}: run 的配置摘要与当前配置文件不一致（不静默迁移）："
            f"{recorded_manifest['config_digest']!r} != {profile.config_digest!r}"
        )
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

    budget = DiagnoseBudget()
    diagnosis: dict[str, Any] = {
        "diagnosis_format_version": "d2-diagnosis@1",
        "command": COMMAND,
        "source_run": str(run_dir),
        "source_run_state": {
            "rounds_completed": int(recorded_state.get("rounds_completed", 0)),
            "training_actions_used": int(recorded_state.get("training_actions_used", 0)),
            "candidate_model_id": recorded_state.get("candidate_model_id"),
        },
        "output_dir": str(output),
        "profile_id": profile.profile_id,
        "config_digest": profile.config_digest,
        "diagnose_seeds": list(DIAGNOSE_ENVIRONMENT_SEEDS),
        "diagnose_seed_note": (
            "40000～40007 已由负责人用于探查，**不称最终保留集**；"
            "final 30000～30007 与未来保留 50000～50007 本命令一律不访问"
        ),
        "rule_baseline": rule_spec(),
        # 输入 run 的**身份与范围**（任务包 §3：其他输入 run 也必须记录同样的身份与范围）。
        "source_run_scope": {
            "profile_id": profile.profile_id,
            "config_digest": profile.config_digest,
            "gym_id": recorded_manifest["environment_identity"]["gym_id"],
            "scenario_profile": recorded_manifest["environment_identity"]["scenario_profile"],
            "train_environment_seeds": [
                profile.train_environment_seed(round_index, episode_index)
                for round_index in range(profile.train_rounds)
                for episode_index in range(profile.episodes_per_round)],
            "validation_environment_seeds": list(profile.validation_seeds()),
            "final_environment_seeds_reserved_not_accessed": list(profile.final_seeds()),
            "future_reserved_seeds_not_accessed": list(range(50000, 50008)),
            "diagnose_environment_seeds": list(DIAGNOSE_ENVIRONMENT_SEEDS),
            "executor_seed_rule": "环境种子 + executor_offset（诊断与训练/评估同规则）",
            "executor_offset": int(profile.seeds["executor_offset"]),
            "note": ("本命令只访问 diagnose_environment_seeds；"
                     "final 30000～30007 与未来保留 50000～50007 记录在案但**未被访问**，"
                     "旧 30000～30007 已在 D1 评估过，不伪称仍是未见数据"),
        },
        "limits": {
            "command_time_limit_s": DIAGNOSE_COMMAND_TIME_LIMIT_S,
            "max_confirmed_actions": DIAGNOSE_MAX_CONFIRMED_ACTIONS,
            "max_decisions_per_episode": int(profile.budget["max_decisions_per_episode"]),
            "episode_time_limit_s": float(profile.budget["episode_time_limit_s"]),
            "request_timeout_s": float(profile.budget["request_timeout_s"]),
            "storage_limit_bytes": DIAGNOSE_STORAGE_LIMIT_BYTES,
        },
        "exit_category": "ok",
        "exit_reason": "该命令完成其承诺：四策略同条件对照 + 同局面概率探针 + 学习信号复核 + 一个接续实验建议",
    }
    write_json(output / "diagnosis.json", diagnosis)

    client = open_bounded_client(profile)
    failures: list[str] = []
    try:
        environment = observe_environment(profile, client)
        verify_environment(recorded_manifest, environment, where=COMMAND)
        catalog = environment.catalog
        labels = catalog_labels(catalog)
        initial_model = load_model_raw(str(run_dir / "initial-model"), environment, where=COMMAND)
        candidate_model = load_model_raw(str(run_dir / "candidate-model"), environment, where=COMMAND)
        if initial_model.model_id == candidate_model.model_id:
            raise UsageError(
                f"{COMMAND}: 初始与候选模型身份相同（{initial_model.model_id!r}）："
                "没有可诊断的更新，先用 D1 train 产出真正更新的 run"
            )
        diagnosis["models"] = {
            "initial": model_summary(initial_model, state="untrained(random-init)"),
            "candidate": model_summary(candidate_model, state="trained(ppo)"),
        }
        diagnosis["environment_identity"] = recorded_manifest["environment_identity"]

        # (1) 四策略同条件对照 + 参考可读对局
        comparison, reference = _four_policy_comparison(
            profile, client, environment, reward_profile, budget,
            initial_model=initial_model, candidate_model=candidate_model,
            catalog=catalog, labels=labels, output=output, failures=failures)
        diagnosis["four_policy_comparison"] = comparison
        diagnosis["reference_episode"] = reference

        # (2) 同局面概率探针
        diagnosis["same_snapshot_probe"] = _same_snapshot_probe(
            run_dir, environment, initial_model, candidate_model, catalog=catalog)

        # (3) 学习信号与更新检查
        diagnosis["learning_signal"] = _learning_signal(
            run_dir, profile, recorded_summary, initial_model, candidate_model,
            diagnosis["same_snapshot_probe"])

        # (4) 一个接续实验建议
        diagnosis["follow_up_experiment"] = _suggest_experiment(
            diagnosis["four_policy_comparison"], diagnosis["same_snapshot_probe"],
            diagnosis["learning_signal"])
        diagnosis["machine"] = machine_report()
        diagnosis["command_seconds"] = budget.command.elapsed_s
        diagnosis["confirmed_actions_used"] = budget.actions_used
        diagnosis["failures"] = failures
        if failures:
            diagnosis["exit_category"] = "fault"
            diagnosis["exit_reason"] = (
                f"有 {len(failures)} 例未产出可用结果（已逐条记录、未混入均值）：技术故障不允许阶段完成"
            )
        storage = check_storage(output, DIAGNOSE_STORAGE_LIMIT_BYTES, where=COMMAND)
        diagnosis["storage_bytes"] = storage
        write_json(output / "diagnosis.json", diagnosis)
        markdown = _render_markdown(diagnosis)
        write_markdown(output / "diagnosis.md", markdown)
        console_print("\n".join(markdown))
        console_print("")
        console_print(f"产物：{output / 'diagnosis.md'}　{output / 'diagnosis.json'}")
        console_print(f"参考对局：{output / 'reference-episode.md'}")
        console_print(f"退出类别：{diagnosis['exit_category']}　原因：{diagnosis['exit_reason']}")
        if failures:
            raise FaultError(diagnosis["exit_reason"])
        return 0
    except DeliveryError as failure:
        diagnosis["exit_category"] = failure.category
        diagnosis["exit_reason"] = failure.reason
        diagnosis["failures"] = failures
        write_json(output / "diagnosis.json", diagnosis)
        raise
    finally:
        client.shutdown()


# --------------------------------------------------------------------------- #
# (1) 四策略同条件对照
# --------------------------------------------------------------------------- #
def _four_policy_comparison(profile, client, environment: Environment, reward_profile: RewardProfile,
                            budget: DiagnoseBudget, *, initial_model, candidate_model,
                            catalog, labels, output: pathlib.Path,
                            failures: list[str]) -> tuple[dict, dict]:
    from ..evaluation import (
        EpisodeOutcome, EvaluationPlan, EvaluationReport, MetricSpec, PolicySpec,
        aggregate, paired_comparison,
    )
    from ..evaluation.runner import (
        METRIC_REWARD, METRIC_STEPS, METRIC_TERMINAL_WIN, METRIC_WALL_MS, USABLE_ENDINGS,
    )

    rule_policy = make_rule_policy()
    policies = (
        (BASELINE_MODEL_ID, None, BASELINE_MODEL_ID),
        (INITIAL_POLICY_ID, initial_model.policy, initial_model.model_id),
        (CANDIDATE_POLICY_ID, candidate_model.policy, candidate_model.model_id),
        (RULE_POLICY_ID, rule_policy, RULE_POLICY_ID),
    )
    outcomes: list[EpisodeOutcome] = []
    sequences: dict[tuple, tuple] = {}
    per_episode: list[dict] = []
    reference: dict = {}

    started = time.perf_counter()
    stopped_early = False
    for policy_id, policy, model_id in policies:
        if stopped_early:
            break
        for seed in DIAGNOSE_ENVIRONMENT_SEEDS:
            remaining = budget.guard(where=f"{COMMAND} {policy_id} seed={seed} 之前")
            cap = int(profile.budget["max_decisions_per_episode"])
            run = run_episode(profile, client, environment, reward_profile,
                              environment_seed=seed,
                              executor_seed=profile.executor_seed(seed),
                              policy=policy, model_id=model_id,
                              require_usable=False, max_steps=min(cap, remaining))
            budget.note(run)
            outcome = _outcome_of(policy_id, seed, run)
            outcomes.append(outcome)
            sequences[(policy_id, seed)] = tuple(
                transition.chosen_semantic_key for transition in run.result.transitions
                if transition.status not in ("terminal", "unknown"))
            per_episode.append({
                "policy_id": policy_id,
                "model_id": model_id,
                "seed": seed,
                "executor_seed": profile.executor_seed(seed),
                "ending": run.ending,
                "limit_reason": run.result.limit_reason,
                "terminal_outcome": run.terminal_outcome,
                "confirmed_actions": run.confirmed_actions,
                "reward_scalar": None if run.reward is None else run.reward.scalar,
                "reward_values": None if run.reward is None else dict(run.reward.values),
                "wall_s": run.wall_s,
            })
            if outcome.ending not in USABLE_ENDINGS:
                # 技术故障/未知执行：保留这一行事实与非零退出，**不替代种子、不继续补局**。
                failures.append(f"{policy_id}/seed={seed}: ending={outcome.ending}"
                                f"（{run.result.ending_detail}）")
                stopped_early = True
                break
            if policy_id == RULE_POLICY_ID and not reference:
                reference = _save_reference_episode(
                    output, run, environment, catalog, labels, profile, seed)
    total_wall_s = time.perf_counter() - started

    plan = EvaluationPlan(
        plan_id=f"d2:diagnose:{profile.profile_id}:{DIAGNOSE_ENVIRONMENT_SEEDS[0]}-"
                f"{DIAGNOSE_ENVIRONMENT_SEEDS[-1]}",
        semantic_version="1",
        description="D2 诊断四策略对照：均匀 / 初始 / 候选 / 公开信息规则基线",
        policies=(
            PolicySpec(BASELINE_MODEL_ID, "uniform_baseline", "baseline", "均匀合法候选基线"),
            PolicySpec(INITIAL_POLICY_ID, "small_model", "untrained", "原 run 的初始模型"),
            PolicySpec(CANDIDATE_POLICY_ID, "small_model", "trained", "原 run 的训练候选"),
            PolicySpec(RULE_POLICY_ID, "rule_baseline", "baseline",
                       "公开信息规则基线（规则，不是学习模型）"),
        ),
        baseline_policy_id=BASELINE_MODEL_ID,
        seeds=DIAGNOSE_ENVIRONMENT_SEEDS,
        metrics=(
            MetricSpec(METRIC_REWARD, "reward", "higher_is_better", "未折扣总回报"),
            MetricSpec(METRIC_STEPS, "steps", "descriptive",
                       "确认决策数：描述性行为/成本，不判改善"),
            MetricSpec(METRIC_WALL_MS, "milliseconds", "lower_is_better", "单局墙钟"),
            MetricSpec(METRIC_TERMINAL_WIN, "boolean", "higher_is_better", "是否终局胜利"),
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
    summaries = aggregate(outcomes, plan)
    comparisons = []
    for policy_id in plan.candidate_policy_ids():
        for metric_id in plan.metric_ids:
            comparisons.append(paired_comparison(outcomes, plan, policy_id, metric_id))

    # 规则相对：参考基线 vs 各策略（同一批种子；回答"参考能赢、模型差多远"）。
    rule_outcomes = [o for o in outcomes if o.policy_id == RULE_POLICY_ID]
    _ = rule_outcomes
    rule_relative = []
    for policy_id in (INITIAL_POLICY_ID, CANDIDATE_POLICY_ID, BASELINE_MODEL_ID):
        for metric_id in plan.metric_ids:
            rule_relative.append(_paired_against(
                outcomes, plan, baseline_policy_id=RULE_POLICY_ID, policy_id=policy_id,
                metric_id=metric_id))

    report = EvaluationReport(
        plan=plan.to_wire(), plan_digest=plan.digest(), outcomes=tuple(outcomes),
        policies=summaries, comparisons=tuple(comparisons), failures=tuple(failures),
        notes=("D2 诊断：规则基线是规则，不是学习模型；全败同回报不等于 PPO 实现错误。",),
        ending="completed" if not failures else "completed_with_failures",
    )
    equality = {}
    for policy_id, _, _ in policies:
        if policy_id == BASELINE_MODEL_ID:
            continue
        same = sum(1 for seed in DIAGNOSE_ENVIRONMENT_SEEDS
                   if sequences.get((policy_id, seed)) == sequences.get((BASELINE_MODEL_ID, seed)))
        equality[policy_id] = {"identical_action_sequence_seeds": same,
                               "seeds": len(DIAGNOSE_ENVIRONMENT_SEEDS)}
    comparison = {
        "plan": plan.to_wire(),
        "plan_digest": plan.digest(),
        "report": report.to_wire(),
        "report_digest": report.digest(),
        "rule_relative_comparisons": [item.to_wire() for item in rule_relative],
        "action_sequence_equality": equality,
        "per_episode": per_episode,
        "total_wall_s": total_wall_s,
        "stopped_early_on_failure": stopped_early,
        "confirmed_actions_used": budget.actions_used,
        "rule_episodes_won": sum(1 for item in per_episode
                                 if item["policy_id"] == RULE_POLICY_ID
                                 and item["terminal_outcome"] == "win"),
    }
    return comparison, reference


def _outcome_of(policy_id: str, seed: int, run):
    from ..evaluation import EpisodeOutcome
    from ..evaluation.runner import (
        METRIC_REWARD, METRIC_STEPS, METRIC_TERMINAL_WIN, METRIC_WALL_MS,
    )

    metrics: dict[str, float] = {}
    unavailable: list[str] = []
    result = run.result
    if run.reward is not None:
        metrics[METRIC_REWARD] = float(run.reward.scalar)
    else:
        unavailable.append(METRIC_REWARD)
    metrics[METRIC_STEPS] = float(run.confirmed_actions)
    metrics[METRIC_WALL_MS] = float(run.wall_s * 1000.0)
    if result.ending == "terminal":
        metrics[METRIC_TERMINAL_WIN] = 1.0 if run.terminal_outcome == "win" else 0.0
    else:
        unavailable.append(METRIC_TERMINAL_WIN)
    return EpisodeOutcome(
        policy_id=policy_id, seed=int(seed), ending=result.ending,
        terminal_outcome=run.terminal_outcome, metrics=metrics,
        unavailable=tuple(sorted(unavailable)), steps=int(run.confirmed_actions),
        wall_ms=float(run.wall_s * 1000.0),
        reward_scalar=None if run.reward is None else float(run.reward.scalar),
        reward_vector=None if run.reward is None else run.reward.vector.to_wire(),
    )


def _paired_against(outcomes, plan, *, baseline_policy_id: str, policy_id: str, metric_id: str):
    """以**任意策略**为对照的配对比较（复用同一套统计与判定规则）。

    `evaluation.paired_comparison` 只支持计划里的 baseline；这里用一份临时计划把对照换掉，
    不重写统计逻辑、也不改变原报告的计划身份。
    """
    import dataclasses

    from ..evaluation import paired_comparison

    swapped = dataclasses.replace(plan, baseline_policy_id=baseline_policy_id)
    return paired_comparison(outcomes, swapped, policy_id, metric_id)


def _save_reference_episode(output: pathlib.Path, run, environment: Environment, catalog,
                            labels, profile, seed: int) -> dict:
    """至少保存第一局参考策略的可读对局与公开 `EpisodeRecord`（用户能直接看"怎么赢"）。"""
    result = run.result
    identities = result.identities
    lines = [
        "# D2 参考对局（公开信息规则基线，可读）",
        "",
        f"- 规则基线：`{RULE_POLICY_ID}`（版本 {rule_spec()['version']}，**规则**不是学习模型）",
        f"- 规则优先级：{'；'.join(rule_spec()['priority'])}",
        f"- 真实 Gym：`{identities.gym_id}`　场景：`{identities.scenario_profile}`",
        f"- 环境种子：{seed}　执行器种子：{profile.executor_seed(seed)}　"
        f"执行器配置：`{identities.executor_profile_id}`",
        f"- 限额：每局最多 {profile.budget['max_decisions_per_episode']} 个确认动作、"
        f"{profile.budget['episode_time_limit_s']}s；每请求 {profile.budget['request_timeout_s']}s",
        "- 概率：选中项 one-hot（1.0），其余 0.0；决策确定，不依赖随机数",
        "",
    ]
    lines.extend(render_episode_body(result, catalog, labels))
    lines.append("## 结束")
    terminal = (result.transitions[-1].terminal
                if result.transitions and result.transitions[-1].status == "terminal" else None)
    lines.append(f"- 结束分类：**{result.ending}**")
    if terminal:
        lines.append(f"- 终局：outcome={terminal.get('outcome')} reason={terminal.get('reason_id')}")
    lines.append(f"- 确认动作数：{confirmed_action_count(result)}")
    lines.append(f"- 实际耗时：{run.wall_s:.3f}s")

    # 每一步"为什么这么选"（读公开字段，可核对）。
    lines.append("")
    lines.append("## 规则选择理由（逐动作）")
    for index, transition in enumerate(result.transitions):
        if transition.status in ("terminal", "unknown"):
            continue
        candidates = ordered_candidates(transition.snapshot)
        by_id = {entity["instance_id"]: entity
                 for entity in transition.snapshot.get("entities") or []}
        ranked = sorted(candidates, key=lambda candidate: rank_of(
            candidate.action, candidate.semantic_key_text, by_id))
        chosen = ranked[0]
        facts = candidate_facts(chosen.action)
        priority = rank_of(chosen.action, chosen.semantic_key_text, by_id)[0]
        reason = {0: "决定类：选公开 HP 最低的已发布选项",
                  1: "攻击牌：选公开 Amount 最高者",
                  2: "其它可打出的牌：语义键升序第一项",
                  3: "没有可打出的牌：结束回合",
                  4: "未知动作类型：语义键升序第一项"}[priority]
        lines.append(f"- 动作 {index}：`{facts['action_type']}`"
                     f"（引用 {facts['referenced_instance']}）⇒ {reason}")
    write_markdown(output / "reference-episode.md", lines)

    record = build_record(result, provenance={
        "command": COMMAND,
        "diagnose_seed": seed,
        "rule_policy_id": RULE_POLICY_ID,
        "rule_version": rule_spec()["version"],
        "note": "规则基线的诊断对局；不写入训练集、不作为训练标签",
    })
    store = EpisodeStore(output / "reference-episodes")
    store.write(record)
    return {
        "episode_markdown": str(output / "reference-episode.md"),
        "episode_id": record.episode_id,
        "record_digest": record.digest(),
        "ending": result.ending,
        "terminal_outcome": run.terminal_outcome,
        "confirmed_actions": confirmed_action_count(result),
        "seed": seed,
        "store_problems": store.verify_all(),
    }


# --------------------------------------------------------------------------- #
# (2) 同局面概率探针
# --------------------------------------------------------------------------- #
def _same_snapshot_probe(run_dir: pathlib.Path, environment: Environment, initial_model,
                         candidate_model, *, catalog) -> dict:
    """在**同一批公开快照**上比较初始/候选模型的概率（TV）；不比较两条不同轨迹。"""
    store = EpisodeStore(run_dir / "episodes")
    entries = store.index()[:PROBE_EPISODE_COUNT]
    positions: list[dict] = []
    problems: list[str] = []
    end_turn_with_energy = 0
    end_turn_positions = 0
    end_turn_probability_initial: list[float] = []
    end_turn_probability_candidate: list[float] = []
    tvs: list[float] = []
    episode_ids: list[str] = []

    for entry in entries:
        episode_id = entry["episode_id"]
        episode_ids.append(episode_id)
        record = store.read(episode_id)
        for index, transition in enumerate(record["transitions"]):
            if transition["outcome"] == "terminal":
                continue
            snapshot = transition["snapshot"]
            if not (snapshot.get("legal_actions") or []):
                continue
            semantic_keys = semantic_keys_of(snapshot)
            try:
                observation = tensorize(snapshot, environment.manifest, catalog=catalog)
                old = initial_model.evaluate_tensorized(
                    observation, semantic_keys=semantic_keys, catalog=catalog)
                new = candidate_model.evaluate_tensorized(
                    observation, semantic_keys=semantic_keys, catalog=catalog)
            except Exception as failure:  # noqa: BLE001 - 逐局面记录，不中断整段诊断
                problems.append(f"{episode_id} step {index}: 概率求值失败 {failure}")
                continue
            p_old, p_new = old.by_semantic_key(), new.by_semantic_key()
            if set(p_old) != set(p_new):
                problems.append(f"{episode_id} step {index}: 两模型候选集不一致")
                continue
            tv = 0.5 * sum(abs(float(p_new[key]) - float(p_old[key])) for key in p_old)
            tvs.append(tv)
            end_turn_keys = [key for key in p_old if _action_type_of(key).endswith("EndTurn")]
            if end_turn_keys:
                end_turn_positions += 1
                key = end_turn_keys[0]
                end_turn_probability_initial.append(float(p_old[key]))
                end_turn_probability_candidate.append(float(p_new[key]))
                chosen_is_end_turn = bool(transition["semantic_key"] == key)
                energy = _player_field(snapshot, "net:field:base-spire:Player.Energy")
                if chosen_is_end_turn and energy is not None and energy > 0:
                    end_turn_with_energy += 1
            positions.append({
                "episode_id": episode_id,
                "step": index,
                "observation_id": transition["observation_id"],
                "turn": turn_of(snapshot),
                "candidates": len(p_old),
                "tv": tv,
                "chosen_semantic_key": transition["semantic_key"],
                "behavior_probability": transition["behavior_probability"],
                "player_hp": _player_field(snapshot, "net:field:base-spire:Player.Hp"),
                "player_energy": _player_field(snapshot, "net:field:base-spire:Player.Energy"),
                "monster_min_hp": _monster_min_hp(snapshot),
                "probabilities": [
                    {"semantic_key": key, "p_initial": float(p_old[key]),
                     "p_candidate": float(p_new[key])} for key in sorted(p_old)],
            })

    def _stats(values: Sequence[float]) -> Optional[dict]:
        if not values:
            return None
        return {"count": len(values), "mean": float(np.mean(values)),
                "min": float(np.min(values)), "max": float(np.max(values)),
                "p50": float(np.percentile(values, 50)),
                "p90": float(np.percentile(values, 90))}

    return {
        "definition": "TV = 0.5 × Σ|p_candidate − p_initial|，在**同一份公开快照**上求两种模型的概率",
        "episode_selection": (f"按 episodes/index.jsonl 次序取最早 {PROBE_EPISODE_COUNT} 局"),
        "episode_ids": episode_ids,
        "episode_seeds": [int(store.read(entry_id)["identities"]["seed"]) for entry_id in episode_ids],
        "positions": len(positions),
        "models": {"initial": initial_model.model_id, "candidate": candidate_model.model_id},
        "tv": _stats(tvs),
        "tv_max_position": (max(positions, key=lambda item: item["tv"]) if positions else None),
        "end_turn_positions": end_turn_positions,
        "end_turn_probability_initial": _stats(end_turn_probability_initial),
        "end_turn_probability_candidate": _stats(end_turn_probability_candidate),
        "end_turn_chosen_with_energy_left": end_turn_with_energy,
        "per_position": positions,
        "problems": problems,
    }


# --------------------------------------------------------------------------- #
# (3) 学习信号与更新检查
# --------------------------------------------------------------------------- #
def _learning_signal(run_dir: pathlib.Path, profile, recorded_summary: Mapping[str, Any],
                     initial_model, candidate_model, probe: Mapping[str, Any]) -> dict:
    store = EpisodeStore(run_dir / "episodes")
    episodes = []
    for entry in store.index():
        record = store.read(entry["episode_id"])
        transitions = record["transitions"]
        first = transitions[0]["snapshot"] if transitions else {}
        last = transitions[-1]["snapshot"] if transitions else {}
        episodes.append({
            "episode_id": entry["episode_id"],
            "seed": int(record["identities"]["seed"]),
            "round": record["provenance"].get("round"),
            "ending_class": record["ending"]["class"],
            "confirmed_transitions": record["summary"]["confirmed_transitions"],
            "turns": turn_of(last),
            "player_hp_first": _player_field(first, "net:field:base-spire:Player.Hp"),
            "player_hp_last": _player_field(last, "net:field:base-spire:Player.Hp"),
            "monster_hp_first": _monster_total_hp(first),
            "monster_hp_last": _monster_total_hp(last),
            "committed_to_training": record["provenance"].get("committed_to_training"),
        })

    metrics = [json.loads(line) for line
               in (run_dir / "metrics.jsonl").read_text(encoding="utf-8").splitlines() if line.strip()]
    rounds = [item for item in metrics if item.get("kind") == "update"]
    validations = [item for item in metrics if item.get("kind") == "validation"]
    episode_lines = [item for item in metrics if item.get("kind") == "episode"]

    # 逐动作样本（每轮检查点里的 action-samples.jsonl）
    samples: list[dict] = []
    for path in sorted((run_dir / "checkpoints").glob("round-*/action-samples.jsonl")):
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.strip():
                samples.append(json.loads(line))

    def _stats(values: Sequence[float]) -> Optional[dict]:
        clean = [float(value) for value in values if value is not None and math.isfinite(float(value))]
        if not clean:
            return None
        array = np.asarray(clean, dtype=np.float64)
        return {"count": int(array.size), "mean": float(array.mean()),
                "min": float(array.min()), "max": float(array.max()),
                "std": float(array.std()), "p05": float(np.percentile(array, 5)),
                "p50": float(np.percentile(array, 50)), "p95": float(np.percentile(array, 95))}

    advantage = [item["advantage"] for item in samples]
    returns = [item["return_G"] for item in samples]
    values = [item["value_old"] for item in samples]
    rewards = [item["reward_scalar"] for item in samples]
    pi_mu_mismatch = [item for item in samples
                      if abs(float(item["pi_selected"]) - float(item["mu_selected"])) > 1e-12]

    # 参数分组变化（按参数名前缀）
    initial_weights = _weights_of(initial_model)
    candidate_weights = _weights_of(candidate_model)
    groups: dict[str, dict] = {}
    for name in sorted(initial_weights):
        group = name.split("/", 1)[0]
        delta = candidate_weights[name] - initial_weights[name]
        entry = groups.setdefault(group, {"parameters": 0, "delta_l2": 0.0, "initial_l2": 0.0,
                                          "max_abs_delta": 0.0})
        entry["parameters"] += int(initial_weights[name].size)
        entry["delta_l2"] += float(np.sum(delta * delta))
        entry["initial_l2"] += float(np.sum(initial_weights[name] ** 2))
        entry["max_abs_delta"] = max(entry["max_abs_delta"], float(np.max(np.abs(delta))))
    for group, entry in groups.items():
        entry["delta_l2"] = math.sqrt(entry["delta_l2"])
        entry["initial_l2"] = math.sqrt(entry["initial_l2"])
        entry["relative_change"] = (entry["delta_l2"] / entry["initial_l2"]
                                    if entry["initial_l2"] > 0 else None)
        entry["changed"] = entry["max_abs_delta"] > 0.0

    # "更短的失败局看起来更好"这一假设的可定位检查：折扣回报 vs 局长度。
    # 局长度取自**公开记录**（记录格式一直有 `summary.confirmed_transitions`），
    # 折扣回报取自 metrics 行；D1.1 之前的旧 run 没有新字段时按"未测"处理，不猜。
    episode_lines_by_order = episode_lines
    paired = []
    for record_item, line in zip(episodes, episode_lines_by_order):
        value = line.get("discounted_return")
        if value is not None:
            paired.append((record_item["confirmed_transitions"], value))
    correlation = None
    if len(paired) >= 3 and len({item[0] for item in paired}) > 1:
        lengths = np.asarray([item[0] for item in paired], dtype=np.float64)
        values_array = np.asarray([item[1] for item in paired], dtype=np.float64)
        if lengths.std() > 0 and values_array.std() > 0:
            correlation = float(np.corrcoef(lengths, values_array)[0, 1])

    undiscounted = [item.get("reward_scalar") for item in episode_lines]
    timings = recorded_summary.get("timings") or {}
    # 占比按**分项**求（command_s 是这些分项的和，放进分母会重复计数）。
    component_keys = ("episodes_s", "sample_build_s", "update_s", "validation_s")
    component_total = sum(float(timings.get(key, 0.0)) for key in component_keys) or None
    timing_shares = ({key: float(timings.get(key, 0.0)) / component_total
                      for key in component_keys} if component_total else None)
    if timing_shares and "command_s" in timings:
        timing_shares["unaccounted_s"] = max(
            float(timings["command_s"]) / component_total
            - sum(value for key, value in timing_shares.items() if key != "unaccounted_s"), 0.0)
    # 旧 run 可能缺 D1.1 之后才写入的字段：显式列出"哪些字段在来源 run 里不存在"。
    missing_metrics_fields = sorted({
        key
        for key in ("confirmed_transitions", "unknown_actions", "usable_for_training",
                    "reward_problem", "failure_reason", "budget")
        if not any(key in item for item in episode_lines)
    })

    return {
        "episodes": episodes,
        "episode_summary": {
            "total": len(episodes),
            "terminal_win": sum(1 for item in episodes if item["ending_class"] == "terminal_win"),
            "terminal_loss": sum(1 for item in episodes if item["ending_class"] == "terminal_loss"),
            "truncated": sum(1 for item in episodes if item["ending_class"] == "truncated"),
            "other": sum(1 for item in episodes
                         if item["ending_class"] not in
                         ("terminal_win", "terminal_loss", "truncated")),
            "undiscounted_return_values": sorted({item.get("reward_scalar")
                                                  for item in episode_lines
                                                  if item.get("reward_scalar") is not None}),
            "player_hp_first_values": sorted({item["player_hp_first"] for item in episodes
                                              if item["player_hp_first"] is not None}),
            "player_hp_last_values": sorted({item["player_hp_last"] for item in episodes
                                             if item["player_hp_last"] is not None}),
        },
        "rounds": [
            {"round": item.get("round"), "samples": item.get("samples"),
             "confirmed_actions": item.get("confirmed_actions"),
             "loss": item.get("loss"), "policy_loss": item.get("policy_loss"),
             "value_loss": item.get("value_loss"), "entropy": item.get("entropy"),
             "grad_norm": item.get("grad_norm"), "grad_scale": item.get("grad_scale"),
             "clip_fraction": item.get("clip_fraction"),
             "optimizer_steps": item.get("optimizer_steps"),
             "round_s": item.get("round_s"), "update_s": item.get("update_s")}
            for item in rounds],
        "gradient": {
            "max_grad_norm": float(profile.ppo["max_grad_norm"]),
            "learning_rate": float(profile.ppo["learning_rate"]),
            "epochs": int(profile.ppo["epochs"]),
            "clip_ratio": float(profile.ppo["clip_ratio"]),
            "rounds_with_clipping": sum(1 for item in rounds
                                        if float(item.get("grad_scale", 1.0)) < 1.0),
            "rounds_total": len(rounds),
            "grad_norm_stats": _stats([item.get("grad_norm") for item in rounds]),
        },
        "validation": [
            {"stage": item.get("stage"),
             "policies": {label: {"mean_reward": payload.get("mean_reward"),
                                  "usable": payload.get("usable"),
                                  "decisions": payload.get("decisions"),
                                  "endings": payload.get("endings")}
                          for label, payload in (item.get("policies") or {}).items()}}
            for item in validations],
        "action_samples": {
            "count": len(samples),
            "advantage": _stats(advantage),
            "return_G": _stats(returns),
            "value_old": _stats(values),
            "reward_scalar": _stats(rewards),
            "positive_advantage_fraction": (
                float(sum(1 for value in advantage if value > 0) / len(advantage))
                if advantage else None),
            "pi_equals_mu_all_samples": not pi_mu_mismatch,
            "pi_mu_mismatch_count": len(pi_mu_mismatch),
            "advantage_by_round": {
                str(item.get("round")): _stats([sample["advantage"] for sample in samples
                                                if sample.get("round") == item.get("round")])
                for item in rounds},
            "parameters_by_group": groups,
        },
        "discount_signal": {
            "gamma": float(profile.reward["discount"]),
            "episode_length_vs_discounted_return_correlation": correlation,
            "direction": (
                None if correlation is None else
                "局越长、折扣回报越高（失败惩罚被折得更远）" if correlation > 0 else
                "局越短、折扣回报越高（更早结束看起来更好）"),
            "note": ("在本 run 里每局都失败且未折扣回报相同，于是**折扣本身**成了唯一可学方向："
                     "它奖励的是'把失败惩罚推到更远的未来'，而不是获胜。这是可定位的信号方向问题，"
                     "不等于 PPO 实现错误，也不等于没有更新"),
        },
        "costs": {
            "timings": {key: float(value) for key, value in timings.items()},
            "timing_shares": timing_shares,
            "timing_share_basis": ("按 episodes_s / sample_build_s / update_s / validation_s 分项求和；"
                                   "command_s 是这些分项之和，不重复计入分母"),
            "command_seconds": recorded_summary.get("command_seconds"),
            "episodes_total": len(episodes),
            "training_actions_used": recorded_summary.get("training_actions_used"),
        },
        "probability_change_cross_reference": {
            "source": "same_snapshot_probe",
            "tv_mean": (probe.get("tv") or {}).get("mean"),
            "tv_max": (probe.get("tv") or {}).get("max"),
            "note": "这是同一批公开快照上的概率变化，**不是**两条不同轨迹之间的比较",
        },
        "metrics_fields_missing_in_source_run": missing_metrics_fields,
        "metrics_fields_missing_note": (
            "来源 run 的 metrics.jsonl 没有这些字段（D1.1 之后才写入）：相关结论标为未测，不用默认值补"
            if missing_metrics_fields else "来源 run 的 metrics.jsonl 含全部诊断所需字段"
        ),
    }


# --------------------------------------------------------------------------- #
# (4) 一个接续实验建议
# --------------------------------------------------------------------------- #
def _suggest_experiment(comparison: Mapping[str, Any], probe: Mapping[str, Any],
                        signal: Mapping[str, Any]) -> dict:
    """从已测事实出发给**一个**最小可区分实验；不确定的地方如实写"未测"。"""
    rule_wins = int(comparison.get("rule_episodes_won") or 0)
    seeds = len(comparison.get("plan", {}).get("seeds") or [])
    tv_mean = (probe.get("tv") or {}).get("mean")
    tv_max = (probe.get("tv") or {}).get("max")
    undiscounted = signal["episode_summary"].get("undiscounted_return_values") or []
    hp_last = signal["episode_summary"].get("player_hp_last_values") or []
    correlation = signal["discount_signal"].get("episode_length_vs_discounted_return_correlation")
    positive_fraction = signal["action_samples"].get("positive_advantage_fraction")
    pi_ok = signal["action_samples"].get("pi_equals_mu_all_samples")

    excluded, undecided = [], []
    excluded.append(f"**任务不可完成**：规则基线在 {rule_wins}/{seeds} 个诊断种子上获胜"
                    "（受控场景内存在可获胜路径）。这不证明规则最优，也不证明 32 局足以学会。")
    if pi_ok:
        excluded.append("**采样/执行概率错配**：765 条（此处 "
                        f"{signal['action_samples'].get('count')} 条）训练样本的 π 与 μ 逐条相等，"
                        "轨迹概率与冻结模型重算一致（D1.1 的 SOURCE 检查另行断言）。")
    if tv_mean is not None and tv_mean > 0:
        excluded.append("**完全没有更新**：初始/候选模型在同一批公开快照上的 TV 均值 "
                        f"{tv_mean:.6f}、最大 {tv_max if tv_max is None else round(tv_max, 6)}，"
                        "参数分组确有变化。")
    if len(undiscounted) == 1 and hp_last and max(hp_last) == 0:
        undecided.append("**学习信号不能区分行为**：全部局都失败且未折扣回报只有 "
                         f"{undiscounted[0]}，玩家 HP 终值均 {hp_last}；"
                         "失败策略之间几乎没有可区分的回报。")
    if positive_fraction is not None:
        undecided.append(f"**优势符号分布**：正优势样本占 {positive_fraction:.4f}；"
                         "若正优势系统性地与局长度同向，梯度就会推'拖长失败'而不是'获胜'——"
                         "本轮没有做'优势符号 vs 局内位置'的相关检查。")
    if correlation is not None:
        direction = signal["discount_signal"].get("direction") or "方向未判定"
        undecided.append(f"**折扣方向**：局长度与折扣回报的相关系数 {correlation:.4f}"
                         f"（γ={signal['discount_signal']['gamma']}，{direction}）；"
                         "在每局都失败且未折扣回报相同时，折扣会成为唯一可学的方向。")
    undecided.append("**表示遗漏**：未测——本轮没有做'同一策略在成对可区分局面上的编码差异'检查。")
    undecided.append("**更新幅度或裁剪影响**：部分未测——"
                     f"裁剪轮数 {signal['gradient']['rounds_with_clipping']}/"
                     f"{signal['gradient']['rounds_total']}、lr={signal['gradient']['learning_rate']}；"
                     "没有做'关掉裁剪/改 lr 后信号是否改变'的对照。")

    gamma = signal["discount_signal"]["gamma"]
    direction_text = signal["discount_signal"].get("direction") or "方向未判定"
    hypothesis = (
        "当前工程奖励下 32 局全部失败且未折扣回报完全相同"
        f"（{undiscounted}），可学的方向主要来自**折扣**本身："
        f"实测局长度与折扣回报的相关系数 {correlation if correlation is None else round(correlation, 4)}"
        f"（{direction_text}）。这属于**学习信号的方向/区分度**问题，"
        "不是 PPO 实现错误，也不是'没有更新'（参数分组全部变化，同局面概率 TV 均值 "
        f"{tv_mean if tv_mean is None else round(tv_mean, 6)}）。"
    )
    experiment = {
        "hypothesis": hypothesis,
        "single_independent_variable": {
            "name": "reward.discount（折扣因子 γ）",
            "from": f"{gamma}",
            "to": "1.0（未折扣）",
            "why_this_one": (
                "它只改'同一批真实轨迹如何变成回报'，不改环境、不改执行器、不改模型、不改种子、"
                "不改 PPO 超参；实测折扣回报与局长度强相关，把 γ 设为 1.0 会让所有失败局的未折扣"
                "回报完全相同（优势只剩 −V_old），从而把'样本不足'与'折扣造成的伪方向'分开"),
        },
        "fixed_conditions": [
            "同一冻结 Profile 的其它字段（Gym/宿主开关/种子规则/PPO 超参/限额）全部不变",
            "同一初始模型（model_init seed）与同一批训练种子 10000～10031",
            "同一 validation 种子 20000～20007；不使用 final 30000～30007 与未来保留 50000～50007",
            "同一执行器与同一评估协议（同种子配对 + 自助区间）",
        ],
        "max_sampling": {
            "train_rounds": 8,
            "episodes_per_round": 4,
            "max_train_actions": 4096,
            "command_time_limit_s": 900.0,
            "note": "与 D1 完全相同的预算；本实验不靠扩大预算区分假设",
        },
        "evaluation": [
            "主指标：validation 保留种子上三种策略（均匀/初始/当前）的平均**折扣**回报曲线",
            "判别指标：候选相对均匀基线的同种子配对差值与自助区间（只有奖励/胜负带方向）",
            "行为指标（描述性，不判优劣）：确认决策数的变化方向——γ=1.0 后'拖长失败'不再有回报；"
            "若决策数不再系统性变化，说明伪方向确由折扣造成",
            "过程指标：正优势样本占比、局长度与折扣回报的相关系数（本 run 为 "
            f"{correlation if correlation is None else round(correlation, 4)}）",
        ],
        "stop_conditions": [
            "跑满 8 轮或 900 秒或 4096 个训练动作即停止；不加轮、不调参",
            "任一技术故障/未知执行：保留已确认事实、不提交该轮、非零退出",
            "若出现首个胜利局：如实记录，但**不算**'已学会'（仍是单场景有限种子）",
        ],
        "what_it_would_show": {
            "if_discount_is_the_cause": "γ=1.0 后决策数不再系统性变化，且奖励曲线仍无改善 ⇒ "
                                        "折扣是当前唯一可学方向的来源，瓶颈是奖励区分度；"
                                        "需要负责人冻结新的奖励 Profile（属 W-07 决策）",
            "if_discount_is_not_the_cause": "决策数仍系统性变化且曲线无改善 ⇒ 折扣不是主因，"
                                            "下一个候选假设是表示遗漏（需要成对可区分局面的编码检查）",
        },
        "decided_by": "负责人（本命令只给建议；未获冻结不自动开训、不自动调参）",
    }
    return {
        "excluded_hypotheses": excluded,
        "undecided_hypotheses": undecided,
        "experiment": experiment,
    }


# --------------------------------------------------------------------------- #
# 渲染
# --------------------------------------------------------------------------- #
def _render_markdown(diagnosis: Mapping[str, Any]) -> list[str]:
    comparison = diagnosis["four_policy_comparison"]
    report = comparison["report"]
    probe = diagnosis["same_snapshot_probe"]
    signal = diagnosis["learning_signal"]
    suggestion = diagnosis["follow_up_experiment"]
    lines = [
        "# D2 学习诊断",
        "",
        f"- run：`{diagnosis['source_run']}`　"
        f"已提交轮数 {diagnosis['source_run_state']['rounds_completed']}　"
        f"训练动作 {diagnosis['source_run_state']['training_actions_used']}",
        f"- 诊断种子：{diagnosis['diagnose_seeds'][0]}～{diagnosis['diagnose_seeds'][-1]}"
        f"（{len(diagnosis['diagnose_seeds'])} 个）　"
        f"执行器种子 = 环境种子 + 1000000",
        f"- 来源 run 范围：train {diagnosis['source_run_scope']['train_environment_seeds'][0]}～"
        f"{diagnosis['source_run_scope']['train_environment_seeds'][-1]}、"
        f"validation {diagnosis['source_run_scope']['validation_environment_seeds'][0]}～"
        f"{diagnosis['source_run_scope']['validation_environment_seeds'][-1]}；"
        f"final {diagnosis['source_run_scope']['final_environment_seeds_reserved_not_accessed'][0]}～"
        f"{diagnosis['source_run_scope']['final_environment_seeds_reserved_not_accessed'][-1]} 与"
        f"未来保留 50000～50007 **未访问**",
        f"- 初始模型：`{diagnosis['models']['initial']['model_id']}`　"
        f"候选模型：`{diagnosis['models']['candidate']['model_id']}`",
        f"- 计划摘要：`{comparison['plan_digest']}`　报告摘要：`{comparison['report_digest']}`",
        f"- 规则基线：`{diagnosis['rule_baseline']['rule_policy_id']}`"
        f"（版本 {diagnosis['rule_baseline']['version']}）",
        f"- 限额：命令 {diagnosis['limits']['command_time_limit_s']}s、"
        f"{diagnosis['limits']['max_confirmed_actions']} 个确认动作、每局 "
        f"{diagnosis['limits']['max_decisions_per_episode']} 个动作 /"
        f"{diagnosis['limits']['episode_time_limit_s']}s；实际用 "
        f"{diagnosis.get('confirmed_actions_used')} 个动作 / "
        f"{diagnosis.get('command_seconds', 0.0):.3f}s",
        "",
        "> 规则基线是**规则**：不是学习模型、不是训练标签、也不是故障兜底。",
        "> 全败同回报**不**等于 PPO 实现错误；规则基线获胜**不**推断模型能在 32 局内学会。",
        "",
        "## 1. 四策略同条件对照",
        "",
        "| 策略 | 状态 | 局数 | 可用 | 完成/截断/故障 | 平均未折扣回报 | 平均确认决策 | 平均墙钟(ms) |",
        "| --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    for summary in report["policies"]:
        endings = summary["endings"]
        reward = summary["metrics"].get("net:metric:reward_scalar")
        steps = summary["metrics"].get("net:metric:steps")
        wall = summary["metrics"].get("net:metric:wall_ms")
        lines.append(
            f"| {summary['policy_id']} | {summary['status']} | {summary['episodes']} | "
            f"{summary['usable']} | {endings.get('terminal', 0)}/{endings.get('truncated', 0)}/"
            f"{endings.get('faulted', 0) + endings.get('interrupted', 0)} | "
            f"{'n/a' if reward is None else f'{float(reward['mean']):.4f}'} | "
            f"{'n/a' if steps is None else f'{float(steps['mean']):.2f}'} | "
            f"{'n/a' if wall is None else f'{float(wall['mean']):.1f}'} |")
    lines += ["",
              f"规则基线获胜 **{comparison['rule_episodes_won']}/{len(diagnosis['diagnose_seeds'])}** "
              f"个诊断种子；第一局的逐决策可读对局在 `reference-episode.md`"
              f"（episode_id `{diagnosis['reference_episode'].get('episode_id')}`）。",
              "",
              "### 同种子配对（对照 = 均匀基线）", "",
              "| 候选 | 指标 | 方向 | 配对 | 平均差值 | 区间 | 判定 |",
              "| --- | --- | --- | --- | --- | --- | --- |"]
    for item in report["comparisons"]:
        interval = "n/a" if item["ci_low"] is None else f"[{float(item['ci_low']):.4f}, {float(item['ci_high']):.4f}]"
        lines.append(f"| {item['policy_id']} | {item['metric_id']} | {item['direction']} | "
                     f"{item['pairs']} | {float(item['mean_difference']):.4f} | {interval} | "
                     f"**{item['verdict']}** |")
    lines += ["", "### 相对规则基线的配对（对照 = 规则基线）", "",
              "| 策略 | 指标 | 方向 | 配对 | 平均差值 | 区间 | 判定 |",
              "| --- | --- | --- | --- | --- | --- | --- |"]
    for item in comparison["rule_relative_comparisons"]:
        interval = "n/a" if item["ci_low"] is None else f"[{float(item['ci_low']):.4f}, {float(item['ci_high']):.4f}]"
        lines.append(f"| {item['policy_id']} | {item['metric_id']} | {item['direction']} | "
                     f"{item['pairs']} | {float(item['mean_difference']):.4f} | {interval} | "
                     f"**{item['verdict']}** |")
    lines += ["", "### 执行器分辨率（动作序列是否与均匀基线相同）", "",
              "| 策略 | 相同的种子数 | 种子数 |", "| --- | --- | --- |"]
    for policy_id, payload in sorted(comparison["action_sequence_equality"].items()):
        lines.append(f"| {policy_id} | {payload['identical_action_sequence_seeds']} | "
                     f"{payload['seeds']} |")
    lines += ["", "### 逐局", "",
              "| 策略 | 种子 | 结束 | 终局 | 确认决策 | 未折扣回报 | 墙钟(ms) |",
              "| --- | --- | --- | --- | --- | --- | --- |"]
    for item in comparison["per_episode"]:
        reward = item["reward_scalar"]
        lines.append(f"| {item['policy_id']} | {item['seed']} | {item['ending']} | "
                     f"{item['terminal_outcome'] or '-'} | {item['confirmed_actions']} | "
                     f"{'n/a' if reward is None else f'{reward:.4f}'} | "
                     f"{item['wall_s'] * 1000.0:.1f} |")

    lines += ["", "## 2. 同局面概率探针", "",
              f"- 定义：{probe['definition']}",
              f"- 选取：{probe['episode_selection']}（episode_ids {len(probe['episode_ids'])} 条，"
              f"seeds {probe['episode_seeds']}）",
              f"- 有合法候选的公开快照：**{probe['positions']}** 个"]
    tv = probe.get("tv") or {}
    if tv:
        lines.append(f"- TV：均值 **{tv['mean']:.6f}**、最大 **{tv['max']:.6f}**、"
                     f"p50 {tv['p50']:.6f}、p90 {tv['p90']:.6f}")
    end_initial = probe.get("end_turn_probability_initial") or {}
    end_candidate = probe.get("end_turn_probability_candidate") or {}
    if end_initial and end_candidate:
        lines.append(f"- 结束回合候选局面 {probe['end_turn_positions']} 个：p(EndTurn) 均值 "
                     f"初始 {end_initial['mean']:.6f} → 候选 {end_candidate['mean']:.6f}")
    lines.append(f"- 带剩余能量仍选择结束回合：**{probe['end_turn_chosen_with_energy_left']}** 次")
    if probe.get("problems"):
        lines.append(f"- 逐局面问题：{len(probe['problems'])} 条（已在 JSON 中逐条记录）")
    lines += ["",
              "> 这是在**同一份公开快照**上比较初始/候选模型，不是比较两条不同轨迹；"
              "概率是 softmax 输出，**不是** Q 值，也不是通关概率。", ""]

    lines += ["## 3. 学习信号与更新检查", "",
              f"- 局：{signal['episode_summary']}",
              f"- 优势/回报/V：{signal['action_samples']['advantage']}",
              f"- 正优势占比：{signal['action_samples']['positive_advantage_fraction']}　"
              f"π=μ（全部样本）：{signal['action_samples']['pi_equals_mu_all_samples']}",
              f"- 折扣信号：{signal['discount_signal']}",
              f"- 梯度/裁剪：{signal['gradient']}",
              f"- 成本占比：{signal['costs']['timing_shares']}",
              "",
              "| 轮 | 样本 | 确认动作 | loss | policy | value | entropy | grad_norm | grad_scale | 裁剪比例 | opt steps | 更新(s) |",
              "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
    for item in signal["rounds"]:
        lines.append(
            f"| {item['round']} | {item['samples']} | {item['confirmed_actions']} | "
            f"{item['loss']:.6f} | {item['policy_loss']:.6f} | {item['value_loss']:.6f} | "
            f"{item['entropy']:.6f} | {item['grad_norm']:.6f} | {item['grad_scale']:.6f} | "
            f"{item['clip_fraction']:.4f} | {item['optimizer_steps']} | {item['update_s']:.3f} |")
    lines += ["", "### 参数分组变化（初始 → 候选）", "",
              "| 组 | 参数 | ΔL2 | 初始 L2 | 相对变化 | 有变化 |",
              "| --- | --- | --- | --- | --- | --- |"]
    for group, entry in sorted(signal["action_samples"]["parameters_by_group"].items()):
        relative = ("n/a" if entry["relative_change"] is None else f"{entry['relative_change']:.6f}")
        lines.append(f"| {group} | {entry['parameters']} | {entry['delta_l2']:.6f} | "
                     f"{entry['initial_l2']:.6f} | {relative} | {entry['changed']} |")

    lines += ["", "### validation 曲线（原训练 run）", "",
              "| 阶段 | 策略 | 平均未折扣回报 | 可用局 | 确认决策 |", "| --- | --- | --- | --- | --- |"]
    for item in signal["validation"]:
        for label, payload in sorted(item["policies"].items()):
            mean_reward = payload["mean_reward"]
            lines.append(f"| {item['stage']} | {label} | "
                         f"{'n/a' if mean_reward is None else f'{mean_reward:.4f}'} | "
                         f"{payload['usable']} | {payload['decisions']} |")

    experiment = suggestion["experiment"]
    lines += ["", "## 4. 一个接续实验建议", "",
              f"**假设**：{experiment['hypothesis']}", "",
              "**已排除**：", ""]
    lines += [f"- {item}" for item in suggestion["excluded_hypotheses"]]
    lines += ["", "**仍未判定**：", ""]
    lines += [f"- {item}" for item in suggestion["undecided_hypotheses"]]
    lines += ["", f"**唯一自变量**：{experiment['single_independent_variable']['name']}"
                  f"（{experiment['single_independent_variable']['from']} → "
                  f"{experiment['single_independent_variable']['to']}）",
              f"- 为什么只改它：{experiment['single_independent_variable']['why_this_one']}",
              "", "**固定条件**：", ""]
    lines += [f"- {item}" for item in experiment["fixed_conditions"]]
    lines += ["", "**最大采样量**：",
              f"- {experiment['max_sampling']['train_rounds']} 轮 × "
              f"{experiment['max_sampling']['episodes_per_round']} 局，最多 "
              f"{experiment['max_sampling']['max_train_actions']} 个训练动作、"
              f"{experiment['max_sampling']['command_time_limit_s']}s"
              f"（{experiment['max_sampling']['note']}）",
              "", "**评价**："]
    lines += [f"- {item}" for item in experiment["evaluation"]]
    lines += ["", "**停止条件**："]
    lines += [f"- {item}" for item in experiment["stop_conditions"]]
    lines += ["", "**两种可能结果分别意味着什么**：",
              f"- 若折扣是主因：{experiment['what_it_would_show']['if_discount_is_the_cause']}",
              f"- 若折扣不是主因：{experiment['what_it_would_show']['if_discount_is_not_the_cause']}",
              "", f"- 决定权：{experiment['decided_by']}"]
    lines += ["", "## 边界与未验证项", "",
              "- 诊断只用 40000～40007（负责人已用于探查）：**不称最终保留集**；"
              "final 30000～30007 与未来保留 50000～50007 未被访问。",
              "- 本命令**不调用 Trainer**、不更新模型、不写训练集；原 run 只读。",
              "- 规则基线的优先顺序是**版本化的朴素规则**（不评估效果、不做跨回合规划）；"
              "它在这些种子上获胜只说明'受控场景存在可获胜路径'。",
              "- 概率是 softmax 输出，不是 Q 值/通关概率；TV 只描述分布变化，不描述好坏。",
              "- 表示遗漏类假设本轮**未测**（没有做成对可区分局面的编码对照）。"]
    if diagnosis.get("failures"):
        lines += ["", "## 失败行（未混入统计）", ""]
        lines += [f"- {item}" for item in diagnosis["failures"]]
    return lines


# --------------------------------------------------------------------------- #
# 助手
# --------------------------------------------------------------------------- #
def _weights_of(model) -> dict:
    return {name: np.asarray(array, dtype=np.float64)
            for name, array in model.scorer.params.items()}


def _action_type_of(semantic_key: str) -> str:
    """动作语义键是 canonical JSON：取出 `action_type`（**不猜**，解析失败就是空串）。"""
    try:
        document = json.loads(semantic_key)
    except (json.JSONDecodeError, TypeError):
        return ""
    return str(document.get("action_type") or "") if isinstance(document, Mapping) else ""


def _player_field(snapshot: Mapping[str, Any], field_id: str) -> Optional[float]:
    for entity in snapshot.get("entities") or []:
        if entity.get("kind_id") != "net:kind:base-spire:Player":
            continue
        for field in entity.get("fields") or []:
            if field.get("field_id") == field_id and field.get("state") == "known":
                try:
                    return float(field.get("value"))
                except (TypeError, ValueError):
                    return None
    return None


def _monster_total_hp(snapshot: Mapping[str, Any]) -> Optional[float]:
    total, found = 0.0, False
    for entity in snapshot.get("entities") or []:
        if entity.get("kind_id") != "net:kind:base-spire:Monster":
            continue
        for field in entity.get("fields") or []:
            if field.get("field_id") == "net:field:base-spire:Monster.Hp" \
                    and field.get("state") == "known":
                try:
                    total += float(field.get("value"))
                    found = True
                except (TypeError, ValueError):
                    pass
    return total if found else None


def _monster_min_hp(snapshot: Mapping[str, Any]) -> Optional[float]:
    values = []
    for entity in snapshot.get("entities") or []:
        if entity.get("kind_id") != "net:kind:base-spire:Monster":
            continue
        for field in entity.get("fields") or []:
            if field.get("field_id") == "net:field:base-spire:Monster.Hp" \
                    and field.get("state") == "known":
                try:
                    values.append(float(field.get("value")))
                except (TypeError, ValueError):
                    pass
    return min(values) if values else None


def _resolve_run_dir(raw: str) -> pathlib.Path:
    target = pathlib.Path(raw)
    resolved = (pathlib.Path.cwd() / target).resolve() if not target.is_absolute() else target.resolve()
    if not resolved.is_dir():
        raise UsageError(f"{COMMAND}: run 目录不存在：{resolved}")
    missing = [name for name in REQUIRED_RUN_FILES if not (resolved / name).is_file()]
    missing += [name for name in REQUIRED_RUN_DIRS if not (resolved / name).is_dir()]
    if missing:
        raise UsageError(
            f"{COMMAND}: run 目录缺少诊断所需产物 {missing}：{resolved}；"
            "本命令不猜缺失内容，也不重新训练"
        )
    if not (resolved / "episodes" / "index.jsonl").is_file():
        raise UsageError(f"{COMMAND}: run 缺少公开轨迹索引 episodes/index.jsonl：{resolved}")
    return resolved


def _read_json(path: pathlib.Path) -> dict:
    if not path.is_file():
        raise UsageError(f"{COMMAND}: 缺少 {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as failure:
        raise UsageError(f"{COMMAND}: {path} 不是合法 JSON（{failure}）") from failure


__all__ = ["COMMAND", "DIAGNOSE_ENVIRONMENT_SEEDS", "run"]
