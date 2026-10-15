"""ai-the-spire 结构化编码层（A-02：Manifest；A-03：校验层）。

边界（encoder-design §1/§2、开工清单 §7.2）：

* 本包**不**属于 ``ai_the_spire.contracts``：协议结构仍只由 contracts + 共享资产定义，
  这里只做 AI 侧字典、数值变换与索引；
* 数值变换/误差容差/容量一律来自显式 :class:`EncodingConfig`，不静默裁剪、
  不按注册顺序重建索引、不用 float 冒充源 decimal 精度；
* 不维护历史、不保存 ``known_top``、不读取隐藏状态、不执行规则、不导入活动 client/core。

A-03 在 :class:`GymManifest` 之上做字段/内容状态、适用域、引用、牌堆四条不变量与
候选/资源上限校验；A-04 做 tensorize/collate。本批只提供它们必须共用的字典、
变换与身份判定，避免出现第二个索引来源。
"""

from .config import (
    CAPACITY_KEYS,
    CONFIG_VERSION,
    CapacityLimits,
    EncodingConfig,
    NumericTransform,
)
from .errors import (
    A02_RAISED_CODES,
    A03_ADDED_CODES,
    A03_RAISED_CODES,
    ADDED_CODES,
    ALL_CODES,
    MINIMUM_CODES,
    EncodingError,
)
from .manifest import (
    DICTIONARY_TABLES,
    DICTIONARY_VERSION_PREFIX,
    EXCLUDED_DECLARATION_TABLES,
    MANIFEST_FORMAT_VERSION,
    TENSOR_LAYOUT_VERSION,
    Dictionary,
    FieldRecord,
    GymManifest,
    build_manifest,
    load_manifest,
    save_manifest,
)
from .validate import (
    SHARED_RULE_CODES,
    SHARED_RULE_FALLBACK,
    SNAPSHOT_KEYS,
    ValidationReport,
    require_valid_snapshot,
    validate_snapshot,
)
from .tensor import (
    CAPACITY_BY_BLOCK,
    COLUMN_SCHEMAS,
    CONTENT_STATE_IDS,
    INDEX_COLUMNS,
    KNOWLEDGE_STATE_IDS,
    MASK_COLUMNS,
    PAD_INDEX,
    RESERVED_PILE_DECLARATIONS,
    RESERVED_PILE_MEMBER,
    RESERVED_PILE_MEMBERS_COMPLETE,
    RESERVED_PILE_TOTAL_COUNT,
    SKIPPED_WITHOUT_CATALOG,
    TENSOR_BLOCK_NAMES,
    VALUE_TYPE_IDS,
    BatchBlock,
    Block,
    ReferenceMap,
    TensorizedBatch,
    TensorizedObservation,
    collate,
    tensorize,
)

__all__ = [
    "ADDED_CODES",
    "ALL_CODES",
    "A02_RAISED_CODES",
    "A03_ADDED_CODES",
    "A03_RAISED_CODES",
    "CAPACITY_BY_BLOCK",
    "CAPACITY_KEYS",
    "COLUMN_SCHEMAS",
    "CONFIG_VERSION",
    "CONTENT_STATE_IDS",
    "CapacityLimits",
    "DICTIONARY_TABLES",
    "DICTIONARY_VERSION_PREFIX",
    "Dictionary",
    "EXCLUDED_DECLARATION_TABLES",
    "EncodingConfig",
    "EncodingError",
    "FieldRecord",
    "GymManifest",
    "INDEX_COLUMNS",
    "KNOWLEDGE_STATE_IDS",
    "MANIFEST_FORMAT_VERSION",
    "MASK_COLUMNS",
    "MINIMUM_CODES",
    "NumericTransform",
    "PAD_INDEX",
    "RESERVED_PILE_DECLARATIONS",
    "RESERVED_PILE_MEMBER",
    "RESERVED_PILE_MEMBERS_COMPLETE",
    "RESERVED_PILE_TOTAL_COUNT",
    "SHARED_RULE_CODES",
    "SHARED_RULE_FALLBACK",
    "SKIPPED_WITHOUT_CATALOG",
    "SNAPSHOT_KEYS",
    "TENSOR_BLOCK_NAMES",
    "TENSOR_LAYOUT_VERSION",
    "VALUE_TYPE_IDS",
    "BatchBlock",
    "Block",
    "ReferenceMap",
    "TensorizedBatch",
    "TensorizedObservation",
    "ValidationReport",
    "build_manifest",
    "collate",
    "load_manifest",
    "require_valid_snapshot",
    "save_manifest",
    "tensorize",
    "validate_snapshot",
]
