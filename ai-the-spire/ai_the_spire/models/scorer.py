"""候选 scorer 与内部状态价值 V（`policy-contract.md` §1、`design-v0.md` §"模型方案" 第 5–6 步）。

* 候选向量 = 动作类型 embedding + 该候选**具名字段**的聚合（数值/布尔/类别/**被引用实体行**）；
* logit 由共享小层与**全局向量**的交互给出：``u_i = tanh(h_i + h_global @ W_g)``、``logit_i = u_i · v``；
* **没有位置嵌入**：候选顺序不影响同一语义键的得分（候选下标不是特征）；
* 掩码候选的 logit 恒为 0；
* 价值头从 global 输出**一个标量 V**，不复制给候选、也不冒称动作收益。

前向只写一遍（走 :mod:`ai_the_spire.models.autodiff` 的算子）：推理时参数是数组，
训练时传入参数 Var 就能对**全模型**求梯度。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping, Optional

import numpy as np

from . import autodiff as ad
from .arrays import ModelInput
from .config import ModelConfig
from .encoder import EncodedObservation, SnapshotEncoder
from .params import ParameterSet


@dataclass(frozen=True)
class ScoredCandidates:
    """候选得分：logits（掩码行为 0）、候选向量、内部状态价值 V。

    训练时 ``logits``/``candidate_vectors``/``value`` 是 :class:`Var`，推理时是 numpy 数组。
    """

    logits: Any
    candidate_vectors: Any
    candidate_mask: np.ndarray
    value: Any


class CandidateScorer:
    """共享 scorer（对全部候选使用同一套参数）+ 价值头。"""

    def __init__(self, config: ModelConfig, table_sizes: Mapping[str, int]) -> None:
        if not isinstance(config, ModelConfig):
            raise TypeError(f"config 必须是 ModelConfig，得到 {type(config).__name__}")
        self.config = config
        self.table_sizes = {name: int(size) for name, size in table_sizes.items()}
        self.encoder = SnapshotEncoder(config, self.table_sizes)
        self.field_dim = self.encoder.field_dim
        self.candidate_dim = config.d_action_type + config.d_field_hidden
        self.params = self.encoder.params
        self._build()
        self.params.randomize(config.seed)

    def _build(self) -> None:
        c = self.config
        p = self.params
        p.add("candidate/W_in", (self.candidate_dim, c.d_candidate), fan_in=self.candidate_dim)
        p.add("candidate/b_in", (c.d_candidate,), fan_in=c.d_candidate)
        p.add("candidate/W_global", (c.d_global, c.d_candidate), fan_in=c.d_global)
        p.add("candidate/v", (c.d_candidate,), fan_in=c.d_candidate)
        p.add("value/W_hidden", (c.d_global, c.d_value_hidden), fan_in=c.d_global)
        p.add("value/b_hidden", (c.d_value_hidden,), fan_in=c.d_value_hidden)
        p.add("value/w_out", (c.d_value_hidden,), fan_in=c.d_value_hidden)

    # -- 参数视图 -------------------------------------------------------- #
    def parameter_vars(self) -> dict[str, ad.Var]:
        """训练用的**全模型**参数视图（与 `ParameterSet` 共享内存）。"""
        return self.encoder.parameter_vars()

    # -- 前向 ------------------------------------------------------------ #
    def score(self, x: ModelInput, encoded: EncodedObservation, *,
              vars: Optional[Mapping[str, ad.Var]] = None) -> ScoredCandidates:
        if x.candidate_count == 0:
            raise ValueError("没有候选就没有得分：终止/故障快照不得伪造概率或价值（由调用方分开处理）")
        p = lambda name: (vars[name] if vars is not None else self.params[name])  # noqa: E731
        dtype = x.dtype
        candidates = x.candidate_count
        mask = x.candidate_mask

        field_vectors = self.encoder.field_vectors(x, encoded.entities, candidate_fields=True, vars=vars)
        field_mask = x.candidate_field_mask.astype(dtype).reshape(-1, 1)
        masked = ad.mul(field_vectors, field_mask)
        total = ad.scatter_add(masked, x.candidate_field_owner, candidates)
        counts = np.zeros(candidates, dtype=np.float64)
        if x.candidate_field_owner.shape[0] > 0:
            np.add.at(counts, x.candidate_field_owner, field_mask.reshape(-1))
        aggregated = ad.divide(total, np.maximum(counts, 1.0).reshape(-1, 1))

        raw = ad.concatenate(
            [ad.gather(p("emb/action_type"), x.candidate_action), aggregated], axis=1
        )
        candidate_vectors = ad.mul(
            ad.tanh(ad.add(ad.matmul(raw, p("candidate/W_in")), p("candidate/b_in"))),
            mask.astype(dtype).reshape(-1, 1),
        )

        global_term = ad.reshape(
            ad.matmul(ad.reshape(encoded.global_vector, (1, -1)), p("candidate/W_global")), (-1,)
        )
        interaction = ad.tanh(ad.add(candidate_vectors, global_term))
        logits = ad.scale(
            ad.reshape(
                ad.matmul(interaction, ad.reshape(p("candidate/v"), (self.config.d_candidate, 1))),
                (-1,),
            ),
            1.0 / float(np.sqrt(float(self.config.d_candidate))),
        )
        logits = ad.where(mask, logits, 0.0)

        hidden = ad.tanh(ad.add(
            ad.reshape(ad.matmul(ad.reshape(encoded.global_vector, (1, -1)),
                                 p("value/W_hidden")), (-1,)),
            p("value/b_hidden"),
        ))
        value = ad.dot(hidden, p("value/w_out"))

        return ScoredCandidates(
            logits=logits,
            candidate_vectors=candidate_vectors,
            candidate_mask=mask,
            value=value,
        )

    def probabilities(self, scored: ScoredCandidates) -> Any:
        """**只在有效候选上**做 softmax（掩码行的概率恒为 0，且不参与归一化）。

        训练路径返回 :class:`Var`（可继续求梯度），推理路径返回 numpy 数组。
        """
        result = ad.masked_softmax(scored.logits, scored.candidate_mask)
        if isinstance(result, ad.Var):
            return result
        return np.asarray(result, dtype=np.float64)

    def value_of(self, scored: ScoredCandidates) -> float:
        return float(np.asarray(ad.value_of(scored.value)))

    # -- 报告 ------------------------------------------------------------ #
    def parameter_breakdown(self) -> dict[str, int]:
        return self.params.breakdown()

    def describe(self) -> dict[str, Any]:
        return {
            "config": self.config.to_wire(),
            "config_digest": self.config.digest(),
            "table_sizes": dict(self.table_sizes),
            "candidate_dim": self.candidate_dim,
            "parameters": self.params.breakdown(),
            "parameter_total": self.params.total_size(),
        }
