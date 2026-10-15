using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第九批：**敌人的行动程序**作者化（此前只作者化了它们的数据）。
///
/// 作者文本：
/// <code>
/// move incantation { ritual = first-ritual }
/// move dark-strike { damage = strike }
/// cycle = [incantation, dark-strike xforever]
/// </code>
/// 判据：解析→IR→加载→行为；最后一个行动永远重复；数值可以引用敌人的字段（数值仍只写在数据里）；
/// 不可达行动、缺行动程序、循环写法错误都在编译期拒绝；冻结文本已声明行动程序，程序化路径行为不变。
/// </summary>
internal static class SliceFrontendEnemyProgramChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x5 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 44  tough-hp = 45  first-move-ritual = 3  strike-damage = 7
          strike-damage-deadly = 9  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 51  tough-hp = 52  first-move-ritual = 5  strike-damage = 1
          strike-damage-deadly = 3  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = 2 }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 1
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 9  steps = [target, damage] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 敌人行动程序：解析 move/cycle，并拒绝不可达行动、缺程序与错误循环（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "enemy-program");
            var calcified = model.Enemies.Single(enemy => enemy.Id == SliceManifest.CalcifiedCultistId);
            Check.Sequence(["incantation", "dark-strike"], calcified.Cycle, "循环");
            Check.Equal(2, calcified.Moves.Count, "行动个数");
            Check.Sequence(["ritual = first-ritual"], calcified.Moves[0].Steps.Select(step => step.ToString()),
                "念咒的动作");

            foreach (var (text, code) in new[]
                     {
                         // 循环里少一个行动 → 该行动不可达
                         (ScenarioText.Replace("cycle = [incantation, dark-strike xforever]",
                             "cycle = [dark-strike xforever]"), "unreachable_move"),
                         // 循环引用未声明的行动
                         (ScenarioText.Replace("cycle = [incantation, dark-strike xforever]",
                             "cycle = [incantation, missing-move xforever]"), "dangling_reference"),
                         // 非末尾行动标 xforever
                         (ScenarioText.Replace("cycle = [incantation, dark-strike xforever]",
                             "cycle = [incantation xforever, dark-strike xforever]"), "unsupported_combination"),
                         // 未知动作
                         (ScenarioText.Replace("move dark-strike { damage = strike }",
                             "move dark-strike { heal = strike }"), "unknown_key"),
                         // 完全没有行动程序（按行过滤，避免依赖缩进）
                         (string.Join("\n", ScenarioText.Split('\n').Where(line =>
                                 !line.TrimStart().StartsWith("move ", StringComparison.Ordinal)
                                 && !line.TrimStart().StartsWith("cycle = ", StringComparison.Ordinal))),
                             "missing_key"),
                     })
                {
                try
                {
                    SliceAuthorFrontend.Parse(text, "enemy-program");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Contains("enemy", StringComparison.Ordinal), $"路径：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 敌人行动程序：开场念咒、此后每回合暗击，数值来自声明的字段引用 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "enemy-program");
            var session = Build(model);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var player = SliceFixtures.Player(session);
            var playerHpBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");

            // 第 1 个敌人回合：念咒（给自己上 Ritual），玩家不掉血。
            EndTurn(session);
            Check.Equal(playerHpBefore, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "开场念咒的回合玩家不掉血");
            Check.Equal(3L, RitualStacks(session, front), "前排按声明的 first-ritual = 3 上 Ritual");
            Check.Equal(5L, RitualStacks(session, back), "后排按声明上 5 层");

            // 第 2 个敌人回合：暗击。前排 7（字段引用 strike），后排 2（字面量）。
            EndTurn(session);
            var afterSecond = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            Check.Equal(playerHpBefore - 9, afterSecond, "第二次敌人回合：7 + 2 = 9 点");

            // 第 3 个敌人回合：仍在暗击（最后一个行动永远重复）。此时 Ritual 已在上一个回合结束时
            // 转成 Strength（3 + 5 = 8），所以伤害是 9 + 8 = 17 —— 这不是循环变了，而是既有力量语义。
            EndTurn(session);
            Check.Equal(playerHpBefore - 9 - 17, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "第三个敌人回合仍在重复暗击（xforever；含 Ritual 转出的 8 点力量）");
        });

        Check.Run("B-05c 敌人行动程序：冻结作者文本已声明行动程序，且作者路径与程序化路径行为一致 [真实]", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            foreach (var enemy in frozen.Enemies)
            {
                Check.True(enemy.Moves.Count > 0 && enemy.Cycle.Count > 0, $"{enemy.Id} 声明了行动程序");
                Check.Equal("dark-strike", enemy.Cycle[^1], $"{enemy.Id} 的重复行动");
            }

            // 两条路径各跑一个敌人回合，玩家受到的伤害必须相同。
            var authored = Build(frozen);
            var programmatic = Build(null);
            EndTurn(authored);
            EndTurn(programmatic);
            Check.Equal(
                SliceFixtures.Number(programmatic, SliceFixtures.Player(programmatic), SliceFixtures.PlayerTypeId, "Hp"),
                SliceFixtures.Number(authored, SliceFixtures.Player(authored), SliceFixtures.PlayerTypeId, "Hp"),
                "第一个敌人回合（念咒）两条路径一致");

            EndTurn(authored);
            EndTurn(programmatic);
            Check.Equal(
                SliceFixtures.Number(programmatic, SliceFixtures.Player(programmatic), SliceFixtures.PlayerTypeId, "Hp"),
                SliceFixtures.Number(authored, SliceFixtures.Player(authored), SliceFixtures.PlayerTypeId, "Hp"),
                "第二个敌人回合（暗击）两条路径一致");
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

    /// <summary>该敌人身上 Ritual 的层数（没有则 0）。</summary>
    private static long RitualStacks(Session session, InstanceId monster)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        foreach (var power in session.State.RelationTargets(relation.Id, monster))
        {
            if (session.State.GetField(power, powerType.FieldIndex("DefinitionId")).AsString()
                == SliceManifest.RitualId)
            {
                return session.State.GetField(power, powerType.FieldIndex("Stacks")).AsInt();
            }
        }

        return 0;
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
