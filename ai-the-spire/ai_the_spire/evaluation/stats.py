"""评估用的小统计工具：均值、样本标准差、分位数、**百分位自助法**置信区间。

为什么用自助法：小样本（保留种子数通常只有几个到几十个）下，"正态近似"要额外假设分布形状，
而自助法只需"配对差值可重抽"这一个前提，且**给定重抽次数与种子就可复现**（报告里记录两者）。
本模块只做算术，不解释结果；"是否更好"由报告按计划声明的方向与区间给出，且允许写 undecided。
"""

from __future__ import annotations

import math
import random
from typing import Sequence

#: 自助法结果的默认身份（报告里必须记录这两个数）。
DEFAULT_RESAMPLES = 2000
DEFAULT_SEED = 20261003


class StatsError(ValueError):
    """统计输入非法（空样本、非有限值等）。"""


def _clean(values: Sequence[float], *, what: str) -> list:
    cleaned = []
    for value in values:
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            raise StatsError(f"{what} 必须是数值，得到 {value!r}")
        number = float(value)
        if not math.isfinite(number):
            raise StatsError(f"{what} 必须是有限数，得到 {value!r}")
        cleaned.append(number)
    if not cleaned:
        raise StatsError(f"{what} 不能为空（零样本不算统计结果）")
    return cleaned


def mean(values: Sequence[float]) -> float:
    cleaned = _clean(values, what="mean 的输入")
    return sum(cleaned) / len(cleaned)


def standard_deviation(values: Sequence[float]) -> float:
    """**样本**标准差（n-1 分母）。n=1 时返回 0.0，并由调用方按 n 声明其无意义。"""
    cleaned = _clean(values, what="standard_deviation 的输入")
    if len(cleaned) == 1:
        return 0.0
    average = sum(cleaned) / len(cleaned)
    variance = sum((value - average) ** 2 for value in cleaned) / (len(cleaned) - 1)
    return math.sqrt(variance)


def percentile(values: Sequence[float], fraction: float) -> float:
    """线性插值分位数（与 numpy 的默认 `linear` 口径一致，便于对照）。"""
    cleaned = sorted(_clean(values, what="percentile 的输入"))
    if not 0.0 <= fraction <= 1.0:
        raise StatsError(f"fraction 必须在 [0, 1] 内，得到 {fraction!r}")
    if len(cleaned) == 1:
        return cleaned[0]
    position = fraction * (len(cleaned) - 1)
    lower = int(math.floor(position))
    upper = min(lower + 1, len(cleaned) - 1)
    weight = position - lower
    return cleaned[lower] * (1.0 - weight) + cleaned[upper] * weight


def bootstrap_ci(values: Sequence[float], *, resamples: int = DEFAULT_RESAMPLES,
                 seed: int = DEFAULT_SEED, alpha: float = 0.05) -> tuple[float, float]:
    """百分位自助法置信区间：``[α/2, 1-α/2]`` 分位；给定 (resamples, seed) 可复现。"""
    cleaned = _clean(values, what="bootstrap_ci 的输入")
    if isinstance(resamples, bool) or not isinstance(resamples, int) or resamples <= 0:
        raise StatsError(f"resamples 必须是正整数，得到 {resamples!r}")
    if isinstance(seed, bool) or not isinstance(seed, int) or seed < 0:
        raise StatsError(f"seed 必须是非负整数，得到 {seed!r}")
    if not 0.0 < alpha < 1.0:
        raise StatsError(f"alpha 必须在 (0, 1) 内，得到 {alpha!r}")
    rng = random.Random(seed)
    size = len(cleaned)
    means = []
    for _ in range(resamples):
        total = 0.0
        for _ in range(size):
            total += cleaned[rng.randrange(size)]
        means.append(total / size)
    return percentile(means, alpha / 2.0), percentile(means, 1.0 - alpha / 2.0)
