using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02 的行为检查共用夹具：**用生产入口 <see cref="FormalSpirePackage"/> 构建**（正式源，不是测试专用夹具），
/// 再提供几条受控操作（把牌挪进手、直接挂能力、读能力层数、结束回合）。
///
/// 抽出来是为了避免两个检查各写一套：夹具一旦分叉，"同一件事"的两个检查就会对不上。
/// </summary>
internal static class FormalSliceFixture
{
    public const string CardTypeId = "base-spire:Card";
    public const string MonsterTypeId = "base-spire:Monster";
    public const string PlayerTypeId = "base-spire:Player";
    public const string PowerTypeId = "base-spire:Power";
    public const string HandPile = "base-spire:Hand";
    public const string DrawPile = "base-spire:DrawPile";
    public const string DiscardPile = "base-spire:DiscardPile";
    public const string ExhaustPile = "base-spire:ExhaustPile";
    public const string PlayCardAction = "base-spire:PlayCard";
    public const string EndTurnAction = "base-spire:EndTurn";

    public static Session NewFormal()
    {
        var rules = FormalSpirePackage.BuildRuleSet(out _);
        var session = new Session(rules, 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }

    /// <summary>
    /// 用作者文本建一个 `run` 场景会话（受控切片路径：`run` 段 → 长期牌组 → `StartNextRoom`）。
    /// 与 <see cref="NewFormal" /> 走**同一套**生产路径（`StartNextRoom` + `CopyCardFields` + 奖励升级），
    /// 区别只在内容：需要"同定义多实例"（正式源每张定义各一张）时用这个入口声明重复牌。
    /// </summary>
    public static Session NewFromAuthorText(string scenarioText)
    {
        var model = SliceAuthorFrontend.Parse(scenarioText, "run");
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: model)
            .Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole],
            computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }

    public static InstanceId Player(Session session) =>
        session.State.AllInstances(session.RuleSet.TypeByQualifiedId(PlayerTypeId).Id).Single();

    public static InstanceId Cultist(Session session, string definitionId = "base-spire:calcified-cultist") =>
        session.State.CollectionMembers(Collection(session, "base-spire:Monsters"))
            .Single(monster => Definition(session, monster, MonsterTypeId) == definitionId);

    public static int Collection(Session session, string name) =>
        session.RuleSet.Collections.Single(item => item.QualifiedId == name).Id;

    public static string Definition(Session session, InstanceId instance, string typeId = CardTypeId) =>
        session.State.GetField(instance, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex("DefinitionId"))
            .AsString();

    /// <summary>把一张牌从它现在所在的牌区挪进手牌。</summary>
    public static InstanceId MoveToHand(Session session, string cardId)
    {
        var hand = Collection(session, HandPile);
        var existing = session.State.CollectionMembers(hand)
            .FirstOrDefault(card => Definition(session, card) == cardId);
        if (existing != default)
        {
            return existing;
        }

        foreach (var pileName in new[] { DrawPile, DiscardPile, ExhaustPile })
        {
            var pile = Collection(session, pileName);
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

    /// <summary>直接给拥有者挂一条能力（与包内 ApplyPower 造出的形状一致），用于受控场景。</summary>
    public static void GivePower(Session session, InstanceId owner, string powerId, long stacks)
    {
        var rules = session.RuleSet;
        var state = session.State;
        var powerType = rules.TypeByQualifiedId(PowerTypeId);
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        state.RunAtomic(() =>
        {
            var instance = state.CreateInstance(powerType.Id, state.ScopeOf(owner));
            state.SetField(instance, powerType.FieldIndex("DefinitionId"), Value.Of(powerId));
            state.SetField(instance, powerType.FieldIndex("Stacks"), Value.Of(stacks));
            state.SetField(instance, powerType.FieldIndex("Ticks"), Value.Of(false));
            state.SetField(instance, powerType.FieldIndex("SkipNextTick"), Value.Of(false));
            state.AddRelation(relation.Id, owner, instance);
        });
    }

    /// <summary>该拥有者身上所有能力的 `定义id:层数`（诊断用；按定义 id 排序）。</summary>
    public static IReadOnlyList<string> PowerIds(Session session, InstanceId owner)
    {
        var powerType = session.RuleSet.TypeByQualifiedId(PowerTypeId);
        var relation = session.RuleSet.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        return
        [
            .. session.State.RelationTargets(relation.Id, owner)
                .Select(power => $"{session.State.GetField(power, powerType.FieldIndex("DefinitionId")).AsString()}:"
                    + $"{session.State.GetField(power, powerType.FieldIndex("Stacks")).AsInt()}")
                .Order(StringComparer.Ordinal),
        ];
    }

    public static long PowerStacks(Session session, InstanceId owner, string powerId)    {
        var powerType = session.RuleSet.TypeByQualifiedId(PowerTypeId);
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

    public static long Number(Session session, InstanceId instance, string typeId, string field) =>
        session.State.GetField(instance, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsInt();

    /// <summary>受控地把某个整数字段设成指定值（例如把敌人血量压低，用来构造"目标会被打死"的场景）。</summary>
    public static void SetNumber(Session session, InstanceId instance, string typeId, string field, long value)
    {
        var fieldIndex = session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field);
        session.State.RunAtomic(() => session.State.SetField(instance, fieldIndex, Value.Of(value)));
    }

    /// <summary>打出这张牌并把随后的决策都答给 <paramref name="target"/>。</summary>
    public static void PlayAt(Session session, InstanceId card, InstanceId target) =>
        PlayAtCountingDecisions(session, card, target);

    /// <summary>
    /// 同上，但返回**回答了多少个决策**：用来断言"不该重复提问"（例如目标已在固定步骤里选定，
    /// 尾部条件段就不该再问一次）。
    /// </summary>
    public static int PlayAtCountingDecisions(Session session, InstanceId card, InstanceId target)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == PlayCardAction);
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
        var decisions = 0;
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            var pending = session.PendingDecision!;
            var chosen = pending.Candidates.Contains(target) ? target : pending.Candidates[0];
            session.Answer(new DecisionAnswer(pending.RequestId, [chosen]));
            decisions++;
        }

        return decisions;
    }

    public static void EndTurn(Session session)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == EndTurnAction);
        session.Submit(action.Definition, SliceManifest.Player, []);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            var pending = session.PendingDecision!;
            session.Answer(new DecisionAnswer(pending.RequestId, [pending.Candidates[0]]));
        }
    }
}
