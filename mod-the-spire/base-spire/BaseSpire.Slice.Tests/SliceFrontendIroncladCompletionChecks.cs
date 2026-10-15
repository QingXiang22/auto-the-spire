using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-T05 卡牌补齐的行为判别（两族）。
///
/// **家族 1**（类型化动态次数 + 计数来源）：
/// - `Whirlwind`：`repeatForX` —— X 只作用于**次数**，每一击是字面 5（升级 8）。
/// - `TearAsunder`：`repeatForCount` 读**战斗级**掉血事件次数（不是本回合、也不是累计掉血量）。
/// - `PerfectedStrike`：`cardsWithTag` 数全场带 `Strike` 标签的牌（含这张牌自己，它在场区）。
///
/// **家族 2**（`withCount` 词法快照 + `forEachCard` 绑定）：
/// - `FiendFire`：快照先于消耗 —— 命中数取「打出时的手牌张数」，跟随后面被消耗掉多少无关。
/// - `SecondWind`：`filter = nonAttack` 只消耗非攻击牌，每张给格挡。
/// - `PrimalForce`：`transformSelectedCard` **就地**改写成 GiantRock（非攻击牌不动）。
///
/// 一律走生产入口 <see cref="FormalSliceFixture.NewFormal" />：卡与能力都来自正式源，
/// 不是测试自己写的文本。期望值逐条对照同版本 sts2 原版与 `ironclad-cards.spire` 的行号。
/// </summary>
internal static class SliceFrontendIroncladCompletionChecks
{
    private const string Whirlwind = "base-spire:whirlwind";
    private const string TearAsunder = "base-spire:tear-asunder";
    private const string PerfectedStrike = "base-spire:perfected-strike";
    private const string FiendFire = "base-spire:fiend-fire";
    private const string SecondWind = "base-spire:second-wind";
    private const string PrimalForce = "base-spire:primal-force";
    private const string GiantRock = "base-spire:giant-rock";
    private const string Strike = SliceManifest.StrikeId;
    private const string Defend = "base-spire:defend-ironclad";
    private const string Bash = "base-spire:bash";
    private const string Armaments = "base-spire:armaments";
    private const string Bloodletting = "base-spire:bloodletting";
    private const string Calcified = SliceManifest.CalcifiedCultistId;

    public static void Run()
    {
        Check.Run("ENV-T05 家族 1：Whirlwind `repeatForX` —— X 只作用于**次数**，每击是字面 5（升级 8）"
            + "（若 X 泄漏进数值叶，X=3 会变成每击 3 点/总共 9）[真实·受控初态]", () =>
        {
            foreach (var (upgraded, perHit) in new[] { (false, 5L), (true, 8L) })
            {
                // X 的来源是打出前**剩余能量**：先花 1 点打一张 Strike，X 就少 1。
                foreach (var (spent, x) in new[] { (0, 3L), (1, 2L) })
                {
                    var session = FormalSliceFixture.NewFormal();
                    var player = FormalSliceFixture.Player(session);
                    var front = FormalSliceFixture.Cultist(session, Calcified);
                    var back = FormalSliceFixture.Cultist(session, SliceManifest.DampCultistId);
                    // 一次取两张进手：Strike 用来花掉能量，Whirlwind 随后打。
                    // （不能分两次 Bring —— 每次 Bring 都会先把整只手挪回抽牌堆。）
                    var cards = Bring(session, Strike, Whirlwind);
                    var lead = cards[0];
                    var card = cards[1];
                    if (upgraded)
                    {
                        MarkUpgraded(session, card);
                    }

                    for (var index = 0; index < spent; index++)
                    {
                        FormalSliceFixture.PlayAt(session, lead, front);
                    }

                    Check.Equal(3L - spent, Energy(session, player), $"前置花费后剩余能量（X = {x}）");
                    var frontHp = Hp(session, front);
                    var backHp = Hp(session, back);

                    var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, card, front);

                    // 来源 Whirlwind：SelectAll 不需要目标决策；每击打**全体**。
                    Check.Equal(0, decisions, "selectAll：不提问目标");
                    Check.Equal(x * perHit, frontHp - Hp(session, front),
                        $"X={x} ⇒ 命中 {x} 次 × 每击 {perHit} 点");
                    Check.Equal(x * perHit, backHp - Hp(session, back), "每一击打全体（后排同样吃满）");
                    Check.Equal(0L, Energy(session, player), "X 费牌把能量清零");
                }
            }
        });

        Check.Run("ENV-T05 家族 1：TearAsunder 命中数 = 1 + 本场战斗玩家掉血**事件次数**"
            + "（不是本回合、也不是累计掉血量）[真实·受控初态]", () =>
        {
            // 来源 TearAsunder：命中次数 = 1 + DamageReceivedEntry(Receiver == owner && Result.UnblockedDamage > 0)。
            var fresh = FormalSliceFixture.NewFormal();
            var freshTarget = FormalSliceFixture.Cultist(fresh, Calcified);
            var freshTear = Bring(fresh, TearAsunder)[0];
            var freshHp = Hp(fresh, freshTarget);
            FormalSliceFixture.PlayAt(fresh, freshTear, freshTarget);
            Check.Equal(5L, freshHp - Hp(fresh, freshTarget),
                "整场没有掉血事件 ⇒ 只打 `base = 1` 次、每次 5 点");

            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var target = FormalSliceFixture.Cultist(session, Calcified);
            var cards = Bring(session, TearAsunder, Bloodletting);
            var tear = cards[0];
            var bleed = cards[1];

            var playerHp = PlayerHp(session, player);
            FormalSliceFixture.PlayAt(session, bleed, player);
            Check.Equal(3L, playerHp - PlayerHp(session, player), "Bloodletting 自伤 3（记 1 次掉血事件）");

            var hp = Hp(session, target);
            FormalSliceFixture.PlayAt(session, tear, target);
            Check.Equal(10L, hp - Hp(session, target),
                "1 次掉血事件 ⇒ (1 + 1) 次 × 5 点 = 10（掉血量 3 不参与，次数才参与）");
        });

        Check.Run("ENV-T05 家族 1：PerfectedStrike `cardsWithTag` 数全场带 Strike 标签的牌"
            + "（含这张牌自己在场区；少了在场区会少数一张）[真实·受控初态]", () =>
        {
            // 来源 PerfectedStrike：base 6 + perCount × AllCards.Count(Tags.Contains(Strike))；升级 perCount 2 → 3。
            // Explicit formal-source deck keeps this assertion independent of unrelated cards added to the package.
            // Five other tagged definitions and PerfectedStrike itself; all piles remain part of the query.
            const int TaggedCards = 6;
            foreach (var (upgraded, perCount) in new[] { (false, 2L), (true, 3L) })
            {
                var model = FormalSpirePackage.Load().Model;
                var builder = new RuleSetBuilder();
                builder.Add(CombatSlicePackage.Build(authorModel: model with { Deck = [new(PerfectedStrike, 1),
                    new(Strike, 1), new("base-spire:pommel-strike", 1), new("base-spire:twin-strike", 1),
                    new("base-spire:ashen-strike", 1), new("base-spire:setup-strike", 1)] }).Build());
                var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
                session.Start();
                var target = FormalSliceFixture.Cultist(session, Calcified);
                var other = FormalSliceFixture.Cultist(session, SliceManifest.DampCultistId);
                var card = Bring(session, PerfectedStrike)[0];
                if (upgraded)
                {
                    MarkUpgraded(session, card);
                }

                var hp = Hp(session, target);
                var otherHp = Hp(session, other);
                FormalSliceFixture.PlayAt(session, card, target);

                var expected = 6L + (perCount * TaggedCards);
                Check.Equal(expected, hp - Hp(session, target),
                    $"6 + {perCount} × {TaggedCards} 张带标签的牌 = {expected}");
                Check.Equal(otherHp, Hp(session, other), "selectOne：只打被选中的目标");
            }
        });

        Check.Run("ENV-T05 家族 2：FiendFire 的快照**先于**消耗 —— 命中数取打出时的手牌张数"
            + "（升级每次 10）[真实·受控初态]", () =>
        {
            // 来源 FiendFire：`withCount { name = handSize count = pileCount hand }`，
            // 体内先 `forEachCard` 把整手 `moveSelectedCard → exhaust`，再 `repeatForCount` 用**快照**的 handSize。
            // 打出时这张牌已离开手牌（CombatSlicePackage.cs:4304）⇒ 手牌 4 张时命中 3 次。
            foreach (var (upgraded, perHit) in new[] { (false, 7L), (true, 10L) })
            {
                var session = FormalSliceFixture.NewFormal();
                var target = FormalSliceFixture.Cultist(session, Calcified);
                var other = FormalSliceFixture.Cultist(session, SliceManifest.DampCultistId);
                var cards = Bring(session, FiendFire, Strike, Defend, Bash);
                var fiend = cards[0];
                if (upgraded)
                {
                    MarkUpgraded(session, fiend);
                }

                Check.Equal(4, Hand(session).Count, "受控初态：手牌 4 张（含 FiendFire 自己）");
                var hp = Hp(session, target);
                var otherHp = Hp(session, other);

                var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, fiend, target);

                Check.Equal(1, decisions, "selectOne：整张牌只提问一次目标");
                Check.Equal(3L * perHit, hp - Hp(session, target),
                    $"打出后手牌剩 3 张 ⇒ 命中 3 次 × 每次 {perHit} 点（快照先于消耗）");
                Check.Equal(otherHp, Hp(session, other), "只打被选中的目标");
                Check.Equal(0, Hand(session).Count, "整手都被消耗掉");
                Check.Equal(4, Exhausted(session),
                    "消耗堆 = 打出前手牌张数（3 张被效果消耗 + FiendFire 自己有 exhaust 关键词）");
                foreach (var card in cards)
                {
                    Check.True(InPile(session, FormalSliceFixture.ExhaustPile, card),
                        $"'{FormalSliceFixture.Definition(session, card)}' 进了消耗堆（不是弃牌堆）");
                }
            }
        });

        Check.Run("ENV-T05 家族 2：SecondWind `filter = nonAttack` 只消耗非攻击牌，每张给 5 格挡（升级 7）"
            + "（攻击牌留在手里）[真实·受控初态]", () =>
        {
            // 来源 SecondWind：`forEachCard { from = hand filter = nonAttack effect = { moveSelectedCard → exhaust; gainBlock 5/7 } }`。
            foreach (var (upgraded, perCard) in new[] { (false, 5L), (true, 7L) })
            {
                var session = FormalSliceFixture.NewFormal();
                var player = FormalSliceFixture.Player(session);
                var cards = Bring(session, SecondWind, Strike, Defend, Bash, Armaments);
                var second = cards[0];
                if (upgraded)
                {
                    MarkUpgraded(session, second);
                }

                // 受控初态：手牌 = SecondWind + 2 张技能（Defend/Armaments）+ 2 张攻击（Strike/Bash）。
                var block = PlayerBlock(session, player);
                FormalSliceFixture.PlayAt(session, second, player);

                Check.Equal(2L * perCard, PlayerBlock(session, player) - block,
                    $"手里 2 张非攻击牌 ⇒ 2 × {perCard} 格挡");
                Check.True(InPile(session, FormalSliceFixture.ExhaustPile, cards[2]), "Defend 被消耗");
                Check.True(InPile(session, FormalSliceFixture.ExhaustPile, cards[4]), "Armaments 被消耗");
                Check.True(InPile(session, FormalSliceFixture.DiscardPile, second),
                    "SecondWind 自己没有 exhaust 关键词 ⇒ 进弃牌堆");
                Check.Equal(2, Exhausted(session), "消耗堆恰好是那 2 张非攻击牌");
                Check.True(InPile(session, FormalSliceFixture.HandPile, cards[1]), "攻击牌 Strike 留在手里");
                Check.True(InPile(session, FormalSliceFixture.HandPile, cards[3]), "攻击牌 Bash 留在手里");
                Check.Equal(2, Hand(session).Count, "手牌只剩攻击牌");
            }
        });

        Check.Run("ENV-T05 家族 2：PrimalForce 把手牌里的攻击牌替换成 GiantRock"
            + "（非攻击牌不动；升级形态给升级 GiantRock）[真实·受控初态]", () =>
        {
            // 来源 PrimalForce：`forEachCard { from = hand filter = attack effect = transformSelectedCard GiantRock }`；
            // 升级变体多一个 `upgraded = true`（ironclad-cards.spire:747）。
            // GiantRock 的卡级契约（:757-789）：攻击 / 1 费 / 普通 16 点、升级 20 点。
            foreach (var upgraded in new[] { false, true })
            {
                var session = FormalSliceFixture.NewFormal();
                var player = FormalSliceFixture.Player(session);
                var front = FormalSliceFixture.Cultist(session, Calcified);
                var cards = Bring(session, PrimalForce, Strike, Defend, Bash);
                var force = cards[0];
                var strike = cards[1];
                var defend = cards[2];
                var bash = cards[3];
                if (upgraded)
                {
                    MarkUpgraded(session, force);
                }

                FormalSliceFixture.PlayAt(session, force, player);

                var held = Hand(session);
                Check.Equal(3, held.Count, "变形保留手牌数量和原位置");
                var rock = held[0];
                Check.Equal(GiantRock, FormalSliceFixture.Definition(session, rock), "第一个攻击位置是新 GiantRock");
                Check.Equal(defend, held[1], "非攻击牌保持实例和中间位置");
                Check.Equal(GiantRock, FormalSliceFixture.Definition(session, held[2]), "后一个攻击位置是新 GiantRock");
                Check.True(rock != strike && held[2] != bash, "变形创建独立替代实例");
                Check.True(!held.Contains(strike) && !held.Contains(bash), "原牌离开手牌");
                Check.Equal(1L, CardField(session, rock, "Cost"), "GiantRock 费用 1");
                Check.Equal(upgraded ? 20L : 16L, CardField(session, rock, "Amount"), "变体数值");
                Check.Equal(upgraded, CardFlag(session, rock, "Upgraded"), "新牌升级状态");
                var sourceIndex = session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId).FieldIndex("LongTermSource");
                Check.True(session.State.GetField(rock, sourceIndex).IsNone, "替代牌不继承长期来源");

                var hp = Hp(session, front);
                FormalSliceFixture.PlayAt(session, rock, front);
                Check.Equal(upgraded ? 20L : 16L, hp - Hp(session, front),
                    "变形后的实例按 GiantRock 的数值结算");
            }
        });
    }

    // ── 受控初态与读数 ────────────────────────────────────────────────────────

    /// <summary>
    /// 把整只手牌挪回抽牌堆，再按给定顺序取这几张牌进手 —— 让"手牌张数/组成"完全受控，
    /// 否则初始抽牌的随机结果会让计数类断言不可复现。
    /// </summary>
    private static IReadOnlyList<InstanceId> Bring(Session session, params string[] cardIds)
    {
        var hand = FormalSliceFixture.Collection(session, FormalSliceFixture.HandPile);
        var draw = FormalSliceFixture.Collection(session, FormalSliceFixture.DrawPile);
        var held = session.State.CollectionMembers(hand).ToList();
        session.State.RunAtomic(() =>
        {
            foreach (var card in held)
            {
                session.State.RemoveFromCollection(hand, card);
                session.State.AddToCollection(draw, card);
            }
        });

        return [.. cardIds.Select(cardId => FormalSliceFixture.MoveToHand(session, cardId))];
    }

    /// <summary>受控前置：只把卡实例的 `Upgraded` 标记置真，不写任何数值槽（升级数值必须来自 `UpgradedProgram`）。</summary>
    private static void MarkUpgraded(Session session, InstanceId card)
    {
        var field = session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId).FieldIndex("Upgraded");
        session.State.RunAtomic(() => session.State.SetField(card, field, Value.Of(true)));
    }

    private static IReadOnlyList<InstanceId> Hand(Session session) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, FormalSliceFixture.HandPile));

    private static int Exhausted(Session session) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, FormalSliceFixture.ExhaustPile)).Count;

    private static long Hp(Session session, InstanceId monster) =>
        FormalSliceFixture.Number(session, monster, FormalSliceFixture.MonsterTypeId, "Hp");

    private static long PlayerHp(Session session, InstanceId player) =>
        FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");

    private static long PlayerBlock(Session session, InstanceId player) =>
        FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Block");

    private static long Energy(Session session, InstanceId player) =>
        FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Energy");

    private static long CardField(Session session, InstanceId card, string field) =>
        FormalSliceFixture.Number(session, card, FormalSliceFixture.CardTypeId, field);

    private static bool CardFlag(Session session, InstanceId card, string field) =>
        session.State.GetField(card, session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId).FieldIndex(field))
            .AsBool();

    private static bool InPile(Session session, string pile, InstanceId card) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, pile)).Contains(card);
}
