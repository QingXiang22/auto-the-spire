"""net v0.1 公开协议的纯协议类型（与 ``docs/contracts/net/0.1/wire.schema.json`` 同源）。

设计边界（encoder-design §1/§2，开工清单 §7.2）：

* 这里**不**重新定义协议或第二个 codec。结构来源是唯一共享资产
  ``docs/contracts/net/0.1/wire.schema.json``；行为来源是唯一共享参考实现
  ``tools/contract-check/``（``wire_json`` / ``wire_canonical`` / ``wire_layers`` /
  ``jsonschema_min``）。见 :mod:`ai_the_spire.contracts.shared`。
* 本包不维护历史、不保存 ``known_top``、不读取隐藏状态、不执行规则、
  不导入活动 client 或 core。
* 数值变换、误差容差与容量属于显式 ``EncodingConfig``（A-02/A-04），不在本包。
"""

from .errors import ContractViolation, Finding
from .layers import (
    ALL_LAYERS,
    action_semantic_key,
    canonical_bytes,
    canonical_text,
    catalog_digest,
    digest_bytes,
    document_findings,
    is_canonical_decimal,
    is_integer_decimal,
    load_catalog,
    load_snapshot,
    normalize_decimal,
    parse_catalog_document,
    parse_snapshot_document,
    parse_wire_text,
    snapshot_storage_digest,
)
from .shared import SpecAssetError, repo_root, spec_assets
from .types import (
    ActionTypeDecl,
    AppliesTo,
    CardPosition,
    ContentTypeDecl,
    Entity,
    EntityKindDecl,
    Field,
    FieldDecl,
    Group,
    GroupMember,
    GroupTypeDecl,
    Identity,
    LegalAction,
    Pile,
    PileTypeDecl,
    PositionSchema,
    Relation,
    RelationTypeDecl,
    Sequence,
    SequenceTypeDecl,
    TerminalReasonDecl,
    TerminalResult,
)
from .views import PublicCatalog, PublicSnapshot

__all__ = [
    "ALL_LAYERS",
    "ActionTypeDecl",
    "AppliesTo",
    "CardPosition",
    "ContentTypeDecl",
    "ContractViolation",
    "Entity",
    "EntityKindDecl",
    "Field",
    "FieldDecl",
    "Finding",
    "Group",
    "GroupMember",
    "GroupTypeDecl",
    "Identity",
    "LegalAction",
    "Pile",
    "PileTypeDecl",
    "PositionSchema",
    "PublicCatalog",
    "PublicSnapshot",
    "Relation",
    "RelationTypeDecl",
    "Sequence",
    "SequenceTypeDecl",
    "SpecAssetError",
    "TerminalReasonDecl",
    "TerminalResult",
    "action_semantic_key",
    "canonical_bytes",
    "canonical_text",
    "catalog_digest",
    "digest_bytes",
    "document_findings",
    "is_canonical_decimal",
    "is_integer_decimal",
    "load_catalog",
    "load_snapshot",
    "normalize_decimal",
    "parse_catalog_document",
    "parse_snapshot_document",
    "parse_wire_text",
    "repo_root",
    "snapshot_storage_digest",
    "spec_assets",
]
