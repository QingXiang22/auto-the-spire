"""分层契约检查入口（复用共享规范资产的分层语义）。

层与共享参考实现一致：

* **L0** 解析：重复 JSON 键、浮点字面量、非法 Unicode、BOM（``wire_json``）。
* **L1** 结构：``wire.schema.json`` / ``coverage.schema.json``（``jsonschema_min``）。
* **L2** 目录内部：引用闭合、kind_id 独占、能力层级、覆盖表（``wire_layers``）。
* **L3** 快照对目录：字段值类型、终局 facts、牌堆四条不变量、排序、动作语义键。

本模块只编排与暴露；判定逻辑一律来自 ``tools/contract-check/``，AI 侧不新增规则、
不放宽任何共享规则（发现分歧时给反例，见交接报告）。
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Iterable, Optional

from .errors import ContractViolation, Finding
from .shared import spec_assets
from .views import PublicCatalog, PublicSnapshot

ALL_LAYERS = ("L0", "L1", "L2", "L3")


def _finding(rule_id: str, layer: str, path: str, message: str) -> Finding:
    return Finding(rule_id, layer, path, message)


# --------------------------------------------------------------------------- #
# 规范字节 / 摘要 / 动作语义键（转发共享参考实现）
# --------------------------------------------------------------------------- #
def canonical_text(value: Any) -> str:
    return spec_assets().wire_canonical.canonical_text(value)


def canonical_bytes(value: Any) -> bytes:
    return spec_assets().wire_canonical.canonical_bytes(value)


def digest_bytes(payload: bytes) -> str:
    return spec_assets().wire_canonical.digest_bytes(payload)


def catalog_digest(document: dict) -> str:
    return spec_assets().wire_canonical.catalog_digest(document)


def snapshot_storage_digest(document: dict) -> str:
    return spec_assets().wire_canonical.snapshot_storage_digest(document)


def action_semantic_key(action: dict) -> bytes:
    return spec_assets().wire_canonical.action_semantic_key(action)


def normalize_decimal(value: Any) -> str:
    return spec_assets().wire_canonical.normalize_decimal(value)


def is_canonical_decimal(text: Any) -> bool:
    """wire 解码判定：字符串是否已是规范十进制（I-21，不默默重写）。"""
    return spec_assets().wire_canonical.is_canonical_decimal(text)


def is_integer_decimal(text: Any) -> bool:
    """``number_format=integer`` 的规范值判定（I-72，无小数部分）。"""
    return spec_assets().wire_canonical.is_integer_decimal(text)


# --------------------------------------------------------------------------- #
# 分层检查
# --------------------------------------------------------------------------- #
def parse_wire_text(text: str | bytes, *, what: str) -> tuple[Optional[Any], list[Finding]]:
    """L0 严格解析；返回 ``(document, findings)``，失败时 document 为 None。"""
    assets = spec_assets()
    try:
        return assets.parse_wire_text(text, what=what), []
    except assets.wire_json.ParseError as exc:
        return None, [_finding(exc.rule_id, "L0", what, exc.message)]
    except assets.wire_canonical.CanonicalError as exc:
        return None, [_finding("L0-UNICODE", "L0", what, str(exc))]


def document_findings(
    document: Any,
    role: str,
    *,
    layers: Iterable[str] = ALL_LAYERS,
    catalog: Optional[dict] = None,
    coverage: Optional[dict] = None,
) -> list[Finding]:
    """对单个文档按层检查（不抛异常，返回共享结论）。"""
    assets = spec_assets()
    active = set(layers)
    findings: list[Finding] = []
    if "L1" in active:
        for report in assets.schema_errors(document, role):
            # 共享 jsonschema_min 的报告是 ``"<JSON path>: <说明>"`` 单字符串；路径与说明必须
            # 分开：路径取 ``$`` 打头的部分并换成 role 前缀，说明独立成 message。
            pointer, separator, detail = report.partition(": ")
            if separator and pointer.startswith("$"):
                findings.append(_finding("I-66", "L1", f"{role}{pointer[1:]}", detail))
            else:  # 没有可解析的指针：整条报告落在 role 上，不伪造路径
                findings.append(_finding("I-66", "L1", role, report))
    if role == "coverage":
        if "L2" in active:
            findings.extend(assets.wire_layers.check_coverage(document))
        return findings
    if "L2" in active and role == "catalog":
        findings.extend(assets.wire_layers.check_catalog(document, coverage))
        if coverage is not None:
            findings.extend(assets.wire_layers.check_catalog_coverage(document, coverage))
    if "L3" in active and role == "snapshot" and catalog is not None:
        findings.extend(assets.wire_layers.check_snapshot(document, catalog))
    return findings


def _raise_if(findings: list[Finding], role: str) -> None:
    if findings:
        raise ContractViolation(role, findings)


def parse_catalog_document(
    document: Any,
    *,
    coverage: Optional[dict] = None,
    layers: Iterable[str] = ALL_LAYERS,
) -> PublicCatalog:
    """校验并构建目录视图；失败抛 :class:`ContractViolation`，不返回部分结果。"""
    findings: list[Finding] = []
    if coverage is not None:
        findings.extend(document_findings(coverage, "coverage", layers=layers))
    findings.extend(
        document_findings(document, "catalog", layers=layers, coverage=coverage)
    )
    _raise_if(findings, "catalog")
    return PublicCatalog.from_wire(document)


def parse_snapshot_document(
    document: Any,
    catalog: dict,
    *,
    layers: Iterable[str] = ALL_LAYERS,
) -> PublicSnapshot:
    """校验并构建快照视图；``catalog`` 必须是已通过校验的目录原文档。"""
    findings = document_findings(document, "snapshot", layers=layers, catalog=catalog)
    _raise_if(findings, "snapshot")
    return PublicSnapshot.from_wire(document)


def load_catalog(path: str | Path, *, coverage_path: Optional[str | Path] = None) -> tuple[PublicCatalog, dict]:
    """从文件读取（L0 严格解析）+ 校验；返回 ``(视图, 原文档)``。"""
    catalog_path = Path(path)
    catalog_document, findings = parse_wire_text(
        catalog_path.read_bytes(), what=catalog_path.name
    )
    _raise_if(findings, "catalog")
    coverage_document = None
    if coverage_path is not None:
        coverage_file = Path(coverage_path)
        coverage_document, findings = parse_wire_text(
            coverage_file.read_bytes(), what=coverage_file.name
        )
        _raise_if(findings, "coverage")
    return (
        parse_catalog_document(catalog_document, coverage=coverage_document),
        catalog_document,
    )


def load_snapshot(path: str | Path, catalog: dict) -> tuple[PublicSnapshot, dict]:
    """从文件读取 + 校验；返回 ``(视图, 原文档)``。"""
    snapshot_path = Path(path)
    snapshot_document, findings = parse_wire_text(
        snapshot_path.read_bytes(), what=snapshot_path.name
    )
    _raise_if(findings, "snapshot")
    return parse_snapshot_document(snapshot_document, catalog), snapshot_document


__all__ = [
    "ALL_LAYERS",
    "action_semantic_key",
    "canonical_bytes",
    "canonical_text",
    "catalog_digest",
    "digest_bytes",
    "document_findings",
    "is_canonical_decimal",
    "is_integer_decimal",
    "load_catalog",
    "load_snapshot",
    "normalize_decimal",
    "parse_catalog_document",
    "parse_snapshot_document",
    "parse_wire_text",
    "snapshot_storage_digest",
]
