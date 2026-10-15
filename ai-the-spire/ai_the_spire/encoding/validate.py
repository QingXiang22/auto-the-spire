"""A-03 校验层：字段/内容状态、适用域、引用、牌堆四条不变量、候选与资源上限。

对应开工清单 §7.2 的 A-03 行（"校验字段/内容状态、适用域、引用、牌堆四条不变量、
候选与资源上限，给错误代码和路径"）与 encoder-design §5.2/§8 的最小调用面
``validate_snapshot(snapshot, manifest)``。

职责划分（"共享规范由协议侧维护，AI 侧不新增第二套协议"）：

* **共享层**：``docs/contracts/net/0.1/wire.schema.json`` 的 L1 结构与
  ``tools/contract-check/wire_layers.check_snapshot`` 的 L3 判定始终执行，命中后按
  :data:`SHARED_RULE_CODES` 映射成编码错误码，并把共享规则 ID 放进 ``related_ids``。
  判定逻辑不在这里复制；未登记的共享规则一律落到 ``unsupported_structure`` 兜底，
  不会被静默丢弃。
* **编码层自有**（共享层看不到或不该看的事实）：
  - Manifest 的 exact identity / 张量布局 / 目录绑定（``version_mismatch``）；
  - Manifest 字典闭合：实体种类、字段、动作类型、类别值、entity_ref 目标种类；
  - 适用域与 required 省略（按 ``FieldRecord.applies_to``/``required``）；
  - 值类型与规范十进制、``number_format=integer``；
  - 数值变换与 float64 转换误差（``non_finite_value``/``numeric_precision_loss``，
    复用 :meth:`GymManifest.encode_number`，不在别处重算）；
  - 引用闭合于当前快照（实体/成员/候选参数）；
  - 牌堆四条不变量（protocol.md §2 第 85–90 行，V-06.1–4）；
  - 候选句柄/动作语义键/候选字段；
  - **资源上限**（``EncodingConfig.capacities``，AI 侧独有配置）：越限显式报
    ``capacity_exceeded`` 并给出路径与实测数，**不静默截断**。
* 失败不返回可供评分的部分编码：调用方拿到 :class:`ValidationReport`（收集全部错误）
  或 :meth:`ValidationReport.raise_first`（首条即抛）。

路径约定与共享 L3 一致（便于定位与去重）：``snapshot.entities[<instance_id>]
.fields[<field_id>].value``、``snapshot.piles[<pile_ref>].known_positions[<card_ref>]
.position``、``snapshot.legal_actions[<candidate_id>].action_type`` 等。报告的
``checked``/``skipped`` 明确列出本层实际执行与因缺少目录而未执行的检查名。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Iterable, Mapping, Optional

from ai_the_spire.contracts import layers
from ai_the_spire.contracts.types import (
    CONTENT_STATES,
    FIELD_STATES,
    SNAPSHOT_STATUSES,
)
from ai_the_spire.contracts.views import PublicCatalog, PublicSnapshot

from .errors import EncodingError
from .manifest import GymManifest

#: PublicSnapshot 的 14 个必需键（protocol §2 第 58 行；I-16）。
SNAPSHOT_KEYS = (
    "protocol_version",
    "ruleset_id",
    "catalog_id",
    "backend_profile_id",
    "session_id",
    "observation_id",
    "status",
    "terminal_result",
    "entities",
    "relations",
    "piles",
    "sequences",
    "groups",
    "legal_actions",
)

#: 快照内必须为数组的块（其余键为标量/对象）。
LIST_BLOCKS = (
    "entities",
    "relations",
    "piles",
    "sequences",
    "groups",
    "legal_actions",
)

#: 共享规则 ID → 编码错误码。粗粒度（一条共享规则可能覆盖多个编码码）时取主要码，
#: 精细化判定由编码层自有检查负责；``related_ids`` 始终保留 ``I-xx``。
SHARED_RULE_CODES = {
    "I-01": "version_mismatch",
    "I-02": "version_mismatch",
    "I-05": "unknown_type",
    "I-17": "empty_candidates_non_terminal",
    "I-18": "value_type_mismatch",
    "I-19": "invalid_content_state",
    "I-20": "missing_required_field",
    "I-21": "value_type_mismatch",
    "I-22": "value_type_mismatch",
    "I-23": "unsupported_structure",
    "I-24": "unsupported_structure",
    "I-25": "duplicate_identity",
    "I-26": "unknown_type",
    "I-27": "invalid_position",
    "I-28": "count_mismatch",
    "I-29": "invalid_position",
    "I-30": "count_mismatch",
    "I-31": "duplicate_identity",
    "I-32": "unsupported_structure",
    "I-33": "unsupported_structure",
    "I-34": "count_mismatch",
    "I-35": "count_mismatch",
    "I-36": "missing_required_field",
    "I-37": "duplicate_identity",
    "I-38": "unsupported_structure",
    "I-39": "dangling_reference",
    "I-40": "duplicate_identity",
    "I-66": "unsupported_structure",
    "I-72": "value_type_mismatch",
    "I-74": "unsupported_structure",
    "I-79": "unsupported_structure",
    "I-80": "unsupported_structure",
    "I-90": "unsupported_structure",
    "I-91": "unsupported_structure",
}

#: 未登记共享规则时的兜底码：宁可报"结构不支持"，也不静默接受。
SHARED_RULE_FALLBACK = "unsupported_structure"

#: 共享层需要目录才能判定的检查（无目录时记入 ``skipped``，不假装通过）。
_SHARED_DECLARATION_DOMAIN = (
    "shared/L3-declaration-domain(成员kind·pile_type·关系种类与位置·序列组基数·终局facts·保留声明)"
)

#: 容量上限键 → 路径与计数口径。
_CAPACITY_BLOCKS = (
    ("entities", "max_entities", "实体行数"),
    ("relations", "max_relations", "关系行数"),
    ("sequences", "max_sequences", "序列行数"),
    ("groups", "max_groups", "组行数"),
    ("legal_actions", "max_candidates", "候选行数"),
)


# --------------------------------------------------------------------------- #
# 结果与收集
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class ValidationReport:
    """一次校验的完整结论：错误列表 + 实际执行/跳过的检查名。"""

    errors: tuple[EncodingError, ...]
    checked: tuple[str, ...]
    skipped: tuple[str, ...]

    @property
    def ok(self) -> bool:
        return not self.errors

    def first(self) -> Optional[EncodingError]:
        return self.errors[0] if self.errors else None

    def raise_first(self) -> None:
        """有错误时抛第一条（失败不返回部分编码）。"""
        if self.errors:
            raise self.errors[0]

    def codes(self) -> tuple[str, ...]:
        return tuple(error.code for error in self.errors)

    def paths(self) -> tuple[str, ...]:
        return tuple(error.path for error in self.errors)

    def render(self) -> list[str]:
        return [error.render() for error in self.errors]

    def __bool__(self) -> bool:  # ``if report:`` 读作"有问题"
        return not self.ok


class _Errors:
    """按 ``(code, path)`` 去重的错误收集器。"""

    def __init__(self) -> None:
        self.items: list[EncodingError] = []
        self._seen: set[tuple[str, str]] = set()

    def add(
        self,
        code: str,
        path: str,
        message: str,
        related: Iterable[str] = (),
    ) -> None:
        key = (code, path)
        if key in self._seen:
            return
        self._seen.add(key)
        self.items.append(EncodingError(code, path, message, related_ids=tuple(related)))

    def add_error(self, error: EncodingError) -> None:
        self.add(error.code, error.path, error.message, error.related_ids)

    def __len__(self) -> int:
        return len(self.items)


# --------------------------------------------------------------------------- #
# 小工具
# --------------------------------------------------------------------------- #
def _document_of(snapshot: Any, errors: _Errors) -> Any:
    if isinstance(snapshot, PublicSnapshot):
        return snapshot.to_wire()
    if isinstance(snapshot, Mapping):
        return snapshot
    errors.add(
        "unsupported_structure",
        "snapshot",
        f"快照必须是对象或 PublicSnapshot 视图，得到 {type(snapshot).__name__}",
        related=("I-16",),
    )
    return None


def _catalog_document(catalog: Any) -> Optional[dict]:
    if catalog is None:
        return None
    if isinstance(catalog, PublicCatalog):
        return catalog.to_wire()
    if isinstance(catalog, Mapping):
        return dict(catalog)
    raise TypeError(
        f"catalog 必须是 PublicCatalog 或目录文档，得到 {type(catalog).__name__}"
    )


def _is_mapping_list(value: Any) -> bool:
    return isinstance(value, list) and all(isinstance(item, Mapping) for item in value)


def _field_path(owner_path: str, field_id: Any) -> str:
    return f"{owner_path}.fields[{field_id}]"


# --------------------------------------------------------------------------- #
# 1) 结构 gate 与 decision/terminal 配对（I-16/I-17）
# --------------------------------------------------------------------------- #
def _check_shape(document: Mapping, errors: _Errors, checked: list[str]) -> None:
    checked.append("structure/top-level+status-pairing")
    for key in SNAPSHOT_KEYS:
        if key not in document:
            errors.add(
                "unsupported_structure",
                f"snapshot.{key}",
                f"快照缺少必需键 {key!r}",
                related=("I-16",),
            )
    for key in document:
        if key not in SNAPSHOT_KEYS:
            errors.add(
                "unsupported_structure",
                f"snapshot.{key}",
                f"快照含未声明键 {key!r}（顶层未知属性拒绝）",
                related=("I-16",),
            )

    status = document.get("status")
    if status not in SNAPSHOT_STATUSES:
        errors.add(
            "unsupported_structure",
            "snapshot.status",
            f"status 必须是 {list(SNAPSHOT_STATUSES)}，得到 {status!r}",
            related=("I-16", "I-17"),
        )

    for block in LIST_BLOCKS:
        if block in document and not isinstance(document[block], list):
            errors.add(
                "unsupported_structure",
                f"snapshot.{block}",
                f"{block} 必须是数组，得到 {type(document[block]).__name__}",
                related=("I-16",),
            )

    terminal = document.get("terminal_result")
    actions = document.get("legal_actions")
    if status == "decision":
        if terminal is not None:
            errors.add(
                "unsupported_structure",
                "snapshot.terminal_result",
                "status=decision 时 terminal_result 必须为 null",
                related=("I-17",),
            )
        if isinstance(actions, list) and not actions:
            errors.add(
                "empty_candidates_non_terminal",
                "snapshot.legal_actions",
                "非终止（decision）快照的候选不得为空",
                related=("I-17",),
            )
    elif status == "terminal":
        if not isinstance(terminal, Mapping):
            errors.add(
                "unsupported_structure",
                "snapshot.terminal_result",
                "status=terminal 时 terminal_result 必须是对象",
                related=("I-17",),
            )
        if isinstance(actions, list) and actions:
            errors.add(
                "unsupported_structure",
                "snapshot.legal_actions",
                "status=terminal 时候选必须为空",
                related=("I-17",),
            )


# --------------------------------------------------------------------------- #
# 2) Manifest 身份 / 布局 / 目录绑定
# --------------------------------------------------------------------------- #
def _check_binding(
    document: Any,
    manifest: GymManifest,
    catalog_document: Optional[dict],
    errors: _Errors,
    checked: list[str],
    skipped: list[str],
) -> None:
    checked.append("encoding/manifest-layout")
    try:
        manifest.require_current_layout()
    except EncodingError as exc:
        errors.add_error(exc)

    if catalog_document is None:
        skipped.append("encoding/catalog-binding")
    else:
        checked.append("encoding/catalog-binding")
        try:
            manifest.assert_catalog(catalog_document, path="catalog")
        except EncodingError as exc:
            errors.add_error(exc)

    if not isinstance(document, Mapping):
        return
    if all(isinstance(document.get(key), str) and document.get(key) for key in
           ("protocol_version", "ruleset_id", "catalog_id", "backend_profile_id")):
        checked.append("encoding/exact-identity")
        try:
            manifest.assert_snapshot_identity(document, path="snapshot")
        except EncodingError as exc:
            errors.add_error(exc)
    else:
        skipped.append("encoding/exact-identity(快照身份键缺失或非字符串)")


# --------------------------------------------------------------------------- #
# 3) 值 / 状态 / 数值（I-20/I-21/I-22/I-72 + 数值变换）
# --------------------------------------------------------------------------- #
def _check_value(
    errors: _Errors,
    manifest: GymManifest,
    record,
    field: Mapping,
    path: str,
    kinds_by_instance: Mapping[str, Any],
) -> None:
    """known 值的类型、域、引用目标与数值变换；``path`` 指到该字段本身。"""
    state = field.get("state")
    if state not in FIELD_STATES:
        errors.add(
            "unsupported_structure",
            f"{path}.state",
            f"state 必须是 {list(FIELD_STATES)}，得到 {state!r}",
            related=("I-20",),
        )
        return
    if state != "known":
        if "value" in field:
            errors.add(
                "state_value_mismatch",
                f"{path}.value",
                f"state={state} 不得带 value（不能把未知/不可提供当已知零）",
                related=("I-20",),
            )
        return
    if "value" not in field:
        errors.add(
            "state_value_mismatch",
            f"{path}.value",
            "state=known 必须带 value",
            related=("I-20",),
        )
        return

    value = field["value"]
    value_type = record.value_type
    if value_type == "number":
        if isinstance(value, bool) or not isinstance(value, str):
            errors.add(
                "value_type_mismatch",
                f"{path}.value",
                f"number 字段必须是规范十进制字符串，得到 {value!r}",
                related=("I-22",),
            )
            return
        if not layers.is_canonical_decimal(value):
            errors.add(
                "value_type_mismatch",
                f"{path}.value",
                f"非规范 decimal: {value!r}（不接受指数形式/前导 +/负零，不默默重写）",
                related=("I-21",),
            )
            return
        if record.number_format == "integer" and not layers.is_integer_decimal(value):
            errors.add(
                "value_type_mismatch",
                f"{path}.value",
                f"number_format=integer 不接受带小数部分的值: {value!r}",
                related=("I-72",),
            )
            return
        try:
            manifest.encode_number(record.field_id, value)
        except EncodingError as exc:
            # 精确解析 → 固定变换 → float64：非有限值/转换误差超限在此显式失败。
            errors.add(exc.code, f"{path}.value", exc.message, exc.related_ids)
    elif value_type == "boolean":
        if not isinstance(value, bool):
            errors.add(
                "value_type_mismatch",
                f"{path}.value",
                f"boolean 字段必须是 JSON true/false，得到 {value!r}",
                related=("I-22",),
            )
    elif value_type == "category":
        if not isinstance(value, str):
            errors.add(
                "value_type_mismatch",
                f"{path}.value",
                f"category 字段必须是字符串类别 ID，得到 {value!r}",
                related=("I-22",),
            )
        elif value not in record.category_ids:
            errors.add(
                "unknown_type",
                f"{path}.value",
                f"类别值 {value!r} 不在字段 {record.field_id!r} 的声明类别 "
                f"{list(record.category_ids)} 中",
                related=("I-22", value),
            )
    elif value_type == "entity_ref":
        if not isinstance(value, str) or not value:
            errors.add(
                "value_type_mismatch",
                f"{path}.value",
                f"entity_ref 字段必须是非空 instance_id 字符串，得到 {value!r}",
                related=("I-22",),
            )
            return
        if value not in kinds_by_instance:
            errors.add(
                "dangling_reference",
                f"{path}.value",
                f"entity_ref 悬空: {value!r} 不在当前快照的实体中",
                related=("I-39", value),
            )
            return
        targets = record.target_kind_ids
        if targets and kinds_by_instance[value] not in targets:
            errors.add(
                "value_type_mismatch",
                f"{path}.value",
                f"entity_ref 目标种类 {kinds_by_instance[value]!r} 不属于声明 {list(targets)}",
                related=("I-22", value),
            )


# --------------------------------------------------------------------------- #
# 4) 实体：内容状态、字典闭合、适用域、required、字段值
# --------------------------------------------------------------------------- #
def _collect_instances(
    document: Mapping, errors: _Errors
) -> tuple[dict[str, Any], list[Mapping]]:
    """返回 ``instance_id → kind_id`` 与合法实体行；重复/非法行在此报告。"""
    entities = document.get("entities")
    if not isinstance(entities, list):
        return {}, []
    kinds_by_instance: dict[str, Any] = {}
    rows: list[Mapping] = []
    for index, entity in enumerate(entities):
        if not isinstance(entity, Mapping):
            errors.add(
                "unsupported_structure",
                f"snapshot.entities[{index}]",
                f"实体必须是对象，得到 {type(entity).__name__}",
                related=("I-16",),
            )
            continue
        instance_id = entity.get("instance_id")
        if not isinstance(instance_id, str) or not instance_id:
            errors.add(
                "unsupported_structure",
                f"snapshot.entities[{index}].instance_id",
                f"instance_id 必须是非空字符串，得到 {instance_id!r}",
                related=("I-16",),
            )
            continue
        if instance_id in kinds_by_instance:
            errors.add(
                "duplicate_identity",
                "snapshot.entities",
                f"instance_id 重复: {instance_id!r}",
                related=("I-40", instance_id),
            )
            continue
        kinds_by_instance[instance_id] = entity.get("kind_id")
        rows.append(entity)
    return kinds_by_instance, rows


def _check_entities(
    document: Mapping,
    manifest: GymManifest,
    errors: _Errors,
    checked: list[str],
    kinds_by_instance: Mapping[str, Any],
    rows: Iterable[Mapping],
) -> None:
    checked.append("content/content-state")
    checked.append("fields/dictionary+applicability+required+value")
    kinds_table = manifest.dictionaries["entity_kinds"]
    for entity in rows:
        instance_id = entity["instance_id"]
        base = f"snapshot.entities[{instance_id}]"
        kind_id = entity.get("kind_id")

        if not isinstance(kind_id, str) or kinds_table.index_of(kind_id) is None:
            errors.add(
                "unknown_type",
                f"{base}.kind_id",
                f"未注册的实体种类: {kind_id!r}（不在本 Manifest 的实体种类字典中）",
                related=("I-05",) + ((kind_id,) if isinstance(kind_id, str) else ()),
            )

        content_state = entity.get("content_state")
        if content_state not in CONTENT_STATES:
            errors.add(
                "invalid_content_state",
                f"{base}.content_state",
                f"content_state 必须是 {list(CONTENT_STATES)}，得到 {content_state!r}",
                related=("I-19",),
            )
        content_id = entity.get("content_id")
        if content_state == "known":
            if not isinstance(content_id, str) or not content_id:
                errors.add(
                    "invalid_content_state",
                    f"{base}.content_id",
                    "content_state=known 必须带非空 content_id",
                    related=("I-19",),
                )
        elif "content_id" in entity:
            errors.add(
                "invalid_content_state",
                f"{base}.content_id",
                f"content_state={content_state!r} 不得带 content_id",
                related=("I-19",),
            )

        fields = entity.get("fields")
        if not isinstance(fields, list):
            errors.add(
                "unsupported_structure",
                f"{base}.fields",
                f"fields 必须是数组，得到 {type(fields).__name__}",
                related=("I-16",),
            )
            continue

        present: set[str] = set()
        for index, field in enumerate(fields):
            if not isinstance(field, Mapping):
                errors.add(
                    "unsupported_structure",
                    f"{base}.fields[{index}]",
                    f"字段必须是对象，得到 {type(field).__name__}",
                    related=("I-16",),
                )
                continue
            field_id = field.get("field_id")
            if not isinstance(field_id, str) or not field_id:
                errors.add(
                    "unsupported_structure",
                    f"{base}.fields[{index}].field_id",
                    f"field_id 必须是非空字符串，得到 {field_id!r}",
                    related=("I-16",),
                )
                continue
            fpath = _field_path(base, field_id)
            if field_id in present:
                errors.add(
                    "duplicate_identity",
                    f"{base}.fields",
                    f"同一实体重复声明 field_id: {field_id!r}",
                    related=("I-88", field_id),
                )
                continue
            present.add(field_id)

            record = manifest.field_records.get(field_id)
            if record is None:
                errors.add(
                    "unregistered_field",
                    fpath,
                    f"未注册字段: {field_id!r}（不在本 Manifest 的字段字典中）",
                    related=("I-05", field_id),
                )
                continue
            if (
                isinstance(kind_id, str)
                and kinds_table.index_of(kind_id) is not None
                and record.applies_to_entity_kinds
                and kind_id not in record.applies_to_entity_kinds
            ):
                errors.add(
                    "field_not_applicable",
                    fpath,
                    f"字段不适用于实体种类 {kind_id!r}；声明的适用域 "
                    f"{list(record.applies_to_entity_kinds)}",
                    related=("I-22", field_id),
                )
            _check_value(errors, manifest, record, field, fpath, kinds_by_instance)

        if isinstance(kind_id, str):
            for field_id, record in manifest.field_records.items():
                if not record.required or field_id in present:
                    continue
                if not record.applies_to_entity_kinds:
                    continue
                if kind_id not in record.applies_to_entity_kinds:
                    continue
                errors.add(
                    "missing_required_field",
                    _field_path(base, field_id),
                    f"required 且适用的字段被省略: {field_id!r}",
                    related=("I-20", field_id),
                )


# --------------------------------------------------------------------------- #
# 5) 引用闭合（不依赖目录：限定在当前快照内）
# --------------------------------------------------------------------------- #
def _check_references(
    document: Mapping,
    errors: _Errors,
    checked: list[str],
    kinds_by_instance: Mapping[str, Any],
) -> None:
    checked.append("references/entity-closure")
    known = kinds_by_instance

    relations = document.get("relations")
    if isinstance(relations, list):
        for relation in relations:
            if not isinstance(relation, Mapping):
                continue
            base = f"snapshot.relations[{relation.get('type_id')}]"
            for key in ("source_ref", "target_ref"):
                ref = relation.get(key)
                if isinstance(ref, str) and ref and ref not in known:
                    errors.add(
                        "dangling_reference",
                        f"{base}.{key}",
                        f"悬空引用: {ref!r} 不在当前快照的实体中",
                        related=("I-39", ref),
                    )

    sequences = document.get("sequences")
    if isinstance(sequences, list):
        for sequence in sequences:
            if not isinstance(sequence, Mapping):
                continue
            base = f"snapshot.sequences[{sequence.get('type_id')}]"
            owner = sequence.get("owner_ref")
            if isinstance(owner, str) and owner and owner not in known:
                errors.add(
                    "dangling_reference",
                    f"{base}.owner_ref",
                    f"悬空引用: {owner!r} 不在当前快照的实体中",
                    related=("I-39", owner),
                )
            elements = sequence.get("elements")
            if isinstance(elements, list):
                for element in elements:
                    if isinstance(element, str) and element and element not in known:
                        errors.add(
                            "dangling_reference",
                            f"{base}.elements",
                            f"悬空引用: {element!r} 不在当前快照的实体中",
                            related=("I-39", element),
                        )

    groups = document.get("groups")
    if isinstance(groups, list):
        for group in groups:
            if not isinstance(group, Mapping):
                continue
            base = f"snapshot.groups[{group.get('type_id')}]"
            owner = group.get("owner_ref")
            if isinstance(owner, str) and owner and owner not in known:
                errors.add(
                    "dangling_reference",
                    f"{base}.owner_ref",
                    f"悬空引用: {owner!r} 不在当前快照的实体中",
                    related=("I-39", owner),
                )
            members = group.get("members")
            if isinstance(members, list):
                for member in members:
                    ref = member.get("entity_ref") if isinstance(member, Mapping) else None
                    if isinstance(ref, str) and ref and ref not in known:
                        errors.add(
                            "dangling_reference",
                            f"{base}.members",
                            f"悬空引用: {ref!r} 不在当前快照的实体中",
                            related=("I-39", ref),
                        )


# --------------------------------------------------------------------------- #
# 6) 牌堆四条不变量（protocol §2 第 85–90 行，V-06.1–4）
# --------------------------------------------------------------------------- #
def _check_piles(
    document: Mapping,
    errors: _Errors,
    checked: list[str],
    kinds_by_instance: Mapping[str, Any],
) -> None:
    checked.append("piles/v06-1-position-bounds")
    checked.append("piles/v06-2-member-closure+count")
    checked.append("piles/v06-3-position-member-consistency")
    checked.append("piles/v06-4-no-fabricated-cards")
    piles = document.get("piles")
    if not isinstance(piles, list):
        return

    seen_refs: set[str] = set()
    for pile in piles:
        if not isinstance(pile, Mapping):
            errors.add(
                "unsupported_structure",
                "snapshot.piles",
                f"牌堆必须是对象，得到 {type(pile).__name__}",
                related=("I-16",),
            )
            continue
        pile_ref = pile.get("pile_ref")
        base = f"snapshot.piles[{pile_ref}]"

        # I-26：pile_ref 唯一，且引用当前快照的实体
        if isinstance(pile_ref, str):
            if pile_ref in seen_refs:
                errors.add(
                    "duplicate_identity",
                    f"{base}.pile_ref",
                    f"pile_ref 重复: {pile_ref!r}",
                    related=("I-26", str(pile_ref)),
                )
            seen_refs.add(pile_ref)
            if pile_ref not in kinds_by_instance:
                errors.add(
                    "dangling_reference",
                    f"{base}.pile_ref",
                    f"悬空引用: {pile_ref!r} 不在当前快照的实体中",
                    related=("I-26", pile_ref),
                )

        total = pile.get("total_count")
        if isinstance(total, bool) or not isinstance(total, int) or total < 0:
            errors.add(
                "unsupported_structure",
                f"{base}.total_count",
                f"total_count 必须是非负安全 JSON 整数，得到 {total!r}",
                related=("I-23",),
            )
            total_ok = False
        else:
            total_ok = True

        members = pile.get("visible_members")
        if not isinstance(members, list):
            members = []
            errors.add(
                "unsupported_structure",
                f"{base}.visible_members",
                "visible_members 必须是数组",
                related=("I-16",),
            )
        member_set = {member for member in members if isinstance(member, str)}

        # V-06.3：known_positions 的 card_ref 是 visible_members 的子集；
        # 每张牌至多一个位置，每个位置至多一张牌（I-29）。
        positions = pile.get("known_positions")
        if not isinstance(positions, list):
            positions = []
            errors.add(
                "unsupported_structure",
                f"{base}.known_positions",
                "known_positions 必须是数组",
                related=("I-16",),
            )
        seen_cards: set[str] = set()
        seen_positions: set[int] = set()
        for position in positions:
            if not isinstance(position, Mapping):
                errors.add(
                    "unsupported_structure",
                    f"{base}.known_positions",
                    f"位置记录必须是对象，得到 {type(position).__name__}",
                    related=("I-16",),
                )
                continue
            card_ref = position.get("card_ref")
            ppath = f"{base}.known_positions[{card_ref}]"
            index = position.get("position")

            if isinstance(card_ref, str):
                if card_ref in seen_cards:
                    errors.add(
                        "duplicate_identity",
                        ppath,
                        f"同一张牌出现多个位置: {card_ref!r}",
                        related=("I-29", card_ref),
                    )
                seen_cards.add(card_ref)
                if card_ref not in member_set:
                    errors.add(
                        "invalid_position",
                        ppath,
                        f"card_ref {card_ref!r} 不是 visible_members 的成员"
                        "（位置记录不能增加卡牌，也不能定位未知成员）",
                        related=("I-29", card_ref),
                    )
            if isinstance(index, bool) or not isinstance(index, int):
                errors.add(
                    "unsupported_structure",
                    f"{ppath}.position",
                    f"position 必须是非负安全 JSON 整数，得到 {index!r}",
                    related=("I-23",),
                )
                continue
            if index in seen_positions:
                errors.add(
                    "duplicate_identity",
                    ppath,
                    f"同一位置被多张牌占用: {index!r}",
                    related=("I-29", str(index)),
                )
            seen_positions.add(index)
            # V-06.1：0 <= position < total_count，成员不完整也用真实总数作边界（I-27）
            if total_ok and not (0 <= index < total):
                errors.add(
                    "invalid_position",
                    f"{ppath}.position",
                    f"position {index} 不满足 0 <= position < total_count {total}",
                    related=("I-27", str(index)),
                )

        # V-06.2：visible_members 唯一、引用闭合、数量不超过 total_count，
        # members_complete=true 时取等（I-28）
        if len(member_set) != len(members):
            errors.add(
                "duplicate_identity",
                f"{base}.visible_members",
                "visible_members 存在重复引用",
                related=("I-28",),
            )
        for member in members:
            if isinstance(member, str) and member not in kinds_by_instance:
                errors.add(
                    "dangling_reference",
                    f"{base}.visible_members",
                    f"悬空引用: {member!r} 不在当前快照的实体中",
                    related=("I-28", member),
                )
        if total_ok and len(members) > total:
            errors.add(
                "count_mismatch",
                f"{base}.visible_members",
                f"成员数 {len(members)} 超过 total_count {total}",
                related=("I-28",),
            )
        if pile.get("members_complete") is True and total_ok and len(members) != total:
            errors.add(
                "count_mismatch",
                f"{base}.members_complete",
                f"members_complete=true 但成员数 {len(members)} != total_count {total}",
                related=("I-28",),
            )

        # V-06.4：位置记录不增加卡牌数（I-30）；成员完整也不意味着顺序完整，
        # 因此只做"不超过"的上界检查，不要求位置铺满。
        if total_ok and len(positions) > min(total, len(members)):
            errors.add(
                "count_mismatch",
                f"{base}.known_positions",
                f"位置记录数 {len(positions)} 超过成员/总数上界 "
                f"{min(total, len(members))}（位置记录不增加卡牌数）",
                related=("I-30",),
            )


# --------------------------------------------------------------------------- #
# 7) 候选：句柄、动作语义键、字段
# --------------------------------------------------------------------------- #
def _check_candidates(
    document: Mapping,
    manifest: GymManifest,
    errors: _Errors,
    checked: list[str],
    has_catalog: bool,
    kinds_by_instance: Mapping[str, Any],
) -> None:
    checked.append("candidates/handles+semantics+fields")
    actions = document.get("legal_actions")
    if not isinstance(actions, list):
        return

    seen_ids: set[str] = set()
    semantics: dict[bytes, str] = {}
    for action in actions:
        if not isinstance(action, Mapping):
            errors.add(
                "unsupported_structure",
                "snapshot.legal_actions",
                f"候选必须是对象，得到 {type(action).__name__}",
                related=("I-16",),
            )
            continue
        candidate_id = action.get("candidate_id")
        if not isinstance(candidate_id, str) or not candidate_id:
            errors.add(
                "unsupported_structure",
                "snapshot.legal_actions",
                f"candidate_id 必须是非空字符串，得到 {candidate_id!r}",
                related=("I-16",),
            )
            continue
        if candidate_id in seen_ids:
            errors.add(
                "duplicate_identity",
                "snapshot.legal_actions",
                f"candidate_id 重复: {candidate_id!r}",
                related=("I-40", candidate_id),
            )
            continue
        seen_ids.add(candidate_id)
        base = f"snapshot.legal_actions[{candidate_id}]"

        # I-37：同一完整动作语义不得以不同句柄重复发布
        try:
            key = layers.action_semantic_key(dict(action))
        except Exception:  # noqa: BLE001 - 结构异常由 L1/L3 报告，这里不重复判定
            key = None
        if key is not None:
            if key in semantics:
                errors.add(
                    "duplicate_identity",
                    "snapshot.legal_actions",
                    f"同一动作语义键以不同 candidate_id 重复发布: "
                    f"{semantics[key]!r} 与 {candidate_id!r}",
                    related=("I-37", candidate_id),
                )
            else:
                semantics[key] = candidate_id

        action_type = action.get("action_type")
        if not isinstance(action_type, str) or manifest.dictionaries[
            "action_types"
        ].index_of(action_type) is None:
            errors.add(
                "unknown_type",
                f"{base}.action_type",
                f"未注册的动作类型: {action_type!r}（不在本 Manifest 的动作类型字典中）",
                related=("I-05",) + ((action_type,) if isinstance(action_type, str) else ()),
            )

        fields = action.get("fields")
        if not isinstance(fields, list):
            errors.add(
                "unsupported_structure",
                f"{base}.fields",
                "fields 必须是数组",
                related=("I-16",),
            )
            continue

        present: set[str] = set()
        for index, field in enumerate(fields):
            if not isinstance(field, Mapping):
                errors.add(
                    "unsupported_structure",
                    f"{base}.fields[{index}]",
                    f"候选字段必须是对象，得到 {type(field).__name__}",
                    related=("I-16",),
                )
                continue
            field_id = field.get("field_id")
            if not isinstance(field_id, str) or not field_id:
                errors.add(
                    "unsupported_structure",
                    f"{base}.fields[{index}].field_id",
                    f"field_id 必须是非空字符串，得到 {field_id!r}",
                    related=("I-16",),
                )
                continue
            fpath = _field_path(base, field_id)
            if field_id in present:
                errors.add(
                    "duplicate_identity",
                    f"{base}.fields",
                    f"动作重复声明 field_id: {field_id!r}",
                    related=("I-88", field_id),
                )
                continue
            present.add(field_id)

            if field.get("state") != "known":
                errors.add(
                    "state_value_mismatch",
                    fpath,
                    "执行所需字段必须 known（未知收益或未知目标留在观察实体）",
                    related=("I-36", field_id),
                )
            record = manifest.field_records.get(field_id)
            if record is None:
                errors.add(
                    "unregistered_field",
                    fpath,
                    f"未声明的动作字段: {field_id!r}（不在本 Manifest 的字段字典中）",
                    related=("I-05", field_id),
                )
                continue
            if (
                isinstance(action_type, str)
                and manifest.dictionaries["action_types"].index_of(action_type) is not None
                and record.applies_to_action_types
                and action_type not in record.applies_to_action_types
            ):
                errors.add(
                    "field_not_applicable",
                    fpath,
                    f"字段不适用于动作类型 {action_type!r}；声明的适用域 "
                    f"{list(record.applies_to_action_types)}",
                    related=("I-22", field_id),
                )
            _check_value(errors, manifest, record, field, fpath, kinds_by_instance)

        # 动作声明侧（required_fields/optional_fields）需要目录；无目录时按字段声明
        # 的 required + applies_to.action_types 推导（L2 的 I-08/I-87 保证两者一致）。
        if isinstance(action_type, str):
            for field_id, record in manifest.field_records.items():
                if not record.required or field_id in present:
                    continue
                if not record.applies_to_action_types:
                    continue
                if action_type not in record.applies_to_action_types:
                    continue
                errors.add(
                    "missing_required_field",
                    _field_path(base, field_id),
                    f"缺少必需动作字段: {field_id!r}",
                    related=("I-36", field_id),
                )


# --------------------------------------------------------------------------- #
# 8) 终局 facts（按字段声明可判定的部分）
# --------------------------------------------------------------------------- #
def _check_terminal(
    document: Mapping,
    manifest: GymManifest,
    errors: _Errors,
    checked: list[str],
    skipped: list[str],
    has_catalog: bool,
    kinds_by_instance: Mapping[str, Any],
) -> None:
    if document.get("status") != "terminal":
        return
    terminal = document.get("terminal_result")
    if not isinstance(terminal, Mapping):
        return
    checked.append("terminal/facts-fields+value")
    if not has_catalog:
        skipped.append("terminal/reason-declaration(reason_id 注册与 outcome 需目录)")
    reason_id = terminal.get("reason_id")
    facts = terminal.get("facts")
    if not isinstance(facts, list):
        return
    for fact in facts:
        if not isinstance(fact, Mapping):
            errors.add(
                "unsupported_structure",
                "snapshot.terminal_result.facts",
                f"结果事实必须是对象，得到 {type(fact).__name__}",
                related=("I-18",),
            )
            continue
        field_id = fact.get("field_id")
        if not isinstance(field_id, str) or not field_id:
            errors.add(
                "unsupported_structure",
                "snapshot.terminal_result.facts",
                f"结果事实的 field_id 必须是非空字符串，得到 {field_id!r}",
                related=("I-18",),
            )
            continue
        fpath = f"snapshot.terminal_result.facts[{field_id}]"
        record = manifest.field_records.get(field_id)
        if record is None:
            errors.add(
                "unregistered_field",
                fpath,
                f"未声明的结果事实字段: {field_id!r}",
                related=("I-18", field_id),
            )
            continue
        if (
            isinstance(reason_id, str)
            and record.applies_to_terminal_reasons
            and reason_id not in record.applies_to_terminal_reasons
        ):
            errors.add(
                "field_not_applicable",
                fpath,
                f"字段不适用于终局原因 {reason_id!r}；声明的适用域 "
                f"{list(record.applies_to_terminal_reasons)}",
                related=("I-18", field_id),
            )
        _check_value(errors, manifest, record, fact, fpath, kinds_by_instance)


# --------------------------------------------------------------------------- #
# 9) 资源上限（AI 侧独有；越限显式失败，不静默截断）
# --------------------------------------------------------------------------- #
def _check_capacity(
    document: Mapping,
    manifest: GymManifest,
    errors: _Errors,
    checked: list[str],
) -> None:
    checked.append("capacity/limits")
    limits = manifest.encoding_config.capacities
    if not limits.configured_keys:
        checked.append("capacity/none-configured")
        return

    for block, key, label in _CAPACITY_BLOCKS:
        limit = getattr(limits, key)
        if limit is None:
            continue
        value = document.get(block)
        if not isinstance(value, list):
            continue
        if len(value) > limit:
            errors.add(
                "capacity_exceeded",
                f"snapshot.{block}",
                f"{label} {len(value)} 超过配置上限 {key}={limit}",
                related=(key, str(limit), str(len(value))),
            )

    fields_total = 0
    for entity in document.get("entities") or []:
        if isinstance(entity, Mapping) and isinstance(entity.get("fields"), list):
            fields_total += len(entity["fields"])
    limit = limits.max_fields
    if limit is not None and fields_total > limit:
        errors.add(
            "capacity_exceeded",
            "snapshot.entities",
            f"实体字段行数 {fields_total} 超过配置上限 max_fields={limit}",
            related=("max_fields", str(limit), str(fields_total)),
        )

    candidate_fields = 0
    for action in document.get("legal_actions") or []:
        if isinstance(action, Mapping) and isinstance(action.get("fields"), list):
            candidate_fields += len(action["fields"])
    limit = limits.max_candidate_fields
    if limit is not None and candidate_fields > limit:
        errors.add(
            "capacity_exceeded",
            "snapshot.legal_actions",
            f"候选字段行数 {candidate_fields} 超过配置上限 "
            f"max_candidate_fields={limit}",
            related=("max_candidate_fields", str(limit), str(candidate_fields)),
        )

    positions_total = 0
    for pile in document.get("piles") or []:
        if isinstance(pile, Mapping) and isinstance(pile.get("known_positions"), list):
            positions_total += len(pile["known_positions"])
    limit = limits.max_pile_positions
    if limit is not None and positions_total > limit:
        errors.add(
            "capacity_exceeded",
            "snapshot.piles",
            f"已知位置记录数 {positions_total} 超过配置上限 "
            f"max_pile_positions={limit}",
            related=("max_pile_positions", str(limit), str(positions_total)),
        )


# --------------------------------------------------------------------------- #
# 10) 共享分层（L1 结构 + L3 快照对目录）→ 编码错误码
# --------------------------------------------------------------------------- #
def _fine_code(rule_id: str, path: str, message: str) -> str:
    """少量按路径/消息可无歧义细分的共享规则；其余用主要码。"""
    if rule_id == "I-05" and ".fields[" in path:
        return "unregistered_field"
    if rule_id == "I-26" and message.startswith("悬空"):
        return "dangling_reference"
    return SHARED_RULE_CODES.get(rule_id, SHARED_RULE_FALLBACK)


def _merge_shared(
    document: Mapping,
    catalog_document: Optional[dict],
    errors: _Errors,
    checked: list[str],
    skipped: list[str],
) -> None:
    checked.append("shared/L1-structure")
    layers_active = ("L1", "L3")
    if catalog_document is None:
        skipped.append(_SHARED_DECLARATION_DOMAIN)
    else:
        checked.append("shared/L3-snapshot-vs-catalog")
    try:
        findings = list(
            layers.document_findings(
                document, "snapshot", layers=layers_active, catalog=catalog_document
            )
        )
    except Exception as exc:  # noqa: BLE001 - 共享层对 L1 已拒绝结构不保证返回型
        # 共享 L3（``tools/contract-check/wire_layers.check_snapshot`` 的排序/动作语义键
        # 路径）会直接索引已判非法的元素，对 L1 已拒绝的文档抛 TypeError/AttributeError。
        # 本层承诺"始终返回 ValidationReport"，因此在此显式降级为一条错误并点名共享层，
        # 既不静默吞掉（报告非空、路径可见），也不假装该层检查通过。
        checked.append("shared/L1+L3-aborted-by-shared-exception")
        errors.add(
            "unsupported_structure",
            "snapshot.shared-findings",
            f"共享分层检查在 L1 已拒绝的结构上抛出 {type(exc).__name__}: {exc}",
            related=("I-66", type(exc).__name__),
        )
        return
    for finding in findings:
        errors.add(
            _fine_code(finding.rule_id, finding.path, finding.message),
            finding.path,
            f"[{finding.layer}/{finding.rule_id}] {finding.message}",
            related=(finding.rule_id,),
        )


# --------------------------------------------------------------------------- #
# 入口
# --------------------------------------------------------------------------- #
def validate_snapshot(
    snapshot: Any,
    manifest: GymManifest,
    *,
    catalog: Any = None,
) -> ValidationReport:
    """校验一份当前公开快照；返回收集全部错误的 :class:`ValidationReport`。

    ``snapshot`` 为 wire 文档（``dict``）或 :class:`PublicSnapshot` 视图；
    ``catalog`` 为构建该 Manifest 的目录（``dict`` 或 :class:`PublicCatalog`），
    给出时会先做 exact identity/目录绑定核对，并启用共享 L3 的全部声明域检查。
    缺目录时报告 ``skipped`` 明确列出未执行的检查，**不把"没查"写成"通过"**。
    """
    if not isinstance(manifest, GymManifest):
        raise TypeError(f"manifest 必须是 GymManifest，得到 {type(manifest).__name__}")

    errors = _Errors()
    checked: list[str] = []
    skipped: list[str] = []

    document = _document_of(snapshot, errors)
    catalog_document = _catalog_document(catalog)

    _check_binding(document, manifest, catalog_document, errors, checked, skipped)

    if isinstance(document, Mapping):
        _check_shape(document, errors, checked)
        kinds_by_instance, rows = _collect_instances(document, errors)
        _check_entities(document, manifest, errors, checked, kinds_by_instance, rows)
        _check_references(document, errors, checked, kinds_by_instance)
        _check_candidates(
            document,
            manifest,
            errors,
            checked,
            catalog_document is not None,
            kinds_by_instance,
        )
        _check_terminal(
            document,
            manifest,
            errors,
            checked,
            skipped,
            catalog_document is not None,
            kinds_by_instance,
        )
        _check_piles(document, errors, checked, kinds_by_instance)
        _check_capacity(document, manifest, errors, checked)
        _merge_shared(document, catalog_document, errors, checked, skipped)

    return ValidationReport(
        errors=tuple(errors.items),
        checked=tuple(checked),
        skipped=tuple(skipped),
    )


def require_valid_snapshot(snapshot: Any, manifest: GymManifest, *, catalog: Any = None) -> None:
    """校验并在失败时抛出首条错误（供 tensorize/collate 前置调用）。"""
    validate_snapshot(snapshot, manifest, catalog=catalog).raise_first()


__all__ = [
    "LIST_BLOCKS",
    "SHARED_RULE_CODES",
    "SHARED_RULE_FALLBACK",
    "SNAPSHOT_KEYS",
    "ValidationReport",
    "require_valid_snapshot",
    "validate_snapshot",
]
