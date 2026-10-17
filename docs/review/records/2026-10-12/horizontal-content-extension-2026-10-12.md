# 内容横向扩展复核与接续 — 2026-10-12

卡牌与怪物已有按文件自动发现的正式入口；负责人补齐可组合的地图房型入口和独立事件模块目录，并用两个文件事件实际验证选择隔离、永久写回和访问关闭。真实问号概率/事件选择仍由后续流程包实现。当前认领与派发只查[主清单](../../tofix/README.md)。

## 扩展接口

- 卡牌：`SpireCardSource.CardResources()`按`BaseSpire.ContentSource.*-cards.spire`排序发现；怪物：`SpireEnemySource.EnemyResources()`按`enemies*.spire`发现。`BaseSpire.csproj`嵌入所有`ContentSource/*.spire`，`SinglePlayerContentSource`逐资源降级并汇总；正式当前加载176卡/38敌人，无逐内容注册白名单。资源加载与源卡池/遭遇池资格是两件事，不以注册数宣称完整语义覆盖。
- 地图：新增`Rooms/MapRoomEntry`，现有房型进入指令移到`Standard`组合，原`RegisterChooseMapPoint`签名仍兼容；新重载接处理器列表，重复类型拒绝，未登记类型明确失败。旧房型的声明/IR顺序保持，具体新房型只增加模块和组合项。
- 事件：`IEventRoomModule`分别拥有ID、进入与选项编译；`EventRoomCatalog`按登记顺序生成分派，不依赖具体事件类。`EventCompileContext`绑定实际Engine字段与phase，各动作按当前ID/phase守卫，关闭清当前ID并恢复旅行。它们不保存Session、RunState或随机状态。源选择器与可执行目录分开，未注册的已选事件明确失败。
- `docs/examples/base-events/GoldEvent.cs`与`HealEvent.cs`独立文件共享目录。例子通过真实地图选点进入预置事件，分别金币10→17、生命50→55，关闭后phase2、事件清空、旧选择不再合法；重复ID及未知已选事件反例成立。它是受控接口例子，没有运行源问号概率或原版事件池。

实现约束写入[mod AGENTS](../../../../mod-the-spire/AGENTS.md#内容横向扩展)。[操作指南](../../../design/mod-the-spire/how-to-add-a-room.md#横向扩展问号事件)保留完成任务步骤，[接口参考](../../../design/mod-the-spire/room-compile-reference.md#横向注册地图入口与事件)给签名和语义，[组织解释](../../../design/mod-the-spire/base-architecture.md)给职责；沿用Diátaxis分类和NBJ表达，不另建待办入口。

## 负责人实际检查

工作目录为仓库根，固定共同输出`.artifacts/parallel/owner-horizontal-extension-20261012`；后续A不得覆盖此输入，B/C用它编隔离工具/Host。

| 命令/检查 | 实际结果 | 证据与范围 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln --artifacts-path <上述绝对目录>` | rc0，0错误/5既有警告 | [.audit构建日志](../../../../.audit/owner-horizontal-extension-build-20261012.log) |
| 同闭包Engine/Slice测试DLL，串行 | rc0，99/775通过 | [Engine](../../../../.audit/owner-horizontal-extension-engine-20261012.log)、[Slice](../../../../.audit/owner-horizontal-extension-slice-20261012.log)，包括新源排序5项 |
| `dotnet run --project docs/examples/base-events --artifacts-path <独立例子目录>` | rc0，上述两个模块与反例通过 | [例子日志](../../../../.audit/owner-horizontal-extension-example-20261012.log)；仅受控例子 |
| `Slice.Tests.dll --describe-build` | dev=`2dab0c5b3c3a4034`、dev-map=`6c1e1be04af1492b`；公开构建=`4a08d5bbaf4270be`/`9d9f43c0d1b58809` | [身份](../../../../.audit/owner-horizontal-extension-identities-20261012.jsonl)，与已交排序包相同，地图注册拆分没有再变身份 |
| `Differential.dll test --suite regression --output .audit/owner-horizontal-extension-regression-20261012` | rc1，17全执行，15双侧/1单侧/1Profile未支持，自检0失败 | [原结果](../../../../.audit/owner-horizontal-extension-regression-20261012/regression.json)；重洗4边界、完整维度、两侧无差异，但不自动判通过 |
| 同闭包net测试DLL | rc1，413passed/3failed/416执行 | [原日志](../../../../.audit/owner-horizontal-extension-net-20261012.log)，V03/V09/V10均旧切片缺SourceStableShuffle compute backend；不改断言压掉，交C收口 |
| 旧共同商店帧0.2契约helper | rc1；wire/商店0违规、Host声明84违规 | [实际结算](../../../../.audit/owner-horizontal-extension-shop-contract-20261012.json)，两局各42个公开声明ID不闭合，沿I71/I84修Host，不放宽契约 |

重洗定向`--only`运行还报告一个decision mutant缺改目标；这是子集没有decision case，完整17运行自检0失败。保留[定向日志](../../../../.audit/owner-horizontal-extension-reshuffle-20261012.log)，不把子集称完整验收。

固定Differential闭包的实际SHA见[摘要](../../../../.audit/owner-horizontal-extension-closure-sha256-20261012.json)。独立规则检查不抵真实第一幕、多幕胜负、正常奖励/事件原版双侧或真实客户端验收；本轮未执行这些范围。现有地图/商店/药水生产成果保留，卡牌批量/召唤复活/AI仍暂缓。

## 交付后的安排

A使用已交接口实现真实问号房，源概率、队列选择、访问生命周期和事件叶分模块；B在固定共同闭包上结算原重洗case的完整源兼容条件；C修公开声明闭合及旧切片的计算后端依赖。文件边界和实际新会话以[主清单](../../tofix/README.md)与对应task-record为准。派发接受不等于工作完成。

三份新请求实际均返回`accepted`，会话独立；[A记录](../../../../.audit/dsh-unknown-room-modules-20261012/task-record.json)、[B记录](../../../../.audit/dsh-strict-reshuffle-close-20261012/task-record.json)、[C记录](../../../../.audit/dsh-host-declarations-close-20261012/task-record.json)保留原请求与提示词摘要。没有恢复旧会话，没有等待或宣称新包完成。

文档检查扫描436篇，新链接未报错，返回4仅两份既有交接的四个`#L`锚点；[检查日志](../../../../.audit/owner-horizontal-extension-doc-check-20261012.log)。`git diff --check`返回0。按已安装交接skill的六项定位检查正文、主清单与短工作包；此为负责人接手自查，不称独立读者验证。
