using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第八小批）：**自我施加能力（`apply_power_self`）** + `Inflame`。
///
/// 来源：`Cards/Content/ironclad-cards.spire:2191` —— `type = power`、`cost = 1`、
/// `applyPower { power = Strength  stacks = 2  target = context.owner }`（升级形态 **3**）。
///
/// 为什么只声明这一张：来源里有 22 张"给自己上能力"的牌，但绝大多数能力的**行为**切片还没实现
/// （例如"消耗时抽牌""受到攻击时反击""回合末给格挡"等触发点）。`Inflame` 用的是 `Strength`——
/// 它已经在切片里实现（`add-per-stack:1` 且回合末**不衰减**），所以声明它不会出现"牌在、效果不在"。
/// </summary>
internal static class SliceFrontendInflameChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  inflame }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 300  tough-hp = 301  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 300  tough-hp = 301  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 6  steps = [target, damage] }
        }
        card inflame {
          name = Inflame  kind = Power  cost = 1
          source = base-spire/Cards/Content/ironclad-cards.spire
          line = 2191
          normal   { amount = 0  power = strength  stacks = 2  steps = [apply_power_self] }
          upgraded { amount = 0  power = strength  stacks = 3  steps = [apply_power_self] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 自我施加能力：解析、与全体/单目标共存、缺 power/stacks 被拒", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "inflame");
            var inflame = model.Cards.Single(card => card.Id == "base-spire:inflame");
            Check.Equal(2191, inflame.SourceLine, "来源行");
            Check.Equal("Power", inflame.Kind, "kind = Power（来源 type = power）");
            Check.Sequence([AuthorSteps.ApplyPowerSelf], inflame.NormalSteps.Select(step => step.Kind),
                "只给自己上能力，不需要 target");
            Check.Equal(SliceManifest.StrengthId, inflame.PowerId, "能力 id = Strength");
            Check.Equal(2, inflame.NormalStacks, "普通 2 层");
            Check.Equal(3, inflame.UpgradedStacks, "升级 3 层");

            // 与"给敌人上状态"可以共存（一个给自己、一个给目标，目标域不冲突）。
            var both = SliceAuthorFrontend.Parse(
                ScenarioText.Replace("steps = [apply_power_self]",
                    "steps = [target, damage, apply_power, apply_power_self]"), "inflame");
            Check.Equal(2, both.Cards.Single(card => card.Id == "base-spire:inflame").NormalSteps
                .Count(step => step.Kind is AuthorSteps.ApplyPower or AuthorSteps.ApplyPowerSelf),
                "给自己与给敌人可以同时声明");

            foreach (var (text, code) in new[]
                     {
                         // power 是**卡牌级**事实：两个变体都去掉才算缺（只去掉一个变体仍然是合法的）。
                         (ScenarioText.Replace("power = strength  ", string.Empty), "missing_key"),
                         // stacks 是**逐变体**事实：普通变体缺了就拒绝。
                         (ScenarioText.Replace("power = strength  stacks = 2  steps = [apply_power_self]",
                             "power = strength  steps = [apply_power_self]"), "missing_key"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "inflame");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                }
            }
        });

        Check.Run("B-06a 自我施加能力：Inflame 给玩家 2 层力量，且**跨回合不衰减** [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "inflame");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var inflame = SliceFixtures.PutInHand(session, "base-spire:inflame", upgraded: false);
            SliceFixtures.PlayAt(session, inflame, player);

            Check.Equal(2L, Stacks(session, player, SliceManifest.StrengthId), "获得 2 层力量");
            Check.Equal(0L, Stacks(session, target, SliceManifest.StrengthId), "敌人没有拿到（不是全体）");

            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(8L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "打击 6 + 2 力量 = 8");

            // Strength 的 `owner-turn-end = none`：回合结束后仍然是 2 层（不是临时力量）。
            EndTurn(session);
            Check.Equal(2L, Stacks(session, player, SliceManifest.StrengthId), "跨回合仍是 2 层");
        });

        Check.Run("B-06a 自我施加能力：升级形态给 3 层 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "inflame");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var inflame = SliceFixtures.PutInHand(session, "base-spire:inflame", upgraded: true,
                (Amount: 0, PowerStacks: 3));
            SliceFixtures.PlayAt(session, inflame, player);

            Check.Equal(3L, Stacks(session, player, SliceManifest.StrengthId), "升级给 3 层");
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(9L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "打击 6 + 3 = 9");
        });

        Check.Run("B-06a 自我施加能力：冻结作者文本声明了 Inflame（来源行与两形态层数）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var inflame = frozen.Cards.Single(card => card.Id == "base-spire:inflame");
            Check.Equal(2191, inflame.SourceLine, "来源行");
            Check.Equal(2, inflame.NormalStacks, "普通 2 层");
            Check.Equal(3, inflame.UpgradedStacks, "升级 3 层");
            Check.Equal(AuthorSteps.ApplyPowerSelf, inflame.NormalSteps.Single().Kind, "自我施加");
        });
    }

    private static long Stacks(Session session, InstanceId owner, string powerId)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        foreach (var power in session.State.RelationTargets(relation.Id, owner))
        {
            if (session.State.GetField(power, powerType.FieldIndex("DefinitionId")).AsString() == powerId)
            {
                return session.State.GetField(power, powerType.FieldIndex("Stacks")).AsInt();
            }
        }

        return 0;
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
