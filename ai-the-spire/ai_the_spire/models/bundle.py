"""模型包：`metadata.json` + `weights.npz`，按 **exact identity** 绑定，载入错误显式。

依据 `policy-contract.md` §"ModelBundle"：模型包必须声明 net exact identity、训练的 scenario_profile、
所需字段能力、gym_id/dictionary_version、张量布局、encoder/scorer 接口、网络精度、
reward/training/executor profile 身份与训练来源范围，并用内容摘要绑定全部文件。

本实现（当前阶段）如实声明：`training = "none(structural,random-init)"`——还没有训练过，
不得据此声称任何学习效果；`reward_profile_id`/`training_profile_id` 显式为 null。
"""

from __future__ import annotations

import hashlib
import json
import pathlib
from typing import Any, Mapping, Optional

import numpy as np

from ..encoding.manifest import GymManifest
from .config import ModelConfig, ModelConfigError
from .policy import SmallPolicyModel

#: 模型包格式版本。
BUNDLE_FORMAT = "model-bundle@1"

#: `metadata.json` 的必需键（缺一即拒绝载入，不静默补默认）。
METADATA_KEYS = frozenset(
    {
        "bundle_format_version",
        "model_id",
        "model_config",
        "model_config_digest",
        "weights_digest",
        "parameter_total",
        "parameters",
        "dictionary_version",
        "tensor_layout_version",
        "manifest_digest",
        "environment_identity",
        "gym_id",
        "scenario_profile",
        "network_precision",
        "encoder_interface",
        "scorer_interface",
        "value_head",
        "training",
        "training_profile_id",
        "reward_profile_id",
        "executor_profile_id",
        "training_sources",
        "files",
        #: W-04：训练状态（未训练时为 null）。非 null 时必须同时给出 optimizer.npz。
        "training_state",
        "optimizer_file",
    }
)


class ModelBundleError(RuntimeError):
    """模型包不可用：带错误码（`code`）与原因，调用方不得把它当成"可用的旧模型"。"""

    def __init__(self, code: str, message: str, *, path: Optional[str] = None) -> None:
        self.code = code
        self.path = path
        super().__init__(f"[{code}] {path + ': ' if path else ''}{message}")


def _digest_file(path: pathlib.Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def save_bundle(model: SmallPolicyModel, directory: "str | pathlib.Path", *,
                training_sources: str = "structural-only(no-training)",
                training_state: Optional[Mapping[str, Any]] = None,
                optimizer_state: Optional[Mapping[str, np.ndarray]] = None,
                reward_profile_id: Optional[str] = None,
                training_profile_id: Optional[str] = None,
                executor_profile_id: Optional[str] = None) -> pathlib.Path:
    """落盘模型包，返回目录。权重按参数名排序写入；包内记录**内容摘要**用于载入校验
    （npz 是 zip 容器，其文件字节不作为"可复现"声明——可复现的是参数内容摘要与身份）。

    ``training_state`` + ``optimizer_state`` 一起给出时，包同时是**可恢复的训练检查点**：
    ``optimizer.npz`` 落盘优化器矩，metadata 记录训练配置摘要与优化器摘要。
    未训练时保持 ``training="none(structural,random-init)"``，不冒充已训练。
    """
    if not isinstance(model, SmallPolicyModel):
        raise TypeError(f"model 必须是 SmallPolicyModel，得到 {type(model).__name__}")
    if (training_state is None) != (optimizer_state is None):
        raise ModelBundleError(
            "bundle_invalid", "training_state 与 optimizer_state 必须同时给出（否则检查点不可恢复）"
        )
    target = pathlib.Path(directory)
    target.mkdir(parents=True, exist_ok=True)
    weights_path = target / "weights.npz"
    state = model.scorer.params.state()
    np.savez(weights_path, **{name: state[name] for name in sorted(state)})
    weights_digest = model.scorer.params.content_digest()

    optimizer_file = None
    optimizer_digest = None
    training_label = "none(structural,random-init)"
    if optimizer_state is not None:
        _validate_optimizer_state(optimizer_state)
        optimizer_file = "optimizer.npz"
        np.savez(target / optimizer_file,
                 **{name: np.asarray(optimizer_state[name]) for name in sorted(optimizer_state)})
        optimizer_digest = _optimizer_digest(optimizer_state)
        training_label = f"ppo(steps={int(training_state.get('steps', 0))})"

    files = {"weights.npz": _digest_file(weights_path)}
    if optimizer_file is not None:
        files[optimizer_file] = _digest_file(target / optimizer_file)

    metadata = {
        "bundle_format_version": BUNDLE_FORMAT,
        "model_id": model.model_id,
        "model_config": model.config.to_wire(),
        "model_config_digest": model.config.digest(),
        "weights_digest": weights_digest,
        "parameter_total": model.scorer.params.total_size(),
        "parameters": model.scorer.params.breakdown(),
        "dictionary_version": model.manifest.dictionary_version,
        "tensor_layout_version": model.manifest.tensor_layout_version,
        "manifest_digest": model.manifest.manifest_digest,
        "environment_identity": model.manifest.environment_identity.to_wire(),
        "gym_id": model.manifest.gym_id,
        "scenario_profile": model.manifest.scenario_profile,
        "network_precision": model.config.dtype,
        "encoder_interface": "encoder.forward(tensorized) -> {entities[B,E,de], global[B,dg], masks}",
        "scorer_interface": "scorer.forward(encoded, candidates) -> {logits[B,A], value[B]}",
        "value_head": "internal_state_value_only(no_Q)",
        "training": training_label,
        # 训练/奖励/执行器身份：未冻结时显式为 null，**不**自造 id（W-07 冻结后才填）。
        "training_profile_id": training_profile_id,
        "reward_profile_id": reward_profile_id,
        "executor_profile_id": executor_profile_id,
        "training_sources": training_sources,
        "files": files,
        "training_state": dict(training_state) if training_state is not None else None,
        "optimizer_file": optimizer_file,
    }
    if optimizer_digest is not None:
        metadata["training_state"]["optimizer_digest"] = optimizer_digest
    (target / "metadata.json").write_text(
        json.dumps(metadata, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8"
    )
    return target


def _validate_optimizer_state(state: Mapping[str, np.ndarray]) -> None:
    if not isinstance(state, Mapping) or not state:
        raise ModelBundleError("bundle_invalid", "optimizer_state 必须是非空映射")
    for name, array in state.items():
        if not isinstance(name, str) or not name:
            raise ModelBundleError("bundle_invalid", f"优化器状态键必须是非空字符串：{name!r}")
        data = np.asarray(array)
        if data.dtype.kind != "f":
            raise ModelBundleError("bundle_invalid", f"优化器状态 {name} 必须是浮点数组")


def _optimizer_digest(state: Mapping[str, np.ndarray]) -> str:
    digest = hashlib.sha256()
    for name in sorted(state):
        array = np.ascontiguousarray(np.asarray(state[name]))
        digest.update(name.encode("utf-8"))
        digest.update(str(array.dtype).encode("utf-8"))
        digest.update(repr(tuple(int(dim) for dim in array.shape)).encode("utf-8"))
        digest.update(array.tobytes())
    return "sha256:" + digest.hexdigest()


def load_bundle(directory: "str | pathlib.Path", *, manifest: GymManifest,
                load_optimizer: bool = False) -> SmallPolicyModel:
    """载入模型包并**逐项核对身份**；任何不一致都抛 :class:`ModelBundleError`。

    `manifest` 必须由调用方提供：模型包不能自己声明"我属于哪个 Gym"就算数——
    同一个 `model_id` 在不同字典/布局下必须被拒绝，否则会把旧 embedding 用在新的字典上。

    ``load_optimizer=True`` 时要求包里带训练状态，并把 ``optimizer_state`` / ``training_state``
    挂到返回的模型上（供 :meth:`Trainer.load_optimizer_state` 恢复）。
    """
    if not isinstance(manifest, GymManifest):
        raise TypeError(f"manifest 必须是 GymManifest，得到 {type(manifest).__name__}")
    target = pathlib.Path(directory)
    metadata_path = target / "metadata.json"
    weights_path = target / "weights.npz"
    if not metadata_path.is_file():
        raise ModelBundleError("bundle_incomplete", "缺少 metadata.json", path=str(target))
    if not weights_path.is_file():
        raise ModelBundleError("bundle_incomplete", "缺少 weights.npz", path=str(target))

    try:
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as failure:
        raise ModelBundleError("bundle_invalid", f"metadata.json 不是合法 JSON：{failure}",
                               path=str(metadata_path)) from failure
    if not isinstance(metadata, Mapping):
        raise ModelBundleError("bundle_invalid", "metadata.json 必须是对象", path=str(metadata_path))

    missing = sorted(METADATA_KEYS - set(metadata))
    extra = sorted(set(metadata) - METADATA_KEYS)
    if missing or extra:
        raise ModelBundleError(
            "bundle_invalid", f"metadata 键不匹配：缺少 {missing}，多出 {extra}", path=str(metadata_path)
        )
    if metadata["bundle_format_version"] != BUNDLE_FORMAT:
        raise ModelBundleError(
            "version_mismatch",
            f"模型包格式 {metadata['bundle_format_version']!r} 与当前 {BUNDLE_FORMAT!r} 不同（不静默迁移）",
            path=str(metadata_path),
        )

    try:
        config = ModelConfig.from_wire(metadata["model_config"])
    except (ModelConfigError, KeyError, TypeError) as failure:
        raise ModelBundleError("bundle_invalid", f"model_config 不可解析：{failure}",
                               path=str(metadata_path)) from failure
    if config.digest() != metadata["model_config_digest"]:
        raise ModelBundleError("identity_mismatch", "model_config_digest 与结构不一致",
                               path=str(metadata_path))

    # exact identity：字典/布局/Manifest 三者必须与调用方提供的 Manifest 逐字一致。
    for key, actual in (
        ("dictionary_version", manifest.dictionary_version),
        ("tensor_layout_version", manifest.tensor_layout_version),
        ("manifest_digest", manifest.manifest_digest),
    ):
        if metadata[key] != actual:
            raise ModelBundleError(
                "identity_mismatch",
                f"{key} 不一致：模型包 {metadata[key]!r} != 当前 {actual!r}；"
                "不得把旧 Gym 的 embedding 用在新字典上",
                path=str(metadata_path),
            )

    declared = metadata["files"].get("weights.npz")
    if declared != _digest_file(weights_path):
        raise ModelBundleError("weights_corrupt", "weights.npz 摘要与 metadata 不符",
                               path=str(weights_path))

    model = SmallPolicyModel(config, manifest)
    try:
        with np.load(weights_path, allow_pickle=False) as archive:
            state = {name: archive[name] for name in archive.files}
        model.scorer.params.load_state(state)
    except (ValueError, KeyError, OSError, EOFError) as failure:
        raise ModelBundleError("weights_invalid", f"权重不可载入：{failure}",
                               path=str(weights_path)) from failure

    if model.scorer.params.content_digest() != metadata["weights_digest"]:
        raise ModelBundleError("weights_corrupt", "载入后的权重摘要与 metadata 不符",
                               path=str(weights_path))
    model.weights_digest = metadata["weights_digest"]
    model.model_id = metadata["model_id"]

    # 训练状态：可选；要求时缺失即失败（不把"未训练"当成"可恢复的检查点"）。
    declared_state = metadata["training_state"]
    optimizer_name = metadata["optimizer_file"]
    if (declared_state is None) != (optimizer_name is None):
        raise ModelBundleError(
            "bundle_invalid", "training_state 与 optimizer_file 必须同时存在或同时为 null",
            path=str(metadata_path),
        )
    if load_optimizer:
        if declared_state is None:
            raise ModelBundleError("checkpoint_missing",
                                   "模型包没有训练状态（未训练），无法从它恢复训练",
                                   path=str(metadata_path))
        optimizer_path = target / str(optimizer_name)
        if not optimizer_path.is_file():
            raise ModelBundleError("bundle_incomplete", f"缺少 {optimizer_name}",
                                   path=str(target))
        declared_file_digest = metadata["files"].get(str(optimizer_name))
        if declared_file_digest != _digest_file(optimizer_path):
            raise ModelBundleError("weights_corrupt", f"{optimizer_name} 文件摘要与 metadata 不符",
                                   path=str(optimizer_path))
        try:
            with np.load(optimizer_path, allow_pickle=False) as archive:
                optimizer_state = {name: archive[name] for name in archive.files}
        except (ValueError, KeyError, OSError, EOFError) as failure:
            raise ModelBundleError("weights_invalid", f"优化器状态不可载入：{failure}",
                                   path=str(optimizer_path)) from failure
        declared_state_digest = declared_state.get("optimizer_digest")
        if _optimizer_digest(optimizer_state) != declared_state_digest:
            raise ModelBundleError("weights_corrupt", "载入后的优化器状态内容摘要与 metadata 不符",
                                   path=str(optimizer_path))
        model.optimizer_state = optimizer_state
        model.training_state = dict(declared_state)
    return model
