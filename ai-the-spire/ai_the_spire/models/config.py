"""模型结构配置：**显式维度**、可复现初始化、可核对摘要（W-04）。

理由（`design-v0.md` §"模型方案"）：维度 d、传播轮数 L 与数值精度由 `ModelConfig` 提供，
不把历史讨论里的"8GB/5 秒"当成预算；`encoder-design.md` §"参数"要求报告参数量而不是断言"轻量"。
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, fields
from typing import Any, Mapping

from ..contracts import layers

#: 结构配置格式版本：字段/语义变更必须换版本（模型包按它绑定）。
MODEL_CONFIG_VERSION = "model-config@1"

#: 允许的浮点精度名（不引入 binary64 以外的隐式转换；float32 只用于显式声明的机器）。
_DTYPES = ("float64", "float32")


class ModelConfigError(ValueError):
    """结构配置非法：一律带字段路径与原因，不静默取默认值。"""


def _canonical_decimal(value: float) -> str:
    """浮点标量 → 规范十进制字符串（可往返；共享 L0 禁止 wire 里的浮点字面量）。"""
    text = repr(float(value))
    if text.endswith(".0"):
        return text
    return text


@dataclass(frozen=True)
class ModelConfig:
    """共享小模型的结构与维度（全部显式；不读环境变量、不按机器自适应）。"""

    #: 字典 embedding 维度（每张表一个，互不强制相等）
    d_entity_kind: int = 8
    d_content_type: int = 8
    d_content_state: int = 4
    d_field: int = 8
    d_value_type: int = 4
    d_category: int = 4
    d_relation_type: int = 8
    d_action_type: int = 8
    #: 中间层维度
    d_entity: int = 16
    d_field_hidden: int = 16
    d_candidate: int = 16
    d_global: int = 32
    d_value_hidden: int = 16
    #: 关系传播轮数（0 = 不做传播；仍保留关系行的偏置项）
    propagation_rounds: int = 1
    #: 可复现初始化种子与精度
    seed: int = 20261003
    dtype: str = "float64"
    #: 数值通道缩放：number/boolean 通道各自乘一个显式系数（1.0 = 原样）
    number_scale: float = 1.0
    boolean_scale: float = 1.0

    def __post_init__(self) -> None:
        for field in fields(self):
            value = getattr(self, field.name)
            if field.name == "dtype":
                if value not in _DTYPES:
                    raise ModelConfigError(f"dtype 必须是 {_DTYPES} 之一，得到 {value!r}")
                continue
            if field.name == "seed":
                if isinstance(value, bool) or not isinstance(value, int) or value < 0:
                    raise ModelConfigError(f"seed 必须是非负整数，得到 {value!r}")
                continue
            if field.name in ("number_scale", "boolean_scale"):
                if not isinstance(value, (int, float)) or isinstance(value, bool):
                    raise ModelConfigError(f"{field.name} 必须是有限数值，得到 {value!r}")
                if value != value or value in (float("inf"), float("-inf")):
                    raise ModelConfigError(f"{field.name} 必须有限，得到 {value!r}")
                continue
            if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
                raise ModelConfigError(f"{field.name} 必须是正整数，得到 {value!r}")
        # 传播轮数可以显式为 0（关闭），但不能为负、也不能大到无法界定的规模。
        if isinstance(self.propagation_rounds, bool) or not isinstance(self.propagation_rounds, int):
            raise ModelConfigError(f"propagation_rounds 必须是整数，得到 {self.propagation_rounds!r}")
        if not 0 <= self.propagation_rounds <= 8:
            raise ModelConfigError(
                f"propagation_rounds 必须在 0..8（显式上界，避免无界堆叠），得到 {self.propagation_rounds}"
            )

    # -- 序列化 ---------------------------------------------------------- #
    def to_wire(self) -> dict:
        """规范 wire：**数值标量写成规范十进制字符串**（共享 L0 不允许 wire 出现浮点字面量）。"""
        document = {"model_config_version": MODEL_CONFIG_VERSION}
        for field in fields(self):
            value = getattr(self, field.name)
            document[field.name] = (
                _canonical_decimal(value) if field.name in ("number_scale", "boolean_scale") else value
            )
        return document

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "ModelConfig":
        if not isinstance(doc, Mapping):
            raise ModelConfigError(f"ModelConfig 必须是对象，得到 {type(doc).__name__}")
        expected = {"model_config_version"} | {field.name for field in fields(cls)}
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise ModelConfigError(f"ModelConfig 键不匹配：缺少 {missing}，多出 {extra}")
        version = doc["model_config_version"]
        if version != MODEL_CONFIG_VERSION:
            raise ModelConfigError(
                f"ModelConfig 版本 {version!r} 与当前 {MODEL_CONFIG_VERSION!r} 不同（不静默迁移）"
            )
        values = {field.name: doc[field.name] for field in fields(cls)}
        for name in ("number_scale", "boolean_scale"):
            raw = values[name]
            if isinstance(raw, bool) or not isinstance(raw, (int, float, str)):
                raise ModelConfigError(f"{name} 必须是数值或规范十进制字符串，得到 {raw!r}")
            try:
                values[name] = float(raw)
            except (TypeError, ValueError) as failure:
                raise ModelConfigError(f"{name} 不是可解析的数值：{raw!r}") from failure
        return cls(**values)

    def digest(self) -> str:
        """结构摘要：规范 JSON 的 sha256（模型包与轨迹用它核对结构一致）。"""
        payload = layers.canonical_bytes(self.to_wire())
        return "sha256:" + hashlib.sha256(payload).hexdigest()

    def float_dtype(self):
        import numpy as np

        return np.float64 if self.dtype == "float64" else np.float32
