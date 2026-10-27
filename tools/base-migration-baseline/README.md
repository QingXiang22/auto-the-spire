# 旧 C# base 的迁移对照源码

本目录保存迁移前的玩法与检查源码，用于恢复行为意图。它不进入活动 solution、生产加载或运行回退。当前开发入口是 [base 手册](../../docs/areas/base/README.md)，可运行检查见 [CI 说明](../ci/README.md)。

`BaseSpire/`、`BaseSpire.Slice.Tests/`、`net-tests/`和`differential/`保留原实现。`tools/ci/check_dsl_migration.py`使用 [迁移数据](../ci/fixtures/legacy-dsl-checks.json)核对固定源码和原 suite 标识；它不把旧检查计作当前执行结果。

迁移时先从正式领域规格与当前 DSL 寻找等价操作，再按需要读取准确的旧方法／case。旧源码中保留的历史路径、DLL 和注释只反映其时点，不作为本轮构建前置条件。新的行为进入当前 DSL 与自动检查；普通历史文档通过 Git 查询。
