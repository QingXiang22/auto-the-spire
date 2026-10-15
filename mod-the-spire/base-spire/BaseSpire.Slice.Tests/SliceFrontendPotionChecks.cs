using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第二十批：**药水首批**（`potion` 声明 + 药水栏 + `UsePotion` 动作）。
///
/// 药水的效果**复用牌的效果步骤词表**，因此自动继承既有步骤的全部校验与运行时语义。
/// 首批只做能被现有伤害/能力语义精确表达的两张（`BlockPotion` 与 `StrengthPotion`）——
/// `FirePotion` 带 `properties=[unpowered]`，需要先补出手方属性门控（见范围说明 §4）。
/// </summary>
internal static class SliceFrontendPotionChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x8 }
        potions { block-potion  strength-potion  fire-potion }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 120  tough-hp = 121  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 120  tough-hp = 121  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 3  steps = [target, damage] }
          upgraded { amount = 3  steps = [target, damage] }
        }
        potion block-potion {
          name = BlockPotion  rarity = common  usage = combat-only  target = self
          use { amount = 12  steps = [block] }
        }
        potion strength-potion {
          name = StrengthPotion  rarity = common  usage = combat-only  target = self
          use { amount = 0  power = strength  stacks = 2  steps = [apply_power] }
        }
        potion fire-potion {
          name = FirePotion  rarity = common  usage = combat-only  target = enemy
          use { amount = 20  steps = [target, damage] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 药水：解析 potion/potions，非法稀有度·用法·目标·步骤被拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "potion");
            Check.Equal(3, model.Potions.Count, "药水声明数");
            Check.Equal(3, model.Belt.Count, "药水栏条数");
            var block = model.Potions.Single(potion => potion.Id == "base-spire:block-potion");
            Check.Equal("common", block.Rarity, "稀有度");
            Check.Equal(PotionUsages.CombatOnly, block.Usage, "用法");
            Check.Equal(PotionTargets.Self, block.Target, "目标");
            Check.Equal(12, block.Amount, "数值");
            Check.Sequence([AuthorSteps.Block], block.Steps.Select(step => step.Kind), "步骤表");
            Check.Equal(PotionTargets.Enemy,
                model.Potions.Single(potion => potion.Id == "base-spire:fire-potion").Target, "敌人目标药水");

            (string Text, string Code, string Path)[] rejected =
            [
                (ScenarioText.Replace("rarity = common", "rarity = mythic"), "invalid_value", "rarity"),
                (ScenarioText.Replace("usage = combat-only", "usage = anywhere"), "invalid_value", "usage"),
                (ScenarioText.Replace("target = self", "target = everyone"), "invalid_value", "target"),
                (ScenarioText.Replace("steps = [block] }", "steps = [block]  retain = true }"), "unknown_key", "retain"),
                (ScenarioText.Replace("steps = [block]", "steps = [block, summon]"), "unknown_step", "steps"),
                // self 目标却声明 damage
                (ScenarioText.Replace("use { amount = 12  steps = [block] }",
                    "use { amount = 12  steps = [target, damage] }"), "unsupported_combination", "steps"),
            ];
            foreach (var (text, code, path) in rejected)
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "potion");
                    Check.True(false, $"必须被拒绝（{code} / {path}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{path}）");
                    Check.True(failure.Path.Contains(path, StringComparison.Ordinal) ||
                               failure.Message.Contains(path, StringComparison.Ordinal),
                        $"路径应指向 {path}：{failure.Path} / {failure.Message}");
                }
            }
        });

        Check.Run("B-05c 药水：自我目标药水生效、离开栏位、且不消耗能量 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "potion");
            var session = Build(model);
            var belt = SliceFixtures.Collection(session, "base-spire:PotionBelt");
            var player = SliceFixtures.Player(session);

            Check.Equal(3, session.State.CollectionMembers(belt).Count, "开局药水栏里有 3 瓶");
            var energyBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            var blockBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");

            UsePotion(session, "base-spire:block-potion");

            Check.Equal(blockBefore + 12, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "格挡 +12（按声明数值）");
            Check.Equal(2, session.State.CollectionMembers(belt).Count, "用掉的那瓶离开药水栏");
            Check.Equal(energyBefore, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "药水不消耗能量");

            // 力量药水：给自己 +2 力量，随后打击多造成 2 点。
            UsePotion(session, "base-spire:strength-potion");
            Check.Equal(2L, Stacks(session, player, SliceManifest.StrengthId), "玩家获得 2 层力量");
            Check.Equal(1, session.State.CollectionMembers(belt).Count, "第二瓶也离开药水栏");

            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(5L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "力量立刻生效（3 + 2）");
        });

        Check.Run("B-05c 药水：敌人目标药水先提问再结算，且只消耗那一瓶 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "potion");
            var session = Build(model);
            var belt = SliceFixtures.Collection(session, "base-spire:PotionBelt");
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);

            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == "base-spire:UsePotion");
            var fire = session.State.CollectionMembers(belt).First(potion =>
                session.State.GetField(potion,
                    session.RuleSet.TypeByQualifiedId("base-spire:Potion").FieldIndex("DefinitionId")).AsString()
                == "base-spire:fire-potion");
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(fire)]);

            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "敌人目标药水先提问（与牌的选择同形）");
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));

            Check.Equal(100L, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "所选敌人受到 20 点（120 - 20）");
            Check.Equal(120L, SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                "另一个敌人未受伤");
            Check.Equal(2, session.State.CollectionMembers(belt).Count, "只消耗了那一瓶");
            Check.False(session.State.IsMember(belt, fire), "被使用的那瓶已离开药水栏");
        });

        Check.Run("W-01 药水栏槽位：容量 = SliceManifest.PotionSlotCount，越界由引擎拒绝 [真实]", () =>
        {
            // 槽位数是**声明的容量**（来源见 SliceManifest.PotionSlotCount 的注释）；
            // 公开侧（net 作者 Profile 的 group max_count）从这里推导，因此必须有行为证据。
            var frozen = SliceAuthorFrontend.Load();
            Check.True(frozen.Belt.Sum(entry => entry.Count) <= SliceManifest.PotionSlotCount,
                $"冻结作者文本的药水 {frozen.Belt.Sum(entry => entry.Count)} 瓶不得超过 " +
                $"{SliceManifest.PotionSlotCount} 个槽位");

            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build(fromAuthorText: true).Build());
            var rules = builder.Build();
            var belt = rules.Collections.Single(collection => collection.QualifiedId == "base-spire:PotionBelt");
            Check.Equal(SliceManifest.PotionSlotCount, belt.Capacity, "药水栏的声明容量 = 槽位数");

            // 越界：声明 4 瓶（> 3 个槽）。引擎必须在 setup 阶段按 CapacityExceeded 拒绝，
            // 而不是静默丢弃多余药水（静默丢弃等于让公开的"已装数量"与真实状态不一致）。
            var overflow = ScenarioText.Replace("potions { block-potion  strength-potion  fire-potion }",
                "potions { block-potion  strength-potion  fire-potion x2 }");
            Check.True(!string.Equals(overflow, ScenarioText, StringComparison.Ordinal),
                "越界场景确实改到了 potions 声明");
            var model = SliceAuthorFrontend.Parse(overflow, "potion-overflow");
            Check.Equal(4, model.Belt.Sum(entry => entry.Count), "越界场景声明 4 瓶药水");
            try
            {
                Build(model);
                Check.True(false, "超过槽位的药水必须被拒绝（CapacityExceeded），不得静默丢弃");
            }
            catch (EngineFaultException failure)
            {
                Check.Equal(EngineFaultCode.CapacityExceeded, failure.Code, "越界必须报 CapacityExceeded");
            }
        });

        Check.Run("B-05c 药水：冻结作者文本没有药水（默认路径形状不变）", () =>
        {
            // 冻结文本现在声明了 BeetleJuice（Shrink 药水）；程序化路径**没有**药水代码路径。
            var frozen = SliceAuthorFrontend.Load();
            Check.Equal(2, frozen.Potions.Count, "冻结文本声明 2 瓶药水（BeetleJuice / BlessingOfTheForge）");
            Check.Equal(2, frozen.Belt.Count, "药水栏里都有");

            // 默认路径的包里不应出现药水类型/动作：形状与之前完全一致。
            var plain = new RuleSetBuilder();
            plain.Add(CombatSlicePackage.Build().Build());
            var rules = plain.Build();
            Check.False(rules.Types.Any(type => type.QualifiedId == "base-spire:Potion"), "没有药水类型");
            Check.False(rules.Actions.Any(action => action.QualifiedId == "base-spire:UsePotion"), "没有使用动作");
        });
    }

    private static void UsePotion(Session session, string potionId)
    {
        var belt = SliceFixtures.Collection(session, "base-spire:PotionBelt");
        var potionType = session.RuleSet.TypeByQualifiedId("base-spire:Potion");
        var potion = session.State.CollectionMembers(belt).First(candidate =>
            session.State.GetField(candidate, potionType.FieldIndex("DefinitionId")).AsString() == potionId);
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:UsePotion");
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(potion)]);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId,
                [session.PendingDecision!.Candidates[0]]));
        }
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
