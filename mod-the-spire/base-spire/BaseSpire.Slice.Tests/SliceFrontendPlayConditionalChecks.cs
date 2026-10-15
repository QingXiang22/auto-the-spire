using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第十四小批）：**条件效果**（`if { condition then else }`）+ `Spite`。
///
/// 来源（`ContentSource/ironclad-cards.spire:3604`）：
/// <code>
/// play = { if = { condition = { playerLostHpThisTurn = { } }
///                 then = { repeat { count = 2  effect = { attack { damage = 5 } } } }
///                 else = { attack { damage = 5 } } } }
/// </code>
/// 条件与 `steps` **二选一**（来源里 `if` 就整个 play）；条件名必须是切片真的能判定的谓词，
/// 别的谓词在解析期被拒绝（不允许"声明了但判不出来"）。
///
/// 谓词的置位点：**真的掉了 HP**（扣掉格挡吸收之后 > 0）的伤害、以及 HP 损失（`lose_hp`）；
/// 在**玩家回合开始**清零。因此本文件的运行检查用一张 `lose_hp:1` 的测试牌在**同一回合内**置位。
/// </summary>
internal static class SliceFrontendPlayConditionalChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x10  spite x2  test-selfharm }
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
        card test-selfharm {
          name = TestSelfHarm  kind = Skill  cost = 0
          normal   { amount = 1  steps = [lose_hp:1] }
          upgraded { amount = 1  steps = [lose_hp:1] }
        }
        card spite {
          name = Spite  kind = Attack  cost = 0
          source = base-spire/ContentSource/ironclad-cards.spire
          line = 3604
          normal {
            amount = 5
            if {
              condition = player-lost-hp-this-turn
              then = [target, damage x2]
              else = [target, damage]
            }
          }
          upgraded {
            amount = 6
            if {
              condition = player-lost-hp-this-turn
              then = [target, damage x2]
              else = [target, damage]
            }
          }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 条件效果：解析 `if { condition then else }`；与 steps 二选一、未知谓词被拒", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "play-conditional");
            var card = model.Cards.Single(entry => entry.Id == "base-spire:spite");
            Check.Equal(3604, card.SourceLine, "来源行");
            Check.True(card.NormalConditional is not null, "普通形态是条件效果");
            Check.Equal(AuthorCondition.PlayerLostHpThisTurn, card.NormalConditional!.Condition, "条件");
            Check.Equal(2, card.NormalConditional.Then.Single(step => step.Kind == AuthorSteps.Damage).Count,
                "then 里伤害是 x2");
            Check.Equal(1, card.NormalConditional.Else.Single(step => step.Kind == AuthorSteps.Damage).Count,
                "else 里伤害是 1 段");
            Check.Equal(0, card.NormalSteps.Count, "条件效果时不再有顶层 steps");
            Check.Equal(5, card.NormalAmount, "普通伤害 5");
            Check.Equal(6, card.UpgradedAmount, "升级伤害 6");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("condition = player-lost-hp-this-turn", "condition = moon-is-full"),
                             "invalid_value"),
                         // 注意：用**同一行内追加**做变异，不依赖换行符（原始字符串字面量里的换行是 CRLF，
                         // 用 "\n" 匹配整行会静默失配——批 9 已经踩过一次）。
                         // 变异不依赖缩进/换行：原始字符串字面量会**剥掉公共缩进**，
                         // 带空格的模式会静默失配（这也是批 9 那类"变异没生效"的坑）。
                         (ScenarioText.Replace("amount = 5",
                             "amount = 5  steps = [target, damage]"), "unsupported_combination"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "play-conditional");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                }
            }
        });

        Check.Run("B-06a 条件效果：本回合**没**失去生命 → else 分支（5 点）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "play-conditional");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var card = SliceFixtures.PutInHand(session, "base-spire:spite", upgraded: false);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, card, target);
            Check.Equal(5L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "else 分支：5 点");
        });

        Check.Run("B-06a 条件效果：本回合**失去过**生命 → then 分支（两段 5 = 10）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "play-conditional");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            // 同一回合内先自损 1 点 HP（置位谓词），再打出 Spite。
            var selfHarm = SliceFixtures.PutInHand(session, "base-spire:test-selfharm", upgraded: false);
            var hpBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            SliceFixtures.PlayAt(session, selfHarm, player);
            Check.Equal(hpBefore - 1, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "自损 1 点");

            var spite = SliceFixtures.PutInHand(session, "base-spire:spite", upgraded: false);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, spite, target);
            Check.Equal(10L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "then 分支：两段 5 = 10");
        });

        Check.Run("B-06a 条件效果：谓词在**新回合开始**清零（又回到 5 点）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "play-conditional");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            var selfHarm = SliceFixtures.PutInHand(session, "base-spire:test-selfharm", upgraded: false);
            SliceFixtures.PlayAt(session, selfHarm, player);
            var first = SliceFixtures.PutInHand(session, "base-spire:spite", upgraded: false);
            var firstBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, first, target);
            Check.Equal(10L, firstBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "本回合失去过生命 → 10");

            EndTurn(session);
            var second = SliceFixtures.PutInHand(session, "base-spire:spite", upgraded: false);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, second, target);
            Check.Equal(5L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "新回合清零 → 5");
        });

        Check.Run("B-06a 条件效果：冻结作者文本声明了 Spite 的两分支", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var card = frozen.Cards.Single(entry => entry.Id == "base-spire:spite");
            Check.Equal(3604, card.SourceLine, "来源行");
            Check.Equal(AuthorCondition.PlayerLostHpThisTurn, card.UpgradedConditional!.Condition,
                "升级同样是该谓词");
            Check.Equal(2, card.UpgradedConditional.Then.Single(step => step.Kind == AuthorSteps.Damage).Count,
                "升级 then 仍是两段");
            Check.Equal(1, card.UpgradedConditional.Else.Single(step => step.Kind == AuthorSteps.Damage).Count,
                "升级 else 仍是一段");
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
