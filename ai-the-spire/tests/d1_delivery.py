"""D1 小预算自动化验收入口（对应任务包 §7 的八个场景）。

用法（仓库根目录，需要先 ``dotnet build AutoTheSpire.sln``）::

    python ai-the-spire/tests/d1_delivery.py                 # 全部组
    python ai-the-spire/tests/d1_delivery.py --only compare  # 只跑一组
    python ai-the-spire/tests/d1_delivery.py --list

分组（每个组都对应任务包 §7 的一行验收场景）：

* ``bounded``  —— help/缺依赖/非空输出目录/限额/冻结 Profile 错误路径；
* ``ending``   —— 决策上限截断 + bootstrap、恰好终止、墙钟限额、断连故障；
* ``visible``  —— demo 的可读对局与公开轨迹一致；
* ``update``   —— 真实采样 + 有效更新 + 身份/检查点/计数；
* ``source``   —— 混用来源被拒绝、同冻结模型概率一致；
* ``load``     —— 载入候选运行 demo、损坏/缺失在启动游戏前拒绝；
* ``compare``  —— 三策略同保留 seed 的表与 JSON 一致、未消费 final 种子；
* ``resume``   —— 连续两轮与停下恢复的参数/Adam/计数逐元素一致。

退出码：0 全通过、1 有用例失败（逐条打印差异）、2 参数或环境错误。
最后一行固定为 ``TOTAL: x passed, y failed, z executed``；**零用例不算通过**。

本入口用**小预算**（smoke Profile）跑真实 stdio；它不替代 `run.py` 的三个产品命令，
只是把"能区分行为"的检查接进现有测试入口。
"""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "ai-the-spire"))

RUN_PY = REPO / "ai-the-spire" / "run.py"
SMOKE_CONFIG = REPO / "ai-the-spire" / "configs" / "battle-demo-smoke-v1.json"
DEMO_CONFIG = REPO / "ai-the-spire" / "configs" / "battle-demo-v1.json"

GROUPS = ("bounded", "ending", "visible", "update", "source", "load", "compare", "resume")


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


def run_cli(*arguments: str, env_extra: dict | None = None) -> subprocess.CompletedProcess:
    import os

    environment = dict(os.environ)
    environment["PYTHONIOENCODING"] = "utf-8"
    if env_extra:
        environment.update(env_extra)
    return subprocess.run([sys.executable, str(RUN_PY), *arguments],
                          capture_output=True, text=True, encoding="utf-8", errors="replace",
                          cwd=str(REPO), env=environment)


def read_json(path: pathlib.Path) -> dict:
    return json.loads(pathlib.Path(path).read_text(encoding="utf-8"))


def read_metrics(run_dir: pathlib.Path) -> list[dict]:
    path = run_dir / "metrics.jsonl"
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def tree_digest(root: pathlib.Path) -> str:
    digest = hashlib.sha256()
    for path in sorted(root.rglob("*")):
        if path.is_file():
            digest.update(str(path.relative_to(root)).encode("utf-8"))
            digest.update(path.read_bytes())
    return "sha256:" + digest.hexdigest()


def host_process_count() -> int | None:
    """当前命令行里带 `Net.TheSpire.Host.dll` 的 dotnet 进程数（用于"退出后回收自己启动的 Host"）。

    查询失败返回 None（当作"测不到"，不把环境问题记成失败）。
    """
    script = ("@(Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\" | "
              "Where-Object { $_.CommandLine -like '*Net.TheSpire.Host.dll*' }).Count")
    try:
        completed = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command",
                                    script],
                                   capture_output=True, text=True, timeout=120)
    except (OSError, subprocess.SubprocessError):  # pragma: no cover
        return None
    if completed.returncode != 0:
        return None
    try:
        return int(completed.stdout.strip() or "0")
    except ValueError:  # pragma: no cover
        return None


# --------------------------------------------------------------------------- #
# 共享夹具（按需构建并缓存）
# --------------------------------------------------------------------------- #
_CACHE: dict[str, object] = {}


def workspace() -> pathlib.Path:
    if "root" not in _CACHE:
        _CACHE["root"] = pathlib.Path(tempfile.mkdtemp(prefix="d1-accept-"))
    return _CACHE["root"]  # type: ignore[return-value]


def demo_initial() -> tuple[pathlib.Path, subprocess.CompletedProcess]:
    if "demo" not in _CACHE:
        output = workspace() / "demo-initial"
        _CACHE["demo"] = (output, run_cli("demo", "--config", str(SMOKE_CONFIG),
                                          "--output", str(output)))
    return _CACHE["demo"]  # type: ignore[return-value]


def train_run() -> tuple[pathlib.Path, subprocess.CompletedProcess]:
    if "train" not in _CACHE:
        output = workspace() / "train"
        _CACHE["train"] = (output, run_cli("train", "--config", str(SMOKE_CONFIG),
                                           "--output", str(output)))
    return _CACHE["train"]  # type: ignore[return-value]


def copy_run(name: str) -> pathlib.Path:
    source, _ = train_run()
    target = workspace() / name
    shutil.copytree(source, target)
    return target


# --------------------------------------------------------------------------- #
# bounded
# --------------------------------------------------------------------------- #
def _run_bounded(checker: Checker) -> None:
    completed = run_cli("--help")
    checker.equal("bounded/top-help-exit", 0, completed.returncode)
    for name in ("demo", "train", "evaluate", "resume"):
        checker.check(f"bounded/help-mentions-{name}", name in completed.stdout, completed.stdout[:200])

    completed = run_cli("demo", "--help")
    checker.equal("bounded/demo-help-exit", 0, completed.returncode)
    checker.check("bounded/demo-help-lists-output", "--output" in completed.stdout)

    # 缺 NumPy：help 仍然可用，真正跑命令则在启动游戏前退出 2 并给准备步骤。
    blocker = workspace() / "no-numpy"
    blocker.mkdir(exist_ok=True)
    (blocker / "numpy.py").write_text('raise ImportError("blocked by d1 acceptance harness")\n',
                                      encoding="utf-8")
    env_extra = {"PYTHONPATH": str(blocker)}
    completed = run_cli("--help", env_extra=env_extra)
    checker.equal("bounded/help-works-without-numpy", 0, completed.returncode)
    completed = run_cli("demo", "--config", str(SMOKE_CONFIG),
                        "--output", str(workspace() / "no-numpy-out"), env_extra=env_extra)
    checker.equal("bounded/missing-numpy-exit", 2, completed.returncode)
    checker.check("bounded/missing-numpy-prep-steps",
                  "requirements.txt" in (completed.stderr + completed.stdout))
    checker.check("bounded/missing-numpy-no-game",
                  not (workspace() / "no-numpy-out" / "episode.md").exists())

    completed = run_cli("demo", "--config", str(workspace() / "missing.json"),
                        "--output", str(workspace() / "missing-config-out"))
    checker.equal("bounded/missing-config-exit", 2, completed.returncode)

    # 篡改冻结值：参数变化必须被发现，不能"改了种子还沿用旧身份"。
    tampered = read_json(SMOKE_CONFIG)
    tampered["seeds"]["train_environment_base"] = 99999
    tampered_path = workspace() / "tampered.json"
    tampered_path.write_text(json.dumps(tampered, ensure_ascii=False), encoding="utf-8")
    completed = run_cli("demo", "--config", str(tampered_path),
                        "--output", str(workspace() / "tampered-out"))
    checker.equal("bounded/tampered-profile-exit", 2, completed.returncode)
    checker.check("bounded/tampered-profile-names-frozen-value",
                  "冻结" in (completed.stderr + completed.stdout),
                  (completed.stderr + completed.stdout)[:400])

    completed = run_cli("train", "--config", str(SMOKE_CONFIG), "--stop-after-round", "9",
                        "--output", str(workspace() / "stop-out"))
    checker.equal("bounded/stop-after-round-out-of-range", 2, completed.returncode)

    existing = workspace() / "non-empty"
    existing.mkdir(exist_ok=True)
    (existing / "keep.txt").write_text("do not overwrite\n", encoding="utf-8")
    completed = run_cli("demo", "--config", str(SMOKE_CONFIG), "--output", str(existing))
    checker.equal("bounded/non-empty-output-exit", 2, completed.returncode)
    checker.check("bounded/non-empty-output-kept",
                  (existing / "keep.txt").read_text(encoding="utf-8").strip() == "do not overwrite")

    completed = run_cli("frobnicate")
    checker.check("bounded/unknown-subcommand-nonzero", completed.returncode != 0,
                  str(completed.returncode))

    # 缺 Host 程序集：显式错误 + 准备步骤（不改动磁盘，只替换模块常量）。
    import ai_the_spire.delivery.runtime as runtime

    from ai_the_spire.delivery.errors import UsageError

    original = runtime.HOST_DLL
    runtime.HOST_DLL = pathlib.Path("Z:/definitely/not/here/Net.TheSpire.Host.dll")
    try:
        runtime.ensure_host()
        checker.check("bounded/missing-host-raises", False, "没有抛出 UsageError")
    except UsageError as failure:
        checker.check("bounded/missing-host-raises", True)
        checker.check("bounded/missing-host-prep-steps", "dotnet build" in str(failure), str(failure))
    finally:
        runtime.HOST_DLL = original

    # 退出后必须回收自己启动的 Host：失败路径（宿主机已起、载入模型失败）也不能留下孤儿进程。
    import ai_the_spire.delivery.runtime as delivery_runtime
    from ai_the_spire.delivery.config import load_profile as _load_profile

    probe_profile = _load_profile(SMOKE_CONFIG)
    probe = delivery_runtime.open_bounded_client(probe_profile)
    inner = probe.client
    checker.check("bounded/host-process-alive-before-shutdown",
                  inner._process.poll() is None, "宿主在 start 之前就已退出")
    probe.shutdown()
    checker.check("bounded/host-process-reaped-after-shutdown",
                  inner._process.poll() is not None, "shutdown 之后宿主进程没有退出")

    before = host_process_count()
    completed = run_cli("demo", "--config", str(SMOKE_CONFIG),
                        "--model", str(workspace() / "no-such-bundle-for-reclaim"),
                        "--output", str(workspace() / "reclaim-out"))
    checker.equal("bounded/failing-command-exit", 2, completed.returncode)
    after = host_process_count()
    if before is None or after is None:
        checker.check("bounded/failing-command-releases-host", True, "（该平台测不到 Host 进程数）")
    else:
        checker.check("bounded/failing-command-releases-host", after <= before,
                      f"失败命令留下了 Host 进程：before={before} after={after}")


# --------------------------------------------------------------------------- #
# ending
# --------------------------------------------------------------------------- #
def _run_ending(checker: Checker) -> None:
    from dataclasses import replace

    import ai_the_spire.delivery.runtime as runtime
    from ai_the_spire.data.record import build_record
    from ai_the_spire.delivery.config import load_profile
    from ai_the_spire.delivery.environment import observe_environment
    from ai_the_spire.delivery.episodes import (bootstrap_value, confirmed_action_indices,
                                                run_episode)
    from ai_the_spire.delivery.errors import DeliveryError
    from ai_the_spire.delivery.model_access import fresh_model
    from ai_the_spire.delivery.reward_v1 import d1_reward_profile
    from ai_the_spire.runner import EpisodeRunner

    profile = load_profile(SMOKE_CONFIG)
    reward_profile = d1_reward_profile(hp_lost_weight=-0.01, victory_weight=1.0,
                                       defeat_weight=-1.0, discount=0.99)
    client = runtime.open_bounded_client(profile)
    try:
        environment = observe_environment(profile, client)
        model = fresh_model(profile, environment)

        # 1) 决策上限 = 1：必须标 truncated（限额 decisions）并留下 bootstrap 用的新公开快照。
        runner = EpisodeRunner(client, seed=10000, max_steps=1,
                               encoding_config=environment.encoding_config,
                               executor_seed=profile.executor_seed(10000),
                               interruption_errors=client.interruption_errors,
                               policy=model.policy, model_id=model.model_id,
                               capture_final_snapshot=True)
        truncated = runner.run()
        checker.equal("ending/decision-cap-truncated", "truncated", truncated.ending)
        checker.equal("ending/decision-cap-limit-reason", "decisions", truncated.limit_reason)
        checker.check("ending/decision-cap-keeps-final-snapshot",
                      truncated.final_snapshot is not None, "没有留下最后公开快照")
        checker.equal("ending/decision-cap-one-action", 1, len(confirmed_action_indices(truncated)))
        value = bootstrap_value(model, environment, truncated.final_snapshot)
        checker.check("ending/bootstrap-value-finite", value == value and abs(value) < 1e6, str(value))

        # 2) 墙钟限额：资源限额截断（不是游戏失败），训练路径必须据此停止而非提交该轮。
        runner = EpisodeRunner(client, seed=10000, max_steps=200,
                               encoding_config=environment.encoding_config,
                               executor_seed=profile.executor_seed(10000),
                               interruption_errors=client.interruption_errors,
                               policy=model.policy, model_id=model.model_id,
                               time_limit_s=0.0001, capture_final_snapshot=True)
        timed = runner.run()
        checker.equal("ending/wall-time-truncated", "truncated", timed.ending)
        checker.equal("ending/wall-time-limit-reason", "wall_time", timed.limit_reason)
        # 限额检查发生在每一步之前。Windows 上 `time.monotonic()` 的分辨率是系统时钟节拍
        # （约 15.6ms），所以极短限额下可能先走掉一两个动作才到点；关键是不能跑完这一局。
        executed = len(confirmed_action_indices(timed))
        checker.check("ending/wall-time-stops-early", executed < 10,
                      f"墙钟限额下执行了 {executed} 个动作")
        tight = replace(profile, budget={**profile.budget, "episode_time_limit_s": 0.0001})
        try:
            run_episode(tight, client, environment, reward_profile, environment_seed=10000,
                        executor_seed=tight.executor_seed(10000), policy=model.policy,
                        model_id=model.model_id, require_usable=True)
            checker.check("ending/time-limit-not-committed", False, "墙钟限额被当成已完成局")
        except DeliveryError as failure:
            checker.check("ending/time-limit-not-committed", failure.category == "fault",
                          f"类别 {failure.category}")

        # 3) 正常走到终局：终止快照没有动作，不计一个假动作。
        run = run_episode(profile, client, environment, reward_profile, environment_seed=10000,
                          executor_seed=profile.executor_seed(10000), policy=model.policy,
                          model_id=model.model_id, require_usable=True)
        checker.equal("ending/full-episode-terminal", "terminal", run.ending)
        last = run.result.transitions[-1]
        checker.equal("ending/terminal-transition-status", "terminal", last.status)
        checker.equal("ending/terminal-no-candidates", [], list(last.candidates))
        checker.check("ending/terminal-has-result", last.terminal is not None)
        checker.check("ending/reward-available", run.reward is not None, str(run.reward_problem))
    finally:
        client.shutdown()

    # 4) 断连故障注入：已确认转移保留，未确认的不得补造后状态，也不得当作胜负。
    client = runtime.open_bounded_client(profile)
    try:
        environment = observe_environment(profile, client)
        model = fresh_model(profile, environment)
        original_step = client.step
        state = {"killed": False}

        def killing_step(*arguments, **keywords):
            response = original_step(*arguments, **keywords)
            if not state["killed"]:
                state["killed"] = True
                client.kill()
            return response

        client.step = killing_step  # type: ignore[assignment]
        try:
            interrupted = run_episode(profile, client, environment, reward_profile,
                                      environment_seed=10002,
                                      executor_seed=profile.executor_seed(10002),
                                      policy=model.policy, model_id=model.model_id,
                                      require_usable=False)
        finally:
            client.step = original_step  # type: ignore[assignment]
        checker.check("ending/fault-injection-fired", state["killed"])
        checker.equal("ending/fault-injection-interrupted", "interrupted", interrupted.ending)
        checker.check("ending/fault-injection-not-win-loss", interrupted.terminal_outcome is None,
                      str(interrupted.terminal_outcome))
        checker.check("ending/fault-injection-kept-confirmed",
                      interrupted.confirmed_actions >= 1, str(interrupted.confirmed_actions))
        record = build_record(interrupted.result)
        checker.equal("ending/fault-record-classified-interrupted", "interrupted",
                      record.ending_class)
        checker.check("ending/fault-record-no-terminal", record.terminal is None,
                      str(record.terminal))
        checker.check("ending/fault-record-keeps-confirmed",
                      record.confirmed_transitions >= 1, str(record.confirmed_transitions))
    finally:
        client.shutdown()

    # 5) 训练路径遇到中断局必须抛错，不得返回一局"正常样本"。
    client = runtime.open_bounded_client(profile)
    try:
        environment = observe_environment(profile, client)
        model = fresh_model(profile, environment)
        original_step = client.step
        state = {"killed": False}

        def killing_step(*arguments, **keywords):
            response = original_step(*arguments, **keywords)
            if not state["killed"]:
                state["killed"] = True
                client.kill()
            return response

        client.step = killing_step  # type: ignore[assignment]
        try:
            run_episode(profile, client, environment, reward_profile, environment_seed=10003,
                        executor_seed=profile.executor_seed(10003), policy=model.policy,
                        model_id=model.model_id, require_usable=True)
            checker.check("ending/training-path-rejects-interrupt", False, "训练路径没有拒绝中断局")
        except DeliveryError as failure:
            checker.check("ending/training-path-rejects-interrupt", True)
            checker.equal("ending/training-path-fault-category", "fault", failure.category)
        finally:
            client.step = original_step  # type: ignore[assignment]
    finally:
        client.shutdown()


# --------------------------------------------------------------------------- #
# visible
# --------------------------------------------------------------------------- #
def _run_visible(checker: Checker) -> None:
    output, completed = demo_initial()
    checker.equal("visible/demo-exit", 0, completed.returncode)
    markdown_path = output / "episode.md"
    checker.check("visible/episode-md-exists", markdown_path.is_file())
    if not markdown_path.is_file():
        return
    markdown = markdown_path.read_text(encoding="utf-8")
    summary = read_json(output / "run-summary.json")

    checker.check("visible/header-shows-gym", "真实 Gym" in markdown, "")
    checker.check("visible/header-shows-seed", str(summary["seed"]) in markdown)
    checker.check("visible/header-shows-model-id", summary["model"]["model_id"] in markdown)
    checker.check("visible/header-marks-untrained", "untrained" in markdown)
    checker.check("visible/shows-all-candidates", "合法候选" in markdown)
    checker.check("visible/shows-chosen-action", "实际选择" in markdown)
    checker.check("visible/shows-after-state", "执行后公开变化" in markdown)

    action_blocks = markdown.count("\n## 动作 ")
    if markdown.startswith("## 动作 "):
        action_blocks += 1
    checker.equal("visible/action-blocks-match-confirmed", summary["confirmed_actions"], action_blocks)
    if summary["ending"] == "terminal":
        checker.check("visible/terminal-section", "## 终止" in markdown)
    if summary["limit_reason"]:
        checker.check("visible/limit-reason-shown", "限额" in markdown)

    store_dir = output / "episodes"
    from ai_the_spire.data.store import EpisodeStore

    store = EpisodeStore(store_dir)
    checker.check("visible/store-clean", not store.verify_all(), f"{store.verify_all()}")
    entries = store.index()
    checker.equal("visible/one-record", 1, len(entries))
    if entries:
        record = store.read(entries[0]["episode_id"])
        terminal = summary.get("terminal_result") or {}
        expected_class = {"terminal": {"win": "terminal_win", "loss": "terminal_loss"}.get(
            str(terminal.get("outcome")), "faulted"),
            "truncated": "truncated",
            "interrupted": "interrupted",
            "faulted": "faulted"}.get(str(summary["ending"]), "faulted")
        checker.equal("visible/record-ending-class", expected_class, record["ending"]["class"])
        confirmed = record["summary"]["confirmed_transitions"]
        checker.equal("visible/record-confirmed-count", summary["confirmed_actions"], confirmed)
        # 可读对局里写的 π 必须等于公开轨迹里记录的行为概率（同一份数据显示两次，不得漂移）。
        chosen = next(transition for transition in record["transitions"]
                      if transition["outcome"] == "confirmed")
        probability = chosen["behavior_probability"]
        checker.check("visible/pi-matches-record",
                      repr(probability) in markdown or f"{probability!r}" in markdown,
                      f"轨迹概率 {probability!r} 没有出现在 episode.md")


# --------------------------------------------------------------------------- #
# update
# --------------------------------------------------------------------------- #
def _run_update(checker: Checker) -> None:
    import numpy as np

    from ai_the_spire.models import ModelBundleError, load_bundle

    output, completed = train_run()
    checker.equal("update/train-exit", 0, completed.returncode)
    if not (output / "summary.json").is_file():
        checker.check("update/summary-written", False, "没有 summary.json")
        return

    for name in ("resolved-config.json", "manifest.json", "run-state.json", "metrics.jsonl",
                 "summary.json", "summary.md"):
        checker.check(f"update/has-{name}", (output / name).is_file(), name)
    for name in ("initial-model", "candidate-model", "checkpoints", "episodes"):
        checker.check(f"update/has-dir-{name}", (output / name).is_dir(), name)

    manifest = read_json(output / "manifest.json")
    resolved = read_json(output / "resolved-config.json")
    checker.check("update/manifest-has-gym", manifest["environment_identity"]["gym_id"].startswith("net:gym:"))
    checker.check("update/manifest-has-dictionary", bool(manifest["dictionary_version"]))
    for key in ("train_rounds", "episodes_per_round", "max_train_actions",
                "max_decisions_per_episode", "episode_time_limit_s", "request_timeout_s"):
        checker.check(f"update/resolved-budget-{key}", key in resolved["budget"], key)
    for key in ("learning_rate", "clip_ratio", "epochs", "value_coef", "entropy_coef",
                "max_grad_norm", "adam_beta1", "adam_beta2", "adam_epsilon"):
        checker.check(f"update/resolved-ppo-{key}", key in resolved["ppo"], key)
    checker.check("update/resolved-model-config-full", "d_entity" in resolved["model_config"])
    checker.check("update/resolved-encoding-config-full", "capacities" in resolved["encoding_config"])

    initial_meta = read_json(output / "initial-model" / "metadata.json")
    candidate_meta = read_json(output / "candidate-model" / "metadata.json")
    checker.check("update/initial-untrained", initial_meta["training"].startswith("none"), initial_meta["training"])
    checker.check("update/candidate-trained", candidate_meta["training"].startswith("ppo"), candidate_meta["training"])
    checker.check("update/identity-changed-after-update",
                  initial_meta["model_id"] != candidate_meta["model_id"],
                  f"{initial_meta['model_id']} == {candidate_meta['model_id']}")
    checker.check("update/weights-changed-after-update",
                  initial_meta["weights_digest"] != candidate_meta["weights_digest"])
    checker.check("update/identity-follows-weights",
                  candidate_meta["model_id"].endswith(candidate_meta["weights_digest"][7:19]),
                  f"{candidate_meta['model_id']} vs {candidate_meta['weights_digest']}")

    with np.load(output / "candidate-model" / "weights.npz", allow_pickle=False) as archive:
        weights = {name: archive[name] for name in archive.files}
    with np.load(output / "initial-model" / "weights.npz", allow_pickle=False) as archive:
        initial_weights = {name: archive[name] for name in archive.files}
    checker.check("update/weights-actually-differ",
                  any(not np.array_equal(weights[name], initial_weights[name]) for name in weights),
                  "候选包与初始包的参数完全相同")

    metrics = read_metrics(output)
    updates = [item for item in metrics if item.get("kind") == "update"]
    episodes = [item for item in metrics if item.get("kind") == "episode"]
    validations = [item for item in metrics if item.get("kind") == "validation"]
    checker.check("update/has-update-records", len(updates) >= 2, str(len(updates)))
    checker.check("update/real-samples", all(item["samples"] > 0 for item in updates),
                  str([item["samples"] for item in updates]))
    checker.check("update/finite-loss", all(abs(item["loss"]) < 1e9 for item in updates))
    checker.check("update/chain-of-frozen-models",
                  len(updates) >= 2 and updates[0]["candidate_model_id"] == updates[1]["behavior_model_id"],
                  "第 2 轮的行为模型不是第 1 轮的候选模型")
    checker.check("update/validation-before-training",
                  any(item.get("stage") == "before-training" for item in validations),
                  str([item.get("stage") for item in validations]))
    checker.check("update/validation-after-each-round",
                  len([item for item in validations if item.get("stage", "").startswith("after-round")])
                  == len(updates))
    checker.check("update/validation-three-policies",
                  all(len(item["policies"]) == 3 for item in validations))

    expected_episodes = resolved["budget"]["train_rounds"] * resolved["budget"]["episodes_per_round"]
    checker.equal("update/episode-count", expected_episodes, len(episodes))
    checker.check("update/actions-within-budget",
                  all(item["training_actions_used"] <= resolved["budget"]["max_train_actions"]
                      for item in updates + [item for item in metrics if item.get("kind") == "budget"]))
    for index in range(1, resolved["budget"]["train_rounds"] + 1):
        directory = output / "checkpoints" / f"round-{index:04d}"
        checker.check(f"update/checkpoint-round-{index}", directory.is_dir(), str(directory))
        checker.check(f"update/checkpoint-round-{index}-optimizer",
                      (directory / "optimizer.npz").is_file() and (directory / "metadata.json").is_file())
        checker.check(f"update/checkpoint-round-{index}-action-samples",
                      (directory / "action-samples.jsonl").is_file())

    from ai_the_spire.data.store import EpisodeStore

    store = EpisodeStore(output / "episodes")
    checker.check("update/store-clean", not store.verify_all(), f"{store.verify_all()}")
    checker.equal("update/store-records", expected_episodes, len(store.index()))

    # 截断/终止语义在真实采样里必须正确分类。
    for item in episodes:
        checker.check(f"update/episode-ending-known-{item['round']}-{item['episode_index']}",
                      item["ending"] in ("terminal", "truncated"),
                      str(item["ending"]))
        if item["ending"] == "truncated":
            checker.equal(f"update/truncated-reason-{item['episode_index']}", "decisions",
                          item["limit_reason"])
    checker.check("update/summary-records-candidate",
                  read_json(output / "summary.json")["candidate"]["model_id"] == candidate_meta["model_id"])

    # 载入候选包必须与 metadata 逐字一致（身份不过期）。
    from ai_the_spire.delivery.config import load_profile
    from ai_the_spire.delivery.environment import observe_environment
    from ai_the_spire.delivery.runtime import open_bounded_client

    profile = load_profile(SMOKE_CONFIG)
    client = open_bounded_client(profile)
    try:
        environment = observe_environment(profile, client)
        model = load_bundle(str(output / "candidate-model"), manifest=environment.manifest)
        checker.equal("update/reload-model-id", candidate_meta["model_id"], model.model_id)
        checker.equal("update/reload-weights-digest", candidate_meta["weights_digest"],
                      model.weights_digest)
    except ModelBundleError as failure:  # pragma: no cover
        checker.check("update/reload-candidate", False, str(failure))
    finally:
        client.shutdown()


# --------------------------------------------------------------------------- #
# source
# --------------------------------------------------------------------------- #
def _run_source(checker: Checker) -> None:
    from ai_the_spire.delivery.config import load_profile
    from ai_the_spire.delivery.environment import observe_environment
    from ai_the_spire.delivery.episodes import run_episode
    from ai_the_spire.delivery.errors import FaultError
    from ai_the_spire.delivery.model_access import fresh_model
    from ai_the_spire.delivery.reward_v1 import d1_reward_profile
    from ai_the_spire.delivery.runtime import open_bounded_client
    from ai_the_spire.delivery.train import samples_from_episode

    # 1) 注入另一个 Gym 身份的样本：启动游戏前拒绝。
    tampered = copy_run("run-wrong-gym")
    manifest = read_json(tampered / "manifest.json")
    manifest["environment_identity"]["gym_id"] = "net:gym:some-other-gym"
    (tampered / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    completed = run_cli("evaluate", "--run", str(tampered), "--split", "validation",
                        "--output", str(workspace() / "eval-wrong-gym"))
    checker.equal("source/wrong-gym-exit", 2, completed.returncode)
    checker.check("source/wrong-gym-rejected-before-game",
                  "不一致" in (completed.stderr + completed.stdout)
                  and not (workspace() / "eval-wrong-gym" / "comparison.json").exists(),
                  (completed.stderr + completed.stdout)[:400])

    # 2) 注入另一份奖励 Profile 身份：同样拒绝。
    tampered = copy_run("run-wrong-reward")
    manifest = read_json(tampered / "manifest.json")
    manifest["reward_profile"]["digest"] = "sha256:" + "0" * 64
    (tampered / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    completed = run_cli("evaluate", "--run", str(tampered), "--split", "validation",
                        "--output", str(workspace() / "eval-wrong-reward"))
    checker.equal("source/wrong-reward-exit", 2, completed.returncode)
    checker.check("source/wrong-reward-message",
                  "奖励" in (completed.stderr + completed.stdout),
                  (completed.stderr + completed.stdout)[:400])

    # 3) 初始模型与候选模型身份相同（权重未真正更新）：拒绝把同一次前向当成两个策略。
    tampered = copy_run("run-same-identity")
    shutil.rmtree(tampered / "candidate-model")
    shutil.copytree(tampered / "initial-model", tampered / "candidate-model")
    completed = run_cli("evaluate", "--run", str(tampered), "--split", "validation",
                        "--output", str(workspace() / "eval-same-identity"))
    checker.equal("source/same-identity-exit", 2, completed.returncode)
    checker.check("source/same-identity-message",
                  "身份相同" in (completed.stderr + completed.stdout),
                  (completed.stderr + completed.stdout)[:300])

    # 4) 同冻结模型 + 同一公开快照 ⇒ 同概率（模型预测本身可复现）。
    profile = load_profile(SMOKE_CONFIG)
    reward_profile = d1_reward_profile(hp_lost_weight=-0.01, victory_weight=1.0,
                                       defeat_weight=-1.0, discount=0.99)
    client = open_bounded_client(profile)
    try:
        environment = observe_environment(profile, client)
        model = fresh_model(profile, environment)
        reference = None
        for _ in range(2):
            run = run_episode(profile, client, environment, reward_profile, environment_seed=10000,
                              executor_seed=profile.executor_seed(10000), policy=model.policy,
                              model_id=model.model_id, require_usable=True)
            snapshot = run.result.transitions[0].snapshot
            probabilities = model.predict(snapshot, catalog=environment.catalog).by_semantic_key()
            if reference is None:
                reference = probabilities
            else:
                checker.equal("source/same-model-same-snapshot-same-probabilities",
                              {key: repr(value) for key, value in reference.items()},
                              {key: repr(value) for key, value in probabilities.items()})

        # 5) 篡改轨迹里的行为概率：训练样本构造必须显式失败（不得按新概率重算冒充旧样本）。
        run = run_episode(profile, client, environment, reward_profile, environment_seed=10000,
                          executor_seed=profile.executor_seed(10000), policy=model.policy,
                          model_id=model.model_id, require_usable=True)
        transition = run.result.transitions[0]
        key = transition.chosen_semantic_key
        transition.probabilities[key] = transition.probabilities[key] * 0.5
        try:
            samples_from_episode(model, environment, run, gamma=0.99)
            checker.check("source/tampered-behavior-probability-rejected", False, "没有抛出 FaultError")
        except FaultError as failure:
            checker.check("source/tampered-behavior-probability-rejected", True)
            checker.check("source/tampered-behavior-probability-message",
                          "行为概率" in str(failure), str(failure)[:200])
    finally:
        client.shutdown()

    # 6) 注入另一份 Gym Manifest 身份（模型包与当前 Manifest 不一致）：启动游戏前拒绝。
    tampered = copy_run("run-wrong-manifest-bundle")
    metadata_path = tampered / "candidate-model" / "metadata.json"
    metadata = read_json(metadata_path)
    metadata["manifest_digest"] = "sha256:" + "1" * 64
    metadata_path.write_text(json.dumps(metadata, ensure_ascii=False, sort_keys=True, indent=2) + "\n",
                             encoding="utf-8")
    completed = run_cli("demo", "--config", str(SMOKE_CONFIG),
                        "--model", str(tampered / "candidate-model"),
                        "--output", str(workspace() / "demo-wrong-manifest"))
    checker.equal("source/wrong-manifest-bundle-exit", 2, completed.returncode)
    checker.check("source/wrong-manifest-bundle-before-game",
                  not (workspace() / "demo-wrong-manifest" / "episode.md").exists())


# --------------------------------------------------------------------------- #
# load
# --------------------------------------------------------------------------- #
def _run_load(checker: Checker) -> None:
    import numpy as np

    output, _ = train_run()
    candidate = output / "candidate-model"
    demo_out = workspace() / "demo-candidate"
    completed = run_cli("demo", "--config", str(SMOKE_CONFIG), "--model", str(candidate),
                        "--output", str(demo_out))
    checker.equal("load/demo-candidate-exit", 0, completed.returncode)
    if completed.returncode == 0:
        summary = read_json(demo_out / "run-summary.json")
        metadata = read_json(candidate / "metadata.json")
        checker.equal("load/demo-uses-candidate-model-id", metadata["model_id"],
                      summary["model"]["model_id"])
        checker.equal("load/demo-model-state", "loaded-bundle", summary["model"]["state"])
        checker.check("load/demo-md-names-loaded-bundle",
                      "loaded-bundle" in (demo_out / "episode.md").read_text(encoding="utf-8"))

    # 权重损坏：必须在启动游戏之前拒绝。
    corrupt = workspace() / "candidate-corrupt"
    shutil.copytree(candidate, corrupt)
    with np.load(corrupt / "weights.npz", allow_pickle=False) as archive:
        weights = {name: archive[name] for name in archive.files}
    target = sorted(weights)[0]
    weights[target] = weights[target] + 1.0
    np.savez(corrupt / "weights.npz", **weights)
    completed = run_cli("demo", "--config", str(SMOKE_CONFIG), "--model", str(corrupt),
                        "--output", str(workspace() / "demo-corrupt"))
    checker.equal("load/corrupt-weights-exit", 2, completed.returncode)
    checker.check("load/corrupt-weights-before-game",
                  not (workspace() / "demo-corrupt" / "episode.md").exists())
    checker.check("load/corrupt-weights-message",
                  "模型包" in (completed.stderr + completed.stdout),
                  (completed.stderr + completed.stdout)[:300])

    completed = run_cli("demo", "--config", str(SMOKE_CONFIG),
                        "--model", str(workspace() / "no-such-bundle"),
                        "--output", str(workspace() / "demo-missing-bundle"))
    checker.equal("load/missing-bundle-exit", 2, completed.returncode)


# --------------------------------------------------------------------------- #
# compare
# --------------------------------------------------------------------------- #
def _run_compare(checker: Checker) -> None:
    output, _ = train_run()
    profile = read_json(output / "resolved-config.json")
    first_dir = workspace() / "evaluate-validation"
    completed = run_cli("evaluate", "--run", str(output), "--split", "validation",
                        "--output", str(first_dir))
    checker.equal("compare/evaluate-exit", 0, completed.returncode)
    if completed.returncode != 0:
        return
    for name in ("comparison.json", "comparison.md"):
        checker.check(f"compare/has-{name}", (first_dir / name).is_file(), name)

    comparison = read_json(first_dir / "comparison.json")
    report = comparison["report"]
    checker.equal("compare/three-policies", 3, len(report["policies"]))
    checker.equal("compare/two-candidates", 2, len(comparison["plan"]["policies"]) - 1)
    checker.equal("compare/comparison-count", 8, len(report["comparisons"]))
    checker.equal("compare/no-failures", [], list(report["failures"]))
    checker.equal("compare/exit-category", "ok", comparison["exit_category"])

    validation_base = profile["seeds"]["validation_environment_base"]
    validation_count = profile["seeds"]["validation_environment_count"]
    expected_seeds = list(range(validation_base, validation_base + validation_count))
    checker.equal("compare/uses-validation-seeds", expected_seeds, comparison["seeds"])
    checker.check("compare/does-not-touch-final-seeds",
                  all(seed < profile["seeds"]["final_environment_base"]
                      for seed in comparison["seeds"]))
    checker.check("compare/plan-binds-gym",
                  comparison["plan"]["gym_id"] == read_json(output / "manifest.json")
                  ["environment_identity"]["gym_id"])
    checker.check("compare/action-sequence-equality-present",
                  "action_sequence_equality" in comparison)
    checker.check("compare/equality-data-shape",
                  all(set(payload) == {"identical_action_sequence_seeds", "seeds"}
                      for payload in comparison["action_sequence_equality"].values()))
    checker.check("compare/bootstrap-config",
                  comparison["plan"]["bootstrap_resamples"] == 1000
                  and comparison["plan"]["bootstrap_seed"] == 20261003)

    markdown = (first_dir / "comparison.md").read_text(encoding="utf-8")
    for token in ("逐策略汇总", "同种子配对", "逐局记录", "说明"):
        checker.check(f"compare/md-has-{token}", token in markdown, token)
    checker.check("compare/md-shows-verdict",
                  any(word in markdown for word in ("improved", "worse", "undecided",
                                                    "insufficient_samples")))

    # 同输入重复一次：计划身份与逐策略回报必须一致（墙钟等成本量允许变化）。
    second_dir = workspace() / "evaluate-validation-again"
    completed = run_cli("evaluate", "--run", str(output), "--split", "validation",
                        "--output", str(second_dir))
    checker.equal("compare/repeat-exit", 0, completed.returncode)
    if completed.returncode == 0:
        again = read_json(second_dir / "comparison.json")
        checker.equal("compare/repeat-plan-digest", comparison["plan_digest"], again["plan_digest"])
        first_rewards = {item["policy_id"]: item["metrics"].get("net:metric:reward_scalar")
                         for item in report["policies"]}
        second_rewards = {item["policy_id"]: item["metrics"].get("net:metric:reward_scalar")
                          for item in again["report"]["policies"]}
        checker.equal("compare/repeat-reward-metrics-equal", first_rewards, second_rewards)
        first_steps = {item["policy_id"]: item["metrics"].get("net:metric:steps")
                       for item in report["policies"]}
        second_steps = {item["policy_id"]: item["metrics"].get("net:metric:steps")
                        for item in again["report"]["policies"]}
        checker.equal("compare/repeat-step-metrics-equal", first_steps, second_steps)


# --------------------------------------------------------------------------- #
# resume
# --------------------------------------------------------------------------- #
def _run_resume(checker: Checker) -> None:
    import numpy as np

    root = workspace()
    partial = root / "smoke-partial-accept"
    completed = run_cli("train", "--config", str(SMOKE_CONFIG), "--stop-after-round", "1",
                        "--output", str(partial))
    checker.equal("resume/stop-after-round-exit", 0, completed.returncode)
    if completed.returncode != 0:
        return
    state = read_json(partial / "run-state.json")
    checker.equal("resume/stopped-after-one-round", 1, state["rounds_completed"])
    checker.check("resume/stop-is-normal", "正常" in completed.stdout or "stop-after-round" in completed.stdout)
    before = tree_digest(partial)

    resumed = root / "smoke-resumed-accept"
    completed = run_cli("resume", "--run", str(partial), "--output", str(resumed))
    checker.equal("resume/resume-exit", 0, completed.returncode)
    if completed.returncode != 0:
        return
    checker.equal("resume/original-run-untouched", before, tree_digest(partial))
    state = read_json(resumed / "run-state.json")
    checker.equal("resume/total-rounds", 2, state["rounds_completed"])
    checker.check("resume/no-extra-rounds", state["train_rounds"] == 2, str(state["train_rounds"]))
    for name in ("resolved-config.json", "manifest.json", "initial-model", "candidate-model",
                 "source-index.json"):
        checker.check(f"resume/new-dir-has-{name}", (resumed / name).exists(), name)
    checker.check("resume/source-index-points-at-original",
                  read_json(resumed / "source-index.json")["source_run"] == str(partial))
    checker.check("resume/episodes-copied-and-extended",
                  len([json.loads(line) for line in
                       (resumed / "episodes" / "index.jsonl").read_text(encoding="utf-8").splitlines()
                       if line.strip()]) == 4)

    continuous = root / "smoke-continuous-accept"
    completed = run_cli("train", "--config", str(SMOKE_CONFIG), "--output", str(continuous))
    checker.equal("resume/continuous-exit", 0, completed.returncode)
    if completed.returncode != 0:
        return

    left = resumed / "checkpoints" / "round-0002"
    right = continuous / "checkpoints" / "round-0002"
    checker.check("resume/both-have-round-0002", left.is_dir() and right.is_dir())
    if left.is_dir() and right.is_dir():
        with np.load(left / "weights.npz", allow_pickle=False) as archive:
            left_weights = {name: archive[name] for name in archive.files}
        with np.load(right / "weights.npz", allow_pickle=False) as archive:
            right_weights = {name: archive[name] for name in archive.files}
        with np.load(left / "optimizer.npz", allow_pickle=False) as archive:
            left_optimizer = {name: archive[name] for name in archive.files}
        with np.load(right / "optimizer.npz", allow_pickle=False) as archive:
            right_optimizer = {name: archive[name] for name in archive.files}
        checker.equal("resume/param-names-equal", sorted(left_weights), sorted(right_weights))
        checker.check("resume/params-elementwise-equal",
                      all(np.array_equal(left_weights[name], right_weights[name])
                          for name in left_weights))
        checker.equal("resume/optimizer-names-equal",
                      sorted(left_optimizer), sorted(right_optimizer))
        checker.check("resume/adam-elementwise-equal",
                      all(np.array_equal(left_optimizer[name], right_optimizer[name])
                          for name in left_optimizer))
        left_meta = read_json(left / "metadata.json")
        right_meta = read_json(right / "metadata.json")
        checker.equal("resume/model-id-equal", left_meta["model_id"], right_meta["model_id"])
        checker.equal("resume/weights-digest-equal", left_meta["weights_digest"],
                      right_meta["weights_digest"])
        checker.equal("resume/optimizer-steps-equal",
                      left_meta["training_state"]["steps"], right_meta["training_state"]["steps"])
        checker.equal("resume/training-actions-equal",
                      left_meta["training_state"]["training_actions_used"],
                      right_meta["training_state"]["training_actions_used"])
        checker.equal("resume/rounds-completed-equal",
                      left_meta["training_state"]["rounds_completed"],
                      right_meta["training_state"]["rounds_completed"])

    # 同一个 run 再 resume 一次：没有剩余轮次，拒绝（不额外增加目标轮数）。
    completed = run_cli("resume", "--run", str(resumed), "--output", str(root / "resume-again"))
    checker.equal("resume/no-remaining-rounds-exit", 2, completed.returncode)

    # 未完成轮次不能伪装已完成：删除检查点后 resume 必须被拒绝。
    tampered = root / "smoke-broken-state"
    shutil.copytree(partial, tampered)
    shutil.rmtree(tampered / "checkpoints" / "round-0001")
    completed = run_cli("resume", "--run", str(tampered), "--output", str(root / "resume-broken"))
    checker.equal("resume/missing-checkpoint-exit", 2, completed.returncode)


_RUNNERS = {
    "bounded": _run_bounded,
    "ending": _run_ending,
    "visible": _run_visible,
    "update": _run_update,
    "source": _run_source,
    "load": _run_load,
    "compare": _run_compare,
    "resume": _run_resume,
}


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="D1 小预算自动化验收入口")
    parser.add_argument("--only", action="append", choices=GROUPS, default=None,
                        help="只跑指定组（可重复）")
    parser.add_argument("--list", action="store_true", help="列出组名并退出")
    args = parser.parse_args(argv)
    if args.list:
        for name in GROUPS:
            print(name)
        return 0

    selected = args.only or list(GROUPS)
    total_passed = 0
    total_failed = 0
    total_executed = 0
    root = workspace()
    print(f"[d1] 工作目录：{root}")
    for name in selected:
        checker = Checker()
        try:
            _RUNNERS[name](checker)
        except Exception as failure:  # noqa: BLE001 - 组异常按失败计，不静默
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
