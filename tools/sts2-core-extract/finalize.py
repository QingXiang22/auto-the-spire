"""Final pass: remove the remaining animation code that `extract.py` could not see.

`extract.py` handles members it knows by name, statement lines it recognises, and fluent
chain calls. What survives are bespoke helpers and guards that only *become* dead once we
know which local variables were holding visual objects.

The safety property here is that taint is seeded **only** from lines that already reference
a presentation identifier, and a line is only ever removed if it references a tainted name.
Code that never mentions a visual type is therefore never a candidate, no matter how it is
shaped. (An earlier pass that inferred taint from *whole bodies* over-deleted real rules
such as `CardPoolModel.AllCards` and `ModifierModel.ShouldReceiveCombatHooks`; this one
cannot, because those bodies never mention a visual type in the first place.)

Rules, applied to a fixpoint, each capped by MAX_ITER:
  1. a declaration/assignment line that mentions a presentation identifier taints its target
     name and is removed;
  2. `if`/`foreach`/`for`/`while` headers naming a tainted variable take their block with them;
  3. any remaining statement naming a tainted variable is removed;
  4. a member whose *signature* mentions a presentation identifier is removed, and calls to
     it are removed;
  5. blocks that became empty are tidied.

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

MAX_ITER = 60

# Identifiers that exist only to draw, animate, or play sound.
PRESENTATION_RE = re.compile(
    r"\b(?:"
    r"NCombatRoom|NCreature|NCard|NCardPlayQueue|NPlayerHand|NCombatUi|NRun|NGame"
    r"|NMapScreen|NModalContainer|NBestiary|NHoverTipSet|NTargetManager"
    r"|NAudioManager|NDebugAudioManager|NRunMusicController"
    r"|N[A-Z]\w*(?:Vfx|Ftue|Banner|Tip|Preview|Overlay)"
    r"|MegaSprite|MegaSkeleton|MegaAnimationState|SpineAnimation|AnimState|CreatureAnimator"
    r"|Node2D|CanvasGroup|PackedScene|PreloadManager|ResourceLoader|Texture2D"
    r"|SfxCmd|VfxCmd|FmodSfx|DamageSfxType"
    r"|GetSpecialNode|GetVfxContainer|GetBackVfxContainer|GetCreatureNode"
    r"|GetBottomOfHitbox|VfxSpawnPosition|AddChildSafely|QueueFreeSafely"
    r"|MoveChildSafely|StartDeathAnim|AnimHideIntent|DeathAnimationTask"
    r"|RefreshIntents|UpdateIntent|PlayVfx|PlayAnim|AnimMultiCardPlay"
    r"|CreateTween|TweenProperty|OstyScaleToSize|ToggleIsInteractable"
    r"|SetSelfModulate|SetVisible|SetAnimation|AddAnimation|GetAnimationState"
    r"|VfxColor|Colors\b|SignalAwaiter|GetCurrentBody|SetTarget|SetSporeTypeIsVulnerable"
    r"|FlipScale|SleepingVfx|ScaleTween|ExtraRunAssetPaths|AssetPaths"
    r")\b"
)

# Declaration / assignment shapes that taint the assigned name.
TAINT_DECL_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|protected|private|internal|static|readonly|virtual|override|sealed|const|async)\s+)*"
    r"(?P<type>[\w\.<>?\[\],\s]+?)\s+"
    r"(?P<name>[A-Za-z_]\w*)\s*(?:=|;|\{)"
)

CONTROL_OPEN_RE = re.compile(r"^\s*\}?\s*(?:if|else\s+if|foreach|for|while|do|try|lock)\b")

MEMBER_SIG_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|protected|private|internal)\b"
)

# Calls that mean real rules even if a visual type appears nearby.
GAMEPLAY_KEEP_RE = re.compile(
    r"\b(?:PowerCmd|CreatureCmd|DamageCmd|CardCmd|CardPileCmd|CardSelectCmd|OrbCmd"
    r"|PlayerCmd|PotionCmd|RelicCmd|ForgeCmd|OstyCmd|Hook|CombatManager|ModelDb"
    r"|RunState|CombatState|PlayerCombatState)\b"
)


def code_of(line: str) -> str:
    s = line.strip()
    if s.startswith("//") or s.startswith("*"):
        return ""
    return line


def rel_of(path: str) -> str:
    return os.path.relpath(path, OUT).replace("\\", "/")


def match_block(lines: list[str], start: int) -> int | None:
    depth = 0
    seen = False
    for idx in range(start, len(lines)):
        for ch in code_of(lines[idx]):
            if ch == "{":
                depth += 1
                seen = True
            elif ch == "}":
                depth -= 1
                if seen and depth == 0:
                    return idx + 1
    return None


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


def delimiters_balanced(text: str) -> bool:
    """True when {}, (), [] are balanced outside strings/comments."""
    stack: list[str] = []
    closing = {"}": "{", ")": "(", "]": "["}
    for ch in strip_code(text):
        if ch in "{([":
            stack.append(ch)
        elif ch in closing:
            if not stack or stack[-1] != closing[ch]:
                return False
            stack.pop()
    return not stack


def statement_end(lines: list[str], start: int) -> int:
    """Exclusive end of the simple statement beginning at `start` (bounded lookahead)."""
    j = start
    consumed = ""
    while j < len(lines) and j < start + 12:
        consumed += lines[j]
        j += 1
        if ";" in consumed:
            return j
    return start + 1


def member_scopes(lines: list[str]) -> list[int]:
    """For each line, the line index of the member body that encloses it (-1 if none).

    The scope key is the *second* enclosing brace: the first is the type declaration, the
    second is the member body. Two statements share a scope exactly when they sit in the same
    method/property, which is the granularity local-variable taint needs.
    """
    scopes: list[int] = []
    stack: list[int] = []
    for idx, raw in enumerate(lines):
        if len(stack) >= 2:
            scopes.append(stack[1])
        else:
            scopes.append(-1)
        for ch in code_of(raw):
            if ch == "{":
                stack.append(idx)
            elif ch == "}":
                if stack:
                    stack.pop()
    return scopes


def starts_brace_block(lines: list[str], start: int) -> bool:
    """True when the declaration at `start` opens a brace on one of the next two lines.

    Covers the common decompiled shape

        public Texture2D Icon
        {
            get { ... }
        }

    where the signature line itself carries no brace.
    """
    seen = 0
    j = start + 1
    while j < len(lines) and seen < 2:
        code = code_of(lines[j]).strip()
        if code:
            return code.startswith("{")
        seen += 1
        j += 1
    return False


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--out", default=None, help="override the target directory")
    args = ap.parse_args()

    global OUT
    if args.out:
        OUT = os.path.abspath(args.out)

    total = Counter()
    files_changed = 0
    reverted: list[str] = []

    for dirpath, _dirs, filenames in os.walk(OUT):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                lines = fh.readlines()

            removed_members: set[str] = set()
            edits: list[str] = []

            for _ in range(MAX_ITER):
                # Taint is scoped to the *enclosing member*, not the file. A local convenience
                # name like `result` or `list` appears in many methods; if one method builds it
                # from a visual node, that must not delete every other method's `result`.
                # (That scoping bug previously gutted CardSelectCmd's selection logic.)
                scope = member_scopes(lines)
                tainted_by_scope: dict[int, set[str]] = {}
                tainted: set[str] = set()
                changed = False

                # (a) seed taint from declaration/assignment lines
                for i, raw in enumerate(lines):
                    code = code_of(raw)
                    if not code.strip() or not PRESENTATION_RE.search(code):
                        continue
                    m = TAINT_DECL_RE.match(code)
                    if m and not MEMBER_SIG_RE.match(code):
                        tainted_by_scope.setdefault(scope[i], set()).add(m.group("name"))
                    # field declarations without initialiser: `private NSleepingVfx? _x;`
                    m2 = re.search(
                        r"\b(?:private|protected|public|internal)\b[^;=]*\b"
                        r"N\w*\w+\??\s+(_\w+)\s*;", code)
                    if m2:
                        tainted_by_scope.setdefault(scope[i], set()).add(m2.group(1))

                def taints_for(line_index: int) -> set[str]:
                    """Names tainted in the scope enclosing `line_index`."""
                    return tainted_by_scope.get(scope[line_index], set())

                # (b) member signatures mentioning presentation types.
                # IMPORTANT: only brace-match when the signature line actually opens a brace.
                # For an expression-bodied member (`... => SceneHelper.GetScenePath(...);`)
                # there is no body on this line, and match_block would scan forward to some
                # *unrelated* member's brace and delete everything in between. That bug ate
                # real rules in BloodWall/Queen/MoltenFist; the member-span is bounded to the
                # statement instead.
                i = 0
                while i < len(lines):
                    code = code_of(lines[i])
                    if MEMBER_SIG_RE.match(code) and PRESENTATION_RE.search(code):
                        m = re.search(r"\b(\w+)\s*\(", code)
                        if m and "(" in code:
                            removed_members.add(m.group(1))
                            if "=>" in code and "{" not in code:
                                # expression-bodied: bounded by its own `;`
                                end = statement_end(lines, i)
                            elif "{" in code or starts_brace_block(lines, i):
                                # block-bodied: the brace may sit on the next line
                                end = match_block(lines, i) or statement_end(lines, i)
                            else:
                                end = statement_end(lines, i)
                            edits.append("member")
                            del lines[i:end]
                            changed = True
                            continue
                    i += 1

                # (c) remove lines that reference tainted names or are pure visual
                i = 0
                while i < len(lines):
                    code = code_of(lines[i])
                    if not code.strip():
                        i += 1
                        continue
                    keeps_gameplay = bool(GAMEPLAY_KEEP_RE.search(code))

                    names_tainted = (
                        scope[i] >= 0
                        and any(
                            re.search(r"\b" + re.escape(t) + r"\b", code)
                            for t in taints_for(i)
                        )
                    )
                    if names_tainted and not keeps_gameplay:
                        # take the whole control-flow block with a header
                        if CONTROL_OPEN_RE.match(code):
                            blk = match_block(lines, i)
                            if blk is not None:
                                edits.append("block")
                                del lines[i:blk]
                                changed = True
                                continue
                        edits.append("stmt")
                        del lines[i:statement_end(lines, i)]
                        changed = True
                        continue

                    # a declaration whose *type* is presentation
                    if PRESENTATION_RE.search(code) and not keeps_gameplay:
                        mm = TAINT_DECL_RE.match(code)
                        if mm and not MEMBER_SIG_RE.match(code):
                            edits.append("decl")
                            del lines[i:statement_end(lines, i)]
                            changed = True
                            continue
                        if CONTROL_OPEN_RE.match(code):
                            blk = match_block(lines, i)
                            if blk is not None:
                                edits.append("block")
                                del lines[i:blk]
                                changed = True
                                continue
                        edits.append("vfx-stmt")
                        del lines[i:statement_end(lines, i)]
                        changed = True
                        continue
                    i += 1

                # (d) calls to removed members
                i = 0
                while i < len(lines):
                    code = code_of(lines[i])
                    for name in removed_members:
                        if re.search(r"(?<![\w.])" + re.escape(name) + r"\s*\(", code):
                            edits.append("call")
                            del lines[i:statement_end(lines, i)]
                            changed = True
                            break
                    else:
                        i += 1

                # (e) tidy empty *control-flow* blocks only.
                # An empty `{ }` is a legitimate member body: a card constructor that only
                # forwards to `base(...)` looks like
                #     public Foo() : base(2, CardType.Skill, ...) { }
                # and stripping the braces leaves a declaration with no body. Only remove an
                # empty block when its header is a control-flow statement, which is what a
                # previous pass leaves behind after deleting a visual branch.
                j = 0
                while j < len(lines) - 1:
                    if (
                        lines[j].strip().endswith("{")
                        and lines[j + 1].strip() == "}"
                        and CONTROL_OPEN_RE.match(code_of(lines[j]).strip().rstrip("{").strip())
                    ):
                        del lines[j:j + 2]
                        changed = True
                        continue
                    j += 1

                if not changed:
                    break

            if edits:
                text = "".join(lines)
                text = re.sub(r"\n{4,}", "\n\n\n", text)
                text = re.sub(r"[ \t]+\n", "\n", text)

                # A removal that cuts a block in half shows up as unbalanced delimiters.
                # Revert the whole file rather than leave a corrupted translation unit;
                # the caller then fixes those few files by hand.
                if not delimiters_balanced(text):
                    reverted.append(rel_of(path))
                    continue

                files_changed += 1
                for e in edits:
                    total[e] += 1
                if not args.check:
                    with open(path, "w", encoding="utf-8", newline="\n") as fh:
                        fh.write(text)

    print(f"files changed : {files_changed}")
    print(f"edits         : {sum(total.values())}")
    for k, v in total.most_common():
        print(f"  {k:<10} {v}")
    if reverted:
        print(f"\nreverted (delimiters would break) : {len(reverted)}")
        for rel in reverted:
            print(f"  {rel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
