using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第四批：X 费（`cost = X`）与**捕获时点**。
///
/// 语义：X 费牌不读声明费用；打出时捕获**当时的剩余能量**写进这张牌的 Amount（效果读的就是该字段），
/// 随后能量清零。因此同一张牌在能量不同时打出，效果数值必须不同 —— 这正是"捕获时点"的独立检查。
/// </summary>
internal static class SliceFrontendXCostChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck {
          strike-ironclad x5
          test-overload
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
          name = StrikeIronclad  kind = Attack  cost = 1
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 9  steps = [target, damage] }
        }
        card test-overload {
          name = TestOverload  kind = Attack  cost = X
          normal   { amount = 0  steps = [target, damage] }
          upgraded { amount = 0  steps = [target, damage] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c X cost: `cost = X` 被解析为 X 费，固定 amount 与它冲突时拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "xcost");
            var overload = model.Cards.Single(card => card.Id == "base-spire:test-overload");
            Check.True(overload.IsXCost, "cost = X 必须被识别为 X 费");
            Check.Equal(0, overload.Cost, "X 费牌的声明费用不是固定值");
            Check.False(model.Cards.Single(card => card.Id == SliceManifest.StrikeId).IsXCost, "Strike 不是 X 费");

            foreach (var (text, code) in new[]
                     {
                         // X 费 + 固定 amount 冲突
                         (ScenarioText.Replace("normal   { amount = 0", "normal   { amount = 6"),
                             "unsupported_combination"),
                         // 非法费用文本
                         (ScenarioText.Replace("cost = X", "cost = half"), "invalid_value"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "xcost");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Contains("test-overload", StringComparison.Ordinal),
                        $"路径应指向该卡：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c X cost: 捕获时点是打出那一刻的剩余能量（3 点 → 3，2 点 → 2，0 点 → 0）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "xcost");

            foreach (var (spentBefore, expected) in new[] { (0, 3L), (1, 2L), (3, 0L) })
            {
                var session = Build(model);
                var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                var before = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");

                // 先花掉 spentBefore 点能量（每张 Strike 1 点）。
                for (var index = 0; index < spentBefore; index++)
                {
                    var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
                    SliceFixtures.PlayAt(session, strike, front);
                }

                var energyBefore = SliceFixtures.Number(session, SliceFixtures.Player(session),
                    SliceFixtures.PlayerTypeId, "Energy");
                Check.Equal(3L - spentBefore, energyBefore, $"前置花费后剩余能量（{spentBefore}）");

                var overload = SliceFixtures.PutInHand(session, "base-spire:test-overload", upgraded: false);
                SliceFixtures.PlayAt(session, overload, front);

                var hpAfter = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
                var strikesDamage = spentBefore * 6L;
                Check.Equal(before - strikesDamage - expected, hpAfter,
                    $"能量 {energyBefore} 时 X 费牌造成 {expected} 点（捕获时点＝打出那一刻）");
                Check.Equal(0L, SliceFixtures.Number(session, SliceFixtures.Player(session),
                    SliceFixtures.PlayerTypeId, "Energy"), "X 费牌把能量清零");
                Check.Equal(expected, SliceFixtures.Number(session, overload, SliceFixtures.CardTypeId, "Amount"),
                    "捕获值写进这张牌的 Amount（效果读的就是该字段）");
            }
        });

        Check.Run("B-05c X cost: 0 能量时 X 费牌仍合法，非 X 费牌仍按声明费用扣费 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "xcost");
            var session = Build(model);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            // 花光 3 点能量。
            for (var index = 0; index < 3; index++)
            {
                var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
                SliceFixtures.PlayAt(session, strike, front);
            }

            Check.Equal(0L, SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Energy"), "能量已花光");

            // 合法性体现在 `LegalAction.Bindings`（guard 成立的参数元组），而不是候选域本身：
            // 候选域是整只手牌，guard 才决定哪些组合真的可提交。
            var overload = SliceFixtures.PutInHand(session, "base-spire:test-overload", upgraded: false);
            var extraStrike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            // 注意：`Bindings` 里是**整帧槽位**的克隆，参数值在它自己的槽位上，不是长度为 1 的元组。
            var cardSlot = action.Domains.Keys.Single();
            var legalRefs = action.Bindings
                .Where(binding => binding[cardSlot].Kind == ValueKind.Ref)
                .Select(binding => binding[cardSlot].AsRef())
                .ToList();

            Check.True(legalRefs.Contains(overload), "0 能量时 X 费牌仍可提交（X = 0 合法）");
            Check.False(legalRefs.Contains(extraStrike), "0 能量时 Strike 不可提交（费用 1）");
            Check.True(action.Domains.Values.All(domain => domain.Count > 0),
                "候选域仍是整只手牌（合法性由 guard 表达）");

            // 直接提交非法的出牌必须被拒绝，且不改变会话状态。
            var statusBefore = session.Status;
            try
            {
                session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(extraStrike)]);
                Check.True(false, "0 能量打出 Strike 必须被拒绝");
            }
            catch (EngineFaultException failure)
            {
                Check.True(failure.Message.Contains("guard", StringComparison.OrdinalIgnoreCase),
                    $"拒绝原因应指向 guard：{failure.Message}");
            }

            Check.Equal(statusBefore, session.Status, "被拒绝的提交不改变会话状态");
        });

        Check.Run("B-05c X cost: 冻结作者文本没有 X 费牌（默认路径不受影响）", () =>
        {
            var model = SliceAuthorFrontend.Load();
            Check.False(model.Cards.Any(card => card.IsXCost), "冻结文本里没有 X 费牌");

            // 程序化路径与作者路径（冻结文本）仍给出同一份受控开局。
            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build().Build());
            var programmatic = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
            programmatic.Start();
            Check.Equal(3L, SliceFixtures.Number(programmatic, SliceFixtures.Player(programmatic),
                SliceFixtures.PlayerTypeId, "Energy"), "程序化路径开局能量仍是 3");
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
