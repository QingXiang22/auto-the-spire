"""ENV-T03 C 线地图公开 Host 的真实进程证据：`--scenario formal-dev --scope dev-map`（0.2）。

派单依据 `docs/design/net-the-spire/design.md` §7（正式环境的 0.2 扩展）与 A 地图包
（`docs/review/records/2026-10-08/dsh-env-t05-map-room-handoff-2026-10-08.md`）交付的公开事实：
地图节点/有序集合/父子关系/选点候选全部由**引擎声明的公开语义**自动派生，net 侧只做透传，
不逐项登记 MapNode/MapNodes/MapChildren/ChooseMapPoint，也不读隐藏状态生成候选。

本脚本驱动**真实宿主进程**（`Net.TheSpire.Host` 的 NDJSON stdio 通道），范围由 `--scope dev-map`
显式选择（仍走唯一 `BaseSpireRuntimeFactory`，只是换了显式 ruleset/scope 配置，会话仍由本包启动一次）；
同时对照默认 `--scope dev`：缺省身份逐字不变，新增范围进身份、每局 seed 不进身份。
每个响应帧都用与 C# 端**同一份** `docs/specs/net/0.2/wire.schema.json` 校验，并把请求/响应原始帧
写进 NDJSON 证据日志（`--log`）。C# 侧的同一批检查走进程内 `ControlHost`（`FormalPathChecks.cs` 的
G-01…G-05），本脚本是真实进程 + 真实字节的独立对照。

候选来自实际引擎：非法/非后继/战斗中错阶段在任何状态或随机变更**之前**被拒；同 request_id 重发幂等；
错身份/过期观察被拒。合法选择 Monster 节点进入**既有正常战斗**；战斗里出牌要么正常结算，要么是引擎
具名故障（`backend_fault`/`unknown`/`stop`）——本脚本如实记录后者，不伪装成可继续，也不改血量/清场。

用法（仓库根目录）::

    python -X utf8 -u net-the-spire/client/test_map_host.py [--only GROUP] [--log FILE]

分组：``host``（范围选择入口：dev-map 进身份、dev 缺省不变、未知范围显式拒绝、seed 不进身份）、
``catalog``（目录里 MapNode/MapNodes/MapChildren/ChooseMapPoint 的公开声明形状全部由引擎派生）、
``trajectory``（两 seed：start→observe→公开候选=公开后继→选 Monster 进战斗→战斗中出牌如实记录）、
``rejections``（错身份/非法候选/已消费候选/过期观察在执行前拒绝且状态与抽数不变；重发幂等）、
``schema``（每类真实帧直接过 0.2 machine schema 与规范字节）。

退出码：0 全通过、1 有失败、2 参数或环境错误（如 schema 缺失/宿主未构建）。
"""

from __future__ import annotations

import argparse
import json
import pathlib
import subprocess
import sys
from typing import Optional

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "tools" / "contract-check"))

from net_client import REPO_ROOT, NetClient, host_command  # noqa: E402

import jsonschema_min  # noqa: E402
import wire_canonical  # noqa: E402

SCHEMA_PATH = REPO_ROOT / "docs" / "specs" / "net" / "0.2" / "wire.schema.json"
DEV_MAP_PROFILE = "base-spire-singleplayer-dev-map-v1"
DEV_PROFILE = "base-spire-singleplayer-dev-v1"
DEFAULT_SCOPE_NAME = "dev"
MAP_SCOPE_NAME = "dev-map"
#: 真实子进程证据用的两个游戏 seed（seed 不进任何身份；同一次运行里两次 start 才可比）。
EVIDENCE_SEEDS = ("t05-map-alpha", "t05-map-beta")

MAP_NODE_KIND = "net:kind:base-spire:MapNode"
RUN_KIND = "net:kind:base-spire:Run"
MAP_NODES_SEQUENCE = "net:sequence_type:base-spire:MapNodes"
MAP_CHILDREN_RELATION = "net:relation:base-spire:MapChildren"
CHOOSE_MAP_POINT = "net:action:base-spire:ChooseMapPoint"
CHOOSE_NODE_FIELD = "net:field:base-spire:ChooseMapPoint.Node"
PLAY_CARD = "net:action:base-spire:PlayCard"

MAP_NODE_ENTITY_FIELDS = {
    "net:field:base-spire:MapNode.Col": "number",
    "net:field:base-spire:MapNode.Row": "number",
    "net:field:base-spire:MapNode.Index": "number",
    "net:field:base-spire:MapNode.Type": "category",
    "net:field:base-spire:MapNode.Visited": "boolean",
}
RUN_MAP_FIELDS = {
    "net:field:base-spire:Run.CurrentNode": "entity_ref",
    "net:field:base-spire:Run.MapPhase": "number",
    "net:field:base-spire:Run.MapFloor": "number",
    "net:field:base-spire:Run.MapNodeCount": "number",
}
MAP_POINT_CATEGORIES = [
    f"net:cat:base-spire:MapPointType:{name}"
    for name in ("Ancient", "Boss", "Elite", "Monster", "RestSite", "Shop", "Treasure", "Unassigned", "Unknown")
]

GROUPS = ("host", "catalog", "trajectory", "rejections", "schema")


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
        self.faults: list[str] = []

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
    """一个真实宿主进程：正式场景 + 显式范围（默认 dev-map）。"""

    def __init__(self, checker: Checker, schema: dict, name: str,
                 scope: str = MAP_SCOPE_NAME) -> None:
        self.checker = checker
        self.schema = schema
        self.name = name
        self.scope = scope
        self.client = NetClient.host_from_solution(
            extra_args=["--scenario", "formal-dev", "--scope", scope])
        self._seq = 0

    # -- 通道 ------------------------------------------------------------------

    def call(self, operation: str, payload: Optional[dict] = None, *,
             request_id: Optional[str] = None) -> dict:
        response = self.client.request(operation, payload, request_id=request_id)
        self._seq += 1
        self.checker.evidence.append({
            "fixture": self.name,
            "scope": self.scope,
            "seq": self._seq,
            "request_id": request_id,
            "operation": operation,
            "payload": payload if payload is not None else {},
            "response": response,
            "canonical": canonical_text(response),
        })
        self.checker.check(f"{self.name}/no-map-diagnostics[{operation}]",
                           "MapDraws" not in json.dumps(response), "ordinary response leaked diagnostic field")
        errors = jsonschema_min.validate(response, self.schema)
        self.checker.check(f"{self.name}/schema[{operation}]", not errors, " | ".join(errors[:3]))
        return response

    def describe(self, *, request_id: Optional[str] = None) -> dict:
        return self.call("describe", {}, request_id=request_id)

    def get_catalog(self, catalog_id: str, *, request_id: Optional[str] = None) -> dict:
        return self.call("get_catalog", {"catalog_id": catalog_id}, request_id=request_id)

    def start(self, identity: dict, seed: Optional[str] = None, *,
              request_id: Optional[str] = None, profile: str = DEV_MAP_PROFILE) -> dict:
        payload: dict = {"expected_identity": identity, "scenario_profile": profile}
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

    def shutdown(self) -> None:
        self.client.shutdown()
        self.checker.banners[self.name] = self.client.stderr_text()


# ---- 载荷读取 -----------------------------------------------------------------


def error_code(response: dict) -> str:
    return (response.get("error") or {}).get("code", "<ok>")


def execution_state(response: dict) -> Optional[str]:
    return (response.get("error") or {}).get("execution_state")


def retry_mode(response: dict) -> Optional[str]:
    return (response.get("error") or {}).get("retry_mode")


def snapshot_of(response: dict) -> dict:
    return response["result"]["snapshot"]


def observation_of(response: dict) -> int:
    return int(snapshot_of(response)["observation_id"])


def entity_of(snapshot: dict, kind_id: str) -> Optional[dict]:
    for entity in snapshot.get("entities", []):
        if entity.get("kind_id") == kind_id:
            return entity
    return None


def entity_by_ref(snapshot: dict, instance_id: str) -> Optional[dict]:
    for entity in snapshot.get("entities", []):
        if entity.get("instance_id") == instance_id:
            return entity
    return None


def known_field(entity: Optional[dict], field_id: str):
    """只认 ``state == "known"`` 的公开值；unknown/hidden/redacted 都按"没有公开事实"处理。"""
    for field in (entity or {}).get("fields", []):
        if field.get("field_id") == field_id and field.get("state") == "known":
            return field.get("value")
    return None


def map_facts(snapshot: dict) -> dict:
    """运行实体的地图公开事实（字段缺失即 None，绝不编造默认值）。"""
    run = entity_of(snapshot, RUN_KIND)
    return {
        "run": run,
        "phase": known_field(run, "net:field:base-spire:Run.MapPhase"),
        "floor": known_field(run, "net:field:base-spire:Run.MapFloor"),
        "node_count": known_field(run, "net:field:base-spire:Run.MapNodeCount"),
        "current": known_field(run, "net:field:base-spire:Run.CurrentNode"),
        "encounter": known_field(run, "net:field:base-spire:Run.EncounterId"),
    }


def map_sequence(snapshot: dict) -> Optional[dict]:
    for sequence in snapshot.get("sequences", []):
        if sequence.get("type_id") == MAP_NODES_SEQUENCE:
            return sequence
    return None


def map_nodes(snapshot: dict) -> list:
    return [entity for entity in snapshot.get("entities", []) if entity.get("kind_id") == MAP_NODE_KIND]


def map_children(snapshot: dict, source_ref: str) -> list:
    """公开父子关系里该节点的后继（关系由引擎声明派生；这里只读公开 relations）。"""
    return sorted({relation.get("target_ref")
                   for relation in snapshot.get("relations", [])
                   if relation.get("type_id") == MAP_CHILDREN_RELATION
                   and relation.get("source_ref") == source_ref
                   and relation.get("target_ref")})


def map_node_type(snapshot: dict, instance_id: str) -> Optional[str]:
    value = known_field(entity_by_ref(snapshot, instance_id), "net:field:base-spire:MapNode.Type")
    return value.rsplit(":", 1)[-1] if isinstance(value, str) else None


def map_node_row(snapshot: dict, instance_id: str):
    row = known_field(entity_by_ref(snapshot, instance_id), "net:field:base-spire:MapNode.Row")
    return int(row) if row is not None else None


def candidate_ids(snapshot: dict, action_type: str) -> list:
    return sorted(action["candidate_id"] for action in snapshot.get("legal_actions", [])
                  if action.get("action_type") == action_type)


def candidate_for(snapshot: dict, action_type: str, field_id: str, value) -> Optional[str]:
    for action in snapshot.get("legal_actions", []):
        if action.get("action_type") != action_type:
            continue
        for field in action.get("fields", []):
            if field.get("field_id") == field_id and field.get("value") == value:
                return action.get("candidate_id")
    return None


def successor_values(snapshot: dict) -> list:
    return sorted(field.get("value")
                  for action in snapshot.get("legal_actions", [])
                  if action.get("action_type") == CHOOSE_MAP_POINT
                  for field in action.get("fields", [])
                  if field.get("field_id") == CHOOSE_NODE_FIELD)


def catalog_entry(declarations: dict, table: str, entry_id: str) -> Optional[dict]:
    for entry in declarations.get(table, []):
        if entry.get("id") == entry_id:
            return entry
    return None


# ---- 分组 ---------------------------------------------------------------------


def _run_host(checker: Checker, schema: dict) -> None:
    """范围选择入口：dev-map 显式进当前身份、seed 不进身份、未知范围显式拒绝；不冻结内容版本摘要。"""
    dev_map = Fixture(checker, schema, "dev-map", scope=MAP_SCOPE_NAME)
    dev = Fixture(checker, schema, "dev", scope=DEFAULT_SCOPE_NAME)
    map_describe = dev_map.describe(request_id="host-dev-map-describe")
    dev_describe = dev.describe(request_id="host-dev-describe")
    checker.check("host/dev-map-describe-ok", map_describe.get("ok") is True, str(map_describe.get("error")))
    checker.check("host/dev-describe-ok", dev_describe.get("ok") is True, str(dev_describe.get("error")))

    map_identity = Fixture.identity_of(map_describe)
    dev_identity = Fixture.identity_of(dev_describe)
    checker.equal("host/protocol-version", "0.2", map_identity["protocol_version"])
    # 身份结构本身就不带每局 seed：新增范围只能进 ruleset/catalog/backend 三个摘要。
    checker.equal("host/identity-keys", ["backend_profile_id", "catalog_id", "protocol_version", "ruleset_id"],
                  sorted(map_identity))
    map_profiles = [profile["id"] for profile in map_describe["result"]["capabilities"]["scenario_profiles"]]
    dev_profiles = [profile["id"] for profile in dev_describe["result"]["capabilities"]["scenario_profiles"]]
    checker.check("host/dev-map-profile-declared", DEV_MAP_PROFILE in map_profiles, str(map_profiles))
    checker.check("host/dev-profile-declared", DEV_PROFILE in dev_profiles, str(dev_profiles))
    checker.equal("host/scope-changes-ruleset-and-catalog",
                  ["catalog_id", "ruleset_id"],
                  sorted(key for key in map_identity if map_identity[key] != dev_identity[key]
                         and key != "backend_profile_id"))
    # 实测：范围连 backend profile 摘要一起换（多一条 act_1_map 随机流），协议版本不变。
    checker.check("host/scope-changes-backend-profile",
                  map_identity["backend_profile_id"] != dev_identity["backend_profile_id"],
                  f"dev={dev_identity['backend_profile_id']} dev-map={map_identity['backend_profile_id']}")
    checker.equal("host/scope-keeps-protocol-version", map_identity["protocol_version"],
                  dev_identity["protocol_version"])

    # 两次 describe 之间没有任何 start：静态身份不因会话/seed 改变。
    again = dev_map.describe(request_id="host-dev-map-describe-again")
    checker.equal("host/identity-stable-across-calls", map_identity, Fixture.identity_of(again))

    dev_map.shutdown()
    dev.shutdown()
    map_banner = checker.banners.get("dev-map", "")
    dev_banner = checker.banners.get("dev", "")
    checker.check("host/dev-map-banner-scope", "scope=" + DEV_MAP_PROFILE in map_banner
                  and "scope_name=dev-map" in map_banner, map_banner[:200])
    checker.check("host/dev-banner-scope", "scope=" + DEV_PROFILE in dev_banner
                  and "scope_name=dev" in dev_banner, dev_banner[:200])
    checker.check("host/banner-declares-protocol", "protocol=0.2" in map_banner, map_banner[:200])

    unknown = subprocess.run(host_command() + ["--scenario", "formal-dev", "--scope", "no-such-scope"],
                             input=b"", capture_output=True, cwd=str(REPO_ROOT), timeout=180)
    unknown_stderr = unknown.stderr.decode("utf-8", "replace")
    checker.check("host/unknown-scope-nonzero-exit", unknown.returncode != 0,
                  f"rc={unknown.returncode} stderr={unknown_stderr[:200]}")
    checker.check("host/unknown-scope-lists-options", "no-such-scope" in unknown_stderr
                  and MAP_SCOPE_NAME in unknown_stderr, unknown_stderr[:200])


def _run_catalog(checker: Checker, schema: dict) -> None:
    """目录把引擎声明的公开事实原样透传：节点种类/有序集合/父子关系/选点动作与字段。"""
    fixture = Fixture(checker, schema, "catalog")
    try:
        describe = fixture.describe(request_id="catalog-describe")
        identity = Fixture.identity_of(describe)
        catalog = fixture.get_catalog(identity["catalog_id"], request_id="catalog-get")
        declarations = catalog["result"]["declarations"]

        checker.check("catalog/MapNode-kind",
                      catalog_entry(declarations, "entity_kinds", MAP_NODE_KIND) is not None)
        sequence = catalog_entry(declarations, "sequence_types", MAP_NODES_SEQUENCE) or {}
        checker.equal("catalog/MapNodes-owner-kind", [RUN_KIND], sequence.get("owner_kind_ids"))
        checker.equal("catalog/MapNodes-element-kind", [MAP_NODE_KIND], sequence.get("element_kind_ids"))
        relation = catalog_entry(declarations, "relation_types", MAP_CHILDREN_RELATION) or {}
        checker.equal("catalog/MapChildren-target-kind", [MAP_NODE_KIND], relation.get("target_kind_ids"))
        checker.check("catalog/MapChildren-owner-source-kind",
                      MAP_NODE_KIND in (relation.get("source_kind_ids") or [])
                      and RUN_KIND in (relation.get("source_kind_ids") or []),
                      str(relation.get("source_kind_ids")))
        action = catalog_entry(declarations, "action_types", CHOOSE_MAP_POINT) or {}
        checker.equal("catalog/ChooseMapPoint-required-field", [CHOOSE_NODE_FIELD], action.get("required_fields"))
        checker.equal("catalog/ChooseMapPoint-optional-fields", [], action.get("optional_fields"))

        node_field = catalog_entry(declarations, "fields", CHOOSE_NODE_FIELD) or {}
        checker.equal("catalog/choose-node-value-kind", "entity_ref", node_field.get("value_type"))
        checker.equal("catalog/choose-node-target-kind", [MAP_NODE_KIND], node_field.get("target_kind_ids"))
        checker.equal("catalog/choose-node-required", True, node_field.get("required"))
        checker.equal("catalog/choose-node-applies-to", [CHOOSE_MAP_POINT],
                      (node_field.get("applies_to") or {}).get("action_types"))
        checker.equal("catalog/choose-node-visibility", "public", node_field.get("visibility"))

        for field_id, value_kind in MAP_NODE_ENTITY_FIELDS.items():
            entry = catalog_entry(declarations, "fields", field_id) or {}
            checker.equal(f"catalog/node-field[{field_id}]/value-kind", value_kind, entry.get("value_type"))
            checker.equal(f"catalog/node-field[{field_id}]/applies-to", [MAP_NODE_KIND],
                          (entry.get("applies_to") or {}).get("entity_kinds"))
            checker.equal(f"catalog/node-field[{field_id}]/visibility", "public", entry.get("visibility"))
        type_entry = catalog_entry(declarations, "fields", "net:field:base-spire:MapNode.Type") or {}
        checker.equal("catalog/node-type-categories", sorted(MAP_POINT_CATEGORIES),
                      sorted(type_entry.get("category_ids") or []))

        for field_id, value_kind in RUN_MAP_FIELDS.items():
            entry = catalog_entry(declarations, "fields", field_id) or {}
            checker.equal(f"catalog/run-map-field[{field_id}]/value-kind", value_kind, entry.get("value_type"))
            checker.equal(f"catalog/run-map-field[{field_id}]/applies-to", [RUN_KIND],
                          (entry.get("applies_to") or {}).get("entity_kinds"))
            checker.equal(f"catalog/run-map-field[{field_id}]/visibility", "public", entry.get("visibility"))
        current_entry = catalog_entry(declarations, "fields", "net:field:base-spire:Run.CurrentNode") or {}
        checker.equal("catalog/current-node-target-kind", [MAP_NODE_KIND], current_entry.get("target_kind_ids"))
        checker.equal("catalog/current-node-optional", False, current_entry.get("required"))

        checker.check("catalog/no-map-draws",
                      catalog_entry(declarations, "fields", "net:field:base-spire:Run.MapDraws") is None)
        # 地图相关字段不得出现非公开可见性（隐藏状态/RNG/debug 不进公开目录）。
        hidden = sorted(entry.get("id") for table in declarations
                        for entry in declarations[table]
                        if entry.get("value_type") is not None
                        and entry.get("visibility") != "public"
                        and set((entry.get("applies_to") or {}).get("entity_kinds") or []).intersection(
                            {MAP_NODE_KIND, RUN_KIND}))
        checker.equal("catalog/map-fields-all-public", [], hidden)
        # 公开目录里不出现内部过程名（地图抽数必须是规则包内部事实，不得成为外部操作）。
        leaked = sorted(entry_id for table in declarations for entry in declarations[table]
                        for entry_id in [entry.get("id", "")]
                        if any(token in entry_id for token in ("SampleInt", "WordBudget", "ActMapStream")))
        checker.equal("catalog/no-internal-map-procedure", [], leaked)
    finally:
        fixture.shutdown()


def _run_trajectory(checker: Checker, schema: dict) -> None:
    """两 seed：公开候选恰为公开后继，合法选 Monster 进既有战斗，战斗出牌如实记录。"""
    fixture = Fixture(checker, schema, "trajectory")
    try:
        describe = fixture.describe(request_id="trajectory-describe")
        identity = Fixture.identity_of(describe)
        started_seeds: list[str] = []
        for seed in EVIDENCE_SEEDS:
            start = fixture.start(identity, seed, request_id=f"trajectory-start-{seed}")
            if not checker.check(f"trajectory/{seed}/start-ok", start.get("ok") is True,
                                 str(start.get("error"))):
                continue
            started_seeds.append(seed)
            checker.equal(f"trajectory/{seed}/seed-echo", seed, start["result"].get("seed"))
            snapshot = snapshot_of(start)
            facts = map_facts(snapshot)
            nodes = map_nodes(snapshot)
            sequence = map_sequence(snapshot) or {}
            elements = sequence.get("elements") or []
            checker.equal(f"trajectory/{seed}/run-kind-present", True, facts["run"] is not None)
            checker.equal(f"trajectory/{seed}/initial-phase", "0", facts["phase"])
            checker.equal(f"trajectory/{seed}/initial-floor", "0", facts["floor"])
            checker.equal(f"trajectory/{seed}/node-count-field", len(nodes), int(facts["node_count"] or -1))
            checker.equal(f"trajectory/{seed}/MapNodes-elements-are-nodes", sorted(n["instance_id"] for n in nodes),
                          sorted(elements))
            checker.equal(f"trajectory/{seed}/MapNodes-owner-is-run",
                          facts["run"]["instance_id"] if facts["run"] else None, sequence.get("owner_ref"))
            observed = fixture.observe(start["result"]["session_id"], request_id=f"trajectory-observe-{seed}")
            checker.equal(f"trajectory/{seed}/repeated-observe-snapshot", snapshot, snapshot_of(observed))
            checker.check(f"trajectory/{seed}/current-node-known",
                          isinstance(facts["current"], str) and facts["current"].startswith("e"), str(facts["current"]))

            successors = map_children(snapshot, facts["current"])
            checker.check(f"trajectory/{seed}/successors-nonempty", len(successors) > 0, str(successors))
            checker.equal(f"trajectory/{seed}/candidates-equal-successors", successors, successor_values(snapshot))
            checker.equal(f"trajectory/{seed}/candidate-count", len(successors), len(candidate_ids(snapshot, CHOOSE_MAP_POINT)))
            node_types = {ref: map_node_type(snapshot, ref) for ref in successors}
            checker.check(f"trajectory/{seed}/successor-types-public",
                          all(value is not None for value in node_types.values()), str(node_types))

            monsters = [ref for ref in successors if node_types.get(ref) == "Monster"]
            if not checker.check(f"trajectory/{seed}/monster-successor", len(monsters) > 0, str(node_types)):
                continue
            target = monsters[0]
            row = map_node_row(snapshot, target)
            candidate = candidate_for(snapshot, CHOOSE_MAP_POINT, CHOOSE_NODE_FIELD, target)
            if not checker.check(f"trajectory/{seed}/monster-candidate", candidate is not None, str(target)):
                continue

            observation = observation_of(start)
            step = fixture.step(start["result"]["session_id"], observation, candidate,
                                request_id=f"trajectory-choose-{seed}")
            if not checker.check(f"trajectory/{seed}/choose-ok", step.get("ok") is True, str(step.get("error"))):
                continue
            battle = snapshot_of(step)
            battle_facts = map_facts(battle)
            checker.equal(f"trajectory/{seed}/phase-after-choose", "1", battle_facts["phase"])
            checker.equal(f"trajectory/{seed}/floor-after-choose", row + 1 if row is not None else None,
                          int(battle_facts["floor"]) if battle_facts["floor"] is not None else None)
            checker.equal(f"trajectory/{seed}/current-after-choose", target, battle_facts["current"])
            checker.equal(f"trajectory/{seed}/node-visited",
                          True, known_field(entity_by_ref(battle, target), "net:field:base-spire:MapNode.Visited"))
            checker.equal(f"trajectory/{seed}/map-candidate-gone-in-battle", [],
                          candidate_ids(battle, CHOOSE_MAP_POINT))
            checker.check(f"trajectory/{seed}/encounter-published",
                          isinstance(battle_facts["encounter"], str) and battle_facts["encounter"] != "",
                          str(battle_facts["encounter"]))
            checker.check(f"trajectory/{seed}/battle-actions-available",
                          len(candidate_ids(battle, PLAY_CARD)) > 0, str(candidate_ids(battle, PLAY_CARD)))

            plays = candidate_ids(battle, PLAY_CARD)
            if not plays:
                continue
            battle_step = fixture.step(start["result"]["session_id"], observation_of(step), plays[0],
                                       request_id=f"trajectory-play-{seed}")
            if battle_step.get("ok") is True:
                after = snapshot_of(battle_step)
                checker.equal(f"trajectory/{seed}/battle-step-keeps-map-phase", "1",
                              map_facts(after)["phase"])
                checker.check(f"trajectory/{seed}/battle-step-advances-observation",
                              observation_of(battle_step) > observation_of(step),
                              f"{observation_of(step)} → {observation_of(battle_step)}")
            else:
                # 引擎具名故障：如实记录为 backend_fault/unknown/stop，交 A 修 base，不伪装可继续。
                checker.equal(f"trajectory/{seed}/battle-step-error-code", "backend_fault",
                              error_code(battle_step))
                checker.equal(f"trajectory/{seed}/battle-step-execution-state", "unknown",
                              execution_state(battle_step))
                checker.equal(f"trajectory/{seed}/battle-step-retry-mode", "stop", retry_mode(battle_step))
                checker.faults.append(f"{seed}: {(battle_step.get('error') or {}).get('message')}")
        # 同一份静态身份同时覆盖两个 seed：说明每局 seed 不在身份里（否则第二次 start 会被拒）。
        checker.equal("trajectory/both-seeds-same-identity", sorted(EVIDENCE_SEEDS), sorted(started_seeds))
        later = fixture.describe(request_id="trajectory-describe-after")
        checker.equal("trajectory/identity-stable-after-sessions", identity, Fixture.identity_of(later))
    finally:
        fixture.shutdown()


def _run_rejections(checker: Checker, schema: dict) -> None:
    """错身份/非法候选/已消费候选/过期观察在执行前拒绝；重发幂等；被拒时状态与抽数不变。"""
    fixture = Fixture(checker, schema, "rejections")
    try:
        describe = fixture.describe(request_id="rejections-describe")
        identity = Fixture.identity_of(describe)

        wrong = dict(identity)
        wrong["catalog_id"] = "net:catalog:0000000000000000"
        bad = fixture.call("start", {"expected_identity": wrong, "scenario_profile": DEV_MAP_PROFILE,
                                     "seed": EVIDENCE_SEEDS[0]}, request_id="reject-wrong-identity")
        checker.equal("rejections/wrong-identity-code", "version_mismatch", error_code(bad))
        checker.check("rejections/wrong-identity-not-started", bad.get("result") is None, str(bad.get("result")))

        start = fixture.start(identity, EVIDENCE_SEEDS[0], request_id="rejections-start")
        if not checker.check("rejections/start-ok", start.get("ok") is True, str(start.get("error"))):
            return
        session = start["result"]["session_id"]
        before = map_facts(snapshot_of(start))
        first_observation = observation_of(start)

        # 非法候选：拿实体引用当 candidate_id（候选只能来自当前稳定边界的公开候选）。
        illegal = fixture.step(session, first_observation, before["current"], request_id="reject-illegal")
        checker.equal("rejections/illegal-code", "illegal_candidate", error_code(illegal))
        checker.equal("rejections/illegal-execution-state", "not_started", execution_state(illegal))
        checker.equal("rejections/illegal-retry-mode", "correct_input", retry_mode(illegal))
        probe = fixture.observe(session, request_id="rejections-observe-after-illegal")
        after = map_facts(snapshot_of(probe))
        checker.equal("rejections/illegal-keeps-phase", before["phase"], after["phase"])
        checker.equal("rejections/illegal-keeps-current-node", before["current"], after["current"])
        checker.equal("rejections/illegal-keeps-node-count", before["node_count"], after["node_count"])
        observation = observation_of(probe)

        successors = map_children(snapshot_of(probe), after["current"])
        candidate = candidate_for(snapshot_of(probe), CHOOSE_MAP_POINT, CHOOSE_NODE_FIELD, successors[0])
        if not checker.check("rejections/choose-candidate", candidate is not None, str(successors)):
            return
        chosen = fixture.step(session, observation, candidate, request_id="rejections-replay-choose")
        checker.check("rejections/choose-ok", chosen.get("ok") is True, str(chosen.get("error")))
        replay = fixture.step(session, observation, candidate, request_id="rejections-replay-choose")
        checker.check("rejections/replay-ok", replay.get("ok") is True, str(replay.get("error")))
        checker.equal("rejections/replay-same-observation", observation_of(chosen), observation_of(replay))
        checker.equal("rejections/replay-same-canonical-bytes", canonical_text(chosen), canonical_text(replay))

        # 已消费候选（新 request_id、当前边界）→ 拒绝，不产生第二次推进。
        consumed = fixture.step(session, observation_of(chosen), candidate, request_id="rejections-consumed")
        checker.equal("rejections/consumed-code", "illegal_candidate", error_code(consumed))
        checker.equal("rejections/consumed-observation-unchanged", observation_of(chosen),
                      observation_of(fixture.observe(session, request_id="rejections-observe-after-consumed")))

        battle = snapshot_of(fixture.observe(session, request_id="rejections-battle"))
        battle_observation = observation_of(fixture.observe(session, request_id="rejections-battle-again"))
        plays = candidate_ids(battle, PLAY_CARD)
        if checker.check("rejections/battle-actions", len(plays) > 0, str(plays)):
            stale = fixture.step(session, first_observation, plays[0], request_id="reject-stale-observation")
            checker.equal("rejections/stale-code", "stale_observation", error_code(stale))
            checker.equal("rejections/stale-retry-mode", "query_request", retry_mode(stale))
            checker.equal("rejections/stale-observation-unchanged", battle_observation,
                          observation_of(fixture.observe(session, request_id="rejections-observe-after-stale")))
    finally:
        fixture.shutdown()


def _run_schema(checker: Checker, schema: dict) -> None:
    """真实帧直接过 0.2 machine schema 与规范字节；错误帧必须带可判定字段。"""
    fixture = Fixture(checker, schema, "schema")
    try:
        describe = fixture.describe(request_id="schema-describe")
        identity = Fixture.identity_of(describe)
        catalog = fixture.get_catalog(identity["catalog_id"], request_id="schema-catalog")
        start = fixture.start(identity, EVIDENCE_SEEDS[0], request_id="schema-start")
        checker.check("schema/start-result-keys", sorted(start.get("result") or {}) == ["seed", "session_id", "snapshot"],
                      str(sorted(start.get("result") or {})))
        snapshot = snapshot_of(start)
        checker.check("schema/snapshot-keys",
                      {"entities", "legal_actions", "observation_id", "relations", "sequences", "status",
                       "session_id", "catalog_id", "ruleset_id", "protocol_version", "backend_profile_id",
                       "piles", "groups", "terminal_result"} <= set(snapshot), str(sorted(snapshot)))
        checker.equal("schema/snapshot-protocol", "0.2", snapshot.get("protocol_version"))
        checker.equal("schema/snapshot-identity-matches-describe", identity["catalog_id"], snapshot.get("catalog_id"))
        checker.equal("schema/catalog-identity-matches-describe", identity["catalog_id"],
                      (catalog.get("result") or {}).get("catalog_id"))
        entity = (snapshot.get("entities") or [{}])[0]
        checker.check("schema/entity-keys", {"instance_id", "kind_id", "fields"} <= set(entity), str(sorted(entity)))
        checker.check("schema/entity-field-keys",
                      all({"field_id", "state", "value"} <= set(field) for field in entity.get("fields", [])),
                      str(entity.get("fields")))
        for record in checker.evidence:
            if record["fixture"] != "schema":
                continue
            response = record["response"]
            checker.check(f"schema/{record['operation']}/envelope-keys",
                          {"ok", "request_id", "result"} <= set(response)
                          or {"ok", "request_id", "error"} <= set(response), str(sorted(response)))
            checker.check(f"schema/{record['operation']}/canonical-bytes",
                          not record["canonical"].startswith("<canonical-error"), record["canonical"][:160])

        error = fixture.step(start["result"]["session_id"], observation_of(start), "not-a-candidate",
                             request_id="schema-error-frame")
        checker.equal("schema/error-ok-false", False, error.get("ok"))
        checker.check("schema/error-fields",
                      {"code", "execution_state", "retry_mode", "message"} <= set(error.get("error") or {}),
                      str(error.get("error")))
    finally:
        fixture.shutdown()


RUNNERS = {
    "host": _run_host,
    "catalog": _run_catalog,
    "trajectory": _run_trajectory,
    "rejections": _run_rejections,
    "schema": _run_schema,
}


def _write_log(path: pathlib.Path, checker: Checker) -> None:
    """NDJSON 证据：头部 + 每个宿主的就绪横幅（stderr）+ 每一帧请求/响应与规范字节。"""
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        header = {
            "kind": "evidence-header",
            "tool": "net-the-spire/client/test_map_host.py",
            "scenario": "formal-dev",
            "scopes": [DEFAULT_SCOPE_NAME, MAP_SCOPE_NAME],
            "scenario_profiles": [DEV_PROFILE, DEV_MAP_PROFILE],
            "seeds": list(EVIDENCE_SEEDS),
            "schema": str(SCHEMA_PATH.relative_to(REPO_ROOT)),
            "passed": checker.passed,
            "failed": checker.failed,
            "engine_faults": checker.faults,
        }
        handle.write(json.dumps(header, ensure_ascii=False, sort_keys=True) + "\n")
        for name, banner in sorted(checker.banners.items()):
            handle.write(json.dumps({"kind": "host-banner", "fixture": name, "stderr": banner},
                                    ensure_ascii=False, sort_keys=True) + "\n")
        for record in checker.evidence:
            handle.write(json.dumps(record, ensure_ascii=False, sort_keys=True) + "\n")


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="ENV-T03 C 线地图公开 Host 的真实 stdio 证据")
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

    for fault in checker.faults:
        print(f"· 引擎具名故障（如实记录，交 A 修 base，不伪装可继续）：{fault}")

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
