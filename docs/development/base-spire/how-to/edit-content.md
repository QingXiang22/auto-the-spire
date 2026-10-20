# 修改单个卡牌、Power或遗物

文档类型：操作指南（How-to）。适用于已有DSL定义的内容。需要新增定义时使用[新增内容模块](add-content-module.md)；需要共享流程尚未提供的行为时使用[机制迁移](migrate-mechanism.md)。

## 1. 定位所属文件

在仓库根目录按精确ID查询，例如：

```powershell
python -X utf8 .agents/skills/base-spire-development/scripts/locate.py cards --id base-spire:strike-ironclad
python -X utf8 .agents/skills/base-spire-development/scripts/locate.py powers --id base-spire:strength-power
python -X utf8 .agents/skills/base-spire-development/scripts/locate.py relics --id base-spire:self-forming-clay
```

先读返回的definition文件，再读它调用的准确操作。修改原版行为时核对source_path对应方法；需要验证本地来源版本时加`--verify-source`。definition为null说明尚无正式定义，不能只改元数据宣称完成。

## 2. 在拥有该规则的位置修改

| 要修改的内容 | 修改位置 | 检查点 |
| --- | --- | --- |
| 普通单体／群体攻击基础伤害 | CardDefinition的baseDamage、upgradedBaseDamage | AttackEffect／AttackAllEnemiesEffect读取CardBaseDamage，不再接收damage／upgradedDamage；普通与升级分别验证 |
| Strength的加法公式 | [strength-power.spire](../../../../mod-the-spire/base-spire/powers/shared/strength-power.spire) | 玩家与敌人有各自监听；调用方仍只施加定义和层数 |
| Weak的持续时间 | [weak-power.spire](../../../../mod-the-spire/base-spire/powers/shared/weak-power.spire)的Duration监听及定义标志 | 应用后的首次跳过与后续TickPowerDuration分别验证；不在公共回合过程新增Weak分支 |
| SelfFormingClay每次受损的收益 | [遗物模块](../../../../mod-the-spire/base-spire/relics/shared/self-forming-clay.spire)的stacks参数 | 层数由遗物施加；延迟兑现和自移除归[Power模块](../../../../mod-the-spire/base-spire/powers/shared/self-forming-clay.spire) |
| 单个Power的私有计数 | 所属模块内部entity与创建／重置／移除监听 | 不向公共Power或CardPlayRecord增加该内容专属字段 |
| 多个内容共用操作的不变量 | 对应领域rules与约定 | 明确所有受影响消费者；只改内容文件不能修复共享语义错误 |

例：[Strike](../../../../mod-the-spire/base-spire/cards/ironclad/strike-ironclad.spire)只在CardDefinition中保存6／9两个基础值，StrikeDamage只配置definition和hits。将普通值改为12时，普通实例与实际基础伤害应同步变成12；升级仍取9。正式原版数值应以匹配版本来源为准，实验值留在隔离副本。

BlockEffect仍有block／upgradedBlock；按实际模板签名修改，不批量删除所有数值参数。每张可执行牌现在必须绑定有序正文与纯ReadCardDamage；CardBaseDamage不再分派DamageScale。费用、附魔及复制入口见[行为操作参考](../reference/card-behavior.md)，不要恢复公共专属字段。

## 3. 运行对应判别

先构建一次工具；只改DSL后可以复用已构建的工具：

```powershell
dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj -v quiet
dotnet run --no-build --project tools/base-spire-dev -- validate
dotnet run --no-build --project tools/base-spire-dev -- check --content-model-only
```

内容模型组验证已登记定义、非法输入、私有状态清理及卡牌数值来源。SelfFormingClay专属判别可运行：

```powershell
dotnet run --no-build --project tools/base-spire-dev -- check --relic-content-only --filter CLAY
```

正式数值改变后，同步更新对应的显式行为预期；不能原样运行旧数值断言再把失败解释为工具不通用。CLAY组中的数值实验自行创建副本，将3改为4并检查实际层数和格挡；它不会把正式遗物改成4。

新增判别应区分本次可能出错的行为，例如普通／升级、层数归零、移除、两个实例或嵌套出牌。定向组不生成全包覆盖报告；批次收尾再按[验证指南](verify-and-debug.md)生成完整验证记录。仅修改内容数值时，无需重写项目级设计或逐次运行全部Engine／公开路径检查。

## 4. 核对修改范围

保持标识和来源不变时，修改一个公式通常只涉及所属玩法文件及对应判别；不需要重新登记index或公开ID。若必须同时修改多个内容调用方、公共schema和共享生命周期，先确认是在补真实共享能力，还是把该内容的规则放错了位置。

PR 说明实际改动和限制；共用接口变化才更新接口参考。任务状态更新路线图或对应 PR。
