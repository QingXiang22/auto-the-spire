# base DSL迁移：设计前改动与纠偏记录

日期：2026-10-18，Asia/Shanghai。关联任务：**CORE-AUTHOR-05**及BASE-LAYOUT-01。基线为本地`codex/relic-content-20261017`的`0e333be`，同时已有根AGENTS本地修改与未提交CardDrawProcedures修改。当前任务状态见[主清单](../../tofix/README.md#当前交付)；当前设计从[规格](../../../design/mod-the-spire/engine-dsl-migration/spec.md)进入。

本记录区分用户决定、实际操作与尚未成立的结论，不将先行操作倒填为规格驱动交付。尚未创建本轮提交或PR。

## 用户决定与实施顺序纠正

1. 用户先要求按游戏领域整理base，方便查找power、relic并降低维护成本，随后授权主agent接管当前未提交改动。
2. 用户指出base过多C#偏离目标，确认Engine为主体、几乎全部逻辑由完整DSL实现、Spire为其子集。
3. 用户进一步要求设计长期DSL目录并迁移base，严格只用DSL及必要JSON。
4. 对“保留旧运行路径还是直接切换”的问题，用户明确选择：**直接切换纯DSL，未迁移玩法明确报未支持，接受暂时功能回退**。该决定授权功能回退，不授权掩盖回退或删除原完整目标。
5. agent在设计记录尚不完整时继续移动代码和修改加载器；用户随后要求spec driven design，禁止盲目开工且不留信息。本次因此先补规格、设计、任务和本记录，不继续扩大生产实现。

## 已发生的改动

| 改动 | 实际位置／证据 | 当前结论 |
| --- | --- | --- |
| 审阅未提交重洗回调 | 原CardDrawProcedures增加afterShuffle但无完整消费者，且独立Reshuffle调用可能漏回调 | 已补共享调用、C# TheAbacus消费者和定向检查；这只是旧规则的行为基线，不是DSL迁移 |
| 首轮领域归类 | 274份实现、内容和检查被归入Cards、Powers、Relics等；本地映射`.artifacts/base-layout/moves.json` | 后续已被严格纯包目标替代，不能沿该C#目录继续扩张 |
| 原包整体移出 | 原`mod-the-spire/base-spire/`内容移到`tools/base-migration-baseline/`；新base目录已创建但为空 | 物理移动已发生，功能切换未完成；旧内容没有删除，但不能作为生产依赖 |
| 对拍与net旧耦合文件移动 | 旧tools/differential在对照目录的differential；7份base耦合net检查在net-tests，3份旧Host适配在net-host | 未完成测试去向和生产接线；不能声称原检查已通过或已全部退役 |
| solution变更 | 从AutoTheSpire.sln移除了旧BaseSpire与Slice.Tests两项目 | 仅移除登记，不表示生产依赖已断开 |
| 同包源片段实验 | EngineDslCompiler.Libraries、EngineDslCompiler及SourceFragmentChecks | 已有实验实现与检查，须经T02按正式契约复核；不是用户逐条指定的API |
| 源与新目录可见性 | 迁移对照曾受tools忽略规则影响；本轮已加白名单并登记tools入口 | git check-ignore不再命中；385份可见源／资料按来源保存，不把产物和凭据纳入 |

根AGENTS及本地凭据不随本任务提交，原用户工作不得以reset／清理覆盖。

## 实际验证及限制

| 命令／检查 | 实际结果 | 证据和限制 |
| --- | --- | --- |
| 旧C#路径TheAbacus定向检查 | 通过1项，包含普通抽牌与自动出牌重洗、非重洗和移除负例 | `.artifacts/base-layout/`相关执行输出；不能据此认定新DSL支持自动出牌 |
| 首轮目录移动后的solution构建 | 成功，0错误、7警告 | `.artifacts/base-layout/build.log`；发生在后续整包移出之前，不证明当前中间工作区能构建 |
| 29份旧DSL迁移前后核对 | DSL正文一致 | 原移动映射及Git HEAD对照；不证明这些旧作者文本均已符合完整DSL子集 |
| 旧base runtime＋report | 非零；药水检查预期固定容量3，实际动态容量999 | `.artifacts/base-layout/runtime.log`；真实冲突保留，未降低断言或伪装全通过 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 104项通过，包含5项新增源片段检查 | `.artifacts/base-layout/engine-dsl.log`；只证明该Engine工作区实验范围，不证明base迁移、Host切换或原版行为 |
| `python tools/doc-check/check_docs.py` | 本次修复指向移动资料的Markdown链接后，扫描703份文档，0问题 | `.artifacts/base-layout/doc-check.log`；只证明文档结构／链接，不证明生产实现 |
| `git diff --check`与`git diff --cached --check` | 均返回0；换行规范提示不计行为证据 | 当前工作区／索引检查；提交前须对最终差异再查 |

运行环境为仓库global.json指定的.NET SDK 9.0.306，Windows／PowerShell，Python 3.12。执行证据尚未绑定新的提交；本地日志为辅助原始证据，关键结论已在本记录保留。

## 当前不能声称的事情

- base纯DSL产物尚不存在，新的根入口、manifest和首链没有交付。
- 生产Host和工具仍有旧BaseSpire引用；不能说已禁止回退或已完成纯DSL切换。
- 对拍、公开完整路径与旧测试分组没有完成迁移，当前构建也未获得新的集成通过证据。
- 旧91件C#可执行遗物不能计入新DSL已支持数量；完整297件源范围及原ENV合同继续开放。

## 本轮规格走查

已建立spec／plan／tasks三份互链产物，并把唯一当前状态留在主清单。13项要求分别有实施归属，12个验收场景分别有负责任务；目录、JSON边界、同包组合、旧范围拒绝、生产依赖和原完整分母均有明确去向。

走查发现并保留了生产阻断项：新包为空、旧引用未断开、原检查迁移未完成，以及旧药水容量断言冲突。没有将这些问题改写成设计已完成的证明，也没有在本轮补规格期间继续增加玩法实现。

## 接续方式

先完成[任务T00](../../../design/mod-the-spire/engine-dsl-migration/tasks.md#首批任务)的变更保存和设计准备度检查，再按T01～T06落实。每个任务开始前对应spec要求和plan契约，执行后登记实际命令、结果与未验证项。新发现改变需求或设计时先改规格，不继续凭局部实现决定架构。

本记录是负责人自查与纠偏证据，不是独立评审，也不是完整迁移验收。
