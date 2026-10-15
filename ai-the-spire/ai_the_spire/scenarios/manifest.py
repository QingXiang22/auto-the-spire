"""场景清单：簇 = 宿主配置 + 决策差异轴 + **可核对的区分事实**。

关键点（针对"不用只换 seed 或标签制造多样性"）：

* 每个 `ScenarioSpec` 必须声明它**实际**使用的宿主开关（`host_flags`）；空 flags 是合法值，
  但必须与 `decision_axes` 和 `distinguishing_fact` 一致——"换了标签却没换环境"会被检查抓住；
* `distinguishing_fact` 是一个**公开事实谓词**（`group_present` / `group_absent` /
  `candidate_content_present`），必须在**该簇真实采到的公开快照**上成立；
* 清单有身份摘要：换 flags、换区分事实就是新清单（数据划分与评估都按它追溯）。
"""

from __future__ import annotations

import hashlib
from dataclasses import dataclass, fields
from typing import Any, Mapping, Sequence

from ..contracts import layers

#: 场景清单格式版本。
SCENARIO_MANIFEST_VERSION = "scenario-manifest@1"

#: 支持的区分事实种类（新增种类必须显式加进来）。
FACT_KINDS = ("group_present", "group_absent", "candidate_content_present")

#: 允许的决策差异轴（写清楚"这个簇在决策上差在哪"）。
DECISION_AXES = (
    "resources.potions",
    "resources.slots",
    "goals.run_state",
    "goals.long_term_deck",
    "knowledge.discard_choice",
    "knowledge.hidden_pile",
)

#: 已知的宿主开关（写错开关名会被拒绝，避免"开关没生效却以为换了环境"）。
KNOWN_HOST_FLAGS = ("--author-content", "--test-headbutt")


class ScenarioManifestError(ValueError):
    """场景清单非法：一律带场景/字段名与原因。"""


class ScenarioFactError(RuntimeError):
    """区分事实不成立：该簇的公开快照与声明不符（数据不可用于该簇）。"""


@dataclass(frozen=True)
class ScenarioSpec:
    """一个场景簇：宿主配置 + 决策差异轴 + 可核对的区分事实。"""

    scenario_id: str
    scenario_profile: str
    host_flags: tuple
    decision_axes: tuple
    fact_kind: str
    fact_value: str
    description: str = ""

    def __post_init__(self) -> None:
        if not isinstance(self.scenario_id, str) or not self.scenario_id.strip():
            raise ScenarioManifestError(f"scenario_id 必须是非空字符串，得到 {self.scenario_id!r}")
        if not isinstance(self.scenario_profile, str) or not self.scenario_profile.strip():
            raise ScenarioManifestError(
                f"{self.scenario_id}.scenario_profile 必须是非空字符串"
            )
        flags = tuple(self.host_flags)
        for flag in flags:
            if flag not in KNOWN_HOST_FLAGS:
                raise ScenarioManifestError(
                    f"{self.scenario_id}.host_flags 含未知开关 {flag!r}（已知：{KNOWN_HOST_FLAGS}）"
                )
        if len(set(flags)) != len(flags):
            raise ScenarioManifestError(f"{self.scenario_id}.host_flags 不得重复：{flags}")
        object.__setattr__(self, "host_flags", tuple(sorted(flags)))
        axes = tuple(self.decision_axes)
        if not axes:
            raise ScenarioManifestError(f"{self.scenario_id} 必须声明至少一个决策差异轴")
        for axis in axes:
            if axis not in DECISION_AXES:
                raise ScenarioManifestError(
                    f"{self.scenario_id}.decision_axes 含未知轴 {axis!r}（已知：{DECISION_AXES}）"
                )
        object.__setattr__(self, "decision_axes", tuple(sorted(axes)))
        if self.fact_kind not in FACT_KINDS:
            raise ScenarioManifestError(
                f"{self.scenario_id}.fact_kind={self.fact_kind!r} 不在 {FACT_KINDS} 内"
            )
        if not isinstance(self.fact_value, str) or not self.fact_value.strip():
            raise ScenarioManifestError(f"{self.scenario_id}.fact_value 必须是非空公开 id")
        if not isinstance(self.description, str):
            raise ScenarioManifestError(f"{self.scenario_id}.description 必须是字符串")
        # "换了标签却没换环境"的基本一致性：声明了资源/目标类的轴就必须有宿主开关,
        # 且不能只靠"隐藏牌堆"这一条（那是所有簇都有的）。
        axes_without_environment = {"resources.potions", "resources.slots", "goals.run_state",
                                    "goals.long_term_deck"}
        if axes_without_environment & set(axes) and not self.host_flags:
            raise ScenarioManifestError(
                f"{self.scenario_id}: 声明了资源/目标类差异轴却没有宿主开关——"
                "这等于只换了标签（开关必须真的改变环境）"
            )

    @property
    def identity_family(self) -> str:
        """产物目录的**身份分组**：同一目录里的记录必须来自同一份 ruleset/catalog/Manifest。

        只有 `--author-content` 会换 Profile 身份（作者变体的 ruleset/catalog 与默认切片不同），
        `--test-headbutt` 只改牌组、不改身份。因此按它分组存放；每组内部再由 `data.audit`
        独立核对"确实只有一种身份"（AI 侧不复制 net 的身份字符串，只按开关分组）。
        """
        return "author" if "--author-content" in self.host_flags else "default"

    def to_wire(self) -> dict:
        return {
            "scenario_id": self.scenario_id,
            "scenario_profile": self.scenario_profile,
            "host_flags": list(self.host_flags),
            "decision_axes": list(self.decision_axes),
            "fact_kind": self.fact_kind,
            "fact_value": self.fact_value,
            "description": self.description,
        }

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "ScenarioSpec":
        expected = {item.name for item in fields(cls)}
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise ScenarioManifestError(f"ScenarioSpec 键不匹配：缺少 {missing}，多出 {extra}")
        return cls(
            scenario_id=doc["scenario_id"],
            scenario_profile=doc["scenario_profile"],
            host_flags=tuple(doc["host_flags"]),
            decision_axes=tuple(doc["decision_axes"]),
            fact_kind=doc["fact_kind"],
            fact_value=doc["fact_value"],
            description=doc["description"],
        )

    def digest(self) -> str:
        return "sha256:" + hashlib.sha256(layers.canonical_bytes(self.to_wire())).hexdigest()


@dataclass(frozen=True)
class ScenarioManifest:
    """一份版本化的场景清单。"""

    manifest_id: str
    semantic_version: str
    scenarios: Sequence[ScenarioSpec]
    description: str = ""

    def __post_init__(self) -> None:
        for name in ("manifest_id", "semantic_version"):
            value = getattr(self, name)
            if not isinstance(value, str) or not value.strip():
                raise ScenarioManifestError(f"{name} 必须是非空字符串，得到 {value!r}")
        scenarios = tuple(self.scenarios)
        if len(scenarios) < 2:
            raise ScenarioManifestError("场景清单至少要两个簇（否则划分必然退化）")
        object.__setattr__(self, "scenarios", scenarios)
        ids: set[str] = set()
        environments: set[tuple] = set()
        for spec in scenarios:
            if not isinstance(spec, ScenarioSpec):
                raise ScenarioManifestError(f"场景必须是 ScenarioSpec，得到 {type(spec).__name__}")
            if spec.scenario_id in ids:
                raise ScenarioManifestError(f"场景 id 重复：{spec.scenario_id}")
            ids.add(spec.scenario_id)
            environment = (spec.scenario_profile, spec.host_flags)
            if environment in environments:
                raise ScenarioManifestError(
                    f"两个簇使用同一套宿主配置 {environment}：那只是同一个环境的两个标签"
                )
            environments.add(environment)
        if not isinstance(self.description, str):
            raise ScenarioManifestError("description 必须是字符串")

    # -- 读取 ------------------------------------------------------------ #
    def by_id(self, scenario_id: str) -> ScenarioSpec:
        for spec in self.scenarios:
            if spec.scenario_id == scenario_id:
                return spec
        raise ScenarioManifestError(f"清单里没有场景 {scenario_id!r}")

    @property
    def scenario_ids(self) -> tuple:
        return tuple(spec.scenario_id for spec in self.scenarios)

    def cluster_keys(self) -> dict:
        """场景 id → 数据划分用的簇键（与 `data.audit.cluster_key` 同构）。"""
        return {spec.scenario_id: f"{spec.scenario_profile}:{spec.scenario_id}"
                for spec in self.scenarios}

    # -- 序列化 ---------------------------------------------------------- #
    def to_wire(self) -> dict:
        return {
            "scenario_manifest_version": SCENARIO_MANIFEST_VERSION,
            "manifest_id": self.manifest_id,
            "semantic_version": self.semantic_version,
            "description": self.description,
            "scenarios": [spec.to_wire() for spec in self.scenarios],
        }

    @classmethod
    def from_wire(cls, doc: Mapping[str, Any]) -> "ScenarioManifest":
        expected = {"scenario_manifest_version", "manifest_id", "semantic_version", "description",
                    "scenarios"}
        missing = sorted(expected - set(doc))
        extra = sorted(set(doc) - expected)
        if missing or extra:
            raise ScenarioManifestError(f"ScenarioManifest 键不匹配：缺少 {missing}，多出 {extra}")
        if doc["scenario_manifest_version"] != SCENARIO_MANIFEST_VERSION:
            raise ScenarioManifestError(
                f"场景清单版本 {doc['scenario_manifest_version']!r} 与当前 "
                f"{SCENARIO_MANIFEST_VERSION!r} 不同"
            )
        if not isinstance(doc["scenarios"], list):
            raise ScenarioManifestError("scenarios 必须是数组")
        return cls(
            manifest_id=doc["manifest_id"],
            semantic_version=doc["semantic_version"],
            scenarios=tuple(ScenarioSpec.from_wire(item) for item in doc["scenarios"]),
            description=doc["description"],
        )

    def digest(self) -> str:
        return "sha256:" + hashlib.sha256(layers.canonical_bytes(self.to_wire())).hexdigest()


# --------------------------------------------------------------------------- #
# 区分事实：在该簇真实采到的**公开快照**上核对
# --------------------------------------------------------------------------- #
def _iter_snapshots(snapshots: Sequence[Mapping]) -> Any:
    for snapshot in snapshots:
        if hasattr(snapshot, "to_wire"):
            yield snapshot.to_wire()
        elif isinstance(snapshot, Mapping):
            yield snapshot
        else:
            raise ScenarioFactError(f"快照必须是对象或视图，得到 {type(snapshot).__name__}")


def check_distinguishing_fact(snapshots: Sequence[Mapping], spec: ScenarioSpec) -> tuple:
    """核对某簇声明的区分事实是否在其公开快照里成立。返回 ``(成立, 说明)``。"""
    if not isinstance(spec, ScenarioSpec):
        raise TypeError(f"spec 必须是 ScenarioSpec，得到 {type(spec).__name__}")
    documents = list(_iter_snapshots(snapshots))
    if not documents:
        raise ScenarioFactError(f"场景 {spec.scenario_id} 没有任何公开快照可核对")

    if spec.fact_kind in ("group_present", "group_absent"):
        present = any(group.get("type_id") == spec.fact_value
                      for document in documents for group in document.get("groups") or [])
        if spec.fact_kind == "group_present":
            return (present, f"group {spec.fact_value} 出现" if present
                    else f"公开快照里没有 group {spec.fact_value}")
        return (not present, f"group {spec.fact_value} 未出现" if not present
                else f"公开快照里出现了不该有的 group {spec.fact_value}")

    if spec.fact_kind == "candidate_content_present":
        for document in documents:
            content = {entity.get("instance_id"): entity.get("content_id")
                       for entity in document.get("entities") or []}
            for action in document.get("legal_actions") or []:
                for field in action.get("fields") or []:
                    if field.get("state") != "known":
                        continue
                    value = field.get("value")
                    if isinstance(value, str) and content.get(value) == spec.fact_value:
                        return (True, f"候选引用了内容 {spec.fact_value}")
        return (False, f"没有任何候选引用内容 {spec.fact_value}")

    raise ScenarioFactError(
        f"场景 {spec.scenario_id} 的区分事实种类 {spec.fact_kind!r} 没有实现（不得默认通过）"
    )


# --------------------------------------------------------------------------- #
# 本轮场景清单（受控 Gym 上真实可用的四个簇）
# --------------------------------------------------------------------------- #
def default_scenario_manifest() -> ScenarioManifest:
    """当前 Gym 上**真实存在**的四个簇（组合两个宿主开关），每簇都有可核对的公开事实。"""
    return ScenarioManifest(
        manifest_id="net:scenario:base-spire-controlled-v1",
        semantic_version="1",
        description="受控战斗 Gym 的场景簇（资源/目标/可观测知识三轴；每个簇绑定真实宿主开关）",
        scenarios=(
            ScenarioSpec(
                scenario_id="net:scenario:slice-default-v1",
                scenario_profile="base-spire-combat-slice-v1",
                host_flags=(),
                decision_axes=("knowledge.hidden_pile",),
                fact_kind="group_absent",
                fact_value="net:group_type:base-spire:potion_belt",
                description="程序化冻结切片、无药水栏：资源轴为空，仅有隐藏抽牌堆这一条知识约束",
            ),
            ScenarioSpec(
                scenario_id="net:scenario:slice-headbutt-v1",
                scenario_profile="base-spire-combat-slice-v1",
                host_flags=("--test-headbutt",),
                decision_axes=("knowledge.discard_choice",),
                fact_kind="candidate_content_present",
                fact_value="base-spire:headbutt",
                description="测试场景：多一张头槌，因此会在牌区里出现弃牌选择这一决策差异",
            ),
            ScenarioSpec(
                scenario_id="net:scenario:author-v1",
                scenario_profile="base-spire-combat-slice-v1",
                host_flags=("--author-content",),
                decision_axes=("resources.potions", "resources.slots", "goals.run_state",
                               "goals.long_term_deck"),
                fact_kind="group_present",
                fact_value="net:group_type:base-spire:potion_belt",
                description="作者内容包：药水栏（2 瓶 / 3 槽）+ 整局 HP/金币 + 长期牌组都是公开事实",
            ),
            ScenarioSpec(
                scenario_id="net:scenario:author-headbutt-v1",
                scenario_profile="base-spire-combat-slice-v1",
                host_flags=("--author-content", "--test-headbutt"),
                decision_axes=("resources.potions", "resources.slots", "goals.run_state",
                               "goals.long_term_deck", "knowledge.discard_choice"),
                fact_kind="group_present",
                fact_value="net:group_type:base-spire:potion_belt",
                description="作者内容 + 头槌：资源轴与弃牌选择轴同时出现（组合场景）",
            ),
        ),
    )


#: 本轮的冻结划分：簇 → split（train 两个簇，validation/final_eval 各一个）。
DEFAULT_SPLIT_ASSIGNMENTS = {
    "net:scenario:slice-default-v1": "train",
    "net:scenario:author-v1": "train",
    "net:scenario:slice-headbutt-v1": "validation",
    "net:scenario:author-headbutt-v1": "final_eval",
}


def default_split_assignments(manifest: ScenarioManifest) -> dict:
    """把 `DEFAULT_SPLIT_ASSIGNMENTS` 的簇键翻译成 `data.audit.cluster_key` 形式。"""
    keys = manifest.cluster_keys()
    assignments = {}
    for scenario_id, split in DEFAULT_SPLIT_ASSIGNMENTS.items():
        if scenario_id not in keys:
            raise ScenarioManifestError(f"划分引用了清单里没有的场景 {scenario_id!r}")
        assignments[keys[scenario_id]] = split
    return assignments
