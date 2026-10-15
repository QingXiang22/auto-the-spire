"""W-03 的场景层：**真正改变环境**的场景簇（不是只换种子或标签）。

要求（[当前清单](../../../docs/review/tofix/README.md) W-03 行 + `agent-goals.md` §7）：

* 场景簇围绕**决策差异**建立：资源（药水/槽位）、目标（整局 HP/金币/长期牌组）、
  可观测牌区知识（能做弃牌选择、能看到被揭示的位置）；
* 每个簇必须绑定**具体宿主配置**（真实开关），并且能被**公开事实**核对——
  否则"簇"只是标签，不同簇的数据会被当成同一种局面（`training-design.md` §"任务分布变更视为新实验"）；
* 划分按**簇**隔离（复用 `data.audit.SplitPlan`），最终组不参与训练选参；
* 采样有**显式限额**并报告真实成本；多会话是**轮转**而不是并行吞吐；
  随机策略数据只用于数据设施验证与基线，**不**作为新策略的近期 PPO 数据。
"""

from .manifest import (
    SCENARIO_MANIFEST_VERSION,
    FACT_KINDS,
    ScenarioFactError,
    ScenarioManifest,
    ScenarioManifestError,
    ScenarioSpec,
    check_distinguishing_fact,
    default_scenario_manifest,
    default_split_assignments,
)
from .sampling import (
    DEGENERATE_MARKER,
    SAMPLING_REPORT_VERSION,
    AggregatedAudit,
    ClusterSample,
    SamplingReport,
    audit_sampling,
    run_scenario_sampling,
)

__all__ = [
    "DEGENERATE_MARKER",
    "FACT_KINDS",
    "SAMPLING_REPORT_VERSION",
    "SCENARIO_MANIFEST_VERSION",
    "AggregatedAudit",
    "ClusterSample",
    "SamplingReport",
    "ScenarioFactError",
    "ScenarioManifest",
    "ScenarioManifestError",
    "ScenarioSpec",
    "audit_sampling",
    "check_distinguishing_fact",
    "default_scenario_manifest",
    "default_split_assignments",
    "run_scenario_sampling",
]
