"""有界采样（D-02）。

`collector.py`：显式限额、确定性种子派生、独立 worker、停止原因与进度索引。
"""

from .collector import (
    COLLECTOR_VERSION,
    STOP_REASONS,
    CollectionResult,
    Collector,
    CollectorLimits,
    derive_seed,
    read_progress,
)

__all__ = [
    "COLLECTOR_VERSION",
    "STOP_REASONS",
    "CollectionResult",
    "Collector",
    "CollectorLimits",
    "derive_seed",
    "read_progress",
]
