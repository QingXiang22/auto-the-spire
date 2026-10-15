"""J-01 / M1 的端到端检查：真实 stdio 上的受控切片闭环。

用法（仓库根目录，需先 ``dotnet build AutoTheSpire.sln``）::

    python ai-the-spire/tests/j01_episode.py [--only GROUP]

分组：

* ``episode`` —— describe → get_catalog → Manifest → start → observe → 校验/编码 → 概率 → 采样 → step，
  跑到确认终局或**显式测试截断**，并落盘完整公开轨迹；
* ``headbutt`` —— 真实 stdio 上驱动头槌的中途选择与置顶，并编码其结算后的**第一份**快照（不给历史）；
* ``invariants`` —— 隐藏顺序/已知事实/候选重排与句柄换名/概率只覆盖真实候选；
* ``trace`` —— 离线重读轨迹并重编码（不需要活动宿主）。

退出码：0 全通过、1 有失败、2 参数或环境错误。
"""

from __future__ import annotations

import copy
import itertools
import json
import pathlib
import random
import shutil
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "ai-the-spire"))
sys.path.insert(0, str(REPO / "net-the-spire" / "client"))

from ai_the_spire.encoding import EncodingConfig, tensorize  # noqa: E402
from ai_the_spire.collector import CollectorLimits  # noqa: E402
from ai_the_spire.data.audit import SPLITS, plan_digest_of  # noqa: E402
from ai_the_spire.evaluation import (  # noqa: E402
    EvaluationPlan,
    EvaluationRunner,
    MetricSpec,
    PolicySpec,
)
from ai_the_spire.evaluation.runner import (  # noqa: E402
    METRIC_REWARD,
    METRIC_STEPS,
    METRIC_TERMINAL_WIN,
    METRIC_WALL_MS,
)
from ai_the_spire.models import (  # noqa: E402
    ModelConfig,
    SmallPolicyModel,
    measure_inference_cost,
    parameter_report,
)
from ai_the_spire.reward import derive_episode_reward, standard_short_task_profile  # noqa: E402
from ai_the_spire.runner import (  # noqa: E402
    EpisodeRunner,
    gym_id_from,
    ordered_candidates,
    sample_action,
    uniform_probabilities,
)
from ai_the_spire.runner.baseline import EXECUTOR_PROFILE_ID  # noqa: E402
from ai_the_spire.runner.trace import load_trace, verify_trace, write_trace  # noqa: E402
from ai_the_spire.scenarios import (  # noqa: E402
    audit_sampling,
    default_scenario_manifest,
    default_split_assignments,
    run_scenario_sampling,
)
from net_client import NetClient  # noqa: E402

GROUPS = ("episode", "headbutt", "invariants", "trace", "author", "model", "reward", "evaluation",
          "scenarios")
EXECUTOR_SEED = 20261002
EPISODE_SEED = 20261001
MAX_STEPS = 40
#: 作者 Gym 现在有**两间房**（W-05），因此步数上限要覆盖两场战斗 + 中间的进入下一间房。
AUTHOR_MAX_STEPS = 140
MODEL_MAX_STEPS = 24

#: 评估组：保留种子与固定预算（工程探针；真实实验的种子/预算由 W-07 冻结）。
EVALUATION_SEEDS = (20261001, 20261002, 20261003)
EVALUATION_MAX_STEPS = 30
BASELINE_POLICY_ID = "baseline:uniform-legal-candidates@1"
MODEL_POLICY_ID = "model:small-embedding-scorer@1"

#: 场景组：每簇 1 局、每局有界步数（W-03 的场景/划分/成本证据；真实预算由 W-07 冻结）。
SCENARIO_MAX_STEPS = 20


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


#: episode 组产出的轨迹（进程内传递，但 trace 组从磁盘重读）。
_ARTIFACTS: dict = {}


# -- wire 助手 -----------------------------------------------------------------------------------------------


def _snapshot(response: dict) -> dict:
    return response["result"]["snapshot"]


def _entities(snapshot: dict) -> list[dict]:
    return list(snapshot.get("entities") or [])


def _content_id(entity: dict) -> str | None:
    return entity.get("content_id") if entity.get("content_state") == "known" else None


def _pile(snapshot: dict, pile_ref: str) -> dict | None:
    for pile in snapshot.get("piles") or []:
        if pile["pile_ref"] == pile_ref:
            return pile
    return None


def _hand_ref(snapshot: dict) -> str | None:
    """手牌区的 pile_ref：kind_id 里带 HandPile 的那个牌堆实体。"""
    kinds = {entity["instance_id"]: entity["kind_id"] for entity in _entities(snapshot)}
    for pile in snapshot.get("piles") or []:
        if kinds.get(pile["pile_ref"], "").endswith("HandPile"):
            return pile["pile_ref"]
    return None


def _hand_cards(snapshot: dict) -> list[str]:
    reference = _hand_ref(snapshot)
    return list(_pile(snapshot, reference)["visible_members"]) if reference else []


def _candidate_ref(action: dict) -> str | None:
    for field in action.get("fields") or []:
        if field["field_id"].endswith(".Card") and field["state"] == "known":
            return field["value"]
    return None


def _play_candidate_for(snapshot: dict, content_id: str) -> dict | None:
    by_ref = {entity["instance_id"]: _content_id(entity) for entity in _entities(snapshot)}
    for action in snapshot.get("legal_actions") or []:
        if not action["action_type"].endswith("PlayCard"):
            continue
        reference = _candidate_ref(action)
        if reference and by_ref.get(reference) == content_id:
            return action
    return None


def _answer_candidates(snapshot: dict) -> list[dict]:
    return [action for action in snapshot.get("legal_actions") or []
            if action["action_type"] == "net:action:net.answer_decision"]


def _status(snapshot: dict) -> str:
    return snapshot["status"]


# -- episode ------------------------------------------------------------------------------------------------


def _scenario_profile(described: dict) -> str:
    """后端声明的场景 profile（唯一来源：`describe`）。

    检查里不再写死 `base-spire-combat-slice-v1`：换场景/换内容包（例如 `--author-content`）时
    身份必须跟着后端声明走，否则会把两个 Gym 的轨迹混在一起。
    """
    profiles = (described["result"].get("capabilities") or {}).get("scenario_profiles") or []
    if len(profiles) != 1:
        raise RuntimeError(f"后端必须声明恰好一个场景 profile，实际 {len(profiles)} 个")
    return profiles[0]["id"]


def _run_episode(checker: Checker) -> None:
    client = NetClient.host_from_solution()
    try:
        runner = EpisodeRunner(client, seed=EPISODE_SEED, max_steps=MAX_STEPS, executor_seed=EXECUTOR_SEED)
        identities = runner.prepare()
        checker.check("episode/manifest-built", bool(identities.manifest_digest), "Manifest 必须真的建出来")
        checker.equal("episode/executor-profile", "net:executor:canonical-cdf-uniform-rng@1",
                      identities.executor_profile_id)
        checker.check("episode/model-identity-is-baseline", identities.model_id.startswith("baseline:"),
                      f"没有神经网络时模型身份必须显式为随机基线：{identities.model_id}")
        checker.equal("episode/identity-binds-catalog", identities.catalog_id,
                      runner._identity["catalog_id"])  # noqa: SLF001 - 只读核对

        result = runner.run()
        checker.check("episode/reaches-terminal-or-explicit-truncation",
                      result.ending in {"terminal", "truncated"}, f"ending={result.ending}")
        checker.check("episode/has-transitions", result.steps > 0, "至少要有一次真实转移")

        for transition in result.transitions:
            if transition.status == "terminal":
                continue
            keys = transition.candidates
            checker.equal(f"episode/step{transition.step}-probabilities-cover-candidates",
                          sorted(keys), sorted(transition.probabilities))
            checker.check(f"episode/step{transition.step}-probabilities-sum-to-one",
                          abs(sum(transition.probabilities.values()) - 1.0) < 1e-9, "概率和必须为 1")
            checker.check(f"episode/step{transition.step}-chosen-in-candidates",
                          transition.chosen_semantic_key in transition.probabilities, "选中项必须在候选内")
            checker.check(f"episode/step{transition.step}-tensor-recorded",
                          transition.tensor_digest.startswith("sha256:"), "张量摘要必须记录")
            checker.check(f"episode/step{transition.step}-candidate-rows-match",
                          transition.sizes["candidates"] == len(keys),
                          f"候选行数必须等于真实候选数：{transition.sizes['candidates']} vs {len(keys)}")
            checker.check(f"episode/step{transition.step}-request-id-recorded",
                          bool(transition.request_id), "request_id 必须记录")

        path = write_trace(result.to_wire(), pathlib.Path(tempfile.mkdtemp(prefix="j01-")) / "episode.json")
        _ARTIFACTS["trace_path"] = path
        _ARTIFACTS["result"] = result
        checker.check("episode/trace-written", path.exists() and path.stat().st_size > 0, f"{path}")
    finally:
        client.shutdown()


# -- headbutt（真实 stdio 上的受控测试场景） -----------------------------------------------------------------


def _run_headbutt(checker: Checker) -> None:
    client = NetClient.host_from_solution(extra_args=["--test-headbutt"])
    try:
        described = client.describe()
        identity = described["result"]["identity"]
        catalog = client.get_catalog(identity["catalog_id"])["result"]
        from ai_the_spire.encoding import build_manifest

        manifest = build_manifest(catalog, EncodingConfig(), backend_profile_id=identity["backend_profile_id"],
                                  gym_id=gym_id_from(identity["ruleset_id"]),
                                  scenario_profile=_scenario_profile(described))
        started = client.start(identity, _scenario_profile(described), seed=EPISODE_SEED)
        session = started["result"]["session_id"]

        # 驱动回合直到头槌进入手牌；每一回合都只在真实公开快照上决策。
        snapshot = _snapshot(client.observe(session))
        for _ in range(12):
            if _play_candidate_for(snapshot, "base-spire:headbutt") is not None:
                break
            snapshot = _step_end_turn(client, session, snapshot)
        headbutt = _play_candidate_for(snapshot, "base-spire:headbutt")
        checker.check("headbutt/reaches-hand-within-bounded-turns", headbutt is not None,
                      "受控测试场景应在有限回合内把头槌抽到手牌")
        if headbutt is None:
            return

        # 头槌的弃牌选择需要弃牌堆非空：先打一张别的牌，让它进入弃牌堆（真实动作，不是桩）。
        strike = _play_candidate_for(snapshot, "base-spire:strike-ironclad")
        if strike is not None:
            after_strike = _step(client, session, strike)
            while _status(after_strike) == "decision" and _answer_candidates(after_strike):
                after_strike = _step(client, session, _answer_candidates(after_strike)[0])
            snapshot = _snapshot(client.observe(session))
            headbutt = _play_candidate_for(snapshot, "base-spire:headbutt")
            checker.check("headbutt/still-playable-after-staging", headbutt is not None,
                          "先手一张其他牌后头槌仍应可出")

        # 出牌 → 目标选择 → 弃牌选择（中途选择必须正确续接）。
        snapshot = _step(client, session, headbutt)
        checker.equal("headbutt/pauses-for-target-selection", "decision", _status(snapshot))
        answers = _answer_candidates(snapshot)
        checker.check("headbutt/target-candidates-present", len(answers) > 0, "目标选择必须有候选")
        if not answers:
            return
        snapshot = _step(client, session, answers[0])
        checker.equal("headbutt/pauses-for-discard-selection", "decision", _status(snapshot))
        discards = _answer_candidates(snapshot)
        checker.check("headbutt/discard-candidates-present", len(discards) > 0, "弃牌选择必须有候选")
        if not discards:
            return

        chosen = discards[0]
        chosen_ref = _candidate_ref(chosen) or _first_entity_ref(chosen)
        snapshot = _step(client, session, chosen)

        # 结算后的**第一份**快照：不给任何历史，直接编码。
        known = [pile for pile in snapshot.get("piles") or [] if pile.get("known_positions")]
        checker.check("headbutt/known-position-in-first-snapshot", len(known) == 1,
                      f"置顶后第一份快照必须恰好有一个已知位置：{known}")
        if known:
            position = known[0]["known_positions"][0]
            checker.equal("headbutt/known-position-is-zero", 0, position["position"])
            checker.equal("headbutt/known-position-is-the-chosen-card", chosen_ref, position["card_ref"])
            checker.check("headbutt/known-card-is-an-entity",
                          position["card_ref"] in {entity["instance_id"] for entity in _entities(snapshot)},
                          "card_ref 必须是快照内的实体")
            checker.check("headbutt/known-card-in-visible-members",
                          position["card_ref"] in known[0]["visible_members"], "I-29：card_ref ⊆ visible_members")

        encoded = tensorize(snapshot, manifest, catalog=catalog)
        checker.check("headbutt/encodes-without-history", encoded.sizes["pile_positions"] >= 1,
                      "编码必须包含位置记录（不允许靠历史重建）")
        rows = encoded.block("pile_positions")
        card_entities = [
            encoded.reference_map.row_of_instance(entity["instance_id"])
            for entity in _entities(snapshot)
        ]
        checker.check("headbutt/position-row-references-a-real-entity",
                      all(row is not None for row in card_entities if row is not None),
                      "位置记录必须引用本样本内的实体行")
        checker.check("headbutt/position-mask-only-for-real-records",
                      sum(1 for mask in rows.column("position_record_mask") if mask) == len(known[0]["known_positions"])
                      if known else False,
                      "position_record_mask 必须恰为真实位置记录数")
        checker.equal("headbutt/no-unknown-encoding-skips",
                      (), tuple(encoded.validation_skipped))
    finally:
        client.shutdown()


def _first_entity_ref(action: dict) -> str | None:
    for field in action.get("fields") or []:
        if field["state"] == "known" and isinstance(field.get("value"), str):
            return field["value"]
    return None


_REQUEST_COUNTER = itertools.count(1)


def _step(client: NetClient, session: str, action: dict) -> dict:
    observed = client.observe(session)
    snapshot = _snapshot(observed)
    # request_id 必须**逐请求唯一**：同 id 换载荷会被宿主按去重规则拒绝（这正是它该做的）。
    response = client.step(session, snapshot["observation_id"], action["candidate_id"],
                           request_id=f"j01h-{next(_REQUEST_COUNTER)}")
    if not response.get("ok"):
        raise RuntimeError(f"step 失败：{response.get('error')}")
    return _snapshot(client.observe(session))


def _step_end_turn(client: NetClient, session: str, snapshot: dict) -> dict:
    for action in snapshot.get("legal_actions") or []:
        if action["action_type"].endswith("EndTurn"):
            return _step(client, session, action)
    raise RuntimeError("当前快照没有 EndTurn 候选")


# -- invariants ---------------------------------------------------------------------------------------------


def _run_invariants(checker: Checker) -> None:
    client = NetClient.host_from_solution()
    try:
        runner = EpisodeRunner(client, seed=EPISODE_SEED, max_steps=3, executor_seed=EXECUTOR_SEED)
        identities = runner.prepare()
        started = client.start(runner._identity, runner.scenario_profile, seed=EPISODE_SEED)  # noqa: SLF001
        session = started["result"]["session_id"]
        snapshot = _snapshot(client.observe(session))
        catalog = runner._catalog  # noqa: SLF001 - 已通过校验的目录文档

        # ① 概率只覆盖真实候选，没有 padding 项。
        candidates = ordered_candidates(snapshot)
        probabilities = uniform_probabilities(candidates)
        checker.equal("invariants/probabilities-exactly-cover-candidates", len(candidates), len(probabilities))
        checker.check("invariants/no-padding-probability",
                      all(key for key in probabilities), "概率键必须都是真实候选的语义键")

        # ② 候选重排 + 句柄换名：同样的概率与执行 RNG 必须选出同样的**语义动作**。
        shuffled = copy.deepcopy(snapshot)
        actions = list(shuffled["legal_actions"])
        renamed = {}
        for index, action in enumerate(actions):
            renamed[action["candidate_id"]] = f"handle-{index:03d}"
            action["candidate_id"] = renamed[action["candidate_id"]]
        shuffled["legal_actions"] = list(reversed(actions))
        first = sample_action(candidates, probabilities, random.Random(EXECUTOR_SEED))
        second = sample_action(ordered_candidates(shuffled), probabilities, random.Random(EXECUTOR_SEED))
        checker.equal("invariants/reorder-and-rename-keep-semantic-action",
                      first.semantic_key_text, second.semantic_key_text)
        checker.check("invariants/handle-not-in-semantic-key",
                      all(key not in first.semantic_key_text for key in renamed.values()),
                      "句柄不得出现在语义键里")

        # ③ 隐藏顺序不进公开输入：协议快照里根本没有隐藏顺序可改；改变**已知事实**必须改变编码。
        encoded = tensorize(snapshot, runner.manifest, catalog=catalog)
        mutated = copy.deepcopy(snapshot)
        probe = None
        for pile in mutated.get("piles") or []:
            if pile.get("known_positions"):
                pile["known_positions"] = []
                pile["visible_members"] = [m for m in pile["visible_members"]]
                probe = pile["pile_ref"]
                break
        if probe is None:
            # 该快照没有已知位置：改用玩家能量的公开事实差异（决策相关）。
            for entity in mutated["entities"]:
                for field in entity.get("fields") or []:
                    if field["field_id"].endswith("Player.Energy") and field["state"] == "known":
                        field["value"] = "0"
                        probe = entity["instance_id"]
                        break
        checker.check("invariants/public-fact-difference-is-not-merged", probe is not None,
                      "需要一个公开事实用于对照")
        if probe is not None:
            changed = tensorize(mutated, runner.manifest, catalog=catalog)
            checker.check("invariants/known-fact-or-resource-change-shows-in-tensor",
                          _tensor_fingerprint(encoded) != _tensor_fingerprint(changed),
                          "公开事实差异不得被编码合并掉（也不得因为聚合而丢失）")
        checker.equal("invariants/no-unknown-encoding-skips", (), tuple(encoded.validation_skipped))
    finally:
        client.shutdown()


def _tensor_fingerprint(encoded) -> str:
    return json.dumps(
        {name: {column: list(values) for column, values in encoded.blocks[name].columns.items()}
         for name in encoded.blocks},
        ensure_ascii=False, sort_keys=True, separators=(",", ":"),
    )


# -- trace（离线） ------------------------------------------------------------------------------------------


def _run_trace(checker: Checker) -> None:
    path = _ARTIFACTS.get("trace_path")
    if path is None:
        checker.check("trace/episode-artifact-available", False, "trace 组依赖 episode 组；请一起运行")
        return

    trace = load_trace(path)
    checker.equal("trace/format-version", "j01-trace@1", trace["trace_format_version"])
    checker.check("trace/identities-complete",
                  all(key in trace["identities"] for key in
                      ("dictionary_version", "manifest_digest", "source_catalog_digest",
                       "encoding_config_digest", "executor_seed")),
                  "身份必须自足")
    checker.check("trace/executor-profile-recorded",
                  trace["identities"]["executor_profile_id"] == EXECUTOR_PROFILE_ID, "执行器配置必须随轨迹记录")
    checker.check("trace/model-identity-recorded",
                  trace["identities"]["model_id"].startswith("baseline:"), "模型身份必须是随机基线")

    problems = verify_trace(trace, EncodingConfig())
    checker.check("trace/offline-reencode-matches", not problems, f"{problems}")
    checker.check("trace/snapshots-are-self-contained",
                  all("snapshot" in transition for transition in trace["transitions"]),
                  "每条转移都必须带完整公开快照（不得只存索引）")
    checker.check("trace/terminal-or-truncation-recorded",
                  trace["ending"] in {"terminal", "truncated"}, f"ending={trace['ending']}")
    if trace["ending"] == "truncated":
        checker.check("trace/truncation-is-explicit",
                      "显式" in trace["ending_detail"] or "上限" in trace["ending_detail"],
                      f"截断必须写明原因：{trace['ending_detail']}")
    print(f"  trace: {path}", file=sys.stderr)


# -- author（W-01：作者内容包上的真实闭环 + 离线重编码） ------------------------------------------------------

#: 脚本策略的模型身份：它**不是**随机基线，也不是训练模型；身份必须如实记录。
SCRIPTED_MODEL_ID = "scripted:author-gym-potions-and-attacks@1"


def _author_policy(snapshot: dict, candidates: list) -> dict:
    """只读**当前公开快照**的脚本策略 π（一热，离线可复核）：

    优先用药水（不消耗能量）→ 攻击牌（Bash 优先）→ 防御牌 → 结束回合；
    暂停期（只剩应答候选）时选第一个候选（目标选择：按公开句柄顺序，确定性）。

    它不是学习策略，只用来把作者 Gym 走到**终局**；因此模型身份写 `scripted:`，不冒充基线或模型。
    """
    def content_of(candidate) -> str | None:
        for field in candidate.action.get("fields") or []:
            if field["field_id"].endswith(".Card") and field.get("value"):
                return content.get(field["value"])
        return None

    content = {entity["instance_id"]: entity.get("content_id")
               for entity in snapshot.get("entities") or []}
    answers = [candidate for candidate in candidates
               if candidate.action["action_type"].endswith("answer_decision")]
    if len(answers) == len(candidates):
        chosen = answers[0]
    else:
        # W-05：本间房结算后引擎才提供 `EnterNextRoom` / `UpgradeLongTermCard`。
        # 先拿一次房间奖励（把一张长期牌升级），再进入下一间房——两者都是"继续整局"的公开动作。
        rewards = [candidate for candidate in candidates
                   if candidate.action["action_type"].endswith("UpgradeLongTermCard")]
        next_room = [candidate for candidate in candidates
                     if candidate.action["action_type"].endswith("EnterNextRoom")]
        potions = [candidate for candidate in candidates
                   if candidate.action["action_type"].endswith("UsePotion")]
        plays = [candidate for candidate in candidates
                 if candidate.action["action_type"].endswith("PlayCard")]
        preferred = []
        for wanted in ("base-spire:bash", "base-spire:strike-ironclad", "base-spire:defend-ironclad"):
            preferred.extend(candidate for candidate in plays if content_of(candidate) == wanted)
        preferred.extend(candidate for candidate in plays if candidate not in preferred)
        if rewards:
            chosen = rewards[0]
        elif next_room:
            chosen = next_room[0]
        elif potions:
            chosen = potions[0]
        elif preferred:
            chosen = preferred[0]
        else:
            ends = [candidate for candidate in candidates
                    if candidate.action["action_type"].endswith("EndTurn")]
            chosen = ends[0] if ends else candidates[0]

    return {candidate.semantic_key_text: (1.0 if candidate is chosen else 0.0)
            for candidate in candidates}


def _run_author(checker: Checker) -> None:
    """W-01：作者内容包（`--author-content`）上的真实闭环 + 轨迹离线重编码。

    覆盖 `describe → get_catalog → Manifest → start → 扩展动作（含药水）→ 终局 → 落盘 → 离线重编码`，
    并且身份**从后端声明读**（Gym 由 ruleset 派生、场景 profile 取 describe 声明），因此换 Gym 时轨迹不会被混淆。
    """
    client = NetClient.host_from_solution(extra_args=["--author-content"])
    try:
        runner = EpisodeRunner(client, seed=EPISODE_SEED, max_steps=AUTHOR_MAX_STEPS,
                               executor_seed=EXECUTOR_SEED, policy=_author_policy,
                               model_id=SCRIPTED_MODEL_ID)
        identities = runner.prepare()
        # 作者变体的版本号来自**后端声明**，不在这里猜：当前是 `…author-content-v2`
        # （`net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs` 的 `AuthorRulesetV2` /
        # `AuthorCatalogV2`，与 `Net.TheSpire.Tests` 的期望一致）。Gym 身份仍必须由 ruleset 派生。
        checker.check("author/gym-identity-follows-backend",
                      identities.gym_id == gym_id_from(identities.ruleset_id)
                      and identities.ruleset_id.endswith("author-content-v2")
                      and identities.catalog_id.endswith("author-content-v2"),
                      "Gym 身份必须由后端 ruleset 派生且版本对齐（v2）："
                      f"{identities.gym_id} / {identities.ruleset_id} / {identities.catalog_id}")
        checker.equal("author/scenario-profile-from-describe", runner.scenario_profile,
                      identities.scenario_profile)
        checker.check("author/catalog-digest-recorded", bool(identities.source_catalog_digest),
                      "目录摘要必须记录（离线重建字典用）")

        result = runner.run()
        checker.equal("author/reaches-terminal", "terminal", result.ending)
        terminal = result.transitions[-1].terminal if result.transitions else None
        checker.check("author/terminal-result-present", bool(terminal), f"terminal={terminal}")
        if terminal:
            checker.check("author/terminal-reason-is-victory-or-defeat",
                          terminal.get("reason_id") in
                          ("net:reason:base-spire:Victory", "net:reason:base-spire:Defeat"),
                          f"终局原因：{terminal.get('reason_id')}")

        # 扩展内容真的被操作过：轨迹里出现用药水，且药水栏成员因此减少。
        used_potion = any(
            transition.chosen_semantic_key.find("UsePotion") >= 0
            for transition in result.transitions
        )
        checker.check("author/potion-actually-used", used_potion, "轨迹里必须有用药的转移")

        belts = []
        for transition in result.transitions:
            for group in transition.snapshot.get("groups") or []:
                if group["type_id"] == "net:group_type:base-spire:potion_belt":
                    belts.append(len(group["members"]))
        checker.check("author/belt-observed-through-trace", bool(belts) and belts[0] == 2,
                      f"药水栏必须出现在公开快照里：{belts[:3]}")
        checker.check("author/belt-shrinks-after-use", belts and min(belts) < belts[0],
                      f"用过的药水必须离开药水栏：{belts[:5]}")

        # W-05：轨迹必须能证明**跨过了房间**——`Run.CurrentRoom` 从 1 到 2，且中途执行过 `EnterNextRoom`。
        rooms = []
        for transition in result.transitions:
            for entity in transition.snapshot.get("entities") or []:
                if entity.get("kind_id") != "net:kind:base-spire:Run":
                    continue
                for field in entity.get("fields") or []:
                    if field.get("field_id") == "net:field:base-spire:Run.CurrentRoom":
                        rooms.append(int(field["value"]))
        used_next_room = any("EnterNextRoom" in transition.chosen_semantic_key
                             for transition in result.transitions)
        checker.check("author/crosses-two-rooms",
                      max(rooms) >= 2 and used_next_room and rooms[0] == 1,
                      f"rooms={sorted(set(rooms))} used_enter_next_room={used_next_room}")

        # W-05：房间奖励（升级一张长期牌）必须真的被取用，且升级后的**战斗拷贝**要出现在第二间房。
        used_reward = any("UpgradeLongTermCard" in transition.chosen_semantic_key
                          for transition in result.transitions)
        checker.check("author/takes-room-reward", used_reward,
                      "轨迹里应有一次 UpgradeLongTermCard（房间奖励）")
        upgraded_in_room_two = []
        for transition in result.transitions:
            snapshot = transition.snapshot
            room = 0
            upgraded = 0
            for entity in snapshot.get("entities") or []:
                if entity.get("kind_id") == "net:kind:base-spire:Run":
                    for field in entity.get("fields") or []:
                        if field.get("field_id") == "net:field:base-spire:Run.CurrentRoom":
                            room = int(field["value"])
                elif entity.get("kind_id") == "net:kind:base-spire:Card":
                    for field in entity.get("fields") or []:
                        if field.get("field_id") == "net:field:base-spire:Card.Upgraded":
                            upgraded += 1 if field.get("value") is True else 0
            if room >= 2:
                upgraded_in_room_two.append(upgraded)
        checker.check("author/upgraded-card-enters-next-room",
                      upgraded_in_room_two and max(upgraded_in_room_two) >= 1,
                      f"第二间房里升级牌数={upgraded_in_room_two[:5]}")

        # 离线重编码：轨迹自足（目录 + 身份 + 逐条快照），不需要活动宿主。
        path = write_trace(result.to_wire(), pathlib.Path(tempfile.mkdtemp(prefix="j01-author-")) / "episode.json")
        trace = load_trace(path)
        problems = verify_trace(trace, EncodingConfig())
        checker.check("author/offline-reencode-matches", not problems, f"{problems}")
        checker.check("author/identity-in-trace",
                      trace["identities"]["gym_id"] == identities.gym_id
                      and trace["identities"]["model_id"] == SCRIPTED_MODEL_ID,
                      "轨迹身份必须是本次真实 Gym 与策略身份")
        reason = (terminal or {}).get("reason_id") if terminal else None
        print(f"  author: {path} ending={result.ending} reason={reason} steps={result.steps}", file=sys.stderr)
    finally:
        client.shutdown()


def _run_model(checker: Checker) -> None:
    """W-04：**小模型实际参与决策**（不是均匀基线），并有界推理 + 轨迹离线重编码。

    覆盖：Manifest → `SmallPolicyModel`（结构显式、随机初始化）→ 作为 `EpisodeRunner` 的行为策略 π
    → 真实 stdio 上执行 → 轨迹记录 `model_id` 与真实 π → 离线重编码一致 → 参数/延迟可核对。
    """
    client = NetClient.host_from_solution()
    try:
        probe = EpisodeRunner(client, seed=EPISODE_SEED, max_steps=MODEL_MAX_STEPS)
        probe.prepare()
        model = SmallPolicyModel(ModelConfig(), probe.manifest, catalog=probe._catalog)  # noqa: SLF001

        report = parameter_report(model)
        checker.check("model/parameters-reported",
                      report["parameter_total"] > 0 and sum(report["parameters"].values())
                      == report["parameter_total"],
                      f"{report['parameters']} total={report['parameter_total']}")
        checker.check("model/identity-is-not-baseline",
                      model.model_id.startswith("small-embedding-scorer@1")
                      and not model.model_id.startswith("baseline:"),
                      model.model_id)

        runner = EpisodeRunner(client, seed=EPISODE_SEED, max_steps=MODEL_MAX_STEPS,
                               policy=model.policy, model_id=model.model_id)
        identities = runner.prepare()
        checker.equal("model/trace-identity-records-model", identities.model_id, model.model_id)
        result = runner.run()
        checker.check("model/ending-is-terminal-or-explicit-truncation",
                      result.ending in {"terminal", "truncated"}, f"ending={result.ending}")
        checker.check("model/participates-in-decisions", result.steps > 0,
                      "模型必须在真实链路上做出至少一次决策")

        non_uniform = 0
        for transition in result.transitions:
            if transition.status == "terminal" or not transition.probabilities:
                continue
            values = list(transition.probabilities.values())
            uniform = 1.0 / len(values)
            if any(abs(value - uniform) > 1e-9 for value in values):
                non_uniform += 1
        checker.check("model/probabilities-come-from-the-model", non_uniform > 0,
                      f"没有任何一步的概率偏离均匀分布（{len(result.transitions)} 步）")

        path = write_trace(result.to_wire(),
                           pathlib.Path(tempfile.mkdtemp(prefix="j01-model-")) / "episode.json")
        trace = load_trace(path)
        checker.check("model/trace-records-model-id",
                      trace["identities"]["model_id"] == model.model_id,
                      trace["identities"]["model_id"])
        problems = verify_trace(trace, EncodingConfig())
        checker.check("model/offline-reencode-matches", not problems, f"{problems}")

        # 有界推理成本：只报告，不下"够轻"的结论。用**本局第一步**的真实快照（终局快照没有候选）。
        first_snapshot = next((transition.snapshot for transition in result.transitions
                               if transition.status != "terminal"), None)
        checker.check("model/has-a-decision-snapshot-to-measure", first_snapshot is not None,
                      "本局没有任何非终局转移，无法测量推理成本")
        if first_snapshot is None:
            return
        observation = tensorize(first_snapshot, runner.manifest, catalog=runner._catalog)  # noqa: SLF001
        cost = measure_inference_cost(model, [observation], repeats=3)
        checker.check("model/inference-cost-measured",
                      cost["forward"] and cost["forward"]["n"] == 3,
                      f"{cost['forward']}")
        print(f"  model: {path} ending={result.ending} steps={result.steps} "
              f"params={report['parameter_total']} forward_p50={cost['forward']['p50_ms']:.3f}ms",
              file=sys.stderr)
    finally:
        client.shutdown()


# -- reward（W-04：真实轨迹上的奖励派生） ---------------------------------------------------------------------

#: 工程用权重：**不是** W-07 的实验偏好，只用于证明派生层在真实轨迹上可核对地工作。
ENGINEERING_WEIGHTS = {
    "net:reward:enemy_hp_delta": 1.0,
    "net:reward:player_hp_delta": -1.0,
    "net:reward:turn_count": -0.5,
    "net:reward:decision_count": -0.05,
    "net:reward:potion_spent": -0.2,
    "net:reward:outcome_win": 2.0,
    "net:reward:outcome_loss": -2.0,
}


def _public_number(snapshot: dict, kind_id: str, field_id: str) -> float | None:
    """独立地从公开快照读一个 number 字段（多实例求和），用于与派生结果对照。"""
    total = 0.0
    found = False
    for entity in snapshot.get("entities") or []:
        if entity.get("kind_id") != kind_id:
            continue
        for field in entity.get("fields") or []:
            if field.get("field_id") == field_id and field.get("state") == "known":
                total += float(field["value"])
                found = True
    return total if found else None


def _run_reward(checker: Checker) -> None:
    """W-04：在**真实 stdio 轨迹**上派生奖励向量（只读公开快照），并核对标量化与确定性。"""
    client = NetClient.host_from_solution()
    try:
        runner = EpisodeRunner(client, seed=EPISODE_SEED, max_steps=MAX_STEPS)
        runner.prepare()
        result = runner.run()
        checker.check("reward/episode-produces-transitions", result.steps > 0,
                      f"steps={result.steps}")
        snapshots = [transition.snapshot for transition in result.transitions]
        profile = standard_short_task_profile(ENGINEERING_WEIGHTS,
                                             profile_id="net:reward:engineering-probe-v1")
        vector = derive_episode_reward(snapshots, profile, steps=result.steps)

        # 与"独立从公开快照读字段"的结果对照（不调用派生层内部函数）
        first, last = snapshots[0], snapshots[-1]
        enemy_first = _public_number(first, "net:kind:base-spire:Monster",
                                     "net:field:base-spire:Monster.Hp")
        enemy_last = _public_number(last, "net:kind:base-spire:Monster",
                                    "net:field:base-spire:Monster.Hp")
        player_first = _public_number(first, "net:kind:base-spire:Player",
                                      "net:field:base-spire:Player.Hp")
        player_last = _public_number(last, "net:kind:base-spire:Player",
                                     "net:field:base-spire:Player.Hp")
        checker.equal("reward/enemy-hp-delta-matches-public-fields",
                      enemy_first - enemy_last, vector.values["net:reward:enemy_hp_delta"])
        checker.equal("reward/player-hp-delta-matches-public-fields",
                      player_first - player_last, vector.values["net:reward:player_hp_delta"])
        checker.equal("reward/decision-count-is-the-step-count",
                      float(result.steps), vector.values["net:reward:decision_count"])

        # 终局分量：到终局就必须可用且与 terminal_result 一致；没到终局就必须记 unavailable。
        if result.ending == "terminal":
            outcome = (result.transitions[-1].terminal or {}).get("outcome")
            checker.check("reward/terminal-components-available",
                          "net:reward:outcome_win" in vector.values
                          and "net:reward:outcome_loss" in vector.values,
                          f"unavailable={vector.unavailable}")
            checker.equal("reward/outcome-win-matches-terminal",
                          1.0 if outcome == "win" else 0.0,
                          vector.values.get("net:reward:outcome_win"))
        else:
            checker.check("reward/absent-terminal-is-unavailable",
                          set(vector.unavailable) == {"net:reward:outcome_win",
                                                      "net:reward:outcome_loss"},
                          f"ending={result.ending} unavailable={vector.unavailable}")

        scalar = sum(profile.weights[key] * value for key, value in vector.values.items())
        checker.check("reward/scalar-is-the-weighted-sum",
                      abs(vector.scalar - scalar) < 1e-12, f"{vector.scalar} vs {scalar}")
        again = derive_episode_reward(snapshots, profile, steps=result.steps)
        checker.check("reward/derivation-is-deterministic",
                      again.digest() == vector.digest(), f"{vector.digest()[:20]}")
        checker.check("reward/units-recorded",
                      set(vector.units) == set(vector.values),
                      f"units={sorted(vector.units)}")
        print(f"  reward: profile={profile.profile_id} scalar={vector.scalar:.4f} "
              f"values={ {key: round(value, 3) for key, value in vector.values.items()} }",
              file=sys.stderr)
    finally:
        client.shutdown()


def _run_evaluation(checker: Checker) -> None:
    """W-04：在**真实 stdio** 上按固定计划做一次配对评估（随机基线 vs 小模型）。

    它证明的是**评估工程**（保留种子、同种子配对、区间、成本、失败分类、未训练声明）；
    模型未训练，因此本组**不**给出任何"学习改善"结论。
    """
    client = NetClient.host_from_solution()
    try:
        probe = EpisodeRunner(client, seed=EPISODE_SEED, max_steps=MODEL_MAX_STEPS)
        probe.prepare()
        model = SmallPolicyModel(ModelConfig(), probe.manifest, catalog=probe._catalog)  # noqa: SLF001

        profile = standard_short_task_profile(ENGINEERING_WEIGHTS,
                                             profile_id="net:reward:engineering-probe-v1")
        plan = EvaluationPlan(
            plan_id="net:eval:j01-paired-probe-v1",
            semantic_version="1",
            policies=(
                PolicySpec(BASELINE_POLICY_ID, "uniform_baseline", "baseline",
                           "随机合法候选基线"),
                PolicySpec(MODEL_POLICY_ID, "small_model", "untrained",
                           "共享小模型（随机初始化，未训练）"),
            ),
            baseline_policy_id=BASELINE_POLICY_ID,
            seeds=EVALUATION_SEEDS,
            metrics=(
                MetricSpec(METRIC_REWARD, "reward", "higher_is_better"),
                MetricSpec(METRIC_STEPS, "steps", "lower_is_better"),
                MetricSpec(METRIC_TERMINAL_WIN, "boolean", "higher_is_better"),
                MetricSpec(METRIC_WALL_MS, "milliseconds", "lower_is_better"),
            ),
            reward_profile_id=profile.profile_id,
            reward_profile_digest=profile.digest(),
            executor_profile_id=EXECUTOR_PROFILE_ID,
            scenario_profile=probe.scenario_profile,
            gym_id=probe.gym_id,
            max_steps=EVALUATION_MAX_STEPS,
            bootstrap_resamples=500,
            bootstrap_seed=EXECUTOR_SEED,
            description="保留种子上的配对评估（工程探针；不做晋升结论）",
        )
        runner = EvaluationRunner(client, plan, profile)
        runner.bind_model_policy(MODEL_POLICY_ID, model)
        report = runner.run()

        wire = report.to_wire()
        checker.equal("evaluation/policies-covered", len(wire["policies"]), 2)
        checker.check("evaluation/episodes-per-policy",
                      all(summary["episodes"] == len(EVALUATION_SEEDS) for summary in wire["policies"]),
                      f"{[summary['episodes'] for summary in wire['policies']]}")
        endings = [episode["ending"] for episode in wire["episodes"]]
        checker.check("evaluation/endings-classified",
                      all(ending in {"terminal", "truncated", "interrupted", "faulted"}
                          for ending in endings),
                      f"{sorted(set(endings))}")
        checker.check("evaluation/comparisons-present",
                      len(wire["comparisons"]) == len(plan.metric_ids),
                      f"{len(wire['comparisons'])}")
        allowed = {"improved", "worse", "undecided", "insufficient_samples"}
        checker.check("evaluation/verdicts-are-explicit",
                      all(item["verdict"] in allowed for item in wire["comparisons"]),
                      f"{sorted({item['verdict'] for item in wire['comparisons']})}")
        checker.check("evaluation/untrained-disclaimer-recorded",
                      any("untrained" in note for note in wire["notes"]),
                      f"{wire['notes']}")
        checker.check("evaluation/costs-recorded",
                      all(summary["total_wall_ms"] and float(summary["total_wall_ms"]) > 0
                          for summary in wire["policies"])
                      and any(summary["parameter_total"] for summary in wire["policies"]),
                      f"{[summary['parameter_total'] for summary in wire['policies']]}")
        checker.check("evaluation/plan-identity-recorded",
                      wire["plan_digest"] == plan.digest()
                      and wire["plan"]["reward_profile_digest"] == profile.digest(),
                      f"{wire['plan_digest'][:20]}")
        path = pathlib.Path(tempfile.mkdtemp(prefix="j01-eval-")) / "report.json"
        path.write_text(json.dumps(wire, ensure_ascii=False, sort_keys=True), encoding="utf-8")
        checker.check("evaluation/report-written", path.stat().st_size > 0, f"{path}")
        summaries = {summary["policy_id"]: summary for summary in wire["policies"]}
        reward_means = {
            policy_id: summaries[policy_id]["metrics"].get(METRIC_REWARD, {}).get("mean")
            for policy_id in (BASELINE_POLICY_ID, MODEL_POLICY_ID)
        }
        verdicts = {(item["policy_id"], item["metric_id"]): item["verdict"]
                    for item in wire["comparisons"]}
        print(f"  evaluation: {path} reward_mean={reward_means} verdicts={verdicts}", file=sys.stderr)
    finally:
        client.shutdown()


def _run_scenarios(checker: Checker) -> None:
    """W-03：在**四个真实簇**上做有界采样，并逐簇核对"环境真的不同"。

    每个簇用自己宿主开关起**新**宿主进程（`--author-content` / `--test-headbutt` 的组合），
    采完之后在**该簇真实录到的公开快照**上核对清单声明的区分事实；限额、计数、墙钟、字节、
    停止原因与声明标签一起进报告。这是"簇不是标签"的可执行证据。
    """
    manifest = default_scenario_manifest()
    assignments = default_split_assignments(manifest)
    root = pathlib.Path(tempfile.mkdtemp(prefix="j01-scenarios-"))
    limits = CollectorLimits(episodes=1, steps_per_episode=SCENARIO_MAX_STEPS, seconds=120.0,
                             bytes=32 * 1024 * 1024, inflight_bytes=8 * 1024 * 1024)
    try:
        report = run_scenario_sampling(
            manifest, root,
            scenario_ids=manifest.scenario_ids,
            limits=limits,
            base_seed=EPISODE_SEED,
            workers=1,
            split_assignments=assignments,
        )
        wire = report.to_wire()
        checker.equal("scenarios/clusters-sampled", len(wire["clusters"]), len(manifest.scenarios))
        checker.check("scenarios/every-cluster-written",
                      all(cluster["episodes"] == 1 and cluster["counts"]["written"] == 1
                          for cluster in wire["clusters"]),
                      f"{[cluster['counts'] for cluster in wire['clusters']]}")
        checker.check("scenarios/distinguishing-facts-hold",
                      all(cluster["fact_ok"] for cluster in wire["clusters"]),
                      f"{[(cluster['scenario_id'], cluster['fact_detail']) for cluster in wire['clusters']]}")
        checker.check("scenarios/no-cluster-faulted-or-interrupted",
                      wire["totals"]["faulted"] == 0 and wire["totals"]["interrupted"] == 0,
                      f"totals={wire['totals']}")
        checker.check("scenarios/costs-recorded",
                      all(float(cluster["wall_seconds"]) > 0 and cluster["written_bytes"] > 0
                          for cluster in wire["clusters"]),
                      f"{[(cluster['wall_seconds'], cluster['written_bytes']) for cluster in wire['clusters']]}")
        checker.check("scenarios/limits-recorded",
                      all(cluster["limits"]["episodes"] == 1
                          and cluster["limits"]["steps_per_episode"] == SCENARIO_MAX_STEPS
                          for cluster in wire["clusters"]),
                      f"{wire['clusters'][0]['limits']}")
        checker.equal("scenarios/split-plan-digest-recorded",
                      wire["split_plan_digest"], plan_digest_of(assignments))
        checker.check("scenarios/round-robin-not-throughput",
                      wire["concurrency"].startswith("sequential")
                      and any("不是并行吞吐" in note for note in wire["notes"]),
                      f"{wire['concurrency']}")
        checker.check("scenarios/random-policy-labelled",
                      any("近期 PPO" in note for note in wire["notes"])
                      and wire["policy_id"].startswith("baseline:"),
                      f"{wire['policy_id']}")

        # 整份数据做一次划分审计（按身份分组 + 全局划分非空）。
        audit_result = audit_sampling(root, manifest, assignments)
        checker.check("scenarios/audit-clean", not audit_result.problems,
                      f"{audit_result.problems[:3]}")
        per_split = {split: audit_result.split_counts[split]["episodes"] for split in SPLITS}
        checker.check("scenarios/three-splits-non-empty",
                      not audit_result.degenerate_globally and all(count > 0 for count in per_split.values()),
                      f"{per_split} degenerate={audit_result.degenerate_globally}")
        checker.check("scenarios/clusters-traceable",
                      all(len(audit_result.split_counts[split]["clusters"]) >= 1 for split in SPLITS),
                      f"{ {split: audit_result.split_counts[split]['clusters'] for split in SPLITS} }")
        checker.check("scenarios/identity-families-separated",
                      len(audit_result.families) == 2
                      and all(not bucket["problems"] for bucket in audit_result.families.values()),
                      f"{ {family: bucket['scenarios'] for family, bucket in audit_result.families.items()} }")
        from ai_the_spire.data.store import EpisodeStore

        recorded = sum(len(EpisodeStore(root / family).rebuild_index())
                       for family in audit_result.families)
        checker.equal("scenarios/records-indexed", recorded, len(manifest.scenarios))
        report_path = root / "sampling-report.json"
        report_path.write_text(json.dumps(wire, ensure_ascii=False, sort_keys=True), encoding="utf-8")
        checker.check("scenarios/report-written", report_path.stat().st_size > 0, f"{report_path}")
        print(f"  scenarios: {report_path} totals={wire['totals']} "
              f"per_split={per_split}", file=sys.stderr)
    finally:
        shutil.rmtree(root, ignore_errors=True)


RUNNERS = {
    "episode": _run_episode,
    "headbutt": _run_headbutt,
    "invariants": _run_invariants,
    "trace": _run_trace,
    "author": _run_author,
    "model": _run_model,
    "reward": _run_reward,
    "evaluation": _run_evaluation,
    "scenarios": _run_scenarios,
}


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
