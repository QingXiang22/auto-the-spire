#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""net-the-spire wire v0.1 —— 规范化（canonical bytes）、decimal 规范形式与摘要参考算法。

仅使用 Python 3 标准库。规范描述见 ``docs/contracts/net/0.1/canonical.md``。

规则出处（docs/design/net-the-spire/contract-artifacts.md）：
  * §2 第 19 行 decimal 规范形式与结构整数范围；
  * §2 第 21 行 规范字节（紧凑 JSON、键按 UTF-8 字节序、转义集合）；
  * §2 第 23–31 行 摘要与身份输入白名单；
  * §3 第 37–39 行 公开数组排序与动作语义键。

实现刻意不依赖 ``json.dumps``：两端 serializer 的默认行为（ensure_ascii、转义集合、
键排序、浮点格式）不同，规范要求逐项固定。
"""

from __future__ import annotations

import hashlib
import re
from decimal import Decimal

#: 规范算法版本；按 contract-artifacts §2 第 33 行“资产中的规范算法版本也进入相应身份”，
#: 它作为一等键进入各身份摘要输入。
CANONICAL_VERSION = "net-wire-canon-v1"

#: 结构整数上界 2^53-1（protocol.md §2 第 73 行）。
SAFE_INTEGER_MAX = 9007199254740991

#: seed 上界 2^64-1（protocol.md §4 第 185 行）。
SEED_MAX = 18446744073709551615

#: 源精确 decimal 的接受语法（编码器输入，允许符号/前导零/指数）。
_DEC_SOURCE_RE = re.compile(
    r"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$"
)

#: 规范 decimal：整数部分为 0 或无前导零的十进制；小数部分最后一位非零。
#: contract-artifacts §2 第 19 行允许 ``0.xxx`` 与 ``-0.xxx``。
_DEC_CANONICAL_RE = re.compile(r"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]*[1-9])?$")

#: number_format=integer 的规范形式（无小数部分）。
_DEC_INTEGER_RE = re.compile(r"^-?(?:0|[1-9][0-9]*)$")

_ESCAPES = {
    '"': '\\"',
    "\\": "\\\\",
    "\b": "\\b",
    "\t": "\\t",
    "\n": "\\n",
    "\f": "\\f",
    "\r": "\\r",
}


class CanonicalError(ValueError):
    """输入无法表示为规范 wire 值。"""


# --------------------------------------------------------------------------- #
# decimal
# --------------------------------------------------------------------------- #
def is_canonical_decimal(text: object) -> bool:
    """wire 解码判定：字符串是否已是规范十进制。非规范输入不默默重写（§2 第 19 行）。"""
    if not isinstance(text, str):
        return False
    if text == "-0":
        return False
    return bool(_DEC_CANONICAL_RE.match(text))


def require_canonical_decimal(text: object, what: str = "decimal") -> str:
    if not is_canonical_decimal(text):
        raise CanonicalError(f"{what}: 非规范 decimal {text!r}")
    return text  # type: ignore[return-value]


def is_integer_decimal(text: object) -> bool:
    """number_format=integer 的规范值判定（无小数部分）。"""
    return isinstance(text, str) and bool(_DEC_INTEGER_RE.match(text))


def normalize_decimal(value: object) -> str:
    """编码器：把源精确值规范化为 wire 十进制字符串（§2 第 19 行、protocol §2 第 73 行）。

    接受 ``int``、``str``、``decimal.Decimal``；拒绝 bool、非有限值与浮点。
    去掉前导 ``+``、前导零与小数末尾多余的零；零一律输出 ``"0"``（含 ``-0``/``-0.000``）。
    """
    if isinstance(value, bool):
        raise CanonicalError("boolean 不是 decimal")
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        raise CanonicalError("不接受浮点源值，会丢失源精度")
    if isinstance(value, Decimal):
        if not value.is_finite():
            raise CanonicalError(f"拒绝非有限 decimal: {value}")
        text = str(value)
    elif isinstance(value, str):
        text = value
    else:
        raise CanonicalError(f"不支持的 decimal 源类型: {type(value).__name__}")
    if not _DEC_SOURCE_RE.match(text):
        raise CanonicalError(f"拒绝非 decimal 源值: {text!r}")

    negative = text[0] == "-"
    body = text[1:] if text[0] in "+-" else text
    mantissa, sep, exponent = body.partition("e")
    if not sep:
        mantissa, sep, exponent = body.partition("E")
    exp = int(exponent) if sep else 0
    int_part, dot, frac_part = mantissa.partition(".")
    digits = int_part + frac_part
    point = len(int_part) + exp

    if point <= 0:
        plain = "0." + "0" * (-point) + digits
    elif point >= len(digits):
        plain = digits + "0" * (point - len(digits))
    else:
        plain = digits[:point] + "." + digits[point:]

    if "." in plain:
        plain = plain.rstrip("0").rstrip(".")
    head, _, tail = plain.partition(".")
    head = head.lstrip("0") or "0"
    out = head if not tail else f"{head}.{tail}"
    if out == "0":
        return "0"
    return f"-{out}" if negative else out


# --------------------------------------------------------------------------- #
# 规范字节
# --------------------------------------------------------------------------- #
def _encode_string(text: str) -> str:
    out = ['"']
    for ch in text:
        cp = ord(ch)
        if ch in _ESCAPES:
            out.append(_ESCAPES[ch])
        elif cp < 0x20:
            out.append("\\u%04x" % cp)
        elif 0xD800 <= cp <= 0xDFFF:
            raise CanonicalError(f"不合法 Unicode（孤立代理项 U+{cp:04X}）")
        else:
            # 保留原 Unicode，不做归一化，不转义 "/"（§2 第 21 行）。
            out.append(ch)
    out.append('"')
    return "".join(out)


def _key_bytes(key: object) -> bytes:
    if not isinstance(key, str):
        raise CanonicalError(f"对象键必须是字符串，得到 {type(key).__name__}")
    try:
        return key.encode("utf-8")
    except UnicodeEncodeError as exc:  # 孤立代理项
        raise CanonicalError(f"不合法 Unicode 键: {key!r}") from exc


def canonical_text(value: object) -> str:
    """规范字节的无 BOM UTF-8 紧凑 JSON 文本（§2 第 21 行）。"""
    if value is None:
        return "null"
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, str):
        return _encode_string(value)
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        raise CanonicalError("wire 不含浮点字面量；精确数值必须是规范字符串")
    if isinstance(value, dict):
        parts = []
        for key in sorted(value.keys(), key=_key_bytes):
            parts.append(f"{_encode_string(key)}:{canonical_text(value[key])}")
        return "{" + ",".join(parts) + "}"
    if isinstance(value, (list, tuple)):
        return "[" + ",".join(canonical_text(item) for item in value) + "]"
    raise CanonicalError(f"不支持的值类型: {type(value).__name__}")


def canonical_bytes(value: object) -> bytes:
    return canonical_text(value).encode("utf-8")


def digest_bytes(payload: bytes) -> str:
    """所有摘要采用 ``sha256:<小写十六进制>``（§2 第 23 行）。"""
    return "sha256:" + hashlib.sha256(payload).hexdigest()


def digest(value: object) -> str:
    return digest_bytes(canonical_bytes(value))


# --------------------------------------------------------------------------- #
# 动作语义键（§3 第 39 行）
# --------------------------------------------------------------------------- #
def action_semantic_key(action: dict) -> bytes:
    """规范序列化的 ``{action_type, fields}``；fields 按 field_id，值使用规范 wire。

    ``candidate_id`` 不参与排序/特征。
    """
    fields = action.get("fields", [])
    ordered = sorted(fields, key=lambda f: _key_bytes(f.get("field_id")))
    return canonical_bytes({"action_type": action.get("action_type"), "fields": ordered})


def legal_action_sort_key(action: dict) -> bytes:
    return action_semantic_key(action)


# --------------------------------------------------------------------------- #
# 数组排序键（§3 第 37 行）
# --------------------------------------------------------------------------- #
def _sort_text(value: object) -> bytes:
    if value is None:
        return b"\x00"  # 缺省排在最前；同一 type_id 下 position 的出现性一致（见 canonical.md）
    return _key_bytes(value)


def entity_sort_key(entity: dict) -> bytes:
    return _sort_text(entity.get("instance_id"))


def field_sort_key(field: dict) -> bytes:
    return _sort_text(field.get("field_id"))


def relation_sort_key(relation: dict) -> tuple:
    return (
        _sort_text(relation.get("type_id")),
        _sort_text(relation.get("source_ref")),
        _sort_text(relation.get("target_ref")),
        relation.get("position") is not None,
        relation.get("position") if relation.get("position") is not None else 0,
    )


def pile_sort_key(pile: dict) -> bytes:
    return _sort_text(pile.get("pile_ref"))


def sequence_sort_key(sequence: dict) -> tuple:
    return (_sort_text(sequence.get("type_id")), _sort_text(sequence.get("owner_ref")))


def group_sort_key(group: dict) -> tuple:
    return (_sort_text(group.get("type_id")), _sort_text(group.get("owner_ref")))


def card_position_sort_key(position: dict) -> tuple:
    return (
        position.get("position") if position.get("position") is not None else -1,
        _sort_text(position.get("card_ref")),
    )


def group_member_sort_key(member: dict) -> bytes:
    return _sort_text(member.get("entity_ref"))


# --------------------------------------------------------------------------- #
# 身份 / 指纹（§2 第 25–31 行）
# --------------------------------------------------------------------------- #
#: 目录身份中需要按 ID 集合排序的键（“ID 集合排序”）。
_ID_SET_KEYS = frozenset(
    {
        "kind_ids",
        "member_kind_ids",
        "owner_kind_ids",
        "element_kind_ids",
        "source_kind_ids",
        "target_kind_ids",
        "required_fields",
        "optional_fields",
        "visibility_check_ids",
    }
)

#: 明确“公开有序”而**不**排序的数组（contract-artifacts §2 第 27 行）。
_ORDERED_BY_DECLARATION = frozenset({"category_ids"})

_DECLARATION_TABLES = (
    "entity_kinds",
    "content_types",
    "fields",
    "relation_types",
    "pile_types",
    "sequence_types",
    "group_types",
    "action_types",
    "terminal_reasons",
)


def _canonicalize_declaration(node: object) -> object:
    """递归去掉 description、排序 ID 集合；有序数组保持源顺序。"""
    if isinstance(node, dict):
        out = {}
        for key, value in node.items():
            if key == "description":
                continue
            if key in _ORDERED_BY_DECLARATION:
                out[key] = value
            elif key in _ID_SET_KEYS and isinstance(value, list):
                out[key] = sorted(value, key=_key_bytes)
            elif key == "applies_to" and isinstance(value, dict):
                out[key] = {
                    name: sorted(items, key=_key_bytes)
                    for name, items in value.items()
                }
            else:
                out[key] = _canonicalize_declaration(value)
        return out
    if isinstance(node, list):
        return [_canonicalize_declaration(item) for item in node]
    return node


def catalog_identity_input(catalog: dict) -> dict:
    """catalog_id 的摘要输入（§2 第 27 行）：protocol_version + 除 description 外的语义键。

    声明按 id 排序；排除自身 id（wire 目录对象不含 catalog_id，天然满足）。
    """
    declarations = catalog.get("declarations", {})
    tables = {}
    for name in _DECLARATION_TABLES:
        items = declarations.get(name, [])
        stripped = [_canonicalize_declaration(item) for item in items]
        tables[name] = sorted(stripped, key=lambda item: _key_bytes(item.get("id", "")))
    return {
        "canonical_version": CANONICAL_VERSION,
        "protocol_version": catalog.get("protocol_version"),
        "declarations": tables,
    }


def catalog_digest(catalog: dict) -> str:
    return digest(catalog_identity_input(catalog))


#: 规则集身份中禁止出现的键（种子与时间/进程身份不属于内容身份，§2 第 28 行）。
_FORBIDDEN_RULESET_KEYS = frozenset(
    {"seed", "seeds", "run_seed", "timestamp", "started_at", "finished_at", "pid", "process_id"}
)

#: 后端身份中禁止出现的键（具体限额与耗时不属于语义身份，§2 第 29 行）。
_FORBIDDEN_BACKEND_KEYS = frozenset(
    {"pid", "process_id", "limits", "elapsed_ms", "timing", "started_at", "max_concurrency"}
)


def _reject_keys(node: dict, forbidden: frozenset, what: str) -> None:
    if not isinstance(node, dict):
        return
    for key in node:
        if key in forbidden:
            raise CanonicalError(f"{what}: 身份输入不得包含 {key!r}")
    for value in node.values():
        if isinstance(value, dict):
            _reject_keys(value, forbidden, what)


def ruleset_identity_input(descriptor: dict) -> dict:
    """ruleset_id 的摘要输入（§2 第 28 行）。

    wire 没有规则集对象；此处固定提供方侧描述符的键，供跨实现复现：
    engine_versions / ir_version / package_order / package_digests / rule_config /
    numeric_config / prng_profile。种子、时间戳与进程身份必须排除。
    """
    _reject_keys(descriptor, _FORBIDDEN_RULESET_KEYS, "ruleset_id")
    required = (
        "engine_versions",
        "ir_version",
        "package_order",
        "package_digests",
        "rule_config",
        "numeric_config",
        "prng_profile",
    )
    missing = [key for key in required if key not in descriptor]
    if missing:
        raise CanonicalError(f"ruleset_id 输入缺少键: {', '.join(missing)}")
    out = {"canonical_version": CANONICAL_VERSION}
    for key in required:
        out[key] = descriptor[key]
    return out


def ruleset_digest(descriptor: dict) -> str:
    return digest(ruleset_identity_input(descriptor))


def backend_profile_identity_input(descriptor: dict) -> dict:
    """backend_profile_id 的摘要输入（§2 第 29 行）。"""
    _reject_keys(descriptor, _FORBIDDEN_BACKEND_KEYS, "backend_profile_id")
    required = (
        "backend_kind",
        "projection_semantics_version",
        "public_identity_version",
        "action_mapping_version",
        "field_availability",
        "supported_mechanism_ids",
    )
    missing = [key for key in required if key not in descriptor]
    if missing:
        raise CanonicalError(f"backend_profile_id 输入缺少键: {', '.join(missing)}")
    out = {"canonical_version": CANONICAL_VERSION}
    for key in required:
        out[key] = descriptor[key]
    return out


def backend_profile_digest(descriptor: dict) -> str:
    return digest(backend_profile_identity_input(descriptor))


def request_payload_fingerprint(operation: str, payload: object) -> str:
    """请求载荷指纹（§2 第 30 行）：operation 与完整 payload；request_id 不参与。"""
    return digest(
        {
            "canonical_version": CANONICAL_VERSION,
            "operation": operation,
            "payload": payload,
        }
    )


def snapshot_storage_digest(snapshot: dict) -> str:
    """快照存储摘要（§2 第 31 行）：完整规范公开快照，含身份与控制元数据。"""
    return digest(snapshot)
