"""Manual terminal player. All choices and visible state come from the public Host."""
from __future__ import annotations

import argparse
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "net-the-spire/client"))
from net_client import NetClient, OutcomeUnknown
from public_snapshot import (
    action_name, entity_of, field, find_entity, pile_visible, public_fields,
    relation_targets, sequence, short,
)

DEFAULT_HOST = ROOT / "net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll"
ACTION_LABELS = {
    "ChooseMapPoint": "前往地图节点", "PlayCard": "打出卡牌", "EndTurn": "结束回合",
    "net.answer_decision": "选择目标或卡牌", "TakeRewardGold": "领取金币",
    "TakeRewardCard": "领取卡牌", "SkipRewardCard": "跳过卡牌奖励",
    "TakeRewardPotion": "领取药水", "SkipRewardPotion": "跳过药水奖励",
    "UsePotion": "使用药水", "DiscardPotion": "丢弃药水",
    "OpenTreasureChest": "打开宝箱", "TakeTreasureRelic": "领取遗物",
    "SkipTreasureRelic": "跳过遗物", "RestAtCampfire": "休息",
    "SmithAtCampfire": "升级卡牌", "BuyShopCard": "购买卡牌",
    "BuyShopRelic": "购买遗物", "BuyShopPotion": "购买药水",
    "RemoveShopCard": "商店删牌", "LeaveShop": "离开商店",
    "ContinueToNextAct": "进入下一幕", "EnterNextRoom": "进入下一房间",
}
PHASES = {0: "地图开局", 1: "战斗", 2: "地图", 3: "奖励", 4: "营火",
          5: "宝箱", 6: "幕完成", 7: "商店"}


class HostRejected(RuntimeError):
    pass


def result(response: dict) -> dict:
    if not response.get("ok"):
        error = response.get("error") or {}
        raise HostRejected(f"{error.get('code')}: {error.get('message')}")
    return response["result"]


def name(entity: dict | None) -> str:
    if not entity:
        return "?"
    identity = (entity.get("content_id") or field(entity, "DefinitionId")
                or field(entity, "SourceName") or entity.get("kind_id"))
    return short(identity) + ("+" if field(entity, "Upgraded") is True else "")


def describe(snapshot: dict, ref) -> str:
    entity = entity_of(snapshot, ref)
    if not entity:
        return str(ref)
    kind = short(entity.get("kind_id"))
    if kind == "MapNode":
        return f"地图第{field(entity, 'Row')}行 / 列{field(entity, 'Col')} / {short(field(entity, 'Type'))}"
    parts = [name(entity)]
    if kind == "Card":
        for key, label in [("Cost", "费用"), ("Amount", "数值")]:
            value = field(entity, key)
            if value is not None:
                parts.append(f"{label}={value}")
        modifier = field(entity, "CostModifier")
        if modifier:
            parts.append(f"费用修正={modifier}")
        if field(entity, "PowerId"):
            parts.append(f"能力={short(field(entity, 'PowerId'))}×{field(entity, 'PowerStacks')}")
    elif kind == "ShopOffer":
        if field(entity, "Kind") == "removal":
            parts[0] = "删牌服务"
        parts.extend([f"商品位={field(entity, 'Slot')}", f"价格={field(entity, 'Price')}"])
        if field(entity, "OnSale"):
            parts.append("折扣")
        if field(entity, "Sold"):
            parts.append("已售出")
        if field(entity, "Unsupported"):
            parts.append(f"不可买：{field(entity, 'UnsupportedReason')}")
    else:
        for key, label in [("Hp", "生命"), ("Block", "格挡"), ("NextMove", "意图")]:
            value = field(entity, key)
            if value is not None:
                parts.append(f"{label}={value}")
    powers = relation_targets(snapshot, "Powers").get(entity["instance_id"], [])
    if powers:
        parts.append("能力=" + ", ".join(
            f"{name(entity_of(snapshot, p))}×{field(entity_of(snapshot, p), 'Stacks')}" for p in powers))
    return " / ".join(parts)


def action_label(snapshot: dict, action: dict) -> str:
    key = action_name(action)
    label = ACTION_LABELS.get(key, key)
    args = [describe(snapshot, f.get("value")) for f in action.get("fields") or [] if f.get("state") == "known"]
    return label + ("：" + "; ".join(args) if args else "")


def show(snapshot: dict) -> list:
    run, player = find_entity(snapshot, "Run"), find_entity(snapshot, "Player")
    phase = field(run, "MapPhase")
    combat = phase == 1 or (phase is None and field(player, "BattleSettled") is not True)
    health = player if combat else run
    turn = f" / 第{field(player, 'Turn')}回合" if combat else ""
    print(f"\n=== {PHASES.get(phase, '游戏')}{turn} ===")
    stats = f"生命 {field(health, 'Hp')}/{field(health, 'MaxHp')}  金币 {field(run, 'Gold')}"
    if combat:
        stats += f"  格挡 {field(player, 'Block')}  能量 {field(player, 'Energy')}/{field(player, 'MaxEnergy')}"
    print(stats)
    enemies = [e for e in snapshot.get("entities") or [] if short(e.get("kind_id")) == "Monster"]
    if combat:
        for enemy in enemies:
            print("敌人：" + describe(snapshot, enemy["instance_id"]))
        powers = relation_targets(snapshot, "Powers").get(player["instance_id"], []) if player else []
        if powers:
            print("玩家能力：" + ", ".join(
                f"{name(entity_of(snapshot, p))}×{field(entity_of(snapshot, p), 'Stacks')}" for p in powers))
    hand = pile_visible(snapshot, "HandPile") if combat else []
    if hand:
        print("手牌：")
        for ref in hand:
            print("  " + describe(snapshot, ref))
    if combat:
        labels = {"DrawPileEntity": "抽牌", "DiscardPileEntity": "弃牌", "ExhaustPileEntity": "消耗"}
        counts = []
        for pile in snapshot.get("piles") or []:
            entity = entity_of(snapshot, pile.get("pile_ref"))
            label = labels.get(short(entity.get("kind_id"))) if entity else None
            if label:
                counts.append(f"{label}={pile.get('total_count')}")
        print("牌堆：" + " / ".join(counts))
    if phase == 7:
        print("货架：")
        for ref in sequence(snapshot, "ShopOffers"):
            print("  " + describe(snapshot, ref))
    actions = list(snapshot.get("legal_actions") or [])
    print("\n当前可执行：")
    for index, action in enumerate(actions, 1):
        print(f"  {index}. {action_label(snapshot, action)}")
    return actions


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="手动试玩 base-spire：输入数字选择当前公开动作")
    parser.add_argument("--host-dll", type=Path, default=DEFAULT_HOST)
    parser.add_argument("--seed", default="ENV-T03-SEED-L")
    parser.add_argument("--scope", choices=["dev-map", "dev"], default="dev-map")
    args = parser.parse_args(argv)
    if not args.host_dll.is_file():
        parser.error(f"Host 文件不存在：{args.host_dll}；请用 --host-dll 指定已构建文件")
    client = NetClient(["dotnet", str(args.host_dll.resolve()), "--scenario", "formal-dev", "--scope", args.scope])
    try:
        description = result(client.describe(timeout=30))
        profile = description["capabilities"]["scenario_profiles"][0]["id"]
        started = result(client.start(description["identity"], profile, seed=args.seed, timeout=30))
        snapshot, session_id = started["snapshot"], started["session_id"]
        print(f"已开局。Seed: {started['seed']}；范围: {args.scope}")
        print("输入动作编号；d 编号查看公开字段；p 查看公开牌区；r 刷新；q 退出。")
        print("当前为开发环境，未实现的房间/敌人会明确停止。")
        while True:
            if snapshot.get("status") == "terminal":
                print("本局结果：", snapshot.get("terminal_result"))
                return 0
            actions = show(snapshot)
            if not actions:
                print("当前没有可执行动作，停止试玩。")
                return 4
            command = input("选择> ").strip()
            if command.lower() in ("q", "quit", "exit"):
                return 0
            if command.lower() == "r":
                snapshot = result(client.observe(session_id, timeout=30))["snapshot"]
                continue
            if command.lower() == "p":
                zones = [("弃牌", pile_visible(snapshot, "DiscardPileEntity")),
                         ("消耗", pile_visible(snapshot, "ExhaustPileEntity")),
                         ("长期牌组", sequence(snapshot, "LongTermDeck"))]
                for label, refs in zones:
                    print(label + "：" + ("; ".join(describe(snapshot, ref) for ref in refs) or "（无公开成员）"))
                continue
            detail = command.lower().startswith("d ")
            number = command[2:].strip() if detail else command
            if not number.isdecimal() or not 1 <= int(number) <= len(actions):
                print("请输入当前列表中的动作编号，或 p / r / q。")
                continue
            chosen = actions[int(number) - 1]
            if detail:
                for f in chosen.get("fields") or []:
                    entity = entity_of(snapshot, f.get("value"))
                    print(describe(snapshot, f.get("value")))
                    if entity:
                        for key, value in public_fields(entity).items():
                            print(f"  {key}: {value}")
                continue
            print("执行：" + action_label(snapshot, chosen))
            snapshot = result(client.step(session_id, snapshot["observation_id"], chosen["candidate_id"], timeout=30))["snapshot"]
    except (EOFError, KeyboardInterrupt):
        print("\n已退出试玩。")
        return 0
    except (HostRejected, OutcomeUnknown) as error:
        print(f"\n运行已停止：{error}")
        return 4
    finally:
        client.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
