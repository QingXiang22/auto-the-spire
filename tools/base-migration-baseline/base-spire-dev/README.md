# 卡牌开发工具接口

文档类型：参考（Reference）。完整操作步骤和已运行例子见[modding指南](../../docs/design/mod-the-spire/modding-guide.md)。

构建 `BaseSpire.Dev.csproj` 后，命令参数为：

```text
check FILE.spire [FILE...]
play FILE.spire [FILE...] QUALIFIED_CARD_ID normal|upgraded
```

接受显式 `base/0.0.2` 卡牌文件；无版本或 `base/0.0.1` 输入经兼容前端适配。新版本支持 common 整字段共享、normal/upgraded 显式替换（空 play/hooks/keywords 清空）、编译期 values 和组合数值表达式。通过生产 `SpireCardSource`、实际能力链接和 `CombatSlicePackage` 进入Engine Session。输入内容必须全部支持；新卡与base定义合并，先注册所有输入文件的Power，再链接卡牌；同ID冲突及卡牌/Power之外的声明拒绝。只读输入，不保存玩法状态。

JSON输出的 `declared` / `parsed` / `lowered` 分别是源声明、前端读取和正式领域降级；`registered` 是受控初始牌组中实际存在的输入卡ID数量；check包含全部输入卡，play只包含指定卡。`play` 包含出牌前后生命/格挡/能量/handCount、决策次数及弃牌/消耗结果。`ruleset` 属于本次探针内容，不是正式工厂的公开构建身份。

### 手动试玩 `play_game.py`

```powershell
python -X utf8 tools/base-spire-dev/play_game.py --seed ENV-T03-SEED-L
```

显示公开状态与数字动作菜单，手动处理地图、战斗、目标选择和房间。输入编号执行，`d 编号`查看公开字段，`p`查看公开牌区，`r`刷新，`q`退出。默认使用负责人2026-10-12已验证的Host；`--host-dll`可指定其它构建，`--scope dev-map|dev`选择范围。客户端不编译源码、不修改玩法状态。

返回码0表示用户退出或已取得终局；4表示Host拒绝、结果不确定或没有可执行动作；2表示命令参数/Host路径错误。退出本身不代表游戏通关。完整操作见[手动试玩指南](../../docs/design/net-the-spire/how-to-play-base-game.md)。

返回码0表示本次检查或出牌完成；2表示用法、读取、解析、链接、未支持、重复ID、组包或执行失败。结果带 `scope = controlled-card-probe`；不自动省略未支持内容。种子固定20261001，输入卡组成探针牌组；先选择第一个敌人，其余决策选择第一个候选。升级模式从长期牌组创建完整升级实例，采用升级费用、变量初值、关键词和入场hook；不是永久升级过程的检查。跨文件Power可以使用同命名空间局部名或限定ID；同名异包必须限定引用。Power的powerDeclared/powerLowered/powerIds表示新声明的加载，不是触发行为覆盖率。

`verify_examples.py` 在已构建Debug工具上检查指南例子和失败分支，输出15次调用的返回码、结果及进程耗时；断言失败为非零。它不执行完整环境门槛。隔离构建可通过 `BASE_SPIRE_DEV_DLL` 指定实际工具 DLL；未设置时使用默认 Debug 产物。

### 公开语义重放冒烟 `act1_smoke.py`

候选句柄自 2026-10-11 起按**会话**派生，同 seed、同观察版本、同动作语义的两局不再共享字面 `candidate_id`。
因此 `act1_smoke.py` 不再把字面候选 ID 当输入，而是记录/重放公开动作语义 `{action_type, fields}`（`fields`
按 `field_id` 排序，语义辅助在 `semantic_actions.py`）：每一步在**实时** `legal_actions` 里找唯一匹配的候选，
再提交它当前的 `candidate_id` 与 `observation_id`。字面候选 ID 只作原始帧证据。

```powershell
# 重放（固定 Host 必须显式给出；不会自动构建，也不会回退到仓库内共享旧 DLL）
python -X utf8 tools/base-spire-dev/act1_smoke.py `
  --host-dll .artifacts/parallel/owner-flow-review-next-20261011/bin/OwnerHost/debug/Net.TheSpire.Host.dll `
  --fixture tools/base-spire-dev/fixtures/act1-semantic-dev-map-prefix.json `
  --output .audit/act1-public-smoke

# 记录：从公开轨迹提取语义动作（run_game.py 的 frames.ndjson，或本工具的 public-trajectory.jsonl）
python -X utf8 tools/base-spire-dev/act1_smoke.py `
  --record-from <DIR>/frames.ndjson --fixture-out tools/base-spire-dev/fixtures/<NAME>.json --scope dev-map
```

| 参数 | 说明 |
| --- | --- |
| `--host-dll PATH` | 固定 `Net.TheSpire.Host.dll`；未给时必须由 `NET_THE_SPIRE_HOST_DLL` 提供，否则返回码 2（不自动构建） |
| `--fixture FILE` | 语义夹具；默认 `fixtures/act1-semantic-dev-map-prefix.json` |
| `--output DIR` | 输出目录：`public-trajectory.jsonl`、`semantic-steps.jsonl`、`summary.json`，首差还有 `first-difference.json` |
| `--record-from FILE` | 记录模式：从公开轨迹提取语义动作 |
| `--fixture-out FILE` | 记录模式写出的语义夹具 |
| `--scope NAME` | 记录模式使用的 scope 名（默认 `dev-map`） |

夹具 schema 为 `base-spire-dev/semantic-actions@1`：`identity`/`seed`/`scope`/`scenario_profile` 固定构建与开局，
`initial_facts` 与每个动作的 `facts` 是**会话无关**的公开游戏事实（引用字段按公开身份摘要与公开集合定位表示），
`actions[].recorded.candidate_id` 只是记录时的原始帧证据。`mode=full-act` 的夹具还必须满足原第一幕完成条件
（开场 80 HP/99 金币/10 张起手、Boss 与宝物房间、`ActCompleted` + `MapPhase 6`、存活 HP、恰好一个
`ContinueToNextAct`、药水动作、不得发布整局胜利）；药水沿用原不变量（领取保留真实公开实例，使用/丢弃后离开栏位），
`SharedRelicBagEntry` 与 `MapDiagnostics` 词表仍不得发布。

匹配缺失、多义或引用身份不符都带首差停止，不取第一个替代、不推进引擎；每步之后逐项核对记录的公开事实。
退出码：`0` 复放完成（`mode=prefix` 只证明前缀重放器本身，`summary.not_claimed` 明确不声明 `ActCompleted`）、
`1` 首差（语义/公开事实/身份/已知故障未复现）、`2` 用法与夹具错误（含只记字面 `candidate_ids` 的旧夹具，会打印
迁移入口）、`3` 未满足第一幕完成条件（含把前缀夹具当整幕跑）。

当前固定构建只能走到 `ACT1-BASIC-10` + `dev-map` 的公开前缀（第 58 步奖励后选择下一房间时宿主
`backend_fault`：`base-spire:fogmog-normal cannot execute: missing enemy behavior base-spire:fogmog`），
所以**完整第一幕冒烟命令尚缺当前流程交付**：本包的 `fixtures/act1-semantic-dev-map-prefix.json` 只是该前缀的
语义记录，重放它会连同这条已知故障一起原样复现（Observation 不推进）。旧 `fixtures/act1-basic-10.json`
保留可追溯，但它是会话绑定前的字面候选夹具，本工具明确拒绝重放。它只读公开响应，不访问 Engine 状态或随机数；
只证明开发范围中的一条路线，不代表任意 seed、所有房型或独立原版对拍通过。
