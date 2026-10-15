"""参数集合：**显式命名 + 确定性初始化 + 可核对摘要**（numpy 实现，不引入训练框架）。

初始化规则（写死在这里，便于离线复现；不声称这是最优初始化）：
按参数名排序后依次从同一个 `numpy.random.default_rng(seed)` 抽样，
矩阵用 ``N(0, 1/sqrt(fan_in))``、embedding 用 ``N(0, 1/sqrt(dim))``、向量用 ``N(0, 1/sqrt(len))``。
"""

from __future__ import annotations

import hashlib
import math
from typing import Iterable, Mapping

import numpy as np

#: 参数名 → 组名（摘要与分项报告用）由名字的 ``组/名`` 前缀决定。
def group_of(name: str) -> str:
    return name.split("/", 1)[0]


class ParameterSet:
    """有序参数集合（名字 → ndarray），带确定性初始化与内容摘要。"""

    def __init__(self, dtype=np.float64) -> None:
        self._dtype = dtype
        self._arrays: dict[str, np.ndarray] = {}

    # -- 构建 ------------------------------------------------------------ #
    def add(self, name: str, shape: Iterable[int], *, fan_in: int | None = None) -> np.ndarray:
        if name in self._arrays:
            raise ValueError(f"参数名重复：{name}")
        shape = tuple(int(dim) for dim in shape)
        if any(dim <= 0 for dim in shape):
            raise ValueError(f"{name} 的维度必须为正，得到 {shape}")
        fan = fan_in if fan_in is not None else (shape[0] if len(shape) > 1 else shape[0])
        scale = 1.0 / math.sqrt(max(fan, 1))
        self._arrays[name] = np.zeros(shape, dtype=self._dtype)
        self._scale = getattr(self, "_scale", {})
        self._scale[name] = scale
        return self._arrays[name]

    def randomize(self, seed: int) -> None:
        """按**参数名排序**依次抽样：同一 seed + 同一结构 ⇒ 同一初始化。"""
        rng = np.random.default_rng(seed)
        for name in sorted(self._arrays):
            array = self._arrays[name]
            array[...] = rng.normal(0.0, self._scale[name], size=array.shape).astype(self._dtype)

    # -- 读取 ------------------------------------------------------------ #
    def __getitem__(self, name: str) -> np.ndarray:
        try:
            return self._arrays[name]
        except KeyError as exc:  # pragma: no cover - 拼写错误的显式失败
            raise KeyError(f"没有参数 {name!r}；可用 {sorted(self._arrays)}") from exc

    def __contains__(self, name: object) -> bool:
        return name in self._arrays

    def names(self) -> tuple[str, ...]:
        return tuple(sorted(self._arrays))

    def items(self) -> tuple[tuple[str, np.ndarray], ...]:
        return tuple((name, self._arrays[name]) for name in self.names())

    def total_size(self) -> int:
        return int(sum(array.size for array in self._arrays.values()))

    # -- 报告与序列化 ---------------------------------------------------- #
    def breakdown(self) -> dict[str, int]:
        """按组统计参数量（embedding/entity/field/relation/candidate/value 等）。"""
        totals: dict[str, int] = {}
        for name, array in self._arrays.items():
            totals[group_of(name)] = totals.get(group_of(name), 0) + int(array.size)
        return {group: totals[group] for group in sorted(totals)}

    def shape_report(self) -> tuple[dict, ...]:
        return tuple(
            {"name": name, "shape": tuple(int(dim) for dim in array.shape), "size": int(array.size)}
            for name, array in self.items()
        )

    def content_digest(self) -> str:
        """权重内容摘要：按参数名排序，把 dtype/shape/原始字节一起哈希（与容器格式无关）。"""
        digest = hashlib.sha256()
        for name, array in self.items():
            canonical = np.ascontiguousarray(array)
            digest.update(name.encode("utf-8"))
            digest.update(str(canonical.dtype).encode("utf-8"))
            digest.update(repr(tuple(int(dim) for dim in canonical.shape)).encode("utf-8"))
            digest.update(canonical.tobytes())
        return "sha256:" + digest.hexdigest()

    def state(self) -> dict[str, np.ndarray]:
        """所有权重（按名排序）——落盘用；调用方不得原地修改。"""
        return {name: np.array(array, copy=True) for name, array in self.items()}

    def load_state(self, state: Mapping[str, np.ndarray]) -> None:
        """载入权重：**形状与名字必须逐项一致**，否则显式失败（不广播、不截断）。"""
        expected = self.names()
        got = tuple(sorted(state))
        if got != expected:
            missing = sorted(set(expected) - set(got))
            extra = sorted(set(got) - set(expected))
            raise ValueError(f"权重键不匹配：缺少 {missing}，多出 {extra}")
        for name in expected:
            array = np.asarray(state[name])
            if array.shape != self._arrays[name].shape:
                raise ValueError(
                    f"权重 {name} 形状 {array.shape} 与结构 {self._arrays[name].shape} 不一致"
                )
            if array.dtype != self._dtype:
                raise ValueError(f"权重 {name} dtype {array.dtype} 与结构 {self._dtype} 不一致")
            self._arrays[name][...] = array
