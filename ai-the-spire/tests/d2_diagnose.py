"""D2 诊断的验收检查（任务包 §4）：用户命令、合法参考策略、相同快照探针、预算/故障、**不触发训练**。

这些检查与 `d1_delivery.py`（D1 路径）、`d1_1_closure.py`（D1.1 收口）分开：
D2 的"不读 final""不调用 Trainer""参考策略只按公开候选"必须是**可判别的事实**，不是文档里的声明。

用法（仓库根目录，需要先 ``dotnet build AutoTheSpire.sln``）::

    python ai-the-spire/tests/d2_diagnose.py
    python ai-the-spire/tests/d2_diagnose.py --only isolation
    python ai-the-spire/tests/d2_diagnose.py --list

分组：

* ``command``   —— 根目录命令可跑、产物齐全、四段内容在、缺产物/非空输出返回 2；
* ``rule``      —— 参考策略对**当前合法候选**返回归一化 one-hot，且选择符合记录的优先级；
* ``probe``     —— 同局面探针取的是 index 次序最早 8 局，TV 可由保留的逐候选概率独立重算；
* ``isolation`` —— **不触发训练**（原 run 逐字节不变、没有新检查点/模型包、模块不引用 Trainer）、
  不访问 final/未来保留种子、预算与故障路径按 D1.1 的停止/资源处理。

退出码：0 全通过、1 有用例失败、2 参数或环境错误。最后一行固定为 ``TOTAL: ...``。
"""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import subprocess
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "ai-the-spire"))

RUN_PY = REPO / "ai-the-spire" / "run.py"
SOURCE_RUN = REPO / "artifacts" / "d1" / "train"
GROUPS = ("command", "rule", "probe", "isolation")

#: 未来保留种子（任务包 §3：本任务不得访问）。
RESERVED_SEEDS = tuple(range(50000, 50008))
FINAL_SEEDS = tuple(range(30000, 30008))


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


def workspace() -> pathlib.Path:
    if "root" not in _CACHE:
        _CACHE["root"] = pathlib.Path(tempfile.mkdtemp(prefix="d2-diagnose-"))
    return _CACHE["root"]  # type: ignore[return-value]


_CACHE: dict = {}


def read_json(path: pathlib.Path) -> dict:
    return json.loads(pathlib.Path(path).read_text(encoding="utf-8"))


def tree_digest(root: pathlib.Path) -> str:
    digest = hashlib.sha256()
    for path in sorted(root.rglob("*")):
        if path.is_file():
            digest.update(str(path.relative_to(root)).encode("utf-8"))
            digest.update(path.read_bytes())
    return "sha256:" + digest.hexdigest()


def diagnosis() -> tuple[pathlib.Path, subprocess.CompletedProcess, dict]:
    """跑一次真实诊断命令并缓存（命令组会另行校验返回码与产物）。"""
    if "diagnosis" not in _CACHE:
        output = workspace() / "diagnose"
        completed = run_cli("diagnose", "--run", str(SOURCE_RUN), "--output", str(output))
        document = (read_json(output / "diagnosis.json")
                    if (output / "diagnosis.json").is_file() else {})
        _CACHE["diagnosis"] = (output, completed, document)
    output, completed, document = _CACHE["diagnosis"]
    if completed.returncode != 0:
        raise RuntimeError(f"diagnose 返回 {completed.returncode}：{completed.stderr[-800:]}")
    return output, completed, document


def digest_of_source_run() -> str:
    if "source_digest" not in _CACHE:
        _CACHE["source_digest"] = tree_digest(SOURCE_RUN)
    return _CACHE["source_digest"]


# --------------------------------------------------------------------------- #
# command
# --------------------------------------------------------------------------- #
def _run_command(checker: Checker, root: pathlib.Path) -> None:
    output, completed, document = diagnosis()
    checker.equal("command/exit", 0, completed.returncode)
    for name in ("diagnosis.md", "diagnosis.json", "reference-episode.md"):
        checker.check(f"command/has-{name}", (output / name).is_file(), name)
    checker.check("command/has-reference-record",
                  any((output / "reference-episodes" / "records").glob("*.json")),
                  "没有参考对局的公开 EpisodeRecord")
    checker.check("command/prints-summary",
                  "四策略" in completed.stdout and "diagnosis.md" in completed.stdout,
                  completed.stdout[-300:])

    document = read_json(output / "diagnosis.json")
    document = read_json(output / "diagnosis.json")
    for section in ("four_policy_comparison", "same_snapshot_probe", "learning_signal",
                    "follow_up_experiment", "source_run_scope", "rule_baseline"):
        checker.check(f"command/section-{section}", section in document, section)
    checker.check("command/rule-spec-has-priority",
                  bool(document["rule_baseline"].get("priority"))
                  and document["rule_baseline"].get("rule_policy_id", "").startswith("d2:rule:"),
                  str(document["rule_baseline"])[:200])
    checker.equal("command/diagnose-seeds", list(range(40000, 40008)),
                  document["diagnose_seeds"])
    scope = document["source_run_scope"]
    checker.equal("command/scope-executor-offset", 1000000, scope["executor_offset"])
    checker.check("command/scope-lists-train-seeds",
                  10000 in scope["train_environment_seeds"]
                  and 10031 in scope["train_environment_seeds"], str(scope)[:200])
    checker.equal("command/scope-final-not-accessed", list(FINAL_SEEDS),
                  scope["final_environment_seeds_reserved_not_accessed"])
    checker.equal("command/scope-future-reserved", list(RESERVED_SEEDS),
                  scope["future_reserved_seeds_not_accessed"])

    reference = document["reference_episode"]
    checker.check("command/reference-is-win",
                  reference.get("terminal_outcome") == "win"
                  and reference.get("ending") == "terminal",
                  str(reference))
    markdown = (output / "reference-episode.md").read_text(encoding="utf-8")
    for token in ("参考对局", "规则优先级", "合法候选", "实际选择", "规则选择理由"):
        checker.check(f"command/reference-md-has-{token}", token in markdown, token)

    checker.check("command/md-names-probability-not-q",
                  "不是** Q 值" in (output / "diagnosis.md").read_text(encoding="utf-8")
                  or "不是 Q 值" in (output / "diagnosis.md").read_text(encoding="utf-8"),
                  "diagnosis.md 没有声明概率不是 Q 值/通关概率")

    # 错误路径：缺产物与非空输出都必须是退出码 2。
    completed = run_cli("diagnose", "--run", str(output), "--output", str(root / "probe-missing"))
    checker.equal("command/missing-artifacts-exit", 2, completed.returncode)
    checker.check("command/missing-artifacts-message",
                  "缺少诊断所需产物" in (completed.stderr + completed.stdout),
                  (completed.stderr + completed.stdout)[-300:])
    completed = run_cli("diagnose", "--run", str(SOURCE_RUN), "--output", str(output))
    checker.equal("command/non-empty-output-exit", 2, completed.returncode)

    # --help 不需要 NumPy / 已构建 Host。
    blocker = root / "no-numpy"
    blocker.mkdir(exist_ok=True)
    (blocker / "numpy.py").write_text('raise ImportError("blocked by d2 harness")\n',
                                      encoding="utf-8")
    completed = run_cli("diagnose", "--help", env_extra={"PYTHONPATH": str(blocker)})
    checker.equal("command/help-without-numpy", 0, completed.returncode)
    checker.check("command/help-lists-run-output",
                  "--run" in completed.stdout and "--output" in completed.stdout)


# --------------------------------------------------------------------------- #
# rule：合法参考策略
# --------------------------------------------------------------------------- #
def _run_rule(checker: Checker, root: pathlib.Path) -> None:
    from ai_the_spire.contracts import layers
    from ai_the_spire.delivery.config import load_profile
    from ai_the_spire.delivery.console import entity_map
    from ai_the_spire.delivery.rule_policy import (ATTACK_CATEGORY, RULE_POLICY_ID,
                                                   candidate_facts, make_rule_policy,
                                                   rank_of)
    from ai_the_spire.delivery.runtime import ensure_numpy, open_bounded_client
    from ai_the_spire.runner.baseline import ordered_candidates
    from ai_the_spire.data.store import EpisodeStore

    ensure_numpy()
    profile = load_profile(REPO / "ai-the-spire" / "configs" / "battle-demo-v1.json")
    store = EpisodeStore(SOURCE_RUN / "episodes")
    record = store.read(store.index()[0]["episode_id"])
    policy = make_rule_policy()
    checked = 0
    all_ok = True
    for transition in record["transitions"]:
        snapshot = transition["snapshot"]
        if not (snapshot.get("legal_actions") or []):
            continue
        candidates = ordered_candidates(snapshot)
        probabilities = policy(snapshot, candidates)
        expected = {candidate.semantic_key_text for candidate in candidates}
        if set(probabilities) != expected:
            all_ok = False
            break
        if abs(sum(probabilities.values()) - 1.0) > 1e-12:
            all_ok = False
            break
        if sorted(set(probabilities.values())) != [0.0, 1.0] and len(candidates) > 1:
            all_ok = False
            break
        chosen = [key for key, value in probabilities.items() if value == 1.0]
        if len(chosen) != 1:
            all_ok = False
            break
        # 选择必须与记录的优先级一致（攻击牌按公开 Amount 降序、并列按语义键升序）。
        by_id = entity_map(snapshot)
        ranked = sorted(candidates, key=lambda candidate: rank_of(
            candidate.action, candidate.semantic_key_text, by_id))
        if ranked[0].semantic_key_text != chosen[0]:
            all_ok = False
            break
        checked += 1
    checker.check("rule/legal-candidates-covered", checked >= 10, f"只检查了 {checked} 个局面")
    checker.check("rule/one-hot-normalized-and-ranked", all_ok,
                  "参考策略的概率不是恰好覆盖合法候选的归一化 one-hot，或选择不符合记录优先级")

    # 判别性检查：打乱候选顺序不改变选择（选择只看语义键与公开字段，不看位置/句柄）。
    transition = next(item for item in record["transitions"]
                      if len(item["snapshot"].get("legal_actions") or []) >= 2)
    snapshot = transition["snapshot"]
    candidates = ordered_candidates(snapshot)
    forward = policy(snapshot, candidates)
    shuffled = list(reversed(candidates))
    backward = policy(snapshot, shuffled)
    chosen_forward = [key for key, value in forward.items() if value == 1.0]
    chosen_backward = [key for key, value in backward.items() if value == 1.0]
    checker.equal("rule/order-invariant", chosen_forward, chosen_backward)
    checker.check("rule/chosen-is-a-legal-candidate",
                  set(chosen_forward) <= {candidate.semantic_key_text for candidate in candidates},
                  str(chosen_forward))

    # 攻击牌优先看**公开 Amount**：构造同局面下两张攻击牌的对照（从真实快照里找一张）。
    attack_amounts = []
    by_id = entity_map(snapshot)
    for candidate in candidates:
        facts = candidate_facts(candidate.action)
        if facts["is_play_card"]:
            entity = by_id.get(facts["referenced_instance"]) if facts["referenced_instance"] else None
            if entity:
                fields = {field.get("field_id"): field.get("value")
                          for field in entity.get("fields") or []
                          if field.get("state") == "known"}
                if fields.get("net:field:base-spire:Card.Kind") == ATTACK_CATEGORY:
                    attack_amounts.append(float(fields.get("net:field:base-spire:Card.Amount")))
    if len(attack_amounts) >= 2:
        chosen_facts = candidate_facts(
            next(c for c in candidates
                 if c.semantic_key_text == chosen_forward[0]).action)
        chosen_entity = by_id.get(chosen_facts["referenced_instance"])
        chosen_fields = {field.get("field_id"): field.get("value")
                         for field in (chosen_entity or {}).get("fields") or []
                         if field.get("state") == "known"}
        if chosen_fields.get("net:field:base-spire:Card.Kind") == ATTACK_CATEGORY:
            checker.equal("rule/picks-highest-public-amount", max(attack_amounts),
                          float(chosen_fields.get("net:field:base-spire:Card.Amount")))
        else:
            checker.check("rule/picks-highest-public-amount", True,
                          "（该局面没有攻击牌候选，跳过）")
    else:
        checker.check("rule/picks-highest-public-amount", True, "（该局面攻击牌不足两张，跳过）")

    checker.check("rule/policy-id-recorded", RULE_POLICY_ID.startswith("d2:rule:"))
    _ = layers, profile, root


# --------------------------------------------------------------------------- #
# probe：同局面探针
# --------------------------------------------------------------------------- #
def _run_probe(checker: Checker, root: pathlib.Path) -> None:
    from ai_the_spire.data.store import EpisodeStore

    output, _, document = diagnosis()
    probe = document["same_snapshot_probe"]
    store = EpisodeStore(SOURCE_RUN / "episodes")
    head = [entry["episode_id"] for entry in store.index()[:8]]
    checker.equal("probe/earliest-eight-by-index", head, probe["episode_ids"])
    checker.check("probe/records-distinct-episodes", len(set(probe["episode_ids"])) == 8,
                  str(probe["episode_ids"]))
    checker.check("probe/positions-positive", probe["positions"] > 0, str(probe["positions"]))
    checker.check("probe/tv-within-bounds",
                  all(0.0 - 1e-12 <= item["tv"] <= 1.0 + 1e-12 for item in probe["per_position"]),
                  "TV 越界")
    checker.check("probe/tv-stats-match-per-position",
                  abs(probe["tv"]["max"] - max(item["tv"] for item in probe["per_position"])) < 1e-12
                  and abs(probe["tv"]["mean"]
                          - sum(item["tv"] for item in probe["per_position"])
                          / probe["positions"]) < 1e-9,
                  "汇总量与逐局面记录不一致")

    # 独立重算：用保留的逐候选概率重新算 TV，必须与记录一致（不采信实现自报）。
    recomputed_ok = True
    for item in probe["per_position"][:60]:
        tv = 0.5 * sum(abs(entry["p_candidate"] - entry["p_initial"])
                       for entry in item["probabilities"])
        if abs(tv - item["tv"]) > 1e-12:
            recomputed_ok = False
            break
        if abs(sum(entry["p_initial"] for entry in item["probabilities"]) - 1.0) > 1e-9:
            recomputed_ok = False
            break
    checker.check("probe/tv-recomputable-from-retained-probabilities", recomputed_ok,
                  "保留的逐候选概率重算不出记录的 TV")
    checker.check("probe/keeps-position-locators",
                  all("observation_id" in item and "episode_id" in item and "step" in item
                      for item in probe["per_position"]),
                  "逐局面记录缺少定位字段")
    checker.check("probe/records-both-model-identities",
                  bool(probe["models"].get("initial")) and bool(probe["models"].get("candidate"))
                  and probe["models"]["initial"] != probe["models"]["candidate"],
                  str(probe["models"]))
    checker.check("probe/end-turn-stats-consistent",
                  (probe["end_turn_positions"] == 0)
                  or (probe["end_turn_probability_initial"]["count"] == probe["end_turn_positions"]
                      and probe["end_turn_probability_candidate"]["count"]
                      == probe["end_turn_positions"]),
                  str(probe["end_turn_positions"]))
    checker.check("probe/same-snapshot-declared",
                  "同一份公开快照" in probe["definition"], probe["definition"])
    _ = output, root


# --------------------------------------------------------------------------- #
# isolation：不触发训练、不读 final、预算与故障
# --------------------------------------------------------------------------- #
def _seed_values(node, path=()) -> list:
    """收集 JSON 里**以 seed 命名**的字段的取值（用于证明没访问 final/未来保留种子）。"""
    found = []
    if isinstance(node, dict):
        for key, value in node.items():
            if isinstance(key, str) and key.endswith("seed") or key in ("seeds",):
                found.append((path + (key,), value))
            found.extend(_seed_values(value, path + (str(key),)))
    elif isinstance(node, list):
        for index, value in enumerate(node):
            found.extend(_seed_values(value, path + (str(index),)))
    return found


def _run_isolation(checker: Checker, root: pathlib.Path) -> None:
    output, _, document = diagnosis()

    # 1) 原 run 逐字节不变 + 没有新检查点/模型包。
    checker.equal("isolation/source-run-unchanged", digest_of_source_run(), tree_digest(SOURCE_RUN))
    checkpoints = sorted((SOURCE_RUN / "checkpoints").glob("round-*")) \
        if (SOURCE_RUN / "checkpoints").is_dir() else []
    checker.equal("isolation/source-checkpoints-still-eight", 8, len(checkpoints))
    checker.check("isolation/output-has-no-checkpoints",
                  not (output / "checkpoints").exists())
    checker.check("isolation/output-has-no-model-bundles",
                  not (output / "candidate-model").exists()
                  and not (output / "initial-model").exists())

    # 2) **不触发训练**：先做源码级检查（不引用 Trainer/学习目标），再做**行为级**检查
    #    （进程内跑一次缩短版诊断，拦截 Trainer 构造与模型包落盘，计数必须为 0）。
    import re

    source = (REPO / "ai-the-spire" / "ai_the_spire" / "delivery" / "diagnose.py").read_text(
        encoding="utf-8")
    forbidden = (r"\bTrainer\s*\(", r"import\s+Trainer", r"from\s+\S+\s+import\s+[^\n]*\bTrainer\b",
                 r"\btrainer\b", r"\btrain_epoch\b", r"\bppo_loss\b", r"models\.learning",
                 r"\bAdam\s*\(")
    hits = [(pattern, line.strip()) for pattern in forbidden for line in source.splitlines()
            if re.search(pattern, line) and not line.strip().startswith("#")]
    checker.equal("isolation/diagnose-source-has-no-training-calls", [], hits)

    import ai_the_spire.delivery.diagnose as diagnose_module
    import ai_the_spire.models.bundle as bundle_module
    import ai_the_spire.models.learning as learning_module

    calls = {"trainer": 0, "save_bundle": 0}
    original_trainer_init = learning_module.Trainer.__init__
    original_save_bundle = bundle_module.save_bundle

    def spy_trainer_init(self, *arguments, **keywords):
        calls["trainer"] += 1
        return original_trainer_init(self, *arguments, **keywords)

    def spy_save_bundle(*arguments, **keywords):
        calls["save_bundle"] += 1
        return original_save_bundle(*arguments, **keywords)

    short = root / "diagnose-short"
    original_seeds = diagnose_module.DIAGNOSE_ENVIRONMENT_SEEDS
    diagnose_module.DIAGNOSE_ENVIRONMENT_SEEDS = (40000, 40001)
    learning_module.Trainer.__init__ = spy_trainer_init
    bundle_module.save_bundle = spy_save_bundle
    try:
        class _Args:
            pass

        arguments = _Args()
        arguments.command = "diagnose"
        arguments.run = str(SOURCE_RUN)
        arguments.output = str(short)
        diagnose_module.run(arguments)
        ran = True
    except Exception as failure:  # noqa: BLE001 - 缩短版也必须正常返回
        ran = False
        checker.check("isolation/short-diagnose-runs", False, f"{type(failure).__name__}: {failure}")
    finally:
        learning_module.Trainer.__init__ = original_trainer_init
        bundle_module.save_bundle = original_save_bundle
        diagnose_module.DIAGNOSE_ENVIRONMENT_SEEDS = original_seeds
    if ran:
        checker.check("isolation/short-diagnose-runs", True)
    checker.equal("isolation/no-trainer-constructed", 0, calls["trainer"])
    checker.equal("isolation/no-model-bundle-written", 0, calls["save_bundle"])
    if ran:
        short_document = read_json(short / "diagnosis.json")
        checker.equal("isolation/short-diagnose-episodes", 8,
                      len(short_document["four_policy_comparison"]["per_episode"]))

    candidate_meta = read_json(SOURCE_RUN / "candidate-model" / "metadata.json")
    checker.equal("isolation/candidate-weights-unchanged",
                  candidate_meta["weights_digest"],
                  document["models"]["candidate"]["weights_digest"])
    checker.equal("isolation/candidate-training-label-unchanged",
                  candidate_meta["training"], "ppo(steps=16)")

    # 3) 不读 final / 未来保留种子：只允许出现在"记录在案但未访问"的字段里。
    offending = []
    for path, value in _seed_values(document):
        if path[:1] == ("source_run_scope",):
            continue  # 范围记录本身允许列出"未访问"的保留种子
        values = value if isinstance(value, list) else [value]
        for item in values:
            if isinstance(item, int) and (item in FINAL_SEEDS or item in RESERVED_SEEDS):
                offending.append((path, item))
    checker.equal("isolation/no-final-or-reserved-seed-used", [], offending)
    checker.equal("isolation/four-policy-seeds-are-diagnose-seeds", list(range(40000, 40008)),
                  document["four_policy_comparison"]["plan"]["seeds"])
    checker.check("isolation/scope-marks-final-unaccessed",
                  "未被访问" in document["source_run_scope"]["note"],
                  document["source_run_scope"]["note"])

    # 4) 预算：实际用量在限额内，且最多 4 策略 x 8 局。
    per_episode = document["four_policy_comparison"]["per_episode"]
    checker.equal("isolation/episode-count-within-budget", 32, len(per_episode))
    checker.check("isolation/confirmed-actions-within-budget",
                  document["confirmed_actions_used"] <= document["limits"]["max_confirmed_actions"],
                  str(document["confirmed_actions_used"]))
    checker.check("isolation/command-seconds-within-budget",
                  document["command_seconds"] <= document["limits"]["command_time_limit_s"],
                  str(document["command_seconds"]))
    checker.check("isolation/per-episode-cap-respected",
                  all(item["confirmed_actions"]
                      <= document["limits"]["max_decisions_per_episode"] for item in per_episode),
                  "有单局超过每局动作上限")
    checker.check("isolation/no-failures", not document["failures"], str(document["failures"]))
    checker.check("isolation/stopped-early-flag-false",
                  document["four_policy_comparison"]["stopped_early_on_failure"] is False)

    # 5) 故障/资源路径：命令时钟耗尽 ⇒ 非零退出、不开启新局、不写半份统计。
    import ai_the_spire.delivery.diagnose as diagnose_module
    from ai_the_spire.delivery.errors import DeliveryError

    exhausted = root / "diagnose-time-exhausted"
    original_limit = diagnose_module.DIAGNOSE_COMMAND_TIME_LIMIT_S
    diagnose_module.DIAGNOSE_COMMAND_TIME_LIMIT_S = 0.0
    try:
        class _Args:
            pass

        arguments = _Args()
        arguments.command = "diagnose"
        arguments.run = str(SOURCE_RUN)
        arguments.output = str(exhausted)
        diagnose_module.run(arguments)
        checker.check("isolation/time-exhausted-nonzero", False, "时钟耗尽却返回 0")
    except DeliveryError as failure:
        checker.check("isolation/time-exhausted-nonzero", failure.exit_code == 1, str(failure))
    finally:
        diagnose_module.DIAGNOSE_COMMAND_TIME_LIMIT_S = original_limit
    written = read_json(exhausted / "diagnosis.json")
    checker.check("isolation/time-exhausted-ran-no-episode",
                  "four_policy_comparison" not in written
                  and int(written.get("confirmed_actions_used", 0) or 0) == 0,
                  str(written.get("confirmed_actions_used")))
    checker.equal("isolation/time-exhausted-exit-category", "fault",
                  written.get("exit_category"))
    _ = output


_RUNNERS = {
    "command": _run_command,
    "rule": _run_rule,
    "probe": _run_probe,
    "isolation": _run_isolation,
}


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="D2 诊断验收（命令/规则/探针/隔离）")
    parser.add_argument("--only", action="append", choices=GROUPS, default=None)
    parser.add_argument("--list", action="store_true")
    args = parser.parse_args(argv)
    if args.list:
        for name in GROUPS:
            print(name)
        return 0

    selected = args.only or list(GROUPS)
    root = workspace()
    print(f"[d2] 工作目录：{root}")
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
