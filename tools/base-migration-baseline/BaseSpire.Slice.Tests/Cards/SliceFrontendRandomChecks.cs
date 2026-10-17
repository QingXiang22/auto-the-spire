using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第五批：目标域 `damage_random`（按命名随机流抽一个敌人）＋**候选顺序与消费点都有来源**。
///
/// 两条明文判据（§14 B-05c 行："随机候选顺序和消费点有来源"）：
/// <list type="number">
/// <item>**消费点**：每次 `damage_random` 恰好消费命名流一次（`RandomService.DrawCount` 可核对），
/// 且不影响重洗流；</item>
/// <item>**候选顺序**：不是插入顺序。把敌人集合的成员顺序倒过来，同一种子必须仍选中**同一个实例**
/// ——这正是"顺序有来源（公开语义键排序）"的可判别形式。</item>
/// </list>
/// </summary>
internal static class SliceFrontendRandomChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck {
          strike-ironclad x4
          test-volley x2
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
        card test-volley {
          name = TestVolley  kind = Attack  cost = 0
          normal   { amount = 5  steps = [damage_random] }
          upgraded { amount = 7  steps = [damage_random] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c random target: 语法接受 damage_random，且目标确定方式必须唯一（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "random");
            var volley = model.Cards.Single(card => card.Id == "base-spire:test-volley");
            Check.Sequence([AuthorSteps.DamageRandom], volley.StepKinds, "damage_random 被接受");
            Check.Equal(0, volley.Cost, "随机目标牌的费用");

            foreach (var (text, code) in new[]
                     {
                         // 两种目标确定方式同时出现
                         (ScenarioText.Replace("steps = [damage_random]",
                             "steps = [target, damage_random]"), "unsupported_combination"),
                         (ScenarioText.Replace("steps = [damage_random]",
                             "steps = [damage_random, damage_all]"), "unsupported_combination"),
                         // 未知步骤
                         (ScenarioText.Replace("steps = [damage_random]", "steps = [damage_any]"), "unknown_step"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "random");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Contains("test-volley", StringComparison.Ordinal),
                        $"路径应指向该卡：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c random target: 消费点有来源（每次恰好一次命名流，且不动重洗流）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "random");
            var session = Build(model);

            Check.Equal(0L, session.Random.DrawCount(SliceManifest.RandomTargetStream),
                "开局还没有消费随机目标流");
            // 流是**首次消费时**才创建的，所以开局只断言"还没消费过"，不断言它存在。
            Check.False(session.Random.Streams.Contains(SliceManifest.RandomTargetStream),
                "还没消费时随机目标流尚未创建");

            var volley = SliceFixtures.PutInHand(session, "base-spire:test-volley", upgraded: false);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var drawBefore = session.Random.DrawCount(SliceManifest.DrawStream);
            SliceFixtures.PlayAt(session, volley, front);

            Check.Equal(1L, session.Random.DrawCount(SliceManifest.RandomTargetStream),
                "一次 damage_random 恰好消费随机目标流一次");
            Check.Equal(drawBefore, session.Random.DrawCount(SliceManifest.DrawStream),
                "随机目标不消费重洗流（两条流互不影响）");
            Check.Contains(session.Random.Streams, name => name == SliceManifest.RandomTargetStream,
                "消费后随机目标流出现在流列表里（流身份有来源）");

            // 第二次打出：再消费一次（不是"只抽一次就固定"）。
            var volley2 = SliceFixtures.PutInHand(session, "base-spire:test-volley", upgraded: true, (7, 0));
            SliceFixtures.PlayAt(session, volley2, front);
            Check.Equal(2L, session.Random.DrawCount(SliceManifest.RandomTargetStream),
                "每一次打出都消费一次");

            var diagnostics = session.Random.Diagnostics()
                .Single(entry => entry.Name == SliceManifest.RandomTargetStream);
            Check.Equal(2L, diagnostics.Draws, "诊断里的抽取次数与消费点一致");
            Check.True(diagnostics.StateFingerprint != 0UL, "流状态指纹可核对");
        });

        Check.Run("B-05c random target: 候选顺序不是插入顺序（倒转集合成员后同种子仍选同一实例）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "random");

            // 同一种子、同一次打出：先记录选中的是谁。
            var first = Build(model, reverseMonsterOrder: false);
            var firstFront = SliceFixtures.Cultist(first, SliceManifest.CalcifiedCultistId);
            var firstBack = SliceFixtures.Cultist(first, SliceManifest.DampCultistId);
            var firstFrontBefore = SliceFixtures.Number(first, firstFront, SliceFixtures.MonsterTypeId, "Hp");
            var firstBackBefore = SliceFixtures.Number(first, firstBack, SliceFixtures.MonsterTypeId, "Hp");
            var volleyA = SliceFixtures.PutInHand(first, "base-spire:test-volley", upgraded: false);
            SliceFixtures.PlayAt(first, volleyA, firstFront);
            var hitFrontFirst = firstFrontBefore - SliceFixtures.Number(first, firstFront,
                SliceFixtures.MonsterTypeId, "Hp");
            var hitBackFirst = firstBackBefore - SliceFixtures.Number(first, firstBack,
                SliceFixtures.MonsterTypeId, "Hp");
            Check.True(hitFrontFirst + hitBackFirst == 5, "恰好一名敌人受到 5 点（抽一个，不是全体）");

            // 把敌人集合的**成员顺序倒过来**：若顺序来自插入顺序，选中的会变成另一个实例。
            var second = Build(model, reverseMonsterOrder: true);
            var secondFront = SliceFixtures.Cultist(second, SliceManifest.CalcifiedCultistId);
            var secondBack = SliceFixtures.Cultist(second, SliceManifest.DampCultistId);
            var secondFrontBefore = SliceFixtures.Number(second, secondFront, SliceFixtures.MonsterTypeId, "Hp");
            var secondBackBefore = SliceFixtures.Number(second, secondBack, SliceFixtures.MonsterTypeId, "Hp");
            var volleyB = SliceFixtures.PutInHand(second, "base-spire:test-volley", upgraded: false);
            SliceFixtures.PlayAt(second, volleyB, secondFront);
            var hitFrontSecond = secondFrontBefore - SliceFixtures.Number(second, secondFront,
                SliceFixtures.MonsterTypeId, "Hp");
            var hitBackSecond = secondBackBefore - SliceFixtures.Number(second, secondBack,
                SliceFixtures.MonsterTypeId, "Hp");

            Check.Equal(hitFrontFirst, hitFrontSecond,
                "倒转集合成员后，同一实例受到的伤害必须相同（顺序来源是公开语义键，不是插入顺序）");
            Check.Equal(hitBackFirst, hitBackSecond, "另一个实例同样不变");
        });

        Check.Run("B-05c random target: 不同种子会选出不同目标（随机流真的被消费）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "random");
            var picks = new List<bool>();
            for (ulong seed = 1; seed <= 24; seed++)
            {
                var session = Build(model, seed: seed);
                var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
                var frontBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
                var backBefore = SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");
                var volley = SliceFixtures.PutInHand(session, "base-spire:test-volley", upgraded: false);
                SliceFixtures.PlayAt(session, volley, front);
                picks.Add(SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp") < frontBefore
                    && SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp") == backBefore);
            }

            Check.True(picks.Any(pick => pick) && picks.Any(pick => !pick),
                $"24 个种子必须两个目标都抽到过（实际：前排 {picks.Count(p => p)} 次）");
        });

        Check.Run("B-05c random target: 冻结作者文本没有随机目标牌（默认路径不受影响）", () =>
        {
            var model = SliceAuthorFrontend.Load();
            Check.False(model.Cards.Any(card => card.StepKinds.Contains(AuthorSteps.DamageRandom, StringComparer.Ordinal)),
                "冻结文本里没有 damage_random");

            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build().Build());
            var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
            session.Start();
            Check.Equal(0L, session.Random.DrawCount(SliceManifest.RandomTargetStream),
                "程序化路径没有创建随机目标流（它的程序里没有 SamplePick）");
        });
    }

    private static Session Build(SliceAuthorModel model, ulong seed = 20261001UL, bool reverseMonsterOrder = false)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: model)
            .Build());
        var session = new Session(builder.Build(), seed, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        if (reverseMonsterOrder)
        {
            // 只改**集合成员顺序**（插入顺序），不改任何可见字段：用于判别候选顺序的来源。
            var monsters = SliceFixtures.Collection(session, "base-spire:Monsters");
            var members = session.State.CollectionMembers(monsters);
            session.State.RunAtomic(() =>
            {
                foreach (var member in members) session.State.RemoveFromCollection(monsters, member);
                foreach (var member in members.Reverse()) session.State.AddToCollection(monsters, member);
            });
        }

        return session;
    }
}
