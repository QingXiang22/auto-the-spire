#!/usr/bin/env python3
"""ENV-01 的**独立神谕**：只按原版源码重写一遍随机数，用来生成"已知向量"。

为什么要独立写一遍：被测实现和对照实现不能是同一份代码。本文件按
`sts2/src/Core/Random/MegaRandom.cs` 与 `sts2/src/Core/Random/Rng.cs` 的语义**逐行重写**，
不参考 C# 侧的任何实现；生成出来的向量再交给 C# 检查去撞。

原版语义（行号对应当前冻结快照）：
- `MegaRandom.Reinitialise(seed)`：`_s0.._s3 = Splitmix64(ref seed)` 连续四次（MegaRandom.cs:98-104）；
- `Splitmix64`：`x += 0x9E3779B97F4A7C15`；`z=(x^(x>>30))*0xBF58476D1CE4E5B9`；
  `z=(z^(z>>27))*0x94D049BB133111EB`；返回 `z^(z>>31)`（MegaRandom.cs:86-92）；
- `NextULongInner`：xoshiro256**（MegaRandom.cs:156-175）；
- `NextDouble` = `(raw >> 11) * 2**-53`（MegaRandom.cs:222-225，`1.1102230246251565E-16 == 2**-53`）；
- `Next(maxValue)` = `(int)(NextDouble() * maxValue)`（MegaRandom.cs:284-287）——**双精度截断，不是拒绝采样**；
- `Next(min, max)` = `Next(max-min) + min`（MegaRandom.cs:202-214）；
- `Rng.NextInt(max)`：`Counter++` 后调用 `Next(max)`（Rng.cs:89-93）；
- `Rng.Shuffle`：`for (i = n-1; i > 0; i--) { j = NextInt(i+1); swap(list[i], list[j]); }`（Rng.cs:308-320）；
- `Rng.FastForwardCounter(n)`：`n` 次 `Counter++; _random.NextInt()`（Rng.cs:62-73）。

用法：
    python tools/rng-oracle/vanilla_rng_oracle.py [--json out.json]
"""

from __future__ import annotations

import argparse
import json
import pathlib
import sys

MASK64 = (1 << 64) - 1
MASK32 = (1 << 32) - 1


def rotl(value: int, count: int) -> int:
    return ((value << count) | (value >> (64 - count))) & MASK64


def splitmix64(state: int) -> tuple[int, int]:
    """返回 (新状态, 输出)。原版用 `ref ulong` 原地更新。"""
    state = (state + 0x9E3779B97F4A7C15) & MASK64
    value = state
    value = ((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
    value = ((value ^ (value >> 27)) * 0x94D049BB133111EB) & MASK64
    return state, value ^ (value >> 31)


class MegaRandom:
    """sts2/src/Core/Random/MegaRandom.cs 的独立重写。"""

    def __init__(self, seed: int) -> None:
        state = seed & MASK64
        state, self.s0 = splitmix64(state)
        state, self.s1 = splitmix64(state)
        state, self.s2 = splitmix64(state)
        state, self.s3 = splitmix64(state)

    def next_ulong(self) -> int:
        result = (rotl((self.s1 * 5) & MASK64, 7) * 9) & MASK64
        temporary = (self.s1 << 17) & MASK64
        self.s2 ^= self.s0
        self.s3 ^= self.s1
        self.s1 ^= self.s2
        self.s0 ^= self.s3
        self.s2 ^= temporary
        self.s3 = rotl(self.s3, 45)
        return result

    def next_int_full(self) -> int:
        """`MegaRandom.NextInt()` = `(int)(raw >> 33)`（MegaRandom.cs:236-239）。"""
        return self.next_ulong() >> 33

    def next_double(self) -> float:
        return (self.next_ulong() >> 11) * (2.0 ** -53)

    def next(self, max_value: int) -> int:
        """`MegaRandom.Next(maxValue)`：双精度截断（MegaRandom.cs:284-287）。"""
        return int(self.next_double() * max_value)


class Rng:
    """sts2/src/Core/Random/Rng.cs 的独立重写（只覆盖 ENV-01 首批用到的入口）。"""

    def __init__(self, seed: int, counter: int = 0) -> None:
        self.counter = 0
        self.seed = seed & MASK32
        self.random = MegaRandom(self.seed)
        self.fast_forward(counter)

    def fast_forward(self, target: int) -> None:
        if self.counter > target:
            raise ValueError("cannot fast-forward an Rng counter to a lower number")
        while self.counter < target:
            self.counter += 1
            self.random.next_int_full()

    def next_int(self, max_exclusive: int) -> int:
        self.counter += 1
        return self.random.next(max_exclusive)

    def next_int_range(self, min_inclusive: int, max_exclusive: int) -> int:
        if min_inclusive >= max_exclusive:
            raise ValueError("minInclusive must be lower than maxExclusive")
        self.counter += 1
        return self.random.next(max_exclusive - min_inclusive) + min_inclusive

    def next_double(self) -> float:
        self.counter += 1
        return self.random.next_double()

    def shuffle(self, items: list) -> list:
        values = list(items)
        for index in range(len(values) - 1, 0, -1):
            other = self.next_int(index + 1)
            values[index], values[other] = values[other], values[index]
        return values


def vectors() -> dict:
    """已知向量：种子 + 逐次读数。每条都带原版入口语义，便于 C# 侧逐一撞。"""
    result: dict = {"oracle": "tools/rng-oracle/vanilla_rng_oracle.py",
                    "source": ["sts2/src/Core/Random/MegaRandom.cs", "sts2/src/Core/Random/Rng.cs"],
                    "seeds": {}}

    for seed in (0, 1, 12345, 0xFFFFFFFF, 0x9E3779B9):
        rng = Rng(seed)
        entry = {
            "seed": seed,
            "raw_first8": [],
            "next_int_100_first8": [],
            "next_int_1_7_first8": [],
            "next_double_first4": [],
        }

        raw = Rng(seed)
        entry["raw_first8"] = [raw.random.next_ulong() for _ in range(8)]

        flat = Rng(seed)
        entry["next_int_100_first8"] = [flat.next_int(100) for _ in range(8)]

        ranged = Rng(seed)
        entry["next_int_1_7_first8"] = [ranged.next_int_range(1, 7) for _ in range(8)]

        doubles = Rng(seed)
        entry["next_double_first4"] = [repr(doubles.next_double()) for _ in range(4)]

        # 洗牌：10 个元素（与起始牌组同规模），记录结果排列与消耗的 counter。
        deck = [f"card-{i:02d}" for i in range(10)]
        shuffler = Rng(seed)
        entry["shuffle_10"] = shuffler.shuffle(deck)
        entry["shuffle_10_counter"] = shuffler.counter

        # 跳步：先跳到 counter=5 再抽，等于连抽 5 次之后的第 6 次读数。
        skipped = Rng(seed, 5)
        entry["after_fast_forward_5_next_int_100"] = skipped.next_int(100)
        entry["after_fast_forward_5_counter"] = skipped.counter

        result["seeds"][str(seed)] = entry

    return result


def emit_csharp(data: dict, path: pathlib.Path) -> None:
    """把**读数**向量写成 C# 向量表：生成而不是手抄，避免转录错误。"""
    lines: list[str] = []
    lines.append("// 本文件由 tools/rng-oracle/vanilla_rng_oracle.py 生成——不要手改。")
    lines.append("// 重新生成：")
    lines.append("//   python tools/rng-oracle/vanilla_rng_oracle.py "
                 "--cs core-the-spire/Core.TheSpire.Engine.Tests/VanillaRandomVectors.cs")
    lines.append("//")
    lines.append("// 向量来自只读原版源码的**独立重写**（不参考 C# 侧任何实现）：")
    for source in data["source"]:
        lines.append(f"//   - {source}")
    lines.append("")
    lines.append("namespace CoreTheSpire.Engine.Tests;")
    lines.append("")
    lines.append("/// <summary>ENV-01 的原版随机已知向量（种子 + 逐次读数 + 洗牌排列 + 消耗计数）。</summary>")
    lines.append("internal static class VanillaRandomVectors")
    lines.append("{")
    lines.append("    internal sealed record Vector(")
    lines.append("        ulong Seed,")
    lines.append("        ulong[] RawFirst8,")
    lines.append("        int[] NextInt100First8,")
    lines.append("        int[] NextInt1To7First8,")
    lines.append("        string[] NextDoubleFirst4,")
    lines.append("        string[] Shuffle10,")
    lines.append("        int Shuffle10Counter);")
    lines.append("")
    lines.append("    public static readonly Vector[] All =")
    lines.append("    [")
    for key in sorted(data["seeds"], key=lambda k: int(k)):
        entry = data["seeds"][key]
        raw = ", ".join(f"{v}UL" for v in entry["raw_first8"])
        int100 = ", ".join(str(v) for v in entry["next_int_100_first8"])
        ranged = ", ".join(str(v) for v in entry["next_int_1_7_first8"])
        doubles = ", ".join(f'"{v}"' for v in entry["next_double_first4"])
        shuffle = ", ".join(f'"{v}"' for v in entry["shuffle_10"])
        lines.append(f"        new({entry['seed']}UL, [{raw}], [{int100}], [{ranged}], [{doubles}], "
                     f"[{shuffle}], {entry['shuffle_10_counter']}),")
    lines.append("    ];")
    lines.append("}")

    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def deterministic_hash_code(text: str) -> int:
    """`StringHelper.GetDeterministicHashCode`（int，352654597 循环）。

    C# 的 `int` 运算在 unchecked 下按 32 位回绕；调用方最后把它转成 `uint`，
    所以这里全程按 2**32 取模即可，结果就是那个 uint 值。
    """
    units = text.encode("utf-16-le", errors="surrogatepass")
    text = [int.from_bytes(units[i:i + 2], "little") for i in range(0, len(units), 2)]
    num = 352654597
    num2 = num
    index = 0
    length = len(text)
    while index < length:
        num = (((num << 5) & MASK32) + num) & MASK32
        num ^= text[index]
        num &= MASK32
        if index == length - 1:
            break
        num2 = (((num2 << 5) & MASK32) + num2) & MASK32
        num2 ^= text[index + 1]
        num2 &= MASK32
        index += 2
    return (num + num2 * 1566083941) & MASK32


def snake_case(text: str) -> str:
    """`StringHelper.SnakeCase`：`CamelCaseRegex().Replace(text.Trim(), "$1_$2").ToLowerInvariant()`。

    正则 `([A-Za-z0-9]|\\G(?!^))([A-Z])` 里的 `\\G` 表示"紧接上一次匹配结束的位置"——
    Python 的 `re` 没有 `\\G`，所以这里按同一语义手写扫描（连续大写字母要各自断开：
    `ABC` → `A_B_C`，而朴素的 `re.sub` 会给出 `A_BC`）。
    """
    text = text.strip()
    out: list[str] = []
    index = 0
    last_end = 0
    length = len(text)
    while index < length:
        char = text[index]
        is_alnum = char.isascii() and (char.isalnum())
        if is_alnum and index + 1 < length and "A" <= text[index + 1] <= "Z":
            out.append(char)
            out.append("_")
            out.append(text[index + 1])
            index += 2
            last_end = index
            continue

        if "A" <= char <= "Z" and index == last_end and index > 0:
            out.append("_")
            out.append(char)
            index += 1
            last_end = index
            continue

        out.append(char)
        index += 1

    return "".join(out).lower()


# `RunRngType`（Entities/Rngs/RunRngType.cs）与 `PlayerRngType`（Entities/Rngs/PlayerRngType.cs）
# 的成员顺序就是 Enum.GetValues 的顺序，流名字取 SnakeCase(枚举名)。
RUN_RNG_TYPES = [
    "UpFront", "Shuffle", "UnknownMapPoint", "CombatCardGeneration",
    "CombatPotionGeneration", "CombatCardSelection", "CombatEnergyCosts",
    "CombatTargets", "MonsterAi", "Niche", "CombatOrbs", "TreasureRoomRelics",
]
PLAYER_RNG_TYPES = ["Rewards", "Shops", "Transformations"]


def run_seed(string_seed: str) -> int:
    """`RunRngSet.cs:107`：`Seed = (uint)StringHelper.GetDeterministicHashCode(seed)`。"""
    return deterministic_hash_code(string_seed) & MASK32


def stream_seed(set_seed: int, type_name: str) -> int:
    """`RunRngSet.cs:115-119` + `Rng.cs:52-55`：`new Rng(Seed, SnakeCase(枚举名))`
    等价于 `MegaRandom(Seed + (uint)GetDeterministicHashCode(SnakeCase(枚举名)))`。"""
    return (set_seed + deterministic_hash_code(snake_case(type_name))) & MASK32


def player_seed(string_seed: str, slot_index: int) -> int:
    """`Player.cs:326`：`(uint)(GetDeterministicHashCode(seed) + GetPlayerSlotIndex(this))`。"""
    return (deterministic_hash_code(string_seed) + slot_index) & MASK32


def content_seed(run_seed_value: int, slot_index: int, entry: str, mixin: int = 0) -> int:
    """`Rng.cs:47-50`：`(uint)((int)Seed + slot + GetDeterministicHashCode(entry)) + mixin`。"""
    return ((run_seed_value + slot_index + deterministic_hash_code(entry)) & MASK32) + mixin & MASK32


def layout_vectors() -> dict:
    """字符串 seed → run/player/内容流布局的已知向量（ENV-01 的 base 侧部分）。"""
    seeds = ["ENV-IRONCLAD-001", "", "1", "ABC", "abc", "seed", "SEED-123", "斩杀尖塔2"]
    run_streams: dict = {}
    player_streams: dict = {}
    for string_seed in seeds:
        seed = run_seed(string_seed)
        entry = {
            "string_seed": string_seed,
            "run_seed": seed,
            "streams": {},
            "player_seed": player_seed(string_seed, 0),
            "player_streams": {},
        }
        for type_name in RUN_RNG_TYPES:
            entry["streams"][type_name] = stream_seed(seed, type_name)
        for type_name in PLAYER_RNG_TYPES:
            entry["player_streams"][type_name] = stream_seed(entry["player_seed"], type_name)

        # 端到端：按布局算出的 Shuffle 流种子，真的抽几次 + 洗一副 10 张的牌。
        # 这条把"布局"和"抽样"接起来——只要哪一层读错，这里就对不上。
        shuffle_rng = Rng(entry["streams"]["Shuffle"])
        entry["shuffle_stream_first4"] = [shuffle_rng.next_int(100) for _ in range(4)]
        entry["shuffle_stream_deck"] = shuffle_rng.shuffle([f"card-{i:02d}" for i in range(10)])
        entry["shuffle_stream_counter"] = shuffle_rng.counter

        run_streams[string_seed] = entry

    return {
        "oracle": "tools/rng-oracle/vanilla_rng_oracle.py",
        "source": [
            "sts2/src/Core/Runs/RunRngSet.cs:104-119",
            "sts2/src/Core/Random/PlayerRngSet.cs:33-47",
            "sts2/src/Core/Entities/Players/Player.cs:324-328",
            "sts2/src/Core/Random/Rng.cs:47-55",
            "sts2/src/Core/Helpers/StringHelper.cs:GetDeterministicHashCode/SnakeCase",
        ],
        "hash_of": {"ABC": deterministic_hash_code("ABC"), "abc": deterministic_hash_code("abc")},
        "snake_case_of": {name: snake_case(name) for name in RUN_RNG_TYPES + PLAYER_RNG_TYPES},
        "content_seeds": {
            entry: content_seed(run_seed("ENV-IRONCLAD-001"), 0, entry)
            for entry in ["STRIKE_IRONCLAD", "BASH", "FEEL_NO_PAIN", "CALCIFIED_CULTIST"]
        },
        "seeds": run_streams,
    }


def emit_layout_csharp(layout: dict, path: pathlib.Path) -> None:
    """把 **seed/流布局**向量写成 C# 向量表（base 侧的检查用它）。"""
    lines: list[str] = []
    lines.append("// 本文件由 tools/rng-oracle/vanilla_rng_oracle.py 生成——不要手改。")
    lines.append("// 重新生成：")
    lines.append("//   python tools/rng-oracle/vanilla_rng_oracle.py "
                 "--cs-layout mod-the-spire/base-spire/BaseSpire.Slice.Tests/Randomness/VanillaSeedLayoutVectors.cs")
    lines.append("//")
    lines.append("// 只按只读原版源码的**独立重写**生成：")
    for source in layout["source"]:
        lines.append(f"//   - {source}")
    lines.append("")
    lines.append("namespace BaseSpire.Slice.Tests;")
    lines.append("")
    lines.append("/// <summary>ENV-01 的 base 侧已知向量：字符串 seed → run/player/内容流种子，以及该布局下的真实读数。</summary>")
    lines.append("internal static class VanillaSeedLayoutVectors")
    lines.append("{")
    lines.append("    internal sealed record LayoutVector(")
    lines.append("        string StringSeed,")
    lines.append("        uint RunSeed,")
    lines.append("        uint PlayerSeed,")
    lines.append("        (string Type, uint Seed)[] RunStreams,")
    lines.append("        (string Type, uint Seed)[] PlayerStreams,")
    lines.append("        uint[] ShuffleStreamFirst4,")
    lines.append("        string[] ShuffleStreamDeck,")
    lines.append("        int ShuffleStreamCounter);")
    lines.append("")
    lines.append("    public static readonly LayoutVector[] Layout =")
    lines.append("    [")
    for key in sorted(layout["seeds"]):
        entry = layout["seeds"][key]
        streams = ", ".join(f'("{name}", {seed}U)' for name, seed in sorted(entry["streams"].items()))
        player_streams = ", ".join(f'("{name}", {seed}U)'
                                   for name, seed in sorted(entry["player_streams"].items()))
        draws = ", ".join(f"{value}U" for value in entry["shuffle_stream_first4"])
        deck = ", ".join(f'"{card}"' for card in entry["shuffle_stream_deck"])
        lines.append(f'        new("{entry["string_seed"]}", {entry["run_seed"]}U, {entry["player_seed"]}U, '
                     f'[{streams}], [{player_streams}], [{draws}], [{deck}], '
                     f'{entry["shuffle_stream_counter"]}),')
    lines.append("    ];")
    lines.append("")
    lines.append("    /// <summary>内容流种子（`Rng.cs:47-50`）：run seed + 槽位 + 内容 entry 的哈希。</summary>")
    lines.append("    internal sealed record ContentVector(string Entry, int Slot, uint Seed);")
    lines.append("")
    lines.append("    public static readonly ContentVector[] Content =")
    lines.append("    [")
    for entry_name, seed in sorted(layout["content_seeds"].items()):
        lines.append(f'        new("{entry_name}", 0, {seed}U),')
    lines.append("    ];")
    lines.append("")
    lines.append("    /// <summary>SnakeCase（流名字用）定点样本：偏差出在命名层还是哈希层，靠它区分。</summary>")
    lines.append("    public static readonly (string Input, string Snake)[] SnakeCases =")
    lines.append("    [")
    for name, snake in sorted(layout["snake_case_of"].items()):
        lines.append(f'        ("{name}", "{snake}"),')
    lines.append("    ];")
    lines.append("")
    lines.append("    public const uint RunSeedOfEnvIronclad001 = "
                 + f'{layout["seeds"]["ENV-IRONCLAD-001"]["run_seed"]}U;')
    lines.append("}")
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def deterministic_hash_code_old(text: str) -> int:
    """`StringHelper.GetDeterministicHashCodeOld`（参考检出里的旧 31 位哈希）。

    算法与 `sts2/` 的 `GetDeterministicHashCode` **逐字相同**：参考把旧实现改名为 `...Old`，
    并把无后缀的那个换成了 XxHash64（见 `reference/sts2-sim/.../Helpers/StringHelper.cs:36-71`）。
    这里单独写一条，用来为「`old` 前缀解释差异」提供独立向量。
    """
    return deterministic_hash_code(text)


# ENV-T04 的**窄源版本兼容 Profile** 涉及的字符串 seed。它就是 suite 里
# `rng-initial-shuffle` 声明的 `game_seed`，两侧必须用**同一个原始字符串**。
SOURCE_COMPAT_CASE_SEED = "ENV-T04-RNG-SHUFFLE-001"

# `sts2/src/Core/Models/Characters/Ironclad.cs:43-55` 的起始牌组声明顺序：
# 5 张 StrikeIronclad、4 张 DefendIronclad、1 张 Bash。
IRONCLAD_STARTING_DECK = (
    ["strike-ironclad"] * 5 + ["defend-ironclad"] * 4 + ["bash"]
)

# ENV §8 要求「参考不同诊断名称通过**显式流映射**对齐」。这是原版 `RunRngType` → 引擎流名的
# 映射表（与 base 的 `VanillaRandomLayout.EngineStreamName` 同一张表，这里独立重写一遍）：
# 只有 `Shuffle` / `CombatTargets` / `CombatCardSelection` 落在内容包已有的战斗流上，
# 其余 run 级流各占一条 `base-spire:run:<snake>`。
ENGINE_STREAM_NAMES = {
    "Shuffle": "base-spire:combat-draw",
    "CombatTargets": "base-spire:random-target",
    "CombatCardSelection": "base-spire:random-card",
}


def engine_stream_name(type_name: str) -> str:
    if type_name in ENGINE_STREAM_NAMES:
        return ENGINE_STREAM_NAMES[type_name]
    return "base-spire:run:" + snake_case(type_name)


def source_compat_vectors(string_seed: str = SOURCE_COMPAT_CASE_SEED) -> dict:
    """源版本兼容 Profile 的独立已知向量（ENV-T04 源随机包）。

    只覆盖**已裁定允许替换**的三类派生差异：
      1. 完整游戏 seed 的 hash32（`sts2/src/Core/Runs/RunRngSet.cs:107`）；
      2. 命名流的 hash32 + **uint 加法回绕**（`Rng.cs:52-55`）；
      3. `old` 前缀的解释（`sts2/` 没有该分支；参考会去掉前缀并用旧哈希）。

    `MegaRandom`（xoshiro256**）、抽样映射与游戏效果**不在**这里，也不允许被替换。
    每个涉及流的 seed、抽样调用以及第一次洗牌的排列与计数都给出，便于运行时逐值核对。
    """
    seed = run_seed(string_seed)
    streams = {name: stream_seed(seed, name) for name in RUN_RNG_TYPES}
    player = player_seed(string_seed, 0)
    player_streams = {name: stream_seed(player, name) for name in PLAYER_RNG_TYPES}

    # 端到端：用**布局算出的** Shuffle 流种子做一次真正的开局洗牌（10 张起始牌组）。
    # 这一条把「剖出种子」与「真的洗牌」接起来：任一层读错都会在这里露出来。
    shuffle = Rng(streams["Shuffle"])
    deck_after = shuffle.shuffle(IRONCLAD_STARTING_DECK)
    shuffle_draws = len(IRONCLAD_STARTING_DECK) - 1

    # 其余在首次洗牌之前会被消费的流：给出前几次真实读数，用于核对「前置消费的位置与计数」。
    def first_draws(type_name: str, count: int) -> list[int]:
        rng = Rng(streams[type_name])
        return [rng.next_int(100) for _ in range(count)]

    old_seed = "old" + string_seed
    return {
        "oracle": "tools/rng-oracle/vanilla_rng_oracle.py",
        "profile_id": "source-compat-sts2-frozen@1",
        "source": [
            "sts2/src/Core/Runs/RunRngSet.cs:104-119",
            "sts2/src/Core/Random/Rng.cs:28-55",
            "sts2/src/Core/Random/MegaRandom.cs",
            "sts2/src/Core/Helpers/StringHelper.cs:GetDeterministicHashCode/SnakeCase",
        ],
        "replaces": [
            "run seed hash32（完整字符串，31 位循环）",
            "命名流 hash32 + uint 加法回绕",
            "old 前缀解释（源无该分支）",
        ],
        "not_replaced": [
            "MegaRandom xoshiro256** 与 Splitmix64 播种",
            "抽样映射（NextInt/NextDouble/Shuffle 等）",
            "卡牌/敌人/地图/奖励效果",
        ],
        "string_seed": string_seed,
        "run_seed": seed,
        "streams": streams,
        "stream_engine_names": {name: engine_stream_name(name) for name in RUN_RNG_TYPES},
        "player_seed": player,
        "player_streams": player_streams,
        "first_shuffle": {
            "stream": "Shuffle",
            "engine_stream": engine_stream_name("Shuffle"),
            "deck_before": list(IRONCLAD_STARTING_DECK),
            "deck_after": deck_after,
            "draws": shuffle_draws,
            "counter": shuffle.counter,
        },
        "pre_shuffle_first_draws": {
            "UpFront": first_draws("UpFront", 8),
            "Shuffle": first_draws("Shuffle", shuffle_draws),
            "Niche": first_draws("Niche", 4),
        },
        "old_prefix": {
            "string_seed": old_seed,
            "source_run_seed": run_seed(old_seed),
            "reference_native_run_seed": deterministic_hash_code_old(old_seed[len("old"):]),
            "note": "源没有 old 分支：整串参与哈希；参考会去掉 'old' 再走旧哈希。两者必须能被区分。",
        },
        "hash_of": {
            string_seed: deterministic_hash_code(string_seed),
            old_seed: deterministic_hash_code(old_seed),
        },
        "snake_case_of": {name: snake_case(name) for name in RUN_RNG_TYPES + PLAYER_RNG_TYPES},
    }


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="原版随机数独立神谕（ENV-01 已知向量生成器）")
    parser.add_argument("--json", default=None, help="把向量写成 JSON")
    parser.add_argument("--cs", default=None, help="把读数向量写成 C# 向量表（Engine.Tests 用）")
    parser.add_argument("--cs-layout", default=None, help="把布局向量写成 C# 向量表（Slice.Tests 用）")
    parser.add_argument("--layout", default=None, help="把 seed/流布局向量写成 JSON")
    parser.add_argument("--source-compat", default=None,
                        help="把 ENV-T04 窄源版本 Profile 的独立向量写成 JSON")
    args = parser.parse_args(argv)

    data = vectors()
    text = json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True)
    if args.cs:
        emit_csharp(data, pathlib.Path(args.cs))
        print(f"vectors written: {args.cs}")

    if args.cs_layout:
        emit_layout_csharp(layout_vectors(), pathlib.Path(args.cs_layout))
        print(f"vectors written: {args.cs_layout}")

    if args.json:
        pathlib.Path(args.json).write_text(text + "\n", encoding="utf-8")
        print(f"vectors written: {args.json}")

    if args.layout:
        layout = json.dumps(layout_vectors(), ensure_ascii=False, indent=2, sort_keys=True)
        pathlib.Path(args.layout).write_text(layout + "\n", encoding="utf-8")
        print(f"vectors written: {args.layout}")

    if args.source_compat:
        compat = json.dumps(source_compat_vectors(), ensure_ascii=False, indent=2, sort_keys=True)
        pathlib.Path(args.source_compat).write_text(compat + "\n", encoding="utf-8")
        print(f"vectors written: {args.source_compat}")

    if not args.json and not args.cs and not args.cs_layout and not args.layout and not args.source_compat:
        print(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
