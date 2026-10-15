"""N-04 / H-03 的 wire 级检查：真实启动宿主进程，走 NDJSON stdio，逐条核对协议行为。

用法（仓库根目录）：``python net-the-spire/client/test_client.py [--only GROUP]``

分组：``wire``（0.1 会话全流程）、``transport``（帧上限/畸形帧）、``recovery``（超时恢复规则）、
``faults``（**故障注入**：延迟管道、宿主断开、脚本化测试后端。这些是注入的故障，不是真实游戏证据）、
``formal``（**正式 0.2 场景**：`--scenario formal-dev` 接 base 唯一正式工厂，每局游戏字符串 seed）。
退出码：0 全通过、1 有失败、2 参数或环境错误（如宿主未构建）。
"""

from __future__ import annotations

import json
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from net_client import (  # noqa: E402
    REPO_ROOT, NetClient, NetClientError, OutcomeUnknown, host_command,
)

SCENARIO = "base-spire-combat-slice-v1"
#: 正式 dev 构建的范围身份（base `BaseSpireScope.Dev`）：0.2 的 `scenario_profile` 必须用它。
FORMAL_SCENARIO = "base-spire-singleplayer-dev-v1"
FORMAL_SEED = "ENV-IRONCLAD-00"
FORMAL_SECOND_SEED = "ENV-T03-C-02"
GROUPS = ("wire", "transport", "recovery", "faults", "formal")


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


def _identity(client: NetClient) -> dict:
    describe = client.describe()
    return describe["result"]["identity"]


def _run_wire(checker: Checker) -> None:
    client = NetClient.host_from_solution()
    try:
        describe = client.describe()
        checker.check("wire/describe-ok", describe.get("ok") is True, f"{describe}")
        identity = describe["result"]["identity"]
        capabilities = describe["result"]["capabilities"]
        checker.check("wire/describe-identity", bool(identity.get("ruleset_id")) and bool(identity.get("catalog_id")),
                      f"{identity}")
        checker.equal("wire/describe-backend-kind", "simulation", capabilities["backend_kind"])
        checker.check("wire/describe-declares-request-status", "request_status" in capabilities["operations"],
                      f"{capabilities['operations']}")
        checker.check("wire/describe-no-attach", "attach" not in capabilities["operations"],
                      f"{capabilities['operations']}")

        catalog = client.get_catalog(identity["catalog_id"])
        checker.check("wire/get_catalog-ok", catalog.get("ok") is True, f"{catalog.get('error')}")
        declarations = catalog["result"]["declarations"]
        for key in ("entity_kinds", "content_types", "fields", "action_types", "terminal_reasons"):
            checker.check(f"wire/get_catalog-{key}", len(declarations[key]) > 0, "空数组")
        # H-02：五个牌区各有一个 pile_type，且 kind_id 唯一（protocol.md:83）。
        pile_types = declarations["pile_types"]
        checker.equal("wire/get_catalog-pile-type-count", 5, len(pile_types))
        checker.equal("wire/get_catalog-pile-kind-unique", len(pile_types),
                      len({pile["kind_id"] for pile in pile_types}))
        checker.check("wire/get_catalog-pile-members-declared",
                      all(pile["member_kind_ids"] for pile in pile_types),
                      f"{[pile['member_kind_ids'] for pile in pile_types]}")

        bad_catalog = client.get_catalog("fixture:not-this-catalog")
        checker.equal("wire/get_catalog-unknown-id", "version_mismatch", bad_catalog["error"]["code"])

        started = client.start(identity, SCENARIO, seed=20261001)
        checker.check("wire/start-ok", started.get("ok") is True, f"{started.get('error')}")
        session_id = started["result"]["session_id"]
        checker.check("wire/start-returns-snapshot", isinstance(started["result"].get("snapshot"), dict), "无快照")

        observed = client.observe(session_id)
        snapshot = observed["result"]["snapshot"]
        checker.equal("wire/observe-active", "active", observed["result"]["session_status"])
        observation_id = snapshot["observation_id"]
        checker.check("wire/observe-has-observation-id", isinstance(observation_id, int) and observation_id > 0,
                      f"{observation_id}")
        actions = snapshot["legal_actions"]
        checker.check("wire/observe-has-candidates", len(actions) > 0, "无候选")
        candidate = actions[0]["candidate_id"]

        step = client.step(session_id, observation_id, candidate, request_id="step-1")
        checker.check("wire/step-ok", step.get("ok") is True, f"{step.get('error')}")

        status = client.request_status("step-1", session_id)
        checker.equal("wire/request_status-completed", "completed", status["result"]["status"])
        checker.equal("wire/request_status-returns-original", step, status["result"]["response"])

        replay = client.step(session_id, observation_id, candidate, request_id="step-1")
        checker.equal("wire/step-replay-identical", step, replay)

        stale = client.step(session_id, observation_id, candidate, request_id="step-2")
        checker.equal("wire/step-stale-rejected", "stale_observation", stale["error"]["code"])

        closed = client.close_session(session_id)
        checker.equal("wire/close-status", "closed", closed["result"]["session_status"])
        after_close = client.observe(session_id)
        checker.equal("wire/observe-after-close", "session_closed", after_close["error"]["code"])

        unknown = client.observe("session-404")
        checker.equal("wire/observe-unknown-session", "session_not_found", unknown["error"]["code"])
    finally:
        client.shutdown()


def _run_transport(checker: Checker) -> None:
    client = NetClient.host_from_solution(frame_limit=256)
    try:
        # 刚好在上限内的帧必须被**正常解析并分发**（判别力：拒绝的是"超限"，不是这个 operation）。
        # 用合法载荷 + 较长 session_id：应答应是 session_not_found，而不是 capacity_exceeded。
        inside = client.request("observe", {"session_id": "s" * 40})
        checker.equal("transport/frame-under-limit-parsed", "session_not_found", inside["error"]["code"])
        oversized = client.request("observe", {"session_id": "session-1", "pad": "x" * 600})
        checker.equal("transport/oversized-frame-rejected", "capacity_exceeded", oversized["error"]["code"])
        alive = client.describe()
        checker.check("transport/host-alive-after-oversize", alive.get("ok") is True, f"{alive.get('error')}")

        identity = alive["result"]["identity"]
        malformed = _send_raw(client, "{not json")
        checker.equal("transport/malformed-frame-rejected", "invalid_input", malformed["error"]["code"])
        checker.check("transport/host-alive-after-malformed", client.describe().get("ok") is True, "宿主不应退出")

        empty = _send_raw(client, "")
        checker.equal("transport/empty-frame-rejected", "invalid_input", empty["error"]["code"])
        checker.check("transport/still-serves-catalog",
                      client.get_catalog(identity["catalog_id"]).get("ok") is True, "后续请求应正常")
    finally:
        client.shutdown()


def _send_raw(client: NetClient, line: str) -> dict:
    response = client.send_raw(line, timeout=30.0)
    assert response is not None, "宿主没有回应该帧"
    return response


def _run_recovery(checker: Checker) -> None:
    client = NetClient.host_from_solution()
    try:
        unknown = client.request_status("never-sent", "session-1")
        checker.equal("recovery/unknown-id-is-not-found", "not_found", unknown["result"]["status"])
        try:
            client._recover("never-sent")  # noqa: SLF001
            checker.check("recovery/not-found-not-proof-of-non-execution", False,
                          "客户端必须把 not_found 当成结果不确定，而不是未执行")
        except OutcomeUnknown as failure:
            checker.check("recovery/not-found-not-proof-of-non-execution", "不得盲目重发" in str(failure), str(failure))
        checker.check("recovery/client-has-no-blind-retry", not hasattr(client, "retry"), "客户端不应提供盲目重发")
    finally:
        client.shutdown()


def _run_faults(checker: Checker) -> None:
    """H-03：**故障注入**证据。每一组都注明注入的是什么，不冒充真实游戏或真实模拟。"""
    _faults_delayed_pipeline(checker)
    _faults_disconnect(checker)
    _faults_restart_session_ids(checker)
    _faults_scripted_states(checker)


def _faults_delayed_pipeline(checker: Checker) -> None:
    # 注入：宿主 stdout 固定延迟 2.5s 才转发给客户端 → 客户端 0.6s 就超时。
    client = NetClient.host_from_solution_delayed(delay=2.5)
    try:
        identity = _identity(client)
        started = client.request("start", {
            "expected_identity": identity,
            "scenario_profile": SCENARIO,
            "seed": "20261001",
        }, timeout=0.6)
        # 恢复取回的必须是 **start 的结果**（session_id + snapshot），不是 request_status 的包装
        # （后者只有 status/response 两个键）。这是"迟到响应按 request_id 归位"的判别点。
        checker.check("fault-injection/delayed-start-recovers", started.get("ok") is True, f"{started.get('error')}")
        result = started.get("result") or {}
        checker.check("fault-injection/delayed-response-attributed",
                      "session_id" in result and "status" not in result,
                      f"恢复取回的响应不是原请求的结果：{sorted(result)}")
        checker.check("fault-injection/delayed-response-archived-by-id", client.late_archived >= 1,
                      f"必须按 request_id 归档迟到响应（late_archived={client.late_archived}）")
        session_id = result.get("session_id")
        observed = client.observe(session_id)
        checker.equal("fault-injection/delayed-start-created-one-session", "active",
                      observed["result"]["session_status"])
    finally:
        client.shutdown()


def _faults_disconnect(checker: Checker) -> None:
    # 注入：拿到会话后直接杀掉宿主进程（模拟进程丢失），再发控制请求。
    client = NetClient.host_from_solution()
    try:
        identity = _identity(client)
        session_id = client.start(identity, SCENARIO, seed=20261001)["result"]["session_id"]
        client.kill()
        try:
            client.observe(session_id, timeout=0.5)
            checker.check("fault-injection/disconnect-not-fabricated", False, "宿主已丢失却返回了结果")
        except OutcomeUnknown as failure:
            checker.check("fault-injection/disconnect-not-fabricated",
                          "不得盲目重发" in str(failure) and "停止控制" in str(failure), str(failure))
        checker.check("fault-injection/disconnect-classified", client.transport_failures >= 1,
                      f"必须记为传输层失败（transport_failures={client.transport_failures}）")
        checker.check("fault-injection/disconnect-no-blind-retry", not hasattr(client, "retry"),
                      "客户端不得提供盲目重发接口")
    finally:
        client.shutdown()


def _faults_restart_session_ids(checker: Checker) -> None:
    # 注入：用两个真实宿主进程代表"重启"。旧进程发出的句柄不得在新进程里指向另一局。
    first = NetClient.host_from_solution()
    try:
        identity = _identity(first)
        before = first.start(identity, SCENARIO, seed=7)["result"]["session_id"]
    finally:
        first.shutdown()

    second = NetClient.host_from_solution()
    try:
        identity = _identity(second)
        after = second.start(identity, SCENARIO, seed=7)["result"]["session_id"]
        checker.check("fault-injection/session-id-not-reused-across-restart", before != after,
                      f"{before} vs {after}")
        crossed = second.observe(before)
        checker.equal("fault-injection/old-handle-not-in-new-process", "session_not_found",
                      crossed["error"]["code"])
        checker.equal("fault-injection/new-handle-still-addressed", "active",
                      second.observe(after)["result"]["session_status"])
    finally:
        second.shutdown()


def _faults_scripted_states(checker: Checker) -> None:
    # 注入：脚本化测试后端（**不是游戏后端**）。同步宿主里 pending/outcome_unknown 不可达，
    # 协议为异步提供方与进程丢失后的重新识别预留了它们，客户端处理必须单独验证。
    pending = NetClient.scripted({"silent": ["lost-1"], "request_status": ["pending"]})
    try:
        pending.request("observe", {"session_id": "s-1"}, request_id="lost-1", timeout=0.4)
        checker.check("fault-injection/pending-not-treated-as-done", False, "pending 被当成了完成结果")
    except OutcomeUnknown as failure:
        checker.check("fault-injection/pending-not-treated-as-done", False, f"pending 不应报不确定：{failure}")
    except NetClientError as failure:
        checker.check("fault-injection/pending-not-treated-as-done",
                      "pending" in str(failure) and "不得重发" in str(failure), str(failure))
    finally:
        pending.shutdown()

    unknown = NetClient.scripted({"silent": ["lost-2"], "request_status": ["outcome_unknown"]})
    try:
        unknown.request("observe", {"session_id": "s-1"}, request_id="lost-2", timeout=0.4)
        checker.check("fault-injection/outcome-unknown-stops-control", False, "未知结果被当成了完成结果")
    except OutcomeUnknown as failure:
        checker.check("fault-injection/outcome-unknown-stops-control",
                      failure.status == "outcome_unknown" and "停止控制" in str(failure), str(failure))
    finally:
        unknown.shutdown()


def _formal_host(*, game_seed: str = FORMAL_SEED) -> NetClient:
    """正式 0.2 场景的宿主进程：`--scenario formal-dev` + Host 的显式默认游戏 seed。"""
    return NetClient.host_from_solution(extra_args=["--scenario", "formal-dev", "--game-seed", game_seed])


def _formal_cli_rejections(checker: Checker) -> None:
    """两个场景的开关互斥与未知场景必须**在启动前显式失败**（不静默忽略看起来生效的开关）。"""
    for args, label in (
        (["--scenario", "formal-dev", "--seed", "5"], "rejects-numeric-seed"),
        (["--scenario", "formal-dev", "--test-headbutt"], "rejects-test-headbutt"),
        (["--scenario", "formal-dev", "--author-content"], "rejects-author-content"),
        (["--scenario", "legacy-slice", "--game-seed", "x"], "legacy-rejects-game-seed"),
        (["--scenario", "no-such-scenario"], "unknown-scenario"),
    ):
        proc = subprocess.run(host_command() + args, input=b"", capture_output=True, cwd=str(REPO_ROOT))
        checker.equal(f"formal/cli-{label}-rc", 2, proc.returncode)
        diagnostic = proc.stderr.decode("utf-8", "replace")
        checker.check(f"formal/cli-{label}-diagnostic", "scenario" in diagnostic or "--seed" in diagnostic,
                      diagnostic.strip())


def _run_formal(checker: Checker) -> None:
    """正式 0.2 场景：真实 NDJSON 上跑通 describe/catalog/start/observe/step，并核对身份与拒绝语义。"""
    _formal_cli_rejections(checker)

    client = _formal_host()
    try:
        describe = client.describe()
        checker.check("formal/describe-ok", describe.get("ok") is True, f"{describe.get('error')}")
        identity = describe["result"]["identity"]
        capabilities = describe["result"]["capabilities"]
        checker.equal("formal/protocol-version", "0.2", identity["protocol_version"])
        scenarios = capabilities["scenario_profiles"]
        checker.equal("formal/scenario-profile", FORMAL_SCENARIO, scenarios[0]["id"])
        checker.check("formal/describe-scenario-ids", all(s["id"] == FORMAL_SCENARIO for s in scenarios),
                      f"{[s['id'] for s in scenarios]}")
        checker.check("formal/seed-control", capabilities["seed_control"] is True,
                      "0.2 声明 seed_control=true 就必须真的使用请求给的每局 seed")
        rng_profile = capabilities["execution_guarantees"]["rng_profile_id"]
        checker.check("formal/rng-profile-reported", bool(rng_profile), "rng_profile_id 必须报告实际 Profile")

        catalog = client.get_catalog(identity["catalog_id"])
        checker.check("formal/catalog-ok", catalog.get("ok") is True, f"{catalog.get('error')}")
        checker.equal("formal/catalog-protocol", "0.2", catalog["result"]["protocol_version"])
        declarations = catalog["result"]["declarations"]
        for key in ("entity_kinds", "content_types", "fields", "action_types"):
            checker.check(f"formal/catalog-{key}", len(declarations[key]) > 0, "空数组")
        unknown_catalog = client.get_catalog("fixture:not-this-catalog")
        checker.equal("formal/catalog-unknown-id", "version_mismatch", unknown_catalog["error"]["code"])

        started = client.start(identity, FORMAL_SCENARIO, seed=FORMAL_SEED)
        checker.check("formal/start-ok", started.get("ok") is True, f"{started.get('error')}")
        result = started["result"]
        checker.equal("formal/start-result-keys", {"session_id", "seed", "snapshot"}, set(result))
        checker.equal("formal/start-accepted-seed", FORMAL_SEED, result["seed"])
        session_id = result["session_id"]
        snapshot = result["snapshot"]
        checker.check("formal/start-snapshot", isinstance(snapshot, dict), "start 结果必须带快照")
        checker.equal("formal/start-snapshot-protocol", "0.2", snapshot["protocol_version"])
        checker.check("formal/start-no-seed-in-snapshot", FORMAL_SEED not in json.dumps(snapshot),
                      "每局 seed 是控制元数据，不得进入公开快照")

        actions = snapshot.get("legal_actions") or []
        checker.check("formal/start-has-candidates", len(actions) > 0, "开局稳定边界必须有公开候选")
        chosen = next((a for a in actions if a["action_type"].endswith("PlayCard")), None) \
            or next((a for a in actions if a["action_type"].endswith("EndTurn")), None)
        checker.check("formal/start-has-public-action", chosen is not None,
                      f"{[a['action_type'] for a in actions]}")
        step = client.step(session_id, snapshot["observation_id"], chosen["candidate_id"])
        checker.check("formal/step-ok", step.get("ok") is True, f"{step.get('error')}")
        advanced = step["result"]["snapshot"]
        checker.check("formal/step-advances-observation",
                      advanced["observation_id"] > snapshot["observation_id"], "推进后观察版本必须变化")
        stale = client.step(session_id, snapshot["observation_id"], chosen["candidate_id"])
        checker.equal("formal/step-stale-rejected", "stale_observation", stale["error"]["code"])

        # 同 Host 第二个 seed：不同会话、各自回报自己的实际接受值。
        second = client.start(identity, FORMAL_SCENARIO, seed=FORMAL_SECOND_SEED)
        checker.check("formal/second-seed-ok", second.get("ok") is True, f"{second.get('error')}")
        checker.equal("formal/second-seed-echo", FORMAL_SECOND_SEED, second["result"]["seed"])
        checker.check("formal/second-seed-independent", second["result"]["session_id"] != session_id,
                      "两局必须是不同的会话句柄")

        # 不带 seed：用 Host 的显式默认值，并回报**实际接受**的值。
        defaulted = client.start(identity, FORMAL_SCENARIO)
        checker.check("formal/default-seed-ok", defaulted.get("ok") is True, f"{defaulted.get('error')}")
        checker.equal("formal/default-seed-reported", FORMAL_SEED, defaulted["result"]["seed"])

        # 错误身份 / 旧切片场景 id 在创建之前拒绝（不自动降级）。
        # 错**协议版本**由版本分支的载荷校验挡下（invalid_input + not_started）；同版本下的身份不符
        # （错目录/错规则集）走 identity 比较 → version_mismatch。两者都在创建/启动之前。
        wrong_version = dict(identity)
        wrong_version["protocol_version"] = "0.1"
        rejected = client.start(wrong_version, FORMAL_SCENARIO, seed=FORMAL_SEED)
        checker.equal("formal/wrong-version-rejected", "invalid_input", rejected["error"]["code"])
        checker.equal("formal/wrong-version-not-started", "not_started", rejected["error"]["execution_state"])
        wrong_catalog = dict(identity)
        wrong_catalog["catalog_id"] = "fixture:other-catalog"
        mismatched = client.start(wrong_catalog, FORMAL_SCENARIO, seed=FORMAL_SEED)
        checker.equal("formal/wrong-catalog-rejected", "version_mismatch", mismatched["error"]["code"])
        checker.equal("formal/wrong-catalog-not-started", "not_started", mismatched["error"]["execution_state"])
        wrong_ruleset = dict(identity)
        wrong_ruleset["ruleset_id"] = "fixture:other-ruleset"
        ruleset_mismatch = client.start(wrong_ruleset, FORMAL_SCENARIO, seed=FORMAL_SEED)
        checker.equal("formal/wrong-ruleset-rejected", "version_mismatch", ruleset_mismatch["error"]["code"])
        legacy_scenario = client.start(identity, SCENARIO, seed=FORMAL_SEED)
        checker.equal("formal/legacy-scenario-rejected", "unsupported_capability",
                      legacy_scenario["error"]["code"])
        checker.equal("formal/legacy-scenario-not-started", "not_started",
                      legacy_scenario["error"]["execution_state"])

        # 0.2 的 seed 形状：空串与非字符串由请求层严格拒绝，不进入提供方。
        empty = client.start(identity, FORMAL_SCENARIO, seed="")
        checker.equal("formal/empty-seed-rejected", "invalid_input", empty["error"]["code"])
        numeric = client.request("start", {"expected_identity": identity, "scenario_profile": FORMAL_SCENARIO,
                                          "seed": 5})
        checker.equal("formal/non-string-seed-rejected", "invalid_input", numeric["error"]["code"])

        # 原样重发：同一 request_id → 逐字同一结果（不重新生成 seed、不启动第二局）。
        resend_first = client.start(identity, FORMAL_SCENARIO, seed=FORMAL_SEED, request_id="formal-resend")
        resend_again = client.start(identity, FORMAL_SCENARIO, seed=FORMAL_SEED, request_id="formal-resend")
        checker.equal("formal/start-resend-identical", resend_first, resend_again)
        conflict = client.start(identity, FORMAL_SCENARIO, seed=FORMAL_SECOND_SEED,
                                request_id="formal-resend")
        checker.equal("formal/start-resend-conflict", "request_conflict", conflict["error"]["code"])
    finally:
        client.shutdown()

    # 旧 0.1 场景不变：默认（不带 --scenario）仍是切片场景 + 数值 seed。
    legacy = NetClient.host_from_solution()
    try:
        legacy_identity = legacy.describe()["result"]["identity"]
        checker.equal("formal/legacy-protocol-unchanged", "0.1", legacy_identity["protocol_version"])
        legacy_start = legacy.start(legacy_identity, SCENARIO, seed=20261001)
        checker.check("formal/legacy-numeric-seed-ok", legacy_start.get("ok") is True,
                      f"{legacy_start.get('error')}")
        legacy_string = legacy.start(legacy_identity, SCENARIO, seed=FORMAL_SEED)
        checker.equal("formal/legacy-rejects-string-seed", "invalid_input", legacy_string["error"]["code"])
    finally:
        legacy.shutdown()


RUNNERS = {"wire": _run_wire, "transport": _run_transport, "recovery": _run_recovery, "faults": _run_faults,
           "formal": _run_formal}


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
    print(f"TOTAL: {checker.passed} passed, {checker.failed} failed, "
          f"{checker.passed + checker.failed} executed")
    return 1 if checker.failed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
