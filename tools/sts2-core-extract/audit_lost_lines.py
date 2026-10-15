"""Find non-presentation lines that exist in `sts2/src` but not in `sts2-core`.

The other audits miss one class of damage. `audit_dangling_locals.py` finds code that still
*names* a variable whose declaration vanished, but if an over-eager pass deleted both the
declaration and every use — as happened to `CardSelectCmd.FromChooseACardScreen`, whose
`result` locals, `LogChoice` call and `return` all went together — nothing dangles and the
method is silently gutted.

This audit is immune to that. It strips presentational lines from *both* sides, then asks: which
surviving original lines have no counterpart in the extraction? A line that was legitimately
*modified* (a signature losing a `CardPreviewStyle` parameter, say) is not a loss, so each
candidate is fuzzy-matched against the extraction and only reported when nothing similar exists.

Run from the repository root; exits non-zero when lines are missing.
"""

from __future__ import annotations

import argparse
import difflib
import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")
SRC = os.path.join(REPO, "sts2", "src")

sys.path.insert(0, HERE)
from audit_deletions import COMMENT, GAMEPLAY_RE, NEUTRAL, PRESENTATION  # noqa: E402

# Structural / boilerplate lines whose disappearance carries no information.
IGNORABLE = re.compile(
    r"^\s*(?:"
    r"[{}()\[\];,]*"
    r"|else|do|try|finally"
    r"|case\b.*:|default\s*:"
    r"|using\b.*"
    r"|namespace\b.*"
    r"|(?:public|private|protected|internal|static|sealed|abstract|partial)\s+class\b.*"
    r")\s*$"
)

SIMILARITY = 0.82


def presentation_names(lines: list[str]) -> set[str]:
    """Local names introduced by a presentational declaration.

    A line like `List<NCard> cardNodes = ...;` is caught by the identifier filter, but the
    later `for (int i = 0; i < cardNodes.Count; i++)` is not — it carries no presentational
    token, only a reference to one. Collecting the names first lets both sides drop those
    lines too, which removes most of the noise.
    """
    names: set[str] = set()
    for raw in lines:
        line = raw.strip()
        if not line or COMMENT.match(line) or not PRESENTATION.search(line):
            continue
        m = re.search(
            r"(?:^|[;{}(,]\s*)(?:[A-Za-z_][\w\.<>?\[\],]*\s+)([a-z_]\w*)\s*(?:=|;|\))",
            line,
        )
        if m:
            names.add(m.group(1))
        m2 = re.search(r"^\s*(?:var|[\w<>?\[\],\.]+)\s+([a-z_]\w*)\s*=", line)
        if m2:
            names.add(m2.group(1))
    # names that are presentational by convention even when the declaration was already cut
    names |= {"tween", "cardNode", "cardNodes", "nCard", "node", "node2", "vfxContainer",
              "playQueue", "playContainer", "control", "control2", "creatureNode",
              "creatureNode2", "creatureNode3", "creatureNode4", "nCreature", "nCreature2",
              "ostyNode", "vector", "marker2D", "specialNode", "treasureNode"}
    return names


def meaningful(lines: list[str], tainted: set[str]) -> list[str]:
    """Lines worth comparing: no comments, no presentation, no pure boilerplate."""
    taint_re = re.compile(
        r"(?<![\w.])(?:" + "|".join(re.escape(n) for n in sorted(tainted)) + r")(?![\w])"
    ) if tainted else None

    out = []
    for raw in lines:
        line = raw.strip()
        if not line or COMMENT.match(line) or IGNORABLE.match(line):
            continue
        if PRESENTATION.search(line):
            continue
        if taint_re is not None and taint_re.search(line):
            continue
        out.append(re.sub(r"\s+", " ", line))
    return out


SIGNATURE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:public|protected|private|internal)\b[^;=]*?(?P<name>\w+)\s*(?:\(|=>|\{|$)"
)

CONTROL_HEADER = re.compile(r"^\s*\}?\s*(?:if|else\s+if|foreach|for|while|do|try|lock)\b")


def enclosing_signatures(lines: list[str]) -> list[str]:
    """For each line index, the signature of the member that encloses it ('' if none).

    Accepts members without a parameter list — `public IEnumerable<IHoverTip> HoverTips`
    and `protected override IEnumerable<IHoverTip> ExtraHoverTips` are members too, and they
    are exactly the tooltip surfaces this extraction drops.
    """
    out: list[str] = []
    stack: list[tuple[int, str]] = []      # (brace-line index, signature at that point)
    last_sig = ""
    for idx, raw in enumerate(lines):
        stripped = raw.strip()
        if SIGNATURE.match(raw):
            last_sig = stripped
        body = stack[1][1] if len(stack) >= 2 else ""
        out.append(body)
        for ch in raw:
            if ch == "{":
                stack.append((idx, last_sig))
            elif ch == "}":
                if stack:
                    stack.pop()
    return out


def block_is_presentation(lines: list[str], idx: int) -> bool:
    """Does the brace block headed at `idx` consist of presentation code?

    A removed visual branch takes its guard with it, so the guard line itself has no
    presentational token; the evidence is in the block it controls.
    """
    text = "".join(lines[idx:])
    brace = text.find("{")
    if brace == -1:
        return False
    depth = 0
    end = -1
    for i in range(brace, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                end = i
                break
    if end == -1:
        return False
    return bool(PRESENTATION.search(text[brace:end]))


def signature_is_presentation(rel: str, signature: str) -> bool:
    """Was the member whose signature this is removed on purpose?"""
    if not signature:
        return False
    if PRESENTATION.search(signature):
        return True
    m = re.search(r"\b(\w+)\s*\(", signature)
    name = m.group(1) if m else ""
    if not name:
        return False
    try:
        from member_diff import PRESENTATION_NAMES, REVIEWED  # noqa: E402
    except Exception:
        return False
    if PRESENTATION_NAMES.search(name):
        return True
    return name in REVIEWED.get(rel, set())


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=None)
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--threshold", type=float, default=SIMILARITY)
    ap.add_argument(
        "--gameplay-only",
        action="store_true",
        help="report only lost lines that name a gameplay API (the actionable subset)",
    )
    args = ap.parse_args()

    out_root = os.path.abspath(args.out) if args.out else OUT
    gate = GAMEPLAY_RE if args.gameplay_only else None

    compared = 0
    lost: dict[str, list[str]] = defaultdict(list)
    modified = 0

    for dirpath, _dirs, filenames in os.walk(out_root):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            out_path = os.path.join(dirpath, fn)
            rel = os.path.relpath(out_path, out_root).replace("\\", "/")
            src_path = os.path.join(SRC, rel.replace("/", os.sep))
            if not os.path.exists(src_path):
                continue
            compared += 1

            orig_raw = open(src_path, encoding="utf-8", errors="replace").read().splitlines()
            ext_raw = open(out_path, encoding="utf-8", errors="replace").read().splitlines()
            tainted = presentation_names(orig_raw) | presentation_names(ext_raw)

            # keep each surviving original line paired with its enclosing member, so a line
            # that vanished merely because its whole member was presentational is excused
            sigs = enclosing_signatures(orig_raw)
            orig: list[str] = []
            orig_meta: list[tuple[str, int]] = []
            for idx, raw in enumerate(orig_raw):
                kept = meaningful([raw], tainted)
                if kept:
                    orig.append(kept[0])
                    orig_meta.append((sigs[idx], idx))
            ext = meaningful(ext_raw, tainted)

            ext_counter = Counter(ext)
            # lines the extraction kept verbatim
            remaining: list[tuple[str, str, int]] = []
            for line, (sig, idx) in zip(orig, orig_meta):
                if ext_counter[line] > 0:
                    ext_counter[line] -= 1
                else:
                    remaining.append((line, sig, idx))

            if not remaining:
                continue

            ext_pool = list(ext_counter.elements())
            for line, sig, idx in remaining:
                if gate is not None and not gate.search(line):
                    continue
                # the whole member was presentational, so its body is expected to be gone
                if signature_is_presentation(rel, sig):
                    continue
                # a guard whose controlled block was presentational goes with the block
                if CONTROL_HEADER.match(line) and block_is_presentation(orig_raw, idx):
                    continue
                best = 0.0
                for cand in ext_pool:
                    r = difflib.SequenceMatcher(None, line, cand).ratio()
                    if r > best:
                        best = r
                if best >= args.threshold:
                    modified += 1
                    continue
                lost[rel].append(line)

    total = sum(len(v) for v in lost.values())
    print(f"files compared        : {compared}")
    print(f"lines modified (ok)   : {modified}")
    print(f"files with lost lines : {len(lost)}")
    print(f"LOST lines            : {total}")
    print()
    for rel in sorted(lost, key=lambda r: -len(lost[r])):
        print(f"  {rel}  ({len(lost[rel])})")
        show = lost[rel] if args.all else lost[rel][:10]
        for line in show:
            print(f"      {line[:150]}")
        if not args.all and len(lost[rel]) > len(show):
            print(f"      ... and {len(lost[rel]) - len(show)} more")
    return 1 if lost else 0


if __name__ == "__main__":
    raise SystemExit(main())
