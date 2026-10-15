"""net v0.1 公开协议的纯协议类型（dataclass 视图 + wire 往返）。

* 键名、枚举与分支全部来自共享 schema；本模块不含任何 AI 侧索引/变换/补齐概念。
* ``from_wire`` 假定文档已通过 L0/L1（严格解析 + schema 结构）；结构错误由
  :mod:`ai_the_spire.contracts.layers` 在构建视图之前以共享 ``I-xx`` 结论报告。
* ``to_wire`` 与合法输入逐键往返（含 ``known``/``unknown``/``unavailable`` 的
  ``value`` 出现性、``content_id`` 出现性、可空声明键）——这是“不静默丢信息”的检查点。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Optional

# --------------------------------------------------------------------------- #
# 固定枚举（与 wire.schema.json 的 $defs 枚举逐值对应）
# --------------------------------------------------------------------------- #
VALUE_TYPES = ("number", "boolean", "category", "entity_ref")
NUMBER_FORMATS = ("integer", "decimal")
VISIBILITIES = ("public", "conditional")
CONTENT_STATES = ("known", "unknown", "unavailable", "not_applicable")
FIELD_STATES = ("known", "unknown", "unavailable")
SNAPSHOT_STATUSES = ("decision", "terminal")
OUTCOMES = ("win", "loss", "other")

DECLARATION_TABLES = (
    "entity_kinds",
    "content_types",
    "fields",
    "relation_types",
    "pile_types",
    "sequence_types",
    "group_types",
    "action_types",
    "terminal_reasons",
)


def _applies(entity_kinds=(), action_types=(), terminal_reasons=()) -> dict:
    return {
        "entity_kinds": list(entity_kinds),
        "action_types": list(action_types),
        "terminal_reasons": list(terminal_reasons),
    }


# --------------------------------------------------------------------------- #
# 目录声明
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class AppliesTo:
    entity_kinds: tuple[str, ...] = ()
    action_types: tuple[str, ...] = ()
    terminal_reasons: tuple[str, ...] = ()

    @classmethod
    def from_wire(cls, doc: dict) -> "AppliesTo":
        return cls(
            entity_kinds=tuple(doc["entity_kinds"]),
            action_types=tuple(doc["action_types"]),
            terminal_reasons=tuple(doc["terminal_reasons"]),
        )

    def to_wire(self) -> dict:
        return _applies(self.entity_kinds, self.action_types, self.terminal_reasons)


@dataclass(frozen=True)
class PositionSchema:
    unit: str
    origin: str
    min: int
    max: Optional[int]

    @classmethod
    def from_wire(cls, doc: Optional[dict]) -> Optional["PositionSchema"]:
        if doc is None:
            return None
        return cls(unit=doc["unit"], origin=doc["origin"], min=doc["min"], max=doc["max"])

    def to_wire(self) -> Optional[dict]:
        return {"unit": self.unit, "origin": self.origin, "min": self.min, "max": self.max}


@dataclass(frozen=True)
class EntityKindDecl:
    id: str
    semantic_version: str
    description: str

    @classmethod
    def from_wire(cls, doc: dict) -> "EntityKindDecl":
        return cls(doc["id"], doc["semantic_version"], doc["description"])

    def to_wire(self) -> dict:
        return {
            "id": self.id,
            "semantic_version": self.semantic_version,
            "description": self.description,
        }


@dataclass(frozen=True)
class ContentTypeDecl:
    id: str
    semantic_version: str
    description: str
    kind_ids: tuple[str, ...]

    @classmethod
    def from_wire(cls, doc: dict) -> "ContentTypeDecl":
        return cls(doc["id"], doc["semantic_version"], doc["description"], tuple(doc["kind_ids"]))

    def to_wire(self) -> dict:
        return {
            "id": self.id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "kind_ids": list(self.kind_ids),
        }


@dataclass(frozen=True)
class FieldDecl:
    id: str
    semantic_version: str
    description: str
    value_type: str
    applies_to: AppliesTo
    required: bool
    visibility: str
    number_format: Optional[str] = None
    category_ids: tuple[str, ...] = ()
    target_kind_ids: tuple[str, ...] = ()
    condition_description: Optional[str] = None
    visibility_check_ids: tuple[str, ...] = ()

    @classmethod
    def from_wire(cls, doc: dict) -> "FieldDecl":
        return cls(
            id=doc["id"],
            semantic_version=doc["semantic_version"],
            description=doc["description"],
            value_type=doc["value_type"],
            applies_to=AppliesTo.from_wire(doc["applies_to"]),
            required=doc["required"],
            visibility=doc["visibility"],
            number_format=doc.get("number_format"),
            category_ids=tuple(doc.get("category_ids") or ()),
            target_kind_ids=tuple(doc.get("target_kind_ids") or ()),
            condition_description=doc.get("condition_description"),
            visibility_check_ids=tuple(doc.get("visibility_check_ids") or ()),
        )

    def to_wire(self) -> dict:
        out: dict[str, Any] = {
            "id": self.id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "value_type": self.value_type,
            "applies_to": self.applies_to.to_wire(),
            "required": self.required,
            "visibility": self.visibility,
        }
        if self.value_type == "number":
            out["number_format"] = self.number_format
        if self.value_type == "category":
            out["category_ids"] = list(self.category_ids)
        if self.value_type == "entity_ref":
            out["target_kind_ids"] = list(self.target_kind_ids)
        if self.visibility == "conditional":
            out["condition_description"] = self.condition_description
            out["visibility_check_ids"] = list(self.visibility_check_ids)
        return out

    @property
    def applicable_entity_kinds(self) -> frozenset[str]:
        return frozenset(self.applies_to.entity_kinds)

    @property
    def applicable_action_types(self) -> frozenset[str]:
        return frozenset(self.applies_to.action_types)

    @property
    def applicable_terminal_reasons(self) -> frozenset[str]:
        return frozenset(self.applies_to.terminal_reasons)


@dataclass(frozen=True)
class RelationTypeDecl:
    id: str
    semantic_version: str
    description: str
    source_kind_ids: tuple[str, ...]
    target_kind_ids: tuple[str, ...]
    position_schema: Optional[PositionSchema]

    @classmethod
    def from_wire(cls, doc: dict) -> "RelationTypeDecl":
        return cls(
            id=doc["id"],
            semantic_version=doc["semantic_version"],
            description=doc["description"],
            source_kind_ids=tuple(doc["source_kind_ids"]),
            target_kind_ids=tuple(doc["target_kind_ids"]),
            position_schema=PositionSchema.from_wire(doc["position_schema"]),
        )

    def to_wire(self) -> dict:
        return {
            "id": self.id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "source_kind_ids": list(self.source_kind_ids),
            "target_kind_ids": list(self.target_kind_ids),
            "position_schema": self.position_schema.to_wire() if self.position_schema else None,
        }


@dataclass(frozen=True)
class PileTypeDecl:
    id: str
    semantic_version: str
    description: str
    kind_id: str
    member_kind_ids: tuple[str, ...]
    position_schema: Optional[PositionSchema]
    exclusive_group_id: Optional[str]
    membership_scope_field_id: Optional[str]

    @classmethod
    def from_wire(cls, doc: dict) -> "PileTypeDecl":
        return cls(
            id=doc["id"],
            semantic_version=doc["semantic_version"],
            description=doc["description"],
            kind_id=doc["kind_id"],
            member_kind_ids=tuple(doc["member_kind_ids"]),
            position_schema=PositionSchema.from_wire(doc["position_schema"]),
            exclusive_group_id=doc["exclusive_group_id"],
            membership_scope_field_id=doc["membership_scope_field_id"],
        )

    def to_wire(self) -> dict:
        return {
            "id": self.id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "kind_id": self.kind_id,
            "member_kind_ids": list(self.member_kind_ids),
            "position_schema": self.position_schema.to_wire() if self.position_schema else None,
            "exclusive_group_id": self.exclusive_group_id,
            "membership_scope_field_id": self.membership_scope_field_id,
        }


@dataclass(frozen=True)
class SequenceTypeDecl:
    id: str
    semantic_version: str
    description: str
    owner_kind_ids: tuple[str, ...]
    element_kind_ids: tuple[str, ...]
    min_count: int
    max_count: Optional[int]

    @classmethod
    def from_wire(cls, doc: dict) -> "SequenceTypeDecl":
        return cls(
            doc["id"],
            doc["semantic_version"],
            doc["description"],
            tuple(doc["owner_kind_ids"]),
            tuple(doc["element_kind_ids"]),
            doc["min_count"],
            doc["max_count"],
        )

    def to_wire(self) -> dict:
        return {
            "id": self.id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "owner_kind_ids": list(self.owner_kind_ids),
            "element_kind_ids": list(self.element_kind_ids),
            "min_count": self.min_count,
            "max_count": self.max_count,
        }


@dataclass(frozen=True)
class GroupTypeDecl:
    id: str
    semantic_version: str
    description: str
    owner_kind_ids: tuple[str, ...]
    member_kind_ids: tuple[str, ...]
    min_count: int
    max_count: Optional[int]

    @classmethod
    def from_wire(cls, doc: dict) -> "GroupTypeDecl":
        return cls(
            doc["id"],
            doc["semantic_version"],
            doc["description"],
            tuple(doc["owner_kind_ids"]),
            tuple(doc["member_kind_ids"]),
            doc["min_count"],
            doc["max_count"],
        )

    def to_wire(self) -> dict:
        return {
            "id": self.id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "owner_kind_ids": list(self.owner_kind_ids),
            "member_kind_ids": list(self.member_kind_ids),
            "min_count": self.min_count,
            "max_count": self.max_count,
        }


@dataclass(frozen=True)
class ActionTypeDecl:
    id: str
    semantic_version: str
    description: str
    required_fields: tuple[str, ...]
    optional_fields: tuple[str, ...]

    @classmethod
    def from_wire(cls, doc: dict) -> "ActionTypeDecl":
        return cls(
            doc["id"],
            doc["semantic_version"],
            doc["description"],
            tuple(doc["required_fields"]),
            tuple(doc["optional_fields"]),
        )

    def to_wire(self) -> dict:
        return {
            "id": self.id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "required_fields": list(self.required_fields),
            "optional_fields": list(self.optional_fields),
        }


@dataclass(frozen=True)
class TerminalReasonDecl:
    id: str
    semantic_version: str
    description: str
    outcome: str
    required_fields: tuple[str, ...]
    optional_fields: tuple[str, ...]

    @classmethod
    def from_wire(cls, doc: dict) -> "TerminalReasonDecl":
        return cls(
            doc["id"],
            doc["semantic_version"],
            doc["description"],
            doc["outcome"],
            tuple(doc["required_fields"]),
            tuple(doc["optional_fields"]),
        )

    def to_wire(self) -> dict:
        return {
            "id": self.id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "outcome": self.outcome,
            "required_fields": list(self.required_fields),
            "optional_fields": list(self.optional_fields),
        }


_DECL_TYPES = {
    "entity_kinds": EntityKindDecl,
    "content_types": ContentTypeDecl,
    "fields": FieldDecl,
    "relation_types": RelationTypeDecl,
    "pile_types": PileTypeDecl,
    "sequence_types": SequenceTypeDecl,
    "group_types": GroupTypeDecl,
    "action_types": ActionTypeDecl,
    "terminal_reasons": TerminalReasonDecl,
}


# --------------------------------------------------------------------------- #
# 快照
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class Identity:
    protocol_version: str
    ruleset_id: str
    catalog_id: str
    backend_profile_id: str

    @classmethod
    def from_wire(cls, doc: dict) -> "Identity":
        return cls(
            doc["protocol_version"],
            doc["ruleset_id"],
            doc["catalog_id"],
            doc["backend_profile_id"],
        )

    def to_wire(self) -> dict:
        return {
            "protocol_version": self.protocol_version,
            "ruleset_id": self.ruleset_id,
            "catalog_id": self.catalog_id,
            "backend_profile_id": self.backend_profile_id,
        }


@dataclass(frozen=True)
class Field:
    field_id: str
    state: str
    value: Any = None

    @classmethod
    def from_wire(cls, doc: dict) -> "Field":
        return cls(doc["field_id"], doc["state"], doc.get("value"))

    @property
    def is_known(self) -> bool:
        return self.state == "known"

    def to_wire(self) -> dict:
        out = {"field_id": self.field_id, "state": self.state}
        if self.state == "known":
            out["value"] = self.value
        return out

    def number_text(self) -> str:
        """number 字段的规范十进制字符串（类型判定由目录声明与 L3 校验负责）。"""
        if self.state != "known" or not isinstance(self.value, str):
            raise TypeError(f"{self.field_id}: 不是 known number 字段")
        return self.value

    def boolean(self) -> bool:
        if self.state != "known" or not isinstance(self.value, bool):
            raise TypeError(f"{self.field_id}: 不是 known boolean 字段")
        return self.value

    def category_id(self) -> str:
        if self.state != "known" or not isinstance(self.value, str):
            raise TypeError(f"{self.field_id}: 不是 known category 字段")
        return self.value

    def entity_ref(self) -> str:
        if self.state != "known" or not isinstance(self.value, str):
            raise TypeError(f"{self.field_id}: 不是 known entity_ref 字段")
        return self.value


@dataclass(frozen=True)
class Entity:
    instance_id: str
    kind_id: str
    content_state: str
    fields: tuple[Field, ...]
    content_id: Optional[str] = None

    @classmethod
    def from_wire(cls, doc: dict) -> "Entity":
        return cls(
            instance_id=doc["instance_id"],
            kind_id=doc["kind_id"],
            content_state=doc["content_state"],
            fields=tuple(Field.from_wire(item) for item in doc["fields"]),
            content_id=doc.get("content_id"),
        )

    def to_wire(self) -> dict:
        out: dict[str, Any] = {
            "instance_id": self.instance_id,
            "kind_id": self.kind_id,
            "content_state": self.content_state,
            "fields": [item.to_wire() for item in self.fields],
        }
        if self.content_state == "known":
            out["content_id"] = self.content_id
        return out

    def field(self, field_id: str) -> Optional[Field]:
        for item in self.fields:
            if item.field_id == field_id:
                return item
        return None

    @property
    def field_ids(self) -> tuple[str, ...]:
        return tuple(item.field_id for item in self.fields)


@dataclass(frozen=True)
class Relation:
    type_id: str
    source_ref: str
    target_ref: str
    position: Optional[int] = None

    @classmethod
    def from_wire(cls, doc: dict) -> "Relation":
        return cls(doc["type_id"], doc["source_ref"], doc["target_ref"], doc.get("position"))

    def to_wire(self) -> dict:
        out: dict[str, Any] = {
            "type_id": self.type_id,
            "source_ref": self.source_ref,
            "target_ref": self.target_ref,
        }
        if self.position is not None:
            out["position"] = self.position
        return out


@dataclass(frozen=True)
class CardPosition:
    card_ref: str
    position: int

    @classmethod
    def from_wire(cls, doc: dict) -> "CardPosition":
        return cls(doc["card_ref"], doc["position"])

    def to_wire(self) -> dict:
        return {"card_ref": self.card_ref, "position": self.position}


@dataclass(frozen=True)
class Pile:
    pile_ref: str
    total_count: int
    visible_members: tuple[str, ...]
    members_complete: bool
    known_positions: tuple[CardPosition, ...]

    @classmethod
    def from_wire(cls, doc: dict) -> "Pile":
        return cls(
            pile_ref=doc["pile_ref"],
            total_count=doc["total_count"],
            visible_members=tuple(doc["visible_members"]),
            members_complete=doc["members_complete"],
            known_positions=tuple(CardPosition.from_wire(item) for item in doc["known_positions"]),
        )

    def to_wire(self) -> dict:
        return {
            "pile_ref": self.pile_ref,
            "total_count": self.total_count,
            "visible_members": list(self.visible_members),
            "members_complete": self.members_complete,
            "known_positions": [item.to_wire() for item in self.known_positions],
        }

    def position_of(self, card_ref: str) -> Optional[int]:
        for item in self.known_positions:
            if item.card_ref == card_ref:
                return item.position
        return None

    @property
    def unknown_member_count(self) -> int:
        """未知身份的成员数（只反映总数，不建立虚构卡牌）。"""
        return self.total_count - len(self.visible_members)


@dataclass(frozen=True)
class Sequence:
    type_id: str
    owner_ref: str
    elements: tuple[str, ...]

    @classmethod
    def from_wire(cls, doc: dict) -> "Sequence":
        return cls(doc["type_id"], doc["owner_ref"], tuple(doc["elements"]))

    def to_wire(self) -> dict:
        return {
            "type_id": self.type_id,
            "owner_ref": self.owner_ref,
            "elements": list(self.elements),
        }


@dataclass(frozen=True)
class GroupMember:
    entity_ref: str
    multiplicity: int

    @classmethod
    def from_wire(cls, doc: dict) -> "GroupMember":
        return cls(doc["entity_ref"], doc["multiplicity"])

    def to_wire(self) -> dict:
        return {"entity_ref": self.entity_ref, "multiplicity": self.multiplicity}


@dataclass(frozen=True)
class Group:
    type_id: str
    owner_ref: str
    members: tuple[GroupMember, ...]

    @classmethod
    def from_wire(cls, doc: dict) -> "Group":
        return cls(doc["type_id"], doc["owner_ref"], tuple(GroupMember.from_wire(m) for m in doc["members"]))

    def to_wire(self) -> dict:
        return {
            "type_id": self.type_id,
            "owner_ref": self.owner_ref,
            "members": [item.to_wire() for item in self.members],
        }


@dataclass(frozen=True)
class LegalAction:
    candidate_id: str
    action_type: str
    fields: tuple[Field, ...]

    @classmethod
    def from_wire(cls, doc: dict) -> "LegalAction":
        return cls(doc["candidate_id"], doc["action_type"], tuple(Field.from_wire(f) for f in doc["fields"]))

    def to_wire(self) -> dict:
        return {
            "candidate_id": self.candidate_id,
            "action_type": self.action_type,
            "fields": [item.to_wire() for item in self.fields],
        }

    def field(self, field_id: str) -> Optional[Field]:
        for item in self.fields:
            if item.field_id == field_id:
                return item
        return None


@dataclass(frozen=True)
class TerminalResult:
    outcome: str
    reason_id: str
    facts: tuple[Field, ...]

    @classmethod
    def from_wire(cls, doc: dict) -> "TerminalResult":
        return cls(doc["outcome"], doc["reason_id"], tuple(Field.from_wire(f) for f in doc["facts"]))

    def to_wire(self) -> dict:
        return {
            "outcome": self.outcome,
            "reason_id": self.reason_id,
            "facts": [item.to_wire() for item in self.facts],
        }

    def fact(self, field_id: str) -> Optional[Field]:
        for item in self.facts:
            if item.field_id == field_id:
                return item
        return None


__all__ = [
    "ActionTypeDecl",
    "AppliesTo",
    "CONTENT_STATES",
    "CardPosition",
    "ContentTypeDecl",
    "DECLARATION_TABLES",
    "Entity",
    "EntityKindDecl",
    "FIELD_STATES",
    "Field",
    "FieldDecl",
    "Group",
    "GroupMember",
    "GroupTypeDecl",
    "Identity",
    "LegalAction",
    "NUMBER_FORMATS",
    "OUTCOMES",
    "Pile",
    "PileTypeDecl",
    "PositionSchema",
    "Relation",
    "RelationTypeDecl",
    "SNAPSHOT_STATUSES",
    "Sequence",
    "SequenceTypeDecl",
    "TerminalReasonDecl",
    "TerminalResult",
    "VALUE_TYPES",
    "VISIBILITIES",
    "_DECL_TYPES",
]
