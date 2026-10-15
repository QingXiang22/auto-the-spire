"""Fourth pass: fix call sites left dangling by the `CardCmd` preview removal.

`CardCmd`'s card-preview API (`Preview`, `PreviewCardPileAdd`, `PreviewInternal`, ...) is pure
presentation and was deleted. Its call sites cannot simply be dropped, because the *argument*
usually carries a real gameplay call:

    CardCmd.PreviewCardPileAdd(
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, base.Owner), 1.5f);

Deleting that line would delete the card generation. So:

  * `PreviewCardPileAdd(<expr>, <time>)` becomes `<expr>;` when `<expr>` has side effects
    (an `await` or a call), and is deleted outright when it is a bare variable reference;
  * `Preview(<expr>)` is deleted (its argument is always a plain variable);
  * `Upgrade(<cards>, CardPreviewStyle.<X>)` loses the style argument, keeping the upgrade;
  * `AfflictAndPreview<T>(<cards>, <amount>, CardPreviewStyle.<X>)` loses the style argument.

Balance-aware: a file whose delimiters would break is left untouched and reported.

Run with ``--check`` to report without writing.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")

sys.path.insert(0, HERE)
from extract import find_matching_paren  # noqa: E402
from finalize import delimiters_balanced  # noqa: E402

STYLE_ARG = r"CardPreviewStyle\.\w+"


def split_top_level(arg_text: str) -> list[str]:
    """Split a call's argument text on commas that are not nested."""
    parts, depth, cur = [], 0, ""
    for ch in arg_text:
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += ch
    parts.append(cur)
    return [p.strip() for p in parts if p.strip()]


def find_call(text: str, callee: str, start: int = 0) -> tuple[int, int, str] | None:
    """Locate `callee(` from `start`; return (match_start, match_end, inner_args)."""
    m = re.compile(re.escape(callee) + r"\s*\(").search(text, start)
    if not m:
        return None
    open_idx = text.index("(", m.start())
    try:
        end = find_matching_paren(text, open_idx)
    except ValueError:
        return None
    return m.start(), end, text[open_idx + 1:end - 1]


def line_bounds(text: str, idx: int) -> tuple[int, int]:
    ls = text.rfind("\n", 0, idx) + 1
    le = text.find("\n", idx)
    if le == -1:
        le = len(text)
    return ls, le


def rewrite(text: str, edits: Counter) -> str:
    for _ in range(500):
        changed = False

        # --- PreviewCardPileAdd(<expr>, <time>) ------------------------------------
        hit = find_call(text, "CardCmd.PreviewCardPileAdd")
        if hit:
            s, e, inner = hit
            args = split_top_level(inner)
            payload = args[0] if args else ""
            ls, le = line_bounds(text, s)
            side_effects = bool(re.search(r"\bawait\b|\w+\s*\(", payload))
            if side_effects:
                indent = re.match(r"[ \t]*", text[ls:s]).group(0)
                repl = f"{indent}{payload};"
                # `payload` may itself span lines; normalise to one statement
                text = text[:ls] + repl + text[le:]
            else:
                text = text[:ls] + text[le + 1:]
            edits["PreviewCardPileAdd->keep-arg" if side_effects else "PreviewCardPileAdd->drop"] += 1
            changed = True
            continue

        # --- Preview(<expr>) -------------------------------------------------------
        hit = find_call(text, "CardCmd.Preview")
        if hit:
            s, e, _inner = hit
            ls, le = line_bounds(text, s)
            text = text[:ls] + text[le + 1:]
            edits["Preview->drop"] += 1
            changed = True
            continue

        # --- Upgrade(<cards>, CardPreviewStyle.<X>) --------------------------------
        m = re.search(r"CardCmd\.Upgrade\s*\(", text)
        if m:
            open_idx = text.index("(", m.start())
            try:
                end = find_matching_paren(text, open_idx)
            except ValueError:
                end = -1
            if end != -1:
                inner = text[open_idx + 1:end - 1]
                args = split_top_level(inner)
                if len(args) == 2 and re.fullmatch(STYLE_ARG, args[1]):
                    text = text[:open_idx + 1] + args[0] + text[end - 1:]
                    edits["Upgrade->drop-style"] += 1
                    changed = True
                    continue

        # --- AfflictAndPreview<T>(<cards>, <amount>, CardPreviewStyle.<X>) ----------
        m = re.search(r"CardCmd\.AfflictAndPreview<[^>]+>\s*\(", text)
        if m:
            open_idx = text.index("(", m.start())
            try:
                end = find_matching_paren(text, open_idx)
            except ValueError:
                end = -1
            if end != -1:
                inner = text[open_idx + 1:end - 1]
                args = split_top_level(inner)
                if len(args) >= 2 and re.fullmatch(STYLE_ARG, args[-1]):
                    text = text[:open_idx + 1] + ", ".join(args[:-1]) + text[end - 1:]
                    edits["AfflictAndPreview->drop-style"] += 1
                    changed = True
                    continue

        if not changed:
            break
    return text


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--out", default=None, help="override the target directory")
    args = ap.parse_args()

    global OUT
    if args.out:
        OUT = os.path.abspath(args.out)

    edits: Counter = Counter()
    files_changed = 0
    reverted: list[str] = []

    needles = ("CardCmd.PreviewCardPileAdd", "CardCmd.Preview(", "CardCmd.Upgrade(",
               "CardCmd.AfflictAndPreview<")

    for dirpath, _dirs, filenames in os.walk(OUT):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                text = fh.read()
            if not any(n in text for n in needles):
                continue

            before = Counter(edits)
            new_text = rewrite(text, edits)
            if edits == before:
                continue

            new_text = re.sub(r"\n{4,}", "\n\n\n", new_text)
            new_text = re.sub(r"[ \t]+\n", "\n", new_text)
            rel = os.path.relpath(path, OUT).replace("\\", "/")

            if not delimiters_balanced(new_text):
                reverted.append(rel)
                edits = before  # roll back this file's counts
                continue

            files_changed += 1
            if not args.check:
                with open(path, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(new_text)

    print(f"files changed : {files_changed}")
    for k, v in edits.most_common():
        print(f"  {k:<32} {v}")
    if reverted:
        print(f"\nreverted (delimiters would break) : {len(reverted)}")
        for rel in reverted:
            print(f"  {rel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
