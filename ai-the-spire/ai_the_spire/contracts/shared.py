"""定位并加载唯一共享规范资产（net v0.2）。

AI 侧 contracts 必须复用同一份规范（开工清单 §7.2 / encoder-design §2），因此本模块
是**唯一**允许触碰仓库其他目录的地方：它读取

* ``docs/contracts/net/0.2/wire.schema.json``     —— 结构唯一来源
* ``docs/contracts/net/0.2/coverage.schema.json``  —— 覆盖表结构
* ``tools/contract-check/``                        —— 规范化/分层校验参考实现

并把这些资产以只读方式暴露给 contracts 包。除此之外 ai-the-spire 不导入任何
其他模块；不导入 ``core-the-spire`` / ``mod-the-spire`` / 活动 client。

缺失任一资产时抛 :class:`SpecAssetError`（显式失败，不静默降级为“AI 自带的第二套
协议”）。
"""

from __future__ import annotations

import json
import sys
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from types import ModuleType
from typing import Any

#: ai-the-spire/ai_the_spire/contracts/shared.py -> 仓库根
_HERE = Path(__file__).resolve()
REPO_ROOT = _HERE.parents[3]

CONTRACT_DIR = REPO_ROOT / "docs" / "contracts" / "net" / "0.2"
WIRE_SCHEMA_PATH = CONTRACT_DIR / "wire.schema.json"
COVERAGE_SCHEMA_PATH = CONTRACT_DIR / "coverage.schema.json"
REFERENCE_DIR = REPO_ROOT / "tools" / "contract-check"

#: 共享 schema 中与快照/目录对应的 ``$defs`` 指针（协议对象 → 唯一结构定义）。
DOC_ROLES = {
    "catalog": "$defs/PublicCatalog",
    "snapshot": "$defs/PublicSnapshot",
    "coverage": "$defs/Coverage",
}


class SpecAssetError(RuntimeError):
    """共享规范资产缺失或无法加载。"""


@dataclass(frozen=True)
class SpecAssets:
    """共享规范资产的只读视图。"""

    repo_root: Path
    wire_schema_path: Path
    coverage_schema_path: Path
    reference_dir: Path
    wire_schema: dict
    coverage_schema: dict
    wire_json: ModuleType
    wire_canonical: ModuleType
    wire_layers: ModuleType
    jsonschema_min: ModuleType
    check_contracts: ModuleType

    # -- 结构定义 ---------------------------------------------------------- #
    def definition(self, pointer: str) -> dict:
        """按 ``"$defs/Name"`` 取出 schema 节点。"""
        node: Any = self.wire_schema
        for token in pointer.split("/"):
            if not isinstance(node, dict) or token not in node:
                raise SpecAssetError(f"schema 中不存在定义 {pointer!r}")
            node = node[token]
        return node

    def schema_errors(self, document: Any, role: str) -> list[str]:
        """L1 结构校验（复用共享 jsonschema_min 与共享 schema）。"""
        if role == "coverage":
            return list(self.jsonschema_min.validate(document, self.coverage_schema))
        pointer = DOC_ROLES.get(role)
        if pointer is None:
            raise SpecAssetError(f"未知角色 {role!r}；可用: {sorted(DOC_ROLES)}")
        return list(
            self.jsonschema_min.validate(
                document, self.definition(pointer), self.wire_schema
            )
        )

    def parse_wire_text(self, text: str | bytes, *, what: str) -> Any:
        """L0 严格解析（重复键、浮点字面量、非法 Unicode、BOM）。"""
        return self.wire_json.parse_json_strict(text, what=what)


def repo_root() -> Path:
    return REPO_ROOT


@lru_cache(maxsize=1)
def spec_assets() -> SpecAssets:
    """加载并缓存共享规范资产。"""

    missing = [
        str(path)
        for path in (WIRE_SCHEMA_PATH, COVERAGE_SCHEMA_PATH, REFERENCE_DIR)
        if not path.exists()
    ]
    if missing:
        raise SpecAssetError(
            "缺少共享规范资产: " + ", ".join(missing) + "（AI 侧不另建第二套协议）"
        )

    if str(REFERENCE_DIR) not in sys.path:
        sys.path.insert(0, str(REFERENCE_DIR))

    try:
        import check_contracts
        import jsonschema_min
        import wire_canonical
        import wire_json
        import wire_layers
    except ImportError as exc:  # pragma: no cover - 仅在资产被移动时触发
        raise SpecAssetError(f"无法导入共享参考实现: {exc}") from exc

    for module in (wire_json, wire_canonical, wire_layers, jsonschema_min, check_contracts):
        module_path = Path(getattr(module, "__file__", "")).resolve()
        if not module_path.is_relative_to(REFERENCE_DIR):
            raise SpecAssetError(
                f"模块 {module.__name__} 来自 {module_path}，不是共享参考实现 "
                f"{REFERENCE_DIR}（拒绝使用同名的第二套实现）"
            )

    with WIRE_SCHEMA_PATH.open("r", encoding="utf-8") as handle:
        wire_schema = json.load(handle)
    with COVERAGE_SCHEMA_PATH.open("r", encoding="utf-8") as handle:
        coverage_schema = json.load(handle)

    return SpecAssets(
        repo_root=REPO_ROOT,
        wire_schema_path=WIRE_SCHEMA_PATH,
        coverage_schema_path=COVERAGE_SCHEMA_PATH,
        reference_dir=REFERENCE_DIR,
        wire_schema=wire_schema,
        coverage_schema=coverage_schema,
        wire_json=wire_json,
        wire_canonical=wire_canonical,
        wire_layers=wire_layers,
        jsonschema_min=jsonschema_min,
        check_contracts=check_contracts,
    )
