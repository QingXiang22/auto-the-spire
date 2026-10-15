"""目录与快照的只读视图（键集合来自共享 schema，索引只用于查找）。

视图只回答“这份当前快照/目录里有什么”，不做：
* 历史维护、``known_top`` 摘要、事件重放；
* 数值变换、误差容差、容量补齐（属显式 ``EncodingConfig``，A-02/A-04）；
* 规则执行、隐藏状态读取。

``to_wire`` 与合法输入逐键往返；``from_wire`` 假定文档已通过 L0/L1。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Optional

from .types import (
    DECLARATION_TABLES,
    _DECL_TYPES,
    ActionTypeDecl,
    ContentTypeDecl,
    Entity,
    EntityKindDecl,
    FieldDecl,
    Group,
    GroupTypeDecl,
    Identity,
    LegalAction,
    Pile,
    PileTypeDecl,
    Relation,
    RelationTypeDecl,
    Sequence,
    SequenceTypeDecl,
    TerminalReasonDecl,
    TerminalResult,
)


@dataclass(frozen=True)
class PublicCatalog:
    protocol_version: str
    ruleset_id: str
    catalog_id: str
    declarations: dict[str, tuple]
    _index: dict[str, dict[str, Any]]

    # ------------------------------------------------------------------ #
    @classmethod
    def from_wire(cls, doc: dict) -> "PublicCatalog":
        raw = doc["declarations"]
        declarations = {
            table: tuple(_DECL_TYPES[table].from_wire(item) for item in raw[table])
            for table in DECLARATION_TABLES
        }
        index = {table: {item.id: item for item in declarations[table]} for table in DECLARATION_TABLES}
        return cls(
            protocol_version=doc["protocol_version"],
            ruleset_id=doc["ruleset_id"],
            catalog_id=doc["catalog_id"],
            declarations=declarations,
            _index=index,
        )

    def to_wire(self) -> dict:
        return {
            "protocol_version": self.protocol_version,
            "ruleset_id": self.ruleset_id,
            "catalog_id": self.catalog_id,
            "declarations": {
                table: [item.to_wire() for item in self.declarations[table]]
                for table in DECLARATION_TABLES
            },
        }

    # -- 查找（纯索引） -------------------------------------------------- #
    def declaration(self, table: str, declaration_id: str):
        return self._index[table].get(declaration_id)

    def entity_kind(self, kind_id: str) -> Optional[EntityKindDecl]:
        return self._index["entity_kinds"].get(kind_id)

    def content_type(self, content_type_id: str) -> Optional[ContentTypeDecl]:
        return self._index["content_types"].get(content_type_id)

    def field(self, field_id: str) -> Optional[FieldDecl]:
        return self._index["fields"].get(field_id)

    def relation_type(self, type_id: str) -> Optional[RelationTypeDecl]:
        return self._index["relation_types"].get(type_id)

    def pile_type(self, pile_type_id: str) -> Optional[PileTypeDecl]:
        return self._index["pile_types"].get(pile_type_id)

    def sequence_type(self, type_id: str) -> Optional[SequenceTypeDecl]:
        return self._index["sequence_types"].get(type_id)

    def group_type(self, type_id: str) -> Optional[GroupTypeDecl]:
        return self._index["group_types"].get(type_id)

    def action_type(self, action_type_id: str) -> Optional[ActionTypeDecl]:
        return self._index["action_types"].get(action_type_id)

    def terminal_reason(self, reason_id: str) -> Optional[TerminalReasonDecl]:
        return self._index["terminal_reasons"].get(reason_id)

    def pile_type_for_kind(self, kind_id: str) -> Optional[PileTypeDecl]:
        """实体 kind_id 唯一匹配的牌堆声明（wire 不重复表达同一事实）。"""
        matches = [item for item in self.declarations["pile_types"] if item.kind_id == kind_id]
        return matches[0] if len(matches) == 1 else None

    def declaration_ids(self) -> frozenset[str]:
        return frozenset(
            declaration_id for table in DECLARATION_TABLES for declaration_id in self._index[table]
        )

    @property
    def declares_piles(self) -> bool:
        return bool(self.declarations["pile_types"])

    def fields_applicable_to_kind(self, kind_id: str) -> tuple[FieldDecl, ...]:
        return tuple(
            item for item in self.declarations["fields"] if kind_id in item.applicable_entity_kinds
        )

    def fields_applicable_to_action(self, action_type_id: str) -> tuple[FieldDecl, ...]:
        return tuple(
            item
            for item in self.declarations["fields"]
            if action_type_id in item.applicable_action_types
        )

    def fields_applicable_to_terminal(self, reason_id: str) -> tuple[FieldDecl, ...]:
        return tuple(
            item
            for item in self.declarations["fields"]
            if reason_id in item.applicable_terminal_reasons
        )


@dataclass(frozen=True)
class PublicSnapshot:
    protocol_version: str
    ruleset_id: str
    catalog_id: str
    backend_profile_id: str
    session_id: str
    observation_id: int
    status: str
    terminal_result: Optional[TerminalResult]
    entities: tuple[Entity, ...]
    relations: tuple[Relation, ...]
    piles: tuple[Pile, ...]
    sequences: tuple[Sequence, ...]
    groups: tuple[Group, ...]
    legal_actions: tuple[LegalAction, ...]
    _entity_index: dict[str, Entity]
    _pile_index: dict[str, Pile]
    _candidate_index: dict[str, LegalAction]

    # ------------------------------------------------------------------ #
    @classmethod
    def from_wire(cls, doc: dict) -> "PublicSnapshot":
        entities = tuple(Entity.from_wire(item) for item in doc["entities"])
        piles = tuple(Pile.from_wire(item) for item in doc["piles"])
        actions = tuple(LegalAction.from_wire(item) for item in doc["legal_actions"])
        return cls(
            protocol_version=doc["protocol_version"],
            ruleset_id=doc["ruleset_id"],
            catalog_id=doc["catalog_id"],
            backend_profile_id=doc["backend_profile_id"],
            session_id=doc["session_id"],
            observation_id=doc["observation_id"],
            status=doc["status"],
            terminal_result=(
                None if doc["terminal_result"] is None else TerminalResult.from_wire(doc["terminal_result"])
            ),
            entities=entities,
            relations=tuple(Relation.from_wire(item) for item in doc["relations"]),
            piles=piles,
            sequences=tuple(Sequence.from_wire(item) for item in doc["sequences"]),
            groups=tuple(Group.from_wire(item) for item in doc["groups"]),
            legal_actions=actions,
            _entity_index={item.instance_id: item for item in entities},
            _pile_index={item.pile_ref: item for item in piles},
            _candidate_index={item.candidate_id: item for item in actions},
        )

    def to_wire(self) -> dict:
        return {
            "protocol_version": self.protocol_version,
            "ruleset_id": self.ruleset_id,
            "catalog_id": self.catalog_id,
            "backend_profile_id": self.backend_profile_id,
            "session_id": self.session_id,
            "observation_id": self.observation_id,
            "status": self.status,
            "terminal_result": None if self.terminal_result is None else self.terminal_result.to_wire(),
            "entities": [item.to_wire() for item in self.entities],
            "relations": [item.to_wire() for item in self.relations],
            "piles": [item.to_wire() for item in self.piles],
            "sequences": [item.to_wire() for item in self.sequences],
            "groups": [item.to_wire() for item in self.groups],
            "legal_actions": [item.to_wire() for item in self.legal_actions],
        }

    # -- 查找 ------------------------------------------------------------ #
    @property
    def identity(self) -> Identity:
        return Identity(
            protocol_version=self.protocol_version,
            ruleset_id=self.ruleset_id,
            catalog_id=self.catalog_id,
            backend_profile_id=self.backend_profile_id,
        )

    def entity(self, instance_id: str) -> Optional[Entity]:
        return self._entity_index.get(instance_id)

    def pile(self, pile_ref: str) -> Optional[Pile]:
        return self._pile_index.get(pile_ref)

    def candidate(self, candidate_id: str) -> Optional[LegalAction]:
        return self._candidate_index.get(candidate_id)

    @property
    def is_terminal(self) -> bool:
        return self.status == "terminal"

    @property
    def candidate_ids(self) -> tuple[str, ...]:
        """快照内候选句柄，按 wire 顺序（规范排序由提供方与协议侧负责）。"""
        return tuple(item.candidate_id for item in self.legal_actions)

    @property
    def instance_kinds(self) -> dict[str, str]:
        return {item.instance_id: item.kind_id for item in self.entities}

    def entity_kind(self, instance_id: str) -> Optional[str]:
        entity = self._entity_index.get(instance_id)
        return None if entity is None else entity.kind_id

    def relations_of(self, instance_id: str) -> tuple[Relation, ...]:
        return tuple(
            item
            for item in self.relations
            if item.source_ref == instance_id or item.target_ref == instance_id
        )

    def sequence(self, type_id: str, owner_ref: str) -> Optional[Sequence]:
        for item in self.sequences:
            if item.type_id == type_id and item.owner_ref == owner_ref:
                return item
        return None

    def group(self, type_id: str, owner_ref: str) -> Optional[Group]:
        for item in self.groups:
            if item.type_id == type_id and item.owner_ref == owner_ref:
                return item
        return None

    def candidate_action_refs(self, field_id: str) -> tuple[str, ...]:
        """候选在该 entity_ref 字段上的引用，按 wire 顺序；缺少该字段的候选跳过。"""
        refs = []
        for candidate in self.legal_actions:
            field = candidate.field(field_id)
            if field is not None and field.is_known:
                refs.append(str(field.value))
        return tuple(refs)


__all__ = ["PublicCatalog", "PublicSnapshot"]
