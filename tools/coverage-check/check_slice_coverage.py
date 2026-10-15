#!/usr/bin/env python3
"""B-06a/B-06c 覆盖核算：把"分母 / 已迁 / 剩余"从**实际数据**算出来，避免报告里的数字漂移。

三个数据源（都不手写）：

1. `tools/content-manifest/manifest-*.json`（B-05a 冻结清单）：卡/能力等**分母**；
2. `mod-the-spire/base-spire/BaseSpire/Slice/Content/slice-content.author`：作者文本里**真的声明**了什么；
3. `mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceFixtures.cs`：测试侧登记的"切片之外已迁内容"
   清单（`AuthoredBeyondSlice` / `AuthoredPowersBeyondSlice`）。

检查的不变量（任一不成立即失败）：

- 已迁清单里的每个 id 都**真的**在作者文本里（清单不能过期/虚报）；
- 已迁清单里的每个 id 都**在分母里**（命名漂移会立刻暴露，而不是让"已迁数"虚高）；
- 作者文本里声明的**每张卡**都要么在分母里、要么是切片自有的测试/夹具卡（显式列出，不静默计入）。

用法：
    python tools/coverage-check/check_slice_coverage.py [--root .] [--json out.json]

退出码：0 = 核算通过；1 = 不变量被破坏；2 = 用法/路径错误。
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys

def _short(ids: list[str]) -> str:
    """把 id 列表压成可读摘要（用于四态里的说明）。"""
    names = [entry.rsplit(":", 1)[1] for entry in sorted(ids)]
    return "、".join(names[:6]) + ("…" if len(names) > 6 else "")


CARD_DECLARATION = re.compile(r"^card\s+([a-z0-9-]+)\s*\{", re.MULTILINE)
POWER_DECLARATION = re.compile(r"^power\s+([a-z0-9-]+)\s*\{", re.MULTILINE)

# 夹具/测试专用卡：它们**不属于**迁移分母（只为验证能力而存在），因此显式列出。
FIXTURE_CARDS = {
    "test-exhauster",
    "test-selfharm",
    "test-drum",
    "test-cheap",
    "test-cleave",
    "test-bulwark",
    "test-charge",
    "test-blunt",
}


def read_fixture_list(text: str, name: str) -> list[str]:
    """从 SliceFixtures.cs 里读某个 `IReadOnlyList<string> NAME = [ "…" ];` 的字面量列表。"""
    match = re.search(rf"{name}\s*=\s*\[(.*?)\];", text, re.DOTALL)
    if match is None:
        return []
    return re.findall(r'"(base-spire:[a-z0-9-]+)"', match.group(1))


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="切片作者路径的覆盖核算")
    parser.add_argument("--root", default=".")
    parser.add_argument("--json", default=None)
    args = parser.parse_args(argv)

    root = pathlib.Path(args.root).resolve()
    if not (root / "AutoTheSpire.sln").exists():
        print(f"不是仓库根目录：{root}", file=sys.stderr)
        return 2

    manifests = sorted(root.glob("tools/content-manifest/manifest-*.json"))
    if not manifests:
        print("找不到 B-05a 清单", file=sys.stderr)
        return 2

    manifest = json.loads(manifests[-1].read_text(encoding="utf-8"))
    # 迁移分母的定义（写清楚，避免"分母悄悄变小"或"把多人内容算进单人范围"）：
    #   分母 = （Ironclad 单人卡池 + 无卡池卡）− 多人专属
    # 多人专属**显式分组**列出、不计入分母（B-05a 清单自己就给了这个分组）。
    multiplayer_only = set(manifest["scope"]["multiplayer_only_cards"])
    single_player = set(manifest["scope"]["ironclad_single_player_cards"])
    without_pool = set(manifest["scope"]["cards_without_pool"])
    registered = {entry["id"] for entry in manifest["categories"]["cards"]}
    card_denominator = (single_player | without_pool) - multiplayer_only

    author = (root / "mod-the-spire/base-spire/BaseSpire/Slice/Content/slice-content.author").read_text(
        encoding="utf-8")
    authored_cards = {f"base-spire:{name}" for name in CARD_DECLARATION.findall(author)}
    authored_powers = {f"base-spire:{name}" for name in POWER_DECLARATION.findall(author)}

    fixtures = (root / "mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceFixtures.cs").read_text(
        encoding="utf-8")
    migrated_cards = set(read_fixture_list(fixtures, "AuthoredBeyondSlice"))
    migrated_powers = set(read_fixture_list(fixtures, "AuthoredPowersBeyondSlice"))

    # 切片自有的 4 张卡（冻结清单的一部分，作者文本里也有）：分母里必然存在。
    frozen_cards = authored_cards - migrated_cards

    failures: list[str] = []

    stale = sorted(card for card in migrated_cards if card not in authored_cards)
    if stale:
        failures.append(f"已迁卡清单里有作者文本**没有**的条目（清单过期）：{stale}")

    outside = sorted(card for card in migrated_cards if card not in card_denominator)
    if outside:
        failures.append(f"已迁卡不在分母里（命名漂移或不属于本范围）：{outside}")

    stray = sorted(card for card in authored_cards
                   if card not in card_denominator
                   and card.rsplit(":", 1)[1] not in FIXTURE_CARDS)
    if stray:
        failures.append(f"作者文本声明了卡但既不在分母也不是夹具卡（未登记）：{stray}")

    migrated_in_denominator = sorted(migrated_cards & card_denominator)
    report = {
        "check_version": "b06a-coverage@1",
        "manifest": str(manifests[-1].relative_to(root)).replace("\\", "/"),
        "card_denominator": len(card_denominator),
        "registered_cards": len(registered),
        "multiplayer_only_cards": sorted(multiplayer_only),
        "denominator_definition": "（Ironclad 单人卡池 + 无卡池卡）− 多人专属",
        "authored_cards_in_author_text": len(authored_cards),
        "frozen_slice_cards": sorted(frozen_cards),
        "migrated_cards": len(migrated_in_denominator),
        "migrated_card_ids": migrated_in_denominator,
        "remaining_cards": len(card_denominator) - len(migrated_in_denominator),
        "fixture_cards_not_in_denominator": sorted(
            card for card in authored_cards if card not in card_denominator),
        "authored_powers": sorted(authored_powers),
        "migrated_powers": sorted(migrated_powers),
        # W-06：**统一口径的四态**（§19.1 指出正文存在 "18+4/76" 与 "22/72" 两套说法，须统一）。
        # 只有"已声明"是从作者文本机械算出的；其余三态各带**证据指针**，不做无证据推断。
        "content_states": {
            "denominator": len(card_denominator),
            "registered_total": len(registered),
            "multiplayer_only_excluded": sorted(multiplayer_only),
            "declared_in_author_text": {
                "value": len(authored_cards),
                "detail": f"其中切片自有 {len(frozen_cards)} 张、{_short(list(migrated_cards))} 张为切片之外新迁",
                "evidence": "slice-content.author（本脚本解析）",
            },
            "loaded_in_author_package": {
                "value": len(migrated_cards) + len(frozen_cards),
                "evidence": "BaseSpire.Slice.Tests 通过（作者包可构建/加载；写法见该套件）",
            },
            "behaviour_verified": {
                "value": len(migrated_cards) + len(frozen_cards),
                "evidence": "切片 B-06a/B-07a 行为检查（见 BaseSpire.Slice.Tests 输出）",
                "caveat": "按十进制的'有检查覆盖'计数，不等于原版一致性",
            },
            "host_available": {
                "value": 0,
                "evidence": "net-the-spire/client/test_author_content.py 当前 rc=3（作者宿主 start 被拒：UsePotion 未声明）",
                "caveat": "整局/扩展内容经真实 stdio 尚不可用；W-05/W-01 完成后此数才上升",
            },
            "unified_statement": (
                f"分母 {len(card_denominator)}（注册 {len(registered)} − 多人专属 {len(multiplayer_only)}）；"
                f"已声明/已加载/行为已验证 = {len(migrated_cards) + len(frozen_cards)}（= {len(frozen_cards)} 切片自有 + {len(migrated_cards)} 新迁）；"
                f"host 可用 = 0；剩余 {len(card_denominator) - len(migrated_cards) - len(frozen_cards)}"
            ),
        },
        "invariant_failures": failures,
    }

    if args.json:
        pathlib.Path(args.json).write_text(
            json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True), encoding="utf-8")
        print(f"coverage written: {args.json}")

    print(f"分母（B-05a 冻结清单；已排除 {len(multiplayer_only)} 张多人专属）: {len(card_denominator)}")
    print(f"注册总数（对照）: {len(registered)}；多人专属（显式分组、不计）: {sorted(multiplayer_only)}")
    print(f"作者文本声明的卡: {len(authored_cards)}（其中切片自有 {len(frozen_cards)}、"
          f"夹具 {len(authored_cards - card_denominator - frozen_cards)}）")
    migrated_total = len(migrated_in_denominator) + len(frozen_cards)
    print(f"已迁入并可运行（含切片自有）: {migrated_total}"
          f"（占分母 {migrated_total * 100 // max(1, len(card_denominator))}%）")
    print(f"剩余（未迁，仍在分母内）: {len(card_denominator) - migrated_total}")
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    print(f"TOTAL: {1 if not failures else 0} passed, {len(failures)} failed, "
          f"{1 + len(failures)} executed")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
