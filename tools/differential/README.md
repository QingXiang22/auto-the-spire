# DSL 与独立参考比较

`regression` 的原 `strike-normal` 使用当前生产 DSL 包和锁定的 `sts2-sim` 分别执行。其余 16 个原用例及其他 10 份 suite 仍明确报告 `unsupported`；11 份原定义、必测 ID 和比较维度保持不变。完整 regression 执行 1 例、通过 1 例，返回 3，测试集的 `independent_verified` 为 false。

在仓库根目录准备只读参考，再运行比较：

```powershell
python tools/differential/prepare_reference.py
dotnet run --project tools/differential -- test --suite regression --output .artifacts/differential/regression
```

准备脚本按[参考锁定清单](../../reference/baselines/sts2-sim.json)获取公开上游的提交；已有检出必须符合 commit／tree 且无改动，否则拒绝覆盖。可用 `--source <已有纯上游克隆路径>` 离线准备。缺参考时仍可构建工具，执行比较明确非零；`-p:UseReference=false` 可验证未编入适配器时的失败。

定向检查使用 `--case strike-normal`。报告保留完整 `expected_case_ids` 并列出 `selected_case_ids`，单例 `passed` 与测试集完成分开：`complete: false`，退出码 1，不能代替完整测试集。`self-test` 执行真实双侧并验证比较器的缺数据、错误终点、随机消费及首差失败路径。`--suite-file <JSON路径>` 用于校验测试数据和复现定义错误，要求保留原必测 ID、注册一致和本例全部原维度；自定义输入的 `complete` 为 false，报告保存其 SHA256，不能宣称原测试集完成。

本例保持原受控语义：10 张起始牌组、手牌仅一张普通 Strike、两个敌人 HP 为 38／51、玩家 HP 为 80、能量 3，打向 `enemy:0` 后保持 running。原 `game_seed` 保留；DSL 使用该字符串配置源流，参考沿用原受控初始化的数值 seed 20261001。双方初始化消费分别记录，脚本消费必须实测为 0；不对初始化随机流作一致结论，不验证正常开局、敌人行动或整局。

比较在初态和动作完成边界进行：

- `actions`：可提交牌的语义槽位和绑定数、结束回合。DSL 的 `PlayTargetedCard`／`PlayCardFromHand` 与 `EndDevelopmentTurn` 对应原 `PlayCard`／`EndTurn`；开发设置动作不属于原动作投影。
- `decisions`：DSL 实际暂停的目标候选、数量及回答；参考由自己的合法目标 API 记录同一选择。
- `facts`：玩家 HP／最大 HP／格挡／能量、两敌人的 HP／最大 HP／格挡／存活与能力。
- `pile-order`：四个牌区的成员数量和构成，以及手牌／耗尽区顺序。原无随机用例不比较随机初始化的抽牌堆／弃牌堆顺序，后续随机用例须按自身原维度恢复。

报告保存双方完整边界、包摘要／规则标识、参考 commit／tree／程序集摘要和受控输入；发生差异时给出首差维度、路径、双方值、最后一致边界及复现命令。缺参考、缺用例、零执行、缺维度、错误终点、随机消费、异常和差异均非零。

[迁移检查](../ci/README.md)执行该原用例及严格失败检查，校验原 suite 和生产依赖隔离。原源与旧适配器见[迁移对照](../base-migration-baseline/README.md)，逐组去向见[CI 迁移数据](../ci/fixtures/legacy-dsl-checks.json)。单例机制比较不替代完整[ENV 验收](../../docs/design/environment-completion-and-differential-v1.md)；参考玩法保持只读。
