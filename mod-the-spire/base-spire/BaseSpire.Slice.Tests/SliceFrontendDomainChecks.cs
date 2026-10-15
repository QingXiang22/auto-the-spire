using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 的第一批：把效果链做成**完全由步骤驱动**，并新增一个目标域步骤 `damage_all`（对全部敌人各一次）。
///
/// 判据（对应开工清单 §14 B-05c 行）：
/// <list type="bullet">
/// <item>语法、编译结果、运行语义、检查四态分别有证据；</item>
/// <item><b>目标域有独立检查</b>：多敌目标不串对象、各打一次且数值来自声明；</item>
/// <item>kind 不再限制效果组合（效果由步骤驱动）；`damage` 与 `damage_all` 二选一，不能同时出现；</item>
/// <item>新能力只用**既有通用 IR 原语**（ForEach + Collection + Call），没有新增 STS 专用节点；</item>
/// <item>内嵌的冻结作者文本不受影响（步骤与卡组逐条不变）。</item>
/// </list>
/// </summary>
internal static class SliceFrontendDomainChecks
{
    /// <summary>测试场景作者文本：在冻结文本之上加两张用于验证目标域/混合效果的卡（只在本检查里使用）。</summary>
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck {
          strike-ironclad x5
          defend-ironclad x4
          bash
          test-cleave
          test-bulwark
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
        card defend-ironclad {
          name = DefendIronclad  kind = Skill  cost = 1
          normal   { amount = 5  steps = [block] }
          upgraded { amount = 8  steps = [block] }
        }
        card bash {
          name = Bash  kind = Attack  cost = 2
          normal   { amount = 8   power = vulnerable  stacks = 2  steps = [target, damage, apply_power] }
          upgraded { amount = 10  power = vulnerable  stacks = 3  steps = [target, damage, apply_power] }
        }
        card test-cleave {
          name = TestCleave  kind = Attack  cost = 1
          normal   { amount = 5  steps = [damage_all] }
          upgraded { amount = 7  steps = [damage_all] }
        }
        card test-bulwark {
          name = TestBulwark  kind = Skill  cost = 1
          normal   { amount = 4  steps = [target, damage, block] }
          upgraded { amount = 6  steps = [target, damage, block] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c target domain: 语法接受 damage_all，且拒绝目标域不明确的组合（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "scenario");
            var cleave = model.Cards.Single(card => card.Id == "base-spire:test-cleave");
            Check.Sequence([AuthorSteps.DamageAll], cleave.StepKinds, "damage_all 步骤被接受");
            Check.Equal("base-spire:test-cleave", model.Deck.Single(entry => entry.CardId.EndsWith("test-cleave")).CardId,
                "测试卡进入卡组");

            // damage 与 damage_all 同时出现：目标域不明确，必须拒绝。
            try
            {
                SliceAuthorFrontend.Parse(ScenarioText.Replace("steps = [damage_all]", "steps = [target, damage, damage_all]"),
                    "scenario");
                Check.True(false, "damage 与 damage_all 同时出现必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("unsupported_combination", failure.Code, "错误码");
                Check.True(failure.Path.Contains("test-cleave", StringComparison.Ordinal), $"路径须指向该卡：{failure.Path}");
            }

            // 未知目标域词汇仍然拒绝。
            try
            {
                SliceAuthorFrontend.Parse(ScenarioText.Replace("steps = [damage_all]", "steps = [damage_some]"), "scenario");
                Check.True(false, "未知步骤必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("unknown_step", failure.Code, "错误码");
            }
        });

        Check.Run("B-05c target domain: damage_all 对每个敌人各打一次，数值来自声明且不串对象 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "scenario");
            var session = Build(model);
            var card = SliceFixtures.PutInHand(session, "base-spire:test-cleave", upgraded: false);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var frontBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            var backBefore = SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");

            // 不选目标：damage_all 不应产生目标决策。
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
            Check.Equal(SessionStatus.Running, session.Status, "damage_all 不应请求目标选择");

            Check.Equal(frontBefore - 5, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "前排敌人各受 5 点");
            Check.Equal(backBefore - 5, SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                "后排敌人各受 5 点（不串对象、不重复）");

            // 升级形态：数值来自声明的 upgraded amount。
            var upgraded = Build(model);
            var upgradedCard = SliceFixtures.PutInHand(upgraded, "base-spire:test-cleave", upgraded: true, (7, 0));
            var upgradedFront = SliceFixtures.Cultist(upgraded, SliceManifest.CalcifiedCultistId);
            var upgradedBack = SliceFixtures.Cultist(upgraded, SliceManifest.DampCultistId);
            var frontUpgraded = SliceFixtures.Number(upgraded, upgradedFront, SliceFixtures.MonsterTypeId, "Hp");
            var backUpgraded = SliceFixtures.Number(upgraded, upgradedBack, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(upgraded, upgradedCard, upgradedFront);
            Check.Equal(frontUpgraded - 7, SliceFixtures.Number(upgraded, upgradedFront, SliceFixtures.MonsterTypeId, "Hp"),
                "升级后前排受 7 点");
            Check.Equal(backUpgraded - 7, SliceFixtures.Number(upgraded, upgradedBack, SliceFixtures.MonsterTypeId, "Hp"),
                "升级后后排受 7 点");
        });

        Check.Run("B-05c 效果完全由步骤驱动：同一张卡可以既造成伤害又获得格挡（kind 只作元数据）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "scenario");
            var session = Build(model);
            var card = SliceFixtures.PutInHand(session, "base-spire:test-bulwark", upgraded: false);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var before = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");

            SliceFixtures.PlayAt(session, card, front);

            Check.Equal(before - 4, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "damage 步骤生效");
            Check.Equal(4L, SliceFixtures.Number(session, SliceFixtures.Player(session), SliceFixtures.PlayerTypeId,
                "Block"), "同一张卡的 block 步骤也生效（效果不再按 kind 分派）");
        });

        Check.Run("B-05c 冻结作者文本不受影响：卡组与四张卡的步骤逐条不变", () =>
        {
            var model = SliceAuthorFrontend.Load();
            Check.Equal(SliceManifest.Cards.Count + SliceFixtures.AuthoredBeyondSlice.Count, model.Cards.Count,
                "冻结文本的卡牌数（切片 + B-06a 已迁内容）");
            Check.Sequence(SliceManifest.StarterDeck(), SliceAuthorFrontend.ToStarterDeck(model), "冻结卡组不变");
            // 只对**冻结切片的那 4 张卡**断言：B-06a 起作者文本里可以有真正使用 damage_all 的内容
            // （例如来源 ironclad-cards.spire:1549 的 Breakthrough）。
            foreach (var entry in SliceManifest.Cards)
            {
                var frozenCard = model.Cards.Single(candidate => candidate.Id == entry.ContentId);
                Check.False(frozenCard.StepKinds.Contains(AuthorSteps.DamageAll, StringComparer.Ordinal),
                    $"{frozenCard.Id} 不在冻结文本里使用 damage_all");
            }

            // 目标域是**新增能力**，不是替换：单目标路径仍按声明工作。
            var session = Build(SliceAuthorFrontend.Load());
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var frontBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            var backBefore = SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, strike, front);
            Check.Equal(frontBefore - 6, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "单目标攻击命中所选敌人");
            Check.Equal(backBefore, SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                "单目标攻击不波及另一个敌人");
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
