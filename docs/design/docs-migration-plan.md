# base-spire 文档迁移方案（评审稿）

文档类型：解释（Explanation）。状态：提案（待评审），未执行；评审通过后按第 8 节批次执行，执行完成后本页删除或并入决策记录。

目标读者：评审执行者、迁移执行者。
适用范围：仓库 `docs/` 下全部 84 个 Markdown 文件；不涉及 `CONTRIBUTING.md`、`AGENTS.md` 规则本体与 `tools/` 文档。

---

## 0. 摘要

- **问题**：文档按模块目录组织，四类内容和规范混放；新人不知道从哪开始，规范与现状分不清，导航有三处。
- **目标**：改成“贡献者优先”的信息架构；四象限（教程／操作／参考／解释）降为页级写法规则；规范单独成层。
- **做法**：先补入口页，再按目录分批纯移动，最后清理；共 8 个 PR，每个都能单独回滚。
- **验收**：旧目录消失、每页类型可校验、全仓只剩一处导航、`docs check` 绿、新人任务走查通过。

---

## 1. 背景与目标

### 1.1 为什么现在改

1. 84 页里 52 页没有声明文档类型，读者只能靠猜。
2. `design/` 同时装着教程（`first-host`）、操作指南（`how-to-submit-parameters`）、参考（`session-api-reference`）、解释（`rules-and-host`）和规范（`environment-completion-and-differential-v1`）。
3. 规范没有家：约 36 页“必须满足什么”散在 `design/` 各层，和“为什么这样设计”抢位置。
4. 导航有三处（`docs/README.md`、`development/base-spire/README.md`、`dsl/core/README.md`），各自维护一套任务表。
5. 已有文档漂移：`environment-completion-and-differential-v1.md` 写“`test` 子命令、`test-env.ps1` 和三个严格 suite 均待实现”，但 `tools/differential/Program.cs` 已实现 `test --suite`，`suites/` 已有 `regression`（17 个必测 ID）与 `ironclad-act1`。缺的是 `test-env.ps1`、`inventory`／`compare`／`run`／`verify` 与 `ironclad-run`、`full-singleplayer` 两个 suite。

### 1.2 目标

- 新人从 `docs/README.md` 出发，三次点击内能：跑通检查、找到要改的文件、找到对应规范。
- 任一模块的“怎么做”“现在是什么”“必须怎样”在同一目录下可达。
- 每页的类型与所在目录一致，可自动检查。
- 全仓只有一处导航表，其他页面只做局部索引。

### 1.3 非目标

- 不重写正文内容；搬家和改内容分开。
- 不改变任何游戏规则、接口或规范条文。
- 不搬 `tools/` 文档（已决定 `areas` 只含 base／core／net／ai）。
- 不引入静态站点生成器、front matter 体系或第二套术语表。
- 不在本方案内执行迁移。

---

## 2. 调研依据

| 项目 | 顶层主线 | 借鉴点 | 来源 |
| --- | --- | --- | --- |
| Python 文档 | 产物／受众 | 参考区按对象拆（语言／库／内置／C API）；HOWTO 独立索引；规范外置（PEP） | [docs.python.org/3](https://docs.python.org/3/) |
| CPython 开发者指南 | 贡献者工作流 | 独立贡献者站；Getting started 含“修简单问题”“去哪求助”；internals 独立成区；Experts index | [devguide.python.org](https://devguide.python.org/) |
| Django | 四象限 | 首页明写四类文档用途；Internals 与 Release notes 在四象限之外 | [docs.djangoproject.com](https://docs.djangoproject.com/en/stable/) |
| Home Assistant 贡献者站 | 任务／领域 | 首页“你想做什么”；按领域分组；“新人四步” | [contributors.home-assistant.io](https://contributors.home-assistant.io/) |
| Kubernetes 贡献者站 | 新人路径 | 十节新人课程；OWNERS 说明归属；代码组织独立成页 | [kubernetes.dev/docs/guide](https://www.kubernetes.dev/docs/guide/) |

**结论**：没有一个样本是纯四象限。主流做法是“贡献者路径／领域入口”作主线，四象限作为每页的写法约束，规范单独一层。本方案采用这一结构。

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
| `design/net-the-spire/how-to-play-base-game.md` + `how-to-run-public-game.md` | design | 两篇操作指南内容重叠，且不在开发手册里 |
| `design/environment-completion-and-differential-v1.md` | design | 同时包含验收规范、命令、裁决流程；373 行，最难分类 |

### 3.3 判定方法

分类依据：目录声明、首行“文档类型”、正文语气与结构（命令／步骤 → 操作；表格／签名 → 参考；why／对比 → 解释；线性练习 → 教程；must／验收条件 → 规范）。未逐页精读全部 84 页，P1 阶段需补全分类表并逐页确认。

---

## 4. 决策记录

| ID | 决定 | 理由 | 备选与否决原因 | 状态 |
| --- | --- | --- | --- | --- |
| D1 | 顶层按贡献者任务组织：`start / understand / areas / reference / specs / meta` | 接手难度来自“不知道从哪开始”，不是“类型不纯” | 四象限优先：新人要先懂四种类型；产物优先：仍需在模块内猜类型 | 定 |
| D2 | 四象限作页级规则，目录与声明类型一一对应 | 保持每页写法一致，可自动检查 | 无 | 定 |
| D3 | 规范分层：模块域规范进 `areas/<module>/spec/`，跨模块接口约定进顶层 `specs/` | 改某模块时一处找齐；跨模块的 ENV／core 基线不归属单模块 | 全部集中 specs：跳转多一层；全部模块内：跨模块规范无处安放 | 定 |
| D4 | `areas` 只含 base／core／net／ai，`tools` 文档留在原地 | 避免同一份 CI／差异测试说明维护两遍 | 五模块：产生第二入口 | 定 |
| D5 | 先补入口页，再搬页面 | 搬完再补入口，等于把“不知道从哪开始”一起搬过去 | 一次性大搬：diff 无法审 | 定 |
| D6 | 每页首行保留 `文档类型：…`，新增目录↔类型一致性检查 | 现有 32 页已用该约定，成本最低 | front matter：全仓改造量大，无额外收益 | 定 |
| D7 | 入口页只链到 area 的 README，不链叶子页 | 叶子页移动时只改一处，降低链接 churn | 直接链叶子：每次移动都要改入口 | 定 |
| D8 | 旧目录搬空后直接删除，在 `docs/README.md` 附旧→新映射表 | Git 历史可查；AGENTS 要求不保留旧版兼容入口 | 留指路页：产生死页；新旧并存：维护两份 | 待评审 |
| D9 | 规范合并单独成 PR，不与搬家同批 | 合并涉及“先迁移要求再删页”，审阅成本高 | 同批合并：无法区分移动与内容改动 | 定 |

---

## 5. 目标结构

```text
docs/
  README.md                    # 唯一导航：任务 → 入口
  AGENTS.md                    # 保持不动
  start/
    README.md                  # 新人四步
    environment.md             # 环境、构建、测试、检查命令
    first-change.md            # 认领工单 → 改动 → 验证 → PR
    first-rule.md              # 以下为迁移进来的教程
    first-host.md
    first-relic.md
    shared-rule.md
    play-base-game.md
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
    core-developer-guide.md
    development-model.md
    combat-engine-comparison.md
  areas/
    base/  { README.md, how-to/, reference/, spec/ }
    core/  { README.md, how-to/, reference/, spec/ }
    net/   { README.md, how-to/, reference/, spec/ }
    ai/    { README.md, how-to/, reference/, spec/ }
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
    net/0.2/invariants.md
    ai-net/operations.md
    ai-net/verification.md
  meta/
    glossary.md
    roadmap.md
    workflow/remote-delegation.md
```

---

## 6. 页面映射（84 页）

### 6.1 根与接口约定

| 现路径 | 目标 | 备注 |
| --- | --- | --- |
| `docs/README.md` | 原地重写 | 任务表 + 旧→新映射 |
| `docs/roadmap.md` | `meta/roadmap.md` | |
| `docs/terminology.md` | `meta/glossary.md` | |
| `docs/AGENTS.md` | 不动 | 协作约束 |
| `docs/contracts/core-engine-baseline.md` | `specs/core-engine-baseline.md` | 跨模块接口约定 |
| `docs/contracts/net/0.2/invariants.md` | `specs/net/0.2/invariants.md` | |

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
| `design/core-the-spire/developer-guide.md` | `understand/core-developer-guide.md` |
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
| `design/mod-the-spire/<domain>/spec.md`（16 个域，20 个域文件） | `areas/base/spec/`（16 份域规格） |
| `design/mod-the-spire/character-combat/ironclad-start.md` | `areas/base/spec/ironclad-start.md` |
| `design/mod-the-spire/ironclad-dsl/shared-operations.md` | `areas/base/reference/ironclad-shared-operations.md` |
| `design/mod-the-spire/relic-combat-start/content-lifecycle.md` | `areas/base/spec/relic-lifecycle.md` |
| `design/mod-the-spire/run-domain/randomness.md` | `areas/base/spec/randomness.md` |
| `design/net-the-spire/spec.md` | `areas/net/spec/overview.md` |
| `design/net-the-spire/design.md` | `understand/net-design.md` |
| `design/net-the-spire/protocol.md` | `areas/net/reference/protocol.md` |
| `design/net-the-spire/contract-artifacts.md` | `areas/net/spec/contract-artifacts.md` |
| `design/net-the-spire/how-to-play-base-game.md` | `start/play-base-game.md` |
| `design/net-the-spire/how-to-run-public-game.md` | `areas/net/how-to/run-public-game.md` |
| `design/net-the-spire/public-client-reference.md` | `areas/net/reference/public-client.md` |
| `design/ai-the-spire/spec.md` | `areas/ai/spec/overview.md` |
| `design/ai-the-spire/architecture.md` | `understand/ai-architecture.md` |
| `design/ai-the-spire/encoder-design.md` | `areas/ai/spec/encoder.md` |
| `design/ai-the-spire/literature.md` | `understand/ai-literature.md` |
| `design/ai-the-spire/policy-contract.md` | `areas/ai/spec/policy-contract.md` |
| `design/ai-the-spire/training-design.md` | `areas/ai/spec/training.md` |

> 15 个域目录：`card-effects`、`card-exhaust`、`card-play`、`character-combat`、`combat-damage`、`combat-end`、`combat-rewards`、`combat-turn-resources`、`enemy-turn`、`event-domain`、`ironclad-dsl`、`permanent-deck-smith`、`potion-inventory`、`power-modifiers`、`relic-combat-start`、`run-domain`（其中 `character-combat`、`ironclad-dsl`、`relic-combat-start`、`run-domain` 各含两个文件，已单列）。

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

## 7. 新建页面

| 页面 | 内容要点 | 素材来源 | 验收 |
| --- | --- | --- | --- |
| `start/README.md` | 四步：装环境 → 跑检查 → 改一处 → 提 PR；每步一条命令、一个链接 | 根 README、`verify-and-debug`、`CONTRIBUTING` | 从零照做能跑通 |
| `start/environment.md` | SDK 版本、Python、构建、测试、文档检查命令 | 根 README、`tools/ci/README.md`、`tools/doc-check/README.md` | 命令可直接复制执行 |
| `start/first-change.md` | 认领工单 → 定位文件 → 改 → 验证 → PR | `CONTRIBUTING.md`、`base-spire-development` skill | 只链接规则，不复述 |
| `understand/architecture.md` | 五模块一句话职责、数据流图、三条边界（DSL 单一来源／core 不认识游戏名词／规范与现状分家） | `design/spec.md`、`base-architecture.md`、`control-flow-modules.md` | 新人读完能说出“改规则去 base，改引擎去 core” |
| `areas/base/README.md` | 来自现有开发手册，改为“改什么 → 看哪页”任务表 | 现有 README | 10 个 how-to 全部可达 |
| `areas/core/README.md` | core 任务表：DSL 语言、编译、Session API、决定 | 现有 core 文档 | |
| `areas/net/README.md` | 协议、客户端、公开运行 | 现有 net 文档 | |
| `areas/ai/README.md` | 规格、编码器、策略、训练 | 现有 ai 文档 | |
| `reference/README.md` | 查表索引：DSL、Session API、操作、Power、协议 | 现有 reference 页 | |
| `specs/README.md` | 接口约定索引：编号 + 一句话 + 验收位置 | 现有 specs | 每份接口约定唯一 |

---

## 8. 批次计划

| PR | 范围 | 文件量 | 类型 | 验收 |
| --- | --- | --- | --- | --- |
| P1 | 入口页：重写 `docs/README.md`；新建 `start/{README,environment,first-change}`、`understand/architecture.md`、`areas/{core,net,ai}/README.md`、`reference/README.md`、`specs/README.md` | +9 新，1 改 | 内容新增 | `docs check` 绿；README 覆盖六个区 |
| P2 | 教程 5 页（`first-rule`、`first-host`、`first-relic`、`shared-rule`、`play-base-game`）→ `start/` | 5 移 | 纯移动 | 链接更新；`git grep` 无旧路径 |
| P3 | base how-to 10 + base reference 5（含 `ironclad-shared-operations`）+ 开发手册 README → `areas/base/` | 16 移 | 纯移动 | 同上 |
| P4 | core 3 页、net 3 页、`reference/dsl/` 5 页 → 对应目录 | 11 移 | 纯移动 | 同上 |
| P5a | `areas/base/spec/` 22 页（16 份域规格 + 铁甲初态 + 遗物生命周期 + 随机 + base 规格 + 行为约定 + 监听约定） | 22 移 | 纯移动 | 每份规范唯一 |
| P5b | `areas/net/spec/` 2 + `areas/ai/spec/` 4 + `specs/` 8 | 14 移 | 纯移动 | 同上 |
| P6 | 解释 11 → `understand/`；meta 3 → `meta/` | 14 移 | 纯移动 | 同上 |
| P7 | 删空目录；更新 `AGENTS.md`、`CONTRIBUTING.md`、`.agents/skills/**`、`tools/*/README.md` 的旧路径；全仓 grep | — | 清理 | `git grep docs/design` 为空；`docs check` 绿 |
| P8（可选） | 合并重复：`behavior.md` 与域规范、ENV 与实现约束、两篇公开运行指南 | — | 内容改动 | 有效要求已迁移；被删页无残留引用 |

每批固定命令：

```powershell
git mv <old> <new>
python tools/ci/check.py docs
git grep -n "<old-path>"
```

要求：移动与内容修改分开提交；每批可单独 revert；P8 不与任何移动同批。

---

## 9. 检查与门禁

1. **链接与格式**：`python tools/ci/check.py docs`（含链接、格式、Vale 术语、差异格式）。
2. **类型一致**：新增检查，目录 ↔ 首行 `文档类型` 映射：
   - `start/` → 教程
   - `*/how-to/` → 操作指南
   - `*/reference/`、`reference/` → 参考
   - `understand/` → 解释
   - `*/spec/`、`specs/` → 规范
   - `meta/` → 不检查
3. **路径残留**：`git grep -n "docs/design\|docs/development\|docs/dsl\|docs/contracts"` 必须为空（P7 之后）。
4. **导航唯一**：全仓 `README.md` 中“任务 → 入口”表只允许 `docs/README.md` 一处；其他 README 只做局部索引。
5. **走查**：新增一条记录（可放 PR 描述）：新人按 `start/README.md` 完成“改一张卡数值并跑检查”。

---

## 10. 风险与缓解

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 84 页相对链接重写 | 高，容易漏 | 每批只改本批路径；`docs check` 每批执行；P7 全局 grep 兜底 |
| 外部引用（AGENTS、skill、CONTRIBUTING、tools README） | 中 | P7 统一 grep 并更新；旧 Issue／PR 链接不追 |
| “负责人”页与 AGENTS 冲突 | 中 | 不建静态名单页；“找谁”指向 GitHub #28 与 Issue 模板 |
| 规范合并删页 | 高 | D9：先迁移要求再删；合并单独 PR；逐条核对 |
| 评审后结构再变 | 中 | 映射表按目录组织，改一个目录不影响其他批 |
| 两套导航并存 | 中 | 门禁第 4 条；P1 时先删旧表再建新表 |
| 迁移期间文档不可读 | 低 | 每批只动一个目录，主入口始终可用 |

---

## 11. 评审问题清单

请评审者逐条给出结论（同意／改成 X／补条件）：

1. **D8 旧目录收尾**：删目录 + 映射表（建议）／每个旧目录留指路页／新旧并存。
2. **命名**：`areas/` 是否比 `modules/` 更清楚？`specs/` 是否该叫 `contracts/`？
3. **批次粒度**：8 个 PR（建议）／合并成 3 个／一次完成。
4. **类型标注**：沿用首行文字（建议）还是引入 front matter？
5. **`CONTRIBUTING` 摘要**：`start/first-change.md` 只链接（建议）还是摘录流程？
6. **DSL 参考位置**：`reference/dsl/`（建议，base 也用）还是 `areas/core/reference/`？
7. **`understand/` 是否保留 `decisions/` 子目录**，还是并进 `architecture.md`？
8. **是否保留 `design/` 作为 `understand/` 的别名**（兼容旧链接）？
9. **P1 的 10 个新页是否过多**：是否先只做 `start/` 三页 + `architecture.md`？
10. **`areas/base/spec/` 20 份域规范是否过细**：是否合并成 5～6 份？

---

## 12. 完成定义（DoD）

- [ ] `docs/design/`、`docs/development/`、`docs/dsl/`、`docs/contracts/` 不再存在。
- [ ] `docs/README.md` 的任务表覆盖 `start`、`understand`、`areas`、`reference`、`specs`、`meta`。
- [ ] 每页首行有类型，且与目录一致；检查通过。
- [ ] `python tools/ci/check.py docs` 通过。
- [ ] `git grep` 旧路径为空。
- [ ] 至少一次“新人按 `start/README.md` 走通”的记录（PR 描述即可）。
- [ ] 规范页与现状页无相互复制；每份规范唯一权威。

---

## 13. 附：核对依据

- 文件计数：`(Get-ChildItem docs -Recurse -Filter *.md).Count` → 84。
- 类型分布：`Select-String -Path (Get-ChildItem docs -Recurse -Filter *.md).FullName -Pattern '文档类型：'` → 32 条（操作 14／参考 9／解释 5／教程 4）。
- 差异测试现状：`tools/differential/Program.cs` 仅接受 `test --suite`；`tools/differential/suites/` 含 `regression.json`、`ironclad-act1.json`、9 个 `normal-*.json`；`test-env.ps1` 不存在于 `tools/differential/`。
- 外部依据：见第 2 节 URL。
