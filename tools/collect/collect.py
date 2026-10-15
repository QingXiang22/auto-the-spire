"""D-04 的长跑入口：一条命令完成**有界采样 → 审计 → 汇报**。

用法（仓库根目录）::

    python tools/collect/collect.py --config tools/collect/example-config.json
    python tools/collect/collect.py --root <dir> --episodes 5 --resume

写入 `<root>/`：

* `progress.json`  —— 进度与 checkpoint 索引（D-02 产出；含 `resume_from_index`）；
* `records/`       —— 每个 episode 一条规范字节记录（D-01 格式）；
* `audit.json`     —— 数据审计（D-03：划分/奖励隔离/完整性/计数/偏移）；
* `report.json`    —— 本次运行的汇报（计数、失败分类、停止原因、身份、恢复语义）。

**恢复语义**（开工清单 §13 D-04）：恢复的是**采样进度**，不是游戏中途状态。被中断的那一局保持
`interrupted` 且**从头新开**；本入口不提供任何 rollback/load/fork，宿主也不支持 `attach`。

退出码：0 = 采集完成且审计无问题；1 = 审计有问题；2 = 配置/参数错误。
"""

from __future__ import annotations

import argparse
import json
import pathlib
import sys
import tempfile

REPO = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "ai-the-spire"))
sys.path.insert(0, str(REPO / "net-the-spire" / "client"))

from ai_the_spire.collector import Collector, CollectorLimits, read_progress  # noqa: E402
from ai_the_spire.data import EpisodeStore, SplitPlan, audit, canonical_bytes  # noqa: E402
from net_client import NetClient, NetClientError  # noqa: E402

COLLECT_ENTRY_VERSION = "tools/collect@1"
EXIT_OK, EXIT_AUDIT, EXIT_CONFIG = 0, 1, 2

DEFAULT_CONFIG = {
    "root": None,                     # 必填：产物目录（调用方给，不写进源码树）
    "base_seed": 20261001,
    "episodes": 4,
    "steps_per_episode": 20,
    "seconds": 300.0,
    "bytes": 16 * 1024 * 1024,
    "inflight_bytes": 4 * 1024 * 1024,
    "workers": 1,
    "scenario_variant": "baseline",
    "test_headbutt": False,
    "splits": {},                     # 场景簇 → train/validation/final_eval
    "fault_injection": None,          # {"at_step": N}：**仅检查用**
}


def load_config(path: str | None, overrides: dict) -> dict:
    config = dict(DEFAULT_CONFIG)
    if path:
        # `utf-8-sig`：Windows 上的编辑器/`Out-File` 常给配置加 BOM，不能因此判定配置非法。
        raw = json.loads(pathlib.Path(path).read_text(encoding="utf-8-sig"))
        unknown = set(raw) - set(DEFAULT_CONFIG)
        if unknown:
            raise ValueError(f"配置里有未知键: {sorted(unknown)}")
        config.update(raw)
    config.update({key: value for key, value in overrides.items() if value is not None})
    if not config.get("root"):
        raise ValueError("必须显式给出产物目录（root / --root），不写进源码树")
    return config


def build_collector(config: dict, *, start_index: int, fault_injection):
    def factory():
        args = ["--test-headbutt"] if config["test_headbutt"] else None
        return NetClient.host_from_solution(extra_args=args)

    return Collector(
        config["root"],
        base_seed=int(config["base_seed"]),
        limits=CollectorLimits(
            episodes=int(config["episodes"]),
            steps_per_episode=int(config["steps_per_episode"]),
            seconds=float(config["seconds"]),
            bytes=int(config["bytes"]),
            inflight_bytes=int(config["inflight_bytes"]),
        ),
        workers=int(config["workers"]),
        client_factory=factory,
        interruption_errors=(OSError, NetClientError),
        fault_injection=fault_injection,
        scenario_variant=str(config["scenario_variant"]),
    )


def make_fault_injection(spec):
    """故障注入（**仅检查用**）：在第 N 步之后杀掉宿主，制造一次真实的传输层丢失。"""
    if not spec:
        return None
    at_step = int(spec["at_step"])
    state = {"done": False}

    def inject(_episode_index, client):
        if state["done"]:
            return
        original = client.step

        def killing_step(*args, **kwargs):
            response = original(*args, **kwargs)
            index = kwargs.get("request_id", "")
            if not state["done"] and index.endswith(f"-{at_step}"):
                state["done"] = True
                client.kill()
            return response

        client.step = killing_step  # type: ignore[assignment]

    return inject


def classify_failures(result, audit_result) -> dict:
    """失败分类：中断/故障/实现错误/限额，各自独立计数，不与"普通终止"混在一起。"""
    return {
        "host_lost": {"count": result.counts.get("interrupted", 0),
                      "meaning": "传输层丢失或结果不确定；该局保持 interrupted，恢复时从头新开"},
        "backend_fault": {"count": result.counts.get("faulted", 0),
                          "meaning": "后端故障；不伪装成胜负"},
        "implementation_error": {"stop_reason": result.stop_reason == "error",
                                 "detail": result.stop_detail if result.stop_reason == "error" else ""},
        "limit_stops": {"stop_reason": result.stop_reason
                        if result.stop_reason.endswith("_limit") else None,
                        "detail": result.stop_detail
                        if result.stop_reason.endswith("_limit") else ""},
        "truncated_episodes": {"count": result.counts.get("truncated", 0),
                               "meaning": "达到每局步数上限的显式截断；不是失败"},
        "unknown_transitions": {"count": result.counts.get("unknown_transitions", 0)},
        "audit_problems": {"count": len(audit_result.problems), "items": audit_result.problems[:10]},
    }


def run(config: dict, *, resume: bool) -> int:
    root = pathlib.Path(config["root"])
    store = EpisodeStore(root)

    start_index = 0
    resumed_from = None
    if resume:
        try:
            progress = read_progress(root)
        except FileNotFoundError:
            print(f"没有可恢复的进度文件：{root / 'progress.json'}", file=sys.stderr)
            return EXIT_CONFIG
        start_index = int(progress.get("resume_from_index", progress.get("next_episode_index", 0)))
        resumed_from = {"previous_stop_reason": progress.get("stop_reason"),
                        "previous_stop_detail": progress.get("stop_detail"),
                        "start_index": start_index,
                        "game_state_resumed": False}

    collector = build_collector(config, start_index=start_index,
                               fault_injection=make_fault_injection(config.get("fault_injection")))
    result = collector.run(start_index=start_index)

    plan = SplitPlan(assignments=dict(config.get("splits") or {})) if config.get("splits") else None
    audit_result = audit(store, plan=plan,
                         attempted=read_progress(root).get("attempted"))

    report = {
        "entry": COLLECT_ENTRY_VERSION,
        "resume_semantics": "sampling_progress_only",
        "game_state_resumed": False,
        "no_public_rollback": True,
        "resumed_from": resumed_from,
        "collection": result.to_wire(),
        "failures": classify_failures(result, audit_result),
        "audit": audit_result.to_wire(),
        "identity": {
            "dictionary_version": None,
            "note": "身份逐条写在记录里；这里只汇总（见 records/ 与 audit.json）",
        },
    }
    (root / "audit.json").write_bytes(canonical_bytes(audit_result.to_wire()))
    (root / "report.json").write_bytes(canonical_bytes(report))

    print(f"collect: stop_reason={result.stop_reason} written={result.counts.get('written')} "
          f"usable={audit_result.counts.get('usable_episodes')} "
          f"interrupted={result.counts.get('interrupted')} problems={len(audit_result.problems)}")
    for note in audit_result.notes:
        print(f"  note: {note}")
    for problem in audit_result.problems[:5]:
        print(f"  problem: {problem}", file=sys.stderr)
    return EXIT_AUDIT if audit_result.problems else EXIT_OK


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="D-04 长跑入口：有界采样 + 审计 + 汇报")
    parser.add_argument("--config", help="JSON 配置路径")
    parser.add_argument("--root", help="产物目录（覆盖配置）")
    parser.add_argument("--episodes", type=int, help="局数上限")
    parser.add_argument("--steps-per-episode", type=int, help="每局步数上限")
    parser.add_argument("--workers", type=int, help="worker 数")
    parser.add_argument("--scenario-variant", help="场景簇标签")
    parser.add_argument("--test-headbutt", action="store_true", default=None,
                        help="使用切片自带的测试场景（**非原版起始卡组**）")
    parser.add_argument("--resume", action="store_true", help="按进度文件恢复采样（不是恢复游戏状态）")
    args = parser.parse_args(argv)

    try:
        config = load_config(args.config, {
            "root": args.root,
            "episodes": args.episodes,
            "steps_per_episode": args.steps_per_episode,
            "workers": args.workers,
            "scenario_variant": args.scenario_variant,
            "test_headbutt": args.test_headbutt,
        })
    except (ValueError, OSError, json.JSONDecodeError) as failure:
        print(f"配置错误: {failure}", file=sys.stderr)
        return EXIT_CONFIG
    return run(config, resume=args.resume)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
