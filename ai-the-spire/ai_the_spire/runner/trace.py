"""轨迹格式与离线重编码校验（J-01 第 5/6 条）。

轨迹必须**自足**：目录、身份、逐条公开快照、实际行为概率、request_id、执行器配置都在文件里，
因此离线重读不需要活动宿主、也不需要重放游戏——重编码只看记录的当前快照，这正是"不读历史、
不保存 known_top"的可核对形式。

**不得**把这里当成“训练数据格式已冻结”：D-01 才是 EpisodeRecord 的正式格式与索引；本模块
只是 J-01 的联调轨迹，格式版本显式为 ``j01-trace@1``。
"""

from __future__ import annotations

import json
import pathlib
import random
from typing import Any, Mapping, Sequence

from ..encoding import EncodingConfig, build_manifest, tensorize
from .baseline import ordered_candidates, sample_action, uniform_probabilities
from .episode import EpisodeIdentities, tensor_digest

TRACE_FORMAT_VERSION = "j01-trace@1"

REQUIRED_KEYS = (
    "trace_format_version",
    "identities",
    "catalog",
    "ending",
    "ending_detail",
    "steps",
    "transitions",
)

IDENTITY_KEYS = tuple(EpisodeIdentities.__dataclass_fields__)


class TraceError(RuntimeError):
    """轨迹不可自足或不可复核；调用方不得把它当成游戏结果。"""


def write_trace(payload: Mapping[str, Any], path: "str | pathlib.Path") -> pathlib.Path:
    """按规范形式落盘（紧凑 JSON + 键排序），保证同一 episode 逐字节可复现。"""
    document = {
        "trace_format_version": TRACE_FORMAT_VERSION,
        **{key: payload[key] for key in ("identities", "catalog", "ending", "ending_detail", "steps",
                                         "transitions")},
    }
    target = pathlib.Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(
        json.dumps(document, ensure_ascii=False, sort_keys=True, separators=(",", ":")),
        encoding="utf-8",
    )
    return target


def load_trace(path: "str | pathlib.Path") -> dict:
    """离线重读：格式与必需键必须齐全，身份必须自足。"""
    document = json.loads(pathlib.Path(path).read_text(encoding="utf-8"))
    missing = [key for key in REQUIRED_KEYS if key not in document]
    if missing:
        raise TraceError(f"轨迹缺少必需键: {missing}")
    if document["trace_format_version"] != TRACE_FORMAT_VERSION:
        raise TraceError(
            f"轨迹格式版本 {document['trace_format_version']!r} 与当前 {TRACE_FORMAT_VERSION!r} 不同"
        )
    identity_missing = [key for key in IDENTITY_KEYS if key not in document["identities"]]
    if identity_missing:
        raise TraceError(f"身份缺少必需键（离线无法重建字典）: {identity_missing}")
    return document


def rebuild_manifest(trace: Mapping[str, Any], encoding_config: EncodingConfig):
    """用轨迹里的目录与配置重建 Manifest，并核对身份逐字一致。"""
    identities = trace["identities"]
    manifest = build_manifest(
        trace["catalog"],
        encoding_config,
        backend_profile_id=identities["backend_profile_id"],
        gym_id=identities["gym_id"],
        scenario_profile=identities["scenario_profile"],
    )
    for key in ("dictionary_version", "manifest_digest", "source_catalog_digest",
                "encoding_config_digest", "tensor_layout_version"):
        if getattr(manifest, key) != identities[key]:
            raise TraceError(
                f"离线重建的 {key} 与轨迹身份不一致：{getattr(manifest, key)!r} != {identities[key]!r}"
            )
    return manifest


def verify_trace(trace: Mapping[str, Any], encoding_config: EncodingConfig) -> list[str]:
    """离线复核：重编码逐条一致 + 概率只覆盖真实候选 + 执行器按语义键可重放。返回问题列表（空为通过）。"""
    problems: list[str] = []
    try:
        manifest = rebuild_manifest(trace, encoding_config)
    except TraceError as failure:
        return [str(failure)]

    executor_seed = int(trace["identities"]["executor_seed"])
    rng = random.Random(executor_seed)
    for transition in trace["transitions"]:
        step = transition["step"]
        if transition["status"] == "terminal":
            if transition["candidates"]:
                problems.append(f"step {step}: 终止快照不得有候选")
            if not transition["terminal"]:
                problems.append(f"step {step}: 终止快照缺少 terminal_result")
            continue

        snapshot = transition["snapshot"]
        encoded = tensorize(snapshot, manifest, catalog=trace["catalog"])
        if tensor_digest(encoded) != transition["tensor_digest"]:
            problems.append(f"step {step}: 离线重编码的张量摘要与轨迹不一致")

        candidates = ordered_candidates(snapshot)
        keys = [candidate.semantic_key_text for candidate in candidates]
        if keys != transition["candidates"]:
            problems.append(f"step {step}: 候选语义键序列与轨迹不一致")
        probabilities = transition["probabilities"]
        if sorted(probabilities) != sorted(keys):
            problems.append(f"step {step}: 概率未恰好覆盖真实候选（不重不漏）")
            continue
        if abs(sum(probabilities.values()) - 1.0) > 1e-9:
            problems.append(f"step {step}: 概率总和不为 1")
        if transition["chosen_semantic_key"] not in probabilities:
            problems.append(f"step {step}: 选中的动作语义不在候选内")

        # 执行器重放：同样的概率 + 同样的执行 RNG → 同样的语义动作（句柄不参与）。
        replayed = sample_action(candidates, probabilities, rng)
        if replayed.semantic_key_text != transition["chosen_semantic_key"]:
            problems.append(
                f"step {step}: 执行器重放选出的语义动作与轨迹不一致 "
                f"({replayed.semantic_key_text!r} != {transition['chosen_semantic_key']!r})"
            )

    return problems


def replay_sampling(candidates: Sequence[Mapping[str, Any]], executor_seed: int) -> list[str]:
    """辅助：对给定候选序列按均匀概率与固定执行 RNG 采样，返回语义键序列（用于重排/换名等价性检查）。"""
    rng = random.Random(executor_seed)
    chosen: list[str] = []
    for snapshot in candidates:
        ordered = ordered_candidates(snapshot)
        chosen.append(sample_action(ordered, uniform_probabilities(ordered), rng).semantic_key_text)
    return chosen
