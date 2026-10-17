# Sown 与 SapphireSeed 阶段交付 — 2026-10-17

本记录补齐此前主清单已引用的交付证据入口，根据已有结果整理，没有重新运行游戏测试。当前状态与后续安排只维护在[当前交付](../../tofix/README.md#当前交付)；本批仍待验证，不关闭 PR04、M01 或正常首幕里程碑。

## 实现范围

卡实例增加附魔身份、数量和禁用状态；复制保留附魔，新牌不继承旧附魔。Sown 在牌本体执行后、AfterPlay 前处理：玩家存活且附魔状态为 Normal 时获得能量，然后禁用；禁止获得能量时仍消耗一次性状态。SapphireSeed 的 Eat／Plant 使用真实永久牌候选，沿既有单选暂停、续接与离场接口运行。公开层发布附魔字段并保护请求重放和过期选择。

主要入口为 `CardEnchantments.cs`、`SapphireSeedEvent.cs`、`SownChecks.cs` 和 `tools/differential/SownCombatChecks.cs`。规则层、受控公开、独立对照和正常路线分别记证，不能互相替代。

## 已有证据与限制

原始结果位于仓库本地 `.audit/sown-20261017/`，构建位于 `.artifacts/sown-20261017/`，均不纳入 Git。本次读取以下结果文件，未重新执行其中命令。

| 结果文件 | 已记录的结论 | 证明边界 |
| --- | --- | --- |
| `combat-reference/result.json` | rc0；普通／升级各两次出牌，4 个边界一致 | 受控战斗；包含复制路径差异说明，不证明正常整局 |
| `sapphire-reference/result.json` | 三组固定 seed 的目录进入、Eat、首张 Plant 共9项通过；3项非首张 Plant 有参考差异，整体 rc1 | 参考自动选首张，生产允许玩家选择实例；该比较保持 blocked_compare |
| `suite-results.json` | regression 和9组正常流程 suite 为 rc0，act-transition 为 rc0；ironclad-act1 为 rc1 | 历史套件结果，不证明新增事件在正常路线到达 |

此前执行阶段报告了定向8项、Slice runtime867项及源码1项、Engine99项、net422项、Python事件12项通过；这些是上批交接结果，本次未复跑。此前正常探索未取得 Sown，正常公开路线的实际获取和使用仍缺证据。后续不继续搜索 seed；按预先选定的少量计划或相应里程碑补齐。

参考锁定为 `2089659bd07920088b93226136c72b6b6ee590ad`。不修改参考玩法以消除非首张 Plant 差异，也不用受控状态注入代替正常路线。Slither、WoodCarvings、其他附魔和 Afflict 不在本批范围。
