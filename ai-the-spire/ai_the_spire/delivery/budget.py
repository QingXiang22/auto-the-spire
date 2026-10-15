"""run 级**资源账目**（D1.1-B）：把"实际消耗"和"已提交训练状态"分开记账。

为什么必须分开（负责人复核 §10.3 的可定位证据）：

* 旧实现采完一整轮才检查动作上限、更新成功后才累计 —— 一轮中途故障或未提交时，
  这一轮**真实消耗掉的**动作与时间在账面上消失，resume 会把额度"还"回来；
* 旧 resume 重建一个完整的 900s/180s 预算，只看已提交检查点里的动作计数 ——
  暂停期间的墙钟被当成没发生，来源链的消耗也丢了。

本模块的规则：

1. **实际消耗**（`confirmed_actions_used` / `training_seconds_used`）只增不减：
   故障局、未知执行、未提交轮次的消耗都留在账上，**禁止以未提交为由返还**；
2. **已提交状态**（`committed_actions` / `rounds_committed`）只在真正提交更新后前移；
3. 时间只记**训练命令内的活动墙钟**（暂停期间不计）；
4. 每局开始前按**剩余额度**限流（本局动作上限 = min(每局限额, 剩余额度)）；
5. 额度或时间耗尽时**不启动 Host、不开启新局**；
6. resume 继承来源链的整本账（含 `inherited_from`），不从零开始；
7. 没有账目的旧 run 可以查看/评估，但 resume **明确拒绝**（不猜剩余预算）。
"""

from __future__ import annotations

import json
import pathlib
from dataclasses import dataclass, field
from typing import Mapping, Optional

from .errors import FaultError, UsageError

#: 账目格式版本。
LEDGER_FORMAT = "d1-budget-ledger@1"

#: 账目文件名（放在 run 目录里，随 resume 复制）。
LEDGER_FILE = "budget-ledger.json"


class BudgetExhausted(FaultError):
    """额度/时间耗尽：属于资源限额，保存已有产物后非零退出，不开启新局。"""


@dataclass
class BudgetLedger:
    """一个 run（含 resume 来源链）的训练资源账目。"""

    path: pathlib.Path
    profile_id: str
    config_digest: str
    max_train_actions: int
    train_command_time_limit_s: float
    confirmed_actions_used: int = 0
    unknown_actions_used: int = 0
    committed_actions: int = 0
    training_seconds_used: float = 0.0
    rounds_committed: int = 0
    episodes_started: int = 0
    episodes_completed: int = 0
    episodes_faulted: int = 0
    inherited_from: Optional[str] = None
    #: 本次命令开始时账上已有的耗时（用于把"本命令的活动墙钟"加到累计值上）。
    inherited_seconds: float = field(default=0.0, compare=False)

    # -- 读取 ------------------------------------------------------------------------------------ #
    @property
    def remaining_actions(self) -> int:
        return max(self.max_train_actions - self.confirmed_actions_used, 0)

    def remaining_seconds(self, active_seconds: float) -> float:
        """本命令还能用多少秒（累计口径：来源链已用 + 本命令已用）。"""
        return max(self.train_command_time_limit_s
                   - self.inherited_seconds - float(active_seconds), 0.0)

    def total_seconds_used(self, active_seconds: float) -> float:
        return self.inherited_seconds + float(active_seconds)

    # -- 记账 ------------------------------------------------------------------------------------ #
    def note_episode(self, run, *, committed: bool) -> None:
        """一局结束后记账：**实际消耗永远累计**，与这一轮是否提交无关。

        "完成"只在整轮提交时统一结算（:meth:`note_round_committed`）；这里只负责
        开始计数、动作/未知执行消耗，以及"这一局确实故障或中断了"。
        """
        self.episodes_started += 1
        self.confirmed_actions_used += int(run.confirmed_actions)
        self.unknown_actions_used += int(getattr(run, "unknown_actions", 0))
        if not committed and run.ending not in ("terminal", "truncated"):
            self.episodes_faulted += 1

    def note_round_committed(self, *, actions_in_round: int, rounds_completed: int,
                             episodes_usable_in_round: int) -> None:
        self.committed_actions += int(actions_in_round)
        self.rounds_committed = int(rounds_completed)
        self.episodes_completed += int(episodes_usable_in_round)

    def bind_clock(self, active_seconds: float) -> None:
        self.training_seconds_used = self.total_seconds_used(active_seconds)

    # -- 限流 ------------------------------------------------------------------------------------ #
    def guard_before_episode(self, *, active_seconds: float, where: str) -> int:
        """开新局之前的检查；返回本局允许的动作上限。

        额度或累计时间已耗尽 ⇒ :class:`BudgetExhausted`（调用方**不得**再启动 Host/新局）。
        """
        if self.remaining_actions <= 0:
            raise BudgetExhausted(
                f"{where}: 训练动作额度已耗尽（已用 {self.confirmed_actions_used}/"
                f"{self.max_train_actions}）；不开启新局，保留已提交检查点后非零退出"
            )
        if self.remaining_seconds(active_seconds) <= 0.0:
            raise BudgetExhausted(
                f"{where}: 训练累计时间已耗尽（来源链 {self.inherited_seconds:.3f}s + 本次 "
                f"{active_seconds:.3f}s ≥ 限额 {self.train_command_time_limit_s:.1f}s）；"
                "不启动 Host、不开启新局"
            )
        return self.remaining_actions

    def guard_before_command(self) -> None:
        """创建 Host 之前的检查：来源链已耗尽时连 Host 都不该起。"""
        if self.remaining_actions <= 0:
            raise BudgetExhausted(
                f"来源链训练动作额度已耗尽（{self.confirmed_actions_used}/{self.max_train_actions}）："
                "不启动 Host；只能查看/评估已有产物"
            )
        if self.inherited_seconds >= self.train_command_time_limit_s:
            raise BudgetExhausted(
                f"来源链训练累计时间已耗尽（{self.inherited_seconds:.3f}s ≥ "
                f"{self.train_command_time_limit_s:.1f}s）：不启动 Host；只能查看/评估已有产物"
            )

    # -- 落盘 ------------------------------------------------------------------------------------ #
    def as_wire(self) -> dict:
        return {
            "budget_ledger_format_version": LEDGER_FORMAT,
            "profile_id": self.profile_id,
            "config_digest": self.config_digest,
            "max_train_actions": int(self.max_train_actions),
            "train_command_time_limit_s": self.train_command_time_limit_s,
            "confirmed_actions_used": int(self.confirmed_actions_used),
            "unknown_actions_used": int(self.unknown_actions_used),
            "committed_actions": int(self.committed_actions),
            "training_seconds_used": float(self.training_seconds_used),
            "rounds_committed": int(self.rounds_committed),
            "episodes_started": int(self.episodes_started),
            "episodes_completed": int(self.episodes_completed),
            "episodes_faulted": int(self.episodes_faulted),
            "inherited_from": self.inherited_from,
            "updated_at_utc": _utc_now(),
        }

    def write(self) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text(
            json.dumps(self.as_wire(), ensure_ascii=False, sort_keys=True, indent=2) + "\n",
            encoding="utf-8",
        )


def _utc_now() -> str:
    import datetime

    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def new_ledger(path: pathlib.Path, profile, *, inherited_from: Optional[str] = None) -> BudgetLedger:
    return BudgetLedger(
        path=path,
        profile_id=profile.profile_id,
        config_digest=profile.config_digest,
        max_train_actions=int(profile.budget["max_train_actions"]),
        train_command_time_limit_s=float(profile.budget["train_command_time_limit_s"]),
        inherited_from=inherited_from,
    )


def load_ledger(path: pathlib.Path, profile, *, where: str) -> BudgetLedger:
    """读出来源链的账目；缺账目/格式不符/身份不符一律**明确拒绝**（不猜剩余预算）。"""
    if not path.is_file():
        raise UsageError(
            f"{where}: 来源 run 没有 {LEDGER_FILE}（D1.1 之前的旧产物没有累计账目）；"
            "可以查看/评估，但 resume 拒绝猜剩余预算——请用当前代码重新跑一次 train 再 resume"
        )
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as failure:
        raise UsageError(f"{where}: {path} 不是合法 JSON（{failure}）") from failure
    if not isinstance(document, Mapping):
        raise UsageError(f"{where}: {LEDGER_FILE} 必须是对象")
    if document.get("budget_ledger_format_version") != LEDGER_FORMAT:
        raise UsageError(
            f"{where}: 账目版本 {document.get('budget_ledger_format_version')!r} "
            f"与当前 {LEDGER_FORMAT!r} 不同（不静默迁移）"
        )
    if document.get("config_digest") != profile.config_digest:
        raise UsageError(
            f"{where}: 账目的 config_digest 与当前配置不一致（拒绝混用来源）："
            f"{document.get('config_digest')!r} != {profile.config_digest!r}"
        )
    ledger = BudgetLedger(
        path=path,
        profile_id=str(document["profile_id"]),
        config_digest=str(document["config_digest"]),
        max_train_actions=int(document["max_train_actions"]),
        train_command_time_limit_s=float(document["train_command_time_limit_s"]),
        confirmed_actions_used=int(document["confirmed_actions_used"]),
        unknown_actions_used=int(document.get("unknown_actions_used", 0)),
        committed_actions=int(document.get("committed_actions", 0)),
        training_seconds_used=float(document["training_seconds_used"]),
        rounds_committed=int(document.get("rounds_committed", 0)),
        episodes_started=int(document.get("episodes_started", 0)),
        episodes_completed=int(document.get("episodes_completed", 0)),
        episodes_faulted=int(document.get("episodes_faulted", 0)),
        inherited_from=document.get("inherited_from"),
    )
    if ledger.max_train_actions != int(profile.budget["max_train_actions"]):
        raise UsageError(
            f"{where}: 账目的动作上限 {ledger.max_train_actions} 与当前 Profile "
            f"{profile.budget['max_train_actions']} 不一致（换预算就是新 profile）"
        )
    if ledger.training_seconds_used < 0 or ledger.confirmed_actions_used < 0:
        raise UsageError(f"{where}: 账目出现负数消耗（记录被改动过）")
    return ledger


def ledger_report(ledger: BudgetLedger, *, active_seconds: float) -> dict:
    """给 summary/metrics 的账目快照（实际消耗与已提交状态分开列）。"""
    return {
        "max_train_actions": ledger.max_train_actions,
        "confirmed_actions_used": ledger.confirmed_actions_used,
        "unknown_actions_used": ledger.unknown_actions_used,
        "remaining_actions": ledger.remaining_actions,
        "committed_actions": ledger.committed_actions,
        "rounds_committed": ledger.rounds_committed,
        "training_seconds_used": ledger.total_seconds_used(active_seconds),
        "inherited_seconds": ledger.inherited_seconds,
        "train_command_time_limit_s": ledger.train_command_time_limit_s,
        "episodes_started": ledger.episodes_started,
        "episodes_completed": ledger.episodes_completed,
        "episodes_faulted": ledger.episodes_faulted,
        "inherited_from": ledger.inherited_from,
    }
