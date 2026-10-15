#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""W-04 学习工程检查：梯度正确性、参数更新、学习曲线与检查点恢复。

分组：

* ``learning-grad``：**有限差分**对照自动微分的解析梯度（逐算子 + 整模型选定参数）；
* ``learning-update``：有限合成任务上验证"损失下降、正确率上升、参数确实变了、同种子可复现"；
* ``learning-restore``：参数 + 优化器状态的检查点往返，恢复后继续训练与不中断训练**逐步一致**。

**证据边界**：合成任务是**可判定的小任务**（只需比较两个候选的数值字段），
它证明的是**学习工程**（梯度/更新/恢复）成立，**不**证明尖塔上的学习效果——
后者要等 W-07 冻结奖励/偏好与预算，并在真实 Gym 上做有界对照。
"""

from __future__ import annotations

import json
import pathlib
import shutil
import sys
import tempfile
from typing import Any, Callable, Optional

import numpy as np

_PACKAGE_ROOT = pathlib.Path(__file__).resolve().parents[1]
if str(_PACKAGE_ROOT) not in sys.path:
    sys.path.insert(0, str(_PACKAGE_ROOT))
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from ai_the_spire.models import ModelBundleError, ModelConfig, SmallPolicyModel, load_bundle, save_bundle  # noqa: E402
from ai_the_spire.models import autodiff as ad  # noqa: E402
from ai_the_spire.models.arrays import ModelInput  # noqa: E402
from ai_the_spire.models.learning import (  # noqa: E402
    LearningError,
    PolicySample,
    Trainer,
    TrainingConfig,
    action_probabilities,
    ppo_loss,
    sample_index,
)

import tensor_suite  # noqa: E402
from fixture_suite import Outcome  # noqa: E402

SUITE_GRAD = "learning-grad"
SUITE_UPDATE = "learning-update"
SUITE_RESTORE = "learning-restore"

SUITE_NAMES = (SUITE_GRAD, SUITE_UPDATE, SUITE_RESTORE)

BUNDLE = "combat"


def _outcome(suite: str, case_id: str, ok: bool, detail: str = "") -> Outcome:
    return Outcome(suite, case_id, bool(ok), detail)


def _guard(suite: str, case_id: str, run: Callable[[], Any]) -> Outcome:
    try:
        result = run()
    except Exception as exc:  # noqa: BLE001 - 单条失败不中止整套
        return _outcome(suite, case_id, False, f"{type(exc).__name__}: {exc}")
    if isinstance(result, Outcome):
        return result
    ok, detail = result
    return _outcome(suite, case_id, ok, detail)


class _Context:
    def __init__(self) -> None:
        self._manifest = None
        self._table_sizes = None

    def manifest(self):
        if self._manifest is None:
            self._manifest = tensor_suite.manifest_for(BUNDLE)
        return self._manifest

    def table_sizes(self) -> dict:
        if self._table_sizes is None:
            manifest = self.manifest()
            self._table_sizes = {name: manifest.dictionaries[name].size
                                 for name in manifest.dictionaries}
        return self._table_sizes

    def model(self, config: Optional[ModelConfig] = None) -> SmallPolicyModel:
        return SmallPolicyModel(config or ModelConfig(), self.manifest())


# --------------------------------------------------------------------------- #
# 有限合成任务
# --------------------------------------------------------------------------- #
#: 两个候选各自的"数值字段"——任务就是**学会挑数值更大的那个**（不需要任何游戏规则）。
SYNTHETIC_FIELD_ID = 1
SYNTHETIC_ACTION_TYPES = (1, 2)


def synthetic_input(dtype, table_sizes: dict, values: tuple) -> ModelInput:
    """造一份最小但**结构完整**的模型输入：2 实体 / 2 字段 / 1 关系 / 2 候选 / 2 候选字段。

    ``values`` 是两个候选各自的数值通道取值；正确动作 = 数值更大的那个。
    索引都落在真实 Manifest 的字典范围内（不越界、不自造类型）。
    """
    correct = 0 if values[0] > values[1] else 1
    entities = 2
    return ModelInput(
        table_sizes=table_sizes,
        dtype=dtype,
        entity_kind=np.asarray([1, 1], dtype=np.int64),
        entity_content=np.asarray([1, 1], dtype=np.int64),
        entity_content_mask=np.asarray([True, True]),
        entity_state=np.asarray([1, 1], dtype=np.int64),
        entity_mask=np.asarray([True, True]),
        field_owner=np.asarray([0, 1], dtype=np.int64),
        field_id=np.asarray([SYNTHETIC_FIELD_ID, SYNTHETIC_FIELD_ID], dtype=np.int64),
        field_value_type=np.asarray([1, 1], dtype=np.int64),
        field_state=np.asarray([1, 1], dtype=np.int64),
        field_number=np.asarray([0.5, 1.5], dtype=dtype),
        field_boolean=np.asarray([0.0, 1.0], dtype=dtype),
        field_category=np.asarray([0, 0], dtype=np.int64),
        field_reference=np.asarray([1, 0], dtype=np.int64),
        field_reference_mask=np.asarray([True, True]),
        field_mask=np.asarray([True, True]),
        relation_type=np.asarray([1], dtype=np.int64),
        relation_source=np.asarray([0], dtype=np.int64),
        relation_target=np.asarray([1], dtype=np.int64),
        relation_mask=np.asarray([True]),
        candidate_action=np.asarray(SYNTHETIC_ACTION_TYPES, dtype=np.int64),
        candidate_mask=np.asarray([True, True]),
        candidate_field_owner=np.asarray([0, 1], dtype=np.int64),
        candidate_field_id=np.asarray([SYNTHETIC_FIELD_ID, SYNTHETIC_FIELD_ID], dtype=np.int64),
        candidate_field_value_type=np.asarray([1, 1], dtype=np.int64),
        candidate_field_state=np.asarray([1, 1], dtype=np.int64),
        candidate_field_number=np.asarray(values, dtype=dtype),
        candidate_field_boolean=np.asarray([0.0, 0.0], dtype=dtype),
        candidate_field_category=np.asarray([0, 0], dtype=np.int64),
        candidate_field_reference=np.asarray([0, 1], dtype=np.int64),
        candidate_field_reference_mask=np.asarray([True, True]),
        candidate_field_mask=np.asarray([True, True]),
    ), correct


def synthetic_batch(rng: np.random.Generator, dtype, table_sizes: dict, size: int) -> list:
    """生成一批样本：行为策略 π 采样 + 优势 = 正确 +1 / 错误 -1，回报 = 正确 1 / 错误 0。"""
    batch = []
    for _ in range(size):
        values = (float(rng.uniform(0.1, 0.9)), float(rng.uniform(1.1, 1.9)))
        model_input, correct = synthetic_input(dtype, table_sizes, values)
        batch.append((model_input, correct))
    return batch


def collect_samples(trainer: Trainer, episodes: list, rng: np.random.Generator) -> list:
    """用**当前**策略（μ=π）采样动作并构造样本。"""
    samples = []
    for model_input, correct in episodes:
        probabilities = action_probabilities(trainer.model, model_input)
        index = sample_index(probabilities, rng)
        log_prob = float(np.log(max(probabilities[index], 1e-300)))
        reward = 1.0 if index == correct else 0.0
        samples.append(PolicySample(
            model_input=model_input,
            action_index=index,
            old_log_prob=log_prob,
            advantage=1.0 if reward > 0.5 else -1.0,
            return_value=reward,
        ))
    return samples


def task_accuracy(model: SmallPolicyModel, dtype, table_sizes: dict, rng: np.random.Generator,
                  size: int = 32) -> tuple:
    """在**新**合成样本上测正确率与"正确候选的平均概率"。"""
    hits = 0
    probabilities = []
    for _ in range(size):
        values = (float(rng.uniform(0.1, 0.9)), float(rng.uniform(1.1, 1.9)))
        model_input, correct = synthetic_input(dtype, table_sizes, values)
        probs = action_probabilities(model, model_input)
        probabilities.append(float(probs[correct]))
        if int(np.argmax(probs)) == correct:
            hits += 1
    return hits / size, float(np.mean(probabilities))


# --------------------------------------------------------------------------- #
# learning-grad
# --------------------------------------------------------------------------- #
def _fd_gradient(f: Callable[[], float], array: np.ndarray, indices, eps: float = 1e-6) -> dict:
    grads = {}
    for index in indices:
        original = float(array[index])
        array[index] = original + eps
        plus = f()
        array[index] = original - eps
        minus = f()
        array[index] = original
        grads[index] = (plus - minus) / (2 * eps)
    return grads


def _grad_cases(context: _Context) -> list:
    def op_gradients_match_finite_differences() -> tuple:
        rng = np.random.default_rng(11)
        problems = []

        def compare(name: str, analytic: np.ndarray, numeric: np.ndarray, tol: float = 1e-6) -> None:
            if analytic.shape != numeric.shape:
                problems.append(f"{name}: 形状 {analytic.shape} vs {numeric.shape}")
                return
            error = float(np.abs(analytic - numeric).max()) if analytic.size else 0.0
            if not (error < tol):
                problems.append(f"{name}: max|err|={error:.3e}")

        def elementwise_fd(values: np.ndarray, evaluate: Callable[[np.ndarray], float],
                            eps: float = 1e-6) -> np.ndarray:
            numeric = np.zeros_like(values)
            flat = values.reshape(-1)
            for position in range(flat.size):
                original = flat[position]
                flat[position] = original + eps
                plus = evaluate(values)
                flat[position] = original - eps
                minus = evaluate(values)
                flat[position] = original
                numeric.reshape(-1)[position] = (plus - minus) / (2 * eps)
            return numeric

        # tanh + matmul（对右操作数）
        x = rng.normal(size=(3, 4))
        w = rng.normal(size=(4, 5))
        xv, wv = ad.make("x", x.copy()), ad.make("w", w.copy())
        ad.backward(ad.total(ad.total(ad.tanh(ad.matmul(xv, wv)), axis=0), axis=0))
        compare("matmul/tanh w", wv.grad, elementwise_fd(w, lambda a: float(np.tanh(x @ a).sum())))

        # layernorm
        a = rng.normal(size=(4, 6))
        av = ad.make("a", a.copy())
        ad.backward(ad.total(ad.total(ad.layernorm(av), axis=0), axis=0))
        compare("layernorm", av.grad,
                elementwise_fd(a, lambda v: float(ad.total(ad.layernorm(v), axis=0).sum())))

        # masked softmax + entropy（熵对 logits 的导数必须经过 softmax 的投影）
        logits = rng.normal(size=4)
        mask = np.asarray([True, True, False, True])
        lv = ad.make("logits", logits.copy())

        def entropy_of(values: np.ndarray) -> float:
            shifted = values - values[mask].max()
            exp = np.where(mask, np.exp(shifted), 0.0)
            probs = exp / exp.sum()
            safe = np.where(probs > 0, probs, 1.0)
            return float(-np.where(probs > 0, probs * np.log(safe), 0.0).sum())

        ad.backward(ad.entropy(ad.masked_softmax(lv, mask)))
        compare("masked_softmax+entropy", lv.grad, elementwise_fd(logits, entropy_of), tol=1e-5)

        # scatter_add：把 4 行按行号散射进 6 行；对**被散射的值**的梯度就是取回上游（全 1）
        table = rng.normal(size=(6, 3))
        index = np.asarray([0, 2, 2, 4])
        values = rng.normal(size=(index.size, 3))
        sv = ad.make("values", values.copy())
        ad.backward(ad.total(ad.total(ad.scatter_add(sv, index, 6), axis=0), axis=0))
        compare("scatter_add", sv.grad, np.ones_like(values))

        # gather：解析梯度 = 按同一行号取回上游梯度（上游为全 1）⇒ 每行等于被引用次数
        tv = ad.make("t", table.copy())
        ad.backward(ad.total(ad.total(ad.gather(tv, index), axis=0), axis=0))
        counts = np.asarray([float((index == row).sum()) for row in range(table.shape[0])])
        compare("gather", tv.grad, np.tile(counts.reshape(-1, 1), (1, table.shape[1])))

        # where / maximum / minimum / clip：分段线性，梯度必须落在被选中的那支
        p = rng.normal(size=5)
        q = rng.normal(size=5)
        condition = np.asarray([True, False, True, False, True])
        pv, qv = ad.make("p", p.copy()), ad.make("q", q.copy())
        ad.backward(ad.total(ad.where(condition, pv, qv), axis=0))
        compare("where p", pv.grad, condition.astype(float))
        compare("where q", qv.grad, (~condition).astype(float))

        mv = ad.make("m", p.copy())
        ad.backward(ad.total(ad.maximum(mv, q), axis=0))
        compare("maximum", mv.grad, (p >= q).astype(float))

        cv = ad.make("c", p.copy())
        ad.backward(ad.total(ad.clip_scalar(cv, -0.5, 0.5), axis=0))
        compare("clip", cv.grad, ((p > -0.5) & (p < 0.5)).astype(float))

        return (not problems, "; ".join(problems))

    def model_gradient_matches_finite_differences() -> tuple:
        dtype = np.float64
        model = context.model()
        table_sizes = context.table_sizes()
        rng = np.random.default_rng(5)
        episodes = synthetic_batch(rng, dtype, table_sizes, size=3)
        samples = [
            PolicySample(model_input=model_input, action_index=correct,
                         old_log_prob=-0.69, advantage=1.0, return_value=1.0)
            for model_input, correct in episodes
        ]
        config = TrainingConfig(entropy_coef=0.1)

        def loss_value() -> float:
            loss, _ = ppo_loss(model, samples, config, vars=model.scorer.parameter_vars())
            return float(np.asarray(ad.value_of(loss)))

        trainer = Trainer(model, config)
        loss, _ = ppo_loss(model, samples, trainer.config, vars=trainer.vars)
        ad.backward(loss)

        checked = []
        problems = []
        for name, positions in (
            ("candidate/W_in", [(0, 0), (1, 1), (2, 0)]),
            ("candidate/v", [(0,), (2,)]),
            ("field/W_field", [(0, 0), (1, 2)]),
            ("emb/action_type", [(1, 0), (2, 1)]),
            ("value/w_out", [(0,), (1,)]),
        ):
            array = model.scorer.params[name]
            numeric = _fd_gradient(loss_value, array, positions, eps=1e-6)
            for index in positions:
                analytic = float(trainer.vars[name].grad[index])
                scale = max(1.0, abs(numeric[index]))
                if abs(analytic - numeric[index]) / scale > 1e-4:
                    problems.append(f"{name}{index}: analytic={analytic:.6g} numeric={numeric[index]:.6g}")
            checked.append(name)
        return (not problems, f"checked={checked} problems={problems[:3]}")

    return [
        _guard(SUITE_GRAD, "grad/operators-match-finite-differences", op_gradients_match_finite_differences),
        _guard(SUITE_GRAD, "grad/full-model-matches-finite-differences",
               model_gradient_matches_finite_differences),
    ]


# --------------------------------------------------------------------------- #
# learning-update
# --------------------------------------------------------------------------- #
def _update_cases(context: _Context) -> list:
    def synthetic_task_learns() -> tuple:
        dtype = np.float64
        table_sizes = context.table_sizes()
        model = context.model()
        trainer = Trainer(model, TrainingConfig(learning_rate=0.05, epochs=1, entropy_coef=0.0))
        rng = np.random.default_rng(2026)
        probe = np.random.default_rng(99)
        before_accuracy, before_probability = task_accuracy(model, dtype, table_sizes, probe)
        before_digest = model.scorer.params.content_digest()

        losses = []
        for _ in range(60):
            episodes = synthetic_batch(rng, dtype, table_sizes, size=8)
            samples = collect_samples(trainer, episodes, rng)
            stats = trainer.update(samples)
            losses.append(stats.loss)

        after_accuracy, after_probability = task_accuracy(model, dtype, table_sizes, probe)
        after_digest = model.scorer.params.content_digest()
        checks = [
            (after_digest != before_digest, "参数必须真的变了（权重摘要不同）"),
            (after_accuracy >= 0.9, f"正确率必须 ≥0.9，实际 {after_accuracy}"),
            (after_probability > before_probability + 0.2,
             f"正确候选的平均概率必须显著上升：{before_probability} → {after_probability}"),
            (losses[-1] < losses[0], f"损失应下降：{losses[0]} → {losses[-1]}"),
            (all(np.isfinite(value) for value in losses), "损失必须始终有限"),
        ]
        failures = [message for ok, message in checks if not ok]
        return (not failures,
                f"accuracy {before_accuracy:.2f}→{after_accuracy:.2f}, "
                f"p(correct) {before_probability:.3f}→{after_probability:.3f}, "
                f"loss {losses[0]:.4f}→{losses[-1]:.4f}; failures={failures}")

    def gradient_clipping_is_bounded() -> tuple:
        dtype = np.float64
        table_sizes = context.table_sizes()
        model = context.model()
        config = TrainingConfig(learning_rate=0.05, max_grad_norm=0.25)
        trainer = Trainer(model, config)
        rng = np.random.default_rng(7)
        episodes = synthetic_batch(rng, dtype, table_sizes, size=8)
        samples = collect_samples(trainer, episodes, rng)
        stats = trainer.update(samples)
        return (stats.grad_scale <= 1.0 and np.isfinite(stats.grad_norm),
                f"grad_norm={stats.grad_norm:.4f} scale={stats.grad_scale:.4f}")

    def clipping_zeroes_out_of_range_policy_gradient() -> tuple:
        """截断必须**真的把区间外样本的策略梯度置零**（这正是 PPO 限制步幅的机制）。

        做法是构造一个"比率已知"的样本：把 ``old_log_prob`` 设为 ``log π(a) − log 1.5``，
        于是当前比率恰为 1.5。``clip_ratio=0.05`` 时它在区间外 ⇒ 代理目标在被截断区域是平的 ⇒ 策略梯度为 0；
        ``clip_ratio=1.0`` 时它落在区间内 ⇒ 梯度非零。两次对照直接把"截断生效"证成可判别的事实。
        """
        dtype = np.float64
        table_sizes = context.table_sizes()
        model_input, _ = synthetic_input(dtype, table_sizes, (0.2, 0.8))

        def run(clip: float):
            model = context.model()
            config = TrainingConfig(learning_rate=0.05, clip_ratio=clip, value_coef=0.0,
                                    entropy_coef=0.0, epochs=1)
            trainer = Trainer(model, config)
            probabilities = action_probabilities(model, model_input)
            index = 0
            old_log_prob = float(np.log(probabilities[index])) - float(np.log(1.5))
            sample = PolicySample(model_input=model_input, action_index=index,
                                  old_log_prob=old_log_prob, advantage=1.0, return_value=1.0)
            stats = trainer.update([sample])
            return stats

        clipped = run(0.05)
        inside = run(1.0)
        return (clipped.grad_norm == 0.0 and clipped.clip_fraction == 1.0 and inside.grad_norm > 0.0,
                f"区间外 grad_norm={clipped.grad_norm:.3g} clip_fraction={clipped.clip_fraction}; "
                f"区间内 grad_norm={inside.grad_norm:.3g}")

    def same_seed_reproduces_training() -> tuple:
        dtype = np.float64
        table_sizes = context.table_sizes()
        digests = []
        for _ in range(2):
            model = context.model()
            trainer = Trainer(model, TrainingConfig(learning_rate=0.05, epochs=1, seed=42))
            rng = np.random.default_rng(1234)
            for _ in range(10):
                episodes = synthetic_batch(rng, dtype, table_sizes, size=4)
                samples = collect_samples(trainer, episodes, rng)
                trainer.update(samples)
            digests.append(model.scorer.params.content_digest())
        return (digests[0] == digests[1], f"{digests[0]} vs {digests[1]}")

    def training_config_is_validated() -> tuple:
        bad = (
            ({"learning_rate": 0.0}, "learning_rate"),
            ({"learning_rate": -1.0}, "learning_rate"),
            ({"clip_ratio": 1.5}, "clip_ratio"),
            ({"max_grad_norm": 0.0}, "max_grad_norm"),
            ({"epochs": 0}, "epochs"),
            ({"adam_beta1": 1.0}, "adam_beta1"),
        )
        for overrides, marker in bad:
            try:
                TrainingConfig(**overrides)
            except LearningError as failure:
                if marker not in str(failure):
                    return False, f"{overrides} 的错误信息未点名 {marker}: {failure}"
            else:
                return False, f"{overrides} 必须被拒绝"
        document = TrainingConfig().to_wire()
        if TrainingConfig.from_wire(document).digest() != TrainingConfig().digest():
            return False, "TrainingConfig 往返摘要必须一致"
        document["training_config_version"] = "training-config@999"
        try:
            TrainingConfig.from_wire(document)
        except LearningError:
            return True, ""
        return False, "训练配置版本不一致必须被拒绝"

    def sample_validation_rejects_bad_rows() -> tuple:
        dtype = np.float64
        table_sizes = context.table_sizes()
        model_input, correct = synthetic_input(dtype, table_sizes, (0.2, 0.8))
        problems = []
        for index, marker in ((2, "越界"), (-1, "越界")):
            try:
                PolicySample(model_input=model_input, action_index=index, old_log_prob=0.0,
                             advantage=0.0, return_value=0.0)
            except LearningError as failure:
                if marker not in str(failure):
                    problems.append(f"{index}: {failure}")
            else:
                problems.append(f"{index} 必须被拒绝")
        return (not problems, "; ".join(problems))

    return [
        _guard(SUITE_UPDATE, "update/synthetic-task-learns", synthetic_task_learns),
        _guard(SUITE_UPDATE, "update/grad-clipping-bounded", gradient_clipping_is_bounded),
        _guard(SUITE_UPDATE, "update/clipping-zeroes-out-of-range-gradient",
               clipping_zeroes_out_of_range_policy_gradient),
        _guard(SUITE_UPDATE, "update/same-seed-reproduces-training", same_seed_reproduces_training),
        _guard(SUITE_UPDATE, "update/training-config-validated", training_config_is_validated),
        _guard(SUITE_UPDATE, "update/sample-validation", sample_validation_rejects_bad_rows),
    ]


# --------------------------------------------------------------------------- #
# learning-restore
# --------------------------------------------------------------------------- #
def _restore_cases(context: _Context) -> list:
    def optimizer_state_roundtrip_is_exact() -> tuple:
        dtype = np.float64
        table_sizes = context.table_sizes()
        model = context.model()
        config = TrainingConfig(learning_rate=0.05)
        trainer = Trainer(model, config)
        rng = np.random.default_rng(21)
        for _ in range(3):
            episodes = synthetic_batch(rng, dtype, table_sizes, size=4)
            trainer.update(collect_samples(trainer, episodes, rng))
        state = trainer.optimizer_state()
        digest = trainer.optimizer.digest()
        steps = trainer.optimizer.steps

        restored_model = context.model()
        restored = Trainer(restored_model, config)
        restored.load_optimizer_state(state, steps=steps)
        return (restored.optimizer.digest() == digest and restored.optimizer.steps == steps,
                f"{digest} vs {restored.optimizer.digest()}")

    def checkpoint_restores_training_exactly() -> tuple:
        """训练 N 步 → 存权重 + 优化器 → 恢复 → 再训 M 步，必须与不中断训练 N+M 步**逐字节一致**。

        样本数据的随机数**按步派生**（`base_seed + step`），因此两条路径每一步的采样序列相同；
        行为策略的新旧概率都取自各自当前的参数（恢复后参数一致 ⇒ 概率一致）。
        """
        dtype = np.float64
        table_sizes = context.table_sizes()
        config = TrainingConfig(learning_rate=0.05, epochs=1)
        base_seed = 20261003

        def episodes_for(step: int) -> list:
            rng = np.random.default_rng(base_seed + step)
            return synthetic_batch(rng, dtype, table_sizes, size=4)

        def sampler_for(step: int) -> np.random.Generator:
            return np.random.default_rng(base_seed + 10_000 + step)

        def run(trainer: Trainer, steps: list) -> None:
            for step in steps:
                samples = collect_samples(trainer, episodes_for(step), sampler_for(step))
                trainer.update(samples)

        uninterrupted = Trainer(context.model(), config)
        run(uninterrupted, [0, 1, 2, 3, 4, 5, 6, 7])

        interrupted = Trainer(context.model(), config)
        run(interrupted, [0, 1, 2, 3, 4])
        weights = interrupted.model.scorer.params.state()
        optimizer_state = interrupted.optimizer_state()
        optimizer_steps = interrupted.optimizer.steps

        resumed_model = context.model()
        resumed_model.scorer.params.load_state(weights)
        resumed = Trainer(resumed_model, config)
        resumed.load_optimizer_state(optimizer_state, steps=optimizer_steps)
        run(resumed, [5, 6, 7])

        left = uninterrupted.model.scorer.params.content_digest()
        right = resumed.model.scorer.params.content_digest()
        return (right == left,
                f"resumed={right} uninterrupted={left} "
                f"（恢复后模型摘要 {resumed_model.scorer.params.content_digest() == left}）")

    def bundle_checkpoint_resumes_identically() -> tuple:
        """走**落盘**的检查点：训练 5 步 → `save_bundle(带优化器状态)` → 重新载入 → 再训 3 步，
        必须与"不中断地训 8 步"逐字节一致（同一份按步派生的样本数据）。"""
        dtype = np.float64
        table_sizes = context.table_sizes()
        config = TrainingConfig(learning_rate=0.05, epochs=1)
        base_seed = 4242
        root = pathlib.Path(tempfile.mkdtemp(prefix="model-checkpoint-"))

        def episodes_for(step: int) -> list:
            return synthetic_batch(np.random.default_rng(base_seed + step), dtype, table_sizes, size=4)

        def sampler_for(step: int) -> np.random.Generator:
            return np.random.default_rng(base_seed + 20_000 + step)

        def run(trainer: Trainer, steps: list) -> None:
            for step in steps:
                trainer.update(collect_samples(trainer, episodes_for(step), sampler_for(step)))

        try:
            uninterrupted = Trainer(context.model(), config)
            run(uninterrupted, list(range(8)))

            partial = Trainer(context.model(), config)
            run(partial, list(range(5)))
            directory = save_bundle(
                partial.model, root / "checkpoint",
                training_state={"steps": partial.optimizer.steps,
                                "training_config": config.to_wire(),
                                "training_config_digest": config.digest()},
                optimizer_state=partial.optimizer_state(),
            )

            restored_model = load_bundle(directory, manifest=context.manifest(), load_optimizer=True)
            resumed = Trainer(restored_model, config)
            resumed.load_optimizer_state(restored_model.optimizer_state,
                                         steps=int(restored_model.training_state["steps"]))
            run(resumed, [5, 6, 7])

            left = uninterrupted.model.scorer.params.content_digest()
            right = resumed.model.scorer.params.content_digest()
            metadata = json.loads((pathlib.Path(directory) / "metadata.json").read_text(encoding="utf-8"))
            return (right == left
                    and metadata["training"].startswith("ppo(")
                    and metadata["optimizer_file"] == "optimizer.npz",
                    f"resumed={right[:24]} uninterrupted={left[:24]} training={metadata['training']}")
        finally:
            shutil.rmtree(root, ignore_errors=True)

    def untrained_bundle_has_no_checkpoint() -> tuple:
        dtype = np.float64
        root = pathlib.Path(tempfile.mkdtemp(prefix="model-checkpoint-"))
        try:
            directory = save_bundle(context.model(), root / "untrained")
            try:
                load_bundle(directory, manifest=context.manifest(), load_optimizer=True)
            except ModelBundleError as failure:
                return (failure.code == "checkpoint_missing",
                        f"{failure}")
            return False, "未训练的模型包不得声称可恢复训练"
        finally:
            shutil.rmtree(root, ignore_errors=True)

    return [
        _guard(SUITE_RESTORE, "restore/optimizer-state-roundtrip", optimizer_state_roundtrip_is_exact),
        _guard(SUITE_RESTORE, "restore/checkpoint-continues-identically",
               checkpoint_restores_training_exactly),
        _guard(SUITE_RESTORE, "restore/bundle-checkpoint-resumes-identically",
               bundle_checkpoint_resumes_identically),
        _guard(SUITE_RESTORE, "restore/untrained-bundle-has-no-checkpoint",
               untrained_bundle_has_no_checkpoint),
    ]


def run_all(only: Optional[str] = None) -> list:
    outcomes: list = []
    if only is not None and only not in SUITE_NAMES:
        return outcomes
    context = _Context()
    if only in (None, SUITE_GRAD):
        outcomes.extend(_grad_cases(context))
    if only in (None, SUITE_UPDATE):
        outcomes.extend(_update_cases(context))
    if only in (None, SUITE_RESTORE):
        outcomes.extend(_restore_cases(context))
    return outcomes


__all__ = [
    "SUITE_GRAD",
    "SUITE_NAMES",
    "SUITE_RESTORE",
    "SUITE_UPDATE",
    "collect_samples",
    "run_all",
    "synthetic_batch",
    "synthetic_input",
    "task_accuracy",
]
