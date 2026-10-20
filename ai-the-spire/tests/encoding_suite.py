#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""A-02 检查套件：Manifest（build/save/load、字典索引、数值变换、exact identity）
与 L0 严格文本解析向量。

职责分离与 A-01 一致：

* ``fixtures/<bundle>/*.json`` 是手写输入，``fixtures/<bundle>/expected.json`` 是手写期望；
* ``fixtures/manifest/legacy/*`` 是**手写旧字典文件**（不是本实现产出的），
  其摘要由共享规范 codec 计算后冻结在 ``fixtures/manifest/expected.json``；
* 本模块只编排检查，不生成期望、不放宽共享规则、不复制协议实现。

组名：``manifest``（A-02 主体）、``l0-text``（A-01 报告 §5 指出的 L0 缺口）。
"""

from __future__ import annotations

import copy
import json
import tempfile
from pathlib import Path
from typing import Any, Callable, Optional

from ai_the_spire.contracts import ContractViolation, layers
from ai_the_spire.contracts.types import Identity
from ai_the_spire.contracts.views import PublicCatalog
from ai_the_spire.encoding import (
    DICTIONARY_TABLES,
    EXCLUDED_DECLARATION_TABLES,
    CapacityLimits,
    EncodingConfig,
    EncodingError,
    GymManifest,
    NumericTransform,
    build_manifest,
    load_manifest,
    save_manifest,
)

from fixture_suite import BUNDLES, FIXTURE_ROOT, Outcome

MANIFEST_ROOT = FIXTURE_ROOT / "manifest"
TEXT_ROOT = FIXTURE_ROOT / "text"

#: 每个 bundle 使用的显式编码配置的变换部分（**输入**，不是期望）。
TRANSFORMS: "dict[str, dict[str, tuple[str, str]]]" = {
    "combat": {"fixture:energy": ("1", "3")},
    "variables": {"modB:charge": ("0", "0.5")},
    "scenario": {"fixture:node_row": ("2", "4")},
    "card_choice": {"fixture:cost": ("0", "2")},
}

#: 显式容量上限（输入；A-02 只持久化，超限拒绝由 A-03/A-04 执行）。
CAPACITIES = {
    "max_entities": 64,
    "max_fields": 64,
    "max_relations": 64,
    "max_pile_positions": 32,
    "max_sequences": 8,
    "max_groups": 8,
    "max_candidates": 8,
    "max_candidate_fields": 32,
}


# --------------------------------------------------------------------------- #
# 装载与工具
# --------------------------------------------------------------------------- #
def _read_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def _expected() -> dict:
    return _read_json(MANIFEST_ROOT / "expected.json")


def _bundle(name: str) -> dict:
    root = FIXTURE_ROOT / name
    coverage_path = root / "coverage.json"
    return {
        "catalog": _read_json(root / "catalog.json"),
        "coverage": _read_json(coverage_path) if coverage_path.exists() else None,
        "snapshots": {stem: _read_json(root / f"{stem}.json") for stem in BUNDLES[name]},
    }


def _config_for(name: str) -> EncodingConfig:
    return EncodingConfig(
        transforms={
            field_id: NumericTransform(offset=offset, scale=scale)
            for field_id, (offset, scale) in TRANSFORMS[name].items()
        },
        capacities=CapacityLimits(**CAPACITIES),
    )


def _manifest_from(name: str, catalog: Optional[dict] = None, config: Optional[EncodingConfig] = None) -> GymManifest:
    environment = _expected()["environment"]
    data = _bundle(name)
    return build_manifest(
        data["catalog"] if catalog is None else catalog,
        _config_for(name) if config is None else config,
        backend_profile_id=environment["backend_profile_id"],
        gym_id=environment["gym_id"],
        coverage=data["coverage"],
    )


def _dict_maps(manifest: GymManifest) -> dict:
    return {table: manifest.dictionaries[table].to_wire()["by_name"] for table in DICTIONARY_TABLES}


def _identity_of(name: str, snapshot_stem: str = "snapshot") -> Identity:
    return Identity.from_wire(_bundle(name)["snapshots"][snapshot_stem])


def _outcome(case_id: str, ok: bool, detail: str = "") -> Outcome:
    return Outcome("manifest", case_id, bool(ok), detail)


def _guard(case_id: str, run: Callable[[], tuple]) -> Outcome:
    """执行一个检查体；编码/契约类失败记为失败项而不是中止整套。"""
    try:
        ok, detail = run()
    except (EncodingError, ContractViolation, AssertionError) as exc:
        return _outcome(case_id, False, f"{type(exc).__name__}: {exc}")
    return _outcome(case_id, ok, detail)


def _expect_error(case_id: str, code: str, run: Callable[[], Any]) -> Outcome:
    try:
        result = run()
    except EncodingError as exc:
        ok = exc.code == code
        detail = "" if ok else f"错误码 {exc.code!r} != 期望 {code!r}；{exc.render()}"
        return _outcome(case_id, ok, detail)
    except ContractViolation as exc:
        return _outcome(case_id, False, f"共享契约违规 {exc.rule_ids}，期望编码错误 {code}")
    return _outcome(case_id, False, f"未失败（期望 {code}）；返回 {result!r}")


_LEGACY: dict = {}


def _resign(doc: dict) -> dict:
    """按共享 codec 重算 Manifest 摘要（测试构造"合法但内容不同"的文件）。"""
    payload = {key: value for key, value in doc.items() if key != "manifest_digest"}
    resigned = dict(doc)
    resigned["manifest_digest"] = layers.digest_bytes(layers.canonical_bytes(payload))
    return resigned


def _write_temp(doc: Any, tmp: str, name: str = "manifest.json") -> Path:
    path = Path(tmp) / name
    path.write_text(json.dumps(doc, ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def _legacy_manifest() -> GymManifest:
    if "manifest" not in _LEGACY:
        _LEGACY["manifest"] = load_manifest(MANIFEST_ROOT / _expected()["legacy"]["manifest"])
    return _LEGACY["manifest"]


# --------------------------------------------------------------------------- #
# manifest 组
# --------------------------------------------------------------------------- #
def check_registration_order(name: str) -> list[Outcome]:
    """编码场景 1：同一批类型、注册顺序不同 → 索引、变换与文件字节相同。"""
    case_id = f"{name}/registration-order"

    def run() -> tuple:
        data = _bundle(name)
        shuffled = copy.deepcopy(data["catalog"])
        for table in shuffled["declarations"]:
            shuffled["declarations"][table].reverse()
        config = _config_for(name)
        base = _manifest_from(name, config=config)
        other = _manifest_from(name, catalog=shuffled, config=config)
        problems = []
        if base.dictionary_version != other.dictionary_version:
            problems.append(f"dictionary_version {base.dictionary_version!r} != {other.dictionary_version!r}")
        if base.encoding_config_digest != other.encoding_config_digest:
            problems.append("encoding_config_digest 不同")
        for table in DICTIONARY_TABLES:
            if base.dictionaries[table].names != other.dictionaries[table].names:
                problems.append(f"dictionaries.{table} 不同")
        if base.to_wire() != other.to_wire():
            problems.append("完整 payload 不同")
        with tempfile.TemporaryDirectory() as tmp:
            first = save_manifest(base, Path(tmp) / "a.json").read_bytes()
            second = save_manifest(other, Path(tmp) / "b.json").read_bytes()
            if first != second:
                problems.append("保存字节不同")
        return (not problems, "；".join(problems[:6]))

    return [_guard(case_id, run)]


def check_dictionary_order(name: str) -> list[Outcome]:
    """固定名称排序 + 索引 0 保留给补齐 + 显式映射自洽。"""
    case_id = f"{name}/dictionary-order"

    def run() -> tuple:
        manifest = _manifest_from(name)
        problems = []
        for table in DICTIONARY_TABLES:
            dictionary = manifest.dictionaries[table]
            ordered = sorted(dictionary.names, key=lambda item: item.encode("utf-8"))
            if list(dictionary.names) != ordered:
                problems.append(f"{table}: names 未按 UTF-8 字节序")
            if dictionary.name_of(0) is not None:
                problems.append(f"{table}: 索引 0 被占用")
            if dictionary.index_of("fixture:__absent__") is not None:
                problems.append(f"{table}: 未注册名称被映射到索引")
            for position, item in enumerate(dictionary.names):
                if dictionary.index_of(item) != position + 1:
                    problems.append(f"{table}: index_of({item}) != {position + 1}")
                if dictionary.name_of(position + 1) != item:
                    problems.append(f"{table}: name_of({position + 1}) != {item}")
        return (not problems, "；".join(problems[:6]))

    return [_guard(case_id, run)]


def check_excluded_tables(name: str) -> list[Outcome]:
    """牌堆类型/终局原因不进入特征字典（牌堆语义经实体 kind_id 保留）。"""
    case_id = f"{name}/excluded-declaration-tables"

    def run() -> tuple:
        manifest = _manifest_from(name)
        present = [table for table in EXCLUDED_DECLARATION_TABLES if table in manifest.dictionaries]
        return (not present, f"不应出现在字典中的表: {present}")

    return [_guard(case_id, run)]


def check_indices(name: str) -> list[Outcome]:
    """手写索引期望逐表比对（编码场景 1/2 的字典侧）。"""
    expected = _expected()["index_expectations"][name]
    manifest = _manifest_from(name)
    outcomes = []
    for table, wanted in expected.items():
        got = manifest.dictionaries[table].to_wire()["by_name"]
        outcomes.append(
            _outcome(
                f"{name}/indices/{table}",
                got == wanted,
                "" if got == wanted else f"得到 {got!r}，期望 {wanted!r}",
            )
        )
    return outcomes


def check_field_records(name: str) -> list[Outcome]:
    """字段记录继承目录声明的逐键期望。"""
    expected = _expected()["field_record_expectations"].get(name, {})
    manifest = _manifest_from(name)
    outcomes = []
    for field_id, wanted in expected.items():
        record = manifest.field_records.get(field_id)
        got = None if record is None else record.to_wire()
        outcomes.append(
            _outcome(
                f"{name}/field-record/{field_id}",
                got == wanted,
                "" if got == wanted else f"得到 {got!r}，期望 {wanted!r}",
            )
        )
    return outcomes


def check_transform_cases(name: str) -> list[Outcome]:
    """数值变换：源十进制 → float64 的手写期望。"""
    cases = [case for case in _expected()["transform_cases"] if case["bundle"] == name]
    manifest = _manifest_from(name)
    outcomes = []
    for case in cases:
        case_id = f"{name}/transform/{case['case_id']}"

        def run(case=case) -> tuple:
            got = manifest.encode_number(case["field_id"], case["raw"])
            return (got == case["expected"], f"得到 {got!r}，期望 {case['expected']!r}")

        outcomes.append(_guard(case_id, run))
    return outcomes


def check_save_load() -> list[Outcome]:
    """保存→加载必须逐键等价，且字节可复现；expected_identity 参与加载校验。"""
    outcomes = []
    manifest = _manifest_from("combat")

    def roundtrip() -> tuple:
        with tempfile.TemporaryDirectory() as tmp:
            path = save_manifest(manifest, Path(tmp) / "manifest.json")
            first = path.read_bytes()
            reloaded = load_manifest(path)
            save_manifest(reloaded, path)
            return (
                reloaded.to_wire() == manifest.to_wire() and path.read_bytes() == first,
                "视图/字节往返不一致",
            )

    outcomes.append(_guard("combat/save-load-roundtrip", roundtrip))

    def identity_ok() -> tuple:
        with tempfile.TemporaryDirectory() as tmp:
            path = save_manifest(manifest, Path(tmp) / "manifest.json")
            load_manifest(path, expected_identity=_identity_of("combat"))
            return (True, "")

    outcomes.append(_guard("combat/load-with-expected-identity", identity_ok))

    def identity_mismatch() -> tuple:
        with tempfile.TemporaryDirectory() as tmp:
            path = save_manifest(manifest, Path(tmp) / "manifest.json")
            try:
                load_manifest(path, expected_identity=_identity_of("card_choice"))
            except EncodingError as exc:
                return (exc.code == "version_mismatch", f"错误码 {exc.code!r} != version_mismatch")
            return (False, "未拒绝身份不一致的字典")

    outcomes.append(_guard("combat/load-expected-identity-mismatch", identity_mismatch))
    return outcomes


def check_identity() -> list[Outcome]:
    """exact identity：四键任一不同都必须拒绝（不降级、不做前缀匹配）。"""
    manifest = _manifest_from("combat")
    snapshot = _bundle("combat")["snapshots"]["snapshot"]
    outcomes = []

    def match() -> tuple:
        manifest.assert_snapshot_identity(snapshot)
        return (True, "")

    outcomes.append(_guard("combat/identity-match", match))

    for key, value in (
        ("protocol_version", "0.1"),
        ("ruleset_id", "fixture:rules-v2"),
        ("catalog_id", "fixture:choice-catalog-v1"),
        ("backend_profile_id", "fixture:other-backend"),
    ):
        variant = copy.deepcopy(snapshot)
        variant[key] = value
        outcomes.append(
            _expect_error(
                f"combat/identity-mismatch-{key}",
                "version_mismatch",
                lambda variant=variant: manifest.assert_snapshot_identity(variant),
            )
        )
    return outcomes


def check_new_content_old_dictionary() -> list[Outcome]:
    """编码场景 2：新增内容生成新版本；旧文件保持原映射；混用报 version_mismatch。"""
    manifest = _manifest_from("combat")
    new_catalog = copy.deepcopy(_bundle("combat")["catalog"])
    new_catalog["catalog_id"] = "fixture:combat-catalog-v2"
    new_catalog["declarations"]["entity_kinds"].append(
        {"id": "fixture:relic", "semantic_version": "1", "description": "新增实体种类（新版本字典）"}
    )
    new_manifest = _manifest_from("combat", catalog=new_catalog)
    outcomes = []

    def new_version() -> tuple:
        problems = []
        if new_manifest.dictionary_version == manifest.dictionary_version:
            problems.append("新增内容后 dictionary_version 未变")
        if new_manifest.environment_identity.catalog_id == manifest.environment_identity.catalog_id:
            problems.append("新目录身份未随之变化")
        if new_manifest.entity_kind_index("fixture:relic") != 8:
            problems.append(f"新种类索引应为 8（按名称排序），得到 {new_manifest.entity_kind_index('fixture:relic')}")
        if manifest.index_of("entity_kinds", "fixture:relic") is not None:
            problems.append("旧字典出现了新种类")
        return (not problems, "；".join(problems))

    outcomes.append(_guard("combat/new-content-new-version", new_version))
    outcomes.append(
        _expect_error(
            "combat/new-content-vs-old-dictionary",
            "version_mismatch",
            lambda: manifest.assert_catalog(new_catalog),
        )
    )

    def old_mapping_kept() -> tuple:
        with tempfile.TemporaryDirectory() as tmp:
            path = save_manifest(manifest, Path(tmp) / "manifest.json")
            reloaded = load_manifest(path)
            return (
                reloaded.to_wire() == manifest.to_wire()
                and reloaded.dictionary_version == manifest.dictionary_version,
                "旧文件加载后映射/版本被改变",
            )

    outcomes.append(_guard("combat/old-file-keeps-mapping", old_mapping_kept))
    return outcomes


def check_legacy_file() -> list[Outcome]:
    """手写旧字典文件：可照常加载、保留原映射、内容更旧仍与旧目录配对。"""
    expected = _expected()["legacy"]
    outcomes: list[Outcome] = []

    def frozen(field: str, extract: Callable[[GymManifest], Any]) -> None:
        wanted = expected[field]

        def run() -> tuple:
            got = extract(_legacy_manifest())
            return (got == wanted, f"得到 {got!r}，期望 {wanted!r}")

        outcomes.append(_guard(f"legacy/{field.replace('.', '-')}", run))

    frozen("dictionary_version", lambda m: m.dictionary_version)
    frozen("source_catalog_digest", lambda m: m.source_catalog_digest)
    frozen("encoding_config_digest", lambda m: m.encoding_config_digest)
    frozen("manifest_digest", lambda m: m.manifest_digest)
    frozen("environment_identity", lambda m: m.environment_identity.to_wire())
    frozen("indices", lambda m: _dict_maps(m))
    frozen("transforms", lambda m: {key: value.to_wire() for key, value in m.encoding_config.transforms.items()})
    frozen("capacities", lambda m: m.encoding_config.capacities.to_wire())

    def tolerances() -> tuple:
        got = {
            "absolute_tolerance": _legacy_manifest().encoding_config.absolute_tolerance,
            "relative_tolerance": _legacy_manifest().encoding_config.relative_tolerance,
        }
        wanted = {
            "absolute_tolerance": expected["absolute_tolerance"],
            "relative_tolerance": expected["relative_tolerance"],
        }
        return (got == wanted, f"得到 {got!r}，期望 {wanted!r}")

    outcomes.append(_guard("legacy/tolerances-explicit", tolerances))

    for case in expected["encode_number"]:
        case_id = f"legacy/encode-number/{case['case_id']}"

        def run(case=case) -> tuple:
            got = _legacy_manifest().encode_number(case["field_id"], case["raw"])
            return (got == case["expected"], f"得到 {got!r}，期望 {case['expected']!r}")

        outcomes.append(_guard(case_id, run))

    category = expected["category"]

    def category_index() -> tuple:
        got = _legacy_manifest().category_index(category["field_id"], category["category_id"])
        return (got == category["expected"], f"得到 {got!r}，期望 {category['expected']!r}")

    outcomes.append(_guard("legacy/category-index", category_index))

    def pairs_with_legacy_catalog() -> tuple:
        manifest = _legacy_manifest()
        manifest.assert_catalog(_read_json(MANIFEST_ROOT / expected["catalog"]))
        return (manifest.matches_catalog(_read_json(MANIFEST_ROOT / expected["catalog"])), "旧目录未与旧字典配对")

    outcomes.append(_guard("legacy/matches-legacy-catalog", pairs_with_legacy_catalog))

    def does_not_match_current() -> tuple:
        manifest = _legacy_manifest()
        mismatched = [
            name for name in BUNDLES if manifest.matches_catalog(_bundle(name)["catalog"])
        ]
        return (not mismatched, f"旧字典不应匹配当前目录: {mismatched}")

    outcomes.append(_guard("legacy/never-matches-current-catalogs", does_not_match_current))

    def resave_bytes() -> tuple:
        """手写文件是**可读的漂亮打印**，因此不与重存字节直接比较；
        比较的是"加载后的视图 == 文件文档"以及"重存字节 == 共享 codec 的规范字节"。"""
        source = MANIFEST_ROOT / expected["manifest"]
        source_doc = json.loads(source.read_bytes().decode("utf-8"))
        manifest = _legacy_manifest()
        with tempfile.TemporaryDirectory() as tmp:
            saved = save_manifest(manifest, Path(tmp) / "manifest.json").read_bytes()
        problems = []
        if layers.canonical_bytes(manifest.to_wire()) != layers.canonical_bytes(source_doc):
            problems.append("加载后的视图与手写文件文档不等价")
        if saved != layers.canonical_bytes(source_doc):
            problems.append("重存字节不是共享 codec 的规范字节形式")
        return (not problems, "；".join(problems))

    outcomes.append(_guard("legacy/save-matches-canonical-form", resave_bytes))
    return outcomes


def check_manifest_file_errors() -> list[Outcome]:
    """文件级负例：格式版本、摘要、内部自洽、L0 解析。"""
    base = _manifest_from("combat").to_wire()
    outcomes: list[Outcome] = []

    def mutated(mutate: Callable[[dict], None]) -> dict:
        doc = copy.deepcopy(base)
        mutate(doc)
        return doc

    def too_new(doc: dict) -> None:
        doc["manifest_format_version"] = 999

    def zero_version(doc: dict) -> None:
        doc["manifest_format_version"] = 0

    def bad_digest(doc: dict) -> None:
        doc["manifest_digest"] = "sha256:" + "0" * 64

    def bad_by_name(doc: dict) -> None:
        doc["dictionaries"]["fields"]["by_name"]["fixture:cost"] = 999

    def extra_key(doc: dict) -> None:
        doc["unexpected"] = 1

    def missing_key(doc: dict) -> None:
        doc.pop("scenario_profile")

    def bad_config_digest(doc: dict) -> None:
        doc["encoding_config_digest"] = "sha256:" + "1" * 64

    def unknown_table(doc: dict) -> None:
        doc["dictionaries"]["terminal_reasons"] = {"names": [], "by_name": {}}

    def record_index_mismatch(doc: dict) -> None:
        doc["field_records"]["fixture:cost"]["index"] = 18

    def category_not_declared(doc: dict) -> None:
        doc["field_records"]["fixture:card_face"]["category_ids"].append("fixture:not_declared")

    def bad_value_type(doc: dict) -> None:
        doc["field_records"]["fixture:cost"]["value_type"] = "string"

    def run_error(case_id: str, code: str, doc: dict) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = _write_temp(doc, tmp)
            outcomes.append(_expect_error(case_id, code, lambda: load_manifest(path)))

    run_error("file/format-too-new", "version_mismatch", mutated(too_new))
    run_error("file/format-zero", "invalid_manifest", mutated(zero_version))
    run_error("file/digest-mismatch", "invalid_manifest", mutated(bad_digest))
    run_error("file/by-name-inconsistent", "invalid_manifest", mutated(bad_by_name))
    run_error("file/unknown-top-level-key", "invalid_manifest", mutated(extra_key))
    run_error("file/missing-required-key", "invalid_manifest", mutated(missing_key))
    run_error("file/config-digest-mismatch", "invalid_manifest", mutated(bad_config_digest))
    run_error("file/unknown-dictionary-table", "invalid_manifest", mutated(unknown_table))
    run_error("file/field-record-index-mismatch", "invalid_manifest", mutated(record_index_mismatch))
    run_error("file/category-not-in-dictionary", "invalid_manifest", mutated(category_not_declared))
    run_error("file/unknown-value-type", "invalid_manifest", mutated(bad_value_type))

    def layout_version() -> tuple:
        doc = mutated(lambda item: item.__setitem__("tensor_layout_version", "ai-tensor-layout-v0"))
        doc = _resign(doc)
        with tempfile.TemporaryDirectory() as tmp:
            path = _write_temp(doc, tmp)
            loaded = load_manifest(path)
            try:
                loaded.require_current_layout()
            except EncodingError as exc:
                return (exc.code == "version_mismatch", f"错误码 {exc.code!r} != version_mismatch")
            return (False, "旧布局未在当前布局要求下被拒绝")

    outcomes.append(_guard("file/old-layout-loads-but-incompatible", layout_version))

    def l0_duplicate_key() -> tuple:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "manifest.json"
            text = json.dumps(base, ensure_ascii=False)
            path.write_text('{"gym_id": "dup",' + text[1:], encoding="utf-8")
            try:
                load_manifest(path)
            except EncodingError as exc:
                ok = exc.code == "invalid_manifest" and "I-24" in exc.related_ids
                return (ok, f"得到 {exc.code!r} related_ids={exc.related_ids}")
            return (False, "重复键文件未被拒绝")

    outcomes.append(_guard("file/l0-duplicate-key", l0_duplicate_key))
    return outcomes


def check_config() -> list[Outcome]:
    """EncodingConfig：默认值、显式持久化、非法配置拒绝。"""
    outcomes: list[Outcome] = []

    def defaults() -> tuple:
        config = EncodingConfig()
        problems = []
        if config.transforms:
            problems.append("默认不该有变换")
        if config.capacities.configured_keys:
            problems.append(f"默认不该设容量上限: {config.capacities.configured_keys}")
        if EncodingConfig().digest() != config.digest():
            problems.append("相同配置摘要不同")
        if config.transform_for("fixture:absent") != NumericTransform():
            problems.append("未声明字段的变换不是恒等")
        return (not problems, "；".join(problems))

    outcomes.append(_guard("config/defaults-are-explicit", defaults))

    def persisted() -> tuple:
        config = _config_for("combat")
        manifest = _manifest_from("combat", config=config)
        with tempfile.TemporaryDirectory() as tmp:
            path = save_manifest(manifest, Path(tmp) / "manifest.json")
            loaded = load_manifest(path)
            problems = []
            if loaded.encoding_config.to_wire() != config.to_wire():
                problems.append("配置往返不一致")
            if loaded.encoding_config_digest != config.digest():
                problems.append("配置摘要不一致")
            if loaded.encoding_config.capacities.to_wire() != config.capacities.to_wire():
                problems.append("容量未持久化")
            return (not problems, "；".join(problems))

    outcomes.append(_guard("config/roundtrip-through-manifest", persisted))

    outcomes.append(
        _expect_error(
            "config/scale-zero",
            "invalid_encoding_config",
            lambda: NumericTransform(scale="0"),
        )
    )
    outcomes.append(
        _expect_error(
            "config/negative-scale",
            "invalid_encoding_config",
            lambda: NumericTransform(scale="-1"),
        )
    )
    outcomes.append(
        _expect_error(
            "config/non-decimal-offset",
            "invalid_encoding_config",
            lambda: NumericTransform(offset="abc"),
        )
    )
    outcomes.append(
        _expect_error(
            "config/capacity-zero",
            "invalid_encoding_config",
            lambda: CapacityLimits(max_entities=0),
        )
    )
    outcomes.append(
        _expect_error(
            "config/capacity-bool",
            "invalid_encoding_config",
            lambda: CapacityLimits(max_candidates=True),
        )
    )
    outcomes.append(
        _expect_error(
            "config/unsupported-dtype",
            "invalid_encoding_config",
            lambda: EncodingConfig(number_channel_dtype="float32"),
        )
    )
    outcomes.append(
        _expect_error(
            "config/transform-on-unregistered-field",
            "unregistered_field",
            lambda: _manifest_from(
                "combat", config=EncodingConfig(transforms={"fixture:absent": NumericTransform()})
            ),
        )
    )
    outcomes.append(
        _expect_error(
            "config/transform-on-non-number-field",
            "value_type_mismatch",
            lambda: _manifest_from(
                "combat",
                config=EncodingConfig(transforms={"fixture:card_face": NumericTransform()}),
            ),
        )
    )
    return outcomes


def check_transform_errors() -> list[Outcome]:
    """数值变换的失败路径：未注册字段、类型不匹配、非有限值、精度损失。"""
    outcomes: list[Outcome] = []
    manifest = _manifest_from("combat")

    outcomes.append(
        _expect_error(
            "transform/unregistered-field",
            "unregistered_field",
            lambda: manifest.encode_number("fixture:absent", "1"),
        )
    )
    outcomes.append(
        _expect_error(
            "transform/value-type-mismatch",
            "value_type_mismatch",
            lambda: manifest.encode_number("fixture:card_face", "fixture:strike"),
        )
    )
    outcomes.append(
        _expect_error(
            "transform/non-finite",
            "non_finite_value",
            lambda: manifest.encode_number("fixture:cost", "1e400"),
        )
    )
    outcomes.append(
        _expect_error(
            "transform/non-decimal-raw",
            "value_type_mismatch",
            lambda: manifest.encode_number("fixture:cost", "not-a-number"),
        )
    )

    strict_manifest = _manifest_from(
        "combat",
        config=EncodingConfig(
            capacities=CapacityLimits(**CAPACITIES),
            absolute_tolerance="1e-30",
            relative_tolerance="1e-30",
        ),
    )
    outcomes.append(
        _expect_error(
            "transform/precision-loss",
            "numeric_precision_loss",
            lambda: strict_manifest.encode_number("fixture:cost", "0.100000000000000000000000000001"),
        )
    )

    def within_default_tolerance() -> tuple:
        got = manifest.encode_number("fixture:cost", "0.100000000000000000000000000001")
        return (got == 0.1, f"默认容差下应得到 0.1，得到 {got!r}")

    outcomes.append(_guard("transform/within-default-tolerance", within_default_tolerance))
    return outcomes


def check_index_lookups() -> list[Outcome]:
    """类别/类型索引的查找语义与失败路径。"""
    outcomes: list[Outcome] = []
    expected = _expected()["index_expectations"]["combat"]["categories"]
    manifest = _manifest_from("combat")

    def category_ok() -> tuple:
        got = manifest.category_index("fixture:card_face", "fixture:strike")
        return (got == expected["fixture:strike"], f"得到 {got!r}，期望 {expected['fixture:strike']!r}")

    outcomes.append(_guard("lookup/category-index", category_ok))

    def typed_lookup() -> tuple:
        problems = []
        if manifest.entity_kind_index("fixture:card") != 1:
            problems.append("entity_kind_index(fixture:card) != 1")
        if manifest.content_type_index("fixture:power_face") != 2:
            problems.append("content_type_index(fixture:power_face) != 2")
        if manifest.field_index("fixture:cost") != 5:
            problems.append("field_index(fixture:cost) != 5")
        if manifest.relation_index("net:pile_member") != 2:
            problems.append("relation_index(net:pile_member) != 2")
        if manifest.action_type_index("fixture:play_card") != 2:
            problems.append("action_type_index(fixture:play_card) != 2")
        if manifest.index_of("entity_kinds", "fixture:absent") is not None:
            problems.append("index_of 未注册名称应返回 None")
        if manifest.dictionaries["entity_kinds"].name_of(0) is not None:
            problems.append("索引 0 必须是补齐位")
        return (not problems, "；".join(problems))

    outcomes.append(_guard("lookup/typed-index", typed_lookup))

    outcomes.append(
        _expect_error(
            "lookup/unknown-entity-kind",
            "unknown_type",
            lambda: manifest.entity_kind_index("fixture:absent"),
        )
    )
    outcomes.append(
        _expect_error(
            "lookup/unknown-category-value",
            "unknown_type",
            lambda: manifest.category_index("fixture:card_face", "fixture:absent"),
        )
    )
    outcomes.append(
        _expect_error(
            "lookup/unregistered-field",
            "unregistered_field",
            lambda: manifest.field_index("fixture:absent"),
        )
    )
    outcomes.append(
        _expect_error(
            "lookup/category-on-non-category-field",
            "value_type_mismatch",
            lambda: manifest.category_index("fixture:cost", "fixture:strike"),
        )
    )
    outcomes.append(
        _expect_error(
            "lookup/unknown-dictionary-table",
            "unknown_type",
            lambda: manifest.index_of("terminal_reasons", "fixture:victory"),
        )
    )

    def duplicate_declaration() -> tuple:
        doc = copy.deepcopy(_bundle("combat")["catalog"])
        doc["declarations"]["entity_kinds"].append(copy.deepcopy(doc["declarations"]["entity_kinds"][0]))
        view = PublicCatalog.from_wire(doc)  # 绕过共享 L2：视图路径仍需自行拒绝重复声明
        try:
            build_manifest(
                view,
                _config_for("combat"),
                backend_profile_id=_expected()["environment"]["backend_profile_id"],
                gym_id=_expected()["environment"]["gym_id"],
            )
        except EncodingError as exc:
            return (exc.code == "duplicate_identity", f"错误码 {exc.code!r} != duplicate_identity")
        return (False, "重复声明未被拒绝")

    outcomes.append(_guard("lookup/duplicate-declaration", duplicate_declaration))
    return outcomes


# --------------------------------------------------------------------------- #
# l0-text 组
# --------------------------------------------------------------------------- #
def _fixture_documents() -> list[Path]:
    """接受向量：手写目录/快照/覆盖表 + 手写旧目录与旧字典文件。"""
    paths: list[Path] = []
    for name in BUNDLES:
        root = FIXTURE_ROOT / name
        paths.append(root / "catalog.json")
        if (root / "coverage.json").exists():
            paths.append(root / "coverage.json")
        for stem in BUNDLES[name]:
            paths.append(root / f"{stem}.json")
    paths.append(MANIFEST_ROOT / "legacy" / "catalog.json")
    paths.append(MANIFEST_ROOT / "legacy" / "manifest-v1.json")
    return paths


def check_text_parse() -> list[Outcome]:
    """L0 严格文本解析：坏字节向量必须命中独立写出的规则 ID；合法文件必须接受。"""
    expected = _read_json(TEXT_ROOT / "expected.json")
    outcomes: list[Outcome] = []

    for case in expected["reject"]:
        path = TEXT_ROOT / case["file"]

        def run(case=case, path=path) -> tuple:
            document, findings = layers.parse_wire_text(path.read_bytes(), what=path.name)
            got = [finding.rule_id for finding in findings]
            ok = document is None and case["rule_id"] in got
            detail = "" if ok else f"得到 findings={got}，document={document!r}，期望拒绝规则 {case['rule_id']}"
            return (ok, detail)

        outcomes.append(
            Outcome("l0-text", case["case_id"], *(_guard_detail("l0-text", case["case_id"], run)))
        )

    for path in _fixture_documents():
        relative = path.relative_to(FIXTURE_ROOT).as_posix()

        def run(path=path) -> tuple:
            raw = path.read_bytes()
            document, findings = layers.parse_wire_text(raw, what=path.name)
            reference = json.loads(raw.decode("utf-8"))
            ok = not findings and document == reference
            detail = "" if ok else f"findings={[finding.render() for finding in findings]}"
            return (ok, detail)

        outcomes.append(
            Outcome("l0-text", f"accept/{relative}", *(_guard_detail("l0-text", f"accept/{relative}", run)))
        )
    return outcomes


def _guard_detail(suite: str, case_id: str, run: Callable[[], tuple]) -> tuple:
    try:
        ok, detail = run()
    except (EncodingError, ContractViolation, AssertionError) as exc:
        return False, f"{type(exc).__name__}: {exc}"
    return bool(ok), detail


# --------------------------------------------------------------------------- #
# 入口
# --------------------------------------------------------------------------- #
def run_all(only: Optional[str] = None) -> list[Outcome]:
    outcomes: list[Outcome] = []
    for name in BUNDLES:
        outcomes.extend(check_registration_order(name))
        outcomes.extend(check_dictionary_order(name))
        outcomes.extend(check_excluded_tables(name))
        outcomes.extend(check_indices(name))
        outcomes.extend(check_field_records(name))
        outcomes.extend(check_transform_cases(name))
    outcomes.extend(check_save_load())
    outcomes.extend(check_identity())
    outcomes.extend(check_new_content_old_dictionary())
    outcomes.extend(check_legacy_file())
    outcomes.extend(check_manifest_file_errors())
    outcomes.extend(check_config())
    outcomes.extend(check_transform_errors())
    outcomes.extend(check_index_lookups())
    outcomes.extend(check_text_parse())
    if only:
        outcomes = [item for item in outcomes if item.suite == only]
    return outcomes


__all__ = [
    "CAPACITIES",
    "MANIFEST_ROOT",
    "TEXT_ROOT",
    "TRANSFORMS",
    "check_text_parse",
    "run_all",
]
