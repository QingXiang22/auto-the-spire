# ENV-T04 vulnerable-decay 定向交接（2026-10-11）

> 负责人已复核并将阻塞及接续并入[主清单T04](../../tofix/README.md#env-t04)；[本轮独立复核](dsh-review-and-parallel-plan-2026-10-11.md)保存完整17项及后续拆分。转入records不表示case已通过。

## 身份与结论
- task_id: ENV-T04-vulnerable-decay-20261011
- dispatch/request_id: ae2c45af-93ea-4f87-b2b3-357242a9f0d2
- session_id: session-fb139ad7-8c0c-4fbe-ab7a-f4ab685b00ee
- prompt_sha256: 9b3b22fc4aa29649c4b38fca01ffa79cf0c86b39df024dcdda37fd172c302781
- 工作区：F:/Scylier-Project/active/auto-the-spire；原 task-record/task.txt 未改。
- **本包未完成严格 case 升级，不能验收为 passed。** 原机制可实际运行；两侧 Bash 施加和回合结束后的 Vulnerable 2→1 一致。但保留原全部比较范围时，敌方回合后的牌区构成存在首差，保持 unsupported_rng_profile。无脚本期新随机消费不足以证明初始私有洗牌序列兼容。

## 最终修改与边界
- [严格 suite](../../../../tools/differential/suites/regression.json)：只更新目标 case 的实际能力/阻塞说明；原 ID、脚本、seed、setup、必需维度和 17 分母保留。
- [宽松 fixture](../../../../tools/differential/fixtures/fixtures-v1.json)：目标 case 补来源及准确说明，恢复 unsupported profile。
- [定向自检](../../../../tools/differential/ComparatorSelfTest.cs)：新增 mutant/vulnerable-duration；原有 field/action/collection/decision/stream 项全部保留。
- [工具 README](../../../../tools/differential/README.md)：定向能力、命令、首差和未验证范围。
- Comparator.cs 曾尝试专用 profile 与牌区过滤，因违反不缩减原比较范围的约束全部撤销；最终无专用 profile/过滤逻辑。
- 未修改 base/core/net/AI/reference/公共身份 golden/共享主清单；未重做 Headbutt；未 reset/清理/提交。其他 16 case 未作定向编辑，但没有原始逐字快照，不能声称已完成逐字摘要对照。

## 输入、构建与最终命令
```powershell
dotnet build tools/differential --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-vulnerable
# 最终构建 rc=0，0 warning，0 error
dotnet .artifacts/parallel/dsh-vulnerable/bin/Differential/debug/Differential.dll test --suite regression --only vulnerable-decay-via-bash --output .audit/dsh-vulnerable-decay-2026-10-11/final-strict
# 实际 rc=1
```
- 实际执行为 DLL，不是报告内自动生成的 dotnet run reproduction 文本。
- suite: regression / env-t04-regression@1；scope base-spire-singleplayer-dev-v1；Ironclad A0；source baseline sts2-frozen-2026-10-05；runtime 9.0.10。
- seed: ENV-T04-CTRL-vulnerable-decay-via-bash；hand Bash，enemy_hp [38,51]，player_hp 80，energy 3；脚本 play_card Bash→enemy:0，end_turn。
- required_dimensions: actions, decisions, facts, power；missing_dimensions=[]；expected_terminal running，base 实际 Running。
- build_id: controlled-slice-v1:headbutt=False
- session_identity: a24cd83cb43d0350|seed=2c71d68f42cadc03|random=mega-random/xoshiro256ss-splitmix64@1|draw=vanilla-uniform@1|participants=player
- suite SHA256: b783c9942d93f62a2632f6ec10633eb7ef1fcc82d1c905851e976a87e89830ed
- Differential.dll SHA256: aa32f65a6c01b6a19ff08821696a5d9286f6f4e4c2d0a5387dc51da6a227d096
- reference HEAD: 2cdaea3b6dbc20cfa340662fa73640f20bb6fb00
- sts2 VulnerablePower.cs SHA256: 8fd6a82db0c6dd460a2f1d118628c4ab2600dc97f93a31c672e84ee4a3147126
- reference VulnerablePower.cs SHA256: d86901e4ac208b2fa6b462dff3c44d7b73071545ce2a2c1257c632f51bd9ea66

## 实际执行与负例
- [最终报告](../../../../.audit/dsh-vulnerable-decay-2026-10-11/final-strict/regression.json)，run_id regression@env-t04-regression@1+20261010T165503Z。
- [base 实际记录](../../../../.audit/dsh-vulnerable-decay-2026-10-11/final-strict/records/base/vulnerable-decay-via-bash.json) 与 [独立 reference 实际记录](../../../../.audit/dsh-vulnerable-decay-2026-10-11/final-strict/records/reference/vulnerable-decay-via-bash.json)：各执行 1 case、2 脚本动作、3 边界，status 均 ok。reference_ran=1，未复制 base 结果。
- 两侧 start：目标无能力；Bash 后目标 HP 38→30，Vulnerable stacks=2；end_turn 后 Vulnerable stacks=1，Ritual stacks=2。
- 最后共同边界：boundary[1]，Bash 结算。
- 首差：boundary[2].pile.draw.sorted；base 为 4 Strike，reference 为 2 Defend+2 Strike。同边界还有 pile.hand.order/sorted 差异。不能把该证据自动认定为生产 Vulnerable 规则错误。
- 定向最小负例 mutant/vulnerable-duration：对真实 base 记录最终 power.enemy.base-spire:calcified-cultist.0.stacks 作 1→2 变异，与未变异同侧记录比较；实测 detected=true，路径 boundary[2].power.enemy.base-spire:calcified-cultist.0.stacks: 2 ≠ 1。它证明比较器抓持续时间错误，不代表独立 reference 负例执行，也不把 unsupported verdict 冒充 diverged。

## 源依据与未验证范围
- [sts2 VulnerablePower:59–65](../../../../sts2/src/Core/Models/Powers/VulnerablePower.cs#L59-L65)：AfterSideTurnEnd 仅 side==Enemy 时 TickDownDuration。
- [独立 reference:53–59](../../../../reference/sts2-sim/src/Sts2Sim.Core/Models/Powers/VulnerablePower.cs#L53-L59)：同一 Enemy 侧规则。
- Bash 经真实应用路径施加，无 setup.powers 替代。
- 原脚本只有 Bash 与一次 end_turn：没有分离玩家侧/敌方侧中间边界，没有衰减后再次攻击；因此不能声称动态证明“不在玩家侧衰减”或衰减后 x1.5 伤害效果，也未验证第二次衰减/移除。
- reference 不提供脚本期随机计数；其 false 是适配器声明而非独立计数测量。base false 是实际计数差。随机兼容尚未证明。
- 全局状态：expected=17，selected/executed=1，passed=0，unsupported=1，complete=false。自检 7 项中 6 detected，mutant/stream 因子集无 stream 目标失败；未放宽或删除。完整 suite 与其他 16 case 未运行，不作全局通过结论。

## 中间实验留痕
- positive：原 profile 执行 rc=1，unsupported，有相同牌区首差。
- positive-final：临时专用 profile rc=1，diverged，draw 首差。
- positive-final2：临时 draw 过滤 rc=1，hand 首差。
- positive-final3：此前构建因 CS1513/CS1002 失败，随后执行的是旧 DLL；**非最终有效验证**。
- 中间 JSON 检查曾因 PowerShell 默认非 UTF8 解码报错；不可把随后的 OK 文本当成成功解析证据。最终严格入口成功载入配置并实际运行。
- 所有临时 profile/过滤撤销后重新构建 rc=0，再生成 final-strict；仅 final-strict 作为最终结论。

本包到此停止；后续若解决初始牌序可比性，应由负责人另行明确范围，不能以删牌区事实/换 seed/补偿随机完成本 case。
