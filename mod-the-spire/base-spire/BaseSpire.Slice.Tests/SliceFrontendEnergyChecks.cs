using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第十三小批）：**能量获取 + "本回合不能再获得能量"** + `ExpectAFight`。
///
/// 来源（两处）：
/// <list type="bullet">
/// <item>`ContentSource/ironclad-powers.spire:19` —— `power NoEnergyGainPower { preventEnergyGain = true
///   removeAfterOwnerTurnEnd = true }`；</item>
/// <item>`ContentSource/ironclad-cards.spire:1192` —— `card ExpectAFight`：
///   `modifyEnergy { amount = scaled { base = 0  perCount = 1
///   count = { pileCount { pile = hand  type = attack } } } }`（普通费用 2、升级 **1**）
///   + `applyPower { power = NoEnergyGainPower  stacks = 1  target = context.owner }`。</item>
/// </list>
///
/// 三件新东西：`gain_energy` 步骤（数量取卡牌 Amount，可由 `amount-source` 决定）、
/// `hand-count:&lt;Kind&gt;`（按牌类过滤的手牌计数）、`prevent-energy-gain`（**声明驱动**的能量门控）。
/// </summary>
internal static class SliceFrontendEnergyChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x8  defend-ironclad x4  expect-a-fight x2 }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        power no-energy-gain {
          name = NoEnergyGainPower
          role = NoEnergyGainPower
          effect = "cannot gain Energy this turn"
          source = base-spire/ContentSource/ironclad-powers.spire
          line = 19
          prevent-energy-gain = true
          owner-turn-end = remove
        }
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
        card defend-ironclad {
          name = DefendIronclad  kind = Skill  cost = 0
          normal   { amount = 5  steps = [block] }
          upgraded { amount = 5  steps = [block] }
        }
        card expect-a-fight {
          name = ExpectAFight  kind = Skill  cost = 2
          source = base-spire/ContentSource/ironclad-cards.spire
          line = 1192
          normal   { cost = 2  amount-source = hand-count:Attack  power = no-energy-gain  stacks = 1  steps = [gain_energy, apply_power_self] }
          upgraded { cost = 1  amount-source = hand-count:Attack  power = no-energy-gain  stacks = 1  steps = [gain_energy, apply_power_self] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 能量：解析 `hand-count:<Kind>`、`gain_energy`、`prevent-energy-gain`；非法值被拒", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "energy");
            var card = model.Cards.Single(entry => entry.Id == "base-spire:expect-a-fight");
            Check.Equal(1192, card.SourceLine, "来源行");
            Check.Equal(AmountSource.HandCountPrefix + "Attack", card.NormalAmountSource, "按攻击牌数量");
            Check.Sequence([AuthorSteps.GainEnergy, AuthorSteps.ApplyPowerSelf], card.NormalSteps.Select(step => step.Kind),
                "先获得能量，再禁止本回合继续获得");
            Check.Equal(2, card.NormalCost, "普通费用 2");
            Check.Equal(1, card.UpgradedCost, "升级费用 1");

            var power = model.Powers.Single(entry => entry.Id == "base-spire:no-energy-gain");
            Check.True(power.PreventEnergyGain, "禁止获得能量");
            Check.Equal(PowerTurnEnd.Remove, power.OwnerTurnEnd, "回合末整体移除");
            Check.Equal(19, power.SourceLine, "能力来源行");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("hand-count:Attack", "hand-count:Spell"), "invalid_value"),
                         (ScenarioText.Replace("prevent-energy-gain = true", "prevent-energy-gain = maybe"),
                             "invalid_value"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "energy");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                }
            }
        });

        Check.Run("B-06a 能量：ExpectAFight 按**手牌里攻击牌数量**获得能量，随后本回合**拿不到**能量 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "energy");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);

            // 手牌里攻击牌的数量（不含正在打出的这张：它在打出时已离开手牌）
            var attacksInHand = session.State.CollectionMembers(hand).Count(held =>
                session.State.GetField(held, cardType.FieldIndex("DefinitionId")).AsString() == SliceManifest.StrikeId);
            Check.True(attacksInHand > 0, "手里有攻击牌");

            var energyBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            var card = SliceFixtures.PutInHand(session, "base-spire:expect-a-fight", upgraded: false);
            SliceFixtures.PlayAt(session, card, player);

            // 花 2 点、得 attacksInHand 点
            Check.Equal(energyBefore - 2 + attacksInHand,
                SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                $"花 2 得 {attacksInHand}（= 打出时手牌里的攻击牌数）");
            Check.Equal(1L, Stacks(session, player, "base-spire:no-energy-gain"), "获得 NoEnergyGainPower");

            // 再打一张能获得能量的牌：被门控挡住（用第二张 ExpectAFight 验证）。
            var second = SliceFixtures.PutInHand(session, "base-spire:expect-a-fight", upgraded: false);
            var beforeSecond = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            SliceFixtures.PlayAt(session, second, player);
            // 第二张的费用是 2；门控生效时**只扣费用、不加能量**（若门控失效，这里会多出"手牌攻击牌数"点）。
            // 反向对照在下一项检查里：回合末门控被移除后，同样的牌**会**加能量。
            Check.Equal(-2L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy") - beforeSecond,
                "本回合拿不到能量：只扣 2 点费用");
        });

        Check.Run("B-06a 能量：回合末移除'禁止获得能量'，下一回合恢复 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "energy");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var card = SliceFixtures.PutInHand(session, "base-spire:expect-a-fight", upgraded: false);
            SliceFixtures.PlayAt(session, card, player);
            Check.Equal(1L, Stacks(session, player, "base-spire:no-energy-gain"), "打出后持有该能力");

            EndTurn(session);
            Check.Equal(0L, Stacks(session, player, "base-spire:no-energy-gain"), "回合末整体移除（不是减层）");

            // 下一回合再打一张：这次能量真的加上了。
            var second = SliceFixtures.PutInHand(session, "base-spire:expect-a-fight", upgraded: true,
                (Amount: 0, PowerStacks: 1), upgradedCost: 1);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var attacksInHand = session.State.CollectionMembers(hand).Count(held =>
                session.State.GetField(held, cardType.FieldIndex("DefinitionId")).AsString() == SliceManifest.StrikeId);
            var before = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            var handBefore = string.Join(",", session.State.CollectionMembers(hand).Select(held =>
                session.State.GetField(held, cardType.FieldIndex("DefinitionId")).AsString()));
            SliceFixtures.PlayAt(session, second, player);
            var after = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            Check.Equal(before - 1 + attacksInHand, after,
                $"门控已移除：花 1（升级费用）得 {attacksInHand}；before={before} after={after} handBefore=[{handBefore}]");
        });

        Check.Run("B-06a 能量：冻结作者文本声明了 NoEnergyGainPower 与 ExpectAFight", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var power = frozen.Powers.Single(entry => entry.Id == "base-spire:no-energy-gain");
            Check.True(power.PreventEnergyGain, "禁止获得能量");
            Check.Equal(19, power.SourceLine, "能力来源行");
            var card = frozen.Cards.Single(entry => entry.Id == "base-spire:expect-a-fight");
            Check.Equal(1192, card.SourceLine, "卡来源行");
            Check.Equal(2, card.NormalCost, "普通 2");
            Check.Equal(1, card.UpgradedCost, "升级 1");
            Check.Equal(AmountSource.HandCountPrefix + "Attack", card.UpgradedAmountSource, "两形态都按攻击牌数");
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
