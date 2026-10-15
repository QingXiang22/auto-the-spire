#!/usr/bin/env python3
"""ENV-T03 公开整局运行器：用公开 NDJSON 协议把一整局 Spire 跑到结论或开发边界。

它**只**用 `net_client.NetClient` 的公开面：`describe` / `get_catalog` / `start` / `step` / `close`，
一切决策都从**实时快照的公开 `legal_actions`** 里挑候选，候选带的是协议发布的 `candidate_id`。
工具**不**读 Engine 状态、**不**碰隐藏牌堆/随机数、**不**维护逐张牌白名单、**不**跨观测复用旧
`candidate_id`（每一步都要求候选出现在当前快照里，并用当前 `observation_id` 作为
`expected_observation_id`）。

固定决策顺序（可读、无评估函数、不是 AI）见 `run_game.md`；摘要：终局 → pending 选择
（`net:answer_decision`：战斗目标选择照旧"公开怪兽里最低 Hp 优先"；商店删牌的**长期牌组**
decision 按 `--shop-remove-card` / `--shop-remove-card-id` 指定的公开实例回答）→ 幕完成边界 →
通用事件（公开 MapPhase=8/CurrentEventId，当前选项稳定排序或 --event-choice 唯一匹配）→
商店 → 地图选点（Unknown/Elite 只提交当前候选，内容由 Host 决定）→ 奖励 → 宝箱 → 营火 →
开发切片推进 → 战斗（先出攻击牌、同一类里公开 Amount 大的先；`--use-potions` 时只在公开 Hp
不高于一半时用药水）→ 都认不出就**精确停在未支持边界**。

商店同样只用公开契约：`ShopOffers` 序列上的五个公开动作（`BuyShopCard` / `BuyShopRelic` /
`BuyShopPotion` / `RemoveShopCard` / `LeaveShop`），以及每个候选自带的 `Offer` 实体公开字段
（`Slot/Kind/DefinitionId/SourceName/Rarity/OnSale/Price/Sold/Unsupported/UnsupportedReason`）。
买不买得起、能不能删、有没有容量**都由 Engine 的候选域与守卫决定，客户端不重判规则**；客户端只按
公开 `Price`/`Slot` 做确定性挑选，商品字段缺失时记录可复查原因并停下，不猜值、不清空货架。
`--shop-room {auto,enter,skip}` 只控制**地图是否走进 Shop 节点**：`auto`（默认）由宿主公开目录
声明的商店动作决定（未声明 = 保持原来的"选之前停在 `unsupported_map_room`"边界，不改整局状态）。

输出（`--output` 目录）：

* `frames.ndjson` —— 双向原始 NDJSON 帧（含**完整** snapshot），逐行 `{seq, dir, frame}`；
* `steps.ndjson`  —— 每一步的语义操作 + 该步之后的实时公开状态视图 + 全部公开候选；
* `summary.json`  —— 最终结论：状态、终止结果、身份、真实 seed/scope、各房间长期状态、退出码。

退出码：0 = 整局结论（Victory / Death / 其它终局）；3 = 未到整局结论（幕完成开发边界 / 步数上限）；
4 = 故障或未支持边界（未支持动作、未接线房型、无合法动作、宿主拒绝、卡住、结果不确定）；
1 = 驱动器自身错误。
**幕完成、步数上限、故障都不得当成整局通过**：`summary.run_complete` 只在这三者都不成立且拿到
`win/loss/other` 终局时为 `true`。
"""

from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import os
import pathlib
import sys
import time
from typing import Any, NamedTuple, Optional

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

from net_client import NetClient, NetClientError, OutcomeUnknown, host_command  # noqa: E402
from public_snapshot import (  # noqa: E402
    field, public_fields, flag, short, action_name, candidate_fields, first_field_value,
    entities_of, entity_of, find_entity, sequence, pile_visible, all_known, relation_targets,
)

from event_policy import choose_event, event_access  # noqa: E402

FILE_VERSION = "run_game.py/2026-10-13-event"

# -- 退出码 ---------------------------------------------------------------------------------------------
EXIT_TERMINAL = 0        # 拿到整局终局（win/loss/other）
EXIT_DRIVER_ERROR = 1    # 参数、身份、协议信封等驱动器侧问题
EXIT_NOT_CONCLUSIVE = 3  # 开发边界（幕完成）或步数上限：不是整局通过，也不是故障
EXIT_FAULT = 4           # 故障/未支持：宿主拒绝、无合法动作、卡住、结果不确定

TERMINAL_STATUS = {
    "win": "victory",
    "loss": "death",
    "other": "terminal_other",
}
#: 未到整局结论、但也不是故障的停止原因。
NON_CONCLUSIVE_STOPS = ("act_completed_dev_boundary", "step_limit")

#: 客户端可消费的公开地图类型；不保证宿主对应内容全部实现。
#: Unknown 的实际房型由 Host 判定；Elite 候选可提交，战斗仍消费同一公开接口。
IMPLEMENTED_MAP_TYPES = ("Monster", "Boss", "Treasure", "RestSite", "Campfire", "Rest", "Unknown", "Elite")
KIND_RANK = {"Attack": 0, "Skill": 1, "Power": 2}

# -- 商店公开契约（只读消费；动作名/集合名对齐 base 侧 ShopRoomWiring 的公开常量） --------------------------
#: `--shop-buy-kind` → 公开动作名。三个购买动作各只发布自己那类可买商品（Engine 候选域已过滤：已售 /
#: 不可买 / 买不起 / 满药水栏都不在候选里），因此客户端不需要、也不得自己重判这些规则。
SHOP_BUY_ACTIONS = {"card": "BuyShopCard", "relic": "BuyShopRelic", "potion": "BuyShopPotion"}
SHOP_REMOVE_ACTION = "RemoveShopCard"
SHOP_LEAVE_ACTION = "LeaveShop"
SHOP_ACTION_NAMES = tuple(SHOP_BUY_ACTIONS.values()) + (SHOP_REMOVE_ACTION, SHOP_LEAVE_ACTION)
#: 公开货架：`ShopOffers` 序列（owner = Run），元素是 `ShopOffer` 实体。
SHOP_OFFERS_SEQUENCE = "ShopOffers"
#: 商店相位（`Run.MapPhase=7`，与 `MapRoomWiring.ShopPhase` 一致；不是 6 的幕完成）。
SHOP_PHASE = 7
#: Run 上的累计删牌次数（`ShopCardRemovalsUsed`；只读，用来在证据里说明涨价）。
SHOP_REMOVALS_USED_FIELD = "ShopCardRemovalsUsed"
#: 挑选商品前必须"公开已知"的字段；缺任何一个都记原因并跳过该候选，不猜值。
OFFER_REQUIRED_FIELDS = ("Slot", "Kind", "Price", "Sold", "Unsupported")
#: 只用于证据/诊断的字段，缺失也照样可以按公开顺序决策。
OFFER_OPTIONAL_FIELDS = ("DefinitionId", "SourceName", "Rarity", "PoolKind", "PoolIndex",
                         "OnSale", "UnsupportedReason")


def _known(fields: dict, name: str) -> bool:
    """公开字段是否 `known`（`public_fields` 用 `{"state": ...}` 表示未公开）。"""
    return name in fields and not isinstance(fields.get(name), dict)


def shop_declared_actions(catalog_result: Optional[dict]) -> dict:
    """从公开目录（`get_catalog` 的 `result`）里读商店动作声明；不猜、不降级。

    `auto` 用它在**进入 Shop 节点之前**判断宿主是否真的接线了商店：目录没有声明商店动作的宿主
    （例如本包核对的本机固定 Host）保持原来的 `unsupported_map_room` 边界，不改整局状态。
    """
    action_types = []
    declarations = (catalog_result or {}).get("declarations") or {}
    for item in declarations.get("action_types") or []:
        identifier = item.get("id") if isinstance(item, dict) else None
        if isinstance(identifier, str):
            action_types.append(identifier)
    present = [name for name in SHOP_ACTION_NAMES
               if any(identifier.endswith(":" + name) for identifier in action_types)]
    missing = [name for name in SHOP_ACTION_NAMES if name not in present]
    return {"declared_action_types": len(action_types), "declared_shop_actions": present,
            "missing_shop_actions": missing, "shop_actions_declared": not missing}


# -- 公开字段读取 ---------------------------------------------------------------------------------------

# -- 决策 -----------------------------------------------------------------------------------------------

class Decision(NamedTuple):
    candidate: dict
    action: str
    reason: str


class Stop(NamedTuple):
    reason: str
    detail: str


class FixedOrderPolicy:
    """可读的固定决策顺序；只用公开字段与公开候选，不看隐藏信息。"""

    def __init__(self, *, step_frontier: bool, relic_policy: str, potion_policy: str,
                 use_potions: bool, step_unsupported_room: bool = False,
                 shop_room_entry: bool = False, shop_policy: str = "leave",
                 shop_buy_kind: str = "card", shop_buy_slot: Optional[int] = None,
                 shop_remove: bool = False, shop_remove_card: int = 0,
                 shop_remove_card_id: Optional[str] = None,
                 event_choice: Optional[str] = None) -> None:
        self.step_frontier = step_frontier
        self.relic_policy = relic_policy
        self.potion_policy = potion_policy
        self.use_potions = use_potions
        self.step_unsupported_room = step_unsupported_room
        self.act_completed_seen = False
        # 商店：进入许可由宿主公开目录决定（`--shop-room`），店内动作由显式 CLI 决定（默认安全离店）。
        self.shop_room_entry = shop_room_entry
        self.steppable_map_types = (IMPLEMENTED_MAP_TYPES + ("Shop",) if shop_room_entry
                                    else IMPLEMENTED_MAP_TYPES)
        self.shop_policy = shop_policy
        self.shop_buy_kind = shop_buy_kind
        self.shop_buy_slot = shop_buy_slot
        self.shop_remove = shop_remove
        self.shop_remove_card = shop_remove_card
        self.shop_remove_card_id = shop_remove_card_id
        # 一次商店访问内的一次性状态：只买一件 / 只删一张，避免重复购买或售罄后重买。
        self.shop_visit_open = False
        self.shop_bought = False
        self.shop_removed = False
        self.shop_removal_pending = False
        self.events: list = []
        self.event_choice = event_choice
        self.event_events: list = []
        self.event_visit = None

    # -- 小工具 -----------------------------------------------------------------------------------------
    def _map_type(self, entities: dict, ref: Optional[str]) -> Optional[str]:
        node = entities.get(ref) if ref else None
        for name in ("NodeType", "Kind", "RoomType", "Type", "Symbol"):
            value = field(node, name)
            if isinstance(value, str) and value:
                return short(value)
        return None

    def _map_children(self, snapshot: dict) -> dict:
        """公开地图边：快照的 `MapChildren` relations（source_ref → [target_ref...]），不看任何隐藏信息。"""
        return relation_targets(snapshot, "MapChildren")

    def _map_reaches_boss(self, entities: dict, children: dict, start: str) -> bool:
        """在公开地图图上从 start 出发、只经过"已接线"房型，能否走到 Boss。

        这是对公开可见地图（MapNode.Type + MapChildren）的连通性判断，不是隐藏池/随机数窥看。
        """
        seen = {start}
        stack = [start]
        while stack:
            ref = stack.pop()
            kind = self._map_type(entities, ref)
            if kind == "Boss":
                return True
            if kind not in self.steppable_map_types:
                continue
            for nxt in children.get(ref) or []:
                if nxt not in seen:
                    seen.add(nxt)
                    stack.append(nxt)
        return False

    def _map_decision(self, snapshot: dict) -> Any:
        offered = [c for c in snapshot.get("legal_actions") or [] if action_name(c) == "ChooseMapPoint"]
        if not offered:
            return None
        entities = entities_of(snapshot)
        run = find_entity(snapshot, "Run")
        hp = field(run, "Hp")
        max_hp = field(run, "MaxHp")
        children = self._map_children(snapshot)
        scored = []
        for index, cand in enumerate(offered):
            target = first_field_value(cand)
            kind = self._map_type(entities, target)
            # 稳定偏好：能从公开字段看出房型时，优先"已接线"的房型；看不出就保持宿主给的公开顺序。
            if kind in self.steppable_map_types:
                rank = self.steppable_map_types.index(kind)
                viable = self._map_reaches_boss(entities, children, target)
            else:
                rank = 5 if kind is None else 9
                viable = False
            # 血量偏低时先走营火补血（公开 Hp/MaxHp，不看任何隐藏信息）。
            if (isinstance(hp, int) and isinstance(max_hp, int) and max_hp > 0
                    and hp * 2 < max_hp and kind in ("RestSite", "Campfire", "Rest")):
                rank = -1
            scored.append((0 if viable else 1, rank, index, cand, kind, target, viable))
        scored.sort(key=lambda t: t[:3])
        _, rank, _, cand, kind, target, viable = scored[0]
        offered_kinds = sorted({self._map_type(entities, first_field_value(c)) for c in offered})
        # 只有未实现房型可选 = 当前开发边界：默认精确停在这里，不退化成"随便挑一个撞失败"。
        if not viable and rank >= 9 and not self.step_unsupported_room:
            return Stop("unsupported_map_room",
                        f"当前地图节点的可选房型都尚未接线：offered={offered_kinds}；"
                        f"已接线集合={list(self.steppable_map_types)}"
                        f"（Shop 是否进店由 --shop-room 决定）。地图节点保留、状态不变；"
                        f"用 --step-unsupported-room 可实地推进一次以取得宿主的具名失败")
        note = ("map: 公开地图上可走到 Boss 的已接线房型优先"
                if viable else
                "map: 没有能走到 Boss 的已接线房型；按固定房型偏好推进（可能停在开发边界）")
        return Decision(cand, "ChooseMapPoint",
                        f"{note} (kind={kind}, target={target}, viable={viable}, "
                        f"hp={hp}/{max_hp}, offered={offered_kinds})")

    # -- 商店（只消费公开货架与公开候选；不重判规则、不猜字段、不硬编码商品/候选 ID） -------------------------
    def _offer_row(self, entities: dict, ref: Any) -> dict:
        """一个 `ShopOffer` 的公开事实；必需字段缺失时如实标出缺口（不清空、不补默认值）。"""
        entity = entities.get(ref) if isinstance(ref, str) else None
        fields = public_fields(entity)
        return {"ref": ref,
                "content_id": entity.get("content_id") if entity else None,
                "fields": fields,
                "missing_required": [name for name in OFFER_REQUIRED_FIELDS if not _known(fields, name)],
                "missing_optional": [name for name in OFFER_OPTIONAL_FIELDS if not _known(fields, name)]}

    def _shop_shelf(self, snapshot: dict) -> list:
        """公开货架：`ShopOffers` 序列按发布顺序的每个商品（含不可买/已售条目）。"""
        entities = entities_of(snapshot)
        return [self._offer_row(entities, ref) for ref in sequence(snapshot, SHOP_OFFERS_SEQUENCE)]

    def _pick_offer(self, entities: dict, offers: list, *, slot_filter: Optional[int] = None) -> dict:
        """在一组公开候选里按 `(公开 Price, 公开 Slot, 公开顺序)` 确定性挑一个。

        跳过 `Sold=true`（已售）与 `Unsupported=true`（不可买）的候选；必需字段未知的候选跳过并留下
        可复查原因。返回 `{"chosen", "skipped", "stop"}`：一个都挑不出时 `chosen=None`。
        """
        scored, skipped = [], []
        for index, cand in enumerate(offers):
            row = self._offer_row(entities, first_field_value(cand))
            row["candidate_id"] = cand.get("candidate_id")
            if row["missing_required"]:
                skipped.append({**row, "skip": "必需公开字段缺失（不猜值）"})
                continue
            if row["fields"].get("Sold") is True:
                skipped.append({**row, "skip": "公开 Sold=true（已售，不重买）"})
                continue
            if row["fields"].get("Unsupported") is True:
                skipped.append({**row, "skip": "公开 Unsupported=true（不可买，原因见 UnsupportedReason）"})
                continue
            price = row["fields"].get("Price")
            slot = row["fields"].get("Slot")
            if not isinstance(price, int) or not isinstance(slot, int):
                skipped.append({**row, "skip": "公开 Price/Slot 不是整数（不猜值）"})
                continue
            if slot_filter is not None and slot != slot_filter:
                skipped.append({**row, "skip": f"不是 --shop-buy-slot={slot_filter} 指定的公开 Slot"})
                continue
            scored.append(((price, slot, index), cand, row))
        if not scored:
            return {"chosen": None, "skipped": skipped,
                    "stop": ("公开候选里没有可挑的商品："
                             + ("；".join(f"{row.get('candidate_id')}: {row['skip']}" for row in skipped)
                                if skipped else "候选为空（Engine 候选域里没有该类可买商品）"))}
        scored.sort(key=lambda item: item[0])
        key, cand, row = scored[0]
        return {"chosen": {"candidate": cand, "offer": row, "key": list(key)},
                "skipped": skipped, "stop": None}

    def _shop_buy(self, entities: dict, candidates: list, kind: str) -> Any:
        action = SHOP_BUY_ACTIONS[kind]
        picked = self._pick_offer(entities, candidates, slot_filter=self.shop_buy_slot)
        if picked["chosen"] is None:
            self.events.append({"event": "shop_buy_unavailable", "action": action, "detail": picked["stop"]})
            return None
        chosen = picked["chosen"]
        self.shop_bought = True
        offer = chosen["offer"]
        self.events.append({"event": "shop_buy", "action": action, "offer": offer,
                            "candidate_id": chosen["candidate"].get("candidate_id"),
                            "skipped": picked["skipped"]})
        return Decision(chosen["candidate"], action,
                        f"shop: --shop-policy=buy 买一件公开 {kind}（确定性：公开 Price/Slot 最小者；"
                        f"slot={offer['fields'].get('Slot')}, price={offer['fields'].get('Price')}, "
                        f"kind={offer['fields'].get('Kind')}, def={offer['fields'].get('DefinitionId')}, "
                        f"source={offer['fields'].get('SourceName')}, onSale={offer['fields'].get('OnSale')}）；"
                        f"跳过 {len(picked['skipped'])} 个候选（已售/不可买/字段缺失），买不起与容量由 Engine 决定")

    def _shop_remove_request(self, entities: dict, candidates: list) -> Any:
        picked = self._pick_offer(entities, candidates)
        if picked["chosen"] is None:
            self.events.append({"event": "shop_remove_unavailable", "detail": picked["stop"]})
            return None
        chosen = picked["chosen"]
        offer = chosen["offer"]
        # 提交后 Engine 会暂停在 EntityRef decision 上问"删哪张长期牌"；用 pending 标记把它与战斗目标
        # 选择区分开，`_answer_decision` 只在这个标记下走删牌分支。
        self.shop_removal_pending = True
        self.events.append({"event": "shop_remove_request", "offer": offer,
                            "candidate_id": chosen["candidate"].get("candidate_id"),
                            "skipped": picked["skipped"]})
        return Decision(chosen["candidate"], SHOP_REMOVE_ACTION,
                        f"shop: --shop-remove 删除一张（删牌位公开 price="
                        f"{offer['fields'].get('Price')}, slot={offer['fields'].get('Slot')}）；"
                        f"回答随后的 RemoveCardChoicePrompt/EntityRef decision 时再公开选具体长期牌实例")

    def _deck_listing(self, snapshot: dict, entities: dict) -> list:
        refs = sequence(snapshot, "LongTermDeck")
        return [{"index": index, "ref": ref,
                 "kind_id": (entities.get(ref) or {}).get("kind_id"),
                 "content_id": (entities.get(ref) or {}).get("content_id"),
                 "definition_id": field(entities.get(ref), "DefinitionId")}
                for index, ref in enumerate(refs)]

    def _shop_removal_decision(self, snapshot: dict, entities: dict, candidates: list) -> Any:
        """删牌 decision：候选必须是公开 `LongTermDeck` 里的真实实例，按 CLI 指定挑具体那一个。"""
        deck = self._deck_listing(snapshot, entities)
        deck_refs = [row["ref"] for row in deck]
        refs = [first_field_value(cand) for cand in candidates]
        listing = [f"{row['index']}:{row['content_id'] or row['definition_id'] or row['ref']}" for row in deck]
        if not refs or not all(isinstance(ref, str) and ref in set(deck_refs) for ref in refs):
            return Stop("shop_removal_decision_domain_mismatch",
                        "商店删牌的 decision 候选不是公开 LongTermDeck 的实例："
                        f"decision_candidates={refs}，公开 LongTermDeck={listing}；"
                        "不把删牌 decision 与营火升级/战斗目标选择混淆，停在未支持边界")
        if self.shop_remove_card_id is not None:
            wanted = short(self.shop_remove_card_id)

            def card_id(ref: Any) -> str:
                return short((entities.get(ref) or {}).get("content_id")
                             or (field(entities.get(ref), "DefinitionId") or ""))

            # 短名精确匹配，或允许省略 "-" 之后的后缀（如 strike → strike-ironclad）；多张命中取公开顺序第一张。
            picks = [index for index, ref in enumerate(refs)
                     if card_id(ref) == wanted or card_id(ref).startswith(wanted + "-")]
            if not picks:
                return Stop("shop_removal_card_not_found",
                            f"--shop-remove-card-id={self.shop_remove_card_id} 不在当前公开候选里："
                            f"候选={[str(r) for r in refs]}；公开 LongTermDeck={listing}")
            chosen_index = picks[0]
        else:
            chosen_index = self.shop_remove_card
            if chosen_index >= len(candidates) or chosen_index < 0:
                return Stop("shop_removal_index_out_of_range",
                            f"--shop-remove-card={self.shop_remove_card} 超出当前 decision 候选数"
                            f"（{len(candidates)}）；公开 LongTermDeck={listing}")
        cand = candidates[chosen_index]
        ref = refs[chosen_index]
        row = next((item for item in deck if item["ref"] == ref), None)
        self.shop_removal_pending = False
        self.shop_removed = True
        self.events.append({"event": "shop_remove_answer", "index": chosen_index, "ref": ref,
                            "card": row, "candidates": len(candidates), "matched": len(picks) if
                            self.shop_remove_card_id is not None else 1, "deck": listing})
        return Decision(cand, "net.answer_decision",
                        f"shop removal decision: 公开选择长期牌实例 index={chosen_index}"
                        f"（deck_position={row['index'] if row else None}, "
                        f"card={row['content_id'] if row else None}, 候选数={len(candidates)}）；"
                        f"公开 LongTermDeck={listing}")

    def _shop_decision(self, snapshot: dict, by_name: dict, run: Any) -> Any:
        """商店相位（`MapPhase=7`）内的固定顺序：买一件（可选）→ 删一张（可选）→ 离店。"""
        entities = entities_of(snapshot)
        if not self.shop_visit_open:
            self.shop_visit_open = True
            shelf = self._shop_shelf(snapshot)
            self.events.append({"event": "shop_enter", "phase": field(run, "MapPhase"),
                                "gold": field(run, "Gold"),
                                "removals_used": field(run, SHOP_REMOVALS_USED_FIELD),
                                "shelf": shelf,
                                "shelf_note": None if shelf else
                                f"公开 {SHOP_OFFERS_SEQUENCE} 序列在本快照里没有元素：不清空、不猜，"
                                f"只按当前 legal_actions={sorted(by_name)} 决策",
                                "policy": {"shop_policy": self.shop_policy,
                                           "shop_buy_kind": self.shop_buy_kind,
                                           "shop_buy_slot": self.shop_buy_slot,
                                           "shop_remove": self.shop_remove,
                                           "shop_remove_card": self.shop_remove_card,
                                           "shop_remove_card_id": self.shop_remove_card_id}})
        if self.shop_policy == "buy" and not self.shop_bought:
            decision = self._shop_buy(entities, by_name.get(SHOP_BUY_ACTIONS[self.shop_buy_kind]) or [],
                                      self.shop_buy_kind)
            if decision is not None:
                return decision
        if self.shop_remove and not self.shop_removed:
            decision = self._shop_remove_request(entities, by_name.get(SHOP_REMOVE_ACTION) or [])
            if decision is not None:
                return decision
        if SHOP_LEAVE_ACTION in by_name:
            cand = by_name[SHOP_LEAVE_ACTION][0]
            self.events.append({"event": "shop_leave", "gold": field(run, "Gold"),
                                "removals_used": field(run, SHOP_REMOVALS_USED_FIELD),
                                "bought": self.shop_bought, "removed": self.shop_removed,
                                "unused_shop_actions": sorted(name for name in by_name
                                                              if name in SHOP_ACTION_NAMES
                                                              and name != SHOP_LEAVE_ACTION)})
            return Decision(cand, SHOP_LEAVE_ACTION,
                            f"shop: --shop-policy={self.shop_policy}"
                            f"{'/--shop-remove' if self.shop_remove else ''} 处理完毕 -> LeaveShop 回地图"
                            f"（gold={field(run, 'Gold')}, bought={self.shop_bought}, "
                            f"removed={self.shop_removed}, "
                            f"removals_used={field(run, SHOP_REMOVALS_USED_FIELD)}）")
        return Stop("shop_no_decision",
                    "商店相位里既没有可执行的显式策略，也没有 LeaveShop："
                    + json.dumps({"actions": {name: [c["candidate_id"] for c in by_name[name]]
                                              for name in sorted(by_name)},
                                  "fields": {name: candidate_fields(by_name[name][0])
                                             for name in sorted(by_name)}}, ensure_ascii=False))

    def _answer_decision(self, snapshot: dict, candidates: list) -> Any:
        """回答 pending 选择（`net:answer_decision`）：所有答案都是公开实体引用。

        固定规则：**商店删牌**的 decision（候选全部是公开 `LongTermDeck` 实例，且上一步刚提交过
        `RemoveShopCard`）按 CLI 指定的具体长期实例回答；其它选择保持原规则——目标是怪兽时选
        **公开 Hp 最低**的那只（同 Hp 保持宿主公开顺序），其他目标保持公开顺序。
        """
        entities = entities_of(snapshot)
        if self.shop_removal_pending:
            return self._shop_removal_decision(snapshot, entities, candidates)
        scored = []
        for index, cand in enumerate(candidates):
            ref = first_field_value(cand)
            target = entities.get(ref)
            kind = short(target.get("kind_id")) if target else None
            hp = field(target, "Hp")
            rank = (0, hp if isinstance(hp, int) else 10 ** 6) if kind == "Monster" else (1, 0)
            scored.append((rank, index, cand, ref, kind, hp))
        scored.sort(key=lambda t: (t[0], t[1]))
        _, _, cand, ref, kind, hp = scored[0]
        return Decision(cand, "net.answer_decision",
                        f"pending decision: 公开目标里优先最低 Hp（kind={kind}, hp={hp}, target={ref}）")

    def _has_upgradable(self, snapshot: dict) -> bool:
        entities = entities_of(snapshot)
        for ref in sequence(snapshot, "LongTermDeck"):
            card = entities.get(ref)
            if card is not None and not flag(card, "Upgraded"):
                return True
        return False

    def _combat_decision(self, snapshot: dict) -> Optional[Decision]:
        legal = snapshot.get("legal_actions") or []
        by_name: dict = {}
        for cand in legal:
            by_name.setdefault(action_name(cand), []).append(cand)
        player = find_entity(snapshot, "Player")
        run = find_entity(snapshot, "Run")
        energy = field(player, "Energy")
        hp = field(player, "Hp")
        max_hp = field(player, "MaxHp")
        if not isinstance(hp, int):
            hp = field(run, "Hp")
        if not isinstance(max_hp, int):
            max_hp = field(run, "MaxHp")
        entities = entities_of(snapshot)

        # 药水只在 --use-potions 时使用，且只挑公开声明 UseSupported=true 的瓶子；
        # 只在公开 Hp 不高于一半时用（固定规则：不在满血乱花药水）。
        if (self.use_potions and isinstance(hp, int) and isinstance(max_hp, int) and max_hp > 0
                and hp * 2 <= max_hp):
            for cand in by_name.get("UsePotion", []):
                ref = first_field_value(cand)
                bottle = entities.get(ref)
                if flag(bottle, "UseSupported"):
                    return Decision(cand, "UsePotion",
                                    f"combat: hp {hp}/{max_hp} 不高于一半，用公开声明 UseSupported=true 的药水 {ref}")

        # 固定规则：先出攻击牌，同类里公开 Amount 大的先。实测（见 .audit 证据）把"低血先格挡"或
        # "费用低者先"换成这条并不改变存活（三个变体都死在同一个 floor 7 的 jaxfruit 战斗），
        # 所以保留最简单的一条，不做任何接近 AI 的评估。
        playable = []
        for index, cand in enumerate(by_name.get("PlayCard", [])):
            ref = first_field_value(cand)
            card = entities.get(ref)
            cost = field(card, "Cost")
            modifier = field(card, "CostModifier")
            effective = cost + modifier if isinstance(cost, int) and isinstance(modifier, int) else cost
            kind = field(card, "Kind")
            amount = field(card, "Amount")
            if field(card, "Unplayable") is True or flag(card, "Unplayable"):
                continue
            if isinstance(effective, int) and isinstance(energy, int) and effective > energy:
                continue
            playable.append((
                KIND_RANK.get(short(kind) if isinstance(kind, str) else "", 5),
                -(amount if isinstance(amount, int) else 0),
                effective if isinstance(effective, int) else 99,
                not all_known(cand),
                index,
                cand,
                ref,
                effective,
                short(kind) if isinstance(kind, str) else None,
                amount,
            ))
        if playable:
            playable.sort(key=lambda t: t[:5])
            _, _, _, _, _, cand, ref, effective, kind, amount = playable[0]
            return Decision(cand, "PlayCard",
                            f"combat: 先攻击、同类里公开 Amount 大的先 "
                            f"(card={ref}, cost={effective}, kind={kind}, amount={amount}, "
                            f"hp={hp}/{max_hp}, energy={energy})")

        if "EndTurn" in by_name:
            return Decision(by_name["EndTurn"][0], "EndTurn",
                            f"combat: no playable card affordable (energy={energy}) -> EndTurn")
        return None

    # -- 主选择 -----------------------------------------------------------------------------------------
    def choose(self, snapshot: dict) -> Any:
        legal = snapshot.get("legal_actions") or []
        if not legal:
            return Stop("no_legal_actions", "legal_actions 为空且状态不是 terminal：没有可推进的公开动作")

        by_name: dict = {}
        for cand in legal:
            by_name.setdefault(action_name(cand), []).append(cand)
        run = find_entity(snapshot, "Run")
        phase, event_id = event_access(snapshot)
        visit = (field(run, "CurrentNode"), event_id) if phase == 8 and event_id else None
        if self.event_visit is not None and visit != self.event_visit:
            self.event_events.append({"event": "event_exit", "visit": list(self.event_visit),
                                      "phase": phase, "current_event_id": event_id,
                                      "hp": field(run, "Hp"), "gold": field(run, "Gold")})
            self.event_visit = None
        if visit is not None and self.event_visit != visit:
            self.event_visit = visit
            self.event_events.append({"event": "event_enter", "visit": list(visit),
                                      "observation_id": snapshot.get("observation_id"),
                                      "hp": field(run, "Hp"), "gold": field(run, "Gold")})
        # pending 选择会独占候选（此时只有 answer_decision），必须先回答它。
        if "net.answer_decision" in by_name:
            return self._answer_decision(snapshot, by_name["net.answer_decision"])
        if flag(run, "ActCompleted") or "ContinueToNextAct" in by_name:
            self.act_completed_seen = True
            if not self.step_frontier:
                return Stop("act_completed_dev_boundary",
                            "Run.ActCompleted=true 且公开候选只剩 ContinueToNextAct：本构建的幕转换未接线，"
                            "这不是整局 Victory（用 --step-frontier 可以实地取回宿主的具名失败）")
            return Decision(by_name["ContinueToNextAct"][0], "ContinueToNextAct",
                            "frontier probe: --step-frontier 明确要求实地推进未接线的幕转换")

        # 商店：公开相位 7（`MapRoomWiring.ShopPhase`）或候选里出现商店动作时，交给显式商店策略；
        # 相位一离开商店就记录这次访问的收尾（长期状态变化另由 summary.rooms[] 逐步记录）。
        in_shop = (field(run, "MapPhase") == SHOP_PHASE
                   or any(name in by_name for name in SHOP_ACTION_NAMES))
        if self.shop_visit_open and not in_shop:
            self.shop_visit_open = False
            self.events.append({"event": "shop_exit", "phase": field(run, "MapPhase"),
                                "gold": field(run, "Gold"),
                                "removals_used": field(run, SHOP_REMOVALS_USED_FIELD),
                                "bought": self.shop_bought, "removed": self.shop_removed})
            self.shop_bought = False
            self.shop_removed = False
            self.shop_removal_pending = False
        # Event dispatch follows visit cleanup, but precedes other room actions.
        event = choose_event(snapshot, self.event_choice)
        if event is not None:
            if "stop" in event:
                return Stop(event["stop"], event["detail"])
            cand = event["candidate"]
            self.event_events.append({"event": "event_option", **event["evidence"]})
            return Decision(cand, action_name(cand),
                            "event: " + json.dumps(event["evidence"], ensure_ascii=False))
        if in_shop:
            return self._shop_decision(snapshot, by_name, run)

        map_choice = self._map_decision(snapshot)
        if map_choice is not None:
            return map_choice

        # 奖励阶段：先领金币/卡牌/药水，领不了就按公开顺序跳过（含药水满栏时的跳过）。
        if any(name in by_name for name in ("TakeRewardGold", "TakeRewardCard", "TakeRewardPotion",
                                            "SkipRewardCard", "SkipRewardPotion")):
            preference = ["TakeRewardGold", "TakeRewardCard",
                          "TakeRewardPotion" if self.potion_policy == "take" else None,
                          "SkipRewardPotion", "SkipRewardCard"]
            for name in preference:
                if name and name in by_name:
                    return Decision(by_name[name][0], name,
                                    f"reward: {name}（取不到才跳过，卡牌只取公开顺序第一张，不评估牌面价值）")

        if "OpenTreasureChest" in by_name:
            return Decision(by_name["OpenTreasureChest"][0], "OpenTreasureChest",
                            "treasure: open the chest first (公开金币/遗物候选)")
        if "TakeTreasureRelic" in by_name or "SkipTreasureRelic" in by_name:
            if self.relic_policy == "take" and "TakeTreasureRelic" in by_name:
                return Decision(by_name["TakeTreasureRelic"][0], "TakeTreasureRelic",
                                "treasure(--relic-policy=take): 尝试领取公开遗物候选；不支持的效果会由宿主具名拒绝")
            if "SkipTreasureRelic" in by_name:
                return Decision(by_name["SkipTreasureRelic"][0], "SkipTreasureRelic",
                                "treasure(--relic-policy=skip): 公开字段无法证明遗物效果已支持，跳过")
            return Stop("unsupported_action",
                        "宝箱只剩 TakeTreasureRelic 且 --relic-policy=skip：不猜遗物支持性，停在未支持边界")

        if "RestAtCampfire" in by_name or "SmithAtCampfire" in by_name:
            hp = field(run, "Hp")
            max_hp = field(run, "MaxHp")
            needs_heal = isinstance(hp, int) and isinstance(max_hp, int) and hp < max_hp
            if needs_heal and "RestAtCampfire" in by_name:
                return Decision(by_name["RestAtCampfire"][0], "RestAtCampfire",
                                f"campfire: hp {hp}/{max_hp} < max -> Rest")
            if "SmithAtCampfire" in by_name and self._has_upgradable(snapshot):
                return Decision(by_name["SmithAtCampfire"][0], "SmithAtCampfire",
                                "campfire: hp 已满且有可升级长期牌 -> Smith")
            if "RestAtCampfire" in by_name:
                return Decision(by_name["RestAtCampfire"][0], "RestAtCampfire",
                                f"campfire: 没有可升级长期牌 -> Rest (hp {hp}/{max_hp})")
            return Stop("unsupported_action",
                        "营火只剩 SmithAtCampfire 但公开 LongTermDeck 里没有 Upgraded=false 的牌")
        if "UpgradeLongTermCard" in by_name:
            entities = entities_of(snapshot)
            for cand in by_name["UpgradeLongTermCard"]:
                ref = first_field_value(cand)
                card = entities.get(ref)
                if card is None or not flag(card, "Upgraded"):
                    return Decision(cand, "UpgradeLongTermCard",
                                    f"upgrade: 选公开顺序里第一张未升级长期牌（card={ref}）")
            if "EnterNextRoom" in by_name:
                return Decision(by_name["EnterNextRoom"][0], "EnterNextRoom",
                                "upgrade: 没有可升级的牌 -> EnterNextRoom")
            return Stop("unsupported_action",
                        "UpgradeLongTermCard 的公开候选都指向已升级的牌且没有 EnterNextRoom")

        if "EnterNextRoom" in by_name:
            return Decision(by_name["EnterNextRoom"][0], "EnterNextRoom",
                            "dev slice: 切片占位流程 -> EnterNextRoom")

        combat = self._combat_decision(snapshot)
        if combat is not None:
            return combat

        names = sorted(by_name)
        return Stop("unsupported_action",
                    "当前公开候选没有已知语义：" + json.dumps({
                        "actions": {n: [c["candidate_id"] for c in by_name[n]] for n in names},
                        "fields": {n: candidate_fields(by_name[n][0]) for n in names},
                    }, ensure_ascii=False))


# -- 观测视图 -------------------------------------------------------------------------------------------

def state_view(snapshot: dict) -> dict:
    """每一步的实时公开状态视图（完整快照在 frames.ndjson 里）。"""
    entities = entities_of(snapshot)
    run = find_entity(snapshot, "Run")
    player = find_entity(snapshot, "Player")
    monsters = []
    for ref in sequence(snapshot, "Monsters"):
        monster = entities.get(ref)
        monsters.append({"ref": ref, "content_id": monster.get("content_id") if monster else None,
                         "fields": public_fields(monster)})
    hand = []
    for ref in pile_visible(snapshot, "HandPile"):
        card = entities.get(ref)
        hand.append({"ref": ref, "content_id": card.get("content_id") if card else None,
                     "fields": public_fields(card)})
    # 商店货架：只读公开 `ShopOffers` 序列；相位 7 但没有元素时保留空列表（不清空、不猜），
    # 由同时记录的 `shop_shelf_published` 说明这份空是"序列未发布/为空"而不是"货架被清掉"。
    shelf = _shop_shelf_rows(snapshot)
    return {
        "status": snapshot.get("status"),
        "observation_id": snapshot.get("observation_id"),
        "terminal_result": snapshot.get("terminal_result"),
        "run": public_fields(run),
        "player": public_fields(player),
        "monsters": monsters,
        "hand": hand,
        "potion_belt": sequence(snapshot, "PotionBelt"),
        "reward_potions": sequence(snapshot, "RewardPotions"),
        "long_term_deck": [{"ref": ref, "content_id": (entities.get(ref) or {}).get("content_id"),
                            "fields": public_fields(entities.get(ref))}
                           for ref in sequence(snapshot, "LongTermDeck")],
        "shop_offers": shelf,
        "shop_offers_published": bool(sequence(snapshot, SHOP_OFFERS_SEQUENCE)),
        "piles": snapshot.get("piles"),
        "legal_actions": [{"action": action_name(c), "candidate_id": c.get("candidate_id"),
                           "fields": candidate_fields(c)} for c in snapshot.get("legal_actions") or []],
    }


def _shop_shelf_rows(snapshot: dict) -> list:
    """公开货架行（`ShopOffers` 序列顺序）：`{ref, content_id, fields, missing_required}`。"""
    entities = entities_of(snapshot)
    rows = []
    for ref in sequence(snapshot, SHOP_OFFERS_SEQUENCE):
        entity = entities.get(ref)
        fields = public_fields(entity)
        rows.append({"ref": ref, "content_id": entity.get("content_id") if entity else None,
                     "fields": fields,
                     "missing_required": [name for name in OFFER_REQUIRED_FIELDS if not _known(fields, name)]})
    return rows


def long_term_state(snapshot: dict) -> dict:
    return {"run": public_fields(find_entity(snapshot, "Run")),
            "player": public_fields(find_entity(snapshot, "Player"))}


# -- 现场反例：已消费的旧候选配当前观察（只用公开事实，不生成候选、不猜参数） ------------------------------

def probe_consumed_candidate(client, session_id: str, snapshot: dict, consumed_candidate_id: str,
                             *, timeout: Optional[float] = None) -> dict:
    """用**刚刚消费掉**的旧 `candidate_id` 配**当前** `observation_id` 再发一次 `step`。

    这是候选闭合的负对照：旧候选已经不在当前 `legal_actions` 里，宿主必须拒绝；拒绝还**不得推进**公开
    状态。全部判断只读当前公开快照——不新建会话、不生成候选、不猜参数、不改任何状态。随后用一次
    `observe`（公开读，不推进）核对公开事实逐字段不变，并确认当前快照**仍提供公开 `LeaveShop`**。
    """
    observation_id = snapshot.get("observation_id")
    legal_now = list(snapshot.get("legal_actions") or [])
    offered_now = {item.get("candidate_id") for item in legal_now}
    probe = client.step(session_id, observation_id, consumed_candidate_id, timeout=timeout)
    record: dict = {
        "purpose": "购买后用已消费的旧候选配当前观察：必须被拒绝、不得推进，且之后仍可离店",
        "observation_id": observation_id,
        "consumed_candidate_id": consumed_candidate_id,
        "consumed_candidate_still_offered": consumed_candidate_id in offered_now,
        "response_ok": bool(probe.get("ok")),
        "response": probe,
    }
    after = client.observe(session_id, timeout=timeout)
    after_snapshot = (after.get("result") or {}).get("snapshot") if after.get("ok") else None
    record["observe_ok"] = bool(after.get("ok"))
    record["observe_error"] = after.get("error")
    if not isinstance(after_snapshot, dict):
        return record
    run_before, run_after = public_fields(find_entity(snapshot, "Run")), \
        public_fields(find_entity(after_snapshot, "Run"))
    legal_after = list(after_snapshot.get("legal_actions") or [])
    record["no_advance"] = {
        "observation_id": [observation_id, after_snapshot.get("observation_id")],
        "run_fields_equal": run_before == run_after,
        "legal_actions_equal": [item.get("candidate_id") for item in legal_now]
                               == [item.get("candidate_id") for item in legal_after],
        "run_before": run_before,
        "run_after": run_after,
    }
    record["no_advance"]["advanced"] = (
        record["no_advance"]["observation_id"][0] != record["no_advance"]["observation_id"][1]
        or not record["no_advance"]["run_fields_equal"]
        or not record["no_advance"]["legal_actions_equal"])
    leave = next((item for item in legal_after if action_name(item) == SHOP_LEAVE_ACTION), None)
    record["leave_available_after_rejection"] = {
        "available": leave is not None,
        "candidate_id": (leave or {}).get("candidate_id"),
        "candidate_fields": candidate_fields(leave) if leave else None,
    }
    return record


def room_key(snapshot: dict) -> tuple:
    run = find_entity(snapshot, "Run")
    return (field(run, "CurrentRoom"), field(run, "RoomCount"), field(run, "MapPhase"),
            field(run, "CurrentNode"), field(run, "SettledRoom"))


# -- 通道记录 -------------------------------------------------------------------------------------------

class RecordingClient(NetClient):
    """把双向原始 NDJSON 帧逐行落到 `frames.ndjson`；不改 net_client 的行为。"""

    def __init__(self, command: list, *, sink, **kwargs) -> None:
        self._sink = sink
        super().__init__(command, **kwargs)

    def _write_frame(self, frame: dict) -> None:
        self._sink("client->host", frame)
        super()._write_frame(frame)

    def _read_line(self, timeout: Optional[float]) -> Optional[str]:
        line = super()._read_line(timeout)
        if line:
            try:
                parsed: Any = json.loads(line)
            except ValueError:
                parsed = {"_unparsed": line}
            self._sink("host->client", parsed)
        return line


def sha256_file(path: pathlib.Path) -> Optional[str]:
    try:
        digest = hashlib.sha256()
        with path.open("rb") as handle:
            for chunk in iter(lambda: handle.read(1 << 20), b""):
                digest.update(chunk)
        return digest.hexdigest().upper()
    except OSError:
        return None


# -- 主流程 ---------------------------------------------------------------------------------------------

def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host-dll", type=pathlib.Path,
                        help="宿主程序集；不传则用 NET_THE_SPIRE_HOST_DLL 或仓库内已构建的程序集")
    parser.add_argument("--scope", default="dev-map", help="formal-dev 范围：dev 或 dev-map（默认 dev-map）")
    parser.add_argument("--seed", required=True, help="原样发送的游戏 seed 字符串（驱动器不做标准化）")
    parser.add_argument("--output", type=pathlib.Path, required=True, help="输出目录（必填，保证写入边界明确）")
    parser.add_argument("--max-actions", type=int, default=400, help="步数上限（默认 400）")
    parser.add_argument("--scenario", default="formal-dev", help="宿主场景名（默认 formal-dev）")
    parser.add_argument("--profile", default=None, help="scenario profile id；默认取 describe 里的第一个")
    parser.add_argument("--timeout", type=float, default=60.0, help="单次请求超时秒数（默认 60）")
    parser.add_argument("--frame-limit", type=int, default=None, help="转发给宿主的 --frame-limit")
    parser.add_argument("--relic-policy", choices=("skip", "take"), default="skip",
                        help="宝箱遗物：skip=只跳过（默认），take=尝试领取（未实现效果会被宿主具名拒绝）")
    parser.add_argument("--potion-policy", choices=("take", "skip"), default="take",
                        help="奖励药水：take=有位置就领（默认），skip=总是跳过")
    parser.add_argument("--use-potions", action="store_true",
                        help="允许在战斗里使用公开声明 UseSupported=true 的药水（默认不主动用）")
    parser.add_argument("--step-frontier", action="store_true",
                        help="允许实地推进 ContinueToNextAct（未接线的幕转换），用来取回宿主的具名失败")
    parser.add_argument("--step-unsupported-room", action="store_true",
                        help="当地图只提供未消费的房型时仍推进一次，用来取回宿主的具名失败；"
                             "默认精确停在该边界、不改整局状态")
    parser.add_argument("--shop-room", choices=("auto", "enter", "skip"), default="auto",
                        help="地图是否走进 Shop 节点：auto（默认）= 按宿主公开目录声明的商店动作决定"
                             "（未声明就保持 unsupported_map_room 边界）；enter = 强制进店；skip = 从不进店")
    parser.add_argument("--shop-policy", choices=("leave", "buy"), default="leave",
                        help="店内动作：leave（默认安全离店）= 进店后直接 LeaveShop；"
                             "buy = 先按 --shop-buy-kind 买一件，再（可选）删一张，然后离店")
    parser.add_argument("--shop-buy-kind", choices=("card", "relic", "potion"), default="card",
                        help="--shop-policy=buy 时买哪一类（映射到公开动作 BuyShopCard/BuyShopRelic/BuyShopPotion）")
    parser.add_argument("--shop-buy-slot", type=int, default=None,
                        help="只买公开 Slot 等于该值的商品；不传则按公开 (Price, Slot) 最小者确定性挑一件")
    parser.add_argument("--shop-remove", action="store_true",
                        help="在店里删一张牌（提交公开 RemoveShopCard，再按 --shop-remove-card[ -id] 回答删牌 decision）")
    parser.add_argument("--shop-remove-card", type=int, default=0,
                        help="删牌 decision 里要选的公开长期牌候选下标（默认 0=宿主公开顺序第一张）")
    parser.add_argument("--shop-remove-card-id", default=None,
                        help="按公开卡 id 挑要删的那张长期牌实例（短名匹配，可省略 '-' 之后的后缀，"
                             "如 strike 匹配 strike-ironclad）；优先于 --shop-remove-card")
    parser.add_argument("--event-choice", default=None,
                        help="当前事件优先选择完整 action_type 或唯一短名后缀；缺失/歧义停止，不回退")
    parser.add_argument("--stall-limit", type=int, default=30,
                        help="同一个动作连续重复这么多次就按 stalled 停下（默认 30）")
    parser.add_argument("--probe-stale-observation", action="store_true",
                        help="负对照：用上一步的（过期）observation_id 与 candidate_id 再发一次 step，"
                             "记录宿主的拒绝；若宿主接受则立即按 fault 停止")
    parser.add_argument("--probe-rejected-candidate", action="store_true",
                        help="负对照：第一次真实买卡后，用**刚消费掉**的旧 candidate_id 配**当前** "
                             "observation_id 再发一次 step，记录拒绝与『不推进』，并确认当前公开快照仍提供 "
                             "LeaveShop；宿主接受或状态推进则立即按 fault 停止（需要能买到卡：配 "
                             "--shop-policy=buy）")
    return parser


def run(args: argparse.Namespace) -> int:
    args.output.mkdir(parents=True, exist_ok=True)
    frames_path = args.output / "frames.ndjson"
    steps_path = args.output / "steps.ndjson"
    started_at = time.time()
    seq = itertools.count(1)
    frames_file = frames_path.open("w", encoding="utf-8")
    steps_file = steps_path.open("w", encoding="utf-8")

    def sink(direction: str, frame: Any) -> None:
        frames_file.write(json.dumps({"seq": next(seq), "dir": direction, "frame": frame},
                                     ensure_ascii=False) + "\n")
        frames_file.flush()

    host_dll = None
    if args.host_dll:
        os.environ["NET_THE_SPIRE_HOST_DLL"] = str(args.host_dll)
        host_dll = args.host_dll
    elif os.environ.get("NET_THE_SPIRE_HOST_DLL"):
        host_dll = pathlib.Path(os.environ["NET_THE_SPIRE_HOST_DLL"])

    summary: dict = {
        "tool": "run_game.py", "version": FILE_VERSION,
        "seed_requested": args.seed, "scope": args.scope, "scenario": args.scenario,
        "max_actions": args.max_actions,
        "policy": {"order": ["terminal", "pending_decision", "act_completed_frontier", "event", "shop", "map", "reward",
                             "treasure", "campfire", "upgrade/EnterNextRoom", "combat", "unsupported"],
                   "pending_decision": "net:answer_decision: 商店删牌 decision（候选全是公开 LongTermDeck "
                                       "实例且上一步提交过 RemoveShopCard）按 --shop-remove-card[-id] 选具体"
                                       "长期实例；其它选择保持：lowest public Hp monster first, else public order",
                   "combat": "attack cards first, higher public Amount first within a kind, then cheaper; "
                             "use potions only at public Hp <= half when --use-potions",
                   "relic_policy": args.relic_policy, "potion_policy": args.potion_policy,
                   "use_potions": args.use_potions, "step_frontier": args.step_frontier,
                   "step_unsupported_room": args.step_unsupported_room,
                   "event_choice": args.event_choice,
                    "event": "MapPhase=8且CurrentEventId非空；完整action_type/公开字段/发布顺序排序；显式选择唯一命中",
                    "shop_room": args.shop_room, "shop_policy": args.shop_policy,
                   "shop_buy_kind": args.shop_buy_kind, "shop_buy_slot": args.shop_buy_slot,
                   "shop_remove": args.shop_remove, "shop_remove_card": args.shop_remove_card,
                   "shop_remove_card_id": args.shop_remove_card_id,
                   "shop": "店内（公开 MapPhase=7）固定顺序：买一件（--shop-policy=buy，公开 (Price, Slot) "
                           "最小者）→ 删一张（--shop-remove，回答删牌 decision）→ LeaveShop；买不起/容量/"
                           "已售/不可买都由 Engine 候选与守卫决定，客户端不重判；字段缺失就记原因并停下",
                   "map": "prefer implemented room types that stay connected to the Boss on the public "
                          "MapNodes/MapChildren graph; Shop 是否算已接线由 --shop-room 决定；stop at "
                          "unsupported_map_room unless --step-unsupported-room"},
        "host": {"dll": str(host_dll) if host_dll else None,
                 "dll_sha256": sha256_file(host_dll) if host_dll else None},
        "identity": None, "identity_history": [], "scenario_profile": None,
        "seed_accepted": None, "session_id": None,
        "status": "driver_error", "stop": None, "exit_code": EXIT_DRIVER_ERROR,
        "run_complete": False, "whole_run_outcome": None,
        "act_completed_observed": False, "step_count": 0,
        "initial_long_term_state": None, "rooms": [],
        "terminal_result": None, "final_long_term_state": None,
        "observation_ids": [], "candidate_ids": [], "host_stderr": None,
        "event_events": [], "host_fault_attribution": None,
        "shop_catalog": None, "shop_room_entry": None, "shop_events": [], "shop_shelf_final": None,
        "files": {"frames": str(frames_path.name), "steps": str(steps_path.name)},
    }

    policy = None
    client = None
    session_id = None
    snapshot = None
    steps = 0
    previous: Optional[tuple] = None
    last_signature: Optional[tuple] = None
    repeat = 0
    stale_probe_json = None
    rejected_probe_json = None
    identity = None

    def finish(status: str, detail: str) -> int:
        code = EXIT_FAULT
        if status in TERMINAL_STATUS.values():
            code = EXIT_TERMINAL
        elif status in NON_CONCLUSIVE_STOPS:
            code = EXIT_NOT_CONCLUSIVE
        elif status == "driver_error":
            code = EXIT_DRIVER_ERROR
        summary["status"] = status
        summary["exit_code"] = code
        summary["stop"] = {"reason": status, "detail": detail}
        summary["step_count"] = steps
        summary["run_complete"] = status in TERMINAL_STATUS.values()
        summary["whole_run_outcome"] = status if status in TERMINAL_STATUS.values() else None
        if policy is not None:
            summary["act_completed_observed"] = policy.act_completed_seen
            summary["shop_events"] = policy.events
            summary["event_events"] = policy.event_events
            if snapshot is not None:
                summary["shop_shelf_final"] = _shop_shelf_rows(snapshot)
        if snapshot is not None:
            summary["final_long_term_state"] = long_term_state(snapshot)
            summary["terminal_result"] = snapshot.get("terminal_result")
        if stale_probe_json is not None:
            summary["stale_observation_probe"] = stale_probe_json
        if args.probe_rejected_candidate:
            summary["rejected_candidate_probe"] = rejected_probe_json
            if rejected_probe_json is None:
                summary["rejected_candidate_probe_note"] = (
                    "--probe-rejected-candidate 已打开，但整局没有成功买卡，反例未触发（没有可消费的旧候选）")
        summary["elapsed_seconds"] = round(time.time() - started_at, 3)
        print(f"[run_game] STOP {status} (exit {code}) steps={steps}: {detail}")
        return code

    try:
        command = host_command() + ["--scenario", args.scenario, "--scope", args.scope]
        client = RecordingClient(command, sink=sink, frame_limit=args.frame_limit)

        describe = client.describe(timeout=args.timeout)
        if not describe.get("ok"):
            return finish("driver_error", f"describe 失败：{json.dumps(describe, ensure_ascii=False)[:800]}")
        identity = describe["result"]["identity"]
        summary["identity"] = identity
        summary["identity_history"].append({"step": -1, "phase": "describe", "identity": identity})
        profiles = describe["result"]["capabilities"]["scenario_profiles"]
        profile = args.profile or profiles[0]["id"]
        summary["scenario_profile"] = profile

        # 商店进店许可：`--shop-room` 是显式 CLI；`auto` 只读宿主公开目录声明（get_catalog），
        # 目录请求失败或声明不全都不进店——不是猜、不是降级，理由逐条写进 summary.shop_room_entry。
        # 三种模式都读一次目录：`enter`/`skip` 只把它记进 summary，让每份证据自己说明宿主声明了什么。
        catalog = client.get_catalog(identity["catalog_id"], timeout=args.timeout)
        declared = None
        if catalog.get("ok"):
            declared = shop_declared_actions(catalog["result"])
            summary["shop_catalog"] = declared
        else:
            summary["shop_catalog"] = {"error": catalog.get("error")}
        if args.shop_room == "enter":
            shop_room_entry = True
            entry_note = "--shop-room=enter 显式指定（即使宿主未声明商店动作也实地推进）"
        elif args.shop_room == "skip":
            shop_room_entry = False
            entry_note = "--shop-room=skip 显式指定（从不进店）"
        elif declared is None:
            shop_room_entry = False
            entry_note = ("get_catalog 未成功："
                          + json.dumps(catalog.get("error"), ensure_ascii=False)[:400]
                          + "；auto 按「不进店」处理（不猜）")
        else:
            shop_room_entry = bool(declared["shop_actions_declared"])
            entry_note = (f"宿主目录声明商店动作 {declared['declared_shop_actions']}"
                          if shop_room_entry
                          else f"宿主目录未声明全部商店动作（缺 {declared['missing_shop_actions']}）")
        summary["shop_room_entry"] = {"mode": args.shop_room, "enter_shop_nodes": shop_room_entry,
                                     "note": entry_note}

        policy = FixedOrderPolicy(step_frontier=args.step_frontier, relic_policy=args.relic_policy,
                                  potion_policy=args.potion_policy, use_potions=args.use_potions,
                                  step_unsupported_room=args.step_unsupported_room,
                                  shop_room_entry=shop_room_entry, shop_policy=args.shop_policy,
                                  shop_buy_kind=args.shop_buy_kind, shop_buy_slot=args.shop_buy_slot,
                                  shop_remove=args.shop_remove, shop_remove_card=args.shop_remove_card,
                                  shop_remove_card_id=args.shop_remove_card_id, event_choice=args.event_choice)
        print(f"[run_game] shop rooms: enter={shop_room_entry} ({entry_note}); "
              f"shop policy={args.shop_policy}/{args.shop_buy_kind}"
              f"{' + remove' if args.shop_remove else ''}")

        started = client.start(identity, profile, seed=args.seed, timeout=args.timeout)
        if not started.get("ok"):
            return finish("driver_error", f"start 失败：{json.dumps(started, ensure_ascii=False)[:800]}")
        session_id = started["result"]["session_id"]
        snapshot = started["result"]["snapshot"]
        summary["session_id"] = session_id
        summary["seed_accepted"] = started["result"].get("seed")
        summary["identity_history"].append({"step": 0, "phase": "start", "session_id": session_id,
                                           "seed": summary["seed_accepted"],
                                           "identity": {k: snapshot.get(k) for k in
                                                        ("protocol_version", "ruleset_id", "catalog_id",
                                                         "backend_profile_id")}})
        summary["initial_long_term_state"] = long_term_state(snapshot)
        summary["rooms"].append({"step": 0, "phase": "start", "key": list(room_key(snapshot)),
                                 "state": long_term_state(snapshot)})
        print(f"[run_game] describe ok: ruleset={identity['ruleset_id']} catalog={identity['catalog_id']}")
        print(f"[run_game] start ok: session={session_id} seed={summary['seed_accepted']!r} "
              f"status={snapshot.get('status')} obs={snapshot.get('observation_id')}")

        while True:
            if snapshot.get("status") == "terminal":
                terminal = snapshot.get("terminal_result") or {}
                outcome = terminal.get("outcome")
                status = TERMINAL_STATUS.get(outcome)
                if status is None:
                    return finish("terminal_other_unknown",
                                  f"terminal_result 的 outcome={outcome!r} 不是 wire 声明的 win/loss/other")
                return finish(status, json.dumps(terminal, ensure_ascii=False))
            if steps >= args.max_actions:
                return finish("step_limit", f"达到 --max-actions={args.max_actions}，整局尚未结束")

            choice = policy.choose(snapshot)
            if isinstance(choice, Stop):
                return finish(choice.reason, choice.detail)

            # 候选必须来自**当前**观测：不跨身份/跨观测复用旧 candidate_id。
            legal_now = {c.get("candidate_id") for c in snapshot.get("legal_actions") or []}
            candidate = choice.candidate
            if candidate.get("candidate_id") not in legal_now:
                return finish("driver_error",
                              f"候选 {candidate.get('candidate_id')} 不在当前快照的 legal_actions 里")
            observation_id = snapshot.get("observation_id")
            signature = (choice.action, tuple((f.get("field_id"), f.get("value"))
                                              for f in candidate.get("fields") or []))
            repeat = repeat + 1 if signature == last_signature else 1
            last_signature = signature
            if repeat > args.stall_limit:
                return finish("stalled",
                              f"动作 {choice.action} 与相同公开字段连续重复 {repeat} 次，疑似不前进")

            response = client.step(session_id, observation_id, candidate["candidate_id"],
                                   timeout=args.timeout)
            step_record = {
                "step": steps + 1, "action": choice.action, "action_type": candidate.get("action_type"),
                "candidate_id": candidate.get("candidate_id"), "fields": candidate_fields(candidate),
                "reason": choice.reason, "expected_observation_id": observation_id,
                "status": "ok" if response.get("ok") else "not_ok",
                "response_envelope": {k: v for k, v in response.items() if k != "result"},
            }
            if not response.get("ok"):
                step_record["error"] = response.get("error")
                attribution = {"source": "host_rejection", "error": response.get("error"),
                               "action_type": candidate.get("action_type"),
                               "expected_observation_id": observation_id,
                               "candidate_id": candidate.get("candidate_id")}
                if choice.action == "ChooseMapPoint":
                    target = first_field_value(candidate)
                    attribution["map_node"] = target
                    attribution["map_node_type"] = policy._map_type(entities_of(snapshot), target)
                # backend_fault is not a structured content-missing code. Preserve
                # the named Host message, without classifying every backend fault
                # as missing content or claiming a rolled-back game state.
                summary["host_fault_attribution"] = attribution
                step_record["host_fault_attribution"] = attribution
                steps_file.write(json.dumps(step_record, ensure_ascii=False) + "\n")
                steps_file.flush()
                return finish("fault_host_rejected",
                              f"宿主拒绝了公开候选：{json.dumps(response.get('error'), ensure_ascii=False)}")
            new_snapshot = response["result"].get("snapshot", response["result"])
            if not isinstance(new_snapshot, dict) or "legal_actions" not in new_snapshot:
                step_record["result_keys"] = sorted((response.get("result") or {}).keys())
                steps_file.write(json.dumps(step_record, ensure_ascii=False) + "\n")
                steps_file.flush()
                return finish("fault_no_snapshot", "step 响应里没有可用的 snapshot")
            previous_step = previous
            previous = (observation_id, candidate["candidate_id"])
            snapshot = new_snapshot
            steps += 1
            summary["observation_ids"].append(observation_id)
            summary["candidate_ids"].append(candidate["candidate_id"])
            step_record["observation_id"] = snapshot.get("observation_id")
            step_record["state"] = state_view(snapshot)
            steps_file.write(json.dumps(step_record, ensure_ascii=False) + "\n")
            steps_file.flush()
            key = room_key(snapshot)
            if not summary["rooms"] or summary["rooms"][-1]["key"] != list(key):
                summary["rooms"].append({"step": steps, "phase": "room", "key": list(key),
                                         "state": long_term_state(snapshot)})
            run_fields = public_fields(find_entity(snapshot, "Run"))
            print(f"[run_game] {steps:>3} {choice.action:<20} obs {observation_id} -> "
                  f"{snapshot.get('observation_id')} "
                  f"room={run_fields.get('CurrentRoom')} enc={run_fields.get('EncounterId')} "
                  f"hp={run_fields.get('Hp')}/{run_fields.get('MaxHp')} gold={run_fields.get('Gold')} "
                  f"mapPhase={run_fields.get('MapPhase')} floor={run_fields.get('MapFloor')}")

            if args.probe_stale_observation and stale_probe_json is None:
                stale_obs, stale_candidate = previous_step or (observation_id, candidate["candidate_id"])
                probe = client.step(session_id, stale_obs, stale_candidate, timeout=args.timeout)
                stale_probe_json = {"stale_observation_id": stale_obs, "stale_candidate_id": stale_candidate,
                                    "response_ok": bool(probe.get("ok")), "response": probe,
                                    "expectation": "宿主必须拒绝过期 observation_id"}
                (args.output / "stale-observation-probe.json").write_text(
                    json.dumps(stale_probe_json, ensure_ascii=False, indent=2), encoding="utf-8")
                print(f"[run_game] stale-observation probe: ok={probe.get('ok')} "
                      f"error={json.dumps(probe.get('error'), ensure_ascii=False)}")
                if probe.get("ok"):
                    return finish("fault_stale_observation_accepted",
                                  "宿主接受了过期 observation_id + 旧 candidate_id：这是协议级发现，已停止")

            # 现场反例：第一次真实买卡后，用刚消费掉的旧候选配**当前**观察再发一次（只触发一次）。
            if args.probe_rejected_candidate and rejected_probe_json is None \
                    and choice.action in SHOP_BUY_ACTIONS.values():
                rejected_probe_json = probe_consumed_candidate(
                    client, session_id, snapshot, candidate["candidate_id"], timeout=args.timeout)
                (args.output / "rejected-candidate-probe.json").write_text(
                    json.dumps(rejected_probe_json, ensure_ascii=False, indent=2), encoding="utf-8")
                no_advance = rejected_probe_json.get("no_advance") or {}
                leave = rejected_probe_json.get("leave_available_after_rejection") or {}
                print(f"[run_game] rejected-candidate probe: ok={rejected_probe_json['response_ok']} "
                      f"error={json.dumps(rejected_probe_json['response'].get('error'), ensure_ascii=False)} "
                      f"advanced={no_advance.get('advanced')} leave_available={leave.get('available')}")
                if rejected_probe_json["response_ok"]:
                    return finish("fault_consumed_candidate_accepted",
                                  "宿主接受了已消费的旧 candidate_id + 当前 observation_id："
                                  "候选闭合被破坏，已停止")
                if no_advance.get("advanced") is not False:
                    return finish("fault_rejected_candidate_advanced",
                                  f"被拒绝的 step 推进或无法核对公开状态（no_advance={no_advance}）")
                if not leave.get("available"):
                    return finish("fault_no_leave_after_rejection",
                                  "被拒绝后当前公开快照不再提供 LeaveShop：无法离店")
    except OutcomeUnknown as failure:
        return finish("uncertain", f"结果不确定，已按 uncertain 停止，绝不重发：{failure}")
    except NetClientError as failure:
        return finish("uncertain", f"客户端通道错误，按 uncertain 停止：{failure}")
    except Exception as failure:  # pragma: no cover - 驱动器自身缺陷
        return finish("driver_error", f"{type(failure).__name__}: {failure}")
    finally:
        if client is not None:
            if session_id is not None:
                try:
                    closed = client.close_session(session_id, timeout=args.timeout)
                    summary["close_ok"] = bool(closed.get("ok"))
                except Exception as failure:  # pragma: no cover
                    summary["close_ok"] = False
                    summary["close_error"] = str(failure)
            try:
                client.shutdown()
            except Exception:  # pragma: no cover
                pass
            # stderr 只能在宿主退出后读（read() 会一直阻塞到管道关闭，先读会把驱动器挂死）。
            summary["host_stderr"] = client.stderr_text()[-4000:]
            summary["frame_diagnostics"] = {"late_archived": client.late_archived,
                                           "late_recovered": client.late_recovered,
                                           "transport_failures": client.transport_failures}
        summary["elapsed_seconds"] = round(time.time() - started_at, 3)
        (args.output / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2),
                                                 encoding="utf-8")
        frames_file.close()
        steps_file.close()


def main(argv: Optional[list] = None) -> int:
    args = build_parser().parse_args(argv)
    return run(args)


if __name__ == "__main__":
    raise SystemExit(main())
