#!/usr/bin/env python3
"""ENV-T03 商店公开驱动检查：离线验证 `run_game.py` 的商店策略与公开候选闭合（不需要 Host 进程）。

它只回答一个问题：**给定公开快照（合成或已记录的真实帧），运行器挑的候选是不是当前
`legal_actions` 里的候选、是不是按照写明的固定规则挑的**。不启动 Host、不构建任何 C#、
不读隐藏牌堆/随机数、不重判 Engine 规则。

分组：

* `policy` —— 合成公开快照上的店内固定规则：默认安全离店、`--shop-policy=buy` 按公开 `(Price, Slot)`
  最小者买一件、`--shop-buy-kind`/`--shop-buy-slot`、`--shop-remove` 删一张后离店、已售/不可买/
  字段缺失候选的跳过与留证、一次访问不重复购买、相位 7 之外不进商店分支。
* `decision` —— 删牌 decision（公开 `LongTermDeck` 实例）与战斗目标/其它选牌 decision 的区分：
  删牌按 CLI 指定的具体长期实例回答；战斗目标仍按公开 Hp 最低；域不匹配时具名停止。
* `map` —— 商店房型的进店许可：`--shop-room` 关闭时保持原 `unsupported_map_room` 边界（不改整局状态），
  打开时才把 `Shop` 当已接线房型；`shop_declared_actions()` 只按公开目录声明判定。
* `replay` —— 已记录的真实公开整局帧（默认 `runs/seed-K`）逐快照复算：每一步挑的候选必须在该快照的
  当前 `legal_actions` 里，且与记录的动作/候选 `candidate_id` 逐字一致（旧战斗/奖励/地图分支不回归）。
  固定规则按**记录里的策略**重建（含 `shop_room_entry` 与 `shop_*`），因此带商店的真实轨迹也能整段
  复算：进店复算额外核对公开货架 14 格且 `Slot` 覆盖 0..13、不可买条目的源身份与原因、买到一件或如实记
  `shop_buy_unavailable`、删牌提交后一定有 answer 或明确未提交；记录里没有商店时保持原行为。

用法（仓库根目录）：

```powershell
python -X utf8 -u net-the-spire/client/test_shop_public.py
python -X utf8 -u net-the-spire/client/test_shop_public.py --only policy
python -X utf8 -u net-the-spire/client/test_shop_public.py --run-dir .audit/<RUN_DIR>
```

退出码：0 = 所选分组全部执行且通过；1 = 断言或输入失败；2 = 参数/输入缺失。
"""

from __future__ import annotations

import argparse
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

from run_game import (  # noqa: E402
    FixedOrderPolicy, Stop, state_view, shop_declared_actions, SHOP_ACTION_NAMES,
)
from public_snapshot import field  # noqa: E402

GROUPS = ("policy", "decision", "map", "replay")
DEFAULT_RUN_DIR = ROOT / ".audit" / "dsh-public-run-driver-2026-10-11" / "runs" / "seed-K"

SHOP_ACTION_FOR_KIND = {"card": "BuyShopCard", "relic": "BuyShopRelic", "potion": "BuyShopPotion",
                        "removal": "RemoveShopCard"}


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

    def report(self, title: str) -> None:
        print(f"[test_shop_public] {title}: passed={self.passed} failed={self.failed}")
        for detail in self.details:
            print(detail)


# -- 合成公开快照（形状与真实帧一致：entity/sequence/candidate 的 field_id 用公开 id） ----------------------

def _f(field_id: str, value) -> dict:
    return {"field_id": field_id, "state": "known", "value": value}


def entity(instance: str, kind: str, fields: dict, *, content_id=None) -> dict:
    return {"instance_id": instance, "kind_id": f"net:kind:base-spire:{kind}",
            "content_id": content_id, "content_state": "known" if content_id else "none",
            "fields": [_f(f"net:field:base-spire:{kind}.{name}", value) for name, value in fields.items()]}


def offer(instance: str, *, slot: int, kind: str, definition_id: str = "", source_name: str = "",
          rarity: str = "common", pool_kind: int = -1, pool_index: int = -1, on_sale: bool = False,
          price: int = 50, sold: bool = False, unsupported: bool = False, reason: str = "",
          omit: tuple = ()) -> dict:
    fields = {"Slot": slot, "Kind": kind, "DefinitionId": definition_id, "SourceName": source_name,
              "Rarity": rarity, "PoolKind": pool_kind, "PoolIndex": pool_index, "OnSale": on_sale,
              "Price": price, "Sold": sold, "Unsupported": unsupported, "UnsupportedReason": reason}
    return entity(instance, "ShopOffer", {name: value for name, value in fields.items() if name not in omit})


def card(instance: str, content_id: str, *, upgraded: bool = False, amount: int = 6, cost: int = 1) -> dict:
    return entity(instance, "Card", {"Amount": amount, "Cost": cost, "CostModifier": 0, "Upgraded": upgraded,
                                     "Kind": "net:cat:base-spire:CardKind:Attack", "Unplayable": False},
                  content_id=content_id)


def candidate(candidate_id: str, action: str, field_name: str, value) -> dict:
    action_type = action if ":" in action else f"net:action:base-spire:{action}"
    return {"candidate_id": candidate_id, "action_type": action_type,
            "fields": [_f(f"net:field:{action}.{field_name}", value)]}


def offer_candidate(candidate_id: str, shop_offer: dict, action=None) -> dict:
    kind = _offer_field(shop_offer, "Kind")
    return candidate(candidate_id, action or SHOP_ACTION_FOR_KIND[kind], "Offer",
                     shop_offer["instance_id"])


def _offer_field(shop_offer: dict, name: str):
    for item in shop_offer["fields"]:
        if item["field_id"].endswith("." + name):
            return item["value"]
    return None


def sequence(type_name: str, elements: list, owner: str = "run") -> dict:
    return {"type_id": f"net:sequence_type:base-spire:{type_name}", "owner_ref": owner,
            "elements": list(elements)}


def shop_snapshot(*, offers=(), deck=(), legal=(), extra_entities=(), gold: int = 99, phase: int = 7,
                  removals_used: int = 0, observation_id: int = 10, status: str = "decision",
                  publish_offers: bool = True, publish_deck: bool = True) -> dict:
    run = entity("run", "Run", {"Hp": 70, "MaxHp": 80, "Gold": gold, "MapPhase": phase,
                                "ShopCardRemovalsUsed": removals_used, "CurrentRoom": 1, "RoomCount": 1,
                                "CurrentNode": "e9", "ActCompleted": False})
    cards = [card(f"d{index}", content_id) for index, content_id in enumerate(deck)]
    sequences = []
    if publish_offers:
        sequences.append(sequence("ShopOffers", [item["instance_id"] for item in offers]))
    if publish_deck and cards:
        sequences.append(sequence("LongTermDeck", [item["instance_id"] for item in cards]))
    return {"protocol_version": "0.2", "ruleset_id": "test", "catalog_id": "test",
            "backend_profile_id": "test", "session_id": "test-session", "observation_id": observation_id,
            "status": status, "terminal_result": None,
            "entities": [run] + list(offers) + cards + list(extra_entities), "sequences": sequences,
            "piles": [], "relations": [], "groups": [], "legal_actions": list(legal)}


def shop_policy(**kwargs) -> FixedOrderPolicy:
    options = {"step_frontier": False, "relic_policy": "skip", "potion_policy": "take",
               "use_potions": True}
    options.update(kwargs)
    return FixedOrderPolicy(**options)


def leave_candidate() -> dict:
    return candidate("c-leave", "LeaveShop", "", "")


# -- policy 组 ------------------------------------------------------------------------------------------

def group_policy(checker: Checker) -> None:
    card_cheap = offer("o1", slot=0, kind="card", definition_id="base-spire:anger", rarity="common", price=45)
    card_mid = offer("o2", slot=3, kind="card", definition_id="base-spire:shrug-it-off", rarity="uncommon",
                     price=90)
    card_sale = offer("o3", slot=1, kind="card", definition_id="base-spire:cleave", rarity="common",
                      price=90, on_sale=True)

    # 1) 默认安全离店：即使有可买候选也先 LeaveShop。
    legal = [offer_candidate("c-buy-1", card_cheap), offer_candidate("c-buy-2", card_mid),
             candidate("c-remove", "RemoveShopCard", "Offer", "o14"), leave_candidate()]
    snapshot = shop_snapshot(offers=[card_cheap, card_mid], deck=["base-spire:strike-ironclad"],
                             legal=legal)
    policy = shop_policy()
    choice = policy.choose(snapshot)
    checker.check("policy: 默认安全离店", getattr(choice, "action", None) == "LeaveShop",
                  f"default policy 应 LeaveShop，实际 {choice}")
    checker.check("policy: 离店候选来自当前快照",
                  choice.candidate["candidate_id"] in {c["candidate_id"] for c in legal},
                  "candidate 必须来自当前 legal_actions")

    # 2) --shop-policy=buy：公开 (Price, Slot) 最小者；已售/不可买/字段缺失的候选被跳过并留证。
    sold = offer("o4", slot=2, kind="card", definition_id="base-spire:sold-out", price=10, sold=True)
    unsupported = offer("o5", slot=4, kind="card", definition_id="", source_name="unimplemented",
                        price=20, unsupported=True, reason="base-spire: 未登记")
    missing_price = offer("o6", slot=5, kind="card", definition_id="base-spire:no-price", omit=("Price",))
    snapshot = shop_snapshot(
        offers=[sold, unsupported, missing_price, card_cheap, card_mid, card_sale, ],
        deck=["base-spire:strike-ironclad"],
        legal=[offer_candidate(f"c-{item['instance_id']}", item)
               for item in (sold, unsupported, missing_price, card_cheap, card_mid, card_sale)]
              + [leave_candidate()])
    policy = shop_policy(shop_policy="buy")
    choice = policy.choose(snapshot)
    checker.check("policy: buy 挑公开 Price 最小者", getattr(choice, "candidate", {}).get("candidate_id") == "c-o1",
                  f"应挑 price=45 的 o1，实际 {choice}")
    checker.check("policy: buy 动作名", getattr(choice, "action", None) == "BuyShopCard", str(choice))
    buy_event = next((event for event in policy.events if event["event"] == "shop_buy"), None)
    checker.check("policy: buy 事件记录公开商品事实",
                  buy_event is not None and buy_event["offer"]["fields"].get("Price") == 45
                  and buy_event["offer"]["fields"].get("Slot") == 0,
                  f"事件里应保留公开货架事实，实际 {buy_event}")
    skipped = {row["candidate_id"]: row["skip"] for row in (buy_event or {}).get("skipped", [])}
    checker.check("policy: 已售候选被跳过", "c-o4" in skipped, f"skipped={skipped}")
    checker.check("policy: 不可买候选被跳过", "c-o5" in skipped, f"skipped={skipped}")
    checker.check("policy: 字段缺失候选被跳过且记原因", "c-o6" in skipped and "缺失" in skipped.get("c-o6", ""),
                  f"skipped={skipped}")

    # 3) 一次访问只买一件：即使买候选还在（防止重复购买已买到手的那件），也回到离店。
    policy = shop_policy(shop_policy="buy")
    first = policy.choose(snapshot)
    second = policy.choose(snapshot)
    checker.check("policy: 只买一件（不重复购买）",
                  getattr(first, "action", None) == "BuyShopCard"
                  and getattr(second, "action", None) == "LeaveShop",
                  f"first={first.action if hasattr(first, 'action') else first} "
                  f"second={second.action if hasattr(second, 'action') else second}")

    # 4) --shop-buy-kind=potion 只从公开 BuyShopPotion 候选里挑。
    potion = offer("o7", slot=10, kind="potion", definition_id="base-spire:fire-potion", rarity="common",
                   price=50)
    policy = shop_policy(shop_policy="buy", shop_buy_kind="potion")
    choice = policy.choose(shop_snapshot(
        offers=[card_cheap, potion], deck=["base-spire:strike-ironclad"],
        legal=[offer_candidate("c-card", card_cheap), offer_candidate("c-potion", potion), leave_candidate()]))
    checker.check("policy: buy-kind 映射到公开动作",
                  getattr(choice, "candidate", {}).get("candidate_id") == "c-potion"
                  and getattr(choice, "action", None) == "BuyShopPotion", str(choice))

    # 5) --shop-buy-slot 只认指定公开 Slot；没有该 Slot 的候选就记原因、然后离店。
    policy = shop_policy(shop_policy="buy", shop_buy_slot=3)
    choice = policy.choose(shop_snapshot(
        offers=[card_cheap, card_mid], deck=["base-spire:strike-ironclad"],
        legal=[offer_candidate("c-card-0", card_cheap), offer_candidate("c-card-3", card_mid),
               leave_candidate()]))
    checker.check("policy: --shop-buy-slot 选中指定 Slot",
                  getattr(choice, "candidate", {}).get("candidate_id") == "c-card-3", str(choice))
    policy = shop_policy(shop_policy="buy", shop_buy_slot=9)
    choice = policy.choose(shop_snapshot(
        offers=[card_cheap], deck=["base-spire:strike-ironclad"],
        legal=[offer_candidate("c-card-0", card_cheap), leave_candidate()]))
    checker.check("policy: 指定 Slot 不可买 -> 记原因并离店",
                  getattr(choice, "action", None) == "LeaveShop"
                  and any(event["event"] == "shop_buy_unavailable" for event in policy.events),
                  f"{choice} events={[e['event'] for e in policy.events]}")

    # 6) 缺可选字段不影响购买（只按必需字段判）；源身份照旧记录。
    partial = offer("o8", slot=6, kind="card", definition_id="base-spire:partial", price=30,
                    omit=("SourceName", "OnSale", "Rarity", "PoolKind", "PoolIndex"))
    policy = shop_policy(shop_policy="buy")
    choice = policy.choose(shop_snapshot(
        offers=[partial], deck=["base-spire:strike-ironclad"],
        legal=[offer_candidate("c-partial", partial), leave_candidate()]))
    checker.check("policy: 可选字段缺失仍可按公开 Price 购买",
                  getattr(choice, "candidate", {}).get("candidate_id") == "c-partial", str(choice))

    # 7) --shop-remove：先提交公开 RemoveShopCard，再在 decision 上公开选具体长期实例，最后离店。
    removal = offer("o14", slot=13, kind="removal", price=75)
    policy = shop_policy(shop_remove=True)
    choice = policy.choose(shop_snapshot(
        offers=[card_cheap, removal], deck=["base-spire:strike-ironclad", "base-spire:defend-ironclad"],
        legal=[offer_candidate("c-card", card_cheap), offer_candidate("c-removal", removal), leave_candidate()]))
    checker.check("policy: --shop-remove 先提交公开删牌位",
                  getattr(choice, "action", None) == "RemoveShopCard"
                  and getattr(choice, "candidate", {}).get("candidate_id") == "c-removal", str(choice))
    checker.check("policy: 删牌前登记 pending（用于区分 decision 域）", policy.shop_removal_pending)

    # 8) 合成货架证据：已售/不可买条目保留在 state_view 里，不被清空或过滤。
    view = state_view(shop_snapshot(offers=[sold, unsupported, card_cheap],
                                    legal=[offer_candidate("c-cheap", card_cheap), leave_candidate()]))
    shelf_rows = view["shop_offers"]
    checker.check("policy: state_view 保留整条公开货架（含已售/不可买）",
                  len(shelf_rows) == 3 and view["shop_offers_published"] is True,
                  f"rows={len(shelf_rows)}")
    checker.check("policy: 已售/不可买条目的公开字段原样保留",
                  shelf_rows[0]["fields"].get("Sold") is True
                  and shelf_rows[1]["fields"].get("Unsupported") is True
                  and shelf_rows[1]["fields"].get("UnsupportedReason") == "base-spire: 未登记",
                  json.dumps([row["fields"] for row in shelf_rows], ensure_ascii=False)[:400])

    # 9) 相位 7 但没有公开货架序列：如实标未发布，不伪装成"库存被清空"。
    view = state_view(shop_snapshot(offers=[card_cheap], legal=[leave_candidate()], publish_offers=False))
    checker.check("policy: 货架序列未发布时如实记录（不清空/不猜）",
                  view["shop_offers"] == [] and view["shop_offers_published"] is False,
                  json.dumps({"offers": view["shop_offers"], "published": view["shop_offers_published"]}))
    policy = shop_policy()
    choice = policy.choose(shop_snapshot(offers=[card_cheap], legal=[leave_candidate()],
                                        publish_offers=False))
    enter = next((event for event in policy.events if event["event"] == "shop_enter"), None)
    checker.check("policy: 无货架证据也照常按公开候选离店", getattr(choice, "action", None) == "LeaveShop"
                  and (enter or {}).get("shelf_note") is not None,
                  f"{choice} enter={enter}")

    # 10) 相位 7 但既没有显式策略也没有 LeaveShop：具名停止，绝不猜着买。
    policy = shop_policy(shop_policy="buy", shop_buy_kind="relic")
    choice = policy.choose(shop_snapshot(
        offers=[card_cheap], deck=["base-spire:strike-ironclad"],
        legal=[offer_candidate("c-card", card_cheap)]))
    checker.check("policy: 店内无可用显式策略 -> shop_no_decision",
                  isinstance(choice, Stop) and choice.reason == "shop_no_decision", str(choice))

    # 11) 非商店快照不进商店分支：地图选择保持不变。
    map_node = entity("e9", "MapNode", {"Type": "Monster", "Row": 3, "Col": 1, "Visited": False})
    boss_node = entity("e21", "MapNode", {"Type": "Boss", "Row": 16, "Col": 3, "Visited": False})
    snapshot = {"protocol_version": "0.2", "ruleset_id": "test", "catalog_id": "test",
                "backend_profile_id": "test", "observation_id": 3, "status": "decision",
                "terminal_result": None, "entities": [entity("run", "Run", {"MapPhase": 2, "Hp": 70,
                                                                           "MaxHp": 80, "Gold": 99}),
                                                      map_node, boss_node],
                "sequences": [], "piles": [], "groups": [],
                "relations": [{"type_id": "net:relation:base-spire:MapChildren", "source_ref": "e9",
                               "target_ref": "e21"}],
                "legal_actions": [candidate("c-map", "ChooseMapPoint", "Node", "e9")]}
    policy = shop_policy(shop_policy="buy", shop_remove=True)
    choice = policy.choose(snapshot)
    checker.check("policy: 非商店相位走原地图分支（不进商店分支）",
                  getattr(choice, "action", None) == "ChooseMapPoint" and not policy.events, str(choice))


# -- decision 组 ----------------------------------------------------------------------------------------

def group_decision(checker: Checker) -> None:
    deck = ["base-spire:strike-ironclad", "base-spire:defend-ironclad", "base-spire:bash"]
    removal_offer = offer("o14", slot=13, kind="removal", price=75)

    def removal_snapshot(observation_id: int) -> dict:
        legal = [candidate(f"c-d{index}", "net.answer_decision", "Instance", f"d{index}")
                 for index in range(len(deck))]
        return shop_snapshot(offers=[removal_offer], deck=deck, legal=legal, observation_id=observation_id)

    # 1) 默认删第一张（公开顺序），并且记录公开牌组与落点。
    policy = shop_policy(shop_remove=True)
    first = policy.choose(shop_snapshot(offers=[removal_offer], deck=deck,
                                        legal=[offer_candidate("c-removal", removal_offer), leave_candidate()]))
    checker.check("decision: 先提交删牌位", getattr(first, "action", None) == "RemoveShopCard", str(first))
    answer = policy.choose(removal_snapshot(11))
    checker.check("decision: 默认删公开顺序第一张",
                  getattr(answer, "candidate", {}).get("candidate_id") == "c-d0", str(answer))
    checker.check("decision: 删牌后 pending 复位", policy.shop_removal_pending is False
                  and policy.shop_removed is True, str(policy.events[-1]))
    checker.check("decision: 事件记录公开长期牌组",
                  "base-spire:strike-ironclad" in json.dumps(policy.events[-1], ensure_ascii=False),
                  json.dumps(policy.events[-1], ensure_ascii=False)[:300])

    # 2) --shop-remove-card 指定具体长期实例（第 3 张 bash）。
    policy = shop_policy(shop_remove=True, shop_remove_card=2)
    policy.shop_removal_pending = True
    answer = policy.choose(removal_snapshot(12))
    checker.check("decision: --shop-remove-card 选指定实例",
                  getattr(answer, "candidate", {}).get("candidate_id") == "c-d2", str(answer))

    # 3) --shop-remove-card-id 按公开卡 id 选实例（短名匹配）。
    policy = shop_policy(shop_remove=True, shop_remove_card_id="defend")
    policy.shop_removal_pending = True
    answer = policy.choose(removal_snapshot(13))
    checker.check("decision: --shop-remove-card-id 选指定卡实例",
                  getattr(answer, "candidate", {}).get("candidate_id") == "c-d1", str(answer))

    # 4) 越界 / 找不到 -> 具名停止，不发动作（状态不变）。
    policy = shop_policy(shop_remove=True, shop_remove_card=9)
    policy.shop_removal_pending = True
    choice = policy.choose(removal_snapshot(14))
    checker.check("decision: 删牌下标越界 -> 具名停止",
                  isinstance(choice, Stop) and choice.reason == "shop_removal_index_out_of_range"
                  and policy.shop_removal_pending, str(choice))
    policy = shop_policy(shop_remove=True, shop_remove_card_id="bludgeon")
    policy.shop_removal_pending = True
    choice = policy.choose(removal_snapshot(15))
    checker.check("decision: 指定卡不在候选里 -> 具名停止",
                  isinstance(choice, Stop) and choice.reason == "shop_removal_card_not_found", str(choice))

    # 5) 删牌 decision 的域不是公开长期牌组时停止，绝不把它当战斗目标选。
    monsters = [entity("m1", "Monster", {"Hp": 5}), entity("m2", "Monster", {"Hp": 40})]
    legal = [candidate("c-m1", "net.answer_decision", "Instance", "m1"),
             candidate("c-m2", "net.answer_decision", "Instance", "m2")]
    policy = shop_policy(shop_remove=True)
    policy.shop_removal_pending = True
    choice = policy.choose(shop_snapshot(extra_entities=monsters, deck=deck, legal=legal))
    checker.check("decision: 删牌 pending 遇到战斗目标域 -> 具名停止（不混淆）",
                  isinstance(choice, Stop) and choice.reason == "shop_removal_decision_domain_mismatch",
                  str(choice))

    # 6) 没有删牌 pending 时，战斗目标选择保持原规则（公开 Hp 最低优先）。
    policy = shop_policy()
    choice = policy.choose(shop_snapshot(extra_entities=monsters, deck=deck, legal=legal))
    checker.check("decision: 战斗目标仍按公开 Hp 最低",
                  getattr(choice, "candidate", {}).get("candidate_id") == "c-m1", str(choice))

    # 7) 其它选牌 decision（手牌，不在长期牌组里）不被当成删牌：保持公开顺序第一张。
    hand_cards = [card("h1", "base-spire:strike-ironclad"), card("h2", "base-spire:defend-ironclad")]
    legal = [candidate("c-h1", "net.answer_decision", "Instance", "h1"),
             candidate("c-h2", "net.answer_decision", "Instance", "h2")]
    policy = shop_policy(shop_remove=True)
    choice = policy.choose(shop_snapshot(extra_entities=hand_cards, deck=deck, legal=legal))
    checker.check("decision: 手牌选择不被当成删牌（保持原行为）",
                  getattr(choice, "candidate", {}).get("candidate_id") == "c-h1", str(choice))


# -- map 组 ---------------------------------------------------------------------------------------------

def group_map(checker: Checker) -> None:
    shop_node = entity("e9", "MapNode", {"Type": "Shop", "Row": 4, "Col": 2, "Visited": False})
    boss_node = entity("e21", "MapNode", {"Type": "Boss", "Row": 16, "Col": 3, "Visited": False})
    snapshot = {"protocol_version": "0.2", "ruleset_id": "test", "catalog_id": "test",
                "backend_profile_id": "test", "observation_id": 5, "status": "decision",
                "terminal_result": None,
                "entities": [entity("run", "Run", {"MapPhase": 2, "Hp": 70, "MaxHp": 80, "Gold": 99}),
                             shop_node, boss_node],
                "sequences": [], "piles": [], "groups": [],
                "relations": [{"type_id": "net:relation:base-spire:MapChildren", "source_ref": "e9",
                               "target_ref": "e21"}],
                "legal_actions": [candidate("c-shop", "ChooseMapPoint", "Node", "e9")]}

    # 1) 默认（不进店）：保持原有 unsupported_map_room 边界，不改整局状态。
    choice = shop_policy().choose(snapshot)
    checker.check("map: 默认不进店 -> unsupported_map_room",
                  isinstance(choice, Stop) and choice.reason == "unsupported_map_room", str(choice))

    # 2) 进店打开：Shop 当已接线房型，选中该节点。
    policy = shop_policy(shop_room_entry=True)
    choice = policy.choose(snapshot)
    checker.check("map: --shop-room 打开后选中 Shop 节点",
                  getattr(choice, "action", None) == "ChooseMapPoint"
                  and getattr(choice, "candidate", {}).get("candidate_id") == "c-shop", str(choice))
    checker.check("map: 进店许可把 Shop 加入已接线房型", "Shop" in policy.steppable_map_types
                  and "Shop" not in shop_policy().steppable_map_types, str(policy.steppable_map_types))

    # 3) `auto` 只按宿主公开目录声明判定：声明五个商店动作才算接线。
    declared = shop_declared_actions({"declarations": {"action_types": [
        {"id": f"net:action:base-spire:{name}"} for name in SHOP_ACTION_NAMES]}})
    checker.check("map: 目录声明全部商店动作 -> 允许进店",
                  declared["shop_actions_declared"] is True and not declared["missing_shop_actions"],
                  json.dumps(declared, ensure_ascii=False))
    partial = shop_declared_actions({"declarations": {"action_types": [
        {"id": "net:action:base-spire:ChooseMapPoint"},
        {"id": "net:action:base-spire:BuyShopCard"},
        {"id": "net:action:base-spire:LeaveShop"}]}})
    checker.check("map: 目录只声明部分商店动作 -> 不进店并列出缺项",
                  partial["shop_actions_declared"] is False
                  and "RemoveShopCard" in partial["missing_shop_actions"],
                  json.dumps(partial, ensure_ascii=False))
    checker.check("map: 没有声明的目录 -> 不进店",
                  shop_declared_actions({"declarations": {}})["shop_actions_declared"] is False
                  and shop_declared_actions(None)["shop_actions_declared"] is False,
                  "空目录/None 都必须判未接线")
    checker.check("map: 目录形状不认识时不猜已接线",
                  shop_declared_actions({"declarations": {"action_types": ["BuyShopCard"]}})
                  ["shop_actions_declared"] is False,
                  "非 dict 条目不得被当成声明")


# -- replay 组 ------------------------------------------------------------------------------------------

def load_replayed_steps(path: pathlib.Path) -> list:
    """按帧顺序重建"每一步用的快照 + 当时提交的 (expected_observation_id, candidate_id)"。

    同一个 `observation_id` 可以出现在多个不同的快照上（例如用完药水后宿主在同一观察号上暂停到
    decision），所以**不能**只按 observation_id 索引；这里直接按双向帧顺序配对。
    """
    pairs, current, pending = [], None, None
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        frame = json.loads(line)
        body = frame.get("frame") or {}
        if frame.get("dir") == "client->host":
            if body.get("operation") == "step":
                payload = body.get("payload") or {}
                pending = (payload.get("expected_observation_id"), payload.get("candidate_id"))
            continue
        if pending is not None and current is not None and body.get("ok") is False:
            pairs.append((current, pending))
            pending = None
        result = body.get("result")
        snapshot = result.get("snapshot") if isinstance(result, dict) else None
        if not isinstance(snapshot, dict) or "legal_actions" not in snapshot:
            continue
        if pending is not None and current is not None:
            pairs.append((current, pending))
            pending = None
        current = snapshot
    return pairs


def load_recorded_steps(path: pathlib.Path) -> list:
    """已记录步骤的 `(expected_observation_id, action, candidate_id, reason)`（按写入顺序）。"""
    records = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        record = json.loads(line)
        records.append((record.get("expected_observation_id"), record.get("action"),
                        record.get("candidate_id"), record.get("reason") or ""))
    return records


#: 记录里出现过的**旧规则变体**的 reason 前缀（驱动器自己写在 steps.ndjson 的 `reason` 里）。
#: 这些步骤的挑选规则与当前生产规则不同（wave-1 开发期试过多个变体），因此只核对"动作名"这一层；
#: 其余步骤必须与记录的 `candidate_id` 逐字一致。每条变体都有具体来源：
#:   * `combat: 低血先格挡`     —— 旧战斗变体（低血先格挡），首差例：seed-G obs 5761 记录选 defend，当前规则选 bash
#:   * `combat: 先出攻击牌`     —— 旧战斗变体（同类里费用低者先），首差例：seed-B obs 1620 记录选 strike，当前规则选 bash
#:   * `combat: use potion `    —— 旧药水规则（没有 hp <= 一半的门槛），例：act1-potions-A3 step 73
#:   * `map: implemented room type preferred` —— 旧地图规则（只按房型偏好，无 Boss 连通性），例：smoke-6 step 1
LEGACY_REASON_PREFIXES = ("combat: 低血先格挡", "combat: 先出攻击牌", "combat: use potion ",
                          "map: implemented room type preferred")


def replay_policy_options(summary: dict) -> dict:
    """按**记录里的策略**重建复算用的固定规则（含商店选项），而不是用当前默认值。

    旧记录（没有商店）在 `summary.policy` 里没有 `shop_*` 键、`shop_room_entry` 也可能缺失，
    这里全部取安全默认（不进店、离店、不买不删），所以无商店记录的复算行为与改动前一致。
    """
    policy = summary.get("policy") or {}
    entry = (summary.get("shop_room_entry") or {}).get("enter_shop_nodes")
    return {
        "step_frontier": bool(policy.get("step_frontier")),
        "step_unsupported_room": bool(policy.get("step_unsupported_room")),
        "use_potions": bool(policy.get("use_potions")),
        "relic_policy": policy.get("relic_policy") or "skip",
        "potion_policy": policy.get("potion_policy") or "take",
        "shop_room_entry": bool(entry),
        "shop_policy": policy.get("shop_policy") or "leave",
        "shop_buy_kind": policy.get("shop_buy_kind") or "card",
        "shop_buy_slot": policy.get("shop_buy_slot"),
        "shop_remove": bool(policy.get("shop_remove")),
        "shop_remove_card": int(policy.get("shop_remove_card") or 0),
        "event_choice": policy.get("event_choice"),
        "shop_remove_card_id": policy.get("shop_remove_card_id"),
    }


def check_replayed_shop(recorder, checker: Checker, summary: dict) -> None:
    """复算出的商店事件与记录策略自洽（进入/店内动作/离开/退出、整条公开货架）。

    只做"记录自己怎么走，复算就怎么走"这一层：候选闭合由上面的逐步比对负责，规则本身仍由 base 拥有。
    """
    policy = summary.get("policy") or {}
    names = [event["event"] for event in recorder.events]
    entered = [event for event in recorder.events if event["event"] == "shop_enter"]
    if len(entered) > 1:
        # Validate the same invariants per visit, not once for the whole run.
        from types import SimpleNamespace
        visits = []
        for event in recorder.events:
            if event["event"] == "shop_enter":
                visits.append([])
            if visits:
                visits[-1].append(event)
        for visit in visits:
            check_replayed_shop(SimpleNamespace(events=visit), checker, summary)
        return
    exited = [event for event in recorder.events if event["event"] == "shop_exit"]
    if not entered:
        # 该记录没有走进商店（旧记录或 `--shop-room=skip`）：保持旧的"没有商店事件"断言。
        checker.check("replay: 整局没有任何商店事件（该记录没有商店）", not recorder.events,
                      json.dumps(names, ensure_ascii=False))
        return
    checker.check("replay: 商店进入/退出事件各一次且成对", len(entered) == len(exited) == 1,
                  f"events={names}")
    checker.check("replay: 商店事件顺序 shop_enter -> … -> shop_leave -> shop_exit",
                  names[0] == "shop_enter" and names[-1] == "shop_exit"
                  and "shop_leave" in names
                  and names.index("shop_leave") < names.index("shop_exit"),
                  f"events={names}")
    shelf = entered[0].get("shelf") or []
    slots = [row.get("fields", {}).get("Slot") for row in shelf]
    checker.check("replay: 进店公开货架 14 格且 Slot 覆盖 0..13",
                  len(shelf) == 14 and slots == list(range(14)), f"slots={slots}")
    kinds = [row.get("fields", {}).get("Kind") for row in shelf]
    checker.check("replay: 公开货架条目都带 Kind（含不可买/已售条目）",
                  all(isinstance(kind, str) and kind for kind in kinds), f"kinds={kinds}")
    unsupported = [row for row in shelf if row.get("fields", {}).get("Unsupported") is True]
    checker.check("replay: 不可买条目保留源身份与非空原因",
                  all((row["fields"].get("DefinitionId") or row["fields"].get("SourceName"))
                      and str(row["fields"].get("UnsupportedReason") or "").strip()
                      for row in unsupported),
                  json.dumps([row["fields"] for row in unsupported], ensure_ascii=False)[:300])
    if policy.get("shop_policy") == "buy":
        # 买不到时客户端**不重判**：只要求留下具名原因，不要求一定买到。
        checker.check("replay: 记录策略=buy -> 复算买到一件或如实记 shop_buy_unavailable",
                      sum(1 for name in names if name in ("shop_buy", "shop_buy_unavailable")) == 1,
                      f"events={names}")
    if policy.get("shop_remove"):
        checker.check("replay: 记录策略=remove -> 复算删一张或如实记 shop_remove_unavailable",
                      sum(1 for name in names if name in
                          ("shop_remove_request", "shop_remove_unavailable")) == 1,
                      f"events={names}")
        checker.check("replay: 提交删牌后一定有 answer 或明确未提交",
                      "shop_remove_request" not in names or "shop_remove_answer" in names,
                      f"events={names}")


def group_replay(checker: Checker, run_dir: pathlib.Path) -> bool:
    frames_path = run_dir / "frames.ndjson"
    steps_path = run_dir / "steps.ndjson"
    if not frames_path.is_file() or not steps_path.is_file():
        print(f"[test_shop_public] replay: 缺少 {frames_path} 或 {steps_path}")
        return False
    pairs = load_replayed_steps(frames_path)
    records = load_recorded_steps(steps_path)
    if not pairs:
        print(f"[test_shop_public] replay: {frames_path} 里没有可复算的 step 帧")
        return False
    summary_path = run_dir / "summary.json"
    recorded_summary, expected_stop = {}, None
    if summary_path.is_file():
        recorded_summary = json.loads(summary_path.read_text(encoding="utf-8"))
        expected_stop = (recorded_summary.get("stop") or {}).get("reason")
    options = replay_policy_options(recorded_summary)
    policy = FixedOrderPolicy(**options)
    print(f"[test_shop_public] replay: 记录策略 use_potions={options['use_potions']} "
          f"relic={options['relic_policy']} potion={options['potion_policy']} "
          f"shop_room_entry={options['shop_room_entry']} shop={options['shop_policy']}"
          f"{'/remove' if options['shop_remove'] else ''}")
    closed = equal = legacy_steps = legacy_action_differences = stops = terminal_steps = 0
    for index, (snapshot, (expected_observation_id, submitted_candidate)) in enumerate(pairs):
        if index >= len(records) or records[index][0] != expected_observation_id \
                or records[index][2] != submitted_candidate:
            recorded = records[index] if index < len(records) else None
            checker.check(f"replay: step#{index + 1} 的帧与步骤记录配对", False,
                          f"帧 expected={expected_observation_id}/candidate={submitted_candidate} 记录={recorded}")
            continue
        recorded_action, recorded_candidate, recorded_reason = records[index][1], records[index][2], records[index][3]
        observation = snapshot.get("observation_id")
        if snapshot.get("status") == "terminal":
            terminal_steps += 1
            continue
        legal = {item["candidate_id"] for item in snapshot.get("legal_actions") or []}
        choice = policy.choose(snapshot)
        case = f"replay: step#{index + 1} obs={observation} expected={expected_observation_id}"
        if isinstance(choice, Stop):
            stops += 1
            # 记录的运行本身就在这一步以同样的分类停止（例如 unsupported_map_room 开发边界）才算复现；
            # 中途出现 Stop 或分类不同都是回归。
            checker.check(f"{case} 停止分类与记录一致",
                          index == len(pairs) - 1 and expected_stop == choice.reason,
                          f"复算停止={choice.reason} 记录停止={expected_stop} pair={index + 1}/{len(pairs)}")
            continue
        closed += 1
        checker.check(f"{case} 候选闭合", choice.candidate["candidate_id"] in legal,
                      f"{choice.candidate['candidate_id']} 不在当前 legal_actions")
        legacy_step = recorded_reason.startswith(LEGACY_REASON_PREFIXES)
        if legacy_step:
            legacy_steps += 1
            # 旧变体的**选择**不能用来核对当前规则（连动作名都可能不同，例如旧药水规则会在
            # 当前规则出牌的地方用药水）；这里只核对记录本身自洽：记录的动作必须在那个快照里合法。
            legal_names = {item["action_type"].rsplit(":", 1)[-1] for item in snapshot.get("legal_actions") or []}
            recorded_name = (recorded_action or "").rsplit(".", 1)[-1]
            checker.check(f"{case} 旧规则变体步骤：记录动作在该快照里合法",
                          recorded_name in legal_names and choice.candidate["candidate_id"] in legal,
                          f"记录动作={recorded_action} 该快照合法动作={sorted(legal_names)}")
            if choice.action != recorded_name:
                legacy_action_differences += 1
            continue
        equal += 1
        checker.check(f"{case} 与记录提交逐字一致",
                      (choice.action, choice.candidate["candidate_id"]) == (recorded_action, recorded_candidate),
                      f"复算={choice.action}/{choice.candidate['candidate_id']} "
                      f"记录={recorded_action}/{recorded_candidate}")
    checker.check("replay: 覆盖了记录里的全部 step", closed + stops + terminal_steps == len(pairs),
                  f"closed={closed} stops={stops} terminal={terminal_steps} pairs={len(pairs)}")
    checker.check("replay: 只有记录最后一个非终局步骤可以停止（且分类一致）", stops <= 1, f"stops={stops}")
    check_replayed_shop(policy, checker, recorded_summary)
    print(f"[test_shop_public] replay: steps={len(pairs)} 复算={closed} 逐字一致={equal} "
          f"旧规则变体步骤={legacy_steps}（其中动作名就不同的 {legacy_action_differences}）")
    return True


# -- 入口 -----------------------------------------------------------------------------------------------

def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", choices=GROUPS, default=None, help="只跑一个分组")
    parser.add_argument("--run-dir", type=pathlib.Path, default=DEFAULT_RUN_DIR,
                        help="replay 组用的运行目录（含 frames.ndjson 与 steps.ndjson）")
    args = parser.parse_args(argv)

    selected = [args.only] if args.only else list(GROUPS)
    if "replay" in selected and not args.run_dir.is_dir():
        print(f"[test_shop_public] replay: --run-dir 不存在：{args.run_dir}")
        return 2

    checker = Checker()
    for group in selected:
        if group == "policy":
            group_policy(checker)
        elif group == "decision":
            group_decision(checker)
        elif group == "map":
            group_map(checker)
        elif group == "replay":
            if not group_replay(checker, args.run_dir):
                return 2
    checker.report("groups=" + ",".join(selected))
    return 1 if checker.failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
