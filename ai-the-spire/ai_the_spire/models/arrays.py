"""张量化输出 → 模型输入数组（**纯函数、不改写张量语义**）。

为什么单独一层：模型只应看到"数组 + 掩码 + 字典规模"，不应自己解析 wire 或目录。
所有索引列都是**样本内行号**（引用映射的行号，不是匿名实例句柄）——这正是
"匿名实例 ID 不作为特征"的可核对形式（`policy-contract.md` §1、`encoder-design.md` §2 第 72 行）。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping

import numpy as np

from ..encoding.manifest import DICTIONARY_TABLES, GymManifest
from ..encoding.tensor import TensorizedObservation

#: 模型输入里每张字典表的规模键（与 Manifest 的字典表同名）。
TABLE_KEYS = DICTIONARY_TABLES


def _int_array(block, column: str) -> np.ndarray:
    return np.asarray(block.column(column), dtype=np.int64)


def _float_array(block, column: str, dtype) -> np.ndarray:
    return np.asarray(block.column(column), dtype=dtype)


def _bool_array(block, column: str) -> np.ndarray:
    return np.asarray(block.column(column), dtype=bool)


@dataclass(frozen=True)
class ModelInput:
    """一份快照的模型输入：数组 + 掩码 + 字典规模（全部只读）。"""

    table_sizes: Mapping[str, int]
    dtype: Any

    #: entities
    entity_kind: np.ndarray
    entity_content: np.ndarray
    entity_content_mask: np.ndarray
    entity_state: np.ndarray
    entity_mask: np.ndarray
    #: fields（实体字段）
    field_owner: np.ndarray
    field_id: np.ndarray
    field_value_type: np.ndarray
    field_state: np.ndarray
    field_number: np.ndarray
    field_boolean: np.ndarray
    field_category: np.ndarray
    field_reference: np.ndarray
    field_reference_mask: np.ndarray
    field_mask: np.ndarray
    #: relations
    relation_type: np.ndarray
    relation_source: np.ndarray
    relation_target: np.ndarray
    relation_mask: np.ndarray
    #: candidates / candidate_fields
    candidate_action: np.ndarray
    candidate_mask: np.ndarray
    candidate_field_owner: np.ndarray
    candidate_field_id: np.ndarray
    candidate_field_value_type: np.ndarray
    candidate_field_state: np.ndarray
    candidate_field_number: np.ndarray
    candidate_field_boolean: np.ndarray
    candidate_field_category: np.ndarray
    candidate_field_reference: np.ndarray
    candidate_field_reference_mask: np.ndarray
    candidate_field_mask: np.ndarray

    @property
    def entity_count(self) -> int:
        return int(self.entity_mask.shape[0])

    @property
    def candidate_count(self) -> int:
        return int(self.candidate_mask.shape[0])

    @property
    def valid_candidates(self) -> int:
        return int(self.candidate_mask.sum())

    def table_size(self, table: str) -> int:
        return int(self.table_sizes[table])


def build_model_input(observation: TensorizedObservation, manifest: GymManifest,
                      *, config=None) -> ModelInput:
    """`TensorizedObservation` → :class:`ModelInput`（dtype 由 config 决定，默认 float64）。"""
    if not isinstance(observation, TensorizedObservation):
        raise TypeError(f"observation 必须是 TensorizedObservation，得到 {type(observation).__name__}")
    if not isinstance(manifest, GymManifest):
        raise TypeError(f"manifest 必须是 GymManifest，得到 {type(manifest).__name__}")
    dtype = config.float_dtype() if config is not None else np.float64

    sizes = {table: manifest.dictionaries[table].size for table in TABLE_KEYS}
    entities = observation.block("entities")
    fields = observation.block("fields")
    relations = observation.block("relations")
    candidates = observation.block("candidates")
    candidate_fields = observation.block("candidate_fields")

    return ModelInput(
        table_sizes=sizes,
        dtype=dtype,
        entity_kind=_int_array(entities, "kind_id"),
        entity_content=_int_array(entities, "content_id"),
        entity_content_mask=_bool_array(entities, "content_mask"),
        entity_state=_int_array(entities, "content_state"),
        entity_mask=_bool_array(entities, "entity_mask"),
        field_owner=_int_array(fields, "owner_entity"),
        field_id=_int_array(fields, "field_id"),
        field_value_type=_int_array(fields, "value_type"),
        field_state=_int_array(fields, "knowledge_state"),
        field_number=_float_array(fields, "number_value", dtype),
        field_boolean=_float_array(fields, "boolean_value", dtype),
        field_category=_int_array(fields, "category_id"),
        field_reference=_int_array(fields, "reference_entity"),
        field_reference_mask=_bool_array(fields, "reference_mask"),
        field_mask=_bool_array(fields, "field_mask"),
        relation_type=_int_array(relations, "type_id"),
        relation_source=_int_array(relations, "source"),
        relation_target=_int_array(relations, "target"),
        relation_mask=_bool_array(relations, "relation_mask"),
        candidate_action=_int_array(candidates, "action_type"),
        candidate_mask=_bool_array(candidates, "candidate_mask"),
        candidate_field_owner=_int_array(candidate_fields, "owner_candidate"),
        candidate_field_id=_int_array(candidate_fields, "field_id"),
        candidate_field_value_type=_int_array(candidate_fields, "value_type"),
        candidate_field_state=_int_array(candidate_fields, "knowledge_state"),
        candidate_field_number=_float_array(candidate_fields, "number_value", dtype),
        candidate_field_boolean=_float_array(candidate_fields, "boolean_value", dtype),
        candidate_field_category=_int_array(candidate_fields, "category_id"),
        candidate_field_reference=_int_array(candidate_fields, "reference_entity"),
        candidate_field_reference_mask=_bool_array(candidate_fields, "reference_mask"),
        candidate_field_mask=_bool_array(candidate_fields, "field_mask"),
    )


def pad_model_input(source: ModelInput, *, entities: int, candidates: int) -> ModelInput:
    """把输入补齐到给定行数（**只用于掩码检查**：补齐行 mask=false、索引列写 0）。

    真实路径不会产出补齐行（单样本 `tensorize` 无 padding）；这里的补齐用于验收
    "被掩码的行不参与聚合、也不获得概率"，不改变任何真实语义。
    """
    if entities < source.entity_count or candidates < source.candidate_count:
        raise ValueError("补齐只能增大行数（不得截断真实行）")

    def grow(array: np.ndarray, rows: int, fill) -> np.ndarray:
        if array.shape[0] == rows:
            return array
        pad = np.full((rows - array.shape[0],) + array.shape[1:], fill, dtype=array.dtype)
        return np.concatenate([array, pad], axis=0)

    entity_rows = entities - source.entity_count
    candidate_rows = candidates - source.candidate_count
    return ModelInput(
        table_sizes=source.table_sizes,
        dtype=source.dtype,
        entity_kind=grow(source.entity_kind, entities, 0),
        entity_content=grow(source.entity_content, entities, 0),
        entity_content_mask=grow(source.entity_content_mask, entities, False),
        entity_state=grow(source.entity_state, entities, 0),
        entity_mask=grow(source.entity_mask, entities, False),
        field_owner=source.field_owner,
        field_id=source.field_id,
        field_value_type=source.field_value_type,
        field_state=source.field_state,
        field_number=source.field_number,
        field_boolean=source.field_boolean,
        field_category=source.field_category,
        field_reference=source.field_reference,
        field_reference_mask=source.field_reference_mask,
        field_mask=source.field_mask,
        relation_type=source.relation_type,
        relation_source=source.relation_source,
        relation_target=source.relation_target,
        relation_mask=source.relation_mask,
        candidate_action=grow(source.candidate_action, candidates, 0),
        candidate_mask=grow(source.candidate_mask, candidates, False),
        candidate_field_owner=source.candidate_field_owner,
        candidate_field_id=source.candidate_field_id,
        candidate_field_value_type=source.candidate_field_value_type,
        candidate_field_state=source.candidate_field_state,
        candidate_field_number=source.candidate_field_number,
        candidate_field_boolean=source.candidate_field_boolean,
        candidate_field_category=source.candidate_field_category,
        candidate_field_reference=source.candidate_field_reference,
        candidate_field_reference_mask=source.candidate_field_reference_mask,
        candidate_field_mask=source.candidate_field_mask,
    ) if entity_rows or candidate_rows else source
