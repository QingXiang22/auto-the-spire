"""D-01 的检查：版本化 EpisodeRecord、公开产物索引与独立复核。

用法（仓库根目录，`record` 组需要先 ``dotnet build AutoTheSpire.sln``）::

    python ai-the-spire/tests/d01_records.py [--only GROUP]

分组：

* ``record`` —— 跑一局真实 episode → 建记录 → 落盘 → 独立重读、验摘要/引用并**重编码**（需要宿主）；
* ``dedup`` —— 重复 request_id、重复 episode_id、被改动的记录、索引漂移都必须被发现；
* ``ending`` —— 终止/截断/故障/中断分类互斥，未知结果不得补造成失败或下一状态；
* ``index`` —— 多条记录的索引与记录一致、按 id 可读、未知 id 报错、重建索引与磁盘一致。

退出码：0 全通过、1 有失败、2 参数或环境错误。
"""

from __future__ import annotations

import copy
import json
import pathlib
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "ai-the-spire"))
sys.path.insert(0, str(REPO / "net-the-spire" / "client"))

from ai_the_spire.data import (  # noqa: E402
    RECORD_FORMAT_VERSION,
    EpisodeStore,
    RecordError,
    build_record,
    classify_ending,
    validate_document,
)
from ai_the_spire.encoding import EncodingConfig, build_manifest, tensorize  # noqa: E402
from ai_the_spire.runner import EpisodeRunner  # noqa: E402
from ai_the_spire.runner.episode import tensor_digest  # noqa: E402
from net_client import NetClient  # noqa: E402

GROUPS = ("record", "dedup", "ending", "index")
SCENARIO = "base-spire-combat-slice-v1"
SEED = 20261001
MAX_STEPS = 25

_ARTIFACTS: dict = {}


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


def _run_real_episode():
    """跑一局真实 episode 并返回记录（`record` 组用）。"""
    client = NetClient.host_from_solution()
    try:
        runner = EpisodeRunner(client, seed=SEED, max_steps=MAX_STEPS, executor_seed=SEED + 1)
        result = runner.run()
        record = build_record(result, provenance={"entry": "d01_records/record", "source": "stdio-simulation"})
        return record, result
    finally:
        client.shutdown()


def _reencode(record_wire) -> list[str]:
    """独立重编码：只用记录里的目录与配置，不用活动宿主。返回与录像不符的问题列表。"""
    identities = record_wire["identities"]
    manifest = build_manifest(
        record_wire["catalog"],
        EncodingConfig(),
        backend_profile_id=identities["backend_profile_id"],
        gym_id=identities["gym_id"],
        scenario_profile=identities["scenario_profile"],
    )
    problems: list[str] = []
    for key in ("dictionary_version", "manifest_digest", "source_catalog_digest",
                "encoding_config_digest", "tensor_layout_version"):
        if getattr(manifest, key) != identities[key]:
            problems.append(f"离线重建的 {key} 与记录身份不一致")
    for transition in record_wire["transitions"]:
        encoded = tensorize(transition["snapshot"], manifest, catalog=record_wire["catalog"])
        if tensor_digest(encoded) != transition["tensor_digest"]:
            problems.append(f"转移 {transition['request_id']} 的重编码摘要与记录不一致")
        # 引用核对：候选与位置引用的实体必须都在本快照内（validate/tensorize 已强制，这里显式再断言一次）。
        snapshot = transition["snapshot"]
        instances = {entity["instance_id"] for entity in snapshot.get("entities") or []}
        for pile in snapshot.get("piles") or []:
            for member in pile.get("visible_members") or []:
                if member not in instances:
                    problems.append(f"转移 {transition['request_id']} 的 visible_members 悬空引用: {member}")
            for position in pile.get("known_positions") or []:
                if position["card_ref"] not in instances:
                    problems.append(f"转移 {transition['request_id']} 的 known_positions 悬空引用")
    return problems


# -- record -------------------------------------------------------------------------------------------------


def _run_record(checker: Checker) -> None:
    record, result = _run_real_episode()
    checker.check("record/ending-classified", record.ending_class in
                  ("terminal_win", "terminal_loss", "truncated"), record.ending_class)
    checker.check("record/has-transitions", len(record.transitions) > 0, "至少一条转移")
    checker.equal("record/transition-outcomes-match-run",
                  result.steps, len(record.transitions))

    # 每条转移:完整快照 + 只覆盖真实候选的概率 + 选中项概率与记录一致。
    for transition in record.to_wire()["transitions"]:
        checker.check(f"record/t{transition['index']}-snapshot-stored",
                      isinstance(transition["snapshot"], dict) and "entities" in transition["snapshot"],
                      "必须保存完整公开快照")
        if transition["outcome"] == "terminal":
            continue
        candidates = transition["candidates"]
        checker.check(f"record/t{transition['index']}-probability-covers-candidates",
                      abs(sum(candidates.values()) - 1.0) < 1e-9 and transition["semantic_key"] in candidates,
                      "实际行为概率必须覆盖候选且包含选中项")
        checker.check(f"record/t{transition['index']}-behavior-probability-recorded",
                      abs(transition["behavior_probability"] - candidates[transition["semantic_key"]]) < 1e-12,
                      "必须记录**实际执行**的概率，而不是事后重算的别的分布")

    path = pathlib.Path(tempfile.mkdtemp(prefix="d01-"))
    store = EpisodeStore(path)
    written = store.write(record)
    checker.check("record/written", written.exists(), f"{written}")

    # 独立重读 + 验摘要 + 重编码。
    reloaded = store.read(record.episode_id)
    checker.equal("record/digest-stable-across-read", record.digest(), reloaded["record_digest"])
    checker.equal("record/format-version", RECORD_FORMAT_VERSION, reloaded["record_format_version"])
    problems = _reencode(reloaded)
    checker.check("record/offline-reencode-matches", not problems, f"{problems}")

    # 会话句柄留在存储里（快照结构要求它、重编码也需要它），但**不得进入记录摘要**：
    # 否则同一配置的两次重放会得到不同摘要（protocol §8.1 / contract-artifacts §3 排除控制元数据）。
    checker.check("record/snapshot-keeps-session-handle",
                  "session_id" in reloaded["transitions"][0]["snapshot"], "快照必须保留完整结构")
    from ai_the_spire.data.record import EpisodeRecord as _Record  # noqa: PLC0415

    variant = copy.deepcopy(reloaded)
    variant["transitions"][0]["snapshot"]["session_id"] = "session-other-process-1"
    checker.check("record/session-handle-not-in-digest",
                  store.read(record.episode_id)["record_digest"] == _Record.from_wire(_resign(variant)).digest(),
                  "换会话句柄不得改变记录摘要")

    # 索引与记录一致，且不含时间戳（内容身份派生）。
    entries = store.index()
    checker.equal("record/index-has-entry", 1, len(entries))
    checker.equal("record/index-ending", record.ending_class, entries[0]["ending_class"])
    checker.equal("record/index-steps", len(record.transitions), entries[0]["steps"])
    checker.check("record/index-no-timestamp",
                  not any(key in entries[0] for key in ("created_at", "timestamp", "time")),
                  "索引不得用时间戳充当身份")
    checker.check("record/store-verify-all-clean", not store.verify_all(), f"{store.verify_all()}")

    # episode_id 由内容派生：改一个转移摘要必须换 id。
    changed = copy.deepcopy(reloaded)
    changed["transitions"][0]["tensor_digest"] = "sha256:" + "0" * 64
    from ai_the_spire.data import EpisodeRecord

    checker.check("record/episode-id-derived-from-content",
                  EpisodeRecord.from_wire(_resign(changed)).episode_id != record.episode_id,
                  "内容变化必须改变 episode_id")

    _ARTIFACTS["store"] = store
    _ARTIFACTS["record"] = reloaded
    _ARTIFACTS["root"] = path


def _resign(document: dict) -> dict:
    """改内容后重新签名（模拟"内容变了但想沿用旧 id"的攻击面）。"""
    from ai_the_spire.data.store import _digest_material, digest_of  # noqa: PLC0415

    document = copy.deepcopy(document)
    document["record_digest"] = digest_of(_digest_material(document))
    return document


# -- dedup --------------------------------------------------------------------------------------------------


def _run_dedup(checker: Checker) -> None:
    record = _ARTIFACTS.get("record")
    store = _ARTIFACTS.get("store")
    if record is None or store is None:
        checker.check("dedup/record-available", False, "dedup 组依赖 record 组；请一起运行")
        return

    # ① 重复 request_id 不得被当成两个样本。
    duplicated = copy.deepcopy(record)
    duplicated["transitions"][1]["request_id"] = duplicated["transitions"][0]["request_id"]
    try:
        validate_document(duplicated)
        checker.check("dedup/duplicate-request-id-rejected", False, "重复 request_id 被接受了")
    except RecordError as failure:
        checker.check("dedup/duplicate-request-id-rejected", "重复" in str(failure), str(failure))

    # ② 同一 episode_id 不得被静默覆盖。
    from ai_the_spire.data import EpisodeRecord

    try:
        store.write(EpisodeRecord.from_wire(record))
        checker.check("dedup/duplicate-episode-id-rejected", False, "同一 episode_id 被覆盖了")
    except RecordError as failure:
        checker.check("dedup/duplicate-episode-id-rejected", "拒绝覆盖" in str(failure), str(failure))

    # ③ 被改动的记录必须被发现（摘要不符）。
    tampered = copy.deepcopy(record)
    tampered["transitions"][0]["semantic_key"] = "tampered"
    path = store.record_path(record["episode_id"])
    original = path.read_bytes()
    try:
        path.write_bytes(json.dumps(tampered, ensure_ascii=False, sort_keys=True,
                                    separators=(",", ":")).encode("utf-8"))
        try:
            store.read(record["episode_id"])
            checker.check("dedup/tampered-record-detected", False, "改动后的记录被当成合法")
        except RecordError as failure:
            checker.check("dedup/tampered-record-detected", "摘要" in str(failure), str(failure))
    finally:
        path.write_bytes(original)

    # ④ 索引漂移必须被发现：删掉索引最后一行后 verify_all 应报不一致。
    index_path = store.index_path
    index_text = index_path.read_text(encoding="utf-8")
    try:
        index_path.write_text("", encoding="utf-8")
        problems = store.verify_all()
        checker.check("dedup/index-drift-detected", bool(problems), f"{problems}")
    finally:
        index_path.write_text(index_text, encoding="utf-8")
    checker.check("dedup/restored-store-clean", not store.verify_all(), f"{store.verify_all()}")


# -- ending -------------------------------------------------------------------------------------------------


def _run_ending(checker: Checker) -> None:
    checker.equal("ending/win", "terminal_win", classify_ending("terminal", {"outcome": "win"}))
    checker.equal("ending/loss", "terminal_loss", classify_ending("terminal", {"outcome": "loss"}))
    checker.equal("ending/truncated", "truncated", classify_ending("truncated", None))
    checker.equal("ending/faulted", "faulted", classify_ending("faulted", None))
    checker.equal("ending/interrupted", "interrupted", classify_ending("interrupted", None))
    # 未知 outcome 不得被硬塞进胜负。
    checker.equal("ending/unknown-outcome-not-forced-into-winloss", "faulted",
                  classify_ending("terminal", {"outcome": "other"}))

    record = _ARTIFACTS.get("record")
    if record is None:
        checker.check("ending/record-available", False, "ending 组依赖 record 组；请一起运行")
        return

    # 非终止类记录不得带 terminal_result（不得把截断/故障伪装成胜负）。
    fake = copy.deepcopy(record)
    fake["ending"] = {"class": "truncated", "detail": "x", "terminal": {"outcome": "win", "reason_id": "r"}}
    try:
        validate_document(fake)
        checker.check("ending/non-terminal-must-not-carry-terminal", False, "截断被伪装成胜负")
    except RecordError as failure:
        checker.check("ending/non-terminal-must-not-carry-terminal", "不得带 terminal_result" in str(failure),
                      str(failure))

    # 终止类记录必须带 terminal_result。
    fake = copy.deepcopy(record)
    fake["ending"] = {"class": "terminal_win", "detail": "x", "terminal": None}
    try:
        validate_document(fake)
        checker.check("ending/terminal-must-carry-terminal", False, "终止类记录缺少 terminal_result")
    except RecordError as failure:
        checker.check("ending/terminal-must-carry-terminal", "必须带 terminal_result" in str(failure), str(failure))

    # 未知结果的转移不得补造下一状态或声称执行过某个候选。
    fake = copy.deepcopy(record)
    fake["transitions"][0]["outcome"] = "unknown"
    fake["transitions"][0]["next_observation_id"] = 999
    try:
        validate_document(fake)
        checker.check("ending/unknown-must-not-fabricate-next-state", False, "未知结果补造了下一状态")
    except RecordError as failure:
        checker.check("ending/unknown-must-not-fabricate-next-state", "不得补造下一状态" in str(failure),
                      str(failure))

    fake = copy.deepcopy(record)
    fake["transitions"][0]["outcome"] = "unknown"
    fake["transitions"][0]["next_observation_id"] = None
    fake["transitions"][0]["candidate_id"] = "c-1"
    try:
        validate_document(fake)
        checker.check("ending/unknown-must-not-claim-execution", False, "未知结果声称执行过候选")
    except RecordError as failure:
        checker.check("ending/unknown-must-not-claim-execution", "不得声称已执行" in str(failure), str(failure))

    # 真实记录的结束分类必须与运行一致（不得事后改成胜负）。
    expected = classify_ending("terminal", record["ending"]["terminal"]) \
        if record["ending"]["class"] in ("terminal_win", "terminal_loss") else record["ending"]["class"]
    checker.equal("ending/real-record-class-consistent", expected, record["ending"]["class"])


# -- index --------------------------------------------------------------------------------------------------


def _run_index(checker: Checker) -> None:
    record = _ARTIFACTS.get("record")
    root = _ARTIFACTS.get("root")
    if record is None or root is None:
        checker.check("index/record-available", False, "index 组依赖 record 组；请一起运行")
        return

    from ai_the_spire.data import EpisodeRecord  # noqa: PLC0415

    store = EpisodeStore(root)
    # 追加两局"同目录同策略但不同种子"的记录（id 必须不同）。
    for seed in (SEED + 1, SEED + 2):
        variant = copy.deepcopy(record)
        variant["identities"]["seed"] = str(seed)
        variant = _resign(variant)
        store.write(EpisodeRecord.from_wire(variant))

    entries = store.index()
    checker.equal("index/three-records", 3, len(entries))
    checker.equal("index/unique-episode-ids", 3, len({entry["episode_id"] for entry in entries}))
    for entry in entries:
        read_back = store.read(entry["episode_id"])
        checker.equal(f"index/{entry['episode_id'][:12]}-digest", entry["record_digest"],
                      read_back["record_digest"])
        checker.equal(f"index/{entry['episode_id'][:12]}-ending", entry["ending_class"],
                      read_back["ending"]["class"])
    checker.check("index/rebuild-matches-disk",
                  sorted(store.rebuild_index(), key=lambda item: item["episode_id"]) ==
                  sorted(entries, key=lambda item: item["episode_id"]),
                  "重建索引必须与磁盘索引一致")
    checker.check("index/verify-all-clean", not store.verify_all(), f"{store.verify_all()}")

    try:
        store.read("ep-does-not-exist")
        checker.check("index/unknown-id-rejected", False, "未知 id 被接受了")
    except RecordError as failure:
        checker.check("index/unknown-id-rejected", "不存在" in str(failure), str(failure))


RUNNERS = {"record": _run_record, "dedup": _run_dedup, "ending": _run_ending, "index": _run_index}


def main(argv: list[str]) -> int:
    only = None
    if len(argv) >= 2 and argv[0] == "--only":
        only = argv[1]
        if only not in GROUPS:
            print(f"未知分组 '{only}'；可用：{', '.join(GROUPS)}", file=sys.stderr)
            return 2

    checker = Checker()
    for group in GROUPS:
        if only is not None and group != only:
            continue
        before_passed, before_failed = checker.passed, checker.failed
        try:
            RUNNERS[group](checker)
        except Exception as exception:  # 单组异常记为一条失败，不中止整套
            checker.check(f"{group}/harness", False, f"{type(exception).__name__}: {exception}")
        group_passed = checker.passed - before_passed
        group_failed = checker.failed - before_failed
        state = "PASS" if group_failed == 0 else "FAIL"
        print(f"[{state}] {group}: {group_passed} passed, {group_failed} failed, "
              f"{group_passed + group_failed} executed")

    for detail in checker.details:
        print(detail, file=sys.stderr)
    if _ARTIFACTS.get("root") is not None:
        print(f"  store: {_ARTIFACTS['root']}", file=sys.stderr)
    print(f"TOTAL: {checker.passed} passed, {checker.failed} failed, "
          f"{checker.passed + checker.failed} executed")
    return 1 if checker.failed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
