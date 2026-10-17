# 公共整局操作与卡牌原语组合交付

日期：2026-10-12。Codex `/root`按用户派单完成ENV-T09共享操作与ENV-T06原语组合重构，当前认领/范围只看[主清单](../../tofix/README.md)。本记录描述实际代码和证据，不关闭完整房间族、多幕、全部卡牌或原版环境。没有派发会话、提交共享工作区、修改core或参考玩法。

接手新增内容时先查[公共能力入口](../../../design/mod-the-spire/room-compile-reference.md#按操作查找已有公共能力)，按[选牌组合指南](../../../../tools/base-migration-baseline/docs/base-development-guide.md#用通用选牌编写卡牌)写本内容的条件/数值/顺序。已有能力直接组合；新机制补对应领域规格与操作，再接实际消费者。不得恢复已经移除的步骤执行器。

## 实际交付

`Run/RunResources`补LoseGold/GainMaxHp；`RunRelics.Obtain`集中受支持遗物实例创建、玩家/共享袋移除和AfterObtained，复用同一个资源服务。`RunDeck.Create/Acquire/Remove`集中永久牌组操作，捕获实例引用、保持候选身份，并提供入组后与移除前的编译hook接缝。资源输入只读一次，局内可变状态仍在Engine。

`RoomCompilation.RoomServices`一次创建这些能力，商店、宝箱、营火、奖励和普通事件共享所需操作。商店减少遗物字段参数，购买/删牌保留各自支付顺序和正式决定；宝箱注册只传package。袋抽取、价格/回复量、已领取/已售和访问关闭仍由来源拥有；没有搬动随机抽取。具体限制见[整局公共操作契约](../../../design/mod-the-spire/run-domain/spec.md)。

`SelectCardsEffect`和`RandomCardEffect`把选择与onSelected子程序分开；升级、克隆、移动和控制流直接组合。递归链接/变量校验/当前牌绑定/hook限制覆盖子程序，显式提示现在保留。短写chooseCard/cloneCard/upgradeCards/randomMoveCard展开成相同原语，旧.author只在输入端翻译。删除运行期CardCommandEffect/CardCommandKind和CardCommandCompiler；正式前端不再调用旧步骤适配。Feed使用攻击、目标存活条件与GainMaxHp，Armaments升级版使用遍历及升级。

运行期模型清单：[primitive-inventory.json](../../../../.audit/owner-shared-run-20261012/primitive-inventory.json)。当前加载176定义，174定义的普通/升级和hooks均由通用原语组合；Thrash保留攻击伤害预览/成长，HowlFromBeyond保留自身自动打出。这个数字只说明已加载定义的表示，不代表完整原版卡池或176张均有独立双侧证据。其余具体机制边界见[原语契约](../../../design/mod-the-spire/card-primitives/spec.md)。

新增判别仅3项整局命令和2项组合：资源输入快照/支出下限；遗物金币hook观察已创建实例且双袋已移除；永久牌组身份与移除前hook；选择后升级→复制→移动→恢复后续；外层快照、子程序Power链接与未绑定实例拒绝。删除选牌夹具只断言内部节点类型的一条断言，保留全部行为检查。

## 构建与验证

统一构建根：`.artifacts/parallel/owner-shared-run-20261012`；日志与真实产物：[证据目录](../../../../.audit/owner-shared-run-20261012/)。最终solution构建使用`dotnet build AutoTheSpire.sln --nologo --artifacts-path .artifacts/parallel/owner-shared-run-20261012 -v:q`，返回0、4项既有base警告；未改警告相关代码。

| 实际执行 | 结果/证据 |
| --- | --- |
| Core.TheSpire.Engine.Tests.dll | 99通过，返回0，engine.log；core未修改 |
| BaseSpire.Slice.Tests.dll完整入口 | 794通过，返回0，slice-final.log |
| Net.TheSpire.Tests.dll完整入口 | 418通过/0失败，返回0，net-final.log |
| Differential.dll test --suite regression | 原17 ID完整保留，16双侧passed+1self_checked、0自检失败，返回0；regression-final/regression.json |
| 正常公开Host，dev-map、ENV-T03-SEED-L、48动作 | 48步成功，返回3=step_limit，非整局完成；public-final/frames.ndjson、steps.ndjson、summary.json |

公共Run命令使用已交只读审阅驱动器`drive_unknown.py`，显式`--host-dll`指向同构建Host，参数`--scope dev-map --seed ENV-T03-SEED-L --max-actions 48 --use-potions --shop-room auto --shop-policy buy --shop-buy-kind card --shop-remove`。无状态注入或调游标；第23步正常买牌、第47步真实Sunken金币+106/生命-7、第48步进入下一商店。金币不足，前缀未执行删牌；删牌仍是正式商店受控检查证据。正式客户端通用事件策略仍未交付。

与前批事件领域48步证据比较：动作和完整记录公开状态一致，忽略会话候选ID与观察编号；结果见[前缀比较](../../../../.audit/owner-shared-run-20261012/public-prefix-comparison.json)。不是独立原版整局对拍。最终Run.Hp60/MaxHp80/Gold179、MapPhase7；保留原16房队列等现有范围。

## 身份与首差处理

| scope | 旧ruleset/publicBuild | 新ruleset/publicBuild |
| --- | --- | --- |
| dev | 2dab0c5b3c3a4034 / 4a08d5bbaf4270be | f568b3218da80445 / 89667f311bbaa06d |
| dev-map | 639ca07b5a486fbd / 6127d4d3959c048c | ded56b348ee3a7f6 / cedae31020fea3b5 |

选择子程序和Feed改变两个scope的卡牌IR；资源/遗物/永久牌组再改变地图scope。新身份来自实际Describe，基线仅在现有行为与严格17成立后迁移，旧值留档。首次完整Slice遇旧dev身份断言；迁移后又捕获cloneCard错误消息未带源位置，修复后完整794通过。失败日志保留slice.log与slice-source-location-failed.log，不删检查或吞失败。core保持engine/ir@0.4及既有版本，未新增引擎版本。

## 文档与未验证

已维护mod AGENTS、DSL作者约定、开发指南、房间接口参考、编排/效果IR规格和架构说明；删除旧执行器后修复历史交接中的链接并保留原日期语境。`python tools/doc-check/check_docs.py`扫描454份Markdown、返回4，只报两份旧交接中的4处无效行号锚点，本次新增/迁移链接未报错；`git diff --check`返回0。日志doc-check-final.log/diff-check.log；构建、规则、实际内容加载、公开前缀与独立比较分列。

遗物叠加/替换/移除、完整金币/治疗修正参与者、永久卡获得替换和全部hook/地图历史未交付；不让后续内容通过默认空链伪造支持。SwordOfStone、事件战斗返回、跨幕及其余卡牌仍按主清单接续；本轮未补批量卡牌、召唤复活、AI、性能矩阵或归档套件。
