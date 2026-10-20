# ENV-T04 随机初始化接续 — 2026-10-15

> 2026-10-19 整理：过时／撤销材料：本文不作为当前设计、接手或验收入口。原时点内容留作历史追溯。 当前任务见[主清单](../tofix/README.md)，后续架构见[内容与行为重构目标](../../design/mod-the-spire/content-behavior-refactor-goals.md)。


> 撤销说明：下文为已撤销操作的历史记录，所有修复结论和验收结果无效。用户指出 sts2-sim 是只读参考目标后，Codex 已恢复本轮修改的6个参考文件并删除新增接口，参考 git status 返回空；本轮对拍工具和神谕修改全部恢复。本次未交付有效随机数修复。`.audit/env-t04-20261015` 与 `.artifacts/env-t04-20261015` 的产物使用过修改版参考，不得用作验收、固定闭包或后续运行。后续必须保持参考只读。

本次让正常参考 Run 在首次抽数前绑定冻结源派生，消除两例初始地图分叉和 seed L 的 UpFront 抽数差异。父项尚未验收，当前状态见[主清单](../tofix/README.md#env-t04)。

## 接手入口与剩余工作

复用 `RunState.CreateSequentialForNewRun` 和工具 `RngSourceCompat.SequentialDerivation`。正常用例保留原 seed、动作和维度；下一步从 `.audit/env-t04-20261015/normal-tags/regression.json` 的首差核对战斗语义，不补抽、不删除流或改 seed。原完成条件仍以主清单及 ENV 合同为准。

## 改动与依据

- 参考新增独立 sequential bootstrap，绑定根、run/player 具名流及地图 seed；玩家及袋、房间预抽先于地图生成。默认参考构造保持原行为。CloneExact 保留派生，现有无法携带身份的存档和 reseed 路径拒绝使用该 run。
- 冻结源 `StringHelper`、`RunRngSet`、`PlayerRngSet`、`RunManager` 是派生及顺序依据。该入口不宣称动态事件/怪物域完整兼容。
- 冻结源 `SlitheringStranglerNormal` 无标签，`SnappingJaxfruitNormal` 仅 Mushroom；修正参考额外标签后，seed L 的 UpFront 297 次变为与 base 一致的 293 次。
- 观察器登记已映射对象，避免把同一流重复计为动态流；正常累计比较仅包含实际消费流，地图明确映射。长期 HP 投影只在真实胜利结算后更新。
- 修改包含嵌套参考仓库，交付时须同时保存参考工作树，父仓库不会自动收录其改动。

## 已执行证据

工作目录为仓库根。构建：`dotnet build tools/differential/Differential.csproj --artifacts-path .artifacts/env-t04-20261015`，rc0。参考有3处既有 DisplayAmount 隐藏成员警告。

运行入口：`dotnet .artifacts/env-t04-20261015/bin/Differential/debug/Differential.dll`。

- `test --suite regression --output .audit/env-t04-20261015/regression`：rc0，17执行，16双侧 passed、1 self_checked，22自检无失败。此结果在标签修正前，最终结果另见 final-regression。
- `test --suite normal-reward-prefix --output .audit/env-t04-20261015/normal-tags`：rc1，2例实际双侧执行，自检无失败。take 首差 boundary11：base 已进入奖励，参考仍在战斗；skip 首差 boundary8：base 保留0层易伤，参考已移除。初始地图和 UpFront 流均不再是首差。

未验证：动态怪物种子表达式、完整内容/多房间、共同动作绑定、参考全量测试与真实客户端。不能将本包称作正常奖励或完整 ENV-T04 通过。

最终检查：`final-regression` rc0，17执行（16双侧通过、1工厂自检），22自检无失败。随机向量初跑暴露神谕生成器三条旧流名，按现有 SliceManifest 常量修正名称并重新生成；数值算法不变。文档检查扫描501篇、0问题；两个仓库的 `git diff --check` 均 rc0。
