using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第三批：剩下三个 hook —— `on_enter_combat`（开局创建这张牌之后）、`before_card_played` 与
/// `after_card_played`（这张牌被打出之前/之后）。
///
/// 判据：三个触发点都复用批次 2 的共享步骤降级；hook 逐变体独立（不隐式继承）；超出运行时能力的步骤在编译期拒绝；
/// 冻结作者文本不受影响。
/// </summary>
internal static class SliceFrontendPlayHookChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck {
          strike-ironclad x4
          defend-ironclad x4
          test-vanguard
          test-herald
          test-echo
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
        card test-vanguard {
          name = TestVanguard  kind = Skill  cost = 0
          normal {
            amount = 3
            steps = [target, damage]
            hook on_enter_combat { steps = [damage_all] }
          }
          upgraded {
            amount = 5
            steps = [target, damage]
          }
        }
        card test-herald {
          name = TestHerald  kind = Attack  cost = 0
          normal {
            amount = 2
            steps = [target, damage]
            hook before_card_played { steps = [damage_all] }
          }
          upgraded {
            amount = 4
            steps = [target, damage]
            hook before_card_played { steps = [damage_all] }
          }
        }
        card test-echo {
          name = TestEcho  kind = Attack  cost = 0
          normal {
            amount = 7
            steps = [target, damage]
            hook after_card_played { steps = [damage_all] }
          }
          upgraded {
            amount = 7
            steps = [target, damage]
            hook after_card_played { steps = [damage_all] }
          }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 三个出牌期 hook：语法与逐变体独立性（不隐式继承，带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "play-hooks");
            var vanguard = model.Cards.Single(card => card.Id == "base-spire:test-vanguard");
            Check.Sequence([AuthorSteps.DamageAll], vanguard.NormalHooks.OnEnterCombat.Select(s => s.Kind), "普通变体声明 on_enter_combat");
            Check.True(vanguard.UpgradedHooks.IsEmpty, "升级变体没有声明 hook（不继承）");

            var herald = model.Cards.Single(card => card.Id == "base-spire:test-herald");
            Check.Sequence([AuthorSteps.DamageAll], herald.NormalHooks.BeforeCardPlayed.Select(s => s.Kind), "before_card_played");
            Check.Sequence([AuthorSteps.DamageAll], herald.UpgradedHooks.BeforeCardPlayed.Select(s => s.Kind), "升级变体独立声明了同一个 hook");

            var echo = model.Cards.Single(card => card.Id == "base-spire:test-echo");
            Check.Sequence([AuthorSteps.DamageAll], echo.NormalHooks.AfterCardPlayed.Select(s => s.Kind), "after_card_played");

            // 未知 hook 名与 hook 里的超能力步骤仍然拒绝。
            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("hook after_card_played", "hook on_discard"), "unknown_hook"),
                         (ScenarioText.Replace("hook before_card_played { steps = [damage_all] }",
                             "hook before_card_played { steps = [top_from_discard] }"), "unsupported_capability"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "play-hooks");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Length > 0, "拒绝必须带路径");
                }
            }
        });

        Check.Run("B-05c on_enter_combat：开局按声明数值触发一次 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "play-hooks");
            var session = Build(model);
            // 开局创建 test-vanguard 之后即触发 damage_all(3)：两名敌人在任何出牌之前就少了 3 点。
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            // 基线取**本场景**声明的血量（场景文本的 hp 与冻结清单不同，用错基线会误判）。
            var scenarioFront = model.Enemies.Single(enemy => enemy.Id == SliceManifest.CalcifiedCultistId).NormalMinHp;
            var scenarioBack = model.Enemies.Single(enemy => enemy.Id == SliceManifest.DampCultistId).NormalMinHp;
            Check.Equal(scenarioFront - 3,
                SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "on_enter_combat 对前排造成 3");
            Check.Equal(scenarioBack - 3,
                SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                "on_enter_combat 对后排造成 3");

            // 对照：冻结作者文本没有该 hook，开局敌人血量不受影响。
            var plain = Build(SliceAuthorFrontend.Load());
            var plainFront = SliceFixtures.Cultist(plain, SliceManifest.CalcifiedCultistId);
            Check.Equal((long)SliceManifest.Enemy(SliceManifest.CalcifiedCultistId).SliceHp,
                SliceFixtures.Number(plain, plainFront, SliceFixtures.MonsterTypeId, "Hp"),
                "没有该 hook 时开局血量不变");
        });

        Check.Run("B-05c before_card_played 与 after_card_played 在该牌效果前后各触发一次 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "play-hooks");

            // before_card_played：打出 test-herald（自身 damage 2 + hook damage_all 2）。
            var session = Build(model);
            var herald = SliceFixtures.PutInHand(session, "base-spire:test-herald", upgraded: false);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var frontBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            var backBefore = SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, herald, front);
            Check.Equal(frontBefore - 4, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "所选目标受到自身 2 + hook 2");
            Check.Equal(backBefore - 2, SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                "后排只受 hook 的 2（before 触发一次）");

            // after_card_played：升级形态只声明 after 的 hook（自身 7 + hook 7）。
            var after = Build(model);
            var echo = SliceFixtures.PutInHand(after, "base-spire:test-echo", upgraded: true, (7, 0));
            var afterFront = SliceFixtures.Cultist(after, SliceManifest.CalcifiedCultistId);
            var afterBack = SliceFixtures.Cultist(after, SliceManifest.DampCultistId);
            var afterFrontBefore = SliceFixtures.Number(after, afterFront, SliceFixtures.MonsterTypeId, "Hp");
            var afterBackBefore = SliceFixtures.Number(after, afterBack, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(after, echo, afterFront);
            Check.Equal(afterFrontBefore - 14,
                SliceFixtures.Number(after, afterFront, SliceFixtures.MonsterTypeId, "Hp"),
                "所选目标受到自身 7 + after hook 7");
            Check.Equal(afterBackBefore - 7,
                SliceFixtures.Number(after, afterBack, SliceFixtures.MonsterTypeId, "Hp"),
                "后排只受 after hook 的 7（触发一次）");
        });

        Check.Run("B-05c 三个新 hook 在冻结作者文本里都不存在（默认路径与冻结数据不被改动）", () =>
        {
            var model = SliceAuthorFrontend.Load();
            // 只对切片那 4 张卡断言：B-06a 的 Beckon 在来源里带 turnEndInHand hook（不属于本批的三个 hook）。
            foreach (var entry in SliceManifest.Cards)
            {
                var card = model.Cards.Single(candidate => candidate.Id == entry.ContentId);
                Check.True(card.NormalHooks.IsEmpty && card.UpgradedHooks.IsEmpty, $"{card.Id} 仍无 hook");
            }

            // 程序化路径（默认）与作者路径的冻结文本都必须给出同一份开局血量。
            var programmatic = Build(null);
            var authored = Build(model);
            var type = SliceFixtures.MonsterTypeId;
            Check.Equal(
                SliceFixtures.Number(programmatic, SliceFixtures.Cultist(programmatic, SliceManifest.CalcifiedCultistId),
                    type, "Hp"),
                SliceFixtures.Number(authored, SliceFixtures.Cultist(authored, SliceManifest.CalcifiedCultistId),
                    type, "Hp"),
                "两条路径的受控开局一致");
        });
    }

    private static Session Build(SliceAuthorModel? model)
    {
        var builder = new RuleSetBuilder();
        builder.Add(model is null
            ? CombatSlicePackage.Build().Build()
            : CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: model).Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }
}
