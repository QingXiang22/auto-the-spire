#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""W-04 检查：共享小模型的**结构、掩码、不变性、策略契约与模型包**。

分组（§`run_checks.py` 的 ``SUITE_ORDER``）：

* ``model-shape``：显式维度 + 参数分项 + 梯度无关的结构身份（配置校验、摘要稳定）；
* ``model-mask``：仅有效候选获得概率、掩码候选恒 0、补齐行不参与聚合；
* ``model-invariance``：候选重排/句柄一致换名/关系行重排**不改变**同一语义键的概率与 V；
* ``model-policy``：终止/无候选**不伪造**概率；与 `EpisodeRunner(policy=...)` 的概率覆盖一致；
* ``model-bundle``：保存/载入往返一致；身份不一致/权重损坏/元数据缺口**显式失败**。

**证据边界**：本套件证明的是**结构与契约**，不是学习效果——模型是随机初始化的；
梯度/更新/恢复需要学习框架（待维护者批准），见 `docs/review/tofix` 的 W-04 交接。
"""

from __future__ import annotations

import json
import math
import pathlib
import shutil
import sys
import tempfile
from typing import Any, Callable, Optional

_PACKAGE_ROOT = pathlib.Path(__file__).resolve().parents[1]
if str(_PACKAGE_ROOT) not in sys.path:
    sys.path.insert(0, str(_PACKAGE_ROOT))
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from ai_the_spire.encoding import tensorize  # noqa: E402
from ai_the_spire.models import (  # noqa: E402
    ModelBundleError,
    ModelConfig,
    ModelConfigError,
    PolicyError,
    SmallPolicyModel,
    build_model_input,
    load_bundle,
    measure_inference_cost,
    parameter_report,
    predict,
    save_bundle,
)
from ai_the_spire.models.arrays import pad_model_input  # noqa: E402
from ai_the_spire.models.autodiff import AutodiffError  # noqa: E402
from ai_the_spire.models.policy import semantic_keys_of  # noqa: E402

import tensor_suite  # noqa: E402
from fixture_suite import Outcome  # noqa: E402

SUITE_SHAPE = "model-shape"
SUITE_MASK = "model-mask"
SUITE_INVARIANCE = "model-invariance"
SUITE_POLICY = "model-policy"
SUITE_BUNDLE = "model-bundle"

SUITE_NAMES = (SUITE_SHAPE, SUITE_MASK, SUITE_INVARIANCE, SUITE_POLICY, SUITE_BUNDLE)

#: 主用夹具：combat 有实体/字段/关系/牌堆/多候选，足以覆盖候选引用实体的路径。
BUNDLE = "combat"


def _outcome(suite: str, case_id: str, ok: bool, detail: str = "") -> Outcome:
    return Outcome(suite, case_id, bool(ok), detail)


def _guard(suite: str, case_id: str, run: Callable[[], Any]) -> Outcome:
    try:
        result = run()
    except Exception as exc:  # noqa: BLE001 - 单条失败不中止整套，差异必须落进 detail
        return _outcome(suite, case_id, False, f"{type(exc).__name__}: {exc}")
    if isinstance(result, Outcome):
        return result
    ok, detail = result
    return _outcome(suite, case_id, ok, detail)


class _Context:
    """懒加载的夹具上下文：同一份 Manifest/张量只构建一次。"""

    def __init__(self) -> None:
        self._manifest = None
        self._catalog = None
        self._models: dict = {}

    def manifest(self):
        if self._manifest is None:
            self._manifest = tensor_suite.manifest_for(BUNDLE)
        return self._manifest

    def catalog(self):
        if self._catalog is None:
            self._catalog = tensor_suite.catalog(BUNDLE)
        return self._catalog

    def snapshot(self, stem: str = "snapshot") -> dict:
        return tensor_suite.snapshot(BUNDLE, stem)

    def observation(self, stem: str = "snapshot", document: Optional[dict] = None):
        return tensorize(document if document is not None else self.snapshot(stem),
                         self.manifest(), catalog=self.catalog())

    def model(self, **overrides) -> SmallPolicyModel:
        key = json.dumps(overrides, sort_keys=True)
        if key not in self._models:
            config = ModelConfig(**overrides) if overrides else ModelConfig()
            self._models[key] = SmallPolicyModel(config, self.manifest(), catalog=self.catalog())
        return self._models[key]


# --------------------------------------------------------------------------- #
# model-shape
# --------------------------------------------------------------------------- #
def _shape_cases(context: _Context) -> list:
    def default_config_is_explicit() -> tuple:
        config = ModelConfig()
        document = config.to_wire()
        expected = {
            "model_config_version", "d_entity_kind", "d_content_type", "d_content_state", "d_field",
            "d_value_type", "d_category", "d_relation_type", "d_action_type", "d_entity",
            "d_field_hidden", "d_candidate", "d_global", "d_value_hidden", "propagation_rounds",
            "seed", "dtype", "number_scale", "boolean_scale",
        }
        return set(document) == expected, f"字段集合 {sorted(document)}"

    def config_roundtrip_is_exact() -> tuple:
        config = ModelConfig(d_entity=12, propagation_rounds=2)
        restored = ModelConfig.from_wire(config.to_wire())
        return (restored == config and restored.digest() == config.digest(),
                f"{restored}")

    def config_rejects_bad_dimensions() -> tuple:
        bad = (
            ({"d_entity": 0}, "正整数"),
            ({"d_entity": -1}, "正整数"),
            ({"propagation_rounds": 9}, "propagation_rounds"),
            ({"propagation_rounds": -1}, "propagation_rounds"),
            ({"dtype": "float16"}, "dtype"),
            ({"seed": -1}, "seed"),
        )
        for overrides, marker in bad:
            try:
                ModelConfig(**overrides)
            except ModelConfigError as failure:
                if marker not in str(failure):
                    return False, f"{overrides} 的错误信息未点名 {marker}: {failure}"
            else:
                return False, f"{overrides} 必须被拒绝"
        return True, ""

    def config_version_mismatch_rejected() -> tuple:
        document = ModelConfig().to_wire()
        document["model_config_version"] = "model-config@999"
        try:
            ModelConfig.from_wire(document)
        except ModelConfigError as failure:
            return "版本" in str(failure), f"{failure}"
        return False, "版本不一致必须被拒绝"

    def parameter_breakdown_sums_to_total() -> tuple:
        report = parameter_report(context.model())
        total = sum(report["parameters"].values())
        groups = set(report["parameters"])
        wanted = {"emb", "entity", "field", "relation", "global", "candidate", "value"}
        return (total == report["parameter_total"] and groups == wanted,
                f"total={report['parameter_total']} 分项和={total} 组={sorted(groups)}")

    def embedding_size_follows_dictionary() -> tuple:
        model = context.model()
        sizes = {name: context.manifest().dictionaries[name].size for name in context.manifest().dictionaries}
        kind_params = sizes["entity_kinds"] * model.config.d_entity_kind
        action_params = sizes["action_types"] * model.config.d_action_type
        report = parameter_report(model)
        # 参数分项直接来自字典规模（增字段/内容 = 新 Gym 配置，不热加载进旧 embedding）。
        expected_min = kind_params + action_params
        return (report["parameters"]["emb"] >= expected_min,
                f"emb={report['parameters']['emb']} < {expected_min}")

    def dictionary_embeddings_reserve_the_pad_row() -> tuple:
        """字典索引 0 是补齐（`Dictionary.index_of` 返回 位置+1）⇒ embedding 必须是 size + 1 行。"""
        model = context.model()
        manifest = context.manifest()
        checked = []
        for table, name in (
            ("entity_kinds", "emb/entity_kind"),
            ("content_types", "emb/content_type"),
            ("fields", "emb/field"),
            ("categories", "emb/category"),
            ("relation_types", "emb/relation_type"),
            ("action_types", "emb/action_type"),
        ):
            rows = int(model.scorer.params[name].shape[0])
            expected = manifest.dictionaries[table].size + 1
            checked.append((name, rows, expected))
            if rows != expected:
                return False, f"{name} 行数 {rows} != size+1 {expected}"
        return True, f"{checked[:3]}"

    def cost_report_separates_stages() -> tuple:
        model = context.model()
        report = measure_inference_cost(model, [context.observation()], repeats=2)
        forward = report["forward"]
        return (forward is not None and forward["n"] == 2 and forward["p50_ms"] >= 0.0
                and report["peak_bytes_first_call"] > 0,
                f"{report}")

    return [
        _guard(SUITE_SHAPE, "shape/config-fields-are-explicit", default_config_is_explicit),
        _guard(SUITE_SHAPE, "shape/config-roundtrip-and-digest", config_roundtrip_is_exact),
        _guard(SUITE_SHAPE, "shape/config-rejects-invalid", config_rejects_bad_dimensions),
        _guard(SUITE_SHAPE, "shape/config-version-mismatch", config_version_mismatch_rejected),
        _guard(SUITE_SHAPE, "shape/parameter-breakdown-sums", parameter_breakdown_sums_to_total),
        _guard(SUITE_SHAPE, "shape/embedding-follows-dictionary", embedding_size_follows_dictionary),
        _guard(SUITE_SHAPE, "shape/dictionary-embeddings-reserve-pad-row",
               dictionary_embeddings_reserve_the_pad_row),
        _guard(SUITE_SHAPE, "shape/cost-separates-encode-and-forward", cost_report_separates_stages),
    ]


# --------------------------------------------------------------------------- #
# model-mask
# --------------------------------------------------------------------------- #
def _mask_cases(context: _Context) -> list:
    def probabilities_only_on_valid_candidates() -> tuple:
        model = context.model()
        observation = context.observation()
        evaluation = model.predict(context.snapshot())
        rows = observation.block("candidates").n
        total = sum(evaluation.probabilities)
        return (len(evaluation.probabilities) == rows
                and evaluation.valid_candidates == rows
                and abs(total - 1.0) < 1e-12,
                f"rows={rows} probs={evaluation.probabilities} sum={total}")

    def padded_candidate_gets_zero_probability() -> tuple:
        model = context.model()
        observation = context.observation()
        real = build_model_input(observation, context.manifest(), config=model.config)
        padded = pad_model_input(real, entities=real.entity_count, candidates=real.candidate_count + 3)
        evaluation = model.evaluate_tensorized(
            observation, semantic_keys=semantic_keys_of(context.snapshot())
        )
        # 直接用补齐后的输入求值：多出的候选行必须拿到 0 概率，且真实行的概率不变。
        scored = model.scorer.score(padded, model.scorer.encoder.forward(padded))
        probabilities = model.scorer.probabilities(scored)
        padded_tail = probabilities[real.candidate_count:]
        return (all(value == 0.0 for value in padded_tail)
                and max(abs(a - b) for a, b in zip(probabilities[:real.candidate_count],
                                                  evaluation.probabilities)) < 1e-12,
                f"tail={padded_tail}")

    def all_masked_candidates_rejected() -> tuple:
        model = context.model()
        observation = context.observation()
        real = build_model_input(observation, context.manifest(), config=model.config)
        blocked = _replace(real, candidate_mask=[False] * real.candidate_count)
        try:
            scored = model.scorer.score(blocked, model.scorer.encoder.forward(blocked))
            model.scorer.probabilities(scored)
        except (ValueError, AutodiffError) as failure:
            return "没有有效候选" in str(failure), f"{failure}"
        return False, "全掩码候选必须被拒绝，不得产出概率向量"

    def masked_entities_do_not_contribute() -> tuple:
        model = context.model()
        observation = context.observation()
        real = build_model_input(observation, context.manifest(), config=model.config)
        if real.entity_count < 2:
            return True, "夹具实体不足两个，跳过"
        masked = _replace(real, entity_mask=[False] + [True] * (real.entity_count - 1))
        base = model.scorer.encoder.forward(real)
        changed = model.scorer.encoder.forward(masked)
        differ = float(abs(base.global_vector - changed.global_vector).max())
        return differ > 0.0, "屏蔽一个实体后全局向量必须改变（否则掩码没生效）"

    def value_is_a_single_scalar() -> tuple:
        model = context.model()
        evaluation = model.predict(context.snapshot())
        return (math.isfinite(evaluation.value)
                and not isinstance(evaluation.value, (list, tuple)),
                f"value={evaluation.value!r}")

    return [
        _guard(SUITE_MASK, "mask/probabilities-cover-valid-candidates",
               probabilities_only_on_valid_candidates),
        _guard(SUITE_MASK, "mask/padded-candidate-gets-zero", padded_candidate_gets_zero_probability),
        _guard(SUITE_MASK, "mask/all-masked-candidates-rejected", all_masked_candidates_rejected),
        _guard(SUITE_MASK, "mask/masked-entity-not-aggregated", masked_entities_do_not_contribute),
        _guard(SUITE_MASK, "mask/value-is-scalar-not-per-candidate", value_is_a_single_scalar),
    ]


# --------------------------------------------------------------------------- #
# model-invariance
# --------------------------------------------------------------------------- #
def _invariance_cases(context: _Context) -> list:
    def model_input_carries_no_instance_ids() -> tuple:
        """结构不变量：模型输入只有数值/布尔数组 + 字典规模，**没有字符串**。

        匿名实例句柄因此不可能成为特征（引用只以样本内**行号**出现）。
        """
        import dataclasses

        import numpy as np

        model = context.model()
        model_input = build_model_input(context.observation(), context.manifest(), config=model.config)
        for field in dataclasses.fields(model_input):
            value = getattr(model_input, field.name)
            if isinstance(value, np.ndarray):
                if value.dtype.kind not in "iufb":
                    return False, f"{field.name} 的 dtype {value.dtype} 不是数值/布尔"
            elif field.name == "table_sizes":
                if not all(isinstance(item, int) for item in value.values()):
                    return False, "字典规模必须是整数"
            elif field.name != "dtype":
                return False, f"{field.name} 是未预期的类型 {type(value).__name__}"
        return True, ""

    def candidate_permutation_keeps_per_key_probability() -> tuple:
        """候选行重排（连同其语义键映射一起重排）后，**同一语义键**的概率必须不变。"""
        model = context.model()
        document = context.snapshot()
        observation = context.observation()
        keys = semantic_keys_of(document)
        baseline = model.evaluate_tensorized(observation, semantic_keys=keys)
        model_input = build_model_input(observation, context.manifest(), config=model.config)
        order = list(range(model_input.candidate_count))[::-1]
        permuted_input = _replace(
            model_input,
            candidate_action=list(model_input.candidate_action[order]),
            candidate_mask=list(model_input.candidate_mask[order]),
            candidate_field_owner=_remap_owner(model_input.candidate_field_owner, order),
        )
        by_row = {row: candidate_id for candidate_id, row in observation.reference_map.candidate_rows.items()}
        permuted_rows = {by_row[old]: new for new, old in enumerate(order)}
        permuted_evaluation = model._evaluate_model_input(
            permuted_input,
            _RowMap(permuted_rows),
            keys,
        )
        left = baseline.by_semantic_key()
        right = permuted_evaluation.by_semantic_key()
        if set(left) != set(right):
            return False, "重排后语义键集合变化"
        worst = max(abs(left[key] - right[key]) for key in left)
        return worst < 1e-12, f"重排后同一语义键最大概率差 {worst}"

    def handle_strings_do_not_influence_the_forward_pass() -> tuple:
        """把语义键换成任意别名：概率（按位置）与 V 必须逐字不变——句柄不是特征。"""
        model = context.model()
        document = context.snapshot()
        observation = context.observation()
        keys = semantic_keys_of(document)
        baseline = model.evaluate_tensorized(observation, semantic_keys=keys)
        aliased = {candidate_id: f"alias-{index}" for index, candidate_id in enumerate(keys)}
        after = model.evaluate_tensorized(observation, semantic_keys=aliased)
        worst = max(abs(a - b) for a, b in zip(baseline.probabilities, after.probabilities))
        return (worst < 1e-12 and abs(baseline.value - after.value) < 1e-12,
                f"换名后概率最大差 {worst}")

    def non_canonical_entity_order_is_rejected_by_the_encoder() -> tuple:
        """非规范输入（实体未按 instance_id 排序）在校验层就被拒绝 ⇒ 模型不依赖输入顺序。"""
        from ai_the_spire.encoding import EncodingError

        model = context.model()
        document = context.snapshot()
        shuffled = dict(document)
        shuffled["entities"] = list(reversed(document["entities"]))
        try:
            model.predict(shuffled)
        except EncodingError as failure:
            return ("I-74" in str(failure), f"{failure}")
        except Exception as failure:  # noqa: BLE001
            return False, f"{type(failure).__name__}: {failure}"
        return False, "非规范实体顺序必须被校验层拒绝，而不是被模型悄悄接受"

    def relation_row_reorder_keeps_output() -> tuple:
        model = context.model()
        observation = context.observation()
        base_model_input = build_model_input(observation, context.manifest(), config=model.config)
        if base_model_input.relation_type.shape[0] < 2:
            return True, "夹具关系少于两行，跳过"
        order = list(range(base_model_input.relation_type.shape[0]))[::-1]
        permuted = _replace(
            base_model_input,
            relation_type=list(base_model_input.relation_type[order]),
            relation_source=list(base_model_input.relation_source[order]),
            relation_target=list(base_model_input.relation_target[order]),
            relation_mask=list(base_model_input.relation_mask[order]),
        )
        base = model.scorer.encoder.forward(base_model_input)
        other = model.scorer.encoder.forward(permuted)
        worst = float(abs(base.global_vector - other.global_vector).max())
        return worst < 1e-12, f"关系行重排后全局向量最大差 {worst}"

    def same_config_same_seed_reproduces_weights() -> tuple:
        left = context.model()
        right = SmallPolicyModel(ModelConfig(), context.manifest(), catalog=context.catalog())
        return (left.weights_digest == right.weights_digest
                and left.model_id == right.model_id,
                f"{left.weights_digest} vs {right.weights_digest}")

    def different_config_changes_identity() -> tuple:
        base = context.model()
        other = context.model(d_entity=24)
        return (base.model_id != other.model_id
                and base.weights_digest != other.weights_digest,
                "结构不同的模型必须有不同身份")

    return [
        _guard(SUITE_INVARIANCE, "invariance/model-input-has-no-instance-ids",
               model_input_carries_no_instance_ids),
        _guard(SUITE_INVARIANCE, "invariance/candidate-permutation-keeps-per-key-probability",
               candidate_permutation_keeps_per_key_probability),
        _guard(SUITE_INVARIANCE, "invariance/handle-strings-are-not-features",
               handle_strings_do_not_influence_the_forward_pass),
        _guard(SUITE_INVARIANCE, "invariance/non-canonical-input-rejected",
               non_canonical_entity_order_is_rejected_by_the_encoder),
        _guard(SUITE_INVARIANCE, "invariance/relation-row-reorder", relation_row_reorder_keeps_output),
        _guard(SUITE_INVARIANCE, "invariance/same-seed-reproduces-weights",
               same_config_same_seed_reproduces_weights),
        _guard(SUITE_INVARIANCE, "invariance/different-config-changes-identity",
               different_config_changes_identity),
    ]


class _RowMap:
    """最小引用映射替身：只提供 ``candidate_rows``（重排检查用）。"""

    def __init__(self, candidate_rows: dict) -> None:
        self.candidate_rows = candidate_rows


def _remap_owner(owner_array, order):
    """按候选行重排映射候选字段的 owner 列（旧行号 → 新行号）。"""
    import numpy as np

    inverse = {old: new for new, old in enumerate(order)}
    return np.asarray([inverse[int(value)] for value in owner_array], dtype=owner_array.dtype)


# --------------------------------------------------------------------------- #
# model-policy
# --------------------------------------------------------------------------- #
def _policy_cases(context: _Context) -> list:
    def terminal_snapshot_is_rejected() -> tuple:
        model = context.model()
        # combat 夹具没有终止快照；scenario 夹具有一个（终局且候选为空）。
        manifest = tensor_suite.manifest_for("scenario")
        catalog = tensor_suite.catalog("scenario")
        terminal_model = SmallPolicyModel(model.config, manifest, catalog=catalog)
        terminal = tensor_suite.snapshot("scenario", "snapshot-terminal")
        try:
            predict(terminal, terminal_model, catalog=catalog)
        except PolicyError as failure:
            return "没有候选" in str(failure), f"{failure}"
        except Exception as failure:  # noqa: BLE001 - 其它异常也必须点名
            return False, f"{type(failure).__name__}: {failure}"
        return False, "终止快照必须被拒绝（不伪造概率）"

    def runner_adapter_matches_candidate_set() -> tuple:
        model = context.model()
        document = context.snapshot()
        from ai_the_spire.runner import ordered_candidates

        candidates = ordered_candidates(document)
        probabilities = model.policy(document, candidates)
        return (sorted(probabilities) == sorted(c.semantic_key_text for c in candidates)
                and abs(sum(probabilities.values()) - 1.0) < 1e-12,
                f"keys={len(probabilities)} sum={sum(probabilities.values())}")

    def evaluation_records_identity() -> tuple:
        model = context.model()
        evaluation = model.predict(context.snapshot())
        manifest = context.manifest()
        return (evaluation.model_id == model.model_id
                and evaluation.manifest_digest == manifest.manifest_digest
                and evaluation.dictionary_version == manifest.dictionary_version
                and evaluation.tensor_layout_version == manifest.tensor_layout_version
                and evaluation.model_config_digest == model.config.digest(),
                json.dumps(evaluation.to_wire())[:200])

    def probabilities_are_not_uniform() -> tuple:
        """随机初始化的模型也必须输出**自身**的概率（不是均匀分布冒充模型输出）。"""
        model = context.model()
        evaluation = model.predict(context.snapshot())
        values = evaluation.probabilities
        uniform = 1.0 / len(values)
        return (any(abs(value - uniform) > 1e-9 for value in values),
                f"probs={values}")

    def manifest_mismatch_rejected() -> tuple:
        model = context.model()
        other = tensor_suite.manifest_for("scenario")
        other_model = SmallPolicyModel(model.config, other, catalog=context.catalog())
        observation = context.observation()
        try:
            other_model.evaluate_tensorized(observation,
                                            semantic_keys=semantic_keys_of(context.snapshot()))
        except PolicyError as failure:
            return "另一份 Manifest" in str(failure), f"{failure}"
        return False, "跨 Manifest 的张量必须被拒绝"

    return [
        _guard(SUITE_POLICY, "policy/terminal-has-no-candidates", terminal_snapshot_is_rejected),
        _guard(SUITE_POLICY, "policy/runner-adapter-covers-real-candidates",
               runner_adapter_matches_candidate_set),
        _guard(SUITE_POLICY, "policy/evaluation-records-identity", evaluation_records_identity),
        _guard(SUITE_POLICY, "policy/probabilities-come-from-the-model",
               probabilities_are_not_uniform),
        _guard(SUITE_POLICY, "policy/manifest-mismatch-rejected", manifest_mismatch_rejected),
    ]


# --------------------------------------------------------------------------- #
# model-bundle
# --------------------------------------------------------------------------- #
def _bundle_cases(context: _Context) -> list:
    root = pathlib.Path(tempfile.mkdtemp(prefix="model-bundle-"))

    def roundtrip_reproduces_probabilities() -> tuple:
        model = context.model()
        directory = save_bundle(model, root / "ok")
        restored = load_bundle(directory, manifest=context.manifest())
        left = model.predict(context.snapshot()).probabilities
        right = restored.predict(context.snapshot()).probabilities
        return (max(abs(a - b) for a, b in zip(left, right)) < 1e-12
                and restored.weights_digest == model.weights_digest
                and restored.model_id == model.model_id,
                f"{directory}")

    def metadata_declares_no_training() -> tuple:
        directory = save_bundle(context.model(), root / "metadata")
        metadata = json.loads((pathlib.Path(directory) / "metadata.json").read_text(encoding="utf-8"))
        return (metadata["training"].startswith("none(")
                and metadata["training_profile_id"] is None
                and metadata["executor_profile_id"] is None
                and metadata["value_head"].endswith("no_Q)"),
                json.dumps({k: metadata[k] for k in ("training", "value_head")}))

    def missing_metadata_is_rejected() -> tuple:
        directory = save_bundle(context.model(), root / "missing")
        (pathlib.Path(directory) / "metadata.json").unlink()
        try:
            load_bundle(directory, manifest=context.manifest())
        except ModelBundleError as failure:
            return failure.code == "bundle_incomplete", f"{failure}"
        return False, "缺少 metadata.json 必须被拒绝"

    def unknown_metadata_key_is_rejected() -> tuple:
        directory = save_bundle(context.model(), root / "extra")
        path = pathlib.Path(directory) / "metadata.json"
        document = json.loads(path.read_text(encoding="utf-8"))
        document["extra_key"] = 1
        path.write_text(json.dumps(document, ensure_ascii=False, sort_keys=True), encoding="utf-8")
        try:
            load_bundle(directory, manifest=context.manifest())
        except ModelBundleError as failure:
            return failure.code == "bundle_invalid" and "多出" in str(failure), f"{failure}"
        return False, "多余键必须被拒绝"

    def corrupted_weights_are_rejected() -> tuple:
        directory = save_bundle(context.model(), root / "corrupt")
        path = pathlib.Path(directory) / "weights.npz"
        payload = bytearray(path.read_bytes())
        payload[-1] = (payload[-1] + 1) % 256
        path.write_bytes(bytes(payload))
        try:
            load_bundle(directory, manifest=context.manifest())
        except ModelBundleError as failure:
            return failure.code in {"weights_corrupt", "weights_invalid"}, f"{failure}"
        return False, "被改写的权重必须被拒绝"

    def foreign_manifest_is_rejected() -> tuple:
        directory = save_bundle(context.model(), root / "foreign")
        other = tensor_suite.manifest_for("scenario")
        try:
            load_bundle(directory, manifest=other)
        except ModelBundleError as failure:
            return failure.code == "identity_mismatch", f"{failure}"
        return False, "换 Gym 的 Manifest 载入必须被拒绝"

    def wrong_format_version_is_rejected() -> tuple:
        directory = save_bundle(context.model(), root / "format")
        path = pathlib.Path(directory) / "metadata.json"
        document = json.loads(path.read_text(encoding="utf-8"))
        document["bundle_format_version"] = "model-bundle@999"
        path.write_text(json.dumps(document, ensure_ascii=False, sort_keys=True), encoding="utf-8")
        try:
            load_bundle(directory, manifest=context.manifest())
        except ModelBundleError as failure:
            return failure.code == "version_mismatch", f"{failure}"
        return False, "格式版本不一致必须被拒绝"

    try:
        return [
            _guard(SUITE_BUNDLE, "bundle/roundtrip-reproduces-probabilities",
                   roundtrip_reproduces_probabilities),
            _guard(SUITE_BUNDLE, "bundle/metadata-declares-untrained", metadata_declares_no_training),
            _guard(SUITE_BUNDLE, "bundle/missing-metadata-rejected", missing_metadata_is_rejected),
            _guard(SUITE_BUNDLE, "bundle/unknown-metadata-key-rejected",
                   unknown_metadata_key_is_rejected),
            _guard(SUITE_BUNDLE, "bundle/corrupted-weights-rejected", corrupted_weights_are_rejected),
            _guard(SUITE_BUNDLE, "bundle/foreign-manifest-rejected", foreign_manifest_is_rejected),
            _guard(SUITE_BUNDLE, "bundle/wrong-format-version-rejected",
                   wrong_format_version_is_rejected),
        ]
    finally:
        shutil.rmtree(root, ignore_errors=True)


# --------------------------------------------------------------------------- #
def _replace(model_input, **overrides):
    """按字段替换 :class:`ModelInput` 的若干数组（检查用；不改真实路径）。"""
    import dataclasses

    import numpy as np

    values = {}
    for field in dataclasses.fields(model_input):
        current = getattr(model_input, field.name)
        if field.name in overrides:
            replacement = overrides[field.name]
            values[field.name] = (
                np.asarray(replacement, dtype=current.dtype)
                if isinstance(replacement, list)
                else replacement
            )
        else:
            values[field.name] = current
    return type(model_input)(**values)


def run_all(only: Optional[str] = None) -> list:
    outcomes: list = []
    if only is not None and only not in SUITE_NAMES:
        return outcomes
    context = _Context()
    if only in (None, SUITE_SHAPE):
        outcomes.extend(_shape_cases(context))
    if only in (None, SUITE_MASK):
        outcomes.extend(_mask_cases(context))
    if only in (None, SUITE_INVARIANCE):
        outcomes.extend(_invariance_cases(context))
    if only in (None, SUITE_POLICY):
        outcomes.extend(_policy_cases(context))
    if only in (None, SUITE_BUNDLE):
        outcomes.extend(_bundle_cases(context))
    return outcomes


__all__ = [
    "SUITE_BUNDLE",
    "SUITE_INVARIANCE",
    "SUITE_MASK",
    "SUITE_NAMES",
    "SUITE_POLICY",
    "SUITE_SHAPE",
    "run_all",
]
