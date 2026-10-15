"""Verify the extracted `sts2-core/` tree.

Checks, in order of how badly a failure would matter:

1. **Delimiter balance** — every ``.cs`` file must have balanced ``{}``/``()``/``[]``
   outside of strings, chars and comments. An unbalanced file means a removal cut a block
   in half, which is the failure mode that matters most.
2. **Presentation residue** — no references to the removed animation/audio/netcode surface.
3. **Structural inventory** — file counts per top-level area, for the summary report.

Run from the repository root.
"""

from __future__ import annotations

import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")
SRC = os.path.join(REPO, "sts2", "src")

RESIDUE_RE = re.compile(
    r"\b(?:"
    r"NCombatRoom|NCreature|NCard|NCardPlayQueue|NPlayerHand|NCombatUi|NRun|NGame"
    r"|NMapScreen|NModalContainer|NBestiary|NHoverTipSet|NTargetManager"
    r"|NAudioManager|NDebugAudioManager|NRunMusicController"
    r"|N[A-Z]\w*(?:Vfx|Ftue|Banner)"
    r"|MegaSprite|MegaSkeleton|MegaAnimationState|SpineAnimation|AnimState|CreatureAnimator"
    r"|SfxCmd|VfxCmd|FmodSfx|DamageSfxType"
    r")\b"
)


def strip_code(text: str) -> str:
    """Remove comments, strings and char literals so delimiters inside them are ignored."""
    out = []
    i, n = 0, len(text)
    while i < n:
        ch = text[i]
        if ch == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                i += 1
        elif ch == "/" and i + 1 < n and text[i + 1] == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                i += 1
            i += 2
        elif ch == '"':
            i += 1
            while i < n and text[i] != '"':
                if text[i] == "\\":
                    i += 1
                i += 1
            i += 1
        elif ch == "'":
            i += 1
            while i < n and text[i] != "'":
                if text[i] == "\\":
                    i += 1
                i += 1
            i += 1
        else:
            out.append(ch)
            i += 1
    return "".join(out)


def check_balance(path: str) -> str | None:
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        text = fh.read()
    code = strip_code(text)
    pairs = {"{": "}", "(": ")", "[": "]"}
    closing = {v: k for k, v in pairs.items()}
    stack: list[str] = []
    line = 1
    for ch in code:
        if ch == "\n":
            line += 1
        elif ch in pairs:
            stack.append(ch)
        elif ch in closing:
            if not stack or stack[-1] != closing[ch]:
                return f"unbalanced {ch!r} at line {line}"
            stack.pop()
    if stack:
        text_after = code[code.rfind(stack[-1]):][:60].replace("\n", "\\n")
        return f"unclosed {stack[-1]!r} near line {line}: {text_after!r}"
    return None


def main() -> int:
    out_root = OUT
    if len(sys.argv) > 1 and not sys.argv[1].startswith("-"):
        out_root = os.path.abspath(sys.argv[1])
    files = []
    for dirpath, _dirs, filenames in os.walk(out_root):
        for fn in filenames:
            if fn.endswith(".cs"):
                files.append(os.path.join(dirpath, fn))
    files.sort()

    bad_balance: list[tuple[str, str]] = []
    residue: list[tuple[str, int, str]] = []
    per_area = Counter()
    loc = 0

    for path in files:
        rel = os.path.relpath(path, out_root).replace("\\", "/")
        parts = rel.split("/")
        per_area["/".join(parts[:2]) if len(parts) > 1 else parts[0]] += 1

        err = check_balance(path)
        if err:
            bad_balance.append((rel, err))

        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            for ln, text in enumerate(fh, 1):
                loc += 1
                stripped = text.strip()
                # a removed type named in a comment is documentation, not a reference
                if stripped.startswith("//") or stripped.startswith("*"):
                    continue
                if RESIDUE_RE.search(text):
                    residue.append((rel, ln, stripped[:100]))

    print(f"files              : {len(files)}")
    print(f"lines of code      : {loc}")
    print(f"delimiter errors   : {len(bad_balance)}")
    print(f"presentation lines : {len(residue)}")

    if bad_balance:
        print("\n--- delimiter errors (worst first) ---")
        for rel, err in bad_balance[:30]:
            print(f"  {rel}: {err}")

    if residue:
        print("\n--- presentation residue by file ---")
        by_file = Counter(r[0] for r in residue)
        for rel, cnt in by_file.most_common(30):
            print(f"  {cnt:>4}  {rel}")

    print("\n--- files per area ---")
    for area, cnt in sorted(per_area.items()):
        print(f"  {cnt:>5}  {area}")

    return 1 if (bad_balance or residue) else 0


if __name__ == "__main__":
    raise SystemExit(main())
