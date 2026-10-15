#!/usr/bin/env python3
"""W-01 检查：作者内容包（`--author-content`）能否经**真实 stdio** 开局并操作扩展内容。

它启动带作者内容包的宿主，走 `describe → start → observe → 用药水`，并断言
**玩家可见的扩展事实**真的出现在 wire 上（药水实体、Rarity/Target、药水栏 group 与成员、
`UsePotion` 候选、整局 HP/金币、长期牌组 group 与其"两批实例"性质），以及药水**真的能被操作**
（提交 → 目标提问 → 消耗 → 效果生效）。

用法（仓库根目录）：
    python net-the-spire/client/test_author_content.py

退出码：
    0 = 作者 Gym 完成了 describe → start → observe，且扩展事实与用药水操作全部成立；
    1 = 宿主起来了、开局也成功，但**扩展事实或操作不成立**（W-01 未完成，逐条打印缺什么）；
    3 = `start` 被拒（当前已知阻塞项会在这里暴露，例如未声明的动作）；
    2 = 基础设施问题（宿主未构建等）。

这个脚本只覆盖"作者内容经真实入口可运行 + 扩展事实可用"；完整 W-01 还要走"终局 + 轨迹离线重编码"，
并把 Runner/Collector 的硬编码身份改为从 `describe` 读取。
"""

from __future__ import annotations

import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from net_client import NetClient, NetClientError  # noqa: E402

SCENARIO = "base-spire-combat-slice-v1"
AUTHOR_FLAG = "--author-content"

POTION_KIND = "net:kind:base-spire:Potion"
POWER_KIND = "net:kind:base-spire:Power"
RUN_KIND = "net:kind:base-spire:Run"
USE_POTION = "net:action:base-spire:UsePotion"
ANSWER_DECISION = "net:action:net.answer_decision"
USE_POTION_FIELD = "net:field:base-spire:UsePotion.Potion"
BELT_GROUP = "net:group_type:base-spire:potion_belt"
DECK_GROUP = "net:group_type:base-spire:long_term_deck"
BEETLE_JUICE = "base-spire:beetle-juice"
BATTLE_PILES = ("net:pile_type:base-spire:hand", "net:pile_type:base-spire:draw",
                "net:pile_type:base-spire:discard")


def _incomplete(message: str) -> int:
    print(f"作者 Gym 未完成（W-01）：{message}", file=sys.stderr)
    return 1


def _field(action: dict, field_id: str):
    for field in action.get("fields", []):
        if field.get("field_id") == field_id:
            return field.get("value")
    raise KeyError(f"候选缺少字段 {field_id}")


def _group(snapshot: dict, group_type: str):
    groups = [group for group in snapshot.get("groups", []) if group.get("type_id") == group_type]
    return groups[0] if len(groups) == 1 else None


def _belt(snapshot: dict):
    return _group(snapshot, BELT_GROUP)


def main() -> int:
    try:
        client = NetClient.host_from_solution(extra_args=[AUTHOR_FLAG])
    except (NetClientError, OSError) as failure:
        print(f"环境错误（宿主未构建？）：{failure}", file=sys.stderr)
        return 2

    try:
        describe = client.describe()
        if describe.get("ok") is not True:
            print(f"describe 失败：{describe}", file=sys.stderr)
            return 2
        identity = describe["result"]["identity"]
        if "author-content" not in identity.get("catalog_id", ""):
            return _incomplete(f"作者宿主的目录身份不对：{identity.get('catalog_id')}")

        started = client.start(identity, SCENARIO, seed=1)
        if started.get("ok") is not True:
            message = started.get("error", {}).get("message", json.dumps(started))
            print(f"已知阻塞（W-01 未完成）：{message}", file=sys.stderr)
            return 3

        session = started["result"]["session_id"]
        observed = client.observe(session)
        if observed.get("ok") is not True:
            print(f"observe 失败：{observed}", file=sys.stderr)
            return 3

        snapshot = observed["result"]["snapshot"]
        kinds = sorted({entity["kind_id"] for entity in snapshot["entities"]})
        actions = sorted({action["action_type"] for action in snapshot["legal_actions"]})
        print("作者内容包开局成功")
        print(f"  entities={len(snapshot['entities'])} kinds={kinds}")
        print(f"  actions={actions}")

        # ① 玩家可见的扩展事实：药水实体、药水栏 group、UsePotion 候选。
        if POTION_KIND not in kinds:
            return _incomplete("公开快照里没有药水实体 kind")
        if USE_POTION not in actions:
            return _incomplete("公开快照里没有 UsePotion 候选")
        belt = _belt(snapshot)
        if belt is None:
            return _incomplete("公开快照里没有唯一的药水栏 group（药水栏应发布为 group）")
        if len(belt["members"]) != 2:
            return _incomplete(f"药水栏成员数应为 2，实际 {len(belt['members'])}")
        content = {entity["instance_id"]: entity.get("content_id") for entity in snapshot["entities"]}
        selected = [content.get(member["entity_ref"]) for member in belt["members"]]
        if BEETLE_JUICE not in selected:
            return _incomplete(f"药水栏里没有 {BEETLE_JUICE}：{selected}")
        print(f"  药水栏 owner={belt['owner_ref']} members={selected}")

        # ② 整局状态与长期牌组：Run 的公开字段 + 长期牌组 group（owner = Run，成员 = 长期那批卡牌）。
        runs = [entity for entity in snapshot["entities"] if entity["kind_id"] == RUN_KIND]
        if len(runs) != 1:
            return _incomplete(f"公开快照里应恰有一个整局实体，实际 {len(runs)}")
        run = runs[0]
        hp = next((field.get("value") for field in run["fields"]
                   if field.get("field_id") == "net:field:base-spire:Run.Hp"), None)
        if hp != "80":
            return _incomplete(f"整局 HP 应为作者文本声明的 80，实际 {hp}")
        deck = _group(snapshot, DECK_GROUP)
        if deck is None:
            return _incomplete("公开快照里没有唯一的长期牌组 group")
        if deck["owner_ref"] != run["instance_id"]:
            return _incomplete("长期牌组的 owner 应是整局实体")
        if not deck["members"]:
            return _incomplete("长期牌组没有成员（作者文本 deck{} 声明了 10 张）")
        battle = set()
        for pile in snapshot["piles"]:
            if pile["pile_ref"] in BATTLE_PILES:
                battle.update(pile["visible_members"])
        overlap = battle & {member["entity_ref"] for member in deck["members"]}
        if overlap:
            return _incomplete(f"长期牌组与战斗牌区必须是两批实例，重叠 {sorted(overlap)}")
        print(f"  整局 HP={hp} 长期牌组 {len(deck['members'])} 张（与战斗牌区互不相交）")

        # ③ 药水**真的能被操作**：提交 beetle-juice（敌人目标 ⇒ 药水的 target 步骤会再问一次目标）。
        beetle = next(
            action for action in snapshot["legal_actions"]
            if action["action_type"] == USE_POTION
            and content.get(_field(action, USE_POTION_FIELD)) == BEETLE_JUICE
        )
        stepped = client.step(session, snapshot["observation_id"], beetle["candidate_id"],
                              request_id="author-use-potion")
        if stepped.get("ok") is not True:
            return _incomplete(f"提交 UsePotion 被拒：{stepped.get('error')}")
        waiting = stepped["result"]["snapshot"]
        answers = [action for action in waiting["legal_actions"] if action["action_type"] == ANSWER_DECISION]
        if not answers:
            return _incomplete("药水的 target 步骤没有在真实链路上暂停提问目标")
        answered = client.step(session, waiting["observation_id"], answers[0]["candidate_id"],
                              request_id="author-potion-target")
        if answered.get("ok") is not True:
            return _incomplete(f"药水目标应答被拒：{answered.get('error')}")

        after = client.observe(session)
        if after.get("ok") is not True:
            return _incomplete(f"用药水后 observe 失败：{after}")
        final = after["result"]["snapshot"]
        belt_after = _belt(final)
        if belt_after is None or len(belt_after["members"]) != 1:
            return _incomplete(
                f"用过的药水必须离开药水栏（消耗）：{belt_after and len(belt_after['members'])}")
        applied = [entity for entity in final["entities"]
                   if entity["kind_id"] == POWER_KIND and entity.get("content_id") == "base-spire:shrink"]
        if not applied:
            return _incomplete("beetle-juice 的效果没有生效（敌人应获得 Shrink）")
        stacks = next(field.get("value") for field in applied[0]["fields"]
                      if field.get("field_id") == "net:field:base-spire:Power.Stacks")
        if stacks != "4":
            return _incomplete(f"Shrink 层数应为药水声明的 4，实际 {stacks}")
        print(f"  用药水成功：药水栏 {len(belt['members'])} → {len(belt_after['members'])}，"
              f"敌人获得 Shrink({stacks})")
        print("  注意：本脚本覆盖到「开局 + 整局/长期牌组事实 + 用药水」；"
              "终局与轨迹离线重编码见 AI 侧 python ai-the-spire/tests/j01_episode.py --only author")
        return 0
    finally:
        try:
            client.close()
        except Exception:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
