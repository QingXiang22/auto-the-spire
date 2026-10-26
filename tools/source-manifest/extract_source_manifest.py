#!/usr/bin/env python3
"""ENV-T04 B 线：冻结源（sts2/）建局输入静态取证工具。

只读 `sts2/`（源基线 `sts2-frozen-2026-10-05`，core 锚点 92ccd34）与
`reference/sts2-sim/`（用于逐项差异对照），不写 obj/bin，不改任何生产文件。

产出：
  * 源侧建局输入 manifest：遗物池（声明顺序 + 稀有度桶）、本幕 + 共享事件池、
    普通（弱/常规）/精英/BOSS/古代人候选、各阶段随机数消费量。
  * 源与参考的逐项 存在/顺序/tag/rarity 差异。

用法：
  python tools/source-manifest/extract_source_manifest.py \
      --out .audit/dsh-env-t04-source-init-manifest-2026-10-08/source-initialization-manifest.json

本工具是**静态取证**：它不执行游戏、不构建 C#、不推断运行期重抽次数（遇袋重抽依赖 seed 与
运行时谓词拒绝，属运行行为，不能由候选数量硬断）。
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[2]

# ---------------------------------------------------------------- 通用工具


def strip_comments(text: str) -> str:
    """去掉整行 `//` 注释与 `/* */` 块注释（不碰字符串字面量的 res:// 之类）。"""
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    out = []
    for line in text.splitlines():
        stripped = line.lstrip()
        if stripped.startswith("//"):
            out.append("")
        else:
            out.append(line)
    return "\n".join(out)


def extract_balanced(text: str, open_index: int) -> str:
    """从 text[open_index]（应为 '{' 或 '(' 或 '['）返回匹配的括号内容（不含外层括号）。"""
    opener = text[open_index]
    closer = {"{": "}", "(": ")", "[": "]"}[opener]
    depth = 0
    for i in range(open_index, len(text)):
        ch = text[i]
        if ch == opener:
            depth += 1
        elif ch == closer:
            depth -= 1
            if depth == 0:
                return text[open_index + 1 : i]
    raise ValueError(f"unbalanced {opener} at {open_index}")


def block_after(text: str, marker: str) -> str:
    """marker 结束之后第一个 '{' 的匹配内容（从 marker 末尾开始找，避免命中 marker 自带的 `{ get; }`）。"""
    idx = text.index(marker) + len(marker)
    open_index = text.index("{", idx)
    return extract_balanced(text, open_index)


def seq_after(text: str, marker: str) -> str:
    """marker 结束之后最早的 `{`/`[`/`(` 的匹配内容（用于 `= [typeof(X)]` 这类集合表达式）。"""
    idx = text.index(marker) + len(marker)
    candidates = [text.index(ch, idx) for ch in "{[(" if ch in text[idx:]]
    return extract_balanced(text, min(candidates))


def brace_blocks(text: str, marker: str) -> list[str]:
    """marker 之后每个 '{' 顶层块（marker 出现多次时逐个取）。"""
    blocks = []
    pos = 0
    while True:
        try:
            idx = text.index(marker, pos)
        except ValueError:
            return blocks
        open_index = text.index("{", idx)
        block = extract_balanced(text, open_index)
        blocks.append(block)
        pos = open_index + len(block) + 2


CAMEL_RE = re.compile(r"([a-z0-9])([A-Z])")
WHITESPACE_RE = re.compile(r"\s+")
SPECIAL_RE = re.compile(r"[^A-Z0-9_]")


def slugify(name: str) -> str:
    """照抄 sts2/src/Core/Helpers/StringHelper.cs:74-79 Slugify。"""
    text = CAMEL_RE.sub(r"\1_\2", name.strip())
    uppered = WHITESPACE_RE.sub("_", text.upper())
    return SPECIAL_RE.sub("", uppered)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def sha256_hex(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def dir_digest(directory: Path) -> dict:
    """目录内所有 .cs 的聚合摘要（排序后逐文件 hash 再 hash），用于绑定源/参考版本。"""
    if not directory.is_dir():
        return {"dir": rel(directory), "unsupported": "directory not found"}
    entries = []
    for p in sorted(directory.rglob("*.cs")):
        if "obj" in p.parts or "bin" in p.parts:
            continue
        entries.append(f"{rel(p)}:{sha256_hex(p)}")
    digest = hashlib.sha256("\n".join(entries).encode("utf-8")).hexdigest().upper()
    return {"dir": rel(directory), "file_count": len(entries), "aggregate_sha256": digest}


def git_head(repo: Path) -> str:
    """读 .git/HEAD（含 worktree/submodule 的 gitdir 指针与 packed-refs），避免依赖 git 可执行文件。"""
    git = repo / ".git"
    if git.is_file():
        text = git.read_text(encoding="utf-8").strip()
        if text.startswith("gitdir:"):
            git = (repo / text.split(":", 1)[1].strip()).resolve()
    head = git / "HEAD"
    if not head.is_file():
        return "unsupported"
    text = head.read_text(encoding="utf-8").strip()
    if not text.startswith("ref:"):
        return text
    ref = text.split(":", 1)[1].strip()
    ref_file = git / ref
    if ref_file.is_file():
        return ref_file.read_text(encoding="utf-8").strip()
    packed = git / "packed-refs"
    if packed.is_file():
        for line in packed.read_text(encoding="utf-8").splitlines():
            if line.endswith(" " + ref):
                return line.split(" ", 1)[0]
    return "unsupported"


def git_dirty(repo: Path, paths: list[str] | None = None) -> list[str]:
    """只读查询未提交改动；git 不可用返回 unsupported 而不是让取证失败。"""
    cmd = ["git", "-C", str(repo), "status", "--short"]
    if paths:
        cmd += ["--", *paths]
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
    except Exception as exc:  # noqa: BLE001 - 取证工具不因 git 缺失而失败
        return [f"unsupported: {exc.__class__.__name__}"]
    if proc.returncode != 0:
        return [f"unsupported: git rc={proc.returncode}"]
    return [line for line in proc.stdout.splitlines() if line.strip()]


# ---------------------------------------------------------------- C# 成员解析


def cs_list_entries(body: str, pattern: str) -> list[str]:
    return re.findall(pattern, body)


# ---------------------------------------------------------------- 源：遗物池


class RelicIndex:
    def __init__(self, models_dir: Path):
        self.relic_dir = models_dir / "Relics"
        self._files = {p.stem: p for p in self.relic_dir.rglob("*.cs")}

    def rarity(self, class_name: str) -> dict:
        path = self._files.get(class_name)
        if path is None:
            return {"class": class_name, "rarity": None, "unsupported": "relic file not found"}
        body = strip_comments(read(path))
        m = re.search(r"RelicRarity\s+Rarity\s*=>\s*RelicRarity\.(\w+)", body)
        if not m:
            return {
                "class": class_name,
                "rarity": None,
                "unsupported": "no `Rarity => RelicRarity.X` override in file",
                "path": rel(path),
            }
        return {"class": class_name, "rarity": m.group(1), "path": rel(path)}


def parse_relic_pool(models_dir: Path, pool_file: str) -> list[str]:
    body = strip_comments(read(models_dir / "RelicPools" / pool_file))
    gen = block_after(body, "GenerateAllRelics()")
    return re.findall(r"ModelDb\.Relic<(\w+)>\(\)", gen)


def rc_epoch_gates(models_dir: Path, pool_file: str) -> list[str]:
    body = strip_comments(read(models_dir / "RelicPools" / pool_file))
    fn = block_after(body, "GetUnlockedRelics(")
    return re.findall(r"IsEpochRevealed<(\w+)>", fn)


# ---------------------------------------------------------------- 源：Act 与遭遇


class EncounterIndex:
    def __init__(self, models_dir: Path):
        self.dir = models_dir / "Encounters"
        self._files = {p.stem: p for p in self.dir.rglob("*.cs")}
        self._cache: dict[str, dict] = {}

    def facts(self, class_name: str) -> dict:
        if class_name in self._cache:
            return self._cache[class_name]
        path = self._files.get(class_name)
        if path is None:
            fact = {"class": class_name, "unsupported": "encounter file not found"}
            self._cache[class_name] = fact
            return fact
        body = strip_comments(read(path))
        room = re.search(r"RoomType\s*=>\s*RoomType\.(\w+)", body)
        weak = re.search(r"IsWeak\s*=>\s*(true|false)", body)
        tags_stmt = re.search(r"Tags\s*=>(.*?);", body, re.S)
        tags = sorted(set(re.findall(r"EncounterTag\.(\w+)", tags_stmt.group(1)))) if tags_stmt else []
        mention = re.findall(r"EncounterTag\.(\w+)", body)
        fact = {
            "class": class_name,
            "id": slugify(class_name),
            "room_type": room.group(1) if room else None,
            "is_weak": (weak.group(1) == "true") if weak else False,
            "tags": tags,
            "tags_declared": bool(tags_stmt),
            "tags_mentions_in_file": sorted(set(mention)),
            "path": rel(path),
            "unsupported": None if room else "no RoomType override",
        }
        self._cache[class_name] = fact
        return fact


def parse_act(acts_dir: Path, act_file: str, enc: EncounterIndex) -> dict:
    path = acts_dir / act_file
    body = strip_comments(read(path))
    all_encounters = re.findall(r"ModelDb\.Encounter<(\w+)>\(\)", block_after(body, "GenerateAllEncounters()"))
    events_stmt = re.search(r"AllEvents\s*=>(.*?);", body, re.S)
    all_events = re.findall(r"ModelDb\.Event<(\w+)>\(\)", events_stmt.group(1)) if events_stmt else []
    ancients_stmt = re.search(r"AllAncients\s*=>(.*?);", body, re.S)
    ancients = (
        re.findall(r"ModelDb\.AncientEvent<(\w+)>\(\)", ancients_stmt.group(1)) if ancients_stmt else []
    )
    weak_n = re.search(r"NumberOfWeakEncounters\s*=>\s*(\d+)", body)
    base_n = re.search(r"BaseNumberOfRooms\s*=>\s*(\d+)", body)
    ancient_gate = re.findall(r"IsEpochRevealed<(\w+)>", block_after(body, "GetUnlockedAncients("))

    facts = [enc.facts(c) for c in all_encounters]
    buckets = {"weak": [], "regular": [], "elite": [], "boss": [], "other": []}
    for f in facts:
        if f.get("room_type") == "Elite":
            buckets["elite"].append(f)
        elif f.get("room_type") == "Boss":
            buckets["boss"].append(f)
        elif f.get("room_type") == "Monster" and f.get("is_weak"):
            buckets["weak"].append(f)
        elif f.get("room_type") == "Monster":
            buckets["regular"].append(f)
        else:
            buckets["other"].append(f)

    return {
        "file": rel(path),
        "all_encounters_declaration": all_encounters,
        "all_events_declaration": all_events,
        "all_ancients_declaration": ancients,
        "ancient_epoch_gate": ancient_gate,
        "number_of_weak_encounters": int(weak_n.group(1)) if weak_n else None,
        "base_number_of_rooms": int(base_n.group(1)) if base_n else None,
        "pools": buckets,
    }


# ---------------------------------------------------------------- 参考侧解析


def parse_reference_overgrowth(path: Path) -> dict:
    body = strip_comments(read(path))
    out: dict = {"file": rel(path), "lists": {}}
    for key, marker in (
        ("monster", "MonsterEncounters { get; } = new List<EncounterDefinition>"),
        ("elite", "EliteEncounters { get; } = new List<EncounterDefinition>"),
        ("boss", "BossEncounters { get; } = new List<EncounterDefinition>"),
    ):
        list_body = block_after(body, marker)
        entries = []
        pos = 0
        while True:
            m = re.search(r"new\(", list_body[pos:])
            if not m:
                break
            start = pos + m.start()
            open_index = list_body.index("(", start)
            entry_body = extract_balanced(list_body, open_index)
            after = open_index + len(entry_body) + 2
            props = ""
            rest = list_body[after:].lstrip()
            if rest.startswith("{"):
                props = extract_balanced(rest, 0)
            name = re.search(r'name:\s*"(\w+)"', entry_body)
            weak = re.search(r"isWeak:\s*(true|false)", entry_body)
            id_entry = re.search(r'IdEntry\s*=\s*"([A-Z0-9_]+)"', props)
            tags = re.findall(r"EncounterTag\.(\w+)", entry_body)
            entries.append(
                {
                    "name": name.group(1) if name else None,
                    "id_entry": id_entry.group(1) if id_entry else (slugify(name.group(1)) if name else None),
                    "id_entry_is_override": bool(id_entry),
                    "is_weak": (weak.group(1) == "true") if weak else False,
                    "tags": sorted(set(tags)),
                    "create": entry_body.split(",")[0].strip(),
                }
            )
            pos = after
        out["lists"][key] = entries
    rooms = re.search(r"BaseNumberOfRooms\s*=>\s*(\d+)", body)
    weak_n = re.search(r"NumberOfWeakEncounters\s*=>\s*(\d+)", body)
    out["ancient_pool"] = re.findall(r"typeof\((\w+)\)", seq_after(body, "AncientPool { get; } ="))
    out["base_number_of_rooms"] = int(rooms.group(1)) if rooms else None
    out["number_of_weak_encounters"] = int(weak_n.group(1)) if weak_n else None
    return out


def parse_reference_type_pool(path: Path) -> list[str]:
    body = strip_comments(read(path))
    return re.findall(r"typeof\((\w+)\)", block_after(body, "All { get; } = Array.AsReadOnly(new[]"))


# ---------------------------------------------------------------- 消费量计算


def buckets_of(items: list[dict]) -> list[dict]:
    order: list[str] = []
    counts: dict[str, int] = {}
    for it in items:
        r = it.get("rarity") or "UNSUPPORTED"
        if r not in counts:
            order.append(r)
            counts[r] = 0
        counts[r] += 1
    return [
        {
            "rarity": r,
            "count": counts[r],
            "shuffle_draws": max(counts[r] - 1, 0),
            "order_index": i,
        }
        for i, r in enumerate(order)
    ]


def consumption(items: list[dict]) -> dict:
    bs = buckets_of(items)
    return {
        "item_count": len(items),
        "bucket_count": len(bs),
        "bucket_order": [b["rarity"] for b in bs],
        "buckets": bs,
        "shuffle_draws": sum(b["shuffle_draws"] for b in bs),
        "formula": "sum over rarity buckets of (n_bucket - 1)  [UnstableShuffle = n-1 NextInt draws]",
    }


# ---------------------------------------------------------------- 主流程


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(WORKSPACE.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


BUCKETED_RARITIES = ["Common", "Uncommon", "Rare", "Shop"]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--source-root", default="sts2")
    ap.add_argument("--reference-root", default="reference/sts2-sim")
    ap.add_argument("--out", default=None)
    ap.add_argument("--markdown", default=None)
    ap.add_argument("--game-seed", default="ENV-T04-RNG-SHUFFLE-001")
    args = ap.parse_args()

    src = WORKSPACE / args.source_root
    ref = WORKSPACE / args.reference_root
    models = src / "src/Core/Models"
    core = src / "src/Core"

    if not models.is_dir():
        print(f"source tree not found: {models}", file=sys.stderr)
        return 2

    enc = EncounterIndex(models)
    relics = RelicIndex(models)

    # ---- 源：遗物池
    shared_classes = parse_relic_pool(models, "SharedRelicPool.cs")
    ironclad_classes = parse_relic_pool(models, "IroncladRelicPool.cs")
    shared_items = [relics.rarity(c) for c in shared_classes]
    ironclad_items = [relics.rarity(c) for c in ironclad_classes]

    shared_bag = consumption(shared_items)  # RunManager.cs:447 -> 共享袋不过滤稀有度
    filtered_shared = [i for i in shared_items if i.get("rarity") in BUCKETED_RARITIES]
    filtered_ironclad = [i for i in ironclad_items if i.get("rarity") in BUCKETED_RARITIES]
    player_pool = filtered_shared + filtered_ironclad
    player_bag = consumption(player_pool)

    # ---- 源：act 0
    act = parse_act(models / "Acts", "Overgrowth.cs", enc)

    # ---- 源：共享事件 / 共享古代人
    modeldb = strip_comments(read(models / "ModelDb.cs"))
    shared_events = re.findall(r"Event<(\w+)>\(\)", block_after(modeldb, "AllSharedEvents =>"))
    shared_ancients_stmt = re.search(r"AllSharedAncients\s*=>(.*?);", modeldb, re.S)
    shared_ancients = (
        re.findall(r"AncientEvent<(\w+)>\(\)", shared_ancients_stmt.group(1)) if shared_ancients_stmt else []
    )

    # ---- 源：事件池 epoch 过滤（ActModel.cs:335-346）
    actmodel = strip_comments(read(models / "ActModel.cs"))
    event_gates = re.findall(r"IsEpochRevealed<(\w+)>", block_after(actmodel, "GenerateRooms("))

    effective_events = act["all_events_declaration"] + shared_events
    ancient_candidates = act["all_ancients_declaration"] + shared_ancients

    normal_slots = None
    if act["number_of_weak_encounters"] is not None and act["base_number_of_rooms"] is not None:
        normal_slots = act["base_number_of_rooms"]

    # ---- 参考侧
    ref_act_path = ref / "src/Sts2Sim.Core/Content/Acts/Overgrowth.cs"
    ref_act = parse_reference_overgrowth(ref_act_path) if ref_act_path.is_file() else {"unsupported": "file not found"}
    ref_events = parse_reference_type_pool(ref / "src/Sts2Sim.Core/Rooms/Act1EventPool.cs")
    ref_shared_events = parse_reference_type_pool(ref / "src/Sts2Sim.Core/Rooms/SharedEventPool.cs")
    ref_shared_ancients = parse_reference_type_pool(ref / "src/Sts2Sim.Core/Rooms/SharedAncientPool.cs")
    ref_effective_events = ref_events + ref_shared_events

    # ---- 逐项差异
    diffs: list[dict] = []

    def diff_list(kind: str, src_ids: list[str], ref_ids: list[str]) -> None:
        # 任一侧解析为 0 项 ⇒ 提取失败，不能读成 identical（任务要求：不能静默少算）
        if not src_ids or not ref_ids:
            diffs.append(
                {
                    "kind": kind,
                    "verdict": "unsupported_parse_gap",
                    "source_count": len(src_ids),
                    "reference_count": len(ref_ids),
                    "reason": "one side parsed to zero entries; diff is not evidence of identity",
                }
            )
            return
        if src_ids == ref_ids:
            diffs.append({"kind": kind, "verdict": "identical", "count": len(src_ids)})
            return
        s, r = set(src_ids), set(ref_ids)
        diffs.append(
            {
                "kind": kind,
                "verdict": "differs",
                "source_count": len(src_ids),
                "reference_count": len(ref_ids),
                "only_in_source": [x for x in src_ids if x not in r],
                "only_in_reference": [x for x in ref_ids if x not in s],
                "order_differs": [x for x in src_ids if x in r and src_ids.index(x) != ref_ids.index(x)],
                "source_order": src_ids,
                "reference_order": ref_ids,
            }
        )

    diff_list("events(act0+shared)", effective_events, ref_effective_events)
    diff_list("shared_events", shared_events, ref_shared_events)
    diff_list("ancients(act0+shared)", ancient_candidates,
              ref_act.get("ancient_pool", []) + ref_shared_ancients)
    diff_list("act0_ancients_only", act["all_ancients_declaration"], ref_act.get("ancient_pool", []))

    ref_monster = ref_act.get("lists", {}).get("monster", [])
    src_weak = [f["id"] for f in act["pools"]["weak"]]
    src_regular = [f["id"] for f in act["pools"]["regular"]]
    ref_weak = [e["id_entry"] for e in ref_monster if e["is_weak"]]
    ref_regular = [e["id_entry"] for e in ref_monster if not e["is_weak"]]
    diff_list("weak_encounters", src_weak, ref_weak)
    diff_list("regular_encounters", src_regular, ref_regular)
    diff_list("elite_encounters", [f["id"] for f in act["pools"]["elite"]],
              [e["id_entry"] for e in ref_act.get("lists", {}).get("elite", [])])
    diff_list("boss_encounters", [f["id"] for f in act["pools"]["boss"]],
              [e["id_entry"] for e in ref_act.get("lists", {}).get("boss", [])])

    # tag 逐项比对
    tag_diffs = []
    src_by_id = {f["id"]: f for f in act["pools"]["weak"] + act["pools"]["regular"] + act["pools"]["elite"] + act["pools"]["boss"]}
    for e in ref_monster + ref_act.get("lists", {}).get("elite", []) + ref_act.get("lists", {}).get("boss", []):
        s = src_by_id.get(e["id_entry"])
        if s is None:
            continue
        if sorted(s["tags"]) != sorted(e["tags"]):
            tag_diffs.append(
                {"id": e["id_entry"], "source_tags": sorted(s["tags"]), "reference_tags": sorted(e["tags"])}
            )
    diffs.append(
        {
            "kind": "encounter_tags",
            "verdict": "identical" if not tag_diffs else "differs",
            "compared": len(src_by_id),
            "count": len(src_by_id),
            "difference_count": len(tag_diffs),
            "differences": tag_diffs,
        }
    )

    # 稀有度逐项比对（参考侧遗物 rarity）
    ref_relic_dir = ref / "src/Sts2Sim.Core"
    ref_rarities = {}
    for p in ref_relic_dir.rglob("*.cs"):
        try:
            b = strip_comments(read(p))
        except (OSError, UnicodeDecodeError):
            continue
        for m in re.finditer(r"class\s+(\w+)\s*:\s*RelicModel", b):
            r = re.search(r"RelicRarity\s+Rarity\s*=>\s*RelicRarity\.(\w+)", b)
            if r:
                ref_rarities[m.group(1)] = r.group(1)
    relic_rarity_diffs = []
    for it in shared_items + ironclad_items:
        rr = ref_rarities.get(it["class"])
        if rr is not None and rr != it["rarity"]:
            relic_rarity_diffs.append({"class": it["class"], "source": it["rarity"], "reference": rr})
    diffs.append(
        {
            "kind": "relic_rarity",
            "verdict": "identical" if not relic_rarity_diffs else "differs",
            "source_items": len(shared_items) + len(ironclad_items),
            "count": len(shared_items) + len(ironclad_items),
            "difference_count": len(relic_rarity_diffs),
            "reference_relic_rarity_entries": len(ref_rarities),
            "differences": relic_rarity_diffs,
            "unmatched_ref_missing": [
                it["class"] for it in shared_items + ironclad_items if it["class"] not in ref_rarities
            ],
        }
    )

    # ---- 身份绑定：源/参考文件摘要、git HEAD、未提交改动
    source_identity_files = [
        "src/Core/Models/RelicPools/SharedRelicPool.cs",
        "src/Core/Models/RelicPools/IroncladRelicPool.cs",
        "src/Core/Models/RelicPoolModel.cs",
        "src/Core/Models/ModelDb.cs",
        "src/Core/Models/ActModel.cs",
        "src/Core/Models/Acts/Overgrowth.cs",
        "src/Core/Runs/RelicGrabBag.cs",
        "src/Core/Runs/RunManager.cs",
        "src/Core/Helpers/GrabBag.cs",
        "src/Core/Helpers/StringHelper.cs",
        "src/Core/Entities/Relics/RelicRarity.cs",
        "src/Core/Entities/Players/Player.cs",
    ]
    reference_identity_files = [
        "src/Sts2Sim.Core/Content/Acts/Overgrowth.cs",
        "src/Sts2Sim.Core/Content/ActDefinition.cs",
        "src/Sts2Sim.Core/Content/EncounterDefinition.cs",
        "src/Sts2Sim.Core/Rooms/Act1EventPool.cs",
        "src/Sts2Sim.Core/Rooms/SharedEventPool.cs",
        "src/Sts2Sim.Core/Rooms/SharedAncientPool.cs",
        "src/Sts2Sim.Core/Runs/RelicGrabBag.cs",
        "src/Sts2Sim.Core/Runs/RunState.cs",
    ]

    def hash_files(root: Path, names: list[str]) -> dict:
        out = {}
        for name in names:
            p = root / name
            out[name] = sha256_hex(p) if p.is_file() else "missing"
        return out

    identity = {
        "source": {
            "root": args.source_root,
            "git_head": git_head(WORKSPACE),
            "baseline_id": "sts2-frozen-2026-10-05",
            "core_anchor_commit": "92ccd34",
            "files": hash_files(src, source_identity_files),
            "relic_pools_dir": dir_digest(models / "RelicPools"),
            "encounters_dir": dir_digest(models / "Encounters"),
            "dirty_files": git_dirty(WORKSPACE, [args.source_root]),
        },
        "reference": {
            "root": args.reference_root,
            "git_head": git_head(ref),
            "files": hash_files(ref, reference_identity_files),
            "relic_pools_dir": dir_digest(ref / "src/Sts2Sim.Core/Models/RelicPools"),
            "dirty_files": git_dirty(ref, reference_identity_files),
        },
    }

    manifest = {
        "schema": "env-t04-source-initialization-manifest@1",
        "generated_by": "tools/source-manifest/extract_source_manifest.py",
        "game_seed": args.game_seed,
        "source_baseline": {
            "id": "sts2-frozen-2026-10-05",
            "tree": args.source_root,
            "core_anchor_commit": "92ccd34",
            "relic_rarity_enum": ["None", "Starter", "Common", "Uncommon", "Rare", "Shop", "Event", "Ancient"],
        },
        "run_configuration": {
            "character": "Ironclad",
            "ascension": 0,
            "unlock_policy": "All",
            "mode": "SinglePlayer",
            "source_baseline_id": "sts2-frozen-2026-10-05",
            "scope_id": "base-spire-singleplayer-dev-v1",
            "evidence": [
                "mod-the-spire/base-spire/BaseSpire/SinglePlayer/RunConfiguration.cs:4,87,107-115",
                "docs/specs/environment-constraints.md §3",
            ],
        },
        "unlock_policy_all_semantics": {
            "epoch_revealed": "all EpochModel ids revealed -> every IsEpochRevealed<T>() is true",
            "event_pool_epoch_filters_bypassed": event_gates,
            "shared_relic_pool_epoch_filters_bypassed": rc_epoch_gates(models, "SharedRelicPool.cs"),
            "ironclad_relic_pool_epoch_filters_bypassed": rc_epoch_gates(models, "IroncladRelicPool.cs"),
            "act0_ancient_epoch_gates_bypassed": act["ancient_epoch_gate"],
            "shared_ancient_epoch_gate_bypassed": ["DarvEpoch"],
            "note": "All 下 ActModel.cs:334-346 / RelicGrabBag 的 RemoveAll 分支全部不执行；"
            "被这些 epoch 门控的条目仍留在有效池里。",
        },
        "call_order_and_conditions": [
            {
                "stage": "shared_relic_bag",
                "source": ["sts2/src/Core/Runs/RunManager.cs:447"],
                "call": "State.SharedRelicGrabBag.Populate(ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(State.UnlockState), State.Rng.UpFront)",
                "condition": "InitializeNewRun（只在全新局，非读档）；先于任何玩家袋与整幕生成",
                "rarity_filter": "无（RelicGrabBag.cs:94-114 共享袋按任意 Rarity 分桶）",
            },
            {
                "stage": "character_relic_bag",
                "source": ["sts2/src/Core/Runs/RunManager.cs:448-451", "sts2/src/Core/Entities/Players/Player.cs:363-369"],
                "call": "player.PopulateRelicGrabBagIfNecessary(State.Rng.UpFront) 每玩家一次",
                "condition": "紧跟共享袋；if (!RelicGrabBag.IsPopulated) 才 Populate",
                "rarity_filter": "RelicGrabBag.cs:16-22 _rarities={Common,Uncommon,Rare,Shop}，"
                "Ancient/Event/Starter/None 被 RemoveAll 剔除",
            },
            {
                "stage": "shared_ancients_allocation",
                "source": ["sts2/src/Core/Runs/RunManager.cs:670-677"],
                "call": "State.UnlockState.SharedAncients.ToList().UnstableShuffle(State.Rng.UpFront)；"
                "然后 `foreach (ActModel item in State.Acts.Skip(1))`：count = rng.NextInt(list.Count + 1)，"
                "取前 count 个交给该 act（SetSharedAncientSubset），并从剩余中移除",
                "condition": "GenerateRooms 阶段开始，先于任何 act 的事件/遭遇队列",
                "act0_note": "Act0 不在 Skip(1) 之外 ⇒ `_sharedAncientSubset` 为 null，"
                "Act0 古代人候选 = GetUnlockedAncients(unlockState) 即本幕 AllAncients（All 解锁下 = [Neow]），"
                "共享先古 [Darv] 只可能分配给 Act1+。参考 RunState.cs:276 `subsets[0] = Array.Empty<Type>()` 同义",
            },
            {
                "stage": "act0_events",
                "source": ["sts2/src/Core/Models/ActModel.cs:334-347"],
                "call": "AllEvents.Concat(ModelDb.AllSharedEvents).ToList().UnstableShuffle(rng)",
                "condition": "每个 act 的 GenerateRooms 开头",
            },
            {
                "stage": "act0_normal_queue",
                "source": ["sts2/src/Core/Models/ActModel.cs:348-371"],
                "call": "弱袋 3 槽(from AllWeakEncounters) + 常规袋 GetNumberOfRooms-3 槽(from AllRegularEncounters)，"
                "共用同一 rng；袋空即重装全部候选；GrabAndRemove 谓词拒绝时重抽",
                "condition": "事件洗牌之后、精英队列之前",
            },
            {
                "stage": "act0_elite_queue",
                "source": ["sts2/src/Core/Models/ActModel.cs:372-383"],
                "call": "固定 15 槽，循环内 rng 抽；候选 AllEliteEncounters",
                "condition": "普通队列之后",
            },
            {
                "stage": "act0_boss",
                "source": ["sts2/src/Core/Models/ActModel.cs:384"],
                "call": "_rooms.Boss = rng.NextItem(AllBossEncounters)",
                "condition": "精英队列之后",
            },
            {
                "stage": "act0_ancient",
                "source": ["sts2/src/Core/Models/ActModel.cs:385"],
                "call": "_rooms.Ancient = rng.NextItem(GetUnlockedAncients(unlockState).Concat(_sharedAncientSubset ?? []))",
                "condition": "BOSS 之后；_sharedAncientSubset 由 RunManager.cs:671-677 预先分配",
            },
        ],
        "relic_pools": {
            "shared_pool": {
                "file": rel(models / "RelicPools/SharedRelicPool.cs"),
                "declaration_order": shared_classes,
                "items": shared_items,
                "bag_consumption_unfiltered": shared_bag,
            },
            "ironclad_pool": {
                "file": rel(models / "RelicPools/IroncladRelicPool.cs"),
                "declaration_order": ironclad_classes,
                "items": ironclad_items,
                "bag_consumption_unfiltered": consumption(ironclad_items),
            },
            "character_bag_pool": {
                "rule": "SharedRelicPool.GetUnlockedRelics ⊕ IroncladRelicPool.GetUnlockedRelics，再按 _rarities={Common,Uncommon,Rare,Shop} 过滤",
                "items": player_pool,
                "consumption": player_bag,
            },
        },
        "act0": {
            "index": 0,
            "model_file": act["file"],
            "base_number_of_rooms": act["base_number_of_rooms"],
            "number_of_weak_encounters": act["number_of_weak_encounters"],
            "get_number_of_rooms_singleplayer": act["base_number_of_rooms"],
            "normal_slots_total": normal_slots,
            "elite_slots_fixed": 15,
            "boss_slots": 1,
            "ancient_slots": 1,
            "event_pool": {
                "act_events": act["all_events_declaration"],
                "shared_events": shared_events,
                "effective_pool_size": len(effective_events),
                "effective_order": effective_events,
                "shuffle_draws": max(len(effective_events) - 1, 0),
            },
            "encounter_pools": {
                "all_encounters_declaration": act["all_encounters_declaration"],
                "weak": act["pools"]["weak"],
                "regular": act["pools"]["regular"],
                "elite": act["pools"]["elite"],
                "boss": act["pools"]["boss"],
                "other": act["pools"]["other"],
                "weak_count": len(act["pools"]["weak"]),
                "regular_count": len(act["pools"]["regular"]),
                "monster_candidate_count": len(act["pools"]["weak"]) + len(act["pools"]["regular"]),
                "elite_count": len(act["pools"]["elite"]),
                "boss_count": len(act["pools"]["boss"]),
            },
            "ancient_pool": {
                "act_ancients": act["all_ancients_declaration"],
                "shared_ancients": shared_ancients,
                "effective_order": ancient_candidates,
                "shuffle_draws": max(len(shared_ancients) - 1, 0),
                "act0_effective_candidates": act["all_ancients_declaration"],
                "act0_note": "Act0 的 `_sharedAncientSubset` 恒为 null（RunManager.cs:671 `State.Acts.Skip(1)`），"
                "故 Act0 古代人候选 = 本幕 AllAncients（All 解锁下 [Neow]），永不包含共享先古；"
                "参考 RunState.cs:276 `subsets[0] = Array.Empty<Type>()` 同义。"
                "`effective_order` 只是'本幕 ⊕ 共享'的一般拼接口径，Act1+ 才可能拼上 [Darv]。",
                "act0_draw_note": "Act0 该调用为 1 项池 → NextItem 消费 1 次 NextInt(1)；"
                "若误按 2 项池实现会多消费 NextInt(2) 并改变其后流位置",
            },
        },
        "redraw_protocol": {
            "source": ["sts2/src/Core/Helpers/GrabBag.cs:96-124", "sts2/src/Core/Models/ActModel.cs:419-430"],
            "grab_and_remove": "每槽先 GrabIndex(rng) = 一次 rng.NextDouble()*totalWeight 按权重选 index；"
            "谓词不通过则**再抽一次**（循环），谓词无解时返回 null",
            "empty_bag": "袋空时把该池全部候选按 weight 1.0 重装（同 seed 下可能产生重复槽）",
            "predicate": "!e.SharesTagsWith(encounters.LastOrDefault()) && e != encounters.LastOrDefault()",
            "predicate_source": [
                "sts2/src/Core/Models/EncounterModel.cs:279-286 SharesTagsWith = Tags.Intersect(other.Tags).Any()",
                "sts2/src/Core/Models/EncounterModel.cs:114 Tags 基类默认 Array.Empty<EncounterTag>()",
                "sts2/src/Core/Models/ActModel.cs:419-430 AddWithoutRepeatingTags",
            ],
            "tag_deviation_note": "源 EncounterTag 枚举无 Jaxfruit（sts2/src/Core/Entities/Encounters/EncounterTag.cs），"
            "参考枚举新增 Jaxfruit；源 SlitheringStranglerNormal 无 Tags 覆写（∅）、SnappingJaxfruitNormal 仅 [Mushroom]，"
            "参考分别为 [Jaxfruit,Slimes] / [Mushroom,Jaxfruit] ⇒ 谓词拒绝行为可能不同，属 §10 窄裁决候选",
            "note": "实际抽取次数由 seed + 谓词拒绝决定，不能由候选数量硬断槽数（普通槽 15，参考实测 16 次、"
            "base 实测 18 次都只是运行结果）",
        },
        "reference": {
            "root": args.reference_root,
            "act0": ref_act,
            "act1_event_pool": ref_events,
            "shared_event_pool": ref_shared_events,
            "effective_event_pool": ref_effective_events,
            "shared_ancient_pool": ref_shared_ancients,
            "bucketed_rarities": BUCKETED_RARITIES,
            "relic_grab_bag_file": rel(ref / "src/Sts2Sim.Core/Runs/RelicGrabBag.cs"),
            "run_state_file": rel(ref / "src/Sts2Sim.Core/Runs/RunState.cs"),
        },
        "source_vs_reference": diffs,
        "identity": identity,
    }

    if args.out:
        out_path = WORKSPACE / args.out
        out_path.parent.mkdir(parents=True, exist_ok=True)
        out_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"wrote {rel(out_path)}")

    if args.markdown:
        md_path = WORKSPACE / args.markdown
        md_path.parent.mkdir(parents=True, exist_ok=True)
        md_path.write_text(render_markdown(manifest), encoding="utf-8")
        print(f"wrote {rel(md_path)}")

    print(json.dumps({
        "shared_relic_items": shared_bag["item_count"],
        "shared_relic_shuffle_draws": shared_bag["shuffle_draws"],
        "character_bag_items": player_bag["item_count"],
        "character_bag_buckets": player_bag["bucket_order"],
        "character_bag_shuffle_draws": player_bag["shuffle_draws"],
        "act0_events": len(effective_events),
        "act0_event_shuffle_draws": max(len(effective_events) - 1, 0),
        "act0_monster_candidates": len(act["pools"]["weak"]) + len(act["pools"]["regular"]),
        "diff_verdicts": {d["kind"]: d["verdict"] for d in diffs},
    }, ensure_ascii=False, indent=2))
    return 0


def render_markdown(m: dict) -> str:
    a = m["act0"]
    sh = m["relic_pools"]["shared_pool"]["bag_consumption_unfiltered"]
    pb = m["relic_pools"]["character_bag_pool"]["consumption"]
    lines = [
        "# ENV-T04 源建局输入 manifest（冻结源 sts2-frozen-2026-10-05）",
        "",
        f"- 生成工具：`{m['generated_by']}`（静态取证，只读）",
        f"- game_seed：`{m['game_seed']}`",
        f"- 配置：{m['run_configuration']['character']} / asc {m['run_configuration']['ascension']} / "
        f"unlock={m['run_configuration']['unlock_policy']} / {m['run_configuration']['mode']}",
        "",
        "## 遗物袋消费",
        "",
        f"- 共享袋（不过滤稀有度）：{sh['item_count']} 项 / {sh['bucket_count']} 桶 → "
        f"Σ(n−1) = **{sh['shuffle_draws']}** 次",
        f"  - 桶顺序 {sh['bucket_order']}",
        f"- 角色袋（`_rarities={{Common,Uncommon,Rare,Shop}}` 过滤）：{pb['item_count']} 项 / "
        f"{pb['bucket_count']} 桶 → Σ(n−1) = **{pb['shuffle_draws']}** 次",
        f"  - 桶顺序 {pb['bucket_order']}",
        "",
        "## Act 0（Overgrowth）输入",
        "",
        f"- 事件池：本幕 {len(a['event_pool']['act_events'])} + 共享 {len(a['event_pool']['shared_events'])} = "
        f"**{a['event_pool']['effective_pool_size']}**，洗牌消费 **{a['event_pool']['shuffle_draws']}** 次",
        f"- 普通槽：{a['number_of_weak_encounters']} 弱 + {a['number_of_weak_encounters'] and (a['normal_slots_total'] - a['number_of_weak_encounters'])} 常规 = "
        f"**{a['normal_slots_total']}** 槽；候选 弱 {a['encounter_pools']['weak_count']} + 常规 "
        f"{a['encounter_pools']['regular_count']} = **{a['encounter_pools']['monster_candidate_count']}**",
        f"- 精英：固定 **{a['elite_slots_fixed']}** 槽，候选 {a['encounter_pools']['elite_count']}",
        f"- BOSS：**1** 抽，候选 {a['encounter_pools']['boss_count']}",
        f"- 古代人：Act0 候选 = **{a['ancient_pool'].get('act0_effective_candidates', a['ancient_pool']['effective_order'])}**"
        f"（`_sharedAncientSubset` 恒为 null；本幕 ⊕ 共享的一般口径为 {a['ancient_pool']['effective_order']}，"
        f"仅 Act1+ 才可能包含共享先古）；共享先古池洗牌消费 {a['ancient_pool']['shuffle_draws']} 次",
        "",
        "## 源 / 参考 差异",
        "",
    ]
    for d in m["source_vs_reference"]:
        if d["verdict"] == "identical":
            lines.append(f"- `{d['kind']}`：**identical**（{d.get('count')} 项，存在/顺序 逐项一致）")
            continue
        if d["verdict"] == "unsupported_parse_gap":
            lines.append(
                f"- `{d['kind']}`：**unsupported_parse_gap**（source {d.get('source_count')} vs "
                f"reference {d.get('reference_count')}；{d.get('reason')}）"
            )
            continue
        if d["kind"] == "encounter_tags":
            lines.append(
                f"- `{d['kind']}`：**differs**（比对 {d.get('compared')} 个 ID，实差 {d.get('difference_count')} 条）"
            )
        elif d["kind"] == "relic_rarity":
            lines.append(
                f"- `{d['kind']}`：**differs**（比对 {d.get('count')} 个遗物，实差 {d.get('difference_count')} 条；"
                f"参考侧可解析 rarity 条目 {d.get('reference_relic_rarity_entries')}）"
            )
        else:
            lines.append(
                f"- `{d['kind']}`：**differs**（source {d.get('source_count')} vs reference "
                f"{d.get('reference_count')}；仅源 {d.get('only_in_source')}；"
                f"仅参考 {d.get('only_in_reference')}；顺序差异 {d.get('order_differs')}）"
            )
        for td in d.get("differences") or []:
            if d["kind"] == "encounter_tags":
                lines.append(f"  - {td['id']}: 源 {td['source_tags']} vs 参考 {td['reference_tags']}")
            else:
                lines.append(f"  - {td['class']}: 源 {td['source']} vs 参考 {td['reference']}")
    ident = m.get("identity", {})
    if ident:
        lines += [
            "",
            "## 身份绑定（源/参考版本与脏文件）",
            "",
            f"- 源 `{ident['source']['root']}` HEAD `{ident['source']['git_head']}`，"
            f"baseline `{ident['source']['baseline_id']}`（core 锚点 {ident['source']['core_anchor_commit']}）",
            f"  - 遗物池目录摘要 {ident['source']['relic_pools_dir'].get('aggregate_sha256')}"
            f"（{ident['source']['relic_pools_dir'].get('file_count')} 文件）；"
            f"遭遇目录摘要 {ident['source']['encounters_dir'].get('aggregate_sha256')}"
            f"（{ident['source']['encounters_dir'].get('file_count')} 文件）",
            f"  - 源树未提交改动：{ident['source']['dirty_files'] or '无'}",
            f"- 参考 `{ident['reference']['root']}` HEAD `{ident['reference']['git_head']}`",
            f"  - 参考遗物池目录摘要 {ident['reference']['relic_pools_dir'].get('aggregate_sha256')}"
            f"（{ident['reference']['relic_pools_dir'].get('file_count')} 文件）",
            f"  - 本包实际引用文件的未提交改动：{ident['reference']['dirty_files'] or '无'}",
            "",
            "逐文件 sha256 见 JSON `identity.*.files`。参考 HEAD 不能替代实际文件摘要"
            "（参考仓库存在大量共享未提交改动）。",
        ]
    lines.append("")
    return "\n".join(lines)


if __name__ == "__main__":
    raise SystemExit(main())
