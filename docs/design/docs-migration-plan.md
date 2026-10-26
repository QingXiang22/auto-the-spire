# 项目文档迁移方案（评审稿）

文档类型：解释（Explanation），兼含迁移步骤。状态：提案，未执行；第 4 节均为建议，不能视为已批准的决定。本 PR 只修订方案和已核实的过时说明；实际迁移另按执行工单实施，批次与验收见第 8、9 节。完成后将仍有效的架构决定并入现行页面，删除本页，历史通过 Git／PR 查询。

目标读者：评审执行者、迁移执行者。
适用范围：输入提交 `4c6514f` 中 `docs/` 下的 84 个 Markdown 文件，不含本提案。移动时同时处理随页资源、全仓链接及读取这些路径的程序；`docs/examples/` 保持原地。`CONTRIBUTING.md`、各层 `AGENTS.md`、`tools/` 文档及检查程序只同步路径，不改变其职责和规则。优先级、负责人及执行状态仍只在 GitHub Issues 维护。

---

## 0. 摘要

- **问题**：`design/` 同时包含多种读者任务，规范要求与当前行为不易区分；已有入口和局部索引需要更明确地衔接。
- **目标**：改成“贡献者优先”的信息架构；四象限（教程／操作／参考／解释）降为页级写法规则；规范单独成层。
- **做法**：先补入口页，再按目录分批移动；每批同步引用、检查程序和资源，最后清理。第 8 节列出 8 个必需批次及 1 个可选整理批次。
- **验收**：每批可独立阅读和验证；迁移完成后旧目录清空、内容按读者任务组织、`python tools/ci/check.py docs` 通过，新人任务走查通过。

---

## 1. 背景与目标

### 1.1 为什么现在改

1. 输入提交的 84 页里有 32 页声明文档类型；其余页面需按正文检查，未标类型本身不等于不可用。
2. `design/` 同时装着教程（`first-host`）、操作指南（`how-to-submit-parameters`）、参考（`session-api-reference`）、解释（`rules-and-host`）和规范（`environment-completion-and-differential-v1`）。
3. 要求与设计理由分布在 `design/` 和 `contracts/`；需逐页核对其职责，不能仅凭目录或“必须”一词分类。
4. `docs/README.md` 是全局入口，`development/base-spire/README.md` 与 `dsl/core/README.md` 是有用的局部索引；需保持分工和相互链接。
5. 已核实的漂移：输入提交的环境验收规格把 `test` 子命令和三个严格 suite 都写成待实现，但 `tools/differential/Program.cs` 已实现 `test --suite`，原 11 份 suite 定义也已存在，其中 `regression` 有 17 个必测 ID。完整 `regression` 仍严格失败；`test-env.ps1`、`inventory`／`compare`／`run`／`verify` 通用入口与 `ironclad-run`、`full-singleplayer` 定义尚缺。专项 `*-compare` 命令另行报告，不代替完整环境验收。本 PR 同步修正环境规格中的入口现状。

### 1.2 目标

- 新人从 `docs/README.md` 出发，三次点击内能：跑通检查、找到要改的文件、找到对应规范。
- 任一模块的“怎么做”“现在是什么”“必须怎样”在同一目录下可达。
- 内容页按教程、操作指南、参考或解释选择主要读者任务；规范表示权威职责，可采用参考写法，不新增第五种 Diataxis 类型。
- 保留一个全局入口和必要的模块索引，不复制全局任务表。

### 1.3 非目标

- 移动批次不重写行为要求；正文纠错与合并单独提交。同步链接、示例路径、资源引用和检查程序是每批移动的组成部分。
- 不改变任何游戏规则、接口或验收要求；只纠正已核实的入口现状说明。
- 不搬 `tools/` 文档（本方案建议 `areas` 只含 base／core／net／ai）。
- 不引入静态站点生成器、front matter 体系或第二套术语表。
- 不在本方案内执行迁移。

---

## 2. 调研依据

| 项目 | 顶层主线 | 借鉴点 | 来源 |
| --- | --- | --- | --- |
| Python 文档 | 产物／受众 | 参考区按对象拆（语言／库／内置／C API）；HOWTO 独立索引；规范外置（PEP） | [docs.python.org/3](https://docs.python.org/3/) |
| CPython 开发者指南 | 贡献者工作流 | 独立贡献者站；Getting started 含“修简单问题”“去哪求助”；internals 独立成区；Experts index | [devguide.python.org](https://devguide.python.org/) |
| Django | 四象限 | 首页明写四类文档用途；Internals 与 Release notes 在四象限之外 | [docs.djangoproject.com](https://docs.djangoproject.com/en/stable/) |
| Home Assistant 贡献者站 | 任务／领域 | 首页“你想做什么”；按领域分组；首次贡献有独立入口 | [contributors.home-assistant.io](https://contributors.home-assistant.io/) |
| Kubernetes 贡献者站 | 新人路径 | 十节新人课程；OWNERS 与贡献流程有独立入口 | [kubernetes.dev/docs/guide](https://www.kubernetes.dev/docs/guide/) |

这些首页展示了贡献者工作流、模块入口和按用途分组的组织方式。它们不证明所有项目都应采用同一目录，也不证明规范必须单独成层。本项目据此提出贡献者入口与领域索引；目录选择仍由本仓库的读者任务和维护成本决定。

---

## 3. 现状诊断

### 3.1 文件分布

| 目录 | 页数 | 实际内容 |
| --- | --- | --- |
| `docs/` 根 | 4 | README、roadmap、terminology、AGENTS |
| `docs/contracts/` | 2 | core 基线、net 0.2 不变量 |
| `docs/design/` 根 | 6 | 项目规格、ENV、实现约束、AI-net 运行／验证、远端委派 |
| `docs/design/core-the-spire/` | 8 | 四象限 + 规范全都有 |
| `docs/design/mod-the-spire/` | 25 | 架构／控制流（解释）、迁移约定与监听（规范）、20 个域文件 |
| `docs/design/net-the-spire/` | 7 | 规格、设计、协议、客户端参考、两篇操作指南 |
| `docs/design/ai-the-spire/` | 6 | 规格、架构、编码器、论文、策略、训练 |
| `docs/development/base-spire/` | 19 | 唯一已是四象限形状的子树 |
| `docs/dsl/core/` | 7 | 教程、操作、四篇参考 |
| **合计** | **84** | |

声明类型统计：操作指南 14、参考 9、解释 5、教程 4；**32/84 有声明，52 页没有**。

### 3.2 五处具体错位

| 页面 | 现目录 | 实际问题 |
| --- | --- | --- |
| `design/core-the-spire/first-host.md` | design | 声明“教程”，却和规范同目录 |
| `design/core-the-spire/how-to-submit-parameters.md` | design | 操作指南混在设计区 |
| `design/core-the-spire/session-api-reference.md` | design | 参考页混在设计区 |
| `design/net-the-spire/how-to-play-base-game.md` + `how-to-run-public-game.md` | design | 前者用于终端手动试玩，后者用于 Host／客户端调用；均属操作指南，不能因面向新人就改标教程或直接合并 |
| `design/environment-completion-and-differential-v1.md` | design | 同时包含验收要求、命令和裁决流程，需区分规则查询与实际操作 |

### 3.3 判定方法

分类依据是主要读者任务：通过受控练习学习为教程；已有基础后完成具体目标为操作指南；查询字段、接口或规则为参考；理解关系与原因为解释。命令、表格或“必须”一词不能单独决定类型。规范另标权威职责；README 等索引和协作规则不强制归入内容页类型。未逐页精读全部 84 页，映射是候选；各移动批次须先核对本批正文，并在 PR 中说明混合内容的处理。

---

## 4. 建议与取舍

| ID | 建议 | 理由 | 备选与取舍 | 状态 |
| --- | --- | --- | --- | --- |
| D1 | 顶层按贡献者任务组织：`start / understand / areas / reference / specs / meta` | 为入门、修改和查阅提供明确入口 | 只改现有首页成本更低；是否值得整体移动需结合新人走查判断 | 建议 |
| D2 | 四象限指导内容页写法，目录仅作默认提示 | 同一入口可服务不同读者任务，索引不必伪装教程 | 目录强制类型会把环境安装和试玩误标为教程 | 建议 |
| D3 | 模块规范进 `areas/<module>/spec/`，跨模块约定进 `specs/` | 在模块内找到要求，跨模块要求保留统一位置 | 全部集中会增加模块间跳转；权威位置不依赖正文类型 | 建议 |
| D4 | `areas` 只含 base／core／net／ai；tools 保持原地并从首页直达 | 复用工具已有说明 | 不复制 CI 和差异测试命令 | 建议 |
| D5 | 先补入口；每批移动同时修复全部消费者 | 每个中间提交均应能阅读和检查 | 把引用更新推迟到最终批次会造成断链及检查失败 | 建议 |
| D6 | 标注放在标题后，沿用 `文档类型：…`；先人工核对，不默认新增检查器 | 类型准确性需要审阅，自动检查只能核对格式 | 不要求索引、协作规则声明类型，不引入 front matter | 建议 |
| D7 | 全局入口保留常用直接链接；模块 README 维护局部索引 | 控制三次点击路径，保留有用的就地导航 | 禁止直达内容页会增加跳转 | 建议 |
| D8 | 搬空旧目录后删除；映射保留在迁移 PR 中，完成后删除本提案 | 遵循现有文档规则，历史通过 Git 查询 | 不留别名、指路页或长期旧路径映射副本 | 建议 |
| D9 | 规范合并与移动分开评审，合并前逐条迁移有效要求 | 能区分路径变动与行为要求变化 | 两篇公开运行指南服务不同任务，保留各自用途 | 建议 |

---

## 5. 目标结构

```text
docs/
  README.md                    # 全局入口，允许直达常用内容与 tools
  AGENTS.md                    # 位置不动，同步引用
  start/
    README.md                  # 新人路径索引，不标教程
    environment.md             # 环境、构建、测试、检查命令
    first-change.md            # 认领工单 → 改动 → 验证 → PR
    first-rule.md              # 以下为迁移进来的教程
    first-host.md
    first-relic.md
    shared-rule.md
  understand/
    README.md                  # 解释区索引（可选）
    architecture.md            # 五模块总览、数据流、边界
    decisions/
    base-architecture.md
    base-control-flow.md
    rules-and-host.md
    runtime-state-model.md
    net-design.md
    ai-architecture.md
    ai-literature.md
    development-model.md
    combat-engine-comparison.md
  areas/
    base/  { README.md, how-to/, reference/, spec/ }
    core/  { README.md, how-to/, reference/ }
    net/   { README.md, how-to/, reference/, spec/ }
    ai/    { README.md, spec/ }  # 有实际内容时再建操作与参考目录
  reference/
    README.md                  # 查表索引
    dsl/                       # core/0.3 语言、作者、包格式
  specs/
    README.md                  # 跨模块接口约定索引
    project.md
    environment.md
    environment-constraints.md
    core-engine-baseline.md
    core/game-model.md
    net/0.2/                   # invariants.md 与三份 JSON 资产一起迁移
    ai-net/operations.md
    ai-net/verification.md
  meta/
    glossary.md
    roadmap.md
    workflow/remote-delegation.md
```

`start/` 同时含入门操作指南和四份教程，按页判断类型。core 的跨模块模型要求放在 `specs/core/`，不创建空的模块规范目录。示例保持 `docs/examples/` 原地；交互 HTML 和六份 SVG 随战斗对照页迁入 `understand/`，保持相对布局。

---

## 6. 页面映射（84 页）

### 6.1 根与接口约定

| 现路径 | 目标 | 备注 |
| --- | --- | --- |
| `docs/README.md` | 原地重写 | 全局入口，不保存历史映射 |
| `docs/roadmap.md` | `meta/roadmap.md` | |
| `docs/terminology.md` | `meta/glossary.md` | |
| `docs/AGENTS.md` | 不动 | 协作约束 |
| `docs/specs/core-engine-baseline.md` | `specs/core-engine-baseline.md` | 跨模块接口约定 |
| `docs/specs/net/0.2/invariants.md` | `specs/net/0.2/invariants.md` | 同批移动三份 JSON 并更新所有读取者，见第 8 节 |

### 6.2 design/

| 现路径 | 目标 |
| --- | --- |
| `design/spec.md` | `specs/project.md` |
| `design/environment-completion-and-differential-v1.md` | `specs/environment.md` |
| `design/environment-implementation-constraints.md` | `specs/environment-constraints.md` |
| `design/ai-net-operations.md` | `specs/ai-net/operations.md` |
| `design/ai-net-verification.md` | `specs/ai-net/verification.md` |
| `design/deepseek-harness-operations.md` | `meta/workflow/remote-delegation.md` |
| `design/core-the-spire/spec.md` | `specs/core/game-model.md` |
| `design/core-the-spire/core-architecture-decisions.md` | `understand/decisions/core.md` |
| `design/core-the-spire/developer-guide.md` | `areas/core/reference/developer-guide.md`（候选） |
| `design/core-the-spire/first-host.md` | `start/first-host.md` |
| `design/core-the-spire/how-to-submit-parameters.md` | `areas/core/how-to/submit-parameters.md` |
| `design/core-the-spire/rules-and-host.md` | `understand/rules-and-host.md` |
| `design/core-the-spire/runtime-state-model.md` | `understand/runtime-state-model.md` |
| `design/core-the-spire/session-api-reference.md` | `areas/core/reference/session-api.md` |
| `design/mod-the-spire/spec.md` | `areas/base/spec/overview.md` |
| `design/mod-the-spire/base-architecture.md` | `understand/base-architecture.md` |
| `design/mod-the-spire/control-flow-modules.md` | `understand/base-control-flow.md` |
| `design/mod-the-spire/content-behavior-migration.md` | `areas/base/spec/behavior.md` |
| `design/mod-the-spire/effect-listeners.md` | `areas/base/spec/effect-listeners.md` |
| `design/mod-the-spire/<domain>/spec.md`（16 份） | `areas/base/spec/<domain>.md`，保留域名，避免同名 `spec.md` 覆盖 |
| `design/mod-the-spire/character-combat/ironclad-start.md` | `areas/base/spec/ironclad-start.md` |
| `design/mod-the-spire/ironclad-dsl/shared-operations.md` | `areas/base/reference/ironclad-shared-operations.md` |
| `design/mod-the-spire/relic-combat-start/content-lifecycle.md` | `areas/base/spec/relic-lifecycle.md` |
| `design/mod-the-spire/run-domain/randomness.md` | `areas/base/spec/randomness.md` |
| `design/net-the-spire/spec.md` | `areas/net/spec/overview.md` |
| `design/net-the-spire/design.md` | `understand/net-design.md` |
| `design/net-the-spire/protocol.md` | `areas/net/reference/protocol.md` |
| `design/net-the-spire/contract-artifacts.md` | `areas/net/spec/contract-artifacts.md` |
| `design/net-the-spire/how-to-play-base-game.md` | `areas/net/how-to/play-base-game.md` |
| `design/net-the-spire/how-to-run-public-game.md` | `areas/net/how-to/run-public-game.md` |
| `design/net-the-spire/public-client-reference.md` | `areas/net/reference/public-client.md` |
| `design/ai-the-spire/spec.md` | `areas/ai/spec/overview.md` |
| `design/ai-the-spire/architecture.md` | `understand/ai-architecture.md` |
| `design/ai-the-spire/encoder-design.md` | `areas/ai/spec/encoder.md` |
| `design/ai-the-spire/literature.md` | `understand/ai-literature.md` |
| `design/ai-the-spire/policy-contract.md` | `areas/ai/spec/policy-contract.md` |
| `design/ai-the-spire/training-design.md` | `areas/ai/spec/training.md` |

> 16 个域目录：`card-effects`、`card-exhaust`、`card-play`、`character-combat`、`combat-damage`、`combat-end`、`combat-rewards`、`combat-turn-resources`、`enemy-turn`、`event-domain`、`ironclad-dsl`、`permanent-deck-smith`、`potion-inventory`、`power-modifiers`、`relic-combat-start`、`run-domain`。其中四个目录的附加文件已单列，域文件合计 20 份。开发者指南含解释、操作和 API 查询，需在 P4 中核对主要读者任务，不能只根据文件名归类。

### 6.3 development/ 与 dsl/

| 现路径 | 目标 |
| --- | --- |
| `development/base-spire/README.md` | `areas/base/README.md` |
| `development/base-spire/tutorials/first-relic.md` | `start/first-relic.md` |
| `development/base-spire/tutorials/shared-rule.md` | `start/shared-rule.md` |
| `development/base-spire/how-to/*.md`（10） | `areas/base/how-to/*.md` |
| `development/base-spire/reference/*.md`（4） | `areas/base/reference/*.md` |
| `development/base-spire/explanation/development-model.md` | `understand/development-model.md` |
| `development/base-spire/explanation/combat-engine-comparison.md` | `understand/combat-engine-comparison.md` |
| `dsl/core/README.md` | `reference/dsl/README.md` |
| `dsl/core/first-rule.md` | `start/first-rule.md` |
| `dsl/core/how-to-configure-game.md` | `areas/core/how-to/configure-game.md` |
| `dsl/core/0.3/*.md`（4） | `reference/dsl/0.3/*.md` |

---

## 7. 入口页面

其中 base README 从现有开发手册迁入，其余 9 页为候选新页；以独立读者任务决定是否新建，不为填满目录而建空页。

| 页面 | 内容要点 | 素材来源 | 验收 |
| --- | --- | --- | --- |
| `start/README.md` | 装环境 → 跑检查 → 改一处 → 提 PR；链接适用指南 | 根 README、`verify-and-debug`、`CONTRIBUTING` | 从零照做能跑通 |
| `start/environment.md` | 操作指南：环境要求和首次检查，工具命令链接权威说明 | `global.json`、根 README、`tools/ci/README.md`、`tools/doc-check/README.md` | 按任务安装依赖；文档检查无需 .NET，命令可执行 |
| `start/first-change.md` | 操作指南：认领工单 → 定位文件 → 改 → 验证 → PR | `CONTRIBUTING.md`、base 开发手册 | 只链接规则，不依赖本机 skill |
| `understand/architecture.md` | 五模块职责、数据流和既有边界的摘要；细节链接权威规格 | `design/spec.md`、`base-architecture.md`、`control-flow-modules.md` | 新人能判断修改规则、引擎或接口应去哪里；不另立规则 |
| `areas/base/README.md` | 来自现有开发手册，改为“改什么 → 看哪页”任务表 | 现有 README | 10 个 how-to 全部可达 |
| `areas/core/README.md` | core 任务表：DSL 语言、编译、Session API、决定 | 现有 core 文档 | 规则作者与 Host 作者均可定位入口 |
| `areas/net/README.md` | 协议、客户端、公开运行 | 现有 net 文档 | 手动试玩与接口调用路径分别可达 |
| `areas/ai/README.md` | 规格、编码器、策略、训练 | 现有 ai 文档 | 明示现状与目标，未实现功能不写成可启动 |
| `reference/README.md` | 查表索引：DSL、Session API、操作、Power、协议 | 现有 reference 页 | 常用字段和接口查询在三次点击内 |
| `specs/README.md` | 接口约定索引：编号 + 一句话 + 验收位置 | 现有 specs | 每份接口约定唯一 |

---

## 8. 批次计划

| PR | 范围 | 文件量 | 类型 | 验收 |
| --- | --- | --- | --- | --- |
| P1 | 入口页：重写 `docs/README.md`；新建 `start/{README,environment,first-change}`、`understand/architecture.md`、`areas/{core,net,ai}/README.md`、`reference/README.md`、`specs/README.md` | +9 新，1 改 | 内容新增 | 全部链接指向当时已有路径；未移动的 base 和 DSL 使用原入口；首页保留 tools 直达链接 |
| P2 | 四份教程（`first-rule`、`first-host`、`first-relic`、`shared-rule`）→ `start/` | 4 移 | 移动及引用同步 | 示例相对路径与全仓引用有效 |
| P3 | base how-to 10 + base reference 5（含 `ironclad-shared-operations`）+ 开发手册 README → `areas/base/` | 16 移 | 移动及引用同步 | 全仓引用有效 |
| P4 | core 4 页（含开发者指南）、net 4 页（含手动试玩）、`reference/dsl/` 5 页 → 对应目录 | 13 移 | 移动及引用同步 | 各页任务与主要类型核对；保留两种公开运行用途 |
| P5a | `areas/base/spec/` 22 页（16 份域规格 + 铁甲初态 + 遗物生命周期 + 随机 + base 规格 + 行为约定 + 监听约定） | 22 移 | 移动及引用同步 | 域名保留，要求与 ID 不变，每份规范唯一 |
| P5b | `areas/net/spec/` 2 + `areas/ai/spec/` 4 + `specs/` 8；协议 JSON 一并移动 | 14 页、3 JSON 移 | 移动及消费者同步 | 文档需求追踪与 net 协议资产加载检查通过 |
| P6 | 解释 10 → `understand/`；meta 3 → `meta/`；HTML 与 SVG 随页移动 | 13 页、7 资源移 | 移动及引用同步 | HTML／SVG 可打开；术语页例外随路径更新 |
| P7 | 删除本提案；将有效决定并入现行架构说明；核对旧目录与消费者 | — | 清理 | 无有效旧路径引用，资源、示例及既有检查均保留 |
| P8（可选） | 核对 `behavior.md` 与域规范、ENV 与实现约束的重复要求，再决定合并 | — | 内容改动 | 逐条迁移有效要求；保留不同职责页面，修复被删页引用 |

必需批次为 P1、P2、P3、P4、P5a、P5b、P6、P7；P8 为可选批次。上表文件量只统计正文和随页资源，另需同步全仓消费者，不能按移动文件量估算完整差异。

每批移动必须在同一提交同步以下内容，不能等到 P7：

- 所有 Markdown、HTML 与 SVG 的相对链接、图片、锚点，以及示例命令中的路径；包含根和模块 README、`AGENTS.md`、`CONTRIBUTING.md`、仓库内 skill 和 tools 文档。
- P5b 更新 `tools/doc-check/check_docs.py` 与 `check_trace.py` 的规格／矩阵路径，保持原需求 ID 和追踪检查；同步 `net-the-spire/Net.TheSpire.Tests/Fixtures.cs` 及其他 JSON 读取者，不能只搬 `invariants.md`。
- P6 同步 `.vale.ini` 的术语页精确路径，使术语定义页的既有例外仍只适用于该页；随页保留 `combat-engine-comparison.html` 及 `assets/combat-engine/*.svg`。
- 示例留在 `docs/examples/`，核对移动后教程指向的工程、`.spire` 和 Python 脚本仍可执行。用户全局 skill 的机器配置不提交；仓库能检查的引用在同批修复，无法访问的外部消费者在 PR 说明。

每批基本命令（占位路径需换成本批实际值）：

```powershell
git mv <old> <new>
python tools/ci/check.py docs
git grep -n --fixed-strings -- '<old-path>'
```

检查旧路径时同时搜索完整路径、相对路径与代码拼接形式；`git grep` 无匹配返回 1，搜索报错须调查。版本锁定源码链接保留原提交和历史路径，不能改成该提交不存在的新路径；迁移 PR 的映射也不算有效残留。每批在干净检出中运行文档检查，修改程序读取路径时再运行对应检查，不删除用例或降低覆盖范围。回退某批时同时回退其消费者，依赖批次先逆序撤销；P8 不与移动同批。

---

## 9. 检查与通过条件

1. **链接、格式与需求追踪**：每批运行 `python tools/ci/check.py docs`；P5b 另运行 `python tools/doc-check/check_trace.py`，确认移动前后的需求与验证关联保持一致。按 `tools/doc-check/README.md` 准备 Vale。
2. **类型与用途**：人工核对内容页主要任务及标题后的类型声明；`how-to/` 默认操作指南，`reference/` 默认参考，`understand/` 默认解释；`start/` 按页判断。规范页核对要求、ID 和唯一权威位置，不标为第五种类型。索引与协作规则不强制声明。
3. **路径与消费者**：逐批查本批原路径；P7 全仓查四个旧目录名及代码中的路径片段，逐项区分有效引用与锁定历史链接。任何仍被使用的旧路径都须修复，不能用绝对“零匹配”要求迫使修改历史源码链接。旧目录内不得遗留 JSON、HTML 或 SVG。
4. **既有检查**：P2 核对四个教程的示例；P4 核对公开运行步骤。P5b 修改协议资产读取路径时运行 `python tools/ci/check.py net`；如其他程序也读这些路径，补跑受影响的检查。P6 打开交互 HTML 并核对六份静态图，验证术语页例外和其他文档的措辞检查仍生效。不因移动而删减必测 ID、字段或来源范围。
5. **导航走查**：全局任务入口指向现有页面；base、core、net、ai 和 tools 的常用任务可达。模块索引保留局部任务表，避免复制全局表。确认入门、检查、定位文件与规格均在首页三次点击内。
6. **新人操作**：按 `start/README.md` 在隔离副本完成“改一张卡数值并运行对应检查”，在 PR 中记录命令、提交、预期与实际结果；无法实际执行时明确未验证，不以链接检查代替操作结果。

---

## 10. 风险与缓解

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 84 页及资源的相对链接重写 | 高，容易漏 | 每批同步全部消费者并检查；P7 最后核对，不能延迟修复 |
| 程序和规则配置写死文档路径 | 高，会导致检查崩溃、追踪遗漏或术语例外失效 | P5b 同步规格追踪和协议资产读取；P6 同步 `.vale.ini`；保留原检查范围 |
| 仓库引用与全局 skill | 中 | 仓库内引用同批更新；本机全局 skill 不提交，无法访问的消费者明确说明；旧 Issue／PR 链接通过 Git 历史查询 |
| “负责人”页与 AGENTS 冲突 | 中 | 不建静态名单页；“找谁”指向 GitHub #28 与 Issue 模板 |
| 规范合并删页 | 高 | D9：先迁移要求再删；合并单独 PR；逐条核对 |
| 评审后结构再变 | 中 | 映射表按目录组织，改一个目录不影响其他批 |
| 新入口重复既有说明 | 中 | 环境页链接工具命令，贡献流程链接 CONTRIBUTING；局部索引保留，不维护第二套规则 |
| 迁移期间文档不可读 | 中 | P1 链接当前路径；每批移动、消费者与资源同时提交，主入口始终可用 |

---

## 11. 评审问题清单

实际迁移工单确定前需解决：

1. 六个顶层区域相对“只改善现有入口”的收益是否足以承担全仓迁移成本？用第 9 节的读者任务比较；本 PR 的修订不等于批准迁移。
2. P1 的 9 个新页是否均有独立任务？能由现有页面提供的内容应链接，不复制环境、流程或架构要求。
3. 各批主要类型能否按正文确认？优先审阅混合的 core 开发者指南及 ENV 页，不能先标类型再按标签修改行为要求。

既有规则已经明确的事项不重新作为开放选项：不保留旧版别名／archive；协作规则继续由 CONTRIBUTING 和 AGENTS 维护；任务状态只进 Issue；规范合并前必须保留有效要求。

---

## 12. 完成定义（DoD）

- [ ] `docs/design/`、`docs/development/`、`docs/dsl/`、`docs/contracts/` 不再存在。
- [ ] `docs/README.md` 的任务表覆盖 `start`、`understand`、`areas`、`reference`、`specs`、`meta`。
- [ ] 内容页主要任务及类型已人工核对；索引、规范职责与 Diataxis 类型区分清楚。
- [ ] `python tools/ci/check.py docs` 通过。
- [ ] 全仓有效旧路径引用已修复；历史源码链接保留锁定提交，JSON、HTML、SVG、示例和程序读取者完整可用。
- [ ] 原需求追踪、协议检查和 Vale 规则继续执行，所需检查结果在各批 PR 中说明。
- [ ] 至少一次“新人按 `start/README.md` 走通”的记录（PR 描述即可）。
- [ ] 规范页与现状页无相互复制；每份规范唯一权威。
- [ ] 有效架构决定已并入现行页面，本提案已删除，未另建历史映射或任务状态副本。

---

## 13. 附：核对依据

- 文件与类型统计基线：`4c6514f` 的 `docs/` 中 84 个纳管 Markdown 文件，32 页有类型声明（操作 14／参考 9／解释 5／教程 4）。本提案加入后为 85 页；不把该数字作为后续迁移的固定通过条件。
- 差异测试入口现状以 `tools/differential/Program.cs` 和工具 README 为准；除 `test --suite` 外已有专项比较和自检。`suites/` 含 `regression.json`、`ironclad-act1.json`、9 个 `normal-*.json`；`test-env.ps1` 尚不存在。定义存在不表示完整测试集通过。
- 迁移消费者示例：`tools/doc-check/check_docs.py`、`check_trace.py`、`.vale.ini`、`net-the-spire/Net.TheSpire.Tests/Fixtures.cs`。这些是核对起点，不是免于全仓检查的封闭列表。
- 外部依据：见第 2 节 URL。
