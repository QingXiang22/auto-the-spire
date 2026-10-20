"""GymManifest：固定训练环境的字典（encoder-design §2；开工清单 A-02）。

本模块实现 ``build_manifest`` / ``save_manifest`` / ``load_manifest``：

* **固定名称排序**：每张字典按完整名称的 UTF-8 字节序排序，索引从 1 开始，
  **索引 0 保留给补齐**；名称映射随文件显式保存，加载时**不重建**；
* **类别索引**：内容类型、实体种类、字段、关系、序列/组、动作类型与类别值各有一张表；
  字段记录继承目录声明的值类型/适用域/必需性/公开条件（description 不进入字典）；
* **变换**：数值变换与容差属于显式 :class:`EncodingConfig`，随 Manifest 持久化；
* **exact identity**：``environment_identity`` 四键必须完全相等（I-01/I-02），
  不自动降级、不做前缀匹配；
* **版本**：``dictionary_version`` 只由目录内容身份决定（与声明顺序无关、与
  description 无关）；旧字典文件按原样加载并保留其原映射，新快照配旧字典报
  ``version_mismatch``，不返回编码。

不维护历史、不保存 ``known_top``、不读取隐藏状态、不执行规则、不导入活动 client/core。
协议结构与判定复用共享资产（``docs/contracts/net/0.2`` + ``tools/contract-check``），
不另写第二套 codec。
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Optional

from ai_the_spire.contracts import layers
from ai_the_spire.contracts.types import (
    NUMBER_FORMATS,
    VALUE_TYPES,
    VISIBILITIES,
    FieldDecl,
    Identity,
)
from ai_the_spire.contracts.views import PublicCatalog

from .config import EncodingConfig
from .errors import EncodingError

#: Manifest 文件格式版本。只有一个版本时也显式记录：更高版本必须显式拒绝。
MANIFEST_FORMAT_VERSION = 1

#: 张量布局版本（encoder-design §5.1 的块与列）。
TENSOR_LAYOUT_VERSION = "ai-tensor-layout-v1"

#: 字典版本前缀 + 目录内容摘要。
DICTIONARY_VERSION_PREFIX = "ai-dict-v1:"

#: 进入模型特征的字典表（encoder-design §2）。
DICTIONARY_TABLES = (
    "entity_kinds",
    "content_types",
    "fields",
    "relation_types",
    "sequence_types",
    "group_types",
    "action_types",
    "categories",
)

#: 刻意**不**进入特征字典的声明表，理由见模块文档与报告：
#: * ``pile_types``：牌堆语义通过实体 ``kind_id`` 保留（§2），牌堆类型不单独编码；
#: * ``terminal_reasons``：只校验结果事实，不把终局 reason 变成决策输入（§2）。
EXCLUDED_DECLARATION_TABLES = ("pile_types", "terminal_reasons")

_MANIFEST_KEYS = frozenset(
    {
        "manifest_format_version",
        "tensor_layout_version",
        "gym_id",
        "dictionary_version",
        "source_catalog_digest",
        "encoding_config_digest",
        "manifest_digest",
        "environment_identity",
        "scenario_profile",
        "encoding_config",
        "dictionaries",
        "field_records",
    }
)
_DICTIONARY_KEYS = frozenset({"names", "by_name"})
_FIELD_RECORD_KEYS = frozenset(
    {
        "index",
        "value_type",
        "number_format",
        "required",
        "visibility",
        "category_ids",
        "target_kind_ids",
        "visibility_check_ids",
        "applies_to",
    }
)
_APPLIES_KEYS = frozenset({"entity_kinds", "action_types", "terminal_reasons"})
_IDENTITY_KEYS = ("protocol_version", "ruleset_id", "catalog_id", "backend_profile_id")


def _utf8(text: str) -> bytes:
    return text.encode("utf-8")


def _require_non_empty_str(value: Any, *, path: str) -> str:
    if not isinstance(value, str) or not value:
        raise EncodingError("invalid_manifest", path, f"必须是非空字符串，得到 {value!r}")
    return value


def _require_exact_keys(doc: Mapping, expected: frozenset, *, path: str) -> None:
    keys = set(doc)
    if keys != expected:
        missing = sorted(expected - keys)
        extra = sorted(keys - expected)
        raise EncodingError(
            "invalid_manifest",
            path,
            f"键集合不匹配（缺少 {missing}，多出 {extra}）",
        )


# --------------------------------------------------------------------------- #
# 字典
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class Dictionary:
    """一张名称→索引的字典；索引 0 保留给补齐，真实条目索引为位置 + 1。"""

    table: str
    names: tuple[str, ...]

    @property
    def size(self) -> int:
        return len(self.names)

    def index_of(self, name: str) -> Optional[int]:
        try:
            return self.names.index(name) + 1
        except ValueError:
            return None

    def name_of(self, index: int) -> Optional[str]:
        if 1 <= index <= len(self.names):
            return self.names[index - 1]
        return None

    def to_wire(self) -> dict:
        """``names`` 给出索引顺序，``by_name`` 显式保存映射（加载时不重建）。"""
        return {
            "names": list(self.names),
            "by_name": {name: position + 1 for position, name in enumerate(self.names)},
        }

    @classmethod
    def from_wire(cls, table: str, doc: Any) -> "Dictionary":
        path = f"manifest.dictionaries.{table}"
        if not isinstance(doc, Mapping):
            raise EncodingError("invalid_manifest", path, f"字典必须是对象，得到 {type(doc).__name__}")
        _require_exact_keys(doc, _DICTIONARY_KEYS, path=path)
        names = doc["names"]
        by_name = doc["by_name"]
        if not isinstance(names, list) or not isinstance(by_name, Mapping):
            raise EncodingError("invalid_manifest", path, "names 必须是数组、by_name 必须是对象")
        seen: set[str] = set()
        for name in names:
            if not isinstance(name, str) or not name:
                raise EncodingError("invalid_manifest", f"{path}.names", f"名称必须是非空字符串: {name!r}")
            if name in seen:
                raise EncodingError(
                    "invalid_manifest", f"{path}.names", f"名称重复: {name!r}", related_ids=[name]
                )
            seen.add(name)
        expected_map = {name: position + 1 for position, name in enumerate(names)}
        if dict(by_name) != expected_map:
            raise EncodingError(
                "invalid_manifest",
                f"{path}.by_name",
                "显式名称映射与 names 顺序不一致（拒绝按新顺序重建）",
            )
        return cls(table=table, names=tuple(names))


def _build_dictionary(table: str, names: list[str], *, dedupe: bool = False) -> Dictionary:
    """按完整名称的 UTF-8 字节序排序。

    ``dedupe=False``（声明表）：重复声明 ID 是 ``duplicate_identity``；
    ``dedupe=True``（类别值）：同一个类别 ID 出现在多个字段的 ``category_ids`` 里是
    合法的，按名称合并为一条。
    """
    ordered = sorted(set(names) if dedupe else names, key=_utf8)
    if not dedupe:
        for previous, current in zip(ordered, ordered[1:]):
            if previous == current:
                raise EncodingError(
                    "duplicate_identity",
                    f"catalog.declarations.{table}",
                    f"声明 ID 重复: {current!r}",
                    related_ids=[current],
                )
    return Dictionary(table=table, names=tuple(ordered))


# --------------------------------------------------------------------------- #
# 字段记录
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class FieldRecord:
    """目录字段声明的编码侧继承（值类型、适用域、必需性、公开条件）。"""

    field_id: str
    index: int
    value_type: str
    number_format: Optional[str]
    required: bool
    visibility: str
    category_ids: tuple[str, ...]
    target_kind_ids: tuple[str, ...]
    visibility_check_ids: tuple[str, ...]
    applies_to_entity_kinds: tuple[str, ...]
    applies_to_action_types: tuple[str, ...]
    applies_to_terminal_reasons: tuple[str, ...]

    @classmethod
    def from_declaration(cls, declaration: FieldDecl, index: int) -> "FieldRecord":
        return cls(
            field_id=declaration.id,
            index=index,
            value_type=declaration.value_type,
            number_format=declaration.number_format,
            required=bool(declaration.required),
            visibility=declaration.visibility,
            category_ids=tuple(declaration.category_ids),
            target_kind_ids=tuple(declaration.target_kind_ids),
            visibility_check_ids=tuple(declaration.visibility_check_ids),
            applies_to_entity_kinds=tuple(declaration.applies_to.entity_kinds),
            applies_to_action_types=tuple(declaration.applies_to.action_types),
            applies_to_terminal_reasons=tuple(declaration.applies_to.terminal_reasons),
        )

    def to_wire(self) -> dict:
        return {
            "index": self.index,
            "value_type": self.value_type,
            "number_format": self.number_format,
            "required": self.required,
            "visibility": self.visibility,
            "category_ids": list(self.category_ids),
            "target_kind_ids": list(self.target_kind_ids),
            "visibility_check_ids": list(self.visibility_check_ids),
            "applies_to": {
                "entity_kinds": list(self.applies_to_entity_kinds),
                "action_types": list(self.applies_to_action_types),
                "terminal_reasons": list(self.applies_to_terminal_reasons),
            },
        }

    @classmethod
    def from_wire(cls, field_id: str, doc: Any, *, categories: Dictionary) -> "FieldRecord":
        path = f"manifest.field_records[{field_id}]"
        if not isinstance(doc, Mapping):
            raise EncodingError("invalid_manifest", path, f"字段记录必须是对象，得到 {type(doc).__name__}")
        _require_exact_keys(doc, _FIELD_RECORD_KEYS, path=path)
        applies = doc["applies_to"]
        if not isinstance(applies, Mapping):
            raise EncodingError("invalid_manifest", f"{path}.applies_to", "applies_to 必须是对象")
        _require_exact_keys(applies, _APPLIES_KEYS, path=f"{path}.applies_to")

        def string_list(value: Any, *, where: str) -> tuple[str, ...]:
            if not isinstance(value, list) or any(not isinstance(item, str) or not item for item in value):
                raise EncodingError("invalid_manifest", where, f"必须是非空字符串数组，得到 {value!r}")
            return tuple(value)

        index = doc["index"]
        if isinstance(index, bool) or not isinstance(index, int) or index <= 0:
            raise EncodingError("invalid_manifest", f"{path}.index", f"索引必须是正整数，得到 {index!r}")
        value_type = doc["value_type"]
        if value_type not in VALUE_TYPES:
            raise EncodingError(
                "invalid_manifest", f"{path}.value_type", f"未知值类型 {value_type!r}", related_ids=[field_id]
            )
        visibility = doc["visibility"]
        if visibility not in VISIBILITIES:
            raise EncodingError("invalid_manifest", f"{path}.visibility", f"未知 visibility {visibility!r}")
        required = doc["required"]
        if not isinstance(required, bool):
            raise EncodingError("invalid_manifest", f"{path}.required", f"required 必须是布尔，得到 {required!r}")

        number_format = doc["number_format"]
        category_ids = string_list(doc["category_ids"], where=f"{path}.category_ids")
        target_kind_ids = string_list(doc["target_kind_ids"], where=f"{path}.target_kind_ids")
        visibility_check_ids = string_list(
            doc["visibility_check_ids"], where=f"{path}.visibility_check_ids"
        )

        if value_type == "number":
            if number_format not in NUMBER_FORMATS:
                raise EncodingError(
                    "invalid_manifest", f"{path}.number_format", f"number 字段的 number_format 非法: {number_format!r}"
                )
        elif number_format is not None:
            raise EncodingError(
                "invalid_manifest", f"{path}.number_format", f"{value_type} 字段不得带 number_format"
            )
        if value_type == "category":
            if not category_ids:
                raise EncodingError("invalid_manifest", f"{path}.category_ids", "category 字段必须列出类别 ID")
            for category_id in category_ids:
                if categories.index_of(category_id) is None:
                    raise EncodingError(
                        "invalid_manifest",
                        f"{path}.category_ids",
                        f"类别 ID 不在类别字典中: {category_id!r}",
                        related_ids=[category_id],
                    )
        elif category_ids:
            raise EncodingError("invalid_manifest", f"{path}.category_ids", f"{value_type} 字段不得带类别 ID")
        if value_type == "entity_ref":
            if not target_kind_ids:
                raise EncodingError("invalid_manifest", f"{path}.target_kind_ids", "entity_ref 字段必须指定目标种类")
        elif target_kind_ids:
            raise EncodingError("invalid_manifest", f"{path}.target_kind_ids", f"{value_type} 字段不得带目标种类")
        if (visibility == "conditional") != bool(visibility_check_ids):
            raise EncodingError(
                "invalid_manifest",
                f"{path}.visibility_check_ids",
                "visibility=conditional 必须带非空机制检查 ID，其余必须为空",
            )

        return cls(
            field_id=field_id,
            index=index,
            value_type=value_type,
            number_format=number_format,
            required=required,
            visibility=visibility,
            category_ids=category_ids,
            target_kind_ids=target_kind_ids,
            visibility_check_ids=visibility_check_ids,
            applies_to_entity_kinds=string_list(
                applies["entity_kinds"], where=f"{path}.applies_to.entity_kinds"
            ),
            applies_to_action_types=string_list(
                applies["action_types"], where=f"{path}.applies_to.action_types"
            ),
            applies_to_terminal_reasons=string_list(
                applies["terminal_reasons"], where=f"{path}.applies_to.terminal_reasons"
            ),
        )


# --------------------------------------------------------------------------- #
# Manifest
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class GymManifest:
    """固定 Gym 的字典 + 数值配置 + 精确环境身份。"""

    manifest_format_version: int
    tensor_layout_version: str
    gym_id: str
    dictionary_version: str
    source_catalog_digest: str
    encoding_config_digest: str
    manifest_digest: str
    environment_identity: Identity
    scenario_profile: Optional[str]
    encoding_config: EncodingConfig
    dictionaries: Mapping[str, Dictionary]
    field_records: Mapping[str, FieldRecord]

    # -- 序列化 ---------------------------------------------------------- #
    def _payload(self) -> dict:
        return {
            "manifest_format_version": self.manifest_format_version,
            "tensor_layout_version": self.tensor_layout_version,
            "gym_id": self.gym_id,
            "dictionary_version": self.dictionary_version,
            "source_catalog_digest": self.source_catalog_digest,
            "encoding_config_digest": self.encoding_config_digest,
            "manifest_digest": self.manifest_digest,
            "environment_identity": self.environment_identity.to_wire(),
            "scenario_profile": self.scenario_profile,
            "encoding_config": self.encoding_config.to_wire(),
            "dictionaries": {
                table: self.dictionaries[table].to_wire() for table in DICTIONARY_TABLES
            },
            "field_records": {
                field_id: self.field_records[field_id].to_wire()
                for field_id in sorted(self.field_records)
            },
        }

    def to_wire(self) -> dict:
        return self._payload()

    def compute_digest(self) -> str:
        payload = self._payload()
        payload.pop("manifest_digest")
        return layers.digest_bytes(layers.canonical_bytes(payload))

    @classmethod
    def from_wire(cls, doc: Any, *, origin: str = "<document>") -> "GymManifest":
        path = f"manifest({origin})"
        if not isinstance(doc, Mapping):
            raise EncodingError("invalid_manifest", path, f"Manifest 必须是对象，得到 {type(doc).__name__}")
        _require_exact_keys(doc, _MANIFEST_KEYS, path=path)

        format_version = doc["manifest_format_version"]
        if isinstance(format_version, bool) or not isinstance(format_version, int) or format_version < 1:
            raise EncodingError(
                "invalid_manifest", f"{path}.manifest_format_version", f"非法格式版本: {format_version!r}"
            )
        if format_version > MANIFEST_FORMAT_VERSION:
            raise EncodingError(
                "version_mismatch",
                f"{path}.manifest_format_version",
                f"文件格式版本 {format_version} 高于本实现支持的 {MANIFEST_FORMAT_VERSION}",
            )

        raw_dictionaries = doc["dictionaries"]
        if not isinstance(raw_dictionaries, Mapping):
            raise EncodingError("invalid_manifest", f"{path}.dictionaries", "字典集合必须是对象")
        if set(raw_dictionaries) != set(DICTIONARY_TABLES):
            raise EncodingError(
                "invalid_manifest",
                f"{path}.dictionaries",
                f"字典表必须恰为 {list(DICTIONARY_TABLES)}，得到 {sorted(raw_dictionaries)}",
            )
        dictionaries = {
            table: Dictionary.from_wire(table, raw_dictionaries[table]) for table in DICTIONARY_TABLES
        }

        raw_records = doc["field_records"]
        if not isinstance(raw_records, Mapping):
            raise EncodingError("invalid_manifest", f"{path}.field_records", "字段记录必须是对象")
        field_records: dict[str, FieldRecord] = {}
        fields_dictionary = dictionaries["fields"]
        for field_id, record_doc in raw_records.items():
            record = FieldRecord.from_wire(field_id, record_doc, categories=dictionaries["categories"])
            index = fields_dictionary.index_of(field_id)
            if index is None:
                raise EncodingError(
                    "invalid_manifest",
                    f"{path}.field_records[{field_id}]",
                    "字段记录不在字段字典中",
                    related_ids=[field_id],
                )
            if index != record.index:
                raise EncodingError(
                    "invalid_manifest",
                    f"{path}.field_records[{field_id}].index",
                    f"与字段字典索引不一致: {record.index} != {index}",
                    related_ids=[field_id],
                )
            field_records[field_id] = record
        if len(field_records) != fields_dictionary.size:
            raise EncodingError(
                "invalid_manifest",
                f"{path}.field_records",
                f"字段记录数 {len(field_records)} 与字段字典 {fields_dictionary.size} 不一致",
            )

        identity_doc = doc["environment_identity"]
        if not isinstance(identity_doc, Mapping) or set(identity_doc) != set(_IDENTITY_KEYS):
            raise EncodingError(
                "invalid_manifest",
                f"{path}.environment_identity",
                f"环境身份必须恰为 {list(_IDENTITY_KEYS)}",
            )
        try:
            identity = Identity.from_wire(identity_doc)
        except (KeyError, TypeError) as exc:
            raise EncodingError(
                "invalid_manifest", f"{path}.environment_identity", f"环境身份不合法: {exc}"
            ) from exc

        scenario_profile = doc["scenario_profile"]
        if scenario_profile is not None:
            _require_non_empty_str(scenario_profile, path=f"{path}.scenario_profile")

        encoding_config = EncodingConfig.from_wire(doc["encoding_config"], path=f"{path}.encoding_config")
        config_digest = encoding_config.digest()
        if doc["encoding_config_digest"] != config_digest:
            raise EncodingError(
                "invalid_manifest",
                f"{path}.encoding_config_digest",
                f"摘要与配置不一致: {doc['encoding_config_digest']!r} != {config_digest!r}",
            )

        source_catalog_digest = _require_non_empty_str(
            doc["source_catalog_digest"], path=f"{path}.source_catalog_digest"
        )
        dictionary_version = _require_non_empty_str(
            doc["dictionary_version"], path=f"{path}.dictionary_version"
        )
        if dictionary_version != DICTIONARY_VERSION_PREFIX + source_catalog_digest:
            raise EncodingError(
                "invalid_manifest",
                f"{path}.dictionary_version",
                f"字典版本与目录摘要不一致: {dictionary_version!r}",
            )

        manifest = cls(
            manifest_format_version=format_version,
            tensor_layout_version=_require_non_empty_str(
                doc["tensor_layout_version"], path=f"{path}.tensor_layout_version"
            ),
            gym_id=_require_non_empty_str(doc["gym_id"], path=f"{path}.gym_id"),
            dictionary_version=dictionary_version,
            source_catalog_digest=source_catalog_digest,
            encoding_config_digest=config_digest,
            manifest_digest=_require_non_empty_str(doc["manifest_digest"], path=f"{path}.manifest_digest"),
            environment_identity=identity,
            scenario_profile=scenario_profile,
            encoding_config=encoding_config,
            dictionaries=dictionaries,
            field_records=field_records,
        )
        expected_digest = manifest.compute_digest()
        if manifest.manifest_digest != expected_digest:
            raise EncodingError(
                "invalid_manifest",
                f"{path}.manifest_digest",
                f"文件摘要与内容不一致: {manifest.manifest_digest!r} != {expected_digest!r}",
            )
        return manifest

    # -- 索引 ------------------------------------------------------------ #
    def index_of(self, table: str, name: str) -> Optional[int]:
        dictionary = self.dictionaries.get(table)
        if dictionary is None:
            raise EncodingError("unknown_type", f"manifest.dictionaries.{table}", f"未知字典表 {table!r}")
        return dictionary.index_of(name)

    def require_index(self, table: str, name: str, *, path: Optional[str] = None) -> int:
        index = self.index_of(table, name)
        if index is None:
            raise EncodingError(
                "unknown_type",
                path or f"{table}[{name}]",
                f"未注册的 {table} 类型/值: {name!r}",
                related_ids=[name],
            )
        return index

    def entity_kind_index(self, kind_id: str, *, path: Optional[str] = None) -> int:
        return self.require_index("entity_kinds", kind_id, path=path)

    def content_type_index(self, content_type_id: str, *, path: Optional[str] = None) -> int:
        return self.require_index("content_types", content_type_id, path=path)

    def field_index(self, field_id: str, *, path: Optional[str] = None) -> int:
        record = self.field_records.get(field_id)
        if record is None:
            raise EncodingError(
                "unregistered_field",
                path or f"fields[{field_id}]",
                f"未注册字段: {field_id!r}",
                related_ids=[field_id],
            )
        return record.index

    def relation_index(self, type_id: str, *, path: Optional[str] = None) -> int:
        return self.require_index("relation_types", type_id, path=path)

    def sequence_type_index(self, type_id: str, *, path: Optional[str] = None) -> int:
        return self.require_index("sequence_types", type_id, path=path)

    def group_type_index(self, type_id: str, *, path: Optional[str] = None) -> int:
        return self.require_index("group_types", type_id, path=path)

    def action_type_index(self, action_type: str, *, path: Optional[str] = None) -> int:
        return self.require_index("action_types", action_type, path=path)

    def category_index(self, field_id: str, category_id: str) -> int:
        """类别值索引：先按目录声明判定适用性，再查类别字典。"""
        record = self.field_records.get(field_id)
        if record is None:
            raise EncodingError(
                "unregistered_field", f"fields[{field_id}]", f"未注册字段: {field_id!r}", related_ids=[field_id]
            )
        if record.value_type != "category":
            raise EncodingError(
                "value_type_mismatch",
                f"fields[{field_id}]",
                f"字段 {field_id!r} 的值类型是 {record.value_type!r}，不是 category",
                related_ids=[field_id],
            )
        if category_id not in record.category_ids:
            raise EncodingError(
                "unknown_type",
                f"fields[{field_id}].value",
                f"类别值 {category_id!r} 不在字段 {field_id!r} 的声明类别中",
                related_ids=[field_id, category_id],
            )
        return self.require_index("categories", category_id, path=f"categories[{category_id}]")

    # -- 数值编码（A-04 张量化复用同一入口，不在别处重算） ---------------- #
    def encode_number(self, field_id: str, raw_text: Any) -> float:
        record = self.field_records.get(field_id)
        if record is None:
            raise EncodingError(
                "unregistered_field", f"fields[{field_id}]", f"未注册字段: {field_id!r}", related_ids=[field_id]
            )
        if record.value_type != "number":
            raise EncodingError(
                "value_type_mismatch",
                f"fields[{field_id}]",
                f"字段 {field_id!r} 的值类型是 {record.value_type!r}，不是 number",
                related_ids=[field_id],
            )
        return self.encoding_config.encode_number(field_id, raw_text)

    # -- 身份 / 版本 ------------------------------------------------------ #
    def require_identity(self, identity: Identity, *, path: str = "identity") -> None:
        for key in _IDENTITY_KEYS:
            expected = getattr(self.environment_identity, key)
            actual = getattr(identity, key)
            if actual != expected:
                raise EncodingError(
                    "version_mismatch",
                    f"{path}.{key}",
                    f"身份不一致（exact identity，不降级）: {actual!r} != {expected!r}",
                    related_ids=[expected, actual],
                )

    def assert_snapshot_identity(self, snapshot: Any, *, path: str = "snapshot") -> None:
        """快照（视图或 wire 文档）必须与本 Manifest 的环境身份完全一致。"""
        if isinstance(snapshot, Mapping):
            try:
                identity = Identity.from_wire(snapshot)
            except (KeyError, TypeError) as exc:
                raise EncodingError(
                    "invalid_manifest", path, f"快照缺少身份键: {exc}"
                ) from exc
        elif isinstance(snapshot, Identity):
            identity = snapshot
        elif hasattr(snapshot, "identity"):  # PublicSnapshot 视图
            identity = snapshot.identity
        else:
            raise EncodingError("invalid_manifest", path, f"无法从 {type(snapshot).__name__} 取环境身份")
        self.require_identity(identity, path=path)

    def matches_catalog(self, catalog: Any) -> bool:
        document = catalog if isinstance(catalog, Mapping) else catalog.to_wire()
        return layers.catalog_digest(document) == self.source_catalog_digest

    def assert_catalog(self, catalog: Any, *, path: str = "catalog") -> None:
        """新目录配旧字典：内容身份不同即拒绝（不重排索引、不自动扩表）。"""
        if not self.matches_catalog(catalog):
            document = catalog if isinstance(catalog, Mapping) else catalog.to_wire()
            raise EncodingError(
                "version_mismatch",
                path,
                "目录内容身份与字典不一致（新输入不能配旧字典）",
                related_ids=[self.source_catalog_digest, layers.catalog_digest(document)],
            )

    def require_current_layout(self, *, path: str = "manifest.tensor_layout_version") -> None:
        if self.tensor_layout_version != TENSOR_LAYOUT_VERSION:
            raise EncodingError(
                "version_mismatch",
                path,
                f"字典布局 {self.tensor_layout_version!r} 与当前布局 {TENSOR_LAYOUT_VERSION!r} 不同",
            )


# --------------------------------------------------------------------------- #
# 构建 / 保存 / 加载
# --------------------------------------------------------------------------- #
def _catalog_view(public_catalog: Any, coverage: Optional[dict]) -> tuple[PublicCatalog, dict]:
    if isinstance(public_catalog, PublicCatalog):
        return public_catalog, public_catalog.to_wire()
    if isinstance(public_catalog, Mapping):
        document = dict(public_catalog)
        # 结构/目录内部检查复用共享参考实现（不合规输入在此抛出 ContractViolation）。
        view = layers.parse_catalog_document(document, coverage=coverage)
        return view, document
    raise TypeError(f"public_catalog 必须是 PublicCatalog 或目录文档，得到 {type(public_catalog).__name__}")


def build_manifest(
    public_catalog: Any,
    encoding_config: EncodingConfig,
    *,
    backend_profile_id: str,
    gym_id: str,
    scenario_profile: Optional[str] = None,
    coverage: Optional[dict] = None,
) -> GymManifest:
    """从目录与显式编码配置构建字典；注册顺序不影响结果。"""
    if not isinstance(encoding_config, EncodingConfig):
        raise TypeError("encoding_config 必须是 EncodingConfig（数值/容差/容量必须显式）")
    view, catalog_document = _catalog_view(public_catalog, coverage)
    if not isinstance(backend_profile_id, str) or not backend_profile_id:
        raise EncodingError(
            "invalid_manifest",
            "environment_identity.backend_profile_id",
            "必须显式给出 backend_profile_id（目录不含该键，不猜默认值）",
        )
    if not isinstance(gym_id, str) or not gym_id:
        raise EncodingError("invalid_manifest", "gym_id", "gym_id 必须是非空字符串")
    if scenario_profile is not None and (not isinstance(scenario_profile, str) or not scenario_profile):
        raise EncodingError("invalid_manifest", "scenario_profile", "scenario_profile 必须是非空字符串或 null")

    declarations = view.declarations
    dictionaries: dict[str, Dictionary] = {}
    for table in ("entity_kinds", "content_types", "fields", "relation_types",
                  "sequence_types", "group_types", "action_types"):
        dictionaries[table] = _build_dictionary(table, [item.id for item in declarations[table]])
    category_names: list[str] = []
    for declaration in declarations["fields"]:
        if declaration.value_type == "category":
            category_names.extend(declaration.category_ids)
    dictionaries["categories"] = _build_dictionary("categories", category_names, dedupe=True)

    fields_dictionary = dictionaries["fields"]
    field_records: dict[str, FieldRecord] = {}
    for declaration in declarations["fields"]:
        index = fields_dictionary.index_of(declaration.id)
        if index is None:  # pragma: no cover - 字典由同一批声明构建，必然存在
            raise EncodingError(
                "duplicate_identity", "catalog.declarations.fields", f"字段 {declaration.id!r} 索引缺失"
            )
        field_records[declaration.id] = FieldRecord.from_declaration(declaration, index)

    for field_id in encoding_config.transforms:
        record = field_records.get(field_id)
        if record is None:
            raise EncodingError(
                "unregistered_field",
                f"encoding_config.transforms[{field_id}]",
                f"变换引用了未注册字段: {field_id!r}",
                related_ids=[field_id],
            )
        if record.value_type != "number":
            raise EncodingError(
                "value_type_mismatch",
                f"encoding_config.transforms[{field_id}]",
                f"变换只适用于 number 字段，{field_id!r} 是 {record.value_type!r}",
                related_ids=[field_id],
            )

    source_catalog_digest = layers.catalog_digest(catalog_document)
    encoding_config_digest = encoding_config.digest()
    manifest = GymManifest(
        manifest_format_version=MANIFEST_FORMAT_VERSION,
        tensor_layout_version=TENSOR_LAYOUT_VERSION,
        gym_id=gym_id,
        dictionary_version=DICTIONARY_VERSION_PREFIX + source_catalog_digest,
        source_catalog_digest=source_catalog_digest,
        encoding_config_digest=encoding_config_digest,
        manifest_digest="",  # 先占位，随后按内容计算
        environment_identity=Identity(
            protocol_version=view.protocol_version,
            ruleset_id=view.ruleset_id,
            catalog_id=view.catalog_id,
            backend_profile_id=backend_profile_id,
        ),
        scenario_profile=scenario_profile,
        encoding_config=encoding_config,
        dictionaries=dictionaries,
        field_records=field_records,
    )
    object.__setattr__(manifest, "manifest_digest", manifest.compute_digest())
    return manifest


def save_manifest(manifest: GymManifest, path: "str | Path") -> Path:
    """以**规范字节**写入（紧凑 JSON、键按 UTF-8 字节序、无浮点字面量）。"""
    if not isinstance(manifest, GymManifest):
        raise TypeError("manifest 必须是 GymManifest")
    recomputed = manifest.compute_digest()
    if recomputed != manifest.manifest_digest:
        raise EncodingError(
            "invalid_manifest",
            "manifest.manifest_digest",
            f"待保存 Manifest 的摘要与内容不一致: {manifest.manifest_digest!r} != {recomputed!r}",
        )
    target = Path(path)
    target.write_bytes(layers.canonical_bytes(manifest.to_wire()))
    return target


def load_manifest(
    path: "str | Path",
    *,
    expected_identity: Optional[Identity] = None,
) -> GymManifest:
    """L0 严格解析 + 结构校验；**不读取目录**、不按新注册顺序重建索引。"""
    target = Path(path)
    document, findings = layers.parse_wire_text(target.read_bytes(), what=target.name)
    if findings:
        first = findings[0]
        raise EncodingError(
            "invalid_manifest",
            first.path,
            first.message,
            related_ids=[finding.rule_id for finding in findings],
        )
    manifest = GymManifest.from_wire(document, origin=target.name)
    if expected_identity is not None:
        manifest.require_identity(expected_identity)
    return manifest


__all__ = [
    "DICTIONARY_TABLES",
    "DICTIONARY_VERSION_PREFIX",
    "EXCLUDED_DECLARATION_TABLES",
    "Dictionary",
    "FieldRecord",
    "GymManifest",
    "MANIFEST_FORMAT_VERSION",
    "TENSOR_LAYOUT_VERSION",
    "build_manifest",
    "load_manifest",
    "save_manifest",
]
