# -*- coding: utf-8 -*-
"""A-04 张量化与拼批：确定性数组、类型化通道、引用映射、局部索引与掩码。

对应开工清单 §7.2 的 A-04 行（"实现 tensorize/collate、typed通道、reference_map、
local索引和 masks，确保跨样本无误连"）与 encoder-design §5/§5.1/§5.2 的最小调用面
``tensorize(snapshot, manifest)``、``collate(encoded_samples)``。

职责边界
--------

* **协议结构**由 ``ai_the_spire.contracts`` + 共享资产定义；本模块不新增第二套协议
  解析，输入先经过 A-03 :func:`validate_snapshot` 全量校验，**校验失败不产生任何
  部分张量**（抛首条 :class:`EncodingError`）。
* **字典与数值变换**只来自 :class:`GymManifest`（A-02），本模块不重建索引、不猜
  offset/scale、不 ``float()`` 冒充源精度。
* 不维护历史、不保存 ``known_top``、不读隐藏状态、不执行规则、不导入活动 client/core，
  不安装训练框架：输出是 Python 列表 + dtype 描述（encoder-design §5.1 第 164 行）。

跨样本安全（本模块的核心不变量）
--------------------------------

1. **局部索引**：``source``/``target``/``owner``/``element``/``member``/
   ``reference_entity``/``pile_entity``/``card_entity`` 一律是**本样本内**的实体行号。
   批内不出现"全局行号"，也不提供把行号跨样本解析的入口；读行必须同时给出样本下标
   ``b``（:meth:`TensorizedBatch.row`），语义解析走
   :meth:`TensorizedBatch.entity_instance` / :meth:`TensorizedBatch.candidate_id`。
   因此"样本 A 的第 4 行"与"样本 B 的第 4 行"即使槽位相同也不会互相解析。
2. **补齐行不等于零值**：补齐行的整型列写 0，但对应 mask 列为 false，且值类型/知识
   状态列写 **pad=0**（不是 known=1）——所以"忽略 mask 也能看出这不是已知记录"。
   补齐的 ``reference_entity`` 也是 0 + ``reference_mask=false``，不会被当成指向
   本样本实体行 0 的真实引用。
3. **引用映射是侧信息**：``reference_map`` 按样本各存一份（实例 ID→行、候选 ID→行），
   不做跨样本共享槽位；它是校验/组装用信息，不作为网络特征（§5 第 147 行）。

派生行（阅读入口）
------------------

encoder-design §4 第 103 行要求三个保留声明在**张量化时**派生，wire 不重复填：

* ``net:pile_total_count`` / ``net:pile_members_complete`` → ``fields`` 块的两行
  （owner 为牌堆实体行）；
* ``visible_members`` → ``relations`` 块的 ``net:pile_member`` 行；
* ``known_positions`` → ``pile_positions`` 块。

因此 ``fields``/``relations`` 块的行数**大于** wire 里的字段/关系行数；A-03 的
``capacity_exceeded`` 按 wire 行数计，本模块按**张量块**行数再计一次（路径前缀不同：
``tensor.<block>`` vs ``snapshot.<block>``），两处都不静默截断。
"""

from __future__ import annotations

from dataclasses import dataclass
from types import MappingProxyType
from typing import Any, Iterable, Mapping, Optional, Sequence

from ai_the_spire.contracts.views import PublicSnapshot

from .config import CONFIG_VERSION, CapacityLimits
from .errors import EncodingError
from .manifest import GymManifest
from .validate import validate_snapshot

# --------------------------------------------------------------------------- #
# 块与列 schema（encoder-design §5.1 第 153–164 行的逐项落点）
# --------------------------------------------------------------------------- #
#: 块顺序即 §5.1 表格顺序；每个样本都带齐 8 个块（可为 0 行）。
TENSOR_BLOCK_NAMES = (
    "entities",
    "fields",
    "relations",
    "pile_positions",
    "sequences",
    "groups",
    "candidates",
    "candidate_fields",
)

#: 每个块的列（顺序即输出列顺序）与 dtype。
#: int64 = 类型/状态/行索引/计数/位置；bool = 布尔与 masks；float64 = number 通道。
COLUMN_SCHEMAS: "Mapping[str, tuple[tuple[str, str], ...]]" = MappingProxyType(
    {
        "entities": (
            ("kind_id", "int64"),
            ("content_id", "int64"),
            ("content_state", "int64"),
            ("content_mask", "bool"),
            ("entity_mask", "bool"),
        ),
        "fields": (
            ("owner_entity", "int64"),
            ("field_id", "int64"),
            ("value_type", "int64"),
            ("knowledge_state", "int64"),
            ("number_value", "float64"),
            ("boolean_value", "int64"),
            ("category_id", "int64"),
            ("reference_entity", "int64"),
            ("reference_mask", "bool"),
            ("field_mask", "bool"),
        ),
        "relations": (
            ("type_id", "int64"),
            ("source", "int64"),
            ("target", "int64"),
            ("position", "int64"),
            ("position_mask", "bool"),
            ("relation_mask", "bool"),
        ),
        "pile_positions": (
            ("pile_entity", "int64"),
            ("card_entity", "int64"),
            ("absolute_position", "int64"),
            ("position_record_mask", "bool"),
        ),
        "sequences": (
            ("sequence_type", "int64"),
            ("owner", "int64"),
            ("element", "int64"),
            ("ordinal", "int64"),
            ("sequence_record_mask", "bool"),
        ),
        "groups": (
            ("group_type", "int64"),
            ("owner", "int64"),
            ("member", "int64"),
            ("multiplicity", "int64"),
            ("group_record_mask", "bool"),
        ),
        "candidates": (
            ("action_type", "int64"),
            ("candidate_mask", "bool"),
        ),
        "candidate_fields": (
            ("owner_candidate", "int64"),
            ("field_id", "int64"),
            ("value_type", "int64"),
            ("knowledge_state", "int64"),
            ("number_value", "float64"),
            ("boolean_value", "int64"),
            ("category_id", "int64"),
            ("reference_entity", "int64"),
            ("reference_mask", "bool"),
            ("field_mask", "bool"),
        ),
    }
)

#: 每个块的"行是否真实存在"掩码列（行 padding 由它表达）。
MASK_COLUMNS: "Mapping[str, str]" = MappingProxyType(
    {
        "entities": "entity_mask",
        "fields": "field_mask",
        "relations": "relation_mask",
        "pile_positions": "position_record_mask",
        "sequences": "sequence_record_mask",
        "groups": "group_record_mask",
        "candidates": "candidate_mask",
        "candidate_fields": "field_mask",
    }
)

#: 每个块里**行索引**列（解引用必须限定在同一 B 样本行范围内；见
#: :meth:`TensorizedBatch.resolve_row`）。``entities``/``candidates`` 自身是行的定义，
#: 它们的行号只由 ``reference_map`` 解析。
INDEX_COLUMNS: "Mapping[str, tuple]" = MappingProxyType(
    {
        "entities": (),
        "fields": ("owner_entity",),
        "relations": ("source", "target"),
        "pile_positions": ("pile_entity", "card_entity"),
        "sequences": ("owner", "element"),
        "groups": ("owner", "member"),
        "candidates": (),
        "candidate_fields": ("owner_candidate",),
    }
)

#: 固定枚举 index（§5.1 第 166 行）：pad=0，其余按文档顺序。
VALUE_TYPE_IDS: "Mapping[str, int]" = MappingProxyType(
    {"pad": 0, "number": 1, "boolean": 2, "category": 3, "entity_ref": 4}
)
KNOWLEDGE_STATE_IDS: "Mapping[str, int]" = MappingProxyType(
    {"pad": 0, "known": 1, "unknown": 2, "unavailable": 3}
)
CONTENT_STATE_IDS: "Mapping[str, int]" = MappingProxyType(
    {"pad": 0, "known": 1, "unknown": 2, "unavailable": 3, "not_applicable": 4}
)

#: 补齐/未知索引：类别字典索引 0 是补齐；实体局部行号 0 是**有效实体**（§5.1 第 166 行）。
PAD_INDEX = 0

#: 保留声明（§4 第 103 行；共享 I-33 要求使用 piles 的目录必须声明这三者）。
RESERVED_PILE_MEMBER = "net:pile_member"
RESERVED_PILE_TOTAL_COUNT = "net:pile_total_count"
RESERVED_PILE_MEMBERS_COMPLETE = "net:pile_members_complete"
RESERVED_PILE_DECLARATIONS = (
    RESERVED_PILE_MEMBER,
    RESERVED_PILE_TOTAL_COUNT,
    RESERVED_PILE_MEMBERS_COMPLETE,
)

#: 张量块 → 容量上限键（``EncodingConfig.capacities``）。
CAPACITY_BY_BLOCK: "Mapping[str, str]" = MappingProxyType(
    {
        "entities": "max_entities",
        "fields": "max_fields",
        "relations": "max_relations",
        "pile_positions": "max_pile_positions",
        "sequences": "max_sequences",
        "groups": "max_groups",
        "candidates": "max_candidates",
        "candidate_fields": "max_candidate_fields",
    }
)

#: 无目录时无法从 Manifest 判定的检查（Manifest 不持久化 position_schema）。
SKIPPED_WITHOUT_CATALOG = (
    "tensor/pile-position-schema(Manifest 不含 pile_type.position_schema；无目录时不判定已知位置是否被声明)",
)


# --------------------------------------------------------------------------- #
# 小工具
# --------------------------------------------------------------------------- #
def _utf8(text: str) -> bytes:
    """规范排序键：UTF-8 字节序（encoder-design §5 第 139 行）。"""
    return text.encode("utf-8")


def _document_of(snapshot: Any) -> Mapping:
    """与 A-03 同一口径取 wire 文档（视图 → ``to_wire()``）。"""
    if isinstance(snapshot, PublicSnapshot):
        return snapshot.to_wire()
    if isinstance(snapshot, Mapping):
        return snapshot
    raise EncodingError(
        "unsupported_structure",
        "snapshot",
        f"快照必须是对象或 PublicSnapshot 视图，得到 {type(snapshot).__name__}",
        related_ids=("I-16",),
    )


def _catalog_document(catalog: Any) -> Optional[dict]:
    if catalog is None:
        return None
    if hasattr(catalog, "to_wire"):
        return catalog.to_wire()
    if isinstance(catalog, Mapping):
        return dict(catalog)
    raise TypeError(f"catalog 必须是 PublicCatalog 或目录文档，得到 {type(catalog).__name__}")


def _pile_schema_by_kind(catalog_document: Optional[Mapping]) -> Optional[dict]:
    """目录的 ``pile_types``：kind_id → position_schema（无目录时为 None，判定跳过）。"""
    if catalog_document is None:
        return None
    declarations = catalog_document.get("declarations")
    if not isinstance(declarations, Mapping):
        return None
    table: dict[str, Any] = {}
    for item in declarations.get("pile_types") or []:
        if isinstance(item, Mapping) and isinstance(item.get("kind_id"), str):
            table[item["kind_id"]] = item.get("position_schema")
    return table


def _pad_value(dtype: str) -> Any:
    if dtype == "bool":
        return False
    if dtype == "float64":
        return 0.0
    return 0


def _check_masks(block: str, columns: Mapping[str, tuple]) -> None:
    """补齐行的 pad 不变量：mask=false 时状态/类型列必须是 pad=0。"""
    mask_name = MASK_COLUMNS[block]
    state_columns = [name for name in ("knowledge_state", "content_state", "value_type") if name in columns]
    for row, real in enumerate(columns[mask_name]):
        if real:
            continue
        for state_column in state_columns:
            if columns[state_column][row] != PAD_INDEX:
                raise AssertionError(
                    f"{block}.{state_column}[{row}]={columns[state_column][row]} 在补齐行上不是 pad=0"
                )


# --------------------------------------------------------------------------- #
# 块
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class Block:
    """一个样本内的一组行；列顺序、dtype 与长度都按 :data:`COLUMN_SCHEMAS` 校验。"""

    name: str
    columns: Mapping[str, tuple]

    def __post_init__(self) -> None:
        if self.name not in COLUMN_SCHEMAS:
            raise ValueError(f"未知张量块 {self.name!r}")
        schema = COLUMN_SCHEMAS[self.name]
        expected = tuple(column for column, _ in schema)
        if tuple(self.columns) != expected:
            raise ValueError(
                f"{self.name} 列顺序必须恰为 {list(expected)}，得到 {list(self.columns)}"
            )
        n = len(self.columns[expected[0]]) if expected else 0
        ordered = {}
        for column, dtype in schema:
            values = tuple(self.columns[column])
            if len(values) != n:
                raise ValueError(
                    f"{self.name}.{column} 长度 {len(values)} 与块行数 {n} 不一致"
                )
            for index, value in enumerate(values):
                if dtype == "bool":
                    ok = isinstance(value, bool)
                elif dtype == "int64":
                    ok = isinstance(value, int) and not isinstance(value, bool)
                else:
                    ok = isinstance(value, float)
                if not ok:
                    raise ValueError(
                        f"{self.name}.{column}[{index}]={value!r} 不符合 dtype {dtype}"
                    )
            ordered[column] = values
        _check_masks(self.name, ordered)
        object.__setattr__(self, "columns", MappingProxyType(ordered))

    # -- 读取面 ---------------------------------------------------------- #
    @property
    def n(self) -> int:
        """本块行数（样本内不含补齐）。"""
        return len(self.columns[COLUMN_SCHEMAS[self.name][0][0]])

    @property
    def mask(self) -> tuple:
        return self.columns[MASK_COLUMNS[self.name]]

    def column(self, name: str) -> tuple:
        try:
            return self.columns[name]
        except KeyError as exc:  # pragma: no cover - 拼写错误的显式失败
            raise KeyError(f"{self.name} 没有列 {name!r}；可用 {list(self.columns)}") from exc

    def dtype(self, name: str) -> str:
        for column, dtype in COLUMN_SCHEMAS[self.name]:
            if column == name:
                return dtype
        raise KeyError(f"{self.name} 没有列 {name!r}")

    def row(self, index: int) -> dict:
        return {column: values[index] for column, values in self.columns.items()}

    def rows(self) -> tuple:
        return tuple(self.row(index) for index in range(self.n))

    def to_wire(self) -> dict:
        """块的可检查表示（列 → 列表）；仅用于报告/调试，不是网络输入。"""
        return {column: list(values) for column, values in self.columns.items()}

    def describe(self) -> dict:
        return {
            "block": self.name,
            "rows": self.n,
            "true_rows": int(sum(1 for flag in self.mask if flag)),
            "dtypes": {column: self.dtype(column) for column in self.columns},
        }


@dataclass(frozen=True)
class BatchBlock:
    """拼批后的一个块：``[B, rows]``，行数取本批最大，短样本按 mask=false 补齐。"""

    name: str
    lengths: tuple
    columns: Mapping[str, tuple]

    def __post_init__(self) -> None:
        if self.name not in COLUMN_SCHEMAS:
            raise ValueError(f"未知张量块 {self.name!r}")
        schema = COLUMN_SCHEMAS[self.name]
        expected = tuple(column for column, _ in schema)
        if tuple(self.columns) != expected:
            raise ValueError(
                f"{self.name} 列顺序必须恰为 {list(expected)}，得到 {list(self.columns)}"
            )
        batch_size = len(self.lengths)
        for length in self.lengths:
            if isinstance(length, bool) or not isinstance(length, int) or length < 0:
                raise ValueError(f"{self.name}.lengths 必须是非负整数，得到 {length!r}")
        rows = max(self.lengths) if self.lengths else 0
        ordered = {}
        for column, dtype in schema:
            column_values = tuple(tuple(sample) for sample in self.columns[column])
            if len(column_values) != batch_size:
                raise ValueError(
                    f"{self.name}.{column} 样本数 {len(column_values)} 与 B={batch_size} 不一致"
                )
            for b, values in enumerate(column_values):
                if len(values) != rows:
                    raise ValueError(
                        f"{self.name}.{column}[{b}] 长度 {len(values)} 与本批行数 {rows} 不一致"
                    )
                for index, value in enumerate(values):
                    if dtype == "bool":
                        ok = isinstance(value, bool)
                    elif dtype == "int64":
                        ok = isinstance(value, int) and not isinstance(value, bool)
                    else:
                        ok = isinstance(value, float)
                    if not ok:
                        raise ValueError(
                            f"{self.name}.{column}[{b}][{index}]={value!r} 不符合 dtype {dtype}"
                        )
            ordered[column] = column_values
        # 补齐行的 pad 不变量（逐样本检查）。
        mask_name = MASK_COLUMNS[self.name]
        for b, length in enumerate(self.lengths):
            for index in range(length, rows):
                for state_column in ("knowledge_state", "content_state", "value_type"):
                    if state_column in ordered and ordered[state_column][b][index] != PAD_INDEX:
                        raise ValueError(
                            f"{self.name}.{state_column}[{b}][{index}] 在补齐行上不是 pad=0"
                        )
                if ordered[mask_name][b][index]:
                    raise ValueError(f"{self.name}.{mask_name}[{b}][{index}] 补齐行 mask 必须为 false")
        object.__setattr__(self, "columns", MappingProxyType(ordered))

    # -- 读取面 ---------------------------------------------------------- #
    @property
    def batch_size(self) -> int:
        return len(self.lengths)

    @property
    def rows(self) -> int:
        return max(self.lengths) if self.lengths else 0

    def column(self, name: str) -> tuple:
        try:
            return self.columns[name]
        except KeyError as exc:
            raise KeyError(f"{self.name} 没有列 {name!r}；可用 {list(self.columns)}") from exc

    def dtype(self, name: str) -> str:
        for column, dtype in COLUMN_SCHEMAS[self.name]:
            if column == name:
                return dtype
        raise KeyError(f"{self.name} 没有列 {name!r}")

    def mask(self, b: int) -> tuple:
        return self.columns[MASK_COLUMNS[self.name]][b]

    def padded_rows(self, b: int) -> int:
        """样本 ``b`` 的补齐行数（mask=false 的尾部行）。"""
        return self.rows - self.lengths[b]

    def is_padding(self, b: int, index: int) -> bool:
        return index >= self.lengths[b]

    def row(self, b: int, index: int) -> dict:
        return {column: values[b][index] for column, values in self.columns.items()}

    def true_rows(self, b: int) -> tuple:
        return tuple(self.row(b, index) for index in range(self.lengths[b]))

    def to_wire(self) -> dict:
        return {column: [list(sample) for sample in values] for column, values in self.columns.items()}

    def describe(self) -> dict:
        return {
            "block": self.name,
            "shape": [self.batch_size, self.rows],
            "lengths": list(self.lengths),
            "padding_rows": [self.padded_rows(b) for b in range(self.batch_size)],
            "dtypes": {column: self.dtype(column) for column in self.columns},
        }


# --------------------------------------------------------------------------- #
# 引用映射
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class ReferenceMap:
    """会话实例 ID → 实体行、当前候选 ID → 候选行；**只在本样本内有效**。

    这是侧信息（§5 第 147 行）：不作为网络特征，也不跨样本共享槽位。``instance_of_row``
    给出反向查询，用于检查"某行到底是哪个实例"。
    """

    entity_rows: Mapping[str, int]
    candidate_rows: Mapping[str, int]

    def __post_init__(self) -> None:
        for label, table in (("entity_rows", self.entity_rows), ("candidate_rows", self.candidate_rows)):
            keys = list(table)
            if len(set(keys)) != len(keys):
                raise ValueError(f"{label} 键重复")
            for key, row in table.items():
                if not isinstance(key, str) or not key:
                    raise ValueError(f"{label} 键必须是非空字符串，得到 {key!r}")
                if isinstance(row, bool) or not isinstance(row, int) or row < 0:
                    raise ValueError(f"{label}[{key!r}] 必须是非负整数行号，得到 {row!r}")
        object.__setattr__(self, "entity_rows", MappingProxyType(dict(self.entity_rows)))
        object.__setattr__(self, "candidate_rows", MappingProxyType(dict(self.candidate_rows)))

    @property
    def entity_count(self) -> int:
        return len(self.entity_rows)

    @property
    def candidate_count(self) -> int:
        return len(self.candidate_rows)

    def row_of_instance(self, instance_id: str) -> Optional[int]:
        return self.entity_rows.get(instance_id)

    def row_of_candidate(self, candidate_id: str) -> Optional[int]:
        return self.candidate_rows.get(candidate_id)

    def instance_of_row(self, row: int) -> Optional[str]:
        for instance_id, candidate_row in self.entity_rows.items():
            if candidate_row == row:
                return instance_id
        return None

    def candidate_of_row(self, row: int) -> Optional[str]:
        for candidate_id, candidate_row in self.candidate_rows.items():
            if candidate_row == row:
                return candidate_id
        return None

    def to_wire(self) -> dict:
        return {
            "instance_rows": {key: self.entity_rows[key] for key in sorted(self.entity_rows, key=_utf8)},
            "candidate_rows": {key: self.candidate_rows[key] for key in sorted(self.candidate_rows, key=_utf8)},
        }


# --------------------------------------------------------------------------- #
# 单样本张量
# --------------------------------------------------------------------------- #
_METADATA_KEYS = (
    "protocol_version",
    "ruleset_id",
    "catalog_id",
    "backend_profile_id",
    "gym_id",
    "dictionary_version",
    "manifest_digest",
    "tensor_layout_version",
    "scenario_profile",
    "source_catalog_digest",
    "encoding_config_digest",
    "config_version",
    "number_channel_dtype",
    "session_id",
    "observation_id",
    "status",
)


@dataclass(frozen=True)
class TensorizedObservation:
    """一份快照的确定性数组表示 + 引用映射 + 校验缺口清单。"""

    metadata: Mapping[str, Any]
    blocks: Mapping[str, Block]
    reference_map: ReferenceMap
    validation_skipped: tuple = ()

    def __post_init__(self) -> None:
        if tuple(self.blocks) != TENSOR_BLOCK_NAMES:
            raise ValueError(f"blocks 必须恰为 {list(TENSOR_BLOCK_NAMES)}，得到 {list(self.blocks)}")
        if not isinstance(self.reference_map, ReferenceMap):
            raise TypeError("reference_map 必须是 ReferenceMap")
        object.__setattr__(self, "metadata", MappingProxyType(dict(self.metadata)))

    # -- 读取面 ---------------------------------------------------------- #
    def block(self, name: str) -> Block:
        try:
            return self.blocks[name]
        except KeyError as exc:
            raise KeyError(f"未知张量块 {name!r}；可用 {list(self.blocks)}") from exc

    @property
    def sizes(self) -> dict:
        return {name: self.blocks[name].n for name in TENSOR_BLOCK_NAMES}

    @property
    def tensor_layout_version(self) -> str:
        return self.metadata["tensor_layout_version"]

    def compat_key(self) -> tuple:
        """拼批兼容身份：布局、Manifest、字典、数值配置与环境身份缺一不可。"""
        return (
            self.metadata["tensor_layout_version"],
            self.metadata["number_channel_dtype"],
            self.metadata["gym_id"],
            self.metadata["dictionary_version"],
            self.metadata["manifest_digest"],
            self.metadata["source_catalog_digest"],
            self.metadata["encoding_config_digest"],
            self.metadata["config_version"],
            self.metadata["protocol_version"],
            self.metadata["ruleset_id"],
            self.metadata["catalog_id"],
            self.metadata["backend_profile_id"],
        )

    #: ``compat_key()`` 各分量名（拼批失败时点名用）。
    COMPAT_FIELDS = (
        "tensor_layout_version",
        "number_channel_dtype",
        "gym_id",
        "dictionary_version",
        "manifest_digest",
        "source_catalog_digest",
        "encoding_config_digest",
        "config_version",
        "protocol_version",
        "ruleset_id",
        "catalog_id",
        "backend_profile_id",
    )

    def describe(self) -> dict:
        return {
            "metadata": dict(self.metadata),
            "block_sizes": self.sizes,
            "blocks": {name: self.blocks[name].describe() for name in TENSOR_BLOCK_NAMES},
            "reference_map": self.reference_map.to_wire(),
            "validation_skipped": list(self.validation_skipped),
        }


# --------------------------------------------------------------------------- #
# 行构造
# --------------------------------------------------------------------------- #
def _field_row(
    manifest: GymManifest,
    record: Any,
    value_field: Mapping,
    owner_row: int,
    row_of_instance: Mapping[str, int],
    owner_path: str,
    *,
    owner_column: str = "owner_entity",
) -> dict:
    """一个字段行：只启用与 ``value_type`` 匹配的通道，其余补零并靠状态列屏蔽。

    ``owner_column`` 区分实体字段（``owner_entity``）与候选字段（``owner_candidate``）：
    两者列名不同，行结构相同。
    """
    field_id = record.field_id
    path = f"{owner_path}.fields[{field_id}]"
    state = value_field["state"]
    row = {
        owner_column: owner_row,
        "field_id": record.index,
        "value_type": VALUE_TYPE_IDS[record.value_type],
        "knowledge_state": KNOWLEDGE_STATE_IDS[state],
        "number_value": 0.0,
        "boolean_value": 0,
        "category_id": 0,
        "reference_entity": 0,
        "reference_mask": False,
        "field_mask": True,
    }
    if state != "known":
        return row  # unknown/unavailable 不因补零而被读成 known（§5 第 137 行）

    value = value_field["value"]
    if record.value_type == "number":
        row["number_value"] = manifest.encode_number(field_id, value)
    elif record.value_type == "boolean":
        if not isinstance(value, bool):
            raise EncodingError(
                "value_type_mismatch",
                f"{path}.value",
                f"boolean 字段必须是 JSON 布尔，得到 {value!r}",
                related_ids=(field_id,),
            )
        row["boolean_value"] = 1 if value else 0
    elif record.value_type == "category":
        row["category_id"] = manifest.category_index(field_id, value)
    else:  # entity_ref
        target = row_of_instance.get(value)
        if target is None:
            raise EncodingError(
                "dangling_reference",
                f"{path}.value",
                f"悬空引用: {value!r} 不在当前快照的实体中",
                related_ids=(field_id, str(value)),
            )
        row["reference_entity"] = target
        row["reference_mask"] = True
    return row


def _reserved_record(
    manifest: GymManifest,
    field_id: str,
    *,
    expected_value_type: str,
    kind_id: Any,
    pile_ref: str,
) -> Any:
    """保留声明的存在性/类型/适用域检查（无目录时靠 Manifest 自身兜底）。"""
    record = manifest.field_records.get(field_id)
    if record is None:
        raise EncodingError(
            "unsupported_structure",
            "snapshot.piles",
            f"牌堆需要保留声明 {field_id!r} 才能张量化；本 Manifest 未注册该字段"
            "（使用 piles 的目录必须声明 net:pile_member 与两个 net:pile_* 字段）",
            related_ids=("I-33", "I-79", field_id, pile_ref),
        )
    if record.value_type != expected_value_type:
        raise EncodingError(
            "value_type_mismatch",
            f"manifest.field_records[{field_id}]",
            f"保留声明 {field_id!r} 的值类型必须是 {expected_value_type!r}，"
            f"得到 {record.value_type!r}",
            related_ids=(field_id, pile_ref),
        )
    if record.applies_to_entity_kinds and kind_id not in record.applies_to_entity_kinds:
        raise EncodingError(
            "field_not_applicable",
            f"manifest.field_records[{field_id}]",
            f"保留声明 {field_id!r} 的适用域 {list(record.applies_to_entity_kinds)} "
            f"不含牌堆种类 {kind_id!r}",
            related_ids=(field_id, pile_ref),
        )
    return record


def _capacity_limits(manifest: GymManifest) -> Any:
    return manifest.encoding_config.capacities


def _check_capacity_rows(limits: Any, sizes: Mapping[str, int], *, prefix: str) -> None:
    """张量块行数超限即显式失败（不静默截断）；``prefix`` 区分本模块与 A-03 的路径。"""
    for block in TENSOR_BLOCK_NAMES:
        limit = getattr(limits, CAPACITY_BY_BLOCK[block])
        if limit is None:
            continue
        rows = sizes[block]
        if rows > limit:
            raise EncodingError(
                "capacity_exceeded",
                f"{prefix}{block}",
                f"张量块 {block} 行数 {rows}（含派生行）超过配置上限 "
                f"{CAPACITY_BY_BLOCK[block]}={limit}",
                related_ids=(CAPACITY_BY_BLOCK[block], str(limit), str(rows)),
            )


def _check_capacity(manifest: GymManifest, sizes: Mapping[str, int], *, prefix: str) -> None:
    _check_capacity_rows(_capacity_limits(manifest), sizes, prefix=prefix)


# --------------------------------------------------------------------------- #
# tensorize
# --------------------------------------------------------------------------- #
def tensorize(
    snapshot: Any,
    manifest: GymManifest,
    *,
    catalog: Any = None,
) -> TensorizedObservation:
    """把一份当前快照编成确定性数组（encoder-design §5/§5.1）。

    先做 A-03 全量校验：**有错即抛首条，不返回部分张量**。``catalog`` 给出时启用共享
    L3 的声明域检查（含 I-33 保留声明、I-25 关系 position 语义），并对牌堆已知位置
    按 ``pile_type.position_schema`` 判定；缺目录时把这些未判定项写进
    :attr:`TensorizedObservation.validation_skipped`，不假装通过。
    """
    if not isinstance(manifest, GymManifest):
        raise TypeError(f"manifest 必须是 GymManifest，得到 {type(manifest).__name__}")

    report = validate_snapshot(snapshot, manifest, catalog=catalog)
    report.raise_first()

    document = _document_of(snapshot)
    catalog_document = _catalog_document(catalog)
    pile_schema_by_kind = _pile_schema_by_kind(catalog_document)

    skipped = list(report.skipped)
    if pile_schema_by_kind is None:
        skipped.append(SKIPPED_WITHOUT_CATALOG)

    # -- 实体：按 instance_id 的 UTF-8 字节序（§5 第 139 行） -------------- #
    entities_wire = list(document.get("entities") or [])
    ordered_entities = sorted(entities_wire, key=lambda entity: _utf8(entity["instance_id"]))
    row_of_instance = {entity["instance_id"]: row for row, entity in enumerate(ordered_entities)}

    kind_ids: list = []
    content_ids: list = []
    content_states: list = []
    content_masks: list = []
    content_type_table = manifest.dictionaries["content_types"]
    for row, entity in enumerate(ordered_entities):
        instance_id = entity["instance_id"]
        kind_ids.append(
            manifest.entity_kind_index(
                entity["kind_id"], path=f"snapshot.entities[{instance_id}].kind_id"
            )
        )
        state = entity["content_state"]
        content_states.append(CONTENT_STATE_IDS[state])
        if state == "known":
            content_id = entity["content_id"]
            index = content_type_table.index_of(content_id)
            if index is None:
                raise EncodingError(
                    "unknown_type",
                    f"snapshot.entities[{instance_id}].content_id",
                    f"未注册的内容类型: {content_id!r}（不在本 Manifest 的 content_types 字典中）；"
                    "目录须把该内容定义声明为 content_types，或改用已声明的内容类型 ID",
                    related_ids=("I-06", "I-19", content_id),
                )
            content_ids.append(index)
            content_masks.append(True)
        else:
            content_ids.append(PAD_INDEX)
            content_masks.append(False)
    entity_mask = [True] * len(ordered_entities)
    entities_block = Block(
        "entities",
        {
            "kind_id": tuple(kind_ids),
            "content_id": tuple(content_ids),
            "content_state": tuple(content_states),
            "content_mask": tuple(content_masks),
            "entity_mask": tuple(entity_mask),
        },
    )

    # -- 字段：wire 字段 + 派生的两个牌堆字段 ------------------------------ #
    field_rows: list = []  # (owner_row, field_id_bytes, row)
    for owner_row, entity in enumerate(ordered_entities):
        instance_id = entity["instance_id"]
        base = f"snapshot.entities[{instance_id}]"
        for value_field in sorted(entity["fields"], key=lambda item: _utf8(item["field_id"])):
            field_id = value_field["field_id"]
            record = manifest.field_records.get(field_id)
            if record is None:  # pragma: no cover - A-03 已拒绝
                raise EncodingError(
                    "unregistered_field",
                    f"{base}.fields[{field_id}]",
                    f"未注册字段: {field_id!r}",
                    related_ids=(field_id,),
                )
            field_rows.append(
                (owner_row, _utf8(field_id), _field_row(manifest, record, value_field, owner_row, row_of_instance, base))
            )

    piles_wire = list(document.get("piles") or [])
    pile_member_index: Optional[int] = manifest.index_of("relation_types", RESERVED_PILE_MEMBER)
    if piles_wire and pile_member_index is None:
        raise EncodingError(
            "unsupported_structure",
            "snapshot.piles",
            f"牌堆需要保留声明 {RESERVED_PILE_MEMBER!r} 才能张量化；本 Manifest 未注册该关系类型"
            "（使用 piles 的目录必须声明 net:pile_member 与两个 net:pile_* 字段）",
            related_ids=("I-33", "I-79", RESERVED_PILE_MEMBER),
        )

    for pile in piles_wire:
        pile_ref = pile["pile_ref"]
        pile_row = row_of_instance[pile_ref]
        pile_kind = ordered_entities[pile_row]["kind_id"]
        total_record = _reserved_record(
            manifest,
            RESERVED_PILE_TOTAL_COUNT,
            expected_value_type="number",
            kind_id=pile_kind,
            pile_ref=pile_ref,
        )
        complete_record = _reserved_record(
            manifest,
            RESERVED_PILE_MEMBERS_COMPLETE,
            expected_value_type="boolean",
            kind_id=pile_kind,
            pile_ref=pile_ref,
        )
        total_field_id = total_record.field_id
        complete_field_id = complete_record.field_id
        field_rows.append(
            (
                pile_row,
                _utf8(total_field_id),
                {
                    "owner_entity": pile_row,
                    "field_id": total_record.index,
                    "value_type": VALUE_TYPE_IDS["number"],
                    "knowledge_state": KNOWLEDGE_STATE_IDS["known"],
                    "number_value": manifest.encode_number(total_field_id, str(pile["total_count"])),
                    "boolean_value": 0,
                    "category_id": 0,
                    "reference_entity": 0,
                    "reference_mask": False,
                    "field_mask": True,
                },
            )
        )
        field_rows.append(
            (
                pile_row,
                _utf8(complete_field_id),
                {
                    "owner_entity": pile_row,
                    "field_id": complete_record.index,
                    "value_type": VALUE_TYPE_IDS["boolean"],
                    "knowledge_state": KNOWLEDGE_STATE_IDS["known"],
                    "number_value": 0.0,
                    "boolean_value": 1 if pile["members_complete"] else 0,
                    "category_id": 0,
                    "reference_entity": 0,
                    "reference_mask": False,
                    "field_mask": True,
                },
            )
        )

    field_rows.sort(key=lambda item: (item[0], item[1]))
    fields_block = Block(
        "fields",
        {
            column: tuple(row[column] for _, _, row in field_rows)
            for column in (name for name, _ in COLUMN_SCHEMAS["fields"])
        },
    )

    # -- 关系：wire 关系 + 派生的 net:pile_member -------------------------- #
    relation_rows: list = []  # (type_bytes, source, target, position_key, row, derived)
    for relation in document.get("relations") or []:
        type_id = relation["type_id"]
        type_index = manifest.relation_index(
            type_id, path=f"snapshot.relations[{type_id}].type_id"
        )
        source = row_of_instance[relation["source_ref"]]
        target = row_of_instance[relation["target_ref"]]
        has_position = "position" in relation
        position = relation["position"] if has_position else PAD_INDEX
        relation_rows.append(
            (
                _utf8(type_id),
                source,
                target,
                position if has_position else -1,
                {
                    "type_id": type_index,
                    "source": source,
                    "target": target,
                    "position": position,
                    "position_mask": has_position,
                    "relation_mask": True,
                },
                False,
            )
        )

    for pile in piles_wire:
        pile_ref = pile["pile_ref"]
        pile_row = row_of_instance[pile_ref]
        for member in pile["visible_members"]:
            relation_rows.append(
                (
                    _utf8(RESERVED_PILE_MEMBER),
                    pile_row,
                    row_of_instance[member],
                    -1,
                    {
                        "type_id": pile_member_index,
                        "source": pile_row,
                        "target": row_of_instance[member],
                        "position": PAD_INDEX,
                        "position_mask": False,
                        "relation_mask": True,
                    },
                    True,
                )
            )
    relation_rows.sort(key=lambda item: (item[0], item[1], item[2], item[3], item[5]))
    relations_block = Block(
        "relations",
        {
            column: tuple(item[4][column] for item in relation_rows)
            for column in (name for name, _ in COLUMN_SCHEMAS["relations"])
        },
    )

    # -- 已知位置：按牌堆行与绝对位置排序 ---------------------------------- #
    position_rows: list = []
    for pile in sorted(piles_wire, key=lambda item: row_of_instance[item["pile_ref"]]):
        pile_ref = pile["pile_ref"]
        pile_row = row_of_instance[pile_ref]
        positions = list(pile["known_positions"])
        if positions and pile_schema_by_kind is not None:
            pile_kind = ordered_entities[pile_row]["kind_id"]
            if pile_kind in pile_schema_by_kind and pile_schema_by_kind[pile_kind] is None:
                raise EncodingError(
                    "invalid_position",
                    f"snapshot.piles[{pile_ref}].known_positions",
                    f"牌堆种类 {pile_kind!r} 声明的 position_schema 为 null（无公开位置语义），"
                    f"不得给出 {len(positions)} 条已知位置",
                    related_ids=("I-29", pile_ref),
                )
        for position in sorted(positions, key=lambda item: item["position"]):
            position_rows.append(
                {
                    "pile_entity": pile_row,
                    "card_entity": row_of_instance[position["card_ref"]],
                    "absolute_position": position["position"],
                    "position_record_mask": True,
                }
            )
    pile_positions_block = Block(
        "pile_positions",
        {
            column: tuple(row[column] for row in position_rows)
            for column in (name for name, _ in COLUMN_SCHEMAS["pile_positions"])
        },
    )

    # -- 序列：保留观察顺序（ordinal 从 0 起连续） ------------------------- #
    sequence_rows: list = []
    for sequence in document.get("sequences") or []:
        type_id = sequence["type_id"]
        type_index = manifest.sequence_type_index(
            type_id, path=f"snapshot.sequences[{type_id}].type_id"
        )
        owner = row_of_instance[sequence["owner_ref"]]
        for ordinal, element in enumerate(sequence["elements"]):
            sequence_rows.append(
                (
                    _utf8(type_id),
                    owner,
                    ordinal,
                    {
                        "sequence_type": type_index,
                        "owner": owner,
                        "element": row_of_instance[element],
                        "ordinal": ordinal,
                        "sequence_record_mask": True,
                    },
                )
            )
    sequence_rows.sort(key=lambda item: (item[0], item[1], item[2]))
    sequences_block = Block(
        "sequences",
        {
            column: tuple(item[3][column] for item in sequence_rows)
            for column in (name for name, _ in COLUMN_SCHEMAS["sequences"])
        },
    )

    # -- 无序组：成员按公开引用（行号）排序，保留 multiplicity --------------- #
    group_rows: list = []
    for group in document.get("groups") or []:
        type_id = group["type_id"]
        type_index = manifest.group_type_index(
            type_id, path=f"snapshot.groups[{type_id}].type_id"
        )
        owner = row_of_instance[group["owner_ref"]]
        members = sorted(
            group["members"],
            key=lambda member: (row_of_instance[member["entity_ref"]], member["multiplicity"]),
        )
        for member in members:
            group_rows.append(
                (
                    _utf8(type_id),
                    owner,
                    row_of_instance[member["entity_ref"]],
                    member["multiplicity"],
                    {
                        "group_type": type_index,
                        "owner": owner,
                        "member": row_of_instance[member["entity_ref"]],
                        "multiplicity": member["multiplicity"],
                        "group_record_mask": True,
                    },
                )
            )
    group_rows.sort(key=lambda item: (item[0], item[1], item[2], item[3]))
    groups_block = Block(
        "groups",
        {
            column: tuple(item[4][column] for item in group_rows)
            for column in (name for name, _ in COLUMN_SCHEMAS["groups"])
        },
    )

    # -- 候选：保留 wire 顺序与句柄映射（§5 第 139 行） --------------------- #
    candidate_rows: list = []
    candidate_field_rows: list = []  # (owner_candidate, field_id_bytes, row)
    candidate_rows_map: dict = {}
    for index, action in enumerate(document.get("legal_actions") or []):
        candidate_id = action["candidate_id"]
        candidate_rows_map[candidate_id] = index
        candidate_rows.append(
            {
                "action_type": manifest.action_type_index(
                    action["action_type"],
                    path=f"snapshot.legal_actions[{candidate_id}].action_type",
                ),
                "candidate_mask": True,
            }
        )
        for value_field in sorted(action["fields"], key=lambda item: _utf8(item["field_id"])):
            field_id = value_field["field_id"]
            record = manifest.field_records.get(field_id)
            if record is None:  # pragma: no cover - A-03 已拒绝
                raise EncodingError(
                    "unregistered_field",
                    f"snapshot.legal_actions[{candidate_id}].fields[{field_id}]",
                    f"未注册字段: {field_id!r}",
                    related_ids=(field_id,),
                )
            candidate_field_rows.append(
                (
                    index,
                    _utf8(field_id),
                    _field_row(
                        manifest,
                        record,
                        value_field,
                        index,
                        row_of_instance,
                        f"snapshot.legal_actions[{candidate_id}]",
                        owner_column="owner_candidate",
                    ),
                )
            )
    candidate_field_rows.sort(key=lambda item: (item[0], item[1]))
    candidates_block = Block(
        "candidates",
        {
            column: tuple(row[column] for row in candidate_rows)
            for column in (name for name, _ in COLUMN_SCHEMAS["candidates"])
        },
    )
    candidate_fields_block = Block(
        "candidate_fields",
        {
            column: tuple(item[2][column] for item in candidate_field_rows)
            for column in (name for name, _ in COLUMN_SCHEMAS["candidate_fields"])
        },
    )

    blocks = {
        "entities": entities_block,
        "fields": fields_block,
        "relations": relations_block,
        "pile_positions": pile_positions_block,
        "sequences": sequences_block,
        "groups": groups_block,
        "candidates": candidates_block,
        "candidate_fields": candidate_fields_block,
    }

    sizes = {name: blocks[name].n for name in TENSOR_BLOCK_NAMES}
    _check_capacity(manifest, sizes, prefix="tensor.")

    identity = manifest.environment_identity
    metadata = {
        "protocol_version": identity.protocol_version,
        "ruleset_id": identity.ruleset_id,
        "catalog_id": identity.catalog_id,
        "backend_profile_id": identity.backend_profile_id,
        "gym_id": manifest.gym_id,
        "dictionary_version": manifest.dictionary_version,
        "manifest_digest": manifest.manifest_digest,
        "tensor_layout_version": manifest.tensor_layout_version,
        "scenario_profile": manifest.scenario_profile,
        "source_catalog_digest": manifest.source_catalog_digest,
        "encoding_config_digest": manifest.encoding_config_digest,
        "config_version": CONFIG_VERSION,
        "number_channel_dtype": manifest.encoding_config.number_channel_dtype,
        # 容量口径随样本带走：collate 要在补齐前复核上限，而不必再持有 Manifest。
        "capacities": manifest.encoding_config.capacities.to_wire(),
        "session_id": document.get("session_id"),
        "observation_id": document.get("observation_id"),
        "status": document.get("status"),
    }

    return TensorizedObservation(
        metadata=metadata,
        blocks=blocks,
        reference_map=ReferenceMap(
            entity_rows=dict(row_of_instance), candidate_rows=candidate_rows_map
        ),
        validation_skipped=tuple(skipped),
    )


# --------------------------------------------------------------------------- #
# collate
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class TensorizedBatch:
    """B 轴拼批结果：每个块 ``[B, rows]``，行号为**样本内局部索引**。

    读一行必须同时给出 ``b``：:meth:`row` 只做 `(b, row)` 寻址，语义解析走
    :meth:`entity_instance` / :meth:`candidate_id`；不提供把行号跨样本解析的入口，
    因此"两个样本同槽位"不会被误连（见 ``collate`` 与报告里的反例）。
    """

    metadata: Mapping[str, Any]
    blocks: Mapping[str, BatchBlock]
    reference_maps: tuple
    observations: tuple

    def __post_init__(self) -> None:
        if tuple(self.blocks) != TENSOR_BLOCK_NAMES:
            raise ValueError(f"blocks 必须恰为 {list(TENSOR_BLOCK_NAMES)}，得到 {list(self.blocks)}")
        if len(self.reference_maps) != len(self.observations):
            raise ValueError("reference_maps 必须与 observations 等长")
        object.__setattr__(self, "metadata", MappingProxyType(dict(self.metadata)))

    # -- 读取面 ---------------------------------------------------------- #
    @property
    def batch_size(self) -> int:
        return len(self.observations)

    @property
    def sizes(self) -> dict:
        return {name: self.blocks[name].rows for name in TENSOR_BLOCK_NAMES}

    def block(self, name: str) -> BatchBlock:
        try:
            return self.blocks[name]
        except KeyError as exc:
            raise KeyError(f"未知张量块 {name!r}；可用 {list(self.blocks)}") from exc

    def column(self, block: str, name: str) -> tuple:
        return self.block(block).column(name)

    def lengths(self, block: str) -> tuple:
        return self.block(block).lengths

    def row(self, block: str, b: int, index: int) -> dict:
        return self.block(block).row(b, index)

    def is_padding(self, block: str, b: int, index: int) -> bool:
        return self.block(block).is_padding(b, index)

    def reference_map(self, b: int) -> ReferenceMap:
        return self.reference_maps[b]

    def entity_instance(self, b: int, row: int) -> Optional[str]:
        """样本 ``b`` 的第 ``row`` 行实体是哪个实例（越界/未映射 → None）。"""
        return self.reference_maps[b].instance_of_row(row)

    def candidate_id(self, b: int, row: int) -> Optional[str]:
        return self.reference_maps[b].candidate_of_row(row)

    # -- 解引用守卫（索引只在本 B 样本内有效） ----------------------------- #
    def resolve_entity(self, b: int, row: int) -> str:
        """把样本 ``b`` 的实体行号解析成实例 ID；越界/非法即 ``dangling_reference``。"""
        if not self._valid_sample(b):
            raise EncodingError(
                "dangling_reference",
                f"samples[{b}]",
                f"样本下标 {b} 越界（本批 B={self.batch_size}）",
                related_ids=(str(b), str(self.batch_size)),
            )
        instances = self.reference_maps[b].entity_count
        if isinstance(row, bool) or not isinstance(row, int) or not (0 <= row < instances):
            raise EncodingError(
                "dangling_reference",
                f"samples[{b}].entities[{row}]",
                f"实体行号 {row!r} 越出该样本的行范围 0..{instances - 1}"
                "（索引列只在同一 B 样本内解引用，不做全局偏移）",
                related_ids=(str(b), str(row)),
            )
        resolved = self.reference_maps[b].instance_of_row(row)
        if resolved is None:  # pragma: no cover - 行数与映射由 tensorize 保证一致
            raise EncodingError(
                "dangling_reference",
                f"samples[{b}].entities[{row}]",
                f"样本 {b} 的第 {row} 行没有实例映射",
                related_ids=(str(b), str(row)),
            )
        return resolved

    def resolve_candidate(self, b: int, row: int) -> str:
        """把样本 ``b`` 的候选行号解析成 candidate_id；越界/非法即 ``dangling_reference``。"""
        if not self._valid_sample(b):
            raise EncodingError(
                "dangling_reference",
                f"samples[{b}]",
                f"样本下标 {b} 越界（本批 B={self.batch_size}）",
                related_ids=(str(b), str(self.batch_size)),
            )
        candidates = self.reference_maps[b].candidate_count
        if isinstance(row, bool) or not isinstance(row, int) or not (0 <= row < candidates):
            raise EncodingError(
                "dangling_reference",
                f"samples[{b}].candidates[{row}]",
                f"候选行号 {row!r} 越出该样本的候选行范围 0..{candidates - 1}",
                related_ids=(str(b), str(row)),
            )
        resolved = self.reference_maps[b].candidate_of_row(row)
        if resolved is None:  # pragma: no cover
            raise EncodingError(
                "dangling_reference",
                f"samples[{b}].candidates[{row}]",
                f"样本 {b} 的第 {row} 行没有候选映射",
                related_ids=(str(b), str(row)),
            )
        return resolved

    def resolve_row(self, block: str, b: int, index: int) -> Optional[dict]:
        """校验并解析一行里的索引列。

        * 该行是补齐行（``mask=false``）→ 返回 ``None``：**不**把填零的索引列解析成
          本样本第 0 行实体/候选（这正是 padding 误连的经典形态）；
        * 真实行 → 返回 ``{索引列: 解析出的实例/候选 ID}``；
        * 索引越出本样本行范围 → ``dangling_reference``，路径点名
          ``samples[b].<block>[index].<column>``。
        """
        block_object = self.block(block)
        if not self._valid_sample(b):
            raise EncodingError(
                "dangling_reference",
                f"samples[{b}]",
                f"样本下标 {b} 越界（本批 B={self.batch_size}）",
                related_ids=(str(b), str(self.batch_size)),
            )
        if isinstance(index, bool) or not isinstance(index, int) or not (0 <= index < block_object.rows):
            raise EncodingError(
                "dangling_reference",
                f"samples[{b}].{block}[{index}]",
                f"行号 {index!r} 越出本批 {block} 块行数 {block_object.rows}",
                related_ids=(block, str(b), str(index)),
            )
        if block_object.is_padding(b, index):
            return None
        resolved: dict = {}
        for column in INDEX_COLUMNS[block]:
            value = block_object.row(b, index)[column]
            is_candidate = column == "owner_candidate"
            bound = (
                self.reference_maps[b].candidate_count
                if is_candidate
                else self.reference_maps[b].entity_count
            )
            if isinstance(value, bool) or not isinstance(value, int) or not (0 <= value < bound):
                raise EncodingError(
                    "dangling_reference",
                    f"samples[{b}].{block}[{index}].{column}",
                    f"{column}={value!r} 越出本样本行范围 0..{bound - 1}"
                    "（引用只在同一 B 样本内解引用）",
                    related_ids=(block, str(b), str(index), column),
                )
            table = self.reference_maps[b].candidate_of_row if is_candidate else self.reference_maps[b].instance_of_row
            target = table(value)
            if target is None:  # pragma: no cover
                raise EncodingError(
                    "dangling_reference",
                    f"samples[{b}].{block}[{index}].{column}",
                    f"{column}={value} 没有映射（本样本第 {value} 行不存在）",
                    related_ids=(block, str(b), str(index), column),
                )
            resolved[column] = target
        return resolved

    def _valid_sample(self, b: int) -> bool:
        return isinstance(b, int) and not isinstance(b, bool) and 0 <= b < self.batch_size

    def compat_key(self) -> tuple:
        return self.observations[0].compat_key()

    def describe(self) -> dict:
        return {
            "metadata": dict(self.metadata),
            "batch_size": self.batch_size,
            "blocks": {name: self.blocks[name].describe() for name in TENSOR_BLOCK_NAMES},
            "reference_maps": [mapping.to_wire() for mapping in self.reference_maps],
        }


def _compat_problem(sample: TensorizedObservation, baseline: TensorizedObservation) -> Optional[str]:
    for field, expected, actual in zip(
        TensorizedObservation.COMPAT_FIELDS, baseline.compat_key(), sample.compat_key()
    ):
        if expected != actual:
            return f"{field}: {actual!r} != {expected!r}"
    return None


def collate(encoded_samples: Sequence[TensorizedObservation]) -> TensorizedBatch:
    """把同一 Manifest/布局/数值配置的多个样本拼成 B 轴批（§5.1）。

    规则：

    * 拒绝空批、非 :class:`TensorizedObservation` 元素（``unsupported_structure``）；
    * 身份不一致即 ``version_mismatch`` 并点名第一个不同的分量（不自动对齐、不降级）；
    * **补齐前**逐样本核对张量块行数上限（``samples[i].tensor.<block>``）；
    * 每个块按本批最大行数补齐，补齐行 mask=false 且状态/类型列写 pad=0；行索引仍是
      各样本自己的局部行号（不做全局偏移）。
    """
    if isinstance(encoded_samples, (str, bytes)) or not isinstance(encoded_samples, Sequence):
        raise EncodingError(
            "unsupported_structure",
            "samples",
            f"collate 需要样本序列，得到 {type(encoded_samples).__name__}",
        )
    samples = list(encoded_samples)
    if not samples:
        raise EncodingError("unsupported_structure", "samples", "collate 至少需要一个样本")
    for index, sample in enumerate(samples):
        if not isinstance(sample, TensorizedObservation):
            raise EncodingError(
                "unsupported_structure",
                f"samples[{index}]",
                f"collate 只接受 TensorizedObservation，得到 {type(sample).__name__}",
            )

    baseline = samples[0]
    for index, sample in enumerate(samples[1:], start=1):
        problem = _compat_problem(sample, baseline)
        if problem is not None:
            raise EncodingError(
                "version_mismatch",
                f"samples[{index}].metadata",
                f"拼批身份不一致（{problem}）；拒绝混合 Manifest/布局/数值配置",
                related_ids=(sample.metadata["dictionary_version"], baseline.metadata["dictionary_version"]),
            )

    # 补齐前核对资源上限：limit 针对单个样本的真实行数（由 tensorize 写入 metadata）。
    for index, sample in enumerate(samples):
        limits = CapacityLimits.from_wire(
            sample.metadata["capacities"], path=f"samples[{index}].metadata.capacities"
        )
        _check_capacity_rows(limits, sample.sizes, prefix=f"samples[{index}].tensor.")

    blocks: dict = {}
    for name in TENSOR_BLOCK_NAMES:
        schema = COLUMN_SCHEMAS[name]
        lengths = tuple(sample.blocks[name].n for sample in samples)
        rows = max(lengths) if lengths else 0
        columns = {}
        for column, dtype in schema:
            samples_columns = []
            for sample in samples:
                values = list(sample.blocks[name].column(column))
                values.extend(_pad_value(dtype) for _ in range(rows - len(values)))
                samples_columns.append(tuple(values))
            columns[column] = tuple(samples_columns)
        blocks[name] = BatchBlock(name=name, lengths=lengths, columns=columns)

    return TensorizedBatch(
        metadata=baseline.metadata,
        blocks=blocks,
        reference_maps=tuple(sample.reference_map for sample in samples),
        observations=tuple(samples),
    )


__all__ = [
    "CAPACITY_BY_BLOCK",
    "COLUMN_SCHEMAS",
    "CONTENT_STATE_IDS",
    "INDEX_COLUMNS",
    "KNOWLEDGE_STATE_IDS",
    "MASK_COLUMNS",
    "PAD_INDEX",
    "RESERVED_PILE_DECLARATIONS",
    "RESERVED_PILE_MEMBER",
    "RESERVED_PILE_MEMBERS_COMPLETE",
    "RESERVED_PILE_TOTAL_COUNT",
    "SKIPPED_WITHOUT_CATALOG",
    "TENSOR_BLOCK_NAMES",
    "VALUE_TYPE_IDS",
    "BatchBlock",
    "Block",
    "ReferenceMap",
    "TensorizedBatch",
    "TensorizedObservation",
    "collate",
    "tensorize",
]
