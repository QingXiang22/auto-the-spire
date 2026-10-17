using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第十批：**保留时点（retain）**——`retain = true` 的牌在回合结束时留在手牌而不是进弃牌堆。
///
/// 判据：解析→IR→加载→行为；**逐变体独立**（普通留、升级不留，反之亦然）；
/// 保留的牌仍然触发 `turn_end_in_hand`（"回合结束时仍在手牌"这个时点没有变）；
/// 非保留牌照常弃掉；冻结作者文本不受影响。
/// </summary>
internal static class SliceFrontendRetainChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck {
          strike-ironclad x10
          test-guard
          test-beacon
        }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x" }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 44  tough-hp = 45  first-move-ritual = 4  strike-damage = 8
          strike-damage-deadly = 10  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 51  tough-hp = 52  first-move-ritual = 5  strike-damage = 1
          strike-damage-deadly = 3  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 9  steps = [target, damage] }
        }
        card test-guard {
          name = TestGuard  kind = Skill  cost = 0
          normal   { amount = 3  retain = true   steps = [block] }
          upgraded { amount = 3  retain = false  steps = [block] }
        }
        card test-beacon {
          name = TestBeacon  kind = Skill  cost = 0
          normal   { amount = 1  retain = true  steps = [block]  hook turn_end_in_hand { steps = [damage_all] } }
          upgraded { amount = 1  retain = true  steps = [block]  hook turn_end_in_hand { steps = [damage_all] } }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c retain：`retain` 逐变体解析，非法值被拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "retain");
            var guard = model.Cards.Single(card => card.Id == "base-spire:test-guard");
            Check.True(guard.NormalRetain, "普通形态保留");
            Check.False(guard.UpgradedRetain, "升级形态不保留（逐变体独立）");
            Check.True(model.Cards.Single(card => card.Id == "base-spire:test-beacon").NormalRetain, "beacon 保留");
            Check.False(model.Cards.Single(card => card.Id == SliceManifest.StrikeId).NormalRetain,
                "未声明 retain 的牌默认不保留");

            try
            {
                SliceAuthorFrontend.Parse(ScenarioText.Replace("retain = true", "retain = yes"), "retain");
                Check.True(false, "非法 retain 值必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("invalid_value", failure.Code, "错误码");
                Check.True(failure.Path.Contains("retain", StringComparison.Ordinal), $"路径：{failure.Path}");
            }
        });

        Check.Run("B-05c retain：普通形态留在手牌、升级形态照常弃掉 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "retain");
            foreach (var (upgraded, shouldStay, values) in new[]
                     {
                         (false, true, ((int, int)?)null),
                         (true, false, (3, 0)),
                     })
            {
                var session = Build(model);
                var label = upgraded ? "升级" : "普通";
                var card = SliceFixtures.PutInHand(session, "base-spire:test-guard", upgraded, values);
                EndTurn(session);
                // 卡组足够大（10 张打击 + 两张测试牌），一次端末不会触发重洗，
                // 因此"还在不在手牌 / 进没进弃牌堆"是可判别的（重洗会把弃牌搬回抽牌堆再抽回手牌）。
                var hand = SliceFixtures.Collection(session, "base-spire:Hand");
                var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
                Check.Equal(shouldStay, session.State.IsMember(hand, card),
                    $"{label}形态：retain={shouldStay} → {(shouldStay ? "留在手牌" : "离开手牌")}");
                Check.Equal(!shouldStay, session.State.IsMember(discard, card),
                    $"{label}形态：{(shouldStay ? "保留牌不进弃牌堆" : "非保留牌进弃牌堆")}");
            }
        });

        Check.Run("B-05c retain：保留的牌仍然触发 `turn_end_in_hand`（时点没变）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "retain");
            var session = Build(model);
            var beacon = SliceFixtures.PutInHand(session, "base-spire:test-beacon", upgraded: false);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var frontBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            var backBefore = SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");

            EndTurn(session);

            Check.Equal(frontBefore - 1, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "回合结束 hook 触发：前排各受 1 点");
            Check.Equal(backBefore - 1, SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                "后排同样受到 1 点");
            Check.True(session.State.IsMember(SliceFixtures.Collection(session, "base-spire:Hand"), beacon),
                "保留的牌没有被 hook 或弃牌流程移走");

            // 连续两个回合：保留牌每回合都触发一次（不是只在第一回合）。
            EndTurn(session);
            Check.Equal(frontBefore - 2, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "第二个回合又触发一次（每次端末都算）");
        });

        Check.Run("B-05c retain：冻结作者文本没有 retain（默认路径不受影响）", () =>
        {
            var model = SliceAuthorFrontend.Load();
            Check.False(model.Cards.Any(card => card.NormalRetain || card.UpgradedRetain),
                "冻结文本里没有 retain");

            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build().Build());
            var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
            session.Start();
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
            var before = session.State.CollectionMembers(hand).ToList();
            Check.True(before.Count > 0, "开局有手牌");

            EndTurn(session);

            // 注意：端末之后新回合会**重抽**一手牌，所以不能断言"手牌为空"。
            // 可判别的是：端末前那几张实例全部离开手牌并进了弃牌堆（卡组 10 张、本回合不触发重洗）。
            Check.True(before.All(card => !session.State.IsMember(hand, card)),
                "程序化路径仍然把端末前的手牌全部弃掉（行为逐字不变）");
            Check.True(before.All(card => session.State.IsMember(discard, card)),
                "这些牌确实进了弃牌堆");
        });
    }

    private static void EndTurn(Session session)
    {
        var endTurn = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
        session.Submit(endTurn.Definition, SliceManifest.Player, []);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId,
                [session.PendingDecision!.Candidates[0]]));
        }
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
