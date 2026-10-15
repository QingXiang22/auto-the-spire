"""学习器：**numpy 手写的**反向传播驱动的 Adam 与近期策略 PPO（用户 2026-10-03 裁定不加训练框架）。

覆盖范围与边界（`training-design.md`、`policy-contract.md`）：

* **策略梯度用真实行为分布 π**：本模块只做"给定样本 → 更新"，采样与执行仍由 runner/executor 负责；
  首版令 **μ=π**（直接从被优化的掩码 π 采样），因此比率项的新旧 log-prob **同源**；
* 目标函数 = PPO 截断代理目标 + 价值回归 + 可选熵正则；系数、截断范围、学习率、轮数、梯度裁剪**全部显式**配置；
* 梯度由 :mod:`ai_the_spire.models.autodiff` 的反向模式给出（前向只写一遍），
  `tests/model_learning_suite.py` 用**有限差分**对照解析梯度；
* 本模块**不**声称学习效果：能不能学会由显式小任务与评估决定，不由"实现了 PPO"推得。
"""

from __future__ import annotations

import hashlib
import math
from dataclasses import dataclass, field, fields
from typing import Any, Iterable, Mapping, Optional, Sequence

import numpy as np

from ..contracts import layers
from . import autodiff as ad
from .arrays import ModelInput
from .config import ModelConfig
from .policy import SmallPolicyModel
from .scorer import CandidateScorer, ScoredCandidates

#: 学习配置格式版本。
TRAINING_CONFIG_VERSION = "training-config@1"


class LearningError(RuntimeError):
    """学习器错误：配置非法、梯度缺失、样本与策略不一致等（一律显式失败）。"""


def _canonical_decimal(value: float) -> str:
    return repr(float(value))


@dataclass(frozen=True)
class TrainingConfig:
    """显式训练超参数（不读环境变量、不按机器自适应）。"""

    learning_rate: float = 0.02
    clip_ratio: float = 0.2
    epochs: int = 4
    value_coef: float = 0.5
    entropy_coef: float = 0.0
    max_grad_norm: float = 1.0
    adam_beta1: float = 0.9
    adam_beta2: float = 0.999
    adam_epsilon: float = 1e-8
    seed: int = 20261003

    def __post_init__(self) -> None:
        numeric = {
            "learning_rate": (self.learning_rate, 0.0, None),
            "clip_ratio": (self.clip_ratio, 0.0, 1.0),
            "value_coef": (self.value_coef, 0.0, None),
            "entropy_coef": (self.entropy_coef, 0.0, None),
            "max_grad_norm": (self.max_grad_norm, 0.0, None),
            # Adam 的 β 必须**严格小于 1**：β=1 会让偏差修正除以 0（1 - β^t = 0）。
            "adam_beta1": (self.adam_beta1, 0.0, 1.0),
            "adam_beta2": (self.adam_beta2, 0.0, 1.0),
            "adam_epsilon": (self.adam_epsilon, 0.0, None),
        }
        for name, (value, low, high) in numeric.items():
            if isinstance(value, bool) or not isinstance(value, (int, float)):
                raise LearningError(f"{name} 必须是数值，得到 {value!r}")
            if not math.isfinite(float(value)) or float(value) < low or (high is not None and float(value) > high):
                raise LearningError(f"{name}={value!r} 不在 [{low}, {high if high is not None else '∞'}] 内")
        for name in ("adam_beta1", "adam_beta2"):
            if float(getattr(self, name)) >= 1.0:
                raise LearningError(f"{name} 必须严格小于 1（否则偏差修正除以 0）")
        if self.learning_rate <= 0.0:
            raise LearningError("learning_rate 必须为正（0 会让'训练'变成空操作）")
        if self.max_grad_norm <= 0.0:
            raise LearningError("max_grad_norm 必须为正（否则梯度会被整体清零）")
        for name in ("epochs", "seed"):
            value = getattr(self, name)
            if isinstance(value, bool) or not isinstance(value, int) or value < (1 if name == "epochs" else 0):
                raise LearningError(f"{name}={value!r} 非法")

    def to_wire(self) -> dict:
        document = {"training_config_version": TRAINING_CONFIG_VERSION}
        for item in fields(self):
            value = getattr(self, item.name)
            document[item.name] = _canonical_decimal(value) if isinstance(value, float) else value
        return document

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "TrainingConfig":
        expected = {"training_config_version"} | {item.name for item in fields(cls)}
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise LearningError(f"TrainingConfig 键不匹配：缺少 {missing}，多出 {extra}")
        if doc["training_config_version"] != TRAINING_CONFIG_VERSION:
            raise LearningError(
                f"TrainingConfig 版本 {doc['training_config_version']!r} 与当前 {TRAINING_CONFIG_VERSION!r} 不同"
            )
        values = {}
        for item in fields(cls):
            raw = doc[item.name]
            if item.name in ("learning_rate", "clip_ratio", "value_coef", "entropy_coef",
                             "max_grad_norm", "adam_beta1", "adam_beta2", "adam_epsilon"):
                if isinstance(raw, bool) or not isinstance(raw, (int, float, str)):
                    raise LearningError(f"{item.name} 必须是数值或规范十进制字符串，得到 {raw!r}")
                try:
                    values[item.name] = float(raw)
                except (TypeError, ValueError) as failure:
                    raise LearningError(f"{item.name} 不是可解析的数值：{raw!r}") from failure
            else:
                values[item.name] = raw
        return cls(**values)

    def digest(self) -> str:
        return "sha256:" + hashlib.sha256(layers.canonical_bytes(self.to_wire())).hexdigest()


# --------------------------------------------------------------------------- #
# Adam
# --------------------------------------------------------------------------- #
class Adam:
    """Adam（手写）：一阶/二阶矩按参数名存储，可导出/恢复（检查点用）。"""

    def __init__(self, params: Mapping[str, ad.Var], config: TrainingConfig) -> None:
        self.config = config
        self.names = tuple(sorted(params))
        self._params = {name: params[name] for name in self.names}
        self._first = {name: np.zeros_like(self._params[name].data) for name in self.names}
        self._second = {name: np.zeros_like(self._params[name].data) for name in self.names}
        self.steps = 0

    def zero_grad(self) -> None:
        for var in self._params.values():
            var.zero_grad()

    def step(self) -> dict:
        """执行一次更新；返回诊断（梯度范数、被裁剪比例、是否有参数没有梯度）。"""
        grads = {name: var.grad for name, var in self._params.items()}
        missing = sorted(name for name, grad in grads.items() if grad is None)
        if missing:
            raise LearningError(f"参数没有梯度（前向没连上）：{missing[:4]}{' …' if len(missing) > 4 else ''}")
        total_norm = math.sqrt(sum(float(np.sum(grad * grad)) for grad in grads.values()))
        scale = 1.0
        if total_norm > self.config.max_grad_norm:
            scale = self.config.max_grad_norm / (total_norm + 1e-12)
        self.steps += 1
        beta1, beta2 = self.config.adam_beta1, self.config.adam_beta2
        for name in self.names:
            grad = grads[name] * scale
            self._first[name] = beta1 * self._first[name] + (1.0 - beta1) * grad
            self._second[name] = beta2 * self._second[name] + (1.0 - beta2) * (grad * grad)
            first_hat = self._first[name] / (1.0 - beta1 ** self.steps)
            second_hat = self._second[name] / (1.0 - beta2 ** self.steps)
            update = self.config.learning_rate * first_hat / (np.sqrt(second_hat) + self.config.adam_epsilon)
            # 就地写回：参数 Var 与 ParameterSet 共享同一块内存，因此模型立即更新。
            self._params[name].data[...] = self._params[name].data - update
        return {
            "steps": self.steps,
            "grad_norm": total_norm,
            "grad_scale": scale,
            "clipped": scale < 1.0,
        }

    # -- 检查点 ---------------------------------------------------------- #
    def state(self) -> dict[str, np.ndarray]:
        return {f"first/{name}": self._first[name].copy() for name in self.names} | {
            f"second/{name}": self._second[name].copy() for name in self.names
        }

    def load_state(self, state: Mapping[str, np.ndarray], *, steps: int) -> None:
        expected = {f"{slot}/{name}" for slot in ("first", "second") for name in self.names}
        if set(state) != expected:
            missing = sorted(expected - set(state))
            extra = sorted(set(state) - expected)
            raise LearningError(f"优化器状态键不匹配：缺少 {missing[:4]}，多出 {extra[:4]}")
        for name in self.names:
            for slot, store in (("first", self._first), ("second", self._second)):
                array = np.asarray(state[f"{slot}/{name}"])
                if array.shape != store[name].shape:
                    raise LearningError(
                        f"优化器状态 {slot}/{name} 形状 {array.shape} 与参数 {store[name].shape} 不一致"
                    )
                store[name][...] = array
        if isinstance(steps, bool) or not isinstance(steps, int) or steps < 0:
            raise LearningError(f"优化器步数非法：{steps!r}")
        self.steps = steps

    def digest(self) -> str:
        digest = hashlib.sha256()
        for name in self.names:
            for slot, store in (("first", self._first), ("second", self._second)):
                array = np.ascontiguousarray(store[name])
                digest.update(f"{slot}/{name}".encode("utf-8"))
                digest.update(str(array.dtype).encode("utf-8"))
                digest.update(repr(tuple(int(dim) for dim in array.shape)).encode("utf-8"))
                digest.update(array.tobytes())
        return "sha256:" + digest.hexdigest()


# --------------------------------------------------------------------------- #
# 样本与目标函数
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class PolicySample:
    """一份用于更新的样本：当前观测 + 行为策略的选择与优势/回报。

    ``old_log_prob`` 必须来自**采样时那条策略**（首版 μ=π，因此它与本轮前向同源）。
    """

    model_input: ModelInput
    action_index: int
    old_log_prob: float
    advantage: float
    return_value: float
    semantic_key: str = ""

    def __post_init__(self) -> None:
        if isinstance(self.action_index, bool) or not isinstance(self.action_index, int):
            raise LearningError(f"action_index 必须是整数，得到 {self.action_index!r}")
        if not 0 <= self.action_index < self.model_input.candidate_count:
            raise LearningError(
                f"action_index {self.action_index} 越界（候选数 {self.model_input.candidate_count}）"
            )
        if not self.model_input.candidate_mask[self.action_index]:
            raise LearningError(f"动作行 {self.action_index} 是被掩码的候选：不得作为样本")
        for name in ("old_log_prob", "advantage", "return_value"):
            value = getattr(self, name)
            if not isinstance(value, (int, float)) or isinstance(value, bool) or not math.isfinite(float(value)):
                raise LearningError(f"{name} 必须是有限数值，得到 {value!r}")


@dataclass
class UpdateStats:
    """一次更新的诊断（可核对，不冒充学习效果）。"""

    loss: float
    policy_loss: float
    value_loss: float
    entropy: float
    grad_norm: float
    grad_scale: float
    samples: int
    steps: int
    clip_fraction: float

    def to_wire(self) -> dict:
        return dict(self.__dict__)


def masked_log_prob(probabilities: Any, index: int) -> Any:
    """``log π(a)``（PPO 比率项用它；与 `negative_log_prob` 同源，只是取负）。"""
    return ad.scale(ad.negative_log_prob(probabilities, index), -1.0)


def ppo_loss(model: SmallPolicyModel, samples: Sequence[PolicySample], config: TrainingConfig, *,
             vars: Mapping[str, ad.Var]) -> tuple[ad.Var, dict]:
    """PPO 截断代理目标 + 价值回归 + 熵正则（对样本取平均）。返回 (loss, 诊断)。"""
    if not samples:
        raise LearningError("没有样本就没有更新目标")
    per_sample: list[ad.Var] = []
    policy_terms: list[float] = []
    value_terms: list[float] = []
    entropy_terms: list[float] = []
    ratios: list[float] = []
    clip_hits = 0
    for sample in samples:
        encoded = model.scorer.encoder.forward(sample.model_input, vars=vars)
        scored: ScoredCandidates = model.scorer.score(sample.model_input, encoded, vars=vars)
        probabilities = ad.masked_softmax(scored.logits, scored.candidate_mask)
        # 比率 = exp(log π_new(a) - log π_old(a))：**两者都必须是 log 概率**。
        # （早先误用 `negative_log_prob` 相当于把比率取成倒数，截断后梯度全被吃掉，训练不升反降。）
        log_prob = masked_log_prob(probabilities, sample.action_index)
        ratio = ad.exp(ad.sub(log_prob, float(sample.old_log_prob)))
        clipped_ratio = ad.clip_scalar(ratio, 1.0 - config.clip_ratio, 1.0 + config.clip_ratio)
        unclipped = ad.scale(ratio, float(sample.advantage))
        clipped = ad.scale(clipped_ratio, float(sample.advantage))
        surrogate = ad.minimum(unclipped, clipped)
        entropy = ad.entropy(probabilities)
        value_error = ad.sub(scored.value, float(sample.return_value))
        value_loss = ad.mul(value_error, value_error)
        total = ad.add(
            ad.add(ad.scale(surrogate, -1.0), ad.scale(value_loss, float(config.value_coef))),
            ad.scale(entropy, -float(config.entropy_coef)),
        )
        per_sample.append(total)

        ratio_value = float(np.asarray(ad.value_of(ratio)))
        ratios.append(ratio_value)
        if abs(ratio_value - 1.0) > config.clip_ratio:
            clip_hits += 1
        policy_terms.append(-float(np.asarray(ad.value_of(surrogate))))
        value_terms.append(float(np.asarray(ad.value_of(value_loss))))
        entropy_terms.append(float(np.asarray(ad.value_of(entropy))))

    loss = ad.scalar_mean(per_sample)
    diagnostics = {
        "policy_loss": float(np.mean(policy_terms)),
        "value_loss": float(np.mean(value_terms)),
        "entropy": float(np.mean(entropy_terms)),
        "clip_fraction": clip_hits / len(samples),
        "mean_ratio": float(np.mean(ratios)),
    }
    return loss, diagnostics


def train_epoch(model: SmallPolicyModel, samples: Sequence[PolicySample], optimizer: Adam,
                config: TrainingConfig, *,
                vars: Mapping[str, ad.Var]) -> UpdateStats:
    """一次参数更新：清零梯度 → 前向+反向 → 梯度裁剪 → Adam 更新。

    ``vars`` 必须与 ``optimizer`` 是**同一批** Var（否则梯度落在别处、参数也更新不到）——
    请用 :class:`Trainer` 构造，避免这种误用。
    """
    optimizer.zero_grad()
    loss, diagnostics = ppo_loss(model, samples, config, vars=vars)
    ad.backward(loss)
    step = optimizer.step()
    return UpdateStats(
        loss=float(np.asarray(ad.value_of(loss))),
        policy_loss=diagnostics["policy_loss"],
        value_loss=diagnostics["value_loss"],
        entropy=diagnostics["entropy"],
        grad_norm=step["grad_norm"],
        grad_scale=step["grad_scale"],
        samples=len(samples),
        steps=step["steps"],
        clip_fraction=diagnostics["clip_fraction"],
    )


@dataclass
class Trainer:
    """把"参数视图 + 优化器 + 更新"绑在一起的入口（避免视图与优化器错配）。"""

    model: SmallPolicyModel
    config: TrainingConfig
    vars: Mapping[str, ad.Var] = field(init=False)
    optimizer: Adam = field(init=False)
    history: list = field(default_factory=list, init=False)

    def __post_init__(self) -> None:
        self.vars = self.model.scorer.parameter_vars()
        self.optimizer = Adam(self.vars, self.config)

    #: 便于外部读参数（与模型共享内存）。
    @property
    def parameters(self) -> Mapping[str, ad.Var]:
        return self.vars

    def update(self, samples: Sequence[PolicySample]) -> UpdateStats:
        """对同一批样本做 ``config.epochs`` 次梯度步（首版 PPO 的多轮复用）。

        第 1 轮时比率恰好为 1（μ=π 且样本来自当前参数）；后续轮次的比率会偏离 1，
        由截断项限制步幅——这正是 `training-design.md` 要检验的闭环。
        """
        stats = train_epoch(self.model, samples, self.optimizer, self.config, vars=self.vars)
        for _ in range(self.config.epochs - 1):
            stats = train_epoch(self.model, samples, self.optimizer, self.config, vars=self.vars)
        self.history.append(stats)
        return stats

    def train(self, batches: Iterable[Sequence[PolicySample]]) -> list:
        return [self.update(batch) for batch in batches]

    # -- 检查点 ---------------------------------------------------------- #
    def optimizer_state(self) -> dict[str, np.ndarray]:
        return self.optimizer.state()

    def load_optimizer_state(self, state: Mapping[str, np.ndarray], *, steps: int) -> None:
        self.optimizer.load_state(state, steps=steps)


def behaviour_log_prob(model: SmallPolicyModel, model_input: ModelInput, action_index: int) -> float:
    """用当前参数（μ=π）计算行为策略下该动作的 log 概率。"""
    encoded = model.scorer.encoder.forward(model_input)
    scored = model.scorer.score(model_input, encoded)
    probabilities = np.asarray(model.scorer.probabilities(scored))
    selected = float(probabilities[action_index])
    if selected <= 0.0:
        raise LearningError(f"行为策略下动作 {action_index} 概率为 0：不产生该样本")
    return math.log(selected)


def action_probabilities(model: SmallPolicyModel, model_input: ModelInput) -> np.ndarray:
    """当前策略的掩码概率（合成任务与评估用；等价于 `predict` 的模型部分）。"""
    encoded = model.scorer.encoder.forward(model_input)
    scored = model.scorer.score(model_input, encoded)
    return np.asarray(model.scorer.probabilities(scored))


def sample_index(probabilities: np.ndarray, rng: np.random.Generator) -> int:
    """按给定概率抽一个候选下标（**合成任务用**；真实 Gym 的执行器身份另见 runner/executor）。"""
    total = float(probabilities.sum())
    if total <= 0.0:
        raise LearningError("概率总和为 0：无法采样")
    draw = float(rng.random()) * total
    cumulative = np.cumsum(probabilities)
    index = int(np.searchsorted(cumulative, draw, side="right"))
    return min(index, len(probabilities) - 1)
