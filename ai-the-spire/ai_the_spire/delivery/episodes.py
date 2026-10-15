"""一局真实 episode 的公共包装：跑局 → 判定结束类别 → 派生 D1 逐动作奖励。

train 与 evaluate **共用**这一份，因此"训练与评估使用同一语义"不是靠约定，而是同一段代码。
"""

from __future__ import annotations

import time
from dataclasses import dataclass
from typing import Any, Optional

from ..reward import RewardProfile
from ..runner import EpisodeInterrupted, EpisodeResult
from .environment import Environment, make_runner
from .errors import DeliveryError, FaultError
from .reward_v1 import EpisodeReward, action_rewards, episode_reward

#: 可用于统计的结束类别（终局与**决策上限**截断）。故障与中断一律不算。
USABLE_ENDINGS = ("terminal", "truncated")


class EpisodeFault(FaultError):
    """训练路径的故障/中断/资源限额：**携带这一局已经确认的部分结果**。

    调用方（`train.py`）必须先把这个 `run` 的公开事实按已有记录格式落盘，再非零退出：
    断连/未知执行/墙钟停止**不等于**这一局没发生过，不能让它从产物里消失。
    """

    def __init__(self, message: str, *, run: "EpisodeRun") -> None:
        super().__init__(message, reason=message)
        self.run = run


@dataclass(frozen=True)
class EpisodeRun:
    """一次真实运行的结果 + 派生奖励 + 成本（不新造任何未观察到的信息）。"""

    result: EpisodeResult
    wall_s: float
    reward: Optional[EpisodeReward]
    reward_problem: Optional[str]
    confirmed_actions: int
    truncated_by_decisions: bool
    #: 结果不确定的转移条数（如实另记，**不**算已确认动作，也不返还任何已用额度）。
    unknown_actions: int = 0

    @property
    def ending(self) -> str:
        return self.result.ending

    @property
    def terminal_outcome(self) -> Optional[str]:
        if self.result.ending != "terminal" or not self.result.transitions:
            return None
        terminal = self.result.transitions[-1].terminal or {}
        return terminal.get("outcome")


def confirmed_action_indices(result: EpisodeResult) -> list[int]:
    """已确认的**外部动作**下标：终止快照不是动作，结果不确定的转移不算已确认。"""
    return [index for index, transition in enumerate(result.transitions)
            if transition.status not in ("terminal", "unknown")]


def reward_snapshots(result: EpisodeResult) -> tuple[list[dict], Optional[str]]:
    """逐动作奖励需要的公开快照序列：每个已确认动作的前快照 + 最后动作的真实后快照。"""
    indices = confirmed_action_indices(result)
    if not indices:
        return [], "没有已确认动作（终止/故障快照不产生样本）"
    snapshots = [result.transitions[index].snapshot for index in indices]
    last = indices[-1]
    if last + 1 < len(result.transitions):
        snapshots.append(result.transitions[last + 1].snapshot)
    elif result.final_snapshot is not None:
        snapshots.append(result.final_snapshot)
    else:
        return [], ("最后一个已确认动作没有可确认的后快照（结果不确定或资源限额停止）："
                    "不得补造，也不得按 0 奖励计样本")
    return snapshots, None


def run_episode(profile, client, environment: Environment, reward_profile: RewardProfile, *,
                environment_seed: int, executor_seed: int, policy, model_id: str,
                require_usable: bool, max_steps: Optional[int] = None) -> EpisodeRun:
    """跑一局并按 D1 §5 判定结束类别。

    ``require_usable=True``（训练路径）：墙钟限额截断、故障、中断一律抛 :class:`EpisodeFault`，
    并且**一定带上这一局的部分结果**（已确认的公开事实、结束类别）——技术故障必须停止当前命令、
    把事实落盘，但不把故障转成 defeat，也不开新局填满批次。
    ``require_usable=False``（评估路径）：如实返回，由调用方记录进失败表并让命令非零退出。

    ``max_steps``：本局的动作上限（缺省取 Profile 的每局上限）。训练用它按**剩余动作额度**限流，
    这样"额度只剩 3 个"时这一局最多只能做 3 个确认动作，不会先超采再回退。
    """
    runner = make_runner(profile, client, environment,
                         environment_seed=environment_seed, executor_seed=executor_seed,
                         policy=policy, model_id=model_id, max_steps=max_steps)
    started = time.perf_counter()
    # 评估路径不抛异常是为了"故障不能从表中消失"；训练路径在返回后立刻判定，效果相同。
    try:
        result = runner.run(raise_on_failure=require_usable)
    except EpisodeInterrupted as failure:
        wall_s = time.perf_counter() - started
        run = _build_run(failure.result, wall_s, reward_profile)
        raise EpisodeFault(
            f"episode(seed={environment_seed}) 结果不确定：{failure}；"
            "已保留这一局确认过的公开事实，停止当前命令，不开启替代局",
            run=run,
        ) from failure
    except DeliveryError:
        raise
    except Exception as failure:  # noqa: BLE001 - 运行故障必须归类为故障，不得当成功或当胜负
        raise FaultError(
            f"episode(seed={environment_seed}) 运行故障：{type(failure).__name__}: {failure}；"
            "已确认记录保留，停止当前命令，不开启替代局"
        ) from failure
    wall_s = time.perf_counter() - started
    run = _build_run(result, wall_s, reward_profile)

    if require_usable:
        if result.ending == "truncated" and result.limit_reason == "wall_time":
            raise EpisodeFault(
                f"episode(seed={environment_seed}) 达到每局墙钟限额：{result.ending_detail}；"
                "该轮不得提交更新",
                run=run,
            )
        if result.ending == "faulted":
            raise EpisodeFault(
                f"episode(seed={environment_seed}) 后端故障：{result.ending_detail}；"
                "已保存已确认记录，停止当前命令，不开启替代局",
                run=run,
            )
        if result.ending == "interrupted":
            raise EpisodeFault(
                f"episode(seed={environment_seed}) 结果不确定：{result.ending_detail}",
                run=run,
            )
    return run


def _build_run(result: EpisodeResult, wall_s: float, reward_profile: RewardProfile) -> EpisodeRun:
    """把结果对象压成 :class:`EpisodeRun`；**缺后快照不阻止保留事实**，只是没有完整样本。"""
    snapshots, problem = reward_snapshots(result)
    reward: Optional[EpisodeReward] = None
    if problem is None:
        try:
            reward = episode_reward(action_rewards(snapshots, reward_profile), reward_profile)
        except (FaultError, ValueError) as failure:  # 数值/引用问题：记成"无完整样本"，不掩盖故障本身
            problem = f"无法形成完整奖励样本：{failure}"
    return EpisodeRun(
        result=result,
        wall_s=wall_s,
        reward=reward,
        reward_problem=problem,
        confirmed_actions=len(confirmed_action_indices(result)),
        unknown_actions=sum(1 for transition in result.transitions
                            if transition.status == "unknown"),
        truncated_by_decisions=(result.ending == "truncated"
                                and result.limit_reason == "decisions"),
    )


def bootstrap_value(model, environment: Environment, snapshot: Any) -> float:
    """截断局的 ``V_old(s_T)``：用**本轮冻结行为模型**的最后公开快照求值。

    终止快照或仍在运行的快照都可能没有候选；没有候选就没有 V —— 这种情形必须显式失败，
    不能悄悄按 0 bootstrap（那等于把"不知道"记成"没有未来收益"）。
    """
    evaluation = model.predict(snapshot, catalog=environment.catalog)
    return float(evaluation.value)
