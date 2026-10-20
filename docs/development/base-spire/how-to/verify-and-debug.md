# 验证 DSL 修改并定位错误

文档类型：操作指南（How-to）。在仓库根目录运行命令，使用固定 SDK。纯文字修改仅检查文档；可执行示例和 DSL 修改还需实际加载与所承诺的行为。

## 选择能证明本次结果的检查

先构建一次开发工具；仅修改DSL时可复用已构建入口。不要在公开Host轨迹运行期间继续改包文件：包摘要包含文本和静态元数据，父进程与子进程读到不同版本会造成expected_identity不匹配。

| 修改范围 | 先运行 | 结论边界 |
| --- | --- | --- |
| 文档／导航／skill 路由 | 文档检查、skill 校验、实际查找演练 | 不能证明玩法 |
| 已有正式包／纯文本边界 | 开发工具 validate | 解析、绑定、元数据与启动；不穷举行为 |
| 当前正式 base 机制（遗物触发、战斗／回合资源、伤害与修正参与者、组合式出牌链、离场分流与胜负判定） | 开发工具 check | 执行工具明确列出的内容行为、永久牌组与角色初态场景；不能通用验证任意新增内容，需检查实际报告见证 |
| 教程配置／教学模板 | 教程真实 stdio 驱动，传明确期望 | 实际公开结果，非原版独立参考 |
| 共享 DSL／通用编译／事件 | 相关机制与 Engine 检查、必要消费者 | 原型通过不等于完整游戏 |
| 公开 schema／投影／候选 | 相关 net 检查与真实公开流程 | 隐藏信息、合法候选和协议身份 |
| 原版完整范围恢复 | 原严格 suite、真正独立参考与正常流程 | 不按可玩子集删分母 |

修改单个内容可先运行`check --content-model-only`；SelfFormingClay已有判别用`check --relic-content-only --filter CLAY`。其他内容按实际检查名前缀选择，已有组不自动验证新内容；编写入口见[内容修改](edit-content.md)和[新增模块](add-content-module.md)。

常用包命令如下：

```powershell
dotnet run --project tools/base-spire-dev -- validate
dotnet run --project tools/base-spire-dev -- describe
dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-spire-check
```

第三条只适用于该工具当前承诺的 base 机制。新增正式内容的行为见证必须实际执行，不能将旧报告、教学报告或仅定义存在的数量算作覆盖。最终集成与 Actions 的各组入口见[CI 指南](../../../../tools/ci/README.md)。

遗物调试先构建受影响工具，再运行`dotnet run --no-build --project tools/base-spire-dev -- check --relic-content-only --filter "AM "`。筛选按检查名称的不区分大小写前缀匹配；`AM `为攻击修正组，其他组先从实际检查名称确认。输出逐项耗时、执行数与跳过数；零匹配失败，不能用该命令生成整包报告。完整check和覆盖门槛在批次最终CI执行，参考[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。

文档检查使用以下命令：

```powershell
python -X utf8 tools/doc-check/check_docs.py
git diff --check
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

公开候选和观察带身份／版本。每次修改后重新 describe／catalog／start，不复用旧会话或缓存 candidate_id。超时结果不确定时按现有客户端恢复协议处理，不盲目重复提交。

## 保存可审阅证据

记录实际命令／退出码、代码版本、包摘要与规则身份、scope、seed、源定位与行为前后值。区分静态源存在、实际 DSL 定义、已运行行为、独立比较和正常 Run。长日志留 artifacts，主清单只维护当前事项及证据链接。

原 differential regression 当前仍逐项未支持并返回 3，完整源遗物覆盖也未满足。migration 检查验证保留与失败语义，不是原版对拍成功。相关历史判断意图见[CI 迁移夹具](../../../../tools/ci/fixtures/legacy-dsl-checks.json)。
