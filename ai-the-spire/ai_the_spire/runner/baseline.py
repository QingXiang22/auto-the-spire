"""随机合法基线：均匀候选概率 + 按规范语义键的执行器采样（J-01）。

权威：`contract-artifacts.md` §3——动作语义键是规范序列化的 ``{action_type, fields}``，
``legal_actions`` 按该键的 UTF-8 字节序排列、相同键拒绝重复、``candidate_id`` 不参与排序或特征；
执行器先按同一键对齐概率，再按**固定 ExecutorProfile + 执行 RNG** 采样。

概率只覆盖真实候选，内部 padding 为 0 是**构造保证**：这里没有 padding 行，概率向量长度恒等于候选数。
"""

from __future__ import annotations

import random
from dataclasses import dataclass
from typing import Any, Mapping, Sequence

from ..contracts.layers import action_semantic_key

#: 执行器配置身份：随轨迹记录，采样语义变更必须换 id。
EXECUTOR_PROFILE_ID = "net:executor:canonical-cdf-uniform-rng@1"

#: 基线模型身份：没有神经网络，**不得**当作训练模型。
BASELINE_MODEL_ID = "baseline:uniform-legal-candidates@1"


@dataclass(frozen=True)
class Candidate:
    """一个真实候选：句柄只用于 ``step``，语义键用于对齐与采样。"""

    candidate_id: str
    semantic_key: bytes
    action: Mapping[str, Any]

    @property
    def semantic_key_text(self) -> str:
        return self.semantic_key.decode("utf-8")


def ordered_candidates(snapshot: Mapping[str, Any]) -> list[Candidate]:
    """按公开语义键的 UTF-8 字节序排列候选；相同键直接拒绝（不得用句柄重复发布扭曲概率总量）。"""
    actions = list(snapshot.get("legal_actions") or [])
    if not actions:
        raise ValueError("快照没有候选：调用方不得为其伪造概率（终止或故障分开处理）")
    candidates = [
        Candidate(candidate_id=action["candidate_id"],
                  semantic_key=action_semantic_key(action),
                  action=action)
        for action in actions
    ]
    candidates.sort(key=lambda candidate: candidate.semantic_key)
    for index in range(1, len(candidates)):
        if candidates[index - 1].semantic_key == candidates[index].semantic_key:
            raise ValueError(
                "同一完整动作语义被重复发布（含 candidate_id 不同）："
                f"{candidates[index].semantic_key_text}"
            )
    return candidates


def uniform_probabilities(candidates: Sequence[Candidate]) -> dict[str, float]:
    """均匀概率，只覆盖真实候选；返回值按语义键顺序，和恰为 1。"""
    if not candidates:
        raise ValueError("没有候选就没有概率")
    weight = 1.0 / len(candidates)
    return {candidate.semantic_key_text: weight for candidate in candidates}


def sample_action(
    candidates: Sequence[Candidate],
    probabilities: Mapping[str, float],
    rng: random.Random,
) -> Candidate:
    """按语义键顺序在累积分布上采样。

    句柄换名或候选数组重排不改变结果：这里只看语义键与概率。传入的概率必须恰好覆盖全部候选
    （不重不漏），否则视为调用方错误——不得用默认值补齐。
    """
    if len(probabilities) != len(candidates):
        raise ValueError(
            f"概率项数 {len(probabilities)} 与候选数 {len(candidates)} 不一致（不重不漏是硬要求）"
        )
    total = 0.0
    cumulative: list[tuple[Candidate, float]] = []
    for candidate in candidates:
        weight = probabilities.get(candidate.semantic_key_text)
        if weight is None:
            raise ValueError(f"候选缺少概率项: {candidate.semantic_key_text}")
        total += weight
        cumulative.append((candidate, total))
    if total <= 0.0:
        raise ValueError("概率总和必须为正")
    draw = rng.random() * total if total != 1.0 else rng.random()
    for candidate, upper in cumulative:
        if draw < upper:
            return candidate
    return cumulative[-1][0]
