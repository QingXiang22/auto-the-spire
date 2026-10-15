"""D-03 的检查：数据审计与冻结划分（需要宿主：先用 Collector 采两批不同场景簇的数据）。

用法（仓库根目录，需要先 ``dotnet build AutoTheSpire.sln``）::

    python ai-the-spire/tests/d03_audit.py [--only GROUP]

分组：

* ``split``      —— 冻结划分的摘要身份、簇到 split 的唯一映射、退化划分必须被**报告**（不是被随机切分掩盖）；
* ``rewards``    —— 原始事实里不得有奖励/回报/优势/价值字段，也不得有未选动作收益标签；
* ``integrity``  —— 悬空引用、身份混用、摘要不符必须被发现；
* ``counts``     —— 总尝试/有效/中断/故障/截断分别计数，中断局不混进可用样本；
* ``drift``      —— 分项分布报告（结束分类/步数/候选规模/种子），不设未批准的门槛。

**故障注入或测试场景产生的数据不代表真实游戏或真实模拟**：本文件用切片自带的测试场景开关
`--test-headbutt` 造出第二个场景簇，只为验证"划分按簇、同簇不跨 split"，不声称那是原版内容。
"""

from __future__ import annotations

import copy
import json
import pathlib
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "ai-the-spire"))
sys.path.insert(0, str(REPO / "net-the-spire" / "client"))

from ai_the_spire.collector import Collector, CollectorLimits  # noqa: E402
from ai_the_spire.data import (  # noqa: E402
    SPLITS,
    EpisodeStore,
    SplitPlan,
    audit,
    cluster_key,
    dangling_references,
    find_reward_labels,
)
from ai_the_spire.data.audit import load_audit, write_audit  # noqa: E402
from net_client import NetClient, NetClientError  # noqa: E402

GROUPS = ("split", "rewards", "integrity", "counts", "drift")
BASE_SEED = 20261001
INTERRUPTION_ERRORS = (OSError, NetClientError)

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


# -- 采两批不同场景簇的数据 ----------------------------------------------------------------------------------


def _collect(root: pathlib.Path, *, variant: str, test_headbutt: bool, episodes: int) -> None:
    def factory():
        args = ["--test-headbutt"] if test_headbutt else None
        return NetClient.host_from_solution(extra_args=args)

    Collector(
        root,
        base_seed=BASE_SEED,
        limits=CollectorLimits(episodes=episodes, steps_per_episode=4),
        workers=1,
        client_factory=factory,
        interruption_errors=INTERRUPTION_ERRORS,
        scenario_variant=variant,
    ).run()


def _prepare(checker: Checker) -> None:
    """采两批（两个场景簇）到**同一个产物目录**，供审计使用。"""
    if _ARTIFACTS.get("root") is not None:
        return
    root = pathlib.Path(tempfile.mkdtemp(prefix="d03-"))
    _collect(root, variant="baseline", test_headbutt=False, episodes=3)
    _collect(root, variant="test-headbutt", test_headbutt=True, episodes=2)
    _ARTIFACTS["root"] = root
    store = EpisodeStore(root)
    _ARTIFACTS["store"] = store
    checker.equal("prepare/two-clusters", 2, len({cluster_key(store.read(e["episode_id"]))
                                                  for e in store.index()}))
    checker.equal("prepare/five-records", 5, len(store.index()))


# -- split --------------------------------------------------------------------------------------------------


def _run_split(checker: Checker) -> None:
    _prepare(checker)
    store = _ARTIFACTS["store"]
    documents = [store.read(entry["episode_id"]) for entry in store.index()]
    clusters = sorted({cluster_key(document) for document in documents})
    checker.equal("split/two-clusters", 2, len(clusters))

    plan = SplitPlan(assignments={clusters[0]: "train", clusters[1]: "validation"})
    result = audit(store, plan=plan)
    checker.check("split/plan-digest-stable", plan.digest == SplitPlan(plan.assignments).digest,
                  "同一划分必须给出同一身份")
    checker.check("split/plan-digest-changes-with-assignment",
                  plan.digest != SplitPlan(assignments={clusters[0]: "validation",
                                                        clusters[1]: "train"}).digest,
                  "改动划分必须换身份")
    checker.check("split/no-cluster-in-two-splits", not result.problems, f"{result.problems}")
    checker.equal("split/train-episodes", 3, result.split_counts["train"]["episodes"])
    checker.equal("split/validation-episodes", 2, result.split_counts["validation"]["episodes"])
    checker.check("split/one-cluster-per-nonempty-split",
                  all(len(bucket["clusters"]) == 1
                      for split, bucket in result.split_counts.items() if bucket["episodes"] > 0),
                  f"{result.split_counts}")
    # 空 split 必须被**报告**为退化，而不是靠随机切分掩盖。
    checker.check("split/degenerate-split-reported",
                  any("退化" in note for note in result.notes), f"{result.notes}")
    checker.check("split/final-eval-empty-and-flagged",
                  result.split_counts["final_eval"]["episodes"] == 0, f"{result.split_counts}")

    # 缺覆盖：有簇未指派时必须报出来。
    partial = audit(store, plan=SplitPlan(assignments={clusters[0]: "train"}))
    checker.check("split/uncovered-cluster-reported",
                  any("未被划分覆盖" in problem for problem in partial.problems), f"{partial.problems}")

    # 泄漏风险的真实形态：记录缺少场景簇标签时，不同配置会被静默并成一簇（同簇跨 split 的入口）。
    unlabelled = copy.deepcopy(documents[0])
    unlabelled["provenance"].pop("scenario_variant")
    labelled = copy.deepcopy(documents[1])
    from ai_the_spire.data.audit import UNLABELLED_CLUSTER  # noqa: PLC0415

    checker.check("split/unlabelled-cluster-not-silently-merged",
                  cluster_key(unlabelled).endswith(UNLABELLED_CLUSTER)
                  and not cluster_key(labelled).endswith(UNLABELLED_CLUSTER),
                  "缺少标签的记录不得默认成与其它记录同簇")
    root = pathlib.Path(tempfile.mkdtemp(prefix="d03-unlabelled-"))
    _collect(root, variant="baseline", test_headbutt=False, episodes=1)
    unlabelled_store = EpisodeStore(root)
    from ai_the_spire.data.store import _digest_material, digest_of  # noqa: PLC0415

    document = copy.deepcopy(unlabelled_store.read(unlabelled_store.index()[0]["episode_id"]))
    document["provenance"].pop("scenario_variant")
    document["record_digest"] = digest_of(_digest_material(document))
    (unlabelled_store.records_dir / f"{document['episode_id']}.json").write_bytes(
        json.dumps(document, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8"))
    unlabelled_audit = audit(unlabelled_store,
                             plan=SplitPlan(assignments={
                                 cluster_key(document): "train"}))
    checker.check("split/unlabelled-cluster-flagged",
                  any("缺少场景簇标签" in problem for problem in unlabelled_audit.problems),
                  f"{unlabelled_audit.problems}")

    path = write_audit(result, pathlib.Path(tempfile.mkdtemp(prefix="d03-audit-")) / "audit.json")
    checker.check("split/audit-written-and-reloadable",
                  load_audit(path)["plan"]["digest"] == plan.digest, f"{path}")


# -- rewards ------------------------------------------------------------------------------------------------


def _run_rewards(checker: Checker) -> None:
    _prepare(checker)
    store = _ARTIFACTS["store"]
    for entry in store.index():
        document = store.read(entry["episode_id"])
        checker.check(f"rewards/{entry['episode_id'][:12]}-no-reward-keys",
                      not find_reward_labels(document), f"{find_reward_labels(document)[:3]}")
        for transition in document["transitions"]:
            # 未选动作收益标签：候选表只能有概率，不得给未选动作任何收益/价值。
            checker.check(f"rewards/{transition['request_id']}-candidates-are-probabilities-only",
                          all(isinstance(value, float) for value in transition["candidates"].values()),
                          "候选表只能是概率")
            checker.check(f"rewards/{transition['request_id']}-no-per-candidate-reward",
                          all(not key.lower().startswith(("reward", "value", "q_", "return"))
                              for key in transition["candidates"]),
                          "候选键不得是收益标签")

    # 判别力：注入一个 reward 字段必须被发现。
    document = store.read(store.index()[0]["episode_id"])
    tampered = copy.deepcopy(document)
    tampered["transitions"][0]["candidates_reward"] = {"x": 1.0}
    checker.check("rewards/injected-reward-detected", bool(find_reward_labels(tampered)),
                  "注入的奖励字段必须被发现")


# -- integrity ----------------------------------------------------------------------------------------------


def _run_integrity(checker: Checker) -> None:
    _prepare(checker)
    store = _ARTIFACTS["store"]
    documents = [store.read(entry["episode_id"]) for entry in store.index()]
    for document in documents:
        checker.check(f"integrity/{document['episode_id'][:12]}-no-dangling",
                      not dangling_references(document), f"{dangling_references(document)[:2]}")

    # ① 悬空引用注入。
    tampered = copy.deepcopy(documents[0])
    tampered["transitions"][0]["snapshot"]["piles"][0]["visible_members"].append("e-does-not-exist")
    checker.check("integrity/dangling-member-detected",
                  any("悬空引用" in problem for problem in dangling_references(tampered)),
                  f"{dangling_references(tampered)}")

    # ② 身份混用：把两个不同目录的记录合到一个目录里。
    mixed_root = pathlib.Path(tempfile.mkdtemp(prefix="d03-mixed-"))
    _collect(mixed_root, variant="baseline", test_headbutt=False, episodes=1)
    mixed_store = EpisodeStore(mixed_root)
    # 造一条"另一个目录"的记录：改 catalog_id 后重签名并落盘。
    from ai_the_spire.data import EpisodeRecord  # noqa: PLC0415
    from ai_the_spire.data.store import _digest_material, digest_of  # noqa: PLC0415

    other = copy.deepcopy(mixed_store.read(mixed_store.index()[0]["episode_id"]))
    other["identities"]["catalog_id"] = "net:catalog:another-slice"
    other["episode_id"] = "ep-" + "f" * 24
    other["record_digest"] = digest_of(_digest_material(other))
    (mixed_store.records_dir / f"{other['episode_id']}.json").write_bytes(
        json.dumps(other, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8"))
    mixed_audit = audit(mixed_store)
    checker.check("integrity/mixed-identity-detected",
                  any("身份" in problem for problem in mixed_audit.problems), f"{mixed_audit.problems}")

    # ③ 摘要不符：改动**身份相关**字段必须被发现（派生的 summary 不在摘要里，改它不算篡改）。
    path = store.record_path(documents[0]["episode_id"])
    original = path.read_bytes()
    try:
        broken = copy.deepcopy(documents[0])
        broken["transitions"][0]["semantic_key"] = "tampered"
        path.write_bytes(json.dumps(broken, ensure_ascii=False, sort_keys=True,
                                    separators=(",", ":")).encode("utf-8"))
        result = audit(store)
        checker.check("integrity/digest-mismatch-detected",
                      any("不可读" in problem or "摘要" in problem for problem in result.problems),
                      f"{result.problems[:2]}")
    finally:
        path.write_bytes(original)
    restored = audit(store)
    checker.check("integrity/restored-audit-clean", not restored.problems, f"{restored.problems}")


# -- counts -------------------------------------------------------------------------------------------------


def _run_counts(checker: Checker) -> None:
    _prepare(checker)
    store = _ARTIFACTS["store"]
    result = audit(store, attempted=7)
    checker.equal("counts/attempted-reported-from-progress", 7, result.counts["attempted"])
    checker.equal("counts/records", 5, result.counts["records"])
    checker.equal("counts/usable-episodes", 5, result.counts["usable_episodes"])
    checker.equal("counts/usable-transitions", 20, result.counts["usable_transitions"])
    for key in ("terminal_win", "terminal_loss", "truncated", "interrupted", "faulted", "unknown_transitions"):
        checker.check(f"counts/{key}-reported", key in result.counts, f"{sorted(result.counts)}")

    # 中断/故障局不得混进可用样本：注入一条 interrupted 记录。
    root = pathlib.Path(tempfile.mkdtemp(prefix="d03-counts-"))
    _collect(root, variant="baseline", test_headbutt=False, episodes=1)
    injected = EpisodeStore(root)
    document = copy.deepcopy(injected.read(injected.index()[0]["episode_id"]))
    from ai_the_spire.data.store import _digest_material, digest_of  # noqa: PLC0415

    document["ending"] = {"class": "interrupted", "detail": "injected", "terminal": None}
    document["transitions"][-1]["outcome"] = "unknown"
    document["transitions"][-1]["candidate_id"] = ""
    document["transitions"][-1]["next_observation_id"] = None
    document["record_digest"] = digest_of(_digest_material(document))
    injected.write_document = None  # 占位：明确不通过 store.write 覆盖
    (injected.records_dir / f"{document['episode_id']}.json").write_bytes(
        json.dumps(document, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8"))
    injected_audit = audit(injected)
    checker.equal("counts/interrupted-counted-separately", 1, injected_audit.counts["interrupted"])
    checker.equal("counts/interrupted-not-usable", 0, injected_audit.counts["usable_episodes"])
    checker.equal("counts/unknown-transition-counted", 1, injected_audit.counts["unknown_transitions"])


# -- drift --------------------------------------------------------------------------------------------------


def _run_drift(checker: Checker) -> None:
    _prepare(checker)
    store = _ARTIFACTS["store"]
    result = audit(store)
    distributions = result.distributions
    checker.check("drift/endings-reported", all(key in distributions["endings"] for key in
                                                ("terminal_win", "terminal_loss", "truncated")),
                  f"{distributions['endings']}")
    checker.check("drift/steps-stats-reported",
                  distributions["steps"]["count"] == 5 and distributions["steps"]["max"] is not None,
                  f"{distributions['steps']}")
    checker.check("drift/candidate-scale-reported", distributions["candidates"]["count"] > 0,
                  f"{distributions['candidates']}")
    checker.equal("drift/five-distinct-seeds", 5, len(distributions["seeds"]))
    checker.check("drift/seeds-differ-across-clusters",
                  len({seed for seed in distributions["seeds"]}) == 5,
                  "两个场景簇不得共用种子流（否则产物目录里会出现同种子不同配置）")
    checker.check("drift/no-threshold-claimed",
                  not any("达标" in note or "门槛" in note for note in result.notes),
                  "没有批准预算时只报告测量，不写达标")


RUNNERS = {"split": _run_split, "rewards": _run_rewards, "integrity": _run_integrity,
           "counts": _run_counts, "drift": _run_drift}


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
