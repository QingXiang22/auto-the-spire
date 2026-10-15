"""AI 侧数据设施（D-01）。

`record.py` 定义版本化 EpisodeRecord 与转移；`store.py` 负责落盘、索引与独立复核。
本包不读隐藏状态、不做规则结算、不训练模型。
"""

from .audit import (
    FORBIDDEN_REWARD_KEYS,
    SPLITS,
    SPLIT_PLAN_VERSION,
    DataAudit,
    SplitPlan,
    audit,
    cluster_key,
    dangling_references,
    find_reward_labels,
    identity_of,
    load_audit,
    usable_transitions,
    write_audit,
)
from .record import (
    ENDING_CLASSES,
    RECORD_FORMAT_VERSION,
    RUNNER_PROVENANCE,
    TRANSITION_OUTCOMES,
    EpisodeRecord,
    RecordError,
    TransitionRecord,
    build_record,
    canonical_bytes,
    classify_ending,
    digest_of,
    validate_document,
)
from .store import EpisodeStore

__all__ = [
    "ENDING_CLASSES",
    "EpisodeRecord",
    "EpisodeStore",
    "FORBIDDEN_REWARD_KEYS",
    "RECORD_FORMAT_VERSION",
    "RUNNER_PROVENANCE",
    "RecordError",
    "SPLITS",
    "SPLIT_PLAN_VERSION",
    "TRANSITION_OUTCOMES",
    "DataAudit",
    "SplitPlan",
    "TransitionRecord",
    "audit",
    "build_record",
    "canonical_bytes",
    "classify_ending",
    "cluster_key",
    "dangling_references",
    "digest_of",
    "find_reward_labels",
    "identity_of",
    "load_audit",
    "usable_transitions",
    "validate_document",
    "write_audit",
]
