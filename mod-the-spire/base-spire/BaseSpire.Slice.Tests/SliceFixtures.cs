using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>B-05b 检查用的小夹具：受控场景里移动卡牌、读字段、出牌（都标注为测试场景，不改规则）。</summary>
internal static class SliceFixtures
{
    /// <summary>
    /// B-06a 起、冻结切片之外**已迁入作者路径**的内容（来源逐条对照，见各批报告）。
    /// 检查里凡是要断言"作者文本总共有多少张卡"，都写成 `SliceManifest.Cards.Count + SliceFixtures.AuthoredBeyondSlice.Count`，
    /// 并逐个核对它们真的存在——这样每加一批内容只改这一处，也不会让某个数字悄悄过期。
    /// </summary>
    public static readonly IReadOnlyList<string> AuthoredBeyondSlice =
    [
        "base-spire:dazed",          // 状态牌（status-cards.spire:3）
        "base-spire:beckon",         // 状态牌（status-cards.spire:25）
        "base-spire:iron-wave",      // ironclad-cards.spire:2103
        "base-spire:twin-strike",    // ironclad-cards.spire:2438
        "base-spire:breakthrough",   // ironclad-cards.spire:1549
        "base-spire:pommel-strike",  // ironclad-cards.spire:2265
        "base-spire:shrug-it-off",   // ironclad-cards.spire:2307
        "base-spire:thunderclap",   // ironclad-cards.spire:2535
        "base-spire:rampage",       // ironclad-cards.spire:3852
        "base-spire:entrench",      // ironclad-cards.spire:1752
        "base-spire:inflame",       // ironclad-cards.spire:2191
        "base-spire:feel-no-pain",  // ironclad-cards.spire:1976
        "base-spire:rage",          // ironclad-cards.spire:3565
        "base-spire:fight-me",      // ironclad-cards.spire:3321
        "base-spire:dominate",      // ironclad-cards.spire:3425
        "base-spire:expect-a-fight",// ironclad-cards.spire:1192
        "base-spire:spite",         // ironclad-cards.spire:3604
        "base-spire:ashen-strike",  // ironclad-cards.spire:3057
    ];

    /// <summary>
    /// 冻结清单之外、**只在作者文本里声明**的能力（与 <see cref="AuthoredBeyondSlice"/> 同规矩）。
    /// 注意 `ritual`/`shrink` 早于这条约定（B-05c 引入），一并列在这里，好让"作者文本里应该有几个能力"
    /// 只有**一处**来源，而不是每加一个能力就改一次数字。
    /// </summary>
    public static readonly IReadOnlyList<string> AuthoredPowersBeyondSlice =
    [
        "base-spire:ritual",         // B-05c 批 11（来源 shared-powers）
        "base-spire:shrink",         // B-06a 批 22（来源 ShrinkPower.cs）
        "base-spire:feel-no-pain",   // ironclad-powers.spire:28
        "base-spire:rage-power",     // ironclad-powers.spire:33
        "base-spire:no-energy-gain",// ironclad-powers.spire:19
    ];

    public const string CardTypeId = "base-spire:Card";
    public const string MonsterTypeId = "base-spire:Monster";
    public const string PlayerTypeId = "base-spire:Player";
    public const string PowerTypeId = "base-spire:Power";
    public const string PlayCardActionId = "base-spire:PlayCard";

    public static int Collection(Session session, string name) =>
        session.RuleSet.Collections.Single(c => c.QualifiedId == name).Id;

    /// <summary>把指定定义的第一张牌从 `from` 移到 `to`。</summary>
    public static InstanceId Relocate(Session session, string cardId, int from, int to)
    {
        var card = session.State.CollectionMembers(from).FirstOrDefault(candidate =>
            Definition(session, candidate) == cardId);
        if (card == default)
        {
            throw new CheckFailedException($"no '{cardId}' card in the source area");
        }

        session.State.RunAtomic(() =>
        {
            session.State.RemoveFromCollection(from, card);
            session.State.AddToCollection(to, card);
        });
        return card;
    }

    /// <summary>取一张牌进手牌；<paramref name="upgraded"/> 时把它标成升级并写入升级数值。</summary>
    /// <param name="upgradedValues">
    /// 升级后的 (Amount, PowerStacks)。为 null 时取冻结清单的升级值（切片内的卡）；测试场景里的卡必须显式给出，
    /// 否则会去冻结清单里找一个不存在的定义并报错。
    /// </param>
    public static InstanceId PutInHand(Session session, string cardId, bool upgraded,
        (int Amount, int PowerStacks)? upgradedValues = null, int? upgradedCost = null)
    {
        var hand = Collection(session, "base-spire:Hand");
        var draw = Collection(session, "base-spire:DrawPile");
        var card = session.State.CollectionMembers(hand).FirstOrDefault(candidate =>
            Definition(session, candidate) == cardId);
        if (card == default)
        {
            card = Relocate(session, cardId, draw, hand);
        }

        if (upgraded)
        {
            // 冻结清单之外的卡（B-06a 起的内容）没有清单记录：**必须**显式给出升级数值，不能猜。
            if (upgradedValues is null && !SliceManifest.Cards.Any(entry => entry.ContentId == cardId))
            {
                throw new CheckFailedException(
                    $"'{cardId}' 不在冻结切片清单里：升级形态必须显式传入 upgradedValues");
            }

            var values = upgradedValues
                ?? (Amount: SliceManifest.Card(cardId).UpgradedAmount,
                    PowerStacks: SliceManifest.Card(cardId).UpgradedPowerStacks);
            var type = session.RuleSet.TypeByQualifiedId(CardTypeId);
            session.State.RunAtomic(() =>
            {
                session.State.SetField(card, type.FieldIndex("Upgraded"), Value.Of(true));
                session.State.SetField(card, type.FieldIndex("Amount"), Value.Of((long)values.Amount));
                session.State.SetField(card, type.FieldIndex("PowerStacks"), Value.Of((long)values.PowerStacks));
                // B-06a：升级形态的费用可能与普通不同（来源 Entrench 2→1 / ExpectAFight 2→1）。
                // 合成升级必须**显式**给出费用，否则会悄悄按普通费用跑（这条曾经让检查差 1 点能量）。
                if (upgradedCost is { } cost)
                {
                    session.State.SetField(card, type.FieldIndex("Cost"), Value.Of((long)cost));
                }
            });
        }

        return card;
    }

    /// <summary>打出这张牌，并把随后的所有决策都答给 <paramref name="target"/>。</summary>
    public static void PlayAt(Session session, InstanceId card, InstanceId target)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == PlayCardActionId);
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            var pending = session.PendingDecision!;
            var chosen = pending.Candidates.Contains(target) ? target : pending.Candidates[0];
            session.Answer(new DecisionAnswer(pending.RequestId, [chosen]));
        }
    }

    public static InstanceId Cultist(Session session, string definitionId) =>
        session.State.CollectionMembers(Collection(session, "base-spire:Monsters")).Single(cultist =>
            Text(session, cultist, MonsterTypeId, "DefinitionId") == definitionId);

    public static InstanceId Player(Session session) =>
        session.State.AllInstances(session.RuleSet.TypeByQualifiedId(PlayerTypeId).Id).Single();

    public static long Number(Session session, InstanceId instance, string typeId, string field) =>
        session.State.GetField(instance, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsInt();

    public static string Text(Session session, InstanceId instance, string typeId, string field) =>
        session.State.GetField(instance, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsString();

    private static string Definition(Session session, InstanceId card) =>
        Text(session, card, CardTypeId, "DefinitionId");
}
