"""共享小 encoder：字典 embedding → 实体/字段聚合 → L 轮稀疏关系传播 → 全局向量。

对应 `architecture.md` §"模型方案"第 5 步与 `policy-contract.md` §1 的
``encoder.forward(T) -> EncodedObservation``（global + entities + masks）。

约束（逐条可核对）：
* **字段/类别索引是类别标识**，只进 embedding，不作为连续数值（`encoder-design.md` §3 第 47 行）；
* 数值/布尔通道来自张量的 number/boolean 列（已按 manifest 的变换缩放），**不**在此二次变换；
* **不读匿名实例句柄**：所有引用列都是样本内行号，实体向量只由 kind/content/state/字段/关系决定；
* 掩码行（`*_mask=false`）在聚合与传播中一律不参与，且不产生残差；
* 字典索引 0 是**补齐**（`Dictionary.index_of` 返回"位置 + 1"）⇒ 每张字典 embedding 需要 ``size + 1`` 行。

前向只写一遍：所有算术都走 :mod:`ai_the_spire.models.autodiff` 的算子。推理时 ``vars=None``，
算子直接落到 numpy（不建图、无额外开销）；训练时传入参数 Var（见 `models/learning.py`）。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping, Optional

import numpy as np

from . import autodiff as ad
from .arrays import ModelInput
from .config import ModelConfig
from .params import ParameterSet

#: 固定枚举规模：value_type = pad/number/boolean/category/entity_ref（张量化 §5.1 第 166 行）。
VALUE_TYPE_COUNT = 5

#: global 向量里的计数特征个数（E/F/R/A 四个 log1p 计数）。
COUNT_FEATURES = 4

#: 数值常量：类别通道的开关（value_type = category）。
_CATEGORY_TYPE = 3


def _layernorm(x: Any, eps: float = 1e-6) -> Any:
    return ad.layernorm(x, eps)


@dataclass(frozen=True)
class EncodedObservation:
    """编码结果：实体行向量 + 全局向量 + 掩码（引用映射是侧信息，不进向量）。"""

    entities: Any
    global_vector: Any
    entity_mask: np.ndarray
    entity_count: int

    @property
    def d_entity(self) -> int:
        return int(ad.value_of(self.entities).shape[1])

    @property
    def d_global(self) -> int:
        return int(ad.value_of(self.global_vector).shape[0])


class SnapshotEncoder:
    """结构化 encoder（前向一次实现；推理走 numpy，训练走 tape）。"""

    def __init__(self, config: ModelConfig, table_sizes: Mapping[str, int]) -> None:
        if not isinstance(config, ModelConfig):
            raise TypeError(f"config 必须是 ModelConfig，得到 {type(config).__name__}")
        for table in ("entity_kinds", "content_types", "fields", "categories", "relation_types",
                      "action_types"):
            if table not in table_sizes or int(table_sizes[table]) <= 0:
                raise ValueError(f"字典表 {table} 的规模必须为正，得到 {table_sizes.get(table)!r}")
        self.config = config
        self.table_sizes = {name: int(size) for name, size in table_sizes.items()}
        #: 字段向量维度（实体字段与候选字段**共用**同一套字段编码：共享权重控制参数增长）
        self.field_dim = (
            config.d_field + config.d_value_type + 1 + 1 + config.d_category + config.d_entity + 1
        )
        self.entity_dim = config.d_entity_kind + config.d_content_type + config.d_content_state
        self.params = ParameterSet(dtype=config.float_dtype())
        self._build()
        self.params.randomize(config.seed)

    # -- 结构 ------------------------------------------------------------ #
    def _build(self) -> None:
        c = self.config
        p = self.params
        sizes = self.table_sizes
        # 字典索引 **0 保留给补齐**（`Dictionary.index_of` 返回 位置+1，真实条目从 1 开始），
        # 因此每张字典 embedding 需要 ``size + 1`` 行——少一行会在第一个"最后一条"内容上越界。
        rows = {name: int(size) + 1 for name, size in sizes.items()}
        p.add("emb/entity_kind", (rows["entity_kinds"], c.d_entity_kind), fan_in=c.d_entity_kind)
        p.add("emb/content_type", (rows["content_types"], c.d_content_type), fan_in=c.d_content_type)
        p.add("emb/content_state", (5, c.d_content_state), fan_in=c.d_content_state)
        p.add("emb/field", (rows["fields"], c.d_field), fan_in=c.d_field)
        p.add("emb/value_type", (VALUE_TYPE_COUNT, c.d_value_type), fan_in=c.d_value_type)
        p.add("emb/category", (rows["categories"], c.d_category), fan_in=c.d_category)
        p.add("emb/relation_type", (rows["relation_types"], c.d_relation_type),
              fan_in=c.d_relation_type)
        p.add("emb/action_type", (rows["action_types"], c.d_action_type), fan_in=c.d_action_type)

        p.add("entity/W_in", (self.entity_dim, c.d_entity), fan_in=self.entity_dim)
        p.add("entity/b_in", (c.d_entity,), fan_in=c.d_entity)
        p.add("field/W_field", (self.field_dim, c.d_field_hidden), fan_in=self.field_dim)
        p.add("field/b_field", (c.d_field_hidden,), fan_in=c.d_field_hidden)
        p.add("field/W_agg", (c.d_field_hidden, c.d_entity), fan_in=c.d_field_hidden)
        p.add("field/b_agg", (c.d_entity,), fan_in=c.d_entity)

        p.add("relation/W_msg", (c.d_entity, c.d_entity), fan_in=c.d_entity)
        p.add("relation/W_msg_back", (c.d_entity, c.d_entity), fan_in=c.d_entity)
        p.add("relation/W_type", (c.d_relation_type, c.d_entity), fan_in=c.d_relation_type)
        p.add("relation/W_upd", (c.d_entity, c.d_entity), fan_in=c.d_entity)
        p.add("relation/b_upd", (c.d_entity,), fan_in=c.d_entity)

        p.add("global/W_global", (2 * c.d_entity + COUNT_FEATURES, c.d_global),
              fan_in=2 * c.d_entity + COUNT_FEATURES)
        p.add("global/b_global", (c.d_global,), fan_in=c.d_global)

    # -- 参数视图 -------------------------------------------------------- #
    def parameter_vars(self) -> dict[str, ad.Var]:
        """训练用参数视图：每个参数包成 :class:`~ai_the_spire.models.autodiff.Var`，
        与 :class:`ParameterSet` 的数组**共享内存**（优化器就地更新即可写回模型）。"""
        return {name: ad.Var(array, requires_grad=True, name=name)
                for name, array in self.params.items()}

    def _p(self, name: str, vars: Optional[Mapping[str, ad.Var]]) -> Any:
        return vars[name] if vars is not None else self.params[name]

    # -- 前向 ------------------------------------------------------------ #
    def field_vectors(self, x: ModelInput, entity_vectors: Any, *,
                      candidate_fields: bool = False,
                      vars: Optional[Mapping[str, ad.Var]] = None) -> Any:
        """把字段行编成向量（实体字段与候选字段共用同一套参数）。

        ``entity_vectors`` 提供引用通道：``entity_ref`` 字段取**被引用实体行的向量**
        （这正是"候选引用实体不得丢"的落点）。
        """
        if candidate_fields:
            owner = x.candidate_field_owner
            field_id = x.candidate_field_id
            value_type = x.candidate_field_value_type
            number = x.candidate_field_number
            boolean = x.candidate_field_boolean
            category = x.candidate_field_category
            reference = x.candidate_field_reference
            reference_mask = x.candidate_field_reference_mask
        else:
            owner = x.field_owner
            field_id = x.field_id
            value_type = x.field_value_type
            number = x.field_number
            boolean = x.field_boolean
            category = x.field_category
            reference = x.field_reference
            reference_mask = x.field_reference_mask
        _ = owner

        rows = field_id.shape[0]
        if rows == 0:
            hidden = self.config.d_field_hidden
            return np.zeros((0, hidden), dtype=x.dtype)

        # 引用通道：行号 0 是合法实体，但 reference_mask=false 时不得贡献任何信息。
        safe_reference = np.where(reference_mask, reference, 0)
        reference_vectors = ad.where(
            reference_mask.reshape(-1, 1), ad.gather(entity_vectors, safe_reference), 0.0
        )
        category_vectors = ad.where(
            (value_type == _CATEGORY_TYPE).reshape(-1, 1),
            ad.gather(self._p("emb/category", vars), category),
            0.0,
        )
        features = ad.concatenate(
            [
                ad.gather(self._p("emb/field", vars), field_id),
                ad.gather(self._p("emb/value_type", vars), value_type),
                (number * self.config.number_scale).reshape(-1, 1),
                (boolean * self.config.boolean_scale).reshape(-1, 1),
                category_vectors,
                reference_vectors,
                reference_mask.astype(x.dtype).reshape(-1, 1),
            ],
            axis=1,
        )
        projected = ad.add(ad.matmul(features, self._p("field/W_field", vars)),
                           self._p("field/b_field", vars))
        return ad.tanh(projected)

    def forward(self, x: ModelInput, *,
                vars: Optional[Mapping[str, ad.Var]] = None) -> EncodedObservation:
        c = self.config
        p = lambda name: self._p(name, vars)  # noqa: E731 - 局部可读性别名
        mask = x.entity_mask.astype(x.dtype).reshape(-1, 1)
        rows = x.entity_count
        if rows == 0:
            raise ValueError("实体行为空：不得产出编码向量（快照退化，调用方必须先判空）")

        # ① 实体基础向量：kind + content（未知内容不贡献）+ content_state。
        base = ad.concatenate(
            [
                ad.gather(p("emb/entity_kind"), x.entity_kind),
                ad.mul(ad.gather(p("emb/content_type"), x.entity_content),
                       x.entity_content_mask.astype(x.dtype).reshape(-1, 1)),
                ad.gather(p("emb/content_state"), x.entity_state),
            ],
            axis=1,
        )
        h = ad.mul(ad.tanh(ad.add(ad.matmul(base, p("entity/W_in")), p("entity/b_in"))), mask)

        # ② 实体字段聚合（按 owner 行做掩码均值）。
        field_vectors = self.field_vectors(x, h, vars=vars)
        field_mask = x.field_mask.astype(x.dtype).reshape(-1, 1)
        masked_fields = ad.mul(field_vectors, field_mask)
        field_total = ad.scatter_add(masked_fields, x.field_owner, rows)
        field_counts = _counts(field_mask.reshape(-1), x.field_owner, rows)
        # 每个实体除以**自己的**字段数（按行缩放，不是跨行归约）。
        aggregated = ad.divide(field_total, _divisor(field_counts))
        h = ad.mul(
            ad.add(h, ad.tanh(ad.add(ad.matmul(aggregated, p("field/W_agg")), p("field/b_agg")))),
            mask,
        )

        # ③ L 轮稀疏关系传播（掩码关系不参与；无关系时只做恒等残差）。
        if x.relation_type.shape[0] > 0 and c.propagation_rounds > 0:
            relation_mask = x.relation_mask.astype(x.dtype).reshape(-1, 1)
            type_bias = ad.matmul(ad.gather(p("emb/relation_type"), x.relation_type),
                                  p("relation/W_type"))
            for _ in range(c.propagation_rounds):
                source = ad.mul(ad.gather(h, x.relation_source), relation_mask)
                target = ad.mul(ad.gather(h, x.relation_target), relation_mask)
                forward_msg = ad.mul(
                    ad.tanh(ad.add(ad.matmul(source, p("relation/W_msg")), type_bias)),
                    relation_mask,
                )
                backward_msg = ad.mul(
                    ad.tanh(ad.add(ad.matmul(target, p("relation/W_msg_back")), type_bias)),
                    relation_mask,
                )
                incoming = ad.scatter_add(forward_msg, x.relation_target, rows)
                outgoing = ad.scatter_add(backward_msg, x.relation_source, rows)
                incoming_counts = _counts(relation_mask.reshape(-1), x.relation_target, rows)
                outgoing_counts = _counts(relation_mask.reshape(-1), x.relation_source, rows)
                delta = ad.tanh(ad.add(
                    ad.matmul(
                        ad.add(ad.divide(incoming, _divisor(incoming_counts)),
                               ad.divide(outgoing, _divisor(outgoing_counts))),
                        p("relation/W_upd"),
                    ),
                    p("relation/b_upd"),
                ))
                h = ad.mul(_layernorm(ad.add(h, delta)), mask)

        # ④ 全局向量：实体集合的掩码均值/最大值 + 四个**有效行数**特征。
        #    计数用掩码和而不是行数：补齐行（mask=false）不得改变全局状态——否则"补几行"就成了特征。
        valid = x.entity_mask
        rows_valid = int(valid.sum())
        if rows_valid > 0:
            mean_vector = ad.divide(
                ad.total(ad.mul(h, valid.astype(x.dtype).reshape(-1, 1)), axis=0),
                float(rows_valid),
            )
            max_vector = ad.masked_row_max(h, valid)
        else:
            mean_vector = np.zeros(c.d_entity, dtype=x.dtype)
            max_vector = np.zeros(c.d_entity, dtype=x.dtype)
        counts = np.asarray(
            [
                np.log1p(rows_valid) / 10.0,
                np.log1p(int(x.field_mask.sum())) / 10.0,
                np.log1p(int(x.relation_mask.sum())) / 10.0,
                np.log1p(int(x.candidate_mask.sum())) / 10.0,
            ],
            dtype=x.dtype,
        )
        global_features = ad.concatenate([mean_vector, max_vector, counts], axis=0)
        global_vector = ad.tanh(ad.add(
            ad.reshape(ad.matmul(ad.reshape(global_features, (1, -1)), p("global/W_global")), (-1,)),
            p("global/b_global"),
        ))

        return EncodedObservation(
            entities=h,
            global_vector=global_vector,
            entity_mask=x.entity_mask,
            entity_count=rows,
        )

    # -- 报告 ------------------------------------------------------------ #
    def parameter_breakdown(self) -> dict[str, int]:
        return self.params.breakdown()

    def describe(self) -> dict[str, Any]:
        return {
            "config": self.config.to_wire(),
            "config_digest": self.config.digest(),
            "table_sizes": dict(self.table_sizes),
            "field_dim": self.field_dim,
            "entity_dim": self.entity_dim,
            "parameters": self.params.breakdown(),
            "parameter_total": self.params.total_size(),
        }


def _counts(mask_values: np.ndarray, index: np.ndarray, rows: int) -> np.ndarray:
    """按行号统计掩码计数（常量，不参与反向）。"""
    out = np.zeros(rows, dtype=np.float64)
    if index.shape[0] > 0:
        np.add.at(out, index, mask_values)
    return out


def _divisor(counts: np.ndarray) -> np.ndarray:
    """把计数变成按行相除的除数（``[n]`` → ``[n, 1]``，下界 1 避免除零）。"""
    return np.maximum(np.asarray(counts, dtype=np.float64), 1.0).reshape(-1, 1)
