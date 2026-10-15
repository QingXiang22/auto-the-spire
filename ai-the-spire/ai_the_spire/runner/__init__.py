"""J-01 / M1 的运行器：把 net 的公开 wire 与 AI 的编码层接起来。

职责边界（`ai-net-operations.md`、`encoder-design.md` §1/§2、开工清单 §8）：

* **只消费当前公开快照**：不读隐藏状态、不维护历史、不保存 ``known_top``、不理解效果 DSL、不结算规则；
* 数值/容量/容差来自显式 :class:`EncodingConfig`；模型身份在没有神经网络时显式为**随机基线**，
  不得称作训练模型；
* 轨迹落盘后必须能**离线重读并重编码**（快照 + 目录 + 身份都在轨迹里）。
"""

from .baseline import Candidate, ordered_candidates, sample_action, uniform_probabilities
from .episode import (
    EpisodeIdentities,
    EpisodeInterrupted,
    EpisodeResult,
    EpisodeRunner,
    Transition,
    gym_id_from,
)
from .trace import TraceError, load_trace, write_trace

__all__ = [
    "Candidate",
    "EpisodeIdentities",
    "EpisodeInterrupted",
    "EpisodeResult",
    "EpisodeRunner",
    "TraceError",
    "Transition",
    "gym_id_from",
    "load_trace",
    "ordered_candidates",
    "sample_action",
    "uniform_probabilities",
    "write_trace",
]
