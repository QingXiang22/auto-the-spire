"""W-04 的**共享低成本模型**：结构化 encoder + 候选 scorer + 内部状态价值 V。

职责边界（`docs/understand/ai-architecture.md` §"模型方案"、`policy-contract.md` §1、`encoder-design.md` §5）：

* 只消费 **当前公开快照** 的张量化表示（`ai_the_spire.encoding.tensorize` 的输出）——不读历史、不读隐藏状态；
* 字典规模由 `GymManifest` 决定，结构/维度由**显式 `ModelConfig`** 决定；增加字段/内容 = 新的 Gym 配置，
  **不热加载**进旧 embedding（模型包按 exact identity 绑定）；
* 候选**不使用位置嵌入**，匿名实例句柄与候选下标不是特征——候选向量只由动作类型、具名字段与
  **被引用实体行**构成；
* 首版只输出合法候选上的概率 π 与内部状态价值 V；**不**输出动作 Q，也**不**把 V 复制给所有候选冒充动作收益。

实现取舍（必须如实声明）：本轮只用 **numpy** 实现结构与前向（不引入训练框架依赖）。
因此本模块给出的是**未训练的随机初始化模型**：它能输出满足契约的概率与 V，可用于接口/掩码/不变性/成本验收，
但**不**代表学习效果。梯度、参数更新、恢复与 PPO 属下一步，依赖学习框架（需维护者批准）。
"""

from .arrays import ModelInput, build_model_input
from .bundle import BUNDLE_FORMAT, ModelBundleError, load_bundle, save_bundle
from .config import MODEL_CONFIG_VERSION, ModelConfig, ModelConfigError
from .cost import measure_inference_cost, parameter_breakdown, parameter_report
from .encoder import EncodedObservation, SnapshotEncoder
from .learning import (
    TRAINING_CONFIG_VERSION,
    Adam,
    LearningError,
    PolicySample,
    Trainer,
    TrainingConfig,
    UpdateStats,
    action_probabilities,
    masked_log_prob,
    ppo_loss,
    sample_index,
    train_epoch,
)
from .policy import PolicyError, PolicyEvaluation, SmallPolicyModel, predict
from .scorer import CandidateScorer

__all__ = [
    "BUNDLE_FORMAT",
    "CandidateScorer",
    "EncodedObservation",
    "MODEL_CONFIG_VERSION",
    "ModelBundleError",
    "ModelConfig",
    "ModelConfigError",
    "ModelInput",
    "PolicyError",
    "PolicyEvaluation",
    "PolicySample",
    "SmallPolicyModel",
    "SnapshotEncoder",
    "TRAINING_CONFIG_VERSION",
    "Trainer",
    "TrainingConfig",
    "UpdateStats",
    "Adam",
    "LearningError",
    "action_probabilities",
    "build_model_input",
    "load_bundle",
    "masked_log_prob",
    "measure_inference_cost",
    "parameter_breakdown",
    "parameter_report",
    "ppo_loss",
    "predict",
    "sample_index",
    "save_bundle",
    "train_epoch",
]
