"""A-05：`encoder-design.md` §9 的 13 条验收场景（**样例侧**）＋ V-01/V-03/V-05/V-06/V-08 的样例部分。

用法：并入 `run_checks.py` 的 `acceptance` 组：`python ai-the-spire/tests/run_checks.py --only acceptance`。

关于"重排"场景（§9 第 5/8/11 条）的诚实说明：wire 的公开数组**按规范必须有序**
（`contract-artifacts.md#3`：实体按 `instance_id`、`visible_members` 按公开引用排序，共享 L3/I-74 强制），
因此"重排成员"在 wire 层不可能合法出现。本模块对这些场景断言两件**可判别**的事实：
① 乱序 wire 会被拒绝（顺序无法泄漏）；② 张量行序等于按公开 id 规范排序的顺序（隐藏排列无法影响结果）。
完整 V-02（隐藏牌序/RNG 游标变化）需要真实模拟证据，属 N-05。
"""

from __future__ import annotations

import copy
import pathlib
import sys
from typing import Any, Optional

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent))

from ai_the_spire.encoding import EncodingError, load_manifest, save_manifest, validate_snapshot  # noqa: E402
from fixture_suite import Outcome  # noqa: E402
import tensor_suite as ts  # noqa: E402
from tensor_suite import _guard  # noqa: E402

SUITE = "acceptance"
SUITE_NAMES = (SUITE,)


def _outcome(case_id: str, ok: bool, detail: str = "") -> Outcome:
    return Outcome(SUITE, case_id, bool(ok), detail)


# -- helpers ---------------------------------------------------------------------------------------------------


def _encode(bundle: str, document: Optional[dict] = None, **manifest_kwargs: Any):
    manifest = ts.manifest_for(bundle, **manifest_kwargs)
    doc = ts.snapshot(bundle) if document is None else document
    return manifest, ts.tensorize(doc, manifest, catalog=ts.catalog(bundle))


def _fingerprint(observation: Any) -> dict:
    """逐块逐列的完整取值（用于"逐元素相等"断言）。"""
    return {
        name: {column: list(observation.blocks[name].column(column)) for column in observation.blocks[name].columns}
        for name in ts.TENSOR_BLOCK_NAMES
    }


def _dictionary_items(dictionary: Any) -> dict:
    """把 manifest 的字典读成 ``{名字: 索引}``（只用公开的 ``name_of``，不假设内部字段）。"""
    items: dict = {}
    index = 0
    while True:
        name = dictionary.name_of(index)
        if name is None:
            break
        items[name] = index
        index += 1
    return items


def _row_of(document: dict) -> dict:
    return {instance: row for row, instance in enumerate(ts.expected_entity_order(document))}


def _field_row(observation: Any, manifest: Any, document: dict, instance: str, field_id: str) -> Optional[dict]:
    order = _row_of(document)
    block = observation.blocks["fields"]
    wanted_field = ts._manifest_index(manifest, "fields", field_id)
    for index in range(block.n):
        row = block.row(index)
        if row["field_id"] == wanted_field and row["owner_entity"] == order[instance]:
            return row
    return None


def _pile_position_rows(observation: Any) -> list:
    block = observation.blocks["pile_positions"]
    return [block.row(index) for index in range(block.n)]


# -- §9 13 条场景 ----------------------------------------------------------------------------------------------


def check_registration_order() -> Outcome:
    """§9-1 注册顺序不同 → 所有类型索引及数值变换逐项相等。"""
    case_id = "acceptance/registration-order-keeps-indices-and-transforms"
    base = ts.catalog("combat")
    shuffled = copy.deepcopy(base)
    for key in ("entity_kinds", "fields", "action_types", "relation_types", "content_types"):
        items = shuffled["declarations"].get(key)
        if isinstance(items, list):
            shuffled["declarations"][key] = list(reversed(items))
    first = ts.manifest_for("combat", catalog_document=base)
    second = ts.manifest_for("combat", catalog_document=shuffled)
    problems = []
    for dictionary in ("entity_kinds", "fields", "action_types"):
        if _dictionary_items(first.dictionaries[dictionary]) != _dictionary_items(second.dictionaries[dictionary]):
            problems.append(f"{dictionary} 索引随注册顺序变化")
    if dict(first.encoding_config.transforms) != dict(second.encoding_config.transforms):
        problems.append("数值变换随注册顺序变化")
    return _outcome(case_id, not problems, "；".join(problems))


def check_version_mix() -> Outcome:
    """§9-2 旧文件保持原映射；版本混用报 version_mismatch 且不返回编码。"""
    case_id = "acceptance/old-file-kept-and-version-mix-rejected"
    manifest = ts.manifest_for("combat")
    problems = []
    scratch = HERE / "_acceptance_manifest_tmp.json"
    try:
        save_manifest(manifest, scratch)
        reloaded = load_manifest(scratch)
        if _dictionary_items(reloaded.dictionaries["fields"]) != _dictionary_items(manifest.dictionaries["fields"]):
            problems.append("重载后的字段映射与原 manifest 不一致")
    finally:
        scratch.unlink(missing_ok=True)

    document = ts.snapshot("combat")
    document["catalog_id"] = "fixture:some-other-catalog"
    report = validate_snapshot(document, manifest, catalog=ts.catalog("combat"))
    codes = {error.code for error in report.errors}
    if "version_mismatch" not in codes:
        problems.append(f"版本混用未被拒绝：{sorted(codes)}")
    try:
        ts.tensorize(document, manifest, catalog=ts.catalog("combat"))
        problems.append("版本混用仍返回了编码（必须抛错、不得返回部分张量）")
    except EncodingError:
        pass
    return _outcome(case_id, not problems, "；".join(problems))


def check_two_mods_charge() -> Outcome:
    """§9-3 两个 mod 都声明 charge、玩家与敌人各持变量 → 字段类型与所有者均可区分。"""
    case_id = "acceptance/two-mods-charge-owners-distinguishable"
    document = ts.snapshot("variables")
    manifest, observation = _encode("variables")
    problems = []
    rows = {}
    for instance in ("player-01", "enemy-01", "power-01"):
        for field_id in ("modA:charge", "modB:charge", "modC:charge"):
            row = _field_row(observation, manifest, document, instance, field_id)
            if row is not None:
                rows[(instance, field_id)] = row
    if len(rows) < 3:
        problems.append(f"没能取到三个 mod 的 charge 字段行：{sorted(rows)}")
    owners = {instance: row["owner_entity"] for (instance, _), row in rows.items()}
    if len(set(owners.values())) != len(owners):
        problems.append("不同所有者的 charge 行没有落在不同的 owner 行上")
    # 场景 #3 要求"完整字段类型及所有者均可区分"：夹具故意让三个 mod 的 charge **同名不同类型**，
    # 因此这里按**目录声明**核对编码出的 value_type 索引，而不是要求它们都是 number。
    declared = {}
    for item in ts.catalog("variables")["declarations"]["fields"]:
        if item["id"] in ("modA:charge", "modB:charge", "modC:charge"):
            declared[item["id"]] = item["value_type"]
    for (instance, field_id), row in rows.items():
        wanted = ts.VALUE_TYPE_IDS.get(declared.get(field_id, ""))
        if wanted is None:
            problems.append(f"{field_id} 未在目录中声明 value_type")
        elif row["value_type"] != wanted:
            problems.append(f"{instance}.{field_id} value_type={row['value_type']} 与目录声明 {declared[field_id]} 不符")
        if row["knowledge_state"] != 1:
            problems.append(f"{instance}.{field_id} 的 knowledge_state={row['knowledge_state']} 不是 known(1)")
    if len({row["field_id"] for row in rows.values()}) != len(rows):
        problems.append("三个 mod 的 charge 没有落在不同的 field_id 上（无法区分）")
    return _outcome(case_id, not problems, "；".join(problems))


def check_same_face_cards() -> Outcome:
    """§9-4 同名卡两实例、费用不同 → 实例状态不同；候选关联准确。"""
    case_id = "acceptance/same-face-cards-differ-and-candidates-bind-right-instance"
    document = ts.snapshot("combat")
    manifest, observation = _encode("combat")
    problems = []
    first = _field_row(observation, manifest, document, "card-05", "fixture:cost")  # defend cost 0
    second = _field_row(observation, manifest, document, "card-07", "fixture:cost")  # defend cost 1
    if first is None or second is None:
        problems.append("没能取到两张同名 defend 卡的费用行")
    elif first["number_value"] == second["number_value"]:
        problems.append(f"同名卡的费用未区分：{first['number_value']} == {second['number_value']}")

    # 候选关联：cand-play-06 / cand-play-07 各自引用不同的卡实例行
    order = _row_of(document)
    candidates = observation.blocks["candidate_fields"]
    referenced = {}
    for index in range(candidates.n):
        row = candidates.row(index)
        if row["reference_mask"] is True:
            referenced.setdefault(row["owner_candidate"], set()).add(row["reference_entity"])
    if len(referenced) < 2:
        problems.append(f"候选未关联到卡实例：{referenced}")
    else:
        flat = [entity for values in referenced.values() for entity in values]
        if len(flat) != len(set(flat)):
            problems.append("两个候选引用了同一张卡行（关联不准确）")
    _ = order
    return _outcome(case_id, not problems, "；".join(problems))


def check_identical_cards_not_aggregated() -> Outcome:
    """§9-5 三张相同牌：各自独立成行、数量仍为三；乱序成员被拒（顺序无法泄漏）。"""
    case_id = "acceptance/identical-cards-stay-three-rows"
    document = ts.snapshot("combat")
    manifest, observation = _encode("combat")
    problems = []
    faces = {}
    for entity in document["entities"]:
        for field in entity["fields"]:
            if field["field_id"] == "fixture:card_face":
                faces.setdefault(field.get("value"), []).append(entity["instance_id"])
    strikes = faces.get("fixture:strike", [])
    if len(strikes) < 3:
        problems.append(f"夹具里同名 strike 少于三张：{strikes}")
    order = ts.expected_entity_order(document)
    rows = {instance: order.index(instance) for instance in strikes}
    if len(set(rows.values())) != len(rows):
        problems.append(f"同名卡没有各自独立成行：{rows}")

    permuted = copy.deepcopy(document)
    for pile in permuted["piles"]:
        if pile["pile_ref"] == "pile-draw":
            pile["visible_members"] = list(reversed(pile["visible_members"]))
    try:
        ts.tensorize(permuted, manifest, catalog=ts.catalog("combat"))
        problems.append("乱序的 visible_members 未被拒绝（顺序可能泄漏）")
    except EncodingError:
        pass
    return _outcome(case_id, not problems, "；".join(problems))


def check_states_distinguishable() -> Outcome:
    """§9-6 已知零 / 未知 / 不可提供 / 补齐 → 状态与 masks 可区分，不都被当作零值。"""
    case_id = "acceptance/states-and-masks-distinguishable"
    document = ts.snapshot("variables")
    manifest, observation = _encode("variables")
    problems = []
    states = {row["knowledge_state"] for row in (observation.blocks["fields"].row(i)
                                                 for i in range(observation.blocks["fields"].n))}
    for wanted, label in ((1, "known"), (2, "unknown"), (3, "unavailable")):
        if wanted not in states:
            problems.append(f"夹具里没有 {label}({wanted}) 的字段行（无法区分）")

    # 已知零：combat 的 card-05 费用为 0，但必须是 known 而非 unknown
    combat_document = ts.snapshot("combat")
    combat_observation = ts.tensorize(combat_document, manifest_for_combat := ts.manifest_for("combat"),
                                      catalog=ts.catalog("combat"))
    zero_row = _field_row(combat_observation, manifest_for_combat, combat_document, "card-05", "fixture:cost")
    if zero_row is None:
        problems.append("没能取到已知零的费用行")
    elif zero_row["number_value"] != 0.0 or zero_row["knowledge_state"] != 1:
        problems.append(f"已知零被当作未知：{zero_row}")

    # 补齐行（同一块里超过该样本真实行数的行）必须所有掩码为 false
    for name, mask_column in ts.MASK_COLUMNS.items():
        block = observation.blocks[name]
        if not (hasattr(block, "lengths") and hasattr(block, "batch_size")):
            continue
        values = block.column(mask_column)
        for sample in range(block.batch_size):
            for index in range(block.lengths[sample], block.rows):
                if values[sample][index] is not False:
                    problems.append(f"{name}.{mask_column}[{sample}][{index}] 补齐行掩码不是 false")
    return _outcome(case_id, not problems, "；".join(problems))


def check_first_input_known_position() -> Outcome:
    """§9-7 首次输入即置顶后（C 位置 0、总数五）→ 直接编码；位置为 0、总数五、位置记录不增加成员数。"""
    case_id = "acceptance/first-snapshot-already-has-position-zero"
    document = ts.snapshot("combat")
    manifest, observation = _encode("combat")
    problems = []
    draw = next(pile for pile in document["piles"] if pile["pile_ref"] == "pile-draw")
    if draw["total_count"] != 5:
        problems.append(f"夹具 draw 总数不是 5：{draw['total_count']}")
    known = {(item["card_ref"], item["position"]) for item in draw["known_positions"]}
    if ("card-03", 0) not in known:
        problems.append(f"夹具缺少『置顶牌位置 0』：{sorted(known)}")

    order = _row_of(document)
    positions = _pile_position_rows(observation)
    pairs = {(row["pile_entity"], row["card_entity"], row["absolute_position"]) for row in positions}
    wanted = {(order["pile-draw"], order["card-03"], 0)}
    if not wanted.issubset(pairs):
        problems.append(f"编码后没有 (draw, card-03, 0)：{sorted(pairs)}")
    if len(positions) != len(known):
        problems.append(f"位置行数 {len(positions)} != 夹具已知位置数 {len(known)}（位置记录不应增加成员数）")
    entity_rows = observation.blocks["entities"].n
    if entity_rows != len(document["entities"]):
        problems.append(f"实体行数 {entity_rows} != 快照实体数 {len(document['entities'])}（不得虚构卡牌）")
    return _outcome(case_id, not problems, "；".join(problems))


def check_positions_zero_and_three_only() -> Outcome:
    """§9-8 五张牌 C=0、D=3 → 0/3 保留、1/2/4 不被推断；行序为公开 id 规范序。"""
    case_id = "acceptance/known-positions-zero-and-three-only"
    document = ts.snapshot("combat")
    manifest, observation = _encode("combat")
    problems = []
    draw = next(pile for pile in document["piles"] if pile["pile_ref"] == "pile-draw")
    wanted_positions = {item["position"] for item in draw["known_positions"]}
    if wanted_positions != {0, 3}:
        problems.append(f"夹具已知位置不是 {{0,3}}：{sorted(wanted_positions)}")
    order = _row_of(document)
    actual = {row["absolute_position"] for row in _pile_position_rows(observation)
              if row["pile_entity"] == order["pile-draw"]}
    if actual != wanted_positions:
        problems.append(f"编码后的位置集合 {sorted(actual)} != 夹具 {sorted(wanted_positions)}（不得推断中间位置）")
    # 行序＝按公开 instance_id 规范排序 → 任何隐藏排列都无法影响结果
    block = observation.blocks["entities"]
    kind_column = block.column("kind_id")
    if len(kind_column) != len(order):
        problems.append("实体行数与规范排序后的实体数不一致")
    return _outcome(case_id, not problems, "；".join(problems))


def check_bad_positions_rejected() -> Outcome:
    """§9-9 越界 / 重复位置 / 重复卡牌定位记录 → invalid_position 或 duplicate_identity ＋ 具体路径。"""
    case_id = "acceptance/bad-position-records-rejected-with-path"
    manifest = ts.manifest_for("combat")
    catalog = ts.catalog("combat")
    problems = []

    def codes_for(document: dict) -> list:
        report = validate_snapshot(document, manifest, catalog=catalog)
        return [(error.code, error.path) for error in report.errors]

    out_of_range = ts.snapshot("combat")
    for pile in out_of_range["piles"]:
        if pile["pile_ref"] == "pile-draw":
            pile["known_positions"] = [{"card_ref": "card-03", "position": pile["total_count"]}]
    if not any(code == "invalid_position" and ".position" in path for code, path in codes_for(out_of_range)):
        problems.append(f"越界位置未被 invalid_position 拒绝：{codes_for(out_of_range)}")

    duplicate_position = ts.snapshot("combat")
    for pile in duplicate_position["piles"]:
        if pile["pile_ref"] == "pile-draw":
            pile["known_positions"] = [{"card_ref": "card-03", "position": 0},
                                       {"card_ref": "card-04", "position": 0}]
    if not any(code == "duplicate_identity" for code, _ in codes_for(duplicate_position)):
        problems.append(f"重复位置未被 duplicate_identity 拒绝：{codes_for(duplicate_position)}")

    duplicate_card = ts.snapshot("combat")
    for pile in duplicate_card["piles"]:
        if pile["pile_ref"] == "pile-draw":
            pile["known_positions"] = [{"card_ref": "card-03", "position": 0},
                                       {"card_ref": "card-03", "position": 1}]
    if not any(code == "duplicate_identity" for code, _ in codes_for(duplicate_card)):
        problems.append(f"重复卡牌定位未被 duplicate_identity 拒绝：{codes_for(duplicate_card)}")

    if not all(path for _, path in codes_for(out_of_range)):
        problems.append("拒绝时没有给出具体输入路径")
    return _outcome(case_id, not problems, "；".join(problems))


def check_no_residual_known_top() -> Outcome:
    """§9-10 先编码"含已知顶部"、再编码"洗牌后未知" → 后者与单独编码相同，无残留。"""
    case_id = "acceptance/no-residual-known-top-between-calls"
    manifest = ts.manifest_for("combat")
    catalog = ts.catalog("combat")
    with_known = ts.snapshot("combat")
    without = copy.deepcopy(with_known)
    for pile in without["piles"]:
        if pile["pile_ref"] == "pile-draw":
            pile["known_positions"] = []
    problems = []
    ts.tensorize(with_known, manifest, catalog=catalog)          # 先编码"含已知顶部"
    after = ts.tensorize(without, manifest, catalog=catalog)      # 再编码"未知顺序"
    alone = ts.tensorize(without, manifest, catalog=catalog)      # 单独编码同一份
    if _fingerprint(after) != _fingerprint(alone):
        problems.append("先前调用留下了残留（两次编码同一份未知快照结果不同）")
    if _pile_position_rows(after):
        problems.append("未知顺序的快照仍产出位置行（残留 known_top）")
    return _outcome(case_id, not problems, "；".join(problems))


def check_hidden_order_not_encoded() -> Outcome:
    """§9-11 隐藏顺序不同（同一公开事实）→ 特征逐元素相等，不含内部排列或 RNG。"""
    case_id = "acceptance/hidden-order-not-encoded"
    manifest = ts.manifest_for("combat")
    catalog = ts.catalog("combat")
    document = ts.snapshot("combat")
    problems = []
    first = ts.tensorize(document, manifest, catalog=catalog)
    second = ts.tensorize(copy.deepcopy(document), manifest, catalog=catalog)
    if _fingerprint(first) != _fingerprint(second):
        problems.append("同一公开事实两次编码结果不同（含 RNG/内部排列）")
    # 三份夹具声明的隐藏顺序都不能影响结果：wire 只保留规范序，因此编码同一份 wire 必须一致。
    fingerprints = {str(_fingerprint(ts.tensorize(document, manifest, catalog=catalog))) for _ in ts.HIDDEN_DRAW_ORDERS}
    if len(fingerprints) != 1:
        problems.append("隐藏顺序的不同取值影响了编码结果")
    return _outcome(case_id, not problems, "；".join(problems))


def check_turn_flags_preserved() -> Outcome:
    """§9-12 本回合出牌计数与公开触发标志 → 不给历史直接编码 → 字段及所有者正确保留。"""
    case_id = "acceptance/turn-flags-and-play-count-preserved"
    document = ts.snapshot("combat")
    manifest, observation = _encode("combat")
    problems = []
    for field_id in ("fixture:cards_played_this_turn", "fixture:relic_triggered"):
        owners = set()
        for entity in document["entities"]:
            if any(field["field_id"] == field_id for field in entity["fields"]):
                owners.add(entity["instance_id"])
        if not owners:
            problems.append(f"夹具里没有 {field_id}")
            continue
        for instance in owners:
            row = _field_row(observation, manifest, document, instance, field_id)
            if row is None:
                problems.append(f"{instance}.{field_id} 未被编码")
            elif row["knowledge_state"] not in (1, 2, 3):
                problems.append(f"{instance}.{field_id} 状态非法：{row['knowledge_state']}")
    return _outcome(case_id, not problems, "；".join(problems))


def check_repeat_matches_after_other_bundles() -> Outcome:
    """§9-13 相同当前快照、先前调用不同 → 重复编码结果一致，不积累游戏历史。"""
    case_id = "acceptance/repeat-after-other-calls-identical"
    manifest = ts.manifest_for("combat")
    variables_manifest = ts.manifest_for("variables")
    combat = ts.snapshot("combat")
    first = ts.tensorize(combat, manifest, catalog=ts.catalog("combat"))
    ts.tensorize(ts.snapshot("variables"), variables_manifest, catalog=ts.catalog("variables"))
    ts.tensorize(ts.snapshot("scenario"), ts.manifest_for("scenario"), catalog=ts.catalog("scenario"))
    again = ts.tensorize(combat, manifest, catalog=ts.catalog("combat"))
    problems = []
    if _fingerprint(first) != _fingerprint(again):
        problems.append("在其它 bundle 的编码之后，同一快照的编码结果改变（积累了历史）")
    return _outcome(case_id, not problems, "；".join(problems))


# -- 入口 ------------------------------------------------------------------------------------------------------


def check_runner_game_seed() -> Outcome:
    """A numeric experiment seed crosses the protocol boundary as game text."""
    from ai_the_spire.runner import EpisodeRunner

    snapshot = ts.snapshot("scenario", "snapshot-terminal")
    catalog = ts.catalog("scenario")
    snapshot["ruleset_id"] = catalog["ruleset_id"] = "net:ruleset:seed-boundary"
    identity = {key: snapshot[key] for key in (
        "protocol_version", "ruleset_id", "catalog_id", "backend_profile_id"
    )}

    class Client:
        accepted_seed = None

        def describe(self):
            return {"ok": True, "result": {"identity": identity, "capabilities": {
                "scenario_profiles": [{"id": "seed-boundary"}]
            }}}

        def get_catalog(self, catalog_id):
            return {"ok": True, "result": catalog}

        def start(self, expected_identity, scenario_profile, *, seed):
            if not isinstance(seed, str) or not seed:
                return {"ok": False, "error": {"code": "invalid_input"}}
            self.accepted_seed = seed
            return {"ok": True, "result": {
                "session_id": snapshot["session_id"], "seed": seed, "snapshot": snapshot
            }}

        def observe(self, session_id):
            return {"ok": True, "result": {"session_status": "terminal", "snapshot": snapshot}}

    client = Client()
    result = EpisodeRunner(client, seed=20261001).run()
    return _outcome("acceptance/runner-game-seed", (
        client.accepted_seed == "20261001" and result.ending == "terminal"
        and result.identities.seed == client.accepted_seed
    ), "runner must send the same game-seed text recorded in episode metadata")


def run_all(only: Optional[str] = None) -> list:
    if only is not None and only not in SUITE_NAMES:
        return []
    cases = (
        ("acceptance/runner-game-seed", check_runner_game_seed),
        ("acceptance/section9-1-registration-order", check_registration_order),
        ("acceptance/section9-2-version-mix", check_version_mix),
        ("acceptance/section9-3-two-mods-charge", check_two_mods_charge),
        ("acceptance/section9-4-same-face-cards", check_same_face_cards),
        ("acceptance/section9-5-identical-cards", check_identical_cards_not_aggregated),
        ("acceptance/section9-6-states-distinguishable", check_states_distinguishable),
        ("acceptance/section9-7-first-input-known-position", check_first_input_known_position),
        ("acceptance/section9-8-positions-zero-and-three", check_positions_zero_and_three_only),
        ("acceptance/section9-9-bad-positions-rejected", check_bad_positions_rejected),
        ("acceptance/section9-10-no-residual-known-top", check_no_residual_known_top),
        ("acceptance/section9-11-hidden-order-not-encoded", check_hidden_order_not_encoded),
        ("acceptance/section9-12-turn-flags-preserved", check_turn_flags_preserved),
        ("acceptance/section9-13-repeat-after-other-calls", check_repeat_matches_after_other_bundles),
    )
    return [_guard(SUITE, case_id, run) for case_id, run in cases]
