# 源建局输入静态取证工具（ENV-T04 B 线）

当前规则依据已改为[锁定 sts2-sim](../../reference/README.md)。本页工具仍用于核对旧本地游戏源码与既有实现的迁移来源，不作为新版规则裁决或覆盖证明。新行为直接核对锁定源码，不能把本页旧源清单改标为新版。

遗物内容与公共接口的静态对账使用本页末尾的[遗物清单入口](#遗物内容与接口统计)。建局源／参考比较继续使用下面的原有入口。

`extract_source_manifest.py` 从**固定源** `sts2/`（基线 `sts2-frozen-2026-10-05`，core 锚点 `92ccd34`）
静态提取建局阶段的输入清单，并与 `reference/sts2-sim/` 逐项对照，供 A 线按真实机制实现前置消费。

- **只读**：不执行游戏、不构建 C#、不写 obj/bin、不改任何生产文件。
- **静态**：只解析源码声明。运行期遇袋重抽次数依赖 seed 与谓词拒绝，属运行行为，本工具不推断。
- **不静默少算**：解析不到就报 `unsupported`/`unsupported_parse_gap`，绝不把"一侧没解析出内容"读成相同。

## 用法

```powershell
python tools/source-manifest/extract_source_manifest.py `
    --out .artifacts/source-inputs/source-initialization-manifest.json `
    --markdown .artifacts/source-inputs/source-initialization-manifest.md
```

可选参数：`--source-root`（默认 `sts2`）、`--reference-root`（默认 `reference/sts2-sim`）、
`--game-seed`（默认 `ENV-T04-RNG-SHUFFLE-001`，仅做记录与标识绑定，不改变任何提取结果）。

退出码 0 = 提取并渲染成功（**不代表某条 `differs` 结论被裁决**）；非 0 = 源路径缺失或工具异常。

## 提取内容

- **遗物池**：共享池（`SharedRelicPool.GenerateAllRelics()` 声明顺序 + 每个遗物的 `RelicRarity`）、
  Ironclad 池、角色袋实际池（共享 ⊕ 角色，按 `RelicGrabBag._rarities={Common,Uncommon,Rare,Shop}` 过滤）、
  以及各桶洗牌消费量 `Σ(桶内项数 − 1)`。
- **事件池**：Act0 本幕事件 + `ModelDb.AllSharedEvents` 的全解锁有效池（顺序 = 声明顺序，洗牌消费 `n − 1`）。
- **遭遇池**：弱/常规/精英/BOSS 候选、`IsWeak`、`Tags`，以及普通槽总数（`BaseNumberOfRooms`）。
- **古代人**：本幕 `AllAncients` + `ModelDb.AllSharedAncients`，以及 Act0 的可用候选（Act0 不参与共享先古分配）。
- **调用顺序与条件**：共享袋 → 每玩家袋 → 共享先古分配 → 各 act 事件洗牌 → 弱/常规袋 → 精英 → BOSS → 古代人。
- **标识绑定**：源/参考的 git HEAD、逐文件 sha256、遗物池/遭遇目录聚合摘要、未提交改动列表。

## 与参考对照

`source_vs_reference` 对 9 类清单给出 `identical` / `differs` / `unsupported_parse_gap`，
`differs` 展开两侧计数、仅源/仅参考项、顺序差异，`encounter_tags` 还展开逐 ID 的 tag 差异。

## 边界

- 本工具属工具层，参考只读；不得把参考数值当作源的期望值写死进生产玩法，也不得用工具补偿生产实现。
- 本工具不修改 RNG 算法、不修改比较器、不参与严格 suite 判定。
- 源/参考版本标识以 JSON `identity` 为准；参考仓库 HEAD 不能替代实际文件摘要（存在共享未提交改动）。

## 遗物内容与接口统计

`extract_relic_manifest.py`只读本地`sts2/`，逐源文件提取直接遗物类、各池声明顺序、稀有度、覆写方法／属性的行号、SavedProperty及命令引用导航。`IsAllowed*`覆写单列，单人可达性仍标待源码与路径审查；不由类名或池归属推断资格。不属于RelicModel的目录辅助类单列；任何池或base定义找不到对应遗物类、池声明长度不符或已知语法无法读取时非零退出。

在仓库根目录使用 Python 与匹配版本的本地原版源码执行：

```powershell
python -X utf8 tools/source-manifest/extract_relic_manifest.py --out .artifacts/source-inputs/relics.json --markdown .artifacts/source-inputs/relics.md
```

`--source-root`选择本地源码，默认 `sts2`；`--out`与`--markdown`必填。退出码 0 仅表示静态提取成功，不表示玩法或独立比较通过。生成数据供本次开发查询，任务结束后删除；正式规则和范围在领域规格中维护。

`--base-catalog`仅兼容旧 formal-relic-declarations 输出，不接受当前 source-catalog；当前内容行为使用 `tools/base-spire-dev` 和 CI 检查。
