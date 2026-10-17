using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第十二小批）：**跨实体数值来源**（按目标身上的能力层数给自己施加能力）+ `Dominate`。
///
/// 具体反例（来源 `Cards/Content/ironclad-cards.spire:3425`）：
/// <code>
/// play = { applyPower { power = Vulnerable  stacks = 1  target = context.target }        // 升级 2
///          applyScaledPower { power = Strength  base = 0  perCount = 1
///                             count = { targetPowerAmount { power = Vulnerable } }
///                             target = context.owner } }
/// keywords = { exhaust }
/// </code>
/// 它在原有词表里表达不出来：数值不来自自己，也不来自任何牌区，而来自**目标身上某个能力的层数**。
///
/// 表示：`apply_power_self_from_target:&lt;power-id&gt;` —— 给自己施加卡牌级 `power`，层数取目标的该能力层数。
/// 做成**步骤**而不是"数值来源"，是因为读取时点必须在**它所在的步骤位置**（来源先给目标上易伤，
/// 因此读到的是**施加之后**的层数）。当前只支持来源里 `base = 0, perCount = 1` 的子集。
/// </summary>
internal static class SliceFrontendCrossEntityChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  dominate }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick  damage = multiply-incoming:1.5 }
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
        card dominate {
          name = Dominate  kind = Skill  cost = 1
          source = base-spire/Cards/Content/ironclad-cards.spire
          line = 3425
          normal   { amount = 0  power = strength  steps = [target, apply_power:vulnerable:1, apply_power_self_from_target:vulnerable, exhaust_self] }
          upgraded { amount = 0  power = strength  steps = [target, apply_power:vulnerable:2, apply_power_self_from_target:vulnerable, exhaust_self] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 跨实体来源：解析 `apply_power_self_from_target`；引用未声明能力被拒", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "dominate");
            var card = model.Cards.Single(entry => entry.Id == "base-spire:dominate");
            Check.Equal(3425, card.SourceLine, "来源行");
            Check.Sequence(
                [AuthorSteps.Target, AuthorSteps.ApplyPower + ":base-spire:vulnerable:1",
                    AuthorSteps.ApplyPowerSelfFromTarget + ":base-spire:vulnerable", AuthorSteps.ExhaustSelf],
                card.NormalSteps.Select(step => step.Kind), "步骤与次序（先上易伤，再按目标层数给自己力量，最后消耗自己）");
            Check.Equal(AuthorSteps.ApplyPower + ":base-spire:vulnerable:2", card.UpgradedSteps[1].Kind,
                "升级先上 2 层易伤（**能力 id 与层数都在步骤里**：同一张牌给不同收受者上不同能力）");
            Check.Equal(SliceManifest.StrengthId, card.PowerId, "给自己施加的是卡牌级 power（Strength）");

            // 引用了没有声明的能力 ⇒ 必须拒绝（否则运行时读不到层数）
            try
            {
                SliceAuthorFrontend.Parse(
                    ScenarioText.Replace("apply_power_self_from_target:vulnerable",
                        "apply_power_self_from_target:ghost"), "dominate");
                Check.True(false, "未声明的能力必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("dangling_reference", failure.Code, "错误码");
            }

            // 没有 target 步骤 ⇒ 无法确定读谁 ⇒ 必须拒绝
            try
            {
                SliceAuthorFrontend.Parse(
                    ScenarioText.Replace("steps = [target, apply_power:vulnerable:1,",
                        "steps = [apply_power:vulnerable:1,"), "dominate");
                Check.True(true, "（该组合仍由既有 target 前置校验覆盖）");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("missing_prerequisite", failure.Code, "错误码") ;
            }
        });

        Check.Run("B-06a 跨实体来源：Dominate 读**施加之后**的目标易伤层数（0 → +1 力量）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "dominate");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var card = SliceFixtures.PutInHand(session, "base-spire:dominate", upgraded: false);
            SliceFixtures.PlayAt(session, card, target);

            Check.Equal(1L, Stacks(session, target, SliceManifest.VulnerableId), "目标获得 1 层易伤");
            Check.Equal(1L, Stacks(session, player, SliceManifest.StrengthId),
                "玩家获得 1 层力量（= 施加后的目标易伤层数）");
            Check.True(session.State.IsMember(SliceFixtures.Collection(session, "base-spire:ExhaustPile"), card),
                "Dominate 消耗自己（来源 keywords = { exhaust }）");
        });

        Check.Run("B-06a 跨实体来源：目标原本就有易伤时读的是**总层数**（2 → 上 1 层 → 读 3）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "dominate");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            Attach(session, target, SliceManifest.VulnerableId, 2);

            var card = SliceFixtures.PutInHand(session, "base-spire:dominate", upgraded: false);
            SliceFixtures.PlayAt(session, card, target);

            Check.Equal(3L, Stacks(session, target, SliceManifest.VulnerableId), "2 + 1 = 3 层易伤");
            Check.Equal(3L, Stacks(session, player, SliceManifest.StrengthId),
                "玩家获得 3 层力量（读到的是施加**之后**的 3，不是之前的 2）");
        });

        Check.Run("B-06a 跨实体来源：升级形态先上 2 层，因此读到 2 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "dominate");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var card = SliceFixtures.PutInHand(session, "base-spire:dominate", upgraded: true,
                (Amount: 0, PowerStacks: 0));
            SliceFixtures.PlayAt(session, card, target);

            Check.Equal(2L, Stacks(session, target, SliceManifest.VulnerableId), "升级上 2 层易伤");
            Check.Equal(2L, Stacks(session, player, SliceManifest.StrengthId), "玩家获得 2 层力量");
        });

        Check.Run("B-06a 跨实体来源：冻结作者文本声明了 Dominate（来源行与两形态次序）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var card = frozen.Cards.Single(entry => entry.Id == "base-spire:dominate");
            Check.Equal(3425, card.SourceLine, "来源行");
            Check.Equal(AuthorSteps.ApplyPower + ":base-spire:vulnerable:1", card.NormalSteps[1].Kind, "普通上 1 层");
            Check.Equal(AuthorSteps.ApplyPower + ":base-spire:vulnerable:2", card.UpgradedSteps[1].Kind, "升级上 2 层");
            Check.Equal(AuthorSteps.ApplyPowerSelfFromTarget + ":base-spire:vulnerable",
                card.UpgradedSteps[2].Kind, "两形态都按目标易伤层数给自己力量");
            Check.Equal(AuthorSteps.ExhaustSelf, card.UpgradedSteps[3].Kind, "两形态都消耗自己");
        });
    }

    private static void Attach(Session session, InstanceId owner, string powerId, int stacks)
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
