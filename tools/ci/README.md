# core、纯 DSL base、net 与迁移检查

本页给出 [GitHub Actions](../../.github/workflows/core-base.yml) 的检查入口。需要根 `global.json` 指定的 .NET SDK、Python 3.12 和 Git，在仓库根目录执行。默认 Release，可用 `--configuration Debug`。

```powershell
python tools/ci/check.py core
python tools/ci/check.py base
python tools/ci/check.py net
python tools/ci/check.py migration
```

| 组 | 实际执行 | 结论边界 |
| --- | --- | --- |
| core | 构建并执行通用 Engine 检查，包括同包文件组合 | 不等于原版玩法验收 |
| base | 通用加载并启动纯 DSL 包、执行当前机制行为、核对 DSL 遗物及卡牌见证 | 开发 scope；卡牌门槛要求源池 85 张单人铁甲战士完整且有实际行为见证，保留 87 项来源及两项多人排除 |
| net | 通用 wire／text-rules 检查和纯 DSL 公开路径，包含真实 stdio 子进程 | 原正常 Run 的 snapshot／control／formal 检查尚未迁移 |
| migration | 构建依赖隔离、153 个冻结源组与 11 份原 suite 定义校验、原 regression 逐项报未支持 | 检查迁移契约，不执行独立原版比较 |

首次非零使当前组失败。原始日志、退出码及 `result.json` 写入 `.artifacts/ci/<组名>/`，Actions 上传各组 artifact。base 的行为报告标记 `independent_verified: false`；迁移清单中的原检查均标记未执行。冻结文本 SHA256 采用 UTF-8 与 LF，跨检出换行保持一致。

卡牌门槛可单独复查：

```powershell
python tools/ci/check_card_coverage.py .artifacts/ci/base/behavior/package-behavior.json --require-ironclad-complete
```

它校验当前包摘要、578 项全卡来源、冻结铁甲池及源 SHA、普通／升级元数据对应的行为报告和全部已声明卡牌见证；不将局部 Ironclad 门槛解释为全卡或原版独立比较通过。冻结元数据来自主源快照，CI 不需要本地 `sts2/`。

## 原完整对拍与覆盖缺口

```powershell
python tools/ci/check.py differential
python tools/ci/check_relic_coverage.py .artifacts/ci/base/behavior/package-behavior.json --require-complete
```

当前这两条命令分别返回 3 和 1。原 regression 的 17 个 case 全部明确未支持，零执行；完整源遗物覆盖仍缺，不能以局部行为见证关闭完整验收。Actions 当前矩阵显式采用 `migration`，没有以 `continue-on-error` 吞掉原对拍失败。

旧 C# Slice、作者探针、Net 游戏专用组与独立适配器保留在[迁移对照](../base-migration-baseline/README.md)。逐组源身份、原场景名和恢复去向见[检查清单](../../docs/review/records/2026-10-18/base-dsl-check-disposition.json)。本次用户授权直接切换及功能回退，不代表原判断意图消失。

## 其他检查

修改文档运行 `python tools/doc-check/check_docs.py` 和 `git diff --check`。本地原版源码核对、Python 客户端完整轨迹、AI 离线检查、正常 Run、完整对拍／120 对矩阵及性能均不包含在上述四组中。相关契约仍见[当前清单](../../docs/review/tofix/README.md)、[ENV 范围](../../docs/design/environment-completion-and-differential-v1.md)和[参考版本](../../reference/README.md)。不得从四组通过推导这些项目已经验收。
