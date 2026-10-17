# 遗物批次尝试记录：回合末弃牌不经过逐张弃牌钩子

任务：**ENV-T09-PR02-CONTENT**。本记录含两段：先记录“回合末手牌处置不经过逐张弃牌钩子”的发现与两次失败尝试；再记录 2026-10-18 用**最小复现**定位后，以“只接遗物监听”的方式落地 ToughBandages（Rare，Silent，`ToughBandages.cs:12-24`：每弃一张牌获得 3 点 Unpowered 格挡）的结果与仍然开放的范围。

## 已解决：只接遗物监听即可覆盖回合末弃牌（2026-10-18）

最小复现做法：在 `TurnCompilation.KeepOrDiscard` 增加 `afterDiscard` 回调、`Discard()` 里只追加**遗物派发**（`relicPrograms.Dispatch(scope, AfterCardDiscarded)`），composer 从 `KeepOrDiscard` 闭包传入；不加 `QueueSly`/`DispatchCardHook`。结果包正常链接，场景通过：“手牌 5 张在回合末被弃 → 3×5=15 点格挡被 SturdyClamp 截到 10；预先空手（受控移动，不经过弃牌回调）→ 0 点”。

因此前面两次尝试的**栈溢出不是遗物派发本身**，而是同时塞进 `QueueSly` 与 `DispatchCardHook(on_discard)` 的组合（或那次的过程符号/体顺序）；现在遗物侧已经可用，`on_discard` 与 Sly 入队在回合末仍未接入，属于**仍然开放**的引擎缺口，留给后续批次单独用同样的小步方式验证。

覆盖与身份：遗物内容组 84/84、覆盖 85/85 可执行、未实现 211；dev `d38b36c7be4c7782`／`02ba7ba8bc64ff3a`、dev-map `cd59bdfde4209734`／`ae0107d8ff66a60c`。

## 历史：第一次发现（2026-10-17）

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

## 第二次尝试（2026-10-18）与失败记录

按上面的位置实际接了三段钩子（`KeepOrDiscard` 增加 `afterDiscard` 回调、`Discard()` 里依次做 `QueueSly → on_discard → 遗物`，composer 传 `AfterDiscard`），并写好了 ToughBandages 的声明、目录资格与场景。三次失败与结论：

1. 直接把 `relicPrograms.Dispatch(scope, AfterCardDiscarded)` 写进 `Discard()`：链接期 **栈溢出**，栈在 `CoreTheSpire.Engine.IrTypeChecker.CheckExprCore`，说明生成的 IR 里有自引用或过深的表达式，不是普通校验失败。
2. 改成“独立过程符号 + 稍后补过程体”后先遇到 **NullReferenceException**：回合末手牌处置体在 `CardPlay` **之前**编译，过程符号若在 `CardPlay` 附近创建就还是 null。把符号创建提前到方法开头的可空局部声明处可以消除该空引用。
3. 即使符号提前创建、过程体只设一次，仍然**栈溢出**（同样在 `IrTypeChecker`）。因此问题不在符号时序，而在 `Dispatch` 生成的指令被放进 `KeepOrDiscard` 的递归 `Branch` 时形成的 IR 形状；下一步应先对照能正常工作的 `RelicAfterDamageReceived`（同样是“先建符号、后设体、由受伤分支调用”），确认差异是否来自 `QueueSly`/`DispatchCardHook` 的上下文或 `Branch` 的重复展开。

该次尝试的全部改动已回退，仓库回到 `85130e7` 状态：遗物内容组 82/82、覆盖 83/83 可执行、未实现 213（校验器 exit 0）。场景设计可以直接复用：同场带 SturdyClamp 观察“每弃一张 +3、保留上限 10”，再用 `EmptyHand` 的空手回合做负例。

## 下一步建议

先做一个**最小复现**：只加遗物监听（不加 `QueueSly`/`on_discard`）看是否仍溢出；再逐步加回，定位是哪一段让 IR 退化。定位后再一次性接入三段钩子并纳入 ToughBandages／Tingsha。



## 边界

该批未产生任何提交或内容变更；`relics.json`、`relic-source-catalog.json`、`SliceRelics.cs`、`CardCompilation.cs`、`CombatPackageComposer.cs` 与检查文件均已回退到 `31d3aa9` 的状态。没有新增独立双侧或公开证据，也没有宣称 ToughBandages 已实现。
