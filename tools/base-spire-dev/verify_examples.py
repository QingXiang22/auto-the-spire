"""Exercise the documented author workflow and reject invalid content. Build the tool first."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parents[2]
DLL = Path(os.environ.get("BASE_SPIRE_DEV_DLL", ROOT / "tools/base-spire-dev/bin/Debug/net9.0/BaseSpire.Dev.dll"))
EXAMPLE = ROOT / "docs/examples/base-card-mod/two-hits.spire"
results = []


def invoke(command, expected_code=0):
    started = time.perf_counter()
    process = subprocess.run(["dotnet", str(DLL), *command], cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
    payload = json.loads(process.stdout)
    results.append({"command": command, "exit_code": process.returncode,
                    "seconds": round(time.perf_counter() - started, 3), "result": payload})
    assert process.returncode == expected_code, (command, process.stdout, process.stderr)
    return payload


def assert_play(result, damage, block):
    play = result["play"]
    assert play["before"]["targetHp"] - play["after"]["targetHp"] == damage, play
    assert play["after"]["block"] == block, play
    assert play["before"]["energy"] - play["after"]["energy"] == 1, play
    assert play["decisions"] == 1 and play["discarded"] and not play["exhausted"], play


check = invoke(["check", str(EXAMPLE)])
assert [check[key] for key in ("declared", "parsed", "lowered", "registered")] == [1, 1, 1, 1]
assert check["ids"] == ["example-mod:two-hits"]
assert_play(invoke(["play", str(EXAMPLE), "example-mod:two-hits", "normal"]), 10, 2)
assert_play(invoke(["play", str(EXAMPLE), "example-mod:two-hits", "upgraded"]), 13, 3)

original = EXAMPLE.read_text(encoding="utf-8")
commands_example = ROOT / "docs/examples/base-card-mod/orchestration.spire"
loaded = invoke(["check", str(commands_example)])
assert [loaded[key] for key in ("declared", "parsed", "lowered", "registered")] == [2, 2, 2, 2]
for variant, generated in (("normal", 1), ("upgraded", 2)):
    result = invoke(["play", str(commands_example), "example-mod:generate-spark", variant])["play"]
    assert result["after"]["handCount"] - result["before"]["handCount"] == generated - 1, result
    assert result["after"]["energy"] == result["before"]["energy"] and result["decisions"] == 0, result

with tempfile.TemporaryDirectory(prefix="base-spire-author-") as directory:
    source = Path(directory) / "probe.spire"
    source.write_text(original.replace("damage = 7", "damage = 11"), encoding="utf-8")
    assert_play(invoke(["play", str(source), "example-mod:two-hits", "normal"]), 14, 2)
    source.write_text(original.replace("attack = { damage = 7 }", "applyPower = { power = MissingPower stacks = 2 }"), encoding="utf-8")
    missing = invoke(["check", str(source)], 2)
    assert missing["registered"] == 0 and "MissingPower" in missing["unsupported"][0]["reason"]
    source.write_text(original.replace("attack = { damage = 7 }", "unsupportedProbe = {}"), encoding="utf-8")
    unsupported = invoke(["check", str(source)], 2)
    assert unsupported["registered"] == 0 and "unsupportedProbe" in unsupported["unsupported"][0]["reason"]
    source.write_text(original.replace("namespace example-mod", "namespace base-spire").replace("TwoHits", "StrikeIronclad"), encoding="utf-8")
    assert "Duplicate content id" in invoke(["check", str(source)], 2)["error"]
    source.write_text(original.replace("attack = { damage = 7 }", "applyPower = { power = base-spire:strength stacks = 1 target = context.owner }"), encoding="utf-8")
    assert invoke(["check", str(source)])["registered"] == 1
    source.write_text(original + "\nenemy ProbeEnemy {}\n", encoding="utf-8")
    assert "does not load 'enemy'" in invoke(["check", str(source)], 2)["error"]

with tempfile.TemporaryDirectory(prefix="base-spire-power-") as directory:
    cards = Path(directory) / "cards.spire"
    powers = Path(directory) / "powers.spire"
    powers.write_text("using namespace example-mod; power FeelNoPain { trigger = afterCardExhausted effect = gainBlock(1, perPowerStack=FeelNoPain) }", encoding="utf-8")
    body = original.replace("attack = { damage = 3 }", "applyPower = { power = FeelNoPain stacks = 2 target = context.owner }").replace("attack = { damage = 4 }", "applyPower = { power = FeelNoPain stacks = 2 target = context.owner }").replace("keywords = {}", "keywords = { exhaust }").replace("cost = 1", "cost = 2", 1)
    cards.write_text(body, encoding="utf-8")
    result = invoke(["check", str(cards), str(powers)])
    assert result["powerIds"] == ["example-mod:feel-no-pain"] and result["registered"] == 1
    play = invoke(["play", str(cards), str(powers), "example-mod:two-hits", "upgraded"])["play"]
    assert play["before"]["energy"] - play["after"]["energy"] == 1
    assert play["after"]["block"] == 5 and play["exhausted"], play
    cards.write_text(original.replace("cost = 1", "cost = 2", 1), encoding="utf-8")
    play = invoke(["play", str(cards), "example-mod:two-hits", "upgraded"])["play"]
    assert play["before"]["energy"] - play["after"]["energy"] == 1, play

print(json.dumps({"checks": len(results), "passed": len(results), "runs": results, "scope": "controlled-card-probe"}, indent=2))
