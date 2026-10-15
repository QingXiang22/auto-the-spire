# BASE-01 负责人复核与接续

日期：2026-10-05。复核对象：[A 的合并审计报告](a-base-line-audit-2026-10-05.md)、SinglePlayer 工厂及调用的 Slice 规则构建路径。本文是独立复核记录，不另建待办；进度和问题只更新 [主清单](../../tofix/README.md#01-core冻结后的分派与下一步)。本次不修改生产代码，不关闭 BASE-01/ENV，也不归档未解决报告。

## 1. 总体判断

有实际工程推进：唯一工厂、不可变配置、独立 Session、启动前种流、正式卡/Power/敌人来源、真实角色牌组数量和起始资源已接上；同 seed 复现与不支持角色/进阶拒绝有检查。工厂接口可成为 B/C 的共同接线点，避免重新开发。

但本批仍是**正式内容来源的 dev 子集**，不是已经保真的原版开局或正常 Run。5 个遭遇来自筛选后的池，缺真实地图、奖励、遗物效果、完整 RNG 调用和四幕终局；“房间 1→5 真实死亡”应理解为该开发规则下未经人工清场的自然 Defeat，不是参考/真实游戏同 seed 的终局证据。11 项新增检查说明本线可运行，不能换算原版覆盖率。

报告诚实保留了 B/C 未接、无双侧比较、奖励/地图/遗物等缺口，这些判断可取。但“BASE-01 只剩 B/C 接入”的解释不完整：本次发现 A 自有的范围校验和牌序缺陷，必须先处理。**BASE-01 未验收，BASE-02/03 和完整 ENV 均开放。**

## 2. 已核实的开放问题

### BASE-01-R1 / P1：开发包可以冒用完整范围身份

位置：`BaseSpire/SinglePlayer/RunConfiguration.cs` 的 Resolve 只检查 ScopeId 属于三个已知字符串，没有检查当前实现能否满足该范围；`BaseSpireRuntimeFactory.cs` 又将配置 ScopeId 直接作为包版本和描述范围。

独立探针分别传 Dev、Ironclad、Full，三者全部接受且均返回 rooms=5、limitations=9、unsupported=157。因此不是三个不同完成度的构建，而是同一个未完成子集改了身份。B/C 若按配置转发会把不完整环境发布成完整环境，违反既定范围契约。

**A 修复要求：** 当前构建只允许 dev。Ironclad/Full 在构建任何 Session 前显式拒绝；未来由实际能力/内容闭合开启，而非仅换标签。增加一组针对三个范围的判别检查，原型和 dev 身份保持明确。B/C 在修复前只能使用 dev，不能发布完整范围。

### BASE-01-R2 / P1：起始牌序在洗牌前被改变

位置：`BaseSpire/SinglePlayer/SinglePlayerContent.cs` 构造 Deck 时 GroupBy 后 OrderBy(CardId)；`SliceAuthorFrontend.ToStarterDeck` 按此列表展开；`CombatSlicePackage` 按该顺序创建长期牌，再复制进战斗牌堆并洗牌。

探针从启动后的 LongTermDeck 读到 `Bash×1 → Defend×4 → Strike×5`。冻结来源 `sts2/src/Core/Models/Characters/Ironclad.cs:43-55` 为 `Strike×5 → Defend×4 → Bash×1`；Player.PopulateStartingDeck 按声明顺序加牌，PopulateCombatState 按 Deck.Cards 顺序复制，CardPile.RandomizeOrderInternal 再洗牌，没有在此之前按内容 ID 排序。

两者牌组多重集相同，现有检查将双方 Order 后比较，所以遗漏顺序。即使 PRNG/流完全一致，输入顺序变化也会改变同 seed 洗牌、手牌和实例来源对应。

**A 修复要求：** 保留来源牌序及每张重复牌的实例对应；若使用分组计数，要证明展开不改变实际顺序，不能全局按 ID 排序。检查实际长期牌组顺序，再由 B 在 `rng-initial-shuffle` 比较洗牌前输入、调用和值/顺序；不同 seed 得到不同顺序只证明用了随机，不证明原版等价。

### BASE-01-R3 / P2：源基线身份目前只是可任填的标签

位置：`RunConfiguration.SourceBaselineId` 未校验，工厂直接写入描述；RulesetId 仅摘要当前 RuleSet.Identity。探针将 SourceBaselineId 改成 `unverified-other-source`，仍接受，同 RulesetId 对外描述了不同来源标签；没有加载另一份源内容。

**A/B 接续要求：** 当前只接受实际支持的源基线，未知基线拒绝；由 B 冻结实际源/参考摘要，A 的构建描述绑定这一份可验证的配置与算法/布局版本。不要把“有一个名字”写成来源已经冻结。静态规则身份、运行随机/布局身份和来源证据的职责应明确；不要求每个纯证据标签变化都改变 IR 语义，但必须保证同公开构建身份不能对应行为不同的运行配置，也不能声称加载了实际上没加载的来源。C 使用实际描述，不自行猜 Profile。

## 3. 参考随机分歧的判断

已核对：冻结 `sts2/src/Core/Runs/RunRngSet.cs` 使用 uint/hash32，当前参考 RunRngSet 的普通路径使用另一 hash。不能凭这点就宣称参考已经升级到另一游戏版本，也不能无证据确定哪一份就是目标客户端行为。

报告第7节建议的参考 `old` 路径**不能直接认定修复**：它只改变 run seed 的旧哈希入口，CreateRng 仍进入 `new Rng(Seed, name)`，具名流派生继续用当前参考哈希；给 seed 加前缀还涉及输入语义变化。B 应按既有约束先核对实际版本/摘要；需要兼容时，仅针对有独立源向量证据的派生差异提供显式版本化接线，保持参考默认行为，不把效果修补、换 seed 或忽略 RNG 藏进兼容 Profile。未完成互证的随机 case 保持 blocked/未通过。

## 4. 架构与下一批重点

工厂是正确的聚合入口；继续复用现有 lowering 也合适。但生产行为仍委托很大的 CombatSlicePackage，并增加正式开关；尚未交付 BASE-01 要求的可复用 DSL 共享机制验证。不能将“有 SinglePlayer 目录”当作规则库架构已完成。

下一批先修 R1/R2/R3，固定 dev 工厂的消费契约；然后 A 沿实际 Run 缺口交付原版遭遇抽取/出生与行动随机、起始遗物有效行为、真实奖励和地图房间流程。按当前机制需要抽出可复用规则/程序，不先全面重写解析器或大规模拆目录。每个机制在正式入口、B 的两侧 case、C 的公开流程同批成立。

B 可先交严格测试执行/失败契约并接 dev 工厂的诊断，不需要等整个 BASE-01 父项关闭；C 同时接已配置的 Session 与 0.2。原有受控夹具允许保留，但新增正式机制的受控初始化必须消费同一正式规则，不继续用旧切片替代正式规则获得绿色。

## 5. 本次实际验证与边界

检查了 core/base/检查项目的源码与资源时间，现有 Debug DLL 不早于各自输入；使用现有产物串行执行，未构建共享生产工程：

| 验证 | 结果 |
| --- | --- |
| Core.TheSpire.Engine.Tests.dll | rc=0，95/95 |
| BaseSpire.Slice.Tests.dll | rc=0，275/275；重现 dev 房间1→5自然Defeat |
| Net.TheSpire.Tests.dll | rc=0，393/393 |
| 隔离调用探针 | rc=0；复现 R1三个范围均接受、R2牌序不同、R3未知来源标签接受 |

探针位于 `.audit/base-01-owner-review-2026-10-05/probe`，只引用既有 BaseSpire/Core/ANTLR DLL，编译写入自己的 obj/bin；最终命令为 `dotnet run --project .audit/base-01-owner-review-2026-10-05/probe/Probe.csproj`。最终探针及检查输出同目录保存。首次探针引用了不存在的 CollectionByQualifiedId，修正为现有 Collections 查询后完成复现；该临时探针编译错误不属于产品缺陷。

本次未重跑根 solution 构建、未执行严格双侧 suite、正常四幕胜利、真实客户端或 Release 性能。A 报告的构建绿色保留为其原时点证据；本次独立检查通过不关闭上述缺陷或完整环境。

新对话先读根 AGENTS、主清单0.1、本文和 A 原报告，再按本线任务接续；不用重新恢复核心、重复认领工厂，或从旧 RESUME HERE 忽略负责人新结论。
