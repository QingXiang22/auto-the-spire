# 遗物卡牌打出触发与四件消费者

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批新增“一张牌打出之后”的遗物触发点与牌型过滤，并把四件只依赖它的遗物纳入正式内容。归属：DSH。

## 新能力

1. **触发点 `after-card-played`**（源 `RelicModel.AfterCardPlayed`）。复用能力侧已经存在的 `TriggerAfterCardPlayed` 过程（`DamageCompilation.PlayedTriggerBody`），在它尾部追加遗物监听，因此遗物与能力看到的是同一个“打出之后”时点。
2. **牌型过滤 `CardKind`**（空／Attack／Skill／Power）。声明里写 `CardKind` 才要求打出牌的 `Card.Kind` 相等；没有传牌上下文时该效果不执行（`RelicCompilation.Condition`）。
3. 每回合计数复用既有 `set-counter`（在 `before-side-turn-start` 清 0）与 `add-counter`（`CounterModulo` 取模），不需要新的计数器原语。

## 四件消费者（源依据）

| 遗物 | 稀有度 | 源行为 | 本批声明 |
| --- | --- | --- | --- |
| Nunchaku | Uncommon | `Nunchaku.cs:88-100`：任意来源为拥有者的 Attack 牌打出后计数（`AttacksPlayed` 是 `SavedProperty`，跨战斗保留），每 10 次获得 1 能量 | `after-card-played`＋`CardKind=Attack`：`add-counter` 1（模 10）与 `gain-energy` 1（`CounterEquals=0`） |
| Kunai | Rare | `Kunai.cs:90-99,101+`：`BeforeSideTurnStart` 清零、每 3 张 Attack 给 1 层 Dexterity | `before-side-turn-start`＋`set-counter` 0；`after-card-played`＋`CardKind=Attack`：计数模 3，`CounterEquals=0` 时 `apply-power` Dexterity 1 |
| Shuriken | Rare | `Shuriken.cs:90-110`：同 Kunai，改为 1 层 Strength | 同上，`apply-power` Strength 1 |
| OrnamentalFan | Rare | `OrnamentalFan.cs:40-44,90-103`：同计数，第 3 张给 4 点 Unpowered 格挡 | 同上，`CounterEquals=0` 时 `gain-block` 4 |

四件的资格都写入 `relic-source-catalog.json`（`Eligibility=singleplayer` 与逐条源依据），不再是 `unreviewed`。

## 修改边界

`Relics/SliceRelics.cs`（触发点、`CardKind` 与参数校验）、`Relics/Compilation/RelicCompilation.cs`（`DispatchCard` 与牌型条件）、`Combat/Compilation/DamageCompilation.cs`（打出后过程追加遗物监听）、`Composition/CombatPackageComposer.cs`（接线牌型比较）、`Relics/Content/relics.json`、`Relics/Content/relic-source-catalog.json`、`BaseSpire.Slice.Tests/Relics/RelicContentChecks.cs`（四个场景）与身份基线文件。未改 core、net 协议或独立参考。

## 命令与结果

```powershell
dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-restore
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --public-semantics-only
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-lifecycle-only
```

- 构建 0 警告 0 错误；遗物内容组 **72/72 通过**，其中新增四个场景分别断言“两次不触发／第三次触发”“跨回合计数重置”“计数跨战斗保留、第九次不给、第十次给 1 能量并归零”。
- 覆盖报告：**73/73 可执行**、source=297、**unimplemented 由 227 降到 223**，校验器 exit 0。
- 公开语义 10/10、遗物生命周期与 Run 14/14。
- 内容变更导致身份再次迁移：dev `daa13b3bc7f1567c`／`cee2fcf4a4a63f5a`、dev-map `bf88d25972f155d9`／`9a5285f99cadfca6`（上一版 `2b67d3cf8a4731f4`／`e3f4afd5fb459d40`、`84b4dbd80cb769ea`／`42889d7374f76501` 记录在注释里）。构建描述里的 `unsupported` 由 346 降到 342，与四件遗物进入正式内容一致。

## 未验证与下一步

只取受控内容场景证据：没有独立双侧、正常整局获得或公开 Host 轨迹，完整 Engine／Slice／net／严格回归仍交给 GitHub Actions。Kunai／Shuriken／OrnamentalFan 的源 `ShowCounter` 只在战斗中为真，当前声明是静态显示，这一表现层差异明确保留、不影响玩法。LetterOpener 与 CharonsAshes 尚未实现，它们需要“对全体敌人造成无加成伤害”的效果种类与 `after-card-exhausted` 触发点，作为下一批（下一已就绪成果）。
