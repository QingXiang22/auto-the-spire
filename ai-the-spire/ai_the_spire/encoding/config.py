"""显式 ``EncodingConfig``：数值变换、精度容差与容量上限（encoder-design §2/§5）。

边界（开工清单 §7.2）：

* 本模块属于 ``ai_the_spire.encoding``，**不进入** ``ai_the_spire.contracts``
  （该包只用标准库、不导入活动 client/core，且只做协议）；
* 所有数值变换 ``encoded = (raw - offset) / scale`` 都是显式声明：
  offset/scale 以精确十进制字符串保存，未声明即 offset=0、scale=1，
  **不静默裁剪**、不用评估数据拟合变换、不先 ``float()`` 再声称保留源精度；
* 容量上限在此**声明**并由 Manifest 持久化；真正的超限拒绝在 A-03/A-04 的
  校验/张量化中执行（本批只保证配置本身合法且可往返）。
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field
from decimal import Decimal
from typing import Any, Mapping, Optional

from ai_the_spire.contracts import layers

from .errors import EncodingError

#: 编码配置自身的版本；配置结构变化时必须改它（旧配置仍按旧结构加载）。
CONFIG_VERSION = "ai-encoding-config-v1"

#: 首阶段数值通道精度（encoder-design §5：建议 float64，形状可检查）。
NUMBER_CHANNEL_DTYPES = ("float64",)

#: 容差默认值：**配置默认**，不是已批准的数值阈值（policy-contract §2 要求
#: 由精度实验确定）。它们随 Manifest 一起持久化，A-04 不再另行猜测。
DEFAULT_ABSOLUTE_TOLERANCE = "1e-12"
DEFAULT_RELATIVE_TOLERANCE = "1e-12"

CAPACITY_KEYS = (
    "max_entities",
    "max_fields",
    "max_relations",
    "max_pile_positions",
    "max_sequences",
    "max_groups",
    "max_candidates",
    "max_candidate_fields",
)

_CONFIG_KEYS = frozenset(
    {
        "config_version",
        "number_channel_dtype",
        "absolute_tolerance",
        "relative_tolerance",
        "transforms",
        "capacities",
    }
)
_TRANSFORM_KEYS = frozenset({"offset", "scale"})


def _normalized_decimal(value: Any, *, path: str, code: str = "invalid_encoding_config") -> str:
    """用共享参考实现规范化十进制（拒绝 float/bool/非有限值/非十进制串）。

    ``code`` 区分两类来源：配置里的 offset/scale/容差写错是
    ``invalid_encoding_config``；快照里的 number 值不是规范十进制则是
    ``value_type_mismatch``（A-03 的 L3 校验会先以共享 ``I-21`` 拒绝，这里是防御层）。
    """
    try:
        return layers.normalize_decimal(value)
    except Exception as exc:  # noqa: BLE001 - 共享 codec 的所有拒绝都归为同一个显式错误
        raise EncodingError(code, path, f"不是合法十进制: {value!r}（{exc}）") from exc


@dataclass(frozen=True)
class NumericTransform:
    """``encoded = (raw - offset) / scale``；两个操作数都是精确十进制。"""

    offset: str = "0"
    scale: str = "1"

    def __post_init__(self) -> None:
        object.__setattr__(self, "offset", _normalized_decimal(self.offset, path="transform.offset"))
        scale = _normalized_decimal(self.scale, path="transform.scale")
        if Decimal(scale) <= 0:
            raise EncodingError(
                "invalid_encoding_config", "transform.scale", f"scale 必须是有限正数: {self.scale!r}"
            )
        object.__setattr__(self, "scale", scale)

    @classmethod
    def from_wire(cls, doc: Any, *, path: str = "transform") -> "NumericTransform":
        if not isinstance(doc, Mapping):
            raise EncodingError("invalid_encoding_config", path, f"变换必须是对象，得到 {type(doc).__name__}")
        keys = set(doc)
        if keys != _TRANSFORM_KEYS:
            raise EncodingError(
                "invalid_encoding_config",
                path,
                f"变换键必须恰为 {sorted(_TRANSFORM_KEYS)}，得到 {sorted(keys)}",
            )
        if not isinstance(doc["offset"], str) or not isinstance(doc["scale"], str):
            raise EncodingError("invalid_encoding_config", path, "offset/scale 必须是十进制字符串")
        return cls(offset=doc["offset"], scale=doc["scale"])

    def to_wire(self) -> dict:
        return {"offset": self.offset, "scale": self.scale}

    def apply_exact(self, raw_text: Any, *, path: str = "value") -> Decimal:
        """精确十进制结果（不做浮点转换）。"""
        raw = Decimal(_normalized_decimal(raw_text, path=path, code="value_type_mismatch"))
        value = (raw - Decimal(self.offset)) / Decimal(self.scale)
        if not value.is_finite():
            raise EncodingError("invalid_encoding_config", path, f"变换结果非有限: {value}")
        return value


@dataclass(frozen=True)
class CapacityLimits:
    """资源上限；``None`` 表示未设上限（不静默截断，超限由 A-03/A-04 显式报错）。"""

    max_entities: Optional[int] = None
    max_fields: Optional[int] = None
    max_relations: Optional[int] = None
    max_pile_positions: Optional[int] = None
    max_sequences: Optional[int] = None
    max_groups: Optional[int] = None
    max_candidates: Optional[int] = None
    max_candidate_fields: Optional[int] = None

    def __post_init__(self) -> None:
        for key in CAPACITY_KEYS:
            value = getattr(self, key)
            if value is None:
                continue
            if isinstance(value, bool) or not isinstance(value, int):
                raise EncodingError(
                    "invalid_encoding_config", f"capacities.{key}", f"上限必须是整数或 null，得到 {value!r}"
                )
            if value <= 0:
                raise EncodingError(
                    "invalid_encoding_config", f"capacities.{key}", f"上限必须为正整数，得到 {value}"
                )

    @classmethod
    def from_wire(cls, doc: Any, *, path: str = "capacities") -> "CapacityLimits":
        if not isinstance(doc, Mapping):
            raise EncodingError("invalid_encoding_config", path, f"容量必须是对象，得到 {type(doc).__name__}")
        keys = set(doc)
        if keys != set(CAPACITY_KEYS):
            raise EncodingError(
                "invalid_encoding_config",
                path,
                f"容量键必须恰为 {sorted(CAPACITY_KEYS)}，得到 {sorted(keys)}",
            )
        return cls(**{key: doc[key] for key in CAPACITY_KEYS})

    def to_wire(self) -> dict:
        return {key: getattr(self, key) for key in CAPACITY_KEYS}

    @property
    def configured_keys(self) -> tuple[str, ...]:
        return tuple(key for key in CAPACITY_KEYS if getattr(self, key) is not None)


@dataclass(frozen=True)
class EncodingConfig:
    """编码配置：变换表 + 容差 + 容量 + 数值通道精度（全部随 Manifest 持久化）。"""

    transforms: Mapping[str, NumericTransform] = field(default_factory=dict)
    capacities: CapacityLimits = field(default_factory=CapacityLimits)
    absolute_tolerance: str = DEFAULT_ABSOLUTE_TOLERANCE
    relative_tolerance: str = DEFAULT_RELATIVE_TOLERANCE
    number_channel_dtype: str = "float64"

    def __post_init__(self) -> None:
        transforms: dict[str, NumericTransform] = {}
        for field_id, transform in dict(self.transforms).items():
            if not isinstance(field_id, str) or not field_id:
                raise EncodingError(
                    "invalid_encoding_config", "transforms", f"变换键必须是非空字段 ID，得到 {field_id!r}"
                )
            if isinstance(transform, NumericTransform):
                transforms[field_id] = transform
            else:
                transforms[field_id] = NumericTransform.from_wire(
                    transform, path=f"transforms[{field_id}]"
                )
        object.__setattr__(self, "transforms", transforms)

        if not isinstance(self.capacities, CapacityLimits):
            object.__setattr__(
                self,
                "capacities",
                CapacityLimits.from_wire(self.capacities, path="capacities"),
            )

        for key in ("absolute_tolerance", "relative_tolerance"):
            normalized = _normalized_decimal(getattr(self, key), path=key)
            if Decimal(normalized) < 0:
                raise EncodingError("invalid_encoding_config", key, f"容差不能为负: {normalized!r}")
            object.__setattr__(self, key, normalized)

        if self.number_channel_dtype not in NUMBER_CHANNEL_DTYPES:
            raise EncodingError(
                "invalid_encoding_config",
                "number_channel_dtype",
                f"首阶段只支持 {list(NUMBER_CHANNEL_DTYPES)}，得到 {self.number_channel_dtype!r}",
            )

    # -- 序列化 ---------------------------------------------------------- #
    @classmethod
    def from_wire(cls, doc: Any, *, path: str = "encoding_config") -> "EncodingConfig":
        if not isinstance(doc, Mapping):
            raise EncodingError("invalid_encoding_config", path, f"配置必须是对象，得到 {type(doc).__name__}")
        keys = set(doc)
        if keys != _CONFIG_KEYS:
            raise EncodingError(
                "invalid_encoding_config",
                path,
                f"配置键必须恰为 {sorted(_CONFIG_KEYS)}，得到 {sorted(keys)}",
            )
        if doc["config_version"] != CONFIG_VERSION:
            raise EncodingError(
                "version_mismatch",
                f"{path}.config_version",
                f"期望 {CONFIG_VERSION!r}，得到 {doc['config_version']!r}",
            )
        raw_transforms = doc["transforms"]
        if not isinstance(raw_transforms, Mapping):
            raise EncodingError("invalid_encoding_config", f"{path}.transforms", "变换表必须是对象")
        return cls(
            transforms={
                field_id: NumericTransform.from_wire(value, path=f"{path}.transforms[{field_id}]")
                for field_id, value in raw_transforms.items()
            },
            capacities=CapacityLimits.from_wire(doc["capacities"], path=f"{path}.capacities"),
            absolute_tolerance=doc["absolute_tolerance"],
            relative_tolerance=doc["relative_tolerance"],
            number_channel_dtype=doc["number_channel_dtype"],
        )

    def to_wire(self) -> dict:
        return {
            "config_version": CONFIG_VERSION,
            "number_channel_dtype": self.number_channel_dtype,
            "absolute_tolerance": self.absolute_tolerance,
            "relative_tolerance": self.relative_tolerance,
            "transforms": {
                field_id: self.transforms[field_id].to_wire() for field_id in sorted(self.transforms)
            },
            "capacities": self.capacities.to_wire(),
        }

    def digest(self) -> str:
        """配置身份（与字典身份分开：同内容不同数值配置不是同一推理配方）。"""
        return layers.digest_bytes(layers.canonical_bytes(self.to_wire()))

    # -- 变换 ------------------------------------------------------------ #
    def transform_for(self, field_id: str) -> NumericTransform:
        """未声明的 number 字段按 offset=0、scale=1（恒等），不做任何猜测。"""
        return self.transforms.get(field_id, NumericTransform())

    def encode_number(self, field_id: str, raw_text: Any) -> float:
        """精确解析源十进制 → 固定变换 → float64，并按配置容差检查转换误差。"""
        path = f"fields[{field_id}].value"
        exact = self.transform_for(field_id).apply_exact(raw_text, path=path)
        converted = float(exact)
        if not math.isfinite(converted):
            raise EncodingError(
                "non_finite_value", path, f"变换结果无法表示为有限 float64: {exact}", related_ids=[field_id]
            )
        error = abs(Decimal(converted) - exact)
        if error != 0:
            absolute = Decimal(self.absolute_tolerance)
            relative = Decimal(self.relative_tolerance)
            within = error <= absolute or (exact != 0 and error <= relative * abs(exact))
            if not within:
                raise EncodingError(
                    "numeric_precision_loss",
                    path,
                    f"float64 转换误差 {error} 超出配置容差 "
                    f"(absolute={self.absolute_tolerance}, relative={self.relative_tolerance})",
                    related_ids=[field_id],
                )
        return converted


__all__ = [
    "CAPACITY_KEYS",
    "CONFIG_VERSION",
    "CapacityLimits",
    "DEFAULT_ABSOLUTE_TOLERANCE",
    "DEFAULT_RELATIVE_TOLERANCE",
    "EncodingConfig",
    "NUMBER_CHANNEL_DTYPES",
    "NumericTransform",
]
