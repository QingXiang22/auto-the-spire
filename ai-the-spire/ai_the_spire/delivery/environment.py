"""运行身份与环境 Manifest：**从后端声明取得**并在后续运行中**严格校验**。

任务包 §1 的要求：

* 实际 Gym、catalog 与 scenario 从 `describe`/`get_catalog` 取得并**冻结**；
* 初次运行落盘 identity/Manifest，后续严格校验；**不在 AI 中猜身份**；
* Manifest/Gym/结构/权重/Profile 随样本与包绑定，更新前拒绝混用行为版本、identity、奖励或执行器来源。

因此这里的 `manifest.json` 不是"我们自己写的声明"，而是**记录的观察结果**：每个字段都能追到
一次真实的 `describe`/`get_catalog` 应答。
"""

from __future__ import annotations

import pathlib
from dataclasses import dataclass
from typing import Any, Mapping, Optional

from ..encoding import EncodingConfig, GymManifest, build_manifest
from ..models.config import ModelConfig
from ..reward import RewardProfile
from ..runner import EpisodeIdentities, EpisodeRunner, gym_id_from
from ..runner.baseline import EXECUTOR_PROFILE_ID
from .config import Profile
from .errors import UsageError

#: 运行 Manifest 的格式版本。
RUN_MANIFEST_VERSION = "d1-run-manifest@1"


@dataclass(frozen=True)
class Environment:
    """一次真实 `describe`/`get_catalog` 得到的身份、目录与 Manifest。"""

    identities: EpisodeIdentities
    catalog: Mapping[str, Any]
    manifest: GymManifest
    encoding_config: EncodingConfig

    @property
    def player_label(self) -> str:
        return self.identities.gym_id


def observe_environment(profile: Profile, client) -> Environment:
    """describe + get_catalog → Manifest（身份全部来自后端声明，缺一即失败）。"""
    described = client.describe()
    if not described.get("ok"):
        raise UsageError(f"describe 失败：{described.get('error')}")
    identity = described["result"]["identity"]

    profiles = (described["result"].get("capabilities") or {}).get("scenario_profiles") or []
    if len(profiles) != 1:
        raise UsageError(
            f"后端必须声明恰好一个场景 profile（实际 {len(profiles)} 个）：不猜场景身份，先确认后端声明"
        )
    scenario_profile = profiles[0]["id"]
    gym_id = gym_id_from(identity["ruleset_id"])

    catalog_response = client.get_catalog(identity["catalog_id"])
    if not catalog_response.get("ok"):
        raise UsageError(f"get_catalog 失败：{catalog_response.get('error')}")
    catalog = catalog_response["result"]

    encoding_config = EncodingConfig()
    manifest = build_manifest(
        catalog,
        encoding_config,
        backend_profile_id=identity["backend_profile_id"],
        gym_id=gym_id,
        scenario_profile=scenario_profile,
    )
    identities = EpisodeIdentities(
        protocol_version=identity["protocol_version"],
        ruleset_id=identity["ruleset_id"],
        catalog_id=identity["catalog_id"],
        backend_profile_id=identity["backend_profile_id"],
        dictionary_version=manifest.dictionary_version,
        manifest_digest=manifest.manifest_digest,
        source_catalog_digest=manifest.source_catalog_digest,
        encoding_config_digest=manifest.encoding_config_digest,
        tensor_layout_version=manifest.tensor_layout_version,
        gym_id=gym_id,
        scenario_profile=scenario_profile,
        executor_profile_id=EXECUTOR_PROFILE_ID,
        model_id="",
        seed="",
        executor_seed="",
    )
    return Environment(identities=identities, catalog=catalog, manifest=manifest,
                       encoding_config=encoding_config)


def build_run_manifest(profile: Profile, environment: Environment, catalog_digest: str,
                       reward_profile: RewardProfile, *, host_dll: pathlib.Path) -> dict:
    """运行 Manifest：身份 + 目录 + 编码/模型结构 + 奖励/执行器来源 + 限额。

    这里记录的是**实际观察**与**实际使用**的配置：resolved-config.json 记"用了什么参数"，
    本文件记"这些参数落在哪个真实环境上"。
    """
    identities = environment.identities
    model_config = ModelConfig()
    return {
        "manifest_format_version": RUN_MANIFEST_VERSION,
        "profile_id": profile.profile_id,
        "semantic_version": profile.semantic_version,
        "config_path": str(profile.config_path),
        "config_digest": profile.config_digest,
        "environment_identity": {
            "protocol_version": identities.protocol_version,
            "ruleset_id": identities.ruleset_id,
            "catalog_id": identities.catalog_id,
            "backend_profile_id": identities.backend_profile_id,
            "gym_id": identities.gym_id,
            "scenario_profile": identities.scenario_profile,
        },
        "catalog_digest": catalog_digest,
        "dictionary_version": identities.dictionary_version,
        "tensor_layout_version": identities.tensor_layout_version,
        "manifest_digest": identities.manifest_digest,
        "source_catalog_digest": identities.source_catalog_digest,
        "encoding_config_digest": identities.encoding_config_digest,
        "encoding_config": environment.encoding_config.to_wire(),
        "model_config": model_config.to_wire(),
        "model_config_digest": model_config.digest(),
        "executor_profile_id": identities.executor_profile_id,
        "reward_profile": {
            "profile_id": reward_profile.profile_id,
            "digest": reward_profile.digest(),
            "wire": reward_profile.to_wire(),
        },
        "training_profile_id": profile.training_profile_id,
        "host": {"dll": str(host_dll), "extra_args": list(profile.host_extra_args)},
        "limits": {
            "max_decisions_per_episode": int(profile.budget["max_decisions_per_episode"]),
            "episode_time_limit_s": float(profile.budget["episode_time_limit_s"]),
            "request_timeout_s": float(profile.budget["request_timeout_s"]),
            "demo_command_time_limit_s": float(profile.budget["demo_command_time_limit_s"]),
            "train_command_time_limit_s": float(profile.budget["train_command_time_limit_s"]),
            "evaluate_command_time_limit_s": float(profile.budget["evaluate_command_time_limit_s"]),
            "storage_limit_bytes": int(profile.budget["storage_limit_bytes"]),
        },
        "seeds": {key: int(profile.seeds[key]) for key in sorted(profile.seeds)},
        "budget": {key: profile.budget[key] for key in sorted(profile.budget)},
    }


def verify_environment(recorded: Mapping[str, Any], environment: Environment, *,
                       where: str) -> None:
    """后续运行对已落盘 Manifest 的**严格校验**：任何身份不一致都在启动游戏前拒绝。"""
    if recorded.get("manifest_format_version") != RUN_MANIFEST_VERSION:
        raise UsageError(
            f"{where}: 运行 Manifest 版本 {recorded.get('manifest_format_version')!r} "
            f"与当前 {RUN_MANIFEST_VERSION!r} 不同（不静默迁移）"
        )
    identity = recorded.get("environment_identity") or {}
    identities = environment.identities
    checks = (
        ("environment_identity.ruleset_id", identity.get("ruleset_id"), identities.ruleset_id),
        ("environment_identity.catalog_id", identity.get("catalog_id"), identities.catalog_id),
        ("environment_identity.backend_profile_id", identity.get("backend_profile_id"),
         identities.backend_profile_id),
        ("environment_identity.gym_id", identity.get("gym_id"), identities.gym_id),
        ("environment_identity.scenario_profile", identity.get("scenario_profile"),
         identities.scenario_profile),
        ("dictionary_version", recorded.get("dictionary_version"), identities.dictionary_version),
        ("tensor_layout_version", recorded.get("tensor_layout_version"),
         identities.tensor_layout_version),
        ("manifest_digest", recorded.get("manifest_digest"), identities.manifest_digest),
        ("encoding_config_digest", recorded.get("encoding_config_digest"),
         identities.encoding_config_digest),
        ("executor_profile_id", recorded.get("executor_profile_id"),
         identities.executor_profile_id),
    )
    for name, got, expected in checks:
        if got != expected:
            raise UsageError(
                f"{where}: 当前后端身份与运行 Manifest 不一致（拒绝混用来源）："
                f"{name} 记录 {got!r} != 当前 {expected!r}"
            )


def make_runner(profile: Profile, client, environment: Environment, *,
                environment_seed: int, executor_seed: int, policy, model_id: str,
                max_steps: Optional[int] = None):
    """按冻结限额构造一局的 runner：决策上限、每局墙钟、截断后的确认观察都在这里固定。

    ``max_steps`` 只允许**收紧**到剩余动作额度（训练预算限流）。超过 Profile 每局上限即拒绝：
    每局限额是冻结值，不能靠"额度还很多"把它放宽。
    """
    per_episode_cap = int(profile.budget["max_decisions_per_episode"])
    resolved = per_episode_cap if max_steps is None else int(max_steps)
    if resolved <= 0:
        raise ValueError(f"每局动作上限必须是正数，得到 {resolved}")
    if resolved > per_episode_cap:
        raise ValueError(
            f"每局动作上限 {resolved} 超过 Profile 冻结的每局限额 {per_episode_cap}（不许放宽冻结值）"
        )
    return EpisodeRunner(
        client,
        seed=int(environment_seed),
        max_steps=resolved,
        encoding_config=environment.encoding_config,
        executor_seed=int(executor_seed),
        interruption_errors=client.interruption_errors,
        policy=policy,
        model_id=model_id,
        time_limit_s=float(profile.budget["episode_time_limit_s"]),
        capture_final_snapshot=True,
    )
