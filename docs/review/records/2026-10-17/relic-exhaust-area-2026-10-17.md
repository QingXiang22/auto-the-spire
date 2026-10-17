# 遗物消耗触发与无加成群体伤害

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批新增“一张牌被消耗之后”的遗物触发点与“对全体敌人造成无加成伤害”的效果种类，并把两件只依赖它们的遗物纳入正式内容。归属：DSH。

## 新能力

1. **触发点 `after-card-exhausted`**（源 `RelicModel.AfterCardExhausted`）。复用能力侧已有的 `TriggerAfterCardExhausted` 过程（`DamageCompilation.ExhaustTriggerBody`），在它尾部追加遗物监听，因此遗物与能力共享同一个消耗时点（回合末手牌处置与效果消耗两条路径都用该过程）。
2. **效果种类 `damage-all-enemies`**：对所有存活敌人逐个调用与卡牌、充能球同一个 `DamageMonster` 命令，`powered = false` 对应源里的 `ValueProp.Unpowered`；伤害量走既有 `Amount`（当前为常量），不再另写伤害管线。

## 两件消费者（源依据）

| 遗物 | 稀有度 | 源行为 | 本批声明 |
| --- | --- | --- | --- |
| LetterOpener | Uncommon | `LetterOpener.cs:39-43,109-118`：每回合第 3 张 Skill 打出后，对全体可命中敌人造成 5 点 Unpowered 伤害 | `before-side-turn-start` 清零；`after-card-played`＋`CardKind=Skill` 计数模 3；`CounterEquals=0` 时 `damage-all-enemies` 5 |
| CharonsAshes | Rare（Ironclad 池） | `CharonsAshes.cs:15-24`：任意来源为拥有者的牌被消耗后，对全体可命中敌人造成 3 点 Unpowered 伤害 | `after-card-exhausted` → `damage-all-enemies` 3 |

两件的资格与源依据写入 `relic-source-catalog.json`（`Eligibility=singleplayer`）。

## 修改边界

`Slice/SliceRelics.cs`、`Slice/Compilation/RelicCompilation.cs`、`Slice/Compilation/DamageCompilation.cs`、`Slice/Compilation/CombatPackageComposer.cs`、`ContentSource/relics.json`、`ContentSource/relic-source-catalog.json`、`BaseSpire.Slice.Tests/RelicContentChecks.cs` 与身份基线文件。未改 core、net 协议或独立参考。

## 命令与结果

```powershell
dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-restore
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --public-semantics-only
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-lifecycle-only
```

- 构建 0 错误（4 项既有警告与上一批相同）；遗物内容组 **74/74 通过**。
- LetterOpener 场景：2 张 Skill 不掉血、第 3 张按“5 × 存活敌人数”掉血、给出 5 层 Strength 后伤害不变（验证 Unpowered）、下一回合计数重新开始。
- CharonsAshes 场景：没有消耗的回合不掉血、Ethereal 的 Dazed 在回合末被消耗后按“3 × 存活敌人数”掉血、给出 Strength 后伤害不变。
- 覆盖报告：**75/75 可执行**、source=297、**unimplemented 由 223 降到 221**，校验器 exit 0；构建描述 `unsupported` 由 342 降到 340。
- 公开语义 10/10、遗物生命周期与 Run 14/14。
- 身份迁移：dev `28ac6d84bc73f96d`／`9b1e393422bebc75`、dev-map `83238c8da492af09`／`70132d1bb1df4034`（上一版 `daa13b3bc7f1567c`／`cee2fcf4a4a63f5a`、`bf88d25972f155d9`／`9a5285f99cadfca6` 记录在注释里）。

## 未验证与下一步

只有受控内容场景证据：没有独立双侧、正常整局获得或公开 Host 轨迹，完整 Engine／Slice／net／严格回归仍交 GitHub Actions。`after-card-exhausted` 目前不区分消耗者身份，单人对局下等价于源的“拥有者为本玩家”判断；多人语义未建模。下一已就绪成果是继续从剩余 221 件源遗物里挑一组已能表达的能力（例如 `ModifyDamageAdditive` 的 StrikeDummy／MiniatureCannon／MysticLighter，或 `AfterPlayerTurnStart` 的 MercuryHourglass 需要先补群体伤害的回合触发组合）。
