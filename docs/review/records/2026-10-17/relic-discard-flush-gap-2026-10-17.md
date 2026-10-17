# 遗物批次尝试记录：回合末弃牌不经过逐张弃牌钩子

任务：**ENV-T09-PR02-CONTENT**。本轮尝试用新加的 `after-card-discarded` 触发点纳入 ToughBandages（Rare，Silent，`ToughBandages.cs:12-24`：每弃一张牌获得 3 点 Unpowered 格挡），实现与场景都写完并编译通过，但受控场景实测“回合末手牌处置后格挡为 0”，说明**回合末的手牌处置不会经过逐张弃牌钩子**。该批改动已全部回退（构建与覆盖恢复：遗物内容组 82/82、覆盖 83/83、未实现 213），本记录只保留发现与下一步。

## 现象与定位

- 逐张弃牌钩子在 `CardCompilation.CardPiles(...)` 的丢弃回调里：先排 Sly 队列、再跑卡的 `on_discard`、最后是遗物监听；`ToughBandages` 应该在同一处结算。
- 回合末的手牌处置走另一条路径：它调用批次级的能力钩子（`AfterDiscardBatch`）与消耗触发过程 `TriggerAfterExhaust`（所以 CharonsAshes 的“Ethereal 消耗→群体伤害”能看到效果），但**不逐张调用**上面那个丢弃回调。
- 因此：`after-card-discarded` 若只在逐张回调里派发，就覆盖不到“回合末弃掉整手牌”这一最常见的触发源；同理，卡自身的 `on_discard` 钩子在回合末处置时也不会触发，这是一个既有的引擎缺口（不是本批引入）。

## 下一步（已就绪）

1. 先定位回合末处置的实现（`EndTurnHandSteps`／`CardPiles` 的批量移动），确认它是否有牌引用可用于逐张派发；
2. 在该路径上逐张派发 `on_discard` 与 `after-card-discarded`，并保持与 Sly／Ethereal 的现有顺序（先 Sly 入队、再 on_discard、再遗物），避免改变既有 Sly 用例的时序；
3. 补齐后重新纳入 ToughBandages，用 SturdyClamp 保留格挡观察“每弃一张 +3、上限 10”，再用空手回合做负例；
4. 同一路径也可顺带核对 `on_exhaust` 是否也需要逐张派发（当前消耗触发走的是批次过程）。

### 已定位的准确位置

- `TurnCompilation.EndTurnHandSteps`（`TurnCompilation.cs:454-488`）：Ethereal 分支已经逐张派发 `on_exhaust` 并调用 `TriggerAfterExhaust`，**只有非 Ethereal 的弃牌分支缺钩子**。
- 缺钩子的是 `KeepOrDiscard`（`TurnCompilation.cs:490-531`）里的 `Discard()`：它只做 `RemoveFromCollection(Hand)` + `AddToCollection(DiscardPile)`，既没有 `PowerRules.QueueSly`，也没有 `DispatchCardHook(HookOnDiscard)`，更没有遗物监听。
- 要补齐的链与 `CardCompilation.CardPiles` 的丢弃回调同形：`QueueSly → DispatchCardHook(on_discard) → 遗物 after-card-discarded`。`KeepOrDiscard` 目前拿不到这三样，需要把回调从 `EndTurnHandSteps`（它已有 `DispatchCardHook` 与 `powerRulesBindings`）与 composer（可传 `relicPrograms.Dispatch`）传进来。
- 注意保留/虚无语义不变：`Keep()` 分支与 Ethereal 分支的顺序不要改；`retainHand` 的判定仍在 `Branch` 之前。


## 边界

该批未产生任何提交或内容变更；`relics.json`、`relic-source-catalog.json`、`SliceRelics.cs`、`CardCompilation.cs`、`CombatPackageComposer.cs` 与检查文件均已回退到 `31d3aa9` 的状态。没有新增独立双侧或公开证据，也没有宣称 ToughBandages 已实现。
