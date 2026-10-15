#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""W-04 奖励派生层检查：单位保留、公开事实来源、缺失事实的显式行为、标量化与版本化。

分组：

* ``reward-profile``：Profile 结构校验（分量/单位/来源/权重覆盖/版本/摘要往返）；
* ``reward-derive``：从**公开快照序列**派生向量与标量（含"塞隐藏诱饵也不改结果"的公开性探针）；
* ``reward-missing``：缺必需事实 ⇒ 显式失败；optional 分量 ⇒ 记 unavailable 且不参与标量化。

**证据边界**：本套件证明"奖励层按显式配置可核对地派生"，**不**决定该奖励什么——
真实实验的权重/折扣/时间尺度由 W-07 冻结的 Profile 给出。
"""

from __future__ import annotations

import copy
import pathlib
import sys
from typing import Any, Callable, Optional

_PACKAGE_ROOT = pathlib.Path(__file__).resolve().parents[1]
if str(_PACKAGE_ROOT) not in sys.path:
    sys.path.insert(0, str(_PACKAGE_ROOT))
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from ai_the_spire.reward import (  # noqa: E402
    RewardComponent,
    RewardError,
    RewardFactPaths,
    RewardProfile,
    RewardProfileError,
    STANDARD_UNITS,
    derive_episode_reward,
    standard_short_task_profile,
)

import tensor_suite  # noqa: E402
from fixture_suite import Outcome  # noqa: E402

SUITE_PROFILE = "reward-profile"
SUITE_DERIVE = "reward-derive"
SUITE_MISSING = "reward-missing"

SUITE_NAMES = (SUITE_PROFILE, SUITE_DERIVE, SUITE_MISSING)

BUNDLE = "combat"

#: 分量 id 常量（测试里独立写一遍，不复用被测模块的常量）。
ENEMY_HP_DELTA = "net:reward:enemy_hp_delta"
PLAYER_HP_DELTA = "net:reward:player_hp_delta"
TURN_COUNT = "net:reward:turn_count"
DECISION_COUNT = "net:reward:decision_count"
POTION_SPENT = "net:reward:potion_spent"
OUTCOME_WIN = "net:reward:outcome_win"
OUTCOME_LOSS = "net:reward:outcome_loss"

#: AI 夹具（`tests/fixtures/combat`）用的是 `fixture:` 前缀的公开 id ⇒ 用**声明式 facts** 适配。
#: 这正好检验"派生层不把某个 Gym 的字段 id 写死在代码里"。
FIXTURE_FACT_PATHS = RewardFactPaths(
    player_kind_id="fixture:player",
    player_hp_field_id="fixture:hp",
    monster_kind_id="fixture:enemy",
    monster_hp_field_id="fixture:hp",
    player_turn_field_id="fixture:cards_played_this_turn",
    potion_belt_group_type_id="fixture:potion_belt",
)

FIXTURE_MONSTER_KIND = "fixture:enemy"
FIXTURE_MONSTER_HP = "fixture:hp"
FIXTURE_PLAYER_KIND = "fixture:player"
FIXTURE_PLAYER_HP = "fixture:hp"
FIXTURE_PLAYER_TURN = "fixture:cards_played_this_turn"
FIXTURE_BELT_GROUP = "fixture:potion_belt"


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


def _fixture_profile(weights, **kwargs) -> RewardProfile:
    """带夹具公开路径的标准分量表（权重仍须显式给出）。"""
    return standard_short_task_profile(weights, facts=FIXTURE_FACT_PATHS, **kwargs)


# --------------------------------------------------------------------------- #
# 公开快照序列（离线构造：只改**公开字段**的值，模拟一局推进）
# --------------------------------------------------------------------------- #
def _edit_field(snapshot: dict, kind_id: str, field_id: str, value: str) -> dict:
    """把某个公开 number 字段改成给定规范十进制字符串（测试场景，只动公开事实）。"""
    document = copy.deepcopy(snapshot)
    changed = False
    for entity in document.get("entities") or []:
        if entity.get("kind_id") != kind_id:
            continue
        for field in entity.get("fields") or []:
            if field.get("field_id") == field_id and field.get("state") == "known":
                field["value"] = value
                changed = True
    if not changed:
        raise AssertionError(f"快照里没有可改的公开字段 {kind_id}/{field_id}")
    return document


def _set_outcome(snapshot: dict, outcome: str, reason_id: str) -> dict:
    document = copy.deepcopy(snapshot)
    document["terminal_result"] = {"reason_id": reason_id, "outcome": outcome, "facts": []}
    return document


def _with_potion_belt(snapshot: dict, members: list, group_type_id: str) -> dict:
    """给构造出来的公开快照加一个药水栏 group（成员用快照里已有的公开引用）。"""
    document = copy.deepcopy(snapshot)
    groups = [group for group in document.get("groups") or []
              if group.get("type_id") != group_type_id]
    groups.append({
        "type_id": group_type_id,
        "owner_ref": document["entities"][0]["instance_id"],
        "members": [{"entity_ref": reference, "multiplicity": 1} for reference in members],
    })
    document["groups"] = groups
    return document


def _set_potions(snapshot: dict, count: int, group_type_id: str) -> dict:
    document = copy.deepcopy(snapshot)
    for group in document.get("groups") or []:
        if group.get("type_id") == group_type_id:
            group["members"] = list(group["members"])[:count]
    return document


def _entities_of(snapshot: dict, kind_id: str) -> list:
    return [entity for entity in snapshot.get("entities") or [] if entity.get("kind_id") == kind_id]


def _sequence(context: "_Context") -> tuple:
    """构造一段"公开快照序列"（只改公开字段）：敌人掉血、玩家掉血、回合推进、药水消耗、终局胜利。

    返回 ``(snapshots, expected)``；``expected`` 由**测试自己**从夹具数出来
    （实体个数 × 设定值），不复用被测模块的派生逻辑。
    """
    base = context.snapshot()
    monsters = _entities_of(base, FIXTURE_MONSTER_KIND)
    players = _entities_of(base, FIXTURE_PLAYER_KIND)
    if not monsters or not players:
        raise AssertionError("combat 夹具里必须有 enemy 与 player 实体")
    references = [entity["instance_id"] for entity in base["entities"]][:2]

    first = _edit_field(_edit_field(base, FIXTURE_MONSTER_KIND, FIXTURE_MONSTER_HP, "30"),
                        FIXTURE_PLAYER_KIND, FIXTURE_PLAYER_HP, "80")
    first = _edit_field(first, FIXTURE_PLAYER_KIND, FIXTURE_PLAYER_TURN, "1")
    first = _with_potion_belt(first, references, FIXTURE_BELT_GROUP)

    middle = _edit_field(_edit_field(first, FIXTURE_MONSTER_KIND, FIXTURE_MONSTER_HP, "18"),
                         FIXTURE_PLAYER_KIND, FIXTURE_PLAYER_HP, "74")
    middle = _edit_field(middle, FIXTURE_PLAYER_KIND, FIXTURE_PLAYER_TURN, "2")
    middle = _set_potions(middle, 1, FIXTURE_BELT_GROUP)

    last = _edit_field(_edit_field(middle, FIXTURE_MONSTER_KIND, FIXTURE_MONSTER_HP, "0"),
                       FIXTURE_PLAYER_KIND, FIXTURE_PLAYER_HP, "71")
    last = _edit_field(last, FIXTURE_PLAYER_KIND, FIXTURE_PLAYER_TURN, "3")
    last = _set_outcome(last, "win", "fixture:victory")

    expected = {
        ENEMY_HP_DELTA: 30.0 * len(monsters) - 0.0,
        PLAYER_HP_DELTA: 80.0 - 71.0,
        TURN_COUNT: 3.0 - 1.0,
        POTION_SPENT: 2.0 - 1.0,
        DECISION_COUNT: 3.0,
        OUTCOME_WIN: 1.0,
        OUTCOME_LOSS: 0.0,
    }
    return [first, middle, last], expected


class _Context:
    def __init__(self) -> None:
        self._snapshot = None

    def snapshot(self) -> dict:
        if self._snapshot is None:
            self._snapshot = tensor_suite.snapshot(BUNDLE)
        return self._snapshot


# --------------------------------------------------------------------------- #
# reward-profile
# --------------------------------------------------------------------------- #
def _profile_cases(context: _Context) -> list:
    def standard_profile_is_explicit() -> tuple:
        profile = _fixture_profile({ENEMY_HP_DELTA: 1.0})
        units = profile.units()
        return (set(profile.component_ids) == set(units)
                and all(unit in STANDARD_UNITS for unit in units.values())
                and all(profile.weights[key] == 0.0 for key in profile.component_ids
                        if key != ENEMY_HP_DELTA),
                f"components={len(profile.component_ids)}")

    def profile_rejects_missing_weight() -> tuple:
        document = _fixture_profile({}).to_wire()
        document["weights"].pop(ENEMY_HP_DELTA)
        try:
            RewardProfile.from_wire(document)
        except RewardProfileError as failure:
            return "缺少" in str(failure), f"{failure}"
        return False, "权重漏掉分量必须被拒绝"

    def profile_rejects_unknown_weight_and_component() -> tuple:
        problems = []
        try:
            _fixture_profile({"net:reward:no-such-component": 1.0})
        except RewardProfileError as failure:
            if "未声明" not in str(failure):
                problems.append(f"未知权重的错误信息不点名：{failure}")
        else:
            problems.append("未知权重必须被拒绝")
        try:
            RewardProfile(
                profile_id="x", semantic_version="1",
                components=(RewardComponent("a", "hp", "来源"), RewardComponent("a", "hp", "来源")),
                weights={"a": 1.0},
            )
        except RewardProfileError as failure:
            if "重复" not in str(failure):
                problems.append(f"重复分量 id 的错误信息不点名：{failure}")
        else:
            problems.append("重复分量 id 必须被拒绝")
        try:
            RewardComponent("c", "parsecs", "来源")
        except RewardProfileError as failure:
            if "单位" not in str(failure):
                problems.append(f"非法单位的错误信息不点名：{failure}")
        else:
            problems.append("非法单位必须被拒绝")
        try:
            RewardComponent("c", "hp", "   ")
        except RewardProfileError as failure:
            if "source" not in str(failure) and "来源" not in str(failure):
                problems.append(f"空来源的错误信息不点名：{failure}")
        else:
            problems.append("空来源必须被拒绝")
        return (not problems, "; ".join(problems))

    def profile_roundtrip_and_digest() -> tuple:
        profile = _fixture_profile({ENEMY_HP_DELTA: 0.5, TURN_COUNT: -0.1}, discount=0.99)
        restored = RewardProfile.from_wire(profile.to_wire())
        document = profile.to_wire()
        document["reward_profile_version"] = "reward-profile@999"
        version_rejected = False
        try:
            RewardProfile.from_wire(document)
        except RewardProfileError:
            version_rejected = True
        return (restored == profile and restored.digest() == profile.digest() and version_rejected,
                f"digest={profile.digest()[:20]} version_rejected={version_rejected}")

    def weights_change_the_identity() -> tuple:
        left = _fixture_profile({ENEMY_HP_DELTA: 1.0})
        right = _fixture_profile({ENEMY_HP_DELTA: 2.0})
        return (left.digest() != right.digest(), "不同权重必须是不同 Profile 身份")

    def facts_change_the_identity() -> tuple:
        """换 Gym 的公开路径 = 新的 Profile（否则历史数据会被当成同一实验）。"""
        fixture = _fixture_profile({ENEMY_HP_DELTA: 1.0})
        slice_based = standard_short_task_profile({ENEMY_HP_DELTA: 1.0})
        return (fixture.digest() != slice_based.digest(),
                "facts 不同必须改变 Profile 摘要")

    def invalid_scalars_rejected() -> tuple:
        problems = []
        for overrides, marker in (({"discount": 0.0}, "discount"), ({"discount": 1.5}, "discount")):
            try:
                _fixture_profile({}, **overrides)
            except RewardProfileError as failure:
                if marker not in str(failure):
                    problems.append(f"{overrides}: {failure}")
            else:
                problems.append(f"{overrides} 必须被拒绝")
        try:
            RewardComponent("c", "hp", "来源", scale=0.0)
        except RewardProfileError:
            pass
        else:
            problems.append("scale=0 必须被拒绝（它会让分量静默消失）")
        return (not problems, "; ".join(problems))

    def profile_id_is_recorded_in_model_bundle() -> tuple:
        """奖励 Profile 身份必须能随模型包声明（未冻结时留 null，不自造 id）。"""
        import json
        import tempfile

        from ai_the_spire.models import ModelConfig, SmallPolicyModel, save_bundle

        manifest = tensor_suite.manifest_for(BUNDLE)
        model = SmallPolicyModel(ModelConfig(), manifest)
        profile = _fixture_profile({ENEMY_HP_DELTA: 1.0})
        with tempfile.TemporaryDirectory(prefix="reward-bundle-") as directory:
            save_bundle(model, directory, reward_profile_id=profile.profile_id,
                        training_profile_id=None, executor_profile_id=None)
            metadata = json.loads(
                (pathlib.Path(directory) / "metadata.json").read_text(encoding="utf-8")
            )
        return (metadata["reward_profile_id"] == profile.profile_id
                and metadata["training_profile_id"] is None,
                f"reward_profile_id={metadata['reward_profile_id']}")

    return [
        _guard(SUITE_PROFILE, "profile/standard-components-explicit", standard_profile_is_explicit),
        _guard(SUITE_PROFILE, "profile/weights-must-cover-components", profile_rejects_missing_weight),
        _guard(SUITE_PROFILE, "profile/rejects-unknown-and-invalid",
               profile_rejects_unknown_weight_and_component),
        _guard(SUITE_PROFILE, "profile/roundtrip-and-digest", profile_roundtrip_and_digest),
        _guard(SUITE_PROFILE, "profile/weights-change-identity", weights_change_the_identity),
        _guard(SUITE_PROFILE, "profile/facts-change-identity", facts_change_the_identity),
        _guard(SUITE_PROFILE, "profile/invalid-scalars-rejected", invalid_scalars_rejected),
        _guard(SUITE_PROFILE, "profile/id-recorded-in-model-bundle",
               profile_id_is_recorded_in_model_bundle),
    ]


# --------------------------------------------------------------------------- #
# reward-derive
# --------------------------------------------------------------------------- #
def _derive_cases(context: _Context) -> list:
    def vector_matches_public_facts() -> tuple:
        profile = _fixture_profile({
            ENEMY_HP_DELTA: 1.0, PLAYER_HP_DELTA: -1.0, TURN_COUNT: -0.5,
            DECISION_COUNT: -0.05, POTION_SPENT: -0.2, OUTCOME_WIN: 2.0, OUTCOME_LOSS: -2.0,
        })
        snapshots, facts = _sequence(context)
        vector = derive_episode_reward(snapshots, profile, steps=3)
        if vector.values != facts:
            return False, f"派生值不符：{vector.values} != {facts}"
        scalar = sum(profile.weights[key] * value for key, value in facts.items())
        return (abs(vector.scalar - scalar) < 1e-12 and vector.unavailable == (),
                f"scalar={vector.scalar} 期望={scalar}")

    def units_are_preserved() -> tuple:
        profile = _fixture_profile({ENEMY_HP_DELTA: 1.0})
        snapshots, _ = _sequence(context)
        vector = derive_episode_reward(snapshots, profile, steps=3)
        expected = profile.units()
        return (vector.units == {key: expected[key] for key in vector.values}
                and set(vector.values) == set(expected) - set(vector.unavailable),
                f"units={vector.units}")

    def decoy_hidden_fields_do_not_change_result() -> tuple:
        """公开性探针：往快照里塞"隐藏状态"诱饵，派生结果必须逐字不变。"""
        profile = _fixture_profile({ENEMY_HP_DELTA: 1.0, PLAYER_HP_DELTA: -1.0})
        snapshots, _ = _sequence(context)
        baseline = derive_episode_reward(snapshots, profile, steps=3)
        salted = []
        for snapshot in snapshots:
            document = copy.deepcopy(snapshot)
            document["hidden_state"] = {"draw_pile_order": ["secret"], "rng_cursor": 12345}
            for entity in document.get("entities") or []:
                entity["internal_id"] = 999
            salted.append(document)
        after = derive_episode_reward(salted, profile, steps=3)
        return (baseline.to_wire() == after.to_wire(), f"{baseline.scalar} vs {after.scalar}")

    def weights_are_linear() -> tuple:
        base = _fixture_profile({ENEMY_HP_DELTA: 1.0})
        doubled = _fixture_profile({ENEMY_HP_DELTA: 2.0})
        snapshots, _ = _sequence(context)
        left = derive_episode_reward(snapshots, base, steps=3)
        right = derive_episode_reward(snapshots, doubled, steps=3)
        return (abs(right.scalar - 2 * left.scalar) < 1e-12 and right.values == left.values,
                f"{left.scalar} vs {right.scalar}")

    def zero_weight_component_stays_recorded() -> tuple:
        """权重为 0 的分量仍出现在向量与贡献表里（"这次记录了什么"必须可核对）。"""
        profile = _fixture_profile({ENEMY_HP_DELTA: 1.0})
        snapshots, facts = _sequence(context)
        vector = derive_episode_reward(snapshots, profile, steps=3)
        return (TURN_COUNT in vector.values
                and vector.contributions[TURN_COUNT] == facts[TURN_COUNT]
                and vector.scalar == vector.contributions[ENEMY_HP_DELTA],
                f"values={sorted(vector.values)} contributions={sorted(vector.contributions)}")

    def derivation_is_deterministic() -> tuple:
        profile = _fixture_profile({ENEMY_HP_DELTA: 1.0})
        snapshots, _ = _sequence(context)
        left = derive_episode_reward(snapshots, profile, steps=3)
        right = derive_episode_reward(snapshots, profile, steps=3)
        return (left.digest() == right.digest(), f"{left.digest()[:20]} vs {right.digest()[:20]}")

    return [
        _guard(SUITE_DERIVE, "derive/vector-matches-public-facts", vector_matches_public_facts),
        _guard(SUITE_DERIVE, "derive/units-preserved", units_are_preserved),
        _guard(SUITE_DERIVE, "derive/decoy-hidden-fields-ignored",
               decoy_hidden_fields_do_not_change_result),
        _guard(SUITE_DERIVE, "derive/weights-are-linear", weights_are_linear),
        _guard(SUITE_DERIVE, "derive/zero-weight-still-recorded", zero_weight_component_stays_recorded),
        _guard(SUITE_DERIVE, "derive/deterministic-digest", derivation_is_deterministic),
    ]


# --------------------------------------------------------------------------- #
# reward-missing
# --------------------------------------------------------------------------- #
def _missing_cases(context: _Context) -> list:
    def required_component_without_facts_fails() -> tuple:
        snapshots, _ = _sequence(context)
        truncated = [copy.deepcopy(snapshot) for snapshot in snapshots]
        truncated[-1].pop("terminal_result", None)
        document = _fixture_profile({OUTCOME_WIN: 1.0}).to_wire()
        for component in document["components"]:
            if component["component_id"] == OUTCOME_WIN:
                component["optional"] = False
        required = RewardProfile.from_wire(document)
        try:
            derive_episode_reward(truncated, required, steps=3)
        except RewardError as failure:
            return (OUTCOME_WIN in str(failure) and "0" in str(failure), f"{failure}")
        return False, "缺必需公开事实必须显式失败（不得用 0 冒充）"

    def optional_component_is_recorded_unavailable() -> tuple:
        snapshots, _ = _sequence(context)
        truncated = [copy.deepcopy(snapshot) for snapshot in snapshots]
        truncated[-1].pop("terminal_result", None)
        profile = _fixture_profile({ENEMY_HP_DELTA: 1.0, OUTCOME_WIN: 5.0})
        vector = derive_episode_reward(truncated, profile, steps=3)
        # 没有终局结果 ⇒ "是否胜利"与"是否失败"两个分量都不可用（都不参与标量化）
        return (set(vector.unavailable) == {OUTCOME_WIN, OUTCOME_LOSS}
                and OUTCOME_WIN not in vector.values
                and abs(vector.scalar - vector.values[ENEMY_HP_DELTA]) < 1e-12,
                f"unavailable={vector.unavailable} scalar={vector.scalar}")

    def absent_potion_belt_is_unavailable_not_zero() -> tuple:
        """没有药水栏时药水分量必须记 unavailable，而不是被读成"没用药水"（0）。"""
        snapshots, _ = _sequence(context)
        without_belt = []
        for snapshot in snapshots:
            document = copy.deepcopy(snapshot)
            document["groups"] = [group for group in document.get("groups") or []
                                  if group.get("type_id") != FIXTURE_BELT_GROUP]
            without_belt.append(document)
        profile = _fixture_profile({POTION_SPENT: -1.0, ENEMY_HP_DELTA: 1.0})
        vector = derive_episode_reward(without_belt, profile, steps=3)
        return (vector.unavailable == (POTION_SPENT,)
                and POTION_SPENT not in vector.values
                and abs(vector.scalar - vector.values[ENEMY_HP_DELTA]) < 1e-12,
                f"unavailable={vector.unavailable} values={sorted(vector.values)}")

    def unknown_component_has_no_rule() -> tuple:
        profile = RewardProfile(
            profile_id="net:reward:probe", semantic_version="1",
            components=(RewardComponent("net:reward:mystery", "events", "外部黑盒"),),
            weights={"net:reward:mystery": 1.0},
            facts=FIXTURE_FACT_PATHS,
        )
        snapshots, _ = _sequence(context)
        try:
            derive_episode_reward(snapshots, profile, steps=3)
        except RewardError as failure:
            return ("没有派生规则" in str(failure), f"{failure}")
        return False, "没有派生规则的分量必须被拒绝，不得静默给 0"

    def empty_snapshot_sequence_is_rejected() -> tuple:
        profile = _fixture_profile({ENEMY_HP_DELTA: 1.0})
        try:
            derive_episode_reward([], profile)
        except RewardError as failure:
            return ("至少一份" in str(failure), f"{failure}")
        return False, "空快照序列必须被拒绝"

    def slice_paths_missing_from_fixture_are_explicit() -> tuple:
        """用**切片**路径去读夹具快照 ⇒ 必需分量缺事实，必须显式失败（而不是静默 0）。"""
        snapshots, _ = _sequence(context)
        slice_profile = standard_short_task_profile({ENEMY_HP_DELTA: 1.0})
        try:
            derive_episode_reward(snapshots, slice_profile, steps=3)
        except RewardError as failure:
            return ("必需的" in str(failure) and ENEMY_HP_DELTA in str(failure), f"{failure}")
        return False, "路径与快照不匹配时必须显式失败"

    return [
        _guard(SUITE_MISSING, "missing/required-fact-fails", required_component_without_facts_fails),
        _guard(SUITE_MISSING, "missing/optional-recorded-unavailable",
               optional_component_is_recorded_unavailable),
        _guard(SUITE_MISSING, "missing/absent-potion-belt-unavailable",
               absent_potion_belt_is_unavailable_not_zero),
        _guard(SUITE_MISSING, "missing/unknown-component-has-no-rule", unknown_component_has_no_rule),
        _guard(SUITE_MISSING, "missing/empty-sequence-rejected", empty_snapshot_sequence_is_rejected),
        _guard(SUITE_MISSING, "missing/paths-mismatch-fails",
               slice_paths_missing_from_fixture_are_explicit),
    ]


def run_all(only: Optional[str] = None) -> list:
    outcomes: list = []
    if only is not None and only not in SUITE_NAMES:
        return outcomes
    context = _Context()
    if only in (None, SUITE_PROFILE):
        outcomes.extend(_profile_cases(context))
    if only in (None, SUITE_DERIVE):
        outcomes.extend(_derive_cases(context))
    if only in (None, SUITE_MISSING):
        outcomes.extend(_missing_cases(context))
    return outcomes


__all__ = [
    "SUITE_DERIVE",
    "SUITE_MISSING",
    "SUITE_NAMES",
    "SUITE_PROFILE",
    "run_all",
]
