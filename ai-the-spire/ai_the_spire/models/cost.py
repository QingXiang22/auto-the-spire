"""成本报告：参数量分项、权重文件大小、**CPU 决策延迟**与分阶段耗时。

依据 `encoder-design.md` §"参数"：任何"轻量"结论都要分别报告总参数量（含字典 embedding）、
权重文件大小、CPU 决策延迟、峰值内存和下游效果；本轮**只报告**结构与推理成本，
不下"够轻"的结论，也不把结构化编码成本当成神经网络推理成本。
"""

from __future__ import annotations

import statistics
import time
import tracemalloc
from typing import Any, Sequence

from ..encoding.tensor import TensorizedObservation, tensorize
from .policy import SmallPolicyModel, semantic_keys_of


def parameter_breakdown(model: SmallPolicyModel) -> dict[str, int]:
    """按组统计参数量（embedding 与各共享层分开）。"""
    return model.scorer.params.breakdown()


def parameter_report(model: SmallPolicyModel) -> dict[str, Any]:
    """参数分项 + 总参数量 + 权重字节数（float64 ⇒ 8 字节/参数）。"""
    breakdown = parameter_breakdown(model)
    total = model.scorer.params.total_size()
    dtype = model.config.dtype
    bytes_per = 8 if dtype == "float64" else 4
    return {
        "model_id": model.model_id,
        "dtype": dtype,
        "parameters": breakdown,
        "parameter_total": total,
        "weights_bytes": total * bytes_per,
        "config_digest": model.config.digest(),
        "weights_digest": model.weights_digest,
    }


def _percentile(values: Sequence[float], fraction: float) -> float:
    if not values:
        raise ValueError("没有样本就没有分位数")
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, int(round(fraction * (len(ordered) - 1)))))
    return ordered[index]


def measure_inference_cost(model: SmallPolicyModel, observations: Sequence[TensorizedObservation],
                           *, snapshots: Sequence[Any] = (), repeats: int = 3) -> dict[str, Any]:
    """分阶段测量：`tensorize`（结构化编码）与模型前向**分开报告**。

    `observations` 用于模型前向计时；`snapshots` 给出时额外测 `tensorize`（端到端决策成本的上界）。
    只报告 p50/p90/max 与峰值内存，不给出达标结论。
    """
    if repeats <= 0:
        raise ValueError("repeats 必须是正数")
    if not observations:
        raise ValueError("必须给出至少一份张量化观察")

    forward_ms: list[float] = []
    for _ in range(repeats):
        for observation in observations:
            start = time.perf_counter()
            model.evaluate_tensorized(
                observation, semantic_keys=_placeholder_keys(observation)
            )
            forward_ms.append((time.perf_counter() - start) * 1000.0)

    encode_ms: list[float] = []
    if snapshots:
        for _ in range(repeats):
            for snapshot in snapshots:
                start = time.perf_counter()
                tensorize(snapshot, model.manifest, catalog=model.catalog)
                encode_ms.append((time.perf_counter() - start) * 1000.0)

    tracemalloc.start()
    model.evaluate_tensorized(observations[0], semantic_keys=_placeholder_keys(observations[0]))
    _, peak = tracemalloc.get_traced_memory()
    tracemalloc.stop()

    def summary(values: Sequence[float]) -> dict[str, float] | None:
        if not values:
            return None
        return {
            "n": len(values),
            "p50_ms": statistics.median(values),
            "p90_ms": _percentile(values, 0.9),
            "max_ms": max(values),
        }

    return {
        "forward": summary(forward_ms),
        "tensorize": summary(encode_ms),
        "peak_bytes_first_call": peak,
        "observations": len(observations),
        "repeats": repeats,
    }


def _placeholder_keys(observation: TensorizedObservation) -> dict[str, str]:
    """计时用占位语义键：键名只用于对齐，不参与任何计算。

    计时**不**使用真实语义键（那需要 wire 动作）；概率数值与语义键无关，
    因此这里不改变被测的计算路径。
    """
    return {
        candidate_id: f"timing:{candidate_id}"
        for candidate_id, _ in observation.reference_map.candidate_rows.items()
    }


def _unused(*args) -> None:  # pragma: no cover - 保留给调用方参考
    _ = (args, semantic_keys_of)
