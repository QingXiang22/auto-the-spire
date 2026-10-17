using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-T06 原版攻击序列族（`TwinStrike` / `Bash` / `Thunderclap`）的**正式源行为检查**。
///
/// 期望值独立取自同版本 sts2 原版（不看当前 base 的输出）：
/// - `sts2/src/Core/Models/Cards/TwinStrike.cs:1`：费用 1 / Attack / Common / AnyEnemy / `DamageVar(5m, Move)`，
///   OnPlay 用 `DamageCmd.Attack(...).WithHitCount(2)`；升级 `Damage.UpgradeValueBy(2m)` ⇒ 5×2 → 7×2。
///   `sts2/src/Core/Commands/Builders/AttackCommand.cs:513` 的 `Execute` 在 `for (i &lt; attackCount)` 里**每一击各调一次**
///   `CreatureCmd.Damage(...)`，所以逐击加成（力量/格挡/易伤）按击结算：两击 5 与一击 10 在"力量 2"下分别是 14 与 12。
/// - `sts2/src/Core/Models/Cards/Bash.cs:1`：费用 2 / Attack / Basic / AnyEnemy / `DamageVar(8m)` + `PowerVar&lt;VulnerablePower&gt;(2m)`；
///   OnPlay 顺序是**先** `DamageCmd.Attack(...)` **后** `PowerCmd.Apply&lt;VulnerablePower&gt;(...)`；升级伤害 +2、易伤 +1 ⇒ 10 后 3 层。
/// - `sts2/src/Core/Models/Cards/Thunderclap.cs:1`：费用 1 / Attack / Common / AllEnemies / `DamageVar(4m)` + `PowerVar&lt;VulnerablePower&gt;(1m)`；
///   OnPlay 顺序是**先** `TargetingAllOpponents(...)` 打完全体，**后**对 `CombatState.HittableEnemies`（`CombatState.cs:142`）
///   各上 1 层；升级伤害 +3 ⇒ 7，层数仍 1。
///
/// 受控边界：会话由生产入口 <see cref="FormalSpirePackage"/> 建立（正式内容源，seed 20261001），
/// 但"起手无能力""力量/易伤""升级标记""把指定的正式牌拿进手牌"这些**初态由检查自己写入**（已逐条标注"受控前置"）。
/// 因此这里只说明"这族内容在受控切片里的行为"，**不代表**正常整局，也不是独立双侧对拍。
/// 需要第二张进攻牌证明"敌方易伤真的生效"时，用的是拿进手牌的另一张正式牌（Bash / Strike），不是伪造的第二实例：
/// 同一实例回手后在本回合二次打出会被 `PlayedThisTurn` 集合判 `MembershipConflict`（见本包证据
/// `.audit/dsh-env-t06-native-attack-sequences-2026-10-09/replay-same-instance-membership-conflict.md`），
/// 那是引擎侧的契约缺口，本文件不依赖它、也不在此绕过。
/// </summary>
internal static class NativeAttackSequenceChecks
{
    private const string TwinStrike = "base-spire:twin-strike";
    private const string Bash = "base-spire:bash";
    private const string Thunderclap = "base-spire:thunderclap";
    private const string Strike = SliceManifest.StrikeId;
    private const string Strength = SliceManifest.StrengthId;
    private const string Vulnerable = SliceManifest.VulnerableId;
    private const string Calcified = SliceManifest.CalcifiedCultistId;
    private const string Damp = SliceManifest.DampCultistId;

    public static void Run()
    {
        Check.Run("ENV-T06 攻击序列族：受控初态无无关能力，三张牌由正式包真实注册 [真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var front = FormalSliceFixture.Cultist(session, Calcified);
            var back = FormalSliceFixture.Cultist(session, Damp);

            Check.True(FormalSliceFixture.PowerIds(session, player).Count == 0,
                "玩家起手身上没有能力（本族隔离前置；实得 "
                + $"[{string.Join(", ", FormalSliceFixture.PowerIds(session, player))}]）");
            Check.True(FormalSliceFixture.PowerIds(session, front).Count == 0, "前排起手没有能力");
            Check.True(FormalSliceFixture.PowerIds(session, back).Count == 0, "后排起手没有能力");

            // 注册证据从**会话状态**读（卡组里有这张牌的实例），而不是解析声明数：
            // 正式包把每张降级卡各放 1 张进卡组，能拿出来就说明它真的过了"降级 → 组包 → 加载"。
            foreach (var cardId in new[] { TwinStrike, Bash, Thunderclap })
            {
                var card = FormalSliceFixture.MoveToHand(session, cardId);
                Check.Equal(cardId, FormalSliceFixture.Definition(session, card), $"'{cardId}' 的实例来自正式包卡组");
                Check.True(InPile(session, FormalSliceFixture.HandPile, card), $"'{cardId}' 已在手牌里");
            }
        });

        Check.Run("ENV-T06 攻击序列族：声明→解析→降级→逐效果程序四态与原版契约对齐（不静默省略）[真实·声明/降级层]", () =>
        {
            // 正式加载入口（不是测试自己写的文本）：能力链接表也来自正式能力源。
            var links = SpirePowerSource.PowerResources()
                .SelectMany(resource => SpirePowerSource.LoadResource(resource).Powers)
                .GroupBy(power => power.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
            var source = SpireCardSource.LoadResource("BaseSpire.ContentSource.ironclad-cards.spire", links);

            // 期望来自同版本 sts2 原版（见文件头；这里只列**卡级契约**：费用 / 目标域 / 能力 / 层数 / 升级是否改它）。
            // 注意 id 是**源级**形式（`SpireCardSource.Source.LoweredIds` 的约定），组包时才加 `base-spire:` 前缀。
            var expected = new[]
            {
                (Name: "TwinStrike", Id: "twin-strike", Cost: 1, Target: "one-opponent-alive",
                    Power: string.Empty, Stacks: 0, UpgradedStacks: 0),
                (Name: "Bash", Id: "bash", Cost: 2, Target: "one-opponent-alive",
                    Power: "Vulnerable", Stacks: 2, UpgradedStacks: 3),
                (Name: "Thunderclap", Id: "thunderclap", Cost: 1, Target: "all",
                    Power: "Vulnerable", Stacks: 1, UpgradedStacks: 1),
            };

            foreach (var (name, id, cost, target, power, stacks, upgradedStacks) in expected)
            {
                Check.True(source.Declared.Any(entry => entry.Name == name), $"'{name}' 在 ironclad-cards.spire 里有声明");
                var parsed = source.Parsed.Where(entry => entry.Name == name).ToList();
                Check.Equal(1, parsed.Count, $"'{name}' 解析成 1 条卡声明");
                Check.False(source.Unsupported.Any(entry => entry.Name == name),
                    $"'{name}' 没有被记为未支持（不静默省略、不回退 .author）");
                if (parsed.Count != 1) continue;
                var declaration = parsed[0];

                Check.Equal(id, declaration.Id, $"'{name}' 的源级 id");
                Check.True(source.LoweredIds.Contains(id), $"'{name}' 在降级结果里出现");
                Check.True(declaration.Upgradeable, $"'{name}' 可升级");
                Check.True(declaration.Pools.Contains("IroncladCardPool"), $"'{name}' 属于 IroncladCardPool");
                Check.Equal(cost, declaration.Normal.Cost, $"'{name}' 普通费用");
                Check.Equal(cost, declaration.Upgraded.Cost, $"'{name}' 升级不改费用");
                Check.Equal(target, declaration.Normal.TargetKind, $"'{name}' 普通目标域");
                Check.Equal(target, declaration.Upgraded.TargetKind, $"'{name}' 升级不改目标域");
                Check.Equal(power, declaration.Normal.PowerId,
                    $"'{name}' 普通施加的能力（声明层保留能力**名**，运行时 id 由链接表解析）");
                Check.Equal(stacks, declaration.Normal.Stacks, $"'{name}' 普通层数");
                Check.Equal(upgradedStacks, declaration.Upgraded.Stacks, $"'{name}' 升级层数");

                // 正式执行依据是**逐效果程序**（不是卡级显示摘要）：两个变体都必须降级出程序。
                // 注意 `Declaration.Id` 是源级形式，而 `AuthorCard.Id` 已带包前缀，两者不是一个字段。
                var qualifiedId = "base-spire:" + id;
                var card = source.Cards.SingleOrDefault(entry => entry.Id == qualifiedId);
                Check.True(card is not null, $"'{name}' 降级出 {qualifiedId}");
                Check.True(card!.NormalProgram is not null, $"{qualifiedId} 普通形态有正式逐效果程序");
                Check.True(card.UpgradedProgram is not null, $"{qualifiedId} 升级形态有正式逐效果程序");
            }
        });

        Check.Run("ENV-T06 攻击序列族：TwinStrike 普通形态对同一目标打两次 5 [真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var target = FormalSliceFixture.Cultist(session, Calcified);
            var other = FormalSliceFixture.Cultist(session, Damp);
            var card = FormalSliceFixture.MoveToHand(session, TwinStrike);

            Check.Equal(1L, CardField(session, card, "Cost"), "来源费用 1");
            var hp = Hp(session, target);
            var otherHp = Hp(session, other);
            var energy = Energy(session, player);

            var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, card, target);

            Check.Equal(1, decisions, "selectOne：整张牌只提问一次目标");
            Check.Equal(10L, hp - Hp(session, target), "5 × 2 击 = 10（同一目标）");
            Check.Equal(otherHp, Hp(session, other), "只打被选中的目标，不是全体");
            Check.Equal(energy - 1, Energy(session, player), "按来源费用 1 付款");
            Check.False(InPile(session, FormalSliceFixture.HandPile, card), "打出的牌离开手牌");
            Check.True(InPile(session, FormalSliceFixture.DiscardPile, card), "打出的牌进入弃牌堆");
            Check.Equal(0L, FormalSliceFixture.PowerStacks(session, target, Vulnerable), "TwinStrike 不施加易伤");
        });

        Check.Run("ENV-T06 攻击序列族：TwinStrike 两击各自吃一次逐击加成（力量 2 ⇒ 14，一击 10 只会有 12）[真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var target = FormalSliceFixture.Cultist(session, Calcified);
            var card = FormalSliceFixture.MoveToHand(session, TwinStrike);

            // 受控前置：直接挂 2 层力量（原版 StrengthPower 的语义是"每一击加一次"，见 SliceManifest 的能力摘要）。
            FormalSliceFixture.GivePower(session, player, Strength, 2);
            var hp = Hp(session, target);

            var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, card, target);

            Check.Equal(1, decisions, "仍然只提问一次目标");
            Check.Equal(14L, hp - Hp(session, target),
                "两击各自 +2 ⇒ (5+2)×2 = 14；若把两击合并成一次 10 点，只会是 12");
        });

        Check.Run("ENV-T06 攻击序列族：TwinStrike 升级形态 7×2（只置升级标记，不写数值槽）[真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var target = FormalSliceFixture.Cultist(session, Calcified);
            var card = FormalSliceFixture.MoveToHand(session, TwinStrike);

            // 受控前置：只把实例标成升级，**不写** Amount/PowerStacks 等卡级显示槽 ——
            // 升级数值必须来自 `UpgradedProgram`（`CombatSlicePackage.cs:4697` 按 `Upgraded` 字段选变体）。
            MarkUpgraded(session, card);
            var hp = Hp(session, target);
            var energy = Energy(session, player);

            var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, card, target);

            Check.Equal(1, decisions, "升级形态同样只提问一次目标");
            Check.Equal(14L, hp - Hp(session, target), "升级每击 7 × 2 击 = 14");
            Check.Equal(energy - 1, Energy(session, player), "升级不改变费用（仍 1）");
        });

        Check.Run("ENV-T06 攻击序列族：Bash 先打 8 再上 2 层易伤（不是被自己的易伤放大成 12）[真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var target = FormalSliceFixture.Cultist(session, Calcified);
            var other = FormalSliceFixture.Cultist(session, Damp);
            var card = FormalSliceFixture.MoveToHand(session, Bash);

            Check.Equal(2L, CardField(session, card, "Cost"), "来源费用 2");
            var hp = Hp(session, target);
            var energy = Energy(session, player);

            var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, card, target);

            Check.Equal(1, decisions, "selectOne：只提问一次目标");
            Check.Equal(8L, hp - Hp(session, target), "先结算伤害 8（若易伤先上，会是 8×1.5=12）");
            Check.Equal(2L, FormalSliceFixture.PowerStacks(session, target, Vulnerable), "所选目标随后获得 2 层易伤");
            Check.Equal(0L, FormalSliceFixture.PowerStacks(session, other, Vulnerable), "另一个敌人没有拿到（不是全体）");
            Check.Equal(energy - 2, Energy(session, player), "按来源费用 2 付款");
            Check.True(InPile(session, FormalSliceFixture.DiscardPile, card), "打出的牌进入弃牌堆");

            // 易伤随后真的生效：再打一张打击 6 → 9（×1.5）。
            var strike = FormalSliceFixture.MoveToHand(session, Strike);
            var before = Hp(session, target);
            FormalSliceFixture.PlayAt(session, strike, target);
            Check.Equal(9L, before - Hp(session, target), "后续打击被易伤放大 6 → 9");
        });

        Check.Run("ENV-T06 攻击序列族：Bash 的对照形态（目标已有 2 层易伤 ⇒ 伤害确实是 12）[真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var target = FormalSliceFixture.Cultist(session, Calcified);
            var card = FormalSliceFixture.MoveToHand(session, Bash);

            // 受控前置：目标事先带 2 层易伤。这是"顺序判据"的正对照：
            // 它证明引擎**确实**会用已有易伤放大伤害，所以上一个检查里的 8 点只能来自"先伤害后上易伤"。
            FormalSliceFixture.GivePower(session, target, Vulnerable, 2);
            var hp = Hp(session, target);

            FormalSliceFixture.PlayAtCountingDecisions(session, card, target);

            Check.Equal(12L, hp - Hp(session, target), "已有易伤 ⇒ 8×1.5 = 12");
            Check.Equal(4L, FormalSliceFixture.PowerStacks(session, target, Vulnerable), "叠加后 2+2 = 4 层");
        });

        Check.Run("ENV-T06 攻击序列族：Bash 升级形态 10 后 3 层易伤 [真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var target = FormalSliceFixture.Cultist(session, Calcified);
            var card = FormalSliceFixture.MoveToHand(session, Bash);

            MarkUpgraded(session, card); // 受控前置：只置升级标记
            var hp = Hp(session, target);
            var energy = Energy(session, player);

            var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, card, target);

            Check.Equal(1, decisions, "升级形态只提问一次目标");
            Check.Equal(10L, hp - Hp(session, target), "升级先打 10（若易伤先上会是 15）");
            Check.Equal(3L, FormalSliceFixture.PowerStacks(session, target, Vulnerable), "升级易伤 3 层");
            Check.Equal(energy - 2, Energy(session, player), "升级不改变费用（仍 2）");
        });

        Check.Run("ENV-T06 攻击序列族：Thunderclap 普通形态给**两个**敌人各打 4 各上 1 层，且不提问 [真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var front = FormalSliceFixture.Cultist(session, Calcified);
            var back = FormalSliceFixture.Cultist(session, Damp);
            var card = FormalSliceFixture.MoveToHand(session, Thunderclap);

            Check.Equal(1L, CardField(session, card, "Cost"), "来源费用 1");
            var frontHp = Hp(session, front);
            var backHp = Hp(session, back);
            var energy = Energy(session, player);

            var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, card, front);

            Check.Equal(0, decisions, "selectAll：**没有任何**目标提问（挂起次数 0）");
            Check.Equal(SessionStatus.Running, session.Status, "打完后直接回到 Running，不停在目标选择上");
            Check.Equal(4L, frontHp - Hp(session, front), "前排 -4");
            Check.Equal(4L, backHp - Hp(session, back), "后排 -4（全敌人域）");
            Check.Equal(1L, FormalSliceFixture.PowerStacks(session, front, Vulnerable), "前排 +1 层易伤");
            Check.Equal(1L, FormalSliceFixture.PowerStacks(session, back, Vulnerable), "后排 +1 层易伤");
            Check.Equal(energy - 1, Energy(session, player), "按来源费用 1 付款");
            Check.True(InPile(session, FormalSliceFixture.DiscardPile, card), "打出的牌进入弃牌堆");
        });

        Check.Run("ENV-T06 攻击序列族：Thunderclap 先各打 4 后各上 1 层，且后排的易伤对后续真的生效 [真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var front = FormalSliceFixture.Cultist(session, Calcified);
            var back = FormalSliceFixture.Cultist(session, Damp);
            var thunderclap = FormalSliceFixture.MoveToHand(session, Thunderclap);

            var frontHp = Hp(session, front);
            var backHp = Hp(session, back);
            FormalSliceFixture.PlayAtCountingDecisions(session, thunderclap, front);

            // 顺序判据：伤害 4（若易伤在伤害之前上，这里会是 6）。
            Check.Equal(4L, frontHp - Hp(session, front), "前排 -4（不是被自己刚上的易伤放大后的 6）");
            Check.Equal(4L, backHp - Hp(session, back), "后排 -4");

            // 易伤是否真的挂在**后排**（而不是只挂到被选中的那个）：用一张 8 点攻击打后排。
            // 受控前置：正式包卡组里 Bash 只有 1 张，这里只是把它拿进手牌，不改任何数值。
            var bash = FormalSliceFixture.MoveToHand(session, Bash);
            Check.Equal(2L, Energy(session, player), "Thunderclap 费用 1 之后剩 2 能量（受控初态 3）");
            backHp = Hp(session, back);
            FormalSliceFixture.PlayAtCountingDecisions(session, bash, back);

            Check.Equal(12L, backHp - Hp(session, back), "后排的易伤真的生效：8×1.5 = 12");
            Check.Equal(3L, FormalSliceFixture.PowerStacks(session, back, Vulnerable), "后排 1+2 = 3 层");
        });

        Check.Run("ENV-T06 攻击序列族：Thunderclap 升级形态各打 7、各上 1 层 [真实·受控初态]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var front = FormalSliceFixture.Cultist(session, Calcified);
            var back = FormalSliceFixture.Cultist(session, Damp);
            var card = FormalSliceFixture.MoveToHand(session, Thunderclap);

            MarkUpgraded(session, card); // 受控前置：只置升级标记（层数不从显示槽取）
            var frontHp = Hp(session, front);
            var backHp = Hp(session, back);

            var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, card, front);

            Check.Equal(0, decisions, "升级形态同样不提问目标");
            Check.Equal(7L, frontHp - Hp(session, front), "前排 -7");
            Check.Equal(7L, backHp - Hp(session, back), "后排 -7");
            Check.Equal(1L, FormalSliceFixture.PowerStacks(session, front, Vulnerable), "升级层数仍 1（来源只升伤害）");
            Check.Equal(1L, FormalSliceFixture.PowerStacks(session, back, Vulnerable), "后排同样 1 层");
        });
    }

    /// <summary>受控前置：只把卡实例的 `Upgraded` 标记置真，不写任何数值槽（变体选择见 `CombatSlicePackage.cs:4697`）。</summary>
    private static void MarkUpgraded(Session session, InstanceId card)
    {
        var field = session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId).FieldIndex("Upgraded");
        session.State.RunAtomic(() => session.State.SetField(card, field, Value.Of(true)));
    }

    private static long CardField(Session session, InstanceId card, string field) =>
        FormalSliceFixture.Number(session, card, FormalSliceFixture.CardTypeId, field);

    private static long Hp(Session session, InstanceId monster) =>
        FormalSliceFixture.Number(session, monster, FormalSliceFixture.MonsterTypeId, "Hp");

    private static long Energy(Session session, InstanceId player) =>
        FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Energy");

    private static bool InPile(Session session, string pile, InstanceId card) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, pile)).Contains(card);
}
