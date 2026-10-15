"""Emit bilingual (zhs/eng) name tables for OriMod content from the extracted localization.

Usage:
    python loc_report.py concept        # the 11 Concept cards, name + description, both langs
    python loc_report.py cards          # all cards, zhs + eng names
    python loc_report.py <table>        # any of cards/powers/relics/events/placements/characters/...
"""

from __future__ import annotations

import argparse
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LOC = os.path.join(ROOT, "reference", "orimod", "pck", "OriMod", "localization")
SRC = os.path.join(ROOT, "reference", "orimod", "src", "Ori", "Cards")


def load(lang: str, table: str) -> dict:
    path = os.path.join(LOC, lang, f"{table}.json")
    with open(path, encoding="utf-8-sig") as fh:
        return json.load(fh)


def snake(name: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).upper()


def concept_classes() -> list[str]:
    d = os.path.join(SRC, "Special", "Concepts")
    return sorted(f[:-3] for f in os.listdir(d) if f.endswith(".cs"))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("what")
    args = ap.parse_args()

    if args.what == "concept":
        zhs, eng = load("zhs", "cards"), load("eng", "cards")
        print("| 类名 | 中文名 | 英文名 | 中文描述 |")
        print("| --- | --- | --- | --- |")
        for cls in concept_classes():
            key = f"ORI-{snake(cls)}"
            zt = zhs.get(f"{key}.title", "<无>")
            et = eng.get(f"{key}.title", "<none>")
            zd = zhs.get(f"{key}.description", "").replace("\n", " ").replace("|", "\\|")
            print(f"| `{cls}` | {zt} | {et} | {zd} |")
        return 0

    table = "cards" if args.what == "cards" else args.what
    zhs, eng = load("zhs", table), load("eng", table)
    keys = sorted({k[:-6] for k in zhs if k.endswith(".title")})
    print(f"| key | 中文 | english |")
    print("| --- | --- | --- |")
    for k in keys:
        print(f"| {k} | {zhs.get(k + '.title', '')} | {eng.get(k + '.title', '')} |")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
