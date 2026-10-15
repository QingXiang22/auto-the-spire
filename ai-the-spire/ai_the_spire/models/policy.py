"""策略接口：`predict(snapshot, model) -> PolicyEvaluation`（`policy-contract.md` §1）。

契约要点（逐条落在这里）：
* 只接收 **net 当前快照**；内部依次 validate/tensorize → encoder → scorer → **只在有效候选上归一化**；
* 不执行环境动作（执行属 runner/executor）；
* 返回每个合法候选的概率 + 内部状态价值 V，**不返回 Q**；
* 终止/无候选快照**不伪造**概率（显式错误），由调用方按终局/故障分开处理；
* 语义键来自**同一份快照的 legal_actions**（不是模型猜测的顺序），因此概率与执行器的对齐是可核对的。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping, Optional

from ..contracts import layers
from ..encoding.manifest import GymManifest
from ..encoding.tensor import TensorizedObservation, tensorize
from .arrays import ModelInput, build_model_input
from .config import ModelConfig
from .scorer import CandidateScorer, ScoredCandidates

#: 模型身份前缀；完整 model_id 带上结构与权重摘要（同一结构不同权重必须是不同身份）。
MODEL_ID_PREFIX = "small-embedding-scorer@1"


class PolicyError(RuntimeError):
    """策略层错误：调用方**不得**把它当成游戏失败或伪造的终局。"""


@dataclass(frozen=True)
class PolicyEvaluation:
    """一次策略求值的公开结果（可核对、可落盘）。"""

    model_id: str
    candidate_ids: tuple[str, ...]
    semantic_keys: tuple[str, ...]
    probabilities: tuple[float, ...]
    value: float
    valid_candidates: int
    tensor_layout_version: str
    dictionary_version: str
    manifest_digest: str
    model_config_digest: str
    weights_digest: str

    def probability_of(self, semantic_key: str) -> float:
        try:
            index = self.semantic_keys.index(semantic_key)
        except ValueError as exc:
            raise PolicyError(f"语义键不在本次候选里：{semantic_key!r}") from exc
        return self.probabilities[index]

    def by_semantic_key(self) -> dict[str, float]:
        return dict(zip(self.semantic_keys, self.probabilities))

    def to_wire(self) -> dict:
        return {
            "model_id": self.model_id,
            "value": self.value,
            "valid_candidates": self.valid_candidates,
            "tensor_layout_version": self.tensor_layout_version,
            "dictionary_version": self.dictionary_version,
            "manifest_digest": self.manifest_digest,
            "model_config_digest": self.model_config_digest,
            "weights_digest": self.weights_digest,
            "candidates": [
                {"candidate_id": candidate_id, "semantic_key": semantic_key,
                 "probability": probability}
                for candidate_id, semantic_key, probability
                in zip(self.candidate_ids, self.semantic_keys, self.probabilities)
            ],
        }


def semantic_keys_of(snapshot: Any) -> dict[str, str]:
    """快照的 `candidate_id → 动作语义键`（唯一来源：共享 L3 的规范序列化）。"""
    document = snapshot.to_wire() if hasattr(snapshot, "to_wire") else snapshot
    if not isinstance(document, Mapping):
        raise PolicyError(f"快照必须是对象，得到 {type(snapshot).__name__}")
    return {
        action["candidate_id"]: layers.action_semantic_key(action).decode("utf-8")
        for action in document.get("legal_actions") or []
    }


class SmallPolicyModel:
    """共享 embedding encoder + 候选 scorer + 价值头（未训练时是随机初始化，身份如实标注）。"""

    def __init__(self, config: ModelConfig, manifest: GymManifest, *, catalog: Any = None) -> None:
        if not isinstance(config, ModelConfig):
            raise TypeError(f"config 必须是 ModelConfig，得到 {type(config).__name__}")
        if not isinstance(manifest, GymManifest):
            raise TypeError(f"manifest 必须是 GymManifest，得到 {type(manifest).__name__}")
        self.config = config
        self.manifest = manifest
        self.catalog = catalog
        self.table_sizes = {name: manifest.dictionaries[name].size for name in manifest.dictionaries}
        self.scorer = CandidateScorer(config, self.table_sizes)
        self.weights_digest = ""
        self.model_id = ""
        self.refresh_identity()

    # -- 身份 ------------------------------------------------------------ #
    def refresh_identity(self) -> str:
        """按**实际权重内容**重算权重摘要与模型身份，返回新的 `model_id`。

        为什么必须是显式动作：`model_id` 在构造时按随机初始化权重派生，训练更新参数之后
        `weights_digest`/`model_id` 会**过期**——W-04 已复现"权重改变但 model_id 不变"，
        于是"候选模型"和"初始模型"看起来是同一个身份（`summary`/模型包/评估计划都会记错）。
        身份完全由内容派生：参数没变则摘要不变（**不伪造变化**），参数变了则身份必然跟着变。
        """
        self.weights_digest = self.scorer.params.content_digest()
        self.model_id = (
            f"{MODEL_ID_PREFIX}:{self.config.digest()[7:19]}:{self.weights_digest[7:19]}"
        )
        return self.model_id

    # -- 求值 ------------------------------------------------------------ #
    def predict(self, snapshot: Any, *, catalog: Any = None) -> PolicyEvaluation:
        """`snapshot` → 概率 + V。先做 A-03 校验与张量化（有错即抛，不返回部分结果）。"""
        resolved = catalog if catalog is not None else self.catalog
        observation = tensorize(snapshot, self.manifest, catalog=resolved)
        return self.evaluate_tensorized(
            observation, semantic_keys=semantic_keys_of(snapshot), catalog=resolved
        )

    def evaluate_tensorized(self, observation: TensorizedObservation, *,
                            semantic_keys: Mapping[str, str],
                            catalog: Any = None) -> PolicyEvaluation:
        """对**已张量化**的观察求值。

        `semantic_keys`（`candidate_id → 动作语义键`）必须由调用方给出：张量里只有规范索引，
        语义键的规范字节来自 wire 动作——不让模型层去猜或反推。
        """
        if not isinstance(observation, TensorizedObservation):
            raise TypeError(f"observation 必须是 TensorizedObservation，得到 {type(observation).__name__}")
        if not isinstance(semantic_keys, Mapping):
            raise PolicyError("semantic_keys 必须是 candidate_id → 语义键 的映射")
        if observation.metadata["manifest_digest"] != self.manifest.manifest_digest:
            raise PolicyError(
                "张量来自另一份 Manifest："
                f"{observation.metadata['manifest_digest']!r} != {self.manifest.manifest_digest!r}"
            )
        if observation.reference_map.candidate_count == 0:
            raise PolicyError(
                "当前快照没有候选（终止或故障边界）：策略不伪造概率，调用方必须按终局/故障分开处理"
            )
        _ = catalog
        model_input = build_model_input(observation, self.manifest, config=self.config)
        return self._evaluate_model_input(model_input, observation.reference_map, semantic_keys)

    def _evaluate_model_input(self, model_input: ModelInput, reference_map,
                              semantic_keys: Mapping[str, str]) -> PolicyEvaluation:
        scored: ScoredCandidates = self.scorer.score(
            model_input, self.scorer.encoder.forward(model_input)
        )
        probabilities = self.scorer.probabilities(scored)

        by_row: dict[int, str] = {}
        for candidate_id, row in reference_map.candidate_rows.items():
            if row in by_row:
                raise PolicyError(f"两候选落在同一行：{row}（引用映射必须一一对应）")
            by_row[row] = candidate_id
        if len(by_row) != model_input.candidate_count:
            raise PolicyError(
                f"候选行数与引用映射不一致：{len(by_row)} vs {model_input.candidate_count}"
            )
        candidate_ids = tuple(by_row[row] for row in range(model_input.candidate_count))
        resolved_keys = []
        for candidate_id in candidate_ids:
            if candidate_id not in semantic_keys:
                raise PolicyError(
                    f"候选 {candidate_id} 缺少语义键：调用方必须提供同一快照的 legal_actions"
                )
            resolved_keys.append(semantic_keys[candidate_id])

        return PolicyEvaluation(
            model_id=self.model_id,
            candidate_ids=candidate_ids,
            semantic_keys=tuple(resolved_keys),
            probabilities=tuple(float(value) for value in probabilities),
            value=float(scored.value),
            valid_candidates=int(model_input.valid_candidates),
            tensor_layout_version=self.manifest.tensor_layout_version,
            dictionary_version=self.manifest.dictionary_version,
            manifest_digest=self.manifest.manifest_digest,
            model_config_digest=self.config.digest(),
            weights_digest=self.weights_digest,
        )

    # -- 与 runner 的适配 ------------------------------------------------- #
    def policy(self, snapshot: Mapping[str, Any], candidates) -> dict[str, float]:
        """`EpisodeRunner(policy=...)` 适配器：返回"语义键 → 概率"，只覆盖真实候选。"""
        evaluation = self.predict(snapshot)
        probabilities = evaluation.by_semantic_key()
        expected = {candidate.semantic_key_text for candidate in candidates}
        if set(probabilities) != expected:
            raise PolicyError(
                "模型概率未恰好覆盖 runner 的真实候选："
                f"缺少 {sorted(expected - set(probabilities))}，多出 {sorted(set(probabilities) - expected)}"
            )
        return probabilities

    # -- 报告 ------------------------------------------------------------ #
    def describe(self) -> dict:
        return {
            "model_id": self.model_id,
            "weights_digest": self.weights_digest,
            "config_digest": self.config.digest(),
            "parameters": self.scorer.params.breakdown(),
            "parameter_total": self.scorer.params.total_size(),
            "dictionary_version": self.manifest.dictionary_version,
            "tensor_layout_version": self.manifest.tensor_layout_version,
            "manifest_digest": self.manifest.manifest_digest,
            "value_head": "internal_state_value_only(no_Q)",
        }


def predict(snapshot: Any, model: SmallPolicyModel, *, catalog: Any = None) -> PolicyEvaluation:
    """模块级入口（与 `policy-contract.md` 的 `predict(snapshot, model_bundle)` 同形）。"""
    if not isinstance(model, SmallPolicyModel):
        raise TypeError(f"model 必须是 SmallPolicyModel，得到 {type(model).__name__}")
    return model.predict(snapshot, catalog=catalog)
