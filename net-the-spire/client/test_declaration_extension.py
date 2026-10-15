"""ENV-T03 声明扩展子包的真实 stdio 证据：`--scenario text-rules`（0.2，同一通用规则提供方）。

派单依据 `docs/design/net-the-spire/design.md` §7.3 第 4 项：扩展例**只在规则包**新增公开字段与合法
Action，net 生产源码不新增逐项注册行；目录/observe 出现对应内容、step 执行正确；新增内部块不成为外部操作。

本脚本驱动**真实宿主进程**（`Net.TheSpire.Host` 的 NDJSON stdio 通道），用同一段提供方代码分别加载
`baseline.spire` 与 `extended.spire`：两者共用同一个 `public-metadata.json`，差别只在规则声明本身。
每个响应帧都用与 C# 端**同一份** `docs/contracts/net/0.2/wire.schema.json` 校验，并把请求/响应原始帧
写进 NDJSON 证据日志（`--log`）。这条通道是 C# 检查之外的独立对照：C# 走进程内 `ControlHost`，
本脚本走真实进程 + 真实字节。

用法（仓库根目录）::

    python -X utf8 -u net-the-spire/client/test_declaration_extension.py [--only GROUP] [--log FILE]

分组：``catalog``（只改规则包 ⇒ 目录只多出新增公开字段/动作；隐藏字段与内部 procedure 不出现）、
``trajectory``（start→observe→候选→step→observe 看到新增字段真实变化）、``identity``（身份随规则变化、
不含每局 seed）、``rejections``（错身份/过期候选/未知候选在执行前拒绝、重发幂等、换载荷冲突）、
``schema``（0.2 机器 schema 直接校验目录/快照/错误帧）。

退出码：0 全通过、1 有失败、2 参数或环境错误（如 schema 缺失/宿主未构建）。
"""

from __future__ import annotations

import argparse
import json
import pathlib
import sys
from typing import Optional

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "tools" / "contract-check"))

from net_client import REPO_ROOT, NetClient  # noqa: E402

import jsonschema_min  # noqa: E402
import wire_canonical  # noqa: E402

FIXTURES = REPO_ROOT / "net-the-spire" / "fixtures" / "declaration-extension"
SCHEMA_PATH = REPO_ROOT / "docs" / "contracts" / "net" / "0.2" / "wire.schema.json"
SCENARIO_PROFILE = "net-text-rules-declaration-extension-v1"
GAME_SEED = "DECL-EXT-00"
GAME_SEED_SPACED = "DECL-EXT-00 "

MARKER_KIND = "net:kind:declaration:Marker"
VALUE_FIELD = "net:field:declaration:Marker.Value"
BUMP_FIELD = "net:field:declaration:Marker.Bump"
SECRET_FIELD = "net:field:declaration:Marker.Secret"
ADD_ACTION = "net:action:declaration:Add"
ADD_DELTA_FIELD = "net:field:declaration:Add.delta"
MARK_ACTION = "net:action:declaration:Mark"
MARK_AMOUNT_FIELD = "net:field:declaration:Mark.amount"
ANSWER_ACTION = "net:action:net.answer_decision"
TERMINAL_REASON = "net:reason:declaration:ReachedLimit"
TERMINAL_VALUE_FIELD = "net:field:declaration:ReachedLimit.Value"
#: 规则包内部（不对外）的名字：隐藏字段与未被引用的 procedure。二者都不得出现在公开载荷里。
HIDDEN_FIELD_NAME = "Secret"
INTERNAL_PROCEDURE_NAME = "Retally"
VANILLA_SCENARIO = "base-spire-combat-slice-v1"

GROUPS = ("catalog", "trajectory", "identity", "rejections", "schema")


def canonical_text(value) -> str:
    """规范字节文本；形状本身违反规范时报错文本，不抛异常（表现为一条失败断言）。"""
    try:
        return wire_canonical.canonical_text(value)
    except Exception as failure:  # noqa: BLE001 - 规范字节拒绝的形状也要作为证据保留
        return f"<canonical-error {type(failure).__name__}: {failure}>"


class Checker:
    def __init__(self) -> None:
        self.passed = 0
        self.failed = 0
        self.details: list[str] = []
        self.evidence: list[dict] = []
        self.banners: dict[str, str] = {}

    def check(self, case: str, condition: bool, detail: str = "") -> bool:
        if condition:
            self.passed += 1
        else:
            self.failed += 1
            self.details.append(f"  - {case}: {detail}")
        return condition

    def equal(self, case: str, expected, actual) -> bool:
        return self.check(case, expected == actual, f"expected {expected!r}, got {actual!r}")


class Fixture:
    """一个真实宿主进程：`--scenario text-rules` + 规则入口 + 共享描述文件。"""

    def __init__(self, checker: Checker, schema: dict, name: str, rules: str) -> None:
        self.checker = checker
        self.schema = schema
        self.name = name
        self.client = NetClient.host_from_solution(extra_args=[
            "--scenario", "text-rules",
            "--rules", str(FIXTURES / rules),
            "--public-metadata", str(FIXTURES / "public-metadata.json"),
        ])
        self._seq = 0

    # -- 通道 ------------------------------------------------------------------

    def call(self, operation: str, payload: Optional[dict] = None, *, request_id: Optional[str] = None,
             timeout: Optional[float] = None) -> dict:
        response = self.client.request(operation, payload, request_id=request_id, timeout=timeout)
        self._seq += 1
        self.checker.evidence.append({
            "fixture": self.name,
            "seq": self._seq,
            "operation": operation,
            "payload": payload if payload is not None else {},
            "response": response,
            "canonical": canonical_text(response),
        })
        errors = jsonschema_min.validate(response, self.schema)
        self.checker.check(f"{self.name}/schema[{operation}]", not errors, " | ".join(errors[:3]))
        return response

    def describe(self, *, request_id: Optional[str] = None) -> dict:
        return self.call("describe", {}, request_id=request_id)

    def get_catalog(self, catalog_id: str, *, request_id: Optional[str] = None) -> dict:
        return self.call("get_catalog", {"catalog_id": catalog_id}, request_id=request_id)

    def start(self, identity: dict, seed: Optional[str] = None, *, request_id: Optional[str] = None) -> dict:
        payload: dict = {"expected_identity": identity, "scenario_profile": SCENARIO_PROFILE}
        if seed is not None:
            payload["seed"] = seed
        return self.call("start", payload, request_id=request_id)

    def observe(self, session_id: str, *, request_id: Optional[str] = None) -> dict:
        return self.call("observe", {"session_id": session_id}, request_id=request_id)

    def step(self, session_id: str, observation_id: int, candidate_id: str, *,
             request_id: Optional[str] = None) -> dict:
        return self.call("step", {
            "session_id": session_id,
            "expected_observation_id": observation_id,
            "candidate_id": candidate_id,
        }, request_id=request_id)

    # -- 便捷读取 --------------------------------------------------------------

    @staticmethod
    def identity_of(response: dict) -> dict:
        return response["result"]["identity"]

    @staticmethod
    def snapshot_of(response: dict) -> dict:
        return response["result"]["snapshot"]

    def shutdown(self) -> None:
        self.client.shutdown()
        self.checker.banners[self.name] = self.client.stderr_text()


# ---- 快照/目录读取 -------------------------------------------------------------


def error_code(response: dict) -> str:
    return (response.get("error") or {}).get("code", "<ok>")


def execution_state(response: dict) -> Optional[str]:
    return (response.get("error") or {}).get("execution_state")


def retry_mode(response: dict) -> Optional[str]:
    return (response.get("error") or {}).get("retry_mode")


def entity_of(snapshot: dict, kind_id: str) -> Optional[dict]:
    for entity in snapshot.get("entities", []):
        if entity.get("kind_id") == kind_id:
            return entity
    return None


def fields_present(snapshot: dict) -> set:
    return {field.get("field_id")
            for entity in snapshot.get("entities", [])
            for field in entity.get("fields", [])}


def field_text(snapshot: dict, field_id: str) -> Optional[str]:
    for field in (field for entity in snapshot.get("entities", []) for field in entity.get("fields", [])):
        if field.get("field_id") == field_id:
            value = field.get("value")
            return value if isinstance(value, str) else None
    return None


def candidates(snapshot: dict, action_type: str) -> list:
    return [action for action in snapshot.get("legal_actions", []) if action.get("action_type") == action_type]


def candidate_values(snapshot: dict, action_type: str, field_id: str) -> list:
    values = [field.get("value")
              for action in candidates(snapshot, action_type)
              for field in action.get("fields", [])
              if field.get("field_id") == field_id]
    return sorted(values)


def candidate_id_of(checker: Checker, snapshot: dict, action_type: str, field_id: str, value: str,
                    case: str) -> Optional[str]:
    matches = [action for action in candidates(snapshot, action_type)
               if any(field.get("field_id") == field_id and field.get("value") == value
                      for field in action.get("fields", []))]
    checker.equal(f"{case}/unique-candidate[{action_type} {field_id}={value}]", 1, len(matches))
    return matches[0]["candidate_id"] if len(matches) == 1 else None


def catalog_ids(result: dict, table: str) -> list:
    return sorted(entry["id"] for entry in result["declarations"][table])


def catalog_entry(result: dict, table: str, entry_id: str) -> Optional[dict]:
    for entry in result["declarations"][table]:
        if entry["id"] == entry_id:
            return entry
    return None


# ---- 分组 ---------------------------------------------------------------------


def _run_catalog(checker: Checker, schema: dict) -> None:
    """只改规则包 ⇒ 公开目录只多出新增公开字段与合法 Action；隐藏字段/内部 procedure 不出现。"""
    baseline = Fixture(checker, schema, "baseline", "baseline.spire")
    extended = Fixture(checker, schema, "extended", "extended.spire")
    try:
        base_describe = baseline.describe(request_id="catalog-baseline-describe")
        ext_describe = extended.describe(request_id="catalog-extended-describe")
        checker.check("catalog/baseline-describe-ok", base_describe.get("ok") is True,
                      f"{base_describe.get('error')}")
        checker.check("catalog/extended-describe-ok", ext_describe.get("ok") is True,
                      f"{ext_describe.get('error')}")
        base_identity = Fixture.identity_of(base_describe)
        ext_identity = Fixture.identity_of(ext_describe)
        checker.equal("catalog/protocol-version", "0.2", ext_identity["protocol_version"])
        scenarios = ext_describe["result"]["capabilities"]["scenario_profiles"]
        checker.check("catalog/scenario-profile-is-fixture",
                      bool(scenarios) and all(entry["id"] == SCENARIO_PROFILE for entry in scenarios),
                      f"{[entry['id'] for entry in scenarios]}")
        checker.check("catalog/scenario-is-not-vanilla",
                      all(entry["id"] != VANILLA_SCENARIO for entry in scenarios),
                      "独立扩展 fixture 不得冒称完整原版场景")

        base_catalog = baseline.get_catalog(base_identity["catalog_id"], request_id="catalog-baseline")
        ext_catalog = extended.get_catalog(ext_identity["catalog_id"], request_id="catalog-extended")
        checker.check("catalog/baseline-catalog-ok", base_catalog.get("ok") is True,
                      f"{base_catalog.get('error')}")
        checker.check("catalog/extended-catalog-ok", ext_catalog.get("ok") is True,
                      f"{ext_catalog.get('error')}")
        base_result = base_catalog["result"]
        ext_result = ext_catalog["result"]
        checker.equal("catalog/catalog-id-matches-identity", ext_identity["catalog_id"], ext_result["catalog_id"])
        checker.equal("catalog/ruleset-id-matches-identity", ext_identity["ruleset_id"], ext_result["ruleset_id"])

        base_fields = set(catalog_ids(base_result, "fields"))
        ext_fields = set(catalog_ids(ext_result, "fields"))
        base_actions = set(catalog_ids(base_result, "action_types"))
        ext_actions = set(catalog_ids(ext_result, "action_types"))

        # 序数排序下 "Mark." < "Marker"（'.' 0x2E < 'e'），所以 Mark.amount 排在 Marker.Bump 之前。
        checker.equal("catalog/added-public-fields", [MARK_AMOUNT_FIELD, BUMP_FIELD], sorted(ext_fields - base_fields))
        checker.equal("catalog/removed-public-fields", [], sorted(base_fields - ext_fields))
        checker.equal("catalog/added-actions", [MARK_ACTION], sorted(ext_actions - base_actions))
        checker.equal("catalog/removed-actions", [], sorted(base_actions - ext_actions))
        checker.equal("catalog/terminal-reasons-unchanged", base_result["declarations"]["terminal_reasons"],
                      ext_result["declarations"]["terminal_reasons"])
        checker.equal("catalog/entity-kinds-unchanged", catalog_ids(base_result, "entity_kinds"),
                      catalog_ids(ext_result, "entity_kinds"))
        checker.check("catalog/net-answer-action-unchanged",
                      ANSWER_ACTION in base_actions and ANSWER_ACTION in ext_actions,
                      "net 侧只有既有的应答动作，扩展例没有新增任何 net 注册行")

        ext_text = json.dumps(ext_result, ensure_ascii=False)
        checker.check("catalog/hidden-field-absent",
                      SECRET_FIELD not in ext_fields and HIDDEN_FIELD_NAME not in ext_text,
                      "引擎声明为 Hidden 的字段不得进入公开目录")
        checker.check("catalog/internal-procedure-absent",
                      INTERNAL_PROCEDURE_NAME not in ext_text
                      and not any(INTERNAL_PROCEDURE_NAME in action for action in ext_actions),
                      "内部 procedure 不得发布成 Action")

        bump = catalog_entry(ext_result, "fields", BUMP_FIELD)
        checker.check("catalog/bump-present", bump is not None, "新增公开字段必须在目录里")
        if bump is not None:
            checker.equal("catalog/bump-value-type", "number", bump["value_type"])
            checker.equal("catalog/bump-number-format", "integer", bump["number_format"])
            checker.equal("catalog/bump-required", True, bump["required"])
            checker.equal("catalog/bump-visibility", "public", bump["visibility"])
            checker.equal("catalog/bump-applies-to", [MARKER_KIND], sorted(bump["applies_to"]["entity_kinds"]))

        amount = catalog_entry(ext_result, "fields", MARK_AMOUNT_FIELD)
        checker.check("catalog/mark-parameter-present", amount is not None, "新增动作的参数必须是公开字段")
        if amount is not None:
            checker.equal("catalog/mark-parameter-applies-to", [MARK_ACTION],
                          sorted(amount["applies_to"]["action_types"]))

        mark = catalog_entry(ext_result, "action_types", MARK_ACTION)
        checker.check("catalog/mark-present", mark is not None, "新增合法动作必须在目录里")
        if mark is not None:
            checker.equal("catalog/mark-required-fields", [MARK_AMOUNT_FIELD], sorted(mark["required_fields"]))
            checker.equal("catalog/mark-optional-fields", [], mark["optional_fields"])

        terminal = catalog_entry(ext_result, "terminal_reasons", TERMINAL_REASON)
        checker.check("catalog/terminal-present", terminal is not None, "终局声明必须在目录里")
        if terminal is not None:
            checker.equal("catalog/terminal-outcome", "win", terminal["outcome"])
            checker.equal("catalog/terminal-field-slot", 1,
                          sum(TERMINAL_VALUE_FIELD in slot
                              for slot in (terminal["required_fields"], terminal["optional_fields"])))
    finally:
        baseline.shutdown()
        extended.shutdown()


def _run_trajectory(checker: Checker, schema: dict) -> None:
    """start → observe → 新动作真实候选 → step → observe：新增公开字段的真实变化。"""
    fixture = Fixture(checker, schema, "extended", "extended.spire")
    try:
        identity = Fixture.identity_of(fixture.describe(request_id="trajectory-describe"))
        started = fixture.start(identity, seed=GAME_SEED, request_id="trajectory-start")
        checker.check("trajectory/start-ok", started.get("ok") is True, f"{started.get('error')}")
        result = started.get("result") or {}
        checker.equal("trajectory/start-result-keys", {"session_id", "seed", "snapshot"}, set(result))
        checker.equal("trajectory/start-seed-echo", GAME_SEED, result.get("seed"))
        session_id = result.get("session_id")
        snapshot = result.get("snapshot") or {}
        checker.check("trajectory/seed-not-in-snapshot", GAME_SEED not in json.dumps(snapshot, ensure_ascii=False),
                      "每局 seed 是控制元数据，不得进入公开快照")
        checker.equal("trajectory/status", "decision", snapshot.get("status"))
        checker.check("trajectory/terminal-result-null", snapshot.get("terminal_result") is None,
                      f"{snapshot.get('terminal_result')}")
        checker.check("trajectory/marker-instance-present", entity_of(snapshot, MARKER_KIND) is not None,
                      "开局必须出现 Marker 实例（规则包的 start 入口已执行）")
        checker.equal("trajectory/initial-value", "0", field_text(snapshot, VALUE_FIELD))
        checker.equal("trajectory/initial-bump", "0", field_text(snapshot, BUMP_FIELD))
        checker.check("trajectory/hidden-field-absent",
                      SECRET_FIELD not in fields_present(snapshot) and HIDDEN_FIELD_NAME not in json.dumps(snapshot),
                      "隐藏字段不得出现在观察里")
        checker.equal("trajectory/add-candidates", ["1", "2"], candidate_values(snapshot, ADD_ACTION, ADD_DELTA_FIELD))
        checker.equal("trajectory/mark-candidates", ["1", "3"],
                      candidate_values(snapshot, MARK_ACTION, MARK_AMOUNT_FIELD))
        legal_text = json.dumps(snapshot.get("legal_actions"), ensure_ascii=False)
        checker.check("trajectory/internal-procedure-not-a-candidate",
                      INTERNAL_PROCEDURE_NAME not in legal_text and HIDDEN_FIELD_NAME not in legal_text,
                      "内部 procedure/隐藏字段不得成为外部候选")

        mark = candidate_id_of(checker, snapshot, MARK_ACTION, MARK_AMOUNT_FIELD, "3", "trajectory")
        if mark is None:
            return
        stepped = fixture.step(session_id, snapshot["observation_id"], mark, request_id="trajectory-mark")
        checker.check("trajectory/mark-step-ok", stepped.get("ok") is True, f"{stepped.get('error')}")
        after_mark = Fixture.snapshot_of(fixture.observe(session_id, request_id="trajectory-observe-1"))
        checker.equal("trajectory/bump-after-mark", "3", field_text(after_mark, BUMP_FIELD))
        checker.equal("trajectory/value-untouched-by-mark", "0", field_text(after_mark, VALUE_FIELD))
        checker.check("trajectory/hidden-still-absent", SECRET_FIELD not in fields_present(after_mark),
                      f"{sorted(fields_present(after_mark))}")

        add = candidate_id_of(checker, after_mark, ADD_ACTION, ADD_DELTA_FIELD, "2", "trajectory")
        if add is None:
            return
        stepped_add = fixture.step(session_id, after_mark["observation_id"], add, request_id="trajectory-add")
        checker.check("trajectory/add-step-ok", stepped_add.get("ok") is True, f"{stepped_add.get('error')}")
        after_add = Fixture.snapshot_of(fixture.observe(session_id, request_id="trajectory-observe-2"))
        checker.equal("trajectory/value-after-add", "2", field_text(after_add, VALUE_FIELD))
        checker.equal("trajectory/bump-after-add", "3", field_text(after_add, BUMP_FIELD))
        checker.check("trajectory/observation-advanced",
                      after_add["observation_id"] > snapshot["observation_id"],
                      f"{snapshot['observation_id']} -> {after_add['observation_id']}")
    finally:
        fixture.shutdown()


def _run_identity(checker: Checker, schema: dict) -> None:
    """身份反映规则声明变化，且不含每局 seed（同一规则包内可重复、可跨会话复用）。"""
    baseline = Fixture(checker, schema, "baseline", "baseline.spire")
    extended = Fixture(checker, schema, "extended", "extended.spire")
    try:
        base_first = Fixture.identity_of(baseline.describe(request_id="identity-baseline-1"))
        base_second = Fixture.identity_of(baseline.describe(request_id="identity-baseline-2"))
        ext_first = Fixture.identity_of(extended.describe(request_id="identity-extended-1"))
        checker.equal("identity/stable-within-same-rules", base_first, base_second)
        for key in ("ruleset_id", "catalog_id", "backend_profile_id"):
            checker.check(f"identity/{key}-moves-with-rules", base_first[key] != ext_first[key],
                          f"{base_first[key]} vs {ext_first[key]}")
        checker.check("identity/prefixes",
                      base_first["ruleset_id"].startswith("net:ruleset:")
                      and base_first["catalog_id"].startswith("net:catalog:")
                      and base_first["backend_profile_id"].startswith("net:backend:"),
                      f"{base_first}")
        checker.check("identity/no-seed-inside",
                      all(GAME_SEED not in str(value) and GAME_SEED_SPACED not in str(value)
                          for value in ext_first.values()),
                      f"{ext_first}")
        checker.check("identity/protocol", ext_first["protocol_version"] == "0.2", f"{ext_first}")

        started = extended.start(ext_first, seed=GAME_SEED, request_id="identity-start")
        checker.check("identity/start-ok", started.get("ok") is True, f"{started.get('error')}")
        ext_again = Fixture.identity_of(extended.describe(request_id="identity-extended-2"))
        checker.equal("identity/unchanged-by-start", ext_first, ext_again)

        catalog = extended.get_catalog(ext_first["catalog_id"], request_id="identity-catalog")
        checker.check("identity/catalog-ok", catalog.get("ok") is True, f"{catalog.get('error')}")
        checker.equal("identity/catalog-id-agrees", ext_first["catalog_id"], catalog["result"]["catalog_id"])
        checker.equal("identity/ruleset-id-agrees", ext_first["ruleset_id"], catalog["result"]["ruleset_id"])
    finally:
        baseline.shutdown()
        extended.shutdown()


def _run_rejections(checker: Checker, schema: dict) -> None:
    """拒绝在执行前发生：错身份/未知候选/过期快照不推进状态；重发幂等；换载荷冲突。"""
    fixture = Fixture(checker, schema, "extended", "extended.spire")
    try:
        identity = Fixture.identity_of(fixture.describe(request_id="rejections-describe"))

        wrong_protocol = fixture.start(dict(identity, protocol_version="0.1"), seed=GAME_SEED,
                                       request_id="rejections-protocol")
        checker.equal("rejections/wrong-protocol", "invalid_input", error_code(wrong_protocol))
        checker.equal("rejections/wrong-protocol-not-started", "not_started", execution_state(wrong_protocol))
        for label, key in (("catalog", "catalog_id"), ("ruleset", "ruleset_id"), ("backend", "backend_profile_id")):
            response = fixture.start(dict(identity, **{key: "net:fixture:not-this-one"}),
                                     seed=GAME_SEED, request_id=f"rejections-{label}")
            checker.equal(f"rejections/wrong-{label}", "version_mismatch", error_code(response))
            checker.equal(f"rejections/wrong-{label}-not-started", "not_started", execution_state(response))
            checker.equal(f"rejections/wrong-{label}-stop", "stop", retry_mode(response))
        mismatch = fixture.call("start", {"expected_identity": identity, "scenario_profile": VANILLA_SCENARIO,
                                          "seed": GAME_SEED}, request_id="rejections-scenario")
        checker.equal("rejections/wrong-scenario", "unsupported_capability", error_code(mismatch))
        empty_seed = fixture.start(identity, seed="", request_id="rejections-empty-seed")
        checker.equal("rejections/empty-seed", "invalid_input", error_code(empty_seed))
        checker.check("rejections/empty-seed-message", "seed" in (empty_seed.get("error") or {}).get("path", "")
                      or bool((empty_seed.get("error") or {}).get("message")),
                      f"{empty_seed.get('error')}")

        first = fixture.start(identity, seed=GAME_SEED, request_id="rejections-start-1")
        checker.check("rejections/start-ok", first.get("ok") is True, f"{first.get('error')}")
        session_id = first["result"]["session_id"]
        checker.check("rejections/no-session-before-accepted-start", session_id.endswith("-1"), session_id)
        resend = fixture.start(identity, seed=GAME_SEED, request_id="rejections-start-1")
        checker.check("rejections/start-resend-ok", resend.get("ok") is True, f"{resend.get('error')}")
        checker.equal("rejections/start-resend-identical", canonical_text(first), canonical_text(resend))
        conflict = fixture.start(identity, seed=GAME_SEED_SPACED, request_id="rejections-start-1")
        checker.equal("rejections/start-conflict", "request_conflict", error_code(conflict))

        second = fixture.start(identity, seed=GAME_SEED_SPACED, request_id="rejections-start-2")
        checker.check("rejections/second-start-ok", second.get("ok") is True, f"{second.get('error')}")
        checker.equal("rejections/second-seed-echoed-verbatim", GAME_SEED_SPACED, second["result"]["seed"])
        checker.check("rejections/second-session-independent", second["result"]["session_id"].endswith("-2"),
                      second["result"]["session_id"])

        snapshot = first["result"]["snapshot"]
        unknown = fixture.step(session_id, snapshot["observation_id"], "c-0000000000000000",
                               request_id="rejections-unknown-candidate")
        checker.equal("rejections/unknown-candidate", "illegal_candidate", error_code(unknown))
        checker.equal("rejections/unknown-candidate-not-started", "not_started", execution_state(unknown))
        checker.equal("rejections/unknown-candidate-retry", "correct_input", retry_mode(unknown))

        add = candidate_id_of(checker, snapshot, ADD_ACTION, ADD_DELTA_FIELD, "2", "rejections")
        if add is None:
            return
        executed = fixture.step(session_id, snapshot["observation_id"], add, request_id="rejections-step")
        checker.check("rejections/step-ok", executed.get("ok") is True, f"{executed.get('error')}")
        after = Fixture.snapshot_of(fixture.observe(session_id, request_id="rejections-observe-1"))
        checker.equal("rejections/value-after-step", "2", field_text(after, VALUE_FIELD))

        stale_candidate = candidate_id_of(checker, after, ADD_ACTION, ADD_DELTA_FIELD, "1", "rejections")
        if stale_candidate is None:
            return
        stale = fixture.step(session_id, snapshot["observation_id"], stale_candidate, request_id="rejections-stale")
        checker.equal("rejections/stale-observation", "stale_observation", error_code(stale))
        checker.equal("rejections/stale-observation-not-started", "not_started", execution_state(stale))
        checker.equal("rejections/stale-observation-retry", "query_request", retry_mode(stale))
        unchanged = Fixture.snapshot_of(fixture.observe(session_id, request_id="rejections-observe-2"))
        checker.equal("rejections/stale-did-not-execute", "2", field_text(unchanged, VALUE_FIELD))

        replay = fixture.step(session_id, snapshot["observation_id"], add, request_id="rejections-step")
        checker.check("rejections/step-replay-ok", replay.get("ok") is True, f"{replay.get('error')}")
        checker.equal("rejections/step-replay-identical", canonical_text(executed), canonical_text(replay))
        after_replay = Fixture.snapshot_of(fixture.observe(session_id, request_id="rejections-observe-3"))
        checker.equal("rejections/replay-executed-once", "2", field_text(after_replay, VALUE_FIELD))
        changed = fixture.step(session_id, unchanged["observation_id"], stale_candidate, request_id="rejections-step")
        checker.equal("rejections/step-conflict", "request_conflict", error_code(changed))
    finally:
        fixture.shutdown()


def _run_schema(checker: Checker, schema: dict) -> None:
    """0.2 机器 schema：逐帧（`Fixture.call`）之外，目录/快照文档本身也要能独立通过同一份 schema。"""
    fixture = Fixture(checker, schema, "extended", "extended.spire")
    try:
        protocol = schema["$defs"]["PublicCatalog"]["properties"]["protocol_version"]
        checker.check("schema/is-0-2", protocol.get("const") == "0.2", f"{protocol}")
        described = fixture.describe(request_id="schema-describe")
        identity = Fixture.identity_of(described)
        catalog = fixture.get_catalog(identity["catalog_id"], request_id="schema-catalog")
        started = fixture.start(identity, seed=GAME_SEED, request_id="schema-start")
        checker.check("schema/start-ok", started.get("ok") is True, f"{started.get('error')}")
        session_id = started["result"]["session_id"]
        snapshot = started["result"]["snapshot"]
        add = candidate_id_of(checker, snapshot, ADD_ACTION, ADD_DELTA_FIELD, "2", "schema")
        stepped = None
        if add is not None:
            stepped = fixture.step(session_id, snapshot["observation_id"], add, request_id="schema-step")
        observed = fixture.observe(session_id, request_id="schema-observe")
        rejected = fixture.start(dict(identity, catalog_id="net:catalog:0000000000000000"), seed=GAME_SEED,
                                 request_id="schema-rejected")
        checker.equal("schema/rejected-error-code", "version_mismatch", error_code(rejected))

        documents = [
            ("describe-result", "DescribeResult", described["result"]),
            ("catalog-result", "PublicCatalog", catalog["result"]),
            ("start-result", "StartResult", started["result"]),
            ("start-snapshot", "PublicSnapshot", snapshot),
            ("observe-result", "ObserveResult", observed["result"]),
            ("observe-snapshot", "PublicSnapshot", Fixture.snapshot_of(observed)),
        ]
        if stepped is not None and (stepped.get("ok") is True):
            documents.append(("step-result", "ObserveResult", stepped["result"]))
            documents.append(("step-snapshot", "PublicSnapshot", stepped["result"]["snapshot"]))
        # 根 schema 是 Request/Response/PublicCatalog/PublicSnapshot 的 oneOf，裸「结果对象」不在其中，
        # 所以按声明名校验（等价于 C# E-08 的 schema.ValidateDefinition(name, document)）：显式把
        # `$defs[name]` 当根传进去（jsonschema_min.validate 的 root 参数）。
        for case, definition, document in documents:
            errors = jsonschema_min.validate(document, schema["$defs"][definition], schema)
            checker.check(f"schema/document[{case}]", not errors, " | ".join(errors[:3]))
    finally:
        fixture.shutdown()


RUNNERS = {
    "catalog": _run_catalog,
    "trajectory": _run_trajectory,
    "identity": _run_identity,
    "rejections": _run_rejections,
    "schema": _run_schema,
}


def _write_log(path: pathlib.Path, checker: Checker) -> None:
    """NDJSON 证据：头部 + 每个宿主的就绪横幅（stderr）+ 每一帧请求/响应与规范字节。"""
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        header = {
            "kind": "evidence-header",
            "tool": "net-the-spire/client/test_declaration_extension.py",
            "scenario_profile": SCENARIO_PROFILE,
            "fixtures": str(FIXTURES.relative_to(REPO_ROOT)),
            "schema": str(SCHEMA_PATH.relative_to(REPO_ROOT)),
            "game_seeds": [GAME_SEED, GAME_SEED_SPACED],
            "passed": checker.passed,
            "failed": checker.failed,
        }
        handle.write(json.dumps(header, ensure_ascii=False, sort_keys=True) + "\n")
        for name, banner in sorted(checker.banners.items()):
            handle.write(json.dumps({"kind": "host-banner", "fixture": name, "stderr": banner},
                                    ensure_ascii=False, sort_keys=True) + "\n")
        for record in checker.evidence:
            handle.write(json.dumps(record, ensure_ascii=False, sort_keys=True) + "\n")


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="ENV-T03 声明扩展子包的真实 stdio 证据")
    parser.add_argument("--only", choices=GROUPS, help="只跑一个分组")
    parser.add_argument("--log", type=pathlib.Path, help="把每一帧写进这个 NDJSON 证据文件")
    args = parser.parse_args(argv)

    if not SCHEMA_PATH.exists():
        print(f"缺少 0.2 wire schema：{SCHEMA_PATH}", file=sys.stderr)
        return 2
    schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))

    checker = Checker()
    for group in GROUPS:
        if args.only is not None and group != args.only:
            continue
        before_passed, before_failed = checker.passed, checker.failed
        try:
            RUNNERS[group](checker, schema)
        except Exception as exception:  # noqa: BLE001 - 单组异常记为一条失败，不中止整套
            checker.check(f"{group}/harness", False, f"{type(exception).__name__}: {exception}")
        group_passed = checker.passed - before_passed
        group_failed = checker.failed - before_failed
        state = "PASS" if group_failed == 0 else "FAIL"
        print(f"[{state}] {group}: {group_passed} passed, {group_failed} failed, "
              f"{group_passed + group_failed} executed")

    if args.log is not None:
        _write_log(args.log, checker)
        print(f"evidence: {args.log}")

    for detail in checker.details:
        print(detail, file=sys.stderr)
    print(f"TOTAL: {checker.passed} passed, {checker.failed} failed, "
          f"{checker.passed + checker.failed} executed")
    return 1 if checker.failed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
