# rng-oracle：原版随机数的独立神谕

ENV-01 的已知向量来源。它**只按只读原版源码重写**，不参考本仓库任何 C# 实现——
否则"被测实现 vs 对照实现"会变成同一份代码自己撞自己。

```powershell
# 仓库根目录
python tools/rng-oracle/vanilla_rng_oracle.py --json artifacts/environment/vanilla-rng-vectors.json `
  --layout artifacts/environment/vanilla-rng-layout.json `
  --cs core-the-spire/Core.TheSpire.Engine.Tests/VanillaRandomVectors.cs `
  --cs-layout mod-the-spire/base-spire/BaseSpire.Slice.Tests/VanillaSeedLayoutVectors.cs
```

`--cs` / `--cs-layout` 直接**生成** C# 向量表而不是手抄：转录错误在这里是不可接受的（一个数字抄错，
检查就从"没通过"变成"通过了错的实现"）。生成的文件里带生成命令，重新生成即可。
分两个文件是因为判据分属两个项目：读数向量给 `Core.TheSpire.Engine.Tests`，
布局向量给 `BaseSpire.Slice.Tests`（布局实现落在 base 侧）。

## 两层向量

| 层 | 内容 | 谁检查 |
| --- | --- | --- |
| **读数** | 原始字、`NextInt(100)`、`NextInt(1,7)`、`NextDouble`、10 张牌洗牌排列、消耗计数 | `Core.TheSpire.Engine.Tests` |
| **布局** | 字符串 seed → run seed / player seed / 12 条 run 流 + 3 条玩家流 + 内容流种子、SnakeCase、以及按布局抽出的 Shuffle 流读数与洗牌 | `BaseSpire.Slice.Tests` |

## 它按什么写（布局层）

| 原版位置 | 语义 |
| --- | --- |
| `Runs/RunRngSet.cs:104-119` | `Seed = (uint)GetDeterministicHashCode(seed)`；每条流 `new Rng(Seed, SnakeCase(枚举名))` |
| `Random/Rng.cs:52-55` | `Rng(seed, name)` = `seed + (uint)GetDeterministicHashCode(name)` |
| `Entities/Players/Player.cs:324-328` | 玩家级 seed = `hash(stringSeed) + 玩家槽位` |
| `Random/PlayerRngSet.cs:33-47` | 玩家级流同 run 级规则 |
| `Random/Rng.cs:47-50` | 内容级 `(uint)(runSeed + 槽位 + hash(内容 entry)) + mixin` |
| `Helpers/StringHelper.cs` | `GetDeterministicHashCode`（int 循环）、`SnakeCase`（CamelCase 拆分） |

## 已登记的参考偏离（重要）

`reference/sts2-sim` 的检出**领先于**我们冻结的 `sts2/` 基线，run/流种子派生已经不是同一版本：

| | 冻结基线 `sts2/`（判据） | 参考检出 |
| --- | --- | --- |
| `RunRngSet.Seed` | `uint`，`(uint)GetDeterministicHashCode`（int 循环） | `ulong`，`GetDeterministicHashCode` 返回 **XxHash64**（旧实现改名 `…Old`） |
| 流种子 | `Seed + (uint)hash(name)` | `Seed + hash(name)`（ulong）+ `CreateSemanticKeyRequired` 重键模式 |

因此：**不得**用参考的数值"对齐"我们的实现，也不得据此修改 `VanillaRandomLayout`。
`dotnet run --project tools/differential -- rng` 会把这条差异按**已登记偏离**分类并返回 0；
未登记的偏离返回 1（否则"参考又变了"会被静默吞掉）。ENV-00 需要把参考冻结到与 `sts2/` 同一版本，
或逐处登记此类差异。

## 三方互证

| 哪两方 | 谁做 | 覆盖 |
| --- | --- | --- |
| 我们的 Engine ↔ 神谕 | `Core.TheSpire.Engine.Tests` 的 `VanillaRandomChecks` | 原始字、有界映射、双精度、洗牌排列、消耗计数 |
| 我们的 base ↔ 神谕 | `BaseSpire.Slice.Tests` 的 `SliceVanillaSeedLayoutChecks` | SnakeCase、run/player/内容流种子、按布局抽出的 Shuffle 流读数与洗牌 |
| 神谕 ↔ 参考实现 | `tools/differential rng` | 读数层一致；布局层为**已登记参考偏离** |

参考实现是第三份独立实现，用来证明神谕没读偏；但它**不是权威**，裁决依据是只读的 `sts2/`（ENV §6.3）。

## 纪律

- 向量由脚本生成，**不手改**生成出来的 `.cs`。
- 原版语义只从 `sts2/` 读；参考实现只为交叉核对，不据其修改神谕。
- 抽样规则（截断 vs 拒绝采样、取最高位 vs 最低位）属于**行为差异**，不同 Profile 之间不得互换或"对齐"。
