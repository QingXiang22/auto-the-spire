# ENV-T04-headbutt-choice-20261011 交接 — 2026-10-11

> 2026-10-11：本交接已并入主清单并归档至records；负责人复核见[本轮推进](../2026-10-11/parallel-main-progress-2026-10-11.md)，当前状态与下一包只查主清单。

本包把原 `headbutt-mid-choice` 从历史 `unsupported_decision_model` 升级为独立双侧实际执行：base 与 reference 都真实完成“先选目标、再从两个弃牌候选中选牌、将所选牌置于抽牌堆顶”。本报告不关闭 ENV-T04 parent；当前状态、主责和验收结论仍以主清单为准。

## 归属与接手入口

- 任务：`ENV-T04-headbutt-choice-20261011`，B 严格对拍；负责人已认领。
- 会话：`session-85bdb279-671e-4458-a505-a5177525005f`。
- 任务审计身份：`.audit/dsh-headbutt-choice-2026-10-11/task-record.json`（未修改）；`task.txt`（未修改）。
- 入口：`docs/README.md` → `docs/review/tofix/README.md` 中 ENV-T04 详情；工具方法见 `tools/differential/README.md` 的“Headbutt 实际选牌定向检查”。
- 可修改边界：`tools/differential/` 与 `.audit/dsh-headbutt-choice-2026-10-11/`；本固定交接文件。未修改 base/core/net/ai、参考实现、主清单及任务身份文件。

## 接手后先做什么

1. 先核对共享工作区和主清单认领，不重做本包：`git status --short`，读取 `tools/differential/suites/regression.json` 的 `headbutt-mid-choice` 定义和本报告的 positive 记录。
2. 在仓库根目录按隔离路径重跑：

   ```powershell
   dotnet build tools/differential --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-headbutt
   dotnet .artifacts/parallel/dsh-headbutt/bin/Differential/debug/Differential.dll test --suite regression --only headbutt-mid-choice --output .audit/dsh-headbutt-choice-2026-10-11/positive
   powershell -NoProfile -ExecutionPolicy Bypass -File tools/differential/check-headbutt-choice.ps1
   ```

   预期：构建返回 0；定向 `test` 返回 1（`--only` 必须 `complete=false`，不是全 suite 验收）；机制检查返回 0，并重新确认双侧两决策、置顶牌、原 17 项分母、其他 16 项未变及改变选择的负例 `diverged`。
3. 将本包证据交负责人；只有负责人可以把主清单状态、parent 或关闭结论改为已验收。

## 已交付与接口变化

- `tools/differential/suites/regression.json`：保留原 case ID、目标 `enemy:0`、选择 `card:discard:0`、seed、`actions/decisions/facts` 和 `expected_terminal=running`；弃牌从一张增加为 `defend-ironclad` 与 `strike-ironclad` 两候选；`reference_scope` 改为 `compared`，移除过时 internal/unsupported 说明。
- `tools/differential/FixtureSuite.cs`：`FixtureStep` 新增 `ObserveDrawTop`（JSON 字段 `observe_draw_top`），用于只在指定结算边界读取抽牌堆顶。
- `tools/differential/EngineSnapshot.cs`、`BaseBackend.cs`、`ReferenceBackend.cs`：在步骤边界按 fixture 选择性抽取 `pile.draw.top`；两侧各从自身牌区读取，不注入或复制对方结果，也不比较受控开局随机牌序。
- `tools/differential/check-headbutt-choice.ps1`：定向审计检查脚本，验证原分母与其他 16 case、双侧实际决策/约束/牌区结算，并用独立改变选择的 reference 记录验证负例。
- `tools/differential/README.md`：补充单例选择为何不触发真实 decision source、定向命令和严格子集返回码说明。

首因与决策：参考 `reference/sts2-sim/src/Sts2Sim.Core/Commands/CardSelectCmd.cs:113-116` 对不可取消单候选直接返回；只有多候选才在 `:124-125` 调用 `ChooseCardsAsync`。因此没有改参考或生产玩法，而是在原 case 内增加第二个合法弃牌候选，保留原选择与全部必需维度。

## 验证与证据

| 检查 | 工作目录与实际命令 | 返回码/结果 | 证据 |
| --- | --- | --- | --- |
| 隔离基线构建 | 仓库根；`dotnet build tools/differential --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-headbutt` | 0 | `.audit/dsh-headbutt-choice-2026-10-11/build-baseline.log` |
| 修改后隔离构建 | 仓库根；同上 | 0 | `.audit/dsh-headbutt-choice-2026-10-11/build.log` |
| 原 case 双侧定向执行 | 仓库根；隔离 DLL `test --suite regression --only headbutt-mid-choice --output .audit/dsh-headbutt-choice-2026-10-11/positive` | 1（严格子集契约） | `positive/regression.json`：expected 17、selected/executed 1、reference_ran 1、case passed、complete false；`positive/records/base|reference/headbutt-mid-choice.json` |
| 改变选择的 reference 独立执行 | 仓库根；`test --suite .audit/dsh-headbutt-choice-2026-10-11/changed-choice-suite.json --only headbutt-mid-choice --output .audit/dsh-headbutt-choice-2026-10-11/changed-choice` | 1（严格子集契约） | `changed-choice/regression.json`：reference_ran 1、case passed；reference 记录实际选 `card:discard:1`，抽牌堆顶为 `base-spire:strike-ironclad` |
| 预期失败负例 | 仓库根；原 suite + `--records .audit/dsh-headbutt-choice-2026-10-11/changed-choice/records/reference`，输出 `negative` | 1（预期） | `negative/regression.json`：`diverged`；首差为 boundary 1 `step:0:play_card` 的 `boundary[1].decisions[1].chosen`：`card:discard:0` ≠ `card:discard:1` |
| 综合机制检查 | 仓库根；`powershell -NoProfile -ExecutionPolicy Bypass -File tools/differential/check-headbutt-choice.ps1` | 0 | `.audit/dsh-headbutt-choice-2026-10-11/mechanism-check.json` |
| 文档/空白检查 | 仓库根；`python tools/doc-check/check_docs.py`、`git diff --check` | 待本交接后执行 | 本包只修改工具说明/交接，未运行完整规则或全量 suite |

`mechanism-check.json` 的实际摘要：positive executed=1、reference_ran=1、original_denominator=17、unchanged_other_cases=16、changed-choice reference_ran=1、negative exit=1、negative verdict=`diverged`。positive 两侧均为 2 个 boundary、2 个 decisions；目标为 `enemy:0`，选牌候选为 `card:discard:0,card:discard:1`，约束 1..1，原选择 `card:discard:0`；结算后抽牌堆顶为 `base-spire:defend-ironclad`，弃牌构成为 `headbutt,strike-ironclad`，抽牌堆 size=9，手牌 size=0。

## 差异、阻塞与未验证

- 原 case 无双侧差异：positive report 的 case verdict=`passed`、divergences=[]；base/reference 均 `status=ok`，均独立运行，不使用 base 记录冒充 reference。
- 负例首差准确保存于 `.audit/dsh-headbutt-choice-2026-10-11/mechanism-check.json`：`boundary_index=1`、`kind=decision`、`path=boundary[1].decisions[1].chosen`、left=`card:discard:0`、right=`card:discard:1`、最后一致边界为 `0:start（受控初始状态）`。
- 严格完整 17-case suite 未运行，按用户要求只运行相关 case 与最小负例；因此不能宣称 parent 或完整 suite 通过。定向命令的 rc=1 是 `--only`/`complete=false` 契约和 self-test 覆盖限制，不是该 case 的双侧失败。
- `positive` 报告的 comparator self-test 仍有既有 `mutant/stream` 覆盖不足（1 个 self-test failure）；本包未放宽比较器或伪造 stream，范围外问题交负责人判断。
- 未运行完整 base/core/net 规则套件、正常整局、公开出口或全量卡牌；这些不属于本机制包。

## 维护位置与交付边界

- 当前状态、认领、依赖、验收和 parent 关闭：仅由负责人维护 `docs/review/tofix/README.md` 对应项。
- 本次命令、首差和产物：`.audit/dsh-headbutt-choice-2026-10-11/`；工具方法说明：`tools/differential/README.md`。
- 本次维护无待续生产工作；负责人复核证据并决定是否验收。不要自行关闭 parent、扩展到其他 regression case、修改主清单或参考玩法。
