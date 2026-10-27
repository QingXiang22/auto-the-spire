# 验证 DSL 修改并定位错误

文档类型：操作指南（How-to）。在仓库根目录运行命令，使用固定 SDK。纯文字修改仅检查文档；可执行示例和 DSL 修改还需实际加载与所承诺的行为。

## 选择能证明本次结果的检查

内容扩展默认整批编辑后运行定向检查，完整回归和覆盖报告交给批次PR CI。按[批次规则](../../../../CONTRIBUTING.md#base-内容按批次交付)确定本批依赖和实际用例，不能只运行无关的旧内容检查。以下命令一次构建并执行拾取生命上限遗物与SelfFormingClay两组；仅作选择示例，不是每批必跑组合：

```powershell
python tools/ci/check.py content --focus "relic:RL pickup " --focus relic:CLAY
```

focus支持遗物／卡牌的检查名前缀以及敌人、内容模型等已有组，准确参数见[本地内容入口](../../../../tools/ci/README.md#本地内容批次检查)。默认输出每步退出码、耗时与日志位置；失败显示末尾日志。未选组、空前缀或零匹配均失败，不生成全量覆盖报告。本地不默认运行base／net／migration全套；共享机制、公共接口或比较适配变化时按影响扩大验证，并在PR说明触发原因。

先构建一次开发工具；仅修改DSL时可复用已构建入口。不要在公开Host轨迹运行期间继续改包文件：包摘要包含文本和静态元数据，父进程与子进程读到不同版本会造成expected_identity不匹配。

| 修改范围 | 先运行 | 结论边界 |
| --- | --- | --- |
| 文档／导航／skill 路由 | docs；改变脚本或路由时再核对相应入口 | 不能证明玩法 |
| 已有正式包／纯文本边界 | 开发工具 validate | 解析、绑定、元数据与启动；不穷举行为 |
| 已有机制的内容扩展／数值调整 | content加相关focus；复用参数化用例 | 只证明实际执行的内容；完整报告交给批次PR CI |
| base共享机制变化 | 受影响的机制组与代表性消费者 | 核对顺序、实例、随机和失败边界，不逐内容复制全套 |
| 教程配置／教学模板 | 教程真实 stdio 驱动，传明确期望 | 实际公开结果，非原版独立参考 |
| 共享 DSL／通用编译／事件 | 相关机制与 Engine 检查、必要消费者 | 原型通过不等于完整游戏 |
| 公开 schema／投影／候选 | 相关 net 检查与真实公开流程 | 隐藏信息、合法候选和协议标识 |
| 原版完整范围恢复 | 原严格 suite、真正独立参考与正常流程 | 不按可玩子集删除必测用例 |

修改单个内容可先运行`check --content-model-only`；SelfFormingClay已有判别用`check --relic-content-only --filter CLAY`。其他内容按实际检查名前缀选择，已有组不自动验证新内容；编写入口见[内容修改](edit-content.md)和[新增模块](add-content-module.md)。

常用包命令如下：

```powershell
dotnet run --project tools/base-spire-dev -- validate
dotnet run --project tools/base-spire-dev -- describe
dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-spire-check
```

第三条是CI或完整报告排障入口，不是每批本地必跑步骤，只适用于工具当前承诺的base机制。新增正式内容的行为执行结果必须实际执行，不能将旧报告、教学报告或仅定义存在的数量算作覆盖。最终集成与Actions的各组入口见[CI指南](../../../../tools/ci/README.md)。

遗物调试先构建受影响工具，再运行`dotnet run --no-build --project tools/base-spire-dev -- check --relic-content-only --filter "AM "`。筛选按检查名称的不区分大小写前缀匹配；`AM `为攻击修正组，其他组先从实际检查名称确认。输出逐项耗时、执行数与跳过数；零匹配失败，不能用该命令生成整包报告。完整check和覆盖验收条件在批次最终CI执行，参考[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。

文档检查使用以下命令：

```powershell
python tools/ci/check.py docs
```

## 从最小错误定位

| 症状 | 查看哪里 | 处理方式 |
| --- | --- | --- |
| 未引用 DSL 文件 | 领域 index 与根 includes | 显式包含；不改成目录自动扫描 |
| namespace／片段包属性冲突 | 错误指向的片段头部 | 同包同 namespace／core/0.3；start／version／imports 归根入口 |
| 未知模板参数／超范围 | 内容 use 与模板 parameters | 按准确参数修正；不扩大边界掩盖源数值错误 |
| 内容模块被拒绝 | catalog definition 指向的文件 | 至少包含一个 DSL 声明；只保留 dsl_version 头部，start／imports／includes／version 等包属性放根入口或索引 |
| unknown field／可执行 JSON | manifest、catalog 或 metadata 的实际 schema | 删除未知字段或迁移行为到 DSL，不让 Host 解释 effects 字符串 |
| unsupported_capability | manifest 支持范围、CLI 场景 | 接受退出 3 并实现缺失范围；不回退旧工厂 |
| legal candidate 不存在 | 当前公开 snapshot、action_type、guard | 检查完整 `net:action:...` ID 和真实状态；不构造候选绕过守卫 |
| 字段没公开 | DSL visibility、projection 和元数据 | 验证应公开的事实，Hidden 字段不强行泄漏 |
| 有声明但数值错误 | 发事件的位置、实际持有者、模板参数、调用顺序 | 用能区分错误的固定输入观察前后，不直接抄结果 hash |
| 源 hash 不匹配 | catalog 与对应本地源文件 | 先确定版本／换行差异并记录，不能静默更新来源证明 |

公开候选和观察带标识／版本。每次修改后重新 describe／catalog／start，不复用旧会话或缓存 candidate_id。超时结果不确定时按现有客户端恢复协议处理，不盲目重复提交。

## 保存可审阅验证记录

记录实际命令／退出码、代码版本、包摘要与规则标识、scope、seed、源定位与行为前后值。区分静态源存在、实际 DSL 定义、已运行行为、独立比较和正常 Run。长日志留 artifacts，主清单只维护当前事项及验证记录链接。

原 differential regression 的 strike-normal 与 random-reshuffle-unsupported 已使用生产 DSL 与锁定独立参考执行。Strike 通过；重洗抽取逐次随机调用并保留真实首差；其余 15 个用例仍未支持，完整测试集返回 3。migration 检查验证受控机制比较、随机诊断及严格失败语义，不代表正常 Run 或完整原版验收；完整源遗物覆盖也未满足。运行方式及比较范围见[差异测试入口](../../../../tools/differential/README.md)，原检查去向见[CI 迁移数据](../../../../tools/ci/fixtures/legacy-dsl-checks.json)。
