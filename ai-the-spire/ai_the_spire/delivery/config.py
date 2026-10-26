"""D1 工程演示 Profile 的解析与自检；当前要求见 `docs/areas/ai/spec/training.md`。

两条硬约束写在这里，而不是散在命令里：

1. **不猜配置**：全部参数来自显式 JSON（`ai-the-spire/configs/*.json`），缺键、多键、类型不对
   都在**启动游戏之前**报错；
2. **参数变化就是新 profile/run**：本模块内置 §4 表格的冻结值，解析结果与冻结值不一致即拒绝执行，
   防止"改了种子还留着旧实验身份"。

本模块只在真正的命令路径里被导入（`--help` 走 `run.py` 自带解析器，不需要 NumPy/已构建 Host）。
"""

from __future__ import annotations

import hashlib
import json
import pathlib
from dataclasses import dataclass
from typing import Any, Mapping

from .errors import UsageError

#: `resolved-config.json` 的格式版本。
RESOLVED_CONFIG_VERSION = "d1-resolved-config@1"

#: 冻结 Profile 表：与任务包 §4 的表格逐项对应（点分路径 → 期望值）。
_FROZEN_BASE: dict[str, Any] = {
    "host.extra_args": [],
    "seeds.demo_environment": 20261001,
    "seeds.demo_executor": 20261002,
    "seeds.model_init": 20261003,
    "seeds.train_environment_base": 10000,
    "seeds.validation_environment_base": 20000,
    "seeds.final_environment_base": 30000,
    "seeds.executor_offset": 1000000,
    "seeds.bootstrap": 20261003,
    "budget.max_decisions_per_episode": 128,
    "budget.episode_time_limit_s": 60.0,
    "budget.request_timeout_s": 10.0,
    "budget.demo_command_time_limit_s": 60.0,
    "budget.evaluate_command_time_limit_s": 180.0,
    "budget.storage_limit_bytes": 536870912,
    "ppo.learning_rate": 0.002,
    "ppo.clip_ratio": 0.2,
    "ppo.epochs": 2,
    "ppo.value_coef": 0.5,
    "ppo.entropy_coef": 0.0,
    "ppo.max_grad_norm": 1.0,
    "ppo.adam_beta1": 0.9,
    "ppo.adam_beta2": 0.999,
    "ppo.adam_epsilon": 1e-08,
    "ppo.seed": 20261003,
    "reward.discount": 0.99,
    "reward.hp_lost_weight": -0.01,
    "reward.victory_weight": 1.0,
    "reward.defeat_weight": -1.0,
    "evaluation.bootstrap_resamples": 1000,
    "evaluation.bootstrap_seed": 20261003,
}

FROZEN_PROFILES: dict[str, dict[str, Any]] = {
    "battle-demo-v1": {
        **_FROZEN_BASE,
        "budget.train_rounds": 8,
        "budget.episodes_per_round": 4,
        "budget.max_train_actions": 4096,
        "budget.train_command_time_limit_s": 900.0,
        "seeds.validation_environment_count": 8,
        "seeds.final_environment_count": 8,
    },
    "battle-demo-smoke-v1": {
        **_FROZEN_BASE,
        "budget.train_rounds": 2,
        "budget.episodes_per_round": 2,
        "budget.max_train_actions": 512,
        "budget.train_command_time_limit_s": 180.0,
        "seeds.validation_environment_count": 4,
        "seeds.final_environment_count": 4,
    },
}

#: 允许出现在配置文件里的键（点分路径通配 ``*`` 表示"任意子键"）。
_ALLOWED_SHAPE: dict[str, tuple[str, ...]] = {
    "": ("profile_id", "semantic_version", "description", "host", "seeds", "budget", "ppo",
         "reward", "evaluation", "model", "encoding"),
    "host": ("extra_args",),
    "seeds": ("demo_environment", "demo_executor", "model_init", "train_environment_base",
              "validation_environment_base", "validation_environment_count",
              "final_environment_base", "final_environment_count", "executor_offset", "bootstrap"),
    "budget": ("train_rounds", "episodes_per_round", "max_train_actions", "max_decisions_per_episode",
               "episode_time_limit_s", "request_timeout_s", "demo_command_time_limit_s",
               "train_command_time_limit_s", "evaluate_command_time_limit_s", "storage_limit_bytes"),
    "ppo": ("learning_rate", "clip_ratio", "epochs", "value_coef", "entropy_coef", "max_grad_norm",
            "adam_beta1", "adam_beta2", "adam_epsilon", "seed"),
    "reward": ("discount", "hp_lost_weight", "victory_weight", "defeat_weight"),
    "evaluation": ("bootstrap_resamples", "bootstrap_seed"),
    # `model` / `encoding` 为空对象 = 使用当前 ModelConfig/EncodingConfig 的默认结构（仍全部落盘）。
    "model": (),
    "encoding": (),
}

#: 训练 Profile 身份（进模型包 `training_profile_id`）：换 profile 或换语义版本即换身份。
TRAINING_PROFILE_PREFIX = "net:training-profile:"


def config_digest_of(path: pathlib.Path) -> str:
    """配置文件的**字节**摘要：同一路径内容变化（含空格/顺序）即换身份。"""
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


@dataclass(frozen=True)
class Profile:
    """解析后的冻结 Profile：全部实际参数（无隐藏默认值）。"""

    profile_id: str
    semantic_version: str
    description: str
    host_extra_args: tuple
    seeds: Mapping[str, int]
    budget: Mapping[str, Any]
    ppo: Mapping[str, Any]
    reward: Mapping[str, Any]
    evaluation: Mapping[str, Any]
    config_path: pathlib.Path
    config_digest: str

    # -- 种子派生（公式写在任务包 §4，这里只做算术，不另设默认） ---------------------------------- #
    @property
    def episodes_per_round(self) -> int:
        return int(self.budget["episodes_per_round"])

    @property
    def train_rounds(self) -> int:
        return int(self.budget["train_rounds"])

    def train_environment_seed(self, round_index: int, episode_index: int) -> int:
        """第 k 轮第 j 局：``base + episodes_per_round * k + j``（k/j 都从 0 起）。"""
        if not 0 <= round_index < self.train_rounds:
            raise UsageError(f"轮号 {round_index} 不在 [0, {self.train_rounds}) 内")
        if not 0 <= episode_index < self.episodes_per_round:
            raise UsageError(f"第 {episode_index} 局不在 [0, {self.episodes_per_round}) 内")
        return int(self.seeds["train_environment_base"]) + self.episodes_per_round * round_index + episode_index

    def validation_seeds(self) -> tuple[int, ...]:
        base = int(self.seeds["validation_environment_base"])
        return tuple(base + offset for offset in range(int(self.seeds["validation_environment_count"])))

    def final_seeds(self) -> tuple[int, ...]:
        base = int(self.seeds["final_environment_base"])
        return tuple(base + offset for offset in range(int(self.seeds["final_environment_count"])))

    def executor_seed(self, environment_seed: int) -> int:
        """局执行器种子：三种评估策略使用**同一规则**（环境种子 + 固定偏移）。"""
        return int(environment_seed) + int(self.seeds["executor_offset"])

    @property
    def training_profile_id(self) -> str:
        return f"{TRAINING_PROFILE_PREFIX}{self.profile_id}@{self.semantic_version}"

    # -- 落盘 ------------------------------------------------------------------------------------ #
    def resolved_document(self) -> dict:
        """`resolved-config.json`：全部**解析后**字段 + 结构与编码配置全文（不能只记"使用默认值"）。"""
        from ..encoding import EncodingConfig
        from ..models.config import ModelConfig

        model_config = ModelConfig()
        encoding_config = EncodingConfig()
        return {
            "resolved_config_format_version": RESOLVED_CONFIG_VERSION,
            "profile_id": self.profile_id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "config_path": str(self.config_path),
            "config_digest": self.config_digest,
            "host": {"extra_args": list(self.host_extra_args)},
            "seeds": {key: int(self.seeds[key]) for key in sorted(self.seeds)},
            "budget": {key: int(self.budget[key]) if isinstance(self.budget[key], int)
                       else float(self.budget[key]) for key in sorted(self.budget)},
            "ppo": {key: self.ppo[key] for key in sorted(self.ppo)},
            "reward": {key: self.reward[key] for key in sorted(self.reward)},
            "evaluation": {key: int(self.evaluation[key]) for key in sorted(self.evaluation)},
            "model_config": model_config.to_wire(),
            "model_config_digest": model_config.digest(),
            "encoding_config": encoding_config.to_wire(),
            "encoding_config_digest": encoding_config.digest(),
            "training_profile_id": self.training_profile_id,
        }


def load_profile(path: "str | pathlib.Path") -> Profile:
    """读取并校验配置文件；任何缺键/多键/类型问题都在这里变成 :class:`UsageError`。"""
    target = pathlib.Path(path)
    if not target.is_file():
        raise UsageError(
            f"配置文件不存在：{target}\n准备步骤：确认路径相对于调用者工作目录，"
            "或使用仓库自带的 ai-the-spire/configs/battle-demo-v1.json"
        )
    try:
        document = json.loads(target.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as failure:
        raise UsageError(f"配置文件不是合法 UTF-8 JSON：{target}（{failure}）") from failure
    if not isinstance(document, Mapping):
        raise UsageError(f"配置文件顶层必须是对象：{target}")

    profile_id = document.get("profile_id")
    if profile_id not in FROZEN_PROFILES:
        raise UsageError(
            f"未知 profile_id={profile_id!r}；本阶段只接受冻结 Profile {sorted(FROZEN_PROFILES)}。"
            "参数变化必须作为新 profile 规划，不能改旧 profile 的种子后沿用其身份"
        )
    _reject_unknown_keys(document)

    required_scalars = ("semantic_version", "description")
    for key in required_scalars:
        if not isinstance(document.get(key), str) or not document[key].strip():
            raise UsageError(f"配置 {key} 必须是非空字符串")

    frozen = FROZEN_PROFILES[str(profile_id)]
    actual = _flatten(document)
    mismatched = [
        (key, frozen[key], actual.get(key))
        for key in sorted(frozen)
        if key not in actual or actual[key] != frozen[key]
    ]
    if mismatched:
        lines = "\n".join(f"  - {key}: 配置 {got!r} != 冻结值 {expected!r}"
                          for key, expected, got in mismatched)
        raise UsageError(
            f"配置与冻结 Profile {profile_id} 的 §4 表格不一致（不猜配置，也不静默改冻结值）：\n{lines}"
        )

    return Profile(
        profile_id=str(profile_id),
        semantic_version=document["semantic_version"],
        description=document["description"],
        host_extra_args=tuple(document["host"]["extra_args"]),
        seeds={key: int(value) for key, value in document["seeds"].items()},
        budget=dict(document["budget"]),
        ppo=dict(document["ppo"]),
        reward=dict(document["reward"]),
        evaluation=dict(document["evaluation"]),
        config_path=target.resolve(),
        config_digest=config_digest_of(target),
    )


def _reject_unknown_keys(document: Mapping[str, Any]) -> None:
    for section, allowed in _ALLOWED_SHAPE.items():
        if section == "":
            actual_keys = set(document)
        else:
            value = document.get(section)
            if not isinstance(value, Mapping):
                raise UsageError(f"配置节 {section!r} 必须是对象，得到 {type(value).__name__}")
            actual_keys = set(value)
        missing = sorted(set(allowed) - actual_keys)
        extra = sorted(actual_keys - set(allowed))
        if missing or extra:
            where = section or "<顶层>"
            raise UsageError(f"配置节 {where} 键不匹配：缺少 {missing}，多出 {extra}")
    for name in ("extra_args",):
        if not isinstance(document["host"][name], list):
            raise UsageError(f"host.{name} 必须是数组")


def _flatten(document: Mapping[str, Any]) -> dict[str, Any]:
    flat: dict[str, Any] = {}
    for section in ("host", "seeds", "budget", "ppo", "reward", "evaluation"):
        for key, value in document[section].items():
            flat[f"{section}.{key}"] = value
    return flat
