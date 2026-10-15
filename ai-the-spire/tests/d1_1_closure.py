"""D1.1 有限修复包的验收检查（故障路径 + 预算账目 + 指标口径 + 旧 run 拒绝）。

任务包：`docs/design/battle-learning-next-step-v1.md` §2。这些检查**替换不了** D1 的 192 项验收：
它们专门验证 D1 未覆盖的真实缺口，并且必须走**真正的 train 调用链**、核对**磁盘产物**
（不能只看 runner 的内存返回值）。

用法（仓库根目录，需要先 ``dotnet build AutoTheSpire.sln``）::

    python ai-the-spire/tests/d1_1_closure.py
    python ai-the-spire/tests/d1_1_closure.py --only fault
    python ai-the-spire/tests/d1_1_closure.py --list

分组：

* ``fault``   —— 一次确认动作后断连：当前局公开事实落盘、结束类别正确、该轮不提交、非零退出；
* ``budget``  —— 未提交轮消耗跨 resume 保留、剩余动作不足不超采、时间耗尽不启动 Host；
* ``metric``  —— 原始确认动作数是**描述性**指标，不因更多/更少自动判改善或变差；
* ``legacy``  —— 没有累计账目的旧 run 可以查看，但 resume 明确拒绝。

允许注入受控故障与受控时钟（**不为等待超限真的跑 900 秒**），不访问 final/未来保留种子，
不覆盖任何已有产物（全部写在临时目录里）。

退出码：0 全通过、1 有用例失败、2 参数或环境错误。最后一行固定为 ``TOTAL: ...``。
"""

from __future__ import annotations

import argparse
import json
import pathlib
import shutil
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "ai-the-spire"))

SMOKE_CONFIG = REPO / "ai-the-spire" / "configs" / "battle-demo-smoke-v1.json"
GROUPS = ("fault", "budget", "metric", "legacy")


class Checker:
    def __init__(self) -> None:
        self.passed = 0
        self.failed = 0
        self.details: list[str] = []

    def check(self, case: str, condition: bool, detail: str = "") -> bool:
        if condition:
            self.passed += 1
        else:
            self.failed += 1
            self.details.append(f"  - {case}: {detail}")
        return condition

    def equal(self, case: str, expected, actual) -> bool:
        return self.check(case, expected == actual, f"expected {expected!r}, got {actual!r}")


def workspace() -> pathlib.Path:
    root = pathlib.Path(tempfile.mkdtemp(prefix="d1_1-closure-"))
    return root


def read_json(path: pathlib.Path) -> dict:
    return json.loads(pathlib.Path(path).read_text(encoding="utf-8"))


def read_metrics(run_dir: pathlib.Path) -> list[dict]:
    path = run_dir / "metrics.jsonl"
    if not path.is_file():
        return []
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def records(run_dir: pathlib.Path) -> list[dict]:
    directory = run_dir / "episodes" / "records"
    if not directory.is_dir():
        return []
    return [read_json(path) for path in sorted(directory.glob("*.json"))]


def train_args(config: pathlib.Path, output: pathlib.Path, *, stop_after: int | None = None):
    class _Args:
        pass

    args = _Args()
    args.command = "train"
    args.config = str(config)
    args.output = str(output)
    args.stop_after_round = stop_after
    return args


def resume_args(run: pathlib.Path, output: pathlib.Path):
    class _Args:
        pass

    args = _Args()
    args.command = "resume"
    args.run = str(run)
    args.output = str(output)
    return args


def killing_client_factory(train_module, *, kill_after_steps: int, log: dict,
                           arm_seed_range: tuple[int, int] = (10000, 20000)):
    """把真实 Host 客户端包一层：在**训练种子**的一局里第 N 次 step 之后杀掉宿主（受控故障注入）。

    注入的是**传输层丢失**，不改宿主行为、不改规则、不改种子；这正是"断连"的真实路径。
    只对训练种子区间生效：validation/evaluate 也用同一个客户端，故障必须落在采样局上，
    否则验的就不是"训练路径在故障时保住当前局"。
    """
    real_open = train_module.open_bounded_client

    def patched_open(profile):
        log["hosts_started"] = log.get("hosts_started", 0) + 1
        client = real_open(profile)
        real_step = client.step
        real_start = client.start
        state = {"calls": 0, "armed": False}

        def start(expected_identity, scenario_profile, *, seed=None):
            if seed is not None and arm_seed_range[0] <= int(seed) < arm_seed_range[1]:
                state["armed"] = True
            return real_start(expected_identity, scenario_profile, seed=seed)

        def step(*arguments, **keywords):
            if state["armed"]:
                state["calls"] += 1
            response = real_step(*arguments, **keywords)
            if state["armed"] and state["calls"] >= kill_after_steps and not log.get("killed"):
                log["killed"] = True
                client.kill()
            return response

        client.start = start  # type: ignore[assignment]
        client.step = step  # type: ignore[assignment]
        return client

    return real_open, patched_open


# --------------------------------------------------------------------------- #
# fault：一次确认动作后断连，事实必须落盘
# --------------------------------------------------------------------------- #
def _run_fault(checker: Checker, root: pathlib.Path) -> None:
    import ai_the_spire.delivery.train as train_module
    from ai_the_spire.delivery.errors import DeliveryError

    output = root / "fault-run"
    log: dict = {}
    real_open, patched = killing_client_factory(train_module, kill_after_steps=2, log=log)
    train_module.open_bounded_client = patched
    exit_code = None
    failure = None
    try:
        train_module.run(train_args(SMOKE_CONFIG, output))
        exit_code = 0
    except DeliveryError as error:
        exit_code = error.exit_code
        failure = error
    finally:
        train_module.open_bounded_client = real_open

    checker.check("fault/injection-fired", bool(log.get("killed")), "故障注入没有触发")
    checker.equal("fault/nonzero-exit", 1, exit_code)
    checker.check("fault/message-says-interrupted-or-unknown",
                  failure is not None and ("不确定" in str(failure) or "中断" in str(failure)
                                           or "故障" in str(failure)),
                  str(failure))

    # 磁盘事实：当前局的公开快照 + 结束类别必须真的写进了 episodes/。
    stored = records(output)
    checker.check("fault/record-written-to-disk", len(stored) >= 1, f"{len(stored)} 条记录")
    if not stored:
        return
    record = stored[0]
    checker.check("fault/ending-class-is-interrupted-or-faulted",
                  record["ending"]["class"] in ("interrupted", "faulted"),
                  record["ending"]["class"])
    checker.check("fault/not-recorded-as-win-or-loss",
                  record["ending"]["class"] not in ("terminal_win", "terminal_loss"),
                  record["ending"]["class"])
    checker.check("fault/no-fabricated-terminal", record["ending"]["terminal"] is None,
                  str(record["ending"]["terminal"]))
    checker.check("fault/keeps-confirmed-transitions",
                  record["summary"]["confirmed_transitions"] >= 1,
                  str(record["summary"]))
    checker.check("fault/keeps-public-snapshots",
                  len(record["transitions"]) >= 1
                  and all("snapshot" in transition for transition in record["transitions"]))
    for transition in record["transitions"]:
        if transition["outcome"] == "unknown":
            checker.check("fault/unknown-has-no-next-state",
                          transition["next_observation_id"] is None, str(transition))
            checker.check("fault/unknown-claims-no-candidate",
                          transition["candidate_id"] == "", str(transition))

    # 索引与记录一致（复用既有记录设施的复核）。
    from ai_the_spire.data.store import EpisodeStore

    store = EpisodeStore(output / "episodes")
    checker.check("fault/store-clean", not store.verify_all(), f"{store.verify_all()}")

    # metrics 里必须有这一行的失败原因，且标成"不是可用样本"。
    episode_lines = [item for item in read_metrics(output) if item.get("kind") == "episode"]
    checker.check("fault/metrics-line-written", len(episode_lines) >= 1, str(len(episode_lines)))
    if episode_lines:
        line = episode_lines[0]
        checker.check("fault/metrics-marks-usable-false",
                      line.get("usable_for_training") is False, str(line.get("usable_for_training")))
        checker.check("fault/metrics-has-failure-reason", bool(line.get("failure_reason")),
                      str(line.get("failure_reason")))
        checker.check("fault/metrics-has-confirmed-actions",
                      int(line.get("confirmed_actions", 0)) >= 1, str(line.get("confirmed_actions")))

    # 该轮**没有**提交：没有检查点、run-state 仍是 0 轮、退出类别是 fault。
    checker.check("fault/round-not-committed",
                  not (output / "checkpoints" / "round-0001").exists())
    state = read_json(output / "run-state.json")
    checker.equal("fault/run-state-rounds-zero", 0, state["rounds_completed"])
    summary = read_json(output / "summary.json")
    checker.equal("fault/summary-exit-category", "fault", summary["exit_category"])
    checker.check("fault/ledger-counts-actual-consumption",
                  bool(read_json(output / "budget-ledger.json")["confirmed_actions_used"] >= 1)
                  if (output / "budget-ledger.json").is_file() else False,
                  "账目没有记录故障局的已确认动作")
    if (output / "budget-ledger.json").is_file():
        ledger = read_json(output / "budget-ledger.json")
        checker.equal("fault/ledger-rounds-committed-zero", 0, ledger["rounds_committed"])
        checker.equal("fault/ledger-episodes-faulted", 1, ledger["episodes_faulted"])


# --------------------------------------------------------------------------- #
# budget：跨 resume 累计、剩余额度限流、时间耗尽不起 Host
# --------------------------------------------------------------------------- #
def _run_budget(checker: Checker, root: pathlib.Path) -> None:
    import ai_the_spire.delivery.train as train_module
    from ai_the_spire.delivery.budget import BudgetExhausted, load_ledger
    from ai_the_spire.delivery.config import load_profile
    from ai_the_spire.delivery.errors import DeliveryError

    profile = load_profile(SMOKE_CONFIG)

    # 1) 正常停下第一轮 → 拿到一份已有账目的 run。
    partial = root / "budget-partial"
    train_module.run(train_args(SMOKE_CONFIG, partial, stop_after=1))
    first = read_json(partial / "budget-ledger.json")
    checker.equal("budget/first-round-committed", 1, first["rounds_committed"])
    checker.check("budget/first-round-used-actions",
                  first["confirmed_actions_used"] >= 1, str(first["confirmed_actions_used"]))
    checker.equal("budget/actual-equals-committed-after-clean-round",
                  first["committed_actions"], first["confirmed_actions_used"])

    # 2) 在第二轮中途注入故障：这一轮的消耗**留在账上**（未提交也不返还）。
    faulted = root / "budget-faulted-resume"
    log: dict = {}
    real_open, patched = killing_client_factory(train_module, kill_after_steps=2, log=log)
    train_module.open_bounded_client = patched
    try:
        train_module.run_resume(resume_args(partial, faulted))
        checker.check("budget/faulted-resume-fails", False, "注入故障后 resume 竟然返回 0")
    except DeliveryError:
        checker.check("budget/faulted-resume-fails", True)
    finally:
        train_module.open_bounded_client = real_open

    second = read_json(faulted / "budget-ledger.json")
    checker.check("budget/uncommitted-consumption-retained",
                  second["confirmed_actions_used"] > first["confirmed_actions_used"],
                  f"{first['confirmed_actions_used']} → {second['confirmed_actions_used']}")
    checker.equal("budget/committed-state-not-advanced", 1, second["rounds_committed"])
    checker.equal("budget/committed-actions-unchanged",
                  first["committed_actions"], second["committed_actions"])
    checker.check("budget/fault-counted", second["episodes_faulted"] >= 1, str(second))

    # 3) 再 resume 一次：继承的是**故障后**的账，不是原始账（不返还预算）。
    third = root / "budget-after-fault-resume"
    train_module.run_resume(resume_args(faulted, third))
    inherited = read_json(third / "budget-ledger.json")
    checker.check("budget/resume-inherits-faulted-consumption",
                  inherited["confirmed_actions_used"] >= second["confirmed_actions_used"],
                  f"{second['confirmed_actions_used']} → {inherited['confirmed_actions_used']}")
    checker.check("budget/inherited-source-recorded",
                  inherited["inherited_from"] == str(faulted.resolve()),
                  str(inherited["inherited_from"]))
    checker.equal("budget/total-rounds-not-increased", 2, inherited["rounds_committed"])
    # 关键：未提交轮的消耗不改变**训练位置**（权重/Adam 仍等于连续两轮的结果）。
    checker.check("budget/training-position-is-two-rounds",
                  (third / "checkpoints" / "round-0002").is_dir())

    # 4) 剩余动作不足时不超采：把动作上限压到 50（第 1 轮已用 46 ⇒ 只剩 4）。
    ledger = load_ledger(partial / "budget-ledger.json", profile, where="check")
    ledger.max_train_actions = 50
    ledger.path = root / "probe-ledger.json"
    ledger.write()
    probe_profile = load_profile(SMOKE_CONFIG)
    limited = root / "budget-limited"
    real_run = train_module.run
    real_load = train_module.load_profile

    def patched_load(_path):
        import dataclasses

        base = real_load(SMOKE_CONFIG)
        return dataclasses.replace(
            base, budget={**base.budget, "max_train_actions": 50})

    train_module.load_profile = patched_load
    try:
        real_run(train_args(SMOKE_CONFIG, limited))
        checker.check("budget/limited-run-stops-nonzero", False, "额度耗尽却返回 0")
    except DeliveryError as error:
        checker.check("budget/limited-run-stops-nonzero", error.exit_code == 1,
                      f"exit={error.exit_code} reason={error.reason}")
        checker.check("budget/limited-run-says-exhausted",
                      "耗尽" in str(error), str(error))
    finally:
        train_module.load_profile = real_load

    limited_ledger = read_json(limited / "budget-ledger.json")
    checker.equal("budget/never-oversamples", 50, limited_ledger["confirmed_actions_used"])
    checker.check("budget/remaining-zero-after-limit", limited_ledger["confirmed_actions_used"]
                  <= limited_ledger["max_train_actions"])
    limited_episodes = [item for item in read_metrics(limited) if item.get("kind") == "episode"]
    checker.check("budget/last-episode-clipped-to-remaining",
                  any(item["confirmed_actions"] <= 4 for item in limited_episodes[1:]),
                  str([item["confirmed_actions"] for item in limited_episodes]))
    checker.check("budget/no-new-round-after-exhaustion",
                  not (limited / "checkpoints" / "round-0002").exists())
    # 第 2 轮开了 1 局（被削到 4 个动作）后额度归零，不应再开新局。
    checker.equal("budget/episodes-started-bounded",
                  3, limited_ledger["episodes_started"])

    # 5) 累计时间耗尽 ⇒ **不启动 Host、不开新局**。
    import dataclasses

    exhausted = root / "budget-time-exhausted"
    host_calls = {"count": 0}

    def forbidden_open(_profile):  # pragma: no cover - 被调用即失败
        host_calls["count"] += 1
        raise AssertionError("累计时间耗尽时不得启动 Host")

    def zero_time_load(_path):
        base = real_load(SMOKE_CONFIG)
        return dataclasses.replace(
            base, budget={**base.budget, "train_command_time_limit_s": 0.0})

    real_open_train = train_module.open_bounded_client
    train_module.load_profile = zero_time_load
    train_module.open_bounded_client = forbidden_open
    try:
        train_module.run(train_args(SMOKE_CONFIG, exhausted))
        checker.check("budget/time-exhausted-stops", False, "时间耗尽却返回 0")
    except DeliveryError as error:
        checker.check("budget/time-exhausted-stops", error.exit_code == 1, str(error))
        checker.check("budget/time-exhausted-message", "时间" in str(error) or "耗尽" in str(error),
                      str(error))
    except AssertionError as error:
        checker.check("budget/time-exhausted-no-host", False, str(error))
    finally:
        train_module.load_profile = real_load
        train_module.open_bounded_client = real_open_train
    checker.equal("budget/time-exhausted-host-not-started", 0, host_calls["count"])

    # 6) 来源链已耗尽 ⇒ resume 也不启动 Host（账目里的累计时间已经超限）。
    stuffed = root / "stuffed-ledger-source"
    shutil.copytree(partial, stuffed)
    ledger_path = stuffed / "budget-ledger.json"
    document = read_json(ledger_path)
    document["training_seconds_used"] = float(profile.budget["train_command_time_limit_s"]) + 1.0
    ledger_path.write_text(json.dumps(document, ensure_ascii=False, indent=2), encoding="utf-8")
    host_calls["count"] = 0
    train_module.open_bounded_client = forbidden_open
    try:
        train_module.run_resume(resume_args(stuffed, root / "resume-time-exhausted"))
        checker.check("budget/resume-time-exhausted-stops", False, "resume 竟然返回 0")
    except DeliveryError as error:
        checker.check("budget/resume-time-exhausted-stops", error.exit_code == 1, str(error))
    except AssertionError as error:
        checker.check("budget/resume-time-exhausted-no-host", False, str(error))
    finally:
        train_module.open_bounded_client = real_open_train
    checker.equal("budget/resume-time-exhausted-host-not-started", 0, host_calls["count"])

    # 7) guard_before_episode 的边界：额度为 0 时直接拒绝（纯函数级，可判别）。
    from ai_the_spire.delivery.budget import BudgetLedger

    probe = BudgetLedger(path=root / "x.json", profile_id="p", config_digest="d",
                         max_train_actions=10, train_command_time_limit_s=100.0,
                         confirmed_actions_used=10)
    try:
        probe.guard_before_episode(active_seconds=0.0, where="probe")
        checker.check("budget/zero-remaining-rejected", False, "额度为 0 却允许开新局")
    except BudgetExhausted:
        checker.check("budget/zero-remaining-rejected", True)
    probe.confirmed_actions_used = 6
    checker.equal("budget/remaining-actions-clips-episode", 4,
                  probe.guard_before_episode(active_seconds=0.0, where="probe"))


# --------------------------------------------------------------------------- #
# metric：确认动作数是描述性指标
# --------------------------------------------------------------------------- #
def _run_metric(checker: Checker, root: pathlib.Path) -> None:
    import ai_the_spire.delivery.evaluate as evaluate_module

    output = root / "metric-run"
    evaluate_module.run(_evaluate_args(REPO / "artifacts" / "d1" / "train", output, "validation"))
    comparison = read_json(output / "comparison.json")
    plan = comparison["plan"]
    metrics = {item["metric_id"]: item for item in plan["metrics"]}
    steps = metrics.get(evaluate_module.METRIC_STEPS)
    checker.check("metric/steps-metric-present", steps is not None)
    if steps:
        checker.equal("metric/steps-direction-descriptive", "descriptive", steps["direction"])
    reward = metrics.get(evaluate_module.METRIC_REWARD)
    checker.check("metric/reward-keeps-direction",
                  reward is not None and reward["direction"] == "higher_is_better", str(reward))
    win = metrics.get(evaluate_module.METRIC_TERMINAL_WIN)
    checker.check("metric/terminal-win-keeps-direction",
                  win is not None and win["direction"] == "higher_is_better", str(win))

    verdicts = {item["metric_id"]: item["verdict"] for item in comparison["report"]["comparisons"]}
    checker.check("metric/steps-never-judged-improved-or-worse",
                  all(value == "descriptive"
                      for key, value in verdicts.items() if key == evaluate_module.METRIC_STEPS),
                  str(verdicts))
    checker.check("metric/reward-still-paired",
                  evaluate_module.METRIC_REWARD in verdicts)
    markdown = (output / "comparison.md").read_text(encoding="utf-8")
    checker.check("metric/md-notes-descriptive",
                  "描述" in markdown, markdown[:200])
    _ = root


def _evaluate_args(run: pathlib.Path, output: pathlib.Path, split: str):
    class _Args:
        pass

    args = _Args()
    args.command = "evaluate"
    args.run = str(run)
    args.split = split
    args.output = str(output)
    return args


# --------------------------------------------------------------------------- #
# legacy：没有账目的旧 run
# --------------------------------------------------------------------------- #
def _run_legacy(checker: Checker, root: pathlib.Path) -> None:
    import ai_the_spire.delivery.train as train_module
    from ai_the_spire.delivery.errors import DeliveryError

    legacy = REPO / "artifacts" / "d1" / "smoke-partial"
    if not legacy.is_dir():
        checker.check("legacy/d1-smoke-partial-available", False, str(legacy))
        return
    checker.check("legacy/source-has-no-ledger",
                  not (legacy / "budget-ledger.json").exists(),
                  "D1 旧产物竟然已经有账目")
    try:
        train_module.run_resume(resume_args(legacy, root / "legacy-resume"))
        checker.check("legacy/resume-refused", False, "resume 没有拒绝没有账目的旧 run")
    except DeliveryError as error:
        checker.check("legacy/resume-refused", error.exit_code == 2, f"exit={error.exit_code}")
        checker.check("legacy/refusal-explains-why", "账目" in str(error), str(error))
    checker.check("legacy/no-partial-output-left",
                  not (root / "legacy-resume" / "candidate-model").exists())


_RUNNERS = {
    "fault": _run_fault,
    "budget": _run_budget,
    "metric": _run_metric,
    "legacy": _run_legacy,
}


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="D1.1 故障/预算/指标/旧 run 验收")
    parser.add_argument("--only", action="append", choices=GROUPS, default=None)
    parser.add_argument("--list", action="store_true")
    args = parser.parse_args(argv)
    if args.list:
        for name in GROUPS:
            print(name)
        return 0

    selected = args.only or list(GROUPS)
    root = workspace()
    print(f"[d1.1] 工作目录：{root}")
    total_passed = total_failed = total_executed = 0
    for name in selected:
        checker = Checker()
        try:
            _RUNNERS[name](checker, root)
        except Exception as failure:  # noqa: BLE001 - 组异常按失败计
            import traceback

            traceback.print_exc()
            checker.check(f"{name}/group-completed", False, f"{type(failure).__name__}: {failure}")
        total_passed += checker.passed
        total_failed += checker.failed
        total_executed += checker.passed + checker.failed
        status = "PASS" if checker.failed == 0 and checker.passed > 0 else "FAIL"
        print(f"[{status}] {name}: {checker.passed} passed, {checker.failed} failed")
        for detail in checker.details:
            print(detail)
    print(f"TOTAL: {total_passed} passed, {total_failed} failed, {total_executed} executed")
    if total_executed == 0:
        return 2
    return 1 if total_failed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
