"""D1-A：从仓库根目录看一局真实对局，每个确认决策都显示局面 / 全部候选概率 / 实际选择 / 后状态。

产物（`--output` 目录）：

* ``episode.md`` —— 可读对局（人不用回查测试 PASS 列表就能判断发生了什么）；
* ``episodes/`` —— 复用正式 `EpisodeRecord` 的公开轨迹（`index.jsonl` + `records/<episode_id>.json`）；
* ``manifest.json`` / ``resolved-config.json`` —— 真实 Gym/scenario/字典/编码/奖励/执行器身份与全部参数；
* ``run-summary.json`` —— 结束分类、确认动作数、耗时、模型身份、资源与机器报告。

没有 ``--model`` 时用冻结种子的**随机初始化**小模型，并明确标注 untrained；
``--model`` 必须按 exact identity 载入，身份或权重损坏就在启动游戏之前拒绝。
"""

from __future__ import annotations

import pathlib
import time
from typing import Any, Mapping

from ..contracts import layers
from ..data.record import build_record
from ..data.store import EpisodeStore
from ..runner import EpisodeResult
from ..runner.baseline import EXECUTOR_PROFILE_ID
from .console import (
    catalog_labels,
    confirmed_action_count,
    render_episode_body,
)
from .config import load_profile
from .environment import build_run_manifest, make_runner, observe_environment
from .errors import DeliveryError
from .model_access import fresh_model, load_model_raw, model_summary
from .reward_v1 import d1_reward_profile
from .runtime import (
    HOST_DLL,
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

#: 命令名（用于退出摘要与准备提示）。
COMMAND = "demo"


def run(args) -> int:
    profile = load_profile(args.config)
    output = resolve_output_dir(args.output, command=COMMAND)
    ensure_numpy()
    command_budget = CommandBudget(limit_s=float(profile.budget["demo_command_time_limit_s"]),
                                  command=COMMAND)
    reward_profile = d1_reward_profile(
        hp_lost_weight=profile.reward["hp_lost_weight"],
        victory_weight=profile.reward["victory_weight"],
        defeat_weight=profile.reward["defeat_weight"],
        discount=profile.reward["discount"],
    )

    write_json(output / "resolved-config.json", profile.resolved_document())
    client = open_bounded_client(profile)
    summary: dict[str, Any] = {
        "command": COMMAND,
        "profile_id": profile.profile_id,
        "config_path": str(profile.config_path),
        "config_digest": profile.config_digest,
        "output_dir": str(output),
    }
    try:
        environment = observe_environment(profile, client)
        manifest_document = build_run_manifest(
            profile, environment, layers.catalog_digest(environment.catalog),
            reward_profile, host_dll=HOST_DLL,
        )
        write_json(output / "manifest.json", manifest_document)

        if args.model:
            model = load_model_raw(args.model, environment, where=COMMAND)
            model_state = "loaded-bundle"
            model_path = str(pathlib.Path(args.model).resolve())
        else:
            model = fresh_model(profile, environment)
            model_state = "untrained(random-init)"
            model_path = None

        seed = int(profile.seeds["demo_environment"])
        executor_seed = int(profile.seeds["demo_executor"])
        runner = make_runner(profile, client, environment,
                             environment_seed=seed, executor_seed=executor_seed,
                             policy=model.policy, model_id=model.model_id)
        started = time.perf_counter()
        result = runner.run()
        wall_s = time.perf_counter() - started
        command_budget.check(where="一局结束后")

        labels = catalog_labels(environment.catalog)
        markdown = _render_episode(result, environment.catalog, labels, profile, model, model_state,
                                   seed=seed, executor_seed=executor_seed, wall_s=wall_s)
        write_markdown(output / "episode.md", markdown)
        console_print("\n".join(markdown))

        record = build_record(result, provenance={
            "command": COMMAND,
            "profile_id": profile.profile_id,
            "config_digest": profile.config_digest,
            "model_state": model_state,
            "reward_profile_id": reward_profile.profile_id,
            "reward_profile_digest": reward_profile.digest(),
            "executor_profile_id": EXECUTOR_PROFILE_ID,
        })
        store = EpisodeStore(output / "episodes")
        store.write(record)
        problems = store.verify_all()

        summary.update({
            "environment_identity": manifest_document["environment_identity"],
            "gym_id": environment.identities.gym_id,
            "scenario_profile": environment.identities.scenario_profile,
            "model": model_summary(model, state=model_state),
            "model_path": model_path,
            "seed": seed,
            "executor_seed": executor_seed,
            "ending": result.ending,
            "ending_detail": result.ending_detail,
            "limit_reason": result.limit_reason,
            "terminal_result": (result.transitions[-1].terminal
                                if result.transitions and result.transitions[-1].status == "terminal"
                                else None),
            "confirmed_actions": _confirmed_actions(result),
            "wall_seconds": wall_s,
            "command_seconds": command_budget.elapsed_s,
            "timings": {"episode_s": wall_s, "command_s": command_budget.elapsed_s},
            "parameter_total": model.scorer.params.total_size(),
            "weight_bytes": weight_bytes(model),
            "machine": machine_report(),
            "episode_id": record.episode_id,
            "record_digest": record.digest(),
            "record_verification_problems": problems,
            "artifacts": {
                "episode_markdown": str(output / "episode.md"),
                "episode_records": str(output / "episodes" / "records"),
                "episode_index": str(output / "episodes" / "index.jsonl"),
                "manifest": str(output / "manifest.json"),
                "resolved_config": str(output / "resolved-config.json"),
                "summary": str(output / "run-summary.json"),
            },
            "exit_category": "ok",
            "exit_reason": "该命令完成其承诺（真实单局 + 可读对局 + 公开轨迹）",
        })
        write_json(output / "run-summary.json", summary)
        _print_footer(summary)
        return 0
    except DeliveryError as failure:
        summary.update({"exit_category": failure.category, "exit_reason": failure.reason})
        write_json(output / "run-summary.json", summary)
        raise
    finally:
        client.shutdown()


# --------------------------------------------------------------------------- #
# 渲染（逐决策块由 `console.render_episode_body` 提供，demo 只负责头尾）
# --------------------------------------------------------------------------- #
def _confirmed_actions(result: EpisodeResult) -> int:
    return confirmed_action_count(result)


def _render_episode(result: EpisodeResult, catalog: Mapping[str, Any], labels: Mapping[str, str],
                    profile, model, model_state: str, *, seed: int, executor_seed: int,
                    wall_s: float) -> list[str]:
    identities = result.identities
    lines: list[str] = [
        "# D1 demo 对局（可读）",
        "",
        f"- 配置 Profile：`{profile.profile_id}`（`{profile.config_path}`，config_digest `{profile.config_digest}`）",
        f"- 真实 Gym：`{identities.gym_id}`　场景 profile：`{identities.scenario_profile}`",
        f"- ruleset：`{identities.ruleset_id}`　catalog：`{identities.catalog_id}`",
        f"- 后端 profile：`{identities.backend_profile_id}`　协议：`{identities.protocol_version}`",
        f"- 字典：`{identities.dictionary_version}`　布局：`{identities.tensor_layout_version}`　"
        f"Manifest：`{identities.manifest_digest}`",
        f"- 环境种子：{seed}　执行器种子：{executor_seed}　执行器配置：`{identities.executor_profile_id}`",
        f"- 模型：`{model.model_id}`　权重摘要：`{model.weights_digest}`　状态：**{model_state}**",
        f"- 限额：每局最多 {profile.budget['max_decisions_per_episode']} 个确认动作、"
        f"{profile.budget['episode_time_limit_s']}s；每请求 {profile.budget['request_timeout_s']}s",
        "",
    ]
    if model_state.startswith("untrained"):
        lines += ["> 该模型是**随机初始化**、未受过训练：本文件只证明使用路径真实，不构成任何学习效果结论。", ""]

    lines.extend(render_episode_body(result, catalog, labels))

    lines.append("## 结束")
    terminal = (result.transitions[-1].terminal
                if result.transitions and result.transitions[-1].status == "terminal" else None)
    lines.append(f"- 结束分类：**{result.ending}**"
                 + (f"（限额：{result.limit_reason}）" if result.limit_reason else ""))
    lines.append(f"- 结束说明：{result.ending_detail or '（无）'}")
    if terminal:
        lines.append(f"- 终局：outcome={terminal.get('outcome')} reason={terminal.get('reason_id')}")
    lines.append(f"- 确认动作数：{_confirmed_actions(result)}（终止快照不计一个假动作）")
    lines.append(f"- 实际耗时：{wall_s:.3f}s")
    return lines


def _print_footer(summary: Mapping[str, Any]) -> None:
    terminal = summary.get("terminal_result") or {}
    outcome = {"win": "胜", "loss": "负"}.get(str(terminal.get("outcome")), None)
    ending = {"terminal": f"终局（{outcome}）" if outcome else "终局（结果未公开）",
              "truncated": "截断（未分胜负，不是失败）",
              "faulted": "故障（不得当作胜负）",
              "interrupted": "中断（结果不确定，不得当作胜负）"}.get(
        str(summary.get("ending")), str(summary.get("ending")))
    console_print("")
    console_print("== demo 结束 ==")
    console_print(f"结果：{ending}"
                  + (f"　限额：{summary['limit_reason']}" if summary.get("limit_reason") else ""))
    console_print(f"确认动作数：{summary['confirmed_actions']}　耗时：{summary['wall_seconds']:.3f}s")
    console_print(f"模型：{summary['model']['model_id']}　状态：{summary['model']['state']}")
    console_print(f"产物目录：{summary['output_dir']}")
    console_print(f"可读对局：{summary['artifacts']['episode_markdown']}")
    console_print(f"公开轨迹：{summary['artifacts']['episode_records']}"
                  f"（索引 {summary['artifacts']['episode_index']}）")
    console_print(f"Manifest：{summary['artifacts']['manifest']}　"
                  f"摘要：{summary['artifacts']['summary']}")
    console_print(f"退出类别：ok　原因：{summary['exit_reason']}")
