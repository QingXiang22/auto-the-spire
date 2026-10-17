using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第一小批）：**状态牌的两个关键词** `unplayable` / `ethereal` + 第一张状态牌 `Dazed`。
///
/// 来源对照：`Cards/Content/status-cards.spire:3-23`（`type = status`、`cost = 0`、
/// `keywords = { ethereal unplayable }`、`play = {}`）——注意它是**双形态同值**且 `upgradeable = false`。
///
/// 判据：语法 / 编译 / 加载 / 行为四态；`unplayable` 的牌不能进合法提交组合（强制提交被 guard 拒绝）；
/// `ethereal` 的牌在端末仍在手牌时进**消耗区**（不是弃牌堆）；普通牌照常进弃牌堆（不回归）。
/// </summary>
internal static class SliceFrontendStatusCardChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  dazed x2 }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 200  tough-hp = 201  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 200  tough-hp = 201  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 9  steps = [target, damage] }
        }
        card dazed {
          name = Dazed  kind = Status  cost = 0
          normal   { amount = 0  unplayable = true  ethereal = true  steps = [] }
          upgraded { amount = 0  unplayable = true  ethereal = true  steps = [] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 状态牌：`kind = Status` + `unplayable`/`ethereal` 被解析，非法布尔值被拒绝", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "status");
            var dazed = model.Cards.Single(card => card.Id == "base-spire:dazed");
            Check.Equal("Status", dazed.Kind, "kind = Status（与来源 type = status 对应）");
            Check.True(dazed.NormalUnplayable && dazed.UpgradedUnplayable, "不可打出");
            Check.True(dazed.NormalEthereal && dazed.UpgradedEthereal, "端末消耗");
            Check.Equal(0, dazed.NormalSteps.Count, "显式空效果表");

            try
            {
                SliceAuthorFrontend.Parse(ScenarioText.Replace("unplayable = true", "unplayable = yes"), "status");
                Check.True(false, "非布尔值必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("invalid_value", failure.Code, "错误码");
                Check.True(failure.Path.Contains("unplayable", StringComparison.Ordinal), $"路径：{failure.Path}");
            }
        });

        Check.Run("B-06a 状态牌：`unplayable` 的牌不进合法提交组合，强制提交被 guard 拒绝 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "status");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var dazed = SliceFixtures.PutInHand(session, "base-spire:dazed", upgraded: false);
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);

            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            var legalRefs = action.Bindings
                .Where(binding => binding[action.Domains.Keys.Single()].Kind == ValueKind.Ref)
                .Select(binding => binding[action.Domains.Keys.Single()].AsRef())
                .ToList();

            Check.False(legalRefs.Contains(dazed), "unplayable 的牌不在合法组合里");
            Check.True(legalRefs.Contains(strike), "可打出的牌仍在合法组合里");

            var statusBefore = session.Status;
            try
            {
                session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(dazed)]);
                Check.True(false, "强制提交 unplayable 的牌必须被拒绝");
            }
            catch (EngineFaultException failure)
            {
                Check.True(failure.Message.Contains("guard", StringComparison.OrdinalIgnoreCase),
                    $"拒绝原因应指向 guard：{failure.Message}");
            }

            Check.Equal(statusBefore, session.Status, "被拒绝的提交不改变会话状态");
            _ = hand;
            _ = cardType;
        });

        Check.Run("B-06a 状态牌：`ethereal` 端末进消耗区，普通牌照常进弃牌堆 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "status");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var exhaust = SliceFixtures.Collection(session, "base-spire:ExhaustPile");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");

            var dazed = SliceFixtures.PutInHand(session, "base-spire:dazed", upgraded: false);
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            Check.True(session.State.IsMember(hand, dazed) && session.State.IsMember(hand, strike), "两张牌都在手里");

            var endTurn = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
            session.Submit(endTurn.Definition, SliceManifest.Player, []);
            while (session.Status == SessionStatus.AwaitingDecision)
            {
                session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId,
                    [session.PendingDecision!.Candidates[0]]));
            }

            Check.True(session.State.IsMember(exhaust, dazed), "ethereal 的牌进了消耗区");
            Check.False(session.State.IsMember(discard, dazed), "它**没有**进弃牌堆");
            Check.True(session.State.IsMember(discard, strike), "普通牌照常进弃牌堆");
            Check.False(session.State.IsMember(exhaust, strike), "普通牌不进消耗区");
        });

        Check.Run("B-06a 状态牌：冻结作者文本声明了 Dazed，且它不在起始卡组里（不改变受控场景）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var dazed = frozen.Cards.Single(card => card.Id == "base-spire:dazed");
            Check.Equal("Status", dazed.Kind, "冻结声明：Status");
            Check.True(dazed.NormalUnplayable && dazed.NormalEthereal, "冻结声明：不可打出 + 端末消耗");
            Check.Equal(3, dazed.SourceLine, "来源行（status-cards.spire:3）");
            Check.False(SliceAuthorFrontend.ToStarterDeck(frozen).Contains("base-spire:dazed", StringComparer.Ordinal),
                "起始卡组里没有它（受控场景不变）");
        });
    }

    private static Session Build(SliceAuthorModel model)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: model)
            .Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }
}
