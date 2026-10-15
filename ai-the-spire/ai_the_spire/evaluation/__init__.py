"""评估（W-04 第 3 项的 Evaluator）：固定执行配置 + 保留种子/场景 + 配对比较 + 成本与失败记录。

依据 `training-design.md` §"Evaluator"：

* 用**固定的执行配置**和**保留场景/种子**比较随机合法基线、可选规则基线与候选模型；
* 对**同种子配对**报告任务指标、方差/置信区间与资源成本；
* 任务分布变更视为新实验（计划里绑定 scenario_profile / gym_id / 奖励 Profile 身份）；
* **最终报告集不用于反复选参**；训练样本数、环境决策数、耗时、参数量与所有失败都要记录；
* 本层**不**替调用方下"更好/晋升"的结论：无改善也如实交付，CI 跨 0 就写 undecided。
"""

from .plan import (
    EVALUATION_PLAN_VERSION,
    METRIC_UNITS,
    EvaluationPlan,
    EvaluationPlanError,
    MetricSpec,
    PolicySpec,
)
from .runner import (
    EpisodeOutcome,
    EvaluationReport,
    EvaluationRunner,
    MetricSummary,
    PairedComparison,
    PolicySummary,
    aggregate,
    bootstrap_ci,
    paired_comparison,
)
from .stats import mean, percentile, standard_deviation

__all__ = [
    "EVALUATION_PLAN_VERSION",
    "METRIC_UNITS",
    "EpisodeOutcome",
    "EvaluationPlan",
    "EvaluationPlanError",
    "EvaluationReport",
    "EvaluationRunner",
    "MetricSpec",
    "MetricSummary",
    "PairedComparison",
    "PolicySpec",
    "PolicySummary",
    "aggregate",
    "bootstrap_ci",
    "mean",
    "paired_comparison",
    "percentile",
    "standard_deviation",
]
