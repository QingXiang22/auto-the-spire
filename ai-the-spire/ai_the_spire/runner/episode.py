"""一次受控切片 episode：describe → get_catalog → Manifest → start → observe → 编码 → 概率 → step。

证据边界（开工清单 §8）：本模块走**真实 stdio**（`net-the-spire/client/net_client.py` 的 NDJSON 客户端），
消费的是当前公开快照。随机基线只证明管道，**不证明学习效果**；模型身份显式为随机基线。
"""

from __future__ import annotations

import hashlib
import json
import random
import time
from dataclasses import dataclass, field
from typing import Any, Callable, Mapping, Optional

from ..encoding import EncodingConfig, GymManifest, TENSOR_BLOCK_NAMES, build_manifest, tensorize
from ..encoding.tensor import TensorizedObservation
from .baseline import (
    BASELINE_MODEL_ID,
    EXECUTOR_PROFILE_ID,
    Candidate,
    ordered_candidates,
    sample_action,
    uniform_probabilities,
)

#: 训练环境身份：一个固定 Gym 对应一份目录/字典/配置（V-08/§16.1）。
#:
#: **不再硬编码**：Gym 身份从后端 `describe` 声明的 `ruleset_id` **派生**（见 :func:`gym_id_from`），
#: 场景 profile 取 `describe` 声明的那个。否则换成作者内容包（`--author-content`）后，
#: 两个不同 Gym 的轨迹会被并到同一个 `gym_id` 下，身份就不可核对了。
RULESET_PREFIX = "net:ruleset:"

GYM_PREFIX = "net:gym:"


def gym_id_from(ruleset_id: str) -> str:
    """后端声明的 `ruleset_id` → Gym 身份（同一 ruleset ⇒ 同一 Gym，不同 ruleset ⇒ 不同 Gym）。"""
    if not ruleset_id.startswith(RULESET_PREFIX) or len(ruleset_id) == len(RULESET_PREFIX):
        raise EpisodeError(
            f"ruleset_id 形状不认识：{ruleset_id!r}（期望 {RULESET_PREFIX!r} 前缀）；"
            "不猜 Gym 身份，先确认后端声明"
        )
    return GYM_PREFIX + ruleset_id[len(RULESET_PREFIX):]


class EpisodeError(RuntimeError):
    """episode 层错误：调用方不得把它当成游戏失败或伪造的终局。"""


class EpisodeInterrupted(EpisodeError):
    """`raise_on_failure=True` 时中断：**必须携带已经确认的部分结果**。

    为什么不能只带一条原因字符串：调用方（训练编排）需要把这一局**已经确认的公开事实**
    落到磁盘上——它们不是"半个样本"，而是真实发生过的事实。把结果对象丢掉，等于让故障局
    在产物里彻底消失，这正是 D1.1-F 要修的缺口。
    """

    def __init__(self, detail: str, result: "EpisodeResult") -> None:
        super().__init__(detail)
        self.result = result


@dataclass(frozen=True)
class EpisodeIdentities:
    """轨迹里必须能核对的身份（缺一即无法离线重编码）。"""

    protocol_version: str
    ruleset_id: str
    catalog_id: str
    backend_profile_id: str
    dictionary_version: str
    manifest_digest: str
    source_catalog_digest: str
    encoding_config_digest: str
    tensor_layout_version: str
    gym_id: str
    scenario_profile: str
    executor_profile_id: str
    model_id: str
    seed: str
    executor_seed: str

    def to_wire(self) -> dict:
        return dict(self.__dict__)


@dataclass
class Transition:
    """一次真实转移：公开事实（快照）+ 实际行为概率 + 执行结果。"""

    step: int
    request_id: str
    observation_id: int
    candidates: list[str]
    probabilities: dict[str, float]
    chosen_semantic_key: str
    chosen_candidate_id: str
    status: str
    terminal: Optional[dict]
    tensor_digest: str
    sizes: dict
    validation_skipped: tuple
    snapshot: dict
    next_observation_id: Optional[int]

    def to_wire(self) -> dict:
        payload = dict(self.__dict__)
        payload["validation_skipped"] = list(self.validation_skipped)
        return payload


@dataclass
class EpisodeResult:
    identities: EpisodeIdentities
    catalog: dict
    transitions: list[Transition] = field(default_factory=list)
    #: `terminal` / `truncated`（达到显式步数上限或每局墙钟上限）/ `interrupted` / `faulted`
    ending: str = "running"
    ending_detail: str = ""
    #: 截断**由哪一条限额**触发：`"decisions"`（可确认截断，仍是有效样本）/
    #: `"wall_time"`（资源限额，本轮不得提交更新）/ None（非截断）。
    #: 两种截断都不等于游戏失败，也都不许被写成胜负。
    limit_reason: Optional[str] = None
    #: 决策上限用尽、但会话**仍在运行**时，最后动作之后的那份**新公开快照**。
    #: 它只用于"用本轮冻结模型 ``V_old(s_T)`` bootstrap"，不是一条动作转移（不产生样本）。
    final_snapshot: Optional[dict] = None

    @property
    def steps(self) -> int:
        return len(self.transitions)

    def to_wire(self) -> dict:
        """轨迹文档（自足：目录 + 身份 + 逐条完整公开快照与行为概率）。"""
        return {
            "identities": self.identities.to_wire(),
            "catalog": self.catalog,
            "ending": self.ending,
            "ending_detail": self.ending_detail,
            "limit_reason": self.limit_reason,
            "steps": self.steps,
            "transitions": [transition.to_wire() for transition in self.transitions],
        }


def tensor_digest(observation: TensorizedObservation) -> str:
    """张量的确定性摘要：块列值 + 形状 + 布局身份（不含任何隐藏状态）。"""
    payload = {
        "layout": observation.tensor_layout_version,
        "sizes": observation.sizes,
        "blocks": {
            name: {column: list(values) for column, values in observation.blocks[name].columns.items()}
            for name in TENSOR_BLOCK_NAMES
        },
        "entity_rows": dict(observation.reference_map.entity_rows),
        "candidate_rows": dict(observation.reference_map.candidate_rows),
    }
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return "sha256:" + hashlib.sha256(encoded).hexdigest()


class EpisodeRunner:
    """驱动一个真实会话；每一步都编码、给概率、按语义键采样并执行。"""

    def __init__(
        self,
        client: Any,
        *,
        seed: int,
        max_steps: int = 60,
        encoding_config: Optional[EncodingConfig] = None,
        executor_seed: int = 20261002,
        interruption_errors: tuple = (OSError,),
        policy: Optional[Callable[[Mapping[str, Any], list[Candidate]], Mapping[str, float]]] = None,
        model_id: str = BASELINE_MODEL_ID,
        time_limit_s: Optional[float] = None,
        capture_final_snapshot: bool = False,
    ) -> None:
        if max_steps <= 0:
            raise ValueError("max_steps 必须是显式正数（截断必须可判定）")
        if time_limit_s is not None:
            if isinstance(time_limit_s, bool) or not isinstance(time_limit_s, (int, float)):
                raise ValueError(f"time_limit_s 必须是数值或 None，得到 {time_limit_s!r}")
            if float(time_limit_s) <= 0.0:
                raise ValueError("time_limit_s 必须是显式正数（截断必须可判定）")
        if policy is not None and model_id == BASELINE_MODEL_ID:
            # 注入策略却不改身份会把"脚本策略"的数据记成随机基线（V-08/§16.1 要求身份可核对）。
            raise ValueError("注入策略必须显式声明自己的 model_id，不得沿用随机基线身份")
        self._client = client
        self._seed = seed
        self._max_steps = max_steps
        self._config = encoding_config or EncodingConfig()
        self._executor_seed = executor_seed
        #: 每局墙钟上限（秒）。到点即按 `truncated/wall_time` 收尾：这是**资源限额**，
        #: 调用方不得据此提交训练更新（D1 §5）。None = 不限时（现有调用方保持原语义）。
        self._time_limit_s = None if time_limit_s is None else float(time_limit_s)
        #: 决策上限用尽后是否再确认观察一次：用来区分"恰好终止"与"仍在运行"，
        #: 并给截断局留下 bootstrap 用的新公开快照。默认关闭，现有 check 行为不变。
        self._capture_final_snapshot = bool(capture_final_snapshot)
        #: 行为策略 π：默认**均匀**（随机基线）。注入策略收到 `(公开快照, 候选)`，必须给出
        #: "恰好覆盖当前候选、和为 1"的概率（这是"先记录行为、再采样"而不是"先执行、再补概率"）。
        self._policy = policy
        self._model_id = model_id
        #: 视为"传输层丢失/结果不确定"的异常类型。数据/编码错误**不在**其中：它们必须继续上抛，
        #: 否则会把实现缺陷误报成"中断"。默认只含 OSError（断管道），调用方按所用客户端补充。
        self._interruption_errors = tuple(interruption_errors)
        self._rng = random.Random(executor_seed)
        self._manifest: Optional[GymManifest] = None
        self._identity: Optional[dict] = None
        self._catalog: Optional[dict] = None
        self._session_id: Optional[str] = None
        #: 后端声明与本 Gym 派生出的身份；`prepare()` 之前不可用（不预置默认值，避免"猜一个"）。
        self._scenario_profile: Optional[str] = None
        self._gym_id: Optional[str] = None

    # -- 只读身份 ------------------------------------------------------------------------------------------

    @property
    def manifest(self) -> GymManifest:
        if self._manifest is None:
            raise EpisodeError("Manifest 尚未构建：先调用 prepare()")
        return self._manifest

    # -- 准备 ----------------------------------------------------------------------------------------------

    def prepare(self) -> EpisodeIdentities:
        """describe + get_catalog → 构建 Manifest（JSON/meta-schema 通过不算完成，字典必须真的建出来）。

        场景 profile 与 Gym 身份都从 `describe` **读**出来：后端声明恰好一个场景 profile 时用它，
        否则报错（不猜）；Gym 身份由 `ruleset_id` 派生。
        """
        described = self._client.describe()
        if not described.get("ok"):
            raise EpisodeError(f"describe 失败：{described.get('error')}")
        self._identity = described["result"]["identity"]

        profiles = (described["result"].get("capabilities") or {}).get("scenario_profiles") or []
        if len(profiles) != 1:
            raise EpisodeError(
                f"后端必须声明恰好一个场景 profile（实际 {len(profiles)} 个）；不猜场景身份，先确认后端声明"
            )
        self._scenario_profile = profiles[0]["id"]
        self._gym_id = gym_id_from(self._identity["ruleset_id"])

        catalog_response = self._client.get_catalog(self._identity["catalog_id"])
        if not catalog_response.get("ok"):
            raise EpisodeError(f"get_catalog 失败：{catalog_response.get('error')}")
        catalog = catalog_response["result"]
        self._catalog = catalog

        self._manifest = build_manifest(
            catalog,
            self._config,
            backend_profile_id=self._identity["backend_profile_id"],
            gym_id=self._gym_id,
            scenario_profile=self._scenario_profile,
        )
        return self.identities()

    @property
    def scenario_profile(self) -> str:
        """后端声明的场景 profile（`prepare()` 之后可用；`start` 必须用同一个）。"""
        if self._scenario_profile is None:
            raise EpisodeError("场景身份尚未就绪：先调用 prepare()")
        return self._scenario_profile

    @property
    def gym_id(self) -> str:
        if self._gym_id is None:
            raise EpisodeError("Gym 身份尚未就绪：先调用 prepare()")
        return self._gym_id

    def identities(self) -> EpisodeIdentities:
        if self._identity is None or not self._catalog:
            raise EpisodeError("身份尚未就绪：先调用 prepare()")
        manifest = self.manifest
        return EpisodeIdentities(
            protocol_version=self._identity["protocol_version"],
            ruleset_id=self._identity["ruleset_id"],
            catalog_id=self._identity["catalog_id"],
            backend_profile_id=self._identity["backend_profile_id"],
            dictionary_version=manifest.dictionary_version,
            manifest_digest=manifest.manifest_digest,
            source_catalog_digest=manifest.source_catalog_digest,
            encoding_config_digest=manifest.encoding_config_digest,
            tensor_layout_version=manifest.tensor_layout_version,
            gym_id=self.gym_id,
            scenario_profile=self.scenario_profile,
            executor_profile_id=EXECUTOR_PROFILE_ID,
            model_id=self._model_id,
            seed=str(self._seed),
            executor_seed=str(self._executor_seed),
        )

    # -- 运行 ----------------------------------------------------------------------------------------------

    def run(self, *, raise_on_failure: bool = True) -> EpisodeResult:
        """跑一局。

        ``raise_on_failure=False`` 时，宿主丢失/超时不会抛异常，而是把这一局标成 `interrupted`，并把它之前
        **已确认**的转移与（若已发出 step）那条**结果不确定**的转移一起保留下来：这正是 D-02 需要的
        "确认转移不丢失、中断局不混入普通终止"。默认仍抛异常，避免调用方把故障当成正常结束。
        """
        identities = self.prepare()
        catalog = self._catalog
        if catalog is None:  # pragma: no cover - prepare() 已赋值
            raise EpisodeError("目录尚未就绪")

        started = self._client.start(self._identity, self.scenario_profile, seed=self._seed)
        if not started.get("ok"):
            raise EpisodeError(f"start 失败：{started.get('error')}")
        self._session_id = started["result"]["session_id"]

        result = EpisodeResult(identities=identities, catalog=catalog)
        deadline = None if self._time_limit_s is None else time.monotonic() + self._time_limit_s
        for step in range(self._max_steps):
            if deadline is not None and time.monotonic() >= deadline:
                # 资源限额：保留已确认转移，**不**再观察、**不**推进游戏，也不冒充胜负。
                result.ending = "truncated"
                result.limit_reason = "wall_time"
                result.ending_detail = (
                    f"达到每局墙钟上限 {self._time_limit_s:.3f}s"
                    "（资源限额截断，不是游戏失败；该局不得作为已完成的训练轮次）"
                )
                return result
            try:
                observed = self._client.observe(self._session_id)
            except self._interruption_errors as failure:
                return self._interrupt(result, raise_on_failure, f"observe 失败：{failure}")
            if not observed.get("ok"):
                result.ending = "faulted"
                result.ending_detail = json.dumps(observed.get("error"), ensure_ascii=False)
                return result

            status = observed["result"]["session_status"]
            snapshot = observed["result"].get("snapshot")
            if status == "terminal":
                if snapshot is None:
                    result.ending = "faulted"
                    result.ending_detail = "terminal 会话没有终止快照"
                    return result
                result.transitions.append(self._terminal_transition(step, snapshot, catalog))
                result.ending = "terminal"
                return result
            if snapshot is None:
                result.ending = "faulted"
                result.ending_detail = f"{status} 会话没有可执行快照"
                return result

            encoded = tensorize(snapshot, self.manifest, catalog=catalog)
            candidates = ordered_candidates(snapshot)
            probabilities = self._probabilities(snapshot, candidates)
            chosen = sample_action(candidates, probabilities, self._rng)

            request_id = f"j01-{step}"
            try:
                response = self._client.step(
                    self._session_id,
                    snapshot["observation_id"],
                    chosen.candidate_id,
                    request_id=request_id,
                )
            except self._interruption_errors as failure:
                # 请求已发出但结果不确定：记录"打算执行"的语义动作与实际概率，**不**声称它执行过，
                # 也**不**补造下一状态（D-01 的校验会拒绝任何相反写法）。
                result.transitions.append(
                    Transition(
                        step=step,
                        request_id=request_id,
                        observation_id=snapshot["observation_id"],
                        candidates=[candidate.semantic_key_text for candidate in candidates],
                        probabilities=probabilities,
                        chosen_semantic_key=chosen.semantic_key_text,
                        chosen_candidate_id=chosen.candidate_id,
                        status="unknown",
                        terminal=None,
                        tensor_digest=tensor_digest(encoded),
                        sizes=encoded.sizes,
                        validation_skipped=tuple(encoded.validation_skipped),
                        snapshot=snapshot,
                        next_observation_id=None,
                    )
                )
                return self._interrupt(result, raise_on_failure, f"step 结果不确定：{failure}")
            if not response.get("ok"):
                result.ending = "faulted"
                result.ending_detail = json.dumps(response.get("error"), ensure_ascii=False)
                return result

            result.transitions.append(
                Transition(
                    step=step,
                    request_id=f"j01-{step}",
                    observation_id=snapshot["observation_id"],
                    candidates=[candidate.semantic_key_text for candidate in candidates],
                    probabilities=probabilities,
                    chosen_semantic_key=chosen.semantic_key_text,
                    chosen_candidate_id=chosen.candidate_id,
                    status=status,
                    terminal=None,
                    tensor_digest=tensor_digest(encoded),
                    sizes=encoded.sizes,
                    validation_skipped=tuple(encoded.validation_skipped),
                    snapshot=snapshot,
                    next_observation_id=None,
                )
            )

            # 记录实际行为概率后，用同一条 `request_id` 查询执行确认（重发只执行一次）。
            # 这一步是**可选的对账**：查询失败并不改变"该 step 已被响应确认"这一事实，
            # 因此只把下一观测版本留空，不编造、也不把整局改判成失败。
            try:
                status_response = self._client.request_status(f"j01-{step}", self._session_id)
            except self._interruption_errors:
                return self._interrupt(result, raise_on_failure, "step 已确认，但后续对账查询丢失")
            if status_response.get("ok") and status_response["result"]["status"] == "completed":
                result.transitions[-1].next_observation_id = self._next_observation_id(status_response)

        if self._capture_final_snapshot:
            # 决策上限用尽：**必须**再确认观察一次，否则"最后一个动作恰好终结"会被误报成截断，
            # 而"仍在运行"又拿不到 bootstrap 需要的新公开快照（D1 §5 的 ENDING 语义）。
            try:
                observed = self._client.observe(self._session_id)
            except self._interruption_errors as failure:
                return self._interrupt(result, raise_on_failure, f"步数上限后的确认观察失败：{failure}")
            if not observed.get("ok"):
                result.ending = "faulted"
                result.ending_detail = json.dumps(observed.get("error"), ensure_ascii=False)
                return result
            status = observed["result"]["session_status"]
            snapshot = observed["result"].get("snapshot")
            if status == "terminal":
                if snapshot is None:
                    result.ending = "faulted"
                    result.ending_detail = "terminal 会话没有终止快照"
                    return result
                result.transitions.append(
                    self._terminal_transition(len(result.transitions), snapshot, catalog)
                )
                result.ending = "terminal"
                result.ending_detail = "最后一个允许动作进入了终止状态（终止快照无动作，不计步）"
                return result
            if snapshot is None:
                result.ending = "faulted"
                result.ending_detail = f"{status} 会话没有可执行快照"
                return result
            result.final_snapshot = snapshot

        result.ending = "truncated"
        result.limit_reason = "decisions"
        result.ending_detail = f"达到显式步数上限 {self._max_steps}（测试截断，不是游戏失败）"
        return result

    def _probabilities(self, snapshot: Mapping[str, Any], candidates: list[Candidate]) -> dict:
        """本轮行为分布 π：默认均匀；注入策略时**校验**它恰好覆盖真实候选且和为 1。

        策略收到 `(当前公开快照, 当前候选)`：模型策略需要快照本身（实体/字段），
        只看候选句柄无法决定动作。不校验就会写出无法离线复核的轨迹（`verify_trace` 要求概率不重不漏）。
        """
        if self._policy is None:
            return uniform_probabilities(candidates)
        probabilities = dict(self._policy(snapshot, list(candidates)))
        expected = {candidate.semantic_key_text for candidate in candidates}
        if set(probabilities) != expected:
            missing = sorted(expected - set(probabilities))
            extra = sorted(set(probabilities) - expected)
            raise EpisodeError(f"策略概率未恰好覆盖真实候选：缺少 {missing}，多出 {extra}")
        total = sum(probabilities.values())
        if abs(total - 1.0) > 1e-9:
            raise EpisodeError(f"策略概率之和必须为 1，实际 {total}")
        if any(value < 0 for value in probabilities.values()):
            raise EpisodeError("策略概率不得为负")
        return probabilities

    def _interrupt(self, result: EpisodeResult, raise_on_failure: bool, detail: str) -> EpisodeResult:
        """宿主丢失/结果不确定：按 `interrupted` 收尾（绝不写成终局或失败），并保留已确认的转移。

        `raise_on_failure=True` 时抛 :class:`EpisodeInterrupted`，它**带着** `result`：调用方必须
        先把已确认事实落盘，再决定非零退出。两种模式下 `result` 都已经是自描述的（`ending`
        与 `ending_detail` 都写好），这样"抛出后拿到的那份结果"没有 `running` 这种半成品状态。
        """
        result.ending = "interrupted"
        result.ending_detail = detail
        if raise_on_failure:
            raise EpisodeInterrupted(detail, result)
        return result

    @staticmethod
    def _next_observation_id(status_response: Mapping[str, Any]) -> Optional[int]:
        original = status_response["result"].get("response") or {}
        payload = original.get("result") or {}
        snapshot = payload.get("snapshot")
        return snapshot.get("observation_id") if isinstance(snapshot, dict) else None

    def _terminal_transition(self, step: int, snapshot: Mapping[str, Any], catalog: Mapping[str, Any]) -> Transition:
        encoded = tensorize(snapshot, self.manifest, catalog=catalog)
        return Transition(
            step=step,
            request_id=f"j01-{step}",
            observation_id=snapshot["observation_id"],
            candidates=[],
            probabilities={},
            chosen_semantic_key="",
            chosen_candidate_id="",
            status="terminal",
            terminal=snapshot.get("terminal_result"),
            tensor_digest=tensor_digest(encoded),
            sizes=encoded.sizes,
            validation_skipped=tuple(encoded.validation_skipped),
            snapshot=snapshot,
            next_observation_id=None,
        )
