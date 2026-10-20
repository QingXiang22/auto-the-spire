#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""wire v0.2 的 L0 解析层：重复键、非法 Unicode、浮点字面量与帧字节上限。

出处：protocol.md §6 第 217 行（“顶层未知属性、未知类型、重复键和缺失必需值拒绝”）、
§2 第 73 行（“禁止重复 JSON 键”“避免先经 JSON 浮点解析丢失源精度”）、
contract-artifacts §1 第 11 行（“解析阶段先拒绝重复键”）。
"""

from __future__ import annotations

import json
from typing import Any

from wire_canonical import CanonicalError, SAFE_INTEGER_MAX


class ParseError(ValueError):
    """L0 层拒绝。"""

    def __init__(self, rule_id: str, message: str) -> None:
        super().__init__(message)
        self.rule_id = rule_id
        self.message = message


def _no_duplicates(pairs: list) -> dict:
    seen = {}
    for key, value in pairs:
        if key in seen:
            raise ParseError("I-24", f"重复 JSON 键: {key!r}")
        seen[key] = value
    return seen


def _reject_constant(name: str) -> Any:
    raise ParseError("I-91", f"JSON 常量 {name} 不是 wire 值")


def _reject_float(text: str) -> Any:
    raise ParseError("I-91", f"wire 不含浮点字面量: {text}")


def parse_json_strict(text: str | bytes, *, what: str = "document") -> Any:
    """严格解析：拒绝重复键、NaN/Infinity、浮点字面量与非 UTF-8 输入。"""
    if isinstance(text, bytes):
        try:
            if text.startswith(b"\xef\xbb\xbf"):
                raise ParseError("L0-BOM", f"{what}: UTF-8 文本不得带 BOM")
            text = text.decode("utf-8")
        except UnicodeDecodeError as exc:
            raise ParseError("L0-UTF8", f"{what}: 不是合法 UTF-8: {exc}") from exc
    if text.startswith("\ufeff"):
        raise ParseError("L0-BOM", f"{what}: UTF-8 文本不得带 BOM")
    try:
        value = json.loads(
            text,
            object_pairs_hook=_no_duplicates,
            parse_constant=_reject_constant,
            parse_float=_reject_float,
        )
    except ParseError:
        raise
    except json.JSONDecodeError as exc:
        raise ParseError("L0-JSON", f"{what}: JSON 解析失败: {exc}") from exc
    check_unicode(value, what=what)
    return value


def iter_strings(node: Any, path: str = "$"):
    if isinstance(node, str):
        yield path, node
    elif isinstance(node, dict):
        for key, value in node.items():
            yield from iter_strings(key, f"{path}.<key>")
            yield from iter_strings(value, f"{path}.{key}")
    elif isinstance(node, list):
        for index, value in enumerate(node):
            yield from iter_strings(value, f"{path}[{index}]")


def check_unicode(node: Any, *, what: str = "document") -> None:
    """拒绝孤立代理项等不合法 Unicode（contract-artifacts §2 第 21 行）。"""
    for path, text in iter_strings(node):
        for ch in text:
            cp = ord(ch)
            if 0xD800 <= cp <= 0xDFFF:
                raise ParseError(
                    "L0-UNICODE", f"{what}: {path} 含非法 Unicode U+{cp:04X}"
                )


def check_canonical_bytes(node: Any, *, what: str = "document") -> None:
    """可编码性检查：wire 文档必须能生成规范字节。"""
    from wire_canonical import canonical_bytes  # 局部导入以避免循环

    try:
        canonical_bytes(node)
    except CanonicalError as exc:
        raise ParseError("I-91", f"{what}: {exc}") from exc


def check_frame_bytes(payload: bytes, limit: int, *, what: str = "frame") -> int:
    """帧字节上限（protocol §6 第 211 行）：超限失败，不解析部分数据。"""
    size = len(payload)
    if limit is not None and size > limit:
        raise ParseError("I-68", f"{what}: {size} 字节超过配置上限 {limit}")
    return size


def safe_int(value: Any, *, what: str, maximum: int = SAFE_INTEGER_MAX) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise ParseError("I-23", f"{what}: 结构整数必须是 JSON 整数")
    if value < 0 or value > maximum:
        raise ParseError("I-23", f"{what}: 结构整数超出 0..{maximum}: {value}")
    return value
