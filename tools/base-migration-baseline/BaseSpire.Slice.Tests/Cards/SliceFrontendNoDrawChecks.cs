using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`NoDraw`（原版 `NoDrawPower.ShouldDraw` 返回 false ⇒ 拥有者抽不了牌）。
///
/// 这一条是"先补通用机制、再迁内容"的又一个实例：能力前端原先把 `no-draw` 判为运行时不支持，
/// 于是 BattleTrance 只能记为未支持，而按宽表 `KnownIds` 链接会得到一个**存在但没加载**的能力 id。
/// 现在能力进了运行时白名单，卡也随之迁移——因此必须有**判别性行为检查**，
/// 否则就是项目明令禁止的"存在但不生效的空能力"。
///
/// 检查用**生产入口** <see cref="FormalSpirePackage"/>（正式源构建），不是测试专用夹具。
/// </summary>
internal static class SliceFrontendNoDrawChecks
{
    private const string CardTypeId = "base-spire:Card";
    private const string PlayerTypeId = "base-spire:Player";
    private const string NoDrawId = "base-spire:no-draw";
    private const string BattleTranceId = "base-spire:battle-trance";
    private const string PommelStrikeId = "base-spire:pommel-strike";
    private const string CultistId = "base-spire:calcified-cultist";

    public static void Run()
    {
        Check.Run("ENV-02 NoDraw：BattleTrance 先抽 3 张再施加阻止；随后抽 1 张的牌被挡住（带对照）[真实]", () =>
        {
            // 对照组：不打出 BattleTrance，PommelStrike 正常抽 1 张。
            var control = NewFormal();
            var controlHand = SliceFixtures.Collection(control, "base-spire:Hand");
            MoveToHand(control, PommelStrikeId);
            var controlBefore = control.State.CollectionMembers(controlHand).Count;
            SliceFixtures.PlayAt(control, MoveToHand(control, PommelStrikeId), Cultist(control));
            var controlAfter = control.State.CollectionMembers(controlHand).Count;
            Check.Equal(controlBefore, controlAfter,
                $"对照：PommelStrike 打出（-1）并抽 1 张（+1）⇒ 手牌数不变（{controlBefore} → {controlAfter}）");

            // 实验组：先 BattleTrance（抽 3 + 施加 NoDraw），再 PommelStrike —— 抽牌必须被挡住。
            var session = NewFormal();
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var player = SliceFixtures.Player(session);
            MoveToHand(session, BattleTranceId);
            MoveToHand(session, PommelStrikeId);
            var before = session.State.CollectionMembers(hand).Count;

            var battleTrance = MoveToHand(session, BattleTranceId);
            SliceFixtures.PlayAt(session, battleTrance, player);
            var afterTrance = session.State.CollectionMembers(hand).Count;
            Check.Equal(before + 2, afterTrance,
                $"BattleTrance 自施（-1）后先抽 3 张（+3）⇒ 手牌 {before} → {afterTrance}（+2）");
            Check.Equal(1L, PowerStacks(session, player, NoDrawId), "打出后玩家身上有 1 层 NoDraw");

            var beforeStrike = afterTrance;
            SliceFixtures.PlayAt(session, MoveToHand(session, PommelStrikeId), Cultist(session));
            var afterStrike = session.State.CollectionMembers(hand).Count;
            Check.Equal(beforeStrike - 1, afterStrike,
                $"NoDraw 生效时 PommelStrike 打出（-1）**抽不到牌**（+0）⇒ 手牌 {beforeStrike} → {afterStrike}；"
                + $"对照组的 +1 说明差别来自阻止而不是牌本身");
        });

        Check.Run("ENV-02 NoDraw：在该侧回合结束时移除，下一回合照常抽满 [真实]", () =>
        {
            var session = NewFormal();
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var player = SliceFixtures.Player(session);
            SliceFixtures.PlayAt(session, MoveToHand(session, BattleTranceId), player);
            Check.Equal(1L, PowerStacks(session, player, NoDrawId), "打出后 NoDraw 在场");

            // 结束回合：NoDraw 的 `trigger = afterPlayerTurnEnd` + `decrementOnTrigger` 应当把它移除。
            EndTurn(session);
            Check.Equal(0L, PowerStacks(session, player, NoDrawId), "玩家回合结束后 NoDraw 被移除");

            // 下一回合照常抽满：**用抽牌堆的减少量判定**，不要用手牌数量——
            // 回合结束会先弃掉整手再抽满，手牌数在"抽了"和"没抽"两种情况下都可能相同（这个坑第一次就踩了）。
            var drawPile = SliceFixtures.Collection(session, "base-spire:DrawPile");
            var drawBefore = session.State.CollectionMembers(drawPile).Count;
            EndTurn(session);
            var drawAfter = session.State.CollectionMembers(drawPile).Count;
            Check.Equal(drawBefore - SliceManifest.DrawPerTurn, drawAfter,
                $"下一回合抽满 {SliceManifest.DrawPerTurn} 张（抽牌堆 {drawBefore} → {drawAfter}）："
                + "NoDraw 不得跨回合残留");
        });
    }

    private static Session NewFormal()
    {
        var rules = FormalSpirePackage.BuildRuleSet(out _);
        var session = new Session(rules, 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }

    /// <summary>把一张牌从它现在所在的牌区挪进手牌（正式源卡组每张各 1 份）。</summary>
    private static InstanceId MoveToHand(Session session, string cardId)
    {
        var hand = SliceFixtures.Collection(session, "base-spire:Hand");
        var existing = session.State.CollectionMembers(hand)
            .FirstOrDefault(card => Definition(session, card) == cardId);
        if (existing != default)
        {
            return existing;
        }

        foreach (var pileName in new[] { "base-spire:DrawPile", "base-spire:DiscardPile", "base-spire:ExhaustPile" })
        {
            var pile = SliceFixtures.Collection(session, pileName);
            var card = session.State.CollectionMembers(pile)
                .FirstOrDefault(candidate => Definition(session, candidate) == cardId);
            if (card == default)
            {
                continue;
            }

            session.State.RunAtomic(() =>
            {
                session.State.RemoveFromCollection(pile, card);
                session.State.AddToCollection(hand, card);
            });
            return card;
        }

        throw new CheckFailedException($"正式源卡组里找不到 '{cardId}'");
    }

    private static InstanceId Cultist(Session session) =>
        session.State.CollectionMembers(SliceFixtures.Collection(session, "base-spire:Monsters"))
            .Single(monster => Definition(session, monster, "base-spire:Monster") == CultistId);

    private static void EndTurn(Session session)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
        session.Submit(action.Definition, SliceManifest.Player, []);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            var pending = session.PendingDecision!;
            session.Answer(new DecisionAnswer(pending.RequestId, [pending.Candidates[0]]));
        }
    }

    private static long PowerStacks(Session session, InstanceId owner, string powerId)
    {
        var powerType = session.RuleSet.TypeByQualifiedId("base-spire:Power");
        var relation = session.RuleSet.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        foreach (var power in session.State.RelationTargets(relation.Id, owner))
        {
            if (session.State.GetField(power, powerType.FieldIndex("DefinitionId")).AsString() == powerId)
            {
                return session.State.GetField(power, powerType.FieldIndex("Stacks")).AsInt();
            }
        }

        return 0L;
    }

    private static string Definition(Session session, InstanceId instance, string typeId = CardTypeId) =>
        session.State.GetField(instance, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex("DefinitionId"))
            .AsString();
}
