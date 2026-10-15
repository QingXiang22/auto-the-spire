"""D-02 的检查：有界 Collector（限额、种子派生、worker 隔离、中断不混入终止、可重放）。

用法（仓库根目录，需要先 ``dotnet build AutoTheSpire.sln``）::

    python ai-the-spire/tests/d02_collector.py [--only GROUP]

分组：

* ``seeds``   —— 种子派生确定性、只依赖基种子与局序号；
* ``limits``  —— 局数/时长/磁盘/在飞/步数限额各自有**明确原因**，且不静默截断；
* ``isolation`` —— 多 worker 轮转 vs 单 worker 顺序：同一批 episode 结果一致（worker 不串状态）；
* ``interruption`` —— 故障注入（杀宿主）后中断局标为 `interrupted`、已确认转移不丢失、不补造下一状态；
* ``replay``  —— 同一配置重复采样得到同一批记录（重复 seed+动作的重放按声明能力成立）。

**故障注入产生的结果不代表真实游戏或真实模拟**。
"""

from __future__ import annotations

import pathlib
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "ai-the-spire"))
sys.path.insert(0, str(REPO / "net-the-spire" / "client"))

from ai_the_spire.collector import Collector, CollectorLimits, derive_seed, read_progress  # noqa: E402
from ai_the_spire.data import EpisodeStore  # noqa: E402
from net_client import NetClient, NetClientError  # noqa: E402

GROUPS = ("seeds", "limits", "isolation", "interruption", "replay")
BASE_SEED = 20261001
INTERRUPTION_ERRORS = (OSError, NetClientError)


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


def _factory():
    return NetClient.host_from_solution()


def _collect(root, **kwargs):
    collector = Collector(
        root,
        base_seed=kwargs.pop("base_seed", BASE_SEED),
        limits=kwargs.pop("limits", CollectorLimits(episodes=2, steps_per_episode=3)),
        workers=kwargs.pop("workers", 1),
        client_factory=kwargs.pop("client_factory", _factory),
        interruption_errors=INTERRUPTION_ERRORS,
        **kwargs,
    )
    return collector.run()


# -- seeds --------------------------------------------------------------------------------------------------


def _run_seeds(checker: Checker) -> None:
    first = [derive_seed(BASE_SEED, index) for index in range(4)]
    second = [derive_seed(BASE_SEED, index) for index in range(4)]
    checker.equal("seeds/deterministic", first, second)
    checker.equal("seeds/distinct-per-index", len(set(first)), len(first))
    checker.check("seeds/not-the-base-seed", all(seed != BASE_SEED for seed in first), f"{first}")
    checker.check("seeds/fit-safe-integer", all(0 <= seed < 2 ** 53 for seed in first), f"{first}")
    checker.check("seeds/different-base-gives-different-stream",
                  [derive_seed(BASE_SEED + 1, index) for index in range(4)] != first,
                  "换基种子必须换整条种子流")

    # 记录里的种子必须就是派生出来的那个（可核对，不是"事后填一个"）。
    root = pathlib.Path(tempfile.mkdtemp(prefix="d02-seeds-"))
    result = _collect(root, limits=CollectorLimits(episodes=3, steps_per_episode=2))
    checker.equal("seeds/three-episodes", 3, len(result.episode_ids))
    store = EpisodeStore(root)
    seeds_seen = []
    for episode_id in result.episode_ids:
        record = store.read(episode_id)
        seeds_seen.append(int(record["identities"]["seed"]))
    checker.equal("seeds/records-carry-derived-seeds",
                  sorted(derive_seed(BASE_SEED, index, "baseline") for index in range(3)), sorted(seeds_seen))
    checker.check("seeds/store-clean", not store.verify_all(), f"{store.verify_all()}")


# -- limits -------------------------------------------------------------------------------------------------


def _run_limits(checker: Checker) -> None:
    root = pathlib.Path(tempfile.mkdtemp(prefix="d02-limits-"))
    result = _collect(root, limits=CollectorLimits(episodes=2, steps_per_episode=3))
    checker.equal("limits/episodes-completed", "episodes_completed", result.stop_reason)
    checker.equal("limits/written-two", 2, result.counts["written"])

    # 每局步数上限 → 该局按**显式截断**收尾，而不是失败。
    truncated = _collect(pathlib.Path(tempfile.mkdtemp(prefix="d02-steps-")),
                         limits=CollectorLimits(episodes=1, steps_per_episode=2))
    store = EpisodeStore(truncated.progress_path.parent)
    record = store.read(truncated.episode_ids[0])
    checker.equal("limits/steps-limit-marks-truncated", "truncated", record["ending"]["class"])
    checker.check("limits/steps-limit-detail-is-explicit", "上限" in record["ending"]["detail"],
                  record["ending"]["detail"])
    checker.equal("limits/step-count-respected", 2, len(record["transitions"]))

    # 磁盘上限 → 停止原因点名磁盘，且该局**不写入**（不写半条）。
    disk_stop = _collect(pathlib.Path(tempfile.mkdtemp(prefix="d02-disk-")),
                         limits=CollectorLimits(episodes=5, steps_per_episode=6, bytes=4000))
    checker.equal("limits/disk-limit-stop-reason", "disk_limit", disk_stop.stop_reason)
    checker.check("limits/disk-limit-detail", "磁盘上限" in disk_stop.stop_detail, disk_stop.stop_detail)
    checker.check("limits/disk-limit-keeps-written-under-bound", disk_stop.written_bytes <= 4000,
                  f"{disk_stop.written_bytes}")

    # 在飞上限 → 停止原因点名在飞字节。
    inflight_stop = _collect(pathlib.Path(tempfile.mkdtemp(prefix="d02-inflight-")),
                             limits=CollectorLimits(episodes=5, steps_per_episode=20, inflight_bytes=1000))
    checker.equal("limits/inflight-limit-stop-reason", "inflight_limit", inflight_stop.stop_reason)
    checker.check("limits/inflight-limit-detail", "在飞快照" in inflight_stop.stop_detail,
                  inflight_stop.stop_detail)
    checker.check("limits/inflight-stop-does-not-write-half-record",
                  all(EpisodeStore(inflight_stop.progress_path.parent).record_path(episode_id).exists()
                      for episode_id in inflight_stop.episode_ids),
                  "停止前已写入的局必须都是完整记录")

    # 时长上限 → 停止原因点名时长。
    time_stop = _collect(pathlib.Path(tempfile.mkdtemp(prefix="d02-time-")),
                         limits=CollectorLimits(episodes=50, steps_per_episode=5, seconds=0.0001))
    checker.equal("limits/time-limit-stop-reason", "time_limit", time_stop.stop_reason)
    checker.check("limits/time-limit-detail", "时长上限" in time_stop.stop_detail, time_stop.stop_detail)

    # 进度文件必须写明原因与下一局序号（D-04 的恢复依据）。
    progress = read_progress(root)
    checker.equal("limits/progress-stop-reason", result.stop_reason, progress["stop_reason"])
    checker.equal("limits/progress-counts", 2, progress["written"])
    checker.check("limits/progress-next-index", progress["next_episode_index"] == 2,
                  f"{progress['next_episode_index']}")
    checker.check("limits/progress-no-timestamp",
                  not any(key in progress for key in ("created_at", "timestamp", "time")),
                  "进度不得用时间戳充当身份")


# -- isolation ----------------------------------------------------------------------------------------------


def _digests(root) -> list[str]:
    store = EpisodeStore(root)
    return [store.read(entry["episode_id"])["record_digest"] for entry in store.index()]


def _content_key(record) -> tuple:
    """与 worker 指派无关的内容身份：身份摘要 + 结束分类 + 逐步张量摘要。

    `record_digest` 含 provenance（worker/局序号），因此**不能**直接跨 worker 布局比较；
    游戏可见内容才是"worker 有没有串状态"的判据。
    """
    return (
        record["identities"]["dictionary_version"],
        record["identities"]["seed"],
        record["ending"]["class"],
        tuple(transition["tensor_digest"] for transition in record["transitions"]),
    )


def _content_keys(root) -> list[tuple]:
    store = EpisodeStore(root)
    return [_content_key(store.read(entry["episode_id"])) for entry in store.index()]


def _run_isolation(checker: Checker) -> None:
    limits = CollectorLimits(episodes=4, steps_per_episode=2)
    sequential_root = pathlib.Path(tempfile.mkdtemp(prefix="d02-seq-"))
    interleaved_root = pathlib.Path(tempfile.mkdtemp(prefix="d02-int-"))
    sequential = _collect(sequential_root, limits=limits, workers=1)
    interleaved = _collect(interleaved_root, limits=limits, workers=2)

    checker.equal("isolation/same-episode-count", len(sequential.episode_ids), len(interleaved.episode_ids))
    # 内容身份（逐步张量摘要）必须一致：worker 之间不得串状态。
    checker.equal("isolation/worker-interleaving-changes-nothing",
                  sorted(_content_keys(sequential_root)), sorted(_content_keys(interleaved_root)))
    # episode_id 由内容派生（不含 provenance），因此跨 worker 布局也必须相同。
    checker.equal("isolation/episode-ids-match-across-worker-layouts",
                  sorted(sequential.episode_ids), sorted(interleaved.episode_ids))
    checker.equal("isolation/unique-episode-ids", 4, len(set(interleaved.episode_ids)))

    # worker 指派是逐局轮转，且必须记录在来源里（同一局不会被两个 worker 各写一次）。
    store = EpisodeStore(interleaved_root)
    workers = [store.read(entry["episode_id"])["provenance"]["worker"] for entry in store.index()]
    checker.equal("isolation/worker-recorded-per-episode", ["0", "1", "0", "1"], workers)
    checker.check("isolation/store-clean", not store.verify_all(), f"{store.verify_all()}")


# -- interruption -------------------------------------------------------------------------------------------


def _run_interruption(checker: Checker) -> None:
    """故障注入：第 0 局跑到第 2 步时杀掉宿主（**注入的故障**，不是真实游戏事件）。"""
    state = {"killed": False}

    def fault(episode_index: int, client) -> None:
        if episode_index == 0:
            # 让这一局先写出两条确认转移，再在下一步把它杀掉。
            original = client.step

            def killing_step(*args, **kwargs):
                response = original(*args, **kwargs)
                if not state["killed"]:
                    state["killed"] = True
                    client.kill()
                return response

            client.step = killing_step  # type: ignore[assignment]

    root = pathlib.Path(tempfile.mkdtemp(prefix="d02-interrupt-"))
    result = _collect(root, limits=CollectorLimits(episodes=3, steps_per_episode=20), fault_injection=fault)

    checker.check("interruption/injection-fired", state["killed"], "故障注入没有触发")
    checker.equal("interruption/stops-with-host-lost", "host_lost", result.stop_reason)
    checker.check("interruption/detail-says-interrupted", "中断" in result.stop_detail, result.stop_detail)

    store = EpisodeStore(root)
    records = [store.read(entry["episode_id"]) for entry in store.index()]
    checker.equal("interruption/one-record", 1, len(records))
    record = records[0]
    checker.equal("interruption/ending-class", "interrupted", record["ending"]["class"])
    checker.check("interruption/not-recorded-as-win-or-loss",
                  record["ending"]["class"] not in ("terminal_win", "terminal_loss"),
                  record["ending"]["class"])
    checker.check("interruption/no-fabricated-terminal",
                  record["ending"]["terminal"] is None, f"{record['ending']['terminal']}")
    checker.check("interruption/confirmed-transitions-kept",
                  record["summary"]["confirmed_transitions"] >= 1,
                  f"{record['summary']}")
    # 中断局里的转移不得补造下一状态；若记录了"结果不确定"的那一步，必须是 unknown 且没有候选句柄。
    for transition in record["transitions"]:
        if transition["outcome"] == "unknown":
            checker.check("interruption/unknown-has-no-next-state",
                          transition["next_observation_id"] is None, f"{transition}")
            checker.check("interruption/unknown-claims-no-candidate",
                          transition["candidate_id"] == "", f"{transition}")
    checker.check("interruption/store-clean", not store.verify_all(), f"{store.verify_all()}")
    progress = read_progress(root)
    checker.equal("interruption/progress-records-interrupted", 1, progress["counts"]["interrupted"])


# -- replay -------------------------------------------------------------------------------------------------


def _run_replay(checker: Checker) -> None:
    limits = CollectorLimits(episodes=3, steps_per_episode=3)
    first_root = pathlib.Path(tempfile.mkdtemp(prefix="d02-replay-a-"))
    second_root = pathlib.Path(tempfile.mkdtemp(prefix="d02-replay-b-"))
    first = _collect(first_root, limits=limits)
    second = _collect(second_root, limits=limits)

    checker.equal("replay/same-episode-ids", list(first.episode_ids), list(second.episode_ids))
    checker.equal("replay/same-record-digests", sorted(_digests(first_root)), sorted(_digests(second_root)))
    checker.check("replay/identity-excludes-session-handles",
                  all("session" not in json_keys(record) for record in
                      [EpisodeStore(first_root).read(episode_id) for episode_id in first.episode_ids]),
                  "记录不得包含会话句柄字符串（控制元数据不进数据身份）")

    # 断言的判别力：换基种子必须换记录（否则"可重放"是恒真）。
    third_root = pathlib.Path(tempfile.mkdtemp(prefix="d02-replay-c-"))
    third = _collect(third_root, base_seed=BASE_SEED + 7, limits=limits)
    checker.check("replay/other-base-seed-differs", list(third.episode_ids) != list(first.episode_ids),
                  "换基种子必须得到不同记录")


def json_keys(document) -> set:
    import json

    return set(json.loads(json.dumps(document)).keys())


RUNNERS = {"seeds": _run_seeds, "limits": _run_limits, "isolation": _run_isolation,
           "interruption": _run_interruption, "replay": _run_replay}


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
