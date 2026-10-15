#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""A-04 检查套件：``encoding/tensor.py`` 的 tensorize / collate。

组名：``tensorize`` 与 ``collate``（同一模块，两个组）。

期望来源（**手写独立**，不由被测实现产出）
------------------------------------------

* 块/列/dtype/枚举的期望**逐字抄自** ``encoder-design.md`` §5.1 第 153–166 行，
  写成本模块顶部的字面量表，与实现里的表相互独立（实现改了表这边就会失败）；
* 行数与行内容由本模块用 ``json`` + 独立排序/计数从 fixtures 重新算出，
  **不调用 tensorize 反填期望**；
* 内容类型索引用 ``fixtures/tensor/content_types.json`` 的补全声明 + "按完整名称
  UTF-8 字节序、索引从 1 开始" 独立算出（``_content_index``）；
* 单条的数值期望（如 ``fixture:energy`` 的 ``(3-1)/3``）用 ``decimal`` 在期望侧
  独立计算，不用被测实现的 ``encode_number``；
* 负例期望写成 ``(错误码, 路径)``，路径口径与共享 L3/A-03 一致。

反向（反例）检查是重点：见 ``check_cross_sample_*`` 与 ``check_padding_*``——
它们证明"同槽位不同实体"不会被跨样本解析、补齐行不会被当成真实记录/第 0 行。
"""

from __future__ import annotations

import copy
import json
from decimal import Decimal
from pathlib import Path
from typing import Any, Callable, Optional

from ai_the_spire.contracts import ContractViolation, layers
from ai_the_spire.encoding import (
    COLUMN_SCHEMAS,
    CONTENT_STATE_IDS,
    INDEX_COLUMNS,
    KNOWLEDGE_STATE_IDS,
    MASK_COLUMNS,
    TENSOR_BLOCK_NAMES,
    VALUE_TYPE_IDS,
    CapacityLimits,
    EncodingConfig,
    EncodingError,
    GymManifest,
    NumericTransform,
    ReferenceMap,
    build_manifest,
    collate,
    tensorize,
    validate_snapshot,
)

from fixture_suite import BUNDLES, FIXTURE_ROOT, Outcome

SUITE_TENSORIZE = "tensorize"
SUITE_COLLATE = "collate"
SUITE_NAMES = (SUITE_TENSORIZE, SUITE_COLLATE)

TENSOR_ROOT = FIXTURE_ROOT / "tensor"

#: 输入配置（不是期望）：与 A-02/A-03 同值的显式变换与容量上限。
TRANSFORMS: "dict[str, dict[str, tuple[str, str]]]" = {
    "combat": {"fixture:energy": ("1", "3")},
    "variables": {"modB:charge": ("0", "0.5")},
    "scenario": {"fixture:node_row": ("2", "4")},
    "card_choice": {"fixture:cost": ("0", "2")},
}
CAPACITIES = {
    "max_entities": 64,
    "max_fields": 64,
    "max_relations": 64,
    "max_pile_positions": 32,
    "max_sequences": 8,
    "max_groups": 8,
    "max_candidates": 8,
    "max_candidate_fields": 32,
}

#: encoder-design §5.1 第 155–162 行的块与列（逐字手抄；dtype 取第 164 行）。
EXPECTED_SCHEMA = (
    ("entities", (("kind_id", "int64"), ("content_id", "int64"), ("content_state", "int64"),
                  ("content_mask", "bool"), ("entity_mask", "bool"))),
    ("fields", (("owner_entity", "int64"), ("field_id", "int64"), ("value_type", "int64"),
                ("knowledge_state", "int64"), ("number_value", "float64"), ("boolean_value", "int64"),
                ("category_id", "int64"), ("reference_entity", "int64"), ("reference_mask", "bool"),
                ("field_mask", "bool"))),
    ("relations", (("type_id", "int64"), ("source", "int64"), ("target", "int64"),
                   ("position", "int64"), ("position_mask", "bool"), ("relation_mask", "bool"))),
    ("pile_positions", (("pile_entity", "int64"), ("card_entity", "int64"),
                        ("absolute_position", "int64"), ("position_record_mask", "bool"))),
    ("sequences", (("sequence_type", "int64"), ("owner", "int64"), ("element", "int64"),
                   ("ordinal", "int64"), ("sequence_record_mask", "bool"))),
    ("groups", (("group_type", "int64"), ("owner", "int64"), ("member", "int64"),
                ("multiplicity", "int64"), ("group_record_mask", "bool"))),
    ("candidates", (("action_type", "int64"), ("candidate_mask", "bool"))),
    ("candidate_fields", (("owner_candidate", "int64"), ("field_id", "int64"), ("value_type", "int64"),
                          ("knowledge_state", "int64"), ("number_value", "float64"),
                          ("boolean_value", "int64"), ("category_id", "int64"),
                          ("reference_entity", "int64"), ("reference_mask", "bool"),
                          ("field_mask", "bool"))),
)

#: §5.1 第 166 行的固定枚举（手抄）。
EXPECTED_VALUE_TYPE = {"pad": 0, "number": 1, "boolean": 2, "category": 3, "entity_ref": 4}
EXPECTED_KNOWLEDGE_STATE = {"pad": 0, "known": 1, "unknown": 2, "unavailable": 3}
EXPECTED_CONTENT_STATE = {
    "pad": 0,
    "known": 1,
    "unknown": 2,
    "unavailable": 3,
    "not_applicable": 4,
}

#: §5.1 第 157/159/160 行的行 padding 掩码列（手抄）。
EXPECTED_MASK_COLUMNS = {
    "entities": "entity_mask",
    "fields": "field_mask",
    "relations": "relation_mask",
    "pile_positions": "position_record_mask",
    "sequences": "sequence_record_mask",
    "groups": "group_record_mask",
    "candidates": "candidate_mask",
    "candidate_fields": "field_mask",
}

#: 关系 ``position`` 只在声明含该语义时有效（§5.1 第 172 行）→ 无位置列的关系行
#: ``position_mask`` 必须为 false；此处只列本套件用到的"无位置"关系。
RELATIONS_WITHOUT_POSITION = ("fixture:held_by", "net:pile_member")

_JSON_CACHE: dict = {}


# --------------------------------------------------------------------------- #
# 夹具装载（输入侧）
# --------------------------------------------------------------------------- #
def _read_json(path: Path) -> Any:
    if path not in _JSON_CACHE:
        with path.open("r", encoding="utf-8") as handle:
            _JSON_CACHE[path] = json.load(handle)
    return copy.deepcopy(_JSON_CACHE[path])


def _content_type_overlay() -> dict:
    return _read_json(TENSOR_ROOT / "content_types.json")["bundles"]


def _environment() -> dict:
    return _read_json(FIXTURE_ROOT / "manifest" / "expected.json")["environment"]


def raw_catalog(bundle: str) -> dict:
    """A-01 的原始目录（content_id 未按 content_types 声明）。"""
    return _read_json(FIXTURE_ROOT / bundle / "catalog.json")


def catalog(bundle: str) -> dict:
    """原始目录 + ``fixtures/tensor/content_types.json`` 的内容定义补全（手写输入）。"""
    document = raw_catalog(bundle)
    items = _content_type_overlay().get(bundle) or []
    if not items:
        return document
    document["declarations"]["content_types"] = list(document["declarations"]["content_types"]) + [
        {
            "id": item["id"],
            "semantic_version": "1",
            "description": f"A-04 内容定义补全 {item['id']}",
            "kind_ids": [item["kind_id"]],
        }
        for item in items
    ]
    return document


def snapshot(bundle: str, stem: str = "snapshot") -> dict:
    return _read_json(FIXTURE_ROOT / bundle / f"{stem}.json")


def config_for(bundle: str, capacities: Optional[dict] = None, tolerances: Optional[dict] = None) -> EncodingConfig:
    return EncodingConfig(
        transforms={
            field_id: NumericTransform(offset=offset, scale=scale)
            for field_id, (offset, scale) in TRANSFORMS.get(bundle, {}).items()
        },
        capacities=CapacityLimits(**(capacities or CAPACITIES)),
        **(tolerances or {}),
    )


def manifest_for(
    bundle: str,
    *,
    catalog_document: Optional[dict] = None,
    encoding_config: Optional[EncodingConfig] = None,
    use_overlay: bool = True,
) -> GymManifest:
    if catalog_document is not None:
        document = catalog_document
    elif use_overlay:
        document = catalog(bundle)
    else:
        # use_overlay=False：显式要用 A-01 的**原始**目录（content_id 未按 content_types 声明）。
        document = raw_catalog(bundle)
    environment = _environment()
    return build_manifest(
        document,
        encoding_config or config_for(bundle),
        backend_profile_id=environment["backend_profile_id"],
        gym_id=environment["gym_id"],
    )


# --------------------------------------------------------------------------- #
# 独立期望（只用 fixtures 与规范文本，不调用被测入口）
# --------------------------------------------------------------------------- #
def expected_entity_order(document: dict) -> list:
    """§5 第 139 行：实体按 instance_id 的 UTF-8 字节序。"""
    return sorted(
        (entity["instance_id"] for entity in document["entities"]),
        key=lambda text: text.encode("utf-8"),
    )


def expected_sizes(document: dict) -> dict:
    """由 wire 行数与 §4 第 103 行的派生规则独立算出块行数。"""
    piles = document.get("piles") or []
    return {
        "entities": len(document["entities"]),
        "fields": sum(len(entity["fields"]) for entity in document["entities"]) + 2 * len(piles),
        "relations": len(document["relations"]) + sum(len(pile["visible_members"]) for pile in piles),
        "pile_positions": sum(len(pile["known_positions"]) for pile in piles),
        "sequences": sum(len(sequence["elements"]) for sequence in document.get("sequences") or []),
        "groups": sum(len(group["members"]) for group in document.get("groups") or []),
        "candidates": len(document["legal_actions"]),
        "candidate_fields": sum(len(action["fields"]) for action in document["legal_actions"]),
    }


def _content_index(bundle: str, content_id: str) -> int:
    """内容索引 = 目录 content_types 名称的 UTF-8 排序位置 + 1（§2 第 45–46 行）。"""
    names = [item["id"] for item in catalog(bundle)["declarations"]["content_types"]]
    ordered = sorted(set(names), key=lambda text: text.encode("utf-8"))
    return ordered.index(content_id) + 1


def _row_of(document: dict, instance_id: str) -> int:
    return expected_entity_order(document).index(instance_id)


def _manifest_index(manifest: GymManifest, table: str, name: str) -> int:
    index = manifest.dictionaries[table].index_of(name)
    assert index is not None, f"{table} 缺少 {name!r}"
    return index


# --------------------------------------------------------------------------- #
# 输入构造（手写的变体；每个变体都先用 A-03 证明是合法输入）
# --------------------------------------------------------------------------- #
def combat_without_first_two_cards() -> dict:
    """战斗快照的手改变体：删掉 card-01 / card-02 并同步 draw 牌堆。

    改动（逐条手写，不是派生）：删除 entities[0]、entities[1]（fixture 里就是
    card-01/card-02，扑克面 strike）；draw 牌堆 ``total_count`` 5→3、
    ``visible_members`` 去掉这两张、``known_positions`` 里 card-04 的位置 3→2
    （否则违反 V-06.1 的 ``0 <= position < total_count``）。
    """
    document = snapshot("combat")
    document["entities"] = [
        entity for entity in document["entities"] if entity["instance_id"] not in ("card-01", "card-02")
    ]
    for pile in document["piles"]:
        if pile["pile_ref"] != "pile-draw":
            continue
        pile["total_count"] = 3
        pile["visible_members"] = ["card-03", "card-04", "card-05"]
        pile["known_positions"] = [
            {"card_ref": "card-03", "position": 0},
            {"card_ref": "card-04", "position": 2},
        ]
    return document


def combat_hand_pile_with_positions() -> dict:
    """手牌堆（pile_type.position_schema = null）却给出已知位置：共享 L3/A-03 都放过。"""
    document = snapshot("combat")
    for pile in document["piles"]:
        if pile["pile_ref"] == "pile-hand":
            pile["known_positions"] = [{"card_ref": "card-06", "position": 0}]
    return document


#: §9 第 230 行"隐藏顺序不同"的手写**隐藏顺序**样本（draw 牌堆五张牌的引擎内部遍历
#: 顺序）。它不上 wire：``wire.schema.json:1108-1110`` 写明 ``visible_members`` 是
#: "无隐藏顺序的卡牌实例引用，按公开引用排序"（L37 同一要求，L3/I-74 强制）——
#: 因此提供方写出前必须按公开引用排序，三种隐藏顺序产生**同一份 wire 字节**。
HIDDEN_DRAW_ORDERS = (
    ("card-01", "card-02", "card-03", "card-04", "card-05"),  # 升序（与 fixture 一致）
    ("card-05", "card-04", "card-03", "card-02", "card-01"),  # 反转
    ("card-03", "card-01", "card-04", "card-05", "card-02"),  # 轮换/交错
)


def combat_members_reordered() -> dict:
    """**反例夹具（非法 wire）**：把 draw 牌堆 ``visible_members`` 反转。

    名称保留自前任，但语义已改：反转后的 ``visible_members`` **违反 I-74**
    （``contract-artifacts.md`` L37：``visible_members`` 按公开引用排序），是**非法
    wire**，只用于证明"提供方必须排序、校验器不替它排序"。**不要**拿它当正例，
    也不要在校验前先排序——那会把"合法 wire 必须有序"与"隐藏顺序不是 wire 信息"
    这两件事混为一谈（§9 第 224/230 行的"重排"指隐藏/内部顺序）。
    """
    document = snapshot("combat")
    for pile in document["piles"]:
        if pile["pile_ref"] == "pile-draw":
            pile["visible_members"] = list(reversed(pile["visible_members"]))
    return document


def observation_of_hidden_order(hidden_order: "tuple[str, ...]") -> dict:
    """**集合层构造**：给定引擎的隐藏遍历顺序，写出一份公开观察。

    这一步刻意写在**检查侧**（并带显式断言），代表提供方的义务
    （``contract-artifacts.md`` L37）：隐藏顺序不写出，``visible_members`` 按公开引用
    的 UTF-8 字节序排序。被测实现拿到的永远是排好序的合法 wire；
    :func:`check_permuted_wire_order_rejected` 另行证明"没排序的 wire 会被拒"。
    """
    document = snapshot("combat")
    for pile in document["piles"]:
        if pile["pile_ref"] != "pile-draw":
            continue
        if sorted(hidden_order) != sorted(pile["visible_members"]):
            raise AssertionError(f"隐藏顺序 {hidden_order} 不是 {pile['visible_members']} 的排列")
        pile["visible_members"] = sorted(hidden_order, key=lambda text: text.encode("utf-8"))
    return document


def renamed_instances(document: dict, transform: Callable[[str], str]) -> dict:
    """把实例 ID 一致重命名（连同所有引用一起改）。

    实现方式与协议无关：先收集当前所有 instance_id，再把**等于某个实例 ID 的字符串值**
    整体替换——覆盖 entities.instance_id、piles 的 pile_ref/visible_members/card_ref、
    relations 的 source_ref/target_ref、sequences 的 owner_ref/elements、groups 的
    owner_ref/members[].entity_ref，以及各字段上取值恰为实例 ID 的 entity_ref。
    """
    renamed = copy.deepcopy(document)
    instance_ids = {entity["instance_id"] for entity in document["entities"]}
    mapping = {old: transform(old) for old in instance_ids}

    def fix(value):
        if isinstance(value, str) and value in mapping:
            return mapping[value]
        if isinstance(value, list):
            return [fix(item) for item in value]
        if isinstance(value, dict):
            return {key: fix(item) for key, item in value.items()}
        return value

    return fix(renamed)


def _pairs(errors) -> list:
    return sorted([error.code, error.path] for error in errors)


# --------------------------------------------------------------------------- #
# 用例执行
# --------------------------------------------------------------------------- #
class _Context:
    """一次运行的共享上下文：目录、Manifest 与已编码样本缓存。"""

    def __init__(self) -> None:
        self._manifests: dict = {}
        self._encoded: dict = {}

    def manifest(self, bundle: str, **kwargs) -> GymManifest:
        key = (bundle, json.dumps(kwargs, sort_keys=True, default=str))
        if key not in self._manifests:
            self._manifests[key] = manifest_for(bundle, **kwargs)
        return self._manifests[key]

    def encoded(self, bundle: str, stem: str = "snapshot", **kwargs):
        key = (bundle, stem, json.dumps(kwargs, sort_keys=True, default=str))
        if key not in self._encoded:
            document = snapshot(bundle, stem)
            manifest = self.manifest(bundle, **kwargs)
            self._encoded[key] = tensorize(document, manifest, catalog=catalog(bundle))
        return self._encoded[key]


def _outcome(suite: str, case_id: str, ok: bool, detail: str = "") -> Outcome:
    return Outcome(suite, case_id, bool(ok), detail)


def _guard(suite: str, case_id: str, run: Callable[[], Any]) -> Outcome:
    """执行一个检查体（返回 Outcome 或 ``(ok, detail)``）；失败记为失败项而非中止整套。"""
    try:
        result = run()
    except (EncodingError, ContractViolation, AssertionError, KeyError, IndexError, TypeError) as exc:
        return _outcome(suite, case_id, False, f"{type(exc).__name__}: {exc}")
    if isinstance(result, Outcome):
        # 检查体自己声明的 case_id 是权威（调用点只给异常路径的兜底名）。
        return _outcome(result.suite or suite, result.case_id or case_id, result.ok, result.detail)
    ok, detail = result
    return _outcome(suite, case_id, ok, detail)


def _expect_error(run: Callable[[], Any], wanted: tuple) -> tuple:
    """断言 ``run()`` 抛出恰好 ``wanted=(code, path)`` 的 EncodingError。"""
    try:
        run()
    except EncodingError as exc:
        if (exc.code, exc.path) != wanted:
            return False, f"抛出 {(exc.code, exc.path)!r}，期望 {wanted!r}"
        return True, f"{exc.code} @ {exc.path}"
    return False, f"未抛出，期望 {wanted!r}"


# --------------------------------------------------------------------------- #
# schema / 枚举 / dtype
# --------------------------------------------------------------------------- #
def check_schema_surface() -> Outcome:
    """块名、列名、dtype、掩码列与固定枚举逐项对照 §5.1 手抄表。"""
    case_id = "schema/blocks-columns-dtypes-enums"
    problems = []
    if tuple(TENSOR_BLOCK_NAMES) != tuple(name for name, _ in EXPECTED_SCHEMA):
        problems.append(f"块顺序/名称不符：{list(TENSOR_BLOCK_NAMES)}")
    for name, columns in EXPECTED_SCHEMA:
        # COLUMN_SCHEMAS[name] 已是 ((column, dtype), ...)（见 tensor.py:80 的类型与 :349 的迭代方式），
        # 因此直接取该元组与手写表逐项比较；不要按 column 名字再索引一次。
        actual = tuple(COLUMN_SCHEMAS[name]) if name in COLUMN_SCHEMAS else ()
        expected = tuple(columns)
        if actual != expected:
            problems.append(f"{name} 列/dtype 不符：{list(actual)} != {list(expected)}")
    for mapping, expected, label in (
        (VALUE_TYPE_IDS, EXPECTED_VALUE_TYPE, "value_type"),
        (KNOWLEDGE_STATE_IDS, EXPECTED_KNOWLEDGE_STATE, "knowledge_state"),
        (CONTENT_STATE_IDS, EXPECTED_CONTENT_STATE, "content_state"),
        (MASK_COLUMNS, EXPECTED_MASK_COLUMNS, "mask 列"),
    ):
        got = dict(mapping)
        if got != expected:
            problems.append(f"{label} 枚举/掩码列不符：{got} != {expected}")
    if dict(INDEX_COLUMNS) != {
        "entities": (),
        "fields": ("owner_entity",),
        "relations": ("source", "target"),
        "pile_positions": ("pile_entity", "card_entity"),
        "sequences": ("owner", "element"),
        "groups": ("owner", "member"),
        "candidates": (),
        "candidate_fields": ("owner_candidate",),
    }:
        problems.append(f"索引列声明不符：{dict(INDEX_COLUMNS)}")
    return _outcome(SUITE_TENSORIZE, case_id, not problems, "；".join(problems))


def _dtype_violations(block_name: str, columns: dict) -> list:
    """独立 dtype 审计：int64 不得是 bool、bool 必须是 bool、float64 必须是 float。"""
    problems = []
    schema = dict(EXPECTED_SCHEMA)[block_name]
    for column, dtype in schema:
        for index, value in enumerate(columns[column]):
            if dtype == "bool":
                ok = isinstance(value, bool)
            elif dtype == "int64":
                ok = isinstance(value, int) and not isinstance(value, bool)
            else:
                ok = isinstance(value, float)
            if not ok:
                problems.append(f"{block_name}.{column}[{index}]={value!r} 不是 {dtype}")
    return problems


# --------------------------------------------------------------------------- #
# 逐 bundle 检查
# --------------------------------------------------------------------------- #
BUNDLE_CASES = (
    ("combat", "snapshot"),
    ("scenario", "snapshot"),
    ("scenario", "snapshot-terminal"),
    ("card_choice", "snapshot"),
    ("variables", "snapshot"),
)


def _check_sizes(context: _Context, bundle: str, stem: str) -> Outcome:
    document = snapshot(bundle, stem)
    expected = expected_sizes(document)
    observation = context.encoded(bundle, stem)
    got = observation.sizes
    problems = []
    for block in TENSOR_BLOCK_NAMES:
        if got[block] != expected[block]:
            problems.append(f"{block}: {got[block]} != {expected[block]}")
    return _outcome(SUITE_TENSORIZE, f"{bundle}/{stem}/sizes", not problems, "；".join(problems))


def _check_entity_rows(context: _Context, bundle: str, stem: str) -> Outcome:
    """实体行顺序、kind/content 索引与 content_mask（独立算，不看实现中间量）。"""
    document = snapshot(bundle, stem)
    manifest = context.manifest(bundle)
    observation = context.encoded(bundle, stem)
    order = expected_entity_order(document)
    by_id = {entity["instance_id"]: entity for entity in document["entities"]}
    problems = []
    block = observation.blocks["entities"]
    if list(block.column("kind_id")) != [
        _manifest_index(manifest, "entity_kinds", by_id[instance]["kind_id"]) for instance in order
    ]:
        problems.append("kind_id 列与按 instance_id 排序的实体不一致")
    for row, instance in enumerate(order):
        entity = by_id[instance]
        state = entity["content_state"]
        if block.column("content_state")[row] != EXPECTED_CONTENT_STATE[state]:
            problems.append(f"{instance}: content_state 序数不符")
        if state == "known":
            if block.column("content_id")[row] != _content_index(bundle, entity["content_id"]):
                problems.append(
                    f"{instance}: content_id {block.column('content_id')[row]} "
                    f"!= {_content_index(bundle, entity['content_id'])}（{entity['content_id']}）"
                )
            if block.column("content_mask")[row] is not True:
                problems.append(f"{instance}: known 的 content_mask 必须是 true")
        else:
            if block.column("content_id")[row] != 0 or block.column("content_mask")[row] is not False:
                problems.append(f"{instance}: 非 known 必须 content_id=0 且 content_mask=false")
    if any(flag is not True for flag in block.column("entity_mask")):
        problems.append("样本内 entity_mask 必须全 true（补齐才 false）")
    return _outcome(SUITE_TENSORIZE, f"{bundle}/{stem}/entity-rows", not problems, "；".join(problems))


def _check_field_rows(context: _Context, bundle: str, stem: str) -> Outcome:
    """字段行：owner/字段索引、知识状态序数、值通道按类型启用、未启用通道为 0。"""
    document = snapshot(bundle, stem)
    manifest = context.manifest(bundle)
    observation = context.encoded(bundle, stem)
    order = expected_entity_order(document)
    row_of = {instance: row for row, instance in enumerate(order)}
    by_id = {entity["instance_id"]: entity for entity in document["entities"]}

    expected_rows = []
    for instance in order:
        for value_field in by_id[instance]["fields"]:
            expected_rows.append((row_of[instance], value_field["field_id"], instance, value_field))
    for pile in document.get("piles") or []:
        for field_id in ("net:pile_members_complete", "net:pile_total_count"):
            expected_rows.append((row_of[pile["pile_ref"]], field_id, pile["pile_ref"], None))
    expected_rows.sort(key=lambda item: (item[0], item[1].encode("utf-8")))

    block = observation.blocks["fields"]
    problems = []
    if block.n != len(expected_rows):
        problems.append(f"字段行数 {block.n} != 手算 {len(expected_rows)}")
    for index, (owner, field_id, instance, value_field) in enumerate(expected_rows):
        if index >= block.n:
            break
        row = block.row(index)
        record = manifest.field_records[field_id]
        if row["owner_entity"] != owner:
            problems.append(f"行{index} owner_entity {row['owner_entity']} != {owner}（{instance}）")
        if row["field_id"] != record.index:
            problems.append(f"行{index} field_id {row['field_id']} != {record.index}（{field_id}）")
        if row["value_type"] != EXPECTED_VALUE_TYPE[record.value_type]:
            problems.append(f"行{index} value_type 序数不符（{field_id}）")
        if row["field_mask"] is not True:
            problems.append(f"行{index} 样本内 field_mask 必须 true")
        if value_field is None:
            if row["knowledge_state"] != EXPECTED_KNOWLEDGE_STATE["known"]:
                problems.append(f"行{index} 派生字段必须是 known（{field_id}）")
            continue
        state = value_field["state"]
        if row["knowledge_state"] != EXPECTED_KNOWLEDGE_STATE[state]:
            problems.append(f"行{index} knowledge_state 序数不符（{instance}.{field_id}）")
        channels = ("number_value", "boolean_value", "category_id", "reference_entity")
        if state != "known":
            if any(row[name] not in (0, 0.0, False) for name in channels):
                problems.append(f"行{index} 非 known 的值通道必须为 0（{instance}.{field_id}）")
            if row["reference_mask"] is not False:
                problems.append(f"行{index} 非 known 的 reference_mask 必须 false（{instance}.{field_id}）")
            continue
        value = value_field["value"]
        if record.value_type == "number":
            offset, scale = TRANSFORMS.get(bundle, {}).get(field_id, ("0", "1"))
            exact = (Decimal(value) - Decimal(offset)) / Decimal(scale)
            if row["number_value"] != float(exact):
                problems.append(f"行{index} number_value {row['number_value']!r} != {float(exact)!r}")
            if not isinstance(row["number_value"], float):
                problems.append(f"行{index} number 通道必须是 float64")
        elif record.value_type == "boolean":
            if row["boolean_value"] != (1 if value else 0) or row["number_value"] != 0.0:
                problems.append(f"行{index} boolean 通道不符（{instance}.{field_id}）")
        elif record.value_type == "category":
            if row["category_id"] != _manifest_index(manifest, "categories", value):
                problems.append(f"行{index} category_id {row['category_id']} 与类别字典不符")
            if row["number_value"] != 0.0:
                problems.append(f"行{index} category 不得写 number 通道（不得转连续浮点）")
        else:  # entity_ref
            if row["reference_entity"] != row_of[value] or row["reference_mask"] is not True:
                problems.append(
                    f"行{index} entity_ref 应指向本样本行 {row_of[value]} 且 reference_mask=true"
                    f"（{instance}.{field_id}）"
                )
    problems.extend(_dtype_violations("fields", dict(block.columns)))
    return _outcome(SUITE_TENSORIZE, f"{bundle}/{stem}/field-rows", not problems, "；".join(problems[:6]))


def _check_derived_piles(context: _Context) -> Outcome:
    """派生：每个牌堆两个字段行的 owner 与取值；三个保留声明必须来自 Manifest 字典。"""
    document = snapshot("combat")
    manifest = context.manifest("combat")
    observation = context.encoded("combat")
    order = expected_entity_order(document)
    row_of = {instance: row for row, instance in enumerate(order)}
    block = observation.blocks["fields"]
    problems = []
    seen = set()
    for index in range(block.n):
        row = block.row(index)
        field_name = manifest.dictionaries["fields"].name_of(row["field_id"])
        if field_name not in ("net:pile_total_count", "net:pile_members_complete"):
            continue
        owner = order[row["owner_entity"]]
        seen.add((owner, field_name))
        pile = next(item for item in document["piles"] if item["pile_ref"] == owner)
        if field_name == "net:pile_total_count":
            if row["number_value"] != float(pile["total_count"]) or row["knowledge_state"] != 1:
                problems.append(f"{owner}.total_count 行不符: {row}")
        else:
            want = 1 if pile["members_complete"] else 0
            if row["boolean_value"] != want or row["value_type"] != EXPECTED_VALUE_TYPE["boolean"]:
                problems.append(f"{owner}.members_complete 行不符: {row}")
    expected = {
        (pile["pile_ref"], field_id)
        for pile in document["piles"]
        for field_id in ("net:pile_total_count", "net:pile_members_complete")
    }
    if seen != expected:
        problems.append(f"派生字段行集合 {sorted(seen)} != 手算 {sorted(expected)}")
    # draw 牌堆的已知常量（与第 4 节示例一致）：总数 5
    for index in range(block.n):
        row = block.row(index)
        if (
            order[row["owner_entity"]] == "pile-draw"
            and manifest.dictionaries["fields"].name_of(row["field_id"]) == "net:pile_total_count"
        ):
            if row["number_value"] != 5.0:
                problems.append(f"pile-draw 总数应为 5.0，得到 {row['number_value']!r}")
    return _outcome(SUITE_TENSORIZE, "combat/derived-pile-fields", not problems, "；".join(problems))


def _check_pile_positions(context: _Context) -> Outcome:
    """已知位置：只有 fixture 声明的 0/3 两条，不推断 1/2/4，且不增加卡牌数。"""
    document = snapshot("combat")
    manifest = context.manifest("combat")
    observation = context.encoded("combat")
    order = expected_entity_order(document)
    row_of = {instance: row for row, instance in enumerate(order)}
    block = observation.blocks["pile_positions"]
    problems = []
    expected = [(row_of["pile-draw"], row_of["card-03"], 0), (row_of["pile-draw"], row_of["card-04"], 3)]
    got = [
        (block.column("pile_entity")[i], block.column("card_entity")[i], block.column("absolute_position")[i])
        for i in range(block.n)
    ]
    if got != expected:
        problems.append(f"位置行 {got} != 手算 {expected}")
    if any(flag is not True for flag in block.column("position_record_mask")):
        problems.append("位置记录 mask 必须全 true")
    if set(block.column("absolute_position")) & {1, 2, 4}:
        problems.append("出现了未被声明的推断位置 1/2/4")
    if block.n != 2:
        problems.append(f"位置记录数 {block.n} != 2（位置记录不增加卡牌数）")
    return _outcome(SUITE_TENSORIZE, "combat/pile-positions", not problems, "；".join(problems))


def _check_derived_relations(context: _Context) -> Outcome:
    """派生 net:pile_member 行与 wire 关系行可区分；无位置声明的关系 position_mask=false。"""
    document = snapshot("combat")
    manifest = context.manifest("combat")
    observation = context.encoded("combat")
    order = expected_entity_order(document)
    row_of = {instance: row for row, instance in enumerate(order)}
    block = observation.blocks["relations"]
    problems = []
    # 派生 net:pile_member 的条数＝所有牌堆 visible_members 之和（夹具当前为 1+5+2=8），
    # 由夹具算出而不是硬编码（旧值 3 是过期期望）。
    visible_total = sum(len(pile["visible_members"]) for pile in document["piles"])
    if block.n != len(document["relations"]) + visible_total:
        problems.append(f"关系行数 {block.n} != {len(document['relations']) + visible_total}")
    names = [manifest.dictionaries["relation_types"].name_of(index) for index in block.column("type_id")]
    derived = [index for index, name in enumerate(names) if name == "net:pile_member"]
    if len(derived) != visible_total:
        problems.append(f"net:pile_member 行数 {len(derived)} != {visible_total}")
    for index in derived:
        row = block.row(index)
        if row["position_mask"] is not False or row["position"] != 0:
            problems.append(f"派生成员关系不得带位置: {row}")
        if row["relation_mask"] is not True:
            problems.append(f"派生成员关系 mask 必须 true: {row}")
    for index, name in enumerate(names):
        if name in RELATIONS_WITHOUT_POSITION and block.column("position_mask")[index] is not False:
            problems.append(f"{name} 未声明位置，position_mask 必须 false")
    pairs = {
        (names[index], block.column("source")[index], block.column("target")[index]) for index in derived
    }
    expected = {
        ("net:pile_member", row_of[pile["pile_ref"]], row_of[member])
        for pile in document["piles"]
        for member in pile["visible_members"]
    }
    if pairs != expected:
        problems.append(f"派生成员对 {sorted(pairs)} != 手算 {sorted(expected)}")
    return _outcome(SUITE_TENSORIZE, "combat/derived-pile-member-relations", not problems, "；".join(problems))


def _check_sequences(context: _Context) -> Outcome:
    """观察序列保留顺序：ordinal 从 0 起连续，element 指向本样本行。"""
    document = snapshot("scenario")
    observation = context.encoded("scenario")
    order = expected_entity_order(document)
    row_of = {instance: row for row, instance in enumerate(order)}
    sequence = document["sequences"][0]
    block = observation.blocks["sequences"]
    problems = []
    if block.n != len(sequence["elements"]):
        problems.append(f"序列行数 {block.n} != {len(sequence['elements'])}")
    if list(block.column("ordinal")) != list(range(len(sequence["elements"]))):
        problems.append(f"ordinal 必须是 0..n-1：{list(block.column('ordinal'))}")
    if list(block.column("element")) != [row_of[element] for element in sequence["elements"]]:
        problems.append("element 未按观察顺序映射到本样本实体行")
    if len(set(block.column("element"))) != block.n:
        problems.append("序列元素必须是不同的实体行")
    if any(flag is not True for flag in block.column("sequence_record_mask")):
        problems.append("序列记录 mask 必须全 true")
    return _outcome(SUITE_TENSORIZE, "scenario/sequence-ordinal", not problems, "；".join(problems))


def _check_groups(context: _Context) -> Outcome:
    """无序组：成员按本样本行号排序，multiplicity 保留。"""
    document = snapshot("scenario")
    observation = context.encoded("scenario")
    order = expected_entity_order(document)
    row_of = {instance: row for row, instance in enumerate(order)}
    group = document["groups"][0]
    expected = sorted(
        (row_of[member["entity_ref"]], member["multiplicity"]) for member in group["members"]
    )
    block = observation.blocks["groups"]
    got = list(zip(block.column("member"), block.column("multiplicity")))
    problems = []
    if got != expected:
        problems.append(f"组成员行 {got} != 手算 {expected}（无序成员按公开引用排序）")
    if any(flag is not True for flag in block.column("group_record_mask")):
        problems.append("组记录 mask 必须全 true")
    return _outcome(SUITE_TENSORIZE, "scenario/group-membership", not problems, "；".join(problems))


def _check_candidates(context: _Context) -> Outcome:
    """候选保留 wire 顺序与句柄映射；候选字段指向实体行；句柄只在侧信息。"""
    document = snapshot("card_choice")
    manifest = context.manifest("card_choice")
    observation = context.encoded("card_choice")
    order = expected_entity_order(document)
    row_of = {instance: row for row, instance in enumerate(order)}
    candidates = observation.blocks["candidates"]
    candidate_fields = observation.blocks["candidate_fields"]
    problems = []
    if candidates.n != len(document["legal_actions"]):
        problems.append(f"候选行数 {candidates.n} != {len(document['legal_actions'])}")
    if any(flag is not True for flag in candidates.column("candidate_mask")):
        problems.append("候选 mask 必须全 true（非终止快照）")
    if list(candidates.column("action_type")) != [
        _manifest_index(manifest, "action_types", action["action_type"])
        for action in document["legal_actions"]
    ]:
        problems.append("候选顺序或 action_type 映射不符（编码器保留 wire 顺序）")
    if observation.reference_map.candidate_rows != {
        action["candidate_id"]: index for index, action in enumerate(document["legal_actions"])
    }:
        problems.append(f"候选句柄映射不符：{dict(observation.reference_map.candidate_rows)}")
    # 候选字段：owner_candidate 是 wire 下标；chosen_card_ref 指向 sel-0x 的本样本行。
    expected_fields = []
    for index, action in enumerate(document["legal_actions"]):
        for value_field in action["fields"]:
            expected_fields.append((index, value_field["field_id"], value_field))
    expected_fields.sort(key=lambda item: (item[0], item[1].encode("utf-8")))
    if candidate_fields.n != len(expected_fields):
        problems.append(f"候选字段行数 {candidate_fields.n} != {len(expected_fields)}")
    for index, (owner, field_id, value_field) in enumerate(expected_fields):
        row = candidate_fields.row(index)
        if row["owner_candidate"] != owner:
            problems.append(f"候选字段行{index} owner_candidate {row['owner_candidate']} != {owner}")
        if row["reference_entity"] != row_of[value_field["value"]] or row["reference_mask"] is not True:
            problems.append(f"候选字段行{index} 引用未指向本样本行（{field_id}）")
    return _outcome(SUITE_TENSORIZE, "card_choice/candidates-and-handles", not problems, "；".join(problems))


def _check_variables_states(context: _Context) -> Outcome:
    """扩展变量夹具：unknown/unavailable 与 not_applicable 可区分，不被当作零值。"""
    document = snapshot("variables")
    observation = context.encoded("variables")
    order = expected_entity_order(document)
    block = observation.blocks["entities"]
    problems = []
    states = {order[row]: int(block.column("content_state")[row]) for row in range(block.n)}
    if states.get("card-01") != EXPECTED_CONTENT_STATE["unknown"]:
        problems.append(f"card-01 content_state={states.get('card-01')} != unknown(2)")
    if states.get("card-02") != EXPECTED_CONTENT_STATE["unavailable"]:
        problems.append(f"card-02 content_state={states.get('card-02')} != unavailable(3)")
    if states.get("player-01") != EXPECTED_CONTENT_STATE["not_applicable"]:
        problems.append(f"player-01 content_state={states.get('player-01')} != not_applicable(4)")
    if any(block.column("content_mask")):
        problems.append("variables 夹具没有任何 known 内容，content_mask 必须全 false")
    fields = observation.blocks["fields"]
    for index in range(fields.n):
        row = fields.row(index)
        if row["knowledge_state"] in (2, 3):
            if row["reference_mask"] is not False or row["number_value"] != 0.0:
                problems.append(f"未知/不可提供的字段行值通道非零: {row}")
    return _outcome(SUITE_TENSORIZE, "variables/content-and-field-states", not problems, "；".join(problems))


# --------------------------------------------------------------------------- #
# 数值、确定性、可区分反例
# --------------------------------------------------------------------------- #
def _check_numeric_transform(context: _Context) -> Outcome:
    """number 通道 = 精确十进制 → 固定变换 → float64（手算 (3-1)/3）。"""
    observation = context.encoded("combat")
    manifest = context.manifest("combat")
    order = expected_entity_order(snapshot("combat"))
    row_of = {instance: row for row, instance in enumerate(order)}
    block = observation.blocks["fields"]
    energy_index = _manifest_index(manifest, "fields", "fixture:energy")
    exact = (Decimal("3") - Decimal("1")) / Decimal("3")
    problems = []
    found = False
    for index in range(block.n):
        row = block.row(index)
        if row["field_id"] == energy_index and row["owner_entity"] == row_of["player-01"]:
            found = True
            if row["number_value"] != float(exact):
                problems.append(f"energy 变换结果 {row['number_value']!r} != {float(exact)!r}")
            if not isinstance(row["number_value"], float):
                problems.append("number 通道必须是 float64")
    if not found:
        problems.append("没有找到 player-01 的 fixture:energy 行")
    return _outcome(SUITE_TENSORIZE, "combat/number-transform-exact", not problems, "；".join(problems))


def _check_precision_loss(context: _Context) -> Outcome:
    """容差内才接受 float64：把容差收到 1e-30 后 0.1 必须报 numeric_precision_loss。"""
    document = snapshot("combat")
    for entity in document["entities"]:
        if entity["instance_id"] == "enemy-01":
            for value_field in entity["fields"]:
                if value_field["field_id"] == "fixture:backend_damage_decimal":
                    value_field["state"] = "known"
                    value_field["value"] = "0.1"
    manifest = context.manifest(
        "combat",
        encoding_config=config_for("combat", tolerances={"absolute_tolerance": "1e-30", "relative_tolerance": "1e-30"}),
    )
    return _outcome(
        SUITE_TENSORIZE,
        "combat/numeric-precision-loss",
        *_expect_error(
            lambda: tensorize(document, manifest, catalog=catalog("combat")),
            ("numeric_precision_loss",
             "snapshot.entities[enemy-01].fields[fixture:backend_damage_decimal].value"),
        ),
    )


def _check_unregistered_content_type() -> Outcome:
    """反例：原始目录未声明这些内容类型 → unknown_type，且**不返回部分张量**。

    这是 A-04 与 A-01 fixtures 的分歧点：fixtures 的 ``content_id`` 用的是内容**定义**
    （fixture:strike…），而目录 ``content_types`` 只声明了内容**家族**
    （fixture:card_face/fixture:power_face）。按 §2 第 50 行"未注册类型…返回显式错误"
    与 §5 第 137 行"只有 known 具有有效内容索引"，这里必须拒绝，不得退化成索引 0 或 UNK。
    """
    manifest = manifest_for("combat", use_overlay=False)
    return _outcome(
        SUITE_TENSORIZE,
        "divergence/unregistered-content-id-rejected",
        *_expect_error(
            lambda: tensorize(snapshot("combat"), manifest, catalog=raw_catalog("combat")),
            ("unknown_type", "snapshot.entities[card-01].content_id"),
        ),
    )


def _check_missing_reserved_declaration() -> Outcome:
    """反例：目录缺三个保留声明时，**建 manifest 阶段**即被共享 L2（I-79）拒绝。

    实测行为比原期望更早也更强：缺 `net:pile_member`/`net:pile_total_count`/`net:pile_members_complete`
    的目录在 `build_manifest` 里就抛 `ContractViolation`（`invariants.md:33` 要求"使用 piles 的目录必须
    包含三个保留声明"，由共享 I-79 强制），因此不可能"先建出 manifest 再让 tensorize 失败"。
    """
    document = raw_catalog("combat")
    document["declarations"]["fields"] = [
        item for item in document["declarations"]["fields"] if not item["id"].startswith("net:pile_")
    ]
    document["declarations"]["relation_types"] = [
        item for item in document["declarations"]["relation_types"] if item["id"] != "net:pile_member"
    ]
    try:
        manifest_for("combat", catalog_document=document)
    except ContractViolation as violation:
        detail = str(violation)
        ok = "net:pile_member" in detail and "I-79" in detail
        return _outcome(SUITE_TENSORIZE, "content/reserved-pile-declarations-required", ok, detail[:300])
    return _outcome(
        SUITE_TENSORIZE, "content/reserved-pile-declarations-required", False,
        "缺保留声明的目录未被拒绝（应当由共享 I-79 在建 manifest 时拒绝）",
    )


def _check_hand_pile_positions() -> Outcome:
    """反例：无位置语义的牌堆带已知位置——共享 L3 与 A-03 都放过，A-04 必须拒绝。"""
    document = combat_hand_pile_with_positions()
    manifest = manifest_for("combat")
    report = validate_snapshot(document, manifest, catalog=catalog("combat"))
    problems = []
    if not report.ok:
        problems.append(f"前置不成立：A-03 已拒绝 {report.paths()}")
    try:
        tensorize(document, manifest, catalog=catalog("combat"))
        problems.append("未拒绝 hand 牌堆的已知位置")
    except EncodingError as exc:
        if (exc.code, exc.path) != ("invalid_position", "snapshot.piles[pile-hand].known_positions"):
            problems.append(f"抛出 {(exc.code, exc.path)!r}，期望 invalid_position @ snapshot.piles[pile-hand].known_positions")
    if problems:
        return _outcome(SUITE_TENSORIZE, "negative/hand-pile-known-positions", False, "；".join(problems))
    return _outcome(
        SUITE_TENSORIZE,
        "negative/hand-pile-known-positions",
        True,
        "A-03 ok 但 A-04 invalid_position（共享 L3 与 A-03 的缺口，见报告 §分歧 D-2）",
    )


def _check_relation_position_divergence() -> Outcome:
    """分歧：无位置语义的关系带 position → 共享 I-25 被 A-03 映射成 duplicate_identity。

    按 §5.1 第 172 行（"Relation 的 position 只有关系声明包含该语义时有效"）期望
    ``invalid_position``；实际由共享 I-25 → ``SHARED_RULE_CODES`` 得到
    ``duplicate_identity``。本检查只钉住"路径正确且确实被拒"，码的分歧见报告。
    """
    document = snapshot("combat")
    document["relations"][0]["position"] = 0
    manifest = manifest_for("combat")
    try:
        tensorize(document, manifest, catalog=catalog("combat"))
    except EncodingError as exc:
        want_path = "snapshot.relations[fixture:held_by].position"
        if exc.path != want_path:
            return _outcome(SUITE_TENSORIZE, "divergence/relation-position-code", False, f"路径 {exc.path!r} != {want_path!r}")
        if "I-25" not in exc.related_ids:
            return _outcome(SUITE_TENSORIZE, "divergence/relation-position-code", False, f"related_ids 未含 I-25：{exc.related_ids}")
        note = f"拒绝于 {exc.code} @ {exc.path}；按 §5.1 期望 invalid_position（分歧见报告 D-3）"
        return _outcome(SUITE_TENSORIZE, "divergence/relation-position-code", True, note)
    return _outcome(SUITE_TENSORIZE, "divergence/relation-position-code", False, "未拒绝带位置的 held_by 关系")


def _check_tensor_capacity(context: _Context) -> Outcome:
    """容量口径差异：A-03 按 wire 行数，A-04 按张量块行数（含派生行）。

    把 ``max_fields`` 设成 wire 字段行数：A-03 通过，A-04 必须报
    ``capacity_exceeded @ tensor.fields``——既不静默截断派生行，也不假装没超。
    """
    document = snapshot("combat")
    wire_fields = sum(len(entity["fields"]) for entity in document["entities"])
    capacities = dict(CAPACITIES)
    capacities["max_fields"] = wire_fields
    manifest = context.manifest("combat", encoding_config=config_for("combat", capacities=capacities))
    problems = []
    report = validate_snapshot(document, manifest, catalog=catalog("combat"))
    if not report.ok:
        problems.append(f"A-03 应当放过 wire 行数：{report.paths()}")
    ok, detail = _expect_error(
        lambda: tensorize(document, manifest, catalog=catalog("combat")),
        ("capacity_exceeded", "tensor.fields"),
    )
    if not ok:
        problems.append(detail)
    return _outcome(SUITE_TENSORIZE, "capacity/wire-vs-tensor-counting", not problems, "；".join(problems))


def check_determinism_and_statelessness(context: _Context) -> Outcome:
    """重复编码同一输入、以及"先编别的再编回来"都必须逐元素相等（无历史残留）。"""
    case_id = "tensorize/repeat-and-interleaved-are-identical"
    problems = []
    document = snapshot("combat")
    manifest = context.manifest("combat")
    first = tensorize(document, manifest, catalog=catalog("combat"))
    other = tensorize(snapshot("variables"), context.manifest("variables"), catalog=catalog("variables"))
    again = tensorize(document, manifest, catalog=catalog("combat"))
    for name in TENSOR_BLOCK_NAMES:
        if dict(first.blocks[name].columns) != dict(again.blocks[name].columns):
            problems.append(f"{name} 块在重复编码后不相等")
    if first.reference_map.to_wire() != again.reference_map.to_wire():
        problems.append("reference_map 在重复编码后不相等")
    if other.sizes["entities"] != len(snapshot("variables")["entities"]):
        problems.append("中间编码了别的样本，影响了自身结果")
    return _outcome(SUITE_TENSORIZE, case_id, not problems, "；".join(problems))


def check_permuted_wire_order_rejected(context: _Context) -> Outcome:
    """**反例（合法性下限）**：反转的 ``visible_members`` 必须被 L3/I-74 拒绝。

    §9 第 224/230 行的"成员重排"不是 wire 排列：``contract-artifacts.md`` L37 与
    ``wire.schema.json:1108-1110`` 要求 ``visible_members`` 按公开引用排序，反转后的
    文档是**非法 wire**。本用例钉住两件事：

    * 非法排列在**校验阶段**被拒（错误码 ``unsupported_structure`` + 精确路径），
      ``tensorize`` 也抛同一条、**不返回部分张量**；
    * 规范顺序（fixture 原样）仍被接受——否则上面的拒绝可能只是"什么都拒"。
    """
    case_id = "tensorize/permuted-wire-order-rejected"
    manifest = context.manifest("combat")
    catalog_document = catalog("combat")
    document = combat_members_reordered()
    wanted = ("unsupported_structure", "snapshot.piles[pile-draw].visible_members")
    problems = []
    report = validate_snapshot(document, manifest, catalog=catalog_document)
    if report.ok:
        problems.append("反转 visible_members 的非法 wire 未被拒绝")
    else:
        got = sorted((error.code, error.path) for error in report.errors)
        if got != [wanted]:
            problems.append(f"拒绝理由 {got} != {[wanted]}")
    ok, detail = _expect_error(
        lambda: tensorize(document, manifest, catalog=catalog_document), wanted
    )
    if not ok:
        problems.append(detail)
    if not validate_snapshot(snapshot("combat"), manifest, catalog=catalog_document).ok:
        problems.append("canary：规范顺序的 fixture 本身被拒绝，上面的拒绝无判别力")
    return _outcome(SUITE_TENSORIZE, case_id, not problems, "；".join(problems))


def check_hidden_order_not_encoded(context: _Context) -> Outcome:
    """§9 第 230 行（+224）：隐藏顺序不同、公开事实相同 → 特征数组逐元素相等。

    隐藏顺序在 wire 里**没有表示**（见 :data:`HIDDEN_DRAW_ORDERS`），所以"两份不同
    隐藏顺序的观察"经提供方写出后是**同一份规范字节**。本用例不用"先排序再校验"
    抹掉这个区别：排序发生在**检查侧的集合层构造**（:func:`observation_of_hidden_order`，
    即提供方义务），而"没排序的 wire 会被拒"由
    :func:`check_permuted_wire_order_rejected` 单独钉住。断言分三组：

    1. 三种手写隐藏顺序得到**逐字节相同**的 wire，且 8 个块的特征数组与
       ``reference_map`` 逐元素相等；
    2. 会话/观察身份字段（``session_id``/``observation_id``）不进特征：换身份后 8 个
       块仍逐元素相等，而 ``metadata`` 必须随之改变（否则本断言无判别力）；
    3. 输出里**没有**内部排列通道：draw 牌堆的成员关系行 ``position`` 列是 pad=0 且
       ``position_mask=false``，成员关系行顺序等于独立算出的公开键
       ``(source 行, target 行)``，已知位置仍只有公开声明的 0/3——把
       ``visible_members`` 的数组下标当位置/序位的实现会在这里失败。
    """
    case_id = "tensorize/hidden-order-not-encoded"
    manifest = context.manifest("combat")
    catalog_document = catalog("combat")
    base = snapshot("combat")
    base_bytes = layers.canonical_bytes(base)
    base_observation = tensorize(base, manifest, catalog=catalog_document)
    problems: list = []

    # 1) 隐藏顺序不是 wire 信息
    for hidden_order in HIDDEN_DRAW_ORDERS:
        variant = observation_of_hidden_order(hidden_order)
        if layers.canonical_bytes(variant) != base_bytes:
            problems.append(f"隐藏顺序 {hidden_order} 改变了 wire 规范字节（wire 不应表达隐藏顺序）")
        observation = tensorize(variant, manifest, catalog=catalog_document)
        for name in TENSOR_BLOCK_NAMES:
            if dict(observation.blocks[name].columns) != dict(base_observation.blocks[name].columns):
                problems.append(f"{name} 块随隐藏顺序 {hidden_order} 变化")
        if observation.reference_map.to_wire() != base_observation.reference_map.to_wire():
            problems.append(f"reference_map 随隐藏顺序 {hidden_order} 变化")

    # 2) 身份不是特征
    other = copy.deepcopy(base)
    other["session_id"] = "session-combat-2"
    other["observation_id"] = 99
    other_observation = tensorize(other, manifest, catalog=catalog_document)
    for name in TENSOR_BLOCK_NAMES:
        if dict(other_observation.blocks[name].columns) != dict(base_observation.blocks[name].columns):
            problems.append(f"{name} 块随 session/observation 身份变化（身份不是特征）")
    if dict(other_observation.metadata) == dict(base_observation.metadata):
        problems.append("canary：换身份后 metadata 未变，第 2 组断言无判别力")

    # 3) 内部排列没有特征通道（独立重算公开事实）
    order_of = expected_entity_order(base)
    row_of = {instance: row for row, instance in enumerate(order_of)}
    relations = base_observation.blocks["relations"]
    member_rows = [
        relations.row(index)
        for index in range(relations.n)
        if manifest.dictionaries["relation_types"].name_of(relations.column("type_id")[index])
        == "net:pile_member"
    ]
    want_member_pairs = sorted(
        (row_of[pile["pile_ref"]], row_of[member])
        for pile in base["piles"]
        for member in pile["visible_members"]
    )
    got_member_pairs = [(row["source"], row["target"]) for row in member_rows]
    if got_member_pairs != want_member_pairs:
        problems.append(f"成员关系行 {got_member_pairs} != 独立算出的公开键顺序 {want_member_pairs}")
    if any(row["position_mask"] is not False or row["position"] != 0 for row in member_rows):
        problems.append("成员关系行的 position 列不是 pad=0 且 position_mask=false（内部排列不得成为位置）")
    draw_row = row_of["pile-draw"]
    positions = base_observation.blocks["pile_positions"]
    draw_positions = [
        positions.column("absolute_position")[index]
        for index in range(positions.n)
        if positions.column("pile_entity")[index] == draw_row
    ]
    if sorted(draw_positions) != [0, 3]:
        problems.append(f"draw 牌堆的位置特征 {draw_positions} != 公开声明的 [0, 3]")
    if positions.n != 2:
        problems.append(f"位置记录数 {positions.n} != 2（内部排列不得产生额外位置行）")
    return _outcome(SUITE_TENSORIZE, case_id, not problems, "；".join(problems[:6]))


def check_rename_invariance(context: _Context) -> Outcome:
    """§9：实例 ID 一致重命名后引用仍正确（按引用映射对齐比较，不比原始行序）。"""
    case_id = "tensorize/consistent-rename-keeps-references"
    manifest = context.manifest("combat")
    renamed_document = renamed_instances(snapshot("combat"), lambda text: "x-" + text)
    renamed = tensorize(renamed_document, manifest, catalog=catalog("combat"))
    problems = []
    for old, row in renamed.reference_map.entity_rows.items():
        if not old.startswith("x-"):
            problems.append(f"实例未重命名：{old}")
            break
    # 候选引用必须解析到重命名后的实例，且与原始样本语义一致。
    original = tensorize(snapshot("combat"), manifest, catalog=catalog("combat"))
    original_refs = {
        original.reference_map.candidate_of_row(index): original.reference_map.instance_of_row(
            original.blocks["candidate_fields"].column("reference_entity")[index]
        )
        for index in range(original.blocks["candidate_fields"].n)
    }
    renamed_refs = {
        renamed.reference_map.candidate_of_row(index): renamed.reference_map.instance_of_row(
            renamed.blocks["candidate_fields"].column("reference_entity")[index]
        )
        for index in range(renamed.blocks["candidate_fields"].n)
    }
    expected = {key: "x-" + value for key, value in original_refs.items()}
    if renamed_refs != expected:
        problems.append(f"重命名后候选引用 {renamed_refs} != {expected}")
    return _outcome(SUITE_TENSORIZE, case_id, not problems, "；".join(problems))


def check_validation_failure_returns_nothing(context: _Context) -> Outcome:
    """校验失败不产生部分编码：悬空引用由 A-03 拒绝，tensorize 抛同一条错误。"""
    case_id = "negative/dangling-reference-no-partial-tensor"
    document = snapshot("combat")
    document["legal_actions"][1]["fields"][0]["value"] = "card-99"
    manifest = context.manifest("combat")
    return _outcome(
        SUITE_TENSORIZE,
        case_id,
        *_expect_error(
            lambda: tensorize(document, manifest, catalog=catalog("combat")),
            ("dangling_reference", "snapshot.legal_actions[cand-play-06].fields[fixture:play_card_ref].value"),
        ),
    )


def check_skipped_is_recorded(context: _Context) -> Outcome:
    """无目录时不假装通过：未判定的检查名必须出现在 validation_skipped 里。"""
    case_id = "tensorize/catalog-less-skips-recorded"
    manifest = context.manifest("combat")
    with_catalog = tensorize(snapshot("combat"), manifest, catalog=catalog("combat"))
    without = tensorize(snapshot("combat"), manifest, catalog=None)
    problems = []
    # validation_skipped 里既有字符串也有 (名字, 规则号, 路径) 元组 → 统一按字符串判定子串。
    if any("position-schema" in str(item) for item in with_catalog.validation_skipped):
        problems.append("给了目录却仍标注位置语义未判定")
    if not any("position-schema" in str(item) for item in without.validation_skipped):
        problems.append(f"无目录时未记录未判定项：{without.validation_skipped}")
    if dict(without.blocks["entities"].columns) != dict(with_catalog.blocks["entities"].columns):
        problems.append("有无目录不应改变实体块（只影响判定记录）")
    return _outcome(SUITE_TENSORIZE, case_id, not problems, "；".join(problems))


# --------------------------------------------------------------------------- #
# collate
# --------------------------------------------------------------------------- #
def _pair() -> tuple:
    """同一 Manifest/目录下的两个样本：原样战斗快照 + 少两张牌的变体。"""
    manifest = manifest_for("combat")
    document = catalog("combat")
    first = tensorize(snapshot("combat"), manifest, catalog=document)
    second = tensorize(combat_without_first_two_cards(), manifest, catalog=document)
    return manifest, first, second


def check_batch_shape() -> Outcome:
    """B 轴形状：行数=本批最大，lengths 记真实行数，块内列长一致。"""
    case_id = "collate/batch-shape-and-lengths"
    manifest, first, second = _pair()
    batch = collate([first, second])
    problems = []
    expected_lengths = {
        name: (first.sizes[name], second.sizes[name]) for name in TENSOR_BLOCK_NAMES
    }
    for name in TENSOR_BLOCK_NAMES:
        block = batch.block(name)
        if block.lengths != expected_lengths[name]:
            problems.append(f"{name}.lengths {block.lengths} != {expected_lengths[name]}")
        want_rows = max(expected_lengths[name])
        if block.rows != want_rows:
            problems.append(f"{name}.rows {block.rows} != {want_rows}")
        for column, values in block.columns.items():
            if any(len(sample) != block.rows for sample in values):
                problems.append(f"{name}.{column} 列长与本批行数不一致")
    if batch.batch_size != 2:
        problems.append(f"batch_size {batch.batch_size} != 2")
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems))


def check_cross_sample_same_slot() -> Outcome:
    """**头号反例**：同槽位不同实体、同一实例在不同样本的不同行，都不得互相解析。

    手算（按 instance_id 排序，删掉 card-01/card-02 后）：
    样本 0 行 0=card-01、行 5=card-06；样本 1 行 0=card-03、行 3=card-06、行 5=card-08。
    候选 cand-play-06 的 ``fixture:play_card_ref`` 在两个样本里都指 card-06，但局部行号
    分别是 5 与 3；若用"另一份样本的行号"或"全局偏移"解引用就会得到 card-04 / card-08。
    """
    case_id = "collate/cross-sample-same-slot-not-crosslinked"
    manifest, first, second = _pair()
    batch = collate([first, second])
    problems = []
    if batch.entity_instance(0, 0) != "card-01" or batch.entity_instance(1, 0) != "card-03":
        problems.append(
            f"行 0 应为 card-01/card-03，得到 {batch.entity_instance(0, 0)}/{batch.entity_instance(1, 0)}"
        )
    if batch.entity_instance(0, 5) != "card-06" or batch.entity_instance(1, 5) != "card-08":
        problems.append(
            f"行 5 应为 card-06/card-08，得到 {batch.entity_instance(0, 5)}/{batch.entity_instance(1, 5)}"
        )
    if batch.entity_instance(0, 5) == batch.entity_instance(1, 5):
        problems.append("两个样本的第 5 行不应是同一实例（否则测试无判别力）")
    # 同一候选在两个样本里的引用必须是各自的局部行号，且都能解析成 card-06。
    row_of_candidate = {0: None, 1: None}
    for b in (0, 1):
        block = batch.block("candidate_fields")
        for index in range(block.lengths[b]):
            row = block.row(b, index)
            if batch.candidate_id(b, row["owner_candidate"]) == "cand-play-06" and manifest.dictionaries[
                "fields"
            ].name_of(row["field_id"]) == "fixture:play_card_ref":
                row_of_candidate[b] = row["reference_entity"]
    if row_of_candidate[0] != 5 or row_of_candidate[1] != 3:
        problems.append(f"cand-play-06 的局部引用应为 5/3，得到 {row_of_candidate}")
    for b, expected_row in ((0, 5), (1, 3)):
        if batch.resolve_entity(b, expected_row) != "card-06":
            problems.append(f"样本 {b} 行 {expected_row} 应解析为 card-06")
    if batch.resolve_entity(1, 5) != "card-08":
        problems.append("样本 1 的行 5 是 card-08，绝不能被当成 card-06")
    # 全局偏移写法必须被显式拒绝（越界），证明没有跨样本索引空间。
    ok, _ = _expect_error(
        lambda: batch.resolve_entity(1, 16 + 3), ("dangling_reference", "samples[1].entities[19]")
    )
    if not ok:
        problems.append("全局偏移行号未被拒绝")
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems))


def check_padding_rows() -> Outcome:
    """补齐行：mask=false、状态/类型列 pad=0、索引列不被解引用成第 0 行。"""
    case_id = "collate/padding-rows-are-not-row-zero"
    manifest, first, second = _pair()
    batch = collate([first, second])
    problems = []
    entities = batch.block("entities")
    for index in range(entities.lengths[1], entities.rows):
        if entities.mask(1)[index] is not False:
            problems.append(f"entities[1][{index}] 补齐行 mask 必须 false")
        row = entities.row(1, index)
        if row["kind_id"] != 0 or row["content_state"] != 0 or row["content_mask"] is not False:
            problems.append(f"entities[1][{index}] 补齐行必须是 pad=0 且 content_mask=false")
        if batch.resolve_row("entities", 1, index) is not None:
            problems.append(f"entities[1][{index}] 补齐行不得被解析")
    fields = batch.block("fields")
    for index in range(fields.lengths[1], fields.rows):
        row = fields.row(1, index)
        if fields.mask(1)[index] is not False:
            problems.append(f"fields[1][{index}] 补齐行 mask 必须 false")
        if row["owner_entity"] != 0 or row["knowledge_state"] != 0 or row["value_type"] != 0:
            problems.append(f"fields[1][{index}] 补齐行必须是 owner=0/knowledge_state=0/value_type=0")
        if row["reference_mask"] is not False or row["reference_entity"] != 0:
            problems.append(f"fields[1][{index}] 补齐行不得是有效引用")
        if batch.resolve_row("fields", 1, index) is not None:
            problems.append(
                f"fields[1][{index}] 补齐行的 owner_entity=0 不得解析成样本 1 的第 0 行实体"
            )
    # 反向对照：样本 1 的真实第 0 行必须能解析（否则上面的 None 无判别力）。
    resolved = batch.resolve_row("fields", 1, 0)
    if not resolved or resolved.get("owner_entity") != "card-03":
        problems.append(f"样本 1 真实字段行的 owner 应解析为 card-03，得到 {resolved}")
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems))


def check_terminal_sample_padding() -> Outcome:
    """终止样本：candidate_mask 全 false（且候选块全为补齐行），不与决策样本串候选。"""
    case_id = "collate/terminal-candidates-all-false"
    manifest = manifest_for("scenario")
    document = catalog("scenario")
    decision = tensorize(snapshot("scenario"), manifest, catalog=document)
    terminal = tensorize(snapshot("scenario", "snapshot-terminal"), manifest, catalog=document)
    batch = collate([decision, terminal])
    problems = []
    candidates = batch.block("candidates")
    if candidates.lengths != (decision.sizes["candidates"], 0):
        problems.append(f"候选长度 {candidates.lengths} != {(decision.sizes['candidates'], 0)}")
    if any(candidates.mask(1)):
        problems.append("终止样本 candidate_mask 必须全 false")
    for index in range(candidates.rows):
        row = candidates.row(1, index)
        if row["action_type"] != 0:
            problems.append(f"终止样本候选补齐行 action_type 必须是 pad=0：{row}")
    candidate_fields = batch.block("candidate_fields")
    for index in range(candidate_fields.rows):
        if batch.resolve_row("candidate_fields", 1, index) is not None:
            problems.append(f"终止样本的候选字段补齐行不得解析：行 {index}")
    if batch.candidate_id(1, 0) is not None:
        problems.append("终止样本不应有候选句柄")
    if batch.candidate_id(0, 0) != "n-a":
        problems.append(f"决策样本候选句柄应保留 wire 顺序，得到 {batch.candidate_id(0, 0)}")
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems))


def check_mixed_manifest_rejected() -> Outcome:
    """不同 Manifest（跨 bundle）禁止混批，点名第一个不同的身份分量。"""
    case_id = "collate/mixed-manifest-rejected"
    combat = tensorize(snapshot("combat"), manifest_for("combat"), catalog=catalog("combat"))
    choice = tensorize(snapshot("card_choice"), manifest_for("card_choice"), catalog=catalog("card_choice"))
    ok, detail = _expect_error(
        lambda: collate([combat, choice]), ("version_mismatch", "samples[1].metadata")
    )
    return _outcome(SUITE_COLLATE, case_id, ok, detail)


def check_mixed_config_rejected() -> Outcome:
    """同一目录、不同数值配置（变换不同 → encoding_config_digest 不同）禁止混批。"""
    case_id = "collate/mixed-encoding-config-rejected"
    document = catalog("combat")
    base = tensorize(snapshot("combat"), manifest_for("combat", catalog_document=document), catalog=document)
    other_config = EncodingConfig(
        transforms={"fixture:energy": NumericTransform(offset="0", scale="1")},
        capacities=CapacityLimits(**CAPACITIES),
    )
    other_manifest = manifest_for("combat", catalog_document=document, encoding_config=other_config)
    other = tensorize(snapshot("combat"), other_manifest, catalog=document)
    if base.compat_key() == other.compat_key():
        return _outcome(SUITE_COLLATE, case_id, False, "两份不同数值配置的身份相同（测试无判别力）")
    ok, detail = _expect_error(lambda: collate([base, other]), ("version_mismatch", "samples[1].metadata"))
    return _outcome(SUITE_COLLATE, case_id, ok, detail)


def check_reference_maps_are_per_sample() -> Outcome:
    """引用映射逐样本各存一份：不共享槽位、不跨样本复用；长度与 B 一致。"""
    case_id = "collate/reference-map-per-sample"
    manifest, first, second = _pair()
    batch = collate([first, second])
    problems = []
    if len(batch.reference_maps) != 2:
        problems.append(f"reference_maps 长度 {len(batch.reference_maps)} != 2")
    if not all(isinstance(mapping, ReferenceMap) for mapping in batch.reference_maps):
        problems.append("reference_maps 元素必须是 ReferenceMap")
    if batch.reference_map(0) is batch.reference_map(1):
        problems.append("两个样本共用了同一个映射对象")
    if batch.reference_map(0).entity_rows == batch.reference_map(1).entity_rows:
        problems.append("两个样本的实体行映射不应相同（否则测试无判别力）")
    if batch.reference_map(1).row_of_instance("card-06") != 3:
        problems.append(f"样本 1 的 card-06 行号应为 3，得到 {batch.reference_map(1).row_of_instance('card-06')}")
    if batch.compat_key() != first.compat_key():
        problems.append("批身份必须等于样本身份")
    if dict(batch.metadata) != dict(first.metadata):
        problems.append("批 metadata 必须等于样本 metadata")
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems))


def check_padding_dtypes_and_enums() -> Outcome:
    """补齐不改变 dtype：补齐值 = int 0 / bool False / float 0.0（逐块逐列审计）。"""
    case_id = "collate/padding-dtypes-and-enums"
    manifest, first, second = _pair()
    batch = collate([first, second])
    problems = []
    schema = dict(EXPECTED_SCHEMA)
    for name in TENSOR_BLOCK_NAMES:
        block = batch.block(name)
        # _dtype_violations 会遍历该块的全部列，因此必须一次传**整块**的展平列集，
        # 而不是逐列传单列字典（旧写法会在取下一列时 KeyError）。
        flattened = {
            column: [value for sample in block.column(column) for value in sample]
            for column, _ in schema[name]
        }
        for column, dtype in schema[name]:
            values = block.column(column)
            for b in range(block.batch_size):
                for index in range(block.lengths[b], block.rows):
                    value = values[b][index]
                    if dtype == "bool":
                        wanted = False
                    elif dtype == "int64":
                        wanted = 0
                    else:
                        wanted = 0.0
                    if value != wanted or (dtype != "float64" and type(value) is not type(wanted)):
                        problems.append(f"{name}.{column}[{b}][{index}]={value!r} != {wanted!r}({dtype})")
        problems.extend(_dtype_violations(name, flattened))
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems[:6]))


def check_reference_mask_only_known_entity_ref() -> Outcome:
    """``reference_mask`` 只在 known 的 entity_ref 行上为真。

    两半证据：① 反例样本（把 card-02 的 entity_ref 字段 ``fixture:owner`` 改成
    ``state=unknown``，A-03 仍然接受）——该行必须 ``reference_mask=false``、
    ``reference_entity=0``、``knowledge_state=2``；② 对全部 bundle 的扫描——凡
    ``reference_mask=true`` 的行必须同时是 entity_ref 且 known，且本套件确实存在
    known entity_ref 行（否则检查无判别力）。
    """
    case_id = "collate/reference-mask-only-known-entity-ref"
    problems = []
    document = snapshot("combat")
    for entity in document["entities"]:
        if entity["instance_id"] != "card-02":
            continue
        entity["fields"] = [
            {"field_id": "fixture:owner", "state": "unknown"}
            if item["field_id"] == "fixture:owner"
            else item
            for item in entity["fields"]
        ]
    manifest = manifest_for("combat")
    if not validate_snapshot(document, manifest, catalog=catalog("combat")).ok:
        problems.append("反例样本被 A-03 拒绝，无法证明 reference_mask 的判定")
    else:
        observation = tensorize(document, manifest, catalog=catalog("combat"))
        fields = observation.blocks["fields"]
        owner_index = _manifest_index(manifest, "fields", "fixture:owner")
        card02_row = expected_entity_order(document).index("card-02")
        hit = False
        for index in range(fields.n):
            row = fields.row(index)
            if row["owner_entity"] == card02_row and row["field_id"] == owner_index:
                hit = True
                if row["knowledge_state"] != EXPECTED_KNOWLEDGE_STATE["unknown"]:
                    problems.append(f"unknown 行状态序数不符：{row}")
                if row["reference_mask"] is not False or row["reference_entity"] != 0:
                    problems.append(f"unknown 的 entity_ref 置了引用：{row}")
                if row["number_value"] != 0.0:
                    problems.append(f"unknown 行 number 通道非零：{row}")
        if not hit:
            problems.append("反例字段行缺失")

    known_refs = 0
    for bundle, stem in BUNDLE_CASES:
        found = tensorize(snapshot(bundle, stem), manifest_for(bundle), catalog=catalog(bundle))
        fields = found.blocks["fields"]
        for index in range(fields.n):
            row = fields.row(index)
            if row["reference_mask"] is not True:
                continue
            known_refs += 1
            if row["value_type"] != EXPECTED_VALUE_TYPE["entity_ref"]:
                problems.append(f"{bundle}/{stem} 行{index} reference_mask=true 但 value_type 不是 entity_ref")
            if row["knowledge_state"] != EXPECTED_KNOWLEDGE_STATE["known"]:
                problems.append(f"{bundle}/{stem} 行{index} reference_mask=true 但知识状态不是 known")
    if known_refs == 0:
        problems.append("没有 known 的 entity_ref 行，reference_mask 检查缺少正向样本")
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems[:6]))


def check_out_of_range_rejected() -> Outcome:
    """索引解引用越界即拒绝：样本下标越界、行号越界都给路径。"""
    case_id = "collate/out-of-range-index-rejected"
    manifest, first, second = _pair()
    batch = collate([first, second])
    problems = []
    for run, wanted in (
        (lambda: batch.resolve_entity(1, 99), ("dangling_reference", "samples[1].entities[99]")),
        (lambda: batch.resolve_entity(5, 0), ("dangling_reference", "samples[5]")),
        (lambda: batch.resolve_row("fields", 1, 999), ("dangling_reference", "samples[1].fields[999]")),
        (lambda: batch.resolve_candidate(0, 99), ("dangling_reference", "samples[0].candidates[99]")),
    ):
        ok, detail = _expect_error(run, wanted)
        if not ok:
            problems.append(detail)
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems))


def check_collate_rejects_bad_input() -> Outcome:
    """空批、非样本元素、非序列实参都必须显式失败（不静默跳过）。"""
    case_id = "collate/rejects-empty-and-bad-elements"
    manifest, first, _ = _pair()
    problems = []
    for run, wanted in (
        (lambda: collate([]), ("unsupported_structure", "samples")),
        (lambda: collate([first, "not-a-sample"]), ("unsupported_structure", "samples[1]")),
        (lambda: collate(42), ("unsupported_structure", "samples")),
    ):
        ok, detail = _expect_error(run, wanted)
        if not ok:
            problems.append(detail)
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems))


def check_collate_capacity_before_padding() -> Outcome:
    """拼批侧在补齐前复核容量：把样本 metadata 的 max_fields 收窄即拒绝。"""
    case_id = "collate/capacity-rechecked-before-padding"
    manifest, first, second = _pair()
    tightened = dict(first.metadata)
    capacities = dict(tightened["capacities"])
    capacities["max_fields"] = 1
    tightened["capacities"] = capacities
    import dataclasses

    broken = dataclasses.replace(first, metadata=tightened)
    ok, detail = _expect_error(
        lambda: collate([broken, second]), ("capacity_exceeded", "samples[0].tensor.fields")
    )
    return _outcome(SUITE_COLLATE, case_id, ok, detail)


def check_blocks_remain_local_under_padding() -> Outcome:
    """补齐后真实行的索引列仍指向本样本行号（不因补齐而整体偏移）。"""
    case_id = "collate/true-rows-keep-local-indices"
    manifest, first, second = _pair()
    batch = collate([first, second])
    problems = []
    for b, sample in ((0, first), (1, second)):
        for block in ("fields", "relations", "pile_positions", "sequences", "groups", "candidate_fields"):
            source = sample.blocks[block]
            target = batch.block(block)
            for column in INDEX_COLUMNS[block]:
                if list(target.column(column)[b][: source.n]) != list(source.column(column)):
                    problems.append(f"{block}.{column} 在样本 {b} 上被改写")
    for b in (0, 1):
        for index in range(batch.block("fields").lengths[b]):
            resolved = batch.resolve_row("fields", b, index)
            owner_row = batch.block("fields").column("owner_entity")[b][index]
            if resolved["owner_entity"] != batch.entity_instance(b, owner_row):
                problems.append(f"样本 {b} 字段行 {index} 的 owner 解析与实体表不一致")
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems[:6]))


def check_three_sample_padding_isolation() -> Outcome:
    """监督者要求的"3 个样本、行数各异"批：每个样本的补齐槽只属于它自己。"""
    case_id = "collate/three-samples-varied-lengths"
    manifest = manifest_for("combat")
    document = catalog("combat")
    full = tensorize(snapshot("combat"), manifest, catalog=document)
    trimmed = tensorize(combat_without_first_two_cards(), manifest, catalog=document)
    terminal = tensorize(snapshot("scenario", "snapshot-terminal"), manifest_for("scenario"), catalog=catalog("scenario"))
    problems = []
    # 第三个样本换了 Manifest，本来必须被拒——先证明拒了，再用同 Manifest 的三样本批。
    ok, _ = _expect_error(lambda: collate([full, trimmed, terminal]), ("version_mismatch", "samples[2].metadata"))
    if not ok:
        problems.append("跨 Manifest 的第三个样本未被拒绝")
    small_document = snapshot("combat")
    small_document["entities"] = [
        entity for entity in small_document["entities"] if entity["instance_id"] in ("player-01", "enemy-01")
    ]
    small_document["relations"] = []
    small_document["piles"] = []
    small_document["legal_actions"] = [
        action for action in small_document["legal_actions"] if action["candidate_id"] == "cand-end"
    ]
    small = tensorize(small_document, manifest, catalog=document)
    batch = collate([full, trimmed, small])
    if batch.batch_size != 3:
        problems.append(f"batch_size {batch.batch_size} != 3")
    if batch.entity_instance(0, 0) != "card-01" or batch.entity_instance(2, 0) != "enemy-01":
        problems.append(
            f"行 0 应为 card-01/enemy-01，得到 {batch.entity_instance(0, 0)}/{batch.entity_instance(2, 0)}"
        )
    entities = batch.block("entities")
    for b in (1, 2):
        for index in range(entities.lengths[b], entities.rows):
            if entities.mask(b)[index] is not False:
                problems.append(f"entities[{b}][{index}] 补齐行 mask=false 必须成立")
            if batch.entity_instance(b, index) is not None:
                problems.append(f"entities[{b}][{index}] 补齐行不得解析出实例")
    # 样本 2 只有 2 个实体：它的第 0/1 行，和样本 0 的第 0/1 行（card-01/card-02）不同。
    if batch.entity_instance(2, 0) == batch.entity_instance(0, 0):
        problems.append("样本 2 与样本 0 的第 0 行不应是同一实例（判别力）")
    if batch.entity_instance(2, 1) == batch.entity_instance(0, 1):
        problems.append("样本 2 与样本 0 的第 1 行不应是同一实例（判别力）")
    return _outcome(SUITE_COLLATE, case_id, not problems, "；".join(problems[:6]))


# --------------------------------------------------------------------------- #
# 入口
# --------------------------------------------------------------------------- #
def _tensorize_cases(context: _Context) -> list:
    outcomes = [_guard(SUITE_TENSORIZE, "schema/blocks-columns-dtypes-enums", check_schema_surface)]
    for bundle, stem in BUNDLE_CASES:
        outcomes.append(
            _guard(SUITE_TENSORIZE, f"{bundle}/{stem}/sizes", lambda b=bundle, s=stem: _check_sizes(context, b, s))
        )
        outcomes.append(
            _guard(
                SUITE_TENSORIZE,
                f"{bundle}/{stem}/entity-rows",
                lambda b=bundle, s=stem: _check_entity_rows(context, b, s),
            )
        )
        outcomes.append(
            _guard(
                SUITE_TENSORIZE,
                f"{bundle}/{stem}/field-rows",
                lambda b=bundle, s=stem: _check_field_rows(context, b, s),
            )
        )
    for case_id, run in (
        ("combat/derived-pile-fields", lambda: _check_derived_piles(context)),
        ("combat/pile-positions", lambda: _check_pile_positions(context)),
        ("combat/derived-pile-member-relations", lambda: _check_derived_relations(context)),
        ("scenario/sequence-ordinal", lambda: _check_sequences(context)),
        ("scenario/group-membership", lambda: _check_groups(context)),
        ("card_choice/candidates-and-handles", lambda: _check_candidates(context)),
        ("variables/content-and-field-states", lambda: _check_variables_states(context)),
        ("combat/number-transform-exact", lambda: _check_numeric_transform(context)),
        ("combat/numeric-precision-loss", lambda: _check_precision_loss(context)),
        ("capacity/wire-vs-tensor-counting", lambda: _check_tensor_capacity(context)),
    ):
        outcomes.append(_guard(SUITE_TENSORIZE, case_id, run))
    # 直接注册的检查也走 ``_guard``：单个夹具/用例出问题只算一条失败，
    # 不允许把整个套件拖成"提前中止"（前任的失败模式）。
    outcomes.append(_guard(SUITE_TENSORIZE, "tensorize/repeat-and-interleaved-are-identical", lambda: check_determinism_and_statelessness(context)))
    outcomes.append(_guard(SUITE_TENSORIZE, "tensorize/permuted-wire-order-rejected", lambda: check_permuted_wire_order_rejected(context)))
    outcomes.append(_guard(SUITE_TENSORIZE, "tensorize/hidden-order-not-encoded", lambda: check_hidden_order_not_encoded(context)))
    outcomes.append(_guard(SUITE_TENSORIZE, "tensorize/consistent-rename-keeps-references", lambda: check_rename_invariance(context)))
    outcomes.append(_guard(SUITE_TENSORIZE, "negative/dangling-reference-no-partial-tensor", lambda: check_validation_failure_returns_nothing(context)))
    outcomes.append(_guard(SUITE_TENSORIZE, "tensorize/catalog-less-skips-recorded", lambda: check_skipped_is_recorded(context)))
    outcomes.append(_guard(SUITE_TENSORIZE, "content/unregistered-content-type-rejected", _check_unregistered_content_type))
    outcomes.append(_guard(SUITE_TENSORIZE, "content/reserved-pile-declarations-required", _check_missing_reserved_declaration))
    outcomes.append(_guard(SUITE_TENSORIZE, "combat/hand-pile-positions-not-applicable", _check_hand_pile_positions))
    outcomes.append(_guard(SUITE_TENSORIZE, "relations/position-only-when-declared", _check_relation_position_divergence))
    return outcomes


def _collate_cases(context: _Context) -> list:
    return [
        _guard(SUITE_COLLATE, "collate/batch-shape-and-lengths", check_batch_shape),
        _guard(SUITE_COLLATE, "collate/cross-sample-same-slot-not-crosslinked", check_cross_sample_same_slot),
        _guard(SUITE_COLLATE, "collate/padding-rows-are-not-row-zero", check_padding_rows),
        _guard(SUITE_COLLATE, "collate/terminal-candidates-all-false", check_terminal_sample_padding),
        _guard(SUITE_COLLATE, "collate/mixed-manifest-rejected", check_mixed_manifest_rejected),
        _guard(SUITE_COLLATE, "collate/mixed-encoding-config-rejected", check_mixed_config_rejected),
        _guard(SUITE_COLLATE, "collate/reference-map-per-sample", check_reference_maps_are_per_sample),
        _guard(SUITE_COLLATE, "collate/padding-dtypes-and-enums", check_padding_dtypes_and_enums),
        _guard(
            SUITE_COLLATE,
            "collate/reference-mask-only-known-entity-ref",
            check_reference_mask_only_known_entity_ref,
        ),
        _guard(SUITE_COLLATE, "collate/out-of-range-index-rejected", check_out_of_range_rejected),
        _guard(SUITE_COLLATE, "collate/rejects-empty-and-bad-elements", check_collate_rejects_bad_input),
        _guard(SUITE_COLLATE, "collate/capacity-rechecked-before-padding", check_collate_capacity_before_padding),
        _guard(SUITE_COLLATE, "collate/true-rows-keep-local-indices", check_blocks_remain_local_under_padding),
        _guard(SUITE_COLLATE, "collate/three-samples-varied-lengths", check_three_sample_padding_isolation),
    ]


def run_all(only: Optional[str] = None) -> list:
    outcomes: list = []
    if only is not None and only not in SUITE_NAMES:
        return outcomes
    context = _Context()
    if only in (None, SUITE_TENSORIZE):
        outcomes.extend(_tensorize_cases(context))
    if only in (None, SUITE_COLLATE):
        outcomes.extend(_collate_cases(context))
    return outcomes


__all__ = [
    "BUNDLE_CASES",
    "CAPACITIES",
    "EXPECTED_CONTENT_STATE",
    "EXPECTED_KNOWLEDGE_STATE",
    "EXPECTED_MASK_COLUMNS",
    "EXPECTED_SCHEMA",
    "EXPECTED_VALUE_TYPE",
    "SUITE_COLLATE",
    "SUITE_NAMES",
    "SUITE_TENSORIZE",
    "TENSOR_ROOT",
    "TRANSFORMS",
    "catalog",
    "combat_without_first_two_cards",
    "expected_entity_order",
    "expected_sizes",
    "manifest_for",
    "raw_catalog",
    "renamed_instances",
    "run_all",
    "snapshot",
]
