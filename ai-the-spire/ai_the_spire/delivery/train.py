"""D1-B：固定预算的**真实**采样 + 回报/优势 + PPO 更新 + 逐轮检查点（含续训）。

固定语义（任务包 §4/§5）：

* 整轮冻结行为模型 μ=π；4 局采完后**更新一次**；最多 4096 个训练动作；
* 每个**已确认外部动作**一个时间步；`r = -0.01*hp_lost + victory - defeat`；
  `gamma=0.99` 折扣回报、`A = G - V_old`，无 GAE、无优势归一化；
* 正常终止 bootstrap 为 0；**决策上限**截断用本轮冻结模型 `V_old(s_T)` bootstrap 并标记 truncated；
  墙钟/存储限额、协议错误、未知执行：保存已确认事实，**不提交该轮更新**，非零退出；
* 更新成功后按实际权重内容重算候选身份；`initial-model/` 与 `candidate-model/` 独立保留。

训练 run 产物：`resolved-config.json`、`manifest.json`、`initial-model/`、`candidate-model/`、
`checkpoints/round-XXXX/`、`episodes/`（公开 `EpisodeRecord`）、`metrics.jsonl`、`summary.json`、
`summary.md`、`run-state.json`。
"""

from __future__ import annotations

import json
import math
import pathlib
import shutil
import time
from dataclasses import dataclass
from typing import Any, Mapping, Optional, Sequence

from ..data.record import build_record
from ..data.store import EpisodeStore
from ..encoding.tensor import tensorize
from ..models import PolicySample, Trainer, TrainingConfig, build_model_input
from ..models.policy import semantic_keys_of
from ..runner.baseline import BASELINE_MODEL_ID, EXECUTOR_PROFILE_ID
from .config import Profile, load_profile
from .budget import (
    LEDGER_FILE,
    BudgetLedger,
    ledger_report,
    load_ledger,
    new_ledger,
)
from .environment import Environment, build_run_manifest, observe_environment, verify_environment
from .episodes import EpisodeFault, confirmed_action_indices, run_episode, bootstrap_value
from .errors import FaultError, UsageError
from .model_access import fresh_model, load_model_for_resume, model_summary
from .reward_v1 import EpisodeReward, d1_reward_profile, discounted_returns
from .runtime import (
    HOST_DLL,
    CommandBudget,
    check_storage,
    console_print,
    ensure_numpy,
    machine_report,
    open_bounded_client,
    resolve_output_dir,
    weight_bytes,
    write_json,
    write_markdown,
)

COMMAND = "train"
RESUME_COMMAND = "resume"

#: 行为模型与候选模型必须能在轨迹里核对；概率一致性容差（浮点回放的数值级别）。
_PROBABILITY_TOLERANCE = 1e-9


class MetricsWriter:
    """`metrics.jsonl`：一行一条机器可读记录（逐动作细节另进 `action-samples.jsonl`）。"""

    def __init__(self, path: pathlib.Path) -> None:
        self.path = path
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.touch(exist_ok=True)

    def write(self, entry: Mapping[str, Any]) -> None:
        with self.path.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(entry, ensure_ascii=False, sort_keys=True, separators=(",", ":")))
            handle.write("\n")


def training_config(profile: Profile) -> TrainingConfig:
    return TrainingConfig(
        learning_rate=float(profile.ppo["learning_rate"]),
        clip_ratio=float(profile.ppo["clip_ratio"]),
        epochs=int(profile.ppo["epochs"]),
        value_coef=float(profile.ppo["value_coef"]),
        entropy_coef=float(profile.ppo["entropy_coef"]),
        max_grad_norm=float(profile.ppo["max_grad_norm"]),
        adam_beta1=float(profile.ppo["adam_beta1"]),
        adam_beta2=float(profile.ppo["adam_beta2"]),
        adam_epsilon=float(profile.ppo["adam_epsilon"]),
        seed=int(profile.ppo["seed"]),
    )


def _save_bundle(model, directory: pathlib.Path, profile: Profile, reward_profile, *,
                 training_state: Optional[Mapping[str, Any]],
                 optimizer_state: Optional[Mapping[str, Any]],
                 sources: str) -> pathlib.Path:
    from ..models import save_bundle

    return save_bundle(
        model, directory,
        training_sources=sources,
        training_state=dict(training_state) if training_state is not None else None,
        optimizer_state=optimizer_state,
        reward_profile_id=reward_profile.profile_id,
        training_profile_id=profile.training_profile_id,
        executor_profile_id=EXECUTOR_PROFILE_ID,
    )


def _round_state(*, round_index: int, training_actions_used: int, optimizer_steps: int,
                 model, initial_model_id: str, profile: Profile, reward_profile,
                 environment: Environment, stop_after_round: Optional[int],
                 origin: Optional[Mapping[str, Any]]) -> dict:
    return {
        "run_state_format_version": "d1-run-state@1",
        "profile_id": profile.profile_id,
        "config_digest": profile.config_digest,
        "gym_id": environment.identities.gym_id,
        "manifest_digest": environment.manifest.manifest_digest,
        "reward_profile_digest": reward_profile.digest(),
        "rounds_completed": round_index,
        "train_rounds": profile.train_rounds,
        "training_actions_used": training_actions_used,
        "max_train_actions": int(profile.budget["max_train_actions"]),
        "optimizer_steps": optimizer_steps,
        "initial_model_id": initial_model_id,
        "candidate_model_id": model.model_id,
        "candidate_weights_digest": model.weights_digest,
        "last_checkpoint": (None if round_index == 0
                            else f"checkpoints/round-{round_index:04d}"),
        "stop_after_round": stop_after_round,
        "origin": dict(origin) if origin else None,
    }


# --------------------------------------------------------------------------- #
# 样本构造
# --------------------------------------------------------------------------- #
def samples_from_episode(model, environment: Environment, run, *, gamma: float) -> tuple[list, list]:
    """把一个真实 episode 变成 PPO 样本，并返回逐动作可核对记录。

    三件事必须同时成立，否则显式失败（不做 0 兜底）：
    1. 逐动作奖励与后快照一一对应；
    2. 用**本轮冻结模型**在同一公开快照上重算的概率与轨迹里记录的行为概率一致；
    3. 截断局用本轮冻结模型的 `V_old(s_T)` bootstrap（正常终止时为 0）。
    """
    result = run.result
    if run.reward is None:
        raise FaultError(f"episode 没有可用奖励：{run.reward_problem}")
    episodes_vectors = run.reward.per_action
    indices = confirmed_action_indices(result)
    if len(indices) != len(episodes_vectors):
        raise FaultError(
            f"已确认动作数 {len(indices)} 与逐动作奖励数 {len(episodes_vectors)} 不一致（拒绝猜样本）"
        )
    scalars = [float(vector.scalar) for vector in episodes_vectors]
    bootstrap: Optional[float] = None
    if run.truncated_by_decisions:
        if result.final_snapshot is None:
            raise FaultError("决策上限截断却没有最后公开快照：无法 bootstrap，拒绝伪造 G")
        bootstrap = bootstrap_value(model, environment, result.final_snapshot)
    returns = discounted_returns(scalars, gamma=gamma, bootstrap=bootstrap)

    samples: list[PolicySample] = []
    rows: list[dict] = []
    for position, index in enumerate(indices):
        transition = result.transitions[index]
        snapshot = transition.snapshot
        observation = tensorize(snapshot, environment.manifest, catalog=environment.catalog)
        evaluation = model.evaluate_tensorized(
            observation, semantic_keys=semantic_keys_of(snapshot), catalog=environment.catalog
        )
        recomputed = evaluation.by_semantic_key()
        recorded = dict(transition.probabilities)
        if set(recorded) != set(recomputed):
            raise FaultError(
                f"step {index}: 冻结模型重算的候选集与轨迹记录不一致；"
                "同一冻结模型 + 同一公开快照必须给出同一概率集合"
            )
        for semantic_key, value in recorded.items():
            if not math.isfinite(float(value)) or abs(float(value) - float(recomputed[semantic_key])) > _PROBABILITY_TOLERANCE:
                raise FaultError(
                    f"step {index}: 语义键 {semantic_key} 的行为概率 {value!r} 与冻结模型重算 "
                    f"{recomputed[semantic_key]!r} 不一致（行为模型身份过期或被改动）"
                )
        if transition.chosen_candidate_id not in evaluation.candidate_ids:
            raise FaultError(f"step {index}: 选中的候选不在本快照的模型候选行里（拒绝跨快照复用下标）")
        action_index = evaluation.candidate_ids.index(transition.chosen_candidate_id)
        probability = float(evaluation.probabilities[action_index])
        if probability <= 0.0:
            raise FaultError(f"step {index}: 行为策略下该动作概率为 0，不产生该样本")
        old_log_prob = math.log(probability)
        return_value = float(returns[position])
        advantage = return_value - float(evaluation.value)
        if not all(math.isfinite(item) for item in (old_log_prob, return_value, advantage)):
            raise FaultError(f"step {index}: old_log_prob/G/A 出现非有限值（数值失败）")
        samples.append(PolicySample(
            model_input=build_model_input(observation, environment.manifest, config=model.config),
            action_index=action_index,
            old_log_prob=old_log_prob,
            advantage=advantage,
            return_value=return_value,
            semantic_key=transition.chosen_semantic_key,
        ))
        rows.append({
            "step": index,
            "observation_id": transition.observation_id,
            "semantic_key": transition.chosen_semantic_key,
            "candidate_id": transition.chosen_candidate_id,
            "action_index": action_index,
            "candidates": len(transition.candidates),
            "pi_selected": probability,
            "mu_selected": float(recorded[transition.chosen_semantic_key]),
            "old_log_prob": old_log_prob,
            "value_old": float(evaluation.value),
            "return_G": return_value,
            "advantage": advantage,
            "reward_scalar": scalars[position],
            "reward_components": {key: float(value)
                                  for key, value in episodes_vectors[position].values.items()},
            "bootstrap_value": bootstrap,
        })
    return samples, rows


# --------------------------------------------------------------------------- #
# 训练主循环
# --------------------------------------------------------------------------- #
@dataclass
class TrainDeps:
    profile: Profile
    output: pathlib.Path
    client: Any
    environment: Environment
    reward_profile: Any
    budget: CommandBudget
    stop_after_round: Optional[int]
    command: str
    origin: Optional[dict]
    #: 冻结的初始模型（**不参与更新**）：validation 的 initial-model 策略与身份核对都用它。
    initial_model: Any = None
    #: run 级资源账目（实际消耗 vs 已提交状态）。
    ledger: Optional[BudgetLedger] = None


def _collect_episode(deps: "TrainDeps", *, round_index: int, episode_index: int, seed: int,
                     model, behavior_model_id: str, episodes_store: EpisodeStore,
                     metrics: MetricsWriter, committed: bool):
    """跑一局，并把这一局**已确认的公开事实**落盘 + 记账；故障局同样落盘，然后抛出。

    D1.1-F 的核心：故障/中断/墙钟停止不能只留一条错误摘要——这一局真实发生过的公开事实必须
    进 `episodes/`（复用既有 `EpisodeRecord` 格式）与 `metrics.jsonl`，账目也要记上**实际消耗**。
    没有完整样本（缺后快照/无奖励）不妨碍保留事实；事实照留、样本照缺，两者不互相冒充。
    """
    ledger = deps.ledger
    if ledger is None:  # pragma: no cover - 调用方保证
        raise FaultError("训练账目未初始化：拒绝在没有预算账目时采样")
    where = f"第 {round_index + 1} 轮第 {episode_index + 1} 局"
    allowed = ledger.guard_before_episode(active_seconds=deps.budget.elapsed_s, where=where)
    cap = int(deps.profile.budget["max_decisions_per_episode"])
    run = None
    failure = None
    try:
        run = run_episode(deps.profile, deps.client, deps.environment, deps.reward_profile,
                          environment_seed=seed,
                          executor_seed=deps.profile.executor_seed(seed),
                          policy=model.policy, model_id=model.model_id,
                          require_usable=True, max_steps=min(cap, allowed))
    except EpisodeFault as fault:
        failure = fault
        run = fault.run

    usable = failure is None and run.reward is not None and run.ending in ("terminal", "truncated")
    record = build_record(run.result, provenance=_provenance(
        deps, run, round_index, episode_index, seed, model, committed=committed and usable))
    episodes_store.write(record)
    ledger.note_episode(run, committed=committed and usable)
    ledger.bind_clock(deps.budget.elapsed_s)
    ledger.write()
    metrics.write({
        "kind": "episode",
        "round": round_index + 1,
        "episode_index": episode_index,
        "seed": seed,
        "executor_seed": deps.profile.executor_seed(seed),
        "behavior_model_id": behavior_model_id,
        "ending": run.ending,
        "limit_reason": run.result.limit_reason,
        "confirmed_actions": run.confirmed_actions,
        "unknown_actions": run.unknown_actions,
        "reward_scalar": None if run.reward is None else run.reward.scalar,
        "reward_values": None if run.reward is None else dict(run.reward.values),
        "discounted_return": None if run.reward is None else run.reward.discounted_return,
        "episode_id": record.episode_id,
        "record_digest": record.digest(),
        "ending_class": record.ending_class,
        "confirmed_transitions": record.confirmed_transitions,
        "wall_s": run.wall_s,
        # 故障局：公开事实已落盘，但**不是**可用样本，这一轮也不提交更新。
        "usable_for_training": usable,
        "reward_problem": run.reward_problem,
        "failure_reason": None if failure is None else failure.reason,
        "budget": ledger_report(ledger, active_seconds=deps.budget.elapsed_s),
    })
    if failure is not None:
        raise failure
    return run, record


def _train(deps: TrainDeps, *, start_round: int, model, trainer: Optional[Trainer],
           training_actions_used: int, initial_model_id: str, episodes_store: EpisodeStore,
           metrics: MetricsWriter, carried_records: int) -> dict:
    profile = deps.profile
    output = deps.output
    gamma = float(profile.reward["discount"])
    max_actions = int(profile.budget["max_train_actions"])
    round_lines: list[str] = []
    timings = {"episodes_s": 0.0, "update_s": 0.0, "validation_s": 0.0}
    summary: dict[str, Any] = {
        "command": deps.command,
        "profile_id": profile.profile_id,
        "config_path": str(profile.config_path),
        "config_digest": profile.config_digest,
        "output_dir": str(output),
        "gym_id": deps.environment.identities.gym_id,
        "scenario_profile": deps.environment.identities.scenario_profile,
        "initial_model_id": initial_model_id,
        "train_rounds": profile.train_rounds,
        "rounds": [],
    }
    if trainer is None:
        trainer = Trainer(model, training_config(profile))

    try:
        if start_round == 0:
            # "训练前"的一次 validation：曲线必须有一个未训练的起点，否则"改善"无从比较。
            pre = _validate(deps, stage="before-training", initial_model_id=initial_model_id,
                            current_model=model)
            metrics.write({"kind": "validation", **pre})
            summary["validation_before_training"] = pre
            timings["validation_s"] += float(pre.get("wall_s", 0.0))

        for round_index in range(start_round, profile.train_rounds):
            deps.budget.check(where=f"第 {round_index + 1} 轮开始前")
            behavior_model_id = model.model_id
            round_started = time.perf_counter()
            episodes = []
            endings: dict[str, int] = {}
            for episode_index in range(profile.episodes_per_round):
                seed = profile.train_environment_seed(round_index, episode_index)
                run, record = _collect_episode(
                    deps, round_index=round_index, episode_index=episode_index, seed=seed,
                    model=model, behavior_model_id=behavior_model_id,
                    episodes_store=episodes_store, metrics=metrics, committed=False)
                endings[run.ending] = endings.get(run.ending, 0) + 1
                episodes.append((episode_index, seed, run, record))
                deps.budget.check(where=f"第 {round_index + 1} 轮第 {episode_index + 1} 局后")

            round_actions = sum(run.confirmed_actions for _, _, run, _ in episodes)

            samples: list[PolicySample] = []
            action_rows: list[dict] = []
            for episode_index, seed, run, _ in episodes:
                episode_samples, rows = samples_from_episode(model, deps.environment, run, gamma=gamma)
                for row in rows:
                    row["round"] = round_index + 1
                    row["episode_index"] = episode_index
                    row["seed"] = seed
                samples.extend(episode_samples)
                action_rows.extend(rows)
            if not samples:
                raise FaultError(f"第 {round_index + 1} 轮没有任何可用样本：拒绝空更新")

            update_started = time.perf_counter()
            stats = trainer.update(samples)
            update_s = time.perf_counter() - update_started
            model.refresh_identity()
            round_s = time.perf_counter() - round_started

            round_number = round_index + 1
            round_dir = output / "checkpoints" / f"round-{round_number:04d}"
            # 提交点：更新成功之后，**已提交状态**前移到本轮结束位置；检查点、run-state
            # 与账目一起落地。实际消耗（confirmed_actions_used）早在每局结束时就已累计，与提交无关。
            training_actions_used += round_actions
            training_state = {
                "steps": trainer.optimizer.steps,
                "rounds_completed": round_number,
                "training_actions_used": training_actions_used,
                "profile_id": profile.profile_id,
                "config_digest": profile.config_digest,
                "reward_profile_digest": deps.reward_profile.digest(),
                "manifest_digest": deps.environment.manifest.manifest_digest,
                "model_init_seed": int(profile.seeds["model_init"]),
                "initial_model_id": initial_model_id,
                "candidate_model_id": model.model_id,
                "training_config": trainer.config.to_wire(),
            }
            _save_bundle(model, round_dir, profile, deps.reward_profile,
                         training_state=training_state,
                         optimizer_state=trainer.optimizer_state(),
                         sources=f"d1:{profile.profile_id}:round-{round_number:04d}")
            _write_action_samples(round_dir / "action-samples.jsonl", action_rows)
            _save_bundle(model, output / "candidate-model", profile, deps.reward_profile,
                         training_state=training_state,
                         optimizer_state=trainer.optimizer_state(),
                         sources=f"d1:{profile.profile_id}:candidate-after-round-{round_number:04d}")
            write_json(output / "run-state.json", _round_state(
                round_index=round_number, training_actions_used=training_actions_used,
                optimizer_steps=trainer.optimizer.steps, model=model,
                initial_model_id=initial_model_id, profile=profile,
                reward_profile=deps.reward_profile, environment=deps.environment,
                stop_after_round=deps.stop_after_round, origin=deps.origin))
            deps.ledger.note_round_committed(
                actions_in_round=round_actions,
                rounds_completed=round_number,
                episodes_usable_in_round=sum(
                    1 for _, _, run, _ in episodes if run.ending in ("terminal", "truncated")),
            )
            deps.ledger.bind_clock(deps.budget.elapsed_s)
            deps.ledger.write()

            validation = _validate(deps, stage=f"after-round-{round_number:04d}",
                                   initial_model_id=initial_model_id, current_model=model)
            timings["validation_s"] += float(validation.get("wall_s", 0.0))
            timings["episodes_s"] += sum(run.wall_s for _, _, run, _ in episodes)
            timings["update_s"] += update_s
            metrics.write({"kind": "validation", **validation})
            metrics.write({
                "kind": "update",
                "round": round_number,
                "behavior_model_id": behavior_model_id,
                "candidate_model_id": model.model_id,
                "candidate_weights_digest": model.weights_digest,
                "optimizer_steps": trainer.optimizer.steps,
                "samples": len(samples),
                "confirmed_actions": round_actions,
                "training_actions_used": training_actions_used,
                "loss": stats.loss,
                "policy_loss": stats.policy_loss,
                "value_loss": stats.value_loss,
                "entropy": stats.entropy,
                "grad_norm": stats.grad_norm,
                "grad_scale": stats.grad_scale,
                "clip_fraction": stats.clip_fraction,
                "episode_rewards": [None if run.reward is None else run.reward.scalar
                                    for _, _, run, _ in episodes],
                "round_s": round_s,
                "update_s": update_s,
                "checkpoint": str(round_dir),
            })

            line = _round_line(round_number, behavior_model_id, endings, round_actions, episodes,
                               stats, model.model_id, round_s, round_dir)
            console_print(line)
            round_lines.append(line)
            summary["rounds"].append({
                "round": round_number,
                "behavior_model_id": behavior_model_id,
                "candidate_model_id": model.model_id,
                "candidate_weights_digest": model.weights_digest,
                "endings": endings,
                "confirmed_actions": round_actions,
                "training_actions_used": training_actions_used,
                "loss": stats.loss,
                "policy_loss": stats.policy_loss,
                "value_loss": stats.value_loss,
                "entropy": stats.entropy,
                "grad_norm": stats.grad_norm,
                "grad_scale": stats.grad_scale,
                "clip_fraction": stats.clip_fraction,
                "optimizer_steps": trainer.optimizer.steps,
                "rewards": [None if run.reward is None else run.reward.scalar
                            for _, _, run, _ in episodes],
                "round_s": round_s,
                "update_s": update_s,
                "checkpoint": str(round_dir),
                "validation": validation,
            })

            storage = check_storage(output, int(profile.budget["storage_limit_bytes"]),
                                    where=f"第 {round_number} 轮后")
            metrics.write({
                "kind": "budget",
                "round": round_number,
                "training_actions_used": training_actions_used,
                "max_train_actions": max_actions,
                "command_seconds": deps.budget.elapsed_s,
                "storage_bytes": storage,
                "rounds_completed": round_number,
            })
            if deps.stop_after_round is not None and round_number >= deps.stop_after_round:
                console_print(f"（--stop-after-round {deps.stop_after_round}：已正常跑完第 "
                              f"{round_number} 轮并保存检查点，不属于故障）")
                break

        # 收尾把账目时钟结清（含最后的 validation 时间）再写一次：暂停期间不计，但本命令
        # 真实用掉的墙钟必须留在来源链上，供后续 resume 继承。
        if deps.ledger is not None:
            deps.ledger.bind_clock(deps.budget.elapsed_s)
            deps.ledger.write()

        summary.update({
            "rounds_completed": _rounds_completed(output),
            "training_actions_used": training_actions_used,
            "max_train_actions": max_actions,
            "candidate": model_summary(model, state="trained(ppo)"),
            "candidate_weight_bytes": weight_bytes(model),
            "initial_weight_bytes": weight_bytes(deps.initial_model) if deps.initial_model else None,
            "episodes_total": len(_index_entries(episodes_store)),
            "command_seconds": deps.budget.elapsed_s,
            "budget_ledger": (None if deps.ledger is None
                              else ledger_report(deps.ledger, active_seconds=deps.budget.elapsed_s)),
            "timings": {**timings, "command_s": deps.budget.elapsed_s,
                        "sample_build_s": max(
                            deps.budget.elapsed_s - timings["episodes_s"] - timings["update_s"]
                            - timings["validation_s"], 0.0)},
            "machine": machine_report(),
            "stopped_after_round": deps.stop_after_round,
            "artifacts": _artifacts(output),
            "exit_category": "ok",
            "exit_reason": ("该命令完成其承诺：真实采样 + 有效更新 + 逐轮检查点"
                            + ("（按 --stop-after-round 正常停止）" if deps.stop_after_round else "")),
        })
        _write_summary(output, summary, round_lines)
        _print_footer(summary, output)
        return summary
    except BaseException as failure:  # noqa: BLE001 - 失败也要落盘"已完成到哪一轮"
        summary.update({
            "rounds_completed": _rounds_completed(output),
            "training_actions_used": training_actions_used,
            "max_train_actions": max_actions,
            "candidate": model_summary(model, state="in-progress"),
            "candidate_weight_bytes": weight_bytes(model),
            "initial_weight_bytes": weight_bytes(deps.initial_model) if deps.initial_model else None,
            "command_seconds": deps.budget.elapsed_s,
            "budget_ledger": (None if deps.ledger is None
                              else ledger_report(deps.ledger, active_seconds=deps.budget.elapsed_s)),
            "timings": {**timings, "command_s": deps.budget.elapsed_s},
            "machine": machine_report(),
            "stopped_after_round": deps.stop_after_round,
            "artifacts": _artifacts(output),
            "exit_category": getattr(failure, "category", "fault"),
            "exit_reason": getattr(failure, "reason", f"{type(failure).__name__}: {failure}"),
        })
        _write_summary(output, summary, round_lines)
        raise


def _artifacts(output: pathlib.Path) -> dict:
    return {
        "resolved_config": str(output / "resolved-config.json"),
        "manifest": str(output / "manifest.json"),
        "run_state": str(output / "run-state.json"),
        "initial_model": str(output / "initial-model"),
        "candidate_model": str(output / "candidate-model"),
        "checkpoints": str(output / "checkpoints"),
        "episodes_index": str(output / "episodes" / "index.jsonl"),
        "episodes_records": str(output / "episodes" / "records"),
        "metrics": str(output / "metrics.jsonl"),
        "summary_json": str(output / "summary.json"),
        "summary_markdown": str(output / "summary.md"),
    }


def _rounds_completed(output: pathlib.Path) -> int:
    state_path = output / "run-state.json"
    if not state_path.is_file():
        return 0
    return int(json.loads(state_path.read_text(encoding="utf-8"))["rounds_completed"])


def _index_entries(store: EpisodeStore) -> list:
    return store.index()


def _provenance(deps: TrainDeps, run, round_index: int, episode_index: int, seed: int,
                model, *, committed: bool) -> dict:
    return {
        "command": deps.command,
        "profile_id": deps.profile.profile_id,
        "config_digest": deps.profile.config_digest,
        "model_state": "trained(ppo)" if round_index > 0 else "untrained(random-init)",
        "behavior_model_id": model.model_id,
        "reward_profile_id": deps.reward_profile.profile_id,
        "reward_profile_digest": deps.reward_profile.digest(),
        "executor_profile_id": EXECUTOR_PROFILE_ID,
        "round": round_index + 1,
        "episode_index": episode_index,
        "environment_seed": seed,
        "limit_reason": run.result.limit_reason,
        # 这一局是否作为**已提交训练状态**的一部分（故障/中断/无完整样本时为 false）。
        "committed_to_training": bool(committed),
    }


def _write_action_samples(path: pathlib.Path, rows: Sequence[Mapping[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as handle:
        for row in rows:
            handle.write(json.dumps(row, ensure_ascii=False, sort_keys=True, separators=(",", ":")))
            handle.write("\n")


def _round_line(round_number: int, behavior_model_id: str, endings: Mapping[str, int],
                actions: int, episodes, stats, candidate_model_id: str, round_s: float,
                round_dir: pathlib.Path) -> str:
    completed = endings.get("terminal", 0)
    truncated = endings.get("truncated", 0)
    faulted = endings.get("faulted", 0) + endings.get("interrupted", 0)
    rewards = [run.reward.scalar for _, _, run, _ in episodes if run.reward is not None]
    reward_text = "、".join(f"{value:.4f}" for value in rewards)
    return (f"[轮 {round_number}] 行为模型 {behavior_model_id}\n"
            f"  尝试 {len(episodes)} 局：完成 {completed} / 截断 {truncated} / 故障 {faulted}；"
            f"确认决策 {actions}\n"
            f"  回报（未折扣）{reward_text}；loss {stats.loss:.6f}"
            f"（policy {stats.policy_loss:.6f} / value {stats.value_loss:.6f}）\n"
            f"  候选模型 {candidate_model_id}；耗时 {round_s:.2f}s；检查点 {round_dir}")


def _validate(deps: TrainDeps, *, stage: str, initial_model_id: str, current_model) -> dict:
    """保留 validation 种子上跑均匀/初始/当前三种策略：**只报告曲线**，不调参、不选最优轮。"""
    profile = deps.profile
    from ..evaluation.stats import mean

    initial_model = deps.initial_model
    if initial_model is None:
        raise FaultError("validation 缺少冻结的初始模型：拒绝用当前模型冒充 initial-model")
    if initial_model.model_id != initial_model_id:
        raise FaultError(
            "冻结初始模型身份与训练开始时不一致："
            f"{initial_model.model_id!r} != {initial_model_id!r}（禁止混用行为版本）"
        )
    policies = (
        (BASELINE_MODEL_ID, None, BASELINE_MODEL_ID),
        ("initial-model", initial_model.policy, initial_model.model_id),
        ("current-candidate", current_model.policy, current_model.model_id),
    )
    started = time.perf_counter()
    report: dict[str, Any] = {"stage": stage, "policies": {}}
    for label, policy, model_id in policies:
        rewards: list[float] = []
        endings: dict[str, int] = {}
        decisions = 0
        wall = 0.0
        for seed in profile.validation_seeds():
            run = run_episode(profile, deps.client, deps.environment, deps.reward_profile,
                              environment_seed=seed, executor_seed=profile.executor_seed(seed),
                              policy=policy, model_id=model_id, require_usable=True)
            endings[run.ending] = endings.get(run.ending, 0) + 1
            decisions += run.confirmed_actions
            wall += run.wall_s
            if run.reward is not None:
                rewards.append(float(run.reward.scalar))
            deps.budget.check(where=f"validation {stage} / {label} seed={seed}")
        report["policies"][label] = {
            "model_id": model_id,
            "episodes": len(profile.validation_seeds()),
            "usable": len(rewards),
            "endings": endings,
            "mean_reward": mean(rewards) if rewards else None,
            "rewards": rewards,
            "decisions": decisions,
            "wall_s": wall,
        }
    report["wall_s"] = time.perf_counter() - started
    report["note"] = ("validation 只用于报告曲线；本命令不依据它调参、也不自动选取最优轮次")
    return report


def _write_summary(output: pathlib.Path, summary: Mapping[str, Any], round_lines: Sequence[str]) -> None:
    write_json(output / "summary.json", summary)
    lines = [
        f"# D1 {summary['command']} summary",
        "",
        f"- Profile：`{summary['profile_id']}`（`{summary['config_path']}`，config_digest `{summary['config_digest']}`）",
        f"- 真实 Gym：`{summary.get('gym_id')}`　场景：`{summary.get('scenario_profile')}`",
        f"- 初始模型：`{summary.get('initial_model_id')}`",
        f"- 完成轮数：{summary.get('rounds_completed')} / {summary.get('train_rounds')}",
        f"- 训练动作：{summary.get('training_actions_used')} / {summary.get('max_train_actions')}",
        f"- 命令耗时：{summary.get('command_seconds', 0.0):.3f}s",
        f"- 退出类别：{summary.get('exit_category')}　原因：{summary.get('exit_reason')}",
        "",
    ]
    if summary.get("candidate"):
        lines += [
            f"- 候选模型：`{summary['candidate']['model_id']}`"
            f"（权重摘要 `{summary['candidate']['weights_digest']}`）",
            "",
        ]
    machine = summary.get("machine") or {}
    timings = summary.get("timings") or {}
    if machine or timings:
        lines += ["## 机器 / 软件 / 成本（实际测量，口径写在字段里）", ""]
        if machine:
            lines += [
                f"- Python {machine.get('python_version')}（`{machine.get('python_executable')}`）"
                f"　NumPy {machine.get('numpy_version')}　dotnet SDK {machine.get('dotnet_sdk_version')}",
                f"- 平台：{machine.get('platform')}　CPU：{machine.get('processor_count')}",
                f"- 本 Python 进程工作集：{machine.get('python_process_working_set_bytes')} 字节"
                f"（口径：{machine.get('python_process_working_set_scope')}）",
                f"- Host/NumPy 进程组 RSS：{machine.get('host_process_group_rss_bytes')}"
                f"（{machine.get('host_process_group_rss_note')}）",
            ]
        if timings:
            lines.append(
                "- 耗时：环境交互 "
                f"{timings.get('episodes_s', 0.0):.3f}s + 更新 {timings.get('update_s', 0.0):.3f}s + "
                f"validation {timings.get('validation_s', 0.0):.3f}s + "
                f"样本构造等 {timings.get('sample_build_s', 0.0):.3f}s = "
                f"命令 {timings.get('command_s', 0.0):.3f}s")
        if summary.get("candidate_weight_bytes") is not None:
            lines.append(
                f"- 参数：{summary.get('candidate', {}).get('parameter_total')} 个；"
                f"初始包 {summary.get('initial_weight_bytes')} 字节 / "
                f"候选包 {summary.get('candidate_weight_bytes')} 字节（按 dtype 实际占用）")
        lines.append("")
    ledger = summary.get("budget_ledger") or {}
    if ledger:
        lines += [
            "## 训练资源账目（实际消耗 vs 已提交状态，`budget-ledger.json`）", "",
            f"- 实际消耗的确认动作：**{ledger.get('confirmed_actions_used')}** / "
            f"{ledger.get('max_train_actions')}（剩余 {ledger.get('remaining_actions')}）",
            f"- 已提交训练状态的动作：{ledger.get('committed_actions')}"
            f"（已提交轮数 {ledger.get('rounds_committed')}）",
            f"- 未知执行（结果不确定、不计确认动作）：{ledger.get('unknown_actions_used')}",
            f"- 累计训练墙钟：{ledger.get('training_seconds_used', 0.0):.3f}s / "
            f"{ledger.get('train_command_time_limit_s')}s"
            f"（其中来源链 {ledger.get('inherited_seconds', 0.0):.3f}s；暂停期间不计）",
            f"- 局计数：开始 {ledger.get('episodes_started')} / 完成 {ledger.get('episodes_completed')} / "
            f"故障或中断 {ledger.get('episodes_faulted')}",
            f"- 来源链：{ledger.get('inherited_from') or '（本次为新 run）'}",
            "",
            "> 实际消耗只增不减：故障局与未提交轮次的消耗留在账上，不以未提交为由返还额度。",
            "",
        ]
    if round_lines:
        lines += ["## 逐轮", "", "```text", *round_lines, "```", ""]
    rounds = summary.get("rounds") or []
    if rounds:
        lines += ["## 逐轮明细", "",
                  "| 轮 | 行为模型 | 候选模型 | 完成/截断/故障 | 确认决策 | 未折扣回报 | loss | optimizer steps | 耗时(s) | 检查点 |",
                  "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
        for item in rounds:
            endings = item["endings"]
            rewards = "、".join("n/a" if value is None else f"{value:.4f}" for value in item["rewards"])
            lines.append(
                f"| {item['round']} | `{item['behavior_model_id']}` | `{item['candidate_model_id']}` | "
                f"{endings.get('terminal', 0)}/{endings.get('truncated', 0)}/"
                f"{endings.get('faulted', 0) + endings.get('interrupted', 0)} | "
                f"{item['confirmed_actions']} | {rewards} | {item['loss']:.6f} | "
                f"{item['optimizer_steps']} | {item['round_s']:.2f} | `{item['checkpoint']}` |")
        lines.append("")
        lines += ["## validation 曲线（均匀 / 初始 / 当前，保留种子）", "",
                  "| 阶段 | 策略 | 平均未折扣回报 | 可用局 | 完成/截断/故障 | 确认决策 |",
                  "| --- | --- | --- | --- | --- | --- |"]
        stages = ([summary["validation_before_training"]]
                  if summary.get("validation_before_training") else [])
        stages += [item["validation"] for item in rounds if item.get("validation")]
        for validation in stages:
            for label, payload in (validation.get("policies") or {}).items():
                endings = payload["endings"]
                mean_reward = payload["mean_reward"]
                lines.append(
                    f"| {validation.get('stage')} | {label} | "
                    f"{'n/a' if mean_reward is None else f'{mean_reward:.4f}'} | {payload['usable']} | "
                    f"{endings.get('terminal', 0)}/{endings.get('truncated', 0)}/"
                    f"{endings.get('faulted', 0) + endings.get('interrupted', 0)} | "
                    f"{payload['decisions']} |")
        lines.append("")
    lines += [
        "> 本文件只报告实际发生的采样/更新与曲线；不声称已学到强策略，也不代表正式游戏收益偏好。",
        "> 未改善同样允许阶段完成；技术故障不允许（命令非零退出并保留已确认记录）。",
    ]
    write_markdown(output / "summary.md", lines)


def _print_footer(summary: Mapping[str, Any], output: pathlib.Path) -> None:
    console_print("")
    console_print(f"== {summary['command']} 结束 ==")
    console_print(f"完成轮数：{summary['rounds_completed']} / {summary['train_rounds']}　"
                  f"训练动作：{summary['training_actions_used']} / {summary['max_train_actions']}　"
                  f"耗时：{summary['command_seconds']:.3f}s")
    if summary.get("candidate"):
        console_print(f"候选模型：{summary['candidate']['model_id']}")
    console_print(f"产物目录：{output}")
    console_print(f"      摘要：{summary['artifacts']['summary_markdown']}　"
                  f"指标：{summary['artifacts']['metrics']}")
    console_print(f"退出类别：{summary['exit_category']}　原因：{summary['exit_reason']}")


# --------------------------------------------------------------------------- #
# 入口
# --------------------------------------------------------------------------- #
def _validate_stop_after(value: Optional[int], profile: Profile) -> Optional[int]:
    if value is None:
        return None
    if value < 1 or value > profile.train_rounds:
        raise UsageError(
            f"--stop-after-round 必须在 1..{profile.train_rounds} 内（总目标轮数不因停止而改变），得到 {value}"
        )
    return value


def run(args) -> int:
    profile = load_profile(args.config)
    output = resolve_output_dir(args.output, command=COMMAND)
    ensure_numpy()
    stop_after = _validate_stop_after(args.stop_after_round, profile)

    reward_profile = d1_reward_profile(
        hp_lost_weight=profile.reward["hp_lost_weight"],
        victory_weight=profile.reward["victory_weight"],
        defeat_weight=profile.reward["defeat_weight"],
        discount=profile.reward["discount"],
    )
    budget = CommandBudget(limit_s=float(profile.budget["train_command_time_limit_s"]), command=COMMAND)
    write_json(output / "resolved-config.json", profile.resolved_document())
    # 资源账目先落盘再起 Host：这样"已耗尽"的 run 连 Host 都不会启动（D1.1-B）。
    ledger = new_ledger(output / LEDGER_FILE, profile)
    ledger.guard_before_command()
    ledger.bind_clock(budget.elapsed_s)
    ledger.write()
    client = open_bounded_client(profile)
    try:
        environment = observe_environment(profile, client)
        manifest_document = build_run_manifest(
            profile, environment, _catalog_digest(environment), reward_profile, host_dll=HOST_DLL)
        write_json(output / "manifest.json", manifest_document)

        initial_model = fresh_model(profile, environment)
        _save_bundle(initial_model, output / "initial-model", profile, reward_profile,
                     training_state=None, optimizer_state=None,
                     sources=f"d1:{profile.profile_id}:structural-random-init")
        write_json(output / "initial-model.json",
                   model_summary(initial_model, state="untrained(random-init)"))

        model = fresh_model(profile, environment)
        if model.weights_digest != initial_model.weights_digest:
            raise FaultError("两次同种子初始化得到不同权重：随机初始化不可复现，停止训练")
        initial_model_id = model.model_id
        write_json(output / "run-state.json", _round_state(
            round_index=0, training_actions_used=0, optimizer_steps=0, model=model,
            initial_model_id=initial_model_id, profile=profile, reward_profile=reward_profile,
            environment=environment, stop_after_round=stop_after, origin=None))
        console_print(f"[train] Profile {profile.profile_id}　Gym {environment.identities.gym_id}"
                      f"　场景 {environment.identities.scenario_profile}")
        console_print(f"[train] 初始模型 {initial_model_id}"
                      f"（untrained，随机初始化 seed={profile.seeds['model_init']}）")
        console_print(f"[train] 计划：{profile.train_rounds} 轮 x {profile.episodes_per_round} 局，"
                      f"最多 {profile.budget['max_train_actions']} 个训练动作；"
                      f"validation 种子 {profile.validation_seeds()[0]}..{profile.validation_seeds()[-1]}")

        deps = TrainDeps(profile=profile, output=output, client=client, environment=environment,
                         reward_profile=reward_profile, budget=budget,
                         stop_after_round=stop_after, command=COMMAND, origin=None,
                         initial_model=initial_model, ledger=ledger)
        summary = _train(deps, start_round=0, model=model, trainer=None, training_actions_used=0,
                         initial_model_id=initial_model_id,
                         episodes_store=EpisodeStore(output / "episodes"),
                         metrics=MetricsWriter(output / "metrics.jsonl"), carried_records=0)
        _ = summary
        return 0
    except BaseException as failure:
        if not (output / "summary.json").exists():
            write_json(output / "summary.json", {
                "command": COMMAND,
                "profile_id": profile.profile_id,
                "output_dir": str(output),
                "exit_category": getattr(failure, "category", "fault"),
                "exit_reason": getattr(failure, "reason", f"{type(failure).__name__}: {failure}"),
            })
        raise
    finally:
        client.shutdown()


def run_resume(args) -> int:
    """从原 run 的**最后一个已提交轮次**继续到总目标轮数；原 run 只读、不被覆盖。"""
    ensure_numpy()
    run_dir = _resolve_run_dir(args.run, command=RESUME_COMMAND)
    output = resolve_output_dir(args.output, command=RESUME_COMMAND)
    recorded_manifest = _read_json(run_dir / "manifest.json", where=RESUME_COMMAND)
    recorded_state = _read_json(run_dir / "run-state.json", where=RESUME_COMMAND)

    profile = load_profile(recorded_manifest["config_path"])
    if profile.config_digest != recorded_manifest["config_digest"]:
        raise UsageError(
            f"{RESUME_COMMAND}: 原 run 的配置摘要与当前配置文件不一致（不静默迁移）："
            f"{recorded_manifest['config_digest']!r} != {profile.config_digest!r}"
        )
    rounds_completed = int(recorded_state["rounds_completed"])
    if rounds_completed >= profile.train_rounds:
        raise UsageError(
            f"{RESUME_COMMAND}: 原 run 已完成 {rounds_completed}/{profile.train_rounds} 轮，"
            "没有剩余轮次可恢复（不会额外增加目标轮数）"
        )
    checkpoint_relative = recorded_state.get("last_checkpoint")
    if not checkpoint_relative:
        raise UsageError(
            f"{RESUME_COMMAND}: 原 run 没有任何已提交轮次（rounds_completed={rounds_completed}），"
            "中途未提交的轮次不能伪装已完成"
        )
    checkpoint_dir = run_dir / checkpoint_relative
    if not checkpoint_dir.is_dir():
        raise UsageError(f"{RESUME_COMMAND}: 找不到已提交检查点 {checkpoint_dir}")

    reward_profile = d1_reward_profile(
        hp_lost_weight=profile.reward["hp_lost_weight"],
        victory_weight=profile.reward["victory_weight"],
        defeat_weight=profile.reward["defeat_weight"],
        discount=profile.reward["discount"],
    )
    recorded_reward = (recorded_manifest.get("reward_profile") or {}).get("digest")
    if recorded_reward != reward_profile.digest():
        raise UsageError(
            f"{RESUME_COMMAND}: 奖励 Profile 与原始 run 不一致（拒绝混用奖励来源）："
            f"{recorded_reward!r} != {reward_profile.digest()!r}"
        )

    # 来源链的资源账目：**必须存在**，否则拒绝恢复（不猜剩余预算）。
    source_ledger = load_ledger(run_dir / LEDGER_FILE, profile, where=RESUME_COMMAND)
    inherited_seconds = float(source_ledger.training_seconds_used)
    budget = CommandBudget(
        limit_s=float(profile.budget["train_command_time_limit_s"]) - inherited_seconds,
        command=RESUME_COMMAND,
    )
    # 来源索引 + 自足复制：新目录不需要用户再引用旧路径。
    origin = {
        "source_run": str(run_dir),
        "source_run_state": dict(recorded_state),
        "resumed_from_round": rounds_completed,
        "source_training_seconds_used": inherited_seconds,
        "source_confirmed_actions_used": int(source_ledger.confirmed_actions_used),
    }
    for name in ("resolved-config.json",):
        shutil.copy2(run_dir / name, output / name)
    manifest_copy = dict(recorded_manifest)
    manifest_copy["resumed_from"] = origin
    write_json(output / "manifest.json", manifest_copy)
    if (run_dir / "initial-model").is_dir():
        shutil.copytree(run_dir / "initial-model", output / "initial-model", dirs_exist_ok=True)
    if (run_dir / "initial-model.json").is_file():
        shutil.copy2(run_dir / "initial-model.json", output / "initial-model.json")
    checkpoints_source = run_dir / "checkpoints"
    if checkpoints_source.is_dir():
        shutil.copytree(checkpoints_source, output / "checkpoints", dirs_exist_ok=True)
    episodes_source = run_dir / "episodes"
    if episodes_source.is_dir():
        shutil.copytree(episodes_source, output / "episodes", dirs_exist_ok=True)
    if (run_dir / "metrics.jsonl").is_file():
        shutil.copy2(run_dir / "metrics.jsonl", output / "metrics.jsonl")
    if (run_dir / "candidate-model").is_dir():
        shutil.copytree(run_dir / "candidate-model", output / "candidate-model", dirs_exist_ok=True)
    write_json(output / "source-index.json", origin)

    # 继承来源链的整本账：实际消耗**不从零开始**，暂停期间不计墙钟。
    ledger = new_ledger(output / LEDGER_FILE, profile, inherited_from=str(run_dir))
    ledger.confirmed_actions_used = int(source_ledger.confirmed_actions_used)
    ledger.unknown_actions_used = int(source_ledger.unknown_actions_used)
    ledger.committed_actions = int(source_ledger.committed_actions)
    ledger.rounds_committed = int(source_ledger.rounds_committed)
    ledger.episodes_started = int(source_ledger.episodes_started)
    ledger.episodes_completed = int(source_ledger.episodes_completed)
    ledger.episodes_faulted = int(source_ledger.episodes_faulted)
    ledger.inherited_seconds = inherited_seconds
    if ledger.committed_actions != int(recorded_state.get("training_actions_used",
                                                          ledger.committed_actions)):
        raise UsageError(
            f"{RESUME_COMMAND}: 账目的已提交动作数 {ledger.committed_actions} 与 run-state 的 "
            f"{recorded_state.get('training_actions_used')} 不一致（来源链被改动过）"
        )
    ledger.guard_before_command()
    ledger.bind_clock(budget.elapsed_s)
    ledger.write()

    client = open_bounded_client(profile)
    try:
        environment = observe_environment(profile, client)
        verify_environment(recorded_manifest, environment, where=RESUME_COMMAND)
        model = load_model_for_resume(str(checkpoint_dir), environment, where=RESUME_COMMAND)
        trainer = Trainer(model, training_config(profile))
        if model.training_state is None:
            raise UsageError(f"{RESUME_COMMAND}: 检查点没有训练状态，无法恢复")
        trainer.load_optimizer_state(model.optimizer_state, steps=int(model.training_state["steps"]))
        training_actions_used = int(model.training_state.get("training_actions_used", 0))
        initial_model_id = str(model.training_state.get("initial_model_id", ""))
        if not initial_model_id:
            raise UsageError(f"{RESUME_COMMAND}: 检查点缺少 initial_model_id（拒绝猜身份）")
        console_print(f"[resume] 来源 {run_dir}")
        console_print(f"[resume] 已提交 {rounds_completed}/{profile.train_rounds} 轮；"
                      f"从检查点 {checkpoint_dir} 继续（不额外增加目标轮数）")
        console_print(f"[resume] 载入模型 {model.model_id}　optimizer steps {trainer.optimizer.steps}"
                      f"　累计训练动作 {training_actions_used}")
        console_print(f"[resume] 继承来源链账目：实际确认动作 {ledger.confirmed_actions_used}/"
                      f"{ledger.max_train_actions}、累计训练墙钟 {inherited_seconds:.3f}s/"
                      f"{ledger.train_command_time_limit_s:.1f}s（暂停期间不计）")

        write_json(output / "run-state.json", _round_state(
            round_index=rounds_completed, training_actions_used=training_actions_used,
            optimizer_steps=trainer.optimizer.steps, model=model,
            initial_model_id=initial_model_id, profile=profile, reward_profile=reward_profile,
            environment=environment, stop_after_round=recorded_state.get("stop_after_round"),
            origin=origin))
        deps = TrainDeps(profile=profile, output=output, client=client, environment=environment,
                         reward_profile=reward_profile, budget=budget,
                         stop_after_round=None, command=RESUME_COMMAND, origin=origin,
                         initial_model=_load_initial_for_validation(run_dir, environment, profile,
                                                                    initial_model_id),
                         ledger=ledger)
        _train(deps, start_round=rounds_completed, model=model, trainer=trainer,
               training_actions_used=training_actions_used, initial_model_id=initial_model_id,
               episodes_store=EpisodeStore(output / "episodes"),
               metrics=MetricsWriter(output / "metrics.jsonl"),
               carried_records=len(_read_index(run_dir / "episodes" / "index.jsonl")))
        return 0
    finally:
        client.shutdown()


def _catalog_digest(environment: Environment) -> str:
    from ..contracts import layers

    return layers.catalog_digest(environment.catalog)


def _load_initial_for_validation(run_dir: pathlib.Path, environment: Environment, profile: Profile,
                                 initial_model_id: str):
    """resume 时载入原 run 的 `initial-model/`：validation 的 initial-model 策略必须用**那一份**。"""
    from ..models import ModelBundleError, load_bundle

    directory = run_dir / "initial-model"
    if not directory.is_dir():
        raise UsageError(f"{RESUME_COMMAND}: 原 run 缺少 initial-model/：无法复核初始模型身份")
    try:
        model = load_bundle(str(directory), manifest=environment.manifest)
    except ModelBundleError as failure:
        raise UsageError(f"{RESUME_COMMAND}: 原 run 的初始模型包不可用：{failure}") from failure
    if model.model_id != initial_model_id:
        raise UsageError(
            f"{RESUME_COMMAND}: 原 run 的初始模型身份与检查点记录不一致："
            f"{model.model_id!r} != {initial_model_id!r}（拒绝混用行为版本）"
        )
    model.catalog = environment.catalog
    return model


def _resolve_run_dir(raw: str, *, command: str) -> pathlib.Path:
    target = pathlib.Path(raw)
    resolved = (pathlib.Path.cwd() / target).resolve() if not target.is_absolute() else target.resolve()
    if not resolved.is_dir():
        raise UsageError(f"{command}: run 目录不存在：{resolved}")
    for name in ("manifest.json", "run-state.json"):
        if not (resolved / name).is_file():
            raise UsageError(f"{command}: run 目录缺少 {name}：{resolved}")
    return resolved


def _read_json(path: pathlib.Path, *, where: str) -> dict:
    if not path.is_file():
        raise UsageError(f"{where}: 缺少 {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as failure:
        raise UsageError(f"{where}: {path} 不是合法 JSON（{failure}）") from failure


def _read_index(path: pathlib.Path) -> list:
    if not path.is_file():
        return []
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


__all__ = [
    "EpisodeReward",
    "run",
    "run_resume",
    "samples_from_episode",
    "training_config",
]
