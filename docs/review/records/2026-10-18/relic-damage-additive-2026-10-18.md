# 遗物伤害加性修正（来源牌过滤）与 StrikeDummy／FakeStrikeDummy

任务：**ENV-T09-PR02-CONTENT**。本轮目标：把“按伤害来源牌加算伤害”做成可复用机制，并纳入两件只依赖 Strike 标签的遗物。

## 变更

1. **效果种类 `modify-damage-additive`**（源 `RelicModel.ModifyDamageAdditive`，只在 powered 攻击上生效）：`RelicCompilation.ModifyDamageAdditive` 生成带 `Card` 参数的纯过程 `RelicModifier_modify_damage_additive`，逐持有遗物检查未熔毁、定义匹配与牌条件后累加 `Amount`。
2. **牌条件**：`CardTag`（用 `CardDefinitions` 的标签→定义 id 集合做成员判断）、`CardUpgradedOnly`、`CardEnchantedOnly`；三者与既有 `CardKind` 一起在 `CardCondition` 里判断。
3. **接线**：`CardEffectCompiler` 的 `Damage` 分支在 `powered` 时把伤害量交给 `CardEffectBindings.ModifyDamage`；`CardCompilation.CompileEffects` 增加 `damageModifier` 参数并在绑定时传入当前牌；composer 绑定到 `relicPrograms.ModifyDamageAdditive`。

## 关键事实：标签名是“命名空间 + 小写”

作者写 `tags = { Strike }`，但 `Lower` 会经 `NamespaceTag` 归一成 **`base-spire:strike`**。第一次按 `"Strike"`、第二次按 `"base-spire:Strike"` 声明都匹配不到（伤害仍是 6），用临时探针读出真实值后改为 `base-spire:strike` 才生效。这一点写进记录，避免后续内容重复踩坑。

## 消费者与验证

| 遗物 | 源行为 | 声明 |
| --- | --- | --- |
| StrikeDummy（Common，`StrikeDummy.cs:11`） | Strike 标签的 powered 攻击 +3 | `modifier/modify-damage-additive` 3、`CardTag=base-spire:strike` |
| FakeStrikeDummy（Event，`FakeStrikeDummy.cs:11`） | 同上 +1 | 同上，Amount 1 |

- 场景（500 血敌人、能量充足）：Strike 打 9（6+3）／7（6+1），随后 Bash 仍打 8（非 Strike 标签不吃加成）——能同时区分“加成了”和“只对该标签加成”。
- 遗物内容组 **87/87 通过**；覆盖 **88/88 可执行**、source=297、**unimplemented 210 → 208**，校验器 exit 0；`unsupported` 326。
- 身份迁移：dev `5858a21346fb08c8`／`29767f2dea35cdc9`、dev-map `95e6cfafcfd4d535`／`e32e93050d8cdc68`。
- 公开语义 10/10、遗物生命周期与 Run 14/14、文档与差异检查通过。

## 未验证与下一步

- 只有受控内容场景证据；无独立双侧、正常整局或公开轨迹，完整回归仍交 Actions。
- `CardUpgradedOnly`／`CardEnchantedOnly` 已实现但还没有消费者：MiniatureCannon（升级牌 +3）与 MysticLighter（带附魔牌 +9）需要“受控把一张牌设为升级/附魔”的检查写法，留作下一批；那也顺带验证两个过滤分支。
