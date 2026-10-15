using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第七批：**条件选择**（用既有查询原语 `Sort`/`Where`/`First` 表达），不做多选。
///
/// 两个新步骤：
/// <list type="bullet">
/// <item>`damage_lowest`：自动挑**当前生命最低**的敌人（不提问）；</item>
/// <item>`top_attack_from_discard`：只把弃牌堆里的**攻击牌**作为候选（`Where` 过滤）选一张置顶。</item>
/// </list>
/// </summary>
internal static class SliceFrontendConditionalChecks
{
    /// <summary>注意：两个敌人的声明顺序与"最低生命"故意相反，用来判别"选最低"而不是"选第一个成员"。</summary>
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck {
          strike-ironclad x4
          defend-ironclad
          test-snipe
          test-salvage
        }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x" }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 90  tough-hp = 91  first-move-ritual = 4  strike-damage = 8
          strike-damage-deadly = 10  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 40  tough-hp = 41  first-move-ritual = 5  strike-damage = 1
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
        card defend-ironclad {
          name = DefendIronclad  kind = Skill  cost = 0
          normal   { amount = 5  steps = [block] }
          upgraded { amount = 8  steps = [block] }
        }
        card test-snipe {
          name = TestSnipe  kind = Attack  cost = 0
          normal   { amount = 7  steps = [damage_lowest] }
          upgraded { amount = 9  steps = [damage_lowest] }
        }
        card test-salvage {
          name = TestSalvage  kind = Skill  cost = 0
          normal   { amount = 1  steps = [top_attack_from_discard] }
          upgraded { amount = 1  steps = [top_attack_from_discard] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 条件选择：`damage_lowest` 自动选生命最低的敌人，且与其它目标域互斥（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "conditional");
            var snipe = model.Cards.Single(card => card.Id == "base-spire:test-snipe");
            Check.Sequence([AuthorSteps.DamageLowest], snipe.StepKinds, "damage_lowest 被接受");

            foreach (var replacement in new[]
                     {
                         "steps = [target, damage_lowest]",
                         "steps = [damage_lowest, damage_all]",
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(
                        ScenarioText.Replace("steps = [damage_lowest]", replacement), "conditional");
                    Check.True(false, "目标确定方式不唯一必须被拒绝");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal("unsupported_combination", failure.Code, "错误码");
                    Check.True(failure.Path.Contains("test-snipe", StringComparison.Ordinal), $"路径：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 条件选择：`damage_lowest` 命中的是生命最低者，不是第一个成员 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "conditional");
            var session = Build(model);
            var declaredFirst = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var declaredSecond = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            // 场景刻意让"声明在前的敌人"生命更高：选最低就必然命中后声明的那个。
            var firstHp = SliceFixtures.Number(session, declaredFirst, SliceFixtures.MonsterTypeId, "Hp");
            var secondHp = SliceFixtures.Number(session, declaredSecond, SliceFixtures.MonsterTypeId, "Hp");
            Check.True(secondHp < firstHp, "场景前提：后声明的敌人生命更低");

            var snipe = SliceFixtures.PutInHand(session, "base-spire:test-snipe", upgraded: false);
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(snipe)]);
            Check.Equal(SessionStatus.Running, session.Status, "damage_lowest 不向玩家提问");

            Check.Equal(secondHp - 7, SliceFixtures.Number(session, declaredSecond, SliceFixtures.MonsterTypeId, "Hp"),
                "生命最低者受到 7 点");
            Check.Equal(firstHp, SliceFixtures.Number(session, declaredFirst, SliceFixtures.MonsterTypeId, "Hp"),
                "另一个敌人未受伤");

            // 条件随状态变化：把先声明者的生命压到更低，再打一次必须改打它。
            var second = Build(model);
            var first = SliceFixtures.Cultist(second, SliceManifest.CalcifiedCultistId);
            var other = SliceFixtures.Cultist(second, SliceManifest.DampCultistId);
            var type = second.RuleSet.TypeByQualifiedId(SliceFixtures.MonsterTypeId);
            second.State.RunAtomic(() => second.State.SetField(first, type.FieldIndex("Hp"), Value.Of(10L)));
            var secondSnipe = SliceFixtures.PutInHand(second, "base-spire:test-snipe", upgraded: false);
            var secondAction = second.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            second.Submit(secondAction.Definition, SliceManifest.Player, [Value.OfRef(secondSnipe)]);
            Check.Equal(3L, SliceFixtures.Number(second, first, SliceFixtures.MonsterTypeId, "Hp"),
                "把它的生命改成 10 后，最低者变成它：10 - 7 = 3");
            Check.Equal(40L, SliceFixtures.Number(second, other, SliceFixtures.MonsterTypeId, "Hp"),
                "另一个敌人仍然未受伤（条件选择跟随状态，不是固定在某个实例上）");
        });

        Check.Run("B-05c 条件选择：`top_attack_from_discard` 的候选只有攻击牌 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "conditional");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
            var draw = SliceFixtures.Collection(session, "base-spire:DrawPile");

            // 造一个弃牌堆：一张攻击牌 + 一张技能牌（技能牌不得出现在候选里）。
            var attack = SliceFixtures.Relocate(session, SliceManifest.StrikeId, hand, discard);
            var skill = SliceFixtures.Relocate(session, SliceManifest.DefendId, hand, discard);
            var salvage = SliceFixtures.PutInHand(session, "base-spire:test-salvage", upgraded: false);

            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(salvage)]);

            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "有攻击牌可弃时提问");
            var candidates = session.PendingDecision!.Candidates;
            Check.Contains(candidates, instance => instance == attack, "攻击牌在候选里");
            Check.False(candidates.Contains(skill), "技能牌不在候选里（候选被条件过滤）");

            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [attack]));
            Check.Equal(attack, session.State.CollectionMembers(draw)[0], "选中的攻击牌被置顶");
            Check.True(session.State.IsMember(discard, skill), "技能牌仍在弃牌堆（没有被移动）");
        });

        Check.Run("B-05c 条件选择：没有攻击牌可弃时不提问 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "conditional");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
            SliceFixtures.Relocate(session, SliceManifest.DefendId, hand, discard);
            var salvage = SliceFixtures.PutInHand(session, "base-spire:test-salvage", upgraded: false);

            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(salvage)]);
            Check.Equal(SessionStatus.Running, session.Status, "候选为空 → 不提问");
            Check.True(session.State.CollectionMembers(discard).Count >= 1, "弃牌堆里的技能牌没有被误移");
        });

        Check.Run("B-05c 条件选择：冻结作者文本没有这两个步骤（默认路径不受影响）", () =>
        {
            var model = SliceAuthorFrontend.Load();
            Check.False(model.Cards.Any(card => card.StepKinds.Contains(AuthorSteps.DamageLowest, StringComparer.Ordinal)
                || card.StepKinds.Contains(AuthorSteps.TopAttackFromDiscard, StringComparer.Ordinal)),
                "冻结文本里没有条件选择步骤");

            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build().Build());
            var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
            session.Start();
            Check.Equal(SessionStatus.Running, session.Status, "程序化路径的受控开局仍然成立");
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
