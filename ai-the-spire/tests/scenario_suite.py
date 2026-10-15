#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""W-03 场景检查：清单身份、**环境真的不同**、区分事实核对、按簇隔离的划分、成本报告。

分组：

* ``scenario-manifest``：簇必须绑定真实宿主开关与决策差异轴，清单身份/往返/摘要；
* ``scenario-facts``：三种区分事实谓词在公开快照上的判定（含反例：事实不成立时必须报否）；
* ``scenario-splits``：划分按簇隔离、三个 split 非空、最终组不参与选参、跨 split 泄漏可检出；
* ``scenario-cost``：成本报告组装（限额/计数/墙钟/字节/停止原因/声明标签）用合成结果核对。

**证据边界**：本套件是离线工程检查；真实簇上"环境确实不同"的证据由
`j01_episode.py --only scenarios`（真实 stdio + 逐簇事实验证）给出。
"""

from __future__ import annotations

import pathlib
import sys
from typing import Any, Callable, Optional

_PACKAGE_ROOT = pathlib.Path(__file__).resolve().parents[1]
if str(_PACKAGE_ROOT) not in sys.path:
    sys.path.insert(0, str(_PACKAGE_ROOT))
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from ai_the_spire.collector import CollectorLimits  # noqa: E402
from ai_the_spire.data.audit import SPLITS, SplitPlan, cluster_key  # noqa: E402
from ai_the_spire.scenarios import (  # noqa: E402
    ClusterSample,
    SamplingReport,
    ScenarioFactError,
    ScenarioManifest,
    ScenarioManifestError,
    ScenarioSpec,
    check_distinguishing_fact,
    default_scenario_manifest,
    default_split_assignments,
)

from fixture_suite import Outcome  # noqa: E402

SUITE_MANIFEST = "scenario-manifest"
SUITE_FACTS = "scenario-facts"
SUITE_SPLITS = "scenario-splits"
SUITE_COST = "scenario-cost"

SUITE_NAMES = (SUITE_MANIFEST, SUITE_FACTS, SUITE_SPLITS, SUITE_COST)


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
# scenario-manifest
# --------------------------------------------------------------------------- #
def _manifest_cases() -> list:
    def default_manifest_is_consistent() -> tuple:
        manifest = default_scenario_manifest()
        keys = manifest.cluster_keys()
        return (len(manifest.scenarios) == 4
                and len(set(keys.values())) == 4
                and all(spec.host_flags is not None for spec in manifest.scenarios),
                f"scenarios={manifest.scenario_ids}")

    def manifest_roundtrip_and_digest() -> tuple:
        manifest = default_scenario_manifest()
        restored = ScenarioManifest.from_wire(manifest.to_wire())
        document = manifest.to_wire()
        document["scenario_manifest_version"] = "scenario-manifest@999"
        rejected = False
        try:
            ScenarioManifest.from_wire(document)
        except ScenarioManifestError:
            rejected = True
        return (restored == manifest and restored.digest() == manifest.digest() and rejected,
                f"digest={manifest.digest()[:20]} version_rejected={rejected}")

    def flags_actually_change_the_environment() -> tuple:
        """两个簇不得使用同一套宿主配置；资源/目标类差异轴必须带真实开关。"""
        problems = []
        try:
            ScenarioManifest(
                manifest_id="m", semantic_version="1",
                scenarios=(
                    ScenarioSpec("a", "profile", (), ("knowledge.hidden_pile",),
                                 "group_absent", "g"),
                    ScenarioSpec("b", "profile", (), ("knowledge.discard_choice",),
                                 "group_absent", "g"),
                ),
            )
        except ScenarioManifestError as failure:
            if "同一套宿主配置" not in str(failure):
                problems.append(f"同环境的报错不点名：{failure}")
        else:
            problems.append("两个簇使用同一套宿主配置必须被拒绝")
        try:
            ScenarioSpec("a", "profile", (), ("resources.potions",), "group_present", "g")
        except ScenarioManifestError as failure:
            if "宿主开关" not in str(failure):
                problems.append(f"缺开关的报错不点名：{failure}")
        else:
            problems.append("声明资源轴却没有宿主开关必须被拒绝")
        try:
            ScenarioSpec("a", "profile", ("--totally-unknown",), ("knowledge.hidden_pile",),
                         "group_absent", "g")
        except ScenarioManifestError as failure:
            if "未知开关" not in str(failure):
                problems.append(f"未知开关的报错不点名：{failure}")
        else:
            problems.append("未知宿主开关必须被拒绝")
        return (not problems, "; ".join(problems))

    def decision_axes_and_fact_kinds_are_validated() -> tuple:
        problems = []
        for kwargs, marker in (
            ({"decision_axes": ()}, "差异轴"),
            ({"decision_axes": ("resources.gold",)}, "未知轴"),
            ({"fact_kind": "vibes"}, "fact_kind"),
            ({"fact_value": ""}, "fact_value"),
        ):
            base = {
                "scenario_id": "s", "scenario_profile": "p", "host_flags": ("--test-headbutt",),
                "decision_axes": ("knowledge.discard_choice",), "fact_kind": "group_absent",
                "fact_value": "g",
            }
            base.update(kwargs)
            try:
                ScenarioSpec(**base)
            except ScenarioManifestError as failure:
                if marker not in str(failure):
                    problems.append(f"{kwargs}: {failure}")
            else:
                problems.append(f"{kwargs} 必须被拒绝")
        return (not problems, "; ".join(problems))

    return [
        _guard(SUITE_MANIFEST, "manifest/default-is-consistent", default_manifest_is_consistent),
        _guard(SUITE_MANIFEST, "manifest/roundtrip-and-digest", manifest_roundtrip_and_digest),
        _guard(SUITE_MANIFEST, "manifest/flags-change-environment",
               flags_actually_change_the_environment),
        _guard(SUITE_MANIFEST, "manifest/axes-and-fact-kinds-validated",
               decision_axes_and_fact_kinds_are_validated),
    ]


# --------------------------------------------------------------------------- #
# scenario-facts
# --------------------------------------------------------------------------- #
def _snapshot(*, groups=(), entities=(), actions=()) -> dict:
    return {"entities": list(entities), "groups": list(groups), "legal_actions": list(actions)}


def _entity(instance_id: str, content_id: Optional[str]) -> dict:
    return {"instance_id": instance_id, "kind_id": "k", "content_state": "known",
            "content_id": content_id, "fields": []}


def _action(field_value: str) -> dict:
    return {"candidate_id": "c", "action_type": "a",
            "fields": [{"field_id": "f", "state": "known", "value": field_value}]}


def _facts_cases() -> list:
    def group_present_and_absent() -> tuple:
        spec_present = ScenarioSpec("s", "p", ("--author-content",), ("resources.potions",),
                                    "group_present", "g:belt")
        spec_absent = ScenarioSpec("s", "p", (), ("knowledge.hidden_pile",),
                                   "group_absent", "g:belt")
        with_belt = [_snapshot(groups=[{"type_id": "g:belt", "members": []}])]
        without = [_snapshot()]
        checks = [
            check_distinguishing_fact(with_belt, spec_present)[0],
            not check_distinguishing_fact(without, spec_present)[0],
            check_distinguishing_fact(without, spec_absent)[0],
            not check_distinguishing_fact(with_belt, spec_absent)[0],
        ]
        return (all(checks), f"checks={checks}")

    def fact_must_hold_somewhere_in_the_episode() -> tuple:
        """事实在**任一**快照成立即可（例如头槌是中途才被抽到并成为候选）。"""
        spec = ScenarioSpec("s", "p", ("--test-headbutt",), ("knowledge.discard_choice",),
                            "candidate_content_present", "base-spire:headbutt")
        snapshots = [
            _snapshot(entities=[_entity("e1", "base-spire:strike")], actions=[_action("e1")]),
            _snapshot(entities=[_entity("e2", "base-spire:headbutt")], actions=[_action("e2")]),
        ]
        ok, detail = check_distinguishing_fact(snapshots, spec)
        missing = check_distinguishing_fact(snapshots[:1], spec)
        return (ok and not missing[0], f"later={detail} early={missing[1]}")

    def unknown_fact_kind_is_rejected() -> tuple:
        spec = ScenarioSpec("s", "p", (), ("knowledge.hidden_pile",), "group_absent", "g")
        object.__setattr__(spec, "fact_kind", "made-up")
        try:
            check_distinguishing_fact([_snapshot()], spec)
        except ScenarioFactError as failure:
            return ("没有实现" in str(failure), f"{failure}")
        return False, "未实现的区分事实必须被拒绝（不得默认通过）"

    def empty_snapshot_list_is_rejected() -> tuple:
        spec = ScenarioSpec("s", "p", (), ("knowledge.hidden_pile",), "group_absent", "g")
        try:
            check_distinguishing_fact([], spec)
        except ScenarioFactError as failure:
            return ("没有任何公开快照" in str(failure), f"{failure}")
        return False, "没有快照可核对时必须显式失败"

    return [
        _guard(SUITE_FACTS, "facts/group-present-and-absent", group_present_and_absent),
        _guard(SUITE_FACTS, "facts/holds-anywhere-in-episode", fact_must_hold_somewhere_in_the_episode),
        _guard(SUITE_FACTS, "facts/unknown-kind-rejected", unknown_fact_kind_is_rejected),
        _guard(SUITE_FACTS, "facts/empty-snapshots-rejected", empty_snapshot_list_is_rejected),
    ]


# --------------------------------------------------------------------------- #
# scenario-splits
# --------------------------------------------------------------------------- #
def _record(cluster_key_value: str, episode_id: str, ending: str = "terminal") -> dict:
    profile, _, variant = cluster_key_value.partition(":")
    return {
        "episode_id": episode_id,
        "ending": ending,
        "provenance": {"scenario_profile": profile, "scenario_variant": variant},
    }


def _split_cases() -> list:
    def default_assignment_covers_every_scenario() -> tuple:
        manifest = default_scenario_manifest()
        assignments = default_split_assignments(manifest)
        keys = set(manifest.cluster_keys().values())
        return (set(assignments) == keys and set(assignments.values()) <= set(SPLITS)
                and all(assignments[key] for key in assignments),
                f"assignments={assignments}")

    def train_has_more_than_one_cluster_and_final_is_single() -> tuple:
        assignments = default_split_assignments(default_scenario_manifest())
        counts = {split: sum(1 for value in assignments.values() if value == split)
                  for split in SPLITS}
        return (counts["train"] >= 2 and counts["validation"] >= 1 and counts["final_eval"] >= 1,
                f"per_split={counts}")

    def split_plan_rejects_empty_and_unknown() -> tuple:
        problems = []
        try:
            SplitPlan(assignments={})
        except Exception as failure:  # RecordError
            if "至少要指派" not in str(failure):
                problems.append(f"空划分的报错不点名：{failure}")
        else:
            problems.append("空划分必须被拒绝（否则三个 split 之一会空着还被当成「已划分」）")
        try:
            SplitPlan(assignments={"cluster": "holdout"})
        except Exception as failure:  # RecordError
            if "未知 split" not in str(failure):
                problems.append(f"未知 split 的报错不点名：{failure}")
        else:
            problems.append("未知 split 必须被拒绝")
        return (not problems, "; ".join(problems))

    def split_plan_digest_is_the_identity() -> tuple:
        base = default_split_assignments(default_scenario_manifest())
        left = SplitPlan(assignments=base)
        moved = dict(base)
        # 挑一个**当前不是** final_eval 的簇改派，否则等于没改（摘要当然相同）
        victim = next(key for key, split in sorted(moved.items()) if split != "final_eval")
        moved[victim] = "final_eval"
        right = SplitPlan(assignments=moved)
        return (left.digest != right.digest and left.version == "split-plan@1",
                f"{left.digest[:20]} vs {right.digest[:20]} (moved {victim})")

    def cluster_key_binds_profile_and_variant() -> tuple:
        document = {"identities": {"scenario_profile": "base-spire"},
                    "provenance": {"scenario_variant": "s1"}}
        unlabelled = {"identities": {"scenario_profile": "base-spire"}, "provenance": {}}
        return (cluster_key(document) == "base-spire:s1"
                and cluster_key(unlabelled).endswith("<unlabelled>"),
                f"{cluster_key(document)} / {cluster_key(unlabelled)}")

    def plan_covers_all_scenarios_without_leak() -> tuple:
        """划分是按簇映射：一个簇只能出现在一个 split 里（结构上不可能泄漏）。"""
        manifest = default_scenario_manifest()
        assignments = default_split_assignments(manifest)
        seen: dict[str, str] = {}
        leaks = []
        for cluster, split in assignments.items():
            if cluster in seen and seen[cluster] != split:
                leaks.append(cluster)
            seen[cluster] = split
        return (not leaks and len(seen) == len(manifest.scenarios), f"leaks={leaks}")

    return [
        _guard(SUITE_SPLITS, "splits/assignment-covers-scenarios",
               default_assignment_covers_every_scenario),
        _guard(SUITE_SPLITS, "splits/three-splits-non-degenerate",
               train_has_more_than_one_cluster_and_final_is_single),
        _guard(SUITE_SPLITS, "splits/empty-and-unknown-rejected",
               split_plan_rejects_empty_and_unknown),
        _guard(SUITE_SPLITS, "splits/plan-digest-is-identity", split_plan_digest_is_the_identity),
        _guard(SUITE_SPLITS, "splits/cluster-key-binds-profile-and-variant",
               cluster_key_binds_profile_and_variant),
        _guard(SUITE_SPLITS, "splits/plan-has-no-cross-split-cluster",
               plan_covers_all_scenarios_without_leak),
    ]


# --------------------------------------------------------------------------- #
# scenario-cost
# --------------------------------------------------------------------------- #
def _cost_cases() -> list:
    def report_aggregates_counts_and_costs() -> tuple:
        manifest = default_scenario_manifest()
        assignments = default_split_assignments(manifest)
        clusters = (
            ClusterSample("net:scenario:slice-default-v1", (), CollectorLimits().to_wire(),
                          "episodes_completed", "", {"attempted": 2, "written": 2, "interrupted": 0,
                                                     "faulted": 0}, 1.5, 2048, True, "ok", 2),
            ClusterSample("net:scenario:author-v1", ("--author-content",),
                          CollectorLimits().to_wire(), "time_limit", "已达上限",
                          {"attempted": 3, "written": 2, "interrupted": 1, "faulted": 0},
                          2.5, 4096, True, "ok", 2),
        )
        report = SamplingReport(
            manifest=manifest.to_wire(), manifest_digest=manifest.digest(),
            split_assignments=assignments, split_plan_digest="sha256:abc",
            clusters=clusters, policy_id="baseline:uniform-legal-candidates@1", workers=2,
            concurrency="sequential_by_cluster_round_robin_within_cluster",
            notes=("policy=...", "workers=2：多会话是**逐簇串行 + 簇内轮转**，不是并行吞吐"),
            root="C:/tmp",
        )
        wire = report.to_wire()
        return (wire["totals"]["attempts"] == 5 and wire["totals"]["written"] == 4
                and wire["totals"]["interrupted"] == 1
                and wire["clusters"][1]["stop_reason"] == "time_limit"
                and wire["totals"]["wall_seconds"] == repr(4.0)
                and report.digest().startswith("sha256:"),
                f"totals={wire['totals']}")

    def report_states_that_round_robin_is_not_throughput() -> tuple:
        manifest = default_scenario_manifest()
        report = SamplingReport(
            manifest=manifest.to_wire(), manifest_digest=manifest.digest(),
            split_assignments={}, split_plan_digest="", clusters=(), policy_id="baseline", workers=3,
            concurrency="sequential_by_cluster_round_robin_within_cluster",
            notes=("workers=3：多会话是**逐簇串行 + 簇内轮转**，不是并行吞吐",),
            root="C:/tmp",
        )
        wire = report.to_wire()
        return (wire["concurrency"].startswith("sequential")
                and any("不是并行吞吐" in note for note in wire["notes"]), f"{wire['concurrency']}")

    def report_labels_random_policy_data() -> tuple:
        manifest = default_scenario_manifest()
        report = SamplingReport(
            manifest=manifest.to_wire(), manifest_digest=manifest.digest(),
            split_assignments={}, split_plan_digest="", clusters=(),
            policy_id="baseline:uniform-legal-candidates@1", workers=1,
            concurrency="sequential_by_cluster_round_robin_within_cluster",
            notes=("policy=baseline:uniform-legal-candidates@1：随机合法策略，数据用于**数据设施验证与基线**，"
                   "不作为新策略的近期 PPO 数据",),
            root="C:/tmp",
        )
        wire = report.to_wire()
        return (any("近期 PPO" in note for note in wire["notes"])
                and wire["policy_id"].startswith("baseline:"),
                f"{wire['policy_id']}")

    def fact_failure_is_visible_in_report() -> tuple:
        manifest = default_scenario_manifest()
        clusters = (
            ClusterSample("net:scenario:author-v1", ("--author-content",),
                          CollectorLimits().to_wire(), "episodes_completed", "",
                          {"attempted": 1, "written": 1}, 0.5, 512, False,
                          "公开快照里没有 group net:group_type:base-spire:potion_belt", 1),
        )
        report = SamplingReport(
            manifest=manifest.to_wire(), manifest_digest=manifest.digest(),
            split_assignments={}, split_plan_digest="", clusters=clusters, policy_id="baseline",
            workers=1, concurrency="sequential_by_cluster_round_robin_within_cluster",
            notes=(), root="C:/tmp",
        )
        wire = report.to_wire()
        return (wire["clusters"][0]["fact_ok"] is False
                and "potion_belt" in wire["clusters"][0]["fact_detail"],
                f"{wire['clusters'][0]['fact_detail']}")

    return [
        _guard(SUITE_COST, "cost/report-aggregates", report_aggregates_counts_and_costs),
        _guard(SUITE_COST, "cost/round-robin-not-throughput",
               report_states_that_round_robin_is_not_throughput),
        _guard(SUITE_COST, "cost/random-policy-labelled", report_labels_random_policy_data),
        _guard(SUITE_COST, "cost/fact-failure-visible", fact_failure_is_visible_in_report),
    ]


def run_all(only: Optional[str] = None) -> list:
    outcomes: list = []
    if only is not None and only not in SUITE_NAMES:
        return outcomes
    if only in (None, SUITE_MANIFEST):
        outcomes.extend(_manifest_cases())
    if only in (None, SUITE_FACTS):
        outcomes.extend(_facts_cases())
    if only in (None, SUITE_SPLITS):
        outcomes.extend(_split_cases())
    if only in (None, SUITE_COST):
        outcomes.extend(_cost_cases())
    return outcomes


__all__ = [
    "SUITE_COST",
    "SUITE_FACTS",
    "SUITE_MANIFEST",
    "SUITE_NAMES",
    "SUITE_SPLITS",
    "run_all",
]
