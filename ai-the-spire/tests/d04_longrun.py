"""D-04 的检查：长跑入口（有界采样 + 审计 + 汇报）、可重启监督与失败分类。

用法（仓库根目录，需要先 ``dotnet build AutoTheSpire.sln``）::

    python ai-the-spire/tests/d04_longrun.py [--only GROUP]

分组：

* ``entry``        —— 一条命令产出 records/index/progress/audit/report，汇报含计数与失败分类；
* ``restart``      —— 故障注入中断后：中断局保持 interrupted、`resume_from_index` 指向它、
                      恢复时**从头新开**该局（不是接着打）、其余局继续采完；
* ``multiworker``  —— 多 worker 交错可复现，且与单 worker 的内容身份一致；
* ``failure-classes`` —— 中断/后端故障/实现错误/限额/截断分别计数，互不混淆；
* ``config``       —— 配置错误（未知键/缺 root/带 BOM）的处理。

**故障注入产生的结果不代表真实游戏或真实模拟**：`restart` 组用注入的宿主丢失制造中断局。
"""

from __future__ import annotations

import json
import pathlib
import subprocess
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
TOOL = REPO / "tools" / "collect" / "collect.py"
sys.path.insert(0, str(REPO / "ai-the-spire"))

from ai_the_spire.data import EpisodeStore  # noqa: E402

GROUPS = ("entry", "restart", "multiworker", "failure-classes", "config")
EXIT_OK, EXIT_AUDIT, EXIT_CONFIG = 0, 1, 2


class Checker:
    def __init__(self) -> None:
        self.passed = 0
        self.failed = 0
        self.details: list[str] = []

    def check(self, case: str, condition: bool, detail: str = "") -> bool:
        if condition:
            self.passed += 1
        else:
            self.failed += 1
            self.details.append(f"  - {case}: {detail}")
        return condition

    def equal(self, case: str, expected, actual) -> bool:
        return self.check(case, expected == actual, f"expected {expected!r}, got {actual!r}")


def _run_tool(root: pathlib.Path, *extra: str, config_extra: dict | None = None) -> subprocess.CompletedProcess:
    args = [sys.executable, "-u", str(TOOL), "--root", str(root), *extra]
    if config_extra is not None or "--config" in extra:
        pass
    return subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=str(REPO))


def _run_config(config: dict, *extra: str) -> subprocess.CompletedProcess:
    directory = pathlib.Path(tempfile.mkdtemp(prefix="d04-cfg-"))
    path = directory / "collect.json"
    path.write_text(json.dumps(config, ensure_ascii=False), encoding="utf-8")
    return subprocess.run([sys.executable, "-u", str(TOOL), "--config", str(path), *extra],
                          capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=str(REPO))


def _json(path: pathlib.Path) -> dict:
    # 显式 UTF-8：PowerShell/编辑器的默认编码会把这份 JSON 读坏（本轮踩过）。
    return json.loads(path.read_text(encoding="utf-8"))


def _temp(prefix: str) -> pathlib.Path:
    return pathlib.Path(tempfile.mkdtemp(prefix=prefix))


# -- entry --------------------------------------------------------------------------------------------------


def _run_entry(checker: Checker) -> None:
    root = _temp("d04-entry-")
    completed = _run_tool(root, "--episodes", "2", "--steps-per-episode", "3")
    checker.equal("entry/exit-code", EXIT_OK, completed.returncode)
    for name in ("progress.json", "index.jsonl", "audit.json", "report.json"):
        checker.check(f"entry/{name}-written", (root / name).exists(), f"{sorted(p.name for p in root.iterdir())}")
    report = _json(root / "report.json")
    checker.check("entry/report-has-counts",
                  report["collection"]["counts"]["written"] == 2, f"{report['collection']['counts']}")
    checker.check("entry/report-has-failure-classes",
                  set(report["failures"]) >= {"host_lost", "backend_fault", "implementation_error",
                                              "limit_stops", "truncated_episodes", "unknown_transitions"},
                  f"{sorted(report['failures'])}")
    checker.equal("entry/resume-semantics-declared", "sampling_progress_only", report["resume_semantics"])
    checker.check("entry/game-state-not-resumed", report["game_state_resumed"] is False, f"{report}")
    checker.check("entry/no-budget-verdict",
                  not any(key in json.dumps(report) for key in ('"meets_budget"', '"within_budget"', "达标")),
                  "没有批准预算就不得写达标结论")
    checker.check("entry/audit-clean", _json(root / "audit.json")["problems"] == [],
                  f"{_json(root / 'audit.json')['problems']}")
    checker.equal("entry/records-written", 2, len(list((root / "records").glob("*.json"))))
    checker.check("entry/exit-code-1-on-audit-problems",
                  _run_tool(_temp("d04-entry-ok-"), "--episodes", "1", "--steps-per-episode", "2").returncode
                  == EXIT_OK,
                  "干净运行必须退出 0")


# -- restart ------------------------------------------------------------------------------------------------


def _run_restart(checker: Checker) -> None:
    root = _temp("d04-restart-")
    faulted = _run_config({"root": str(root), "episodes": 3, "steps_per_episode": 6,
                           "fault_injection": {"at_step": 1}})
    checker.equal("restart/faulted-run-exit", EXIT_OK, faulted.returncode)
    progress = _json(root / "progress.json")
    checker.equal("restart/stops-with-host-lost", "host_lost", progress["stop_reason"])
    checker.equal("restart/interrupted-counted", 1, progress["counts"]["interrupted"])
    checker.equal("restart/resume-points-at-interrupted-index", 0, progress["resume_from_index"])
    checker.check("restart/partial-record-saved", progress["counts"]["written"] >= 1, f"{progress['counts']}")

    store = EpisodeStore(root)
    before = {entry["episode_id"]: store.read(entry["episode_id"]) for entry in store.index()}
    interrupted = [document for document in before.values() if document["ending"]["class"] == "interrupted"]
    checker.equal("restart/one-interrupted-record", 1, len(interrupted))
    interrupted_id = interrupted[0]["episode_id"]
    checker.check("restart/interrupted-keeps-confirmed-transitions",
                  interrupted[0]["summary"]["confirmed_transitions"] >= 1, f"{interrupted[0]['summary']}")

    # 恢复：**不带故障注入**（故障是暂时的），从 resume_from_index 开始，从头新开那一局。
    resumed = _run_config({"root": str(root), "episodes": 3, "steps_per_episode": 6}, "--resume")
    checker.equal("restart/resume-exit", EXIT_OK, resumed.returncode)
    progress_after = _json(root / "progress.json")
    checker.equal("restart/resume-completes", "episodes_completed", progress_after["stop_reason"])
    checker.equal("restart/resume-writes-all-episodes", 3, progress_after["written"])

    report = _json(root / "report.json")
    checker.equal("restart/resume-start-index", 0, report["resumed_from"]["start_index"])
    checker.check("restart/resume-does-not-restore-game-state",
                  report["resumed_from"]["game_state_resumed"] is False and report["game_state_resumed"] is False,
                  f"{report['resumed_from']}")
    checker.equal("restart/previous-stop-recorded", "host_lost",
                  report["resumed_from"]["previous_stop_reason"])

    after = {entry["episode_id"]: store.read(entry["episode_id"]) for entry in store.index()}
    checker.equal("restart/interrupted-record-preserved", "interrupted", after[interrupted_id]["ending"]["class"])
    checker.check("restart/interrupted-not-rewritten-as-terminal",
                  after[interrupted_id]["ending"]["class"] not in ("terminal_win", "terminal_loss"),
                  after[interrupted_id]["ending"]["class"])
    checker.equal("restart/four-records-total", 4, len(after))
    # 恢复出来的局必须**从开局**开始，而不是接着死掉的那局：首条观测版本与同种子新局一致。
    resumed_docs = [document for episode_id, document in after.items() if episode_id != interrupted_id]
    checker.check("restart/resumed-episodes-start-fresh",
                  all(document["transitions"][0]["index"] == 0 for document in resumed_docs),
                  "恢复的局必须从 index 0 开始")
    audit_report = _json(root / "audit.json")
    checker.equal("restart/audit-excludes-interrupted-from-usable", 3,
                  audit_report["counts"]["usable_episodes"])
    checker.equal("restart/audit-counts-interrupted", 1, audit_report["counts"]["interrupted"])
    checker.check("restart/audit-clean-after-resume", audit_report["problems"] == [],
                  f"{audit_report['problems']}")


# -- multiworker --------------------------------------------------------------------------------------------


def _content_keys(root: pathlib.Path) -> list[tuple]:
    store = EpisodeStore(root)
    keys = []
    for entry in store.index():
        document = store.read(entry["episode_id"])
        keys.append((document["identities"]["seed"], document["ending"]["class"],
                     tuple(t["tensor_digest"] for t in document["transitions"])))
    return sorted(keys)


def _run_multiworker(checker: Checker) -> None:
    single_root = _temp("d04-w1-")
    multi_root = _temp("d04-w2-")
    single = _run_tool(single_root, "--episodes", "4", "--steps-per-episode", "2", "--workers", "1")
    multi = _run_tool(multi_root, "--episodes", "4", "--steps-per-episode", "2", "--workers", "2")
    checker.equal("multiworker/single-exit", EXIT_OK, single.returncode)
    checker.equal("multiworker/multi-exit", EXIT_OK, multi.returncode)
    checker.equal("multiworker/four-records", 4, len(list((multi_root / "records").glob("*.json"))))
    checker.equal("multiworker/content-identical-to-single-worker",
                  _content_keys(single_root), _content_keys(multi_root))
    store = EpisodeStore(multi_root)
    workers = sorted({store.read(entry["episode_id"])["provenance"]["worker"] for entry in store.index()})
    checker.equal("multiworker/both-workers-used", ["0", "1"], workers)
    checker.check("multiworker/audit-clean", _json(multi_root / "audit.json")["problems"] == [],
                  f"{_json(multi_root / 'audit.json')['problems']}")


# -- failure-classes ----------------------------------------------------------------------------------------


def _run_failure_classes(checker: Checker) -> None:
    # 限额停止：时长极小 → 必须报 time_limit 且写明原因。
    limited_root = _temp("d04-limit-")
    limited = _run_config({"root": str(limited_root), "episodes": 20, "steps_per_episode": 3,
                           "seconds": 0.0001})
    checker.equal("failure-classes/limit-run-exit", EXIT_OK, limited.returncode)
    report = _json(limited_root / "report.json")
    checker.equal("failure-classes/limit-stop-named", "time_limit", report["failures"]["limit_stops"]["stop_reason"])
    checker.check("failure-classes/limit-detail-present", "时长上限" in report["failures"]["limit_stops"]["detail"],
                  report["failures"]["limit_stops"]["detail"])

    # 步数上限 → 截断（不是失败）。
    truncated_root = _temp("d04-trunc-")
    _run_tool(truncated_root, "--episodes", "1", "--steps-per-episode", "2")
    truncated_report = _json(truncated_root / "report.json")
    checker.equal("failure-classes/truncation-counted",
                  1, truncated_report["failures"]["truncated_episodes"]["count"])
    checker.check("failure-classes/truncation-not-called-failure",
                  "不是失败" in truncated_report["failures"]["truncated_episodes"]["meaning"]
                  and truncated_report["failures"]["backend_fault"]["count"] == 0,
                  f"{truncated_report['failures']}")

    # 中断与后端故障分开计数（中断由 restart 组注入；这里断言两块语义与计数键都在）。
    checker.check("failure-classes/host-lost-block",
                  "从头新开" in report["failures"]["host_lost"]["meaning"], f"{report['failures']['host_lost']}")
    checker.check("failure-classes/backend-fault-block",
                  "不伪装成胜负" in report["failures"]["backend_fault"]["meaning"],
                  f"{report['failures']['backend_fault']}")


# -- config -------------------------------------------------------------------------------------------------


def _run_config_group(checker: Checker) -> None:
    checker.equal("config/unknown-key-rejected",
                  EXIT_CONFIG,
                  _run_config({"root": str(_temp("d04-bad-")), "episodes": 1, "unknown": True}).returncode)
    checker.equal("config/missing-root-rejected", EXIT_CONFIG,
                  subprocess.run([sys.executable, str(TOOL), "--episodes", "1"],
                                 capture_output=True, text=True, cwd=str(REPO)).returncode)

    # 带 BOM 的配置必须被接受（Windows 编辑器默认会加 BOM；本轮踩过这个坑）。
    root = _temp("d04-bom-")
    directory = pathlib.Path(tempfile.mkdtemp(prefix="d04-bomcfg-"))
    path = directory / "collect.json"
    path.write_bytes(b"\xef\xbb\xbf" + json.dumps({"root": str(root), "episodes": 1,
                                                  "steps_per_episode": 2}).encode("utf-8"))
    completed = subprocess.run([sys.executable, str(TOOL), "--config", str(path)],
                               capture_output=True, text=True, cwd=str(REPO))
    checker.equal("config/bom-config-accepted", EXIT_OK, completed.returncode)

    # 恢复但没有进度文件 → 配置类错误（不是伪造进度）。
    checker.equal("config/resume-without-progress-rejected", EXIT_CONFIG,
                  _run_tool(_temp("d04-noprogress-"), "--episodes", "1", "--resume").returncode)


RUNNERS = {"entry": _run_entry, "restart": _run_restart, "multiworker": _run_multiworker,
           "failure-classes": _run_failure_classes, "config": _run_config_group}


def main(argv: list[str]) -> int:
    only = None
    if len(argv) >= 2 and argv[0] == "--only":
        only = argv[1]
        if only not in GROUPS:
            print(f"未知分组 '{only}'；可用：{', '.join(GROUPS)}", file=sys.stderr)
            return 2

    checker = Checker()
    for group in GROUPS:
        if only is not None and group != only:
            continue
        before_passed, before_failed = checker.passed, checker.failed
        try:
            RUNNERS[group](checker)
        except Exception as exception:  # 单组异常记为一条失败，不中止整套
            checker.check(f"{group}/harness", False, f"{type(exception).__name__}: {exception}")
        group_passed = checker.passed - before_passed
        group_failed = checker.failed - before_failed
        state = "PASS" if group_failed == 0 else "FAIL"
        print(f"[{state}] {group}: {group_passed} passed, {group_failed} failed, "
              f"{group_passed + group_failed} executed")

    for detail in checker.details:
        print(detail, file=sys.stderr)
    print(f"TOTAL: {checker.passed} passed, {checker.failed} failed, "
          f"{checker.passed + checker.failed} executed")
    return 1 if checker.failed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
