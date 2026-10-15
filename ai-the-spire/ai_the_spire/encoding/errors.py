"""编码层最小错误结果（encoder-design §5.2）。

错误对象形状固定为 ``{code, path, related_ids, message}``；失败不返回可供评分的
部分编码（调用方只拿到异常）。本模块属于 ``ai_the_spire.encoding``，**不进入**
``ai_the_spire.contracts``：contracts 是纯协议包，只携带共享 ``I-xx`` 结论。

编码阶段错误码分两类：

* ``MINIMUM_CODES``：encoder-design §5.2 的"至少包括"清单。本批（A-02）
  只实际抛出其中的一部分，其余由 A-03/A-04 的校验/张量化使用；
* ``ADDED_CODES``：A-02 为"Manifest 文件本身"新增的两个码。它们描述的是
  AI 侧自有文件格式（不是 wire 协议对象），共享规范没有对应规则 ID，
  因此必须显式新增并登记，不能塞进共享词汇冒充协议错误。
"""

from __future__ import annotations

from typing import Iterable

#: encoder-design §5.2 的最低错误码集合（逐字来自设计文档）。
MINIMUM_CODES = (
    "unknown_type",
    "unregistered_field",
    "value_type_mismatch",
    "dangling_reference",
    "duplicate_identity",
    "count_mismatch",
    "invalid_position",
    "version_mismatch",
    "capacity_exceeded",
    "non_finite_value",
    "unsupported_structure",
    "empty_candidates_non_terminal",
    "numeric_precision_loss",
)

#: A-02 新增：Manifest 文件格式/编码配置不合法（均为 AI 侧自有资产，非 wire 对象）。
ADDED_CODES = ("invalid_manifest", "invalid_encoding_config")

#: A-03 新增：§5.2 的"至少包括"清单没有覆盖的四类失败，必须能与邻近码区分开：
#:
#: * ``field_not_applicable``   —— 字段本身已注册、值也合法，但该 owner（实体种类/
#:   动作类型/终局原因）不在其 ``applies_to`` 内（共享 I-22 的适用域一面）。
#: * ``invalid_content_state``  —— ``content_state`` 取值非法，或与 ``content_id`` 的
#:   存在性不配（共享 I-19）；与"实体字段"的 ``state``/``value`` 配对（I-20）分开。
#: * ``state_value_mismatch``   —— ``state`` 与 ``value`` 配对错误（known 无值、非 known
#:   带值，共享 I-20），以及候选字段非 known（共享 I-36）。它不是值类型错误。
#: * ``missing_required_field`` —— required 且适用的字段被省略（共享 I-20/I-36），
#:   与"未注册字段"（``unregistered_field``）和"不适用"（``field_not_applicable``）分开。
#:
#: 其余 A-03 校验点直接使用 §5.2 已有码（``capacity_exceeded``/``invalid_position``/
#: ``count_mismatch``/``dangling_reference``/``unknown_type``/…），不另造同义词。
A03_ADDED_CODES = (
    "field_not_applicable",
    "invalid_content_state",
    "missing_required_field",
    "state_value_mismatch",
)

ALL_CODES = MINIMUM_CODES + ADDED_CODES + A03_ADDED_CODES

#: 本批（A-02）实际会抛出的错误码（其余码由 A-03/A-04 抛出）。
A02_RAISED_CODES = (
    "unknown_type",
    "unregistered_field",
    "value_type_mismatch",
    "duplicate_identity",
    "version_mismatch",
    "non_finite_value",
    "numeric_precision_loss",
    "invalid_manifest",
    "invalid_encoding_config",
)

#: A-03 校验层实际会抛出的错误码（含共享映射与自有检查两层）。
A03_RAISED_CODES = (
    "unknown_type",
    "unregistered_field",
    "value_type_mismatch",
    "dangling_reference",
    "duplicate_identity",
    "count_mismatch",
    "invalid_position",
    "version_mismatch",
    "capacity_exceeded",
    "non_finite_value",
    "unsupported_structure",
    "empty_candidates_non_terminal",
    "numeric_precision_loss",
) + A03_ADDED_CODES


class EncodingError(Exception):
    """编码阶段显式失败；``to_wire()`` 给出 §5.2 的最小错误对象。"""

    __slots__ = ("code", "path", "message", "related_ids")

    def __init__(
        self,
        code: str,
        path: str,
        message: str,
        related_ids: Iterable[str] = (),
    ) -> None:
        if code not in ALL_CODES:
            raise ValueError(f"未登记的错误码 {code!r}；请先在 errors.py 登记")
        self.code = code
        self.path = path
        self.message = message
        #: 相关标识：共享规则 ID（``I-xx``/``L0-xx``）、字段/实体/候选 ID 等。
        self.related_ids = tuple(related_ids)
        super().__init__(self.render())

    def to_wire(self) -> dict:
        return {
            "code": self.code,
            "path": self.path,
            "related_ids": list(self.related_ids),
            "message": self.message,
        }

    def render(self) -> str:
        extra = f" related_ids={list(self.related_ids)}" if self.related_ids else ""
        return f"{self.code} @ {self.path}: {self.message}{extra}"


__all__ = [
    "ADDED_CODES",
    "ALL_CODES",
    "A02_RAISED_CODES",
    "A03_ADDED_CODES",
    "A03_RAISED_CODES",
    "EncodingError",
    "MINIMUM_CODES",
]
