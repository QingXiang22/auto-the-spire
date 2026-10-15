#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""JSON Schema 2020-12 的最小子集校验器（仅标准库）。

只实现 ``docs/contracts/net/0.1/wire.schema.json`` 与 ``coverage.schema.json`` 实际使用的
关键字；不是通用 JSON Schema 实现，用于让契约检查不依赖第三方包。

支持：$ref（本地 #/$defs/*）、type、enum、const、required、properties、
additionalProperties、propertyNames、items、minItems、maxItems、uniqueItems、
minLength、maxLength、pattern、minimum、maximum、allOf、anyOf、oneOf、not、if/then/else。
"""

from __future__ import annotations

import re
from typing import Any

from wire_canonical import CanonicalError, canonical_bytes

_JSON_TYPES = ("object", "array", "string", "integer", "number", "boolean", "null")


def _type_of(value: Any) -> str:
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int):
        return "integer"
    if isinstance(value, float):
        return "number"
    if isinstance(value, str):
        return "string"
    if isinstance(value, list):
        return "array"
    if isinstance(value, dict):
        return "object"
    return "unknown"


def _matches_type(value: Any, name: str) -> bool:
    kind = _type_of(value)
    if name == "number":
        return kind in ("integer", "number")
    return kind == name


def _resolve(ref: str, root: dict) -> dict:
    if not ref.startswith("#/"):
        raise ValueError(f"只支持本地 $ref: {ref}")
    node: Any = root
    for token in ref[2:].split("/"):
        token = token.replace("~1", "/").replace("~0", "~")
        if isinstance(node, list):
            node = node[int(token)]
        else:
            node = node[token]
    return node


def _unique_values(items: list) -> bool:
    seen = set()
    for item in items:
        try:
            key = canonical_bytes(item)
        except CanonicalError:
            key = repr(item).encode("utf-8", "replace")
        if key in seen:
            return False
        seen.add(key)
    return True


def validate(instance: Any, schema: dict, root: dict | None = None, path: str = "$") -> list[str]:
    """返回错误字符串列表；空列表表示通过。"""
    if root is None:
        root = schema
    errors: list[str] = []
    _walk(instance, schema, root, path, errors)
    return errors


def _walk(instance: Any, schema: Any, root: dict, path: str, errors: list[str]) -> None:
    if schema is True or schema == {}:
        return
    if schema is False:
        errors.append(f"{path}: 被 false schema 拒绝")
        return
    if not isinstance(schema, dict):
        return

    if "$ref" in schema:
        _walk(instance, _resolve(schema["$ref"], root), root, path, errors)
        # 2020-12 允许 $ref 的兄弟关键字；本资产未使用，忽略以免误判。

    if "type" in schema:
        declared = schema["type"]
        names = declared if isinstance(declared, list) else [declared]
        if not any(_matches_type(instance, name) for name in names):
            errors.append(f"{path}: 期望 {declared}，得到 {_type_of(instance)}")

    if "const" in schema and instance != schema["const"]:
        errors.append(f"{path}: 期望常量 {schema['const']!r}")
    if "enum" in schema and instance not in schema["enum"]:
        errors.append(f"{path}: {instance!r} 不在枚举 {schema['enum']!r}")

    if "allOf" in schema:
        for sub in schema["allOf"]:
            _walk(instance, sub, root, path, errors)
    if "not" in schema:
        sub_errors: list[str] = []
        _walk(instance, schema["not"], root, path, sub_errors)
        if not sub_errors:
            errors.append(f"{path}: 命中禁止分支 not")
    if "anyOf" in schema:
        branches = []
        for sub in schema["anyOf"]:
            sub_errors = []
            _walk(instance, sub, root, path, sub_errors)
            branches.append(sub_errors)
        if all(branch for branch in branches):
            errors.append(f"{path}: 不匹配任何 anyOf 分支")
    if "oneOf" in schema:
        matches = 0
        for sub in schema["oneOf"]:
            sub_errors = []
            _walk(instance, sub, root, path, sub_errors)
            if not sub_errors:
                matches += 1
        if matches != 1:
            errors.append(f"{path}: oneOf 期望恰好一个分支匹配，实际 {matches}")
    if "if" in schema:
        cond_errors: list[str] = []
        _walk(instance, schema["if"], root, path, cond_errors)
        branch = "then" if not cond_errors else "else"
        if branch in schema:
            _walk(instance, schema[branch], root, path, errors)

    if "required" in schema and isinstance(instance, dict):
        for key in schema["required"]:
            if key not in instance:
                errors.append(f"{path}: 缺少必需键 {key!r}")

    if isinstance(instance, dict):
        properties = schema.get("properties", {})
        for key, sub in properties.items():
            if key in instance:
                _walk(instance[key], sub, root, f"{path}.{key}", errors)
        additional = schema.get("additionalProperties")
        if additional is not None:
            for key in instance:
                if key in properties:
                    continue
                if additional is False:
                    errors.append(f"{path}: 未知键 {key!r}（additionalProperties=false）")
                elif isinstance(additional, dict):
                    _walk(instance[key], additional, root, f"{path}.{key}", errors)
        if "propertyNames" in schema:
            for key in instance:
                if not isinstance(key, str):
                    errors.append(f"{path}: 对象键必须是字符串")
                    continue
                _walk(key, schema["propertyNames"], root, f"{path}.<key {key!r}>", errors)

    if isinstance(instance, list):
        if "items" in schema:
            for index, item in enumerate(instance):
                _walk(item, schema["items"], root, f"{path}[{index}]", errors)
        if "minItems" in schema and len(instance) < schema["minItems"]:
            errors.append(f"{path}: 元素数 {len(instance)} 小于 minItems {schema['minItems']}")
        if "maxItems" in schema and len(instance) > schema["maxItems"]:
            errors.append(f"{path}: 元素数 {len(instance)} 大于 maxItems {schema['maxItems']}")
        if schema.get("uniqueItems") and not _unique_values(instance):
            errors.append(f"{path}: uniqueItems 违反（存在重复元素）")

    if isinstance(instance, str):
        if "minLength" in schema and len(instance) < schema["minLength"]:
            errors.append(f"{path}: 长度小于 minLength {schema['minLength']}")
        if "maxLength" in schema and len(instance) > schema["maxLength"]:
            errors.append(f"{path}: 长度大于 maxLength {schema['maxLength']}")
        if "pattern" in schema and not re.search(schema["pattern"], instance):
            errors.append(f"{path}: 不匹配 pattern {schema['pattern']!r}")

    if isinstance(instance, (int, float)) and not isinstance(instance, bool):
        if "minimum" in schema and instance < schema["minimum"]:
            errors.append(f"{path}: {instance} 小于 minimum {schema['minimum']}")
        if "maximum" in schema and instance > schema["maximum"]:
            errors.append(f"{path}: {instance} 大于 maximum {schema['maximum']}")
        if "exclusiveMinimum" in schema and instance <= schema["exclusiveMinimum"]:
            errors.append(f"{path}: {instance} 不大于 exclusiveMinimum {schema['exclusiveMinimum']}")
        if "exclusiveMaximum" in schema and instance >= schema["exclusiveMaximum"]:
            errors.append(f"{path}: {instance} 不小于 exclusiveMaximum {schema['exclusiveMaximum']}")


def collect_refs(schema: Any) -> set[str]:
    refs: set[str] = set()
    if isinstance(schema, dict):
        for key, value in schema.items():
            if key == "$ref" and isinstance(value, str):
                refs.add(value)
            else:
                refs |= collect_refs(value)
    elif isinstance(schema, list):
        for item in schema:
            refs |= collect_refs(item)
    return refs
