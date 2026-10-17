"""通过公开 NDJSON Host 重放第一幕路线；按**公开动作语义**选择实时候选，不读写 Engine 状态或随机数。

用法::

    python -X utf8 tools/base-spire-dev/act1_smoke.py --host-dll PATH --fixture FILE --output DIR
    python -X utf8 tools/base-spire-dev/act1_smoke.py --record-from FRAMES.ndjson --fixture-out FILE --scope dev-map

为什么不再按字面 ``candidate_id`` 重放：候选句柄自 2026-10-11 起按**会话**派生（同 seed、同观察版本、
同动作语义的两局不再共享句柄），把上一次会话的字面 ID 当输入必然失效。本工具改为记录/重放
``{action_type, fields}`` 规范语义（``semantic_actions.py``），每步在**实时** ``legal_actions`` 里找
唯一匹配的候选，再提交它**当前的** ``candidate_id`` 与 ``observation_id``。字面候选 ID 只作原始帧证据。

严格性：匹配缺失、多义或引用身份不符都带首差停止，不取第一个替代，也不推进引擎；每步之后核对记录的
公开游戏事实，任何差异同样是首差。夹具声明 ``mode=full-act`` 时保留原第一幕完成条件（开场 80HP/99 金币/
10 张起手、ActCompleted/MapPhase 6/Boss 与宝物、ContinueToNextAct、药水动作、不得发布整局胜利）；只声明
``mode=prefix`` 的夹具只证明重放器本身，汇总里不写 ActCompleted，已知 unsupported 故障必须原样复现。

``--host-dll`` 必须显式给出（或用既有 ``NET_THE_SPIRE_HOST_DLL``）：本工具不自动构建，也不回退到仓库内
可能过期的共享 Host DLL。旧 ``candidate_ids`` 唯一字段的夹具会被明确拒绝并给出迁移入口。

本检查是开发范围的公开复放检查，不是独立原版对拍，也不访问隐藏随机。
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "net-the-spire/client"))
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from net_client import NetClient  # noqa: E402

import semantic_actions as semantics  # noqa: E402

CLAIM_PREFIX = "public semantic replay of a recorded prefix only; ActCompleted not claimed"
CLAIM_FULL = "Act 1 completion conditions on the recorded public route"

#: 原第一幕冒烟的开场契约（ENV-T05 记录的正常 dev-map 初始资源）。
FULL_ACT_INITIAL = {"hp": 80, "gold": 99, "long_term_deck": 10}


class UsageError(RuntimeError):
    """用法、schema、Host 或夹具形态错误（退出码 2）。"""


def _sha256(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _rel(path: pathlib.Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def _scalar(fact) -> object:
    """把公开事实里的规范值还原成可读标量（`value:8` → 8）。"""
    if isinstance(fact, str) and fact.startswith("value:"):
        try:
            return json.loads(fact[len("value:"):])
        except ValueError:
            return fact
    return fact


def resolve_host(explicit: str | None) -> pathlib.Path:
    """固定 Host 必须显式给出（或由既有环境变量给出），绝不自动构建或回退到仓库内共享 DLL。"""
    candidate = explicit or os.environ.get("NET_THE_SPIRE_HOST_DLL")
    if not candidate:
        raise UsageError(
            "必须用 --host-dll 指定固定 Host（或设置 NET_THE_SPIRE_HOST_DLL）：本工具不会自动构建，"
            "也不会回退到仓库内可能过期的共享 Host DLL")
    path = pathlib.Path(candidate)
    if not path.is_file():
        raise UsageError(f"--host-dll 指向的文件不存在：{path}")
    os.environ["NET_THE_SPIRE_HOST_DLL"] = str(path)
    return path


def load_fixture(path: pathlib.Path) -> dict:
    if not path.is_file():
        raise UsageError(f"夹具文件不存在：{path}")
    fixture = json.loads(path.read_text(encoding="utf-8-sig"))
    if fixture.get("schema") != semantics.SCHEMA:
        legacy = [name for name in semantics.LEGACY_FIXTURE_FIELDS if name in fixture]
        if legacy:
            raise UsageError(
                f"夹具 {path} 只登记了字面 {'/'.join(legacy)}：候选句柄按会话派生后，这些 ID 不能作为重放输入，"
                "本工具不会把它们退化成任意候选。迁移入口（先录一份新的公开轨迹，再提取语义重录）：\n"
                "  python -X utf8 net-the-spire/client/run_game.py --host-dll <HOST> --scope dev-map "
                "--seed <SEED> --output <DIR> --max-actions 400 --use-potions\n"
                "  python -X utf8 tools/base-spire-dev/act1_smoke.py --record-from <DIR>/frames.ndjson "
                f"--fixture-out {path.name} --scope dev-map")
        raise UsageError(f"夹具 {path} 缺少 schema={semantics.SCHEMA}（既不是语义夹具，也不是可识别的旧夹具）")
    if not fixture.get("actions"):
        raise UsageError(f"夹具 {path} 的 actions 为空，没有可重放的动作")
    if fixture.get("mode") not in ("prefix", "full-act"):
        raise UsageError(f"夹具 {path} 的 mode 必须是 prefix 或 full-act（当前 {fixture.get('mode')!r}）")
    return fixture


def record(trajectory: pathlib.Path, fixture_out: pathlib.Path, scope: str, host: str | None) -> dict:
    """从公开轨迹提取语义动作并写出语义夹具（不自己寻找游戏路线）。"""
    extracted = semantics.extract(trajectory, scope=scope)
    last_facts = extracted["actions"][-1]["facts"]
    fault = extracted["known_fault"]
    act_completed = last_facts.get("run", {}).get("ActCompleted") == "value:true"
    mode = "full-act" if act_completed and not fault else "prefix"
    host_info = None
    resolved_host = host or os.environ.get("NET_THE_SPIRE_HOST_DLL")
    if resolved_host:
        path = pathlib.Path(resolved_host)
        host_info = {"dll": _rel(path), "sha256": _sha256(path)}
    potions = sorted({semantics.short(a["action_type"]) for a in extracted["actions"]
                      if semantics.short(a["action_type"]) in ("TakeRewardPotion", "UsePotion", "DiscardPotion")})
    fixture = {
        "schema": semantics.SCHEMA,
        "mode": mode,
        "seed": extracted["seed"],
        "scope": extracted["scope"],
        "scenario_profile": extracted["scenario_profile"],
        "identity": extracted["identity"],
        "host": host_info,
        "source": {"trajectory": _rel(trajectory), "sha256": extracted["trajectory_sha256"],
                   "session_id": extracted["session_id"]},
        "initial_facts": extracted["initial_facts"],
        "actions": extracted["actions"],
        "known_fault": fault,
        "notes": [
            "candidate_id 只作该次记录的原始帧证据；重放只用 {action_type, fields} 语义在实时候选里匹配。",
            "mode=prefix 只覆盖当前构建公开可达前缀；完整第一幕完成条件仍需能走到 ActCompleted 的夹具。",
        ],
    }
    if mode == "full-act":
        fixture["completion"] = {"expected_hp": _scalar(last_facts.get("run", {}).get("Hp")),
                                 "potion_actions_required": potions}
        fixture["notes"].append("mode=full-act：重放结束必须满足原第一幕完成条件（含开场 80HP/99 金币/10 张起手）。")
    fixture_out.parent.mkdir(parents=True, exist_ok=True)
    fixture_out.write_text(json.dumps(fixture, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"fixture": _rel(fixture_out), "mode": mode, "actions": len(extracted["actions"]),
            "known_fault": bool(fault), "seed": extracted["seed"], "scope": extracted["scope"],
            "trajectory_sha256": extracted["trajectory_sha256"]}


# -- 公开访问小工具（只读快照） ---------------------------------------------------------------------------


def _field(entity: dict | None, name: str):
    fields = semantics.known_fields(entity)
    if name in fields:
        return fields[name]
    return next((value for key, value in fields.items() if key.endswith("." + name)), None)


def _live_values(candidate: dict) -> dict:
    return {f["field_id"]: f.get("value") for f in candidate.get("fields") or []}


def _check_no_hidden_leak(snapshot: dict, number: int) -> None:
    for entity in snapshot.get("entities") or []:
        assert "SharedRelicBagEntry" not in (entity.get("kind_id") or ""), \
            f"hidden bag leaked at step {number}"
        for field in entity.get("fields") or []:
            assert not (field.get("field_id") or "").endswith(".MapDraws"), \
                f"map diagnostic payload leaked at step {number}"


def _potion_checks(action_name: str, potion_ref, snapshot: dict, seen: set) -> None:
    """原药水不变量：领取保留真实公开实例、使用/丢弃后离开栏位（只读公开快照）。"""
    if not potion_ref:
        return
    seen.add(action_name)
    belt = semantics.sequence(snapshot, "PotionBelt")
    if action_name == "TakeRewardPotion":
        assert potion_ref in belt, "receive must retain the real offered instance"
        assert _field(semantics.entity_of(snapshot, potion_ref), "SlotIndex") in (0, 1, 2), \
            "领取的药水必须在公开栏位上"
    elif action_name in ("DiscardPotion", "UsePotion") and not snapshot.get("pending_decision"):
        assert potion_ref not in belt, "completed use/discard must consume the selected bottle"


def _full_act_step_checks(action_name: str, snapshot: dict, state: dict) -> None:
    """原第一幕完成条件的逐步检查（Boss/宝物/高楼层奖励），只在 mode=full-act 时执行。"""
    run_entity = semantics.find_entity(snapshot, "Run")
    if action_name == "ChooseMapPoint":
        if _field(run_entity, "MapFloor") == 17:
            live_monsters = semantics.sequence(snapshot, "Monsters")
            assert len(live_monsters) == 1, "Boss 房间必须只发布一个 Boss 实例"
            boss = semantics.entity_of(snapshot, live_monsters[0])
            assert boss is not None and boss.get("content_id") == "base-spire:vantom"
            assert _field(boss, "Hp") == 173
            assert any(e.get("content_id") == "base-spire:slippery-power" and _field(e, "Stacks") == 8
                       for e in snapshot.get("entities") or [])
            state["seen_boss"] = True
        if _field(run_entity, "MapPhase") == 5:
            assert _field(run_entity, "TreasureRelicId") not in (None, "")
            state["seen_treasure"] = True
    if _field(run_entity, "MapFloor") == 17 and _field(run_entity, "MapPhase") == 3:
        assert _field(run_entity, "RewardGold") == 100
        assert all(_field(run_entity, f"RewardCardRarity{i}") == 4 for i in range(3))


def _write_summary(output: pathlib.Path, summary: dict) -> None:
    (output / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")


def _stop(output: pathlib.Path, summary: dict, code: int, stop: str, detail) -> tuple[int, dict]:
    summary.update({"stop": stop, "detail": detail, "exit_code": code,
                    "act_completed": False, "run_complete": False})
    _write_summary(output, summary)
    evidence = output / "first-difference.json"
    if not evidence.is_file():
        evidence.write_text(json.dumps({"stop": stop, "detail": detail,
                                        "submitted_steps": summary.get("submitted_steps")},
                                       ensure_ascii=False, indent=2), encoding="utf-8")
    return code, summary


def replay(fixture: dict, output: pathlib.Path, fixture_path: pathlib.Path, host: pathlib.Path) -> tuple[int, dict]:
    client = NetClient.host_from_solution(
        extra_args=["--scenario", "formal-dev", "--scope", fixture["scope"]])
    output.mkdir(parents=True, exist_ok=True)
    summary: dict = {"tool": "tools/base-spire-dev/act1_smoke.py", "schema": semantics.SCHEMA,
                     "fixture": _rel(fixture_path), "fixture_mode": fixture["mode"],
                     "seed": fixture["seed"], "scope": fixture["scope"],
                     "host_dll": str(host), "host_sha256": _sha256(host),
                     "host_pinned": (fixture.get("host") or {}).get("sha256") == _sha256(host),
                     "recorded_candidate_ids": [a["recorded"]["candidate_id"] for a in fixture["actions"]],
                     "live_candidate_ids": [], "candidate_id_reused": [],
                     "claim": CLAIM_PREFIX if fixture["mode"] == "prefix" else CLAIM_FULL,
                     "exit_code": 1}
    try:
        try:
            return _replay_body(client, fixture, output, summary)
        except AssertionError as failure:
            return _stop(output, summary, 3, "invariant_failed", str(failure))
    finally:
        client.shutdown()


def _replay_body(client: NetClient, fixture: dict, output: pathlib.Path, summary: dict) -> tuple[int, dict]:
    describe = client.describe(timeout=60)
    if not describe.get("ok"):
        raise UsageError(f"describe 失败：{describe}")
    identity = describe["result"]["identity"]
    summary["identity"] = identity
    mismatched = {k: {"recorded": v, "live": identity.get(k)}
                  for k, v in (fixture.get("identity") or {}).items() if identity.get(k) != v}
    if mismatched:
        return _stop(output, summary, 1, "identity_mismatch", mismatched)

    profiles = [p["id"] for p in describe["result"]["capabilities"]["scenario_profiles"]]
    scenario = fixture.get("scenario_profile")
    if scenario not in profiles:
        scenario = profiles[0]
    started = client.start(identity, scenario, seed=fixture["seed"], timeout=60)
    if not started.get("ok"):
        return _stop(output, summary, 1, "start_rejected", started)
    session = started["result"]["session_id"]
    snapshot = started["result"]["snapshot"]
    summary.update({"scenario_profile": scenario, "session_id": session,
                    "observation_ids": [snapshot.get("observation_id")]})

    initial_live = semantics.public_facts(snapshot)
    differences = semantics.diff_facts(fixture.get("initial_facts") or {}, initial_live)
    if differences:
        return _stop(output, summary, 1, "initial_facts_mismatch", differences)

    if fixture["mode"] == "full-act":
        run_entity = semantics.find_entity(snapshot, "Run")
        initial = {"hp": _field(run_entity, "Hp"), "gold": _field(run_entity, "Gold"),
                   "long_term_deck": len(semantics.sequence(snapshot, "LongTermDeck"))}
        summary["initial_contract"] = initial
        assert initial == FULL_ACT_INITIAL, \
            f"full-act 开场契约不满足：{initial} != {FULL_ACT_INITIAL}"

    seen_potion_actions: set = set()
    route: list = []
    state = {"seen_boss": False, "seen_treasure": False}
    with (output / "public-trajectory.jsonl").open("w", encoding="utf-8") as trajectory, \
            (output / "semantic-steps.jsonl").open("w", encoding="utf-8") as steps:
        trajectory.write(json.dumps({"describe": describe, "start": started}, ensure_ascii=False) + "\n")
        for action in fixture["actions"]:
            number = action["seq"]
            _check_no_hidden_leak(snapshot, number)
            wanted = {"action_type": action["action_type"], "fields": action["fields"]}
            outcome = semantics.match_candidates(snapshot, wanted)
            step_evidence = {
                "seq": number, "action_type": action["action_type"],
                "recorded_candidate_id": action["recorded"]["candidate_id"],
                "match_status": outcome["status"], "detail": outcome["detail"],
                "offer": outcome["offer"], "refs": outcome["refs"],
            }
            if outcome["status"] != semantics.MATCHED:
                steps.write(json.dumps(step_evidence, ensure_ascii=False) + "\n")
                summary["submitted_steps"] = number
                _first_difference(output, number, action, step_evidence)
                return _stop(output, summary, 1, outcome["status"], outcome["detail"])

            candidate = outcome["candidate"]
            live_values = _live_values(candidate)
            action_name = semantics.short(action["action_type"])
            instruction = {"action": action["action_type"],
                           "fields": [{"field_id": f["field_id"], "value": live_values.get(f["field_id"])}
                                      for f in action["fields"]]}
            potion_ref = None
            if action_name == "TakeRewardPotion":
                offered_potions = semantics.sequence(snapshot, "RewardPotions")
                assert len(offered_potions) == 1, "奖励药水必须恰好发布一个公开实例"
                potion_ref = offered_potions[0]
            elif action_name in ("UsePotion", "DiscardPotion"):
                potion_ref = next(iter(live_values.values()), None)
                if action_name == "UsePotion":
                    assert _field(semantics.entity_of(snapshot, potion_ref), "UseSupported") is True, \
                        "UsePotion 必须指向公开支持使用的药水实例"

            response = client.step(session, snapshot["observation_id"], candidate["candidate_id"], timeout=60)
            if not response.get("ok"):
                step_evidence["response_error"] = response.get("error")
                steps.write(json.dumps(step_evidence, ensure_ascii=False) + "\n")
                summary["submitted_steps"] = number
                _first_difference(output, number, action, step_evidence)
                return _stop(output, summary, 1, "host_rejected", response.get("error"))

            snapshot = response["result"].get("snapshot", response["result"])
            trajectory.write(json.dumps({"instruction": instruction, "response": response},
                                        ensure_ascii=False) + "\n")
            step_evidence.update({"submitted_candidate_id": candidate["candidate_id"],
                                  "candidate_id_reused": candidate["candidate_id"]
                                  == action["recorded"]["candidate_id"],
                                  "observation_id": snapshot.get("observation_id")})
            summary["live_candidate_ids"].append(candidate["candidate_id"])
            summary["candidate_id_reused"].append(step_evidence["candidate_id_reused"])
            summary["observation_ids"].append(snapshot.get("observation_id"))

            live_facts = semantics.public_facts(snapshot)
            fact_differences = semantics.diff_facts(action.get("facts") or {}, live_facts)
            step_evidence["fact_differences"] = fact_differences
            steps.write(json.dumps(step_evidence, ensure_ascii=False) + "\n")
            if fact_differences:
                summary["submitted_steps"] = number + 1
                _first_difference(output, number, action, step_evidence)
                return _stop(output, summary, 1, "public_facts_mismatch", fact_differences)

            _potion_checks(action_name, potion_ref, snapshot, seen_potion_actions)
            if action_name == "ChooseMapPoint":
                node = semantics.entity_of(snapshot, next(iter(live_values.values()), None))
                if node is not None:
                    route.append({"row": _field(node, "Row"), "col": _field(node, "Col")})
            if fixture["mode"] == "full-act":
                _full_act_step_checks(action_name, snapshot, state)

        run_entity = semantics.find_entity(snapshot, "Run")
        completed = _field(run_entity, "ActCompleted") is True
        summary.update({"submitted_steps": len(fixture["actions"]), "final_facts": semantics.public_facts(snapshot),
                        "act_completed": bool(completed), "run_complete": False,
                        "terminal_result": snapshot.get("terminal_result"), "route": route,
                        "potion_actions": sorted(seen_potion_actions)})

        if fixture["mode"] == "full-act":
            completion = fixture.get("completion") or {}
            blocking = {"act_completed": bool(completed), "map_phase": _field(run_entity, "MapPhase"),
                        "map_floor": _field(run_entity, "MapFloor"), "run_hp": _field(run_entity, "Hp"),
                        "seen_boss": state["seen_boss"], "seen_treasure": state["seen_treasure"],
                        "potion_actions": sorted(seen_potion_actions)}
            if not (state["seen_boss"] and state["seen_treasure"] and completed
                    and _field(run_entity, "MapPhase") == 6):
                return _stop(output, summary, 3, "act_not_completed",
                             {"detail": "重放没有走到第一幕完成条件；前缀/中途停止都不得算整幕通过", **blocking})
            if completion.get("expected_hp") is not None:
                assert _field(run_entity, "Hp") == completion["expected_hp"], "结算存活 HP 与记录不一致"
            assert snapshot.get("terminal_result") is None, "第一幕完成不得发布整局胜利"
            offered = snapshot.get("legal_actions") or []
            assert len(offered) == 1 and offered[0]["action_type"].endswith(":ContinueToNextAct"), \
                "第一幕完成必须恰好提供 ContinueToNextAct"
            assert set(completion.get("potion_actions_required") or []) <= seen_potion_actions
            summary.update({"claim": CLAIM_FULL, "exit_code": 0,
                            "comparison": "public Host replay; independent vanilla side not executed"})
            _write_summary(output, summary)
            return 0, summary

        if completed:
            return _stop(output, summary, 1, "prefix_fixture_reached_act_completed",
                         "前缀夹具走到了 ActCompleted：应按完整第一幕条件重录为 full-act 夹具")

        known_fault = fixture.get("known_fault")
        if known_fault:
            before = snapshot.get("observation_id")
            pinned = (known_fault.get("recorded") or {}).get("expected_observation_id")
            assert pinned is None or pinned == before, \
                "故障步骤的记录基准观察与当前不一致，前缀夹具已过期"
            outcome = semantics.match_candidates(
                snapshot, {"action_type": known_fault["action_type"], "fields": known_fault["fields"]})
            if outcome["status"] != semantics.MATCHED:
                return _stop(output, summary, 1, f"known_fault_{outcome['status']}", outcome["detail"])
            fault_candidate = outcome["candidate"]
            response = client.step(session, before, fault_candidate["candidate_id"], timeout=60)
            with (output / "public-trajectory.jsonl").open("a", encoding="utf-8") as trajectory:
                trajectory.write(json.dumps({
                    "instruction": {"action": known_fault["action_type"],
                                    "fields": [{"field_id": f["field_id"], "value": f["raw"]}
                                               for f in known_fault["fields"]]},
                    "response": response}, ensure_ascii=False) + "\n")
            recorded_error = known_fault.get("error") or {}
            live_error = response.get("error") or {}
            reproduced = (not response.get("ok") and live_error.get("code") == recorded_error.get("code")
                          and live_error.get("observation_id") == before)
            summary["known_fault"] = {"action_type": known_fault["action_type"],
                                      "recorded_code": recorded_error.get("code"),
                                      "recorded_message": recorded_error.get("message"),
                                      "live_error": live_error, "reproduced": bool(reproduced),
                                      "observation_id_unchanged": live_error.get("observation_id") == before}
            if not reproduced:
                return _stop(output, summary, 1, "known_fault_not_reproduced", live_error)
        summary.update({"claim": CLAIM_PREFIX, "exit_code": 0,
                        "not_claimed": ["ActCompleted", "whole-run victory", "full Act 1 smoke",
                                        "independent vanilla comparison"]})
        _write_summary(output, summary)
        return 0, summary


def _first_difference(output: pathlib.Path, step_number: int, action: dict, detail: dict) -> None:
    (output / "first-difference.json").write_text(
        json.dumps({"step": step_number, "recorded_action": action, "detail": detail},
                   ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"stop": "first_difference", "step": step_number, "detail": detail},
                     ensure_ascii=False), file=sys.stderr)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host-dll", help="固定的 Net.TheSpire.Host.dll（也可用 NET_THE_SPIRE_HOST_DLL）")
    parser.add_argument("--fixture", type=pathlib.Path,
                        default=pathlib.Path(__file__).parent / "fixtures/act1-semantic-dev-map-prefix.json")
    parser.add_argument("--output", type=pathlib.Path, default=ROOT / ".audit/act1-public-smoke")
    parser.add_argument("--record-from", type=pathlib.Path,
                        help="从公开轨迹（run_game frames.ndjson 或本工具 public-trajectory.jsonl）提取语义动作")
    parser.add_argument("--fixture-out", type=pathlib.Path, help="记录模式写出的语义夹具路径")
    parser.add_argument("--scope", default="dev-map", help="记录模式使用的 Host scope 名（默认 dev-map）")
    args = parser.parse_args(argv)

    try:
        if args.record_from:
            if not args.fixture_out:
                raise UsageError("--record-from 必须同时给出 --fixture-out")
            report = record(args.record_from, args.fixture_out, args.scope, args.host_dll)
            print(json.dumps(report, ensure_ascii=False))
            return 0
        fixture_path = args.fixture
        fixture = load_fixture(fixture_path)
        host = resolve_host(args.host_dll)
        code, summary = replay(fixture, args.output, fixture_path, host)
        print(json.dumps(summary, ensure_ascii=False))
        return code
    except UsageError as failure:
        print(f"用法/夹具错误：{failure}", file=sys.stderr)
        return 2
    except semantics.SemanticsError as failure:
        print(f"轨迹/语义错误：{failure}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
