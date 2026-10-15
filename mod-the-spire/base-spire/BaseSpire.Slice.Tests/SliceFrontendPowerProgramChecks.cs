using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第十一批：**能力的回合末行为程序**作者化（此前由"按 id 比较"硬编码）。
///
/// 作者文本：`owner-turn-end = none | tick | convert-to-strength`。
/// 判据：解析→IR→加载→行为；作者路径与程序化路径的行为一致（时长衰减、Ritual 转力量、力量不衰减）；
/// **不留隐式默认**——用了 ritual 行动却没有声明这条行为时在编译期拒绝；冻结作者文本已声明四个能力。
/// </summary>
internal static class SliceFrontendPowerProgramChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x6  test-hasten }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick }
        power weak { name = Weak  role = Weak  effect = "x"  owner-turn-end = tick }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none }
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
        card test-hasten {
          name = TestHasten  kind = Attack  cost = 0
          normal   { amount = 0  power = vulnerable  stacks = 2  steps = [target, damage, apply_power] }
          upgraded { amount = 0  power = vulnerable  stacks = 2  steps = [target, damage, apply_power] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 能力行为：解析 owner-turn-end，非法值与缺声明被拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "power-program");
            var ritual = model.Powers.Single(power => power.Id == "base-spire:ritual");
            Check.Equal(PowerTurnEnd.ConvertToStrength, ritual.OwnerTurnEnd, "Ritual 转力量");
            Check.Equal(PowerTurnEnd.Tick,
                model.Powers.Single(power => power.Id == SliceManifest.VulnerableId).OwnerTurnEnd, "Vulnerable 衰减");
            Check.Equal(PowerTurnEnd.None,
                model.Powers.Single(power => power.Id == SliceManifest.StrengthId).OwnerTurnEnd, "Strength 不衰减");
            // 默认值：完全不写 owner-turn-end 时是 none（不是 tick）。
            var minimal = SliceAuthorFrontend.Parse(
                ScenarioText.Replace("  owner-turn-end = tick", string.Empty), "power-program");
            Check.Equal(PowerTurnEnd.None,
                minimal.Powers.Single(power => power.Id == SliceManifest.VulnerableId).OwnerTurnEnd,
                "未声明 owner-turn-end 时默认 none");

            foreach (var (text, code, path) in new[]
                     {
                         (ScenarioText.Replace("owner-turn-end = tick", "owner-turn-end = explode"),
                             "invalid_value", "owner-turn-end"),
                         // 用了 ritual 行动却没有声明转力量
                         (string.Join("\n", ScenarioText.Split('\n').Where(line =>
                                 !line.Contains("convert-to-strength", StringComparison.Ordinal))),
                             "missing_key", "powers"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "power-program");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Contains(path, StringComparison.Ordinal), $"路径：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 能力行为：作者路径与程序化路径的时长/Ritual 语义一致 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "power-program");
            var authored = Build(model);
            var programmatic = Build(null);

            var authoredFront = SliceFixtures.Cultist(authored, SliceManifest.CalcifiedCultistId);
            var programmaticFront = SliceFixtures.Cultist(programmatic, SliceManifest.CalcifiedCultistId);

            // 给它挂 2 层易伤（声明用 tick），再结束回合：第一次端末**跳过**、第二次才减 1。
            ApplyPower(authored, authoredFront, SliceManifest.VulnerableId, 2);
            ApplyPower(programmatic, programmaticFront, SliceManifest.VulnerableId, 2);
            Check.Equal(2L, Stacks(authored, authoredFront, SliceManifest.VulnerableId), "施加后 2 层");

            EndTurn(authored);
            EndTurn(programmatic);
            Check.Equal(2L, Stacks(authored, authoredFront, SliceManifest.VulnerableId),
                "作者路径：施加当回合末跳过，仍是 2 层");
            Check.Equal(Stacks(programmatic, programmaticFront, SliceManifest.VulnerableId),
                Stacks(authored, authoredFront, SliceManifest.VulnerableId), "两条路径一致（首回合跳过）");

            EndTurn(authored);
            EndTurn(programmatic);
            Check.Equal(1L, Stacks(authored, authoredFront, SliceManifest.VulnerableId),
                "作者路径：第二个端末减 1（tick）；" + Diagnostic(authored, authoredFront) + " / " +
                Diagnostic(programmatic, programmaticFront));
            Check.Equal(Stacks(programmatic, programmaticFront, SliceManifest.VulnerableId),
                Stacks(authored, authoredFront, SliceManifest.VulnerableId),
                "两条路径一致（衰减）；authored: " + Diagnostic(authored, authoredFront) +
                " | programmatic: " + Diagnostic(programmatic, programmaticFront));
        });

        Check.Run("B-05c 能力行为：Ritual 按声明在端末转成力量，且力量自己不再衰减 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "power-program");
            var session = Build(model);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            // 第一个敌人回合：念咒上 4 层 Ritual（施加当回合不转）。
            EndTurn(session);
            Check.Equal(4L, Stacks(session, front, SliceManifest.RitualId), "Ritual 仍在（首回合跳过）");
            Check.Equal(0L, Stacks(session, front, SliceManifest.StrengthId), "首回合未转力量");

            // 第二个敌人回合：Ritual 转成 4 层力量；力量声明 none，因此之后不再变。
            EndTurn(session);
            Check.Equal(4L, Stacks(session, front, SliceManifest.StrengthId), "第二个端末转成 4 层力量");

            EndTurn(session);
            // 力量自己**不衰减**（none），但 Ritual 每个端末都会再把它的层数加到力量上，
            // 所以力量是**累加**的：4 → 8。这与"力量不随时间减少"并不矛盾。
            Check.Equal(8L, Stacks(session, front, SliceManifest.StrengthId),
                "第三个端末：Ritual 再加 4 层力量（力量自身不衰减）");
            Check.Equal(4L, Stacks(session, front, SliceManifest.RitualId),
                "Ritual 自身声明为转力量，层数不变");
        });

        Check.Run("B-05c 能力行为：冻结作者文本声明了四个能力的回合末行为", () =>
        {
            var model = SliceAuthorFrontend.Load();
            Check.Equal(SliceManifest.Powers.Count + SliceFixtures.AuthoredPowersBeyondSlice.Count,
                model.Powers.Count, "作者文本的能力数（切片 + B-06a 已迁能力）");
            Check.Equal(PowerTurnEnd.ConvertToStrength,
                model.Powers.Single(power => power.Id == SliceManifest.RitualId).OwnerTurnEnd, "冻结文本的 Ritual");
            foreach (var id in new[] { SliceManifest.VulnerableId, SliceManifest.WeakId })
            {
                Check.Equal(PowerTurnEnd.Tick,
                    model.Powers.Single(power => power.Id == id).OwnerTurnEnd, $"{id} 是时长型");
            }

            Check.Equal(PowerTurnEnd.None,
                model.Powers.Single(power => power.Id == SliceManifest.StrengthId).OwnerTurnEnd, "Strength 不衰减");
        });
    }

    private static void ApplyPower(Session session, InstanceId owner, string powerId, int stacks)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        var power = session.State.CreateInstance(powerType.Id, session.State.ScopeOf(owner));
        session.State.RunAtomic(() =>
        {
            session.State.SetField(power, powerType.FieldIndex("DefinitionId"), Value.Of(powerId));
            session.State.SetField(power, powerType.FieldIndex("Stacks"), Value.Of((long)stacks));
            session.State.SetField(power, powerType.FieldIndex("Ticks"), Value.Of(true));
            session.State.SetField(power, powerType.FieldIndex("SkipNextTick"), Value.Of(true));
            session.State.AddRelation(relation.Id, owner, power);
        });
    }

    private static string Diagnostic(Session session, InstanceId owner)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        return string.Join(";", session.State.RelationTargets(relation.Id, owner).Select(power =>
            $"{session.State.GetField(power, powerType.FieldIndex("DefinitionId")).AsString()}" +
            $"={session.State.GetField(power, powerType.FieldIndex("Stacks")).AsInt()}" +
            $"/ticks={session.State.GetField(power, powerType.FieldIndex("Ticks")).AsBool()}" +
            $"/skip={session.State.GetField(power, powerType.FieldIndex("SkipNextTick")).AsBool()}"));
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
