"""Drop `using Godot;` from files that no longer reference any Godot type.

After the presentation extraction, 88 files still begin with `using Godot;` but only about a
dozen actually name a Godot type. The rest are stale imports left behind when the type they
were needed for was deleted.

A file keeps its import if its body mentions any Godot type; otherwise the line is removed.
The residual Godot surface that legitimately remains is what cannot be expressed without the
engine: frame/timer waits (`Cmd.Wait`, `CombatState.GodotTimerTask`,
`ActionExecutor`'s process-frame await) and a handful of `Mathf` shims.

Run with ``--check`` to report without writing.
"""

from __future__ import annotations

import argparse
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")

GODOT_TOKEN_RE = re.compile(
    r"\b(?:"
    r"Godot|SceneTree|SceneTreeTimer|Engine|Callable|Mathf|Color|Colors|Tween|Vector2"
    r"|Vector2I|Vector3|Node|Node2D|Node3D|Control|Texture2D|PackedScene|Resource"
    r"|StringName|CanvasGroup|Font|Image|Rid|Variant|SignalAwaiter|Basis|Transform2D"
    r"|Rect2|RandomNumberGenerator|MegaSprite|MegaSkeleton"
    r")\b"
)

USING_RE = re.compile(r"^\s*using\s+Godot\s*;\s*$")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    removed = 0
    kept = 0
    for dirpath, _dirs, filenames in os.walk(OUT):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                lines = fh.readlines()
            if not any(USING_RE.match(l) for l in lines):
                continue

            body = "".join(l for l in lines if not USING_RE.match(l))
            # strip comments so a doc mention does not keep the import alive
            body_nocomment = re.sub(r"//[^\n]*", "", body)
            if GODOT_TOKEN_RE.search(body_nocomment):
                kept += 1
                continue

            new_lines = [l for l in lines if not USING_RE.match(l)]
            removed += 1
            if not args.check:
                with open(path, "w", encoding="utf-8", newline="\n") as fh:
                    fh.writelines(new_lines)

    print(f"using Godot; removed : {removed}")
    print(f"using Godot; kept    : {kept}  (file still names a Godot type)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
