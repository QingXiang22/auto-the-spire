"""Frozen source facts, independent of production content catalogs."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "tools/base-spire-dev/fixtures/source-catalogs.json"
COUNTS = {"cards": 578, "powers": 260, "relics": 297, "potions": 64,
          "enemies": 121, "events": 68, "characters": 8,
          "encounters": 88, "run": 5, "pools": 27}


def load_sources() -> dict:
    data = json.loads(SOURCE.read_text(encoding="utf-8-sig"))
    if data["format"] != "base-spire/source-catalogs@1" or set(data["domains"]) != set(COUNTS):
        raise ValueError("frozen source domains changed")
    for domain, count in COUNTS.items():
        rows = data["domains"][domain]["entries"]
        if len(rows) != count or len({r["id"] for r in rows}) != count:
            raise ValueError(f"frozen {domain} source denominator must remain {count}")
        for row in rows:
            if set(row) - {"id", "name", "source_path", "source_sha256", "pools", "members", "exclusion"}:
                raise ValueError("source fixtures contain only frozen facts")
            if len(row["source_sha256"]) != 64 or any(c not in "0123456789abcdef" for c in row["source_sha256"]):
                raise ValueError("invalid source SHA256")
    return data["domains"]
