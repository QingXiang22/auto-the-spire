# 规则源与差异测试依赖

当前规则依据为 [sts2-sim 上游](https://github.com/iRyougi/sts2-sim) 的锁定提交，同时使用该提交的原生实现作为比较后端。[锁定清单](baselines/sts2-sim.json)记录 repository、commit、tree、源码位置及其声明的游戏基线。每次更新先核对上游最新 main，再固定实际 SHA；构建和测试不使用浮动分支。

生产 core／base／net 不引用该项目。base 仍将规则迁移为 DSL，由同一 Engine 执行；不能直接调用模拟器执行生产玩法。参考 checkout 保持纯上游，不应用规则补丁。更新只切换干净上游提交，实际报告绑定提交、tree、程序集和适配器摘要。

在仓库根目录恢复锁定源码并运行比较：

```powershell
python tools/differential/prepare_reference.py
python tools/ci/check.py differential
```

准备脚本只在目标缺失时创建克隆；已有克隆须符合锁定 commit／tree 且无本地改动，否则拒绝覆盖。离线恢复可传入 `--source <已有纯上游克隆路径>`。检出目录被忽略，不提交第三方源码。修改锁定清单前须实际编译和运行差异检查，严格比较的非零结果原样保留。

规则源码入口为 `reference/sts2-sim/src/Sts2Sim.Core/`。当前上游声明游戏基线 v0.111.0；普通构造函数使用顺序随机，`CreateKeyedForLabels` 属于不同用途的重键模式，不能作为正常 Run 比较路径。上游尚未实现的终局和已知保真差异继续单列，不能因参考缺失而缩减本项目阶段验收条件。

本地 `sts2/` 不再作为当前规则裁决依据，也不上传 GitHub。现有 `tools/base-spire-dev/fixtures/source-catalogs.json` 与随机／开局向量记录旧实现来源；它们尚须按新基线重新核对，不能改写摘要后继续当作新版测试依据。旧受控适配只验证其明确的旧场景，不构成新规则源符合性结论。迁移状态在 [GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)维护。

DSL 与模拟器分开执行和比较，但规则移植与比较共享同一来源；一致结果不能单独证明真实客户端保真。其他本地研究仓库不属于当前规则依据。
