"""P-02 的**端到端与 AI 侧**测量（需宿主）。

用法（仓库根目录，需要先 ``dotnet build AutoTheSpire.sln``）::

    python ai-the-spire/tests/p02_measure.py [--only GROUP]

分组：

* ``stages``     —— AI 侧分项计时：编码（校验+张量化）、采样（概率+按语义键采样）、记录构建+落盘；
* ``loads``      —— 四类可重复负载（minimal/typical/large/long-settlement）的覆盖与可区分性 + wire 每请求延迟；
* ``machine``    —— 机器/配置/状态与候选规模、延迟分位、Python 分配峰值；
* ``diagnostics``—— 开启诊断不改变模型特征、不消费 RNG（用确定性动作序列不变来判定）。

**只报告测量，不写"达标"**：没有批准的预算就没有结论。wire 数字把规则推进/投影/codec 与传输、
进程边界**捆在一起**，不能与进程内三段相加相比（进程内三段见 `Net.TheSpire.Tests` 的 P-02 检查）。
"""

from __future__ import annotations

import copy
import hashlib
import json
import pathlib
import platform
import statistics
import subprocess
import sys
import tempfile
import time
import tracemalloc

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "ai-the-spire"))
sys.path.insert(0, str(REPO / "net-the-spire" / "client"))

from ai_the_spire.data import EpisodeStore, build_record  # noqa: E402
from ai_the_spire.encoding import EncodingConfig, build_manifest, tensorize  # noqa: E402
from ai_the_spire.runner import EpisodeRunner, ordered_candidates, sample_action, uniform_probabilities  # noqa: E402
from ai_the_spire.runner.episode import tensor_digest as _tensor_digest_of  # noqa: E402
from net_client import NetClient  # noqa: E402

GROUPS = ("stages", "loads", "machine", "diagnostics")
SCENARIO = "base-spire-combat-slice-v1"
SEED = 20261001
MAX_STEPS = 30
BUCKETS = ("minimal", "typical", "large", "long-settlement")

_ARTIFACTS: dict = {}


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


def _bucket(candidates: int, long_settlement: bool) -> str:
    if long_settlement:
        return "long-settlement"
    if candidates <= 2:
        return "minimal"
    if candidates <= 5:
        return "typical"
    return "large"


def _stats(values: list[float]) -> dict:
    if not values:
        return {"n": 0}
    ordered = sorted(values)
    return {
        "n": len(ordered),
        "p50": round(statistics.median(ordered), 4),
        "p90": round(ordered[min(len(ordered) - 1, int(len(ordered) * 0.9))], 4),
        "max": round(ordered[-1], 4),
    }


def _measure_episode() -> dict:
    """跑一局真实 episode，分别计时 AI 侧各段与 wire 每请求延迟。"""
    client = NetClient.host_from_solution()
    try:
        runner = EpisodeRunner(client, seed=SEED, max_steps=MAX_STEPS)
        identities = runner.prepare()
        catalog = runner._catalog  # noqa: SLF001 - 已通过校验的目录文档
        manifest = runner.manifest
        started = client.start(runner._identity, SCENARIO, seed=SEED)  # noqa: SLF001
        session = started["result"]["session_id"]

        stages: dict[str, list[float]] = {"encode": [], "sample": [], "record_write": []}
        wire: dict[str, list[float]] = {"observe": [], "step": []}
        buckets: dict[str, list[int]] = {name: [] for name in BUCKETS}
        class_order: list[str] = []
        transitions = []
        store = EpisodeStore(pathlib.Path(tempfile.mkdtemp(prefix="p02-store-")))

        for step in range(MAX_STEPS):
            begin = time.perf_counter()
            observed = client.observe(session)
            wire["observe"].append((time.perf_counter() - begin) * 1000.0)
            if not observed.get("ok"):
                break
            snapshot = observed["result"].get("snapshot")
            status = observed["result"]["session_status"]
            if status == "terminal" or snapshot is None:
                break

            begin = time.perf_counter()
            encoded = tensorize(snapshot, manifest, catalog=catalog)
            stages["encode"].append((time.perf_counter() - begin) * 1000.0)

            candidates = ordered_candidates(snapshot)
            begin = time.perf_counter()
            probabilities = uniform_probabilities(candidates)
            chosen = sample_action(candidates, probabilities, __import__("random").Random(SEED + step))
            stages["sample"].append((time.perf_counter() - begin) * 1000.0)

            actions = snapshot.get("legal_actions") or []
            is_long = all(a["action_type"].endswith("EndTurn") for a in actions) and len(actions) == 1
            bucket = _bucket(len(actions), is_long)
            class_order.append(bucket)
            buckets[bucket].append(len(actions))

            begin = time.perf_counter()
            response = client.step(session, snapshot["observation_id"], chosen.candidate_id,
                                   request_id=f"p02-{step}")
            wire["step"].append((time.perf_counter() - begin) * 1000.0)
            if not response.get("ok"):
                break

            transitions.append({
                "index": step,
                "request_id": f"p02-{step}",
                "observation_id": snapshot["observation_id"],
                "candidate_id": chosen.candidate_id,
                "semantic_key": chosen.semantic_key_text,
                "behavior_probability": probabilities[chosen.semantic_key_text],
                "candidates": dict(probabilities),
                "outcome": "confirmed",
                "next_observation_id": None,
                "tensor_digest": _tensor_digest_of(encoded),
                "snapshot": snapshot,
            })
    finally:
        client.shutdown()

    # 轨迹写入计时：用同一个 runner 跑一局真实 episode，把结果建成记录并落盘。
    writer_client = NetClient.host_from_solution()
    try:
        writer = EpisodeRunner(writer_client, seed=SEED, max_steps=MAX_STEPS)
        episode = writer.run(raise_on_failure=False)
    finally:
        writer_client.shutdown()
    begin = time.perf_counter()
    record = build_record(episode, provenance={"entry": "p02_measure", "source": "stdio-simulation"})
    store.write(record)
    stages["record_write"].append((time.perf_counter() - begin) * 1000.0)
    _ARTIFACTS["manifest"] = manifest

    return {"stages": stages, "wire": wire, "buckets": buckets, "class_order": class_order,
            "transitions": len(transitions), "records": transitions,
            "first_snapshot": transitions[0]["snapshot"] if transitions else None,
            "identities": identities, "catalog": catalog, "manifest": manifest}


# -- stages -------------------------------------------------------------------------------------------------


def _run_stages(checker: Checker) -> None:
    measured = _measure_episode()
    _ARTIFACTS["measured"] = measured
    for name, values in measured["stages"].items():
        checker.check(f"stages/{name}-has-samples", len(values) > 0, f"{len(values)}")
        stats = _stats(values)
        print(f"       AI {name}: n={stats['n']} p50={stats['p50']}ms p90={stats['p90']}ms max={stats['max']}ms",
              file=sys.stderr)
    checker.check("stages/encode-slower-than-sampling",
                  _stats(measured["stages"]["encode"])["p50"] > _stats(measured["stages"]["sample"])["p50"],
                  "编码（校验+张量化）应当明显重于一次采样")
    checker.check("stages/record-write-measured", _stats(measured["stages"]["record_write"])["n"] == 1,
                  "轨迹写入必须单独计时")


# -- loads --------------------------------------------------------------------------------------------------


def _run_loads(checker: Checker) -> None:
    measured = _ARTIFACTS.get("measured") or _measure_episode()
    _ARTIFACTS["measured"] = measured
    buckets = measured["buckets"]
    for name in BUCKETS:
        checker.check(f"loads/{name}-observed", len(buckets[name]) > 0,
                      f"{name} 没有样本；分档需覆盖 {BUCKETS}")
    # 分档必须真的不同（否则"分档"没有判别力）。
    if all(buckets[name] for name in BUCKETS):
        checker.check("loads/classes-are-distinguishable",
                      max(buckets["minimal"]) < min(buckets["large"]),
                      f"minimal={buckets['minimal']} large={buckets['large']}")
        checker.check("loads/long-settlement-has-fewest-candidates",
                      min(buckets["long-settlement"]) <= min(buckets["typical"]),
                      f"{buckets}")
    # 同一配置重复测量必须给出同一分档序列（负载可重复）。
    repeat = _measure_episode()
    checker.equal("loads/repeatable-class-sequence", measured["class_order"], repeat["class_order"])
    checker.equal("loads/repeatable-transition-count", measured["transitions"], repeat["transitions"])

    # wire 每请求延迟（**含**规则推进/投影/codec/传输/进程边界）。
    for name, values in measured["wire"].items():
        stats = _stats(values)
        print(f"       wire {name}: n={stats['n']} p50={stats['p50']}ms p90={stats['p90']}ms max={stats['max']}ms",
              file=sys.stderr)
        checker.check(f"loads/wire-{name}-measured", stats["n"] > 0, f"{stats}")
    checker.check("loads/wire-step-heavier-than-observe",
                  _stats(measured["wire"]["step"])["p50"] > _stats(measured["wire"]["observe"])["p50"],
                  "step 要推进规则，应当重于纯读 observe")


# -- machine ------------------------------------------------------------------------------------------------


def _run_machine(checker: Checker) -> None:
    measured = _ARTIFACTS.get("measured") or _measure_episode()
    _ARTIFACTS["measured"] = measured

    # 峰值内存：只对一次真实编码测量 Python 分配峰值（tracemalloc 看不到宿主进程的内存）。
    manifest = measured["manifest"]
    sample = measured["first_snapshot"]
    tracemalloc.start()
    if sample is not None:
        _ = tensorize(sample, manifest, catalog=measured["catalog"])
    allocation_peak = tracemalloc.get_traced_memory()[1]
    tracemalloc.stop()

    dotnet = subprocess.run(["dotnet", "--version"], capture_output=True, text=True, check=False).stdout.strip()
    report = {
        "machine": {
            "platform": platform.platform(),
            "python": sys.version.split()[0],
            "cpu_count": __import__("os").cpu_count(),
            "dotnet": dotnet,
        },
        "config": {
            "scenario_profile": SCENARIO,
            "seed": str(SEED),
            "max_steps": MAX_STEPS,
            "executor_profile_id": measured["identities"].executor_profile_id,
            "model_id": measured["identities"].model_id,
            "dictionary_version": measured["identities"].dictionary_version,
        },
        "state": {
            "transitions": measured["transitions"],
            "observed_classes": {name: len(measured["buckets"][name]) for name in BUCKETS},
        },
        "latency_ms": {
            "ai_stages": {name: _stats(values) for name, values in measured["stages"].items()},
            "wire": {name: _stats(values) for name, values in measured["wire"].items()},
        },
        "python_allocation_peak_bytes": allocation_peak,
    }
    path = pathlib.Path(tempfile.mkdtemp(prefix="p02-report-")) / "measurement.json"
    path.write_text(json.dumps(report, ensure_ascii=False, sort_keys=True, indent=2), encoding="utf-8")
    _ARTIFACTS["report"] = report

    checker.check("machine/machine-recorded", bool(report["machine"]["platform"])
                  and bool(report["machine"]["dotnet"]), f"{report['machine']}")
    checker.check("machine/config-recorded", report["config"]["max_steps"] == MAX_STEPS, f"{report['config']}")
    checker.check("machine/candidate-scale-recorded",
                  all(name in report["state"]["observed_classes"] for name in BUCKETS),
                  f"{report['state']}")
    checker.check("machine/latency-percentiles-recorded",
                  all("p90" in stats for stats in report["latency_ms"]["wire"].values() if stats.get("n")),
                  f"{report['latency_ms']}")
    checker.check("machine/allocation-peak-is-python-only",
                  "python_allocation_peak_bytes" in report,
                  "峰值只声明为 Python 分配峰值（tracemalloc），不是进程 RSS")
    checker.check("machine/no-budget-verdict",
                  not any(key in json.dumps(report) for key in ('"within_budget"', '"meets_budget"', '"达标"')),
                  "没有批准的预算就不得写达标结论")
    print(f"       report: {path}", file=sys.stderr)


# -- diagnostics --------------------------------------------------------------------------------------------


def _run_diagnostics(checker: Checker) -> None:
    """同一配置跑两遍：第二遍插入额外诊断读取；**动作序列与张量摘要必须完全一致**。"""
    plain = _measure_episode()

    client = NetClient.host_from_solution()
    try:
        runner = EpisodeRunner(client, seed=SEED, max_steps=MAX_STEPS)
        runner.prepare()
        catalog = runner._catalog  # noqa: SLF001
        manifest = runner.manifest
        started = client.start(runner._identity, SCENARIO, seed=SEED)  # noqa: SLF001
        session = started["result"]["session_id"]
        digests = []
        keys = []
        import random

        for step in range(len(plain["records"])):
            observed = client.observe(session)
            snapshot = observed["result"]["snapshot"]
            # 诊断读取：额外观察、额外序列化、额外校验、额外候选查询。
            _ = client.observe(session)
            _ = json.dumps(snapshot, ensure_ascii=False, sort_keys=True)
            candidates = ordered_candidates(snapshot)
            _ = ordered_candidates(copy.deepcopy(snapshot))
            probabilities = uniform_probabilities(candidates)
            chosen = sample_action(candidates, probabilities, random.Random(SEED + step))
            keys.append(chosen.semantic_key_text)
            digests.append(_tensor_digest_of(tensorize(snapshot, manifest, catalog=catalog)))
            response = client.step(session, snapshot["observation_id"], chosen.candidate_id,
                                   request_id=f"p02d-{step}")
            if not response.get("ok"):
                break
    finally:
        client.shutdown()

    plain_keys = [item["semantic_key"] for item in plain["records"]]
    plain_digests = [item["tensor_digest"] for item in plain["records"]]
    checker.equal("diagnostics/action-sequence-unchanged", plain_keys, keys[:len(plain_keys)])
    checker.equal("diagnostics/tensor-digests-unchanged", plain_digests, digests[:len(plain_digests)])

    # 诊断内容不得进入公开数据。
    catalog_text = json.dumps(plain["catalog"], ensure_ascii=False)
    for forbidden in ("diagnostic", "trace", "rng_state", "internal"):
        checker.check(f"diagnostics/{forbidden}-absent-from-catalog",
                      forbidden not in catalog_text, forbidden)
    snapshot_text = json.dumps(plain["first_snapshot"], ensure_ascii=False) if plain["first_snapshot"] else ""
    for forbidden in ("diagnostic", "rng_state", "state_hash"):
        checker.check(f"diagnostics/{forbidden}-absent-from-snapshot",
                      forbidden not in snapshot_text, forbidden)

    # 判别力对照：诊断内容根本进不了编码——含未知诊断键的快照会被**拒绝**，而不是被静默忽略。
    if plain["first_snapshot"]:
        injected = copy.deepcopy(plain["first_snapshot"])
        injected["diagnostics"] = {"rng_draws": 1}
        try:
            tensorize(injected, plain["manifest"], catalog=plain["catalog"])
            checker.check("diagnostics/injected-diagnostic-is-rejected", False,
                          "含未知诊断键的快照被编码接受了")
        except Exception as failure:  # EncodingError 或契约违规
            checker.check("diagnostics/injected-diagnostic-is-rejected",
                          "diagnostics" in str(failure) or "I-16" in str(failure), str(failure))


RUNNERS = {"stages": _run_stages, "loads": _run_loads, "machine": _run_machine,
           "diagnostics": _run_diagnostics}


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
