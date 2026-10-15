"""模型包的**创建 / 载入**衔接（复用 `ai_the_spire.models`，不重写网络或序列化）。

三条不可省的规则（任务包 §5）：

1. 未训练就写 `untrained`，不得因为"跑起来了"就声称已训练；
2. 载入必须按 **exact identity** 校验（Gym/字典/布局/Manifest 逐项一致），
   身份或权重损坏时**在启动游戏之前**拒绝，绝不默默随机初始化；
3. 更新成功后按**实际权重内容**重算候选身份（`refresh_identity`），权重没变就不伪造变化。
"""

from __future__ import annotations

from typing import Any

from ..encoding import GymManifest
from ..models import SmallPolicyModel, load_bundle
from ..models.config import ModelConfig
from .config import Profile
from .errors import UsageError
from .environment import Environment


def fresh_model(profile: Profile, environment: Environment) -> SmallPolicyModel:
    """按冻结 `model_init` 种子做确定性随机初始化（**未训练**，身份如实标注）。"""
    config = ModelConfig(seed=int(profile.seeds["model_init"]))
    return SmallPolicyModel(config, environment.manifest, catalog=environment.catalog)


def load_model_raw(path: str, environment: Environment, *, where: str) -> SmallPolicyModel:
    """按 exact identity 载入模型包；任何不一致都变成退出码 2 的使用/身份错配。"""
    from ..models import ModelBundleError

    try:
        model = load_bundle(path, manifest=environment.manifest)
    except ModelBundleError as failure:
        raise UsageError(
            f"{where}: 模型包不可用（在启动游戏之前拒绝，不默默随机初始化）：{failure}"
        ) from failure
    except (OSError, ValueError) as failure:
        raise UsageError(f"{where}: 模型包路径不可读：{path}（{failure}）") from failure
    model.catalog = environment.catalog
    return model


def load_model_for_resume(path: str, environment: Environment, *, where: str) -> SmallPolicyModel:
    """载入**可恢复检查点**（含优化器状态）；没有训练状态即拒绝。"""
    from ..models import ModelBundleError

    try:
        model = load_bundle(path, manifest=environment.manifest, load_optimizer=True)
    except ModelBundleError as failure:
        raise UsageError(f"{where}: 检查点不可用于恢复：{failure}") from failure
    except (OSError, ValueError) as failure:
        raise UsageError(f"{where}: 检查点路径不可读：{path}（{failure}）") from failure
    model.catalog = environment.catalog
    return model


def model_summary(model: SmallPolicyModel, *, state: str) -> dict:
    """模型身份摘要（可核对：同一结构不同权重必须是不同 model_id）。"""
    return {
        "model_id": model.model_id,
        "weights_digest": model.weights_digest,
        "config_digest": model.config.digest(),
        "parameter_total": model.scorer.params.total_size(),
        "parameters": model.scorer.params.breakdown(),
        "state": state,
    }


def assert_manifest_matches(model: SmallPolicyModel, manifest: GymManifest, *, where: str) -> None:
    if model.manifest.manifest_digest != manifest.manifest_digest:
        raise UsageError(
            f"{where}: 模型包的 Manifest 与当前环境不一致："
            f"{model.manifest.manifest_digest!r} != {manifest.manifest_digest!r}"
        )


def catalog_for(model: SmallPolicyModel, environment: Environment) -> Any:
    return environment.catalog
